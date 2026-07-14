# Architecture and File Reference

This document describes what each file in the project does and how they fit together.

## Overview

The app is a .NET 8 console program that runs a REPL: the user types a message, it's sent to
Claude via the Anthropic API, and Claude's reply is printed. Claude can call one of three
built-in tools (calculator, date/time, sandboxed file access) mid-conversation; the agent
executes the tool locally and feeds the result back to Claude before returning a final answer.
Conversation history is persisted to a JSON file on disk so it survives across runs.

Request flow: `Program.cs` → `ClaudeAgent` → `AnthropicClient` (Anthropic API) → back to
`ClaudeAgent`, which dispatches any tool calls through `ToolDispatcher` → the individual
`Tools/*` implementations, then loops until Claude returns a final text answer, which
`MemoryService` persists.

## `Program.cs`

Entry point. Builds configuration from .NET user-secrets and environment variables, reads
`Anthropic:ApiKey` and `Anthropic:Model` (default `claude-sonnet-5`), and exits with an error
message if no API key is configured. Constructs `AnthropicClient`, `ToolDispatcher`,
`MemoryService`, and `ClaudeAgent`, then runs a loop that reads a line from stdin, sends it to
the agent via `SendAsync`, and prints the response. Typing `exit` (or an empty line) ends the
loop. Exceptions from a single turn are caught and printed without crashing the REPL.

## `ClaudeEnterpriseAgent.csproj`

Project file. Targets `net8.0`, produces an executable, and declares a `UserSecretsId` so
`dotnet user-secrets` can store the API key outside of source control. Package references:
`Anthropic` (the Claude SDK client), and `Microsoft.Extensions.Configuration` plus its
`EnvironmentVariables` and `UserSecrets` providers (used to layer config sources in
`Program.cs`).

## `Agents/ClaudeAgent.cs`

Orchestrates a single conversational turn end-to-end, including the tool-use loop.

- Holds `_history`, the full list of `MessageParam` exchanged so far, seeded on construction
  from whatever `MemoryService.Load()` returns (so history survives process restarts).
- `SendAsync(userMessage)`: appends the user's message to history, then loops:
  1. Calls `AnthropicClient.CreateMessageAsync` with the full history and the tool
     definitions from `ToolDispatcher.GetTools()`.
  2. Walks the response content blocks. Text blocks are kept as-is; `tool_use` blocks are
     recorded and immediately executed via `ToolDispatcher.Dispatch`, producing a
     `ToolResultBlockParam` keyed by the tool call's ID.
  3. Appends the assistant's turn (text + tool-use blocks) to history.
  4. If the response's `StopReason` is `tool_use`, appends the tool results as a new user
     turn and loops again so Claude can see the results and continue.
  5. Otherwise, concatenates the text blocks into the final answer, appends the
     user/assistant pair to `MemoryService`, and returns the answer.

This means a single call to `SendAsync` may make multiple round-trips to the Anthropic API if
Claude chains several tool calls before producing a final answer.

## `Clients/AnthropicClient.cs`

Thin wrapper around the `Anthropic` SDK client (aliased as `SdkClient` to avoid a name clash
with this class).

- Constructed with an API key and model name; wraps `Anthropic.AnthropicClient`.
- `SendMessageAsync(userMessage)`: convenience method for a single-turn, tool-free message —
  sends one user message and concatenates any text blocks in the reply. Not used by
  `ClaudeAgent` (which needs tool support and full history) but useful for quick testing.
- `CreateMessageAsync(messages, tools)`: the method `ClaudeAgent` actually uses. Sends the full
  message history and optional tool definitions, returns the raw SDK `Message` response
  (including content blocks and `StopReason`) for the agent to interpret.

Both methods use `MaxTokens = 1024`, so long responses will be truncated — increase this if
you need longer completions.

## `Models/Message.cs`

A single-line record: `Message(string Role, string Content)`. This is the persisted-history
shape used by `MemoryService` — deliberately simpler than the SDK's `MessageParam`/content-block
types, since only plain text (not tool calls) is persisted across sessions.

## `Services/MemoryService.cs`

Simple JSON-file-backed conversation store.

- Defaults to `<current working directory>/workspace/memory.json` if no path is given.
- `Load()`: reads and deserializes the file into `List<Message>`, or returns an empty list if
  the file doesn't exist or fails to deserialize.
- `Append(message)`: loads the existing history, adds one message, and re-saves the whole
  file (not append-only at the byte level — it rewrites the full JSON array each time).
- `Save(history)`: creates the containing directory if needed and writes indented JSON.
- `Clear()`: deletes the memory file, effectively resetting history.

This is a small append-and-rewrite log, not a streaming/append-only file format — fine for the
REPL's scale but will get slower as history grows large.

## `Services/ToolDispatcher.cs`

Bridges the three concrete tool classes (`CalculatorTool`, `DateTimeTool`, `FileTool`) to the
Anthropic SDK's tool-calling protocol.

- `_handlers`: a name → function map. Each entry pulls typed arguments out of the raw
  `IReadOnlyDictionary<string, JsonElement>` input Claude provides (via the `GetRequired*`/
  `GetOptional*` helpers) and calls the corresponding tool's `Execute` method.
- `ToolDefinitions`: builds the `name` / `description` / `input_schema` JSON shape for all
  three tools, sourced from each tool's own `Name`, `Description`, `InputSchema` properties.
- `GetTools()`: round-trips `ToolDefinitions` through `JsonSerializer` to reshape it into the
  SDK's strongly-typed `Tool` objects (via the private `RawToolDefinition`/`RawInputSchema`
  records) so they can be passed to `AnthropicClient.CreateMessageAsync`. This
  serialize-then-deserialize approach lets the tool classes describe their schema with plain
  anonymous objects instead of depending on the SDK's schema types directly.
- `Dispatch(toolName, input)`: looks up the handler, invokes it, and wraps the result (or any
  exception's message) in a `ToolResult(Content, IsError)`. Unknown tool names return an
  `IsError: true` result instead of throwing, so a single bad tool call doesn't crash the
  agent loop.

## `Tools/CalculatorTool.cs`

Tool name `calculator`. Performs `add`, `subtract`, `multiply`, or `divide` on two numbers
(`a`, `b`). Divide-by-zero throws `DivideByZeroException`; an unrecognized operation throws
`ArgumentException`. Both are caught by `ToolDispatcher.Dispatch` and surfaced to Claude as an
error result rather than crashing the app.

## `Tools/DateTimeTool.cs`

Tool name `get_datetime`. Returns the current date/time, defaulting to UTC in ISO 8601
(`"O"` format). Accepts an optional IANA `timezone` (resolved via
`TimeZoneInfo.FindSystemTimeZoneById`, with unknown/invalid IDs raising a clear
`ArgumentException`) and an optional .NET custom `format` string.

## `Tools/FileTool.cs`

Tool name `file`. Provides `read`, `write`, `list`, and `delete` operations, sandboxed to a
root directory (default `<current working directory>/workspace`, created if missing).

- `ResolvePath` combines the sandbox root with the requested relative path and calls
  `Path.GetFullPath`, then `IsWithinRoot` verifies the resolved path is still inside the
  sandbox root (case-insensitive prefix check) before any file operation runs — this is the
  guard against path-traversal (e.g. `../../etc/passwd`) escaping the workspace directory.
- `write` requires non-null `content` and creates parent directories as needed.
- `list` returns file/directory names (not full paths) joined by newlines.
- `delete` removes a file or, if the path is a directory, recursively deletes it.

Because tool calls originate from whatever Claude decides to send, this sandboxing is the
main safety boundary preventing the agent from touching files outside `workspace/`.

## `.vscode/launch.json` and `.vscode/tasks.json`

Minimal VS Code debugger/task scaffolding (just version stubs) so the folder opens cleanly in
VS Code. No custom debug configurations or build tasks are defined yet.

## `.gitignore`

Excludes .NET build output (`bin/`, `obj/`), Visual Studio's `.vs/` cache, and most of
`.vscode/` (keeping only `settings.json`, `tasks.json`, `launch.json`, `extensions.json` if
present).

## `LICENSE`

MIT License.

## `.claude/skills/pr-description/SKILL.md`

Claude Code skill that drafts a PR description in Valtech's standard format (Summary, Changes,
Test plan) from the current branch's diff and commit history. Invoked via `/pr-description`.
