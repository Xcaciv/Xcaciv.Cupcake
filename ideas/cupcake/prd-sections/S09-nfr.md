## 9. Non-Functional Requirements

This section states the qualities the clone must exhibit, as distinct from the behaviours §7 specifies.
It is short on aspiration and long on arithmetic: almost every non-functional property of this product
is a number, and almost none of those numbers were chosen by the product. Reading them correctly is
the whole job of this section.

**How to read the classification tags.** Every observed number carries one:

| Tag | Meaning | What the clone owes it |
|---|---|---|
| **[BUSINESS RULE]** | The number is written in the product's own source, and the source states or strongly implies why. | Reproduce it. It is a product decision. |
| **[STACK DEFAULT]** | The number is not written in the product at all. It is the default of the Command Service or of the registry library the Package Registry Client sits on, inherited because the product never configures the knob. | Decide it deliberately. A clone on a different stack that configures nothing will inherit *its own* stack's number, not this one — so "do nothing" does **not** reproduce the behaviour. |
| **[UNCLASSIFIED LITERAL]** | The number is written in the product's source with no stated rationale. | Reproduce it for parity, but record that its intent is unknown. Classification as a business rule is INFERRED from its shape alone. |

**Evidence conventions** follow §7: an in-repo path with line numbers is direct observation at the
pinned commit; `OUT-OF-REPO (framework v2.1.2)` refers to the external command framework at commit
`f34dedca8dc6d690290b2139acbd3e9b8264349c`, **one patch release ahead** of the versions the product
pins (see §11.3 assumption 1). Requirements resting on no in-repo evidence at all say
"OUT-OF-REPO evidence only".

**Standing caveat.** Nothing in this section was measured. The product does not build at the pinned
commit for two independent, established reasons (§2.4, §9.8): a deleted declaration in the Package
Search Command with five surviving uses, and a dependency restore that fails for every project. There
are therefore **no latency figures, no throughput figures, no memory figures and no profiles anywhere
in this document** — every quantitative statement below is a configured limit read from source, not an
observed measurement. Where a claim required execution to settle, it says so at the point of claim.

**One primitive was executed, and it changes how several requirements below must be read.** The
Plugin Scanner composes its search mask by joining a wildcard path segment, the sub-directory name
and the fixed binary pattern, producing `*/bin/*.dll`, and hands that whole string to a recursive
directory enumeration rooted at the verified Plugin Directory (OUT-OF-REPO evidence only, framework
v2.1.2: `src/Xcaciv.Command.FileLoader/Crawler.cs:20`, `:173-178`). That enumeration was run against
a real filesystem with a perfectly correct layout in place (`<root>/HelloPkg/bin/Hello.dll` and
`<root>/Other/bin/Other.dll`): it returned neither those files nor an empty set, but **raised a
directory-not-found condition naming the literal path `<root>/*/bin`**, in both recursive and
top-level enumeration modes, while a control run with the plain pattern `*.dll` recursively found
both files. The directory portion of a search mask is joined to the root literally; wildcards in it
are never expanded. (Executed on the analysis host with a current runtime; the enumeration semantic
is long-standing and INFERRED to be identical on the runtime the product targets.) The consequences
are established in §7.4 FR-4.25 and FR-4.42 and are carried into every scanning-related requirement
below — NFR-10, NFR-33, NFR-40, NFR-41, NFR-44, NFR-57, NFR-AC-7, NFR-AC-8 and NFR-AC-11: **no
Plugin is ever loaded from disk on a real filesystem, any Plugin Directory that survives
verification is fatal at startup whether it is populated or empty, and the only startup path that
reaches a usable Prompt is the one where the Plugin Directory does not exist.** The framework's own
scanner tests pass because they run against a mock filesystem that INFERRED expands the wildcard
segment; the production path uses the real filesystem and cannot. The register's nearest row is
Q-11, which under-states the defect, and the open question is OQ-7.

---

### 9.1 Concurrency and threading

- **NFR-1 — Command lines are dispatched strictly serially.** The Session dispatches exactly one
  Command line at a time and does not redraw the Prompt until that dispatch has returned. Evidence:
  `src/Xcaciv.Cupcake.Core/Loop.cs:57-66`, `:91-100`.

- **NFR-2 — There is no job control.** No background execution, no job table, no suspend, no resume,
  no kill, no interrupt handling, no signal handler. A running Command owns the Session until it
  finishes. Evidence: `src/Xcaciv.Cupcake.Core/Loop.cs:56-66` contains the whole dispatch loop and
  nothing else.

- **NFR-3 — The Session consumes an asynchronous contract by blocking.** Every step — the startup
  Status line, each dispatch, each read — is waited on synchronously. This is stated design, not
  accident: the shipping executable is named and described as the deliberately synchronous build.
  Evidence: `src/Xcaciv.Cupcake.Core/Loop.cs:37,62,65`; `src/Xcaciv.Cupcake.Lit/README.md:1-3`
  ("A light and syncronous implementation of Cupcake shell.", spelling as in the source).
  INFERRED for a clone: on a platform with a single-threaded event loop this shape deadlocks, and a
  clone should write the loop natively synchronous rather than translate the blocking-on-asynchronous
  idiom literally. No observable rule in §7 depends on the idiom.

- **NFR-4 — The asynchronous Session entry point introduces no parallelism.** It moves the same
  strictly serial loop onto a worker; it does not overlap Command lines. Evidence:
  `src/Xcaciv.Cupcake.Core/Loop.cs:89-101`.

- **NFR-5 — Pipeline stages run concurrently.** Every stage of a Pipeline is started before any stage
  is awaited: each is given its own child Interaction Context, its 1-based stage number, the stage
  total, the previous stage's Stage Channel as input and a fresh Stage Channel as output, and is added
  to a task list; the whole list is then awaited together. OUT-OF-REPO evidence only (framework
  v2.1.2): `src/Xcaciv.Command/PipelineExecutor.cs:81-105` (stage construction), `:103` (started, not
  awaited), `:43,46,55` (awaited together).

- **NFR-6 — The published Interaction Context contract requires thread-safety.** Its documented
  remarks state that implementations "must be thread-safe and prioritize pipeline output when
  channels are available". This is an obligation on every Interaction Context implementation, and the
  clone inherits it the moment it supports Pipelines. OUT-OF-REPO evidence only (framework v2.1.2):
  `src/Xcaciv.Command.Interface/IIoContext.cs:21-23`.

- **NFR-7 — QUIRK (register Q-35). The Presentation Adapter does not meet that obligation.** It takes
  no lock, holds no per-context buffer, and paints by a **three-step, non-atomic** sequence — set
  foreground, set background, write — against **process-global** terminal state, with a reset as a
  fourth step on two of its three channels. Every child Interaction Context of a Pipeline is another
  Presentation Adapter painting the same global state. Evidence:
  `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:53-60` (Output), `:66-72` (Prompt, no reset),
  `:90-103` (Status), `:38-47` (children are more of the same); against OUT-OF-REPO (framework
  v2.1.2): `src/Xcaciv.Command.Interface/IIoContext.cs:21-23`. The keep-or-fix decision is §11 Q-35.

- **NFR-8 — The exposure of NFR-7 is narrower than it first appears, and the clone must understand
  why.** During a Pipeline every stage has an output Stage Channel attached, and the routing rule
  sends Output to the attached channel rather than to the terminal; the final Stage Channel is drained
  into the parent Interaction Context only *after* every stage has completed. So **Output from a
  Pipeline is painted serially, by one context, after the fact**. What is *not* serialised is
  everything that bypasses the channels: Status lines, progress readings and Command-failure text,
  each painted immediately by whichever stage produced it. Two stages reporting progress or failing at
  the same moment can therefore interleave mid-line, and one stage's colour setting can bleed onto
  another stage's text. INFERRED (reasoned from the routing and drain order; never executed).
  OUT-OF-REPO evidence only (framework v2.1.2): `src/Xcaciv.Command.Core/AbstractTextIo.cs:67-74`
  (routing: an attached output channel wins over the terminal),
  `src/Xcaciv.Command/PipelineExecutor.cs:55,63,179-193` (drain after completion),
  `src/Xcaciv.Command/CommandExecutor.cs:229` (per-stage failure Status).

- **NFR-9 — A clone shall take an explicit position on NFR-7.** Either serialise all painting behind
  one lock (meeting the contract, at the cost of a behaviour the source does not have), or reproduce
  the unsynchronised adapter and record the interleaving as accepted. Silence is not an option,
  because the contract the clone publishes to Plugin authors states the obligation either way.

- **NFR-10 — Plugin scanning may run on multiple workers.** Above the parallelism threshold (NFR-33)
  the Plugin Scanner processes discovered Plugin binaries in parallel, and the per-binary callback it
  invokes is documented as required to be thread-safe. The Command registration index is safe for
  concurrent population, which is what makes the parallel scan viable. OUT-OF-REPO evidence only
  (framework v2.1.2): `src/Xcaciv.Command.FileLoader/Crawler.cs:168,182-190`;
  `src/Xcaciv.Command/CommandRegistry.cs:12-18`. INFERRED consequence for §7.6: a startup failure may
  originate on a worker rather than on the Session's own thread.
  **As shipped, neither branch is ever entered.** The enumeration that produces the candidate list
  raises a directory-not-found condition before it returns a single file (standing caveat above;
  §7.4 FR-4.25), so the parallel/sequential switch is unreachable on a real filesystem. This
  requirement is stated for the clone, which must implement real segment-spanning glob semantics and
  will then reach it. Nearest register row Q-11; see OQ-7.

- **NFR-11 — Command execution shares no mutable state between concurrent stages.** A fresh Command
  instance is created per invocation, each execution runs against a child Session Variables scope, and
  the Session Variables store itself is a thread-safe map. Package Registry Client operations hold no
  instance state at all. Evidence: `src/Xcaciv.Command.Packages/NugetWrapper.cs:18-131` (every
  operation is a stateless entry point); OUT-OF-REPO (framework v2.1.2):
  `src/Xcaciv.Command/CommandFactory.cs:38-56`, `src/Xcaciv.Command/CommandExecutor.cs:187`,
  `src/Xcaciv.Command/EnvironmentContext.cs:18`.

  **The child scope is discarded unless the Command declared itself environment-modifying, and that
  is what bounds the read-side write-back.** The child is seeded with a copy of the parent's
  values, and it is merged back into the caller's store **only** when the Command's registration
  record carries the environment-modifying flag *and* the child reports itself changed. The flag
  defaults to off, and the shipping host registers both Host-linked Commands without setting it.
  Reading a missing Session Variable with default options genuinely does write the default back under
  the upper-cased key — but on the Shell's own dispatch path that write lands in the per-execution
  child and is thrown away on return, so a later variable dump does **not** list the key. The side
  effect is observable only on a **direct-invocation** path, where a host hands a Command its own
  store instead of a child — which is exactly what this repository's tests do. **This corrects a
  stronger claim made elsewhere in earlier drafts:** the register's Q-10 (and the echo Built-in's
  failed `%NAME%` resolutions) describe a real quirk, but its scope is the direct path, not the
  Shell. It remains a trap for a clone that registers Package Search as environment-modifying.
  Evidence: `src/Xcaciv.Cupcake.Lit/Program.cs:11` (registered with no flag);
  `Xcaciv.Command.PackagesTests/SearchCommandTests.cs:15,31,90` (the direct path the tests take);
  OUT-OF-REPO (framework v2.1.2): `src/Xcaciv.Command/CommandExecutor.cs:187` (child created),
  `:216-219` (merged back only when flagged and changed),
  `src/Xcaciv.Command/CommandController.cs:190` (flag defaults to off),
  `src/Xcaciv.Command/EnvironmentContext.cs:45-55` (child seeded from a copy), `:94-110` (the
  default-storing read).

- **NFR-12 — QUIRK — register Q-48. Two concurrent downloads to the same target path corrupt each
  other.** The Package Registry Client opens the destination with create-or-truncate semantics and
  takes no lock; nothing serialises two downloads naming the same file. Unreachable today because
  Package Install is inert (§7.9, Q-46), but it is a defect a clone finishing that feature will
  inherit — and a clone that resolves Q-48 by adding signature or checksum verification alone still
  ships this race. Evidence: `src/Xcaciv.Command.Packages/NugetWrapper.cs:89`.

---

### 9.2 Throughput and buffering

- **NFR-13 — Stage Channel capacity is 10,000 items. [STACK DEFAULT]** Each connection between two
  Pipeline stages is a bounded, ordered buffer holding at most 10,000 Output items. The framework's
  own note sizes this at "approximately 100MB for 10KB strings" and states the intent as
  denial-of-service protection. The product never sets it. Cross-reference §6.1 GR-9. OUT-OF-REPO
  evidence only (framework v2.1.2): `src/Xcaciv.Command.Interface/PipelineConfiguration.cs:5-7,11-15`;
  `src/Xcaciv.Command/PipelineExecutor.cs:97-100` (the bound applied at channel creation).

- **NFR-14 — The full-buffer policy is Block: producers wait. [STACK DEFAULT]** Three policies exist —
  drop the oldest item, drop the newest item, or block the producer until the consumer makes room.
  Block is the default and the product never chooses otherwise. The clone must reproduce **block**,
  not drop: under load a slow consumer slows its producer, and no Output is ever silently lost.
  OUT-OF-REPO evidence only (framework v2.1.2):
  `src/Xcaciv.Command.Interface/PipelineConfiguration.cs:17-23`;
  `src/Xcaciv.Command/PipelineExecutor.cs:195-201` (policy translated to the buffer's full-mode).

- **NFR-15 — What a clone inherits by not configuring the buffer.** The Command Service exposes the
  whole Pipeline resource configuration as a settable property on the controller. The product never
  touches it — the Session constructs a stock Command Service and configures nothing. A clone that
  likewise configures nothing will inherit **its own** platform's queue default, which will not be
  10,000 and may not block. Reproducing NFR-13 and NFR-14 therefore requires *writing* them down, not
  omitting them. Evidence: `src/Xcaciv.Cupcake.Core/Loop.cs:25` (stock construction), and a repo-wide
  search finds no Pipeline configuration anywhere in the product; OUT-OF-REPO (framework v2.1.2):
  `src/Xcaciv.Command/CommandController.cs:147-154` (the property that is never set).

- **NFR-16 — QUIRK — register Q-64. INFERRED. A final Pipeline stage emitting more than 10,000
  Outputs hangs the
  Shell.** The final Stage Channel has no reader until every stage has completed, but the buffer is
  bounded at 10,000 and its full-buffer policy is to block the producer. A final stage whose 10,001st
  Output is written therefore waits for a consumer that cannot start until it finishes. INFERRED —
  reasoned from the ordering at OUT-OF-REPO (framework v2.1.2)
  `src/Xcaciv.Command/PipelineExecutor.cs:43,55,63,97-100,179-193`; never executed. Not reachable with
  the product's own Commands (Package Search emits its entire result set as **one** Output), so it is
  a Plugin-facing hazard. Rests on OUT-OF-REPO evidence only. Cross-reference §6.1 GR-9, which
  states the same stall as a product-wide rule.

- **NFR-17 — There is no batching, no paging, no rate limiting and no output throttling.** Every
  Output is one immediate write. There is no page-at-a-time rendering, no line-count limit, no
  scrollback management, no coalescing of consecutive writes, and no reuse of buffers. Colours are
  re-set before every single line even when unchanged, so each painted Output costs four terminal
  operations (two colour sets, one write, one reset) and each Prompt costs three plus a blocking read.
  Evidence: `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:53-60,66-72,90-103`.

- **NFR-18 — Memory profile: streaming where it matters, materialised where it does not.** A Package
  Archive is streamed to disk rather than buffered in memory, so archive size does not bound memory.
  Search results and version lists are fully materialised into collections before being returned, but
  both are bounded by NFR-27. The Session holds one Command line and its settings; nothing accumulates
  across iterations. Evidence: `src/Xcaciv.Command.Packages/NugetWrapper.cs:89-98` (streamed), `:31`,
  `:48`, `:72` (materialised); `src/Xcaciv.Cupcake.Core/Loop.cs:56,65`.

---

### 9.3 Timeouts and cancellation

- **NFR-19 — Every timeout and every output cap defaults to disabled. [STACK DEFAULT]** Four knobs
  exist in the Pipeline resource configuration and all four hold the value `0`, which each one defines
  as "off": the **whole-Pipeline timeout** (`0` = no timeout, unlimited execution time), the
  **per-stage timeout** (`0` = no per-stage timeout, unlimited per stage), the **per-stage Output byte
  cap** (`0` = unlimited bytes), and the **per-stage Output item cap** (`0` = unlimited items). The
  product sets none of them (NFR-15). Cross-reference §6.1 GR-9. OUT-OF-REPO evidence only (framework
  v2.1.2): `src/Xcaciv.Command.Interface/PipelineConfiguration.cs:25-50`. All four are **[STACK DEFAULT]**: a
  clone inherits *nothing* by omission and must write the four zeros down explicitly to reproduce the
  behaviour.

- **NFR-20 — The product never supplies a cancellation signal.** The Command Service publishes two
  dispatch forms, one taking a cancellation signal and one not. Both Session entry points call the
  form that supplies none. The Package Registry Client accepts an optional cancellation signal on two
  of its six operations and defaults it to "never cancelled" when the caller omits it — which every
  caller does — and hard-codes "never cancelled" at the two remaining asynchronous operations; its
  other two operations are synchronous and take no signal at all.
  Nothing anywhere in the
  product constructs a cancellation source. Evidence: `src/Xcaciv.Cupcake.Core/Loop.cs:62`, `:94`;
  `src/Xcaciv.Command.Packages/NugetWrapper.cs:24`, `:56` (the two defaulted), `:46`, `:84` (the two
  hard-coded); OUT-OF-REPO (framework
  v2.1.2): `src/Xcaciv.Command.Interface/ICommandController.cs:36` (the form used) versus `:44` (the
  form with cancellation, never used).

- **NFR-21 — Waiting for input is unbounded.** The read blocks the Session for as long as the Shell
  user takes to type. There is no idle timeout, no maximum Session length, no auto-exit and no
  interrupt path. Evidence: `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:71`;
  `src/Xcaciv.Cupcake.Core/Loop.cs:65`.

- **NFR-22 — Waiting for a Command is unbounded.** Dispatch blocks the Session until the Command
  returns. For Package Search this means the registry round trip happens on the same thread that reads
  the Prompt: the Prompt is frozen for the whole round trip, there is no progress indication, no
  spinner, and no way for the Shell user to abandon the wait. A hung Package Registry hangs the Shell
  indefinitely. Evidence: `src/Xcaciv.Command.Packages/SearchCommand.cs:60`;
  `src/Xcaciv.Cupcake.Core/Loop.cs:62`.

- **NFR-23 — There is no retry, no back-off and no circuit breaking anywhere.** Startup Plugin loading
  is attempted exactly once and its failure is terminal (§7.6). A failed registry call is not retried.
  Evidence: `src/Xcaciv.Cupcake.Core/Loop.cs:39-54`; `src/Xcaciv.Command.Packages/NugetWrapper.cs:18-131`
  (no retry construct anywhere in the file).

- **NFR-24 — There is no rate limiting.** Neither the number of Command lines per unit time, nor the
  number of registry queries, nor the volume of Output is bounded by anything other than the Shell
  user's typing speed.

- **NFR-25 — Consequence: both stage-completion messages are unreachable under stock settings.** With
  the per-stage timeout at `0` and no cancellation signal supplied, neither `Stage '<name>' exceeded
  timeout of <n> seconds` nor `Stage '<name>' was cancelled` can be produced. Every stage closes with
  no message and nothing is painted at stage completion. A clone must still implement both messages
  for Shell Operators who configure a timeout. Cross-reference §7.3 FR-3.19, FR-3.20; §11 Q-36.
  OUT-OF-REPO evidence only (framework v2.1.2): `src/Xcaciv.Command/PipelineExecutor.cs:149-165`.

- **NFR-26 — The disabled timeouts are the *only* denial-of-service posture the product has, and it is
  the permissive one.** The single limit switched on by default is the 10,000-item buffer (NFR-13),
  and blocking on a full buffer bounds memory rather than time. A Plugin that never returns holds the
  Session forever, and the framework's own stated intent for these knobs — DoS protection — is
  therefore unrealised in this product. Documented as observed; the keep-or-fix decision belongs to
  §11. OUT-OF-REPO evidence only (framework v2.1.2):
  `src/Xcaciv.Command.Interface/PipelineConfiguration.cs:5-7`.

---

### 9.4 Scale and limits

- **NFR-27 — The search Result limit is clamped to the closed range 1 to 100. [BUSINESS RULE]** A
  requested value below 1 becomes 1 and above 100 becomes 100; the clamp is silent, with no message.
  The intent is stated in the source: the comment immediately above reads `Clamp limit to prevent
  abuse`. Evidence: `src/Xcaciv.Command.Packages/SearchCommand.cs:41,46`.

- **NFR-28 — The declared default Result limit is 20. [BUSINESS RULE]** Declared as the Named
  Parameter's default value, so it applies whenever the parameter is omitted *and* at least one
  argument was supplied — with zero arguments binding short-circuits before any default is applied,
  and the invocation fails on the Result limit lookup with `The 'take' parameter must be a valid
  integer value.` rather than on the missing search term (§7.8 FR-8.11, FR-8.12; register Q-5).
  Evidence: `src/Xcaciv.Command.Packages/SearchCommand.cs:15`.

- **NFR-29 — There is no pagination, so 100 is an absolute ceiling per search term.** The query is
  issued at offset `0` with page size equal to the clamped Result limit, and there is no continuation,
  cursor, offset parameter or "next page" affordance anywhere. Results beyond the hundredth are
  unreachable by any configuration. Evidence: `src/Xcaciv.Command.Packages/NugetWrapper.cs:29`
  (offset `0`, page size = limit); `src/Xcaciv.Command.Packages/SearchCommand.cs:46`.

- **NFR-30 — There is no pagination anywhere else either.** Help listings, Session Variable dumps and
  Pipeline Output are all emitted in full, however long: a Command Group's help emits one line per
  member with no break, and the variable dump concatenates every key/value pair into a single string.
  OUT-OF-REPO evidence only (framework v2.1.2):
  `src/Xcaciv.Command/CommandExecutor.cs:57-73` (the help branch), `:260-289` (every member emitted,
  no page break); `src/Xcaciv.Command/Commands/EnvCommand.cs:15-23` (the whole store concatenated);
  `src/Xcaciv.Command/PipelineExecutor.cs:179-193` (the final channel drained in full).

- **NFR-31 — The search term is truncated at 200 characters. [UNCLASSIFIED LITERAL]** A longer term is
  silently cut to its first 200 characters and the query proceeds; there is no message and no
  rejection. Unlike the Result limit clamp, this number carries **no stated rationale** — the only
  comment above the block reads `Validate search terms`. Treating it as a cost or abuse bound is
  INFERRED from its shape alone. Evidence: `src/Xcaciv.Command.Packages/SearchCommand.cs:49,55-58`.

- **NFR-32 — An empty or whitespace-only search term short-circuits with no round trip.** Evidence:
  `src/Xcaciv.Command.Packages/SearchCommand.cs:50-54`.

- **NFR-33 — Plugin scanning switches to parallel above 50 discovered Plugin binaries. [STACK
  DEFAULT]** The comparison is strictly greater than 50: 50 binaries scan sequentially, 51 scan in
  parallel. The stated intent is in the framework's own comment — "avoid overhead of paralell if it is
  not needed" (spelling as in the source). The threshold is a **mutable process-global setting**, not
  a constant, and the product never pins it. OUT-OF-REPO evidence only (framework v2.1.2):
  `src/Xcaciv.Command.FileLoader/Crawler.cs:16-18` (value and comment), `:182-183` (comment and
  comparison).
  **As shipped the threshold is never reached, in either direction.** The comparison is made on the
  candidate list, and the enumeration that would produce that list raises before returning anything
  (standing caveat above; §7.4 FR-4.25), so on a real filesystem neither 50 nor 51 Plugin binaries
  are ever counted. The number is reproduced here because a clone that fixes the mask inherits the
  threshold decision, not because the reference implementation exercises it.

- **NFR-34 — Package Registry responses are cached on disk for 30 minutes. [STACK DEFAULT, INFERRED]**
  The Package Registry Client opens a registry cache scope on three of its six operations — version
  enumeration (which opens two), dependency resolution, and download — and **configures none of them**,
  so all inherit the library's own response lifetime, which is 30 minutes. Keyword search, the one
  operation a Shell user can actually reach today, opens no cache scope of its own; whatever caching
  it gets is the search capability's own. The 30-minute figure is INFERRED as the library default:
  nothing in this repository sets it, and the pinned artefacts could not be fetched to confirm it
  (§2.4). Evidence: `src/Xcaciv.Command.Packages/NugetWrapper.cs:37`, `:43`, `:62`, `:85` (four scopes
  opened, none configured); `:20-31` (search opens none).

- **NFR-35 — QUIRK — register Q-49. Cache-scope lifetime is handled three different ways in one
  file.** One scope is
  properly opened and released (dependency resolution, `:62`); two are created and never released
  (version enumeration `:37`, download `:85`); and one is opened *with* release and then never used at
  all, because the enquiry it belongs to passes the unreleased one instead (`:43` versus `:46`). A
  clone should treat cache-scope lifetime as an explicit decision rather than copy this. Evidence:
  `src/Xcaciv.Command.Packages/NugetWrapper.cs:37,43,46,62,85`.

- **NFR-36 — No other quantitative limit exists.** There is no maximum Command line length, no maximum
  Output size, no maximum number of Plugins, no maximum number of Session Variables, no disk quota, no
  download size limit, no connection limit and no throttle. Every bound the product has is listed in
  the number register at §9.11.

---

### 9.5 Startup latency and packaging

- **NFR-37 — The shipping build is precompiled ahead of time.** The Release profile enables
  ahead-of-time precompilation of the shipped code, which shortens cold start. Evidence:
  `src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj:16`. INFERRED intent: the setting is directly
  observed, but no comment, commit message or document in the repository states *why* — startup
  latency is the reading, not a claim the source makes.

- **NFR-38 — The shipping build is a single compressed file.** The whole program plus its runtime is
  bundled into one file, and the bundle is compressed. Compression trades a smaller download for
  decompression work at **every** launch, so it partly offsets NFR-37. Evidence:
  `src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj:13,17,18` (the single-file setting appears twice,
  redundantly).

- **NFR-39 — The shipping build is self-contained: the target machine needs nothing pre-installed.**
  Evidence: `src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj:14`. INFERRED intent, same basis as
  NFR-37. Combined with NFR-38 and the dead-code elimination of NFR-48, the three settings read as
  one goal: a zero-prerequisite download that is as small as a bundled runtime can be.

- **NFR-40 — Startup does exactly two filesystem-touching steps before the Prompt appears.** Register
  the Plugin Directory, then load Commands from it. The `Loading Commands` Status line is painted
  before both and exists precisely to cover that latency. Evidence:
  `src/Xcaciv.Cupcake.Core/Loop.cs:37,42-43`. **On a real filesystem the second step never completes
  unless the first found nothing:** if the Plugin Directory exists and survives verification, the
  scan raises a directory-not-found condition naming `<root>/*/bin`, which is not the tolerated No
  Plugins Available condition, so it is wrapped as a Startup Load Failure carrying
  `Unable to load commands.` and the process exits with status `1` without ever drawing the Prompt
  (standing caveat above; §7.4 FR-4.25, FR-4.42; §7.6). Startup latency in the sense this requirement
  measures is therefore only ever observed on the path where no directory was verified.

- **NFR-41 — Plugin discovery happens exactly once per Session.** There is no rescan, no filesystem
  watch, and no reload verb. A newly installed Plugin is visible only after restarting the Shell — a
  limitation the source itself acknowledges in an unfinished note about downloading a first Plugin and
  restarting loading. Evidence: `src/Xcaciv.Cupcake.Core/Loop.cs:41-43`, `:48`, `:79-80`. Plugin
  binaries are additionally never unloaded once loaded, for performance. OUT-OF-REPO (framework
  v2.1.2): `src/Xcaciv.Command.Tests/CommandControllerTests.cs:107`.

- **NFR-42 — There is no caching of any kind at the startup or distribution layer.** Plugins are
  re-scanned from disk on every launch; no discovery result is persisted between Sessions. (Re-scanned
  in intent only: per NFR-40 the scan cannot succeed on a real filesystem.)

- **NFR-43 — The build is not reproducible and never was.** There is no continuous-integration
  definition, no build script and no publish script anywhere in the repository; the delivered artefact
  is whatever a developer's local publish produced. Evidence: the tracked tree at the pinned commit
  contains no workflow directory, build script or publish profile — the same search recorded in
  §11 OQ-9's "where we looked" column. Combined with §2.4, **no shipping artefact of this product is
  known to have been produced at all**, and per NFR-47 the shipping profile could not resolve its own
  dependencies if one were attempted (§7.11 FR-11.34, register Q-52).

---

### 9.6 Portability

- **NFR-44 — The default Plugin Directory literal is Windows-shaped.** It is the relative path
  `.\packages`, with a backslash separator. On POSIX platforms a backslash is an ordinary filename
  character, so the literal names a single directory whose name contains a backslash, not a
  sub-directory named `packages`. Evidence: `src/Xcaciv.Cupcake.Core/Loop.cs:24`. **QUIRK**, register
  Q-25. Its consequence is silent: a Plugin Directory that fails verification is discarded without a
  diagnostic, so on a POSIX host the Shell simply reports that no Plugins were found (§7.4, §7.6).
  **Read together with NFR-40 this is the load-bearing accident of the whole product.** Because the
  default literal cannot name an existing directory on a POSIX host, the default configuration takes
  the only startup path that reaches a usable Prompt. A Shell Operator who "corrects" the separator,
  or a user who creates the folder, converts a working Shell into one that exits with
  `Error Unable to load commands.` — whatever the folder contains (§7.4 FR-4.42, register Q-11,
  OQ-7).

- **NFR-45 — The Plugin Directory resolves against the process working directory, not the executable's
  directory,** so *where the Shell was launched from* silently changes which Plugins load. This is
  also what makes the containment boundary of §6.6 a property of the launch location. Evidence:
  `src/Xcaciv.Cupcake.Core/Loop.cs:24,42`; §11 Q-26.

- **NFR-46 — The Prompt requires a UTF-8 capable terminal and font.** The Prompt is exactly three
  characters: U+0190 LATIN CAPITAL LETTER OPEN E, then `>`, then one space. The first is the only
  non-ASCII character in any user-visible text the product produces. A terminal or code page that
  cannot render it shows a replacement glyph in place of the Prompt — and since the Prompt's only
  other distinguishing mark is colour (NFR-71), a Shell user on such a terminal has no reliable
  indication that the Shell is waiting for input. Evidence: `src/Xcaciv.Cupcake.Core/Loop.cs:16`.
  Source files are stored UTF-8 with a byte-order mark and CRLF line endings, so the clone's own
  source encoding must round-trip the character too.

- **NFR-47 — QUIRK — registers Q-51 and Q-52. The shipping profile narrows both the operating system
  and the runtime version, and the narrowed target cannot be satisfied.** The
  development profile targets a current cross-platform runtime and builds an ordinary console program;
  the Release profile targets an **older, Windows-only** runtime, for a single 64-bit architecture, and
  marks the program as windowed rather than console. The shipped artefact therefore runs on a
  different runtime from the one every test exercises, on one operating system, on one architecture —
  and in fact cannot be produced at all, because both in-repository libraries it depends on are built
  only for the current generation and the external Command Service publishes no build for the older
  one (§7.11 FR-11.34, AC-11.18; register Q-52). What is tested is not what ships, and what ships
  does not link.
  Evidence: `src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj:4-5` (development) versus `:10-15,22`
  (shipping). §11 OQ-10. The windowed marking has a separate and larger consequence recorded at
  §7.11 FR-11.32 (register Q-51) and at NFR-74.

- **NFR-48 — QUIRK — register Q-53. The shipping profile enables dead-code elimination, which is
  hostile to this product's central design.** Trimming removes code that is not statically reachable.
  Every Plugin
  Command is reached *only* dynamically — discovered on disk at startup and instantiated reflectively
  — and so is every Host-linked Command. There is no keep-list, no trimming annotation and no note
  anywhere in the repository acknowledging the interaction. INFERRED: Commands can silently vanish
  from a shipped build, and per NFR-57 each such disappearance is invisible, so the symptom is a
  missing Command with no message. Evidence:
  `src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj:19`; §7.11 FR-11.30, AC-11.19; §11 OQ-11.
  A clone that keeps whole-program optimisation must pin every dynamically
  reached type explicitly. The defect is currently **masked** for Plugins, because no Plugin loads
  from disk in any build (NFR-40); it becomes observable the moment the scan mask is fixed.

- **NFR-49 — QUIRK — register Q-54. INFERRED. Single-file bundling additionally breaks Host-linked
  Commands.** Registration
  records the on-disk location of the module that declared the Command, and dispatch re-resolves the
  Command by loading that path; in a single-file bundle that location is empty, which the Command
  Service treats as "no module was defined". The two package Commands are the only Commands the
  shipping program registers this way, so in a shipped build both `PACKAGE SEARCH` and
  `PACKAGE INSTALL` fail with the Output `Error executing PACKAGE (see trace for more info)` rather
  than running. Not executed — this is packaging-platform behaviour, not
  repository-observable. Evidence: `src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj:13`;
  `src/Xcaciv.Cupcake.Lit/Program.cs:10-11` (the two Commands registered this way); OUT-OF-REPO
  (framework v2.1.2): `src/Xcaciv.Command/CommandRegistry.cs:49-53` (the declaring module's location
  recorded), `src/Xcaciv.Command/CommandFactory.cs:58-79` (by-name resolution, then the empty-path
  refusal at `:76-78`). §7.11 FR-11.31, AC-11.20; §11 OQ-12. Unlike NFR-48 this one is **not**
  masked by the scan defect: it applies to the direct registration route, which does run.

- **NFR-50 — Text case handling uses the ambient culture.** Three normalisations upper- or
  lower-case using whatever culture the host machine is configured for: the Command-name parse, the
  Command Group's member lookup, and the Session Variable key. Parameter names are not affected —
  nothing case-folds them. In practice the
  sanitising allow-lists strip non-ASCII letters from Command names before normalisation, so the
  exposure is small — but a clone should normalise with a fixed, culture-invariant rule rather than
  inherit the host's. OUT-OF-REPO evidence only (framework v2.1.2):
  `src/Xcaciv.Command.Interface/CommandDescription.cs:52-64` (Command-name parse);
  `src/Xcaciv.Command/CommandFactory.cs:46,128` (member lookup);
  `src/Xcaciv.Command/EnvironmentContext.cs:74,97` (variable keys). A repository-wide search of the
  framework finds no other culture-sensitive case operation.

- **NFR-51 — INFERRED. Detailed search output is machine-dependent.** The publication timestamp in the
  `detailed` rendering is formatted with the host's default date and time conventions, so the same
  search produces different text on differently configured machines. Evidence:
  `src/Xcaciv.Command.Packages/SearchCommand.cs:73`. A clone should choose an explicit, stable format
  rather than reproduce this; the choice is a §11 matter because it changes observable output.

---

### 9.7 Observability

The honest summary: **when something goes wrong, a clone built to parity can see almost nothing.**
NFR-52 to NFR-58 enumerate the absences; NFR-59 states what is left.

- **NFR-52 — There are no metrics.** No counters, no timers, no histograms, no health check, no
  startup timing, no Command duration, no result counts. Nothing measures anything. Repo-wide: no
  metric construct of any kind exists in the product's sources.

- **NFR-53 — There is no log file and no structured log sink is wired up.** The Package Registry
  Client accepts a log sink on two of its six operations and defaults it to a **discard** sink when
  the caller omits it — which every caller does — and hard-codes the discard sink at
  its two remaining network operations. So the one component that talks to the network
  logs nothing, anywhere, ever, and a Package Registry outage, a feed error and a genuine zero-result
  search are indistinguishable at the terminal. Evidence:
  `src/Xcaciv.Command.Packages/NugetWrapper.cs:23`, `:55` (the two defaulted), `:46`, `:83` (the two
  hard-coded).

- **NFR-54 — The Command Service offers a structured audit hook, and the product never sets it.**
  Every Command execution emits **exactly one** audit record, on success and on failure alike,
  carrying the Command name, the origin of the Command's code (its Plugin's path, or the literal
  `built-in`), the raw argument list, the start time, the duration, the success flag, the error text,
  and the Pipeline stage number and stage total. The hook is a settable property on the Command
  Service, and its default implementation discards everything. The product constructs a stock Command
  Service and never assigns the hook, so **every one of those records is discarded**. This is the
  single largest observability gap that costs nothing to close: a clone that wires one sink gets
  per-Command timing, origin attribution and failure text for free. Evidence:
  `src/Xcaciv.Cupcake.Core/Loop.cs:25` and a repo-wide search finding no audit assignment in the
  product; OUT-OF-REPO (framework v2.1.2): `src/Xcaciv.Command/CommandExecutor.cs:244-255` (record
  shape), `:248` (raw arguments recorded), `src/Xcaciv.Command/CommandController.cs:107` (discarding
  default), `:128-134` (the property never set).

- **NFR-55 — QUIRK — register Q-27. The Diagnostic trace channel is silenced by a shadowed flag.**
  The routing rule
  is: a trace message is painted as Output when the **inherited** Status Visibility flag is set, and
  otherwise goes to the platform debug sink. The Presentation Adapter **re-declares** its own Status
  Visibility flag rather than setting the inherited one; the inherited flag keeps its initial value of
  off and is never assigned anywhere in the product. Net effect: **no Diagnostic trace line ever
  reaches the terminal, however the Shell is configured.** Every message that ends "see trace for more
  info" points at a channel the Shell user cannot see. Evidence:
  `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:31` (the shadowing declaration); OUT-OF-REPO (framework
  v2.1.2): `src/Xcaciv.Command.Core/AbstractTextIo.cs:25` (inherited flag, initial value off),
  `:167-176` (the routing rule that reads it). INFERRED — read, not executed. §11 OQ-21, Q-27.

- **NFR-56 — Status text that is suppressed goes to a sink optimised builds strip.** When Status
  Visibility is off, Status text is written to the debug diagnostic sink rather than the terminal —
  and that sink, unlike the one used for suppressed Diagnostic trace, is removed from optimised
  builds. In the shipping build a suppressed Status line is lost with no record anywhere, and Command
  failure text is carried on the Status channel. Evidence:
  `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:92-96`; §7.3 FR-3.15, §11 Q-32.

- **NFR-57 — Plugin discovery is silent on success *and* on every partial failure.** Nothing reports
  how many Plugins or Commands were loaded, or from where. A Plugin Directory that fails verification
  is discarded silently; an individual Plugin that fails to load — security violation, missing file,
  unreadable or wrong-architecture binary, or any other cause — is swallowed to the Diagnostic trace
  and skipped, and the Session continues. Because of NFR-55 that trace is invisible. **A rejected
  directory, a skipped Plugin, a displaced Command and a completely clean start are indistinguishable
  at the terminal.** Evidence: `src/Xcaciv.Cupcake.Core/Loop.cs:41-50`; OUT-OF-REPO (framework
  v2.1.2): `src/Xcaciv.Command.FileLoader/VerifiedSourceDirectories.cs:82-89` (a directory failing
  the check is dropped with a bare `false`), `:100-115` (the containment test's silent
  refusal path); `src/Xcaciv.Command.FileLoader/Crawler.cs:118` (one catch per Command type),
  `:128-131,136,141,146,151` (five catches per Plugin binary — security violation, missing file,
  load failure, wrong-architecture binary, and a catch-all) — every one of the six writing only to
  Diagnostic trace, and every one followed by a skip. §11 Q-15.
  **Two of these silences are unreachable as shipped and one is not**, and a clone must keep the
  distinction. The per-Plugin skip and the load-count silence sit behind the scan, which raises
  before any candidate is reached (standing caveat above; §7.4 FR-4.25), so today nothing is ever
  skipped because nothing is ever examined. The *directory rejection* silence is fully live, and is
  the one that matters most: on the default POSIX configuration it is the entire first-run
  experience (NFR-44). A clone that fixes the mask activates the other two immediately, which is
  precisely when Q-15 stops being latent.

- **NFR-58 — The most informative artefact of a startup failure is discarded at the process
  boundary.** The Session wraps the underlying cause into a Startup Load Failure carrying a fixed
  summary; the process-level handler prints only the outermost message — the line `Error {message}` —
  and never the wrapped cause. That line is written to **standard output**, not an error stream, so
  redirecting output swallows every diagnosis. The exit-code taxonomy is `0` and `1` and nothing else.
  Evidence: `src/Xcaciv.Cupcake.Core/Loop.cs:53`; `src/Xcaciv.Cupcake.Lit/Program.cs:16,18`. §11 Q-38,
  Q-39.

- **NFR-59 — What a clone at parity can actually see when something goes wrong** is enumerated in
  the table immediately below. Cross-reference §7.6 for the failure contract itself. Every "No" in
  it is a documented absence, not a recommendation to reproduce it; the keep-or-fix decisions are
  §11 Q-15, Q-27, Q-38, Q-39.

| Signal | Available? | Where it comes from |
|---|---|---|
| The Shell failed to start | Yes | one line `Error Unable to load commands.` on standard output, exit code `1` |
| *Why* it failed to start | **No** | the wrapped cause is discarded (NFR-58) |
| A Command failed | Yes | Output `Error executing NAME (see trace for more info)` plus Status line `**Error: <message>` |
| *Why* the Command failed | Partly | the message is on the Status line; the detail is on the trace channel, which is dark (NFR-55) |
| Which Plugin Directory was rejected | **No** | silent (NFR-57) |
| That the scan raised, and on which path | **No** | the inner cause — a directory-not-found condition naming `<root>/*/bin` — is discarded at the process boundary (NFR-58); the Shell user sees only the start-up failure line above |
| Which Plugin failed to load, and why | **No** | swallowed to a dark channel (NFR-57) — and unreachable today, because nothing is ever examined (NFR-40) |
| How many Commands loaded | **No** | never reported (NFR-57) |
| How long anything took | **No** | no metrics; the audit record that carries duration is discarded (NFR-52, NFR-54) |
| What the Shell user typed | **No** | no history, no echo log, no audit sink (NFR-54) |

---

### 9.8 Testability

- **NFR-60 — The Session is testable because every collaborator it touches is a contract.** The
  Session depends on exactly three abstractions — the Interaction Context, the Command Service and the
  Session Variables store — and takes all three as arguments rather than constructing them. The
  existing tests exploit this: they hand-implement the full surface of all three and drive a complete
  Session with no terminal, no Command Service and no filesystem. A clone must preserve these three
  seams; they are the only reason any of §7.5 or §7.6 can be exercised at all. Evidence:
  `src/Xcaciv.Cupcake.Core/Loop.cs:32`, `:70` (all three injected);
  `Xcaciv.Cupcake.Core.Tests/LoopTests.cs:8-122` (the three stand-ins).

- **NFR-61 — There is no seam for the Presentation Adapter, and a clone must add one.** The adapter
  writes to process-global terminal state directly, with no injected output surface and no injected
  input source. The consequence is visible in the product's own test file: the test that is named for
  the read path cannot exercise it at all, says so in a comment before skipping it, then calls the
  two paint methods purely for their side effects and closes with the tautology `Assert.True(true)`.
  Nothing about what was painted is asserted, because nothing can be observed. Evidence:
  `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:53-72,90-103` (global state, no injection);
  `Xcaciv.Cupcake.Core.Tests/ConsoleContextTests.cs:14-23` (the skipped read path, the two
  side-effect calls at `:20-21`, and the tautological assertion at `:22`).

- **NFR-62 — The only adapter test that asserts a computed value pins the wrong arithmetic.** The
  progress test asserts the current formula's output for the input pair `(100, 10)`, at which the
  ratio `total ÷ step` and the intended percentage coincide — they agree for every pair where
  `total = 10 × step`, and the test picked one — which is why the defect at §7.3 is not caught. A
  clone's test suite must choose inputs that discriminate. Evidence:
  `Xcaciv.Cupcake.Core.Tests/ConsoleContextTests.cs:25-31`; §11 OQ-22, Q-29.

- **NFR-63 — Every registry test is a live network integration test.** Fourteen tests live in the two
  package test projects, and **thirteen** of them reach the public registry over the internet — five
  directly and eight through Package Search. The fourteenth exercises the per-chunk invocation path,
  which returns a fixed refusal string and never reaches the registry. There are **no unit tests, no
  fakes, no stubs, no recorded responses and no
  offline fixtures** for this layer anywhere in the repository. Evidence:
  `Xcaciv.Command.PackagesTests/NugetWrapperTests.cs:16-17,56,71,86-88` (the five direct tests);
  `Xcaciv.Command.PackagesTests/SearchCommandTests.cs:9-126` (the eight network tests), `:128-138`
  (the one that is not).

- **NFR-64 — Those tests are non-hermetic in four distinct ways,** and a clone must not reproduce any
  of them: (a) they fail with no network access; (b) they depend on four named third-party identities
  continuing to exist on a public registry — `XCBatch`, `XCBatch.Core`, `XCBatch.Interfaces` and
  `Cake.NuGet` — and on one named package, `Package.Does.Not.Exist`, continuing *not* to exist;
  (c) two depend on that registry's **relevance ordering** staying stable — one needs both
  `XCBatch.Core` and `XCBatch.Interfaces` to appear within the default 20 results, the other needs
  `Cake.NuGet` to appear within the default 20 for the term `cake.nuget` — so they can break with no
  code change at all;
  (d) one writes a real Package Archive into the host's temporary directory under a random name and
  deletes it afterwards. No test asserts an exact output string: every formatting assertion is a
  substring or a not-contains check. Evidence:
  `Xcaciv.Command.PackagesTests/NugetWrapperTests.cs:16,56,71,86-89,101`;
  `Xcaciv.Command.PackagesTests/SearchCommandTests.cs:14,22-23,90,117,125`. §11 OQ-24.

- **NFR-65 — No failure path has coverage.** The stand-in Command Service cannot fail — every one of
  its methods is an empty body or a completed task — so no error
  branch of the Session is exercised; the two visibility flags the failure behaviour depends on are
  not pinned by the presentation tests; and the tests that touch the code where failures are raised
  need live network. Every acceptance criterion in §7.4 and §7.6 concerning a message, a directory
  outcome or a failure branch is derived from source reading, not from an existing test. In
  particular, **no test covers the start-up failure that the shipped product always takes when a
  Plugin Directory exists** (NFR-40; §7.4 AC-4.24). Evidence:
  `Xcaciv.Cupcake.Core.Tests/LoopTests.cs:67-82` (the no-op stand-in Command Service), `:124-162`
  (the three tests, all of which assert only that two properties are non-null or that defaults are
  set); §7.6.

- **NFR-66 — Nothing was executed, and this is established rather than assumed.** The product does not
  build at the pinned commit for two independent reasons. **First**, the Package Search Command's
  result accumulator had its declaration deleted in commit `e1123b2` while five uses of the identifier
  remained; the identifier is unresolved at the pinned commit, and no framework version defines such a
  member. **Second**, dependency restore fails for every project: the feed that claims all first-party
  packages is the unexpanded environment token `%NUGET_LOCAL_PACKAGES%`, the private hosted feed is
  declared but given no routing pattern and is therefore never consulted, and the framework packages
  are absent from the public
  index. Both are established. The **only** residual uncertainty is that the exact pinned framework
  artefacts could not be fetched to confirm the base-class surface at that precise version — which is
  why §11.3 assumption 1 exists. Evidence: `src/Xcaciv.Command.Packages/SearchCommand.cs:66,69,72,81,85`
  (five uses, no declaration); `NuGet.config:6-8` (the three declared feeds), `:12-20` (the routing
  map, which names only two of them); §2.4; §11 OQ-1, OQ-2, OQ-3; register Q-55 for the feed half.

---

### 9.9 Internationalisation and accessibility

Stated plainly: **the product has neither.** These are absences, recorded honestly, not features to
document.

- **NFR-67 — There is no message catalogue.** A repository-wide search returns **zero** resource files.
  Every user-visible string is a literal written inline at its use site, with no lookup, no
  substitution mechanism and no formatting indirection. Evidence: `src/Xcaciv.Cupcake.Core/Loop.cs:16,37,47,53,75,84,87`;
  `src/Xcaciv.Cupcake.Lit/Program.cs:16`; `src/Xcaciv.Command.Packages/InstallCommand.cs:21,26`;
  `src/Xcaciv.Command.Packages/SearchCommand.cs:34,44,90`;
  `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:20`.

- **NFR-68 — All text is in one language, English, including the Command vocabulary itself.** The exit
  vocabulary, the Prompt, every message, and the progress template are all fixed English or symbols.
  The progress template `{0} progress {1}%` additionally fixes its placeholder **order** — context
  name first, number second — which a language with different word order cannot honour without
  changing the template. Evidence: `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:20`;
  `src/Xcaciv.Cupcake.Core/Loop.cs:20`.

- **NFR-69 — There is no locale handling.** No language selection, no locale detection, no fallback
  chain, no pluralisation, no bidirectional-text consideration, and no date or number formatting
  policy — the one formatted timestamp follows the host's configuration by accident rather than by
  choice (NFR-51).

- **NFR-70 — The only non-ASCII character in user-visible text is the Prompt's first character**, and
  it carries a hard terminal requirement (NFR-46).

- **NFR-71 — Colour is the only channel distinction.** Three fixed foreground/background pairs
  separate the three channels — Output `Blue` on `Black`, Status `Yellow` on `DarkBlue`, Prompt
  `Green` on `Black` — and there is **no prefix, symbol, indentation or severity marker** on any of
  them. Command output, Shell status, Command failures and the Prompt are textually identical to one
  another. On a monochrome terminal, in a colour-blind reader's view, or through a screen reader, the
  three channels collapse into one undifferentiated stream. Evidence:
  `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:23-30,53-60,66-72,90-103`.

- **NFR-72 — The one textual channel marker in the entire design is unreachable.** The Diagnostic
  trace channel would prefix its lines with a tab and `TRACE: ` — the only place any channel labels
  itself — and that channel is permanently dark (NFR-55). OUT-OF-REPO (framework v2.1.2):
  `src/Xcaciv.Command.Core/AbstractTextIo.cs:171`.

- **NFR-73 — There is no high-contrast mode, no no-colour mode, and no way to turn colour off.** The
  six colour settings are writable by a Shell Operator constructing the Session, but nothing in the
  shipping program changes them, no Command exposes them, and no environment convention is consulted.
  `Blue` on `Black` is low-contrast on many terminal themes, and the Status pair fixes a non-default
  background for the width of the Status line only, so the two channels also differ in background
  extent. Child Interaction Contexts inherit none of these settings and revert to the defaults.
  Evidence: `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:20,23-30,38-47`; §11 Q-28.

- **NFR-74 — QUIRK — register Q-51. There is no screen-reader consideration anywhere, and the
  shipping profile makes it
  worse.** Beyond the colour-only channel distinction, the shipped build is marked as a windowed
  rather than a console program (NFR-47); INFERRED, a windowed program gets no terminal attached by
  default, so a user launching the shipped artefact from a graphical shell — the launch path a
  screen-reader user is most likely to take — gets no interface at all: the Prompt, the Status lines,
  the Output and even the crash line have nowhere to go, and the cycle spins on empty reads without
  blocking and without exiting. Evidence:
  `src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj:11,22`; §7.11 FR-11.32, AC-11.21; §11 OQ-9.

- **NFR-75 — Terminal styling is left dirty on exit.** The Prompt path sets colours and never resets
  them, so the Shell user's echoed keystrokes render in Prompt colours and the terminal stays
  recoloured after the process ends. It is the one piece of this product's behaviour an operator keeps
  seeing after the Shell is gone. Evidence: `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:66-72` (no
  reset), contrast `:58` and `:101`; §11 Q-31.

- **NFR-76 — A clone shall record its i18n and accessibility position explicitly.** Reproducing
  NFR-67 to NFR-75 verbatim reproduces a product that is unusable through a screen reader and
  untranslatable without editing source. Adding a message catalogue and textual channel markers
  changes observable output and is therefore a §11 decision, not a free improvement — but it is the
  one place in this document where the recommendation is unambiguous.

---

### 9.10 Verification checks

Eleven checks a clone can run to demonstrate parity on the properties above. They are written against
observable behaviour, not against source.

**Two of them cannot be run against the reference implementation, and say so.** NFR-AC-7 and
NFR-AC-8 both require a Plugin to be discovered on disk, which the reference implementation can
never do (standing caveat above; §7.4 FR-4.25). They are stated as the contract a clone must satisfy
once it implements real segment-spanning glob semantics; NFR-AC-11 pins what the reference
implementation actually does instead, so that a clone team can see exactly which behaviour it is
choosing to abandon.

- **NFR-AC-1** — *Given* a Session at the Prompt, *when* a Command line is submitted that takes
  measurable time, *then* the Prompt is not redrawn until that Command line has finished, and no
  second Command line can be submitted in the interval. (NFR-1, NFR-2)

- **NFR-AC-2** — *Given* a Pipeline of two stages that each emit a Status line while running, *when*
  the Pipeline is executed, *then* both stages are observably running at the same time. (NFR-5)

- **NFR-AC-3** — *Given* a Pipeline whose first stage emits more than 10,000 Outputs faster than its
  second stage consumes them, *when* the Pipeline is executed, *then* the first stage blocks and **no
  Output is dropped**; the final Output count equals the emitted count. (NFR-13, NFR-14)

- **NFR-AC-4** — *Given* a Command that never returns, *when* it is dispatched, *then* the Session
  waits indefinitely: no timeout fires, no cancellation is offered, and no message appears. (NFR-19
  through NFR-22)

- **NFR-AC-5** — *Given* a search for a term matching more than 100 Plugins, *when* a Result limit of
  `500` is requested, *then* exactly 100 Package Listings are returned, with **no message** stating
  that the request was clamped. (NFR-27)

- **NFR-AC-6** — *Given* a search whose term is 250 characters long, *when* it is submitted, *then*
  the query is performed on its first 200 characters and no message reports the truncation. (NFR-31)

- **NFR-AC-7** — *Given* a clone with real segment-spanning glob semantics and a Plugin Directory
  containing 50 Plugin binaries, and another containing 51,
  *when* each is scanned, *then* both produce identical registration results — the threshold changes
  scheduling only, never outcome. **Not runnable against the reference implementation**, which raises
  before counting either set. (NFR-33)

- **NFR-AC-8** — *Given* a clone with real segment-spanning glob semantics and a Plugin that throws
  during loading, *when* the Session starts, *then* the
  Session starts normally, the Plugin's Commands are absent, and **nothing is printed** distinguishing
  this from a clean start. **Not runnable against the reference implementation**, which never reaches
  the Plugin at all. (NFR-57)

- **NFR-AC-9** — *Given* any Command that emits a Diagnostic trace message, *when* it runs with Status
  Visibility on and again with it off, *then* in **neither** case does the trace text appear at the
  terminal. (NFR-55)

- **NFR-AC-10** — *Given* the Shell is started and then exited normally, *when* the operator types at
  their terminal afterwards, *then* the terminal is still painted in the Prompt's colours. (NFR-75)

- **NFR-AC-11** — *Given* a Plugin Directory that exists and passes the Verified Directory rule,
  **and given** it holds a correctly laid-out Plugin at `<root>/Hello/bin/Hello.dll`, *when* the
  Session starts, *then* no Prompt is drawn: the process prints exactly `Error Unable to load
  commands.` on standard output and exits with status `1`; *and given* the same directory emptied of
  every file, *then* the outcome is identical, because the scan raises before it can distinguish the
  two; *and given* the directory removed altogether, *then* the Session starts normally and prints
  the No Plugins Available guidance. This criterion records the reference implementation's actual
  behaviour and duplicates §7.4 AC-4.24 deliberately, because it is the fact that invalidates
  NFR-AC-7 and NFR-AC-8 as parity checks. A clone with real glob semantics **fails** it, and that is
  the recommended outcome — recorded as a deliberate deviation rather than allowed to pass silently.
  (NFR-40, NFR-44, NFR-57)

---

### 9.11 The number register

Every number this product's non-functional behaviour depends on, in one place.

| Number | What it means | Where it lives | Classification |
|---|---|---|---|
| `10,000` | Stage Channel capacity, in Output items, between two Pipeline stages | Command Service default; product never sets it | **[STACK DEFAULT]** |
| Block | Full-buffer policy: the producer waits rather than dropping | Command Service default; alternatives drop-oldest / drop-newest exist | **[STACK DEFAULT]** |
| `0` | Whole-Pipeline timeout: none | Command Service default; product never sets it | **[STACK DEFAULT]** |
| `0` | Per-stage timeout: none | Command Service default; product never sets it | **[STACK DEFAULT]** |
| `0` | Per-stage Output byte cap: unlimited | Command Service default; product never sets it | **[STACK DEFAULT]** |
| `0` | Per-stage Output item cap: unlimited | Command Service default; product never sets it | **[STACK DEFAULT]** |
| `1` | Lower clamp bound on the search Result limit | `src/Xcaciv.Command.Packages/SearchCommand.cs:41,46` | **[BUSINESS RULE]** — the comment at `:41` states abuse prevention |
| `100` | Upper clamp bound on the search Result limit; also the absolute ceiling per term, since offset is fixed at `0` | `src/Xcaciv.Command.Packages/SearchCommand.cs:41,46`; `src/Xcaciv.Command.Packages/NugetWrapper.cs:29` | **[BUSINESS RULE]** — the comment at `:41` states abuse prevention |
| `20` | Declared default Result limit when the parameter is omitted | `src/Xcaciv.Command.Packages/SearchCommand.cs:15`; second, independent copy at `src/Xcaciv.Command.Packages/NugetWrapper.cs:20` | **[BUSINESS RULE]** |
| `0` | Search result offset — fixed, no pagination | `src/Xcaciv.Command.Packages/NugetWrapper.cs:29` | **[BUSINESS RULE]** |
| `200` | Search-term truncation length, in characters, applied silently | `src/Xcaciv.Command.Packages/SearchCommand.cs:55-58` | **[UNCLASSIFIED LITERAL]** — no stated rationale; abuse-bound reading is INFERRED |
| `50` | Plugin-scan parallelism threshold; **strictly greater than** switches to parallel. Never reached as shipped (NFR-33) | Command Service default, a mutable process-global; product never pins it | **[STACK DEFAULT]** — comment states overhead avoidance |
| `30 minutes` | Package Registry response cache lifetime, on disk, per user | Registry library default; product opens four cache scopes and configures none | **[STACK DEFAULT]**, value INFERRED |
| `3` | Prompt length in characters: U+0190, `>`, one space | `src/Xcaciv.Cupcake.Core/Loop.cs:16` | **[BUSINESS RULE]** |
| `3` | Exit vocabulary size: `END`, `EXIT`, `BYEE` | `src/Xcaciv.Cupcake.Core/Loop.cs:20` | **[BUSINESS RULE]** |
| `3` | Colour pairs, one per channel — and the total number of channel distinctions available | `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:23-30` | **[BUSINESS RULE]** |
| `4` / `3` | Terminal operations per painted Output line / per Prompt | `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:53-60`, `:66-72` | consequence, not a setting |
| `0` / `1` | The entire exit-code taxonomy | `src/Xcaciv.Cupcake.Lit/Program.cs:18` | **[BUSINESS RULE]** |
| `1` | Times Plugin discovery runs per Session | `src/Xcaciv.Cupcake.Core/Loop.cs:41-43` | **[BUSINESS RULE]** |
| `14` / `13` | Tests in the two package test projects / how many of them are live network integration tests | `Xcaciv.Command.PackagesTests/NugetWrapperTests.cs`, `Xcaciv.Command.PackagesTests/SearchCommandTests.cs` | observation |

---

### 9.12 External technology

*This paragraph is the only place in this section where implementation names appear.* The product is
C# on .NET, and the two build profiles differ in framework: `net8.0` for development, `net6.0-windows`
for Release, with `RuntimeIdentifier win-x64`, `OutputType WinExe`, `PublishSingleFile`,
`SelfContained`, `PublishReadyToRun`, `EnableCompressionInSingleFile`, `PublishTrimmed` and
`NoWin32Manifest`. Stage Channels are `System.Threading.Channels.Channel<string>` created with
`BoundedChannelOptions`, whose `FullMode` maps from `PipelineBackpressureMode` to
`BoundedChannelFullMode.Wait`; the resource defaults live in `Xcaciv.Command.Interface.PipelineConfiguration`
and are reachable as `CommandController.PipelineConfig`. Cancellation is `CancellationToken`, and the
never-cancelled value is `CancellationToken.None`. The audit hook is `IAuditLogger`, whose default is
`NoOpAuditLogger` and whose wired alternative is `StructuredAuditLogger`; the record is `AuditEvent`.
Registry access is the NuGet client libraries: `SourceRepository`, `SourceCacheContext` (whose
`MaxAge` default supplies the 30-minute figure of NFR-34), `PackageSearchResource.SearchAsync`, and
`NuGet.Common.NullLogger.Instance` as the discard log sink. Terminal painting is `System.Console`
foreground/background properties plus `ResetColor`, and suppressed status goes to
`System.Diagnostics.Debug.WriteLine` while suppressed trace goes to `System.Diagnostics.Trace.WriteLine`
— the asymmetry behind NFR-56. The parallel scan is `Parallel.ForEach` over discovered `*.dll` paths
with `ParallelizeAt` as the threshold; the paths themselves come from
`IDirectory.GetFiles(basePath, searchMask, SearchOption.AllDirectories)` where `searchMask` is
`Path.Combine("*", subDirectory, "*.dll")` — and that combination is the defect of NFR-40, because
.NET splits a search mask at its last separator and joins the directory portion to the root
literally, raising `DirectoryNotFoundException` for `<root>/*/bin` rather than expanding the
wildcard. Session Variable write-back is gated on `ICommandDescription.ModifiesEnvironment` together
with `IEnvironmentContext.HasChanged`, and the child scope is an `EnvironmentContext` seeded from
`GetEnvironment()` (NFR-11). Tests are xUnit. Packages resolve through `NuGet.config` with
package-source mapping enabled — `packageSourceMapping` names `nuget.org` and `local` but not
`github`, which is why the private feed is never consulted (NFR-66).

---

### 9.13 Source notes

1. **The classification tags are the load-bearing content of this section.** Eight of the twenty
   numbers in §9.11 are **[STACK DEFAULT]** — they exist because nobody configured anything. A clone
   team that reads them as product decisions will hard-code a competitor's defaults; a team that
   ignores them will ship different behaviour. Both failures are avoidable only by making the decision
   explicit, which is what §9.11 is for.

2. **Nothing here was measured, and the section deliberately contains no performance targets.** The
   product does not build (NFR-66), so there is no baseline to target. Any latency or throughput
   budget in the clone's own plan is a new requirement, not a reverse-engineered one, and should be
   labelled as such. **One exception, and it is the reason note 7 exists:** the Plugin Scanner's
   search-mask enumeration was executed directly against a real filesystem, independently of the
   product, and it raises rather than matching (standing caveat). That is an executed observation of
   a primitive, not a measurement of the product, and nothing else in this section was run.

3. **The thread-safety finding is a contract violation, not a style complaint.** The obligation is
   published to Plugin authors in the Interaction Context contract (NFR-6). A clone that reproduces
   the unsynchronised adapter is shipping an implementation that fails its own published contract, and
   should say so in its Plugin-author documentation rather than leave authors to discover it. The
   narrowing at NFR-8 — that Output is diverted into Stage Channels, so only Status, progress and
   failure text can actually interleave — is the difference between "the terminal is a mess" and "a
   Plugin reporting progress from two stages can tear a line". Both readings appear in the source
   dossiers; NFR-8 is the accurate one.

4. **NFR-16 (register Q-64) is the section's one genuinely new inference and rests on OUT-OF-REPO
   evidence only.** The
   final-stage buffer deadlock follows from three facts that are individually documented in the
   framework and never combined anywhere: stages are awaited before the final channel is drained, the
   channel is bounded, and the full-buffer policy blocks. It is unreachable with the product's own
   Commands and fully reachable by a Plugin. It was not executed. If the clone team verifies exactly
   one inference from this section, make it this one.

5. **Observability is the cheapest thing on this list to fix and the most expensive to leave.** The
   audit hook of NFR-54 already emits a complete record per execution — name, origin, arguments,
   duration, outcome, error, Pipeline position — and the product discards every one of them by
   omission. Wiring a single sink converts §9.7's table of "No" into a table of "Yes" without changing
   one line of observable Shell behaviour. It is recorded here rather than in §11 because it requires
   no keep-or-fix decision: nothing observable changes either way.

6. **Two numbers deserve suspicion rather than reproduction.** The `200`-character truncation
   (NFR-31) has no stated rationale anywhere and is silent, so a user pasting a long query gets
   results for a query they did not type. The `30`-minute cache lifetime (NFR-34) is inherited from a
   library the clone will not be using, and it governs how stale a Package Listing may be — a
   user-visible property that no one in this product ever chose.

7. **A whole class of this section's numbers is latent rather than live, and the distinction is not
   cosmetic.** The Plugin scan cannot succeed on a real filesystem (standing caveat; §7.4 FR-4.25,
   FR-4.42), so the parallelism threshold of NFR-33, the per-Plugin silences of NFR-57, the
   discovery-once rule of NFR-41 and the two scanning parity checks NFR-AC-7 and NFR-AC-8 describe
   machinery that never runs as shipped. They are documented in full because a clone that implements
   real segment-spanning glob semantics — the recommendation — inherits every one of them on the
   first day the scan works, and because the trimming quirk of NFR-48 is masked by exactly the same
   defect. A clone team that reads the mask fix as a small correctness patch will be surprised by
   how much latent behaviour it switches on. The one scanning-related property that *is* live today
   is the directory-rejection silence of NFR-57, and on the default POSIX configuration it is the
   entire first-run experience (NFR-44).

8. **The Session Variable write-back scope was corrected during verification, and the correction
   narrows a quirk rather than removing it.** Reading a missing key does write the default back
   (NFR-11), but on the Shell's dispatch path that write lands in a per-execution child scope that is
   discarded unless the Command's registration record declares it environment-modifying — which the
   shipping host never does. The effect is therefore visible only on the direct-invocation path this
   repository's own tests use. The register's Q-10 remains a real finding; a clone must simply not
   expect to see the empty key in a variable dump, and must not "fix" a symptom the Shell does not
   exhibit while leaving the read-mutates-store behaviour in place for any embedder that does
   register the Command as environment-modifying.
