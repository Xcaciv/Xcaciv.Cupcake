# Testing, quality gates and analysis tooling

*Research date: 28 August 2026. Every version number and date below was checked against nuget.org, learn.microsoft.com, or the project's own repository on the live web on that date. Anything I could not verify is listed in the final section rather than guessed at.*

---

## Bottom line — the recommendation in three sentences

Build the test suite on **xUnit.net v3 4.0.0 running on Microsoft.Testing.Platform v2**, with **hand-rolled test doubles as the default** and **NSubstitute 6.2.0** kept in the project only for the handful of wide interfaces where writing a fake by hand is genuinely worse — because MTP is now the only platform that can run Native AOT and trimmed test hosts (the exact deployment mode this app ships in), and because every Castle.Core-based mocking library, NSubstitute included, is structurally incapable of running inside one.

For the LLM boundary, define your own narrow port, adapt each provider behind it, and test the ports with **deterministic hand-written fakes**; reserve **WireMock.Net 2.15.0** for a small number of on-the-wire contract tests per provider, and reserve real Azure/Bedrock/GGUF traffic for a nightly, trait-gated job that CI can skip.

For gates, run **coverlet.mtp 10.0.1** for coverage (MIT, unlike Microsoft's code-coverage extension), **dotnet-stryker 4.16.0** mutation testing scoped only to the token-math namespaces, **BenchmarkDotNet 0.15.8** in a separate project for the token hot paths, and turn the compiler itself into the first quality gate with `AnalysisLevel=latest-recommended`, `TreatWarningsAsErrors`, `Nullable=enable`, `IsAotCompatible=true` and a `BannedSymbols.txt`.

---

## Landscape — the real options

### Test platforms and frameworks

| Package | Latest | Status | Last published | One-line verdict |
|---|---|---|---|---|
| [`xunit.v3`](https://www.nuget.org/packages/xunit.v3) | **4.0.0** | GA | 2026-08-15 (released 2026-08-14) | Recommended. Native AOT test execution, MTP v2 by default, opt-in full parallelisation. |
| [`xunit.v3`](https://www.nuget.org/packages/xunit.v3) (previous line) | 3.2.2 | GA | 2025-11-02 for 3.2.0 | The de-risked fallback if 4.0's runner-stack rewrite proves unstable; still MTP v1 by default. |
| [`xunit`](https://www.nuget.org/packages/xunit) (v2) | 2.9.3 | GA, maintenance | — | What the current ChatDbg and Xcaciv.Command test projects use. VSTest-only. Migrate off it. |
| [`MSTest`](https://www.nuget.org/packages/MSTest) (meta) | **4.3.3** | GA | 2026-07-28 | The conservative choice. Microsoft-supported, MTP-native, SDK-style `Microsoft.Testing.Platform` profiles. Weakest data-driven story of the four. |
| [`NUnit`](https://www.nuget.org/packages/NUnit) | **4.6.1** | GA | 2026-05-19 | Fine, mature, has an MTP runner. No advantage here over xUnit v3 and a smaller .NET-10-era ecosystem gravity. |
| [`TUnit`](https://www.nuget.org/packages/TUnit) | **1.65.68** | GA 1.x, very high churn | 2026-08-26 | Genuinely production-*usable* (5.3M downloads, 3.9k stars, MTP-only, source-generated, AOT-first) but shipping ~3 releases a week. Second-best; see below. |
| [`Microsoft.Testing.Platform`](https://www.nuget.org/packages/Microsoft.Testing.Platform) | **2.3.3** | GA | 2026-07-28 | The platform. v2 dropped .NET Core 3.1–.NET 7; min is .NET 8. |
| [`Microsoft.NET.Test.Sdk`](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk) | **18.9.0** | GA | 2026-08-14 | Only needed if you keep a VSTest path alive. You should not. |
| [`Microsoft.Testing.Extensions.HangDump`](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump) / `.CrashDump` | **2.3.3** | GA, MIT | 2026-07-28 | Directly useful: llama.cpp can hang or hard-crash the test host, and these capture a dump instead of a silent CI timeout. |
| [`Xunit.SkippableFact`](https://www.nuget.org/packages/Xunit.SkippableFact) | 1.5.85 | GA | 2026-08-24 | Obsolete for you — xUnit v3 has `Assert.Skip` / `Assert.SkipUnless` / `Assert.SkipWhen` built in. |

### Test doubles

| Package | Latest | Status | Last published | One-line verdict |
|---|---|---|---|---|
| [`NSubstitute`](https://www.nuget.org/packages/NSubstitute) | **6.2.0** | GA | 2026-08-11 | Recommended *library* mocker. 297.6M total downloads, active repo (pushed 2026-08-24). |
| [`FakeItEasy`](https://www.nuget.org/packages/FakeItEasy) | **9.0.1** | GA | 2026-01-24 | Excellent, actively maintained (repo pushed 2026-08-17), 4 open issues. Second-best; strict-verification style. |
| [`Moq`](https://www.nuget.org/packages/Moq) | 4.20.72 | **Stalled, not abandoned** | **2024-09-07** | Repo is alive ([devlooped/moq](https://github.com/devlooped/moq) pushed 2026-08-27, 21 open issues) but *no NuGet release in ~23 months*. That is the disqualifier, not SponsorLink. |
| [`RichardSzalay.MockHttp`](https://www.nuget.org/packages/RichardSzalay.MockHttp) | **7.1.0** | GA, MIT | 2026-08-08 | The middle ground for HTTP: a fluent `HttpMessageHandler` without a dynamic proxy anywhere. 50.1M downloads. |
| Hand-rolled `HttpMessageHandler` | n/a | n/a | n/a | What the current codebase already does (`src/Xcaciv.ChatDbg.Core.Tests/TestDoubles/StubHttpMessageHandler.cs`). Keep it, but grow it. |

### HTTP record / replay

| Package | Latest | Status | Last published | One-line verdict |
|---|---|---|---|---|
| [`WireMock.Net`](https://www.nuget.org/packages/WireMock.Net) | **2.15.0** | GA, Apache-2.0 | 2026-08-15 | Recommended for contract tests. Real HTTP server, record-via-proxy, request matching, response templating. |
| [`Vcr.HttpRecorder`](https://www.nuget.org/packages/Vcr.HttpRecorder) | **3.1.3** | GA, maintained *fork* | 2026-05-18 | HAR-format cassettes at the `HttpMessageHandler` level; `System.Text.Json` only (AOT-friendlier). Single maintainer, ~66 downloads/day — bus-factor risk. |
| [`EasyVCR`](https://www.nuget.org/packages/EasyVCR) | 0.13.0 | 0.x, EasyPost-maintained | 2025-12-08 | Best censoring story (redact API keys from cassettes). Drags in `Newtonsoft.Json`. |
| [`HttpRecorder`](https://www.nuget.org/packages/HttpRecorder) (nventive) | 2.0.0 | **Abandoned** | **2020-04-06** | Six years stale. Do not use; use the Vcr fork if you want this design. |

### Snapshot / approval testing

| Package | Latest | Status | Last published | One-line verdict |
|---|---|---|---|---|
| [`Verify.XunitV3`](https://www.nuget.org/packages/Verify.XunitV3) | **32.0.0** | GA, MIT source | 2026-08-26 | Best-in-class, and the right tool for grids/heat-maps. **But**: an Open Source Maintenance Fee applies to every version released after **1 September 2026**. |
| [`Snapshooter.Xunit`](https://www.nuget.org/packages/Snapshooter.Xunit) | 1.3.1 | GA, MIT | 2026-02-24 | Free alternative, but its dependencies are still `xunit.core`/`xunit.assert` **2.4.2** — xUnit v3 support unverified. |
| Hand-rolled golden files | n/a | n/a | n/a | ~40 lines. Zero licence surface. The pragmatic escape hatch. |

### Assertions

| Package | Latest | Status | Last published | One-line verdict |
|---|---|---|---|---|
| `xunit.v3.assert` (in `xunit.v3`) | 4.0.0 | GA, Apache-2.0 | 2026-08-15 | Sufficient. Use it. No extra licence, no extra dependency, AOT-clean. |
| [`AwesomeAssertions`](https://www.nuget.org/packages/AwesomeAssertions) | **9.6.0** | GA, **Apache-2.0** | 2026-08-20 | The community fork of FluentAssertions 7. 19.9M downloads, used by `dotnet/runtime`. Use if you want `BeEquivalentTo`. |
| [`FluentAssertions`](https://www.nuget.org/packages/FluentAssertions) | 8.10.0 | GA, **commercial licence required** | 2026-05-12 | Paid Xceed licence for commercial use since v8. 7.2.2 (2026-03-16) is the last Apache line. Avoid. |
| [`Shouldly`](https://www.nuget.org/packages/Shouldly) | 4.3.0 | GA, BSD-3 | **2025-01-23** | Stable release is 19 months old; 5.0.0-preview.2 in flight. Repo active. Watch, don't adopt. |

### Coverage, mutation, benchmarking, analysers

| Package | Latest | Status | Last published | One-line verdict |
|---|---|---|---|---|
| [`coverlet.mtp`](https://www.nuget.org/packages/coverlet.mtp) | **10.0.1** | GA, MIT | 2026-05-18 | Recommended. Native MTP extension (`--coverlet`). Requires MTP ≥ 2.2.2. |
| [`coverlet.collector`](https://www.nuget.org/packages/coverlet.collector) | 10.0.1 | GA, MIT | 2026-05-18 | The VSTest sibling. Coverlet is *not* stuck at 6.0.x any more — 8.0.0 (2026-02) and 10.0.0 (2026-04) shipped. |
| [`Microsoft.Testing.Extensions.CodeCoverage`](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CodeCoverage) | **18.10.0** | GA, **proprietary MS licence** | 2026-08-12 | Better fidelity (Microsoft's dynamic instrumentation), but a non-OSS licence that forbids offering it "as a stand-alone offering" and caps liability at $5.00. Fine internally; a licence-audit item for a shipped OSS binary. |
| [`dotnet-stryker`](https://www.nuget.org/packages/dotnet-stryker) | **4.16.0** | GA (MTP runner **preview**) | 2026-07-03 | Mutation testing. `--test-runner mtp` added in 4.13 (2026-03), still preview; it keeps the test host alive across mutants. |
| [`BenchmarkDotNet`](https://www.nuget.org/packages/BenchmarkDotNet) | **0.15.8** | GA | **2025-11-30** | Still the only serious answer. 0.16.0-preview.1 shipped 2026-06-30; repo active (pushed 2026-08-16). Supports `RuntimeMoniker.Net10` and `NativeAot10`. |
| [`Microsoft.CodeAnalysis.BannedApiAnalyzers`](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BannedApiAnalyzers) | **5.6.0** | GA, MIT | 2026-07-02 | Yes. Cheap, high-value for a secrets-handling app. |
| [`Meziantou.Analyzer`](https://www.nuget.org/packages/Meziantou.Analyzer) | **3.0.192** | GA, MIT | 2026-08 (late) | 220+ rules, strong async/`CancellationToken`/culture rules. Recommended. |
| [`SonarAnalyzer.CSharp`](https://www.nuget.org/packages/SonarAnalyzer.CSharp) | 10.33.0.1635 | GA | 2026-08-21 | Only if you already run SonarQube/SonarCloud. Otherwise noise. |

### App-surface testing helpers

| Package | Latest | Status | Last published | One-line verdict |
|---|---|---|---|---|
| [`Spectre.Console.Testing`](https://www.nuget.org/packages/Spectre.Console.Testing) | **0.57.2** | 0.x, active | 2026-07-02 | `TestConsole`, `TestConsoleInput`, `TestCapabilities` — the way you assert on rendered grids/heat-maps. Pin exactly; it's 0.x. |
| [`Terminal.Gui`](https://www.nuget.org/packages/Terminal.Gui) | **2.4.17** | GA, targets net10.0 | 2026-07-07 | v2 is out. The current `ChatDbg.Shell.Gui` pins 1.19.0 — that is a v1→v2 migration, not an upgrade. |
| [`Microsoft.Extensions.AI.Evaluation`](https://www.nuget.org/packages/Microsoft.Extensions.AI.Evaluation) | **10.8.0** | GA | 2026-07-14 | For *quality* evals of model output, plus a response cache that makes repeated eval runs deterministic and cheap. Nightly job, not a PR gate. |
| [`LLamaSharp`](https://www.nuget.org/packages/LLamaSharp) | **0.27.0** | 0.x | 2026-04-26 | Now depends on `Microsoft.Extensions.AI.Abstractions ≥ 10.4.1`, i.e. it speaks `IChatClient`. That is your seam. |

---

## Analysis

### 1. Test frameworks as of August 2026

**The platform question now dominates the framework question.** In the .NET 10 SDK, `dotnet test` has two implementations, selected by `global.json`:

```json
{
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

([dotnet test reference](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test)). MTP v2 **removed** the old `TestingPlatformDotnetTestSupport` MSBuild opt-in; running an MTP-v2 test project through VSTest-based `dotnet test` now hard-errors with *"Testing with VSTest target is no longer supported by MTP on .NET 10 SDK and later"* ([MTP v1→v2 migration](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-migration-from-v1-to-v2)).

Microsoft's own decision matrix is unambiguous on the one axis that matters to this application ([MTP vs VSTest](https://learn.microsoft.com/en-us/dotnet/core/testing/test-platforms-overview), updated 2026-08-07):

> "You need Native AOT or trimming test execution scenarios. → **MTP** — MTP supports these modern deployment scenarios, while VSTest doesn't."

It also warns: *"Don't mix VSTest-based and MTP-based .NET test projects in the same solution or run configuration because that scenario isn't supported."* So this is a whole-repo decision, and it has to include the Xcaciv.Command / Xcaciv.Loader dependencies if you build them from source alongside.

**xUnit v3 4.0.0** (released [2026-08-14](https://xunit.net/releases/v3/4.0.0)) is the headline change since a 2025 view of the world:

- **Native AOT test execution.** Result reports were rewritten from XSL-T to hand-written code, and a whole parallel "Native AOT runner stack" (`CodeGenTestXyzRunner`, `ISelfExecutingCodeGenTestCase`, `CoreTestXyzRunner`) was added alongside the reflection stack. Extensibility authors need the source-only `xunit.v3.generatorutility` package.
- **MTP v1 support removed.** `xunit.v3` 4.0.0 now takes a dependency on `xunit.v3.mtp-v2` 4.0.0; MTP v2 is 2.3.3. You can still pick explicitly with `xunit.v3.mtp-v2` / `xunit.v3.mtp-off`, but `mtp-v1` variants are gone from 4.0 onward ([xUnit MTP docs](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)).
- **Mono support dropped.**
- **Full parallelisation** is now *available* but **not the default**. Per [Running tests in parallel](https://xunit.net/docs/running-tests-in-parallel), the modes are `none`, `collections`, `all`, and the default remains **`collections`**. Configuration moved from `CollectionBehavior` to `[assembly: Parallelization(Mode = ..., MaxThreads = ..., Algorithm = Conservative|Aggressive)]`, and once you opt out at a layer you cannot opt back in lower down.
- Targets **net8.0+ and net472+**; Apache-2.0.

What v3 already gave you over v2 and that this app will use daily: **each test project is an executable** (`dotnet run` a test project, F5 it, `dotnet watch` it), `TestContext` for ambient state and output, and **built-in dynamic skipping** — `Assert.Skip(reason)`, `Assert.SkipUnless(cond, reason)`, `Assert.SkipWhen(cond, reason)`, plus `[Fact(SkipUnless = nameof(ModelIsPresent))]` pointing at a `public static bool` property. That last feature is exactly the "skip if the GGUF isn't on this machine" primitive, with no third-party package.

**TUnit** is the real second-best and deserves a fair hearing. At **1.65.68** (2026-08-26) it has 5.3M downloads, 3,929 GitHub stars, and is MTP-only and source-generated from day one, so its AOT story is native rather than bolted on. The reason it is second and not first here is *release cadence as a risk*: five releases in the nine days 2026-08-18 → 2026-08-26. For a project that will pin versions in `Directory.Packages.props` and expects a stable test suite over years, that is a lot of surface area moving. Neither [tunit.dev](https://tunit.dev/) nor the repo publishes an API-stability or LTS statement.

> **TUnit wins if**: you decide to make Native-AOT test execution the *primary* daily mode (not a CI extra), or you want its first-class `[DependsOn]`, per-test parallel limits and async-native assertions badly enough to ride the churn. Its constraint is documented plainly by Microsoft: *"TUnit: Not supported on VSTest. Use MTP."*

**MSTest 4.3.3** (2026-07-28) is the "nobody gets fired" answer, and it is genuinely good now — the meta-package bundles `Microsoft.NET.Test.Sdk ≥ 18.4.0`, `Microsoft.Testing.Extensions.CodeCoverage ≥ 18.9.0`, `Microsoft.Testing.Extensions.TrxReport ≥ 2.3.3`, and MTP profiles (`AllMicrosoft` turns on Code Coverage, Crash Dump, Hang Dump, Hot Reload and Retry in one property). MSTest 4 dropped .NET Core 3.1–7, removed `ExpectedExceptionAttribute`, is not binary-compatible with v3, and disables AppDomains under MTP by default. **NUnit 4.6.1** (2026-05-19) is equally fine and equally unnecessary here.

**Recommendation: xUnit.net v3 4.0.0 + MTP v2.** Reasons specific to this app:

1. It is the only framework in the list that is simultaneously (a) mature, (b) AOT-capable for *test* execution, and (c) already what both this repo and Xcaciv.Command write tests in — the v2→v3 migration is a mechanical one (`xunit` → `xunit.v3`, drop `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk`, add `UseMicrosoftTestingPlatformRunner`), whereas moving to MSTest or TUnit is a rewrite of ~20 existing test files.
2. `[Theory]` + `MemberData`/`ClassData` is the right shape for token-level introspection tests, which are inherently table-driven: *this token stream + this top-K → this probability map*.
3. Executable-first test projects mean the same binary that CI runs can be run under `lldb`/`gdb`/WinDbg when llama.cpp segfaults — a real workflow for native-backed inference.

**What you give up by choosing xUnit v3 4.0.0**: you are on a **two-week-old major version**. The runner-stack rewrite for AOT touched the internals that every third-party extension hooks. Mitigation: pin `xunit.v3` at 4.0.0 in CPM, and keep 3.2.2 as a documented rollback (3.2.x still defaults to MTP v1, so a rollback also means changing your `global.json`/package choice — write that down now).

---

### 2. Mocking: Moq's aftermath, and when hand-rolled wins

**The SponsorLink story, accurately.** In August 2023 Moq 4.20.0 bundled a closed-source, obfuscated `SponsorLink` assembly that read `user.email` from local git config, SHA-256'd it, and sent the hash to an Azure endpoint at build time to check GitHub Sponsors status. The reaction was severe; many organisations banned the package outright, and NSubstitute/FakeItEasy adoption jumped. Later 4.20.x releases removed the dependency.

**The 2026 reason not to use Moq is different and simpler.** Moq's latest NuGet release is **4.20.72, published 2024-09-07** — roughly 23 months ago. The repository ([devlooped/moq](https://github.com/devlooped/moq)) is *not* abandoned: it was pushed to on 2026-08-27 and has only 21 open issues. But a library that does runtime IL generation, and that has not shipped a binary across two .NET major versions (.NET 9 and .NET 10) and a C# language version, is a dependency you are choosing to freeze. That is the honest characterisation: **stalled, not abandoned.** The existing ChatDbg test project pins `Moq 4.20.69` — the *pre-fix* SponsorLink version. That alone is a reason to move.

**The bigger structural point: no dynamic-proxy mocker can run under Native AOT.** Moq, NSubstitute and FakeItEasy all sit on `Castle.Core`'s `DynamicProxy`, which builds proxy types with `System.Reflection.Emit` at runtime. Under Native AOT there is no runtime code generation; Castle's own `ReflectionEmitThrower.ThrowPlatformNotSupportedException()` is what you get. Since MTP + xUnit v3 4.0 makes AOT test execution *possible*, and since this application ships as an AOT/trimmed self-contained binary, every mock you write is a test you can never run in the mode your product actually ships in.

**Recommendation.**

| Situation | Use |
|---|---|
| Any interface you own (`ILlmBackend`, `IChatHistoryStore`, `ISecretStore`, `IIoContext`, `ITokenFormatter`) | **Hand-rolled fake**, in a `TestDoubles/` folder, one class per role |
| Anything HTTP | **Hand-rolled `HttpMessageHandler`** for the simple cases; **RichardSzalay.MockHttp 7.1.0** when you need URL/query/JSON-body matching and call-count verification |
| Wide third-party interfaces you don't own (`IAmazonBedrockRuntime` has dozens of members) | **NSubstitute 6.2.0** |
| Anything that must run in an AOT test host | Hand-rolled only |

**Second-best mocking library: FakeItEasy 9.0.1** (2026-01-24, repo pushed 2026-08-17, **4** open issues — the healthiest issue tracker of the three). It wins over NSubstitute when you want explicit strict verification and dislike NSubstitute's extension-method-on-any-object syntax, which can produce confusing failures when a substitute is accidentally a concrete type.

**When hand-rolled test doubles beat a mocking library — concretely, for this app:**

1. **When the double needs to *record and replay a protocol*, not answer a call.** A streaming chat response is a sequence: N deltas, then a finish reason, then usage. A `FakeChatClient` that yields a scripted `IAsyncEnumerable<ChatResponseUpdate>` and asserts on the `ChatMessage[]` it was handed is ten lines and reads like the spec. The same thing in NSubstitute is `.Returns(callInfo => ...)` lambdas nobody can debug.
2. **When you need the double to be *inspectable after the fact*.** `fake.LastRequest.Messages`, `fake.SeenSystemPrompt`, `fake.CallCount` — a plain class gives you strongly-typed access; a mock gives you `Received().Method(Arg.Is<...>(x => ...))`, which fails with a stringly-typed diff.
3. **When the interface is narrow and stable.** Interfaces you designed for testability (which is the whole point of putting a port in front of Bedrock) are by definition narrow. A mocking library's value is proportional to member count.
4. **When the test must run under AOT/trimming.** See above — this is absolute, not a preference.
5. **When the double is shared across dozens of tests.** A `FakeLlmBackend` used by 40 tests is a first-class piece of the test codebase and deserves to be a real class with a builder, not 40 copies of arrange-block mock setup.

Conversely, **use the library** when the interface is wide and foreign (AWS/Azure SDK surfaces), when you need one-off exception-throwing behaviour for a resilience test, or when you're spiking and don't yet know the shape.

The existing `StubHttpMessageHandler` is the right instinct but too thin — it returns one canned response regardless of the request. Grow it into a handler that takes a `Func<HttpRequestMessage, HttpResponseMessage>`, records every request it saw, and can emit a chunked SSE stream (which you need for streaming completions).

---

### 3. Testing an application that calls LLMs

This decomposes into four distinct problems that people usually conflate.

#### 3a. The seam: put your own port in front, and use `IChatClient` where the providers already meet you

`Microsoft.Extensions.AI`'s `IChatClient` is now the industry seam in .NET, and **LLamaSharp 0.27.0 already depends on `Microsoft.Extensions.AI.Abstractions ≥ 10.4.1`** — so your local GGUF backend can be an `IChatClient` for free. Microsoft's own guidance is explicit that a hand-written fake beats a mock here: *"A small fake implementation is preferred over mocking `IChatClient` with Moq or NSubstitute"* and *"The fake client does not need to simulate intelligence — it only needs to return the response that the test scenario requires and record what the application sent to it"* ([Unit test your agents in .NET](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/unit-testing-agents-dotnet)).

But `IChatClient` is not enough for *this* app, because `IChatClient` does not model per-token log-probabilities with top-K alternatives in a provider-neutral way. Define your own port — something like:

```
ILlmBackend
  ├─ IAsyncEnumerable<TokenEvent> StreamAsync(ChatRequest, TokenInspectionOptions, CancellationToken)
  ├─ TokenizationResult Tokenize(string)
  └─ BackendCapabilities Capabilities   // supports logprobs? top-K max? vocab exposed?
```

`BackendCapabilities` is the single most valuable testing decision you can make: Azure OpenAI gives you `top_logprobs` up to a service-defined cap, Bedrock's per-model support varies, and llama.cpp gives you the full vocabulary distribution. Making capability a *value* rather than a `try/catch` means you can write one table-driven test suite that runs against every backend and asserts the right degradation.

#### 3b. Deterministic fakes — the layer that carries 90% of the tests

```
FakeLlmBackend
  .Script(TokenEvent.Text("The", logprob: -0.21, alts: [("A", -1.9), ("This", -2.4)]))
  .Script(TokenEvent.Text(" cat", ...))
  .ThenFinish(FinishReason.Stop, usage: new(12, 34))
```

Everything in the app that is *not* an HTTP call or a native call — history management, named system prompts, settings resolution, probability-map construction, attribution back to input spans, the terminal renderers — is tested against this, runs in milliseconds, and never touches a network or a model file. This is where your coverage should live.

#### 3c. On-the-wire contract tests, one small set per provider

The three providers give you three *different* injection seams, and this matters:

| Provider | Seam | Verified |
|---|---|---|
| **Azure OpenAI** (`Azure.AI.OpenAI` on `System.ClientModel`) | `AzureOpenAIClientOptions` derives from `ClientPipelineOptions`, which exposes a **`Transport`** property. Set `Transport = new HttpClientPipelineTransport(new HttpClient(yourStubHandler))`. | Yes — [ClientPipelineOptions](https://learn.microsoft.com/en-us/dotnet/api/system.clientmodel.primitives.clientpipelineoptions?view=azure-dotnet), [AzureOpenAIClientOptions](https://learn.microsoft.com/en-us/dotnet/api/azure.ai.openai.azureopenaiclientoptions?view=azure-dotnet) |
| **Bedrock** (`AWSSDK.BedrockRuntime` **4.0.101.4**, 2026-08-24) | **There is no `HttpClientFactory` property on `ClientConfig` in the v4 API.** V4 removed `DefaultClientConfig.HttpClientFactory`; the guidance is *"Use `AWSConfigs.HttpClientFactory` instead"* — a **process-global**. | Yes — [AWS SDK v4 migration](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/net-dg-v4.html), [ClientConfig v4 API](https://docs.aws.amazon.com/sdkfornet/v4/apidocs/items/Runtime/TClientConfig.html) |
| **LLamaSharp** | No HTTP. In-process P/Invoke. See §4. | — |

**The Bedrock global is a genuine sharp edge.** `AWSConfigs.HttpClientFactory` is static mutable state, so any test that sets it cannot run in parallel with any other test that sets it. Two mitigations, and you want both: (i) put those tests in a single non-parallel xUnit collection — with xUnit v3 4.0's `Parallelization` model, opt that collection out and note that you cannot opt back in below it; (ii) keep the count tiny by pushing everything above the SDK boundary onto `ILlmBackend` fakes, and substituting `IAmazonBedrockRuntime` (which NSubstitute handles fine) for tests about request shaping.

For those small contract suites, **WireMock.Net 2.15.0** is the recommendation over cassette libraries:

- It is a real HTTP server, so it exercises the SDK's *actual* pipeline — retries, signing, timeouts, chunked transfer — rather than short-circuiting the handler. For Bedrock in particular, SigV4 signing is part of what you want to know still works.
- Its **record-via-proxy** mode lets you capture real Azure/Bedrock traffic once, into JSON mappings you can hand-edit and redact, then serve them offline forever.
- Handlebars response templating lets one mapping serve a whole `[Theory]`.
- Apache-2.0, published 2026-08-15, 122k downloads on the current version.

**Second-best: `Vcr.HttpRecorder` 3.1.3** — it wins when you specifically want cassettes in the **HAR** standard format (so you can open them in browser devtools or `har`-aware tooling), and when you'd rather not spin up a listening socket in CI. Its dependencies are `Microsoft.Extensions.Http` and `System.Text.Json` only, which is friendlier to a trimmed build than EasyVCR's Newtonsoft dependency. What you give up: it is a *fork* maintained by one person with ~66 downloads/day. Vendor the cassette format, not the library, if you go this way.

**Do not use `HttpRecorder` 2.0.0** (nventive) — last published 2020-04-06.

**Whatever you record: redact.** Cassettes and WireMock mappings will contain `Authorization: Bearer`, `api-key`, and AWS SigV4 `Authorization` headers. EasyVCR has censoring built in; with WireMock.Net you scrub in the recording step. Add a CI check that greps recorded fixtures for `sk-`, `Bearer `, `AWS4-HMAC` and fails the build — this is a secrets-handling application, and the test fixtures are the likeliest leak path.

#### 3d. Snapshot testing the rich terminal output — and the Verify licence change

This is the single most valuable testing technique for this app. The distinguishing feature is *visual*: probability grids, top-K tables, heat maps. Asserting on them field-by-field is unbearable; asserting on the rendered block of ANSI text is exactly right.

**The mechanism** is `Spectre.Console.Testing` **0.57.2** (2026-07-02). It ships `TestConsole`, `TestConsoleInput` and `TestCapabilities` (confirmed by [file listing in spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console/tree/main/src/Spectre.Console.Testing)). The critical part is `TestCapabilities`: you pin colour depth, Unicode support, ANSI on/off and width, so a heat map renders identically on a Windows dev box and an Ubuntu CI runner. Without that pinning, snapshot tests of coloured output are flaky by construction.

**The comparison tool** is where the news is. **Verify 32.0.0 / Verify.XunitV3 32.0.0** shipped 2026-08-26, MIT-licensed source. But from the project's own readme:

> "Every version released after 1 September 2026 is covered by a maintenance fee for organizations that generate revenue, and for government agencies. The source code stays freely available... Versions released on or before that date are not."

Tiers: **<40 employees $5/mo; 40–200 $20/mo; 200–1000 $30/mo; >1000 $100/mo**. Exempt: individuals, non-revenue organisations (governments excluded from the exemption), and orgs engaging the maintainers for consulting. And:

> "From v33 (currently available as a beta on nuget), sponsorship is validated at build time by [SponsorCheck](https://github.com/SimonCropp/SponsorCheck). Each package bundles a hashed list of sponsors and a build-time verifier, and a consuming project declares how it is licensed as metadata on the `PackageReference`... `SponsorshipLicenseIgnored="true"`, which builds with a warning rather than an error. Nothing phones home."

SponsorCheck is deliberately *not* SponsorLink: it runs entirely offline inside MSBuild, hashes are bundled at pack time, there is no network call and no runtime dependency, and unlicensed use produces an `SC0xx` **warning**, not an error. That is a materially better design — but note that with `TreatWarningsAsErrors` (which I recommend below) an ignored-licence warning **becomes a build failure**, so you must either sponsor, or add an explicit `NoWarn` for the SC code, or pin.

**Three viable positions, pick one deliberately:**

1. **Pin `Verify.XunitV3` at 32.0.0.** Released 2026-08-26 — before the 1 September cut-off — therefore fee-free in perpetuity. Costs you future Verify features. This is the lowest-friction choice and what I'd do first.
2. **Sponsor.** $5–$100/month depending on headcount. Verify is worth it if you lean on it; its diff-tool integration (`DiffEngine`) makes accepting a changed heat-map a one-keystroke operation, which is the difference between snapshot tests being loved and being deleted.
3. **Hand-roll.** `AssertSnapshot(actual, [CallerFilePath], [CallerMemberName])` that writes `*.received.txt` next to `*.verified.txt`, diffs, and honours an `ACCEPT_SNAPSHOTS=1` env var. About 40 lines. You lose scrubbers, the diff-tool launcher, and parameterised-snapshot naming. For a codebase whose snapshots are all "block of ANSI text", that loss is small.

**Do not** reach for `Snapshooter.Xunit` as the free alternative without checking first: 1.3.1 (2026-02-24) still declares dependencies on `xunit.core`/`xunit.assert` **≥ 2.4.2**, i.e. xUnit **v2**.

#### 3e. How you test a TUI at all

Three layers, and only the first two belong in CI:

1. **Render-to-string.** Never let a renderer write to `AnsiConsole` directly. Every formatter takes an `IAnsiConsole` (Spectre) or an `IIoContext` (Xcaciv.Command — the interface already exists at `src/Xcaciv.Command.Interface/IIoContext.cs`, and the repo already has a `TestTextIo` test double). Point it at a `TestConsole` with fixed `TestCapabilities`, render, snapshot the string. This covers grids, tables, heat maps, and the token-probability visualiser completely.
2. **Drive the shell as a state machine.** `Xcaciv.Command`'s `ICommandController` / `ICommandExecutor` / `IPipelineExecutor` are the app's real entry points. Feed command lines in through a `TestTextIo`-style double, assert on `CommandResult` and on the captured output. This tests `/history`, `/prompt`, `/settings`, `/inspect` etc. without a terminal existing. Xcaciv.Command's own test suite already has a `CommandControllerTestHarness` — reuse that shape.
3. **Full-screen TUI (`Terminal.Gui`).** Terminal.Gui v2 is GA at **2.4.17** (2026-07-07, targets `net10.0`). Its own tests drive a fake console driver and assert on the character buffer after redraw; the project has been [replacing the v1 `FakeDriver`](https://github.com/gui-cs/Terminal.Gui/issues/3947) and has disabled all real driver I/O in its unit tests. Treat this layer as **thin and barely tested on purpose**: keep all logic out of the Terminal.Gui views, let the views delegate to the same renderers you snapshot in layer 1, and accept a handful of smoke tests. Note that the current `ChatDbg.Shell.Gui` pins `Terminal.Gui 1.19.0` — moving to v2 is a rewrite of that project, and it is a separate decision from the testing strategy.
4. **(Optional, out of CI) End-to-end PTY.** Spawning the published binary under a pseudo-terminal and asserting on the byte stream is possible but slow, flaky and platform-divergent. Do it as a nightly smoke test for the *self-contained binary artifact* — one test: "it starts, prints a banner, accepts `/quit`, exits 0" — on both `win-x64` and `linux-x64`. That test's real job is catching AOT/trimming regressions, not UI regressions.

---

### 4. Testing native-backed local inference without a multi-gigabyte model in CI

**The proven pattern comes from LLamaSharp itself.** Its `LLama.Unittest` project uses an MSBuild `DownloadFileItem` item group plus a `DownloadSingleFile` target that skips the download when the file already exists, pulling models straight from Hugging Face into a `Models/` folder. Its smallest general model is **`smollm-360m-instruct-add-basics-q8_0.gguf`** from `HuggingFaceTB/smollm-360M-instruct-v0.2-Q8_0-GGUF`. It also references `Xunit.SkippableFact` for conditional tests (which you don't need on xUnit v3).

Copy the mechanism; go smaller than they do.

**Model size tiers, pick by what the test proves:**

| Model | Approx. size | What it can prove |
|---|---|---|
| `tinyllama-15M-stories` GGUF (e.g. [tensorblock/tinyllama-15M-stories-GGUF](https://huggingface.co/tensorblock/tinyllama-15M-stories-GGUF)) | ~15–20 MB | Native loads. Tokenizer round-trips. Logits array has vocab-size length. Sampler returns a token. Top-K slicing works. **This is your PR-gate model.** |
| `SmolLM-135M` Q8_0 ([HuggingFaceTB](https://huggingface.co/HuggingFaceTB/smollm-135M-instruct-v0.2-Q8_0-GGUF), [QuantFactory](https://huggingface.co/QuantFactory/SmolLM-135M-GGUF)) | ~140–150 MB | Everything above, plus a chat template applies and a short greedy completion is stable enough to assert on. |
| `smollm-360M-instruct` Q8_0 | ~390 MB | What LLamaSharp itself gates on. Nightly, not per-PR. |

**Concretely:**

```xml
<!-- tests/…Inference.Tests.csproj -->
<ItemGroup>
  <TestModel Include="tinyllama-15M-stories-Q8_0">
    <SourceUrl>https://huggingface.co/…/resolve/main/…-Q8_0.gguf</SourceUrl>
    <LocalFileName>tiny.gguf</LocalFileName>
  </TestModel>
</ItemGroup>
```

- The download target **must** be idempotent and skip-if-exists (LLamaSharp's is).
- **Cache it in CI**, keyed on the URL, not on a lockfile. On GitHub Actions, `actions/cache` around the `Models/` directory turns a 20 MB download into a cache hit.
- **Never commit the `.gguf`.** Even 15 MB in git history is a permanent tax, and model licences are not all redistribution-friendly.
- Have the test project **fail loudly but skip cleanly**: `Assert.SkipUnless(File.Exists(ModelPath), $"Set CHATDBG_TEST_MODEL or run `dotnet build -t:DownloadTestModels`")`.

**Skipping by trait.** Give every test that needs a native backend a trait, and make the trait the thing CI filters on:

```csharp
[Trait("Category", "NativeInference")]
[Fact(SkipUnless = nameof(TinyModelAvailable))]
public async Task TopK_alternatives_are_ordered_by_descending_logprob() { … }

public static bool TinyModelAvailable => File.Exists(TestModels.Tiny);
```

Then `dotnet test --filter-not-trait "Category=NativeInference"` for the fast PR gate, and the full run nightly. Under MTP, note that **zero discovered tests exits with code 8** — so an over-aggressive filter fails the build rather than silently passing, which is the behaviour you want.

**The contract-test pattern is the payoff.** Write the backend test suite once, as an abstract class parameterised by a factory:

```csharp
public abstract class LlmBackendContract
{
    protected abstract ILlmBackend CreateBackend();
    protected abstract BackendCapabilities Expected { get; }

    [Fact] public async Task Tokenize_then_detokenize_round_trips() { … }
    [Fact] public async Task Streaming_emits_finish_reason_exactly_once() { … }
    [Fact] public async Task TopK_count_never_exceeds_capability_cap() { … }
    [Fact] public async Task Cancellation_stops_the_stream_and_disposes_native_state() { … }
    [Fact] public async Task Logprobs_are_non_positive_and_sum_to_at_most_zero() { … }
}

public sealed class FakeBackendContract   : LlmBackendContract { … }   // always runs
public sealed class LlamaSharpContract    : LlmBackendContract { … }   // trait-gated, tiny model
public sealed class AzureOpenAIContract   : LlmBackendContract { … }   // trait-gated, WireMock
public sealed class BedrockContract       : LlmBackendContract { … }   // trait-gated, WireMock
```

This is the mechanism that makes "three backends, one behaviour" true rather than aspirational, and it is the reason the `BackendCapabilities` value object earns its keep — the shared assertions consult it instead of branching on backend type.

**Two native-specific hazards to plan for:**

- **Native crashes and hangs.** `llama.cpp` can `abort()` on a malformed GGUF or OOM, taking the test host with it. Add `Microsoft.Testing.Extensions.CrashDump` and `Microsoft.Testing.Extensions.HangDump` (both **2.3.3**, 2026-07-28, **MIT**, depend on `Microsoft.Diagnostics.NETCore.Client`). `--hangdump --hangdump-timeout 5m` turns an opaque 6-hour CI timeout into a dump file.
- **Native state is not parallel-safe.** Model/context handles are expensive and often not thread-safe. Put all native inference tests in one non-parallel collection, share one loaded model via a fixture, and dispose it deterministically. This interacts directly with xUnit v3 4.0's parallelisation change: if you ever set `Mode = All` assembly-wide, remember you **cannot opt back in** below a layer you opted out of, so structure the opt-out at the collection level.

---

### 5. Coverage and quality gates

#### 5a. MTP vs VSTest — decided above, but the operational consequences

- Single `global.json` at the repo root selects the runner for the whole solution. Because mixing is explicitly unsupported, this decision has to cover any Xcaciv.* projects built from source.
- MTP is *executable-first*: a test project is a program. That means `dotnet run --project tests/…` works, `dotnet watch` works, and CI can invoke the test binary directly with no `vstest.console` in the picture.
- MTP has **stricter defaults** by design — Microsoft's comparison page frames it as *"it can fail when no tests run, reduce environment-dependent variability, and let you disable individual extensions per environment."* Good for a project that is going to lean on trait filters.
- What you give up: some third-party CI integrations still lag. On Azure DevOps you must use `DotNetCoreCLI@2`/`dotnet test`, **not** the `VSTest@3` task. Verify your coverage-reporting step accepts Cobertura from MTP before you commit.

#### 5b. coverlet vs Microsoft.CodeCoverage

| | `coverlet.mtp` 10.0.1 | `Microsoft.Testing.Extensions.CodeCoverage` 18.10.0 |
|---|---|---|
| Licence | **MIT** | **Proprietary Microsoft licence** — forbids offering "as a stand-alone offering", liability capped at $5.00 |
| Published | 2026-05-18 | 2026-08-12 |
| Invocation | `--coverlet` | `--coverage` |
| Instrumentation | IL rewriting at build/host-start | Microsoft's dynamic instrumentation engine |
| Config | `testconfig.json` (MTP standard) or `coverlet.mtp.appsettings.json` | `testconfig.json` from MTP 2.3.0; `.runsettings`-style XML |
| Formats | json, lcov, cobertura, opencover, teamcity | coverage, cobertura, xml |
| Native/mixed code | Managed only | Better at mixed managed/native |
| Requires | `Microsoft.Testing.Platform ≥ 2.2.2` | MTP |

**Recommendation: `coverlet.mtp` 10.0.1.** The licence is the deciding factor for a product that ships as an open, self-contained binary; the MIT/proprietary distinction is the kind of thing that surfaces in a legal review long after the choice was made. The correction to a 2025-trained prior is worth stating plainly: **coverlet is not stagnant.** It shipped 8.0.0 (2026-02-14), 10.0.0 (2026-04-17) and 10.0.1 (2026-05-18), and its repo was pushed 2026-08-22 with 16 open issues. Its MTP integration is a first-class native extension implementing `ITestHostProcessLifetimeHandler`, not a shim ([coverlet MTP integration docs](https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/Coverlet.MTP.Integration.md)).

**Second-best: Microsoft's extension**, and it wins in exactly one case — if you end up wanting coverage that spans the managed/native boundary into llama.cpp, or if you standardise on Azure DevOps' built-in coverage UI which is tuned for the `.coverage` format.

**Thresholds.** Do not set a single repo-wide number. Set per-assembly gates in `testconfig.json` / coverlet settings:

| Assembly | Line threshold | Why |
|---|---|---|
| `*.Core.TokenInspection` (log-probs, top-K, probability maps, attribution) | **90%** | This is the product. It is pure functions over numbers; there is no excuse. |
| `*.Core` (history, prompts, settings, secrets) | 80% | Mostly pure, some I/O. |
| `*.Providers.*` (Azure/Bedrock/LLamaSharp adapters) | 50% | Thin translation layers; contract tests cover behaviour, not lines. |
| `*.Shell.Gui` (Terminal.Gui) | **excluded** | Deliberately untested; keep it empty of logic instead. |

#### 5c. Mutation testing with Stryker.NET

**`dotnet-stryker` 4.16.0** (2026-07-03; repo pushed 2026-08-28, 193 open issues). The relevant new thing: an **MTP runner, introduced in 4.13 (announced 2026-03-13) and still labelled preview** ([Stryker blog](https://stryker-mutator.io/blog/stryker-net-mtp-runner/)). Enable with `--test-runner mtp` or `"test-runner": "mtp"`. It keeps the test host alive across mutants instead of spawning a VSTest process per run — a large speedup — and it is what makes Stryker work with MTP-only frameworks at all.

Stated limitations, verbatim in effect: coverage analysis is incomplete (Stryker can filter unmutated code but *cannot yet map which test covers which mutant*), and .NET Framework test projects are unsupported. The missing mutant-to-test mapping means slower runs and less useful reports — which is another reason to scope it.

**Scope it hard.** Mutation testing the whole solution is a multi-hour job that no one will run. Point it at the token-inspection namespaces only:

```json
{ "stryker-config": {
    "project": "Xcaciv.ChatDbg.Core.csproj",
    "mutate": ["**/TokenInspection/**.cs", "**/Tokenization/**.cs"],
    "test-runner": "mtp",
    "thresholds": { "high": 85, "low": 70, "break": 60 },
    "reporters": ["html", "markdown"]
} }
```

Run it on a schedule (weekly) or on PRs that touch those paths, not on every PR. Mutation score is the only metric that will actually tell you whether your top-K and probability-map tests assert anything, as opposed to merely executing the code — which line coverage cannot distinguish. Given that the product's differentiator is numerical correctness in that exact code, this is the highest-value gate on the list after the compiler.

#### 5d. The analyser set worth enabling

Put this in `Directory.Build.props` at the repo root. The current one contains only `<LangVersion>latest</LangVersion>`, which is leaving almost everything on the table.

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>

    <!-- Correctness -->
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsNotAsErrors></WarningsNotAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>

    <!-- Analysers -->
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest-recommended</AnalysisLevel>

    <!-- AOT / trimming, because that is how this ships -->
    <IsAotCompatible>true</IsAotCompatible>
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
    <EnableSingleFileAnalyzer>true</EnableSingleFileAnalyzer>
    <EnableAotAnalyzer>true</EnableAotAnalyzer>

    <!-- Reproducibility -->
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" PrivateAssets="all" />
    <PackageReference Include="Meziantou.Analyzer" PrivateAssets="all" />
    <AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.txt" />
  </ItemGroup>
</Project>
```

Notes on each, with the current facts:

- **`AnalysisLevel`** defaults to `latest` with `AnalysisMode=Default`; the compound value `latest-recommended` means "latest analysers, Recommended rule set". `EnableNETAnalyzers` is already `true` by default for .NET 5+ projects, but stating it prevents surprise if someone sets `AnalysisMode=None` locally. `AnalysisMode` values are `None | Default | Minimum | Recommended | All`; `AnalysisLevel` takes precedence when both are set ([MSBuild props reference](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#analysislevel)).
- **`TreatWarningsAsErrors`** — do it, but note the two interactions above: (a) a future Verify v33 `SC0xx` unlicensed-build warning would become an error; (b) the current csprojs set `SuppressTrimAnalysisWarnings=true`, which is precisely the opposite of what you want. Delete that suppression and fix the warnings; an AOT app that suppresses trim analysis is an app that fails at runtime on a code path CI never took.
- **The AOT/trim analysers** are the highest-leverage gate this project isn't using. `IsAotCompatible=true` turns on the trim, single-file and AOT analysers together, and it is the only way to catch a reflection-based JSON serialiser or a `Type.GetType` in a plugin loader *before* it ships as a broken binary. This matters doubly because **Xcaciv.Loader** does runtime type loading by design — the analyser will tell you exactly where the `[DynamicallyAccessedMembers]` annotations have to go.
- **`Microsoft.CodeAnalysis.BannedApiAnalyzers` 5.6.0** (2026-07-02, MIT). Your `BannedSymbols.txt` writes itself for this app:
  ```
  M:System.Console.WriteLine        ; all output goes through IIoContext/IAnsiConsole
  M:System.Console.Write
  P:System.Console.Out
  T:Newtonsoft.Json.JsonConvert     ; System.Text.Json source-gen only, for AOT
  M:System.Text.Json.JsonSerializer.Serialize``1(``0,System.Text.Json.JsonSerializerOptions)  ; force the JsonTypeInfo overloads
  M:System.DateTime.get_Now         ; use TimeProvider so history timestamps are testable
  M:System.DateTime.get_UtcNow
  M:System.Environment.GetEnvironmentVariable(System.String)  ; go through ISettingsSource
  T:System.Threading.Thread
  M:System.String.ToLower           ; culture bugs in tokenizer paths
  ```
  Every one of those is enforcing an architectural decision that the test strategy depends on. Banning `DateTime.Now` in favour of `TimeProvider` (which has `FakeTimeProvider` in `Microsoft.Extensions.TimeProvider.Testing`) is what makes conversation-history tests deterministic. Banning `Console.*` is what makes the render-to-string TUI strategy actually hold.
- **`Meziantou.Analyzer` 3.0.192** (August 2026, MIT, 220+ rules). Its `CancellationToken` propagation, `ConfigureAwait`, `StringComparison` and "use `TimeProvider`" rules are directly relevant to a streaming, cancellable, cross-platform CLI.
- **`SonarAnalyzer.CSharp` 10.33.0.1635** (2026-08-21) only if you already run SonarQube. Otherwise it is duplicate findings and a slower build.
- **Analyser build cost is real.** If the build slows unacceptably, use `<RunAnalyzersDuringLiveAnalysis>false</RunAnalyzersDuringLiveAnalysis>` locally and keep the full set on in CI — but keep `TreatWarningsAsErrors` on locally so nobody is surprised at push time.

---

### 6. Benchmarking the token-processing hot paths

**BenchmarkDotNet 0.15.8**, published **2025-11-30**. This is the one recommendation where the last stable release is ~9 months old, and you should know that: `0.16.0-preview.1` shipped 2026-06-30 and the repo is actively pushed (2026-08-16, 11.5k stars). It remains the only credible option in .NET — there is no second-best worth naming, only "don't benchmark", and for an app whose selling point is per-token analysis over a full vocabulary, not benchmarking is not an option.

Confirmed relevant capability: BenchmarkDotNet supports **`RuntimeMoniker.Net10`, `NativeAot10` and `Mono10`** with full toolchain support, and its NativeAOT toolchain works by generating a project that references your benchmarks and compiling it with **ILCompiler** ([toolchains docs](https://benchmarkdotnet.org/articles/configs/toolchains.html)). That matters enormously here: this app ships AOT, and **JIT and AOT performance characteristics diverge sharply** on exactly the code you care about — tight loops over `float[]`/`Span<float>`, devirtualisation, and whether the vectorised path in `System.Numerics.Tensors` gets selected. Benchmarking only the JIT build would measure a binary you don't ship.

**What to benchmark — the actual hot paths in this application:**

| Benchmark | Why it is hot | What to measure |
|---|---|---|
| Softmax / log-softmax over the full vocabulary | Runs once per generated token; vocab is 32k–256k floats | ns/op, allocations, scaling by vocab size |
| Top-K selection from the logit array | Per token, and K is user-configurable | Compare full sort vs partial selection vs heap; the crossover K is worth knowing |
| Probability-map construction (vocab → renderable buckets) | Per token, for the map view | Allocations above all — this is where a naive LINQ chain will allocate megabytes |
| Tokenization / detokenization round-trip | Per request, and per attribution query | Throughput on realistic prompt sizes |
| Token→input-span attribution | O(tokens × spans) if done naively | Complexity behaviour, not just constant factor |
| ANSI rendering of a heat map (Spectre) | Per redraw, and redraws are frequent while streaming | Time and allocations per frame — a 60-line heat map re-rendered per token is a real cost |

**How to set it up:**

- **A separate `benchmarks/` project**, `Exe`, Release-only, **not** a test project and **not** in the `dotnet test` graph. Xcaciv.Command does exactly this (`src/tests/BenchmarkSuite1/`), so there is precedent in the ecosystem you're building on.
- `[MemoryDiagnoser]` on every benchmark. For this workload, allocations matter more than nanoseconds: an allocation per token in a streaming loop is a GC pause mid-render.
- `[DisassemblyDiagnoser(maxDepth: 2)]` on the softmax and top-K benchmarks specifically — that's how you confirm the SIMD path was taken.
- Multi-runtime jobs so you compare what you ship against what you debug:
  ```csharp
  [SimpleJob(RuntimeMoniker.Net10)]
  [SimpleJob(RuntimeMoniker.NativeAot10)]
  [MemoryDiagnoser]
  public class TopKBenchmarks { … }
  ```
- `[Params(1000, 32000, 128256, 256000)]` for vocab size and `[Params(1, 5, 20, 100)]` for K — the *shape* of the curve is the finding, not any single number.
- **Never** run benchmarks as part of the test suite or the PR gate. Shared CI runners are too noisy for absolute numbers.
- **For regression detection**, [`benchmark-action/github-action-benchmark`](https://github.com/benchmark-action/github-action-benchmark) (repo pushed 2026-07-23) natively consumes BenchmarkDotNet output, plots trends with Chart.js on a GitHub Pages branch, and can comment on a PR or fail the workflow past a threshold. Its default alert threshold is 200% — far too loose. Set it to ~110–125% and run it on a schedule from a consistent runner, treating alerts as *investigate*, not *block*.

---

### 7. Recommendation: the concrete test strategy, by layer

| # | Layer | Runs | Framework / tools | Speed budget | Gate |
|---|---|---|---|---|---|
| 0 | **Compile-time** | Every build | Roslyn analysers, `Nullable`, `TreatWarningsAsErrors`, AOT/trim analysers, BannedApi, Meziantou | — | Blocking |
| 1 | **Unit** — token math, history, prompts, settings, secrets serialisation, parameter parsing | Every PR | xUnit v3 4.0 + hand-rolled fakes | < 10 s total | Blocking, 90% on token namespaces |
| 2 | **Render snapshot** — grids, top-K tables, heat maps, probability maps | Every PR | xUnit v3 + `Spectre.Console.Testing` `TestConsole` with pinned `TestCapabilities` + Verify 32.0.0 (pinned) or a hand-rolled golden-file helper | < 5 s | Blocking |
| 3 | **Command / shell integration** — `Xcaciv.Command` controller, pipelines, `Xcaciv.Loader` plugin discovery | Every PR | xUnit v3 + `TestTextIo`-style `IIoContext` double + a real temp directory for the loader | < 30 s | Blocking |
| 4 | **Provider contract (stubbed)** — the abstract `LlmBackendContract` against fakes, and against WireMock.Net for on-the-wire shape | Every PR | xUnit v3 + WireMock.Net 2.15.0 + recorded, redacted mappings | < 60 s | Blocking |
| 5 | **Native inference (tiny model)** — the same contract against LLamaSharp with `tinyllama-15M-stories` | Every PR *if the cached model is present*, else skipped via `Assert.SkipUnless` | xUnit v3 + MSBuild `DownloadFile` + `actions/cache` + Crash/HangDump extensions | < 2 min | Blocking when present |
| 6 | **Mutation** — token-inspection namespaces only | Weekly + on PRs touching those paths | `dotnet-stryker` 4.16.0, `--test-runner mtp` | ~15–30 min | Report; break below 60 |
| 7 | **Benchmarks** — softmax, top-K, probability map, attribution, render | Scheduled, dedicated runner | BenchmarkDotNet 0.15.8, `Net10` + `NativeAot10` jobs | ~20 min | Alert at ~115% |
| 8 | **Live provider smoke** — one real call each to Azure OpenAI, Bedrock, and a 360M local model | Nightly, credentials from CI secrets | xUnit v3, trait-gated `Category=Live` | ~5 min | Non-blocking; opens an issue on failure |
| 9 | **Model-quality eval** — does inspection output stay sane as models change | Nightly / on demand | `Microsoft.Extensions.AI.Evaluation` 10.8.0 + `.Reporting` response cache + `dotnet aieval` report | varies | Report only |
| 10 | **Published-artifact smoke** — the AOT self-contained binary starts, prints a banner, exits 0, on `win-x64` and `linux-x64` | Per release + nightly | Shell script, no test framework | < 1 min | Blocking for release |

**Central package management is mandatory**, not optional, for a suite this wide — a single `Directory.Packages.props` with `ManagePackageVersionsCentrally`, mirroring what Xcaciv.Command already does. Add `packageSourceMapping` from day one (Xcaciv.Command's `NuGet.config` already does this): the Xcaciv.* packages are **not on nuget.org** — `Xcaciv.Command.Interface` returns 404 there and a nuget.org search for "Xcaciv" returns only unrelated `XCBatch.*` packages. They come from `https://nuget.pkg.github.com/xcaciv/` and a local feed. CI needs a GitHub Packages token, and source mapping is what prevents a same-named package appearing on nuget.org from being pulled instead.

---

## What this application specifically needs — recommendation → operation

| Recommendation | The concrete operation it protects |
|---|---|
| **xUnit v3 4.0 on MTP v2** | The app ships as an AOT, trimmed, self-contained binary for Windows and Linux. MTP is the only platform that can execute tests in that mode at all; VSTest cannot. Today's suite (`xunit 2.9.1`, `Microsoft.NET.Test.Sdk 17.12.0`, VSTest) can never test the shipped configuration. |
| **Hand-rolled fakes as the default** | `FakeLlmBackend` scripts a streaming response with per-token log-probs and top-K alternatives and records the `ChatMessage[]` it was handed. A Castle.Core mock can neither express that sequence readably nor run inside an AOT test host. |
| **NSubstitute 6.2.0, narrowly** | `IAmazonBedrockRuntime` has dozens of members. When a test only cares that `ConverseStreamAsync` was called with a particular `inferenceConfig`, a substitute is the honest tool. Not for anything you own. |
| **Drop Moq** | The repo pins `Moq 4.20.69` — a SponsorLink-era build — and Moq has not published since 2024-09-07. Two .NET majors have shipped since. |
| **Own port + `BackendCapabilities`** | Azure OpenAI caps `top_logprobs`; Bedrock's support varies by model; llama.cpp exposes the whole vocabulary. Making that a value lets one `LlmBackendContract` suite assert correct degradation for all three instead of three divergent suites. |
| **`AzureOpenAIClientOptions.Transport`** | Lets a test drive the *real* `Azure.AI.OpenAI` client — its serialisation, its streaming parser, its retry policy — against a stub handler, with no network and no mock. This is the seam the SDK was designed to give you. |
| **`IAmazonBedrockRuntime` substitution + a serialised WireMock collection for Bedrock** | AWS SDK v4 has **no per-client `HttpClientFactory`**; the guidance is the process-global `AWSConfigs.HttpClientFactory`. Test-level parallelism and a process global are incompatible, so Bedrock HTTP tests must be few and must be pinned to one non-parallel collection. |
| **`Spectre.Console.Testing` + pinned `TestCapabilities`** | The heat map, the top-K grid, the probability map. Pinning colour depth, Unicode and width is what stops these snapshots differing between a Windows dev machine and an Ubuntu runner. |
| **Snapshot testing (Verify 32.0.0 pinned, or hand-rolled)** | "Render the top-20 alternatives for token 7 as a table with a diverging colour scale" is a 60-line block of ANSI. Field-by-field assertions on it will not be written, and will not be maintained. |
| **Tiny GGUF + MSBuild download + `Assert.SkipUnless`** | Proves the P/Invoke boundary, tokenizer round-trip, logits-array length, sampler behaviour and native disposal — at ~15 MB and a couple of seconds, on every PR. LLamaSharp's own CI does exactly this at 390 MB; you can do it smaller. |
| **Crash/HangDump MTP extensions** | `llama.cpp` `abort()`s on a malformed GGUF and can wedge on OOM. Without these, that is a silent CI timeout; with them, it is a dump file. |
| **Stryker scoped to token namespaces** | Line coverage cannot tell you whether your top-K test asserts anything. Mutation score can. The token maths *is* the product; nothing else in the app justifies the runtime. |
| **BenchmarkDotNet with `Net10` **and** `NativeAot10` jobs** | The softmax over a 128k vocabulary is executed once per generated token. Its JIT and AOT performance differ, and only one of those is what the user runs. |
| **`IsAotCompatible` + trim analysers, and deleting `SuppressTrimAnalysisWarnings=true`** | `Xcaciv.Loader` loads types at runtime by design. Trim analysis is the only mechanism that will tell you which of those paths the trimmer is about to delete, before a customer finds out. |
| **`BannedSymbols.txt` for `Console.*`, `DateTime.Now`, `Newtonsoft.Json`, `Environment.GetEnvironmentVariable`** | Each ban enforces a seam the tests depend on: output through `IIoContext`, time through `TimeProvider`, serialisation through source-generated `System.Text.Json` (AOT-safe), configuration through a settings port. |
| **Redaction check on recorded fixtures** | The app stores provider secrets. Any recorded Azure or Bedrock interaction contains a live `Authorization` header until you strip it. |

---

## Risks, sharp edges and what you give up

**1. xUnit v3 4.0.0 is two weeks old.** The AOT work rewrote the runner stacks and added `CodeGenTestXyzRunner`/`CoreTestXyzRunner` alongside the reflection path; obsolete methods became non-callable; runner-context constructors changed. Third-party extensions may lag. *Mitigation*: pin 4.0.0; document that the rollback is 3.2.2, and that rolling back also flips you to MTP v1, which the .NET 10 SDK's `dotnet test` treats differently.

**2. Parallelisation semantics changed shape.** `CollectionBehavior` properties are obsolete in favour of `[assembly: Parallelization]`. The default is still `collections`, so nothing breaks on upgrade — but the moment someone tries `Mode = All` for speed, the shared native model fixture and any `AWSConfigs.HttpClientFactory` test will start failing nondeterministically, and *you cannot opt back in below a layer you opted out of*.

**3. Verify's Open Source Maintenance Fee, effective 1 September 2026 — four days from now.** Versions released on or before that date are exempt forever; 32.0.0 (2026-08-26) qualifies. From v33, SponsorCheck emits an `SC0xx` **warning** on unlicensed builds — which, with `TreatWarningsAsErrors`, is a build failure. This is not SponsorLink (no network call, no telemetry, offline hash check, warning not error), but it is a decision that has to be made explicitly rather than discovered by a red build. *What you give up by pinning at 32.0.0*: future Verify features and fixes.

**4. MTP's ecosystem is still catching up.** Microsoft's own comparison page concedes *"some third-party integrations might still lag behind VSTest"* and warns against mixing platforms in one solution. Practical consequences: on Azure DevOps you must use `DotNetCoreCLI@2`, not `VSTest@3`; verify your coverage publisher accepts MTP's Cobertura output before you build a dashboard on it.

**5. Stryker's MTP runner is preview and cannot map mutants to tests.** Announced 2026-03-13 for 4.13, still preview at 4.16.0. The missing coverage-per-mutant analysis means slower runs and weaker reports. *Fallback*: run Stryker with the VSTest runner against a v2-style project — which you won't have — or accept the preview. Realistically: accept it, scope it narrowly, and don't make it blocking above a low `break` threshold.

**6. AWS SDK v4's global `HttpClientFactory` is a testability regression.** `ClientConfig` has no `HttpClientFactory` property in v4; `DefaultClientConfig.HttpClientFactory` was removed. You are pushed toward a process-global or toward interface substitution. *What you give up*: the ability to run Bedrock transport tests in parallel with anything else.

**7. Spectre.Console is 0.x, and `Spectre.Console.Cli` has drifted.** `Spectre.Console` is at 0.57.2 (2026-07-02) while `Spectre.Console.Cli` is at 0.55.0 (2026-04-03). Pre-1.0 means rendering output can change between minor versions — and rendering output is exactly what you are snapshotting. *Mitigation*: exact-pin `Spectre.Console` and `Spectre.Console.Testing` to the same version in CPM, and treat a Spectre bump as a deliberate task that includes re-approving snapshots.

**8. Downloading models in CI is a supply-chain dependency on Hugging Face.** A HF outage, a repo rename, or a licence change breaks the build. *Mitigations*: cache aggressively; assert the SHA-256 of the downloaded file; mirror the tiny model into your own artifact store; and make the tests *skip* rather than *fail* when the model is absent so an HF outage degrades the gate instead of blocking every PR.

**9. `TreatWarningsAsErrors` plus 220+ Meziantou rules plus `latest-recommended` will produce hundreds of diagnostics on day one.** *Mitigation*: turn analysers on at the start of the rebuild, not after. Retrofitting them onto a finished codebase is how teams end up with a `NoWarn` list longer than the ruleset.

**10. `SuppressTrimAnalysisWarnings=true` is currently set in the Compact and SingleFile configurations.** That is the single most dangerous line in the existing build: it means the AOT binary is shipped with the compiler's warnings about deleted reflection targets silenced. Removing it will surface real work — particularly around `Xcaciv.Loader` and any reflection-based JSON — but that work is the difference between an AOT build that runs and one that throws on an untested path.

**What you give up by taking the whole recommendation:**

- **A single `dotnet test` that "just works" in every tool.** Committing to MTP means some integrations, dashboards and IDE plugins will be a step behind for a while.
- **The convenience of `mock.Setup(...)` everywhere.** Hand-rolled fakes are more code up front. The payoff is tests that survive AOT and read like specifications; the cost is a `TestDoubles/` folder somebody has to own.
- **Verify's newest features**, if you pin at 32.0.0 — or $5–$100/month, if you don't.
- **Fast full-suite runs, once native inference is in it.** Even a 15 MB model costs seconds per test run. That is why the trait-gate exists, and why the fast lane must be the default.
- **Absolute performance numbers from CI.** Shared runners are too noisy; benchmarks give you *trends* on a dedicated machine, not gate-able absolutes.

---

## Unconfirmed

Everything below I could not verify on the live web on 2026-08-28, with where I looked.

1. **Whether `Spectre.Console.Cli` still ships `CommandAppTester`.** A file-tree search of [`spectreconsole/spectre.console`](https://github.com/spectreconsole/spectre.console) `main` found `TestConsole`, `TestConsoleInput` and `TestCapabilities` under `src/Spectre.Console.Testing/` but **no** `CommandAppTester` and no `Spectre.Console.Cli.Testing` project. `Spectre.Console.Cli` is separately versioned at 0.55.0 (2026-04-03) vs `Spectre.Console` 0.57.2. Low impact here — this app dispatches commands through `Xcaciv.Command`, not `Spectre.Console.Cli`.
2. **Whether `Snapshooter` has an xUnit v3 package.** `Snapshooter.Xunit` 1.3.1 (2026-02-24) still declares `xunit.core`/`xunit.assert ≥ 2.4.2`. I did not find a `Snapshooter.XunitV3` on nuget.org. Treat Snapshooter as xUnit-v2-only until proven otherwise.
3. **Whether Stryker's MTP runner has graduated from preview in 4.16.0.** Confirmed preview in 4.13 via the [Stryker blog post of 2026-03-13](https://stryker-mutator.io/blog/stryker-net-mtp-runner/). I found no release note for 4.14/4.15/4.16 declaring it stable, and did not read the full changelogs.
4. **Whether xUnit v3 4.0's Native AOT test execution works with a test assembly that P/Invokes into `llama.cpp`.** The AOT runner stack is confirmed to exist; nobody has published a report of it working with a native-dependency-heavy test project. Assume it needs proving before you rely on it, and treat AOT test execution as a stretch goal rather than a day-one requirement.
5. **The exact third dropped support line in xUnit v3 4.0.** The release page names MTP v1 and Mono; a secondary article referenced "three dropped support lines". `xunit.v3` 4.0.0's declared targets are net8.0+ and net472+.
6. **Whether `AWSConfigs.HttpClientFactory` exists and is honoured in AWSSDK.Core 4.0.102.x at runtime.** Confirmed only from the [v4 migration guide's](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/net-dg-v4.html) prose recommendation; I did not verify it in the v4 API reference or in code.
7. **The full terms of the `Microsoft.Testing.Extensions.CodeCoverage` licence.** A WebFetch of the [licence page](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CodeCoverage/18.10.0/License) reported no Visual-Studio-subscription tie and standard Microsoft distributable-code terms with a $5.00 liability cap and a bar on redistribution "as a stand-alone offering". Have counsel read it if you plan to redistribute coverage tooling; the coverlet recommendation sidesteps the question entirely.
8. **Whether `coverlet.mtp` 10.0.1 is considered production-stable by its maintainers.** It shares coverlet's version number and is documented in-repo, but I found no explicit GA statement.
9. **TUnit's stability / semantic-versioning policy.** Neither [tunit.dev](https://tunit.dev/) nor the repo README states an API-stability guarantee, LTS cadence, or breaking-change policy. My risk assessment is inferred from release frequency (5 releases 2026-08-18 → 2026-08-26), not from a published policy.
10. **Whether Moq will resume shipping.** [devlooped/moq](https://github.com/devlooped/moq) is actively pushed (2026-08-27) with only 21 open issues, but I found no roadmap, deprecation notice, or statement explaining the 23-month release gap. "Stalled" is my characterisation, not the maintainer's.
11. **Exact on-disk sizes of the candidate tiny GGUF files.** The sizes in the model table are estimates from parameter count and quantisation, not from reading the file listings on Hugging Face. Verify before you set a CI cache budget.
12. **Whether `benchmark-action/github-action-benchmark` parses BenchmarkDotNet's current JSON exporter format without modification.** The action lists BenchmarkDotNet among its supported tools and ships a [BenchmarkDotNet example](https://github.com/benchmark-action/github-action-benchmark/tree/master/examples/benchmarkdotnet), and the repo was pushed 2026-07-23, but I did not check the example against BenchmarkDotNet 0.15.8's output schema.
13. **.NET 11's GA date.** Reported as 2026-11-10 by secondary sources; .NET 11 Preview 7 (`11.0.100-preview.7.26381.103`) was current as of 2026-08-11. I did not confirm the GA date on a Microsoft page. **This does not affect the recommendation** — target `net10.0` (LTS); .NET 11 is STS.
14. **`Meziantou.Analyzer` 3.0.192's publish date** shows as 2026-08-29 on nuget.org, one day ahead of today's date — almost certainly a timezone artefact. Treat it as "late August 2026".
15. **`Microsoft.Extensions.AI.Evaluation.Reporting`'s current version.** Only the core `Microsoft.Extensions.AI.Evaluation` 10.8.0 (2026-07-14) and `.Console` 10.7.0 were confirmed; I assumed the Reporting package tracks the same 10.x line.
