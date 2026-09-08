## 2. ChatDbg.Tools.ConfigurationProfiles — Configuration & Profiles

### 2.1 Purpose and boundary

**What this package owns.** Every tunable that decides *which back end, which model, how creative, how long, where, and with what back-end-specific tuning* a ChatDbg session runs — and the document those tunables live in. Concretely:

- The **settings record**: the 21 persisted fields of the source product's `ChatSettings` (plus the deliberate additions listed in §2.3), their exact defaults, their exact inclusive ranges, their exact wire names.
- The **settings document**: where it resolves to (`~/.ChatDbg/settings.json`, with a temp-directory fallback), how it is loaded, how a corrupt document degrades, how it is validated, how it is written, and how it is reset.
- The **write surface**: viewing, reading one key, writing one key, clearing one key, restoring defaults, importing and exporting whole documents, and validating a candidate document before it is adopted.
- **Named configuration profiles** — a complete set of records that can be listed, switched, captured, dropped and diffed. This is entirely new (§2.1.3).
- The **publication of effective configuration** into the host's controller environment under `CHATDBG_*` keys, so that every other tool package reads configuration from one authoritative place instead of sharing a mutable object (§2.6.1).

**What this package explicitly does NOT own.**

| Not owned | Owned by | Boundary rule |
|---|---|---|
| Secret **values** — resolving, storing, reading, migrating, or the OS keystore itself | `ChatDbg.Tools.Credentials` (root `CRED`) | This package owns the *toggle* (`useOsCredentialStore`) and the three deprecated in-document secret slots it is trying to retire. It asks `CRED` two questions only: "is a credential store available on this platform?" and "where does secret *X* resolve from?" It never sees, prints, pipes or persists a secret value. The source's `/set wincred`, `/set enablewincred`, `/set migrate` and the three blocked keys `azureApiKey` / `awsAccessKey` / `awsSecretKey` all move to `CRED`; `SET VALUE` refuses those keys and names the `CRED` tool that replaces each one. |
| The **meaning** of a system prompt: its body, its store, create/edit/delete | `ChatDbg.Tools.Prompts` (root `PROMPT`) | This package persists the prompt **name** only, and calls `PROMPT` to validate that the name resolves. The prompt *body* is never persisted (source parity). |
| What a provider **does** with `temperature`, `maxTokens`, `azureEndpoint`, `azureApiVersion`, `awsRegion`, the GGUF file | `ChatDbg.Tools.Providers` (root `AI`) | This package validates and stores; `AI` decides whether a provider "is configured", builds requests, and reports capability. `SET VALUE provider llama` does not load a model. |
| Token-probability **analysis and rendering** semantics | `ChatDbg.Tools.TokenProbability` (root `LOGPROB`) and `ChatDbg.Tools.Rendering` (root `RENDER`) | The five display fields (`enableLogProbabilities`, `logProbabilitiesTopK`, `showAllTokens`, `gridViewForTokens`, `gridViewMaxAlternatives`) are *stored* here and are settable through `SET VALUE`; the source's convenience surface `/logprobs enable|top|showall|grid|list|gridmaxalt` is re-homed on `LOGPROB`, which writes through this package's settings-store port. Both surfaces share one validator, so the source's split-brain wording (`Top-K value must be…` vs `LogProbabilitiesTopK must be…`) disappears. |
| Chat history, transcripts, injection/pop/clear | `ChatDbg.Tools.History` (root `CHAT`) | No overlap. |
| Diagnostic log capture and export | `ChatDbg.Tools.Diagnostics` (root `LOG`) | `SET` emits no log files; it emits audit events like every other tool. |
| Shell prompt, exit words, package directory, theme | The host (`ChatDbg.Shell.Core`) | Host personality is host configuration, not product configuration. `SET` never rewrites the loop. |

#### 2.1.1 The `SET` name collision — resolved, not ignored

`Xcaciv.Command` ships a built-in top-level command named `SET` (`Xcaciv.Command/Commands/SetCommand.cs`, registered under package key `Default` with `modifiesEnvironment: true`) whose contract is `SET <varname> <value>` writing a **shell environment variable**. This package claims `SET` as a `[CommandRoot]`, which the registry resolves by replacement in registration order.

The decision: **the host registers built-ins first and this package second, so `SET` becomes a root**, and this package ships `SET ENV` (§2.4.16) reproducing the built-in's behaviour byte-for-byte — including `UsePipe` on the value and the `OnStartPipe` clear-then-append accumulation idiom. Nothing is lost, and the product's most-typed verb keeps the meaning its users expect. The host must not call `RegisterBuiltInCommands()` *after* loading packages, and the packaging check in §2.6.8 asserts that ordering.

#### 2.1.2 Fidelity posture

Every default, bound and message in §2.3 is carried verbatim from the source unless a row is marked **DEVIATION**, in which case the reason is stated inline. There are eleven deliberate deviations and no accidental ones.

#### 2.1.3 Why profiles exist at all

The source explicitly provides none: *"No profiles, no per-workspace settings, no config-file layering, no `--config` override at runtime."* But the product's whole reason for having ~24 tunables is that a user moves between an Azure deployment, a Bedrock model and a local GGUF rig, each with a different endpoint, model id, context size and GPU layer count. In the source, switching rigs means six to nine `/set` commands typed from memory, every one of which overwrites the single document — and there is no way back. Profiles are the smallest addition that makes the existing tunables usable: a named, complete, validated snapshot with `SET PROFILE <name>` to switch and `SET DIFF` to see what changed. They are additive — a user who never types `PROFILE` sees the source's exact one-document behaviour, because the document `~/.ChatDbg/settings.json` *is* the profile named `default`.

---

### 2.2 Package manifest

| Property | Value |
|---|---|
| Assembly / package id | `Xcaciv.ChatDbg.Tools.ConfigurationProfiles` |
| Root command | `SET` (claimed as `[CommandRoot("SET", "Configuration, tunables and profiles")]`; see §2.1.1) |
| Sub-commands | 16 (§2.4) |
| Contract assemblies targeted | `Xcaciv.Command.Interface` **3.3.4** and `Xcaciv.Command.Core` **3.3.4** — and nothing else from the framework. No reference to `Xcaciv.Command` (the host), per the Cupcake rule "tool → SDK, never tool → host". |
| Target framework | `net10.0` (`LangVersion 14`, `ImplicitUsings`, `Nullable` enabled), single TFM across the graph |
| Third-party dependencies | `System.Text.Json` (in-box). `YamlDotNet` **16.3.0** only if `-format yaml` ships in v1; otherwise none. **No** `System.IO.Abstractions` in the package itself — filesystem access is behind this package's own `ISettingsStore` port (§2.6.3). |
| Elevated trust required | **No.** No P/Invoke, no native library, no registry, no service control, no admin-only path. |
| Filesystem reach | Read/write exactly two directory subtrees: the settings base directory (default `<user profile>/.ChatDbg/`, override via `SET_BASEDIR`) and its `profiles/` child. Plus **read-only, existence-check only** access to an arbitrary path when validating `modelId` under the `llama` provider. It never writes outside the base directory and never deletes anything but a profile document. |
| Network reach | **None.** This package makes no outbound connection of any kind. Endpoint and region are strings it validates and stores; it never dials them. |
| OS keystore reach | **None directly.** It calls `ChatDbg.Tools.Credentials` for the availability predicate and for provenance strings. |
| Native libraries | None. |
| Process environment | Reads five credential variable **names** for provenance reporting (never their values into output), and three optional startup overrides (§2.4 per-tool tables). Writes none of the process environment; writes only the framework's `IEnvironmentContext`. |
| Environment-modifying permission | **Required for 6 of 16 tools.** `SET VALUE`, `SET UNSET`, `SET RESET`, `SET IMPORT`, `SET PROFILE` and `SET ENV` must be registered with `modifiesEnvironment: true` because they publish `CHATDBG_*` globals other packages read. The other ten write only their own `SET_`-prefixed bucket and need no elevation. |
| Safe in a restricted host | **Yes, with one caveat.** Loadable under `AssemblySecurityPolicy` with `DisallowDynamicAssemblies = true`; it emits no dynamic code and reflects only over its own attributes (which `AbstractCommand` does anyway). The caveat: in a host whose settings base directory is not writable, the package must be loaded with `SET_READONLY=true`, in which case all twelve mutating tools degrade to a single explanatory failure and the six read tools continue to work (§2.6.6). |
| Audit posture | Every tool is audit-logged once per execution by `CommandExecutor`. Three tools carry parameters that can hold a path; none carries a secret. The package supplies its own `IAuditMaskingConfiguration` additions (§2.6.7) because the framework's masking only rewrites `-name=value` tokens and is effectively non-functional for `-name value` syntax. |

---

### 2.3 The settings key catalog (normative)

Every tool in this package refers back to this one table rather than restating ranges. It is also the table `SET KEYS` (§2.4.9) emits at runtime, so the help text, the validator, the GUI and the machine-readable schema can never drift apart — the source had three copies of this list and all three disagreed (source quirk Q7: the "unknown setting" error named 13 of ~26 keys).

Key matching is **case-insensitive**; the canonical form below is what is echoed and emitted.

| Key (canonical) | Aliases accepted | Wire name in the document | Type | Default | Valid range / values | Violation message (verbatim from source unless noted) |
|---|---|---|---|---|---|---|
| `provider` | — | `provider` | enum text | `azure` | `azure`, `bedrock`, `llama`; stored lowercase | `Provider must be 'azure', 'bedrock', or 'llama'` |
| `modelId` | `model` | `modelId` | free text | `gpt-4` | any text; **when the active provider is exactly `llama` and the value is non-empty, the value must name an existing file** | `LLama model file not found: <path>` + newline + `Make sure you've specified the correct path to a GGUF model file.` |
| `temperature` | — | `temperature` | decimal | `0.7` | **0.0 … 2.0 inclusive** | `Temperature must be a number between 0 and 2` |
| `maxTokens` | — | `maxTokens` | integer | `1000` | **1 … 8192 inclusive** | `MaxTokens must be a number between 1 and 8192` |
| `azureEndpoint` | — | `azureEndpoint` | free text, nullable | *(null)* | source enforces nothing. **DEVIATION:** a *warning-only* URL shape check is emitted (`Warning: '<v>' does not look like an https:// endpoint.`) and the value is still stored, preserving "accept anything". One trailing `/` is trimmed on read by the provider package, as in source. | — (warning only) |
| `azureApiVersion` | — | `azureApiVersion` | free text | `2023-12-01-preview` | non-empty; shape `yyyy-MM-dd[-preview]` warned, not enforced | `azureApiVersion must not be empty` — **NEW / DEVIATION**: the source hard-codes this in the request URL while `/logprobs debug` tells the user to "use a recent API version", advice they cannot act on (source quirk Q33). Making it a real key is the fix. |
| `awsRegion` | — | `awsRegion` | free text | `us-east-1` | no validation (source parity — any string is a region) | — |
| `systemPrompt` | `systemPromptName` | `systemPromptName` | free text | `default` | when `ChatDbg.Tools.Prompts` is reachable, the name must resolve; when it is not, the name is stored unvalidated (source parity) | `System prompt not found: <name>. Use '/prompt list' to see available prompts or '/prompt create <name>' to create a new one.` — **DEVIATION:** re-worded to the rebuilt verbs: `System prompt not found: <name>. Use 'PROMPT LIST' to see available prompts or 'PROMPT NEW <name>' to create one.` |
| `enableLogProbabilities` | `logprobs` | `enableLogProbabilities` | boolean | `false` | `true` / `false`. **DEVIATION:** the framework converter also accepts `1/0/yes/no/on/off`; the source accepted only `true`/`false`. The broader set is adopted deliberately — it is the framework's documented boolean grammar and rejecting `yes` inside a framework that accepts it everywhere else is a worse surprise than the widened grammar. | `enableLogProbabilities must be 'true' or 'false'` |
| `logProbabilitiesTopK` | `logtopk`, `topk` | `logProbabilitiesTopK` | integer | `5` | **1 … 20 inclusive** | `logProbabilitiesTopK must be a number between 1 and 20` |
| `showAllTokens` | — | `showAllTokens` | boolean | `false` | as above | `showAllTokens must be 'true' or 'false'` |
| `gridViewForTokens` | `tokensgrid` | `gridViewForTokens` | boolean | `false` | as above | `gridViewForTokens must be 'true' or 'false'` |
| `gridViewMaxAlternatives` | `gridmaxalt` | `gridViewMaxAlternatives` | integer | `5` | **1 … 20 inclusive** | `gridViewMaxAlternatives must be a number between 1 and 20` |
| `useOsCredentialStore` | `useWindowsCredentialManager`, `wincred` | `useWindowsCredentialManager` | boolean | `false` | `true` only when `CRED` reports a store is available on this OS | `useOsCredentialStore must be 'true' or 'false'` / `No OS credential store is available on this platform (<os>).` — **DEVIATION:** the key is renamed because the capability is no longer Windows-only (§2.6.5); the **wire name is unchanged** so existing documents keep loading, and the old name stays as an alias forever. |
| `llamaContextSize` | — | `llamaContextSize` | integer | `4096` | **512 … 32768 inclusive** | `llamaContextSize must be a number between 512 and 32768` |
| `llamaGpuLayerCount` | `llamaGpuLayers` | `llamaGpuLayerCount` | integer | `0` | **0 … 100 inclusive**; 0 = CPU-only | `llamaGpuLayerCount must be a number between 0 and 100. 0 means CPU-only, higher numbers move more layers to GPU.` |
| `llamaGpuDevice` | — | `llamaGpuDevice` | free text, nullable | *(null)* | no validation; conventionally `0` or `0,1` | — |
| `llamaThreads` | — | `llamaThreads` | integer | `0` | **0 … 64 inclusive**; 0 = system default | `llamaThreads must be a number between 0 and 64. 0 means system default.` |
| `llamaBatchSize` | — | `llamaBatchSize` | integer | `512` | **1 … 2048 inclusive** | `llamaBatchSize must be a number between 1 and 2048` |
| `azureApiKey` | — | `azureApiKey` | free text | `""` | **write-blocked.** Always emitted to the document (empty when unset), source parity. | `For security, Azure API Key is no longer set via this command.` + the secure-options guide, now naming `CRED PUT azure` and `CHATDBG_AZURE_API_KEY` |
| `awsAccessKey` | — | `awsAccessKey` | free text | `""` | write-blocked, always emitted | as above, naming `CHATDBG_AWS_ACCESS_KEY` and `AWS_ACCESS_KEY_ID` |
| `awsSecretKey` | — | `awsSecretKey` | free text | `""` | write-blocked, always emitted | as above, naming `CHATDBG_AWS_SECRET_KEY` and `AWS_SECRET_ACCESS_KEY` |
| `profile` | — | `activeProfile` | free text | `default` | must name an existing profile; set by `SET PROFILE`, **read-only through `SET VALUE`** | `Use 'SET PROFILE <name>' to change the active profile.` — **NEW** |

**Range semantics are inclusive on both ends, everywhere** — `0` and `2` are both valid temperatures, `1` and `8192` are both valid token caps, `1` and `20` are both valid Top-K values, `512` and `32768` are both valid context sizes, `0` and `100` are both valid GPU-layer counts, `0` and `64` are both valid thread counts, `1` and `2048` are both valid batch sizes. This is source behaviour and is preserved exactly.

**Three keys the source persists but never consumes** — `llamaGpuDevice`, `llamaThreads`, `llamaBatchSize` (source quirk Q6). They remain settable, validated, persisted and displayed here, because removing them would break existing documents; `SET SHOW` and `SET KEYS` mark them `(not yet consumed by the local runtime)` so the user is not misled the way the source's README misled them.

#### 2.3.1 The document

- Location: `<base>/settings.json`, where `<base>` is, in order: the `SET_BASEDIR` environment value if set; else `CHATDBG_HOME` if set; else `<user profile>/.ChatDbg`; else — if the profile lookup is empty, whitespace or throws — the OS temp directory. (Source parity, with the two environment overrides marked **NEW**.)
- Profiles live at `<base>/profiles/<name>.json`. The active profile's content is mirrored into `<base>/settings.json` on every switch, so a v1 document and a profile-aware document are the same file shape and older builds keep working.
- Format: **indented** JSON with the exact lower-camel wire names above (the file is meant to be hand-edited). The three deprecated secret slots are always written, empty when unset. Computed/derived members and the system-prompt **body** are never written.
- **DEVIATION — culture.** Every number in the document and every number accepted on the command line is parsed and formatted with **invariant culture**, unconditionally, in every build configuration. The source used ambient culture, and forced invariant globalization only in its `Compact` and `SingleFile` builds — so the same `SET VALUE temperature 0.7` was accepted by the shipped binary and rejected by a debug build on a comma-decimal locale, and a document written by one could not be read by the other (source quirk Q15). Packaging must never change input validation.
- **DEVIATION — atomicity.** Writes are temp-file-plus-rename inside the base directory, not whole-file overwrite in place. The source had no lock, no atomic replace and no backup, so two shells clobbered each other silently. Last-writer-still-wins, but a crashed or racing writer can no longer leave a truncated document.

---

### 2.4 Tool catalog

All sixteen classes carry `[CommandRoot("SET", "Configuration, tunables and profiles")]` and are invoked as `SET <sub-command> …`. All parameter attributes sit **on the class** (`AttributeTargets.Class, AllowMultiple = true`), never on properties or fields; injected fields are `public` **instance fields**, never properties.

Two framework facts shape every tool below and are not repeated per tool:

1. **Zero arguments means zero parameter processing.** `AbstractCommand.ProcessParameters` returns an empty dictionary when `io.Parameters.Length == 0` — no defaults applied, no flags materialised, no field injection. Every tool therefore states its bare-invocation behaviour explicitly and codes `parameters.TryGetValue(...) && p.IsValid ? … : <literal default>` for every read.
2. **Parse-time exceptions are invisible to the user.** An `ArgumentException` from a missing required parameter or an `AllowedValues` violation is caught by `CommandExecutor` and reduced to `Error executing SET (see trace for more info)`. So this package declares `AllowedValues` **only** where a generic error is acceptable (low-cardinality display options), and validates high-value inputs — setting keys, numeric ranges, profile names — inside `HandleExecution`, returning `CommandResult<string>.Failure(<the exact message from §2.3>)`.

---

#### 2.4.1 `SET SHOW` — render the current configuration

| | |
|---|---|
| Command | `SHOW` |
| Root command | `SET` |
| Description | Show the effective configuration, or one section of it, in human or machine form |
| Usage prototype | `SET SHOW [<section>] [-format text\|json\|yaml\|csv] [-profile <name>] [-defaults] [-changed]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Show", "Show the effective configuration",
    Prototype = "SET SHOW [<section>] [-format text|json|yaml|csv] [-profile <name>] [-defaults] [-changed]")]
[CommandParameterOrdered("section", "Section to show", IsRequired = false, DefaultValue = "all",
    AllowedValues = new[] { "all", "general", "provider", "azure", "aws", "llama", "logprobs", "credentials", "paths" })]
[CommandParameterNamed("format", "Output shape",
    DefaultValue = "text", AllowedValues = new[] { "text", "json", "yaml", "csv" }, ShortAlias = "f")]
[CommandParameterNamed("profile", "Show this profile instead of the active one", ShortAlias = "p")]
[CommandFlag("defaults", "Show built-in defaults rather than stored values")]
[CommandFlag("changed", "Show only values that differ from the built-in default", ShortAlias = "c")]
[CommandHelpRemarks("Secret values are never rendered. Credential rows show status and provenance only.")]
[CommandHelpRemarks("'-format json' emits the document's wire names and is round-trippable through SET IMPORT.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `section` | ordered | `string` | no | `all` | `all`, `general`, `provider`, `azure`, `aws`, `llama`, `logprobs`, `credentials`, `paths` | Which block of the dump to render |
| `format` | named | `string` | no | `text` | `text`, `json`, `yaml`, `csv` | **NEW** — the source rendered one fixed human block only |
| `profile` | named | `string` | no | *(active profile)* | any existing profile name | **NEW** — inspect another profile without switching to it |
| `defaults` | flag | `bool` | no | `false` | — | **NEW** — render the built-in defaults, for "what would RESET give me?" |
| `changed` | flag | `bool` | no | `false` | — | **NEW** — render only drift from defaults; the fastest bug report a user can produce |

**`text` output is the source's dump, preserved.** Section headers and labels are carried verbatim: `Current Settings:` with `- Provider:`, `- Model ID:`, `- Temperature:`, `- Max Tokens:`, `- Azure Endpoint:` (`(not set)` when empty), `- AWS Region:`, `- System Prompt:`, `- Log Probabilities:` (`Enabled`/`Disabled`), `- Log Probabilities Top-K:`, `- Show All Tokens:` (`Yes` / `No (sample only)`), `- Token Display:` (`Grid Layout` / `List Layout`), `- Grid View Max Alternatives:`, `- OS Credential Store:` (`Enabled`/`Disabled`); then `Credentials (secure):` with `***set***` / `(not set)` plus a bracketed provenance string; then `LLama Provider Settings:` with `- Context Size:`, `- GPU Layer Count:` (` (CPU-only)` when 0), `- GPU Device:` (`(default)` when unset), `- Threads:` (`(system default)` when 0), `- Batch Size:`. **DEVIATION:** the source's trailing space after a non-zero GPU layer count (quirk Q28) is removed, and the source's dead find-and-replace over the dump (quirk Q8) is not reproduced. The three static help blocks the source appended to every dump are moved to `SET KEYS` and `HELP SET SHOW`; a dump is a dump.

**Pipeline behaviour** — **produces** piped output; **does not accept** piped input. Piped invocation returns the explanatory failure `SET SHOW does not read piped input. Did you mean 'SET IMPORT' or 'SET VALIDATE'?` rather than throwing. Non-piped `HandleExecution` may emit exactly one chunk under `AbstractCommand`, so the whole dump is one chunk with embedded newlines; that is correct here, because a settings dump is one document and splitting it per line would let a downstream filter silently halve it. `OutputFormat` is set from `-format`: `ResultFormat.General` for `text`, `ResultFormat.JSON`, `ResultFormat.YAML`, `ResultFormat.CSV` for the others — declared so the host's `AbstractTextIo` subclass can pick a renderer (the framework itself never branches on it; encoding is the host's job).

**Environment interaction** — reads `SET_FILE`, `SET_BASEDIR`, `SET_PROFILE`, `SET_REDACT` (default `true`). Reads the *names* `CHATDBG_AZURE_API_KEY`, `CHATDBG_AWS_ACCESS_KEY`, `AWS_ACCESS_KEY_ID`, `CHATDBG_AWS_SECRET_KEY`, `AWS_SECRET_ACCESS_KEY` from the process environment to report provenance, **never their values**. Writes nothing. Needs no environment-modifying permission.

**Failure modes** — unknown `section`: parse-time `AllowedValues` rejection, surfacing as the generic executor message (accepted here: the prototype and `HELP` list all nine). Unknown `-profile`: `Profile '<name>' not found. Run 'SET PROFILES' to list them.` Unreadable/corrupt document: the tool **still succeeds**, rendering built-in defaults plus a first line `Warning: the settings document could not be parsed (<reason>); showing built-in defaults. The file has not been modified.` — the source's degrade-to-defaults behaviour, but now visible in the command result instead of only on stdout. Base directory unresolvable: renders with the temp-directory path and a `paths` warning row. Downstream errors: not applicable (no piped input).

**Security and audit** — no parameter and no output line carries a secret; the credential section is status-and-provenance only, and the source's habit of echoing plaintext secrets during migration (quirk Q9) has no equivalent here. `-profile` values appear in audit parameters and are treated as non-sensitive names. Non-destructive; no confirmation.

**Traceability** — PRD **7.2 Settings & Configuration** (primary), **7.3 Credential Management** (provenance rows only), **7.12 Output Rendering** (`-format`). Descends from `/set` with no arguments (`Commands/SetCommand.cs:401-458`), the windowed Settings dialog's read path, and the plain-console startup banner. `-format`, `-profile`, `-defaults`, `-changed` and the `section` filter are **NEW**.

---

#### 2.4.2 `SET GET` — read exactly one value

| | |
|---|---|
| Command | `GET` |
| Root command | `SET` |
| Description | Print the value of one setting, and nothing else |
| Usage prototype | `SET GET <key> [-profile <name>] [-source]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Get", "Print the value of one setting", Prototype = "SET GET <key> [-profile <name>] [-source]")]
[CommandParameterOrdered("key", "Setting key to read", UsePipe = true)]
[CommandParameterNamed("profile", "Read from this profile instead of the active one", ShortAlias = "p")]
[CommandFlag("source", "Append where the value came from (document, default, environment override)")]
[CommandHelpRemarks("With piped input, each incoming chunk is treated as one key and one value is emitted per chunk.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `key` | ordered, `UsePipe = true` | `string` | yes (unless piped) | — | any canonical key or alias in §2.3 | Setting to read |
| `profile` | named | `string` | no | *(active)* | existing profile name | Read another profile's value |
| `source` | flag | `bool` | no | `false` | — | Append ` [document]` / ` [built-in default]` / ` [environment override]` |

**Pipeline behaviour** — **both**. As a source: `SET GET temperature` emits one chunk, the bare value with no label, so it composes. As a filter: one incoming chunk means **one setting key**; each chunk is looked up and one value chunk is emitted, in order. An unknown key in a piped chunk emits a failure chunk naming that key and processing continues with the next chunk — a bad key must not kill a batch. `OutputFormat` is `ResultFormat.General` (bare scalar) so the value can be consumed by any downstream tool; with `-source` it stays `General` because the annotation is human text.

**Environment interaction** — reads `SET_FILE`, `SET_BASEDIR`, `SET_PROFILE`. Writes nothing. No elevation.

**Failure modes** — unknown key: `Unknown setting: <key>. Run 'SET KEYS' for the full list.` (**DEVIATION** — the source's inline "Valid keys:" list named 13 of ~26 and rotted, quirk Q7; delegating to `SET KEYS` cannot rot). Write-blocked secret key: succeeds and returns the *status word* `***set***` or `(not set)` — never the value. Missing key with no pipe: parse-time required-parameter failure, so the usage prototype is the user's only cue; the help remark states it. Unparseable document: same visible-warning degrade as `SET SHOW`, then the default value.

**Security and audit** — the `key` parameter can name a secret slot; the **output is masked unconditionally** and cannot be unmasked by any flag. This package registers `azureApiKey`, `awsAccessKey`, `awsSecretKey` and `apikey` in its audit masking configuration, but see §2.6.7 — framework masking does not cover `-name value` syntax, which is exactly why no tool here ever accepts a secret as a parameter. Non-destructive.

**Traceability** — PRD **7.2**. **NEW** — the source had no single-value read; the only way to see one setting was the 46-line dump. This tool is what makes every pipeline in §2.5 possible.

---

#### 2.4.3 `SET VALUE` — change one tunable

| | |
|---|---|
| Command | `VALUE` |
| Root command | `SET` |
| Description | Validate and store one setting, then persist the document |
| Usage prototype | `SET VALUE <key> <value…> [-profile <name>] [-clamp] [-dryrun] [-quiet]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Value", "Validate and store one setting",
    Prototype = "SET VALUE <key> <value...> [-profile <name>] [-clamp] [-dryrun] [-quiet]")]
[CommandParameterOrdered("key", "Setting key to write")]
[CommandParameterSuffix("value", "New value; all remaining words, joined with single spaces", UsePipe = true)]
[CommandParameterNamed("profile", "Write to this profile instead of the active one", ShortAlias = "p")]
[CommandFlag("clamp", "Pull an out-of-range number to the nearest bound instead of rejecting it")]
[CommandFlag("dryrun", "Validate and report, but do not mutate or persist", ShortAlias = "n")]
[CommandFlag("quiet", "Suppress the confirmation line", ShortAlias = "q")]
[CommandHelpRemarks("Ranges are inclusive at both ends. Run 'SET KEYS' for every key, default and range.")]
[CommandHelpRemarks("Secrets cannot be set here. Use the CRED tools or the documented environment variables.")]
[CommandHelpRemarks("Quote a value containing '|' so the pipeline parser does not split your command line.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `key` | ordered | `string` | yes | — | any canonical key or alias in §2.3. **No `AllowedValues` is declared** — the tool validates in-body so the user sees the source's precise `Unknown setting: <key>` message instead of the executor's generic one | Setting to write |
| `value` | suffix, `UsePipe = true` | `string` | yes (unless piped) | — | per-key, per §2.3 | Everything after the key, joined with single spaces. Runs of spaces collapse — source parity |
| `profile` | named | `string` | no | *(active)* | existing profile name | **NEW** — edit a profile you are not running |
| `clamp` | flag | `bool` | no | `false` | — | **NEW** — makes the source's *second*, hidden policy explicit (§2.6.4) |
| `dryrun` | flag | `bool` | no | `false` | — | **NEW** |
| `quiet` | flag | `bool` | no | `false` | — | **NEW** — for pipelines that only want the side effect |

**Pipeline behaviour** — **accepts and produces**. Because `value` carries `UsePipe = true`, `… | SET VALUE temperature` reads the value from the pipe and the command line carries only the key. One incoming chunk means **one candidate value for the named key**; each chunk is validated and applied and the tool emits `Set <key> = <value>` per chunk, or an empty success under `-quiet` (which the host drops, so a quiet pipeline stage is silent — the `SET`/`REGIF` idiom). `OnStartPipe` snapshots the record and opens one write transaction; `OnEndPipe` commits **once**, so a 500-chunk pipe produces one document write, not 500. That is a deliberate improvement on the source's save-on-every-change, without losing the guarantee: after `OnEndPipe` the document reflects every applied chunk. Non-piped invocation writes immediately, exactly as the source did.

**Environment interaction** — reads `SET_FILE`, `SET_BASEDIR`, `SET_PROFILE`, `SET_STRICT` (default `true`; `false` makes `-clamp` the default), `SET_READONLY`. **Writes** the effective-configuration mirror: on success it republishes the changed key as `CHATDBG_<UPPER_SNAKE_KEY>` in the controller environment (e.g. `CHATDBG_TEMPERATURE`, `CHATDBG_MAX_TOKENS`, `CHATDBG_AZURE_API_VERSION`). **This tool must be registered with `modifiesEnvironment: true`** — without it the framework confines its writes to the `SET_` bucket and no other package sees the change. Reads no process environment variable except the three startup overrides.

**Failure modes**

| Situation | What the user sees |
|---|---|
| Unknown key | `Unknown setting: <key>. Run 'SET KEYS' for the full list.` Nothing mutated, nothing written. |
| Key given, no value, no pipe | `Usage: SET VALUE <key> <value>` — the source's arity rule, minus its consequence: a value *may* now be empty via `SET UNSET` (§2.4.4). |
| Out of range, `-clamp` absent | The exact per-key message from §2.3. Nothing mutated, nothing written. |
| Out of range, `-clamp` present | Success with `Set <key> = <bound> (clamped from <input>)`. The clamp is always reported — the source's dialog clamped silently. |
| Unparseable number | `<key> must be a number between <lo> and <hi>` — even under `-clamp`. The source's dialog silently kept the old value; silence about garbage input is not a feature. |
| Write-blocked secret key | The secure-options guide, now naming `CRED PUT <type>` and the documented environment variables. Nothing stored anywhere. |
| `modelId` under provider `llama`, file missing | `LLama model file not found: <path>` + `Make sure you've specified the correct path to a GGUF model file.` |
| System prompt name does not resolve | `System prompt not found: <name>. Use 'PROMPT LIST' …` |
| `useOsCredentialStore true` with no store on this OS | `No OS credential store is available on this platform (<os>).` |
| Document write fails | **DEVIATION:** `Failed to save settings: <reason>. The change is active for this session only.` returned as a **failure result**. The source printed to stdout and still reported success (quirk Q5) — invisible in the windowed shell. |
| `SET_READONLY=true` | `The settings document is read-only in this host. Values can be inspected but not changed.` |
| Failure chunk arrives on the pipe | `AbstractCommand.Main` forwards it downstream untouched and never calls `HandlePipedChunk`; the transaction stays open and later good chunks still apply. `OnEndPipe` commits what succeeded and appends `<n> chunk(s) failed upstream and were not applied.` |

**Security and audit** — the `value` suffix parameter is the one place in this package where a user could type something sensitive (e.g. an endpoint with an embedded token). Because framework audit masking only rewrites `-name=value` tokens, this tool declares that `azureEndpoint` values are recorded in audit metadata **hashed, not literal**, and the three secret keys never reach the audit record at all (the command fails before storing). Not destructive in the "irreversible" sense — every write is recoverable via `SET RESET <key>` or a profile — so no confirmation is required.

**Traceability** — PRD **7.2** (primary), **7.3** (blocked keys), **7.5** (system prompt name), **7.6 / 7.7 / 7.8** (the keys each provider consumes), **7.9** (the five display keys). Descends from `/set <key> <value…>` (`Commands/SetCommand.cs:28-294`). `-profile`, `-clamp`, `-dryrun`, `-quiet` and pipe-fed values are **NEW**.

---

#### 2.4.4 `SET UNSET` — clear a text setting

| | |
|---|---|
| Command | `UNSET` |
| Root command | `SET` |
| Description | Return one setting to empty (text keys) or to its built-in default (typed keys) |
| Usage prototype | `SET UNSET <key> [-profile <name>] [-dryrun]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Unset", "Clear one setting", Prototype = "SET UNSET <key> [-profile <name>] [-dryrun]")]
[CommandParameterOrdered("key", "Setting key to clear", UsePipe = true)]
[CommandParameterNamed("profile", "Clear in this profile instead of the active one", ShortAlias = "p")]
[CommandFlag("dryrun", "Report what would be cleared without writing", ShortAlias = "n")]
[CommandHelpRemarks("Nullable text keys become empty; typed keys return to the built-in default from SET KEYS.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `key` | ordered, `UsePipe = true` | `string` | yes (unless piped) | — | any key in §2.3 except the three write-blocked secret slots and `profile` | Setting to clear |
| `profile` | named | `string` | no | *(active)* | existing profile name | Target profile |
| `dryrun` | flag | `bool` | no | `false` | — | Validate only |

**Pipeline behaviour** — **accepts and produces**. One chunk = one key to clear; one confirmation chunk out per key. Same one-transaction `OnStartPipe`/`OnEndPipe` commit discipline as `SET VALUE`. `ResultFormat.General`.

**Environment interaction** — identical to `SET VALUE`, including the `CHATDBG_*` republish and the `modifiesEnvironment: true` requirement (clearing `azureEndpoint` must propagate, or the provider package keeps dialling a stale host).

**Failure modes** — unknown key: as `SET GET`. Attempt to clear a secret slot: `Secrets are cleared with 'CRED CLEAR <type>', not here.` Attempt to clear `provider`, `temperature` or any other non-nullable typed key: succeeds, restoring the §2.3 default, and says so — `Cleared temperature (restored default 0.7)`. Read-only host and write-failure behaviour identical to `SET VALUE`.

**Security and audit** — no secrets. Reversible via `SET VALUE` or a saved profile; no confirmation required.

**Traceability** — PRD **7.2**. **NEW**, and it closes a real hole: the source's arity rule rejected a bare `/set azureEndpoint`, and there was no reset, unset or clear verb anywhere, so **no text setting could ever be returned to empty from the command surface** — only by hand-editing or deleting the document. The windowed dialog could clear those fields, so the two surfaces disagreed about what was expressible.

---

#### 2.4.5 `SET RESET` — restore built-in defaults

| | |
|---|---|
| Command | `RESET` |
| Root command | `SET` |
| Description | Restore one key, one section, or the whole record to built-in defaults |
| Usage prototype | `SET RESET [<key-or-section>] [-profile <name>] [-force] [-dryrun] [-backup]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Reset", "Restore built-in defaults",
    Prototype = "SET RESET [<key-or-section>] [-profile <name>] [-force] [-dryrun] [-backup]")]
[CommandParameterOrdered("target", "Key or section to reset", IsRequired = false, DefaultValue = "all")]
[CommandParameterNamed("profile", "Reset this profile instead of the active one", ShortAlias = "p")]
[CommandFlag("force", "Confirm a whole-record reset", ShortAlias = "y")]
[CommandFlag("dryrun", "List what would change without writing", ShortAlias = "n")]
[CommandFlag("backup", "Copy the current document to <base>/backups/ before writing")]
[CommandHelpRemarks("'SET RESET all' rewrites every field and requires -force. A single key does not.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `target` | ordered | `string` | no | `all` | any key in §2.3, any section name from `SET SHOW`, or `all` | What to restore |
| `profile` | named | `string` | no | *(active)* | existing profile name | Target profile |
| `force` | flag | `bool` | no | `false` | — | Required when `target` is `all` or a section |
| `dryrun` | flag | `bool` | no | `false` | — | Emits the same lines a real run would, prefixed `would reset` |
| `backup` | flag | `bool` | no | `false` | — | **NEW** — the source never backed anything up |

**Pipeline behaviour** — **produces only**. Emits one line per field actually changed (`reset <key>: <old> -> <default>`), so a reset is auditable at a glance and `SET RESET -dryrun | REGIF llama` answers "what would this touch?". Piped input is refused with `SET RESET does not read piped input. Pipe keys into 'SET UNSET' instead.` `ResultFormat.General`; `-format` is intentionally absent because the output is a change log, not a record.

**Environment interaction** — reads `SET_FILE`, `SET_BASEDIR`, `SET_PROFILE`, `SET_CONFIRM` (default `true`; when `false`, `-force` is not demanded — for non-interactive hosts and test harnesses). **Writes** the full `CHATDBG_*` mirror, because a whole-record reset changes almost every published key. Requires `modifiesEnvironment: true`.

**Failure modes** — `target` names nothing recognisable: `Unknown setting or section: <target>. Run 'SET KEYS' or 'SET SHOW -format csv'.` Whole-record or section reset without `-force` while `SET_CONFIRM` is `true`: **fails safe** with `This resets <n> settings in profile '<p>'. Re-run with -force to confirm.` — nothing is touched. Backup directory not writable under `-backup`: the reset is **abandoned**, not performed without the backup. Write failure: reported as a failure result, with the note that the in-memory record has already been reset for this session.

**Security and audit** — **destructive and, without `-backup`, irreversible.** Confirmation is mandatory for `all` and for sections. It never touches the three secret slots (those are `CRED`'s to clear) and never deletes the document itself — a reset rewrites content, it does not unlink the file. Audit metadata records the target and the count of fields changed.

**Traceability** — PRD **7.2**. **NEW.** The source had no reset path at all: *"Deleted: never. There is no reset command, no 'restore defaults', and no file deletion path. The only way back to defaults is to delete the file out-of-band."* A product with ~24 tunables and eight numeric ranges needs a way home.

---

#### 2.4.6 `SET MODEL` — read or change the model identifier

| | |
|---|---|
| Command | `MODEL` |
| Root command | `SET` |
| Description | Show or change the model id / GGUF path for the active provider |
| Usage prototype | `SET MODEL [<model id or path…>] [-profile <name>] [-nocheck]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Model", "Show or change the model identifier",
    Prototype = "SET MODEL [<model id or path...>] [-profile <name>] [-nocheck]")]
[CommandParameterSuffix("modelid", "New model id, or a GGUF file path for the local provider",
    IsRequired = false, UsePipe = true)]
[CommandParameterNamed("profile", "Change the model in this profile", ShortAlias = "p")]
[CommandFlag("nocheck", "Skip the local-model file existence check")]
[CommandHelpRemarks("With no argument this prints the current model and changes nothing.")]
[CommandHelpRemarks("Under provider 'llama' the value must name an existing GGUF file unless -nocheck is given.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `modelid` | suffix, `UsePipe = true` | `string` | no | *(none — read mode)* | any text; existing file path when provider is `llama` | New model identifier |
| `profile` | named | `string` | no | *(active)* | existing profile name | Target profile |
| `nocheck` | flag | `bool` | no | `false` | — | **NEW** — the deliberate escape hatch for the one case the check gets wrong (a path that will exist by the time the model loads) |

**Pipeline behaviour** — **accepts and produces**. Bare `SET MODEL` emits `Current model: <id>` and writes nothing (source parity). With a value it emits `Changed model from '<old>' to '<new>'` (source parity, verbatim). Piped: one chunk = one candidate model id; each is validated and applied in order, last one wins, one confirmation chunk each. `ResultFormat.General`.

**Environment interaction** — reads `SET_FILE`, `SET_BASEDIR`, `SET_PROFILE`; republishes `CHATDBG_MODEL_ID`. Requires `modifiesEnvironment: true` when it writes.

**Failure modes** — empty or whitespace-only value: `A model id is required. Run 'SET MODEL' with no arguments to see the current one.` — **DEVIATION**, the source accepted empty-ish input here. Provider is `llama` and the file does not exist: the same message `SET VALUE modelId` gives, `LLama model file not found: <path>` + `Make sure you've specified the correct path to a GGUF model file.` — **DEVIATION**, and the important one: in the source, `/model` bypassed every rule `/set modelId` enforced while writing the identical field (quirk Q1). Two commands writing one field must agree. Write failure: failure result, as `SET VALUE`.

**Security and audit** — the value may be a filesystem path; paths are recorded in audit parameters as given. Not destructive; the previous model id is in the confirmation line, so the undo is obvious.

**Traceability** — PRD **7.2**, **7.6 / 7.7 / 7.8**. Descends from `/model` (`Commands/ModelCommand.cs:23-34`). `-profile`, `-nocheck`, pipe support and validation parity are **NEW**.

---

#### 2.4.7 `SET PATH` — where configuration lives

| | |
|---|---|
| Command | `PATH` |
| Root command | `SET` |
| Description | Report the resolved settings document, base directory, profile directory and how each was chosen |
| Usage prototype | `SET PATH [-format text\|json] [-verify]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Path", "Report configuration file locations",
    Prototype = "SET PATH [-format text|json] [-verify]")]
[CommandParameterNamed("format", "Output shape", DefaultValue = "text", AllowedValues = new[] { "text", "json" }, ShortAlias = "f")]
[CommandFlag("verify", "Also report existence, writability and size for each path")]
[CommandHelpRemarks("The base directory falls back to the OS temp directory when the user profile cannot be resolved.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text`, `json` | Output shape |
| `verify` | flag | `bool` | no | `false` | — | Adds existence / writable / byte-size / last-write columns |

**Pipeline behaviour** — **produces only**; refuses piped input with an explanatory string. `ResultFormat.JSON` under `-format json`, else `General`. The bare text form emits the settings document path alone on the first line so `SET PATH | …` composes with any path-consuming tool.

**Environment interaction** — reads `SET_BASEDIR`, `SET_FILE`, `SET_PROFILE`, plus the process variables `CHATDBG_HOME` and `CHATDBG_SETTINGS_FILE` (**NEW** overrides). Writes `SET_FILE` and `SET_BASEDIR` back into its own bucket so later tools in the same pipeline resolve identically — a same-bucket write, so no elevation.

**Failure modes** — user profile lookup empty, whitespace or throwing: succeeds, reporting the OS temp directory and the reason (`user profile could not be resolved; using the temp directory`). Under `-verify`, an unwritable base directory is reported as a row, not an error — this tool's job is to tell the truth about the filesystem, never to fail because of it.

**Security and audit** — emits absolute filesystem paths, which can leak a username in the home-directory component. Audit records the flags only, not the resolved output. Non-destructive.

**Traceability** — PRD **7.2**, **7.11 Diagnostic Logging** (it is the first line of every good bug report). Descends from the plain-console startup banner's `Settings file: <path>` line — **the only place the source ever told the user where their configuration lived**, and one the windowed shell never printed. Promoting it to a tool is **NEW**.

---

#### 2.4.8 `SET VALIDATE` — check a record before trusting it

| | |
|---|---|
| Command | `VALIDATE` |
| Root command | `SET` |
| Description | Validate the active record, a profile, or a piped document against the key catalog |
| Usage prototype | `SET VALIDATE [<profile>] [-strict] [-format text\|json\|csv]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Validate", "Validate a configuration record",
    Prototype = "SET VALIDATE [<profile>] [-strict] [-format text|json|csv]")]
[CommandParameterOrdered("profile", "Profile to validate", IsRequired = false, DefaultValue = "", UsePipe = true)]
[CommandFlag("strict", "Treat warnings (unknown keys, suspicious endpoints) as failures")]
[CommandParameterNamed("format", "Output shape", DefaultValue = "text", AllowedValues = new[] { "text", "json", "csv" }, ShortAlias = "f")]
[CommandHelpRemarks("Piped input is treated as a JSON settings document, one complete document per chunk.")]
[CommandHelpRemarks("Validation never mutates anything; use SET IMPORT to adopt a document that passes.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `profile` | ordered, `UsePipe = true` | `string` | no | `""` (= active record) | existing profile name | What to validate |
| `strict` | flag | `bool` | no | `false` | — | Promote warnings to failures |
| `format` | named | `string` | no | `text` | `text`, `json`, `csv` | Report shape |

**Checks performed** — every §2.3 range and enum; provider name **case-sensitivity** (a hand-edited `"Azure"` is reported as `provider 'Azure' will not match any known provider; use 'azure'` — the source silently produced `Warning: Unknown AI provider: Azure` at startup and dropped every chat turn thereafter); `modelId` file existence when `provider` is `llama`; system-prompt name resolution via `PROMPT`; `useOsCredentialStore` set on a platform with no store; unknown keys present in the document; keys whose value is a number formatted for a non-invariant culture; and the three deprecated secret slots being non-empty — reported as `azureApiKey is stored in plaintext in the settings document. Run 'CRED MIGRATE'.` **with the value never shown**.

**Pipeline behaviour** — **accepts and produces**. One incoming chunk = one complete JSON settings document, validated independently; one report per chunk. This is the shape that makes `SET SHOW -format json | SET VALIDATE -strict` and `SET PROFILES | SET VALIDATE` work. `ResultFormat.JSON` / `CSV` / `General` per `-format`. A clean record under `-format csv` emits **no rows and an empty success**, which the host drops — so a validation stage is silent when everything is fine, and only speaks when something is wrong.

**Environment interaction** — reads `SET_FILE`, `SET_BASEDIR`, `SET_PROFILE`, `SET_STRICT`. Writes nothing at all — this tool is pure. No elevation.

**Failure modes** — piped chunk is not valid JSON: a failure chunk `Chunk <n> is not a JSON object: <parser message>`, and the next chunk is still processed. Named profile missing: `Profile '<name>' not found.` Findings present: the tool returns **success** with the findings as output unless `-strict`, in which case it returns a failure whose message is the finding count — so a script can branch on it. Upstream failure chunks pass straight through untouched.

**Security and audit** — reads documents that may contain plaintext secrets in the deprecated slots and **must never echo one**; findings name the key, never the value. Non-destructive, no confirmation.

**Traceability** — PRD **7.2**. **NEW.** The source validated only at the moment of a `/set` write; a hand-edited or copied document was read verbatim with no validation whatsoever, and the user discovered the problem as a runtime chat failure.

---

#### 2.4.9 `SET KEYS` — the machine-readable key catalog

| | |
|---|---|
| Command | `KEYS` |
| Root command | `SET` |
| Description | List every setting key with its type, default, range and current value |
| Usage prototype | `SET KEYS [<group>] [-format text\|csv\|json] [-changed]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Keys", "List every setting key, type, default and range",
    Prototype = "SET KEYS [<group>] [-format text|csv|json] [-changed]")]
[CommandParameterOrdered("group", "Key group", IsRequired = false, DefaultValue = "all",
    AllowedValues = new[] { "all", "general", "azure", "aws", "llama", "logprobs", "credentials" })]
[CommandParameterNamed("format", "Output shape", DefaultValue = "csv", AllowedValues = new[] { "text", "csv", "json" }, ShortAlias = "f")]
[CommandFlag("changed", "List only keys whose current value differs from the default", ShortAlias = "c")]
[CommandHelpRemarks("Default output is CSV, one key per chunk, so it composes directly into SET GET.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `group` | ordered | `string` | no | `all` | `all`, `general`, `azure`, `aws`, `llama`, `logprobs`, `credentials` | Which family of keys |
| `format` | named | `string` | no | `csv` | `text`, `csv`, `json` | Output shape |
| `changed` | flag | `bool` | no | `false` | — | Drift only |

**Pipeline behaviour** — **produces only**, and it is the package's primary pipeline **source**. It overrides `Main` (legal, and necessary: `AbstractCommand`'s non-piped path emits exactly one chunk) to emit **one chunk per key**, so downstream filters and per-key tools compose naturally. Columns: `key,aliases,type,default,min,max,allowed,current,consumed`. `ResultFormat.CSV` by default, `JSON`, or `General` for the aligned human table. Piped input is refused with an explanatory string.

**Environment interaction** — reads `SET_FILE`, `SET_BASEDIR`, `SET_PROFILE` for the `current` column. Writes nothing. No elevation.

**Failure modes** — unreadable document: still succeeds, with the `current` column filled from defaults and a leading warning row. Unknown group: parse-time `AllowedValues` rejection (acceptable; all seven groups are in the prototype).

**Security and audit** — the `current` column for the three secret slots is `***set***` / `(not set)`, never a value. Non-destructive.

**Traceability** — PRD **7.1 Command System & Dispatch** (discoverability) and **7.2**. **NEW.** It replaces three drifting hand-maintained lists in the source — the `/set` long help (~72 lines), the settings dump's static "Setup Commands" block, and the `Unknown setting:` error's 13-of-26 key list — with one generated catalog.

---

#### 2.4.10 `SET IMPORT` — adopt a whole document

| | |
|---|---|
| Command | `IMPORT` |
| Root command | `SET` |
| Description | Validate and adopt a complete settings document from a file or the pipe |
| Usage prototype | `SET IMPORT [<path>] [-profile <name>] [-merge] [-dryrun] [-force] [-backup]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Import", "Adopt a settings document",
    Prototype = "SET IMPORT [<path>] [-profile <name>] [-merge] [-dryrun] [-force] [-backup]")]
[CommandParameterOrdered("path", "Document to import", IsRequired = false, DefaultValue = "", UsePipe = true)]
[CommandParameterNamed("profile", "Import into this profile instead of the active one", ShortAlias = "p")]
[CommandFlag("merge", "Apply only the keys present in the document; keep the rest")]
[CommandFlag("dryrun", "Validate and report the change list without writing", ShortAlias = "n")]
[CommandFlag("force", "Adopt despite validation warnings", ShortAlias = "y")]
[CommandFlag("backup", "Copy the current document to <base>/backups/ before writing")]
[CommandHelpRemarks("Without -merge the document replaces every key; absent keys revert to their defaults.")]
[CommandHelpRemarks("Secret keys in an imported document are ignored and reported, never stored.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `path` | ordered, `UsePipe = true` | `string` | yes (unless piped) | `""` | readable file path | JSON (or YAML, if that format shipped) document |
| `profile` | named | `string` | no | *(active)* | profile name; created if absent | Import target |
| `merge` | flag | `bool` | no | `false` | — | Patch rather than replace |
| `dryrun` | flag | `bool` | no | `false` | — | Change list only |
| `force` | flag | `bool` | no | `false` | — | Required when validation produces warnings |
| `backup` | flag | `bool` | no | `false` | — | Pre-write snapshot |

**Pipeline behaviour** — **accepts and produces**. One incoming chunk = one complete JSON document; each is validated in full and, if it passes, applied. Emits one `imported <key>: <old> -> <new>` line per changed key, and a final count. `OnStartPipe` opens one transaction; `OnEndPipe` commits once. `ResultFormat.General`.

**Environment interaction** — reads `SET_FILE`, `SET_BASEDIR`, `SET_PROFILE`, `SET_CONFIRM`, `SET_READONLY`. **Writes** the full `CHATDBG_*` mirror; requires `modifiesEnvironment: true`.

**Failure modes** — path missing or unreadable: `Cannot read '<path>': <reason>`. Not valid JSON: `<path> is not a valid settings document: <parser message>`. Validation findings without `-force`: fails with the finding list, nothing written — a bad document must never half-apply. Findings with `-force`: applies the valid keys, skips the invalid ones, and names each skipped key. Secret keys present: ignored, with `Ignored 2 credential field(s) in the imported document; secrets are managed by CRED.` Write failure: failure result naming the reason, with a note that the in-memory record now differs from disk.

**Security and audit** — reads a user-supplied file that may contain plaintext secrets; those values are **never stored, never echoed, never logged**. The path parameter is audited. **Destructive without `-merge`** — it replaces the record — so `-force`/confirmation applies whenever validation is not clean, and `-backup` is strongly recommended in the help text.

**Traceability** — PRD **7.2**. **NEW.** The source had no import, no merge and no layering of any kind; the only way to move a configuration between machines was to copy the file by hand and hope it parsed.

---

#### 2.4.11 `SET PROFILE` — show or switch the active profile

| | |
|---|---|
| Command | `PROFILE` |
| Root command | `SET` |
| Description | Show the active configuration profile, or switch to another one |
| Usage prototype | `SET PROFILE [<name>] [-dryrun]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Profile", "Show or switch the active configuration profile",
    Prototype = "SET PROFILE [<name>] [-dryrun]")]
[CommandParameterOrdered("name", "Profile to activate", IsRequired = false, DefaultValue = "", UsePipe = true)]
[CommandFlag("dryrun", "Report what switching would change without switching", ShortAlias = "n")]
[CommandHelpRemarks("With no name this prints the active profile and changes nothing.")]
[CommandHelpRemarks("Switching validates the target first; an invalid profile is never activated.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered, `UsePipe = true` | `string` | no | `""` (= read mode) | an existing profile name; `[A-Za-z0-9._-]{1,64}` | Profile to activate |
| `dryrun` | flag | `bool` | no | `false` | — | Preview the switch as a change list |

**Pipeline behaviour** — **accepts and produces**. Bare invocation emits `Active profile: <name>` and changes nothing. With a name it validates, activates, mirrors the profile into `<base>/settings.json`, and emits `Switched to profile '<name>' (provider <p>, model <m>).` Piped: one chunk = one profile name; the last valid one wins, one line each. `ResultFormat.General`.

**Environment interaction** — reads `SET_BASEDIR`, `SET_PROFILE`, and the process variable `CHATDBG_PROFILE` (**NEW**, honoured at first resolution so a shell can be launched into a profile). **Writes** `CHATDBG_PROFILE` and the whole `CHATDBG_*` mirror; requires `modifiesEnvironment: true` — a switch that other packages cannot see is a bug, not a switch.

**Failure modes** — profile not found: `Profile '<name>' not found. Run 'SET PROFILES' to list them.` Profile exists but fails validation: `Profile '<name>' has <n> problem(s) and was not activated. Run 'SET VALIDATE <name>'.` — the active profile is untouched. Name fails the character rule: `A profile name may contain letters, digits, dot, dash and underscore only.` Mirror write fails: the switch is **rolled back in memory** and reported, so the session and the document never disagree.

**Security and audit** — no secrets (a profile document never holds one; secret slots are stripped on save, §2.4.13). Reversible by switching back; no confirmation. The profile name is audited.

**Traceability** — PRD **7.2**. **NEW** — see §2.1.3 for why the package earns its name.

---

#### 2.4.12 `SET PROFILES` — list profiles

| | |
|---|---|
| Command | `PROFILES` |
| Root command | `SET` |
| Description | List every saved profile, one per chunk |
| Usage prototype | `SET PROFILES [-format text\|csv\|json] [-verbose]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Profiles", "List saved configuration profiles",
    Prototype = "SET PROFILES [-format text|csv|json] [-verbose]")]
[CommandParameterNamed("format", "Output shape", DefaultValue = "text", AllowedValues = new[] { "text", "csv", "json" }, ShortAlias = "f")]
[CommandFlag("verbose", "Include provider, model, last-used and validity for each profile", ShortAlias = "v")]
[CommandHelpRemarks("The active profile is marked with a leading '*' in text form and an isActive column otherwise.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text`, `csv`, `json` | Output shape |
| `verbose` | flag | `bool` | no | `false` | — | Adds provider, model, last-used timestamp, validity |

**Pipeline behaviour** — **produces only**, one chunk per profile (overrides `Main` for the same reason `SET KEYS` does). In `text` form each chunk is the bare profile name, so `SET PROFILES | SET VALIDATE` and `SET PROFILES | SET DIFF -against active` work with no glue. Refuses piped input with an explanatory string. `ResultFormat.CSV` / `JSON` / `General`.

**Environment interaction** — reads `SET_BASEDIR`, `SET_PROFILE`. Writes nothing. No elevation.

**Failure modes** — profile directory missing: succeeds, emitting the single implicit profile `default` (the one-document world the source lived in). Unreadable profile document: emitted with `(unreadable)` in verbose form rather than aborting the listing. Directory unreadable: failure result naming the path and the reason.

**Security and audit** — none; names and metadata only. Non-destructive.

**Traceability** — PRD **7.2**. **NEW.**

---

#### 2.4.13 `SET SAVEPROFILE` — capture the current record as a profile

| | |
|---|---|
| Command | `SAVEPROFILE` |
| Root command | `SET` |
| Description | Save the current configuration as a named profile |
| Usage prototype | `SET SAVEPROFILE <name> [-from <profile>] [-overwrite] [-describe <text…>]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("SaveProfile", "Save the current configuration as a named profile",
    Prototype = "SET SAVEPROFILE <name> [-from <profile>] [-overwrite] [-describe <text...>]")]
[CommandParameterOrdered("name", "Name for the new profile", UsePipe = true)]
[CommandParameterNamed("from", "Copy this profile instead of the live record")]
[CommandFlag("overwrite", "Replace an existing profile of the same name", ShortAlias = "y")]
[CommandParameterSuffix("describe", "Free-text description stored with the profile", IsRequired = false)]
[CommandHelpRemarks("Credential fields are stripped: a profile document never contains a secret slot.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered, `UsePipe = true` | `string` | yes (unless piped) | — | `[A-Za-z0-9._-]{1,64}`, not `default` unless `-overwrite` | Profile name |
| `from` | named | `string` | no | *(live record)* | existing profile name | Copy source |
| `overwrite` | flag | `bool` | no | `false` | — | Required to replace |
| `describe` | suffix | `string` | no | `""` | any text | Stored as `description` in the profile document |

**Pipeline behaviour** — **accepts and produces**. One chunk = one profile name to create (useful for scripted fan-out of a base configuration). Emits `Saved profile '<name>' (<n> settings).` per chunk. `ResultFormat.General`.

**Environment interaction** — reads `SET_BASEDIR`, `SET_PROFILE`, `SET_READONLY`. Writes only its own `SET_LAST_PROFILE` bucket key — saving a profile does not change the active configuration, so **no `modifiesEnvironment` elevation is required**. That asymmetry with `SET PROFILE` is deliberate and is the cheapest way to keep the elevated set small.

**Failure modes** — name already exists without `-overwrite`: `Profile '<name>' already exists. Re-run with -overwrite to replace it.` Invalid name: the character-rule message from §2.4.11. `-from` names nothing: `Profile '<name>' not found.` Profile directory not writable: failure result naming the path. Read-only host: the standard read-only refusal.

**Security and audit** — **the three secret slots are stripped on save, unconditionally.** A profile document is designed to be shared, mailed and committed; it must never be able to carry a credential. That is stated in a help remark so the user can rely on it. `-overwrite` replaces a document, so it is the confirmation gate.

**Traceability** — PRD **7.2**. **NEW.**

---

#### 2.4.14 `SET DROPPROFILE` — delete a profile

| | |
|---|---|
| Command | `DROPPROFILE` |
| Root command | `SET` |
| Description | Permanently delete a saved profile |
| Usage prototype | `SET DROPPROFILE <name> [-force] [-backup]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("DropProfile", "Delete a saved profile",
    Prototype = "SET DROPPROFILE <name> [-force] [-backup]")]
[CommandParameterOrdered("name", "Profile to delete", UsePipe = true)]
[CommandFlag("force", "Confirm the deletion", ShortAlias = "y")]
[CommandFlag("backup", "Copy the profile to <base>/backups/ before deleting")]
[CommandHelpRemarks("The active profile and the profile named 'default' cannot be deleted.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered, `UsePipe = true` | `string` | yes (unless piped) | — | an existing profile, not the active one, not `default` | Profile to delete |
| `force` | flag | `bool` | no | `false` | — | **Required** unless `SET_CONFIRM=false` |
| `backup` | flag | `bool` | no | `false` | — | Pre-delete snapshot |

**Pipeline behaviour** — **accepts and produces**. One chunk = one profile name; each deletion is confirmed individually and a refusal on one name does not stop the rest. Emits `Deleted profile '<name>'.` per success. `ResultFormat.General`. Note that `-force` applies to the whole invocation, which is the point: a piped bulk delete is an explicit act.

**Environment interaction** — reads `SET_BASEDIR`, `SET_PROFILE`, `SET_CONFIRM`, `SET_READONLY`. Writes nothing outside its bucket; no elevation.

**Failure modes** — no `-force` with `SET_CONFIRM=true`: `This permanently deletes profile '<name>'. Re-run with -force.` — nothing deleted. Target is the active profile: `Profile '<name>' is active. Switch away with 'SET PROFILE <other>' first.` Target is `default`: `The 'default' profile cannot be deleted. Use 'SET RESET all -force' to return it to built-in defaults.` Not found: `Profile '<name>' not found.` Backup requested but the backup directory is unwritable: **the deletion is abandoned**. Delete fails: failure result naming the path and reason.

**Security and audit** — **destructive and irreversible without `-backup`.** Confirmation mandatory. No secrets involved (profiles carry none). The deleted name and the backup path go into audit metadata.

**Traceability** — PRD **7.2**. **NEW.**

---

#### 2.4.15 `SET DIFF` — compare two configurations

| | |
|---|---|
| Command | `DIFF` |
| Root command | `SET` |
| Description | Report the keys that differ between two profiles, or between a profile and the built-in defaults |
| Usage prototype | `SET DIFF <left> [<right>] [-against active\|defaults\|<profile>] [-format text\|csv\|json]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Diff", "Compare two configurations",
    Prototype = "SET DIFF <left> [<right>] [-against active|defaults|<profile>] [-format text|csv|json]")]
[CommandParameterOrdered("left", "First profile, or 'active'", UsePipe = true)]
[CommandParameterOrdered("right", "Second profile", IsRequired = false, DefaultValue = "")]
[CommandParameterNamed("against", "Comparison target when only one side is given", DefaultValue = "defaults")]
[CommandParameterNamed("format", "Output shape", DefaultValue = "text", AllowedValues = new[] { "text", "csv", "json" }, ShortAlias = "f")]
[CommandHelpRemarks("Emits one chunk per differing key, so a clean comparison is silent.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `left` | ordered, `UsePipe = true` | `string` | yes (unless piped) | — | a profile name, or `active`, or `defaults` | Left side |
| `right` | ordered | `string` | no | `""` | as above | Right side; when omitted, `-against` decides |
| `against` | named | `string` | no | `defaults` | `active`, `defaults`, or a profile name | Right side for single-sided comparison |
| `format` | named | `string` | no | `text` | `text`, `csv`, `json` | Output shape |

**Pipeline behaviour** — **accepts and produces**, and it overrides `Main` to emit **one chunk per differing key** (`<key>: <left> | <right>`). One incoming chunk = one left-hand profile name compared against `-against`, which is what makes `SET PROFILES | SET DIFF -against active` a per-profile drift report. **A clean comparison emits nothing** — an empty success that the host drops — so the tool is silent when there is no news, following the framework's `REGIF` filter idiom. `ResultFormat.CSV` / `JSON` / `General`.

**Environment interaction** — reads `SET_BASEDIR`, `SET_PROFILE`. Writes nothing. No elevation.

**Failure modes** — either side not found: `Profile '<name>' not found.` as a failure chunk; other chunks continue. A side that is unreadable or unparseable: reported as one finding row rather than aborting. Both sides identical: empty success (silence), which is a documented outcome, not an error.

**Security and audit** — a diff can surface the value of any non-secret key; secret slots are compared by **presence only**, rendered `***set*** | (not set)`. Non-destructive.

**Traceability** — PRD **7.2**, **7.11**. **NEW.**

---

#### 2.4.16 `SET ENV` — write a shell environment variable

| | |
|---|---|
| Command | `ENV` |
| Root command | `SET` |
| Description | Set a shell environment variable (the framework's built-in `SET`, preserved under the new root) |
| Usage prototype | `SET ENV <varname> <value>` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Env", "Set a shell environment value", Prototype = "SET ENV <varname> <value>")]
[CommandParameterOrdered("key", "Key used to access value")]
[CommandParameterOrdered("value", "Value stored for accessing", UsePipe = true)]
[CommandHelpRemarks("This command modifies the shell environment outside its own context.")]
[CommandHelpRemarks("Piped input is accumulated: the variable is cleared, then every chunk is appended.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `key` | ordered | `string` | yes | — | any environment variable name | Variable to write |
| `value` | ordered, `UsePipe = true` | `string` | yes (unless piped) | — | any text | Value, or the accumulated pipe contents |

**Pipeline behaviour** — **accepts, produces nothing visible.** Exactly the framework built-in's contract: `OnStartPipe` clears the variable; each chunk **appends** to it; every result is an empty success, which the host drops, so the tool is silent. Non-piped invocation writes once and returns an empty success. `ResultFormat.General`.

**Environment interaction** — writes an arbitrary **global** environment key, so it **must** be registered with `modifiesEnvironment: true` — this is the one tool in the package whose whole purpose is a global write. It reads nothing.

**Failure modes** — missing key or value with no pipe: parse-time required-parameter failure (the executor's generic message; this matches the built-in exactly and the prototype is the cue). Nothing else can fail.

**Security and audit** — a user can put anything in an environment variable, including a token. Environment writes **are** audit-logged by the framework's `EnvironmentContext.SetValue` → `LogEnvironmentChange`, and that path *does* redact by variable name — so `SET ENV CHATDBG_AZURE_API_KEY …` is masked in the audit record even though the command-line parameter is not (§2.6.7). The help text warns that the value is visible in shell history regardless.

**Traceability** — PRD **7.1**. Descends from the **framework's** built-in `SET` (`Xcaciv.Command/Commands/SetCommand.cs`), not from ChatDbg. It exists solely so that claiming `SET` as a root command costs the user nothing (§2.1.1).

---

### 2.5 Pipeline compositions

Six worked examples. The pipeline parser splits on `|`; each stage runs in its own child environment and its own child IO context, and chunks stream lazily through a bounded channel, so a long listing reaches the next stage before the first stage finishes.

**1. What have I actually changed?**

```
SET KEYS -changed -format csv | REGIF "llama"
```

`SET KEYS` emits one CSV row per key whose current value differs from the built-in default; the framework's built-in `REGIF` passes through only rows matching `llama` and returns empty success for the rest, which the host drops. **The user gets** a short list of exactly the local-model tunables they have drifted from stock — the fastest possible answer to "why is my GGUF rig slow and my colleague's is not". In the source this required reading a 46-line dump and remembering eight defaults.

**2. Validate the live record before blaming the provider.**

```
SET SHOW -format json | SET VALIDATE -strict
```

Stage one emits the active record as one JSON chunk with the document's wire names. Stage two treats that chunk as a complete document, runs every check in §2.4.8, and — because `-strict` is set — returns a **failure** if anything is wrong, with the finding list as output. **The user gets** silence when the configuration is sound and a precise finding list when it is not: `provider 'Azure' will not match any known provider; use 'azure'`, `modelId '/models/mistral.gguf' does not exist`, `useOsCredentialStore is true but no store is available on linux`. This is the composition a host should run at startup in place of the source's ad-hoc self-check.

**3. Which of my profiles have drifted from what I am running?**

```
SET PROFILES | SET DIFF -against active -format csv
```

`SET PROFILES` emits one bare profile name per chunk. `SET DIFF` treats each chunk as a left-hand side, compares it to the active configuration, and emits one row per differing key — **and emits nothing at all for a profile that matches**. **The user gets** a compact drift report across every saved profile, with the identical ones silently absent.

**4. Promote a profile after checking it, without switching to it.**

```
SET SHOW -profile gpu-rig -format json | SET IMPORT -profile staging -merge -dryrun
```

Stage one renders a profile the user is *not* running. Stage two validates that document against the key catalog and reports exactly which keys in `staging` would change — `imported llamaGpuLayerCount: 0 -> 32` — **and writes nothing**, because of `-dryrun`. **The user gets** a reviewable change list before committing. Drop `-dryrun` to apply it.

**5. Cross-package — resolve the configured system prompt end to end.**

```
SET GET systemPrompt | PROMPT SHOW
```

`SET GET` emits the bare prompt **name** with no label — which is why it is designed to emit a bare scalar. `PROMPT SHOW` (package `ChatDbg.Tools.Prompts`, PRD 7.5) takes one prompt name per chunk and emits that prompt's body. **The user gets** the actual text their next chat turn will be primed with. The source could not do this: the prompt body was resolved once at startup, never persisted, and never printable from the settings surface.

**6. Cross-package — configuration handed to a diagnosis.**

```
SET SHOW -changed -format json | CHAT ASK "Why would this configuration produce empty log-probability output?"
```

Stage one emits only the drift, as JSON, with every credential rendered as status-and-provenance and no value anywhere. Stage two (`ChatDbg.Tools.History` / session package, PRD 7.4 and 7.6) sends it as context alongside the question. **The user gets** a model answer grounded in their real configuration — and, because `SET SHOW` cannot emit a secret under any flag, a configuration they can paste into a bug report or a chat with a stranger without auditing it first. That property is a design constraint of this package, not a happy accident: it is why the credential rows are provenance-only in every one of the four output formats.

---

### 2.6 Design notes for the architect

#### 2.6.1 The state this package holds — and the one it must not

**Holds:** one in-memory settings record per process, loaded once, plus the resolved paths and the active profile name. That is all.

**Must not hold:** a shared mutable object handed out to other packages. The source made the settings record "the integration bus between commands" — every command was constructed with a reference to one `ChatSettings` instance — and it produced the product's worst bug: the windowed shell built its commands against a defaults record and then rebound the *name* to the loaded record, so `/set`, `/model`, `/logprobs` and `/prompt use` all mutated and persisted an object the UI never read, silently overwriting the user's document with mostly-default values on every command (quirk Q4, and its visible symptom in the Change Model dialog, Q26). Rebinding a name does not rebind captured references.

The rebuild's answer is that **the environment is the bus**. This package publishes the effective configuration into the controller environment as `CHATDBG_*` keys on every successful write, and every other package reads its configuration from `IEnvironmentContext` with a documented default — `env.GetValue("CHATDBG_TEMPERATURE", "0.7", storeDefault: false)`. That gives three properties the source lacked: there is exactly one writer; a reader that starts late still gets the current value; and no reader can mutate what it read. Note `GetValue`'s `storeDefault` defaults to **true**, which would flip `HasChanged` and cause a write-back on a mere read — every consumer must pass `storeDefault: false`, and that belongs in the cross-package conventions chapter.

The six tools that publish (`SET VALUE`, `SET UNSET`, `SET RESET`, `SET IMPORT`, `SET PROFILE`, and `SET ENV`) must be registered by the host with `modifiesEnvironment: true`; the other ten must not be. `ModifiesEnvironment` is a property of the *registration*, not of the class — there is no attribute for it — so this package must ship a documented registration manifest and the host's composition root must honour it. A tool registered without the flag can still persist its own state under a `SET_`-prefixed key; that is how the read-only tools cache resolved paths.

#### 2.6.2 One validator, three surfaces

The source had the same bound written out in up to four places with three different wordings: `/set logProbabilitiesTopK 0` said `LogProbabilitiesTopK must be a number between 1 and 20` while `/logprobs top 0` said `Top-K value must be a number between 1 and 20`, and the windowed dialog silently clamped instead of saying anything. The rebuild has exactly one key catalog (§2.3), one validator that consumes it, and one message per key. `LOGPROB`'s convenience verbs, the future GUI, and `SET VALUE` all call the same validator and produce the same sentence. `SET KEYS` emits that catalog at runtime so help text cannot drift from behaviour.

#### 2.6.3 Testability

- **The store is a port.** `ISettingsStore` has the six operations the source's persistence interface had — save, load, report path, and the three that were credential-flavoured are *gone* from this package (they moved to `CRED`, which is where their interactive terminal I/O belongs). What remains is pure: load, save, resolve path, list/read/write/delete profile. No tool touches `System.IO` directly, so every tool is unit-testable against an in-memory store, and the source's "did it save exactly once" assertions remain expressible verbatim.
- **No terminal I/O inside a service.** The source's settings service read `Console.ReadLine()` inside migration and keystore-enable flows, which meant those flows were invisible and unanswerable under the windowed shell (its Migrate button called straight into a blocking stdin read while the UI owned the screen). Nothing in this package prompts. Confirmation is a **flag** (`-force`) plus an environment switch (`SET_CONFIRM`), which works identically in a REPL, a full-screen shell, a pipeline and a test.
- **The clock and the platform are ports too** — profile `lastUsed` timestamps and the "is an OS credential store available" predicate both come through injected abstractions, so a test can pin them.
- **Parameter parsing is the framework's**, not the package's. There is no hand-rolled `string.Split`; the source's ad-hoc splitting is what produced the "value is everything after the key, re-joined with single spaces, runs of spaces collapse" quirk. That behaviour is preserved deliberately (it is what a suffix parameter does), but now it is the framework's documented behaviour rather than an accident.

#### 2.6.4 Reject or clamp — one policy, made visible

The source ran two contradictory out-of-range policies: text commands rejected, the windowed dialog clamped to the same bounds and saved silently, and additionally ignored unparseable input without a word. A user who typed `5` into the temperature box got `2` and no notification; the same `5` typed at the prompt got an error.

The rebuild picks **reject** as the default (`SET_STRICT=true`) and makes clamping an explicit, always-reported opt-in (`-clamp`, or `SET_STRICT=false` for a host that wants dialog-like behaviour globally). Unparseable input is **never** silently ignored under either policy. A GUI built on this package therefore behaves the same as the prompt unless it deliberately sets `SET_STRICT=false`, and even then the user is told about every clamp.

#### 2.6.5 Cross-platform behaviour

Everything in this package is portable except the one predicate it delegates. Concretely:

| Capability | Windows | Linux / macOS |
|---|---|---|
| Settings document location | `%USERPROFILE%\.ChatDbg\settings.json` | `$HOME/.ChatDbg/settings.json` — the same literal dotted directory name, which ports unchanged |
| Base-directory fallback | OS temp directory when the profile lookup is empty or throws | identical |
| Every range, default, message and format | identical | identical |
| `useOsCredentialStore` | `CRED` reports available (Credential Manager) | `CRED` reports available where a Secret Service or Keychain backend is present; otherwise the key can be read but not set to `true`, with the message `No OS credential store is available on this platform (<os>).` |
| A document carrying `useWindowsCredentialManager: true` copied from Windows to Linux | n/a | `SET VALIDATE` reports it as a finding and `SET SHOW` renders `- OS Credential Store: Enabled (no store on this platform)`. It does **not** silently keep warning on every launch forever as the source did, and it does not auto-clear the user's setting either — a machine-specific fact must not rewrite a portable document. |
| Path separators, case sensitivity | joined platform-neutrally; profile names are matched case-insensitively and stored lowercase so a profile set does not fracture between an NTFS and an ext4 home directory | identical |

The source's windowed dialog was the one place that skipped the platform gate entirely — it wrote the keystore toggle with no check, and its credential sub-dialog reported `Credential saved successfully` even where no keystore existed (quirks Q18, Q19). There is no surface in this package that can turn the toggle on where the capability is absent.

#### 2.6.6 Where to degrade rather than fail

Degrading is the default posture; failing is reserved for a request that cannot be honoured.

| Condition | Behaviour |
|---|---|
| Settings document absent | Create it with all built-in defaults, silently, on first resolution. Source parity. |
| Settings document corrupt or unparseable | **Degrade.** Read tools succeed against built-in defaults with a visible one-line warning; the corrupt file is left untouched, never deleted, never auto-rewritten. A write tool refuses until the user runs `SET RESET all -force -backup` or `SET IMPORT`, so a fat-fingered `SET VALUE` cannot silently discard a document the user could still repair by hand. This is a deliberate tightening: the source would happily overwrite the corrupt file on the next successful `/set`. |
| User profile directory unresolvable | **Degrade** to the OS temp directory and say so in `SET PATH`. Source parity. |
| Base directory read-only (`SET_READONLY=true`, or a write that fails with a permission error) | **Degrade to read-only mode**: the six read tools work normally; the ten mutating tools return one clear failure explaining that the change is active for this session only. Never a crash, never a silent success. |
| Profile directory missing | **Degrade**: the world contains exactly one profile, `default`, which is the settings document itself. |
| `ChatDbg.Tools.Prompts` not loaded | **Degrade**: `systemPrompt` names are stored unvalidated, with a one-line note. Source parity — the source did exactly this when the prompt service was absent. |
| `ChatDbg.Tools.Credentials` not loaded | **Degrade**: the credential section of `SET SHOW` renders `(provenance unavailable — CRED tools not loaded)` and `useOsCredentialStore` cannot be set to `true`. Configuration viewing must never depend on the credential package being present. |
| A capability is absent on the current back end (e.g. log probabilities on a provider that cannot return them) | **Store it anyway, and annotate.** `SET VALUE enableLogProbabilities true` succeeds and appends `Note: the active provider (<p>) does not report token probabilities.` This package's job is to record intent; the provider package's job is to report capability. Refusing to store a value because today's provider ignores it would make profiles useless — a profile is written for the back end it targets, not the one currently selected. The three local-model tunables the runtime does not yet consume are annotated the same way. |
| Whole-record reset, profile deletion, non-clean import | **Fail closed** without `-force`. These are the only four places the package refuses to act on a well-formed request. |

#### 2.6.7 Audit and masking — what the framework will not do for you

`AuditMaskingConfiguration` only rewrites tokens of the form `-name=value`; the framework's own `-name value` syntax is **not** masked, and the argument tokenizer strips `=` anyway. Treat command-line parameter masking as non-functional. Three consequences are baked into this package's design:

1. **No tool accepts a secret as a parameter.** That is enforced by the write-blocked keys, and it is the reason `SET VALUE azureApiKey …` fails rather than storing.
2. **Environment-change auditing does work**, because `StructuredAuditLogger.LogEnvironmentChange` redacts by *variable name*. So `SET ENV` is genuinely masked where a hypothetical `SET VALUE apikey` would not have been.
3. The package still contributes `azureApiKey`, `awsAccessKey`, `awsSecretKey` to `RedactedParameterNames` — belt and braces for a host that fixes the masker later — and records the `azureEndpoint` value in audit metadata **hashed**, since an endpoint occasionally carries an embedded token.

The host should enable `StructuredAuditLogger` and assign it explicitly to the controller; DI registration alone does not push it onto `CommandController`, which initialises its own `NoOpAuditLogger`.

#### 2.6.8 Loading, registration and packaging

- The assembly references `Xcaciv.Command.Interface` and `Xcaciv.Command.Core` only, and must **not** ship a private copy of the interface assembly — the crawler reports exactly that as a `ReflectionTypeLoadException` cause.
- Under `Xcaciv.Loader`, this package needs no elevated policy: load it with a per-instance `AssemblySecurityPolicy` carrying the host's forbidden-directory list and `DisallowDynamicAssemblies = true`, `basePathRestriction` set to the tools directory (**never** `"*"`), `learningMode: false` on the integrity verifier, discovery by contract (`GetTypes<ICommandDelegate>()`) rather than by class name, and one collectible context per package disposed deterministically.
- The registration manifest the host must honour: 16 sub-commands under root `SET`; `modifiesEnvironment: true` for `VALUE`, `UNSET`, `RESET`, `IMPORT`, `PROFILE`, `ENV`; `false` for the rest. Load order matters exactly once: built-ins first, this package second (§2.1.1).
- **Do not register these commands as DI singletons.** `SET VALUE`, `SET UNSET` and `SET IMPORT` hold per-pipe transaction state across `OnStartPipe` → `HandlePipedChunk` → `OnEndPipe`; a singleton instance would leak that state between runs and between pipeline stages. Transient only.
- `CommandExecutor` does **not** dispose the command instance it executes. Any tool that opens a write transaction must therefore also close it in `OnEndPipe` and in the non-piped path — never rely on `DisposeAsync` to flush a pending document write.

#### 2.6.9 Open questions worth a decision before implementation

1. **Environment prefix for sub-commands.** The framework seeds `GetDefaultEnvironment()` values under the command name and hands the child context keys prefixed `{COMMANDNAME}_`; for a sub-command it is not settled in the reference whether that name is the root (`SET`) or the sub-command (`SHOW`). This spec assumes the root, and every tool reads defensively — `SET_FILE` first, bare `FILE` second — until the host pins it with a test.
2. **YAML.** `-format yaml` costs a `YamlDotNet` dependency in a package that otherwise has none. Ship JSON and CSV in v1; add YAML only if a real user asks.
3. **Profile inheritance.** A `base` field on a profile document (profile B inherits A and overrides three keys) is the obvious next step and is deliberately *not* in v1: it turns validation into a graph problem and `SET DIFF` into a three-way merge. Revisit once profiles have users.
