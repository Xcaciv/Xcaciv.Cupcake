## 12. Appendix: Traceability

This appendix exists so that any statement in this document can be walked back to the
source text it was read from, and so that a reviewer can confirm that nothing in the
source was silently left out. Section 12.2 maps requirements to evidence. Section 12.3
maps evidence back to requirements — every file in the repository's source tree,
accounted for or explicitly excused. Sections 12.4 and 12.5 do the same for the
repository's documentation, build scripts and tests. Sections 12.6 and 12.7 describe how
the document was produced and how to check any single line of it.

Every count in this appendix was measured against the pinned commit at the time of
writing, not estimated. Two counting conventions are stated once here and applied
throughout. **Lines** are physical lines — the final line is counted whether or not it
ends in a newline. Forty-one tracked text files in this repository have no trailing newline,
so a count here is sometimes one greater than `wc -l` reports for the same file; the
convention was chosen to match the `path:line` citations in the dossiers, which are what
a reviewer will actually open. **Line totals exclude `test-publish/Xcaciv.ChatDbg.Shell`**,
a committed 15.7 MB stripped ELF binary whose byte stream contains 48,213 newline
characters; counting it as text would inflate every repository-wide total by a factor of
three and mean nothing. It is accounted for in §12.4.3.

---

### 12.1 Provenance

| Item | Value |
|---|---|
| Repository | `https://github.com/Xcaciv/chatdbg` |
| Pinned commit | `d8c18f61d6bb73666ed97cd4885e877e35558485` |
| Branch | `LLamaSharp_support` |
| Commit date | 2025-10-14 10:14:10 −0600 |
| Surveyed | 2026-08-28 |
| Licence | GPL-3.0 (`LICENSE`, 674 lines, verbatim GNU GPL v3 text) |
| Local working copy (read-only) | `/mnt/g/3RD-Party/reversing/subject/chatdbg` |

**Repository size, measured (`git ls-files`, the authoritative file list):**

| Measure | Count |
|---|---|
| Tracked files, total | 145 |
| Tracked files under `src/` | 107 |
| Tracked files outside `src/` | 38 |
| Commits reachable from the pinned commit | 47 |
| Lines, all tracked text files (excluding the one committed binary) | 20,976 |
| Lines, all tracked `.cs` files | 13,083 |
| Lines, `.cs` files under `src/` | 12,868 |
| — of which production C# (three shipped projects) | 10,810 |
| — of which test C# (one test project) | 2,058 |
| Lines, all tracked files under `src/` (incl. project files and two in-tree docs) | 13,580 |
| Lines, tracked text files outside `src/` (docs, scripts, CI, licence) | 7,396 |
| Projects in `Xcaciv.ChatDbg.sln` | 4 (`Xcaciv.ChatDbg.Core`, `Xcaciv.ChatDbg.Shell`, `Xcaciv.ChatDbg.Shell.Gui`, `Xcaciv.ChatDbg.Core.Tests`) plus one solution folder, `Docs` |
| Automated test methods (`[Fact]`) | 100 (there are no `[Theory]` methods) |

**Lines of production C# by project**

| Project | Files (all tracked) | `.cs` lines |
|---|---|---|
| `src/Xcaciv.ChatDbg.Core` | 53 | 6,165 |
| `src/ChatDbg.Shell.Gui` | 11 | 3,912 |
| `src/ChatDbg` | 4 | 733 |
| `src/Xcaciv.ChatDbg.Core.Tests` | 39 | 2,058 |

**Statement of scope.** This PRD documents exactly one commit of one branch. It is a
description of what the source contained on 2026-08-28 at
`d8c18f61d6bb73666ed97cd4885e877e35558485`, not of the product's intent, its roadmap, or
its current state upstream. The pinned branch, `LLamaSharp_support`, was not the
repository's default branch at survey time (`origin/HEAD` pointed at `main`), and the
repository carried 14 other local branches and 13 other remote branches, several of which
contain work not represented here. The upstream repository may have moved since; nothing
in this document should be assumed to describe any other commit. Where a requirement in
this PRD contradicts the upstream project's own documentation, the contradiction is
deliberate and is recorded as a quirk — the source code at the pinned commit is the
specification throughout, and documentation was treated as a hint requiring code
confirmation.

---

### 12.2 Feature → evidence → dossier map

One row per written feature subsection of Section 7. The **Functional requirements**,
**Acceptance criteria** and **Quirks recorded** columns are counts of distinct
`FR-n.m`, `AC-n.m` and `QUIRK-n.m` identifiers in each subsection file, measured
by de-duplicated pattern extraction over the written text. All subsection files live in
`/mnt/g/3RD-Party/reversing/output/chatdbg/prd_sections/`; all dossiers live in
`/mnt/g/3RD-Party/reversing/output/chatdbg/dossiers/`.

| PRD § | Feature | Source evidence paths (primary; each subsection's own **Source notes** block is exhaustive) | Dossier | Functional requirements | Acceptance criteria | Quirks recorded |
|---|---|---|---|---|---|---|
| 7.1 | Command System & Dispatch<br>*(file `07-01-command-system.md`)* | `src/Xcaciv.ChatDbg.Core/Models/ICommand.cs`, `Models/CommandResult.cs`, `Commands/HelpCommand.cs`, `Commands/ExitCommand.cs`, `Commands/QuitCommand.cs`; `src/ChatDbg/ChatShell.cs`, `src/ChatDbg/Program.cs`; `src/ChatDbg.Shell.Gui/Program.cs`, `UI/ChatWindow.cs`, `ChatShell.cs` (dead); `src/Xcaciv.ChatDbg.Core/Services/IConsoleFormatter.cs` | `command-system.md` | 86 | 33 | 26 |
| 7.2 | Settings & Configuration<br>*(`07-02-settings-configuration.md`)* | `Core/Models/ChatSettings.cs`, `Core/Services/SettingsService.cs`, `Core/Services/ISettingsService.cs`, `Core/Commands/SetCommand.cs`, `Core/Commands/ModelCommand.cs`, `Core/Commands/LogProbsCommand.cs`; `src/ChatDbg/ChatShell.cs`; `src/ChatDbg.Shell.Gui/Program.cs`, `UI/SettingsDialog.cs`, `UI/ChatWindow.cs`; both shell `.csproj` files | `settings-configuration.md` | 86 | 43 | 34 |
| 7.3 | Credential Management & Secret Storage<br>*(`07-03-credential-management.md`)* | `Core/Models/WindowsCredentialManager.cs`, `Core/Models/ChatSettings.cs`, `Core/Services/SettingsService.cs`, `Core/Commands/SetCommand.cs`; `src/ChatDbg/ChatShell.cs`; `src/ChatDbg.Shell.Gui/{Program.cs,UI/SettingsDialog.cs,UI/ChatWindow.cs,ChatShell.cs}`; `Core/Services/{BedrockService,DefaultBedrockRuntimeClientFactory,AzureOpenAIService}.cs`; `docs/SECURITY-IMPLEMENTATION.md`, `docs/WINCRED-IMPLEMENTATION.md`, `tmp/test-wincred.cmd`, `tmp/test-env-vars.cmd`, `.gitignore` | `credential-management.md` | 110 | 43 | 31 |
| 7.4 | Chat History Management<br>*(`07-04-chat-history.md`)* | `Core/Models/ChatHistory.cs`, `Models/ChatMessage.cs`, `Models/TokenLogProbabilities.cs`, `Core/Services/ChatHistoryService.cs`, `Core/Commands/{Inject,Pop,Clear,Import,Export,Help}Command.cs`; `src/ChatDbg/{ChatShell,Program}.cs`; `src/ChatDbg.Shell.Gui/{Program.cs,UI/ChatWindow.cs}`; `Core/Services/{AzureOpenAI,Bedrock,LLamaSharp}Service.cs` | `chat-history.md` | 67 | 40 | 30 |
| 7.5 | System Prompt Management<br>*(`07-05-system-prompts.md`)* | `Core/Models/SystemPrompt.cs`, `Core/Services/SystemPromptService.cs`, `Core/Services/ISystemPromptService.cs`, `Core/Commands/PromptCommand.cs`, `Core/Commands/SetCommand.cs`, `Core/Models/ChatSettings.cs`; `src/ChatDbg.Shell.Gui/UI/SystemPromptsDialog.cs`, `UI/ChatWindow.cs`, `Program.cs`, `ChatShell.cs` (dead); `src/ChatDbg/ChatShell.cs` | `system-prompts.md` | 98 | 35 | 27 |
| 7.6 | AI Provider Abstraction & Hosted OpenAI Integration<br>*(`07-06-ai-provider-azure.md`)* | `Core/Services/IAIService.cs`, `Services/AzureOpenAIService.cs`, `Services/IAzureOpenAIClientFactory.cs`, `Services/DefaultAzureOpenAIClientFactory.cs`; `Core/Models/{AIResponse,TokenLogProbabilities,ChatMessage,ChatHistory}.cs`; `Core/Services/{Bedrock,LLamaSharp}Service.cs` (contract obligations only); both shells | `ai-provider-azure.md` | 70 | 30 | 24 |
| 7.7 | Managed Cloud Model Marketplace Integration<br>*(`07-07-ai-provider-bedrock.md`)* | `Core/Services/BedrockService.cs`, `Services/IBedrockRuntimeClientFactory.cs`, `Services/DefaultBedrockRuntimeClientFactory.cs`; `Core/Models/{ChatSettings,TokenLogProbabilities,AIResponse,ChatMessage,WindowsCredentialManager}.cs`; `Core/Commands/{Set,Model}Command.cs`; both shells; `Core/Xcaciv.ChatDbg.Core.csproj` | `ai-provider-bedrock.md` | 69 | 38 | 42 |
| 7.8 | Local Model Inference<br>*(`07-08-local-llm-inference.md`)* | `Core/Services/LLamaSharpService.cs`, `Services/TokenInspection/LLamaSharpLogConfig.cs`, `Services/TokenInspection/TokenAnalysis.cs`, `Core/Models/{TokenLogProbabilities,ChatSettings}.cs`, `Core/Commands/{Set,LogProbs,ShowTokenAnalysis,ExportTokenAnalysis,ExportLogs}Command.cs`; both shells; `Core/Xcaciv.ChatDbg.Core.csproj`, `global.json`; `docs/LLamaSharp-*.md`, `docs/compact-build.md`; `tmp/LLamaSharpInvestigation.cs` (vestigial) | `local-llm-inference.md` | 81 | 48 | 42 |
| 7.9 | Token Probability Analysis<br>*(`07-09-token-probability-analysis.md`)* | `Core/Models/TokenLogProbabilities.cs`, `Models/{AIResponse,ChatMessage,ChatHistory,ChatSettings}.cs`; `Core/Commands/{LogProbs,DemoLogProbs,Set,Help}Command.cs`; `Core/Services/{TokenFormatters,BasicConsoleFormatter,IConsoleFormatter}.cs`; `Gui/Services/{SpectreConsoleFormatter,TokenProbabilityVisualizer}.cs`, `Gui/UI/LogProbHeatmapView.cs`; `Core/Services/{AzureOpenAI,Bedrock,LLamaSharp}Service.cs`; both shells; `docs/Token Probability Testing.prompt.md` | `token-probability-analysis.md` | 85 | 39 | 26 |
| 7.10 | Token Inspection, Tokenization & Attribution<br>*(`07-10-token-inspection.md`)* | `Core/Services/TokenInspectionService.cs`; `Core/Services/TokenInspection/{TokenAnalysis,TokenInfo,TokenInspectionResult,TokenProbabilityAlternative,TokenProbabilityMap,TokenAttribution,TokenProbabilityMapResult}.cs`; `Core/Commands/{Tokenize,Inspect,ShowTokenAnalysis,ExportTokenAnalysis,ExportLogs,Help,LogProbs,Set}Command.cs`; `Core/Services/LLamaSharpService.cs`; both shells; `README.md`, `docs/LLamaSharp-Token-Introspection.md`, `IMPLEMENTATION_SUMMARY.md` | `token-inspection.md` | 74 | 44 | 32 |
| 7.11 | Diagnostic Logging & Log Export<br>*(`07-11-diagnostic-logging.md`)* | `Core/Services/TokenInspection/LLamaSharpLogConfig.cs`, `Core/Commands/ExportLogsCommand.cs`, `Core/Services/LLamaSharpService.cs` (sole production consumer), `Core/Commands/HelpCommand.cs`; both shells; `Directory.Build.props`, all three shipped `.csproj` files; `docs/LLamaSharp-*.md`, `IMPLEMENTATION_SUMMARY.md` | `diagnostic-logging.md` | 62 | 26 | 24 |
| 7.12 | Output Rendering & Token Visualization<br>*(`07-12-output-rendering.md`)* | `Core/Services/IConsoleFormatter.cs`, `Services/BasicConsoleFormatter.cs`, `Services/TokenFormatters.cs`; `Gui/Services/SpectreConsoleFormatter.cs`, `Gui/Services/TokenProbabilityVisualizer.cs` (dead), `Gui/UI/ChatWindow.cs`, `Gui/UI/LogProbHeatmapView.cs` (dead), `Gui/UI/ThemeManager.cs`, `Gui/UI/SettingsDialog.cs`, `Gui/Program.cs`, `Gui/ChatShell.cs` (dead); `src/ChatDbg/ChatShell.cs`; `Core/Commands/{LogProbs,Set,DemoLogProbs}Command.cs` | `output-rendering.md` | 131 | 35 | 29 |
| 7.13 | Interactive Chat Session (Line-Oriented Shell)<br>*(`07-13-chat-session-console.md`)* | `src/ChatDbg/Program.cs`, `src/ChatDbg/ChatShell.cs`, `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj`; supporting: `Core/Models/*.cs`, `Core/Services/*.cs`, `Core/Commands/*.cs`; `README.md`, `src/ChatDbg/prd.md` (both overruled where they disagree) | `chat-session-console.md` | 83 | 32 | 24 |
| 7.14 | Full-Screen Terminal Shell<br>*(`07-14-terminal-gui-shell.md`)* | `src/ChatDbg.Shell.Gui/Program.cs`, `UI/ChatWindow.cs`, `UI/SettingsDialog.cs`, `UI/SystemPromptsDialog.cs`, `UI/ThemeManager.cs`, `UI/LogProbHeatmapView.cs` (dead), `Services/SpectreConsoleFormatter.cs`, `Services/TokenProbabilityVisualizer.cs` (dead), `ChatShell.cs` (dead), `Xcaciv.ChatDbg.Shell.Gui.csproj`; contracts from `Core/Models/*` and `Core/Services/*`; `.github/workflows/build-release.yml` (proves this shell has no released binary) | `terminal-gui-shell.md` | 124 | 47 | 27 |
| 7.15 | Packaging, Build & Release Distribution<br>*(`07-15-packaging-build-release.md`)* | All four `.csproj` files; `Directory.Build.props`, `global.json`, `Xcaciv.ChatDbg.sln`, `.gitignore`, `LICENSE`; `build-compact.bat`, `build-compact.ps1`, `build-compact-robust.bat`, `build-singlefile.bat`; `.github/workflows/build-release.yml`; runtime path/filesystem contracts across `Core/Services/*`; `docs/compact-build.md`, `docs/github-actions-release.md`, `docs/release-setup-complete.md`; measured artefact `test-publish/Xcaciv.ChatDbg.Shell` | `packaging-build-release.md` | 128 | 40 | 37 |
| **Totals** | **15 subsections** | — | **15 dossiers, 11,621 lines** | **1,354** | **573** | **455** |

Supporting counts for the same 15 subsections, measured the same way: **169** distinct
user stories (`US-n.m`), **53** distinct numbered use cases (`UC-n.m`), and **109**
occurrences of the explicit `INFERRED` marker distinguishing deduction from observation.
Subsection files total 7,804 lines.

**Reading the counts honestly.** The three count columns measure *identifiers written*,
not *behaviour covered*. Two consequences a reviewer should hold in mind:

- **Requirement density does not track feature size.** Output Rendering (7.12) carries the
  most functional requirements (131) from 566 lines of dedicated rendering code, because
  colour bands, escaping rules and layout modes each pin an exact observable string. The
  Local Model Inference back end (7.8) is a single 697-line file but yields 81, because
  much of it is one long generation loop.
- **Use-case numbering is not uniform.** Six subsections (7.5, 7.6, 7.7, 7.8, 7.11, 7.12,
  7.14) present their scenarios without `UC-n.m` identifiers, so the 53 total understates
  scenario coverage. This is an inconsistency between independently-written subsections
  that synthesis did not normalise; it is recorded here rather than hidden. It does not
  affect `FR`/`AC` numbering, which every subsection applies consistently.

---

### 12.3 Source file coverage

Every one of the 107 tracked files under `src/` is listed. **Documented in** names the
subsection that owns the file — that is, the one whose requirements were derived from it —
followed in parentheses by other subsections that cite it as supporting evidence. Files
with no owning subsection appear in the block at the end of this section with a reason.

#### 12.3.1 `src/Xcaciv.ChatDbg.Core` — the shared library (53 tracked files, 6,165 lines of C#)

| Source path | Lines | Documented in | Notes |
|---|---|---|---|
| `Models/ICommand.cs` | 9 | 7.1 | The four-member command contract; the whole product's extension seam. |
| `Models/CommandResult.cs` | 17 | 7.1 (7.13) | Three fields, three factories. |
| `Models/AIResponse.cs` | 53 | 7.6 (7.7, 7.9, 7.13) | Provider response envelope. |
| `Models/ChatHistory.cs` | 57 | 7.4 (7.6, 7.9, 7.13) | Conversation aggregate. |
| `Models/ChatMessage.cs` | 19 | 7.4 (7.6, 7.7, 7.9, 7.12, 7.13) | Conversation entry. |
| `Models/ChatSettings.cs` | 202 | 7.2 (7.3, 7.5–7.13, 7.15) | The most widely-cited file in the repository: defaults, wire names, credential resolution. |
| `Models/SystemPrompt.cs` | 17 | 7.5 | Named-prompt record. |
| `Models/TokenLogProbabilities.cs` | 33 | 7.9 (7.4, 7.6–7.8, 7.12–7.14) | Probability data model; source of the double-exponentiation quirk. |
| `Models/WindowsCredentialManager.cs` | 173 | 7.3 (7.6, 7.7, 7.13, 7.15) | The only OS-specific dependency in the library. |
| `Commands/ClearCommand.cs` | 24 | 7.4 (7.1) | |
| `Commands/DemoLogProbsCommand.cs` | 251 | 7.9 (7.1, 7.12, 7.13, 7.14) | Fixture-driven demonstration. |
| `Commands/ExitCommand.cs` | 15 | 7.1 | |
| `Commands/QuitCommand.cs` | 15 | 7.1 | Byte-equivalent sibling of `ExitCommand`. |
| `Commands/ExportCommand.cs` | 50 | 7.4 (7.1, 7.14) | |
| `Commands/ImportCommand.cs` | 49 | 7.4 (7.1, 7.14) | |
| `Commands/InjectCommand.cs` | 47 | 7.4 (7.1) | |
| `Commands/PopCommand.cs` | 31 | 7.4 (7.1) | |
| `Commands/HelpCommand.cs` | 108 | 7.1 (7.4, 7.5, 7.9–7.11) | One of two competing help renderers. |
| `Commands/SetCommand.cs` | 464 | 7.2 (7.1, 7.3, 7.5–7.10, 7.12, 7.13) | Largest command; the settings key table lives here. |
| `Commands/ModelCommand.cs` | 36 | 7.2 (7.1, 7.7) | |
| `Commands/PromptCommand.cs` | 379 | 7.5 (7.1, 7.2, 7.13) | |
| `Commands/LogProbsCommand.cs` | 209 | 7.9 (7.1, 7.2, 7.6, 7.8, 7.10, 7.12–7.14) | |
| `Commands/TokenizeCommand.cs` | 98 | 7.10 (7.1) | |
| `Commands/InspectCommand.cs` | 212 | 7.10 (7.1, 7.13) | |
| `Commands/ShowTokenAnalysisCommand.cs` | 50 | 7.10 (7.1, 7.8, 7.14) | **Unregistered stub** — compiled, never reachable from any shell (QUIRK-1.1). |
| `Commands/ExportTokenAnalysisCommand.cs` | 25 | 7.10 (7.1, 7.8, 7.14) | **Unregistered stub** (QUIRK-1.1). |
| `Commands/ExportLogsCommand.cs` | 25 | 7.11 (7.1, 7.8, 7.10, 7.14) | **Unregistered stub** (QUIRK-1.1). |
| `Services/IAIService.cs` | 11 | 7.6 (7.9) | The provider port. |
| `Services/AzureOpenAIService.cs` | 497 | 7.6 (7.2–7.5, 7.7, 7.9, 7.13, 7.15) | |
| `Services/IAzureOpenAIClientFactory.cs` | 9 | 7.6 | Test seam only; no production alternative implementation. |
| `Services/DefaultAzureOpenAIClientFactory.cs` | 15 | 7.6 | |
| `Services/BedrockService.cs` | 345 | 7.7 (7.2–7.6, 7.9, 7.13, 7.15) | |
| `Services/IBedrockRuntimeClientFactory.cs` | 9 | 7.7 | Test seam only. |
| `Services/DefaultBedrockRuntimeClientFactory.cs` | 22 | 7.7 (7.3) | |
| `Services/LLamaSharpService.cs` | 697 | 7.8 (7.2, 7.4–7.7, 7.9–7.11, 7.13, 7.15) | Largest single file in the library; 9 commits touch it, the product's active frontier. |
| `Services/IConsoleFormatter.cs` | 52 | 7.12 (7.1, 7.9) | Output port. |
| `Services/BasicConsoleFormatter.cs` | 206 | 7.12 (7.1, 7.9) | |
| `Services/TokenFormatters.cs` | 123 | 7.12 (7.9, 7.14) | Shared escaping, colour bands, value formatting. |
| `Services/ISettingsService.cs` | 13 | 7.2 | Six-operation persistence port. |
| `Services/SettingsService.cs` | 361 | 7.2 (7.3, 7.5–7.7, 7.9–7.15) | |
| `Services/ISystemPromptService.cs` | 12 | 7.5 | |
| `Services/SystemPromptService.cs` | 215 | 7.5 (7.11, 7.13–7.15) | |
| `Services/ChatHistoryService.cs` | 66 | 7.4 (7.9, 7.15) | |
| `Services/TokenInspectionService.cs` | 325 | 7.10 (7.8, 7.11) | |
| `Services/TokenInspection/LLamaSharpLogConfig.cs` | 201 | 7.11 (7.8, 7.10, 7.15) | Folder placement is misleading — it is a log sink, not a token model (QUIRK-11.7). |
| `Services/TokenInspection/TokenAnalysis.cs` | 141 | 7.10 (7.8, 7.11) | Per-token analysis record + export key names. |
| `Services/TokenInspection/TokenAttribution.cs` | 32 | 7.10 | |
| `Services/TokenInspection/TokenInfo.cs` | 37 | 7.10 | |
| `Services/TokenInspection/TokenInspectionResult.cs` | 27 | 7.10 | |
| `Services/TokenInspection/TokenProbabilityAlternative.cs` | 22 | 7.10 | |
| `Services/TokenInspection/TokenProbabilityMap.cs` | 32 | 7.10 | |
| `Services/TokenInspection/TokenProbabilityMapResult.cs` | 27 | 7.10 | |
| `Xcaciv.ChatDbg.Core.csproj` | 19 | 7.15 (7.7–7.11) | Pins the inference engine, backends and cloud client versions. |

#### 12.3.2 `src/ChatDbg` — the line-oriented shell (4 tracked files, 733 lines of C#)

| Source path | Lines | Documented in | Notes |
|---|---|---|---|
| `Program.cs` | 14 | 7.13 (7.1–7.7, 7.9–7.12, 7.14, 7.15) | Entry point, fatal-error handling, exit codes. |
| `ChatShell.cs` | 719 | 7.13 (7.1–7.12, 7.14) | Highest-churn file in the repository (10 commits). Owns the REPL; other subsections cite it for their command wiring. |
| `Xcaciv.ChatDbg.Shell.csproj` | 117 | 7.15 (7.1, 7.2, 7.4, 7.8, 7.11, 7.13) | Assembly identity plus the `Compact` and `SingleFile` publish profiles. |
| `prd.md` | 211 | *(see 12.3.5)* | Stale in-tree design document; no requirement derived from it. |

#### 12.3.3 `src/ChatDbg.Shell.Gui` — the full-screen shell (11 tracked files, 3,912 lines of C#)

| Source path | Lines | Documented in | Notes |
|---|---|---|---|
| `Program.cs` | 103 | 7.14 (7.1–7.7, 7.9–7.13, 7.15) | Startup and registry construction; source of the split-settings defect (QUIRK-2.4). |
| `ChatShell.cs` | 672 | 7.14 (7.1–7.13) | **Dead code** — a complete second shell class that is never instantiated (QUIRK-14.5, QUIRK-1.4). Documented as a defect, not as behaviour. |
| `UI/ChatWindow.cs` | 1,143 | 7.14 (7.1–7.12, 7.15) | Largest file in the repository; joint highest-churn (10 commits). |
| `UI/SettingsDialog.cs` | 608 | 7.14 (7.2, 7.3, 7.5–7.9, 7.11, 7.12) | Four-tab settings screen and its clamp ranges. |
| `UI/SystemPromptsDialog.cs` | 697 | 7.5 (7.14) | Content owned by 7.5; window chrome and dialog behaviour by 7.14. |
| `UI/ThemeManager.cs` | 46 | 7.12 (7.14) | Four global palettes. |
| `UI/LogProbHeatmapView.cs` | 89 | 7.12 (7.9, 7.14) | **Dead code** — never constructed (QUIRK-14.5). Documented as a defect. |
| `Services/SpectreConsoleFormatter.cs` | 236 | 7.12 (7.9, 7.14) | Reachable, but only through the demonstration command, where it writes to a console the full-screen toolkit owns (QUIRK-14.3, QUIRK-14.4). |
| `Services/TokenProbabilityVisualizer.cs` | 318 | 7.12 (7.9, 7.14) | **Dead code** — compiled, never called (QUIRK-14.5). Documented as a defect. |
| `Xcaciv.ChatDbg.Shell.Gui.csproj` | 128 | 7.15 (7.1, 7.2, 7.5, 7.8–7.11, 7.14) | Publish-profile blocks are byte-identical to the console shell's. |
| `prd.md` | 211 | *(see 12.3.5)* | Byte-identical duplicate of `src/ChatDbg/prd.md` (both MD5 `1787915e0aced7bc00018b6a64ba44e7`). |

#### 12.3.4 `src/Xcaciv.ChatDbg.Core.Tests` — the test project (39 tracked files, 2,058 lines of C#)

All 37 test classes and the one test double are enumerated with their assertion counts and
subject matter in **§12.5**, which is where test evidence is accounted for. The project
file is listed here for completeness.

| Source path | Lines | Documented in | Notes |
|---|---|---|---|
| 37 `*Tests.cs` files | 2,031 total | See §12.5 | 100 `[Fact]` methods; every file mapped to at least one subsection. |
| `TestDoubles/StubHttpMessageHandler.cs` | 27 | 7.6 | Test-only helper; documented as the reason the hosted-provider request shape is unobservable to the suite. |
| `Xcaciv.ChatDbg.Core.Tests.csproj` | 26 | 7.15 (7.1, 7.11) | Pins the test framework, mocking library and coverage collector; declares the project non-packable. |

#### 12.3.5 Not documented, and why

Two files under `src/` contributed **no requirement** to this PRD. Both are accounted for,
and both were read. Nothing else under `src/` is unmapped.

| Source path | Lines | Why no requirement was derived |
|---|---|---|
| `src/ChatDbg/prd.md` | 211 | An in-tree design document, not code and not a build input. It is stale: it names a different runtime version, a different provider count, a different project layout and a different dependency list from what the code at this commit contains. Subsections 7.1, 7.4, 7.13 and 7.14 cite it only to record where it contradicts the source. Treated as evidence of past intent. |
| `src/ChatDbg.Shell.Gui/prd.md` | 211 | Byte-identical duplicate of the file above (verified: same MD5). It describes the line-oriented shell, not the full-screen shell it sits beside — so it is not merely stale but misfiled. Same treatment. |

**Explicitly not present in the file list, and therefore not in this table.** `bin/`,
`obj/`, `.vs/` and `TestResults/` are excluded by `.gitignore` and are not tracked, so they
are outside the pinned commit. One PRD subsection (7.4) does cite an *untracked* build
output — `src/ChatDbg.Shell.Gui/bin/Debug/net10.0/chat.json`, 193,375 bytes — as the only
available specimen of the product's real export wire format. That evidence is honest but
not reproducible from the commit alone: a reviewer checking out
`d8c18f61d6bb73666ed97cd4885e877e35558485` will not find that file, and must instead read
the serialization attributes on `Models/ChatHistory.cs` and `Models/ChatMessage.cs`.

**One known citation defect.** Subsection 7.10's Source notes list
`src/Xcaciv.ChatDbg.Core/Services/LLamaSharpLogConfig.cs`. No such path exists; the correct
path is `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs`, which is
what 7.8, 7.11 and 7.15 cite. The file is documented; only the one citation is wrong.
Recorded here rather than quietly corrected, because an appendix that silently repairs its
own inputs cannot be audited.

---

### 12.4 Non-source artifacts

All 38 tracked files outside `src/`, and where each is accounted for. The three tables
below hold 11 + 16 + 9 = 36 of them; the remaining two, `README.md` and
`IMPLEMENTATION_SUMMARY.md`, are described in the prose of §12.4.2 because they sit at the
repository root rather than in `docs/`. Nothing outside `src/` is unaccounted for.

One correction to an upstream input: `inventory.md` states that `docs/` holds 14 files.
It holds **16** (`git ls-files docs/`). The inventory does not enumerate them, so which two
it omitted cannot be recovered; all 16 are listed and classified in §12.4.2, so the
miscount cost no requirement. It is recorded rather than silently fixed.

#### 12.4.1 Build, packaging and CI

| Path | Lines | Accounted for in | Notes |
|---|---|---|---|
| `Xcaciv.ChatDbg.sln` | 52 | 7.15 (7.1) | Four projects plus a `Docs` solution folder. |
| `Directory.Build.props` | 5 | 7.15 (7.11) | Repository-wide MSBuild defaults. |
| `global.json` | 6 | 7.15 (7.1, 7.5, 7.8–7.10, 7.13, 7.14) | Pins the SDK to `10.0.100-rc.1.25451.107`, `rollForward: latestFeature`. |
| `.gitignore` | 58 | 7.15 (7.3, 7.4) | Also load-bearing evidence: it is why the settings and history documents are not in the repository, and why §12.3.5's `chat.json` is not reproducible. |
| `build-compact.bat` | 29 | 7.15 | Local size-reduced publish. |
| `build-compact.ps1` | 42 | 7.15 | |
| `build-compact-robust.bat` | 113 | 7.15 | Interactive menu; native and bundled branches. |
| `build-singlefile.bat` | 43 | 7.15 | |
| `.github/workflows/build-release.yml` | 196 | 7.15 (7.14) | The only CI pipeline. Publishes **the line-oriented shell only** — which is how 7.14 establishes that the full-screen shell has no released binary. |
| `LICENSE` | 674 | 7.15 | Verbatim GPL-3.0. |
| `.mcp.json` | 14 | **Nowhere — see below** | Declares one stdio MCP server (`@upstash/context7-mcp`) for the maintainers' own AI coding assistant. It is developer tooling configuration, has no effect on any build, test or runtime path, and no requirement derives from it. Recorded as deliberately excluded. |

#### 12.4.2 Repository documentation

`README.md` (344 lines) is the most authoritative behavioural document in the repository
and is cited by **all 15 dossiers and 14 of the 15 subsections** (7.8 reaches the same
material through the `docs/LLamaSharp-*` set). It was still treated as a hint, not a
specification: several of the recorded quirks exist precisely because the README describes
behaviour the code does not implement.

`IMPLEMENTATION_SUMMARY.md` (272 lines) is a status document. It is cited by 8 dossiers and
4 subsections **as a claim to be checked**, never as a requirement.

| `docs/` file | Lines | Cited by PRD § | Character |
|---|---|---|---|
| `LLamaSharp-Implementation-Notes.md` | 320 | 7.8, 7.11 | Mixed. Descriptive in parts, aspirational in others; individual claims were checked against code. |
| `LLamaSharp-Quick-Start.md` | 239 | 7.8, 7.11 | Descriptive user guidance; the documented success-log signature was used as evidence. |
| `LLamaSharp-Token-Introspection.md` | 343 | 7.7, 7.8, 7.10, 7.11 | Mixed; several capability claims verified as unimplemented. |
| `LLamaSharp-Troubleshooting-0xC0000005.md` | 319 | 7.8, 7.11, 7.15 | Descriptive; the native-crash mitigations it documents are visible in code. |
| `SECURITY-IMPLEMENTATION.md` | 253 | 7.3, 7.6, 7.7 | Largely descriptive and largely accurate; still checked line by line. |
| `WINCRED-IMPLEMENTATION.md` | 195 | 7.3 | Descriptive. |
| `TERMINAL-GUI-IMPLEMENTATION.md` | 152 | 7.5, 7.6, 7.14 | **Found stale** by 7.14. |
| `compact-build.md` | 173 | 7.8, 7.15 | Descriptive; size budgets and prerequisites used as claims requiring confirmation. |
| `github-actions-release.md` | 181 | 7.15 | Descriptive. |
| `Token Probability Testing.prompt.md` | 126 | 7.9 | An AI-assistant prompt, not documentation. Evidence of intent only. |
| **`PHASE1-SUMMARY.md`** | 210 | 7.5 | **Status document — aspirational. Claims not taken as requirements.** |
| **`release-setup-complete.md`** | 151 | 7.15 | **Status document — announces a setup as "complete". Claims not taken as requirements.** |
| **`llamasharp-lowlevel-api-checklist.md`** | 336 | *(none)* | **Forward-looking plan. Describes work that is not in the code at this commit. Cited by 2 dossiers as intent; contributed no requirement.** |
| **`llamasharp-lowlevel-api-implementation-plan.md`** | 464 | *(none)* | **Forward-looking plan. Same treatment; cited by 3 dossiers as intent only.** |
| **`llamasharp-sampling-pipeline-approach.md`** | 344 | *(none)* | **Forward-looking design note. Contributed no requirement.** |
| **`llamasharp-sampling-pipeline-implementation-summary.md`** | 708 | *(none)* | **The largest document in `docs/` and the most dangerous to a future reader: it reads as a completed-work summary for a sampling pipeline that the code at this commit does not contain. Contributed no requirement.** |

> **Warning for future readers.** The six rows in bold above total **2,213 lines** — more
> than a third of everything in `docs/` — and every one of them is written in the past or
> perfect tense about work that is partly or wholly absent from
> `d8c18f61d6bb73666ed97cd4885e877e35558485`. A reader who takes them at face value will
> believe the product has a low-level sampling pipeline, a completed release setup and a
> finished phase-1 scope. It does not. Requirements in this PRD were derived from code and
> from code alone; these documents were read for intent and then set aside.

#### 12.4.3 AI-assistant prompts and scratch files

| Path | Lines | Accounted for in | Notes |
|---|---|---|---|
| `.github/copilot-instructions.md` | 259 | 7.7, 7.15 (2 dossiers) | Coding-assistant instructions. Cited only where it disagrees with code. Not a requirement source. |
| `.github/chatdbg_create.prompt.md` | 152 | *(none)* | Prompt used to generate the product. Deliberately excluded — evidence of intent, not of behaviour. |
| `.github/gui_convert.prompt.md` | 91 | *(none)* | Same. |
| `.github/llamasharp_enhansement.prompt.md` | 180 | *(none, 2 dossiers)* | Same. Note the misspelling is in the source filename. |
| `.github/local_llm.prompt.md` | 72 | *(none)* | Same. |
| `tmp/LLamaSharpInvestigation.cs` | 215 | 7.8 | A feasibility spike. **Compiled into nothing** — no project includes it. Recorded as vestigial intent, explicitly not a requirement. |
| `tmp/test-wincred.cmd` | 43 | 7.3 | Manual verification script for the credential vault. Read as evidence of intended behaviour. |
| `tmp/test-env-vars.cmd` | 22 | 7.3 | Manual verification script for environment-variable credential resolution. |
| `test-publish/Xcaciv.ChatDbg.Shell` | binary | 7.15 | A committed pre-built ELF x86-64 binary, 15,677,171 bytes, stripped, dated 2025-10-01. **Inspected, deliberately not executed.** Used only to measure the real published artefact size against the size budget `docs/compact-build.md` claims. Its presence in version control is itself recorded as a packaging quirk. |

---

### 12.5 Test coverage as requirement evidence

The repository contains **one** test project, `src/Xcaciv.ChatDbg.Core.Tests`, holding
**100 `[Fact]` methods** across 37 test classes plus one test double (there are no
`[Theory]` methods, so the fact count is the test count). It references **only** the shared
library. Neither shell is under test.

| Test file (under `src/Xcaciv.ChatDbg.Core.Tests/`) | Facts | What it pins | PRD § drawing on it |
|---|---|---|---|
| `Commands/ClearCommandTests.cs` | 1 | Clearing empties the conversation | 7.4 |
| `Commands/DemoLogProbsCommandTests.cs` | 2 | Demonstration command succeeds and emits fixture output | 7.9 (7.1, 7.12) |
| `Commands/ExitAndQuitCommandTests.cs` | 2 | Both terminators set the exit flag | 7.1 (7.13) |
| `Commands/ExportCommandTests.cs` | 2 | Export arity guard and success path | 7.4 |
| `Commands/ExportLogsAndAnalysisCommandTests.cs` | 4 | The fixed refusal messages of two unregistered stubs | 7.11, 7.10 |
| `Commands/HelpCommandTests.cs` | 2 | Help lists registered commands; unknown name is refused | 7.1 (7.13) |
| `Commands/ImportCommandTests.cs` | 2 | Import arity guard and success path | 7.4 |
| `Commands/InjectCommandTests.cs` | 3 | Role validation and message append | 7.4 |
| `Commands/InspectCommandTests.cs` | 3 | Precondition guards only — never loads a model | 7.10 |
| `Commands/LogProbsCommandTests.cs` | 3 | Display-settings dump, enable path, unknown sub-command | 7.9 (7.2, 7.12) |
| `Commands/ModelCommandTests.cs` | 2 | No-arg reports current model and saves nothing; a change saves exactly once | 7.2 |
| `Commands/PopCommandTests.cs` | 2 | Removal from a non-empty and an empty conversation | 7.4 |
| `Commands/PromptCommandTests.cs` | 3 | No-arg names the active prompt; list mentions a name; `use` sets name+text with one save | 7.5 |
| `Commands/SetCommandTests.cs` | 5 | Dump; valid and invalid provider; keystore gate; migration delegate called once | 7.2 (7.3) |
| `Commands/ShowTokenAnalysisCommandTests.cs` | 1 | The fixed message of an unregistered stub | 7.10 (7.8) |
| `Commands/TokenizeCommandTests.cs` | 2 | Precondition guards only — never tokenizes anything | 7.10 |
| `Models/AIResponseTests.cs` | 2 | Response envelope shape | 7.6 (7.13) |
| `Models/ChatHistoryTests.cs` | 4 | Add, clear, ordering, last-message access | 7.4 (7.13) |
| `Models/ChatMessageTests.cs` | 2 | Entry shape and defaults | 7.4 (7.13) |
| `Models/ChatSettingsTests.cs` | 4 | Defaults and credential-resolution order | 7.2, 7.3 (7.6, 7.13, 7.15) |
| `Models/CommandResultTests.cs` | 3 | The three result factories | 7.1 (7.13) |
| `Models/SystemPromptTests.cs` | 1 | In-memory record round-trip | 7.5 |
| `Models/TokenLogProbabilityTests.cs` | 1 | **This test fails.** See the note below. | 7.9 (7.6, 7.8, 7.13, 7.14) |
| `Models/WindowsCredentialManagerTests.cs` | 2 | Platform probe and constant naming | 7.3 (7.13, 7.15) |
| `Services/AzureOpenAIServiceTests.cs` | 4 | The "is configured" predicate only — the stub transport ignores the request | 7.6 |
| `Services/BasicConsoleFormatterTests.cs` | 2 | Plain formatter output | 7.12 (7.1) |
| `Services/BedrockServiceTests.cs` | 2 | Readiness predicate over empty model id | 7.7 |
| `Services/ChatHistoryServiceTests.cs` | 2 | Save/load round-trip | 7.4 |
| `Services/DefaultFactoriesTests.cs` | 2 | The two client factories consume the expected settings fields | 7.6, 7.7 (7.15) |
| `Services/LLamaSharpServiceTests.cs` | 3 | Readiness over a missing and an existing file; unconfigured throws | 7.8 |
| `Services/SettingsServiceTests.cs` | 2 | Round-trip through an explicit base directory; absent-file default | 7.2 (7.13, 7.15) |
| `Services/SystemPromptServiceTests.cs` | 2 | Seeding is non-empty; save-delete-fetch returns nothing | 7.5 (7.13, 7.15) |
| `Services/TokenFormattersTests.cs` | 5 | Escaping, colour bands, value formatting | 7.12 (7.14) |
| `Services/TokenInspection/LLamaSharpLogConfigTests.cs` | 7 | Log sink defaults, buffering, clearing, explicit save, double dispose/configure | 7.11 (7.8, 7.10, 7.15) |
| `Services/TokenInspection/TokenAnalysisTests.cs` | 6 | Analysis record property access and round-trip | 7.10 (7.8, 7.15) |
| `Services/TokenInspectionModelsTests.cs` | 3 | The token-model family's shapes; one assertion duplicates the stub test above | 7.10 (7.11) |
| `Services/TokenInspectionServiceTests.cs` | 2 | Missing-file guards only | 7.10 |
| `TestDoubles/StubHttpMessageHandler.cs` | 0 | Test-only helper; returns a canned response and **ignores the request entirely** | 7.6 |
| **Total** | **100** | | |

#### What the suite does and does not establish

**The continuous-integration pipeline never runs these tests.** `.github/workflows/build-release.yml`
performs exactly four kinds of step: checkout, SDK install, `dotnet restore`, and
`dotnet publish` of `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj`, followed by artefact rename,
upload and release creation. There is no `dotnet test` invocation anywhere in the
repository's CI, and there is no second workflow. **Therefore every test in the table above
is evidence of what the author intended at the moment of writing, verified at authoring
time and never since. None of it is evidence of current behaviour.**

That distinction is not academic here. **The committed suite is red.**
`Models/TokenLogProbabilityTests.cs:9-18` constructs a token with
`LogProb = Math.Log(0.25)` and asserts `Assert.Equal(25, token.Probability, precision: 5)`,
while `Models/TokenLogProbabilities.cs:26` defines `Probability => Math.Exp(LogProb)`,
which yields `0.25`. Running that assertion produces
`Assert.Equal() Failure: Values are not within 5 decimal places / Expected: 25 / Actual: 0.25`.
The test and the code disagree about whether the value is a fraction or a percentage, the
disagreement has been committed, and no pipeline would have caught it. This PRD documents
the *code's* behaviour and records the disagreement as a quirk.

Three further limits on this evidence, all measured:

1. **Neither shell is tested at all.** The test project references only
   `Xcaciv.ChatDbg.Core`. Subsection 7.14 (Full-Screen Terminal Shell, 124 functional
   requirements) has **zero** covering tests; every statement in it is read from source.
   Subsection 7.13's shell behaviour is likewise uncovered — its cited tests all pin
   library contracts the shell consumes, not the shell itself.
2. **No test performs real inference, tokenization or network I/O.** The hosted-provider
   tests use a stub transport that ignores the request, so the request address, API
   version, header name, body shape, message ordering and role mapping are unobservable to
   the suite. No test loads a model file. The token-inspection tests reach only the
   missing-file guards.
3. **The commands with the most requirements have the fewest assertions.** `SetCommand.cs`
   is 464 lines with 5 covering facts and no test for any numeric range, alias, on-disk wire
   format, or migration flow. `PromptCommand.cs` is 379 lines with 3 facts covering none of
   create, delete, edit, export or import.

---

### 12.6 Document generation method

This PRD was produced by reverse-engineering the pinned commit. No maintainer was
consulted, no issue tracker was read, and the product was never executed. The procedure had
four stages.

**1. Reconnaissance.** The repository was enumerated with `git ls-files`, sized, and its
commit churn ranked per file to locate the code that was actually moving. That pass
produced `inventory.md`: 15 features, each with a kind, a complexity estimate, its evidence
paths, its dependencies on other features, and — importantly — a written list of deliberate
exclusions and boundary notes handed verbatim to every later pass, so that two analysts
looking at the same file would agree on which of them owned it.

**2. Per-feature dossiers.** Fifteen independent analysis passes, one per inventory feature,
each reading the source directly rather than reading each other's output. Each produced a
dossier under `dossiers/` with a fixed structure: purpose, behaviour, business rules and
edge cases, quirks, workflows and states, data entities, interfaces, external technology,
error handling, non-functional observations, platform coupling, acceptance criteria, and a
closing confidence block. The dossiers total 11,621 lines.

**3. Adversarial completeness review.** Each dossier was then re-examined against the source
with the specific goal of finding what it had missed or overstated. The visible residue of
that pass is the three-tier confidence block every dossier now carries: *Directly observed*
(with file and line), *INFERRED* (deduction stated as deduction, with the reasoning and an
explicit note that the behaviour was not reproduced), and *Could not determine* (with a list
of where the analyst looked and failed to find an answer). All 15 dossiers carry one. A
separate written critique of the parallel target-technology research package survives at
`research/_completeness-critique.md` (40,557 bytes) and is itself blunt about that package's
integration failures — it is included in the output set rather than discarded.

**4. Synthesis.** The dossiers were rewritten as the 15 numbered subsections of Section 7,
with implementation-specific vocabulary generalised so the requirements bind a reimplementer
without prescribing the original's libraries, and with `FR-n.m` / `AC-n.m` / `US-n.m` /
`QUIRK-n.m` identifiers assigned. Each subsection kept a Source notes block naming the
evidence it was written from, which is what makes §12.2 auditable.

#### Known limitations of this method

- **Dossiers were written independently; consistency was reconciled at synthesis, not
  guaranteed by construction.** Fifteen analysts reading overlapping files can produce
  fifteen slightly different accounts of a shared boundary. The inventory's boundary notes
  reduced this but did not eliminate it. Reconciliation happened during synthesis, by hand.
  Where a fact appears in two subsections, the two statements were aligned; where alignment
  was impossible without new evidence, the divergence is recorded rather than resolved.
  A reader who finds two subsections disagreeing should treat that as a residual defect of
  this method, report it, and fall back to the cited source lines — which are the same in
  both.
- **The residue of independence is visible.** §12.2 already records one instance: seven
  subsections number their scenarios `UC-n.m` and eight do not. §12.3.5 records another: one
  Source-notes citation names a path that does not exist. Neither affects a requirement, but
  both are exactly the class of inconsistency this method admits.
- **The product was never run.** Every behavioural statement is read from source or marked
  `INFERRED` — 109 such markers survive in the written subsections. Nothing was confirmed by
  observation, and the two most common inference patterns (what a UI toolkit does with a
  given widget configuration; what a runtime does with a given build property) are precisely
  where a static reading is weakest.
- **The test suite could not be used as a safety net.** As §12.5 establishes, the suite is
  red and CI never runs it, so it could not be used to confirm any reading of the code.
- **Documentation was systematically distrusted.** This is a deliberate choice with a cost:
  where the source is ambiguous and the documentation is clear, this PRD sides with the
  ambiguous source and records an open question, rather than adopting the documentation's
  answer. In a repository where 2,212 lines of `docs/` describe unbuilt work, that choice was
  correct — but a reimplementer who has access to the original authors should ask them, not
  assume this document guessed right.
- **One branch, one commit.** Fifteen other branches exist. Work in them is invisible here.

---

### 12.7 How to verify a requirement

Every functional requirement in this PRD can be walked back to source in three steps. The
procedure is identical for all 1,354 of them; here it is executed end to end on one.

**Step 1 — start from the requirement identifier in Section 7.**

Open `prd_sections/07-02-settings-configuration.md` and find **FR-2.49**. It reads:

> **FR-2.49** — `/model <model id…>` SHALL join the arguments with single spaces, overwrite
> the model id **with no validation of any kind** — no file-existence check even when the
> local provider is active, no emptiness check — persist the whole record, and return
> `Changed model from '<old>' to '<new>'`. (realizes US-2.4; see QUIRK-2.1)

Two things to carry forward: the cross-reference to **QUIRK-2.1**, and the subsection number,
**7.2**.

**Step 2 — cross to the dossier named in that subsection's Source notes.**

The Source notes block at the end of `07-02-settings-configuration.md` names
`/mnt/g/3RD-Party/reversing/output/chatdbg/dossiers/settings-configuration.md`. Open it and
search for the behaviour. Two hits matter:

- Line 66, under *B4. Change the model*:
  > With arguments: joins them with spaces, overwrites the model id, saves the whole record,
  > and returns `Changed model from '<old>' to '<new>'`. (`Commands/ModelCommand.cs:28-34`)
- Line 247, under *Quirks*, entry **Q1** — the dossier-level identifier that became
  QUIRK-2.1 at synthesis:
  > `/model <anything>` accepts empty-ish input and never checks that a local model file
  > exists, while `/set modelId` does when the local provider is active. Both write the same
  > field and both persist. (`Commands/ModelCommand.cs:28-34` vs `Commands/SetCommand.cs:55-68`)

The dossier hands over two exact source citations. Note that the quirk cites *two* files —
the claim is comparative, so verifying it means reading both.

**Step 3 — read the cited source lines at the pinned commit.**

```
$ git -C <repo> rev-parse HEAD
d8c18f61d6bb73666ed97cd4885e877e35558485

$ sed -n '28,34p' src/Xcaciv.ChatDbg.Core/Commands/ModelCommand.cs
        var newModelId = string.Join(" ", args);
        var oldModelId = _settings.ModelId;

        _settings.ModelId = newModelId;
        await _settingsService.SaveSettingsAsync(_settings);

        return CommandResult.SuccessResult($"Changed model from '{oldModelId}' to '{newModelId}'");

$ sed -n '55,68p' src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs
                case "modelid":
                    var modelPath = string.Join(" ", args.Skip(1));

                    // For LLama provider, check if the model file exists
                    if (_settings.Provider == "llama" && !string.IsNullOrEmpty(modelPath))
                    {
                        if (!File.Exists(modelPath))
                        {
                            return CommandResult.ErrorResult($"LLama model file not found: {modelPath}\nMake sure you've specified the correct path to a GGUF model file.");
                        }
                    }

                    _settings.ModelId = modelPath;
                    break;
```

Every clause of FR-2.49 is now confirmed against text: the space-joining
(`string.Join(" ", args)`), the absence of any guard between the join and the assignment,
the unconditional persist (`SaveSettingsAsync`), and the exact return string. QUIRK-2.1 is
confirmed by the contrast: the second excerpt contains the file-existence check that the
first lacks, and both assign to the same `_settings.ModelId`.

**The general procedure.**

1. Note the requirement's subsection number `n` from its `FR-n.m` identifier; open
   `prd_sections/07-<nn>-*.md`.
2. Read the **Source notes** block at the end of that file. It names the dossier and the
   primary evidence paths. §12.2 of this appendix gives the same mapping in table form.
3. Open the named dossier under `dossiers/`. Its *Behavior*, *Business rules & edge cases*
   and *Quirks* sections carry `path:line` citations.
4. Read those lines in a checkout of `d8c18f61d6bb73666ed97cd4885e877e35558485`. If your
   checkout is at any other commit, line numbers will not match and the verification is void.
5. If the requirement is marked `INFERRED`, stop expecting to find it stated in the code —
   by definition it is a deduction from what the code does not do, or from a library's
   documented semantics. The dossier's *Confidence & open questions* block states the
   reasoning and says explicitly that the behaviour was not reproduced. Those are the
   requirements most worth re-checking against a running build.
