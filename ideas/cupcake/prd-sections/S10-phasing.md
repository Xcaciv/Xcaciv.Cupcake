## 10. Suggested Delivery Phasing

Dependency-driven build order for the clone, derived from the feature dependency map (§5.2). Each
phase ends at a demonstrable state, so progress is observable rather than inferred from ticket
counts. Effort shapes are relative, not estimates.

### Phase 0 — Decide the quirks before writing code

**Deliverable:** a signed-off disposition for every entry in the §11.2 quirk register, and answers
to at least OQ-4, OQ-5, OQ-7, OQ-9, OQ-10, OQ-11, OQ-12, OQ-14 and OQ-17.

Four of those — OQ-9, OQ-10, OQ-11, OQ-12 — are the shipping-profile questions, and they are gated
here deliberately: as configured, the shipped artefact is a windowed binary with no terminal, built
for an older OS-narrowed runtime, trimmed so dynamically-reached Plugin code can vanish, and bundled
single-file in a way that INFERRED breaks both Commands the host registers. Any one of those makes
the delivered product non-functional, and none of them is visible from the source's own tests.

This phase exists because roughly a quarter of this document's requirements encode behaviour that
looks like a defect. Deciding *afterwards* means re-testing: several quirks (the ineffective
prerelease flag, the silently-downgrading detail level, the fatal empty Plugin Directory) sit
directly on acceptance criteria. Deciding first costs a meeting; deciding late costs a rewrite of
the test suite. Do not start Phase 1 without it.

### Phase 1 — The command capability (§7.1)

**Deliverable:** a library that registers Commands, resolves a typed line to one, binds its
arguments from declarative metadata, runs a pipeline of stages, generates help, and reports an
unknown command — driven by a test harness with no terminal and no plugins.

The largest and least negotiable phase: it is the substrate everything else stands on, and it is the
one thing a clone cannot obtain off the shelf (§5.3 ET-1). Build it against §7.1 and the
product-wide semantics in §6 — in particular the tokenizer's character allow-list, the binding
order, and the unconditional presence of flag keys, all of which produce user-visible behaviour
elsewhere.

**Exit criteria:** §7.1 acceptance criteria pass; a Command declared purely by metadata can be
registered, found, helped and run; a two-stage pipeline moves data between stages.

### Phase 2 — Configuration surface and terminal face (§7.2, §7.3)

**Deliverable:** a Presentation Adapter that renders the four channels with the specified colour
treatment and reads a line, plus the complete settings census with its defaults.

Build these together: the settings are mostly presentation settings, and the adapter is the only
component that reads them. Build the adapter behind the contract from Phase 1 so the Session can
later be tested without a terminal — and, unlike the source, **put a seam in front of the terminal**
(§9), because its absence is why almost nothing about rendering is testable in the original.

**Exit criteria:** §7.2 and §7.3 acceptance criteria pass, including the quirk-pinning ones.

### Phase 3 — Plugin discovery (§7.4)

**Deliverable:** startup scanning of a Plugin Directory, per-Plugin sandboxed loading, silent
skipping of a Plugin that fails to load, and **separately distinguishable empty-state signals**,
because the Session in Phase 4 must be able to tolerate some and not others.

**This is the phase with no working reference.** §7.4 documents, and verification confirmed by
execution, that the source's scan **cannot succeed on a real filesystem**: its search mask puts a
wildcard in the directory portion, which is joined literally rather than expanded, so the enumeration
raises a directory-not-found condition naming `<root>/*/bin` even when a correct layout is present.
No Plugin has ever loaded from disk, and any existing Plugin Directory is fatal at startup. A clone
therefore cannot port this scan — it must **design** the discovery rule, and in doing so must settle
the Plugin packaging contract (what layout a Plugin author must produce) that the source never
successfully established. Treat §7.4's requirements as the *intended* rule and its acceptance
criteria as the *observed* failure; the two are deliberately different, and §11.1 OQ-8 records what
could not be determined about the intent.

**Exit criteria:** a Command bundle dropped into the Plugin Directory becomes invocable without
recompiling — the thing the source cannot do; a corrupt bundle is skipped with a retrievable record
and does not abort startup; the empty-state signals are distinguishable by the caller; and the
packaging contract is written down for Plugin authors.

### Phase 4 — The Session and the failure contract (§7.5, §7.6)

**Deliverable:** a running interactive shell. Prompt, dispatch, blank-line handling, exit
vocabulary, the startup sequence with its tolerant and fatal paths, and the process-level crash
contract.

This is the phase where the product becomes demonstrable end to end. Build the two session entry
points together, because their divergence is a documented requirement, not an accident of
implementation (§7.5) — or make the deliberate decision to have only one and record it.

**Exit criteria:** a user can start the shell, run a built-in Command, and leave. §7.5 and §7.6
acceptance criteria pass, including the first-run experience for all three states of the Plugin
Directory: absent, present-and-empty, present-and-populated.

### Phase 5 — Registry client and the package commands (§7.7, §7.8, §7.9)

**Deliverable:** Package Search working end to end against a real registry, and Package Install in
whatever state Phase 0 decided.

Build the registry client first and behind a test double (§9) — the source's own tests are live
network integration tests, which is why none of them can run offline and why the whole test project
fails together. Then the search Command, which is the richest single feature in the product and
carries the most quirk-pinning acceptance criteria.

**Package Install is the one real product decision in this phase.** As specified it is inert (§7.9)
and the routine behind it is half-built: it downloads, reads identity from the archive, creates a
versioned directory, and then neither extracts nor resolves dependencies — and the directory shape
it creates does not match the layout the Plugin scanner looks for. Finishing it is a genuine feature
build, not a port. Phase 0 must have said which it is.

**Exit criteria:** §7.7 and §7.8 acceptance criteria pass against a registry double and, separately,
against a live registry; §7.9 criteria pass for whichever disposition was chosen.

### Phase 6 — Security posture (§7.10)

**Deliverable:** an explicit decision and implementation for every control and every gap in §7.10.

Not a phase of new features — a phase of deciding what the product's stance is. The source has five
runtime input gates and essentially no supply-chain verification: nothing checks a signature, a
checksum or a publisher before code is loaded and executed, and the settings that once existed for
exactly that were removed as unused (§2.2 NG-7). A shell whose headline feature is fetching and
executing third-party code needs that stance stated deliberately.

**Do not defer this phase.** Retrofitting provenance verification after Plugin loading ships means
changing the Plugin format and breaking every bundle already published.

### Phase 7 — Packaging and delivery (§7.11)

**Deliverable:** a shippable artefact, from a clean checkout, with no machine-local prerequisite.

Carry the source's genuine goals — self-contained, single file, fast start (§2.1 G-7) — and drop the
three settings §11 flags as probable defects: the windowed subsystem marking, the older and
OS-narrowed runtime target, and dead-code elimination on a host that reaches its plugins only
dynamically.

**Exit criteria:** a fresh clone builds and produces a runnable artefact with one documented
command; §7.11 acceptance criteria pass; §2.4's build-state goal is met.

### Sequencing notes

- **Phases 1–4 are a hard chain.** Each genuinely needs the last.
- **Phase 5's registry client (§7.7) can start in parallel with Phase 2**, since it depends on
  nothing internal. It is the natural second work-stream if more than one person is available.
- **Phase 6 is scheduled late but decided early.** Its Phase 0 decisions constrain Phases 3 and 5.
- **A minimum demonstrable product ends at Phase 4**: a working extensible shell with built-in
  Commands and disk-loaded Plugins, but no in-shell discovery of new ones. That is a coherent
  release. Phase 5 is what makes the product's central bet — extensibility the user can act on from
  inside the shell — actually pay off.
