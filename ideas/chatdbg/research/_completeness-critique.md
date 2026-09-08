# Completeness critique — ChatDbg .NET rebuild research package

**Reviewed:** 2026-08-28. Nine research files, ~4,860 lines, `output/chatdbg/research/`.
**Also read for context:** `output/chatdbg/inventory.md` (15-feature inventory), the `dossiers/` and `synthesis/` file listings.

**Overall:** the technical depth per area is genuinely high — version numbers are dated and sourced, several claims are verified by disassembly or by a working local probe, and the `Unconfirmed` sections are honest in a way most research is not. The failures are **not** failures of depth. They are failures of **integration**: nine files researched nine areas in parallel, four of them assumed mutually exclusive deployment models, and nothing reconciled them. Two of the application's fifteen inventoried features got no research at all. And the single dependency the whole architecture is mandated to sit on was never actually read.

Read this as a list of what must be resolved *before* anyone writes code, not as a critique of the individual files.

---

## 1. Capability areas with NO research coverage

These are inventory features (or requirements the research itself created) that no file covers.

### 1.1 Chat History Management — inventory feature #4, complexity M — effectively uncovered

`ChatHistory` appears in four files. In every one it is an example of something else: a type to add to a `JsonSerializerContext`, a service to register in DI, a directory to place under `LocalApplicationData`, a thing a plugin must not be given. Nothing covers:

- The **on-disk schema** and its **version field**. `packaging-observability.md` says persist it via source-generated STJ; nobody asks what happens on the next release when `TokenObservation` gains a field.
- **Context-window budgeting and truncation.** The single most common failure mode in any chat shell — the conversation exceeds `n_ctx` or the model's max input — is discussed exactly once, in `plugin-tooling-mcp.md`, and only as a caution about tool output ("an unbounded token dump will blow the context window"). No file specifies a truncation policy, a token-budget accounting model, or what the user sees when it happens. `llm-abstraction.md` actively *forbids* the one mechanism it mentions: "Chat reduction (`SummarizingChatReducer`) ... rewrites history — poison for a tool whose output must be reproducible." Correct, and it leaves a hole.
- **`/inject`, `/pop`, `/clear`, `/import`, `/export` semantics.** These are five of the inventory's commands. They appear only in `plugin-tooling-mcp.md`'s "should the model be allowed to call this" table.
- **History size caps, retention, and per-turn persistence cost.** `local-inference.md` mandates "persist conversation history to disk on every turn, before inference, not after" as native-crash mitigation. Nobody costs that against a history file that has accumulated full token-introspection records — which `token-introspection.md` warns can be `4 × positions × |V|` bytes if handled naively.

### 1.2 System Prompt Management — inventory feature #7, complexity M — effectively uncovered

Every mention is incidental. The complete substantive treatment across 4,860 lines is one table row in `llm-abstraction.md`: "`ChatOptions.Instructions` for per-request system text." Not covered:

- **Per-provider system-message mapping.** Azure/OpenAI take a system/developer role message; Bedrock `Converse` takes a *separate top-level `system` block*; Anthropic requires it out-of-band; a local GGUF requires it rendered into the model's chat template. `llm-abstraction.md` documents `Converse`'s request shape as "`messages` / `system` / `inferenceConfig` / `toolConfig`" and then never returns to what that means for a named-prompt feature.
- Named-prompt **storage format, variable substitution, import/export, versioning** — the actual content of `SystemPromptService`, `PromptCommand` and `SystemPromptsDialog` (697 lines in the source).
- The interaction with the app's own headline feature: a *system prompt* is prompt tokens, and prompt-token surprisal/perplexity is one of the four things `token-introspection.md` says only the local backend can do. "Is my system prompt line-noise to this model" is arguably the killer feature and nobody joined those two dots.

### 1.3 Chat-template rendering on the local path — a gap the research *created*

`local-inference.md` recommends abandoning the high-level executors for `BatchedExecutor` + `Conversation.Sample()`. Its own tier table says the high level is where "chat templating" lives. Dropping to the low level therefore means **you now own rendering `ChatHistory` → ChatML/Llama-3/Gemma/Qwen prompt text**, per model architecture, matching what the GGUF expects. No file addresses this. `llama_chat_apply_template` is never mentioned. A grep for "chat template" across all nine files returns three hits, all about *other* runtimes (ORT GenAI, llama-server's `/apply-template`, Ollama's `Raw = true`).

This is not a detail. Get it wrong and every token index in the introspection grid is off, silently.

### 1.4 Model asset management

`local-inference.md` covers loading a GGUF exhaustively. Nothing covers *getting* one: discovery/listing of local models, `/model` enumeration for the local backend, disk-space checks before load, checksums, where models live on disk, unload/swap semantics (beyond the ALC-pinning warning), or what "the user picked a .gguf" means as a UX. `testing-quality.md` solves this for *CI* (HuggingFace download + `actions/cache`) and explicitly does not generalise it.

### 1.5 Post-v1 lifecycle: update, version check, data migration

Zero hits across all nine files for `self-update`, `auto-update`, `update check`, `schema version`. `packaging-observability.md` enumerates release assets and a `dotnet tool` channel, which is *distribution*, not *lifecycle*. `secrets-config.md` designs an excellent one-shot migration for legacy plaintext credentials — and nothing for `settings.json`, `history/`, or the named-prompt store. A self-contained binary that users download from a GitHub Release has no update mechanism at all in this design.

### 1.6 Multi-instance and concurrent access

Zero hits for `multi-instance`. One hit for `file lock` (about Windows DLL locking). `secrets-config.md` notes MSAL `Storage` is "cross-process safe but not thread safe" and moves on. Nobody asks the obvious question for a debugging shell: **two terminals open at once**, both with `reloadOnChange: true` on `settings.json`, both appending to history, both holding a multi-gigabyte GGUF. The recommended `IOptionsMonitor` + `FileSystemWatcher` design makes this *worse*, not better.

### 1.7 Consolidated licensing position

Licence facts are scattered and individually correct: subject repo GPL-3.0; `Xcaciv.Command`/`Xcaciv.Loader` **AGPL-3.0**; `Xcaciv.Cupcake` BSD-3-Clause; `PrettyPrompt` MPL-2.0; `Microsoft.Testing.Extensions.CodeCoverage` proprietary; `FluentAssertions` 8.x commercial; Verify fee-bearing after 2026-09-01; MCP SDK Apache-2.0-with-residual-MIT. `plugin-tooling-mcp.md` gets closest — "GPLv3 §13 permits the combination, but the AGPL portions carry AGPL obligations into the combined work ... get this reviewed" — and then correctly declines to give legal advice. **Nobody produced the combined answer for a redistributed self-contained binary.** That answer gates whether this can be shipped at all.

### 1.8 A single threat model

Three files do security work — plugin trust (`plugin-tooling-mcp.md`), secret storage (`secrets-config.md`), telemetry/content capture (`packaging-observability.md`) — each against a *different* implied adversary, with no shared trust-boundary statement. The result is inconsistent: `secrets-config.md` says "Do not expose the resolved secret to tool code. Tools should receive a configured client object"; `plugin-tooling-mcp.md` says a loaded plugin "can read the Azure OpenAI key you just decrypted out of DPAPI/libsecret" and no abstraction prevents it. Both are right. Together they mean the Tier-1 plugin model and the secret-handling model are incompatible, and no file notices.

### 1.9 Non-functional requirements, effort, and sequencing

There is no latency budget, no memory budget, no binary-size requirement, no concurrency target, no team size, no estimate, no critical path, and no sequencing anywhere in 4,860 lines. `testing-quality.md` proposes 90%/80%/50% coverage gates and `packaging-observability.md` proposes a six-RID release matrix, both without reference to any stated requirement. Every performance recommendation is untethered from a target.

### 1.10 Smaller, but real

- **macOS.** Consistently "out of stated scope, but if it is ever added…" — while `secrets-config.md` designs a Keychain leg, `local-inference.md` verifies Metal ships in `Backend.Cpu`, `terminal-ui.md` and `packaging-observability.md` both cost signing/notarisation, and `packaging-observability.md` notes non-AOT macOS apps need `com.apple.security.cs.allow-jit`. Substantial work exists for a platform nobody decided to support.
- **Error model and exit codes** for the non-interactive/scriptable path. `host-di-cli.md` says System.CommandLine gives you "correct exit codes"; nobody defines them. `testing-quality.md` notes MTP exits 8 on zero tests. That's the entire treatment.
- **Localisation of the UI itself.** Zero hits for `i18n`/`localiz`. `SatelliteResourceLanguages=en` is recommended twice, which is a decision to be English-only, made as a size optimisation.

---

## 2. Load-bearing claims asserted with no cited source

I'm listing only claims that *drive a recommendation*, not incidental colour.

### 2.1 "The app ships as an AOT, trimmed, self-contained binary" — `testing-quality.md`

The single most consequential unsourced claim in the package. It appears three times, always as settled premise:

> "because MTP is now the only platform that can run Native AOT and trimmed test hosts (**the exact deployment mode this app ships in**)" — bottom line, L9
> "since this application ships as an AOT/trimmed self-contained binary, every mock you write is a test you can never run in the mode your product actually ships in" — L146
> "The app ships as an AOT, trimmed, self-contained binary for Windows and Linux. MTP is the only platform that can execute tests in that mode at all" — L516

No citation. And `packaging-observability.md` disproves it from the actual repo:

> "the checked-in GitHub Actions workflow (`.github/workflows/build-release.yml`) installs `dotnet-version: '9.0.x'` and **only ever publishes the `SingleFile` configuration**, never `Compact`. The only published artifact in the tree, `test-publish/Xcaciv.ChatDbg.Shell`, is a **15,677,171-byte stripped linux-x64 ELF** — a SingleFile build. The AOT configuration existed on paper; the shipping pipeline did not use it."

The premise is false *today*, and three other files say it must remain false. Yet it is the primary justification for the entire test-platform recommendation (xUnit v3 4.0 on MTP v2 — a **two-week-old major version**, which the file itself flags as risk #1) and for banning Moq/NSubstitute from anything the team owns.

### 2.2 Every effort estimate in the package is an unsourced line count

There is no other form of estimate anywhere. Collected:

| Claim | File |
|---|---|
| "~200 lines for spawn, port selection, `/health` polling, stderr capture, graceful shutdown and orphan reaping" | `local-inference.md` |
| "hand-rolled `AssemblyLoadContext` + `AssemblyDependencyResolver` (~80 lines)" | `plugin-tooling-mcp.md` |
| "you own ~200 lines of segment-walking code" / "Both are ~150 lines" (IIoContext adapters) / "the 20-line `HeatMap : View`" | `terminal-ui.md` |
| "a rolling NDJSON file `ILoggerProvider`, ~150 dependency-free lines" | `packaging-observability.md` |
| "roll the two P/Invokes yourself, it is ~200 lines" | `secrets-config.md` |
| "About 40 lines" (snapshot harness) / "It is ten lines" (entropy scan) | `testing-quality.md`, `secrets-config.md` |

`secrets-config.md` is the only file that pushes back on its own estimate — "Budget it as real work, not a weekend" — and it does so about the *alternative* it rejects. Nothing aggregates these. Summed, the package asks for roughly seven hand-built subsystems, and there is no total anywhere.

### 2.3 Three mutually incompatible, unsourced binary-size figures

- `packaging-observability.md`: "One file, **~40–70 MB** pre-compression for this dependency set"
- `local-inference.md`: "A realistic self-contained `win-x64` CPU+Vulkan publish is on the order of **70–90 MiB** before compression; adding CUDA takes it past 280 MiB" — self-flagged: "**UNCONFIRMED**: exact published-folder sizes — I did not run a publish."
- `plugin-tooling-mcp.md`: "Bigger (**~70–90 MB** before compression)" — no source, no flag.
- `terminal-ui.md`: an actual measurement, "a working **21.8 MB** native binary ... trimmed self-contained with `TrimMode=full` was **28 MB**" — but that's AOT, without LLamaSharp, on linux-x64.

The only hard datum anyone found is `packaging-observability.md`'s: the checked-in `test-publish` ELF at 15,677,171 bytes. None of the estimates reconcile with it or with each other.

### 2.4 Latency assertions that justify the resilience design — `host-di-cli.md`

> "the standard handler's 30 s total / 10 s per-attempt timeouts will guillotine a long generation"
> "Time-to-first-token on a large hosted model, or a cold Bedrock endpoint, **can exceed 10 s**."
> "Raise to something generous (5–10 min)"

The *defaults* are sourced (Microsoft Learn). The claims that real workloads exceed them — which is the entire argument for rejecting `AddStandardResilienceHandler()` and hand-building a pipeline — are asserted. No measurement, no vendor SLA, no citation. The conclusion is probably right; it is not evidenced, and it is doing a lot of work.

### 2.5 The invented 60% decision rule — `llm-abstraction.md`

> "The signal to switch: if fewer than ~60% of your calls go through the abstraction without a downcast, the abstraction is not paying for itself."

A specific numeric threshold, presented as an architectural tripwire for abandoning `IChatClient` entirely, with no derivation and no source.

### 2.6 The AOT-is-impossible conclusion rested on an unread dependency — `packaging-observability.md`

The file's headline finding is that Native AOT is "categorically unavailable" *because* `Xcaciv.Loader` does runtime assembly loading. Its own Unconfirmed section then says:

> "4. **Whether `Xcaciv.Loader` uses `AssemblyLoadContext` at all.** Not on nuget.org (`totalHits: 0`), no GitHub result surfaced. §2's conclusion is conditional on it doing runtime assembly loading. **This is the single most important thing to confirm before acting on this document.**"

The conclusion happens to be correct — `plugin-tooling-mcp.md` read the source and quotes `loadContext = new AssemblyLoadContext(fullName, isCollectible);` — but the two files never reconcile, so the package's most consequential decision is documented as resting on an unverified premise.

### 2.7 The market-differentiation claim — `token-introspection.md`

> "What you *can* deliver ... is genuinely differentiated and, **as far as I can find**, not offered by any existing .NET tool."

No competitive survey is cited, and no search is described. This is the claim that justifies building the product at all.

### 2.8 Coverage gates — `testing-quality.md`

90% / 80% / 50% per-assembly thresholds, and a Stryker `break: 60`, presented as blocking CI gates. No basis given; no reference to any quality requirement.

---

## 3. Where the files contradict each other

### 3.1 Native AOT: mandatory, or impossible? — the package's central unresolved conflict

**Against:**

> `packaging-observability.md` L11 — "Native AOT is **categorically unavailable** to this rebuild ... trimming fails for the same reason and must be off, so the source project's `Compact` configuration cannot be carried forward."
> `packaging-observability.md` L431 — "| **`PublishAot`** | **`false`** | **Mandatory.** 'No dynamic loading.' Delete the `Compact` configuration. |"
> `plugin-tooling-mcp.md` L13 — "The expensive consequence you must accept up front: **a plugin-loading ChatDbg cannot be a Native-AOT binary.**"
> `local-inference.md` L353 — "**Conclusion: do not plan on Native AOT for the shell process while LLamaSharp is in-process.**"

**For:**

> `testing-quality.md` L516 — "The app ships as an AOT, trimmed, self-contained binary for Windows and Linux. MTP is the only platform that can execute tests in that mode at all; VSTest cannot."
> `testing-quality.md` L413 — recommends in the root `Directory.Build.props`: "`<IsAotCompatible>true</IsAotCompatible>`" together with "`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`"
> `host-di-cli.md` L344 — "it is disqualified here by two facts: ... and this application *already ships* a `Compact` configuration with `PublishAot=true`, `PublishTrimmed=true`, `TrimMode=full`"
> `host-di-cli.md` L662 — "| Publishing a self-contained binary for Windows and Linux | System.CommandLine (AOT-supported since beta4), and **not** Spectre.Console.Cli | The existing `Compact` config is `PublishAot=true; TrimMode=full`. |"

This is not a stylistic disagreement. It decides:
- the **CLI parser** (`host-di-cli.md` rejects Spectre.Console.Cli *solely* on AOT grounds; remove the premise and that decision is unmade),
- the **test platform and mocking policy** (`testing-quality.md`'s entire chapter 2),
- the **secret store** (`secrets-config.md`: "**It wins when** ... NativeAOT or hard binary-size limits are a requirement" — the second-best hand-rolled option becomes primary),
- the **serialisation strategy**, and
- whether `IsAotCompatible=true` + `TreatWarningsAsErrors=true` produces a build that cannot compile, since `Xcaciv.Loader` is unannotated and does runtime type loading by design.

### 3.2 `IncludeAllContentForSelfExtract` — "correct" vs "remove entirely"

> `local-inference.md` L115 — "Because these are `Content`, not NuGet native assets, `PublishSingleFile` needs **`IncludeAllContentForSelfExtract=true`** ... The existing `SingleFile` configuration sets both, which is correct."
> `local-inference.md` L465 — "`PublishSingleFile` + `SelfContained` + `IncludeAllContentForSelfExtract` + explicit `-r` | **Present in the `SingleFile` configuration and correct.**"

versus

> `packaging-observability.md` L89 — "⚠️ Docs: *'This mode is not recommended: it's a .NET Core 3.1 compatibility mode and might be removed in a future release.'* **The source project sets this to `true` in its `SingleFile` configuration — do not carry it forward.**"
> `packaging-observability.md` L434 — "| **`IncludeAllContentForSelfExtract`** | **`false` — remove entirely** |" and L433 "| `IncludeNativeLibrariesForSelfExtract` | `false` | Keep the llama.cpp `runtimes/` tree loose on disk so LLamaSharp's device probing works |"

And a third position:

> `terminal-ui.md` L370 — "either use `PublishSingleFile` with `IncludeNativeLibrariesForSelfExtract=true` (the source's SingleFile config, which self-extracts at run time) or drop Terminal.Gui from the shipped binary."

Three files, three different answers to "how do native assets reach the disk", each with a real consequence: `local-inference.md`'s bundles everything into one file; `packaging-observability.md`'s deliberately ships a *directory* (so "self-contained binary for Windows and Linux" becomes "a zip"); `terminal-ui.md`'s is the middle option that `packaging-observability.md` explicitly rejects.

### 3.3 OllamaSharp logprobs — three incompatible statements

> `llm-abstraction.md` L35 — "| `OllamaSharp` | **5.4.30** | ... | **Yes**, natively typed | Implements `IChatClient` explicitly on `OllamaApiClient`. Has typed `Logprobs`/`TopLogprobs`/`Logprob`. |"
> `llm-abstraction.md` Unconfirmed #7 — "[ollama/ollama#16117] ... is **closed as not planned**, which contradicts the secondary sources. I could not reconcile these against primary Ollama documentation."

versus

> `local-inference.md` L26 — "| **OllamaSharp** | **5.4.30** | GA | 2026-07-24 | Does **not** model `logprobs`/`top_logprobs` — **verified against its `ChatRequest`/`ChatResponseStream` source.** Server has it; the client doesn't. |"

versus the file that actually resolves it:

> `token-introspection.md` L181 — "`OllamaSharp` ... implements `Logprobs`/`TopLogprobs` in `Models/Generate.cs` **only** — on `GenerateRequest`, `GenerateResponseStream` ... Grepping `Models/Chat/` for 'logprob' returns **nothing**."

`llm-abstraction.md` over-claims (it's true only on the generate path), `local-inference.md` over-denies (it's false for the generate path), and `llm-abstraction.md`'s Unconfirmed #7 is stale — both `local-inference.md` and `token-introspection.md` verify the feature landed in Ollama v0.12.11 by reading `api/types.go`.

### 3.4 Azure OpenAI's `top_logprobs` ceiling — 20 or 5?

> `llm-abstraction.md` L152 — "`TopLogProbabilityCount   = 20,   // OpenAI's documented max`"
> `llm-abstraction.md` L348 — "| Azure OpenAI | same type ... | **identical** | Same as above, subject to per-deployment parameter support |" (i.e. "up to 20 alternatives")
> `secrets-config.md` L306 — "`.Validate(o => o.LogProbabilitiesTopK is >= 1 and <= 20, \"logProbabilitiesTopK must be between 1 and 20\")`"
> `secrets-config.md` L408 — "`logProbabilitiesTopK` is capped at 20 by the OpenAI API — validate it, the source app does not"

versus

> `token-introspection.md` L82 — "**Azure OpenAI: 5.** The service returns `Invalid value for 'top_logprobs': must be less than or equal to 5.` ... This is runtime validation only ... you cannot discover the limit from the schema; you must discover it from a 400."

`secrets-config.md`'s recommended startup validation rule would accept a value Azure rejects at request time — exactly the failure mode it was written to prevent.

### 3.5 Bedrock: "no first-party model returns logprobs" vs "Cohere Command does"

> `llm-abstraction.md` L9 — "for Amazon Bedrock it means accepting that **no Bedrock API — Converse, ConverseStream or InvokeModel on any first-party foundation model — returns log probabilities at all**"

versus, in `llm-abstraction.md` itself twelve pages later, and in `token-introspection.md`:

> `llm-abstraction.md` L292 — "| **Cohere Command (legacy text)** | `"return_likelihoods": "GENERATION" / "ALL" / "NONE"` | `generations[].likelihood` ... `token_likelihoods` | `cohere.command-text-v14` and siblings |"
> `token-introspection.md` L106 — "| **Cohere Command (`cohere.command-text-v14`)** | **Yes, partially** | ... | **Yes (`ALL`)** | Yes |" — including *prompt* token likelihoods.

The bottom line, which is what a reader acts on, contradicts the body. `cohere.command-text-v14` is a first-party foundation model reachable via `InvokeModel`. The consequence is real: `llm-abstraction.md` prescribes `SupportsTokenTelemetry = false` for Bedrock unconditionally, which would disable a capability that exists.

### 3.6 Should the local backend go through `IChatClient`?

> `llm-abstraction.md` L9 — "**Code the application's conversation, history, settings and tool-hosting layer against `Microsoft.Extensions.AI.Abstractions` 10.9.0 `IChatClient`**"
> `packaging-observability.md` L371 — "**Prefer routing everything through `IChatClient` and pushing token-introspection data into `AdditionalProperties`, so one middleware covers all three backends.**"

versus

> `token-introspection.md` L9 — "**Do not route this feature through `Microsoft.Extensions.AI`** ... define your own `ITokenIntrospectingChatBackend` and keep MEAI, if you want it, for the boring chat path."
> `local-inference.md` L466 — "| Uniform provider abstraction across Azure OpenAI / Bedrock / local | An interface that carries logprobs | **Not `Microsoft.Extensions.AI`** ... Define your own `ITokenIntrospectingChatProvider` ... **Use `IChatClient` only for the plain-text path.**"

`packaging-observability.md`'s entire observability plan — free `gen_ai.*` spans, the four histograms, cross-provider cost comparison — depends on all three backends flowing through `UseOpenTelemetry()` in the `IChatClient` pipeline. It even names the risk and then recommends against its own hedge: "the local llama.cpp path is a genuine question mark, since ... a raw `LLamaSharpService` doing token-level probability work may bypass the `IChatClient` pipeline entirely." The other two files say it *will* bypass it. So the telemetry design has a hole for the backend that matters most, and the file that owns telemetry doesn't know it.

### 3.7 Three incompatible shapes for the same "capability" record

Not a factual contradiction, but a design one that will produce three types in one codebase:

- `llm-abstraction.md`: `ITokenTelemetryExtractor { bool SupportsTokenTelemetry; string CapabilityNote; void ConfigureRequest(...); IReadOnlyList<TokenStep>? Extract(object?); }`
- `testing-quality.md`: `BackendCapabilities` on an `ILlmBackend` port — "supports logprobs? top-K max? vocab exposed?"
- `token-introspection.md`: `IntrospectionCapabilities(bool OutputLogProbs, int MaxTopK, bool StreamingLogProbs, bool PromptLogProbs, bool FullVocabulary, bool TeacherForcedScoring, int? VocabularySize, string ProbeStatus)`

Only the third models prompt-logprobs and teacher-forced scoring, which are the two capabilities that gate the attribution feature. `testing-quality.md` builds its whole `LlmBackendContract` suite on *its* shape.

### 3.8 Bedrock `HttpClient` injection — the resilience design may not be implementable

> `host-di-cli.md` L605 — "`builder.Services.AddHttpClient(ProviderKeys.Bedrock, ConfigureBedrock).AsLlmClient();`" — i.e. Bedrock gets a keyed `HttpClient` with a Polly resilience handler attached
> `host-di-cli.md` Unconfirmed — "**AWS SDK v4's exact `HttpClient` injection API.** ... The property name for supplying a custom `HttpClient` factory on `AmazonBedrockRuntimeConfig` is **UNCONFIRMED**"

versus

> `testing-quality.md` L210 — "**There is no `HttpClientFactory` property on `ClientConfig` in the v4 API.** V4 removed `DefaultClientConfig.HttpClientFactory`; the guidance is *'Use `AWSConfigs.HttpClientFactory` instead'* — a **process-global**."

If `testing-quality.md` is right, `host-di-cli.md`'s per-provider keyed-client architecture cannot be expressed for Bedrock, and the "one place to attach the resilience handler, and one place to redact secrets" argument loses one of its three providers.

---

## 4. Decisions listed but never made

Each of these is a place where options are laid out, conditions are named, and no choice is recorded.

1. **The publish mode.** See §3.1. `plugin-tooling-mcp.md` is explicit that it is punting: "Options, in preference order: 1. Single SKU ... 2. Two SKUs ... 3. AOT core + MCP-only extensibility" and then "| Existing `Compact` config uses `PublishAot=true` | **Pick one of the three deployment options above *before* writing the loader, not after** |". Nobody picked. And option 3 is described as "genuinely the better architecture if the product can accept it" — which is a product decision no file is authorised to make.

2. **In-process LLamaSharp vs supervised `llama-server` subprocess.** `local-inference.md` names a primary and then defers the real decision to a future observation: "invert the ladder ... That wins when (a) **the native crash log in the first month of dogfooding is worse than expected**, (b) the app needs a llama.cpp newer than LLamaSharp's binding often enough to matter, or (c) you want one shared model instance across multiple shell windows." Two of those three are knowable now.

3. **How the Xcaciv packages get into a build.** Named as a risk in **all nine files**. The options — publish to nuget.org, GitHub Packages with a PAT, git submodule + `ProjectReference`, vendor, fork — appear across `plugin-tooling-mcp.md` ("Fix before the rebuild: publish to nuget.org, or vendor as submodules/`ProjectReference`") and `terminal-ui.md` ("Consumption is via submodule, project reference, or a private feed"). No file chooses, and each one scopes it out as somebody else's problem: "*a build/release risk to plan for, though outside this area's scope*" (`terminal-ui.md`).

4. **Whether Terminal.Gui ships at all.** `terminal-ui.md` recommends the hybrid, then immediately undercuts it twice: "The alternative (Spectre `SelectionPrompt` + `TextPrompt`) is fine and simpler **if you want to drop Terminal.Gui entirely**" and "**If the product vision is 'an IDE in the terminal', this recommendation is wrong** and you should go Terminal.Gui-primary." The v1→v2 migration is costed as a rewrite (`LogProbHeatmapView` "uses **seven** removed APIs — It is a rewrite, not a port"; `SettingsDialog.cs` 608 lines and `SystemPromptsDialog.cs` 697 lines "hit hardest"). Nobody decides whether that rewrite happens.

5. **Whether Bedrock remains a first-class backend.** `llm-abstraction.md`: "Bedrock will always be the poor cousin, and the honest thing is to show that in the UI rather than paper over it." `token-introspection.md`: Bedrock scores ❌ on every column except one legacy model. No file asks whether a backend that cannot do the product's headline feature should be in v1, or be demoted to a text-only chat backend, or be cut. That is the highest-leverage scope question in the package and it is never posed.

6. **The Verify licence position.** "**Three viable positions, pick one deliberately**: 1. Pin at 32.0.0 ... 2. Sponsor ... 3. Hand-roll." Softly leans ("This is the lowest-friction choice and what I'd do first") and does not decide — while noting the deadline is "**four days from now**" relative to the research date.

7. **Logging sink.** `packaging-observability.md`: "Two ways to close that: 1. Write a small custom `ILoggerProvider` ... **Preferred** ... 2. Add Serilog + `Serilog.Sinks.File`". Then re-opens it: "**Serilog wins when** you want rolling-file policy, retention and a rich sink set without writing them" — which is exactly the stated requirement (`/exportlogs`).

8. **Secret store.** `secrets-config.md` picks MSAL `Storage`, then lists three conditions under which the hand-rolled alternative wins — the first being "**NativeAOT or hard binary-size limits are a requirement**", which is unresolved (§3.1), and the third being "a security review objects to an auth library appearing in the dependency graph of a chat tool", which is likely. It also flags the recommendation's own user-visible regression ("**No entry in the Windows Credential Manager UI** ... the most user-visible regression in the recommendation") without deciding whether that is acceptable.

9. **`EnableCompressionInSingleFile`.** "`false` for the default asset; `true` for an optional `-slim` asset | Compression trades launch latency for download size; **measure before choosing**." No measurement was taken; `plugin-tooling-mcp.md` meanwhile says flatly "Use `EnableCompressionInSingleFile`."

10. **macOS support.** Consistently deferred across five files while incurring design cost in three.

11. **Startup-args parser.** `host-di-cli.md` picks System.CommandLine 2.0.11, but the two disqualifying facts against Spectre.Console.Cli are (a) trimming/AOT and (b) "this application *already ships* a `Compact` configuration with `PublishAot=true`". Both premises are contested (§3.1). Strip them and the decision reverts to open — and the file's own third option, ConsoleAppFramework, was last released 2025-11-26.

---

## 5. What a senior .NET architect would ask that these notes cannot answer

1. **"Show me `Xcaciv.Cupcake.Core.Loop`'s actual API — I'm being told to pattern the app on it."** Cannot be answered. `host-di-cli.md`: "GitHub's API rate-limited me before I could read `src/Xcaciv.Cupcake.Core/`. I know `Loop` has a `Controller` property and a `RunWithDefaults()` method from the call site ... the rest is **UNCONFIRMED**." `terminal-ui.md`: "did not enumerate Cupcake's shell loop ... the contents API rate-limited before I could walk the tree."

2. **"Does `ICommandDelegate.Main` take a `CancellationToken` in 3.3.x, and if not, which of your three workarounds is the sanctioned one?"** `host-di-cli.md` names it as "the largest impedance mismatch in the whole design" and offers (a) smuggle through `IEnvironmentContext`, (b) `AsyncLocal<CancellationToken>`, (c) upstream a change — with no verification of which is possible. The attribute signatures are separately flagged UNCONFIRMED.

3. **"What's the size and cold-start budget, and does the design meet it?"** Unanswerable: no requirement is stated and the four size figures span 15 MB to 280+ MB (§2.3).

4. **"What ships? Give me the exact release matrix."** `packaging-observability.md` lists eight assets across four RIDs plus a CUDA variant, a nupkg and a container. `local-inference.md` implies CPU/Vulkan/CUDA×2 platforms as separate builds. `terminal-ui.md` adds a native `libonigwrap.so` that breaks "one file". No file states the shipping matrix as a decision, and `NETSDK1152` (multi-backend duplicate native filenames, upstream issue closed **not planned**) sits unresolved underneath it.

5. **"Which backend is the reference for acceptance criteria?"** Unanswerable. A test asserting "top-K alternatives are ordered by descending logprob" passes locally (K=|V|), passes on OpenAI gpt-4o (K≤20), fails validation on Azure at K>5, is meaningless on Bedrock, and returns HTTP 500 on gpt-5.2+ at K≥2. Three different capability record shapes exist (§3.7).

6. **"Who renders the chat template on the local path?"** Unanswerable — §1.3.

7. **"What happens when the conversation exceeds the context window?"** Unanswerable — §1.1.

8. **"Two terminals, same profile. What breaks?"** Unanswerable — §1.6.

9. **"What can I legally ship?"** Unanswerable — §1.7. GPL-3.0 + AGPL-3.0 + MPL-2.0 in one redistributed self-contained binary, plus fee-bearing and proprietary build-time tooling.

10. **"How long, how many people, in what order?"** Unanswerable — §1.9, §2.2. There is no estimate, no sequencing, no critical path, and roughly seven hand-built subsystems whose only sizing is a line-count guess.

11. **"Who owns the AOT decision and by when?"** Unanswerable. There is no ADR, no decision log, no owner, and four files that assume different answers.

12. **"What does CI cost per PR?"** Unanswerable. The proposed gate is: R2R publish × 4–6 RIDs, model download + cache, xUnit v3 on MTP, coverlet, snapshot tests, WireMock contract suites, weekly Stryker, scheduled BenchmarkDotNet on a dedicated runner, nightly live-provider calls against three paid backends. No wall-clock or dollar figure anywhere.

13. **"Is `Microsoft.ML.Tokenizers` in the dependency set or not?"** `token-introspection.md` makes it a headline pick (for `EncodedToken.Offset`, "the whole ballgame for attribution"). It appears in **no other file** — not in `packaging-observability.md`'s dependency list, not in `host-di-cli.md`'s wiring, not in `testing-quality.md`'s test matrix. Likewise `Tokenizers.DotNet`'s Rust native asset, which would collide directly with the single-file goal.

14. **"What is the threat model?"** Unanswerable — §1.8, and the two security recommendations that do exist are mutually incompatible.

---

## 6. The single biggest underplayed risk

**The entire architecture is mandated onto three unpublished, AGPL-3.0, single-maintainer repositories whose runtime behaviour nobody could actually read — and every one of the nine files names this, then explicitly hands it to another file.**

What is well documented:

> "I queried nuget.org for `Xcaciv.Command`, `Xcaciv.Command.Core`, `Xcaciv.Loader`, `Xcaciv.Cupcake` and `Xcaciv.Configuration`: **all return 'not found.'**" — `host-di-cli.md`
> "`https://nuget.pkg.github.com/xcaciv/index.json` returns **401** anonymously ... **Restore is not reproducible for anyone outside the author's machine or without a GitHub PAT.**" — `plugin-tooling-mcp.md`
> "`Xcaciv.Loader` and `Xcaciv.Command` are AGPL-3.0; ChatDbg's `LICENSE` is GPL-3.0 ... the AGPL portions carry AGPL obligations into the combined work." — `plugin-tooling-mcp.md`

What is underplayed is the **blast radius**, because it was never assembled in one place:

- **The pattern to copy was never read.** Cupcake's `Loop` API is UNCONFIRMED in two files (GitHub rate limits). What *is* known is bad: it targets **`net8.0`** while the app targets `net10.0`; its real entry point calls `Environment.Exit(1)` in the catch — which `host-di-cli.md` shows skips `finally` blocks and loses in-flight history/settings writes; its `Program.cs` is still the "Hello, World!" template; and it pins `Xcaciv.Command` **2.1.1** against a current **3.3.x** that removed `EnableDefaultCommands()`/`GetHelp()` and changed `HandlePipedChunk` to take `IResult<string>`. `plugin-tooling-mcp.md`: "**Cupcake as it stands will not compile against current Xcaciv.Command.**" So the brief's "patterned on Xcaciv.Cupcake" names a pattern that does not currently build and that nobody has fully seen.

- **A known live defect in code you don't own is on the critical path.** `plugin-tooling-mcp.md` found, in current `Xcaciv.Command` `main`, `CommandFactory.CreateCommand` doing `using var context = new AssemblyContext(...); return context.CreateInstance<ICommandDelegate>(fullTypeName);` — disposing the collectible ALC while handing out an instance whose type lives in it. "**a new ALC is created for every single command invocation** ... In an interactive chat/debug REPL that a user leaves open for hours ... you accumulate one `LoaderAllocator` per invocation." Ranked "Highest-value single fix" — in a repository the rebuild team does not control.

- **It is load-bearing in five separate designs simultaneously.** `IIoContext` is `terminal-ui.md`'s entire swappable-UI seam; `ICommandDelegate`'s `IAsyncEnumerable<IResult<string>>` + `ChannelReader` pipe is `host-di-cli.md`'s streaming contract; `AddXcacivCommand(IConfiguration)` is its DI entry point; `ResultFormat` is `terminal-ui.md`'s `--format json` mode; `ICommandDescription` → JSON Schema is `plugin-tooling-mcp.md`'s tool-calling adapter; `IAuditLogger` is its audit trail; `Xcaciv.Loader`'s `AssemblyContext` is the plugin tier. If the API differs from what was inferred, or if the feed cannot be obtained, **five chapters need rewriting, not one.**

- **`secrets-config.md` says so outright and nobody escalated:** "This is the **single largest gap in this analysis** — the DI and configuration recommendations above assume a stock `Microsoft.Extensions.Hosting` generic host, which may need adjusting."

- **The licence question has no owner.** GPL-3.0 host + AGPL-3.0 libraries + MPL-2.0 `PrettyPrompt`, redistributed as a self-contained binary. Four files touch it; none concludes.

The diffusion is the mechanism of the underplay: nine files each said "outside this area's scope" about the one dependency that is inside *every* area's scope.

**Runner-up, and it is close.** The product's differentiating features are **local-only**, and no file says so as a product finding. `token-introspection.md`'s own matrix: full-vocabulary probability maps — LLamaSharp only. Prompt logprobs / attribution — LLamaSharp, ORT GenAI, Bedrock Custom Model Import, legacy Cohere. Exact entropy — local only. Meanwhile Azure caps `top_logprobs` at 5, Azure documents logprobs as **unsupported on the entire GPT-5/o-series**, OpenAI 500s at K≥2 on gpt-5.2/5.3-codex/5.4, and Bedrock's Converse path returns nothing at all. So **two of the three named backends cannot do the thing the application exists to do**, and one of them is being actively withdrawn upstream. `token-introspection.md` calls this "the headline risk" *within its own area* and correctly proposes graceful degradation. What nobody does is take the next step and ask the scoping question: is this a three-provider chat client with an introspection feature, or a local-model introspection tool with two chat backends attached? Those are different products, with different test matrices, different UIs, different release plans — and the answer determines whether roughly a third of this research package is even in scope.
