## 6. Product-Wide Requirements

### How to read this section

The rules below are the cross-cutting contracts that every feature in this product must honour. They are numbered **GR-1** through **GR-28** ("General Requirement") and the per-feature functional requirements in section 7 reference them by number rather than restating them. Where a section 7 requirement says "per GR-9", the credential precedence chain defined here is binding on that feature without further argument.

Three reading conventions apply throughout:

1. **Every GR is a testable statement.** Each carries the real thresholds, the real path expressions, the real environment-variable names and the exact user-visible strings observed in the source, so that an acceptance test can be written from the rule alone without opening the source.
2. **Where the source violates its own rule, the violation is recorded, not normalised.** Each such case appears under an explicit **Source inconsistency** heading naming the exact file and line where the product breaks the rule it otherwise follows. A reimplementer must make a deliberate decision about each one; those decisions are collected in section 11. Nothing in this section is a fix instruction.
3. **Anything not directly observable in the source is labelled INFERRED.** An INFERRED clause is a reasoned consequence of code structure that was not executed or reproduced.

Evidence citations are given as `path:line` against the pinned commit `d8c18f61d6bb73666ed97cd4885e877e35558485`. Paths are repository-relative.

The fifteen feature authors nominated 196 candidate product-wide rules. Those have been merged into the 28 below; the merge and rejection accounting is at the end of this section.

---

### GR-1 — Single local operator; no authentication, authorization, tenancy or audit

The product is a single-process, single-user, local interactive tool. There is no authentication step, no authorization check, no role or permission model, no multi-tenancy, no server component, no session concept and no audit trail anywhere in it. Any person who can reach the input prompt can invoke every registered command, read and change every setting, read every stored conversation and prompt, and cause a request to be sent to any configured backend. The only security boundary the product relies on is the operating-system user account under which the process runs, and the only per-user scoping is that all persisted state lives under that account's own profile directories (GR-12). The product performs no permission check, sets no file mode or access-control list on anything it creates, and requires no elevated rights to run any of its functions. The one permission-like test in the whole product is the run-time platform capability probe that gates the operating-system credential store (GR-10), and that is a capability check, not a security check. Consequently: possession of a backend credential is the only thing that gates outbound requests; nothing records when a secret was read, stored or rotated; and nothing records what any operator did.

**Evidence:** `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:85-104` (directory and file created with platform defaults, no mode or access-control call); `src/Xcaciv.ChatDbg.Core/Models/WindowsCredentialManager.cs:59-62` (the only platform gate in the product); `src/ChatDbg/ChatShell.cs:323-341` (dispatch performs no caller check); `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:382-417` (likewise); absence of any credential, token, role or session type anywhere in `src/`.

**Binds:** all fifteen features.

---

### GR-2 — Command marker and argument grammar

A line of user input whose first character is the solidus `/` is a command; every other line is a conversation turn sent to the selected backend. There is no escape sequence for sending a literal leading `/` to the model, and there is no way to make a command line become chat content. The leading `/` is stripped, the remainder is split on the single space character U+0020 with empty segments discarded, the first resulting segment is lowercased with a culture-independent mapping and used as the dispatch key, and the remaining segments are passed to the command as an ordered argument list with their case preserved. There is no quoting mechanism and no escaping mechanism anywhere in the product: runs of consecutive spaces collapse to a single space before any command sees them, and tab and newline characters are not separators and cannot be entered at all. Commands that need free text (a message body, a file path, a prompt description) re-join their arguments with a single space, so a file path containing two consecutive spaces is unreachable from any surface. Command names are matched case-insensitively; argument values are not lowercased by the dispatcher, though individual commands lowercase their own first sub-token (for example the setting key and the provider value). A line consisting of `/` alone parses to zero segments.

**Evidence:** `src/ChatDbg/ChatShell.cs:92` (marker test), `:325-334` (split, lowercase, argument slice); `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:384-391` (same grammar, second host); `src/Xcaciv.ChatDbg.Core/Commands/ExportCommand.cs:28` and `src/Xcaciv.ChatDbg.Core/Commands/ImportCommand.cs:28` (re-join with a single space); `src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs:47` (per-command lowercasing of the value token).

**Binds:** 1, 2, 3, 4, 5, 6, 7, 11, 12, 13.

**Source inconsistency.** The two hosts disagree on the empty-parse case and on leading whitespace. The console host returns a failure result carrying the message `Invalid command` (`src/ChatDbg/ChatShell.cs:327-330`); the full-screen host returns silently with the input box already cleared, so the typed text vanishes with no trace (`src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:385-388`). The full-screen host trims its input before the marker test while the console host does not (`src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:334` versus `src/ChatDbg/ChatShell.cs:83`), so a line beginning with a space is a chat turn in one host and a command in the other. The console host's marker test is a culture-sensitive prefix comparison rather than an ordinal one, so a line prefixed with a Unicode ignorable character is classified as a command and then mis-parsed; under the invariant-globalisation packaging configurations (GR-28) the same input becomes a chat turn instead (`src/ChatDbg/ChatShell.cs:92`).

---

### GR-3 — Tri-state command result contract

Every user-facing command exposes exactly four things: a stable lowercase **name** used as its dispatch key, a one-line **description**, a multi-line **usage** string, and an asynchronous **execute** operation that accepts the ordered argument list and yields a result. The result is a triple of a boolean **success** flag, an **optional** message string that may be absent, and a boolean **exit-requested** flag that defaults to false. There is no error-code scheme, no severity level and no structured payload anywhere in the product: failure is carried entirely by the boolean plus a human-readable English sentence. Three factories produce the three canonical shapes — a success result carrying an optional message with exit false; a failure result carrying a required message with success false; and an exit result carrying success true, exit true and **no** message, which is why requesting exit never produces a success line on any surface. A command's failure never terminates the session; only the exit flag does.

**Evidence:** `src/Xcaciv.ChatDbg.Core/Models/ICommand.cs:3-9`; `src/Xcaciv.ChatDbg.Core/Models/CommandResult.cs:3-17`.

**Binds:** 1, 2, 3, 4, 5, 7, 11, 12, 13.

**Source inconsistency.** The failure factory accepts an empty message without validation, producing a failure that renders as total silence and is indistinguishable from a silent success (`src/Xcaciv.ChatDbg.Core/Models/CommandResult.cs:13`). One command publishes its generated data on a second, separately readable property alongside its result — a second output channel that no host reads and only tests consume (`src/Xcaciv.ChatDbg.Core/Commands/DemoLogProbsCommand.cs`).

---

### GR-4 — Outcome glyphs and per-host result presentation

A command result with a non-empty message is always presented; a result with an empty or absent message produces no output at all. The plain-console host writes the message in one write, prefixed with U+2713 CHECK MARK followed by one space (`✓ `) on success and U+2717 BALLOT X followed by one space (`✗ `) on failure — so a multi-line message carries its marker on the first physical line only. The full-screen host presents a success message on a transient status line prefixed with the same `✓ ` glyph, and presents **every** failure as a modal dialog titled `Command Error` carrying the message; it never renders the cross glyph. In the console host the glyph is the only machine-readable success/failure signal the product emits per command — there is no per-command exit code, no structured output and no second channel — so any reimplementation targeting assistive technology or scripting must add a second signal.

**Evidence:** `src/ChatDbg/ChatShell.cs:100-106`; `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:406-415`; `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:903-916` (transient status line).

**Binds:** 1, 2, 3, 4, 5, 7, 11, 12, 13, 14.

**Source inconsistency.** The unknown-command outcome is worded differently in each host: the console host prints exactly `Unknown command: /{name}. Type '/help' for available commands.` while the full-screen host raises a modal titled `Error` reading `Unknown command: {name}` with no glyph and no pointer to help (`src/ChatDbg/ChatShell.cs:340` versus `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:395`). Two dialog-driven paths in the full-screen host bypass the empty-message-means-silence rule by substituting canned text, and title their failure modal `Error` instead of `Command Error` (`src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:960-995`, `:1061-1079`). The full-screen host's status line reverts to the default provider/model line after 3000 ms via a timer started unconditionally for every message and never cancelled, so two messages inside 3000 ms leave the second wiped early by the first message's timer (`src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:903-916`).

---

### GR-5 — Explicit hand-wiring; registration is manual and per host

There is no dependency-resolution container anywhere in the product. Every collaborator is constructed directly at its owner's construction site, in a fixed literal order, and passed positionally. Each host builds its own command table by hand and stores it keyed by each command's own declared lowercase name. **Implemented does not imply reachable**: a command class can exist, compile, carry unit tests and be described in the user manual and still be registered in neither host, in which case it is unreachable from every surface and appears in no help output. A reimplementation must therefore treat the registration list, not the set of implemented command types, as the definition of the command surface, and must publish that list explicitly. A direct consequence of hand-wiring that every host must honour: **all surfaces must share one settings instance**, and a host that constructs collaborators before loading persisted state must re-attach them to the loaded state rather than reassigning its own variable.

**Evidence:** `src/ChatDbg/ChatShell.cs:42-57` (console registration); `src/ChatDbg.Shell.Gui/Program.cs:31-47` (full-screen registration); absence of any container registration anywhere in `src/`.

**Binds:** 1, 2, 3, 5, 7, 11, 12, 13, 14.

**Source inconsistency.** Three complete, tested command implementations — export-logs, export-analysis and show-analysis — are registered in neither host and are unreachable, while five product documents describe them as working (`src/Xcaciv.ChatDbg.Core/Commands/ExportLogsCommand.cs`, `ExportTokenAnalysisCommand.cs`, `ShowTokenAnalysisCommand.cs` versus both registries). The full-screen host constructs its commands against one settings record and then reassigns its settings variable to the record loaded from disk, so every typed command in that host mutates and persists an orphaned record that the window never reads — with the consequence that a single setting change rewrites the user's whole settings document with construction-time defaults (`src/ChatDbg.Shell.Gui/Program.cs:14`, `:37-46`, `:58`, `:81`). The full-screen project additionally contains an entire second, never-instantiated host class that registers only 13 commands and only 2 of the 3 backends (`src/ChatDbg.Shell.Gui/ChatShell.cs:33-37`, `:43-68`).

---

### GR-6 — Settings precedence and immediate whole-file persistence

All application configuration lives in exactly one record with exactly one persisted document. The precedence is three-tier and nothing else participates: **(1)** built-in defaults compiled into the record; **(2)** values read from the settings document at startup, which replace the defaults wholesale; **(3)** in-session mutations from a command or a dialog, which take effect immediately on the shared live record and are visible to every collaborator holding it without a restart. There is no fourth tier: neither entry point parses any command-line flag or argument, there is no profile concept, no per-workspace configuration, no configuration layering, no environment-variable override for any setting, and no way to relocate the settings document at run time. Environment variables affect **credentials only** (GR-9). Every accepted mutation writes the **entire** document back to disk immediately after that individual assignment; there is no dirty tracking, no explicit save verb, no partial patch, no merge and no backup. The directory is created on demand at save time if it does not exist.

**Evidence:** `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:85-104` (whole-document write, directory created on demand); `src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs:44-140` (mutate then save, per assignment); `src/ChatDbg/Program.cs:1-14` and `src/ChatDbg.Shell.Gui/Program.cs` (neither reads command-line arguments).

**Binds:** 1, 2, 3, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14.

**Source inconsistency.** The save operation catches every exception, prints `Error saving settings: {message}` to standard output and returns normally with no failure signal, so every caller in the product reports success while nothing reached disk — on a read-only directory the user is told `Set temperature = 1.5` and nothing was written (`src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:100-103` with `src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs:284-289`). The console host does not restore three persisted display settings at startup (`showAllTokens`, `gridViewForTokens`, `gridViewMaxAlternatives`), so those preferences silently revert to defaults on every launch (`src/ChatDbg/ChatShell.cs:130-151` versus `src/Xcaciv.ChatDbg.Core/Models/ChatSettings.cs:39-46`). The full-screen settings dialog applies a provider change to the live record even when the dialog is cancelled; cancelling only skips the disk write (`src/ChatDbg.Shell.Gui/UI/SettingsDialog.cs:139-153`).

---

### GR-7 — Self-provisioning first run; a missing state file is not an error

A freshly installed binary must start correctly on a machine with no prior state and no shipped configuration file. Loading the settings document when the file does not exist is **not** an error: the product constructs a fully populated defaults record, writes it to the settings path, and returns it — so the first run creates the document as a side effect of reading it. Likewise, constructing the system-prompt store creates its directory if absent and seeds it with the four built-in starter prompts. No feature may treat "the state file is absent" as a failure condition, and no feature may require a configuration file to be shipped or hand-authored before first use. A settings document that exists but cannot be read or parsed is a different case and is handled by GR-20.

**Evidence:** `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:44-49` (missing file → defaults written and returned); `src/Xcaciv.ChatDbg.Core/Services/SystemPromptService.cs:10-37` (directory creation plus seeding of the built-in prompts).

**Binds:** 1, 2, 5, 6, 7, 13, 14, 15.

**Source inconsistency.** The freshly written defaults document contains the three deprecated plaintext credential slots as empty strings, so a brand-new installation writes a document with credential-shaped fields in it even though no supported user action can populate them (`src/Xcaciv.ChatDbg.Core/Models/ChatSettings.cs:79-86` with `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:47-53`).

---

### GR-8 — Validation is enforced at the write boundary only, with fixed shared ranges

Numeric and enumerated settings are validated **only** at the point a value is written through a user-facing surface. Values read back from the settings document at startup are never re-validated, so a hand-edited or migrated document carries arbitrary out-of-range values straight into every consumer. The ranges are product-wide and shared by every feature that reads the value:

| Setting | Accepted range | Default | Rejection message |
|---|---|---|---|
| `temperature` | 0.0 – 2.0 inclusive | 0.7 | `Temperature must be a number between 0 and 2` |
| `maxTokens` | 1 – 8192 inclusive | 1000 | `MaxTokens must be a number between 1 and 8192` |
| `logProbabilitiesTopK` | 1 – 20 inclusive | 5 | (varies — see inconsistency) |
| `llamaContextSize` | 512 – 32768 inclusive | 4096 | `LlamaContextSize must be a number between 512 and 32768` |
| `llamaGpuLayerCount` | 0 – 100 inclusive | 0 | `LlamaGpuLayerCount must be a number between 0 and 100. 0 means CPU-only, higher numbers move more layers to GPU.` |
| `llamaThreads` | 0 – 64 inclusive | 0 | `LlamaThreads must be a number between 0 and 64. 0 means system default.` |
| `llamaBatchSize` | 1 – 2048 inclusive | 512 | `LlamaBatchSize must be a number between 1 and 2048` |
| `enableLogProbabilities` | boolean | false | — |

A rejected value leaves the stored value unchanged, persists nothing, and returns a failure result carrying the exact sentence above.

**Evidence:** `src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs:70-135`; `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:43-58` (load path performs no validation); `src/Xcaciv.ChatDbg.Core/Models/ChatSettings.cs:8-66` (defaults).

**Binds:** 2, 5, 6, 8, 9, 10, 11, 12, 14.

**Source inconsistency.** The two user surfaces disagree on the failure mode for the identical rule: the command surface **rejects** an out-of-range value with the message above and changes nothing, while the full-screen settings dialog **silently clamps** the value into range with no message, and silently ignores unparseable input (`src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs:70-135` versus `src/ChatDbg.Shell.Gui/UI/SettingsDialog.cs:420-427`, `:453-463`, `:478-498`). The same 1–20 top-K bound produces two differently worded sentences depending on which command was used (`src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs:178` versus `src/Xcaciv.ChatDbg.Core/Commands/LogProbsCommand.cs:59`). The unknown-key error lists only 13 of roughly 26 accepted keys, so a user who mistypes a valid key is told the correct spelling does not exist (`src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs:279-280` versus `:303-375`). Three local-inference tunables (`llamaGpuDevice`, `llamaThreads`, `llamaBatchSize`) are validated, clamped, persisted and displayed but are never read by the inference engine (`src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:550-551`).

---

### GR-9 — Credential resolution precedence chain

Every secret the product needs is resolved through one fixed three-tier chain, evaluated fresh on **every** read with nothing cached and no snapshot taken anywhere. A credential exported into the environment mid-process therefore takes effect on the very next read without a restart. The tiers, in strict order, are:

1. **Process environment variables**, tried in a per-secret declared order; the first non-empty value wins.
2. **The operating-system credential store**, consulted **only** when the `useWindowsCredentialManager` setting is true (GR-10). Every failure at this tier — including "this platform has no such store" — is swallowed and treated as "not found".
3. **The deprecated plaintext slot in the settings document**, which remains fully live on the read path even though no supported user action can populate it.

If all three tiers yield nothing, the resolved value is the empty string. There is no way to prefer a lower tier over a higher one, no override flag, and no way to shadow an environment variable short of unsetting it outside the process. The exact names are product-wide:

| Secret | Environment variables, in order | Credential-store entry name | Deprecated document key |
|---|---|---|---|
| Azure API key | `CHATDBG_AZURE_API_KEY` | `ChatDbg:AzureApiKey` | `azureApiKey` |
| AWS access key | `CHATDBG_AWS_ACCESS_KEY`, then `AWS_ACCESS_KEY_ID` | `ChatDbg:AwsAccessKey` | `awsAccessKey` |
| AWS secret key | `CHATDBG_AWS_SECRET_KEY`, then `AWS_SECRET_ACCESS_KEY` | `ChatDbg:AwsSecretKey` | `awsSecretKey` |

**Evidence:** `src/Xcaciv.ChatDbg.Core/Models/ChatSettings.cs:69-77` (the three resolved properties and their variable orders), `:88-118` (the chain), `:120-141` (entry-name mapping and swallowed failure).

**Binds:** 1, 2, 5, 6, 8, 9, 13.

**Source inconsistency.** A fourth, undocumented channel exists outside this chain: when no key resolves, the cloud client-construction seam falls through to the vendor library's own ambient credential discovery, so a machine with ambient cloud credentials authenticates through a path this chain never describes (`src/Xcaciv.ChatDbg.Core/Services/DefaultBedrockRuntimeClientFactory.cs:11-20`). Session-token credentials are unsupported: the standard environment variables are hoisted into a static key pair and re-injected without any session token, and no session-token name appears anywhere in the product. One readiness check reads `AWS_ACCESS_KEY_ID` a second time directly, bypassing the chain it duplicates (`src/Xcaciv.ChatDbg.Core/Services/BedrockService.cs:27`).

---

### GR-10 — The operating-system credential store is opt-in, capability-detected and silently absent

The encrypted-at-rest credential tier is an **optional, pluggable secret source with a working "unavailable" branch**. It is consulted only when the boolean setting `useWindowsCredentialManager` is true, and it is only available on hosts that provide such a store; on every other host the capability probe reports false, the tier yields nothing, and the absence is silent. A reimplementation must model this as a pluggable source, must run a **run-time** capability probe rather than a compile-time platform switch (GR-24), and must hide every credential-store option, instruction and remediation sentence from help text and guidance when the running platform cannot offer one. Enabling the tier from the command surface is refused on a platform that has no store, with the message `Windows Credential Manager is not available on this platform.` Every failure at this tier — wrong platform, policy denial, corrupted store, exhausted handles, entry genuinely absent — is collapsed into the single outcome "not found", at two independent layers, with nothing logged and nothing reported.

**Evidence:** `src/Xcaciv.ChatDbg.Core/Models/WindowsCredentialManager.cs:59-62` (capability probe), `:88-91` (inner swallow), `:169-172` (write refused off-platform); `src/Xcaciv.ChatDbg.Core/Models/ChatSettings.cs:137-141` (outer swallow); `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:107-111` (refusal message); `src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs:219-224` (command-surface platform gate).

**Binds:** 2, 5, 6, 8, 9.

**Source inconsistency.** The full-screen settings dialog copies its checkbox straight into the flag and saves it with **no** availability check, so the flag can be enabled on a platform that has no store — after which every subsequent load prints an "enabled but not available" warning forever and every resolution wastes a probe that can only return nothing (`src/ChatDbg.Shell.Gui/UI/SettingsDialog.cs:431-437`, `:501` versus `src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs:219-222`). On a host with no store the remediation guidance dead-ends: the store-a-secret command refuses until the flag is enabled, and the enable command refuses because the platform is unsupported (`src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs:234-240` versus `:219-222`). A delete primitive is fully implemented with zero call sites anywhere, so nothing in the product can remove a stored secret (`src/Xcaciv.ChatDbg.Core/Models/WindowsCredentialManager.cs:148-163`).

---

### GR-11 — Report a secret's source, never its value

No surface may print, render, log, trace or echo a credential value. A credential is reported by **presence** and **provenance** only: the literal `***set***` when a value resolved and the literal `(not set)` when none did, accompanied by a source label drawn from a fixed four-value vocabulary shared across every provider:

- `environment variable ({VARIABLE_NAME})` — with the actual variable name that supplied the value
- `Windows Credential Manager`
- `settings file (deprecated)`
- `not set`

The resolved credential properties are excluded from serialisation entirely and never reach the settings document (GR-14). Diagnostic traces record full request and reply bodies but never credential values.

**Evidence:** `src/Xcaciv.ChatDbg.Core/Models/ChatSettings.cs:69-77` (resolved properties excluded from persistence), `:143-175` (source labels); `src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs:400-455` (masked status rendering).

**Binds:** 1, 2, 5, 6, 8, 9, 13.

**Source inconsistency — the product's second most consequential defect.** The migration-instruction printer interpolates the **actual stored secret values** into lines of the form `set CHATDBG_AZURE_API_KEY=<the real key>` and writes them to standard output. This block fires automatically on **every** settings load while any deprecated plaintext slot is populated — that is, at every launch — and again mid-way through the store-a-secret operation, because that operation re-reads settings from disk and re-triggers the load-path check. Under the full-screen host the secrets still reach the terminal scrollback; they are merely invisible while the interface is painted over them (`src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:60-64` reaching `:328-357`, interpolation at `:335`, `:341`, `:346`; re-entry at `:155`; `src/ChatDbg.Shell.Gui/Program.cs:71-90`). Separately, credentials typed on the console surface are echoed in the clear as ordinary command arguments and land in shell history; only the full-screen dialog offers a masked field (`src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs:243-244` versus `src/ChatDbg.Shell.Gui/UI/SettingsDialog.cs:538`). The keep-or-fix decision for the plaintext echo is routed to section 11.

---

### GR-12 — Per-user state roots, auto-created; nothing is written into the install directory

The application writes nothing into the directory it was launched from. Every piece of persistent state resolves through a well-known per-user directory lookup performed at run time, and every such directory is created on demand at first write. Any feature that adds persistence must follow this rule or the single-file distribution unit stops working. The product uses **three different roots** for three kinds of state:

| State | Path | Fallback |
|---|---|---|
| Settings | `<user profile directory>/.ChatDbg/settings.json` | `<system temporary directory>/settings.json`, silently, with no message, whenever the profile directory is null, empty, whitespace, or raises |
| System prompts | `<local application-data directory>/ChatDbg/system_prompts/<sanitised name>.json` | none |
| Diagnostic logs | `<roaming application-data directory>/ChatDbg/Logs/` | none |

The literal directory-name component is `.ChatDbg` for settings and `ChatDbg` for the other two, used unchanged on every operating system. There is no uninstall action and no state-removal command. **INFERRED:** on a host where the application-data lookup yields the empty string, the log path composes into a working-directory-relative path and logs land wherever the process happens to be running; nothing guards the empty root.

**Evidence:** `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:11-32` (path plus temporary-directory fallback), `:89-93` (directory created on demand); `src/Xcaciv.ChatDbg.Core/Services/SystemPromptService.cs:10-20`; `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs:20`.

**Binds:** 2, 4, 5, 6, 7, 10, 13, 15.

**Source inconsistency.** Three roots under two naming conventions is itself an inconsistency the source never reconciles, and a reimplementation should choose one convention deliberately. The temporary-directory fallback silently converts per-user settings into per-boot settings and relocates a document that can contain plaintext secrets into a world-readable location, with no message (`src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:20-31`). Every per-user path in the product's documentation is written in one platform's form only; the renderings for other platforms are documented nowhere.

---

### GR-13 — Path handling: tilde expansion, extension defaulting, name sanitisation, no sandboxing

Where a command accepts a file path, exactly one home-directory shorthand is recognised: the two-character sequence `~/` at position 0, which expands to the current user's profile directory. A bare `~`, the backslash form `~\`, and the `~someuser/` form are **not** expanded and produce a literal directory named `~` on disk. Where a command writes a conversation export, a path carrying no extension at all gets `.json` appended; a path carrying any extension is used verbatim. Where a name is turned into a file name, every character the host operating system forbids in file names is replaced with an underscore `_`. There is **no path sandboxing anywhere in the product**: no canonicalisation, no allow-list, no root restriction, no traversal check. Any path the operating system permits is read or written verbatim, including absolute paths outside the user's own directories and paths containing `..` segments.

**Evidence:** `src/Xcaciv.ChatDbg.Core/Commands/ExportCommand.cs:29-40` (expansion then extension defaulting); `src/Xcaciv.ChatDbg.Core/Commands/ImportCommand.cs:29-34` (expansion, no extension defaulting); `src/Xcaciv.ChatDbg.Core/Services/SystemPromptService.cs:209-214` (name sanitisation); absence of any canonicalisation or restriction on any path in `src/`.

**Binds:** 4, 5, 7, 11, 12, 13.

**Source inconsistency.** Tilde expansion exists on **only** two paths in the product — conversation export and conversation import. The system-prompt export and import commands accept paths with no expansion whatsoever, even though the user manual's own examples for those commands use `~/`-prefixed paths that therefore cannot work (`src/Xcaciv.ChatDbg.Core/Commands/PromptCommand.cs:307`, `:320`, `:337`, `:339` versus `README.md:189-190`). Extension defaulting is asymmetric: export appends `.json` but import does not, so `/export mychats` followed by `/import mychats` fails (`src/Xcaciv.ChatDbg.Core/Commands/ExportCommand.cs:37-40` versus `src/Xcaciv.ChatDbg.Core/Commands/ImportCommand.cs:28-34`). The defaulting test is "has any extension", not "has the conversation extension", so `/export notes.txt` writes the conversation format into a `.txt` file. Because the sanitisation character set is the **host's**, a prompt library is not portable between operating systems: the same name yields different file names on different platforms (`src/Xcaciv.ChatDbg.Core/Services/SystemPromptService.cs:209-214`).

---

### GR-14 — Persisted and exported wire format

Every document the product reads or writes is a UTF-8, indented (pretty-printed), brace-delimited structured text document with **explicit, fixed key names declared per field** — never names derived at run time from a naming convention. Users hand-edit these documents and exported conversations must remain re-importable by the same product, so **key names are a compatibility contract**. The rules:

- **Field naming** is camelCase throughout, with exactly one deliberate exception: the token-alternatives collection is persisted under the snake_case name `top_alternatives`. An explicitly declared name always overrides the camelCase convention.
- **Indentation** is on for every write path, so documents are human-readable and hand-editable.
- **Computed members and resolved secrets are never written.** The three resolved credential properties, the "has probabilities" predicate, and the derived probability value are all excluded.
- **Every persisted timestamp is coordinated universal time**, stored as an absolute instant. Timestamps are converted to local time only at display.
- **Absent optional values are written as an explicit null, not omitted**, so a record file always carries its full key set. A reimplementation that omits absent fields produces byte-different documents.
- **Every write is a whole-document overwrite.** There is no dirty tracking, no partial patch, no merge, no backup, no temporary-file-and-rename and no atomic replace anywhere in the product. An interrupted write leaves a truncated document.
- **Hand edits are read back verbatim with no validation and no repair** (GR-8). A document that fails to parse is left untouched on disk until the next successful save overwrites it.

Persisted conversation records carry: `messages` (each with `role`, `content`, `timestamp`, `isCommand`, `logProbabilities`), `sessionId`, `createdAt`. Persisted probability records carry `token`, `logprob`, `top_alternatives`. Persisted prompt records carry all five of their keys always, including an explicitly null last-used stamp.

**Evidence:** `src/Xcaciv.ChatDbg.Core/Models/ChatMessage.cs:6-19`; `src/Xcaciv.ChatDbg.Core/Models/ChatHistory.cs:7-12`; `src/Xcaciv.ChatDbg.Core/Models/TokenLogProbabilities.cs:13-30`; `src/Xcaciv.ChatDbg.Core/Models/SystemPrompt.cs:15-16`; `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:33-37` (indentation and camelCase); `src/Xcaciv.ChatDbg.Core/Services/ChatHistoryService.cs:36` (whole-file overwrite, previous file destroyed first).

**Binds:** 2, 4, 5, 6, 7, 11, 12, 13.

**Source inconsistency.** The prompt store's serialiser is configured for indented output **after** its constructor has already written the four seeded starter prompts, so those four files are written unindented on a single line while every later save is indented — leaving a library permanently mixed (`src/Xcaciv.ChatDbg.Core/Services/SystemPromptService.cs:32` running before `:34-37`, writer at `:106`). Conversation import performs **zero** validation: roles bypass the three-value whitelist that injection enforces, content is unbounded, timestamps are unchecked and the hidden-from-model flag is honoured as read (`src/Xcaciv.ChatDbg.Core/Commands/ImportCommand.cs:43-44`). Import discards the imported `createdAt` while copying `sessionId` on the adjacent line, so a re-exported file carries the importing process's start time (`src/Xcaciv.ChatDbg.Core/Commands/ImportCommand.cs:43-45`). One timestamp escapes the UTC rule: the diagnostic log file name is composed from the **local** date (`src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs:149`). The system-prompt export format is not the record format — it writes bare instruction text as `.txt`, so a round trip loses description, created-at and last-used (`src/Xcaciv.ChatDbg.Core/Commands/PromptCommand.cs:320` versus `:347-361`).

---

### GR-15 — Provider registry and key vocabulary

Exactly three backends exist and they are addressed by three lowercase literal keys: `azure`, `bedrock` and `llama`. The configured provider value is lowercased before it is stored. Any other value offered to the setting surface is rejected with the exact message `Provider must be 'azure', 'bedrock', or 'llama'`. The default provider is `azure`. A configured key that resolves to no registered backend at turn time is a recoverable per-turn failure: the console host prints `Error: Unknown AI provider: <key>` and returns to the prompt with the user's message already in the transcript; the full-screen host raises a modal titled `Error` reading `Unknown AI provider: <key>`.

**Evidence:** `src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs:46-52` (lowercasing, whitelist, message); `src/Xcaciv.ChatDbg.Core/Models/ChatSettings.cs:8` (default); `src/ChatDbg/ChatShell.cs:349-352`; `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:425-433`.

**Binds:** 1, 2, 5, 8, 9, 10, 13, 14.

**Source inconsistency.** Provider lookup is **case-sensitive** in the console host and **case-insensitive** in the full-screen host, so one hand-edited settings document containing `Azure` yields two entirely different behaviours: a warning at every launch plus a dropped turn in one host, normal operation in the other (`src/ChatDbg/ChatShell.cs:251`, `:349` versus `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:425-433`). The full-screen provider selector pre-selects the first entry for any unrecognised value, claiming a provider that is not in effect. The About dialog names two providers while general help names three.

---

### GR-16 — Uniform backend contract and the non-empty reply guarantee

Every chat backend implements one shared five-part contract and nothing more: **(1)** report a human-readable display name; **(2)** answer a readiness question from a settings record, with no side effects the caller must undo; **(3)** answer a conversation with plain reply text; **(4)** answer a conversation with reply text plus an optional per-token probability list; **(5)** be disposable (GR-23). Each host holds exactly one instance of each backend and may swap between them without knowing which it holds. A backend **must never return nothing** for the reply text: where the upstream answer is absent, it substitutes a fixed fallback sentence, and the shells append that sentence to the transcript as the assistant's turn. A backend that reports itself not ready causes the host to refuse the turn before any request is built.

**Evidence:** `src/Xcaciv.ChatDbg.Core/Services/IAIService.cs`; `src/ChatDbg/ChatShell.cs:349-361` (lookup, readiness gate, refusal); `src/Xcaciv.ChatDbg.Core/Services/AzureOpenAIService.cs:50`, `src/Xcaciv.ChatDbg.Core/Services/BedrockService.cs:33`, `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:58` (three fallback sentences).

**Binds:** 1, 2, 8, 9, 10, 11, 13, 14.

**Source inconsistency.** The same "reply text absent" condition produces **five** different user-visible outcomes across the product: `Error: Response text expected, none given.`, the misspelled `Error: Response text expected, none recieved.`, a correctly spelled `Error: Response text expected, none received`, a third backend's own wording, and an empty string substituted by the full-screen host (`src/Xcaciv.ChatDbg.Core/Services/AzureOpenAIService.cs:50`; `BedrockService.cs:33`; `LLamaSharpService.cs:58`; `src/ChatDbg/ChatShell.cs:377`; `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:444`). All of them are stored in the transcript and re-sent as context on the next turn. The readiness precondition and the client-construction precondition disagree for one backend: readiness passes on a model identifier plus an access key alone, while construction requires both halves of the key pair (`src/Xcaciv.ChatDbg.Core/Services/BedrockService.cs:23-27` versus `src/Xcaciv.ChatDbg.Core/Services/DefaultBedrockRuntimeClientFactory.cs:11`). The full-screen host performs **no** readiness check at all before sending, so an unconfigured user sees an internal, developer-worded guard message in a modal instead of the console host's actionable remediation text (`src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:420-441` versus `src/ChatDbg/ChatShell.cs:356-361`).

---

### GR-17 — Conversation context contract

Every turn re-sends the **entire** conversation transcript to the selected backend. There is no server-side thread identifier, no delta or incremental protocol, no history window, no truncation policy, no summarisation and no client-side token accounting or budget warning of any kind. Transcript entries flagged as command entries are excluded from every model request on every backend. The user's own message is appended to the transcript **before** any readiness check or backend lookup runs, so a turn that fails for any reason leaves the user's message in the transcript, where it is re-sent as context on the next successful turn. Nothing is ever removed from the transcript by a failure. A reimplementer must expect cost and latency to grow with the square of session length and must decide explicitly whether to add a windowing policy — the source has none.

**Evidence:** `src/ChatDbg/ChatShell.cs:346` (append before validation) versus `:349`, `:356`; `src/Xcaciv.ChatDbg.Core/Services/AzureOpenAIService.cs:82`, `:141` and `src/Xcaciv.ChatDbg.Core/Services/BedrockService.cs:51` (command entries excluded, whole transcript mapped); `src/Xcaciv.ChatDbg.Core/Models/ChatHistory.cs:14-24`.

**Binds:** 1, 2, 4, 8, 9, 10, 13, 14.

**Source inconsistency.** The local-inference backend ignores the command-entry exclusion entirely and reads only the **last** user-role entry, so the identical transcript produces materially different model context depending on which provider is selected (`src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:131`, `:361`). The three backends also disagree on role handling for the same transcript: one drops unrecognised roles, one forwards every role lowercased, and one coerces every non-assistant role to `user`, silently downgrading an injected system directive.

---

### GR-18 — Backend capability degradation

When the user asks for a capability the selected backend cannot supply, the turn still completes and the missing capability degrades to an explicit, visible notice — it never fails the turn and never silently disappears. Concretely, when token-probability capture is enabled and the backend returns no probability data, the console host prints the reply normally and then writes the two lines `Note: Log probabilities were requested but none were returned by the model.` and `This could be due to the model not supporting this feature or an API limitation.` A backend that cannot honour a tuning value ignores it rather than rejecting the request. A backend that cannot supply a capability never raises an error for that reason alone.

**Evidence:** `src/ChatDbg/ChatShell.cs:386-391` (the degradation notice); `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:550-551` (unsupported tunables silently ignored).

**Binds:** 1, 2, 8, 9, 10, 11, 12, 14.

**Source inconsistency.** In the source, GR-19's fabrication made this notice nearly unreachable for the default backend (resolved by D-001 — the fabrication is removed, so the notice becomes reachable again). Separately, one backend emits its probability-request fields on every call, as `false` and `0` rather than omitted, even when the feature is off (`src/Xcaciv.ChatDbg.Core/Services/BedrockService.cs:76-77`, `:101-102`), and a requested top-K is accepted by that backend's response parser and never applied, so a user asking for 5 alternatives may be shown more (`BedrockService.cs:199`, `:225`).

**Decision refinement (D-001, 2026-08-29).** This GR's degrade-to-notice rule now applies only to **transient** absence — a provider that declares the capability but returned no data on this response. **Capability absence** — the provider's declared capability record says it cannot supply token log probabilities at all — is handled by GR-19's decided behaviour instead: the setting is auto-disabled with a notice instructing the user to switch providers, the enabling control is disabled where the platform allows, and view attempts produce a non-blocking unavailable notice. Capability is always determined from the declared record, never by attempting a call and interpreting its failure.

---

### GR-19 — No fabricated telemetry: capability-absent providers disable the feature, with guidance

> **RESOLVED — Owner decision D-001 (2026-08-29, `DECISIONS.md`).** What follows states the *decided* requirement first; the observed source behaviour it replaces is retained below as the quirk record, because acceptance tests written against the source depend on it and the traceability contract requires it.

**The requirement (decided):**

1. **No path in the product may invent probability data and present it as measured.** All three of the source's fabricators — the hosted-cloud generator, the local engine's temperature step-function, and the token-inspection index-derived formulas — are removed, not reproduced. The sole permitted producer of synthetic data is the explicitly named demonstration mode, and every record it emits carries a `synthetic: true` stamp that all consumers propagate.
2. **When token log probabilities are enabled and the active provider does not support them** (capability absence per GR-18's declared-capability rule — never inferred from a single failed call), the product SHALL **disable the log-probabilities setting**, persist that change, and emit a one-line informational notice naming the provider, stating it does not support the capability, and **instructing the user to use a different LLM service provider** (naming a configured one that does, where one exists). The check fires on backend switch and on session start; the chat turn itself proceeds normally without capture.
3. **Where the platform allows it, the UI control for enabling log probabilities SHALL be disabled** while a capability-absent provider is active — greyed with an explanation naming the provider and pointing at the capability report — and **attempting to view probabilities SHALL produce a non-blocking error** (a transient status notice in the full-screen shell, an advisory line in the line-oriented shell; never a modal). The line-oriented enable commands refuse with the same instruction rather than silently succeeding.
4. **Transient absence is not capability absence.** A provider that *declares* the capability but returns no data on one response produces the honest "none were returned" notice of GR-18 and leaves the setting untouched.

The normative notice texts are recorded in `DECISIONS.md` D-001.

**The observed source behaviour this replaces (retained as the quirk record):**

When token-probability capture is enabled and the primary cloud backend's reply carries **no** probability data, the backend **invents** a probability list and returns it in the same shape, through the same field, as genuinely measured data. The fabricated list is derived from the reply text: each sampled token is assigned a log-probability of ln(0.9) — rendering as 90 % confidence — and up to three synthetic alternatives named `{token}_alt`, `similar_{token}` and `other_{token}` with log-probabilities of ln(0.05), ln(0.03) and ln(0.02). The fabricated data carries **no marker of any kind**. No consumer — neither host, neither renderer, nor an exported conversation document — can distinguish an invented value from a measured one, because there is no field, flag, colour, label or log line that says so. The fabrication is fully deterministic despite constructing a pseudo-random generator that it never uses, and it caps alternatives at three regardless of the user's configured top-K.

Because fabrication triggers whenever the extraction path yields nothing, and because the extraction path discards **all** successfully extracted probability records on encountering a single malformed entry, and because it never reaches the per-choice location where the real service actually puts the data whenever a differently shaped top-level block is present, a mostly-valid real response can be replaced wholesale by invented data. The consequence is that the honest "none were returned" notice of GR-18 is nearly unreachable on this backend.

The product's whole stated purpose is deep token-level introspection of model responses for developers analysing model uncertainty. Presenting invented confidence numbers to that audience, unmarked, is a product-level decision and not an implementation detail. A separate, explicitly named demonstration command already exists for showing the feature without a live backend, which argues the fallback is vestigial scaffolding.

**The keep-or-fix decision has been taken and recorded: Owner decision D-001 rules the fabrication out** (see the decided requirement at the head of this GR and `DECISIONS.md`). The observed behaviour above remains documented solely so that acceptance tests written against the source's observable output can be identified and rewritten deliberately rather than failing mysteriously.

**Evidence:** `src/Xcaciv.ChatDbg.Core/Services/AzureOpenAIService.cs:197-208` (the trigger and the in-code comment "generate some simulated ones so the user can see the feature works"), `:438-470` (the generator, its fixed 0.9 base, its three fixed alternatives, and the unused random generator at `:451`); `src/ChatDbg/ChatShell.cs:386-391` (the notice the fabrication pre-empts); `src/Xcaciv.ChatDbg.Core/Services/AzureOpenAIService.cs:245-270` (per-choice data unreachable when a differently shaped top-level block exists), `:298`, `:330-334` (one malformed entry discards every record already extracted). The extraction-precedence and partial-loss consequences are **INFERRED** from branch structure; no test exercises them.

**Binds:** 8 primarily; 1, 2, 11, 12, 14 consume the result and cannot tell the difference.

---

### GR-20 — Error classes and what the operator observes

Failures fall into four classes, and each class has one product-wide observable outcome:

| Class | Observable outcome |
|---|---|
| **Validation failure** (bad argument, out-of-range value, unknown key) | A failure result carrying a specific human-readable English sentence. Nothing is mutated, nothing is persisted, the session continues (GR-3, GR-4, GR-8). |
| **Handled operational failure** (backend error, unreadable file, failed parse) | A caught exception is re-raised or returned with a contextual prefix of the form `<Operation context>: <underlying message>` and rendered by the host per GR-4. Observed prefixes include `Error loading settings: `, `Error saving settings: `, `Error setting <key>: `, `Error configuring log probabilities: `, `Error during migration: `, `Error processing prompt command: `. Prefixes **nest verbatim**, so multi-level messages such as `Error processing prompt command: Error saving system prompt: <reason>` are the norm. The session always continues and nothing is removed from the transcript. |
| **Turn-level backend failure** | The console host prints `Error getting AI response: <message>` and returns to the prompt; the full-screen host shows a modal reading `Failed to get AI response: <message>`. The user's message remains in the transcript (GR-17). |
| **Fatal failure** (a fault escaping the outermost run) | Disposal runs first, then exactly one line `Fatal error: <message>` is written to standard output, then the process exits with status **1**. No stack trace and no log file is produced. Normal shutdown exits with status **0**. These two codes are the only machine-readable signals the shipped program emits. |

Full exception detail — type and call stack — goes only to a developer diagnostic channel (GR-25) and never to the operator.

**Evidence:** `src/ChatDbg/Program.cs:1-14` (fatal path and both exit codes); `src/ChatDbg/ChatShell.cs:405-412` (per-turn catch and continue); `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:78-82`, `:100-103` (prefixed messages); `src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs:291-295`.

**Binds:** all fifteen features.

**Source inconsistency.** Several failure paths violate the class table by reporting success. Settings saves swallow every failure and return normally (GR-6). Conversation export and import failures print their real cause to standard output and reduce the result to a generic `Failed to export chat history to: {path}` with no cause (`src/Xcaciv.ChatDbg.Core/Services/ChatHistoryService.cs:39-43`, `:60-64`). All diagnostic-log file operations and the analysis-export writer swallow every failure and report success, so a user whose target directory is unwritable sees no error and gets no file (`src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs:187-190`). Declining a consent prompt is reported as a failure reading `Failed to enable Windows Credential Manager integration.`, conflating refusal with breakage (`src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs:255-257`). A corrupt settings document is reported only as `Error loading settings: <message>` and then silently replaced by an all-defaults record, discarding the user's entire configuration, which the next save overwrites (`src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:78-82`). One backend reports an unparseable reply as a **successful** turn whose assistant text is the literal `Unable to parse model response` (`src/Xcaciv.ChatDbg.Core/Services/BedrockService.cs:202-204`). The reply envelope carries an error field and an elapsed-time field that no backend populates and no host reads.

---

### GR-21 — No cancellation, no timeout, no retry, no back-off, no caching

Every service and command entry point in the product is asynchronous and **not one of them accepts a cancellation signal**. There is no cancellation, no timeout, no interrupt handling, no idle expiry and no progress reporting on any path in the product. A model load, a generation, a network request or an output dump cannot be aborted from any surface; a hung operation blocks until it completes or the process is killed. There is likewise no retry, no back-off, no rate-limit or retry-after handling, no circuit breaker, no queue bound and no proxy or connection-pool configuration on any backend: whatever the underlying transport defaults to is the only bound on any remote call. Nothing anywhere is cached — not clients, not responses, not credentials (GR-9), not configuration — so every operation re-reads settings and re-resolves secrets.

**This is a defect and a reimplementer must not clone it.** Its absence is uniform across the source, which makes it a repo-wide convention rather than a per-feature oversight, and that is precisely why it must be recorded once here rather than repeatedly excused in section 7. A user who triggers a slow local generation or a stalled remote request has no way to return to the prompt, sees only the word `Thinking...`, and must terminate the process. Any reimplementation is expected to thread a cancellation signal through every entry point, bound every outbound request with an explicit timeout, and expose an interrupt to the operator. Doing so is a deliberate deviation from observed behaviour and belongs in section 11's decision list.

**Evidence:** absence of any cancellation parameter on every member of `src/Xcaciv.ChatDbg.Core/Models/ICommand.cs:3-9` and `src/Xcaciv.ChatDbg.Core/Services/IAIService.cs`; `src/ChatDbg/ChatShell.cs:363` (`Thinking...` with no abort); absence of any timeout, retry or back-off configuration in `AzureOpenAIService.cs`, `BedrockService.cs` and `LLamaSharpService.cs`.

**Binds:** all fifteen features.

---

### GR-22 — Serial execution and mutual exclusion

The product is strictly serial at the user level: exactly one user-initiated operation runs at a time and is awaited to completion before the next input is accepted. There is no queueing, no parallelism, no re-entrancy protection and no user-level locking anywhere. On top of that, the local-inference backend enforces two **process-wide** mutual-exclusion gates that are the only concurrency guards in the entire codebase: **at most one generation in flight at any moment**, and **at most one model load in flight at any moment**. Both gates are process-scoped rather than instance-scoped, so they hold across every backend instance in the process. A reimplementation must preserve the at-most-one-generation-in-flight guarantee: the native inference engine is not safe to drive concurrently, and the gate is what makes the serial user model safe rather than merely convenient.

There is **no** mutual exclusion on persisted state. Settings and prompt documents are read-modify-written whole with no file lock, no temporary-file-and-rename and no optimistic-concurrency token, so two instances of the product sharing one settings document interleave writes with no detection and the loser's edits vanish with no message, and a crash mid-write leaves a truncated document. Partial mutations of shared state are never rolled back: there is no transaction, compensation or undo concept anywhere in the product, and neither destructive conversation operation (remove-last, clear-all) confirms or can be undone.

**Evidence:** `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:23-24` (both process-wide gates), `:71-72` (generation gate acquired), `:518` (model-load gate acquired); `src/ChatDbg/ChatShell.cs:80-118` (serial read-eval loop); `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:85-104` and `src/Xcaciv.ChatDbg.Core/Services/SystemPromptService.cs:100-113` (unguarded whole-file overwrite); `src/Xcaciv.ChatDbg.Core/Commands/ClearCommand.cs:18-22` and `PopCommand.cs:19-30` (no confirmation, no undo).

**Binds:** 1, 2, 4, 5, 7, 10, 13, 14.

**Source inconsistency.** Interactive confirmation prompts inside the service layer block the calling thread on standard input with no timeout and no cancellation, which under the full-screen host blocks the interface thread on input the user cannot see or type (GR-26). The generation gate, though process-wide and shared, is disposed from an **instance** teardown path, so disposing one backend instance renders the gate unusable for any other (`src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:681`). **INFERRED**: latent today because each host holds exactly one instance.

---

### GR-23 — Resource lifecycle and disposal

Every backend is disposable and every host must dispose every backend it constructed, whether or not that backend was ever selected. The hosts construct **all three** backends at startup and dispose all three at shutdown regardless of which provider is configured, so a session that never touches local inference still allocates the native diagnostic buffer and still writes a diagnostic log file at exit. Disposal of the local-inference backend is the one place in the product where real native resources are released, and it must release, in order: the session handle, the executor, the inference context, the model, and the diagnostic recorder. A reimplementation must treat the native model and context as scarce, explicitly released resources — a machine can hold only one loaded model of any size — and must not rely on automatic reclamation to release them.

**Evidence:** `src/ChatDbg/ChatShell.cs:29-34` (all three constructed), `:698-713` (all disposed); `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:670-688` (native release order).

**Binds:** 1, 2, 8, 9, 10, 11, 13, 14.

**Source inconsistency.** The full-screen host's main window declares **no** teardown at all and never releases its backends; this is harmless only because two of the three backends' release operations do nothing (`src/ChatDbg.Shell.Gui/UI/ChatWindow.cs`, no disposal member, versus `src/ChatDbg/Program.cs:5`). The local backend's automatic-reclamation path routes to shared teardown with the explicit-disposal flag false, and the recorder teardown lives only on the true branch, so a backend reclaimed rather than disposed loses its entire unflushed diagnostic buffer with no final flush and no report (`src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:690-696` versus `:676-682`). Shutdown does not uninstall the process-global engine diagnostic destination, and the installed callback retains a reference to the recorder's buffer, so engine diagnostics keep accumulating into a buffer that will never be flushed (`src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs:81` versus `:193-200`). One cloud backend's automatic-reclamation hook never releases the transport it created, leaking it while keeping every instance alive an extra collection cycle (`src/Xcaciv.ChatDbg.Core/Services/AzureOpenAIService.cs:480-496`), and another constructs a fresh remote client on every single turn paired with a release operation that does nothing (`src/Xcaciv.ChatDbg.Core/Services/BedrockService.cs:45`, `:326-344`).

---

### GR-24 — Run-time platform guards and graceful degradation

Platform-specific capabilities are guarded by **run-time** operating-system checks only — never by conditional compilation, never by per-platform source variants, and never by separate builds. An absent capability yields "no value" rather than raising. This is what allows one unmodified source tree to be published for every supported platform from one build profile, and any reimplementation must preserve the property: one artefact, run-time probes, silent absence. Exactly one capability in the product is platform-coupled — the operating-system credential store (GR-10) — and everything else is platform-neutral. On hosts without that store the credential chain degrades to environment variables plus the deprecated document slot, which means those users have **no encrypted-at-rest option at all** and nothing tells them so. Any port therefore needs either a per-platform substitute for that tier or a documented environment-variable-only posture.

A second product-wide rule follows from the packaging profiles: **any code reachable only by name-based lookup, dynamic discovery or reflection is at risk of removal in a shipped build**, and the analysis diagnostics that would warn about it are suppressed, so violations surface only at run time as silently missing behaviour. Every feature that persists or discovers anything must be written to be statically analysable, or the build must not eliminate unreferenced code.

**Evidence:** `src/Xcaciv.ChatDbg.Core/Models/WindowsCredentialManager.cs:59-62`, `:169-172` (the only platform probe, refusal returns false rather than raising); `src/Xcaciv.ChatDbg.Core/Models/ChatSettings.cs:137-141` (absence collapses to "no value"); `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:423-427` (a platform member resolved by literal name at run time, with "not found" treated as an empty result).

**Binds:** 2, 5, 6, 8, 9, 10, 13, 14, 15.

**Source inconsistency.** The product's own security documentation instructs users on platforms with no credential store to enable it anyway; off-platform the store yields nothing, the enable command is refused, and the failure is silent (`docs/SECURITY-IMPLEMENTATION.md:226`, `:233`, `:236` versus `src/Xcaciv.ChatDbg.Core/Models/WindowsCredentialManager.cs:59-62`). The shipped packaging profiles default their target runtime to one 64-bit desktop platform in a product whose own assembly description calls itself cross-platform (`src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:16`, `:30`, `:70`), and every per-user path in the documentation is rendered in that one platform's form.

---

### GR-25 — Diagnostics, logging and audit

The product has **no logging framework, no log level control, no structured logging and no audit record of any user action**. Two diagnostic channels exist and no others:

1. **A developer diagnostic trace channel.** Full request bodies, full reply bodies and full exception detail — type and call stack — are written to it unredacted and untruncated. Credential values are excluded from it (GR-11) but **every prompt and every model answer is written verbatim**, so in any deployment this channel must be treated as carrying sensitive conversation content. It is never surfaced to the operator. Critically, this channel is **removed at compile time in any build that does not define the diagnostic build symbol**, which no shipped configuration does, so in every build a user actually runs the channel is inert. No subsystem may route its own internal failure reports through a channel that can vanish at build time.
2. **A diagnostic log file for the local-inference engine.** It lives under the roaming application-data root per GR-12, rolls over daily into files named `llamasharp_{yyyyMMdd}.log`, and formats each entry as `[{yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}`. The in-memory buffer flushes when it exceeds 10 000 characters. There is no retention policy, no size cap, no compression and no deletion of old files, so a long-lived installation grows on disk without bound.

A third product-wide rule governs both: **diagnostics must never break the thing they are diagnosing.** Every operation on a diagnostic path swallows its own exceptions and returns no status. The cost is that a non-functional diagnostic subsystem is today indistinguishable from a healthy one on every channel, so a reimplementation should preserve the never-break property but add an out-of-band health signal.

**Evidence:** `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs:20` (root), `:40` (buffer threshold default 10 000), `:83` (entry format), `:90` (threshold flush), `:149` (daily file name), `:187-190` (failures swallowed); `src/Xcaciv.ChatDbg.Core/Services/BedrockService.cs:108`, `:124` (full conversation and reply traced); absence of the diagnostic build symbol in `Directory.Build.props:1-5` and all four project files.

**Binds:** 1, 2, 8, 9, 10, 11, 13; log-file mechanics in detail are owned by section 7's diagnostic-logging subsection.

**Source inconsistency.** The size-triggered flush fires only for engine-sourced lines and never for application entries, even though application entries are by far the higher-volume producer — one per generated token when probability capture is on — so the largest buffers are precisely the ones never trimmed (`LLamaSharpLogConfig.cs:90` present versus `:123-126` absent). Nothing anywhere records when a secret was stored, rotated or read.

---

### GR-26 — Presentation degradation and host-channel discipline

The product's rendering assumes a modern, Unicode-capable, attached terminal and **selects no fallback at run time under any condition**. Colour, box-drawing borders and the non-ASCII glyphs used for outcome markers (GR-4), column headers and status prefixes are emitted unconditionally; terminal output encoding is never configured anywhere, so on a legacy code page the banner frame, the outcome markers and the numero-sign column header render as replacement characters. Confidence banding is conveyed by **colour alone**, with no textual label and no monochrome fallback, so the information is unavailable to a colour-blind operator, a monochrome terminal or redirected output. Grid layouts query the terminal width unguarded, so a redirected or detached output stream can surface as an unrelated turn-level failure (**INFERRED**; not reproduced). Column widths and wrap points are measured in code units rather than display cells, so wide East-Asian glyphs and combining sequences mis-measure in every table, panel and wrap point.

The companion rule is **host-channel discipline**, which the product states by violating: the result message must be a command's only output channel. A service or command that writes directly to standard output, or reads directly from standard input, corrupts the display of any host that owns the screen, and is untestable without a real console.

**Evidence:** absence of any encoding configuration in `src/ChatDbg/Program.cs` and `src/ChatDbg/ChatShell.cs`; `src/ChatDbg/ChatShell.cs:657-671` (colour-only banding), `:501` (unguarded width query), `:103-104` (glyph-only outcome signal).

**Binds:** 1, 2, 3, 5, 7, 11, 12, 13, 14.

**Source inconsistency.** Host-channel discipline is broken in at least five places, all of them in the host that can least tolerate it. Two inspection commands write directly to the terminal (37 and 10 direct writes respectively); the system-prompt content editor writes prompts to standard output and reads lines from standard input until a line equal to `END`, which cannot work under a full-screen host on any platform; and the entire credential subsystem — the load-time plaintext warning, the migration instructions carrying secrets, the wizard menus, both consent prompts and the enable banner — writes to standard output and blocks on standard input inside the service layer, beneath a full-screen interface painted over the same terminal (`src/Xcaciv.ChatDbg.Core/Commands/InspectCommand.cs`, `TokenizeCommand.cs`, `PromptCommand.cs:258-270`, `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs` throughout, with `src/ChatDbg.Shell.Gui/Program.cs:71-90`). The full-screen host injects the rich renderer into exactly the commands that cannot tolerate direct writes. Separately, a command that reads further lines from standard input while the read-eval loop is blocked inside it silently consumes lines intended as conversation under piped input, and takes the negative branch at end of stream; and end-of-input on the console host is treated identically to a blank line, producing an infinite busy loop that re-prints the prompt forever with no exit path (`src/ChatDbg/ChatShell.cs:83-88`).

---

### GR-27 — Text, culture and encoding

All user-visible text in the product — command names, descriptions, usage strings, menu and dialog titles, prompts, success and failure messages, help text and log level labels — is a hard-coded English literal embedded at its call site. There is no message catalogue, no resource lookup, no formatting indirection, no localisation hook and no locale awareness in the text layer anywhere. All source files must be stored in a single encoding, UTF-8, and must emit real characters or plain ASCII markers.

Text is unlocalised but **rendering is locale-dependent**, which is the product's central culture defect: numeric parsing and numeric, percentage and timestamp formatting all follow the ambient locale, and every user-visible list is ordered by culture-sensitive collation rather than byte order. Because two of the shipped packaging configurations force invariant-globalisation mode, **the same source produces different behaviour depending on how it was published**: a decimal value with a comma separator is rejected by one build and accepted by another on the same machine; percentages gain a space before the sign; timestamps and orderings change; and the culture-sensitive prefix test of GR-2 changes which inputs count as commands. A reimplementation must pin one culture explicitly for all parsing, formatting and comparison rather than relying on the ambient one, and must use ordinal comparison for every identifier match.

**Evidence:** `src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs:72` (ambient-culture numeric parse); `src/ChatDbg.Shell.Gui/UI/SettingsDialog.cs:420-422`; `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:52`, `:95` and `src/ChatDbg.Shell.Gui/Xcaciv.ChatDbg.Shell.Gui.csproj:52`, `:95` (invariant globalisation forced in two of four configurations); `src/ChatDbg/ChatShell.cs:92` (culture-sensitive prefix test).

**Binds:** all fifteen features.

**Source inconsistency.** The single-encoding rule is broken twice, and both breaks reach the user. One service file is stored as pure ASCII with status glyphs committed as literal question-mark characters, so the program really prints `??  WARNING: Credentials found in settings file...` and `? Credential stored securely...` (`src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:62`, `:110`, `:180`, `:257`). One command file and one dialog file carry a raw byte `0x95` — a legacy single-byte bullet that is not valid UTF-8 — 30 times, so every bullet in the detailed configuration help renders as a replacement character (`src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs:307-352`; `src/ChatDbg.Shell.Gui/UI/SettingsDialog.cs`). Literals must therefore be re-authored in a reimplementation rather than copied. One further consistent oddity is deliberate enough to look intentional and is preserved rather than corrected: every usage string, error message and documentation line that means "a name" writes the placeholder token `<n>`, which reads as "a number".

---

### GR-28 — Documentation-versus-code precedence, and build-flavour divergence

**Where the repository's own documentation and its code disagree, the code is the specification.** The source ships a 14.5 KB user manual plus fourteen further documents, several of which are aspirational or status documents describing partially-complete work. Across the fifteen feature analyses the documentation was found to contradict the code on command counts, provider counts, menu parity, target runtime version, persisted-field lists, dependency versions, credential storage claims, export workflows, log rotation promises, path renderings and example output formats. No behaviour claimed by a document may become a requirement until it is confirmed in code.

A second, related rule governs the shipped artefacts: **the packaging configuration changes observable behaviour, so "the product" is not one behaviour but a family of them.** The two size-optimised publish configurations force invariant globalisation (GR-27), substitute bare symbolic keys for framework-supplied exception message text, strip debugging symbols, disable stack-trace metadata, suppress code-elimination analysis warnings (GR-24), and default the target runtime to one 64-bit desktop platform. The consequence is that every diagnostic surface in the product is materially degraded in exactly the builds users run: an operational failure message that a developer sees as an explanatory sentence degrades to an opaque key. A reimplementation should carry its own diagnostic text rather than interpolating runtime-supplied exception messages, and must pin the culture and the runtime target explicitly rather than inheriting them from a size-reduction recipe.

**Evidence:** `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:52-53`, `:58-59`, `:70`, `:95-96` and the same lines in `src/ChatDbg.Shell.Gui/Xcaciv.ChatDbg.Shell.Gui.csproj`; `README.md` versus code at the many points listed in the quirk register; `src/ChatDbg/prd.md` and `src/ChatDbg.Shell.Gui/prd.md` (two stale duplicated specification copies describing a two-provider product).

**Binds:** all fifteen features.

**Source inconsistency.** The repository as pinned **cannot be built at all**: the toolchain pin document is not valid structured text — it carries one closing brace too many — so the build tool refuses to start from any directory inside the tree, and the release pipeline additionally requests a different major toolchain line than the pin demands (`global.json:6`; `.github/workflows/build-release.yml:36-39`). One shipped packaging profile also suppresses a runtime-configuration side-car that its own artefact needs in order to start (`src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:79`). Consequently the majority of this specification was derived by reading rather than by execution, and every claim resting on run-time behaviour is labelled INFERRED where it was not measured.

---

### Rules considered and rejected as not product-wide

The fifteen feature authors nominated 196 candidate rules. 182 of them were duplicates or near-duplicates of one another and were merged into GR-1 through GR-28 above — the credential chain alone was nominated seven times, the settings path six times, the absence of cancellation eight times, the localisation posture nine times, and the command grammar seven times. The 14 below were rejected outright because they bind exactly one feature, or because they are recommendations rather than observed behaviour, or because they were factually contradicted by another feature's evidence. Each is redirected to the section 7 subsection that owns it.

| Candidate rule | Why it belongs to one feature | Section 7 owner |
|---|---|---|
| Transient status message reverts to the provider/model line after 3000 ms via an unconditional fire-and-forget timer | Only the full-screen host has a status line; the console host has no equivalent surface and no timer | 7.14 Terminal GUI Shell |
| Diagnostic log entries are formatted `[{yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}` and roll over daily into `llamasharp_{yyyyMMdd}.log` | Exactly one subsystem writes a log file; the format binds nothing else. The *root path* is product-wide and stayed as GR-12 | 7.11 Diagnostic Logging & Log Export |
| Cloud backends expose a substitutable single-operation client-construction seam so the request path is exercisable with no network | A testability affordance on two provider adapters only; the product never registers an alternative implementation | 7.06 / 7.07 Provider integrations |
| The provider readiness message names credentials for every backend, including one that reads no credential | One message string emitted from the console host's turn path | 7.13 Chat Session Console |
| Nothing in the presentation layer is cached; views are torn down and rebuilt widget-by-widget on every refresh, giving allocations proportional to transcript length per refresh | A rendering-loop property of one host | 7.14 Terminal GUI Shell |
| Every usage and error string meaning "a name" uses the placeholder token `<n>` | A wording convention confined to the prompt and settings command surfaces; noted as a preserved oddity under GR-27 | 7.05 System Prompts |
| "No product behaviour reads any environment variable" | **Factually contradicted** by GR-9: three secrets resolve from five named environment variables. The nominating feature simply reads none itself | — (withdrawn) |
| No service may read standard input directly; prompts must go through an injectable interaction port | A *recommendation*, not observed behaviour. The observed behaviour — services do read standard input and write standard output — is recorded as the source inconsistency under GR-26 | 7.03 Credential Management (observed), section 11 (recommendation) |
| Tests must place the clock, the file store and the diagnostic sink behind seams, and every named behaviour must carry at least one assertion | Test-suite hygiene, not product behaviour; no user-observable consequence | — (not a product requirement) |
| Version metadata is hand-maintained in several independent places with no reconciliation step | A packaging concern with no run-time behaviour beyond the reported version string | 7.15 Packaging, Build & Release |
| Shipped publish configurations default their runtime target to one 64-bit desktop platform | A packaging default; its *behavioural* consequences (invariant culture, stripped diagnostics) are product-wide and stayed as GR-28 | 7.15 Packaging, Build & Release |
| Conversation exports must remain re-importable by the same product, with computed members never exported | A specific instance of the general wire-format contract; the general rule stayed as GR-14, the export/import symmetry rules bind one feature | 7.04 Chat History |
| Console-echo mode for engine diagnostics interleaves raw log lines into the chat transcript with no visual separation | A switch on one subsystem that no shipping code path ever enables | 7.11 Diagnostic Logging & Log Export |
| Each backend must be constructed fresh per turn / must reuse one client across turns | The three backends disagree with each other, so no product-wide rule can be stated; the divergence is recorded per adapter, and the disposal obligation that *is* shared stayed as GR-23 | 7.06 / 7.07 / 7.08 |
