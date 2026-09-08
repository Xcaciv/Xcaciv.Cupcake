## 1. ChatDbg.Tools.SessionConversation — Session & Conversation

### Purpose and boundary

This package owns **the conversational turn and the conversation record**. Everything that adds, removes, reorders, reads, saves, restores, names or switches the record of what was said lives here, plus the one operation that makes a record grow by talking to a model: sending a turn and streaming the reply back.

Concretely, this package owns:

- **The turn** — composing a user turn, dispatching it to whatever backend is currently active, streaming the reply as it arrives, and writing both sides into the record (`CHAT SEND`, `CHAT RETRY`).
- **The record as a directly editable artifact** — append, inject at a position, pop the last turn, clear, list (`CHAT APPEND`, `CHAT INJECT`, `CHAT POP`, `CHAT CLEAR`, `CHAT LIST`).
- **The record as a file** — export to disk, import from disk, in a byte-compatible format (`CHAT EXPORT`, `CHAT IMPORT`).
- **The session** — the identity, name, and persistence of a conversation, and switching between several of them (`CHAT SESSIONS`, `CHAT USE`, `CHAT NAME`).
- **The `IConversationStore` and `IConversationBackend` contracts** (declared in the contract-only assembly `ChatDbg.Tools.Abstractions`) — the two seams every tool in this package talks through.

This package explicitly does **NOT** own:

| Not owned | Owned by | Why the line is here |
|---|---|---|
| Which provider is active, its endpoint, region, model id, local-model file, context size, GPU layers, thread count, batch size; probing whether a backend is configured | **`ChatDbg.Tools.Providers`** (root `MODEL`) — PRD 7.6, 7.7, 7.8 | `CHAT SEND` consumes an `IConversationBackend`; it never constructs an Azure client, an AWS client, or a `LLamaWeights`. Keeping the SDKs and the native `llama.cpp` binaries out of this assembly is what lets the record tools load under a Strict security policy. |
| Reading/writing settings, validation ranges, the settings file | **`ChatDbg.Tools.Settings`** (root `CONFIG`) — PRD 7.2 | This package *reads* settings from the environment context; it never writes the settings document. The per-turn `-temperature`/`-maxtokens`/`-topk` overrides here are transient and never persisted. |
| API keys, the OS keystore, environment credential resolution, credential-source reporting | **`ChatDbg.Tools.Credentials`** (root `CRED`) — PRD 7.3 | No tool in this package accepts, prints, or stores a secret. |
| System prompt authoring, storage, seeding, selection | **`ChatDbg.Tools.Prompts`** (root `PROMPT`) — PRD 7.5 | `CHAT SEND -prompt <name>` *names* a prompt; resolving that name to text is the Prompts package's job, and the resolved system prompt is never stored in the record (source behaviour: providers prepend it separately). |
| Tokenization, log-probability math, attribution, alternatives, analysis export | **`ChatDbg.Tools.TokenAnalysis`** (root `TOKEN`) — PRD 7.9, 7.10 | This package *carries* per-token log-probability payloads on messages and round-trips them through export/import. It never interprets them. |
| Heatmaps, grids, tables, colour banding, the `№` table, terminal width maths | **`ChatDbg.Tools.Render`** (root `VIEW`) — PRD 7.12 | Tools here emit chunks; the IO context and the render package decide what a chunk looks like. |
| Native log capture and daily log files | **`ChatDbg.Tools.Diagnostics`** (root `LOG`) — PRD 7.11 | |
| The REPL, the prompt string, the exit commands, dispatch, `HELP` | **the host** (`ChatDbg.Shell.Core`, the Cupcake `Loop`) — PRD 7.1, 7.13, 7.14 | The source product's hand-rolled `Dictionary<string, ICommand>` and its `/`-prefix rule are gone; dispatch is `CommandController.Run`, help is generated from attributes. |

**One boundary worth stating twice:** the source product kept exactly one conversation record per process, in a field on the shell, never autosaved and discarded at exit (chat-history rule 40). This package inverts that: the record is owned by an `IConversationStore` behind the seam, the canonical copy is a session file, and no tool holds it in a static.

---

### Package manifest

| Property | Value |
|---|---|
| Assembly name | `ChatDbg.Tools.SessionConversation.dll` |
| Package key on disk | `SessionConversation` — laid out as `{packageRoot}/SessionConversation/bin/ChatDbg.Tools.SessionConversation.dll` (the crawler's `*/bin/*.dll` mask) |
| Root command | `CHAT` (`[CommandRoot("CHAT", "Conversation and session tools")]` on every class) |
| Contract assemblies targeted | `Xcaciv.Command.Interface` **3.3.4** and `Xcaciv.Command.Core` **3.3.4** — and nothing else from the framework. Never ship a private copy of `Xcaciv.Command.Interface`: the crawler catches `ReflectionTypeLoadException` and reports exactly that cause. |
| Other references | `ChatDbg.Tools.Abstractions` (contract-only: `IConversationStore`, `IConversationBackend`, `ConversationRecord`, `ConversationTurn`, `TokenLogProbability`). No SDKs, no `HttpClient`, no P/Invoke. |
| TFM | `net10.0`, `LangVersion 14`, `Nullable` + `ImplicitUsings` enabled — matching the framework's default. A `net8.0;net10.0` multi-target is available behind the framework's `UseNet08` switch. |
| Elevated trust required | **No.** No reflection emit, no dynamic assemblies, no unsafe blocks, no unmanaged code. Loads cleanly under `AssemblySecurityPolicy.Strict` with `DisallowDynamicAssemblies = true` and `EnforceBasePathRestriction = true`. |
| Network access | **None, directly.** `CHAT SEND` / `CHAT RETRY` reach the network only through an injected `IConversationBackend`. If no backend is supplied, they fail with a stated message; nothing else in the package is affected. |
| Filesystem access | **Yes, and this is the package's one real capability.** Three roots: (1) the session store, `<LocalApplicationData>/ChatDbg/sessions/` — `%LOCALAPPDATA%\ChatDbg\sessions` on Windows, `$XDG_DATA_HOME/ChatDbg/sessions` or `~/.local/share/ChatDbg/sessions` on Linux/macOS; (2) arbitrary user-named paths for `CHAT EXPORT` / `CHAT IMPORT`, optionally confined by `CHAT_EXPORT_ROOT` / `CHAT_IMPORT_ROOT`; (3) the user home directory, read only to expand `~`. |
| OS keystore | **Never.** Delegated to `ChatDbg.Tools.Credentials`. |
| Native libraries | **None.** |
| Environment-modifying registration | **Not required.** Every value this package persists is written under a key prefixed with its own root command name, so the host registers it as `controller.AddCommand("SessionConversation", new …())` with `modifiesEnvironment` left at its default `false`. |
| Safe to load in a restricted host? | **Yes.** Under a filesystem-restricted host, `CHAT EXPORT`/`CHAT IMPORT`/`CHAT SESSIONS`/`CHAT USE` degrade to a stated failure ("session store is unavailable in this host"); `CHAT SEND`, `APPEND`, `INJECT`, `POP`, `CLEAR`, `LIST` continue to work against an in-memory store. Under a network-restricted host, only `SEND` and `RETRY` degrade. No tool in this package escalates, and none needs a confirmation dialog it cannot render. |

**Attribute conventions used throughout this package** (they are load-bearing, and two of them are workarounds for real framework traps):

1. `[CommandRoot("CHAT", …)]` is on **every** class. It must be — `AbstractCommand.RootCommand` throws `InvalidOperationException` when the attribute is absent, and a host that enumerates roots for help would trip it.
2. **`AllowedValues` silently makes its first element the default when `DefaultValue` is empty, and explicitly setting `DefaultValue = ""` alongside an allow-list makes the parser throw.** Therefore every allow-listed parameter in this package puts its **source-preserving default first in the list** and also states `DefaultValue` explicitly, and every "no override" parameter carries a sentinel first value (`inherit`) rather than an empty default.
3. Free text and paths use `[CommandParameterSuffix]`, declared **last and only once per class**, because the suffix parameter re-joins all remaining tokens with single spaces. That reproduces the source's space-collapsing behaviour exactly (chat-history rule 41) — deliberately, because file-compatibility and muscle memory both depend on it. `AllowedValues` is **not** enforced on suffix parameters; no suffix parameter in this package declares one.
4. Numeric ranges (temperature 0.0–2.0, top-K 1–20, …) **cannot** be expressed as attributes. Each range is declared in the parameter's `ValueDescription` so it appears in generated help, and enforced inside the tool, returning the source's exact rejection text as a `Failure` chunk rather than throwing.
5. Every tool that must emit more than one chunk on the non-piped path (`SEND`, `LIST`, `POP -count > 1`, `SESSIONS`, `IMPORT -emit`, `RETRY`) **overrides `Main`**, because `AbstractCommand.Main` emits exactly one chunk when `HasPipedInput` is false. Those overrides reproduce the base class's piped contract faithfully: forward failed upstream chunks verbatim, skip empty ones, call `OnStartPipe`/`OnEndPipe` around the loop.
6. Every tool guards against `ProcessParameters` returning an **empty dictionary when invoked with zero arguments** — no defaults, no flags, no field injection. `CHAT POP`, `CHAT CLEAR`, `CHAT LIST`, `CHAT SESSIONS` and `CHAT NAME` are all commonly invoked bare, so each carries sane field initialisers *and* dictionary fallbacks.

---

### Tool catalog

---

#### 1.1 `CHAT SEND` — send a conversational turn and stream the reply

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Send", "Send a turn to the active model backend and stream the reply",
    Prototype = "CHAT SEND <message...> [-provider inherit|azure|bedrock|llama] [-model <id>] " +
                "[-temperature <0.0-2.0>] [-maxtokens <1-8192>] [-logprobs inherit|on|off] " +
                "[-topk <1-20>] [-prompt <name>] [-stream on|off] [-format text|json] " +
                "[-timeout <seconds>] [-norecord] [-rollback]",
    Version = "1.0.0")]
[CommandParameterNamed("provider", "Backend for this turn only (inherit = use the configured provider)",
    DefaultValue = "inherit", AllowedValues = new[]{ "inherit", "azure", "bedrock", "llama" })]
[CommandParameterNamed("model", "Model id for this turn only; empty = the configured model")]
[CommandParameterNamed("temperature", "Sampling temperature, 0.0-2.0", DataType = typeof(double), ShortAlias = "t")]
[CommandParameterNamed("maxtokens", "Maximum reply tokens, 1-8192", DataType = typeof(int))]
[CommandParameterNamed("logprobs", "Request per-token log probabilities",
    DefaultValue = "inherit", AllowedValues = new[]{ "inherit", "on", "off" })]
[CommandParameterNamed("topk", "Alternatives per token when log probabilities are on, 1-20", DataType = typeof(int))]
[CommandParameterNamed("prompt", "System prompt name for this turn only")]
[CommandParameterNamed("stream", "Emit the reply incrementally as it arrives",
    DefaultValue = "on", AllowedValues = new[]{ "on", "off" })]
[CommandParameterNamed("format", "Output shape",
    DefaultValue = "text", AllowedValues = new[]{ "text", "json" })]
[CommandParameterNamed("timeout", "Abandon the call after N seconds; 0 = wait forever",
    DataType = typeof(int), DefaultValue = "0")]
[CommandFlag("norecord", "Do not write this turn or its reply into the conversation record")]
[CommandFlag("rollback", "Remove the user turn from the record if the backend call fails")]
[CommandParameterSuffix("message", "The text to send", IsRequired = true, UsePipe = true)]
[CommandHelpRemarks("A message that begins with '-' is not captured by the suffix parameter. Pipe it instead: SAY \"-x\" | CHAT SEND")]
[CommandHelpRemarks("Streaming requires backend support. Backends that cannot stream emit one chunk at the end; the tool does not fail.")]
public sealed class SendCommand : AbstractCommand { /* overrides Main, HandleExecution, HandlePipedChunk */ }
```

| Registration | Value |
|---|---|
| Command name | `SEND` |
| Root command | `CHAT` |
| Description | Send a turn to the active model backend and stream the reply |
| Usage prototype | `CHAT SEND <message...> [-provider inherit\|azure\|bedrock\|llama] [-model <id>] [-temperature <0.0-2.0>] [-maxtokens <1-8192>] [-logprobs inherit\|on\|off] [-topk <1-20>] [-prompt <name>] [-stream on\|off] [-format text\|json] [-timeout <seconds>] [-norecord] [-rollback]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `message` | suffix | `string` | yes (unless piped) | — | any text; remaining tokens joined with single spaces | The text to send. `UsePipe = true`, so when piped it is supplied by the upstream chunk and is not demanded on the command line. |
| `provider` | named | `string` | no | `inherit` | `inherit`, `azure`, `bedrock`, `llama` | Per-turn backend override. `inherit` reads `CHATDBG_PROVIDER`. Source default is `azure`. |
| `model` | named | `string` | no | `""` | free text | Per-turn model override. Source default `gpt-4`; for `llama` this is a path to a `.gguf` file that must exist. |
| `temperature` | named | `double` | no | *(from `CHATDBG_TEMPERATURE`, source default `0.7`)* | **0.0 – 2.0 inclusive** | Rejection text preserved: `Temperature must be a number between 0 and 2`. |
| `maxtokens` | named | `int` | no | *(from `CHATDBG_MAX_TOKENS`, source default `1000`)* | **1 – 8192 inclusive** | Rejection text preserved: `MaxTokens must be a number between 1 and 8192`. |
| `logprobs` | named | `string` | no | `inherit` | `inherit`, `on`, `off` | `inherit` reads `CHATDBG_ENABLE_LOGPROBS` (source default `false`). |
| `topk` | named | `int` | no | *(from `CHATDBG_LOGPROBS_TOPK`, source default `5`)* | **1 – 20 inclusive** | Rejection text preserved: `LogProbabilitiesTopK must be a number between 1 and 20`. |
| `prompt` | named | `string` | no | `""` | free text | **NEW.** System prompt name for this turn only; empty uses `CHATDBG_SYSTEM_PROMPT_NAME` (source default `default`). Earns its place because comparing two system prompts on the same question is the product's core use case and the source forced a persistent `/prompt use` between them. |
| `stream` | named | `string` | no | `on` | `on`, `off` | **NEW.** The source had no streaming, no spinner, no elapsed display, and blocked the prompt until the provider answered. |
| `format` | named | `string` | no | `text` | `text`, `json` | **NEW.** `json` emits one object per turn (`role`, `content`, `elapsedMs`, `logProbabilities`) and sets `ResultFormat.JSON`. |
| `timeout` | named | `int` | no | `0` | `0` (unlimited) – `86400` | **NEW.** The source has no cancellation token anywhere; a wedged local model wedges the shell. |
| `norecord` | flag | `bool` | no | `false` | presence = true | **NEW.** Ask without polluting the context under test — the product's stated purpose. |
| `rollback` | flag | `bool` | no | `false` | presence = true | **NEW.** Source behaviour (preserved as the default) leaves an orphan user turn in the record when the call fails, and documents `/pop` as the remedy. |

**Pipeline behaviour — both.** Accepts piped input: **one chunk is one complete user turn**. Each surviving chunk is sent as an independent request against the *same* record and the same overrides, and produces the reply for that chunk; the record grows by two turns per chunk unless `-norecord`. This turns `CHAT SEND` into a batch prompt runner. Produces piped output: with `-stream on` and a streaming backend, one chunk per arriving segment plus a terminal chunk carrying elapsed time; with `-stream off`, exactly one chunk per turn. Declares `ResultFormat.General` for `-format text` and `ResultFormat.JSON` for `-format json`, because downstream token tools need to know whether they are parsing prose or a turn object. Because streaming needs many chunks on the non-piped path, this tool **overrides `Main`**.

**Environment interaction.** Reads (always with `storeDefault: false`, so that a mere read does not flip `HasChanged` and cause a write-back): `CHATDBG_PROVIDER`, `CHATDBG_MODEL_ID`, `CHATDBG_TEMPERATURE`, `CHATDBG_MAX_TOKENS`, `CHATDBG_ENABLE_LOGPROBS`, `CHATDBG_LOGPROBS_TOPK`, `CHATDBG_SYSTEM_PROMPT_NAME`, and `CHAT_SESSION` (this package's own bucket). Writes, into its own bucket only: `CHAT_LAST_ELAPSED_MS`, `CHAT_LAST_TURN_ROLE`, `CHAT_TURN_COUNT`. **Does not need environment-modifying permission** — every written key carries the `CHAT_` root prefix, so the host routes it into this package's private bucket without `ModifiesEnvironment = true`. Declares in `GetDefaultEnvironment()`: `{ "SESSION", "default" }`, `{ "TURN_COUNT", "0" }` — the host seeds these as `CHAT_SESSION` / `CHAT_TURN_COUNT`.

**Failure modes.**

| Condition | User sees |
|---|---|
| No message and no pipe | `ArgumentException("Missing required parameter message")` is raised inside parameter processing and reduced by the executor to `Error executing CHAT (see trace for more info)`; the specific text reaches the trace only. Mitigation: `ValueDescription` and `Prototype` both spell the requirement, and `CommandHelpRemarks` names the pipe alternative. |
| Message begins with `-` | The suffix parameter takes its default (empty), so the turn is refused with `CHAT SEND received no message. A message beginning with '-' must be piped: SAY "…" \| CHAT SEND`. |
| Dangling named parameter (`… -temperature` at end of line) | Framework raises `ArgumentOutOfRangeException` during parsing; surfaces as the generic executor failure chunk. Documented in help remarks. |
| `-temperature` / `-maxtokens` / `-topk` out of range | `Failure` chunk carrying the source's exact rejection string. Nothing is sent; nothing is recorded. |
| `-provider` names a backend the host has no adapter for | `Failure`: `Unknown AI provider: {name}` — source text preserved. |
| Backend present but not configured | `Failure`: `{Provider} service is not configured. Use environment variables or the OS credential store to configure credentials securely.` followed by `Type 'CRED STATUS' to see current configuration and setup instructions.` — the source's two-line advisory, retargeted at the Credentials package. |
| **No backend at all** (package crawled into a restricted host with no `IConversationBackend` registered) | `Failure`: `No conversation backend is available in this host. Record tools (CHAT LIST/INJECT/POP/CLEAR/IMPORT/EXPORT) still work.` This is the package's principal degrade-don't-fail path. |
| Backend returns an envelope with no text | The literal `Error: Response text expected, none received.` is recorded as the assistant turn **and** the envelope's own error message is emitted as a second failure chunk. *(Deliberate improvement: the source spelled this placeholder four different ways across four code paths, and the console shell silently dropped the envelope's error message and elapsed time.)* |
| Log probabilities requested but none returned | Two-line note, source text preserved: `Note: Log probabilities were requested but none were returned by the model.` / `This could be due to the model not supporting this feature or an API limitation.` The turn is still recorded. |
| Backend throws | `Failure(message, exception)`. The user turn remains in the record unless `-rollback` was given. Nothing is written to `Console` — the source wrote diagnostics straight to stdout, which painted over the full-screen shell. |
| `-timeout` elapses | `Failure`: `CHAT SEND timed out after {n}s`. The user turn remains (or is removed under `-rollback`). |
| Downstream error arriving through the pipe | A failed upstream chunk is forwarded verbatim and **no request is made for it** — reproducing `AbstractCommand.Main`'s contract inside the `Main` override. Empty upstream chunks are skipped. A failure originating downstream cannot reach this stage at all; the pipeline is one-directional. |
| Stage timeout configured on the host's `PipelineConfiguration` | The stage is cancelled, a *status message* (not an output chunk) reads `Stage 'CHAT' exceeded timeout of {n} seconds`. Any in-flight turn is not recorded. |

**Security and audit.** No parameter and no output carries a secret **by construction** — there is no credential parameter, and the backend resolves credentials itself. But the `message` suffix and the reply routinely contain the user's source code and stack traces, and the framework emits exactly one `AuditEvent` per execution carrying `Parameters = ioContext.Parameters` verbatim. **The shipped `AuditMaskingConfiguration.ApplyMasking` only rewrites `-name=value` tokens and is effectively non-functional for this framework's space-separated syntax**, so the host MUST supply its own `IAuditMaskingConfiguration` that redacts the whole tail of a `CHAT SEND` invocation, or configure the audit logger to record parameter *count* rather than parameter *values* for this root. This is a host-level obligation stated here because this tool is the reason it exists. Not destructive and not irreversible in the local sense — but it spends money and leaves a record on a third-party service, so `-norecord` is deliberately about the local record only and says so in its help. No confirmation prompt: a chat turn is the shell's ordinary case.

**Traceability.** PRD **7.6 AI Provider Abstraction & Hosted OpenAI** (primary), with **7.4 Chat History**, **7.9 Token Probability Analysis** (`-logprobs`, `-topk`), **7.5 System Prompt Management** (`-prompt`), **7.13 Line-Oriented Shell**. Source ancestor: the console shell's chat-turn path — any typed line not beginning with `/`, handled by `ChatShell.SendMessageAsync` (`src/ChatDbg/ChatShell.cs:343-410`) and its GUI twin (`ChatWindow.cs:420-497`).

---

#### 1.2 `CHAT APPEND` — append a fabricated turn to the end of the record

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Append", "Append a message to the end of the conversation record",
    Prototype = "CHAT APPEND <role> <content...> [-command] [-quiet]")]
[CommandParameterOrdered("role", "Message role",
    DefaultValue = "user", AllowedValues = new[]{ "user", "assistant", "system" })]
[CommandFlag("command", "Mark the message as a command so backends exclude it from every request")]
[CommandFlag("quiet", "Emit nothing on success")]
[CommandParameterSuffix("content", "Message text", IsRequired = false, UsePipe = true)]
public sealed class AppendCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `APPEND` |
| Root command | `CHAT` |
| Description | Append a message to the end of the conversation record |
| Usage prototype | `CHAT APPEND <role> <content...> [-command] [-quiet]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `role` | ordered | `string` | **yes** (ordered params default `IsRequired = true`) | `user` | `user`, `assistant`, `system` | Lower-cased before validation and before storage, so `User`, `USER`, `user` all store as `user`. Rejection text preserved: `Role must be one of: user, assistant, system`. |
| `content` | suffix | `string` | no | `""` | any text | An empty message is accepted — the source accepted one too (only reachable there through the GUI dialog). |
| `command` | flag | `bool` | no | `false` | presence = true | **NEW.** Sets the record's `isCommand` flag. The flag exists in the file format and every hosted backend honours it by excluding the message from requests, but **nothing in the source ever set it** — it was a dormant hook reachable only by hand-editing an exported file. Earns its place: a note-to-self turn the model never sees is genuinely useful when debugging a prompt. |
| `quiet` | flag | `bool` | no | `false` | presence = true | **NEW.** Keeps batch appends from flooding the transcript. |

**Pipeline behaviour — both.** Accepts piped input: **one chunk is the content of one message**, appended with the `role` given on the command line (`content` carries `UsePipe = true`, so it is not demanded when piped). Produces one confirmation chunk per append, or nothing under `-quiet` — an empty success chunk is dropped by the host, which is the framework's idiomatic way to stay silent. `ResultFormat.General`.

**Environment interaction.** Reads `CHAT_SESSION` (`storeDefault: false`). Writes `CHAT_TURN_COUNT` into its own bucket. No environment-modifying permission needed.

**Failure modes.** Role outside the whitelist → `Failure` with the preserved text. Zero arguments → the framework's early return produces an empty parameter dictionary; the tool detects the missing ordered parameter itself and returns `Failure`: `Usage: CHAT APPEND <role> <content...>`. Store unavailable → `Failure`: `Conversation record is unavailable in this host.` Failed upstream chunks forward verbatim; empty ones are skipped by `AbstractCommand.Main` before `HandlePipedChunk` is reached.

**Security and audit.** No secrets. The `content` tail has the same audit-masking obligation as `CHAT SEND`. Not destructive — append is additive and the record is not truncated.

**Traceability.** PRD **7.4 Chat History**. Source ancestor: `ChatHistory.AddMessage(role, content, isCommand, logProbabilities)` (`Models/ChatHistory.cs:14-24`) — a real product operation that had no user-facing command; the shells called it directly. Surfacing it as a tool is what makes the record composable in a pipeline.

---

#### 1.3 `CHAT INJECT` — insert a fabricated turn at a position

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Inject", "Insert a message into the conversation record at a position",
    Prototype = "CHAT INJECT <role> <message...> [-position <n>] [-strict] [-quiet]")]
[CommandParameterOrdered("role", "Message role",
    DefaultValue = "user", AllowedValues = new[]{ "user", "assistant", "system" })]
[CommandParameterNamed("position", "Zero-based insertion index; -1 appends",
    DataType = typeof(int), DefaultValue = "-1", ShortAlias = "p")]
[CommandFlag("strict", "Fail instead of appending when the position is out of range")]
[CommandFlag("quiet", "Emit nothing on success")]
[CommandParameterSuffix("message", "Message text", IsRequired = false, UsePipe = true)]
[CommandHelpRemarks("Piped injections keep their upstream order: chunk n is inserted at position + n.")]
public sealed class InjectCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `INJECT` |
| Root command | `CHAT` |
| Description | Insert a message into the conversation record at a position |
| Usage prototype | `CHAT INJECT <role> <message...> [-position <n>] [-strict] [-quiet]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `role` | ordered | `string` | yes | `user` | `user`, `assistant`, `system` | Lower-cased first. Rejection text preserved: `Role must be one of: user, assistant, system`. |
| `message` | suffix | `string` | no | `""` | any text | Empty accepted (source accepted it). |
| `position` | named | `int` | no | `-1` | `-1` (append) or **`0 ≤ position < current message count`**; anything else appends | **Changed shape, same semantics.** The source read the position from the *last positional token*, and only when there were strictly more than two arguments — so `/inject user 42` injected the text `42` while `/inject user 42 7` injected `42` at 7, and an unparseable trailing token silently became part of the message. That is a genuine trap and is **deliberately not reproduced**; the value range and the out-of-range-appends rule are preserved exactly. |
| `strict` | flag | `bool` | no | `false` | presence = true | **NEW.** With `-strict`, an out-of-range position is a `Failure` instead of a silent append. Earns its place: the source reported `at position 99` after appending at index 2 — it told the user something that did not happen. |
| `quiet` | flag | `bool` | no | `false` | presence = true | **NEW.** |

**Success text.** `Injected {role} message at position {actualIndex}: {message}` — with ` at position …` omitted entirely when no position was requested, exactly as the source did. *(Deliberate correction: the index reported is the index actually used. When it differs from the requested one the chunk reads `Injected {role} message at position {actual} (requested {requested}, appended at end): {message}`.)*

**Pipeline behaviour — both.** Accepts piped input: **one chunk is the text of one message**. `OnStartPipe` resets an ordinal; chunk *n* is inserted at `position + n`, so a piped sequence lands in upstream order rather than reversed. With `position = -1` every chunk appends, which is the same thing. Produces one confirmation chunk per injection. `ResultFormat.General`.

**Environment interaction.** Reads `CHAT_SESSION`. Writes `CHAT_TURN_COUNT`. No environment-modifying permission needed.

**Failure modes.** Fewer than the required arguments → `Failure`: `Usage: CHAT INJECT <role> <message...> [-position <n>]` (source text, retargeted). Bad role → preserved rejection text; record untouched. `-position` given a non-integer → the framework marks the parameter invalid and the tool falls back to `-1` (append) with a warning chunk, rather than folding the token into the message. `-strict` with an out-of-range position → `Failure`: `Position {n} is outside the valid range 0..{count-1}`. Store unavailable → stated failure. Failed upstream chunks forward verbatim.

**Security and audit.** No secrets. Injection **fabricates provenance**: an injected `assistant` turn is indistinguishable from a real one in the record and is sent to the backend as context on the next turn. That is the whole point of the feature, but it means an exported record is not evidence of what a model said. The audit event records the role and the fact of injection; the host's masking configuration should treat the message tail as it treats `CHAT SEND`. Not destructive — nothing is removed.

**Traceability.** PRD **7.4 Chat History**. Source ancestor: `/inject <role> <message> [position]` (`Commands/InjectCommand.cs`, `Models/ChatHistory.cs:34-51`) and the GUI's 70×15 "Inject Message" dialog.

---

#### 1.4 `CHAT POP` — remove the last turn (or the last N)

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Pop", "Remove the last message from the conversation record",
    Prototype = "CHAT POP [-count <n>] [-preview <chars>] [-format text|json] [-yes] [-quiet]")]
[CommandParameterNamed("count", "How many messages to remove from the end, 1-1000",
    DataType = typeof(int), DefaultValue = "1", ShortAlias = "n")]
[CommandParameterNamed("preview", "Characters of removed content to echo back, 0-500",
    DataType = typeof(int), DefaultValue = "50")]
[CommandParameterNamed("format", "Output shape",
    DefaultValue = "text", AllowedValues = new[]{ "text", "json" })]
[CommandFlag("yes", "Skip the confirmation prompt", ShortAlias = "y")]
[CommandFlag("quiet", "Emit nothing on success")]
[CommandHelpRemarks("Removed messages are emitted as output chunks, so a pop can be piped to CHAT EXPORT before it is lost.")]
public sealed class PopCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `POP` |
| Root command | `CHAT` |
| Description | Remove the last message from the conversation record |
| Usage prototype | `CHAT POP [-count <n>] [-preview <chars>] [-format text\|json] [-yes] [-quiet]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `count` | named | `int` | no | `1` | **1 – 1000** | **NEW.** The source ignored all arguments, so `/pop 3` removed exactly one message. Default 1 preserves that. |
| `preview` | named | `int` | no | **`50`** | 0 – 500 | The source's magic number: 50 characters of the removed content echoed in the confirmation. Preserved as the default. |
| `format` | named | `string` | no | `text` | `text`, `json` | **NEW.** `json` emits the full removed message (role, content, timestamp, isCommand, logProbabilities) as one object per pop, so a pop is recoverable. Sets `ResultFormat.JSON`. |
| `yes` | flag | `bool` | no | `false` | presence = true | **NEW.** Required when `count > 1` in a non-interactive or piped context; see below. |
| `quiet` | flag | `bool` | no | `false` | presence = true | **NEW.** |

**Success text.** `Removed last message: [{role}] {preview}` — with the literal `...` appended **only when truncation actually occurred**, and the cut taken on **text-element (grapheme) boundaries**. *(Two deliberate corrections: the source appended `...` unconditionally, so a two-character message popped as `Removed last message: [user] hi...` and an empty one as `Removed last message: [user] ...`; and it sliced at 50 UTF-16 code units, so a surrogate pair or combining sequence straddling offset 50 was cut in half.)* With `count > 1` the chunks are emitted newest-first and a final chunk reads `Removed {n} messages from the conversation record`.

**Pipeline behaviour — output only.** Does **not** accept piped input; `HandlePipedChunk` returns the explanatory string `Unsupported pop method for {chunk} (piped)` rather than throwing, following the framework's own convention for pipe-incapable tools. Produces piped output: one chunk per removed message, so `CHAT POP -count 3 -format json | CHAT EXPORT ~/discarded.json` saves what is about to be lost. Because `count > 1` needs many chunks on the non-piped path, this tool **overrides `Main`**.

**Environment interaction.** Reads `CHAT_SESSION`. Writes `CHAT_TURN_COUNT`. No environment-modifying permission needed.

**Failure modes.** Empty record → `Failure`: `Chat history is empty` (source text preserved). `count` out of range → `Failure`: `Count must be a number between 1 and 1000`. `count` greater than the record length → removes everything present and reports the actual number removed, no error. Store unavailable → stated failure. Downstream errors cannot reach this stage.

**Security and audit.** No secrets, but the emitted chunks contain the removed conversation content, so `-format json` output inherits the same masking obligation. **Destructive and irreversible in the record** — this is the tool's whole job. Confirmation policy: with `count == 1` no confirmation (matching the source, where `/pop` was the documented one-keystroke remedy for a failed turn); with `count > 1` the tool calls `IIoContext.PromptForCommand("Remove {n} messages? (y/N) ")` and accepts `y`/`yes` case-insensitively, matching every other confirmation in the source product. **When `HasPipedInput` is true a prompt is meaningless** — the contract says `PromptForCommand` is only defined for a non-piped context — so in a pipeline `count > 1` without `-yes` is refused with `Refusing to remove {n} messages inside a pipeline without -yes`.

**Traceability.** PRD **7.4 Chat History**. Source ancestor: `/pop` (`Commands/PopCommand.cs`, `Models/ChatHistory.cs:26-32`) and GUI *File ▸ Pop Last Message*.

---

#### 1.5 `CHAT CLEAR` — empty the record

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Clear", "Clear the conversation record",
    Prototype = "CHAT CLEAR [-keep none|system] [-newsession] [-backup <path...>] [-yes]")]
[CommandParameterNamed("keep", "Roles to preserve while clearing",
    DefaultValue = "none", AllowedValues = new[]{ "none", "system" })]
[CommandFlag("newsession", "Also mint a new session identifier and creation timestamp")]
[CommandFlag("yes", "Skip the confirmation prompt", ShortAlias = "y")]
[CommandParameterSuffix("backup", "Write the record to this path before clearing", IsRequired = false)]
public sealed class ClearCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `CLEAR` |
| Root command | `CHAT` |
| Description | Clear the conversation record |
| Usage prototype | `CHAT CLEAR [-keep none\|system] [-newsession] [-backup <path...>] [-yes]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `keep` | named | `string` | no | `none` | `none`, `system` | **NEW.** `system` preserves messages whose lower-cased role is `system`, in order. Earns its place: the common debugging loop is "reset the conversation but keep the seeded system turn", which the source could only do by re-injecting by hand. |
| `newsession` | flag | `bool` | no | `false` | presence = true | **NEW.** Source behaviour, preserved as the default: clearing resets **only** the message list — the session identifier and the creation timestamp survive. |
| `backup` | suffix | `string` | no | `""` | a file path; `~` expanded | **NEW.** Writes the record through the same code path as `CHAT EXPORT` before emptying it. Earns its place: clear is the one operation with no undo and no prior write to disk. |
| `yes` | flag | `bool` | no | `false` | presence = true | **NEW.** |

**Success text.** `Cleared {n} messages from chat history` — with `n` captured **before** the clear, and `Cleared 0 messages from chat history` returned as a **success** on an empty record, exactly as the source did. With `-keep system` the chunk reads `Cleared {n} messages from chat history ({k} system messages kept)`.

**Pipeline behaviour — neither.** No piped input (`HandlePipedChunk` returns `Unsupported clear method for {chunk} (piped)`), one output chunk. `ResultFormat.General`. A destructive whole-record operation has no sensible per-chunk meaning.

**Environment interaction.** Reads `CHAT_SESSION`. Writes `CHAT_TURN_COUNT` (to `0`) and, under `-newsession`, `CHAT_SESSION_ID`. No environment-modifying permission needed.

**Failure modes.** Empty record → **success**, not an error (preserved). `-backup` given but unwritable → `Failure` carrying the underlying reason **and the record is not cleared**; backup failure is fail-closed. Store unavailable → stated failure.

**Security and audit.** No secrets. **Destructive and irreversible; requires confirmation.** The tool prompts `Clear {n} messages? (y/N) ` unless `-yes` or `-backup` was given, and refuses outright inside a pipeline without `-yes`. This is a deliberate departure: the source had *no* confirmation anywhere, and the GUI placed *Clear History* directly beneath *Pop Last Message* in the same menu, one keystroke apart.

**Traceability.** PRD **7.4 Chat History**. Source ancestor: `/clear` (`Commands/ClearCommand.cs`, `Models/ChatHistory.cs:53-56`) and GUI *File ▸ Clear History*.

---

#### 1.6 `CHAT LIST` — emit the record as chunks *(NEW)*

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("List", "Emit the conversation record, one message per chunk",
    Prototype = "CHAT LIST [-role any|user|assistant|system] [-from <n>] [-count <n>] " +
                "[-format text|json|csv] [-index] [-logprobs] [-last]")]
[CommandParameterNamed("role", "Only emit messages with this role",
    DefaultValue = "any", AllowedValues = new[]{ "any", "user", "assistant", "system" })]
[CommandParameterNamed("from", "Zero-based index of the first message to emit",
    DataType = typeof(int), DefaultValue = "0")]
[CommandParameterNamed("count", "How many messages to emit; 0 = all remaining",
    DataType = typeof(int), DefaultValue = "0")]
[CommandParameterNamed("format", "Output shape",
    DefaultValue = "text", AllowedValues = new[]{ "text", "json", "csv" })]
[CommandFlag("index", "Prefix each chunk with its zero-based index")]
[CommandFlag("logprobs", "Include per-token log probabilities (json only)")]
[CommandFlag("last", "Emit only the final matching message")]
public sealed class ListCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `LIST` |
| Root command | `CHAT` |
| Description | Emit the conversation record, one message per chunk |
| Usage prototype | `CHAT LIST [-role any\|user\|assistant\|system] [-from <n>] [-count <n>] [-format text\|json\|csv] [-index] [-logprobs] [-last]` |

**Why it is NEW and why it earns its place.** The source product had **no way at all** to see the conversation record in the plain-console shell — the transcript scrolled past and was gone; only the full-screen shell re-rendered it, and only for human eyes. A pipeline needs a *source*, and every downstream capability in the rebuild (token analysis, filtered export, re-running a prompt set against a different model, feeding `REGIF`) needs turns as discrete chunks. `CHAT LIST` is the single tool that makes this package composable.

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `role` | named | `string` | no | `any` | `any`, `user`, `assistant`, `system` | Compared **case-insensitively** against the stored role. *(Deliberate correction: the source's GUI compared the stored role to the literal `assistant` case-sensitively in six places, so an imported message stored as `Assistant` was invisible to the token panel while still rendering normally.)* |
| `from` | named | `int` | no | `0` | 0 – record length | Out of range yields no chunks and a success. |
| `count` | named | `int` | no | `0` | 0 (all remaining) – 100000 | |
| `format` | named | `string` | no | `text` | `text`, `json`, `csv` | `text` → `[{role}] {content}`; `json` → one message object per chunk with the exported key names; `csv` → `index,role,timestamp,isCommand,content`. Sets `ResultFormat.General` / `.JSON` / `.CSV` respectively, so downstream tools can branch on the format rather than sniffing. |
| `index` | flag | `bool` | no | `false` | presence = true | Zero-based, matching `-position` on `CHAT INJECT`. Note the source's *rendered* indices in the probability views were 1-based and absolute; those belong to the Render package, not here. |
| `logprobs` | flag | `bool` | no | `false` | presence = true | Ignored for `text` and `csv`. |
| `last` | flag | `bool` | no | `false` | presence = true | Emits the final message matching `-role`. `CHAT LIST -role assistant -last -format json` is the canonical feed for the token tools. |

**Pipeline behaviour — output only.** Refuses piped input with `Unsupported list method for {chunk} (piped)`. Produces one chunk per message — a true pipeline source. **Overrides `Main`**, because the non-piped path of `AbstractCommand` emits exactly one chunk. Declares a non-`General` output format for `json` and `csv` because those shapes are contracts other packages parse.

**Environment interaction.** Reads `CHAT_SESSION` only. Writes nothing. No environment-modifying permission needed.

**Failure modes.** Empty record → success with **zero chunks** (the host drops empty successes), not an error. `from`/`count` non-numeric → parameter marked invalid, tool falls back to the declared default and emits a warning chunk. Store unavailable → stated failure. Zero arguments is the common case and is explicitly handled: the empty parameter dictionary maps to "all messages, text format".

**Security and audit.** No secrets in parameters. **Output is the whole conversation**, so a `CHAT LIST` in a shell whose output is being logged copies the transcript into that log. This is the tool the host's audit configuration should be least worried about at the *parameter* level and most careful about at the *sink* level. Not destructive; read-only.

**Traceability.** **NEW** — no direct ancestor. Nearest source behaviour: the GUI shell's `RefreshChatHistory` rendering loop (`ChatWindow.cs:500-620`), which produced the same information for the screen only. PRD **7.4 Chat History**, with **7.12 Output Rendering** for `-format`.

---

#### 1.7 `CHAT EXPORT` — write the record to a file

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Export", "Export the conversation record to a file",
    Prototype = "CHAT EXPORT <file_path...> [-format json|jsonl|md|text] [-noclobber] [-quiet]")]
[CommandParameterNamed("format", "File format",
    DefaultValue = "json", AllowedValues = new[]{ "json", "jsonl", "md", "text" })]
[CommandFlag("noclobber", "Fail instead of overwriting an existing file")]
[CommandFlag("quiet", "Emit nothing on success")]
[CommandParameterSuffix("path", "Destination path; a leading ~ is expanded", IsRequired = true, UsePipe = false)]
[CommandHelpRemarks("Runs of consecutive spaces in a path collapse to one - quote-stripping happens before argument tokenization.")]
[CommandHelpRemarks("Set CHAT_EXPORT_ROOT to confine exports to one directory tree.")]
public sealed class ExportCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `EXPORT` |
| Root command | `CHAT` |
| Description | Export the conversation record to a file |
| Usage prototype | `CHAT EXPORT <file_path...> [-format json\|jsonl\|md\|text] [-noclobber] [-quiet]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `path` | suffix | `string` | **yes** | — | any path; leading `~/`, `~\` or bare `~` expanded to the OS home directory | Remaining tokens joined with single spaces — the source's behaviour, preserved, quirk included. |
| `format` | named | `string` | no | `json` | `json`, `jsonl`, `md`, `text` | `json` is **byte-compatible with the source** (see below). `jsonl`/`md`/`text` are **NEW** and earn their place because a Markdown transcript is what people actually paste into an issue, and JSON Lines streams. |
| `noclobber` | flag | `bool` | no | `false` | presence = true | **NEW.** Source overwrote silently with no warning; that stays the default so re-exporting to the same path keeps working. |
| `quiet` | flag | `bool` | no | `false` | presence = true | **NEW.** |

**Path handling — preserved exactly unless marked.**
- Leading `~/` expands to the OS home (`%USERPROFILE%` on Windows, `$HOME` on Unix). *(Deliberate improvement: `~\` and a bare `~` now expand too. The source expanded only the literal two characters `~/`, so `~\chats\a.json` on Windows was written to a directory literally named `~`.)*
- **The default extension is appended only when the resolved file name has no extension at all** — so `notes.txt` stays `notes.txt` and is written as JSON into a `.txt` file, and `.history` counts as already having an extension and gets no suffix. Preserved. The appended extension now follows `-format`: `.json`, `.jsonl`, `.md`, `.txt`.
- Missing parent directories are created. Preserved.
- **Writes are atomic**: temp file in the destination directory, `fsync`, then rename over the target. *(Deliberate improvement: the source overwrote in place with a whole-file write, so an interrupted export left a truncated, unparseable file where a valid history used to be.)*
- **Optional containment**: when the environment value `CHAT_EXPORT_ROOT` is non-empty, the fully-resolved destination must be inside it — checked component-wise with a relative-path test and after resolving symlinks, never with a string `StartsWith`. Outside → `Failure`. Unset (the default) reproduces the source's total absence of path restrictions.

**`-format json` file format — must match the source byte for byte.** Top-level keys in declaration order `messages`, `sessionId`, `createdAt`; per message `role`, `content`, `timestamp`, `isCommand`, `logProbabilities`; per token entry `token`, `logprob`, `top_alternatives` (note the snake_case outliers — they are load-bearing). Pretty-printed with **2-space** indentation. Timestamps ISO-8601 UTC round-trip with exactly **seven** fractional digits and a `Z` suffix. `null` written literally for absent lists; keys never omitted. Escaping is the **aggressive** profile: apostrophe becomes `\u0027`, backtick becomes `\u0060`, plus becomes `\u002B`, `<`/`>`/`&` are escaped, and all non-ASCII is escaped as `\uXXXX`. Numbers in shortest round-trip form (`-5`, not `-5.0`). UTF-8, no BOM. There is no schema version field and no format negotiation.

**Pipeline behaviour — both.** Accepts piped input: **one chunk is one message record in the `-format` shape** (the shape `CHAT LIST -format json` emits). When piped, the tool writes *those* messages instead of the live record, using `OnStartPipe` to open the temp file and `OnEndPipe` to close and rename it — so `CHAT LIST -format json | REGIF … | CHAT EXPORT ~/subset.json` exports a filtered subset. A chunk that does not parse in the declared format is reported as a failure chunk and skipped; the file is still written from the chunks that did parse, and the summary names the skip count. Produces one summary chunk. `ResultFormat.General`.

**Environment interaction.** Reads `CHAT_SESSION`, `CHAT_EXPORT_ROOT` (both `storeDefault: false`). Writes `CHAT_LAST_EXPORT_PATH` into its own bucket. Declares `{ "EXPORT_ROOT", "" }` in `GetDefaultEnvironment()`. No environment-modifying permission needed.

**Failure modes.**

| Condition | User sees |
|---|---|
| No path | `Failure`: `Usage: CHAT EXPORT <file_path>` (source text, retargeted). |
| Permission denied, invalid path characters, path too long, disk full, directory creation refused | `Failure`: `Failed to export chat history to: {resolved path}` **followed by the underlying reason**, with the exception attached to the result. *(Deliberate correction: the source swallowed the exception, printed the reason to stdout — which painted over the full-screen shell — and handed the user a cause-free message, so a full disk and a permission denial were indistinguishable.)* No tool in this package writes to `Console`; diagnostics go to `IIoContext.AddTraceMessage`. |
| `-noclobber` and the file exists | `Failure`: `Refusing to overwrite existing file: {path} (omit -noclobber to replace it)`. |
| Outside `CHAT_EXPORT_ROOT` | `Failure`: `Path {path} is outside the configured export root {root}`. |
| Interrupted mid-write | The previous file is intact; the temp file is orphaned and cleaned up on the next export to the same directory. |
| Unparseable piped chunk | One failure chunk naming the chunk's correlation id; the export continues. |

**Security and audit.** The `path` parameter is not a secret, but the **file content is the entire conversation**, which routinely contains pasted code, tokens and internal URLs. Two consequences: (1) the file inherits the process umask and is written with owner-only permissions where the platform supports it; (2) the audit event records the resolved path and the message count, never the content. **Destructive**: it silently overwrites an existing file, which is the source's behaviour and is preserved deliberately so that re-exporting a working session keeps working — `-noclobber` is the opt-in guard, and the atomic write means the destructive step is now all-or-nothing rather than a truncation. No confirmation prompt, for the same reason.

**Traceability.** PRD **7.4 Chat History**. Source ancestor: `/export <file_path>` (`Commands/ExportCommand.cs`, `Services/ChatHistoryService.cs:29-43`) and GUI *File ▸ Export History…* (save dialog pre-filled `{home}/chat_history.json`).

---

#### 1.8 `CHAT IMPORT` — load a record from a file

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Import", "Import a conversation record from a file",
    Prototype = "CHAT IMPORT <file_path...> [-mode replace|append] [-validate warn|off|strict] " +
                "[-maxbytes <n>] [-emit] [-quiet]")]
[CommandParameterNamed("mode", "Replace the record or append to it",
    DefaultValue = "replace", AllowedValues = new[]{ "replace", "append" })]
[CommandParameterNamed("validate", "How to treat messages whose role is outside user|assistant|system",
    DefaultValue = "warn", AllowedValues = new[]{ "warn", "off", "strict" })]
[CommandParameterNamed("maxbytes", "Refuse files larger than this; 0 = unlimited",
    DataType = typeof(long), DefaultValue = "33554432")]
[CommandFlag("emit", "Emit one chunk per imported message instead of a summary")]
[CommandFlag("quiet", "Emit nothing on success")]
[CommandParameterSuffix("path", "Source path; a leading ~ is expanded", IsRequired = true, UsePipe = true)]
public sealed class ImportCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `IMPORT` |
| Root command | `CHAT` |
| Description | Import a conversation record from a file |
| Usage prototype | `CHAT IMPORT <file_path...> [-mode replace\|append] [-validate warn\|off\|strict] [-maxbytes <n>] [-emit] [-quiet]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `path` | suffix | `string` | yes (unless piped) | — | any path; leading `~` expanded | `UsePipe = true`. **No extension is defaulted on import** — preserved, including the asymmetry that makes `CHAT EXPORT mychats` (writes `mychats.json`) followed by `CHAT IMPORT mychats` fail. *(Small correction: on a miss, if `{path}.json` exists the failure adds `(did you mean {path}.json?)`.)* |
| `mode` | named | `string` | no | `replace` | `replace`, `append` | `replace` is the source's only behaviour — it empties the message list and refills it in file order, and **mutates the live record in place rather than swapping it**, which matters because every tool and both UIs hold the same reference. `append` is **NEW** and earns its place because merging a saved probe into a live session is otherwise impossible. |
| `validate` | named | `string` | no | `warn` | `warn`, `off`, `strict` | **NEW default of `warn` is a deliberate improvement.** The source performed **zero** validation on import: roles were not checked against the whitelist `/inject` enforced, content was unbounded, timestamps unchecked, and the `isCommand` flag honoured as read — so a file could introduce a role that hosted backends variously drop, forward to the remote service, or coerce to `user`. `off` reproduces the source exactly; `warn` imports but emits one trace note per offending message; `strict` refuses the file. |
| `maxbytes` | named | `long` | no | **`33554432`** (32 MiB) | 0 (unlimited) – 1073741824 | **NEW.** The source read the whole file into a string before parsing with no size check, no streaming and no cap. |
| `emit` | flag | `bool` | no | `false` | presence = true | **NEW.** One chunk per imported message, so an import can feed a pipeline directly. |
| `quiet` | flag | `bool` | no | `false` | presence = true | **NEW.** |

**Preserved semantics.** The imported **session identifier overwrites** the live one. Messages arrive in file order. Unknown keys are ignored; key matching is **case-sensitive**. Comments and trailing commas are rejected. A JSON object with no `messages` key yields an empty list and replaces the live session id with a freshly generated one, reported as `Successfully imported 0 messages`. *(Deliberate correction: the imported `createdAt` is now restored instead of silently discarded. The source copied the session id on one line and never copied the creation timestamp anywhere, so a re-exported file carried the importing process's start time — a silent rewrite of history metadata.)*

**Success text.** `Successfully imported {n} messages from: {resolved path}`. Failure text: `Failed to import chat history from: {resolved path}` **plus the underlying reason** (source correction, as for export).

**Pipeline behaviour — both.** Accepts piped input: **one chunk is one file path**. Each path is imported in order; with `-mode replace` the last file wins and the tool emits a warning chunk on the second and subsequent paths (`Each replace discards the previous import; use -mode append to merge`), with `-mode append` they concatenate. Produces one summary chunk per path, or one chunk per message under `-emit`. `ResultFormat.General`, or `.JSON` under `-emit`. Because `-emit` needs many chunks on the non-piped path, this tool **overrides `Main`**.

**Environment interaction.** Reads `CHAT_SESSION`, `CHAT_IMPORT_ROOT` (`storeDefault: false`). Writes `CHAT_LAST_IMPORT_PATH`, `CHAT_TURN_COUNT`, `CHAT_SESSION_ID`. Declares `{ "IMPORT_ROOT", "" }`. No environment-modifying permission needed.

**Failure modes.** No path → `Failure`: `Usage: CHAT IMPORT <file_path>`. Missing file → `Failure`: `File not found: {path}` (source text, now delivered as a chunk rather than printed to stdout), plus the `.json` hint. Malformed JSON → `Failure`: `Error importing chat history: {parser reason}` (source text) — **the live record is left completely untouched**, preserved. A file whose content is the literal `null` takes the same path as a parse error. File larger than `maxbytes` → `Failure`: `File exceeds the {n}-byte import limit; raise -maxbytes to override`. `-validate strict` with a bad role → `Failure` naming the offending index and role; nothing imported. Outside `CHAT_IMPORT_ROOT` → stated failure. Failed upstream chunks forward verbatim.

**Security and audit.** No secrets in parameters. **This is the package's principal untrusted-input surface**: an imported file is attacker-controllable content that becomes model context on the next turn (a prompt-injection vector), can carry a role that bypasses the whitelist, and can set the `isCommand` flag to hide a message from the model while leaving it in the transcript. `-validate warn` and `-maxbytes` are the mitigations that ship on by default; `CHAT_IMPORT_ROOT` is the mitigation an operator can add. **Destructive** in `-mode replace` — it discards the entire live record with no undo. Confirmation policy: when the live record is non-empty and `-mode replace` is in effect, the tool prompts `Replace {n} messages with the contents of {file}? (y/N) ` unless `-yes`-equivalent is implied by a non-interactive context, in which case it proceeds (matching the source, which never asked) but emits a warning chunk naming the discarded count. Inside a pipeline it never prompts.

**Traceability.** PRD **7.4 Chat History**. Source ancestor: `/import <file_path>` (`Commands/ImportCommand.cs`, `Services/ChatHistoryService.cs:45-65`) and GUI *File ▸ Import History…* (open dialog rooted at the home directory).

---

#### 1.9 `CHAT SESSIONS` — list saved sessions *(NEW)*

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Sessions", "List saved conversation sessions",
    Prototype = "CHAT SESSIONS [-format text|json|csv] [-sort name|used|created|size] [-filter <text>]")]
[CommandParameterNamed("format", "Output shape",
    DefaultValue = "text", AllowedValues = new[]{ "text", "json", "csv" })]
[CommandParameterNamed("sort", "Ordering",
    DefaultValue = "used", AllowedValues = new[]{ "used", "name", "created", "size" })]
[CommandParameterNamed("filter", "Only list sessions whose name contains this text")]
public sealed class SessionsCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `SESSIONS` |
| Root command | `CHAT` |
| Description | List saved conversation sessions |
| Usage prototype | `CHAT SESSIONS [-format text\|json\|csv] [-sort name\|used\|created\|size] [-filter <text>]` |

**Why NEW, and why it earns its place.** The source had **no autosave and no autoload**: the record was created empty at process start and lost at process end unless the user explicitly exported it. It carried a session identifier that nothing ever read — not for lookup, not for file naming, not for logging. That is the largest single capability gap in the product, and the whole point of a model-debugging tool is to run the *same* conversation against different settings and compare. Making the session a named, listable, switchable object closes it. `SESSIONS` is the read half.

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text`, `json`, `csv` | `text` → `{marker}{name}  {messages} msgs  last used {timestamp}`, where marker is `*` for the active session. Sets `ResultFormat` accordingly. |
| `sort` | named | `string` | no | `used` | `used`, `name`, `created`, `size` | `used` is descending (most recent first); `name` is ordinal ascending. |
| `filter` | named | `string` | no | `""` | free text | Case-insensitive substring match against the session name. |

**Pipeline behaviour — output only.** Refuses piped input with an explanatory string. Emits one chunk per session, so `CHAT SESSIONS -filter bug- | CHAT USE` works (`CHAT USE`'s ordered `name` parameter declares `UsePipe = true`). **Overrides `Main`.**

**Environment interaction.** Reads `CHAT_SESSION` (to mark the active one) and `CHAT_SESSION_ROOT` (`storeDefault: false`). Writes nothing. Declares `{ "SESSION_ROOT", "" }` — empty means the platform default location.

**Failure modes.** Session directory missing → **success with a single chunk** `No saved sessions.` (creating the directory is `CHAT USE`'s job, not this one's). Directory unreadable → `Failure` with the reason. A session file that will not parse is **skipped**, not fatal, and reported as one failure chunk naming the file — failure isolation per file, mirroring the framework crawler's per-package isolation. Zero arguments is the common case and is handled explicitly.

**Security and audit.** No secrets. Session **names** are user-chosen and appear in audit records; content does not. Read-only, not destructive. The listing reflects only the session root; it never enumerates arbitrary directories.

**Traceability.** **NEW.** Nearest source ancestor: the unused `sessionId` field on the record (`Models/ChatHistory.cs:8-12`). PRD **7.4 Chat History**.

---

#### 1.10 `CHAT USE` — switch to (or create) a named session *(NEW)*

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Use", "Switch the active conversation session",
    Prototype = "CHAT USE <name> [-create yes|no] [-nosave] [-fork]")]
[CommandParameterOrdered("name", "Session name", IsRequired = true, UsePipe = true)]
[CommandParameterNamed("create", "Create the session if it does not exist",
    DefaultValue = "yes", AllowedValues = new[]{ "yes", "no" })]
[CommandFlag("nosave", "Discard unsaved changes to the current session instead of saving them")]
[CommandFlag("fork", "Copy the current record into the new session instead of starting empty")]
public sealed class UseCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `USE` |
| Root command | `CHAT` |
| Description | Switch the active conversation session |
| Usage prototype | `CHAT USE <name> [-create yes\|no] [-nosave] [-fork]` |

**Why NEW.** The write half of the session feature. `-fork` is the tool that makes the product's core experiment cheap: branch the conversation at its current state, then change one setting and continue in the branch, leaving the original intact for comparison.

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** | — | 1–128 characters | `UsePipe = true`, so a piped chunk supplies it. Sanitized for use as a file name by replacing every invalid filename character with `_` — the same rule the source used for system-prompt file names. |
| `create` | named | `string` | no | `yes` | `yes`, `no` | `no` turns a typo into an error rather than a new empty session. |
| `nosave` | flag | `bool` | no | `false` | presence = true | Default saves the current record before switching, so switching is non-destructive. |
| `fork` | flag | `bool` | no | `false` | presence = true | Copies the current record into the target session (which must not already exist) and mints a new session identifier for the copy. |

**Success text.** `Switched to session '{name}' ({n} messages)`, or `Created session '{name}'`, or `Forked '{from}' into '{name}' ({n} messages)`.

**Pipeline behaviour — both, but pathologically.** Accepts piped input (`name` declares `UsePipe = true`); **one chunk is one session name**, and each switches the active session, so a multi-chunk pipe leaves the *last* name active. That is genuinely useful with `CHAT SESSIONS -filter x | CHAT USE` where the filter yields one result, and confusing otherwise — so when more than one chunk arrives the tool emits a warning chunk (`{n} session names arrived; '{last}' is now active`). Produces one confirmation chunk per switch. `ResultFormat.General`.

**Environment interaction.** Reads `CHAT_SESSION_ROOT`. **Writes `CHAT_SESSION` and `CHAT_SESSION_ID`** into this package's bucket — and this is the mechanism by which every other tool in the package sees the switch on its next invocation, without any tool holding process state and without `ModifiesEnvironment = true`, because both keys carry the `CHAT_` root prefix. **This is the single most important environment interaction in the package.**

**Failure modes.** Missing name → `Failure`: `Usage: CHAT USE <name>`. `-create no` and the session does not exist → `Failure`: `No session named '{name}'. Run CHAT SESSIONS to list, or omit -create no to create it.` `-fork` onto an existing name → `Failure`: `Session '{name}' already exists; forking will not overwrite it.` Saving the current session fails → `Failure` **and the switch does not happen** (fail-closed; the user does not silently lose the current record). Session root not creatable → `Failure`: `Session store is unavailable in this host.` — and the package continues to work in memory-only mode with a warning, which is the restricted-host degrade path. Sanitized name collides with an existing different name → `Failure` naming both.

**Security and audit.** No secrets. **Path-traversal surface**: the session name becomes a file name. Two defences, both required — sanitize invalid filename characters to `_`, *then* verify component-wise that the resolved path is inside the session root after symlink resolution (never a string `StartsWith`). A name of `../../etc/x` sanitizes and then fails containment. Destructive only under `-nosave`, which discards unsaved changes to the current session; that flag prompts `Discard unsaved changes to '{current}'? (y/N) ` unless piped.

**Traceability.** **NEW.** PRD **7.4 Chat History**.

---

#### 1.11 `CHAT NAME` — show, set, or rename the current session *(NEW)*

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Name", "Show or set the name and identifier of the current session",
    Prototype = "CHAT NAME [<new_name...>] [-id <guid>] [-format text|json]")]
[CommandParameterNamed("id", "Set the session identifier explicitly", DataType = typeof(Guid))]
[CommandParameterNamed("format", "Output shape",
    DefaultValue = "text", AllowedValues = new[]{ "text", "json" })]
[CommandParameterSuffix("newname", "New session name; omit to show the current one", IsRequired = false)]
public sealed class NameCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `NAME` |
| Root command | `CHAT` |
| Description | Show or set the name and identifier of the current session |
| Usage prototype | `CHAT NAME [<new_name...>] [-id <guid>] [-format text\|json]` |

**Why NEW.** The naming half of the session feature, and the only place a reproducible experiment can pin an identifier. The source generated a random session id at construction, overwrote it wholesale on import, and never read it for anything.

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `newname` | suffix | `string` | no | `""` | 1–128 characters | Absent → the tool *shows* the current name, id, message count and creation timestamp. Present → renames, moving the session file. |
| `id` | named | `Guid` | no | *(unset)* | canonical hyphenated UUID | Sets the session identifier. Format preserved from the source: canonical lowercase hyphenated form. |
| `format` | named | `string` | no | `text` | `text`, `json` | |

**Zero-argument behaviour is the common case** and is explicitly designed around the framework's early return: with no arguments `ProcessParameters` yields an **empty dictionary**, no defaults are applied and no fields are injected, so the tool must treat "empty dictionary" as "show the current session" rather than as "rename to the default value". Stated here because it is the exact shape of the trap.

**Pipeline behaviour — output only.** Refuses piped input (a rename per chunk is meaningless and destructive). One output chunk. `ResultFormat.General` / `.JSON`.

**Environment interaction.** Reads `CHAT_SESSION`, `CHAT_SESSION_ROOT`. Writes `CHAT_SESSION` and `CHAT_SESSION_ID` on a rename or an `-id` change. No environment-modifying permission needed.

**Failure modes.** Rename onto an existing session name → `Failure`: `Session '{name}' already exists.` `-id` not a valid UUID → the framework marks the parameter invalid; the tool returns `Failure`: `Session id must be a UUID.` Session store unavailable → shows the in-memory session and warns that the rename cannot be persisted. Name sanitization and containment as for `CHAT USE`.

**Security and audit.** No secrets. Changing a session identifier **breaks the link between a saved record and any analysis previously derived from it** — the tool says so in the confirmation chunk. Renaming is a file move within the session root, containment-checked. Not destructive to content.

**Traceability.** **NEW.** Nearest source ancestor: `sessionId` on the record and the fact that import overwrote it. PRD **7.4 Chat History**.

---

#### 1.12 `CHAT RETRY` — re-run the last user turn with different settings *(NEW)*

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Retry", "Discard the last reply and re-send the preceding user turn",
    Prototype = "CHAT RETRY [-provider inherit|azure|bedrock|llama] [-model <id>] " +
                "[-temperature <0.0-2.0>] [-maxtokens <1-8192>] [-logprobs inherit|on|off] " +
                "[-topk <1-20>] [-prompt <name>] [-stream on|off] [-format text|json] " +
                "[-timeout <seconds>] [-keep] [-yes]")]
[CommandParameterNamed("provider", "Backend for this retry only",
    DefaultValue = "inherit", AllowedValues = new[]{ "inherit", "azure", "bedrock", "llama" })]
[CommandParameterNamed("model", "Model id for this retry only")]
[CommandParameterNamed("temperature", "Sampling temperature, 0.0-2.0", DataType = typeof(double), ShortAlias = "t")]
[CommandParameterNamed("maxtokens", "Maximum reply tokens, 1-8192", DataType = typeof(int))]
[CommandParameterNamed("logprobs", "Request per-token log probabilities",
    DefaultValue = "inherit", AllowedValues = new[]{ "inherit", "on", "off" })]
[CommandParameterNamed("topk", "Alternatives per token, 1-20", DataType = typeof(int))]
[CommandParameterNamed("prompt", "System prompt name for this retry only")]
[CommandParameterNamed("stream", "Emit the reply incrementally",
    DefaultValue = "on", AllowedValues = new[]{ "on", "off" })]
[CommandParameterNamed("format", "Output shape",
    DefaultValue = "text", AllowedValues = new[]{ "text", "json" })]
[CommandParameterNamed("timeout", "Abandon the call after N seconds; 0 = wait forever",
    DataType = typeof(int), DefaultValue = "0")]
[CommandFlag("keep", "Emit the discarded reply as the first output chunk")]
[CommandFlag("yes", "Skip the confirmation prompt", ShortAlias = "y")]
public sealed class RetryCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `RETRY` |
| Root command | `CHAT` |
| Description | Discard the last reply and re-send the preceding user turn |
| Usage prototype | `CHAT RETRY [-provider …] [-model …] [-temperature <0.0-2.0>] [-maxtokens <1-8192>] [-logprobs …] [-topk <1-20>] [-prompt <name>] [-stream on\|off] [-format text\|json] [-timeout <seconds>] [-keep] [-yes]` |

**Why NEW, and why it earns its place.** This is the product's central loop expressed as one command. The source made the user do it by hand every time: `/pop` the assistant turn, retype the question (or lose it), `/set temperature 0.3`, ask again — and if the provider call had failed, the orphan user turn was left behind with `/pop` documented as the remedy. `CHAT RETRY -temperature 0.3` is that whole sequence, and because the overrides are per-invocation and transient, it does not disturb the configured settings. Sweeping a parameter — the same question at five temperatures — becomes five keystrokes apiece.

**Parameters** are `CHAT SEND`'s override set verbatim (same names, types, defaults, ranges and rejection texts — see §1.1), minus `message`, `norecord` and `rollback`, plus:

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `keep` | flag | `bool` | no | `false` | presence = true | Emits the discarded assistant turn as the **first** output chunk so it can be piped somewhere before it is gone. |
| `yes` | flag | `bool` | no | `false` | presence = true | Skips the confirmation. Required inside a pipeline. |

**Behaviour.** Locate the last message whose lower-cased role is `user`; discard every message after it (in practice the one assistant reply, but a fabricated tail is handled the same way); re-send the located user turn with the overrides applied; append the new reply. If the last turn is already a `user` turn with no reply after it — the state the source left behind after a failed provider call — nothing is discarded and the turn is simply re-sent.

**Pipeline behaviour — output only.** Refuses piped input (`Unsupported retry method for {chunk} (piped)`); a retry has no per-chunk meaning. Produces piped output: the discarded reply under `-keep`, then the streamed reply chunks, then a terminal chunk with elapsed time. **Overrides `Main`.** `ResultFormat.General` / `.JSON`.

**Environment interaction.** Identical to `CHAT SEND` (§1.1): reads the seven `CHATDBG_*` settings plus `CHAT_SESSION`; writes `CHAT_LAST_ELAPSED_MS`, `CHAT_TURN_COUNT`. No environment-modifying permission needed.

**Failure modes.** No `user` turn in the record → `Failure`: `Nothing to retry: no user message in the conversation record.` All the backend failure modes of `CHAT SEND` apply verbatim (unknown provider, not configured, no backend, no reply text, no log probabilities returned, timeout). **On a backend failure the discarded reply is not restored** — which is why `-keep` exists and why the confirmation exists; the failure chunk says so explicitly.

**Security and audit.** No secrets. **Destructive**: it removes the previous reply, irreversibly unless `-keep` was given. Confirmation: prompts `Discard the last reply and re-send? (y/N) ` unless `-yes` or `-keep`, and refuses inside a pipeline without `-yes`. Cost note as for `CHAT SEND` — a retry is a fresh billable call. Audit-masking obligation as for `CHAT SEND`, though `RETRY` itself passes no message text on the command line, which makes it the *safer* of the two to leave unmasked.

**Traceability.** **NEW.** Source ancestors it composes: the chat-turn path (`ChatShell.cs:343-410`), `/pop` (`Commands/PopCommand.cs`), and the documented orphan-turn remedy (`ChatShell.cs:404-408`). PRD **7.4 Chat History** and **7.6 AI Provider Abstraction**.

---

### Pipeline compositions

All six run through `CommandController.Run(line, ioContext, environmentContext)`. Remember the framework's two-stage tokenization: `PipelineParser` splits on unquoted `|` and **consumes the quotes**, then each segment is re-tokenized by the argument regex — which is exactly why every free-text and path parameter in this package is a **suffix** parameter that re-joins the remaining tokens with single spaces.

**1. Ask with a colder temperature and inspect the token probabilities — crosses into `ChatDbg.Tools.TokenAnalysis`.**

```
CHAT SEND -temperature 0.2 -logprobs on -topk 10 -format json why does this deadlock | TOKEN INSPECT -top 5 | VIEW GRID -maxalt 5
```

The user gets the reply streaming into the terminal as `CHAT SEND` produces chunks, each chunk a turn object carrying its per-token log probabilities; `TOKEN INSPECT` (package `ChatDbg.Tools.TokenAnalysis`, PRD 7.9/7.10) converts each into a token analysis; `VIEW GRID` (package `ChatDbg.Tools.Render`, PRD 7.12) lays the tokens out as cards, one card per 40 terminal columns with a minimum of one column, capped at 5 alternatives per card with a `+ N more` summary — the source's grid geometry, preserved. Because all three stages run concurrently on bounded channels, the first tokens are being rendered while the model is still generating.

**2. Re-run every question in this conversation against a different model, without touching the record.**

```
CHAT LIST -role user | CHAT SEND -norecord -model gpt-4o -temperature 0 -format json | CHAT EXPORT ~/sweep-gpt4o.jsonl -format jsonl
```

`CHAT LIST` emits one chunk per user turn; `CHAT SEND` treats each chunk as an independent prompt and, because of `-norecord`, leaves the live record exactly as it was; `CHAT EXPORT` — in its piped mode, where one chunk is one message record — writes the answers as JSON Lines. The user gets a side-by-side corpus for a model comparison and a conversation that is still fit to be a control.

**3. Save a filtered slice of the conversation — using the framework's own built-in `REGIF` filter.**

```
CHAT LIST -format json -logprobs | REGIF "\"role\":\"assistant\"" | CHAT EXPORT ~/answers-only.json -noclobber
```

`REGIF` is one of the framework's built-in commands (registered by `RegisterBuiltInCommands()`), and it filters by returning an **empty success** for non-matching chunks, which the host drops. The user gets a JSON file containing only the assistant turns with their log probabilities, and `-noclobber` means a second run reports `Refusing to overwrite existing file` instead of silently replacing it.

**4. Save what you are about to destroy.**

```
CHAT POP -count 4 -format json | CHAT EXPORT ~/rolled-back.json
```

`CHAT POP` emits the four removed messages, newest first, as JSON objects; `CHAT EXPORT` writes them. The user gets the record trimmed back four turns **and** a file from which those turns can be re-imported with `CHAT IMPORT ~/rolled-back.json -mode append`. This composition is the undo the source product never had. Note that `-count 4` inside a pipeline requires `-yes` — the tool cannot prompt when `HasPipedInput` is true — so the real line is `CHAT POP -count 4 -yes -format json | CHAT EXPORT ~/rolled-back.json`.

**5. Jump to a session by fuzzy name.**

```
CHAT SESSIONS -filter deadlock -format text | CHAT USE
```

`CHAT SESSIONS` emits one chunk per matching session; `CHAT USE`'s ordered `name` parameter declares `UsePipe = true`, so it is fed from the chunk instead of the command line. With one match the user is switched to it; with several, the last wins and a warning chunk names it. `CHAT USE` writes `CHAT_SESSION` into the package's environment bucket, so the very next `CHAT SEND` in the same shell is already in the new session.

**6. Seed a conversation from a file, ask, and branch — crosses into `ChatDbg.Tools.Prompts`.**

```
PROMPT SHOW security-expert | CHAT INJECT system -position 0 -quiet
CHAT SEND -prompt security-expert review the auth middleware
CHAT USE auth-review-cold -fork
CHAT RETRY -temperature 0 -keep | CHAT EXPORT ~/cold-answer.md -format md
```

Four lines rather than one pipeline, because they are four decisions. `PROMPT SHOW` (package `ChatDbg.Tools.Prompts`, PRD 7.5) emits the prompt body; `CHAT INJECT` places it at the head of the record as a `system` turn. The question is asked. `CHAT USE -fork` copies the whole conversation into a new session with a new identifier, leaving the original untouched. `CHAT RETRY -temperature 0 -keep` re-asks the same question deterministically in the fork, emits the discarded warm answer first, and the Markdown export captures both. That is the product's central experiment, and it is four commands.

---

### Design notes for the architect

**What this package holds.** Almost nothing, deliberately. `CommandFactory` constructs a **fresh command instance per execution**, and `CommandExecutor` does **not** dispose the instance it executed — so any state a tool holds in a field is per-invocation at best and leaked at worst. Therefore:

- The conversation record lives behind **`IConversationStore`**, a host-registered singleton resolved through the framework's DI path. Every tool takes it as a constructor dependency and touches the record only through it.
- The *identity* of the current session lives in the **environment context**, as `CHAT_SESSION` / `CHAT_SESSION_ID` in this package's private bucket. Because every key carries the root-command prefix, the host does not need to register any tool in this package with `modifiesEnvironment: true` — the framework routes prefixed writes into the command's own bucket and hands them back on the next invocation. This is the entire cross-invocation state mechanism, and it is worth being explicit that it costs nothing and needs no elevated permission.
- **Non-obvious framework detail to verify at integration**: sub-commands are dispatched through their root, so the command key the controller passes to `GetChild(commandName)` is `CHAT`, not `SEND`. All twelve tools therefore **share one environment bucket** and must read the `CHAT_`-prefixed forms of the keys they declare in `GetDefaultEnvironment()`. That sharing is a feature here (it is how `USE` talks to `SEND`), but it means two tools must never declare the same default key with different meanings.
- Per-pipe state (the injection ordinal, the export temp-file handle, the chunk counter) lives in instance fields initialised in `OnStartPipe` and flushed in `OnEndPipe`, which run only on the piped path.

**What this package must not hold.** No static conversation record. No `HttpClient`, no provider SDK, no native library, no credential — those belong to `ChatDbg.Tools.Providers` and `ChatDbg.Tools.Credentials`, and keeping them out is precisely what lets this assembly load under `AssemblySecurityPolicy.Strict` with `DisallowDynamicAssemblies = true`. No `Console` calls: the source product wrote persistence diagnostics straight to stdout, which painted stray text over the full-screen shell on every export or import failure. Everything a tool wants to say goes out as an `IResult<string>` chunk, a status message, or `AddTraceMessage`.

**How it stays testable.** The two seams — `IConversationStore` and `IConversationBackend` — are the whole test strategy. A hermetic suite drives every tool against an in-memory store and a scripted backend that replays canned replies and log-probability payloads, using the framework's `MemoryIoContext` (whose `Output` bag fills only when there is no output pipe, i.e. on the last stage or a standalone run) and a plain `EnvironmentContext`. A second, separate suite exercises the file-backed store and export/import round-trips against a temp directory, including the byte-for-byte format assertions: 2-space indentation, seven-digit UTC fractional seconds, the aggressive escaping profile (apostrophe as `\u0027`, backtick as `\u0060`, plus as `\u002B`, non-ASCII as `\uXXXX`), shortest-round-trip numbers, key order, `null` written literally. Keep those two suites in separate projects; never mix the hermetic one with anything that touches a real disk or a real network.

**When a capability is unavailable.** The rule is *degrade at the smallest granularity that still tells the truth*:

| Missing | Behaviour |
|---|---|
| No `IConversationBackend` (restricted host, or the Providers package was not loaded) | `SEND` and `RETRY` fail with `No conversation backend is available in this host. Record tools (CHAT LIST/INJECT/POP/CLEAR/IMPORT/EXPORT) still work.` The other ten tools are unaffected. |
| Backend present but not configured | The source's exact two-line advisory, retargeted at `CRED STATUS`. No network call is attempted. |
| Backend cannot stream | `-stream on` is honoured as a request, not a demand: one chunk arrives at the end and a trace note records that streaming was unavailable. **Never a failure.** |
| Backend returns no log probabilities | The source's two-line note, and the turn is still recorded. **Never a failure** — hosted models and local models differ here and the user should not have to know which. |
| Local backend ignores most of the record | Stated, not silently absorbed: the source's local-model paths read **only the content of the last message whose role case-insensitively equals `user`** and ignored the is-a-command flag entirely, so injecting or popping assistant and system turns changed nothing the model saw. `CHAT INJECT` and `CHAT POP` emit a one-line trace note when the active provider is `llama` and the affected role is not `user`. Reproducing the behaviour silently would make the product lie about its own core feature. |
| Session store unreadable or uncreatable | The package falls back to a memory-only store for the process, emits one warning chunk at first use, and `SESSIONS`/`USE`/`NAME` report `Session store is unavailable in this host.` Editing, sending and exporting continue. |
| Non-interactive or piped context where a confirmation is required | Never prompt into a pipe — `PromptForCommand` is only meaningful when `HasPipedInput` is false. `POP -count > 1`, `CLEAR` and `RETRY` refuse with a message naming `-yes`. `IMPORT -mode replace` proceeds (matching the source, which never asked) but emits a warning chunk naming the discarded count. |
| Windows-only capabilities | There are none in this package. The source's only Windows-bound behaviour was credential storage, which lives in `ChatDbg.Tools.Credentials`. Every path here resolves through the platform's own home and local-application-data locations, and the `~` expansion accepts `~/`, `~\` and a bare `~` on all platforms. |

**Where it should degrade rather than fail — and where it must not.** Degrade: a missing backend, a non-streaming backend, absent log probabilities, an unwritable session store, an unparseable session file in a listing, a chunk that will not parse mid-export. Fail closed, always: a `-backup` that cannot be written before a `CLEAR`; a save that fails during a `CHAT USE` switch; a path outside a configured `CHAT_EXPORT_ROOT` / `CHAT_IMPORT_ROOT`; a session name that fails containment after sanitization; an import larger than `-maxbytes`. The distinction is whether the user could lose data they cannot get back — if yes, refuse.

**Two quirks deliberately preserved, and why.** First, **space collapsing**: every free-text and path parameter joins its tokens with single spaces, so `~/my  chats/a.json` still becomes `~/my chats/a.json`. This is not laziness — it is the framework's suffix-parameter contract and the source's tokenizer behaving identically, and pretending otherwise would require a quoting layer the pipeline parser already ate. It is documented in `CommandHelpRemarks` on every affected tool. Second, **`CHAT EXPORT` still overwrites silently**, because re-exporting a working session to the same path is the source's normal case and breaking it would be gratuitous; the atomicity fix removes the actual harm (a truncated file where a valid history used to be) without changing the contract, and `-noclobber` is there for the careful.

**One quirk deliberately abandoned.** The source read `/inject`'s position from the *last positional token*, and only when there were strictly more than two arguments — so `/inject user 42` injected the text `42`, `/inject user 42 7` injected `42` at position 7, and an unparseable trailing token silently became part of the message while the GUI dialog silently discarded it. Two paths, two incompatible rules, both undiscoverable. `-position` is a named parameter here, and that is a change users will notice; it is called out in this package's migration notes and in the tool's help remarks.
