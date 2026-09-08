### 7.12 Output Rendering & Token Visualization

**Description**

This feature is the product's presentation layer. Everything the user ever sees — chat replies, command results, separators, status text, error dialogs, and above all the per-token confidence visualisations — is drawn by the components described here. Its central job is turning a flat list of *token probability records* (each one a piece of generated text, the model's log-probability for it, and an ordered list of alternatives the model considered) into something a person can scan in a terminal. Raw, that data is unreadable; rendered, it shows at a glance where a model answer was confident and where it was guessing.

Three concerns shape the design. First, **host independence**: features living in the product core must be able to emit output without knowing whether they are running inside a plain line-oriented console loop, a full-screen terminal interface, an automated test harness, or a headless script. A deliberately narrow **output surface contract** of six operations decouples them. Second, **volume control**: a model answer can run to hundreds of tokens, so the feature can render every token or a representative beginning/middle/end sample, and can cap how many alternatives are shown per token. Third, **layout choice**: a dense card *grid* for spotting patterns across many tokens, versus a row-per-token *table* for inspecting individual tokens closely.

The feature has four rendering surfaces of very different maturity: a dependency-free plain surface that writes only to standard output; a styled surface that emits inline styling directives to a rich terminal; the full-screen interface's own transcript and side panel; and a flowing background heat-map view. They were written at different times and **do not agree with each other** on colour scale, number formatting, escaping, alternative caps, or token numbering base. Those disagreements are observable behaviour and are all recorded below as requirements or as quirks — a reimplementer must decide deliberately which to keep. There is no authentication, authorization, permission model, redaction, or multi-tenancy anywhere in this feature: every message and every token is rendered verbatim to whoever is at the terminal.

---

**User stories**

- **US-12.1** — As a developer reading a model reply, I want each token coloured according to how confident the model was, so that I can spot the low-confidence regions of an answer without reading any numbers.
- **US-12.2** — As a developer, I want to switch between a dense card grid and a detailed row-per-token table, so that I can either scan many tokens at once or inspect a few of them closely.
- **US-12.3** — As a developer, I want a long token list reduced by default to a beginning / middle / end sample, and I want to be able to switch to showing every token, so that a several-hundred-token reply stays readable but nothing is permanently hidden from me.
- **US-12.4** — As a developer, I want to control how many alternative tokens are shown per token, so that I can trade visual density against detail.
- **US-12.5** — As a developer using the full-screen interface, I want a dedicated side panel that lists every token of one chosen reply together with its ranked alternatives, so that I can inspect one answer in depth while the conversation stays on screen.
- **US-12.6** — As a developer using the full-screen interface, I want a visible marker beside every reply that carries probability data, so that I can open the panel for any earlier reply and not only for the most recent one.
- **US-12.7** — As a developer, I want tokens made of invisible characters — a newline, a tab, a carriage return, a null — rendered as visible escape sequences, so that I do not mistake them for blanks or for each other.
- **US-12.8** — As a developer, I want a fixed dark colour scheme applied to the full-screen interface at start-up, so that the interface is legible in a dark terminal without my having to configure anything.
- **US-12.9** — As a developer using the full-screen interface, I want short-lived feedback for what just happened and a persistent line telling me which provider, model and prompt are in effect, so that I always know my context.
- **US-12.10** — As a developer, I want a demonstration command that renders fabricated sample probability data, so that I can see what the visualisation looks like and check my display options without configuring a provider or spending a request.
- **US-12.11** — As an in-process feature that produces user-visible output, I want a narrow contract for emitting styled lines, plain lines, blank lines, captioned separators and token renderings, so that I can be hosted by any shell without depending on it.
- **US-12.12** — As an automated test, I want the plain output surface to write through a replaceable output stream rather than a raw terminal handle, so that I can capture its output and assert on its structure.
- **US-12.13** — As a developer who asked for probability data, I want to be told explicitly when a reply came back without any, so that I do not think the feature is broken.
- **US-12.14** — As a developer, I want my display-mode, layout and alternative-count choices written to disk the moment I change them, so that they survive a restart.

---

**Use cases**

**UC-12.A — Render a model reply that carries probability data, plain console host (realizes US-12.1, US-12.3, US-12.13)**

- *Preconditions:* Probability capture is enabled. A reply has been received. The plain console host owns the terminal.
- *Main flow:*
  1. Before the request is sent, the host emits `Log probabilities enabled - requesting with top-k={N}` where N is the configured top-K value.
  2. The reply arrives carrying a non-empty token list.
  3. The host emits a blank line, then a **left-justified** separator captioned `Token Probabilities Analysis` in yellow.
  4. If the show-all-tokens option is on, or the token count is 15 or fewer, the whole list is rendered in one pass starting at index 0.
  5. Otherwise the list is rendered as three captioned slices: the caption `Beginning Tokens:` in blue followed by tokens 0–4; a blank line, the caption `Middle Tokens:` followed by 5 tokens starting at `floor(count / 2) − 2`; a blank line, the caption `End Tokens:` followed by the last 5 tokens. Each slice is numbered from its **true absolute position** in the full list.
  6. Each pass renders either the card grid or the row-per-token table, according to the grid-layout option.
  7. The host emits a blank line and a closing, uncaptioned separator.
- *Alternate flows:*
  - **A1** — Token count is exactly 15 in sample mode: no slice captions are emitted; all 15 tokens render as one pass.
  - **A2** — Token count is 16 in sample mode: the three slices are indices 0–4, 6–10 and 11–15. **Slices may overlap** for counts just above 15 and no de-duplication is performed.
  - **A3** — Show-all-tokens is on: no captions, no slicing, every token rendered.
- *Error flows:*
  - **E1** — The reply's token list is empty or absent: the host emits `Note: Log probabilities were requested but none were returned by the model.` followed by `This could be due to the model not supporting this feature or an API limitation.` and renders no visualisation.
  - **E2** — The request itself failed: the host emits `Error getting AI response: {message}` and renders no visualisation.
  - **E3** — Any unhandled failure in the host's loop: the line `Error: «message»` is emitted, detail goes to the platform debug channel only, and the loop continues.
  - **E4** — The terminal width cannot be queried (output redirected, no console attached) and grid layout is selected: the column-count calculation is guarded only against a zero result, not against a failed query; a failed query surfaces as an unhandled failure to the host's own handler.
- *Postconditions:* The visualisation has been written to the terminal. No state is changed; nothing is cached.

**UC-12.B — Render tokens as a card grid, styled surface (realizes US-12.2, US-12.4)**

- *Preconditions:* Grid layout is selected. A styled terminal surface is in use. A token list and a numbering start index are supplied.
- *Main flow:*
  1. Determine the column count: use the caller's column budget if it is greater than 0; otherwise compute `max(1, terminalWidth / 40)` by integer division.
  2. Create that many columns, all configured not to wrap.
  3. For each token in emission order, build one card and append it to the current row buffer.
  4. A card is a rounded, expanding bordered panel whose header is `#` followed by the 1-based token number (start index plus position plus one) in grey.
  5. The card body contains: `Token: «escaped token text»`; `Prob: «probability, colour-banded»`; if alternatives exist, the label `Alternatives:` followed by one line per alternative reading `- «escaped token» («probability, colour-banded»)`, limited to `min(configured max alternatives, actual count)`; and, if any were withheld, a dimmed line `+ N more`.
  6. When the row buffer is full **or** the token just processed was the last one, pad the buffer with **empty cards** up to the column count, emit the row, and clear the buffer.
  7. Render the completed grid.
- *Alternate flows:*
  - **A1** — A token has no alternatives: the `Alternatives:` block and the `+ N more` line are both omitted entirely.
  - **A2** — The caller supplies an explicit column budget greater than 0: the responsive calculation is skipped.
- *Error flows:*
  - **E1** — The token list is empty: the columns are created but no row is ever added; an empty grid is emitted rather than nothing.
  - **E2** — Terminal width query fails: see UC-12.A/E4.
  - **E3** — A colour band name not present in the styling engine's palette reaches the renderer: the styling engine raises a markup error. (INFERRED — see QUIRK-12.6.)
- *Postconditions:* One card per token has been emitted, in emission order, row-major, left to right.

**UC-12.C — Render tokens as a row-per-token table (realizes US-12.2)**

- *Preconditions:* List layout is selected. A token list and a numbering start index are supplied.
- *Main flow (plain surface):*
  1. Emit a border line, a caption row with the captions `Token #`, `Text`, `Probability`, `Top Alternatives`, and a second border line.
  2. For each token, emit one row: the 1-based number (start index plus position plus one); the escape-formatted token text truncated to 20 characters by keeping the first 17 and appending `...`; the probability as a 5-decimal percentage plus one extra literal `%`; and a compact alternatives string capped at **2** entries.
  3. Emit the closing border line.
- *Main flow (styled surface):*
  1. Emit a rounded-border, full-width-expanding table with four columns: a centred number column captioned `?`, a `Token` column of width 20, a centred `Probability` column, and a `Top Alternatives` column of width 50.
  2. Per token: the 1-based number in grey; the escaped token text; the colour-banded probability; and up to **3** alternatives, one per line, each reading `«escaped token» («colour-banded probability»)`.
- *Alternate flows:*
  - **A1** — A token has no alternatives: the plain surface writes the literal `(none)`; the styled surface writes a dimmed `(none)`.
  - **A2** — A cell's content is wider than its column: the plain surface **overflows and breaks table alignment** (there is no clipping on the probability or alternatives cells); the styled surface wraps or clips per the styling engine's own rules.
- *Error flows:*
  - **E1** — The token list is **empty** on the plain surface: four lines are still emitted (border, caption row, border, closing border) — an empty frame around nothing, not an absence of output.
  - **E2** — The token list is **null** on the plain surface: the call fails; the count is read without a guard and the contract promises nothing.
  - **E3** — A token's text is null: the plain surface renders the literal `(null)`.
- *Postconditions:* One row per token, in emission order.

**UC-12.D — Open and render the token probability panel, full-screen host (realizes US-12.5, US-12.6)**

- *Preconditions:* The full-screen interface is running. At least one assistant message in the transcript carries a non-empty probability list.
- *Main flow:*
  1. The user activates the probability indicator `◊` shown beneath an assistant message, or selects the panel toggle from the menu.
  2. If the panel is closed it is opened, docked to the right of the transcript; the transcript is shrunk to **60%** of the window width and the panel takes the remaining 40%.
  3. The panel is pointed at the message: the indicator points it at *that* message; the menu toggle points it at the most recent message carrying data.
  4. The panel content is torn down and rebuilt: a header line reading `[«message timestamp»] Token Probabilities:` followed by one blank line.
  5. For each token in order: a line at indent 2 reading `«0-based index»: "«escaped token»" («probability as a 5-decimal percentage»)`, painted with the 10-bucket confidence colour map.
  6. For each of that token's alternatives, **sorted descending by probability** and limited to the configured grid-max-alternatives value: a line at indent 4 reading `Alt: "«escaped token»" («probability as a 5-decimal percentage»)`, also colour-mapped. This is the only place in the product that re-orders alternatives.
  7. A blank line closes each token's block.
  8. The panel's content width is set to `max(longest rendered line + 5, 50)`; the panel is scrolled to the top; the screen is refreshed.
  9. The status line shows `Showing token probabilities for message at «timestamp»` when opened by the indicator, or `Token probabilities panel enabled` when opened by the menu toggle.
- *Alternate flows:*
  - **A1** — At start-up: the panel opens automatically only if probability capture is enabled **and** at least one assistant message already carries data; the most recent such message is selected.
  - **A2** — A new reply arrives carrying probability data while the panel is closed: the panel opens automatically and is pointed at the new message.
  - **A3** — The panel is already open and the toggle is used: the panel is removed, the transcript returns to full width, and the status line reads `Token probabilities panel disabled`.
  - **A4** — Probability capture is toggled off from the menu: the setting is flipped and persisted, the transcript is rebuilt, the panel is closed to match, and the status reads `Log probabilities display disabled` (or `Log probabilities display enabled` for the reverse).
  - **A5** — Every token of the selected message is rendered; the panel performs **no sampling at all** and relies on scrolling.
- *Error flows:*
  - **E1** — The panel is toggled while no assistant message carries probability data: a modal dialog titled `No Log Probabilities` with body `There are no assistant messages with log probabilities to display.` and a single `OK` button appears; nothing else changes.
  - **E2** — The panel is pointed at a message with no probability data: the single line `No token probability data available.` is added and the routine **returns immediately**, skipping the content-size update, the scroll-to-top reset and the screen refresh.
  - **E3** — A token's text is null: the panel dereferences it without a guard and the draw fails.
  - **E4** — The panel is open and the history is then cleared: the toggle raises the `No Log Probabilities` dialog and returns, leaving the panel open with no way to dismiss it.
- *Postconditions:* The panel shows one block per token of the selected message, scrolled to the top; the transcript occupies 60% of the width.

**UC-12.E — Render the conversation transcript, full-screen host**

- *Preconditions:* The full-screen interface is running and the transcript viewport has been assigned a width of at least 6 columns.
- *Main flow:*
  1. The transcript area is torn down and rebuilt from scratch.
  2. Left padding is 2 columns; usable width is the viewport width minus 4; the wrap limit is **three quarters** of the usable width.
  3. For each message: a role caption line reading the role name lower-cased inside square brackets, followed by the wrapped content lines.
  4. Content is wrapped by splitting on explicit newlines first, then greedily breaking each long line at the **last space** within the wrap limit — the space is kept at the end of the emitted line — or, when no space is available, by a hard cut at the limit.
  5. Blank or whitespace-only wrapped lines are skipped.
  6. Messages whose role is `user` are right-aligned; every other role is left-aligned at the padding offset.
  7. Role colour schemes are applied: user is white on dark grey; assistant is bright yellow on blue; system is green on black; any unrecognised role falls back to the system scheme.
  8. An assistant message carrying probability data gets a focusable probability indicator `◊` appended below it, bright green on black, and bright green on dark grey when focused.
  9. One blank line separates messages.
  10. The scrollable content height is set to the greater of the used height and the viewport height, and the view is auto-scrolled to the bottom.
- *Alternate flows:*
  - **A1** — Role stored in mixed case (for example `Assistant`, reachable through imported history): the transcript lower-cases the role, so colours and the `◊` indicator work; the panel-eligibility queries compare the raw string, so they do not. See QUIRK-12.19.
- *Error flows:*
  - **E1** — Transcript viewport width is 4 or 5 columns: the wrap limit computes to 0, the wrapper appends an empty string and advances its cursor by 0, and the loop **never terminates** — the interface stops responding and memory grows without bound. No error, no message.
  - **E2** — Transcript viewport width is 0 to 3 columns (including the pre-layout state, where the limit is −3): the substring operation receives a negative length and **fails**, surfaced through a modal dialog titled `Error` carrying the failure message.
  - **E3** — Any other failure while sending or rendering: a modal dialog titled `Error` carrying the message; the transcript is still rebuilt afterwards.
- *Postconditions:* The transcript reflects the full message history; nothing is cached between rebuilds.

**UC-12.F — Apply the interface theme at start-up (realizes US-12.8)**

- *Preconditions:* The full-screen interface toolkit has just been initialised.
- *Main flow:*
  1. The single, argument-free theme operation is invoked unconditionally.
  2. Four global colour-scheme slots are replaced wholesale: base, dialog, menu and error, each with normal, focus, hot-normal and hot-focus attributes.
  3. Control returns; the window is constructed.
- *Alternate flows:* None. There is no light theme, no theme setting, no persisted theme choice and no runtime way to change it.
- *Error flows:* None handled. The base, dialog and error slots are left with no disabled attribute at all, which may render disabled controls as black on black. (INFERRED.)
- *Postconditions:* Normal text is white on black; focused elements are bright yellow on dark grey; menus are white on dark grey; error text is bright red on black.

**UC-12.G — Change a display option (realizes US-12.4, US-12.14)**

- *Preconditions:* A shell is running with loaded settings.
- *Main flow (command surface):*
  1. The user issues a display-option subcommand.
  2. The value is validated against its range.
  3. The setting is updated and **persisted immediately**.
  4. A confirmation line is emitted (exact strings in FR-12.110).
  5. The next render reads the new value.
- *Main flow (dialog surface, full-screen host only):*
  1. The user edits the display-mode radio, layout radio, or the numeric alternative-count fields.
  2. On acceptance the numeric values are **silently clamped** into 1–20; no message is shown.
  3. Settings are persisted.
- *Alternate flows:*
  - **A1** — The subcommand is issued with no argument: a status block is emitted listing the current values (FR-12.111).
- *Error flows:*
  - **E1** — Out-of-range value through the command surface: `Grid max alternatives value must be a number between 1 and 20` or `Top-K value must be a number between 1 and 20`; the stored value is unchanged.
  - **E2** — Missing numeric argument: `Please specify a number: /logprobs gridmaxalt <number>` or `Please specify a number: /logprobs top <number>`.
  - **E3** — Non-boolean value through the generic setting-assignment surface: `showAllTokens must be 'true' or 'false'`, `gridViewForTokens must be 'true' or 'false'`, or `gridViewMaxAlternatives must be a number between 1 and 20`.
  - **E4** — Unknown subcommand: `Unknown subcommand: {name}.` followed by `Valid options are:` and six bullet lines.
  - **E5** — Any other failure while configuring: `Error configuring log probabilities: {message}`.
  - **E6** — The option is changed from inside the **full-screen host**: the command reports success but nothing on screen changes, and the stored settings file is overwritten with process-start defaults plus the single changed field. See QUIRK-12.18.
- *Postconditions:* The settings file has been rewritten. In the plain host the next render honours the change.

**UC-12.H — Render the demonstration visualisation (realizes US-12.10)**

- *Preconditions:* The demonstration command is invoked.
- *Main flow:*
  1. Build the fixed sample sentence and a deterministic sample token list from a pseudo-random generator seeded with **42**.
  2. Emit `Sample Text:` in yellow (preceded by an embedded newline), then the sample text, then a blank line.
  3. Emit `Display Mode: All Tokens` or `Display Mode: Sample Tokens`, and `Layout: Grid View` or `Layout: List View`, in blue.
  4. In grid layout only, also emit `Grid Max Alternatives: {n}`.
  5. Emit a blank line, then a left-justified separator captioned `Token Probabilities Analysis`.
  6. Render the sampled token list as grid or table.
  7. Emit a closing uncaptioned separator and a blank line.
  8. Return success with the message `Sample token probability analysis generated`.
- *Alternate flows:*
  - **A1** — No output surface was supplied to the command: nothing is drawn at all; the command still returns success with the same message and still exposes its generated sample data.
  - **A2** — Sample mode (the default): the 25-token sentence exceeds the threshold of 15, so the three slices are **concatenated into one list** and rendered as a single pass numbered from 0 — no slice captions, and the middle and end slices lose their true positions.
- *Error flows:*
  - **E1** — The token list is null: the surface emits `No token probability data available` in red; the command still reports success.
- *Postconditions:* A demonstration visualisation has been drawn (or not, per A1). No persisted state changes.

**UC-12.I — Emit a markup line or a captioned separator through the plain surface (realizes US-12.11, US-12.12)**

- *Preconditions:* A caller holds the output surface contract; the plain implementation is in use.
- *Main flow (markup line):*
  1. The caller passes one string that may contain inline styling directives delimited by square brackets.
  2. Every bracketed span is stripped: a `[` opens "in-markup" and is dropped; the next `]` closes it and is dropped; everything between them is dropped.
  3. The remainder is written as one line through the **process-wide standard-output writer**, which must be replaceable.
- *Main flow (separator):*
  1. The caller optionally supplies a title and a left-justify flag; the default is centred.
  2. The title has markup stripped before it is measured and printed.
  3. With no title, 80 dash characters are emitted.
  4. Left-justified: title, one space, then `80 − (title length + 2)` dashes — a total of **79** characters.
  5. Centred: `floor(remaining / 2)` dashes, a space, the title, a space, then the remaining dashes — a total of exactly **80** characters, with the odd character going to the right side.
- *Alternate flows:*
  - **A1** — A `]` appears while not inside a bracketed span: it is **kept**.
- *Error flows:*
  - **E1** — An unclosed `[`: the rest of the line is silently swallowed.
  - **E2** — A separator title longer than 78 characters: the dash count goes negative and the call fails outright rather than truncating gracefully. (INFERRED.)
  - **E3** — A separator caption containing unbalanced brackets on the **styled** surface: the styling engine parses it as markup and raises a markup error, where the plain surface would silently strip it.
- *Postconditions:* One line has been written.

**UC-12.J — Show and expire a transient status message (realizes US-12.9)**

- *Preconditions:* The full-screen interface is running with a one-line status label above the status bar.
- *Main flow:*
  1. A caller sets the status text; the screen is refreshed immediately.
  2. A 3000-millisecond timer starts on a background scheduler.
  3. On expiry the callback marshals back to the interface thread and resets the label to the idle text `Provider: «provider» | Model: «model» | Prompt: «prompt name»`.
- *Alternate flows:*
  - **A1** — A successful command result is surfaced as `✓ «message»`.
- *Error flows:*
  - **E1** — A failed command result opens a modal dialog titled `Command Error` carrying the command's message rather than using the status line.
  - **E2** — An unknown command opens a modal dialog titled `Error` with body `Unknown command: «name»`.
  - **E3** — An unimplemented menu action sets the status text `System messages toggle not yet implemented`.
  - **E4** — Timers are **never cancelled**, so an older timer can wipe a newer status message before its own 3 seconds have elapsed.
- *Postconditions:* The status label eventually returns to the idle text.

---

**Functional requirements**

*The output surface contract*

- **FR-12.1** — The system SHALL expose exactly six output operations to in-process callers: write markup line, write plain line, write blank line, write separator, display token grid, display token table. (realizes US-12.11)
- **FR-12.2** — The write-separator operation SHALL accept an optional title string (which may contain markup) and an optional left-justify flag whose default is **false**, meaning centred. (realizes US-12.11)
- **FR-12.3** — The display-token-grid operation SHALL accept a token list, a numbering start index defaulting to **0**, a maximum column count defaulting to **0** (meaning automatic/responsive), and a maximum alternatives-per-token count defaulting to **3**. (realizes US-12.11)
- **FR-12.4** — The display-token-table operation SHALL accept a token list and a numbering start index defaulting to **0**. (realizes US-12.11)
- **FR-12.5** — Numbering start indexes SHALL exist so a caller can render a slice of a longer list while preserving each token's true position in the full list. (realizes US-12.3)
- **FR-12.6** — The contract SHALL be substitutable by a test double, and the plain implementation SHALL write through a replaceable standard-output writer rather than a raw terminal handle. (realizes US-12.12)
- **FR-12.7** — The system SHALL provide at least two concrete implementations of the contract: a dependency-free plain implementation and a styled-terminal implementation. There SHALL be no dependency-injection container; the concrete implementation is constructed by the host and hand-passed to callers.

*Plain surface — markup, separators*

- **FR-12.8** — On the plain surface, writing a markup line SHALL strip every bracketed span and write the remainder verbatim. A `[` opens a span and is dropped; the next `]` closes it and is dropped; all characters between them are dropped. (realizes US-12.11)
- **FR-12.9** — On the plain surface, a `]` encountered while not inside a bracketed span SHALL be preserved in the output.
- **FR-12.10** — On the plain surface, an unclosed `[` SHALL cause the remainder of the line to be discarded silently.
- **FR-12.11** — On the plain surface, write-plain-line and write-blank-line SHALL pass through with no styling interpretation.
- **FR-12.12** — The plain separator width SHALL be fixed at **80** characters. With no title (null or empty) exactly 80 dash characters SHALL be emitted.
- **FR-12.13** — A plain separator title SHALL consume a padding budget of **+2** (one space on each side), so the dash budget is `80 − (title length + 2)`.
- **FR-12.14** — A left-justified plain separator SHALL emit title, one space, then the dash budget — a total line length of **79** characters, one short of the centred form.
- **FR-12.15** — A centred plain separator SHALL emit `floor(remaining / 2)` dashes, a space, the title, a space, then `remaining − left` dashes — a total line length of exactly **80** characters, with the odd character on the right.
- **FR-12.16** — A plain separator title SHALL have markup stripped before it is measured and before it is printed.

*Plain surface — token table*

- **FR-12.17** — The plain token-grid operation SHALL ignore both the column budget and the alternatives budget and delegate to the token table. It does not draw a grid. (see QUIRK-12.9)
- **FR-12.18** — The plain token table SHALL emit a border line, a caption row with the captions `Token #`, `Text`, `Probability`, `Top Alternatives`, a second border line, one row per token, and a closing border line — in that order. (realizes US-12.2)
- **FR-12.19** — Plain-table column inner widths SHALL be **7**, **20**, **12** and **38**, with border segments of **9**, **22**, **14** and **40** (one space of padding on each side). The total line width is 90 characters.
- **FR-12.20** — The plain-table row number SHALL be `startIndex + positionInList + 1` — 1-based, offset by the caller's start index. (realizes US-12.3)
- **FR-12.21** — Plain-table token text SHALL be escape-formatted, then truncated when longer than **20** characters by keeping the first **17** characters and appending `...`.
- **FR-12.22** — Plain-table probability SHALL be rendered as a percentage with **5** decimal places, to which an additional literal `%` is appended. (see QUIRK-12.2)
- **FR-12.23** — Plain-table alternatives SHALL be capped at **2** entries, hard-coded; the caller's alternatives budget never reaches this path.
- **FR-12.24** — The plain compact alternatives string SHALL be the literal `(none)` for a null or empty list; otherwise up to the cap, joined by `, `, each formatted `«token» («5-decimal percentage»%)`, with each alternative token escape-formatted and truncated when longer than **10** characters to the first **7** plus `...`; when more alternatives exist than shown, the string SHALL end with ` (+N more)` — **no space after the plus**.
- **FR-12.25** — Plain-table cells whose content exceeds their column width SHALL overflow, breaking table alignment. No clipping is applied to the probability or alternatives cells.

*Styled surface — grid and table*

- **FR-12.26** — On the styled surface, markup lines, plain lines and blank lines SHALL be delegated to the styled console, which interprets bracketed style tags.
- **FR-12.27** — On the styled surface, a separator SHALL be captioned with the supplied title, or with an empty caption when no title is given, left-justified on request and otherwise at the styling engine's default justification.
- **FR-12.28** — The styled grid column count SHALL be the caller's column budget when greater than 0, and otherwise `max(1, terminalWidth / 40)` using integer division — **40** being the assumed minimum card width. (realizes US-12.2)
- **FR-12.29** — Every styled grid column SHALL be created with wrapping disabled.
- **FR-12.30** — Styled grid tokens SHALL be laid out row-major, left to right, one card per token, in emission order.
- **FR-12.31** — A styled grid row SHALL be flushed when the row buffer is full **or** when the last token has been processed; a partially filled final row SHALL be padded with **empty cards** so the grid stays rectangular.
- **FR-12.32** — A styled token card SHALL be a rounded-border, expanding panel whose header is the 1-based token number prefixed with `#` in grey, and whose body contains, in order: `Token: «escaped token»`; `Prob: «colour-banded percentage»`; and, when alternatives exist, `Alternatives:` followed by one line per alternative reading `- «escaped token» («colour-banded percentage»)`. (realizes US-12.1, US-12.4)
- **FR-12.33** — A styled card SHALL show `min(configured max alternatives, actual alternative count)` alternatives and, when any were withheld, a dimmed line reading `+ N more` (with a space after the plus). (realizes US-12.4)
- **FR-12.34** — A styled card SHALL omit the alternatives block entirely when the token has no alternatives.
- **FR-12.35** — The styled token table SHALL have a rounded border, expand to full width, and carry four columns: a centred number column captioned `?`, a `Token` column of width **20**, a centred `Probability` column, and a `Top Alternatives` column of width **50**.
- **FR-12.36** — The styled table row number SHALL be rendered in grey, 1-based, offset by the caller's start index.
- **FR-12.37** — The styled table alternatives cell SHALL show at most **3** entries, hard-coded, one per line, each formatted `«escaped token» («colour-banded percentage»)`; the configured max-alternatives value does **not** apply to table layout. An empty list SHALL render as a dimmed `(none)`.
- **FR-12.38** — On the styled surface, token text SHALL be escape-formatted for control characters **first**, then have its bracket characters doubled so the styling engine treats them as literals.

*Confidence colour mapping*

- **FR-12.39** — The system SHALL provide a shared 5-band colour-band map whose bands are: value ≥ 90 → `green`; ≥ 70 → `lime`; ≥ 50 → `yellow`; ≥ 30 → `orange`; otherwise → `red`. The map is fed a **0…100** scale. (realizes US-12.1)
- **FR-12.40** — The host shells SHALL each carry their own copy of the 5-band map with byte-identical thresholds but the 30–50 band named `orange3` instead of `orange`. (see QUIRK-12.6, QUIRK-12.12)
- **FR-12.41** — The full-screen probability panel SHALL use a 10-bucket map whose bucket index is `clamp(floor(probability × 10), 0, 9)` on a **0…1** scale, painted as a foreground colour over a black background: bucket 0 bright red; 1 red; 2 bright magenta; 3 magenta; 4 bright blue; 5 blue; 6 cyan; 7 bright cyan; 8 bright yellow; 9 bright green. Clamping means values at or above 1.0 all land in bucket 9 and negative values in bucket 0. (realizes US-12.1, US-12.5)
- **FR-12.42** — The flowing heat-map view SHALL use a 6-band map on a **0…1** scale applied as the *background* colour: ≥ 0.9 green; ≥ 0.7 bright green; ≥ 0.5 brown; ≥ 0.3 bright yellow; ≥ 0.1 red; otherwise bright red. The foreground SHALL be black when probability is ≥ 0.5 and white below, chosen for contrast.
- **FR-12.43** — Only one of the four colour maps is reachable in normal operation: the 10-bucket panel map. The 6-band map belongs to a view that is never constructed, and the shared 5-band map is fed a 0…1 value so only its fallback band is ever selected. (see QUIRK-12.1, QUIRK-12.28)

*Sampling of long token lists*

- **FR-12.44** — The default sampling rule SHALL use a slice size of **5**. When the token count is **15 or fewer** (`5 × 3`), all tokens SHALL be shown with no slicing. (realizes US-12.3)
- **FR-12.45** — Otherwise three slices SHALL be shown: the first 5 tokens; 5 tokens starting at `floor(count / 2) − 2` using integer arithmetic; and the last 5 tokens (starting at `count − 5`). (realizes US-12.3)
- **FR-12.46** — Slices MAY overlap for counts just above 15 (a 16-token list yields indices 0–4, 6–10 and 11–15) and no de-duplication SHALL be performed.
- **FR-12.47** — In the shells the three slices SHALL be announced by the blue captions `Beginning Tokens:`, `Middle Tokens:` and `End Tokens:`, with a blank line before the middle and end captions, and each slice SHALL be numbered from its true absolute index.
- **FR-12.48** — In the demonstration command the three slices SHALL be concatenated into one list and rendered as a single pass numbered from 0 — no captions, and the middle and end slices lose their true positions. (see QUIRK-12.12)
- **FR-12.49** — The flowing heat-map view SHALL use a separate rule: slice size **10**, threshold **30 or fewer** shows all, and a middle slice starting at `floor((count − 10) / 2)` — a different formula that is off-centre by 5 relative to the default rule.
- **FR-12.50** — The full-screen probability panel SHALL apply **no sampling**: it renders every token of the selected message and relies on scrolling. (realizes US-12.5)
- **FR-12.51** — When the token list supplied to the sampling helper is null or empty, the helper SHALL return nothing and the caller SHALL emit `No token probability data available`.

*Token text escaping*

- **FR-12.52** — The shared plain escape rule SHALL render a null token as the literal `(null)`, and replace newline with `\n`, carriage return with `\r`, tab with `\t` and NUL with `\0` as literal two-character sequences. (realizes US-12.7)
- **FR-12.53** — The styled escape rule SHALL render a null token as a dimmed `(null)` and replace each control character with a **dimmed** literal escape sequence. (realizes US-12.7)
- **FR-12.54** — The flowing heat-map view SHALL escape newline and carriage return only; tab and NUL SHALL NOT be escaped there.
- **FR-12.55** — The full-screen probability panel SHALL escape newline, carriage return and tab; NUL SHALL NOT be escaped there.
- **FR-12.56** — Bracket characters in token text SHALL be doubled before styled output so they display literally. The plain markup stripper is **not** the inverse of this escaper: doubled brackets are mangled by it.

*Probability number formatting*

- **FR-12.57** — The shared value formatter SHALL emit a fixed-point number with **2** decimal places followed by a single literal `%` (for example, the input 42.1234 produces exactly `42.12%`).
- **FR-12.58** — The plain surface, the shell-local visualisers and the shared text report SHALL emit a percentage with **5** decimal places to which an additional literal `%` is appended, producing a doubled percent sign. (see QUIRK-12.2)
- **FR-12.59** — The full-screen probability panel SHALL emit a percentage with **5** decimal places and **no** extra `%` — the only correct formatter in the product.
- **FR-12.60** — The percentage pattern is culture-sensitive: under the invariant culture a space precedes the sign (`50.00000 %`), under US English it does not (`50.00000%`). Both shell binaries force invariant globalization in their compact and single-file build configurations, so packaged builds produce the spaced form. A reimplementation SHOULD pin a culture explicitly if exact output matters.
- **FR-12.61** — The probability rendered for a token SHALL be `e^(log probability)`, computed at read time and never persisted.

*Shared text report and compact description (defined; not reached by any current interface)*

- **FR-12.62** — The system SHALL provide a shared plain-text token report producing, in order: the line `=== Token Probabilities Analysis ===`; a blank line; a pipe-delimited caption row `| # | Token          | Probability | Alternatives                |`; a dashed separator row; one row per token; a blank line; and a closing line of **39** equals signs.
- **FR-12.63** — Each report row SHALL contain the 1-based index right-aligned in 2, the token text left-aligned in 14 and **not** escape-formatted (the only place in the product a token is printed unescaped), the probability right-aligned in 10 with 5 decimals plus a literal `%`, and the alternatives cell left-aligned in 24.
- **FR-12.64** — The report's alternatives cell SHALL contain the first **2** alternatives joined by `, ` as `«raw token» («5-decimal percentage»%)`, or the literal `none` when there are none.
- **FR-12.65** — The system SHALL provide a shared compact alternatives description: `(none)` for an empty or absent list; otherwise up to a caller-supplied cap defaulting to **3**, joined by `, `, each `«escaped token» («5-decimal percentage»%)`; when more exist, the string ends with ` (+ N more)` — **with** a space after the plus, which differs from the plain surface's form.

*Full-screen transcript rendering*

- **FR-12.66** — The transcript SHALL be torn down and rebuilt from scratch on every refresh — on every message, every command and every panel toggle. Nothing is cached; colour schemes and the 10 heat buckets are the only objects built once and reused.
- **FR-12.67** — The transcript SHALL use a left padding of **2** columns, a usable width of `viewport width − 4`, and a wrap limit of **three quarters** of the usable width.
- **FR-12.68** — Each message SHALL emit a role caption line reading the role name lower-cased inside square brackets, followed by the wrapped content lines, followed by one blank separator line.
- **FR-12.69** — Content wrapping SHALL split on explicit newlines first, then greedily break each long line at the **last space** within the limit — keeping that space at the end of the emitted line — and hard-cut at the limit when no space is available. Blank and whitespace-only wrapped lines SHALL be skipped.
- **FR-12.70** — Messages whose role is `user` SHALL be right-aligned; every other role SHALL be left-aligned at the padding offset.
- **FR-12.71** — Role colour schemes SHALL be: user white on dark grey; assistant bright yellow on blue; system green on black; any unrecognised role falls back to the system scheme. A "divider" scheme of grey on black is defined but never used.
- **FR-12.72** — An assistant message carrying a non-empty probability list SHALL have a focusable probability indicator `◊` appended below it — bright green on black, and bright green on dark grey when focused. (realizes US-12.6)
- **FR-12.73** — Activating the probability indicator SHALL open the panel if closed, shrink the transcript to **60%** width, point the panel at that message, re-render the panel, scroll the panel to the top, and show the status message `Showing token probabilities for message at «timestamp»`. (realizes US-12.6)
- **FR-12.74** — After a rebuild, the scrollable content height SHALL be the greater of the used height and the viewport height, and the view SHALL auto-scroll to the bottom.

*Full-screen probability panel*

- **FR-12.75** — The panel SHALL be framed and captioned `Token Probabilities`, docked to the right of the transcript, of the same height, with a default content width of **50** columns. (realizes US-12.5)
- **FR-12.76** — With no probability data on the selected message, the panel SHALL show the single line `No token probability data available.` and stop.
- **FR-12.77** — Otherwise the panel SHALL emit a header line `[«message timestamp»] Token Probabilities:` followed by one blank line.
- **FR-12.78** — Per token the panel SHALL emit, at indent **2**, a line reading `«0-based index»: "«escaped token»" («5-decimal percentage»)`, painted with the 10-bucket colour map. This is the **only** surface in the product that numbers tokens from 0; every other surface numbers from 1.
- **FR-12.79** — Per alternative the panel SHALL emit, at indent **4**, a line reading `Alt: "«escaped token»" («5-decimal percentage»)`, also colour-mapped, with alternatives **sorted descending by probability** and limited to the configured grid-max-alternatives value. This is the only place in the product that re-orders alternatives. (realizes US-12.4, US-12.5)
- **FR-12.80** — One blank line SHALL follow each token's block.
- **FR-12.81** — The panel content width SHALL be tracked as the longest rendered line plus **2** for token lines and plus **4** for alternative lines, and the final content width SHALL be `max(longest + 5, 50)`.
- **FR-12.82** — The panel SHALL scroll to the top after every successful refresh.

*Panel visibility state*

- **FR-12.83** — At start-up the panel SHALL be shown only if probability capture is enabled **and** at least one assistant message already carries probability data; the most recent such message SHALL be selected. (realizes US-12.5)
- **FR-12.84** — Toggling the panel when no assistant message carries probability data SHALL raise a modal error dialog titled `No Log Probabilities` with the body `There are no assistant messages with log probabilities to display.` and a single `OK` button, and SHALL change nothing else.
- **FR-12.85** — Showing the panel SHALL set the transcript width to **60%** of the window; hiding it SHALL restore full width.
- **FR-12.86** — Toggle status messages SHALL be `Token probabilities panel enabled` and `Token probabilities panel disabled`.
- **FR-12.87** — The menu item that toggles probability capture SHALL flip the enable setting, persist settings, rebuild the transcript, open or close the panel to match, and report `Log probabilities display enabled` or `Log probabilities display disabled`.
- **FR-12.88** — When a new reply arrives carrying probability data and the panel is closed, the panel SHALL be auto-opened and pointed at the new message. (realizes US-12.5)

*Status line and static chrome*

- **FR-12.89** — The full-screen host SHALL show a one-line status label above the status bar whose idle text is `Provider: «provider» | Model: «model» | Prompt: «prompt name»`. (realizes US-12.9)
- **FR-12.90** — A transient status message SHALL replace the idle text and revert to it after **3000 milliseconds**. Timers SHALL NOT be cancelled when a newer message arrives, so an older timer can clear a newer message early. (realizes US-12.9)
- **FR-12.91** — A successful command result SHALL be surfaced as `✓ «message»`; a failed one SHALL open a modal error dialog titled `Command Error`. In the plain host, results SHALL be rendered as `✓ «message»` on success and `✗ «message»` on failure.
- **FR-12.92** — The full-screen chrome SHALL consist of a menu bar with the top-level items `File`, `Edit`, `View`, `Tools`, `Help`; a transcript frame captioned `Chat History` filling the area below the menu bar down to **5** rows above the bottom; an `Input` frame anchored 5 rows from the bottom containing a one-line editor and a `Send` button; and a status bar offering `F1 Help` and `F10 Quit`.
- **FR-12.93** — The help modal SHALL list every command **sorted by name** as `/«name»` plus an indented description.
- **FR-12.94** — The initial scroll canvases SHALL be **80 × 1000** for the transcript and **50 × 1000** for the panel; both are placeholders replaced after the first render.

*Theming*

- **FR-12.95** — The system SHALL expose exactly one theming operation — apply the dark theme — taking no arguments, returning nothing, invoked unconditionally once at full-screen start-up immediately after the interface toolkit is initialised, with no way to undo it. (realizes US-12.8)
- **FR-12.96** — The dark theme SHALL replace four global scheme slots wholesale, as follows. (realizes US-12.8)

  | Slot | Normal | Focus | Hot-normal | Hot-focus | Disabled |
  |---|---|---|---|---|---|
  | Base | white / black | bright yellow / dark grey | bright cyan / black | bright yellow / dark grey | *(not set)* |
  | Dialog | white / dark grey | bright yellow / dark grey | bright cyan / dark grey | bright yellow / dark grey | *(not set)* |
  | Menu | white / dark grey | bright yellow / black | bright cyan / dark grey | bright yellow / black | grey / dark grey |
  | Error | bright red / black | bright red / dark grey | bright red / black | bright yellow / dark grey | *(not set)* |

- **FR-12.97** — There SHALL be no light theme, no theme setting, no persisted theme choice and no runtime theme switch.

*Flowing heat-map view (defined; never constructed)*

- **FR-12.98** — The system SHALL define a focusable view that paints tokens as a continuous flowing paragraph, each token's **background** coloured by its probability (FR-12.42), honouring the show-all-tokens setting with the 10/10/10 sampling rule (FR-12.49), and adopting the base colour scheme as its own default.
- **FR-12.99** — That view SHALL lay tokens left to right; when the next token would exceed the view width the cursor moves to the next row and restarts at column 0; drawing SHALL stop entirely once the row index reaches the view height, with no scrolling, no ellipsis and no indicator. The drawing attribute SHALL be reset to the view's normal colour before and after the loop.
- **FR-12.100** — No code path in the shipped product constructs this view. (see QUIRK-12.13)

*Settings that drive rendering (consumed, not owned)*

- **FR-12.101** — Rendering SHALL be driven by five persisted settings read fresh on every render, with the exact stored key names, types, defaults and ranges below. (realizes US-12.2, US-12.3, US-12.4, US-12.14)

  | Setting | Stored key | Type | Default | Valid range | Effect on rendering |
  |---|---|---|---|---|---|
  | Show all tokens | `showAllTokens` | boolean | **false** (sample mode) | true / false | all tokens vs. beginning/middle/end sampling |
  | Grid layout for tokens | `gridViewForTokens` | boolean | **false** (list mode) | true / false | card grid vs. row-per-token table |
  | Grid max alternatives | `gridViewMaxAlternatives` | integer | **5** | **1–20** | alternatives per card before `+ N more`; also caps alternatives in the full-screen panel |
  | Enable token log probabilities | `enableLogProbabilities` | boolean | **false** | true / false | gates whether any token visualisation appears at all |
  | Top-K alternatives | `logProbabilitiesTopK` | integer | **5** | **1–20** | how many alternatives are requested upstream, bounding what can be drawn |

- **FR-12.102** — Display settings SHALL be stored at `«user profile directory»/.ChatDbg/settings.json`, with a temporary directory used as a fallback when the user-profile directory cannot be used. The directory name is the literal `.ChatDbg`. (realizes US-12.14)
- **FR-12.103** — Every mutation of a display setting SHALL persist the settings file immediately. (realizes US-12.14)
- **FR-12.104** — The computed probability field SHALL be excluded from persistence, along with all transient render state.
- **FR-12.105** — No rendering behaviour SHALL read any environment variable. There are no environment variables in this feature.
- **FR-12.106** — Out-of-range values SHALL be **rejected with a message** on the command surface but **silently clamped** into 1–20 by the settings dialog. These two entry points have deliberately different validation semantics.

*Exact user-visible strings*

- **FR-12.107** — The plain host SHALL emit `Log probabilities enabled - requesting with top-k={N}` before every request while probability capture is on. (realizes US-12.13)
- **FR-12.108** — When a reply carries an empty or absent probability list, the plain host SHALL emit `Note: Log probabilities were requested but none were returned by the model.` followed by `This could be due to the model not supporting this feature or an API limitation.` (realizes US-12.13)
- **FR-12.109** — When a request fails, the plain host SHALL emit `Error getting AI response: {message}`.
- **FR-12.110** — The display-options command SHALL emit exactly the following confirmations. (realizes US-12.2, US-12.3, US-12.4)

  | Subcommand | Message(s) |
  |---|---|
  | `enable` | `Token probability analysis enabled.` then `Note: This feature requires a compatible model and API version.` then `If you don't see probabilities after responses, try '/logprobs debug'.` then `You can see a demonstration with the '/demologprobs' command.` (four lines) |
  | `disable` | `Token probability analysis disabled.` |
  | `top <n>` | `Token probability analysis will show top {n} alternatives.` |
  | `showall` | `Token probability analysis will show all tokens.` |
  | `showsample` | `Token probability analysis will show token samples (beginning, middle, end).` |
  | `grid` | `Token probability analysis will use grid view layout.` |
  | `list` | `Token probability analysis will use list view layout.` |
  | `gridmaxalt <n>` | `Grid view will show up to {n} alternatives per token.` |

- **FR-12.111** — With no argument, the display-options command SHALL emit a status block headed `Token Probability Analysis Settings:` listing `- Enabled: Yes|No`, `- Top-K Alternatives: {n}`, `- Display Mode: Show all tokens` or `Show token samples (beginning, middle, end)`, `- View Mode: Grid layout` or `List layout`, `- Grid View Max Alternatives: {n}`, followed by a `Usage:` block.
- **FR-12.112** — The display-options command's error strings SHALL be exactly: `Please specify a number: /logprobs top <number>`, `Top-K value must be a number between 1 and 20`, `Please specify a number: /logprobs gridmaxalt <number>`, `Grid max alternatives value must be a number between 1 and 20`, `Error configuring log probabilities: {message}`, and for an unrecognised subcommand `Unknown subcommand: {name}.` followed by `Valid options are:` and six bullet lines.
- **FR-12.113** — The generic setting-assignment command SHALL reject with `gridViewMaxAlternatives must be a number between 1 and 20`, `showAllTokens must be 'true' or 'false'`, and `gridViewForTokens must be 'true' or 'false'`.
- **FR-12.114** — The display-options command SHALL advertise the usage line `/logprobs [enable|disable|top <number>|showall|showsample|grid|list|gridmaxalt <number>|debug] - Configure token probability analysis settings`.
- **FR-12.115** — The demonstration command SHALL advertise `/demologprobs - Display sample token probability analysis` and be described as `Show sample token probability analysis for demonstration purposes`. (realizes US-12.10)
- **FR-12.116** — The settings dialog SHALL present a display-mode radio group with items `Show All Tokens` (item 0) and `Show Samples` (item 1) — item 0 selected when show-all is on; a layout radio group with items `Grid View` (item 0) and `List View` (item 1) — item 0 selected when grid layout is on; and the labels `Display Mode:`, `Layout:`, `Grid View Max Alternatives:` and `(1 - 20)` (shown twice, beside the top-K field and beside the grid-max-alternatives field).
- **FR-12.117** — Every user-visible string in this feature SHALL be a hard-coded English literal. There are no resource files and no localisation.

*Demonstration rendering*

- **FR-12.118** — The demonstration command SHALL emit, in order: `Sample Text:` in yellow (preceded by an embedded newline inside the markup line); the sample text; a blank line; `Display Mode: All Tokens` or `Display Mode: Sample Tokens`; `Layout: Grid View` or `Layout: List View`; in grid layout only, `Grid Max Alternatives: {n}`; a blank line; a left-justified separator captioned `Token Probabilities Analysis`; the token rendering; a closing uncaptioned separator; a blank line. It SHALL return the result message `Sample token probability analysis generated`. (realizes US-12.10)
- **FR-12.119** — When the demonstration command is constructed **without** an output surface, it SHALL draw nothing and still return success with the same message and still expose its generated sample data.
- **FR-12.120** — When the demonstration token list is null, the surface SHALL emit `No token probability data available` in red and the command SHALL still report success.
- **FR-12.121** — The demonstration fixture SHALL use the fixed sample sentence `This is a sample response with token probability analysis. You can see how the model assigned probabilities to each token and what alternatives it considered.` tokenised by splitting on space, newline, tab, `.`, `,`, `!`, `?` and dropping empties — yielding **25** tokens.
- **FR-12.122** — The demonstration generator SHALL be seeded with **42** so that repeated runs in the same process produce identical output. The exact numbers produced are an artefact of the generator algorithm and SHALL NOT be treated as a requirement; "deterministic given a fixed seed" is the requirement.
- **FR-12.123** — Demonstration selected-token confidence SHALL be `min(98.0, 70.0 + random × 28.0)` — range **70.0 … 98.0** — written into the *log-probability* field. (see QUIRK-12.5)
- **FR-12.124** — Five demonstration tokens SHALL receive three hand-written alternatives each, matched case-insensitively, with these values written into the log-probability field: `sample` → `example` 15.0, `test` 8.0, `demo` 5.0; `response` → `reply` 12.0, `answer` 9.0, `output` 6.0; `token` → `word` 14.0, `symbol` 10.0, `element` 7.0; `probability` → `likelihood` 11.0, `chance` 8.0, `confidence` 6.0; `analysis` → `evaluation` 13.0, `assessment` 9.0, `examination` 6.0.
- **FR-12.125** — Every other demonstration token SHALL receive generic alternatives `«token»_1` at 10.0, `«token»_2` at 7.0 and `«token»_3` at 4.0 (the series `10.0 − i × 3.0`).
- **FR-12.126** — When the generated alternatives fall short of the top-K count, random filler alternatives SHALL be added with value `max(1.0, confidence − 20.0 − random × 50.0)` — range **1.0 … 78.0** — and names of the form `alt_{0-999}`. The final alternative count per token SHALL equal exactly the top-K setting.
- **FR-12.127** — The demonstration SHALL report a simulated response time of **0.5**.
- **FR-12.128** — With default settings the demonstration SHALL render exactly **15** of the 25 tokens (source indices 0–4, 10–14, 20–24) as one list starting at index 0, numbered **1…15**, with the true source positions lost.

*Ordering and numbering guarantees*

- **FR-12.129** — Token order SHALL always be the model's emission order; no rendering surface reorders tokens.
- **FR-12.130** — Alternatives SHALL be assumed to arrive already ranked and SHALL be rendered in the order given, with the single exception of the full-screen probability panel (FR-12.79).
- **FR-12.131** — All user-visible token numbers SHALL be 1-based except in the full-screen probability panel, which is 0-based.

---

**External technology**

*Requires: Styled terminal output — inline markup tags, named colour palette, rounded-box tables with per-column fixed widths and centring, multi-column grids, bordered panels with headers, captioned horizontal rules with left/centre justification, auto-expanding widths (ANSI/VT escape sequences). Source used: Spectre.Console 0.51.1, referenced by the core project, the plain shell and the full-screen shell. Reimplementer notes: markup uses square-bracket tags closed by a `[/]` sentinel, and literal brackets must be doubled to escape them. Colour names must exist in the chosen library's palette — the shared band map emits a bare `orange` that is very likely invalid (QUIRK-12.6). Any equivalent rich-terminal library works provided it supplies: a styled span, a table with per-column fixed widths and centring, a grid of fixed columns, a bordered panel with a header, and a captioned rule with left/centre justification.*

*Requires: Full-screen terminal user-interface toolkit — windows, framed views, menu bar, status bar, modal dialogs and message boxes, scrollable views with content-size and offset control, aligned labels, buttons, radio groups, check boxes, tab views, file open/save dialogs, named global colour-scheme slots, and custom views with a draw hook giving direct cell painting (terminfo / ANSI). Source used: Terminal.Gui 1.19.0, full-screen shell only. Reimplementer notes: needs per-widget foreground/background attribute pairs; four global scheme slots (base, dialog, menu, error) each with normal, focus, hot-normal, hot-focus and disabled attributes; a custom-draw hook exposing move-cursor, set-attribute and write-string; and a main-loop marshalling primitive so timer callbacks can touch the interface thread.*

*Requires: Terminal width query. Source used: standard-library console metrics. Reimplementer notes: used only for the responsive grid column count. Must degrade safely when output is redirected or no console is attached — the source guards only against a zero result via `max(1, …)`, not against a failing query.*

*Requires: Redirectable standard-output stream. Source used: standard-library console writer. Reimplementer notes: the plain surface must write through a replaceable stream, not a raw terminal handle, or the existing test approach (swap writer, render, restore in a finally block) cannot be reproduced.*

*Requires: Culture-sensitive number formatting for percent and fixed-point values (Unicode CLDR number patterns). Source used: standard-library formatting against the ambient culture. Reimplementer notes: the percent pattern differs by culture — invariant inserts a space before `%`, US English does not. Two shell build configurations force invariant globalization, so packaged builds show the spaced form. Pin a culture explicitly if exact output matters.*

*Requires: Deterministic pseudo-random generator. Source used: standard-library generator seeded with 42. Reimplementer notes: only used to build demonstration data so that demo output is stable across runs within a process. Reimplementations will not reproduce the same numbers unless the same algorithm is used; treat "deterministic given a fixed seed" as the requirement, not the specific values.*

*Requires: Exponential function. Source used: standard-library `exp`. Reimplementer notes: the only arithmetic this feature performs on model data — displayed probability equals e raised to the log-probability.*

*Requires: Unicode glyph rendering. Source used: literal characters in source — `◊` (U+25CA, the probability indicator), `№` (U+2116, one variant of the table number caption), `✓` / `✗` (U+2713 / U+2717, status marks), and the box-drawing set `═ ║ ╔ ╗ ╚ ╝` for the plain welcome banner. Reimplementer notes: the terminal font must carry these; supply ASCII fallbacks. Note the numero sign has already been corrupted to `?` in two of three copies (QUIRK-12.8).*

*Requires: A single consistent source and document text encoding (UTF-8). Source used: UTF-8 source files. Reimplementer notes: two files in this feature's blast radius are already broken — the project README is pure ASCII, so every box-drawing character in its rendered examples is `?`-mojibake and the examples are unusable as a visual specification; and `Shell.Gui/UI/SettingsDialog.cs` carries a lone `0x95` byte (a Windows-1252 bullet) at line 368 that is not valid UTF-8, decoding to a replacement character. A reimplementation must fix the encoding of literals rather than copy them.*

*Requires: Structured document persistence for display settings (JSON). Source used: standard-library JSON serializer with explicit wire names. Reimplementer notes: consumed, not owned by this feature, but the wire names are load-bearing — `showAllTokens` (default false), `gridViewForTokens` (false), `gridViewMaxAlternatives` (5), `enableLogProbabilities` (false), `logProbabilitiesTopK` (5). The computed probability field is explicitly excluded from serialization.*

*Requires: Per-user configuration directory resolution. Source used: standard-library special-folder lookup. Reimplementer notes: settings live at `«user profile»/.ChatDbg/settings.json`, with a temporary directory as the fallback. Any per-user configuration location works provided the directory name and file name are preserved for compatibility with existing installs.*

*Requires: Test doubles for the output contract, plus standard-output redirection. Source used: a mocking library for the contract; process-wide output writer replacement for the plain surface. Reimplementer notes: the output contract must be substitutable and the plain surface must write through a replaceable stream, or the existing test approach cannot be reproduced.*

No network, database, message queue, cryptography or clock dependency is required by this feature, and there is no dependency-injection container — the concrete output surface is constructed by the host and hand-passed to callers.

---

**Acceptance criteria**

- **AC-12.1** — **Given** the plain output surface, **when** a markup line with the content `[red]hello[/]` is written, **then** the captured output contains `hello` and does not contain `[red]`.
- **AC-12.2** — **Given** the plain output surface and the input `a]b[red]c`, **when** a markup line is written, **then** the output is exactly `a]bc`; **and given** the input `abc[red`, **then** the output is exactly `abc`.
- **AC-12.3** — **Given** the plain output surface and exactly one token whose text is `token` and whose log probability is the natural logarithm of 0.5, **when** the token table is drawn with the start index omitted, **then** exactly five lines are emitted, each 90 characters wide, matching:

  ```
  +---------+----------------------+--------------+----------------------------------------+
  | Token # | Text                 | Probability  | Top Alternatives                       |
  +---------+----------------------+--------------+----------------------------------------+
  | 1       | token                | 50.00000 %%  | (none)                                 |
  +---------+----------------------+--------------+----------------------------------------+
  ```

  under the invariant culture; under US English the probability cell reads `50.00000%%` instead.
- **AC-12.4** — **Given** the plain output surface, **when** a separator is requested with no title, **then** exactly 80 dash characters are emitted on one line; **when** the title `Report` is requested centred (the default), **then** the line is exactly 80 characters and reads 36 dashes, a space, `Report`, a space, 36 dashes; **when** the same title is requested left-justified, **then** the line is exactly 79 characters and reads `Report`, a space, 72 dashes.
- **AC-12.5** — **Given** the plain output surface and a token whose text is 25 characters long, **when** the token table is drawn, **then** the token cell shows the first 17 characters followed by `...`; **and given** an alternative token 15 characters long, **then** the alternative shows the first 7 characters followed by `...`.
- **AC-12.6** — **Given** the plain output surface and a token with 5 alternatives, **when** the token table is drawn, **then** exactly 2 alternatives appear and the cell ends with the exact substring `(+3 more)` — no space after the plus.
- **AC-12.7** — **Given** the shared compact alternatives description with 3 alternatives and a cap of 2, **when** it is built, **then** it lists 2 entries and ends with ` (+ 1 more)` — with a space after the plus.
- **AC-12.8** — **Given** a token whose text is `line\n` (containing a real newline), **when** it is escape-formatted for display, **then** the result contains the two-character sequence backslash followed by `n`; **and given** a null token, **then** the result is exactly `(null)`.
- **AC-12.9** — **Given** the shared colour-band map, **when** the value is 95 the band is `green`; **when** 70, `lime`; **when** 50, `yellow`; **when** 30, the 30–50 band name (`orange` in the shared map, `orange3` in the shell-local copies); **when** 10, `red`.
- **AC-12.10** — **Given** the shared value formatter and the input 42.1234, **when** it is formatted, **then** the result is exactly `42.12%` — string equality, not containment.
- **AC-12.11** — **Given** the shared plain-text report and one token whose text is `token` with no alternatives, **when** it is generated, **then** the first line is `=== Token Probabilities Analysis ===`, line 3 is `| # | Token          | Probability | Alternatives                |`, line 4 is `|---|----------------|------------|----------------------------|`, the data row's alternatives cell reads `none`, and the last non-empty line is 39 `=` characters — and the three row shapes do **not** align with one another.
- **AC-12.12** — **Given** sample display mode and a response of exactly 15 tokens, **when** the visualisation is rendered in a shell, **then** all 15 tokens appear once with no `Beginning Tokens:` / `Middle Tokens:` / `End Tokens:` captions; **and given** 16 tokens, **then** the three captions appear, each followed by exactly 5 tokens, numbered 1–5, 7–11 and 12–16 respectively.
- **AC-12.13** — **Given** grid layout, a terminal 120 columns wide and no explicit column budget, **when** 7 tokens are rendered, **then** exactly 3 columns are produced and the final row contains 1 token card plus 2 empty cards.
- **AC-12.14** — **Given** grid layout, a grid-max-alternatives setting of 3 and a token with 5 alternatives, **when** its card is drawn, **then** exactly 3 alternatives are listed and a dimmed line reading `+ 2 more` follows; **and given** the table layout with the same token and the same setting, **then** exactly 3 alternatives are shown — and would still be 3 with the setting at 5, because the table cap is hard-coded.
- **AC-12.15** — **Given** the plain output surface and an **empty** token list, **when** the token table is drawn, **then** exactly four lines are emitted (border, caption row, border, closing border) and no data row; **and given** a null list, **then** the call fails rather than emitting nothing.
- **AC-12.16** — **Given** the demonstration command constructed **without** an output surface, **when** it is executed, **then** it returns success with the message `Sample token probability analysis generated`, exposes non-null generated sample data, and writes nothing to the output.
- **AC-12.17** — **Given** the demonstration command constructed **with** an output surface and list layout, **when** it is executed, **then** the table-drawing operation is invoked at least once with a start index of 0, and the lines `Sample Text:`, `Display Mode: Sample Tokens` and `Layout: List View` are emitted before it.
- **AC-12.18** — **Given** the demonstration command with an output surface and the defaults (sample mode, list layout, top-K 5), **when** it is executed, **then** the sample sentence tokenises to exactly 25 tokens, the table operation receives exactly 15 of them (source indices 0–4, 10–14, 20–24) as one list with start index 0, and the rendered numbers run 1 through 15.
- **AC-12.19** — **Given** the demonstration command, **when** it is executed twice in the same process with the same top-K value, **then** both runs produce identical token probabilities and identical filler alternative names of the form `alt_{0-999}`.
- **AC-12.20** — **Given** the full-screen interface with the probability panel open on a message whose second token has probability 0.35, **when** the panel renders, **then** that token's line reads `1: "«token»" («percentage»)` at indent 2, is painted magenta on black (bucket 3), and its alternatives appear at indent 4 prefixed `Alt: `, ordered from highest to lowest probability, capped at the grid-max-alternatives value.
- **AC-12.21** — **Given** the full-screen interface with no assistant message carrying probability data, **when** the user toggles the probability panel, **then** a modal titled `No Log Probabilities` appears with body `There are no assistant messages with log probabilities to display.` and the layout is unchanged.
- **AC-12.22** — **Given** the full-screen interface, **when** an assistant message carrying probability data is drawn in the transcript, **then** a `◊` control appears below it at the padding indent; **and when** it is activated, **then** the panel opens with the transcript at 60% width, shows that message's tokens scrolled to the top, and the status line reads `Showing token probabilities for message at «timestamp»` for 3 seconds before reverting to `Provider: … | Model: … | Prompt: …`.
- **AC-12.23** — **Given** the grid-max-alternatives setting, **when** the value 25 is supplied through the command surface, **then** the response is `Grid max alternatives value must be a number between 1 and 20` and the stored value is unchanged; **when** 25 is supplied through the settings dialog and the dialog is accepted, **then** the stored value is exactly 20 and no message is shown.
- **AC-12.24** — **Given** the settings dialog with `Display Mode` on `Show Samples`, **when** the dialog is accepted, **then** show-all-tokens is stored as `false` (radio item 1).
- **AC-12.25** — **Given** the full-screen interface at start-up, **when** the window is drawn, **then** normal text is white on black, focused elements are bright yellow on dark grey, menus are white on dark grey, and error text is bright red on black.
- **AC-12.26** — **Given** the settings file `«user profile»/.ChatDbg/settings.json` is absent, **when** either shell starts, **then** rendering uses sample mode, list layout, 5 grid alternatives, probability capture off and top-K 5.
- **AC-12.27** — **Given** the full-screen interface started with settings loaded from disk, **when** the user runs the grid-layout subcommand inside it, **then** the command reports `Token probability analysis will use grid view layout.`, the probability panel's rendering is unchanged, and the settings file is rewritten with grid layout true alongside the **process-start defaults** for every other field.
- **AC-12.28** — **Given** stored history containing an assistant message whose role is spelled `Assistant` and which carries probability data, **when** the transcript is drawn, **then** a `◊` indicator appears below it; **and when** the user toggles the probability panel from the menu, **then** the `No Log Probabilities` dialog appears instead.
- **AC-12.29** — **Given** the probability panel showing a 200-token message and the user scrolled halfway down, **when** the panel is re-pointed at a message with no probability data, **then** the single line `No token probability data available.` is added but the scroll region keeps its previous size and offset and the screen is not refreshed, so the line may not be visible.
- **AC-12.30** — **Given** the probability panel open and the history then cleared, **when** the user selects the panel toggle to close it, **then** the modal `No Log Probabilities` appears and the panel remains open at the 60/40 split.
- **AC-12.31** — **Given** a token whose text is null and the full-screen probability panel, **when** the panel renders, **then** the draw fails; **whereas given** the same token and the plain token table, **then** the cell reads `(null)`.
- **AC-12.32** — **Given** the full-screen interface whose transcript viewport is 5 columns wide and a message containing any non-empty line, **when** the transcript is rebuilt, **then** the wrapper never terminates and the interface stops responding; **and given** a viewport of 0 columns (the pre-layout state), **then** the rebuild fails with a negative-length error surfaced through the `Error` dialog.
- **AC-12.33** — **Given** probability capture enabled and a reply that comes back with an empty token list, **when** the plain host renders the reply, **then** it emits `Note: Log probabilities were requested but none were returned by the model.` followed by `This could be due to the model not supporting this feature or an API limitation.` and no visualisation.
- **AC-12.34** — **Given** a command that succeeds in the plain host with the message `Done`, **when** the result is rendered, **then** the line is `✓ Done`; **and given** it fails with `Nope`, **then** the line is `✗ Nope`.
- **AC-12.35** — **Given** a separator title of 79 characters and the plain output surface, **when** the separator is drawn, **then** the call fails rather than truncating the title. (INFERRED — arithmetic goes negative; not exercised by any run.)

---

**Quirks**

- *QUIRK-12.1: The probability value handed to renderers is e raised to the log probability — a fraction in 0…1 — but the shared 5-band colour map and the shared 2-decimal value formatter compare against a 0…100 scale (thresholds 90/70/50/30). Consequently every real token renders in the lowest-confidence band (`red`) in the styled surface and both shells, and a 50%-probability token formats as `0.50%`. Evidence: `Core/Models/TokenLogProbabilities.cs:26`, `Core/Services/TokenFormatters.cs:38-45`, `:52-55`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.2: Every use of the 5-decimal percent format appends an extra literal `%`, so probabilities print as `50.00000 %%` (invariant culture) or `50.00000%%` (US English). Evidence: `BasicConsoleFormatter.cs:138`, `:204`; `TokenFormatters.cs:79`, `:111`; `TokenProbabilityVisualizer.cs:285`; `src/ChatDbg/ChatShell.cs:671`; `Shell.Gui/ChatShell.cs:624`. The full-screen panel (`ChatWindow.cs:671`, `:699`) is the only correct one. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.3: The project README shows 2-decimal probabilities such as `92.15%`; no live code path produces that form. Evidence: `README.md:305-345` versus `TokenFormatters.cs:52-55` fed a 0…1 value. Code wins. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.4: The README claims the demonstration command visualises in the plain shell; it does not. The plain host constructs the demonstration command without an output surface, so it produces only its result message and draws nothing. Evidence: `src/ChatDbg/ChatShell.cs:50`; `DemoLogProbsCommand.cs:62-68`; `README.md:256-269`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.5: The demonstration generator writes percentage-scale confidence values (70.0–98.0) and alternative values (1.0–78.0) directly into the log-probability field. Because displayed probability is the exponential of that field, the demo renders astronomically large percentages (confidence 70 renders as about 2.5 × 10^32 %, confidence 98 as about 3.6 × 10^44 %) and colours everything `green`. The demo therefore does not demonstrate the colour spectrum it claims to. Evidence: `DemoLogProbsCommand.cs:136`, `:141`, `:154-155`, `:212-247`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.6: The shared 5-band map returns the bare colour name `orange` for the 30–50 band while all three shell-local copies return `orange3`. The styling library's palette contains `orange1`, `orange3`, `orange4` and `orangered1` but no bare `orange`, so emitting that tag would raise a markup error. Evidence: `TokenFormatters.cs:43` versus `src/ChatDbg/ChatShell.cs:665`. (INFERRED — palette knowledge, not verified by execution; in practice unreachable because QUIRK-12.1 forces every real value into the `red` band.) Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.7: In the compiled-but-never-called visualiser, bracket escaping runs **after** the token formatter has already injected dim-styling brackets, neutralising the styling tags so they appear as literal doubled brackets. Evidence: `TokenProbabilityVisualizer.cs:166`, `:176`, `:229`, `:301`; the plain-shell copy does it in the correct order at `src/ChatDbg/ChatShell.cs:643-654`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.8: The token-table number-column caption is the numero sign in one copy and a literal question mark in the others — an encoding-loss artefact. Evidence: bytes `E2 84 96` at `src/ChatDbg/ChatShell.cs:604` and `Shell.Gui/ChatShell.cs:557`, versus byte `3F` at `SpectreConsoleFormatter.cs:114` and `TokenProbabilityVisualizer.cs:217`. A third casualty: `Shell.Gui/UI/SettingsDialog.cs` line 368 carries a lone `0x95` byte that is not valid UTF-8. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.9: The plain output surface's grid operation is not a grid — it silently discards both the column budget and the alternatives budget and renders the table instead, contradicting the contract's own documentation. Evidence: `IConsoleFormatter.cs:37-44` versus `BasicConsoleFormatter.cs:72-77`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.10: The plain output surface is never wired into the product. Nothing constructs it outside tests; the plain host emits directly to standard output using the styling library itself, and the full-screen host wires the styled surface. Evidence: `BasicConsoleFormatter.cs` with no non-test construction site. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.11: The full-screen host hands the **styled console** surface to the demonstration command, so running that command from inside the full-screen interface writes tables and separators straight to the terminal while the full-screen interface owns the screen, corrupting the display until the next full refresh. Evidence: `Shell.Gui/Program.cs:20`, `:41`. (INFERRED — follows from two libraries owning the terminal; no run performed.) Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.12: Three near-identical copies of the token-visualisation logic exist and have drifted apart in caption glyph and escaping order. Evidence: `src/ChatDbg/ChatShell.cs:412-687` (live), `Shell.Gui/Services/TokenProbabilityVisualizer.cs` (compiled, never called), `Shell.Gui/ChatShell.cs:365-640` (compiled, never instantiated). Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.13: Several rendering surfaces are dead. Never referenced anywhere: the flowing heat-map view (`LogProbHeatmapView.cs`), the visualiser (`TokenProbabilityVisualizer.cs`), the full-screen project's own shell class (`Shell.Gui/ChatShell.cs`), the shared text report and compact alternatives description (`TokenFormatters.cs:62`, `:97` — exercised only by tests), the divider colour scheme (`ChatWindow.cs:41`), the heat-map view's title property (`LogProbHeatmapView.cs:21`), and the full-screen window's own output-surface field, which is passed at construction (`Shell.Gui/Program.cs:87`) and stored (`ChatWindow.cs:18`, `:61`) but never read. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.14: The dark theme sets a disabled attribute only for the menu slot; the base, dialog and error slots are left at their default-constructed value, which may render disabled controls as black on black. Evidence: `ThemeManager.cs:16-19`, `:38`. (INFERRED — toolkit default for an unset attribute was not inspected.) Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.15: A separator title longer than 78 characters makes the dash count negative in the plain output surface, producing an outright failure rather than graceful truncation. Evidence: `BasicConsoleFormatter.cs:55`, `:59`, `:63-65`. (INFERRED — arithmetic is unambiguous; no test exercises it.) Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.16: The `Toggle System Messages in Status Bar` menu action is unimplemented and responds with the status text `System messages toggle not yet implemented`. Evidence: `ChatWindow.cs:1139-1141`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.17: A narrow transcript viewport hangs or crashes the renderer. The wrap limit is `(viewportWidth − 4) × 3 ÷ 4` with no lower bound: at widths 4 or 5 the limit is 0, the wrapper appends an empty string and advances by 0, giving an infinite loop that grows memory without bound; at widths 0 to 3 (including the pre-layout state, where the limit is −3) the substring call receives a negative length and throws. Only widths of 6 and above render. Evidence: `ChatWindow.cs:507`, `:539`, `:749-789`. (INFERRED — arithmetic is unambiguous; no run made at those widths.) Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.18: In the full-screen host, changing a display option updates a settings object the renderer never reads. The host creates a default settings object, hands it to every command, and only then replaces its local variable with the settings loaded from disk — which is the object the window and panel receive. Consequences: display commands typed inside the full-screen interface report success but change nothing on screen; each such command persists its stale object, so the save writes the process-start defaults plus the one changed field, silently discarding the user's other stored settings; and the no-argument status readout reports the stale values. The plain host is unaffected. Evidence: `Shell.Gui/Program.cs:14`, `:37-46`, `:58`, `:81`. (The persistence consequence is INFERRED — the resulting file contents were not observed.) Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.19: Role matching is case-sensitive in one half of the full-screen interface and case-insensitive in the other. The transcript, its colour lookup and the `◊` indicator lower-case the role before comparing; every query that decides whether the panel has anything to show compares the raw string against a lower-case literal. A message whose role is stored as `Assistant` therefore gets an indicator that opens the panel while the menu toggle insists there is nothing to display. Evidence: `ChatWindow.cs:513`, `:523`, `:554`, `:570`, `:740` versus `:219`, `:233`, `:364`, `:447`, `:804`, `:837`, `:873`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.20: The panel's empty state leaves stale scroll geometry behind. When the selected message has no probability data the panel adds its single label and returns immediately, skipping the content-size update, the scroll-to-top reset and the screen refresh the normal path performs, so the one-line message can be scrolled off-screen entirely. Evidence: `ChatWindow.cs:639-650` versus `:723-728`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.21: The panel cannot be closed once the history no longer contains an eligible message. The toggle checks for a qualifying message before deciding direction, so clearing the history while the panel is open leaves it stuck open with only the `No Log Probabilities` dialog as a response. Evidence: `ChatWindow.cs:804-810`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.22: Two rendering surfaces dereference token text without a null guard while every other surface returns `(null)`. Evidence: `ChatWindow.cs:670`, `:698`; `LogProbHeatmapView.cs:36` versus `TokenFormatters.cs:20`, `BasicConsoleFormatter.cs:186`, `TokenProbabilityVisualizer.cs:255`. (Reachability is INFERRED — traced through the persisted history shape but not executed.) Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.23: The plain token table draws a frame around nothing for an empty list, and throws for a null list. The header border, caption row and second border are written before the loop and the closing border after it, and the count is read without a guard. The contract's documentation promises neither behaviour. Evidence: `BasicConsoleFormatter.cs:85-89`, `:111`; `IConsoleFormatter.cs:46-51`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.24: The shared plain-text report's caption row, separator row and data rows are three different widths (3/16/13/29 versus 3/16/12/28 versus 4/16/13/26), so the pipes never line up. The token text there is also the only place in the product a token is printed without escape-formatting, so a token containing a newline breaks the row in half. Evidence: `TokenFormatters.cs:70-82`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.25: The README's rendered card-layout example labels each card's alternatives block `Alt:` and shows two-decimal probabilities; the code emits `Alternatives:` and five decimals plus a stray percent sign. Evidence: `README.md:305-322` versus `SpectreConsoleFormatter.cs:168` and QUIRK-12.2. Code wins. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.26: Three display settings are half-documented. Show-all-tokens, grid-layout and grid-max-alternatives are demonstrated as commands and as generic setting targets but are absent from the README's list of keys stored in the settings file, which does list the other two probability settings — even though all five are persisted under exactly those names. Evidence: `README.md:232-245`, `:96-111` versus `Core/Models/ChatSettings.cs:39-46`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.27: The flowing heat-map view neither clips nor wraps a token wider than the view. The wrap test moves to the next row before drawing but nothing shortens the token afterwards, so an over-wide token is written past the right edge from column 0 and still consumes a whole row, with no ellipsis or indicator. Evidence: `LogProbHeatmapView.cs:39-46`. (INFERRED — the view is never constructed, so this cannot be observed at all.) Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.28: Two of the four confidence palettes are unreachable in the shipped product. The 6-band background palette belongs to a view nothing constructs, and the shared 5-band palette is fed a 0…1 value so only its fallback band is selected. A user sees exactly one live palette — the 10-bucket map in the full-screen panel — plus solid `red` everywhere the styled surface is used. Evidence: `LogProbHeatmapView.cs:60-77`; `TokenFormatters.cs:38-45` with `TokenLogProbabilities.cs:26`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-12.29: Column widths and wrap points are measured in code units, not display cells. No surface consults a display-width function, so a wide East-Asian glyph occupies two terminal cells but counts as one, and a combining sequence counts as several while occupying one. Every table, panel and wrap point mis-measures for non-Latin content. Evidence: `BasicConsoleFormatter.cs:96`, `:133`; `ChatWindow.cs:683`, `:711`; `LogProbHeatmapView.cs:37`. (INFERRED — arithmetic is plain but no run was made.) Keep-or-fix decision deferred to Open Questions.*

---

**Source notes**

Derived from the dossier `dossiers/output-rendering.md`, itself read from the repository at commit `d8c18f61d6bb73666ed97cd4885e877e35558485`.

Primary evidence paths (all relative to the repository root):

- Output surface contract: `src/Xcaciv.ChatDbg.Core/Services/IConsoleFormatter.cs`
- Plain output surface: `src/Xcaciv.ChatDbg.Core/Services/BasicConsoleFormatter.cs`
- Shared token-presentation helpers (escaping, colour bands, value formatting, text report, compact description): `src/Xcaciv.ChatDbg.Core/Services/TokenFormatters.cs`
- Styled output surface: `src/ChatDbg.Shell.Gui/Services/SpectreConsoleFormatter.cs`
- Compiled-but-uncalled visualiser: `src/ChatDbg.Shell.Gui/Services/TokenProbabilityVisualizer.cs`
- Full-screen transcript, probability panel, status line, chrome and heat buckets: `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs`
- Flowing heat-map view (never constructed): `src/ChatDbg.Shell.Gui/UI/LogProbHeatmapView.cs`
- Theme application: `src/ChatDbg.Shell.Gui/UI/ThemeManager.cs`; invoked from `src/ChatDbg.Shell.Gui/Program.cs:71-74`
- Full-screen host wiring and the split-settings defect: `src/ChatDbg.Shell.Gui/Program.cs:14`, `:20`, `:37-46`, `:58`, `:81`, `:87`
- Plain host's live visualisation path: `src/ChatDbg/ChatShell.cs:365-687`
- Never-instantiated full-screen shell copy: `src/ChatDbg.Shell.Gui/ChatShell.cs:365-640`
- Display-options command strings and validation: `src/Xcaciv.ChatDbg.Core/Commands/LogProbsCommand.cs`
- Generic setting-assignment validation: `src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs:187`, `:198`, `:208`
- Demonstration command and fixture: `src/Xcaciv.ChatDbg.Core/Commands/DemoLogProbsCommand.cs`
- Settings dialog radio groups, labels and clamping: `src/ChatDbg.Shell.Gui/UI/SettingsDialog.cs:257-284`, `:456-464`
- Consumed models: `src/Xcaciv.ChatDbg.Core/Models/TokenLogProbabilities.cs`, `Models/ChatMessage.cs`, `Models/ChatSettings.cs:33-46`
- Settings persistence path: `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:11`, `:15-31`
- Tests: `Tests/Services/BasicConsoleFormatterTests.cs`, `Tests/Services/TokenFormattersTests.cs`, `Tests/Commands/DemoLogProbsCommandTests.cs`

There is no test project for either shell, so nothing in the automated suite covers the styled surface, the full-screen window, the theme, the flowing heat-map view or the slice captions.
