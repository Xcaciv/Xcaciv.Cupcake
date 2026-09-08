## 12. Appendix: Traceability

### 12.1 Feature → source evidence → dossier

Every feature subsection traces to the source files it was derived from and to the working dossier
that produced it. Dossiers are intermediate artefacts retained for audit; they are not part of the
deliverable.

| § | Feature | Primary source evidence (repo-relative) | Dossier |
|---|---|---|---|
| 7.1 | Command Extensibility Contract | `Xcaciv.Cupcake.Core.Tests/LoopTests.cs:8-122` (hand-written implementations of all three contracts — the in-repo record of the required surface); `src/Xcaciv.Command.Packages/SearchCommand.cs:11-20,88`; `src/Xcaciv.Command.Packages/InstallCommand.cs:14-26`; `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:13,38-47`. Plus OUT-OF-REPO framework `src/Xcaciv.Command.Interface/`, `src/Xcaciv.Command.Core/`, `src/Xcaciv.Command/` | `dossiers/F10-extensibility-contract.md` |
| 7.2 | Configuration & Settings | `src/Xcaciv.Cupcake.Core/Loop.cs:11-26`; `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:20-31`; `src/Xcaciv.Command.Packages/SearchCommand.cs:24-29`; `NuGet.config`; `Directory.Packages.props`; `src/Directory.Packages.props` | `dossiers/F8-configuration.md` |
| 7.3 | Console Presentation & Interaction | `src/Xcaciv.Cupcake.Core/ConsoleContext.cs` (whole file); `Xcaciv.Cupcake.Core.Tests/ConsoleContextTests.cs`. Plus OUT-OF-REPO framework `src/Xcaciv.Command.Core/AbstractTextIo.cs` | `dossiers/F2-console-presentation.md` |
| 7.4 | Plugin Discovery & Command Registration | `src/Xcaciv.Cupcake.Core/Loop.cs:37-54,77-85,105-112`; `src/Xcaciv.Cupcake.Lit/Program.cs:9-12`; `Xcaciv.Cupcake.Core.Tests/LoopTests.cs:67-82`. Plus OUT-OF-REPO framework `Crawler.cs`, `CommandLoader.cs`, `VerifiedSourceDirectories.cs`, `CommandRegistry.cs` | `dossiers/F3-plugin-discovery.md` |
| 7.5 | Interactive Shell Session | `src/Xcaciv.Cupcake.Core/Loop.cs` (whole file); `Xcaciv.Cupcake.Core.Tests/LoopTests.cs:124-163`; `src/Xcaciv.Cupcake.Lit/Program.cs` | `dossiers/F1-interactive-shell-session.md` |
| 7.6 | Error Handling & Failure Reporting | `src/Xcaciv.Cupcake.Core/Exceptions/LoadingException.cs`; `src/Xcaciv.Cupcake.Core/Loop.cs:39-54,77-85`; `src/Xcaciv.Cupcake.Lit/Program.cs:6-19`. Plus OUT-OF-REPO framework `CommandExecutor.cs`, the four exception types | `dossiers/F9-error-handling.md` |
| 7.7 | Package Registry Client | `src/Xcaciv.Command.Packages/NugetWrapper.cs` (whole file); `Xcaciv.Command.PackagesTests/NugetWrapperTests.cs` (whole file) | `dossiers/F6-registry-client.md` |
| 7.8 | Package Search Command | `src/Xcaciv.Command.Packages/SearchCommand.cs` (whole file); `Xcaciv.Command.PackagesTests/SearchCommandTests.cs` (whole file) | `dossiers/F4-package-search.md` |
| 7.9 | Package Install Command | `src/Xcaciv.Command.Packages/InstallCommand.cs` (whole file); `src/Xcaciv.Command.Packages/NugetWrapper.cs:103-128`; `src/Xcaciv.Cupcake.Core/Loop.cs:11` | `dossiers/F5-package-install.md` |
| 7.10 | Input Validation & Supply-Chain Safety | `src/Xcaciv.Command.Packages/SearchCommand.cs:31-58`; `src/Xcaciv.Command.Packages/NugetWrapper.cs:81-128`; `NuGet.config`; `src/Xcaciv.Cupcake.Core/Loop.cs:24,42`; commits `b0ca736`, `907c535` | `dossiers/F11-input-validation-safety.md` |
| 7.11 | Shell Distribution & Entry Points | `src/Xcaciv.Cupcake.Lit/*`; `src/Xcaciv.Cupcake/*`; `Xcaciv.Cupcake.sln`; `src/Xcaciv.Cupcake.Core/Xcaciv.Cupcake.Core.csproj` | `dossiers/F7-distribution.md` |

### 12.2 Source file → owning feature

Complete coverage check over the 27 tracked files. Every one is accounted for.

| Tracked file | Lines | Owning § |
|---|---|---|
| `src/Xcaciv.Cupcake.Core/Loop.cs` | 113 | 7.5 (with 7.2, 7.4, 7.6) |
| `src/Xcaciv.Cupcake.Core/ConsoleContext.cs` | 105 | 7.3 (with 7.2) |
| `src/Xcaciv.Cupcake.Core/Exceptions/LoadingException.cs` | 13 | 7.6 |
| `src/Xcaciv.Cupcake.Core/Xcaciv.Cupcake.Core.csproj` | 17 | 7.11 |
| `src/Xcaciv.Command.Packages/NugetWrapper.cs` | 132 | 7.7 (with 7.9, 7.10) |
| `src/Xcaciv.Command.Packages/SearchCommand.cs` | 94 | 7.8 (with 7.10) |
| `src/Xcaciv.Command.Packages/InstallCommand.cs` | 29 | 7.9 |
| `src/Xcaciv.Command.Packages/Xcaciv.Command.Packages.csproj` | 14 | 7.11 |
| `src/Xcaciv.Command.Packages/README.md` | 3 | 7.9 (context only) |
| `src/Xcaciv.Cupcake.Lit/Program.cs` | 21 | 7.11 (with 7.4, 7.6) |
| `src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj` | 34 | 7.11 |
| `src/Xcaciv.Cupcake.Lit/README.md` | 3 | 7.11 (context only) |
| `src/Xcaciv.Cupcake/Program.cs` | 2 | §2.3 X-1 — excluded as vestigial |
| `src/Xcaciv.Cupcake/Xcaciv.Cupcake.csproj` | 14 | §2.3 X-1 — excluded as vestigial |
| `Xcaciv.Cupcake.Core.Tests/LoopTests.cs` | 164 | evidence for 7.1, 7.5 |
| `Xcaciv.Cupcake.Core.Tests/ConsoleContextTests.cs` | 33 | evidence for 7.3 |
| `Xcaciv.Cupcake.Core.Tests/Xcaciv.Cupcake.Core.Tests.csproj` | 30 | §2.3 X-5 |
| `Xcaciv.Command.PackagesTests/SearchCommandTests.cs` | 139 | evidence for 7.8 |
| `Xcaciv.Command.PackagesTests/NugetWrapperTests.cs` | 104 | evidence for 7.7 |
| `Xcaciv.Command.PackagesTests/Xcaciv.Command.PackagesTests.csproj` | 34 | §2.3 X-5 |
| `Directory.Packages.props` | 19 | 7.2 (duplicate — §2.3 X-2) |
| `src/Directory.Packages.props` | 18 | 7.2 (duplicate — §2.3 X-2) |
| `NuGet.config` | 21 | 7.2, 7.10 |
| `Xcaciv.Cupcake.sln` | — | 7.11 |
| `README.md` | 2 | §1 |
| `LICENSE` | — | §1 header |
| `.gitignore` | — | not a requirement source |

> **Note on the duplicated version manifest (§2.3 X-2).** The two copies are identical in *content*
> but not byte-identical: the root copy is 19 lines, LF-terminated, 794 bytes; the `src/` copy is 18
> lines, CRLF-terminated, with no trailing newline, 811 bytes. Verified with `git show` at the pinned
> commit. Describe them as "identical line for line", never as "byte-identical".

### 12.3 Out-of-repo reference

| Property | Value |
|---|---|
| What | The external command framework the product depends on, read to specify §7.1 as a required capability |
| Repository | `https://github.com/Xcaciv/Xcaciv.Command` (public) |
| Tag read | `v2.1.2` @ commit `f34dedca8dc6d690290b2139acbd3e9b8264349c` |
| Versions the product pins | `Xcaciv.Command` 2.1.1, `Xcaciv.Command.Core` 2.1.0, `Xcaciv.Command.Interface` 2.1.0 |
| Why a different version | The pinned artefacts are unobtainable: absent from the public index (HTTP 404) and unreachable through the repository's own feed routing (§2.4). `v2.1.2` is the closest published tag. |
| Version risk | See OQ-3. The repository's own test doubles show the pinned contract differed in several members. |
| Files most relied on | `Xcaciv.Command.Interface/` (contracts, attributes, exceptions, `CommandDescription`, `CommandSyntax`, `PipelineConfiguration`); `Xcaciv.Command.Core/` (`AbstractCommand`, `AbstractTextIo`, `CommandParameters`); `Xcaciv.Command/` (`CommandController`, `CommandExecutor`, `CommandFactory`, `CommandRegistry`, `CommandLoader`, `PipelineExecutor`, `PipelineParser`, `EnvironmentContext`, `HelpService`, `Commands/`); `Xcaciv.Command.FileLoader/` (`Crawler`, `VerifiedSourceDirectories`) |

### 12.4 How this document was produced

| Phase | What was done |
|---|---|
| Scope | Whole product, extensive depth. Two commissioning decisions were taken by the requester: document the command framework at **contract depth** as a required capability rather than as an opaque dependency; and stay target-agnostic while documenting the current stack as annotations. |
| Reconnaissance | Full survey of the tracked tree (27 files, 949 lines of source), manifests, entry points, tests, and commit churn. Commit pinned; licence recorded; dependency restore attempted and its failure recorded as evidence. |
| Fan-out | Eleven parallel analysis agents, one per inventory feature, each writing a behaviour dossier to `workspace/dossiers/`. Each dossier was then re-read by an independent critic agent that re-verified every quoted literal character-by-character against source, hunted fabrication and unmarked inference, and repaired the dossier in place. All twenty-two completed; every critic returned "repaired". |
| Synthesis | Cross-dossier reconciliation: terminology unified against `workspace/glossary.md`; conflicts resolved (notably the containment-boundary rule and the detail-level fallback, where two dossiers disagreed and the evidence settled it); external-technology tables merged and deduplicated; cross-cutting rules promoted to §6. Orchestrator-verified findings in `workspace/orchestrator-notes.md` took precedence over dossier claims. |
| Drafting | Fifteen parallel drafting agents produced §3, §6, §7.1–§7.11, §8 and §9 from the reconciled dossiers. §1, §2, §4, §5, §10, §11 and §12 were written by the orchestrator. |
| Verification | Independent verification pass against the five checks in §12.5. |

### 12.5 Verification performed

Two adversarial rounds ran against the assembled document, followed by a repair round.

**Round 1 — audit and trace (9 agents, 641 checks).** Five audits covered coverage and structural
integrity, open-question reconciliation, stories and criteria, the quirk register, and cross-section
factual consistency. Four adversarial evidence traces re-checked cited claims against the source at
the pinned commit — instructed to *refute*, not confirm, and to read through `git show` so
working-tree line-ending churn could not mislead them. Result: **87 findings** — 7 critical, 27
major, 53 minor. Three audits returned a failing verdict.

**Round 2 — repair (15 agents, 114 fixes applied).** Each section's findings were routed back to a
repair agent that verified every finding against source before applying it. Eight findings were
**rejected with evidence** — six because a proposed quirk identifier collided with another finding's
allocation, and two because the finding named the wrong section. No section failed.

**The five checks this skill requires, and their outcomes:**

1. **Coverage** — every inventory feature appears in §7, or in §2.3 with a reason; §12.2 accounts for
   all 27 tracked files. Three line counts in that table were wrong and were corrected.
2. **Open questions** — four dossier questions had been dropped and are now OQ-26 to OQ-29.
3. **Evidence spot-check** — 641 checks, biased toward quoted literals and quirk claims. Citations
   pointing past end-of-file, wrong line ranges and misattributed evidence were corrected.
4. **Implementation leakage** — a section-aware scan finds source-stack names only in
   External-technology entries, *Source used* annotations, *Source notes*, §4's code-name column and
   this appendix. Evidence paths are exempt: requirement traceability depends on them.
5. **Stories and criteria** — 125 user stories, 339 acceptance criteria across 11 subsections;
   eleven stories missing their benefit clause were completed, one use case missing its
   alternate-flow heading was fixed, and criteria that merely restated a requirement were rewritten
   as executable statements.

**Two substantive corrections, both of which changed conclusions rather than wording:**

- **The Plugin scan cannot succeed on a real filesystem.** Established by executing the scan mask
  against a real directory tree containing a correct plugin layout: it raises a directory-not-found
  condition naming `<root>/*/bin` rather than matching or returning empty, because a wildcard in a
  mask's directory portion is never expanded. A control run with a plain pattern found the same
  files. The framework's own scanner tests pass because the one test using a real filesystem passes
  an empty sub-directory filter, degrading the mask to a working form; the rest run against a mock.
  This supersedes the earlier, milder reading that only an *empty* Plugin Directory was fatal, and
  it is recorded as Q-82, Q-83 and OQ-7.
- **The Session-Variable read side effect is scoped, not permanent.** An earlier analysis note held
  that the first Package Search permanently adds an empty entry visible in every later variable
  dump. It does not: the write lands in the per-execution child scope, which is discarded because
  the Command is not registered as environment-modifying. It is observable only on the
  direct-invocation path — which is what the repository's own tests use, and why the wrong reading
  looked convincing. The error had propagated into three acceptance criteria before the adversarial
  pass caught it; one feature subsection had independently derived the correct version and flagged
  the contradiction rather than conforming to the note. Corrected throughout; recorded as Q-10.

**Residual known limitations.** Nothing in §7 was observed by running the product, which cannot be
built (§2.4). Three isolable mechanisms *were* executed on their own — the scan mask, the progress
arithmetic and the path-containment primitive — and say so at the point of claim. Every
`OUT-OF-REPO:` requirement carries the version gap of OQ-3.

### 12.6 Working artefacts

Retained under `workspace/` for audit; not part of the deliverable.

| Artefact | Purpose |
|---|---|
| `inventory.md` | Phase 1 feature inventory, conventions note, churn analysis, and the issues found at recon |
| `dossiers/*.md` | Eleven per-feature behaviour dossiers (5,275 lines), each critic-verified and repaired |
| `glossary.md` | The code-name → product-term mapping that §4 is built from |
| `orchestrator-notes.md` | First-hand orchestrator findings: verified literals, the framework contract, the build-state investigation, and the quirk register |
| `prd-sections/*.md` | Per-section drafts assembled into the deliverable |
