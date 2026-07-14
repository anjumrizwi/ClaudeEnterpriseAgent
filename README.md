# Claude Enterprise Agent

A .NET console agent that talks to Claude via the Anthropic API, with tool-calling and memory support.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An Anthropic API key

## Setup

1. Clone the repository:

   ```bash
   git clone https://github.com/anjumrizwi/ClaudeEnterpriseAgent.git
   cd ClaudeEnterpriseAgent
   ```

2. Restore dependencies:

   ```bash
   dotnet restore
   ```

3. Set your Anthropic API key using .NET user-secrets (keeps it out of source control):

   ```bash
   dotnet user-secrets set "Anthropic:ApiKey" "<your-api-key>"
   ```

   Optionally set a non-default model (defaults to `claude-sonnet-5`):

   ```bash
   dotnet user-secrets set "Anthropic:Model" "<model-id>"
   ```

   Alternatively, set environment variables instead of user-secrets:

   ```bash
   export Anthropic__ApiKey="<your-api-key>"
   export Anthropic__Model="<model-id>"
   ```

## Run

```bash
dotnet run
```

Type a message at the `>` prompt and press Enter. Type `exit` to quit.

## Project structure

- `Program.cs` — entry point, configuration, and REPL loop
- `Clients/` — Anthropic API client wrapper
- `Agents/` — agent orchestration logic
- `Services/` — tool dispatch and memory services
- `Tools/` — individual tool implementations (calculator, date/time, file)
- `Models/` — message data models

See [.claude/ARCHITECTURE.md](.claude/ARCHITECTURE.md) for a detailed description of every file.
