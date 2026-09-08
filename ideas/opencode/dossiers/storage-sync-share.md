# Feature: Storage, Sync & Share

## Purpose

Give a single OpenCode installation durable, crash-safe local storage for everything that makes up a user's work (projects, workspaces, sessions, conversation messages/parts, accounts, share links), evolve that storage's shape safely across upgrades, and let a session's ongoing conversation be mirrored — either to another local process/remote workspace acting on the same project ("sync") or to a public, read-only web link ("share"). The feature does not itself define what a session or message *is* (Session & Conversation Runtime) or how a server exposes it over HTTP (Server/HTTP API) — it defines where that data lives, how it survives restarts and version upgrades, and how copies of it stay consistent.

## Behavior

**On-disk layout.** All state lives under OS-standard per-app directories (an XDG-style base-directory scheme, with platform-appropriate fallbacks): a data directory, a cache directory, a config directory, a state directory, a log directory, and a temp directory, all namespaced under the application name. The data directory holds:
- One SQLite database file — the system of record. Its name is normally `opencode.db`; on beta/dev release channels it becomes `opencode-<channel>.db` (channel name sanitized) so channels don't share a database; both can be overridden by an environment variable that also accepts an absolute path or the special value for an in-memory database.
- A `storage/` subtree of loose JSON files — a legacy, mostly retired key-value store (see Data, and Open Questions).
- `log/`, `repos/`, and `bin/` subdirectories used by other features.

**Database engine and lifecycle.** On open, the database is put into a write-ahead-log journal mode with normal sync durability, a multi-second busy timeout (so a second process contending for a write briefly retries instead of failing immediately), a raised page cache size, foreign-key enforcement turned on, and a passive checkpoint pass. A single always-run migration step then brings the schema up to date (see below) before any query is allowed to execute.

**Schema migration.** The schema is versioned as an ordered list of uniquely-named, one-off migration steps (each identified by a timestamp-prefixed name). A `migration` bookkeeping table records which steps have completed. On open:
- If the database has no tables at all, the *entire current schema* is created in one transaction and every known migration step is immediately marked complete — new installs never replay history.
- If the database already has a table that identifies it as an OpenCode database, only the migration steps not yet recorded are run, each in its own transaction, in order; a failure stops the process before recording that step (or any later one) as complete, so a fixed build can resume cleanly.
- If the database has other tables but is missing that identifying table, startup refuses to proceed — protects a user from accidentally pointing the app at an unrelated database file.
- Upgrading from an older toolchain that tracked completed migrations in its own bookkeeping table is handled once: entries are imported into the new bookkeeping table either by name (when recorded) or, for older/unnamed entries, by matching each entry's timestamp against the known migration names — if a legacy timestamp can't be matched to any known step, migration aborts rather than guess.
- Concurrent startup of the database from more than one process is safe: initialization is serialized by an internal lock and is transactional/idempotent, so simultaneous first-time opens converge on one consistent, fully-migrated database.
- A parallel, currently-unpopulated bookkeeping table exists for one-off *data* backfills (as opposed to schema changes); no data migration is registered against it at this commit.

**Durable event log ("sync").** Underneath every mutation to a session's conversation state sits a generic, durable event log, keyed by a logical aggregate id (in practice, a session id): an append-only table of events (id, type+version, aggregate id, strictly increasing per-aggregate sequence number starting at 0, JSON payload) plus a per-aggregate cursor row tracking the latest sequence number and an optional "owner" tag.
- **Local publish**: producing a new event for an aggregate atomically reads the current latest sequence, assigns the next one, runs any registered in-process projectors (the code that turns the event into the actual session/message/part rows), then inserts the sequence and event rows — all inside one transaction, so a crash never leaves a half-applied event or a dangling event with no bumped cursor. A caller may also chain a local, non-replayable side effect into that same transaction; if it fails, the whole event (and any projector effects) rolls back.
- **Replay**: accepting an externally-produced, already-sequenced event (or ordered batch of events) into the local log — used when another party (see Interfaces) needs to hand its history to this instance. Replay enforces: the event's declared aggregate must match the aggregate implied by its own payload; its sequence must be exactly one past the current local cursor, *or*, if it targets an already-recorded sequence, its id/type/payload must match byte-for-byte what's already stored (a mismatch is treated as diverged history and rejected); an event id can never be attached to two different aggregate/sequence pairs.
- **Ownership / single-writer fencing**: a cursor may be tagged with an owner id. Replay may pass its own caller-id and optionally demand strict ownership checking. Without strict checking, replaying against an aggregate owned by someone else is silently ignored (no error, no mutation) — a design that lets a "losing" writer's late-arriving batch be dropped harmlessly. With strict checking, the same conflict raises an explicit error instead of being silently swallowed. The first party to touch an unowned aggregate (via a plain claim call, or via replay) implicitly becomes its owner.
- **Live delivery**: independent of durability, events are also fanned out in-process to live subscribers (typed-by-event-type and wildcard) the moment they're generated; only events whose definition opts in to being "durable" get sequenced/persisted at all — most events are fire-and-forget notifications.
- **Removal**: deleting an aggregate (e.g., a deleted session) clears both its cursor and its entire event history transactionally.
- A thin local HTTP surface (start / replay / steal / history) rides on top of these primitives so that another workspace touching the same project can pull a project's event history, hand back its own replayed history, and reassign ("steal") a session to itself; this surface is gated behind an experimental flag and belongs conceptually to Control-Plane/Cloud-Sync (see Interfaces) — only the underlying durable-event mechanics described above are this feature's concern.

**Legacy JSON storage.** A separate, older key-value store keeps one JSON file per logical key (an ordered list of path segments becomes a nested file path under `storage/`). Every read/write/update/remove is guarded by a per-resolved-path async read/write lock, so concurrent operations on the *same* key serialize (writers exclude readers and other writers; readers don't block other readers), while operations on different keys never contend. A missing key surfaces as a dedicated not-found error rather than a raw filesystem error. This store still carries a two-step, marker-tracked migration for very old on-disk layouts (per-project directories with per-session/message/part files, later collapsed into per-project session files with a computed diff summary), applied once at first use and recorded by a small integer marker file; a failed migration step leaves the marker unadvanced so it retries next launch. At the current commit its only live write path is a best-effort snapshot of a session's file-diff data, whose failures are explicitly ignored (see Open Questions).

**Share.** A session can be published to a link:
- Sharing is a hard off switch at the config or environment level; when off, every share operation is a no-op.
- Creating a share posts the session id to a remote endpoint and receives back a share id, a public URL, and a secret. The share id/url/secret is upserted into a local table keyed by session id, cached in memory, and the URL is also denormalized onto the session's own record (for quick display); a background "full sync" then pushes the session's current info, every existing message, every existing part, the running file-diff summary, and the distinct models used across the conversation.
- After the initial full sync, incremental pushes are driven by the same live event stream: whenever the session's info, a message, a part, or the file-diff summary changes (scoped to the sharing instance's own working directory), the changed item is queued; queued items are keyed by logical identity (session / `message/<id>` / `part/<messageId>/<id>` / diff / models) so several rapid changes to the same item collapse to only the latest value. A short delay (about a second) after the first queued item, the whole batch is sent in one request to the remote "sync" endpoint, authenticated with the share's own secret rather than the user's login — so background processes can keep a share updated without holding an active session/account.
- Removing a share calls the remote delete endpoint (again via the secret) and then deletes the local share record and its denormalized URL; removing a share that doesn't exist locally is a no-op.
- Deleting the underlying session also removes its share, driven by the same session-deleted event.
- Which remote endpoint family is used depends on whether the caller has an active, org-linked account: with one, an authenticated per-organization API is used (bearer token + org header, base URL from that account); without one, a legacy, unauthenticated public API is used (base URL from an enterprise-config override, else a fixed default host).
- A config option can also make sharing automatic: a *newly created, top-level* session (not a sub-agent/child session) is shared immediately in the background right after creation, with share failures swallowed rather than surfaced.

## Business rules & edge cases

- A non-empty database lacking the table that identifies it as an OpenCode database is treated as foreign and startup fails loudly rather than silently mutating it.
- Legacy pre-existing migration bookkeeping is imported at most once, and only when the new bookkeeping table is otherwise empty; an unrecognized legacy timestamp aborts migration instead of guessing which steps already ran.
- Every schema migration step and every durable-event commit is transactional; a failure part-way through never leaves partially-applied schema or a bumped sequence with no matching event row.
- Directory/worktree-like path columns are normalized to forward slashes for storage and re-expanded to the platform's native separator on read, so the same session/project is comparable and portable regardless of the OS that wrote it; one legacy carve-out lets an already-stored empty directory value on very old sessions pass through unvalidated instead of being rejected.
- Sub-agent/child sessions are never auto-shared, even when auto-share is enabled — only creating a new top-level session triggers it.
- Event replay requires *contiguous* sequence numbers (no gaps) whether the event was generated locally or handed in as an externally replayed batch; an event id can never be reused at a different aggregate/sequence position.
- Given an aggregate already owned by one party, a replay batch claiming a different owner without requesting strict checking is dropped with no error and no change to stored state or ownership; the same conflict under strict checking is reported as an explicit error instead.
- The first party to write to (or claim) a previously unowned aggregate becomes its owner going forward; ownership can also be reassigned explicitly.
- A share create request that fails (non-success response) leaves no local share record — sharing is all-or-nothing from the caller's point of view.
- Share sync/remove calls authenticate with the share's own per-share secret, not the user's account credentials, so a share can keep being updated by a process that has no active login.

## Workflows & states

**Database bring-up:** open file → set pragmas → empty? → create full schema + mark all migrations done ⟶ else → known table present? → run only pending migration steps in order ⟶ else → refuse to start.

**Event lifecycle for one aggregate (e.g. one session):** *unowned* → first local publish or first accepted replay/claim → *owned by writer W* → (a) further local publishes from W keep advancing the sequence normally; (b) a replay batch from another party P without strict checking is silently ignored while W remains owner; (c) an explicit `claim` call, or a strict-checked replay that matches the existing history, can hand ownership to a new party; (d) removal clears the aggregate back to nonexistent (cursor and history both gone).

**Share lifecycle for one session:** *unshared* → share requested → config allows it? no → rejected/no-op; yes → remote create call → *shared, full-syncing* (pushes entire current session/messages/parts/diffs/models in the background) → *shared, live* (subsequent local changes are queued, debounced ~1s, and pushed incrementally) → explicit unshare, or session deleted → remote delete call → *unshared* (local record and denormalized URL cleared). A failed full-sync or incremental flush is logged and left shared (no automatic retry loop beyond the next triggering event); a failed create leaves the session unshared.

## Data

**Primary store — SQLite (Drizzle-modeled tables), selected ones relevant here:**
- `project` — one row per known project (id, worktree path, vcs, name/icon, timestamps); `project_directory` — additional directories associated with a project.
- `workspace` — a working context under a project (id, type, branch, directory, timestamps).
- `session` — id, project id, optional workspace id, optional parent id (sub-sessions), directory/path, title, version, denormalized `share_url`, running cost/token usage, summary/diff totals, revert state, permission ruleset, agent/model, timestamps, archival/compaction timestamps.
- `message`, `part` — conversation content, each referencing `session`/`message` with cascade delete.
- `session_message`, `session_input`, `session_context_epoch`, `todo` — session-runtime projections/inboxes (owned by Session & Conversation Runtime; only their storage lives here).
- `event`, `event_sequence` — the durable event log and per-aggregate cursor described above.
- `session_share` — one row per shared session: session id (primary key, cascades from `session`), remote share id, secret, url, timestamps.
- `account`, `account_state`, and a legacy `control_account` — locally cached OAuth-style credentials and the currently-active account/org, used to decide which share API a request uses (owned by an adjacent account/control-plane feature; only the has-active-account check is this feature's concern).
- `data_migration` — id/time_completed bookkeeping table for one-off data backfills; present in schema but not exercised by any registered backfill at this commit.

**Legacy file store (`storage/` subtree):** one `.json` file per key, path = joined key segments; observed key families: `project/<id>.json`, `session/<projectId>/<sessionId>.json`, `message/<sessionId>/<messageId>.json`, `part/<messageId>/<partId>.json`, `session_diff/<sessionId>.json` (the one still actively written); a top-level `migration` marker file holding a small integer.

**Identifiers:** sortable, prefixed ids (e.g. session/message/part/event ids) generated by a shared id scheme; durable events carry both an id and a per-aggregate integer sequence, which is what total-ordering and replay validation rely on (not the id's own sortability).

## Interfaces

- **Session & Conversation Runtime** (adjacent, not detailed here): defines the session/message/part shapes and business logic; publishes the durable events (session updated/diff/deleted, message/part updated, etc.) that this feature persists, sequences, and — for shares — mirrors outward. Also owns the `session.share_url` denormalized field this feature writes.
- **Config & Data Contracts** (adjacent): supplies the `share` setting (off / auto / on-demand) and an enterprise base-URL override that this feature reads to decide whether/where to share, plus an `OPENCODE_DB` path override and channel-name input that select which SQLite file is opened.
- **Server/HTTP API** (adjacent): bridges this feature's live (non-durable-log) event fan-out onto the server's SSE/event stream for attached clients, and hosts the `/sync/*` endpoints built on the durable-event replay/claim primitives described above.
- **Control Plane & Cloud Sync** (adjacent): owns the "workspace" concept, remote sandbox provisioning, and the org-scoped authenticated share/shares API and account/token model; this feature only consumes an "is there an active org account" check and an issued bearer token, and exposes the generic replay/claim/steal HTTP surface that workspace syncing drives.

## External technology

| Technology | Role |
|---|---|
| Bun | JavaScript/TypeScript runtime the CLI runs on |
| SQLite | Embedded relational database file format; the system of record |
| Drizzle ORM | Schema definition and query builder over SQLite; also generates/tracks schema migrations from a diff of the schema definitions |
| Effect-TS | Structured concurrency/effect system used throughout (transactions, locks, streams) — implementation detail, not part of the observable behavior |
| XDG base directory convention (via a small library) | Resolves the per-OS data/cache/config/state directories under which the database and legacy storage live |

## Error handling

- A read against a missing legacy JSON key raises a distinct not-found error carrying the resolved file path; an update against a missing key raises the same error rather than creating one.
- Opening a non-empty, unrecognized database is a fatal startup error with an explicit message.
- An unmatched legacy migration-journal timestamp aborts migration with an explicit error rather than silently skipping or guessing.
- Durable-event integrity violations (unknown event type on replay, aggregate/payload mismatch, non-contiguous sequence, reused event id, diverged replayed history) are unrecoverable defects that abort the operation — they indicate a corrupted or malicious event stream, not a transient condition, so they are not retried.
- A strict-ownership replay conflict is reported as an explicit, distinguishable error (as opposed to the silent no-op used for a non-strict conflict).
- Share creation propagates any remote-call failure (network error or non-success HTTP status) as an error and guarantees no local share record is left behind.
- Share flush (incremental sync) and full-sync failures are caught, logged, and otherwise swallowed — a background sync failure never surfaces to or interrupts the interactive session.
- A failed legacy-storage migration step is caught, logged, and leaves the migration marker unadvanced so the same step is retried on next launch rather than being skipped.

## Non-functional observations

- Using one SQLite file as the system of record makes backup/restore a simple file copy (mindful of the WAL/shared-memory sidecar files that accompany it while open) and makes multi-process concurrency SQLite's problem: writers serialize via the database's own locking and a multi-second busy timeout rather than an application-level lock file.
- The durable-event/ownership model deliberately assumes exactly one legitimate writer per aggregate at any moment (fenced by the owner tag) instead of a full distributed/CRDT merge scheme — simpler to reason about, at the cost of not supporting true concurrent multi-writer edits to the same session; a losing writer's changes are dropped rather than merged.
- Share sync is intentionally decoupled from the hot conversation path: it listens to the same event bus everything else does and debounces outbound network calls (~1s, coalesced by logical item) so rapid local edits don't translate into a flood of outbound HTTP requests.
- The legacy JSON store's per-key locking is fine-grained (per resolved file path via a map of reentrant locks with no eviction limit noted), so unrelated keys never contend, but it is otherwise a vestigial code path at this commit — most of the application no longer reads or writes through it.

## Acceptance criteria — 5-8 Given/When/Then statements

1. **Given** a fresh, empty database file, **when** the application opens it for the first time, **then** the complete current schema is created in one transaction and every known migration step is recorded as already applied, with no incremental steps re-run later.
2. **Given** an existing database recognized as an OpenCode database with some migration steps already recorded, **when** the application starts, **then** only the migration steps not yet recorded are applied, each atomically and in order, and previously stored data is preserved unchanged.
3. **Given** a database file containing unrelated tables and no table identifying it as an OpenCode database, **when** the application attempts to open it, **then** startup fails with an explicit error instead of altering the file.
4. **Given** two processes opening the same not-yet-initialized database file at the same time, **when** both run startup migrations concurrently, **then** the schema is created exactly once and both processes end up with a fully migrated, non-corrupted database.
5. **Given** a session aggregate at durable sequence N, **when** a new local mutation to that session is committed, **then** it is persisted at sequence N+1, and any replayed batch of events for that aggregate must also be perfectly contiguous or it is rejected.
6. **Given** a session aggregate already owned by writer A, **when** a batch of events for that aggregate is replayed on behalf of writer B without requesting strict ownership, **then** the batch is discarded with no change to the stored history or ownership; **when** the same batch requests strict ownership instead, **then** the replay fails with an explicit ownership-conflict error.
7. **Given** a session that has been successfully shared, **when** its title, a message, a part, or its file-diff summary changes one or more times within about a second, **then** exactly one incremental update reflecting only the latest value per changed item is sent to the remote share endpoint, authenticated by the share's own secret.
8. **Given** a shared session, **when** the session is deleted or the share is explicitly removed, **then** the remote share is deleted and the local share record (and the session's denormalized share URL) is cleared, regardless of whether the acting process has an active user login.

## Confidence & open questions

High confidence: SQLite (via a Drizzle-based query layer) is confirmed as the actual system of record used by the shipping CLI at this commit; the migration engine's empty/pending/refuse/legacy-import behavior and the durable-event log's sequencing/replay/ownership semantics are both directly exercised by an extensive unit test suite; the share create/sync/coalesce/remove behavior is likewise unit-tested end to end.

Open items to confirm with the team before reimplementing:

1. **`repository.ts` / `repository-cache.ts` / `state.ts`** (explicitly listed as starting points for this dossier) turned out, on inspection, to implement something unrelated to session storage/sync: parsing and locally caching *external git repository references* (for cloning a referenced repo) and a generic reactive draft/reload utility used elsewhere (e.g., configuration). They are not part of Storage/Sync/Share's actual code path; flagging in case a different file was intended or the naming collision itself needs attention.
2. **The legacy JSON "Storage" service** is still initialized and still carries its historical two-step migration for very old on-disk layouts, but at this commit its only production write is a fire-and-forget snapshot of a session's file-diff data that failures are explicitly told to ignore, and nothing in the shipping app appears to read it back. Unclear whether it's (a) deliberately kept as low-value legacy scaffolding pending removal, (b) still needed by an external/legacy import path not covered here, or (c) dead weight — worth confirming before deciding whether a reimplementation needs a file-based store at all.
3. **`packages/opencode/migration/`** (also an explicit starting point) contains a single leftover, tool-generated migration folder that predates the current generator, which now writes hand-wrapped TypeScript migration modules directly under the core package and is the only path covered by tests/CI. This directory looks orphaned from before migration generation moved into the core package; a test exists specifically to make sure it's never mistakenly replayed, suggesting the team is aware of it but hasn't removed it.
4. **The `sync/README.md` design doc** describes a distinct "SyncEvent" abstraction layered over an existing event bus; at this commit that design appears to have been realized as the more general "durable event" system described above, living in the core package rather than as a separate module under the CLI package's `sync/` directory (which now holds only a leftover id-schema file and the design doc). Worth confirming whether the README is intentionally-stale design history or describes work still pending.
5. **The `data_migration` bookkeeping table** exists in the schema (created by a real migration step) but has no other references anywhere in the codebase at this commit — treat it as inactive scaffolding for a not-yet-used one-off backfill mechanism unless the target team has plans for it.
6. **The local `/sync/*` HTTP surface** (start/replay/steal/history) is gated behind an experimental flag and is tightly coupled to the "workspace"/cloud-sandbox concept that belongs to the explicitly out-of-scope Control-Plane & Cloud Sync feature; this dossier describes only the generic durable-event mechanics it rides on, not what a workspace is or how remote sandboxes are provisioned/authenticated.
7. **Exact default and validation rules for the `share` config setting** (disabled / auto / on-demand) live in the Config feature, out of scope here; this dossier documents only how Storage/Sync/Share consumes that value (hard-stop on "disabled", background auto-share on "auto" for top-level sessions only).
