# TECH_STACK.md

> Concrete .NET 10 technology stack for the Cupcake pivot: Lit, Concierge Agent template, Funfetti, and Sommelier.
> Drafted 2026-09-07 for consideration. Versions verified against api.nuget.org on that date. Status column reflects the package's own stability, not Cupcake's confidence.
> Builds on: `TECH_STACK_SUGGESTED.md` (layering rules), `MAF-EVALUATION.md` (agent substrate decisions), `../chatdbg/TECH-STACK-TARGET.md` (decisions inherited where the ChatDbg reasoning still applies), `../opencode/dossiers/*` (data model and storage requirements), `../../project-pivot.md` (products).

---

## 1. Platform baseline

| Item | Choice | Notes |
|---|---|---|
| Target framework | `net10.0` everywhere | LTS to 2028-11-14. MAF 1.20.0 and MEAI 10.9.0 ship `net10.0` assets. The existing `Xcaciv.Cupcake` Release retarget to `net6.0-windows` is removed. |
| SDK | 10.0.4xx band, pinned by `global.json` | Local machine has 10.0.400. |
| Language | C# 14, `Nullable` enable, `ImplicitUsings` enable, `TreatWarningsAsErrors` | |
| Package management | Central Package Management (`Directory.Packages.props`) already in use | Extend, do not replace. |
| Serialization | `System.Text.Json` source-generated contexts, `JsonSerializerIsReflectionEnabledByDefault=false` | Plugin-contributed types compose through `TypeInfoResolverChain`. |
| Trimming / AOT | **Off** for every host that loads command packages | `Xcaciv.Loader` uses runtime `AssemblyLoadContext`. Concierge Cupcake (no plugin architecture) **may** trim; AOT remains blocked by `AIFunctionFactory` reflection unless measured otherwise. |
| Test runner | `Microsoft.Testing.Platform` v2, selected repo-wide via `global.json` `"test": { "runner": "Microsoft.Testing.Platform" }` | Whole-repo decision; VSTest and MTP projects must not mix. |
| Publish (terminal hosts) | Self-contained, single-file, ReadyToRun, untrimmed, one artifact per RID, plugins outside the bundle | Plus a `dotnet tool` package for Lit (`dotnet tool install -g cupcake-lit`). |
| Publish (Sommelier) | SDK container publish, OCI, non-chiseled | |

`global.json` sketch:

```json
{
  "sdk": { "version": "10.0.400", "rollForward": "latestFeature" },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

---

## 2. Project map

Layer names follow `TECH_STACK_SUGGESTED.md`. Root namespace prefix is `Xcaciv.Cupcake`.

```
src/
  Xcaciv.Cupcake.Core/                        Layer 1  contracts, rule engine, catalog model, event-log schema
  Xcaciv.Cupcake.Core.Agent/                  Layer 1  MAF adapters: command->AIFunction, PolicyGatedAIFunction,
                                                        SQLite AgentSessionStore / ChatHistoryProvider / JsonCheckpointStore,
                                                        planning-strategy context providers
  Xcaciv.Cupcake.Core.Storage/                Layer 1  SQLite event log, projections, migrations, credential store
  Xcaciv.Cupcake.Core.Profile.Interactive/    Layer 2  streaming renderer contract, session lifecycle, UX telemetry
  Xcaciv.Cupcake.Core.Profile.Headless/       Layer 2  structured output, batch engine, result sinks, eval harness
  Xcaciv.Cupcake.Providers.OpenAI/            adapters (layer-less)  OpenAI + Azure OpenAI via OpenAI SDK
  Xcaciv.Cupcake.Providers.Anthropic/         adapters
  Xcaciv.Cupcake.Providers.Bedrock/           adapters
  Xcaciv.Cupcake.Providers.Ollama/            adapters
  Xcaciv.Cupcake.Providers.LlamaSharp/        adapters (shipped as an installable command package, not core)
  Xcaciv.Cupcake.Transports.Console/          input-interface plugins (layer-less)
  Xcaciv.Cupcake.Transports.Acp/
  Xcaciv.Cupcake.Transports.Rest/
  Xcaciv.Cupcake.Transports.Mcp/              Cupcake-as-MCP-server
  Xcaciv.Cupcake.Lit/                         Layer 3  dotnet tool, Terminal.Gui, restricted console, Concierge wizard
  Xcaciv.Cupcake.ConciergeAgent.Template/     Layer 3  `dotnet new` template pack; Spectre.Console; headless capable
  Xcaciv.Cupcake.Funfetti/                    Layer 3  Terminal.Gui coding agent, modes, plugin architecture
  Xcaciv.Cupcake.Sommelier.NuGet/             Layer 3  BaGetter-based feed + registration + submission
  Xcaciv.Cupcake.Sommelier.Web/               Layer 3  gallery UI
  Xcaciv.Command.Packages/                    existing command packages
tests/
  *.Tests/                                    xunit.v3 on MTP
benchmarks/
  Xcaciv.Cupcake.Benchmarks/                  BenchmarkDotNet
```

Dependency rule unchanged: `Core` never references `Profile.*` or hosts; `Profile.*` never reference each other; only hosts reference ASP.NET Core, Terminal.Gui, Spectre, and MAF hosting packages.

---

## 3. Package matrix by concern

Status: **GA**, **pre** (preview/alpha/rc), **exp** (GA package, `[Experimental]` API), **0.x** (stable but pre-1.0 semver).

### 3.1 AI abstractions and agent runtime

| Package | Version | Status | Layer | Role |
|---|---|---|---|---|
| `Microsoft.Extensions.AI.Abstractions` | 10.9.0 | GA | Core | `IChatClient`, `IEmbeddingGenerator`, `AITool`, `AIFunction`, `ChatMessage`, `UsageDetails`. Zero transitive deps on net10. |
| `Microsoft.Extensions.AI` | 10.9.0 | GA | Core.Agent, Profiles | `ChatClientBuilder`, `FunctionInvokingChatClient`, `OpenTelemetryChatClient`, `DistributedCachingChatClient`, `RoutingChatClient` (exp). |
| `Microsoft.Agents.AI.Abstractions` | 1.20.0 | GA | Core | `AIAgent`, `AgentSession`, `AgentSessionStore`, `AIContextProvider`, `ChatHistoryProvider`. Depends only on MEAI.Abstractions. |
| `Microsoft.Agents.AI` | 1.20.0 | GA | Core.Agent, Profiles | `ChatClientAgent`, middleware builder, `ApprovalRequiredAIFunction` handling, `InMemoryChatHistoryProvider`, compaction (exp `MAAI001`). |
| `Microsoft.Agents.AI.Harness` | 1.20.0 | GA | Profile.Interactive, Funfetti | `HarnessAgent`, `AgentModeProvider`, `TodoProvider`, `AgentSkillsProvider`, `ToolApprovalAgent`, `LoopAgent`. Shell tooling deliberately **not** referenced. |
| `Microsoft.Agents.AI.Workflows` | 1.20.0 | GA | Funfetti, Headless | Multi-agent orchestration, checkpointing (`JsonCheckpointStore`). |
| `Microsoft.Extensions.AI.Evaluation` | 10.9.0 | GA | Profile.Headless | Evaluation harness for prompts and modes. Already a transitive dependency of MAF. |
| `Microsoft.Extensions.VectorData.Abstractions` | 10.9.0 | GA | Core.Agent | Memory provider contract. MAF pins 10.8.2; app pins 10.9.0. |
| `Microsoft.ML.Tokenizers` | 2.0.0 | GA | Core | Token counting for compaction triggers and cost. **Do not take 3.0.0-preview**; MAF pins 2.0.0. |

Optional and deferred:

| Package | Version | Status | Use when |
|---|---|---|---|
| `Microsoft.Agents.AI.Mcp` | 1.20.0-alpha.260831.1 | pre | MCP long-running tasks (2026-07-28 Tasks extension). Otherwise the MCP SDK alone suffices. Pins `ModelContextProtocol` 2.1.0. |
| `Microsoft.Agents.AI.A2A` | 1.20.0-preview.260831.1 | pre | Consuming remote A2A agents. Pins `A2A` 1.0.0-preview2. |
| `Microsoft.Agents.AI.Hosting` | 1.20.0-preview.260831.1 | pre | Sommelier-hosted agents; REST transport plugin. |
| `Microsoft.Agents.AI.Hosting.A2A.AspNetCore` | 1.20.0-preview.260831.1 | pre | Serving A2A. |
| `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` | 1.20.0-preview.260831.1 | pre | Web UI later. |
| `Microsoft.Agents.AI.Hosting.OpenAI` | 1.20.0-alpha.260831.1 | pre | OpenAI-compatible endpoint. |
| `Microsoft.Agents.AI.DurableTask` | 1.16.0-preview.260730.1 | pre, lagging | Not planned; Cupcake durability is the SQLite event log. |
| `Microsoft.Agents.AI.Mem0` | 1.0.0-preview.251028.1 | pre, stale | Not planned. |
| `Microsoft.Agents.AI.Declarative` | 1.20.0-rc1 | pre | YAML agent definitions; revisit for Concierge templates. |
| `Microsoft.Agents.AI.GitHub.Copilot` | 1.20.0 | GA | Optional Copilot backend for Funfetti. |

### 3.2 Model providers

| Package | Version | Status | Notes |
|---|---|---|---|
| `Microsoft.Extensions.AI.OpenAI` | 10.9.0 | GA | `AsIChatClient()` over `OpenAI.Chat.ChatClient`. One client type for OpenAI and Azure OpenAI (`https://{res}.openai.azure.com/openai/v1/`). Pins the `OpenAI` SDK range; let it resolve (2.12.x) rather than pinning 2.13.0 directly. |
| `Azure.Identity` | 1.21.0 | GA | Explicit `ChainedTokenCredential`, never bare `DefaultAzureCredential`. |
| `Anthropic` | 12.46.0 | GA | Official Anthropic C# SDK (MIT, `anthropics/anthropic-sdk-csharp`). `Microsoft.Agents.AI.Anthropic` (pre) pins 12.42.0; prefer the SDK's own MEAI adapter if present, otherwise the MAF package. |
| `AWS.Bedrock.MEAI` + `AWSSDK.BedrockRuntime` | 1.0.0 / 4.0.101 | GA | Young adapter; fallback is a hand-written `IChatClient`. |
| `OllamaSharp` | 5.4.30 | GA | Implements `IChatClient`; typed logprobs. Preferred local path for most users. |
| `LLamaSharp` + `LLamaSharp.Backend.Cpu` | 0.27.0 | 0.x | Only inside the optional ChatDbg-style introspection command package; keeps native binaries out of the core hosts. |

Not taken: `Azure.AI.OpenAI` 2.1.0 (20 months stale; beta only since), `Microsoft.Extensions.AI.AzureAIInference` (retiring), `Anthropic.SDK` (community; superseded by the official package).

### 3.3 Tools and interoperability protocols

| Package | Version | Status | Notes |
|---|---|---|---|
| `ModelContextProtocol` | 2.2.0 | GA | Client (`McpClientTool` is an `AIFunction`) and stdio server. |
| `ModelContextProtocol.AspNetCore` | 2.2.0 | GA | HTTP MCP server for Sommelier-hosted tools. |
| `ModelContextProtocol.Extensions.Tasks` | 2.2.0 | GA | Long-running tool tasks. |
| `A2A` / `A2A.AspNetCore` | 1.0.0-preview2 | pre | Only through the MAF A2A packages. |
| ACP: `dotacp.protocol` / `dotacp.agent` | 2026.7.19 | community, tiny | Candidate for generated protocol types. Alternatives `AcpKit.*.V1` 0.0.4, `AgentClientProtocol` 0.1.5. **Decision pending:** take generated types from one of these and own the transport, or hand-roll over `StreamJsonRpc`. |
| `StreamJsonRpc` | 2.25.29 | GA | Fallback JSON-RPC transport for ACP and for any first-party out-of-process bridge. |
| ANP | none | | No .NET implementation exists. Out of scope. |

### 3.4 Command framework, plugin loading, package install

| Package | Version | Status | Notes |
|---|---|---|---|
| `Xcaciv.Command.Interface` | this repo pins 2.1.0; chatdbg research cites 3.3.x | FIXED | Confirm current release on the `github` feed in `NuGet.config`. Contract assembly must load once in the Default ALC. |
| `Xcaciv.Command.Core`, `Xcaciv.Command` | as above | FIXED | Add `AskOrExecute()` (default policy effect) to the command contract. |
| `Xcaciv.Loader` | 2.1.2 | FIXED | One cached `AssemblyContext` per package per session; strict integrity mode in release; never `"*"` base path. **AGPL-3.0**: legal note applies to Sommelier's hosted mode. |
| `NuGet.Protocol`, `NuGet.Packaging` | 7.9.0 | GA | Sommelier client: search, download, extract, TFM asset selection, signature verification against an explicit trust store (self-contained hosts have no SDK bundle). Repo currently pins 7.0.1. |
| `System.IO.Hashing` | 10.0.11 | GA | Package and assembly hash manifests. |
| `Microsoft.Extensions.FileSystemGlobbing` | 10.0.11 | GA | Rule-engine resource patterns, Agent Skills discovery. Already transitive via MAF. |

Fallback for `Xcaciv.Loader` on licence grounds: `McMaster.NETCore.Plugins` 2.0.0 (no release since 2025-01) or ~80 lines of `AssemblyLoadContext` + `AssemblyDependencyResolver`.

### 3.5 Host, configuration, DI, CLI

| Package | Version | Status | Notes |
|---|---|---|---|
| `Microsoft.Extensions.Hosting` | 10.0.11 | GA | Generic Host in every product; REPL and TUI run as `BackgroundService` with a custom `IHostLifetime`. |
| `Microsoft.Extensions.Configuration.Json`, `.EnvironmentVariables`, `.CommandLine` | 10.0.11 | GA | opencode-style discovery: walk up to the repo root, global config dir, env, flags. JSON-with-comments via `System.Text.Json` `JsonCommentHandling.Skip`. `{env:NAME}` / `{file:path}` substitution is Cupcake code. |
| `Microsoft.Extensions.Options.DataAnnotations` | 10.0.11 | GA | `AddOptionsWithValidateOnStart<T>()`, `[OptionsValidator]`, `EnableConfigurationBindingGenerator`. |
| `System.CommandLine` | 2.0.11 | GA | Entry-point parsing for `cupcake_lit`, `funfetti`, Concierge binaries. Stay off the 3.0 previews. |
| `Microsoft.Extensions.Compliance.Redaction` | 10.9.0 | GA | Secret redaction in logs and audit output. MAF already depends on `Compliance.Abstractions`. |

### 3.6 Secrets and identity

| Package | Version | Status | Notes |
|---|---|---|---|
| `Microsoft.Identity.Client.Extensions.Msal` | 4.88.0 | GA | `Storage` for encrypted-at-rest secrets (DPAPI / Keychain / libsecret). Store nothing by default; prefer provider credential chains. |
| `Azure.Identity` | 1.21.0 | GA | See §3.2. |
| Cupcake `Credential` / `Integration` tables | | | SQLite rows hold references and metadata; secret material is an MSAL `Storage` blob keyed by row id. Matches the opencode dossier. |

### 3.7 State, storage, cache, memory

| Package | Version | Status | Notes |
|---|---|---|---|
| `Microsoft.Data.Sqlite` | 10.0.11 | GA | WAL, busy timeout, foreign keys, single-writer fencing per aggregate. Append-only event log plus projections; hand-written SQL, no EF Core. |
| `Microsoft.Extensions.Caching.Hybrid` | 10.9.0 | GA | Optional response cache behind `UseDistributedCache` for Headless replay and evals. |
| `NeoSmart.Caching.Sqlite` | 9.0.3 | GA | `IDistributedCache` over SQLite so the cache lives beside the event log. Optional. |
| `Microsoft.Extensions.VectorData.Abstractions` | 10.9.0 | GA | Memory provider contract. |
| `Microsoft.SemanticKernel.Connectors.SqliteVec` | 1.74.0-preview | pre | Only if vector memory is enabled; otherwise no vector store at all in v1. |

### 3.8 Resilience

| Package | Version | Status | Notes |
|---|---|---|---|
| `Microsoft.Extensions.Http.Resilience` | 10.9.0 | GA | Explicit `AddResilienceHandler("llm", ...)`; honours `Retry-After`. Never `AddStandardResilienceHandler()`. Context-window overflow is never retried. |
| `Polly.Core` | 8.7.0 | GA | Transitive. |

### 3.9 Terminal UI and rendering

| Package | Version | Status | Product | Notes |
|---|---|---|---|---|
| `Terminal.Gui` | 2.4.17 | GA (v2 since 2026-04-28) | Lit, Funfetti | v2 only. Redirected stdout yields nothing, so every TUI product also ships a non-interactive entry point (`--headless`, `--format json`). Read `ColorCapabilities`, not `SupportsTrueColor`. |
| `Spectre.Console` | 0.57.2 | 0.x | Concierge, plain/pipe mode everywhere | Every view is a pure function returning `IRenderable`; three output modes `rich` / `plain` / `data`. `LiveDisplay` has no redirect fallback. |
| `Terminal.Gui.Interop.Spectre` | 2.4.17 | GA, tiny | Lit, Funfetti | ~200 lines; read before depending. Renders Spectre output inside Terminal.Gui views. |

Not taken: `Spectre.Console.Cli` (System.CommandLine is the parser), Terminal.Gui 1.x.

### 3.10 Observability and logging

| Package | Version | Status | Notes |
|---|---|---|---|
| `OpenTelemetry` | 1.18.0 | GA | One `ActivitySource` and one `Meter` named `Xcaciv.Cupcake`. Always instrumented, exported only on an explicit flag. MAF pins `OpenTelemetry.Api` 1.15.3; compatible. |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.18.0 | GA | Referenced, unconfigured by default. |
| `Microsoft.Extensions.Logging` + `[LoggerMessage]` | 10.0.11 | GA | ~150-line NDJSON rolling-file provider. Serilog is the documented alternative. |
| MEAI `UseOpenTelemetry`, MAF `UseOpenTelemetry` | | | GenAI semantic conventions for free on the chat and agent paths. Harness enables agent OTel by default. |

### 3.11 Sommelier (package server)

| Package | Version | Status | Notes |
|---|---|---|---|
| `BaGetter.Core`, `BaGetter.Web` | 1.6.5 | GA, MIT | Lightweight NuGet v3 server, community fork of BaGet. Targets `net9.0` at 1.6.5; consumable from a `net10.0` host. Fork or extend for user registration, API keys per user, package submission review, and signature policy. |
| ASP.NET Core Identity (in-box) | 10.0 | GA | Registration and login. |
| `Aspire.Hosting.AppHost` | 13.5.3 | GA | Optional local orchestration of NuGet + Web + SQLite/Postgres for development. |

Not taken: forking `NuGet/NuGetGallery`. It is a .NET Framework application whose self-hosting is described by its own issue tracker as "not for the faint of heart". Mimic nuget.org behaviour on BaGetter instead.

### 3.12 Concierge (template and build)

| Package | Version | Status | Notes |
|---|---|---|---|
| `Microsoft.TemplateEngine.Authoring.Templates` | 10.0.400 | GA | Authoring the `dotnet new cupcake-concierge` template pack. |
| `dotnet` CLI as a process | | | Lit's wizard writes `template.json` parameters, runs `dotnet new`, then `dotnet publish -r <rid>`. No MSBuild API dependency. |
| Concierge output | | | Spectre.Console host, hardcoded command registration, `Microsoft.Agents.AI` + one provider adapter, headless prompt execution via Profile.Headless. Trimming allowed here. |

### 3.13 Testing and quality

| Package | Version | Status | Notes |
|---|---|---|---|
| `xunit.v3` | 4.0.0 | GA | Rollback path is 3.2.2 (also flips to MTP v1). |
| `Microsoft.Testing.Platform` | 2.4.0 | GA | |
| `Microsoft.Testing.Extensions.HangDump`, `.CrashDump` | 2.4.0 | GA | Essential once LLamaSharp natives are in any test. |
| `Verify.XunitV3` | 32.0.0 (pinned) | GA | Last pre-fee release. Render snapshots for Spectre output. |
| `NSubstitute` | 6.2.0 | GA | Narrowly; hand-rolled fakes by default. A scripted `FakeChatClient` replays streaming protocols. |
| `BenchmarkDotNet` | 0.15.8 | GA | Separate project; hot paths are the event-log append and compaction. |
| `Microsoft.Extensions.AI.Evaluation` | 10.9.0 | GA | Mode and prompt regression evals in Headless. |

### 3.14 Messaging (deferred)

ESB input plugins are out of scope for v1. When needed: `MassTransit` 9.2.1 is current but **v9 moved to commercial licensing**; MassTransit 8.x remains Apache-2.0. `WolverineFx` is the other candidate. Decide at the time; the Core `IAgentTransport` contract is what matters now.

---

## 4. Version alignment constraints

These pins form one graph. Upgrade them together.

| Anchor | Requires | Consequence |
|---|---|---|
| `Microsoft.Agents.AI` 1.20.0 | `Microsoft.Extensions.AI` 10.9.0, `Microsoft.ML.Tokenizers` 2.0.0, `VectorData.Abstractions` 10.8.2+, `OpenTelemetry.Api` 1.15.3+, `Microsoft.Extensions.*` 10.0.11 | MEAI and MAF are upgraded in the same change. |
| `Microsoft.Extensions.AI.OpenAI` 10.9.0 | `OpenAI` 2.12.x range | Do not pin `OpenAI` 2.13.0 directly. |
| `Microsoft.Agents.AI.Mcp` alpha | `ModelContextProtocol` 2.1.0 | App pins 2.2.0; NuGet resolves upward. Test after each MAF release. |
| `Microsoft.Agents.AI.Anthropic` preview | `Anthropic` 12.42.0+ | App pins 12.46.0. |
| `Microsoft.Agents.AI.A2A` preview | `A2A` 1.0.0-preview2 | Preview-on-preview; hosts only. |
| Plugin host allowlist | `Xcaciv.Command.Interface`, `Microsoft.Extensions.AI.Abstractions`, `Microsoft.Agents.AI.Abstractions`, `System.Text.Json`, `Microsoft.Extensions.Logging.Abstractions` | Published with versions; a command package whose `deps.json` demands a higher version is refused with a message naming both. |

---

## 5. Product bills of materials

| Group | Lit | Concierge (generated) | Funfetti | Sommelier |
|---|---|---|---|---|
| 3.1 core agent runtime | yes | yes (no Workflows, no Harness unless chosen) | yes, all | Hosting (pre) only if agents are hosted |
| 3.2 providers | installable packages | one, chosen at build | installable packages | none |
| 3.3 MCP / A2A / ACP | MCP client + server | MCP client optional | all | MCP HTTP server optional |
| 3.4 command framework + loader | yes | Command.Interface only, no Loader | yes | client side of NuGet.Protocol |
| 3.5 host / config / CLI | yes | yes | yes | yes |
| 3.6 secrets | yes | yes | yes | Identity |
| 3.7 SQLite state | yes | optional | yes | Postgres or SQLite via BaGetter |
| 3.9 TUI | Terminal.Gui + Spectre | Spectre | Terminal.Gui + Spectre | none |
| 3.10 observability | yes | yes | yes | yes, exported |
| 3.12 template tooling | authoring + wizard | consumer | none | none |

---

## 6. `Directory.Packages.props` sketch

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup Label="AI">
    <PackageVersion Include="Microsoft.Extensions.AI.Abstractions" Version="10.9.0" />
    <PackageVersion Include="Microsoft.Extensions.AI" Version="10.9.0" />
    <PackageVersion Include="Microsoft.Extensions.AI.OpenAI" Version="10.9.0" />
    <PackageVersion Include="Microsoft.Extensions.AI.Evaluation" Version="10.9.0" />
    <PackageVersion Include="Microsoft.Extensions.VectorData.Abstractions" Version="10.9.0" />
    <PackageVersion Include="Microsoft.ML.Tokenizers" Version="2.0.0" />
    <PackageVersion Include="Microsoft.Agents.AI.Abstractions" Version="1.20.0" />
    <PackageVersion Include="Microsoft.Agents.AI" Version="1.20.0" />
    <PackageVersion Include="Microsoft.Agents.AI.Harness" Version="1.20.0" />
    <PackageVersion Include="Microsoft.Agents.AI.Workflows" Version="1.20.0" />
    <PackageVersion Include="Microsoft.Agents.AI.A2A" Version="1.20.0-preview.260831.1" />
    <PackageVersion Include="Microsoft.Agents.AI.Hosting" Version="1.20.0-preview.260831.1" />
    <PackageVersion Include="Microsoft.Agents.AI.Hosting.A2A.AspNetCore" Version="1.20.0-preview.260831.1" />
    <PackageVersion Include="Anthropic" Version="12.46.0" />
    <PackageVersion Include="AWS.Bedrock.MEAI" Version="1.0.0" />
    <PackageVersion Include="OllamaSharp" Version="5.4.30" />
    <PackageVersion Include="Azure.Identity" Version="1.21.0" />
  </ItemGroup>
  <ItemGroup Label="Protocols">
    <PackageVersion Include="ModelContextProtocol" Version="2.2.0" />
    <PackageVersion Include="ModelContextProtocol.AspNetCore" Version="2.2.0" />
    <PackageVersion Include="ModelContextProtocol.Extensions.Tasks" Version="2.2.0" />
    <PackageVersion Include="StreamJsonRpc" Version="2.25.29" />
  </ItemGroup>
  <ItemGroup Label="Commands and packages">
    <PackageVersion Include="Xcaciv.Command.Interface" Version="TBD" />
    <PackageVersion Include="Xcaciv.Command.Core" Version="TBD" />
    <PackageVersion Include="Xcaciv.Command" Version="TBD" />
    <PackageVersion Include="Xcaciv.Loader" Version="2.1.2" />
    <PackageVersion Include="NuGet.Protocol" Version="7.9.0" />
    <PackageVersion Include="NuGet.Packaging" Version="7.9.0" />
    <PackageVersion Include="System.IO.Hashing" Version="10.0.11" />
  </ItemGroup>
  <ItemGroup Label="Host">
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="10.0.11" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Json" Version="10.0.11" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="10.0.11" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.CommandLine" Version="10.0.11" />
    <PackageVersion Include="Microsoft.Extensions.Options.DataAnnotations" Version="10.0.11" />
    <PackageVersion Include="Microsoft.Extensions.Compliance.Redaction" Version="10.9.0" />
    <PackageVersion Include="Microsoft.Extensions.Http.Resilience" Version="10.9.0" />
    <PackageVersion Include="System.CommandLine" Version="2.0.11" />
    <PackageVersion Include="Microsoft.Identity.Client.Extensions.Msal" Version="4.88.0" />
  </ItemGroup>
  <ItemGroup Label="Storage">
    <PackageVersion Include="Microsoft.Data.Sqlite" Version="10.0.11" />
    <PackageVersion Include="Microsoft.Extensions.Caching.Hybrid" Version="10.9.0" />
    <PackageVersion Include="NeoSmart.Caching.Sqlite" Version="9.0.3" />
  </ItemGroup>
  <ItemGroup Label="UI">
    <PackageVersion Include="Terminal.Gui" Version="2.4.17" />
    <PackageVersion Include="Terminal.Gui.Interop.Spectre" Version="2.4.17" />
    <PackageVersion Include="Spectre.Console" Version="0.57.2" />
  </ItemGroup>
  <ItemGroup Label="Observability">
    <PackageVersion Include="OpenTelemetry" Version="1.18.0" />
    <PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.18.0" />
  </ItemGroup>
  <ItemGroup Label="Sommelier">
    <PackageVersion Include="BaGetter.Core" Version="1.6.5" />
    <PackageVersion Include="BaGetter.Web" Version="1.6.5" />
    <PackageVersion Include="Aspire.Hosting.AppHost" Version="13.5.3" />
  </ItemGroup>
  <ItemGroup Label="Tests">
    <PackageVersion Include="xunit.v3" Version="4.0.0" />
    <PackageVersion Include="Microsoft.Testing.Platform" Version="2.4.0" />
    <PackageVersion Include="Microsoft.Testing.Extensions.HangDump" Version="2.4.0" />
    <PackageVersion Include="Microsoft.Testing.Extensions.CrashDump" Version="2.4.0" />
    <PackageVersion Include="Verify.XunitV3" Version="32.0.0" />
    <PackageVersion Include="NSubstitute" Version="6.2.0" />
    <PackageVersion Include="BenchmarkDotNet" Version="0.15.8" />
  </ItemGroup>
</Project>
```

---

## 7. Prerelease exposure and fallback triggers

| Dependency | Exposure | Fallback trigger | Fallback |
|---|---|---|---|
| MAF hosting / A2A / Mcp previews | Hosts and transport plugins only | Breaking change blocks a release | Hand-written ASP.NET endpoint over `AIAgent`; MCP SDK direct |
| Compaction (`MAAI001`) | One options class in Core.Agent | API churn | `SummarizingChatReducer` from MEAI or a Cupcake strategy over `ChatHistoryProvider` |
| Routing (`MEAI001`) | One class in Core.Agent | API churn | Keyed `IChatClient` resolution in the catalog service |
| `Xcaciv.Loader` AGPL | Lit, Funfetti | Hosted mode ships, or licence review fails | Hand-rolled ALC (~80 lines), lose hash store |
| `Terminal.Gui` 2.x | Lit, Funfetti | Windows conhost inline rendering fails testing | Spectre-primary REPL with Terminal.Gui episodes |
| `Spectre.Console` 0.x | All | Breaking 0.x release | Pin; `Spectre.Console.Ansi` + own widgets |
| `xunit.v3` 4.0 | Tests | Runner instability | 3.2.2 on MTP v1 |
| `BaGetter` 1.6.5 on net9 | Sommelier | No net10 release by Sommelier's first cut | Fork; it is MIT and small |
| ACP community packages | Transports.Acp | Abandonment | Own protocol types over `StreamJsonRpc` |

---

## 8. Open items to confirm before committing

1. Current `Xcaciv.Command` release on the `github` package feed, and whether `AskOrExecute()` lands there or in a Cupcake-side extension interface.
2. Whether `Xcaciv.Loader` genuinely uses runtime `AssemblyLoadContext` loading (chatdbg flagged this as unconfirmed). It decides trimming for Lit and Funfetti.
3. AGPL review for `Xcaciv.Command` and `Xcaciv.Loader` given Sommelier's hosted mode.
4. Terminal.Gui `AppModel.Inline` on Windows conhost and legacy terminals.
5. Whether the official `Anthropic` SDK exposes an MEAI `IChatClient` directly, which would make `Microsoft.Agents.AI.Anthropic` unnecessary.
6. BaGetter's net10 upgrade timeline and its fitness for public registration and moderation.
7. MassTransit v9 licence versus WolverineFx, when ESB transport is scheduled.
8. MAF session serialization with pending approvals (agent-framework #2365, #5189) on each MAF upgrade.
