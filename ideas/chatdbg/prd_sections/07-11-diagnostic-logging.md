### 7.11 Diagnostic Logging & Log Export

**Description**

The product can answer a user's chat turn using one of three back-ends: two hosted network services and one **local model executed inside the product's own process**. The local path is the fragile one — it maps a multi-gigabyte model file into memory, may offload work to a graphics processor, and drives a native compiled engine that can fail in ways that produce no catchable error at all: hard process termination, silent fallback from GPU to CPU, memory exhaustion, and unreadable or incompatible model files. When that happens, the user is left with a single line of chat text and nothing to send to whoever maintains the tool.

Diagnostic Logging exists to leave a durable trail behind. It is a small in-process recorder that does two things: it installs itself as the destination for every diagnostic line the native inference engine emits about itself, and it accepts a running commentary from the application about what it just asked the engine to do — model load started, model file is this many megabytes, context created, generation finished in this many milliseconds, this token was emitted, this failed. Everything lands in one in-memory text buffer as timestamped, severity-labelled lines, and that buffer is periodically and deliberately pushed onto a plain-text file on disk, one file per calendar day, at a well-known per-user location. The product's own troubleshooting documentation tells a user hitting a native crash to go find that file and attach it to a bug report — and that workflow works, because the recorder is deliberately forced to disk immediately before and after every hazardous operation, so the evidence is already written when the process dies.

The feature is entirely automatic and has **no runtime configuration surface whatsoever**. The end user never turns it on, never picks a directory, and never sets a level. It arms itself the first time a message is sent to the local model and stays armed for the process lifetime. Alongside the file trail there is a second, non-destructive operation — writing a snapshot of the current buffer to a file path chosen by the caller — and a user-facing `export-logs` command that was *intended* to expose that operation. At the source commit the command is a stub: it validates only its argument count, writes no file, ignores the path it was given, and is not registered with either shell, so typing it produces "unknown command". Its exact strings and behaviour are nonetheless part of the observable contract and are specified below. The whole feature is a strict sink: nothing in the product ever reads the log files back.

*A note on literals.* Several file-name patterns and message texts below contain the token `LLamaSharp`. In this product that token is not a framework reference — it is literal text baked into file names and log lines that a reader of the artifact will see. Those literals are reproduced verbatim because they are part of the output contract; the implementation technology behind them is named only in **External technology**.

---

**User stories**

- **US-11.1** — As a developer using the chat shell against a locally-executed model, I want the product to capture diagnostics automatically without my configuring anything, so that when local inference misbehaves there is evidence on disk instead of only a one-line error in my chat transcript.

- **US-11.2** — As a support engineer receiving a bug report, I want a per-day, plain-text, human-readable log file at one predictable per-user location, so that I can tell any reporter exactly which file to attach without walking them through settings.

- **US-11.3** — As the local-inference subsystem, I want to record labelled progress and failure entries at every stage of model load, context creation and generation, and to force the accumulated entries onto durable storage at each hazard point, so that a native crash that kills the process outright cannot take the evidence with it.

- **US-11.4** — As a developer running the product under a debugger, I want every captured entry echoed live to the platform's attachable debug output stream, so that I can watch model loading and token generation as they happen without opening a file.

- **US-11.5** — As the local-inference subsystem, I want a non-destructive operation that writes the currently-buffered diagnostics to a file path I name, so that a snapshot can be taken on demand without disturbing the ongoing capture.

- **US-11.6** — As a user of the chat shell, I want to ask the product to export its captured diagnostics to a file path I name, so that I can attach them to a bug report without hunting for the daily file. **Status at the source commit: unfulfilled.** The command object exists with fixed text, writes nothing, and is registered with neither shell. Specified here as observed; see UC-11.E, FR-11.44 to FR-11.51 and QUIRK-11.1 to QUIRK-11.3.

- **US-11.7** — As a user of the chat shell, I want diagnostic capture never to interrupt, slow or fail my chat session, so that a logging problem is never a chat problem. Every operation in this feature swallows its own errors and returns nothing.

---

**Use cases**

#### UC-11.A — Automatic capture across one local generation request *(realizes US-11.1, US-11.3, US-11.4)*

**Preconditions**
- The active provider is the locally-executed model.
- A diagnostic recorder instance exists, owned solely by the local-inference subsystem, created when that subsystem was created.
- The recorder's defaults are in force: file capture on, debug-stream echo on, console echo off, buffer threshold 10000 characters, log directory = per-user roaming application-data root + `ChatDbg` + `Logs`.

**Main flow**
1. The user sends a chat turn.
2. The inference subsystem acquires its process-wide single-generation gate (only one generation runs at a time).
3. **Arm capture.** On the first request only: the log directory is created recursively if file capture is on and it does not exist; the recorder installs itself as the engine's process-global diagnostic destination; the armed latch is set; the entry `LLamaSharp logging configured successfully` is recorded at level `INFO`. On every later request this step is an immediate no-op.
4. Record `Starting message generation` at level `INFO`.
5. Model initialization runs behind a second single-permit gate. If the configured model file differs from the one already loaded, the previous model and context are torn down and a reload begins, recording in order at level `INFO`: `Loading LLamaSharp model from {model path}` → `Model file size: {N} MB` (whole megabytes, the file's byte length divided by 1048576) → `Model params: ContextSize={n}, GpuLayers={n}` → `Calling LLamaWeights.LoadFromFile...` → `Model weights loaded successfully` → `Creating context...` → `Context created successfully` → `LLamaSharp initialization complete`.
6. Record the routing decision at level `INFO`: `Using sampling pipeline with token probability capture mode` when token-probability capture is active, otherwise `Using standard ChatSession mode`.
7. Record `Processing user message with {N} characters` at level `INFO`.
8. In the probability-capture path only, record `Starting token-by-token generation with sampling pipeline probability capture` at level `INFO` before the generation loop.
9. Generation runs. In the probability-capture path only, record one entry per emitted token at level `DEBUG`: `Token {ordinal}: '{token text}'`.
10. Record `Generation complete in {N}ms, generated {N} tokens` at level `INFO`.
11. **Force a flush.** The whole buffer is appended to today's rolling file and the buffer is emptied.
12. Release the generation gate; the response is returned to the shell.

**Alternate flows**
- **A1 — Capture already armed.** Step 3 returns immediately and records nothing; the confirmation entry appears only once per process.
- **A2 — Model already loaded and unchanged.** Step 5 records nothing; loading is skipped entirely.
- **A3 — Plain generation path.** Steps 8 and 9 do not occur; there are no per-token entries at all.
- **A4 — Engine diagnostics arrive concurrently.** The engine emits its own lines on its own threads at any time; each is formatted and appended to the same buffer under the same exclusion lock, interleaved with application entries in true production order.
- **A5 — Buffer threshold crossed by engine output.** When an engine line pushes the buffer's character length strictly above 10000 and file capture is on, that append is immediately followed — still holding the lock — by an append-and-drain to today's rolling file.

**Error flows**
- **E1 — Log directory cannot be created while arming** (permission denied, read-only volume, invalid path). The failure is swallowed. One line `Failed to configure LLamaSharp logging: {exception}` goes to the debug stream and nothing else happens. The recorder is left **un-armed**: the engine destination was never installed, so **no engine diagnostics are captured for the remainder of the process run**, and because the directory does not exist every subsequent flush also fails silently. The user sees nothing; chat continues normally.
- **E2 — Installing the engine diagnostic destination fails** (native library absent or unloadable). Identical handling to E1 — same swallow, same message, recorder left un-armed.
- **E3 — Model weights fail to load.** Two entries are recorded at level `ERROR`: `Failed to load model weights: {full exception}` then `Model loading failed: {full exception}`; a flush is forced; the failure is re-raised. The user receives a chat response beginning `Error running local LLM:` naming four suspected causes (incompatible model file format, missing or incompatible native libraries, insufficient memory, runtime-version incompatibility).
- **E4 — Context creation fails.** `Context creation failed: {full exception}` at level `ERROR`, a forced flush, then the failure is re-raised wrapped as `Failed to create context: {message}`.
- **E5 — Per-token candidate computation fails.** One entry at level `WARN`: `Failed to compute candidates from logits: {exception message}`. Generation continues; that step simply has no alternatives recorded.
- **E6 — Any other failure inside the request.** One entry at level `ERROR`: `Error in LLamaSharpService: {full exception}`; a forced flush; an echo to the debug stream; a non-raising error response returned to the shell. The buffer is empty afterwards.
- **E7 — Flush to the rolling file fails** (directory missing, disk full, file locked, permission denied). Swallowed. One line `Failed to flush logs to file: {exception}` to the debug stream. **The buffer is not drained**, so nothing is lost and a later successful flush still emits it.
- **E8 — The native engine terminates the process outright.** Nothing is catchable. Everything already flushed survives on disk; everything still buffered is lost. This is the reason forced flushes bracket model load, context creation and every error.
- **E9 — The build in use does not carry the diagnostic-build compilation symbol** (the shipped configurations). Every internal-failure report above, and the debug-stream echo itself, is removed at compile time. E1, E2, E7 and a failed snapshot export then produce **no observable signal on any channel** — a completely dead logging subsystem is indistinguishable from a healthy one.

**Postconditions**
- On success: today's rolling file contains, in production order, every entry produced by the request, and the in-memory buffer is empty.
- On E1/E2: the buffer holds only application entries, unflushed and unflushable; no rolling file exists.
- On E3/E4/E6: the failure entries are on disk before control returns to the shell.

---

#### UC-11.B — Support engineer collects the trail *(realizes US-11.2)*

**Preconditions**
- The reporter has run at least one local generation that produced at least one flush on the day in question.

**Main flow**
1. The support engineer asks the reporter for the contents of the log directory.
2. The reporter opens the per-user roaming application-data root, then `ChatDbg`, then `Logs`. On Windows this renders as `%APPDATA%\ChatDbg\Logs`.
3. The reporter locates files named `llamasharp_YYYYMMDD.log` and attaches the relevant day.
4. The engineer reads plain UTF-8 text, one entry per line, shaped `[YYYY-MM-DD HH:MM:SS.mmm] [LEVEL] message`, in chronological order.

**Alternate flows**
- **B1 — No file for that day.** A rolling file is created only on the first *non-empty* flush of that local calendar day; a day with no flushed content leaves no file behind.
- **B2 — A run crossed local midnight.** Entries produced before midnight but flushed after it land in the *later* day's file, because the file name is derived from the clock at flush time, not at record time.
- **B3 — Non-Windows host.** The same per-user lookup resolves elsewhere (see External technology). The product's documentation only ever states the Windows form, so a non-Windows reporter has no documented place to look.

**Error flows**
- **B4 — Two instances of the product ran on the same machine on the same day.** Both append to the same file with no cross-process coordination; entries from the two runs interleave unpredictably and are not distinguishable by any marker in the file.
- **B5 — The file is enormous.** There is no size cap, no compression, no retention limit and no pruning; a long-lived install grows on disk without bound.
- **B6 — The reporter ran a shipped build.** Every `ERROR` entry that embeds a failure object renders an opaque resource key with no message and no stack trace, so the highest-value lines in the file carry no diagnostic content.

**Postconditions**
- The engineer holds a plain-text file requiring no tooling to read. Nothing in the product ever reads it back.

---

#### UC-11.C — Programmatic snapshot export *(realizes US-11.5)*

**Preconditions**
- A diagnostic recorder instance exists.

**Main flow**
1. A caller invokes "write the current diagnostics to this file path" on the inference subsystem, which passes it straight through to the recorder.
2. The parent directory of the supplied path is derived; if it is non-empty and does not exist, it is created recursively.
3. The destination file is **overwritten** with the entire current buffer content.
4. An entry `Logs saved to {path}` is recorded at level `INFO` — *after* the write, so it is never present in the file it describes.
5. Control returns. The buffer is **not** cleared: export is non-destructive.

**Alternate flows**
- **C1 — Bare file name with no directory component.** Directory creation is skipped and the file is written relative to the process working directory.
- **C2 — Destination already exists.** It is truncated and replaced without prompt or backup.

**Error flows**
- **C3 — Export fails** (invalid path, permission denied, disk full, path names a directory). Swallowed. One line `Failed to save logs to {path}: {exception}` to the debug stream. The caller receives no signal and **cannot distinguish failure from success**. The `Logs saved to …` entry is not recorded in this case.
- **C4 — Buffer is empty.** The write is unconditional, so a **0-byte** file is produced and reported as nothing at all. Because a completed generation force-flushes and drains the buffer, this is the *ordinary* outcome of exporting after a generation.

**Postconditions**
- The destination file exists and contains exactly the buffer content at the instant of the write (possibly zero bytes); the buffer is unchanged.

---

#### UC-11.D — Shutdown flush *(realizes US-11.3)*

**Preconditions**
- A recorder instance exists, possibly holding unflushed entries.

**Main flow**
1. The owning inference subsystem is explicitly shut down.
2. The recorder performs one flush: if file capture is on and the buffer is non-empty, the buffer is appended to today's rolling file and emptied.
3. The recorder latches itself as shut down.

**Alternate flows**
- **D1 — Shut down again.** Second and subsequent shutdowns do nothing at all and must not raise.
- **D2 — File capture off at shutdown.** The flush is an immediate no-op; the buffer's entire contents are discarded with the object.

**Error flows**
- **E10 — The owner is reclaimed by automatic memory management instead of being shut down explicitly.** The reclamation path skips recorder teardown entirely, so **no final flush ever runs and the whole unflushed buffer is lost** with no report on any channel.
- **E11 — Engine diagnostics arrive after shutdown.** The process-global engine destination is never uninstalled, so lines keep accumulating into a buffer that will never be flushed again, retaining memory indefinitely.

**Postconditions**
- On the normal path, the rolling file holds everything and the buffer is empty.

---

#### UC-11.E — Command-driven export, as it exists *(realizes US-11.6)*

**Preconditions**
- Either shell is at its prompt.

**Main flow (observed behaviour at the source commit)**
1. The user types `/export-logs debug.log`.
2. The shell strips the leading `/`, splits the remainder on spaces discarding empty segments, lower-cases the first segment as the command name and passes the rest as arguments.
3. Lookup fails — the command is registered in neither shell's command table.
4. The console shell prints `Unknown command: /export-logs. Type '/help' for available commands.`; the full-screen shell raises an "Unknown command" error dialog.
5. No file is created. End of flow.

**Alternate flows (the command object's own behaviour, reachable only by invoking it directly)**
- **E-A1 — Zero arguments.** Returns a **failure** result whose message is exactly, on three lines:
  `Usage: export-logs <filepath>`
  `Example: export-logs llamasharp-debug.log`
  `Note: This command only works with LLamaSharp provider`
- **E-A2 — One or more arguments.** Returns a **success** result whose message is exactly, on two lines:
  `Note: This command requires LLamaSharp provider integration.`
  `To use: Ensure provider is set to 'llama' and logs will be captured automatically`
  The supplied path is never inspected and never used; **no file is written**.

**Error flows**
- **E12 — A user follows the product's own documentation.** Several documents show `export-logs debug.log` producing `Success: System logs exported to paris_debug.log`. No such text exists in the product and no file is produced. The gap between documented and actual behaviour is total.
- **E13 — The user looks for the command in help.** The help output contains no logging section and no log-export entry, so the command is undiscoverable even as a stub.

**Postconditions**
- No file is created under any branch. No diagnostic state is read or modified.

---

**Functional requirements**

*Recorder configuration and defaults*

- **FR-11.1** — The system SHALL own exactly one diagnostic recorder per local-inference subsystem instance, created when that subsystem is created and never shared or exposed outside it. *(realizes US-11.3)*
- **FR-11.2** — A newly created recorder SHALL default its log directory to the per-user **roaming** application-data root joined with the two segments `ChatDbg` then `Logs`, in that order. On Windows this renders as `%APPDATA%\ChatDbg\Logs`. *(realizes US-11.2)*
- **FR-11.3** — The log directory SHALL never be null.
- **FR-11.4** — File capture SHALL default to **enabled**. *(realizes US-11.1)*
- **FR-11.5** — Debug-stream echo SHALL default to **enabled**. *(realizes US-11.4)*
- **FR-11.6** — Console echo SHALL default to **disabled**.
- **FR-11.7** — The maximum buffer size SHALL default to **10000 characters** (characters of accumulated buffer text, not bytes and not entries). This is a tunable default, not a business rule, but the value is asserted by the source's own tests and must be reproduced exactly.
- **FR-11.8** — The maximum buffer size SHALL accept **any** integer with no validation, no clamping, no lower bound and no upper bound; zero and negative values are accepted and cause every engine line to trigger a flush.
- **FR-11.9** — All five settings (log directory, file capture, debug-stream echo, console echo, maximum buffer size) SHALL be mutable on the recorder object at any time, and SHALL have **no runtime configuration surface at all** — they appear in no settings record, no settings command and no settings dialog. Changing them requires rebuilding the product.

*Arming capture*

- **FR-11.10** — Arming SHALL be performed at the top of **every** local generation request, before any engine work. *(realizes US-11.1)*
- **FR-11.11** — Arming SHALL be idempotent: if the recorder is already armed the operation returns immediately, registers nothing, records nothing and raises nothing.
- **FR-11.12** — Arming SHALL, in this exact order: (a) return immediately if already armed; (b) if file capture is enabled and the log directory does not exist, create it recursively; (c) install the recorder as the engine's process-global diagnostic destination; (d) set the armed latch; (e) record `LLamaSharp logging configured successfully` at level `INFO`.
- **FR-11.13** — Because the armed latch is set in step (d), the confirmation entry from step (e) SHALL always be the first application entry in a fresh buffer.
- **FR-11.14** — Any failure during arming SHALL be swallowed, SHALL emit exactly one line `Failed to configure LLamaSharp logging: {exception}` to the debug stream, and SHALL leave the recorder **un-armed** and retryable. *(realizes US-11.7)*
- **FR-11.15** — The engine diagnostic destination SHALL be **process-global and last-writer-wins**; installing a second recorder's destination silently stops the first recorder from receiving engine output while that first recorder still reports itself armed.
- **FR-11.16** — Directory creation SHALL occur **only** during arming and SHALL be gated on file capture being enabled at that moment. A recorder armed with file capture disabled therefore has no directory, and enabling file capture afterwards can never produce a rolling file unless the directory happens to exist already. *(See QUIRK-11.19.)*

*Recording entries*

- **FR-11.17** — On receiving an engine diagnostic line the system SHALL, in this order: (a) format the line as `[{timestamp}] [{level token}] {message with trailing whitespace stripped}`; (b) under the buffer exclusion lock, append that line plus a platform line terminator; (c) **still holding the lock**, if the buffer's character length is now strictly greater than the maximum buffer size **and** file capture is enabled, flush; (d) outside the lock, if debug-stream echo is enabled, emit the same line to the debug stream; (e) outside the lock, if console echo is enabled, emit the same line to standard output.
- **FR-11.18** — On recording an application entry the system SHALL, in this order: (a) format the line as `[{timestamp}] [{label}] {message}` with **no** trailing-whitespace stripping; (b) under the lock, append that line plus a platform line terminator; (c) outside the lock, echo to the debug stream if enabled; (d) outside the lock, echo to standard output if enabled. **No size check and therefore no automatic flush occurs on this path.** *(See QUIRK-11.5.)*
- **FR-11.19** — Entry timestamps SHALL use the pattern `yyyy-MM-dd HH:mm:ss.fff` — 24-hour clock, exactly three fractional-second digits, **local machine time**, with **no timezone or offset recorded**.
- **FR-11.20** — The severity label on an application entry SHALL be an arbitrary free-text token with no validation and no fixed set; empty, lower-case and nonsensical labels are accepted and rendered verbatim.
- **FR-11.21** — The application's own label vocabulary SHALL be `INFO`, `WARN`, `ERROR` and `DEBUG`, all upper-case. Engine-sourced entries carry whatever token the engine's own severity type renders as (INFERRED: mixed-case names such as `Info` / `Warn`), so a single file mixes **two label vocabularies**.
- **FR-11.22** — No level-based filtering SHALL exist anywhere. Everything captured is retained; there is no way to suppress the per-token `DEBUG` entries other than not enabling token-probability capture.
- **FR-11.23** — Buffer append order SHALL strictly match production order across both producer paths, guaranteed by a single mutual-exclusion lock around every append.
- **FR-11.24** — The lock SHALL be **re-entrant on the same thread**, because the size-triggered flush is invoked from inside the already-locked append and the flush itself acquires the same lock. A clone using a non-re-entrant primitive must restructure the flush.
- **FR-11.25** — Debug-stream and console echoes SHALL be emitted **outside** the lock; consequently the echoed order may differ from the buffered order under concurrency. **Only buffer and file ordering is guaranteed.**

*Reading and clearing*

- **FR-11.26** — Reading the buffer SHALL return the entire current buffer as one text value, taken under the lock, without mutating it; an empty buffer returns the empty string.
- **FR-11.27** — Clearing the buffer SHALL empty it under the lock and **discard** the content — it is not written anywhere first.

*Flushing to the rolling daily file*

- **FR-11.28** — Flushing SHALL return immediately, doing nothing, when file capture is disabled — and in particular **SHALL NOT drain the buffer**. Combined with FR-11.17(c), a recorder with file capture off accumulates without bound for the process lifetime with no cap, no eviction and no warning.
- **FR-11.29** — The rolling file name SHALL be `llamasharp_` + the flush-time local date rendered as `yyyyMMdd` (four-digit year, two-digit month, two-digit day, no separators) + `.log`, resolved inside the log directory.
- **FR-11.30** — Flushing SHALL, under the lock and only when the buffer is non-empty, **append** the whole buffer to that file and then clear the buffer. An empty buffer SHALL leave the file untouched and uncreated.
- **FR-11.31** — Any failure during a flush SHALL be swallowed and SHALL emit exactly one line `Failed to flush logs to file: {exception}` to the debug stream. Because the append precedes the clear, **buffered content survives a failed flush** and may be emitted by a later successful one. *(realizes US-11.7)*
- **FR-11.32** — Rolling SHALL be by local calendar day only: no size cap, no compression, no retention limit, no deletion of old files, and no size-based rotation of any kind. A process crossing local midnight begins writing to the next day's file at its next flush.
- **FR-11.33** — The rolling file SHALL be written as **UTF-8 with no byte-order mark** (INFERRED from the platform default; no test asserts it), with no header, no footer and no schema marker.
- **FR-11.34** — Files SHALL be opened, appended and closed per flush, with no held handles, no file locking, no forced synchronization to storage and no cross-process coordination. Two instances of the product writing on the same day interleave into one file.
- **FR-11.35** — The system SHALL force a flush at exactly five points: after a request-level failure; at the end of each of the two generation paths; after a model-weight-load failure; and after a context-creation failure. *(realizes US-11.3)*

*Snapshot export*

- **FR-11.36** — The snapshot export SHALL derive the parent directory of the supplied path and, if that component is non-empty and missing, create it recursively. A bare file name with no directory component SHALL skip creation and write relative to the process working directory. *(realizes US-11.5)*
- **FR-11.37** — The snapshot export SHALL **overwrite** the destination with the entire current buffer, under the lock, as UTF-8 with no byte-order mark. This is the opposite of the rolling flush, which always appends.
- **FR-11.38** — The snapshot export SHALL be **non-destructive**: the buffer is not cleared.
- **FR-11.39** — The snapshot export SHALL have **no emptiness guard**: an empty buffer produces a **0-byte** destination file.
- **FR-11.40** — After the write, the snapshot export SHALL record `Logs saved to {path}` at level `INFO`, which — being appended after the write — is never present in the file it describes.
- **FR-11.41** — Any failure during a snapshot export SHALL be swallowed and SHALL emit exactly one line `Failed to save logs to {path}: {exception}` to the debug stream. The operation returns nothing, so **the caller cannot distinguish success from failure**; the `Logs saved to …` entry is not recorded on the failure path.
- **FR-11.42** — The supplied path SHALL NOT be validated in any way: no extension check, no overwrite prompt, no length or character check, and no check that the path names a directory.

*Shutdown*

- **FR-11.43** — Shutting the recorder down SHALL perform exactly one flush and then latch itself as shut down; second and subsequent shutdowns SHALL do nothing and SHALL NOT raise. Shutdown SHALL NOT uninstall the process-global engine destination.

*The export-logs command*

- **FR-11.44** — The system SHALL provide a command named `export-logs`, typed by the user as `/export-logs`. *(realizes US-11.6)*
- **FR-11.45** — Its one-line description SHALL be exactly `Export LLamaSharp system/debug logs to a file`.
- **FR-11.46** — Its usage text SHALL be exactly `export-logs <filepath>` followed by a line break, two spaces, and `Example: export-logs llamasharp-debug.log`.
- **FR-11.47** — Invoked with **zero** arguments the command SHALL return a **failure** result whose message is exactly these three lines: `Usage: export-logs <filepath>` / `Example: export-logs llamasharp-debug.log` / `Note: This command only works with LLamaSharp provider`.
- **FR-11.48** — Invoked with **one or more** arguments the command SHALL return a **success** result whose message is exactly these two lines: `Note: This command requires LLamaSharp provider integration.` / `To use: Ensure provider is set to 'llama' and logs will be captured automatically`.
- **FR-11.49** — The command SHALL validate argument **count only**; the supplied path SHALL never be inspected, never be used, and no file SHALL ever be written.
- **FR-11.50** — The command SHALL NOT be registered in either shell's command table at the source commit; typing `/export-logs <anything>` SHALL therefore produce the shell's unknown-command outcome — the console shell printing `Unknown command: /export-logs. Type '/help' for available commands.` and the full-screen shell raising an "Unknown command" error dialog. *(See QUIRK-11.1 and Open Questions.)*
- **FR-11.51** — The product's help output SHALL contain no logging section and no log-export entry.

*Failure philosophy and observability*

- **FR-11.52** — Every operation in this feature SHALL swallow its own exceptions and return no status. Diagnostics must never break the thing they are diagnosing. *(realizes US-11.7)*
- **FR-11.53** — The debug stream SHALL be the **only** channel on which this feature reports its own internal failures (arming failure, flush failure, snapshot-export failure).
- **FR-11.54** — In the source, that channel is compiled out of every build that does not define the diagnostic-build compilation symbol, and no build configuration in the source defines it outside the stock diagnostic configuration. In the shipped configurations, therefore, a completely dead logging subsystem SHALL be indistinguishable from a healthy one — no console line, no debug line, no file, no chat error. A clone SHOULD make this channel a **runtime** switch rather than a compile-time one, or route internal failures somewhere that survives; see Open Questions. *(See QUIRK-11.11.)*
- **FR-11.55** — Buffer growth caused by disabled file capture SHALL never be detected, capped, evicted or warned about.
- **FR-11.56** — Engine diagnostics arriving after shutdown SHALL never be detected; they accumulate in a buffer that will never be flushed, retained by the still-installed global destination.
- **FR-11.57** — A recorder reclaimed by automatic memory management rather than shut down explicitly SHALL lose its entire unflushed buffer, because the reclamation path skips recorder teardown. No report is made on any channel. *(See QUIRK-11.22 — carried from the dossier as observed behaviour, decision deferred.)*

*Cross-feature*

- **FR-11.58** — This feature SHALL be inert unless the locally-executed model provider is selected; the two hosted providers write to the debug stream directly and never touch this recorder.
- **FR-11.59** — This feature SHALL perform no network activity, require no credentials and reach no remote service.
- **FR-11.60** — No content from the diagnostic buffer SHALL ever reach the per-token analysis record's system-debug-information field; that field is filled with a synthesized one-line string of the form `Generated via sampling pipeline at step {N}, temperature={T}.` regardless of what the recorder holds.
- **FR-11.61** — Log entry text SHALL never be parsed, indexed or queried by the product. There is no structured (key-value or document) log format anywhere in this feature and no read-back path of any kind.
- **FR-11.62** — All level labels, message texts, usage strings and command result text SHALL be hard-coded English with no resource lookup and no localization hook.

---

**External technology**

*Requires: an in-process local large-language-model inference engine that emits native diagnostic lines and exposes a hook to install a diagnostic-sink callback (native/unmanaged library callback carrying a severity-level token plus a message string per line). Source used: LLamaSharp 0.25.0 managed bindings over llama.cpp (`NativeLogConfig.llama_log_set`), with CPU and CUDA-12 backend packages. Reimplementer notes: the sink is process-global, set-only (the source never unregisters), and last-writer-wins. The callback fires on engine-owned threads, so the recorder is genuinely multi-threaded, not merely thread-safe by convention. The severity level is an engine-internal enumeration rendered by name — treat it as an opaque token and do not attempt to map it onto the application's own vocabulary. INFERRED: the engine may deliver partial lines rather than whole lines; the source assumes whole lines (it appends a line terminator per callback invocation) and only strips trailing whitespace, so a multi-part engine message would fragment across several timestamped lines. The product's own documentation still cites version 0.11.2 — that is stale and should be ignored.*

*Requires: a local filesystem offering recursive directory creation, append-to-file, overwrite-file, file-exists and directory-exists primitives (POSIX / Win32 file I/O). Source used: platform file APIs. Reimplementer notes: no file handles are held open — every flush opens, appends and closes. There is no file locking, no forced synchronization to storage, and no cross-process coordination, so two instances writing the same daily file interleave unpredictably. All failures must be swallowed.*

*Requires: resolution of a per-user "roaming application data" folder (OS convention). Source used: platform special-folder lookup for roaming application data, yielding `%APPDATA%` (i.e. `C:\Users\<user>\AppData\Roaming`) on Windows. Reimplementer notes: two fixed sub-segments follow, `ChatDbg` then `Logs`. On Unix-like hosts the same lookup yields `$XDG_CONFIG_HOME`, falling back to `$HOME/.config`, giving `~/.config/ChatDbg/Logs` — a location documented nowhere in the source. **Hazard:** with no home directory set the lookup returns the empty string and the two segments compose into the relative path `ChatDbg/Logs`, so logs silently land under the process working directory; nothing guards this. INFERRED from platform behaviour. Note that this product uses three different per-user roots for three different kinds of state — see Cross-cutting rules.*

*Requires: an attachable debug/trace output stream visible to an attached debugger (no protocol). Source used: the platform debug-write primitive, conditionally compiled on the diagnostic-build symbol. Reimplementer notes: this is on by default **and** is the only channel for this feature's own internal failures. In the source it is removed at compile time in any build lacking that symbol, and no project configuration defines it outside the stock diagnostic build — so in a shipped binary the channel does not exist. In a clone this maps to a debugger console, a development-time sink, or standard error; a clone should make it a runtime switch and must not route internal failure reports through a channel that can vanish. It must be cheap or a no-op when nothing is listening.*

*Requires: standard console output (no protocol). Source used: the platform console write-line primitive. Reimplementer notes: off by default; when enabled, log lines interleave directly into the user's chat transcript with no visual separation. No code path in the source ever enables it.*

*Requires: a local wall clock with millisecond resolution (no protocol). Source used: the platform local (not coordinated-universal) date-time. Reimplementer notes: used both for entry timestamps and for the daily file-name date. No timezone or offset is recorded, so entries are ambiguous across daylight-saving transitions and across machines. Timestamps are rendered through the **current culture's** formatter, so a locale with a non-`:` time separator changes the output shape; the shipped publish configurations force invariant globalization while the development configurations do not, meaning the same source emits two timestamp shapes depending on how it was built.*

*Requires: a thread mutual-exclusion primitive that is re-entrant on the same thread (no protocol). Source used: the platform monitor lock over a private object. Reimplementer notes: re-entrancy is load-bearing — the size-triggered flush is invoked from inside the locked append and re-acquires the same lock. A non-re-entrant primitive deadlocks unless the flush is restructured.*

*Requires: elapsed-time measurement (no protocol). Source used: the platform stopwatch. Reimplementer notes: used only to produce the `Generation complete in {N}ms` entry.*

*Requires: text file encoding of UTF-8 (UTF-8). Source used: the platform default for unspecified-encoding text writes — **UTF-8 with no byte-order mark**. Reimplementer notes: applies to both the daily append and the snapshot overwrite. A clone that emits a byte-order mark, or a 16-bit encoding, breaks the "attach the file to a bug report" workflow for line-oriented readers. INFERRED — no test inspects bytes.*

*Requires: a unit-test runner with fact-style tests and access to a system temporary directory (no protocol). Source used: xUnit 2.9.1, with a mocking library present but unused by these tests (Moq 4.20.69) and a coverage collector (coverlet 6.0.2). Reimplementer notes: the source's tests touch the real filesystem with no abstraction over file I/O, and two of the seven recorder tests contain zero assertions. A clone should place the clock, the file store and the debug sink behind seams so that the engine-callback path, the auto-flush threshold, the daily file-name derivation and all four failure branches — none of which the source tests reach — become testable.*

*Requires: a managed runtime and build toolchain capable of ahead-of-time compilation, trimming and single-file packaging (no protocol). Source used: .NET 10 (`net10.0`) across the library, both shells and the test project. Reimplementer notes: the publish configurations matter more to this feature than usual — the compact and single-file configurations force invariant globalization, substitute bare framework resource keys for exception messages, strip symbols and disable stack-trace metadata, and default the runtime identifier to 64-bit Windows. Every `ERROR` entry embeds a whole failure object, so precisely in the configurations a user is most likely to run, the most valuable log lines degrade to an opaque key. INFERRED: aggressive trimming plus a callback marshalled across the managed/native boundary is a known hazard and trim-analysis warnings are explicitly suppressed, so such a break would present as silence rather than an error.*

---

**Acceptance criteria**

- **AC-11.1** — **Given** a freshly created diagnostic recorder with nothing overridden, **when** its configuration is read, **then** file capture is enabled, debug-stream echo is enabled, console echo is disabled, the maximum buffer size is exactly `10000`, and the log directory is non-null and equals the per-user roaming application-data root joined with `ChatDbg` then `Logs`.

- **AC-11.2** — **Given** a recorder with file capture, debug-stream echo and console echo all disabled, **when** an entry is recorded at level `INFO` with the message `Test message`, **then** reading the buffer returns text containing both `Test message` and the literal `[INFO]`, and no file has been created anywhere.

- **AC-11.3** — **Given** a recorder holding at least one entry, **when** the buffer is cleared, **then** reading the buffer returns the empty string and no file was written as a side effect.

- **AC-11.4** — **Given** a recorder with file capture disabled holding one entry with the message `Test log entry`, **when** a snapshot export is requested to a path inside a temporary directory, **then** that file exists and contains `Test log entry`, **and** reading the buffer still returns that entry.

- **AC-11.5** — **Given** a recorder, **when** three entries are recorded at levels `INFO`, `WARNING` and `ERROR` with messages `Message 1`, `Message 2` and `Message 3` in that order, **then** the buffer contains all three messages and all three bracketed labels, one entry per line, in the order recorded.

- **AC-11.6** — **Given** a recorder with file capture enabled and its log directory set to an existing writable directory, holding one entry, **when** it is shut down, **then** a file named `llamasharp_<today's local date as YYYYMMDD>.log` in that directory contains the entry, the buffer is empty, **and** a second shutdown completes without error and without writing again. *(The source's identically-named test asserts none of this — see QUIRK-11.15. A clone must implement this as a real, asserting test.)*

- **AC-11.7** — **Given** a recorder and a loadable inference engine, **when** arming is invoked twice in succession, **then** neither call raises, the engine destination is installed exactly once, and the buffer contains exactly one entry at level `INFO` reading `LLamaSharp logging configured successfully`. **And given** an environment where the engine destination cannot be installed, **when** the same two calls are made, **then** neither raises, the recorder remains un-armed, the buffer stays empty, and the only trace is one debug-stream line beginning `Failed to configure LLamaSharp logging:` — which itself vanishes in a build without the diagnostic-build symbol.

- **AC-11.8** — **Given** a recorder with file capture **disabled** and a buffer holding entries, **when** a flush is requested, **then** no file is created or modified anywhere and the buffer still holds every entry.

- **AC-11.9** — **Given** a recorder with file capture enabled whose log directory does not exist and which was never armed, **when** a flush is requested, **then** no exception escapes, no file is created, and the buffer retains all entries.

- **AC-11.10** — **Given** an armed recorder with file capture enabled and a maximum buffer size of `10000`, **when** engine diagnostic lines arrive until the buffer's character length first exceeds `10000`, **then** on that append the buffer is appended to today's rolling file and emptied, and subsequent lines begin a fresh buffer. **And given** the same recorder, **when** an equivalent volume arrives as application entries instead, **then** no automatic flush occurs at any buffer size.

- **AC-11.11** — **Given** any entry produced by any path, **when** it is inspected, **then** it matches the shape `[YYYY-MM-DD HH:MM:SS.mmm] [LEVEL] message` in local time with exactly three fractional-second digits; **and** an engine-sourced entry has had trailing whitespace stripped from its message while an application-sourced entry has not.

- **AC-11.12** — **Given** the locally-executed model provider is active and token-probability capture is **enabled**, **when** a chat turn completes generation, **then** the day's rolling file contains, in this order: `Starting message generation`, `Using sampling pipeline with token probability capture mode`, `Processing user message with {N} characters`, `Starting token-by-token generation with sampling pipeline probability capture`, one `DEBUG` entry per generated token of the form `Token {n}: '{text}'`, and `Generation complete in {X}ms, generated {N} tokens` — and the in-memory buffer is empty afterwards.

- **AC-11.13** — **Given** the locally-executed model provider is active and the configured model file is not a loadable model, **when** a chat turn is sent, **then** the day's rolling file contains an `ERROR` entry beginning `Failed to load model weights:` followed by an `ERROR` entry beginning `Model loading failed:`, the buffer was flushed before the failure propagated, and the user receives a chat response beginning `Error running local LLM:`.

- **AC-11.14** — **Given** a generation that has just completed (and therefore force-flushed and drained the buffer), **when** a programmatic snapshot export is requested, **then** the destination file is created or overwritten but contains at most the handful of entries produced since that flush — it does **not** contain the generation's diagnostics.

- **AC-11.15** — **Given** either shell at its prompt, **when** the user types `/export-logs debug.log`, **then** the console shell prints `Unknown command: /export-logs. Type '/help' for available commands.` (the full-screen shell raising an "Unknown command" error dialog) and no file named `debug.log` is created.

- **AC-11.16** — **Given** the export-logs command object invoked directly with zero arguments, **when** it executes, **then** it returns a **failure** whose message is exactly the three lines `Usage: export-logs <filepath>` / `Example: export-logs llamasharp-debug.log` / `Note: This command only works with LLamaSharp provider`; **and** invoked with the single argument `file.log` it returns a **success** whose message contains the literal `Note`, and no file named `file.log` is created.

- **AC-11.17** — **Given** the product's help output, **when** it is displayed, **then** no logging entry and no log-export entry appear anywhere in it.

- **AC-11.18** — **Given** a recorder whose buffer is empty, **when** a snapshot export to `/tmp/empty.log` (or `C:\tmp\empty.log`) is requested, **then** that file exists afterwards with a length of exactly **0 bytes**, no failure is signalled to the caller, and the only way to distinguish this from a successful capture is to inspect the file's size.

- **AC-11.19** — **Given** a recorder whose log directory does not exist and whose file capture is **off**, **when** capture is armed, then file capture is switched **on**, an entry is recorded, and a flush is requested, **then** the directory is still absent, no file is created, no exception escapes, and the entry remains in the buffer — and a second flush behaves identically, indefinitely.

- **AC-11.20** — **Given** the product built in any configuration that does not define the diagnostic-build compilation symbol, **when** the log directory cannot be created so that arming fails and every subsequent flush fails, **then** the user and the operator observe **nothing on any channel** — no console line, no debug line, no log file, no chat error — and the chat session continues normally with local inference fully functional.

- **AC-11.21** — **Given** either shell published under the compact or single-file configuration with no runtime identifier supplied on the command line, **when** the publish completes, **then** the output targets 64-bit Windows and runs only there, despite the product describing itself as cross-platform.

- **AC-11.22** — **Given** either shell published under the compact configuration, **when** a model load fails, **then** the `ERROR` entry written to the daily file carries a bare framework resource key in place of the failure message and no stack trace, rather than the readable text the same failure produces in a diagnostic build.

- **AC-11.23** — **Given** a daily rolling file produced by two successive flushes, **when** its bytes are inspected, **then** it is UTF-8 with **no** byte-order mark, and no mark appears at the boundary between the two appends. *(INFERRED — no test in the source covers this.)*

- **AC-11.24** — **Given** a recorder whose maximum buffer size has been set to `0`, **when** a single engine diagnostic line arrives and file capture is enabled, **then** the buffer is flushed to today's file on that one line — one file append per diagnostic line — with no validation error raised at any point.

- **AC-11.25** — **Given** a recorder that has been shut down, **when** the engine emits further diagnostic lines, **then** those lines are still appended to the buffer, no further flush ever occurs, and no error is raised or reported.

- **AC-11.26** — **Given** a running process that crosses local midnight between one flush and the next, **when** the later flush occurs, **then** its content is appended to the **new** day's file, named from the clock at flush time, even for entries recorded before midnight.

---

**Quirks**

*QUIRK-11.1: The `export-logs` command is unreachable — it is registered in neither shell's command table and appears in no help output, so typing `/export-logs anything` yields the unknown-command response, while five separate product documents show it as a working command. Evidence: `src/Xcaciv.ChatDbg.Core/Commands/ExportLogsCommand.cs`; registries at `src/ChatDbg/ChatShell.cs:42-57`, `src/ChatDbg.Shell.Gui/ChatShell.cs:45-58`, `src/ChatDbg.Shell.Gui/Program.cs:31-47`; docs at `docs/LLamaSharp-Token-Introspection.md:89,222,301`, `docs/LLamaSharp-Quick-Start.md:117-119`, `docs/LLamaSharp-Implementation-Notes.md:176`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.2: Even if it were registered, the command exports nothing — it returns a canned advisory string, writes no file, and ignores the supplied path entirely. The documented example output `Success: System logs exported to paris_debug.log` exists nowhere in the product. Evidence: `src/Xcaciv.ChatDbg.Core/Commands/ExportLogsCommand.cs:23`; `docs/LLamaSharp-Token-Introspection.md:301-305`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.3: A zero-argument invocation of the command is reported as a failure, but an invocation with a path is reported as a **success** despite doing nothing at all — and the test suite locks that wrongness in, asserting success and that the message contains the literal `Note`. Evidence: `src/Xcaciv.ChatDbg.Core/Commands/ExportLogsCommand.cs:18-23`; `src/Xcaciv.ChatDbg.Core.Tests/Commands/ExportLogsAndAnalysisCommandTests.cs:10-28`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.4: Flushing defeats exporting. Generation force-flushes (and drains) at the end of every run and after every error, while the snapshot export writes only what is currently buffered — so the ordinary user workflow "run a generation, then export" produces a file containing none of the generation's diagnostics. Evidence: `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:101,255,405,587,601` vs `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs:182`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.5: The size-triggered auto-flush fires only for engine-sourced lines, never for application entries — so a run producing thousands of per-token `DEBUG` entries accumulates every one of them with no size-triggered flush. Evidence: `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs:90` (present) vs `:123-126` (absent); producer at `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:231`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.6: The documented inference-engine dependency version (0.11.2) is stale; the build pins 0.25.0. Evidence: `docs/LLamaSharp-Implementation-Notes.md` package section, `IMPLEMENTATION_SUMMARY.md` package section vs `src/Xcaciv.ChatDbg.Core/Xcaciv.ChatDbg.Core.csproj`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.7: The recorder is filed under a folder named for a different feature (token inspection) and its tests mirror that folder, yet it has no relationship to token inspection — which uses the raw platform debug stream directly. A reimplementer must not infer a dependency from the folder layout. Evidence: `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs`; `src/Xcaciv.ChatDbg.Core/Services/TokenInspectionService.cs:35,43,82,109,127,208`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.8: The product writes three kinds of per-user state to three different per-user roots — logs to roaming application data, system prompts to local application data, settings to the user-profile root. Evidence: `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs:20`; `src/Xcaciv.ChatDbg.Core/Services/SystemPromptService.cs:15`; `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:16`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.9: A unit test writes a real dated log file into the system temporary directory and never cleans it up, while a sibling test in the same file does clean up in a finally block. Evidence: `src/Xcaciv.ChatDbg.Core.Tests/Services/TokenInspection/LLamaSharpLogConfigTests.cs:119-137` vs `:85-90`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.10: The shipped builds are 64-bit-Windows-only by default in a product whose own assembly description calls it cross-platform; the condition supplies only a default that an explicit publish-time runtime identifier would override, and nothing in the repository ever does. Evidence: `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:16,30,70`; `src/ChatDbg.Shell.Gui/Xcaciv.ChatDbg.Shell.Gui.csproj:30,70`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.11: The feature's only internal-failure channel — the platform debug-trace sink — is removed at compile time in every build lacking the `DEBUG` symbol, and no project file or shared property file in the repository defines that symbol outside the stock Debug configuration. In Release, compact and single-file builds the default-on debug-output switch therefore does nothing, and a completely dead logging subsystem reports nothing on any channel. Evidence: `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs:98,112,130,163,189`; `Directory.Build.props:1-5`; `src/Xcaciv.ChatDbg.Core/Xcaciv.ChatDbg.Core.csproj:1-19`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.12: The shipped builds gut the exception detail these logs exist to capture — the compact and single-file configurations substitute bare framework resource keys for exception messages, strip symbols and disable stack-trace metadata, so every `ERROR` entry that embeds an exception, and the chat-visible `Error running local LLM: {message}` text, degrade to an opaque key. Evidence: `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:53,58,59,96`; `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:100,107,571`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.13: The same source emits two different timestamp shapes depending on build configuration — entry timestamps and the daily file-name date are rendered through the current culture, and only the compact and single-file configurations force invariant globalization. Evidence: `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs:83,121,149`; `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:52,95`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.14: The five configuration knobs the documentation tells users to set "in config" have no configuration surface at all — they appear in no settings model and no settings dialog, and the documentation's only example uses a Windows-only literal directory. Evidence: `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:25`; `src/Xcaciv.ChatDbg.Core/Models/ChatSettings.cs` and `src/ChatDbg.Shell.Gui/UI/SettingsDialog.cs` (no matches for any of the five names); `docs/LLamaSharp-Quick-Start.md:230`, `docs/LLamaSharp-Token-Introspection.md:243,260`, `docs/LLamaSharp-Implementation-Notes.md:199-208`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.15: Two of the seven recorder unit tests assert nothing whatsoever — their entire "assert" step is a source comment — so both would still pass if flush-on-shutdown and arm-idempotency were deleted outright. Evidence: `src/Xcaciv.ChatDbg.Core.Tests/Services/TokenInspection/LLamaSharpLogConfigTests.cs:118-137` (assert at `:134-135`) and `:139-153` (assert at `:152`). Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.16: The arm-idempotency test probably never exercises the path it names — installing the engine destination requires the native library, and on a host where it will not load the attempt throws, is swallowed, the armed latch is never set, and both calls take the un-armed path. INFERRED. Evidence: `src/Xcaciv.ChatDbg.Core.Tests/Services/TokenInspection/LLamaSharpLogConfigTests.cs:139-153`; `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs:69-70,81,107,110-113`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.17: The documented promise "logs flushed every 10,000 characters or on disposal" is half true — the threshold check exists only on the engine-diagnostic path, and the documentation omits the five forced flushes entirely. Evidence: `docs/LLamaSharp-Quick-Start.md:206`; `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs:90` vs `:123-126`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.18: Exporting an empty buffer silently produces a 0-byte file — the snapshot write is unconditional, unlike the daily flush which skips an empty buffer — and the caller is told neither that it succeeded nor that it wrote nothing. Evidence: `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs:180-183` vs `:154`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.19: A recorder armed with file capture off can never write a daily file afterwards, because directory creation is gated on the file-capture switch at arm time and arming is one-shot — which is exactly the state the product's own "verify file logging is enabled" troubleshooting advice would leave a user in. Evidence: `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs:69-70,75,161-164`; `docs/LLamaSharp-Token-Introspection.md:258-261`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.20: The export command imports the console-rendering library and the services namespace and uses neither — vestigial evidence that it was meant to reach a live service instance and never got wired to one. Evidence: `src/Xcaciv.ChatDbg.Core/Commands/ExportLogsCommand.cs:1,3`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.21: Shutting the recorder down does not uninstall the process-global engine destination, and the installed callback holds a reference to the recorder's buffer — so after shutdown, engine diagnostics keep accumulating into a buffer that will never be flushed, retaining memory for the process lifetime. Evidence: `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs:81` vs `:193-200`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.22: The owning inference subsystem's finalizer routes to shared teardown with the "explicit disposal" flag false, and recorder teardown lives only on the true branch — so a subsystem reclaimed by the garbage collector rather than disposed explicitly loses its entire unflushed buffer with no final flush. Evidence: `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:693-696` vs `:678-681`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.23: The documentation claims the per-token analysis record's system-debug-information field is populated from the log buffer; in code it is filled with a synthesized one-liner and no log content ever reaches it. Evidence: `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:286`; `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/TokenAnalysis.cs:52-56`; `docs/LLamaSharp-Implementation-Notes.md:266`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-11.24: On a Unix-like host with no home directory set, the per-user application-data lookup returns the empty string and the two path segments compose into a working-directory-relative path, so logs land wherever the process happens to be running. Nothing guards the empty root. INFERRED from platform behaviour. Evidence: `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs:20`. Keep-or-fix decision deferred to Open Questions.*

---

**Source notes**

Derived from dossier `output/chatdbg/dossiers/diagnostic-logging.md`, itself pinned to `subject/chatdbg` at commit `d8c18f61d6bb73666ed97cd4885e877e35558485` (branch `LLamaSharp_support`). Feature boundary from `output/chatdbg/inventory.md`, entry 13 ("Diagnostic Logging & Log Export", platform capability, depends on entry 10).

Primary evidence:
- `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs` (201 lines — the recorder: defaults `:20,25,30,35,40`; buffer read `:45-51`; clear `:56-62`; arm `:67-114`; engine callback `:81-105`; application entry `:119-137`; flush `:142-165`; snapshot export `:170-191`; shutdown `:193-200`)
- `src/Xcaciv.ChatDbg.Core/Commands/ExportLogsCommand.cs` (25 lines — the stub command, name/description/usage `:12-14`, argument check `:18`, messages `:20,:23`)
- `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs` (the only production consumer — recorder field `:25`; arm and first entry `:77-78`; routing entries `:91,95`; request entries `:100-110,138,163,217,231,249,255,368,399,405`; model load and context entries `:535-615`; token-analysis export entries `:642,646`; re-exposed save method `:653-656`; teardown `:678-681` and finalizer `:693-696`)
- `src/ChatDbg/ChatShell.cs` (command registry `:42-57`, result rendering `:102-104`, input parsing `:326-333`, unknown-command message `:340`)
- `src/ChatDbg.Shell.Gui/ChatShell.cs:45-58`, `src/ChatDbg.Shell.Gui/Program.cs:26,31-47`, `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:384-391,395,411,415`
- `src/Xcaciv.ChatDbg.Core/Commands/HelpCommand.cs:42-97` (no logging section)
- Build configuration: `Directory.Build.props:1-5`; `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:16,23-97` (esp. `:30,35,52,53,58,59,70,95,96`); `src/ChatDbg.Shell.Gui/Xcaciv.ChatDbg.Shell.Gui.csproj:30,70`; `src/Xcaciv.ChatDbg.Core/Xcaciv.ChatDbg.Core.csproj:14-15`
- Tests: `src/Xcaciv.ChatDbg.Core.Tests/Services/TokenInspection/LLamaSharpLogConfigTests.cs:10-153`; `src/Xcaciv.ChatDbg.Core.Tests/Commands/ExportLogsAndAnalysisCommandTests.cs:9-28`; duplicate at `src/Xcaciv.ChatDbg.Core.Tests/Services/TokenInspectionModelsTests.cs:106-121`; test project pins at `src/Xcaciv.ChatDbg.Core.Tests/Xcaciv.ChatDbg.Core.Tests.csproj:12-19`
- Documentation consulted as *intent only* (code wins on every disagreement): `docs/LLamaSharp-Troubleshooting-0xC0000005.md:185,263,270`; `docs/LLamaSharp-Token-Introspection.md:89,221-224,236,243,244,258-261,301-305`; `docs/LLamaSharp-Quick-Start.md:117-119,130-148,206,230,233`; `docs/LLamaSharp-Implementation-Notes.md:38,176,199-208,266`; `IMPLEMENTATION_SUMMARY.md`. `README.md` contains no mention of this feature.

Cross-feature: the recorder's folder co-location with token-inspection models is misleading (QUIRK-11.7) — this feature depends on Local LLM Inference and on the shared Command System contract, and on nothing else.
