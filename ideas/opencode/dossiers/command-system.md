# Feature: Command System

## Purpose

Lets a user (or a project) author reusable, named prompt templates — invoked as `/name arg1 arg2 ...` inside a chat session — that expand into the text (and optionally files/agent-mentions) actually sent to the AI assistant. This is distinct from the CLI's own built-in subcommands (`opencode auth login`, etc.): a "command" here is always a user-authored or config-authored macro that produces a prompt, resolved and expanded entirely inside a running session. Commands can also be contributed indirectly through two adjacent mechanisms that get merged into the same namespace: prompts exposed by connected MCP servers, and locally installed "skills" (each skill becomes an invokable command by name unless a real command already claims that name).

## Behavior

1. **Definition sources** (merged into one flat name → command map):
   - Two built-in commands always exist: `init` (guided AGENTS.md authoring) and `review` (code-review of a diff/commit/branch/PR; runs as a subtask).
   - Inline definitions inside project/global configuration under a `command`/`commands` map, keyed by command name, each an object with `template` (required) plus optional `description`, `agent`, `model` (and/or `variant`), `subtask`.
   - File-based definitions: any Markdown file found under a `command/` or `commands/` directory relative to a config root (recursive glob, hidden files and symlinks included). The file's body (after optional YAML frontmatter) is the template; frontmatter fields map to the same optional properties as inline definitions. The command's name is the file's path relative to the `command(s)/` directory, with the `.md` extension stripped and any subdirectory preserved with `/` separators (e.g. `commands/nested/docs.md` → command name `nested/docs`).
   - MCP server "prompts" are surfaced as commands whose template is resolved lazily (asynchronously) from the MCP server, with positional placeholders substituted for the prompt's declared arguments.
   - Installed skills are surfaded as commands (name = skill name, template = skill's instructional content, plus a note about the skill's base directory for relative-path resolution) — but only if no real command already uses that name.
   - When both a legacy singular `command` config key and a newer plural `commands` config key define the same name, the legacy definition wins entirely (no field-level merge) — the newer one is only used to fill in names not present in the legacy map.
   - When multiple config sources (e.g. an inline document definition and a file-based directory definition) define the same command name, later-processed sources overwrite earlier ones's fields as a whole-record replace, in the order configuration is resolved.

2. **Invocation**: a chat surface (TUI, ACP client, or HTTP API) sends a command name plus a raw argument string (everything typed after the command name) to a running session. The session:
   - Looks up the command by exact name; unknown name is a hard error listing all known command names as a hint.
   - Tokenizes the raw argument string on whitespace, respecting single/double-quoted spans and a `[Image N]` placeholder token as an atomic token; surrounding matched quotes are stripped from each token.
   - Resolves an "effective template" (awaiting the MCP-lazy case).

3. **Placeholder substitution** (applied to the raw resolved template):
   - Numbered placeholders `$1`, `$2`, … are replaced by the corresponding positional argument token (1-indexed). The *highest-numbered* placeholder present in the template greedily absorbs itself plus all remaining trailing argument tokens (joined by a single space) — i.e. it behaves like a "rest of arguments" slot. A numbered placeholder beyond the supplied argument count resolves to an empty string.
   - `$ARGUMENTS` (if present, one or more occurrences) is replaced by the entire raw, untokenized argument string exactly as typed.
   - If the template contains neither numbered placeholders nor `$ARGUMENTS`, and the user supplied a non-empty argument string, that argument string is appended to the end of the template as a new paragraph (template + blank line + arguments).

4. **Shell interpolation**: after placeholder substitution, any inline shell-command markers of the form `` !`shell command` `` in the resulting text are executed (using the configured/preferred shell) and each marker is replaced by that command's captured text output. Shell markers are detected with a non-greedy backtick-delimited pattern and all matches in one template are run concurrently before substitution.

5. **File / agent references**: after all substitution, the final text is scanned for `@`-prefixed path-like references (not part of an email address, not immediately preceded by a backtick, not itself inside backticks). For each unique reference: `~/`-prefixed references resolve relative to the user's home directory, everything else resolves relative to the current project/worktree root. If the path exists on disk, it is attached to the outgoing message as a file part (directories get a directory mime type, everything else is treated as text); if it does not exist as a file but matches the name of a known agent, an "agent mention" part is attached instead; otherwise the reference is left as literal text with no attachment. File references already present in the caller-supplied message parts are not duplicated.

6. **Dispatch to the model**: the expanded template becomes the primary prompt text of a new user message (plus any generated file/agent parts and any explicitly attached input parts). Model and agent selection, in priority order: the command's own `model` (and `variant`) field; else, if the command names an `agent` that itself specifies a model, that; else the caller-supplied model; else the session's current model. Agent selection: the command's `agent` field if set, else the caller-supplied agent, else the default agent — an unresolvable agent name is a hard error listing known agent names.

7. **Subtask mode**: if the resolved agent's mode is `subagent` (and the command didn't explicitly opt out with `subtask: false`), or the command explicitly sets `subtask: true` regardless of agent mode, the command runs as an isolated subtask/subagent invocation instead of inline in the current conversation — the expanded template becomes the subtask's prompt and the command's `description` becomes the subtask's label, and any extra input parts are dropped rather than merged in.

8. **Hooks/telemetry**: before dispatch, a `command.execute.before` extension point fires (receiving command name, session id, raw arguments, and the assembled parts, mutable by listeners). After the resulting message is created, a `command executed` event is published (command name, session id, raw arguments, resulting message id) for downstream consumers (activity feeds, ACP clients, etc.).

9. **Discovery/listing**: a command listing API returns every known command's name, optional description/agent/model/subtask/source (`"command"` | `"mcp"` | `"skill"`), and a computed list of "hints" — the distinct `$N` placeholders found in the template (sorted) plus `$ARGUMENTS` if present, or (for MCP-derived commands) one hint per declared prompt argument. This is intended for client-side autocomplete/argument-hinting UIs, not consumed further server-side.

## Business rules & edge cases

- A command with an empty template body (frontmatter-only Markdown file) is valid and simply expands to an empty/whitespace string plus any appended raw arguments.
- Frontmatter parsing is deliberately lenient: unquoted values that merely contain a colon (common in prose descriptions, times, or URLs) are auto-repaired into a valid multi-line YAML block scalar before real YAML parsing, so files copied from other agent tools with informally-quoted frontmatter don't fail to load; genuinely malformed frontmatter still throws a descriptive parse error identifying the offending file.
- A file with no frontmatter delimiter, or an empty frontmatter block, parses with an empty metadata object and the full file body as the template.
- `@` file-reference detection intentionally excludes: references immediately following a backtick or fully inside backtick-quoted spans (so example paths in inline code are not attached), and things that look like email addresses. It does match: bare relative paths, dotted/extension paths, multi-extension paths, hidden dotfiles/dot-directories, references followed immediately by a comma or ending a sentence with a period, absolute paths (with or without extension), and `~/`-relative home paths — trailing punctuation like a comma or sentence-ending period is not considered part of the path.
- A command name may contain `/` (from nested directories), which end users see as e.g. `/nested/docs`.
- Numbered placeholder collapsing only special-cases the single highest-numbered placeholder in the template; every other numbered placeholder is a straight 1:1 substitution (or empty if no corresponding argument was supplied).
- If a command's `model` field is a combined `provider/model#variant` style reference, the variant portion is split out separately; a top-level `variant` field (config schema) can also set/override the variant without re-specifying provider/model.
- Skills only become commands when they don't collide with an existing (command- or MCP-sourced) command name — command and MCP sources take precedence over skill-derived commands.
- Unknown command name and unknown agent name are both reported as user-facing typed errors (not exceptions/crashes), each including the full list of currently known valid names to help the user recover.

## Workflows & states

Primary path — user types `/reviewcmd fix the login bug` in a session:
1. Session receives command name `reviewcmd` + raw arguments `fix the login bug`.
2. Command is looked up; if missing, session emits an error event and the call fails immediately (no message is created).
3. Arguments are tokenized; template placeholders (`$1`/`$ARGUMENTS`) and inline shell markers are resolved in sequence to produce final prompt text.
4. `@`-references in the final text are resolved into file/agent parts.
5. Model + agent are resolved per the priority rules above; an unresolvable agent again fails immediately with an error event.
6. `command.execute.before` hook runs, then the message (or subtask) is created and streamed to the model exactly like a normal chat turn.
7. A `command executed` event is published once the resulting message exists.

Secondary path — MCP-sourced command: the same flow, except step 3's template resolution is an async fetch from the MCP server (its prompt content, with the server's declared argument names bound one-to-one to `$1`, `$2`, … before the standard numbered-placeholder substitution runs).

Secondary path — skill-sourced command: template is the skill's static instructional content (no placeholder or MCP resolution needed) plus, for file-backed skills, an appended note of the skill's base directory.

## Data

Per-command record (conceptually — two schema variants exist in-repo, see Confidence & open questions):
- `name` (string, required; derived from config key or from directory-relative file path with `.md` stripped)
- `template` (string, required — the raw, unexpanded prompt body; may be a value resolved asynchronously for MCP-sourced commands)
- `description` (string, optional)
- `agent` (string, optional — name of an agent this command always uses)
- `model` (optional — either a plain provider/model string, or a structured `{providerID, id/model, variant}` reference)
- `variant` (string, optional — a separate model-variant override, only meaningful alongside `model`)
- `subtask` (boolean, optional — forces or forbids running as a detached subagent task regardless of the resolved agent's own mode)
- `source` (implementation detail exposed on the listing API: `"command"`, `"mcp"`, or `"skill"`)
- `hints` (computed, not stored — list of placeholder tokens discovered in the template, for client autocomplete)

Config-file representation: same fields, supplied either inline in a config document under a `command`/`commands` map keyed by name, or as a Markdown file (YAML frontmatter = the optional fields, document body = `template`) under a `command/` or `commands/` directory.

## Interfaces

- Consumed by: chat/session runtime (this is where templates are actually expanded and dispatched) — command execution is exposed as one operation on the session's prompt-handling surface, taking a session id, command name, raw argument string, and optional overrides (message id, agent, model, variant, extra input parts), and returning the created assistant-bound message.
- Exposed over HTTP/RPC: a "list commands" read endpoint (returns every command's public fields) and a "send command" endpoint scoped to a session (accepts the same command-invocation payload described above and returns the resulting message) — used by the CLI's own `run --command` flag and by any remote client (TUI, editor plugin via the agent-client protocol) to trigger commands non-interactively.
- Feeds into: an "available commands" listing surfaced to protocol clients (e.g. editor integrations) so they can render a command palette.
- Depends on: Config & Data Contracts (source of inline/file-based command definitions and merge/precedence rules across config layers — out of scope here), Agent/Subagent/Skill System (agent resolution, subtask/subagent execution, skill-to-command bridging — out of scope here), Session & Conversation Runtime (message creation, model selection, streaming — out of scope here), and an MCP integration (prompt discovery/fetch — out of scope here).
- Emits: an extension/plugin hook fired immediately before a command's expanded parts are sent, and a domain event published after the resulting message is created (name, session, raw arguments, message id) for activity/telemetry consumers.

## External technology

| Concern | Technology observed |
|---|---|
| Frontmatter parsing | `gray-matter` (YAML front matter + body split) |
| Config/data validation | Effect Schema (structural validation/decoding) |
| Runtime/async plumbing | Effect-TS effects and services (business logic only; not itself a design constraint for a reimplementation) |
| File globbing | A thin project-internal glob wrapper (Bun's glob under the hood) |

## Error handling

- Unknown command name: typed, non-crashing error; message names the missing command and lists all known command names (empty list omits the hint clause). Also published as a session-level error event before propagating.
- Unknown agent name (whether the caller supplied it directly or a resolved command names it): same pattern — typed error naming the missing agent plus the list of visible (non-hidden) agent names; also published as a session error event.
- Malformed YAML frontmatter that fails even the lenient auto-repair: a descriptive parse error naming the offending file path, rather than a silent skip — loading aborts for that source (contrast with unreadable/missing files during directory scanning, which are silently skipped rather than failing the whole load).
- A file that fails to parse as valid Markdown+frontmatter during directory scanning (e.g. unreadable) is silently skipped rather than aborting the whole command-loading pass.
- MCP prompt resolution is asynchronous and can itself fail; failure surfaces as a normal effect failure at command-execution time, not at command-listing time (the listing only carries a not-yet-resolved template).

## Non-functional observations

- Command definitions are loaded/merged once into an in-memory per-session-instance map (not re-scanned per invocation); config or file changes require a reload of that state.
- Directory scanning for file-based commands is recursive and matches both singular and plural directory names (`command/` and `commands/`) for compatibility with different authoring conventions.
- The `@`-file-reference and shell-marker (`` !`...` ``) syntaxes are deliberately compatible with conventions used by other, similarly-shaped coding-agent CLIs, to ease porting existing command libraries.
- Frontmatter leniency (auto-quoting colon-bearing scalar values) exists specifically to accept command/agent Markdown files authored for other tools without modification.

## Acceptance criteria — 5-8 Given/When/Then statements

1. **Given** a project defines a Markdown file at `command/foo.md` with frontmatter `description: does X` and body `Do the thing: $ARGUMENTS`, **when** the user invokes `/foo hello world`, **then** a command named `foo` exists with that description, and invoking it produces the prompt text `Do the thing: hello world`.
2. **Given** a command template contains `$1` and `$2` but the user supplies three arguments, **when** the command is invoked, **then** `$1` is replaced by the first argument and `$2` (the highest-numbered placeholder) is replaced by the second and third arguments joined with a space.
3. **Given** a command template contains no `$1`/`$ARGUMENTS` placeholder at all, **when** the user invokes it with non-empty arguments, **then** the raw argument string is appended to the end of the expanded template as a trailing paragraph.
4. **Given** a command template contains `` !`echo hi` ``, **when** the command is invoked, **then** the shell command is executed and its output text replaces the marker in the final prompt before dispatch to the model.
5. **Given** the same command name is defined both by a legacy single-command config entry and a newer-style commands-map config entry, **when** commands are loaded, **then** the legacy entry's fields win in full for that name; a command name that exists only in the newer-style map is still loaded.
6. **Given** a command whose resolved `agent` has execution mode "subagent" and the command does not set `subtask: false`, **when** the command is invoked, **then** it is dispatched as a detached subtask (not as an inline conversation turn), carrying the command's description as the subtask's label.
7. **Given** no command is registered under the invoked name, **when** the user invokes it, **then** the session raises a typed, catchable error naming the requested command and listing all currently known command names, and no message is created.
8. **Given** the final expanded prompt text contains `@src/main.ts` where that file exists in the project, **when** the command runs, **then** that file is attached to the outgoing message as a file part in addition to the text.

## Confidence & open questions

- **Unresolved duplication between `packages/core/src/command.ts` and `packages/opencode/src/command/index.ts`**: these are two materially different implementations sharing only the concept and some field names. The core version is a minimal, mutable draft/state container (`get`/`list`/`transform`, no built-in commands, no placeholder logic) fed by a separate "plugin" (`packages/core/src/plugin/command.ts`, `packages/core/src/config/plugin/command.ts`) that layers in the built-in `init`/`review` templates and config/file-based commands. The `opencode` version independently re-implements config lookup, MCP-prompt bridging, and skill bridging inline in one service, and is the version actually wired into the session/prompt runtime that does placeholder/shell/file expansion (`packages/opencode/src/session/prompt.ts`). It is not clear from the code alone which of these is the authoritative/current implementation and which is legacy or in-progress migration scaffolding — this dossier describes the `opencode` runtime behavior (since that's what session execution actually calls) but folds in the core version's config-loading/precedence semantics (config-order override, file+inline merge) since those tests were the clearest source of merge-order guarantees. A reimplementation should treat this as a single logical feature but flag it for the original team.
- **Global vs. project config precedence for identically-named commands** is asserted by test to be "later entry in config resolution order wins" but the actual global/project/local ordering itself is defined by the Config subsystem (explicitly out of scope here) — a full reimplementation needs that adjacent dossier to know which physical config layer is "later."
- The `hints` field is computed and exposed on the listing API but no consumer of it was found anywhere in this codebase (likely consumed by an out-of-tree TUI/editor client) — its exact autocomplete UX is inferred, not observed.
- MCP prompt-argument binding to `$1`, `$2`, … in declaration order is observed in code but no test exercises it end-to-end; behavior on prompts with zero or duplicate-named arguments is unverified.
- It is unverified whether the file-based command loader deduplicates a case where the same logical directory is reachable from two different config layers (e.g. symlinked project dirs) — only straightforward two-source override was tested.
