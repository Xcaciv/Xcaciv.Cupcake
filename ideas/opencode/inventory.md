# Feature Inventory — OpenCode (core product scope)

## Source

- Repo (local, read-only): `/mnt/g/reversing/reversing/subject/opencode`
- Remote: https://github.com/anomalyco/opencode.git
- Pinned commit: `5cf9f517cfec3ef68d3e68a12a6a4b3163947f44` (branch `dev`)
- Clone/analysis date: 2026-09-04
- License: MIT (Copyright (c) 2025 opencode)

## Scope decision (user-confirmed)

**Core product only.** OpenCode is the AI coding agent: its CLI/daemon, terminal UI, session/agent runtime, tools, providers, plugins, HTTP API, and SDKs. Excluded as auxiliary/SaaS surface (not documented in this PRD, listed as Non-Goals): `packages/app` (web console UI), `packages/desktop` (Tauri desktop shell), `packages/console` + `packages/enterprise` (billing/SaaS backend), `packages/slack`, `packages/stats`, `packages/web` (marketing site), `packages/docs`, `packages/storybook`, `packages/identity`, `packages/function`, `packages/containers`, `packages/http-recorder`, `packages/httpapi-codegen` (dev tooling), `packages/effect-drizzle-sqlite` / `packages/effect-sqlite-node` (generic infra libs, not product behavior), `packages/session-ui` / `packages/ui` (shared UI kit used by the excluded web surfaces).

**Depth: Summary.** Dossiers and PRD sections are condensed — key behaviors, rules, and 5-8 acceptance criteria per feature rather than exhaustive edge-case enumeration.

**Target platform:** none specified; no target-specific hazard annotations.

## Conventions note (given to every sub-agent)

- **Language/runtime**: TypeScript on Bun. Heavy use of the Effect-TS library (`Effect.gen`, services, layers) — describe *behavior*, not Effect plumbing.
- **Monorepo layout**: Bun workspaces under `packages/*`. The product is split across two overlapping layers:
  - `packages/core/src/<domain>` — newer shared domain library (services, schemas, business logic).
  - `packages/opencode/src/<domain>` — the CLI application: often the SAME domain names (e.g. both have `session/`, `tool/`, `provider/`, `permission/`, `plugin/`, `git.ts`, `snapshot.ts`) and appears to be mid-migration, with `opencode/` sometimes re-exporting/wrapping `core/` and sometimes still holding legacy/CLI-only logic.
  - **Sub-agents must check BOTH locations for their assigned domain**, treat `core/` as authoritative for shared business rules when the two disagree, and explicitly flag any unresolved contradiction as an open question rather than silently picking one.
- **Wire contracts**: `packages/schema/src/*.ts` defines the canonical data shapes (Effect Schema). `packages/protocol/src` defines the public `HttpApi` surface. Dependency rule (from repo `AGENTS.md`): Schema → Core/Protocol → Server; Client depends only on Schema/Protocol (never Core/Server); `sdk-next` composes Client+Core+Server.
- **Tests**: colocated under each package's `test/` (mirrors `src/` structure). Test names/assertions are the best proxy for requirements — mine them hard.
- **Docs of record**: `/mnt/g/reversing/reversing/subject/opencode/CONTEXT.md` is a precise glossary/spec for the session/context runtime — required reading for the Session feature.
- **Built-in agents** (from README): `build` (default, full access), `plan` (read-only, asks permission before bash), `general` (internal subagent for search/multi-step tasks, invoked as `@general`).
- **No source code, no Effect/TypeScript idioms, no framework names in dossiers** except in the External Technology table.

## Dependency tiers & inventory

### Tier 1 — foundational (no intra-scope dependencies)

| Feature | Kind | Evidence paths | Complexity |
|---|---|---|---|
| Config & Data Contracts | platform capability | `packages/opencode/src/config`, `packages/core/src/config*`, `packages/schema/src/*` | M |
| Auth, Credentials & Accounts | platform capability | `packages/opencode/src/auth`, `packages/opencode/src/account`, `packages/core/src/credential*`, `packages/core/src/oauth`, `packages/core/src/github-copilot`, `packages/core/src/account*` | M |
| Permission System | cross-cutting | `packages/opencode/src/permission`, `packages/core/src/permission*` | S |
| Platform Utilities | cross-cutting | `packages/opencode/src/id`, `src/env`, `src/format`, `src/image`, `src/util`, `src/temporary.ts`, `packages/core/src/id`, `src/filesystem*`, `src/fs-util.ts`, `src/process.ts`, `src/cross-spawn-spawner.ts`, `src/image.ts` | S |

### Tier 2 — depends only on Tier 1

| Feature | Kind | Evidence paths | Depends on | Complexity |
|---|---|---|---|---|
| Provider & LLM Integration | integration | `packages/opencode/src/provider`, `packages/core/src/provider.ts`, `src/catalog.ts`, `src/models-dev.ts`, `src/aisdk.ts`, `packages/llm/src/*` | Config, Auth | L |
| Git, Worktree, Snapshot & Patch | user-facing feature | `packages/opencode/src/git`, `src/worktree`, `src/snapshot`, `src/patch`, `packages/core/src/git.ts`, `src/snapshot.ts`, `src/patch.ts` | Platform Utilities | M |
| LSP Integration | user-facing feature | `packages/opencode/src/lsp`, `packages/core/src/*` (lsp refs), `packages/core/src/ripgrep*` | Platform Utilities | M |
| Storage, Sync & Share | platform capability | `packages/opencode/src/storage`, `src/sync`, `src/share`, `packages/core/src/database`, `src/repository*.ts`, `src/state.ts`, `src/share` | Config | M |
| Plugin & MCP System | integration | `packages/opencode/src/plugin`, `src/mcp`, `packages/core/src/plugin*`, `packages/plugin/src/*` | Config, Permission | M |
| Codemode (sandboxed code execution over tools) | platform capability | `packages/codemode/src/*` | Platform Utilities | M |

### Tier 3 — depends on Tier 1-2

| Feature | Kind | Evidence paths | Depends on | Complexity |
|---|---|---|---|---|
| Tool System | user-facing feature | `packages/opencode/src/tool`, `packages/core/src/tool`, `src/tool-output-store.ts`, `src/ripgrep` | Permission, LSP, Git/Snapshot/Patch, Platform Utilities, Codemode | L |
| Session & Conversation Runtime | user-facing feature | `packages/opencode/src/session`, `src/question`, `packages/core/src/session`, `src/system-context`, `src/background-job.ts`, `src/instruction-context.ts`, `/mnt/g/reversing/reversing/subject/opencode/CONTEXT.md` | Provider, Tool, Permission, Storage | L |
| Agent, Subagent & Skill System | user-facing feature | `packages/opencode/src/agent`, `src/skill`, `packages/core/src/agent.ts`, `src/skill*` | Session, Tool, Config | M |
| Command System (user-defined slash commands) | user-facing feature | `packages/opencode/src/command`, `packages/core/src/command.ts` | Config, Session | S |

### Tier 4 — depends on Tier 1-3

| Feature | Kind | Evidence paths | Depends on | Complexity |
|---|---|---|---|---|
| Server, HTTP API & Event Bus | platform capability | `packages/opencode/src/server`, `packages/server/src/*`, `packages/protocol/src/*`, `packages/opencode/src/bus`, `src/event-manifest.ts`, `src/event-v2-bridge.ts` | Session, Agent, Tool, Storage | L |
| Client SDKs & Embedded Client | integration | `packages/client/src/*`, `packages/sdk/js/src/*`, `packages/sdk-next/src/*` | Server, Protocol | M |
| Control Plane & Cloud Sync | integration | `packages/opencode/src/control-plane`, `packages/core/src/integration*` | Auth, Session, Server | S |
| IDE Integration & ACP (Agent Client Protocol) | integration | `packages/opencode/src/ide`, `packages/opencode/src/acp` | Server, Client SDK | M |

### Tier 5 — depends on Tier 1-4

| Feature | Kind | Evidence paths | Depends on | Complexity |
|---|---|---|---|---|
| Terminal UI (TUI) | user-facing feature | `packages/tui/src/*` | Client SDK, Session, Agent | L |
| CLI, Daemon & Self-Update | user-facing feature | `packages/cli/src/*`, `packages/opencode/src/cli`, `src/index.ts`, `src/node.ts`, `src/installation` | Server, TUI, Client SDK | M |

**Total: 20 features.**

## Non-Goals (excluded, in scope of the whole repo but not this PRD)

`packages/app`, `packages/desktop`, `packages/console`, `packages/enterprise`, `packages/slack`, `packages/stats`, `packages/web`, `packages/docs`, `packages/storybook`, `packages/identity`, `packages/function`, `packages/containers`, `packages/http-recorder`, `packages/httpapi-codegen`, `packages/effect-drizzle-sqlite`, `packages/effect-sqlite-node`, `packages/session-ui`, `packages/ui`, `packages/script`, `infra/`, `nix/`, `sdks/vscode` (VS Code extension shell — thin wrapper, excluded), non-English READMEs.

## Living heart (git churn signal, last 200 commits touching in-scope dirs)

`core/src` (general, 96), `opencode/src/provider` (52), `opencode/src/session` (46), `core/src/tool` (34), `opencode/src/tool` (26), `opencode/src/mcp` (24), `opencode/src/cli/cmd` (15), `opencode/src/acp` (15), `tui/src/routes/session` (14). These get extra scrutiny in their dossiers.
