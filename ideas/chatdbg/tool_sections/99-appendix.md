# Part III — Appendices

## Appendix A — Complete tool index

All 117 tools, alphabetical within package. **Origin** is `ported` (traces to observed source behaviour) or **NEW**.


### A.1 `CHAT` — Session & Conversation (12 tools)

| Tool | Purpose | Pipeline role | Origin |
|---|---|---|---|
| `CHAT APPEND` | Append a fabricated turn (role + content) to the end of the conversation record without calling any model; can set the dormant isCommand flag so backends exclude the message from every request. | both — one piped chunk is the content of one message appended with the command-line role; emits one confirmation chunk per append (silent under -quiet via an empty success) | ported:ChatHistory.AddMessage(role, content, isCommand, logProbabilities) Models/ChatHistory.cs:14-24 — a real product operation with no user-facing command |
| `CHAT CLEAR` | Empty the conversation record, optionally preserving system-role turns, optionally minting a new session identifier, and optionally writing a backup file first; preserves 'Cleared 0 messages' as a success and the rule that session id and creation timestamp survive. | not-pipeable — refuses piped input, emits one output chunk | ported:/clear Commands/ClearCommand.cs and Models/ChatHistory.cs:53-56 |
| `CHAT EXPORT` | Write the conversation record to a file, byte-compatible with the source's JSON format (key order, 2-space indent, seven-digit UTC fractional seconds, aggressive escaping, shortest-round-trip numbers), plus new jsonl/md/text formats, atomic temp-and-rename writes and optional export-root containment. | both (sink-leaning) — one piped chunk is one message record in the -format shape, written instead of the live record; emits one summary chunk | ported:/export <file_path> Commands/ExportCommand.cs and Services/ChatHistoryService.cs:29-43 |
| `CHAT IMPORT` | Load a conversation record from a file, replacing or appending, with a new default of warn-level role validation, a 32 MiB size cap, and optional import-root containment; preserves in-place mutation of the live record, session-id overwrite and the no-extension-defaulting asymmetry. | both — one piped chunk is one file path imported in order; emits one summary chunk per path, or one chunk per message under -emit; overrides Main for -emit | ported:/import <file_path> Commands/ImportCommand.cs and Services/ChatHistoryService.cs:45-65 |
| `CHAT INJECT` | Insert a fabricated turn at a chosen zero-based position in the record, with the source's role whitelist and out-of-range-appends rule preserved and its last-positional-token position parsing deliberately replaced by a named -position parameter. | both — one piped chunk is one message body, chunk n inserted at position + n so upstream order is preserved; one confirmation chunk per injection | ported:/inject <role> <message> [position] Commands/InjectCommand.cs and Models/ChatHistory.cs:34-51 |
| `CHAT LIST` | Emit the conversation record one message per chunk, filtered by role and range, in text, JSON or CSV — the pipeline source every other tool composes against. | source — refuses piped input; emits one chunk per message; declares ResultFormat.JSON or .CSV so downstream tools branch on format rather than sniffing; overrides Main | **NEW** |
| `CHAT NAME` | Show, set or rename the current session's friendly name, and optionally pin its session identifier to a specific UUID for reproducible experiments. | source (output only) — refuses piped input because a rename per chunk is meaningless and destructive; emits one chunk | **NEW** |
| `CHAT POP` | Remove the last message (or the last N) from the record and emit the removed messages so they can be saved before they are lost; preserves the 50-character preview default while fixing the unconditional ellipsis and the UTF-16 mid-surrogate cut. | source (output only) — refuses piped input with an explanatory string; emits one chunk per removed message newest-first plus a summary; overrides Main for -count > 1 | ported:/pop Commands/PopCommand.cs and Models/ChatHistory.cs:26-32 |
| `CHAT RETRY` | Discard the last reply and re-send the preceding user turn with per-invocation overrides (temperature, model, provider, top-K, system prompt), optionally emitting the discarded reply first — the product's central compare-two-settings loop as one command. | source (output only) — refuses piped input; emits the discarded reply under -keep, then the streamed reply chunks, then an elapsed-time chunk; overrides Main | **NEW** |
| `CHAT SEND` | Send a conversational turn to the active model backend and stream the reply, recording both sides in the conversation record; carries per-turn overrides for provider, model, temperature, max tokens, log probabilities, top-K, system prompt, streaming, output format and timeout. | both — accepts piped input (one chunk = one complete user turn, sent as an independent request) and produces piped output (one chunk per streamed segment plus a terminal elapsed-time chunk); overrides Main to emit multiple chunks on the non-piped path | ported:the console shell's chat-turn path (any typed line not beginning with '/'), ChatShell.SendMessageAsync src/ChatDbg/ChatShell.cs:343-410 |
| `CHAT SESSIONS` | List saved conversation sessions with name, message count, created and last-used timestamps and an active marker, sortable and filterable, in text, JSON or CSV. | source — refuses piped input; emits one chunk per session; unparseable session files are skipped as isolated failures; overrides Main | **NEW** |
| `CHAT USE` | Switch the active conversation session, creating it when absent or forking the current record into it, saving the current session first by default; writes CHAT_SESSION into the package environment bucket, which is how every other tool sees the switch. | both — the ordered name parameter declares UsePipe, so one piped chunk is one session name; emits one confirmation chunk per switch and warns when more than one name arrives | **NEW** |

### A.2 `SET` — Configuration & Profiles (16 tools)

| Tool | Purpose | Pipeline role | Origin |
|---|---|---|---|
| `SET DIFF` | Report the keys that differ between two profiles, or between a profile and the built-in defaults | filter (one chunk = one left-hand profile) and source; silent when identical | **NEW** |
| `SET DROPPROFILE` | Permanently delete a saved profile; requires -force, refuses the active profile and 'default' | filter (one chunk = one profile name) | **NEW** |
| `SET ENV` | Set a shell environment variable — the framework built-in SET preserved under the new root | sink (accumulates piped chunks into the variable; emits only empty successes) | ported:the Xcaciv.Command built-in SET command (Xcaciv.Command/Commands/SetCommand.cs) |
| `SET GET` | Print the value of exactly one setting, optionally with its provenance | filter (one chunk = one setting key; one value chunk out per key) and source | **NEW** |
| `SET IMPORT` | Validate and adopt a complete settings document from a file or the pipe, replacing or merging | filter (one chunk = one complete document) and sink | **NEW** |
| `SET KEYS` | Emit the machine-readable key catalog: key, aliases, type, default, min, max, allowed values, current value | source (overrides Main to emit one chunk per key) | **NEW** |
| `SET MODEL` | Show or change the model identifier / GGUF path, with the same validation SET VALUE modelId applies | filter and source | ported:/model (Commands/ModelCommand.cs:23-34) |
| `SET PATH` | Report the resolved settings document, base directory and profile directory, and how each was chosen | source | ported:the plain-console startup banner line 'Settings file: <path>' (ChatDbg/ChatShell.cs:192-208) |
| `SET PROFILE` | Show the active configuration profile, or validate and switch to another | filter and source | **NEW** |
| `SET PROFILES` | List every saved profile, one per chunk, optionally with provider/model/last-used/validity | source (one chunk per profile) | **NEW** |
| `SET RESET` | Restore one key, one section, or the whole record to built-in defaults | source (emits one line per field changed; refuses piped input) | **NEW** |
| `SET SAVEPROFILE` | Capture the current record (or another profile) as a named profile, with secret slots stripped | filter (one chunk = one profile name to create) | **NEW** |
| `SET SHOW` | Render the effective configuration (all or one section) in text/JSON/YAML/CSV, with secrets shown as status-and-provenance only | source (produces piped output; refuses piped input with an explanatory string) | ported:/set with no arguments (Commands/SetCommand.cs:401-458) plus the windowed Settings dialog read path |
| `SET UNSET` | Clear a text setting to empty, or return a typed setting to its built-in default | filter (one chunk = one key to clear) | **NEW** |
| `SET VALIDATE` | Validate the active record, a named profile, or a piped JSON document against the key catalog | filter (one chunk = one complete JSON settings document) and sink under -strict | **NEW** |
| `SET VALUE` | Validate and store one tunable, then persist the document | filter (value arrives via UsePipe; one chunk = one candidate value; one transaction committed in OnEndPipe) | ported:/set <key> <value...> (Commands/SetCommand.cs:28-294) |

### A.3 `CRED` — Credentials & Secret Storage (13 tools)

| Tool | Purpose | Pipeline role | Origin |
|---|---|---|---|
| `CRED DISABLE` | Disable the secret-store tier (always permitted on every platform), optionally purging every ChatDbg entry from the backend. | source (refuses piped input) | ported:the false half of /set useWindowsCredentialManager |
| `CRED DOCTOR` | Diagnose credential readiness per provider end to end - both halves of a key pair individually, winning tier, shadowing, store reachability, the SDK ambient chain, plaintext copies and file permissions - with remediation text. | filter (one piped chunk = one provider name; one chunk per check plus a verdict; source when unpiped) | ported:startup credential diagnostics (ChatShell.cs:239-321), with the half-key-pair and wrong-provenance defects corrected |
| `CRED ENABLE` | Enable the OS secret-store credential tier after explicit y/yes consent, selecting a backend; declining is a success, not a failure. | source (refuses piped input with an explanatory string - consent must not derive from an upstream stage) | ported:/set enablewincred + the true half of /set useWindowsCredentialManager |
| `CRED ENV` | Name the environment variables each slot reads, in priority order, with occupancy and shell-specific copy-paste templates carrying placeholders. | filter (one piped chunk = one slot id; source when unpiped) | **NEW** |
| `CRED LIST` | List every credential slot with a masked status (***set*** / (not set)) and the channel it resolved from, never a value. | filter (accepts piped slot ids via UsePipe; emits one row chunk per slot; also runs as a source with no pipe) | ported:credential block of bare `/set` (SetCommand.cs:401-463) + ChatSettings.GetCredentialSource |
| `CRED MIGRATE` | Move credentials out of the deprecated settings-file fields into a store or environment variables, emitting placeholders instead of plaintext, with per-slot truthful outcomes and abort-before-purge safety. | filter (one piped chunk = one slot id; one outcome chunk per slot plus a summary) | ported:/set migrate wizard (SettingsService.cs:196-268), env-instruction printer (:328-360), bulk vault migration (:289-326) |
| `CRED REDACT` | Mask live credential values and credential-shaped patterns in text flowing through the pipe; overrides Main so it also scrubs upstream FAILURE chunks' ErrorMessage. | filter (requires piped input; one chunk in, one chunk out, preserving IsSuccess, CorrelationId and OutputFormat) | **NEW** |
| `CRED REMOVE` | Delete a credential entry from a store and/or clear the deprecated plaintext settings field; idempotent, confirmed, never touches the environment tier. | filter (one piped chunk = one slot id; one outcome chunk per deletion) | **NEW** |
| `CRED ROTATE` | Replace a stored secret, verify by read-back, optionally keep a backup entry, and warn when a higher-priority tier still shadows the new value. | filter/sink (one piped chunk = the new secret value; emits one summary chunk) | **NEW** |
| `CRED SCAN` | Find plaintext credentials on disk or occupied environment variables and report them with salted 4-char fingerprints, severities and file-permission findings - never a value. | filter (one piped chunk = one filesystem path; one chunk per finding; source when unpiped) | **NEW** |
| `CRED SET` | Store a secret in an OS secret store. Deliberately has NO value parameter - the secret arrives from a no-echo prompt or as one piped chunk. | filter/sink (one piped chunk = one complete secret value; emits one confirmation chunk) | ported:/set wincred <credential-type> <value> (SetCommand.cs:228-250, SettingsService.cs:145-194) |
| `CRED SOURCE` | Report which tier supplied one credential, with -all expanding the full priority ladder and marking the winner. | filter (one piped chunk = one slot id; one provenance chunk out; source when unpiped) | ported:ChatSettings.GetCredentialSource(string) + the startup 'credentials loaded from:' line |
| `CRED STORES` | Enumerate secret-store backends with LIVE availability, capabilities, roaming/encryption facts, max secret length and the reason any backend is unusable. | source (one chunk per backend; refuses piped input) | **NEW** |

### A.4 `PROMPT` — System Prompt Library (14 tools)

| Tool | Purpose | Pipeline role | Origin |
|---|---|---|---|
| `PROMPT COPY` | Duplicate a prompt under a new name so a persona can be forked without re-typing it. | source | **NEW** |
| `PROMPT CREATE` | Create a new prompt with content from a pipe, a file, inline text, or the built-in default text. | sink | ported:/prompt create <n> [description] (and GUI Create dialog) |
| `PROMPT DELETE` | Delete a prompt, with the active-prompt guard, a No-defaulted confirmation and a single-generation backup. | filter | ported:/prompt delete <n> (and GUI Delete button) |
| `PROMPT DOCTOR` | Diagnose the library: unreadable files, name/file mismatches, sanitization collisions, dangling active name, missing built-ins; -fix applies only reversible repairs. | source | **NEW** |
| `PROMPT EDIT` | Replace a prompt's content and/or description via pipe, file, inline text, $EDITOR, or END-terminated interactive entry. | sink | ported:/prompt edit <n> (and GUI Edit dialog) |
| `PROMPT EXPORT` | Export a prompt into the exchange directory as text (content only) or JSON (lossless round trip). | filter | ported:/prompt export <n> [file_path] |
| `PROMPT IMPORT` | Import a prompt from a bare file name inside the confined exchange directory, as text or as a full JSON record. | filter | ported:/prompt import <n> <file_path> [description] |
| `PROMPT LIST` | Enumerate, filter and sort the prompt library; emits one chunk per prompt in text/names/csv/json. | source | ported:/prompt list |
| `PROMPT PATH` | Report the store, exchange and backup directories, how each was resolved, and whether each is writable. | source | **NEW** |
| `PROMPT RENAME` | Rename a prompt while preserving description, createdAt and lastUsedAt, and follow the active selection. | not-pipeable | **NEW** |
| `PROMPT RESET` | Restore missing (or, with -force, modified) built-in starter prompts explicitly instead of relying on empty-store auto-seeding. | source | **NEW** |
| `PROMPT SHOW` | Show one prompt with its description and timestamps, or only its content. | filter | ported:/prompt show <n> (and GUI Show button) |
| `PROMPT STATUS` | Show the active system prompt, its text and metadata; -content-only emits just the text for piping. | source | ported:/prompt (bare, no arguments) |
| `PROMPT USE` | Select the active prompt: publishes name and text to the global environment and stamps last-used. | filter | ported:/prompt use <n>, /set systemPrompt <n>, GUI Use button |

### A.5 `MODEL` — Model Backends (11 tools)

| Tool | Purpose | Pipeline role | Origin |
|---|---|---|---|
| `MODEL CAPS` | Report per-backend capabilities and ceilings: log-probabilities, top-K reporting max, prompt-side logprobs, streaming, tool calling, temperature/token/context ceilings, cancellation; states declared vs verified. | filter (one chunk = one backend key in; one chunk per backend or per capability row out) | **NEW** |
| `MODEL CATALOG` | List the models, deployments or GGUF files a backend can actually serve, assigning token-safe short handles; caches with TTL. | source (one chunk per catalog row; -format ids emits bare ids for piping; refuses piped input) | **NEW** |
| `MODEL LIST` | Enumerate every registered model backend with key, display name, readiness and active marker; emits one chunk per backend. | source (produces piped output, one chunk per backend; refuses piped input with an explanatory chunk; overrides Main to emit multiple chunks) | ported:shell provider registry (Dictionary<string,IAIService> keyed azure/bedrock/llama) + console start-up configuration self-check block |
| `MODEL LOAD` | Bring a GGUF weight file resident with its hardware-acceleration settings; strict teardown ordering on reload; -force applies changed hardware settings the source could only apply on a path change. | filter (one chunk = one weight path or catalog handle, loaded in sequence with unload between; one summary chunk per load) | ported:LLamaSharpService.EnsureModelLoadedAsync (private lazy first-chat-turn side effect, promoted to an explicit tool) |
| `MODEL SELECT` | Set the model id / deployment name / weight-file handle for a backend, with per-backend validation and a -revision escape for ':'-suffixed ids stripped by the tokenizer. | filter (one chunk = one candidate model id or handle; the safe channel for values containing ':' '/' '\\') | ported:`/model <id…>` merged with `/set modelId <id>` (the two source commands wrote the same field under different rules) |
| `MODEL SHOW` | Describe one backend's configuration, effective request parameters after backend-specific clamping, and credential source label (never a value). | filter (accepts piped input where one chunk = a backend key; produces one record chunk per input) | ported:`/set` with no arguments (settings listing) + console start-up banner + `LLama Configuration:` block |
| `MODEL STATUS` | Report what is resident locally — path, size, load time, effective hardware settings, bound native backend and RID, honest context accounting — plus a drift table of settings that differ from the resident load. | source (one chunk per group: model, hardware, context, drift; refuses piped input) | **NEW** |
| `MODEL TEST` | Six-stage end-to-end probe: configured, credential source, shape, reachable, entitled (-deep, billable), capability agreement. | filter (one chunk = one backend key in; one chunk per stage out, failed stages followed by 'skipped' successes) | **NEW** |
| `MODEL TUNE` | Persist the five local hardware-acceleration settings (context, gpu-layers, threads, batch, device) with reject-not-clamp validation; -apply reloads so they take effect now; modifiesEnvironment:true. | source (one confirmation chunk; refuses piped input) | ported:`/set llamaContextSize|llamaGpuLayers|llamaGpuDevice|llamaThreads|llamaBatchSize` and the windowed shell's 'LLama Settings' tab |
| `MODEL UNLOAD` | Release the resident weight file, context and session; idempotent; confirmation-gated because it discards the engine's retained conversation state. | sink / not-pipeable (declines piped input with an explanatory chunk; emits one summary chunk, or none with -quiet) | ported:LLamaSharpService.Dispose teardown path (reachable only at shell shutdown, and only from one of the two shells) |
| `MODEL USE` | Make a registered backend active and report its readiness; registered modifiesEnvironment:true to write CHATDBG_PROVIDER. | filter (one chunk = one backend key; emits a confirmation row per switch) | ported:`/set provider <azure|bedrock|llama>` |

### A.6 `TOKEN` — Token Introspection (13 tools)

| Tool | Purpose | Pipeline role | Origin |
|---|---|---|---|
| `TOKEN ATTRIBUTE` | Attribute each generated token to the prompt tail or the look-back window of prior tokens that influenced it, with a real recency weight and a named method. | both (filter-shaped: passes analysis/token chunks through and interleaves attribution chunks) | ported:/inspect stage E and TokenInspectionService.BuildAttributionMap |
| `TOKEN CAPTURE` | Configure per-token confidence capture: on/off, top-K 1-20 (default 5), display mode, layout, grid max alternatives, sample size, generation budget, low-confidence threshold; bare form reports status. | neither by default (empty success, like built-in SET); source with -print (key=value lines); filter when fed key=value assignments | ported:/logprobs [enable|disable|top n|showall|showsample|grid|list|gridmaxalt n] and /set enableLogProbabilities|logProbabilitiesTopK|showAllTokens|gridViewForTokens|gridViewMaxAlternatives |
| `TOKEN DEMO` | Emit a deterministic, cross-platform-reproducible 25-token sample analysis with no model call; every record stamped synthetic:true. | source | ported:/demologprobs (DemoLogProbsCommand) |
| `TOKEN DIAG` | Explain why token confidence is or is not available: current policy, provider/model, backend capability probe, and the actual API version the provider will send. | source | ported:/logprobs debug |
| `TOKEN DIFF` | Compare two analyses token by token with text or index alignment, reporting per-position probability shift, band change, alternatives gained/lost, and a summary with perplexity delta. | filter (piped stream is the 'against' side; baseline named as an ordered parameter) | **NEW** |
| `TOKEN EXPORT` | Write an analysis to json/jsonl/csv/md in the analysis directory, with -redact to strip all content and keep the numbers, and confirmation before overwrite. | sink (accumulates a piped analysis stream, writes one file per analysis id, emits one confirmation line) | ported:export-analysis (ExportTokenAnalysisCommand stub) and LLamaSharpService.SaveAnalysesToJson |
| `TOKEN FILTER` | Keep only token records matching a confidence predicate (-below/-above/-band/-marginbelow/-range), or apply the source's beginning/middle/end sampling rule; rejects via empty-success so the host drops them. | filter (primary form); also operates on a named or last analysis when not piped | **NEW** |
| `TOKEN INSPECT` | Composite run: tokenize, generate, map probabilities and attribute in one analysis, with a pipeline-safe consent gate via io.PromptForCommand and a -yes bypass. | both (source and filter, emitting the union of all three stages for one analysis id) | ported:/inspect (InspectCommand) |
| `TOKEN LOAD` | Read a previously exported analysis back into the session and stream it record by record, with schema-version checking and path containment. | source | **NEW** |
| `TOKEN MAP` | Map the probability distribution the model weighed at each generation step: chosen token plus top-K alternatives, streamed as generation proceeds. | both (source streams one chunk per generated token; filter: one piped chunk = one prompt) | ported:TokenInspectionService.GenerateProbabilityMap and /inspect stage D |
| `TOKEN SHOW` | Render an analysis as readable text in list or grid layout, preserving the source's escaping, 2-decimal percentage format and '(+ N more)' alternatives suffix; marks synthetic and estimated data. | sink-shaped filter (one document record in, rendered lines out as ResultFormat.General) | ported:show-analysis (ShowTokenAnalysisCommand stub) and the console shell's render path |
| `TOKEN SPLIT` | Split text into model tokens with real decoded text, vocabulary IDs and character spans; emits an analysis document. | both (source: overrides Main to emit analysis chunk + one token chunk per token; filter: one piped chunk = one text to tokenize) | ported:/tokenize (TokenizeCommand) and TokenInspectionService.AnalyzePrompt |
| `TOKEN STATS` | Compute entropy (truncated top-K, nats or bits), normalized entropy, perplexity, mean negative log-likelihood, margin, confidence-band counts and low-confidence ratio over an analysis. | both (overrides Main: passes every chunk through and appends one stats chunk per analysis id at end of stream; -perstep enriches token chunks in place) | **NEW** |

### A.7 `VIEW` — Presentation & Visualization (13 tools)

| Tool | Purpose | Pipeline role | Origin |
|---|---|---|---|
| `VIEW CAPS` | Probe the terminal once and publish width, height, colour depth, redirection, Unicode support, wide-character support, mouse support and platform as CHATDBG_VIEW_CAPS_* globals; -assume forces a fixed profile for deterministic tests and CI. | source — produces one report chunk; declares ResultFormat.JSON under -format json because the output is genuinely machine-readable and meant to be filtered | **NEW** |
| `VIEW HEAT` | Paint generated text as a continuous flowing paragraph with each token's background coloured by confidence, honouring the 6-band background palette and the black/white contrast flip at 0.5. | filter — one JSON token record per chunk in, ResultFormat.General rows out; streams when drawing all records, buffers when sampling | ported:LogProbHeatmapView.cs (defined in source but never instantiated) and heat map D4 |
| `VIEW LAYOUT` | Set or report layout (grid/list/flow), alternatives budget (1-20, default 5), probability precision and show-all-vs-sample volume mode; persists through the Configuration package by default. | sink with pipe-fed parameter — ordered 'mode' declares UsePipe=true, returns empty success when piped (the framework SET idiom); emits one confirmation line when not piped | ported:/logprobs grid|list|showall|showsample|gridmaxalt and /set gridViewForTokens|showAllTokens|gridViewMaxAlternatives |
| `VIEW PALETTE` | Select or inspect the probability-to-colour mapping among the source's four contradictory heat maps, declare the scale (unit 0-1 vs percent 0-100) so values are converted rather than misread, and diagnose a single probability with -check. | sink with pipe-fed parameter — ordered 'name' declares UsePipe=true; -show and -check produce ResultFormat.General text | ported:heat maps D1-D4 (TokenFormatters.cs:38-45, ChatShell.cs:657-672, ChatWindow.cs:99-113, LogProbHeatmapView.cs:60-77); the selection surface, scale declaration and mono palette are new |
| `VIEW PLAIN` | Strip styling markup and ANSI sequences (or escape them, or resolve them to real escapes), fold decoration to ASCII and expand tabs — the correct answer to redirected output. | filter — one text line per chunk in, one plain line per chunk out (HandlePipedChunk); re-declares ResultFormat.General | ported:BasicConsoleFormatter.cs:17-22 and :152-179 markup stripper, plus SpectreConsoleFormatter.cs:230-235 bracket escaping |
| `VIEW PREVIEW` | Render a built-in deterministic fixture (seed 42, the source's 25-token sample sentence and hand-written alternative sets) through the current settings, so layout, theme and palette can be seen without a backend call. | source — ResultFormat.General; piped input gets an explanatory refusal | **NEW** |
| `VIEW RULE` | Emit an 80-character horizontal rule with an optional caption, preserving the centred (exactly 80 chars) and left-justified (exactly 79 chars) geometries and the markup-stripped caption measurement. | source — not pipeable as a filter; given piped input it returns an explanatory string and succeeds without consuming the pipe | ported:IConsoleFormatter.WriteRule and BasicConsoleFormatter.cs:41-66 |
| `VIEW SAMPLE` | Reduce a long record stream to beginning/middle/end slices using the source's exact arithmetic (size 5, threshold 15, middle start count/2-2, end start count-5) or the 'flow' preset (size 10, threshold 30, middle start floor((count-10)/2)). | filter — buffers the stream (bounded by -max, default 100000), re-emits surviving records unmodified and echoes the upstream OutputFormat; record indices are never rewritten | ported:E1 sampling in ChatShell.cs:435-481 / TokenProbabilityVisualizer.cs:39-88 / DemoLogProbsCommand.cs:179-200, and E2 in LogProbHeatmapView.cs:79-88 |
| `VIEW STATUS` | Report the current presentation settings — display mode, view mode, alternatives budget, theme, palette and scale, colour policy, glyph policy and width — with provenance under -verbose. | source — ResultFormat.General, or ResultFormat.JSON under -format json; reads every key with storeDefault:false so asking cannot cause a write-back | ported:the display half of LogProbsCommand.cs:129-156 and the /set report lines |
| `VIEW THEME` | Select the colour theme (auto/dark/light/contrast/mono), the colour policy (auto/always/never), the glyph policy (auto/on/off) and a fixed render width — the switches that make redirected-output degradation and deterministic tests possible. | sink with pipe-fed parameter — ordered 'name' declares UsePipe=true; emits a confirmation line or a swatch under -preview | ported:ThemeManager.cs:16-44 supplies the dark palette slot-for-slot; the colour/glyph/width policy layer is new |
| `VIEW TOKENS` | Render token-probability records as a dense card grid or a detail table, preserving the source's 90-char ASCII frame (inner widths 7/20/12/38), 17+'...' token truncation, 7+'...' alternative truncation, responsive max(1, width/40) grid columns and the '+ N more' overflow indicator. | filter — accepts one JSON token record per chunk, produces ResultFormat.General rendered text; overrides Main because table frames and grid row packing are not 1:1 with input chunks | ported:IConsoleFormatter.DisplayTokenGrid/DisplayTokenTable, BasicConsoleFormatter.cs:72-112, SpectreConsoleFormatter.cs:55-195, ChatShell.cs:412-687 |
| `VIEW TRANSCRIPT` | Render chat messages as an aligned, wrapped transcript: '[role]' banner, 2-column padding, 75% bubble width, right-aligned user messages, blank-line separation and the diamond probability marker. | filter — one JSON chat message per chunk in, one fully rendered block per chunk out (HandlePipedChunk, genuinely 1:1) | ported:ChatWindow.cs:500-622 transcript rendering and the wrapper at :735-788 |
| `VIEW WRAP` | Wrap text to a width measured in display cells rather than code units, with indent, hanging indent and alignment, clamped so no input can loop or throw. | filter — one text line per chunk in, one wrapped block per chunk out; refuses with a Failure if the upstream chunk declares JSON rather than corrupting a record | ported:ChatWindow.cs:735-788 wrapping logic, extracted as its own bounded, cell-aware tool |

### A.8 `DIAG` — Diagnostics & Observability (12 tools)

| Tool | Purpose | Pipeline role | Origin |
|---|---|---|---|
| `DIAG AUDIT` | Configure structured command-execution auditing, its sink and its redaction policy, and report the framework's real masking limitation. | not-pipeable | **NEW** |
| `DIAG BUILDINFO` | Report the identity and inferred publish profile of the running artefact plus, with -packages, the loaded tool-package inventory and their load-time verification status. | source | **NEW** |
| `DIAG CAPTURE` | Arm/disarm engine diagnostic capture and configure the recorder's sinks, buffer threshold, level filter, file-name pattern, timestamp mode and redaction. | not-pipeable | ported:LLamaSharpLogConfig.ConfigureLogging() plus the five compile-time-only switches (LogDirectory, EnableFileLogging, EnableDebugOutput, EnableConsoleOutput, MaxBufferSize) |
| `DIAG CLEAR` | Discard the in-memory diagnostic buffer without writing it anywhere; requires -force, offers -flush as the non-destructive alternative. | not-pipeable | ported:LLamaSharpLogConfig.ClearLogs() (operation A5) |
| `DIAG DOCTOR` | Environment health check across runtime, build profile, backend availability, native library resolution, credential resolution provenance, configuration validity and storage; one chunk per check. | source / filter (piped chunk = one check id or group name) | **NEW** |
| `DIAG EXPORT` | Write diagnostic entries to a caller-named file suitable for attaching to a bug report, sourced from the buffer, today's file, both merged, or every retained day. | sink (piped chunk = one line to write); also usable non-piped | ported:Commands/ExportLogsCommand.cs (the unregistered stub) + LLamaSharpLogConfig.SaveLogsToFile(path) (operation A7) |
| `DIAG FILES` | List the rolling log files (path, day, bytes, mtime, pattern) and, with -roots, the product's three per-user state locations and their writability. | source (emits one absolute path per chunk, directly consumable by TAIL/ROTATE/EXPORT) | **NEW** |
| `DIAG FLUSH` | Append the buffered entries to today's rolling log file and empty the buffer; report what was written or why nothing was. | not-pipeable | ported:LLamaSharpLogConfig.FlushToFile() (operation A6) |
| `DIAG RECORD` | Record an application-level entry at a given level and tag; as a pipeline stage, journal every chunk that passes through and re-emit it verbatim (or swallow it with -quiet). | filter (tee) / sink with -quiet | ported:LLamaSharpLogConfig.LogMessage(level, message) (operation A3) |
| `DIAG ROTATE` | Apply retention policy to the rolling log files: prune by age, cap total size, roll oversized files, optionally gzip. Dry-run by default; -apply required to modify anything. | source / filter (piped chunk = one candidate file path) | **NEW** |
| `DIAG STATUS` | Report capture state, engine-sink health, buffer occupancy, resolved policy, fault count and last internal fault. | source | **NEW** |
| `DIAG TAIL` | Emit the most recent diagnostic entries from the buffer, the daily files, or both merged, with level/regex/since filtering; one chunk per entry. | source / filter (piped chunk = one log-file path) | ported:LLamaSharpLogConfig.GetLogs() (operation A4, which had no user-facing surface) |

### A.9 `PKG` — Tool Package Management (13 tools)

| Tool | Purpose | Pipeline role | Origin |
|---|---|---|---|
| `PKG DOCTOR` | Run fifteen path, layout, digest, signature, metadata, TFM/RID and manifest checks - without loading any package code - to explain why a package was silently skipped. | filter (one chunk per finding; errors are failure chunks) | **NEW** |
| `PKG INSTALL` | Download to quarantine, re-read identity from the artefact, verify signature and digests, extract safely into {root}/{id}/{version}/bin, record digests; installs SANDBOXED, never trusted. | filter (one chunk = one quoted id[@version]; one verdict chunk per input) | ported:Xcaciv.Command.Packages/InstallCommand + NugetWrapper.InstallPackage (both stubs in Cupcake; steps 5-10 are new) |
| `PKG LIST` | Enumerate installed packages with version, trust level, load state, root, command count; -fields id emits the identity shape every other tool consumes. | source (one chunk per package; JSON/CSV result format) | **NEW** |
| `PKG LOCK` | Write, check or restore a portable lockfile of package identities and relative-path content digests; regenerates the machine-local absolute-path hash store on restore. | filter (CHECK emits drifted/missing ids straight into PKG INSTALL) | **NEW** |
| `PKG RELOAD` | Verify and re-scan the package roots, ask the host to register newly present packages, and report the honest diff including what stays registered until restart. | source (refuses piped input) | **NEW** |
| `PKG REMOVE` | Uninstall a package, purge its digest and trust records, defer file deletion when assemblies are locked; destructive, typed confirmation. | filter (piped removal requires -force) | **NEW** |
| `PKG SEARCH` | Search a package feed for installable tool packages; quiet/normal/detailed rows, take clamped to 1-100, HTTPS-only sources. | source (refuses piped input; emits one chunk per result row) | ported:Xcaciv.Command.Packages/SearchCommand (Cupcake ref-cupcake §7) |
| `PKG SHOW` | Manifest, declared commands (metadata-only), files with digest prefixes, trust record and dependency probe paths for one package - the evidence for a trust decision. | filter (one chunk = one id[@version]) | **NEW** |
| `PKG SOURCE` | List, add, remove, select, test and map package feeds; enforces HTTPS, requires a package-source mapping per feed, stores credential slot names never secrets. | filter for ADD (one chunk = the feed URL, the only lossless channel), source for LIST, refuses input elsewhere | **NEW** |
| `PKG TRUST` | Interactively grant a verified package the environment-modifying rights its manifest declares, recorded against a named digest and signature. | not-pipeable (in either direction; no -force exists) | **NEW** |
| `PKG UNTRUST` | Revoke a grant, optionally drop the digest allowlist entries or block the package from loading entirely. | filter (mass revocation with -force) | **NEW** |
| `PKG UPDATE` | Check for and apply newer versions within a semver bound, installing beside the current version with rollback retention; an update always resets trust to sandboxed. | filter (-check emits bare ids re-consumable by the same tool) | **NEW** |
| `PKG VERIFY` | Re-hash installed packages against the trust allowlist and re-check signatures; reports changed/missing/unknown/signature-failed before it becomes a failed start. | filter (one chunk = one id; unhealthy verdicts are failure chunks) | **NEW** |

**Total: 117 tools.**


---

## Appendix B — Environment key registry

Every key any tool reads or writes. The host seeds the read-only ones at startup; the rest are written by the tool that owns them. Collisions across packages are a design error — this table exists so they are visible.

| Key | Declared by |
|---|---|
| `AWS_ACCESS_KEY_ID` | CRED, DIAG, MODEL, SET |
| `AWS_SECRET_ACCESS_KEY` | CRED, DIAG, MODEL, SET |
| `AWS_SESSION_TOKEN` | MODEL |
| `CATALOG_FETCHED_AT` | MODEL |
| `CHATDBG_ALLOW_USER_PACKAGES` | PKG |
| `CHATDBG_AWS_ACCESS_KEY` | CRED, DIAG, MODEL, SET |
| `CHATDBG_AWS_REGION` | MODEL, SET |
| `CHATDBG_AWS_SECRET_KEY` | CRED, DIAG, MODEL, SET |
| `CHATDBG_AZURE_API_KEY` | CRED, DIAG, MODEL, SET |
| `CHATDBG_AZURE_API_VERSION` | MODEL, SET |
| `CHATDBG_AZURE_ENDPOINT` | MODEL, SET |
| `CHATDBG_DATA_ROOT` | PKG |
| `CHATDBG_DIAG_ARMED` | DIAG |
| `CHATDBG_DIAG_AUDIT` | DIAG |
| `CHATDBG_DIAG_AUDIT_PATH` | DIAG |
| `CHATDBG_DIAG_AUDIT_SINK` | DIAG |
| `CHATDBG_DIAG_BUFFER` | DIAG |
| `CHATDBG_DIAG_COMPRESS` | DIAG |
| `CHATDBG_DIAG_CONSOLE` | DIAG |
| `CHATDBG_DIAG_DIR` | DIAG |
| `CHATDBG_DIAG_FILE` | DIAG |
| `CHATDBG_DIAG_LEVEL` | DIAG |
| `CHATDBG_DIAG_MAX_FILE_MB` | DIAG |
| `CHATDBG_DIAG_MAX_TOTAL_MB` | DIAG |
| `CHATDBG_DIAG_PATTERN` | DIAG |
| `CHATDBG_DIAG_REDACT` | DIAG |
| `CHATDBG_DIAG_RETAIN_DAYS` | DIAG |
| `CHATDBG_DIAG_TIMESTAMPS` | DIAG |
| `CHATDBG_DIAG_TRACE` | DIAG |
| `CHATDBG_ENABLE_LOGPROBS` | CHAT |
| `CHATDBG_FRONTEND` | PKG |
| `CHATDBG_GRID_MAX_ALT` | SET |
| `CHATDBG_GRID_VIEW` | SET |
| `CHATDBG_HOME` | SET |
| `CHATDBG_LLAMA_BATCH_SIZE` | MODEL, SET |
| `CHATDBG_LLAMA_CONTEXT_SIZE` | MODEL, SET |
| `CHATDBG_LLAMA_GPU_DEVICE` | MODEL, SET |
| `CHATDBG_LLAMA_GPU_LAYERS` | MODEL, SET |
| `CHATDBG_LLAMA_THREADS` | MODEL, SET |
| `CHATDBG_LOGPROBS_ENABLED` | MODEL, SET |
| `CHATDBG_LOGPROBS_TOPK` | CHAT, MODEL, SET |
| `CHATDBG_MAX_TOKENS` | CHAT, MODEL, SET |
| `CHATDBG_MODEL_ID` | CHAT, MODEL, SET |
| `CHATDBG_MODEL_PATHS` | MODEL |
| `CHATDBG_OUTPUT_FORMAT` | PKG |
| `CHATDBG_PACKAGE_DIR` | PKG |
| `CHATDBG_PKG_COUNT` | PKG |
| `CHATDBG_PKG_LAST_VERIFY` | PKG |
| `CHATDBG_PKG_LOADED` | PKG |
| `CHATDBG_PKG_LOCK_PATH` | PKG |
| `CHATDBG_PKG_NETWORK` | PKG |
| `CHATDBG_PKG_PRERELEASE` | PKG |
| `CHATDBG_PKG_QUARANTINE_DIR` | PKG |
| `CHATDBG_PKG_RELOAD_PENDING` | PKG |
| `CHATDBG_PKG_SIGNATURE_POLICY` | PKG |
| `CHATDBG_PKG_SOURCE` | PKG |
| `CHATDBG_PKG_TIMEOUT` | PKG |
| `CHATDBG_PROFILE` | SET |
| `CHATDBG_PROMPT_ALLOW_EDITOR` | PROMPT |
| `CHATDBG_PROMPT_DIR` | PROMPT |
| `CHATDBG_PROMPT_EXCHANGE_DIR` | PROMPT |
| `CHATDBG_PROMPT_MAX_CHARS` | PROMPT |
| `CHATDBG_PROMPT_SEED` | PROMPT |
| `CHATDBG_PROVIDER` | CHAT, MODEL, SET |
| `CHATDBG_SETTINGS_FILE` | SET |
| `CHATDBG_SHOW_ALL_TOKENS` | SET |
| `CHATDBG_SYSTEM_PROMPT` | PROMPT |
| `CHATDBG_SYSTEM_PROMPT_BODY` | MODEL |
| `CHATDBG_SYSTEM_PROMPT_NAME` | CHAT, SET |
| `CHATDBG_SYSTEM_PROMPT_TEXT` | PROMPT |
| `CHATDBG_TEMPERATURE` | CHAT, MODEL, SET |
| `CHATDBG_TRUST_STORE` | PKG |
| `CHATDBG_USER_PACKAGE_DIR` | PKG |
| `CHATDBG_USE_OS_KEYSTORE` | MODEL |
| `CHATDBG_VERBOSE` | PKG |
| `CHATDBG_VIEW_CAPS_COLOR` | VIEW |
| `CHATDBG_VIEW_CAPS_COLUMNS` | VIEW |
| `CHATDBG_VIEW_CAPS_MOUSE` | VIEW |
| `CHATDBG_VIEW_CAPS_PLATFORM` | VIEW |
| `CHATDBG_VIEW_CAPS_REDIRECTED` | VIEW |
| `CHATDBG_VIEW_CAPS_ROWS` | VIEW |
| `CHATDBG_VIEW_CAPS_UNICODE` | VIEW |
| `CHATDBG_VIEW_CAPS_WIDECHARS` | VIEW |
| `CHATDBG_VIEW_COLOR` | VIEW |
| `CHATDBG_VIEW_LAYOUT` | VIEW |
| `CHATDBG_VIEW_MAXALT` | VIEW |
| `CHATDBG_VIEW_PALETTE` | VIEW |
| `CHATDBG_VIEW_PRECISION` | VIEW |
| `CHATDBG_VIEW_SCALE` | VIEW |
| `CHATDBG_VIEW_SHOWALL` | VIEW |
| `CHATDBG_VIEW_THEME` | VIEW |
| `CHATDBG_VIEW_UNICODE` | VIEW |
| `CHATDBG_VIEW_WIDTH` | VIEW |
| `CHAT_EXPORT_ROOT` | CHAT |
| `CHAT_IMPORT_ROOT` | CHAT |
| `CHAT_LAST_ELAPSED_MS` | CHAT |
| `CHAT_LAST_EXPORT_PATH` | CHAT |
| `CHAT_LAST_IMPORT_PATH` | CHAT |
| `CHAT_LAST_TURN_ROLE` | CHAT |
| `CHAT_SESSION` | CHAT |
| `CHAT_SESSION_ID` | CHAT |
| `CHAT_SESSION_ROOT` | CHAT |
| `CHAT_TURN_COUNT` | CHAT |
| `CLICOLOR` | VIEW |
| `CLICOLOR_FORCE` | VIEW |
| `COLORTERM` | VIEW |
| `COLUMNS` | VIEW |
| `CRED_CONFIRM` | CRED |
| `CRED_LAST_DOCTOR_VERDICT` | CRED |
| `CRED_LAST_MIGRATION_UTC` | CRED |
| `CRED_LAST_REMOVE_SLOT` | CRED |
| `CRED_LAST_ROTATE_SLOT` | CRED |
| `CRED_LAST_ROTATE_UTC` | CRED |
| `CRED_LAST_SCAN_FINDINGS` | CRED |
| `CRED_LAST_WRITE_BACKEND` | CRED |
| `CRED_LAST_WRITE_SLOT` | CRED |
| `CRED_PROFILE` | CRED |
| `CRED_REDACT_COUNT` | CRED |
| `CRED_REDACT_MINLENGTH` | CRED |
| `CRED_REDACT_MODE` | CRED |
| `CRED_REDACT_TOKEN` | CRED |
| `CRED_SETTINGS_PATH` | CRED |
| `CRED_STORE_BACKEND` | CRED |
| `CRED_STORE_ENABLED` | CRED |
| `ComSpec` | CRED |
| `ConEmuANSI` | VIEW |
| `DOCTOR_LAST_RUN` | DIAG |
| `DOCTOR_LAST_STATUS` | DIAG |
| `EDITOR` | PROMPT |
| `EXPORT_LAST_PATH` | DIAG |
| `FORCE_COLOR` | VIEW |
| `HTTPS_PROXY` | PKG |
| `LANG` | VIEW |
| `LC_ALL` | VIEW |
| `LC_CTYPE` | VIEW |
| `LINES` | VIEW |
| `LIST_FORMAT` | MODEL |
| `LOAD_EFFECTIVE_CONTEXT` | MODEL |
| `LOAD_EFFECTIVE_GPU_LAYERS` | MODEL |
| `LOAD_RESIDENT_AT` | MODEL |
| `LOAD_RESIDENT_PATH` | MODEL |
| `MAXTOKENS` | TOKEN |
| `MODELID` | TOKEN |
| `NO_COLOR` | VIEW |
| `NO_PROXY` | PKG |
| `NUGET_LOCAL_PACKAGES` | PKG |
| `PROVIDER` | TOKEN |
| `PSModulePath` | CRED |
| `RECORD_CHUNKS` | DIAG |
| `SEARCH_TAKE` | PKG |
| `SEARCH_VERBOSITY` | PKG |
| `SET_AUTOSAVE` | SET |
| `SET_AWSREGION` | CRED |
| `SET_AZUREENDPOINT` | CRED |
| `SET_BASEDIR` | SET |
| `SET_CONFIRM` | SET |
| `SET_CULTURE` | SET |
| `SET_FILE` | SET |
| `SET_LAST_PROFILE` | SET |
| `SET_MODELID` | CRED |
| `SET_PROFILE` | SET |
| `SET_PROVIDER` | CRED |
| `SET_READONLY` | SET |
| `SET_REDACT` | SET |
| `SET_STRICT` | SET |
| `SHELL` | CRED |
| `TEMPERATURE` | TOKEN |
| `TERM` | VIEW |
| `TERM_PROGRAM` | VIEW |
| `TEST_LAST_RESULT` | MODEL |
| `TEST_LAST_RUN_AT` | MODEL |
| `TOKEN_CAPTURE` | TOKEN |
| `TOKEN_CONTEXT` | TOKEN |
| `TOKEN_EXPORTDIR` | TOKEN |
| `TOKEN_GRIDMAXALT` | TOKEN |
| `TOKEN_KEEP` | TOKEN |
| `TOKEN_LAST` | TOKEN |
| `TOKEN_LASTEXPORT` | TOKEN |
| `TOKEN_LAYOUT` | TOKEN |
| `TOKEN_LOWCONF` | TOKEN |
| `TOKEN_MAXGEN` | TOKEN |
| `TOKEN_MODE` | TOKEN |
| `TOKEN_SAMPLESIZE` | TOKEN |
| `TOKEN_TOPK` | TOKEN |
| `USE_PREVIOUS` | MODEL |
| `VERIFY_LAST` | PKG |
| `VISUAL` | PROMPT |
| `WT_SESSION` | VIEW |

**188 distinct keys.** 20 are declared by more than one package and need an ownership decision: `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `CHATDBG_AWS_ACCESS_KEY`, `CHATDBG_AWS_REGION`, `CHATDBG_AWS_SECRET_KEY`, `CHATDBG_AZURE_API_KEY`, `CHATDBG_AZURE_API_VERSION`, `CHATDBG_AZURE_ENDPOINT`, `CHATDBG_LLAMA_BATCH_SIZE`, `CHATDBG_LLAMA_CONTEXT_SIZE`, `CHATDBG_LLAMA_GPU_DEVICE`, `CHATDBG_LLAMA_GPU_LAYERS`, `CHATDBG_LLAMA_THREADS`, `CHATDBG_LOGPROBS_ENABLED`, `CHATDBG_LOGPROBS_TOPK`, `CHATDBG_MAX_TOKENS`, `CHATDBG_MODEL_ID`, `CHATDBG_PROVIDER`, `CHATDBG_SYSTEM_PROMPT_NAME`, `CHATDBG_TEMPERATURE`

---

## Appendix C — Open design questions

Questions the package authors could not settle. Each needs a decision or an explicit deferral in the architecture document.


### C.1 `CHAT` — Session & Conversation

1. Environment prefix confirmation: sub-commands are dispatched through their root, so the key CommandController passes to GetChild(commandName) should be CHAT, not SEND — meaning all twelve tools share one environment bucket keyed CHAT_. The chapter states this and flags it for verification at integration; if the framework actually keys per sub-command, every GetDefaultEnvironment declaration and every prefixed read in the package changes.
2. How IConversationBackend reaches a tool when the package is crawled from the plugin directory rather than registered in-process. CommandFactory only consults an IServiceProvider when Type.GetType resolves the type, which it will not for an isolated AssemblyContext load. The chapter's answer is that SEND/RETRY degrade with a stated failure in that case, but a host that wants a crawled package to talk to a backend needs a mechanism the framework does not currently offer.
3. Whether CHAT EXPORT should keep the source's silent overwrite as the default (chosen) or flip to -noclobber-by-default. The chapter preserves silent overwrite and removes the actual harm via atomic temp-and-rename, but this is the one preserved quirk most likely to be challenged.
4. Whether restoring the imported createdAt (a deliberate correction of source quirk Q7) can break byte-for-byte round-trip expectations for anyone diffing exported files against source-produced ones.
5. Confirmation UX inside a pipeline: PromptForCommand is only defined when HasPipedInput is false, so POP -count>1, CLEAR and RETRY refuse rather than prompt. If the host later gains an out-of-band confirmation channel, that refusal should become a prompt.
6. Whether -validate should default to warn (chosen) or off (exact source behaviour). warn changes observable output for existing files containing non-whitelisted roles.
7. Session file format and whether a session file is simply an exported conversation document plus a name/lastUsed sidecar, or a distinct schema. The chapter assumes the former but does not pin it.
8. Audit masking: the framework's shipped AuditMaskingConfiguration.ApplyMasking only rewrites -name=value tokens and is effectively non-functional for space-separated syntax, so CHAT SEND's full prompt text lands in audit events verbatim. The host must supply its own IAuditMaskingConfiguration; the exact contract for that is left to the host chapter.

### C.2 `SET` — Configuration & Profiles

9. Environment prefix for sub-commands: the framework prefixes seeded environment values with '{COMMANDNAME}_', but it is not settled in ref-command.md whether that name is the root token (SET) or the sub-command (SHOW). The spec assumes the root and reads defensively (SET_FILE, then bare FILE) until the host pins it with a test.
10. Whether -format yaml ships in v1: it costs a YamlDotNet 16.3.0 dependency in a package that otherwise has none. JSON and CSV are sufficient for every composition in the chapter.
11. Profile inheritance (a 'base' field so profile B inherits A and overrides three keys) is deliberately out of v1: it turns validation into a graph problem and SET DIFF into a three-way merge.
12. Whether the framework's widened boolean grammar (1/0/yes/no/on/off) should be adopted for boolean keys, as specified, or narrowed back to the source's strict true/false. The spec adopts the framework grammar deliberately.
13. Whether a corrupt settings document should block writes until an explicit SET RESET/SET IMPORT (as specified) or be silently overwritten on the next successful write (source behaviour).
14. Whether the host should run 'SET SHOW -format json | SET VALIDATE -strict' at startup in place of the source's ad-hoc configuration self-check, and what it should do with a non-clean result.

### C.3 `CRED` — Credentials & Secret Storage

15. Generalizing the store tier off Windows contradicts a source test that asserts a vault write returns false on every non-Windows host (WindowsCredentialManagerTests.cs:18-26). The spec deliberately generalizes; that test must be rewritten as 'wincred is unavailable off Windows' rather than deleted, and the product owner must sign off on the change.
16. Ownership of the settings-file tier: CRED emits `SET CLEARCRED <slot>` request chunks rather than editing settings.json. That inter-package request protocol is asserted here but not specified anywhere - the SET package chapter must define the chunk grammar, or the two packages must agree on a shared contract assembly.
17. The no-echo prompt convention (a prompt string prefixed `secret:` is read with echo suppressed) is a HOST requirement that IIoContext cannot express - PromptForCommand takes only a string. Either the ChatDbg IO context adopts the sentinel convention, or the framework needs a PromptForSecret member.
18. Whether `encfile` should exist at all. It is the only tier where this package stores a secret itself, and on a platform with no OS keystore it is either a real improvement over plaintext or a false sense of security depending on how the key is derived. The key-derivation policy is unspecified here.
19. Whether the per-machine salt used for CRED SCAN fingerprints should be shared across machines so that two machines' scans can be compared. Sharing it makes fingerprints correlatable (useful for fleet audits) and slightly more attackable.
20. The AWS provider's fallback to the SDK's own ambient credential chain is a fourth, undocumented credential channel the rebuild inherits unless it is closed. CRED DOCTOR reports it; nobody has decided whether the provider package should disable it.
21. Whether CRED ENABLE/DISABLE should be registered with modifiesEnvironment:true so the store flag is a global variable. The spec recommends no (CRED_-prefixed keys reach the private bucket without the grant), but that makes the flag invisible to tools that do not know the bucket convention.
22. Backup entries created by CRED ROTATE -keepbackup have no expiry and no lifecycle. Nothing prunes them, which reintroduces a milder version of the source's 'stored secrets are never cleaned up' problem.

### C.4 `PROMPT` — System Prompt Library

23. Registration mode for PROMPT USE: it needs modifiesEnvironment: true to publish CHATDBG_SYSTEM_PROMPT globally, but package-directory loading registers every discovered command with modifiesEnvironment: false. Is the package always registered in-process by the composition root (Cupcake path (a)), or must the store marker file <store>/.active become the primary selection channel with the environment as a mirror?
24. Ownership of persistence for the active prompt name: this chapter has USE publish to the environment and ChatDbg.Tools.Settings persist systemPromptName. That requires Settings to observe an environment change or to be invoked as a later pipeline stage (PROMPT USE | SET SAVE). Confirm with the Settings chapter which mechanism is contractual.
25. Does the host's IIoContext implementation propagate an output encoder and branch on ResultFormat? The framework never applies IOutputEncoder itself, so PROMPT LIST -format csv/json is metadata only unless the shell's HandleOutputChunk renders on it.
26. The argument tokenizer strips ':' '/' '\' ';' '<' '>' '{' '}' '+' '=' ',' even inside quotes. This chapter routes all paths and all real content around it (basenames, pipe, -file, interactive prompt). If the host is permitted to fork or wrap NamesValidator to preserve quoted spans verbatim, several parameters (-text, -description, real paths) could be restored — is that fork acceptable?
27. Backup policy: one generation in <store>/.backup is specified. Should PROMPT DOCTOR be able to restore from it (a RESTORE verb), or is that deliberately left to the exchange directory plus real version control?
28. Should the exchange directory default to the documents folder (source's command-path default) or to a dedicated ChatDbg subdirectory? The chapter keeps the documents folder for continuity, but a dedicated directory would make confinement easier to explain and to lock down.
29. Culture-sensitive collation is the default to match the source exactly, but the source's size-optimised publish profiles disabled globalization and silently changed it. Should the shipped release pin -collation ordinal by default and keep culture as opt-in?
30. PROMPT EDIT -editor spawns a process, the only such capability in the package. Confirm whether the restricted-host profile disables it by default (CHATDBG_PROMPT_ALLOW_EDITOR=false) rather than enabling it by default as specified here.

### C.5 `MODEL` — Model Backends

31. Argument tokenization strips ':' '/' '\\' and '=' even inside quoted spans (NamesValidator.InvalidParameterChars), so no filesystem path, endpoint URL, or Bedrock model id such as 'anthropic.claude-3-sonnet-20240229-v1:0' can survive the command line. The chapter routes around it with catalog handles, piped values, and a -revision parameter — but if the framework ever relaxes the filter, MODEL SELECT and MODEL LOAD should take raw values directly and the workaround should be retired.
32. GetDefaultEnvironment() values are seeded under the SUB-COMMAND name prefix (LOAD_, TEST_, CATALOG_), not the root, so private-bucket keys can collide with an identically-named sub-command in another package. Confirm whether the host should namespace buckets, or whether this package should abandon the private bucket entirely and keep its breadcrumbs in the process singleton.
33. Should the local satellite assembly be run out of process by default? An access violation inside the native engine is uncatchable and kills the shell with the user's unsaved conversation; a supervised subprocess isolates it but loses raw logits, prompt-token logits and custom sampling stages.
34. The source clamped the request-side top-K to 1-20 AND used the same setting as the local sampler's top-K, so local generation was always top-5 limited out of the box even with introspection off. Decide whether MODEL TUNE should expose a separate sampler top-K, or whether the coupling stays (and is merely disclosed by MODEL CAPS).
35. Fabricated log-probabilities are removed. Confirm with the product owner that the demo affordance the fabrication served is fully covered by an explicit demo command elsewhere, since MODEL CAPS will now report 'logprobs: no' for Bedrock where the source silently showed a populated visualisation.
36. Bedrock family detection is widened from the source's bare 'anthropic.' prefix to '^(?:[a-z]{2}\\.)?anthropic\\.' so cross-region inference profiles match. Confirm no other geography prefix shape is in use before pinning that regex.
37. The generation and model-load locks are process-wide, unbounded and un-cancellable in the source. This chapter specifies a bounded wait plus a status message; the timeout value is not yet chosen.
38. Catalog TTLs (15 min cloud, 60 s local) and the handle-stability guarantee are asserted, not derived from any source behaviour; they need a product decision.
39. MODEL CAPS -probe on a cloud backend can cost a billable completion. Confirm whether the default should stay 'declared only' or whether a host-level policy should forbid -probe entirely in shared/CI contexts.

### C.6 `TOKEN` — Token Introspection

40. Sibling package names and root commands are assumed, not confirmed: ChatDbg.Tools.Providers (AI), ChatDbg.Tools.Rendering (RENDER), ChatDbg.Tools.Settings (CONFIG), ChatDbg.Tools.History (CHAT), ChatDbg.Tools.Diagnostics (LOG). If the other chapters chose different names or roots, §6.1 and the compositions in §6.4 need to be reconciled.
41. How does a plugin-loaded command reach host services? Xcaciv.Command's CommandFactory activates ALC-loaded plugin commands via a public parameterless constructor with no DI injection, so I specified a shared ChatDbg.Introspection.Abstractions assembly in the default load context plus a static IntrospectionServices registry. The architect should confirm this is the product-wide pattern rather than a per-package invention.
42. Environment seeding for sub-commands is unverified: CommandController.Run derives the command name from the first token, so a child environment is built as GetChild("TOKEN") and only the TOKEN bucket is copied, while CommandRegistry.GetEnvironment seeds per-registered-command buckets (and would try to instantiate the synthetic root, whose FullTypeName is empty). Every tool therefore supplies its own in-code fallback defaults; whether the framework actually seeds anything for sub-commands should be tested against 3.3.4.
43. Command-line file paths are unusable in this framework (the argument tokenizer strips backslash, forward slash, colon, equals inside quotes as well as outside), so EXPORT/LOAD take a filename stem resolved against TOKEN_EXPORTDIR. If the product needs arbitrary destinations, the host must add a path-alias mechanism — this is a product-wide decision, not a package-local one.
44. Whether the settings-dialog clamp semantics survive: the source rejected out-of-range top-K from commands but silently clamped in the GUI settings dialog (1-20). This chapter rejects everywhere; if the rebuilt GUI reintroduces a dialog, the divergence must be resolved in one direction.
45. Whether the source's verbatim user-visible strings should be preserved character-for-character where they contain defects, e.g. 'Error: Response text expected, none recieved.' (misspelling) and 'Unknown subcommand: {arg}. ' (trailing space). This chapter preserves the trailing space and drops the misspelling by not carrying that string; the product needs one policy.
46. Whether the truncated top-K entropy should be reported at all, given it is only a lower bound on the true distribution's entropy. The alternative is to report it only when the backend can supply full-vocabulary logits. Currently it is always reported with entropyIsTruncated:true.
47. Attribution beyond the recency heuristic: -method attention and -method gradient are declared and refuse when unavailable, but no backend contract for attention export or gradient attribution is specified here. Whoever owns ChatDbg.Tools.Providers must decide whether IAttributionModel is real or the two methods should be dropped from the allow-list.
48. Whether TOKEN INSPECT should exist at all given SPLIT + MAP + ATTRIBUTE compose to the same thing. It is kept for source parity and for the single-model-load optimisation, but it is the one tool in the catalog that is pure convenience.
49. Analysis retention: TOKEN_KEEP defaults to 8 in-memory analyses. Whether analyses should instead be written through to TOKEN_EXPORTDIR automatically (making the ring a cache rather than the store) is undecided and affects TOKEN DIFF's ergonomics.

### C.7 `VIEW` — Presentation & Visualization

50. Chunk granularity for VIEW TOKENS output: one chunk per rendered line, per grid row, or one chunk for the whole rendering? Per-line streams best through the bounded channel but means a downstream stage sees a table in pieces; the chapter specifies per-line (list) and per-row (grid), which should be confirmed against the host's terminal sink.
51. VIEW SAMPLE, VIEW TOKENS (grid) and VIEW HEAT (sampling) must buffer, which defeats the framework's lazy streaming for long responses. Is the 100000-chunk default ceiling right, and should the host lower it via PipelineConfiguration.MaxChannelQueueSize instead?
52. The framework prefixes per-command environment keys with the COMMAND name, not the root, so a sub-command named LAYOUT or STATUS would collide across packages. The chapter routes around this with fully-qualified CHATDBG_VIEW_* globals and modifiesEnvironment:true registrations — confirm the host is willing to grant that permission to four tools.
53. Invoking the bare root 'VIEW' throws InvalidOperationException from CommandFactory and the async path is less forgiving than the sync one on an unknown sub-command. The host must catch and render usage; no tool can absorb it. Confirm where that mapping lives.
54. Should the default palette really change from the source's bands5 to bands10? The chapter argues yes (bands5 fed unit-scale values rendered every real token red), but it is a visible behavioural change and needs an explicit sign-off.
55. Should the plain-table alternatives cap of 2 and the styled-table cap of 3 be preserved as per-surface compatibility defaults rather than unified on the setting? The chapter unifies; a -compat flag was considered and dropped.
56. Should the left-justified rule stay 79 characters while the centred rule is 80? Preserved as the source's asymmetry, but it looks like a defect to anyone reading the output.
57. Precision default is 5 (matching both table renderers) while the shared value formatter used 2. Which should a fresh installation see?
58. Who owns enabling virtual-terminal processing on legacy Windows consoles — the chapter assigns it to the host at start-up and has VIEW CAPS only observe the result. Confirm.
59. Does the full-screen host consume VIEW TOKENS output as text into its probability panel, or does it need a structured intermediate shape? The panel's 50-column default, max(longest+5,50) sizing and 0-based numbering are host constants that the renderer's 1-based output must be reconciled with.
60. Should VIEW TRANSCRIPT offer any redaction hook? It renders message content verbatim, including anything a user pasted, and a host that logs its output is logging conversation content.

### C.8 `DIAG` — Diagnostics & Observability

61. Daily file-name pattern: the chapter renames the default from 'llamasharp_yyyyMMdd.log' to 'chatdbg_yyyyMMdd.log' because the rebuilt recorder captures all three backends, and keeps read-compatibility with the legacy name. If the support workflow's documented artefact name is treated as a contract, this must be reverted.
62. Whether DIAG RECORD's suffix message parameter should be blocked entirely for interactive use, given that the framework's audit masking cannot mask space-separated parameters. The chapter mitigates by recording only length+hash in audit metadata, but a stricter reading argues for a prompt-based input path instead.
63. Whether the recorder should own or merely borrow the process-global native log-sink hook when the binding offers no unregister. The chapter specifies swapping to a no-op sentinel on disposal; the underlying binding's actual capability was not verified.
64. Whether DIAG DOCTOR's read-only ports into settings and credential provenance should be host-supplied interfaces or a formal cross-package query contract. The chapter chose host-supplied ports to keep the tool -> SDK dependency arrow, at the cost of check groups reporting 'skipped (no provider)' in a bare host.
65. Whether the DIAG tools that need shared configuration should use global environment variables (requiring modifiesEnvironment: true on CAPTURE/ROTATE/AUDIT) or a host-injected policy object. The chapter chose globals to stay inside the framework's documented propagation rules; a policy object would be simpler but less inspectable via ENV.
66. Whether -probe native's out-of-process helper is acceptable in a restricted host, or whether it should be a separate opt-in package so the base DIAG package never spawns a process.
67. Retention defaults (14 days, 256 MiB total, 64 MiB per file) are invented -- the source had no retention at all and the dossier records 'could not determine whether the daily file was ever intended to be pruned'. These need a product decision.
68. Whether DIAG DOCTOR should also fail (not warn) when maxTokens is inert on the Azure non-logprobs path and when llamaGpuDevice/llamaThreads/llamaBatchSize reach no runtime, or whether those source quirks should be fixed in the owning packages instead of merely reported here.

### C.9 `PKG` — Tool Package Management

69. Who owns the operator identity recorded in a trust grant? The host knows only an OS user name; a shared machine or a service account makes 'who granted this' ambiguous, and the chapter assumes the host can supply something better than the process account.
70. Should PKG RELOAD attempt a real collectible-ALC unload at all? The chapter deliberately never claims one (unload is terminal, returns four indistinguishable falses, and CommandExecutor does not dispose command instances). If the host later holds every plugin reference behind a single draining wrapper, a genuine hot-swap becomes possible and PKG RELOAD's contract would change.
71. Package signature verification on Linux has no platform code-signature analogue; the design drops the policy from require to prefer with a warning. Is a detached-signature scheme over the package manifest (operator-supplied trusted keys) worth specifying, or does that duplicate what the feed's own package signing already provides?
72. -deps allow acquires a transitive closure, but Xcaciv.Loader does not confine dependency loads to basePathRestriction. Should PKG instead flatten and rewrite deps.json at install time, or refuse any package whose deps.json probes outside its own directory?
73. Whether PKG should be able to install into the system package root at all under an explicit elevated-install mode, or whether that must remain entirely out-of-band deployment tooling as specified here.
74. The lockfile records feed names, but a name is machine-local. A shared lockfile referencing feed 'corp' restores only if the receiving machine has a feed by that name; embedding the URL instead would make the lockfile a redirection vector. The chapter chose names - this trade-off is worth a second opinion.

**74 open design questions.**
