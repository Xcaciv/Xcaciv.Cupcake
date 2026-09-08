## 3. ChatDbg.Tools.CredentialsSecretStorage — Credentials & Secret Storage

> Root command: **`CRED`** · Package: `ChatDbg.Tools.CredentialsSecretStorage`
> Behavioural source of truth: `dossiers/credential-management.md` (feature 6 of the inventory).
> Framework contract: `synthesis/ref-command.md` (Xcaciv.Command **3.3.4**). Host pattern: `synthesis/ref-cupcake.md` §8. Loading: `synthesis/ref-loader.md` §10.

---

### 3.0 Purpose and boundary

**What this package owns.**

`CRED` owns the *identity, provenance, custody and disclosure* of the long-lived provider secrets ChatDbg needs to talk to a hosted OpenAI-compatible service and to a cloud model-inference service. Concretely it owns:

1. **The credential slot registry** — the fixed set of logical secrets (`azureApiKey`, `awsAccessKey`, `awsSecretKey`), the environment-variable names that supply each one and their order, the OS-keystore entry names, and the deprecated settings-file field names.
2. **The resolution algorithm** — the strict three-tier priority (process environment → enabled OS secret store → deprecated settings-file field), evaluated fresh on every read, with empty-string treated as absent at every tier.
3. **Provenance reporting** — answering *which channel supplied this value* without ever emitting the value.
4. **Custody operations** — writing, rotating and deleting a secret in an OS-managed store, across Windows, macOS and Linux.
5. **Migration and hygiene** — moving a user off plaintext-in-a-settings-file, scanning for plaintext leakage, and masking secrets that would otherwise transit a pipeline or a log.
6. **Masking policy** — the single authority on what a masked secret looks like (`***set***`, `(not set)`, `[REDACTED]`) and on the rule that no tool in the product prints a live secret value.

**What this package explicitly does NOT own.**

| Not owned | Owned instead by |
|---|---|
| The settings record itself, its schema, its file path, its serialization, its validation ranges, and the `/set`-style generic key/value surface | **`ChatDbg.Tools.SettingsConfiguration`** (root `SET`) — PRD 7.2. `CRED` reads the credential-relevant members of that record through the host environment and never serializes `settings.json` itself. |
| Provider selection, endpoint/region configuration, model identifiers, and the actual request signing or `api-key` header emission | **`ChatDbg.Tools.Providers`** (root `AI`) — PRD 7.6, and the Bedrock/marketplace package — PRD 7.7. `CRED` hands them a resolved string and nothing else. |
| Local model file paths and GGUF loading (which need no secret at all) | **`ChatDbg.Tools.LocalInference`** (root `LLAMA`) — PRD 7.8. |
| Log sinks, log files and log export | **`ChatDbg.Tools.DiagnosticLogging`** (root `LOG`) — PRD 7.11. `CRED REDACT` is a *filter* those logs pass through; it owns no sink. |
| Terminal rendering, colour, heat-maps and dialogs | **`ChatDbg.Tools.OutputRendering`** (root `OUT`) — PRD 7.12 — and the two shells, PRD 7.13 / 7.14. |
| Command dispatch, help generation, the pipeline itself | The host + Xcaciv.Command — PRD 7.1. |

**The boundary rule that matters most:** the source product's `SetCommand` reached across this line — it was 464 lines that owned settings keys *and* credential semantics, and its credential paths re-loaded and re-wrote the whole settings file behind the caller's back (dossier Q7). The rebuild cuts that: `CRED` mutates **no** file owned by `SET`. It publishes its own state into its own environment bucket and lets the settings package mirror it.

---

### 3.1 Package manifest

| Property | Value |
|---|---|
| Assembly / package name | `ChatDbg.Tools.CredentialsSecretStorage` (`ChatDbg.Tools.CredentialsSecretStorage.dll`) |
| Root command | `CRED` (`[CommandRoot("CRED", "Credential and secret storage")]` on every tool class) |
| Contract assemblies targeted | `Xcaciv.Command.Interface` **3.3.4**, `Xcaciv.Command.Core` **3.3.4** (`AbstractCommand`, `IResult<string>`, `CommandResult<T>`, the seven class-targeted attributes, `IParameterValue`). No reference to the host. |
| Target framework | `net10.0` (matches the source product's pinned `net10.0`; a `net8.0` asset is buildable from the same sources if the host is pinned to the framework's .NET 8 opt-in) |
| Elevated trust required | **No.** Every operation runs with the interactive user's own rights. No administrator/root path exists, no elevation is requested, and nothing this package does needs it — OS keystores are per-user by construction. |
| Network reach | **None.** `CRED` never opens a socket. It resolves secrets; the provider packages spend them. A `CRED` tool that made a network call would be a defect. |
| Filesystem reach | Read of the settings directory (default `<user profile>/.ChatDbg/`, temp-directory fallback) for the deprecated plaintext tier and for `CRED SCAN`; read/write of `<settings dir>/secrets.protected` **only** when the `encfile` backend is selected. No write to `settings.json`. |
| OS keystore reach | Windows Credential Manager (generic credentials); macOS Keychain (generic passwords); Linux Secret Service / libsecret (default collection). Per-user scope only. |
| Native libraries | `advapi32.dll` (`CredReadW`/`CredWriteW`/`CredDeleteW`/`CredFree`), `Security.framework` (`SecItem*`), `libsecret-1.so.0`. **These live in three satellite backend assemblies, not in the tool assembly.** The tool assembly is pure managed code with no P/Invoke. |
| Safe to load in a restricted host | **Yes, for the tool assembly.** It contains no dynamic code generation and no interop, so it survives `DisallowDynamicAssemblies = true` and passes a preflight-enabled policy. The three native backend satellites will *not* pass `AssemblySecurityPolicy.Strict` preflight — that is expected and designed for: when a backend cannot be loaded, `CRED` reports that store as unavailable with a reason and keeps working on the environment and file tiers. See §3.4 "Degradation". |
| Loader posture | One `AssemblyContext` per package, `isCollectible: true`, `basePathRestriction: <toolsRoot>` (never `"*"`), integrity verifier with `learningMode: false`, discovery by contract via `GetTypes<ICommandDelegate>()` — per `ref-loader.md` §10 steps 1–9. |
| Registration paths supported | All three of `ref-cupcake.md` rule 26: dropped into the package directory (each tool class has a public parameterless constructor); registered in-process by the shell under a host package key; or resolved through `Xcaciv.Command.DependencyInjection` with an injected `ISecretStore`. |
| `ModifiesEnvironment` | **No tool in this package requires it.** Every key `CRED` writes carries the `CRED_` prefix and therefore lands in this root's private bucket without global-write permission. A host that *wants* the store flag visible as a global variable may register `CRED ENABLE`/`CRED DISABLE` with `modifiesEnvironment: true`; this specification recommends against it. |

**Credential slot registry (preserved verbatim from the source).**

| Slot id (canonical, camelCase) | Write-path aliases | Process env vars, **in order** | Store entry name | Deprecated settings field |
|---|---|---|---|---|
| `azureApiKey` | `azure` | `CHATDBG_AZURE_API_KEY` | `ChatDbg:AzureApiKey` | `azureApiKey` |
| `awsAccessKey` | `awsaccess` | `CHATDBG_AWS_ACCESS_KEY`, then `AWS_ACCESS_KEY_ID` | `ChatDbg:AwsAccessKey` | `awsAccessKey` |
| `awsSecretKey` | `awssecret` | `CHATDBG_AWS_SECRET_KEY`, then `AWS_SECRET_ACCESS_KEY` | `ChatDbg:AwsSecretKey` | `awsSecretKey` |

Slot ids are matched case-insensitively (source lower-cases before matching; `awsAccessKey` and `AWSACCESSKEY` both work). The Azure key has exactly **one** environment variable — there is no vendor-standard alias for it. Within tier 1 the product-specific name always beats the vendor-standard name.

**Store backends.**

| Backend id | Platform | Entry identity | Notes |
|---|---|---|---|
| `wincred` | Windows | Generic credential (type `1`), target `ChatDbg:<Slot>`, blob = secret as **UTF-16LE**, blob size in **bytes** (2× character count), persistence **`2` = local machine**, user name `ChatDbg`, comment `ChatDbg API Credential`, flags `0` | Byte-for-byte compatible with the source product; entries written by the source are read by the rebuild and vice-versa. A zero-length blob reads back as *absent*, not as empty string. |
| `keychain` | macOS | Generic password, service `ChatDbg`, account `<Slot>` | **NEW** — see §3.4 "Deliberate improvements", item 4. |
| `secretservice` | Linux | Secret Service item in the default collection, attributes `application=ChatDbg`, `slot=<Slot>` | **NEW**. Requires a running secret-service provider (gnome-keyring, KWallet's SS bridge, `keepassxc`); absence is reported, not fatal. |
| `encfile` | any | `<settings dir>/secrets.protected`, per-user-scoped OS data protection where the platform offers it, otherwise a passphrase-derived key | **NEW** — the "encrypted credential files (future enhancement)" the source's docs promised and never built (dossier Q31). Explicitly labelled *weaker than a keystore* everywhere it appears. |
| `none` | any | — | Store tier disabled. The default. |

**Compatibility note on the legacy flag.** The source's boolean `useWindowsCredentialManager` (default **`false`**) is preserved in the settings record and continues to serialize. `CRED` reads and writes it through the host environment key `CRED_STORE_ENABLED`; the settings package mirrors the two. When `CRED_STORE_BACKEND` is `wincred` the two are exactly equivalent, so an old settings file keeps working unchanged.

---

### 3.2 Tool catalog

**Conventions that apply to every tool in this package.** Stated once here rather than repeated thirteen times.

* **All parameter attributes are class-targeted.** `[CommandParameterOrdered]`, `[CommandParameterNamed]`, `[CommandFlag]`, `[CommandParameterSuffix]` are `AttributeTargets.Class, AllowMultiple = true, Inherited = false`. They are declared on the tool class, never on a property or field.
* **Ordered parameters are `IsRequired = true` by default.** Where a slot id is optional, the attribute says `IsRequired = false` explicitly.
* **A secret value is never a command-line parameter.** Two independent framework facts force this and they are not negotiable:
  1. The argument tokenizer scrubs arguments to `[-_0-9A-Za-z .*?\[\]|"~!@#$%^&*()]`, deleting `/`, `\`, `;`, `<`, `>`, `` ` ``, `=`, `+`, `,`, `:` and `{}` **before the tool sees them** (`NamesValidator.cs:22,51-60`). Base64 and URL-safe API keys would be silently mangled — a far worse failure than the source's whitespace collapsing.
  2. `AuditEvent.Parameters` is populated from `ioContext.Parameters` verbatim, and `AuditMaskingConfiguration.ApplyMasking` only rewrites `-name=value` tokens — the framework's own `-name value` form is **not masked**. Any secret typed as a parameter is written to the audit log in the clear.
  Therefore every tool that ingests a secret takes it **from the pipe** or **from a no-echo prompt**, and from nowhere else. This also repairs the source's defect where `/set wincred <type> <value>` echoed secrets to the terminal and into shell history, and where runs of two or more spaces inside a secret collapsed to one and tabs/newlines were untypeable.
* **No-echo prompting.** `IIoContext` offers only `Task<string> PromptForCommand(string prompt)`. The host's IO context (the `AbstractTextIo` subclass required by `ref-cupcake.md` rule 18) MUST honour the convention that a prompt string beginning with the sentinel `secret:` is read with terminal echo suppressed and is never written to the shell's own history. Where echo cannot be suppressed (a redirected stdin, a terminal that refuses raw mode, the full-screen shell before its driver is up), the tool emits the warning `Terminal echo cannot be suppressed on this input; the value you type will be visible.` and requires the `-force` flag to continue. This is a **host requirement**, recorded here because the package cannot enforce it alone.
* **Masking tokens.** `***set***` and `(not set)` in status output; `[REDACTED]` in audit and redaction output. These three literals are the package's contract with the rest of the product.
* **Source labels**, verbatim from the source and asserted by its tests: `environment variable (<VARNAME>)` — always naming the actual variable that won — `Windows Credential Manager`, `settings file (deprecated)`, `not set`. **NEW** labels added for the generalized backends: `macOS Keychain`, `Secret Service`, `encrypted file (weak)`, and the diagnostic-only `provider SDK ambient chain (unverified)`.
* **Empty is absent.** At every tier, an empty string is treated as no value and resolution falls through. A zero-length store blob is absent. Only the deprecated file tier returns its stored string unconditionally, including empty — preserved from the source.
* **`AllowedValues` is used only for closed enumerations, never for slot ids.** The attribute's initialiser silently promotes `AllowedValues[0]` to `DefaultValue` when no default was declared, so an ordered parameter with an allow-list can quietly acquire a default the author never intended — a `CRED SET` that defaulted to `azureApiKey` because the argument went missing would be a genuine hazard. Slot ids are therefore validated inside the tool and rejected with the source's own message pair (`Unknown credential type: <as typed>` / `Valid types: azureApiKey, awsAccessKey, awsSecretKey`), while closed sets like `-format`, `-store`, `-to` and `-tier` use `AllowedValues` with an explicit `DefaultValue` and get parse-time enforcement for free. Note that allow-lists are enforced for ordered and named parameters but **not** for suffix parameters — no tool in this package uses a suffix parameter.
* **Zero-argument invocations inject nothing.** `AbstractCommand.ProcessParameters` early-returns an empty dictionary when `io.Parameters.Length == 0`: no defaults are applied, no flags materialise, no field injection runs. Every tool below is specified to fall back to its declared default in that case, and its field initialisers carry the same default.
* **Failures are data.** Tools return `CommandResult<string>.Failure(message, ex)`; they do not throw. A thrown exception would be reduced by the host to `Error executing CRED (see trace for more info)` and the user would lose the remediation text.
* **Confirmation.** Destructive tools (`CRED REMOVE`, `CRED ROTATE`, `CRED MIGRATE -purge`, `CRED SET` over an existing entry) prompt for confirmation via `PromptForCommand`. The **only** affirmative answers are `y` and `yes`, compared after lower-casing — preserved exactly from the source (`Y`/`YES` work; `yeah`, `1`, `true` do not). A `-yes` flag pre-answers the prompt for non-interactive use; when a tool is running as a pipeline stage and no `-yes` was given, it refuses rather than blocking on a prompt nobody can answer.

---

#### 3.2.1 `CRED LIST` — masked status of every credential slot

| Field | Value |
|---|---|
| Command | `LIST` |
| Root command | `CRED` |
| Description | List every credential slot with a masked status and the channel it resolved from. |
| Usage prototype | `CRED LIST [<slot>] [-format table\|csv\|json] [-profile <name>] [-showempty] [-nocache]` |

```csharp
[CommandRoot("CRED", "Credential and secret storage")]
[CommandRegister("LIST", "List credential slots with masked status and resolved source",
    Prototype = "CRED LIST [<slot>] [-format table|csv|json] [-profile <name>] [-showempty] [-nocache]",
    Version   = "1.0.0")]
[CommandParameterOrdered("slot", "Credential slot to report; omit for all three",
    IsRequired = false, UsePipe = true)]
[CommandParameterNamed("format", "Output shape",
    DefaultValue = "table", AllowedValues = new[] { "table", "csv", "json" })]
[CommandParameterNamed("profile", "Credential profile namespace", DefaultValue = "default")]
[CommandFlag("showempty", "Include slots that resolved to nothing", ShortAlias = "e")]
[CommandFlag("nocache", "Bypass the per-invocation resolution cache")]
[CommandHelpRemarks("Never prints a secret value. Status is exactly '***set***' or '(not set)'.")]
public sealed class CredListCommand : AbstractCommand { /* ... */ }
```

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `slot` | ordered | `string` | no (`IsRequired = false`) | *(none — all three slots)* | `azureApiKey`, `awsAccessKey`, `awsSecretKey`, plus aliases `azure`, `awsaccess`, `awssecret`; matched case-insensitively | Credential slot to report; omit for all three. Fed by the pipe when piped (`UsePipe = true`). |
| `format` | named | `string` | no | `table` | `table`, `csv`, `json` | Output shape. `csv` emits one row per slot with a header; `json` emits one object per slot. |
| `profile` | named | `string` | no | `default` | any name matching `[-_0-9A-Za-z]{1,32}` | **NEW.** Credential profile namespace (§3.4 item 8). `default` maps to the legacy un-namespaced entry names. |
| `showempty` | flag | `bool` | no | `false` | — | Include slots whose resolved value is empty. Without it, `(not set)` slots are still listed in `table` mode (matching the source's fixed three-line block) but suppressed in `csv`/`json` so downstream filters see only live slots. |
| `nocache` | flag | `bool` | no | `false` | — | **NEW.** Force a fresh probe of every tier for every slot, defeating the per-invocation cache. |

**Output, `table` mode** — the source's block, preserved line-for-line so transcript tests keep passing:

```
Credentials (secure):
- Azure API Key: ***set*** [environment variable (CHATDBG_AZURE_API_KEY)]
- AWS Access Key: (not set) [not set]
- AWS Secret Key: ***set*** [Windows Credential Manager]
- Secret store: Enabled (wincred)
```

The fourth line replaces the source's `- Windows Credential Manager: Enabled|Disabled` and names the active backend; on a host where the flag is on but the backend cannot answer it reads `Enabled (wincred, unavailable: not supported on this platform)`.

* **Pipeline behaviour** — **both**. Accepts piped input: one chunk is one slot id (or alias); the tool resolves that slot and emits one status row per chunk, so `CRED SCAN | CRED LIST` annotates whatever the scanner found. Produces piped output: one chunk per slot row. Declares `ResultFormat.CSV` when `-format csv`, `ResultFormat.JSON` when `-format json`, otherwise `ResultFormat.General` — the format is metadata riding on each chunk (the framework never branches on it; the host's `HandleOutputChunk` renders on it).
* **Environment interaction** — **reads** the process variables `CHATDBG_AZURE_API_KEY`, `CHATDBG_AWS_ACCESS_KEY`, `AWS_ACCESS_KEY_ID`, `CHATDBG_AWS_SECRET_KEY`, `AWS_SECRET_ACCESS_KEY`. **Reads** the host environment keys `CRED_STORE_ENABLED`, `CRED_STORE_BACKEND`, `CRED_SETTINGS_PATH`, `CRED_PROFILE` (all with `storeDefault: false` — a pure read that must not flip `HasChanged`). **Writes** nothing. Needs no environment-modifying permission.
* **Failure modes** — an unrecognised `slot` yields `CommandResult<string>.Failure("Unknown credential type: <as typed>")` followed by the source's second line `Valid types: azureApiKey, awsAccessKey, awsSecretKey`; the tool echoes the user's original casing, as the source did. A store that throws is reported as a distinct fourth outcome, `[store error: <reason>]`, instead of the source's silent fall-through to the next tier — the user sees that the vault failed rather than concluding the entry does not exist. A failure chunk arriving from upstream is forwarded verbatim by `AbstractCommand.Main` and never reaches this tool's chunk handler. Zero arguments is a valid invocation (all three slots, `table`).
* **Security and audit** — no parameter and no output carries a secret; the only masked artefacts are the two status tokens. `AuditEvent.Parameters` for this tool is safe to log verbatim. Not destructive; no confirmation.
* **Traceability** — PRD **7.3 Credential Management**. Descends from the credential block of bare `/set` (`SetCommand.cs:401-463`, dossier B3) and from `ChatSettings.GetCredentialSource` (B2). `-profile`, `-nocache`, `-showempty`, `csv`/`json` output and the `[store error]` outcome are **NEW**.

---

#### 3.2.2 `CRED SOURCE` — provenance of one credential, and nothing else

| Field | Value |
|---|---|
| Command | `SOURCE` |
| Root command | `CRED` |
| Description | Report which channel supplied a credential, without disclosing its value. |
| Usage prototype | `CRED SOURCE <slot> [-profile <name>] [-all] [-quiet]` |

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `slot` | ordered | `string` | **yes** | — | the three slot ids + three aliases, case-insensitive | Credential slot to trace. Supplied by the pipe when piped (`UsePipe = true`). |
| `profile` | named | `string` | no | `default` | `[-_0-9A-Za-z]{1,32}` | **NEW.** Profile namespace to probe. |
| `all` | flag | `bool` | no | `false` | — | **NEW.** Report *every* tier that holds a value, in priority order, marking the winner — instead of only the winner. Values are never shown; each tier reports `holds a value` or `empty`. |
| `quiet` | flag | `bool` | no | `false` | — | **NEW.** Emit only the bare label (`environment variable (CHATDBG_AZURE_API_KEY)`) with no slot prefix, for scripting. |

**Output.** Default: `Azure API Key: environment variable (CHATDBG_AZURE_API_KEY)`. Unrecognised slot: the literal `not set` — the source's behaviour, which answers `not set` rather than erroring for an unknown type name, and which a test pins by asserting only that a winning environment tier's answer *contains* the phrase `environment variable`, case-insensitively. With `-all`:

```
azureApiKey
  1 environment variable (CHATDBG_AZURE_API_KEY)  holds a value   <- winner
  2 Windows Credential Manager                    empty
  3 settings file (deprecated)                    holds a value
```

That third line is a genuine finding — it means a plaintext copy is still on disk — and it is the input `CRED MIGRATE` wants.

* **Pipeline behaviour** — **both**. One piped chunk is one slot id; one output chunk per input chunk. `ResultFormat.General` by default, `ResultFormat.CSV` under `-all` (tier, label, occupancy, winner). Chaining `CRED LIST -format csv | CRED SOURCE -all` is the intended provenance audit.
* **Environment interaction** — identical read set to `CRED LIST`. Writes nothing.
* **Failure modes** — unknown slot → the literal `not set` as a **success** result (source-preserving; use `CRED LIST` if you want a hard error on a typo). Store throws → the tier line reads `store error: <reason>` and the probe continues to the next tier, so the winner is still computed. Missing prerequisite is impossible: this tool has none. Upstream failure chunks pass through untouched.
* **Security and audit** — no secret in any parameter or in any output. Safe to log verbatim. Not destructive.
* **Traceability** — PRD **7.3**. Descends from `ChatSettings.GetCredentialSource(string)` (dossier B2, rules R39–R41) and from the startup provenance line `Azure credentials loaded from: <source>` (B13). `-all`, `-quiet` and `-profile` are **NEW**; `-all` is what closes the source's defect where a user whose secret key came from the store and whose access key came from the environment was told, flatly, that "AWS credentials" came from the environment.

---

#### 3.2.3 `CRED SET` — put a secret into a store

| Field | Value |
|---|---|
| Command | `SET` |
| Root command | `CRED` |
| Description | Store a secret in the OS secret store. The value is never typed as an argument. |
| Usage prototype | `CRED SET <slot> [-store wincred\|keychain\|secretservice\|encfile] [-profile <name>] [-yes] [-force] [-noverify]` |

```csharp
[CommandRoot("CRED", "Credential and secret storage")]
[CommandRegister("SET", "Store a secret in the OS secret store (value read from a no-echo prompt or the pipe)",
    Prototype = "CRED SET <slot> [-store <backend>] [-profile <name>] [-yes] [-force] [-noverify]",
    Version   = "1.0.0")]
[CommandParameterOrdered("slot", "Credential slot to write")]
[CommandParameterNamed("store", "Backend to write to; default is the enabled backend",
    DefaultValue = "auto",
    AllowedValues = new[] { "auto", "wincred", "keychain", "secretservice", "encfile" })]
[CommandParameterNamed("profile", "Credential profile namespace", DefaultValue = "default")]
[CommandFlag("yes", "Pre-answer the overwrite confirmation", ShortAlias = "y")]
[CommandFlag("force", "Proceed even when terminal echo cannot be suppressed")]
[CommandFlag("noverify", "Skip the read-back verification after writing")]
[CommandHelpRemarks("There is deliberately no <value> parameter. The secret arrives from a no-echo prompt, or as one piped chunk.")]
[CommandHelpRemarks("The argument tokenizer strips / \\ = + : ; and other characters from parameters; a secret passed as an argument would be silently corrupted and written to the audit log.")]
public sealed class CredSetCommand : AbstractCommand { /* ... */ }
```

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `slot` | ordered | `string` | **yes** | — | `azureApiKey`\|`azure`, `awsAccessKey`\|`awsaccess`, `awsSecretKey`\|`awssecret`, case-insensitive | Credential slot to write. The lookup lower-cases; messages echo the casing you typed. |
| `store` | named | `string` | no | `auto` | `auto`, `wincred`, `keychain`, `secretservice`, `encfile` | **NEW** (the source had only the implicit Windows store). `auto` resolves to `CRED_STORE_BACKEND`, else to the single available backend for this OS, else fails with the platform message. |
| `profile` | named | `string` | no | `default` | `[-_0-9A-Za-z]{1,32}` | **NEW.** Namespace for the entry name. `default` writes the legacy names (`ChatDbg:AzureApiKey`), any other profile writes `ChatDbg:<profile>:AzureApiKey`. |
| `yes` | flag | `bool` | no | `false` | — | **NEW.** Pre-answers the overwrite confirmation. Required when running as a pipeline stage. |
| `force` | flag | `bool` | no | `false` | — | **NEW.** Continue when the terminal cannot suppress echo. |
| `noverify` | flag | `bool` | no | `false` | — | **NEW.** Skip the read-back check. Present only for stores that are known to be write-only under policy. |
| *(the secret)* | **not a parameter** | `string` | **yes** | — | 1 … 1280 UTF-16 characters (see below) | Read from `secret:` prompt or taken as one piped chunk. |

**Pre-flight gate — preserved.** The store tier must already be enabled (`CRED_STORE_ENABLED` true). If it is not, the tool makes **no** store call at all and returns the source's message:

```
Windows Credential Manager is not enabled. Enable it first with:
/set useWindowsCredentialManager true
Or use: /set enablewincred
```

restated for the rebuild as `Secret store is not enabled. Enable it first with: CRED ENABLE`. The source pins the "no store call is made" half of this with a never-called mock assertion; that assertion survives the port unchanged and is the reason the gate is a hard requirement rather than an optimisation.

**Value validation — NEW, and a deliberate improvement.** The source performed no length, character-set or emptiness check between reading the typed value and handing it to the OS, so an oversized value surfaced only as a generic failure. This tool rejects an empty or whitespace-only value (`A secret cannot be empty.`), warns above 1200 UTF-16 characters and refuses above **1280** (the platform's documented generic-credential blob ceiling of 2560 bytes, since the blob is UTF-16LE and its declared size is in bytes), and refuses a value containing a control character other than none, naming the offending code point without printing the value. Leading, trailing and internal whitespace are preserved exactly — because the value never passes through the tokenizer.

* **Pipeline behaviour** — **accepts piped input, produces piped output**. One piped chunk is **one complete secret value**; the tool writes it to the selected backend for the `slot` given on the command line and emits one confirmation chunk. This is how a secret reaches the tool from a password manager without ever touching the terminal or the argument list. Piped mode requires `-yes` (there is no one to answer the overwrite prompt) and never prompts. Output is `ResultFormat.General`, one line: `Credential stored securely in <backend display name>: <slot as typed>` on success — the source's message shape with the backend name generalized. With `-noverify` the line gains the suffix ` (not verified)`.
* **Environment interaction** — **reads** `CRED_STORE_ENABLED`, `CRED_STORE_BACKEND`, `CRED_PROFILE`, `CRED_CONFIRM` (`storeDefault: false`). **Writes** `CRED_LAST_WRITE_SLOT` and `CRED_LAST_WRITE_BACKEND` into its own bucket for `CRED DOCTOR` to report. Both keys carry the `CRED_` prefix, so **no environment-modifying permission is needed**. It never writes a process environment variable, and it never writes `settings.json` — unlike the source, which reloaded the settings record from disk mid-command, re-emitted the plaintext warning (secrets and all), silently discarded the caller's unsaved changes, and rewrote the file.
* **Failure modes** — store not enabled → the gate message above, no store call, `Failure`. Unknown slot → `Unknown credential type: <as typed>` + `Valid types: azureApiKey, awsAccessKey, awsSecretKey`, `Failure`. Backend unavailable on this OS → `Secret store '<backend>' is not available on this platform.` (the source's exact sentiment, with the backend named), `Failure`, no state change. Empty/oversized/control-character value → the validation messages above, no write. Write refused by the OS → `Failed to store credential: <slot>` plus, **NEW**, the OS reason on a second line — the source swallowed every exception and gave the user nothing to act on. Read-back verification mismatch → `Wrote <slot> but read back a different value; the store may be shared with another tool.` and a non-zero result. Terminal echo unavailable and no `-force` → refusal with the echo warning. An upstream failure chunk is forwarded by the base class and never reaches the write path — a secret is never derived from a failed stage.
* **Security and audit** — **the value is a secret and never appears in a parameter, in output, in a prompt echo, or in an audit record.** `AuditEvent.Parameters` for this tool contains only the slot id and the flags, all of which are safe. The `slot` id is deliberately not redacted so that audits can answer "which credential was written, when". The action is **destructive when it overwrites an existing entry** and therefore requires confirmation (`Overwrite the existing <slot> entry in <backend>? (y/N): `, affirmative `y`/`yes` only) or `-yes`. Writing a *new* entry is not destructive and is not confirmed.
* **Traceability** — PRD **7.3**. Descends from `/set wincred <credential-type> <value>` (`SetCommand.cs:228-250`, `SettingsService.cs:145-194`, dossier B6) and the vault write primitive (B7). The removal of the value parameter, the read-back verification, value validation, `-store`, `-profile`, `-yes`, `-force`, `-noverify`, the OS failure reason, and the refusal to touch `settings.json` are **NEW**.

---

#### 3.2.4 `CRED ROTATE` — NEW — replace a live secret and prove the replacement took

| Field | Value |
|---|---|
| Command | `ROTATE` |
| Root command | `CRED` |
| Description | Replace a stored secret with a new value, verify it, and report what the credential now resolves from. |
| Usage prototype | `CRED ROTATE <slot> [-store <backend>] [-profile <name>] [-keepbackup] [-yes]` |

**Why it earns its place.** The source product could *write* a secret and could never delete one; its delete primitive had zero call sites in the entire repository, and its own documentation claimed delete was implemented. Consequently there was no rotation story at all: a user with a leaked API key had to leave the old entry in place and go to the operating system's own credential UI. Key rotation is table stakes for anything holding a long-lived bearer secret, and rotation is not simply "write again" — it must verify the new value landed, and it must tell the user whether a *higher-priority* tier is still shadowing the store, which is the failure that makes people believe rotation silently did nothing.

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `slot` | ordered | `string` | **yes** | — | the three slot ids + aliases | Credential slot to rotate. |
| `store` | named | `string` | no | `auto` | `auto`, `wincred`, `keychain`, `secretservice`, `encfile` | Backend holding the entry. |
| `profile` | named | `string` | no | `default` | `[-_0-9A-Za-z]{1,32}` | Profile namespace. |
| `keepbackup` | named | `string` | no | *(none)* | `[-_0-9A-Za-z]{1,32}` | Copy the outgoing value to `ChatDbg:<name>:<Slot>` before overwriting, so a bad rotation can be undone. The backup entry is itself a secret and is reported, never printed. |
| `yes` | flag | `bool` | no | `false` | — | Pre-answer the rotation confirmation. |

* **Pipeline behaviour** — **accepts piped input, produces piped output**. One piped chunk is the **new** secret value. Non-piped, the new value is read from the `secret:` prompt, twice, and the two entries must match (`The two values did not match; nothing was changed.`). Output is one `ResultFormat.General` chunk summarising: the backend written, whether read-back verified, whether a backup was kept, and — the important part — the credential's *current effective source*, e.g. `Rotated awsSecretKey in Windows Credential Manager. WARNING: awsSecretKey still resolves from environment variable (AWS_SECRET_ACCESS_KEY); the rotated value is being shadowed.`
* **Environment interaction** — reads the same set as `CRED SET`; writes `CRED_LAST_ROTATE_SLOT` and `CRED_LAST_ROTATE_UTC` into its own bucket. No environment-modifying permission needed. Never writes a process environment variable.
* **Failure modes** — no existing entry → `No stored value for <slot> in <backend>; use CRED SET to create one.` Store unavailable → the platform message, no change. Write succeeds but read-back differs → the old value is restored from the in-memory copy where the backend supports it, and the result is a `Failure` naming the inconsistency; where restore is impossible the message says so explicitly rather than pretending. Backup requested but backup write fails → the rotation is **not** performed (`Refusing to rotate without the requested backup.`). Upstream failure chunk → forwarded, no rotation.
* **Security and audit** — the new value, the old value and any backup are all secrets: never printed, never in a parameter, never in an audit record. The audit record carries slot, backend, profile, whether a backup was taken, and the outcome. **Destructive and irreversible without `-keepbackup`** — confirmation is mandatory (`Rotate <slot> in <backend>? The current value will be replaced. (y/N): `) unless `-yes`.
* **Traceability** — **NEW**. Its ancestor is the unreferenced delete primitive and the write primitive of `WindowsCredentialManager.cs` (dossier B7, Q14, Q28) and open question 5 ("is there any intended cleanup/rotation lifecycle?" — answered: yes, here). PRD **7.3**.

---

#### 3.2.5 `CRED REMOVE` — NEW — delete a stored secret

| Field | Value |
|---|---|
| Command | `REMOVE` |
| Root command | `CRED` |
| Description | Delete a credential entry from a secret store, or clear a deprecated plaintext field. |
| Usage prototype | `CRED REMOVE <slot> [-tier store\|file\|all] [-store <backend>] [-profile <name>] [-yes]` |

**Why it earns its place.** The source shipped a fully-written delete operation with **zero** call sites; no command, menu item or wizard step could remove a stored secret, and the migration wizard's cleanup step cleared only the plaintext settings fields. Revocation — the thing you do first when a key leaks — was impossible inside the product. This tool wires the primitive up and extends it to the file tier so that "remove this credential from my machine" is one command rather than a trip through three different operating-system UIs.

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `slot` | ordered | `string` | **yes** | — | the three slot ids + aliases | Credential slot to delete. |
| `tier` | named | `string` | no | `store` | `store`, `file`, `all` | Which custody tier to clear. `file` blanks the deprecated settings field (via the settings package, see below); `all` does both. The **environment tier is never touched** — a tool does not reach into the user's shell. |
| `store` | named | `string` | no | `auto` | `auto`, `wincred`, `keychain`, `secretservice`, `encfile` | Backend to delete from. |
| `profile` | named | `string` | no | `default` | `[-_0-9A-Za-z]{1,32}` | Profile namespace. |
| `yes` | flag | `bool` | no | `false` | — | Pre-answer the deletion confirmation. |

* **Pipeline behaviour** — **accepts piped input, produces piped output**. One piped chunk is one slot id, so `CRED SCAN -tier file | CRED REMOVE -tier file -yes` clears exactly what the scanner found. One output chunk per deletion: `Removed <slot> from <backend>.` / `Cleared the deprecated settings-file field for <slot>.` / `No entry to remove for <slot> in <backend>.` (the last is a **success**, not a failure — deletion is idempotent). `ResultFormat.General`.
* **Environment interaction** — reads `CRED_STORE_ENABLED`, `CRED_STORE_BACKEND`, `CRED_SETTINGS_PATH`, `CRED_PROFILE`. Writes `CRED_LAST_REMOVE_SLOT` into its own bucket. No environment-modifying permission needed. For `-tier file`/`all` it does **not** edit `settings.json` itself: it emits a `SET CLEARCRED <slot>` request chunk that the settings package consumes, keeping the file-ownership boundary intact; when that package is not loaded the tool reports `Cannot clear the settings-file tier: the SET package is not loaded.` and the store deletion still proceeds.
* **Failure modes** — store unavailable → platform message, no change. Delete refused by the OS → `Failed to remove credential: <slot>` plus the OS reason. Unknown slot → the `Unknown credential type` pair. Refusal to run unconfirmed as a pipeline stage without `-yes`. Upstream failure chunks forwarded untouched.
* **Security and audit** — carries no secret value in any parameter or output. **Destructive and irreversible** — confirmation is mandatory (`Permanently remove <slot> from <backend>? (y/N): `) unless `-yes`. The audit record carries slot, tier, backend, profile and outcome; this is the record an incident review will want, so it is deliberately not masked.
* **Traceability** — **NEW**. Ancestor: the dead `DeleteCredential` primitive (`WindowsCredentialManager.cs:148-163`, dossier B7/Q14/Q28) and the wizard's settings-file cleanup step (B9 step 7). PRD **7.3**.

---

#### 3.2.6 `CRED ENABLE` — turn on the secret-store tier, with consent

| Field | Value |
|---|---|
| Command | `ENABLE` |
| Root command | `CRED` |
| Description | Enable the OS secret-store credential tier after explicit consent. |
| Usage prototype | `CRED ENABLE [-store wincred\|keychain\|secretservice\|encfile\|auto] [-yes]` |

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `store` | named | `string` | no | `auto` | `auto`, `wincred`, `keychain`, `secretservice`, `encfile` | **NEW** (source: implicit Windows only). Backend to enable. `auto` picks the first *available* backend in the order `wincred`, `keychain`, `secretservice`, `encfile` — which on any single OS is at most two candidates. |
| `yes` | flag | `bool` | no | `false` | — | **NEW.** Pre-answer the consent prompt. Consent is still recorded in the audit event. |

**Consent flow — preserved.** When the backend is available the tool prints the source's banner, verbatim in spirit:

```
Enabling secure credential storage...
This will allow ChatDbg to securely store credentials using <backend display name>.
Credentials will be encrypted and stored securely by the operating system.
```

then prompts `Do you want to enable <backend display name> for secure credential storage? (y/N): ` and reads a line. Only `y` and `yes`, after lower-casing, are affirmative; anything else — including empty input and end-of-input — declines. On acceptance it sets the store tier on and prints:

```
Secure credential storage enabled.
You can now store credentials using: CRED SET <credential-type>
Example: CRED SET azureApiKey
```

Note the example no longer carries a value, because the value is never an argument.

**Declining is a success, not a failure — a deliberate improvement.** The source returned an *error* result reading `Failed to enable Windows Credential Manager integration.` when the user answered "n", conflating a deliberate decision with a breakage. This tool returns a success result carrying `Secure credential storage not enabled.` and changes no state.

* **Pipeline behaviour** — **produces piped output; refuses piped input.** One chunk, `ResultFormat.General`. Piped input is rejected with the explanatory string `CRED ENABLE does not accept piped input; it requires interactive consent. Use -yes for unattended enablement.` rather than an exception (`ref-cupcake.md` rule 29). With `-yes` and piped input it still declines, because a consent decision must not be derivable from an upstream stage's data.
* **Environment interaction** — **reads** `CRED_STORE_ENABLED`, `CRED_STORE_BACKEND`. **Writes** `CRED_STORE_ENABLED = true` and `CRED_STORE_BACKEND = <backend>` into its own bucket; the settings package mirrors those to `useWindowsCredentialManager` (when the backend is `wincred`) and to a new `secretStoreBackend` field. No environment-modifying permission needed for the prefixed keys; if the host wants these as globals it must register this tool with `modifiesEnvironment: true`, which this specification advises against.
* **Failure modes** — backend unavailable on this platform → **no prompt, no state change**, message `<backend display name> is not available on this platform.` (preserving the source's refusal semantics and the test that pins them). `-store auto` with no available backend → `No secret store is available on this platform. Credentials can still be supplied through environment variables; run CRED ENV for the variable names.` — a degradation, not a failure. Persistence failure → **reported**, unlike the source, which caught the save failure, printed a line, and returned success anyway so that `/set useWindowsCredentialManager true` on a read-only settings file cheerfully answered `Set usewindowscredentialmanager = true` and was silently lost at the next restart.
* **Security and audit** — no secret in any parameter or output. Not destructive (the inverse operation is `CRED DISABLE`), but it changes the trust posture of the product, so the audit record carries backend, whether consent was interactive or `-yes`, and the outcome.
* **Traceability** — PRD **7.3**. Descends from `/set enablewincred` (`SettingsService.EnableWindowsCredentialManagerAsync`, dossier B4) and the `true` half of `/set useWindowsCredentialManager` (B5). `-store`, `-yes`, the multi-backend picker, the success-on-decline change and the surfaced persistence failure are **NEW**.

---

#### 3.2.7 `CRED DISABLE` — turn the secret-store tier off

| Field | Value |
|---|---|
| Command | `DISABLE` |
| Root command | `CRED` |
| Description | Disable the OS secret-store credential tier. Stored entries are left untouched. |
| Usage prototype | `CRED DISABLE [-purge] [-yes]` |

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `purge` | flag | `bool` | no | `false` | — | **NEW.** Also delete every `ChatDbg:*` entry this profile owns from the backend, as `CRED REMOVE` would. Without it, entries survive and reappear the moment the tier is re-enabled. |
| `yes` | flag | `bool` | no | `false` | — | **NEW.** Pre-answer the purge confirmation. |

**Always permitted.** Disabling is accepted on every platform regardless of backend availability — preserved exactly from the source, where setting the flag `false` was always allowed while setting it `true` was refused off-Windows. This is what lets a user recover a settings file that some other machine (or the source product's GUI checkbox, which never checked availability) left with the flag stuck on.

* **Pipeline behaviour** — **produces piped output; refuses piped input** with an explanatory string. One `ResultFormat.General` chunk: `Secure credential storage disabled.` plus, when entries remain, the advisory `3 stored entries were left in place; run CRED DISABLE -purge to remove them.`
* **Environment interaction** — reads and writes `CRED_STORE_ENABLED` (set to `false`) in its own bucket; leaves `CRED_STORE_BACKEND` intact so re-enabling remembers the choice. No environment-modifying permission needed.
* **Failure modes** — nothing to disable (already off) → success with `Secure credential storage is already disabled.` `-purge` on an unavailable backend → the tier is still disabled and the message says `Could not purge: <backend> is not available on this platform; entries remain.` — degradation, not failure. Persistence failure → reported.
* **Security and audit** — no secret in any parameter or output. **`-purge` is destructive and irreversible** and requires confirmation (`Permanently remove all stored ChatDbg credentials from <backend>? (y/N): `) unless `-yes`. Plain disable is not destructive and is not confirmed.
* **Traceability** — PRD **7.3**. Descends from the `false` half of `/set useWindowsCredentialManager` (dossier B5, rule R22). `-purge` is **NEW**.

---

#### 3.2.8 `CRED STORES` — NEW — what secret storage this machine actually offers

| Field | Value |
|---|---|
| Command | `STORES` |
| Root command | `CRED` |
| Description | Enumerate secret-store backends with live availability, capabilities and the reason any is unusable. |
| Usage prototype | `CRED STORES [-format table\|csv\|json] [-probe]` |

**Why it earns its place.** The source decided store availability with a bare "is the running OS Windows" test. It never asked whether the credential service actually answered, so a Windows host with the credential service disabled by policy was treated as available and every operation failed later with a generic message; and on macOS and Linux the product silently degraded to "environment variables or a plaintext file" with nothing telling the user that their only encrypted-at-rest option was missing. A user cannot make an informed custody decision without knowing what their machine can do. This tool is that answer, and it is also the tool `CRED DOCTOR` and `CRED ENABLE -store auto` consult.

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `table` | `table`, `csv`, `json` | Output shape. |
| `probe` | flag | `bool` | no | `false` | — | Perform a **live** round-trip against each backend — write, read back and delete a throwaway entry named `ChatDbg:Probe:<guid>` — instead of only checking that the platform and native library are present. Mirrors the source's own test technique of using a randomly generated, never-written entry name to guarantee a miss. |

**Output columns.** `backend`, `platform`, `available` (`yes` / `no`), `reason` (empty when available; otherwise e.g. `not supported on this platform`, `libsecret-1.so.0 not found`, `no secret service is running`, `blocked by policy`, `backend assembly not loaded (restricted host)`), `encrypted-at-rest` (`yes` / `weak` for `encfile`), `roams` (`no` for `wincred` — entries are written with local-machine persistence and explicitly do **not** roam with a domain profile, contradicting every enterprise/roaming claim the source's documentation made), `max-secret-chars` (`1280` for `wincred`, backend-specific elsewhere), `entries` (count of `ChatDbg:*` entries in the active profile, or `-` when unavailable).

* **Pipeline behaviour** — **source only.** Produces one chunk per backend; refuses piped input with an explanatory string. `ResultFormat.CSV` / `ResultFormat.JSON` under `-format`, otherwise `General`.
* **Environment interaction** — reads `CRED_STORE_BACKEND`, `CRED_STORE_ENABLED`, `CRED_PROFILE` (`storeDefault: false`). Writes nothing.
* **Failure modes** — a backend that throws during probing is reported as `available = no` with the exception's message as `reason`; the tool never fails as a whole because one backend is broken. `-probe` write that succeeds but whose cleanup delete fails → the row is still `available = yes` and a warning line names the leftover probe entry so a human can remove it. In a restricted host where the native satellites were refused by the loader's security policy, every native backend reports `backend assembly not loaded (restricted host)` and `encfile` remains available — this is the designed degradation, not an error.
* **Security and audit** — no secret in any parameter or output; probe values are random throwaways and are still never printed. Not destructive, except that `-probe` briefly creates and deletes a uniquely-named entry — which is why the probe name is namespaced under `ChatDbg:Probe:` and can never collide with a real slot.
* **Traceability** — **NEW**. Ancestor: the availability probe `WindowsCredentialManager.IsAvailable` (dossier B7, rules R19–R21) and the platform-conditional wording scattered through the source's wizard, `/set` help and startup hints. PRD **7.3**, with a reporting overlap into **7.2 Settings & Configuration**.

---

#### 3.2.9 `CRED MIGRATE` — move off plaintext-in-a-file, without printing the plaintext

| Field | Value |
|---|---|
| Command | `MIGRATE` |
| Root command | `CRED` |
| Description | Move credentials out of the deprecated settings-file fields into a secret store or environment variables. |
| Usage prototype | `CRED MIGRATE [<slot>] [-to env\|store\|both] [-store <backend>] [-purge] [-dryrun] [-yes]` |

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `slot` | ordered | `string` | no (`IsRequired = false`) | *(all populated slots)* | the three slot ids + aliases | Migrate one slot only. Fed by the pipe when piped (`UsePipe = true`). |
| `to` | named | `string` | no | `env` | `env`, `store`, `both` | **NEW as a parameter** — the source made this an interactive three-item menu accepting only the exact trimmed strings `1`, `2`, `3`. `env` = option 1, `store` = option 2, `both` = option 3, with identical semantics. |
| `store` | named | `string` | no | `auto` | `auto`, `wincred`, `keychain`, `secretservice`, `encfile` | **NEW.** Backend for `-to store` / `-to both`. |
| `purge` | flag | `bool` | no | `false` | — | Blank the deprecated settings-file fields after a successful migration. Corresponds to the source's second confirmation prompt, `Would you like to remove credentials from the settings file now? (y/N): `. |
| `dryrun` | flag | `bool` | no | `false` | — | **NEW.** Report exactly what would move where, change nothing. |
| `yes` | flag | `bool` | no | `false` | — | **NEW.** Pre-answer the purge confirmation. Required in a pipeline. |

**Behaviour, and the one thing that changes.** The source's wizard printed the migration instructions by interpolating **the actual plaintext secret** into `set CHATDBG_AZURE_API_KEY=<value>` lines — and it printed that block automatically on *every* settings load while any plaintext field was populated, and again inside the store-a-secret path, and again from behind a full-screen terminal UI where the user could not see it but the terminal scrollback could. That is the single worst defect in the feature: a tool whose stated purpose is "report without disclosure" was the product's most reliable secret-disclosure channel.

**This tool never prints a secret.** `-to env` emits a copy-paste block with a **placeholder**, plus the name of the slot whose value the user must paste in:

```
To migrate to environment variables, run these commands and paste each value yourself:
  set CHATDBG_AZURE_API_KEY=<paste the azureApiKey value>
  set CHATDBG_AWS_ACCESS_KEY=<paste the awsAccessKey value>
Or add them to your system environment variables for persistence.
Run 'CRED REVEAL azureApiKey' if you need to recover a value you no longer have.
```

`CRED REVEAL` is deliberately **not** a tool in this package (see §3.4). Recovery of a value the user themselves stored in plaintext is the operating system's job, and the message says so on platforms where it applies. `-to store` and `-to both` copy the value directly from the file tier into the backend without any human handling and therefore never need to disclose it — those are the recommended paths, and `-to env` exists only because the environment tier is the one channel that works everywhere.

**Truthful reporting — a deliberate improvement.** The source's `/set migrate` returned a **success** result whatever happened, and its service returned `false` whenever the user declined the *cleanup* prompt even if the store migration had fully succeeded — so "migrated to the vault but kept the file copy" was reported to the user as `No credentials found to migrate or migration cancelled.` This tool reports per-slot outcomes and an accurate summary: `2 of 3 credentials migrated to Windows Credential Manager; settings-file copies retained (use -purge to remove them).`

* **Pipeline behaviour** — **both.** One piped chunk is one slot id, so `CRED SCAN -tier file | CRED MIGRATE -to store -yes` migrates exactly what the scan found. Produces one chunk per slot outcome plus a final summary chunk. `ResultFormat.General`, or `ResultFormat.CSV` under `-dryrun` so the plan is machine-readable.
* **Environment interaction** — **reads** `CRED_SETTINGS_PATH`, `CRED_STORE_ENABLED`, `CRED_STORE_BACKEND`, `CRED_PROFILE`. **Reads** the five process environment variables to report whether a target variable is already occupied (migrating into an occupied variable would be shadowed and the tool says so). **Writes** `CRED_LAST_MIGRATION_UTC` into its own bucket. It **never writes a process environment variable** — the source never did either, and a tool cannot change its parent shell's environment anyway; pretending otherwise is how users end up believing a migration happened. For `-purge` it emits `SET CLEARCRED <slot>` request chunks for the settings package rather than editing `settings.json` itself.
* **Failure modes** — nothing to migrate → **success** with `No credentials found in the settings file.` (the source returned `false` silently here and let the command report success with a message that conflated "nothing to do" with "cancelled"). `-to store` with no available backend → per-slot failure `Secret store '<backend>' is not available on this platform.` and the whole run aborts **before** any purge, so a failed migration can never lose the only copy. A partial migration with `-purge` purges **only** the slots that verifiably landed. Store write failure → that slot is reported failed, its file copy is retained, and the summary counts it. Upstream failure chunks are forwarded and never trigger a migration.
* **Security and audit** — the migrated values are secrets: read from the file tier, written to the backend, never printed and never placed in a parameter. The audit record carries slot ids, target tier, backend and outcomes only. **`-purge` is destructive and irreversible** and requires confirmation (`Remove credentials from the settings file now? (y/N): `, affirmative `y`/`yes`) unless `-yes`. The migration itself is not destructive.
* **Traceability** — PRD **7.3**. Descends from `/set migrate` (`SettingsService.MigrateCredentialsAsync`, dossier B9), the environment-instructions printer (B10) and the bulk store migration (B11). The placeholder-instead-of-plaintext change, `-to`, `-store`, `-dryrun`, `-yes`, per-slot outcomes, occupied-variable detection and the abort-before-purge rule are **NEW**. It also answers the source's dangling "environment variables only" mode flag, which two call sites passed and the body never read, and the graphical shell's three-way radio group whose selection was read into a local variable and then discarded.

---

#### 3.2.10 `CRED SCAN` — NEW — find plaintext secrets without printing them

| Field | Value |
|---|---|
| Command | `SCAN` |
| Root command | `CRED` |
| Description | Report where plaintext credentials are sitting on disk or in the environment, with never a value. |
| Usage prototype | `CRED SCAN [-path <file-or-dir>] [-tier file\|env\|all] [-format table\|csv\|json] [-strict]` |

**Why it earns its place.** The source detected plaintext credentials only as a side effect of loading settings, and its reaction was to print the secrets. It also created a fresh settings file containing the three legacy credential slots on first run, wrote them on every save, never restricted the file's permissions, never added `settings.json` to the repository ignore list, and fell back to the world-readable temp directory whenever the home directory could not be resolved — which silently relocates a file containing plaintext secrets. There was no way to ask "am I leaking?" and get an answer that was not itself a leak. `CRED SCAN` is that question, and it is safe to run in CI, in a pre-commit hook, or over a colleague's machine.

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `path` | named | `string` | no | the active settings file (`CRED_SETTINGS_PATH`, else `<user profile>/.ChatDbg/settings.json`, else the temp-directory fallback) | an existing file or directory | File or directory to scan. Quote it — the tokenizer splits unquoted `.`, `/`, `\` and `:`. |
| `tier` | named | `string` | no | `all` | `file`, `env`, `all` | Which custody tiers to inspect. `env` reports which of the five process variables are occupied, never their contents. |
| `format` | named | `string` | no | `table` | `table`, `csv`, `json` | Output shape. `csv` emits `slot,tier,location,severity,fingerprint`. |
| `strict` | flag | `bool` | no | `false` | — | Treat *any* finding as a failure result, so a CI stage fails the build. Without it, findings are reported as a successful run. |

**Findings and severity.** `high` — a non-empty deprecated settings-file field (the exact condition the source used for its warning: *any* of the three fields non-empty). `high` — a settings file located under the OS temp directory, or inside a directory that contains a `.git` folder. `medium` — a settings file whose permissions are readable by users other than the owner (`0o077` bits set on POSIX; a non-owner ACE on Windows), a check the source never performed. `low` — an occupied environment variable, reported for completeness because environment variables are visible to every process the user runs and leak into `ps`, crash dumps and CI logs.

**Fingerprints, not values.** Each finding carries a `fingerprint`: the first 4 characters of a salted SHA-256 of the value, rendered as hex. It is enough to answer "is the value in the file the same one the store holds?" — which is exactly the question a migration audit asks — and it discloses nothing. The salt is per-machine, generated once, stored alongside the settings, and never emitted.

* **Pipeline behaviour** — **both.** Accepts piped input: one chunk is one filesystem path to scan, so `FIND -name settings.json | CRED SCAN` works. Produces one chunk per finding; a clean scan produces one chunk `No plaintext credentials found.` `ResultFormat.CSV`/`JSON` under `-format`.
* **Environment interaction** — **reads** `CRED_SETTINGS_PATH`, `CRED_PROFILE`, and probes the five process variables for occupancy under `-tier env`/`all`. **Writes** `CRED_LAST_SCAN_FINDINGS` (a count) into its own bucket. No environment-modifying permission needed.
* **Failure modes** — path does not exist → `Failure` naming the path. Path is unreadable → the finding is reported as `severity = unknown, reason = access denied` rather than aborting the whole scan. A file that is not JSON, or is corrupt, is reported as `could not parse; cannot rule out plaintext credentials` — deliberately *not* silently skipped, because the source's own load path silently reverted to defaults on a corrupt file and discarded whatever it held. `-strict` with findings → a `Failure` result whose message is the finding count. Upstream failure chunks are forwarded untouched.
* **Security and audit** — no secret in any parameter or output; fingerprints are one-way and salted. Not destructive. The audit record is safe verbatim and is genuinely useful: it is the only record in the product of *when* a leak was detected.
* **Traceability** — **NEW**. Ancestor: the load-time plaintext warning and `ChatSettings.HasPlaintextCredentials` (dossier B8, rule R42), plus the unaddressed permissions and temp-fallback risks (R43, R51). PRD **7.3**, with reporting overlap into **7.2**.

---

#### 3.2.11 `CRED REDACT` — NEW — the pipeline's secret filter

| Field | Value |
|---|---|
| Command | `REDACT` |
| Root command | `CRED` |
| Description | Mask any live credential value, and anything shaped like a credential, in text flowing through the pipe. |
| Usage prototype | `CRED REDACT [-token <text>] [-mode live\|pattern\|both] [-minlength <n>] [-fail]` |

**Why it earns its place.** Every serious disclosure defect in the source product was the same defect wearing a different hat: a secret reached an output channel. The migration instructions interpolated secrets into stdout; the load path re-emitted them on every start; the full-screen shell pushed them into terminal scrollback where nobody could see them but everybody could scroll back to them; `/set wincred` echoed them into shell history; and the framework's own audit masking does not work for `-name value` syntax. Fixing those one at a time leaves the next one. A pipeline-native redactor fixes the class: any package's output becomes safe by composition, `LOG EXPORT | CRED REDACT` is one keystroke, and the rule "nothing leaves this product unfiltered" becomes enforceable rather than aspirational.

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `token` | named | `string` | no | `[REDACTED]` | 1–32 characters | Replacement text. Default matches the framework's own `AuditMaskingConfiguration.RedactionPlaceholder`. |
| `mode` | named | `string` | no | `both` | `live`, `pattern`, `both` | `live` masks only exact matches of the currently-resolved secret values; `pattern` masks anything matching the credential-shape rules below; `both` does both. |
| `minlength` | named | `int` | no | `8` | 4 – 256 | Shortest live value that will be masked. Guards against a one-character secret turning every output into `[REDACTED]`. A live value shorter than this is reported once as a warning on the status channel, never in output. |
| `fail` | flag | `bool` | no | `false` | — | Convert any chunk that required redaction into a **failure** chunk, so a pipeline that was never supposed to carry a secret stops instead of silently continuing. |

**Pattern rules (`pattern` / `both`).** `sk-` followed by 20 or more base64url characters; `AKIA`/`ASIA` followed by 16 uppercase alphanumerics; a 40-character base64 run adjacent to the words `secret`, `key` or `token`; the value half of `KEY=value` / `KEY: value` where the key name matches the framework's own default redaction patterns (`*password*`, `*secret*`, `*token*`, `*key*`, `*credential*`). Patterns are heuristics and are documented as such; `live` mode is exact and is the guarantee.

**It overrides `Main`.** This is the one tool in the package that does **not** use `AbstractCommand`'s template method unchanged. `AbstractCommand.Main` forwards a failed upstream chunk verbatim and skips empty chunks, so `HandlePipedChunk` never sees a failure — and a failure chunk's `ErrorMessage` is exactly where an upstream tool's exception text will have interpolated a connection string or a bearer token. `CRED REDACT` overrides `Main` (legal — it is not sealed) so that it reads `io.ReadInputPipeChunks()` itself and redacts **both** `Output` and `ErrorMessage` on every chunk, success or failure, before re-emitting it. A redactor that cannot see failures is not a redactor.

* **Pipeline behaviour** — **filter: requires piped input, produces piped output.** One piped chunk is one unit of text to be scrubbed; the tool emits exactly one chunk per input chunk, preserving the upstream chunk's `IsSuccess`, `CorrelationId` and **`OutputFormat`** — so a CSV stage stays CSV and a JSON stage stays JSON through the filter. Invoked without a pipe it returns the explanatory string `CRED REDACT is a pipeline filter; give it input, e.g. 'LOG EXPORT | CRED REDACT'.` rather than throwing.
* **Environment interaction** — **reads** the five process credential variables and the enabled store (it must know the live values in order to mask them), plus `CRED_REDACT_TOKEN`, `CRED_REDACT_MODE`, `CRED_REDACT_MINLENGTH`. **Writes** `CRED_REDACT_COUNT` (redactions performed in this run) from `OnEndPipe`, into its own bucket. No environment-modifying permission needed.
* **Failure modes** — no live secrets resolvable and `mode = live` → the tool still runs, masks nothing, and emits one status-channel note `No live credentials to match; pattern mode is off.` so the user is not misled into thinking output was scrubbed. A store read that throws while gathering live values → that tier is skipped, a status note names the failure, and pattern mode continues; the tool does **not** fail open silently. `-fail` converts a redacted chunk into `CommandResult<string>.Failure("A credential value was found in this stream and was masked.")`. Upstream failure chunks are *not* forwarded verbatim — they are redacted first, then forwarded with their failure status intact.
* **Security and audit** — this tool **reads live secret values into memory by design**; that is its function. It never emits them, never places them in a parameter, and never writes them anywhere. Its audit record carries the redaction count and the mode, which is a genuinely useful signal (a spike in redactions means something upstream started printing secrets). Not destructive. Values are held for the duration of one `Main` invocation only and are dropped in `OnEndPipe`; the specification acknowledges (see §3.4) that .NET strings cannot be reliably zeroed and does not pretend otherwise, unlike the source's documentation, which claimed "minimal credential lifetime in memory" while the code did nothing whatsoever to achieve it.
* **Traceability** — **NEW**. Ancestor: the masking tokens of the status display (dossier B3, rule R38) and, inversely, the plaintext-printing defects of B10/B8/B6 and the full-screen-shell disclosure. PRD **7.3**, composing with **7.11 Diagnostic Logging** and **7.12 Output Rendering**.

---

#### 3.2.12 `CRED ENV` — NEW — name the environment channel, and say whether it is occupied

| Field | Value |
|---|---|
| Command | `ENV` |
| Root command | `CRED` |
| Description | List the environment variables each credential slot reads, in priority order, with occupancy and copy-paste templates. |
| Usage prototype | `CRED ENV [<slot>] [-shell auto\|cmd\|powershell\|bash\|fish] [-format table\|csv\|json]` |

**Why it earns its place.** Environment variables are the source product's *primary* credential channel — the only one that works on every platform, the one the migration wizard recommends first — and the product's README documented **none** of the five variable names. They existed only in `/set` help text, startup hints, a docs folder and a manual test script. A user reading the front door of the product could not discover the main way in. This tool is the discoverable answer, and it also states the priority rule that surprises people: the product-specific `CHATDBG_AWS_ACCESS_KEY` beats the vendor-standard `AWS_ACCESS_KEY_ID`, and an existing-but-empty variable is treated as absent and falls through.

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `slot` | ordered | `string` | no (`IsRequired = false`) | *(all three)* | the three slot ids + aliases | Slot to describe. Fed by the pipe when piped (`UsePipe = true`). |
| `shell` | named | `string` | no | `auto` | `auto`, `cmd`, `powershell`, `bash`, `fish` | Syntax for the copy-paste template. `auto` detects the host shell; on Windows it emits the source's `set NAME=value` form, elsewhere `export NAME=value`. |
| `format` | named | `string` | no | `table` | `table`, `csv`, `json` | Output shape. |

**Output.** Per slot, each variable in declared priority order, with `occupied` (`yes`/`no`/`empty — treated as absent`), and one template line per variable using a **placeholder**, never a value:

```
azureApiKey
  1  CHATDBG_AZURE_API_KEY   occupied
     set CHATDBG_AZURE_API_KEY=<your Azure API key>
awsAccessKey
  1  CHATDBG_AWS_ACCESS_KEY  not set
  2  AWS_ACCESS_KEY_ID       occupied   <- currently winning
```

* **Pipeline behaviour** — **both.** One piped chunk is one slot id; one group of chunks per slot. `ResultFormat.CSV`/`JSON` under `-format`.
* **Environment interaction** — **reads** the five process variables for occupancy only (never their contents) and `SHELL`/`ComSpec`/`PSModulePath` for `-shell auto`. **Writes** nothing, in either the process environment or the host environment. Needs no environment-modifying permission.
* **Failure modes** — unknown slot → `Unknown credential type: <as typed>` + `Valid types: azureApiKey, awsAccessKey, awsSecretKey`, `Failure`. Shell detection failure under `-shell auto` → falls back to the platform default and says so. This tool has no prerequisites and cannot fail for want of a store. Upstream failure chunks forwarded untouched.
* **Security and audit** — no secret in any parameter or output; occupancy is a boolean, and templates carry placeholders. Safe to log verbatim. Not destructive.
* **Traceability** — **NEW**. Ancestor: the environment-variable lists inside `ChatSettings` (dossier B1, rules R2–R4), the `/set` help text, and the blocked-legacy-key refusal template that was the only place the names were shown to users. PRD **7.3**, with documentation overlap into **7.2**.

---

#### 3.2.13 `CRED DOCTOR` — NEW — is this machine actually able to talk to the provider?

| Field | Value |
|---|---|
| Command | `DOCTOR` |
| Root command | `CRED` |
| Description | Diagnose credential readiness for a provider end to end, and print remediation. |
| Usage prototype | `CRED DOCTOR [-provider azure\|bedrock\|llama\|all] [-format text\|json] [-strict]` |

**Why it earns its place.** The source's startup diagnostics were duplicated verbatim in two shells, one of which was dead code, and they carried two real defects: the cloud-inference provider reported itself *configured* when only the **access** half of a key pair was present — never looking at the secret key — so the user was told `AWS credentials loaded from: <source>`, the remediation block was suppressed, and the SDK then silently fell back to its own ambient credential chain and failed at request time with an unrelated error. And the provenance line for that provider always reported the **access** key's source even when it was emitted because the **secret** key was present. A user cannot debug that. `CRED DOCTOR` is the tool that says the true thing.

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `provider` | named | `string` | no | `all` | `azure`, `bedrock`, `llama`, `all` | Which provider's credential requirements to check. `azure` is the product's default provider. `llama` needs no credential and reports so in one line. |
| `format` | named | `string` | no | `text` | `text`, `json` | `text` is the human remediation report; `json` is machine-readable for CI. |
| `strict` | flag | `bool` | no | `false` | — | Return a failure result when any check fails, for use as a CI gate or a pre-flight stage. |

**Checks performed.** Per provider: every required slot resolves to a non-empty value (**both halves of a key pair, individually**); each slot's winning tier and variable name; whether a lower-priority tier is being shadowed and by what; store tier enabled/available/reachable; whether the SDK's own ambient credential chain would be consulted as an undocumented fourth channel, reported as `provider SDK ambient chain (unverified)` so it stops being invisible; whether the settings file holds plaintext copies (delegating to the same logic as `CRED SCAN`); whether the settings file is over-permissive or living in the temp directory; and, for `azure`, whether the endpoint is configured at all — the source's own remediation reminded the user that a key without `azureEndpoint` is not a working configuration.

**Output** preserves the source's remediation shape and its provider-specific text, generalized. For example, for `bedrock` with a half key pair:

```
bedrock: NOT READY
  awsAccessKey  OK    environment variable (AWS_ACCESS_KEY_ID)
  awsSecretKey  MISSING
  -> A key pair needs both halves. Supply the secret key:
       set CHATDBG_AWS_SECRET_KEY=<your AWS secret access key>
     or store it:  CRED ENABLE  then  CRED SET awsSecretKey
  !  With only one half present the provider SDK will fall back to its own
     ambient credential chain; requests may fail with an unrelated error.
```

* **Pipeline behaviour** — **both.** Accepts piped input: one chunk is one provider name, so `AI PROVIDERS | CRED DOCTOR` checks each one. Produces one chunk per check plus a per-provider verdict chunk. `ResultFormat.JSON` under `-format json`, otherwise `General`.
* **Environment interaction** — **reads** the five process variables, `CRED_STORE_ENABLED`, `CRED_STORE_BACKEND`, `CRED_SETTINGS_PATH`, `CRED_PROFILE`, and the settings package's `SET_PROVIDER` / `SET_AZUREENDPOINT` / `SET_AWSREGION` / `SET_MODELID` mirrors (`storeDefault: false`). **Writes** `CRED_LAST_DOCTOR_VERDICT` into its own bucket. No environment-modifying permission needed.
* **Failure modes** — unknown provider name → `Failure` listing the four accepted values. Settings mirrors unavailable because the `SET` package is not loaded → each dependent check reports `unknown (SET package not loaded)` and the run continues; endpoint and region checks degrade to advisory. Store unreachable → reported as a finding, not as a tool failure. `-strict` with any failing check → a `Failure` whose message names the first failing check. Upstream failure chunks forwarded untouched.
* **Security and audit** — no secret in any parameter or output; every value is reported as present/absent with a source label. Safe to log verbatim, and the audit record is the most useful one in the package for support. Not destructive.
* **Traceability** — **NEW as a tool**, but its content is a faithful port of the startup credential diagnostics (`ChatShell.cs:239-321`, dossier B13) with the half-key-pair and wrong-provenance defects corrected. PRD **7.3**, reporting into **7.6 AI Provider Abstraction & Hosted OpenAI**, **7.7 Managed Cloud Model Marketplace** and **7.8 Local Model Inference**.

---

### 3.3 Pipeline compositions

Six worked examples. Two of them cross a package boundary.

---

**1. Audit every credential's provenance, including shadowed tiers.**

```
CRED LIST -format csv -showempty | CRED SOURCE -all
```

`CRED LIST` emits one CSV row per slot and, because the rows carry the slot id as their first field, the downstream stage's `slot` parameter (`UsePipe = true`) is fed by the pipe instead of the command line. `CRED SOURCE -all` then expands each into a full tier ladder. The user gets, for each of the three credentials, every channel that holds a value, in priority order, with the winner marked — and immediately sees the two situations that confuse people most: a store entry being shadowed by a stale environment variable, and a plaintext file copy that survived a migration. Nothing in the output is a secret.

---

**2. Take a secret from a password manager into the OS store without it ever touching the terminal.**

```
SH "op read op://Private/ChatDbg/azure-api-key" | CRED REDACT -mode pattern -fail | CRED SET azureApiKey -yes
```

The first stage is the host's shell-out tool. The middle stage is a **guard**, not a masker: `-mode pattern -fail` turns the pipeline into a failure the instant the retrieved text looks like something *other* than a well-formed key, catching the common accident where the password-manager command printed an error message and the error message got stored as the API key. `CRED SET` consumes one chunk as one complete value — no tokenizer scrubbing, no shell history, no audit-log parameter — and writes it to the enabled backend, verifying by read-back. The user gets `Credential stored securely in Windows Credential Manager: azureApiKey`. (In `-mode pattern` the redactor is deliberately not masking the value it passes on; that is the one composition where `-fail` matters more than the mask, and it is why `mode` and `fail` are separate knobs.)

---

**3. Clean up a machine that has plaintext secrets on disk — plan first, then act.**

```
CRED SCAN -tier file -format csv | CRED MIGRATE -to store -dryrun
CRED SCAN -tier file -format csv | CRED MIGRATE -to store -purge -yes
```

The first line is a rehearsal: the scan finds the populated deprecated fields and emits one chunk per slot, the migration reports exactly which slot would go to which backend and what would be purged, and changes nothing. The second line performs it. Because `CRED MIGRATE` aborts before any purge when the backend is unavailable or any write failed, the run can never leave the user with no copy of a secret. The user gets a per-slot outcome list and a truthful summary — `3 of 3 credentials migrated to macOS Keychain; settings-file copies removed.` — which is precisely what the source product could not say, since its wizard reported success on cancellation and reported "nothing to migrate" on a fully successful migration whose cleanup prompt was declined.

---

**4. Cross-package — make a diagnostic bundle safe to attach to a bug report.**

```
LOG EXPORT -since 24h | CRED REDACT -mode both | OUT FILE -path "./chatdbg-diagnostics.txt"
```

`LOG EXPORT` belongs to **`ChatDbg.Tools.DiagnosticLogging`** (root `LOG`, PRD 7.11) and `OUT FILE` to **`ChatDbg.Tools.OutputRendering`** (root `OUT`, PRD 7.12); the middle stage is the only one this package owns. Because `CRED REDACT` overrides `Main` and scrubs failure chunks as well as successful ones, a stack trace in the log that interpolated an endpoint URL with an embedded key is masked too — which the framework's own audit masking would not have caught, since it only rewrites `-name=value` tokens. The user gets a file they can attach without reading it line by line first, and a status line reporting how many redactions were performed. This composition is the reason `CRED REDACT` exists as a tool rather than as a helper class inside the logging package: redaction has to be the last thing before an output sink, wherever that sink lives.

---

**5. Cross-package — a pre-flight gate before a long chat session or a batch run.**

```
AI PROVIDERS -format csv | CRED DOCTOR -strict -format json | OUT JSON -pretty
```

`AI PROVIDERS` belongs to **`ChatDbg.Tools.Providers`** (root `AI`, PRD 7.6). Each provider name arrives as a chunk, `CRED DOCTOR` checks that provider's credential requirements end to end, and `-strict` turns any failing check into a failure chunk — which the host surfaces and which stops a scripted run before it burns twenty minutes discovering that only half an AWS key pair was configured. The user gets a machine-readable readiness report and a non-zero outcome exactly when something is genuinely wrong.

---

**6. Answer "what can this machine actually store, and where should I put my key?"**

```
CRED STORES -probe -format table | REGIF "available"
```

`REGIF` is one of the framework's built-in commands and filters chunks by regular expression, emitting empty successes for non-matches (which the host drops). The user gets only the rows for backends that survived a live write/read/delete round-trip — not the ones that merely exist on paper. That distinction is the whole point: the source product decided store availability by asking whether the operating system was Windows, so a Windows machine whose credential service was disabled by policy was declared available and every subsequent operation failed with a message that explained nothing.

---

### 3.4 Design notes for the architect

**State this package holds.**

* **Per-invocation only:** a resolution cache keyed by `(slot, profile)`, alive for exactly one `Main` call and discarded at its end (defeatable with `-nocache`). This exists because the source's status display resolved every credential **twice** — once for the masked status, once for the source label — which on an enabled Windows host meant up to **six** native round-trips for one status command, with the two passes free to disagree if a variable changed between them. One consistent snapshot per command, recomputed on the next command, preserves the source's "no memoization across invocations" contract (change an environment variable mid-process and the very next command sees it) while removing the incoherence.
* **In its own environment bucket:** `CRED_STORE_ENABLED`, `CRED_STORE_BACKEND`, `CRED_PROFILE`, and the `CRED_LAST_*` breadcrumbs. All are `CRED_`-prefixed, so they persist to this root's private bucket with no `ModifiesEnvironment` grant. Note the framework trap: `IEnvironmentContext.GetValue` defaults to `storeDefault: true`, so *reading* a missing key writes the default back and flips `HasChanged`. Every read in this package passes `storeDefault: false` explicitly; only deliberate writes go through `SetValue`.
* **In the OS store:** the secrets themselves, which the package does not own so much as *address*.

**State this package must NOT hold.**

* **No secret at rest that it manages itself**, except the explicitly-weak `encfile` backend, which is labelled as such at every appearance.
* **No copy of `settings.json`, and no write to it.** The source's store-a-secret path re-loaded the settings record from disk mid-command, re-emitted the load-time plaintext warning (secrets included), silently discarded whatever unsaved changes the caller held, and rewrote the file — three distinct bugs from one boundary violation. `CRED` emits requests; `SET` owns the file.
* **No process-environment writes, ever.** Tier 1 is read-only. A tool that appeared to set `CHATDBG_AZURE_API_KEY` would change only its own process and mislead the user about what happens after restart.
* **No long-lived secret in a field.** `CRED REDACT` is the only tool that holds live values across chunks, and only for the span of one pipe.
* **No cross-invocation credential cache.** The source's "recomputed on every read" behaviour is a feature: it is what makes "export the variable and try again" work without a restart.

**Honesty about memory.** The source's documentation claimed "minimal credential lifetime in memory" and the code did nothing to achieve it — no protected string type, no zeroing, copies made freely at every tier boundary. This specification does not repeat the claim. Secrets are ordinary managed strings; the mitigations that are real are (a) never printing them, (b) never putting them in a parameter or an audit record, (c) never holding them beyond one command, and (d) telling the truth in the documentation about (a)–(c).

**Testability.**

* **The store is an interface.** `ISecretStore` with `TryRead(entry) -> string?`, `Write(entry, value) -> outcome`, `Delete(entry) -> outcome`, `Probe() -> availability + reason`. The source already had to keep this seam so that a mock could assert the store operation was invoked **zero** times when the enable flag was false; that assertion ports directly and must keep passing.
* **The environment is an interface.** An `IProcessEnvironment` with a single `Get(name)` member. The source's tests mutated real process variables and restored them in a `finally`, which made them order-sensitive and unparallelizable; a seam removes that.
* **The interaction port is an interface.** All prompting goes through `IIoContext.PromptForCommand` — never a direct `Console.ReadLine`. This is the single biggest structural fix: the source's service layer wrote prompts to stdout and blocked on stdin *from inside the service*, which is why its migration wizard was untestable, why the graphical shell's migration button produced prompts nobody could answer behind a full-screen UI, and why exactly **zero** of its ten tests covered the enable flow, the store-write flow, the wizard, the store branch of resolution, or any prompt parsing. With prompting on the IO context, `MemoryIoContext.PromptAnswers` scripts every consent path in a unit test.
* **Every tool class has a public parameterless constructor** (the loader's only activation path is `Activator.CreateInstance`) plus an internal constructor taking `(ISecretStore, IProcessEnvironment, ISettingsMirror)` for tests and for in-process registration.
* **Round-trip fixtures.** The `wincred` backend's byte-level contract — UTF-16LE blob, byte-count size, generic type `1`, persistence `2`, user name `ChatDbg`, comment `ChatDbg API Credential` — is pinned by a fixture that writes with the rebuild and reads with a byte-level reader, so compatibility with entries written by the source product does not silently rot.
* **The "never printed" property is testable.** Every tool's output is run through a fixture that asserts no chunk contains any of the seeded secret values. That is a property test over the whole package, and it is the one that would have caught the source's migration-instructions defect.

**Capability unavailable — what happens instead of failing.**

| Situation | Behaviour |
|---|---|
| No secret store on this OS (or every backend refused) | The store tier is simply absent. Resolution runs on tiers 1 and 3. `CRED ENABLE` reports `No secret store is available on this platform. Credentials can still be supplied through environment variables; run CRED ENV for the variable names.` — a **degradation with a next step**, where the source degraded silently to "environment variables or a plaintext file" and told the user nothing. |
| Backend present but service not responding (credential service disabled by policy, no keyring daemon, locked keychain) | Reported as `available = no` with the real reason, distinct from "not supported on this platform" and distinct from "entry not found". The source collapsed all three into one silent null, at two nested layers of swallowed exceptions, guaranteeing that a policy denial was indistinguishable from an absent entry, forever, with nothing logged. |
| Native backend satellite refused by the loader's security policy (restricted host) | The tool assembly still loads and runs. `CRED STORES` reports `backend assembly not loaded (restricted host)`; `encfile` remains selectable; tiers 1 and 3 are unaffected. |
| `SET` package not loaded | Settings-file tier and endpoint checks report `unknown (SET package not loaded)`. `CRED REMOVE -tier file` and `CRED MIGRATE -purge` report that they cannot clear the file tier and complete the parts they can. |
| Terminal cannot suppress echo | Warn, and require `-force`. Never silently echo a secret. |
| Running as a pipeline stage where a prompt would be needed | Refuse with an explanatory string naming the flag that makes it unattended (`-yes`). Never block on a prompt nobody can answer — the source's wizard did exactly that behind a full-screen UI. |
| A settings file that will not parse | Report `could not parse; cannot rule out plaintext credentials`. Do **not** replace it with defaults. The source's load path returned a defaults record on any parse error, which silently reverted the store-enabled flag to false for the session and discarded whatever the file held. |

**Deliberate improvements over the source, enumerated so a reviewer can object to each one individually.**

1. **No tool ever prints a secret.** Kills the migration-instructions disclosure, the every-load re-emission, and the terminal-scrollback leak.
2. **No secret is ever a command-line parameter.** Forced by the tokenizer's character scrubbing and by the framework's non-functional parameter masking; also removes the source's whitespace-collapsing quirk and its shell-history leak.
3. **Delete and rotate exist.** The source shipped a delete primitive with zero call sites and documented it as implemented.
4. **The store tier is cross-platform.** The source refused off-Windows and a test pinned the refusal; the dossier records this as an open product question. The rebuild answers it: **generalize**, keeping the Windows entry format byte-identical, keeping the refusal semantics per-backend (`keychain` is unavailable on Linux exactly as `wincred` is unavailable on macOS), and keeping `encfile` as an everywhere-fallback that is honestly labelled weak.
5. **Availability is a live probe, not an OS check.**
6. **Declining consent is a success.** The source reported a deliberate "no" as `Failed to enable…`.
7. **Outcomes are reported truthfully**, per slot, with partial results named.
8. **Profiles.** `-profile` namespaces store entries as `ChatDbg:<profile>:<Slot>`, with `default` mapping to the legacy names. The source wrote every entry with the fixed account label `ChatDbg` and matched on entry name only, so one OS user could not keep two ChatDbg configurations apart; the dossier records this as an unresolved question.
9. **Persistence and store failures are surfaced**, with the OS's reason where one exists. The source swallowed every exception at two layers and swallowed save failures entirely, so `/set useWindowsCredentialManager true` against a read-only settings file reported success and was lost at the next restart.
10. **Value validation before storage**, with the platform's 1280-character UTF-16 ceiling stated rather than discovered as a generic failure.
11. **One surface, one rule.** The source's console command refused to enable the store off-Windows while its GUI checkbox happily persisted the flag, after which every load warned forever and every read wasted a probe that could only return nothing. Every `CRED` tool goes through the same gate, and both shells drive the same tools.

**What is deliberately NOT a tool.**

* **`CRED REVEAL`** — a tool that prints a stored secret. It would be the single most useful capability for exfiltration in the product and would destroy the "no tool ever prints a secret" property that every other guarantee here rests on. Recovering a value the user themselves stored is the operating system's own credential UI's job, and `CRED MIGRATE -to env` says so in the one place a user genuinely needs it. If a future requirement forces it, it must be a separate, separately-loadable package with its own audit stream, so that a host can refuse to load it.
* **A tool that writes process environment variables.** It cannot work across a restart and would mislead.
* **A `CRED` tool that talks to a provider to validate a key.** That is the provider package's job and it costs money and network reach this package deliberately does not have. `CRED DOCTOR` diagnoses *custody*, not *acceptance*; the two are different questions and conflating them is how you get a diagnostic that fails because a rate limit was hit.
