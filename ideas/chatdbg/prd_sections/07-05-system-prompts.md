### 7.5 System Prompt Management

**Description**

Every request the product sends to a language model is prefixed with a block of instruction text — the *system prompt* — that sets the assistant's persona and focus: general debugging help, code review, algorithm design, security review, or anything the user writes themselves. System Prompt Management is the library of those instruction texts. It is a set of named, human-editable, persisted **prompt records** that the user can browse, inspect, switch between, author, revise, remove, and move in and out of the product as plain text files. Exactly one prompt is **active** at any moment, and its text is what the provider layer injects as the system message on every model call.

Without this feature, re-roling the assistant would mean editing a configuration file by hand and restarting. With it, the user re-roles the assistant with a single typed command or two keystrokes in a graphical dialog, and can accumulate a personal library of reusable personas that survive restarts and can be handed to a colleague as a file. On first run — or any run where the library is found empty — the product seeds four built-in prompts so that a brand-new install is immediately useful.

The feature has two user-facing surfaces that are deliberately **not** equivalent: a text-command surface (`/prompt …`, plus a second entry point through the settings command) available in both shells, and a modal graphical management dialog available only in the full-screen terminal shell. The two surfaces differ in their guards, their defaults and their side effects, and those differences are load-bearing observable behavior, documented below. There is no multi-user model, no authentication, no authorization and no per-user scoping beyond the operating-system account whose per-user data directory holds the files.

---

**User stories**

- **US-5.1** — As a developer using the chat shell, I want a starter library of prompts to exist the first time I run the product, so that I can re-role the assistant without authoring anything first.
- **US-5.2** — As a developer, I want to see which prompt is currently active and read its full text, so that I know how the assistant is being instructed before I ask it anything.
- **US-5.3** — As a developer, I want to list every prompt in my library with its description and its created / last-used times, so that I can choose one and see which I actually use.
- **US-5.4** — As a developer, I want to read one named prompt's full detail without switching to it, so that I can compare candidates before committing.
- **US-5.5** — As a developer, I want to switch the active prompt by name in one command, so that the assistant's persona changes from my next message onward and stays changed after a restart.
- **US-5.6** — As a developer whose prompt name contains spaces, I want a command entry point that accepts a multi-word name, so that names I created elsewhere remain reachable.
- **US-5.7** — As a developer, I want to create a new named prompt with a description, so that I can start building a persona of my own.
- **US-5.8** — As a developer, I want to replace a prompt's instruction text through an interactive multi-line editor in the plain console, so that I can author long personas without leaving the shell.
- **US-5.9** — As a developer using the full-screen terminal shell, I want a form that lets me set name, description and content in one step (and later change description *and* content together), so that authoring is not a two-command dance.
- **US-5.10** — As a developer, I want to delete a prompt I no longer need, and be protected from deleting the one the assistant is currently using, so that I cannot accidentally break my running session.
- **US-5.11** — As a developer, I want to export a prompt's text to a plain text file, so that I can share the persona with a colleague or keep it in version control.
- **US-5.12** — As a developer, I want to import any text file as a named prompt with a description, so that a persona someone sent me becomes usable immediately.
- **US-5.13** — As a developer using the full-screen terminal shell, I want a single modal screen from which I can browse, view, select, create, edit, delete, import and export prompts, so that I do not have to remember command syntax.
- **US-5.14** — As the host shell at startup, I want to resolve the persisted active-prompt *name* into prompt *text* and record that the prompt was used, so that the very first model call of the session carries the right instructions and the library shows real usage.
- **US-5.15** — As the provider layer, I want to read the active prompt's text as a plain string at call time, so that I can inject it as the conversation's system message regardless of which backend I am talking to.
- **US-5.16** — As a developer, I want the active prompt's name shown in the launch banner and in the persistent status line, so that I always know which persona is in effect.

---

**Use cases**

**UC-5.A — Library bootstrapping at launch (realizes US-5.1)**

- *Preconditions:* The product is starting. The operating system exposes a per-user local application-data directory.
- *Main flow:*
  1. The prompt store resolves its base directory: the per-user local application-data directory joined with the fixed folder name `ChatDbg`.
  2. It appends the fixed subdirectory name `system_prompts`.
  3. If that directory tree does not exist, it is created.
  4. Every record file already in the directory is loaded.
  5. If the number of successfully loaded records is exactly zero, the four built-in prompts are written to disk in the fixed order `default`, `code-reviewer`, `algorithm-helper`, `security-expert`.
  6. Control returns to the shell; startup continues.
- *Alternate flows:*
  - **A1** — The directory already contains one or more loadable records: step 5 is skipped entirely; no existing record is touched.
  - **A2** — A test or embedding host supplies a base-directory override: the override replaces the default base directory. A whitespace-only or empty override falls back to the default. No shipping shell supplies an override.
- *Error flows:*
  - **E1** — The directory cannot be created (permissions, read-only volume): the failure is **not caught**; it propagates out of store construction and out of application startup. The shell fails to start.
  - **E2** — One or more record files are malformed or unreadable: each is reported on standard output as `Error loading system prompt from <full path>: <reason>` and skipped; loading continues.
  - **E3** — *All* files in the directory are unparseable: the loaded count is zero, so the four built-ins are written, **overwriting** any of those files whose names collide with the four built-in file names.
- *Postconditions:* The store directory exists. The library contains at least the four built-ins, or whatever it previously held. Blocking work performed on the calling thread before any user interface exists.

**UC-5.B — Resolve the active prompt at startup (realizes US-5.14, US-5.16)**

- *Preconditions:* The prompt store has been constructed (UC-5.A). Persisted settings have been loaded; they carry an active-prompt **name** whose default value is the literal `default`.
- *Main flow:*
  1. The shell asks the store for the prompt whose name equals the persisted active-prompt name.
  2. The record is found. Its text is copied into the runtime active-prompt text slot.
  3. The record's last-used timestamp is set to the current UTC instant and the whole record is rewritten to disk.
  4. The launch banner prints `System Prompt: <activeName>`; in the full-screen shell the status line reads `Provider: <provider> | Model: <model> | Prompt: <activeName>`.
  5. From this point every model call is prefixed with the runtime active-prompt text.
- *Alternate flows:*
  - **A1** — No record with that name exists: the runtime active text falls back to the built-in fallback text (FR-5.30). No message is shown. The persisted name is left pointing at the missing prompt — it is **not** self-healed.
  - **A2** — Full-screen shell entry point: the lookup runs only when the persisted name is non-empty, and it does **not** stamp last-used and has no failure handling of its own.
- *Error flows:*
  - **E1** — Any exception during resolution in the plain console shell: two lines are printed, `Error loading system prompt: <reason>` then `Using default system prompt.`, and the built-in fallback text is used.
- *Postconditions:* A non-empty runtime active-prompt text exists for the whole session.

**UC-5.C — List the library (realizes US-5.3)**

- *Preconditions:* Store constructed. User is at a shell prompt.
- *Main flow:*
  1. User types `/prompt list`.
  2. The whole library is read from disk and sorted ascending by name using culture-sensitive collation.
  3. The header `Available system prompts:` and a blank line are emitted.
  4. For each record, three lines are emitted — `- <name>` (with ` (current)` appended when the name equals the active-prompt name), `  Description: <description>`, and `  Created: <created local short date+time>, ` followed by either `Last used: <last-used local short date+time>` or the literal `Never used` — then a blank line.
  5. The line `System prompts directory: <value>` is emitted (see QUIRK-5.2).
  6. The two footer lines `Use '/prompt show <n>' to view a prompt's content` and `Use '/prompt use <n>' to switch to another prompt` are emitted.
- *Alternate flows:*
  - **A1** — The library is empty: the entire output is the single line `No system prompts found.` and the result is a **success**.
- *Error flows:*
  - **E1** — Individual record files unreadable: each produces `Error loading system prompt from <full path>: <reason>` on standard output and is omitted from the listing; the listing still succeeds. In the full-screen shell these lines go to the raw console behind the interface and are effectively invisible.
  - **E2** — The store directory has been removed or made unreadable since launch: directory enumeration is unguarded, so the failure escapes the store and surfaces as `Error processing prompt command: <message>`.
- *Postconditions:* No state change. The library was read from disk twice (QUIRK-5.3).

**UC-5.D — Switch the active prompt (realizes US-5.5, US-5.6)**

- *Preconditions:* Store constructed; settings loaded.
- *Main flow:*
  1. User types `/prompt use <name>`.
  2. The store is asked for `<name>`; the record is found.
  3. The active-prompt **name** in settings is set to `<name>`.
  4. The runtime active-prompt **text** is set to the record's text (runtime-only; never persisted).
  5. Settings are written to disk.
  6. The record's last-used timestamp is set to the current UTC instant and the record is rewritten.
  7. `Now using system prompt: <name>` is returned as a success.
- *Alternate flows:*
  - **A1** — The user instead types `/set systemPrompt <name…>`: all remaining tokens are joined with single spaces to form the name, so multi-word names are reachable here and only here. On success the same four side effects occur and the message is `Set systemprompt = <name>` — note the key echoed in lower case.
  - **A2** — The user presses **Use** in the graphical dialog: the active name and text are set and settings are persisted, but the last-used timestamp is **not** stamped.
- *Error flows:*
  - **E1** — No name token: `Please specify a prompt name: /prompt use <n>`; failure result; no state change.
  - **E2** — Unknown name via `/prompt use`: `Prompt not found: <name>`; failure result; neither settings nor any record file is modified.
  - **E3** — Unknown name via the settings command: `System prompt not found: <name>. Use '/prompt list' to see available prompts or '/prompt create <name>' to create a new one.`
  - **E4** — The settings command is given the key with no value token: `Usage: /set <key> <value>` — the arity guard fires before any lookup, so an empty prompt name is unreachable through that entry point.
  - **E5** — Graphical **Use** fails: an error box reading `Failed to set system prompt: <message>`.
  - **E6** — Record vanished between the lookup and the last-used stamp: the stamp is a silent no-op.
- *Postconditions:* Settings file rewritten; the selected record's file rewritten with a new last-used timestamp; the next chat turn uses the new text.

**UC-5.E — Author a new prompt, command path (realizes US-5.7, US-5.8)**

- *Preconditions:* Store constructed. The plain console shell with a real, line-buffered standard input.
- *Main flow:*
  1. User types `/prompt create <name> [description words…]`.
  2. No record with that name exists, so a new record is created with: the given name; the **built-in fallback text** as its content (creation never asks for content); a description formed by joining the remaining tokens with single spaces; created-at = the current UTC instant; last-used unset.
  3. The success message is emitted: `Created new system prompt: <name>`, a blank line, `Use '/prompt edit <name>' to edit the content.`, `Use '/prompt use <name>' to start using it.`
  4. User types `/prompt edit <name>`.
  5. The command takes over the terminal and prints `Editing system prompt: <name>`, `Enter the new content below. Type 'END' on a line by itself when finished.`, `Current content:`, the existing text, a blank line, `New content (END to finish):`.
  6. Lines are read from standard input until a line exactly equal to `END` (case-sensitive, no trimming) is read. All prior lines, each terminated by the platform newline, form the new text; trailing whitespace and newlines are stripped.
  7. The record is saved with description, created-at and last-used unchanged.
  8. If the edited prompt is the active one, the runtime active text is refreshed in memory; settings are not re-saved.
  9. `Updated system prompt: <name>` is returned.
- *Alternate flows:*
  - **A1** — No description tokens given at create: the description becomes the literal `Custom prompt: <name>`.
  - **A2** — The user supplies extra tokens after the name on `create`: they are absorbed into the description.
- *Error flows:*
  - **E1** — No name token on create: `Please specify a prompt name: /prompt create <n> [description]`.
  - **E2** — A record with that name already exists: `Prompt already exists: <name>. Use '/prompt edit <name>' to modify it.` — creation is non-destructive.
  - **E3** — No name token on edit: `Please specify a prompt name: /prompt edit <n>`.
  - **E4** — Unknown name on edit: `Prompt not found: <name>. Use '/prompt create <name>' to create it.`
  - **E5** — The record cannot be written: the store raises an operation error carrying `Error saving system prompt: <reason>`, surfaced as `Error processing prompt command: Error saving system prompt: <reason>`.
  - **E6** — Standard input is redirected and reaches end-of-input without an `END` line: the read loop never terminates; it keeps appending blank lines and grows memory without bound (QUIRK-5.4).
  - **E7** — `/prompt edit` is invoked inside the full-screen shell: its raw console output never reaches the chat pane and its raw console input never arrives; the subcommand is effectively unusable there (QUIRK-5.5).
- *Postconditions:* One new record file; then one rewritten record file.

**UC-5.F — Delete a prompt (realizes US-5.10)**

- *Preconditions:* Store constructed; settings loaded.
- *Main flow:*
  1. User types `/prompt delete <name>`.
  2. The record exists and its name is **not** the active-prompt name.
  3. The record file is removed.
  4. `Deleted system prompt: <name>` is returned. There is no confirmation step on the command path.
- *Alternate flows:*
  - **A1** — Graphical path: **Delete** shows a confirmation box asking `Are you sure you want to delete the prompt '<name>'?` with buttons **Yes** then **No**; only the first button deletes. On success a box reads `Prompt '<name>' deleted successfully`. **The graphical path has no active-prompt guard**, so it can delete the prompt the assistant is currently using.
- *Error flows:*
  - **E1** — No name token: `Please specify a prompt name: /prompt delete <n>`.
  - **E2** — Unknown name: `Prompt not found: <name>`.
  - **E3** — The name equals the active-prompt name (command path only): `Cannot delete the currently active prompt. Switch to another prompt first with '/prompt use <n>'.`
  - **E4** — The record file cannot be removed: the store raises `Error deleting system prompt: <reason>`; the command surfaces `Error processing prompt command: Error deleting system prompt: <reason>`; the graphical path shows `Failed to delete prompt: <reason>`.
  - **E5** — Deleting a record that is already gone at the storage layer: silent success, no message change.
- *Postconditions:* One record file removed. If the library is now empty, the four built-ins reappear at the next launch.

**UC-5.G — Export a prompt to a text file (realizes US-5.11)**

- *Preconditions:* The named record exists.
- *Main flow:*
  1. User types `/prompt export <name> [file_path]`.
  2. The destination is the third token if present; otherwise the user Documents directory joined with `chatdbg_prompt_<sanitized name>.txt`.
  3. **Only the prompt's text** is written, as plain text, with no metadata and no wrapper. Any existing file at that path is overwritten without asking.
  4. `Exported system prompt to: <path>` is returned.
- *Alternate flows:*
  - **A1** — Graphical path: **Export…** opens a path dialog pre-filled with the *user home* directory joined with `<name>.txt` — a different default directory, and the name is **not** sanitized. Success reads `Prompt '<name>' exported successfully to <path>`.
  - **A2** — Extra tokens after the path are silently ignored.
- *Error flows:*
  - **E1** — No name token: `Please specify a prompt name: /prompt export <n> [file_path]`.
  - **E2** — Unknown name: `Prompt not found: <name>`.
  - **E3** — Destination unwritable: `Error exporting prompt: <message>` (failure result). Graphical path: an error box `Failed to export prompt: <reason>` and the path dialog stays open.
  - **E4** — Empty path in the graphical dialog: error box `File path is required`; the dialog stays open.
  - **E5** — A path beginning with `~`: there is no home-directory expansion and no environment-variable expansion anywhere in this feature, so `~` is treated as a literal relative directory name that does not exist and the command fails with `Error exporting prompt: <reason>` (QUIRK-5.17).
  - **E6** — On a host whose "Documents" well-known folder resolves to the empty string, the default destination collapses to the bare relative name `chatdbg_prompt_<name>.txt` in the process's current working directory, and the success message shows no directory at all (QUIRK-5.18).
- *Postconditions:* One plain-text file written. Description, created-at and last-used are **not** exported; a round trip loses them.

**UC-5.H — Import a text file as a prompt (realizes US-5.12)**

- *Preconditions:* A readable text file exists at the given path.
- *Main flow:*
  1. User types `/prompt import <name> <file_path> [description words…]`.
  2. The file's existence is checked.
  3. The file is read **verbatim as the prompt text**. There is no format validation of any kind.
  4. A record is created with the given name, that text, a description formed from the remaining tokens joined by spaces, created-at = the current UTC instant, and last-used unset.
  5. The success message is emitted: `Imported system prompt: <name>`, `Description: <description>`, `Length: <character count> characters`, a blank line, `Use '/prompt use <name>' to start using it.`
- *Alternate flows:*
  - **A1** — No description tokens: the description defaults to `Imported from: <file name without directory>`.
  - **A2** — A record with the same name already exists: it is **silently overwritten**; there is no existence check and no warning (contrast create).
  - **A3** — Graphical path: an import form with Prompt Name, File Path (plus a **Browse…** button that does not browse — it opens a plain text-entry box titled `Enter File Path`) and Description. A blank description is stored as the empty string here, **not** defaulted to `Imported from: …`. File existence is not pre-checked.
- *Error flows:*
  - **E1** — Fewer than two operands: `Please specify both prompt name and file path: /prompt import <n> <file_path> [description]`.
  - **E2** — File absent (command path): `File not found: <file_path>`.
  - **E3** — File absent or unreadable (graphical path): surfaces as `Failed to import prompt: <reason>`.
  - **E4** — Any read or save failure on the command path: `Error importing prompt: <message>` (which may itself wrap `Error saving system prompt: <reason>`).
  - **E5** — Blank name or blank file path in the graphical form: error boxes `Name is required` and `File path is required`; the form stays open.
  - **E6** — A `~`-prefixed path: `File not found: ~/…` — no expansion (QUIRK-5.17).
- *Postconditions:* One record file created or replaced.

**UC-5.I — Manage prompts from the graphical dialog (realizes US-5.9, US-5.13)**

- *Preconditions:* The full-screen terminal shell is running. Store constructed.
- *Main flow:*
  1. User opens **Tools → System Prompts…**. A modal window titled **System Prompts**, 80 columns by 25 rows, appears with a framed list titled **Available Prompts** and one row of buttons: **Show, Use, Create…, Edit…, Delete, Import…, Export…, Close**.
  2. The list is loaded from disk and each row renders as `<name> - <description>`, ordered by name.
  3. The user selects a row and presses an action button. Selection is by list index into that same ordered collection; the guard is "index within range".
  4. **Show** opens a read-only, word-wrapped viewer 80 by 20 titled `Prompt: <name>` with the description on the second-to-last line.
  5. **Create…** opens an 80 by 20 form with Name, Description and multi-line Content. Name and Content are required; all three fields are trimmed; description may be blank and is stored as the empty string. Saving reports `Prompt '<name>' created successfully`. **There is no duplicate-name check — an existing prompt of that name is silently overwritten.**
  6. **Edit…** opens an 80 by 20 form pre-filled with the selected record's description and content. Content is required; description is optional. It saves description **and** content — this is the only surface anywhere that can change a description after creation. Success reads `Prompt '<name>' updated successfully`. It does **not** refresh the runtime active text when the edited prompt is the active one.
  7. After create, edit, delete or import the list is reloaded from disk.
  8. **Close** dismisses the dialog and the main window's status line is re-rendered.
- *Alternate flows:*
  - **A1** — The library is empty: any action button produces an error box reading `Please select a prompt first`.
  - **A2** — The library is non-empty and the user has touched nothing: the alphabetically first prompt already counts as selected, so every action button targets it (QUIRK-5.19).
- *Error flows:*
  - **E1** — Blank name or blank content on create: error boxes `Name is required` / `Content is required`; the form stays open.
  - **E2** — Blank content on edit: `Content is required`; the form stays open.
  - **E3** — Save failures: `Failed to create prompt: <message>`, `Failed to update prompt: <message>`, `Failed to import prompt: <message>`, `Failed to delete prompt: <message>`, `Failed to set system prompt: <message>`, `Failed to export prompt: <message>`.
  - **E4** — A failure during the post-mutation list reload has **nowhere to surface**: it runs on a background path with no handler and fails silently.
  - **E5** — The store directory becomes unreadable while the dialog is open: the list reload fails silently.
- *Postconditions:* Record files created, replaced or removed; the list reflects disk; the main status line re-rendered.

---

**Functional requirements**

*Store location and bootstrapping*

- **FR-5.1** — The prompt library SHALL be stored as one record file per prompt inside a directory formed by joining the operating system's per-user **local application-data** directory, the fixed folder name `ChatDbg`, and the fixed subdirectory name `system_prompts`. (realizes US-5.1)
- **FR-5.2** — On a Windows host that resolves to `C:\Users\<user>\AppData\Local\ChatDbg\system_prompts`; on a Linux host to `~/.local/share/ChatDbg/system_prompts`, or `$XDG_DATA_HOME/ChatDbg/system_prompts` when that variable is set. This is the **only** indirect environment sensitivity in the feature.
- **FR-5.3** — The store SHALL accept an optional base-directory override that replaces the per-user local application-data root. A whitespace-only or empty override SHALL fall back to the default. No shipping shell supplies an override; only the automated tests do.
- **FR-5.4** — The store directory tree SHALL be created at store construction if it does not exist.
- **FR-5.5** — Store construction SHALL be synchronous and blocking, SHALL happen once per application launch before any user interface exists, and SHALL pay the cost of one directory listing on every launch even when the library is already populated.
- **FR-5.6** — At construction, after loading, if the number of successfully loaded records is exactly `0`, the store SHALL write the four built-in prompts. The threshold is the literal count zero; this is a business rule, not a tunable. (realizes US-5.1)
- **FR-5.7** — The four built-ins SHALL be written in the fixed order `default`, `code-reviewer`, `algorithm-helper`, `security-expert`. That order is never observable because every listing re-sorts.
- **FR-5.8** — Seeded records SHALL have created-at equal to the UTC instant of seeding and an unset last-used timestamp.
- **FR-5.9** — The seeding check SHALL run at **every** application launch, so emptying the library causes all four built-ins to reappear on the next launch.
- **FR-5.10** — A directory containing only unparseable files SHALL count as empty for the purposes of FR-5.6, and the seeding write SHALL overwrite any of those files whose names collide with the four built-in file names.
- **FR-5.11** — Exactly **four** built-in prompts SHALL ship, with these verbatim names, descriptions and texts (realizes US-5.1):

  | Name | Description | Text |
  |---|---|---|
  | `default` | `Default system prompt for general debugging assistance` | `You are ChatDBG, a helpful debugging assistant. Help users analyze code, debug issues, and understand programming concepts.` |
  | `code-reviewer` | `System prompt for code review assistance` | `You are ChatDBG in code review mode. Analyze code for bugs, security issues, performance problems, and maintainability concerns. Provide specific, actionable feedback with examples of how to improve the code.` |
  | `algorithm-helper` | `System prompt for algorithm assistance` | `You are ChatDBG in algorithm mode. Help users understand, design, and optimize algorithms. Provide step-by-step explanations, time and space complexity analysis, and pseudocode when helpful.` |
  | `security-expert` | `System prompt for security-focused assistance` | `You are ChatDBG in security expert mode. Help users identify and fix security vulnerabilities in their code. Focus on common issues like injection attacks, authentication problems, authorization flaws, data exposure, and insecure dependencies.` |

*Record model, file naming and serialization*

- **FR-5.12** — A prompt record SHALL carry exactly five fields: **name** (short text), **content** (long free multi-line text — the instruction text handed to the model), **description** (short text, display-only), **created-at** (UTC timestamp) and **last-used-at** (nullable UTC timestamp).
- **FR-5.13** — The record file name SHALL be `<sanitized name>.json`, where sanitization replaces **every character the host operating system forbids in a file name** with a single underscore `_`.
- **FR-5.14** — The forbidden-character set is platform-dependent and was measured at the source runtime as **41 characters on Windows** (code points 0–31 plus `"` `<` `>` `|` `:` `*` `?` `\` `/`) and **2 characters on Linux** (code point 0 and `/`). A reimplementation MUST pick one explicit, documented set rather than delegating to its host, because delegating makes libraries non-portable between operating systems.
- **FR-5.15** — Sanitization SHALL be the **only** name validation anywhere in the store. Empty names, whitespace-only names, `.`, `..` and operating-system reserved device names SHALL all be accepted.
- **FR-5.16** — Because sanitization is lossy, two distinct names MAY map to one file (on Windows `a/b`, `a:b` and `a_b` all become `a_b.json`); the later save wins and the earlier record is lost. Because the result is always `<store dir>/<sanitized>.json`, `.` and `..` cannot escape the store directory.
- **FR-5.17** — Lookup of a record by name SHALL be file-existence based, so name matching inherits the host file system's case sensitivity: case-insensitive on default Windows and default macOS, case-sensitive on typical Linux. *(INFERRED from the lookup being file-existence based; the underlying file systems' case behaviour was not exercised.)*
- **FR-5.18** — Listing SHALL enumerate only files matching `*.json` directly inside the store directory, without recursing into subdirectories.
- **FR-5.19** — A record file whose stored `name` field disagrees with its file name SHALL be listed under the stored name but SHALL NOT be fetchable or deletable by that name.
- **FR-5.20** — A record file that fails to parse SHALL be skipped, and the line `Error loading system prompt from <full path>: <reason>` SHALL be written to standard output; listing SHALL continue and SHALL still succeed.
- **FR-5.21** — A record file containing only the literal `null` document SHALL deserialize to nothing and SHALL be skipped **without any message**.
- **FR-5.22** — Record files SHALL be written pretty-printed/indented, **except** the four seeded built-ins, which are written compact on a single line (QUIRK-5.1).
- **FR-5.23** — An unset last-used timestamp SHALL be written as an explicit null value; the key SHALL never be omitted. Every record file therefore always carries all five keys.
- **FR-5.24** — Field-key matching on read SHALL be case-sensitive; a hand-edited file using a differently-cased key loses that field to its default value.
- **FR-5.25** — Created-at SHALL default to the UTC instant the in-memory record is constructed, **including** when deserializing a file that omits the field.
- **FR-5.26** — The record file wire format is the following flat object, keys in this declaration order:

  ```
  Prompt record file (one record per file; JSON per RFC 8259; UTF-8, no byte-order mark)
  {
    "name":        string,             // primary key; also the basis of the file name
    "content":     string,             // the instruction text; may contain newlines
    "description": string,             // display-only
    "createdAt":   string,             // ISO-8601 round-trip form, UTC, "Z" suffix
    "lastUsedAt":  string | null       // ISO-8601 round-trip UTC, or explicit null
  }
  ```
  A measured seeded file is exactly one line, e.g. `{"name":"default","content":"You are ChatDBG, …","description":"Default system prompt for general debugging assistance","createdAt":"2026-08-28T22:21:03.1234567Z","lastUsedAt":null}`.

*Identity, ordering and timestamps*

- **FR-5.27** — The prompt **name** SHALL be the record's identity. Uniqueness SHALL be enforced **only** by the `/prompt create` path; every other write path (command import, graphical create, graphical import, graphical edit, last-used stamping) overwrites.
- **FR-5.28** — **Every** listing, in both the text and the graphical surface, SHALL be sorted ascending by name using the platform's default **culture-sensitive** string collation, not byte/ordinal ordering. Measured: the names `a-b`, `ab`, `a_b`, `B`, `a` sort to `a`, `a_b`, `a-b`, `ab`, `B` culture-sensitively but to `B`, `a`, `a-b`, `a_b`, `ab` ordinally. (realizes US-5.3)
- **FR-5.29** — The graphical list SHALL re-sort the already-sorted collection by name a second time. This is harmless and stable, but the double sort is observable only as wasted work.
- **FR-5.30** — The runtime fallback text — used whenever the named active prompt cannot be loaded, and also used as the content of every prompt created through `/prompt create` — SHALL be the literal string `You are ChatDBG, a helpful debugging assistant. Help users analyze code, debug issues, and understand programming concepts.` This is the same string as the `default` built-in's text. In the source it is hard-coded in five separate places.
- **FR-5.31** — All timestamps SHALL be **stored** in UTC and **displayed** converted to local time using the current culture's "general short date and time" pattern — date plus hours and minutes, **no seconds**. Measured under the invariant culture: `08/28/2026 22:21`.
- **FR-5.32** — An unset last-used timestamp SHALL render as the literal `Never used`.
- **FR-5.33** — Last-used SHALL be stamped — set to the current UTC instant, with the whole record then rewritten — by exactly three paths: `/prompt use`, `/set systemPrompt`, and plain-console-shell startup resolution. It SHALL **not** be stamped by: the graphical **Use** button, full-screen-shell startup resolution, `/prompt` with no arguments, `/prompt show`, or sending a chat message.
- **FR-5.34** — Stamping last-used on a name that no longer exists SHALL be a silent no-op.

*Active prompt semantics*

- **FR-5.35** — The active prompt SHALL be identified by **name only**. The name is a persisted setting whose default value is the literal `default`. (realizes US-5.5)
- **FR-5.36** — The active prompt's **text** SHALL be a runtime-only value explicitly excluded from the settings file, and SHALL be re-derived from the library at every launch. (realizes US-5.14)
- **FR-5.37** — Every select operation — `/prompt use`, `/set systemPrompt`, and the graphical **Use** button — SHALL persist settings to disk immediately. (realizes US-5.5)
- **FR-5.38** — There SHALL be no referential integrity between the settings' active-prompt name and the library: settings may name a prompt that does not exist, and the product SHALL still start (UC-5.B A1).
- **FR-5.39** — The provider layer SHALL read the runtime active-prompt text as a plain string and inject it as the conversation's system message on every model call, for every backend. This feature SHALL never call a model itself. (realizes US-5.15)
- **FR-5.40** — The active prompt **name** SHALL be surfaced in the plain-console launch banner as `System Prompt: <name>` and in the full-screen status line as `Provider: <p> | Model: <m> | Prompt: <name>`. The settings display SHALL show `- System Prompt: <name>` and the hint `- System Prompt: /prompt list (then: /set systemPrompt <n>)`. (realizes US-5.16)

*Command surface — parsing and dispatch*

- **FR-5.41** — A line beginning with `/` SHALL be treated as a command; the remainder SHALL be split on the space character with empty entries discarded. Consequence: runs of spaces collapse, tab characters are **not** separators, and there is **no quoting or escaping** of any kind.
- **FR-5.42** — The command word SHALL be lower-cased for dispatch and the `/prompt` subcommand token SHALL be lower-cased before matching; **prompt names and file paths SHALL be case-preserved**.
- **FR-5.43** — `/prompt show`, `use`, `create`, `delete`, `edit` and `export` SHALL take the prompt name from a **single token**, so names containing spaces are unreachable through them. `/prompt import` likewise takes single tokens for both operands. Only `/set systemPrompt` joins the remaining tokens, so it alone can address a multi-word name. (realizes US-5.6)
- **FR-5.44** — Extra trailing tokens SHALL be silently ignored by `show`, `use`, `delete` and `edit`; absorbed into the description by `create` and `import`; and, for `export`, token three is the path and the rest are ignored.
- **FR-5.45** — The `/prompt` command SHALL be registered with the one-line description `Manage system prompts for AI responses`, SHALL be listed on the help screen under the heading `System Prompt Management`, and SHALL carry the usage line `/prompt [list|show <n>|use <n>|create <n>|delete <n>|edit <n>|export <n>|import <file>] - Manage system prompts`.
- **FR-5.46** — Every usage and error string that means "a name" SHALL use the literal placeholder token `<n>` — not `<name>` — because that spelling is consistent across the source, the error messages and the user manual. A clone that "corrects" it deviates from observed output.
- **FR-5.47** — Any first token other than `list`, `show`, `use`, `create`, `delete`, `edit`, `export`, `import` SHALL produce the failure message `Unknown subcommand: <token>. Use list, show, use, create, delete, edit, export, or import.`
- **FR-5.48** — Every invocation SHALL return a triple of (succeeded?, message text, exit-requested?). This command SHALL never request exit.

*Command surface — individual subcommands*

- **FR-5.49** — `/prompt` with no arguments SHALL look up the record whose name equals the active-prompt name, use the found record's text or — if no such record exists — the runtime active text, and emit exactly (realizes US-5.2):
  `Current system prompt: <activeName>`, blank line, `Content:`, the text, blank line, `Use '/prompt list' to see all available prompts`, `Use '/prompt use <n>' to switch to another prompt`. It SHALL NOT stamp last-used and SHALL have no other side effect.
- **FR-5.50** — `/prompt list` output SHALL follow UC-5.C step 3–6 exactly, with the header `Available system prompts:`, per-entry blocks and a trailing blank line after each entry, and the two footer lines. (realizes US-5.3)
- **FR-5.51** — The suffix ` (current)` SHALL be appended to exactly the entry whose name equals the active-prompt name, and to no other.
- **FR-5.52** — `/prompt list` against an empty library SHALL return the single line `No system prompts found.` as a **success**.
- **FR-5.53** — `/prompt list` SHALL emit a line intended to read `System prompts directory: <store directory path>`. In the source this prints the wrong value (QUIRK-5.2); a reimplementation SHALL print the real directory path and SHALL record that as a deliberate deviation.
- **FR-5.54** — `/prompt show <name>` SHALL emit `Prompt: <name>`, `Description: <description>`, `Created: <created local short>`, then either `Last used: <last-used local short>` or `Never used`, then a blank line, `Content:`, and the text. It SHALL have no side effects. (realizes US-5.4)
- **FR-5.55** — Missing-name errors SHALL be, verbatim and per subcommand: `Please specify a prompt name: /prompt show <n>`; `Please specify a prompt name: /prompt use <n>`; `Please specify a prompt name: /prompt create <n> [description]`; `Please specify a prompt name: /prompt delete <n>`; `Please specify a prompt name: /prompt edit <n>`; `Please specify a prompt name: /prompt export <n> [file_path]`; and `Please specify both prompt name and file path: /prompt import <n> <file_path> [description]`.
- **FR-5.56** — Unknown-name errors SHALL be `Prompt not found: <name>` for `show`, `use`, `delete` and `export`, and `Prompt not found: <name>. Use '/prompt create <name>' to create it.` for `edit`.
- **FR-5.57** — `/prompt use <name>` SHALL, in this exact order: set the active-prompt name; set the runtime active-prompt text; persist settings; stamp the record's last-used and rewrite it; then return `Now using system prompt: <name>`. The ordering is a guarantee — settings are saved before the record is rewritten. (realizes US-5.5)
- **FR-5.58** — `/set systemPrompt <name…>` SHALL join **all** remaining tokens with single spaces to form the name, validate it against the library, set the active name and text, stamp last-used, persist settings, and return `Set systemprompt = <name>` with the key echoed lower-cased. On an unknown name it SHALL return `System prompt not found: <name>. Use '/prompt list' to see available prompts or '/prompt create <name>' to create a new one.` (realizes US-5.6)
- **FR-5.59** — The settings command SHALL refuse any key given with no value token, returning `Usage: /set <key> <value>` before any lookup runs, so an empty prompt name is unreachable through that entry point.
- **FR-5.60** — When the settings command is constructed without a library reference it SHALL set the active name only and validate nothing. This path is unreachable in the shipping product.
- **FR-5.61** — `/prompt create <name> [description…]` SHALL refuse an existing name with `Prompt already exists: <name>. Use '/prompt edit <name>' to modify it.`, SHALL default an omitted description to the literal `Custom prompt: <name>`, SHALL give the new record the FR-5.30 fallback text, SHALL set created-at to now (UTC) and leave last-used unset, and SHALL return `Created new system prompt: <name>`, blank line, `Use '/prompt edit <name>' to edit the content.`, `Use '/prompt use <name>' to start using it.` (realizes US-5.7)
- **FR-5.62** — `/prompt edit <name>` SHALL enter an interactive content-entry mode on the plain console, printing the preamble of UC-5.E step 5 verbatim, and SHALL read lines until a line **exactly equal** to `END` (case-sensitive, no trimming) is read. (realizes US-5.8)
- **FR-5.63** — The collected lines, each terminated by the platform newline, SHALL form the new text, with trailing whitespace and newlines stripped. Description, created-at and last-used SHALL be unchanged. There SHALL be no cancel path out of content-entry mode other than typing `END`.
- **FR-5.64** — If the edited prompt is the active one, the runtime active text SHALL be refreshed in memory; settings SHALL NOT be re-saved (harmless, because the active text is not a persisted field).
- **FR-5.65** — There SHALL be **no command** that changes a prompt's description. The graphical edit form is the only surface that can.
- **FR-5.66** — `/prompt delete <name>` SHALL refuse when the name equals the active-prompt name, with `Cannot delete the currently active prompt. Switch to another prompt first with '/prompt use <n>'.`, and otherwise SHALL remove the record and return `Deleted system prompt: <name>` with no confirmation step. (realizes US-5.10)
- **FR-5.67** — `/prompt export <name> [file_path]` SHALL write **only the prompt text**, as plain text, to the third token when present, otherwise to the user Documents directory joined with `chatdbg_prompt_<sanitized name>.txt` using the same sanitization rule as FR-5.13. It SHALL overwrite any existing file at that path without asking, and SHALL return `Exported system prompt to: <path>` or, on failure, `Error exporting prompt: <message>`. (realizes US-5.11)
- **FR-5.68** — Destination and source paths SHALL be used **exactly as typed**: no home-directory (`~`) expansion, no environment-variable expansion, and no directory creation.
- **FR-5.69** — `/prompt import <name> <file_path> [description…]` SHALL pre-check the file's existence (`File not found: <file_path>` when absent), read the file **verbatim as the prompt text** with no format validation, default an omitted description to `Imported from: <file name without directory>`, set created-at to now (UTC) with last-used unset, **silently overwrite** any existing record of the same name, and return `Imported system prompt: <name>`, `Description: <description>`, `Length: <character count of the file text> characters`, blank line, `Use '/prompt use <name>' to start using it.` On failure it SHALL return `Error importing prompt: <message>`. (realizes US-5.12)
- **FR-5.70** — Export and import SHALL be asymmetric with the stored record: export emits bare text, so description, created-at and last-used are lost on a round trip, and importing a stored record file yields a prompt whose *text* is that file's raw markup.

*Graphical management dialog*

- **FR-5.71** — The full-screen shell SHALL offer **Tools → System Prompts…**, opening a modal window **80 columns by 25 rows** titled **System Prompts**, containing a framed list titled **Available Prompts** and one row of eight buttons in this order: **Show, Use, Create…, Edit…, Delete, Import…, Export…, Close**. (realizes US-5.13)
- **FR-5.72** — List rows SHALL render as `<name> - <description>`, ordered by name per FR-5.28.
- **FR-5.73** — Every action button SHALL guard on the list's selected index being within range, and SHALL show an error box reading `Please select a prompt first` when it is not. In practice that box can appear **only when the library is empty**, because the selected index starts at zero and remains zero after the list is populated (QUIRK-5.19).
- **FR-5.74** — **Show** SHALL open a read-only, word-wrapped viewer **80 by 20** titled `Prompt: <name>` with the description on the second-to-last line.
- **FR-5.75** — **Use** SHALL write the active name and text into settings, persist settings, and show `Now using system prompt: <name>`; on failure `Failed to set system prompt: <message>`. It SHALL **not** stamp last-used.
- **FR-5.76** — **Create…** SHALL open an **80 by 20** form with Name, Description and multi-line Content; all three fields SHALL be trimmed; Name and Content SHALL be required (`Name is required`, `Content is required`); a blank description SHALL be stored as the empty string; there SHALL be **no duplicate-name check**; success reports `Prompt '<name>' created successfully` and failure `Failed to create prompt: <message>`. (realizes US-5.9)
- **FR-5.77** — **Edit…** SHALL open an **80 by 20** form pre-filled with the selected record's description and content, SHALL require content, SHALL save **both** description and content, and SHALL report `Prompt '<name>' updated successfully` or `Failed to update prompt: <message>`. It SHALL NOT refresh the runtime active text when the edited prompt is the active one. (realizes US-5.9)
- **FR-5.78** — **Delete** SHALL ask `Are you sure you want to delete the prompt '<name>'?` with buttons **Yes** then **No**, where only the first button (index 0) deletes, then report `Prompt '<name>' deleted successfully` or `Failed to delete prompt: <message>`. **There SHALL be no active-prompt guard on this path.**
- **FR-5.79** — **Import…** SHALL open a **70 by 15** form with Prompt Name, File Path (each field **40 columns** wide) and Description, plus a **Browse…** button, with Import/Cancel buttons on row 10. Name and file path SHALL be required. A blank description SHALL be stored as the empty string — **not** defaulted to `Imported from: …`. File existence SHALL NOT be pre-checked. Success reports `Prompt '<name>' imported successfully`, failure `Failed to import prompt: <message>`.
- **FR-5.80** — **Browse…** SHALL open a **60 by 8** dialog titled **Enter File Path** with a single text field and OK/Cancel; OK copies a non-empty value into the file-path field. There SHALL be no file listing, no completion and no existence check.
- **FR-5.81** — **Export…** SHALL open a **70 by 8** dialog titled **Enter Export File Path**, pre-filled with the *user home* directory joined with `<name>.txt` — **unsanitized**, and a different default directory from the command path. OK writes the prompt text and reports `Prompt '<name>' exported successfully to <path>`; an empty path reports `File path is required`; failure reports `Failed to export prompt: <message>`.
- **FR-5.82** — After create, edit, delete or import the list SHALL be reloaded from disk.
- **FR-5.83** — Closing the dialog SHALL refresh the main window's status line.
- **FR-5.84** — Dialog layout constants — window sizes 80×25, 80×20, 70×15, 70×8 and 60×8; action-button column offsets 2, 12, 20, 33, 45, 56 and 69; 1-cell right and bottom margins; the button row anchored one row above the window's bottom edge; the list frame leaving three rows of headroom — SHALL be treated as **layout tunables**, not business rules. They do not reflow.

*Errors, concurrency and limits*

- **FR-5.85** — Any otherwise-unhandled failure inside the `/prompt` command SHALL be caught at the top and rendered as `Error processing prompt command: <message>`. No stack trace, no logging, no telemetry.
- **FR-5.86** — A record write failure SHALL raise an operation error carrying `Error saving system prompt: <reason>`; a delete failure SHALL carry `Error deleting system prompt: <reason>`. These strings SHALL appear nested inside the caller's own message (e.g. `Error processing prompt command: Error saving system prompt: <reason>`).
- **FR-5.87** — A fetch of a malformed or unreadable record SHALL write `Error loading system prompt from <full path>: <reason>` to standard output and report **not found**, so a corrupt record is indistinguishable from a missing one and callers say `Prompt not found: <name>`.
- **FR-5.88** — Deleting a record that is already absent SHALL be a silent success.
- **FR-5.89** — Failure to **enumerate** the store directory SHALL NOT be caught by the store; it surfaces as `Error processing prompt command: <message>` on the command path and fails silently on the graphical list reload.
- **FR-5.90** — All failure messages SHALL be plain, unlocalized English embedded in the product. There SHALL be no error-code scheme, no structured logging, and nothing written to a log file by this feature.
- **FR-5.91** — There SHALL be **no** caching: every operation re-reads from disk. `/prompt list` reads the entire library **twice** per invocation, and the graphical dialog reloads the entire library after each mutation. The behavioural consequence that MUST survive any optimization is that externally-made edits to record files are picked up immediately.
- **FR-5.92** — There SHALL be **no** pagination, result limit, search or filter, and **no** upper bound on the number of prompts or on the length of a prompt's text.
- **FR-5.93** — There SHALL be **no** concurrency control: whole-file overwrite with no locking, no temp-file-and-rename, and no version token. Two shells running at once will clobber each other, a last-used stamp is a full read-modify-write that can overwrite a concurrent content edit, and a crash mid-write leaves a truncated file that is thereafter silently skipped as unparseable.
- **FR-5.94** — There SHALL be **no** cancellation support on any operation.
- **FR-5.95** — There SHALL be **no** permission model. Any code path in the process may read, write or delete any prompt; protection is entirely the operating system's file-permission model on a per-user directory. The directory is created with the platform default mode and no explicit permissions are set. Prompt contents are stored in clear text and are not treated as secret.
- **FR-5.96** — Export and import SHALL take a raw, unvalidated, unsanitized path and read or write anywhere the process can reach. Prompt **names**, by contrast, cannot escape the store directory (FR-5.16). A reimplementation on a multi-tenant or sandboxed platform MUST add path confinement and record that as a deliberate deviation.
- **FR-5.97** — All screen strings, error messages and built-in prompt texts SHALL be hard-coded English with no message catalogue. Timestamp rendering and name ordering, however, are culture-sensitive (FR-5.28, FR-5.31), so the same library renders differently under different locales — and differently again under a build profile that disables globalization data.
- **FR-5.98** — The library contract exposed to other features SHALL offer exactly six capabilities: fetch one prompt by name (returning nothing, never an error, when absent); stamp a prompt as just-used (silent no-op when absent); list all prompts ordered by name (never failing as a whole; bad files skipped); save a prompt as create-or-replace by name (raising a wrapped error on write failure); delete a prompt by name (silent success when absent, wrapped error on failure); and **report the store directory**. The sixth capability is absent from the source's contract, which is the direct cause of QUIRK-5.2; a reimplementation SHALL include it.

---

**External technology**

*Requires: local file system access — directory creation, non-recursive directory listing filtered by file extension, and whole-file read, write and delete of UTF-8 text (POSIX / Win32 file APIs). Source used: the platform runtime's standard file and directory APIs (.NET `System.IO`). Reimplementer notes: whole-file overwrite semantics with no locking, no temporary-file-and-rename, and no version token. Reads and writes are UTF-8 without a byte-order mark. No recursion into subdirectories. Per-file read failures are caught; directory-enumeration failures are not.*

*Requires: per-user well-known directory resolution — a per-user local application-data directory, a "Documents" directory, and a user home directory (XDG user directories on Linux, Known Folders on Windows). Source used: the platform runtime's known-folder lookup (.NET `Environment.GetFolderPath` for LocalApplicationData, MyDocuments and UserProfile). Reimplementer notes: measured at the source runtime — Windows returns `C:\Users\<user>\AppData\Local`, `C:\Users\<user>\Documents` and `C:\Users\<user>`; Linux with no XDG user-directories file returns `/home/<user>/.local/share`, the **empty string** for Documents, and `/home/<user>`. The empty Documents result is not an error path; it silently degrades the default export destination to a bare relative file name (QUIRK-5.18). A reimplementation must choose an explicit, non-empty default export directory and document it.*

*Requires: the host operating system's set of characters forbidden in file names. Source used: the platform runtime's invalid-filename-character query (.NET `Path.GetInvalidFileNameChars`). Reimplementer notes: platform-dependent and measured — 41 characters on Windows (code points 0–31 plus `"` `<` `>` `|` `:` `*` `?` `\` `/`), 2 on Linux (code point 0 and `/`). Delegating to the host makes libraries non-portable between operating systems (QUIRK-5.21) and changes which names silently collide. Pick one explicit set, document it, and do not delegate.*

*Requires: object serialization and deserialization to a text document with explicitly named fields and optional pretty-printing (JSON, RFC 8259). Source used: the platform runtime's JSON serializer with per-field name attributes and an indentation option (.NET `System.Text.Json`). Reimplementer notes: field names are fixed as `name`, `content`, `description`, `createdAt`, `lastUsedAt` and are matched **case-sensitively** — a hand-edited file using a differently-cased key loses that field to its default. Timestamps use ISO-8601 round-trip form with a `Z` suffix for UTC. An unset last-used timestamp is written as an explicit `null` and never omitted. Missing fields are tolerated and fall back to defaults; a malformed document raises an error this feature catches per file. Note the settings file (a different feature) uses a naming *policy* rather than per-field names — do not conflate the two formats.*

*Requires: a character-cell terminal user-interface toolkit providing modal windows, a list view with an integer selection index, single-line text fields, multi-line word-wrapped text views, buttons, message boxes and confirmation boxes returning the chosen button's index, and a primitive for marshaling work onto the main loop (ANSI/VT terminal rendering). Source used: Terminal.Gui 1.19.0 with NStack.Core 1.1.1. Reimplementer notes: needed only for the graphical management screen. The confirmation box returns the **index** of the chosen button, and the source treats index 0 as "delete". A fresh list view reports selected index 0 and still reports 0 immediately after its item source is set, which is what makes the "select a prompt first" guard nearly dead (QUIRK-5.19). All sizes are in character cells. No native file-open dialog is used.*

*Requires: console line input and line output. Source used: the platform runtime's standard console read-line / write-line. Reimplementer notes: used for the interactive content editor and for library load-error notices. Requires a real, line-buffered standard input; the source's read loop has no end-of-input check and hangs when input is exhausted (QUIRK-5.4).*

*Requires: wall-clock time in UTC, UTC-to-local conversion, and culture-aware short date-and-time formatting. Source used: the platform runtime's UTC clock, local-time conversion and "general short date/time" format (.NET `DateTime.UtcNow`, `ToLocalTime`, format specifier `g`). Reimplementer notes: storage is always UTC; only display is localized. The display format is date plus hours and minutes with **no seconds** — measured under the invariant culture as `08/28/2026 22:21`.*

*Requires: culture-aware string ordering (Unicode collation). Source used: the platform runtime's default current-culture string comparison. Reimplementer notes: affects list order for names containing punctuation or diacritics. Measured: `a-b, ab, a_b, B, a` orders as `a, a_b, a-b, ab, B` culture-sensitively but as `B, a, a-b, a_b, ab` by bytes. A byte-ordering clone renders the list visibly differently.*

*Requires: globalization data — collation tables and culture date formats (CLDR / ICU). Source used: the runtime's ICU-backed globalization, **disabled** in the product's two size-optimized publish profiles via an invariant-globalization switch. Reimplementer notes: turning globalization off changes **both** the list ordering and the timestamp rendering for the same library, so the same prompt library renders differently depending on which build of the product opened it. If the clone ships a size-optimized variant, this divergence must be reproduced or explicitly eliminated.*

*Requires: nothing else. This feature uses no network access, no database, no message broker and no operating-system credential store, and it reads **no environment variable** of its own — there is no override for the library directory, the active prompt, or the export directory. The library root is nonetheless indirectly environment-sensitive on Linux because the per-user-data lookup honours `$XDG_DATA_HOME`.*

*Requires (test harness only): a unit-test framework with mocking, if the clone ports the reference tests. Source used: xUnit 2.9.1 with Moq 4.20.69.*

---

**Acceptance criteria**

- **AC-5.1** — *Given* a machine where the directory `<local app data>/ChatDbg/system_prompts` does not exist, *when* the application starts, *then* the directory is created and exactly four record files exist for `default`, `code-reviewer`, `algorithm-helper`, `security-expert`, each carrying the verbatim text and description of FR-5.11, each with a created-at within one second of startup and a `lastUsedAt` of explicit `null`; *and* each of those four files is a single line with no indentation.
- **AC-5.2** — *Given* a library already containing at least one loadable prompt, *when* the application starts, *then* no built-in is created and no existing record is modified.
- **AC-5.3** — *Given* a library containing prompts named `a-b`, `ab`, `a_b`, `B` and `a`, *when* the user runs `/prompt list` under a normal (non-invariant) locale, *then* the entries appear in the order `a`, `a_b`, `a-b`, `ab`, `B` — culture-sensitive collation, not the byte order `B`, `a`, `a-b`, `a_b`, `ab`. Each entry renders as three lines followed by a blank line, and the block is preceded by `Available system prompts:` and a blank line.
- **AC-5.4** — *Given* the active prompt is `default`, *when* the user runs `/prompt list`, *then* exactly the `default` entry is suffixed with ` (current)` and no other entry is.
- **AC-5.5** — *Given* a library whose files have all been removed while the application is running, *when* the user runs `/prompt list`, *then* the only output is `No system prompts found.` and the result is a success.
- **AC-5.6** — *Given* a prompt named `code` whose text is `new content`, *when* the user runs `/prompt use code`, *then* the message is exactly `Now using system prompt: code`, the active-prompt name becomes `code`, the runtime active text becomes `new content`, the settings file is written exactly once, and `code`'s last-used timestamp is updated exactly once.
- **AC-5.7** — *Given* no prompt named `nope`, *when* the user runs `/prompt use nope`, *then* the command fails with exactly `Prompt not found: nope` and neither the settings file nor any record file is modified.
- **AC-5.8** — *Given* a prompt named `mine` already exists, *when* the user runs `/prompt create mine`, *then* the command fails with `Prompt already exists: mine. Use '/prompt edit mine' to modify it.` and the existing record is byte-identical afterwards.
- **AC-5.9** — *Given* no prompt named `mine`, *when* the user runs `/prompt create mine helper for my team`, *then* a prompt `mine` is created with description `helper for my team`, content equal to `You are ChatDBG, a helpful debugging assistant. Help users analyze code, debug issues, and understand programming concepts.`, a created-at of now, and no last-used; *and when* the user instead runs `/prompt create mine` with no further words, *then* the description is exactly `Custom prompt: mine`.
- **AC-5.10** — *Given* the active prompt is `default`, *when* the user runs `/prompt delete default`, *then* the command fails with `Cannot delete the currently active prompt. Switch to another prompt first with '/prompt use <n>'.` and the record file still exists; *and when* the user then runs `/prompt use code` followed by `/prompt delete default`, *then* it succeeds with `Deleted system prompt: default` and the file is gone.
- **AC-5.11** — *Given* a prompt `custom` was saved and then deleted, *when* it is fetched by name, *then* nothing is returned and no error is raised.
- **AC-5.12** — *Given* a prompt `demo` whose text is `hello`, *when* the user runs `/prompt export demo` on a Windows host, *then* a plain-text file containing exactly `hello` — no markup, no metadata — is written to `C:\Users\<user>\Documents\chatdbg_prompt_demo.txt` and the message is `Exported system prompt to: C:\Users\<user>\Documents\chatdbg_prompt_demo.txt`; *and when* the user runs `/prompt export demo /tmp/x.txt`, *then* `/tmp/x.txt` is used instead.
- **AC-5.13** — *Given* a text file of exactly 42 characters at `/tmp/p.txt`, *when* the user runs `/prompt import newone /tmp/p.txt`, *then* a prompt `newone` is created with that file's full text, description `Imported from: p.txt`, and the message reports `Length: 42 characters`; *and* if a prompt `newone` already existed, it is silently replaced with no warning.
- **AC-5.14** — *Given* the user runs `/prompt import onlyname`, *then* the command fails with `Please specify both prompt name and file path: /prompt import <n> <file_path> [description]`; *and given* `/prompt import x /no/such/file`, *then* it fails with `File not found: /no/such/file`.
- **AC-5.15** — *Given* the user runs `/prompt frobnicate`, *then* the command fails with exactly `Unknown subcommand: frobnicate. Use list, show, use, create, delete, edit, export, or import.`; *and given* `/PROMPT LIST`, *then* it behaves identically to `/prompt list`.
- **AC-5.16** — *Given* the settings file records active prompt `code-reviewer` and that prompt exists, *when* the application starts, *then* the runtime active text equals that prompt's stored text, its last-used timestamp is advanced, and the banner shows `System Prompt: code-reviewer`; *and given* the settings file records a name that no longer exists, *then* the application still starts, the runtime active text is `You are ChatDBG, a helpful debugging assistant. Help users analyze code, debug issues, and understand programming concepts.`, no error is shown, and the recorded name is left unchanged.
- **AC-5.17** — *Given* the graphical prompts dialog is opened against an **empty** library, *when* any of Show, Use, Edit, Delete or Export is pressed, *then* an error box reading `Please select a prompt first` appears and nothing changes.
- **AC-5.18** — *Given* the library contains `algorithm-helper`, `code-reviewer`, `default` and `security-expert` and the dialog has just opened with the user having pressed no key, *when* **Delete** then **Yes** is pressed, *then* `algorithm-helper` — the alphabetically first prompt — is deleted, a box reads `Prompt 'algorithm-helper' deleted successfully`, and the `Please select a prompt first` box does **not** appear.
- **AC-5.19** — *Given* the graphical Create form with a blank Name (or blank Content), *when* Create is pressed, *then* an error box `Name is required` (respectively `Content is required`) appears and the form remains open; *given* both are filled, *then* the prompt is saved, the form closes, the list refreshes, and a box reads `Prompt '<name>' created successfully`.
- **AC-5.20** — *Given* the active prompt is `security-expert` and it is the selected row, *when* the user presses **Delete** then **Yes** in the graphical dialog, *then* the record is removed with no active-prompt objection and a box reads `Prompt 'security-expert' deleted successfully` — unlike the command path, which refuses.
- **AC-5.21** — *Given* the active prompt is `default`, *when* the user runs `/prompt` with no arguments, *then* the result is a success whose message is exactly `Current system prompt: default`, a blank line, `Content:`, the prompt's stored text, a blank line, `Use '/prompt list' to see all available prompts`, `Use '/prompt use <n>' to switch to another prompt`; *and given* the recorded name refers to a prompt that no longer exists, *then* the same block is emitted with the runtime active text substituted and no error is raised.
- **AC-5.22** — *Given* a prompt named `code review` (with a space) exists, *when* the user runs `/prompt use code review`, *then* the command fails with `Prompt not found: code`; *and when* the user instead runs `/set systemPrompt code review`, *then* it succeeds, the active name becomes `code review`, last-used is stamped, and the message is exactly `Set systemprompt = code review`.
- **AC-5.23** — *Given* any library state, *when* the user runs `/set systemPrompt` with no value token, *then* the command fails with exactly `Usage: /set <key> <value>` and no library lookup is attempted.
- **AC-5.24** — *Given* a Windows host and a prompt created with the name `a:b`, *when* the library is listed, *then* the record is listed under the name `a:b` but its file is `a_b.json`; *and* if a prompt `a_b` is then created, it overwrites the same file and `a:b` is lost. *And given* the identical sequence on Linux, *then* two distinct files `a:b.json` and `a_b.json` exist and nothing is lost.
- **AC-5.25** — *Given* a prompt `demo` exists, *when* the user runs `/prompt export demo ~/Desktop/demo.txt`, *then* the command **fails** with `Error exporting prompt: <reason>` because `~` is never expanded; *and given* `/prompt import x ~/Desktop/demo.txt`, *then* it fails with `File not found: ~/Desktop/demo.txt`.
- **AC-5.26** — *Given* a Linux host with no XDG user-directory configuration, a prompt `demo` with text `hello`, and a working directory of `/work`, *when* the user runs `/prompt export demo`, *then* the file `/work/chatdbg_prompt_demo.txt` is created containing exactly `hello` and the message is `Exported system prompt to: chatdbg_prompt_demo.txt` — with no directory in it.
- **AC-5.27** — *Given* a library whose files all fail to parse, *when* the application restarts, *then* each file is reported on standard output as `Error loading system prompt from <full path>: <reason>`, the load count is treated as zero, and the four built-ins are written — overwriting any of those files named `default.json`, `code-reviewer.json`, `algorithm-helper.json` or `security-expert.json`.
- **AC-5.28** — *Given* a record file that has been hand-edited so its key is `Name` instead of `name`, *when* the library is listed, *then* that record's name is the empty string (the field falls back to its default) rather than the intended value.
- **AC-5.29** — *Given* a record file whose entire content is the literal `null` document, *when* the library is listed, *then* that file is skipped and **no** `Error loading system prompt from …` line is emitted for it.
- **AC-5.30** — *Given* the full-screen terminal shell is running with a settings file recording provider `bedrock`, model `anthropic.claude-v2`, temperature `0.2` and active prompt `security-expert`, *when* the user types `/prompt use code-reviewer` into the chat input, *then* the command reports success `Now using system prompt: code-reviewer`, **but** the status line still reads `Provider: bedrock | Model: anthropic.claude-v2 | Prompt: security-expert`, the next model call still uses the `security-expert` text, and the settings file on disk is overwritten with construction-time defaults (`provider: azure`, `modelId: gpt-4`, `temperature: 0.7`, `maxTokens: 1000`, `awsRegion: us-east-1`, `systemPromptName: code-reviewer`). *(This is the observed source behavior; see QUIRK-5.8 / QUIRK-5.20 — the clone is expected to deviate and to record the deviation.)*
- **AC-5.31** — *Given* the plain console shell, a prompt `p` that exists, and standard input redirected from a file containing no `END` line, *when* `/prompt edit p` runs, *then* the source never returns and grows memory without bound. A clone MUST instead terminate on end-of-input, commit or discard per its recorded decision, and return control.
- **AC-5.32** — *Given* a prompt `demo` with description `desc` and a last-used timestamp, *when* it is exported and then re-imported under the name `demo2`, *then* `demo2`'s description is `Imported from: <file name>`, its created-at is the import instant, and its last-used is unset — the original description, created-at and last-used are not recoverable from the exported file.
- **AC-5.33** — *Given* the user runs `/prompt show`, `/prompt use`, `/prompt create`, `/prompt delete`, `/prompt edit`, `/prompt export` or `/prompt import` with no operands, *then* each returns its exact usage string from FR-5.55, each using the literal placeholder token `<n>`.
- **AC-5.34** — *Given* the graphical Edit form is opened on prompt `mine`, its description changed from `old` to `new` and its content changed, *when* Save is pressed, *then* both the description and the content are persisted — and this is the only surface in the product that can change a description.
- **AC-5.35** — *Given* the active prompt is `mine` and the user runs `/prompt edit mine` and completes the edit, *then* the runtime active text reflects the new content immediately for the next chat turn, and the settings file is **not** rewritten.

---

**Quirks**

- *QUIRK-5.1: The library's serializer is configured for indented output **after** the constructor seeds the built-ins, so the four seeded files are written compact on one line while every later save is written indented. Evidence: `Services/SystemPromptService.cs:32` (seeding) runs before `:34-37` (writer options); writer used at `:106`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.2: `/prompt list` intends to print the library's directory but instead re-queries the whole prompt collection and prints the collection object's type name — users literally see ``System prompts directory: System.Collections.Generic.List`1[Xcaciv.ChatDbg.Core.Models.SystemPrompt]``. Root cause: the "report the store directory" capability exists only on the concrete store, not on the contract the command holds. Evidence: `Commands/PromptCommand.cs:102`; accessor at `Services/SystemPromptService.cs:43`; contract at `Services/ISystemPromptService.cs:5-12`. (The exact rendered characters are INFERRED — the defect itself is certain.) Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.3: As a consequence of QUIRK-5.2, `/prompt list` reads the entire library from disk twice per invocation. Evidence: `Commands/PromptCommand.cs:78` and `:102`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.4: The interactive content editor's read loop compares each line against `END` but never checks for end-of-input. With standard input redirected and exhausted, the loop never terminates and spins forever appending blank lines — an unbounded-memory hang. Evidence: `Commands/PromptCommand.cs:267-270`. (INFERRED from the code shape; not observed at runtime.) Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.5: `/prompt edit` writes to and reads from the raw console. In the full-screen terminal shell, commands run inside the interface event loop, so the subcommand's output never reaches the chat pane and its input never arrives — it is effectively unusable there. Evidence: `Commands/PromptCommand.cs:258-270` vs `UI/ChatWindow.cs:382-399`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.6: The user manual describes the graphical dialog's capabilities but nowhere records that the graphical path skips the active-prompt delete guard, skips duplicate-name protection on create and import, and skips the last-used stamp on Use. Evidence: `README.md:15` vs `UI/SystemPromptsDialog.cs:433-459`, `:296-332`, `:583-620`, `:205-226`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.7: The user manual's create example implies quoted descriptions are supported (`/prompt create my-prompt "Custom prompt for specific tasks"`), but the parser has no quoting; the double-quote characters are stored verbatim in the description. Evidence: `README.md:183` vs `src/ChatDbg/ChatShell.cs:326` + `Commands/PromptCommand.cs:192`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.8: In the full-screen shell's entry point the command objects are constructed against one settings instance, and the settings variable is then reassigned to the instance loaded from disk, which is the one handed to the main window and the providers. Commands therefore mutate an **orphaned** settings object. Evidence: `src/ChatDbg.Shell.Gui/Program.cs:30-46`, `:57`, `:76-84`. (INFERRED from construction ordering; not observed at runtime because the repository will not build — see QUIRK-5.25.) Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.9: The full-screen project contains a second, complete console-style shell class that is never instantiated but still carries its own copy of the startup prompt-resolution logic. Evidence: `src/ChatDbg.Shell.Gui/ChatShell.cs` — no construction site anywhere in `src/`; the type is present in the committed build output. (INFERRED from an exhaustive search for construction sites.) Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.10: The graphical dialog's Close button is centred on the same bottom row already occupied by the seven action buttons, so it visually overlaps them. Evidence: `UI/SystemPromptsDialog.cs:60`, `:111-115`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.11: The "Browse…" button does not browse — it opens a plain 60×8 text-entry box titled `Enter File Path` in which the user must type the whole path by hand. In-code comments blame a limitation of a toolkit major version the project does not actually use. No file listing, no completion, no existence check. Evidence: `UI/SystemPromptsDialog.cs:522-567`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.12: Export and import are asymmetric with the stored record — export writes only the text as a plain `.txt`, so description, created-at and last-used are lost on a round trip, and importing a stored record file yields a prompt whose text is that file's raw markup. Evidence: `Commands/PromptCommand.cs:320` vs `:347-361`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.13: The default export directory differs between the two surfaces — user Documents for the command path, user home for the graphical path — and the graphical default does not sanitize the name into the file name. Evidence: `Commands/PromptCommand.cs:313-314` vs `UI/SystemPromptsDialog.cs:645-647`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.14: Three sources disagree about the terminal-interface toolkit version — in-code comments say major version 2, the project file pins 1.19.0, and the documentation set says 1.17.1 and claims the dialog is 402 lines when it is 697. Resolved by inspecting the committed build output: the shipped shell carries 1.19.0 plus its 1.x-only string dependency. Evidence: `src/ChatDbg.Shell.Gui/Xcaciv.ChatDbg.Shell.Gui.csproj`; `docs/PHASE1-SUMMARY.md:41-48`, `:60`; `docs/TERMINAL-GUI-IMPLEMENTATION.md:128-133`; `src/ChatDbg.Shell.Gui/bin/Debug/net10.0/Xcaciv.ChatDbg.Shell.Gui.deps.json`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.15: The graphical Create / Edit / Import handlers close the form window, start a background list reload, then immediately raise the success box without waiting for the reload — so the refreshed list and the success box are unordered relative to one another, the reload's own failure has nowhere to surface, and the box is shown against an already-dismissed form. Evidence: `UI/SystemPromptsDialog.cs:324-326`, `:417-419`, `:612-614`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.16: The library never guards against concurrent writers — no lock, no temporary-file-and-rename, no version token — so two shells running at once interleave whole-file overwrites, a last-used stamp can clobber a concurrent content edit, and a crash mid-write leaves a truncated file that is thereafter silently skipped. Evidence: `Services/SystemPromptService.cs:100-113`, `:141-152`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.17: The user manual's export and import examples cannot work as written — both use a `~/`-prefixed path, but no home-directory expansion exists anywhere in the feature. Export tries to write into a literal relative directory named `~` and fails; import reports `File not found: ~/Desktop/my-prompt.txt`. Evidence: `README.md:189-190` vs `Commands/PromptCommand.cs:307`, `:320`, `:337`, `:339`. (INFERRED — the code shape is certain; the exact failure text is the platform's own message and was not captured at runtime.) Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.18: The command-path export default silently loses its directory on Linux — the "Documents" well-known folder resolves to the empty string on a host with no XDG user-directory configuration, so the default destination collapses to a bare relative name written into the process's working directory and the success message shows no directory at all. Evidence: `Commands/PromptCommand.cs:313-314`; measured on both platforms. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.19: The graphical "select a prompt first" guard is nearly dead code — the list's selected index starts at 0 and is still 0 after the list is populated, so whenever the library is non-empty the alphabetically first prompt counts as selected from the instant the dialog opens. Pressing Delete immediately after opening targets that prompt, not "nothing". Evidence: `UI/SystemPromptsDialog.cs:150-157`, `:162-166`, `:208-212`, `:342-347`, `:435-440`, `:630-635`; selected-index behaviour measured against toolkit 1.19.0. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.20: Spelled-out consequences of QUIRK-5.8 inside the full-screen shell — (a) `/prompt list` marks whichever prompt is named `default` as ` (current)` regardless of the real active prompt; (b) `/prompt delete` refuses to delete `default` and happily deletes the actually-active prompt; (c) `/prompt use` and `/set systemPrompt` write the orphaned settings object to the settings file, overwriting the user's stored provider, model, temperature, token limit, endpoint, region and every other persisted setting with construction-time defaults. That last one is silent data loss on the settings file. Evidence: `src/ChatDbg.Shell.Gui/Program.cs:14`, `:38`, `:40`, `:58`, `:79-87`; `Commands/PromptCommand.cs:91`, `:231`, `:166`. (INFERRED from the same reading as QUIRK-5.8.) Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.21: Prompt libraries are not portable between operating systems — the file name is derived by replacing every character the **host** operating system forbids, and that set is 41 characters on Windows versus 2 on Linux. A prompt named `a:b` is stored as `a_b.json` on Windows but as `a:b.json` on Linux; copy a library from Linux to Windows and prompts whose names contain `:` `*` `?` `<` `>` `"` `|` `\` become unreachable by name, and copy the other way and names silently collide. Evidence: `Services/SystemPromptService.cs:209-214`; measured on both platforms. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.22: The command's own usage line advertises `import <file>` when import actually requires two operands, `<n>` then `<file_path>` — the form the error message and the user manual both give correctly. The same usage line renders every name placeholder as the bare token `<n>`, which reads as "a number"; that spelling is nonetheless consistent across the code, the error messages and the manual, so a clone that "corrects" it deviates from observed output. Evidence: `Commands/PromptCommand.cs:25` vs `:333`; `README.md:73`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.23: The graphical **settings** dialog is handed the prompt library at construction and never uses it — no field, no read, no write. Evidence: `UI/ChatWindow.cs:1037` passes the store into `UI/SettingsDialog.cs`, which contains no reference to prompts at all. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.24: An unset last-used timestamp is written to disk as an explicit `null` rather than being omitted, so every record file always carries all five keys. Harmless, but a reimplementation that omits absent fields produces files that differ byte-for-byte. Evidence: `Models/SystemPrompt.cs:15-16`; measured serialization. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.25: The repository as committed cannot be built — its `global.json` ends with a stray extra brace, making it invalid, and the build tool aborts with a parse error at line 5 from any directory inside the repository. This is why no dynamic verification of this feature was possible and why the graphical half had to be confirmed from the committed build output instead. Evidence: `global.json` (5 lines, trailing `}`); reproduced. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.26: Per-file read and parse failures during a listing are caught and skipped, but a failure to **enumerate** the library directory is not caught in the store at all — if the directory is removed or made unreadable while the application runs, `/prompt list` surfaces the generic `Error processing prompt command: <message>` and the graphical list reload, which runs on a background path with no handler, has nowhere to report it. Evidence: `Services/SystemPromptService.cs:52` vs `:54-66`; `Commands/PromptCommand.cs:52-55`; `UI/SystemPromptsDialog.cs:131-143`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-5.27: The graphical delete confirmation lists **Yes** first, so the affirmative is the focused/default choice and a stray Enter deletes. Combined with QUIRK-5.19 (a prompt is always "selected") and the missing active-prompt guard, two keystrokes from opening the dialog can delete the prompt the assistant is currently using. Evidence: `UI/SystemPromptsDialog.cs:442-446`. (The index-0 mapping is directly in the code; the "Yes is focused by default" half is INFERRED from the button order and the toolkit's documented confirmation-box behaviour.) Keep-or-fix decision deferred to Open Questions.*

---

**Source notes**

Dossier of record: `/mnt/g/3RD-Party/reversing/output/chatdbg/dossiers/system-prompts.md` (feature 7 of the inventory at `/mnt/g/3RD-Party/reversing/output/chatdbg/inventory.md`).

Source repository: `/mnt/g/3RD-Party/reversing/subject/chatdbg` at pinned commit `d8c18f61d6bb73666ed97cd4885e877e35558485` (branch `LLamaSharp_support`).

Primary evidence paths, repo-relative:

| Area | Path |
|---|---|
| Record model | `src/Xcaciv.ChatDbg.Core/Models/SystemPrompt.cs` |
| Library store and seeding | `src/Xcaciv.ChatDbg.Core/Services/SystemPromptService.cs` |
| Store contract | `src/Xcaciv.ChatDbg.Core/Services/ISystemPromptService.cs` |
| Text-command surface | `src/Xcaciv.ChatDbg.Core/Commands/PromptCommand.cs` |
| Settings-command entry point | `src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs` (`:33-36`, `:138-161`, `:280`, `:283-289`, `:411`, `:438`) |
| Active-prompt settings fields | `src/Xcaciv.ChatDbg.Core/Models/ChatSettings.cs:7-30`, `:70-76` |
| Settings file location | `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:11-31` |
| Help-screen placement | `src/Xcaciv.ChatDbg.Core/Commands/HelpCommand.cs:54-57` |
| Graphical management dialog | `src/ChatDbg.Shell.Gui/UI/SystemPromptsDialog.cs` (697 lines) |
| Menu entry, status line, command dispatch | `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:304`, `:382-399`, `:798`, `:1037`, `:1043-1049` |
| Console-shell startup resolution and banner | `src/ChatDbg/ChatShell.cs:27`, `:161-190`, `:201`, `:326`, `:332` |
| Full-screen entry point (orphaned settings) | `src/ChatDbg.Shell.Gui/Program.cs:14`, `:17`, `:30-46`, `:57-67`, `:76-87` |
| Dead duplicate shell class | `src/ChatDbg.Shell.Gui/ChatShell.cs:31`, `:155-184` |
| Provider consumption of the active text | `src/Xcaciv.ChatDbg.Core/Services/AzureOpenAIService.cs:79`, `:137`; `Services/BedrockService.cs:73`, `:89`; `Services/LLamaSharpService.cs:609` |
| Tests (six assertions total) | `src/Xcaciv.ChatDbg.Core.Tests/Models/SystemPromptTests.cs:9-25`; `Tests/Services/SystemPromptServiceTests.cs:12-63`; `Tests/Commands/PromptCommandTests.cs:13-74` |
| User manual claims | `README.md:12`, `:15`, `:65-73`, `:161-202` |
| Build profiles that disable globalization | `src/ChatDbg.Shell.Gui/Xcaciv.ChatDbg.Shell.Gui.csproj` (`Compact`, `SingleFile`) |
| Committed build output used to confirm the graphical half | `src/ChatDbg.Shell.Gui/bin/Debug/net10.0/Xcaciv.ChatDbg.Shell.Gui.deps.json` |

Test coverage is thin and thinner than it looks: three test classes, six assertions. They pin only in-memory record round-tripping, "seeding produces a non-empty list" (**not** the count, names, texts or timestamps), save-then-delete-then-fetch returning nothing, `/prompt` no-args mentioning the active name, `/prompt list` mentioning a name, and `/prompt use` setting the name and text with exactly one settings save and one last-used stamp. Nothing covers create, delete, edit, export or import at the command level; nothing covers the settings-command entry point; nothing covers serialization, sanitization, malformed files or the active-prompt delete guard; nothing covers the graphical dialog at all — which is why QUIRK-5.2, QUIRK-5.8 and QUIRK-5.19 survive in the source.
