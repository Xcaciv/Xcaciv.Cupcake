### 7.10 Token Inspection, Tokenization & Attribution

**Description**

A language model does not read characters — it reads *tokens*, the vocabulary fragments its tokenizer chops text into. Two prompts that look identical to a person can tokenize very differently, cost very differently, and behave very differently. Token Inspection, Tokenization & Attribution is the part of the product that lets a person running a **local** model on their own machine look inside that process. It answers three questions: *how does this model chop my text up?* (a token-by-token report of vocabulary IDs and an approximate character span for each token); *what else could the model have said at each step?* (each generated token shown next to a ranked set of runners-up with probabilities); and *why did it say that?* (an attribution view mapping each generated token back to the span of input, or of earlier output, deemed to have influenced it, with a numeric influence score).

The feature ships two working commands — a tokenization report and a full inspection that runs tokenization, then a probability analysis, then an attribution analysis behind an interactive yes/no gate — plus three commands that were designed to view and export the analysis of the last chat generation but that exist only as stubs and are not wired into either shell. It also owns the **per-step analysis record**: the structured, exportable object the local-inference subsystem fills in for every token it generates during ordinary chat, holding the token text, the vocabulary index, the probability and log-probability, a candidate list, a free-text debug note, and a snapshot of model/context state at that step. That record is the only entity in this feature designed to be persisted, and its exported form is a compatibility surface.

There is exactly **one human role** — the interactive shell user (a developer, prompt engineer, or educator) typing slash-commands at a prompt. There is no authentication, no authorization, no multi-tenancy, and no server component anywhere in this feature. The only gate is "is the configured provider the local one, and does the configured model file exist". Two machine actors participate: the local inference subsystem, which supplies the tokenizer, the generator, and the per-step records; and the adjacent probability-visualization subsystem, which consumes the same records for its own richer rendering. A reimplementer must know up front that a great deal of what this feature *displays* is **fabricated rather than measured** — the token text is never decoded, the probabilities and alternatives on the inspection path are arithmetic functions of a word index, and the attribution score is a constant. These are recorded below as requirements and as quirks, not silently corrected.

---

**User stories**

- **US-10.1** — As the interactive shell user, I want to type a piece of text and see how the currently configured local model tokenizes it, so that I can understand why a prompt costs what it costs and how the model actually segments my wording.
- **US-10.2** — As the interactive shell user, I want the tokenization report to show each token's position in the sequence and its vocabulary index, so that I can correlate tokens with model behaviour.
- **US-10.3** — As the interactive shell user, I want a single command that runs tokenization *and* probability analysis *and* attribution analysis over one piece of text, so that I get a complete introspection report without chaining several commands.
- **US-10.4** — As the interactive shell user, I want to be asked before the expensive generation phase starts, so that I can get the cheap tokenization report and stop there without waiting for the model to load a second time and generate.
- **US-10.5** — As the interactive shell user, I want to see, for each generated token, its log-probability, the derived percentage, and a ranked list of alternatives, so that I can spot low-confidence or surprising choices.
- **US-10.6** — As the interactive shell user, I want to see which span of my input (or of the model's own earlier output) is claimed to have influenced each generated token, with a score, so that I have a starting point for explaining an answer.
- **US-10.7** — As the interactive shell user, I want clear, actionable refusals when the feature cannot run — wrong provider, missing model file, missing text — so that I know exactly which setting to change.
- **US-10.8** — As the interactive shell user, I want token inspection to be discoverable in the general help listing and to have per-command help, so that I can find it without reading documentation.
- **US-10.9** — As the local inference subsystem, I want a single structured per-step analysis record to write into for every token I generate, so that the shell, the probability visualizer, and any exporter all read the same shape.
- **US-10.10** — As the interactive shell user, I want the analysis of the last generation to be reviewable in a table and exportable to a file, so that I can archive or share the evidence. *(This story is **designed but not delivered** — see FR-10.44 through FR-10.49 and QUIRK-10.1/QUIRK-10.2. It is recorded because the product's own documentation and shipped stub commands promise it.)*

---

**Use cases**

#### UC-10.1 — Produce a tokenization report (realizes US-10.1, US-10.2, US-10.7)

**Preconditions**
- The application is running and the shell is at its input prompt.
- The settings record has been loaded; the shell holds a single shared, mutable settings object that commands read at execution time.

**Main flow**
1. The user types the tokenization command followed by one or more words of text.
2. The shell recognises the leading command marker, strips it, splits the remainder on spaces discarding empty entries, lower-cases the first element as the command name, and passes the rest as the argument list.
3. The command verifies, in order: at least one argument is present; the configured provider is exactly the local-model provider name; the configured model identifier is non-empty and names an existing file.
4. The command joins the argument words with a single space to form the text to tokenize.
5. The command announces that it is loading the model.
6. The command loads the model weights on a background thread with a context window of **512**, then creates an inference context on a background thread.
7. The command announces that it is tokenizing.
8. The command tokenizes the text **with a beginning-of-sequence marker prepended**.
9. The command writes to standard output: the echoed input text, the total token count, a blank line, a section title, a dashed rule, a fixed-width left-aligned column header, a second dashed rule, and then one row per token carrying the zero-based index, the vocabulary ID, and a placeholder token text.
10. The command disposes the context and the model.
11. The command returns a success result whose message is the completion string; the plain console shell prints a success mark followed by that message.

**Alternate flows**
- **A1 — Runs of whitespace in the typed line.** Multiple spaces and tabs are collapsed to a single space before tokenization, and the echoed input line shows the *collapsed* text, not what the user typed (QUIRK-10.31).
- **A2 — Very long input.** Every token produces one output row; there is no cap, no paging and no truncation (QUIRK-10.30).
- **A3 — Command name casing.** The typed command name is lower-cased before lookup, so any casing of the command word resolves.

**Error flows**
- **E1 — No text supplied.** Failure result with the message `Please provide text to tokenize. Usage: ` followed by the command's usage string. No model is opened.
- **E2 — Provider is not the local-model provider.** Failure result with the exact message `The tokenize command only works with the LLamaSharp provider. Use '/set provider llama' first.` The comparison is exact and case-sensitive; a provider value differing only in case still fails.
- **E3 — Model identifier empty or file missing.** Failure result with the exact message `LLamaSharp model not configured or file not found. Use '/set modelId <path-to-gguf-model>' first.`
- **E4 — Weights fail to load, context creation fails, or the tokenizer throws.** The failure is caught; the full failure detail is written to the diagnostic trace channel as `Error in TokenizeCommand: {exception}`; the user sees a failure result with `Error tokenizing text: {message}`.
- **E5 — Model file exists but is not a model.** The existence check passes (no extension, size, magic-byte or readability check is performed) and the failure surfaces later through E4 as a single line of native error text with no further diagnosis.
- **E6 — Run under the full-screen shell.** The command can never pass E2 in that shell because it holds a discarded settings object (QUIRK-10.6); were it to pass, its report would be written past the full-screen UI's own screen management (QUIRK-10.7, INFERRED).

**Postconditions**
- No file is written, no state is retained, and the model is fully unloaded. The report exists only in the terminal scrollback.

---

#### UC-10.2 — Full inspection with a consent gate (realizes US-10.3, US-10.4, US-10.5, US-10.6, US-10.7)

**Preconditions**
- Same as UC-10.1.
- Standard input is an interactive terminal capable of supplying a line of text.

**Main flow**
1. The user types the inspection command followed by one or more words of text.
2. The command performs the same three ordered precondition checks as UC-10.1 and joins the argument words with a single space.
3. The command announces the text being inspected and warns that loading the model may take a moment.
4. **Stage B — tokenization.** The command invokes the *analyze prompt* operation with visualization requested. That operation loads the model with a context window of **512**, creates a context, tokenizes with a beginning-of-sequence marker, builds one token record per token, estimates each token's character span, and builds a fixed five-line visualization block.
5. The command renders a tokenization section: a section banner, the total token count, the visualization block, a details title and rule, a fixed-width right-aligned header with a fourth character-range column, a rule, and one row per token.
6. **Stage C — consent gate.** The command prints a blank line and a yes/no question, then **blocks reading one line from standard input**. The reply is lower-cased; only `y` or `yes` proceeds.
7. **Stage D — probability analysis.** The command announces the probability map, computes the token budget as the smaller of **10** and the configured maximum response length, and invokes the *generate probability map* operation with that budget and with the alternatives-per-position count taken from the configured top-K setting. That operation loads the model a **second** time with a context window of **2048**, creates an interactive generator with an **empty** stop-sequence list, streams the generation into a buffer, splits the assembled text into words, and produces one generated-token record, one step-probability record and exactly top-K alternatives per word — all with **synthetic** IDs and log-probabilities.
8. The command renders a probability section: a banner, the generated continuation, a per-token entry giving the selected token text and ID, its log-probability to 5 decimal places with the derived percentage to 2 decimal places, and an indented list of alternatives.
9. **Stage E — attribution analysis.** The command renders an attribution section: a banner, an explanatory line, and one entry per generated token naming the influencing excerpt and a score to 2 decimal places.
10. The command returns a success result with the completion string.

**Alternate flows**
- **A1 — User declines at the gate.** Any reply other than `y`/`yes` — including an empty line — skips stages D and E entirely. Nothing further is printed. The command **still** returns the same success result (QUIRK-10.29).
- **A2 — Top-K setting is zero or negative.** Each probability entry prints its selected token and **no** alternatives heading at all; nothing errors.
- **A3 — Configured maximum response length below 10.** The generation budget is that smaller number.
- **A4 — Generation produces no words.** Both section banners print with no entries; the generated continuation line is empty; the command returns success.
- **A5 — Influencing excerpt longer than 40 characters.** It is cut to its first 37 characters and suffixed with an ellipsis of three dots, *before* escaping is applied.

**Error flows**
- **E1/E2/E3 — Precondition failures.** Same three checks and same ordering as UC-10.1, with the inspection-worded strings: `Please provide text to inspect. Usage: ` + usage; `The inspect command only works with the LLamaSharp provider. Use '/set provider llama' first.`; `LLamaSharp model not configured or file not found. Use '/set modelId <path-to-gguf-model>' first.`
- **E4 — The analyze operation is invoked with an empty or non-existent model path.** A *file-not-found* condition is raised carrying the message `Model file not found` with the offending path attached. This guard sits **ahead** of the protected block, so it is **not** re-wrapped.
- **E5 — Any other failure inside the analyze operation.** Re-raised as an *invalid-operation* condition with the message `Error analyzing prompt: {inner message}`, inner cause preserved; the full detail goes to the diagnostic trace as `Error in TokenInspectionService.AnalyzePrompt: {exception}`.
- **E6 — Any failure inside the probability-map operation.** Re-raised as an *invalid-operation* condition with the message `Error generating probability map: {inner message}`; trace line `Error in TokenInspectionService.GenerateProbabilityMap: {exception}`.
- **E7 — Any failure surfaced through the command.** Caught; trace line `Error in InspectCommand: {exception}`; failure result with `Error during token inspection: {message}` — typically double-prefixed, e.g. `Error during token inspection: Error analyzing prompt: {root cause}`.
- **E8 — Standard input is redirected or piped.** The gate reads end-of-file, which is treated as a decline; the command returns the same success message a completed analysis would (QUIRK-10.29). There is no timeout, no default, no flag and no cancellation.
- **E9 — Run under the full-screen shell.** The gate has no console to read from and the report is written past the UI (QUIRK-10.7, INFERRED).

**Postconditions**
- The model has been loaded and fully unloaded **twice**. No file is written and no state is retained.

---

#### UC-10.3 — Capture per-step analysis during an ordinary chat turn (realizes US-10.9)

**Preconditions**
- The local-model provider is active and per-token probability capture is enabled.

**Main flow**
1. A chat turn begins. The inference subsystem **clears** the analysis list before emitting the first token.
2. For each emitted token, the subsystem appends one per-step analysis record whose step number is the emitted-token count minus one; whose token text is the emitted piece; whose vocabulary index is **always −1**; whose probability is a **temperature-derived estimate**; whose log-probability is the natural log of that estimate; whose prompt offset equals the same step number; whose debug text is `Generated via sampling pipeline at step {step}, temperature={temperature to 2 decimal places}.`; and to which a model-state snapshot is attached.
3. The subsystem computes a real candidate set from the current logits — top-K taken from the configured alternatives setting, clamped to at least 1 and at most the logit-vector length, softmaxed against the running maximum — and attaches it to the record just built, filling **only** the candidate text and log-probability.
4. The list is retrievable through a programmatic call that returns a defensive copy, and writable to a file as an indented array of records.

**Alternate flows**
- **A1 — Candidate computation throws.** The failure is swallowed to a warning log line `Failed to compute candidates from logits: {message}` and that step keeps an empty candidate list.
- **A2 — A new generation begins.** The list is emptied first; only the most recent generation is ever retrievable.

**Error flows**
- **E1 — The file write fails.** The failure is swallowed into a logged error `Failed to save token analyses: {exception}`; the caller receives no signal at all.

**Postconditions**
- The in-memory list holds exactly one record per token of the most recent generation.

---

#### UC-10.4 — Review or export the last generation's analysis (realizes US-10.10 — **designed, not delivered**)

**Preconditions**
- None enforced. The commands read no data and check no state.

**Main flow (as implemented)**
1. The user invokes the view command with optional flags for how many candidates to show, whether to show model state, and a step range.
2. The command parses the flags into local values and returns a **success** result whose message is a fixed informational note echoing the parsed flag values back.
3. The user invokes the export command with a file path.
4. The command returns a **success** result whose message is a fixed informational note. **The path argument is never read, validated, or used, and no file is created.**

**Alternate flows**
- **A1 — Flag parsing edge cases.** A count flag whose value is missing or non-numeric leaves the default of **3** in place and does *not* consume the value, which is then examined as if it were a flag and ignored. A range flag is honoured only when **two** further arguments exist; each is parsed independently, so a bad first value and a good second value yield the default start with the given end. Unknown flags and stray words are silently ignored. Repeated flags: last one wins.
- **A2 — Neither command is reachable.** Neither shell registers these commands, so in a shipped build the user gets an unknown-command response instead (QUIRK-10.1).

**Error flows**
- **E1 — Export invoked with no arguments.** Failure result carrying the exact three-line usage message.
- **E2 — Export invoked with a path.** **Success** result carrying an informational note — a false positive: the shell prints it with a success mark and no file exists afterwards.
- **E3 — View command with malformed flags.** Never errors; always returns success with the echo message.
- **E4 — Command typed in a shipped shell.** The plain console shell answers `Unknown command: /{name}. Type '/help' for available commands.`; the full-screen shell pops an error dialog reading `Unknown command: {name}` with no leading marker and no pointer to help (QUIRK-10.20).

**Postconditions**
- Nothing is read, nothing is rendered, nothing is written.

---

**Functional requirements**

*Command identity and dispatch*

- **FR-10.1** The feature MUST expose a command named `tokenize` with the description `Analyze and tokenize text using the current LLM model` and the usage string `/tokenize <text> - Tokenizes the provided text and displays token IDs, offsets, and statistics`. (realizes US-10.1)
- **FR-10.2** The feature MUST expose a command named `inspect` with the description `Performs detailed token-level analysis of text using the current model` and the usage string `/inspect <text> - Analyzes token probabilities and attribution`. (realizes US-10.3)
- **FR-10.3** Both commands MUST be reachable through the shared command dispatch convention: a line beginning with the command marker `/` is a command; the marker is stripped; the remainder is split on spaces discarding empty entries; the first element lower-cased is the command name; the remainder is the argument list. Command names are matched case-insensitively; registration is by the command's own lowercase declared name.
- **FR-10.4** The text to analyse MUST be the argument list joined with a **single space**. Because the dispatcher discards empty entries, runs of multiple spaces and tabs in the user's original line are normalised to one space before tokenization. (realizes US-10.1)
- **FR-10.5** Both commands MUST check preconditions in exactly this order: (1) at least one argument; (2) provider; (3) model file. A zero-argument invocation on an otherwise valid local-model configuration MUST fail with the argument error, not the provider or model error. **This ordering is a guarantee, not an implementation detail.** (realizes US-10.7)
- **FR-10.6** With zero arguments, the tokenization command MUST return failure with the message `Please provide text to tokenize. Usage: ` immediately followed by its usage string; the inspection command MUST return failure with `Please provide text to inspect. Usage: ` immediately followed by its usage string. No model is opened. (realizes US-10.7)
- **FR-10.7** Both commands MUST proceed only when the configured provider value equals the exact lowercase string `llama`. The comparison is **case-sensitive and exact**. Otherwise the tokenization command MUST fail with the exact message `The tokenize command only works with the LLamaSharp provider. Use '/set provider llama' first.` and the inspection command with `The inspect command only works with the LLamaSharp provider. Use '/set provider llama' first.` (realizes US-10.7)
- **FR-10.8** Both commands MUST require the configured model identifier to be non-empty **and** to name an existing file. Otherwise they MUST fail with the exact message `LLamaSharp model not configured or file not found. Use '/set modelId <path-to-gguf-model>' first.` (realizes US-10.7)
- **FR-10.9** The model path MUST be validated **only** for existence — no extension check, no size check, no magic-byte check, no readability check. Any existing file, including a zero-byte file or a plain text file, MUST pass this check, with the real failure surfacing later as a wrapped load error.
- **FR-10.10** Both commands MUST read the four settings they use — provider, model identifier, maximum response length, alternatives-per-position count — from the shared mutable settings object **at execution time**, so a setting changed earlier in the same session takes effect without a restart. This requires the shell to *mutate* the shared object rather than replace it (see QUIRK-10.6).
- **FR-10.11** Neither command MAY re-validate the settings it reads. The alternatives count is range-checked (1–20) and the maximum response length is range-checked (1–8192) only at the points where they are *set*; a hand-edited settings file carrying an out-of-range value MUST be passed straight through, yielding zero alternatives per position for a value of zero or below.
- **FR-10.12** On a factory-default installation the provider default is `azure` and the model-identifier default is the literal `gpt-4`, which is not a path. Consequently **both commands fail their preconditions on a fresh install** until the user changes both settings. These are tunable defaults owned by the settings feature, not business rules of this feature.

*Tokenization report*

- **FR-10.13** The tokenization command MUST load the model with a context window of exactly **512** ("small context for tokenization only"). The user's configured context-window setting (default 4096) MUST be ignored. This value is a hard-coded performance choice, not a tunable. (realizes US-10.1)
- **FR-10.14** Loading the model weights and creating the inference context MUST each be performed off the calling thread; tokenization itself and the rendering loop run on the caller.
- **FR-10.15** The text MUST be tokenized **with a beginning-of-sequence marker prepended**, so the reported token count includes that marker. (realizes US-10.2)
- **FR-10.16** The tokenization command MUST write its entire report to standard output, in this exact order, with these exact literals: `Loading model {modelPath} for tokenization...`; `Tokenizing text...`; `Input text: "{text}"`; `Total tokens: {count}`; a blank line; `Token analysis:`; the rule `---------------`; the header `Index | Token ID | Token Text` with the three names **left**-aligned in fixed widths 5 / 8 / 15; the rule `------------------------------------`; then one row per token. (realizes US-10.1, US-10.2)
- **FR-10.17** Each token row MUST carry the zero-based sequence index (width 5, left-aligned), the integer vocabulary ID (width 8, left-aligned), and the token text (width 15, left-aligned), separated by ` | `.
- **FR-10.18** The token text rendered by both commands MUST be the placeholder literal `<token_{id}>`. **The real decoded token string is never shown.** A reimplementer building the intended behaviour needs a detokenize call the source never wired up (QUIRK-10.5). (realizes US-10.2)
- **FR-10.19** Despite its usage string promising "offsets, and statistics", the tokenization command MUST print **no character offsets and no summary statistics** — only index, vocabulary ID and placeholder text (QUIRK-10.4).
- **FR-10.20** On completion the tokenization command MUST return a success result with the exact message `Tokenization complete.` The plain console shell prints a success mark, a space, and that message.
- **FR-10.21** Any failure inside the tokenization command MUST be caught, written in full to the diagnostic trace as `Error in TokenizeCommand: {exception}`, and surfaced as a failure result with the message `Error tokenizing text: {message}`.
- **FR-10.22** The token table MUST have **no upper bound, no paging and no truncation**; an arbitrarily long input produces an unbounded row dump.

*Inspection — announce and tokenization stage*

- **FR-10.23** The inspection command MUST first print `Performing token inspection on: "{text}"` and `This may take a moment to load the model...`. (realizes US-10.3)
- **FR-10.24** The inspection command MUST invoke the *analyze prompt* operation with visualization requested, then render, in order: a blank line; `=== TOKENIZATION ANALYSIS ===`; `Total tokens: {count}`; a blank line; the visualization block when non-empty; `Token Details:`; the rule `-------------`; the header `Index | Token ID | Token Text | Char Range` with the first three names **right**-aligned in fixed widths 5 / 8 / 15 and a literal fourth column label; and the rule `------------------------------------------------------`. (realizes US-10.3)
- **FR-10.25** The visualization block MUST be exactly these five content lines: `Tokenization visualization (simplified):`; the rule `-------------------------------------`; `Original text: "{prompt}"`; `Approximate token count: {tokenCount}`; a blank line; `Note: Actual token boundaries cannot be visualized accurately without decoding functionality.`
- **FR-10.26** Each token detail row MUST render the character span as `{start}-{end}`, **except** that when start equals end it MUST render the literal `n/a`.
- **FR-10.27** Character positions MUST be produced by a uniform-distribution approximation, not a real offset map: let `charCount` be the prompt length in characters and `tokenCount` the number of tokens *including* the beginning-of-sequence marker.
  - When `tokenCount` is greater than 1, `charsPerToken = charCount / (tokenCount - 1)` in floating point — the beginning-of-sequence token is deliberately excluded from the denominator.
  - Token 0 gets start = **−1** and end = **−1**.
  - Token `i` at or above 1 gets `start = round((i-1) × charsPerToken)` and `end = round(i × charsPerToken)`, using the platform's default midpoint rounding rule (half-to-even in the source). *(INFERRED — no test pins a midpoint case; choosing half-away-from-zero would differ on exact ties.)*
  - Clamps are **asymmetric**: `start = min(start, charCount - 1)` and `end = min(end, charCount)`.
  - When `tokenCount` equals 1, that single token gets start = −1 and end = −1.
  - When `tokenCount` is 0, nothing is written and the empty list is returned unchanged.
  - Spans are therefore contiguous and non-overlapping by construction, and bear no relation to real token boundaries.
- **FR-10.28** Before display, token text MUST have newline escaped to `\n`, carriage return to `\r`, and tab to `\t`. If, **after** escaping, the display text is whitespace-only but the original was non-empty, it MUST be replaced by `[whitespace:{comma-separated character codes}]`. With the current placeholder text this branch is unreachable; it must nonetheless be implemented, because it is the correct behaviour once real decoded tokens exist.

*Inspection — consent gate*

- **FR-10.29** After rendering tokenization the inspection command MUST print a blank line and then the exact question `Do you want to continue with probability analysis? This will generate tokens and analyze their probabilities. (y/n)`, then **block reading one line from standard input**. There MUST be no timeout, no default and no cancellation. (realizes US-10.4)
- **FR-10.30** The reply MUST be lower-cased using invariant casing; **only** `y` or `yes` proceeds. Any other reply — including an empty line and end-of-file — MUST silently skip the probability and attribution stages.
- **FR-10.31** The inspection command MUST return a success result with the exact message `Token inspection complete.` on **both** the consented and the declined path — the two outcomes are indistinguishable in the result.

*Inspection — probability stage*

- **FR-10.32** On consent the command MUST print `Generating probability map...`, then compute the generation budget as **min(10, configured maximum response length)** and invoke the *generate probability map* operation with that budget and with the alternatives-per-position count taken from the configured top-K setting. The cap of 10 overrides the configured maximum (default 1000) unconditionally. (realizes US-10.5)
- **FR-10.33** The probability-map operation MUST load the model with a context window of exactly **2048**, ignoring the user's configured context-window setting, and MUST create the generator with an **empty** stop-sequence list, so generation ends only on the token budget or the model's own end-of-sequence.
- **FR-10.34** The probability-map operation MUST assemble the streamed pieces into one string, then split that string on exactly five separators — space, `.`, `,`, `!`, `?` — discarding empty entries. Newlines, tabs, semicolons, colons, quotes and dashes are **not** separators.
- **FR-10.35** For each resulting word at index `i` the operation MUST fabricate: a generated-token record with index `i`, vocabulary ID **1000 + i**, text equal to the word and log-probability **−2.5**; a step-probability record at position `i` mirroring that record's ID, text and log-probability; and exactly *top-K* alternatives, where alternative `j` (zero-based) has vocabulary ID **2000 + (i × 100) + j**, text `{word}_alt{j+1}` and log-probability **−3.0 − j**. The ×100 stride keeps alternative IDs unique while top-K is at most 100; the −3.0 − j progression guarantees alternatives are emitted in strictly descending probability order and always below the selected token's −2.5. **These numbers are synthetic, not measured** (QUIRK-10.3).
- **FR-10.36** The command MUST render the probability section as: `=== TOKEN PROBABILITY ANALYSIS ===`; `Generated continuation:`; the rule `----------------------`; the concatenation of all generated token texts with no separator; `Token Probabilities:`; the rule `------------------`; then per entry `Token {n}: "{selectedText}" (ID: {selectedId})` with `n` **1-based** while the underlying index is 0-based; then `  Log Probability: {logprob} ({percentage}%)`; then, when alternatives exist, `  Top alternatives:` and per alternative `    "{text}" (ID: {id}) - LogProb: {logprob} ({percentage}%)`; then a blank line. (realizes US-10.5)
- **FR-10.37** Log-probabilities MUST be formatted to **5** decimal places and derived percentages to **2** decimal places, where percentage = e^(log-probability) × 100. Formatting uses the ambient culture, so a locale with a comma decimal separator renders `-2,50000 (8,21%)` (QUIRK-10.32).
- **FR-10.38** When the alternatives count is zero or negative, each entry MUST print its selected token and **no** alternatives heading; nothing errors.
- **FR-10.39** When generation produces no words, the section headers MUST still print, the generated continuation MUST be empty, no entries appear, and the command returns success.

*Inspection — attribution stage*

- **FR-10.40** The command MUST render the attribution section as: `=== TOKEN ATTRIBUTION ANALYSIS ===`; `This shows which parts of the input may have influenced each generated token.`; a blank line; then per attribution `Token {index+1} "{tokenText}" was influenced by:`, then `  "{influencingText}" (score: {score})`, then a blank line. (realizes US-10.6)
- **FR-10.41** The attribution heuristic MUST be, for the generated token at index `i`: if `i` is **less than 3** and the prompt is non-empty, the influencing text is the **last** min(**50**, prompt length) characters of the prompt; otherwise it is the concatenation, with no separator, of the text of generated tokens in the half-open range [max(0, i − **5**), i). For `i` = 0 with an empty prompt this yields the empty string. (realizes US-10.6)
- **FR-10.42** Every attribution entry MUST carry the constant influence score **0.8**, rendered to 2 decimal places as `0.80`. This is an explicit placeholder for a real influence calculation, not a computed value.
- **FR-10.43** Influencing text longer than **40** characters MUST be cut to its first **37** characters and suffixed with three dots. Truncation happens **before** escaping, so an escape sequence can be split. Attribution text escaping covers newline and carriage return **only** — tab is not escaped and is emitted raw.

*View and export commands (stubs)*

- **FR-10.44** The feature MUST define a command named `show-analysis` with the description `Display detailed token-level analysis from the last LLamaSharp generation` and a multi-line usage string listing three options: a candidate-count flag `--top N` (default **3**), a bare model-state flag `--state` (default false), and a two-value step-range flag `--range START END`. (realizes US-10.10)
- **FR-10.45** The view command's flag parsing MUST scan left to right. The count flag consumes the following argument **only if** it exists and parses as an integer, advancing past it; a missing or non-numeric value leaves the default 3 in place and **does not consume** the value, which is then examined as a flag and ignored. The range flag is honoured only when **two** further arguments exist, advancing past both; each is parsed independently. No range is validated — negatives, inverted ranges and a zero count are accepted and echoed verbatim. Unknown flags and stray words are silently ignored. Repeated flags: last one wins.
- **FR-10.46** The view command MUST return **success** with this exact five-line message and MUST read no analysis data and render no table: `Note: This command requires LLamaSharp provider integration.` / `Token analysis data would be displayed here when integrated with the AI service` / `Options parsed: top={count}, state={stateFlag}, range={start}-{end}` / a blank line / `Use 'export-analysis <file>' to save full analysis to JSON`. With no range flag the start is 0 and the end is the sentinel **−1**, so the echo reads the literal `range=0--1`.
- **FR-10.47** The feature MUST define a command named `export-analysis` with the description `Export detailed token-level analysis from the last LLamaSharp generation to JSON file` and the usage string `export-analysis <filepath>` followed by `  Example: export-analysis analysis.json`. (realizes US-10.10)
- **FR-10.48** With zero arguments the export command MUST return **failure** with this exact three-line message: `Usage: export-analysis <filepath>` / `Example: export-analysis analysis.json` / `Note: This command only works with LLamaSharp provider`.
- **FR-10.49** With one or more arguments the export command MUST return **success** with this exact two-line message and MUST create no file and validate no path: `Note: This command requires LLamaSharp provider integration.` / `To use: Ensure provider is set to 'llama' and generate a response first`. The success flag causes the shell to print a success mark, making this a user-visible false positive.
- **FR-10.50** Neither shell registers `show-analysis`, `export-analysis` or the sibling `export-logs`; each shell hand-wires an explicit command list containing only `tokenize` and `inspect` from this feature. Typing any of the three MUST yield the shell's unknown-command response.

*Per-step analysis record and its capture*

- **FR-10.51** The analysis list MUST be **cleared at the start of every generation**, so only the most recent generation is retained. It is append-only within a generation. There is no user-facing clear operation. (realizes US-10.9)
- **FR-10.52** One analysis record MUST be appended per emitted token, with: step = emitted-token count − 1; token text = the emitted piece; vocabulary index = **−1 always** (real IDs are not available); probability = a temperature-derived estimate; log-probability = the natural log of that estimate; prompt offset = the same step number; debug text = `Generated via sampling pipeline at step {step}, temperature={temperature to 2 decimal places}.` (realizes US-10.9)
- **FR-10.53** The probability estimate MUST be selected by temperature band: temperature at or below 0.1 → **0.95**; at or below 0.5 → **0.85**; at or below 0.7 → **0.75**; at or below 1.0 → **0.60**; at or below 1.5 → **0.50**; otherwise **0.40**. At the product default temperature of 0.7 every token therefore reads probability 0.75 and log-probability −0.28768.
- **FR-10.54** Each analysis record MUST carry a model-state snapshot whose total-tokens-processed and context-token-count both equal the step; whose context size is the configured context-window setting, falling back to **4096** when that is at or below 0; whose remaining capacity is context size − step; whose timestamp is taken from the **local** clock; and whose diagnostic map holds the keys `Temperature` (2 decimal places), `TopK` and `Mode` with the value `SamplingPipeline`.
- **FR-10.55** Candidates attached to an analysis record MUST be computed from the current logits, taking the configured top-K clamped to at least 1 and at most the logit-vector length, softmaxed against the running maximum. Each candidate MUST have **only** its text and log-probability filled; vocabulary ID, probability and raw pre-softmax score are left at zero. If the computation throws, the failure is swallowed to a warning log line `Failed to compute candidates from logits: {message}` and that step keeps an empty candidate list.
- **FR-10.56** A programmatic retrieval operation MUST return the analysis list as a **defensive copy**; a save operation MUST write the list as an **indented array** to a file path; a third operation MUST save the system logs to a file. Save failures MUST be swallowed into a logged error `Failed to save token analyses: {exception}` with no signal to the caller.
- **FR-10.57** The per-step analysis record and its nested types MUST serialize with these exact field names — this is a **wire-format compatibility requirement** for exported analysis files:

  ```
  Analysis record (array element):
    step             integer   0-based generation step
    tokenText        string    selected token text
    tokenId          integer   vocabulary index (−1 in practice)
    probability      number    documented range 0.0–1.0
    logprob          number    natural log of probability
    promptOffset     integer   equals step in practice
    topCandidates    array of Candidate, defaults to empty
    systemDebugInfo  string, nullable
    modelState       ModelState, optional

  Candidate:
    text             string    decoded alternative text, or the literal "id:{tokenId}" when decoding fails
    tokenId          integer   0 in practice
    probability      number    0 in practice
    logprob          number
    logit            number    raw pre-softmax score; 0 in practice

  ModelState:
    totalTokensProcessed  integer
    contextTokenCount     integer
    contextSize           integer
    remainingContext      integer
    timestamp             date-time
    debugInfo             map of string to string, defaults to empty
  ```
  Round-trip fidelity MUST be preserved for step, token text, vocabulary index, probability, candidate-list length and the nested snapshot's total-tokens-processed. The exported document is a **JSON array**, written with indentation.
- **FR-10.58** All analysis entities MUST be freely constructible with no arguments, with every member independently writable and **nothing validated on assignment** — a probability of 0.5 stored alongside a log-probability that does not agree with it MUST be accepted as given. The model-state snapshot member is optional; the candidate list defaults to empty.
- **FR-10.59** The influence score MUST be a single-precision value accepting any float; nothing constrains it to the range 0–1, even though the only producer writes 0.8.

*Ordering guarantees*

- **FR-10.60** Token records from tokenization MUST be in sequence order with contiguous zero-based indices, the beginning-of-sequence marker first.
- **FR-10.61** Step-probability records MUST be in generation order with each record's position equal to its list index; alternatives within a record MUST be in descending probability order.
- **FR-10.62** Attribution entries MUST be in generation order, one per generated token, with attribution index equal to generated-token index, and the attribution list length always equal to the generated-token list length.
- **FR-10.63** The probability-mapping result MUST hold the original prompt, the generated-token list, the step-probability list and the attribution list, with the three lists of identical length and order.

*Integration and non-functional*

- **FR-10.64** Both working commands MUST appear in the general help listing under the exact heading `LLama Provider Commands (local LLM):`, and per-command help MUST render `Command: /{name}` / `Description: {description}` / `Usage: {usage}`. (realizes US-10.8)
- **FR-10.65** Neither working command MAY cache the loaded model. Each tokenization invocation loads and disposes a model; each inspection invocation does so **twice**, with context windows 512 and 2048 and a full unload between. For a multi-gigabyte model this makes both commands dramatically slower than a chat turn. *(A reimplementer should strongly consider a shared cached tokenizer; this is called out as a deliberate deviation, not a silent one.)*
- **FR-10.66** The feature MUST take **no locks** and MUST NOT participate in the process-wide guards the chat path uses to serialise model loading and generation. A command issued while a chat generation is running attempts a concurrent native model load. *(INFERRED risk; no test or guard exists.)*
- **FR-10.67** The feature MUST provide **no cancellation** — a long model load or generation cannot be aborted.
- **FR-10.68** The feature MUST ignore the configured GPU layer count, GPU device, thread count, batch size and context window; both loads use hard-coded context windows and library defaults for everything else.
- **FR-10.69** The feature MUST perform **no authorization checks of any kind**. Arbitrary local file paths are accepted as model identifiers with no sandboxing.
- **FR-10.70** All user-visible strings MUST be hard-coded English literals; there is no internationalization. Timestamps use the patterns `yyyy-MM-dd HH:mm:ss.fff` and `yyyyMMdd` but are rendered with the **local** clock; numeric formatting is culture-sensitive.
- **FR-10.71** The feature MUST read **no environment variables of its own**.
- **FR-10.72** The two inspection operations MUST be stateless: they retain nothing between invocations and persist nothing. Every entity except the per-step analysis record is created per invocation, lives only long enough to be printed, and is then discarded.

*Diagnostic log sink (consumed here, owned by the diagnostic-logging feature)*

- **FR-10.73** The per-step analysis record's debug-text field and the introspection documentation depend on a diagnostic log sink whose factory state is: file logging **on**, debug output **on**, console output **off**, buffer threshold **10000** characters, and a non-null log directory defaulting to the platform application-data folder joined with `ChatDbg` then `Logs` (on the source platform, `%APPDATA%\ChatDbg\Logs`).
- **FR-10.74** Log entries MUST be formatted `[{yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}` and MUST flush to a daily file named `llamasharp_{yyyyMMdd}.log` inside the log directory, in append mode, when the buffer exceeds the threshold and file logging is on. Explicit save-to-path **overwrites** the target, creates missing parent directories, and then appends `Logs saved to {path}` to the buffer. Configuring native log capture and disposing the sink MUST each be idempotent. All file failures MUST be swallowed to the debug trace.

---

**External technology**

*Requires: a local transformer inference engine exposing a tokenizer and a streaming text generator (native shared library invoked through a language binding; model files in a single-file quantized-weights container format). Source used: LLamaSharp 0.25.0 with CPU and CUDA-12 backend packages, over llama.cpp, consuming GGUF model files. Reimplementer notes: only three capabilities are actually exercised — load-model-with-explicit-context-size, tokenize-string-with-beginning-of-sequence-marker yielding integer vocabulary IDs, and stream-generate-with-max-token-budget-and-stop-sequence-list. A detokenize (ID → string) call is **required by the intended behaviour but was never wired up**; the clone should implement it rather than reproduce the `<token_N>` placeholder, and must then decide whether the whitespace-fallback display rule (FR-10.28) becomes reachable. The backend is loaded and unloaded once per command, twice for the inspection command. The CUDA backend is referenced unconditionally and is dead weight on non-NVIDIA machines; nothing in the code selects between backends and this feature never enables GPU offload at all.*

*Requires: a managed runtime with asynchronous streaming enumeration and background-thread offload for blocking native calls. Source used: .NET 10 (`net10.0`), SDK pinned to `10.0.100-rc.1.25451.107` in the build's version file with roll-forward to the latest feature band. Reimplementer notes: model loading and context creation are pushed onto worker threads so the caller's thread is not blocked; generation is consumed as an asynchronous stream. Tokenization and the streaming loop run on the caller. No cancellation token exists anywhere in the feature.*

*Requires: structured-document serialization with explicit field naming. Source used: the platform's built-in JSON serializer with per-member name attributes; the exporter writes an indented array. Reimplementer notes: only the per-step analysis record, its candidate entries and its state snapshot are annotated; the six inspection result types are never serialized. The exported field names in FR-10.57 are a compatibility surface — an exported analysis file must remain readable by any tool built against those names.*

*Requires: a rich terminal rendering library. Source used: Spectre.Console 0.51.1. Reimplementer notes: imported by the two stub commands but **never called**. The commands that actually render use plain fixed-width text with pipe separators and dashed rules — reproduce that, not a styled table.*

*Requires: a full-screen terminal UI toolkit (ANSI terminal). Source used: Terminal.Gui 1.19.0, in the full-screen shell only. Reimplementer notes: relevant here only because it **breaks** this feature — raw standard-output writes and a blocking standard-input read cannot work under a toolkit that owns the screen and keyboard (QUIRK-10.6, QUIRK-10.7).*

*Requires: per-user configuration storage on local disk. Source used: a structured text settings document at `~/.ChatDbg/settings.json`, falling back to the platform temp directory when the user-profile folder resolves to an empty string. Reimplementer notes: read-only from this feature's perspective; the fallback silently converts per-user settings into per-boot settings.*

*Requires: per-user diagnostic log storage as plain text with daily rollover. Source used: `%APPDATA%\ChatDbg\Logs\llamasharp_{yyyyMMdd}.log` — the platform application-data special folder joined with `ChatDbg` then `Logs`. Reimplementer notes: append mode for threshold flushes, overwrite mode for an explicit save-to-path; the directory is created on demand. On platforms where the application-data folder resolves empty, the joined path becomes relative and logs land under the working directory or the write silently fails.*

*Requires: native-library log interception — a callback registered with the inference library receiving a severity level and a message string. Source used: the inference binding's native log-set hook. Reimplementer notes: registered once and never unregistered. The clone needs an equivalent sink that timestamps each line, tags it with a level, buffers it in memory under a lock, and flushes past a character threshold.*

*Requires: console standard input and standard output (plain text, no escape sequences). Source used: direct process standard output and a blocking standard-input line read. Reimplementer notes: the entire report bypasses the command result contract; only the one-line completion message travels back through it. Under a full-screen terminal UI this breaks.*

*Requires: a unit test framework with mocking. Source used: xUnit 2.9.1 with Moq 4.20.69. Reimplementer notes: the tests for this feature use no mocks, because the two inspection operations are process-wide utilities with no substitutable seam — which is why the only service-level tests are the missing-file guards. **A reimplementer should introduce an injectable tokenizer/generator abstraction.***

*Requires: environment variables. Source used: **none read by this feature.** Reimplementer notes: the product reads credential variables elsewhere, but token inspection has no environment-variable switch of its own.*

---

**Acceptance criteria**

*Gating*

- **AC-10.1** **Given** the provider setting is `azure` (the factory default) and any model identifier, **when** the user types `/tokenize hello`, **then** the result is a failure whose message is exactly `The tokenize command only works with the LLamaSharp provider. Use '/set provider llama' first.`, the console line is a success/failure mark followed by that message, and no model file is opened.
- **AC-10.2** **Given** the provider setting is `llama` and the model identifier is a path under the platform temp directory that does not exist, **when** the user types `/tokenize hello`, **then** the result is a failure whose message is exactly `LLamaSharp model not configured or file not found. Use '/set modelId <path-to-gguf-model>' first.`
- **AC-10.3** **Given** the provider setting is `llama` and the model identifier is `model.gguf`, **when** the user types `/inspect` with no further words, **then** the result is a failure whose message is exactly `Please provide text to inspect. Usage: /inspect <text> - Analyzes token probabilities and attribution` — proving the argument check runs before the provider and model checks.
- **AC-10.4** **Given** the provider setting is `azure`, **when** the user types `/inspect text`, **then** the result is a failure with exactly `The inspect command only works with the LLamaSharp provider. Use '/set provider llama' first.`
- **AC-10.5** **Given** the provider setting is `llama` and the model identifier is `missing.gguf` (relative, non-existent), **when** the user types `/inspect text`, **then** the result is a failure with exactly `LLamaSharp model not configured or file not found. Use '/set modelId <path-to-gguf-model>' first.`
- **AC-10.6** **Given** the provider setting in the settings document is `LLAMA`, **when** the user types `/tokenize hi`, **then** the provider check still fails — the comparison is exact and case-sensitive.

*Tokenization report*

- **AC-10.7** **Given** a readable model file and the line `/tokenize   This    is  a test` typed with runs of spaces, **when** the command runs, **then** the console shows in order: `Loading model {modelId} for tokenization...`, `Tokenizing text...`, `Input text: "This is a test"` (runs of spaces collapsed to one), `Total tokens: {T}`, a blank line, `Token analysis:`, `---------------`, `Index | Token ID | Token Text` with the three names left-aligned in widths 5 / 8 / 15, `------------------------------------`, then exactly `T` rows of the form `{i} | {id} | <token_{id}>` with `i` starting at 0 and the three fields left-aligned in widths 5 / 8 / 15 — and the result is success with the message `Tokenization complete.`
- **AC-10.8** **Given** the same run, **then** no character offsets and no summary statistics appear anywhere in the output, despite the usage line promising "token IDs, offsets, and statistics".
- **AC-10.9** **Given** a model whose weights fail to load, **when** `/tokenize hi` runs, **then** the result is a failure with the message `Error tokenizing text: {native error text}` and the line `Error in TokenizeCommand: {full exception}` goes to the diagnostic trace channel only.

*Inspection, tokenization stage*

- **AC-10.10** **Given** `/inspect Tell me about quantum computing`, **when** the tokenization stage renders, **then** the console shows `Performing token inspection on: "Tell me about quantum computing"`, `This may take a moment to load the model...`, a blank line, `=== TOKENIZATION ANALYSIS ===`, `Total tokens: {T}`, a blank line, the five-line visualization block ending with `Note: Actual token boundaries cannot be visualized accurately without decoding functionality.`, then `Token Details:`, `-------------`, the header `Index | Token ID | Token Text | Char Range` with the first three names right-aligned in widths 5 / 8 / 15, and `------------------------------------------------------`.
- **AC-10.11** **Given** a prompt of exactly 30 characters that tokenizes to 6 tokens including the leading beginning-of-sequence token, **when** the table renders, **then** row 0 shows `n/a` and rows 1 through 5 show `0-6`, `6-12`, `12-18`, `18-24`, `24-30`.
- **AC-10.12** **Given** a prompt that tokenizes to exactly 1 token, **when** the table renders, **then** that single row shows `n/a`.
- **AC-10.13** **Given** an empty prompt that still tokenizes to 2 tokens, **when** the table renders, **then** row 0 shows `n/a` and row 1 shows the literal `-1-0`.

*Inspection, consent gate*

- **AC-10.14** **Given** the tokenization stage has rendered, **then** the console shows a blank line followed by exactly `Do you want to continue with probability analysis? This will generate tokens and analyze their probabilities. (y/n)` and the process blocks on a line of standard input.
- **AC-10.15** **Given** the user answers `Y`, `y`, `YES` or `yes`, **then** the probability and attribution stages run. **Given** any other reply — `n`, `maybe`, an empty line, or end-of-file from redirected input — **then** both stages are skipped, nothing further is printed, and the command still returns success with the message `Token inspection complete.`, indistinguishable in the result from a completed analysis.

*Inspection, probability and attribution stages*

- **AC-10.16** **Given** the user consented, the maximum response length is 1000, the alternatives count is 5, and the model's continuation assembles to the text `alpha beta gamma`, **then** the console shows `Generating probability map...`, `=== TOKEN PROBABILITY ANALYSIS ===`, `Generated continuation:`, `----------------------`, the line `alphabetagamma` (the words concatenated with no separator, because the separators were consumed by the split), `Token Probabilities:`, `------------------`, and exactly three entries.
- **AC-10.17** **Given** the same run, **then** the first entry reads exactly `Token 1: "alpha" (ID: 1000)`, then `  Log Probability: -2.50000 (8.21%)`, then `  Top alternatives:`, then `    "alpha_alt1" (ID: 2000) - LogProb: -3.00000 (4.98%)`, `    "alpha_alt2" (ID: 2001) - LogProb: -4.00000 (1.83%)`, `    "alpha_alt3" (ID: 2002) - LogProb: -5.00000 (0.67%)`, `    "alpha_alt4" (ID: 2003) - LogProb: -6.00000 (0.25%)`, `    "alpha_alt5" (ID: 2004) - LogProb: -7.00000 (0.09%)`, then a blank line; and the second entry's alternative IDs are 2100 through 2104 and the third's are 2200 through 2204.
- **AC-10.18** **Given** the maximum response length is 4, **then** at most 4 tokens are requested from the generator; **given** it is 1000, at most 10 are requested.
- **AC-10.19** **Given** the alternatives count is 0 (hand-edited into the settings document), **then** every probability entry prints its selected token and no `Top alternatives:` line at all, and nothing errors.
- **AC-10.20** **Given** the model emits nothing, or only the characters space, `.`, `,`, `!` and `?`, **then** the probability and attribution headers still print, `Generated continuation:` is followed by an empty line, no entries appear, and the command returns success.
- **AC-10.21** **Given** a prompt of 80 characters and 6 generated words, **when** the attribution stage renders, **then** the console shows `=== TOKEN ATTRIBUTION ANALYSIS ===`, `This shows which parts of the input may have influenced each generated token.`, a blank line, then six entries; entries 1 through 3 each name the last 50 characters of the prompt, entry 4 names the concatenation of words 1 through 3, entry 5 words 1 through 4, entry 6 words 1 through 5; and every entry ends `(score: 0.80)`.
- **AC-10.22** **Given** an influencing excerpt of exactly 50 characters, **then** it is displayed as its first 37 characters followed by three dots — the 40-character display threshold is smaller than the 50-character excerpt, so the first three entries are always truncated.
- **AC-10.23** **Given** an influencing excerpt containing a newline, **then** it appears as `\n`; **given** it contains a tab, **then** the tab is emitted raw and unescaped.
- **AC-10.24** **Given** the same inspection run repeated on the same text, **then** the printed IDs, log-probabilities, percentages and scores are byte-identical, because they are functions of the word index only; the only thing that can differ between runs is the generated words themselves.

*Service-level guards*

- **AC-10.25** **Given** either inspection operation is called with a path that is empty or does not exist, **then** a file-not-found condition is raised carrying the message `Model file not found` with the offending path attached, and it is **not** wrapped in an invalid-operation condition.
- **AC-10.26** **Given** any other failure inside the analyze operation, **then** an invalid-operation condition is raised with the message `Error analyzing prompt: {inner message}` and the inner cause attached; surfaced through the inspection command, the user sees `Error during token inspection: Error analyzing prompt: {inner message}`.

*View and export commands*

- **AC-10.27** **Given** the view command is executed directly with the arguments `--top 2 --state`, **then** it returns success with the exact message `Note: This command requires LLamaSharp provider integration.` / `Token analysis data would be displayed here when integrated with the AI service` / `Options parsed: top=2, state=True, range=0--1` / a blank line / `Use 'export-analysis <file>' to save full analysis to JSON`, and no analysis table is rendered and no analysis data is read.
- **AC-10.28** **Given** the view command is executed with `--range 5` (one value), or `--top` (no value), or `--top abc`, **then** the defaults survive and the echo still reads `top=3` and/or `range=0--1`; no error is produced and the unparsed word is silently ignored.
- **AC-10.29** **Given** the view command is executed with `--range abc 10`, **then** the echo reads `range=0-10`.
- **AC-10.30** **Given** the export command is executed with no arguments, **then** it returns failure with the exact three-line message `Usage: export-analysis <filepath>` / `Example: export-analysis analysis.json` / `Note: This command only works with LLamaSharp provider`.
- **AC-10.31** **Given** the export command is executed with the argument `analysis.json`, **then** the result is success with the exact two-line message `Note: This command requires LLamaSharp provider integration.` / `To use: Ensure provider is set to 'llama' and generate a response first`, the shell prints it with a success mark, and **no file named `analysis.json` exists afterwards**.
- **AC-10.32** **Given** the sibling log-export command with the same two argument shapes, **then** the same failure/success pattern holds.
- **AC-10.33** **Given** a shipped build of either shell, **when** the user types `/show-analysis`, `/export-analysis x.json` or `/export-logs x.log`, **then** the plain console shell answers `Unknown command: /show-analysis. Type '/help' for available commands.` (and equivalents) and the full-screen shell pops an error dialog reading `Unknown command: show-analysis` — none of the three is registered.
- **AC-10.34** **Given** the user types `/help`, **then** `tokenize` and `inspect` appear under the heading `LLama Provider Commands (local LLM):`; **and given** the user types `/help tokenize`, **then** the output is `Command: /tokenize` / `Description: Analyze and tokenize text using the current LLM model` / `Usage: /tokenize <text> - Tokenizes the provided text and displays token IDs, offsets, and statistics`.

*Per-step analysis record and its round trip*

- **AC-10.35** **Given** an analysis record with step 3, token text `test`, vocabulary index 999, probability 0.85, log-probability −0.162, prompt offset 20, one candidate and a state snapshot whose total-tokens-processed is 100, **when** it is serialized and read back, **then** step, token text, vocabulary index, probability (to 3 decimal places), candidate-list length and the snapshot's total-tokens-processed are all preserved, under the field names `step`, `tokenText`, `tokenId`, `probability`, `logprob`, `promptOffset`, `topCandidates`, `systemDebugInfo`, `modelState`.
- **AC-10.36** **Given** a chat turn at the default temperature 0.7 producing 5 tokens, **then** the analysis list holds exactly 5 records, each with vocabulary index −1, probability exactly 0.75, log-probability −0.28768, prompt offset equal to its own step, and debug text `Generated via sampling pipeline at step {n}, temperature=0.70.`
- **AC-10.37** **Given** a second chat turn begins, **then** the analysis list is emptied before the first token of the new turn is recorded — only the most recent generation is ever retrievable.
- **AC-10.38** **Given** the analysis list is written to a file, **then** the file holds an indented JSON array; **and given** the write fails, **then** nothing is thrown and nothing is reported to the caller — only a logged error `Failed to save token analyses: {exception}`.
- **AC-10.39** **Given** an analysis record is constructed with only a step number and a state snapshot, **then** it is valid; **and given** one is constructed with neither candidates nor a snapshot, **then** it is valid, the candidate list defaults to empty and the snapshot is absent.
- **AC-10.40** **Given** a candidate list of three entries written in a given order, **then** insertion order and every per-entry value — text, vocabulary index, probability, log-probability and raw pre-softmax score — are preserved on read-back.

*Diagnostic log sink*

- **AC-10.41** **Given** a freshly constructed log sink, **then** file logging is on, debug output is on, console output is off, the buffer threshold is 10000 characters, and the log directory is non-null.
- **AC-10.42** **Given** messages logged at INFO, WARNING and ERROR levels, **then** the accumulated text contains `[INFO]`, `[WARNING]` and `[ERROR]` and all three message bodies in write order, each line prefixed `[{yyyy-MM-dd HH:mm:ss.fff}] `; **and when** the sink is cleared, **then** the accumulated text is exactly the empty string.
- **AC-10.43** **Given** the sink is saved to an explicit path, **then** that file exists and contains the logged text, the buffer is **not** cleared, and the confirmation line `Logs saved to {path}` is absent from the file just written.
- **AC-10.44** **Given** the sink is disposed twice, or native log capture is configured twice, **then** neither throws.

---

**Quirks**

- *QUIRK-10.1: The view command, the analysis-export command and the sibling log-export command are not registered in either shell; both shells hand-wire an explicit command list containing only the tokenization and inspection commands from this feature, so typing any of the three yields an unknown-command response — while the shipped documentation presents all three as working, with sample transcripts. Evidence: `src/ChatDbg/ChatShell.cs:42-57`; `src/ChatDbg.Shell.Gui/Program.cs:31-47`; `docs/LLamaSharp-Token-Introspection.md:57-96,269-303`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.2: Even if registered, all three commands are pure stubs — they read no data and write no files, returning a fixed informational note where the documentation shows a rendered probability table and an "exported 12 token analyses" confirmation. Evidence: `src/Xcaciv.ChatDbg.Core/Commands/ShowTokenAnalysisCommand.cs:46-48`; `src/Xcaciv.ChatDbg.Core/Commands/ExportTokenAnalysisCommand.cs:23`; `docs/LLamaSharp-Token-Introspection.md:279-303`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.3: The probability map and attribution data produced by the inspection command are fabricated, not measured. Real inference runs and the generated text is real, but every token ID, log-probability, alternative and influence score is a formula of the word index. The code's own comments say "Made-up token ID", "synthetic" and "Placeholder"; the product README describes the same output as "token probability mapping" and "token attribution" without qualification. Evidence: `src/Xcaciv.ChatDbg.Core/Services/TokenInspectionService.cs:151-193,281,319`; `README.md:293-295`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.4: The README and the command's own usage string promise that the tokenization command shows offsets and statistics; the implementation prints only index, vocabulary ID and a placeholder text. Evidence: `src/Xcaciv.ChatDbg.Core/Commands/TokenizeCommand.cs:20,70-88` vs `README.md:282`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.5: Token text is never decoded — both commands display the literal `<token_{id}>`, and the visualization block admits the limitation in its own text. The README's example output block shows decoded token strings and real percentages that no command in this feature can produce. Evidence: `src/Xcaciv.ChatDbg.Core/Services/TokenInspectionService.cs:56,268`; `src/Xcaciv.ChatDbg.Core/Commands/TokenizeCommand.cs:85`; `README.md:303-345`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.6: In the full-screen shell both working commands can never succeed. That shell constructs the commands with the initial default settings object, then replaces its variable with the settings loaded from disk before building the window; the commands keep a reference to the discarded object, whose provider is `azure`, so the provider precondition always fails. The plain console shell avoids this by copying loaded values field-by-field onto the same object. Evidence: `src/ChatDbg.Shell.Gui/Program.cs:14,45-46,58` vs `src/ChatDbg/ChatShell.cs:24,56-57,122-152`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.7: In the full-screen shell command output would be invisible or actively harmful. Both commands write their entire report straight to standard output while that shell surfaces only a command's single result message; the raw writes corrupt the managed screen, and the inspection command's blocking standard-input read has no console to read from. (INFERRED — not exercised by any test.) Evidence: `src/Xcaciv.ChatDbg.Core/Commands/InspectCommand.cs:50-65,98-210`; `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:399-416`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.8: Configured GPU, context-window, thread and batch settings are all ignored by this feature in favour of hard-coded context windows of 512 and 2048 and library defaults, while the documentation advertises GPU acceleration for introspection. Evidence: `src/Xcaciv.ChatDbg.Core/Services/TokenInspectionService.cs:30-33,104-107`; `src/Xcaciv.ChatDbg.Core/Models/ChatSettings.cs:53-66`; `docs/LLamaSharp-Token-Introspection.md:192-200`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.9: The inspection command loads the model twice in one invocation — once for tokenization at context window 512 and once for generation at 2048 — with a full unload in between. Evidence: `src/Xcaciv.ChatDbg.Core/Commands/InspectCommand.cs:54,73`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.10: The implementation summary states the inference-library version three times as 0.11.2 and reasons at length about what "the actual 0.11.2 release" exposes; the project actually references 0.25.0. Evidence: `IMPLEMENTATION_SUMMARY.md:75-77,139-165` vs `src/Xcaciv.ChatDbg.Core/Xcaciv.ChatDbg.Core.csproj:13-15`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.11: Chat-time analysis records always carry vocabulary index −1 and a temperature-derived probability estimate rather than a measured one, so any export would report estimates. At the default temperature of 0.7 every token in an export reads probability 0.75 and log-probability −0.2877, making the whole file look uniformly and falsely confident. Evidence: `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:275,281,283,310-323`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.12: The two stub commands import the rich-console rendering library but never call it — a leftover from an intended table renderer. Evidence: `src/Xcaciv.ChatDbg.Core/Commands/ShowTokenAnalysisCommand.cs:1`; `src/Xcaciv.ChatDbg.Core/Commands/ExportTokenAnalysisCommand.cs:1`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.13: The probability-map operation accumulates emitted pieces into a word collection that is never read; the real word list is produced by re-splitting the assembled string. Evidence: `src/Xcaciv.ChatDbg.Core/Services/TokenInspectionService.cs:131,137-147,156`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.14: The feature's "top-K" is not a top-K over the vocabulary — it emits exactly that many fabricated alternatives, even when the value exceeds any plausible vocabulary size, and simply produces none when the value is 0 or negative. It applies none of the 1–20 clamping enforced where the setting is set. Evidence: `src/Xcaciv.ChatDbg.Core/Services/TokenInspectionService.cs:182-190`; `src/Xcaciv.ChatDbg.Core/Commands/LogProbsCommand.cs:57-59`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.15: Off-by-one in the chat-time candidate capture — the alternatives attached to step n are computed from the logits current *after* step n was emitted (the code comment says "Compute candidates for the NEXT token using current logits"). Every record's candidate list therefore describes the following step, and the final step's real candidates are dropped from this list. Evidence: `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:209-212,222-227,235-246`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.16: Exported candidates are half-empty — only text and log-probability are filled, so every alternative shows vocabulary index 0, probability 0 and raw score 0, values that look measured but are not. Evidence: `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:225-227` vs `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/TokenAnalysis.cs:68-99`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.17: The implementation summary claims the fabrication was removed ("Removed dummy token probability generation") and separately lists real probabilities, token IDs, top-N candidates and raw logits as not yet implemented; the fabricating code is still present and still reachable. Evidence: `IMPLEMENTATION_SUMMARY.md:119-135,248` vs `src/Xcaciv.ChatDbg.Core/Services/TokenInspectionService.cs:151-193`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.18: The attribution builder is handed the probability-map list as a parameter and never reads it. Evidence: `src/Xcaciv.ChatDbg.Core/Services/TokenInspectionService.cs:276-324`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.19: The `n/a` marker collides with a real empty span — it prints whenever start equals end, which is true both for the beginning-of-sequence sentinel and for any genuinely zero-width span; conversely an empty input that yields two or more tokens prints the nonsensical range `-1-0` for every non-sentinel token, because the start clamp floors at −1. Evidence: `src/Xcaciv.ChatDbg.Core/Commands/InspectCommand.cs:131-133`; `src/Xcaciv.ChatDbg.Core/Services/TokenInspectionService.cs:240-241`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.20: The two shells disagree on the unknown-command message — one says `Unknown command: /{name}. Type '/help' for available commands.` and the other pops a dialog reading `Unknown command: {name}` with no leading marker and no pointer to help. Evidence: `src/ChatDbg/ChatShell.cs:340` vs `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:395`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.21: A range flag with a single trailing value is silently dropped — the flag is honoured only when two further arguments exist, so `--range 5` parses nothing, reports no error, and echoes the defaults. Likewise a trailing or non-numeric count flag silently leaves the default 3 in place and the bad value is then re-examined as a flag and ignored. Evidence: `src/Xcaciv.ChatDbg.Core/Commands/ShowTokenAnalysisCommand.cs:27,36-43`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.22: The end-of-range sentinel renders as a double dash — with no range flag the echo interpolates start 0 and end −1 into `range={start}-{end}`, producing the literal `range=0--1`. Evidence: `src/Xcaciv.ChatDbg.Core/Commands/ShowTokenAnalysisCommand.cs:23,46`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.23: Explicitly saving the diagnostic log can silently save almost nothing — the buffer is cleared on every threshold flush and the explicit save writes only what is in the buffer at that moment, so after any flush the "complete" log is just the tail; the explicit save also never clears, so a second save duplicates content. Evidence: `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpLogConfig.cs:154-158,180-182`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.24: The save-confirmation line can never appear in the file it describes — the sink writes the file first and only then appends `Logs saved to {path}` to its own buffer, so that line is absent from the file just written and pollutes the next one. Evidence: `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpLogConfig.cs:182,185`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.25: Native log capture is never unregistered — configuring installs a callback capturing the sink instance, while disposal only flushes and sets a flag, so a disposed sink keeps receiving native log lines into a buffer that will never be flushed again, with no way to detach. Evidence: `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpLogConfig.cs:81-105,193-200`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.26: All log-sink file failures are invisible — directory creation, buffered flush and explicit save each swallow every failure into the debug channel, so a user whose log directory is unwritable sees success everywhere and gets no file. Evidence: `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpLogConfig.cs:110-113,161-164,187-190`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.27: The documented way to turn file logging off has no user-facing control — the flag exists only as an in-process property with no settings key, no set-command name and no environment variable, and the sink is constructed with defaults inside the inference service. Evidence: `docs/LLamaSharp-Token-Introspection.md:205,260` vs `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpLogConfig.cs:25` and the absence of any binding in `src/Xcaciv.ChatDbg.Core/Models/ChatSettings.cs`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.28: The chat path and the introspection path disagree about the default context window — chat falls back to 2048 when the configured size is at or below 0 while the state snapshot written at the same moment falls back to 4096, so an analysis record can claim a 4096-token window for a context actually created at 2048. Evidence: `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:550` vs `:291-292`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.29: The consent gate cannot be answered non-interactively — the question is written to standard output and the answer read from standard input with no timeout, no default, no flag and no cancellation; a piped or redirected session reads end-of-file, is treated as a decline, and still returns a success message identical to a completed analysis. Evidence: `src/Xcaciv.ChatDbg.Core/Commands/InspectCommand.cs:63-67,86`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.30: The token table has no upper bound — every token of an arbitrarily long input is printed as its own line with no paging, no cap and no truncation, so a novel pasted into the tokenization command dumps hundreds of thousands of lines to the terminal. Evidence: `src/Xcaciv.ChatDbg.Core/Commands/TokenizeCommand.cs:79-88`; `src/Xcaciv.ChatDbg.Core/Commands/InspectCommand.cs:118-136`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.31: The multi-space normalisation is invisible but reported as fact — the command echoes the normalised text as `Input text: "{text}"`, so a user who typed two spaces or a tab sees one space and has no signal that what was tokenized differs from what they typed. Evidence: `src/Xcaciv.ChatDbg.Core/Commands/TokenizeCommand.cs:46,70`; `src/ChatDbg/ChatShell.cs:326`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-10.32: Numeric output is culture-sensitive — log-probabilities, percentages and the influence score are formatted with the ambient culture, so on a locale using a comma decimal separator the report prints `-2,50000 (8,21%)` and `(score: 0,80)` while every surrounding label stays English. Evidence: `src/Xcaciv.ChatDbg.Core/Commands/InspectCommand.cs:170,177,208`. Keep-or-fix decision deferred to Open Questions.*

---

**Source notes**

Derived from the dossier `output/chatdbg/dossiers/token-inspection.md`, itself built against the source repository `subject/chatdbg` at pinned commit `d8c18f61d6bb73666ed97cd4885e877e35558485` (branch `LLamaSharp_support`). Feature boundaries taken from `output/chatdbg/inventory.md`.

Primary evidence paths (relative to the source repository root):

- Commands: `src/Xcaciv.ChatDbg.Core/Commands/TokenizeCommand.cs`, `src/Xcaciv.ChatDbg.Core/Commands/InspectCommand.cs`, `src/Xcaciv.ChatDbg.Core/Commands/ShowTokenAnalysisCommand.cs`, `src/Xcaciv.ChatDbg.Core/Commands/ExportTokenAnalysisCommand.cs`, `src/Xcaciv.ChatDbg.Core/Commands/ExportLogsCommand.cs`, `src/Xcaciv.ChatDbg.Core/Commands/HelpCommand.cs`, `src/Xcaciv.ChatDbg.Core/Commands/LogProbsCommand.cs`, `src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs`
- Services: `src/Xcaciv.ChatDbg.Core/Services/TokenInspectionService.cs`, `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs`, `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpLogConfig.cs`, `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs`
- Data model: `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/TokenAnalysis.cs`, `.../TokenInfo.cs`, `.../TokenInspectionResult.cs`, `.../TokenProbabilityAlternative.cs`, `.../TokenProbabilityMap.cs`, `.../TokenAttribution.cs`, `.../TokenProbabilityMapResult.cs`, `src/Xcaciv.ChatDbg.Core/Models/ChatSettings.cs`
- Shells: `src/ChatDbg/ChatShell.cs`, `src/ChatDbg.Shell.Gui/Program.cs`, `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs`
- Tests: `src/Xcaciv.ChatDbg.Core.Tests/Commands/TokenizeCommandTests.cs`, `.../InspectCommandTests.cs`, `.../ShowTokenAnalysisCommandTests.cs`, `.../ExportLogsAndAnalysisCommandTests.cs`, `src/Xcaciv.ChatDbg.Core.Tests/Services/TokenInspectionServiceTests.cs`, `src/Xcaciv.ChatDbg.Core.Tests/Services/TokenInspection/TokenAnalysisTests.cs`, `.../TokenInspectionModelsTests.cs`, `.../LLamaSharpLogConfigTests.cs`
- Packaging: `src/Xcaciv.ChatDbg.Core/Xcaciv.ChatDbg.Core.csproj`, `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj`, `src/ChatDbg.Shell.Gui/Xcaciv.ChatDbg.Shell.Gui.csproj`, `global.json`
- Documentation compared against code (35 claims checked): `README.md`, `docs/LLamaSharp-Token-Introspection.md`, `IMPLEMENTATION_SUMMARY.md`

Coverage caveat carried forward from the dossier: the shipped test suite reaches only the three precondition guards on each working command, the missing-file guard on each service operation, the fixed messages of the three stubs, property access and round-trip on the analysis record family, and the log sink's defaults, buffering, clearing, explicit save and double dispose/configure. **No test ever loads a model, tokenizes anything, generates anything, or exercises the character-position estimator, the visualization builder or the attribution heuristic** — every requirement covering those paths is read from code, not pinned by a test.
