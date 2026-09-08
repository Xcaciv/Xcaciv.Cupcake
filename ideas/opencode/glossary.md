# Glossary — OpenCode PRD

Product terms used throughout the PRD, mapped from the code-names found across `packages/core` and `packages/opencode` (which frequently disagree on naming — see the V1/V2 note below).

| Product term | Code-names seen | Meaning |
|---|---|---|
| **Session** | `Session`, conversation | One durable conversation thread: id, project/workspace, optional parent (for subagents), message history, active agent/model, running cost/tokens. |
| **Provider Turn** | "turn", `streamText` call | One request/response cycle with an LLM provider within a Session Drain. |
| **Session Drain** | turn loop, `SessionPrompt.loop` | The iterative loop that runs Provider Turns (and the tool calls they request) until the assistant's response is final and no tool calls are pending. |
| **System Context** | system prompt | The full set of instructions (base prompt + environment + AGENTS.md-style instruction files + skills + MCP guidance) sent to the model at the start of context. |
| **Agent** | agent, mode, persona | A named configuration of system prompt + default model + permission ruleset. Built-ins: `build`, `plan`, `general`, `explore`, plus hidden internal agents `compaction`/`title`/`summary`. |
| **Subagent dispatch / Task** | "task tool", dispatch | Handing a bounded task to a fresh child Session running under a (usually more restricted) Agent, returning only its final text. |
| **Skill** | Skill, `SKILL.md` | A reusable instruction bundle (Markdown + optional companion files) an Agent can load on demand into context. |
| **Command** | slash command | A user- or config-authored `/name` prompt template, expanded with placeholder/shell/file-reference substitution. |
| **Permission Rule** | permission, rule | An `(action, resource-pattern) → allow|ask|deny` triple gating every tool call. |
| **Tool** | tool | A named, schema-described capability the model can invoke (bash, read, write, edit, grep, glob, apply_patch, task, todowrite, question, webfetch, websearch, skill, lsp, codemode `execute`, MCP/plugin tools). |
| **Snapshot / Checkpoint** | Snapshot | A git-tree-hash capture of the project's tracked+untracked files (in a hidden, private git store) taken before/after each turn, used for diff/revert. |
| **Revert** | revert | Rolling a Session back to an earlier message, restoring files from Snapshots and deleting later messages. |
| **Worktree** | sandbox worktree | An isolated `git worktree` checkout a Session (or subagent) can run in without touching the user's active checkout. |
| **Share** | share | Publishing a Session to a public (or org-authenticated) read-only link. |
| **Workspace** | `Workspace` (control-plane) | An alternate execution location for a project: a local worktree, or a remote OpenCode instance (e.g. a cloud sandbox), synced via the durable event log. Distinct from the IDE term "workspace folder." |
| **Sync / Durable Event Log** | `event`/`event_sequence` tables | An append-only, per-aggregate-sequenced event log used to replicate Session state between instances (workspaces) and to drive Share. |
| **Control Plane** | opencode.ai account/console | OpenCode's own hosted service: account/org login, org-scoped remote config/catalog delivery, Workspace remote targets. |
| **Catalog** | provider/model catalog, "models.dev" | The hosted (cached, offline-fallback) database of provider/model capabilities, limits, and pricing. |
| **MCP** | Model Context Protocol | External tool/prompt/resource servers (local subprocess or remote HTTP/SSE) whose tools are namespaced and merged into the model's tool set. |
| **Plugin** | plugin (V1 `Hooks`) | An in-process JS/TS module contributing lifecycle hooks, tools, or provider-auth methods. Ships several built-in provider-auth integrations this way. |
| **Codemode** | `execute` tool | Experimental (flag-gated) confined script execution over a schema-described tool tree, replacing "one tool call at a time" for externally-supplied (MCP) tools. |
| **ACP** | Agent Client Protocol | Standards-based JSON-RPC-over-stdio protocol (`opencode acp`) letting any ACP-compatible editor (e.g. Zed) drive OpenCode as an agent backend. |
| **TUI** | Terminal UI | The full-screen terminal application; the primary interactive client, built only against the Client SDK contract. |
| **Server** | HTTP API | The local HTTP+SSE+WebSocket server exposing all of the above; embedded in-process by the TUI or run standalone (`opencode serve`). |
| **Truncation store** | tool-output store | The on-disk overflow location for tool output exceeding the line/byte ceiling, with a 7-day retention sweep. |

## The "V1 shipping / V2 dormant" pattern

Nearly every dossier independently found the same architectural situation: `packages/opencode/src/<domain>` holds the implementation actually reachable from the shipping CLI/TUI/HTTP routes ("**V1**"), while `packages/core/src/<domain>` (sometimes alongside `packages/server`, `packages/protocol`, `packages/llm`) holds a structurally different, often more strictly-typed rewrite of the *same* responsibility ("**V2**") that is compiled into the binary, sometimes even mounted on the same server port, but is **not** on the code path any shipped client actually calls. This repeats, independently, in: Config, Auth/Credential, Permission, Provider/LLM (plus a third **fully-live but unreached** V2 session/agent-loop pipeline reachable over its own route), Git/Snapshot, Storage (schema is shared; a second table set is unpopulated), Tool System, Session Runtime, Agent/Skill/Command, and Server/HTTP API (`/api/*` mounted alongside `/session` etc.).

This PRD documents **V1 (shipping) behavior as the reimplementation target** throughout, per the source team's own migration-in-progress evidence (`specs/v2/session.md`'s "V1 Runtime Context Parity" checklist, stubbed V2 facade methods, zero production callers for V2 model-instantiation, etc.). V2 is called out only where it reveals a genuine, already-tested behavioral difference (e.g. `apply_patch` move semantics — see [[git-worktree-snapshot-patch]] / FR-6.x) or a documented target design a reimplementer should be aware of (e.g. CONTEXT.md's Context-Epoch model vs. V1's per-turn full system-prompt rebuild). See PRD §11 (Open Questions) for the consolidated list of "which generation is authoritative" questions raised across dossiers — they are one underlying question asked from twenty different angles, not twenty separate risks.
