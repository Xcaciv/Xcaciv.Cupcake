# Feature: Codemode

## Purpose

Codemode replaces "call one tool at a time" agent loops with a single confined-script-execution
tool. Instead of exposing every connected tool directly to the model, the host exposes one
`execute` tool. Inside the script the model can call a tree of schema-described tools by property
path (e.g. `tools.orders.lookup(...)`), sequence dependent calls, run independent calls
concurrently, and filter/transform intermediate data before returning only what the model needs.

Goals: reduce the context spent listing large tool catalogs; avoid an agent round-trip between
every dependent tool call; keep large intermediate tool results inside the script instead of
routing them through the model's context; and give generated code no authority beyond the tools
the host explicitly supplied. It is explicitly an orchestration language, not a general-purpose
scripting sandbox and not an application-level authorization system — authorization stays the
responsibility of each underlying tool.

## Behavior

1. **Tool tree → catalog.** The host builds a tree of tool definitions (each with a description,
   an input schema, an optional output schema, and an implementation). Codemode turns this tree
   into (a) a budgeted, human/model-readable catalog of TypeScript-like call signatures grouped by
   namespace, and (b) a set of executable entry points reachable as `tools.<namespace>.<tool>(...)`
   inside scripts.
2. **Script submission.** The model writes one script in a restricted JavaScript/TypeScript-like
   language and submits it as the sole argument to the `execute` tool. TypeScript syntax is
   stripped, the script is parsed, and it runs on a tree-walking interpreter — no dynamic `eval`,
   no access to the host's real JS runtime globals.
3. **Tool calls from inside the script.** A call such as `tools.orders.lookup({ id })` begins
   running immediately in the background the moment it is written (not only when awaited), so
   independent calls started before any `await` execute concurrently. `await` (or returning the
   call) waits for and yields its result; `Promise.all`/`allSettled`/`race` combine several. Each
   call's input is validated/decoded against its schema before the underlying implementation runs,
   and its output is decoded and copied into plain data before the script can see it.
4. **Result.** When the script finishes (via `return`, or falling off the end, which yields
   `null`), the value is copied to plain JSON-safe data and handed back as the `execute` tool's
   result, together with captured `console.*` log lines and the ordered list of admitted tool
   calls. Failures (parse errors, disallowed syntax, unknown tool paths, bad input/output data,
   an exceeded limit, a tool's own failure, an uncaught script exception) are all returned as
   structured, categorized diagnostic data rather than throwing — except for the host cancelling
   the run, which is treated as an interruption, not a diagnostic.
5. **Discovery when the tool catalog is large.** Above a configurable size budget, only as many
   full signatures as fit are inlined (chosen round-robin across namespaces so no single namespace
   crowds out the rest); every namespace is still listed by name with its tool count. A
   built-in search entry point (`tools.$codemode.search`) is always callable and is advertised in
   the instructions whenever the inline listing is partial, letting the model look up an exact
   tool by name, browse a namespace, or keyword-search descriptions/parameter names, with
   pagination.
6. **File/binary content.** Binary or file-like tool outputs (e.g., images) never enter the
   script's data space. The host collects such attachments alongside the structured result while
   the underlying tool runs, and returns them out-of-band on the outer result; the script only
   ever sees a plain-data placeholder/marker for that value.

## Business rules & edge cases

- **Confinement is total.** No ambient filesystem, process, environment variable, network,
  credential, module/import, `eval`, or npm-package access exists inside the script. The only way
  a script exercises real-world authority is by calling a tool the host explicitly placed in the
  tree; each such tool remains solely responsible for its own authorization and side effects.
- **A namespace named `$codemode` is reserved** for the built-in search tool; a host cannot define
  its own top-level namespace with that name.
- **Schema boundary is strict, both directions.** A tool implementation is never invoked unless
  its declared input has already decoded successfully; a tool's result is never visible to the
  script unless its declared output decoded and copied into plain data successfully. Tools may
  describe their input/output either with a fully validating schema, or with a schema document
  used only to render the model-facing signature (values pass through unvalidated in that case) —
  this second mode exists to accommodate externally supplied tool definitions (e.g., MCP tools)
  whose schemas already arrive as plain schema documents. A tool with no declared output
  advertises an opaque/untyped result type to the model.
- **Unknown host failures are sanitized.** Only a tool's explicit, safe "I refuse/failed for this
  reason" message is ever shown to the model; any other internal exception is caught and reported
  generically so private causes never leak into model-visible diagnostics.
- **Concurrency cap.** At most 8 tool calls run concurrently regardless of how many the script
  starts; excess calls queue. This is a fixed internal bound, not a configurable option.
- **Data nesting cap.** Any value crossing into or out of the script (tool arguments, tool
  results, the final return value) may nest at most 32 levels deep; deeper structures are rejected
  as invalid data rather than causing a stack overflow.
- **JSON-like serialization semantics at the boundary.** Dates serialize to ISO-8601 strings (an
  invalid date becomes null); regular expressions, Maps, Sets, and URL-search-param objects
  serialize to empty objects — matching how plain `JSON.stringify` treats them. An unawaited
  promise value crossing a boundary is rejected with a diagnostic telling the caller to await it,
  rather than silently serializing to an empty object.
- **Language subset is deliberately incomplete, matching real JavaScript where it is intuitive
  and deviating where real JavaScript is surprising.** Supported: literals, control flow
  (`if`/`switch`/loops/`try-catch`), arrow/named functions with closures/defaults/rest/destructuring,
  optional chaining, nullish coalescing, template strings, spread, common Array/String/Number/
  Object/Math/JSON operations, Date, RegExp, Map, Set, URL/URLSearchParams, and first-class
  promises. Not supported: classes, generators, timers, dynamic import, `eval`, prototype
  mutation, custom Promise construction, and promise chaining via `.then`/`.catch`/`.finally`
  (only `await` + `try/catch` is offered). `for...in` over a `tools` reference yields its
  namespace/tool names; over anything else besides plain objects and arrays it is an error
  telling the model to use `for...of` or `Object.keys` instead of falling back to real
  JavaScript's often-surprising behavior (e.g. iterating string indices, zero iterations over a
  Map/Set).
- **Errors are catchable, typed data.** A thrown `Error` (or its standard subtypes) is caught as
  plain `{name, message}` data that still satisfies an `instanceof` check against the matching
  error type; interpreter-detected mistakes are mapped to the same error name a real JS engine
  would use (e.g., an unknown identifier reads as a reference error, invalid JSON as a syntax
  error); tool failures and otherwise-unclassified failures are named generically. Throwing a
  non-Error value is not treated as an Error instance, matching real JavaScript.
- **Unsupported syntax and unknown tool paths are structured diagnostics with source location
  where available**, not silent no-ops — a script that guesses a tool path or uses unsupported
  syntax gets an actionable, categorized failure back.
- **Regex execution runs on the host engine** (not reimplemented), so pathological
  backtracking is bounded only by the overall execution timeout, not by a dedicated regex budget.
- **Execution-limit defaults are intentionally absent.** None of the three configurable limits
  (wall-clock timeout, max tool calls, max output size) has a built-in default; a host that wants
  a bound must set one explicitly. This is a deliberate design choice: appropriate values are a
  product/host policy decision, not a library policy.
- **Oversized output degrades gracefully, it never fails the run.** When output exceeds the
  configured byte budget, the returned value is replaced with truncated serialized text plus an
  explanatory marker, captured logs are kept from the start until the budget runs out (with a
  trailing marker noting the cut), and the result is flagged as truncated.
- **Timeout interrupts everything, including work the script never awaited.** All in-flight tool
  calls (even ones the script forked and never `await`ed) are interrupted, and the interpreter
  yields control between execution steps so that even a pure infinite busy-loop is caught by the
  timeout — there is no separate CPU/step budget.
- **Reserved/dangerous property names.** How tool-tree path segments literally named things like
  `__proto__`, `constructor`, or `prototype` should be handled is called out in the source's own
  living design notes as an unresolved design question — see Confidence & open questions.

## Workflows & states

Intended model workflow, reflected in the generated instructions:
1. Pick an exact tool signature already inlined in the catalog, or, if the catalog is only
   partial, call the search entry point and use one of the exact paths it returns (search results
   include the ready-to-use call path, description, and full signature — no extra lookup needed).
2. Call the tool at the exact path returned/listed — never a guessed or "normalized" variant.
3. If a tool's result type is opaque (no declared output schema), narrow/inspect it before reading
   fields from it.
4. Start independent tool calls together and combine them (e.g., with a "wait for all" combinator)
   rather than awaiting them one at a time.
5. Filter, transform, and aggregate data inside the script; `return` only the fields the model
   actually needs, instead of dumping raw results back into the conversation.

Execution states, from the outer caller's perspective:
- **Success** — script completed, result decoded to plain data; carries the log lines and the
  ordered list of tool calls admitted during the run (name only, not full input/output — for
  auditing without leaking sensitive arguments).
- **Failure (diagnostic)** — categorized, model-safe error plus the tool calls admitted before the
  failure occurred plus captured logs. Program correctness and expected tool failures never
  surface as thrown host exceptions.
- **Interrupted** — a host-driven cancellation (e.g., user aborts the request) is not represented
  as a diagnostic result at all; it is real interruption of the interpreter and its in-flight tool
  calls, propagated the way any cooperative cancellation would be.

Observation hooks fire around each admitted tool call: one just before the underlying
implementation runs (carrying the call's position/index, tool name, and decoded input) and one
when it settles (adding duration and success/failure outcome, with a safe failure message when it
failed). An interrupted call never fires its "settled" hook. These are how a host renders live
"tool X: running / completed / error" progress in a UI while the script is still executing.

## Data

- **Tool tree**: a nested key-value structure; leaves are tool definitions, branches are
  namespaces, exposed to the script as the `tools` global.
- **Tool definition**: description string (model-visible), input schema, optional output schema,
  and an implementation function that receives decoded input and produces a result (or a safe
  failure / opaque failure).
- **Catalog entry / tool description**: path (dotted or bracket-quoted for non-identifier
  segments), one-line description, and a full rendered call signature (JSDoc-annotated
  parameter/return description comments, plus tags for constraints a type signature alone can't
  express, such as default value, format, deprecation, min/max item counts).
- **Script result**: `{ ok: true, value, logs?, truncated?, toolCalls }` on success or
  `{ ok: false, error: diagnostic, logs?, truncated?, toolCalls }` on failure, where `diagnostic`
  is `{ kind, message, location?, suggestions? }` and `kind` is one of: parse error, unsupported
  syntax, unknown tool, invalid tool input, invalid tool output, invalid data value, tool-call
  limit exceeded, timeout exceeded, tool failure, or generic execution failure.
- **Execution limits**: three independent numeric knobs — max wall-clock time, max number of
  admitted tool calls, max output size in bytes — all optional/unset by default.
- **Discovery options**: one knob — an approximate token budget (characters ÷ 4 heuristic,
  defaulting to 2,000) governing how many full signatures get inlined into the instructions.
- **Search request/response**: request is `{ query, namespace?, limit?, offset? }`; response is a
  ranked, paginated list of `{ path, description, signature }` plus a remaining-count and a
  next-page cursor (or none on the last page). Ranking is deterministic: exact path match scores
  highest, then path substring, then description substring, then a match anywhere in the tool's
  searchable text (which also includes input parameter names/descriptions); naive singular/plural
  query variants are tried too; ties break alphabetically by path.

## Interfaces

- **Tool System** (adjacent, not documented here): Codemode consumes whatever tool definitions the
  host's tool system decides to expose to it, and produces one opaque "execute" tool back to that
  same system. In the shipping product this boundary is realized by wrapping externally supplied
  (MCP-sourced) tool definitions one-for-one into Codemode's own tool-definition shape; the host's
  own built-in first-party tools (file read/write/edit, shell, search, etc.) are *not* placed into
  the Codemode tree and remain directly callable by the model as before.
- **Session & Conversation Runtime** (adjacent, not documented here): the host session runtime
  decides whether to build the `execute` tool for a given turn at all — only when there is at
  least one eligible externally-supplied tool. It wires cancellation of the model turn to
  interruption of the still-running script and its child tool calls; it renders the streamed
  per-call running/completed/error status into the visible task progress metadata; and it decides
  what happens to non-text results returned from inside a call (e.g., image results are captured
  by the host and reattached to the visible conversation, never routed back through the script's
  data space).
- **Plugin & MCP System** (adjacent, not documented here): each nested tool call made from inside
  a script still runs that tool's normal before/after lifecycle hooks and its normal
  authorization/permission check, keyed by the tool's real (flattened) name, exactly as if the
  model had called it directly — Codemode does not bypass or batch these hooks.
- **OpenAPI-to-tool adapter**: a self-contained utility that turns an OpenAPI document into a tool
  tree (one tool per operation, namespaced by dotted operation IDs, sanitized/deduplicated method
  + path names as fallback). It flattens path/query/header/body parameters into one model-facing
  object while preserving their original HTTP placement, handles a bounded set of parameter
  encodings and JSON bodies/responses, resolves bearer/basic/header/query authentication through a
  host-supplied resolver (never model-visible), and explicitly skips (rather than guesses at)
  operations using encodings, bodies, or auth schemes it does not support, returning a precise
  "skipped" reason for each. This adapter is not confirmed to be wired into the shipping product
  (see Confidence & open questions).

## External technology

| Technology | Role |
|---|---|
| TypeScript on Bun | Implementation language/runtime for the package itself |
| Effect (Effect-TS) | Structures schema validation/decoding, concurrent/interruptible execution ("fibers"), and typed error channels used to implement tool calls, timeouts, and diagnostics |
| Acorn | Parses the (TypeScript-stripped) script text into a syntax tree that the owned interpreter walks — no `eval`/dynamic code execution of untrusted script text |
| JSON Schema | Render-only schema format accepted for adapter-supplied tool signatures (e.g., MCP tool definitions) — shapes the displayed signature only, not validated against |
| OpenAPI 3.x | Input document format consumed by the OpenAPI-to-tool adapter |
| Model Context Protocol (MCP) | Source of the externally supplied tools that the shipping integration places into the Codemode tool tree |

## Error handling

- All *expected* failure modes (bad script syntax, disallowed syntax, calling a tool path that
  doesn't exist, tool input/output failing schema validation, a value violating the plain-data
  contract, exceeding a configured limit, a tool explicitly failing, an uncaught script exception)
  are returned as structured, categorized diagnostic data with a stable "kind", a model-safe
  message, and — where available — a source location and remediation suggestions.
- Tools distinguish two failure channels: an explicit, safe, model-visible refusal message (with
  an optional private cause that is used only for host-side logging and is never returned to the
  model), versus any other/unknown internal failure, which is always sanitized to a generic
  message before it can reach the model.
- Genuine host/process interruption (the surrounding cancellation mechanism, e.g. a user stopping
  a run) is categorically different from a diagnostic: it is not returned as a `Failure` result at
  all, it propagates as real interruption so the host's own cancellation handling runs normally.
- A call whose failure the script never observed (started but never awaited, and the run ends
  before it's checked) still surfaces as an "unhandled rejection"-style diagnostic rather than
  being silently dropped.
- Regex validity, flag validity, and use of a global-search operation without the required flag
  all fail as catchable script errors carrying an explanation of what was wrong and how to fix it,
  rather than as silent no-ops or interpreter crashes.

## Non-functional observations

- **Context/token economy is a first-order design goal**: the progressive catalog + search
  design, the JSDoc-compact signature rendering, and the "return only what's needed" workflow
  guidance are all aimed at keeping large tool surfaces from consuming the model's context window.
- **No default resource limits** — a reimplementation must decide its own policy for
  timeout/max-calls/max-output-bytes; the source package treats leaving all three unset as a valid
  and intentional configuration, not an oversight.
- **Deterministic behavior is prioritized** for anything the model might reason about across
  calls: search ranking, catalog ordering (round-robin, alphabetical), signature rendering, and
  serialization-at-the-boundary rules are all specified precisely enough to be reproducible, and
  known nondeterministic surfaces (e.g., a random-number source) are explicitly called out as
  something to decide on deliberately rather than include implicitly.
- **The interpreter's language subset is a moving target by design** — the source's own living
  design document tracks specific unsupported constructs (promise chaining beyond await/try-catch,
  async iteration, certain callback-bearing standard library methods, several `Object`/`Math`
  parity gaps) as backlog to grow only when it demonstrably improves orchestration, not as a goal
  of matching real JavaScript in full.
- **Tool-call concurrency (8) and data nesting depth (32) are fixed internal constants**, not
  part of the configurable contract — a reimplementation is free to choose its own values but
  should keep them fixed/internal rather than host-configurable if aiming for behavioral parity.

## Acceptance criteria — 5-8 Given/When/Then statements

1. Given a tool tree with at least one namespaced tool, when the capability is enabled for a turn,
   then the model is offered exactly one script-execution entry point (never the individual tools
   directly), and calling that entry point with a script that calls a listed tool by its exact
   path returns that tool's result to the model as the script's own return value.
2. Given a script that starts two independent tool calls and only afterward waits on both
   together, when the script executes, then both calls run concurrently rather than sequentially,
   and both results are available together once the combined wait resolves.
3. Given a tool catalog too large to fit the configured token budget, when the model requests the
   instructions/catalog, then every namespace is still listed by name and tool count, only a
   budget-fitting subset of namespaces get full inlined signatures (chosen so no single namespace
   crowds out the others), and the search entry point is advertised as available and usable for
   any tool not inlined.
4. Given a tool call inside a script whose input fails schema validation, when the script executes
   that call, then the underlying tool implementation is never invoked and the run ends with an
   "invalid tool input" diagnostic rather than a raw exception.
5. Given a tool implementation that fails with an unclassified internal error (not an explicit
   safe refusal), when a script calls it, then the model-visible diagnostic message never contains
   the internal error's private details, and the call registers as a failed admitted call.
6. Given a configured wall-clock timeout, when a script never returns within that time — including
   because it is stuck in a pure loop with no tool calls — then execution stops, in-flight tool
   calls (whether awaited or not) are interrupted, and the run ends with a "timeout exceeded"
   diagnostic including whatever calls were already admitted.
7. Given a configured maximum output size, when a script's return value or captured logs would
   exceed it, then the run still reports success with a truncated value/logs and a flag indicating
   truncation occurred, rather than failing the run.
8. Given a host-driven cancellation of the surrounding turn (e.g., user abort) while a script is
   running, when that cancellation fires, then the script and any in-flight tool calls are
   interrupted through the host's normal cancellation path, and no ordinary diagnostic "Failure"
   result is produced for that case.

## Confidence & open questions

- **Wired into the shipping CLI, but behind an explicit opt-in flag, and only for a subset of
  tools.** In this codebase the capability is integrated (`packages/opencode/src/tool/code-mode.ts`,
  wired through `packages/opencode/src/tool/registry.ts` and `packages/opencode/src/session/tools.ts`),
  but it is gated by a feature flag (`OPENCODE_EXPERIMENTAL_CODE_MODE`, or the blanket
  `OPENCODE_EXPERIMENTAL` switch) that defaults to off. When enabled, the `execute` tool is built
  only from externally supplied (MCP-sourced) tools; the product's own built-in first-party tools
  (file/shell/search/etc.) are never placed inside the script tree and stay directly callable.
  This confirms the capability is real and tested end-to-end (see the integration test at
  `packages/opencode/test/tool/code-mode-integration.test.ts`), but it is not the default
  operating mode of the shipping agent as of the pinned commit — a reimplementer should treat it
  as an optional, currently niche capability layered on top of a conventional per-call tool loop,
  not as the primary tool-invocation path.
- **A described "V2/Core" integration is not present in this checkout.** The package's own living
  design document (`packages/codemode/codemode.md`) describes a broader integration through
  `packages/core/src/tool/registry.ts` (a canonical tool representation, deferred/grouped tool
  registration feeding an automatically-reserved `execute` tool for *any* deferred tools, not just
  MCP) as belonging to a "V2" line of development. At the pinned commit, `packages/core/src/tool/`
  contains no reference to Codemode or to any "deferred tool" concept at all — that broader
  integration either lives on a different branch not present here, or has not landed yet. Treat
  the simpler, MCP-only, flag-gated integration described above as the only integration actually
  present in this codebase.
- **OpenAPI-to-tool adapter's product usage is unconfirmed.** The adapter that turns an OpenAPI
  document into a Codemode tool tree is fully implemented and tested inside the package
  (`packages/codemode/src/openapi/*`, `packages/codemode/test/openapi.test.ts`), but no reference
  to it was found anywhere under `packages/opencode/src` or `packages/core/src` — it appears to be
  a general-purpose library capability the package ships but that the shipping product does not
  currently invoke. A reimplementer targeting only what end users of the CLI experience could
  reasonably treat this adapter as out of scope, or as a nice-to-have library feature.
- **Exact search-ranking weights and the character-per-token heuristic (÷4) are treated here as
  behavioral requirements** taken directly from the package's own documentation and mirrored in
  its tests; they are somewhat arbitrary tuning constants rather than principled algorithms, and a
  reimplementation aiming for "equivalent user experience" rather than "identical output" could
  reasonably choose different constants as long as the qualitative properties (deterministic,
  fair-across-namespaces, substring/plural-tolerant) are preserved.
- **The handling of reserved property names as tool-path segments** (e.g. a tool literally named
  `constructor` or `__proto__`) is called out in the source's own design notes as an explicitly
  unresolved question at the pinned commit, not a settled behavior — a reimplementer should not
  assume any particular documented behavior here without checking a later revision.
