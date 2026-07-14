# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

- Build: `dotnet build`
- Run: `dotnet run` (starts the REPL; type a message at the `>` prompt, `exit` or an empty line to quit)
- Restore dependencies: `dotnet restore`
- Configure the API key (required to run, keeps it out of source control):
  ```bash
  dotnet user-secrets set "Anthropic:ApiKey" "<your-api-key>"
  dotnet user-secrets set "Anthropic:Model" "<model-id>"   # optional, defaults to claude-sonnet-5
  ```
  or via environment variables: `Anthropic__ApiKey`, `Anthropic__Model`.

There is no test project in this repo yet — no `dotnet test` target exists.

## Architecture

.NET 8 console app running a single-loop REPL that talks to Claude via the Anthropic SDK, with tool-calling and disk-persisted conversation memory. See `.claude/ARCHITECTURE.md` for a file-by-file reference; the key structural points:

- **Request flow**: `Program.cs` → `Agents/ClaudeAgent.cs` → `Clients/AnthropicClient.cs` (Anthropic API) → back to `ClaudeAgent`, which dispatches any tool calls through `Services/ToolDispatcher.cs` → `Tools/*`, looping until Claude returns a final text answer, which `Services/MemoryService.cs` persists.
- **Tool-use loop lives in `ClaudeAgent.SendAsync`**: a single call can make multiple round-trips to the Anthropic API if Claude chains several tool calls (loops while `StopReason == tool_use`) before producing a final text answer.
- **Tools are self-describing**: each class in `Tools/` (`CalculatorTool`, `DateTimeTool`, `FileTool`) exposes its own `Name`, `Description`, and `InputSchema`; `ToolDispatcher` builds the Anthropic tool definitions from these rather than hardcoding schemas centrally, and round-trips them through `JsonSerializer` to reshape into the SDK's typed `Tool` objects.
- **`FileTool` is sandboxed** to a `workspace/` root — every path is resolved and checked against the root directory before any read/write/list/delete to prevent path traversal escaping the sandbox. This is the main safety boundary since tool calls originate from whatever Claude decides to send.
- **Memory persistence is a full-file JSON rewrite**, not append-only: `MemoryService.Append` loads the whole history, adds one message, and re-saves the entire file. Defaults to `workspace/memory.json`.
- **`Models/Message.cs`** is a minimal `(Role, Content)` record used only for persisted history — deliberately simpler than the SDK's `MessageParam`/content-block types, since only plain text (not tool calls) is persisted across sessions.
- Configuration is layered via `Microsoft.Extensions.Configuration`: user-secrets, then environment variables (`Anthropic:ApiKey`, `Anthropic:Model`).

## If you're new to this codebase

- **Adding a new tool touches two places, not one**: create `Tools/YourTool.cs` implementing `Name`/`Description`/`InputSchema`/`Execute` (follow `CalculatorTool.cs` as the simplest example), then register it in `Services/ToolDispatcher.cs`'s `_handlers` map and `ToolDefinitions` list. A tool class that exists but isn't wired into `ToolDispatcher` will silently never be offered to Claude.
- **A tool throwing an exception won't crash the app or show a stack trace** — `ToolDispatcher.Dispatch` catches it and sends the exception message back to Claude as an error result. If a tool "isn't working" in the REPL, check `ToolDispatcher`'s error handling and the tool's own `Execute` logic before assuming the SDK call failed.
- **Responses can look cut off mid-sentence**: `MaxTokens = 1024` is hardcoded in both methods in `Clients/AnthropicClient.cs`. That's the first thing to check/raise if you see truncated output.
- **To reset conversation history while testing**, delete `workspace/memory.json` or call `MemoryService.Clear()` — the REPL reloads history from that file on every `Program.cs` startup, so stale test data will keep reappearing otherwise.
- **There's no automated test project.** Verify changes by running `dotnet run` and exercising the REPL manually (send a message that triggers each tool you touched) rather than assuming `dotnet build` succeeding means the change works.

## Claude Code skills in this repo

- `.claude/skills/pr-description/SKILL.md` — drafts a PR description in the standard Summary/Changes/Test plan format from the current branch's diff and commit history. Invoke via `/pr-description`.
