# Application host, dependency injection, command-line surface and resilience

**Research date: 2026-08-28.** Every version number and publish date below was read live from the
nuget.org V3 API (`api.nuget.org/v3-flatcontainer/{id}/index.json` and
`api.nuget.org/v3/registration5-gz-semver2/{id}/index.json`) or from the named documentation page,
on that date. Where I could not confirm something, it is listed under **Unconfirmed** rather than
guessed at.

Grounding for "what this app does" comes from the actual subject tree at
`/mnt/g/3RD-Party/reversing/subject/chatdbg` and from the live Xcaciv repositories on GitHub, both
read during this research. Specific line references are given so the claims are checkable.

---

## Bottom line — the recommendation in three sentences

Build the app on the .NET Generic Host via `Host.CreateApplicationBuilder(args)`
(`Microsoft.Extensions.Hosting` **10.0.11**, GA, published 2026-08-11), run the REPL as a single
`BackgroundService`, and replace the source's hand-built `Dictionary<string, IAIService>` with
**keyed singletons** (`AddKeyedSingleton<IChatProvider>("azure" | "bedrock" | "local")`) resolved
through a thin `IChatProviderResolver` façade so the runtime `/model` switch stays testable.

Use **System.CommandLine 2.0.11** (GA since 2.0.0 on 2025-11-11) for *process startup arguments
only* — it is already in the dependency closure because `Xcaciv.Command` references
System.CommandLine 2.0.1 and ships a `CommandLineCommand<T>` adapter — and let **Xcaciv.Command**'s
attribute-driven system own every in-session command; both are warranted, but only with a hard
boundary between them.

Put every hosted-LLM call behind `IHttpClientFactory` + a **custom** resilience pipeline
(`Microsoft.Extensions.Http.Resilience` **10.9.0** over Polly **8.7.0**) rather than
`AddStandardResilienceHandler()`'s defaults, because the standard handler's 30 s total / 10 s
per-attempt timeouts will guillotine a long generation and its circuit breaker
(`MinimumThroughput = 100` per 30 s window) can never trip at single-user CLI volumes.

---

## Landscape — the real options

### Host, DI, options, HTTP (all from the `dotnet/runtime` + `dotnet/extensions` release trains)

| Package | Latest stable | Published | Status | Verdict |
|---|---|---|---|---|
| `Microsoft.Extensions.Hosting` | **10.0.11** | 2026-08-11 | GA, .NET 10 LTS servicing | The host. Take it. `11.0.0-preview.7.26381.103` also exists (2026-08-11) — **preview**, do not ship it. |
| `Microsoft.Extensions.Hosting.Abstractions` | 10.0.11 | 2026-08-11 | GA | What a library project should reference if it only needs `IHostedService`/`IHostApplicationLifetime`. |
| `Microsoft.Extensions.DependencyInjection` | **10.0.11** | 2026-08-11 | GA | The container. Keyed services are in-box. |
| `Microsoft.Extensions.Options` | 10.0.11 | 2026-08-11 | GA | Settings binding + `[OptionsValidator]` source-generated validation. |
| `Microsoft.Extensions.Http` | **10.0.11** | 2026-08-11 | GA | `IHttpClientFactory`, `AddHttpClient`, `AddAsKeyed`. |
| `Microsoft.Extensions.Http.Resilience` | **10.9.0** | 2026-08-11 | GA | Note the *different* version line: `dotnet/extensions` ships `10.x.y` monthly, not `10.0.x`. Depends on `Microsoft.Extensions.Resilience 10.9.0`. |
| `Microsoft.Extensions.Resilience` | 10.9.0 | 2026-08-11 | GA | Pulls `Polly.Extensions [8.4.2, )` and `Polly.RateLimiting [8.4.2, )` (dependency ranges read from the 10.9.0 catalog entry). |
| `System.Threading.Channels` | 10.0.11 | 2026-08-11 | GA, **in-box since .NET Core 3.0** | Do not add the PackageReference on `net10.0`; it is in the shared framework ([docs](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels)). |
| `System.Threading.RateLimiting` | 10.0.11 | 2026-08-11 | GA | Only if you want a client-side token bucket independent of Polly. |

.NET 10 is LTS: supported 2025-11-11 → 2028-11-14. .NET 11 is STS and launches at .NET Conf,
2026-11-10 ([dotnet/core release notes](https://github.com/dotnet/core/blob/main/release-notes/11.0/README.md),
[.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)).
Target `net10.0`. The subject's `global.json` currently pins `10.0.100-rc.1.25451.107` with
`rollForward: latestFeature` — that must move to a GA 10.0.1xx SDK band.

### Resilience

| Package | Latest stable | Published | Status | Verdict |
|---|---|---|---|---|
| `Polly` (metapackage) | **8.7.0** | 2026-06-10 | GA | v8 pipeline API. **No 9.x exists on nuget.org** — I enumerated the full flat-container version list and there is no `9.*`. |
| `Polly.Core` | 8.7.0 | 2026-06-10 | GA | The `ResiliencePipelineBuilder` engine. 8.7.0's headline change is caller cancellation-token propagation in the hedging and timeout strategies ([releases](https://github.com/App-vNext/Polly/releases)). |
| `Polly.RateLimiting` | 8.7.0 | 2026-06-10 | GA | Pulled transitively by `Microsoft.Extensions.Resilience`. |

Polly v7's `Policy.Handle<T>().WaitAndRetryAsync(...)` API is legacy; v8 pipelines are the current
shape and are what `Microsoft.Extensions.Http.Resilience` builds on.

### Command-line parsing

| Package | Latest stable | Published | Status | Verdict |
|---|---|---|---|---|
| `System.CommandLine` | **2.0.11** | 2026-08-11 | **GA** — 2.0.0 shipped 2025-11-11 alongside .NET 10 | Recommended for startup args. Monthly servicing (2.0.0 → 2.0.11 in nine months). |
| `System.CommandLine` 3.0 line | `3.0.0-preview.7.26381.103` | 2026-08-11 | **Preview** | A 3.0 preview line has shipped monthly since `3.0.0-preview.1` on 2026-02-10, tracking the .NET 11 train. **Do not adopt.** Fallback is simply staying on 2.0.x, which is serviced. |
| `Spectre.Console.Cli` | **0.55.0** | 2026-04-03 | GA-ish but still `0.x`, and now a *separate* project | Second-best. See the sharp edges below — it is explicitly unsupported under trimming/AOT. |
| `Spectre.Console` | 0.57.2 | 2026-07-02 | `0.x`, actively developed (repo pushed 2026-08-27) | Rendering only. Not a CLI parser. This is the sibling area's problem, listed here only because the CLI package split away from it. |
| `ConsoleAppFramework` | 5.7.13 | 2025-11-26 | GA, source-generator based, zero-reflection | Genuine third option and the most AOT-friendly of the lot. Loses: no shared vocabulary with Xcaciv.Command, and 9 months without a release. |
| `McMaster.Extensions.CommandLineUtils` | 5.1.0 | 2026-04-05 | GA, maintained | Fine library, but attribute model duplicates Xcaciv.Command's, and it brings nothing System.CommandLine lacks now that 2.0 is GA. |
| `Cocona` | 2.2.0 | **2023-03-27** | **Stale — 3 years 5 months without a release** | Do not adopt. State this as a risk if anyone proposes it. |
| `CommandLineParser` | 2.9.1 | **2022-05-17** | **Effectively abandoned — 4 years 3 months** | Do not adopt. |

**The Spectre.Console.Cli split is the surprise here.** As of Spectre.Console 0.54.0 (November
2025), `Spectre.Console.Cli` was moved to its own repository
([spectreconsole/spectre.console.cli](https://github.com/spectreconsole/spectre.console.cli), 79
stars, last pushed 2026-05-08) and de-coupled from the main version line
([PR #1928](https://github.com/spectreconsole/spectre.console/pull/1928),
[0.54.0 blog post](https://spectreconsole.net/blog/2025-11-13-spectre-console-0-54-released)). The
consequences are visible in the feed: `Spectre.Console` is at 0.57.2 while `Spectre.Console.Cli` is
stuck at 0.55.0 from April 2026, the latest *listed* prerelease is `0.55.1-alpha.0.7` (2026-05-08),
and the `1.0.0-alpha.0.5` … `1.0.0-alpha.0.16` packages that exist in the flat container are all
**unlisted** on nuget.org (registration entries show `listed=false`, `published=1900-01-01`, the
standard unlisted marker). A 1.0 is being worked toward but is not something you can take a
dependency on today.

### The Xcaciv stack (verified on GitHub — **none of these are on nuget.org**)

| Repo | Version | Last push | License | Notes |
|---|---|---|---|---|
| [`Xcaciv/Xcaciv.Command`](https://github.com/Xcaciv/Xcaciv.Command) | 3.3.x (`Xcaciv.Command*` 3.3.3 in `src/Directory.Packages.props`) | 2026-07-26 | **AGPL-3.0** | Targets `net10.0` by default, `net8.0` optional. Has a real `Xcaciv.Command.DependencyInjection` project. |
| [`Xcaciv/Xcaciv.Loader`](https://github.com/Xcaciv/Xcaciv.Loader) | 2.1.2 | 2026-07-26 | **AGPL-3.0** | Runtime type/assembly loading with per-plugin directory security policies. |
| [`Xcaciv/Xcaciv.Cupcake`](https://github.com/Xcaciv/Xcaciv.Cupcake) | — | 2026-02-24 | BSD-3-Clause | Consumes `Xcaciv.Command` 2.1.1 / `.Core` 2.1.0 / `.Interface` 2.1.0. |

I queried nuget.org for `Xcaciv.Command`, `Xcaciv.Command.Core`, `Xcaciv.Loader`, `Xcaciv.Cupcake`
and `Xcaciv.Configuration`: **all return "not found."** The repo's
[`NuGet.config`](https://raw.githubusercontent.com/Xcaciv/Xcaciv.Command/main/NuGet.config) confirms
they come from `https://nuget.pkg.github.com/xcaciv/index.json` and a local `G:\NuGetPackages`
folder, with package-source mapping. Treat that as a build-infrastructure prerequisite, not a
detail.

---

## Analysis

### 1. Generic Host for a console app

**Use `HostApplicationBuilder` via `Host.CreateApplicationBuilder(args)`.** The current
[Generic Host doc](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host) (last
updated 2026-07-01) is unambiguous: `IHostApplicationBuilder` "is recommended for new projects and
is the default in current .NET templates," while `IHostBuilder` / `Host.CreateDefaultBuilder` is
"this legacy approach [that] works best for maintaining compatibility with existing codebases."
There is no existing host to be compatible with here — `src/ChatDbg/Program.cs` is fourteen lines
that do `using var chatShell = new ChatShell(); await chatShell.RunAsync();` inside a try/catch.

`Host.CreateApplicationBuilder(args)` gives you, per that doc:

- content root = `Directory.GetCurrentDirectory()`;
- host config from `DOTNET_`-prefixed env vars and command-line args;
- app config from `appsettings.json`, `appsettings.{Environment}.json`, User Secrets in
  `Development`, env vars, command-line args;
- Console / Debug / EventSource / EventLog(Windows) logging providers;
- **scope validation and `ValidateOnBuild` when the environment is `Development`.**

That last one matters more than it sounds: it is what will catch the captive-dependency mistakes
this design is otherwise prone to (a singleton REPL holding a scoped `HttpClient`).

**The REPL as a hosted service.** Register one `BackgroundService` whose `ExecuteAsync(CancellationToken stoppingToken)`
is the read-eval-print loop. `BackgroundService` is the right base class rather than raw
`IHostedService` because `ExecuteAsync` is allowed to run for the life of the process, whereas
`IHostedService.StartAsync` is expected to return promptly.

**Shutdown.** Defaults, confirmed from
[`HostOptions.cs` in dotnet/runtime](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Hosting/src/HostOptions.cs):

| Property | Default |
|---|---|
| `ShutdownTimeout` | `TimeSpan.FromSeconds(30)` |
| `StartupTimeout` | `Timeout.InfiniteTimeSpan` |
| `ServicesStartConcurrently` | `false` |
| `ServicesStopConcurrently` | `false` |

Ordering, quoted from the Generic Host doc, is: on start
`IHostedLifecycleService.StartingAsync` → `IHostedService.StartAsync` →
`IHostedLifecycleService.StartedAsync` → `IHostApplicationLifetime.ApplicationStarted`; on stop
`ApplicationStopping` → `StoppingAsync` → `StopAsync` → `StoppedAsync` → `ApplicationStopped`.
`ConsoleLifetime` is the default `IHostLifetime` and handles **SIGINT (Ctrl+C), SIGQUIT
(Ctrl+Break / Ctrl+\\) and SIGTERM** gracefully; since .NET 6 it uses real POSIX signal handling and
"no longer gets involved when `Environment.Exit` is invoked."

**Two concrete instructions that follow from that:**

1. **Never call `Environment.Exit`.** The doc is explicit — `Environment.Exit` raises `ProcessExit`
   and then exits; "the end of the `Main` method doesn't get executed. Background and foreground
   threads are terminated, and `finally` blocks *aren't* executed." For this app that means an
   in-flight `ChatHistoryService` save or a settings write can be lost. Use
   `IHostApplicationLifetime.StopApplication()`. **The pattern you are asked to follow gets this
   wrong**: `Xcaciv.Cupcake`'s real entry point,
   [`src/Xcaciv.Cupcake.Lit/Program.cs`](https://raw.githubusercontent.com/Xcaciv/Xcaciv.Cupcake/main/src/Xcaciv.Cupcake.Lit/Program.cs),
   catches and then calls `Environment.Exit(1)`.
2. **Ctrl+C must mean two different things.** In a chat shell, the first Ctrl+C during a generation
   should cancel *that generation* and return to the prompt; only a Ctrl+C at an idle prompt (or a
   second one within a couple of seconds) should quit. `ConsoleLifetime` unconditionally maps SIGINT
   to "stop the host," so you cannot get this by adding a `Console.CancelKeyPress` handler alongside
   it — you get two registrations with no defined precedence. The clean answer is the seam the doc
   itself names: **`IHostLifetime`, where "the last implementation registered is used."** Register
   your own `ReplLifetime : IHostLifetime` that owns the `PosixSignalRegistration` for SIGINT,
   cancels the current-turn `CancellationTokenSource` on the first press, and calls
   `StopApplication()` on the second (or when no turn is in flight). It still delegates SIGTERM /
   SIGQUIT straight to shutdown.

**Token propagation.** There are three tokens, and conflating them is the classic bug:
`stoppingToken` (process is going down), the per-turn token (user pressed Ctrl+C or `/stop`), and
the per-request token (a provider timeout). Link them with
`CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, turnCts.Token)` and pass the linked
token down; dispose the linked source per turn or you leak registrations on the long-lived
`stoppingToken` for the life of the shell — a real leak in a process that stays open for hours.

**The source has none of this.** Grepping `src/**/*.cs` for `CancellationToken` outside the test
project returns exactly nothing. `IAIService` is
`Task<string> SendMessageAsync(ChatHistory, ChatSettings)` /
`Task<AIResponse> SendMessageWithLogProbsAsync(ChatHistory, ChatSettings)` — fully buffered, no
token, no streaming. Every recommendation in section 6 is a net-new capability, not a port.

### 2. Dependency injection and keyed services

The source's provider table is literally a dictionary, in three places:
`src/ChatDbg/ChatShell.cs:18` and `:29`, `src/ChatDbg.Shell.Gui/Program.cs:22`,
`src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:17` and `:51`. Keyed DI replaces all of it.

Registration and consumption, per the
[DI overview](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#keyed-services):

```csharp
services.AddKeyedSingleton<IChatProvider, AzureOpenAIProvider>(ProviderKeys.Azure);
services.AddKeyedSingleton<IChatProvider, BedrockProvider>(ProviderKeys.Bedrock);
services.AddKeyedSingleton<IChatProvider, LlamaProvider>(ProviderKeys.Local);
```

The doc notes the key "isn't limited to `string`. The key can be any `object` you want, as long as
the type correctly implements `Equals`." **Use a small enum or a `readonly record struct ProviderKey`
rather than bare strings** — keyed DI has no compile-time key checking, and a typo in a string key
is a runtime failure with a fairly unhelpful message.

**Resolving by a runtime-chosen key** — which is exactly what `/model bedrock` does — is a
`GetRequiredKeyedService` call, not an injected `[FromKeyedServices(...)]` parameter, because the
key isn't known at compile time:

```csharp
sealed class ChatProviderResolver(IServiceProvider sp) : IChatProviderResolver
{
    public IChatProvider Get(ProviderKey key) => sp.GetRequiredKeyedService<IChatProvider>(key);
    public IEnumerable<IChatProvider> All() => sp.GetKeyedServices<IChatProvider>(KeyedService.AnyKey);
}
```

Wrap it in that one-method interface rather than injecting `IServiceProvider` all over the REPL, so
`/model` is unit-testable without a container.

Three sharp edges here that are current as of .NET 10:

- **`KeyedService.AnyKey` semantics changed in .NET 10 — a documented breaking change**
  ([compat doc](https://learn.microsoft.com/en-us/dotnet/core/compatibility/extensions/10.0/getkeyedservice-anykey)).
  `GetKeyedService()` (singular) with `AnyKey` now **throws `InvalidOperationException`**; and
  `GetKeyedServices()` (plural) with `AnyKey` no longer returns `AnyKey` registrations, only
  services registered with a *specific* key. The plural form with `AnyKey` is exactly what you want
  for `/model` with no argument, i.e. "list the providers I know about" — and it now returns the
  right thing. The singular form is a trap.
- **Lifetimes.** Singleton is correct for the providers in a long-lived single-user interactive
  process. The local GGUF provider is the one that needs care: loading multi-gigabyte weights must
  not happen at host build time. Use the factory overload — `AddKeyedSingleton<IChatProvider>(key,
  (sp, key) => new LlamaProvider(...))` — which is only invoked on first resolve, so the model
  loads the first time the user actually selects it. Keep `LLamaWeights`/`LLamaContext` behind that
  and dispose them in the provider's `IAsyncDisposable`, which the host will honour via
  `IHost.StopAsync` → container disposal.
- **Scopes in a process with no request boundary.** There is no ambient scope in a REPL. If you
  want per-turn services (per-turn correlation id, per-turn scratch buffers), inject
  `IServiceScopeFactory` and `using var scope = factory.CreateScope()` per turn — the DI doc calls
  this out specifically for hosted services: "*don't* inject the service dependencies via
  constructor injection. Instead, inject `IServiceScopeFactory`, create a scope, then resolve
  dependencies from the scope."

**Xcaciv.Command already speaks this language.** Its
[`ServiceCollectionExtensions`](https://raw.githubusercontent.com/Xcaciv/Xcaciv.Command/main/src/Xcaciv.Command.DependencyInjection/ServiceCollectionExtensions.cs)
exposes `AddXcacivCommand(this IServiceCollection)`, an `Action<CommandControllerOptions>` overload,
and an `IConfiguration` overload that binds `CommandControllerOptions` and `PipelineOptions` by
section name. It registers `ICommandRegistry`, `ICommandFactory`, `ICommandExecutor`,
`IPipelineExecutor`, `ICommandLoader`, `ICrawler`, `IVerifiedSourceDirectories`, `IAuditLogger`,
`IOutputEncoder`, `IHelpService` and `ICommandController` — all as **singletons**, all via
`TryAdd*`, so your own registration wins if you register first. It also offers
`WithAuditLogger<T>()` and `WithStructuredAuditLogging()` which use `services.Replace(...)`. So
`builder.Services.AddXcacivCommand(builder.Configuration)` drops straight into the host builder.

### 3. Command-line parsing: where each library belongs

**System.CommandLine's status, precisely.** The long preview is over. Publish dates read from the
registration feed:

| Version | Published | Note |
|---|---|---|
| `2.0.0-beta4.22272.1` | 2022-06-02 | then a **three-year gap** |
| `2.0.0-beta5.25306.1` | 2025-06-19 | the API redesign |
| `2.0.0-beta6`, `-beta7` | 2025-07-15, 2025-08-12 | |
| `2.0.0-rc.1`, `-rc.2` | 2025-09-09, 2025-10-14 | |
| **`2.0.0`** | **2025-11-11** | **GA, with .NET 10** |
| `2.0.11` | 2026-08-11 | current stable, monthly servicing |
| `3.0.0-preview.1` … `-preview.7` | 2026-02-10 … 2026-08-11 | preview line, .NET 11 train |

The redesign landed in `2.0.0-beta5`: `SetHandler` became **`SetAction`**, `IConsole` was removed,
and the model became `ParseResult`-centric with `SynchronousCommandLineAction` /
`AsynchronousCommandLineAction` and a `CommandLineConfiguration`. Anything you remember from
beta1–beta4 tutorials is wrong. The
[migration guide](https://learn.microsoft.com/en-us/dotnet/standard/commandline/migration-guide-2.0.0-beta5)
is the reference. Beta 4 was also where trimming/Native-AOT support landed
([announcement issue #1750](https://github.com/dotnet/command-line-api/issues/1750)) — relevant
because this app already publishes with `PublishAot` (see below).

**The decisive fact for this application: System.CommandLine is already in the closure.**
`Xcaciv.Command`'s
[`src/Directory.Packages.props`](https://raw.githubusercontent.com/Xcaciv/Xcaciv.Command/main/src/Directory.Packages.props)
pins `System.CommandLine 2.0.1`, and the repo ships
[`Xcaciv.Command.Extensions.Commandline`](https://raw.githubusercontent.com/Xcaciv/Xcaciv.Command/main/src/Xcaciv.Command.Extensions.Commandline/readme.md),
"a thin adapter for running `System.CommandLine` commands inside the `Xcaciv.Command` pipeline. Use
`CommandLineCommand<T>` to wrap your `System.CommandLine.Command` instances and expose them as
`ICommandDelegate` implementations" — and its sample uses `SetAction((ParseResult parseResult) => …)`,
i.e. the 2.0 GA API. Choosing System.CommandLine for startup args therefore adds **zero new
dependencies** and lets a command be defined once and surfaced in both places.

**In-session commands belong to Xcaciv.Command, full stop.** Its attribute set —
`CommandRegisterAttribute`, `CommandRootAttribute`, `CommandParameterNamedAttribute`,
`CommandParameterOrderedAttribute`, `CommandFlagAttribute`, `CommandParameterSuffixAttribute`,
`CommandHelpRemarksAttribute` — plus `ICommandParameter` / `IParameterValue<T>` with `GetValue<T>()`
already covers `/model`, `/prompt`, `/set`, `/tokenize`, `/logprobs`, `/inspect`, `/export`,
`/import`, `/inject`, `/pop`, `/clear` — every command the test project
`src/Xcaciv.ChatDbg.Core.Tests/Commands/` names. It also does something System.CommandLine does not:
`|` pipelines between commands, which is genuinely useful for `tokenize input.txt | logprobs |
export --csv`.

**Are both warranted? Yes, with this rule.** *If it must be decided before the host is built, or it
decides whether the REPL is entered at all, it is System.CommandLine. Everything typed after the
prompt appears is Xcaciv.Command.* Concretely, System.CommandLine owns:

```
chatdbg [--provider azure|bedrock|local] [--model <id>] [--prompt-name <name>]
        [--config <path>] [--no-color] [--json] [-- <one-shot prompt>]
chatdbg tokenize <file> --top-k 5 --format csv     # one-shot, non-interactive, scriptable
```

The one-shot / `--json` path is why you want a real parser at startup and not `args[0]` switching:
it is what makes the tool usable from a script or a CI job, and it needs correct exit codes,
`--help`, and `--version`, all of which System.CommandLine gives you free.

**Capability matrix**

| | System.CommandLine 2.0.11 | Spectre.Console.Cli 0.55.0 | Xcaciv.Command 3.3.x | ConsoleAppFramework 5.7.13 |
|---|---|---|---|---|
| Status | GA | 0.x, separate repo, 1.0 alphas unlisted | private feed, AGPL-3.0 | GA |
| Startup argv parsing | yes | yes | no (it parses in-session lines) | yes |
| In-session REPL commands | no | no | **yes, its whole purpose** | no |
| Attribute-driven params | no (builder/`Option<T>`) | yes (`CommandSettings`) | **yes** | via method signature |
| Command pipelines (`\|`) | no | no | **yes, channel-backed** | no |
| Plugin/assembly loading | no | no | **yes, via Xcaciv.Loader** | no |
| DI integration | manual | `ITypeRegistrar` | `AddXcacivCommand(IServiceCollection)` | source-gen + `IServiceProvider` |
| Trimming / Native AOT | **supported** since beta4 | **explicitly not supported** | UNCONFIRMED | **designed for it** |
| Tab completion | yes (built-in `dotnet-suggest`) | limited | n/a (own prompt) | limited |

**Second-best for startup args: Spectre.Console.Cli, and the condition under which it wins** — if
you decide to abandon Native AOT publishing *and* you want the startup surface and the in-session
surface to share one attribute-driven `CommandSettings` vocabulary with Spectre's help renderer.
That is a real, coherent choice. But it is disqualified here by two facts: the project's own docs
state that Spectre.Console.Cli "relies on reflection, and use during trimming and AOT compilation is
not supported and may result in unexpected behaviors"
([issue #1155](https://github.com/spectreconsole/spectre.console/issues/1155)), and this application
*already ships* a `Compact` configuration with `PublishAot=true`, `PublishTrimmed=true`,
`TrimMode=full` (`src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj`). Third-best is ConsoleAppFramework,
which wins if AOT binary size and startup latency dominate everything else — its cost is that it
shares nothing with the rest of the stack.

### 4. Resilience for LLM calls

**What the standard handler actually does.** From
[Build resilient HTTP apps](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience)
(doc updated 2026-03-30), `AddStandardResilienceHandler()` stacks five strategies, outermost first:

| # | Strategy | Default |
|---|---|---|
| 1 | Rate limiter | Queue `0`, Permit `1_000` |
| 2 | **Total request timeout** | **30 s** |
| 3 | Retry | Max 3, `Exponential`, `UseJitter = true`, base delay 2 s |
| 4 | Circuit breaker | Failure ratio 10 %, **MinimumThroughput `100`**, sampling 30 s, break 5 s |
| 5 | **Attempt timeout** | **10 s** |

Handled conditions: HTTP 500+, 408, 429, plus `HttpRequestException` and
`TimeoutRejectedException`. (Note that last one: Polly throws `TimeoutRejectedException`, *not*
`System.TimeoutException` — the doc warns about this explicitly when you write your own
`ShouldHandle`.)

**Verdict per strategy, for this application:**

| Strategy | Appropriate for LLM calls? | What to do |
|---|---|---|
| Rate limiter | Harmless but pointless at 1 000 concurrent permits for a single-user shell. | Drop it, or set permits to 1–2 if you want to serialise concurrent turns. |
| Total request timeout **30 s** | **No.** A long answer or a big local-model prompt routinely exceeds this. Left at the default it will abort real generations. | Raise to something generous (5–10 min) **and** understand the streaming interaction below. |
| Retry (3 × exponential + jitter) | **Shape is right, scope is wrong.** | Reduce to 2 attempts, and gate on *nothing having been emitted yet* (see below). |
| Circuit breaker | **No.** `MinimumThroughput = 100` inside a 30 s sampling window means the breaker mathematically cannot open in an interactive CLI — you will never make 100 requests in 30 seconds. It is dead weight that only adds a failure mode. | Drop it, or set `MinimumThroughput` to ~4 and `FailureRatio` to ~0.5 so it can actually protect you from a hard-down endpoint. |
| Attempt timeout **10 s** | **No.** Time-to-first-token on a large hosted model, or a cold Bedrock endpoint, can exceed 10 s. | Use it, but as a **time-to-first-byte** budget (30–60 s), which is only meaningful once you fix the streaming interaction. |
| Hedging (`AddStandardHedgingHandler`) | **Actively harmful.** Hedging "retries slow requests in parallel." For a metered LLM that means two full generations, two bills, and two token streams that both need discarding. | Do not use. The only defensible case is failing over between two deployments of the *same* model where per-token cost is genuinely irrelevant — say, an on-prem cluster. |

**Retry-After and 429 — confirmed from source.**
`Microsoft.Extensions.Http.Resilience.HttpRetryStrategyOptions` sets, in its constructor,
`ShouldHandle = IsTransient`, `BackoffType = DelayBackoffType.Exponential`,
**`ShouldRetryAfterHeader = true`**, `UseJitter = true`; and the `ShouldRetryAfterHeader` setter
installs a `DelayGenerator` that parses the response's `Retry-After` header and uses it as the delay
([HttpRetryStrategyOptions.cs](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.Http.Resilience/Polly/HttpRetryStrategyOptions.cs);
[API page](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.http.resilience.httpretrystrategyoptions)).
So **Azure OpenAI and Bedrock 429s with `Retry-After` are honoured out of the box** — that is the
single strongest argument for using this package over hand-rolled `Task.Delay` retry loops. Do not
also implement `Retry-After` yourself; you would double the wait.

**The streaming trap — the most important item in this section.** The resilience handler is a
`DelegatingHandler`; it wraps `SendAsync`. `HttpClient`'s default `HttpCompletionOption` is
`ResponseContentRead`, which means `SendAsync` does not complete until the **entire response body**
has been read. If you leave that default, the pipeline's 30 s total timeout applies to the whole
generation and it will kill long answers, and the retry strategy will see a half-consumed stream.
When you switch to `HttpCompletionOption.ResponseHeadersRead` — which you must, to stream tokens —
`SendAsync` returns as soon as headers arrive, so the pipeline's timeouts become a **time-to-first-byte**
budget and its predicates see only the status line. That is precisely the behaviour you want, and it
also automatically gives you the "only retry before the first token" property, because by the time
tokens are flowing the resilience pipeline has already completed and released the request. The
corollary, from the
[HttpCompletionOption docs](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcompletionoption):
"the timeout applies only up to where the headers end and the content starts. The content reading
operation needs to be timed out separately." So you need a **second, independent stall timeout on
the token stream itself** — e.g. "no token for 60 s ⇒ cancel" — implemented with a
`CancellationTokenSource` you `CancelAfter`-reset on each chunk. Nothing in the resilience package
does that for you.

**Double-retry is a live hazard.** Both provider SDKs retry internally:

- `Azure.AI.OpenAI` → `AzureOpenAIClientOptions.RetryPolicy = new ClientRetryPolicy(maxRetries: n)`
  and `Transport = new HttpClientPipelineTransport(httpClient)` (the System.ClientModel-era
  replacement for `Azure.Core.Pipeline.HttpClientTransport`).
- AWS SDK → `ClientConfig.RetryMode` (default `RequestRetryMode.Legacy`) and `MaxErrorRetry`, which
  returns **4** under `Legacy` and **2** under `Standard`/`Adaptive`
  ([AWS retries & timeouts](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/retries-timeouts.html)).

If you hand a resilience-wrapped `HttpClient` to either SDK without disabling its own retries, you
get the product of the two — 3 × 4 = 12 attempts against a rate-limited endpoint, which is how you
turn a 429 into a ban. **Pick one layer.** My recommendation: set the SDK retry counts to zero and
own retry in one place, because that is the layer where you can see the "have any tokens been
emitted?" state. The subject already has the right seams for this — `IAzureOpenAIClientFactory` and
`IBedrockRuntimeClientFactory` in `src/Xcaciv.ChatDbg.Core/Services/` — so this is a configuration
change inside two existing factory implementations.

**Polly directly, for the non-HTTP paths.** `Microsoft.Extensions.Http.Resilience` only helps things
that flow through an `HttpMessageHandler`. Use `Polly.Core` 8.7.0's `ResiliencePipelineBuilder`
directly for anything that doesn't, and register named pipelines with
`services.AddResiliencePipeline<string, T>(...)` resolved from `ResiliencePipelineRegistry<string>`
so each provider gets its own tuned pipeline. Note 8.7.0 specifically added caller cancellation-token
propagation into the hedging and timeout strategies — worth having, given how much of this design
hangs on cancellation.

The local GGUF path should get **no retry at all**. A local inference failure is an out-of-memory or
a bad model file; retrying it three times just makes the user wait three times as long for the same
error. Give it a timeout and a clear message.

### 5. `IHttpClientFactory` and typed clients — why a long-lived console app still needs them

The source does the thing the guidance exists to prevent:
`src/Xcaciv.ChatDbg.Core/Services/AzureOpenAIService.cs:22-32` —
`public AzureOpenAIService(HttpClient? httpClient = null, …)` and, when none is passed,
`_httpClient = new HttpClient(); _disposeHttpClient = true;`, disposed in `Dispose(bool)` at line
484. In a REPL where `/model` can create and dispose provider instances repeatedly across a session,
that is a handler-churn pattern with a stale-DNS pattern layered on top.

Four reasons this matters here specifically, from the
[IHttpClientFactory doc](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory)
(updated 2026-08-13):

1. **DNS staleness, which is the real risk for this app.** "Some handlers also keep connections open
   indefinitely, which can prevent the handler from reacting to DNS changes." A debugging shell is
   left open for hours; Azure OpenAI endpoints sit behind fronting infrastructure whose addresses
   move. Default `HandlerLifetime` is **two minutes**, adjustable with `SetHandlerLifetime`.
2. **Socket exhaustion**, the classic reason — less acute for one user, but not zero once `/model`
   churn and background token-attribution calls are in play.
3. **One place to attach the resilience handler, and one place to redact secrets.**
   `RedactLoggedHeaders(["api-key", "Authorization", "x-api-key"])` matters a lot for an app whose
   whole point is dumping internals to the terminal, and which stores provider secrets in the
   Windows Credential Manager (`WindowsCredentialManagerTests.cs` in the test project).
4. **Testability.** The test project already has
   `src/Xcaciv.ChatDbg.Core.Tests/TestDoubles/StubHttpMessageHandler.cs`; `ConfigurePrimaryHttpMessageHandler`
   is the sanctioned way to swap it in without a nullable-`HttpClient` constructor parameter.

**Use the Keyed DI approach, not typed clients.** Keyed `HttpClient` support arrived with
Microsoft.Extensions.Http/DI **9.0.0+** via `AddAsKeyed()`, and the docs now say plainly: "We
currently recommend using Keyed DI approach instead of Typed clients"
([Keyed DI Support in IHttpClientFactory](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory-keyed-di)).
Default lifetime for `AddAsKeyed()` is **Scoped**. But a REPL has no request scope — a client
resolved once at startup is captive by construction. The docs bless exactly the configuration you
therefore need:

```csharp
builder.Services.AddHttpClient(ProviderKeys.Azure, c =>
    {
        c.BaseAddress = azureEndpoint;
        c.Timeout = Timeout.InfiniteTimeSpan;   // the resilience pipeline owns timeouts
    })
    .AddAsKeyed(ServiceLifetime.Singleton)                       // consciously long-lived
    .UseSocketsHttpHandler((h, _) => h.PooledConnectionLifetime = TimeSpan.FromMinutes(5))
    .SetHandlerLifetime(Timeout.InfiniteTimeSpan)                // rotation handled by the line above
    .RedactLoggedHeaders(["api-key", "Authorization"])
    .AddResilienceHandler("llm", ConfigureLlmPipeline);
```

That mirrors the doc's own singleton example verbatim ("In cases when client's longevity can't be
avoided—or if it's consciously desired, for example, for a Keyed Singleton—it's advised to leverage
`SocketsHttpHandler` by setting `PooledConnectionLifetime` to a reasonable value"), and it keeps DNS
freshness while eliminating the captive-client warning.

Two warnings to carry forward. First, **do not** use the global
`services.ConfigureHttpClientDefaults(b => b.AddAsKeyed())` opt-in: the doc's own "Beware Of
'Unknown' clients" note explains that this becomes a `KeyedService.AnyKey` registration, container
validation stops applying, and "an *erroneous* key value *silently* leads to a *wrong instance* being
injected" — you would get a silently unconfigured `HttpClient` pointed at nothing for a mistyped
provider name. Opt in per client. Second, note the app's `HttpClient.Timeout` must be effectively
disabled (or set far above the pipeline's), because `HttpClient.Timeout` is a hard cap that fires
independent of Polly and surfaces as `TaskCanceledException`, which is easy to misread as a user
cancellation.

### 6. Async and streaming

**`IAsyncEnumerable<T>` is the right provider contract, and the framework already demands it.**
`Xcaciv.Command`'s
[`ICommandDelegate`](https://raw.githubusercontent.com/Xcaciv/Xcaciv.Command/main/src/Xcaciv.Command.Interface/ICommandDelegate.cs)
is `IAsyncDisposable` and its entry point is:

```csharp
IAsyncEnumerable<IResult<string>> Main(IIoContext ioContext, IEnvironmentContext env);
```

with the remark "Commands support pipelining via `IAsyncEnumerable` output … Output supports
pipelining: if part of a piped command sequence, output is sent to the next command's input via
channels." And
[`IIoContext`](https://raw.githubusercontent.com/Xcaciv/Xcaciv.Command/main/src/Xcaciv.Command.Interface/IIoContext.cs)
carries `void SetInputPipe(ChannelReader<IResult<string>> reader)` and
`IAsyncEnumerable<IResult<string>> ReadInputPipeChunks()`. So the pipeline boundary in this app is a
`System.Threading.Channels` channel whether you choose one or not.

**Correctness rules, with the reasons:**

- **`[EnumeratorCancellation]`.** Declare the provider iterator as
  `async IAsyncEnumerable<TokenChunk> StreamAsync(…, [EnumeratorCancellation] CancellationToken ct = default)`.
  Per the [async streams tutorial](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/generate-consume-asynchronous-stream),
  `System.Runtime.CompilerServices.EnumeratorCancellationAttribute` "causes the compiler to generate
  code for the `IAsyncEnumerator<T>` that makes the token passed to `GetAsyncEnumerator` visible to
  the body of the async iterator." Without it, a caller's `.WithCancellation(ct)` is silently
  ignored — the most common async-stream bug there is.
- **`.WithCancellation(ct)` at every consumption site**
  (`TaskAsyncEnumerableExtensions.WithCancellation`).
- **`.ConfigureAwait(false)`** — the `TaskAsyncEnumerableExtensions.ConfigureAwait` extension, which
  is the `await foreach` form. In a pure console app there is no `SynchronizationContext`, so this
  is a no-op for correctness *today*. Do it anyway in `Xcaciv.ChatDbg.Core`, because the repo
  already contains a second front end — `src/ChatDbg.Shell.Gui/` — and a UI host is exactly the
  place where a missing `ConfigureAwait(false)` in a library turns into a deadlock or a stuttering
  render.
- **Disposal.** `await foreach` compiles to a `try/finally` with `await enumerator.DisposeAsync()`,
  which the tutorial spells out. That is load-bearing for this app: `break`ing out of the token loop
  is what disposes the `HttpResponseMessage`/stream and releases the connection — it is how you abort
  a generation. If you ever hand the enumerator to something else (buffering the stream on a
  background task while the UI renders), you must `await using var e = source.GetAsyncEnumerator(ct);`
  yourself.
- **Never `async void`**, including in the Ctrl+C / signal handler path. An unobserved exception
  there takes the process down without running shutdown.

**Channels between the inference loop and the renderer.** Use a **bounded** channel:

```csharp
var ch = Channel.CreateBounded<RenderEvent>(new BoundedChannelOptions(1024)
{
    SingleWriter = true,
    SingleReader = true,
    FullMode     = BoundedChannelFullMode.Wait,          // backpressure, don't drop tokens
    AllowSynchronousContinuations = false,
});
```

Rationale, against the [Channels doc](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels):

- **Bounded, `FullMode.Wait`** — the doc's default and the only correct choice here. Any `Drop*`
  mode silently discards tokens, which for a *token-level introspection tool* would corrupt the
  thing the app exists to show. `Wait` gives you real backpressure: "whenever a `Writer` produces
  faster than a `Reader` can consume, the channel's writer experiences back pressure." That is what
  you want when the terminal is slow — a heat-map redraw, a 200-column probability grid, or output
  piped into `less`.
- **`AllowSynchronousContinuations = false`** — with `true`, "the writes might end up doing work
  associated with a reader by executing their continuations," i.e. terminal rendering would run
  inline on the network-read continuation. For a renderer that takes a console lock and repaints a
  grid, that is a latency and reentrancy hazard.
- **`SingleWriter`/`SingleReader = true`** lets the implementation take a faster path, and it is
  true here (one inference loop, one renderer).
- Consume with `await foreach (var e in ch.Reader.ReadAllAsync(ct))`; finish with
  `ch.Writer.Complete(ex)` so the reader observes the fault rather than hanging — and only ever call
  `Complete` once, from the producer.

Why a channel at all when you already have `IAsyncEnumerable`? Three reasons specific to this app:
one producer has **two** consumers (the live token renderer and the log-prob accumulator that feeds
the probability map); Spectre's `Live`/`Progress` rendering wants to own its own write loop rather
than be driven from inside your network loop; and Xcaciv.Command's pipe contract is already a
`ChannelReader<IResult<string>>`, so you are matching an existing shape rather than inventing one.

### 7. Recommendation for wiring this application

```csharp
// Program.cs
var startup = ChatDbgCli.Parse(args);            // System.CommandLine 2.0.11 — pure, no side effects
if (startup.ExitImmediately) return startup.ExitCode;   // --help / --version / parse error

var builder = Host.CreateApplicationBuilder(args);

// -- configuration -------------------------------------------------------
builder.Configuration.AddJsonFile(startup.ConfigPath ?? DefaultConfigPath, optional: true);
builder.Services.AddOptions<ChatSettings>()
       .Bind(builder.Configuration.GetSection("Chat"))
       .ValidateOnStart();                        // fail at startup, not mid-turn

// -- host behaviour ------------------------------------------------------
builder.Services.Configure<HostOptions>(o =>
{
    o.ShutdownTimeout = TimeSpan.FromSeconds(10); // default 30 s is too long to wait at a prompt
    o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
});
builder.Services.AddSingleton<IHostLifetime, ReplLifetime>();  // last registration wins; owns Ctrl+C

// -- HTTP + resilience, per provider ------------------------------------
builder.Services.AddHttpClient(ProviderKeys.Azure,   ConfigureAzure)  .AsLlmClient();
builder.Services.AddHttpClient(ProviderKeys.Bedrock, ConfigureBedrock).AsLlmClient();
// .AsLlmClient() = AddAsKeyed(Singleton) + UseSocketsHttpHandler(PooledConnectionLifetime = 5 min)
//                + SetHandlerLifetime(Infinite) + RedactLoggedHeaders + AddResilienceHandler("llm", …)

// -- providers, keyed ----------------------------------------------------
builder.Services.AddKeyedSingleton<IChatProvider, AzureOpenAIProvider>(ProviderKeys.Azure);
builder.Services.AddKeyedSingleton<IChatProvider, BedrockProvider>(ProviderKeys.Bedrock);
builder.Services.AddKeyedSingleton<IChatProvider>(ProviderKeys.Local,
    (sp, key) => new LlamaProvider(sp.GetRequiredService<IOptions<ChatSettings>>()));  // lazy: loads GGUF on first use
builder.Services.AddSingleton<IChatProviderResolver, ChatProviderResolver>();

// -- app services --------------------------------------------------------
builder.Services.AddSingleton<IChatHistoryService, ChatHistoryService>();
builder.Services.AddSingleton<ISystemPromptService, SystemPromptService>();
builder.Services.AddSingleton<ISecretStore, OsSecretStore>();
builder.Services.AddSingleton<ITokenInspectionService, TokenInspectionService>();
builder.Services.AddSingleton<IConsoleFormatter, SpectreConsoleFormatter>();

// -- in-session command framework ---------------------------------------
builder.Services.AddXcacivCommand(builder.Configuration);   // from Xcaciv.Command.DependencyInjection

// -- the REPL ------------------------------------------------------------
builder.Services.AddSingleton(startup);                      // parsed startup options
builder.Services.AddHostedService<ChatReplService>();        // BackgroundService

using var host = builder.Build();
await host.RunAsync();
return Environment.ExitCode;
```

`ChatReplService.ExecuteAsync(stoppingToken)` then does, per turn: create a linked CTS
(`stoppingToken` + this turn's token published to `ReplLifetime`); read a line (see the
`Console.ReadLine` caveat below); if it starts with the command prefix, hand it to
`ICommandController` and stream `IAsyncEnumerable<IResult<string>>` to the formatter; otherwise
resolve `IChatProvider` from `IChatProviderResolver` for the currently selected key and stream tokens
into the bounded channel that the renderer drains; on `/exit`, call
`IHostApplicationLifetime.StopApplication()`; dispose the linked CTS.

---

## What this application specifically needs — tied to concrete operations

| Operation the app performs | What it needs | Why |
|---|---|---|
| `/model bedrock` at the prompt | `GetRequiredKeyedService<IChatProvider>(key)` behind `IChatProviderResolver` | Replaces the `Dictionary<string, IAIService>` at `ChatShell.cs:18/29` with a container lookup; the key is chosen at runtime, so it is a resolve call, not `[FromKeyedServices]`. |
| `/model` with no argument (list providers) | `GetKeyedServices<IChatProvider>(KeyedService.AnyKey)` | Post-.NET-10 this returns exactly the specifically-keyed registrations. The **singular** form with `AnyKey` now throws. |
| Loading the GGUF the first time `/model local` is used | Keyed singleton **factory** overload | Multi-GB weights must not load at host build. The factory delegate defers to first resolve. |
| Streaming an answer token by token | `IAsyncEnumerable<TokenChunk>` + `[EnumeratorCancellation]` + `HttpCompletionOption.ResponseHeadersRead` | The source has **zero** `IAsyncEnumerable` and returns `Task<string>`; without `ResponseHeadersRead` the resilience pipeline's total timeout covers the whole generation. |
| Ctrl+C mid-generation | Custom `IHostLifetime` + per-turn linked CTS; `break` out of `await foreach` | Default `ConsoleLifetime` maps SIGINT straight to host shutdown, which would quit the shell instead of cancelling the answer. The `break` disposes the enumerator and releases the HTTP stream. |
| Ctrl+C at an idle prompt / `/exit` | `IHostApplicationLifetime.StopApplication()` | Runs `StoppingAsync`/`StopAsync`/`StoppedAsync`, so the chat history and settings actually get flushed. `Environment.Exit` — which Cupcake's `Program.cs` uses — skips `finally` blocks. |
| Azure OpenAI 429 with `Retry-After: 12` | `HttpRetryStrategyOptions.ShouldRetryAfterHeader` (default `true`) | Confirmed in source: the setter installs a `DelayGenerator` that parses the header. Free correct behaviour; do not reimplement it. |
| A generation that stalls with no tokens for a minute | A **separate** idle-timeout CTS reset on each chunk | The pipeline's timeouts stop applying once headers are read. Nothing in the resilience package covers stream stalls. |
| Rendering a 200-column probability heat map while tokens keep arriving | Bounded `Channel<RenderEvent>` with `FullMode.Wait`, `AllowSynchronousContinuations = false` | Backpressure instead of unbounded buffering; keeps repaint work off the network continuation. Any `Drop*` mode would silently lose the tokens the tool exists to inspect. |
| `tokenize file.txt \| logprobs \| export --csv` | Xcaciv.Command's pipeline (`IIoContext.SetInputPipe(ChannelReader<IResult<string>>)`) | Already channel-backed; nothing to build. System.CommandLine has no equivalent. |
| `chatdbg tokenize file.txt --json` from a script | System.CommandLine 2.0.11 root command with a non-interactive path that never starts the REPL hosted service | Correct exit codes, `--help`, `--version`, completion. Already in the closure via Xcaciv.Command's own reference. |
| A session left open for six hours | `IHttpClientFactory` + `PooledConnectionLifetime` | The source's `new HttpClient()` per provider pins connections and will not react to DNS changes behind the Azure endpoint. |
| Not leaking the API key into logs | `RedactLoggedHeaders(["api-key", "Authorization"])` on the `IHttpClientBuilder` | `IHttpClientFactory` adds `ILogger`-based request logging by default; without redaction the key lands in the log. |
| Publishing a self-contained binary for Windows and Linux | System.CommandLine (AOT-supported since beta4), and **not** Spectre.Console.Cli | The existing `Compact` config is `PublishAot=true; TrimMode=full`. |

---

## Risks, sharp edges and what you give up

**1. `Xcaciv.Command` gives you no `CancellationToken`.** `ICommandDelegate.Main(IIoContext, IEnvironmentContext)`
has no token parameter, and the XML docs mention cancellation nowhere. For a shell whose commands
call an LLM, that is the largest impedance mismatch in the whole design: the framework has no way to
tell a running command to stop. Workarounds, in order of preference: (a) smuggle the per-turn token
through `IEnvironmentContext` or a custom `IIoContext` implementation you own, since your host
constructs the context; (b) hold an ambient `AsyncLocal<CancellationToken>` scoped per turn;
(c) upstream a `CancellationToken` overload — you control the repo. Do not rely on `IAsyncDisposable`
alone; disposal happens after the enumeration ends, which is too late.

**2. `Console.ReadLine` is not cancellable, and this is unfixed.** `TextReader.ReadLineAsync(CancellationToken)`
exists, but on standard input the underlying read blocks and cancellation does not take effect until
a newline is actually typed — see [dotnet/runtime#100308](https://github.com/dotnet/runtime/issues/100308)
and [Meziantou's write-up](https://www.meziantou.net/cancelling-console-read.htm). Consequence: after
Ctrl+C, your `BackgroundService` will sit inside the read until the user presses Enter, so the host's
`ShutdownTimeout` may expire before the loop notices. Mitigation: run the read on a dedicated
foreground-independent thread and `Task.WhenAny(readTask, cancelTask)`, accepting that the abandoned
read completes later and is discarded; or drive the prompt with a raw `Console.ReadKey`/`KeyAvailable`
loop you control. Spectre's prompt APIs inherit the same limitation. Budget for this — it is the kind
of thing that turns into "Ctrl+C doesn't work" bug reports.

**3. `SuppressTrimAnalysisWarnings=true` is currently hiding the exact warnings this design would
produce.** `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj` sets it in both the `Compact` and `SingleFile`
configurations. Adding host + DI + options-binding + resilience is adding reflection-shaped code to
an AOT build with the warning light disconnected. Turn it off, then close the resulting warnings
properly: enable the configuration-binding source generator, use `[OptionsValidator]` for validation
instead of DataAnnotations reflection, and register every service with an explicit
`AddSingleton<TService, TImpl>()` rather than open-generic or assembly-scanning registration. What
you give up if you do **not** do this: an AOT binary that appears to build and then throws
`InvalidOperationException` at first config bind, on a user's machine.

**4. The circuit breaker in `AddStandardResilienceHandler` is inert at CLI volumes and the timeouts
are hostile to LLMs.** Stated above; repeated here because the temptation to write one line and move
on is strong, and one line is the wrong answer. Use `AddResilienceHandler("llm", …)` with explicit
strategies.

**5. Hedging costs money.** `AddStandardHedgingHandler` sends duplicate requests. On a metered
completions API that is a direct, silent doubling of spend, plus two streams to reconcile. Never
enable it for this workload.

**6. The Xcaciv packages are not on nuget.org, and two of the three are AGPL-3.0.** Verified: all
five candidate package IDs return not-found. Builds require the private
`nuget.pkg.github.com/xcaciv` feed (credentialled) or a local folder. And `Xcaciv.Command` and
`Xcaciv.Loader` are AGPL-3.0 while `Xcaciv.Cupcake` is BSD-3-Clause — a licence-compatibility
question for a distributed self-contained binary that someone should answer deliberately rather than
by accident. That is a factual observation about the repositories, not legal advice.

**7. "Patterned on Xcaciv.Cupcake" currently means "no host at all."** Cupcake's
`src/Xcaciv.Cupcake/Program.cs` is still the `Console.WriteLine("Hello, World!")` template stub; the
functioning entry point is `Xcaciv.Cupcake.Lit`, which does `new Xcaciv.Cupcake.Core.Loop()`,
`Controller.AddCommand("internal", new InstallCommand())`, `RunWithDefaults()`, and
`Environment.Exit(1)` in the catch. `src/Xcaciv.Cupcake/Xcaciv.Cupcake.csproj` targets **`net8.0`**.
So following the pattern literally gives you: no Generic Host, no DI, no `CancellationToken`,
non-graceful exit, and a framework retarget. Follow the *shape* (a `Loop` driving an
`ICommandController` over an `IIoContext`) and supply the host yourself.

**8. Keyed DI has no compile-time key checking.** A mistyped key is a runtime
`InvalidOperationException` — or, worse, if you ever take the global
`ConfigureHttpClientDefaults(b => b.AddAsKeyed())` shortcut, a *silent* unconfigured `HttpClient`,
which the docs warn about explicitly. Use typed keys and per-client opt-in.

**9. What you give up by choosing this recommendation.** The Generic Host adds five or so assemblies
and a few milliseconds of startup — irrelevant next to loading a GGUF, but it is not nothing for an
AOT size budget, and the current build scripts (`build-compact.ps1`, `build-singlefile.bat`) exist
because size was a goal. You also give up the direct, greppable simplicity of
`new ChatShell()`: with DI, "where does this instance come from" becomes a registration lookup rather
than a `new`. And by choosing System.CommandLine for startup args you give up Spectre.Console.Cli's
nicer help rendering and its shared attribute vocabulary with the in-session surface — a real loss,
paid to keep the AOT publish path viable.

**10. Version-line confusion is a live footgun.** `Microsoft.Extensions.Hosting` is `10.0.11` while
`Microsoft.Extensions.Http.Resilience` is `10.9.0`. They are different repos on different cadences
(`dotnet/runtime` vs `dotnet/extensions`) and both are correct. Use Central Package Management
(`Directory.Packages.props`) — which Xcaciv.Command and Cupcake already do — and do not "fix" the
mismatch.

---

## Unconfirmed

- **`System.CommandLine` 3.0's contents and ship vehicle.** Seven previews exist on nuget.org
  (`3.0.0-preview.1.26104.118`, 2026-02-10 → `3.0.0-preview.7.26381.103`, 2026-08-11). I found **no**
  release notes, migration guide, or announcement issue describing what changes in 3.0. The
  [dotnet/command-line-api releases page](https://github.com/dotnet/command-line-api/releases) does
  not list the 3.0 previews as releases. Whether 3.0 is a breaking redesign or a retarget for .NET 11
  is **UNCONFIRMED**. Searched: nuget.org feeds, the releases page, and general web search for
  "System.CommandLine 3.0 breaking changes / roadmap".
- **`Xcaciv.Command`'s exact command-attribute surface.** I read the interface file list and
  `ICommandDelegate.cs` / `IIoContext.cs` directly, and the attribute *file names* under
  `src/Xcaciv.Command.Interface/Attributes/`, but I did not read each attribute's constructor
  parameters or `COMMAND_TEMPLATE.md`. Treat the attribute names in this document as verified and
  their signatures as **UNCONFIRMED**.
- **Whether `Xcaciv.Command` / `Xcaciv.Loader` / `Xcaciv.Cupcake` are trim- or AOT-safe.**
  `Xcaciv.Loader` does runtime assembly and type loading by design, which is fundamentally at odds
  with Native AOT. I did not verify whether the plugin-loading path can be disabled, or whether the
  packages set `IsTrimmable`/`IsAotCompatible`. This interacts directly with risk #3 above and should
  be settled before committing to the `Compact` AOT configuration. Searched: the repos' README,
  `Directory.Packages.props`, `NuGet.config`; did not read the `.csproj` files.
- **Whether `Microsoft.Extensions.Http.Resilience` 10.9.0 declares `IsAotCompatible`.** Searched
  nuget.org, the dotnet/extensions repo listing and general web; no direct statement found. The
  historical concern is options binding + DataAnnotations validation using unbounded reflection.
  **UNCONFIRMED.**
- **AWS SDK v4's exact `HttpClient` injection API.** `ClientConfig.RetryMode` (default
  `RequestRetryMode.Legacy`) and `MaxErrorRetry` (4 under Legacy, 2 under Standard/Adaptive) are
  confirmed from the [AWS retries and timeouts doc](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/retries-timeouts.html).
  The property name for supplying a custom `HttpClient` factory on `AmazonBedrockRuntimeConfig` is
  **UNCONFIRMED** — I did not open the `ClientConfig` API reference for the version the app uses
  (`AWSSDK.BedrockRuntime 4.0.7.3`).
- **Whether Bedrock's streaming API (`InvokeModelWithResponseStream` / `ConverseStream`) exposes
  per-token log-probabilities at all**, and therefore whether the resilience/streaming design here
  is equally applicable to all three providers. Out of scope for this brief and not investigated;
  flagged because the app's distinguishing feature depends on it.
- **`Spectre.Console.Cli` 1.0 timing.** The 1.0 alphas are unlisted on nuget.org and the separate
  repo was last pushed 2026-05-08. No public target date found. **UNCONFIRMED.**
- **`McMaster.Extensions.CommandLineUtils` maintenance posture.** 5.1.0 shipped 2026-04-05, so it is
  not stale, but I did not check whether the author has declared it in maintenance-only mode.
- **`Xcaciv.Cupcake.Core.Loop`'s actual API.** GitHub's API rate-limited me before I could read
  `src/Xcaciv.Cupcake.Core/`. I know `Loop` has a `Controller` property and a `RunWithDefaults()`
  method from the call site in `Xcaciv.Cupcake.Lit/Program.cs`; the rest is **UNCONFIRMED**.
