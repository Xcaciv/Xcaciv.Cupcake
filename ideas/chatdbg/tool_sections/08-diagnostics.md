## 8. ChatDbg.Tools.DiagnosticsObservability — Diagnostics & Observability

> Root command: **`DIAG`**. Every tool in this package is a sub-command invoked as `DIAG <VERB> …`.
> Framework: `Xcaciv.Command` **3.3.4** contracts (`ICommandDelegate`, `AbstractCommand`, `IIoContext`,
> `IEnvironmentContext`, `IResult<string>`), hosted by a Cupcake-pattern shell and loaded through
> `Xcaciv.Loader`. Where this chapter and memory disagree with `synthesis/ref-command.md`, the reference wins.

---

### 8.1 Purpose and boundary

**What this package owns.**

1. **The diagnostic recorder** — the process-wide, thread-safe, timestamped text buffer that captures (a) every
   line the native inference engine emits through its process-global log-sink hook and (b) every application-level
   entry any part of the product chooses to record. This is the rebuilt form of
   `Services/TokenInspection/LLamaSharpLogConfig.cs`.
2. **Buffering, thresholded auto-flush, daily rolling files, retention and pruning.** The source had a buffer, a
   10 000-character threshold and date-stamped file names; it had *no* retention, *no* size cap and *no* pruning
   (diagnostic-logging dossier, business rule 18). Retention is new here and is marked as such.
3. **Export** — writing a durable snapshot a user can attach to a bug report. This is the rebuilt (and repaired)
   form of the `export-logs` command, which in the source was a stub registered in neither shell and which wrote
   no file at all (QUIRK-1, QUIRK-2, QUIRK-3).
4. **The environment health check ("doctor")** — a single command that reports runtime and build-profile facts,
   backend availability, native-library resolution, credential *resolution provenance* (never values), and
   configuration validity against the product's real ranges.
5. **Audit-stream configuration** — turning the framework's `IAuditLogger` on, choosing its sink, and configuring
   redaction, plus honestly reporting what that redaction can and cannot mask.

**What this package explicitly does NOT own.**

| Not owned | Owner |
|---|---|
| Loading models, creating inference contexts, running generation, arming the *engine's* own behaviour | **`ChatDbg.Tools.Inference`** (root `LLM`) — PRD 7.8. `DIAG` installs the sink; `LLM` is what makes the engine talk into it. |
| Per-token probability capture, token analyses, heatmaps, `TokenAnalysis` JSON export | **`ChatDbg.Tools.TokenAnalysis`** (root `TOKEN`) — PRD 7.9 / 7.10. The source's folder placement (`Services/TokenInspection/LLamaSharpLogConfig.cs`) is misleading; there is no dependency (QUIRK-7). |
| Reading, writing, validating or persisting settings | **`ChatDbg.Tools.Configuration`** (root `CFG`) — PRD 7.2. `DIAG DOCTOR` *validates a snapshot* against the documented ranges; it never writes one. |
| Resolving, storing, migrating or displaying credentials | **`ChatDbg.Tools.Credentials`** (root `CRED`) — PRD 7.3. `DIAG DOCTOR` reports which **tier** would win and whether a value is present; it never reads, prints, fingerprints beyond a length class, or copies secret material. |
| Rendering — colour, markup, heatmaps, tables, themes | **`ChatDbg.Tools.Rendering`** (root `RENDER`) — PRD 7.12. `DIAG` emits plain text and declares an `OutputFormat`; how it is painted is the host's `IIoContext` and `RENDER`'s business. |
| Producing distribution artefacts, running the release pipeline, publishing | **`ChatDbg.Tools.Packaging`** (root `PKG`) — PRD 7.15. `DIAG BUILDINFO` *reads* the identity of the artefact it is running inside; it never builds one. |
| Chat history, system prompts, provider clients | `ChatDbg.Tools.History` (7.4), `ChatDbg.Tools.Prompts` (7.5), `ChatDbg.Tools.Providers` (7.6/7.7). |

**The governing philosophy, preserved verbatim from the source:** *diagnostics must never break the thing they are
diagnosing.* Every operation degrades rather than throws. The one place the rebuild deliberately deviates is that
the source made a completely dead logging subsystem **indistinguishable from a healthy one** (diagnostic-logging
acceptance criterion 20, QUIRK-11): every failure went to a debug-trace sink that the shipping build configurations
compile away. This package keeps the swallow but adds an **always-available, in-band health signal** —
`DIAG STATUS` and `DIAG DOCTOR` report fault counts and the last internal failure.

---

### 8.2 Package manifest

| Facet | Value |
|---|---|
| Assembly / package name | `ChatDbg.Tools.DiagnosticsObservability` |
| Root command | `DIAG` (`[CommandRoot("DIAG", "Diagnostics and observability")]`, normalized to uppercase by `NamesValidator`) |
| Contract assembly targeted | `Xcaciv.Command.Interface` **3.3.4** + `Xcaciv.Command.Core` **3.3.4** (`AbstractCommand`). No reference to `Xcaciv.Command` (the host assembly) — the dependency arrow points tool → SDK only (Cupcake rule 4). |
| Target framework | `net10.0` (framework default per ref-command §1.2). Matches the source product's TFM. |
| Elevated trust required | **No.** No administrative rights, no driver access, no service control, no registry writes. |
| Filesystem reach | **Write:** the configured log root (default `<roaming app data>/ChatDbg/Logs`) and any path the user names to `DIAG EXPORT`. **Read:** the log root, `AppContext.BaseDirectory` and its `runtimes/<rid>/native/` subtree (presence + size + mtime only), the settings/prompt roots (existence + writability probe only, never content). **Delete:** only inside the configured log root, only from `DIAG ROTATE`, only with `-apply`. |
| Network reach | **None by default.** `DIAG DOCTOR -probe endpoints` opts in to an outbound reachability check of the configured Azure endpoint / AWS region endpoint. Off unless asked; never sends credentials; never sends a chat payload. |
| OS keystore reach | **Presence probe only.** On Windows, "does an entry named `ChatDbg:AzureApiKey` exist?" via the platform credential API. It never reads the blob. On macOS/Linux the probe reports "no OS keystore integration on this platform". |
| Native library reach | **Metadata only by default** — file existence, size, timestamp, architecture header of `llama.dll` / `libllama.so` / `libllama.dylib` under `runtimes/<rid>/native/`. An **actual load probe** (`DIAG DOCTOR -probe native`) is opt-in and runs **out of process**, because the documented failure mode is a hard `0xC0000005` access violation that no managed handler can intercept and that would otherwise kill the shell. |
| Dynamic code generation | **None.** No `System.Reflection.Emit`, no `Assembly.Load` of user input, no expression compilation. The package is compatible with `AssemblySecurityPolicy { DisallowDynamicAssemblies = true }` (ref-loader §3.1, step 4). |
| Reflection use | Read-only metadata on assemblies already loaded (`AssemblyInformationalVersionAttribute`, `TargetFrameworkAttribute`, `RuntimeInformation`, `AppContext` switches). No name-based type resolution, so it survives `TrimMode=full`. |
| Safe to load in a restricted host | **Yes**, with named degradations: with no writable log root it runs memory-only; with no native engine it captures application entries only; with no keystore it reports tier availability; with `-probe` flags refused by host policy it reports `skipped (policy)` rather than failing. |
| Audit posture | Registered with the host's `IAuditLogger`. Three tools are registered `modifiesEnvironment: true` (see §8.4); all others write only their own prefixed bucket. |

---

### 8.3 Tool catalog

Eleven tools. Five are direct descendants of source behaviour, six are NEW and each states why it earns its place.

**Two framework facts that shape every tool below, stated once:**

* **Zero arguments ⇒ empty parameter dictionary.** `AbstractCommand.ProcessParameters` early-returns when
  `io.Parameters.Length == 0` (ref-command §3.4), so **no defaults are applied, no flags materialise as `false`,
  and no field injection happens**. Every tool here is specified to behave correctly when invoked bare, using the
  documented default in the parameter table as its in-code fallback.
* **Parameter parse errors are invisible to the user.** An `ArgumentException` from a missing required parameter
  surfaces only as `Error executing DIAG (see trace for more info)` (ref-command §5.8). Therefore **no tool in this
  package declares a required parameter whose absence has a good error message**: path-like ordered parameters are
  declared `IsRequired = false` and the tool returns a hand-written `CommandResult<string>.Failure(...)` carrying
  the same usage text the source printed.

---

#### 8.3.1 `DIAG CAPTURE` — arm, disarm and configure diagnostic capture

**Registration**

| Field | Value |
|---|---|
| Command | `CAPTURE` |
| Root command | `DIAG` |
| Description | `Arm engine diagnostic capture and configure the recorder's sinks and thresholds` |
| Prototype | `DIAG CAPTURE [on|off|status] [-dir <path>] [-file on|off] [-console on|off] [-trace on|off] [-buffer <chars>] [-level <min>] [-pattern <name>] [-timestamps local|localoffset|utc] [-redact on|off]` |

```csharp
[CommandRoot("DIAG", "Diagnostics and observability")]
[CommandRegister("Capture", "Arm engine diagnostic capture and configure the recorder's sinks and thresholds",
    Prototype = "DIAG CAPTURE [on|off|status] [-dir <path>] [-file on|off] …", Version = "1.0.0")]
[CommandParameterOrdered("state", "Turn capture on, off, or report its state",
    IsRequired = false, DefaultValue = "on", AllowedValues = new[] { "on", "off", "status" })]
[CommandParameterNamed("dir", "Directory that holds the rolling daily log files")]
[CommandParameterNamed("file", "Write the buffer to the rolling daily file",
    DefaultValue = "on", AllowedValues = new[] { "on", "off" })]
[CommandParameterNamed("console", "Echo every entry to standard output",
    DefaultValue = "off", AllowedValues = new[] { "on", "off" })]
[CommandParameterNamed("trace", "Echo every entry to the platform debug/trace stream",
    DefaultValue = "on", AllowedValues = new[] { "on", "off" })]
[CommandParameterNamed("buffer", "Auto-flush threshold in characters", DataType = typeof(int), DefaultValue = "10000")]
[CommandParameterNamed("level", "Minimum level retained", DefaultValue = "TRACE",
    AllowedValues = new[] { "TRACE", "DEBUG", "INFO", "WARN", "ERROR", "FATAL" })]
[CommandParameterNamed("pattern", "Daily file-name pattern", DefaultValue = "chatdbg_{0:yyyyMMdd}.log")]
[CommandParameterNamed("timestamps", "Entry timestamp rendering", DefaultValue = "local",
    AllowedValues = new[] { "local", "localoffset", "utc" })]
[CommandParameterNamed("redact", "Mask credential-shaped text on ingest", DefaultValue = "on",
    AllowedValues = new[] { "on", "off" })]
[CommandHelpRemarks("Arming is idempotent: the engine sink is installed at most once per process.")]
[CommandHelpRemarks("The engine's log-sink hook is process-global and last-writer-wins; only one recorder may own it.")]
public sealed class CaptureCommand : AbstractCommand { … }
```

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `state` | ordered | `string` | no (`IsRequired=false`; ordered params are required by default — opted out) | `on` | `on` \| `off` \| `status` | Arm, disarm, or report without changing anything. |
| `dir` | named | `string` | no | platform log root: `<roaming app data>/ChatDbg/Logs` — `%APPDATA%\ChatDbg\Logs` (Windows), `$XDG_CONFIG_HOME/ChatDbg/Logs` else `~/.config/ChatDbg/Logs` (Linux/macOS) | any writable directory; must resolve to an absolute path | Directory for the rolling daily files. Created recursively on first use. |
| `file` | named | `string`→bool | no | `on` | `on` \| `off` | Source default: file logging **on**. |
| `console` | named | `string`→bool | no | `off` | `on` \| `off` | Source default: console echo **off**. Interleaves log lines into the chat transcript when on. |
| `trace` | named | `string`→bool | no | `on` | `on` \| `off` | Source default: debug-stream echo **on**. **NEW behaviour:** this is a *runtime* switch here; in the source it was compiled out of every non-`DEBUG` build (QUIRK-11). |
| `buffer` | named | `int` | no | `10000` | `0` … `10000000` characters; comparison is strictly greater-than, so the flush fires on the append that pushes length to `threshold + 1`; `0` means "flush every engine line" | Auto-flush threshold, in **characters** of accumulated buffer text — not bytes, not entries. |
| `level` | named | `string` | no | `TRACE` | `TRACE` \| `DEBUG` \| `INFO` \| `WARN` \| `ERROR` \| `FATAL` | **NEW.** Minimum retained level. The source had no level filtering at all and no way to suppress the per-token `DEBUG` firehose (business rule 10). |
| `pattern` | named | `string` | no | `chatdbg_{0:yyyyMMdd}.log` | a composite-format string whose `{0}` is the local date | **Deliberate deviation:** the source's pattern was `llamasharp_yyyyMMdd.log`. The rebuilt recorder captures every backend, not just the local one. `DIAG TAIL` / `FILES` / `ROTATE` still recognise the legacy `llamasharp_*.log` name for backward compatibility. |
| `timestamps` | named | `string` | no | `local` | `local` \| `localoffset` \| `utc` | `local` preserves the source's `yyyy-MM-dd HH:mm:ss.fff`. **Deliberate deviation:** always rendered with **invariant culture**, fixing the source's build-dependent double shape (QUIRK-13). |
| `redact` | named | `string`→bool | no | `on` | `on` \| `off` | **NEW.** Masks credential-shaped text on ingest (see Security below). |

**Pipeline behaviour** — **Neither.** It is a configuration verb; the non-piped `AbstractCommand` path emits exactly
one chunk, a one-line confirmation such as
`Capture armed. dir=/home/u/.config/ChatDbg/Logs file=on trace=on console=off buffer=10000 level=TRACE`.
Invoked in a pipeline it follows Cupcake rule 29 and returns an explanatory `Failure`
(`DIAG CAPTURE does not accept piped input. Pipe into DIAG RECORD or DIAG EXPORT instead.`) rather than throwing.
`OutputFormat = ResultFormat.General`.

**Environment interaction** — Registered `modifiesEnvironment: true`, so it writes the **global** shell variables the
rest of the package reads:

| Written (global) | Meaning |
|---|---|
| `CHATDBG_DIAG_DIR` | resolved absolute log root |
| `CHATDBG_DIAG_FILE`, `CHATDBG_DIAG_CONSOLE`, `CHATDBG_DIAG_TRACE` | sink switches |
| `CHATDBG_DIAG_BUFFER` | threshold in characters |
| `CHATDBG_DIAG_LEVEL`, `CHATDBG_DIAG_PATTERN`, `CHATDBG_DIAG_TIMESTAMPS`, `CHATDBG_DIAG_REDACT` | filter and format policy |
| `CHATDBG_DIAG_ARMED` | `true` once the engine sink is installed |

`GetDefaultEnvironment()` returns those keys with the defaults above; the host seeds them under the
`CAPTURE_` prefix (ref-command §2.4), so the tool reads its own seeded copy as `CAPTURE_DIR` etc. and publishes
the shared, unprefixed `CHATDBG_DIAG_*` names for its siblings. All sibling reads use
`GetValue(key, default, storeDefault: false)` — `storeDefault` defaults to `true` and a bare read would otherwise
flip `HasChanged` and trigger a write-back (ref-command §7.1).
It reads **no OS environment variables** and needs no environment-modifying OS permission.

**Failure modes**

| Situation | Behaviour |
|---|---|
| `state` is not in the allow-list | Rejected at parse time by `AllowedValues`; the user sees the generic executor message, so the help text and `Prototype` carry the three legal values. |
| `-buffer` is non-numeric or negative | Parameter is materialised **invalid** (`IsValid == false`); the tool falls back to `10000` and emits `Warning: -buffer '<raw>' is not a non-negative integer; using 10000.` **Deviation:** the source accepted any integer including negatives with no validation (business rule, max-buffer-size valid range: none enforced). |
| `-dir` cannot be created (permissions, read-only volume, invalid path) | **Never throws.** Capture arms in **memory-only** mode, the fault counter increments, and the confirmation reads `Capture armed (memory-only): log directory '<path>' is not writable — file sink disabled.` **This is the deliberate repair of the source's worst failure** (business rule 19 + acceptance criterion 20): the source left the recorder un-armed and reported it on a channel that does not exist in a shipping build. |
| Native engine sink cannot be installed (native library absent, wrong architecture, blocked) | Capture arms anyway, in **application-entries-only** mode. Reports `engine sink: unavailable — <reason>`. The recorder is **still armed** (the source's armed latch was set after sink installation, so a sink failure silently disabled everything — this rebuild sets the latch first). |
| `-dir` changed after arming | **Repaired.** Directory creation is re-evaluated on every flush, not only at arm time. The source gated creation on the file-logging switch at arm time only, making "turn file logging on later" permanently broken (QUIRK-19, business rule 23). |
| A second `DIAG CAPTURE on` | Idempotent no-op for the sink; switch changes still apply. Preserves the source's arm-idempotency (business rule 6). |
| Another recorder already owns the process-global engine hook | Reports `engine sink: owned by another recorder (last-writer-wins)` and refuses to steal it unless `-force`. The source silently allowed last-writer-wins with earlier recorders still believing themselves armed (business rule 21). |
| Downstream pipe error | Not applicable — this tool is never a pipeline stage. |

**Security and audit** — No parameter carries a secret. `-dir` is a path and may contain a user name; it is recorded
in the audit event as-is (paths are not treated as secrets). The tool is **not destructive**: turning capture off
does not discard the buffer. **Redaction is configured here and applied on ingest**: with `-redact on` (default) the
recorder rewrites, before anything reaches the buffer, any occurrence of the five credential variable names' values
and any text matching `set CHATDBG_(AZURE_API_KEY|AWS_ACCESS_KEY|AWS_SECRET_KEY)=…`, `AWS_ACCESS_KEY_ID=…`,
`AWS_SECRET_ACCESS_KEY=…`, or a bearer-token/`sk-`/`AKIA`-shaped literal, to `[REDACTED]`. This exists because the
source's credential-migration wizard prints plaintext secrets to standard output on **every settings load** where
plaintext credentials exist (credential-management QUIRK-3) — with console capture on, those would land verbatim in
a file users are told to attach to bug reports.

**Traceability** — PRD **7.11 Diagnostic Logging**. Descends from `LLamaSharpLogConfig.ConfigureLogging()` and the
five never-reachable switches (`LogDirectory`, `EnableFileLogging`, `EnableDebugOutput`, `EnableConsoleOutput`,
`MaxBufferSize`, business rule 24 / QUIRK-14). The runtime configuration surface itself is **NEW** — the source
required a recompile to change any of them, while its own documentation told users to "set it in config".

---

#### 8.3.2 `DIAG STATUS` — report the recorder's own health

**Registration**

| Field | Value |
|---|---|
| Command | `STATUS` |
| Root command | `DIAG` |
| Description | `Report capture state, sink health, buffer occupancy and internal faults` |
| Prototype | `DIAG STATUS [-format text|json|csv] [-v]` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text` \| `json` \| `csv` | Output shape. Sets `OutputFormat` to `General` / `JSON` / `CSV` accordingly. |
| `verbose` | flag (`ShortAlias = "v"`) | `bool` | no | `false` (flags are always materialised as bool) | — | Include per-sink counters, the last five internal faults, and the resolved policy values. |

**Pipeline behaviour** — **Produces piped output; accepts none.** Overrides `Main` so it can emit **one chunk per
reported row** (`AbstractCommand`'s non-piped path emits exactly one chunk, ref-command §3.3), which is what makes
`DIAG STATUS | REGIF "unavailable"` work. With `-format json` it emits a **single** chunk carrying one JSON object
and declares `ResultFormat.JSON`; with `-format csv` it emits a header chunk then one chunk per row and declares
`ResultFormat.CSV`. Format is metadata only — the framework never branches on it (ref-command §10.5) — but the
host's `IIoContext` and `RENDER` do.

Reported fields: `armed`, `engineSink` (`installed` / `unavailable(<reason>)` / `owned-elsewhere`), `mode`
(`file+memory` / `memory-only`), `logDir`, `dirWritable`, `todayFile`, `todayFileBytes`, `bufferChars`,
`bufferThreshold`, `entriesBuffered`, `entriesFlushed`, `entriesDropped`, `lastFlushUtc`, `minLevel`,
`redaction`, `timestampMode`, `faultCount`, `lastFault`, `recorderOwner` (`di-singleton` / `package-static`).

**Environment interaction** — Reads `CHATDBG_DIAG_*` globals with `storeDefault: false`. Writes nothing. No
environment-modifying permission needed.

**Failure modes** — Cannot fail: with no recorder yet constructed it reports `armed=false, mode=none` and exits
successfully. An unreadable log directory yields `dirWritable=false` plus the reason string, not an error. Invoked
with a piped input it returns the explanatory `Failure` (Cupcake rule 29).

**Security and audit** — No secrets. Paths appear in output. Non-destructive; no confirmation.

**Traceability** — PRD **7.11**. **NEW.** The source had no status surface of any kind; its acceptance criterion 20
records that a totally dead logging subsystem was indistinguishable from a healthy one on every channel. This tool
exists to make that impossible, and is the "out-of-band health signal" the dossier explicitly recommends.

---

#### 8.3.3 `DIAG TAIL` — read entries out of the buffer and the rolling files

**Registration**

| Field | Value |
|---|---|
| Command | `TAIL` |
| Root command | `DIAG` |
| Description | `Emit the most recent diagnostic entries, one chunk per entry` |
| Prototype | `DIAG TAIL [<file>] [-n <count>] [-level <min>] [-grep <regex>] [-since <duration>] [-source buffer|file|both] [-format text|json|csv]` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `file` | ordered, `UsePipe = true` | `string` | no | *(unset — use `-source`)* | an existing file inside the log root, or an absolute path | The log file to read. When piped, this value arrives from the upstream chunk and is dropped from command-line parsing (ref-command §3.5). |
| `n` | named | `int` | no | `50` | `1` … `100000` | How many entries to emit, counted from the end. |
| `level` | named | `string` | no | `TRACE` | `TRACE` \| `DEBUG` \| `INFO` \| `WARN` \| `ERROR` \| `FATAL` | Minimum level. Entries whose level token is not in the vocabulary (engine-sourced tokens such as `Info`, `Warn`) are normalised case-insensitively; unrecognised tokens are treated as `INFO` and never dropped. |
| `grep` | named | `string` | no | *(none)* | a .NET regular expression; bounded to 1 000 characters | Keep only entries whose message matches. |
| `since` | named | `string` | no | *(none)* | `<n>s` \| `<n>m` \| `<n>h` \| `<n>d`, or an ISO-8601 instant | Keep only entries at or after this point. Compared in the recorder's configured timestamp mode. |
| `source` | named | `string` | no | `both` | `buffer` \| `file` \| `both` | `both` merges the un-flushed buffer with today's file in timestamp order — **this is the repair of QUIRK-4**, where the source's forced flushes meant the buffer a user read was almost always empty. |
| `format` | named | `string` | no | `text` | `text` \| `json` \| `csv` | `json` emits one JSON object per entry (`ts`, `level`, `origin`, `message`) and declares `ResultFormat.JSON`. |

**Pipeline behaviour** — **Both.**
*As a source* it overrides `Main` and emits **one chunk per entry**, so downstream filters see lines, not a blob.
*As a filter* each piped chunk is interpreted as **one log-file path**; the tool tails that file with the same
`-n` / `-level` / `-grep` / `-since` policy and emits that file's surviving entries. A chunk naming a file outside
the configured log root is refused with a per-chunk `Failure` and the pipeline continues. A chunk that is empty or
already a failure never reaches `HandlePipedChunk` at all (ref-command §3.3, §2.7).
Declares `ResultFormat.General` / `.JSON` / `.CSV` per `-format`.

**Environment interaction** — Reads `CHATDBG_DIAG_DIR`, `CHATDBG_DIAG_PATTERN`, `CHATDBG_DIAG_TIMESTAMPS`
(`storeDefault: false`). Persists nothing. No OS environment access.

**Failure modes**

| Situation | Behaviour |
|---|---|
| `-n` non-numeric or out of range | Clamped to `[1, 100000]` with a one-line `Warning:` chunk, then proceeds. |
| `-grep` is not a valid regex | Single `Failure`: `DIAG TAIL: '-grep <pattern>' is not a valid regular expression: <reason>`. Nothing is emitted. |
| Named file does not exist | `Failure`: `DIAG TAIL: no such log file '<path>'. Try 'DIAG FILES'.` |
| Named file is outside the log root and not absolute-and-explicit | `Failure`: refused, with the resolved root named. Path containment is checked component-wise via `Path.GetRelativePath`, not `StartsWith`, and link targets are resolved (ref-loader §10 step 2). |
| Log root missing / capture never armed | Emits from the buffer only; if that is empty, emits nothing and succeeds. (An empty success with empty `Output` is dropped by the host, ref-command §2.3.) |
| Zero arguments | Uses every default above — `-n 50`, `-source both`. Safe by construction (§8.3 preamble). |
| Downstream stage fails | The failure chunk propagates; `TAIL` neither retries nor swallows. Backpressure is the pipeline's bounded channel. |

**Security and audit** — No parameter carries a secret, but **output may**: log content is arbitrary text produced
by a native engine and by application code. Ingest-time redaction (§8.3.1) is the primary control; `DIAG TAIL`
applies it a second time on read for files written by an older build with redaction off. Audit records the
parameters, not the output. Non-destructive.

**Traceability** — PRD **7.11**. Descends from `LLamaSharpLogConfig.GetLogs()` (operation A4), which was public API
with **no user-facing surface anywhere in the product**. The filtering, merging and per-entry chunking are **NEW**.

---

#### 8.3.4 `DIAG RECORD` — write an application entry, and journal a pipeline

**Registration**

| Field | Value |
|---|---|
| Command | `RECORD` |
| Root command | `DIAG` |
| Description | `Record an application-level diagnostic entry; as a pipeline stage, journal every chunk that passes through` |
| Prototype | `DIAG RECORD [-level TRACE|DEBUG|INFO|WARN|ERROR|FATAL] [-tag <name>] [-quiet] <message…>` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `level` | named | `string` | no | `INFO` | `TRACE` \| `DEBUG` \| `INFO` \| `WARN` \| `ERROR` \| `FATAL` | Severity label. **Deliberate deviation:** the source accepted *any* free string as a level and its own two halves disagreed (`WARNING` in tests vs `WARN` in production, business rule 9). |
| `tag` | named | `string` | no | `app` | 1–32 characters of `[A-Za-z0-9_-]` | **NEW.** Origin marker written into the entry as `[<tag>]`, so a merged file can be attributed. Engine-sourced entries carry `[engine]`. |
| `quiet` | flag | `bool` | no | `false` | — | In a pipeline, swallow each chunk after recording it instead of passing it through. |
| `message` | suffix, `UsePipe = true` | `string` | no | *(none)* | all remaining tokens joined by single spaces into **one string** (ref-command §4.7) | The entry text. When piped, the message is the upstream chunk's `Output`. |

**Pipeline behaviour** — **Both, as a tee.** Non-piped it records one entry and emits a one-line confirmation.
Piped, `HandlePipedChunk` records `pipedChunk.Output` as one entry at `-level` with `-tag`, then re-emits the chunk
**verbatim** so the tool is transparent in the middle of a pipeline; with `-quiet` it returns
`CommandResult<string>.Success(string.Empty, OutputFormat)` — the host drops empty successes (ref-command §2.3), so
the stage becomes a pure sink, exactly the idiom `SET` uses to stay silent. `OnStartPipe` / `OnEndPipe` bracket the
run and write a `[diag] journal opened` / `… closed, N chunks` pair, giving every pipeline a delimited region in
the log. The re-emitted chunk keeps the upstream `CorrelationId` so a log entry and a pipeline stage can be joined.

**Environment interaction** — Reads `CHATDBG_DIAG_LEVEL`, `CHATDBG_DIAG_REDACT`. Persists its own chunk counter as
`RECORD_CHUNKS` in its private bucket (allowed without `modifiesEnvironment` because the key carries the command's
prefix, ref-command §7.3). No OS environment access.

**Failure modes**

| Situation | Behaviour |
|---|---|
| No message and no pipe | `Failure`: `DIAG RECORD: nothing to record. Usage: DIAG RECORD [-level INFO] <message…>` |
| `-level` outside the allow-list | Rejected at parse time; help names the six values. |
| Recorder in memory-only mode | Records normally, into memory. No error. |
| Recorder not yet armed | **Records anyway** — the buffer exists from construction; arming concerns only the engine sink. Preserves the source's behaviour, where application entries accumulated even when arming had failed. |
| A chunk cannot be recorded (buffer at hard cap) | The oldest entries are dropped, `entriesDropped` increments, and a single `[diag] N entries dropped (buffer cap)` marker is inserted. The chunk still passes through. **NEW** — the source grew without bound whenever file logging was off (business rule 13). |
| Downstream failure | Propagates unchanged; the journal entry has already been written. |

**Security and audit** — **The `message` suffix parameter can carry anything the user types**, including a pasted
secret, and the framework's `AuditMaskingConfiguration` only rewrites `-name=value` tokens, so a space-separated
message is **not masked in the audit record** (ref-command §11.4). Therefore: the audit metadata for `DIAG RECORD`
records the *message length and hash*, never the message text, and the help remarks say plainly "do not paste
secrets into `DIAG RECORD`". Ingest redaction still applies to the buffer copy. Non-destructive.

**Traceability** — PRD **7.11**. Descends from `LLamaSharpLogConfig.LogMessage(level, message)` (operation A3), which
in the source was callable only by the inference service. The user-facing verb and the pipeline-journal role are
**NEW**, and earn their place because they make every other package's output traceable through one log: without a
sink, a pipeline is unobservable.

---

#### 8.3.5 `DIAG FLUSH` — drain the buffer to the rolling daily file

**Registration**

| Field | Value |
|---|---|
| Command | `FLUSH` |
| Root command | `DIAG` |
| Description | `Append the buffered entries to today's rolling log file and empty the buffer` |
| Prototype | `DIAG FLUSH [-quiet]` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `quiet` | flag | `bool` | no | `false` | — | Suppress the confirmation line; still reports failures. |

**Pipeline behaviour** — **Neither.** One confirmation chunk:
`Flushed 412 entries (31 204 chars) to /home/u/.config/ChatDbg/Logs/chatdbg_20260828.log`. Piped, it returns the
explanatory `Failure`.

**Environment interaction** — Reads `CHATDBG_DIAG_DIR`, `CHATDBG_DIAG_FILE`, `CHATDBG_DIAG_PATTERN`. Writes nothing.

**Failure modes**

| Situation | Behaviour |
|---|---|
| File logging is **off** | No file is created or touched **and the buffer is not drained** — the source's exact behaviour (acceptance criterion 8). Reports `File sink is off; nothing flushed. 412 entries remain buffered.` **The rebuild adds the report**; the source was a silent no-op. |
| Buffer is empty | No file is created. Reports `Buffer empty; no file written.` (Source: acceptance criterion 14, a daily file exists only on days with at least one flushed entry.) |
| Directory missing, disk full, file locked, permission denied | **Never throws. The buffer is preserved**, exactly as in the source (the append throws before the clear, so nothing is lost, business rule / acceptance criterion 9). Returns a `Failure` naming the reason, and increments the fault counter so `DIAG STATUS` shows it. |
| Called concurrently with an engine-driven auto-flush | Serialised by the recorder's re-entrant buffer lock; append order is preserved. The auto-flush path re-enters the same lock from inside the locked append, so re-entrancy on the same thread is load-bearing (diagnostic-logging business rule 8). |

**Security and audit** — No secrets in parameters. Writes a file whose content may contain redacted log text.
**Semi-destructive** (it empties the buffer) but not *irreversible* — the content moves to durable storage, which is
the point — so no confirmation is required.

**Traceability** — PRD **7.11**. Direct descendant of `LLamaSharpLogConfig.FlushToFile()` (operation A6), including
its append-not-truncate semantics, its empty-buffer guard, and its preserve-on-failure guarantee. The user-facing
verb and the reporting are **NEW** (the source flushed only from five hard-coded points inside the inference
service).

---

#### 8.3.6 `DIAG EXPORT` — write a support snapshot

**Registration**

| Field | Value |
|---|---|
| Command | `EXPORT` |
| Root command | `DIAG` |
| Description | `Write diagnostic entries to a file suitable for attaching to a bug report` |
| Prototype | `DIAG EXPORT [<filepath>] [-source buffer|file|both|all] [-days <n>] [-append] [-force] [-redact on|off] [-format text|json]` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `filepath` | ordered | `string` | no (`IsRequired = false`, so the tool can print the source's usage text itself) | *(none)* | any writable path; parent directory created recursively; a bare file name resolves against the process working directory | Destination file. |
| `source` | named | `string` | no | `both` | `buffer` \| `file` \| `both` \| `all` | `both` = un-flushed buffer + today's file, merged in timestamp order. `all` = every retained daily file plus the buffer. **This is the repair of QUIRK-4**: in the source, export snapshotted only the buffer, which the forced flushes had almost always just emptied, so the ordinary post-generation export produced a 0-byte file. |
| `days` | named | `int` | no | `1` | `1` … `3650` | With `-source all`, how many days back to include. |
| `append` | flag | `bool` | no | `false` | — | Append instead of overwrite. **Deviation:** the source always truncated (acceptance criterion 17). |
| `force` | flag | `bool` | no | `false` | — | Required to overwrite an existing **non-empty** file. **Deviation:** the source overwrote unconditionally with no prompt and no extension check (business rule 11). |
| `redact` | named | `string`→bool | no | *(inherits `CHATDBG_DIAG_REDACT`, default `on`)* | `on` \| `off` | Re-apply masking on the way out. |
| `format` | named | `string` | no | `text` | `text` \| `json` | `text` reproduces the source's `[ts] [LEVEL] message` line shape; `json` writes one JSON object per line. |

**Pipeline behaviour** — **Accepts piped input; produces a one-line summary.** Piped, **one chunk means one line to
write**: `HandlePipedChunk` appends `pipedChunk.Output` to the destination and returns an empty success (swallowed
by the host), and `OnEndPipe` closes the file and the summary is emitted from the non-piped path's counterpart. This
makes `… | DIAG EXPORT ./bug.log` the universal "capture this pipeline to a file" sink. `-source` is ignored while
piped (the pipe *is* the source) and a `Warning:` chunk says so once.

**Environment interaction** — Reads `CHATDBG_DIAG_DIR`, `CHATDBG_DIAG_PATTERN`, `CHATDBG_DIAG_REDACT`. Writes
`EXPORT_LAST_PATH` into its own prefixed bucket so `DIAG STATUS -v` can report the last export. No OS environment
access.

**Failure modes**

| Situation | Behaviour |
|---|---|
| No path given | `Failure` whose message reproduces the source's three lines, adapted: `Usage: DIAG EXPORT <filepath>` / `Example: DIAG EXPORT chatdbg-debug.log` / `Tip: 'DIAG EXPORT -source all -days 7 bug.log' captures the whole week.` **Deviation from QUIRK-3:** the source returned *success* for a path it ignored and *failure* only for no path at all — an inversion its own tests locked in. Here, no path is a failure and a supplied path really writes. |
| Destination exists, non-empty, no `-append`/`-force` | `Failure`: `DIAG EXPORT: '<path>' already exists (14 KB). Use -append or -force.` |
| Nothing to write | Writes **no file** and returns `Success` with `Nothing to export (buffer empty, no matching files).` **Deviation:** the source wrote a 0-byte file unconditionally and told the caller nothing (QUIRK-18, acceptance criterion 18). |
| Parent directory cannot be created, disk full, permission denied | `Failure` naming the reason; fault counter increments. **Deviation:** the source swallowed every export failure and the caller could not distinguish success from failure (operation A7). |
| Path is a directory | `Failure`: `'<path>' is a directory.` (The source performed no such check.) |
| Downstream error arriving through the pipe | Failure chunks are forwarded verbatim by `AbstractCommand.Main` before `HandlePipedChunk` is reached; the destination file is still closed cleanly in `OnEndPipe`, and the summary records `wrote N lines, forwarded M upstream failures`. |

**Security and audit** — Output may contain log text; masking is applied unless explicitly disabled, and
`-redact off` is recorded in the audit event as a metadata flag so a reviewer can see that an unmasked export was
taken. Writing a file is a side effect but not irreversible destruction; overwriting an existing non-empty file
**is** treated as destructive and gated behind `-force`. Encoding is **UTF-8 with no byte-order mark**, matching
the source, because the whole support workflow is "grep this file and attach it".

**Traceability** — PRD **7.11**. Descends from the `export-logs` command (`Commands/ExportLogsCommand.cs`) — a stub
that validated argument count, ignored the path, wrote nothing, and was registered in neither shell — and from
`LLamaSharpLogConfig.SaveLogsToFile(path)` (operation A7), the only export path that actually did anything. This
tool is the resolution of the dossier's own open question: *"implement the documented behavior: resolve the active
recorder, and write the union of the buffered and already-flushed content to the requested path."*

---

#### 8.3.7 `DIAG CLEAR` — discard buffered entries

**Registration**

| Field | Value |
|---|---|
| Command | `CLEAR` |
| Root command | `DIAG` |
| Description | `Discard the in-memory diagnostic buffer without writing it anywhere` |
| Prototype | `DIAG CLEAR -force [-flush]` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `force` | flag | `bool` | no | `false` | — | **Required.** Without it the tool reports what *would* be discarded and does nothing. |
| `flush` | flag | `bool` | no | `false` | — | Flush to the daily file first, then clear — the non-destructive alternative, offered inline because it is almost always what the user meant. |

**Pipeline behaviour** — **Neither.** One confirmation chunk.

**Environment interaction** — Reads `CHATDBG_DIAG_FILE` (to decide whether `-flush` is even possible). Writes nothing.

**Failure modes** — Without `-force`: `Success` with
`Would discard 412 entries (31 204 chars). Re-run with -force, or use -force -flush to keep them.`
With `-flush` and a failing flush: nothing is cleared, a `Failure` reports the flush reason — content is never
discarded because a write failed.

**Security and audit** — **Destructive and irreversible**: discarded content is written nowhere, exactly as in the
source (operation A5). Hence the mandatory `-force`. Audited with the discarded entry count in metadata.

**Traceability** — PRD **7.11**. Descends from `LLamaSharpLogConfig.ClearLogs()` (operation A5). The confirmation
gate and the `-flush` alternative are **NEW**; the source's method discarded silently on the first call.

---

#### 8.3.8 `DIAG FILES` — enumerate the diagnostic artefacts

**Registration**

| Field | Value |
|---|---|
| Command | `FILES` |
| Root command | `DIAG` |
| Description | `List the rolling log files and the product's per-user state locations` |
| Prototype | `DIAG FILES [-days <n>] [-roots] [-format text|json|csv]` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `days` | named | `int` | no | `30` | `1` … `3650` | Include files whose day-stamp falls within this many days of today. |
| `roots` | flag | `bool` | no | `false` | — | Also emit the product's per-user state roots rather than only log files. |
| `format` | named | `string` | no | `text` | `text` \| `json` \| `csv` | Output shape; `csv` declares `ResultFormat.CSV`. |

**Pipeline behaviour** — **Produces piped output; accepts none.** Overrides `Main` to emit **one chunk per file**,
each chunk being the file's **absolute path** in `text` mode — chosen deliberately so the chunk is directly
consumable by `DIAG TAIL`, `DIAG ROTATE` and `DIAG EXPORT`. In `csv`/`json` mode each chunk additionally carries
`day`, `bytes`, `modifiedUtc`, `pattern` (`chatdbg` or legacy `llamasharp`).

With `-roots`, three extra chunks name the product's per-user roots and their writability. The source scattered
state across **three different roots under two naming conventions** — settings at `<user profile>/.ChatDbg`,
prompts at `<local app data>/ChatDbg/system_prompts`, logs at `<roaming app data>/ChatDbg/Logs`
(packaging QUIRK-17, diagnostic-logging QUIRK-8) — and documented none of them. This flag is the only place in the
rebuilt product that tells a user where their state actually lives.

**Environment interaction** — Reads `CHATDBG_DIAG_DIR`, `CHATDBG_DIAG_PATTERN`. Writes nothing.

**Failure modes** — Missing log root: emits nothing and succeeds (with `-roots`, still reports the root and
`exists=false`). Unreadable directory: one `Failure` naming the reason. Zero arguments: `-days 30`, no roots.

**Security and audit** — Paths only; no content is read. Non-destructive.

**Traceability** — PRD **7.11**, with a reporting borrow from **7.15 Packaging & Release** (the per-user roots a
packaged artefact writes into). **NEW** — the source had no enumeration surface and no documentation of the
locations on any platform other than Windows.

---

#### 8.3.9 `DIAG ROTATE` — retention, pruning and compaction

**Registration**

| Field | Value |
|---|---|
| Command | `ROTATE` |
| Root command | `DIAG` |
| Description | `Apply retention policy to the rolling log files: prune by age, cap total size, optionally compress` |
| Prototype | `DIAG ROTATE [<file>] [-days <n>] [-maxtotal <MiB>] [-maxfile <MiB>] [-compress none|gzip] [-apply] [-format text|csv]` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `file` | ordered, `UsePipe = true` | `string` | no | *(none — scan the log root)* | a path inside the configured log root | Act on one file. When piped, supplied by the upstream chunk. |
| `days` | named | `int` | no | `14` | `1` … `3650` | Delete daily files older than this many days. Today's file is never deleted. |
| `maxtotal` | named | `int` | no | `256` | `1` … `65536` MiB | Cap on the total size of the log root; oldest files are removed first until the cap is met. |
| `maxfile` | named | `int` | no | `64` | `1` … `4096` MiB | A single day's file above this size is rolled to `<name>.1`, `<name>.2`, … and a fresh file is started. |
| `compress` | named | `string` | no | `none` | `none` \| `gzip` | Compress files older than one day in place, to `<name>.gz`. `DIAG TAIL` and `DIAG EXPORT` transparently read `.gz`. |
| `apply` | flag | `bool` | no | `false` | — | **Required to modify anything.** Without it the tool is a dry run and every emitted chunk is prefixed `WOULD `. |
| `format` | named | `string` | no | `text` | `text` \| `csv` | Output shape. |

**Pipeline behaviour** — **Both.** As a source it scans the log root and emits one chunk per candidate action
(`WOULD DELETE …` / `DELETED …` / `COMPRESSED …` / `ROLLED …`). As a filter each piped chunk is one candidate file
path — so `DIAG FILES -days 365 | DIAG ROTATE -days 14 -apply` is the canonical retention pipeline. Chunks naming
paths outside the configured log root are refused per-chunk with a `Failure` and the pipeline continues.

**Environment interaction** — Registered `modifiesEnvironment: true` so `-days` / `-maxtotal` / `-maxfile` /
`-compress` persist as the global retention policy (`CHATDBG_DIAG_RETAIN_DAYS`, `CHATDBG_DIAG_MAX_TOTAL_MB`,
`CHATDBG_DIAG_MAX_FILE_MB`, `CHATDBG_DIAG_COMPRESS`), which the recorder consults after each flush so retention is
also enforced continuously rather than only when the verb is typed. `GetDefaultEnvironment()` seeds all four.

**Failure modes**

| Situation | Behaviour |
|---|---|
| No `-apply` | Dry run. Always the default, because this is the only tool in the package that deletes user data. |
| Path outside the log root | Refused with the resolved root named. Containment is checked component-wise after link resolution, never by `StartsWith` (ref-loader §10, step 2). |
| A file is locked or undeletable | Per-file `Failure`; the run continues to the next file and the summary reports `n deleted, m failed`. |
| Compression unavailable on the host | Reports `compress: unavailable` and falls back to `none` rather than failing the run. |
| Zero arguments | Dry-run scan with the defaults. Nothing is ever deleted by a bare invocation. |
| Downstream error | Propagated; already-applied deletions are not undone (and cannot be) — which is exactly why `-apply` is opt-in. |

**Security and audit** — **Destructive and irreversible.** Gated by `-apply`, confined to the configured log root,
and every applied action is written to the audit stream with the file path, size and age in metadata, and to the
recorder itself as a `WARN` entry so the log records its own pruning. No parameter carries a secret.

**Traceability** — PRD **7.11**. **NEW.** The source had **no retention policy of any kind**: "rotation" meant only
a date-stamped file name, with no size cap, no compression, no retention limit and no deletion of old files, and a
long-lived install grew unboundedly on disk (business rule 18, and an explicit "could not determine whether the
daily file was ever intended to be pruned"). This tool earns its place because a diagnostic subsystem that cannot
be bounded is one operators turn off.

---

#### 8.3.10 `DIAG DOCTOR` — environment health check

**Registration**

| Field | Value |
|---|---|
| Command | `DOCTOR` |
| Root command | `DIAG` |
| Description | `Check runtime, backend availability, native libraries, credential resolution and configuration validity` |
| Prototype | `DIAG DOCTOR [<check>] [-only <ids>] [-probe none|native|endpoints|all] [-severity info|warn|error] [-format text|json|csv] [-v]` |

```csharp
[CommandRoot("DIAG", "Diagnostics and observability")]
[CommandRegister("Doctor", "Check runtime, backend availability, native libraries, credential resolution and configuration validity",
    Prototype = "DIAG DOCTOR [<check>] [-only <ids>] [-probe none|native|endpoints|all] …", Version = "1.0.0")]
[CommandParameterOrdered("check", "Run a single check group", IsRequired = false, DefaultValue = "all",
    AllowedValues = new[] { "all", "runtime", "build", "backend", "native", "credentials", "config", "storage" },
    UsePipe = true)]
[CommandParameterNamed("probe", "Perform active probes as well as passive inspection",
    DefaultValue = "none", AllowedValues = new[] { "none", "native", "endpoints", "all" })]
[CommandParameterNamed("severity", "Minimum severity to report",
    DefaultValue = "info", AllowedValues = new[] { "info", "warn", "error" })]
[CommandParameterNamed("format", "Output shape", DefaultValue = "text",
    AllowedValues = new[] { "text", "json", "csv" })]
[CommandParameterNamed("only", "Comma-separated check ids to run")]
[CommandFlag("verbose", "Include the evidence behind each verdict", ShortAlias = "v")]
[CommandHelpRemarks("DOCTOR never prints a credential value. It reports which resolution tier would win.")]
[CommandHelpRemarks("'-probe native' loads the inference backend OUT OF PROCESS: a hard native crash cannot kill this shell.")]
public sealed class DoctorCommand : AbstractCommand { … }
```

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `check` | ordered, `UsePipe = true` | `string` | no | `all` | `all` \| `runtime` \| `build` \| `backend` \| `native` \| `credentials` \| `config` \| `storage` | Which group to run. Note `AllowedValues[0]` also auto-populates the default (ref-command §4.1). |
| `only` | named | `string` | no | *(none)* | comma-separated check ids, e.g. `NAT-002,CFG-004` | Run exactly these checks. |
| `probe` | named | `string` | no | `none` | `none` \| `native` \| `endpoints` \| `all` | Opt in to **active** probing. `none` is passive inspection only — no native load, no network. |
| `severity` | named | `string` | no | `info` | `info` \| `warn` \| `error` | Minimum severity emitted. |
| `format` | named | `string` | no | `text` | `text` \| `json` \| `csv` | `json` emits one object per check with `id`, `group`, `status`, `summary`, `evidence`, `remedy`; declares `ResultFormat.JSON`. |
| `verbose` | flag (`ShortAlias = "v"`) | `bool` | no | `false` | — | Include the evidence string and the remedy for passing checks too. |

**The checks.** Each emits one chunk with a status of `pass` / `warn` / `fail` / `skipped`.

*Group `runtime`*

| Id | Checks | Fails / warns when |
|---|---|---|
| `RUN-001` | Runtime version and TFM of the loaded product assemblies (`net10.0` expected — the source's four projects all targeted `net10.0`). | Major version differs from the built-against TFM. |
| `RUN-002` | OS, architecture, and the resolved runtime identifier (`win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`). | Reports `osx-*` as **supported-but-never-released**: the source documented macOS triples but built none (packaging P10, Q6). |
| `RUN-003` | Globalization mode. | `warn` when running invariant-only, because that changes string comparison and number/date formatting — the source forced it in `Compact` and `SingleFile` but not in Debug/Release, so the same source emitted two timestamp shapes and parsed `/set temperature 0.7` differently on a comma-decimal locale (QUIRK-13, settings Q15). |
| `RUN-004` | Working set and available memory against the configured `llamaContextSize`. | `warn` below the documented 8 GB build/run guidance. |

*Group `build`* (delegates its facts to, but does not depend on, `ChatDbg.Tools.Packaging`)

| Id | Checks | Fails / warns when |
|---|---|---|
| `BLD-001` | Assembly identity metadata present (version, title, company, product, copyright). | `warn` when absent — the source's `Compact` profile stripped exactly this metadata while `SingleFile` kept it, so a support engineer could not tell one `Compact` build from another (Q35, R19/R33). |
| `BLD-002` | Trimming / AOT / single-file status. | `warn` when trimmed, `error` when `TrimMode=full`: all of the product's persistence runs through reflection-driven JSON with no serializer context, no trim annotations and every trim diagnostic suppressed (Q13, R76/R77). |
| `BLD-003` | Stack-trace metadata and framework message resources. | `warn` when stack traces are stripped or framework messages are symbolic resource keys, because **every `ERROR` entry this package writes interpolates a whole exception** and degrades to an opaque key with no message and no frames in exactly the configurations users run (Q12/R21/R24). |
| `BLD-004` | Runtime-configuration side-car presence for a bundled build. | `error` when a bundled, JIT artefact was published without it — the suspected fatal defect in the profile that actually ships (R38/Q22/F15). |

*Group `backend`*

| Id | Checks |
|---|---|
| `BCK-001` | Configured provider (`azure` \| `bedrock` \| `llama`) and whether the package that implements it is loaded. |
| `BCK-002` | Azure: endpoint present and syntactically an HTTPS URL; deployment/model id non-empty. `-probe endpoints` adds a TLS reachability check (no credentials sent). |
| `BCK-003` | Bedrock: region present and in the known region list; model id non-empty. `-probe endpoints` resolves the regional endpoint host. |
| `BCK-004` | Local: model path set, file exists, extension is `.gguf`, size is plausible. **Reproduces the source's weakness explicitly**: its "is this configured?" check reported *configured* for **any existing file, including a zero-byte non-GGUF one** (local-inference acceptance criterion 2). `DOCTOR` reports `warn: model file is not a GGUF container (magic bytes 00 00 00 00)` — a header read, not a load. |

*Group `native`*

| Id | Checks |
|---|---|
| `NAT-001` | Presence of the backend native library for the resolved RID: `runtimes/win-x64/native/llama.dll`, `runtimes/linux-x64/native/libllama.so`, `runtimes/osx-{x64,arm64}/native/libllama.dylib`. Reports path, size, mtime. |
| `NAT-002` | **Both CPU and CUDA-12 backends present at once.** `warn` — the source referenced both unconditionally in the same project, and its own troubleshooting document names shipping both as a cause of native load failure and recommends keeping one (packaging P12, local-inference external-technology table). |
| `NAT-003` | Architecture of the native image vs the process architecture. `fail` on a mismatch. |
| `NAT-004` | Platform prerequisites: on Windows, the Microsoft C++ runtime redistributables; on Linux, `glibc` version and the presence of `libgomp`; on macOS, code-signing/quarantine attributes on the dylib. Each is a documented cause of the `0xC0000005`-class failure this package exists to diagnose. |
| `NAT-005` | CUDA availability when the CUDA backend is present: driver version and device count, read from the driver API without initialising a context. `skipped` on hosts with no NVIDIA driver — not a failure. |
| `NAT-006` | **Active load probe.** `skipped` unless `-probe native`. Runs **out of process**: a helper process loads the backend and the model header and reports back. A hard native access violation kills the helper, and `DOCTOR` reports `fail: native load crashed the probe process (exit 0xC0000005) — see the daily log`. **This is the single most valuable check in the package**, because in the source that crash killed the shell and left only whatever had already been flushed. |

*Group `credentials`* — **presence and provenance only, never values**

| Id | Checks |
|---|---|
| `CRD-001` | The resolution tiers, in the source's exact order: **environment variables → OS credential vault → settings-file field**. Reports which tier *would* win for each of the three credentials. |
| `CRD-002` | Environment tier, by declared order, first non-empty wins, empty string treated as absent: `CHATDBG_AZURE_API_KEY`; `CHATDBG_AWS_ACCESS_KEY` then `AWS_ACCESS_KEY_ID`; `CHATDBG_AWS_SECRET_KEY` then `AWS_SECRET_ACCESS_KEY`. Output is `set` / `unset` and the winning variable **name**. |
| `CRD-003` | Vault tier: on Windows, existence of `ChatDbg:AzureApiKey` / `ChatDbg:AwsAccessKey` / `ChatDbg:AwsSecretKey`. On macOS/Linux: `unavailable — no OS keystore integration on this platform; available tiers are environment variables and the settings file`. **Improvement on the source's probe**, which was a bare "is the OS Windows" test that never asked whether the vault service actually answers, so a Windows host with the credential service disabled by policy looked healthy and failed later with a generic message. `DOCTOR` performs a real read-of-a-nonexistent-name probe and distinguishes *available* from *present-but-refusing*. |
| `CRD-004` | **Plaintext-in-settings detection.** `warn` when any of `azureApiKey` / `awsAccessKey` / `awsSecretKey` is non-empty in the settings file, with the remedy naming the environment variables — **and without printing the value**. The source's migration wizard interpolated the actual plaintext secret into `set CHATDBG_…=<value>` lines and did so on **every settings load** (credential Q3). This package will not reproduce that. |
| `CRD-005` | **Half a key pair.** `warn` when an AWS access key resolves but its secret does not, or vice versa — the source declared itself configured in that state (credential acceptance criterion 24). |

*Group `config`* — validated against the product's real ranges

| Id | Setting | Valid range (inclusive) |
|---|---|---|
| `CFG-001` | `temperature` | `0.0` … `2.0` |
| `CFG-002` | `maxTokens` | `1` … `8192` |
| `CFG-003` | `logProbabilitiesTopK` | `1` … `20` |
| `CFG-004` | `gridViewMaxAlternatives` | `1` … `20` |
| `CFG-005` | `llamaContextSize` | `512` … `32768` |
| `CFG-006` | `llamaGpuLayerCount` | `0` … `100` |
| `CFG-007` | `llamaThreads` | `0` … `64` |
| `CFG-008` | `llamaBatchSize` | `1` … `2048` |
| `CFG-009` | `provider` | `azure` \| `bedrock` \| `llama` |
| `CFG-010` | Inert-setting warnings | `warn`: `llamaGpuDevice`, `llamaThreads` and `llamaBatchSize` are validated, clamped, persisted and displayed but **reach no runtime** (settings Q6); `warn`: `maxTokens` is **not sent by the Azure path** unless log probabilities are on (settings Q32). Reporting a setting that silently does nothing is exactly the doctor's job. |
| `CFG-011` | Culture sensitivity of the persisted document | `warn` when the settings file was written under a comma-decimal locale by a non-invariant build and would fail to re-parse under the shipped invariant build (settings Q15). |

*Group `storage`*

| Id | Checks |
|---|---|
| `STO-001` | The three per-user roots exist and are writable: settings `<user profile>/.ChatDbg`, prompts `<local app data>/ChatDbg/system_prompts`, logs `<roaming app data>/ChatDbg/Logs`. |
| `STO-002` | `fail` when the app-data root resolves to the **empty string** (no `HOME` on a Unix-like host), which in the source silently turned the log directory into the *relative* path `ChatDbg/Logs` under the process working directory with nothing guarding it. The rebuild refuses that and names a deterministic fallback. |
| `STO-003` | `warn` when the settings store has fallen back to the temporary directory — the source did that inside a bare catch-all, so on a locked-down or service account settings appeared to save and then vanished (packaging Q18). |
| `STO-004` | Free space in the log root against the retention cap. |

**Pipeline behaviour** — **Both.** As a source it overrides `Main` and emits **one chunk per check**, so
`DIAG DOCTOR | REGIF "fail"` is a working health gate. As a filter, **one piped chunk means one check id or group
name**, so a check list can be generated elsewhere and fed in. With `-format json` it emits one JSON object per
check and declares `ResultFormat.JSON`; with `-format csv`, a header chunk then one row per check
(`ResultFormat.CSV`).

**Environment interaction** — Reads the `CHATDBG_DIAG_*` shell globals for its own configuration, and reads the
**OS process environment** (a different store from `IEnvironmentContext`) for credential-variable *presence*:
`CHATDBG_AZURE_API_KEY`, `CHATDBG_AWS_ACCESS_KEY`, `AWS_ACCESS_KEY_ID`, `CHATDBG_AWS_SECRET_KEY`,
`AWS_SECRET_ACCESS_KEY`. It **never writes an OS environment variable** and needs no environment-modifying
permission. It writes `DOCTOR_LAST_RUN` and `DOCTOR_LAST_STATUS` into its own prefixed bucket so the shell can show
a health badge without re-running the checks.

**Failure modes**

| Situation | Behaviour |
|---|---|
| A single check throws | That check reports `fail` with the exception message as evidence; **the run continues**. A doctor that dies on its first sick patient is useless. |
| `-only` names an unknown id | `Warning:` chunk listing the valid ids, then runs the rest. |
| `-probe native` refused by host policy or unavailable | `skipped (policy)` — never a failure. |
| `-probe endpoints` with no network | `NET unreachable` reported as `warn`, not `fail`: an offline machine running the local provider is a perfectly healthy machine. |
| Settings file absent | `config` group reports `pass — no settings file; built-in defaults apply` and lists them (`provider azure`, `modelId gpt-4`, `temperature 0.7`, `maxTokens 1000`, `awsRegion us-east-1`, `systemPromptName default`, log probabilities off, `logProbabilitiesTopK 5`, `llamaContextSize 4096`, `llamaGpuLayerCount 0`, `llamaThreads 0`, `llamaBatchSize 512`). |
| Zero arguments | Runs `all` at `severity info` with `probe none`. Safe, read-only, offline. |
| Downstream error | Propagated; the doctor does not retry. |

**Security and audit** — **This is the tool most likely to leak a secret, and it is specified never to.** No check
reads secret material: environment checks read *names* and test for emptiness; the vault check tests for entry
existence; the settings check tests for a non-empty field. Output carries `set` / `unset` / `present` and a
**source label** (`environment variable (AWS_ACCESS_KEY_ID)`, `OS credential vault`, `settings file (deprecated)`),
never a value, never a prefix, never a hash. Audit metadata records the check ids run and the aggregate verdict.
The tool is entirely **read-only** except for `-probe native`, which spawns a short-lived helper process — that is
recorded in the audit event and is the only reason a host might want to withhold it.

**Traceability** — PRD **7.11 Diagnostic Logging**, drawing evidence owned by **7.2 Settings & Configuration**,
**7.3 Credential Management**, **7.6/7.8 Provider Abstraction & Local Inference**, and **7.15 Packaging & Release**.
**NEW as a command.** In the source these facts existed only as scattered one-line startup prints (`Azure credentials
loaded from: …`, `Warning: … service is not configured`), a four-cause error string pasted into a chat reply after
a model-load failure, and a 300-line hand-written troubleshooting document for a native access violation. This tool
earns its place by turning that document into an executable check.

---

#### 8.3.11 `DIAG AUDIT` — configure the audit stream

**Registration**

| Field | Value |
|---|---|
| Command | `AUDIT` |
| Root command | `DIAG` |
| Description | `Configure structured command-execution auditing and its redaction policy` |
| Prototype | `DIAG AUDIT [on|off|status] [-sink log|stdout|file] [-path <file>] [-redact <names>] [-placeholder <text>]` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `state` | ordered | `string` | no | `status` | `on` \| `off` \| `status` | Enable, disable, or report. |
| `sink` | named | `string` | no | `log` | `log` \| `stdout` \| `file` | `log` routes audit events into this package's recorder (so they land in the daily file); `stdout` matches the framework's shipped `StructuredAuditLogger` default; `file` writes JSON lines to `-path`. |
| `path` | named | `string` | no | `<log root>/audit_{0:yyyyMMdd}.jsonl` | any writable path | Destination when `-sink file`. |
| `redact` | named | `string` | no | `password,pwd,secret,token,apikey,api_key,connectionstring,conn,credential` | comma-separated names | Parameter and variable names whose values are masked. Default reproduces the framework's `AuditMaskingConfiguration` defaults exactly. |
| `placeholder` | named | `string` | no | `[REDACTED]` | 1–32 characters | Replacement text. Matches the framework default. |

**Pipeline behaviour** — **Neither.** One chunk: a confirmation, or, for `status`, a short report including the
honest caveat below.

**Environment interaction** — Registered `modifiesEnvironment: true`; writes `CHATDBG_DIAG_AUDIT`,
`CHATDBG_DIAG_AUDIT_SINK`, `CHATDBG_DIAG_AUDIT_PATH`. The audit logger itself is installed by the **host** via
`IEnvironmentContext.SetAuditLogger` / `CommandController.AuditLogger`; this tool sets policy, and the host reads
it at the next dispatch. Where the host refuses (audit locked by policy), the tool reports `refused by host policy`
rather than failing.

**Failure modes** — Unwritable `-path`: `Failure`, and auditing stays in its previous state rather than silently
degrading to nothing. `status` with no audit logger installed: `Success` with `auditing: not installed by host`.

**Security and audit** — This tool configures the very mechanism that records it, so its own event is emitted
**after** the change with the before/after policy in metadata. It must state, in `status` output and in its help
remarks, the framework's real limitation: **`AuditMaskingConfiguration` only rewrites `-name=value` /
`--name=value` tokens; the framework's own space-separated `-name value` form is not masked, and the argument
tokenizer strips `=` anyway** (ref-command §11.4). The honest consequence, printed verbatim by `DIAG AUDIT status`,
is: *"Parameter masking is unreliable for this framework's syntax — never pass a secret as a command parameter."*
Variable-name-based redaction on the `EnvironmentChange` path **does** work and is reported as such.

**Traceability** — PRD **7.11**, with a boundary to **7.1 Command System & Dispatch** (the host owns installing the
logger; this tool owns the policy). **NEW.** The source had no audit trail at all. It earns its place because this
rebuild's tools are dynamically loaded from a plugin directory, and a plugin host with no record of what ran is not
defensible.

---

#### 8.3.12 `DIAG BUILDINFO` — identify the running artefact

**Registration**

| Field | Value |
|---|---|
| Command | `BUILDINFO` |
| Root command | `DIAG` |
| Description | `Report the identity and publish profile of the running artefact` |
| Prototype | `DIAG BUILDINFO [-format text|json|csv] [-packages]` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text` \| `json` \| `csv` | Output shape. |
| `packages` | flag | `bool` | no | `false` | — | Also list every loaded tool package: name, version, origin path, and whether it was integrity-verified at load. |

**Pipeline behaviour** — **Produces piped output; accepts none.** Overrides `Main` to emit one chunk per fact:
product version, informational version, commit id when present, TFM, RID, publish profile inference
(`Debug` / `Release` / `Compact`-like / `SingleFile`-like, deduced from AOT, trim, single-file, invariant-globalization
and resource-key switches), globalization mode, stack-trace metadata availability, and the loaded-package inventory.

**Environment interaction** — None written. Reads no OS environment variables.

**Failure modes** — Metadata stripped (the source's `Compact` profile removed assembly identity entirely, Q35): each
missing fact reports `unknown (metadata stripped)` rather than failing. A package whose origin path cannot be read
reports `origin: unavailable`.

**Security and audit** — Emits file paths of loaded packages, which is provenance information a support engineer
needs and an attacker gains little from. Read-only; no confirmation.

**Traceability** — PRD **7.15 Packaging & Release** (reporting side only) with a home in **7.11**. **NEW.** The
source declared its version in **three** independent, unreconciled places — two project files and a hard-coded
`ChatDbg v1.0` literal in the GUI about box (Q34/R81) — and the profile that shipped stripped the metadata that
would have distinguished builds. This tool earns its place because every bug report this package's exports feed
into needs to name the artefact that produced it.

---

### 8.4 Host registration requirements

Three tools must be registered with `modifiesEnvironment: true`, because `ModifiesEnvironment` is a property of the
**registration**, not an attribute, and without it a command can persist only keys carrying its own command-name
prefix (ref-command §7.3):

```csharp
controller.AddCommand("ChatDbg.Diagnostics", new CaptureCommand(recorder), modifiesEnvironment: true);
controller.AddCommand("ChatDbg.Diagnostics", new RotateCommand(recorder),  modifiesEnvironment: true);
controller.AddCommand("ChatDbg.Diagnostics", new AuditCommand(),           modifiesEnvironment: true);
// every other DIAG tool is registered without it
```

The recorder is a **DI singleton** (`services.AddSingleton<IDiagnosticRecorder, DiagnosticRecorder>()`), and the
tools that need it are registered as singletons too — otherwise `CommandFactory` constructs a fresh instance per
execution and, since `CommandExecutor` **does not dispose the instance it executes** (ref-command §2.6), a recorder
owned by a command instance would be orphaned with its buffer un-flushed. That is precisely the source's defect:
its inference service's finalizer skipped recorder teardown, so a GC-reclaimed service lost its entire buffer with
no final flush (diagnostic-logging business rule 22).

---

### 8.5 Pipeline compositions

**1. Assemble a support bundle for a bug report** *(within the package)*

```
DIAG FILES -days 7 | DIAG TAIL -level WARN -n 500 | DIAG EXPORT ./chatdbg-support.log -append
```

`FILES` emits one absolute path per daily file in the last week; `TAIL` opens each and emits only `WARN`/`ERROR`/`FATAL`
entries, newest 500 per file; `EXPORT` writes each surviving entry as a line and finishes with
`Wrote 1 284 lines from 6 files to /home/u/chatdbg-support.log`. The user gets exactly the artefact the source's
documentation promised and its code never produced.

**2. Gate a session on a healthy environment** *(crosses into the framework built-ins)*

```
DIAG DOCTOR -severity error -format text | REGIF "^fail" | SAY
```

`DOCTOR` emits one chunk per check; the built-in `REGIF` filters to failing rows by returning an empty success for
non-matches (which the host drops, ref-command §3.3); `SAY` prints what survives. Nothing printed means nothing
failed — a usable exit condition for a startup script.

**3. Journal a chat turn and its diagnostics together** *(crosses into `ChatDbg.Tools.Inference`)*

```
LLM CHAT "why did the model load fail?" | DIAG RECORD -level INFO -tag chat
```

`LLM CHAT` streams the assistant's reply chunk by chunk; `DIAG RECORD` writes each chunk into the diagnostic buffer
tagged `[chat]` **and re-emits it verbatim**, so the user still sees the reply while the transcript and the engine's
own native log lines end up interleaved, in timestamp order, in the same daily file. `OnStartPipe`/`OnEndPipe`
bracket the turn with `journal opened` / `journal closed, N chunks`.

**4. Mine the per-token diagnostic firehose** *(crosses into `ChatDbg.Tools.TokenAnalysis`)*

```
DIAG TAIL -source all -grep "^Token [0-9]+: " -n 20000 -format json | TOKEN HISTOGRAM -bins 32
```

The source wrote one `DEBUG Token n: '<text>'` entry per generated token on the hot loop whenever probability
capture was on — the highest-volume producer in the product, and previously unreachable. `TAIL` emits them as JSON
objects, one chunk per entry; `TOKEN HISTOGRAM` consumes the stream and renders a distribution. Streaming is real,
so the histogram starts filling before the tail finishes.

**5. Enforce retention without ever risking a wrong delete**

```
DIAG FILES -days 365 | DIAG ROTATE -days 14 -maxtotal 256          # dry run: every line reads "WOULD DELETE …"
DIAG FILES -days 365 | DIAG ROTATE -days 14 -maxtotal 256 -apply   # same plan, applied
```

The same pipeline is run twice — once to read the plan, once to execute it. `ROTATE` refuses any chunk naming a path
outside the configured log root, so a mis-generated upstream cannot direct it at arbitrary files.

**6. Prove a build is safe to trust, and file the proof** *(crosses into `ChatDbg.Tools.Packaging`)*

```
DIAG BUILDINFO -packages -format json | DIAG RECORD -level INFO -tag build -quiet ; DIAG FLUSH
```

Every fact about the running artefact and every loaded tool package is written into the diagnostic log and nothing
is echoed to the terminal (`-quiet` swallows the chunks); `DIAG FLUSH` puts it on disk immediately. Every subsequent
export therefore carries the artefact identity the source's shipping profile had stripped from the binary.

---

### 8.6 Design notes for the architect

**What this package holds.** Exactly one piece of long-lived state: the **recorder** — a buffer, its sink switches,
its policy (threshold, minimum level, timestamp mode, redaction, retention), an armed latch, fault counters, and a
re-entrant lock. Nothing else. The lock's re-entrancy is load-bearing and must be asserted by a test: the
threshold-triggered auto-flush is invoked from *inside* the locked append, so a non-reentrant lock deadlocks the
engine's callback thread (diagnostic-logging business rule 8).

**What this package must not hold.** No model, no inference context, no provider client, no HTTP handler, no
credential value, no settings record, no chat history, no rendering state. `DIAG DOCTOR` reads settings and
credential *provenance* through read-only ports supplied by the host — it does not link against
`ChatDbg.Tools.Configuration` or `ChatDbg.Tools.Credentials`, because a tool package must depend on the SDK only
(Cupcake rule 4). If those ports are not supplied, the affected check groups report `skipped (no provider)` and the
rest of the doctor still runs.

**Ownership of the process-global engine hook.** The native engine's log-sink registration is process-global,
set-only, and last-writer-wins. Exactly one recorder may own it, ownership is recorded, and a second claimant is
refused unless forced. The source silently allowed a later recorder to steal the hook while earlier recorders
continued to believe themselves armed (business rule 21). Equally: the source's disposal path **never unregistered
the callback**, so engine lines kept accumulating into a buffer that would never be flushed again (business rule 20).
The rebuilt recorder unregisters on disposal where the binding permits, and where it does not, it swaps the sink's
target to a no-op sentinel so the abandoned buffer can be collected.

**Testability.** Four seams, all constructor-injected, all with in-memory fakes:
`IClock` (so timestamp shape, day rollover and `-since` are deterministic), `IFileStore` (append, overwrite, exists,
enumerate, delete — so the flush-failure, missing-directory, empty-buffer and retention branches become testable),
`IEngineSink` (so arm/re-arm/steal and the callback path can be exercised with no native library present), and
`IEnvironmentProbe` (OS environment, special folders, RID, runtime switches — so the doctor's every verdict is
reproducible on one machine). This is a direct response to the source's testing posture: **two of its seven recorder
tests asserted nothing at all**, the arm-idempotency test structurally could not reach the armed path because the
native sink registration was swallowed on a host with no native library, and **every failure branch was unreached**
(QUIRK-15, QUIRK-16). The rebuilt equivalents of acceptance criteria 6, 7, 10, 11, 18, 19 and 24 must be real,
asserting tests. Tests must not write into the real temp directory and leave files behind (QUIRK-9).

**When a capability is unavailable — degrade, never fail.** The rule is uniform: *report the degradation in-band and
carry on.*

| Missing capability | Behaviour |
|---|---|
| Native inference engine (any OS) | Capture arms in **application-entries-only** mode; `STATUS` says `engineSink: unavailable (<reason>)`; `DOCTOR` `native` group reports it as `fail` with the platform-specific remedy. Chat with the hosted providers is unaffected. |
| Writable log root | **Memory-only** mode with a bounded buffer that drops oldest and counts drops. `FLUSH` and `EXPORT -source file` report the reason. The product still runs. |
| OS credential keystore (Linux, macOS, or Windows with the service disabled by policy) | `DOCTOR` reports the tier as `unavailable` and names the tiers that remain (environment variables, settings file), rather than pretending Windows-ness implies a working vault. Neither `CAPTURE` nor any other verb is affected. |
| CUDA / GPU | `skipped`, never `fail`. CPU inference is a first-class configuration. |
| Compression | `ROTATE -compress gzip` degrades to `none` with a warning; retention by age and total size still applies. |
| Network | `-probe endpoints` reports `unreachable` as `warn`. An offline machine running the local provider is healthy. |
| Audit logger not installed by the host | `DIAG AUDIT status` says so; no other tool changes behaviour. |
| Host DI container absent | The recorder falls back to a package-scoped process singleton; `STATUS` reports `recorderOwner: package-static` so the difference is visible rather than mysterious. |

**Cross-platform posture, stated because the source's was not.** Nothing in the source's logging code was
Windows-only — every primitive it used was portable — but its *packaging and documentation* were: both shells
defaulted the runtime identifier to `win-x64`, every documented log path was `%APPDATA%\ChatDbg\Logs`, and the only
configuration example used the literal `C:\Logs\LLamaSharp`. This package resolves paths through platform-neutral
well-known-folder lookups on every OS, **refuses the empty-root case** rather than silently writing a
working-directory-relative `ChatDbg/Logs` when `HOME` is unset, names native libraries per platform
(`llama.dll` / `libllama.so` / `libllama.dylib`), and reports `osx-x64` / `osx-arm64` as first-class RIDs even
though the source never built one.

**Where to degrade rather than fail, in one sentence each.**
Arming with an unwritable directory → memory-only, not un-armed.
Flush with the sink off → report and retain, not silently retain.
Export with nothing to write → say so and write no file, not a 0-byte file.
A single doctor check that throws → mark that check failed, not the run.
A rotation candidate that is locked → skip it and report, not abort the sweep.
A piped chunk naming an out-of-root path → refuse that chunk, not the pipeline.
And, above everything: **a diagnostic subsystem that has failed must say so on a channel that exists in the shipping
build** — the one lesson the source's own acceptance criteria record as its worst defect.
