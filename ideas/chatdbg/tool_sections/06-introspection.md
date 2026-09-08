## 6. ChatDbg.Tools.TokenIntrospection — Token Introspection

> **Root command:** `TOKEN` · **Assembly:** `ChatDbg.Tools.TokenIntrospection.dll` · **Contract:** `Xcaciv.Command.Interface` + `Xcaciv.Command.Core` **3.3.4** (`net10.0`)
>
> This package is the rebuilt product's differentiator. It carries features **7.9 Token Probability Analysis** and **7.10 Token Inspection** in full, plus the derived-statistics layer the source never had.

---

### 6.1 Purpose and boundary

**What this package owns.**

| Owned concern | Source ancestry |
|---|---|
| The **capture policy** — whether per-token confidence is requested at all, and with how many top-K alternatives | `enableLogProbabilities`, `logProbabilitiesTopK` (BR-01, BR-02) |
| **Tokenization** of arbitrary text into an ordered token sequence with vocabulary IDs and character spans | `/tokenize`, `TokenInspectionService.AnalyzePrompt` |
| The **probability map** — the distribution the model weighed at each generation step, chosen token plus top-K runners-up | `/inspect` stage D, `TokenInspectionService.GenerateProbabilityMap` |
| **Attribution** — which span of prompt or prior output influenced a generated token | `/inspect` stage E, `BuildAttributionMap` |
| **Derived statistics** — entropy, perplexity, negative log-likelihood, margin, confidence bands, low-confidence ratio | *no ancestor — the source computed none* |
| **Selection/sampling** — which tokens are presented when the set is large (5-per-segment beginning/middle/end rule) | BR-10, BR-11, BR-12 |
| The **canonical analysis document** — its schema, its identity, its serialization, its round-trip | `TokenAnalysis` / `TokenLogProbability` / `TokenProbabilityMapResult` |
| **Viewing and exporting** an analysis: projection to text, JSON, CSV; writing to and reading from disk | `show-analysis` (stub), `export-analysis` (stub), `LLamaSharpService.SaveAnalysesToJson` |
| The **offline demonstration** dataset | `/demologprobs` |

**What this package explicitly does NOT own.**

| Not owned | Owner | Why the line is here |
|---|---|---|
| Calling a model, holding a session, or parsing a provider's wire format | **`ChatDbg.Tools.Providers`** (root `AI`) — 7.6 / 7.7 / 7.8 | This package consumes an abstraction (`IProbabilitySource`) and never speaks HTTPS, never loads a native inference backend, and never sees a credential. That is what makes it restricted-host-safe (§6.2). |
| Loading GGUF weights, managing a native context, GPU offload | **`ChatDbg.Tools.Providers`** | The source's `TokenInspectionService` loaded the model itself — twice per `/inspect` (Q9) — bypassing the chat path's caching and its concurrency gates. The rebuild delegates. |
| Drawing anything: colour, grid geometry, heat maps, panels, themes, terminal-width probing | **`ChatDbg.Tools.Rendering`** (root `RENDER`) — 7.12 | This package decides *which* tokens and *what* the numbers are; rendering decides what they look like. Confidence **bands** (a named enum) are ours; the green/lime/yellow/orange/red palette is theirs. |
| Persisting settings to `~/.ChatDbg/settings.json` | **`ChatDbg.Tools.Settings`** (root `CONFIG`) — 7.2 | We write the process environment; durable persistence is one owner, one file, one writer. |
| Storing conversation turns, `/import`, `/export` of chat history | **`ChatDbg.Tools.History`** (root `CHAT`) — 7.4 | An analysis references a message by id; it does not contain the conversation. |
| Capturing the native library's log stream, `export-logs` | **`ChatDbg.Tools.Diagnostics`** (root `LOG`) — 7.11 | The source's `ExportLogsCommand` sat beside `ExportTokenAnalysisCommand` and shared a test class; they are separate concerns and separate packages here. |
| Credentials, endpoints, API keys of any kind | **`ChatDbg.Tools.Credentials`** — 7.3 | **No tool in this package accepts, reads, stores, emits or logs a secret.** See §6.2. |

**The one-sentence boundary:** *this package turns a model's token-level uncertainty into a document, and turns that document into numbers, filters, files and text — it never produces the uncertainty and never paints it.*

---

### 6.2 Package manifest

| Property | Value |
|---|---|
| **Assembly name** | `ChatDbg.Tools.TokenIntrospection` |
| **Root command** | `TOKEN` (uppercased by `NamesValidator.GetValidCommandName`; every tool carries `[CommandRoot("TOKEN", "Token-level introspection of model output")]`) |
| **Contract assembly version** | `Xcaciv.Command.Interface` **3.3.4** and `Xcaciv.Command.Core` **3.3.4** — the shipped csproj `<Version>`, not the `3.3.0`/`3.3.3`/`3.2.2` figures in the framework's own docs. Nothing else from the framework is referenced. **The plugin DLL must not ship a private copy of `Xcaciv.Command.Interface`** — `Crawler` catches `ReflectionTypeLoadException` and reports precisely that cause, and the package is skipped silently. |
| **Target framework** | `net10.0` (framework default). Build with `/p:UseNet08=true` only if the host is pinned to `net8.0`; the package has no `net10.0`-only API dependency. |
| **Elevated trust** | **No.** No tool is registered with `modifiesEnvironment: true`. Every key this package writes is prefixed `TOKEN_`, which `CommandController.Run` routes into the package's own private environment bucket without any elevation. |
| **Network** | **None.** No tool opens a socket. Provider traffic belongs to `ChatDbg.Tools.Providers`. |
| **Filesystem** | **Two tools only** — `TOKEN EXPORT` (write) and `TOKEN LOAD` (read), both confined to the directory named by `TOKEN_EXPORTDIR`. No tool accepts an absolute path on the command line (see §6.3.10 for why that is impossible, not merely discouraged). All other tools are filesystem-free. |
| **OS keystore** | **Never.** No credential surface at all. |
| **Native libraries** | **None.** The package is pure managed, trim-safe and AOT-safe: no `Reflection.Emit`, no `Expression.Compile`, no late binding. This is a deliberate correction of QUIRK-Q14, where the source's reflection-based logit accessor failed **silently** under the project's own trimmed/AOT publish configuration and reported 100 % confidence in everything. |
| **Safe to load in a restricted host** | **Yes.** It passes `AssemblySecurityPolicy.Strict` preflight (no dynamic-code constructs), needs no wildcard `basePathRestriction`, and is safe to run under `learningMode: false` integrity verification. Under a restricted host with no `IProbabilitySource` registered, `TOKEN SPLIT` / `MAP` / `INSPECT` fail with a stated reason; `TOKEN DEMO`, `LOAD`, `FILTER`, `STATS`, `SHOW`, `EXPORT`, `CAPTURE` and `DIAG` remain fully functional. |
| **Host services required** | A single shared contract assembly, `ChatDbg.Introspection.Abstractions`, loaded in the **default** load context and referenced by both host and package, exposing `ITokenizer`, `IProbabilitySource`, `IAttributionModel`, `IAnalysisStore` and a static `IntrospectionServices` registry the host populates at startup. This indirection is mandatory: `CommandFactory` activates plugin commands through `AssemblyContext.ActivateInstance<ICommandDelegate>`, which uses the **public parameterless constructor only** — there is no DI injection into an ALC-loaded plugin command. |
| **Audit sensitivity** | **Medium — content, not credentials.** `AuditEvent.Parameters` is `ioContext.Parameters` verbatim, and `AuditMaskingConfiguration.ApplyMasking` only rewrites `-name=value` tokens, which this framework's `-name value` syntax never produces. Therefore **any prompt text typed as a command-line argument is written to the audit log in the clear.** See §6.5 for the mitigation. |

**Environment keys.** All are read with the `TOKEN_` prefix applied by `ControllerEnvironmentContext.GetChild("TOKEN")`; all are declared unprefixed in `GetDefaultEnvironment()`.

| Key (as read) | Default | Range | Written by | Source ancestry |
|---|---|---|---|---|
| `TOKEN_CAPTURE` | `false` | `true`/`false` | `TOKEN CAPTURE` | `enableLogProbabilities`, default **false** (BR-01) |
| `TOKEN_TOPK` | `5` | **1–20 inclusive** | `TOKEN CAPTURE` | `logProbabilitiesTopK`, default **5** (BR-02) |
| `TOKEN_MODE` | `sample` | `sample`\|`all` | `TOKEN CAPTURE`, `TOKEN SHOW` | `showAllTokens`, default **false** ⇒ sampled (BR-03) |
| `TOKEN_LAYOUT` | `list` | `list`\|`grid` | `TOKEN CAPTURE`, `TOKEN SHOW` | `gridViewForTokens`, default **false** ⇒ list (BR-04) |
| `TOKEN_GRIDMAXALT` | `5` | **1–20 inclusive** | `TOKEN CAPTURE`, `TOKEN SHOW` | `gridViewMaxAlternatives`, default **5** (BR-05) |
| `TOKEN_SAMPLESIZE` | `5` | 1–100 | `TOKEN CAPTURE` | **NEW** — the source hard-coded 5 in four places (BR-10) |
| `TOKEN_MAXGEN` | `10` | 1–8192 | `TOKEN CAPTURE` | `min(10, maxTokens)` (M3, M5) |
| `TOKEN_CONTEXT` | `0` (`0` = use the provider's configured context) | 0 or 512–32768 | `TOKEN CAPTURE` | **NEW** — replaces the hard-coded 512/2048 (M1, M2, Q8) |
| `TOKEN_LOWCONF` | `0.30` | 0.0–1.0 | `TOKEN CAPTURE` | **NEW** — the source's lowest colour-band boundary (BR-22) reused as a threshold |
| `TOKEN_EXPORTDIR` | platform application-data path (§6.6) | absolute directory | host / `TOKEN CAPTURE` | **NEW** — the source accepted arbitrary paths on the command line |
| `TOKEN_LAST` | *(unset)* | analysis id | every analysis-producing tool | **NEW** — replaces the source's single mutable "last generation" list |
| `TOKEN_KEEP` | `8` | 1–64 | `TOKEN CAPTURE` | **NEW** — bound on the in-process analysis ring |

**Global (unprefixed) keys this package *reads only*,** owned by `ChatDbg.Tools.Settings` and written by a command registered `modifiesEnvironment: true`: `PROVIDER`, `MODELID`, `TEMPERATURE`, `MAXTOKENS`. This is the only cross-package configuration channel that exists — a command's private bucket is invisible to every other command, so anything two packages must agree on has to be global.

---

### 6.3 Tool catalog

Thirteen tools. Eight are ported; five are marked **NEW** and each states why it earns its place.

Conventions that hold for **every** tool in the package and are therefore stated once:

- **Parameter attributes go on the class**, never on members (`AttributeTargets.Class, AllowMultiple = true`). Parameter names normalise to **lowercase**; command names to **UPPERCASE**.
- **Almost nothing is declared `IsRequired`.** An `ArgumentException` raised inside `CommandParameters` is caught by `CommandExecutor` and reduced to `Error executing TOKEN (see trace for more info)` — the user never sees *which* parameter was wrong. Every tool therefore declares ordered parameters with `IsRequired = false` and validates in the body, returning `CommandResult<string>.Failure` with a message the user can act on. This is how the source's exact strings (`Top-K value must be a number between 1 and 20`) survive into the rebuild.
- **`AllowedValues` is declared only where a generic error is acceptable.** Where the source pins an exact message, the candidate list lives in `ValueDescription` and validation is in-body. Where `AllowedValues` *is* declared, the first entry is chosen to match the source's default, because `AllowedValues[0]` silently becomes `DefaultValue` when no default is set.
- **Zero arguments ⇒ zero parameter processing.** `AbstractCommand.ProcessParameters` returns an empty dictionary immediately when `io.Parameters.Length == 0`: no defaults applied, no flags materialised, no field injection. Every tool's bare form is therefore designed to be meaningful with no parameters at all (usually "report status" or "operate on the last analysis").
- **A failed upstream chunk never reaches `HandlePipedChunk`.** `AbstractCommand.Main` forwards failures verbatim and skips empty successes. So every tool in this package propagates downstream errors correctly without writing a line of code for it, and a failure travels to the end of the pipeline and out to the user while later stages keep running.
- **Unknown chunk kinds are passed through unchanged.** Any tool that receives an analysis-document chunk whose `kind` it does not understand re-emits it verbatim. This is what lets new record kinds be added without breaking existing pipelines.
- **`OutputFormat` is metadata.** No framework code branches on it; the host's IO context does. Tools set it in the constructor, or reassign it in `HandleExecution` before building results when a `-format` parameter changes the shape.
- **Reading environment is a pure read.** Every `env.GetValue(...)` call passes `storeDefault: false`; the default `true` would mark the child environment changed and trigger a write-back on every invocation.

#### The analysis document (the wire and file format)

One chunk = one JSON object = one line. `ResultFormat.JSON`. A well-formed analysis stream is exactly one `analysis` chunk, then N `token` chunks in ascending `index`, then zero or more `attribution` chunks, then zero or one `stats` chunk.

| Record | Fields |
|---|---|
| `analysis` | `kind`, `analysisId` (short, URL-safe, 12 chars), `schemaVersion` (`1`), `createdUtc`, `source` (`live`\|`demo`\|`file`), `synthetic` (bool), `provider`, `modelId`, `topK`, `temperature`, `contextSize`, `promptTokenCount`, `generatedTokenCount`, `truncatedTopK` (always `true` for live data — alternatives are the top-K, not the vocabulary), `prompt` (present unless `-redact`) |
| `token` | `kind`, `analysisId`, `index` (**0-based**), `step`, `tokenId` (`-1` = unavailable), `text`, `charStart`, `charEnd` (`-1`/`-1` = unknown, never the source's `n/a` string), `logprob` (natural log, `null` when unavailable), `probability` (`exp(logprob)`, 0–1), `entropy`, `normalizedEntropy`, `margin`, `band`, `alternatives[]`, `synthetic`, `estimated` |
| `alternatives[]` element | `tokenId`, `text`, `logprob`, `probability`. Nested alternatives are always absent (BR-19). Ordered **descending by probability** — the source imposed ordering in exactly one surface (BR-33) and trusted arrival order everywhere else; the rebuild normalises once, at the producer. |
| `attribution` | `kind`, `analysisId`, `tokenIndex`, `tokenId`, `tokenText`, `influencingText`, `influenceSpan` (`{start,end}` or `null`), `influenceScore` (0.0–1.0), `method` (`recency-window`\|`attention`\|`gradient`) |
| `stats` | `kind`, `analysisId`, `n`, `meanProbability`, `medianProbability`, `minProbability`, `maxProbability`, `meanNegLogLikelihood`, `perplexity`, `meanEntropy`, `meanNormalizedEntropy`, `meanMargin`, `bands` (`{veryHigh,high,medium,low,veryLow}` counts), `lowConfidenceCount`, `lowConfidenceRatio`, `threshold`, `entropyUnit` (`nats`\|`bits`), `entropyIsTruncated` (always `true` for live top-K data) |

**Indexing rule, stated once for the whole product:** the document is **0-based**; every human-facing rendering is **1-based**. The source had grid cards, both table renderers and the plain-text report numbering from 1 while the terminal-UI panel numbered from 0 (QUIRK-Q15) — the same token was `#7` in one view and `6:` in another. One rule, applied everywhere.

**Probability scale rule, stated once:** probabilities are **0–1 internally**, formatted to a percentage only at the display edge. The source carried three incompatible scales simultaneously, producing `0.92%` for a 92 %-confident token and a doubled `%%` in several formatters (QUIRK-Q3). Confidence bands keep the source's boundaries, renormalised: **≥ 0.90 `very-high`, ≥ 0.70 `high`, ≥ 0.50 `medium`, ≥ 0.30 `low`, else `very-low`**, evaluated top-down, inclusive at the lower bound (BR-22).

**No-fabrication rule, stated once — owner-ratified as decision D-001 (2026-08-29, `DECISIONS.md`):** no tool in this package ever invents data and presents it as measured. When a provider returns no probabilities, the analysis carries zero `token` chunks and the `analysis` chunk says why; it does not silently substitute a synthetic list as the source's cloud adapter did (BR-50, verified by a test that pinned the fabrication). `TOKEN DEMO` is the sole fabricating tool, every record it emits is stamped `synthetic: true`, and every downstream tool carries that stamp forward.

D-001 adds the *capability-absence* behaviour on top of the no-fabrication rule: when capture is **on** and the active backend's declared capability record (`MODEL CAPS`) says it **cannot** supply token log probabilities, the product **turns capture off** (`TOKEN_ENABLED` → `false`, persisted) and emits the auto-disable notice — `Token log probabilities disabled: provider '{provider}' does not support them. Use a provider that does (see MODEL CAPS).` — naming a configured capable provider where one exists. This fires on backend switch (`MODEL USE` owns it, §5) and at session start (the host owns it, §A.3); it never fires on a *transient* absence from a backend that declares the capability, which instead produces the honest two-line "none were returned" notice. In the full-screen shell the enabling control is rendered disabled with the same explanation while a capability-absent backend is active, and probability-view attempts produce a **non-blocking** transient status notice, never a modal.

---

#### 6.3.1 `TOKEN CAPTURE` — capture policy

| | |
|---|---|
| **Command** | `CAPTURE` |
| **Root** | `TOKEN` |
| **Description** | `Configure per-token confidence capture` |
| **Prototype** | `TOKEN CAPTURE [status\|on\|off] [-topk 1-20] [-mode sample\|all] [-layout list\|grid] [-gridmaxalt 1-20] [-samplesize n] [-maxgen n] [-lowconf 0.0-1.0] [-print]` |

```csharp
[CommandRoot("TOKEN", "Token-level introspection of model output")]
[CommandRegister("Capture", "Configure per-token confidence capture",
    Prototype = "TOKEN CAPTURE [status|on|off] [-topk 1-20] [-mode sample|all] "
              + "[-layout list|grid] [-gridmaxalt 1-20] [-samplesize n] [-maxgen n] "
              + "[-lowconf 0.0-1.0] [-print]")]
[CommandParameterOrdered("action", "status | on | off  (bare form reports status)", IsRequired = false)]
[CommandParameterNamed("topk", "Alternatives requested per position (1-20)", DataType = typeof(int))]
[CommandParameterNamed("mode", "Which tokens later views present", AllowedValues = new[] { "sample", "all" })]
[CommandParameterNamed("layout", "Preferred presentation", AllowedValues = new[] { "list", "grid" })]
[CommandParameterNamed("gridmaxalt", "Alternatives per grid card (1-20)", DataType = typeof(int))]
[CommandParameterNamed("samplesize", "Tokens per sampled segment", DataType = typeof(int))]
[CommandParameterNamed("maxgen", "Token budget for TOKEN MAP", DataType = typeof(int))]
[CommandParameterNamed("lowconf", "Low-confidence threshold", DataType = typeof(double))]
[CommandFlag("print", "Emit the resulting policy as key=value lines", ShortAlias = "p")]
[CommandHelpRemarks("Bare 'TOKEN CAPTURE' reports the current policy and changes nothing.")]
[CommandHelpRemarks("Policy lives in this package's private environment bucket; pipe -print into CONFIG SET to persist it.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `action` | ordered | `string` | no | *(absent ⇒ status)* | `status`, `on`, `off` — **validated in-body**, not via `AllowedValues`, so the source's `Unknown subcommand: {arg}. ` message survives | What to do |
| `topk` | named | `int` | no | `5` | **1–20 inclusive** (BR-02) | Alternatives requested per position |
| `mode` | named | `string` | no | `sample` | `sample`, `all` — `sample` first so it becomes the auto-default (BR-03) | Which tokens later views present |
| `layout` | named | `string` | no | `list` | `list`, `grid` — `list` first (BR-04) | Preferred presentation |
| `gridmaxalt` | named | `int` | no | `5` | **1–20 inclusive** (BR-05) | Alternatives per grid card |
| `samplesize` | named | `int` | no | `5` | 1–100 — **NEW** (source hard-coded 5, BR-10) | Tokens per sampled segment |
| `maxgen` | named | `int` | no | `10` | 1–8192 — source used `min(10, maxTokens)` (M5) | Token budget for `TOKEN MAP` |
| `lowconf` | named | `double` | no | `0.30` | 0.0–1.0 — **NEW** (BR-22 boundary) | Low-confidence threshold |
| `print` | flag | `bool` | n/a | `false` | presence ⇒ true | **NEW** — emit policy as `key=value` for piping |

**Pipeline behaviour.** Neither source nor sink by default: bare and mutating forms return `Success(string.Empty)`, which the host drops — the same silence the built-in `SET` uses. With `-print` it becomes a **source**, emitting one `key=value` line per policy value (`ResultFormat.General`). It accepts piped input only in the `-print`-less mutating form, where one chunk is one `key=value` line applying one policy change, so `CONFIG GET -section token | TOKEN CAPTURE` restores a saved policy.

**Environment.** Reads and writes all eleven `TOKEN_*` keys. Reads global `PROVIDER`/`MODELID` for the status report only. **Needs no environment-modifying permission** — every key it writes is prefixed with its own root command name, so `CommandController.Run` routes it into the `TOKEN` bucket automatically.

**Failure modes.**

| Condition | Result |
|---|---|
| `-topk 0`, `-topk 21`, `-topk abc`, `-topk 1.5` | Failure, message exactly `Top-K value must be a number between 1 and 20`. No state change, no write. (Preserves BR-02's reject-don't-clamp semantics; the source's settings dialog clamped instead, an inconsistency the rebuild deliberately drops in favour of rejection — see §6.5.) |
| `-gridmaxalt` out of range | Failure, `Grid max alternatives value must be a number between 1 and 20` |
| `-topk` as the final token with no value | The framework throws `ArgumentOutOfRangeException` inside `CommandParameters` **before** the tool runs; the user sees `Error executing TOKEN (see trace for more info)`. Mitigated by naming the value in the prototype and by a help remark; it cannot be fixed from inside the tool. |
| Unknown `action` | Failure, `Unknown subcommand: {arg}. ` followed by the valid-option list — the trailing space after the period is preserved verbatim from the source |
| `on` while the active backend's declared capability record says it cannot supply token log probabilities | **Refusal (D-001):** failure with exactly `Cannot enable token log probabilities: provider '{provider}' does not support them. Switch providers first (see MODEL CAPS).` No state change, no write. The full-screen shell's equivalent control is disabled with this same text as its explanation. |
| `-lowconf` outside 0.0–1.0 | Failure, `Low-confidence threshold must be between 0.0 and 1.0` |
| Piped chunk not of the form `key=value` | That chunk fails with `Not a policy assignment: {chunk}`; the failure travels downstream and later chunks are still processed |

**Security and audit.** No secret in any parameter or output. `-print` emits policy values only — never the model id, endpoint or provider credentials. Non-destructive; no confirmation required.

**Traceability.** **7.9 Token Probability Analysis** (and 7.2 for the persisted keys). Descends from `/logprobs`, `/logprobs enable|disable|top <n>|showall|showsample|grid|list|gridmaxalt <n>` and the equivalent `/set enableLogProbabilities|logProbabilitiesTopK|showAllTokens|gridViewForTokens|gridViewMaxAlternatives` keys with their aliases `logprobs`, `logtopk`, `tokensgrid`, `gridmaxalt`. `-samplesize`, `-maxgen`, `-lowconf` and `-print` are **NEW**.

---

#### 6.3.2 `TOKEN DIAG` — capture diagnostics

| | |
|---|---|
| **Command** | `DIAG` |
| **Root** | `TOKEN` |
| **Description** | `Explain why token confidence is or is not available` |
| **Prototype** | `TOKEN DIAG [-verbose]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `verbose` | flag | `bool` | n/a | `false` | presence ⇒ true | Include backend capability probe details |

**Pipeline behaviour.** Source only. Emits `ResultFormat.General` lines; with `-verbose`, a trailing `ResultFormat.JSON` capability record. Ignores piped input (declares no `UsePipe` parameter and returns its report unchanged per chunk would be meaningless, so it overrides `Main` to emit its report once and drain any input pipe without acting on it).

**Environment.** Reads all `TOKEN_*` policy keys plus globals `PROVIDER`, `MODELID`. Writes nothing.

**Failure modes.** Cannot fail on input. When no `IProbabilitySource` is registered it reports `Backend: none registered — TOKEN SPLIT/MAP/INSPECT are unavailable in this host` and still succeeds, because the whole purpose of the tool is to explain absence.

**Security and audit.** The report names the provider and model but **masks the endpoint host to its scheme and registrable domain** and never prints a key, token or region credential. This is a deliberate tightening: the source's `/logprobs debug` echoed the configured Azure endpoint verbatim.

**Traceability.** **7.9**. Descends from `/logprobs debug`. One correction is deliberate: the source's static troubleshooting text advised an API version (`2023-05-15`) that the code did not send (`2023-12-01-preview`) and that predates the parameter it relies on (QUIRK-Q6). `TOKEN DIAG` asks the registered `IProbabilitySource` for the version and capability set it will actually use and prints that; it holds no hard-coded provider knowledge.

---

#### 6.3.3 `TOKEN SPLIT` — tokenize text

| | |
|---|---|
| **Command** | `SPLIT` |
| **Root** | `TOKEN` |
| **Description** | `Split text into model tokens with IDs and character spans` |
| **Prototype** | `TOKEN SPLIT ["text"] [-bos\|-nobos] [-context n] [-format json\|text]` |

```csharp
[CommandRoot("TOKEN", "Token-level introspection of model output")]
[CommandRegister("Split", "Split text into model tokens with IDs and character spans",
    Prototype = "TOKEN SPLIT [\"text\"] [-bos|-nobos] [-context n] [-format json|text]")]
[CommandParameterNamed("context", "Context window for the tokenizer load (0 = provider default)",
    DataType = typeof(int), DefaultValue = "0")]
[CommandParameterNamed("format", "Output shape", AllowedValues = new[] { "json", "text" })]
[CommandFlag("nobos", "Do not prepend the beginning-of-sequence token")]
[CommandParameterSuffix("text", "Text to tokenize", IsRequired = false, UsePipe = true)]
[CommandHelpRemarks("Quote the text. The argument tokenizer splits unquoted '.', '/' and ':' into separate words.")]
[CommandHelpRemarks("Piped text is tokenized one chunk at a time; each chunk becomes its own analysis.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `text` | suffix | `string` | no (in-body check) | *(none)* | any text; **all remaining tokens joined with single spaces into one string** | Text to tokenize. `UsePipe = true` — omitted from parsing when piped |
| `nobos` | flag | `bool` | n/a | `false` | presence ⇒ true | **NEW** — suppress the BOS marker the source always prepended |
| `context` | named | `int` | no | `0` | `0`, or 512–32768 | **NEW** — replaces the source's hard-coded 512 (M1, Q8); `0` means "use the provider's configured `llamaContextSize`", which the source ignored entirely |
| `format` | named | `string` | no | `json` | `json`, `text` | `json` emits the analysis document; `text` emits the source's fixed-width `Index \| Token ID \| Token Text` table |

**Pipeline behaviour.** **Both.** As a source it overrides `Main` to emit one `analysis` chunk followed by one `token` chunk per token — `AbstractCommand`'s non-piped path can emit only a single chunk, so multi-chunk emission requires the override, which the framework explicitly permits. As a filter, **one piped chunk is one complete text to tokenize**, producing its own analysis id; a 20-line piped document therefore yields 20 independent analyses, which is exactly what you want for `CHAT EXPORT | TOKEN SPLIT | TOKEN STATS`. Declares `ResultFormat.JSON` (or `General` under `-format text`) because downstream tools parse the records rather than reading them.

**Environment.** Reads `TOKEN_CONTEXT`, `TOKEN_KEEP`, `TOKEN_LAST` and global `MODELID`, `PROVIDER`. Writes `TOKEN_LAST` (its own bucket). No environment-modifying permission needed.

**Failure modes.**

| Condition | Result |
|---|---|
| No text and no pipe | Failure: `Please provide text to split. Usage: TOKEN SPLIT "<text>"` (the source's `Please provide text to tokenize.` wording, adapted to the new name) |
| No `ITokenizer` registered in the host | Failure: `No tokenizer is available in this host. Register a local inference backend, or use TOKEN DEMO for sample data.` — **never** a heuristic fallback presented as real |
| Provider is not a tokenizing provider | Failure: `Tokenization requires a local model. Set a local provider with 'CONFIG SET provider llama'.` The source gated on the exact lowercase string `"llama"`; the rebuild gates on a *capability* the provider advertises, so a second local backend needs no code change here |
| Model not configured, or the model file does not exist | Failure: `Model not configured or file not found. Use 'CONFIG SET modelId <path-to-model>' first.` (preserved wording) |
| Backend throws mid-load | Failure: `Error splitting text: {message}` — the source's double-prefixing (`Error during token inspection: Error analyzing prompt: <root>`) is deliberately collapsed to one layer |
| Piped chunk is empty | Skipped by `AbstractCommand.Main` before the tool sees it |
| Upstream failure chunk | Forwarded verbatim; the tool is not invoked for it |

**Security and audit.** ⚠️ **The `text` suffix parameter is content-bearing and is written verbatim into `AuditEvent.Parameters`.** The framework's masking cannot help. Mitigation, in order of preference: (1) pipe text in — piped payloads are not part of `AuditEvent.Parameters`; (2) the host registers an `IAuditMaskingConfiguration` whose `RedactedParameterNames` includes `text`, accepting that it only fires for `-name=value` forms; (3) the host disables audit parameter capture for this root. This package declares `text`, `prompt` and `note` as its content-bearing parameter names so a host can honour (2) mechanically. Non-destructive; no confirmation required.

**Traceability.** **7.10 Token Inspection**. Descends from `/tokenize` (`TokenizeCommand`) and `TokenInspectionService.AnalyzePrompt`. Two source behaviours are deliberately **not** preserved: the placeholder text `<token_{id}>` (the source never decoded a token to a string — QUIRK-Q5; the rebuild requires `ITokenizer.Decode` and reports a real string, falling back to `<token_{id}>` only when the backend genuinely has no decoder, in which case `estimated: true` is set on every record), and the uniform-distribution character-span estimator, which produced spans "that always *look* like a clean segmentation even though they bear no relation to real token boundaries". The rebuild emits real spans when the backend supplies them and `charStart = charEnd = -1` when it does not. `-nobos`, `-context` and `-format` are **NEW**.

---

#### 6.3.4 `TOKEN MAP` — probability map

| | |
|---|---|
| **Command** | `MAP` |
| **Root** | `TOKEN` |
| **Description** | `Map the probability distribution the model weighed at each generation step` |
| **Prototype** | `TOKEN MAP ["prompt"] [-n tokens] [-topk 1-20] [-context n] [-temperature t] [-seed s]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `prompt` | suffix | `string` | no (in-body check) | *(none)* | joined remaining tokens; `UsePipe = true` | Prompt to continue |
| `n` | named | `int` | no | `10` (`TOKEN_MAXGEN`) | 1–8192; the source capped at `min(10, maxTokens)` regardless of the 1000 default (M3, M5) | Tokens to generate |
| `topk` | named | `int` | no | `5` (`TOKEN_TOPK`) | **1–20 inclusive** (BR-02). Note the source's own inspection service applied *no* clamp here at all (Q14), accepting 0 and negative values; the rebuild applies the same 1–20 rule everywhere | Alternatives per position |
| `context` | named | `int` | no | `0` | `0`, or 512–32768 | **NEW** — replaces the hard-coded 2048 (M2, Q8) |
| `temperature` | named | `double` | no | global `TEMPERATURE` | 0.0–2.0 | Sampling temperature for this run only; does not mutate global settings |
| `seed` | named | `long` | no | *(backend default)* | any | **NEW** — makes a run reproducible; the source's stage D was non-deterministic while printing deterministic fabricated numbers |

**Pipeline behaviour.** **Both.** Source: overrides `Main`, emitting one `analysis` chunk then one `token` chunk per generated step, streamed as generation proceeds so a downstream `TOKEN FILTER | TOKEN SHOW` starts printing before the model finishes. Filter: one piped chunk is one prompt. `ResultFormat.JSON`. **Backpressure note:** at one chunk per token with the framework's default `MaxChannelQueueSize` of 10 000 and `BackpressureMode.Block`, a long generation blocks rather than dropping — the correct choice, and the host should not switch this root to `DropOldest`.

**Environment.** Reads `TOKEN_TOPK`, `TOKEN_MAXGEN`, `TOKEN_CONTEXT`, `TOKEN_KEEP`, globals `PROVIDER`, `MODELID`, `TEMPERATURE`, `MAXTOKENS`. Writes `TOKEN_LAST`.

**Failure modes.** Same preconditions and messages as `TOKEN SPLIT`, plus:

| Condition | Result |
|---|---|
| Provider supports generation but not per-token probabilities | Succeeds, emits the `analysis` chunk with `truncatedTopK: false`, `generatedTokenCount` set, and `token` chunks whose `logprob` is `null` and `estimated: false`. A companion note chunk carries the source's exact text: `Note: Log probabilities were requested but none were returned by the model.` / `This could be due to the model not supporting this feature or an API limitation.` **No data is fabricated.** |
| Generation produces nothing | Succeeds with an `analysis` chunk and zero `token` chunks; `TOKEN STATS` downstream reports `n: 0` rather than dividing by zero |
| Backend throws mid-generation | Partial stream already emitted, then one failure chunk `Error generating probability map: {message}`. Chunks already downstream are not retracted — the analysis is marked `partial: true` in a trailing record |
| Model in use by another generation | The provider abstraction serialises; the tool reports `Waiting for the model…` via `io.SetStatusMessage` and does not spin. The source's `/inspect` bypassed the chat path's semaphores entirely and could attempt a concurrent native load |

**Security and audit.** `prompt` is content-bearing — see §6.3.3. Non-destructive; consumes model time and, for a hosted provider, **incurs cost** — the tool calls `io.SetProgress(n, step)` so the host can show what it is spending. No confirmation required (generation is not irreversible), but a host may choose to gate it.

**Traceability.** **7.10** (and 7.9 for the top-K contract). Descends from `TokenInspectionService.GenerateProbabilityMap` and `/inspect` stage D. The decisive correction: the source **fabricated every number** in this stage — token IDs `1000 + i`, a constant log-probability of `-2.5` on every token, and exactly `topK` alternatives named `{word}_alt{j+1}` with log-probabilities `-3.0 - j` — while running real inference and presenting the result as measurement (QUIRK-Q3). `TOKEN MAP` reports what the backend measured or reports nothing. `-context`, `-temperature` and `-seed` are **NEW**.

---

#### 6.3.5 `TOKEN ATTRIBUTE` — influence attribution

| | |
|---|---|
| **Command** | `ATTRIBUTE` |
| **Root** | `TOKEN` |
| **Description** | `Attribute each generated token to the input span that influenced it` |
| **Prototype** | `TOKEN ATTRIBUTE [-method recency\|attention\|gradient] [-window n] [-prompttail n] [-cutover n] [-id analysisid]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `method` | named | `string` | no | `recency` | `recency`, `attention`, `gradient` — `recency` first so it is the auto-default | Attribution model. `attention`/`gradient` require a backend that exposes them; otherwise the tool refuses rather than silently downgrading |
| `window` | named | `int` | no | `5` | 1–512 | Look-back window for `recency` (M12: the source concatenated tokens `[max(0, i-5), i)`) |
| `prompttail` | named | `int` | no | `50` | 1–4096 | Characters of prompt tail used for early tokens (M11) |
| `cutover` | named | `int` | no | `3` | 0–1024 | Token index at which attribution switches from prompt-tail to look-back window (M10) |
| `id` | named | `string` | no | `TOKEN_LAST` | 12-char analysis id | Analysis to attribute when not piped |

**Pipeline behaviour.** **Both, and filter-shaped by design.** Inherits `AbstractCommand`: one piped chunk in, one chunk out. `analysis` chunks pass through unchanged; `token` chunks pass through and are buffered (`OnStartPipe` clears the buffer, so the tool is safe even if the host registered it as a DI singleton); at `OnEndPipe` the accumulated attribution cannot be yielded — so the tool overrides `Main` to interleave, emitting each `token` chunk followed immediately by its `attribution` chunk once the look-back window is satisfiable. Non-piped, it loads the analysis named by `-id` (or `TOKEN_LAST`) from the in-process store and emits the same stream. `ResultFormat.JSON`.

**Environment.** Reads `TOKEN_LAST`. Writes nothing.

**Failure modes.**

| Condition | Result |
|---|---|
| Not piped and no `-id` and `TOKEN_LAST` unset | Failure: `No analysis to attribute. Run TOKEN MAP first, or pipe an analysis in.` (The source's docs implied a "no analyses available" message that no code ever produced.) |
| `-id` names an analysis that has aged out of the ring | Failure: `Analysis '{id}' is no longer in memory. Re-run it, or load it with TOKEN LOAD.` |
| `-method attention` on a backend with no attention export | Failure: `Attribution method 'attention' is not available from the current backend. Available: recency.` **The tool never silently substitutes a weaker method.** |
| `-window 0`, `-cutover -1` etc. | Failure naming the parameter and its range |
| Upstream failure chunk | Forwarded verbatim |

**Security and audit.** `influencingText` is a verbatim slice of the prompt and therefore content-bearing; it is subject to the same `-redact` treatment in `TOKEN EXPORT`. Non-destructive.

**Traceability.** **7.10**. Descends from `/inspect` stage E and `TokenInspectionService.BuildAttributionMap`. Behaviours preserved: the 3-token cut-over, the 50-character prompt tail, the ≤5-token look-back window, one entry per generated token with `attributionIndex == tokenIndex`, and the guarantee that the attribution list is the same length as the generated-token list. Behaviours corrected: the source stamped a **constant `InfluenceScore = 0.8`** on every entry, labelled in its own comments as a "Placeholder for a real influence calculation" (M13). The rebuild makes `recency` report a genuine recency weight normalised to sum to 1.0 across the window, and marks `method` on every record so a reader can never mistake a heuristic for a measurement. The source also truncated the influencing text to 37 characters + `...` **before** escaping, splitting escape sequences (M14); the rebuild carries the full text in the document and lets `TOKEN SHOW` truncate at the display edge.

---

#### 6.3.6 `TOKEN INSPECT` — the composite run

| | |
|---|---|
| **Command** | `INSPECT` |
| **Root** | `TOKEN` |
| **Description** | `Tokenize, generate, map probabilities and attribute in one run` |
| **Prototype** | `TOKEN INSPECT ["text"] [-n tokens] [-topk 1-20] [-yes] [-stopafter split\|map\|attribute]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `text` | suffix | `string` | no (in-body check) | *(none)* | joined remaining tokens; `UsePipe = true` | Text to inspect |
| `n` | named | `int` | no | `10` | 1–8192 | Tokens to generate (`min(10, maxTokens)` in the source, M5) |
| `topk` | named | `int` | no | `5` | 1–20 | Alternatives per position |
| `yes` | flag | `bool` | n/a | `false` | presence ⇒ true | Skip the generation-consent prompt |
| `stopafter` | named | `string` | no | `attribute` | `split`, `map`, `attribute` — `attribute` cannot be first (it would become the auto-default correctly, and it is the source's full behaviour) | Where to stop |

**Pipeline behaviour.** Source and filter, identical to `TOKEN MAP` but emitting the union of all three stages' records for one analysis id. `ResultFormat.JSON`.

**The consent gate — the one behaviour that most needed rebuilding.** The source printed `Do you want to continue with probability analysis? This will generate tokens and analyze their probabilities. (y/n)` and **blocked on `Console.ReadLine()`**, which has no console under a full-screen terminal UI and no answerer in a pipeline (Q7). The rebuild:

1. When `io.HasPipedInput == false` **and** `-yes` was not given: `await io.PromptForCommand("Continue with generation? (y/n)")`. Only `y`/`yes` (case-insensitive, invariant) proceed — every other answer, including empty and end-of-input, stops after the split stage and still returns success, preserving the source's semantics exactly.
2. When `io.HasPipedInput == true`: **never prompt.** `PromptForCommand` is contractually meaningful only when not piped. Without `-yes`, the tool stops after the split stage and emits one note chunk: `Generation skipped: pass -yes to generate inside a pipeline.`
3. `-stopafter split` makes the gate moot and is the scriptable form.

**Environment.** Union of `TOKEN SPLIT`, `TOKEN MAP` and `TOKEN ATTRIBUTE`. Writes `TOKEN_LAST` once, for the single composite analysis.

**Failure modes.** The union of the three stages', with one structural difference from the source: the model is loaded **once**, not twice. The source ran a 512-context load for tokenization and a separate 2048-context load for generation with a full unload between (Q9), making a multi-gigabyte model pay twice per command. Additionally, where the source returned `success` from a declined consent gate in a way indistinguishable from a completed analysis, the rebuild's trailing `analysis` record carries `stoppedAfter: "split"` so a consumer can tell.

**Security and audit.** `text` is content-bearing (§6.3.3). Generation may incur provider cost. Non-destructive.

**Traceability.** **7.10**. Descends from `/inspect` (`InspectCommand`), preserving its precondition ordering — argument count is checked **before** provider, which is checked **before** model file, so a bare `TOKEN INSPECT` on a perfectly configured llama setup still reports the argument error. `-yes` and `-stopafter` are **NEW**; they exist because the source's gate made the command unusable non-interactively.

---

#### 6.3.7 `TOKEN STATS` — derived statistics **NEW**

**Why it earns its place.** The source stored a log-probability and derived `exp(logprob)` and nothing else. Every question a user actually asks — *how confident was this answer overall? where was it guessing? is this model more certain than that one?* — needs an aggregate, and the README's four stated use cases ("understanding model confidence", "identifying uncertain parts", "debugging unexpected outputs", "tuning prompts") are all aggregate questions. This is the smallest tool that answers them, and it is pure arithmetic over a document, so it runs with no backend, no model and no network.

| | |
|---|---|
| **Command** | `STATS` |
| **Root** | `TOKEN` |
| **Description** | `Compute entropy, perplexity and confidence statistics over an analysis` |
| **Prototype** | `TOKEN STATS [-id analysisid] [-threshold 0.0-1.0] [-unit nats\|bits] [-format json\|csv\|text] [-perstep]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `id` | named | `string` | no | `TOKEN_LAST` | 12-char analysis id | Analysis to summarise when not piped |
| `threshold` | named | `double` | no | `0.30` (`TOKEN_LOWCONF`) | 0.0–1.0 | Low-confidence cut-off (the source's lowest colour band, BR-22) |
| `unit` | named | `string` | no | `nats` | `nats`, `bits` | Entropy unit |
| `format` | named | `string` | no | `json` | `json`, `csv`, `text` | Output shape; sets `OutputFormat` to `JSON`/`CSV`/`General` accordingly |
| `perstep` | flag | `bool` | n/a | `false` | presence ⇒ true | Also emit per-token entropy/margin rows instead of the summary alone |

**Definitions (normative).** For the selected token set of size *n*, with chosen-token probability *pᵢ = exp(logprobᵢ)* clamped to (0, 1]:

- `meanNegLogLikelihood` = −(1/n) Σ ln pᵢ
- `perplexity` = exp(meanNegLogLikelihood)
- per-step `entropy` Hᵢ = −Σ_k q_k ln q_k over the chosen token **plus** its top-K alternatives, renormalised so Σ q_k = 1. **This is a truncated top-K entropy and therefore a lower bound on the true distribution's entropy**; `entropyIsTruncated` is always `true` for live data and the tool says so in `-format text`. Reporting a bound honestly is the point; the source reported nothing.
- `normalizedEntropy` = Hᵢ / ln(K+1) ∈ [0, 1], so runs with different top-K are comparable.
- `margin` = p₁ − p₂ (chosen minus best alternative); `null` when the record has no alternatives.
- `bands` = counts per the fixed boundaries ≥0.90 / ≥0.70 / ≥0.50 / ≥0.30 / else.

**Pipeline behaviour.** **Both, and it is the one tool that must aggregate.** `HandlePipedChunk` cannot emit an end-of-stream summary (`OnEndPipe` returns `void`), so `STATS` overrides `Main`: it consumes the whole input stream, passes every `analysis`/`token`/`attribution` chunk through unchanged, and appends one `stats` chunk per analysis id at end of stream — so `… | TOKEN STATS | TOKEN SHOW` shows both the tokens and the summary. With `-perstep` it enriches each `token` chunk with `entropy`, `normalizedEntropy` and `margin` in place. `-format csv` emits a header row and one row per analysis (or per token with `-perstep`) and declares `ResultFormat.CSV`.

**Environment.** Reads `TOKEN_LOWCONF`, `TOKEN_LAST`. Writes nothing.

**Failure modes.**

| Condition | Result |
|---|---|
| `n = 0` (no token records) | **Succeeds** with `n: 0` and every aggregate `null` — no division by zero, no error. An empty analysis is a legitimate answer |
| Every record has `logprob: null` | Succeeds; `perplexity`/`meanNegLogLikelihood` are `null`, `bands` all zero, and a `note` field explains that the provider returned no probabilities |
| A record has `logprob > 0` (not a log-probability) | That record is excluded, `excludedRecords` is incremented, and the summary carries `warning: "n records carried non-negative log-probabilities and were excluded"`. The source accepted and exponentiated positive values without comment, which is how its demonstration data produced probabilities of e⁷⁰ (QUIRK-Q2, BR-09) |
| `-threshold` outside 0.0–1.0 | Failure naming the range |
| Not piped, no `-id`, `TOKEN_LAST` unset | Failure: `No analysis to summarise. Pipe one in, or run TOKEN MAP first.` |

**Security and audit.** Emits **numbers only** — no token text appears in a `stats` record. `TOKEN STATS -format csv` is therefore the safe artefact to paste into a bug report, and the composition `TOKEN LOAD run42 | TOKEN STATS -format csv` is the recommended way to share a finding without sharing the prompt. Non-destructive.

**Traceability.** **7.9** / **7.10**. **NEW** — no ancestor. The nearest thing in the source was `TokenLogProbability.Probability` (`exp(logprob)`), whose sole test asserted `25` for a stored `ln(0.25)` and could not pass (QUIRK-Q1); this tool's contract is `probability = exp(logprob)` on a 0–1 scale, and the corresponding test asserts `0.25`.

---

#### 6.3.8 `TOKEN FILTER` — confidence filter **NEW**

**Why it earns its place.** The framework's own idiom for narrowing a stream is a filter command that returns empty-success for records it rejects (`REGIF`). The single most common introspection task — *show me only where the model was unsure* — has no expression in the source at all; it required reading every token in a grid and looking for a colour. One composable primitive replaces that.

| | |
|---|---|
| **Command** | `FILTER` |
| **Root** | `TOKEN` |
| **Description** | `Keep only the token records that match a confidence predicate` |
| **Prototype** | `TOKEN FILTER [-below p] [-above p] [-band veryhigh\|high\|medium\|low\|verylow] [-marginbelow m] [-range start end] [-mode sample\|all] [-samplesize n] [-dedupe] [-invert]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `below` | named | `double` | no | *(unset)* | 0.0–1.0 | Keep records with probability strictly below this |
| `above` | named | `double` | no | *(unset)* | 0.0–1.0 | Keep records with probability at or above this |
| `band` | named | `string` | no | *(unset)* | `veryhigh`, `high`, `medium`, `low`, `verylow` | Keep records in this confidence band |
| `marginbelow` | named | `double` | no | *(unset)* | 0.0–1.0 | Keep records whose chosen-vs-runner-up margin is below this — near-ties |
| `range` | named | `int` ×2 | no | *(unset)* | two integers, `start` ≤ `end` | Keep records whose 0-based index is in `[start, end]` |
| `mode` | named | `string` | no | `sample` (`TOKEN_MODE`) | `sample`, `all` | `all` keeps every record; `sample` applies the beginning/middle/end rule below |
| `samplesize` | named | `int` | no | `5` (`TOKEN_SAMPLESIZE`) | 1–100 | Records per sampled segment |
| `dedupe` | flag | `bool` | n/a | `false` | presence ⇒ true | **NEW** — drop records selected by more than one segment |
| `invert` | flag | `bool` | n/a | `false` | presence ⇒ true | Keep exactly the records the predicate rejects |

**The sampling rule, preserved exactly (BR-10, BR-11, BR-12).** With `-mode sample` and no other predicate: if the record count ≤ `3 × samplesize` (**15** by default) every record is kept, un-segmented. Otherwise exactly three segments of `samplesize` records are kept — beginning at index `0`, middle at index `count / 2 − samplesize / 2` (integer division; `count/2 − 2` at the default), end at index `count − samplesize`. **Segments may overlap or leave gaps for counts just above the floor and no de-duplication is performed** — at count 16 the segments are `0–4`, `6–10`, `11–15`, so index 5 is never shown and nothing is duplicated. That is the source's behaviour and it is preserved by default; `-dedupe` is the opt-in correction. Records keep their **absolute** `index` throughout, which fixes QUIRK-Q9 (the source's demonstration path renumbered sampled tokens 1…15, losing their true positions 1-5, 11-15, 21-25).

**Pipeline behaviour.** **Filter — its natural and primary form.** Inherits `AbstractCommand`. One piped chunk is one document record. `analysis`, `attribution` and `stats` chunks pass through unchanged (a filter must never strip an analysis's header). `token` chunks that match are re-emitted verbatim; `token` chunks that do not match return `CommandResult<string>.Success(string.Empty, …)`, which the host drops — the framework's canonical filter idiom. Sampling predicates need the total count, so `-mode sample` buffers `token` chunks per analysis id in `Main` and flushes the selection when the analysis ends; `OnStartPipe` clears that buffer. Non-piped, it filters the analysis named by `-id`/`TOKEN_LAST`. `ResultFormat.JSON`.

**Environment.** Reads `TOKEN_MODE`, `TOKEN_SAMPLESIZE`, `TOKEN_LOWCONF`, `TOKEN_LAST`. Writes nothing.

**Failure modes.** No predicate at all with `-mode all` is a no-op pass-through, not an error. `-below` above `-above` yields an empty result set, not an error. `-range` with a missing second value is caught by the framework before the tool runs (`Error executing TOKEN …`), which is why the prototype spells both values. A record with `logprob: null` is treated as unmatched by every probability predicate and is dropped unless `-invert`.

**Security and audit.** Passes content through; adds none. Non-destructive.

**Traceability.** **7.9**. **NEW** as a tool; the sampling half descends directly from the source's shared sampler (`DemoLogProbsCommand`, `ChatShell`, `TokenProbabilityVisualizer`, `SpectreConsoleFormatter` — the same logic in four places, three of them unreachable at runtime). Collapsing it to one implementation was the source's own recorded recommendation.

---

#### 6.3.9 `TOKEN SHOW` — view an analysis

| | |
|---|---|
| **Command** | `SHOW` |
| **Root** | `TOKEN` |
| **Description** | `Render an analysis as readable text` |
| **Prototype** | `TOKEN SHOW [-id analysisid] [-layout list\|grid] [-maxalt 1-20] [-alts 0-20] [-attribution] [-stats] [-width n]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `id` | named | `string` | no | `TOKEN_LAST` | 12-char analysis id | Analysis to render when not piped |
| `layout` | named | `string` | no | `list` (`TOKEN_LAYOUT`) | `list`, `grid` — `list` first (BR-04) | Presentation shape |
| `maxalt` | named | `int` | no | `5` (`TOKEN_GRIDMAXALT`) | 1–20 (BR-05) | Alternatives drawn per grid card before a `+N more` marker |
| `alts` | named | `int` | no | `3` | 0–20 | Alternatives listed per list row (the source's shared helper defaulted to 3, BR-25) |
| `attribution` | flag | `bool` | n/a | `false` | presence ⇒ true | Include attribution lines |
| `stats` | flag | `bool` | n/a | `false` | presence ⇒ true | Include the summary block |
| `width` | named | `int` | no | `0` | `0`, or 20–500 | `0` = let the renderer decide. **This tool never queries the terminal itself** |

**Formatting contract preserved from the source (these are pinned behaviours, not suggestions).**

- Token text is escaped for display: newline → `\n`, carriage return → `\r`, tab → `\t`, NUL → `\0`; a null token renders as the literal `(null)` (BR-21). Unlike the source, this escaping is applied on **every** path — the source's plain-text report skipped it even though the helper sat eleven lines above in the same file, so a token containing a newline broke the table (QUIRK-Q22), and the terminal-UI panel escaped only three of the four characters and would fault on a genuinely null token (BR-39).
- Probability text is fixed-point with **2 decimals** plus a percent sign: `0.421234` → `42.12%` (BR-23, on the corrected 0–1 scale). Exactly one `%`; the source produced a doubled `%%` on six call sites (QUIRK-Q3).
- The compact alternatives description lists at most `alts` entries, comma-separated as `{escaped token} ({percentage})`, and appends ` (+ {count − alts} more)` when the list is longer; an empty or null list renders as the literal `(none)` (BR-25).
- Numbering is **1-based on display** for every layout (§6.3, indexing rule).

**Pipeline behaviour.** **Sink-shaped filter.** Inherits `AbstractCommand`; one piped chunk is one document record, and each produces zero or more rendered lines as one `ResultFormat.General` chunk. `analysis` chunks render the header; `token` chunks render a row or a card; `stats` chunks render the summary block; `attribution` chunks render only with `-attribution`. Non-piped it renders `-id`/`TOKEN_LAST`. Because it emits `General`, it is normally the last stage — but nothing prevents `… | TOKEN SHOW | REGIF "very-low"`.

**Environment.** Reads `TOKEN_LAYOUT`, `TOKEN_GRIDMAXALT`, `TOKEN_MODE`, `TOKEN_LAST`. Writes nothing.

**Failure modes.**

| Condition | Result |
|---|---|
| Not piped, no `-id`, `TOKEN_LAST` unset | Failure: `No analysis to show. Pipe one in, or run TOKEN MAP first.` |
| Analysis exists but has zero token records | Succeeds, emitting the single line `No token probability data available.` — the source had two variants of this string differing only in a trailing period (BR-38 vs §B6-7); the rebuild uses one, with the period. **D-001:** when the cause is a capability-absent provider, the line is instead `No token probability data for this message: provider '{provider}' does not supply it.` — and in the full-screen shell this arrives as a **transient status-bar notice, never a modal**; the view attempt is non-blocking in every surface |
| Records carry `synthetic: true` | Every rendered header is prefixed `SAMPLE DATA —` so demonstration output can never be mistaken for a measurement |
| Records carry `estimated: true` | The header notes `(token text estimated; backend has no decoder)` |
| Upstream failure chunk | Forwarded verbatim and rendered by the host as an error, after which `SHOW` continues with later chunks |

**Security and audit.** Renders token text; content-bearing on **output**, not on input. Non-destructive.

**Traceability.** **7.10** and **7.12 Output Rendering**. Descends from `ShowTokenAnalysisCommand` — which in the source was a pure stub that parsed `--top`, `--state` and `--range` into locals and returned a fixed informational message, read no data, rendered nothing, and **was not registered in either shell**, so typing it produced `Unknown command`. It also descends from the console shell's own render path, which was the only working renderer. `--state` has no successor here (model-state snapshots belong to `ChatDbg.Tools.Diagnostics`); `--range` is superseded by `TOKEN FILTER -range`, which is composable and does not need a `-1` sentinel whose meaning the source never defined.

---

#### 6.3.10 `TOKEN EXPORT` — write an analysis to disk

| | |
|---|---|
| **Command** | `EXPORT` |
| **Root** | `TOKEN` |
| **Description** | `Write an analysis to a file in the analysis directory` |
| **Prototype** | `TOKEN EXPORT <name> [-format json\|jsonl\|csv\|md] [-id analysisid] [-redact] [-force]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | no (in-body check) | *(none)* | **Filename stem only**: `[A-Za-z0-9_-]{1,64}`. Not a path — see below | Destination file stem |
| `format` | named | `string` | no | `json` | `json`, `jsonl`, `csv`, `md` | `json` = one document object; `jsonl` = the wire format verbatim; `csv` = one row per token; `md` = the source's plain-text report shape |
| `id` | named | `string` | no | `TOKEN_LAST` | 12-char analysis id | Analysis to export when not piped |
| `redact` | flag | `bool` | n/a | `false` | presence ⇒ true | **NEW** — replace every `text` field with `<t:{tokenId}>` and drop `prompt`/`influencingText`, keeping all numbers |
| `force` | flag | `bool` | n/a | `false` | presence ⇒ true | Overwrite an existing file without confirmation |

**Why the parameter is a name and not a path — this is a hard constraint, not a preference.** The framework's argument tokenizer matches unquoted tokens as `[\w-]+`, so `C:\logs\run.json` arrives as four separate arguments `C`, `logs`, `run`, `json`. Quoting does not save it: the quoted span is then filtered by `InvalidParameterChars`, which strips `\`, `/`, `:`, `=`, `,` and `;` **inside quotes too**, yielding `C:logsrun.json` → `Clogsrun.json`. **A filesystem path cannot survive this framework's command line.** The destination directory therefore comes from `TOKEN_EXPORTDIR`, and the command line supplies only a stem; the extension is chosen by `-format`. This also removes the entire path-traversal surface: the tool joins `TOKEN_EXPORTDIR` with a stem it has already validated against `[A-Za-z0-9_-]{1,64}`, resolves the result to a full path, and verifies component-wise containment (`Path.GetRelativePath` must not start with `..` and must not be rooted) plus symlink resolution before opening the file — the `StartsWith` containment check the loader library itself uses is not sufficient.

**Pipeline behaviour.** **Sink.** Accepts a piped analysis stream (one chunk = one record) and accumulates it; at end of stream it writes one file per analysis id encountered, then emits **one** `ResultFormat.General` confirmation line per file: `Wrote {n} token records to {name}.{ext}`. Non-piped it exports `-id`/`TOKEN_LAST`. It never emits the analysis itself, so it terminates a pipeline cleanly. Multi-analysis input writes `{name}-{analysisId}.{ext}` for the second and subsequent analyses rather than clobbering.

**Environment.** Reads `TOKEN_EXPORTDIR`, `TOKEN_LAST`. Writes `TOKEN_LASTEXPORT` (the resolved filename, so a subsequent `TOKEN LOAD` can be typed without remembering it).

**Failure modes.**

| Condition | Result |
|---|---|
| No `name` | Failure: `Usage: TOKEN EXPORT <name> [-format json\|jsonl\|csv\|md]` — modelled on the source's three-line usage block, minus its final line about needing provider integration |
| `name` fails the stem pattern | Failure: `Export name must be 1-64 characters of letters, digits, '-' or '_'. Directory comes from TOKEN_EXPORTDIR.` |
| `TOKEN_EXPORTDIR` unset, missing, or not writable | Failure naming the directory and the reason. **It does not fall back to the current directory or the temp directory.** The source's settings layer silently fell back to the OS temp directory when the profile path was unavailable, which meant a user could not tell where their data went |
| Target exists and `-force` absent | **Confirmation required.** Not piped: `io.PromptForCommand("{file} exists. Overwrite? (y/n)")`; only `y`/`yes` proceeds. Piped: no prompt is possible, so it fails with `{file} exists. Re-run with -force to overwrite.` |
| Write fails (disk full, permission, path too long) | **Failure**, with the OS message. The source swallowed every export failure into a logged error and told the caller nothing; worse, its `export-analysis` returned **success** with a "Note:" message while creating no file at all, so the shell printed a `✓` for an export that never happened |
| Zero token records | Writes the header-only document and reports `Wrote 0 token records to {file}` — an empty analysis is a legitimate artefact |

**Security and audit.** ⚠️ **This is the only tool that persists content.** An exported analysis contains the prompt and the model's output verbatim; if the user pasted a secret into a prompt, `TOKEN EXPORT` writes it to disk in cleartext. Two mitigations are specified: `-redact` produces a numerically complete but content-free document suitable for sharing, and the confirmation gate above ensures no silent overwrite. **Destructive/irreversible:** yes, when overwriting — hence `-force` and the prompt. The `name` parameter carries no secret and needs no masking; the *file* does, and the host should place `TOKEN_EXPORTDIR` inside the user's private profile.

**Traceability.** **7.10**. Descends from `ExportTokenAnalysisCommand` (a stub that validated nothing, wrote nothing and returned success) and from `LLamaSharpService.SaveAnalysesToJson`, the real, indented-JSON exporter that no command could reach. `-redact`, `-force`, the `csv`/`md`/`jsonl` formats and the containment checks are **NEW**.

---

#### 6.3.11 `TOKEN LOAD` — read an analysis back **NEW**

**Why it earns its place.** Every other tool in this package operates on a document; without a way to get a document back from disk, the entire surface is trapped inside one process lifetime and cannot be tested, compared or shared. `LOAD` is what makes `STATS`, `FILTER`, `SHOW` and `DIFF` runnable with no model, no backend and no network — which is also what makes them unit-testable and what makes this package safe in a restricted host.

| | |
|---|---|
| **Command** | `LOAD` |
| **Root** | `TOKEN` |
| **Description** | `Read a previously exported analysis` |
| **Prototype** | `TOKEN LOAD <name> [-format auto\|json\|jsonl\|csv] [-as alias]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | no (in-body check) | *(none)* | `[A-Za-z0-9_-]{1,64}` stem, resolved under `TOKEN_EXPORTDIR` | File stem to read |
| `format` | named | `string` | no | `auto` | `auto`, `json`, `jsonl`, `csv` | `auto` picks by extension, then by sniffing the first byte |
| `as` | named | `string` | no | *(the file's own id)* | 12-char id pattern | **NEW** — rename the analysis on load, so two files can coexist in the ring for `TOKEN DIFF` |

**Pipeline behaviour.** **Source.** Overrides `Main` to stream the file record by record — one `analysis` chunk then one `token` chunk per record — so a very large analysis never has to be materialised. `ResultFormat.JSON`. Ignores piped input.

**Environment.** Reads `TOKEN_EXPORTDIR`, `TOKEN_LASTEXPORT`, `TOKEN_KEEP`. Writes `TOKEN_LAST`.

**Failure modes.**

| Condition | Result |
|---|---|
| No `name` and `TOKEN_LASTEXPORT` unset | Failure: `Usage: TOKEN LOAD <name>` |
| Stem fails the pattern, or resolves outside `TOKEN_EXPORTDIR` | Failure: `Refusing to read outside the analysis directory.` — the containment check is component-wise plus symlink-resolved, not a `StartsWith` prefix test |
| File not found | Failure: `No analysis named '{name}' in {dir}.` |
| Malformed JSON at line *k* | Failure naming the line number, following the framework's own trust-store precedent of reporting the offending line. Records already emitted are not retracted; the stream ends with the failure chunk |
| `schemaVersion` newer than this build understands | Failure: `Analysis '{name}' uses schema version {v}; this build understands {n}.` **It does not attempt a best-effort parse** |
| `schemaVersion` older | Succeeds with an upgrade shim and a note chunk naming the migration applied |
| Duplicate id already in the ring | The loaded analysis takes the id and evicts the older one, unless `-as` was given |

**Security and audit.** Reads content into memory and onto the pipe. The path containment above is the security boundary; there is no other. Non-destructive.

**Traceability.** **NEW** — the source had no import path for analyses at all (its `/import` handled chat history, a different owner). Sits on the 7.10 / 7.4 boundary and is deliberately placed here because it reads the *analysis* schema, not the *history* schema.

---

#### 6.3.12 `TOKEN DEMO` — offline sample data

| | |
|---|---|
| **Command** | `DEMO` |
| **Root** | `TOKEN` |
| **Description** | `Emit a deterministic sample analysis with no model call` |
| **Prototype** | `TOKEN DEMO [-topk 1-20] [-seed s] [-tokens n]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `topk` | named | `int` | no | `5` (`TOKEN_TOPK`) | 1–20 | Alternatives per token |
| `seed` | named | `long` | no | `42` | any | Generator seed — the source's constant (BR-14) |
| `tokens` | named | `int` | no | `25` | 1–500 | How many sample tokens to emit; `25` reproduces the source's fixed sentence exactly |

**Preserved sample data (BR-13 – BR-20).** With default parameters the fixed sentence *"This is a sample response with token probability analysis. You can see how the model assigned probabilities to each token and what alternatives it considered."* yields exactly **25** tokens, in order: `This, is, a, sample, response, with, token, probability, analysis, You, can, see, how, the, model, assigned, probabilities, to, each, token, and, what, alternatives, it, considered` — splitting on space, newline, tab, `.`, `,`, `!`, `?` and discarding empties. The hard-coded plausible alternatives are preserved verbatim (`sample` → `example`, `test`, `demo`; `response` → `reply`, `answer`, `output`; `token` → `word`, `symbol`, `element`; `probability` → `likelihood`, `chance`, `confidence`; `analysis` → `evaluation`, `assessment`, `examination`; anything else → `{token}_1/_2/_3`), as is the filler rule that pads to top-K with `alt_{0..999}` entries and the truncation to the first `min(topK, count)` alternatives in generation order. The simulated elapsed time is **0.5 seconds**.

**Three deliberate corrections.**
1. **Values are stored as real log-probabilities.** The source wrote numbers in the range **[70, 98)** into the *log-probability* field, which display code then exponentiated — rendering "probabilities" of e⁷⁰ ≈ 2.5×10³⁰ and putting every token in the top colour band (QUIRK-Q2). Those numbers were plainly meant as percentages. `TOKEN DEMO` stores `ln(percent/100)`, so the derived probability lands in [0.70, 0.98) exactly as intended and the confidence bands finally distribute.
2. **Determinism is portable.** The source seeded the platform RNG with 42, which is reproducible within one runtime and not across a port. `TOKEN DEMO` uses a specified deterministic generator (a documented 64-bit xorshift), so the same seed yields the same bytes on every OS, runtime and architecture — a property the source's own dossier flagged as the actual requirement.
3. **Everything is stamped `synthetic: true`** on both the `analysis` record and every `token` record, and `TOKEN SHOW` prefixes the header `SAMPLE DATA —`. The source's demonstration data was indistinguishable from measurement once it left the command.

**Pipeline behaviour.** **Source only.** Overrides `Main`; emits one `analysis` chunk and `tokens` `token` chunks. `ResultFormat.JSON`. Ignores piped input.

**Environment.** Reads `TOKEN_TOPK`, `TOKEN_KEEP`. Writes `TOKEN_LAST`.

**Failure modes.** `-topk` or `-tokens` out of range fails with the range in the message. It cannot fail for any environmental reason: no model, no file, no network, no backend. **That is its whole purpose** — the source's demonstration mode existed precisely to avoid paid API calls, and it is the tool that proves the rest of the package works in a restricted host.

**Security and audit.** No secret, no user content, no filesystem, no network. Non-destructive. The single safest tool in the package and the right first thing to run in a new host.

**Traceability.** **7.9**. Descends from `/demologprobs` (`DemoLogProbsCommand`). One structural change: the source's demo command rendered the visualization itself when a formatter had been injected and rendered nothing when one had not — and *neither shell wired it up correctly*, so on the plain console it printed only `✓ Sample token probability analysis generated` and under the terminal UI it wrote ANSI escapes beneath a full-screen application that owned the screen (QUIRK-Q7). `TOKEN DEMO` renders nothing at all; it emits a document, and `TOKEN SHOW` renders it. The README's promise that the command "generates sample data to show how the visualization works" is honoured by the composition `TOKEN DEMO | TOKEN SHOW`.

---

#### 6.3.13 `TOKEN DIFF` — compare two analyses **NEW**

**Why it earns its place.** The source's stated purposes include "tuning prompts for better results" and "debugging unexpected outputs" — both are *comparative*: the same prompt at two temperatures, before and after a system-prompt edit, one model against another. The source could not express the comparison at all; a user had to read two grids side by side. This is the smallest tool that answers "what changed", and like `STATS` it is pure arithmetic over documents, so it needs no backend.

| | |
|---|---|
| **Command** | `DIFF` |
| **Root** | `TOKEN` |
| **Description** | `Compare two analyses token by token` |
| **Prototype** | `TOKEN DIFF <baseline> [-against analysisid] [-align text\|index] [-minshift d] [-format json\|text]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `baseline` | ordered | `string` | no (in-body check) | *(none)* | 12-char analysis id, or a `TOKEN LOAD` alias | The analysis to compare against |
| `against` | named | `string` | no | piped stream, else `TOKEN_LAST` | 12-char analysis id | The other analysis |
| `align` | named | `string` | no | `text` | `text`, `index` | `text` aligns on token text with a longest-common-subsequence pass so insertions do not shift everything; `index` compares position for position |
| `minshift` | named | `double` | no | `0.05` | 0.0–1.0 | Only report positions whose probability moved by at least this much |
| `format` | named | `string` | no | `json` | `json`, `text` | Output shape |

**Pipeline behaviour.** **Filter.** The piped stream is the "against" side; `baseline` names the other. Emits one `diff` record per aligned position (`kind`, `baselineIndex`, `againstIndex`, `text`, `baselineProbability`, `againstProbability`, `shift`, `bandChange`, `alternativesGained[]`, `alternativesLost[]`) plus one trailing `diffsummary` record with mean absolute shift, perplexity delta and band migration counts. `ResultFormat.JSON`, or `General` with `-format text`. Non-piped it compares `baseline` against `-against`/`TOKEN_LAST`.

**Environment.** Reads `TOKEN_LAST`. Writes nothing.

**Failure modes.** A missing side fails with `Analysis '{id}' not found. Load it with TOKEN LOAD, or run it first.` Comparing an analysis with itself succeeds and reports all-zero shifts. Comparing a `synthetic: true` analysis against a live one succeeds but stamps `syntheticInvolved: true` on the summary. Analyses with different `topK` are comparable for probabilities but not for entropy; the summary carries `entropyComparable: false` rather than silently comparing incomparable numbers.

**Security and audit.** Content-bearing on output (it prints token text). `-format json` plus a downstream `TOKEN STATS`-style numeric projection is the shareable form. Non-destructive.

**Traceability.** **7.9**. **NEW** — no ancestor of any kind.

---

### 6.4 Pipeline compositions

**1. Find where the model was guessing, in one line.**

```
TOKEN MAP "explain the CAP theorem" -n 60 -topk 10 | TOKEN FILTER -below 0.5 -mode all | TOKEN SHOW -alts 3
```

`MAP` streams 60 token records as they are generated; `FILTER` drops every record at or above 50 % confidence, returning empty-success for them so the host silently discards them; `SHOW` prints the survivors with their three best alternatives. The user sees only the uncertain spans — with absolute 1-based indices, so they can be located in the full output — and sees them appearing while generation is still running, because every stage runs concurrently and the channels are pulled lazily.

**2. Summarise a run without ever showing its content.**

```
TOKEN LOAD run42 | TOKEN STATS -format csv -threshold 0.3
```

`LOAD` streams the file from `TOKEN_EXPORTDIR`; `STATS` passes every record through, accumulates, and appends one CSV row: `analysisId,n,meanProbability,medianProbability,minProbability,maxProbability,meanNegLogLikelihood,perplexity,meanEntropy,meanNormalizedEntropy,meanMargin,veryHigh,high,medium,low,veryLow,lowConfidenceCount,lowConfidenceRatio`. The `stats` record contains **no token text**, so this is the artefact to paste into an issue. Runs with no model, no network and no backend.

**3. See the visualization the source promised but never delivered.**

```
TOKEN DEMO -topk 5 | TOKEN STATS -perstep | TOKEN SHOW -layout grid -maxalt 5
```

`DEMO` emits the 25-token sample set with real log-probabilities; `STATS -perstep` enriches each record with entropy, normalized entropy and margin and appends a summary; `SHOW` draws the grid, prefixing the header `SAMPLE DATA —`. This is the smoke test for a fresh host: it exercises the document schema, the statistics layer and the renderer with no model, no file and no credential, and it is the README's promise finally honoured.

**4. Archive an analysis without archiving the prompt.**

```
TOKEN MAP "review this contract clause: ..." -n 200 | TOKEN ATTRIBUTE -window 8 | TOKEN EXPORT clause-review -format jsonl -redact
```

`MAP` streams; `ATTRIBUTE` interleaves an attribution record after each token; `EXPORT` accumulates and writes `{TOKEN_EXPORTDIR}/clause-review.jsonl` with every `text`, `prompt` and `influencingText` field replaced by `<t:{tokenId}>` placeholders, keeping every number intact. Then one confirmation line. If `clause-review.jsonl` already exists the tool prompts (or fails with a `-force` hint when piped), so an archive is never silently clobbered.

**5. Cross-package: chat, capture, summarise, draw.** *(crosses into `ChatDbg.Tools.Providers` and `ChatDbg.Tools.Rendering`)*

```
AI ASK "why did the deploy fail" -capture | TOKEN STATS -perstep | TOKEN FILTER -band low -mode all | RENDER HEATMAP -scale 0-1
```

`AI ASK -capture` (Providers, 7.6/7.8) performs the turn and emits the assistant text plus the analysis document, honouring `TOKEN_CAPTURE` and `TOKEN_TOPK` from this package's environment bucket via the global settings it shares. `TOKEN STATS -perstep` adds per-token entropy and margin. `TOKEN FILTER -band low` narrows to the `low` confidence band. `RENDER HEATMAP` (Rendering, 7.12) paints the survivors — reading the `band` field this package computed and mapping it to the source's green/lime/yellow/orange/red palette. Four packages' worth of concern, one line, and each stage is independently testable because the boundary between them is a documented record, not a shared object.

**6. Cross-package: make a capture policy durable.**

```
TOKEN CAPTURE on -topk 10 -mode all -print | CONFIG SET -section token
```

`TOKEN CAPTURE` applies the policy to its own environment bucket and, because of `-print`, also emits it as `key=value` lines. `CONFIG SET` (Settings, 7.2) is the only command in the product registered with `modifiesEnvironment: true` and the only writer of `~/.ChatDbg/settings.json`, so it takes those lines and persists them. This replaces the source's arrangement in which `/logprobs` wrote the whole settings document itself on every mutation — and in which three of the five keys it wrote were never read back on the next start of the console shell (QUIRK-Q5), so display preferences silently reverted at every restart.

**7. Compare two prompts with a framework built-in in the chain.**

```
TOKEN LOAD baseline -as base | TOKEN DIFF base -against last -format text | REGIF "band-drop"
```

`LOAD` re-reads a saved run under a stable alias; `DIFF` aligns it against the most recent analysis by token text and emits one line per shifted position; `REGIF` (a framework built-in, needing no code from us) keeps only the lines reporting a band drop. Nothing in this pipeline touches a model.

---

### 6.5 Design notes for the architect

**What state this package holds.** Exactly two things. (1) A **bounded, process-scoped analysis ring** — the last `TOKEN_KEEP` analyses (default 8), keyed by a 12-character id, behind an `IAnalysisStore` interface with an in-memory default the host may replace. It must be process-scoped and not instance-scoped: `CommandFactory` constructs a **new command instance per execution**, so nothing survives on `this` between two `TOKEN` invocations. (2) **Policy in the environment**, under `TOKEN_*` keys in the package's own bucket, which the host round-trips for free. That is all. The source held one mutable "last generation" list that was cleared at the start of every generation, so the answer to "show me the previous run" was always "gone"; a small ring with explicit ids costs nothing and makes `DIFF` possible.

**What it must not hold.** No credential, endpoint, region, or key — ever, in any field, on any path, in any log line. No conversation: an analysis references a message id and never embeds the chat history. No provider client, HTTP handler, socket, or native model handle: those are borrowed through `IProbabilitySource` for the duration of one call and never cached here. No `System.Diagnostics.Trace`-only error swallowing: the source's diagnostic channel absorbed a whole class of failures invisibly (the logit accessor returning an empty candidate list from a bare `catch` with no log line, so the feature reported total confidence in everything — QUIRK-Q14). Every failure in this package either becomes a `Failure` chunk the user sees or an explicit note record; nothing is written only to the trace.

**How it stays testable.** Three deliberate properties. First, **every tool except `SPLIT`, `MAP` and `INSPECT` is a pure function over documents** — no model, no network, no clock beyond `createdUtc`, no filesystem except `LOAD`/`EXPORT`. `STATS`, `FILTER`, `SHOW`, `DIFF` and `DEMO` are testable with a string fixture and an assertion. Second, the three impure tools depend on `ITokenizer` / `IProbabilitySource` / `IAttributionModel` interfaces resolved through `IntrospectionServices`, which is a settable static registry — a test substitutes a fake and never loads a model. This is the direct answer to the source's own recorded verdict: *"the inspection service is `static`, so no seam exists for substituting a tokenizer; consequently the only tests are the two file-not-found guards."* Third, **`TOKEN DEMO` is the integration fixture**: its output is deterministic across platforms by construction, so `TOKEN DEMO | TOKEN STATS` has a byte-exact expected value, and the whole pipeline machinery is covered without a model. Add one hermetic suite (documents and fakes) and one backend suite (a small real GGUF, opt-in, skipped by default) — the same split the reference host uses for its offline and network tests.

**Instance state and the DI trap.** `CommandFactory` prefers `serviceProvider.GetService(commandType)` when the type resolves in the default context. If a host registers these tools as **singletons**, the same instance is reused across executions *and across pipeline stages* — which would corrupt `FILTER`'s sampling buffer and `ATTRIBUTE`'s look-back window. Two defences, both required: every per-pipe field is reset in `OnStartPipe` rather than in the constructor, and the package's registration guidance says **transient**. When loaded as a plugin from a package directory this cannot arise (activation is per-execution through the ALC), but a host that project-references the assembly and wires it through DI can hit it.

**Concurrency.** The framework starts **all pipeline stages concurrently** and the stages communicate only through bounded channels, so the tools need no locks among themselves. The one shared mutable is the analysis ring, which is a concurrent dictionary with a bounded eviction policy. Model access is serialised inside `IProbabilitySource`, not here — the source's `/tokenize` and `/inspect` bypassed the chat path's process-wide generation semaphores and constructed their own model instances, so a command issued during a chat turn attempted a concurrent native load. Delegating removes that risk rather than duplicating the gate.

**Cancellation.** The source had none anywhere: a long generation could not be aborted. `IProbabilitySource` takes a `CancellationToken`, and `MAP`/`INSPECT` honour the pipeline's `StageTimeoutSeconds` — which is the only `PipelineConfiguration` timeout the framework actually reads (`ExecutionTimeoutSeconds`, `MaxStageOutputBytes` and `MaxStageOutputItems` are declared and never enforced). A host that wants a wall-clock cap on generation must set `StageTimeoutSeconds`; on expiry the framework completes the stage with a **status message**, not an output chunk, so the host's IO context must surface status for the user to see why the stream stopped.

**When a capability is unavailable — degrade, or refuse, but never invent.** The rule is uniform and it is the single most important correction in this chapter.

| Missing capability | Behaviour |
|---|---|
| No local backend at all (hosted-only, or restricted host) | `DEMO`, `LOAD`, `FILTER`, `STATS`, `SHOW`, `EXPORT`, `DIFF`, `CAPTURE`, `DIAG` all work fully. `SPLIT`, `MAP`, `INSPECT` fail with a message naming the missing capability and pointing at `TOKEN DEMO`. **Degrade the surface, not the truth.** |
| Provider generates but returns no probabilities | The analysis is produced with `token` records carrying `logprob: null`, plus the source's exact two-line note. **No synthetic substitution** — the source's cloud adapter fabricated a 15-token list at `ln(0.9)` with three hard-coded alternatives and returned it as if measured, a behaviour its own test suite pinned |
| Backend decodes token IDs but exposes no text | Records carry `<token_{id}>` **and** `estimated: true`; `SHOW` says so in the header. The source printed the same placeholder with no marker and its README claimed real decoded strings |
| Backend exposes no character offsets | `charStart = charEnd = -1`. The source computed a uniform-distribution estimate that produced contiguous, plausible-looking spans bearing no relation to real boundaries |
| Only top-K alternatives available (always, for real providers) | `truncatedTopK: true` and `entropyIsTruncated: true`; entropy is documented as a lower bound |
| Attribution method unavailable | Refuse, naming the available methods. Never silently downgrade `attention` to `recency` |
| Terminal is not attached, or output is redirected | Irrelevant — **no tool in this package queries the console**. The source derived grid column count from `Console.WindowWidth` with no guard around the query itself, so a redirected session could surface as a generic `Error getting AI response`. Width is a `SHOW` parameter with `0` meaning "renderer decides" |

**Cross-platform posture.** Nothing in this package is gated on an operating system — that was already true of the source feature, and it stays true. Four environmental couplings are handled explicitly. *Analysis directory:* `TOKEN_EXPORTDIR` defaults to `%APPDATA%\ChatDbg\analyses` on Windows and to `$XDG_DATA_HOME/ChatDbg/analyses` (falling back to `~/.local/share/ChatDbg/analyses`) on Linux and macOS, because the .NET application-data special folder can resolve to an empty string on some Unix configurations; if the resolved directory cannot be created, `EXPORT`/`LOAD` **fail loudly** rather than silently redirecting to the temp directory as the source's settings layer did. *Native backends:* not this package's problem by construction — CPU-versus-CUDA is entirely inside `IProbabilitySource`. *Trimming and AOT:* the package is pure managed with no reflection over external types, so it survives the aggressive publish configurations that broke the source's late-bound logit lookup. *Culture:* every number written into a document uses the invariant culture and every number parsed from one is parsed with it — the source formatted with the ambient culture, so a comma-decimal locale produced documents that a period-decimal locale could not read back, and the source's own `Compact`/`SingleFile` publish profiles enabled invariant globalization, meaning **the same build produced different output depending on how it was published**.

**Where it should degrade rather than fail.** A zero-token analysis is a success, not an error. A `stats` record over an empty set reports `n: 0` with null aggregates. An analysis whose records all lack probabilities still renders its text. A record with a corrupt log-probability is excluded and counted, not fatal. An unknown chunk kind is passed through, not rejected. An older schema version is migrated with a note. Conversely, it should **fail** — loudly and specifically — when asked to write outside its directory, when asked to overwrite without permission, when asked for an attribution method the backend cannot provide, when the trust boundary of a path check is ambiguous, and whenever the only alternative is to present a number the model did not produce.

**One number, one place.** Every threshold in this chapter is either a source constant preserved verbatim (top-K 1–20 default 5; grid alternatives 1–20 default 5; sample size 5; sample floor 15; look-back 5; prompt tail 50; attribution cut-over 3; generation budget 10; band boundaries 0.90/0.70/0.50/0.30; demo seed 42; demo elapsed 0.5 s; demo token count 25) or a named environment key with a stated default. The source had the same constants spread across four unreachable copies of the same sampler and two same-named formatting helpers with contradictory contracts, so the identical token rendered as `92.00%` through one path and `9200.00000%%` through the other. There is one sampler, one formatter, one scale and one indexing rule in this package, and every one of them is a parameter.
