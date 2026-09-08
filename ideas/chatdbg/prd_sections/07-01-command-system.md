### 7.1 Command System & Dispatch

**Description**

The product is an interactive, terminal-hosted AI chat assistant for debugging. It has exactly one input box, and everything the user types arrives on the same line. Some of that text is *conversation* — it must be sent to a language-model provider, which costs money and takes time. The rest is *control*: change the model, manage the conversation transcript, configure token analysis, end the session. The Command System & Dispatch capability is the control plane that separates the two. It defines the disambiguation rule (a single leading `/` character), the parse, the name lookup, the invocation, the uniform reporting of the outcome, and the self-describing help that tells the user what is available.

The second reason this capability exists is extensibility. Every controllable capability in the product implements one small contract — a name, a one-sentence description, a usage string, and an asynchronous operation that takes an ordered list of text arguments and returns a uniform result. The host that owns the input loop never knows what any individual command does; it only knows how to parse a line, find a command by name, run it, and report three signals back to the user: *did it work*, *what should the user be told*, and *should the session stop*. Adding a capability means implementing that contract and adding it to a registry at start-up; there is no plugin discovery, no configuration-driven registration, and no runtime registration API.

Two independent hosts consume this capability: a line-oriented console shell and a full-screen terminal shell with menus, a status bar and modal dialogs. Both build their own registry of the same fifteen commands, and both must honour the same result contract — but as shipped they diverge in several user-visible ways (input trimming, the empty-command message, unknown-command wording, and how a dialog-triggered command reports itself). Those divergences are documented as requirements and as quirks, because a reimplementer will otherwise produce a third, different behaviour by accident. There is no authentication, authorisation, tenancy or audit dimension anywhere in this capability: anyone who can reach the prompt can run any registered command.

---

**User stories**

- **US-1.1** — As an end user at the chat prompt, I want to steer the application by typing a slash-prefixed instruction, so that control actions are handled locally and instantly instead of being sent to a language model.
- **US-1.2** — As an end user, I want a single instruction that lists every capability grouped by topic, so that I can discover what the product can do without reading documentation.
- **US-1.3** — As an end user, I want detailed help for one named capability, including its exact usage form, so that I can get the arguments right the first time.
- **US-1.4** — As an end user, I want every command to report its outcome the same way — a success mark, a failure mark, or deliberate silence — so that I can tell at a glance whether what I asked for happened.
- **US-1.5** — As an end user, I want a command that ends the session cleanly, so that provider connections are released and the process exits normally.
- **US-1.6** — As an end user of the full-screen terminal shell, I want menu items, function keys and dialogs to run the same capabilities as the typed instructions, so that I do not have to memorise command syntax.
- **US-1.7** — As a developer extending the product, I want to add a new capability by implementing one four-member contract and registering it by name, so that I never have to modify the input loop.
- **US-1.8** — As a host shell (the component that owns the input loop), I want a uniform three-signal result from every command, so that I can render feedback and honour a stop request without knowing what the command did.

---

**Use cases**

#### UC-1.1 — Dispatch a typed command (realizes US-1.1, US-1.4)

**Preconditions**
- The session is running and the prompt is accepting input.
- The command registry has been built and contains fifteen entries.

**Main flow**
1. The user submits one line of text.
2. The full-screen terminal shell trims leading and trailing whitespace from the line and clears the input box before doing anything else; the line-oriented console shell does not trim.
3. The host rejects blank or whitespace-only input: nothing is printed, no command runs, no state changes.
4. The host tests whether the first character of the line is `/`. If not, the line is handed to the chat-turn capability and this use case ends.
5. The host removes exactly the first character.
6. The host splits the remainder on the space character only, discarding empty tokens, so runs of spaces collapse. Tabs are not separators and quoting is not supported.
7. The first token, lower-cased using a culture-independent mapping, becomes the command name. The remaining tokens, in order, become the argument vector (possibly empty).
8. The host looks the name up in the registry using exact key matching.
9. The host invokes the found command asynchronously with the argument vector only, and waits for it to finish. Nothing else is processed meanwhile.
10. The host receives a result carrying success, an optional message, and an exit-requested flag.
11. Exit-requested is checked first. If it is not set, and the message is present and non-empty, the host renders it: on success prefixed with `✓ ` (U+2713 followed by a space), on failure prefixed with `✗ ` (U+2717 followed by a space) in the console shell, or as a transient status message / a modal error dialog respectively in the full-screen shell.
12. In the full-screen shell only, the transcript view, the token-probability side panel (when visible) and the status bar are re-rendered after any command.

**Alternate flows**
- **A1 — Zero tokens after the slash** (`/` alone, or `/` followed only by spaces): the console shell produces the failure message `Invalid command`; the full-screen shell returns silently with no feedback at all, and because the input box was already cleared the typed text simply vanishes.
- **A2 — Slash followed by a space then a name** (`/ help`): only one character was stripped and empty tokens were discarded, so the command `help` runs normally.
- **A3 — Doubled slash** (`//help`): the name becomes `/help`, which matches nothing, and the unknown-command error flow applies.
- **A4 — Name typed in mixed or upper case** (`/HELP`, `/Help`): the name is lower-cased before lookup, so the command runs. Arguments are never case-folded by the dispatcher; each command decides for itself.
- **A5 — Leading space before the slash** (`" /help"`): in the full-screen shell the line is trimmed, so the command runs; in the console shell it is not trimmed, so the line is sent to the language model as a chat turn.
- **A6 — Empty or absent message on the result**: nothing is displayed at all, on either success or failure.
- **A7 — Exit-requested set**: see UC-1.4.

**Error flows**
- **E1 — Unknown command name.** Console shell: `✗ Unknown command: /<name>. Type '/help' for available commands.` (the slash is re-added to the name the user typed). Full-screen shell: a modal dialog titled `Error` whose body is `Unknown command: <name>` — no slash, no hint about help. The session continues either way.
- **E2 — The command returns a failure result.** Console shell: `✗ <message>` on one line; the loop continues. Full-screen shell: a modal dialog titled `Command Error` containing the message and one `OK` button; the views then refresh.
- **E3 — The command raises an unexpected fault.** Console shell: `Error: <fault message>` is printed to standard output, and the full fault detail (type and call stack) goes only to a debug trace channel, never to the user; the loop continues. Full-screen shell: a modal titled `Error` with the fault message; the window survives.
- **E4 — The command never finishes.** There is no timeout, no cancellation, no progress indicator and no way to interrupt short of killing the process. The console prompt does not return; the full-screen window is frozen because the handler runs on the interface thread.
- **E5 — The command mutates shared state and then fails.** Partial mutations are not rolled back; there is no transaction or undo concept anywhere.
- **E6 — A fault escapes the entire session.** The entry point prints `Fatal error: <message>` and the process exits with code `1`.

**Postconditions**
- Exactly one command has run, or none has.
- The session is still accepting input unless an exit was requested.
- Any shared state a command mutated (transcript, configuration) reflects that mutation, whether the command succeeded or failed.

#### UC-1.2 — List all capabilities (realizes US-1.2)

**Preconditions**
- The registry is populated and contains an entry named `help`.

**Main flow**
1. The user submits `/help` with no arguments.
2. The help capability walks seven hand-authored section scripts in fixed order and, for each scripted name, emits one entry line `"/" + name + " - " + description` **only if that name is currently in the registry**.
3. It then appends three fixed blocks that are not derived from the registry at all: a local-LLM configuration block, a supported-providers block, and two closing instruction lines.
4. The whole document is returned as a single success message and rendered by the host as one multi-line block (the console shell prefixes only the first line with `✓ `).

**Alternate flows**
- **A1 — A registered command is absent from the section scripts**: it never appears in this listing.
- **A2 — A scripted name is not registered**: its entry line is silently skipped.
- **A3 — The registry contains nothing the scripts know about**: all seven headings, the five configuration lines, the three provider lines and the two closing lines are still emitted, producing a document with zero command entries, still reported as a success.
- **A4 — Full-screen shell alternative surface**: pressing `F1`, or choosing `Help ▸ View Commands...`, opens a completely different renderer — see UC-1.3 alternate flow A2.

**Error flows**
- **E1 — There is no failure path.** General help cannot fail; it neither validates nor reports an empty listing.

**Postconditions**
- No state has changed. The document is re-rendered from scratch on every invocation; nothing is cached.

#### UC-1.3 — Get detailed help for one capability (realizes US-1.3)

**Preconditions**
- The registry is populated.

**Main flow**
1. The user submits `/help <name>`.
2. Only the first argument is used; any further arguments are ignored.
3. The argument is lower-cased and looked up in the registry.
4. On a hit, a success result is returned whose message is exactly three lines separated by single newlines: `Command: /<registered name>`, `Description: <description>`, `Usage: <usage>`.
5. The host renders it as a success.

**Alternate flows**
- **A1 — The name echoed back is the registered command's own name, not the text the user typed**, so `/help SAMPLE` still yields `Command: /sample`.
- **A2 — Full-screen shell command listing dialog.** Pressing `F1` or choosing `Help ▸ View Commands...` opens a modal titled `Help`, fixed at 80 columns by 20 rows, containing a read-only word-wrapped view and a centred `Close` button. Its body is the line `Available Commands:`, a blank line, then for **every** registered command sorted ascending by name: `"/" + name`, then two spaces followed by the description, then a blank line. No usage strings, no grouping. This surface checks that a command named `help` exists but then ignores it entirely.
- **A3 — Usage computed from live state.** One capability recomputes its usage document from the current configuration each time it is read, so its detailed help reflects the configuration in force at that moment rather than a constant.

**Error flows**
- **E1 — The argument names nothing in the registry.** A **failure** result with the message exactly `Unknown command: <lower-cased argument>` — no leading slash and no hint about how to list commands. In the console shell this prints as `✗ Unknown command: set2`; in the full-screen shell it raises the modal titled `Command Error`, so a typo inside a help request produces a blocking dialog.
- **E2 — The argument includes the slash** (`/help /set`): the argument is looked up verbatim after lower-casing, so this fails with `Unknown command: /set`.
- **E3 — The `help` entry has been removed from the registry** (full-screen shell): pressing `F1` does nothing at all — no dialog, no error, no status text.

**Postconditions**
- No state has changed.

#### UC-1.4 — End the session (realizes US-1.5)

**Preconditions**
- The session is running.

**Main flow**
1. The user submits `/exit` or `/quit`. Both are separate capabilities with identical behaviour, and both ignore their arguments entirely, so `/exit now please` behaves exactly like `/exit`.
2. The command returns a result whose exit-requested flag is `true`, whose success flag is also `true`, and whose message is absent. Requesting an exit is a *successful* outcome, not an error.
3. The host checks exit-requested **before** rendering any message and stops immediately.
4. Console shell: the read loop breaks, `Goodbye!` is printed, the language-model provider services are released, and the process returns exit code `0`. Commands are never released — the contract has no disposal member.
5. Full-screen shell: the main window is asked to stop, the interface event loop unwinds, the terminal is restored, and the process returns exit code `0`.

**Alternate flows**
- **A1 — A result carries both an exit request and a message**: the message is discarded unrendered. Exit beats message, always.
- **A2 — Full-screen shell shortcuts.** `F10` on the status bar and `File ▸ Exit` stop the window directly and do **not** go through the exit capability at all, so any behaviour attached to those commands would not run on those paths.

**Error flows**
- **E1 — End of input reached without an exit command** (console shell, standard input redirected from an exhausted file or a closed pipe): the read returns nothing, which is treated identically to a blank line, so the prompt is re-printed and re-read indefinitely. There is no end-of-input check and no exit path other than the exit commands or killing the process. *(INFERRED consequence — the conflation of end-of-input with a blank line is directly observed in source; the resulting spin was not observed at run time.)*
- **E2 — A fault escapes during shutdown**: `Fatal error: <message>` is printed and the process returns exit code `1`.

**Postconditions**
- The process has terminated with exit code `0` on the normal path.
- Provider services have been released in the console shell; commands have not been released in either shell.

#### UC-1.5 — Run a capability from a graphical affordance (realizes US-1.6)

**Preconditions**
- The full-screen terminal shell is running with its registry populated.

**Main flow (synthesised command line)**
1. The user chooses a menu item that maps to a command: `File ▸ Pop Last Message` → `pop`; `File ▸ Clear History` → `clear`; `View ▸ Log Probabilities ▸ Run Demo Visualization` → `demologprobs`; `File ▸ Import History...` → `import <chosen path>`; `File ▸ Export History...` → `export <chosen path>`.
2. For the import and export items a file chooser opens first — titled `Import Chat History` with the prompt `Select a chat history file to import` and starting in the user-profile directory, or titled `Export Chat History` with the prompt `Select a location to save chat history` and pre-filled with `<user-profile>/chat_history.json`.
3. The handler builds a text command line by prepending `/` and re-enters the normal parse-and-dispatch path (UC-1.1) unchanged.
4. The result is rendered through the normal success/failure path.

**Main flow (direct invocation with a pre-built argument vector)**
1. The user opens `File ▸ Inject Message...` (a dialog fixed at 70 columns by 15 rows, with the role field pre-filled `user`) or the change-model dialog (fixed at 60 columns by 10 rows).
2. On `OK` the handler builds the argument vector itself with no text parsing: the inject path supplies `[role, message]` plus an optional third argument for a numeric position; the model path supplies `[model identifier]`.
3. The handler looks the command up by name and awaits it.
4. The handler renders the result itself: on success a transient status message; on failure a modal titled `Error`.

**Alternate flows**
- **A1 — Chosen path contains spaces**: because the path is interpolated into a space-split command line, the receiving command re-joins its whole argument vector with single spaces, so the path works — but runs of consecutive spaces in it collapse to one.
- **A2 — Multi-word inject message**: the direct-invocation path applies no parsing, so the command receives exactly two arguments and the message keeps its internal spacing verbatim.
- **A3 — Non-numeric position in the inject dialog**: the position is discarded silently with no warning and the message is appended at the end; the user sees a success message that does not mention the position they typed.
- **A4 — Empty message on a result from either dialog**: canned text is substituted — `Message injected` or `Model changed` on success, `Failed to inject message` or `Failed to change model` on failure — so the identical command is silent when typed and chatty when triggered from a dialog.
- **A5 — An exit-requested result from a dialog-driven command** would be ignored entirely; these paths do not check the flag.

**Error flows**
- **E1 — A fault raised on the menu path** is caught by the menu wrapper and shown as a modal titled `Error` with the fault message.
- **E2 — A fault raised on a dialog-driven direct-invocation path is unhandled.** Neither dialog handler guards the call, and nothing waits on those handlers, so the failure has no caller to report it. *(INFERRED consequence: no dialog appears and the failure is invisible, or the application crashes, depending on the toolkit's policy for unobserved failures.)*
- **E3 — The named command is missing from the registry**: both dialog handlers guard the lookup but supply no alternative branch, so pressing `OK` closes the dialog and does nothing at all.

**Postconditions**
- The requested capability has run exactly once, or not at all.
- The transcript view, token panel and status bar have been refreshed on the synthesised-command-line path.

#### UC-1.6 — Build the command registry at start-up (realizes US-1.7, US-1.8)

**Preconditions**
- The process has started and no user input has been read yet.

**Main flow**
1. The host constructs the shared state objects: one conversation-transcript object, one configuration object, and the configuration, transcript-persistence and system-prompt services.
2. The host constructs an empty name-to-command map.
3. The host constructs each command instance in a fixed literal order, passing its collaborators explicitly by position. There is no dependency-resolution container, no scanning and no configuration file.
4. Each instance is inserted under its own declared name. A later insertion under a name already present silently replaces the earlier one; no warning is emitted.
5. **Last and unconditionally**, the host constructs the help capability, handing it a reference to that very same map, and stores it under `help`, overwriting anything previously registered under that name. Because the map is shared by reference rather than copied, the help listing includes an entry for help itself.
6. The map is then treated as read-only for the rest of the process. No command can add, remove or alias entries.
7. On shutdown the host releases the provider services; commands are never released.

**Alternate flows**
- **A1 — Console shell configuration wiring**: the host also builds its commands before the saved configuration is loaded, but afterwards copies the loaded values field-by-field into the same configuration object the commands already hold, so the commands and the shell stay connected to one object.
- **A2 — Full-screen shell configuration wiring**: the host builds every command against the empty, default configuration object created before start-up, then *replaces the variable* with the configuration loaded from storage and hands the **replacement** to the window. The transcript object is not replaced, so transcript commands stay connected; every configuration-bearing command is left holding a detached object.

**Error flows**
- **E1 — A command declares a name containing an upper-case character**: the typed name is lower-cased before an exact-match lookup, so the command would be permanently unreachable by typing. *(INFERRED — no shipped command does this.)*
- **E2 — A command returns nothing at all from its operation**: the host dereferences the result unconditionally, so this raises a fault on the dispatch path. *(INFERRED — no test asserts a non-null result.)*
- **E3 — A capability is implemented but never inserted into any registry**: it is unreachable by any surface and appears in no help listing, with no diagnostic anywhere.

**Postconditions**
- The registry contains fifteen entries: `clear`, `demologprobs`, `exit`, `export`, `help`, `import`, `inject`, `inspect`, `logprobs`, `model`, `pop`, `prompt`, `quit`, `set`, `tokenize`.
- The help capability holds a live reference to that registry.

---

**Functional requirements**

*Classification and parsing*

- **FR-1.1** Blank or whitespace-only submitted input MUST be discarded with no output, no command lookup and no state change. (realizes US-1.1)
- **FR-1.2** The full-screen terminal shell MUST trim leading and trailing whitespace from the submitted line before classifying it; the line-oriented console shell MUST NOT trim. This divergence is the shipped behaviour and is recorded as a quirk. (realizes US-1.1)
- **FR-1.3** The full-screen terminal shell MUST clear the input field before dispatch, so the typed text is gone from the box whether the command succeeds or fails. (realizes US-1.6)
- **FR-1.4** A submitted line whose first character is `/` MUST be treated as a command; any other line MUST be handed to the chat-turn capability without any command lookup. The marker is exactly one character. (realizes US-1.1)
- **FR-1.5** Parsing MUST drop exactly the first character, then split the remainder on the space character (U+0020) only, discarding empty tokens so runs of spaces collapse. Tab characters MUST NOT be treated as separators and remain part of the token containing them. (realizes US-1.1)
- **FR-1.6** No quoting or escaping syntax exists. Multi-word text arguments survive only because individual commands re-join their whole argument vector (or a slice of it) with single spaces; original spacing is therefore lost. (realizes US-1.1)
- **FR-1.7** The first token, lower-cased with a culture-independent mapping, MUST become the command name; the remaining tokens in order MUST become the argument vector, which excludes the command name and is empty when no arguments were typed. The culture-independent mapping is required so that locale-specific casing rules (notably dotted/dotless `I`) cannot break lookup. (realizes US-1.1)
- **FR-1.8** The dispatcher MUST NOT case-fold arguments; each command decides for itself whether to fold its own arguments.
- **FR-1.9** When parsing yields zero tokens (input `/` alone, or `/` followed only by spaces), the console shell MUST produce a failure with the exact message `Invalid command`, and the full-screen shell MUST return silently with no feedback whatsoever. (realizes US-1.1)
- **FR-1.10** Input `/ help` (slash, space, name) MUST dispatch the `help` command, because only one character is stripped and empty tokens are discarded.
- **FR-1.11** Input `//help` MUST yield the command name `/help`, which matches no registry entry and therefore takes the unknown-command path.

*Lookup and registry*

- **FR-1.12** Registry lookup MUST use exact (ordinal) key matching of the lower-cased typed name against each command's own declared name. There is no prefix matching, no abbreviation, no alias table, and no "did you mean" suggestion. (realizes US-1.1)
- **FR-1.13** Every registered command name MUST be lower-case; a name containing an upper-case character would be permanently unreachable by typing. *(INFERRED — no shipped command violates this.)* (realizes US-1.7)
- **FR-1.14** Each host MUST build its registry once at start-up, in code, in a fixed literal order, with every collaborator passed explicitly. There MUST be no discovery, no scanning, no configuration-file registration, no plugin loading and no dependency-resolution container. (realizes US-1.7)
- **FR-1.15** Inserting a command under a name already present MUST silently replace the earlier entry; only the last one is reachable and no warning is emitted. (realizes US-1.7)
- **FR-1.16** The help capability MUST be constructed last, unconditionally, and MUST be given a live reference to the same registry map (not a copy), then stored under `help`, overwriting any prior entry under that name. Consequently the help listing includes an entry for help itself. (realizes US-1.2)
- **FR-1.17** The registry MUST be created once per process and never mutated afterwards; no command may add, remove, rename or alias entries. (realizes US-1.7)
- **FR-1.18** Both shipped hosts MUST register exactly these fifteen commands: `inject`, `pop`, `import`, `export`, `model`, `set`, `logprobs`, `prompt`, `demologprobs`, `clear`, `exit`, `quit`, `tokenize`, `inspect`, and `help`. The two hosts differ only in that the full-screen host supplies a rich rendering collaborator to the demo-visualisation command while the console host supplies none. (realizes US-1.7)
- **FR-1.19** Commands MUST be constructed with all their collaborators at registration time and hold them for the process lifetime, mutating shared mutable state objects (the single conversation transcript, the single configuration object) in place so that changes are immediately visible to every other holder. (realizes US-1.7)
- **FR-1.20** A command that accepts an optional rendering collaborator MUST also work correctly when none is supplied (headless), returning the same success message either way. (realizes US-1.7)
- **FR-1.21** A host that builds commands before loading the saved configuration MUST connect them to the configuration the rest of the application uses — either by copying loaded values into the object the commands already hold, or by making the configuration object replaceable in place. The full-screen host as shipped does neither; see QUIRK-1.13. (realizes US-1.7)

*The command contract*

- **FR-1.22** Every command MUST expose exactly four things: a **name**, a **description** (one short sentence), a **usage** string, and an asynchronous **execute** operation taking an ordered list of text arguments and yielding a result. Nothing else is part of the contract — no cancellation signal, no disposal member, no synchronous variant, no per-command exit code. (realizes US-1.7, US-1.8)
- **FR-1.23** Name, description and usage MUST be readable **without executing the command**, so help can render metadata for a command that is never invoked. (realizes US-1.2, US-1.3)
- **FR-1.24** Name and description MUST be constant for a given command. Usage MAY be recomputed from live state on each read; exactly one shipped command does this, so its detailed help reflects the configuration currently in force. (realizes US-1.3)
- **FR-1.25** By convention a usage string begins with `/<name>` followed by a description of the argument form. Three unregistered implementations violate this (usage begins with the bare name and embeds newlines and an `Example:` line); a clone MUST make the convention uniform. (realizes US-1.7)
- **FR-1.26** Every command MUST tolerate an empty argument vector and MUST branch on argument count to produce its own usage error when arguments are missing.
- **FR-1.27** A command MUST NOT return "nothing"; the result is dereferenced unconditionally by every caller. *(INFERRED — deduced from the unguarded dereference on every dispatch path.)* (realizes US-1.8)
- **FR-1.28** The result MUST be the only output channel a command uses. One shipped command additionally publishes its generated data on a separately readable property that no host reads; a clone MUST NOT reproduce that side channel.
- **FR-1.29** A command receives the argument vector and nothing else — no reference to the host, the raw line, the registry (except the help capability, which is given the registry at construction), and no cancellation signal.

*The result contract*

- **FR-1.30** A result MUST carry exactly three fields: **success** (boolean), **message** (optional text, may be absent), and **exit-requested** (boolean, defaulting to `false`). (realizes US-1.8)
- **FR-1.31** The success factory MUST produce `{success = true, message = the supplied text or absent, exit = false}`. The message argument is optional, so a bare success is silent by construction.
- **FR-1.32** The error factory MUST produce `{success = false, message = the supplied text, exit = false}`. A message is required by the signature but is **not validated**: empty text is accepted and, by FR-1.36, renders as nothing — a failure indistinguishable from a silent success. *(INFERRED consequence — no shipped command returns an empty error message.)*
- **FR-1.33** The exit factory MUST produce `{success = true, message = absent, exit = true}`. Requesting an exit is a *successful* outcome, not an error. There is no exit-with-message and no exit-with-failure factory.
- **FR-1.34** The result is a plain mutable value holder whose three fields are individually settable, so any combination (including failure plus exit) is constructible; no shipped command produces such a combination.
- **FR-1.35** **Exit beats message.** When exit is requested the host MUST stop before rendering anything; a message carried on an exit result is never shown. (realizes US-1.5)
- **FR-1.36** **Empty message means silence.** On the typed and menu paths of both hosts, a success or failure whose message is absent or empty MUST produce no user-visible output whatsoever. The user cannot distinguish "worked silently" from "nothing happened". (realizes US-1.4)
- **FR-1.37** The message MUST be rendered verbatim. It may be multi-line and arbitrarily long (general help is roughly 35 lines); there is no length limit, no truncation, no pagination and no escaping or sanitisation anywhere.

*Rendering the outcome*

- **FR-1.38** Console shell, success with a non-empty message: print the message prefixed with `✓ ` — U+2713 CHECK MARK followed by one space. Only the first line of a multi-line message carries the prefix. (realizes US-1.4)
- **FR-1.39** Console shell, failure with a non-empty message: print the message prefixed with `✗ ` — U+2717 BALLOT X followed by one space. Failure is never fatal; the loop continues. (realizes US-1.4)
- **FR-1.40** Full-screen shell, success with a non-empty message: show `✓ ` followed by the message in the transient status label, which reverts to the standard status text after **3000 ms**. The standard status text follows the template `Provider: <provider> | Model: <model> | Prompt: <prompt-name>`. (realizes US-1.4)
- **FR-1.41** Each transient status message starts its own independent 3000 ms timer that unconditionally overwrites the label with the standard status text, and no earlier timer is cancelled. Timers are fire-and-forget with no failure handling. This is the shipped behaviour; see QUIRK-1.15.
- **FR-1.42** Full-screen shell, failure with a non-empty message: open a modal dialog titled exactly `Command Error` containing the message and a single `OK` button. (realizes US-1.4)
- **FR-1.43** After any command in the full-screen shell, the transcript view, the token-probability side panel (when visible) and the status bar MUST be re-rendered.
- **FR-1.44** The status label is also used for non-command feedback with no glyph prefix at all; the glyph prefix is a property of command results, not of the label.
- **FR-1.45** Console shell, unknown command name: produce the exact message `Unknown command: /<name>. Type '/help' for available commands.` — note that the slash is re-added to the name the user typed. (realizes US-1.4)
- **FR-1.46** Full-screen shell, unknown command name: open a modal titled exactly `Error` whose body is `Unknown command: <name>` — with no leading slash and no hint about help. (realizes US-1.4)
- **FR-1.47** In the console shell the glyph is the only machine-readable success/failure signal — there is no per-command exit code and no structured output. A clone targeting assistive technology MUST NOT rely on the glyph alone.

*Exit*

- **FR-1.48** Two separate commands, `exit` and `quit`, MUST each return an exit-requested result. Both MUST ignore their arguments entirely, so `/exit now please` behaves exactly like `/exit`. (realizes US-1.5)
- **FR-1.49** Console shell on exit: break the read loop, print exactly `Goodbye!`, release the language-model provider services, and return process exit code `0`. Commands MUST NOT be released — the contract has no disposal member. (realizes US-1.5)
- **FR-1.50** Full-screen shell on exit: request the main window to stop, unwind the interface event loop, shut the terminal interface down in a guaranteed-cleanup block, and return process exit code `0`. (realizes US-1.5)
- **FR-1.51** The full-screen shell's `F10` status-bar item and its `File ▸ Exit` menu item MUST stop the window directly **without** invoking the `exit` or `quit` commands. This is the shipped behaviour; see QUIRK-1.9.
- **FR-1.52** A fault escaping the whole session MUST cause the entry point to print `Fatal error: <message>` and return process exit code `1`. Exit code `0` means normal shutdown; these are the only machine-readable signals the application emits.
- **FR-1.53** The console read loop treats end-of-input identically to a blank line and continues, so with an exhausted redirected input it re-prints the prompt and re-reads indefinitely. This is the shipped behaviour; see QUIRK-1.14.

*General help document*

- **FR-1.54** `/help` with an empty argument vector MUST return a success whose message is the general help document. (realizes US-1.2)
- **FR-1.55** The general help document MUST begin with the line `ChatDbg Commands:` followed by a blank line. (realizes US-1.2)
- **FR-1.56** The document MUST then emit seven sections in exactly this order, each heading followed by its entries and then a blank line: `Basic Commands:` (entries for `help`, `exit`, `quit`, `clear` in that order), `Model Configuration:` (`set`, `model`), `System Prompt Management:` (`prompt`), `Chat History Management:` (`import`, `export`, `inject`, `pop`), `Token Analysis:` (`logprobs`, `demologprobs`), `LLama Provider Commands (local LLM):` (`tokenize`, `inspect`). This ordering is hand-authored — it is neither registration order nor alphabetical. (realizes US-1.2)
- **FR-1.57** Each per-command entry MUST be rendered as `"/" + name + " - " + description`, and MUST be emitted **only if** that name is currently present in the registry; a scripted name that is not registered is silently skipped, and a registered command whose name is not in the script never appears here. (realizes US-1.2)
- **FR-1.58** The document MUST then emit the heading `LLama GPU Configuration:` followed by these five lines verbatim, including their internal spacing:
  - `/set llamaContextSize <512-32768> - Context size for model (default: 4096)`
  - `/set llamaGpuLayers <0-100>       - Layers to offload to GPU (0=CPU only)`
  - `/set llamaGpuDevice <0,1,...>     - GPU device IDs to use (e.g., "0" or "0,1")`
  - `/set llamaThreads <0-64>          - Thread count (0=system default)`
  - `/set llamaBatchSize <1-2048>      - Batch size for inference`

  These ranges and the default context size of `4096` are **help text only** — the help renderer enforces nothing. (realizes US-1.2)
- **FR-1.59** The document MUST then emit the heading `Supported Providers:` followed by the three lines `- azure: Azure OpenAI Service`, `- bedrock: Amazon Bedrock AI`, `- llama: Local LLM via LLamaSharp (supports GPU acceleration)`. (realizes US-1.2)
- **FR-1.60** The document MUST close with exactly two lines: `Type '/help <command>' for detailed help on a specific command.` and `Type '/set' without parameters for current configuration details.` (realizes US-1.2)
- **FR-1.61** The general help document MUST be re-rendered from scratch on every invocation; nothing is cached or memoised.
- **FR-1.62** General help MUST emit its full scaffolding (all seven headings, the five configuration lines, the three provider lines, the two closing lines) even against a registry that contains none of the scripted names, and MUST still report success. This is the shipped behaviour; see QUIRK-1.27.

*Detailed help*

- **FR-1.63** `/help <name>` MUST use only the first argument; any further arguments are ignored, so `/help set provider` is treated as `/help set`. (realizes US-1.3)
- **FR-1.64** The argument MUST be lower-cased and looked up verbatim; an argument that includes a slash (`/help /set`) therefore fails. (realizes US-1.3)
- **FR-1.65** On a hit, the result MUST be a success whose message is exactly three lines separated by single newlines: `Command: /<registered name>`, `Description: <description>`, `Usage: <usage>`. The name echoed is the registered command's own name, not the text typed. (realizes US-1.3)
- **FR-1.66** On a miss, the result MUST be a **failure** whose message is exactly `Unknown command: <lower-cased argument>` — no leading slash and no hint. Because it is a failure, the full-screen shell shows the `Command Error` modal for a typo inside a help request. (realizes US-1.3)

*Command listing dialog (full-screen shell)*

- **FR-1.67** Pressing `F1` (status-bar item labelled `~F1~ Help`) or choosing `Help ▸ View Commands...` MUST open a second, independent help renderer. It MUST first check that a command named `help` exists in the registry, but MUST then ignore that command entirely and build its own listing. (realizes US-1.2)
- **FR-1.68** That dialog MUST be modal, titled `Help`, fixed at **80 columns by 20 rows**, containing a read-only word-wrapped text view and a centred `Close` button. (realizes US-1.2)
- **FR-1.69** Its body MUST be the line `Available Commands:`, a blank line, then for **every** registered command sorted ascending by name: `"/" + name`, then two spaces followed by the description, then a blank line. Usage strings MUST NOT be shown and there MUST be no grouping. *(The ordering uses the platform's default string ordering; INFERRED to be culture-sensitive, though for the all-lowercase names in use the result is plain alphabetical: `clear, demologprobs, exit, export, help, import, inject, inspect, logprobs, model, pop, prompt, quit, set, tokenize`.)* (realizes US-1.2)
- **FR-1.70** If no `help` entry exists, pressing `F1` MUST do nothing at all — no dialog, no error, no status text. This is the shipped behaviour; see QUIRK-1.3.

*Graphical invocation paths (full-screen shell)*

- **FR-1.71** These menu items MUST build a text command line and re-enter the normal parse-and-dispatch path: `File ▸ Pop Last Message` → `pop`; `File ▸ Clear History` → `clear`; `View ▸ Log Probabilities ▸ Run Demo Visualization` → `demologprobs`; `File ▸ Import History...` → `import <chosen path>`; `File ▸ Export History...` → `export <chosen path>`. (realizes US-1.6)
- **FR-1.72** The import file chooser MUST be titled `Import Chat History` with the prompt `Select a chat history file to import` and MUST start in the user-profile directory. The export file chooser MUST be titled `Export Chat History` with the prompt `Select a location to save chat history` and MUST pre-fill `<user-profile>/chat_history.json`. (realizes US-1.6)
- **FR-1.73** Because a chosen path is interpolated into a space-split command line and re-joined by the receiving command with single spaces, a path containing single spaces works but runs of consecutive spaces collapse to one. A clone SHOULD pass the chosen path as a single pre-built argument instead. (realizes US-1.6)
- **FR-1.74** The inject dialog (titled `Inject Message`, fixed at 70 columns by 15 rows, role field pre-filled `user`) MUST invoke the `inject` command with a pre-built argument vector `[role, message]`, appending an optional third argument for the position, bypassing text parsing entirely so the message keeps its internal spacing verbatim. The role field accepts only `user`, `assistant` or `system` downstream. (realizes US-1.6)
- **FR-1.75** The change-model dialog (titled `Change Model`, fixed at 60 columns by 10 rows) MUST invoke the `model` command with the pre-built argument vector `[model identifier]`. (realizes US-1.6)
- **FR-1.76** A position entered in the inject dialog that does not parse as an integer MUST be discarded silently, with no warning, and the message appended at the end. This is the shipped behaviour; see QUIRK-1.18.
- **FR-1.77** Both dialog paths render the result themselves rather than using the standard renderer: on success a transient status message; on failure a modal titled `Error` (not `Command Error`). When the message is absent or empty they substitute canned text — `Message injected` / `Model changed` on success, `Failed to inject message` / `Failed to change model` on failure — which contradicts FR-1.36. This is the shipped behaviour; see QUIRK-1.16.
- **FR-1.78** Neither dialog path checks the exit-requested flag, so an exit result from those commands would be ignored.
- **FR-1.79** Neither dialog path guards against a fault raised by the command, and neither supplies an alternative branch when the named command is absent from the registry; pressing `OK` in the latter case closes the dialog and does nothing. This is the shipped behaviour; see QUIRK-1.19 and QUIRK-1.20.

*Concurrency, observability and scope*

- **FR-1.80** Commands MUST execute strictly one at a time, in the order the user submits them; the host MUST await completion before reading the next input. There is no queueing, no parallelism, no re-entrancy protection and no locking. (realizes US-1.8)
- **FR-1.81** There MUST be no timeout, cancellation, progress indication or interrupt for a running command.
- **FR-1.82** Command invocations MUST NOT be logged. The only diagnostic output on this path is the full fault detail written to a debug trace channel by the console shell when a command raises a fault.
- **FR-1.83** This capability MUST NOT itself read or write any file, database, network resource, environment variable or setting. All of its own state is in memory for the lifetime of the process.
- **FR-1.84** This capability provides **no** permission or role checks, rate limiting, audit log, undo, command chaining or piping, scripting or batch execution, per-command exit codes, command history recall, tab completion, prefix or abbreviation matching, "did you mean" suggestions, aliases, or help localisation. All names, headings and messages are hard-coded English.
- **FR-1.85** Detailed help for the configuration command surfaces, through this capability, the environment-variable names `CHATDBG_AZURE_API_KEY`, `CHATDBG_AWS_ACCESS_KEY`, `CHATDBG_AWS_SECRET_KEY`, `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY`, together with a Windows-only example path `C:\models\llama3-8b.gguf`. The text is surfaced verbatim; this capability neither validates nor owns it. (realizes US-1.3)
- **FR-1.86** Adding a new capability requires three coordinated edits — the implementation, a construction line in **each** host's registry initialiser, and a line in the hand-authored general-help script — with silent degradation if any is missed. A clone SHOULD reduce this to one edit and drive both help surfaces from the registry. (realizes US-1.7)

---

**External technology**

*Requires: a managed runtime with first-class asynchronous operations and a task/promise type (no protocol). Source used: .NET 10 (`net10.0`), SDK pinned in `global.json` to `10.0.100-rc.1.25451.107` with `rollForward: latestFeature`. Reimplementer notes: every command entry point is asynchronous even when the work is synchronous — the exit, quit, help and clear commands simply wrap an already-computed value. A single-threaded event loop is sufficient; nothing in dispatch is CPU-bound or I/O-bound. No cancellation tokens exist anywhere in the product, so the substitute runtime's cancellation facilities are unused by this contract.*

*Requires: an interactive character terminal that can read a line and write Unicode lines (ANSI/VT escape sequences on POSIX, the Win32 console API on Windows). Source used: the platform standard library's console input/output. Reimplementer notes: the success and failure markers are the Unicode characters U+2713 and U+2717 followed by a space; the terminal and font must render them or an ASCII substitute must be chosen deliberately, because in the console shell the glyph is the only success/failure signal emitted. The dispatcher itself uses no colour.*

*Requires: a terminal user-interface widget toolkit providing menus, a status bar with function-key items, modal message dialogs with a title and an OK button, a read-only scrolling word-wrapped text view, transient status text, and a "stop the application" call. Source used: Terminal.Gui 1.19.0, in the full-screen shell only. Reimplementer notes: the exact toolkit is irrelevant; what must survive is the modal-with-title semantics (`Command Error` vs. `Error` titles are user-visible), the 80x20 fixed help dialog, the F1/F10 status items, and a stop mechanism that unwinds the event loop so a guaranteed-cleanup block can restore the terminal.*

*Requires: a rich console text renderer supporting markup, horizontal rules and tables/grids (no protocol). Source used: Spectre.Console 0.51.1, hidden behind the product's own formatter abstraction. Reimplementer notes: reached only by the demo-visualisation command and optional by contract — the same command must succeed with no renderer supplied. The abstraction declares: write a marked-up line, write a plain line, write a blank line, write a horizontal rule with an optional title, render a token grid (start index, max columns where 0 means auto, max alternatives defaulting to 3), and render a token table (start index). Do not mix a direct console renderer with a full-screen toolkit that owns the screen.*

*Requires: a native file open/save chooser supporting a starting directory, a pre-filled name and a cancel signal (no protocol). Source used: the terminal toolkit's own open and save dialogs. Reimplementer notes: needed only because two menu items feed a chosen path into a text command line. Pass the path as a single pre-built argument rather than re-parsing it, or paths with consecutive spaces are corrupted.*

*Requires: a unit-test framework with a mocking facility, to execute the acceptance criteria below (no protocol). Source used: xUnit 2.9.1 with Moq 4.20.69, driven by Microsoft.NET.Test.Sdk 17.12.0 and xunit.runner.visualstudio 2.8.1, with coverage via coverlet.collector 6.0.2. Reimplementer notes: the help tests build a fake command exposing only name, description and usage and never wire up its execute member — keep the contract narrow enough that a test double remains that trivial. Note that in the source only the core library is under test; neither host is, so no dispatch behaviour has an executable specification.*

*Requires: a single, consistent source-text encoding for embedded help and usage strings (UTF-8). Source used: UTF-8 for every source file except one, which is stored in a legacy single-byte encoding with no byte-order mark. Reimplementer notes: keep all embedded help text in one encoding; the affected document's only non-ASCII character is a bullet glyph, and a toolchain reading it as UTF-8 substitutes a replacement character on 30 lines.*

*Requires: process exit codes (POSIX/Win32 convention). Source used: `0` on normal shutdown, `1` when a fault escapes the session. Reimplementer notes: this is the only machine-readable signal the whole application emits; individual commands have no exit codes.*

*Requires (optional): size-optimised packaging that produces ahead-of-time-compiled and single-file binaries. Source used: two extra build configurations, both defaulting to a `win-x64` runtime identifier and both enabling invariant-globalisation mode. Reimplementer notes: invariant-globalisation mode changes the culture-sensitive comparisons this capability performs — the `/`-prefix test and the alphabetical ordering in the command listing dialog. Nothing in dispatch requires Windows; only the shipped packaging presets assume it. A clone should make the prefix test ordinal so packaging cannot alter behaviour.*

---

**Acceptance criteria**

- **AC-1.1** **Given** the session is at the prompt, **when** the user submits a line that is empty or contains only spaces, **then** nothing is printed, no command is looked up, and the prompt returns.
- **AC-1.2** **Given** the session is at the prompt, **when** the user submits `how do I read a core dump?`, **then** the line is treated as a chat turn and no command lookup occurs.
- **AC-1.3** **Given** a registry containing a command named `sample`, **when** the user submits `/SAMPLE`, `/Sample` or `/sample`, **then** in all three cases the same command is invoked with an empty argument vector.
- **AC-1.4** **Given** the user submits `/inject   user    hello   world` (three spaces between tokens), **when** it is parsed, **then** the command receives exactly the three arguments `user`, `hello`, `world`.
- **AC-1.5** **Given** the user submits `/` alone, **then** the console shell prints `✗ Invalid command` and the full-screen shell displays nothing at all.
- **AC-1.6** **Given** the user submits `/nosuchcommand`, **then** the console shell prints `✗ Unknown command: /nosuchcommand. Type '/help' for available commands.` and the full-screen shell opens a modal titled `Error` reading `Unknown command: nosuchcommand`, and in both cases the session continues.
- **AC-1.7** **Given** the user submits `/ help` (slash, space, `help`), **then** the general help document is produced; **and given** the user submits `//help`, **then** the unknown-command error for the name `/help` is produced.
- **AC-1.8** **Given** the console shell, **when** the user submits `" /help"` with one leading space, **then** the line is sent as a chat turn; **given** the full-screen shell and the same text, **then** the general help document is produced.
- **AC-1.9** **Given** the `exit` command and an empty argument vector, **when** it is executed, **then** the result carries exit-requested `true`, success `true` and no message. **And** `/quit` behaves identically. **And** `/exit now please` behaves exactly like `/exit`.
- **AC-1.10** **Given** a command returns a result with exit-requested `true` and the message `see you`, **when** the host processes it, **then** `see you` is never displayed, the console shell prints `Goodbye!`, the provider services are released, and the process returns exit code `0`.
- **AC-1.11** **Given** a command returns success with the message `Model changed to gpt-4o`, **then** the console shell prints exactly `✓ Model changed to gpt-4o`, and the full-screen shell shows `✓ Model changed to gpt-4o` in the status line, which reverts to `Provider: azure | Model: gpt-4o | Prompt: default` after 3000 ms.
- **AC-1.12** **Given** a command returns failure with the message `File not found`, **then** the console shell prints exactly `✗ File not found` and continues accepting input, while the full-screen shell opens a modal titled `Command Error` containing `File not found` and one `OK` button.
- **AC-1.13** **Given** a command returns success with an absent or empty message, **then** neither host displays anything at all.
- **AC-1.14** **Given** a registry containing exactly one command named `sample` with description `description` and usage `/sample`, **when** `/help` is run with no arguments, **then** the result is a success whose message contains the literal `ChatDbg Commands`, contains the section headings `Basic Commands:`, `Model Configuration:`, `System Prompt Management:`, `Chat History Management:`, `Token Analysis:`, `LLama Provider Commands (local LLM):`, `LLama GPU Configuration:` and `Supported Providers:` in that order, contains the five `/set llama...` lines verbatim, contains the three provider lines `- azure: Azure OpenAI Service`, `- bedrock: Amazon Bedrock AI` and `- llama: Local LLM via LLamaSharp (supports GPU acceleration)`, contains the two closing lines `Type '/help <command>' for detailed help on a specific command.` and `Type '/set' without parameters for current configuration details.`, **and contains no entry for `sample`**.
- **AC-1.15** **Given** a registry containing a command named `sample` with description `description` and usage `/sample`, **when** `/help sample` is run, **then** the result is a success whose message is exactly the three lines `Command: /sample`, `Description: description`, `Usage: /sample` separated by single newlines; **and when** `/help SAMPLE` is run, **then** the first line is still `Command: /sample`.
- **AC-1.16** **Given** `/help set provider` is run and `set` is registered, **then** the result is identical to `/help set` — the second argument is ignored.
- **AC-1.17** **Given** `/help notacommand` is run, **then** the result is a **failure** whose message is exactly `Unknown command: notacommand`; **and given** `/help /set` is run, **then** the message is exactly `Unknown command: /set`.
- **AC-1.18** **Given** the help capability was constructed with the registry *before* it was itself inserted into that registry, **when** `/help` is run, **then** the listing still contains an entry for `/help`, proving the registry is shared by reference and not copied.
- **AC-1.19** **Given** the full-screen shell with all fifteen commands registered, **when** the user presses `F1` or chooses `Help ▸ View Commands...`, **then** a modal titled `Help` sized 80 columns by 20 rows opens listing every registered command in the order `clear, demologprobs, exit, export, help, import, inject, inspect, logprobs, model, pop, prompt, quit, set, tokenize`, each rendered as `/name` followed by a line of two spaces plus the description followed by a blank line, with no usage strings and no section grouping.
- **AC-1.20** **Given** the full-screen shell, **when** the user chooses `File ▸ Clear History`, **then** the `clear` command is invoked exactly as if `/clear` had been typed and its result is rendered through the standard success/failure path.
- **AC-1.21** **Given** the full-screen shell's inject dialog is completed with role `user`, message `hello there friend` and no position, **then** the `inject` command receives exactly the two arguments `user` and `hello there friend`, with the message spacing preserved and no parsing applied.
- **AC-1.22** **Given** the full-screen shell's inject dialog is completed with role `user`, message `hello` and position `abc`, **then** the position is discarded with no warning, the message is appended at the end of the transcript, and the status line reads `✓ Injected user message: hello`.
- **AC-1.23** **Given** the full-screen shell and a command that returns success with no message, **when** it is invoked by typing, **then** nothing is displayed; **when** the same command is invoked through the inject dialog, **then** the status line reads `Message injected`; **and when** invoked through the change-model dialog, **then** the status line reads `Model changed`.
- **AC-1.24** **Given** a command raises a fault with the message `Access denied`, **then** the console shell prints `Error: Access denied` and continues the loop while the full fault detail appears only on the debug trace channel, and the full-screen shell opens a modal titled `Error` with `Access denied` and keeps running.
- **AC-1.25** **Given** a fault escapes the whole session, **then** the entry point prints `Fatal error: <message>` and the process returns exit code `1`; **given** a normal `/exit`, **then** the process returns exit code `0`.
- **AC-1.26** **Given** two commands both declaring the name `sample` are inserted in sequence, **then** only the second is reachable by typing `/sample` and no warning is emitted anywhere.
- **AC-1.27** **Given** the full-screen shell started with a stored configuration naming provider `bedrock` and model `anthropic.claude-v2`, **when** the user types `/set` with no arguments, **then** the reported configuration is the built-in defaults — provider `azure`, model `gpt-4`, temperature `0.7`, maximum tokens `1000`, region `us-east-1`, system prompt `default`, token log probabilities off, top-K `5` — not the stored values; **and when** the user then types `/model gpt-4o`, **then** the command reports `Changed model from 'gpt-4' to 'gpt-4o'`, writes that to the stored configuration, and the status bar still reads `Model: anthropic.claude-v2`. **In the console shell the same sequence reports the stored values instead.**
- **AC-1.28** **Given** the full-screen shell, **when** two commands each returning a success message are run one second apart, **then** the second message disappears roughly two seconds later — three seconds after the *first* command, not the second.
- **AC-1.29** **Given** the full-screen shell and a registry from which `help` has been removed, **when** the user presses `F1`, **then** nothing happens at all — no dialog, no error, no status text; **and** with `inject` or `model` removed, pressing `OK` in the corresponding dialog closes it and does nothing.
- **AC-1.30** **Given** the console shell with standard input redirected from a file that contains no exit instruction, **when** the file is exhausted, **then** the shell does not terminate: it re-prints the prompt and re-reads indefinitely. *(INFERRED — the conflation of end-of-input with a blank line is observed in source; the spin was not observed at run time.)*
- **AC-1.31** **Given** any host, **when** the user runs `/help set`, **then** the result is a success whose message is the configuration command's usage document rendered from the **current** configuration values, listing the environment-variable names `CHATDBG_AZURE_API_KEY`, `CHATDBG_AWS_ACCESS_KEY`, `CHATDBG_AWS_SECRET_KEY`, `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY`, and containing 30 lines whose leading bullet character renders as a replacement character rather than a bullet.
- **AC-1.32** **Given** a command whose usage string is a constant, **when** `/help <that name>` is run twice with a configuration change in between, **then** both results are identical; **given** the configuration command, **then** the second result differs and reflects the new values.
- **AC-1.33** **Given** the registry contains a command implementing the contract but never inserted into any registry, **when** the user types its name with a slash, **then** the unknown-command error is produced and the command appears in neither help surface.

---

**Quirks**

*QUIRK-1.1: Three complete command implementations — `export-logs`, `export-analysis` and `show-analysis` — are never inserted into any registry, so no user can invoke them and they appear in no help surface. They also break the usage convention (usage begins with the bare name, embeds newlines and an `Example:` line) and one of them is the only command in the product with flag-style arguments (`--top N`, `--state`, `--range START END`, default top = 3), a style the parser offers no support for whatsoever. The repository states the cause outright: they are listed as complete but "Requires integration with shell command registry (placeholder implementation)". Evidence: `Core/Commands/ExportLogsCommand.cs:12`, `Core/Commands/ExportTokenAnalysisCommand.cs:12`, `Core/Commands/ShowTokenAnalysisCommand.cs:13`, `:15`, `:20` vs. `src/ChatDbg/ChatShell.cs:42-58` and `src/ChatDbg.Shell.Gui/Program.cs:31-47`; `IMPLEMENTATION_SUMMARY.md:49-54`, `:221-222`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.2: There are two entirely different help renderers with different content and different ordering. Typing `/help` gives the curated, grouped document; pressing F1 or choosing Help ▸ View Commands... in the same shell gives an alphabetical name-and-description list with no usage strings. They can disagree. Evidence: `Core/Commands/HelpCommand.cs:37-98` vs. `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:1087-1126`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.3: The command listing dialog checks that a command named `help` exists but never uses it; if `help` were ever unregistered, F1 would silently do nothing. Evidence: `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:1089`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.4: An entire second dispatcher is dead code — a full registry, read-eval loop and dispatcher that is never instantiated, because the full-screen entry point builds its own registry and constructs the window directly. The dead copy registers only 13 commands (no `tokenize`, no `inspect`) and only 2 providers. Evidence: `src/ChatDbg.Shell.Gui/ChatShell.cs:43-68`, `:81-121`, `:276-294` vs. `src/ChatDbg.Shell.Gui/Program.cs:30-90`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.5: General help is a hand-maintained script, so it can drift from the registry in both directions — a registered command missing from the script is invisible in `/help`, and a scripted name that is not registered is silently dropped. Today the 15 scripted names match the 15 registered names in both live hosts. Evidence: `Core/Commands/HelpCommand.cs:44-78`, `:101-107`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.6: `/help` with several arguments ignores all but the first, so `/help set provider` silently becomes `/help set`. Evidence: `Core/Commands/HelpCommand.cs:29`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.7: `/help /set` fails, because the argument is looked up verbatim after lower-casing, giving `Unknown command: /set` for a user who naturally includes the slash. Evidence: `Core/Commands/HelpCommand.cs:29-34`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.8: The help capability reports an unknown name as a **failure**, while listing help is a success — so a typo inside `/help` produces a blocking modal error dialog in the full-screen shell rather than inline text. Evidence: `Core/Commands/HelpCommand.cs:34` with `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:415`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.9: F10 "Quit" and File ▸ Exit bypass the command system entirely, stopping the window directly, so any future exit-time behaviour attached to the `exit`/`quit` commands would not run on those paths. Evidence: `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:193`, `:271`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.10: Several commands write directly to the terminal instead of, or in addition to, returning a message — 37 direct console writes in one, 10 in another, and the system-prompt editor reads console lines until a line equal to `END`. In the full-screen shell this output is drawn straight onto the screen the widget toolkit owns, corrupting the layout; and the full-screen host is precisely the one that injects the rich renderer, while the console host injects none. The `END`-terminated editor cannot work under the full-screen shell at all, since there is no console reader. Evidence: `Core/Commands/InspectCommand.cs`, `Core/Commands/TokenizeCommand.cs`, `Core/Commands/PromptCommand.cs:258-267`, `src/ChatDbg.Shell.Gui/Program.cs:41`, `src/ChatDbg/ChatShell.cs:52`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.11: A plain-text renderer implementing the formatter abstraction ships in the core library but is wired into no host; only tests construct it. Evidence: `Core/Services/BasicConsoleFormatter.cs:12`; `Xcaciv.ChatDbg.Core.Tests/.../BasicConsoleFormatterTests.cs:15`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.12: The About dialog states the product "Supports Amazon Bedrock and Azure OpenAI" — two providers — while general help lists three, including the local-LLM provider. Evidence: `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:1130-1135` vs. `Core/Commands/HelpCommand.cs:90-92`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.13: In the full-screen shell, every configuration-bearing command is wired to a configuration object nobody else uses. The entry point creates a default configuration, constructs `model`, `set`, `logprobs`, `prompt`, `demologprobs`, `tokenize` and `inspect` against it, then reassigns the variable to the configuration loaded from storage and hands the replacement to the window. Observable consequences: `/set` with no arguments reports built-in defaults rather than the user's saved configuration; `/model <id>` reports success and persists the change but the window keeps using the previously loaded model and the status bar never updates; `/set provider llama` does not switch the provider the chat turn actually uses. Transcript commands are unaffected because the transcript object is never reassigned; the console shell does not have this defect. Evidence: `src/ChatDbg.Shell.Gui/Program.cs:14`, `:37-46`, `:58`, `:81`; `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:55`; `Core/Models/ChatSettings.cs:8-37`; contrast `src/ChatDbg/ChatShell.cs:127-140`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.14: The console shell spins forever at end of input — a null read is treated exactly like a blank line, so with an exhausted redirected input or a closed pipe the loop re-prints the prompt and re-reads without end, with no exit path other than an exit command or killing the process. Evidence: `src/ChatDbg/ChatShell.cs:83-88`; same code at `src/ChatDbg.Shell.Gui/ChatShell.cs:84-89`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.15: Overlapping transient status messages truncate each other. Each message starts an independent 3000 ms timer that unconditionally overwrites the label, and no earlier timer is cancelled, so two commands run less than three seconds apart leave the second message visible only until the first command's timer fires. The timer callback is fire-and-forget with no failure handling. Evidence: `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:903-915`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.16: The "empty message means silence" rule is violated on exactly two paths. The inject and change-model dialogs substitute canned text for a missing message, so the identical command is silent when typed and chatty when triggered from a dialog; they also title the failure modal `Error` where the typed path titles it `Command Error`. Evidence: `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:987`, `:991`, `:1072`, `:1076`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.17: The failure factory accepts an empty message without validation, producing a failure that renders as total silence and is therefore indistinguishable from a silent success. Evidence: `Core/Models/CommandResult.cs:12-13` with `src/ChatDbg/ChatShell.cs:100`. (INFERRED consequence — no shipped command does this and no test covers it.) Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.18: The inject dialog silently discards a position that fails to parse as an integer, with no warning, and the user sees a success message that does not mention the position they typed. Evidence: `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:967-970`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.19: Both dialog paths become silent no-ops if their command is missing from the registry — the lookup is guarded but there is no alternative branch, so pressing OK closes the dialog and does nothing. Evidence: `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:974-993`, `:1066-1078`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.20: Faults raised on the two dialog-driven paths are unhandled — unlike the menu path and the typed path, which both catch and show a modal. Nothing waits on those handlers, so a failure has no caller to report it. Evidence: `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:960-993`, `:1061-1079` vs. `:931-934` and `:353-356`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.21: One command publishes the data it generated on a separately readable property alongside its result — a second output channel that no host reads and only tests use. Evidence: `Core/Commands/DemoLogProbsCommand.cs:20`, `:54-59`; `DemoLogProbsCommandTests.cs:25`, `:38`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.22: General help renders its whole skeleton against an empty registry — seven headings, five configuration lines, three provider lines and two closing lines, with zero command entries — and reports success. The shipped test does exactly this and passes. Evidence: `Core/Commands/HelpCommand.cs:39-96`, `:101-107`; `HelpCommandTests.cs:20-31`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.23: A View-menu item is a stub that only shows the status text `System messages toggle not yet implemented`, although the user manual lists "Toggle System Messages" as a working feature. Evidence: `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:296`, `:1138-1141`; `README.md:35`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.24: Detailed help for the configuration command renders 30 lines whose leading bullet is a replacement character, because that one source file is stored in a legacy single-byte encoding with no byte-order mark (30 lone `0x95` bytes, the only non-ASCII bytes in the file). The rendered output is toolchain-dependent; the source defect is certain. Evidence: `Core/Commands/SetCommand.cs:24`, `:297`, `:307-352`. Keep-or-fix decision deferred to Open Questions.*

*QUIRK-1.25: The command listing dialog sorts by the platform's default string ordering rather than an explicit ordinal ordering, and two shipped packaging configurations enable invariant-globalisation mode, which changes that ordering. The `/`-prefix test likewise uses a culture-sensitive "starts with" comparison. Both are harmless for the ASCII names shipped but make behaviour depend on the build configuration. Evidence: `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:1107`; `src/ChatDbg/ChatShell.cs:92`; `Xcaciv.ChatDbg.Shell.csproj:52`, `:95`. (INFERRED that the ordering is culture-sensitive.) Keep-or-fix decision deferred to Open Questions.*

---

**Source notes**

Dossier: `/mnt/g/3RD-Party/reversing/output/chatdbg/dossiers/command-system.md` (feature 3 of the inventory, "Command System & Dispatch", platform capability, complexity M). Source repository pinned at commit `d8c18f61d6bb73666ed97cd4885e877e35558485` on branch `LLamaSharp_support`; all paths below are relative to the repository root.

Contract and result:
- `src/Xcaciv.ChatDbg.Core/Models/ICommand.cs:3-9` — the four-member command contract.
- `src/Xcaciv.ChatDbg.Core/Models/CommandResult.cs:5-16` — three fields and three factories.

Help:
- `src/Xcaciv.ChatDbg.Core/Commands/HelpCommand.cs:13`, `:19-22`, `:26-35`, `:37-98`, `:101-107` — both help modes, the section scripts, the fixed blocks.
- `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:192`, `:313`, `:1087-1126` — the second, alphabetical help renderer.

Exit:
- `src/Xcaciv.ChatDbg.Core/Commands/ExitCommand.cs:7`, `:11-14`; `src/Xcaciv.ChatDbg.Core/Commands/QuitCommand.cs:7`, `:11-14`.

Dispatch (console host):
- `src/ChatDbg/ChatShell.cs:36-37`, `:40-67`, `:80-119`, `:127-140`, `:326-340`, `:692-714`; `src/ChatDbg/Program.cs:8-14`.

Dispatch (full-screen host):
- `src/ChatDbg.Shell.Gui/Program.cs:14`, `:30-55`, `:58`, `:81`, `:90-103`.
- `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:55`, `:192-193`, `:266-271`, `:290`, `:296`, `:313-314`, `:334-420`, `:798`, `:900-935`, `:940-995`, `:1004-1032`, `:1053-1079`, `:1087-1141`.
- `src/ChatDbg.Shell.Gui/ChatShell.cs:43-68`, `:81-121`, `:276-294` — the dead second dispatcher (QUIRK-1.4).

Commands reached through this capability but owned elsewhere:
- `src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs:22`, `:24`, `:44-279`, `:280`, `:297`, `:307-352`, `:358`; `ModelCommand.cs:17`, `:25-34`; `InjectCommand.cs:15`, `:21-45`; `ExportCommand.cs:17`, `:23-48`; `ImportCommand.cs:17`, `:28-34`; `PopCommand.cs:15`, `:29`; `PromptCommand.cs:23`, `:192`, `:258-267`, `:351`; `LogProbsCommand.cs:18`, `:30`; `DemoLogProbsCommand.cs:20`, `:25-40`, `:54-68`; `TokenizeCommand.cs:16`, `:46`; `InspectCommand.cs:16`, `:46`; `ClearCommand.cs:14`.
- Unregistered implementations (QUIRK-1.1): `ExportLogsCommand.cs:12-14`, `ExportTokenAnalysisCommand.cs:12-14`, `ShowTokenAnalysisCommand.cs:13-20`.

Rendering abstraction:
- `src/Xcaciv.ChatDbg.Core/Services/IConsoleFormatter.cs:11-51`; `src/Xcaciv.ChatDbg.Core/Services/BasicConsoleFormatter.cs:12`.

Executable specification (the only one that exists):
- `HelpCommandTests.cs:12-52`; `ExitAndQuitCommandTests.cs:9-27`; `CommandResultTests.cs:8-34`; `DemoLogProbsCommandTests.cs:12-39`; `BasicConsoleFormatterTests.cs:15`.
- `Xcaciv.ChatDbg.sln` and `Xcaciv.ChatDbg.Core.Tests.csproj` — four projects, only the core library referenced by tests, so **no dispatch behaviour is covered by any test**.

Documentation consulted and treated as hints only (code wins): `README.md:3`, `:13`, `:29`, `:33`, `:35`, `:46-73`, `:82-83`; `IMPLEMENTATION_SUMMARY.md:49-54`, `:221-222`; `src/ChatDbg/prd.md:38`, `:47`, `:97-107` and its byte-identical duplicate `src/ChatDbg.Shell.Gui/prd.md` (both stale). Build and packaging evidence: `global.json`, `Xcaciv.ChatDbg.Shell.csproj:16`, `:23-60`, `:63-97` (runtime identifier at `:30`, `:70`; invariant-globalisation at `:52`, `:95`), and the byte-identical settings in `Xcaciv.ChatDbg.Shell.Gui.csproj`.
