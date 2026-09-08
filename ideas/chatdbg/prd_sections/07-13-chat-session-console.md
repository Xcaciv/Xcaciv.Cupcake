### 7.13 Interactive Chat Session (Line-Oriented Shell)

**Description**

This feature is the line-oriented terminal session that a developer actually sits in front of. It owns the terminal for the whole life of the process: it prints a banner, tells the user which model provider is active and whether credentials were found, then repeatedly writes a prompt, reads one line of typed text, decides whether that line is a *control instruction* or a *conversation turn*, does the corresponding work, prints the outcome, and asks again. It ends only when the user asks it to end, and then it prints a farewell, releases every provider connection, and exits.

Its reason to exist is that a developer debugging code wants a conversation with a language model *inside the shell they are already in*, and wants that conversation to be inspectable and manipulable — swap the provider or the model mid-session, edit the transcript, load and save it, and (the product's distinguishing capability) see the per-token probabilities behind each answer rendered as a table or a grid of cards. This session is the glue that makes all of that reachable from one blinking cursor. It owns almost no domain logic of its own; what it owns is *session lifecycle*, the *dispatch decision*, and the *rendering of a reply*.

There is no authentication, no authorization, no multi-user concept and no background actor anywhere in this feature. The process runs entirely with the invoking operating-system user's privileges, and the only "identity" in play is whichever provider credentials the environment or the machine's secret store supplies. The session is strictly single-threaded and strictly serial: one turn is fully rendered before the next prompt appears, there is no streaming, no spinner, no elapsed-time display, no timeout and no cancellation of any kind. A note on product scope: a second, full-screen terminal interface exists as a separate feature over the same command set; nothing in this section describes it, and the two are not required to behave identically.

---

**User stories**

- **US-13.1** — As a developer at a terminal, I want to type free text at a prompt and get a language-model reply printed back in the same terminal, so that I can reason about code without leaving my shell.
- **US-13.2** — As a developer, I want any line I begin with a forward slash to be treated as an instruction to the application rather than as conversation, so that control actions are handled locally and instantly instead of being billed to a model.
- **US-13.3** — As a developer, I want the prompt itself to show the provider and model that will answer my next message, so that I always know what I am talking to and can see the effect of a configuration change immediately.
- **US-13.4** — As a developer, I want a startup banner that reports the active provider, model, system prompt and the absolute path of my settings file, so that I can confirm the session is configured the way I expect before I spend a request.
- **US-13.5** — As an operator configuring the machine, I want the session to tell me at startup whether the active provider's credentials were found and *where they came from* — or, if they were not found, exactly which environment variables or instructions to use — so that I can fix configuration without guessing.
- **US-13.6** — As a developer analysing model behaviour, I want each reply's per-token probabilities rendered visually with the most likely alternatives for each token, so that I can see where the model was confident and where it nearly said something else.
- **US-13.7** — As a developer analysing a long reply, I want the token display to sample the beginning, middle and end of a long response by default, so that a thousand-token answer does not flood my scrollback.
- **US-13.8** — As a developer, I want the session to survive every recoverable failure — bad settings, missing prompt, unknown provider, provider outage, malformed command — printing the reason and returning me to the prompt, so that a single mistake never costs me my conversation.
- **US-13.9** — As a developer, I want an explicit instruction that ends the session cleanly, so that provider resources are released, diagnostic buffers are flushed, and the process exits with a success status my calling script can check.

---

**Use cases**

#### UC-13.1 — Start a session (realizes US-13.4, US-13.5)

**Preconditions**
- The user has launched the application. The application accepts **no command-line arguments**; nothing is read from the invocation line.
- The operating-system user profile, local-application-data and roaming-application-data locations are resolvable (or a documented fallback applies).

**Main flow**
1. The session object is constructed. Construction eagerly creates, in this order: an empty in-memory transcript (new random session identifier, creation timestamp in coordinated universal time); a live settings record populated with built-in defaults; a settings store bound to `<user profile>/.ChatDbg/settings.json`; a transcript import/export store; a system-prompt store bound to `<local app data>/ChatDbg/system_prompts`; **all three** provider adapters (`azure`, `bedrock`, `llama`) whether or not they will be used; and the command registry.
2. Constructing the system-prompt store creates its directory if absent and, if the directory holds no prompt files, writes four starter prompts named `default`, `code-reviewer`, `algorithm-helper`, `security-expert`. This happens synchronously, during construction, before any of the session's own output.
3. The session loads persisted settings and copies them field by field into the live settings record that every command and provider adapter already holds a reference to.
4. The session loads the system prompt named by the settings (default `default`) from the prompt store; on success its content becomes the live system-prompt content and the stored prompt's *last used* timestamp is rewritten to the file.
5. The session prints the welcome banner (provider, model, system prompt name, absolute settings-file path, the provider list, and a security advisory), followed by provider-specific blocks where applicable.
6. The session runs a one-shot, read-only credential advisory and prints its outcome (see UC-13.2).
7. The session enters the read-evaluate-print loop and writes its first prompt.

**Alternate flows**
- **A1 — No settings file exists.** The settings store silently creates one containing the defaults and returns those defaults. Nothing is printed about it.
- **A2 — The user profile location cannot be resolved.** The settings file relocates to the system temporary directory; the banner's settings-file line shows that path. Nothing is printed about it.
- **A3 — The named system prompt does not exist.** The built-in fallback prompt content is used. **Nothing is printed**, and no file is written.
- **A4 — The settings file already contains file-stored credential values.** The settings store prints a migration warning followed by literal shell assignment lines that **echo the secret values in cleartext**.
- **A5 — The machine secret-store flag is on.** An informational line is printed; if the flag is on but the host is not the platform that supports that store, a "not available on this platform" line is printed instead.
- **A6 — The active provider is the local-model provider.** A `LLama Configuration:` block is printed after the banner (context size; GPU layers, with ` (CPU-only)` appended when the count is 0; the GPU device line **only** when a device value is set; threads, printing the word `default` when the value is 0; batch size).
- **A7 — Token probability analysis is enabled.** The single line `Token probability analysis is enabled. Type '/logprobs' for details.` is printed after the banner.

**Error flows**
- **E1 — Settings load raises a fault.** `Error loading settings: {message}` then `Using default settings.` are printed; the session continues on the built-in defaults established at construction. (The settings store may additionally have printed its own `Error loading settings: {message}` first, so the message can appear twice.)
- **E2 — System-prompt load raises a fault.** `Error loading system prompt: {message}` then `Using default system prompt.` are printed; the built-in fallback content is used.
- **E3 — A stored prompt file is unreadable or malformed.** The store prints `Error loading system prompt from {path}: {message}` and the prompt is then treated as absent — so flow A3 applies and *no further message* is printed.
- **E4 — The prompt directory cannot be created or seeded** (read-only home, no permission, disk full). The fault escapes construction *before any of the session's own output*. The process prints only `Fatal error: Error saving system prompt: {message}` and exits with status `1`. **This is the only fatal startup failure**; no banner and no prompt are shown.

**Postconditions**
- A live settings record, a live system-prompt content string, an empty transcript, a registry of fifteen commands and a table of three provider adapters exist in memory.
- The terminal shows the banner, the advisory and one prompt.
- No network call has been made.

---

#### UC-13.2 — Report credential configuration at startup (realizes US-13.5)

**Preconditions**
- Settings have been loaded and the banner has been printed.

**Main flow**
1. The session reads the active provider name from the live settings.
2. It looks the provider up among the three known keys and asks the corresponding adapter for a synchronous, network-free self-check of whether it is configured.
3. When the adapter reports configured, the session prints one transparency line naming the **source** of the credential rather than its value: `Azure credentials loaded from: {source}` for the cloud provider, `AWS credentials loaded from: {source}` for the managed-model provider, or `Local LLM model loaded from: {model id}` for the local provider. The source string is exactly one of `environment variable ({VARIABLE_NAME})`, `Windows Credential Manager`, `settings file (deprecated)` or `not set`.
4. The loop starts regardless of the outcome; the advisory never blocks.

**Alternate flows**
- **A1 — No provider name is configured.** `Warning: No AI provider configured.` and `   Use '/set provider azure', '/set provider bedrock', or '/set provider llama' to configure.` are printed, followed by a blank line, and the advisory ends.
- **A2 — The provider name is not one of the three known keys.** `Warning: Unknown AI provider: {provider}` is printed and the advisory ends — **with no trailing blank line**, so the warning butts directly against the first prompt.
- **A3 — Provider known but the adapter reports itself not configured.** `Warning: {provider display name} service is not configured.` is printed, followed by a provider-specific remedy block, followed by a blank line:
  - Cloud provider: method 1 shows the environment-variable form `set CHATDBG_AZURE_API_KEY=your-api-key`; method 2 is shown **only on the platform that has a machine secret store** and reads `/set enablewincred` then `/set wincred azureApiKey your-api-key`; and if the endpoint setting is unset, `   Also set your Azure endpoint: /set azureEndpoint https://your-resource.openai.azure.com/` is appended.
  - Managed-model provider: method 1 lists `CHATDBG_AWS_ACCESS_KEY` and `CHATDBG_AWS_SECRET_KEY` and mentions the standard `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` alternatives; method 2 (secret-store platform only) reads `/set enablewincred` then `/set wincred awsAccessKey …` and `/set wincred awsSecretKey …`.
  - Local provider: three numbered steps — download a model file in the local-model file format, `/set modelId C:\path\to\your\model.gguf`, and ensure the file exists and is accessible.
- **A4 — Only the secret half of the managed-model credential pair is present.** A source line is still printed, and it reports the **access key's** source, never the secret key's.

**Error flows**
- **E1 — The host is not the platform with a machine secret store.** All "method 2" blocks are omitted entirely; only the environment-variable method is shown. Following the omitted guidance elsewhere dead-ends (see QUIRK-13.16).

**Postconditions**
- Exactly one advisory outcome has been printed. No credential value has been displayed by this feature (only its source label). No state has changed.

---

#### UC-13.3 — Submit a conversation turn (realizes US-13.1, US-13.3, US-13.6)

**Preconditions**
- The loop is at the prompt `ChatDbg ({provider}/{model id})> `.

**Main flow**
1. The user types a line whose first character is **not** `/` and presses Enter.
2. The raw, **untrimmed** line is appended to the transcript as a `user` message with a coordinated-universal-time timestamp, no probability data, and the is-command flag clear. This happens **before any validation**.
3. The provider adapter is looked up by the current provider key.
4. The adapter's configuration self-check is run. No network traffic occurs at this step, and credential values are re-read from the environment on every turn rather than snapshotted.
5. A blank line and then `Thinking...` are printed.
6. **Token probability analysis disabled:** the adapter's plain send operation is called with the whole transcript and the settings. The returned reply text is appended to the transcript as an `assistant` message with no probability data, and printed with one blank line before and one after.
7. **Token probability analysis enabled:** `Log probabilities enabled - requesting with top-k={K}` is printed first; the adapter's with-probabilities operation is called; the reply is appended to the transcript as an `assistant` message **with** the returned per-token probability list attached; the reply text is printed with one blank line before and one after; and the visualization (UC-13.4) is rendered.
8. The loop returns to the prompt.

**Alternate flows**
- **A1 — Provider or model was changed earlier in the session.** The prompt string is rebuilt from the live settings on every iteration, so the change is visible on the very next prompt with no restart.
- **A2 — Probabilities were requested but the returned list is empty or absent.** No visualization is drawn; instead a blank line and then `Note: Log probabilities were requested but none were returned by the model.` and `This could be due to the model not supporting this feature or an API limitation.` are printed.
- **A3 — The reply envelope carries no text on the probability path.** The literal string `Error: Response text expected, none recieved.` (misspelled, with a trailing period) is stored in the transcript, while the screen shows an empty reply between the two blank lines.
- **A4 — The reply envelope carries no text on the plain path.** Each adapter supplies its own differently-spelled placeholder, which is both stored and displayed (see FR-13.53).

**Error flows**
- **E1 — The provider name is not one of the three known keys.** `Error: Unknown AI provider: {provider}` is printed and the turn ends with no reply. The user's message **remains** in the transcript.
- **E2 — The provider is known but not configured.** Two lines are printed and the turn ends without contacting the network: `Error: {provider display name} service is not configured. Use environment variables or Windows Credential Manager to configure credentials securely.` then `   Type '/set' to see current configuration and setup instructions.` The user's message **remains** in the transcript.
- **E3 — The provider call raises a fault** (network, authentication, quota, model load, native crash surfaced as a fault). `Error getting AI response: {fault message}` is printed; the user's message stays in the transcript as an orphan with no matching assistant message; nothing is rolled back.
- **E4 — The provider reports failure by *returning* an envelope carrying an error message rather than raising.** This session reads neither the envelope's error message nor its elapsed-time field. The screen shows a blank reply between two blank lines, the transcript stores the placeholder from A3 or A4, and the provider's real error text is discarded.
- **E5 — Any other fault escapes the iteration.** `Error: {fault message}` is printed and the loop continues; the full fault detail goes only to a platform debug channel, invisible in a normal run. The session is never terminated by a handled error.
- **E6 — The provider never answers.** There is no timeout, no progress indicator and no interrupt handling. The prompt does not return; the only escape is killing the process, which skips the shutdown flush.

**Postconditions**
- The transcript has gained exactly one `user` message and, on success, exactly one `assistant` message.
- Failed turns leave orphan user messages that are re-sent as context on the next successful turn.
- Nothing is written to disk by this use case.

---

#### UC-13.4 — Render token probabilities for a reply (realizes US-13.6, US-13.7)

**Preconditions**
- Token probability analysis is enabled and the provider returned a non-empty ordered list of per-token probability records.

**Main flow**
1. A blank line is printed, then a left-justified horizontal rule titled `Token Probabilities Analysis` in yellow.
2. The session decides how many tokens to render (see alternate flows) and in which of the two layouts.
3. **List layout (default):** a rounded-border table that expands to the terminal width, with four columns — `№` (centred), `Token` (fixed 20 characters), `Probability` (centred), `Top Alternatives` (fixed 50 characters). One row per token; the index cell is grey and shows the token's **1-based absolute position within the whole reply**; the alternatives cell lists **at most 3** alternatives, one per line, each as `token (probability)`, or the dimmed literal `(none)` when the token has none.
4. **Grid layout:** cards laid out left-to-right at `max(1, terminal width / 40)` columns, the final row padded with empty cells so every row is full. Each card is a rounded, expanded panel headed by the grey 1-based absolute index (`#7`) and containing `Token: {token}`, `Prob: {coloured probability}`, and — only when the token has alternatives — an `Alternatives:` block listing up to the configured grid-alternative cap as `- {alt} ({coloured probability})`, with a dimmed `+ N more` line when more exist than were shown.
5. A blank line and a second, untitled left-justified rule close the block.

**Alternate flows**
- **A1 — Show-all mode is on.** The entire token list is rendered in the chosen layout, with no captions.
- **A2 — Sample mode is on (the default) and the list has 15 or fewer entries.** The entire list is rendered anyway, with no captions, because sampling would show everything.
- **A3 — Sample mode is on and the list has more than 15 entries.** Three blocks are rendered in this fixed, non-configurable order, each preceded by its own blue caption and separated by a blank line: `Beginning Tokens:` (the first 5, numbered from 1); `Middle Tokens:` (5 tokens starting at whole-number index `count / 2 − 2`, numbered from that index plus one); `End Tokens:` (the last 5, numbered from `count − 5 + 1`).
- **A4 — A token's text is absent.** It renders as the dimmed literal `(null)`.
- **A5 — A token's text contains markup-significant or control characters.** Square brackets are doubled to escape console markup *before* the four control characters line feed, carriage return, tab and NUL are replaced with the dimmed visible escapes `\n`, `\r`, `\t`, `\0`.

**Error flows**
- **E1 — The terminal width query fails** (no console attached). The fault is caught by the loop's blanket guard: `Error: {message}` is printed and the visualization is lost. The reply text itself had already been printed, so the user sees an answer with no analysis. (INFERRED for the no-console case; measured behaviour with redirected output is that the query returns 80 and rendering proceeds at 2 columns without error.)
- **E2 — Malformed markup slips through escaping.** INFERRED: the rendering layer raises and the same blanket guard catches it; the loop continues.

**Postconditions**
- The analysis block has been printed in full or not at all. No state changes.

---

#### UC-13.5 — Run a control instruction (realizes US-13.2, US-13.8)

**Preconditions**
- The loop is at the prompt.

**Main flow**
1. The user types a line whose first character is `/`.
2. The leading `/` is dropped and the remainder is split on **the space character only**, discarding empty segments. Tabs are not separators; there is no quoting, no escaping and no flag syntax at this layer.
3. The first segment, lower-cased, is the lookup key; the remaining segments are passed through **with their original casing preserved**.
4. The registry entry is invoked and run to completion. Nothing else is processed meanwhile.
5. The returned result carries three signals: success, an optional message, and an exit request.
6. If the result requests exit, the loop breaks (see UC-13.6).
7. Otherwise, if the message is present and non-empty, it is printed in a **single** write as `✓ {message}` on success or `✗ {message}` on failure. A multi-line message therefore carries the marker only on its first physical line. A result with no message prints nothing at all.

**Alternate flows**
- **A1 — Runs of spaces in arguments.** Empty segments are discarded, so consecutive spaces collapse irrecoverably before the command can see them. Commands that rejoin their arguments with a single space silently normalise values containing double spaces.
- **A2 — The command performs its own terminal input/output while running.** Four registered instructions read further lines from the same standard input while the loop is blocked inside them: the credential-migration flow (a numbered 1–3 menu, then `Would you like to remove credentials from the settings file now? (y/N)`), the secret-store enablement flow (`Do you want to enable Windows Credential Manager for secure credential storage? (y/N)`), the token-inspection flow (`Do you want to continue with probability analysis? …(y/n)`), and the prompt-editing flow (reads lines until a line equal to the literal `END`). All affirmative prompts accept `y` or `yes`, case-insensitively; anything else is negative. The loop has no knowledge of these reads and simply resumes afterwards.
- **A3 — A command mutates shared state.** The settings record and the transcript are shared by reference, so mutations are visible immediately — most obviously in the next prompt string.

**Error flows**
- **E1 — The line is `/` alone, or `/` followed only by spaces.** `✗ Invalid command` is printed.
- **E2 — The lookup key is not registered.** `✗ Unknown command: /{lower-cased name}. Type '/help' for available commands.` is printed. Note that the per-command help facility uses a *different* wording for the same condition (`Unknown command: {name}` — no slash, no hint).
- **E3 — The command returns a failure result.** `✗ {message}` is printed and the loop continues.
- **E4 — The command raises a fault.** `Error: {fault message}` is printed and the loop continues; full detail goes only to the debug channel.
- **E5 — Partial mutation before a failure.** Nothing is rolled back; there is no transaction or undo concept.

**Postconditions**
- At most one command has run. Control instructions are **never** appended to the transcript by this session.

---

#### UC-13.6 — End the session (realizes US-13.9)

**Preconditions**
- The session is running.

**Main flow**
1. The user types `/exit` or `/quit`.
2. The command returns a result whose exit flag is set and whose message is absent, so no marker line is printed.
3. The loop breaks and `Goodbye!` is printed.
4. The run method returns; the enclosing scope disposes the session, which releases **all three** provider adapters in registration order, whether or not they were ever used.
5. Disposing the local-model adapter releases its model and context handles and flushes its captured native diagnostic buffer to `<roaming app data>/ChatDbg/Logs/llamasharp_{yyyyMMdd}.log`, where the date component is the **local** date.
6. The process exits with status `0`.

**Alternate flows**
- **A1 — The transcript was never exported.** It is discarded. Nothing is auto-saved at shutdown.

**Error flows**
- **E1 — Standard input reaches end of stream** (closed stream, exhausted pipe). The read returns nothing, which the blank-input guard treats as "keep going", so the prompt is re-written immediately and forever with no output and no exit. The process must be killed. See QUIRK-13.2.
- **E2 — The process is interrupted.** No interrupt handling exists; INFERRED that the disposal path does not run, so the local-model diagnostic buffer is not flushed.
- **E3 — Any fault escapes the run method entirely.** The session is disposed as the fault unwinds, *then* `Fatal error: {fault message}` is printed to standard output and the process exits with status `1`.

**Postconditions**
- All provider adapters are released; the diagnostic buffer is flushed; the in-memory transcript is gone.

---

**Functional requirements**

*Process lifecycle*

- **FR-13.1** The application MUST accept no command-line arguments; configuration comes only from the settings file, environment variables and in-session instructions. (realizes US-13.4)
- **FR-13.2** The application's entire body MUST be: construct one session, run it to completion, dispose it, return status `0`. (realizes US-13.9)
- **FR-13.3** Any fault escaping the run method MUST print `Fatal error: {fault message}` to standard output and return status `1`; the session MUST be disposed before that message is printed. (realizes US-13.8)
- **FR-13.4** Session construction MUST eagerly create, in this order: the empty transcript; the settings record with built-in defaults; the settings store; the transcript import/export store; the system-prompt store; **all three** provider adapters keyed `azure`, `bedrock`, `llama`; and the command registry. No collaborator is created lazily and none can be substituted from outside the process.
- **FR-13.5** All three provider adapters MUST be constructed at startup and disposed at shutdown regardless of which one is selected, in registration order. (See QUIRK-13.7 for the observable side effects of this.)
- **FR-13.6** The command registry MUST contain exactly fifteen entries keyed by each command's own lower-cased name: `inject`, `pop`, `import`, `export`, `model`, `set`, `logprobs`, `prompt`, `demologprobs`, `clear`, `exit`, `quit`, `tokenize`, `inspect`, `help`. Registration is last-write-wins, so a duplicate name would silently shadow an earlier entry. (realizes US-13.2)
- **FR-13.7** The three instructions `export-logs`, `export-analysis` and `show-analysis`, which exist in the shared command set, MUST NOT be registered by this session and MUST therefore produce the unknown-command error when typed. (See QUIRK-13.5.)
- **FR-13.8** The startup sequence MUST run in exactly this order: load settings → load system prompt → print banner → print credential advisory → enter the loop. There MUST be no splash delay, no version check and no network call during startup. (realizes US-13.4)

*Settings load*

- **FR-13.9** Startup MUST copy exactly these fifteen fields from the persisted settings into the live settings record, in this order: provider, model identifier, temperature, maximum tokens, cloud endpoint, managed-service region, machine-secret-store flag, enable-probabilities flag, probabilities top-K, system-prompt name, local-model context size, local-model GPU layer count, local-model GPU device, local-model thread count, local-model batch size — followed by the three deprecated file-stored credential values.
- **FR-13.10** Startup MUST NOT copy the three display-mode settings (show-all-tokens, grid-layout, grid-maximum-alternatives) from the persisted settings, even though they are persisted and settable at runtime. Every session therefore begins in sample mode, list layout, with a grid alternative cap of 5. (See QUIRK-13.1.)
- **FR-13.11** Startup MUST perform **no range validation** on any loaded value. Values outside the ranges the setting instructions enforce (for example a top-K of `9999`, a temperature of `50`, a context size of `0`, a maximum-token count of `-1`) MUST be loaded and used verbatim. (See QUIRK-13.11.)
- **FR-13.12** If loading settings raises a fault, the session MUST print `Error loading settings: {message}` then `Using default settings.` and continue with the built-in defaults. (realizes US-13.8)
- **FR-13.13** The built-in defaults MUST be: provider `azure`; model identifier `gpt-4`; temperature `0.7`; maximum tokens `1000`; cloud endpoint unset; region `us-east-1`; system-prompt name `default`; enable-probabilities `false`; probabilities top-K `5`; show-all-tokens `false`; grid-layout `false`; grid-maximum-alternatives `5`; machine-secret-store flag `false`; local-model context size `4096`; GPU layer count `0`; GPU device unset; thread count `0`; batch size `512`. All of these read as tunable defaults rather than business rules, except the provider and model identifier, which determine which adapter is addressed.
- **FR-13.14** If the settings file does not exist, the store MUST create one containing the defaults and return those defaults, with no message printed. If the user-profile location cannot be resolved, the settings file MUST relocate to the system temporary directory, again with no message.
- **FR-13.15** The settings file path MUST be `<user profile>/.ChatDbg/settings.json`, and its absolute resolved value MUST be the value shown on the banner's settings-file line. (realizes US-13.4)

*System prompt load*

- **FR-13.16** Startup MUST look up the prompt named by the system-prompt setting in the prompt store. On success its content becomes the live system-prompt content and the stored prompt's *last used* timestamp MUST be rewritten to its file — a write on **every** startup. (realizes US-13.4)
- **FR-13.17** If the named prompt is not found, the live system-prompt content MUST be set to the built-in fallback text `You are ChatDBG, a helpful debugging assistant. Help users analyze code, debug issues, and understand programming concepts.`, no file MUST be written, and **no message MUST be printed**.
- **FR-13.18** If the prompt load raises a fault, the session MUST print `Error loading system prompt: {message}` then `Using default system prompt.` and use the same fallback text. (realizes US-13.8)
- **FR-13.19** System prompts MUST live at `<local app data>/ChatDbg/system_prompts/{sanitized name}.json`, with invalid file-name characters replaced by `_`. Prompt-name matching therefore inherits the host file system's case rules. (See QUIRK-13.14.)
- **FR-13.20** On a first run where the prompt directory holds zero prompt files, exactly four starter prompts MUST be written during session construction, named `default`, `code-reviewer`, `algorithm-helper`, `security-expert`, with the fixed bodies recorded in the Source notes' constant C4. Failure to create or write that directory MUST be fatal per FR-13.3.

*Welcome banner*

- **FR-13.21** The banner MUST be printed verbatim in this order and geometry: a box-drawn frame whose corners are U+2554, U+2557, U+255A, U+255D, whose horizontal rule is U+2550 repeated 48 times and whose verticals are U+2551, containing the two centred rows `ChatDbg` and `AI-Powered Debugging Assistant`; every banner line is exactly 50 characters wide with 48 characters between the verticals. Then a blank line; then `Provider: {provider}`, `Model: {model id}`, `System Prompt: {system prompt name}`, `Settings file: {absolute settings path}`; then a blank line; then `Available providers: azure, bedrock, llama (local LLM)`; then `Security Enhancement: Credentials are now managed via environment variables`, `   or Windows Credential Manager for improved security.`, `   See '/set' command for more details.`; then a blank line. (realizes US-13.4)
- **FR-13.22** When and only when the active provider key is exactly the lower-case string `llama`, the banner MUST additionally print `LLama Configuration:` followed by `   Context Size: {value}`; `   GPU Layers: {value}` with ` (CPU-only)` appended when the value is `0`; `   GPU Device(s): {value}` **omitted entirely** when the value is unset or empty; `   Threads: {value}` printing the literal word `default` when the value is `0`; `   Batch Size: {value}`; then a blank line.
- **FR-13.23** When and only when token probability analysis is enabled, the banner MUST additionally print `Token probability analysis is enabled. Type '/logprobs' for details.`
- **FR-13.24** The banner MUST always end with a blank line, then `Type '/prompt list' to manage system prompts.`, `Type '/help' to see available commands or start chatting!`, `Type '/exit' or '/quit' to exit.`, then a blank line.
- **FR-13.25** The session MUST NOT configure the terminal's output encoding; all non-ASCII glyphs (banner frame, `✓` U+2713, `✗` U+2717, `№` U+2116) depend on the host terminal already being Unicode-capable. (See QUIRK-13.9.)

*Credential advisory*

- **FR-13.26** At startup the session MUST run exactly one read-only credential advisory with four mutually exclusive outcomes, and MUST NOT block the loop under any outcome. (realizes US-13.5)
- **FR-13.27** Empty provider name → print `Warning: No AI provider configured.` and `   Use '/set provider azure', '/set provider bedrock', or '/set provider llama' to configure.`, then a blank line, and return.
- **FR-13.28** Provider name not among `azure`, `bedrock`, `llama` (matched **case-sensitively**) → print `Warning: Unknown AI provider: {provider}` and return **with no trailing blank line**. (See QUIRK-13.10 and QUIRK-13.15.)
- **FR-13.29** Provider known but self-reported unconfigured → print `Warning: {provider display name} service is not configured.` followed by the provider-specific remedy block described in UC-13.2 A3, then a blank line. Display names are `Azure OpenAI`, `Amazon Bedrock`, `Local LLM (LLamaSharp)`.
- **FR-13.30** The machine-secret-store remedy ("method 2") blocks MUST be printed only on the platform that supports that store, determined by a plain operating-system check.
- **FR-13.31** Provider configured → print the credential *source*, never the value: `Azure credentials loaded from: {source}`, `AWS credentials loaded from: {source}`, or `Local LLM model loaded from: {model id}`. The source string MUST be exactly one of `environment variable ({VARIABLE_NAME})`, `Windows Credential Manager`, `settings file (deprecated)`, `not set`. (realizes US-13.5)
- **FR-13.32** For the managed-model provider, the reported source MUST always be the **access key's** source, even when only the secret key is present. (See QUIRK-13.13.)
- **FR-13.33** "Configured" MUST be defined per provider as: cloud provider = non-empty endpoint AND non-empty resolved key AND non-empty model identifier; managed-model provider = non-empty model identifier AND (resolved access key present OR the standard `AWS_ACCESS_KEY_ID` variable present); local provider = non-empty model identifier AND that path existing as a file.
- **FR-13.34** Credential values MUST NOT be snapshotted. Each read re-consults, in fixed priority: (1) environment variables in declared order, (2) the machine secret store when explicitly enabled, (3) the deprecated value in the settings file. Environment variable names are `CHATDBG_AZURE_API_KEY`; `CHATDBG_AWS_ACCESS_KEY` then `AWS_ACCESS_KEY_ID`; `CHATDBG_AWS_SECRET_KEY` then `AWS_SECRET_ACCESS_KEY`. A credential that appears in the environment mid-session MUST take effect on the next turn.

*The read–evaluate–print loop*

- **FR-13.35** Each iteration MUST write the prompt `ChatDbg ({provider}/{model id})> ` **without a trailing line break**, re-reading provider and model identifier from the live settings record every iteration so that a configuration change is visible on the very next prompt. (realizes US-13.3)
- **FR-13.36** Input that is absent, empty or whitespace-only MUST restart the iteration silently: nothing printed, nothing added to the transcript. End of stream is indistinguishable from blank input at this guard. (See QUIRK-13.2.)
- **FR-13.37** A line MUST be treated as a control instruction **if and only if** its first character is `/`. The line MUST NOT be trimmed first, so a line beginning with a space followed by `/help` is a conversation turn, and a conversation turn preserves all internal and trailing whitespace exactly. (realizes US-13.2)
- **FR-13.38** The prefix test as built is locale-aware rather than byte-exact; a reimplementer MUST decide explicitly between an ordinal test and the source behaviour. (See QUIRK-13.12.)
- **FR-13.39** Command results MUST be printed in one write as `✓ {message}` (U+2713) on success and `✗ {message}` (U+2717) on failure, and only when the message is non-empty; a result with no message MUST print nothing. Multi-line messages carry the marker on the first physical line only. (realizes US-13.2)
- **FR-13.40** The loop MUST terminate only when a command result carries the exit flag. Both `/exit` and `/quit` set it and both carry no message, so the only output on exit is `Goodbye!`. (realizes US-13.9)
- **FR-13.41** A blanket guard MUST wrap the whole command-or-chat step: any escaping fault prints `Error: {fault message}` and the loop continues; the full fault detail goes only to a platform debug channel. No handled error MUST ever end the session. (realizes US-13.8)
- **FR-13.42** Control instructions MUST NOT be appended to the transcript; the transcript's per-message is-command flag is never set by this session.

*Instruction parsing*

- **FR-13.43** Parsing MUST drop the leading `/` and split the remainder on the space character only, discarding empty segments. Tabs MUST NOT be separators; there MUST be no quoting, escaping or flag syntax at this layer. Runs of consecutive spaces collapse and are unrecoverable by the command. (realizes US-13.2)
- **FR-13.44** If nothing remains after the split, the result MUST be a failure whose message is exactly `Invalid command`.
- **FR-13.45** The first segment MUST be lower-cased for lookup; the remaining segments MUST be passed to the command with their original casing preserved.
- **FR-13.46** An unregistered key MUST produce a failure whose message is exactly `Unknown command: /{lower-cased name}. Type '/help' for available commands.` (Note this differs from the per-command help facility's `Unknown command: {name}`.)
- **FR-13.47** The loop MUST tolerate a command performing its own terminal output and reading further lines from the same standard input while it runs. Four instructions do so (credential migration menu plus a `(y/N)` confirmation; secret-store enablement `(y/N)`; token inspection `(y/n)`; prompt editing until a line equal to the literal `END`). Affirmative answers are `y` or `yes`, case-insensitive; anything else is negative. (See QUIRK-13.17.)

*Conversation turn*

- **FR-13.48** The user's raw line MUST be appended to the transcript as a `user` message with a coordinated-universal-time timestamp **before** provider lookup and the configuration check, so that it survives even when no reply is possible. (See QUIRK-13.3.)
- **FR-13.49** An unrecognised provider key during a turn MUST print `Error: Unknown AI provider: {provider}` and end the turn.
- **FR-13.50** An unconfigured provider MUST print `Error: {provider display name} service is not configured. Use environment variables or Windows Credential Manager to configure credentials securely.` followed by `   Type '/set' to see current configuration and setup instructions.` and end the turn **without contacting the network**.
- **FR-13.51** A blank line and then the literal `Thinking...` MUST be printed before every provider call. (realizes US-13.1)
- **FR-13.52** When probability analysis is enabled, `Log probabilities enabled - requesting with top-k={K}` MUST additionally be printed before the call, using the loaded top-K value verbatim including out-of-range values. (realizes US-13.6)
- **FR-13.53** On the probability path, a missing reply text MUST be stored in the transcript as the literal `Error: Response text expected, none recieved.` (misspelled, trailing period) while the *displayed* reply is empty. On the plain path each adapter supplies its own placeholder, which is both stored and displayed: `Error: Response text expected, none given` (cloud, no period), `Error: Response text expected, none given.` (managed model, with period), `Error: Response text expected, none received` (local, correctly spelled, no period). All four are stored as assistant messages and therefore re-sent as context on later turns.
- **FR-13.54** Per-token probabilities MUST be attached to the assistant message only on the probability path; the plain path MUST always store no probability data.
- **FR-13.55** The reply text MUST always be printed with exactly one blank line before it and one after. (realizes US-13.1)
- **FR-13.56** When probabilities were requested but the returned list is empty or absent, the session MUST print a blank line, then `Note: Log probabilities were requested but none were returned by the model.` and `This could be due to the model not supporting this feature or an API limitation.`, and MUST draw no table, grid or rule.
- **FR-13.57** A fault from the provider call MUST print `Error getting AI response: {fault message}` and end the turn, leaving the orphan user message in the transcript; nothing MUST be rolled back. (realizes US-13.8)
- **FR-13.58** The session MUST NOT read the reply envelope's error-message field or its total-elapsed-time field. (See QUIRK-13.18.)
- **FR-13.59** The turn MUST be synchronous from the user's point of view: no streaming, no spinner, no elapsed-time display, no timeout and no cancellation. (realizes US-13.1)

*Token probability rendering*

- **FR-13.60** Rendering MUST open with a blank line and a left-justified horizontal rule titled `Token Probabilities Analysis` in yellow, and close with a blank line and a second, untitled left-justified rule. This is the only place in the session where rich rendering (rules, tables, grids, panels, colour) is used; everything else is plain text. (realizes US-13.6)
- **FR-13.61** Two orthogonal display switches MUST govern selection: show-all versus sample (default sample), and grid versus list layout (default list).
- **FR-13.62** With show-all on, the entire token list MUST be rendered. With show-all off and the list at **15 or fewer** entries, the entire list MUST still be rendered. With show-all off and more than 15 entries, three sample blocks MUST be rendered. The value 15 is exactly `5 × 3` and exists so that sampling never shows more than the whole list would. (realizes US-13.7)
- **FR-13.63** The three sample blocks MUST appear in this fixed, non-configurable order, each preceded by its own blue caption and separated by a blank line: `Beginning Tokens:` (the first 5, numbered from 1); `Middle Tokens:` (5 tokens starting at whole-number index `count / 2 − 2`, numbered from that index plus one); `End Tokens:` (the last 5, starting at index `count − 5`, numbered from `count − 5 + 1`). The sample size 5 is a tunable-looking constant but is not exposed to the user.
- **FR-13.64** Displayed token indices MUST be 1-based and **absolute within the whole reply**, preserved across sample blocks, so a middle-sample entry can read `#37`.
- **FR-13.65** List layout MUST be a rounded-border table that expands to the terminal width with four columns: `№` (U+2116, centred), `Token` (fixed width 20), `Probability` (centred), `Top Alternatives` (fixed width 50). The index cell MUST be grey.
- **FR-13.66** In list layout the alternatives cell MUST show **at most 3** alternatives regardless of the configured top-K or the grid alternative cap, one per line as `token (probability)`, or the dimmed literal `(none)` when the token has none. This cap is not user-tunable.
- **FR-13.67** Grid layout MUST use `max(1, terminal width / 40)` columns — one card per 40 characters of terminal width, at least one column always — laying cards left-to-right and padding the final row with empty cells so every row has the full column count. The constant 40 is a legibility heuristic, not a business rule.
- **FR-13.68** Each grid card MUST be a rounded, expanded panel headed by the grey 1-based absolute index prefixed with `#`, with body lines `Token: {token}`, `Prob: {coloured probability}` and — omitted entirely when the token has no alternatives — an `Alternatives:` block listing up to the grid alternative cap (default `5`, valid range 1–20) as `- {alt} ({coloured probability})`, followed by a dimmed `+ N more` line only when more alternatives exist than were shown.
- **FR-13.69** Alternatives MUST be rendered one level deep only; an alternative's own alternatives MUST never be rendered.
- **FR-13.70** Token text rendering MUST render an absent token as the dimmed literal `(null)`; otherwise it MUST double `[` and `]` to escape console markup **first**, then replace line feed, carriage return, tab and NUL with the dimmed visible escapes `\n`, `\r`, `\t`, `\0`.
- **FR-13.71** Probability colouring MUST use the bands `>= 90` green, `>= 70` lime, `>= 50` yellow, `>= 30` orange, otherwise red, evaluated against the **un-multiplied** 0…1 probability value. (See QUIRK-13.4.)
- **FR-13.72** The probability value MUST be formatted as a percentage to 5 decimal places, with a **second, literal percent sign concatenated after** the format's own. (See QUIRK-13.5.)
- **FR-13.73** Colour MUST be the only channel conveying confidence banding; no textual band label is emitted. This is an accessibility gap recorded as a requirement because it is observable behaviour, not an intent.

*Shutdown*

- **FR-13.74** Leaving the loop MUST print `Goodbye!` before disposal. (realizes US-13.9)
- **FR-13.75** Disposal MUST release all three provider adapters in registration order, guarded by a disposed flag against double disposal, with a finalizer as a backstop.
- **FR-13.76** Disposing the local-model adapter MUST release its model and context handles and flush its captured native diagnostic buffer, appending to `<roaming app data>/ChatDbg/Logs/llamasharp_{yyyyMMdd}.log`, where the date component is the **local** date even though every timestamp the product stores is coordinated universal time. (See QUIRK-13.8.)
- **FR-13.77** Nothing else MUST be persisted at shutdown; the transcript is discarded unless the user explicitly exported it. (realizes US-13.9)
- **FR-13.78** Normal termination MUST return process status `0`.

*Storage and compatibility*

- **FR-13.79** The persisted settings document MUST be a JSON object using exactly these key names, so that an existing file keeps working: `provider`, `modelId`, `temperature`, `maxTokens`, `azureEndpoint`, `awsRegion`, `systemPromptName`, `enableLogProbabilities`, `logProbabilitiesTopK`, `showAllTokens`, `gridViewForTokens`, `gridViewMaxAlternatives`, `useWindowsCredentialManager`, `llamaContextSize`, `llamaGpuLayerCount`, `llamaGpuDevice`, `llamaThreads`, `llamaBatchSize`, `azureApiKey`, `awsAccessKey`, `awsSecretKey`. Documents are written indented.
- **FR-13.80** The system-prompt content and the three resolved credential values MUST be computed and MUST NOT be written to the settings document.
- **FR-13.81** A stored transcript document MUST use `messages`, `sessionId`, `createdAt`, and per message `role`, `content`, `timestamp`, `isCommand`, `logProbabilities`, with each probability record using `token`, `logprob` and `top_alternatives` — the last key being a snake-case outlier that must be reproduced for exported transcripts to remain re-importable. A stored prompt document MUST use `name`, `content`, `description`, `createdAt`, `lastUsedAt`.
- **FR-13.82** Special-folder resolution MUST follow the host platform: user profile = the Windows user directory / `$HOME`; local application data = `%LOCALAPPDATA%` / `$XDG_DATA_HOME` or `$HOME/.local/share`; roaming application data = `%APPDATA%` / `$XDG_CONFIG_HOME` or `$HOME/.config`.
- **FR-13.83** Settings and the system prompt MUST be read once at startup and held in memory; a settings file edited externally mid-session MUST be ignored. There is no caching layer and no reload.

---

**External technology**

*Requires: a managed application runtime providing line-oriented terminal input and output, file input/output, environment-variable access, a coordinated-universal-time clock and random unique-identifier generation (no protocol). Source used: .NET 10 (`net10.0`) with the SDK pinned by `global.json` to `10.0.100-rc.1.25451.107`. Reimplementer notes: only the console, file, environment and clock primitives are load-bearing here. Two size-optimised publish profiles exist that enable invariant globalization and trimming; both change observable behaviour (instruction dispatch and percentage spacing), so the chosen build configuration is part of the specification, not an implementation detail.*

*Requires: rich terminal rendering — horizontal rules with optional left-justified titles, auto-expanding bordered tables with per-column fixed widths and centring, grids of bordered and headed panels, and inline colour/dim markup (ANSI escape sequences). Source used: Spectre.Console 0.51.1, invoked directly by the session and used **only** for the token-probability visualization. Reimplementer notes: the required palette names are green, lime, yellow, orange3, red, blue, grey and dim; the escaping convention is bracket doubling; borders are the rounded style; tables expand to terminal width. Everything else the session prints is plain text.*

*Requires: a terminal-width query (no protocol). Source used: the runtime's console window-width property. Reimplementer notes: used once, to compute grid columns as `max(1, width / 40)`. Measured on Linux with output redirected it returns 80 and does not fail; on a Windows host with no console attached the equivalent query raises and the visualization is lost to the loop's blanket guard. The source guards only against a zero width, never against failure.*

*Requires: line-oriented standard input (no protocol). Source used: the runtime's console read-line. Reimplementer notes: measured, it returns nothing at end of stream, which the blank-input guard reports as "blank", producing an unbounded busy loop. A reimplementer should treat end of stream as an exit condition and flag that as a deliberate deviation.*

*Requires: a Unicode-capable terminal font and code page (Unicode). Source used: none configured. Reimplementer notes: the banner frame (U+2554/2550/2557/2551/255A/255D), the status markers `✓` U+2713 and `✗` U+2717, and the table's numero header `№` U+2116 all fall outside ASCII and Latin-1. Because the encoding is never set, a legacy-code-page console shows replacement characters.*

*Requires: cloud language-model chat completion with optional per-token log probabilities (HTTPS/REST plus JSON, via a vendor client). Source used: Azure.AI.OpenAI 2.1.0 over the runtime HTTP client. Reimplementer notes: this session needs only "reply text" plus "ordered per-token probabilities with alternatives" from the abstraction; the adapter owns request shaping and system-prompt injection.*

*Requires: managed-service language-model chat completion (HTTPS with request signing, plus JSON). Source used: AWSSDK.BedrockRuntime 4.0.7.3. Reimplementer notes: same abstraction; default region `us-east-1`. The provider is considered configured on the presence of an access key, so a session with only a secret key still reports "configured".*

*Requires: local language-model inference from an on-disk model file with token-level introspection (native library binding). Source used: LLamaSharp 0.25.0 with CPU and CUDA-12 backends, bundling native runtimes for Windows x64, Linux x64 (AVX/AVX2/AVX512/CUDA12/no-AVX variants), Linux musl x64, Linux ARM64, macOS x64 and macOS ARM64. Reimplementer notes: this feature only constructs and disposes it. Construction installs a native diagnostic sink; disposal appends to a daily log file — both happen even in a session that never selects this provider.*

*Requires: an optional operating-system secret vault (a platform-native credential API). Source used: direct native interop with the Windows credential API using generic credential entries. Reimplementer notes: availability is a plain platform check; the session hides all vault guidance when unavailable. Target names are `ChatDbg:AzureApiKey`, `ChatDbg:AwsAccessKey`, `ChatDbg:AwsSecretKey`. Treat this as a pluggable secret source with a working "unavailable" branch — it is a genuine capability gap off-platform, not a cosmetic one.*

*Requires: structured document persistence for settings, prompts and transcripts (JSON). Source used: the runtime's JSON serializer with indented output, explicit per-property names and computed/secret members excluded. Reimplementer notes: this feature reaches it only through the stores, but the key names in FR-13.79 and FR-13.81 are a compatibility contract for existing user files and exported transcripts.*

*Requires: locale-aware text comparison and number formatting (Unicode collation). Source used: the runtime's default culture-sensitive string prefix comparison and ambient-culture percentage format. Reimplementer notes: load-bearing in exactly two places — the `/` prefix test (QUIRK-13.12) and the percentage rendering, which produces `25.00000%%` under a US-English locale and `25.00000 %%` (note the space) under invariant globalization. A reimplementer should use an ordinal prefix test and pin the number format, and record both as deliberate deviations.*

---

**Acceptance criteria**

- **AC-13.1** — **Given** a machine with no `.ChatDbg` directories, **when** the application starts, **then** a settings file is created at `<user profile>/.ChatDbg/settings.json` containing the defaults, a `system_prompts` directory is created under local application data holding exactly four files named `default.json`, `code-reviewer.json`, `algorithm-helper.json`, `security-expert.json`, `default.json` is immediately rewritten with a `lastUsedAt` timestamp, and the banner reports `Provider: azure`, `Model: gpt-4`, `System Prompt: default` and the absolute settings path.
- **AC-13.2** — **Given** the prompt-store directory cannot be created (its parent is read-only), **when** the application starts, **then** the only output is `Fatal error: Error saving system prompt: {message}`, no banner and no prompt appear, and the process exits with status `1`.
- **AC-13.3** — **Given** the session is at the prompt, **when** the user presses Enter on an empty line, then on a line of three spaces, **then** nothing is printed either time, the transcript remains empty, and `ChatDbg (azure/gpt-4)> ` is written again.
- **AC-13.4** — **Given** the session is at the prompt, **when** the user types `/bogus`, **then** exactly `✗ Unknown command: /bogus. Type '/help' for available commands.` is printed and the session continues; **and when** the user types `/` alone, **then** exactly `✗ Invalid command` is printed.
- **AC-13.5** — **Given** the session is at the prompt, **when** the user types `/HELP`, **then** the general help is shown, because the name is lower-cased before lookup.
- **AC-13.6** — **Given** the session is at the prompt, **when** the user types `/model` followed by **two** spaces and `gpt-4o`, **then** the command receives the single argument `gpt-4o`; **and when** the user types `/set modelId C:\my  models\a.gguf` with two spaces inside the path, **then** the stored model identifier is `C:\my models\a.gguf` with one space.
- **AC-13.7** — **Given** the session is at the prompt, **when** the user types `/exit` (and separately `/quit`), **then** no marker line is printed, `Goodbye!` is printed, all three provider adapters are disposed, and the process exits with status `0`.
- **AC-13.8** — **Given** the provider is `azure` with no endpoint and no key from any source, **when** the user sends the line `why is my loop off by one?`, **then** the transcript gains a `user` message whose content is exactly that text, `Error: Azure OpenAI service is not configured. Use environment variables or Windows Credential Manager to configure credentials securely.` and `   Type '/set' to see current configuration and setup instructions.` are printed, no network call is made, and no assistant message is added.
- **AC-13.9** — **Given** a fully configured provider and probability analysis disabled, **when** the user sends `hello world`, **then** a blank line and `Thinking...` are printed, the adapter receives the whole transcript, the reply is appended as an `assistant` message with no probability data, and the reply is printed with exactly one blank line before and one after.
- **AC-13.10** — **Given** a configured provider with probability analysis enabled and top-K `5`, **when** the user sends a message, **then** `Log probabilities enabled - requesting with top-k=5` is printed before the call and the stored assistant message carries the returned per-token list.
- **AC-13.11** — **Given** probability analysis is enabled and the provider returns text with an empty probability list, **when** the reply is rendered, **then** exactly `Note: Log probabilities were requested but none were returned by the model.` and `This could be due to the model not supporting this feature or an API limitation.` are printed and no rule, table or grid is drawn.
- **AC-13.12** — **Given** sample mode and list layout (both defaults) and a reply of exactly **16** tokens, **when** it is rendered, **then** three captioned blocks appear in the order `Beginning Tokens:` (numbered 1–5), `Middle Tokens:` (numbered 7–11, from start index `16 / 2 − 2 = 6`), `End Tokens:` (numbered 12–16), each drawn as a rounded four-column table.
- **AC-13.13** — **Given** the same settings and a reply of exactly **15** tokens, **when** it is rendered, **then** one table containing all 15 tokens is drawn with no `Beginning`/`Middle`/`End` captions.
- **AC-13.14** — **Given** grid layout on a terminal exactly **120** columns wide and a 16-token reply, **when** it is rendered, **then** cards appear **3** per row, the final row is padded to 3 cells, and each card header shows the token's absolute 1-based index.
- **AC-13.15** — **Given** grid layout on a terminal exactly **80** columns wide and a 16-token reply in sample mode, **when** it is rendered, **then** cards appear **2** per row, the `Middle Tokens:` block's first card header reads `#7`, and the `End Tokens:` block's last card header reads `#16`.
- **AC-13.16** — **Given** list layout and a returned top-K of **20**, **when** a row is rendered, **then** its `Top Alternatives` cell shows **at most 3** alternatives; **and given** grid layout with a grid alternative cap of **5** and 9 returned alternatives, **then** the card lists 5 alternatives followed by a dimmed `+ 4 more`.
- **AC-13.17** — **Given** a token whose text is `a[b]c` followed by a tab and a line feed, **when** it is rendered, **then** no console markup is interpreted from the token and the tab and line feed appear as dimmed `\t` and `\n`; **and given** a token whose text is absent, **then** the cell reads the dimmed `(null)`.
- **AC-13.18** — **Given** probability analysis is enabled and the provider returns one token whose log-probability is the natural logarithm of `0.25`, **when** the row is rendered, **then** the probability cell reads `25.00000%%` under a US-English locale (or `25.00000 %%` under invariant globalization) **and** the value is coloured **red**, because the colour band is chosen from `0.25` rather than `25`.
- **AC-13.19** — **Given** the user runs `/logprobs showall` and `/logprobs grid` and then `/exit`, **when** the settings file is inspected, **then** it contains `"showAllTokens": true` and `"gridViewForTokens": true`; **and when** the application is restarted and a 16-token reply is rendered, **then** it uses **sample mode and list layout** again. (A reimplementation that restores these must flag it as an intentional deviation — see QUIRK-13.1.)
- **AC-13.20** — **Given** a configured provider that raises during a turn, **when** the fault surfaces, **then** `Error getting AI response: {message}` is printed, the user's message remains in the transcript with no matching assistant message, and the prompt is shown again.
- **AC-13.21** — **Given** the session is at the prompt, **when** the user runs `/set provider bedrock` and then `/model anthropic.claude-3-sonnet-20240229-v1:0`, **then** the next prompt reads exactly `ChatDbg (bedrock/anthropic.claude-3-sonnet-20240229-v1:0)> `.
- **AC-13.22** — **Given** the provider is `llama` with context size `4096`, GPU layer count `0`, no GPU device, thread count `0` and batch size `512`, **when** the application starts, **then** the banner includes `LLama Configuration:`, `   Context Size: 4096`, `   GPU Layers: 0 (CPU-only)`, **no** GPU device line, `   Threads: default`, `   Batch Size: 512`.
- **AC-13.23** — **Given** a host without a machine secret vault and an unconfigured cloud provider, **when** the application starts, **then** the advisory shows only the environment-variable method and no vault instructions; **and when** the user runs `/set enablewincred`, **then** `Windows Credential Manager is not available on this platform.` is printed and the flag is unchanged.
- **AC-13.24** — **Given** a settings file hand-edited to `"logProbabilitiesTopK": 9999` and `"temperature": 50`, **when** the application starts and the user sends a message with probability analysis enabled, **then** no complaint is printed and `Log probabilities enabled - requesting with top-k=9999` appears.
- **AC-13.25** — **Given** a settings file hand-edited to `"provider": "Azure"` with a capital A, **when** the application starts, **then** the banner prints `Provider: Azure`, `Warning: Unknown AI provider: Azure` is printed with **no** blank line after it, and every subsequent conversation turn prints `Error: Unknown AI provider: Azure` while still appending the user's text to the transcript.
- **AC-13.26** — **Given** the user runs `/set temperature 3`, **then** exactly `✗ Temperature must be a number between 0 and 2` is printed and the stored value is unchanged; **given** `/logprobs top 21`, **then** exactly `✗ Top-K value must be a number between 1 and 20`; **given** `/set maxTokens 0`, **then** exactly `✗ MaxTokens must be a number between 1 and 8192`.
- **AC-13.27** — **Given** a settings file whose `systemPromptName` is `Default` with a capital D and a prompt store containing `default.json`, **when** the application starts on a case-insensitive file system, **then** the stored prompt is loaded; **when** it starts on a case-sensitive file system, **then** the built-in fallback prompt is used and **nothing is printed about it**. A reimplementation must pick one behaviour explicitly.
- **AC-13.28** — **Given** a configured provider, probability analysis disabled, and an adapter that returns an envelope with no reply text, **when** the turn completes, **then** the transcript stores that adapter's own placeholder (`Error: Response text expected, none given` for the cloud provider, `Error: Response text expected, none given.` for the managed-model provider, `Error: Response text expected, none received` for the local provider) and the screen shows the same text between blank lines; **when** probability analysis is enabled instead, **then** the transcript stores `Error: Response text expected, none recieved.` while the screen shows an empty reply.
- **AC-13.29** — **Given** the application's standard input is a closed stream or an exhausted pipe, **when** the loop reaches its read, **then** the prompt is written again immediately and repeatedly with no output and no exit, and the process must be killed. (A reimplementation that exits on end of stream must flag that as a deliberate deviation.)
- **AC-13.30** — **Given** the local-model provider was never selected in a session, **when** the user exits, **then** the local-model adapter is still disposed and a file named `llamasharp_{yyyyMMdd}.log` (local date) is created or appended under `<roaming app data>/ChatDbg/Logs/`.
- **AC-13.31** — **Given** a settings file that is present but malformed, **when** the application starts, **then** `Error loading settings: {message}` and `Using default settings.` are printed (possibly preceded by the store's own identical first line), the banner then reports the built-in defaults, and the session reaches the prompt normally.
- **AC-13.32** — **Given** a settings file containing a non-empty `azureApiKey`, **when** the application starts, **then** a migration warning is printed that includes a literal `set CHATDBG_AZURE_API_KEY=<the actual secret>` line echoing the secret in cleartext, and the session continues.

---

**Quirks**

- **QUIRK-13.1**: The three display-mode settings (show-all-tokens, grid-layout, grid-maximum-alternatives) are persisted and settable at runtime but are never read back at startup, so every session begins in sample mode with list layout and a cap of 5 no matter what was saved. The product documentation implies they persist. *Evidence: `src/ChatDbg/ChatShell.cs:130-151` (omission) vs `Models/ChatSettings.cs:39-46`, `Commands/LogProbsCommand.cs:67-97`, `README.md:230-246`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.2**: Reading a line at end of stream returns nothing, which the blank-input guard treats as "keep going", so piping input into the program or closing standard input produces an infinite busy loop that re-prints the prompt forever. Only an exit instruction or killing the process ends it. *Evidence: `src/ChatDbg/ChatShell.cs:83-88` (measured).* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.3**: The user's turn is appended to the transcript before any validation, so failed and unconfigured turns accumulate orphan user messages that are then re-sent as context on the next successful turn. *Evidence: `src/ChatDbg/ChatShell.cs:346` vs `:349,356`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.4**: The probability is a 0…1 value, but the colour thresholds compare it against 90/70/50/30, so **every genuine token is coloured red**. The intended scale was clearly 0–100 — a unit test asserts a probability of `25` for a log-probability of the natural logarithm of `0.25`. The displayed number is nonetheless correct, because the percentage format multiplies by 100 independently of the colour test. *Evidence: `Models/TokenLogProbabilities.cs:26` vs `src/ChatDbg/ChatShell.cs:657-671`; failing test `Models/TokenLogProbabilityTests.cs:9-18`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.5**: The probability is formatted with a percentage specifier that already appends a percent sign, and a second literal percent sign is concatenated, producing `25.00000%%` (or `25.00000 %%` under invariant globalization). The product documentation's sample output shows a single sign. *Evidence: `src/ChatDbg/ChatShell.cs:671` vs `README.md:315,333` (measured).* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.6**: The demonstration instruction for token analysis is constructed here without a renderer, so in this session it displays **nothing at all** — it merely returns the message `Sample token probability analysis generated`. The documentation claims it demonstrates the visualization. *Evidence: `src/ChatDbg/ChatShell.cs:52`; `Commands/DemoLogProbsCommand.cs:34-38,62-65` vs `README.md:256-268`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.7**: All three provider adapters are constructed at startup and disposed at shutdown regardless of which is selected, so a pure-cloud session still allocates a native diagnostic capture buffer and, at exit, creates or appends a daily log file under roaming application data; and merely constructing the cloud adapter opens an outbound connection pool that may never be used. *Evidence: `src/ChatDbg/ChatShell.cs:29-34,704-708`; `Services/LLamaSharpService.cs:25,670-688`; `Services/AzureOpenAIService.cs:24-27`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.8**: The local-model diagnostic log file is named from the **local** date while every timestamp the product persists is coordinated universal time; a session spanning local midnight in a negative-offset zone writes to a file dated differently from the messages it contains. *Evidence: `Services/TokenInspection/LLamaSharpLogConfig.cs:149` vs `Models/ChatHistory.cs:12,21`, `Models/ChatMessage.cs:12`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.9**: The terminal output encoding is never configured, so the banner frame, the success/failure markers and the numero column header depend on the host terminal already being Unicode-capable and render as replacement characters on a legacy code page. *Evidence: absence of any encoding setup in `src/ChatDbg/Program.cs` and `src/ChatDbg/ChatShell.cs`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.10**: The provider key is matched **case-sensitively** both for adapter lookup and for the local-model banner test, while the setting instruction lower-cases its argument. A hand-edited settings file containing `"provider": "Azure"` therefore produces an unknown-provider warning at startup and an unknown-provider error on every turn, with the user's messages still piling up in the transcript. *Evidence: `src/ChatDbg/ChatShell.cs:211,251,349` vs `Commands/SetCommand.cs:46-52`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.11**: Every numeric range is enforced only at the setting-instruction boundary; the startup copy validates nothing, so a hand-edited file with a top-K of `9999`, a context size of `0`, a temperature of `50` or a maximum-token count of `-1` is loaded and used verbatim, and the out-of-range top-K is announced to the user before the call. *Evidence: `src/ChatDbg/ChatShell.cs:130-151` vs `Commands/SetCommand.cs:70-135`, `Commands/LogProbsCommand.cs:51-100`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.12**: The command test is a locale-aware prefix match rather than a byte comparison. A line beginning with an ignorable character (a zero-width joiner, say) followed by `/help` **is** classified as a control instruction, and the parser then strips that ignorable character instead of the slash, so the user sees `✗ Unknown command: //help. Type '/help' for available commands.` Under invariant globalization — which the size-optimised publish profiles enable — the same input is treated as a conversation turn instead, so **the same input dispatches differently depending on which build shipped**. *Evidence: `src/ChatDbg/ChatShell.cs:92,326,340`; `Xcaciv.ChatDbg.Shell.csproj:52,95` (measured).* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.13**: The startup advisory reports the managed-model credential source using the **access key's** source even when only the secret key is present, so a half-configured session can be told its credentials came from a source that supplied nothing. *Evidence: `src/ChatDbg/ChatShell.cs:310-313`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.14**: The active system prompt is located by turning its name into a file name and testing for that file, so name matching inherits the host file system's case rules. The same settings file produces different model behaviour on different operating systems, **with no diagnostic printed**. *Evidence: `Services/SystemPromptService.cs:76-84,209-214`; `src/ChatDbg/ChatShell.cs:166-180`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.15**: Of the three startup-advisory warning branches, only "no provider" and "not configured" print a trailing blank line; the "unknown provider" branch returns without one, so its warning butts directly against the first prompt. *Evidence: `src/ChatDbg/ChatShell.cs:246-247,301` vs `:253-254`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.16**: On a host without a machine secret vault the credential guidance dead-ends: the vault-write instruction refuses until the vault flag is enabled, and the enable instruction then refuses with `Windows Credential Manager is not available on this platform.` The user is sent to an instruction that cannot succeed. *Evidence: `Commands/SetCommand.cs:234-240` vs `:219-222`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.17**: Four registered instructions read further lines from the same standard input while the loop is blocked inside them. Under piped or scripted input these reads silently consume lines the author intended as conversation, and at end of stream they receive nothing and take the negative branch — after which the loop resumes and spins per QUIRK-13.2. *Evidence: `Services/SettingsService.cs:120-123,210-211,246-248`; `Commands/InspectCommand.cs:63-65`; `Commands/PromptCommand.cs:267`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.18**: The reply envelope carries an error message and a total elapsed time; this session reads neither. A provider that reports failure by returning an envelope instead of raising produces a blank reply between two blank lines, stores the misspelled placeholder in the transcript, and discards the real error text entirely. *Evidence: `Models/AIResponse.cs:25-32` vs `src/ChatDbg/ChatShell.cs:374-380,396-402`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.19**: The blank lines around `Thinking...` and around every reply are emitted as bare line-feed characters embedded in the text, while the surrounding writes use the platform line ending. On Windows the on-screen transcript therefore mixes two line-ending conventions, which corrupts naive redirection into tools that expect only one. *Evidence: `src/ChatDbg/ChatShell.cs:363,380,389,402`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.20**: The four "no reply text" placeholders are spelled four different ways across the paths that can produce them, one of them misspelled, and all four are stored in the transcript as assistant messages and re-sent as context. *Evidence: `src/ChatDbg/ChatShell.cs:377`, `Services/AzureOpenAIService.cs:50`, `Services/BedrockService.cs:33`, `Services/LLamaSharpService.cs:58`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.21**: The starter prompts are written from inside the prompt store's own construction, before its formatting options are assigned, so the four seed files are written unindented while every later write is indented; the session then immediately rewrites only the active prompt in the indented form, leaving three files permanently inconsistent with the rest. *Evidence: `Services/SystemPromptService.cs:32` vs `:34-37,106`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.22**: The migration and credential notices printed during settings load contain literal `?`/`??` placeholder glyphs where symbols were intended (source-file encoding damage), and the migration instructions print secret values in cleartext to the terminal. *Evidence: `Services/SettingsService.cs:62,69,73,110,116,128,335,341,346`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.23**: The confidence banding is conveyed by colour alone, with no textual label and no monochrome fallback, and the success/failure signal for every command result is carried solely by two Unicode glyphs. *Evidence: `src/ChatDbg/ChatShell.cs:657-671,103-104`.* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-13.24**: The user-facing remedy text is Windows-flavoured on every platform: the advisory always shows the Windows shell assignment verb `set CHATDBG_AZURE_API_KEY=…` (wrong for POSIX shells, which need `export`) and the local-model guidance always shows a Windows path `C:\path\to\your\model.gguf`. Nothing malfunctions; the guidance is simply wrong for most non-Windows users. *Evidence: `src/ChatDbg/ChatShell.cs:265,282-283,297`.* Keep-or-fix decision deferred to Open Questions.

---

**Source notes**

Dossier: `output/chatdbg/dossiers/chat-session-console.md`. Evidence commit `d8c18f61d6bb73666ed97cd4885e877e35558485` on branch `LLamaSharp_support`.

Primary evidence:
- `src/ChatDbg/Program.cs` (14 lines) — entry point, fatal-error handling, exit codes.
- `src/ChatDbg/ChatShell.cs` (719 lines) — construction (`:21-38`), command registration (`:40-67`), startup sequence (`:69-78`), the loop (`:80-120`), settings load (`:122-159`), system-prompt load (`:161-190`), banner (`:192-237`), credential advisory (`:239-321`), parse and dispatch (`:323-341`), chat turn (`:343-410`), probability rendering (`:412-690`), disposal (`:692-718`).
- `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj` — assembly identity (`ChatDbg Shell`, product `ChatDbg`, version 1.0.0, `:10-19`), package references (`:107-111`), the `Compact` and `SingleFile` publish profiles (`:23-97`).

Supporting evidence in the shared library: `Models/ChatSettings.cs` (defaults `:7-66`, credential resolution `:88-118`, source labels `:173-201`), `Models/ChatHistory.cs`, `Models/ChatMessage.cs`, `Models/AIResponse.cs`, `Models/CommandResult.cs`, `Models/TokenLogProbabilities.cs`, `Models/WindowsCredentialManager.cs`, `Services/SettingsService.cs`, `Services/SystemPromptService.cs`, `Services/AzureOpenAIService.cs`, `Services/BedrockService.cs`, `Services/LLamaSharpService.cs`, `Services/TokenInspection/LLamaSharpLogConfig.cs`, and `Commands/*.cs` for the registered instructions' names, descriptions, usage strings and validation messages.

Test evidence (statements of intent, not passing evidence — the committed suite is red): `Commands/ExitAndQuitCommandTests.cs`, `Models/CommandResultTests.cs`, `Commands/HelpCommandTests.cs`, `Models/ChatHistoryTests.cs`, `Models/ChatMessageTests.cs`, `Models/AIResponseTests.cs`, `Models/ChatSettingsTests.cs`, `Models/WindowsCredentialManagerTests.cs`, `Models/TokenLogProbabilityTests.cs`, `Services/SettingsServiceTests.cs`, `Services/SystemPromptServiceTests.cs`, `Commands/LogProbsCommandTests.cs`, `Commands/DemoLogProbsCommandTests.cs`.

Documentation checked and overruled where it disagreed with code: `README.md` (interface description, display-option persistence, demonstration instruction, prompt format, sample percentage output) and `src/ChatDbg/prd.md` (runtime version, provider count, project layout, dependency list). Code is truth throughout.
