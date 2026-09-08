# ChatDbg Rebuild — Target .NET Technology Stack

> Researched **August 2026**. Every recommendation carries a status and a fallback.

---

## 1. How to read this document

**Status vocabulary.** Every pick in §2 carries exactly one of these:

| Status | Meaning |
|---|---|
| **GA** | Stable release, 1.0 or higher, actively serviced. Safe to build on. |
| **GA (0.x)** | Stable-quality and actively maintained, but the version number is below 1.0 and the maintainers reserve the right to break the API in a minor bump. Pin the exact version; never float. `Spectre.Console`, `LLamaSharp` and `dotnet-stryker`'s MTP runner all live here. |
| **Preview** | Explicitly labelled preview or prerelease by its own publisher. Not shipped in the recommended stack unless a row says otherwise. |
| **FIXED** | Not a choice. `Xcaciv.Cupcake`, `Xcaciv.Command` and `Xcaciv.Loader` are constraints handed to this design. My job was to choose everything around them and to name what they make hard. |
| **Avoid** | Present in the landscape, deliberately rejected, and named so nobody re-proposes it. |

**UNCONFIRMED** means a researcher could not verify the claim against a primary source on the live web on 2026-08-28. It is not a soft "probably true". Where a decision rests on an UNCONFIRMED fact, the fact is repeated at the decision and again in §8, with the experiment that would settle it. I have not upgraded any researcher's UNCONFIRMED to a confident claim; where I had to decide anyway, I say the decision was mine and what it is provisional on.

**Where the research contradicted itself**, §3 and §4 resolve it explicitly and name the two sources. There are six such contradictions; they are collected in §8 as R-items so none is lost.

**A note on a missing input.** The brief expected a completeness critique at `output/chatdbg/research/_completeness-critique.md`. **That file does not exist.** No adversarial pass over the nine research areas was available to me. Every gap named in §8 was found by me while reconciling the nine files against `tech-stack-observed.md`, which means §8 is almost certainly less complete than it would be with the critique in hand. Treat §8 as a floor, not a ceiling, and re-run the critique before this document is used to plan work.

**Three constraints that fall out of the fixed stack, stated once here because they govern half the table:**

1. `Xcaciv.Loader` loads plugin assemblies at runtime. That forecloses Native AOT and trimming for the shell process (§5).
2. `Xcaciv.Command`'s `ICommandDelegate.Main(IIoContext, IEnvironmentContext)` carries **no `CancellationToken`**, and everything crossing that boundary is a `string`. Both facts shape the async design and the presentation layer.
3. `Xcaciv.Command`, `Xcaciv.Loader` and `Xcaciv.Cupcake` are **not on nuget.org** — verified 404 by four separate researchers against `api.nuget.org`. They come from `https://nuget.pkg.github.com/xcaciv/` (401 anonymously) or a local folder. Restore is not reproducible without a credential. This is a build-infrastructure prerequisite, not a footnote.

---

## 2. The stack at a glance

| Concern | Recommended | Version | Status | Second choice | Why the second choice would win |
|---|---|---|---|---|---|
| **Target framework** | `net10.0` | 10.0.11 runtime / 10.0.1xx SDK | **GA** (LTS to 2028-11-14) | `net11.0` | .NET 11 GAs 2026-11-10 with Zstandard in `System.IO.Compression` and Podman multi-arch container publish. It is STS (24 mo). Revisit Dec 2026. |
| **Host** | `Microsoft.Extensions.Hosting`, `Host.CreateApplicationBuilder`, REPL as a `BackgroundService`, custom `IHostLifetime` | 10.0.11 | **GA** | Cupcake-style hand-rolled `Loop` with no host | If the ~5 extra assemblies and the DI indirection are judged worse than losing graceful shutdown, `ValidateOnBuild`, and options binding. They are not. |
| **Dependency injection** | `Microsoft.Extensions.DependencyInjection`, **keyed singletons** per backend behind an `IChatBackendResolver` façade | 10.0.11 | **GA** | Hand-rolled `Dictionary<ProviderKey, IBackend>` (what the source does) | Only if the container's startup cost or AOT posture ever mattered. Neither does once AOT is off. |
| **Configuration** | `Microsoft.Extensions.Configuration` + `.Json`/`.EnvironmentVariables`/`.CommandLine`, `Microsoft.Extensions.Options` with `[OptionsValidator]` and `EnableConfigurationBindingGenerator` | 10.0.11 | **GA** | Bespoke `ISettingsService` over raw STJ (the source's design) | Never. The source's version is the origin of three of its worst defects. |
| **Secret storage** | **No stored secret by default** — `ChainedTokenCredential` (`Azure.Identity`) and the AWS SDK default chain; for keys that must persist, `Microsoft.Identity.Client.Extensions.Msal` `Storage` as a single encrypted blob | Azure.Identity 1.21.0 · MSAL.Extensions 4.88.0 · AWSSDK.SSO/SSOOIDC/Signin 4.0.10x | **GA** | Hand-rolled `ISecretStore` over three `[LibraryImport]` backends (WinCred / libsecret / Security.framework), `Meziantou.Framework.Win32.CredentialManager` 3.0.1 on Windows | Wins if binary size is hard-capped, if a security review objects to an auth library in a chat tool's graph, or **if per-key visibility in the OS credential UI must be preserved** — MSAL writes a DPAPI file, not a Credential Manager entry, which is a user-visible regression from the source. |
| **LLM client abstraction** | **Your own `ITokenIntrospectingBackend` port**, with `Microsoft.Extensions.AI` `IChatClient` used underneath for the plain-chat and tool-invocation path only | MEAI 10.9.0 | **GA** | Pure `IChatClient` with logprobs tunnelled through `RawRepresentation` | Wins if introspection turns out to be optional and the middleware ecosystem (function invocation, caching, OTel) is worth more than typed logprobs. It is not — see §4. |
| **Hosted-provider clients** | `OpenAI` official SDK against **both** api.openai.com and Azure `https://{res}.openai.azure.com/openai/v1/`; `AWSSDK.BedrockRuntime` with hand-written `InvokeModel` bodies; `AWS.Bedrock.MEAI` for the Converse chat path | OpenAI 2.12.0 (pinned by MEAI.OpenAI) · AWSSDK.BedrockRuntime 4.0.101.4 · AWS.Bedrock.MEAI 1.0.0 | **GA** | `Azure.AI.OpenAI` 2.1.0 | **Never.** Latest stable is 2024-12-06 — 20 months stale while the beta line ships. Microsoft's own current Azure guidance no longer mentions it. |
| **Local inference** | `LLamaSharp` **low-level path** — `BatchedExecutor` + `Conversation.Prompt(tokens, allLogits: true)` + `LLamaTokenDataArray.Softmax()` | 0.27.0 (+ `.Backend.Cpu` 0.27.0; Vulkan/CUDA12 RID-gated) | **GA (0.x)** | Supervised `llama-server` subprocess over HTTP (`n_probs`, `post_sampling_probs`) | Wins the moment a native crash losing the user's conversation is judged worse than losing prompt-token logits, full-vocab maps and custom sampler stages. Also wins if you need llama.cpp newer than the April-2026 commit LLamaSharp pins. |
| **Tokenization** | `Microsoft.ML.Tokenizers` for remote models; the **GGUF-embedded vocabulary** (`SafeLlamaModelHandle.Tokenize`/`TokenToSpan`) for local | 2.0.0 | **GA** | `Tokenizers.DotNet` 1.4.1 | Wins the moment byte-exact parity with an arbitrary HuggingFace `tokenizer.json` is required — `Microsoft.ML.Tokenizers` has **no `tokenizer.json` loader**. Costs a Rust native dependency on two platforms. |
| **Terminal UI (interaction model)** | **Spectre-primary REPL**; `Terminal.Gui` v2 as an *optional* second front end for bounded episodes, bridged by `Terminal.Gui.Interop.Spectre` | Terminal.Gui 2.4.17 · Interop 2.4.17 | **GA** | Terminal.Gui-primary full-screen TUI | Wins if the product vision is "an IDE in the terminal". Costs: a redirected stdout produces a **literally empty file** (measured — Terminal.Gui wrote 0 bytes), so you need a separate non-interactive entry point. |
| **Rich rendering** | `Spectre.Console` — every view is a pure function returning `IRenderable` | 0.57.2 | **GA (0.x)** | `Spectre.Console.Ansi` 0.57.2 + hand-rolled widgets | Wins only if Spectre's 0.x churn becomes intolerable; `Ansi` is zero-dependency and stable-shaped. You lose `Table`/`Grid`/`BarChart` measurement, which is most of the value. |
| **Serialization** | `System.Text.Json` with a **source-generated `JsonSerializerContext`** and `JsonSerializerIsReflectionEnabledByDefault=false` | in-box 10.0.x | **GA** | Reflection-based STJ (the source's design) | Never. Even untrimmed, source generation is what makes the persistence contract compile-time-checked and makes plugin-contributed types composable via `TypeInfoResolverChain`. |
| **Resilience** | `Microsoft.Extensions.Http.Resilience` over Polly, with an **explicit `AddResilienceHandler("llm", …)` pipeline** — never `AddStandardResilienceHandler()` | MEHR 10.9.0 · Polly 8.7.0 | **GA** | Hand-rolled retry with `Retry-After` parsing | Never — `HttpRetryStrategyOptions.ShouldRetryAfterHeader` already honours `Retry-After` correctly, and reimplementing it doubles the wait. |
| **Logging** | `Microsoft.Extensions.Logging` with `[LoggerMessage]` source generators + a ~150-line NDJSON rolling-file `ILoggerProvider` | 10.0.11 | **GA** | `Serilog` 4.4.0 + `Serilog.Sinks.File` behind `Serilog.Extensions.Logging` | Wins if you want rolling-file policy, retention and `{@Object}` destructuring without writing them, or if operators already run Seq. Costs ~4 assemblies and a second config system. |
| **Telemetry** | One `ActivitySource` + one `Meter` named `Xcaciv.ChatDbg`, **always instrumented, never exported** unless the user passes a flag; `OpenTelemetry` SDK referenced but unconfigured; MEAI `UseOpenTelemetry(sourceName:)` for the chat path | OpenTelemetry 1.18.0 | **GA** (conventions are **Development**) | Hand-rolled `ActivitySource`/`Meter` in each backend adapter | Wins if the three backends do *not* all route through `IChatClient` — and the local introspection path genuinely does not. You lose free GenAI spec conformance and the four histograms. |
| **Tool/plugin framework** | `Xcaciv.Command` for in-session commands (**FIXED**), bridged to MEAI `AIFunction` for model-callable tools; **`ModelContextProtocol`** as the only route for third-party tools | Xcaciv.Command 3.3.x · MCP 2.2.0 | **FIXED** / **GA** | `StreamJsonRpc` 2.25.29 for the out-of-process transport | Wins if every tool is yours, per-call overhead dominates, and MCP's schema ceremony and nine-releases-in-six-months cadence are pure cost. You lose the entire MCP ecosystem. |
| **Dynamic loading** | `Xcaciv.Loader`, **one cached `AssemblyContext` per package for the session** (not per invocation), contract assembly pinned to the Default ALC | 2.1.2 | **FIXED** | Hand-rolled `AssemblyLoadContext` + `AssemblyDependencyResolver` (~80 lines), or `McMaster.NETCore.Plugins` 2.0.0 | Would win on licence grounds (Xcaciv.Loader is AGPL-3.0) or if the unpublished-package problem is not solved. McMaster has had no release since 2025-01-05. |
| **Testing framework** | `xunit.v3` on **Microsoft.Testing.Platform v2** | xunit.v3 4.0.0 · MTP 2.3.3 | **GA** (4.0.0 is ~2 weeks old) | `xunit.v3` 3.2.2 (MTP v1), or MSTest 4.3.3 | 3.2.2 is the documented rollback if 4.0's runner-stack rewrite proves unstable — note the rollback also flips you to MTP v1. MSTest wins only as a risk-aversion play. |
| **Mocking** | **Hand-rolled fakes by default**; `NSubstitute` only for wide third-party interfaces you don't own | NSubstitute 6.2.0 | **GA** | `FakeItEasy` 9.0.1 | Wins if you prefer strict verification and dislike NSubstitute's extension-method-on-any-object syntax. `Moq` is **Avoid** — no NuGet release since 2024-09-07, and the source pins the SponsorLink-era 4.20.69. |
| **Snapshot testing** | `Verify.XunitV3` **pinned at 32.0.0** (released 2026-08-26, before the 1 Sept maintenance-fee cut-off) | 32.0.0 | **GA** | ~40-line hand-rolled golden-file helper | Wins if you refuse both the fee and the pin. You lose scrubbers, `DiffEngine` integration and parameterised snapshot naming — small, because every snapshot here is a block of ANSI text. |
| **Benchmarking** | `BenchmarkDotNet`, separate `benchmarks/` Exe project, `RuntimeMoniker.Net10` job | 0.15.8 | **GA** (last stable 2025-11-30) | Not benchmarking | Not an option. Softmax over a 128k vocabulary runs once per generated token; it is the product's hot path. |
| **Publish mode** | **Self-contained + single-file + ReadyToRun + untrimmed**, one artifact per RID, plugins and llama.cpp natives **outside** the bundle | — | **GA** | Multi-RID `dotnet tool` package (self-contained flavour + `any` fallback) | Not a replacement — a *second* channel. `dnx chatdbg` for SDK users; the GitHub Release binary for everyone else. Publish both. |
| **Containerization** | SDK container publish (`dotnet publish /t:PublishContainer`), OCI format, **non-chiseled** base | .NET 10 SDK | **GA** | Hand-written Dockerfile | Wins only if you need a base image or layer layout the SDK properties cannot express. Container is a *secondary* channel: this is an interactive TTY app that reads user-profile config. |
| **CI** | GitHub Actions, matrix over RIDs, SDK pinned by a **valid** `global.json`, `dotnet test` on MTP, sign Windows assets before bundling | — | **GA** | Azure DevOps YAML | Wins if the org standardises there. Note MTP requires `DotNetCoreCLI@2`, **not** `VSTest@3`. |

---

## 3. Decisions, one per concern

Each subsection: what the app needs it for (tied to a concrete ChatDbg operation) · options considered · the decision · reasoning · what is given up · the fallback trigger · UNCONFIRMED caveat.

### 3.1 Target framework — `net10.0`

**Needed for.** Everything. The source already targets `net10.0` in all four projects, so this is a confirmation, not a migration.

**Options.** `net10.0` (LTS, GA 2025-11-11, supported to 2028-11-14); `net11.0` (STS, preview.7 as of 2026-08-11, GA expected 2026-11-10); `net9.0`/`net8.0` (both **end of support 2026-11-10** — under three months away).

**Decision. `net10.0`.** SDK band `10.0.1xx` GA, pinned in a **valid** `global.json` with `rollForward: latestFeature`.

**Reasoning.** Three-year LTS; every package in this document supports net8.0+ and nothing blocks .NET 10; `Microsoft.NETCore.App` 10.0.11 is the current servicing release. .NET 11's genuine wins for this app — Zstandard in `System.IO.Compression`, `Microsoft.Extensions.Diagnostics` Activity tracing *rules*, Podman multi-arch container publish, and `ConfigurationIgnoreAttribute` — are real but do not justify shipping on STS preview.

**Given up.** `ConfigurationIgnoreAttribute` (§3.4 wanted it as the bind-direction mirror of `[JsonIgnore]`) and `JsonNamingPolicy.PascalCase`, both .NET 11 APIs. Design without them.

**Fallback trigger.** Revisit December 2026, one month after .NET 11 GA, and only if a named .NET 11 feature is on the critical path.

**UNCONFIRMED.** The .NET 11 GA date of 2026-11-10 is reported consistently by secondary sources but was not confirmed against `dotnet/core/releases.md` by either the packaging or the testing researcher. Immaterial to the decision.

**Carried forward from the source, must be fixed:** `global.json` in the subject repo is **syntactically invalid JSON** — a stray `}` at byte 95 — and pins an RC SDK (`10.0.100-rc.1.25451.107`). The CI workflow installs `9.0.x`. Neither survives.

### 3.2 Host — Generic Host, REPL as `BackgroundService`, custom `IHostLifetime`

**Needed for.** The read-eval-print loop itself; ordered startup and shutdown so that a `/exit` or a Ctrl+C actually flushes chat history and settings to disk before the process dies.

**Options.** `Host.CreateApplicationBuilder(args)` (`IHostApplicationBuilder`, "recommended for new projects" per the current Generic Host doc); the legacy `Host.CreateDefaultBuilder`; no host at all, which is literally what `Xcaciv.Cupcake` does today.

**Decision. `Host.CreateApplicationBuilder(args)`, `Microsoft.Extensions.Hosting` 10.0.11.** The REPL is one `BackgroundService`. A custom `ReplLifetime : IHostLifetime` owns SIGINT.

**Reasoning.** You get content root, config chain, logging providers, and — the one that matters — **scope validation and `ValidateOnBuild` in Development**, which is what catches a singleton REPL capturing a scoped `HttpClient`. The custom lifetime is not optional: `ConsoleLifetime` maps SIGINT unconditionally to "stop the host", but in a chat shell **the first Ctrl+C during a generation must cancel that generation and return to the prompt**; only a Ctrl+C at an idle prompt should quit. The doc names the seam — `IHostLifetime`, where "the last implementation registered is used" — so register your own and own the `PosixSignalRegistration`.

**Given up.** ~5 assemblies and a few ms of startup, invisible next to loading a multi-GB GGUF. And the greppable simplicity of `new ChatShell()`: with DI, "where does this instance come from" becomes a registration lookup.

**Fallback trigger.** None credible. If the host is dropped, you have re-created the source's defect list.

**Sharp edge that is not optional to solve.** `Console.ReadLine` is not cancellable — `TextReader.ReadLineAsync(CancellationToken)` does not take effect on stdin until a newline is actually typed ([dotnet/runtime#100308](https://github.com/dotnet/runtime/issues/100308)). After Ctrl+C the `BackgroundService` will sit inside the read until the user presses Enter, and `HostOptions.ShutdownTimeout` (default 30 s; set it to 10) may expire first. Mitigate with a dedicated read thread and `Task.WhenAny(readTask, cancelTask)`, accepting that the abandoned read completes later and is discarded. Spectre's prompts inherit the same limitation. **Budget for this — it is how you get "Ctrl+C doesn't work" bug reports.**

**"Patterned on Xcaciv.Cupcake" is a trap here.** Cupcake's real entry point (`src/Xcaciv.Cupcake.Lit/Program.cs`) does `new Loop()`, `RunWithDefaults()`, and `Environment.Exit(1)` in the catch — and `src/Xcaciv.Cupcake/Xcaciv.Cupcake.csproj` targets **net8.0**. `Environment.Exit` raises `ProcessExit` and terminates without running `finally` blocks, so an in-flight history save is lost. **Follow Cupcake's *shape* — a `Loop` driving an `ICommandController` over an `IIoContext` — and supply the host yourself. Never call `Environment.Exit`; call `IHostApplicationLifetime.StopApplication()`.**

**UNCONFIRMED.** `Xcaciv.Cupcake.Core.Loop`'s actual API. The researcher was GitHub rate-limited before reading `src/Xcaciv.Cupcake.Core/`; only `Controller` (property) and `RunWithDefaults()` are known, from the call site.

### 3.3 Dependency injection — keyed singletons behind a resolver façade

**Needed for.** `/model bedrock` at the prompt. The source implements this as a `Dictionary<string, IAIService>` duplicated in three places (`ChatShell.cs:18` and `:29`, `Shell.Gui/Program.cs:22`, `ChatWindow.cs:17` and `:51`).

**Options.** `Microsoft.Extensions.DependencyInjection` keyed services; a hand-rolled registry; Scrutor-style assembly scanning.

**Decision. `AddKeyedSingleton<IChatBackend, …>(ProviderKey.Azure | .Bedrock | .Local)`, resolved at runtime through a one-method `IChatBackendResolver`.**

```csharp
sealed class ChatBackendResolver(IServiceProvider sp) : IChatBackendResolver
{
    public IChatBackend Get(ProviderKey key) => sp.GetRequiredKeyedService<IChatBackend>(key);
    public IEnumerable<IChatBackend> All() => sp.GetKeyedServices<IChatBackend>(KeyedService.AnyKey);
}
```

**Reasoning.** The key is chosen at runtime, so this is a `GetRequiredKeyedService` call, not an injected `[FromKeyedServices]` parameter. The façade keeps `IServiceProvider` out of the REPL and makes `/model` unit-testable without a container. **Use a typed key** (`readonly record struct ProviderKey` or an enum) — keyed DI has no compile-time key checking and a typo is a runtime `InvalidOperationException`.

Two .NET 10 specifics that decide the shape:
- **`KeyedService.AnyKey` semantics changed in .NET 10 — a documented breaking change.** `GetKeyedService()` (singular) with `AnyKey` now **throws**; `GetKeyedServices()` (plural) with `AnyKey` returns only specifically-keyed registrations. The plural form is exactly what `/model` with no argument wants, and it now returns the right thing. The singular form is a trap.
- **The local GGUF backend must use the factory overload** — `AddKeyedSingleton<IChatBackend>(key, (sp, key) => new LlamaBackend(...))` — so multi-gigabyte weights load on first resolve, not at host build. Dispose via `IAsyncDisposable`, which the host honours through container disposal.

`Xcaciv.Command` already speaks this language: `AddXcacivCommand(IServiceCollection)` (and an `IConfiguration` overload that binds `CommandControllerOptions`/`PipelineOptions` by section) registers `ICommandRegistry`, `ICommandFactory`, `ICommandExecutor`, `IPipelineExecutor`, `ICommandLoader`, `ICrawler`, `IVerifiedSourceDirectories`, `IAuditLogger`, `IOutputEncoder`, `IHelpService`, `ICommandController` — all singletons, all via `TryAdd*`, so your own registration wins if you register first.

**Given up.** Compile-time key checking. Explicit `AddSingleton<TService, TImpl>()` everywhere instead of assembly scanning (which you want anyway — see §3.22).

**Fallback trigger.** None.

**Do not** take the global `ConfigureHttpClientDefaults(b => b.AddAsKeyed())` shortcut. The docs' own "Beware of 'Unknown' clients" note explains that it becomes an `AnyKey` registration, container validation stops applying, and *"an erroneous key value silently leads to a wrong instance being injected"* — a silently unconfigured `HttpClient` pointed at nothing.

### 3.4 Configuration — `Microsoft.Extensions.Configuration` + validated Options

**Needed for.** `/set temperature 0.8`, `~/.chatdbg/settings.json`, per-project `.chatdbg.json` overrides, `CHATDBG_*` environment variables, and startup flags.

**Options.** The Microsoft.Extensions stack; the source's bespoke `SettingsService` over raw `System.Text.Json`.

**Decision. `Microsoft.Extensions.Configuration` 10.0.11 with an explicit source chain, bound to one validated `ChatDbgOptions` via `AddOptionsWithValidateOnStart<T>()`, with `EnableConfigurationBindingGenerator` on.**

Precedence, lowest wins to highest wins:

| # | Source | Hot reload | Note |
|---|---|---|---|
| 1 | Compiled-in defaults (`AddInMemoryCollection`) | no | Never empty, never null |
| 2 | `$(AppContext.BaseDirectory)/appsettings.json` | no | Optional; an admin ships an org default beside the binary |
| 3 | `<config-dir>/settings.json` | **yes** | The file `/set` writes. camelCase, `WriteIndented` |
| 4 | `./.chatdbg.json` in the working directory | **yes** | Per-project overrides — natural for a debugging tool |
| 5 | `CHATDBG_*` environment variables (`__` → `:`) | no | Session/CI scoping |
| 6 | Command-line arguments | no | This invocation only |

**Reasoning.** `Host.CreateApplicationBuilder` gives you most of this; clear `Configuration.Sources` and rebuild it explicitly so the per-user and per-project files land in the right order. `AddOptionsWithValidateOnStart` means a bad settings file fails at process start with a readable message rather than eight seconds into the user's first prompt.

Three mechanics that will otherwise bite:
- **Hierarchy separator.** Keys nest with `:`; bash cannot put `:` in an env var name, so all platforms accept `__`. `CHATDBG_Llama__ContextSize=8192` binds `Llama:ContextSize`. Document the double underscore.
- **Recursive validation is opt-in.** DataAnnotations does not descend into nested objects or collections. You need `[ValidateObjectMembers]` on the nested `LlamaOptions` and `[ValidateEnumeratedItems]` on the named-prompt collection. Easy to miss; silently validates nothing.
- **Hot reload is file-provider only**, and unreliable on network shares, WSL2 and containers (documented workaround: `DOTNET_USE_POLLING_FILE_WATCHER=1`, which polls every four seconds, non-configurable).

**Deliberate restriction: hot-reload the display and generation knobs only.** `enableLogProbabilities`, `logProbabilitiesTopK`, `showAllTokens`, `gridViewForTokens`, `temperature` — yes. `provider`, `modelId`, `llamaModelPath`, `llamaContextSize` — **no**. Swapping the loaded GGUF out from under an in-flight completion is a bug generator. Use `IOptionsMonitor<T>` for the first set only. `IOptionsSnapshot<T>` is scoped and therefore near-useless in a console app with no request scope; ignore it.

**Where files go** (the source uses three different roots for three artefacts of the same app — `UserProfile`, `LocalApplicationData`, `ApplicationData`):

| Content | Windows | Linux | macOS |
|---|---|---|---|
| `settings.json` | `%APPDATA%\ChatDbg\` | `${XDG_CONFIG_HOME:-~/.config}/chatdbg/` | `~/Library/Application Support/ChatDbg/` |
| Encrypted secret blob | `%LOCALAPPDATA%\ChatDbg\` | `${XDG_DATA_HOME:-~/.local/share}/chatdbg/` | Keychain; file only as fallback |
| History, named prompts | `%LOCALAPPDATA%\ChatDbg\` | `${XDG_DATA_HOME:-~/.local/share}/chatdbg/` | `~/Library/Application Support/ChatDbg/` |
| Tokenizer/vocab caches | `%LOCALAPPDATA%\ChatDbg\cache\` | `${XDG_CACHE_HOME:-~/.cache}/chatdbg/` | `~/Library/Caches/ChatDbg/` |

The secret blob belongs in `LocalApplicationData`, not `ApplicationData`: `%APPDATA%` roams to the domain profile, and DPAPI-`CurrentUser` ciphertext travelling to another machine is at best fragile. No `SpecialFolder` maps to `$XDG_CACHE_HOME` — read the env var yourself. Provide a `CHATDBG_CONFIG_DIR` override for portable installs, CI, and hermetic tests. **Never fall back to `Path.GetTempPath()`** — the source does, silently, writing a credential-bearing file into a world-readable `/tmp`.

**Migration obligation.** Existing users have `~/.ChatDbg/settings.json`. Read it, migrate, then **overwrite the key fields with empty strings, save, re-chmod**, and print: *"N credentials migrated. The plaintext copies in `<path>` have been cleared. **Rotate these keys** — they have been on disk in plaintext."* Rotation advice is mandatory; a key that sat in a world-readable file is disclosed.

**Given up.** The source's single flat settings file. And `ConfigurationIgnoreAttribute`, which is .NET 11.

**Fallback trigger.** None.

### 3.5 Secret storage — store nothing by default; MSAL `Storage` for what must persist

**Needed for.** Authenticating the Azure OpenAI data-plane call on every turn, and the Bedrock `InvokeModel` call. Also: not being the tool that leaks a key into terminal scrollback, which is what the source does.

**Options.** Windows Credential Manager P/Invoke (the source's design, Windows-only); `System.Security.Cryptography.ProtectedData` (Windows-only, throws `PlatformNotSupportedException` elsewhere); `Microsoft.Identity.Client.Extensions.Msal` `Storage` (the only Microsoft-shipped DPAPI + Keychain + libsecret abstraction); `SIL.PasswordStore` (**preview**, ~900 lifetime downloads); `GnomeStack.Os.Secrets` (**abandoned**, 2023-12-06); `SecureStore` (solves the wrong problem — needs a key file); `Microsoft.Extensions.Configuration.UserSecrets` (**a plaintext JSON file; a development tool, not a store**); provider credential chains.

**Decision, in two parts.**

**(a) Prefer no stored secret at all.** Azure: an explicitly constructed `ChainedTokenCredential(new AzureCliCredential(), new AzureDeveloperCliCredential(), new DeviceCodeCredential())` — **not** bare `DefaultAzureCredential`. AWS: construct `AmazonBedrockRuntimeClient` with no explicit credentials and let the SDK chain resolve `aws sso login` / `aws login`, with **`AWSSDK.SSO` + `AWSSDK.SSOOIDC` + `AWSSDK.Signin` referenced** — AWS documents that *"failure to reference these packages will result in a runtime exception."* `AWSSDK.Signin` 4.0.101.8 shipped 2026-08-24 and did not exist in 2025-era knowledge.

**(b) For keys that genuinely must persist**, `Microsoft.Identity.Client.Extensions.Msal` 4.88.0 `Storage` as a single encrypted blob, gated on `Storage.VerifyPersistence()`.

Secret resolution precedence, first hit wins — note this **inverts** the source's order:

| # | Tier | Persisted? | Reported as |
|---|---|---|---|
| 0 | `--api-key` on the command line | never | `command-line (this session only)` |
| 1 | `CHATDBG_AZURE_API_KEY` env var | no | `environment variable (not persisted)` |
| 2 | **Provider credential chain** | no secret exists | `Azure CLI identity` / `AWS SSO profile <name>` |
| 3 | OS store via MSAL `Storage` | yes, encrypted | `OS keystore (DPAPI \| Keychain \| Secret Service)` |
| 4 | `0600` file, **explicit opt-in only** | yes, **plaintext** | `INSECURE FILE — not encrypted` |
| — | Settings file | **never, at any tier** | — |

**Reasoning.** Microsoft's own guidance is not to ship `DefaultAzureCredential`: nine credentials whose first three always fail on a laptop, `ManagedIdentityCredential` waiting on an IMDS endpoint that isn't there, and ambient env vars anyone can set machine-wide silently changing which identity the tool uses. `DeviceCodeCredential` is the piece that works over SSH — it prints a code and URL. MSAL's `Storage` is a genuinely general-purpose blob store (`ReadData()`/`WriteData(byte[])`/`Clear()`/`VerifyPersistence()`), and its `Accessors/FileWithPermissions.cs` already does the part hand-rolled stores get wrong: a `chmod`-at-`open(2)` so the file is never briefly world-readable, an `lstat(2)` symlink pre-check, and an explicit Windows `FileSecurity` ACL described in its own source as *"'600' mode … translates to this in Windows."*

**Defined behaviour when no secure store exists** — this is the case that decides the architecture, because a debugging shell runs over SSH, in containers, and in CI. Git Credential Manager, the most battle-tested cross-platform .NET credential consumer, documents Secret Service as *"⚠️ Requires a graphical user interface session."* You get one of: `DllNotFoundException` (`libsecret-1.so.0` absent — **and note self-contained publishing does not bundle native OS libraries**, so this is *more* likely for your binary, not less); D-Bus failure with no session bus; or a locked collection that cannot prompt. So:

1. Call `Storage.VerifyPersistence()`. Success → tier 3.
2. Failure → **write nothing.** Print the specific cause and three named options: `chatdbg auth login` (use the chain, store nothing — recommended), `export CHATDBG_AZURE_API_KEY=…` (session only), `chatdbg auth set-key --insecure-file`.
3. `--insecure-file` creates with `FileStreamOptions.UnixCreateMode = UserRead|UserWrite`, then `File.SetUnixFileMode` again (create-time mode is filtered by umask), records `credentialSource = "insecure-file"`, and prints a **persistent startup banner** for as long as that tier is in use.
4. **Never fall back automatically, silently, or to `Path.GetTempPath()`.**

**Structural rule that fixes the source's worst defect.** Make the settings type **incapable of holding a secret**. Two types, two files, two stores: `ChatDbgSettingsFile` has no secret-shaped member at all; `SecretBag` is only ever passed to `Storage.WriteData`/`ReadData` and is not registered in the settings `JsonSerializerContext`, so there is no code path that can put one in the other. Add a save-time entropy/known-prefix scan (`sk-`, `AKIA`, `ASIA`, a 32+ char base64url run) that **fails the write**. Ten lines, unit-testable, and it catches the exact regression the source has.

**Given up, and it is user-visible.** MSAL writes a **DPAPI-encrypted file on Windows, not a Credential Manager entry.** Users lose the ability to see and revoke the credential via `control keymgr.dll` / `cmdkey /list` — a capability the source's users have today. You also pull all of `Microsoft.Identity.Client` (a full OAuth2/OIDC public client you will not call) into a self-contained binary, and any reviewer will ask why a chat tool depends on an auth library. Wrap it behind your own `ISecretStore` and comment the choice, or the next maintainer will "clean it up".

Separately, the chain path gives up a real persona: **a developer who was handed an API key by a colleague and has no Entra/IAM identity of their own cannot self-serve.** Hence: chain first, key path documented and supported, never the easy one.

**Fallback trigger.** Adopt the hand-rolled three-backend `ISecretStore` if (a) binary size is hard-capped, (b) per-key OS-credential-UI visibility is required, or (c) a security review objects to MSAL in the graph. Budget it as real work — you inherit chmod-before-write ordering, `lstat` symlink checks, Windows ACL construction, D-Bus detection, musl `DllNotFoundException` handling, unmanaged-buffer zeroing and cross-process locking.

**Contract to preserve from the source** (it got these right): the three stable target names `ChatDbg:AzureApiKey` / `ChatDbg:AwsAccessKey` / `ChatDbg:AwsSecretKey`; the env-var names **including the AWS-standard fallbacks** `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY`; and `GetCredentialSource()` — provenance reporting, which should be promoted to a first-class `/status` output rather than a diagnostic afterthought.

**UNCONFIRMED.** (i) Native-AOT compatibility of MSAL — no authoritative statement found; **moot**, since AOT is off (§5). (ii) Precise roaming-profile DPAPI master-key behaviour on current Windows Server / Entra-joined configurations — sidestepped by choosing `%LOCALAPPDATA%`. (iii) Whether the AWS SDK v4 `SharedCredentialsFile` writer applies restrictive permissions — assume not; do not write that file yourself.

### 3.6 LLM client abstraction — your own port, MEAI underneath

**Needed for.** Streaming a turn to the terminal from any of three backends *and* carrying per-token log probabilities, top-K alternatives, residual mass and entropy-exactness alongside it.

**Options.** (a) Code against `Microsoft.Extensions.AI` `IChatClient` and recover logprobs through `RawRepresentation` downcasts. (b) Define your own `ITokenIntrospectingBackend` and implement it three times. (c) Microsoft Agent Framework (`Microsoft.Agents.AI` 1.19.0, GA). (d) Semantic Kernel 1.80.0.

**This is where the research contradicted itself, and I am resolving it.** `llm-abstraction.md` recommends coding against `IChatClient` with a parallel `ITokenTelemetryExtractor` channel. `token-introspection.md` says *"Do **not** route this feature through `Microsoft.Extensions.AI`… define your own `ITokenIntrospectingChatBackend`."* `local-inference.md` and `testing-quality.md` independently reach the same conclusion (`ITokenIntrospectingChatProvider`; `ILlmBackend` + `BackendCapabilities`). **Three of four say own-port. I am deciding for the own-port, and the decision was mine to make** — the researchers agreed on every fact and differed only on which layer to make primary.

**Decision. Your own `ITokenIntrospectingBackend` is the primary contract. `IChatClient` (MEAI 10.9.0) sits *underneath* it, used for the plain-chat path, for `AIFunction` tool invocation via `.UseFunctionInvocation()`, and for `UseOpenTelemetry()`. It is not the app's abstraction; it is one of its implementation details.**

```csharp
public interface ITokenIntrospectingBackend
{
    IntrospectionCapabilities Capabilities { get; }
    IAsyncEnumerable<TokenEvent> StreamAsync(ChatRequest r, IntrospectionOptions o, CancellationToken ct);
    TokenizationResult Tokenize(string text);
}

public sealed record IntrospectionCapabilities(
    bool OutputLogProbs, int MaxTopK, bool StreamingLogProbs,
    bool PromptLogProbs, bool FullVocabulary, bool TeacherForcedScoring,
    int? VocabularySize, string ProbeStatus);   // Verified | Assumed | Failed
```

**Reasoning.** `IChatClient` provably cannot carry the app's core datum. The full public surface of `ChatOptions`, `ChatResponse`, `ChatResponseUpdate` and `UsageDetails` in 10.9.0 was enumerated: **no logprob member on any of them**, and a grep of the whole `Microsoft.Extensions.AI.Abstractions` source tree for "logprob" returns no hits. A search of `dotnet/extensions` issues for "logprobs" returns two results, both closed, neither about logprobs. **Design as though typed support never lands.** Making an abstraction primary that structurally omits the product's differentiating feature means every logprob you display travels through an unchecked `object?` downcast — a runtime `null`, not a compile error, when a provider adapter changes what it stows.

Making capability a *declared value* rather than a `try/catch` is the second reason. It lets one table-driven contract suite assert correct degradation across all three backends (§3.18), and it lets the shell grey out `/attribute` with a message naming the missing capability instead of rendering an empty grid.

**But keep MEAI, because it is genuinely load-bearing for three things**: `FunctionInvokingChatClient` (tool calling, §3.16), `OpenTelemetryChatClient` (free GenAI semconv conformance, §3.15), and the fact that all five candidate backends already ship an `IChatClient` adapter, so the boring chat path costs nothing.

**Six facts that constrain the wiring, all verified by the researchers:**

1. **`ChatOptions.TopK` is a decoy.** Its own docs read *"the number of most probable tokens that the model considers when generating"* — that is top-k **sampling**. It is not `top_logprobs`, which is a *reporting* parameter. Setting `ChatOptions.TopK = 20` changes what the model generates and returns you nothing. In a token-introspection tool that is the worst combination of failure modes.
2. **`RawRepresentation` is `[JsonIgnore]`** on `ChatResponse` (`ChatResponse.cs:111`), `ChatResponseUpdate` (`:115`) and `ChatMessage` (`:99`). It never survives serialization — not through your history store, not through the response cache.
3. **`AdditionalProperties` is not `[JsonIgnore]`** and does serialise. It is the only carrier that survives. Project telemetry into it; on deserialisation values return as `JsonElement`, so define an explicit DTO with a `JsonSerializerContext`.
4. **Streaming composition is lossy and documented as such.** `ChatResponseUpdate`'s own remarks warn that *"multiple updates all have different `RawRepresentation` objects whereas there's only one slot… in `ChatResponse.RawRepresentation`."* Buffering a stream and calling `ToChatResponseAsync()` keeps one raw object and discards N−1. **Harvest per update.** This is a five-line mistake that produces a plausible-looking, mostly-empty heat map.
5. **Pipeline order is load-bearing, not stylistic.** `DistributedCachingChatClient` round-trips through `JsonSerializer` over `typeof(ChatResponse)`, so **every cache hit returns a response with no raw representation and therefore no logprobs**. `FunctionInvokingChatClient` performs multiple round-trips and collapses them to one `RawRepresentation` slot. Correct order:
   `UseLogging → UseOpenTelemetry → UseDistributedCache → UseFunctionInvocation → <your telemetry decorator> → <provider>`
6. **Request-side injection is sanctioned.** `ChatOptions.RawRepresentationFactory` is a `Func<IChatClient, object?>`; `OpenAIChatClient.ToOpenAIOptions` maps every property with `??=`, so **anything you set on the raw options object is preserved and the abstraction fills only gaps.**

**Rejected: Microsoft Agent Framework and Semantic Kernel.** The decisive argument is not "less is more" — it is that **agent frameworks are built to hide the model call, and this application's product *is* the model call.** You need byte-exact visibility into every round-trip and every alternative the sampler rejected. Adding a layer whose job is to normalise that away and then reaching around it is strictly worse than not adding it. SK additionally duplicates `Xcaciv.Command` + `Xcaciv.Loader` with a competing plugin registry, on a framework Microsoft has publicly put into succession. AF's `ChatClientAgent` wraps any `IChatClient`, so adopting it later is additive, not a rewrite — that is the fallback, not the start.

**Also rejected: MEAI's experimental `RoutingChatClient` / `FailoverChatClient` / `OrderedFailoverChatClient` and `SummarizingChatReducer`.** Silently failing over to a *different model* would invalidate every measurement, and a reducer that rewrites history is poison for a tool whose output must be reproducible. Both are marked experimental. Make backend selection explicit and user-visible.

**Given up.** A layer of indirection in a tool whose value is proximity to the wire; compile-time safety on the telemetry path; and you inherit MEAI's cadence — `Microsoft.Extensions.AI.OpenAI` 10.9.0 pins `OpenAI [2.12.0, 2.13.0)`, so **you cannot adopt OpenAI SDK 2.13.0 features until the adapter widens.** Currently a ~2.5-week lag.

**Fallback trigger.** Drop `IChatClient` entirely and write three implementations directly over `OpenAI`, `AWSSDK.BedrockRuntime` and `LLamaSharp` **if fewer than ~60% of your calls go through the abstraction without a downcast.** At that point it is pure overhead with a lossy-conversion hazard attached.

**Mandatory test.** One integration test per backend asserting `Extract()` returns a non-empty list against a live call, run on a schedule, not just on commit. The downcasts are unchecked; nothing else will tell you a provider adapter moved.

### 3.7 Hosted-provider clients — `OpenAI` for both, raw `InvokeModel` for Bedrock

**Needed for.** The Azure OpenAI and Amazon Bedrock backends, including the logprob request switch on the one that has it.

**Options.** `Azure.AI.OpenAI` 2.1.0; the official `OpenAI` SDK against the Azure `/openai/v1` endpoint; `Microsoft.Extensions.AI.AzureAIInference` (**preview, stalled ~9 months; the underlying beta SDK is slated for retirement 2026-08-26**); `AWS.Bedrock.MEAI` 1.0.0; `AWSSDK.Extensions.Bedrock.MEAI` (**deprecated — every version flagged**); raw `AWSSDK.BedrockRuntime`.

**Decision.**
- **One `OpenAI.Chat.ChatClient` type for both hosted OpenAI-shaped backends**, differing only in base URL and credential: `https://api.openai.com` vs `https://{res}.openai.azure.com/openai/v1/` with a `BearerTokenPolicy` over the chained credential, scope `https://ai.azure.com/.default`. `AsIChatClient()` gives you the MEAI view of the same instance.
- **Bedrock: `AWS.Bedrock.MEAI` 1.0.0 for the Converse chat path, and hand-written `InvokeModel` JSON for anything introspective.**

**Reasoning.** `Azure.AI.OpenAI`'s latest **stable is 2.1.0, published 2024-12-06** — twenty months, with ten prereleases since and the newest beta itself five months old. Microsoft's own current Azure .NET guidance (`supported-languages`, doc date 2026-07-20) does not mention the package at all; it says `dotnet add package OpenAI` + `Azure.Identity` and constructs a plain `ChatClient` against `/openai/v1/`. **Azure has converged onto the OpenAI SDK.** For this app that is unambiguously good: one client type, one options type, one logprob code path for both hosted backends.

For Bedrock the finding is blunt and was verified **by disassembly**: the researcher extracted `AWSSDK.BedrockRuntime.dll` 4.0.101.4 and scanned all 2,272 distinct ASCII strings ≥5 chars. Strings containing `logprob`, `LogProb` or `likelihood`: **zero**. The same scan on `AWS.Bedrock.MEAI` 1.0.0: zero. `Converse`/`ConverseStream` have no logprob field in either direction, and `inferenceConfig` accepts only `maxTokens`, `stopSequences`, `temperature`, `topP` — not even `topK`. So the MEAI adapter is a faithful Converse wrapper with working escape hatches that has nothing to put through them, and **any logprobs you get from Bedrock come out of a `MemoryStream` you parse yourself.** `InvokeModelRequest.Body` is an opaque `MemoryStream`, so nothing blocks you.

**Given up.** OpenAI SDK 2.13.0 features until MEAI's adapter widens its range (see §3.6). Any pretence that the three backends are equivalent — **Bedrock will always be the poor cousin**, and the honest thing is a capability badge in the UI, not papering over it.

**Fallback trigger.** If `AWS.Bedrock.MEAI` (1.0.0, published 2026-08-11, ~5,200 downloads, seventeen days old at research time) proves unstable, drop it and write the `IChatClient` wrapper over `AWSSDK.BedrockRuntime` yourself. **You lose the adapter, not the capability** — it carries no telemetry you would miss. Pin the exact version and expect churn.

**Double-retry hazard.** Both SDKs retry internally: `ClientRetryPolicy(maxRetries:)` on the System.ClientModel side, and AWS's `ClientConfig.RetryMode` (default `Legacy`) with `MaxErrorRetry` returning **4** under Legacy and 2 under Standard/Adaptive. Hand a resilience-wrapped `HttpClient` to either without disabling its own retries and you get the product — 3 × 4 = 12 attempts against a rate-limited endpoint, which is how you turn a 429 into a ban. **Set the SDK retry counts to zero and own retry in one place** (§3.13).

**UNCONFIRMED, and it is the single highest-value experiment in this document.** Whether **Bedrock's OpenAI-compatible endpoint** (`https://bedrock-runtime.{region}.amazonaws.com/openai/v1/chat/completions`, and the `bedrock-mantle` variant) honours `logprobs`/`top_logprobs` for `openai.gpt-oss-20b`/`120b` and the Qwen3 family. AWS documents the request body purely by reference to OpenAI's docs and enumerates no supported or unsupported fields. **If it works, one `OpenAI.Chat.ChatClient` gives you full OpenAI-shaped logprobs on Bedrock-hosted open-weights models through the same code path as OpenAI and Azure.** Thirty minutes to settle: point the client at that URL with `IncludeLogProbabilities = true` and see whether `ContentTokenLogProbabilities` comes back populated, empty, or 400s.

**Also UNCONFIRMED.** Whether Azure's `/openai/v1/` endpoint honours `IncludeLogProbabilities` for current GPT-5-family deployments. There is a documented history of Azure rejecting or silently ignoring `logprobs` on some API versions and model families ([Azure/azure-sdk-for-net#42340](https://github.com/Azure/azure-sdk-for-net/issues/42340)). **Verify against your actual deployment before promising the feature** — see the `probe` command in §4.

### 3.8 Local inference — LLamaSharp 0.27.0, low-level path

**Needed for.** `/tokenize`, `/inspect`, the vocabulary probability map, prompt perplexity, and occlusion attribution. This is where the app's marquee features live and it should be built first.

**Options.** `LLamaSharp` 0.27.0 (high-level `ChatSession`, mid-level `LLamaContext`, or low-level `BatchedExecutor`); `Microsoft.ML.OnnxRuntimeGenAI` 0.15.2 (**preview**); `OllamaSharp` 5.4.30 + Ollama server; a supervised `llama-server` subprocess; LM Studio; vLLM.

**Decision. `LLamaSharp` 0.27.0 (upgrade from the source's 0.25.0), using the low-level `BatchedExecutor` + `Conversation` API, loaded into the host's *default* `AssemblyLoadContext`.** `LLamaSharp.Backend.Cpu` 0.27.0 always; Vulkan and CUDA12 behind `Condition`-guarded `PackageReference`s and separate release assets.

```csharp
conversation.Prompt(tokens, allLogits: true);          // logits at EVERY prompt position
var arr = LLamaTokenDataArray.Create(conversation.Sample(offset));
arr.Softmax();                                          // full-vocab softmax, TensorPrimitives SIMD
```

**Reasoning.** `Conversation.Prompt(tokens, allLogits: true)` + `Sample(offset)` is **the only mechanism in the entire .NET ecosystem that gives you per-prompt-token distributions.** Not `llama-server`, not Ollama, not LM Studio, not ORT GenAI — only vLLM (Python, Linux, NVIDIA) has an equivalent. That single requirement is what makes LLamaSharp the answer, because prompt-token scoring is what "token attribution back to input spans" *is* (§4).

`LLamaTokenDataArray.Softmax()` sorts the whole `n_vocab` array descending and computes the softmax with `System.Numerics.Tensors.TensorPrimitives.SoftMax` over **all** ~128k–256k tokens. That normalisation is the correctness point.

**Concrete upgrade reasons for 0.25.0 → 0.27.0, not generic ones:** 0.26.0 added Blackwell `sm_120` to the CUDA-12 backend (the difference between "works on an RTX 50-series box" and "silently produces garbage"); 0.26.0 fixed a crash in grammar optimization when `TopK == 0` — **and an introspection tool wanting the unfiltered distribution is exactly the caller that sets `TopK = 0`**; 0.27.0 fixed musl RID detection (Alpine containers) and Linux `RUNPATH`/`$ORIGIN` resolution, which is directly relevant to single-file self-extract finding `libggml.so` next to `libllama.so`.

**Four defects in the source that must be *rewritten*, not ported.** `LLamaSharpService.ComputeTopKFromCurrentLogits` does `context.GetType().GetMethod("GetLogits", Type.EmptyTypes)` and `mi.Invoke(...)`, with `ResolveTokenStringSafe` doing `(dynamic)context → dyn.TokenToString(id)`:
1. **The reflection probe always returns empty.** `LLamaContext` has no public parameterless `GetLogits` — it is on `context.NativeHandle`. `mi` is `null`, the method returns an empty list, and the whole top-K feature is silently dead behind a `catch { }`.
2. **The softmax denominator is wrong.** Even if reflection worked, it exponentiates only the top-K logits and divides by their sum, yielding conditional-within-top-K probabilities that always total 1.0 — systematically inflating the top token and making the numbers incomparable across steps or against a hosted provider's logprobs.
3. **`OrderByDescending` over the full vocab per generated token** — an O(V log V) LINQ sort with an allocated index array, at every step.
4. **`dynamic` and `GetMethod` are trim- and AOT-hostile**, and `SuppressTrimAnalysisWarnings=true` is exactly the setting that turns that into a silent runtime failure rather than a build error.

**Two configuration traps to fix explicitly.** `GpuLayerCount` defaults to **20** — neither 0 (pure CPU) nor -1 (offload everything); on a small GPU it OOMs, on a big one it leaves most of the model on the CPU and the user concludes GPU acceleration is broken. **Set it explicitly and display the resolved value.** `ContextSize` defaults to null → `n_ctx = 0` → the model's trained length; display the resolved value or "why did my long conversation get truncated" is unanswerable.

**Backend selection fails in the field more than the docs suggest.** CUDA detection **never asks the driver** — `SystemInfo.GetCudaMajorVersion()` reads `CUDA_PATH`, then `CUDA_VERSION`, then `/usr/local/cuda`, then `LD_LIBRARY_PATH`, parsing `version.json`. A machine with a perfectly good NVIDIA driver and no CUDA *Toolkit* returns -1 and silently gets CPU inference at a tenth of the speed ([issue #990](https://github.com/SciSharp/LLamaSharp/issues/990), still open); the override is `.WithCuda().SkipCheck()`. Vulkan detection **spawns `vulkaninfo` as a child process** — no `vulkaninfo` on `PATH`, no Vulkan, plus startup latency and endpoint-protection heuristics on locked-down Windows. Only CUDA 11 and 12 are recognised; CUDA 13 is [open request #1360](https://github.com/SciSharp/LLamaSharp/issues/1360). **Expose `NativeLibraryConfig.All.DryRun(out var lib)` as a `/backend` or `/diag` command** — it reports which library *would* load without committing, and it is the single highest-value diagnostic available, for free.

**An ALC hazard specific to this rebuild.** `NativeApi`'s static constructor calls `NativeLibrary.SetDllImportResolver` on its own assembly and loads `libllama`. **A native library loaded from a collectible ALC pins that ALC and prevents unload** — so if the local backend ships as an `Xcaciv.Loader` plugin, `/unload` will appear to succeed while 4 GB of weights stay resident. Worse, two plugin loads would give two `LLamaSharp` assembly identities each registering a resolver against the same process. **Reference `LLamaSharp` from the host, expose it to `Xcaciv.Command` tools through a shared abstraction assembly, and keep it out of every plugin context.** The commands (`/tokenize`, `/inspect`) can live in plugins; the handle must not.

**Given up.** A `0.x` dependency with breaking changes between minors; native backend packages per platform/accelerator that fight the self-contained goal; **a hard four-and-a-half-month llama.cpp lag** — 0.27.0 binds commit `3f7c29d3` (2026-04-16) while upstream is at b10687 (2026-08-29), so any GGUF whose architecture landed after mid-April 2026 fails with `unknown model architecture`. **Print the bound commit in `/version` so that failure is diagnosable rather than mysterious**, and expose `NativeLibraryConfig.LLama.WithLibrary(path)` as the escape hatch. And: **every native crash is your crash.** An access violation or `abort()` inside `ggml` — CUDA OOM, malformed GGUF, KV-cache overflow — terminates the .NET process immediately; `try/catch` does not help, because `SIGSEGV` from native code is not a CLR exception. This is not theoretical ([#860](https://github.com/SciSharp/LLamaSharp/issues/860), [#1231](https://github.com/SciSharp/LLamaSharp/issues/1231), [#1091](https://github.com/SciSharp/LLamaSharp/issues/1091)). **Mitigation that costs nothing: persist conversation history to disk before every inference call, not after.**

**Fallback ladder for a machine with no working native backend** — four rungs, each cheap because the settings model maps 1:1 onto `llama-server` CLI flags (`-ngl`, `-c`, `-b`, `-ub`, `-t`, `--cache-type-k/v`, `-fa`):
1. **CPU `noavx`** — `WithAutoFallback(true)` already walks avx512 → avx2 → avx → noavx. Warn when the selected variant is below the CPU's actual capability, because that usually means a publish problem, not a hardware limit.
2. **User-supplied `WithLibrary(path)`** — also the escape hatch for a too-new GGUF.
3. **Out-of-process `llama-server`** — `n_probs` (no documented hard cap) for top-K, `post_sampling_probs` to compare raw vs. post-sampler distributions, `/tokenize?with_pieces=true` for the tokenization grid. **Degrade the UI explicitly:** grey out the full-probability-map and attribution views with a tooltip naming the in-process requirement. There is no well-maintained .NET package that supervises llama-server; budget ~200 lines for spawn, port selection, `/health` polling, stderr capture, graceful shutdown and orphan reaping.
4. **Ollama or LM Studio if already running** (`localhost:11434` / `:1234`). Top-20 alternatives per generated token only. A convenience rung, not a supported configuration.

**Fallback trigger for inverting the whole choice.** Make `llama-server` the default and LLamaSharp the opt-in "deep introspection mode" if (a) the native crash log in the first month of dogfooding is worse than expected, (b) you need a llama.cpp newer than the binding often enough to matter, or (c) you want one model instance shared across shell windows.

**Rejected: `Microsoft.ML.OnnxRuntimeGenAI` 0.15.2.** It **cannot load a GGUF at runtime** — GGUF is only an offline conversion input to a Python builder tool, so "point it at a `.gguf`" would require shipping Python. Its docs are also actively misleading: the published C# API page documents `ComputeLogits()` and a `Tensor.Data` property that **do not exist in current source**. It wins only if the product pivots to shipping one pre-converted ONNX model and needs DirectML/QNN/NPU acceleration on Windows-on-ARM.

**UNCONFIRMED.** (i) Whether the `noavx` `GGML_F16C`/`GGML_BMI2` bug ([#1407](https://github.com/SciSharp/LLamaSharp/issues/1407), fixed in the tagged-but-unpublished 0.29.0) affects 0.27.0 — **test explicitly if pre-AVX hardware must be supported.** (ii) Actual published-folder sizes; the figures below are compressed `.nupkg` sizes, and no publish was run. (iii) Whether Vulkan covers macOS (only `.Windows`/`.Linux` sub-packages exist; Metal ships inside `Backend.Cpu` for `osx-arm64`, but the researcher confirmed only that `libggml-metal.dylib` is *present*, not that Metal is *enabled* in that build).

**Do not write a csproj against LLamaSharp 0.29.0.** It is tagged on GitHub (2026-08-24) but **not on NuGet**. When it lands it removes `LLamaSharp.SemanticKernel`, `LLamaSharp.kernel-memory` and `LLama.Experimental` — do not build on those — and it carries the `BatchedExecutor` seq-ID pooling fix, which matters because `BatchedExecutor` is exactly the path recommended here.

### 3.9 Tokenization — `Microsoft.ML.Tokenizers` for remote, GGUF vocabulary for local

**Needed for.** `/tokenize <text>` showing token IDs, boundaries and offsets into the source string; "compare how three models tokenize this string"; and the local-estimate-vs-`usage.prompt_tokens` delta.

**Options.** `Microsoft.ML.Tokenizers` 2.0.0 (GA; 3.0.0-preview also exists); `SharpToken` 2.0.6 (alive, tiktoken-only); `TiktokenSharp` 1.2.1; `Tokenizers.DotNet` 1.4.1 (Rust HF binding); `FastBertTokenizer` (**effectively stale**, stable last published 2024-04-30).

**Decision. `Microsoft.ML.Tokenizers` 2.0.0 for hosted models; the model's own embedded vocabulary via `SafeLlamaModelHandle.Tokenize` / `TokenToSpan` for local GGUF. Never cross them.**

**Reasoning.** The API that matters is not `CountTokens`, it is:

```csharp
IReadOnlyList<EncodedToken> tokens = tokenizer.EncodeToTokens(text, out string? normalized);
// EncodedToken { int Id; string Value; Range Offset; }
```

**`Offset` is the whole ballgame for attribution.** It is the only supported way to draw a rectangle around the source characters a token came from — which is exactly what the presentation layer needs. Neither `SharpToken` nor `TiktokenSharp` gives you this. `Microsoft.ML.Tokenizers` is also the only library covering Llama/SentencePiece (BPE *and* Unigram)/WordPiece as well as tiktoken, which you need for local models and for the tokenization-comparison feature, and Microsoft's own migration guide directs `SharpToken` and `Microsoft.DeepDev.TokenizerLib` users to it.

Useful specifics: `TiktokenTokenizer.CreateForModel("gpt-5")` handles `gpt-5` through `gpt-5.6`, `gpt-4.1`, `gpt-4o`, the o-series, and **`gpt-35*` Azure deployment names**; `gpt-oss-` maps to `O200kHarmony`, which resolves to the o200k ranks file plus harmony special tokens — **there is no `…Data.O200kHarmony` package and you do not need one.** `GetIndexByTokenCount` trims to a budget without re-encoding.

**Be honest about the boundary, in the UI.** You can match a hosted provider's tokenizer at the *encoding* level. You **cannot** match it at the *request* level: the server tokenizes a rendered chat template — role headers, tool schemas, `<|im_start|>`-family scaffolding, harmony channel markers — that you do not control and OpenAI does not publish. Your count will not equal `usage.prompt_tokens` and your boundaries will differ at the joins. **Display "content tokens (local estimate)" next to the server's `prompt_tokens` and let the delta be visible.** That difference is itself an interesting debugging datum, and pretending it is zero is a lie the users of a *debugging* shell will catch.

**For local GGUF, use the model's own tokenizer.** It is by definition exactly what the model is running, including its special tokens. Using `Microsoft.ML.Tokenizers` against a GGUF introduces a second, subtly different tokenizer for no benefit.

**Given up.** Byte-exact parity with arbitrary HuggingFace models: **`Microsoft.ML.Tokenizers` has no `tokenizer.json` loader** — the researcher inspected `Model/`, `Normalizer/`, `PreTokenizer/` and `Utils/` on `main` and found no HF-JSON deserializer. And **no character offsets on the local path**: neither llama.cpp nor LLamaSharp hands you token→character spans, so you must reconstruct them by accumulating `TokenToSpan` byte lengths against the original UTF-8 buffer. That is the honest cost.

**Fallback trigger.** Adopt `Tokenizers.DotNet` 1.4.1 the moment byte-exact parity with an arbitrary `tokenizer.json` becomes a requirement. It costs a Rust native dependency across two platforms, which fights the self-contained goal — so make it a decision, not a drift.

**Rendering hazard specific to this domain.** **A token is a byte sequence, not a character.** Both OpenAI (`ChatTokenLogProbabilityDetails.Utf8Bytes`, `ReadOnlyMemory<byte>?`) and Ollama (`bytes []int`) return raw bytes *precisely because* a token can be half a UTF-8 codepoint or half an emoji. Rendering `token.Value` directly produces replacement characters and mis-measured column widths in a grid. **Attribute on `Utf8Bytes`, display `Token`**, and for a still-incomplete sequence render a visible placeholder (e.g. `‹e2 96›`) rather than a broken glyph. Use `StreamingTokenDecoder` for incremental display on the local path.

**UNCONFIRMED.** (i) Whether `Microsoft.ML.Tokenizers` 3.0.0-preview adds a `tokenizer.json` loader — the researcher inspected `main` and found none but did not read every file. (ii) Whether OpenAI's current lineup still uses `o200k_base` for the gpt-5.x series, or has introduced an encoding the library has not yet mapped; the `gpt-5`→`gpt-5.6` mapping is the maintainers' current belief, not independently verified against `tiktoken`'s `openai_public.py`.

### 3.10 Terminal UI — Spectre-primary REPL, Terminal.Gui v2 as an optional second front end

**Needed for.** Streaming a reply token by token; a scrollable, sortable probability map over a 128k vocabulary; a settings dialog; and `chatdbg … > transcript.txt` producing a usable transcript.

**Options.** Plain REPL + Spectre; full-screen `Terminal.Gui` v2; a hybrid; `Consolonia` (Avalonia-XAML-in-a-terminal, 62.5K lifetime downloads vs Terminal.Gui's 2.0M).

**Decision. Hybrid, Spectre-primary.** `Spectre.Console` 0.57.2 is the always-on renderer. `Terminal.Gui` 2.4.17 is an *optional* second front end for bounded interactive episodes — the settings dialog, the system-prompt picker, and the scrollable vocabulary inspector — driven either by v2's `AppModel.Inline` (renders into the primary scrollback buffer below the shell prompt, sizes to content, leaves its output in history) or by an opt-in `--tui` full-screen mode. `Terminal.Gui.Interop.Spectre` 2.4.17's `SpectreView` is the *only* sanctioned way to show a Spectre `IRenderable` inside a Terminal.Gui app.

**The thing a 2025-trained model gets wrong: Terminal.Gui v2 shipped GA on 2026-04-28** and is at 2.4.17. The "v1 is the only stable option" advice is dead. The source pins 1.19.0, which is maintenance-mode; **v1 → v2 is a rewrite of that project, not an upgrade** (`LogProbHeatmapView.cs` is 88 lines using **seven** removed APIs; `SettingsDialog.cs` is 608 lines and `SystemPromptsDialog.cs` 697, both hit hard by `CanFocus` now defaulting to **false** and by `Dim.Auto()` replacing `AutoSize`).

**Reasoning — the decisive measurement.** With stdout redirected to a file, a full Terminal.Gui `app.Run()` loop with the `ansi` driver wrote **zero bytes** — not even `CSI ?1049h`. Only Spectre's writes survived. That is arguably correct behaviour, but it means **a Terminal.Gui-primary app produces nothing at all when piped**: `chatdbg … > transcript.txt` yields an empty file. Spectre degrades correctly and measurably: same probe, redirected, produced a clean unstyled box-drawn table with **zero ESC bytes**.

**The source has a live bug here that this design fixes.** `ChatDbg.Shell.Gui/Program.cs` creates a `SpectreConsoleFormatter` and passes it into `ChatWindow`; commands then call `AnsiConsole.Write(grid)` — writing escape sequences directly to stdout **while Terminal.Gui owns the screen**. Terminal.Gui's cell buffer has no idea those bytes happened, so its next repaint leaves the output half-overwritten, and v1's alternate screen buffer destroys whatever survives at `Application.Shutdown()`. `SpectreView` is the fix: it renders the `IRenderable` against a `TextWriter.Null` console, converts each `Segment.Style` to a Terminal.Gui `Attribute`, and paints grapheme by grapheme with `AddStr`. **Spectre never touches the terminal.** Verified on the hard case — a table with CJK and emoji rows kept every `│` in columns 8 and 15 on every row.

**The one surface that genuinely needs the TUI** is the vocabulary probability map. Spectre `Table` has **no virtualisation** — it renders every row on every write. `Terminal.Gui` `TableView` scrolls, sorts and filters over 100k rows. That is the strongest argument for keeping Terminal.Gui at all.

**Three output modes, not two.** This is architectural, and it is the only real mitigation for the accessibility gap (below):

| Mode | Trigger | Renderer |
|---|---|---|
| `rich` | TTY, colour available, no `--plain` | Spectre with colour and borders; Terminal.Gui for opt-in episodes |
| `plain` | `--plain`, `NO_COLOR`, `TERM=dumb`, `Ansi=false`, or redirected stdout | Spectre with `ColorSystemSupport.NoColors` and `Table.NoBorder()` |
| `data` | `--format json` / `--format csv` | **No rendering at all** — serialise the token model |

The third mode is nearly free: `Xcaciv.Command`'s `IResult<T>` already carries a `ResultFormat` enum with `General`, `Object`, `CSV`, `TDL`, `YAML`, `JSON`. Use it. It is also what makes token introspection *scriptable*, which is arguably its highest-value use, and it gives you **one code path to audit for secret redaction instead of three**.

**Given up.** A persistent full-screen dashboard: the transcript scrolls into terminal scrollback rather than living in an independently scrollable pane. And two presenters for the same data — mitigated by putting the layout logic in one `ChatDbg.Presentation` assembly of pure `IRenderable`-returning functions consumed by both adapters, but the TUI path still needs its own mouse bindings, focus order and scroll state. **Budget it honestly; the hybrid is not free.**

**Fallback trigger.** Go Terminal.Gui-primary if the product vision becomes "an IDE in the terminal" — and then build a separate non-interactive entry point, because piping will otherwise produce nothing.

**Three hazards to design against.**
- **`LiveDisplay` is the most likely bug you will actually ship.** `Progress` and `Status` check `caps.Interactive && caps.Ansi` and swap in fallback renderers, producing clean plain text when redirected. **`LiveDisplay` has no such fallback** — three `ctx.Refresh()` calls redirected produced `live tick 0live tick 0live tick 1live tick 1live tick 2live tick 2live tick 2`. If the token stream is rendered with `LiveDisplay`, **every piped transcript is corrupt.** Add an integration test that redirects stdout and asserts no `\x1b` and no duplicated lines.
- **Column width is not `.Length`.** The source computes `var tokenWidth = tokenText.Length` — UTF-16 code units. For a CJK token that under-counts by 2×; for an emoji it over-counts while under-measuring display width. Since LLM tokenizers routinely emit CJK fragments and emoji, **the source's heat-map wrapping is already broken for non-Latin content.** Use `GetColumns()` (Terminal.Gui/Wcwidth) or let Spectre measure.
- **Colour detection is over-optimistic in Terminal.Gui.** Bare `TERM=xterm` with no `COLORTERM` reported `TrueColor`, and `IDriver.SupportsTrueColor` reported `True` even in the `NoColor` case — **read `ColorCapabilities`, never `SupportsTrueColor`**. Ship `--colors {auto|truecolor|256|16|none}`. Both libraries do honour `NO_COLOR`.

**Two things this design cannot deliver, and must document.** **RTL and bidi are absent from both libraries** — a GitHub search for `bidi` returns zero results in either repo; Arabic or Hebrew tokens render in logical order, unshaped. **Screen-reader accessibility is not achievable with either library** and is an ecosystem-wide gap, not a library choice; the mitigation is architectural (`--plain` and `--format json` as first-class paths), not a swap.

**UNCONFIRMED.** (i) `AppModel.Inline` behaviour on the Windows driver and on legacy conhost, where the CPR (`ESC[6n`) query may go unanswered — the docs say the startup gate "times out and rendering proceeds from row 0", which on a real shell would **overwrite the user's scrollback**. **Test on Windows conhost before shipping inline mode.** (ii) Whether `Canvas`/`BarChart` render correctly through `SpectreView` — `Canvas.Measure` branches on the *null* console's `options.Unicode`, so glyph selection may be wrong. `Table` was verified; these were not.

### 3.11 Rich rendering — `Spectre.Console` 0.57.2

**Needed for.** The heat-mapped token text, the responsive grid of token cards, the top-K detail table, and the rank–probability curve.

**Options.** `Spectre.Console`; `Spectre.Console.Ansi` 0.57.2 (standalone ANSI writer, **zero dependencies**) plus hand-rolled widgets.

**Decision. `Spectre.Console` 0.57.2, exact-pinned, with every view expressed as a pure function `TokenIntrospection → IRenderable` in one `ChatDbg.Presentation` assembly.**

**Reasoning.** The measure/render two-phase model (`IRenderable.Measure(RenderOptions, maxWidth)` then `Render(...)` producing `Segment`s) means a token-card grid re-lays-out for free at any width — **including a width you invent for a test**, which is what makes the snapshot strategy in §3.20 work. The widget set already covers what the app needs: `Table`, `Grid`, `Columns`, `Panel`, `Rule`, `Paragraph`, `Markup`, `BarChart`, `BreakdownChart`, `Canvas`. Downsampling is automatic and correct — the same `[rgb(255,120,0)]` emits a truecolor sequence or `ESC[33m` depending on the detected profile.

**Presentation rules that follow from `Xcaciv.Command`'s shape.** `IIoContext.OutputChunk(IResult<string>)` takes a **string**. An `IRenderable` cannot cross that boundary. That is a feature, and it dictates the architecture: **commands never render.** `InspectCommand`, `TokenizeCommand`, `LogProbsCommand` compute a `TokenIntrospection` model and emit it with `ResultFormat.JSON`; they take no dependency on Spectre or Terminal.Gui and are unit-testable against a `MemoryIoContext` with zero UI in the loop. Two ~150-line host adapters (`ConsoleIoContext`, `TuiIoContext`) call the same presentation functions.

**Colour semantics for this domain**, because getting them wrong misleads: sequential, perceptually uniform scale (viridis/magma family, **not rainbow**) for probability and surprisal; **diverging, zero-centred** for attribution deltas, because sign is meaningful there. **Encode probability as lightness, not hue**, so it survives a monochrome terminal, and always keep a numeric column for anyone who cannot see colour at all. Degrade truecolor → 24-step greyscale → `░▒▓█`. Prefer a `Gradient` over the source's six-bucket `switch` on `Color.Green`/`Brown`/`BrightRed`.

**Given up.** A pre-1.0 dependency that means it: 0.55.0 shipped `> [!CAUTION] There are breaking changes` and **converted `Style` from a class to a struct**, silently changing null-check and reference-equality semantics for anything holding a `Style?`. There is **no announced 1.0 for `Spectre.Console` itself** — only `Spectre.Console.Cli` was split out and prepared for one.

**Fallback trigger.** Drop to `Spectre.Console.Ansi` + hand-rolled widgets only if 0.x churn becomes intolerable. You lose `Table`/`Grid` measurement, which is most of the value.

**Mitigation for the 0.x risk.** Exact-pin `Spectre.Console` and `Spectre.Console.Testing` to the **same** version in central package management, keep the presentation assembly small so an upgrade is a bounded diff, and treat a Spectre bump as a deliberate task that includes re-approving snapshots. Rendering output is exactly what you are snapshotting.

**Note.** `Spectre.Console.Cli` is **not** taken (§3.24 / §3.2): it is separately versioned, stuck at 0.55.0 (2026-04-03) with its 1.0 alphas **unlisted** on nuget.org, it is explicitly `RequiresDynamicCode`, and `Xcaciv.Command` already owns command dispatch.

### 3.12 Serialization — source-generated `System.Text.Json`, reflection off

**Needed for.** `settings.json`, chat history, named system prompts, `/export` of token-analysis results, and telemetry that survives a round-trip through `ChatResponse.AdditionalProperties`.

**Options.** Reflection-based `System.Text.Json` (the source's design — zero `JsonSerializerContext` anywhere); source-generated STJ; Newtonsoft (**banned**, §3.22).

**Decision. One `JsonSerializerContext` per assembly, `JsonSerializerIsReflectionEnabledByDefault=false` in the csproj, `[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]`.**

**Reasoning.** Not because you trim — you don't (§5) — but because it makes the persistence contract **compile-time-checked** and fails loudly when a model changes. `JsonSerializerIsReflectionEnabledByDefault=false` turns an accidental reflection-based call into an `InvalidOperationException` with a descriptive message **consistently on CoreCLR**, so you find the gap on your dev box instead of in a user's build.

Five rules that will otherwise bite this codebase:
- **`object`-typed members are the exception** — a member declared `object` needs its runtime types explicitly `[JsonSerializable]`'d. So: **do not type any settings member as `object`**, and be careful with an "additional properties" bag, which is exactly the shape MEAI's `AdditionalPropertiesDictionary` has.
- **`JsonSourceGenerationMode.Serialization`** (fast path) **is not supported for async serialisation** — a streaming export of a large probability map falls back to metadata mode. Leave the default (both).
- **Use the generic `JsonStringEnumConverter<TEnum>`**; the non-generic one is unsupported under AOT.
- **Plugins contributing serialisable types compose via the resolver chain**: `options.TypeInfoResolverChain.Insert(0, hostContext); options.TypeInfoResolverChain.Add(pluginContext);`. Order is significant — the chain returns the first non-null result. This is the correct architecture even untrimmed, because it gives each plugin a compile-time-checked serialisation contract instead of ambient reflection.
- **Configuration keys are case-insensitive; STJ property matching is case-sensitive by default.** If you both bind the file through `IConfiguration` and round-trip it through `JsonSerializer`, a hand-edited `MaxTokens` will bind through configuration and be **dropped on the next save** unless you set `PropertyNameCaseInsensitive`.

**Wire-format contract to preserve** (from `tech-stack-observed.md` §5.2, D6): explicit `[JsonPropertyName]` on every persisted field, mixed camelCase with **one snake_case key — `top_alternatives`** — and `"logprob"` for `LogProb`, ISO-8601 `DateTime`. `WriteIndented = true` matters because users hand-edit these files. Note the asymmetry to *break*: the source writes `azureApiKey`/`awsAccessKey`/`awsSecretKey` from deprecated backing fields while the same-named C# properties are `[JsonIgnore]` computed — §3.5 removes those fields entirely.

**Given up.** A little boilerplate per persisted type.

**Fallback trigger.** None. Reflection-based STJ is strictly worse here even without trimming.

**Also fix, from the source:** three different `JsonSerializerOptions` configurations inconsistently applied across four services (one lacking a naming policy entirely, Bedrock request bodies serialised with **no options at all**), and `SystemPromptService`'s constructor calling `CreateDefaultSystemPromptsAsync().GetAwaiter().GetResult()` at line 32 **before `_jsonOptions` is assigned at line 34** — so the four default prompts are written un-indented while every later prompt is indented. One context, one options instance, no constructor I/O.

### 3.13 Resilience — an explicit Polly pipeline, never `AddStandardResilienceHandler()`

**Needed for.** Azure OpenAI and Bedrock 429s; a cold endpoint's slow time-to-first-token; a generation that stalls mid-stream.

**Options.** `AddStandardResilienceHandler()`; a custom `AddResilienceHandler("llm", …)`; `AddStandardHedgingHandler()`; hand-rolled retry.

**Decision. `IHttpClientFactory` + a custom `AddResilienceHandler("llm", …)` over `Microsoft.Extensions.Http.Resilience` 10.9.0 / Polly 8.7.0. Explicitly *not* the standard handler.**

**Reasoning — the standard handler's defaults are actively hostile to this workload:**

| Strategy | Default | Verdict for an LLM CLI |
|---|---|---|
| Rate limiter | queue 0, permit 1,000 | Pointless for a single user. Drop, or set 1–2 to serialise turns. |
| **Total request timeout** | **30 s** | **No.** A long answer or a big local prompt routinely exceeds it; left alone it aborts real generations. |
| Retry | 3 × exponential + jitter | Shape right, scope wrong. Reduce to 2 and gate on "nothing emitted yet". |
| **Circuit breaker** | failure ratio 10 %, **MinimumThroughput 100** per 30 s | **Mathematically cannot open in an interactive CLI** — you will never make 100 requests in 30 seconds. Dead weight that only adds a failure mode. Drop, or set `MinimumThroughput ≈ 4`, `FailureRatio ≈ 0.5`. |
| **Attempt timeout** | **10 s** | **No.** Time-to-first-token on a large hosted model, or a cold Bedrock endpoint, exceeds 10 s. Use it as a **time-to-first-byte** budget of 30–60 s instead. |
| Hedging | — | **Actively harmful.** It retries slow requests *in parallel*: two full generations, two bills, two streams to discard. Never enable it. |

**Keep the package anyway, for one reason:** `HttpRetryStrategyOptions`'s constructor sets **`ShouldRetryAfterHeader = true`**, and the setter installs a `DelayGenerator` that parses the response's `Retry-After` header and uses it as the delay. **Azure OpenAI and Bedrock 429s with `Retry-After` are honoured out of the box.** That is the single strongest argument over a hand-rolled `Task.Delay` loop — and it means you must **not** also implement `Retry-After` yourself, or you double the wait.

**The streaming interaction is the most important item here.** The resilience handler is a `DelegatingHandler` wrapping `SendAsync`. `HttpClient`'s default `HttpCompletionOption` is `ResponseContentRead`, so `SendAsync` does not complete until the **entire response body** is read — meaning the pipeline's total timeout covers the whole generation and kills long answers. Switch to **`HttpCompletionOption.ResponseHeadersRead`**, which you must do to stream anyway: `SendAsync` returns when headers arrive, the pipeline's timeouts become a time-to-first-byte budget, and **you automatically get the "only retry before the first token" property**, because by the time tokens flow the pipeline has already released the request.

The corollary, from the `HttpCompletionOption` docs: *"the timeout applies only up to where the headers end… The content reading operation needs to be timed out separately."* So you need a **second, independent stall timeout on the token stream** — "no token for 60 s ⇒ cancel", implemented with a `CancellationTokenSource` you `CancelAfter`-reset on each chunk. **Nothing in the resilience package does this for you.**

**The `HttpClient` shape**, mirroring the docs' own singleton example:

```csharp
builder.Services.AddHttpClient(ProviderKeys.Azure, c => { c.BaseAddress = ep; c.Timeout = Timeout.InfiniteTimeSpan; })
    .AddAsKeyed(ServiceLifetime.Singleton)
    .UseSocketsHttpHandler((h, _) => h.PooledConnectionLifetime = TimeSpan.FromMinutes(5))
    .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
    .RedactLoggedHeaders(["api-key", "Authorization", "x-api-key"])
    .AddResilienceHandler("llm", ConfigureLlmPipeline);
```

`HttpClient.Timeout` must be effectively disabled — it is a hard cap that fires independent of Polly and surfaces as `TaskCanceledException`, easily misread as user cancellation. `PooledConnectionLifetime` matters more than usual here: **a debugging shell is left open for hours, and Azure OpenAI endpoints sit behind fronting infrastructure whose addresses move.** `RedactLoggedHeaders` matters a lot for an app whose whole point is dumping internals to the terminal.

**The local path gets no retry at all.** A local inference failure is an out-of-memory or a bad model file; retrying three times makes the user wait three times as long for the same error. Give it a timeout and a clear message.

**Given up.** One-line setup. And the `Microsoft.Extensions.Http.Resilience` version line is `10.9.0` while `Microsoft.Extensions.Hosting` is `10.0.11` — different repos, different cadences, **both correct**. Use central package management and do not "fix" the mismatch.

**Fallback trigger.** Use `Polly.Core`'s `ResiliencePipelineBuilder` directly, registered via `AddResiliencePipeline<string, T>`, for anything that does not flow through an `HttpMessageHandler`.

**UNCONFIRMED.** Whether `Microsoft.Extensions.Http.Resilience` 10.9.0 declares `IsAotCompatible`. **Moot** — AOT is off (§5).

### 3.14 Logging — `Microsoft.Extensions.Logging` + a small NDJSON file provider

**Needed for.** `/exportlogs`; capturing the **native llama.cpp log callback**, which can fire on a native thread; and not corrupting piped stdout.

**Options.** `Microsoft.Extensions.Logging` with `[LoggerMessage]` source generators; Serilog 4.4.0 + `Serilog.Sinks.File`.

**Decision. `Microsoft.Extensions.Logging` 10.0.11 as the API surface everywhere, `[LoggerMessage]`-generated, writing to **stderr** by default at `Warning`, plus a ~150-line rolling NDJSON `ILoggerProvider` for the file path.**

**Reasoning.** Zero extra dependencies (it is in the shared framework), allocation-free and compile-time-checked, and it is the same `ILogger` that plugins will already have injected. The one gap is rolling files, which `Microsoft.Extensions.Logging` does not have in the box — and ~150 dependency-free lines is cheaper than four assemblies in a single-file download.

**stderr, not stdout, is not a style preference.** Two independent reasons: a piped transcript must not be polluted, and **if ChatDbg ever runs as an MCP stdio server, stdout *is* the protocol wire** — one stray `Console.WriteLine` breaks it. Route all human output through the `IIoContext`/`IAnsiConsole` abstraction and switch it to stderr in MCP-server mode. This is why `Console.Write*` is on the banned-API list (§3.22).

**Preserve the source's log file format** (D19): `[yyyy-MM-dd HH:mm:ss.fff] [LEVEL] message`, appended, daily-rotated as `llamasharp_yyyyMMdd.log`, buffered in memory with a 10,000-char flush threshold — and preserve the fact that a **native `NativeLogConfig.llama_log_set` callback writes into the same lock-protected buffer.** That lock is load-bearing: the callback fires on a thread you do not own.

**Given up.** Serilog's sink ecosystem, retention policy, and `{@Object}` destructuring of the token-analysis models.

**Fallback trigger.** Add Serilog behind `Serilog.Extensions.Logging` (keeping `ILogger<T>` as the app-facing API) if you want rolling-file policy and retention without writing them, or if operators already run Seq. They are not mutually exclusive — `ILogger` can fan out to both.

**UNCONFIRMED.** Serilog 4.4.0's official AOT/trim statement — a secondary source claims full support; unverified against Serilog's own notes. Moot here.

### 3.15 Telemetry — instrument unconditionally, export never (unless asked)

**Needed for.** Cross-provider token/cost accounting, time-to-first-chunk, local model-load duration, and plugin-load failures — without a developer tool ever phoning home.

**Options.** `OpenTelemetry` 1.18.0 SDK + MEAI's `UseOpenTelemetry()` middleware; hand-rolled `ActivitySource`/`Meter` in each adapter.

**Decision. One `ActivitySource` and one `Meter`, both named `Xcaciv.ChatDbg`, always present. `UseOpenTelemetry(sourceName: "Xcaciv.ChatDbg")` on the `IChatClient` pipeline. Reference `OpenTelemetry` 1.18.0 and `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.18.0 but build **no provider at startup**.**

**Reasoning — the single most important architectural fact here.** The .NET instrumentation API is **in the BCL**, not the OTel package: `System.Diagnostics.ActivitySource`, `System.Diagnostics.Metrics.Meter`, `ILogger`. With no listener registered, `ActivitySource.StartActivity` returns `null` and `Histogram.Record` is a no-op. **You can ship the instrumentation in every build and it never opens a socket.** For a local developer tool that must not phone home, this is the load-bearing decision — and "not constructing an exporter" is strictly stronger than "configuring one to be quiet", because `OTEL_EXPORTER_OTLP_ENDPOINT` defaults to `localhost:4317/4318` and merely constructing an exporter starts trying to connect somewhere.

Opt-in, explicit, one flag each:

| Flag | Effect |
|---|---|
| `--diag-log <path>` | Adds the NDJSON file logger. Satisfies `/exportlogs` with no network. |
| `--trace-console` | Builds providers with **only** `OpenTelemetry.Exporter.Console`. Everything stays on the terminal. |
| `--otlp-endpoint <url>` | **The only path that opens a socket**, and only to the URL the user typed. Never defaulted, never read from `OTEL_EXPORTER_OTLP_ENDPOINT` implicitly. |
| `--capture-content` | Sets `EnableSensitiveData = true`. Print a one-line stderr warning every session it is on. |

Also honour **`OTEL_SDK_DISABLED=true`** as defence in depth. And **do not silently honour `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT`** — MEAI's `OpenTelemetryChatClient` will turn on content capture from that env var; surface its state in `/status` so it is visible. Prompts and completions must not enter telemetry by accident in a tool that also holds provider secrets.

**What MEAI's middleware gives you free**, and why not to hand-roll: `gen_ai.operation.name`, `gen_ai.request.model`, `gen_ai.response.model`, `gen_ai.provider.name`, the full request-parameter set, `gen_ai.response.finish_reasons`, the usage attributes, and four histograms including **`gen_ai.client.operation.time_to_first_chunk`** — the number a REPL user actually feels. One `sourceName` string enables or disables all of it, because the `ActivitySource` and `Meter` share that name.

**Be honest about the conventions' status.** **Nothing in the `gen_ai.*` namespace is Stable.** As of the 17 July 2026 snapshot, every GenAI span, event, metric and attribute carries **Development**. They also *moved*: as of semantic-conventions **v1.42.0 (2026-06-12)** all `gen_ai.*` content left `open-telemetry/semantic-conventions` for the dedicated `semantic-conventions-genai` repo — an organisational split to let GenAI move fast, **not a graduation**. There is a rename history (`gen_ai.system` → `gen_ai.provider.name`; `prompt_tokens`/`completion_tokens` → `input_tokens`/`output_tokens`) and there will be more. **Do not build user-visible features that assume attribute names are stable**, and do not treat a dashboard built on them as a supported contract.

**There is no `gen_ai.provider.name` enum value for a locally-hosted llama.cpp model.** The list is open; emit `llama.cpp` and document that any conformant backend will treat it as unknown.

**Custom metrics worth having**, because no GenAI convention covers them: `chatdbg.model.load.duration` (local GGUF load dominates cold start), `chatdbg.local.tokens_per_second`, `chatdbg.plugin.load.duration` and `chatdbg.plugin.load.failures` — plugin load is a **new failure surface that did not exist in the source**.

**Cost accounting.** Normalise on `Microsoft.Extensions.AI.UsageDetails` (it maps onto all three backends; the local one fills it from the tokenizer the introspection feature already runs, so it is nearly free) and compute cost **locally and offline** from a user-editable price table versioned with an `effectiveDate`, defaulting to `null` = "cost unknown" rather than a baked-in price that silently goes stale. **Do not fetch prices over the network.** Two traps: **streaming Azure OpenAI returns usage only if you request it** (`stream_options: {include_usage: true}`), and without it `/cost` reports zero for every streamed turn; and **cached tokens are the accounting trap** — a model that multiplies `InputTokenCount` by the input rate is materially wrong for a shell that replays a long system prompt every turn. Compute `(input − cached)×inputRate + cached×cachedRate + cacheWrite×cacheWriteRate + (output − reasoning)×outputRate + reasoning×reasoningRate`. Bedrock's `cacheWriteInputTokens` has no first-class `UsageDetails` property; put it in `AdditionalCounts`.

**Given up.** Free spec conformance for the local introspection path, which does **not** flow through `IChatClient` and must be instrumented by hand. And MEAI does not emit the spec's `gen_ai.client.inference.operation.details` **ActivityEvents** ([agent-framework#3637](https://github.com/microsoft/agent-framework/issues/3637)) — if a backend needs them, that is your code.

**Fallback trigger.** Hand-roll `ActivitySource`/`Meter` per adapter if the three backends stop sharing `IChatClient`.

**For the user who wants to look:** document `docker run --rm -it -p 18888:18888 -p 4317:18889 mcr.microsoft.com/dotnet/aspire-dashboard:latest` then `chatdbg --otlp-endpoint http://localhost:4317`. In-memory only, no Aspire dependency, never leaves the machine.

**UNCONFIRMED.** The exact OpenTelemetry .NET version that shipped `OTEL_SDK_DISABLED` — verify against `src/OpenTelemetry/CHANGELOG.md` before relying on it as a kill switch. And whether the GenAI semconv repo has cut a release after the 17 July 2026 snapshot promoting anything to Stable; re-check at implementation time.

### 3.16 Tool/plugin framework — `Xcaciv.Command` bridged to `AIFunction`; MCP for everything third-party

**Needed for.** In-session commands (`/tokenize`, `/inspect`, `/export`); letting the *model* call the introspection tools to explain its own token choice; and letting users add tools without handing them your process.

**Options.** (FIXED) `Xcaciv.Command` for command definition. For model-callable tools: MEAI `AIFunction`; Semantic Kernel plugins; Agent Framework tools. For third-party extension: in-process assemblies; MCP over stdio; `StreamJsonRpc`.

**Decision. A four-tier model, terminating in exactly one `AIFunction` registry.**

```
Tier 0  Built-in commands            compiled in; Xcaciv.Command ICommandDelegate; zero ALC involvement
Tier 1  First-party optional packs   assemblies you built and signed; one long-lived ALC per pack
Tier 2  MCP servers                  ANYTHING third-party; own process, stdio, killable, restartable
Tier 3  ChatDbg-as-MCP-server        `chatdbg mcp` re-exports the read-only introspection tools
```

**One registry, one abstraction.** Tier 0/1 arrive via `AIFunctionFactory.Create(MethodInfo, createInstanceFunc, options)` driven by an `ICommandDescription` → JSON Schema adapter; Tier 2 arrive as `McpClientTool`, which **derives from `AIFunction`**, so an MCP tool drops straight into `ChatOptions.Tools` with zero adapter code; Tier 3 goes the other way via `McpServerTool.Create(AIFunction, …)`. **There is exactly one place that decides what the model can call.**

**Reasoning — the rule of thumb.** *If the tool needs a pointer into ChatDbg's own state, it is an in-process command and you compiled it. If it needs the outside world, it is an MCP server and it runs in its own process. There is no third category, and "trusted third-party plugin" is not one.*

That is not fastidiousness; it follows from a platform fact. **There is no CAS and no sandbox in modern .NET.** Microsoft states it plainly: *"Sandboxing… isn't supported… Use security boundaries provided by the operating system."* And on AppDomains: *"For code isolation, use separate processes or containers… To dynamically load assemblies, use the `AssemblyLoadContext` class."* Note the deliberate split — isolation → processes; dynamic loading → ALC. They are different problems and .NET only solves the second. Concretely: **a loaded plugin can read the Azure OpenAI key you just decrypted out of the process's own memory.** A child process cannot.

**The bridging work, honestly.** `Xcaciv.Command`'s attribute set (`[CommandRegister]`, `[CommandRoot]`, `[CommandParameterOrdered]`, `[CommandParameterNamed]`, `[CommandFlag]`, `IParameterValue<T>` with `ParameterString`/`ParameterLong`/`ParameterBool`/`ParameterGuid`/`ParameterDateTime`/`ParameterJson`) maps mechanically onto `AIFunction.JsonSchema` — ordered params to `required` in order, named to optional properties, flags to booleans. The strong typing added in Command 3.1.0 is what makes it mechanical. **Two real mismatches:** flatten `Root Sub` to `root_sub`, because most providers require `^[a-zA-Z0-9_-]{1,64}$` and a space is rejected; and `IAsyncEnumerable<IResult<string>> Main(...)` versus `Task<object?> InvokeAsync(...)` is structural — **buffer the enumerable to a bounded string or record for the tool path and keep the streaming path for the human**, capping the buffer, because an unbounded token dump will blow the context window. Inject a *headless* `IIoContext` that captures output and **throws** on `PromptForCommand`; a tool the model invoked must not prompt the human. If you need mid-call input, that is MCP v2's `InputRequiredException`/elicitation.

**Which commands the model may call — a curated allowlist, not "everything the parser knows":**

| Commands | Exposure | Why |
|---|---|---|
| `tokenize`, `logprobs`, `show-analysis`, `inspect`, `help` | **Yes, read-only** | *"Why did you pick `foo` over `bar`?"* → the model calls `logprobs` on its own last turn and explains itself. **This is the killer feature of the rebuild, and it only works in-process.** |
| `export`, `export-logs`, `export-analysis` | **Approval-gated** | Writes files at a model-chosen path. Classic confused-deputy target. Show the **resolved absolute path** before writing. |
| `set`, `model`, `prompt` | **Approval-gated or not at all** | These change settings, switch backends (spend money elsewhere) and swap the system prompt. A model that can rewrite its own system prompt is not a feature. |
| `inject` | **Never** | — |
| `import` | **No** | Reads arbitrary files into history at a model-chosen path — a direct exfiltration primitive when combined with any outbound tool. |
| `clear`, `pop` | **No** | Destroys the user's history on the model's initiative. No upside. |
| `exit`, `quit`, `install` | **Never** | A model that can install its own tools has no ceiling. |

The deciding heuristic is the **lethal trifecta**: private data + untrusted content + an outbound channel. ChatDbg has the first the moment it holds credentials and history; it has the second the moment any MCP server returns attacker-controlled text; `export` or any network tool is the third. **Keeping the third behind `ApprovalRequiredAIFunction` is the cheapest break in the chain.**

**`ApprovalRequiredAIFunction` does not enforce anything** — the docs are explicit that *"it is the responsibility of the invoker to obtain that approval."* Intercept `FunctionApprovalRequestContent`, render a Spectre panel showing tool name, **resolved** arguments and **origin** (`built-in` / `pack:Foo` / `mcp:context7`), require a keypress, reply with `FunctionApprovalResponseContent`. **Always show origin** — a tool named `read_file` from an MCP server configured last month must not look identical to a built-in. Set `MaximumIterationsPerRequest` to 5–10 so a tool loop cannot burn the user's budget. Treat every tool *result* as untrusted content: do not render it as markup Spectre will interpret, and never promote it into the system prompt.

`Xcaciv.Command`'s `IAuditLogger`/`StructuredAuditLogger` already exists and already masks values — tag every tool invocation into it rather than inventing a second log.

**A natural risk signal is already in the framework:** any command whose description sets `ModifiesEnvironment == true` gets wrapped in `ApprovalRequiredAIFunction` automatically. So does anything that writes files or spends money.

**Given up.** In-process third-party extensibility as a headline feature. Anyone who wants to write a ChatDbg tool writes an MCP server, in whatever language, and takes a process-boundary latency hit and a JSON-serializable data model. **For the token-map use case that is a real loss** — you cannot cheaply hand an external tool a 128k `float[]`. You also take a dependency on a six-month-old GA library that shipped nine releases in six months (MCP 1.0.0 → 2.2.0), and `ModelContextProtocol.Core` 2.2.0 hard-requires `Microsoft.Extensions.AI.Abstractions ≥ 10.8.3`, so the two version together whether you like it or not.

**Fallback trigger.** Swap MCP for `StreamJsonRpc` 2.25.29 if every tool is yours, per-call overhead dominates, and MCP's schema/discovery ceremony and release cadence are pure cost. You lose the entire MCP ecosystem.

**Package-install security, if `install <name>` ships at all.** State it bluntly in the design doc: *`install Foo.Bar` means fetch code chosen by a string the user typed, from a public feed anyone can publish to, and execute it inside the process that currently holds the user's Azure OpenAI key, AWS credentials and complete chat history — with no runtime sandbox, because .NET does not have one.* Mitigations in order of value: **restrict to a curated feed or ID-prefix allowlist** (`ChatDbg.Tools.*`); two-step confirmation showing ID, version, publisher, signature status, download count, publish date and the transitive set; **`PackageExtractor.ExtractPackageAsync` with a `PackageSignatureVerifier` and `ClientPolicyContext`** (rolling your own `ZipFile.ExtractToDirectory` skips verification and re-opens zip-slip); pin every extracted assembly's SHA-256 into the loader's hash store afterwards and run strict; extract to a per-user, non-world-writable directory that is **not** `/tmp`; **never make `install` model-callable at any approval level.**

Note that `Xcaciv.Cupcake`'s NuGet layer is a **sketch, not a component** — `NugetWrapper.InstallPackage` ends with `// TODO: Extract package to directory` and `// TODO: resolve dependencies`, and `InstallCommand.HandleExecution` returns the literal `"Not installing " + …`. Search, enumerate, download and read-identity work; extraction, transitive resolution, TFM asset selection and signature verification are absent. Cupcake also pins `Xcaciv.Command` **2.1.1** while Command is at 3.3.x and shipped breaking changes in 3.0.0 and 3.2.3 — **Cupcake as it stands will not compile against current Xcaciv.Command. Copy the pattern (`Loop.cs`), not the code.**

**The self-contained-binary trap for NuGet signature verification:** the documented behaviour describes the **SDK's** restore path reading the SDK's certificate bundles. A self-contained binary on a machine with no SDK has no bundle. Windows' OS root store carries you; on Debian/Ubuntu, `/etc/pki/ca-trust/extracted/pem/objsign-ca-bundle.pem` is absent by default and there is nothing to fall back to. **Verify against a shipped, signed SHA-512 hash manifest rather than X.509** — verifiable offline, no dependency on three divergent OS trust stores, and it composes with the loader's existing integrity check. (Signature verification is also **disabled by default on macOS, and Microsoft recommends leaving it so.**)

**UNCONFIRMED.** (i) Whether `ModelContextProtocol.Core` 2.2.0 is annotated trim-safe/AOT-safe — the SDK does not set `IsAotCompatible`; **moot**, AOT is off. (ii) Whether `AIFunctionFactory.Create`'s `Delegate`/`MethodInfo` overloads carry `RequiresUnreferencedCode`/`RequiresDynamicCode` — the Learn page shows none, but generated reference docs routinely omit those attributes; moot here, decisive if AOT ever returns. (iii) `Command.Packages` (github.com/Xcaciv/Command.Packages) exists and **may already contain the finished install logic Cupcake stubs out** — worth checking before rebuilding that layer.

### 3.17 Dynamic loading — `Xcaciv.Loader`, one cached ALC per package

**Needed for.** Tier-1 first-party command packs, discovered at runtime from a `plugins/` directory beside the executable.

**Options.** FIXED: `Xcaciv.Loader` 2.1.2. (For completeness: `McMaster.NETCore.Plugins` 2.0.0 — best-documented in the space, but **no stable release since 2025-01-05**; or ~80 lines of hand-rolled `AssemblyLoadContext` + `AssemblyDependencyResolver`.)

**Decision. `Xcaciv.Loader` 2.1.2, with eight non-negotiable rules.**

1. **One `AssemblyContext` per package, cached for the session.** Not per invocation. `Xcaciv.Command`'s `CommandFactory.CreateCommand` currently does `using var context = new AssemblyContext(...); return context.CreateInstance<ICommandDelegate>(fullTypeName);` — the `using` calls `Unload()` at the moment the freshly created instance (whose *type* lives in that ALC) is handed to the caller. Because unload is **cooperative, not forced**, nothing crashes; the ALC simply never finishes unloading, and **a new ALC is created for every single command invocation.** In a batch tool nobody notices. In an interactive shell left open for hours running token inspection in a loop, you accumulate one `LoaderAllocator` per invocation. **This is the highest-value single fix in the whole plugin area.**
2. **Contract assembly in the Default ALC, asserted at startup.** `Xcaciv.Command.Interface.dll` — `ICommandDelegate`, `IIoContext`, `IEnvironmentContext`, `IResult<T>`, the attributes, the whole `Parameters` namespace — must load exactly once, in `AssemblyLoadContext.Default`, and plugins must reference it with `<Private>false</Private>` / `ExcludeAssets="runtime"`. Assert `AssemblyLoadContext.GetLoadContext(typeof(ICommandDelegate).Assembly) == Default` and **fail loudly** if not. That one check prevents the single most confusing plugin bug, whose exception message is famously useless: `Object of type 'ICommandDelegate' cannot be converted to type 'ICommandDelegate'`.
3. **Cache the `AssemblyDependencyResolver`.** `Xcaciv.Loader` constructs a fresh one **on every `Resolving` event**, re-parsing `deps.json` each time.
4. **Publish an explicit host-provided assembly allowlist** with versions (`Xcaciv.Command.Interface`, `Microsoft.Extensions.AI.Abstractions`, `System.Text.Json`, `Microsoft.Extensions.Logging.Abstractions`, …) and **refuse a plugin whose `deps.json` demands a higher version of anything on it**, with a message naming both versions. This is the diamond conflict, and it is real: `Xcaciv.Loader` subscribes to the `Resolving` event rather than overriding `Load`, which correctly shares host assemblies by default — but the mirror image is that **a plugin can never win a version fight with the host**, and if it needs a higher `System.Text.Json` you end up with two `JsonElement` types in the process.
5. **`basePathRestriction` = the exact package directory. Never `"*"`** — the README itself says `"*"` "is equivalent to no security."
6. **Integrity verifier in strict, non-learning mode in release builds.** Learning mode is trust-on-first-use, fine in dev, wrong in release; gate it behind an explicit `--trust-on-first-use`.
7. **Shadow-copy before load**, so a package can be upgraded without restarting the shell. `LoadFromAssemblyPath` memory-maps the file and Windows will hold the lock.
8. **Unload only on explicit `unload`/`exit`**, verified with `WeakReference(alc, trackResurrection: true)` and a bounded GC loop. If it fails to unload, log it and move on — do not spin.

**Be precise about what `Xcaciv.Loader` buys.** Its controls are **load-time admission control, not runtime confinement** — a defensible *integrity* story ("these are the bits I approved") and a weak *confinement* story ("this code can only do X"). Specifically:
- `AssemblySecurityPolicy.Strict`'s forbidden-directory list (`system32`, `programfiles`, `windows defender`, `appdata\local\microsoft\credentials`, …) is a **case-insensitive substring match on Windows-shaped paths.** On Linux it matches essentially nothing — a plugin at `/opt/tools/plugin.dll` passes Strict trivially. **Do not present it as cross-platform protection.**
- `AssemblyPreflightAnalyzer`'s scan for `Reflection.Emit` / `Expressions.Compile` indicators is a metadata heuristic, defeated by `Type.GetType("System.Reflection.Emit.AssemblyBuilder")` + late binding. **A speed bump and an audit signal.**
- The SHA-256 `AssemblyHashStore` is the genuinely valuable piece, and it composes with the shipped-hash-manifest approach in §3.16.

**Given up.** Native AOT and trimming, for the whole shell — see §5. Also collectible ALCs **ignore ReadyToRun code**, so plugins lose the R2R startup benefit the host gets, and **C++/CLI assemblies are unsupported** in them.

**Fallback trigger.** Replace with ~80 lines of hand-rolled ALC + `AssemblyDependencyResolver` (the Microsoft "Create a .NET Core application with plugins" tutorial is the whole recipe) **if the AGPL-3.0 licence or the unpublished-package problem is not resolved.** You lose the hash store and the audit events.

**Licence note, as fact not advice.** `Xcaciv.Command` and `Xcaciv.Loader` are **AGPL-3.0**; `Xcaciv.Cupcake` is BSD-3-Clause; the subject's own `LICENSE` is GPL-3.0. GPLv3 §13 permits the combination, but the AGPL portions carry AGPL obligations into the combined work. For a local terminal binary the network-interaction clause is mostly inert — **but if ChatDbg ever grows a hosted mode, get this reviewed.** The MCP SDK (Apache-2.0 with residual MIT) is one-way compatible with GPLv3 and fine.

**UNCONFIRMED, and it is the single most important thing to confirm before acting on this document:** whether `Xcaciv.Loader` genuinely uses runtime `AssemblyLoadContext` loading. Two researchers flagged this. §5's conclusion — that Native AOT is impossible — is **conditional on it.** If the loader turns out to use a compile-time source-generated registry instead, §5 changes entirely and AOT is back on the table. Also unconfirmed: whether the per-invocation ALC issue is fixed on `main` after the last release (the pattern is present in the 2026-07-26 push; the functional consequence is inferred from documented cooperative-unload semantics, not measured).

### 3.18 Testing framework — xUnit v3 4.0.0 on Microsoft.Testing.Platform v2

**Needed for.** Table-driven token-math tests; contract tests across three backends; render snapshots; native-inference tests that must not wedge CI.

**Options.** `xunit.v3` 4.0.0 (GA 2026-08-14) on MTP v2; `xunit.v3` 3.2.2 (MTP v1); `MSTest` 4.3.3; `NUnit` 4.6.1; `TUnit` 1.65.68; the source's `xunit` 2.9.1 on VSTest.

**Decision. `xunit.v3` 4.0.0 on `Microsoft.Testing.Platform` 2.3.3**, selected repo-wide by `global.json`:

```json
{ "test": { "runner": "Microsoft.Testing.Platform" } }
```

**Reasoning — and one of the four supporting arguments has to be retired.** `testing-quality.md` leads with *"MTP is the only platform that can run Native AOT and trimmed test hosts (the exact deployment mode this app ships in)."* **That premise is false for this rebuild**: §5 establishes that the app ships **untrimmed, non-AOT**, because `Xcaciv.Loader` loads assemblies at runtime. I am resolving that contradiction here and keeping the decision, because the remaining reasons stand on their own:

1. **It is already what this repo and `Xcaciv.Command` write tests in.** v2 → v3 is mechanical (`xunit` → `xunit.v3`, drop `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk`, add the MTP runner). Moving to MSTest or TUnit is a rewrite of ~20 existing test files and 100 `[Fact]`s.
2. **Executable-first test projects.** A test project is a program: `dotnet run`, F5, `dotnet watch` — and, decisively, **the same binary CI runs can be run under `lldb`/`gdb`/WinDbg when llama.cpp segfaults.** That is a real workflow for native-backed inference.
3. **`[Theory]` + `MemberData` is the right shape** for token introspection, which is inherently table-driven: *this token stream + this top-K → this probability map*. The source has **100 `[Fact]` and 0 `[Theory]`**; variation is expressed by copy-pasted methods.
4. **Built-in dynamic skipping** — `Assert.Skip`, `Assert.SkipUnless`, `Assert.SkipWhen`, and `[Fact(SkipUnless = nameof(ModelIsPresent))]`. That is the "skip if the GGUF isn't on this machine" primitive with no third-party package (`Xunit.SkippableFact` is now obsolete for you).
5. **`Microsoft.Testing.Extensions.CrashDump` / `.HangDump` 2.3.3 (MIT).** `llama.cpp` can `abort()` on a malformed GGUF and wedge on OOM. `--hangdump --hangdump-timeout 5m` turns an opaque six-hour CI timeout into a dump file. This is the MTP-specific argument that survives dropping AOT.

**This is a whole-repo decision.** Microsoft warns explicitly against mixing VSTest-based and MTP-based projects in one solution, and MTP v2 hard-errors when run through the VSTest `dotnet test` path. It has to cover any Xcaciv.* projects built from source alongside.

**The test pyramid, by layer:**

| # | Layer | Runs | Budget | Gate |
|---|---|---|---|---|
| 0 | Compile-time (analysers, nullable, warnings-as-errors) | every build | — | Blocking |
| 1 | Unit — token math, history, prompts, settings, parsing | every PR | < 10 s | Blocking, **90 % on token namespaces** |
| 2 | Render snapshot — grids, top-K tables, heat maps | every PR | < 5 s | Blocking |
| 3 | Command/shell integration — `ICommandController`, pipelines, loader discovery | every PR | < 30 s | Blocking |
| 4 | Provider contract (stubbed + WireMock.Net 2.15.0) | every PR | < 60 s | Blocking |
| 5 | Native inference, **tiny model** | every PR *if the cached model is present* | < 2 min | Blocking when present |
| 6 | Mutation (`dotnet-stryker` 4.16.0, token namespaces only) | weekly / on touch | 15–30 min | Report; break < 60 |
| 7 | Benchmarks | scheduled, dedicated runner | ~20 min | Alert at ~115 % |
| 8 | Live provider smoke (one real call each) | nightly | ~5 min | Non-blocking; opens an issue |
| 9 | Published-artifact smoke (starts, banner, `/quit`, exit 0, win-x64 + linux-x64) | per release + nightly | < 1 min | **Blocking for release** |

**The contract-test pattern is the payoff**, and it is why `IntrospectionCapabilities` (§3.6) earns its keep — write the backend suite once as an abstract class parameterised by a factory, and let the shared assertions consult the capability value instead of branching on backend type:

```csharp
public abstract class BackendContract {
    protected abstract ITokenIntrospectingBackend Create();
    protected abstract IntrospectionCapabilities Expected { get; }
    [Fact] public Task Tokenize_then_detokenize_round_trips();
    [Fact] public Task Streaming_emits_finish_reason_exactly_once();
    [Fact] public Task TopK_count_never_exceeds_capability_cap();
    [Fact] public Task Cancellation_stops_the_stream_and_disposes_native_state();
    [Fact] public Task Logprobs_are_non_positive();
}
```
with `FakeBackendContract` (always runs), `LlamaSharpContract` (trait-gated, tiny model), `AzureOpenAIContract` and `BedrockContract` (trait-gated, WireMock). **That is the mechanism that makes "three backends, one behaviour" true rather than aspirational.**

**Native inference in CI, at ~15 MB.** Copy LLamaSharp's own mechanism — an MSBuild `DownloadFileItem` group with a skip-if-exists target — but go smaller than it does: a `tinyllama-15M-stories` GGUF (~15–20 MB) is enough to prove the P/Invoke boundary loads, the tokenizer round-trips, the logits array has vocab-size length, the sampler returns a token and top-K slicing works. **That is the PR-gate model.** `SmolLM-135M` Q8_0 (~140 MB) for "a chat template applies and a short greedy completion is stable"; LLamaSharp itself gates on 360M/~390 MB — nightly, not per-PR. Cache in CI keyed on the URL; **assert the SHA-256**; **never commit the `.gguf`**; and make the tests **skip** rather than fail when it is absent, so a Hugging Face outage degrades the gate instead of blocking every PR. Under MTP, **zero discovered tests exits with code 8**, so an over-aggressive filter fails the build rather than silently passing — which is the behaviour you want.

**Native state is not parallel-safe.** Model and context handles are expensive and not thread-safe. Put all native inference tests in one non-parallel collection with a shared fixture. This interacts directly with xUnit v3 4.0's parallelisation change: configuration moved from `CollectionBehavior` to `[assembly: Parallelization(Mode = …)]`, the default is still `collections`, and **once you opt out at a layer you cannot opt back in below it.**

**Given up.** A `dotnet test` that "just works" in every tool — Microsoft concedes *"some third-party integrations might still lag behind VSTest"*; on Azure DevOps you must use `DotNetCoreCLI@2`, **not** `VSTest@3`, and you must verify your coverage publisher accepts MTP's Cobertura before building a dashboard on it. And you are on a **two-week-old major version** whose runner-stack rewrite touched the internals every third-party extension hooks.

**Fallback trigger.** Roll back to `xunit.v3` 3.2.2 if 4.0's runner stack proves unstable. **Write down now that the rollback also flips you to MTP v1**, which the .NET 10 SDK's `dotnet test` treats differently.

**Rejected: TUnit 1.65.68.** Genuinely production-usable (5.3M downloads, MTP-only, source-generated, AOT-first) and the honest second-best — but **five releases in the nine days 2026-08-18 → 2026-08-26**, and neither the site nor the repo publishes an API-stability or LTS statement. For a project pinning versions in CPM and expecting a stable suite over years, that is a lot of moving surface.

**Coverage:** `coverlet.mtp` 10.0.1 (`--coverlet`), **MIT**. The licence is the deciding factor over `Microsoft.Testing.Extensions.CodeCoverage` 18.10.0, which is better at mixed managed/native but carries a **proprietary Microsoft licence** forbidding it "as a stand-alone offering" with liability capped at $5.00 — the kind of thing that surfaces in a legal review long after the choice was made. Correction to a 2025-era prior: **coverlet is not stagnant** — 8.0.0 (2026-02), 10.0.0 (2026-04), 10.0.1 (2026-05). Set **per-assembly** thresholds, not one repo-wide number: token-inspection 90 %, Core 80 %, provider adapters 50 %, the TUI project **excluded** (keep it empty of logic instead).

**Mutation testing:** `dotnet-stryker` 4.16.0 with `--test-runner mtp` (**preview**), scoped to `**/TokenInspection/**` and `**/Tokenization/**` only, weekly. **Line coverage cannot tell you whether your top-K test asserts anything; mutation score can** — and the token maths *is* the product. Nothing else in the app justifies the runtime. Stryker's MTP runner cannot yet map which test covers which mutant, which means slower runs and weaker reports; scope narrowly and keep the `break` threshold low (60).

### 3.19 Mocking — hand-rolled fakes by default, `NSubstitute` narrowly

**Needed for.** A `FakeBackend` that scripts a streaming response with per-token logprobs and top-K alternatives and records the messages it was handed.

**Options.** `NSubstitute` 6.2.0; `FakeItEasy` 9.0.1; `Moq` 4.20.72; hand-rolled; `RichardSzalay.MockHttp` 7.1.0 for HTTP specifically.

**Decision.**

| Situation | Use |
|---|---|
| Any interface you own (`ITokenIntrospectingBackend`, `IChatHistoryStore`, `ISecretStore`, `IIoContext`) | **Hand-rolled fake** in `TestDoubles/`, one class per role |
| Anything HTTP | Hand-rolled `HttpMessageHandler` for simple cases; **`RichardSzalay.MockHttp` 7.1.0** when you need URL/query/JSON-body matching and call counts |
| Wide third-party interfaces you don't own (`IAmazonBedrockRuntime` has dozens of members) | **`NSubstitute` 6.2.0** |

**Reasoning — four concrete situations where hand-rolled wins for *this* app.** (1) **When the double replays a protocol, not a call.** A streaming response is a sequence: N deltas, then a finish reason, then usage. `FakeBackend.Script(TokenEvent.Text("The", logprob: -0.21, alts: [("A", -1.9)])).ThenFinish(...)` is ten lines and reads like the spec; the NSubstitute equivalent is `.Returns(callInfo => …)` lambdas nobody can debug. (2) **When the double must be inspectable afterwards** — `fake.LastRequest.Messages`, `fake.SeenSystemPrompt` are strongly typed; `Received().Method(Arg.Is<…>(x => …))` fails with a stringly-typed diff. (3) **When the interface is narrow and stable** — an interface you designed for testability is by definition narrow, and a mocking library's value is proportional to member count. (4) **When the double is shared across dozens of tests** — a fake used by 40 tests is a first-class piece of the codebase and deserves to be a real class with a builder. Microsoft's own guidance agrees: *"A small fake implementation is preferred over mocking `IChatClient` with Moq or NSubstitute."*

**`Moq` is out, and the 2026 reason is not SponsorLink.** The 2023 SponsorLink incident (a closed-source assembly that hashed `user.email` from git config and phoned an Azure endpoint at build time) was reverted in later 4.20.x. The disqualifier now is simpler: **latest NuGet release 4.20.72, published 2024-09-07** — no binary across two .NET majors and a C# language version, for a library that does runtime IL generation. The repo is alive (pushed 2026-08-27, 21 open issues), so "stalled, not abandoned" is the accurate characterisation. **The source pins `Moq 4.20.69` — a pre-fix SponsorLink build.** That alone is a reason to move.

**Grow the existing stub.** `StubHttpMessageHandler` is the right instinct but too thin — it returns one canned response regardless of the request. Make it take a `Func<HttpRequestMessage, HttpResponseMessage>`, record every request, and be able to emit a **chunked SSE stream**, which streaming completions need.

**Given up.** `mock.Setup(...)` convenience. Hand-rolled fakes are more code up front; the payoff is tests that read like specifications, and the cost is a `TestDoubles/` folder somebody has to own.

**Fallback trigger.** `FakeItEasy` 9.0.1 if you prefer strict verification and dislike NSubstitute's extension-method-on-any-object syntax, which produces confusing failures when a substitute is accidentally a concrete type. Its issue tracker is the healthiest of the three (4 open).

**Per-provider test seams, because they differ and it matters:**
- **Azure/OpenAI:** `AzureOpenAIClientOptions` derives from `ClientPipelineOptions`, which exposes **`Transport`** — set `Transport = new HttpClientPipelineTransport(new HttpClient(stubHandler))` and drive the *real* client, its serialisation, its streaming parser, with no network and no mock.
- **Bedrock:** **AWS SDK v4 removed `DefaultClientConfig.HttpClientFactory`**; the guidance is `AWSConfigs.HttpClientFactory` — a **process-global**. Static mutable state means any test that sets it cannot run in parallel with any other that does. Both mitigations, not one: put those tests in a single non-parallel collection, **and** keep the count tiny by substituting `IAmazonBedrockRuntime` (which NSubstitute handles fine) for tests about request shaping.
- **LLamaSharp:** no HTTP. In-process P/Invoke, tiny model, trait gate.

For the small on-the-wire suites, **WireMock.Net 2.15.0** over cassette libraries: it is a real HTTP server, so it exercises the SDK's actual pipeline — retries, **SigV4 signing**, timeouts, chunked transfer — rather than short-circuiting the handler, and its record-via-proxy mode captures real traffic once into hand-editable JSON. **Add a CI check that greps recorded fixtures for `sk-`, `Bearer `, `AWS4-HMAC` and fails the build.** This is a secrets-handling application and the test fixtures are the likeliest leak path.

**UNCONFIRMED.** Whether `AWSConfigs.HttpClientFactory` exists and is honoured at runtime in `AWSSDK.Core` 4.0.102.x — confirmed only from the v4 migration guide's prose, not from the API reference or code.

### 3.20 Snapshot testing — `Verify.XunitV3` pinned at 32.0.0

**Needed for.** The heat map, the top-K grid, the probability map. *"Render the top-20 alternatives for token 7 as a table with a diverging colour scale"* is a 60-line block of ANSI; field-by-field assertions on it will not be written and will not be maintained. **This is the single most valuable testing technique for this app.**

**Options.** `Verify.XunitV3` 32.0.0; `Snapshooter.Xunit` 1.3.1; a hand-rolled golden-file helper.

**Decision. `Verify.XunitV3` 32.0.0, exact-pinned — plus `Spectre.Console.Testing` 0.57.2's `TestConsole` with pinned `TestCapabilities`.**

**Reasoning.** `TestCapabilities` is the critical part: you pin colour depth, Unicode support, ANSI on/off and width, so a heat map renders **identically on a Windows dev box and an Ubuntu CI runner**. Without that pinning, snapshot tests of coloured output are flaky by construction. Verify's `DiffEngine` integration makes accepting a changed heat map a one-keystroke operation, which is the difference between snapshot tests being loved and being deleted.

**The pin is a licence decision, made deliberately.** Verify carries an Open Source Maintenance Fee for revenue-generating organisations and government agencies from **1 September 2026**; versions released on or before that date are exempt in perpetuity, and **32.0.0 shipped 2026-08-26**. Tiers run $5–$100/month by headcount. From v33, `SponsorCheck` validates at build time — offline, bundled hashes, no network, an `SC0xx` **warning** rather than an error, which is a materially better design than SponsorLink. **But with `TreatWarningsAsErrors` (§3.22) an unlicensed-build warning becomes a build failure**, so you must either pin, sponsor, or `NoWarn` the SC code. Pinning is the lowest-friction choice; sponsoring is the right one if you lean on Verify.

**Given up.** Future Verify features and fixes, at 32.0.0.

**Fallback trigger.** Hand-roll if you refuse both the fee and the pin: `AssertSnapshot(actual, [CallerFilePath], [CallerMemberName])` writing `*.received.txt` beside `*.verified.txt`, honouring `ACCEPT_SNAPSHOTS=1`. About 40 lines. You lose scrubbers, the diff-tool launcher and parameterised naming — **small, because every snapshot here is a block of ANSI text.**

**Do not** reach for `Snapshooter.Xunit` as the free alternative without checking: 1.3.1 still declares dependencies on `xunit.core`/`xunit.assert` **≥ 2.4.2**, i.e. xUnit **v2**, and no `Snapshooter.XunitV3` was found.

**Assertions:** `xunit.v3.assert` is sufficient — no extra licence, no extra dependency. If you want `BeEquivalentTo`, use **`AwesomeAssertions` 9.6.0 (Apache-2.0)**, the community fork used by `dotnet/runtime`. **`FluentAssertions` 8.x requires a paid Xceed licence for commercial use**; 7.2.2 is the last Apache line. `Shouldly`'s stable is 19 months old.

### 3.21 Benchmarking — `BenchmarkDotNet` 0.15.8, separate project

**Needed for.** The softmax over a 128k-vocabulary logit vector, which runs **once per generated token**; top-K selection; probability-map construction; attribution; and heat-map rendering per frame.

**Options.** `BenchmarkDotNet`; nothing.

**Decision. `BenchmarkDotNet` 0.15.8 in a separate `benchmarks/` Exe project, Release-only, not in the `dotnet test` graph** (`Xcaciv.Command` already does exactly this with `src/tests/BenchmarkSuite1/`, so there is precedent in the stack you are building on).

**Reasoning.** There is no second-best worth naming — only "don't benchmark", and for an app whose selling point is per-token analysis over a full vocabulary that is not an option. What to measure and why:

| Benchmark | Why it is hot | Measure |
|---|---|---|
| Softmax / log-softmax over the full vocabulary | once per generated token; 32k–256k floats | ns/op, allocations, scaling by vocab size |
| Top-K selection from the logit array | per token, K user-configurable | full sort vs partial selection vs bounded heap — **the crossover K is worth knowing** |
| Probability-map construction | per token, for the map view | **allocations above all** — a naive LINQ chain allocates megabytes |
| Tokenize/detokenize round-trip | per request and per attribution query | throughput at realistic prompt sizes |
| Token→input-span attribution | O(tokens × spans) if naive | complexity behaviour, not constant factor |
| Heat-map render (Spectre) | per redraw, and redraws are frequent while streaming | time **and allocations per frame** |

`[MemoryDiagnoser]` on everything — for a streaming loop, an allocation per token is a GC pause mid-render, and that matters more than nanoseconds. `[DisassemblyDiagnoser(maxDepth: 2)]` on softmax and top-K specifically, to confirm the SIMD path was taken. `[Params(1000, 32000, 128256, 256000)]` for vocab and `[Params(1, 5, 20, 100)]` for K — **the shape of the curve is the finding, not any single number.**

**One divergence from the research.** `testing-quality.md` recommends both a `RuntimeMoniker.Net10` and a `NativeAot10` job, on the argument that "the app ships AOT" and JIT/AOT performance diverges sharply on tight `Span<float>` loops. **The app does not ship AOT (§5), so the AOT job measures a binary nobody runs.** Take the `Net10` job only. Keep the AOT job in your pocket for the day the `--no-plugins` SKU in §5 becomes real.

**Given up.** Nothing material.

**Fallback trigger.** None.

**Never run benchmarks in the PR gate.** Shared CI runners are too noisy for absolute numbers. For regression detection use `benchmark-action/github-action-benchmark` on a schedule from a consistent runner; **its default alert threshold is 200 %, which is far too loose — set 110–125 %** and treat alerts as *investigate*, not *block*.

**UNCONFIRMED.** Whether that action parses BenchmarkDotNet 0.15.8's current JSON exporter schema without modification — the action lists BDN among supported tools and ships an example, but the example was not checked against 0.15.8's output.

### 3.22 Publish mode — self-contained, single-file, ReadyToRun, **untrimmed**, per-RID

**Needed for.** "Download one file and run it", on Windows and Linux, with a plugin directory and a llama.cpp native tree beside it.

**Options.** Framework-dependent; self-contained folder; single-file self-contained; + ReadyToRun; + trimming; Native AOT; SDK container; `dotnet tool`.

**Decision.**

| Property | Value | Why |
|---|---|---|
| `TargetFramework` | `net10.0` | LTS to Nov 2028 |
| `PublishSingleFile` | `true` | One downloadable artifact |
| `SelfContained` | `true` | No runtime prerequisite |
| `PublishReadyToRun` | `true` | ~30 % of the JIT→AOT startup gap for zero code change |
| `PublishReadyToRunComposite` | `false` | Docs recommend it only for tiered-compilation-disabled apps; huge size and build cost |
| **`PublishTrimmed`** | **`false`** | **Mandatory** — plugins + trimming = removed host types and unresolvable plugin dependencies |
| **`PublishAot`** | **`false`** | **Mandatory** — see §5 |
| `EnableCompressionInSingleFile` | `false` default; `true` for an optional `-slim` asset | Trades launch latency for download size. **Measure before choosing** |
| `IncludeNativeLibrariesForSelfExtract` | `false` | Keep the llama.cpp `runtimes/` tree loose so LLamaSharp's device probing works and a user can swap a backend without a rebuild |
| **`IncludeAllContentForSelfExtract`** | **remove entirely** | Documented as *"not recommended… a .NET Core 3.1 compatibility mode [that] might be removed"*. **The source sets it `true`.** |
| **`SuppressTrimAnalysisWarnings`** | **remove entirely** | The source sets it `true` in both configurations — the single most dangerous line in its build |
| `EnableSingleFileAnalyzer` | `true` | Single-file **is** on the critical path; catches the `Assembly.Location`-returns-`""` class of bug at build time |
| `JsonSerializerIsReflectionEnabledByDefault` | `false` | §3.12 |
| **`InvariantGlobalization`** | **`false`** | The source sets `true`. This app renders Unicode token text, box-drawing tables and locale-formatted numbers, and `true` also changes string-comparison semantics |
| `SatelliteResourceLanguages` | `en` | Legitimate saving; keep |
| **`UseSystemResourceKeys`** | **`false`** | The source sets `true`, replacing framework exception messages with bare resource keys. **On a debugging tool, readable exception text is the product** |
| **`DebugType`** | **`portable`** (+ publish the `.pdb` as a release asset) | The source sets `none`, discarding line numbers. A debug shell should produce diagnosable crash reports |
| `<Content Update="plugins\**"> ExcludeFromSingleFile` | `true` | Plugins outside the bundle, resolved from `AppContext.BaseDirectory` |
| `ErrorOnDuplicatePublishOutputFiles` | leave `true` and **fix the layout** | Do not paper over `NETSDK1152` |

**Single-file API constraints to design against:** `Assembly.Location` returns `""`; `Assembly.CodeBase`/`EscapedCodeBase` throw `PlatformNotSupportedException`; `Assembly.GetFile(s)` throw `IOException`; `Module.FullyQualifiedName` returns `<Unknown>`. **Use `AppContext.BaseDirectory` and `Environment.ProcessPath`.** `EnableSingleFileAnalyzer` catches these.

**Reasoning on the numbers, read honestly.** The published head-to-head (minimal ASP.NET Core API, .NET 11 RC2, Ryzen 9 7950X) is JIT 118 ms / R2R 84 ms / AOT 37 ms startup, 41 / 39 / 18 MB working set. **That is a web-server benchmark.** This is an interactive REPL whose dominant cost is the first llama.cpp model load — seconds to tens of seconds for a multi-GB GGUF — or the first HTTPS handshake. **A 118 → 37 ms startup improvement is invisible next to loading a 4 GB model. Startup optimisation has a low ceiling here and must not be allowed to drive the packaging decision.** ReadyToRun buys the meaningful, free part.

**The sharpest packaging edge is `NETSDK1152`.** LLamaSharp's backend packages ship multiple CPU-feature variants of the same native filenames (`avx/ggml.dll`, `avx2/ggml.dll`, `avx512/ggml.dll`, plus CUDA). Publishing self-contained with more than one backend referenced produces *"Found multiple publish output files with the same relative path"*, and the documented workaround `ErrorOnDuplicatePublishOutputFiles=false` *"results in only one `ggml.dll` and one `llama.dll`, and it's not clear or predictable which one you end up getting"* — i.e. it may silently ship a variant that `SIGILL`s on an older CPU. [SciSharp/LLamaSharp#977](https://github.com/SciSharp/LLamaSharp/issues/977) was closed **not planned**. **The source references both `Backend.Cpu` and `Backend.Cuda12` unconditionally, so the rebuild inherits this collision on day one.**

**The workable shape:** do not let MSBuild flatten the backends. Keep `runtimes/<rid>/native/**` intact as loose files beside the executable and call `NativeLibraryConfig.Instance.WithSearchDirectory(Path.Combine(AppContext.BaseDirectory, "runtimes"))` at startup, letting LLamaSharp's own probing pick the variant. **Ship one backend family per release asset.**

**One more reason the RID must always be explicit.** The backend packages do **not** use NuGet's `runtimes/<rid>/native/` convention — they ship under a private `LLamaSharpRuntimes/` folder plus a props file that copies them as MSBuild `Content`, with a fallback branch that copies **everything** when `$(RuntimeIdentifier)` is empty. A plain `dotnet build` with no RID therefore copies every RID and every AVX variant of every referenced backend. In the source that is **~630 MB of output per project — including the unit-test project** — of which **two files, the Windows and Linux CUDA-12 GGML kernels, are ~550 MB (≈88 %)**. **Make a RID-less publish an MSBuild error.**

**Release assets per version:**

```
chatdbg-<ver>-win-x64.zip           # single-file exe + plugins/ + runtimes/ (CPU), signed
chatdbg-<ver>-win-x64-cuda12.zip
chatdbg-<ver>-win-arm64.zip         # NOTE: no llama.cpp native exists for win-arm64 — CPU shell only
chatdbg-<ver>-linux-x64.tar.gz
chatdbg-<ver>-linux-x64-cuda12.tar.gz
chatdbg-<ver>-linux-arm64.tar.gz
SHA256SUMS + SHA256SUMS.asc
chatdbg.<ver>.nupkg                 # multi-RID dotnet tool: self-contained + `any` fallback
ghcr.io/<org>/chatdbg:<ver>         # secondary; OCI, non-chiseled
```

Build Linux on the **oldest glibc** you intend to support — the binary runs on that version and newer, not older. `linux-musl-x64` is a separate RID **and a separate llama.cpp backend build**; only ship it if you promise Alpine.

**`dotnet tool` is a real second channel now.** .NET 10 added **multi-RID tool packages** (one package bundling per-platform binaries, self-contained flavour included), the **`any` RID** fallback so the tool still installs on platforms you never enumerated, and **`dotnet tool exec` / `dnx <tool>`** — npx-shaped, no install. Publish the self-contained platform-specific flavour plus the `any` fallback. **Do not publish the trimmed or AOT flavours** — same plugin argument.

**Signing.** Unsigned Windows binaries hit SmartScreen, which is a hard adoption blocker for a downloaded `.exe`. Use Microsoft's `sign` CLI over Azure Trusted Signing — **but note it has never had a stable release** (latest `0.9.1-beta.26371.2`, 2026-08-03; `--prerelease` is mandatory; Windows-x64-only; the older `Azure.CodeSigning` package was removed from NuGet). **Critically for single-file: sign the constituent binaries *before* they are bundled**, via the `PrepareForBundle` → `GenerateSingleFileBundle` extension point exposing `@(FilesToBundle)`, then sign the produced executable. Linux: SHA-256 sums, plus a detached GPG signature or a Sigstore attestation if the audience warrants it.

**Given up, concretely.** ~80 ms of startup, ~23 MB of working set, and a binary roughly 3–5× larger than an AOT equivalent. For an interactive shell that spends its first seconds mapping a multi-gigabyte GGUF into memory, this is the correct trade — **but it is a trade, and it is irreversible for as long as plugins exist.**

**Fallback trigger.** See §5's publish matrix and the two-SKU option.

**UNCONFIRMED.** Concrete startup and size numbers **for this app** in each mode. The 118/84/37 ms figures are from a different application. The source's own `docs/compact-build.md` claims (8–15 MB AOT, <100 ms) are undated, internally inconsistent (it references `net9.0` paths for a `net10.0` project) and unverified. **The only hard datum in the tree is `test-publish/Xcaciv.ChatDbg.Shell` at 15,677,171 bytes — a stripped linux-x64 ELF that clearly contains no llama.cpp payload.** Measure before choosing compression.

### 3.23 Containerization — SDK container publish, secondary channel

**Needed for.** CI use, scripted/headless invocation, and a reproducible llama.cpp backend environment.

**Options.** `dotnet publish /t:PublishContainer`; a hand-written Dockerfile.

**Decision. SDK container publish, `ContainerImageFormat=OCI`, non-chiseled base, as a clearly secondary channel.**

**Reasoning.** The .NET SDK creates container images **without Docker** — a daemon is needed only to *run* one locally. You can push straight to a registry or emit a tarball with `ContainerArchiveOutputPath`, which is exactly what a security-scanning CI stage wants. In .NET 10 console apps no longer need `EnableSdkContainerSupport`, and `ContainerImageFormat` (`Docker` | `OCI`) is new.

**Why secondary and not primary.** This is an interactive TTY application that reads and writes user-profile config. Containerising it means solving TTY allocation, volume-mounting the GGUF and the settings directory, and passing credentials in. Ship it, document `docker run -it --rm -v ~/.chatdbg:/root/.chatdbg -v /models:/models`, and **do not make it the recommended install.**

**Do not use a chiseled/distroless base.** A debug shell that cannot shell out is a debug shell with one hand tied.

**Given up.** Nothing — it is additive.

**Fallback trigger.** Write a Dockerfile only if you need a base image or layer layout the SDK properties cannot express.

**UNCONFIRMED.** `ContainerFamily` values and chiseled-image availability for .NET 10/11 were not enumerated. Only relevant if containers are pursued seriously.

### 3.24 CI — GitHub Actions, matrix over RIDs, MTP, signed

**Needed for.** Reproducible per-RID artifacts, a fast PR gate, and a release process that does not depend on a developer's laptop.

**Options.** GitHub Actions; Azure DevOps YAML.

**Decision. GitHub Actions.** A matrix over `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, with `windows-11-arm` / `ubuntu-24.04-arm` runners for the Arm64 legs (or R2R cross-compilation from x64, which is supported: a Windows-x64 SDK targets Windows x86/x64/Arm64 and Linux x64/Arm/Arm64).

**Six things the source's workflow gets wrong that must be fixed first:**

1. **It installs `dotnet-version: '9.0.x'` to build `net10.0` projects** pinned by `global.json` to a 10.0.100-rc SDK with `rollForward: latestFeature` — which will not roll *back*. This is unresolvable as written; it only ever worked because GitHub-hosted images ship extra SDKs preinstalled. **Pin the SDK in exactly one place — a valid `global.json` — and have CI honour it.**
2. **`global.json` is invalid JSON** (stray `}` at byte 95), which strict tooling rejects outright.
3. **It never runs `dotnet test`.** 100 xUnit facts and `coverlet.collector` have never executed in automation.
4. **It has no push or pull-request trigger at all** — `workflow_dispatch` only. There is no CI on commits.
5. **It builds only the console shell**, while the README markets the TUI as the product.
6. **It computes the artifact size into `$GITHUB_OUTPUT` from a step whose `id` is declared after the `run:` block**, and no later step consumes it. No size is ever published.

**The gate, in order:** restore with package-source mapping → build with `TreatWarningsAsErrors` → `dotnet test` (fast lane: `--filter-not-trait "Category=NativeInference"`) → publish per RID → sign Windows assets before bundling → checksum → upload. Native-inference tests run when the cached tiny model is present. Mutation, benchmarks, live-provider smoke and model-quality evals are scheduled, not gated.

**Central package management is mandatory, not optional.** One `Directory.Packages.props` with `ManagePackageVersionsCentrally` — which `Xcaciv.Command` and `Cupcake` already use, and which the source conspicuously lacks (it triplicates three package versions by hand across three csproj files). **Add `packageSourceMapping` from day one**: the Xcaciv.* packages are not on nuget.org, they come from `https://nuget.pkg.github.com/xcaciv/` (401 anonymously), CI needs a GitHub Packages token, and **source mapping is what prevents a same-named package appearing on nuget.org from being pulled instead.** That is a real supply-chain risk given the package IDs are currently unclaimed on the public feed.

**The compiler is the first quality gate.** `Directory.Build.props` at the repo root — the source's contains exactly one property (`<LangVersion>latest</LangVersion>`), which leaves nearly everything on the table:

```xml
<Nullable>enable</Nullable>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
<EnableNETAnalyzers>true</EnableNETAnalyzers>
<AnalysisLevel>latest-recommended</AnalysisLevel>
<EnableSingleFileAnalyzer>true</EnableSingleFileAnalyzer>
<Deterministic>true</Deterministic>
<ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
```
plus `Microsoft.CodeAnalysis.BannedApiAnalyzers` 5.6.0 and `Meziantou.Analyzer` 3.0.192, both `PrivateAssets="all"`.

**Note the divergence from the research:** `testing-quality.md` prescribes `IsAotCompatible=true` (which enables the trim, single-file and AOT analysers together) and `EnableTrimAnalyzer`/`EnableAotAnalyzer`. **Since the app ships untrimmed and non-AOT (§5), those analysers would produce a large volume of unfixable warnings against `Xcaciv.Loader`'s deliberate runtime type loading, and with `TreatWarningsAsErrors` that is an unbuildable repo.** Keep **`EnableSingleFileAnalyzer` only** — single-file *is* on the critical path, trimming is not. If the `--no-plugins` SKU in §5 ever ships, turn the rest on for that project alone.

**`BannedSymbols.txt` writes itself for this app**, and every entry enforces an architectural decision the tests depend on:

```
M:System.Console.WriteLine       ; all output goes through IIoContext / IAnsiConsole
M:System.Console.Write
P:System.Console.Out
T:Newtonsoft.Json.JsonConvert    ; source-generated System.Text.Json only
M:System.DateTime.get_Now        ; use TimeProvider so history timestamps are testable
M:System.DateTime.get_UtcNow
M:System.Environment.GetEnvironmentVariable(System.String)   ; go through the settings port
M:System.String.ToLower          ; culture bugs in tokenizer paths
```

Banning `Console.*` is what makes the render-to-string strategy hold — and it directly fixes a real defect: **Core makes 119 direct `Console.*` calls today, including three blocking `Console.ReadLine()` prompts inside `SettingsService`'s credential migration, which under a TUI that owns the terminal is a hang or a corrupted screen. Core is not actually headless.** Banning `DateTime.Now` in favour of `TimeProvider` (with `FakeTimeProvider` in `Microsoft.Extensions.TimeProvider.Testing`) is what makes history tests deterministic.

**Turn the analysers on at the start of the rebuild, not after.** `TreatWarningsAsErrors` + `latest-recommended` + Meziantou's 220+ rules will produce hundreds of diagnostics on day one; retrofitting them onto a finished codebase is how teams end up with a `NoWarn` list longer than the ruleset.

**Given up.** A green build on day one.

**Fallback trigger.** Azure DevOps if the org standardises there — with `DotNetCoreCLI@2`, never `VSTest@3`.

**UNCONFIRMED.** GitHub Actions Arm64 runner availability and pricing for public repos; it decides whether the Arm64 legs build natively or cross-compile.

---

## 4. The token-introspection problem

This is the application's reason to exist and its hardest technical constraint. Stated plainly:

> **The backends differ so sharply in what they will tell you about a token that treating them as interchangeable is not a simplification — it is a lie the UI would be telling the user. And the frontier hosted models are, in practice, the *worst* backends for this application's headline feature.**

### 4.1 Which backends can supply per-token log probabilities and top-K alternatives

**Can, fully:** local `LLamaSharp` (unbounded K = |V|, plus prompt-token logits and the full distribution) · OpenAI non-reasoning models (gpt-4o, gpt-4o-mini, gpt-4.1, gpt-4-turbo) at K ≤ 20 · Azure OpenAI non-reasoning deployments at **K ≤ 5** · Ollama at K ≤ 20 · Bedrock **Custom Model Import** with the OpenAI-shaped request, which is **the only hosted path that returns `prompt_logprobs`**.

**Cannot, at all:** Bedrock `Converse`/`ConverseStream` for **any** model — the response schema has no logprob field in either direction · Anthropic Claude anywhere, including on Bedrock (Anthropic's own OpenAI-compat table says request `logprobs` → *"Ignored"*, response `logprobs` → *"Always empty"*; the native Messages API has no such parameter) · Amazon Nova, Meta Llama, Mistral as hosted Bedrock foundation models · **OpenAI's entire reasoning line**.

**That last one is the headline risk.** Microsoft Learn's Azure reasoning-models page (`ms.date: 2026-08-20`) lists verbatim under **Not Supported**: *"`temperature`, `top_p`, `presence_penalty`, `frequency_penalty`, **`logprobs`, `top_logprobs`**, `logit_bias`, `max_tokens`"* — covering the entire GPT-5 series and the o-series. On the OpenAI side the record is worse and more granular: `gpt-5-chat-latest` returns *"You Are Not Allowed To Request Logprobs From This Model"*, and a March 2026 community thread reports that on **gpt-5.2, gpt-5.3-codex, gpt-5.4 and gpt-5.4-mini, `top_logprobs` ≥ 2 returns HTTP 500** while `top_logprobs=1` works — closed for inactivity on 2026-08-25 without a fix.

**A tool whose differentiating feature is token introspection is building on a capability its largest provider is deprecating in practice.** Three mitigations, all in the design: (a) make the **local backend the demonstration backend, not the fallback**; (b) route hosted introspection through gpt-4o/gpt-4.1-class deployments explicitly and **warn when the selected deployment is a reasoning model**; (c) instrument the probe command so you learn about withdrawals from telemetry rather than from bug reports.

### 4.2 Backend × capability matrix

| Backend | Chosen-token logprob | Top-K alternatives | Max K | Streaming logprobs | Prompt/input logprobs | Full vocabulary | Teacher-forced scoring | Attention |
|---|---|---|---|---|---|---|---|---|
| **LLamaSharp (local GGUF)** | ✅ | ✅ | **\|V\|** | ✅ | **✅** | **✅** | **✅** | ❌ |
| ORT GenAI (**preview**; ONNX, not GGUF) | ✅ | ✅ | \|V\| | ✅ | ✅\* | ✅ | ✅\* | ❌ |
| OpenAI — gpt-4o / gpt-4.1 (Chat Completions) | ✅ | ✅ | 20 (spec) | ✅ | ❌ | ❌ | ❌ | ❌ |
| OpenAI — Responses API (non-reasoning) | ✅ | ✅ | 20 (spec) | ✅ | ❌ | ❌ | ❌ | ❌ |
| **OpenAI — gpt-5.x / o-series** | **❌** | ❌ | — | ❌ | ❌ | ❌ | ❌ | ❌ |
| Azure OpenAI — non-reasoning deployments | ✅ | ✅ | **5** (runtime-enforced) | ✅ | ❌ | ❌ | ❌ | ❌ |
| **Azure OpenAI — reasoning deployments** | **❌** (documented unsupported) | ❌ | — | ❌ | ❌ | ❌ | ❌ | ❌ |
| Ollama (`/api/generate` **and** `/api/chat`) | ✅ | ✅ | **20** | ✅ | ❌ | ❌ | ❌ | ❌ |
| llama-server (`/completion`) | ✅ | ✅ | `n_probs`, no documented cap | ✅ | ❌ | ✗ practical | ❌ | ❌ |
| **Bedrock `Converse`/`ConverseStream` (any model)** | **❌** | ❌ | — | ❌ | ❌ | ❌ | ❌ | ❌ |
| Bedrock — Anthropic Claude | ❌ | ❌ | — | ❌ | ❌ | ❌ | ❌ | ❌ |
| Bedrock — Nova / Llama / Mistral (hosted) | ❌ | ❌ | — | ❌ | ❌ | ❌ | ❌ | ❌ |
| Bedrock — Cohere Command (**legacy** `command-text-v14`) | ✅ (`likelihood`) | ❌ | — | ✅ | ✅ (`return_likelihoods: "ALL"`) | ❌ | ✅ | ❌ |
| Bedrock — Custom Model Import (`BedrockCompletion`) | ✅ | ❌ (top-1 only) | 1 | ✅ | ❌ | ❌ | ❌ | ❌ |
| **Bedrock — Custom Model Import (OpenAI\* shapes)** | ✅ | ✅ | **UNCONFIRMED** | ✅ | **✅ `prompt_logprobs`** | ❌ | ✅ | ❌ |
| Bedrock — gpt-oss via `/openai/v1` | **UNCONFIRMED** | UNCONFIRMED | — | UNCONFIRMED | ❌ | ❌ | ❌ | ❌ |

\* model-export dependent — see §4.7.

Two entries need naming because they are counter-intuitive. **Azure's K ≤ 5 is runtime validation only** — the Azure v1 GA OpenAPI declares `top_logprobs: anyOf: [integer, 'null']` with no bound, so **you cannot discover the limit from the schema; you discover it from a 400.** And OpenAI's own docs are internally inconsistent: the TypeSpec says 0–20 with no `@maxValue`, while the public logprobs cookbook says *"between 0 and 5"*.

### 4.3 What the generic client abstraction drops, and how to recover it

`Microsoft.Extensions.AI` 10.9.0 has **no logprob concept anywhere** — the full public surface of `ChatOptions`, `ChatResponse`, `ChatResponseUpdate` and `UsageDetails` was enumerated, and a grep of the whole abstractions source tree for "logprob" returns nothing. So:

**Dropped on the way in.** There is no generic way to *ask* for logprobs. Recover via `ChatOptions.RawRepresentationFactory` — a `Func<IChatClient, object?>` whose result the adapter fills only the gaps of (`??=`), so your values win:

```csharp
new ChatOptions {
    RawRepresentationFactory = _ => new ChatCompletionOptions {
        IncludeLogProbabilities = true,
        TopLogProbabilityCount  = probedMaxK,     // 5 on Azure, 20 on OpenAI — probe it
    }
}
```
**Not `ChatOptions.TopK`** — that is top-k *sampling*, which changes what the model generates and returns you nothing.

**Dropped on the way out.** Logprobs arrive only inside `RawRepresentation`, as `OpenAI.Chat.StreamingChatCompletionUpdate.ContentTokenLogProbabilities` (per chunk) or `ChatCompletion.ContentTokenLogProbabilities`. Recovery is an unchecked downcast per update.

**Dropped on composition — three distinct losses, each verified:**
1. **`ToChatResponseAsync()` keeps one `RawRepresentation` and discards N−1.** The abstraction's own remarks say so. **Harvest per update.**
2. **`RawRepresentation` is `[JsonIgnore]`** on `ChatResponse`, `ChatResponseUpdate` and `ChatMessage`. Your history store will not persist it.
3. **`DistributedCachingChatClient` serialises through `System.Text.Json` over `typeof(ChatResponse)`, so every cache hit returns a logprob-free response** — the heat map goes blank for no visible reason.

**The recovery design, in three parts.**

**(a) A `TokenTelemetryChatClient : DelegatingChatClient` placed innermost**, adjacent to the provider:
```
UseLogging → UseOpenTelemetry → UseDistributedCache → UseFunctionInvocation → TokenTelemetry → <provider>
```
It calls `ConfigureRequest` on the way in and harvests **per streaming update** on the way out. Register it so `IChatClient.GetService<ITokenTelemetryExtractor>()` finds it, letting the shell query capability *before* the user asks for a heat map.

**(b) Project into `AdditionalProperties`, which is *not* `[JsonIgnore]`** and does serialise. This is the only carrier that survives caching and history persistence. Define an explicit DTO with a `JsonSerializerContext`; values return as `JsonElement` otherwise.

**(c) Persist provenance with the data, not just in the view model:**
```csharp
record TokenObservation(int Index, int? TokenId, string Text, byte[] Utf8Bytes,
    double LogProb, int Rank, IReadOnlyList<Alternative> TopK,
    double ResidualMass, double? Entropy, bool EntropyIsExact);
```
**Six months later nobody will remember which backend produced a given transcript.** `ResidualMass` and `EntropyIsExact` must be in the stored record.

**And for the deepest views, bypass the abstraction entirely.** Do not try to force a 128k-entry full-vocabulary distribution through `RawRepresentation`. Use `AsChatClient()` for the ordinary conversational path so the local model looks like every other backend, and a **direct `BatchedExecutor`/`Conversation` path underneath** for the introspection features no `IChatClient` shape can express.

### 4.4 The capability-degradation design

Five tiers, **declared** per backend (never discovered by `try/catch`), with a defined behaviour at each step:

| Tier | Capability | Backends | What the UI does when it is absent |
|---|---|---|---|
| **0** | Text only | any | Introspection commands **hidden**; `:caps` explains why |
| **1** | Chosen-token logprob | + Bedrock CMI (`BedrockCompletion`), Cohere Command | Heat map and output perplexity available; top-K views disabled |
| **2** | Top-K alternatives | + OpenAI/Azure (non-reasoning), Ollama, Bedrock CMI (OpenAI shapes) | Alternatives table shows **K and residual mass**; entropy shown as a **bounded interval** with a "truncated at K" badge |
| **3** | Prompt logprobs | + Bedrock CMI (OpenAI shapes), Cohere `ALL` | Prompt heat map and prompt perplexity available |
| **4** | Full vocabulary + teacher-forced scoring | **LLamaSharp, ORT GenAI** | Vocabulary map, **exact** entropy, and occlusion attribution; residual mass reads `0.000` |

**Two rules make this work in practice.**

**Every derived number carries its provenance.** An entropy value knows whether it is exact or a K-truncated bound. A probability knows its residual mass. The transcript stores both.

**Never fake a tier.** If the user asks for attribution on an Azure deployment, say *"attribution requires teacher-forced scoring, which Azure OpenAI does not expose; this is available on the local backend"* and offer to re-run the same prompt locally. **In a debugging tool, an honest refusal is worth more than a plausible number.** Do not ship a degraded "regenerate and diff" version under the same command name.

**Ship a `probe` command**, because three of the hard limits are only discoverable at runtime — Azure's K ≤ 5, gpt-5.x 500-ing at K ≥ 2, Bedrock gpt-oss unknown. On first use of a deployment, issue one tiny request at the assumed `MaxTopK`, halve on 400/500 until it succeeds, and cache the discovered value against the deployment name. **That converts three UNCONFIRMEDs into a runtime fact for each user's actual deployment**, and it is what `IntrospectionCapabilities.ProbeStatus` (`Verified` | `Assumed` | `Failed`) exists to record.

### 4.5 The maths, because getting it wrong is the failure mode

**Top-K normalisation is the single most commonly botched thing in these tools.** The K logprobs a provider returns are **absolute probabilities over the full vocabulary**, so `Σ p_i ≤ 1`. The residual `m = 1 − Σ p_i` is real mass belonging to the ~200,000 tokens you were not shown.

- **Do not silently renormalise.** Rendering `q_i = p_i / Σ p_j` makes a position where the top-5 hold 12 % of the mass look identical to one where they hold 99.8 % — destroying precisely the information a debugging tool exists to show.
- **Do** show absolute bars plus an explicit **"other (`m`)"** bar. If you offer a renormalised view for comparing the shortlist, label the toggle and put `m` in the header.
- On LLamaSharp, `m = 0` by construction. **Make that visible too** — "full vocabulary" vs "top-5 of 200,019" is exactly the backend-fidelity signal the user needs.

**Entropy.** `H_t = −Σ p log p`. Exact on local backends only; that is a headline capability no hosted backend can match. On top-K-only backends **you cannot compute it** — you can bracket it: `H ≥ H_K − m log m` (residual as one token) and `H ≤ H_K − m log(m/(|V|−K))` (residual spread uniformly). The bounds need `|V|`, which you know from the tokenizer, not the API. **Render an interval, or `H_K` with a "truncated at K=5" badge. Reporting a bare number computed from 5 of 200,000 tokens as "entropy" is indefensible in a tool that exists to be precise.** Normalised entropy `H / log|V| ∈ [0,1]` is the right thing for a sparkline, because it is comparable across vocabularies.

**Two sentinels that are not numbers.** OpenAI/Azure return **`-9999.0`** to mean "not in the top-20 window" — it is a marker, not a log probability; feeding it to `exp()` silently yields 0 and into a perplexity sum yields nonsense. **Filter it and render "outside top-K".** And **Cohere's `likelihood` is a log-likelihood despite the name**; its sequence-level value is documented as the *mean* token likelihood, so `PPL = exp(−likelihood)` directly.

**The temperature trap — decide and document it.** On LLamaSharp, `GetLogitsIth` gives raw logits *before* the sampler chain and `Softmax()` gives the T=1 distribution; read the array *after* `chain.Apply(...)` and you get the truncated, temperature-scaled one. **These are different numbers and a debugging tool should show both, side by side, clearly labelled.** For hosted APIs, whether returned logprobs are pre- or post-temperature is **UNCONFIRMED** — neither the TypeSpec, the cookbook, nor the Azure reference states it. **Flag it in the UI rather than guessing.**

Other per-position scalars worth having: margin `p₁ − p₂` (how close was the decision), **rank of the emitted token** (a great integer for a table — "emitted token was rank 7"), and sequence logprob for ranking candidates. Report perplexity over generated tokens, and separately over the prompt where the backend allows it — **prompt perplexity is the useful one for debugging**, because it tells you whether your carefully engineered system prompt reads as natural text or as line noise to this particular model.

### 4.6 Attribution — what is real, and what to cut from the PRD

**Cut attention visualisation.** Not achievable on any backend without forking C++. No hosted API returns attention. In llama.cpp the scores exist as an internal graph tensor with no supported route through the public C API — and **with flash-attention kernels enabled (the default on modern builds) the attention matrix is never materialised**, so there is literally nothing to read. You would fork llama.cpp, disable flash attention, add an export hook, and ship your own native binaries for two platforms: a whole second product. **And even then**, the interpretability literature is clear that raw attention is a poor explanation. Shipping an attention heat map as "why the model said this" sells the user a picture that does not answer their question.

**Cut gradient saliency and integrated gradients.** They need `∂logit/∂embedding`, i.e. reverse-mode autodiff over the model graph. `TorchSharp` 0.107.0 has real autograd but gives you tensors and ops, not architectures — you would re-implement Llama/Qwen/Phi forward passes in C# and write safetensors loading per architecture, a multi-month project that duplicates llama.cpp and forfeits GGUF quantisation. `Microsoft.ML.OnnxRuntime.Training`, the only "gradients in .NET without PyTorch" story, has a **latest stable of 1.19.2 published 2024-09-03** — roughly two years stale. Integrated gradients additionally needs 20–300 passes. **The honest answer to a stakeholder is "that is a Python/PyTorch tool, and it is a different product."**

**Ship occlusion attribution — it is real, model-agnostic, and it is the right answer.** It is a first-class method in `inseq`, the standard sequence-attribution library. **The detail most implementations get wrong: occlusion requires teacher-forced scoring of a *fixed* completion.** If you ablate a span and *re-generate*, you are measuring output volatility, not attribution — sampler noise swamps the signal. That single requirement is why the capability matrix matters, and why attribution is a **Tier-4 (local) feature**.

Three layers, increasing cost. Ship all three; label each honestly.

**Layer 1 — Prompt surprisal (1 forward pass, exact, *not* attribution).** Tokenize with the model's own vocab, build one `LLamaBatch` with `logits: true` at every position (chunked), decode, and for each position read `GetLogitsIth(t−1)`, softmax, take `−log p(x_t)`. Render as a heat map over the input. This answers *"which parts of my prompt did the model find unexpected"* — genuinely useful, cheap, and exactly true. **Do not label it attribution.**

**Layer 2 — Contrastive occlusion over spans (N+1 prefills — this *is* the attribution).** Generate `y` normally, recording per-token logprobs; the baseline `S₀ = Σ log p(y_t | x, y_<t)` is already in hand. Segment the prompt coarse-to-fine (messages → lines → sentences → words) by the user's zoom level. For each span, build `x_{−s}` — **offer both deletion and neutral-filler replacement, and say which you used, because they answer slightly different questions** (deletion shifts positions; replacement preserves them) — decode `x_{−s} ⧺ y` with logits at the `y` positions, and report `S₀ − S_s` **in nats per output token** so spans are comparable across completions of different length. Positive means the span supported the output; negative means it worked against it. **Also report per-position `D_KL(P_full ‖ P_ablated)`** — you already have full-vocab distributions locally so it costs nothing, and it captures "this span changed what the model was *considering*" even when the emitted token's score did not move. **Cache the KV state for the longest common prefix across ablations**; with a system prompt and history that is usually most of the context, this can cut cost by an order of magnitude.

**Layer 3 — Single-token counterfactual (2 prefills).** Given an output token `y_t` and an alternative `y'_t` from its top-K, attribute `log p(y_t) − log p(y'_t)` across spans. **This is what tells the user "the word 'urgent' in your prompt is why it chose 'immediately' over 'soon'" — which is the question people actually have.**

**Gate it behind an explicit cost estimate.** Cost is O(N) full prefills; without prefix caching a 40-span attribution over a 2k-token context is 80k+ tokens of prefill. Show *"47 spans × 1,850 tokens ≈ 87k tokens of prefill, ~12 s"* and require confirmation. This is a debugging shell; an expensive, explicit, opt-in command is correct.

**And put this sentence in the UI:** *"Attribution is measured by ablation: we re-score the same output with each input span removed. It is a causal measure of that span's contribution under this model, not a claim about the model's internal mechanism."* **That sentence is the difference between a credible tool and a plausible-looking one.**

### 4.7 Sharp edges specific to this feature

**Memory.** Full-vocab logits cost `4 × positions × |V|` bytes. Qwen3's 151k vocab over a 4,000-token prompt is **~2.4 GB** if you flag every position in one batch. **Chunk the prefill (e.g. 128 positions per decode), reduce eagerly to per-position surprisal + top-K, discard the rest, and never hold more than a window of full distributions in the conversation store.** Persist top-K plus scalars; recompute the full map on demand.

**Bedrock's prefix-cache interaction.** AWS states plainly that requesting prompt logprobs on Custom Model Import *"will ignore the prefix cache and recompute the prefill of full prompt."* In an interactive shell with a long, stable system prompt, turning on prompt logprobs converts every turn from a cached prefill into a full one. **Make it a per-command flag, never a global setting.**

**Cohere Command is a legacy escape hatch, not a plan.** `return_likelihoods` exists only on the old generation API; it does not exist on the Command R/R+ chat surface. Treat it as a demo that "Bedrock can do this for one model", not as the Bedrock story.

**The Ollama contradiction, resolved.** `token-introspection.md` and `local-inference.md` both verified against `ollama/api/types.go` on `main` that **both** `GenerateRequest` and `ChatRequest` carry `Logprobs`/`TopLogprobs` (0–20), shipped in v0.12.11 — while `OllamaSharp` 5.4.30 models them **only on the generate path** (grepping `Models/Chat/` for "logprob" returns nothing). `llm-abstraction.md`'s unconfirmed item #7 claims OllamaSharp ships typed logprobs generally, which contradicts the two direct source reads. **I am resolving in favour of the two grep-level verifications: the server supports it on both endpoints; the .NET client does not on chat.** So if Ollama is supported, either PR the field upstream or route through `/api/generate` with `Raw = true` and own the chat templating — **which for a tool that also wants to show tokenization is arguably the better architecture anyway, since it makes the exact prompt bytes visible to the user.** Also: [ollama#13638](https://github.com/ollama/ollama/issues/13638) reports **logprobs are not returned from Ollama Cloud**; probe rather than assume.

**What ORT GenAI would cost you, if it is ever reconsidered.** It is **preview** and its published C# API page is **stale** — it documents `ComputeLogits()` and a `Tensor.Data` property that do not exist in current source, so anyone implementing from the docs writes code that does not compile. Read `src/csharp/*.cs` directly and pin the exact version.

**UNCONFIRMED, collected** (each repeated in §8 with its experiment): max `top_logprobs`/`prompt_logprobs` on Bedrock CMI (AWS shows only `1` in every example and documents no bound; the response shapes suggest vLLM underneath, which would default to 20, but AWS does not say) · whether Bedrock's OpenAI-compatible endpoints honour logprobs for gpt-oss · whether Nova exposes anything via an undocumented `additionalModelRequestFields` key · AI21 Jamba on Bedrock (a genuine gap — not checked) · whether hosted logprobs are pre- or post-temperature · the gpt-5.1 `reasoning_effort: "none"` carve-out on Azure specifically · whether stock published ONNX model repos export logits for all prompt positions or only the last (`num_logits_to_keep`) · whether Ollama's OpenAI-compatible `/v1/chat/completions` exposes logprobs · **retirement dates for gpt-4o / gpt-4.1 on Azure — since these are the deployments that actually support logprobs, their retirement schedule is a direct risk to this feature and should be checked against the Azure model-retirements page before committing.**

---

## 5. The native-inference and packaging collision

### 5.1 The definitive answer

> **No. This application cannot be Native-AOT compiled.** And it is worth being precise about *which* half causes the failure, because the intuitive answer is wrong.

**The native llama.cpp half is fine.** Native AOT supports P/Invoke normally — *"The P/Invoke calls in AOT-compiled binaries are bound lazily at runtime by default, for better compatibility."* You can even opt into `<DirectPInvoke>`/`<NativeLibrary>` to statically link `libllama.a`, and LLamaSharp's `NativeLibraryConfig` machinery is ordinary `NativeLibrary.Load`-shaped work over file paths. **Loading `libllama.so`/`llama.dll` is not the blocker.**

**The plugin half is fatal.** Native AOT's documented limitations begin with **"No dynamic loading, for example, `Assembly.LoadFile`"** and include **"Requires trimming, which has limitations."** The mechanism, not the slogan: AOT compiles IL to machine code at *publish* time and ships **no JIT**. The image contains exactly the code ILC could prove reachable from the entry point, plus metadata for exactly those types. A plugin assembly discovered on disk at runtime contains IL that ILC never saw, so (a) nothing in the process can turn it into executable code — no JIT, no `Reflection.Emit`; (b) the generic instantiations, interface dispatch stubs and reflection metadata its types would need were never generated. `AssemblyLoadContext.LoadFromAssemblyPath` / `Assembly.LoadFile` therefore **throw `PlatformNotSupportedException`**, and `AssemblyDependencyResolver` — the type a well-built plugin loader uses to resolve a plugin's private dependencies from its `.deps.json` — **does not support NativeAOT** ([dotnet/sdk#42389](https://github.com/dotnet/sdk/issues/42389), still open as a feature request).

**This is not a bug awaiting a fix. It is the definition of the deployment model:** AOT trades away the ability to execute code that did not exist at compile time, in exchange for having no compiler at runtime.

**Trimming compounds it independently.** *"Trimming relies on seeing all assemblies at build time… Most plugin systems load third-party code dynamically, so it's not possible for the trimmer to identify what code is needed."* Even the **host** half is at risk: the trimmer will remove the very host types a plugin resolves against, because nothing statically references them. Since AOT **requires** trimming, you inherit this failure even if the loading problem were somehow solved.

**Three secondary blockers**, any one of which would be sufficient on its own:
- **`LLamaSharp` 0.27.0 declares neither `IsAotCompatible` nor `IsTrimmable`**, and its native payloads are MSBuild `Content` items that AOT will not bundle. `PublishAot=true` additionally **silently ignores `IncludeNativeLibrariesForSelfExtract=true`** ([dotnet/sdk#49995](https://github.com/dotnet/sdk/issues/49995)), and the SDK enters a broken state when `PublishAot` and `PublishSingleFile` are both set.
- **`Terminal.Gui` v1 → `System.Management` (WMI, Windows-only RID asset) → `System.CodeDom` (runtime code generation)** — among the least AOT-compatible parts of the BCL. (v2 drops `System.Management`, but pulls `TextMateSharp` → `Onigwrap`, which ships a **native `libonigwrap.so`/`.dll`** — so even the AOT-clean v2 UI stack is "one file plus a native", not one file.)
- **`WithToolsFromAssembly()` and `AIFunctionFactory.Create` over arbitrary `MethodInfo`** are reflection-shaped and need pre-generated `JsonTypeInfo`.

### 5.2 Why the source project appeared to get away with it

`src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj` declares a `Compact` configuration with `PublishAot=true`, `PublishTrimmed=true`, `TrimMode=full`, `IlcOptimizationPreference=Size`, `IlcGenerateStackTraceData=false`, `InvariantGlobalization=true`, `UseSystemResourceKeys=true` — **and `SuppressTrimAnalysisWarnings=true`**, which silences precisely the diagnostics that would have said whether the build was sound. It was survivable only because **the source has no plugin loading at all**: no DI container anywhere, all wiring `new`-in-constructor, commands hand-registered into a `Dictionary<string, ICommand>`.

**The rebuild changes exactly that premise.** And it never actually shipped: `Compact` and `SingleFile` **are not solution configurations** (the `.sln` declares only Debug/Release), the checked-in workflow **only ever publishes `SingleFile`**, and the one tracked binary in the tree — `test-publish/Xcaciv.ChatDbg.Shell`, 15,677,171 bytes, stripped linux-x64 ELF — is a SingleFile build that cannot contain a 275 MB CUDA kernel. **The AOT configuration existed on paper; the shipping pipeline never used it. Delete it, do not port it.**

Six settings the source set purely in service of that never-built configuration must not be carried forward: `SuppressTrimAnalysisWarnings`, `InvariantGlobalization`, `UseSystemResourceKeys`, `DebugType=none`, `IncludeAllContentForSelfExtract`, `TrimMode=full`. Each is separately wrong for a debugging tool (§3.22).

### 5.3 The publish matrix that follows

| Requirement | Consequence |
|---|---|
| Load plugin assemblies at runtime (`Xcaciv.Loader`) | **Requires a JIT ⇒ CoreCLR, not Native AOT** |
| Trim the app | **Impossible while plugins exist ⇒ `PublishTrimmed=false`** |
| One downloadable artifact | `PublishSingleFile=true` |
| No runtime prerequisite | `SelfContained=true` |
| Recover some startup | `PublishReadyToRun=true` |
| Plugins discoverable and swappable | **Outside the bundle**, in `plugins/`, via `ExcludeFromSingleFile`, resolved from `AppContext.BaseDirectory` |
| llama.cpp natives discoverable and swappable | **Outside the bundle**, `runtimes/<rid>/native/**` tree intact, `IncludeNativeLibrariesForSelfExtract=false`, `NativeLibraryConfig.WithSearchDirectory(...)` |
| One backend family per artifact | Dodges `NETSDK1152` (§3.22) |

```xml
<ItemGroup>
  <Content Update="plugins\**\*.dll">
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    <ExcludeFromSingleFile>true</ExcludeFromSingleFile>
  </Content>
</ItemGroup>
```

**Be honest in the marketing: this is "one executable plus a `plugins/` folder and a `runtimes/` tree", not literally one file.** That is a direct, unavoidable consequence of the two fixed constraints (runtime plugin loading, a native inference engine), and pretending otherwise would drive someone back to the AOT configuration that cannot work.

**Three escape routes, in preference order, if AOT ever becomes non-negotiable:**

1. **AOT core + MCP-only extensibility.** Genuinely elegant: **an AOT binary can still spawn MCP servers over stdio, because that is `Process.Start` and JSON, not assembly loading.** You give up Tier-1 in-process plugins entirely and need source-generated `JsonSerializerContext` for every tool schema. **If the product can live without in-process third-party plugins, this is the best answer** — smallest binary, strongest isolation, one SKU. It also happens to be the security posture §3.16 already recommends, which makes it a real option rather than a theoretical one.
2. **Two SKUs.** `chatdbg` (AOT, Tier 0 only, tiny, instant startup) and `chatdbg-full` (single-file JIT, Tiers 0–3). Doubles CI and support surface.
3. **AOT host + out-of-process plugin workers.** Preserves both properties at the cost of an IPC layer and process lifecycle management. Consider only if AOT becomes mandatory and MCP is insufficient.

**Fallback trigger for revisiting all of this:** startup latency becoming a genuine product requirement (e.g. the shell invoked per-keystroke by an editor integration), a JIT-prohibited deployment environment, or memory footprint mattering more than extensibility (18 MB vs 41 MB RSS).

**The one UNCONFIRMED that could overturn §5 entirely** is restated here because it is that important: **nobody verified that `Xcaciv.Loader` actually uses runtime `AssemblyLoadContext` loading.** Its README documents `using (var context = new AssemblyContext(dllPath, basePathRestriction: …)) { … } // Unload`, and its source shows `new AssemblyLoadContext(fullName, isCollectible)` with a `Resolving` handler — but no researcher had confirmed access to the published package or ran it. **If it turns out to use a compile-time source-generated registry instead, this entire section changes and AOT returns to the table. Confirm this before planning work.**

---

## 6. Architecture decision records

**Status vocabulary here:** *accepted* — decided, with the evidence to back it; *provisional* — decided by me because the research left it open or contradicted itself, and it should be re-examined at the named trigger; *deferred* — deliberately not decided yet, with the decision point named.

| ADR | Decision | Status | Consequence | Revisit when |
|---|---|---|---|---|
| **ADR-1** | Target `net10.0` (LTS), SDK pinned by a **valid** `global.json` in one place | accepted | No `ConfigurationIgnoreAttribute`, no `JsonNamingPolicy.PascalCase`. CI must honour the pin, unlike the source's `9.0.x` workflow | December 2026, one month after .NET 11 GA, and only for a named feature on the critical path |
| **ADR-2** | Generic Host + REPL as `BackgroundService` + custom `ReplLifetime : IHostLifetime` | accepted | First Ctrl+C cancels the turn, second quits. **Never `Environment.Exit`** — Cupcake's own entry point does, and it skips `finally` blocks | Only if graceful shutdown stops mattering, i.e. never |
| **ADR-3** | Keyed singletons + `IChatBackendResolver`; local backend registered via the **factory** overload | accepted | Multi-GB weights load on first `/model local`, not at host build. Typed keys, not strings | — |
| **ADR-4** | **Own `ITokenIntrospectingBackend` port is primary; `IChatClient` is an implementation detail underneath it** | **provisional** — three research files said own-port, one said MEAI-primary; **I decided** | Telemetry travels through unchecked downcasts, so one live integration test per backend is mandatory. `IChatClient` still earns its keep for tool calling and OTel | If MEAI ever grows typed logprobs (no open tracking issue exists — design as though it never will), **or** if fewer than ~60 % of calls survive without a downcast, at which point drop `IChatClient` entirely |
| **ADR-5** | Capability is a **declared, queryable value** (`IntrospectionCapabilities`), never a `try/catch`; ship a `probe` command that discovers real per-deployment limits and caches them | accepted | One table-driven contract suite covers all backends. The UI greys out and explains instead of failing at call time | — |
| **ADR-6** | **Never fake a tier.** An honest refusal beats a plausible number; every derived value carries provenance into the stored transcript | **accepted — ratified by owner 2026-08-29 as decision D-001 (`DECISIONS.md`)**, extended: capability-absent providers auto-disable the log-probabilities setting with a switch-provider instruction; the enabling UI is disabled where the platform allows it, and view attempts produce a non-blocking notice | Attribution is unavailable on Azure/OpenAI/Ollama and says so by name. Directly reverses the source's fabricated-telemetry behaviour (D10) | — |
| **ADR-7** | **Cut attention maps, gradient saliency and integrated gradients from the PRD.** Ship three honest layers: prompt surprisal, contrastive occlusion, single-token counterfactual | accepted | The differentiating attribution feature is **local-only** and gated behind an explicit cost estimate | Only if a .NET autodiff story appears that does not require re-implementing transformer architectures — `Microsoft.ML.OnnxRuntime.Training` is ~2 years stale |
| **ADR-8** | `LLamaSharp` 0.27.0 low-level `BatchedExecutor` path, **in the host's default ALC**, never inside a plugin context | accepted | `Prompt(tokens, allLogits: true)` is the only .NET mechanism for prompt-token logits — the whole basis of attribution. A native crash kills the shell, so **persist history before every inference call** | LLamaSharp 0.29.0 lands on NuGet (tagged 2026-08-24, not published): it carries the `BatchedExecutor` seq-ID fix and removes `LLamaSharp.SemanticKernel`/`kernel-memory`/`LLama.Experimental` |
| **ADR-9** | GPU backends are **opt-in per artifact**, never an unconditional `PackageReference`; publish always specifies a RID and a RID-less publish is an MSBuild error | accepted | Fixes the source's ~550 MB (88 % of output) unconditional CUDA payload, present even in the unit-test project. Ship one backend family per release asset to dodge `NETSDK1152` | — |
| **ADR-10** | `OpenAI` SDK against **both** api.openai.com and Azure `/openai/v1`; `Azure.AI.OpenAI` is **not** a dependency | accepted | One client type, one options type, one logprob path for both hosted backends. Accepts OpenAI 2.12.0 while the MEAI adapter pins `[2.12.0, 2.13.0)` | If the MEAI adapter's pin becomes a real blocker — then drop `Microsoft.Extensions.AI.OpenAI` and hand-write the `IChatClient` wrapper to unlock 2.13.0 |
| **ADR-11** | Bedrock is a **structurally degraded backend**, shown as such in the UI. Converse for chat; hand-written `InvokeModel` JSON for anything introspective | accepted | Verified by disassembly: zero `logprob`/`likelihood` strings in `AWSSDK.BedrockRuntime` 4.0.101.4 *or* `AWS.Bedrock.MEAI` 1.0.0. Claude — Bedrock's most-used family — has none upstream | Immediately after the `/openai/v1` gpt-oss probe (R2) — a positive result promotes part of Bedrock to a full-telemetry backend |
| **ADR-12** | **Store no secret by default.** Credential chains first; MSAL `Storage` for what must persist; **plaintext only behind an explicit flag with a permanent banner** | accepted | Reverses the source's env-var-first, plaintext-by-default order. Costs a persona: a developer handed a key with no Entra/IAM identity cannot self-serve | If a hand-rolled `ISecretStore` becomes necessary for size, OS-credential-UI visibility, or a security review |
| **ADR-13** | The settings type is **structurally incapable** of holding a secret — two types, two files, two stores, plus a save-time entropy scan that fails the write | accepted | Makes the source's worst defect unrepresentable rather than merely discouraged | — |
| **ADR-14** | Spectre-primary, Terminal.Gui v2 optional, `SpectreView` the only bridge; **three output modes — `rich` / `plain` / `data`** | accepted | A piped transcript works. `--format json` is a first-class path, which is both the accessibility mitigation and the single code path to audit for redaction | If the product vision becomes "an IDE in the terminal" — then invert and build a separate non-interactive entry point |
| **ADR-15** | **Commands never render.** Pure `TokenIntrospection → IRenderable` functions in one `ChatDbg.Presentation` assembly, consumed by two ~150-line `IIoContext` adapters | accepted | Forced by `Xcaciv.Command`'s string-only `OutputChunk`, and it is the right shape anyway: snapshot tests target pure functions at a fixed width | — |
| **ADR-16** | **Never `LiveDisplay`** for token streaming; `Progress`/`Status` only | accepted | Measured: `LiveDisplay` has no redirect fallback and corrupts every piped transcript. An integration test asserts no `\x1b` and no duplicated lines in redirected output | If Spectre adds a fallback renderer to `LiveDisplay` |
| **ADR-17** | Four-tier tool model; **MCP is the only route for third-party tools**; model-callable commands are a curated allowlist with approval gating and origin labelling | accepted | A loaded plugin can read the decrypted API key out of process memory; a child process cannot. `install` is never model-callable at any approval level | If MCP's cadence (nine releases in six months) becomes unmanageable — fall back to `StreamJsonRpc` and lose the ecosystem |
| **ADR-18** | `Xcaciv.Loader`: **one cached ALC per package for the session**, contract assembly asserted in the Default ALC at startup, host-provided assembly allowlist enforced | accepted | Fixes a real per-invocation ALC leak inherited from `CommandFactory`. The startup assertion prevents the single most confusing plugin bug | — |
| **ADR-19** | **No Native AOT, no trimming.** Self-contained + single-file + R2R, per RID, plugins and natives outside the bundle | accepted | ~80 ms of startup and ~23 MB of RSS given up, invisible next to a multi-GB model load. Delete the source's `Compact` configuration and its six supporting settings | **If `Xcaciv.Loader` turns out not to use runtime assembly loading (R1)** — then AOT returns. Otherwise: if startup becomes a product requirement, take the AOT-core + MCP-only option |
| **ADR-20** | Source-generated `System.Text.Json` everywhere, reflection disabled, plugin contexts composed via `TypeInfoResolverChain` | accepted | Not for trimming — for a compile-time-checked persistence contract that fails loudly. Preserve the observed wire format including `top_alternatives` and `logprob` | — |
| **ADR-21** | Explicit Polly pipeline, **never `AddStandardResilienceHandler()`**; `ResponseHeadersRead` + a separate stream-stall timeout; SDK-internal retries set to zero | accepted | The standard handler's 30 s total / 10 s attempt timeouts guillotine real generations, and its breaker (`MinimumThroughput = 100`/30 s) mathematically cannot trip at CLI volumes. Hedging is never enabled — it doubles the bill | — |
| **ADR-22** | Instrument unconditionally with BCL primitives; **construct no exporter at startup**; `--otlp-endpoint` is the only network path | accepted | The tool never opens a socket it was not asked to. Cost is computed locally from a versioned, user-editable price table defaulting to "unknown" | If GenAI semconv reaches Stable and a supported dashboard contract becomes worth depending on |
| **ADR-23** | xUnit v3 4.0.0 on MTP v2, repo-wide; hand-rolled fakes default; `Verify` pinned at 32.0.0; tiny-GGUF native tests trait-gated and skip-not-fail | accepted | **The "MTP is required for AOT tests" argument is retired** (ADR-19), but executable-first debugging and Crash/HangDump for llama.cpp carry the decision alone | If xUnit v3 4.0's runner rewrite proves unstable → roll back to 3.2.2, **which also flips you to MTP v1** |
| **ADR-24** | Enable `EnableSingleFileAnalyzer` **but not** `IsAotCompatible`/`EnableTrimAnalyzer`/`EnableAotAnalyzer`, alongside `TreatWarningsAsErrors` | **provisional** — diverges from `testing-quality.md`, which prescribed all four; **I decided** | Trim/AOT analysers against `Xcaciv.Loader`'s deliberate runtime loading would emit unfixable warnings, and with warnings-as-errors that is an unbuildable repo. Single-file *is* on the critical path | Turn the other three on for a `--no-plugins` SKU if one ever ships |
| **ADR-25** | `BenchmarkDotNet` with a `Net10` job only | **provisional** — the research prescribed `NativeAot10` as well; **I decided**, following ADR-19 | An AOT job would measure a binary nobody runs | Reinstate the AOT job if the AOT-core SKU ships |
| **ADR-26** | Ollama is **not** a first-class backend; if added, route introspection through `/api/generate` with `Raw = true` | **provisional** — resolves a three-way contradiction between the research files | Owning the chat templating makes the exact prompt bytes visible, which suits a tokenization-analysis tool. Ollama Cloud reportedly returns no logprobs | If `OllamaSharp` gains `ChatRequest.Logprobs` upstream |
| **ADR-27** | Ship a **containerised** secondary channel and a **multi-RID `dotnet tool`**; the GitHub Release binary stays primary | accepted | `dnx chatdbg` for SDK users at zero install cost. Never publish the trimmed or AOT tool flavours | — |
| **ADR-28** | **Deferred: whether the TUI ships at all in v1.** Spectre-primary works standalone; Terminal.Gui v2 is a rewrite of the source's three dialogs (~1,400 lines) and pulls a native `libonigwrap` | **deferred** | The only surface that genuinely *needs* it is the scrollable vocabulary map, which could ship as `--format json` + an external pager in v1 | At v1 scope-lock. The source never decided which shell was the product; **this rebuild must, up front** |
| **ADR-29** | **Deferred: whether `install <package>` ships at all.** It is the highest-risk feature in the product, and Cupcake's NuGet layer is a stub, not a component | **deferred** | If it ships: curated feed or ID-prefix allowlist, two-step confirmation with facts, shipped signed hash manifest as the trust root (**not** X.509 — a self-contained binary on a machine with no SDK has no certificate bundle), hash-pinned after install, never model-callable | Before any work on the package layer. Check `github.com/Xcaciv/Command.Packages` first — it may already contain the finished install logic |
| **ADR-30** | **Deferred: the licence question.** `Xcaciv.Command` and `Xcaciv.Loader` are AGPL-3.0; the subject is GPL-3.0; Cupcake is BSD-3-Clause | **deferred** | GPLv3 §13 permits the combination and the network clause is largely inert for a local binary — **but it stops being inert if ChatDbg ever grows a hosted mode.** This is a factual observation, not legal advice | Before distribution, and again before any hosted/served mode is designed |

---

## 7. What the source product got right, and what must change

Drawn from `tech-stack-observed.md`. The source is a 12,846-line codebase that a small team clearly cared about; several of its instincts are better than the code that implements them, and those instincts are worth naming rather than bulldozing.

### 7.1 Got right — keep these

1. **Clean one-way layering with zero cycles.** `Core` has **no** `ProjectReference` at all; both shells reference `Core` and nothing else. That is genuinely good architecture and the rebuild should preserve it exactly.
2. **The `IConsoleFormatter` instinct** — documented in-source as *"an abstraction layer to remove UI dependencies from Core"*, with a dependency-free `BasicConsoleFormatter` fallback so `Core` can render without Spectre. **The seam is right**; §3.11 keeps it and fixes the leak.
3. **Optional-constructor seams for testability.** `IAzureOpenAIClientFactory`, `IBedrockRuntimeClientFactory`, an injectable `HttpClient?` with a `_disposeHttpClient` ownership flag, and injectable base directories on both persistence services. Without a DI container, this is a thoughtful way to get testability, and it is why the test project can exist at all.
4. **Credential provenance reporting.** `GetCredentialSource()` returns which tier won — `"environment variable (X)"` / `"Windows Credential Manager"` / `"settings file (deprecated)"` / `"not set"`. **Most tools cannot tell you where their credential came from.** §3.5 promotes this from a diagnostic to a first-class `/status` output.
5. **Feature-detecting the Windows credential store rather than assuming it.** Every `WindowsCredentialManager` method opens with an OS check and returns `null`/`false` — no `DllNotFoundException` is ever thrown off-Windows. The *degradation target* is wrong (§7.2), but the *detection* is right.
6. **The migration instinct.** The code detects plaintext credentials in `settings.json` and warns. Wanting users off the insecure tier is correct; §3.4 keeps the flow and fixes the mechanism.
7. **Serialising native access with a process-wide semaphore.** llama.cpp contexts are not safe for concurrent use, and `_generationSemaphore`/`_modelLoadSemaphore` encode "at most one generation in flight per process". **Preserve the invariant** (drop the disposal bug).
8. **Locking the log buffer against the native callback.** `NativeLogConfig.llama_log_set` can fire on a thread you do not own, and the `_logLock` around the `StringBuilder` is correct. Preserve it, and the log file format.
9. **Offloading model load to `Task.Run`** so a multi-GB `LLamaWeights.LoadFromFile` does not block the caller. Keep it, and prefer `LoadFromFileAsync`.
10. **Explicit `[JsonPropertyName]` on every persisted field.** It makes the wire format a deliberate contract rather than an accident of property naming — which is why §3.12 can commit to preserving it byte-for-byte.
11. **The command surface itself.** `/`-prefixed dispatch, a tri-state `CommandResult{Success, Message, ExitRequested}` rendered as `✓`/`✗`, validated ranges (temperature 0–2, maxTokens 1–8192), `~/` expansion with `.json` extension defaulting. This is user-visible design, users have muscle memory for it, and it should survive.
12. **`Nullable=enable` and `ImplicitUsings=enable` on all four projects** from the start.
13. **The `SingleFile` configuration was substantially right** — `PublishSingleFile` + `SelfContained` + an explicit `-r win-x64` in the build script. It is the one publish configuration CI ever actually used, and §3.22 keeps its shape.

### 7.2 Must change — and why each one matters

1. **Plaintext secrets are the default destination, not a fallback.** `JsonAzureApiKey`/`JsonAwsAccessKey`/`JsonAwsSecretKey` have **no `[JsonIgnore]`** and are serialised into `~/.ChatDbg/settings.json`, while `UseWindowsCredentialManager` defaults to **`false`**. The comment directly above the save says *"The JsonIgnore attributes on properties will ensure they're not included"* — **that comment is false for exactly the three properties that matter.** The design is not "plaintext as last resort"; it is "plaintext unless the user opts out". → §3.5, §3.13 (ADR-13).
2. **The migration path prints live secrets to the terminal.** `instructions.Add($"  set CHATDBG_AZURE_API_KEY={settings.JsonAzureApiKey}")` writes an API key into scrollback, into any `script`/`tee`/tmux capture, into CI logs, and into shell history the moment it is pasted — **while teaching the user to store it in the weakest tier.**
3. **Every credential failure path is a silent fail-open.** Three `catch { return null; }` blocks make a locked store, an ACL problem and a marshalling bug **indistinguishable from "not configured"**, so resolution falls through to plaintext without a word. Fail closed with a diagnosable error; never fail open to a weaker tier.
4. **Fabricated telemetry presented as real.** When a provider returns no logprobs, `GenerateSimulatedLogProbs(text, topK)` **invents them and returns them as if measured**; `EstimateProbabilityFromTemperature` returns a hardcoded step function (0.95/0.85/0.75/0.60/0.50/0.40 by temperature band); `TokenAnalysis.TokenId` is always `-1`. **This is the single most misleading behaviour in the codebase and it is disqualifying for a tool whose product is measurement.** Cut it. ADR-6 replaces it with declared capability and honest refusal.
5. **The local top-K feature is silently dead, and its maths is wrong anyway.** The reflection probe for `LLamaContext.GetLogits` can never succeed (the method is on `NativeHandle`), so it returns an empty list behind a `catch { }`; and even if it worked, the softmax denominator covers only the top-K subset, systematically inflating the top token and making the numbers incomparable across steps or against a hosted provider. → §3.8.
6. **No `CancellationToken` anywhere in production code.** A repo-wide grep returns three hits, all in tests. A local generation with `MaxTokens` up to 8192 and no token is an **unabortable UI freeze**. → §3.2, and note that `Xcaciv.Command`'s `ICommandDelegate` has no token either, so you must smuggle a per-turn token through your own `IIoContext`/`IEnvironmentContext` implementation.
7. **`Core` is not headless.** 119 direct `Console.*` calls, including three **blocking `Console.ReadLine()` prompts inside `SettingsService`'s credential migration** — which, under a TUI that owns the terminal, is a hang or a corrupted screen. → banned-API list, §3.24.
8. **A ~550 MB unconditional CUDA payload.** `LLamaSharp.Backend.Cuda12` is an unguarded `PackageReference`, so two files (the Windows and Linux CUDA GGML kernels) are **~88 % of a 630 MB build output** — copied into every project including the unit-test project, on every platform. → ADR-9.
9. **An AOT configuration that cannot work, with the warnings switched off.** `PublishAot` + `TrimMode=full` + `SuppressTrimAnalysisWarnings=true` over reflection, `dynamic`, anonymous-type JSON serialisation and 630 MB of RID-probed natives. The docs even *prescribe the exact fixes* (`[DynamicallyAccessedMembers]`, STJ source generators) — **none of which were applied.** → §5.
10. **Settings written with no permission hardening, in the wrong place, with a `/tmp` fallback.** `File.WriteAllTextAsync` inherits the umask (commonly `0644` — world-readable, holding an API key); three different special folders for three artefacts of one app; and if the profile lookup throws, `settings.json` lands **silently in `Path.GetTempPath()`**, which on Linux is shared by every local user. → §3.4.
11. **Constructors that mutate the user's disk**, one of them sync-over-async, one of them **using `_jsonOptions` two lines before it is assigned** so the four default prompts are written un-indented while every later prompt is indented.
12. **`ChatSettings` is a DTO that performs I/O.** Reading `settings.AzureApiKey` reads environment variables and **P/Invokes into `advapi32.dll` from a property getter** — on every access, on the hot path of an interactive loop. Resolve once, at a boundary, and record provenance.
13. **Secrets live as `System.String` for process lifetime**, never cleared (and .NET strings are immutable and GC-relocatable, so copies persist), while `SetCredential` `FreeHGlobal`s the blob **without zeroing it first**, leaving plaintext in freed unmanaged memory.
14. **671 lines of dead near-duplicate.** `ChatDbg.Shell.Gui/ChatShell.cs` is a clone of the console shell that nothing instantiates, omits the `llama` provider, and still compiles into the binary dragging `Spectre.Console` and `Terminal.Gui` with it. **Do not port it.** Adjacent: three commands (`export-logs`, `export-analysis`, `show-analysis`) are implemented, tested, and **never registered by either shell** — all three are stubs returning *"Note: This command requires LLamaSharp provider integration."* Do not port them as features.
15. **The GUI shell never disposes its three `IAIService` instances** — the native model and context leak until process exit — while the console shell does. And `LLamaSharpService.Dispose(bool)` disposes a **static** `SemaphoreSlim` from an **instance** dispose, so the first disposal kills the process-wide semaphore for every other instance; the other static is never disposed at all.
16. **No CI on commits or pull requests.** `workflow_dispatch` only, **no `dotnet test` step anywhere**, a `9.0.x` SDK against `net10.0` projects, and an invalid `global.json`. 100 xUnit facts and a coverage collector have never run in automation. → §3.24.
17. **No Central Package Management.** Three package versions triplicated by hand across three csproj files; any bump must be applied three times or the projects silently diverge.
18. **`Console.WindowWidth` read unconditionally for layout in four places** — it **throws `IOException` when stdout is redirected or there is no TTY**. Piped output, CI, `nohup`. Not guarded anywhere.
19. **User-facing status glyphs are already mojibake in the committed source** — `"??  WARNING: Credentials found…"`, `"? Credential stored securely…"` — where `✓`/`⚠`/`🔑` were intended, in a file that `file` reports as Non-ISO extended-ASCII. **Do not faithfully reproduce `?`/`??` prefixes.** (Contrast the console shell, which is valid UTF-8 and prints real `✓`/`✗`.)
20. **The README says "for Windows terminal"; the csproj description says "Cross-platform chat debugging tool"; CI ships a Linux binary; the README markets the TUI while CI releases the console shell.** **The product never decided what it was.** The rebuild must decide up front (ADR-28).
21. **`AllowUnsafeBlocks=true` on Core with no `unsafe`, pointer or `stackalloc` anywhere** — a leftover from an abandoned low-level LLamaSharp interop attempt. Ironically, that abandoned attempt is exactly what §3.8 now recommends doing properly.

---

## 8. Risks and unknowns

Ranked by expected damage × likelihood. Every UNCONFIRMED marker from the nine research files is represented, every preview-dependent recommendation carries its fallback, and the six inter-file contradictions are recorded with how they were resolved. **Caveat repeated from §1: no completeness critique existed, so this list was assembled by me from the research and the observed stack. Treat it as a floor.**

### Tier 1 — Could invalidate a decision in this document

**R1. Nobody has confirmed that `Xcaciv.Loader` actually uses runtime `AssemblyLoadContext` loading. UNCONFIRMED.**
Flagged independently by the packaging and plugin researchers as *"the single most important thing to confirm before acting on this document."* **§5's entire conclusion — no AOT, no trimming — is conditional on it**, and so are ADR-19, ADR-24, ADR-25 and the publish matrix. Evidence for: the loader's README documents `using (var context = new AssemblyContext(dllPath, …)) { … } // Unload`, and its source shows `new AssemblyLoadContext(fullName, isCollectible)` with a `Resolving` handler. Evidence against: nobody could restore the package or run it.
**Resolve by:** reading `Xcaciv.Loader`'s source with confirmed access, or running a trivial `PublishAot=true` build that references it. **If it turns out to use a compile-time source-generated registry, §5 changes entirely and AOT returns to the table.**

**R2. Whether Bedrock's OpenAI-compatible endpoint honours `logprobs`/`top_logprobs`. UNCONFIRMED — and it is the highest-value 30-minute experiment on the list.**
`https://bedrock-runtime.{region}.amazonaws.com/openai/v1/chat/completions` and the `bedrock-mantle` variant accept the OpenAI SDK with a Bedrock API key, and AWS documents the request body **purely by reference to OpenAI's docs**, enumerating no supported or unsupported fields. **If it works, `gpt-oss-20b`/`120b` and the Qwen3 family become full-telemetry backends through the same `OpenAI.Chat.ChatClient` code path as OpenAI and Azure**, and ADR-11's "Bedrock is structurally degraded" softens materially.
**Resolve by:** one call with `IncludeLogProbabilities = true` against `openai.gpt-oss-20b`, checking whether `ContentTokenLogProbabilities` comes back populated, empty, or 400s.

**R3. OpenAI is visibly walking away from logprobs on its frontier line.**
Unsupported on **all** reasoning models per Microsoft's own docs (`ms.date` 2026-08-20); `gpt-5-chat-latest` returns *"You Are Not Allowed To Request Logprobs From This Model"*; on gpt-5.2 / 5.3-codex / 5.4 / 5.4-mini, `top_logprobs ≥ 2` returns **HTTP 500** while `top_logprobs=1` works, with the thread closed unresolved on 2026-08-25. **This application's differentiating feature depends on a capability its largest provider is deprecating in practice.**
**Mitigations, all in the design:** local backend is the *demonstration* backend, not the fallback; route hosted introspection through gpt-4o/gpt-4.1-class deployments explicitly and warn on reasoning models; instrument the `probe` command so withdrawals surface in telemetry rather than bug reports. **Related and unresolved:** the retirement dates for gpt-4o and gpt-4.1 on Azure — since these are the deployments that actually work, their schedule is a direct risk to the feature and **should be checked against the Azure model-retirements page before committing**.

**R4. The Xcaciv packages are not on nuget.org and restore is not reproducible.**
Verified 404 by four researchers against `api.nuget.org` for `Xcaciv.Command`, `.Core`, `.Interface`, `Xcaciv.Loader` and `Xcaciv.Cupcake`; a broad `q=Xcaciv` search returns only unrelated `XCBatch.*`. They come from `https://nuget.pkg.github.com/xcaciv/` (**401 anonymously**) and a local folder. **UNCONFIRMED whether a GitHub PAT can restore them, and what versions the feed actually holds** — the README says 3.3.0, the newest release tag is v3.3.1 (2026-02-12), and the repo was pushed 2026-07-26 after that tag.
**Consequence:** nobody outside the author can build this, and the package IDs are **unclaimed on the public feed** — a live supply-chain risk. **Mitigation:** `packageSourceMapping` from day one (§3.24), and either publish to nuget.org or vendor as submodules/`ProjectReference`.

**R5. `LLamaSharp` is the highest-severity single-package risk, because the distinguishing features live there.**
Pre-1.0 at 0.27.0 with **breaking changes between minor versions**; an irregular cadence (0.25.0 in Aug 2025, then nothing until Feb 2026 — a six-month gap); a **four-and-a-half-month llama.cpp lag** (binds `3f7c29d3` from 2026-04-16 while upstream is at b10687), so **any GGUF whose architecture landed after mid-April 2026 fails to load**; API docs that redirect to 0.25.0, so you read source not docs; and 0.29.0 tagged but unpublished, removing three sub-packages.
**Mitigations:** pin exactly; print the bound llama.cpp commit in `/version`; expose `WithLibrary(path)` as the escape hatch; budget for possibly maintaining a fork. **Fallback:** the `llama-server` subprocess — but that costs prompt-token logits and full-vocabulary maps, which is the whole point.

**R6. A native crash takes the shell down with the user's unsaved conversation.**
An access violation or `abort()` inside `ggml` — CUDA OOM, malformed GGUF, KV-cache overflow — terminates the process immediately; `try/catch` does not help because `SIGSEGV` from native code is not a CLR exception. Documented history: [#860](https://github.com/SciSharp/LLamaSharp/issues/860), [#1231](https://github.com/SciSharp/LLamaSharp/issues/1231), [#1091](https://github.com/SciSharp/LLamaSharp/issues/1091), plus the `BatchedExecutor` seq-ID overflow fixed only in the unpublished 0.29.0 — **which matters because `BatchedExecutor` is exactly the path recommended here.**
**Mitigation that costs nothing: persist conversation history to disk before every inference call, not after.** Add `Microsoft.Testing.Extensions.CrashDump`/`.HangDump` so CI produces a dump rather than a six-hour timeout. **Inversion trigger:** if the first month of dogfooding produces a worse crash log than expected, make `llama-server` the default and LLamaSharp the opt-in deep-introspection mode.

### Tier 2 — Will cost real schedule or produce wrong output

**R7. Preview and 0.x dependencies, each with its fallback.**

| Dependency | Status | Fallback if it fails |
|---|---|---|
| `LLamaSharp` 0.27.0 + backends | **GA (0.x)**, breaking minors | `llama-server` subprocess (loses prompt logits, full-vocab maps) |
| `Spectre.Console` 0.57.2 | **GA (0.x)**, no announced 1.0; 0.55.0 turned `Style` from class to struct | `Spectre.Console.Ansi` + hand-rolled widgets |
| `Terminal.Gui.Interop.Spectre` 2.4.17 | **GA but 7.1K lifetime downloads, 2 files, ~9 KB** | Own the ~200 lines of segment-walking. **Read them before depending on them** |
| `dotnet-stryker` MTP runner | **Preview** since 4.13; cannot map mutants to tests | Accept the preview, scope narrowly, keep `break` low (60) |
| `xunit.v3` 4.0.0 | GA, **two weeks old**; runner-stack rewrite | Roll back to 3.2.2 — **which also flips you to MTP v1** |
| `AWS.Bedrock.MEAI` 1.0.0 | GA, **17 days old, ~5,200 downloads**, replaces a fully-deprecated package | Hand-write the `IChatClient` wrapper over `AWSSDK.BedrockRuntime`. **You lose the adapter, not the capability** |
| `ModelContextProtocol` 2.2.0 | GA, **nine releases in six months** | `StreamJsonRpc` 2.25.29; lose the ecosystem |
| `sign` CLI 0.9.1-beta | **Prerelease only — never had a stable release**; Windows-x64 only | No alternative named. Signing infrastructure on a perpetual beta is a supply-chain risk worth naming in the release runbook |
| `Microsoft.ML.OnnxRuntimeGenAI` 0.15.2 | **Preview**, docs actively stale | Not taken; ruled out (cannot load GGUF) |
| `System.CommandLine` 3.0 line | **Preview**, seven previews, **no release notes or migration guide found anywhere** | Stay on 2.0.x, which is serviced monthly |
| `Verify` ≥ 33 | Maintenance fee + `SponsorCheck` warnings | **Pinned at 32.0.0** (pre-cut-off) |

**R8. `NETSDK1152` on multi-backend LLamaSharp publish is unresolved upstream.**
[SciSharp/LLamaSharp#977](https://github.com/SciSharp/LLamaSharp/issues/977) closed **not planned**. The documented workaround `ErrorOnDuplicatePublishOutputFiles=false` *"results in only one `ggml.dll` and one `llama.dll`, and it's not clear or predictable which one you end up getting"* — i.e. it may **silently ship a CPU-feature variant that `SIGILL`s on an older CPU.** The source references both `Backend.Cpu` and `Backend.Cuda12` unconditionally, so the rebuild inherits this on day one. **Needs a deliberate, tested layout — one backend family per artifact, `runtimes/` tree preserved — not a suppression flag.**

**R9. The GenAI semantic conventions are Development, in a repo that just moved, with a rename history.**
Nothing in `gen_ai.*` is Stable as of the 17 July 2026 snapshot. All `gen_ai.*` content left `open-telemetry/semantic-conventions` for `semantic-conventions-genai` at **v1.42.0 (2026-06-12)** — an organisational split, not a graduation. `gen_ai.system` → `gen_ai.provider.name` and `prompt_tokens`/`completion_tokens` → `input_tokens`/`output_tokens` already happened; more will. **Do not build user-visible features assuming attribute names are stable.** MEAI claims v1.41 conformance with an explicit experimental caveat, and does **not** emit the spec's `gen_ai.client.inference.operation.details` ActivityEvents. There is **no enum value for a locally-hosted llama.cpp model** — emit `llama.cpp` and document it. **UNCONFIRMED:** whether a release after that snapshot promoted anything to Stable; re-check at implementation time.

**R10. Memory blow-up on full-vocabulary introspection.**
`4 × positions × |V|` bytes. Qwen3's 151k vocab over a 4,000-token prompt is **~2.4 GB** if every position is flagged in one batch. **Chunk the prefill, reduce eagerly, never hold more than a window of full distributions**, persist top-K plus scalars and recompute on demand. Related: occlusion attribution is O(N) full prefills — a 40-span attribution over 2k tokens is 80k+ tokens of prefill without prefix caching. **Gate it behind an explicit cost estimate and confirmation.**

**R11. Windows-only build automation and a broken release pipeline, inherited.**
Three `.bat` + one `.ps1`, all hardcoding `-r win-x64`, all ending in `pause`; **no `.sh`, no `Makefile`** — while the docs advertise `linux-x64`/`osx-x64`/`osx-arm64` as supported. All four scripts and the docs reference **`net9.0` output paths** for `net10.0` projects, so the post-publish `dir` step looks in a directory that does not exist. Plus the six CI defects in §3.24. **Rebuild the pipeline; do not port it.**

**R12. `Terminal.Gui` v1 → v2 is a rewrite that lands inside this same effort.**
Namespaces split; `Application` moved from static to instance (`using IApplication app = Application.Create()`); `OnDrawContent(Rect)` → `OnDrawingContent(DrawContext?)` returning `bool`; `Rect`/`Bounds` → `Rectangle`/`Viewport`; `Driver.*` drawing forbidden from views; `Attribute.Make`/`ColorScheme`/`Colors.Base` → `new Attribute(...)`/`Scheme`+`VisualRole`; `NStack.ustring` → `string`; `ScrollView` removed; **`CanFocus` now defaults to `false`.** The source's `LogProbHeatmapView.cs` is 88 lines using **seven** removed APIs; `SettingsDialog.cs` (608 lines) and `SystemPromptsDialog.cs` (697 lines) are hit hardest. **Its docs also track `develop`, not the release** — three concrete API drifts were found in one afternoon. Seventeen patch releases in ten weeks after GA: pin exactly. **ADR-28 defers whether the TUI ships in v1 at all, largely because of this.**

**R13. Prompt injection through tool results is unsolved, and the app has the full lethal trifecta.**
Private data (credentials, history, local files) + untrusted content (any MCP server result) + an outbound channel (`export`, any network tool). OWASP's MCP cheat sheet and Microsoft's 2026 MCP security post both frame prompt injection, **tool poisoning (hostile instructions hidden in tool *descriptions* and metadata)** and confused-deputy as the core triad, and the MCP spec itself says tool annotations *"should be considered untrusted, unless obtained from a trusted server."* **Approval gating and origin labelling are mitigations, not fixes.** Note also that the repo's own `.mcp.json` uses `npx -y @upstash/context7-mcp@latest` — **`@latest` re-resolves every run, which is a rug-pull vector by construction.** Acceptable for a dev-time config; **unacceptable as a pattern for the shipped runtime config. Pin versions there.**

**R14. Analyser adoption will produce hundreds of diagnostics on day one.**
`TreatWarningsAsErrors` + `latest-recommended` + Meziantou's 220+ rules. **Turn them on at the start of the rebuild, not after** — retrofitting is how teams end up with a `NoWarn` list longer than the ruleset. And note the interaction: a future Verify `SC0xx` unlicensed-build warning becomes a build *failure* under warnings-as-errors (mitigated by the 32.0.0 pin).

### Tier 3 — Contradictions between research files, and how each was resolved

| # | The contradiction | Resolution |
|---|---|---|
| **C-A** | **Is MEAI or a custom port the primary abstraction?** `llm-abstraction.md` says `IChatClient` primary with a telemetry side-channel; `token-introspection.md` says explicitly *"do not route this feature through MEAI"*; `local-inference.md` and `testing-quality.md` independently reach the same own-port conclusion. | **Own port primary, MEAI underneath** (ADR-4). Three files to one; the researchers agreed on every *fact* and differed only on which layer to make primary. **The decision was mine.** |
| **C-B** | **Is Native AOT achievable?** `terminal-ui.md` **verified locally** that Terminal.Gui 2.4.17 + Spectre 0.57.2 + the interop AOT-publish with **zero IL/AOT warnings** and a working 21.8 MB binary. `packaging-observability.md`, `plugin-tooling-mcp.md` and `local-inference.md` all say AOT is categorically impossible. | **Both are true and not in conflict.** The UI stack in isolation is AOT-clean; the *application* is not, because of `Xcaciv.Loader` (and LLamaSharp's unannotated assemblies and `Content`-item natives). §5 stands. The terminal-ui finding is preserved as evidence for the AOT-core + MCP-only escape route in §5.3. |
| **C-C** | **`IncludeAllContentForSelfExtract`: true or false?** `local-inference.md` calls the source's `true` "correct", because LLamaSharp's natives are `Content` items that `IncludeNativeLibrariesForSelfExtract` alone will not bundle. `packaging-observability.md` says remove it — it is a **documented deprecated .NET Core 3.1 compatibility mode that "might be removed in a future release."** | **Packaging wins: set it `false` and remove it.** Do not bundle the natives at all — keep the `runtimes/` tree loose beside the executable and point `NativeLibraryConfig.WithSearchDirectory` at it (§3.22). That satisfies LLamaSharp's probing *and* avoids the deprecated mode, and it lets a user swap a backend without a rebuild. Cost: the artifact is a binary plus a folder, not literally one file. |
| **C-D** | **Does `OllamaSharp` support logprobs on the chat path?** `llm-abstraction.md`'s unconfirmed item claims 5.4.30 ships typed `Logprobs`/`TopLogprobs`; `local-inference.md` and `token-introspection.md` both grepped `Models/Chat/` on `main` and found **zero hits**, with the members present only under `Models/Generate.cs`. | **The two direct source reads win.** Server supports it on both endpoints (verified in `api/types.go`); the .NET client does not on chat. Route through `/api/generate` with `Raw = true` if Ollama is supported at all (ADR-26). |
| **C-E** | **Is MTP required?** `testing-quality.md`'s lead argument is *"MTP is the only platform that can run Native AOT and trimmed test hosts (the exact deployment mode this app ships in)"* — but §5 establishes the app ships **untrimmed and non-AOT**. | **The premise is retired; the decision stands on the remaining four reasons** — mechanical migration from the existing xUnit v2 suite, executable-first test projects (debuggable under `lldb`/WinDbg when llama.cpp segfaults), `[Theory]`/`MemberData` for table-driven token maths, and Crash/HangDump. **Stated explicitly in ADR-23 so nobody re-derives the retired argument.** |
| **C-F** | **Which analysers to enable?** `testing-quality.md` prescribes `IsAotCompatible=true` + `EnableTrimAnalyzer` + `EnableAotAnalyzer` alongside `TreatWarningsAsErrors`. | **Enable `EnableSingleFileAnalyzer` only** (ADR-24). Trim/AOT analysers against `Xcaciv.Loader`'s deliberate runtime loading emit unfixable warnings, and with warnings-as-errors that is an unbuildable repo. Single-file *is* on the critical path; trimming is not. **The decision was mine.** Turn the rest on for a `--no-plugins` SKU if one ever ships. Same logic retires the `NativeAot10` benchmark job (ADR-25). |

### Tier 4 — Remaining UNCONFIRMED items, by area

**Token introspection.** Max `top_logprobs`/`prompt_logprobs` on Bedrock Custom Model Import (AWS shows `1` in every example, documents no bound; response shapes suggest vLLM underneath, which would default to 20 — **resolve by probing**) · whether Amazon Nova exposes any logprob field via an undocumented `additionalModelRequestFields` key (absence of documentation, not documented absence) · **AI21 Jamba on Bedrock — a genuine gap, not checked at all** · whether hosted logprobs are pre- or post-temperature (**materially affects how you label the chart — flag it in the UI rather than guessing**) · the gpt-5.1 `reasoning_effort: "none"` carve-out on Azure specifically · whether Ollama's OpenAI-compatible `/v1/chat/completions` exposes logprobs ([#16117](https://github.com/ollama/ollama/issues/16117) is **closed as not planned**, contradicting secondary sources) · whether Ollama Cloud returns logprobs ([#13638](https://github.com/ollama/ollama/issues/13638) says no) · whether stock published ONNX model repos export logits for all prompt positions or only the last (`num_logits_to_keep`) · a precise upper bound on llama-server's `n_probs`.

**Local inference.** Whether the `noavx` `GGML_F16C`/`GGML_BMI2` bug ([#1407](https://github.com/SciSharp/LLamaSharp/issues/1407), fixed only in unpublished 0.29.0) affects 0.27.0 — **test explicitly if pre-AVX hardware must be supported** · actual published-folder sizes (all figures are compressed `.nupkg` `Content-Length`; **no publish was run**) · whether Vulkan covers macOS, and whether Metal is *enabled* rather than merely present in `Backend.Cpu`'s `osx-arm64` assets · whether llama.cpp ships prebuilt `llama-server` binaries for every target RID (relevant only on the subprocess route) · the precise current `NativeLibraryConfig` API surface in 0.27.0 (attested only via 0.12/0.14-era docs — **confirm against the assembly before writing loader glue**).

**Tokenization.** Whether `Microsoft.ML.Tokenizers` 3.0.0-preview adds a HuggingFace `tokenizer.json` loader · whether OpenAI's current lineup still uses `o200k_base` for gpt-5.x.

**Host / CLI.** `System.CommandLine` 3.0's contents and ship vehicle — **seven previews, no release notes, no migration guide, no announcement issue found anywhere** · `Xcaciv.Command`'s exact command-attribute *signatures* (names verified, constructors not) · whether `Xcaciv.Command`/`Loader`/`Cupcake` are trim- or AOT-safe · `Microsoft.Extensions.Http.Resilience`'s `IsAotCompatible` declaration · AWS SDK v4's exact `HttpClient` injection API on `AmazonBedrockRuntimeConfig` · `Spectre.Console.Cli` 1.0 timing (**alphas unlisted on nuget.org**) · `Xcaciv.Cupcake.Core.Loop`'s actual API.

**Secrets / config.** NativeAOT compatibility of MSAL (moot) · `JsonNamingPolicy.PascalCase` availability in .NET 10 (**verify against your SDK before using it**) · whether an in-box cross-platform credential API is *proposed* for .NET (none exists) · roaming-profile DPAPI master-key behaviour · whether AWS's `SharedCredentialsFile` writer applies restrictive permissions · **whether the Xcaciv stack already imposes a configuration/DI shape** — the secrets researcher named this "the single largest gap in this analysis", since the recommendations assume a stock generic host.

**Plugins / packaging.** Whether `ModelContextProtocol.Core` 2.2.0 is trim/AOT-annotated (moot) · whether `AIFunctionFactory.Create`'s overloads carry trim/AOT warnings (moot now, decisive if AOT returns) · NuGet client SDK trim posture · **what certificate bundle a *library* consumer gets when no SDK is installed** — the reason §3.16 recommends a shipped hash manifest instead of X.509 · whether `Xcaciv.Command`'s per-invocation ALC issue is fixed on `main` · whether `Command.Packages` already contains the finished install logic · the exact OpenTelemetry version that shipped `OTEL_SDK_DISABLED` · `ContainerFamily` values for .NET 10/11 · GitHub Actions Arm64 runner availability and pricing.

**Terminal UI.** **`AppModel.Inline` behaviour on the Windows driver and legacy conhost** — the CPR query may go unanswered and the startup gate then "proceeds from row 0", which would **overwrite the user's scrollback. Test on conhost before shipping inline mode** · whether `Canvas`/`BarChart` render correctly through `SpectreView` · `LiveDisplay` under a non-redirected but non-ANSI console · NativeAOT publish on `win-x64` for the UI stack (only `linux-x64` was exercised) · Terminal.Gui v1's formal support policy · **actual screen-reader behaviour under NVDA/JAWS/Orca — if accessibility is a stated requirement rather than a nice-to-have, this needs a real user test, not a literature review.**

**Testing.** Whether Stryker's MTP runner has graduated from preview in 4.16.0 · whether xUnit v3 4.0's AOT test execution works with a test assembly that P/Invokes into llama.cpp (**treat AOT test execution as a stretch goal, not a day-one requirement**) · whether `AWSConfigs.HttpClientFactory` is honoured at runtime in AWSSDK.Core 4.0.102.x · whether `Snapshooter` has an xUnit v3 package (**assume v2-only**) · whether `coverlet.mtp` 10.0.1 is maintainer-considered production-stable · TUnit's stability policy (**none published**) · exact on-disk sizes of the candidate tiny GGUFs · whether `github-action-benchmark` parses BenchmarkDotNet 0.15.8's current schema.

### Things this stack deliberately cannot do, and must say so

- **No attention visualisation, no gradient saliency, no integrated gradients.** Not on any backend, without forking C++ or re-implementing transformers in C#. If a stakeholder wants them, the honest answer is *"that is a Python/PyTorch tool, and it is a different product."*
- **No prompt-token introspection on Azure OpenAI, OpenAI, or Ollama.** Attribution and prompt perplexity are **local-only** (or Bedrock CMI). Say it by name in the UI; do not ship a degraded regenerate-and-diff under the same command.
- **No exact entropy on any top-K-only backend.** Bounded intervals with a "truncated at K" badge, or nothing.
- **No RTL or bidi text rendering.** Absent from both terminal libraries; a `bidi` issue search returns zero results in either repo.
- **No screen-reader support.** An ecosystem-wide gap. The mitigation is architectural — `--plain` and `--format json` as first-class paths, not a library swap.
- **No literal single-file artifact.** One executable plus a `plugins/` folder and a `runtimes/` tree, forced by runtime plugin loading and a native inference engine.
- **No local-LLM backend on Windows-on-ARM.** There is no `win-arm64` llama.cpp native in the backend packages. Ship a `win-arm64` shell if you like, but the local provider will not work on it.

---

*Written August 2026 against nine live-researched capability areas and the observed source stack. Every version number and capability claim traces to a researcher's citation dated 2026-08-28/29; every unverified claim is marked UNCONFIRMED and repeated in §8. The completeness critique that should have adversarially reviewed those nine areas did not exist at the time of writing — re-run it before this document is used to plan work.*
