# ChatDbg — Tech Stack As Built

> **Deliverable 2 of 4.** Companion to `PRD-ChatDbg.md` (what the product *does*, implementation-agnostic) and to
> `TECH-STACK-TARGET.md` (what the *rebuild* should use). This document is the opposite of both: it is the
> unvarnished record of the technology decisions the source product actually made, and of which libraries it
> actually uses — as distinct from which it merely declares.
>
> **Why it is separate from the PRD.** The PRD is deliberately technology-free so the product can be rebuilt on any
> stack. But a reimplementer still needs to know what the original leaned on, because the original's choices carry
> sizing information, wire-format contracts, and a list of traps. That is this file. Section 11 ("Decisions That Will
> Not Survive a Port") is the bridge between the two: for each observed choice it says whether the choice is
> load-bearing and what must be preserved *semantically* when the technology is swapped.
>
> **Evidence discipline.** Every claim is anchored to `path:line`. Facts drawn from build output on disk are marked
> **[build output]**; facts from the restore lockfile are marked **[assets]**. Neither is committed to git, so those
> are reproducible only by restoring and building at this commit.
>
> **Headline findings.** `global.json` is malformed JSON (§1.4). Three declared packages are never used by the
> projects that declare them (§2.2). The GPU backend package is an unconditional dependency costing roughly 550 MB
> (§3.2). The AOT and trimming build configurations cannot work against this dependency set and were never validated
> (§8.3). Twenty-eight contradictions between code, docs, build scripts and CI are catalogued in §12.

---

# ChatDbg — Observed Technology Stack

**Repository:** `/mnt/g/3RD-Party/reversing/subject/chatdbg` (read-only)
**Pinned commit:** `d8c18f61d6bb73666ed97cd4885e877e35558485` — *"updating copilot instructions"*
**Tracked files:** 145 (`git ls-files | wc -l`)
**Hand-written C# LOC:** 12,846 across 4 projects (excluding `obj/`, `bin/`)

Every claim below is anchored to `path:line`. Where a fact comes from **build output on disk** (not committed to git — `bin/` and `obj/` are gitignored per `.gitignore:21,22`) it is explicitly labelled **[build output]**. Where a fact comes from a **restore lockfile** (`obj/project.assets.json`, also gitignored) it is labelled **[assets]**. Everything else is from tracked source.

---

## 1. Runtime, Language, and Portability Constraints

### 1.1 Solution layout

| Project | Path | Type | TFM |
|---|---|---|---|
| `Xcaciv.ChatDbg.Core` | `src/Xcaciv.ChatDbg.Core/Xcaciv.ChatDbg.Core.csproj` | Library | `net10.0` |
| `Xcaciv.ChatDbg.Shell` | `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj` | Exe (console REPL) | `net10.0` |
| `Xcaciv.ChatDbg.Shell.Gui` | `src/ChatDbg.Shell.Gui/Xcaciv.ChatDbg.Shell.Gui.csproj` | Exe (TUI) | `net10.0` |
| `Xcaciv.ChatDbg.Core.Tests` | `src/Xcaciv.ChatDbg.Core.Tests/Xcaciv.ChatDbg.Core.Tests.csproj` | xUnit test | `net10.0` |

Solution file: `Xcaciv.ChatDbg.sln:6,17,19,21`. Only `Debug|Any CPU` and `Release|Any CPU` are declared as solution configurations (`Xcaciv.ChatDbg.sln:25-26`) — **the `Compact` and `SingleFile` configurations described in §9 exist only inside the two `.csproj` files and are not reachable from the solution.**

### 1.2 Per-project compiler settings

| Property | Core | Console Shell | GUI Shell | Tests |
|---|---|---|---|---|
| `TargetFramework` | `net10.0` (`:4`) | `net10.0` (`:5`) | `net10.0` (`:5`) | `net10.0` (`:4`) |
| `ImplicitUsings` | `enable` (`:5`) | `enable` (`:6`) | `enable` (`:6`) | `enable` (`:5`) |
| `Nullable` | `enable` (`:6`) | `enable` (`:7`) | `enable` (`:7`) | `enable` (`:6`) |
| `AllowUnsafeBlocks` | **`true`** (`:7`) | not set | not set | not set |
| `OutputType` | (library) | `Exe` (`:4`) | `Exe` (`:4`) | (library) |
| `IsTestProject` | — | — | — | `true` (`:8`) |
| `IsPackable` | — | — | — | `false` (`:7`) |

`AllowUnsafeBlocks=true` is set on Core (`src/Xcaciv.ChatDbg.Core/Xcaciv.ChatDbg.Core.csproj:7`) but **no `unsafe` block, `stackalloc`, or pointer type appears anywhere in Core's 3,600 lines of source** — it is a leftover from an abandoned low-level LLamaSharp interop attempt (see `docs/llamasharp-lowlevel-api-implementation-plan.md`).

### 1.3 Language version

`Directory.Build.props` (repo root) is the **only** cross-cutting MSBuild file and sets exactly one property:

```xml
<LangVersion>latest</LangVersion>   <!-- Directory.Build.props:3 -->
```

There is **no** `Directory.Packages.props` (no Central Package Management), **no** `.editorconfig`, **no** `.runsettings`, **no** `nuget.config`, and no `TreatWarningsAsErrors` / `EnableNETAnalyzers` anywhere. Verified by `find` over the whole tree.

Language features actually observed in source: file-scoped namespaces (all files), collection expressions absent, target-typed `new`, raw string literals (`src/Xcaciv.ChatDbg.Core.Tests/Services/AzureOpenAIServiceTests.cs:40`), range/index operators (`filePath[2..]` in `src/Xcaciv.ChatDbg.Core/Commands/ExportCommand.cs:33`; `tokenProbabilities[^1]` in `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:237`), switch expressions, top-level statements in both `Program.cs` files. Nothing requires C# 13/14 specifically.

### 1.4 SDK pin and roll-forward — **`global.json` is malformed JSON**

```json
{
  "sdk": {
    "version": "10.0.100-rc.1.25451.107",
    "rollForward": "latestFeature"
  }
}}
```
`global.json:1-6` — the file is 96 bytes and ends `}\n}}` (`xxd` tail: `...2020 7d0a 7d7d`). `json.load` fails with `Extra data: line 6 column 2 (char 95)`. There is a **stray trailing `}`**.

| Fact | Value | Evidence |
|---|---|---|
| Pinned SDK | `10.0.100-rc.1.25451.107` (a **release-candidate/preview** SDK) | `global.json:3` |
| Roll-forward policy | `latestFeature` — accepts any 10.0.**1xx**+ feature band, but not 11.x | `global.json:4` |
| File validity | **Invalid JSON** (extra closing brace at byte 95) | `xxd global.json`, `python3 -m json.tool` |

**Portability constraints introduced here:**
- Requires a **preview/RC .NET 10 SDK**; a machine with only .NET 9 or only GA .NET 10.0.100 stable may not satisfy the pin (and `latestFeature` will not roll *back*).
- The malformed `global.json` is a hard blocker for any tooling that parses it strictly.
- No `RuntimeIdentifiers` are declared in any project's default `PropertyGroup`; RIDs appear only inside the `Compact`/`SingleFile` conditionals, hardcoded to `win-x64` (§9).

---

## 2. NuGet Package Inventory — exact pinned versions, per project, and actual use

### 2.1 Direct `PackageReference` declarations (source of truth: the `.csproj` files)

| Package | Exact version | Declared in (file:line) | What the code actually does with it |
|---|---|---|---|
| `AWSSDK.BedrockRuntime` | `4.0.7.3` | Core `:11`; Console Shell `:108`; GUI Shell `:118` | **Core only.** `IAmazonBedrockRuntime` / `AmazonBedrockRuntimeClient` construction with `RegionEndpoint.GetBySystemName` (`Services/DefaultBedrockRuntimeClientFactory.cs:13,16,20`), and exactly one API call — `InvokeModelAsync` with a hand-built `InvokeModelRequest{ModelId, ContentType, Accept, Body=MemoryStream}` (`Services/BedrockService.cs:110-118`). No streaming, no Converse API, no paginators. **Redundant in both Shell projects** (see §2.3). |
| `Azure.AI.OpenAI` | `2.1.0` | Core `:12`; Console Shell `:109`; GUI Shell `:119` | **Core only, and only for one type.** `new AzureOpenAIClient(endpoint, new ApiKeyCredential(apiKey)).GetChatClient(modelId)` (`Services/DefaultAzureOpenAIClientFactory.cs:12-13`). Everything else goes through the transitive `OpenAI` package's `ChatClient`/`ChatMessage`/`ChatCompletionOptions` (`Services/AzureOpenAIService.cs:76,79,87,90,93,98,103`). **Redundant in both Shell projects.** |
| `LLamaSharp` | `0.25.0` | Core `:13` | Local GGUF inference. Uses `LLamaWeights.LoadFromFile`, `ModelParams`, `LLamaContext`, `InteractiveExecutor`, `ChatSession.ChatAsync`, `InferenceParams`, `DefaultSamplingPipeline`, `AuthorRole`, `LLama.Common.ChatHistory` (`Services/LLamaSharpService.cs:548,567,595,606,610,141-150,170-172`), `context.Tokenize(...)` (`Services/TokenInspectionService.cs:46`, `Commands/TokenizeCommand.cs:67`), and `LLama.Native.NativeLogConfig.llama_log_set` for the native log callback (`Services/TokenInspection/LLamaSharpLogConfig.cs:1,81`). |
| `LLamaSharp.Backend.Cpu` | `0.25.0` | Core `:14` | Pure native payload package — no managed API. Supplies llama.cpp binaries for 6 RIDs (§3). |
| `LLamaSharp.Backend.Cuda12` | `0.25.0` | Core `:15` | Pure native payload metapackage — no managed API. Pulls CUDA-12 llama.cpp for `win-x64` + `linux-x64` (§3). |
| `Spectre.Console` | `0.51.1` | Core `:16`; Console Shell `:110`; GUI Shell `:120` | **NOT used in Core** — see §2.2. In the Console Shell: `AnsiConsole.MarkupLine/Write/WriteLine`, `Rule`, `Panel`, `Table`, `Grid` (`src/ChatDbg/ChatShell.cs:6,414,417,453,467,481,493,496`, 11 `AnsiConsole` call sites). In the GUI Shell: used **only for its markup-based table/grid/panel renderers** in the out-of-band `IConsoleFormatter` implementation — `new Table().Border(TableBorder.Rounded)`, `new Grid()` with `GridColumn().NoWrap()`, `new Panel(...)`, `new Rule(...).LeftJustified()` (`Services/SpectreConsoleFormatter.cs:44-50,61-66,109-117`) and the standalone `TokenProbabilityVisualizer` (`Services/TokenProbabilityVisualizer.cs:4`). |
| `Terminal.Gui` | `1.19.0` | GUI Shell `:121` | The entire TUI. `Application.Init/Run/Shutdown/Top/Refresh/Driver/MainLoop`, `Window`, `View`, `Dialog`, `MenuBar`, `MenuItem`, `StatusBar`, `StatusItem`, `FrameView`, `Label`, `Button`, `TextField`, `TextView`, `ListView`, `ScrollView`, `MessageBox`, `OpenDialog`, `SaveDialog`, `ColorScheme`, `Terminal.Gui.Attribute`, `Dim`/`Pos` layout, `Colors.Base/Dialog/Menu/Error` (`UI/ChatWindow.cs`, `UI/SettingsDialog.cs`, `UI/SystemPromptsDialog.cs`, `UI/ThemeManager.cs:16-44`, `UI/LogProbHeatmapView.cs:8,23-26`, `Program.cs:71,74,89,90,94`). |
| `Microsoft.NET.Test.Sdk` | `17.12.0` | Tests `:12` | VSTest host/adapter plumbing for `dotnet test`. |
| `xunit` | `2.9.1` | Tests `:13` | Test framework — 100 `[Fact]` methods, 0 `[Theory]`. |
| `xunit.runner.visualstudio` | `2.8.1` | Tests `:14-17` (`PrivateAssets=all`) | VSTest adapter that discovers xUnit tests. |
| `Moq` | `4.20.69` | Tests `:18` | Interface mocking (25 `new Mock<…>()` sites). |
| `coverlet.collector` | `6.0.2` | Tests `:19` | Coverage data collector (`--collect:"XPlat Code Coverage"`). No `.runsettings` exists to configure it. |

### 2.2 Packages referenced but **never used in code**

| Package | Project | Proof of non-use |
|---|---|---|
| **`Spectre.Console` `0.51.1`** | **`Xcaciv.ChatDbg.Core`** (`:16`) | Only four hits for `Spectre` in all of Core: three *vestigial* `using Spectre.Console;` directives at `Commands/ExportLogsCommand.cs:1`, `Commands/ExportTokenAnalysisCommand.cs:1`, `Commands/ShowTokenAnalysisCommand.cs:1` — all three files are 25–50-line stubs that return a hardcoded `CommandResult` string and reference **no** Spectre type — plus a comment at `Services/BasicConsoleFormatter.cs:10`. Core deliberately routes all rich output through its own `IConsoleFormatter` abstraction (`Services/IConsoleFormatter.cs`) with a dependency-free `BasicConsoleFormatter` fallback (`Services/BasicConsoleFormatter.cs:12`). **The Core PackageReference can be deleted with no code change.** |
| **`AWSSDK.BedrockRuntime` `4.0.7.3`** | **Console Shell** (`:108`) and **GUI Shell** (`:118`) | Neither project has a single `using Amazon…` or any `Amazon.*` type reference (`grep -rn "^using" src/ChatDbg src/ChatDbg.Shell.Gui` → only `ChatDBG`, `System.*`, `Spectre.Console`, `Terminal.Gui`, `Xcaciv.ChatDbg.*`). The only textual hit is the string literal `"Supports Amazon Bedrock and Azure OpenAI"` in the About box (`src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:1133`). The assembly arrives transitively via `ProjectReference` to Core. |
| **`Azure.AI.OpenAI` `2.1.0`** | **Console Shell** (`:109`) and **GUI Shell** (`:119`) | Same proof — no `using Azure…` in either shell. |

### 2.3 Packages referenced by more than one project

All at **identical versions** — there is no version skew among direct references.

| Package | Version | Projects declaring it | Notes |
|---|---|---|---|
| `AWSSDK.BedrockRuntime` | `4.0.7.3` | Core, Console Shell, GUI Shell | Same version in all 3; redundant in the 2 shells |
| `Azure.AI.OpenAI` | `2.1.0` | Core, Console Shell, GUI Shell | Same version in all 3; redundant in the 2 shells |
| `Spectre.Console` | `0.51.1` | Core, Console Shell, GUI Shell | Same version in all 3; **unused in Core** |

Because there is no `Directory.Packages.props`, these versions are **triplicated by hand** in three files. Any bump must be applied three times or the projects silently diverge.

### 2.4 Full resolved dependency closure **[assets]**

`Xcaciv.ChatDbg.Core` resolves to **19** packages for `net10.0`:

| Package | Resolved version | Reached via |
|---|---|---|
| `AWSSDK.BedrockRuntime` | `4.0.7.3` | direct |
| `AWSSDK.Core` | `4.0.0.33` | `AWSSDK.BedrockRuntime` → `[4.0.0.33, 5.0.0)` |
| `Azure.AI.OpenAI` | `2.1.0` | direct |
| `Azure.Core` | `1.44.1` | `Azure.AI.OpenAI` |
| `OpenAI` | `2.1.0` | `Azure.AI.OpenAI` (**this is the package whose `ChatClient` the code actually uses**) |
| `System.ClientModel` | `1.2.1` | `OpenAI` |
| `System.Memory.Data` | `6.0.0` | `Azure.Core` |
| `LLamaSharp` | `0.25.0` | direct (`lib/net8.0/LLamaSharp.dll`) |
| `CommunityToolkit.HighPerformance` | `8.4.0` | `LLamaSharp` |
| `Microsoft.Bcl.AsyncInterfaces` | `9.0.8` | `LLamaSharp` |
| `Microsoft.Extensions.AI.Abstractions` | `9.7.1` | `LLamaSharp` |
| `Microsoft.Extensions.Logging.Abstractions` | `9.0.8` | `LLamaSharp` |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `9.0.8` | `Microsoft.Extensions.*` chain |
| `System.Numerics.Tensors` | `9.0.8` | `LLamaSharp` |
| `LLamaSharp.Backend.Cpu` | `0.25.0` | direct |
| `LLamaSharp.Backend.Cuda12` | `0.25.0` | direct |
| `LLamaSharp.Backend.Cuda12.Linux` | `0.25.0` | `LLamaSharp.Backend.Cuda12` |
| `LLamaSharp.Backend.Cuda12.Windows` | `0.25.0` | `LLamaSharp.Backend.Cuda12` |
| `Spectre.Console` | `0.51.1` | direct (unused) |

`Xcaciv.ChatDbg.Shell.Gui` adds **4** more (24 total):

| Package | Resolved version | Reached via | Significance |
|---|---|---|---|
| `Terminal.Gui` | `1.19.0` | direct (`lib/net8.0/Terminal.Gui.dll`) | The TUI |
| `NStack.Core` | `1.1.1` | `Terminal.Gui` | `ustring` rune handling (`lib/netstandard2.0`) |
| **`System.Management`** | `9.0.4` | `Terminal.Gui` | **WMI. Ships `runtimes/win/lib/net9.0/System.Management.dll` — a Windows-only native-backed asset dragged into the GUI app purely by the TUI library.** |
| `System.CodeDom` | `9.0.4` | `System.Management` | Runtime code generation — reflection-heavy, AOT-hostile |

`Xcaciv.ChatDbg.Core.Tests` adds **9** more (37 total): `Microsoft.NET.Test.Sdk 17.12.0`, `Microsoft.CodeCoverage 17.12.0`, `Microsoft.TestPlatform.ObjectModel 17.12.0`, `Microsoft.TestPlatform.TestHost 17.12.0`, `Moq 4.20.69`, `Castle.Core 5.1.1` (Moq's DynamicProxy), `Newtonsoft.Json 13.0.1` (via TestPlatform), `System.Diagnostics.EventLog 6.0.0` (via TestPlatform), `coverlet.collector 6.0.2`, and the xunit family (`xunit 2.9.1`, `xunit.core/assert/extensibility.* 2.9.1`, `xunit.abstractions 2.0.3`, `xunit.analyzers 1.16.0`, `xunit.runner.visualstudio 2.8.1`).

**Framework-version skew inside the closure:** the app targets `net10.0`, but every third-party assembly it loads is compiled for an older TFM — `LLamaSharp` and `Terminal.Gui` ship `lib/net8.0`, `AWSSDK.BedrockRuntime` ships `lib/net8.0`, `Spectre.Console` and `System.Management` ship `lib/net9.0`, `OpenAI` ships `lib/net6.0`, `Azure.AI.OpenAI` and `NStack.Core` ship `lib/netstandard2.0`. Nothing in the closure was built for .NET 10.

---

## 3. Native and Transitive Payloads: what the LLamaSharp backends actually drag in

### 3.1 `LLamaSharp.Backend.Cpu 0.25.0` — 85 native files across **6 RIDs** **[assets]**

Declared as `runtimeTargets` in `src/Xcaciv.ChatDbg.Core/obj/project.assets.json`. Package contains **zero managed assemblies** and one MSBuild props file (`build/netstandard2.0/LLamaSharp.Backend.Cpu.props`).

| RID | ISA variants shipped | Files each | Libraries per variant |
|---|---|---|---|
| `win-x64` | `noavx`, `avx`, `avx2`, `avx512` | 5 | `ggml-base.dll`, `ggml-cpu.dll`, `ggml.dll`, `llama.dll`, `mtmd.dll` |
| `linux-x64` | `noavx`, `avx`, `avx2`, `avx512` | 5 | `libggml-base.so`, `libggml-cpu.so`, `libggml.so`, `libllama.so`, `libmtmd.so` |
| `linux-musl-x64` | `noavx`, `avx`, `avx2`, `avx512` | 5 | (same `.so` set — Alpine/musl) |
| `linux-arm64` | (single) | 5 | (same `.so` set) |
| `osx-arm64` | (single, + Metal) | 8 | adds `libggml-blas.dylib`, `libggml-metal.dylib`, **`ggml-metal.metal`** (a shader source file, not a library) |
| `osx-x64` | native + `rosetta2` | 6 each | adds `libggml-blas.dylib` |

### 3.2 `LLamaSharp.Backend.Cuda12 0.25.0` — a metapackage **[assets]**

`LLamaSharp.Backend.Cuda12` itself contains only `build/netstandard2.0/LLamaSharp.Backend.Cuda12.props` and declares two dependencies, **both of which are unconditionally pulled regardless of the RID you build for**:

- `LLamaSharp.Backend.Cuda12.Windows 0.25.0` → 5 files under `runtimes/win-x64/native/cuda12/` (`ggml-base.dll`, `ggml-cuda.dll`, `ggml.dll`, `llama.dll`, `mtmd.dll`)
- `LLamaSharp.Backend.Cuda12.Linux 0.25.0` → 5 files under `runtimes/linux-x64/native/cuda12/` (`libggml-base.so`, `libggml-cuda.so`, `libggml.so`, `libllama.so`, `libmtmd.so`)

Both also depend on `LLamaSharp.Backend.Cpu 0.25.0`, so **taking the CUDA backend forces the CPU backend's 85 files in as well.**

### 3.3 Measured on-disk cost **[build output]**

Measured against the RID-agnostic `Debug` build of the console shell in the working tree (`bin/` is gitignored, so this is not committed evidence — but it is the real output of this exact package set):

| Path | Size |
|---|---|
| `src/ChatDbg/bin/Debug/net10.0` (whole app output) | **630 MB** |
| `src/ChatDbg/bin/Debug/net10.0/runtimes` (native only) | **623 MB** across **95 files**, 6 RID directories |
| `src/ChatDbg.Shell.Gui/bin/Debug/net10.0` | **631 MB** (adds `runtimes/win/` for `System.Management`) |
| `src/Xcaciv.ChatDbg.Core.Tests/bin/Debug/net10.0` | **636 MB** — *the unit-test project also carries the full 624 MB native payload* |

Where the mass actually is:

| Native file | Bytes |
|---|---|
| `runtimes/win-x64/native/cuda12/ggml-cuda.dll` | **288,125,952** (≈275 MiB) |
| `runtimes/linux-x64/native/cuda12/libggml-cuda.so` | **288,482,056** (≈275 MiB) |
| `runtimes/win-x64/native/cuda12/llama.dll` | 1,540,608 |
| `runtimes/linux-x64/native/cuda12/libllama.so` | 2,460,408 |
| All four `win-x64` CPU ISA variants combined | ≈12.8 MB (3.1–3.3 MB each) |

**Two files — the Windows and Linux CUDA-12 GGML kernels — account for ~550 MB, i.e. ~88 % of the entire build output.** They are copied into *every* project's output including the test project, on *every* platform, because `LLamaSharp.Backend.Cuda12` is an unconditional `PackageReference` with no RID or property guard (`src/Xcaciv.ChatDbg.Core/Xcaciv.ChatDbg.Core.csproj:15`).

### 3.4 Binary-size evidence in the repo (all of it is **claimed**, none of it is **measured** at this commit)

| Source | Claim |
|---|---|
| `docs/compact-build.md:124-126` | Compact (AOT) 8–15 MB; SingleFile 15–25 MB; Regular Release 50–100 MB |
| `docs/github-actions-release.md:86,92` | "~13-25 MB (depending on dependencies)" for both Windows and Linux |
| `docs/release-setup-complete.md:62-63` | Windows `~13-15 MB`, Linux `~15-17 MB` |
| `.github/workflows/build-release.yml:95-101` | The CI job *computes* `stat -c%s` and echoes `file_size`, but writes it to `$GITHUB_OUTPUT` in a step whose `id: file_info` is declared **after** the `run:` block (`:102`) and is never consumed by any later step. **No size is published anywhere.** |
| `build-singlefile.bat:25-27`, `build-compact.ps1:31` | Print the published `.exe` size to the console; no artifact retained |

**The one hard size artifact in the repo:** `test-publish/Xcaciv.ChatDbg.Shell` is a *tracked* file (`git ls-files test-publish/` → present; `.gitignore` has no rule matching `test-publish/`). `ls -l` → **15,677,171 bytes (14.95 MiB)**, dated 2025-10-01. `file` reports: *ELF 64-bit LSB shared object, x86-64, dynamically linked, for GNU/Linux 2.6.32, stripped*. That is a **Linux single-file publish of the console shell that clearly does not contain any llama.cpp native payload** — 15 MB cannot hold a 275 MB CUDA kernel, and the docs' 13–25 MB figures likewise predate or exclude the LLamaSharp backends.

**Conclusion on size:** every documented size figure in the repo describes a build *without* the LLamaSharp native backends. The real, measured cost of the package set at this commit is **≈630 MB of output per project**. A reimplementer must treat "ship a local-LLM backend" as a ~550 MB decision, not a ~15 MB one.

### 3.5 Platform-support consequences of the native payload

- llama.cpp natives exist for `win-x64`, `linux-x64`, `linux-musl-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`. **There is no `win-arm64` native** — the local-LLM provider cannot work on Windows on ARM.
- The CUDA backend covers only `win-x64` and `linux-x64`. macOS gets Metal via the CPU backend's `osx-arm64` assets instead.
- The `Compact`/`SingleFile` configurations hardcode `RuntimeIdentifier=win-x64` (`src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:30,70`), so a RID-specific publish prunes the other five RIDs — but only for the shells, and only for `win-x64` unless overridden on the command line.

---

## 4. Test Stack

### 4.1 Frameworks and versions

| Concern | Choice | Exact version | Evidence |
|---|---|---|---|
| Test framework | xUnit.net v2 | `2.9.1` | `…Core.Tests.csproj:13` |
| Test host / adapter | `Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio` | `17.12.0` / `2.8.1` | `:12`, `:14` |
| Mocking | Moq (Castle DynamicProxy `5.1.1` transitively) | `4.20.69` | `:18` |
| Coverage collector | `coverlet.collector` (XPlat) | `6.0.2` | `:19` |
| Assertions | xUnit's built-in `Assert` — no FluentAssertions / Shouldly | — | 37 files, all `using Xunit;` only |
| Runner config | **none** — no `.runsettings`, no `[assembly: CollectionBehavior]`, no fixtures | — | `find` over tree |

Test surface: **37 test files, 100 `[Fact]` methods, 0 `[Theory]`, 0 `[InlineData]`** (`grep -rho "\[Fact\]\|\[Theory\]\|\[InlineData"` → `100 [Fact]`). No parameterised tests at all; variation is expressed by copy-pasted `[Fact]` methods.

**Version-alignment note:** `Microsoft.NET.Test.Sdk 17.12.0` (a VSTest generation aligned with .NET 9-era tooling) is being used to host a `net10.0` test assembly. It works in this tree — the test project's `bin/Debug/net10.0/` contains `testhost.exe` and the full `Microsoft.TestPlatform.*` set **[build output]** — but it is a deliberately older host than the target framework, and the project uses the legacy VSTest path rather than Microsoft.Testing.Platform.

### 4.2 Testing patterns actually used — a **three-way mix**

**(a) Moq interface mocks** — 25 `new Mock<…>()` sites, used exclusively for interfaces the production code was refactored to accept:

| Mocked type | Test files |
|---|---|
| `ISettingsService` | `Commands/LogProbsCommandTests.cs:16,29,44`; `Commands/ModelCommandTests.cs:16,29`; `Commands/SetCommandTests.cs:16,29,44,56,69`; `Commands/PromptCommandTests.cs:22,39,59` |
| `IChatHistoryService` | `Commands/ImportCommandTests.cs:16,32`; `Commands/ExportCommandTests.cs:16,30` |
| `ISystemPromptService` | `Commands/PromptCommandTests.cs:23,40,61` |
| `IConsoleFormatter` | `Commands/DemoLogProbsCommandTests.cs:19` |
| `ICommand` | `Commands/HelpCommandTests.cs:15,36` |
| `IBedrockRuntimeClientFactory` | `Services/BedrockServiceTests.cs:43` |
| **`IAmazonBedrockRuntime`** (a third-party SDK interface) | `Services/BedrockServiceTests.cs:44` — set up on `InvokeModelAsync(It.IsAny<InvokeModelRequest>(), It.IsAny<CancellationToken>())` returning an `InvokeModelResponse` with a `MemoryStream` body (`:45-50`), verified with `Times.Once` (`:69-70`) |

**(b) Hand-rolled test doubles — 2 files, both named explicitly:**

| File | Kind | What it does |
|---|---|---|
| `src/Xcaciv.ChatDbg.Core.Tests/TestDoubles/StubHttpMessageHandler.cs` | **Stub** (the only file in the `TestDoubles/` folder) | `internal class StubHttpMessageHandler : HttpMessageHandler` (`:8`); ctor takes `(HttpStatusCode, string content)` (`:13`); `SendAsync` returns `Task.FromResult(new HttpResponseMessage(status){Content = new StringContent(content)})` (`:19-25`). Ignores the request entirely. This is the seam that makes `AzureOpenAIService`'s raw-REST log-probability path testable. |
| `src/Xcaciv.ChatDbg.Core.Tests/Services/AzureOpenAIServiceTests.cs:115-121` | **Fake / explosive dummy**, `private sealed class FakeChatClientFactory : IAzureOpenAIClientFactory` | `CreateChatClient` **throws `NotSupportedException("Chat client path is not used in this test context.")`**. It exists purely to prove the SDK path is never taken when `EnableLogProbabilities=true`. |

**(c) Real filesystem I/O against temp directories** — no filesystem abstraction exists, so the persistence tests write real files:

| Test | Directory expression |
|---|---|
| `Services/SettingsServiceTests.cs:15,43` | `Path.Combine(Path.GetTempPath(), "ChatDbgSettingsTests", Guid.NewGuid().ToString())` |
| `Services/SystemPromptServiceTests.cs:15,37` | `Path.Combine(Path.GetTempPath(), "ChatDbgPrompts", Guid.NewGuid().ToString())` |
| `Services/ChatHistoryServiceTests.cs:15,45` | `Path.Combine(Path.GetTempPath(), "ChatDbgTests", Guid.NewGuid().ToString())` |
| `Services/TokenInspection/LLamaSharpLogConfigTests.cs:72,127` | `Path.GetTempPath()` |
| `Services/LLamaSharpServiceTests.cs:16,24`, `Commands/TokenizeCommandTests.cs:25` | A **non-existent** `missing.gguf` path — the tests only exercise the "not configured" guard, never real inference |

**No test ever loads a GGUF model or calls a real endpoint.** `TokenInspectionServiceTests.cs` is 21 lines. `LLamaSharpServiceTests.cs` is 48 lines and only checks `IsConfigured`. There is **no integration-test project, no test fixture, no collection, no `IAsyncLifetime`.**

---

## 5. Persistence and Serialization

### 5.1 The one format: JSON via `System.Text.Json` (reflection-based)

There is no database, no binary format, no XML, no YAML, and **no `JsonSerializerContext` / source-generated serialization anywhere** (`grep -rn "JsonSerializerContext\|JsonSourceGeneration"` → zero hits). Everything is `System.Text.Json` reflection.

### 5.2 Serializer options — **three different configurations, inconsistently applied**

| Serializer instance | Options set | Evidence |
|---|---|---|
| `SettingsService._jsonOptions` | `WriteIndented = true`, `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` | `Services/SettingsService.cs:34-38` |
| `ChatHistoryService._jsonOptions` | `WriteIndented = true`, `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` | `Services/ChatHistoryService.cs:18-22` |
| `SystemPromptService._jsonOptions` | `WriteIndented = true` **only — no naming policy** | `Services/SystemPromptService.cs:34-37` |
| `LLamaSharpService.SaveTokenAnalysesToFile` | inline `new JsonSerializerOptions { WriteIndented = true }` | `Services/LLamaSharpService.cs:633-640` |
| Azure REST request body | `PropertyNamingPolicy = CamelCase`, `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` | `Services/AzureOpenAIService.cs:165-169` |
| Bedrock request body | **`JsonSerializer.Serialize(requestBody)` / `SerializeToUtf8Bytes(requestBody)` with NO options at all** | `Services/BedrockService.cs:107,115` |

The camelCase policies are largely **cosmetic**, because every persisted model already carries explicit `[JsonPropertyName]` attributes which win over the policy: `ChatSettings` (24 attributes, `Models/ChatSettings.cs:7-86`), `ChatMessage` (`Models/ChatMessage.cs:7-16`), `ChatHistory` (`Models/ChatHistory.cs:7-12`), `SystemPrompt` (`Models/SystemPrompt.cs:7-16`), `AIResponse` (`Models/AIResponse.cs:13-32`), `TokenLogProbability` (`Models/TokenLogProbabilities.cs:13-32`).

**Converters:** none — zero custom `JsonConverter` types in the codebase. `DateTime` fields (`ChatMessage.Timestamp`, `ChatHistory.CreatedAt`, `SystemPrompt.CreatedAt/LastUsedAt`) rely on STJ's default ISO-8601 round-trip.

**Naming inconsistency baked into the wire format:** `TokenLogProbability.TopAlternatives` is `[JsonPropertyName("top_alternatives")]` — snake_case (`Models/TokenLogProbabilities.cs:31`) — sitting inside otherwise camelCase documents. `LogProb` is `"logprob"` (`:19`). These are load-bearing for any file round-trip.

**Credential fields that are `[JsonIgnore]` vs. persisted:** `AzureApiKey`/`AwsAccessKey`/`AwsSecretKey` are computed `[JsonIgnore]` properties (`Models/ChatSettings.cs:69-76`), while the *deprecated* backing fields `JsonAzureApiKey`/`JsonAwsAccessKey`/`JsonAwsSecretKey` **are serialized** under the plain names `"azureApiKey"`, `"awsAccessKey"`, `"awsSecretKey"` (`:79-86`). So plaintext secrets can and do land in `settings.json`; `HasJsonStoredCredentials()` (`:147`) detects that and `SettingsService.LoadSettingsAsync` prints a migration warning (`Services/SettingsService.cs:60-64`).

`SystemPromptContent` is `[JsonIgnore]` (`Models/ChatSettings.cs:29`) — only the prompt *name* is persisted (`:26-27`) and the content is re-hydrated from disk at startup (`src/ChatDbg/ChatShell.cs`; `src/ChatDbg.Shell.Gui/Program.cs:61-68`).

### 5.3 On-disk locations — **three different special folders, no single app-data root**

| What | Path expression | Resolves to (Windows) | Resolves to (Linux/macOS) | Evidence |
|---|---|---|---|---|
| `settings.json` | `Path.Combine(Environment.GetFolderPath(SpecialFolder.UserProfile), ".ChatDbg")` + `"settings.json"` | `C:\Users\<u>\.ChatDbg\settings.json` | `~/.ChatDbg/settings.json` | `Services/SettingsService.cs:15-18,25`; default `fileName` param `:11` |
| ↳ fallback | `Path.GetTempPath()` if `UserProfile` is empty **or** if resolving it throws | `%TEMP%\settings.json` | `/tmp/settings.json` | `Services/SettingsService.cs:20-23, 27-32` |
| System prompts | `Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "ChatDbg")` + `"system_prompts"` | `C:\Users\<u>\AppData\Local\ChatDbg\system_prompts\` | `~/.local/share/ChatDbg/system_prompts/` | `Services/SystemPromptService.cs:14-17,23` |
| ↳ one file per prompt | `{sanitizedName}.json`, sanitised by `string.Join("_", name.Split(Path.GetInvalidFileNameChars()))` | — | — | `Services/SystemPromptService.cs:209-214` |
| LLamaSharp logs | `Path.Combine(Environment.GetFolderPath(SpecialFolder.ApplicationData), "ChatDbg", "Logs")` + `llamasharp_{yyyyMMdd}.log` | `C:\Users\<u>\AppData\Roaming\ChatDbg\Logs\` | `~/.config/ChatDbg/Logs/` | `Services/TokenInspection/LLamaSharpLogConfig.cs:20,149-150` |
| `/export` default | user-supplied path; `~/` prefix expanded to `SpecialFolder.UserProfile`; `.json` appended if no extension | — | — | `Commands/ExportCommand.cs:31-40` |
| `/import` | same `~/` expansion | — | — | `Commands/ImportCommand.cs:33` |
| `/prompt export` default | `Path.Combine(Environment.GetFolderPath(SpecialFolder.MyDocuments), $"chatdbg_prompt_{sanitizedName}.txt")` | `C:\Users\<u>\Documents\` | `~/Documents/` (often unset → `~`) | `Commands/PromptCommand.cs:313-314` |
| GUI file dialogs seed | `Environment.GetFolderPath(SpecialFolder.UserProfile)` | — | — | `UI/ChatWindow.cs:1008,1023`; `UI/SystemPromptsDialog.cs:646` |

**Three different roots — `UserProfile`, `LocalApplicationData`, `ApplicationData` — for three artefacts of the same application.** On Linux, `LocalApplicationData` and `ApplicationData` resolve to *different* XDG directories, so a port must decide deliberately which (if any) of this split to preserve.

**Default-write side effect:** `SettingsService.LoadSettingsAsync` **writes a default `settings.json` to disk if none exists** before returning (`Services/SettingsService.cs:47-53`), and `SystemPromptService`'s **constructor** creates the directory (`:26-29`) and writes four default prompts — `default`, `code-reviewer`, `algorithm-helper`, `security-expert` (`:168-203`). Merely constructing the service mutates the user's disk.

**Ordering bug worth preserving-or-fixing:** `SystemPromptService`'s constructor calls `CreateDefaultSystemPromptsAsync().GetAwaiter().GetResult()` at line 32, **before** `_jsonOptions` is assigned at line 34. The four default prompts are therefore serialized with `options == null` (STJ default, **not indented**), while every subsequently saved prompt is indented. The on-disk formatting of default vs. user prompts differs.

---

## 6. Concurrency and Async

### 6.1 Async shape

The codebase is `async`/`await` throughout at the service and command layer. `ICommand.ExecuteAsync` returns `Task<CommandResult>` (`Models/ICommand.cs:8`); `IAIService` exposes `Task<string> SendMessageAsync` and `Task<AIResponse> SendMessageWithLogProbsAsync` (`Services/IAIService.cs:9-10`); `ISettingsService` and `ISystemPromptService` are fully async (`Services/ISettingsService.cs:7-11`, `Services/ISystemPromptService.cs:7-11`). Both entry points are `async` top-level statements (`src/ChatDbg/Program.cs:3-6`, `src/ChatDbg.Shell.Gui/Program.cs:10,58,63`).

Streaming is consumed with `await foreach` over `ChatSession.ChatAsync(...)` (`Services/LLamaSharpService.cs:170-172, 390-392`).

Several commands are synchronous work wrapped in `Task.FromResult` to satisfy the async interface: `HelpCommand.ExecuteAsync` (`Commands/HelpCommand.cs:24,32,34`), `ExportLogsCommand` (`:16,20,23`), `ExportTokenAnalysisCommand` (`:16,20,23`), `ShowTokenAnalysisCommand` (`:17,48`), `DemoLogProbsCommand`. Two private helpers end with a pointless `await Task.CompletedTask;` to be async at all (`Services/SettingsService.cs:286, 359`).

### 6.2 Blocking calls — **exactly one**, and it is in a constructor

```csharp
CreateDefaultSystemPromptsAsync().GetAwaiter().GetResult();   // SystemPromptService.cs:32
```
`src/Xcaciv.ChatDbg.Core/Services/SystemPromptService.cs:32`. This is the **only** sync-over-async site in the codebase — a repo-wide grep for `.Result`, `.Wait()`, `.RunSynchronously` returns no other hits. Because it runs inside a constructor invoked from `Program.cs`/`ChatShell` startup, it blocks the calling thread on file enumeration + up to four file writes.

### 6.3 `Task.Run` offloading

Used **only** to keep native model loading off the caller's thread — 6 sites:

| Site | Work offloaded |
|---|---|
| `Services/LLamaSharpService.cs:563` | `LLamaWeights.LoadFromFile(modelParams)` (wrapped in its own try/catch that rethrows an `InvalidOperationException` with a 4-point diagnostic message, `:572-579`) |
| `Services/LLamaSharpService.cs:595` | `_model.CreateContext(modelParams)` |
| `Services/TokenInspectionService.cs:38,115` | `LLamaWeights.LoadFromFile` |
| `Services/TokenInspectionService.cs:41,118` | `weights.CreateContext` |
| `Commands/TokenizeCommand.cs:59,62` | same pair |

### 6.4 Locking and thread-safety mechanisms

| Mechanism | Where | Guards |
|---|---|---|
| `static readonly SemaphoreSlim _modelLoadSemaphore = new(1, 1)` | `Services/LLamaSharpService.cs:23`, taken at `:518`, released in `finally` at `:621` | Serialises GGUF model load/context creation **process-wide** (it is `static`) |
| `static readonly SemaphoreSlim _generationSemaphore = new(1, 1)` | `Services/LLamaSharpService.cs:24`, taken at `:72`, released in `finally` at `:113` | Serialises *all* inference — one generation at a time, process-wide |
| `private readonly object _logLock = new()` + 6 `lock` blocks | `Services/TokenInspection/LLamaSharpLogConfig.cs:13`, locks at `:47, 58, 85, 123, 152, 180` | Protects the `StringBuilder _logBuffer` against the **native llama.cpp log callback**, which can fire on a native thread |

Nothing else in the codebase locks. `ChatHistory.Messages` is a bare `List<ChatMessage>` (`Models/ChatHistory.cs:8`) mutated from UI event handlers with no synchronisation. The `Dictionary<string, ICommand>` and `Dictionary<string, IAIService>` registries (`src/ChatDbg/ChatShell.cs:17-18`, `src/ChatDbg.Shell.Gui/Program.cs:22,30`) are built once and read thereafter. No `Concurrent*` collection, no `Interlocked`, no `volatile`, no `Mutex`, no `Monitor.*` anywhere.

**Disposal bug in the semaphore design:** `LLamaSharpService.Dispose(bool)` disposes the **static** `_generationSemaphore` from an **instance** dispose (`Services/LLamaSharpService.cs:681`). Both shells construct three `IAIService` instances and dispose them all (`src/ChatDbg/ChatShell.cs:702-707`); the first `LLamaSharpService.Dispose()` kills the process-wide semaphore for every other instance. `_modelLoadSemaphore` is *not* disposed, so the two statics are treated inconsistently.

### 6.5 `CancellationToken` — **completely absent from production code**

A repo-wide grep for `CancellationToken` returns **three** hits, all in tests or a test double:

- `src/Xcaciv.ChatDbg.Core.Tests/Services/BedrockServiceTests.cs:46,70` — `It.IsAny<CancellationToken>()` in a Moq setup, matching the AWS SDK's own signature
- `src/Xcaciv.ChatDbg.Core.Tests/TestDoubles/StubHttpMessageHandler.cs:19` — the required `HttpMessageHandler.SendAsync` override parameter, which is ignored

**No production method accepts, creates, or propagates a `CancellationToken`.** `client.InvokeModelAsync(request)` is called without one (`Services/BedrockService.cs:118`); `_httpClient.PostAsync(url, httpContent)` without one (`Services/AzureOpenAIService.cs:175`); `ChatSession.ChatAsync(message, inferenceParams)` without one (`Services/LLamaSharpService.cs:170-172, 390-392`). There is no `CancellationTokenSource`, no timeout, no way to abort a long local-LLM generation. The `HttpClient` is constructed with no `Timeout` override (`Services/AzureOpenAIService.cs:26`), so the .NET default (100 s) is the only bound.

`ConfigureAwait` is used **nowhere** — zero hits repo-wide.

### 6.6 GUI-specific concurrency

Terminal.Gui event handlers are `async void`:

| Site | Signature |
|---|---|
| `UI/ChatWindow.cs:332` | `private async void OnSendClicked()` |
| `UI/ChatWindow.cs:869` | `private async void ToggleLogProbs()` |
| `UI/ChatWindow.cs:918` | `private async void ExecuteCommand(string commandLine)` |
| `UI/ChatWindow.cs:960, 1061` | `okButton.Clicked += async () => { … await command.ExecuteAsync(…) … }` |

Any exception escaping these becomes an unobserved crash of the process. The one cross-thread UI marshalling site is a fire-and-forget continuation:

```csharp
Task.Delay(3000).ContinueWith(_ => {
    Application.MainLoop.Invoke(() => { _statusLabel.Text = GetStatusText(); });
});                                   // UI/ChatWindow.cs:909-915
```
No `TaskScheduler`, no exception handling, no cancellation if the window closes first.

---

## 7. Architecture Decisions Visible in the Code

### 7.1 Layering: a three-layer split with one enforced abstraction

```
src/ChatDbg (console REPL)      src/ChatDbg.Shell.Gui (Terminal.Gui TUI)
        └───────────────┬───────────────┘
                        ▼
              src/Xcaciv.ChatDbg.Core
        Models · Commands · Services (+ Services/TokenInspection)
                        ▼
      AWS SDK · Azure/OpenAI SDK · LLamaSharp (+ native llama.cpp)
```

Core has **no** `ProjectReference` at all (`src/Xcaciv.ChatDbg.Core/Xcaciv.ChatDbg.Core.csproj` — the file has exactly one `ItemGroup`, packages only). Both shells `ProjectReference` Core and nothing else (`src/ChatDbg/…csproj:114`, `src/ChatDbg.Shell.Gui/…csproj:125`). Dependency direction is clean and one-way.

### 7.2 Dependency injection: **none — no container, manual poor-man's DI**

- Zero hits repo-wide for `IServiceCollection`, `ServiceProvider`, `AddSingleton`, `AddScoped`, `AddTransient`, `HostBuilder`, `IHost`.
- Zero hits repo-wide for `ILogger` or `LoggerFactory` — despite `Microsoft.Extensions.Logging.Abstractions 9.0.8` and `Microsoft.Extensions.DependencyInjection.Abstractions 9.0.8` being in the closure via LLamaSharp **[assets]**.
- Composition happens once, by hand, in each entry point: `src/ChatDbg/ChatShell.cs:23-37` (constructor `new`s five services and three AI providers) and `src/ChatDbg.Shell.Gui/Program.cs:13-55` (same, plus `IConsoleFormatter consoleFormatter = new SpectreConsoleFormatter()` at `:20`, then 14 commands into a `Dictionary<string, ICommand>`).
- Object lifetimes are implicit: everything is effectively a singleton for the process lifetime.

### 7.3 How testability was actually achieved: **optional-constructor seams**

Rather than a container, each hard-to-test dependency got an interface plus a **defaulted, nullable constructor parameter that falls back to the real implementation**:

| Seam | Interface | Production default | Evidence |
|---|---|---|---|
| Azure chat client | `IAzureOpenAIClientFactory` (`Services/IAzureOpenAIClientFactory.cs:6-9`) | `?? new DefaultAzureOpenAIClientFactory()` | `Services/AzureOpenAIService.cs:22,35` |
| Raw HTTP | `HttpClient? httpClient = null` | `new HttpClient()` with `_disposeHttpClient = true` ownership flag | `Services/AzureOpenAIService.cs:22-33` |
| Bedrock client | `IBedrockRuntimeClientFactory` (`Services/IBedrockRuntimeClientFactory.cs:6-8`) | `?? new DefaultBedrockRuntimeClientFactory()` | `Services/BedrockService.cs:16-19` |
| Settings file root | `string? baseDirectory = null, string fileName = "settings.json"` | `%USERPROFILE%\.ChatDbg` | `Services/SettingsService.cs:11-18` |
| Prompt dir root | `string? baseDirectory = null` | `%LOCALAPPDATA%\ChatDbg` | `Services/SystemPromptService.cs:11-21` |
| Rich console output | `IConsoleFormatter` (`Services/IConsoleFormatter.cs:11-51`) | `BasicConsoleFormatter` (dependency-free) in Core; `SpectreConsoleFormatter` in the GUI shell | `Services/BasicConsoleFormatter.cs:12`; `src/ChatDbg.Shell.Gui/Services/SpectreConsoleFormatter.cs:13` |
| Formatter optionality | `DemoLogProbsCommand` has **two constructors** — one taking `IConsoleFormatter`, one taking none and setting `_formatter = null` | `Commands/DemoLogProbsCommand.cs:25-38` |

This is the codebase's defining testability decision, and it is what lets 100 tests run with no container and no integration harness.

### 7.4 How UI was decoupled from Core: `IConsoleFormatter`

`Services/IConsoleFormatter.cs` is documented in-source as *"an abstraction layer to remove UI dependencies from Core project"* (`:9`). It defines six members: `WriteMarkupLine`, `WriteLine(string)`, `WriteLine()`, `WriteRule(title, leftJustified)`, `DisplayTokenGrid(tokens, startIndex, maxColumns, maxAlternatives)`, `DisplayTokenTable(tokens, startIndex)` (`:17-51`).

Two implementations:
- `BasicConsoleFormatter` (Core) — strips markup and writes plain text; `WriteRule` draws an 80-char dash rule (`Services/BasicConsoleFormatter.cs:45,49,59,65`); `DisplayTokenGrid` just delegates to `DisplayTokenTable` because *"true grid layout requires more sophisticated console handling"* (`:74-76`).
- `SpectreConsoleFormatter` (GUI shell) — the real renderer (`Services/SpectreConsoleFormatter.cs`).

**The abstraction is leaky in two directions:**
1. The interface's contract is *Spectre markup strings* (`WriteMarkupLine(string text)` with `[blue]…[/]` payloads); `BasicConsoleFormatter` copes by regex-stripping them. The abstraction encodes Spectre's syntax as its wire format.
2. It is **incompletely adopted.** Core still calls `Console.*` directly in 9 files — most heavily `Commands/InspectCommand.cs` (38 call sites), `Services/SettingsService.cs` (45 call sites, including interactive `Console.ReadLine()` prompts at `:121, 211, 247`), `Commands/TokenizeCommand.cs` (10), `Commands/PromptCommand.cs` (7), `Services/BasicConsoleFormatter.cs` (11), `Services/ChatHistoryService.cs` (3), `Services/SystemPromptService.cs` (2), `Services/LLamaSharpService.cs` (1, at `:557`), `Services/TokenInspection/LLamaSharpLogConfig.cs` (2).

**Consequence:** `SettingsService.EnableWindowsCredentialManagerAsync` and `MigrateCredentialsFromJsonAsync` run an interactive `Console.ReadLine()` dialogue from inside Core (`Services/SettingsService.cs:120-121, 210-211, 246-247`). Under Terminal.Gui, which owns the terminal, this is a **hang or a corrupted screen**. Core is not actually headless.

### 7.5 The command pattern

`ICommand { Name, Description, Usage, Task<CommandResult> ExecuteAsync(string[] args) }` (`Models/ICommand.cs:3-9`) with a 3-state `CommandResult { Success, Message, ExitRequested }` + static factories `SuccessResult` / `ErrorResult` / `ExitResult` (`Models/CommandResult.cs`). 18 command classes live in `Core/Commands/`. Dispatch is a `Dictionary<string, ICommand>` keyed on `Name`, with `HelpCommand` registered **last** and handed the dictionary itself so it can enumerate its siblings (`src/ChatDbg/ChatShell.cs:66`; `src/ChatDbg.Shell.Gui/Program.cs:55`; `Commands/HelpCommand.cs:19-22`).

**14 of the 18 commands are registered.** Both shells register the same list: `inject, pop, import, export, model, set, logprobs, prompt, demologprobs, clear, exit, quit, tokenize, inspect` + `help` (`src/ChatDbg/ChatShell.cs:42-66`; `src/ChatDbg.Shell.Gui/Program.cs:31-55`). **`ExportLogsCommand`, `ExportTokenAnalysisCommand`, and `ShowTokenAnalysisCommand` are never registered by either shell** — they exist only as classes and are reachable only from `Commands/ExportLogsAndAnalysisCommandTests.cs` and `Commands/ShowTokenAnalysisCommandTests.cs`. All three are stubs that return the literal string *"Note: This command requires LLamaSharp provider integration."*

### 7.6 What the two-shell split implies

| | `src/ChatDbg` (`Xcaciv.ChatDbg.Shell`) | `src/ChatDbg.Shell.Gui` (`Xcaciv.ChatDbg.Shell.Gui`) |
|---|---|---|
| Entry point | 14-line top-level `Program.cs` → `new ChatShell(); await RunAsync()` | 103-line top-level `Program.cs` that **is** the composition root |
| Loop | `while(true) { Console.Write(prompt); Console.ReadLine(); … }` (`ChatShell.cs:80-117`) | `Application.Init(); ThemeManager.ApplyDarkTheme(); Application.Top.Add(new ChatWindow(…)); Application.Run();` in try/finally with `Application.Shutdown()` (`Program.cs:71-95`) |
| AI providers wired | 3 — `azure`, `bedrock`, `llama` (`ChatShell.cs:29-34`) | 3 — same (`Program.cs:22-27`) |
| `IConsoleFormatter` | not injected; `DemoLogProbsCommand(_settings)` uses the no-formatter ctor (`ChatShell.cs:52`) | `SpectreConsoleFormatter` injected into `DemoLogProbsCommand` **and** into `ChatWindow` (`Program.cs:20,41,87`) |
| Service disposal | `ChatShell.Dispose(bool)` iterates `_aiServices.Values` and disposes each (`ChatShell.cs:698-713`), with a finalizer at `:715` | **None.** `Program.cs` never disposes `aiServices`; `grep -n Dispose src/ChatDbg.Shell.Gui/Program.cs src/ChatDbg.Shell.Gui/UI/ChatWindow.cs` → zero hits. The native LLamaSharp model/context leak until process exit. |

**Duplication is the cost of the split.** `src/ChatDbg.Shell.Gui/ChatShell.cs` is a **671-line near-clone of the console `ChatShell.cs`** (718 lines) — same fields, same `InitializeCommands`, same `Dispose` pattern, but registering only `azure` + `bedrock` (`:33-37`, no `llama`). **It is dead code:** `grep -rn "ChatShell" src/ChatDbg.Shell.Gui` finds only its own declaration (`:14`), constructor (`:25`), and finalizer (`:668`) — nothing ever instantiates it, because `Program.cs` uses `ChatWindow` instead. It still compiles into the GUI binary and still drags `using Spectre.Console` (`:5`) and `using Terminal.Gui` (`:9`) with it.

The GUI shell also duplicates the console-width formatting logic: `Console.WindowWidth` is read at `src/ChatDbg/ChatShell.cs:502`, `src/ChatDbg.Shell.Gui/ChatShell.cs:455` (dead), `Services/SpectreConsoleFormatter.cs:58`, and `Services/TokenProbabilityVisualizer.cs:109`.

### 7.7 Where abstractions exist vs. where they are missing

| Concern | Abstraction? |
|---|---|
| AI provider | ✅ `IAIService` (`Services/IAIService.cs`) with 3 implementations, keyed by `settings.Provider` string |
| Azure client construction | ✅ `IAzureOpenAIClientFactory` |
| Bedrock client construction | ✅ `IBedrockRuntimeClientFactory` |
| Settings persistence | ✅ `ISettingsService` |
| Chat history persistence | ✅ `IChatHistoryService` (declared **inside** `Services/ChatHistoryService.cs:6-10`, not its own file) |
| System prompts | ✅ `ISystemPromptService` |
| Rich console output | ⚠️ `IConsoleFormatter` — exists, but bypassed by 119 direct `Console.*` calls in Core |
| Commands | ✅ `ICommand` |
| **Filesystem** | ❌ none — `File.*` / `Directory.*` called directly in `SettingsService`, `ChatHistoryService`, `SystemPromptService`, `LLamaSharpService`, `LLamaSharpLogConfig`, `ExportCommand`, `ImportCommand`, `PromptCommand`, `TokenizeCommand`, `InspectCommand`. Tests compensate with real temp dirs. |
| **Clock** | ❌ none — `DateTime.UtcNow` (`Models/ChatHistory.cs:12,21,40`, `Models/ChatMessage.cs:12`, `Models/SystemPrompt.cs:14`, `Services/SystemPromptService.cs:150,175,182,189,196`) and `DateTime.Now` (`Services/TokenInspection/LLamaSharpLogConfig.cs:83,121,149`, `Services/LLamaSharpService.cs:293`) called directly |
| **Logging** | ❌ no `ILogger`. Two ad-hoc channels: `Debug.WriteLine` (6 files) and the bespoke `LLamaSharpLogConfig` (`StringBuilder` + `lock` + `File.AppendAllText`) |
| **Credential store** | ❌ `WindowsCredentialManager` is a `public static class` with `[DllImport]`s (`Models/WindowsCredentialManager.cs:9`) — not injectable, not mockable, and called *statically* from inside the `ChatSettings` **model** (`Models/ChatSettings.cs:135`) |
| **Environment variables** | ❌ `Environment.GetEnvironmentVariable` called directly from the `ChatSettings` model (`Models/ChatSettings.cs:93,178`) and `BedrockService` (`Services/BedrockService.cs:27`) |
| **DI container** | ❌ none |
| **Cancellation** | ❌ none |

**The biggest structural smell:** `ChatSettings` is nominally a serialization DTO but performs I/O — it reads environment variables and P/Invokes into `advapi32.dll` from property getters (`Models/ChatSettings.cs:70,73,76 → 88-118 → 120-142`). Reading `settings.AzureApiKey` is a side-effecting call, not a field access.

---

## 8. Build and Packaging Decisions

### 8.1 The two custom configurations — declared **identically in both shell csproj files**

`src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:23-97` and `src/ChatDbg.Shell.Gui/Xcaciv.ChatDbg.Shell.Gui.csproj:23-97` are **byte-for-byte the same 75 lines**, copy-pasted. `Xcaciv.ChatDbg.Core` and the test project have **no** custom configurations.

#### `Configuration == 'Compact'` (AOT)

| Property | Value | Line (both files) |
|---|---|---|
| `PublishAot` | `true` | 25 |
| `PublishTrimmed` | `true` | 26 |
| `SelfContained` | `true` | 27 |
| `RuntimeIdentifier` | `win-x64` (only if not already set) | 30 |
| `UseAppHost` | `true` | 31 |
| `TrimMode` | **`full`** | 34 |
| `SuppressTrimAnalysisWarnings` | **`true`** | 35 |
| `DebugType` | `none` | 38 |
| `GenerateRuntimeConfigurationFiles` | `false` | 39 |
| `SatelliteResourceLanguages` | `en` | 42 |
| `Optimize` | `true` | 45 |
| `GenerateAssemblyInfo` | `false` | 48 |
| `GenerateTargetFrameworkAttribute` | `false` | 49 |
| `InvariantGlobalization` | `true` | 52 |
| `UseSystemResourceKeys` | `true` | 53 |
| `IlcOptimizationPreference` | `Size` | 56 |
| `IlcFoldIdenticalMethodBodies` | `true` | 57 |
| `IlcGenerateStackTraceData` | `false` | 58 |
| `StripSymbols` | `true` | 59 |

#### `Configuration == 'SingleFile'` (JIT)

| Property | Value | Line (both files) |
|---|---|---|
| `PublishSingleFile` | `true` | 65 |
| `PublishTrimmed` | `true` | 66 |
| `SelfContained` | `true` | 67 |
| `RuntimeIdentifier` | `win-x64` (only if not already set) | 70 |
| `UseAppHost` | `true` | 71 |
| `TrimMode` | **`partial`** | 74 |
| `SuppressTrimAnalysisWarnings` | `true` | 75 |
| `DebugType` | `none` | 78 |
| `GenerateRuntimeConfigurationFiles` | `false` | 79 |
| `SatelliteResourceLanguages` | `en` | 82 |
| `Optimize` | `true` | 85 |
| `EnableCompressionInSingleFile` | `true` | 86 |
| `IncludeNativeLibrariesForSelfExtract` | `true` | 87 |
| `IncludeAllContentForSelfExtract` | `true` | 88 |
| `GenerateAssemblyInfo` | `true` | 91 |
| `GenerateTargetFrameworkAttribute` | `true` | 92 |
| `InvariantGlobalization` | `true` | 95 |
| `UseSystemResourceKeys` | `true` | 96 |

### 8.2 Compile-item surgery

`src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:98-105` removes `Serialization\**` and `Services\**` from `Compile`/`EmbeddedResource`/`None`. **Neither directory exists** in that project (its only files are `Program.cs`, `ChatShell.cs`, `prd.md`) — vestigial.

`src/ChatDbg.Shell.Gui/Xcaciv.ChatDbg.Shell.Gui.csproj:98-115` removes `Commands\**`, `Models\**`, `Serialization\**`, `Services\**`, then **re-adds exactly two files**:
```xml
<Compile Include="Services\SpectreConsoleFormatter.cs" />
<Compile Include="Services\TokenProbabilityVisualizer.cs" />
```
Those are the only two files in `Services/`, so today this is a no-op — but it is a **fragile allow-list**: any new file added to `src/ChatDbg.Shell.Gui/Services/` is silently excluded from compilation. Note `UI/**` is *not* excluded, so `ChatWindow.cs`, `SettingsDialog.cs`, `SystemPromptsDialog.cs`, `ThemeManager.cs`, `LogProbHeatmapView.cs` all compile normally, as does the dead root-level `ChatShell.cs`.

### 8.3 Contradictions in the packaging story

| # | Contradiction | Evidence |
|---|---|---|
| **B1** | **`PublishAot` vs. a package tree full of native, RID-specific, non-AOT-analysable payloads.** AOT requires a single RID and static linking of managed code; `LLamaSharp.Backend.Cpu/Cuda12` deliver ~630 MB of loose native `.dll`/`.so`/`.dylib`/`.metal` across 6 RIDs that llama.cpp loads at runtime by probing `runtimes/<rid>/native/<isa>/`. These are not AOT artefacts and are not linked in. | csproj `:25`; assets §3.1–3.2 |
| **B2** | **`PublishAot` + `TrimMode=full` vs. reflection in `LLamaSharpService`.** `ComputeTopKFromCurrentLogits` (`Services/LLamaSharpService.cs:419`) does `context.GetType().GetMethod("GetLogits", Type.EmptyTypes)` then `mi.Invoke(context, null)` (`:423,429`). `ResolveTokenStringSafe` (`:495`) uses **`(dynamic)context` → `dyn.TokenToString(tokenId)`** (`:499-500`), which pulls in the entire C# runtime binder. Full trimming will remove `LLamaContext.GetLogits`/`TokenToString`, and the `catch { }` at `:489-492` / `:506-508` will silently swallow the failure — token probabilities degrade to `id:{n}` strings with **no error**. | `Services/LLamaSharpService.cs:423,429,499-500,489,506` |
| **B3** | **`PublishTrimmed` vs. reflection-based `System.Text.Json` on anonymous types.** No `JsonSerializerContext` exists. `AzureOpenAIService` serialises `new { messages, temperature, max_tokens, top_p, logprobs, top_logprobs }` (`:151-159, 165`) and `BedrockService` serialises `new { anthropic_version, max_tokens, temperature, system, messages, logprobs, top_logprobs }` (`:68-78, 95-103, 107, 115`). All are anonymous types serialised by reflection — precisely what trimming breaks. | `Services/AzureOpenAIService.cs:151-169`; `Services/BedrockService.cs:68-115` |
| **B4** | **`SuppressTrimAnalysisWarnings=true` in *both* configurations** (`:35`, `:75`) — the compiler is explicitly told to hide the IL2xxx/IL3xxx warnings that would have flagged B2 and B3. The failure mode is therefore a *silent runtime* failure, not a build failure. | csproj `:35,75` |
| **B5** | **`InvariantGlobalization=true`** (`:52`, `:95`) vs. code that calls `ToLowerInvariant()` heavily (fine) *but also* `double.TryParse(tempValue, out var temp)` and `int.TryParse(...)` with **no `CultureInfo`** (`Commands/SetCommand.cs:72,81`) and `settings.Temperature.ToString("F2")` (`Services/LLamaSharpService.cs:286,296`). Under invariant globalization the behaviour is consistent, but it *changes* relative to a normal build on a comma-decimal locale — settings files written by one build may not parse in the other. | csproj `:52,95`; `Commands/SetCommand.cs:72,81` |
| **B6** | **`IlcGenerateStackTraceData=false` + `StripSymbols=true` + `DebugType=none`** (`:58,59,38`) vs. an application whose primary error-reporting mechanism is `ex.Message` inside broad `catch (Exception ex)` blocks (30+ sites) and `Debug.WriteLine($"...{ex}")`. Stack traces are the missing half of every one of those diagnostics. | csproj `:38,58,59` |
| **B7** | **AOT vs. `Terminal.Gui 1.19.0` → `System.Management 9.0.4` → `System.CodeDom 9.0.4`.** WMI + runtime CodeDom generation is among the least AOT-compatible parts of the BCL, and `System.Management` ships a Windows-only RID asset. The `Compact` configuration is nonetheless declared identically in the GUI csproj. | GUI csproj `:23-60,121`; assets §2.4 |
| **B8** | **`Compact` and `SingleFile` are not solution configurations.** `Xcaciv.ChatDbg.sln:25-26` declares only `Debug`/`Release`. Building them requires `dotnet publish <csproj> -c Compact`, which is exactly what all four build scripts and the CI workflow do — but `dotnet build Xcaciv.ChatDbg.sln -c Compact` will not map. | `.sln:24-45` |
| **B9** | **Build scripts and docs point at `net9.0` output paths while every project targets `net10.0`.** `build-compact.bat:20,22`; `build-singlefile.bat:21,25`; `build-compact.ps1:23,27`; `build-compact-robust.bat:57,58,80,81`; `docs/compact-build.md:97,98,132`. The `dir`/`Get-ChildItem` step after a successful publish therefore looks in a directory that does not exist and reports nothing. | listed lines |
| **B10** | **All build automation is Windows-only.** Three `.bat` + one `.ps1`, all `cd /d "%~dp0"` / `Set-Location $PSScriptRoot`, all hardcoding `-r win-x64`, all ending in `pause` / `$Host.UI.RawUI.ReadKey`. `build-compact-robust.bat` is an interactive `set /p choice=` menu. There is no `.sh`, no `Makefile`, no `Directory.Build.targets`. Yet `docs/compact-build.md:104-116` advertises `linux-x64`, `osx-x64`, `osx-arm64` as supported. | `build-*.bat`, `build-compact.ps1` |
| **B11** | **`docs/compact-build.md:170` states "**.NET 9 SDK** installed" as a prerequisite**, and `:144-146` even prescribes the fixes for the exact AOT hazards the code contains (*"Reflection warnings: Add `[DynamicallyAccessedMembers]`"*, *"Serialization issues: Use source generators for System.Text.Json"*) — **none of which were applied**. | `docs/compact-build.md:144-146,170` |

---

## 9. Platform Coupling: every Windows assumption, named

| # | Assumption | API / mechanism | File:line | Behaviour on Linux/macOS |
|---|---|---|---|---|
| **P1** | **Windows Credential Manager P/Invoke** | `[DllImport("advapi32.dll", EntryPoint="CredReadW", CharSet=Unicode, SetLastError=true)]` → `CredRead`; `CredWriteW` → `CredWrite`; `CredDeleteW` → `CredDelete`; `CredFree` | `Models/WindowsCredentialManager.cs:11,14,17,20` | **Guarded.** Every public method opens with `if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null/false;` (`:59-62, 103-106, 150-153`) and `IsAvailable()` is just that check (`:169-172`). No `DllNotFoundException` is ever thrown — the feature simply degrades to unavailable. |
| **P2** | `CREDENTIAL` struct marshalling | `[StructLayout(LayoutKind.Sequential, CharSet=Unicode)]` with `System.Runtime.InteropServices.ComTypes.FILETIME`, `Marshal.StringToHGlobalUni`, `Marshal.AllocHGlobal`, `Marshal.PtrToStructure<CREDENTIAL>`, `Marshal.Copy`, `Marshal.FreeHGlobal`, `Encoding.Unicode` for the blob | `Models/WindowsCredentialManager.cs:35-50, 73-81, 110-135` | Unreachable off-Windows. `Persist = 2 /* CRED_PERSIST_LOCAL_MACHINE */` (`:120`) is a Windows-only persistence semantic with no cross-platform analogue. |
| **P3** | Credential lookup called from the settings **model** | `WindowsCredentialManager.GetCredential(targetName)` where `targetName ∈ {"ChatDbg:AzureApiKey","ChatDbg:AwsAccessKey","ChatDbg:AwsSecretKey"}` | `Models/ChatSettings.cs:124-135`; write side `Services/SettingsService.cs:157-172, 295, 304, 313` | Returns `null`; credential resolution falls through to Priority 3 (plaintext JSON). |
| **P4** | Startup message asserting platform | `"Windows Credential Manager is enabled in settings but not available on this platform."` | `Services/SettingsService.cs:73` | Prints on every load if `useWindowsCredentialManager: true` was ever persisted. |
| **P5** | `SpecialFolder.LocalApplicationData` vs. `SpecialFolder.ApplicationData` used as *different* roots | `Environment.GetFolderPath` | `Services/SystemPromptService.cs:15` vs. `Services/TokenInspection/LLamaSharpLogConfig.cs:20` | On Windows these are `AppData\Local` and `AppData\Roaming` — a meaningful distinction. On Linux they are `~/.local/share` and `~/.config`; on macOS both map under `~/Library`. The Roaming/Local split the design assumes does not exist. |
| **P6** | `SpecialFolder.MyDocuments` as a default export target | `Environment.GetFolderPath(SpecialFolder.MyDocuments)` | `Commands/PromptCommand.cs:313` | On Linux this frequently resolves to the **home directory itself** (or `""` if `XDG_DOCUMENTS_DIR` is unset), so `/prompt export foo` writes `~/chatdbg_prompt_foo.txt` instead of into a Documents folder. |
| **P7** | `SpecialFolder.UserProfile` for `~/` expansion and file dialogs | `Environment.GetFolderPath(SpecialFolder.UserProfile)` | `Commands/ExportCommand.cs:33`; `Commands/ImportCommand.cs:33`; `Services/SettingsService.cs:16`; `UI/ChatWindow.cs:1008,1023`; `UI/SystemPromptsDialog.cs:646` | Works everywhere (`$HOME`). The `~/` handling is a **manual prefix check** (`filePath.StartsWith("~/")`), so `~` alone, `~user/`, and Windows-style `~\` are not expanded. |
| **P8** | `Console.WindowWidth` read unconditionally for layout | `Console.WindowWidth` | `src/ChatDbg/ChatShell.cs:502`; `src/ChatDbg.Shell.Gui/ChatShell.cs:455` (dead); `Services/SpectreConsoleFormatter.cs:58`; `Services/TokenProbabilityVisualizer.cs:109` | **Throws `IOException` when stdout is redirected or there is no TTY** (piped output, CI, `nohup`). Not guarded anywhere; only `SpectreConsoleFormatter.cs:58` even defends the *value* (`Math.Max(1, …/40)`), not the call. |
| **P9** | Transitive Windows-only runtime asset in the GUI app | `System.Management 9.0.4` → `runtimes/win/lib/net9.0/System.Management.dll`, plus `System.CodeDom 9.0.4`, pulled by `Terminal.Gui 1.19.0` | GUI csproj `:121`; assets §2.4; **[build output]** `src/ChatDbg.Shell.Gui/bin/Debug/net10.0/runtimes/win/` | On non-Windows the RID asset is simply not selected; whether `Terminal.Gui` needs it at runtime on Windows only is a library-internal detail the app has no control over. |
| **P10** | Windows-only build automation | `.bat` (`cmd`), `.ps1` (`$Host.UI.RawUI`), `rd /s /q`, `pause`, `dir /B`, `set /p` | `build-compact.bat`, `build-singlefile.bat`, `build-compact-robust.bat`, `build-compact.ps1` | Cannot be run on Linux/macOS at all. |
| **P11** | Copilot instructions declare the terminal | *"Your terminal is PowerShell, only use powershell syntax."* | `.github/copilot-instructions.md:8` | Project-level assumption of a Windows dev environment. |
| **P12** | README framing | *"A C# Chat shell **for Windows terminal**…"* | `README.md:3` | The stated target platform is Windows, even though the CI ships a Linux binary and the csproj description says *"Cross-platform chat debugging tool"* (`src/ChatDbg/…csproj:16`). |
| **P13** | **Source-file encoding damage** | `src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs` is *"Non-ISO extended-ASCII text"* (`file`); `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs` is pure ASCII with emoji already collapsed to `?` — e.g. `"??  WARNING: Credentials found…"` (`:62`), `"?? Windows Credential Manager integration is enabled…"` (`:69`), `"? Credential stored securely…"` (`:180`) | `Services/SettingsService.cs:62,69,73,110,116,128-130,149,167,180,185,205,257,297,304,315,324`; `Commands/SetCommand.cs:342` | The user-facing strings are already corrupted **in the committed source**. A port must not faithfully reproduce `?`/`??` prefixes — they were intended as status glyphs. Contrast `src/ChatDbg/ChatShell.cs` (valid UTF-8) which prints real `✓`/`✗` (`:103-104`). |
| **P14** | Three `.csproj` files carry a **UTF-8 BOM** (`efbbbf`), the test `.csproj` does not | `head -c 3` on each | Core, Console Shell, GUI Shell csproj | Cosmetic, but indicates the files were edited by different tools. |

**Net portability verdict:** the *only* hard Windows dependency is the credential store (P1–P3), and it is correctly feature-detected. Everything else that breaks off-Windows breaks **quietly** — wrong directories (P5, P6), a crash on redirected stdout (P8), corrupted glyphs (P13) — or breaks the *developer* workflow rather than the app (P10, P11).

---

## 10. CI/CD

Single workflow: `.github/workflows/build-release.yml` (196 lines). No other workflow, no `dependabot.yml`, no CodeQL, no `nuget.config`.

### 10.1 What it actually does

| Aspect | Value | Line |
|---|---|---|
| Trigger | **`workflow_dispatch` only** — manual, with inputs `version` (default `v1.0.0`) and `prerelease` (boolean) | `:3-14` |
| Trigger on push/PR/tag | **none** — there is **no CI on commits or pull requests at all** | `:3` |
| Job 1 matrix | `windows` / `windows-latest` / `win-x64` / `.exe`; `linux` / `ubuntu-latest` / `linux-x64` / `''` | `:20-30` |
| macOS | **not built** | `:22-30` |
| Checkout | `actions/checkout@v4` | `:34` |
| SDK setup | `actions/setup-dotnet@v4` with **`dotnet-version: '9.0.x'`** | `:36-39` |
| Restore | `dotnet restore src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj -r ${{matrix.runtime}}` | `:42` |
| Publish | `dotnet publish src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj -c SingleFile -r <rid> --self-contained --no-restore -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o ./publish/<os>` — duplicated as a PowerShell block (`:47-54`) and a bash block (`:59-66`) | `:44-66` |
| **Projects built** | **`src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj` only** — the GUI shell is never built or released | `:42,47,59` |
| **Tests** | **never run** — there is no `dotnet test` step anywhere in the file | — |
| Rename | `mv Xcaciv.ChatDbg.Shell[.exe] → chatdbg-<os>-<rid>[.exe]` | `:84-88` |
| Size capture | `stat -c%s` / `wc -c` → `echo "file_size=$SIZE" >> $GITHUB_OUTPUT`, but the step's `id: file_info` is declared **after** the `run:` block and no later step references `steps.file_info.outputs.file_size` | `:95-102` |
| Artifact | `actions/upload-artifact@v4`, `retention-days: 7` | `:104-109` |
| Job 2 | `release` (needs `build`, `runs-on: ubuntu-latest`): downloads artifacts, writes `release_notes.md` heredoc, `softprops/action-gh-release@v1` with `token: ${{ secrets.GH_PATT }}` (a **custom PAT secret**, not `GITHUB_TOKEN`), `draft: false` | `:111-182` |
| Release notes claim | *"Built with .NET 9 using SingleFile publishing for optimal deployment."* | `:168` |

### 10.2 SDK mismatch — **workflow `9.0.x` vs. `global.json` `10.0.100-rc.1.25451.107`**

| Source | Declares |
|---|---|
| `global.json:3` | `"version": "10.0.100-rc.1.25451.107"` |
| `global.json:4` | `"rollForward": "latestFeature"` — matches `10.0.1xx` and above within major 10; **will not roll back to 9.x** |
| `.github/workflows/build-release.yml:38-39` | `dotnet-version: '9.0.x'` |
| `.github/workflows/build-release.yml:36` | step name: `Setup .NET 9` |
| Every `.csproj` | `<TargetFramework>net10.0</TargetFramework>` |

**This is an unresolvable mismatch as written.** A runner given only .NET 9 cannot satisfy a `global.json` pinning 10.0.100-rc with `latestFeature`, and even if `global.json` were ignored, a 9.0.x SDK cannot build `net10.0`. GitHub-hosted `windows-latest`/`ubuntu-latest` images happen to ship additional SDKs preinstalled, which is the only reason this workflow could ever have produced the tracked `test-publish/Xcaciv.ChatDbg.Shell` binary — but the workflow does not *pin* what it actually needs. Compounding it, `global.json` is **syntactically invalid** (§1.4), which the SDK may reject outright.

Supporting documentation repeats the .NET 9 claim: `docs/github-actions-release.md:57` (*"Installs .NET 9 SDK with preview support"*), `:124`, `docs/release-setup-complete.md:132`, `docs/compact-build.md:170`, `build-singlefile.bat:37`, `.github/copilot-instructions.md:138,240`.

### 10.3 Other CI gaps

- **The `Compact` (AOT) configuration is never exercised by CI** — only `SingleFile` (`:48,60`). Contradictions B1–B7 have therefore never been tested.
- **No test execution**, so the 100 xUnit facts and `coverlet.collector 6.0.2` are never run in automation.
- **Only one of the two shells is shipped.** `README.md:3,8,23-37` describes the Terminal.Gui experience as *the* product, but the released binary is the console REPL.
- Release notes (`:150-157`) advertise *"Interactive console interface with Spectre.Console"* and *"Support for AWS Bedrock and Azure OpenAI"* — with **no mention of the LLamaSharp local-LLM provider**, consistent with the ~15 MB binary size (§3.4) that cannot contain it.

---

## 11. Decisions That Will Not Survive a Port

Each row: the observed choice → whether it is load-bearing (i.e. changes observable behaviour or contracts) → what a reimplementer must preserve *semantically*.

| # | Observed decision | Load-bearing? | What must be preserved semantically |
|---|---|---|---|
| **D1** | **Windows Credential Manager via `advapi32.dll` P/Invoke** with `CRED_TYPE.GENERIC`, `Persist = CRED_PERSIST_LOCAL_MACHINE`, UTF-16LE blob, username `"ChatDbg"`, targets `ChatDbg:AzureApiKey` / `ChatDbg:AwsAccessKey` / `ChatDbg:AwsSecretKey` (`Models/WindowsCredentialManager.cs:11-21,101-141`) | **Yes — the API surface and the fallback chain; No — the specific OS API** | The *contract*: an OS-provided secret store, **feature-detected at runtime** (`IsAvailable()`), that degrades silently to the next tier rather than erroring. Preserve the three stable target names and the "returns null/false when unavailable" semantics. Substitute libsecret / Keychain / DPAPI-equivalent freely. |
| **D2** | **Three-tier credential resolution: env var → OS credential store (only if opted in) → plaintext JSON (deprecated)** (`Models/ChatSettings.cs:88-118`) with per-key env var lists — `CHATDBG_AZURE_API_KEY`; `CHATDBG_AWS_ACCESS_KEY` then `AWS_ACCESS_KEY_ID`; `CHATDBG_AWS_SECRET_KEY` then `AWS_SECRET_ACCESS_KEY` (`:70,73,76`) | **Yes — critically** | Exact precedence order, exact env-var names **including the AWS-standard fallbacks**, the opt-in gate `useWindowsCredentialManager` (`:101`), and `GetCredentialSource()`'s four return strings — `"environment variable (X)"`, `"Windows Credential Manager"`, `"settings file (deprecated)"`, `"not set"` (`:180,190,197,200`) — which the UI displays verbatim. |
| **D3** | **Credential getters are side-effecting property reads on a serialization DTO** (`Models/ChatSettings.cs:70-76`) | No — an accident of design | Preserve *what* is resolved, not *when*. A port should resolve credentials once, explicitly, at a boundary — and must then re-check that nothing depended on late/lazy resolution. |
| **D4** | **Three distinct on-disk roots**: `~/.ChatDbg/settings.json`, `%LOCALAPPDATA%/ChatDbg/system_prompts/*.json`, `%APPDATA%/ChatDbg/Logs/llamasharp_yyyyMMdd.log` (`Services/SettingsService.cs:15-18`, `Services/SystemPromptService.cs:14-23`, `Services/TokenInspection/LLamaSharpLogConfig.cs:20,149`) | **Yes for settings** (users have existing files); **No for the split** | Keep `~/.ChatDbg/settings.json` as the settings location for migration. The Local-vs-Roaming split is a Windows artefact with no meaning elsewhere — consolidate deliberately and document the move. Preserve one-file-per-prompt with `Path.GetInvalidFileNameChars()`-style sanitisation (`Services/SystemPromptService.cs:212`) and the daily-rotated log filename. |
| **D5** | **Constructors that mutate the disk**: `SettingsService.LoadSettingsAsync` writes a default file when none exists (`:47-53`); `SystemPromptService`'s ctor creates the directory and writes 4 named default prompts — `default`, `code-reviewer`, `algorithm-helper`, `security-expert` (`:26-29,168-203`) | **Yes** | First-run must materialise those four prompts with those exact names and content, and a missing settings file must be created with defaults rather than erroring. Move it out of a constructor. |
| **D6** | **JSON wire format with explicit `[JsonPropertyName]` on every persisted field, mixed camelCase + one snake_case key (`top_alternatives`), `logprob` for `LogProb`, ISO-8601 `DateTime`, and computed fields marked `[JsonIgnore]`** (`Models/*.cs`) | **Yes — this is the interop contract** | Byte-compatible field names for `settings.json`, exported chat-history files, and `system_prompts/*.json`. Note the asymmetry: `azureApiKey`/`awsAccessKey`/`awsSecretKey` are *written* from the deprecated backing fields while the same-named C# properties are `[JsonIgnore]` computed. `WriteIndented = true` matters because users hand-edit these files. |
| **D7** | **`Provider` is a magic string** — `"azure" \| "bedrock" \| "llama"` — used as a `Dictionary<string, IAIService>` key, persisted to JSON, and validated by string comparison in `/set provider` (`Models/ChatSettings.cs:8`; `src/ChatDbg/ChatShell.cs:29-34`; `Commands/SetCommand.cs:47-52`; `Commands/InspectCommand.cs:35`) | **Yes — the three literals** | The three literals are on disk and in user muscle memory. A port should use an enum internally but must serialise and accept exactly these three strings. |
| **D8** | **`ModelId` is overloaded**: an Azure deployment name, a Bedrock model ID (`anthropic.*` triggers a different request shape at `Services/BedrockService.cs:61`), **or a filesystem path to a `.gguf` file** for the `llama` provider — validated with `File.Exists` (`Services/LLamaSharpService.cs:48-49`; `Commands/SetCommand.cs:59-65`) | **Yes** | One field, three meanings, provider-dependent validation. A port that splits this into three fields breaks every existing `settings.json`. |
| **D9** | **Azure log-probabilities bypass the SDK entirely.** When `EnableLogProbabilities` is true the code abandons `AzureOpenAIClient` and hand-builds a REST POST to `{endpoint}/openai/deployments/{modelId}/chat/completions?api-version=2023-12-01-preview` with an `api-key` header and `{logprobs: true, top_logprobs: N, top_p: 1.0}` (`Services/AzureOpenAIService.cs:120-175`) | **Yes** | The hardcoded API version **`2023-12-01-preview`** (`:120`), the `api-key` header auth (not `Authorization: Bearer`), the URL shape, and the dual-path behaviour (SDK when logprobs off, raw REST when on). The response is parsed with `JsonDocument` looking for **both** `choices[].logprobs.content[]` and a top-level `logprobs[]` array (`:227,288,337`) — two tolerated shapes. |
| **D10** | **Fabricated telemetry when the provider returns none.** If a real response carries no logprobs, `GenerateSimulatedLogProbs(text, topK)` **invents** them and returns them as if real (`Services/AzureOpenAIService.cs:202-207,413-437`). Likewise `LLamaSharpService.EstimateProbabilityFromTemperature` returns a hardcoded step function — 0.95/0.85/0.75/0.60/0.50/0.40 keyed on temperature bands (`Services/LLamaSharpService.cs:310-324`) — and `TokenAnalysis.TokenId` is always `-1` (`:281`) | **Behaviourally yes; scientifically no** | This is a **decision a port should consciously break**. Document it as observed behaviour, then either drop it or label simulated data explicitly in the UI. Silently presenting invented probabilities as model output is the single most misleading behaviour in the codebase. |
| **D11** | **Local-LLM token probabilities obtained by reflection and `dynamic`**: `context.GetType().GetMethod("GetLogits", Type.EmptyTypes)` + `mi.Invoke` (`:423,429`), softmax over Top-K (`:465-485`), and `(dynamic)context → dyn.TokenToString(id)` with `catch {}` → `$"id:{tokenId}"` (`:495-511`) | **Semantics yes; mechanism no** | Preserve *the observable output*: Top-K alternatives with softmax-normalised probabilities, `logProb = ln(p)`, and a graceful `id:{n}` placeholder when the token string cannot be decoded. In a port with real API access, call the logits API directly — the reflection was a workaround for an API the author could not reach, and it is exactly what `PublishAot` + `TrimMode=full` would destroy (B2). |
| **D12** | **Off-by-one alternative attribution**: alternatives computed from the logits *after* emitting token *N* are attached to token *N* (the previously emitted one) via `pendingAlternatives` / `lastAdded` (`Services/LLamaSharpService.cs:166-167,188-206,235-243`) | **Yes — it is the observed output** | Reproduce or fix, but know it is there. A port that "correctly" aligns alternatives will produce different numbers than the original for the same prompt and seed. |
| **D13** | **Process-wide static generation lock**: `static readonly SemaphoreSlim _generationSemaphore`/`_modelLoadSemaphore` (`Services/LLamaSharpService.cs:23-24,72,113,518,621`) | **Yes** | llama.cpp contexts are not safe for concurrent use. Preserve "at most one generation in flight per process" and "model loads are serialised". Do **not** preserve the bug of disposing a static semaphore from instance `Dispose` (`:681`). |
| **D14** | **No cancellation anywhere** (§6.5) | **No — a defect** | A port **must add** cancellation. A local-LLM generation with `MaxTokens` up to 8192 (`Commands/SetCommand.cs:81`) and no `CancellationToken` is an unabortable UI freeze. |
| **D15** | **`~/` expansion is a manual `StartsWith("~/")` prefix check** (`Commands/ExportCommand.cs:31-33`, `Commands/ImportCommand.cs:33`), and `.json` is auto-appended when `!Path.HasExtension(filePath)` (`ExportCommand.cs:37-40`) | **Yes — user-visible** | Preserve `~/` expansion and the extension defaulting. Note what is *not* supported: bare `~`, `~user/`, `~\`. |
| **D16** | **Command surface**: `/`-prefixed dispatch (`src/ChatDbg/ChatShell.cs:92`), 15 registered names, and the tri-state `CommandResult{Success, Message, ExitRequested}` rendered as `✓ msg` / `✗ msg` (`:102-104`) | **Yes** | The exact command names, argument grammar (`README.md:41-100`), validation ranges — temperature 0–2 (`SetCommand.cs:72`), maxTokens 1–8192 (`:81`) — and the ✓/✗ result convention. Three commands (`export-logs`, `export-analysis`, `show-analysis`) exist as classes but are **unregistered stubs** — do not port them as features. |
| **D17** | **`IConsoleFormatter` whose contract is Spectre markup strings** with a markup-stripping `BasicConsoleFormatter` fallback (`Services/IConsoleFormatter.cs`, `Services/BasicConsoleFormatter.cs`) | **Partly** | Preserve the *seam* (Core must not depend on a specific renderer) and the two rendering modes (`DisplayTokenGrid` vs. `DisplayTokenTable`, selected by `settings.GridViewForTokens`). Do **not** preserve Spectre's `[color]…[/]` syntax as the interface's data format — that is the leak. |
| **D18** | **Core still calls `Console.*` 119 times, including interactive `Console.ReadLine()` from `SettingsService`** during credential migration (`Services/SettingsService.cs:121,211,247`) | **No — a defect** | The migration *flow* (offer env vars / credential manager / both, then optionally clear the JSON keys — `:213-259`) is worth preserving; driving it with `Console.ReadLine()` from a library is not. It hangs the Terminal.Gui shell. |
| **D19** | **No DI container, no `ILogger`** — manual composition in two entry points, `Debug.WriteLine` + a bespoke `StringBuilder` logger (§7.2, §7.7) | **No** | Free to change. But preserve the *log file format* — `[yyyy-MM-dd HH:mm:ss.fff] [LEVEL] message`, appended, daily-rotated as `llamasharp_yyyyMMdd.log` (`Services/TokenInspection/LLamaSharpLogConfig.cs:83,121,149`), buffered in memory with a 10,000-char flush threshold (`:40,90`) — and the fact that a **native llama.cpp callback** (`NativeLogConfig.llama_log_set`, `:81`) writes into the same lock-protected buffer. |
| **D20** | **`LLamaSharp.Backend.Cuda12` as an unconditional `PackageReference`** costing ~550 MB (§3) | **No — and it should be broken** | A port must make GPU backends **opt-in** (separate package/plugin/download), not a mandatory dependency of the core library. Preserve the *capability* (`llamaGpuLayerCount`, `llamaGpuDevice` settings — `Models/ChatSettings.cs:56-60`), not the packaging. |
| **D21** | **Two shells over one Core, with the console shell being the only one CI ships** (§7.6, §10) | **Design intent yes; the duplication no** | Preserve the principle that Core is UI-agnostic. Do **not** port `src/ChatDbg.Shell.Gui/ChatShell.cs` — it is 671 lines of dead near-duplicate. Decide up front which shell is the product; the repo never did. |
| **D22** | **AOT/trimming configurations that cannot work with this dependency set** (§8.3, B1–B7) | **No — aspirational, never validated** | Do not port `Compact`/`SingleFile` verbatim. Preserve the *goals* — small self-contained binary, fast start — and re-derive the settings against whatever runtime the port targets. If AOT is wanted, it forces source-generated JSON (B3) and the elimination of D11's reflection (B2). |
| **D23** | **`InvariantGlobalization=true` + culture-less `double.TryParse`** (`Commands/SetCommand.cs:72`) | **Yes, subtly** | Settings values (`temperature`) must parse and format identically regardless of host locale. Pin invariant culture explicitly in the port rather than relying on a publish-time MSBuild property that only applies to two configurations. |
| **D24** | **Corrupted status glyphs in committed source** — `?`/`??` where `✓`/`⚠`/`🔑` were meant (`Services/SettingsService.cs:62,69,73,110,116,128-130,…`; `Commands/SetCommand.cs:342`) | **No — damage, not design** | Restore intended glyphs. Do not faithfully reproduce `??  WARNING:`. |
| **D25** | **Malformed `global.json` + a `9.0.x` CI SDK against `net10.0` projects** (§1.4, §10.2) | **No — broken** | Nothing to preserve. Pin one runtime version in exactly one place and make CI use it. |

---

## 12. Consolidated Contradiction Register

| ID | Contradiction | Primary evidence |
|---|---|---|
| C1 | `global.json` is **invalid JSON** — a stray `}` at byte 95 | `global.json:1-6`; `xxd` tail `7d0a 7d7d` |
| C2 | CI installs **.NET 9** (`dotnet-version: '9.0.x'`) to build **`net10.0`** projects pinned by `global.json` to `10.0.100-rc.1.25451.107` with `rollForward: latestFeature` | `.github/workflows/build-release.yml:36-39` vs. `global.json:3-4` vs. all four `.csproj` |
| C3 | Build scripts and `docs/compact-build.md` reference **`net9.0`** output paths; every project targets `net10.0` | `build-compact.bat:20,22`; `build-singlefile.bat:21,25`; `build-compact.ps1:23,27`; `build-compact-robust.bat:57,58,80,81`; `docs/compact-build.md:97,98,132` |
| C4 | `PublishAot` + `TrimMode=full` vs. `GetType().GetMethod(…)`/`Invoke` and `(dynamic)` in the hot path | csproj `:25,34` vs. `Services/LLamaSharpService.cs:423,429,499-500` |
| C5 | `PublishTrimmed` vs. reflection-based `System.Text.Json` on anonymous types, with no `JsonSerializerContext` | csproj `:26,66` vs. `Services/AzureOpenAIService.cs:151-169`, `Services/BedrockService.cs:68-115` |
| C6 | `SuppressTrimAnalysisWarnings=true` hides exactly the warnings that would surface C4/C5 | csproj `:35,75` |
| C7 | AOT vs. `LLamaSharp.Backend.*`'s 630 MB of RID-probed loose native libraries | csproj `:25` vs. assets §3 |
| C8 | AOT vs. `Terminal.Gui` → `System.Management` (WMI, Windows-only RID asset) → `System.CodeDom` (runtime codegen) | GUI csproj `:23-60,121` vs. assets §2.4 |
| C9 | `Compact`/`SingleFile` are not solution configurations | `Xcaciv.ChatDbg.sln:24-45` vs. both csproj `:23,63` |
| C10 | Docs claim 13–25 MB binaries; the measured build output is 630 MB and the one tracked binary (15,677,171 B) clearly excludes the LLamaSharp natives | `docs/compact-build.md:124-126`, `docs/github-actions-release.md:86,92`, `docs/release-setup-complete.md:62-63` vs. **[build output]** `du -sh src/ChatDbg/bin/Debug/net10.0` and `ls -l test-publish/` |
| C11 | `Spectre.Console 0.51.1` is a `PackageReference` on **Core** with zero Core usage (3 vestigial `using`s only) | Core csproj `:16` vs. `Commands/ExportLogsCommand.cs:1`, `Commands/ExportTokenAnalysisCommand.cs:1`, `Commands/ShowTokenAnalysisCommand.cs:1` |
| C12 | `AWSSDK.BedrockRuntime` and `Azure.AI.OpenAI` are `PackageReference`d by both shells with zero shell usage | Shell csproj `:108-109`, GUI csproj `:118-119` vs. `grep "^using" src/ChatDbg src/ChatDbg.Shell.Gui` |
| C13 | `AllowUnsafeBlocks=true` on Core with no `unsafe`/pointer/`stackalloc` anywhere | Core csproj `:7` |
| C14 | `IConsoleFormatter` exists *"to remove UI dependencies from Core"* while Core makes 119 direct `Console.*` calls, three of them blocking `Console.ReadLine()` | `Services/IConsoleFormatter.cs:9` vs. `Services/SettingsService.cs:121,211,247` and 8 other files |
| C15 | GUI shell contains a 671-line dead `ChatShell.cs` that nothing instantiates and that omits the `llama` provider | `src/ChatDbg.Shell.Gui/ChatShell.cs:14,33-37`; `grep -rn ChatShell src/ChatDbg.Shell.Gui` |
| C16 | GUI shell **never disposes** its three `IAIService` instances (native model/context leak); the console shell does | `src/ChatDbg.Shell.Gui/Program.cs` (no `Dispose`) vs. `src/ChatDbg/ChatShell.cs:692-713` |
| C17 | `LLamaSharpService.Dispose(bool)` disposes a **static** `SemaphoreSlim` from an instance dispose, and disposes only one of the two statics | `Services/LLamaSharpService.cs:23-24,681` |
| C18 | `SystemPromptService` uses `_jsonOptions` before it is assigned — default prompts are written un-indented, later prompts indented | `Services/SystemPromptService.cs:32` (call) vs. `:34-37` (assignment), consumed at `:106` |
| C19 | Three commands are implemented, tested, and never registered by either shell | `Commands/{ExportLogs,ExportTokenAnalysis,ShowTokenAnalysis}Command.cs` vs. `src/ChatDbg/ChatShell.cs:42-66`, `src/ChatDbg.Shell.Gui/Program.cs:31-55` |
| C20 | README markets the Terminal.Gui experience as the product; CI builds and releases only the console shell | `README.md:3,8,23-37` vs. `.github/workflows/build-release.yml:42,47,59` |
| C21 | CI never runs `dotnet test`, so 100 facts and `coverlet.collector` never execute in automation; and there is no push/PR trigger at all | `.github/workflows/build-release.yml:3-14,32-109` |
| C22 | The workflow computes the artifact size but the `id: file_info` step output is never consumed | `.github/workflows/build-release.yml:95-102` |
| C23 | `docs/compact-build.md:144-146` prescribes the exact AOT remedies (`[DynamicallyAccessedMembers]`, STJ source generators, `TrimmerRootAssembly`) that the code never applies | `docs/compact-build.md:144-146` |
| C24 | User-facing status glyphs are already mojibake in committed source | `Services/SettingsService.cs:62,69,73,…`; `Commands/SetCommand.cs:342`; `file` reports Non-ISO extended-ASCII |
| C25 | `docs/PHASE1-SUMMARY.md:60,208` and `docs/TERMINAL-GUI-IMPLEMENTATION.md:128` state **Terminal.Gui 1.17.1**; the csproj pins **1.19.0** | those lines vs. GUI csproj `:121` |
| C26 | `README.md:3` says *"for Windows terminal"*; the csproj `AssemblyDescription` says *"Cross-platform chat debugging tool"*; CI ships a Linux binary | `README.md:3` vs. `src/ChatDbg/…csproj:16` vs. workflow `:26-30` |
| C27 | The GUI csproj's `Compile Remove="Services\**"` + two explicit `Include`s form a fragile allow-list — any new file in `Services/` silently stops compiling | GUI csproj `:102,106,110,113-114` |
| C28 | The console shell's csproj removes `Serialization\**` and `Services\**`, neither of which exists in that project | Console csproj `:99-104` |

---

## 13. Quick Reference — every direct dependency, exact version string

```
AWSSDK.BedrockRuntime          4.0.7.3     Core, Shell, Shell.Gui   (unused in both shells)
Azure.AI.OpenAI                2.1.0       Core, Shell, Shell.Gui   (unused in both shells)
LLamaSharp                     0.25.0      Core
LLamaSharp.Backend.Cpu         0.25.0      Core                     (85 native files / 6 RIDs)
LLamaSharp.Backend.Cuda12      0.25.0      Core                     (metapackage -> Win + Linux, ~550 MB)
Spectre.Console                0.51.1      Core, Shell, Shell.Gui   (UNUSED in Core)
Terminal.Gui                   1.19.0      Shell.Gui                (-> NStack.Core, System.Management)
Microsoft.NET.Test.Sdk        17.12.0      Core.Tests
xunit                          2.9.1       Core.Tests
xunit.runner.visualstudio      2.8.1       Core.Tests               (PrivateAssets=all)
Moq                           4.20.69      Core.Tests
coverlet.collector             6.0.2       Core.Tests
```

SDK pin: `10.0.100-rc.1.25451.107`, `rollForward: latestFeature` (`global.json:3-4`, file is invalid JSON).
Language: `LangVersion=latest` (`Directory.Build.props:3`). TFM: `net10.0` (all four projects).
`Nullable=enable` and `ImplicitUsings=enable` on all four; `AllowUnsafeBlocks=true` on Core only (unused).
