# CLAUDE.md — Claude Code for Visual Studio (this repository)

Rules for working on THIS extension's own source code with an AI assistant.
(Deliberately generic — this file ships to the public build mirror too.)

## Ground rules (no exceptions)

- **Understand BEFORE you act.** Read the relevant files and follow the data flow before
  claiming anything. Every claim about code carries a `file:line` you actually read.
  Missing context or an unclear concept → SAY exactly what is missing instead of guessing.
- **Golden rule for edits**: before changing anything, state precisely
  "I would change X and Y (file:line), like this, because Z — agree?" and wait.
  In Auto/Bypass mode still announce what and why (transparency stays).
- Never claim "done/works" without evidence (build output, test run, concrete check).
- Files carrying a `LOCKED FOR AI EDIT` comment are never edited (a PreToolUse hook
  enforces this in every mode) — propose the change to the user instead.

## What this is

A Visual Studio (2017–2026) extension wrapping the Claude Code CLI as a native
tool-window chat. One shared codebase, three VSIX flavors (linked `.cs` files).

| Area | Where | Notes |
|---|---|---|
| Chat window host (C#) | `src/ClaudeCode.VisualStudio/ToolWindows/ClaudeChatControl.cs` | message dispatch, session lifecycle, persistence |
| CLI driver | `src/ClaudeCode.VisualStudio/Services/ClaudeSession.cs` | bidirectional stream-json, `--resume`, `--effort`, `--model` rules |
| Session persistence + history | `src/ClaudeCode.VisualStudio/Services/SessionStore.cs` | DPAPI-encrypted per-workspace store + archive (v0.4) |
| Chat UI (WebView2) | `src/ClaudeCode.VisualStudio/media/` (`app.js`, `index.html`, `style.css`) | vanilla JS, message handlers map, top-bar popovers |
| Auth/account detection | `src/ClaudeCode.VisualStudio/Services/AccountService.cs` | env-key vs OAuth; third-party host detection |
| VS commands / menu | `Commands/`, `VSCommandTable.vsct`, `Vsct/PackageIds.cs` | ids must match between .vsct and PackageIds.cs |
| Tests (MSTest, net48) | `tests/ClaudeCode.VisualStudio.Tests/` | run on Windows; CI runs them on every push |
| VS2017/2019 flavors | `src/ClaudeCode.VisualStudio.Vs2017|Vs2019/` | link the SAME .cs files — an edit affects all flavors |

## Conventions that MUST hold

- An edit to shared `.cs` files affects all three VSIX flavors — check `#if VS2017 || VS2019`
  guards before using APIs newer than the 2017/2019 SDKs.
- `media/app.js` is plain browser JS (no build step): after editing run a syntax check
  (`node --check media/app.js`) and keep the existing handler-map / popover patterns.
- Tool-window id 0 is the primary (persists the workspace session); ids > 0 are scratch
  windows and must NEVER write to `SessionStore` (see v0.4.1 fix — regression risk).
- The `.csproj` files list `Compile` items EXPLICITLY (old-style projects): a NEW `.cs`
  file must be added to ALL THREE csproj files (main + Vs2017/Vs2019 linked entries) or it
  silently never ships — the build stays green and the class just doesn't exist (bit us
  in v0.4.0-0.4.11 with two command classes).
- Version lives in THREE `source.extension.vsixmanifest` files + `ClaudeCodePackage.cs`
  + the `init` payload in `ClaudeChatControl.cs` — bump all five together.
- New user-facing strings: English in the extension UI.

## Build & test (Windows)

```powershell
# from the repo root (any PS with Visual Studio installed; VSSDK targets come via NuGet):
msbuild -t:Restore .\ClaudeCode.sln
msbuild -t:Build -p:Configuration=Release .\ClaudeCode.sln       # produces the .vsix files
dotnet test .\tests\ClaudeCode.VisualStudio.Tests                # MSTest suite
```

To see a change live: install the rebuilt `.vsix` (double-click → Install) and restart VS.
For debugging, F5 launches the VS experimental instance.

## Git

- Work on a branch (`<name>/<topic>`), descriptive commit messages, no AI trailers.
- Push rather than keeping local-only work — CI builds and tests every push.
