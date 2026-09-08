## 2. Goals & Non-Goals

### 2.1 Product goals (derived from what the code actually pursues)

| # | Goal | Evidence that the source pursues it |
|---|---|---|
| G-1 | Give a user an interactive terminal session that reads a line, runs it, shows the result, and repeats until told to stop. | `src/Xcaciv.Cupcake.Core/Loop.cs:32-68` |
| G-2 | Own no command vocabulary of its own — delegate every parse, dispatch and render decision to a replaceable command capability, so the shell is a host rather than an application. | The Session touches only three operations of the presentation contract and five of the command contract; it performs no parsing, casing, trimming or history of its own (`Loop.cs:57-66`). |
| G-3 | Let the running shell be extended from disk without recompiling it: discover Command bundles at startup and register everything they offer. | `Loop.cs:42-43`; scan convention OUT-OF-REPO: `src/Xcaciv.Command.FileLoader/Crawler.cs:20,171-190` |
| G-4 | Let a user discover installable extensions from inside the shell, against a configurable package registry. | `src/Xcaciv.Command.Packages/SearchCommand.cs` in full |
| G-5 | Complete the extension loop by installing a discovered bundle into the Plugin Directory. | **Aspirational only.** Declared and registered (`InstallCommand.cs:14-17`), with a half-built routine (`NugetWrapper.cs:111-128`) whose extraction and dependency steps are unwritten (`:125-127`). |
| G-6 | Keep the terminal face swappable and the session testable, by routing every character the user sees through one contract with exactly one terminal implementation. | `ConsoleContext.cs` is the only component that touches the terminal; the Session is driven end-to-end in tests through a hand-written stand-in (`Xcaciv.Cupcake.Core.Tests/LoopTests.cs:8-65,126-152`). |
| G-7 | Ship as one self-contained file that needs no runtime pre-installed and starts quickly. | Release publish profile: single-file, self-contained, compressed, ahead-of-time precompiled (`src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj:13-18`) |
| G-8 | Keep the shipping variant deliberately simple by consuming an asynchronous capability synchronously. | "A light and syncronous implementation of Cupcake shell" (`src/Xcaciv.Cupcake.Lit/README.md:3`, misspelling in the source); every asynchronous step is blocked on (`Loop.cs:37,62,65`). |
| G-9 | Apply basic hygiene to untrusted input and to the code the shell is asked to fetch. | Encrypted-transport requirement, result-count clamping and search-term truncation (`SearchCommand.cs:31-58`); feed-to-name routing (`NuGet.config:12-20`); per-Plugin sandboxing OUT-OF-REPO: `src/Xcaciv.Command/CommandFactory.cs:80-113`. The commit that added most of this is titled "Update security features and package versions" (`b0ca736`). |

### 2.2 Non-goals — capabilities the source deliberately does not have

A reimplementer must not invent these. Each is an evidence-backed absence.

| # | Non-goal | Evidence of absence |
|---|---|---|
| NG-1 | **No user identity, authentication or authorization.** No login, no accounts, no roles, no permission checks on any Command. | Repo-wide: no credential, token, role or permission construct anywhere in `src/`. |
| NG-2 | **No persistence between runs.** No settings file the product reads at run time, no history file, no state directory, no database. Session Variables live in memory and die with the process. | §8 persistence subsection; the only files written are the downloaded Package Archive and an empty versioned directory (`NugetWrapper.cs:89,119-123`). |
| NG-3 | **No command-line arguments.** Neither executable parses any; the shell cannot be scripted by argument. | `src/Xcaciv.Cupcake.Lit/Program.cs:1-22`, `src/Xcaciv.Cupcake/Program.cs:1-2` |
| NG-4 | **No command history, recall, completion or line editing** beyond whatever the host terminal provides for reading one line. | `ConsoleContext.cs:71` is a single read-a-line call; the Session keeps one line and overwrites it (`Loop.cs:56,65`). |
| NG-5 | **No job control, background execution or interleaving.** Commands run strictly one at a time and the next prompt waits for the previous Command to finish. | `Loop.cs:62,65` |
| NG-6 | **No timeouts and no cancellation.** Waiting for input and waiting for a Command are both unbounded; every framework timeout and output cap defaults to disabled and the product never configures them, nor ever supplies a cancellation signal. | `Loop.cs:62,65,94,96`; OUT-OF-REPO: `src/Xcaciv.Command.Interface/PipelineConfiguration.cs:29-51` |
| NG-7 | **No integrity or provenance verification of downloaded code.** No signature check, no checksum, no publisher trust decision before a Plugin is loaded. Certificate-validation, thumbprint-allowlist and package-signature-verification settings existed and were **deliberately removed** as unused. | `NugetWrapper.cs:81-128`; removal in commit `907c535` |
| NG-8 | **No internationalisation.** Every string is hard-coded in one language; no message catalogue, no locale handling. | Repo-wide: no resource file, no culture parameter. |
| NG-9 | **No metrics, structured logging or audit trail.** The framework offers an audit hook; the product never sets it, leaving the no-op default. | OUT-OF-REPO: `src/Xcaciv.Command/CommandController.cs:106,126-134`; no assignment anywhere in `src/`. |
| NG-10 | **No graphical interface, no remote access, no network listener.** The only network use is outbound to a package registry. | Repo-wide. |
| NG-11 | **No dependency resolution or transitive install for Plugins.** The step exists as an unwritten intention. | `NugetWrapper.cs:126` |

### 2.3 Explicitly excluded from the clone (found in source, not to be rebuilt)

| # | Exclusion | Why |
|---|---|---|
| X-1 | **The second executable** (`src/Xcaciv.Cupcake/`). A two-line program that prints `Hello, World!` and references the session library without using it. Vestigial: its only real source file was deleted in commit `7b3a6b5`, and the shipping variant that replaced it arrived in `da1831b`. | Dead. Build one executable, not two. Recorded here rather than silently omitted. |
| X-2 | **The duplicated dependency-version manifest.** The same version list exists byte-for-byte at the repository root and under `src/`, differing only by a trailing newline (813 vs 811 bytes), free to drift apart. | A clone needs one. |
| X-3 | **The unreferenced empty folder declaration** in the session library's project file, and an unused import in the shipping program left over from an abandoned attempt at richer error reporting (`src/Xcaciv.Cupcake.Lit/Program.cs:4`, added in the same commit as the error handler, `569bbd0`). | Leftovers. |
| X-4 | **The `ideas/` directory** — present on disk, empty, untracked by version control. | Nothing to clone. |
| X-5 | **"Having tests" as a product feature.** The test content is mined throughout for acceptance criteria and is the single best requirement source in the repository, but the test projects themselves are not a feature of the shell. | Scope. See §9 for what a clone should test differently. |

### 2.4 The build state — a goal for the clone, not of the source

The source **does not build at the pinned commit**. This is stated as a goal for the clone because a
reimplementer will otherwise assume the reference implementation runs and try to compare against it.

1. **A missing declaration.** The Package Search command assigns into and reads from a result
   accumulator that has no declaration. `List<string> searchResult = [];` was removed in commit
   `e1123b2`; the five uses at `src/Xcaciv.Command.Packages/SearchCommand.cs:66,69,72,81,85` remain.
   No framework version defines such a member — confirmed by a whole-tree search of the reference at
   both `v2.1.2` and framework HEAD. Residual uncertainty: the exact pinned artefacts could not be
   fetched (reason 2), so the pinned base-class surface is unconfirmed. Behaviourally the accumulator
   is inert — it is fully reassigned on every path before being read — so the documented rendering
   behaviour stands regardless.
2. **Unresolvable dependencies.** `dotnet restore` fails on every project with NU1301. Package-source
   mapping gives the public index the pattern `*` and a local folder feed the pattern `Xcaciv.*`,
   while the private hosted feed is declared with **no** pattern and is therefore never consulted
   (`NuGet.config:5-22`). Every first-party package is thus sought only in the local folder, whose
   path is the unexpanded token `%NUGET_LOCAL_PACKAGES%`. Independently, the framework package is
   absent from the public index (HTTP 404 on its flat-container index).

**Goal for the clone:** a working build from a clean checkout with no out-of-band, machine-local,
undocumented prerequisite. Whatever supplies the command capability must be obtainable by anyone who
clones the repository, or must be part of the repository.
