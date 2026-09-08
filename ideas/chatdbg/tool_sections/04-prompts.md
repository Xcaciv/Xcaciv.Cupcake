## 4. ChatDbg.Tools.SystemPromptLibrary — System Prompt Library

> Root command: **`PROMPT`**
> Source ancestor: feature 7 *System Prompt Management* (`Core/Services/SystemPromptService.cs`, `Core/Models/SystemPrompt.cs`, `Core/Commands/PromptCommand.cs`, `Gui/UI/SystemPromptsDialog.cs`).
> PRD traceability: **7.5 System Prompt Management** (owner), with named touch-points into 7.1, 7.2, 7.6, 7.11, 7.13, 7.14.

---

### 4.0 Purpose and boundary

**What this package owns.** The *library of named instruction texts* and everything that manipulates it:

* the on-disk prompt store (one record per prompt: `name`, `content`, `description`, `createdAt`, `lastUsedAt`);
* the built-in starter set of four prompts (`default`, `code-reviewer`, `algorithm-helper`, `security-expert`) and the rule that seeds them;
* listing, filtering, showing, creating, editing, renaming, duplicating, deleting, importing and exporting prompts;
* prompt metadata — description, created-at, last-used-at — including *when* last-used is stamped;
* the *act of selection*: naming which prompt is active and publishing that name plus its text so other packages can consume it;
* store diagnostics: where the store is, which files are unreadable, which names collide.

**What this package does NOT own.**

| Not owned | Owner |
|---|---|
| The persisted `systemPromptName` setting, the settings document at `~/.ChatDbg/settings.json`, and the `SET`-style key/value surface (`/set systemPrompt <n>` in the source) | **ChatDbg.Tools.Settings** (root `SET`) — PRD 7.2. `PROMPT USE` publishes the selection; the Settings package persists it. |
| Injecting the active prompt text into a model request as the system message | **ChatDbg.Tools.Providers** (root `AI`) — PRD 7.6/7.8. This package never calls a model. |
| Conversation turns, message injection, history import/export | **ChatDbg.Tools.ChatHistory** (root `HISTORY`) — PRD 7.4. A system prompt is not a chat message here. |
| Any secret, credential or keystore access | **ChatDbg.Tools.Credentials** (root `CRED`) — PRD 7.3. Prompt text is *not* a secret and is stored in clear text. |
| Counting or visualising the tokens a prompt costs | **ChatDbg.Tools.TokenInspection** (root `TOKEN`) — PRD 7.10. `PROMPT SHOW -content-only` pipes into it. |
| Rendering, colour, heat-maps, dialogs | **ChatDbg.Tools.Rendering** / the host shell — PRD 7.12/7.13/7.14. Every tool here emits text or a declared result format; nothing here draws. |
| Log files and log export | **ChatDbg.Tools.DiagnosticLogging** (root `LOG`) — PRD 7.11. `PROMPT DOCTOR` pipes into it. |

**Deliberate deviation from the source, stated once.** The source shipped two prompt surfaces — a text command and a Terminal.Gui dialog — that disagreed about duplicate protection, the active-prompt delete guard, last-used stamping and export defaults (dossier Q6, Q13, R21, R25). **This package is the single surface.** A graphical shell drives these same tools; it does not carry a second implementation. Every behavioural divergence recorded as Q6/Q8/Q13/Q19/Q20/Q21/Q27 in the dossier is therefore resolved *in favour of the command path*, except where a row below says otherwise.

---

### 4.1 Package manifest

| Attribute | Value |
|---|---|
| Assembly / package | `ChatDbg.Tools.SystemPromptLibrary` (`ChatDbg.Tools.SystemPromptLibrary.dll`) |
| Root command | `PROMPT` (declared once as `[CommandRoot("PROMPT", "System prompt library")]` on every tool class) |
| Contract assemblies targeted | `Xcaciv.Command.Interface` **3.3.4**, `Xcaciv.Command.Core` **3.3.4** (`AbstractCommand`, `IResult<string>`, `Dictionary<string,IParameterValue>` piped-chunk contract). No reference to `Xcaciv.Command` (the host) and none to any ChatDbg shell — the dependency arrow points tool → SDK only (Cupcake rule 4). |
| Target framework | `net10.0`, `ImplicitUsings`, `Nullable`, `IsPackable=true`, no `AllowUnsafeBlocks` |
| Elevated trust | **No.** No P/Invoke, no `Reflection.Emit`, no dynamic assemblies, no unsafe code, no process spawning *except* the opt-in `PROMPT EDIT -editor` path (below). Designed to load cleanly under `AssemblySecurityPolicy.Strict` with `DisallowDynamicAssemblies = true` and preflight enabled (ref-loader §10 Step 4). |
| Network | **None.** No socket is opened by any tool in this package. |
| Filesystem reach | Two directories, both configurable, both required to be *directories the tool resolves itself* — never a caller-supplied path (see §4.2): **(a)** the prompt store, default per-user local-application-data `/ChatDbg/system_prompts` (Windows `%LocalAppData%\ChatDbg\system_prompts`, Linux `$XDG_DATA_HOME/ChatDbg/system_prompts` → `~/.local/share/ChatDbg/system_prompts`, macOS `~/Library/Application Support/ChatDbg/system_prompts`); **(b)** the exchange directory used by `IMPORT`/`EXPORT`, default per-user documents directory, falling back to the home directory when the documents folder resolves empty (fixes dossier Q18 — it must **never** silently become the process working directory). |
| OS keystore | **None.** |
| Native libraries | **None.** |
| Process spawn | Only `PROMPT EDIT -editor`, which launches `$VISUAL`/`$EDITOR` (fallback `notepad` on Windows, `nano` then `vi` elsewhere). This is the one capability a restricted host should switch off; see `CHATDBG_PROMPT_ALLOW_EDITOR`. |
| Safe to load in a restricted host | **Yes**, with two conditions: the store directory must be writable (otherwise the package degrades to read-only, §4.16), and `CHATDBG_PROMPT_ALLOW_EDITOR` must be `false` if external editors are not permitted. Under a fully read-only policy every mutating tool fails with one clear message rather than throwing. |
| Environment-modifying registration | Exactly **one** tool needs it: `PROMPT USE`. Register as `controller.AddCommand("ChatDbg.Prompts", typeof(PromptUseCommand), modifiesEnvironment: true)`. Every other tool registers with the default `false`. |
| Audit posture | No parameter or output in this package is a secret. `-text` and `description` values *do* land verbatim in `AuditEvent.Parameters`; see §4.17. |

---

### 4.2 Conventions that apply to every tool in this package

These are stated once and referenced by each tool's tables.

**C1 — No tool in this package accepts a filesystem path as a parameter.** The framework's argument tokenizer (`NamesValidator.GetArgumentsFromCommandline`, ref-command §8.3) splits unquoted tokens on `[\w-]+` and strips `\ / : = , ; < > { } +` **even inside quotes**. `"/home/u/p.txt"` reaches a command as `homeup.txt`. Path parameters are therefore *unimplementable* in this framework and are replaced by **basenames resolved against the exchange directory** (`-file <basename>`), which also gives path confinement for free. Absolute paths remain reachable through two un-tokenized channels: the interactive prompt (`IIoContext.PromptForCommand`, whose return value is **not** tokenized) and the environment (`CHATDBG_PROMPT_EXCHANGE_DIR`, set by the host from configuration, not by the `SET` command — `SET`'s value token is tokenized too). This is a deliberate, documented deviation from the source's raw-path `export`/`import` (dossier B9/B10, Q17).

**C2 — Content is never authored inline where fidelity matters.** The same tokenizer strips `: ; < > / \ { } + = ,` from every argument, so `-text "You are ChatDBG: help users…"` loses the colon. Inline `-text` exists for short, alphanumeric content only and every tool that offers it says so in its help. The fidelity-preserving content channels are, in order of preference: **the pipe** (piped chunks are delivered verbatim as `IResult<string>.Output`, untouched by the tokenizer), **`-file <basename>`**, and **the interactive editor** (`PromptForCommand` loop, also verbatim).

**C3 — Zero-argument invocations get no parameter defaults.** `AbstractCommand.ProcessParameters` returns an empty dictionary when `io.Parameters.Length == 0` (ref-command §3.4) — no defaults applied, no flags materialised, no field injection. Every tool here is specified to fall back to its documented default *in code*, using `parameters.TryGetValue(k, out var p) && p.IsValid ? p.GetValue<T>() : <default>`. The tables below give the effective default, which is what the user observes whether or not any argument was typed.

**C4 — Name matching is exact, ordinal, and case-preserving, on every operating system.** The source inherited case sensitivity from the host file system (dossier R6) — case-insensitive on Windows, case-sensitive on Linux, so the same store behaved differently per OS. This package pins **ordinal, case-sensitive** matching everywhere and stores an explicit index, so `Default` and `default` are two prompts on every platform.

**C5 — File names are sanitized with one fixed, platform-independent forbidden set.** The source delegated to the host OS: 41 forbidden characters on Windows, 2 on Linux, making stores non-portable (dossier R3, R5, Q21). This package pins the **Windows set** on all platforms — code points 0–31 plus `" < > | : * ? \ /` — each replaced by `_`, and additionally refuses (rather than silently collides) when two distinct names sanitize to the same file name. Reserved Windows device names (`CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`, `LPT1`–`LPT9`) are prefixed with `_` on every platform.

**C6 — Ordering.** Default listing order is ascending by name using **culture-sensitive** collation, exactly as the source (dossier R11: `a, a_b, a-b, ab, B`, *not* the ordinal `B, a, a-b, a_b, ab`). `-collation ordinal` switches to byte order. Note for the architect: the source's size-optimised publish profiles disabled globalization and silently changed this ordering — the package therefore ships its own collation selection rather than inheriting the runtime's.

**C7 — Timestamps.** Stored UTC, ISO-8601 round-trip with `Z`. Rendered converted to local time in the culture's general short date/time pattern (date + `HH:mm`, no seconds), and the literal `Never used` when `lastUsedAt` is absent — both verbatim from the source (dossier R24, R27). `-format json` emits raw UTC instead.

**C8 — On-disk record.** One JSON file per prompt, `<sanitized name>.json`, five keys in declaration order: `name`, `content`, `description`, `createdAt`, `lastUsedAt`. `lastUsedAt` is always written, as an explicit `null` when unset (dossier Q24 — preserved, because omitting it changes nothing for readers and keeps the file self-describing). **Deviation:** every write is indented, including the seeded built-ins — the source seeded compact and wrote indented afterwards (Q1), a cosmetic accident with no consumer. **Deviation:** every write is temp-file-plus-atomic-rename with an advisory lock file, closing the source's clobber/truncation window (Q16).

**C9 — Failure is data, not an exception.** Every tool returns `CommandResult<string>.Failure(message, ex)` rather than throwing. A throw would be reduced by `CommandExecutor` to `"Error executing <CMD> (see trace for more info)"` with the real reason visible only in the trace (ref-command §5.8), which is exactly the source's opaque `Error processing prompt command: <message>` behaviour and is worth improving on.

**C10 — Environment keys are global and explicit.** This package declares an **empty `GetDefaultEnvironment()`** on every tool. Rationale: the host seeds declared defaults under the `{COMMANDNAME}_` prefix (ref-command §2.4), and for sub-commands the command name is the *verb* (`LIST`, `USE`), which would produce collision-prone keys such as `LIST_DIR` shared with every other package's `LIST`. Instead each tool reads fully-qualified global keys (`CHATDBG_PROMPT_*`) with `storeDefault: false`, so a mere read never flips `HasChanged` and never provokes a write-back.

---

### 4.3 Tool catalog

Fourteen tools. Nine are ported; five are `NEW`.

| Tool | Origin | One line |
|---|---|---|
| `PROMPT STATUS` | ported: bare `/prompt` | Show the active prompt and its text |
| `PROMPT LIST` | ported: `/prompt list` | Enumerate, filter and sort the library |
| `PROMPT SHOW` | ported: `/prompt show <n>` | Show one prompt with its metadata, or just its text |
| `PROMPT USE` | ported: `/prompt use <n>`, `/set systemPrompt <n>` | Select the active prompt |
| `PROMPT CREATE` | ported: `/prompt create <n> [desc]` | Create a new prompt |
| `PROMPT EDIT` | ported: `/prompt edit <n>` | Replace a prompt's content and/or description |
| `PROMPT RENAME` | **NEW** | Rename a prompt, preserving all metadata |
| `PROMPT COPY` | **NEW** | Duplicate a prompt under a new name |
| `PROMPT DELETE` | ported: `/prompt delete <n>` | Delete a prompt |
| `PROMPT IMPORT` | ported: `/prompt import <n> <file> [desc]` | Import a prompt from the exchange directory |
| `PROMPT EXPORT` | ported: `/prompt export <n> [file]` | Export a prompt to the exchange directory |
| `PROMPT RESET` | **NEW** | Restore missing (or all) built-in starter prompts |
| `PROMPT PATH` | **NEW** | Report the store and exchange directories |
| `PROMPT DOCTOR` | **NEW** | Diagnose the store: unreadable files, collisions, dangling active name |

---

#### 4.3.1 `PROMPT STATUS` — show the active prompt

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Status", "Show the currently active system prompt",
    Prototype = "PROMPT STATUS [-format text|json] [-content-only]",
    Alias = "CURRENT", Version = "1.0.0")]
[CommandParameterNamed("format", "Output shape",
    DefaultValue = "text", AllowedValues = new[] { "text", "json" })]
[CommandFlag("content-only", "Emit only the prompt text, with no headings")]
[CommandHelpRemarks("The source product spelled this as a bare '/prompt'. The framework requires a sub-command on a root, so the bare form is unavailable; STATUS is its replacement.")]
```

| Field | Value |
|---|---|
| Command | `STATUS` |
| Root | `PROMPT` |
| Description | Show the currently active system prompt and its text |
| Prototype | `PROMPT STATUS [-format text\|json] [-content-only]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text`, `json` | Output shape. `json` sets `ResultFormat.JSON` on the chunk. |
| `content-only` | flag | `bool` | no | `false` | — | Emit only the prompt text — the pipe-friendly form. **NEW** |

**Pipeline behaviour.** Produces piped output; does **not** accept piped input (it has no per-item work to do). One chunk. When `-content-only` is set the chunk is exactly the prompt text with no trailing hints, so it composes cleanly into `TOKEN COUNT` or `AI ASK`. Without it the chunk reproduces the source's five-part block verbatim:

```
Current system prompt: <activeName>

Content:
<text>

Use 'PROMPT LIST' to see all available prompts
Use 'PROMPT USE <name>' to switch to another prompt
```

(The source printed `<n>` as its name placeholder everywhere, including the README — dossier Q22. This package prints `<name>`; recorded as a deliberate deviation because `<n>` reads as "a number".)

**Environment interaction.** Reads `CHATDBG_SYSTEM_PROMPT` (active name; default `default`), `CHATDBG_SYSTEM_PROMPT_TEXT` (runtime active text), `CHATDBG_PROMPT_DIR`. Writes nothing. No environment-modifying permission needed.

**Failure modes.** Active name names a prompt that no longer exists → the block is still emitted, using `CHATDBG_SYSTEM_PROMPT_TEXT` if the host set it, otherwise the built-in fallback text, plus one line `(the prompt named '<name>' is no longer in the library — run 'PROMPT DOCTOR')`. Store unreadable → success with the runtime text and the same advisory. Never fails.

**Security and audit.** No secret. Non-destructive. `-content-only` output may be long; the audit event records parameters only, not output.

**Traceability.** PRD 7.5; source `/prompt` with no arguments (`Commands/PromptCommand.cs:58-73`), plus the startup banner's `System Prompt: <name>` line (`src/ChatDbg/ChatShell.cs:201`).

---

#### 4.3.2 `PROMPT LIST` — enumerate the library

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("List", "List the system prompts in the library",
    Prototype = "PROMPT LIST [-name-like <glob>] [-text-like <words>] [-sort name|created|lastused] [-collation culture|ordinal] [-format text|names|csv|json] [-limit <n>] [-unused] [-builtin]")]
[CommandParameterNamed("name-like", "Case-insensitive glob over prompt names, e.g. code-*")]
[CommandParameterNamed("text-like", "Case-insensitive substring match over prompt content")]
[CommandParameterNamed("sort", "Sort key", DefaultValue = "name",
    AllowedValues = new[] { "name", "created", "lastused" })]
[CommandParameterNamed("collation", "Name-ordering rule", DefaultValue = "culture",
    AllowedValues = new[] { "culture", "ordinal" })]
[CommandParameterNamed("format", "Output shape", DefaultValue = "text",
    AllowedValues = new[] { "text", "names", "csv", "json" })]
[CommandParameterNamed("limit", "Maximum entries to emit; 0 means all",
    DataType = typeof(int), DefaultValue = "0")]
[CommandFlag("unused", "Only prompts that have never been used")]
[CommandFlag("builtin", "Only the four built-in starter prompts")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name-like` | named | `string` | no | *(none — no filter)* | glob, `*` and `?` | Filter by name. **NEW** |
| `text-like` | named | `string` | no | *(none)* | free text (tokenizer-limited, see C2) | Filter by content substring, case-insensitive. **NEW** |
| `sort` | named | `string` | no | `name` | `name`, `created`, `lastused` | Sort key. Source always sorted by name (R11). **NEW** for the other two. |
| `collation` | named | `string` | no | `culture` | `culture`, `ordinal` | `culture` reproduces the source's ordering exactly. **NEW** |
| `format` | named | `string` | no | `text` | `text`, `names`, `csv`, `json` | `text` = source's three-line entry block; `names` = one bare name per chunk. **NEW** |
| `limit` | named | `int` | no | `0` | `0`–`1000`; clamped, `0` = unlimited | Bound the output. **NEW** (Cupcake rule 45: clamp result counts). |
| `unused` | flag | `bool` | no | `false` | — | Only prompts whose `lastUsedAt` is unset. **NEW** |
| `builtin` | flag | `bool` | no | `false` | — | Only `default`, `code-reviewer`, `algorithm-helper`, `security-expert`. **NEW** |

**Pipeline behaviour.** **Source only** — emits piped output, refuses piped input. Because `AbstractCommand`'s non-piped path emits exactly one chunk (ref-command §3.3), this tool **overrides `Main`** to yield **one chunk per prompt**, so downstream stages see individual records and backpressure works. `-format names` makes each chunk a bare prompt name, which is the canonical feed for `SHOW`, `EXPORT`, `DELETE` and `USE`. Declared output shape: `ResultFormat.General` for `text`/`names`, `ResultFormat.CSV` for `csv`, `ResultFormat.JSON` for `json` — metadata only, since the framework never branches on it (ref-command §10.5); the host renderer is what reads it. When piped input is present the tool emits a single explanatory failure chunk, `PROMPT LIST does not accept piped input; it is a pipeline source.` (Cupcake rule 29.)

Text entry shape, verbatim from the source (dossier B2, acceptance criterion 3):

```
- <name>[ (current)]
  Description: <description>
  Created: <local date HH:mm>, Last used: <local date HH:mm>      ← or "Never used"
```

with a blank line after each entry, preceded by `Available system prompts:` and a blank line, and — **fixing dossier Q2** — closed by `System prompts directory: <the real absolute path>` instead of the source's accidental rendering of a .NET collection type name (`System.Collections.Generic.List` of `SystemPrompt`). Empty store → the single line `No system prompts found.`, a **success** (source B2, acceptance criterion 5).

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_SYSTEM_PROMPT` (for the ` (current)` marker). Writes nothing.

**Failure modes.** Store directory missing → it is created, then the built-in seeding rule runs (§4.3.12), so the first list on a clean machine shows four prompts. Store directory unreadable or vanished mid-run → one failure chunk `Cannot read the prompt library at <path>: <reason>` — the source let this escape as the generic `Error processing prompt command:` (Q26). Individual unreadable/corrupt files → **skipped, and counted**, with a trailing line `1 file could not be read; run 'PROMPT DOCTOR' for details.`; the source wrote a raw line to stdout that the GUI shell swallowed entirely (R9). `limit` outside range → clamped silently. Unknown `format`/`sort` value → `ArgumentException` from the allow-list at parse time, surfaced by the host as `Error executing LIST (see trace for more info)`; the prototype and `HELP PROMPT LIST` carry the allowed values.

**Security and audit.** No secret. Non-destructive. `-text-like` values appear in the audit event's parameter array; harmless.

**Traceability.** PRD 7.5; source `/prompt list` (`Commands/PromptCommand.cs:76-108`) and the GUI list pane (`UI/SystemPromptsDialog.cs:131-143`).

---

#### 4.3.3 `PROMPT SHOW` — show one prompt

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Show", "Show one system prompt and its metadata",
    Prototype = "PROMPT SHOW <name> [-content-only] [-format text|json]")]
[CommandParameterOrdered("name", "Name of the prompt to show", UsePipe = true)]
[CommandFlag("content-only", "Emit only the prompt text")]
[CommandParameterNamed("format", "Output shape", DefaultValue = "text",
    AllowedValues = new[] { "text", "json" })]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** (unless piped — `UsePipe = true`) | — | any existing prompt name | The prompt to show. |
| `content-only` | flag | `bool` | no | `false` | — | Emit only the text, no headings. **NEW** |
| `format` | named | `string` | no | `text` | `text`, `json` | `json` emits the whole record including raw UTC timestamps. **NEW** |

**Pipeline behaviour.** **Both.** As a source it emits one chunk. As a filter it accepts piped input: **one chunk = one prompt name** (trailing/leading whitespace trimmed; empty chunks are already dropped by `AbstractCommand.Main`), and emits one chunk per input name, so `PROMPT LIST -format names | PROMPT SHOW -content-only` concatenates the whole library. Because `name` is declared `UsePipe = true`, it is excluded from command-line parsing when piped, so the name need not — and must not — be given twice.

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_SYSTEM_PROMPT` (to mark the shown prompt as current). Writes nothing. Does **not** stamp `lastUsedAt` — matching the source exactly (dossier R25: only selection stamps).

**Failure modes.** Missing name and no pipe → failure `Please specify a prompt name: PROMPT SHOW <name>` (source wording, `<n>` → `<name>`). Unknown name → failure `Prompt not found: <name>` (verbatim). Corrupt record file → **distinguishable from missing**, unlike the source (which reported both as "not found", dossier error table): `Prompt '<name>' exists but could not be read: <reason>`. Piped chunk that is not a known name → one failure chunk for that item; the stream continues with the next chunk.

**Security and audit.** No secret. Non-destructive. Prompt content is user-authored text and is echoed to output — a host that pipes prompt content into an audit sink should be aware it is unbounded in length.

**Traceability.** PRD 7.5; source `/prompt show <n>` (`Commands/PromptCommand.cs:110-143`) and GUI **Show** (`UI/SystemPromptsDialog.cs:168-202`).

---

#### 4.3.4 `PROMPT USE` — select the active prompt

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Use", "Make a prompt the active system prompt",
    Prototype = "PROMPT USE <name> [-no-stamp]")]
[CommandParameterOrdered("name", "Name of the prompt to activate", UsePipe = true)]
[CommandFlag("no-stamp", "Do not update the prompt's last-used timestamp")]
[CommandHelpRemarks("Register this tool with modifiesEnvironment: true. Without it the selection cannot reach the global environment and the tool falls back to the store marker file.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** (unless piped) | — | any existing prompt name | The prompt to activate. |
| `no-stamp` | flag | `bool` | no | `false` | — | Suppress the `lastUsedAt` update. **NEW** — exists so scripted/bulk selection does not rewrite records. |

**Pipeline behaviour.** **Both.** As a filter, one chunk = one prompt name; each chunk performs a selection and emits `Now using system prompt: <name>` (verbatim source wording). Last-selection-wins, which makes `… | PROMPT USE` in a multi-name pipeline a legal but pointless composition — documented rather than blocked.

**Environment interaction.** This is the only tool here that writes globals.
* Reads: `CHATDBG_PROMPT_DIR`, `CHATDBG_SYSTEM_PROMPT` (previous value, for the audit trail).
* Writes: `CHATDBG_SYSTEM_PROMPT` = the selected name, and `CHATDBG_SYSTEM_PROMPT_TEXT` = the selected text.
* **Requires environment-modifying permission**: registered with `modifiesEnvironment: true` so `CommandController.Run` promotes the child environment into globals (ref-command §7.3a). A package-directory load registers with `modifiesEnvironment: false` by default, so in that mode the promotion silently would not happen — see the degradation rule below.
* `CHATDBG_SYSTEM_PROMPT_TEXT` is **runtime-only and must never be persisted**, mirroring the source's `[JsonIgnore]` on the active-text field (dossier R19). The Settings package persists only the *name*.

**Order of effects**, matching the source (dossier B4) with the persistence step relocated:
1. Resolve the name in the store (fail if unknown — no state changes).
2. Publish name and text to the global environment.
3. Stamp `lastUsedAt = UtcNow` and rewrite the record atomically, unless `-no-stamp`.
4. Write the store marker file `<store>/.active` (**NEW** — the degradation path).
5. Emit `Now using system prompt: <name>`.

The source's step "persist settings to disk" is *not* performed here: publishing to the environment is the contract, and **ChatDbg.Tools.Settings** persists `systemPromptName` in response (source R22 — settings were saved immediately on selection; that guarantee is preserved, the owner of it moves).

**Failure modes.** Missing name and no pipe → `Please specify a prompt name: PROMPT USE <name>`. Unknown name → `Prompt not found: <name>` and **no state change of any kind** (source acceptance criterion 7). Store read-only → the selection still publishes to the environment and the tool returns success with `Selected '<name>'; the last-used timestamp could not be written (library is read-only).` — degrade, do not fail. Not registered as environment-modifying → the publish cannot reach globals; the tool detects this (it re-reads its own child environment), writes the `.active` marker, and returns success with `Selected '<name>'. This host did not grant environment write permission, so the selection was recorded in the library only; restart or 'SET systemPrompt <name>' to apply it.` This is the single most important degrade-don't-fail path in the package.

**Security and audit.** No secret. Not destructive, but it *is* a state change with process-wide effect: the audit event records the command, the parameter array, and — via `EnvironmentContext.SetValue`'s `LogEnvironmentChange` — the old and new values of `CHATDBG_SYSTEM_PROMPT`. `CHATDBG_SYSTEM_PROMPT_TEXT` is a full prompt body and will be written in full into the environment-change audit record; a host that wants terse audit lines should add `CHATDBG_SYSTEM_PROMPT_TEXT` to `IAuditMaskingConfiguration.RedactedParameterNames`, which *does* work on the environment-change path (ref-command §11.4). No confirmation required.

**Traceability.** PRD 7.5 (primary) and 7.2 (the persisted setting it feeds); source `/prompt use <n>` (`Commands/PromptCommand.cs:145-171`), `/set systemPrompt <n>` (`Commands/SetCommand.cs:138-161`), GUI **Use** (`UI/SystemPromptsDialog.cs:205-226`). The GUI's failure to stamp last-used (Q6/R25) is not reproduced; stamping is now uniform.

---

#### 4.3.5 `PROMPT CREATE` — create a prompt

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Create", "Create a new system prompt",
    Prototype = "PROMPT CREATE <name> [-text <words>] [-file <basename>] [-force] [<description words...>]")]
[CommandParameterOrdered("name", "Name of the new prompt")]
[CommandParameterNamed("text", "Prompt content, inline (see remarks about lossy tokenization)")]
[CommandParameterNamed("file", "Basename of a file in the exchange directory to use as the content")]
[CommandFlag("force", "Overwrite an existing prompt of the same name")]
[CommandParameterSuffix("description", "Description for the new prompt", IsRequired = false)]
[CommandHelpRemarks("Inline -text is lossy: the argument tokenizer strips ':' '/' '\\' ';' '<' '>' '{' '}' '+' '=' ',' even inside quotes. For real prompt text pipe it in, use -file, or run PROMPT EDIT.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** | — | non-empty after sanitization (C5) | Name of the new prompt. |
| `text` | named | `string` | no | *(built-in default text — see below)* | tokenizer-limited (C2) | Inline content. **NEW** (the source's create never accepted content). |
| `file` | named | `string` | no | *(none)* | basename only, no separators (C1); must exist in the exchange directory | Content from a file. **NEW** |
| `force` | flag | `bool` | no | `false` | — | Overwrite an existing prompt. **NEW** |
| `description` | suffix | `string` | no | `Custom prompt: <name>` | free text (tokenizer-limited) | All remaining words become the description — the source's exact behaviour. |

When neither `-text`, `-file` nor piped input supplies content, the new prompt gets the **built-in default text**, verbatim from the source (dossier R20):

> `You are ChatDBG, a helpful debugging assistant. Help users analyze code, debug issues, and understand programming concepts.`

`createdAt` = now (UTC); `lastUsedAt` = unset.

**Pipeline behaviour.** **Both, with a special piped contract.** As a source it emits one chunk. When piped, **the entire upstream stream is the new prompt's content**: `OnStartPipe` opens a buffer, each chunk is appended with a newline separator, `OnEndPipe` writes the record once and the per-chunk return value is an **empty success** so nothing is echoed mid-stream (the host drops empty successes — ref-command §3.3). The confirmation chunk is emitted from `OnEndPipe`'s flush. This is the fidelity-preserving authoring path (C2): `HISTORY SHOW -last | PROMPT CREATE from-session` captures text the tokenizer would otherwise mangle. If the pipe yields nothing at all, the prompt is created with the built-in default text and the message notes it.

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_PROMPT_EXCHANGE_DIR`, `CHATDBG_PROMPT_MAX_CHARS`. Writes nothing. No environment-modifying permission needed.

**Failure modes.** Missing name → `Please specify a prompt name: PROMPT CREATE <name> [description]`. Name already exists and no `-force` → `Prompt already exists: <name>. Use 'PROMPT EDIT <name>' to modify it.` (source wording, command spelling updated) — **creation stays non-destructive**, and the GUI's silent overwrite (Q6) is *not* reproduced. Name empty or whitespace after sanitization → `'<name>' is not a usable prompt name.` (the source accepted these, R4). Name collides with an existing file after sanitization → `'<name>' would overwrite the storage of '<other>'; choose a different name.` (fixes R5). `-file` names a file that does not exist in the exchange directory → `File not found in the exchange directory: <basename>. Run 'PROMPT PATH' to see where that is.` `-file` and `-text` both given → `Specify only one of -text and -file.` Content longer than `CHATDBG_PROMPT_MAX_CHARS` → `Content is <n> characters; the limit is <max>. Raise CHATDBG_PROMPT_MAX_CHARS or shorten it.` Store read-only → `The prompt library at <path> is read-only.` Upstream failure chunk arriving through the pipe → forwarded verbatim by `AbstractCommand.Main` and the buffer is discarded without writing anything: **a broken upstream never produces a half-written prompt.**

**Security and audit.** No secret, but see §4.17: an inline `-text` value is recorded in the audit event's parameter array in full. Non-destructive without `-force`; with `-force` it is an overwrite and therefore requires confirmation (see §4.17 confirmation rule).

**Traceability.** PRD 7.5; source `/prompt create <n> [description]` (`Commands/PromptCommand.cs:173-211`) and GUI **Create…** (`UI/SystemPromptsDialog.cs:228-338`), whose Name/Content-required validation is preserved and whose missing duplicate check is not.

---

#### 4.3.6 `PROMPT EDIT` — replace content and/or description

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Edit", "Replace a prompt's content and/or description",
    Prototype = "PROMPT EDIT <name> [-text <words>] [-file <basename>] [-editor] [-description <words>] [-append]")]
[CommandParameterOrdered("name", "Name of the prompt to edit")]
[CommandParameterNamed("text", "Replacement content, inline (lossy — see remarks)")]
[CommandParameterNamed("file", "Basename of a file in the exchange directory to use as the content")]
[CommandParameterNamed("description", "Replacement description")]
[CommandFlag("editor", "Open the content in $VISUAL/$EDITOR")]
[CommandFlag("append", "Append to the existing content instead of replacing it")]
[CommandHelpRemarks("With no content source, EDIT enters interactive mode: type the new content and end with a line containing only END. Ctrl-D / end-of-input also ends the edit.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** | — | any existing prompt name | The prompt to edit. |
| `text` | named | `string` | no | *(none)* | tokenizer-limited (C2) | Inline replacement content. **NEW** |
| `file` | named | `string` | no | *(none)* | basename only (C1) | Replacement content from a file. **NEW** |
| `description` | named | `string` | no | *(unchanged)* | free text | Replacement description. **NEW at the command surface** — the source could only change a description through the GUI dialog (dossier B7). |
| `editor` | flag | `bool` | no | `false` | — | Round-trip the content through `$VISUAL`/`$EDITOR`. **NEW**, and refused when `CHATDBG_PROMPT_ALLOW_EDITOR` is `false`. |
| `append` | flag | `bool` | no | `false` | — | Append rather than replace. **NEW** |

**Content-source precedence** (exactly one is used, in this order): piped input → `-file` → `-text` → `-editor` → interactive `END`-terminated entry. Giving two explicit sources is an error, not a silent precedence win.

**Interactive mode** reproduces the source's console editor (dossier B7) through `IIoContext.PromptForCommand`, which is un-tokenized and therefore lossless:

```
Editing system prompt: <name>
Enter the new content below. Type 'END' on a line by itself when finished.
Current content:
<existing text>

New content (END to finish):
```

Terminated by a line equal to `END` (case-sensitive, untrimmed — the source's exact sentinel). **Fixes dossier Q4:** end-of-input also terminates the loop, and an accumulated-length ceiling (`CHATDBG_PROMPT_MAX_CHARS`) bounds it, so a closed or redirected stdin can no longer spin forever appending blank lines. Trailing whitespace and newlines are stripped from the result, as in the source. Interactive mode is **unavailable when `HasPipedInput` is true** (contract of `PromptForCommand`) — which is fine, since a pipe is itself a content source.

**Pipeline behaviour.** **Both**, with the same accumulate-then-flush contract as `CREATE`: one chunk is one line/segment of the new content, chunks return empty successes, and `OnEndPipe` performs the single atomic write and emits `Updated system prompt: <name>` (verbatim source wording).

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_PROMPT_EXCHANGE_DIR`, `CHATDBG_PROMPT_MAX_CHARS`, `CHATDBG_PROMPT_ALLOW_EDITOR`, `CHATDBG_SYSTEM_PROMPT` (to detect editing the active prompt), and `VISUAL` / `EDITOR` only under `-editor`. Writes `CHATDBG_SYSTEM_PROMPT_TEXT` **only when the edited prompt is the active one** — the source refreshed the in-memory active text in the console path but not in the GUI path (B7 vs Q6); this package always refreshes, and because that write is a global it needs `modifiesEnvironment: true` to take effect. When the host has not granted it, the tool degrades: the record is saved and the message adds `Restart or run 'PROMPT USE <name>' for the change to take effect in this session.`

**Failure modes.** Missing name → `Please specify a prompt name: PROMPT EDIT <name>`. Unknown name → `Prompt not found: <name>. Use 'PROMPT CREATE <name>' to create it.` (verbatim). Two content sources → `Specify only one content source (-text, -file, -editor or a pipe).` `-editor` with editors disabled → `External editors are disabled in this host (CHATDBG_PROMPT_ALLOW_EDITOR=false). Use -file or pipe the content in.` `-editor` with no editor resolvable → names the variables it tried. Editor exits non-zero or the temp file is unchanged → **no write**, message `Edit cancelled; '<name>' is unchanged.` Content exceeds the ceiling → same message as `CREATE`. Upstream failure through the pipe → forwarded, buffer discarded, record untouched.

**Security and audit.** No secret. **Destructive in the sense that it overwrites content that cannot be recovered** — see the confirmation rule in §4.17: `EDIT` writes a single-generation backup `<store>/.backup/<sanitized>.json` before replacing, which `PROMPT DOCTOR` reports and which is the cheapest possible undo. `-editor` spawns a process; a restricted host must disable it.

**Traceability.** PRD 7.5; source `/prompt edit <n>` (`Commands/PromptCommand.cs:241-283`) and GUI **Edit…** (`UI/SystemPromptsDialog.cs:340-431`). The GUI's ability to edit descriptions is folded in; its failure to refresh the active text is not.

---

#### 4.3.7 `PROMPT RENAME` — rename a prompt  **NEW**

**Why it earns its place.** In the source the only way to rename was export → import → delete, which silently reset `createdAt`, dropped the description, and (per Q12) could not round-trip metadata at all. Renaming is the single most common library-maintenance action and it should not destroy provenance.

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Rename", "Rename a prompt, preserving its description and timestamps",
    Prototype = "PROMPT RENAME <name> <newname> [-force]")]
[CommandParameterOrdered("name", "Existing prompt name")]
[CommandParameterOrdered("newname", "New prompt name")]
[CommandFlag("force", "Overwrite an existing prompt at the new name")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** | — | existing prompt | Prompt to rename. **NEW** |
| `newname` | ordered | `string` | **yes** | — | usable name (C5), must not collide after sanitization | New name. **NEW** |
| `force` | flag | `bool` | no | `false` | — | Overwrite an existing prompt at the new name. **NEW** |

**Pipeline behaviour.** Neither source nor filter in a meaningful sense: it emits one confirmation chunk and **declines piped input** with `PROMPT RENAME does not accept piped input (it needs two names).` — the explanatory-refusal convention (Cupcake rule 29).

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_SYSTEM_PROMPT`. Writes `CHATDBG_SYSTEM_PROMPT` **only when the renamed prompt was the active one**, so the active selection follows the rename instead of dangling — requires `modifiesEnvironment: true`, degrading with an advisory when absent.

**Failure modes.** Either name missing → `Please specify both names: PROMPT RENAME <name> <newname>`. Unknown source → `Prompt not found: <name>`. Target exists without `-force` → `Prompt already exists: <newname>. Use -force to replace it.` New name unusable or colliding after sanitization → the `CREATE` wording. Write failure mid-operation → the operation is a write-new-then-delete-old sequence, so a crash leaves **both** copies rather than none; the message says which state the library is in and `PROMPT DOCTOR` reports the duplicate.

**Security and audit.** No secret. **Destructive with `-force`** → confirmation required. `createdAt` is preserved; `lastUsedAt` is preserved; only `name` changes.

**Traceability.** **NEW** — no source ancestor. PRD 7.5.

---

#### 4.3.8 `PROMPT COPY` — duplicate a prompt  **NEW**

**Why it earns its place.** The source's two-step authoring flow (`create` then `edit`) always started from the same generic default text, so deriving a variant of `security-expert` meant re-typing or re-importing it. Duplication is the natural way to fork a persona, and it is explicitly in this package's scope.

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Copy", "Duplicate a prompt under a new name",
    Prototype = "PROMPT COPY <name> <newname> [-force] [<description words...>]",
    Alias = "DUPLICATE")]
[CommandParameterOrdered("name", "Prompt to copy")]
[CommandParameterOrdered("newname", "Name of the copy")]
[CommandFlag("force", "Overwrite an existing prompt at the new name")]
[CommandParameterSuffix("description", "Description for the copy", IsRequired = false)]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** | — | existing prompt | Source prompt. **NEW** |
| `newname` | ordered | `string` | **yes** | — | usable name (C5) | Name of the copy. **NEW** |
| `force` | flag | `bool` | no | `false` | — | Overwrite an existing target. **NEW** |
| `description` | suffix | `string` | no | `Copy of <name>` | free text | Description for the copy. **NEW** |

The copy gets `createdAt` = now (it is a new record) and `lastUsedAt` unset; content is byte-identical to the source prompt.

**Pipeline behaviour.** Emits one chunk. Declines piped input, explanatorily, for the same reason as `RENAME`.

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`. Writes nothing. No environment-modifying permission.

**Failure modes.** As `RENAME`, minus the active-prompt concern (the original is untouched, so the active selection never dangles).

**Security and audit.** No secret. Destructive only with `-force` → confirmation required.

**Traceability.** **NEW** — no source ancestor. PRD 7.5.

---

#### 4.3.9 `PROMPT DELETE` — delete a prompt

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Delete", "Delete a system prompt",
    Prototype = "PROMPT DELETE <name> [-force] [-yes]", Alias = "REMOVE")]
[CommandParameterOrdered("name", "Name of the prompt to delete", UsePipe = true)]
[CommandFlag("force", "Delete even if it is the active prompt")]
[CommandFlag("yes", "Skip the confirmation question")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** (unless piped) | — | existing prompt | Prompt to delete. |
| `force` | flag | `bool` | no | `false` | — | Override the active-prompt guard. **NEW** — the source's command path refused unconditionally, the GUI path had no guard at all (R21). |
| `yes` | flag | `bool` | no | `false` | — | Skip the interactive confirmation. **NEW** — required for non-interactive and piped use. |

**Confirmation.** Interactive and not `-yes` → `IIoContext.PromptForCommand("Delete prompt '<name>'? [y/N] ")`, defaulting to **No** on anything but `y`/`yes` (case-insensitive). The source's GUI listed **Yes** first and focused it, so a stray Enter deleted the alphabetically-first prompt (Q19 + Q27); defaulting to No is the deliberate correction. When `HasPipedInput` is true, `PromptForCommand` is not meaningful, so **`-yes` is mandatory in a pipeline**: without it each piped chunk fails with `Refusing to delete '<name>' in a pipeline without -yes.`

**Pipeline behaviour.** **Both.** As a filter, one chunk = one prompt name; each deletion emits `Deleted system prompt: <name>` (verbatim) or one failure chunk, and the stream continues — a single bad name never aborts a bulk delete.

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_SYSTEM_PROMPT`. Writes nothing. When `-force` deletes the active prompt, the tool does **not** silently leave a dangling selection: it emits an additional line `The active prompt was deleted; 'PROMPT STATUS' will fall back to the built-in default text until you run 'PROMPT USE'.` (The source's GUI left exactly this dangling state with no notice — R21.)

**Failure modes.** Missing name → `Please specify a prompt name: PROMPT DELETE <name>`. Unknown name → `Prompt not found: <name>`. Active prompt without `-force` → `Cannot delete the currently active prompt. Switch to another prompt first with 'PROMPT USE <name>'.` (source wording; `<n>` → `<name>`). Already gone → treated as success with `Prompt '<name>' was already absent.` (the source's store silently succeeded here — dossier error table; making it explicit costs nothing). Store read-only → `The prompt library at <path> is read-only.` Deleting the **last** prompt is allowed and warns: `The library is now empty; the four built-in prompts will be restored on the next launch (or run 'PROMPT RESET' now).` — this is the source's R15 behaviour, surfaced instead of surprising.

**Security and audit.** No secret. **Irreversible and destructive** → confirmation required by default (see above); the deleted record is copied to `<store>/.backup/` first, so `PROMPT DOCTOR` can report one generation of recoverable deletions. The audit event's parameter array carries the deleted prompt's name — deliberately, since deletion is the action most worth auditing here.

**Traceability.** PRD 7.5; source `/prompt delete <n>` (`Commands/PromptCommand.cs:213-239`) and GUI **Delete** (`UI/SystemPromptsDialog.cs:433-459`).

---

#### 4.3.10 `PROMPT IMPORT` — import a prompt from a file

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Import", "Import a prompt from a file in the exchange directory",
    Prototype = "PROMPT IMPORT <name> <basename> [-format text|json|auto] [-force] [<description words...>]")]
[CommandParameterOrdered("name", "Name to give the imported prompt")]
[CommandParameterOrdered("basename", "File name (no directories) inside the exchange directory")]
[CommandParameterNamed("format", "Interpretation of the file", DefaultValue = "auto",
    AllowedValues = new[] { "auto", "text", "json" })]
[CommandFlag("force", "Overwrite an existing prompt of the same name")]
[CommandParameterSuffix("description", "Description for the imported prompt", IsRequired = false)]
[CommandHelpRemarks("Only a bare file name is accepted: the argument tokenizer removes path separators. Run 'PROMPT PATH' to see, or CHATDBG_PROMPT_EXCHANGE_DIR to change, the directory that name is resolved against.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** | — | usable name (C5) | Name for the imported prompt. |
| `basename` | ordered | `string` | **yes** | — | file name only; no `/`, `\`, `..` (C1); must exist in the exchange directory | Source file. |
| `format` | named | `string` | no | `auto` | `auto`, `text`, `json` | `text` = the whole file is the content (source behaviour); `json` = a full five-field record; `auto` detects a `PROMPT EXPORT -format json` document and otherwise treats it as text. **NEW** — closes the source's lossy round trip (Q12). |
| `force` | flag | `bool` | no | `false` | — | Overwrite an existing prompt. **NEW** — the source imported over an existing prompt *silently* (B10); this package refuses without `-force`. |
| `description` | suffix | `string` | no | `Imported from: <basename>` | free text | Description. Source default preserved verbatim; note the GUI stored an empty string instead (Q6), which is not reproduced. |

`createdAt` = now, `lastUsedAt` = unset, for `-format text`. For `-format json` the record's own `createdAt`/`lastUsedAt`/`description` are honoured, which is what makes prompts shareable between machines.

**Pipeline behaviour.** **Both.** As a filter, one chunk = one **basename** to import; the prompt name is then derived from the file's stem (sanitized per C5) unless `name` was given on the command line, enabling `LOG LIST -dir exchange | PROMPT IMPORT` style bulk loads. Emits, per import, the source's four-line block verbatim:

```
Imported system prompt: <name>
Description: <description>
Length: <n> characters

Use 'PROMPT USE <name>' to start using it.
```

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_PROMPT_EXCHANGE_DIR`, `CHATDBG_PROMPT_MAX_CHARS`. Writes nothing.

**Failure modes.** Fewer than two operands and no pipe → `Please specify both a prompt name and a file name: PROMPT IMPORT <name> <basename> [description]` (source wording, path→basename). File absent → `File not found in the exchange directory: <basename>` (source said `File not found: <path>`; changed because the path is now implicit — `PROMPT PATH` shows it). Basename containing a separator or `..` → `Only a file name is accepted; run 'PROMPT PATH' to see the exchange directory.` File not valid UTF-8 → `<basename> is not readable as text.` `-format json` on a file that is not a prompt record → `<basename> is not a prompt document; import it with -format text to take it as raw content.` (The source, given a store `.json` file, cheerfully made the JSON itself the prompt text — Q12.) Existing name without `-force` → `Prompt already exists: <name>. Use -force to replace it.` Over the size ceiling → as `CREATE`. Any read/save failure → `Error importing prompt: <reason>` (source wording preserved).

**Security and audit.** No secret. **Path confinement is a security property here**, not a convenience: the exchange directory is the only place `IMPORT` will read from, so a prompt library command can never be turned into an arbitrary-file-read primitive (the source read any path the process could reach — dossier "Path handling / injection surface"). Overwrites only with `-force` → confirmation required in that case. Imported content is untrusted text that will later be sent to a model as a system instruction; the package does not attempt to sanitize it, but `DOCTOR` flags records whose content is suspiciously large or contains control characters.

**Traceability.** PRD 7.5; source `/prompt import <n> <file_path> [description]` (`Commands/PromptCommand.cs:328-378`) and GUI **Import…** (`UI/SystemPromptsDialog.cs:461-626`), whose non-browsing "Browse…" box (Q11) is replaced by `PROMPT PATH` plus a plain basename.

---

#### 4.3.11 `PROMPT EXPORT` — export a prompt to a file

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Export", "Export a prompt to a file in the exchange directory",
    Prototype = "PROMPT EXPORT <name> [-file <basename>] [-format text|json] [-force]")]
[CommandParameterOrdered("name", "Prompt to export", UsePipe = true)]
[CommandParameterNamed("file", "Destination file name (no directories); defaults to a derived name")]
[CommandParameterNamed("format", "Export shape", DefaultValue = "text",
    AllowedValues = new[] { "text", "json" })]
[CommandFlag("force", "Overwrite an existing destination file")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** (unless piped) | — | existing prompt | Prompt to export. |
| `file` | named | `string` | no | `chatdbg_prompt_<sanitized name>.txt` (text) / `chatdbg_prompt_<sanitized name>.json` (json) | file name only (C1) | Destination inside the exchange directory. Source's default file name preserved verbatim. |
| `format` | named | `string` | no | `text` | `text`, `json` | `text` writes only the prompt content, exactly as the source did; `json` writes the full five-field record so a round trip is lossless. **NEW** |
| `force` | flag | `bool` | no | `false` | — | Overwrite an existing file. **NEW** — the source overwrote silently (B9). |

**Destination resolution** — this is where the source was worst. The command path defaulted to the user's Documents folder, the GUI path to the home folder without sanitizing the name, and on Linux with no XDG user-dirs configuration the Documents lookup returned the **empty string**, silently writing into the process working directory and reporting a bare relative name (Q13, Q18). This package resolves **one** directory, in this order, and `PROMPT PATH` prints it: `CHATDBG_PROMPT_EXCHANGE_DIR` → the per-user documents directory when non-empty → the per-user home directory. **It never falls back to the working directory**, and it creates the directory if absent.

**Pipeline behaviour.** **Both.** As a filter, one chunk = one prompt name, so `PROMPT LIST -format names | PROMPT EXPORT -format json` backs up the whole library; the per-chunk `-file` default is derived per prompt, and giving an explicit `-file` in a multi-name pipeline is an error rather than a silent last-writer-wins. Output per export: `Exported system prompt to: <full path>` (source wording, now always with a real directory).

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_PROMPT_EXCHANGE_DIR`. Writes nothing.

**Failure modes.** Missing name and no pipe → `Please specify a prompt name: PROMPT EXPORT <name> [-file <basename>]`. Unknown name → `Prompt not found: <name>`. Destination exists without `-force` → `<basename> already exists in the exchange directory; use -force to overwrite.` Destination unwritable / disk full → `Error exporting prompt: <reason>` (source wording). Exchange directory cannot be created → names the directory and the reason; this is the one place the tool tells the user to set `CHATDBG_PROMPT_EXCHANGE_DIR`.

**Security and audit.** No secret. Confined write: a caller cannot direct the write outside the exchange directory (the source could write anywhere the process could reach). Overwrites only with `-force` → confirmation required in that case.

**Traceability.** PRD 7.5; source `/prompt export <n> [file_path]` (`Commands/PromptCommand.cs:286-326`) and GUI **Export…** (`UI/SystemPromptsDialog.cs:628-696`).

---

#### 4.3.12 `PROMPT RESET` — restore the built-in starter set  **NEW**

**Why it earns its place.** The source seeded its four built-ins **only when the store loaded zero prompts, checked at every launch** (R14). Consequences: deleting all prompts silently resurrected them at the next start; a store containing only unreadable files counted as empty and was overwritten (R15); and there was no way to get `code-reviewer` back after deleting it, short of emptying the whole library. Making seeding an explicit, idempotent tool removes all three surprises.

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Reset", "Restore the built-in starter prompts",
    Prototype = "PROMPT RESET [<name>] [-force] [-list]")]
[CommandParameterOrdered("name", "Restore only this built-in", IsRequired = false)]
[CommandFlag("force", "Overwrite built-ins that have been modified")]
[CommandFlag("list", "Show what would be restored, and change nothing")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | no | *(all four)* | `default`, `code-reviewer`, `algorithm-helper`, `security-expert` | Which built-in to restore. **NEW** |
| `force` | flag | `bool` | no | `false` | — | Overwrite a built-in whose content has been changed. **NEW** |
| `list` | flag | `bool` | no | `false` | — | Dry run. **NEW** |

**The built-in set, verbatim** (source `Services/SystemPromptService.cs:168-198`; written in this fixed order, though listings re-sort — R13):

| Name | Description | Content |
|---|---|---|
| `default` | `Default system prompt for general debugging assistance` | `You are ChatDBG, a helpful debugging assistant. Help users analyze code, debug issues, and understand programming concepts.` |
| `code-reviewer` | `System prompt for code review assistance` | `You are ChatDBG in code review mode. Analyze code for bugs, security issues, performance problems, and maintainability concerns. Provide specific, actionable feedback with examples of how to improve the code.` |
| `algorithm-helper` | `System prompt for algorithm assistance` | `You are ChatDBG in algorithm mode. Help users understand, design, and optimize algorithms. Provide step-by-step explanations, time and space complexity analysis, and pseudocode when helpful.` |
| `security-expert` | `System prompt for security-focused assistance` | `You are ChatDBG in security expert mode. Help users identify and fix security vulnerabilities in their code. Focus on common issues like injection attacks, authentication problems, authorization flaws, data exposure, and insecure dependencies.` |

Seeded records get `createdAt` = the UTC instant of seeding and `lastUsedAt` unset (R17).

**Automatic seeding is retained but narrowed.** The store still seeds on first use when the directory is **absent or contains no files at all** — that is what makes a fresh install useful (acceptance criteria 1 and 2). It no longer seeds when the directory contains files that merely failed to parse (fixing R15); that case now produces the `DOCTOR` advisory instead.

**Pipeline behaviour.** Source only; **overrides `Main`** to emit one chunk per restored (or would-be-restored) prompt. Declines piped input explanatorily.

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_PROMPT_SEED`. Writes nothing.

**Failure modes.** Unknown built-in name → `'<name>' is not a built-in prompt. The built-ins are: default, code-reviewer, algorithm-helper, security-expert.` Built-in exists and differs, without `-force` → per-prompt chunk `'<name>' exists and has been modified; use -force to restore the original.` (a **success** chunk, not a failure — it is a report, not an error). Store read-only → one failure chunk naming the path.

**Security and audit.** No secret. **`-force` is destructive** (it discards user edits to a built-in) → confirmation required, and the previous record is copied to `<store>/.backup/` first.

**Traceability.** **NEW** as a tool; the seeding behaviour it exposes descends from `Services/SystemPromptService.cs:157-204`. PRD 7.5.

---

#### 4.3.13 `PROMPT PATH` — report the library's locations  **NEW**

**Why it earns its place.** The source *tried* to print the store directory in `/prompt list` and printed a .NET type name instead, because the "get directory" accessor existed on the concrete store but not on the abstraction the command held (Q2). Users had no supported way to find their prompts. With paths no longer expressible as parameters (C1), knowing the two directories is a prerequisite for `IMPORT` and `EXPORT`, so it becomes a first-class tool.

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Path", "Show where the prompt library and exchange directory are",
    Prototype = "PROMPT PATH [-format text|json] [-which store|exchange|backup|all]")]
[CommandParameterNamed("format", "Output shape", DefaultValue = "text",
    AllowedValues = new[] { "text", "json" })]
[CommandParameterNamed("which", "Which location to report", DefaultValue = "all",
    AllowedValues = new[] { "store", "exchange", "backup", "all" })]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text`, `json` | Output shape. **NEW** |
| `which` | named | `string` | no | `all` | `store`, `exchange`, `backup`, `all` | Which location. `-which store` emits the bare path, for piping. **NEW** |

Reports, per location: the absolute path, whether it exists, whether it is writable, how it was resolved (environment override vs. platform default), and — for the store — the number of records and unreadable files.

**Pipeline behaviour.** Source only (one chunk for a single `-which`, several when `all`). Declines piped input.

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_PROMPT_EXCHANGE_DIR`. Writes nothing.

**Failure modes.** None that fail: a missing or unwritable directory is *reported*, not thrown. This is the tool a user runs when something else failed, so it must work when everything else does not.

**Security and audit.** Emits absolute filesystem paths containing the OS user name — worth knowing if audit output is shipped off-box, but not a secret. Non-destructive.

**Traceability.** **NEW**; corrects source `Commands/PromptCommand.cs:102` (Q2) and supersedes the missing sixth capability on `Services/ISystemPromptService.cs`. PRD 7.5, with a nod to 7.11.

---

#### 4.3.14 `PROMPT DOCTOR` — diagnose the library  **NEW**

**Why it earns its place.** The dossier records seven distinct silent-corruption modes: unparseable files skipped with a message that the GUI shell swallowed (R9, Q26), files whose JSON `name` disagrees with their file name and are therefore unfetchable (R8), lossy sanitization collisions (R5), stores made non-portable by per-OS forbidden-character sets (Q21), a settings file pointing at a prompt that no longer exists (R21), a `null` JSON document skipped with no message at all (R10), and truncated files left by a crash mid-write (Q16). Every one of them presents to the user as "my prompt disappeared". One tool that names them is worth more than any amount of defensive code elsewhere.

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Doctor", "Check the prompt library for problems",
    Prototype = "PROMPT DOCTOR [-format text|csv|json] [-fix]", Alias = "CHECK")]
[CommandParameterNamed("format", "Output shape", DefaultValue = "text",
    AllowedValues = new[] { "text", "csv", "json" })]
[CommandFlag("fix", "Apply the safe repairs (rename mismatched files, quarantine unreadable ones)")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text`, `csv`, `json` | Output shape; `csv` sets `ResultFormat.CSV` for a log sink. **NEW** |
| `fix` | flag | `bool` | no | `false` | — | Apply only the reversible repairs. **NEW** |

**Checks performed**, one chunk each: unreadable/unparseable files (with the parse error); `null` documents; file-name ↔ record-name mismatches; sanitization collisions; names that would be unreachable on another operating system (C5 portability warning); records with empty name or empty content; content over the size ceiling or containing control characters; a `CHATDBG_SYSTEM_PROMPT` that names no existing prompt; missing built-ins; stale lock files; backups available in `<store>/.backup/`; store and exchange directory writability.

**`-fix` is deliberately narrow**: it renames a mismatched file to match its record name (never the reverse — the record is authoritative), moves an unreadable file to `<store>/.quarantine/` so listing stops tripping over it, and removes lock files older than an hour. It **never** deletes a record, never edits content, and never changes the active selection.

**Pipeline behaviour.** Source only; **overrides `Main`** to emit one chunk per finding, terminating with a summary chunk. A clean library emits exactly one chunk: `No problems found in <n> prompts.` Declines piped input.

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_PROMPT_EXCHANGE_DIR`, `CHATDBG_SYSTEM_PROMPT`, `CHATDBG_PROMPT_MAX_CHARS`. Writes nothing.

**Failure modes.** Store directory unreadable → a single failure chunk naming the path and the reason (this is the one condition `DOCTOR` cannot diagnose around). `-fix` unable to write → each repair reports its own failure and the rest continue.

**Security and audit.** No secret. `-fix` moves files but destroys nothing → no confirmation required for the moves; it is nonetheless recorded in the audit event like any other command.

**Traceability.** **NEW**; addresses source rules R5, R8, R9, R10, R15, R21 and quirks Q16, Q21, Q26. PRD 7.5, feeding 7.11.

---

### 4.4 Environment variables

| Key | Read by | Written by | Default | Notes |
|---|---|---|---|---|
| `CHATDBG_PROMPT_DIR` | all | none | per-user local-app-data `/ChatDbg/system_prompts` | **NEW** — the source had a constructor override used only by tests (dossier B0), so there was no supported way to relocate a library. Host-set only (a path cannot survive the argument tokenizer, C1). |
| `CHATDBG_PROMPT_EXCHANGE_DIR` | `IMPORT`, `EXPORT`, `CREATE -file`, `EDIT -file`, `PATH`, `DOCTOR` | none | documents dir, else home dir; **never** the working directory | **NEW** — replaces the source's three inconsistent defaults (Q13, Q18). |
| `CHATDBG_SYSTEM_PROMPT` | `STATUS`, `LIST`, `SHOW`, `USE`, `EDIT`, `DELETE`, `RENAME`, `DOCTOR` | `USE`, `RENAME` (global; needs `modifiesEnvironment: true`) | `default` | The active prompt **name**. Same default as the source's `systemPromptName` setting (R18). Persisted by ChatDbg.Tools.Settings, not here. |
| `CHATDBG_SYSTEM_PROMPT_TEXT` | `STATUS`, providers | `USE`, `EDIT` (when editing the active prompt) | the built-in default text (R20) | **Runtime only — must never be persisted** (R19). |
| `CHATDBG_PROMPT_SEED` | store bootstrap, `RESET` | none | `true` | `false` disables automatic seeding of the built-ins entirely. **NEW** |
| `CHATDBG_PROMPT_MAX_CHARS` | `CREATE`, `EDIT`, `IMPORT`, `DOCTOR` | none | `1000000` | **NEW** — the source had no length bound anywhere, and the interactive editor could grow without limit (Q4). |
| `CHATDBG_PROMPT_ALLOW_EDITOR` | `EDIT` | none | `true` | **NEW** — set `false` in a restricted host to forbid process spawning. |
| `VISUAL`, `EDITOR` | `EDIT -editor` only | none | `notepad` (Windows), else `nano` then `vi` | **NEW**; standard POSIX convention. |

Every read uses `storeDefault: false` so that reading configuration never marks the environment changed (ref-command §7.1). No tool in this package reads any `CHATDBG_*` credential variable, and none reads the process environment directly — everything goes through `IEnvironmentContext`, which is what makes the package testable (§4.16).

---

### 4.5 Pipeline compositions

**1. Back up the whole library, losslessly.**

```
PROMPT LIST -format names | PROMPT EXPORT -format json
```

`LIST` emits one chunk per prompt name; `EXPORT` consumes each as its `name` operand (`UsePipe = true`) and writes `chatdbg_prompt_<name>.json` into the exchange directory, one confirmation line per prompt. Unlike the source's text export, the JSON form carries description, `createdAt` and `lastUsedAt`, so re-importing restores the library rather than four undated blanks (Q12).

**2. Prune the prompts nobody ever selected.**

```
PROMPT LIST -unused -format names | PROMPT DELETE -yes
```

The user sees one `Deleted system prompt: <name>` per removed prompt. The active prompt cannot be caught by this: it has a `lastUsedAt` (`USE` stamps it) so `-unused` excludes it, and even if it did not, `DELETE`'s active-prompt guard refuses without `-force`. Each deleted record is copied to `<store>/.backup/` first.

**3. Cross-package: budget a persona against the model's context window.**  *(into ChatDbg.Tools.TokenInspection, PRD 7.10)*

```
PROMPT SHOW security-expert -content-only | TOKEN COUNT -model gpt-4
```

`SHOW -content-only` emits the prompt text and nothing else; `TOKEN COUNT` reports how many tokens every request will spend before the user has typed a word. This is the composition the source could not express at all — its `/tokenize` took inline text, which the argument tokenizer would have mangled anyway.

**4. Cross-package: switch persona and persist the choice.**  *(into ChatDbg.Tools.Settings, PRD 7.2)*

```
PROMPT USE code-reviewer | SET SAVE
```

`USE` publishes `CHATDBG_SYSTEM_PROMPT` and `CHATDBG_SYSTEM_PROMPT_TEXT` to the global environment and emits `Now using system prompt: code-reviewer`; the last stage's `SET SAVE` writes `systemPromptName` into the settings document. Note the framework rule this depends on: in a pipeline only the **last** stage's `ModifiesEnvironment` promotes its bucket to globals (ref-command §7.3c), so `SET SAVE` must be the environment-modifying stage — which it is, being the port of the source's `SET`.

**5. Cross-package: record a library health check.**  *(into ChatDbg.Tools.DiagnosticLogging, PRD 7.11)*

```
PROMPT DOCTOR -format csv | LOG APPEND -channel prompts
```

One chunk per finding, tagged `ResultFormat.CSV`, appended as rows to the diagnostic log. The source wrote its "could not read this file" notices to raw stdout, where the full-screen shell rendered them invisible (R9); this composition is the replacement.

**6. Fork a persona and edit it in one flow.**

```
PROMPT COPY security-expert appsec-team | PROMPT EDIT -editor
```

`COPY` emits the new name; `EDIT` consumes it and opens `$EDITOR` on the content. In a host with `CHATDBG_PROMPT_ALLOW_EDITOR=false` the second stage fails with one clear message and the copy still exists — degradation without loss.

---

### 4.6 Design notes for the architect

**What state this package holds.** Exactly one thing: the prompt store on disk, plus a derived in-memory index (name → file path → record) that is invalidated on every mutation and on a directory-timestamp change. The index is the one place this package deviates from the source's "re-read everything, twice, every time" model (Q3): the source's *observable* property — that an external edit to a prompt file is picked up immediately — is preserved by stamping the index with the directory's last-write time and re-reading when it moves. Nothing else is cached.

**What this package must not hold.** It must not hold the settings document, the active-prompt *persistence*, an `HttpClient`, a model handle, a chat history, or a reference to the host shell. In particular it must not hold the active prompt as private mutable state: the active name lives in the environment where every package can read it, and the active text is derived. The temptation — a `CurrentPrompt` property on a service — is what produced the source's worst defect, where the GUI's command objects mutated an orphaned settings instance and every `/prompt use` silently overwrote the user's provider, model, temperature and endpoint with construction-time defaults (Q8, Q20).

**Sub-commands, not a monolith.** The source's `PromptCommand` was one 380-line class with a `switch` on `args[0]`, which is why its usage line was wrong for `import` (Q22) and why five subcommands had near-identical but subtly different name-missing messages. Here each verb is its own `AbstractCommand` under `[CommandRoot("PROMPT", …)]`, so the framework generates help, validates arity, enforces allow-lists and routes `PROMPT LIST --HELP` without a line of dispatch code. Two framework consequences must be designed for, not discovered: a root invoked bare throws `InvalidOperationException` listing its sub-commands, which is why bare `/prompt` becomes `PROMPT STATUS`; and `AbstractCommand.RootCommand` throws when `[CommandRoot]` is absent — harmless here because every class carries it, but a reason never to make a tool in this package "top-level just this once".

**One chunk versus many.** `AbstractCommand`'s non-piped path emits exactly one chunk. `LIST`, `RESET` and `DOCTOR` are inherently multi-record, so they override `Main` and yield per record; everything else stays on the template method. The rule for reviewers: if a tool's output is a list, it overrides `Main`, and if it does not, its output is a paragraph. `CREATE` and `EDIT` invert this for their piped path — they consume many chunks and emit one, using `OnStartPipe`/`OnEndPipe` to buffer and flush, returning empty successes per chunk so nothing is echoed mid-stream.

**Testability.** Three seams and no more: `IPromptStore` (fetch, list, save, delete, stamp-last-used, **and the store directory** — the sixth capability the source omitted from its abstraction, which is precisely why its list output printed a type name, Q2); `IClock` for `UtcNow`, so timestamp assertions are deterministic; and `IIoContext`/`IEnvironmentContext`, which the framework already supplies as parameters. `MemoryIoContext` drives every tool in a hermetic suite with no filesystem at all; a second suite runs the real store against a temporary directory. The source's test coverage here was six assertions that pinned neither the seeding count, nor any built-in's text, nor any ordering, nor any error string — this package's acceptance suite pins all four, including the culture-sensitive ordering case (`a, a_b, a-b, ab, B`) that a naive ordinal implementation gets wrong. Split the suites the way Cupcake splits its: hermetic tests at the repo root, no network in either (this package has none).

**Capability unavailable on the current backend.** This package has no backend, so "unavailable" here means an OS or host restriction, and the rule is uniform: **report the restriction and continue with everything else.** A read-only store leaves every read tool fully functional and gives each write tool one specific message naming the path. A host that did not grant `modifiesEnvironment` leaves `USE` able to select, stamp and record — it just says so instead of pretending. A host with editors disabled leaves `EDIT` with four other content sources. Absent `$EDITOR`, the platform fallback chain is tried before failing. The only unconditional hard failure is a store directory that cannot be created at all — and even that is now a tool's failure chunk rather than the source's uncaught constructor exception that prevented the whole application from starting.

**Cross-platform, explicitly.** The source inherited five behaviours from its host OS and got a different product on each (dossier "Platform coupling"). All five are pinned here: file-name sanitization uses one fixed set on every platform (C5); name matching is ordinal on every platform (C4); collation is a parameter, not an ambient (C6); the export directory never degrades to the working directory; and the store's default location follows each platform's own convention (`%LocalAppData%`, `$XDG_DATA_HOME`, `~/Library/Application Support`) while the *layout inside it* is identical. A prompt library copied from Linux to Windows must open, list and select identically — that is an acceptance criterion, not an aspiration.

**Where it degrades rather than fails.** Unreadable file → skipped, counted, reported by `DOCTOR`, listing still succeeds. Missing active prompt → falls back to the built-in default text with one advisory line, never blocks a chat turn. Empty store → seeded, or if seeding is disabled, an empty list and a hint. Missing exchange directory → created. Lock contention → one retry with backoff, then a message naming the lock holder's age. Store directory vanishes mid-session → the next command reports it; the package does not cache its way into pretending otherwise.

**Considered and deliberately excluded.** `PROMPT DIFF` (compare two prompts, or a prompt against an exchange file) is genuinely useful but belongs to a text-utility package, not here; if it is ever wanted, it should live beside `REGIF` as a general filter, not as a fourteenth prompt verb. Prompt *versioning* (a full history per prompt) was rejected in favour of the single-generation `.backup` copy: it would double the store's shape, and the exchange directory plus a real version-control system covers the serious case.

---

### 4.7 Security and audit summary

| Concern | Position |
|---|---|
| Secrets | **None** in this package. No parameter and no output carries a credential, and no tool reads a credential variable. Prompt content is user-authored clear text and is stored unencrypted, exactly as in the source. |
| Audit masking | `AuditEvent.Parameters` records every argument token, including an inline `-text` or a description. The framework's masking only rewrites `-name=value` tokens and the tokenizer strips `=` anyway, so parameter masking is effectively non-functional (ref-command §11.4). The mitigation is structural: the fidelity-preserving content channels are the pipe, `-file` and the interactive editor — **none of which passes through the parameter array**. Document inline `-text` as convenience-only. |
| Environment-change audit | `USE` writes `CHATDBG_SYSTEM_PROMPT_TEXT`, whose value is a whole prompt body, and `LogEnvironmentChange` records old and new values. Variable-name-based redaction *does* work on that path; a host that wants terse records adds that key to `RedactedParameterNames`. |
| Path safety | No tool accepts a path. Store and exchange directories are resolved by the package; `IMPORT`/`EXPORT`/`-file` accept a bare file name and reject separators and `..`. The store's own file names are always `<store>/<sanitized>.json`, which cannot escape the directory. This is stricter than the source, which read and wrote any path the process could reach. |
| Destructive actions requiring confirmation | `DELETE` (always, unless `-yes`); `EDIT` (overwrites content — one backup generation, no prompt); `CREATE -force`, `IMPORT -force`, `EXPORT -force`, `RENAME -force`, `COPY -force`, `RESET -force` (each overwrites something that exists — confirm unless `-yes`). Confirmation is `PromptForCommand` defaulting to **No**; in a pipeline, where prompting is meaningless, the corresponding `-yes`/`-force` must be explicit or the operation is refused. |
| Untrusted content | An imported prompt becomes a system instruction sent to a model. The package does not attempt to filter instruction-injection attempts — that is not a check it can make correctly — but `DOCTOR` reports oversized content and control characters, and `SHOW` always displays the full text before `USE` applies it. |
| Loader posture | No dynamic assemblies, no reflection-emit, no native code, no network. Ships as `packages/ChatDbg.Tools.SystemPromptLibrary/<version>/bin/` for `AddPackageDirectory` + `LoadCommands`, and is hash-allowlisted in the host's integrity store (`learningMode: false`). The only capability worth a policy decision is `EDIT -editor`'s process spawn, which is gated by `CHATDBG_PROMPT_ALLOW_EDITOR`. |
