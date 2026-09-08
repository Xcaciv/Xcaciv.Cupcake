# ChatDbg — Unified Glossary

> **Purpose.** This is the single controlled vocabulary for the ChatDbg product requirements. Every other
> section of this PRD uses these terms and only these terms. The source system used several different names
> for the same idea, and in a few cases used one name for several different ideas; this document resolves
> each of those to exactly one product term and records the mapping.
>
> **How to read an entry.** **Term** — definition — *code names that mean this: `x`, `y`*.
> The *code names* annotation is the only place in this PRD where names taken verbatim from the source
> appear. It exists so that a reimplementer reading the original repository can find the corresponding
> product concept, and so that persisted wire formats (file keys, environment-variable names, on-screen
> literals) survive the port unchanged. A name in that annotation is **not** a requirement to use that
> name in a clone, *except* where the entry says the literal is user-visible or persisted.
>
> Consolidated from 291 collected terms across 15 feature dossiers. Statements marked **INFERRED** were
> deduced from structure rather than directly observed.

---

## A. Product and session concepts

**Product (ChatDbg)** — A single-user, single-process, terminal-hosted chat and debugging assistant. It
holds one conversation with one language model at a time and offers token-level introspection of the
model's replies. It has no server, no accounts and no network surface of its own.
*code names that mean this: `ChatDbg`, `Xcaciv.ChatDbg`, product name literal `ChatDbg`.*

**Front end (shell)** — One of the two interchangeable terminal programs that own the terminal for the life
of the process, read the operator's input, decide command-versus-conversation, drive a model backend and
render the result. Both front ends build their own registry of the same fifteen commands and share the same
core library, but they diverge in several user-visible ways. **The source used "shell", "host", "front
end" and "UI" interchangeably; this PRD uses *front end* for the concept and *console front end* /
*full-screen front end* for the two instances.**
*code names that mean this: `ChatShell`, "shell", "host", "REPL", assemblies `Xcaciv.ChatDbg.Shell` and `Xcaciv.ChatDbg.Shell.Gui`.*

**Console front end** — The line-oriented front end: print a prompt, read one line, dispatch, print the
outcome with a status glyph, repeat. It maintains no redrawn view and is the only front end the release
pipeline actually publishes.
*code names that mean this: `src/ChatDbg/ChatShell.cs`, "plain-console shell", "line-oriented console shell", "console shell", legacy namespace `ChatDBG`.*

**Full-screen front end** — The windowed front end that paints a persistent menu bar, a scrollable
transcript, an input frame, a status line and modal dialogs over the whole terminal surface. It adds
surfaces the console front end does not have (a settings dialog, a prompt manager, a token-probability side
panel, a command-listing dialog) and omits behaviours the console front end has.
*code names that mean this: `src/ChatDbg.Shell.Gui`, `ChatWindow`, "Terminal GUI Shell", "windowed shell", "graphical shell".*

**Session** — One run of the process from launch to exit. A session owns exactly one conversation record,
one live settings record, one command registry and one backend registry. Nothing about a session survives
process exit except what the operator explicitly exported.
*code names that mean this: session state, `RunAsync` loop.*

**Session identifier** — A random unique identifier generated when the conversation record is constructed.
It is written on export and overwritten on import, and nothing else in the product ever reads it.
*code names that mean this: `SessionId`, `sessionId`.*

**Created-at timestamp (session)** — The UTC instant the conversation record was constructed. Written on
export, read but discarded on import, never mutated, and not reset by clearing the conversation.
*code names that mean this: `CreatedAt`, `createdAt`.*

**Conversation turn** — A submitted line that does *not* begin with the command prefix and is therefore sent
to the active model backend as a user message.
*code names that mean this: chat turn, `SendChatMessageAsync`, `ProcessChatAsync`.*

**Control instruction** — A submitted line that *does* begin with the command prefix and is handled locally
by a registered command instead of being sent to a model.
*code names that mean this: command, `ProcessCommandAsync`.*

**Startup self-check** — The one-shot, read-only diagnostic printed after the banner that reports whether
the selected backend is empty, unknown, recognised-but-unconfigured, or configured, and — when configured —
which credential channel supplied its key. It never prints a credential value.
*code names that mean this: `CheckServiceConfiguration`, ChatShell startup validation block, "configuration self-check", "startup credential advisory".*

**Prompt string (input prompt)** — The literal text drawn before each console input line, rebuilt from live
settings on every loop iteration, of the form `ChatDbg ({backend}/{model identifier})> ` with no trailing
line break. Distinct from an *instruction prompt* (category G).
*code names that mean this: prompt literal in `ChatShell`.*

---

## B. Conversation and messages

**Conversation record** — The single in-memory, ordered list of messages kept for the life of one process,
plus its session identifier and created-at timestamp. It is what the model backend receives, in full, on
every request. It is never auto-saved and never auto-loaded. **The source called this "chat history",
"history", "transcript" and "conversation"; this PRD reserves *transcript* for the rendered view (category
H) and uses *conversation record* for the data.**
*code names that mean this: `ChatHistory`, "chat history", "history", `messages`.*

**Message** — One element of the conversation record: a role, content text, a UTC timestamp, a
hidden-from-model flag, and an optional token-confidence list.
*code names that mean this: `ChatMessage`, "message", "turn".*

**Role** — The speaker label on a message. The message-insertion command enforces exactly three lower-cased
values (user, assistant, system); appending and importing enforce nothing at all, so arbitrary role text can
enter the record.
*code names that mean this: `Role`, `role`.*

**Hidden-from-model flag** — A per-message boolean that, when set, excludes the message from every outbound
request to a hosted backend while leaving it visible in the record and in the transcript. It was intended
for command echoes; nothing in the shipping product ever sets it true. **The source name reads as a type
label rather than a behaviour flag, which is misleading.**
*code names that mean this: `IsCommand`, `isCommand`, "is-command flag".*

**Message insertion (inject)** — The operation that fabricates a message with a chosen role and content and
inserts it at a chosen index in the conversation record.
*code names that mean this: `InjectCommand`, `/inject`.*

**Message removal (pop)** — The operation that removes exactly one message — the last — from the
conversation record. **Documentation described it as removing "messages" plural; the code removes one.**
*code names that mean this: `PopCommand`, `/pop`.*

**Conversation clear** — The operation that empties the message list while preserving the session identifier
and the created-at timestamp.
*code names that mean this: `ClearCommand`, `/clear`.*

**Conversation export** — The operation that writes the whole conversation record to an operator-named file
in the conversation-file format.
*code names that mean this: `ExportCommand`, `/export`, `SaveHistoryAsync`.*

**Conversation import** — The operation that replaces the live conversation record's messages and session
identifier wholesale from a file. It replaces; it never merges.
*code names that mean this: `ImportCommand`, `/import`, `LoadHistoryAsync`.*

**Conversation file** — The exported on-disk artifact: one structured-text object holding the message list,
the session identifier and the created-at timestamp. It is a wire format: an exported file must remain
importable by any conforming implementation. Default file name is a persisted, user-visible literal.
*code names that mean this: `chat_history.json`, `chat.json`.*

---

## C. Model backends and inference

**Model backend** — A pluggable implementation of the five-operation backend contract (report a display
name, answer a network-free readiness check, send a turn and return text, send a turn and return text plus
token confidence data, release resources) that turns a conversation into a reply. Exactly three ship. **The
source called these "AI service", "provider", "backend" and "adapter"; this PRD uses *model backend*
throughout.**
*code names that mean this: `IAIService`, `AzureOpenAIService`, `BedrockService`, `LLamaSharpService`, "provider", "provider adapter", "AI service", "back end".*

**Hosted cloud model service** — Model backend #1: a subscription-scoped, endpoint-addressed cloud service
reached over HTTPS with a single API key. Its display name is a fixed user-visible literal.
*code names that mean this: Azure OpenAI, `AzureOpenAIService`, backend key `azure`, display literal `Azure OpenAI`, package `Azure.AI.OpenAI`.*

**Managed cloud model marketplace** — Model backend #2: a regional, account-scoped cloud service through
which the operator's own cloud account rents access to hosted foundation models. Addressed by region plus a
free-text model identifier and an access-key/secret-key pair. Its display name is a fixed user-visible
literal.
*code names that mean this: Amazon Bedrock, `BedrockService`, backend key `bedrock`, display literal `Amazon Bedrock`, package `AWSSDK.BedrockRuntime`.*

**Local inference backend** — Model backend #3: an in-process backend that loads an open-weights model file
from local disk and generates with no network call, no account and no credential. It is the product's
current development frontier and the only backend that can produce genuinely measured per-token confidence
data.
*code names that mean this: `LLamaSharpService`, backend key `llama`, LLamaSharp.*

**Backend key** — The lower-case identifier under which each model backend is registered and which is
persisted in the settings document as the selected backend. Exactly three values exist and they are
persisted user-visible literals: `azure`, `bedrock`, `llama`.
*code names that mean this: `ChatSettings.Provider`, `_aiServices` dictionary keys, `provider`.*

**Backend registry** — The name-keyed table each front end builds once at start-up mapping the three backend
keys to backend instances. Never mutated after construction.
*code names that mean this: `Dictionary<string, IAIService> _aiServices`, "provider registry", "provider table".*

**Backend display name** — The human-readable name a backend reports for itself, interpolated verbatim into
every warning and error that names it. These are user-visible literals and must be preserved.
*code names that mean this: `IAIService.GetProviderName()`, `ProviderName`.*

**Readiness check** — Each backend's read-only, side-effect-free, network-free test over the settings record
that answers "can this backend serve a turn?". For the cloud backends it tests for a resolved credential and
an address; for the local backend its entire content is "does the model file exist on disk?".
*code names that mean this: `IsConfigured(ChatSettings)`, "is-configured predicate".*

**Reply envelope** — The single structure every backend returns: reply text, an optional ordered
token-confidence list, a total elapsed time, and an optional error message. **Absent confidence data is
represented as *absent*, never as an empty list.**
*code names that mean this: `AIResponse`, `Text`, `LogProbabilities`, `TotalTime`, `ErrorMessage`, "provider response", "response record", "response envelope".*

**Model identifier** — A single free-text settings value that names the model to invoke. Its meaning is
backend-dependent: for the hosted cloud service it names both the model and its service-side deployment and
is interpolated directly into the request address; for the marketplace it is a vendor model name; for the
local backend it is a **filesystem path to the model weights file**. One field, three semantics.
*code names that mean this: `ModelId`, `modelId`, `/set modelId`, `/model`, "deployment name".*

**Model weights file** — The single quantised open-weights file on local disk that the local inference
backend loads. Its path is carried in the model-identifier setting.
*code names that mean this: GGUF file, `.gguf`, `LLamaWeights`.*

**Inference context** — A sized working state created over the loaded weights that holds per-session
computation state. Its size is the context window.
*code names that mean this: `LLamaContext`, `ContextSize`, `llamaContextSize`.*

**Context window size** — The token capacity of an inference context. The operator-configurable value
defaults to 4096, but the introspection subsystem hard-codes 512 for tokenization loads and 2048 for
generation loads and ignores the configured value.
*code names that mean this: `ContextSize`, `llamaContextSize`, `ContextParams.ContextSize`.*

**Engine-side conversation state** — The conversation state retained *inside* the local inference engine,
seeded once at model-load time with a system-role message. For the local backend this — not the product's
own conversation record — is where multi-turn continuity actually lives.
*code names that mean this: `ChatSession`, `InteractiveExecutor`, `LLama.Common.ChatHistory`.*

**Text piece** — The unit the local engine streams back: a decoded fragment of text, which is not
necessarily one model token. Every per-token counter, analysis record and confidence entry produced by the
local backend actually counts pieces, not tokens. **This distinction is invisible in the source, which calls
pieces "tokens" everywhere.**
*code names that mean this: streamed chunk from `ChatSession.ChatAsync`, `tokenCounter`.*

**Introspection generation path / fast generation path** — The two mutually exclusive local-backend
generation routines. The introspection path produces per-piece confidence and analysis records; the fast
path streams and concatenates text only and returns an absent confidence list. Which one runs is decided by
the confidence-capture switch.
*code names that mean this: `GenerateWithSamplingPipeline`, `GenerateStandard`, "sampling pipeline mode", "standard ChatSession mode".*

**Score vector** — The engine's raw per-vocabulary scores for the next position, read by run-time name
lookup and transformed into candidate probabilities.
*code names that mean this: `logits`, `GetLogits`.*

**Normalised-exponential transform** — The softmax applied to only the selected top candidates rather than
the whole vocabulary, so returned candidate probabilities always sum to 1.0 within the selection.
*code names that mean this: softmax over top-K logits.*

**Stop sequence** — A literal string whose emission halts local generation. Hard-coded to exactly two
values and not configurable.
*code names that mean this: `AntiPrompts`, `InferenceParams.AntiPrompts`, literals `User:` and `USER:`.*

**Maximum new tokens** — The per-turn generation budget, falling back to 512 when the configured value is
not positive.
*code names that mean this: `maxTokens`, `MaxTokens`, `InferenceParams.MaxTokens`.*

**Graphics-layer offload count** — How many model layers are moved onto the graphics processor; zero means
processor-only.
*code names that mean this: `llamaGpuLayers`, `llamaGpuLayerCount`, `GpuLayerCount`.*

**Generation lock / model-load lock** — Two separate process-wide binary locks that serialise, respectively,
all generation and all model loading across every backend instance in the process. Neither has a timeout
and neither is cancellable.
*code names that mean this: static `SemaphoreSlim _generationLock`, static `SemaphoreSlim _modelLoadLock`.*

**Backend client seam** — The substitutable single-operation component that turns settings into a
vendor client, existing purely so that the whole build/invoke/parse path can be exercised without network
access. One exists per cloud backend. The product never registers an alternative implementation.
*code names that mean this: `IAzureOpenAIClientFactory`, `DefaultAzureOpenAIClientFactory`, `IBedrockRuntimeClientFactory`, `DefaultBedrockRuntimeClientFactory`, `CreateClient`, "client factory seam", "client-construction seam".*

**Library-mediated request path** — The hosted-cloud-service request path taken when confidence capture is
**off**: a vendor client sends the turn carrying only a temperature option.
*code names that mean this: path P1, `GetChatClient`, `ChatCompletionOptions`.*

**Direct request path** — The hosted-cloud-service request path taken when confidence capture is **on**: a
hand-composed HTTPS request carrying temperature, maximum tokens, a nucleus-sampling value and the
confidence options. **The two paths differ in address normalisation, role filtering and whether the
maximum-token cap is sent at all.**
*code names that mean this: path P2, `CallAzureOpenAIDirectAsync`.*

**Model-family discriminator** — For the marketplace backend, the literal prefix tested case-insensitively
against the model identifier to choose between the two request-body shapes. It is the sole discriminator.
*code names that mean this: `ModelId.StartsWith("anthropic.", …)`.*

**Contract version constant** — The fixed, never-configurable version string identifying the
marketplace request contract for the discriminated model family.
*code names that mean this: `anthropic_version`, literal `bedrock-2023-05-31`.*

**Strict extraction / tolerant extraction** — The two confidence-data readers on the hosted-cloud path.
Strict extraction requires both a token string and a numeric log-probability per entry and accepts
alternatives under only one field name; tolerant extraction additionally accepts a second alternatives field
name and property-map-shaped alternative entries.
*code names that mean this: `ExtractTokenLogProbabilities`, `ExtractTokenLogProbabilitiesFromArray`.*

**Tolerant probability parser** — The never-throwing response-side confidence reader used on the
marketplace's completion branch; it swallows every failure and yields "no confidence data".
*code names that mean this: `ParseLogProbabilities`.*

**Unrecognised-response outcome** — The marketplace backend's third response shape: neither structured
content nor a completion string, answered with a fixed literal answer text and reported as a **normal
success**, not an error.
*code names that mean this: the final else branch of the response parser, literal `Unable to parse model response`.*

**Fabricated confidence data** — Invented per-token confidence records that the hosted cloud backend
substitutes when the service returns none, at a uniform 90 percent confidence with a fixed cap of three
alternatives, returned **indistinguishably from measured data**. See also *temperature-derived confidence
estimate* and *fabricated attribution*. **A clone must either not fabricate, or mark fabricated data as
such.**
*code names that mean this: `GenerateSimulatedLogProbabilities`, "simulated logprobs".*

**Temperature-derived confidence estimate** — A six-step lookup table mapping the configured sampling
temperature to a fixed "probability" written into every per-step analysis record of a local-backend run, in
place of a measured value. At the default temperature every record in an export reads the same value.
*code names that mean this: `EstimateProbabilityFromTemperature`.*

**Ambient credential fallback** — The undocumented fourth credential channel the marketplace backend
inherits when its client is constructed without both halves of the key pair and the vendor library falls
back to its own credential discovery (shared profile file, container or instance role, single sign-on,
environment variables).
*code names that mean this: default `AmazonBedrockRuntimeClient(RegionEndpoint)` construction.*

---

## D. Token-level introspection

> This is the capability that distinguishes the product. Four related but distinct things live here and the
> source blurred all four under the word "logprobs": the *stored value*, the *derived probability*, the
> *capture switch*, and the *display preferences*.

**Token** — A vocabulary fragment the model reads and emits, as opposed to a character. Reported with a
zero-based sequence index, an integer vocabulary index, a display text and an approximate character span.
*code names that mean this: `TokenInfo`, `token`, `LLamaToken`.*

**Vocabulary index** — The integer identifier of a token within the model's vocabulary. Real for
tokenization; synthetic on the introspection generation path; always a sentinel value on chat-time analysis
records.
*code names that mean this: `TokenId`, `tokenId`.*

**Token log probability (stored value)** — The natural logarithm of the probability the model assigned to a
token at a generation step, stored verbatim with no range validation. **"logprob", "log probability",
"log prob" and "confidence value" in the source all mean this; this PRD uses *token log probability*.**
*code names that mean this: `LogProb`, `logprob`, `log_prob`, `SelectedLogProb`, `logprobs`.*

**Derived probability** — The linear probability of a token, computed as *e* raised to the stored token log
probability. Computed on every read; never stored and never persisted. It is a 0–1 fraction. **Several
display paths in the source treat it as if it were a 0–100 percentage; this PRD fixes it as 0–1.**
*code names that mean this: `Probability` (computed property), `Math.Exp(LogProb)`.*

**Token confidence record** — One entry in a token-confidence list: token text, the stored token log
probability, the derived probability, and a nullable ordered list of alternatives of the same shape. The
shape is recursive, though nested alternatives are normally absent.
*code names that mean this: `TokenLogProbabilities`, `TokenLogProbability`, "token probability record".*

**Token-confidence list** — The ordered list of token confidence records attached to a reply and to the
message that stores it. Absent (not empty) when no confidence data exists.
*code names that mean this: `LogProbabilities`, `logProbabilities`.*

**Alternative token** — One of the other tokens the model weighed at a given position, carried with its own
text, vocabulary index and stored value. Emitted in strictly descending probability order.
*code names that mean this: `TopAlternatives`, `top_alternatives`, `top_logprobs`, `TokenProbabilityAlternative`, `Alternatives`, `TopCandidates`, `CandidateToken`, "candidate token", "runner-up token".*

**Alternatives-per-position count (top-K)** — How many alternative tokens the backend is asked to report per
generated position. Inclusive range 1–20, default 5. It shapes the request and, on the local backend, also
silently sets the sampler's selection limit; it never truncates a response. **The introspection subsystem
applies neither the range clamp nor the top-K semantics — it emits exactly that many fabricated
alternatives.**
*code names that mean this: `LogProbabilitiesTopK`, `logProbabilitiesTopK`, `top_logprobs`, `topK`, `/set logtopk`, `/logprobs top`, `--top`.*

**Confidence capture (capture switch)** — The single global boolean that decides, per turn, whether the
send-with-confidence operation or the plain send operation is used. Default off. It is the only thing that
selects between a backend's two request paths.
*code names that mean this: `EnableLogProbabilities`, `enableLogProbabilities`, `/logprobs on|off`, set alias `logprobs`.*

**Has-confidence-data predicate** — A derived, never-persisted boolean on a message: its token-confidence
list exists **and** has at least one element. It drives the transcript's confidence marker and the
availability of the confidence panel.
*code names that mean this: `HasLogProbabilities`.*

**Confidence configuration command** — The user-facing command that reports, configures and diagnoses token
confidence capture and display.
*code names that mean this: `/logprobs`, `LogProbsCommand`.*

**Offline demonstration** — A command that fabricates a sample token-confidence record from a fixed sentence
and renders it without contacting any backend, so the visualisation can be seen without configuring a
backend or spending a request.
*code names that mean this: `/demologprobs`, `DemoLogProbsCommand`, `SampleData`, "demo visualisation".*

**Tokenization report** — The on-screen table of sequence index, vocabulary index and token text produced by
the tokenization command against a locally loaded model.
*code names that mean this: `TokenizeCommand`, `/tokenize`.*

**Beginning-of-sequence marker** — A synthetic leading token prepended during tokenization. It is counted in
the reported total but carries a sentinel character span and is displayed as a fixed "not applicable"
literal.
*code names that mean this: BOS, `addBos`, special token.*

**Character span** — The approximate inclusive start and exclusive end offsets of a token in the original
text. Produced by a **uniform-distribution estimate**, not by a real offset map, so it is not a truthful
mapping.
*code names that mean this: `CharStartPosition`, `CharEndPosition`, "Char Range".*

**Full inspection** — The three-stage report (tokenization, then probability mapping, then attribution)
produced by the inspection command behind an interactive consent gate.
*code names that mean this: `InspectCommand`, `/inspect`.*

**Consent gate** — The blocking yes/no question asked between the cheap tokenization stage and the expensive
generation stage. Only an affirmative answer proceeds; anything else, including end-of-input, silently
declines.
*code names that mean this: the consent prompt in `InspectCommand`.*

**Step-probability record** — The distribution at one generation step: position, selected token identity and
its stored value, plus the alternative list.
*code names that mean this: `TokenProbabilityMap`, `TokenProbabilityMapResult`.*

**Attribution** — A per-generated-token claim about which span of input, or of earlier output, influenced
it, with a numeric influence score. Produced by a fixed heuristic — **not** by gradients, attention or any
model-internal signal.
*code names that mean this: `TokenAttribution`, `AttributionMap`, `InfluencingText`.*

**Influence score** — The numeric strength attached to an attribution entry. Always the same constant.
*code names that mean this: `InfluenceScore`.*

**Per-step analysis record** — The exportable per-token introspection record the local inference subsystem
writes for each generated piece during ordinary chat: step index, token text, vocabulary index, probability,
stored log probability, prompt offset, candidate list, a free-text debug sentence and a model-state
snapshot. It is the only persisted entity produced by the introspection subsystem.
*code names that mean this: `TokenAnalysis`, "per-token analysis record".*

**Model-state snapshot** — The context-utilisation record captured alongside each per-step analysis record:
tokens processed, context token count, context window size, remaining capacity, a timestamp and a free-form
diagnostic map.
*code names that mean this: `ModelStateInfo`, `ModelState`, `modelState`, `debugInfo`.*

**Prompt offset** — A field documented as naming which part of the prompt influenced a token, but written by
its only producer as the step index. **The name does not describe the value.**
*code names that mean this: `PromptOffset`, `promptOffset`.*

**Generation budget (inspection)** — The maximum number of tokens requested from the generator during a full
inspection: the smaller of ten and the configured maximum response length.
*code names that mean this: `numTokensToGenerate`, `Math.Min(10, MaxTokens)`.*

---

## E. Configuration and profiles

**Settings record** — The single mutable, process-wide configuration object shared *by reference* with every
command, dialog and backend, so a mutation is immediately visible everywhere. It carries backend selection,
model addressing, generation tuning, confidence capture and display preferences, local-model tuning, the OS
vault toggle and the deprecated in-document secret slots.
*code names that mean this: `ChatSettings`, `_settings`, "live settings record", "configuration object".*

**Settings document** — The persisted, indented, hand-editable structured-text file holding the settings
record in full. It has exactly 21 keys with camel-cased names. Located under the operator's profile
directory in a product-named folder, falling back silently to the system temporary directory when the
profile directory cannot be resolved.
*code names that mean this: `settings.json`, `<user profile>/.ChatDbg/settings.json`, `SettingsService.GetSettingsFilePath`.*

**Settings store** — The narrow six-operation port through which everything outside the configuration
feature reaches persistence: persist, load, report path, store one credential, interactively enable the OS
vault, and run the credential migration.
*code names that mean this: `ISettingsService`, `SettingsService`.*

**Save-after-every-change rule** — Every successful mutation immediately writes the entire record to disk.
There is no save verb, no dirty tracking, no locking and no atomic replace.
*code names that mean this: `SaveSettingsAsync` call sites.*

**Setting key** — The lower-cased token naming one setting in the settings command. A small number of keys
are *delegated keys* that hand off to the credential subsystem and deliberately skip the trailing save
because the delegate already persisted.
*code names that mean this: `/set <key> <value>`, `SetCommand`, delegated keys `wincred`, `enablewincred`, `migrate`.*

**Tuning values** — The product-wide generation tunables with fixed ranges: temperature 0.0–2.0 inclusive
(default 0.7), maximum output tokens 1–8192 inclusive (default 1000), alternatives-per-position 1–20
inclusive (default 5).
*code names that mean this: `temperature`, `maxTokens`, `logProbabilitiesTopK`.*

**Clamp-versus-reject split** — The observed inconsistency whereby the text commands **reject** an
out-of-range number with an error while the full-screen settings dialog **silently clamps** it to the
nearest bound. A clone must pick one policy.
*code names that mean this: `SetCommand` validation versus `SettingsDialog` clamp calls.*

**Local-model tunables** — Context window size, graphics-layer offload count, graphics device selection,
thread count and batch size. **Only the first two ever reach the runtime; the other three are persisted,
displayed and ignored.**
*code names that mean this: `llamaContextSize`, `llamaGpuLayerCount`, `llamaGpuDevice`, `llamaThreads`, `llamaBatchSize`.*

**Confidence display preferences** — The four persisted preferences that shape how token confidence data is
drawn: the show-all-versus-sampled switch, the grid-versus-list layout switch, the grid alternatives cap,
and (indirectly) the alternatives-per-position count. **The full-screen confidence panel ignores the first
two entirely.**
*code names that mean this: `ShowAllTokens`, `GridViewForTokens`, `GridViewMaxAlternatives`.*

---

## F. Credentials and secrets

**Credential resolution** — The per-read procedure that walks the credential channels in fixed priority and
returns the first non-empty value, coalescing "no value" to the empty string. Nothing is cached, so an
environment change takes effect without restart. Failures at any leg are swallowed and the walk continues.
*code names that mean this: `ChatSettings.GetSecureValue`, `AzureApiKeySecure`, `AwsAccessKeySecure`, `AwsSecretKeySecure`, "resolved credential".*

**Credential channel** — One leg of the resolution chain. Three exist, in order: (1) **environment
variable**, (2) **OS credential vault** — consulted only when the vault toggle is on, (3) **deprecated
in-document slot**. A fourth, *ambient credential fallback* (category C), exists only for the marketplace
backend and only when both key halves resolve empty.
*code names that mean this: the three legs of `GetSecureValue`.*

**Credential environment variable** — A process-scoped, read-only variable supplying a secret. The
product-specific names follow a fixed convention; for the marketplace backend the standard vendor variable
names are also accepted as a second choice within the same leg. These names are a wire contract and must be
preserved.
*code names that mean this: `CHATDBG_AZURE_API_KEY`, `CHATDBG_AWS_ACCESS_KEY`, `AWS_ACCESS_KEY_ID`, `CHATDBG_AWS_SECRET_KEY`, `AWS_SECRET_ACCESS_KEY`.*

**OS credential vault** — The operating-system-managed, encrypted-at-rest, per-user secret store used as
credential channel 2. In the source it exists on one platform only and silently yields "not found"
everywhere else. **The source called this the "machine secret vault", "OS credential keystore", "operating
system credential store" and by its platform product name; this PRD uses *OS credential vault*.**
*code names that mean this: `WindowsCredentialManager`, Windows Credential Manager, `advapi32.dll`, `CredReadW`/`CredWriteW`/`CredDeleteW`/`CredFree`.*

**Vault toggle** — The single persisted boolean, default false, that makes the OS credential vault channel
eligible during resolution. It may be set true only on a platform that has a vault — **except through the
full-screen settings dialog, which performs no such check.**
*code names that mean this: `useWindowsCredentialManager`, `UseWindowsCredentialManager`, `/set enablewincred`.*

**Vault entry name** — The fixed, colon-containing opaque key under which each secret is stored in the OS
vault. Three exist and they are a persisted wire contract.
*code names that mean this: `ChatDbg:AzureApiKey`, `ChatDbg:AwsAccessKey`, `ChatDbg:AwsSecretKey`, credential target name.*

**Vault entry kind / persistence scope** — The two fixed vault attributes the product uses for every read,
write and delete: the *generic credential* kind, and *local-machine* persistence, which keeps the entry on
one machine across logoff and reboot and explicitly does not roam.
*code names that mean this: `CRED_TYPE_GENERIC` (1), `CRED_PERSIST_LOCAL_MACHINE` (2).*

**Deprecated in-document secret slot** — One of three settings-document string fields that hold a secret in
the clear. They are still serialized (always written as empty strings) and still read as credential channel
3, but **no supported product action ever writes a non-empty value into them**; they exist to carry forward
values from older installations.
*code names that mean this: `azureApiKey`, `awsAccessKey`, `awsSecretKey`.*

**Plaintext-credentials-present condition** — The derived condition, true when at least one deprecated
in-document slot is non-empty, that drives a standing load-time warning and gates the migration wizard.
*code names that mean this: `ChatSettings.HasPlaintextCredentials`.*

**Credential source label** — The short, non-disclosing string naming which channel supplied a credential,
printed instead of the credential value. Exactly four values exist and they are user-visible literals:
`environment variable (<NAME>)`, `Windows Credential Manager`, `settings file (deprecated)`, `not set`.
**The source used "credential source label", "credential source string" and "credential source" for this
one thing.**
*code names that mean this: `ChatSettings.GetCredentialSource`, `AzureApiKeySource`, `AwsAccessKeySource`.*

**Masked status token** — The literal printed in status displays in place of a present secret, paired with a
second literal for an absent one. Both are user-visible literals: `***set***` and `(not set)`.
*code names that mean this: the credential status formatter in `SetCommand`.*

**Migration wizard** — The interactive flow that shows a platform-conditional three-option menu, either
prints migration instructions or writes secrets into the OS vault, and then offers to erase the plaintext
copies from the settings document. **As shipped it prints secret values in cleartext to the terminal.**
*code names that mean this: `SettingsService.MigrateCredentials`, `/set migrate`.*

**Vault-store command** — The command that places a named credential into the OS vault. It requires at
least three argument tokens, requires the vault toggle to already be true, and accepts short type aliases.
*code names that mean this: `/set wincred`, `SettingsService.StoreCredentialSecurely`.*

**Vault-enable consent flow** — The interactive yes/no prompt that must be answered affirmatively before the
OS vault channel is turned on and persisted.
*code names that mean this: `/set enablewincred`, `SettingsService.EnableWindowsCredentialManager`.*

**Credential-key refusal** — The always-failing response to any attempt to set a secret through the ordinary
settings-change command, carrying per-credential remediation text pointing at the environment variable or
the vault instead.
*code names that mean this: the credential-key guard in `SetCommand`, `GetCredentialSecurityMessage`.*

---

## G. Instruction prompts

> Distinct from the *prompt string* (category A), which is the terminal's input marker.

**Instruction prompt (system prompt)** — A named block of instruction text that sets the assistant's persona
and focus. The active one is injected as the conversation's system message at index 0 of every outbound
request. **The source called this "system prompt" and "prompt"; this PRD uses *instruction prompt* where
ambiguity with the input prompt is possible, and *system prompt* elsewhere.**
*code names that mean this: `SystemPrompt`, `systemPrompt`, `ChatSettings.SystemPromptContent`.*

**Prompt record** — One persisted instruction prompt: name, content, description, created-at instant and
last-used-at instant. One record per file; the record *is* the entire file. The name is the primary key and
also the basis of the file name.
*code names that mean this: `SystemPrompt` model, `system_prompts/*.json`.*

**Prompt library** — The directory of prompt record files plus the capabilities for fetching, listing,
saving, deleting, stamping-as-used and reporting its own location. It lives under the per-user *local*
application-data root — a different root from the settings document.
*code names that mean this: `SystemPromptService`, `ISystemPromptService`, the `system_prompts` directory.*

**Active prompt** — The single prompt whose text is injected into every model request. Identified **by name
only**: the name is persisted in the settings document, the text is runtime-only and re-resolved at every
launch, falling back to a built-in default body when the named record is missing.
*code names that mean this: `ChatSettings.SystemPromptName` (persisted, default literal `default`), `ChatSettings.SystemPromptContent` (never persisted).*

**Seed prompt set** — The four prompt records written automatically whenever the library loads zero records.
Their names are user-visible literals: `default`, `code-reviewer`, `algorithm-helper`, `security-expert`.
*code names that mean this: `CreateDefaultPrompts`.*

**Last-used timestamp** — The UTC instant a prompt was most recently selected as active; absent until first
use and rendered as a fixed literal when absent.
*code names that mean this: `SystemPrompt.LastUsedAt`, `lastUsedAt`, `UpdateLastUsedAsync`.*

**Prompt description** — Short display-only text shown in listings and detail views; changeable only through
the full-screen edit form, never from the console front end.
*code names that mean this: `SystemPrompt.Description`, `description`.*

**Sanitized file name** — The record's name with every host-forbidden filename character replaced by an
underscore, plus a structured-text extension. The mapping is **lossy**, so two distinct prompt names can
collide onto one file.
*code names that mean this: `SanitizeFileName`.*

**Content-entry mode** — The console front end's interactive multi-line editor state, entered by the prompt
edit subcommand and left only by typing a sentinel line. The sentinel is a user-visible literal: `END`.
*code names that mean this: the `PromptCommand` edit read loop.*

**Current marker** — The literal suffix appended in prompt listings to the entry whose name equals the
active prompt's name. A user-visible literal: ` (current)`.
*code names that mean this: `PromptCommand` listing.*

**Exported prompt file** — The artifact written by prompt export: plain text containing **only** the
prompt's content — no name, no description, no timestamps, no wrapper. It is therefore not round-trippable
without supplying a name on import.
*code names that mean this: `/prompt export`.*

**Prompt manager** — The full-screen front end's modal management screen for the prompt library, reached
from the tools menu.
*code names that mean this: `SystemPromptsDialog`.*

---

## H. Presentation and rendering

**Output surface** — The six-operation abstraction (write a marked-up line, write a plain line, write a
blank line, write a separator rule, display a token grid, display a token table) through which in-process
features emit user-visible output without knowing which front end hosts them. A command given no output
surface must still work.
*code names that mean this: `IConsoleFormatter`, "rendering collaborator".*

**Plain output surface** — The implementation of the output surface that strips styling directives and
writes only to a replaceable standard-output writer.
*code names that mean this: `BasicConsoleFormatter`.*

**Styled output surface** — The implementation that emits inline styling directives to a rich terminal
renderer, producing coloured cards, tables, panels and captioned rules.
*code names that mean this: `SpectreConsoleFormatter`, Spectre.Console.*

**Shared token-presentation helpers** — The library of format-only routines shared across surfaces: escape a
token for display, map a probability to a colour band, format a probability as a percentage, build a
plain-text confidence report, build a compact alternatives description.
*code names that mean this: `TokenFormatters`.*

**Transcript** — The rendered, scrollable view of the conversation record in the full-screen front end,
captioned with a fixed literal and rebuilt from scratch on every refresh, one row per role banner plus one
per wrapped body line, with per-role colour schemes and alignment. The console front end has no transcript
— it prints each turn as it happens.
*code names that mean this: `_chatView`, `chatView`, `RefreshChatView`, `RenderChatHistory`, `Chat History` frame.*

**Bubble width factor** — The three-quarters multiplier that keeps a wrapped message body to
three-quarters of the transcript's usable width after subtracting the frame padding.
*code names that mean this: the wrap arithmetic in `WrapText`.*

**Confidence marker** — The clickable diamond glyph drawn beneath (console: rendered inline with) any
assistant message that carries confidence data. Activating it points the confidence panel at that message.
**The source called this the "token-probability marker", "confidence indicator", "probability indicator",
"probability marker" and "lozenge"; all are one control.**
*code names that mean this: the U+25CA button in `ChatWindow`, `logProbIndicator`, `lozengeScheme`.*

**Confidence panel** — The framed, scrollable side panel in the full-screen front end that lists every token
of one selected assistant message with its ranked alternatives, colour-coded by confidence. Captioned with
a fixed literal.
*code names that mean this: `_logProbView`, `logProbsPanel`, `Token Probabilities` frame, `ShowLogProbsPanel`.*

**Card grid layout** — The dense multi-column layout that draws one bordered card per token, for spotting
patterns across many tokens.
*code names that mean this: `gridViewForTokens`, `DisplayTokenGrid`, "grid view".*

**Row-per-token table layout** — The detailed layout that draws one table row per token with its text,
probability and top alternatives, for inspecting individual tokens. Its alternatives count is fixed at
three and is **not** governed by the grid alternatives cap.
*code names that mean this: `DisplayTokenTable`, "list view".*

**Grid alternatives cap** — The operator-configurable maximum number of alternatives drawn per token in the
card grid and in the full-screen confidence panel, before a "plus N more" summary. Range 1–20, default 5.
**Its name says "grid" but it also drives the list-form panel.**
*code names that mean this: `GridViewMaxAlternatives`, `gridViewMaxAlternatives`, `gridmaxalt`, `maxAlternatives`.*

**Beginning/middle/end sampling** — The default volume-control rule that renders only three five-token
slices of a long token list — from the start, the middle and the end — instead of every token. Applied
above a token-count threshold.
*code names that mean this: `showAllTokens`, `SampleTokens`, `GetSampledTokens`, `GetSampleTokens`, `sampleSize = 5`.*

**Numbering start index** — A caller-supplied offset added to each rendered token's position so that a slice
of a longer list still shows each token's true position in the full reply.
*code names that mean this: `startIndex`.*

**Confidence colour band** — The mapping from a token's confidence to a colour, so low-confidence regions of
an answer are visible at a glance. **Four mutually inconsistent maps exist in the source, on two different
numeric scales (0–100 and 0–1) with different band counts; a clone must define exactly one.**
*code names that mean this: `GetColorForProbability`, `GetProbabilityColor`, `GetColorForLogProb`, `GetHeatMapColor`, `_probColorSchemes`, `probabilityColorSchemes`.*

**Heat-map bucket set** — The full-screen front end's ten fixed colour buckets indexed by the truncated
tenth of the derived probability, clamped to the valid range, applied to token and alternative rows in the
confidence panel.
*code names that mean this: `_probColorSchemes`, `GetProbabilityColorScheme`.*

**Flowing heat-map view** — A defined-but-never-constructed view intended to paint tokens as a continuous
paragraph with each token's background coloured by its confidence. It has its own, sixth colour scale and
its own sampling rule. **Dead code; see PRD §2.3.**
*code names that mean this: `LogProbHeatmapView`.*

**Escape formatting** — Replacing a token's control characters with visible two-character escape sequences
so that an invisible token is not mistaken for a blank.
*code names that mean this: `FormatTokenForDisplay`, `EscapeTokenText`.*

**Plain-text confidence report** — The dependency-free textual report with a fixed heading, a markdown-style
table and a fixed-width footer rule, used where no rich surface is available.
*code names that mean this: `GenerateTokenProbabilityReport`.*

**Resting status text** — The persistent one-line readout in the full-screen front end naming the backend,
the model identifier and the active prompt name. A user-visible literal template.
*code names that mean this: `UpdateStatusLabel`, `_statusLabel`, `Provider: X | Model: Y | Prompt: Z`.*

**Transient status message** — A status-line message that replaces the resting text and is unconditionally
restored to it after a fixed three-second delay by an independent, uncancellable timer.
*code names that mean this: `ShowStatusMessage`, `ShowStatus`, `SetStatusMessage`.*

**Outcome glyphs** — The two Unicode glyphs that prefix a rendered command message: a check mark for
success, a ballot cross for failure, each followed by one space. In the console front end **the glyph is
the only machine-readable success/failure signal** — there is no exit code and no structured output.
*code names that mean this: the `✓ ` and `✗ ` literals (U+2713, U+2717).*

**Theme applier** — The single argument-free operation that installs the fixed dark colour scheme into the
full-screen toolkit's four global scheme slots at start-up. There is no theme choice.
*code names that mean this: `ThemeManager.ApplyDarkTheme`.*

---

## I. Commands and the command surface

**Command prefix** — The single leading forward-slash character that marks a submitted line as a control
instruction rather than a conversation turn. A user-visible, load-bearing literal: `/`.
*code names that mean this: the `"/"` literal tested in `ProcessInputAsync` / `ProcessInput`.*

**Command** — A named, locally-executed capability exposing a name, a one-sentence description, a usage
string and an asynchronous execute operation over an ordered argument list.
*code names that mean this: `ICommand` (`Name`, `Description`, `Usage`, `ExecuteAsync`).*

**Command registry** — The name-to-command map each front end builds once at start-up. It is the **only**
discovery mechanism: keyed by each command's own declared lower-case name, matched exactly, never mutated
afterwards. There is no plugin discovery, no configuration-driven registration and no runtime registration.
Both shipping front ends register exactly fifteen commands: `clear`, `demologprobs`, `exit`, `export`,
`help`, `import`, `inject`, `inspect`, `logprobs`, `model`, `pop`, `prompt`, `quit`, `set`, `tokenize`.
*code names that mean this: `Dictionary<string, ICommand> _commands`, `InitializeCommands()`.*

**Argument vector** — The ordered list of text tokens following the command name, excluding the name;
empty when no arguments were typed. Produced by splitting the line on the single space character and
discarding empty tokens, so runs of spaces collapse, tab characters are not separators, and **there is no
quoting or escaping anywhere in the product**.
*code names that mean this: the `string[] args` parameter, `parts[1..]`.*

**Command result** — The uniform three-signal outcome every command returns: whether it worked, an optional
free-form message for the operator (rendered verbatim, possibly multi-line), and whether the session should
stop.
*code names that mean this: `CommandResult` (`Success`, `Message`, `ExitRequested`), `SuccessResult`, `ErrorResult`, `ExitResult`.*

**Exit request** — The result flag that tells the front end to terminate the session. It is a **successful**
outcome, not an error, and it suppresses rendering of any message carried on the same result.
*code names that mean this: `CommandResult.ExitRequested`, `ExitResult()`.*

**General help document** — The curated, hand-authored, topic-grouped listing produced by the help command
with no arguments. It is a script, not a projection of the registry, so it can drift in both directions: a
registered command missing from the script is invisible, and a scripted name that is not registered is
silently skipped.
*code names that mean this: `HelpCommand.GetGeneralHelp()`.*

**Detailed help** — The three-line block produced by the help command for one named command, echoing the
*registered* name rather than the text typed.
*code names that mean this: `HelpCommand` with a non-empty argument vector.*

**Command listing dialog** — The full-screen front end's second, independent help surface: an alphabetical
name-and-description list opened by a function key or a menu item. It shows no usage strings and no
grouping, and it is not derived from the general help document.
*code names that mean this: `ShowCommandsDialog`, `ShowHelp`.*

**Direct invocation** — A front-end path that calls a command by name with a pre-built argument vector,
bypassing text parsing and taking on result rendering itself.
*code names that mean this: the full-screen inject and change-model dialog handlers calling `ExecuteAsync` directly.*

**Synthesised command line** — A front-end path that builds a slash-prefixed text line from a menu action
and re-enters the ordinary parse-and-dispatch pipeline.
*code names that mean this: `ExecuteCommandAsync("/" + name + args)`.*

**Name placeholder** — The literal placeholder token used in every usage and error string where a *name* is
meant. It is spelled `<n>`, which reads as "a number", but that spelling is consistent across the code, the
error messages and the user manual, so it is a user-visible convention, not a typo to silently fix.
*code names that mean this: the `<n>` literal throughout `PromptCommand` and `SetCommand`.*

---

## J. Files, artifacts and storage

**Per-user state root** — One of the three well-known per-user directories the product writes into, keeping
the install location read-only. **The product uses three different roots under two naming conventions: the
profile root for settings, the local application-data root for the prompt library, and the roaming
application-data root for diagnostic logs. A clone should pick one.**
*code names that mean this: `Environment.SpecialFolder.UserProfile`, `LocalApplicationData`, `ApplicationData`; the `.ChatDbg` and `ChatDbg` folder literals.*

**Home-path expansion** — The rule by which a leading tilde-and-forward-slash at position 0 of a supplied
path expands to the operator's profile directory. A bare tilde, the backslash form and a named-user form
are **not** expanded, and several commands do not expand at all even though the manual's examples use the
tilde form.
*code names that mean this: the `~/` prefix test.*

**Diagnostic recorder** — The in-process object that owns the capture buffer, the output switches and the
log directory, and performs arming, recording, flushing, snapshot export and shutdown. Exactly one exists
per local-inference subsystem instance.
*code names that mean this: `LLamaSharpLogConfig`, "diagnostic log sink".*

**Capture buffer** — The single in-memory, lock-guarded text accumulator holding whole formatted entry
lines. Drained by a flush, emptied by a clear, and snapshotted non-destructively by an export.
*code names that mean this: `_logBuffer`, `GetLogs()`.*

**Arm capture** — The one-shot, idempotent operation that ensures the log directory exists, installs the
recorder as the inference engine's process-global diagnostic destination, sets the armed latch and records
its own confirmation entry. **It is never un-armed, even by shutdown.**
*code names that mean this: `ConfigureLogging()`, `_isConfigured`, `NativeLogConfig.llama_log_set`.*

**Engine diagnostic line** — A diagnostic message emitted by the local inference engine about itself,
delivered with an opaque severity token and a message string; trailing whitespace is stripped before
buffering, and only these lines can trigger the size-based auto-flush.
*code names that mean this: the native log callback, `LLamaLogLevel`.*

**Application diagnostic entry** — A commentary line recorded by the product about what it just asked the
engine to do, carrying a free-text severity label. Not whitespace-stripped and never triggers auto-flush.
*code names that mean this: `LogMessage(level, message)`.*

**Flush** — Appending the whole capture buffer to today's rolling daily file and then emptying the buffer.
A no-op that does not drain when file capture is disabled or the buffer is empty.
*code names that mean this: `FlushLogs()`.*

**Forced flush** — A deliberate flush invoked at one of exactly five hazard points (request-level error, the
end of each of the two generation paths, model-load failure, context-creation failure) so evidence reaches
disk before a native crash can destroy it.
*code names that mean this: the `FlushLogs()` call sites in the local backend.*

**Snapshot export** — Overwriting a caller-named file with the current capture buffer content **without**
clearing the buffer; produces a zero-byte file when the buffer is empty and reports nothing to the caller.
*code names that mean this: `SaveLogsToFile(path)`, `SaveSystemLogs`.*

**Rolling daily log file** — The plain-text, append-only, UTF-8-without-byte-order-mark file named for the
local calendar date inside the log directory, created lazily on the first non-empty flush of a day and never
pruned, capped, rotated or compressed.
*code names that mean this: `llamasharp_<yyyyMMdd>.log`, `LogDirectory`.*

**Log entry format** — The fixed line shape `[yyyy-MM-dd HH:mm:ss.fff] [LEVEL] message`, in local time,
with exactly three fractional-second digits and **no timezone or offset**.
*code names that mean this: the formatter in `LLamaSharpLogConfig`.*

**File capture / debug-stream echo / console echo** — The three independent output switches on the
diagnostic recorder: file capture (on by default; gates both flushing and auto-flush), debug-stream echo
(on by default; also the only channel on which the recorder reports its own internal failures), and console
echo (off by default; interleaves log lines into the operator's transcript when on).
*code names that mean this: `EnableFileLogging`, `EnableDebugOutput`, `EnableConsoleOutput`.*

**Maximum buffer size** — The character-count threshold, default 10000, above which an engine-sourced append
immediately triggers a flush. Unvalidated: any integer, including zero and negatives, is accepted.
*code names that mean this: `MaxBufferSize`.*

**Diagnostic trace channel** — The developer-build-only sink to which backends write full request bodies,
full reply bodies and failure detail. It is never surfaced to the operator and **may contain complete
prompts and conversations, so it must be treated as sensitive in any deployment.**
*code names that mean this: `System.Diagnostics.Debug.WriteLine` call sites.*

---

## K. Packaging and distribution

**Build profile** — A named set of compilation and packaging switches selected by name at build time. Four
exist: a development profile, an ordinary optimised profile, a *native profile*, and a *bundled profile*.
*code names that mean this: `Configuration`; `Debug`; `Release`; `Compact`; `SingleFile`.*

**Native profile** — The build profile that compiles ahead-of-time to native machine code with
whole-framework dead-code removal, size-first code generation, symbol stripping and no identity metadata.
Despite its documentation it produces a folder, not a single file, and it has never actually been released.
*code names that mean this: the `Compact` configuration, `PublishAot`, `IlcOptimizationPreference`.*

**Bundled profile** — The build profile that actually ships: one compressed self-extracting file,
just-in-time compiled, conservatively trimmed, retaining identity metadata.
*code names that mean this: the `SingleFile` configuration, `PublishSingleFile`, `EnableCompressionInSingleFile`.*

**Dead-code elimination** — Reachability-based removal of unreferenced code from the artifact, with two
selectable aggressiveness levels (whole-framework versus opt-in-only).
*code names that mean this: `PublishTrimmed`, `TrimMode=full`, `TrimMode=partial`, trimming, tree shaking.*

**Ahead-of-time native compilation** — Compiling the managed program to native machine code at build time so
that no just-in-time compiler ships and start-up is process load only.
*code names that mean this: `PublishAot`, Native AOT.*

**Self-contained artifact** — An executable that embeds the language runtime so the target machine needs
nothing pre-installed.
*code names that mean this: `SelfContained`, `--self-contained`, `UseAppHost`.*

**Platform triple** — The identifier naming the operating system and processor architecture a build targets.
It appears literally inside published asset names.
*code names that mean this: runtime identifier, RID, `win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`.*

**Platform matrix row** — One entry defining a build-agent operating system, a platform triple, an executable
suffix and a row key. Two exist in the shipping pipeline.
*code names that mean this: `strategy.matrix.include`.*

**Fan-in join** — The rule that the publication job runs only after every platform row succeeds, so a
half-published release cannot exist.
*code names that mean this: `needs: build`.*

**Release file name** — The public download name of a published artifact, formed as
`chatdbg-<platform key>-<platform triple><suffix>`.
*code names that mean this: `RELEASE_NAME`, `chatdbg-windows-win-x64.exe`, `chatdbg-linux-linux-x64`.*

**Release version** — The tag and title component of a published release: the operator's input verbatim,
otherwise a `v` prefix plus the version scraped out of the console front end's project descriptor.
*code names that mean this: `release_version`, `PROJECT_VERSION`, `VERSION`.*

**Identity metadata** — The version, title, description, company, product and copyright stamped into the
artifact. Kept by the bundled profile, stripped by the native profile. **The version fact is declared in
three independent places that no build step reconciles.**
*code names that mean this: `GenerateAssemblyInfo`, `AssemblyTitle`, `Company`, `Product`, `Copyright`, `Version`, `AssemblyVersion`, `FileVersion`.*

**Toolchain pin document** — The repository-root machine-readable file declaring the exact build toolchain
version and its roll-forward policy.
*code names that mean this: `global.json`, `sdk.version`, `rollForward: latestFeature`.*

**Native inference payload** — The large per-platform machine-learning backend library shipped inside the
artifact. **Two mutually exclusive backend payloads (processor-only and graphics-accelerated) are
referenced simultaneously, so every published output carries both sets of native binaries.**
*code names that mean this: `LLamaSharp.Backend.Cpu`, `LLamaSharp.Backend.Cuda12`, `llama.dll`, `libllama.so`, `<RID>/native/`.*

**Self-extraction set** — The payloads carried inside the single bundled file and unpacked to a per-user
cache before the program runs.
*code names that mean this: `IncludeNativeLibrariesForSelfExtract`, `IncludeAllContentForSelfExtract`, `DOTNET_BUNDLE_EXTRACT_BASE_DIR`.*

**Culture-invariant operation** — Running with no culture data present, so comparison, casing and formatting
collapse to invariant behaviour regardless of machine locale. Enabled in both distribution profiles, which
**changes list ordering and timestamp rendering relative to a development build**.
*code names that mean this: `InvariantGlobalization`.*

**Symbolic message keys** — Replacement of framework exception and message sentences with short identifier
keys to reduce artifact size. Because the product interpolates platform exception messages into
operator-facing text, this is **user-visible in error output** of a packaged build.
*code names that mean this: `UseSystemResourceKeys`.*

**Runtime-configuration side-car** — The companion configuration document a bundled just-in-time artifact
needs in order to start. Suppressed in both distribution profiles.
*code names that mean this: `GenerateRuntimeConfigurationFiles`, `runtimeconfig.json`.*

**Fatal-error contract** — The single machine-readable signal a packaged artifact emits: exit code 0 on a
clean shutdown, or one line beginning `Fatal error: ` on standard output followed by exit code 1.
*code names that mean this: the top-level catch block in the entry point, `Environment.Exit(1)`.*

**Publication credential** — The long-lived repository secret authorising creation of a public release.
*code names that mean this: `GH_PATT`, personal access token.*

---

## Notes on canonicalization

These are the rules applied when collapsing 291 collected terms into the entries above. They are recorded so
that a reimplementer can trace any source name back to a product term.

1. **"logprobs" → *token log probability*.** The single most overloaded name in the source. Every
   occurrence of `logprobs`, `logProb`, `LogProb`, `log_prob`, "log probability", "per-token confidence
   data" and "confidence value" was resolved to one of four distinct product terms, chosen by what the
   value actually is: the **stored value** (*token log probability*), the **computed linear value**
   (*derived probability*), the **switch** (*confidence capture*), or the **display preferences**. Sixteen
   collected glossary entries collapsed into these four plus *token confidence record* and
   *token-confidence list*.

2. **Scale is part of the definition.** *Derived probability* is fixed as a 0–1 fraction. The source
   contains four confidence-to-colour maps on two different scales, and several formatters that feed a 0–1
   value into a 0–100 comparison. The glossary states the scale once; the clone must not reproduce the
   inconsistency.

3. **The three backend literals are frozen.** The persisted values `azure`, `bedrock` and `llama` are wire
   data, not implementation names, and must survive the port unchanged. The generic product terms are
   *hosted cloud model service*, *managed cloud model marketplace* and *local inference backend*
   respectively. Vendor product names appear only in code-name annotations.

4. **"shell" versus "front end".** The source used "shell", "host", "UI", "REPL" and "GUI" for two
   different programs and for the abstract idea of a program that owns the terminal. This PRD uses **front
   end** for the concept and **console front end** / **full-screen front end** for the two instances.
   "Shell" survives only inside code-name annotations, where it names assemblies and files. Note that one
   source file named `ChatShell.cs` exists in *both* front-end projects and one of the two is dead — the
   annotations distinguish them by path.

5. **"provider" → *model backend*.** `IAIService`, "AI service", "provider", "provider adapter" and "back
   end" all named one contract. *Backend key*, *backend registry* and *backend display name* were split out
   because each is separately observable and separately persisted.

6. **"chat history" → two terms.** The source used one word for the in-memory data and for its rendered
   view. Split into *conversation record* (data, category B) and *transcript* (rendering, category H),
   because the full-screen front end can hold a stale transcript over a mutated record.

7. **"system prompt" versus the input prompt.** The source used "prompt" for both the instruction text sent
   to the model and the characters printed before the input line. Resolved to *instruction prompt (system
   prompt)* and *prompt string* respectively.

8. **"credential source" → one term, four literal values.** Three collected entries ("credential source
   label", "credential source string", "credential source") describe one thing. The four literal values are
   recorded as user-visible strings because the operator reads them and support processes quote them.

9. **The OS vault is named generically.** The source names a single platform's credential store throughout,
   including in a user-visible label. The product term is *OS credential vault*; the platform product name
   is retained in the code-name annotation and in the frozen label literal, because that literal is what an
   existing installation prints.

10. **Marker controls unified.** Five collected names ("token-probability marker", "confidence indicator",
    "probability indicator", "probability marker", "lozenge") describe one clickable glyph. One term:
    *confidence marker*.

11. **Colour maps unified as one term with a plurality warning.** Rather than record four colour-band terms,
    one term (*confidence colour band*) carries the explicit statement that four inconsistent maps exist and
    that a clone must define exactly one.

12. **Names that lie about their value are flagged, not renamed.** Where a source name misdescribes what it
    holds — the hidden-from-model flag, the prompt offset, the grid alternatives cap, the alternatives count
    that also sets the sampler limit, the "text piece" counted as a token — the entry says so explicitly.
    That mismatch is exactly the information a reimplementer needs and would lose to a tidy rename.

13. **Dead concepts are kept, and labelled.** Terms that exist only in unreachable code (*flowing heat-map
    view*, and the three unregistered commands) are retained in the glossary with a pointer to PRD §2.3, so
    that a reader encountering them in the source knows immediately that they are not requirements.

### Terms that could not be confidently canonicalized

- **"Top-K"** — the source uses one setting name for two different things: the count of alternatives
  *requested and displayed* per position, and (on the local backend only) the sampler's *selection limit*.
  These are recorded together under *alternatives-per-position count* with the dual role called out, but
  they are arguably two settings that a clone should separate. Marked **INFERRED** that they were meant to
  be one value.
- **"Token"** — for the local inference backend, everything the product calls a token is actually a
  *text piece* (a decoded stream fragment). Both terms are defined; which one a given requirement means
  cannot always be determined from the source, because the two coincide for most ASCII text.
- **"Grid View Max Alternatives"** — the name says grid, the behaviour covers grid *and* the full-screen
  list panel, and the row-per-token table ignores it in favour of a hard-coded three. Recorded as one term
  with the discrepancy stated; a clone must decide whether it is one setting or two.
- **"Provider" as a settings value versus as a contract** — the persisted string `provider` selects a
  backend, but the same word names the contract, the registry and the adapter. Split into *backend key* and
  *model backend*, but source text using "provider" alone is genuinely ambiguous and each occurrence had to
  be resolved from context.
- **"Log" / "logging"** — spans three unrelated things: the engine's own native diagnostic output, the
  product's commentary entries, and the developer-only trace channel. Split into *engine diagnostic line*,
  *application diagnostic entry* and *diagnostic trace channel*; the source's single word "logs" in the
  unregistered export command cannot be resolved to one of them from the code, since that command writes
  nothing.
