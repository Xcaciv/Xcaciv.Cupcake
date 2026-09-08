# ChatDbg — Feature Inventory (Phase 1 Reconnaissance)

> Source: `subject/chatdbg` (origin `https://github.com/Xcaciv/chatdbg.git`)
> Pinned commit: `d8c18f61d6bb73666ed97cd4885e877e35558485` on branch `LLamaSharp_support`
> Surveyed: 2026-08-28. License: GPL-3.0 (LICENSE, 35 KB, GNU GPL v3 text).
> Size: 47 commits, 12,846 lines of C# across 4 projects (2 shells, 1 core library, 1 test project).

## Conventions Note (given verbatim to every sub-agent)

- **Language/runtime**: C# targeting `net10.0` (`LangVersion=latest`, `ImplicitUsings=enable`, `Nullable=enable`, `AllowUnsafeBlocks=true` in Core). SDK pinned in `global.json` to `10.0.100-rc.1.25451.107` with `rollForward: latestFeature`.
- **Solution layout**:
  - `src/Xcaciv.ChatDbg.Core/` — class library holding all domain logic: `Models/`, `Services/`, `Services/TokenInspection/`, `Commands/`.
  - `src/ChatDbg/` (assembly `Xcaciv.ChatDbg.Shell`) — plain-console REPL shell.
  - `src/ChatDbg.Shell.Gui/` (assembly `Xcaciv.ChatDbg.Shell.Gui`) — Terminal.Gui full-screen shell with `UI/` and `Services/`.
  - `src/Xcaciv.ChatDbg.Core.Tests/` — xUnit + Moq test project mirroring Core's folder structure (`Commands/`, `Models/`, `Services/`, `Services/TokenInspection/`, `TestDoubles/`).
- **Naming idioms**: interfaces `I<Name>`; services `<Name>Service`; commands `<Verb>Command` implementing `ICommand`; DTO/state classes in `Models/`. Namespaces are `Xcaciv.ChatDbg.Core.*` for the library, but the console shell uses the legacy namespace `ChatDBG`.
- **Command convention**: every user-facing command implements `Xcaciv.ChatDbg.Core.Models.ICommand` (`Name`, `Description`, `Usage`, `Task<CommandResult> ExecuteAsync(string[] args)`), is constructed with its collaborators by hand in the shell's `InitializeCommands()`, and is stored in a `Dictionary<string, ICommand>` keyed by `Name`. There is **no** dependency-injection container anywhere in the product; all wiring is `new`-in-constructor.
- **Command surface**: user input beginning with `/` is a command; everything else is a chat turn. Argument parsing is ad-hoc `string.Split` inside each command.
- **Result convention**: `CommandResult` with `Success`, `Message?`, `ExitRequested`, and static factories `SuccessResult`/`ErrorResult`/`ExitResult`.
- **Persistence convention**: `System.Text.Json` with explicit `[JsonPropertyName]` on every persisted member and `[JsonIgnore]` on computed/secret members. Settings live under the user profile; system prompts under local app data.
- **Async convention**: `async Task` on all service and command entry points; no cancellation tokens anywhere.
- **Tests**: xUnit 2.9.1 with Moq 4.20.69 and `coverlet.collector`; test names read as `Method_Condition_ExpectedOutcome`. A hand-rolled `StubHttpMessageHandler` under `TestDoubles/` stands in for network calls, and factory interfaces (`IAzureOpenAIClientFactory`, `IBedrockRuntimeClientFactory`) exist purely to make the provider services testable.
- **Where things live**: entry points `src/ChatDbg/Program.cs` and `src/ChatDbg.Shell.Gui/Program.cs`; the REPL/orchestration in each shell's `ChatShell.cs`; all provider calls in `Core/Services/*Service.cs`.
- **Documentation set**: `README.md` (14.5 KB user manual, the most authoritative behavioral doc), `IMPLEMENTATION_SUMMARY.md`, and 14 files in `docs/` — several are aspirational/status documents describing partially-complete work; treat code as truth and docs as hints.

## Git Churn Hot Spots (where the product's living heart is)

| Rank | Path | Commits touching |
|---|---|---|
| 1 | `src/ChatDbg/ChatShell.cs` | 10 |
| 1 | `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs` | 10 |
| 3 | `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs` | 9 |
| 4 | `src/ChatDbg.Shell.Gui/ChatShell.cs` | 6 |
| 5 | `Core/Services/{Bedrock,AzureOpenAI}Service.cs`, `Models/ChatSettings.cs`, `Commands/DemoLogProbsCommand.cs` | 5 each |

The most recent ~15 commits are all local-LLM / token-probability work, which is the product's current frontier. Cloud provider code is stable.

## Feature Inventory

| # | Name | Kind | Evidence paths | Depends on | Complexity |
|---|---|---|---|---|---|
| 1 | Interactive Chat Session (Console Shell) | user-facing feature | `src/ChatDbg/ChatShell.cs`, `src/ChatDbg/Program.cs` | 3, 4, 5, 8, 14 | L |
| 2 | Terminal GUI Shell | user-facing feature | `src/ChatDbg.Shell.Gui/**` (`Program.cs`, `ChatShell.cs`, `UI/ChatWindow.cs`, `UI/SettingsDialog.cs`, `UI/SystemPromptsDialog.cs`, `UI/ThemeManager.cs`, `UI/LogProbHeatmapView.cs`) | 3, 4, 5, 7, 8, 14 | L |
| 3 | Command System & Dispatch | platform capability | `Core/Models/ICommand.cs`, `Core/Models/CommandResult.cs`, `Core/Commands/HelpCommand.cs`, `Core/Commands/{Exit,Quit}Command.cs`, shells' `InitializeCommands`/`ProcessCommandAsync` | — | M |
| 4 | Chat History Management | user-facing feature | `Core/Models/{ChatHistory,ChatMessage}.cs`, `Core/Services/ChatHistoryService.cs`, `Core/Commands/{Inject,Pop,Clear,Import,Export}Command.cs` | 3 | M |
| 5 | Settings & Configuration | platform capability | `Core/Models/ChatSettings.cs`, `Core/Services/SettingsService.cs`, `Core/Commands/SetCommand.cs`, `Core/Commands/ModelCommand.cs` | 3, 6 | L |
| 6 | Credential Management & Secret Storage | platform capability | `Core/Models/WindowsCredentialManager.cs`, `ChatSettings.GetSecureValue`/`GetCredentialSource`, `SettingsService` credential methods, `docs/SECURITY-IMPLEMENTATION.md`, `docs/WINCRED-IMPLEMENTATION.md`, `tmp/test-wincred.cmd`, `tmp/test-env-vars.cmd` | — | M |
| 7 | System Prompt Management | user-facing feature | `Core/Models/SystemPrompt.cs`, `Core/Services/SystemPromptService.cs`, `Core/Commands/PromptCommand.cs`, `Gui/UI/SystemPromptsDialog.cs` | 3, 5 | M |
| 8 | AI Provider Abstraction & Azure OpenAI Integration | integration | `Core/Services/IAIService.cs`, `Core/Services/AzureOpenAIService.cs`, `Core/Services/{I,Default}AzureOpenAIClientFactory.cs`, `Core/Models/AIResponse.cs` | 5, 6, 11 | L |
| 9 | Amazon Bedrock Integration | integration | `Core/Services/BedrockService.cs`, `Core/Services/{I,Default}BedrockRuntimeClientFactory.cs` | 5, 6, 8, 11 | M |
| 10 | Local LLM Inference (LLamaSharp / GGUF) | integration | `Core/Services/LLamaSharpService.cs`, `docs/LLamaSharp-*.md`, `tmp/LLamaSharpInvestigation.cs` | 5, 8, 11, 13 | L |
| 11 | Token Probability Analysis (Log Probabilities) | user-facing feature | `Core/Models/TokenLogProbabilities.cs`, `Core/Commands/LogProbsCommand.cs`, `Core/Commands/DemoLogProbsCommand.cs`, `Core/Services/TokenFormatters.cs` | 5, 14 | M |
| 12 | Token Inspection, Tokenization & Attribution | user-facing feature | `Core/Services/TokenInspectionService.cs`, `Core/Services/TokenInspection/{TokenAnalysis,TokenAttribution,TokenInfo,TokenInspectionResult,TokenProbabilityAlternative,TokenProbabilityMap,TokenProbabilityMapResult}.cs`, `Core/Commands/{Tokenize,Inspect,ShowTokenAnalysis,ExportTokenAnalysis}Command.cs` | 10, 11 | L |
| 13 | Diagnostic Logging & Log Export | platform capability | `Core/Services/TokenInspection/LLamaSharpLogConfig.cs`, `Core/Commands/ExportLogsCommand.cs` | 10 | M |
| 14 | Output Rendering & Token Visualization | platform capability | `Core/Services/IConsoleFormatter.cs`, `Core/Services/BasicConsoleFormatter.cs`, `Gui/Services/SpectreConsoleFormatter.cs`, `Gui/Services/TokenProbabilityVisualizer.cs`, `Gui/UI/LogProbHeatmapView.cs`, `Gui/UI/ThemeManager.cs`, `Core/Services/TokenFormatters.cs` | 11 | M |
| 15 | Packaging, Build & Release Distribution | platform capability | `src/*/*.csproj` (`Compact`/`SingleFile` configurations), `build-compact.bat`, `build-compact.ps1`, `build-compact-robust.bat`, `build-singlefile.bat`, `.github/workflows/build-release.yml`, `docs/compact-build.md`, `docs/github-actions-release.md`, `global.json`, `Directory.Build.props` | — | M |

All 15 entries are Tier 1 (no dossier requires another dossier's text; cross-feature relationships are captured as interface references only), so all 15 fan out in parallel.

## Deliberate Exclusions (recorded, not silently dropped)

- `.vs/`, `bin/`, `obj/`, `TestResults/`, `test-publish/` — build and IDE artifacts.
- `tmp/LLamaSharpInvestigation.cs` — a scratch spike file, not compiled into any project. Read for intent by feature 10, but recorded as **vestigial**, not a requirement.
- `.github/*.prompt.md` — AI coding-assistant prompts used to build the product, not product behavior. Useful as evidence of *intent* only.
- `docs/PHASE1-SUMMARY.md`, `docs/release-setup-complete.md`, `docs/llamasharp-*-checklist.md`, `docs/llamasharp-*-plan.md`, `IMPLEMENTATION_SUMMARY.md` — status/aspirational documents. Any behavior they claim must be confirmed in code before it becomes a requirement.

## Known Boundary Notes for Sub-Agents

- The two shells (features 1 and 2) implement **overlapping but not identical** command surfaces. Feature 1 documents the console REPL's behavior; feature 2 documents the GUI's; feature 3 owns the shared command contract. Where they differ, both must record the divergence rather than assume parity.
- `SetCommand` (feature 5) is the single largest command (464 lines) and reaches into credentials (6) and system prompts (7). Feature 5 owns the setting keys, validation ranges, and persistence; features 6 and 7 own the semantics of what those settings control.
- Log probabilities (11) are *produced* by features 8, 9 and 10 and *rendered* by feature 14. Feature 11 owns the data model, the probability math, and the user-facing enable/disable/top-K surface.
