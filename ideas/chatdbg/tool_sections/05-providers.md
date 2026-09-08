## 5. ChatDbg.Tools.ModelBackends — Model Backends

### Purpose and boundary

**What this package owns.** Everything about *which model answers, on what hardware, and whether it can do what you are about to ask of it.* Concretely:

1. **The backend registry** — enumerating the model backends the host has loaded (hosted Azure OpenAI, Amazon Bedrock, a locally-run GGUF engine, plus any backend contributed later by a dropped-in package), their display names, their registry keys, and their readiness.
2. **Selection** — switching the active backend, and choosing which model/deployment/foundation-model/weight-file that backend should use.
3. **Discovery** — listing the models a backend can actually serve, rather than making the user guess a free-text identifier.
4. **Verification** — probing connectivity, credential resolution and entitlement *before* a chat turn burns a conversation on a misconfiguration.
5. **Capability reporting** — answering, per backend and per model, whether token log-probabilities, streaming, tool calling and prompt-side probabilities are available, and with what ceilings.
6. **Local engine lifecycle** — loading a weight file into memory with its hardware-acceleration settings, reporting what is resident, retuning the acceleration knobs, and unloading.

**What this package explicitly does NOT own.**

| Not owned | Owner | Why the split |
|---|---|---|
| The settings store, the `SET`-style key/value surface, persistence, and generation parameters (`temperature`, `maxTokens`, `logProbabilitiesTopK`, system-prompt name) | **ChatDbg.Tools.Settings** (root `SET`, PRD 7.2) | MODEL *reads* these and *writes three of them through* that package's environment keys; it never opens the settings document. The source's single 464-line `SetCommand` conflated all of this — the rebuild does not. |
| Secret acquisition, storage, the OS keystore, migration, and the "where did this credential come from" label | **ChatDbg.Tools.Credentials** (root `CRED`, PRD 7.3) | MODEL consumes a *resolved* secret and a *source label*. No tool in this package accepts, prints, logs or returns a secret value. See **Security and audit** on every tool. |
| Chat turns, conversation history, injection/pop/import/export | **ChatDbg.Tools.History** (root `CHAT`, PRD 7.4) | MODEL never sends a conversation. `MODEL TEST -deep` sends a synthetic one-token probe that is never appended to history. |
| System prompt bodies and their catalogue | **ChatDbg.Tools.Prompts** (root `PROMPT`, PRD 7.5) | The local engine binds a system prompt at load time; MODEL asks the prompt package for the resolved body, it does not resolve names. |
| Token log-probability maths, the top-K analysis surface, tokenization and attribution | **ChatDbg.Tools.Tokens** (root `TOKEN`, PRD 7.9/7.10) | MODEL reports *whether* a backend can produce log-probabilities. What is done with them is that package's business. |
| Diagnostic log capture, buffering, daily files and log export | **ChatDbg.Tools.Diagnostics** (root `DIAG`, PRD 7.11) | The local engine's native log stream is *emitted* here and *owned* there. `MODEL LOAD` writes lines into the diagnostics sink; it has no `export-logs` verb. |
| Colour, heat-maps, grids, tables, theming | **ChatDbg.Tools.Render** / host `IIoContext` (PRD 7.12) | Every tool here emits plain rows plus a declared `ResultFormat`; the host decides how they look. |

---

### Package manifest

The package ships as **two assemblies under one root command**, because the framework merges sub-commands into an existing root description (`CommandRegistry.AddCommand(ICommandDescription)` merges sub-command dictionaries when the root already exists; `CommandParameters.CreatePackageDescription` synthesises or merges the root). This lets a hardened host load the cloud half and refuse the native half.

| Property | `ChatDbg.Tools.ModelBackends` (primary) | `ChatDbg.Tools.ModelBackends.Local` (satellite) |
|---|---|---|
| Assembly / package id | `ChatDbg.Tools.ModelBackends` | `ChatDbg.Tools.ModelBackends.Local` |
| Root command | `MODEL` | `MODEL` (merged into the same root) |
| Tools contributed | `LIST`, `SHOW`, `USE`, `SELECT`, `CATALOG`, `CAPS`, `TEST` | `LOAD`, `UNLOAD`, `STATUS`, `TUNE` |
| Contract assemblies targeted | `Xcaciv.Command.Interface` **3.3.4** + `Xcaciv.Command.Core` **3.3.4** (no reference to `Xcaciv.Command`, none to the host) | same |
| Target framework | `net10.0`, `ImplicitUsings`, `Nullable` enabled, no `AllowUnsafeBlocks` | `net10.0`, `ImplicitUsings`, `Nullable` enabled |
| Elevated trust required | **No.** Managed code only: HTTP/TLS, JSON, no reflection-emit, no P/Invoke | **Yes, effectively.** Loads a native shared library (`llama.dll` / `libllama.so` / `libllama.dylib`) via the engine binding; uses `dynamic`/reflection on the engine's context type |
| Network reach | Outbound HTTPS to the configured Azure OpenAI endpoint host and to the AWS Bedrock regional endpoints only. No other host. No inbound | **None** |
| Filesystem reach | Read-only: nothing outside the catalog cache file it owns (`<app-data>/ChatDbg/catalog.json`) | Read: the weight file and its directory; the model-search roots. Write: none of its own (the log sink belongs to `DIAG`) |
| OS keystore | **Indirect only** — through `ChatDbg.Tools.Credentials`. This package never calls a credential API | None |
| Native libraries | None | The inference backend native set for the running RID (CPU baseline; CUDA / Vulkan / Metal when present) |
| Safe to load in a restricted host | **Yes.** Loads cleanly under `AssemblySecurityPolicy.Strict` with `DisallowDynamicAssemblies = true`, `EnforceBasePathRestriction = true`, and an integrity allow-list | **No.** Preflight under `Strict` will reject the engine binding's dependencies, and a fault inside the native layer is an uncatchable process kill. Load it only in a host that has accepted in-process native code, or run it out of process (see **Design notes**) |
| Audit posture | Every tool is registered so that its `AuditEvent.Parameters` can be logged verbatim: **no tool in this package ever accepts a secret as a parameter** | same |
| Environment-modifying registration | `USE`, `SELECT` → `modifiesEnvironment: true` | `TUNE` → `modifiesEnvironment: true`; `LOAD`, `UNLOAD`, `STATUS` → `false` |

> **Framework caveat carried into the design.** `ICommandController.AddCommand(packageKey, ICommandDelegate, bool)` keeps only the instance's `Type`; a *fresh* instance is constructed per execution and `CommandExecutor` never disposes it. No tool here may hold live state on the instance. The resident local model, the HTTP transports and the catalog cache all live in a package-internal process singleton reached through the host's `IServiceProvider` (or, in a DI-free host, a static accessor inside the satellite assembly). See **Design notes — what state this package holds**.

---

### Tool catalog

Registration shape shared by every tool in the package:

```csharp
[CommandRoot("Model", "Model backends: registry, selection, capabilities and local engine lifecycle")]
[CommandRegister("<Verb>", "<one line>", Prototype = "MODEL <VERB> …", Version = "1.0.0")]
```

`CommandRootAttribute.Command` and `CommandRegisterAttribute.Command` are normalised to **UPPERCASE** by `NamesValidator`; parameter `Name`s are normalised to **lowercase**. Invocation is therefore `model list`, `MODEL LIST`, `Model List` — all equivalent. The parameter dictionary handed to `HandleExecution` is `StringComparer.OrdinalIgnoreCase`.

---

#### 5.1 `MODEL LIST` — enumerate the registered backends

| | |
|---|---|
| Command | `LIST` |
| Root command | `MODEL` |
| Description | List every registered model backend with its key, display name and readiness |
| Prototype | `MODEL LIST [-only all\|configured\|unconfigured\|active\|unavailable] [-format text\|keys\|csv\|json] [-v]` |

```csharp
[CommandRoot("Model", "Model backends: registry, selection, capabilities and local engine lifecycle")]
[CommandRegister("List", "List every registered model backend with its key, display name and readiness",
    Prototype = "MODEL LIST [-only all|configured|unconfigured|active|unavailable] [-format text|keys|csv|json] [-v]")]
[CommandParameterNamed("only", "Which backends to include",
    AllowedValues = new[] { "all", "configured", "unconfigured", "active", "unavailable" })]
[CommandParameterNamed("format", "Output shape",
    AllowedValues = new[] { "text", "keys", "csv", "json" })]
[CommandFlag("verbose", "Include endpoint/region/model-path detail on each row", ShortAlias = "v")]
[CommandHelpRemarks("Emits ONE CHUNK PER BACKEND, so it composes directly into MODEL TEST, MODEL CAPS and MODEL USE.")]
[CommandHelpRemarks("An empty registry is not an error: the tool reports it and names the recovery command.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `only` | named | `string` | no | `all` (first allowed value auto-becomes the default) | `all`, `configured`, `unconfigured`, `active`, `unavailable` | Which backends to include |
| `format` | named | `string` | no | `text` | `text`, `keys`, `csv`, `json` | Output shape. `keys` emits the bare registry key per chunk — the pipe-friendly form |
| `verbose` / `-v` | flag | `bool` (always) | no | `false` | — | Append endpoint host, region or weight-file path to each row (never a secret) |

**Pipeline behaviour.** Produces piped output; does **not** accept piped input (a piped invocation returns an explanatory chunk rather than throwing, per the framework convention). It emits **one chunk per backend**, which `AbstractCommand`'s non-piped path cannot do — the template method yields exactly one chunk when `HasPipedInput` is false. `LIST` therefore **overrides `Main`** (legal; the method is not sealed) and yields a `CommandResult<string>.Success(row, OutputFormat)` per backend, plus a final zero-length success (silently dropped by the host) when the registry is empty and a message chunk has already been emitted. `OutputFormat` is set from `-format`: `ResultFormat.General` for `text`/`keys`, `ResultFormat.CSV` for `csv`, `ResultFormat.JSON` for `json`, so downstream stages can read `pipedChunk.OutputFormat` and the host renderer can pick a table style. (The framework does not act on `OutputFormat` itself — encoding is the host's job — so `-format` also genuinely changes the text.)

**Environment interaction.** Reads (with `storeDefault: false`): `CHATDBG_PROVIDER` (to mark the active row). Writes nothing. Declares `GetDefaultEnvironment()` → `{ "FORMAT", "text" }` so a host can pin a house default; remember the host stores that as `LIST_FORMAT` and the tool reads the prefixed key. Does **not** need environment-modifying permission.

**Failure modes.**

| Condition | Behaviour | User sees |
|---|---|---|
| No backends registered | Not an error. One informational chunk, then normal completion | `No model backends are loaded. Drop a backend package under .\packages\<name>\bin, then restart, or run 'PACKAGE SEARCH model' to find one.` |
| A backend's readiness probe throws | The row is still emitted, marked `error` with the exception's message truncated to 200 chars | `bedrock   Amazon Bedrock        error: <short reason>` |
| A backend has no native runtime for this RID (local engine on an unsupported RID) | Row marked `unavailable (<reason>)`; never an exception | `llama     Local LLM (GGUF)      unavailable (no native runtime for linux-musl-arm64)` |
| `-only` given an unlisted value | Framework rejects at parse time with `ArgumentException` before `HandleExecution`; the host reduces it to `Error executing LIST (see trace for more info)` and traces the specific message | Generic failure line; the specific reason is in the trace. Mitigated by listing the allowed values in the help text and in `Prototype` |
| Invoked with piped input | Explanatory chunk, no throw | `MODEL LIST does not consume piped input. Did you mean 'MODEL CAPS' or 'MODEL TEST'?` |

**Security and audit.** No parameter or output carries a secret; `-v` prints endpoint *hosts* and file *paths*, never keys, and never the credential value or the settings-file secret slot. Non-destructive; no confirmation. Safe to audit-log verbatim.

**Traceability.** PRD **7.6** (AI Provider Abstraction) with reads from **7.2**. Descends from the shells' hard-wired provider registry (`ChatShell.InitializeAIServices` → `Dictionary<string, IAIService>` keyed `azure`/`bedrock`/`llama`) and from the start-up configuration self-check block that printed `Warning: Unknown AI provider: …` / `Warning: <name> service is not configured.`. **Improvement over source (deliberate):** the source registry was fixed at compile time in two places that disagreed (one dead shell class registered only two of three backends); this registry is whatever the loader found, and there is exactly one of it.

---

#### 5.2 `MODEL SHOW` — describe one backend's effective configuration

| | |
|---|---|
| Command | `SHOW` |
| Root command | `MODEL` |
| Description | Show the active (or named) backend's configuration, effective request parameters and credential source |
| Prototype | `MODEL SHOW [<backend>] [-format text\|csv\|json] [-effective]` |

```csharp
[CommandRegister("Show", "Show a backend's configuration, effective request parameters and credential source",
    Prototype = "MODEL SHOW [<backend>] [-format text|csv|json] [-effective]")]
[CommandParameterOrdered("backend", "Registry key of the backend (default: the active one)",
    IsRequired = false, UsePipe = true)]
[CommandParameterNamed("format", "Output shape", AllowedValues = new[] { "text", "csv", "json" })]
[CommandFlag("effective", "Show the values that would actually go on the wire after backend-specific clamping")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `backend` | ordered | `string` | **no** (`IsRequired = false` — ordered parameters are required by default, so this is an explicit opt-out) | the value of `CHATDBG_PROVIDER`, else `azure` | any registered key, case-insensitive | Which backend to describe |
| `format` | named | `string` | no | `text` | `text`, `csv`, `json` | Output shape |
| `effective` | flag | `bool` | no | `false` | — | Apply backend/model-specific clamping before printing (e.g. Anthropic-on-Bedrock caps temperature at 1.0 even though the product accepts 0.0–2.0) |

Fields reported (all read-only, all from the settings package):

| Field | Source default | Range enforced by `SET` | Note |
|---|---|---|---|
| provider | `azure` | `azure` \| `bedrock` \| `llama` + any loaded key | lookup is case-insensitive (the source's console shell was case-sensitive, the GUI was not — unified here) |
| model id / deployment / weight path | `gpt-4` | free text | for `bedrock` the id doubles as the foundation-model id; for `llama` it is a filesystem path |
| azure endpoint | `null` | unvalidated free text | printed host-only unless `-effective` |
| azure api version | `2023-12-01-preview` | — | source hard-coded this literal; here it is a settings key with that value as default |
| aws region | `us-east-1` | unvalidated free text | |
| temperature | `0.7` | `0.0`–`2.0` inclusive | `-effective` shows `1.0` for `anthropic.*` on Bedrock and flags the clamp |
| max tokens | `1000` | `1`–`8192` inclusive | source sent this to Azure **only** when log-probabilities were on; the rebuild always sends it and says so |
| log-probabilities enabled | `false` | `true`/`false` | |
| log-probabilities top-K | `5` | `1`–`20` inclusive | this value also constrains the *local sampler*, not only reporting — see `MODEL CAPS` |
| llama context size | `4096` | `512`–`32768` inclusive | |
| llama gpu layers | `0` (CPU only) | `0`–`100` inclusive | |
| llama gpu device | *(empty)* | free text, e.g. `0` or `0,1` | |
| llama threads | `0` (= host default) | `0`–`64` inclusive | |
| llama batch size | `512` | `1`–`2048` inclusive | |
| credential source | `not set` | — | one of `environment variable (<NAME>)`, `OS keystore`, `settings file (deprecated)`, `not set`, `unavailable on this platform` |

**Pipeline behaviour.** Both. As a source it emits one chunk (one record). As a sink it accepts piped input where **one chunk is one backend key**, describing each in turn — so `MODEL LIST -format keys | MODEL SHOW` prints a full configuration dump. `backend` carries `UsePipe = true`, so when piped the framework does not demand it on the command line. `OutputFormat` follows `-format`.

**Environment interaction.** Reads every key in the table above from the shell environment context with `storeDefault: false` (a plain read must not mark the environment changed). Reads the OS **process** environment only indirectly, by asking the credentials package for the *source label* — never the value. Writes nothing; not environment-modifying.

**Failure modes.** Unknown backend key → a `Failure` chunk `Unknown backend '<k>'. Registered: azure, bedrock, llama.` (not an exception — failures are data). Backend registered but unavailable on this platform → a normal success row with `status: unavailable (<reason>)`. A piped failure chunk is forwarded verbatim by `AbstractCommand.Main` before `HandlePipedChunk` is ever called, so upstream errors pass through unaltered and are not misreported as configuration problems.

**Security and audit.** Prints the credential **source label** only. Endpoint is shown host-only (`example.openai.azure.com`) unless `-effective`, which shows the full composed request URL *with the deployment segment* but still no key. Never prints the deprecated settings-file secret slots even when populated. Non-destructive.

**Traceability.** PRD **7.6**, **7.7**, **7.8**, reading **7.2**/**7.3**. Descends from `/set` with no arguments (the settings listing, which printed `- AWS Access Key: ***set*** [environment variable (…)]`), from the console shell's start-up banner (Provider / Model / System Prompt / settings-file path) and from its `LLama Configuration:` block.

---

#### 5.3 `MODEL USE` — switch the active backend

| | |
|---|---|
| Command | `USE` |
| Root command | `MODEL` |
| Description | Make a registered backend the active one and report its readiness |
| Prototype | `MODEL USE <backend> [-no-verify] [-yes]` |

```csharp
[CommandRegister("Use", "Make a registered backend the active one and report its readiness",
    Prototype = "MODEL USE <backend> [-no-verify] [-yes]")]
[CommandParameterOrdered("backend", "Registry key of the backend to activate", UsePipe = true)]
[CommandFlag("no-verify", "Switch without running the readiness check")]
[CommandFlag("yes", "Do not prompt when switching away from a backend with a resident local model")]
[CommandHelpRemarks("Registered with modifiesEnvironment: true — this is one of only three tools here that write a global setting.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `backend` | ordered | `string` | **yes** (ordered default) | — | any registered key; built-ins `azure`, `bedrock`, `llama`; matched case-insensitively | Backend to activate |
| `no-verify` | flag | `bool` | no | `false` | — | Skip the post-switch readiness report (useful in scripts) — **NEW** |
| `yes` | flag | `bool` | no | `false` | — | Confirm, non-interactively, that a resident local model may stay loaded / be dropped — **NEW** |

> **Why `backend` is not declared with `AllowedValues`.** The source restricted the value to the literal set `azure|bedrock|llama` and rejected anything else. `AllowedValues` is an `init`-only compile-time array; a registry that grows by dropping a package into `.\packages` cannot be expressed that way. Validation is therefore done against the live registry inside the tool, and the built-in three are named in `ValueDescription`, `Prototype` and the failure message so the user still sees a closed list when only the built-ins are loaded.

**Pipeline behaviour.** Accepts piped input and produces piped output. **One chunk = one backend key**; the tool switches to it and emits a one-line confirmation. Piping more than one key is legal and the last one wins — the emitted rows make the sequence visible, which is why the tool does not silently swallow all but the last. `backend` is the `UsePipe = true` parameter (there is at most one per command by convention). `OutputFormat = General`.

**Environment interaction.** Writes `CHATDBG_PROVIDER` (lower-cased, exactly as the source stored it). Because that is a **global** key with no command-name prefix, the tool must be registered `modifiesEnvironment: true`, otherwise the host routes the write into this command's private bucket and the change is invisible to every other tool. Also writes its own audit breadcrumb `USE_PREVIOUS` (command-prefixed, so it persists without needing global rights). Reads `CHATDBG_PROVIDER` with `storeDefault: false` before overwriting so it can report the transition.

**Failure modes.**

| Condition | Behaviour |
|---|---|
| Unknown key | `Failure` chunk: `Unknown backend 'x'. Registered: azure, bedrock, llama. Run 'MODEL LIST' to see them all.` Nothing is written. |
| Known key, backend not configured | The switch **still happens** (matching the source, where `/set provider` never required readiness), followed by a warning row naming the remediation route for that backend — environment-variable names for cloud backends, `MODEL SELECT <path>` for the local one. Exit is a success chunk plus a warning chunk, not a failure. |
| Switch lands on a backend whose declared capability record cannot supply token log probabilities **while capture is on** | **Owner decision D-001:** the switch succeeds, then this tool **turns capture off** (`TOKEN_ENABLED` → `false`, persisted) and appends the auto-disable notice chunk: `Token log probabilities disabled: provider '{provider}' does not support them. Use a provider that does (see MODEL CAPS){, e.g. '{example}'}.` — naming a configured capable backend where one exists. Informational, not a failure; the turn loop is unaffected. The same rule runs at session start (§A.3) when loaded settings combine capture-on with a capability-absent provider. |
| Known key, backend unavailable on this platform | Switch is **refused** with a failure chunk naming the reason (`no native runtime for <rid>`), because activating it guarantees every subsequent turn fails. |
| A local model is resident and the target is a cloud backend | Interactive host (`HasPipedInput == false`): prompt `Unload the resident model '<name>' (frees <n> MB)? [y/N]`. Non-interactive: **requires `-yes`**, otherwise refuses with a failure chunk explaining that `PromptForCommand` is only meaningful off-pipe. Default answer is *no* — the model stays resident so switching back is instant. |
| Downstream stage fails after the switch | Nothing is rolled back. The switch is already committed to the environment; the failure chunk propagates. Documented, not repaired: rollback across pipeline stages is not something the framework offers. |

**Security and audit.** No secret. **Environment-modifying → audited twice:** once as an `AuditEvent` for the execution, and once as a `LogEnvironmentChange` for `CHATDBG_PROVIDER` (the environment-change path is the one where the framework's redaction actually works, since it matches on the variable name). Not destructive on its own; destructive only through the optional unload, which is confirmation-gated.

**Traceability.** PRD **7.6** (with **7.1** for dispatch). Descends from `/set provider <azure|bedrock|llama>` (`Provider must be 'azure', 'bedrock', or 'llama'`).

---

#### 5.4 `MODEL SELECT` — choose the model the active backend will serve

| | |
|---|---|
| Command | `SELECT` |
| Root command | `MODEL` |
| Description | Set the model id, deployment name or weight-file handle for a backend |
| Prototype | `MODEL SELECT <model> [-revision <r>] [-backend <key>] [-force]` |

```csharp
[CommandRegister("Select", "Set the model id, deployment name or weight-file handle for a backend",
    Prototype = "MODEL SELECT <model> [-revision <r>] [-backend <key>] [-force]")]
[CommandParameterOrdered("model", "Catalog handle, model id, deployment name, or weight-file handle", UsePipe = true)]
[CommandParameterNamed("revision", "Version suffix to re-attach after the ':' the tokenizer removes (e.g. 0 for '…-v1:0')")]
[CommandParameterNamed("backend", "Backend to change (default: the active one)")]
[CommandFlag("force", "Skip catalog and file-existence validation")]
[CommandHelpRemarks("The argument tokenizer removes ':' '/' '\\' and '=' even inside quotes. Prefer a catalog handle from MODEL CATALOG, or pipe the value in.")]
[CommandHelpRemarks("Registered with modifiesEnvironment: true.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `model` | ordered | `string` | yes | — | a catalog handle (`[-_0-9A-Za-z]`), or a raw id | The model to select |
| `revision` | named | `string` | no | *(empty)* | `[-_0-9A-Za-z.]` | **NEW.** Re-attaches a `:`-delimited version suffix that the framework's tokenizer strips: `MODEL SELECT "anthropic.claude-3-sonnet-20240229-v1" -revision 0` reconstructs `anthropic.claude-3-sonnet-20240229-v1:0` |
| `backend` | named | `string` | no | active backend | any registered key | Change a non-active backend's model without switching to it |
| `force` | flag | `bool` | no | `false` | — | **NEW.** Bypass validation. Required to set an id the catalog does not know, or a weight path that does not yet exist |

**Validation, per backend** (this is where the rebuild is stricter than the source, deliberately):

- **azure** — the value is the *deployment* name and doubles as a URL path segment. Rejected if it contains a character that would need URL-encoding (the source interpolated it into the URL unencoded). Warned, not rejected, if it is absent from the cached deployment catalog.
- **bedrock** — checked against the cached foundation-model / inference-profile catalog. If the id resolves to an Anthropic model, the tool reports the family it will use. Family detection is `^(?:[a-z]{2}\.)?anthropic\.` case-insensitive, which — unlike the source's bare `anthropic.` prefix test — also matches cross-region inference profiles such as `us.anthropic.…`.
- **llama** — the value must resolve to an existing file. The source's `/set modelId` did this check and its `/model` command did not, while both wrote the same field; here there is one path and it always checks. A **`.gguf` extension check and a GGUF magic-number read** are performed as a *warning*, not a rejection, preserving the source's behaviour that any existing file counts as configured (a zero-byte file passes readiness) while telling the user it will not load.

**Pipeline behaviour.** Both. **One chunk = one candidate model id or handle**; the tool validates and selects it, emitting one confirmation row per chunk (last write wins, every step visible). This is the safe channel for values containing `:` `/` `\`, because pipe payloads are not passed through the command-line tokenizer. `MODEL CATALOG -format ids | REGIF "^anthropic" | MODEL SELECT` is the canonical form. `OutputFormat = General`.

**Environment interaction.** Writes `CHATDBG_MODEL_ID` (global → `modifiesEnvironment: true`). Reads `CHATDBG_PROVIDER`, `CHATDBG_MODEL_ID`, and, for `llama`, the model-search roots key `CHATDBG_MODEL_PATHS` (**NEW**, `storeDefault: false`).

**Failure modes.**

| Condition | User sees |
|---|---|
| Value arrived mangled by the tokenizer (contains no `:`/`/` but the catalog has exactly one id whose stripped form matches) | Failure chunk that *names the mangling*: `'anthropic.claude-3-sonnet-20240229-v10' looks like 'anthropic.claude-3-sonnet-20240229-v1:0' with the ':' removed by argument tokenization. Re-run with -revision 0, or pipe the id in.` This is the single most valuable error message in the package. |
| Unknown id, catalog available | Failure chunk listing the three closest catalog entries, plus `use -force to set it anyway`. |
| Unknown id, catalog unavailable (offline / no permission) | **Degrades**: accepts the value with a warning row `catalog unavailable (<reason>); id not verified`. Never blocks configuration because discovery failed. |
| Local weight file missing | Failure chunk `Model file not found: <path>` — the source's exact wording — plus `Make sure you've specified the correct path to a GGUF model file.` |
| Local file exists but is not GGUF | Success + warning row: `warning: '<file>' has no GGUF magic; MODEL LOAD will fail.` |
| Downstream error arrives through the pipe | Forwarded unchanged by the framework before this tool sees it. |

**Security and audit.** No secret. A model id is not sensitive, but it is *tenant-identifying* for a private Azure deployment, so the audit event carries it as-is by design and the host is expected to scope its audit sink accordingly. Not destructive; changing the local model id does **not** unload a resident model (that happens lazily at the next `MODEL LOAD`, matching the source's load-on-path-change rule) — this is stated in the confirmation row so the user is not surprised.

**Traceability.** PRD **7.6**/**7.7**/**7.8**, writing **7.2**. Descends from `/model <id…>` (`Changed model from 'old' to 'new'`) and from `/set modelId <id>` with its local-file pre-check. The two source commands are merged here because they wrote the same field with different rules.

---

#### 5.5 `MODEL CATALOG` — list the models a backend can actually serve — **NEW**

| | |
|---|---|
| Command | `CATALOG` |
| Root command | `MODEL` |
| Description | List the models, deployments or weight files available to a backend |
| Prototype | `MODEL CATALOG [-backend <key>] [-filter <substring>] [-take <n>] [-format text\|ids\|csv\|json] [-refresh]` |

```csharp
[CommandRegister("Catalog", "List the models, deployments or weight files available to a backend",
    Prototype = "MODEL CATALOG [-backend <key>] [-filter <substring>] [-take <n>] [-format text|ids|csv|json] [-refresh]")]
[CommandParameterNamed("backend", "Backend to interrogate (default: the active one)")]
[CommandParameterNamed("filter", "Case-insensitive substring match on id and display name")]
[CommandParameterNamed("take", "Maximum entries to return", DataType = typeof(int), DefaultValue = "50")]
[CommandParameterNamed("format", "Output shape", AllowedValues = new[] { "text", "ids", "csv", "json" })]
[CommandFlag("refresh", "Bypass the cache and re-query the backend")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `backend` | named | `string` | no | active backend | any registered key | Which backend to interrogate |
| `filter` | named | `string` | no | *(empty)* | free text, trimmed; **bounded to 200 characters**, longer input truncated | Case-insensitive substring on id and display name. For regular expressions, pipe through `REGIF` |
| `take` | named | `int` | no | `50` | **clamped to `1`–`200`** (`Math.Clamp`) | Maximum entries |
| `format` | named | `string` | no | `text` | `text`, `ids`, `csv`, `json` | `ids` emits the bare id per chunk — the pipe-friendly form |
| `refresh` | flag | `bool` | no | `false` | — | Ignore the cache; always hits the network / rescans the disk |

Per-backend semantics:

| Backend | What it lists | Cost | Cache TTL |
|---|---|---|---|
| `azure` | Deployments on the configured resource. If the deployment-listing surface is not reachable with the configured credential, **degrades** to a single row for the configured deployment plus `catalog not available for this backend; showing configured model only` | one metadata call, no tokens | 15 min |
| `bedrock` | Foundation models and inference profiles entitled to the account in the configured region, each row carrying id, provider, and the streaming/tool-calling flags the service reports | one metadata call, no tokens | 15 min |
| `llama` | `*.gguf` files under the model-search roots (`CHATDBG_MODEL_PATHS`, defaulting to the directory of the currently configured weight file), with size in whole MB, quantisation tag parsed from the filename where present, and a stable short **handle** | filesystem scan only | 60 s |

**Handles** are the point of this tool. Each row is assigned a token-safe handle drawn from `[-_0-9A-Za-z]` (e.g. `llama3-8b-q4km`, `claude-3-sonnet`), which survives the framework's argument tokenizer intact and can be typed directly into `MODEL SELECT` and `MODEL LOAD`. Handles are stable for the lifetime of the cache entry and are printed in every format.

**Pipeline behaviour.** Produces piped output, **one chunk per catalog row**; overrides `Main` for the same reason `LIST` does. Does not accept piped input (returns an explanatory chunk). `OutputFormat` follows `-format`.

**Environment interaction.** Reads `CHATDBG_PROVIDER`, `CHATDBG_AZURE_ENDPOINT`, `CHATDBG_AZURE_API_VERSION`, `CHATDBG_AWS_REGION`, `CHATDBG_MODEL_PATHS`, all with `storeDefault: false`; obtains resolved credentials from the credentials package. Writes the cache timestamp under its own command-prefixed key `CATALOG_FETCHED_AT` (private bucket, no global rights needed). Not environment-modifying.

**Failure modes.** Network/credential failure → **degrades**, never throws: a warning row naming the cause plus whatever local knowledge exists (the configured model). HTTP non-2xx → the status and a **truncated, redacted** excerpt of the body (max 200 characters, credential-shaped substrings masked); the source echoed entire upstream error bodies into the terminal, which is how quota text, request ids and echoed prompt fragments reached the screen. Model-search root missing → warning row, empty result, no exception. `take` non-numeric → parse-time `ArgumentException`; the specific text lands in the trace, so the range is repeated in the help.

**Security and audit.** Credentials are used, never emitted. Endpoint hosts and region names appear in output. Read-only, no confirmation.

**Why this NEW tool earns its place.** In the source, the model identifier was free text with **no validation on any path**, and the shipped default (`gpt-4`) is not a valid Bedrock identifier — so the default configuration of the Bedrock backend was guaranteed to fail, with the failure surfacing only as an opaque service error at the end of a chat turn. A catalog turns the product's most common misconfiguration into a pick-list.

**Traceability.** **NEW.** PRD **7.7** (Managed Cloud Model Marketplace) is the closest section; it also serves **7.6** and **7.8**. No source ancestor.

---

#### 5.6 `MODEL CAPS` — report what a backend can actually do — **NEW**

> **D-001 makes this tool load-bearing:** it is the declared-capability authority that the auto-disable notice, the enable-refusal text and the full-screen shell's disabled-control explanation all point the user at. Capability is read from the backend's declared record (refined by the cached probe where one has run) — never inferred from a failed call. A backend that cannot supply token log probabilities is reported here as such, by name, before any turn is attempted.

| | |
|---|---|
| Command | `CAPS` |
| Root command | `MODEL` |
| Description | Report a backend's capabilities: log-probabilities, streaming, tool calling, ceilings |
| Prototype | `MODEL CAPS [<backend>] [-only all\|logprobs\|streaming\|tools\|limits] [-format text\|csv\|json] [-probe]` |

```csharp
[CommandRegister("Caps", "Report a backend's capabilities: log-probabilities, streaming, tool calling, ceilings",
    Prototype = "MODEL CAPS [<backend>] [-only all|logprobs|streaming|tools|limits] [-format text|csv|json] [-probe]")]
[CommandParameterOrdered("backend", "Backend to report on (default: the active one)", IsRequired = false, UsePipe = true)]
[CommandParameterNamed("only", "Restrict the report to one capability group",
    AllowedValues = new[] { "all", "logprobs", "streaming", "tools", "limits" })]
[CommandParameterNamed("format", "Output shape", AllowedValues = new[] { "text", "csv", "json" })]
[CommandFlag("probe", "Verify the declared capability against the live service instead of reporting the declared matrix")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `backend` | ordered | `string` | no (`IsRequired = false`) | active backend | any registered key | Backend to report on |
| `only` | named | `string` | no | `all` | `all`, `logprobs`, `streaming`, `tools`, `limits` | Restrict the report |
| `format` | named | `string` | no | `text` | `text`, `csv`, `json` | Output shape |
| `probe` | named→**flag** | `bool` | no | `false` | — | Ask the service rather than the table. Costs one metadata call, or (for `logprobs`) one 1-token completion. Implies the same cost warning as `MODEL TEST -deep` |

Capabilities reported, and the honest answers the rebuild must give:

| Capability | `azure` (hosted OpenAI) | `bedrock` | `llama` (local GGUF) |
|---|---|---|---|
| Per-token log-probabilities | **yes** — request `logprobs: true`, `top_logprobs: K` | **no** — no Bedrock API returns them, on any first-party foundation model | **yes**, and richest of the three: the full vocabulary distribution is reachable in-process |
| Top-K reporting ceiling | **20** (service maximum, and the product's own `1`–`20` range agrees) | n/a | vocabulary size; the product still clamps the *reported* K to `1`–`20` |
| Prompt-side (input token) log-probabilities | no | no | **yes**, in principle |
| Streaming | yes | yes | yes |
| Tool / function calling | yes | yes | **partial** — depends on the weight file's chat template; reported as `partial (template-dependent)` |
| Temperature ceiling | `2.0` | `1.0` for `anthropic.*`; model-dependent otherwise | `2.0` |
| Max output tokens ceiling | model-dependent, product cap `8192` | model-dependent, product cap `8192` | product cap `8192`; falls back to `512` if the setting is ≤ 0 |
| Context window | model-dependent (from the catalog) | model-dependent (from the catalog) | from the weight file's metadata when loaded; otherwise the configured `512`–`32768` value |
| Cancellation of an in-flight turn | yes (**NEW** — the source threaded no cancellation anywhere) | yes (**NEW**) | best-effort at token boundaries (**NEW**) |

**Pipeline behaviour.** Both. **One chunk = one backend key** on input; one chunk per backend (or per capability row when `-format csv`/`json`) on output — so it overrides `Main` for the multi-row case. This is the tool that makes `MODEL LIST -format keys | MODEL CAPS -only logprobs` a one-line capability matrix. `OutputFormat` follows `-format`; `json` is declared so the token package can consume it structurally.

**Environment interaction.** Reads `CHATDBG_PROVIDER`, `CHATDBG_MODEL_ID`, `CHATDBG_LOGPROBS_TOPK` (`storeDefault: false`). With `-probe`, additionally needs resolved credentials. Writes nothing. Not environment-modifying.

**Failure modes.** `-probe` with an unconfigured backend → falls back to the declared matrix and marks every probed row `declared (not probed: backend not configured)`. Probe network failure → same degradation with the reason attached. Unknown backend → failure chunk listing registered keys. **A capability is never reported as `yes` because a probe failed to disprove it**; the two states are `declared` and `verified`, and the output says which.

**Security and audit.** No secret in parameters or output. `-probe` makes a billable call on cloud backends — reported in the output as `probe cost: 1 metadata call` or `probe cost: 1 completion (≤ 1 token)`. Read-only; no confirmation, because the cost is bounded and disclosed.

**Why this NEW tool earns its place.** The source shipped two capability lies that this tool exists to end. (1) When Azure returned no log-probability block, the backend **fabricated** one — 15 sampled words at a uniform `ln(0.9)` ≈ 90 % confidence with three canned alternatives — and returned it *indistinguishably from real data*, so a user studying "model confidence" could be reading invented numbers. (2) The Bedrock backend put `logprobs` and `top_logprobs` members into every request payload, where they are not part of any real Bedrock contract, and then parsed a response shape no real model emits. **The rebuild does not fabricate.** `MODEL CAPS` is the single place that says what is really available, and the token package is specified to refuse `TOKEN LOGPROBS ENABLE` on a backend whose caps report `logprobs: no`.

**Traceability.** **NEW.** PRD **7.9** (Token Probability Analysis) is the consumer; the tool itself sits in **7.6**. Its ancestors are the source's *symptoms*, not its code: the Azure fabrication path and the two-line notice `Note: Log probabilities were requested but none were returned by the model. / This could be due to the model not supporting this feature or an API limitation.`

---

#### 5.7 `MODEL TEST` — verify connectivity, credentials and entitlement — **NEW**

| | |
|---|---|
| Command | `TEST` |
| Root command | `MODEL` |
| Description | Probe a backend end to end: configuration, credential resolution, reachability, entitlement |
| Prototype | `MODEL TEST [<backend>] [-timeout <s>] [-deep] [-format text\|csv\|json]` |

```csharp
[CommandRegister("Test", "Probe a backend end to end: configuration, credentials, reachability, entitlement",
    Prototype = "MODEL TEST [<backend>] [-timeout <s>] [-deep] [-format text|csv|json]")]
[CommandParameterOrdered("backend", "Backend to test (default: the active one)", IsRequired = false, UsePipe = true)]
[CommandParameterNamed("timeout", "Per-stage timeout in seconds", DataType = typeof(int), DefaultValue = "30")]
[CommandParameterNamed("format", "Output shape", AllowedValues = new[] { "text", "csv", "json" })]
[CommandFlag("deep", "Also send a 1-token completion to prove entitlement (billable)")]
[CommandHelpRemarks("Stages run in order and stop at the first hard failure; each stage reports pass/fail/skip with a reason.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `backend` | ordered | `string` | no (`IsRequired = false`) | active backend | any registered key | Backend to test |
| `timeout` | named | `int` | no | `30` | **clamped to `1`–`300`** | **NEW.** Per-stage timeout. The source configured no timeout anywhere — the only bound was the transport's ~100 s default and the UI simply sat on `Thinking...` |
| `deep` | flag | `bool` | no | `false` | — | Send a minimal completion. Billable on cloud backends; on the local backend it loads the weights |
| `format` | named | `string` | no | `text` | `text`, `csv`, `json` | Output shape |

Stages, in order (one output chunk per stage):

| # | Stage | `azure` | `bedrock` | `llama` |
|---|---|---|---|---|
| 1 | **configured** | endpoint, resolved key and model id all non-empty | model id non-empty **and** an access key resolvable | model id non-empty **and** the file exists |
| 2 | **credential source** | reports the winning leg of the chain: process env → OS keystore (opt-in) → settings file (deprecated) | same, for the AWS pair, and reports whether a **session token** is present | `n/a (no credential required)` |
| 3 | **shape** | endpoint parses as an absolute HTTPS URI; deployment name is URL-safe; api-version non-empty | region is a syntactically plausible region name; model id family is recognised | file has GGUF magic; size in whole MB |
| 4 | **reachable** | TLS handshake + one metadata request | one metadata request | `n/a` |
| 5 | **entitled** (`-deep`) | 1-token completion against the configured deployment | 1-token invocation against the configured model | load weights + create context, then unload if it was not resident before |
| 6 | **capability agreement** | compares what `MODEL CAPS` declares with what stage 5 actually returned (e.g. a log-probability block genuinely present) | reports `logprobs: unavailable — expected` | reports the effective sampler top-K |

**Cross-platform note.** Stage 2 must report `OS keystore: unavailable on this platform` on Linux and macOS rather than silently skipping it. The source's credential chain collapsed to *process environment → settings file* off Windows with **no indication whatsoever**, while the shipped documentation instructed Linux users to enable the keystore — a setup that could not work on the platform it targeted. Naming the unavailability is the fix.

**Pipeline behaviour.** Both. **One chunk = one backend key** on input; **one chunk per stage** on output (so `MODEL LIST -format keys | MODEL TEST` produces a readable stage-by-stage report for every backend). Overrides `Main` on the non-piped path to emit multiple stage chunks. A stage that fails emits a `CommandResult<string>.Failure(...)`, which the host re-wraps and records; subsequent stages emit `skipped (previous stage failed)` as successes so the report is complete rather than truncated. `OutputFormat` follows `-format`.

**Environment interaction.** Reads every configuration key `MODEL SHOW` reads, plus asks the credentials package to resolve secrets. Reads the OS **process** environment only through that package. Writes its own `TEST_LAST_RESULT` and `TEST_LAST_RUN_AT` under the command-prefixed private bucket. Not environment-modifying.

**Failure modes.**

| Condition | User sees |
|---|---|
| Not configured | Stage 1 fails with the *specific missing item(s)*, plus the exact remediation for the platform: environment-variable names for cloud backends, `MODEL SELECT` for local. Never the source's message `…is not properly configured. Please set AzureEndpoint, AzureApiKey, and ModelId.`, which named internal identifiers rather than commands. |
| DNS / TLS / connect failure | Stage 4 fails with the transport reason and the **host only**, never the full URL with the key header. |
| HTTP non-2xx | Stage 4 or 5 fails with the numeric status **and** its symbolic name (the source printed only the symbolic name), plus a body excerpt truncated to 200 characters with credential-shaped substrings masked. |
| Timeout | The stage fails as `timeout after <n>s`; remaining stages are skipped. The source had no timeout, so this state did not exist. |
| Temperature above the model's real ceiling | Stage 6 warns: `temperature 1.5 exceeds this model's ceiling of 1.0; requests will be rejected upstream`. |
| Downstream error arriving through the pipe | Forwarded verbatim by the framework; `TEST` never re-labels an upstream failure as a backend problem. |

**Security and audit.** This is the tool most likely to touch a secret, and therefore the one most tightly specified: it **never accepts a credential as a parameter**, **never prints one**, and **never writes one to the trace**. That is not a preference — the framework's own audit masking only rewrites `-name=value` tokens, while the framework's tokenizer strips `=`, so parameter masking is effectively non-functional for this command syntax. The only safe rule is "secrets never appear in a command line", and this package enforces it structurally. `-deep` is billable and is therefore opt-in, disclosed in the output, and refused when `MODEL CAPS` reports the backend unavailable. Not destructive.

**Why this NEW tool earns its place.** The source performed **no** format validation, no URL parsing, no scheme check, no key-shape check and no connectivity probe anywhere; an endpoint typo surfaced only as a wrapped exception at the end of a chat turn, and a missing AWS *secret* key still reported "configured" because readiness checked only the access key. `MODEL TEST` converts a class of silent misconfiguration into a two-second answer.

**Traceability.** **NEW.** PRD **7.6**/**7.7**/**7.8**, consuming **7.3**. Its ancestor is the console shell's start-up *self-check* block (`Warning: <name> service is not configured.` plus the numbered remediation list) — promoted from an un-runnable start-up side effect into a first-class, re-runnable, pipeable tool.

---

#### 5.8 `MODEL LOAD` — bring a local weight file resident

*(Satellite assembly `ChatDbg.Tools.ModelBackends.Local`.)*

| | |
|---|---|
| Command | `LOAD` |
| Root command | `MODEL` |
| Description | Load a GGUF weight file into memory with its hardware-acceleration settings |
| Prototype | `MODEL LOAD [<model>] [-context <n>] [-gpu-layers <n>] [-threads <n>] [-batch <n>] [-device <spec>] [-no-mmap] [-mlock] [-force] [-yes]` |

```csharp
[CommandRegister("Load", "Load a GGUF weight file into memory with its hardware-acceleration settings",
    Prototype = "MODEL LOAD [<model>] [-context <n>] [-gpu-layers <n>] [-threads <n>] [-batch <n>] [-device <spec>] [-no-mmap] [-mlock] [-force] [-yes]")]
[CommandParameterOrdered("model", "Catalog handle or configured weight file (default: the configured model)",
    IsRequired = false, UsePipe = true)]
[CommandParameterNamed("context",    "Context window in tokens",   DataType = typeof(int))]
[CommandParameterNamed("gpu-layers", "Layers to offload to GPU",   DataType = typeof(int))]
[CommandParameterNamed("threads",    "Worker threads, 0 = host default", DataType = typeof(int))]
[CommandParameterNamed("batch",      "Batch size",                 DataType = typeof(int))]
[CommandParameterNamed("device",     "GPU device selector, e.g. 0 or 0,1")]
[CommandFlag("no-mmap", "Disable memory-mapping of the weight file")]
[CommandFlag("mlock",   "Lock the weights in physical memory")]
[CommandFlag("force",   "Reload even when the same file is already resident")]
[CommandFlag("yes",     "Confirm tearing down a resident model non-interactively")]
[CommandHelpRemarks("Paths are mangled by argument tokenization. Use a handle from MODEL CATALOG, pipe the path in, or set it once with MODEL SELECT.")]
[CommandHelpRemarks("Hardware options given here apply to THIS load only; MODEL TUNE persists them.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `model` | ordered | `string` | no (`IsRequired = false`) | `CHATDBG_MODEL_ID` | catalog handle, or an id/path arriving through the pipe | Which weight file to load |
| `context` | named | `int` | no | `CHATDBG_LLAMA_CONTEXT_SIZE`, itself defaulting to `4096` | **`512`–`32768` inclusive** | Context window. A configured value ≤ 0 falls back to `4096` — the rebuild uses one constant here, where the source used `2048` at load time and `4096` in its state snapshot |
| `gpu-layers` | named | `int` | no | `CHATDBG_LLAMA_GPU_LAYERS`, default `0` | **`0`–`100` inclusive**; `0` = CPU only | Layers offloaded to the GPU |
| `threads` | named | `int` | no | `CHATDBG_LLAMA_THREADS`, default `0` | **`0`–`64` inclusive**; `0` = host default | **Applied for real.** The source validated, persisted and displayed this value and then never passed it to the engine |
| `batch` | named | `int` | no | `CHATDBG_LLAMA_BATCH_SIZE`, default `512` | **`1`–`2048` inclusive** | **Applied for real** (same story as `threads`) |
| `device` | named | `string` | no | `CHATDBG_LLAMA_GPU_DEVICE`, default empty | free text: an index or comma-separated indices, e.g. `0`, `0,1` | **Applied for real** (same story). Ignored with a warning where the platform's accelerator has no device index (see cross-platform note) |
| `no-mmap` | flag | `bool` | no | `false` (i.e. mmap **on**) | — | **NEW.** The source hard-wired memory-mapping on with no way to disable it |
| `mlock` | flag | `bool` | no | `false` | — | **NEW.** The source hard-wired memory-locking off |
| `force` | flag | `bool` | no | `false` | — | **NEW.** The source reloaded **only** when the *path* changed, so changing context size, GPU layers, threads or batch had no effect until the process restarted. `-force` is the escape hatch |
| `yes` | flag | `bool` | no | `false` | — | **NEW.** Non-interactive confirmation for tearing down a resident model |

**Behaviour, preserved from the source.** Reload is required when no weights are resident, no context exists, or the requested path differs from the resident one — plus, now, when `-force` is given or when any hardware option differs from the resident configuration (the latter two are the improvement). Teardown order on reload is strict and unchanged: session → executor → context (disposed) → weights (disposed), each reference cleared *before* the new load begins. Weight loading and context creation run off the calling thread. The system prompt is bound **once**, at load time, into a fresh session — so a system-prompt change still requires a reload, and the tool now says so instead of leaving the user to discover it.

**Progress and logging.** Emits, in order: the resolved path, the file size in whole MB, `Loading model with context size: <n>, GPU layers: <m>`, then load-complete with elapsed time. Progress is routed through `IIoContext.SetProgress` / `SetStatusMessage` — status, not command output — so a quiet host redirects it to the trace instead of the transcript. The engine's own native log stream is handed to the `DIAG` package's sink; this tool does not own a log file.

**Pipeline behaviour.** Both. **One chunk = one weight-file path or catalog handle.** Piping is the recommended way to pass a real filesystem path, because pipe payloads bypass the argument tokenizer that would otherwise strip `\`, `/` and `:` from the value. Piping several paths loads them **in sequence**, unloading each before the next — deliberately, since two resident multi-gigabyte models is rarely what anyone means. Output is one summary chunk per load. `OutputFormat = General`; with `-format` absent there is no structured shape to declare.

**Environment interaction.** Reads `CHATDBG_MODEL_ID`, `CHATDBG_LLAMA_CONTEXT_SIZE`, `CHATDBG_LLAMA_GPU_LAYERS`, `CHATDBG_LLAMA_THREADS`, `CHATDBG_LLAMA_BATCH_SIZE`, `CHATDBG_LLAMA_GPU_DEVICE`, `CHATDBG_SYSTEM_PROMPT_BODY`, all with `storeDefault: false`. Writes `LOAD_RESIDENT_PATH`, `LOAD_RESIDENT_AT`, `LOAD_EFFECTIVE_CONTEXT`, `LOAD_EFFECTIVE_GPU_LAYERS` into its own command-prefixed bucket — which persists across invocations **without** environment-modifying permission, and is exactly what `MODEL STATUS` reads. Registered `modifiesEnvironment: false`.

**Failure modes.**

| Condition | Behaviour |
|---|---|
| Weight file missing at load time | `Failure` chunk `Model file not found: <path>` (source wording preserved) |
| Not a GGUF / incompatible architecture | `Failure` chunk carrying the engine's message plus the four numbered candidate causes the source enumerated: incompatible model format; missing or incompatible native libraries; insufficient memory; runtime/library version incompatibility — and, added here, *model architecture newer than the bundled engine build* |
| Context creation fails | `Failure` chunk `Failed to create context: <reason>`, weights already released |
| GPU requested but no GPU backend present for this RID | **Degrades**: loads on CPU, emits a warning row `gpu-layers 32 requested but no GPU backend is available (<reason>); loaded CPU-only`. Never a hard failure |
| `-device` given on a platform whose accelerator has no device index | Warning row `device selector ignored on this platform (<accelerator>)`; load proceeds |
| Insufficient memory | `Failure` chunk with the requested context size and a concrete suggestion to lower it |
| A different model is already resident | Interactive: prompt. Non-interactive: **requires `-yes`**, else a failure chunk explaining why (`PromptForCommand` is only meaningful when there is no input pipe) |
| Uncatchable native fault (access violation inside the engine) | **The process dies; no managed handler can intercept it.** Documented, not repaired at this layer. Mitigations the tool applies pre-emptively: refuse a `-context` above the range, warn when both a CPU and a GPU native backend are present for the same RID, warn on non-ASCII or space-bearing paths, and record the intended load into the diagnostics sink *before* calling the engine, so the last line of the daily log names the file that killed the shell |
| Downstream error through the pipe | Forwarded unchanged |

**Security and audit.** No secret. **Destructive**: loading over a resident model discards that model's session, which is where the local backend's multi-turn memory actually lives — so the conversation the engine remembers is lost even though the transcript in `CHAT` is untouched. Confirmation is therefore required, interactively or via `-yes`. The audit event records the resolved path, the effective context size and the effective GPU layer count.

**Traceability.** PRD **7.8** (Local Model Inference). Descends from the source's implicit `EnsureModelLoadedAsync` — a private, lazy, first-chat-turn side effect with no user-facing command, whose parameters were only observable through a line echoed to standard output. Promoting it to an explicit tool is what makes `-force`, `-threads`, `-batch` and `-device` meaningful at all.

---

#### 5.9 `MODEL UNLOAD` — release the resident local model

*(Satellite assembly.)*

| | |
|---|---|
| Command | `UNLOAD` |
| Root command | `MODEL` |
| Description | Release the resident weight file, its context and its session |
| Prototype | `MODEL UNLOAD [-yes] [-quiet]` |

```csharp
[CommandRegister("Unload", "Release the resident weight file, its context and its session",
    Prototype = "MODEL UNLOAD [-yes] [-quiet]")]
[CommandFlag("yes",   "Confirm non-interactively")]
[CommandFlag("quiet", "Suppress the summary row; report only failures")]
[CommandHelpRemarks("Unloading discards the engine's retained conversation state. The transcript in CHAT is not affected.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `yes` | flag | `bool` | no | `false` | — | Confirm without prompting. Mandatory when there is no interactive prompt available |
| `quiet` | flag | `bool` | no | `false` | — | Emit nothing on success |

**Pipeline behaviour.** Neither meaningfully. It declares no piped-input handling and, when invoked with an input pipe, returns an explanatory chunk (`MODEL UNLOAD takes no piped input.`) rather than throwing — the framework convention for a tool that cannot participate. It emits one summary chunk (or none, with `-quiet`). Placed last in a pipeline it acts as a sink; that is the only sensible composition.

**Environment interaction.** Reads and then clears its own `LOAD_*` bucket keys. Reads nothing global. Not environment-modifying.

**Failure modes.** Nothing resident → **not an error**: `No local model is resident.` as a success chunk (idempotent by design, so scripts can call it unconditionally). Native release fails or reports that references remain → warning row `unload reported incomplete: <reason>; memory may not be reclaimed until process exit`, recorded as a leak metric in the diagnostics sink, not as a failure. A generation is in flight → the tool waits on the same process-wide serialisation lock the engine uses and reports `waiting for an in-flight generation…` as a status message; there is no timeout, matching the source, and this is called out in **Design notes** as the one place a bounded wait should be added.

**Security and audit.** No secret. **Destructive and irreversible for in-engine conversation state** → confirmation required (interactive prompt, or `-yes`). Reloading is possible but the retained session is gone.

**Traceability.** PRD **7.8**. Descends from the source's `Dispose` path (session and executor references dropped, context disposed, weights disposed, log component disposed), which was reachable only at shell shutdown and only from one of the two shells — the windowed host never tore its backends down at all.

---

#### 5.10 `MODEL STATUS` — report the resident engine's runtime state — **NEW**

*(Satellite assembly.)*

| | |
|---|---|
| Command | `STATUS` |
| Root command | `MODEL` |
| Description | Report what is resident locally, with what settings, and whether those settings are stale |
| Prototype | `MODEL STATUS [-format text\|csv\|json] [-stale]` |

```csharp
[CommandRegister("Status", "Report what is resident locally, with what settings, and whether they are stale",
    Prototype = "MODEL STATUS [-format text|csv|json] [-stale]")]
[CommandParameterNamed("format", "Output shape", AllowedValues = new[] { "text", "csv", "json" })]
[CommandFlag("stale", "Report only the settings that differ between the resident model and the current configuration")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text`, `csv`, `json` | Output shape |
| `stale` | flag | `bool` | no | `false` | — | Show only the drifted settings, and exit with a failure chunk when any exist — the script-friendly form |

Reported: resident path and handle; file size (whole MB); load timestamp and load duration; **effective** context size, GPU layers, threads, batch size, device, mmap/mlock; which native backend actually bound (CPU / CUDA / Vulkan / Metal) and its RID; the accelerator's reported device name when available; approximate resident bytes; the effective sampler top-K; and a **drift table** naming every configured value that differs from the value the resident model was loaded with.

> The drift table is the reason this tool exists. In the source, changing context size, GPU layers, threads, batch size or the system prompt after the model was loaded had **no effect whatsoever** until the model *path* changed or the process restarted — and nothing said so. `MODEL STATUS -stale` makes that condition visible and `MODEL LOAD -force` clears it.

Context accounting is reported honestly: tokens actually held in the engine's context, distinguished from generated-token counts. The source's per-step "model state" snapshot reported prompt-free counters (`totalTokensProcessed` and `contextTokenCount` were both just the generated-token index, and `remainingContext` therefore over-reported free space); those numbers were decorative and gated nothing. Where the engine cannot supply a real number, the field is reported as `unknown`, never as a synthesised one.

**Pipeline behaviour.** Produces piped output; does not accept piped input. Emits one chunk per reported group (`model`, `hardware`, `context`, `drift`), so it overrides `Main`. `OutputFormat` follows `-format`; `json` exists so `MODEL STATUS -format json | DIAG REPORT` can attach it to a bug report.

**Environment interaction.** Reads the `LOAD_*` private bucket and all `CHATDBG_LLAMA_*` keys with `storeDefault: false`. Writes nothing. Not environment-modifying.

**Failure modes.** Nothing resident → a single success chunk `No local model is resident. Run 'MODEL LOAD' or start a chat turn to load one.` With `-stale` and nothing resident → the same chunk, still a success (nothing has drifted). Engine present but unresponsive → the fields it cannot answer are `unknown` with a reason; the tool never blocks waiting for the engine.

**Security and audit.** No secret. Filesystem paths appear. Read-only.

**Traceability.** **NEW.** PRD **7.8**. Ancestors are internal-only: the source's load-time log lines, its per-token synthetic state snapshot, and the console banner's `LLama Configuration:` block — none of which reported what was *actually resident*.

---

#### 5.11 `MODEL TUNE` — persist the local hardware-acceleration settings

*(Satellite assembly.)*

| | |
|---|---|
| Command | `TUNE` |
| Root command | `MODEL` |
| Description | Persist the local engine's hardware-acceleration settings, optionally applying them now |
| Prototype | `MODEL TUNE [-context <n>] [-gpu-layers <n>] [-threads <n>] [-batch <n>] [-device <spec>] [-apply] [-reset] [-yes]` |

```csharp
[CommandRegister("Tune", "Persist the local engine's hardware-acceleration settings, optionally applying them now",
    Prototype = "MODEL TUNE [-context <n>] [-gpu-layers <n>] [-threads <n>] [-batch <n>] [-device <spec>] [-apply] [-reset] [-yes]")]
[CommandParameterNamed("context",    "Context window in tokens", DataType = typeof(int))]
[CommandParameterNamed("gpu-layers", "Layers to offload to GPU", DataType = typeof(int))]
[CommandParameterNamed("threads",    "Worker threads, 0 = host default", DataType = typeof(int))]
[CommandParameterNamed("batch",      "Batch size", DataType = typeof(int))]
[CommandParameterNamed("device",     "GPU device selector, e.g. 0 or 0,1")]
[CommandFlag("apply", "Reload the resident model so the new settings take effect now")]
[CommandFlag("reset", "Restore every hardware setting to its default")]
[CommandFlag("yes",   "Confirm the reload -apply implies, non-interactively")]
[CommandHelpRemarks("Registered with modifiesEnvironment: true. Without -apply the new values take effect at the next load.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `context` | named | `int` | no | unchanged (default `4096`) | **`512`–`32768` inclusive** | Context window in tokens |
| `gpu-layers` | named | `int` | no | unchanged (default `0`) | **`0`–`100` inclusive**; `0` = CPU only | Layers offloaded to the GPU |
| `threads` | named | `int` | no | unchanged (default `0`) | **`0`–`64` inclusive**; `0` = host default | Worker threads |
| `batch` | named | `int` | no | unchanged (default `512`) | **`1`–`2048` inclusive** | Batch size |
| `device` | named | `string` | no | unchanged (default empty) | free text; `0`, `0,1`, … | GPU device selector |
| `apply` | flag | `bool` | no | `false` | — | **NEW.** Reload now instead of at the next load |
| `reset` | flag | `bool` | no | `false` | — | **NEW.** Restore all five to their defaults |
| `yes` | flag | `bool` | no | `false` | — | **NEW.** Non-interactive confirmation for the reload `-apply` implies |

**Out-of-range policy: reject, do not clamp.** The source had two contradictory policies for the same values — the text commands rejected out-of-range input and changed nothing, while the windowed settings dialog silently clamped to the same bounds and saved the clamped value, and additionally ignored unparseable input while leaving the previous value in place. This package standardises on **reject**, matching the command-line half, and states the bounds in `ValueDescription` and `Prototype`. Ranges are inclusive at both ends, exactly as the source enforced them.

With no options at all, `MODEL TUNE` prints the current values and changes nothing — the same courtesy the source's `/model` with no arguments extended.

**Pipeline behaviour.** Produces piped output (one confirmation chunk); does not accept piped input, and says so when piped. `OutputFormat = General`.

**Environment interaction.** Reads and writes `CHATDBG_LLAMA_CONTEXT_SIZE`, `CHATDBG_LLAMA_GPU_LAYERS`, `CHATDBG_LLAMA_THREADS`, `CHATDBG_LLAMA_BATCH_SIZE`, `CHATDBG_LLAMA_GPU_DEVICE`. These are **global** keys owned by the settings package, so the tool must be registered `modifiesEnvironment: true`; without it every write lands in a private bucket and nothing else in the shell would see the change. It does **not** write the settings document itself — persistence is the settings package's job, triggered by the global environment change.

**Failure modes.** Out-of-range → failure chunk naming the value, the bound and the accepted range (`LlamaGpuLayerCount must be a number between 0 and 100. 0 means CPU-only, higher numbers move more layers to GPU.` — source wording preserved for the values the source validated). Unparseable → the framework marks the parameter invalid at parse time; the tool reports `-threads: 'abc' is not an integer` from the parameter's own `ValidationError` rather than letting the generic `Error executing TUNE` line be the whole story. `-apply` with nothing resident → settings are still written, and the output says `nothing resident; new settings will apply at the next load`. `-apply` with a resident model and no interactive prompt → requires `-yes`. `-gpu-layers` above 0 on a host with no GPU backend → **warning, not rejection**: the setting is stored (the user may be preparing a machine) and `MODEL LOAD` will degrade to CPU with its own warning.

**Cross-platform behaviour.** `threads = 0` means the host's default degree of parallelism on every OS. `gpu-layers` maps to CUDA or Vulkan offload on Windows and Linux and to Metal offload on Apple silicon. `device` is an accelerator device index on CUDA/Vulkan and is **ignored with a warning** on Metal, which exposes no equivalent selector. On a RID with no GPU-capable native backend at all, both `gpu-layers` and `device` are accepted and stored but reported as inert.

**Security and audit.** No secret. Not destructive without `-apply`; with `-apply` it inherits `MODEL LOAD`'s teardown semantics and its confirmation requirement. Environment-modifying → each key change is audit-logged individually through the environment-change path.

**Traceability.** PRD **7.8**, writing **7.2**. Descends from `/set llamaContextSize`, `/set llamaGpuLayers` (alias `llamaGpuLayerCount`), `/set llamaGpuDevice`, `/set llamaThreads`, `/set llamaBatchSize`, and from the windowed shell's "LLama Settings" tab. Three of those five settings were validated, persisted and displayed by the source and then **never applied to inference**; here they are applied, which is why they belong with the loader rather than with the general settings surface.

---

### Pipeline compositions

**1. Probe every backend in one line.**

```
MODEL LIST -format keys | MODEL TEST -timeout 10
```

`MODEL LIST -format keys` emits one chunk per registry key (`azure`, `bedrock`, `llama`). `MODEL TEST` consumes one key per chunk and emits one chunk per stage, so the user gets a complete stage-by-stage readiness report for the whole registry — the thing the source only ever produced as an un-repeatable start-up side effect, and only for the one active provider. Ten-second per-stage bound; an unconfigured backend fails at stage 1 and the remaining stages report `skipped`.

**2. Capability matrix, filtered to what matters.**

```
MODEL LIST -format keys | MODEL CAPS -only logprobs -format csv
```

Three rows of CSV: `azure,logprobs,yes,top_logprobs<=20,declared` / `bedrock,logprobs,no,-,declared` / `llama,logprobs,yes,full-vocabulary,declared`. Declaring `ResultFormat.CSV` on each chunk lets the host render a table and lets a downstream stage parse rather than scrape. This composition is the honest replacement for the source's fabricated 90 %-confidence tokens.

**3. Pick a Bedrock model from the catalog, using a framework built-in as the filter.**

```
MODEL CATALOG -backend bedrock -format ids -take 100 | REGIF "^anthropic" | MODEL SELECT -backend bedrock
```

`CATALOG` emits one bare id per chunk; `REGIF` (a framework built-in, package key `Default`) drops every chunk that does not match by returning an empty success, which the host silently discards; `SELECT` receives only the survivors, validates each against the same catalog, and reports the final selection. The composition also side-steps the argument tokenizer entirely: ids containing `.` and `:` travel through the pipe, never through the command line.

**4. Cross-package — let capability decide whether introspection is even legal.**

```
MODEL CAPS -only logprobs -format json | TOKEN LOGPROBS ENABLE -topk 20
```

`MODEL CAPS` emits one JSON chunk describing the active backend's log-probability support and ceiling. `TOKEN LOGPROBS ENABLE` (package **ChatDbg.Tools.Tokens**, PRD 7.9) consumes it and **refuses** when `logprobs: no`, and clamps `-topk` to the reported ceiling when it is lower than 20. This is the composition that structurally prevents the source's two capability lies: no fabricated probabilities on a backend that has none, and no `top_logprobs: 20` sent to a service that ignores it.

**5. Cross-package — attach a hardware snapshot to a bug report.**

```
MODEL STATUS -format json | DIAG REPORT -title "gguf load crash"
```

`MODEL STATUS` emits its `model` / `hardware` / `context` / `drift` groups as JSON chunks; `DIAG REPORT` (package **ChatDbg.Tools.Diagnostics**, PRD 7.11) folds them into a report alongside the tail of the daily engine log. For the local backend's signature failure — an uncatchable native access violation that kills the shell — this is the only forensic trail there is, which is why `MODEL LOAD` writes its intent to the diagnostics sink *before* it calls the engine.

**6. Retune and apply in one pass.**

```
MODEL TUNE -context 8192 -gpu-layers 32 -apply -yes | MODEL STATUS -stale
```

`TUNE` persists, reloads, and emits a confirmation; `STATUS -stale` then reports an empty drift table — the proof that the settings actually took effect, which in the source they never did without a path change or a restart.

---

### Design notes for the architect

**State this package holds.**

- A **process singleton** per assembly, resolved from the host's `IServiceProvider` (or a static accessor when the host has no container): the backend registry projection, the shared HTTP transports (one per cloud backend, created once, never per command), the catalog cache, and — in the satellite — the resident weight handle, its context, its session, and the two serialisation locks (one for generation, one for loading).
- Nothing on a command instance. The controller keeps only the registered `Type` and constructs a fresh instance per execution, and the executor never disposes it. A tool that cached a transport on `this` would create one per invocation and leak it; the source did exactly that, and its finaliser then failed to release it because it took the non-disposing branch. Own the transport in the singleton, dispose it with the host.
- Per-command breadcrumbs in the environment (`LOAD_RESIDENT_PATH`, `CATALOG_FETCHED_AT`, `TEST_LAST_RESULT`). These survive between invocations *without* environment-modifying rights because the host routes command-name-prefixed keys into the command's private bucket. Note the prefix is the **sub-command** name (`LOAD_`, not `MODEL_LOAD_`), which is a collision risk across packages: keep the names distinctive, and treat the private bucket as a cache, never as a source of truth.

**State this package must not hold.** Secrets of any kind — not in a field, not in the environment, not in a parameter, not in a log line, not in an error message. Settings values (read them, never cache them; the source re-read configuration on every call and that was one of its better decisions). Conversation history. Rendering state. A snapshot of "the active backend" — read `CHATDBG_PROVIDER` each time, so a `SET` from another package is visible immediately.

**Two locks, and the one bounded wait to add.** Generation and model loading are serialised process-wide, exactly as the source did it. Keep that. The one change: the source's waits were unbounded and un-cancellable, so a second caller blocked forever with no feedback. Give both waits a timeout and a status message, and have `MODEL UNLOAD` and `MODEL LOAD` report `waiting for an in-flight generation…` rather than appearing hung. Note also that the source disposed a *static* lock from *instance* teardown — harmless only because exactly one instance ever existed. Do not reproduce that.

**Testability.** Every backend sits behind a factory seam — the source already had `IAzureOpenAIClientFactory` and `IBedrockRuntimeClientFactory`, and they existed purely so tests could avoid the network; keep them and add one for the local engine so `LOAD`/`STATUS`/`UNLOAD` are drivable against a fake. Split the suites by dependency class: one hermetic suite driven by hand-written fakes and a recording HTTP stub, one integration suite that touches the network and is never run in the fast loop. The source's stub *ignored the request object entirely*, which is why not one of its tests asserted a URL, an API version, a header name, a body shape, message ordering, the command-message exclusion or the role mapping. Use a **recording** stub so those become assertions rather than code readings.

**Parameter-handling rules that apply to every tool here.** A command invoked with **zero arguments** receives an empty parameter dictionary: no defaults are applied, no flags are materialised, no field injection happens. Every tool must therefore carry its own fallback (`parameters.TryGetValue(k, out var p) && p.IsValid ? p.GetValue<T>() : fallback`) and must not rely on `DefaultValue` alone. `GetValue<T>` demands `T` equal the declared `DataType` exactly; an `int` parameter must be read as `int`. Return failures as data (`CommandResult<string>.Failure`), never as exceptions — a throw is reduced by the host to `Error executing <CMD> (see trace for more info)`, which is useless to the user. And because parse-time `ArgumentException`s are likewise flattened to that generic line, every range and allow-list in this package is repeated in `ValueDescription` and `Prototype` so the user can see it before they trip it.

**When a capability is unavailable on the current backend.** Report, do not simulate. The single worst behaviour inherited from the source was manufacturing per-token confidence data when the service returned none and returning it with no marker, so a debugging tool presented invented numbers as measurement. The rebuild's contract is: `MODEL CAPS` states what exists; tools that need a capability ask first; a missing capability produces a named, actionable message; and no tool in this package ever synthesises data that looks like a measurement. Where the source *did* synthesise (fabricated log-probabilities on Azure, dead `logprobs`/`top_logprobs` members on every Bedrock request, temperature-derived "probabilities" and a `-1` token-id sentinel in the local analysis records), the rebuild either produces the real value or reports `unknown`.

**Where to degrade rather than fail.**

| Situation | Degrade to |
|---|---|
| No backends loaded at all | `MODEL LIST` reports it and names the recovery command; the shell still starts. A zero-plugin shell is a valid shell |
| Catalog unreachable (offline, no permission, backend has no listing surface) | Show the configured model, mark it unverified, allow selection |
| OS keystore absent (Linux, macOS, or a Windows host with the feature off) | Report `unavailable on this platform`, fall back to process environment then the deprecated settings slot. **Say so** — the source degraded silently while its own documentation told Linux users to enable it |
| GPU backend absent, or fewer devices than requested | Load CPU-only with a warning; never refuse the load |
| A capability probe fails | Report `declared (not probed: <reason>)`. Never upgrade an unproven capability to `verified` |
| A model id cannot be verified | Accept with a warning. Discovery failing must not block configuration |
| Native unload reports remaining references | Warn, record a leak metric, continue |

**Where to fail loudly instead.** A backend that is registered but has no native runtime for this RID must refuse activation, because every subsequent turn would fail. An out-of-range hardware value must be rejected, not clamped. A weight file that does not exist must fail at `SELECT`, not at the first chat turn. And a missing or corrupt integrity store in the host must fail startup rather than silently becoming "allow everything" — that is the host's rule, but this package is the one whose satellite assembly makes it matter.

**Cross-platform posture.** Nothing in the primary assembly is OS-coupled: URL composition, header auth, JSON, and parsing behave identically everywhere. The satellite is coupled to the availability of native binaries per RID, and its accelerator story differs by platform (CUDA/Vulkan on Windows and Linux, Metal on Apple silicon, CPU everywhere). The one inherited Windows-only leg — the OS credential vault — is not in this package at all; it belongs to the credentials package, and this package consumes only a platform-neutral "resolved value plus source label". Remediation text must hide options the running platform cannot offer, which is the one thing the source got exactly right.

**A last framework caveat worth designing around.** A root command invoked with no sub-command, or with an unknown one, produces a poor error on the asynchronous dispatch path the executor actually uses. Have the host map a bare `MODEL` to `HELP MODEL` before dispatch, and make sure the `MODEL` root's description reads as a menu, because for many users that string is the first thing they will see.
