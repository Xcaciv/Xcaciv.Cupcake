# Feature: Shell Distribution & Entry Points

> Scope note: this dossier covers **how the shell product is packaged and started** — the shipping program's entry point and startup wiring, its process-level failure contract, the packaging/publish profile that defines the delivered artifact, the solution-wide build matrix and dependency-feed configuration, and the second, abandoned program in the same repository. The behavior of the interactive session itself, the console presentation, plugin discovery, the package commands and the registry client are **adjacent features**; they appear here only as interfaces this feature wires together.

---

## Purpose — what user/business problem this solves; who uses it

The product is an interactive command shell that gains its commands from downloadable plugin packages. Before any of that is usable, somebody has to be able to **double-click or type one name and get a running shell**. This feature is that: the one program that starts the product, the fixed sequence of wiring it performs before the user sees a prompt, what the process prints and returns to its parent when startup fails, and the packaging decisions that determine what a user actually downloads.

Actors:

- **End user / shell operator** — launches the delivered program and expects a prompt. Sees the startup status text, and sees the single-line error text if startup fails.
- **Release engineer** — builds the delivered artifact. Chooses between the development profile and the shipping profile, which differ in far more than optimization level (see Business rules R10–R12, R17).
- **Automation / parent process** — consumes the process exit status only. There are exactly two possible statuses (R4, R5).
- **Plugin author** — indirectly affected: the shipping profile's code-trimming decision is in direct tension with the run-time, metadata-driven plugin loading the product depends on (QUIRK-4).

There is a **second program** in the repository that is not the product. It prints a greeting and exits. It is vestigial (R26–R28).

---

## Behavior — observable operations, inputs, outputs, side effects

### Operation 1 — Start the shell (the shipping program, "Cupcake Lit")

**Inputs:** none. The program declares no command-line parameters and never reads the process argument list. Anything typed after the program name on the launching command line is silently ignored (`src/Xcaciv.Cupcake.Lit/Program.cs:7-19` contains the whole of the executable program — the remaining lines of the file are a template comment, two imports and a truncated note, `:1-5` and `:21-22`; no argument access appears anywhere in it).

**Sequence (exact, and order-significant):**

1. Create the interactive session object with all of its defaults (`src/Xcaciv.Cupcake.Lit/Program.cs:9`). The session's defaults, owned by the adjacent Interactive Shell Session feature, are: install-command-enabled = true (declared, but read by nothing — R30), prompt text `Ɛ> `, exit words `END`, `EXIT`, `BYEE`, plugin directory `.\packages` (`src/Xcaciv.Cupcake.Core/Loop.cs:11`, `:16`, `:20`, `:24`).
2. Register the **package-install** command into the session's command controller under the package key `internal` (`src/Xcaciv.Cupcake.Lit/Program.cs:10`).
3. Register the **package-search** command into the same controller under the same package key `internal` (`src/Xcaciv.Cupcake.Lit/Program.cs:11`).
4. Run the session with defaults (`src/Xcaciv.Cupcake.Lit/Program.cs:12`), which registers the framework's own built-in commands, builds a console presentation named `Cupcake Console Context` with an empty parameter list, and enters the interactive loop (`src/Xcaciv.Cupcake.Core/Loop.cs:105-112`).

**Outputs / side effects:** everything the session and presentation produce — a status line, possibly a "no plugins" line, then the prompt, repeatedly, until the user types an exit word. Plugin code files under the plugin directory are opened and inspected during step 4.

**Termination:** when the session loop ends normally, control returns to the end of the entry point and the process ends with status **0** (implicit; `src/Xcaciv.Cupcake.Lit/Program.cs:12-13` — nothing follows the try block except the failure handler).

### Operation 2 — Report a fatal startup or session failure

**Trigger:** any error that escapes the whole of Operation 1 — construction, either command registration, or anything the session throws while loading commands or running the loop.

**Output:** exactly one line on the process's standard output, formed as the literal word `Error`, a single space, then the failure's own top-level message text (`src/Xcaciv.Cupcake.Lit/Program.cs:16`). No prefix, no timestamp, no cause chain, no stack detail.

**Side effect:** the process is terminated immediately with exit status **1** (`src/Xcaciv.Cupcake.Lit/Program.cs:18`).

This line is written **directly to the console's ordinary output stream**, not through the presentation layer the rest of the product uses. Consequences: it is not colorized, it does not honor the presentation's verbose switch, and it is not redirectable through the presentation's pipe plumbing (contrast `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:53-60`, where all normal output goes through the presentation).

**QUIRK-10 — the fatal-error line is written to the ordinary output stream, not to the separate error stream** (`src/Xcaciv.Cupcake.Lit/Program.cs:16`). A parent process that captures only the error stream sees nothing at all when the shell dies; a parent that captures ordinary output gets the failure text interleaved with normal shell output with nothing to distinguish it. Note also that when the failure was raised while a user command was running, the text after `Error ` is a wrapper message rather than the original failure text (R33).

### Operation 3 — Produce the development build

Selecting the development configuration yields: a **console-subsystem** program, targeting **runtime major version 8**, with **no operating-system restriction**, framework-dependent, not bundled, not trimmed, no fixed processor architecture (`src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj:3-8`).

### Operation 4 — Produce the shipping build

Selecting the release configuration replaces the above wholesale (`src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj:10-23`). The distribution requirements it encodes, in generic terms:

| Requirement | Meaning for a reimplementer |
|---|---|
| Windowed-subsystem binary | The executable is marked as a GUI/windowed program, not a console program (`:11`). |
| Runtime major version 6, Windows-only scope | The program targets an *older* runtime generation than the development build, and one narrowed to a single desktop operating system (`:12`). |
| Single-file bundle | All program and runtime files are bundled into one executable file (`:13`, and again at `:18`). |
| Self-contained runtime | The managed runtime is shipped inside the bundle; the target machine needs no pre-installed runtime (`:14`). |
| Specific 64-bit desktop-OS target | One fixed target: 64-bit Windows on x64 (`:15`). |
| Ahead-of-time-compiled startup | Program code is precompiled to native form ahead of time to shorten startup, while retaining the just-in-time compiler as fallback (`:16`). |
| Compressed bundle | The single-file bundle's contents are compressed to reduce download size (`:17`). |
| Trimmed unused code | Code judged unreachable is removed from the bundle to reduce size (`:19`). |
| No embedded application manifest | The executable carries no OS application manifest (`:22`). |

### Operation 5 — The second, abandoned program

A second executable exists in the solution. Its whole behavior is: print the literal line `Hello, World!` and exit with status 0 (`src/Xcaciv.Cupcake/Program.cs:2`). It declares a dependency on the session library but never references it (`src/Xcaciv.Cupcake/Xcaciv.Cupcake.csproj:11`). It has **no** release-configuration overrides, so it is built the same way in both configurations (`src/Xcaciv.Cupcake/Xcaciv.Cupcake.csproj:3-8`).

---

## Business rules & edge cases

> Rule identifiers are stable and are referenced from every other section. R30–R33 were added by a later verification pass and are placed beside the rules they extend, so the numbering is not monotonic down the page.

### Startup wiring

- **R1 — Exactly two commands are registered in-process at startup, and the package key is the literal `internal`.** Install is registered first, search second (`src/Xcaciv.Cupcake.Lit/Program.cs:10-11`). No other command is added by the entry point.
- **R2 — Registration happens on the controller the session created, before the session runs.** The session exposes its controller as a readable property whose default value is a fresh controller (`src/Xcaciv.Cupcake.Core/Loop.cs:25`); the entry point mutates that instance and then hands control to the session (`src/Xcaciv.Cupcake.Lit/Program.cs:9-12`). Ordering guarantee: the two package commands are present in the registry **before** framework built-ins and **before** any plugin is loaded.
- **R3 — Registration order collapses both package commands under one grouping.** Both declare the same command-root grouping name and distinct sub-names in their own declarative metadata (`src/Xcaciv.Command.Packages/InstallCommand.cs:14-15`, `src/Xcaciv.Command.Packages/SearchCommand.cs:11-12`); the registry stores the first under the grouping name and *merges* the second's sub-name into that same entry rather than replacing it (OUT-OF-REPO: `src/Xcaciv.Command/CommandRegistry.cs:24-35` (framework v2.1.2)). Net effect: one top-level command grouping with two sub-commands, install and search. INFERRED from the merge logic cited above (no run performed): reversing the two registration lines would leave the same grouping carrying the same two sub-names, differing only in the insertion order of the sub-names and in which of the two registrations supplied the retained top-level entry.
- **R4 — Any escaping failure prints `Error ` + the failure's own message and terminates with status 1** (`src/Xcaciv.Cupcake.Lit/Program.cs:14-18`). Magic number: **1** is "the shell could not start or the session died", the only failure status.
- **R5 — Status 0 means the session ended normally** (user typed an exit word). There are only two statuses; the shell never distinguishes failure kinds by status code (`src/Xcaciv.Cupcake.Lit/Program.cs:12-19`).
- **R6 — Only the outermost message text is printed; the underlying cause is discarded.** The session wraps every plugin-loading failure in a new failure carrying the fixed message `Unable to load commands.` and the original as its inner cause (`src/Xcaciv.Cupcake.Core/Loop.cs:53`). Because the entry point prints only the top-level message (`src/Xcaciv.Cupcake.Lit/Program.cs:16`), the user sees exactly `Error Unable to load commands.` and the real reason is unrecoverable from the console. **QUIRK-1.**
- **R7 — A missing or empty plugin directory is NOT a startup failure.** The session catches the framework's "no plugins found" signal and writes `No Plugins Found. You may want to check out `install --help`` through the presentation, then continues into the prompt loop (`src/Xcaciv.Cupcake.Core/Loop.cs:45-50`). Confirmed by the framework's behavior: a plugin directory that does not exist is silently *not added* to the search list (OUT-OF-REPO: `src/Xcaciv.Command.FileLoader/VerifiedSourceDirectories.cs:82-89` (framework v2.1.2)), and loading with an empty search list raises exactly that signal (OUT-OF-REPO: `src/Xcaciv.Command/CommandLoader.cs:42-45` (framework v2.1.2)). So a fresh install with no `.\packages` folder starts fine and exits 0.
- **R8 — The framework's built-in commands are registered twice per startup.** Once by "run with defaults" (`src/Xcaciv.Cupcake.Core/Loop.cs:108`) and again inside the session's own load step (`src/Xcaciv.Cupcake.Core/Loop.cs:41`). This is harmless because registration overwrites by name (OUT-OF-REPO: `src/Xcaciv.Command/CommandRegistry.cs:34` (framework v2.1.2)), but it is redundant work at every start. **QUIRK-2.**
- **R9 — The entry point ignores the process argument list entirely** (`src/Xcaciv.Cupcake.Lit/Program.cs:7-19`). There is no `--help`, no `--version`, no non-interactive/one-shot mode, no way to point at a different plugin directory from the launch line, even though the session object exposes settable prompt, exit words and plugin directory (`src/Xcaciv.Cupcake.Core/Loop.cs:11-24`). Those knobs are unreachable from the shipped program.
- **R30 — The session's install-enable switch is dead; the entry point registers the package-install command unconditionally.** The session declares an install-command-enabled setting defaulting to true (`src/Xcaciv.Cupcake.Core/Loop.cs:11`) and a test asserts that default (`Xcaciv.Cupcake.Core.Tests/LoopTests.cs:158`), but no code reads it — a repository-wide search finds exactly two occurrences, the declaration and that assertion. The entry point registers the install command without consulting it (`src/Xcaciv.Cupcake.Lit/Program.cs:10`). A reimplementer who honours the switch would change observed behavior; the observed product ignores it. **QUIRK-11.**
- **R31 — The defaults the entry point silently inherits are the only part of this startup contract that is under test.** A test constructs a session with no configuration and asserts four things: install-command-enabled is true, the prompt is non-empty, the exit-word list is non-empty, and the plugin-directory value is non-empty (`Xcaciv.Cupcake.Core.Tests/LoopTests.cs:154-162`). The concrete values behind those assertions are `Ɛ> `, the three exit words, and `.\packages` (`src/Xcaciv.Cupcake.Core/Loop.cs:11`, `:16`, `:20`, `:24`). No test pins the concrete values — only their presence — so a reimplementer changing the prompt or the plugin-directory name would break no existing assertion.
- **R32 — There are two session run paths, and the entry point takes the synchronous one; the two are not equivalent.** The synchronous path registers the framework built-ins and tolerates the "no plugins found" signal (`src/Xcaciv.Cupcake.Core/Loop.cs:41`, `:45-50`). The asynchronous path does neither: it never registers built-ins and lets every load failure — including "no plugins found" — become the fatal wrapped failure (`src/Xcaciv.Cupcake.Core/Loop.cs:77-85`). Both are exercised by tests that drive a stub presentation answering `END` on every prompt (`Xcaciv.Cupcake.Core.Tests/LoopTests.cs:126-138`, `:140-152`). Had the entry point chosen the asynchronous path, a fresh install with no plugin directory would fail to start and R7 would not hold. **QUIRK-12 — the choice of run path is load-bearing and is nowhere documented in the source.**
- **R33 — A failure raised while a user command runs reaches the entry point wrapped, so the text printed is not the original message.** The session bridges the framework's asynchronous execution into its synchronous loop by blocking on it (`src/Xcaciv.Cupcake.Core/Loop.cs:62`, and likewise `:37` for the status line and `:65` for the prompt read). **INFERRED** (runtime platform semantics; no run was performed): the failure that escapes such a blocking wait is an aggregate wrapper whose own message is a fixed generic sentence with the real message appended in parentheses, so the line the user actually sees for a failing command is of the form `Error One or more errors occurred. (<real message>)` rather than `Error <real message>`. The plugin-load failure path is unaffected, because that call is not bridged (`src/Xcaciv.Cupcake.Core/Loop.cs:43`) — which is why R6 can state its output text exactly. **QUIRK-13.**

### Distribution / build profile

- **R10 — The development and shipping profiles target DIFFERENT runtime versions.** Development: runtime major version **8**, OS-neutral (`src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj:5`). Shipping: runtime major version **6**, Windows-scoped (`:12`). This is a two-major-version *downgrade* applied only when shipping. State plainly: **what is tested in development is not what ships.**
- **R11 — The development and shipping profiles target DIFFERENT operating-system scopes.** Development is OS-neutral and would run anywhere the runtime runs. Shipping is pinned to 64-bit Windows on x64 and to the Windows-specific runtime flavor (`:12`, `:15`).
- **R12 — The shipping profile switches the binary from console-subsystem to windowed-subsystem** (`:4` vs `:11`). For a program whose *entire* interface is reading lines from and writing lines to a console, marking the shipped binary as windowed is contradictory. **QUIRK-3 — real consequence:** launched from a graphical file browser, a windowed-subsystem binary gets no console at all; the presentation's line-read then yields nothing, the session treats an empty line as "not an exit word and not a command", and the loop spins without ever blocking or producing output (`src/Xcaciv.Cupcake.Core/ConsoleContext.cs:71` returns empty text when there is no console input; `src/Xcaciv.Cupcake.Core/Loop.cs:57-66` neither exits nor executes on empty text). INFERRED consequence — not observed running, derived from the observed configuration plus the observed loop logic.
- **R13 — Trimming is enabled on a product whose core value proposition is runtime-loaded plugins. QUIRK-4, with real consequences.** The shipping profile removes code the build tool cannot prove is reachable (`:19`). But the product discovers and creates commands *at runtime*, by scanning plugin files for implementations that satisfy the command contract and reading the declarative metadata attached to them (OUT-OF-REPO: `src/Xcaciv.Command.FileLoader/Crawler.cs:96` and `src/Xcaciv.Command.Core/CommandParameters.cs:177-179` (framework v2.1.2)); registration additionally reads that declarative metadata off an implementation handed in at run time (OUT-OF-REPO: `src/Xcaciv.Command/CommandRegistry.cs:42-47` (framework v2.1.2)). Nothing in the shipped program refers to a plugin's implementations in a way a build tool can see, so a trimmer has no way to know which parts of the framework and runtime surface those plugins need. **INFERRED practical consequences** (derived from the observed trimming setting plus the observed discovery mechanism; no trimmed build was produced): plugins that work in a development build can fail in the shipped build with missing-implementation errors at load time; each such failure is swallowed — separately per implementation and again per package — and merely recorded on a diagnostic trace channel (OUT-OF-REPO: `src/Xcaciv.Command.FileLoader/Crawler.cs:100-120` and `:125-153` (framework v2.1.2)), so the user sees a shell with silently *missing* commands rather than an error. **If you reimplement this, either drop trimming or maintain an explicit keep-list for the plugin contract surface.**
- **R14 — Single-file bundling interacts badly with the registry's recorded plugin path.** Command registration records the on-disk file location of the code module a command's implementation came from (OUT-OF-REPO: `src/Xcaciv.Command/CommandRegistry.cs:49-53` (framework v2.1.2)). For the two commands the entry point registers in-process, that module is inside the single-file bundle and has no on-disk location, so the recorded path is empty. INFERRED (single-file bundling semantics; not observed running). **QUIRK-5.**
- **R15 — The single-file requirement is declared twice** in the shipping profile (`:13` and `:18`). Duplicated, not contradictory; INFERRED no behavioral effect (a setting repeated with the same value). Two further settings (implicit-imports and null-safety) are also restated redundantly (`:20-21` duplicating `:6-7`).
- **R16 — No application manifest is embedded in the shipped binary** (`:22`). Consequences for a reimplementer targeting Windows: the executable declares no OS-compatibility list, no display-scaling awareness, and no privilege level, which re-enables legacy OS compatibility shims and file/registry virtualization for the process. INFERRED consequence; the setting itself is directly observed.
- **R17 — The shipping profile cannot actually be satisfied by this repository as configured. QUIRK-6.** The shipping program targets runtime major version 6, but both libraries it depends on are built only for runtime major version 8 (`src/Xcaciv.Cupcake.Core/Xcaciv.Cupcake.Core.csproj:4`, `src/Xcaciv.Command.Packages/Xcaciv.Command.Packages.csproj:4`), and the external command framework it references publishes no runtime-6 build at all (OUT-OF-REPO: `Directory.Build.props:5-7` (framework v2.1.2) — the framework builds for runtime major 10, optionally also 8; a repository-wide search for a runtime-6 target in the framework clone returns nothing). INFERRED consequence (platform rule — a program targeting an older runtime generation cannot consume libraries built only for a newer one; the target values themselves are directly observed): a release build cannot resolve its own dependencies. **The shipping profile as written is aspirational, not a working recipe.**
- **R18 — No artifact identity is overridden anywhere.** No program name, no product version, no icon, no root namespace is set in any project file (searched all project files under `src/` and the repository root; none present). The delivered file is named after the project and carries the tooling's default version. A reimplementer must choose these deliberately.

### Solution build matrix and dependency sourcing

- **R19 — Six solution-wide configurations are declared: development and release, each crossed with Any-CPU, x64 and x86** (`Xcaciv.Cupcake.sln:27-34`). Magic number **6** = 2 configurations × 3 platforms.
- **R20 — The processor-architecture axis is inert.** Every project maps both x64 and x86 to the Any-CPU build (`Xcaciv.Cupcake.sln:36-107`; e.g. the shipping program's mappings at `:60-71`). Selecting x86 produces the same output as Any CPU. Combined with the shipping profile pinning 64-bit-only (R11), the platform axis is doubly meaningless.
- **R21 — Six projects are in the solution: four product projects at the top level and two test projects nested in a folder named `Tests`** (`Xcaciv.Cupcake.sln:6-13`, `:20-25`, `:112-115`).
- **R22 — Two files are carried as solution-level shared items: the dependency-version file under `src/` and the package-feed configuration** (`Xcaciv.Cupcake.sln:15-18`).
- **R23 — Dependency versions are pinned centrally, and the pin file is duplicated.** Two version files exist, one at the repository root and one under `src/` (`Directory.Packages.props:1-19`; `src/Directory.Packages.props:1-19`). Their content is identical line for line, but they are **not** byte-identical: the root copy ends with a line terminator and the `src/` copy does not, so a byte-level comparison reports them as different files. Pinned values: command framework `2.1.1`, framework core `2.1.0`, framework contracts `2.1.0`, package-registry protocol library `7.0.1`, test runner `2.9.3`, test adapter `3.1.5`, test platform `18.0.1`, coverage collector `6.0.4`. INFERRED (build-system directory-walk semantics — the nearest such file above the project wins; not directly observable in the checked-in state): product projects under `src/` resolve against the `src/` copy, while the two test projects, which sit beside `src/` rather than inside it, resolve against the root copy. Keeping two copies in sync is a standing hazard. **QUIRK-7.**
- **R24 — Three package feeds are declared, and the private feed is unreachable by construction.** Declared: the public feed `https://api.nuget.org/v3/index.json`, a private feed `https://nuget.pkg.github.com/xcaciv/index.json`, and a local directory whose path is the literal, unexpanded text `%NUGET_LOCAL_PACKAGES%` (`NuGet.config:6-8`). Routing rules send every package to the public feed and packages whose name begins `Xcaciv.` to the *local directory* feed (`NuGet.config:12-20`). **The private feed has no routing entry at all, so it is never consulted.** INFERRED (feed-routing precedence — where several name patterns match a package, the most specific pattern wins): every package whose name begins `Xcaciv.` is fetched *only* from the local directory and never from the public feed, even though the public feed's catch-all pattern also matches it. The build therefore cannot succeed unless that local directory exists and already holds the framework packages — which is exactly the observed failure (R25). Whether those packages are *also* published on the public feed is not established by this repository and does not matter, because the routing excludes that feed for them. For contrast, the framework's own repository routes the same name prefix to the private hosted feed rather than to a local folder (OUT-OF-REPO: `NuGet.config:15-17` (framework v2.1.2)) and its build script publishes to the public feed by default (OUT-OF-REPO: `BUILD.md:40-53` (framework v2.1.2)) — so this repository looks like it dropped the private-feed routing entry. **QUIRK-8.**
- **R25 — Observed evidence that R24 breaks the build.** The checked-in restore state for the shipping program records failure: `"success": false` (`src/Xcaciv.Cupcake.Lit/obj/project.nuget.cache:4`) with error code `NU1301` (`:13`) and message `The local source '/mnt/g/reversing/reversing/subject/Xcaciv.Cupcake/%NUGET_LOCAL_PACKAGES%' doesn't exist.` (`:15`; the whole log entry spans `:11-20`). The corresponding resolved-dependency file lists an empty library set and an empty target set (`src/Xcaciv.Cupcake.Lit/obj/project.assets.json`, `targets.net8.0` = `{}`, `libraries` = `{}`), and records all three declared feeds as the sources that restore used — with the placeholder feed resolved as a literal directory name under the repository root (same file, the restore source list). Note the placeholder is treated as a literal directory name rather than expanded — on a non-Windows host the percent-delimited form is not an environment reference at all.

### The vestigial second program

- **R26 — The second program's entire behavior is one printed line, `Hello, World!`** (`src/Xcaciv.Cupcake/Program.cs:2`). Both of its lines are the untouched project-template default, including the template's own comment line (`:1`).
- **R27 — It declares a dependency on the session library that it never uses** (`src/Xcaciv.Cupcake/Xcaciv.Cupcake.csproj:11`), and does *not* depend on the package-command library or the command framework — so even if wired up it could not do what the shipping program does.
- **R28 — It is vestigial, and the history says so.** It was the original program (present at the first code commit, `1fcc70c`, 2022-01-14) and was superseded eleven days later by the "lit" program, added explicitly "for offering a simplified interface" (commit `da1831b`, 2022-01-25). Its only real source file — a presentation implementation whose file name was misspelled and six of whose seven members were unimplemented stubs — was deleted in commit `7b3a6b5` (2024-05-16), leaving only the template greeting. Its project has received no content change since (last content-affecting commit `7b3a6b5`, 2024-05-16, versus `59633ea`, 2024-07-10 for the shipping program). **A reimplementation should not carry it forward.**
- **R29 — The shipping program's source file ends in a truncated note**: a comment reading `// TODO:` followed by a comment reading `//  - ` and nothing more (`src/Xcaciv.Cupcake.Lit/Program.cs:21-22`). It also retains an import of an exception-services facility it never uses (`src/Xcaciv.Cupcake.Lit/Program.cs:4`). Both are dead text; no behavior. **QUIRK-9.**

---

## Workflows & states

### W1 — Process lifecycle of the shipping program

```mermaid
stateDiagram-v2
    [*] --> Constructing: process starts (arguments ignored)
    Constructing --> Registering: session object created with defaults
    Registering --> HandingOff: install command added under key "internal", then search command
    HandingOff --> LoadingCommands: run-with-defaults; built-ins registered; console presentation "Cupcake Console Context" created
    LoadingCommands --> LoadingCommands: status line "Loading Commands"
    LoadingCommands --> Interactive: plugins loaded OK
    LoadingCommands --> NoPlugins: plugin directory absent/empty
    NoPlugins --> Interactive: prints "No Plugins Found. You may want to check out `install --help`"
    LoadingCommands --> Failing: any other load error (wrapped as "Unable to load commands.")
    Interactive --> Interactive: prompt, read line, run if non-empty
    Interactive --> Failing: unhandled error during command execution
    Interactive --> ExitOK: user typed END / EXIT / BYEE
    Failing --> ExitErr: print "Error <message>" on ordinary output
    ExitOK --> [*]: process status 0
    ExitErr --> [*]: process status 1
```

Numbered, with the exact ordering guarantee this feature owns:

1. Process starts. The argument list is never read (R9).
2. Session object constructed with defaults (`src/Xcaciv.Cupcake.Lit/Program.cs:9`).
3. Package-install command registered under package key `internal` (`:10`).
4. Package-search command registered under package key `internal` (`:11`) — merges into the same command grouping as step 3 (R3).
5. Control handed to the session (`:12`). From here the adjacent Interactive Shell Session feature owns the flow.
6. On normal loop exit, control returns and the process ends with status 0.
7. On any error escaping steps 2–5, the handler prints one line on the ordinary output stream — never the error stream (QUIRK-10) — and terminates with status 1 (`:14-18`). For a failure raised inside a running user command the printed text is the blocking bridge's wrapper message, not the original (R33/QUIRK-13, INFERRED).

There are **no timeouts** anywhere in this flow. Startup blocks for as long as plugin scanning takes; the loop blocks indefinitely on user input.

### W2 — Building the delivered artifact

1. Choose a configuration. Development → console-subsystem, runtime major 8, OS-neutral, framework-dependent, loose files (E3, R10).
2. Release → the profile silently changes runtime version, OS scope and binary subsystem in addition to enabling bundling/trimming/precompilation/compression (R10–R12).
3. Restore dependencies. Framework packages route only to the local-directory feed whose path is an unexpanded placeholder (R24) — this step fails unless that environment variable is defined and populated (R25).
4. Publish. As configured, a release publish additionally cannot succeed because no runtime-6 build of the dependencies exists (R17).

State plainly for the reimplementer: **there is no evidence in the repository that the shipping profile has ever produced an artifact.** Treat it as a statement of *distribution intent*, and re-derive a working equivalent on the target platform.

---

## Data — entities this feature owns

### E1 — Process outcome (produced, per run)

| Field | Type (generic) | Constraints / values | Lifecycle |
|---|---|---|---|
| exit status | small integer | exactly `0` (normal session end) or `1` (any failure) | set at process termination (`src/Xcaciv.Cupcake.Lit/Program.cs:12-18`) |
| failure line | single line of text | `Error ` + the failure's top-level message — which, for a failure raised while a user command was running, is a wrapper message rather than the original text (R33) ; absent when status is 0 | written once to the ordinary output stream, immediately before terminating (`:16`); never to the error stream (QUIRK-10) |

### E2 — Startup registration list (fixed, compiled in)

| Field | Type | Value | Lifecycle |
|---|---|---|---|
| package key | text | literal `internal` for both entries | created at startup, never mutated (`src/Xcaciv.Cupcake.Lit/Program.cs:10-11`) |
| entry 1 | command instance | the package-install command | registered first |
| entry 2 | command instance | the package-search command | registered second, merged into entry 1's grouping (R3) |

### E3 — Build profile (repository configuration)

| Field | Type | Development value | Shipping value | Evidence |
|---|---|---|---|---|
| binary subsystem | enumeration | console | windowed | `src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj:4`, `:11` |
| runtime generation | version | 8 | 6 | `:5`, `:12` |
| OS scope | enumeration | neutral | Windows | `:5`, `:12` |
| bundling | boolean | off | on (declared twice) | `:13`, `:18` |
| runtime included | boolean | off | on | `:14` |
| target architecture | identifier | unset | 64-bit Windows x64 | `:15` |
| ahead-of-time startup compile | boolean | off | on | `:16` |
| bundle compression | boolean | off | on | `:17` |
| code trimming | boolean | off | on | `:19` |
| OS application manifest | boolean | default (embedded) | suppressed | `:22` |

Lifecycle: static; changed only by editing the repository.

### E4 — Solution build matrix

Six named combinations (2 configurations × 3 platforms) with per-project mappings; both non-default platforms map to the default for every project (`Xcaciv.Cupcake.sln:27-107`). Static.

### E5 — Central dependency pins

Name → exact version pairs, duplicated in two files (R23). Static; a reimplementer needs the *concept* (single source of truth for third-party versions), not the duplication.

### E6 — Package feed configuration

Ordered list of three named sources plus routing rules by name prefix (`NuGet.config:4-20`). Static. See R24 for the routing defect.

---

## Interfaces — to and from other features

### Consumed

| From feature | Semantic contract this feature relies on |
|---|---|
| **Interactive Shell Session** | A session object that (a) can be created with no arguments and comes pre-populated with a command controller and an environment, (b) exposes that controller for mutation before the session runs, (c) offers a single "run with sensible defaults" entry that builds the presentation and enters the loop, and (d) surfaces every unrecoverable problem by *throwing out* rather than exiting itself (`src/Xcaciv.Cupcake.Core/Loop.cs:25-26`, `:105-112`, `:53`). |
| **Console Presentation & Interaction** | Constructed by the session, not by this feature. This feature only requires that a console-backed presentation exists and is chosen by the "run with defaults" path (`src/Xcaciv.Cupcake.Core/Loop.cs:110`, with the display name `Cupcake Console Context` and an empty parameter list). |
| **Package Install Command / Package Search Command** | Two ready-to-register command objects, constructible with no arguments, self-describing through declarative metadata so the controller can name and document them without the entry point supplying any names (`src/Xcaciv.Cupcake.Lit/Program.cs:10-11`; the metadata lives with those commands at `src/Xcaciv.Command.Packages/InstallCommand.cs:14-16` and `src/Xcaciv.Command.Packages/SearchCommand.cs:11-17`). |
| **Plugin Discovery & Command Registration** | A "no plugins found" outcome must be distinguishable from a real load failure, because the two get opposite process-level treatment (R7 vs R6). |
| **External command framework** (see next section) | Register-an-instance-under-a-package-key, register-built-ins, add-plugin-directory, load-plugins, parse-and-run-a-command-line. Semantics summarized in the External technology table. |

### Exposed

| To | Contract |
|---|---|
| **Operating system / launching process** | Two exit statuses only: 0 = clean session end, 1 = anything else (R4, R5). One line of failure text on standard output, un-styled (R4). No arguments accepted (R9). |
| **End user** | A single downloadable executable file that needs no separately installed runtime, on 64-bit Windows only (R10–R11, E3). |
| **Plugin ecosystem** | The plugin search location is a directory named `packages` resolved **relative to the process's current working directory** (`src/Xcaciv.Cupcake.Core/Loop.cs:24`), not relative to the executable. Launching the shipped file from a different working directory changes where plugins are looked for, and there is no way to override it (R9). |
| **Release engineering** | The build profile is the whole distribution specification; there is no installer, no packaging script, no continuous-integration definition anywhere in the repository (searched the repository tree; no build or workflow files exist). |

---

## External technology

| Generic capability | Protocol/standard if any | What the source used | Notes for reimplementer |
|---|---|---|---|
| Managed application runtime with a project/build system that can express per-configuration build profiles | — | C# on .NET; SDK-style MSBuild project files; development profile `net8.0`, shipping profile `net6.0-windows` (`src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj:5`, `:12`) | You need an equivalent of "configuration-conditional build settings". The *fact* that the two profiles diverge in runtime version and OS scope is a defect to fix, not to reproduce (R10, R11, R17). |
| Top-level program entry point with implicit process-wide exception boundary and explicit process termination | — | C# top-level statements; `Environment.Exit(1)` (`src/Xcaciv.Cupcake.Lit/Program.cs:18`) | Any language's `main` + immediate-exit primitive suffices. Note the exit is *immediate* — no unwinding, no cleanup handlers run. |
| Single-file self-contained application bundling with compression | — | .NET publish options: single-file, self-contained, compression-in-single-file (`:13`, `:14`, `:17`, `:18`) | Equivalents: a packed native binary, an embedded-runtime bundle, or an app-image. Expect the runtime to extract or memory-map bundled content on start. |
| Ahead-of-time precompilation of managed code for startup latency | — | .NET ReadyToRun (`:16`) | Partial AOT: native code is emitted ahead of time, JIT stays available. Do not confuse with full native AOT — the product's runtime plugin loading requires a JIT-capable runtime. |
| Whole-program dead-code elimination (trimming) | — | .NET PublishTrimmed (`:19`) | **Directly hostile to this product's plugin model — see R13/QUIRK-4.** If your target toolchain trims, you must supply a keep-list for the plugin contract types and their attribute metadata, or disable trimming. |
| Fixed OS + processor-architecture publish target | — | .NET runtime identifier `win-x64` (`:15`) | 64-bit Windows on x64 only. |
| Windowed (non-console) executable subsystem marking | PE subsystem field on Windows | .NET `WinExe` output type (`:11`) | See R12/QUIRK-3 — almost certainly wrong for this product. |
| Suppression of the default OS application manifest | Windows side-by-side manifest | .NET `NoWin32Manifest` (`:22`) | Removes OS-compatibility, DPI-awareness and privilege declarations. |
| Extensible command framework supplying the controller, contracts, parsing, pipelines, help and plugin loading | — | `Xcaciv.Command 2.1.1`, `Xcaciv.Command.Core 2.1.0`, `Xcaciv.Command.Interface 2.1.0` (`Directory.Packages.props:8-10`), consumed by the shipping program at `src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj:26`. Semantics established from OUT-OF-REPO framework v2.1.2. | Capabilities this feature depends on, with observed semantics: **(a)** register a command instance under a caller-chosen package key; the framework reads the command's declarative metadata, normalizes each declared name by trimming leading dashes, stripping characters it considers invalid and upper-casing the rest, and keys the registry by that normalized name — commands that share a root-grouping name are merged into one entry with multiple sub-names rather than overwriting (OUT-OF-REPO: `src/Xcaciv.Command/CommandRegistry.cs:20-67`; the name-normalizing metadata setters at `src/Xcaciv.Command.Interface/Attributes/CommandRootAttribute.cs:27-32` and `src/Xcaciv.Command.Interface/Attributes/CommandRegisterAttribute.cs:27-32`; the normalizer itself at `src/Xcaciv.Command.Interface/CommandDescription.cs:52-64` (framework v2.1.2)). The same normalizer is applied to the typed command line at dispatch time, which is what makes the two ends match. **(b)** register four built-in commands — regular-expression filter, echo, set-environment-value, dump-environment — under the package key `Default` (OUT-OF-REPO: `src/Xcaciv.Command/CommandController.cs:177-185`, `src/Xcaciv.Command/Commands/*.cs` (framework v2.1.2)). **(c)** add a plugin base directory (silently ignored if it does not exist) and load plugins from a `bin` sub-directory of each package folder, raising a distinct "no plugins" signal when no directory was accepted (OUT-OF-REPO: `src/Xcaciv.Command/CommandController.cs:160-172`, `src/Xcaciv.Command/CommandLoader.cs:38-55` (framework v2.1.2)). **(d)** run a command line: if it contains the pipeline delimiter, execute as a pipeline, otherwise parse the leading token as an upper-cased command name and the rest as arguments (OUT-OF-REPO: `src/Xcaciv.Command/CommandController.cs:214-247` (framework v2.1.2)). |
| Central dependency-version management + multi-source package feeds with per-name routing | NuGet v3 feed protocol over HTTPS | NuGet Central Package Management (`Directory.Packages.props`, `src/Directory.Packages.props`) and NuGet feed routing (`NuGet.config`) | Three feeds declared, one of them a local directory whose path is an unexpanded environment-variable placeholder; the private hosted feed is declared but never routed to. See R24/R25 — the build cannot restore without a pre-populated local folder. |
| Solution/workspace grouping with a configuration × platform matrix | — | Visual Studio solution file (`Xcaciv.Cupcake.sln`) | Six combinations, platform axis inert (R19, R20). |
| Unit test runner (used by adjacent features, pinned here) | — | xUnit `2.9.3` + adapter `3.1.5` + platform `18.0.1` + coverage `6.0.4` (`Directory.Packages.props:14-17`) | No test in the repository exercises the entry point or the build profile — see Confidence. |
| Redistribution licence | — | BSD 3-Clause, Copyright (c) 2022, Alton Crossley (`LICENSE:1-4`) | Permissive; attribution and the disclaimer must ship with any binary distribution. |

---

## Error handling

| Failure mode | What the user/system observes | Evidence |
|---|---|---|
| Plugin directory absent or contains no loadable package | Not fatal. One line through the presentation: `No Plugins Found. You may want to check out `install --help``, then the normal prompt. Process later exits 0. | `src/Xcaciv.Cupcake.Core/Loop.cs:45-50`; OUT-OF-REPO: `src/Xcaciv.Command.FileLoader/VerifiedSourceDirectories.cs:82-89`, `src/Xcaciv.Command/CommandLoader.cs:42-45` (framework v2.1.2) |
| Any other plugin-loading failure | Fatal. Exactly `Error Unable to load commands.` on standard output; exit status 1. The underlying cause is **not shown** (QUIRK-1). | `src/Xcaciv.Cupcake.Core/Loop.cs:51-54`; `src/Xcaciv.Cupcake.Lit/Program.cs:14-18` |
| An individual plugin package fails to load (corrupt, blocked by the loader's security policy, missing file) | The user sees nothing at all. The framework swallows the failure — separately per implementation and again per package — and writes only to a diagnostic trace channel; a package that yields no usable command is simply not added to the result, so the shell starts with that package's commands silently missing. | OUT-OF-REPO: `src/Xcaciv.Command.FileLoader/Crawler.cs:100-120`, `:125-153`, `:156` (framework v2.1.2) |
| An error escapes while a user command is running | Fatal to the whole shell. Same single line, `Error ` + message, exit status 1. There is no per-command error boundary at the entry point — one bad command ends the session. INFERRED: the message shown is the blocking bridge's wrapper text with the real message in parentheses, not the real message on its own (R33/QUIRK-13). | `src/Xcaciv.Cupcake.Lit/Program.cs:14-18`; `src/Xcaciv.Cupcake.Core/Loop.cs:62` |
| Registering either package command throws | Same handler: `Error ` + message, exit 1, before the user ever sees a prompt. | `src/Xcaciv.Cupcake.Lit/Program.cs:10-18` |
| No console attached (shipping build launched from a graphical shell) | INFERRED: nothing visible; the loop spins on empty input without exiting or executing (QUIRK-3). | `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:66-72`; `src/Xcaciv.Cupcake.Core/Loop.cs:57-66`; `src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj:11` |
| Dependency restore cannot find the framework packages | Build-time, not run-time. Restore reports source-not-found and produces an empty dependency graph. | `src/Xcaciv.Cupcake.Lit/obj/project.nuget.cache:4`, `:11-20`; `src/Xcaciv.Cupcake.Lit/obj/project.assets.json` |
| Release build attempted | Build-time. Cannot resolve runtime-6 builds of its own libraries or of the framework (QUIRK-6). | `src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj:12` vs `src/Xcaciv.Cupcake.Core/Xcaciv.Cupcake.Core.csproj:4`, `src/Xcaciv.Command.Packages/Xcaciv.Command.Packages.csproj:4`; OUT-OF-REPO: `Directory.Build.props:5-7` (framework v2.1.2) |

Nothing is logged to a file. There is no diagnostic verbosity switch reachable from the launch line (R9). The only machine-readable failure signal is exit status 1.

---

## Non-functional observations

- **Startup performance appears to be a goal of the shipping profile** — INFERRED intent: the setting is directly observed, but no comment, commit message or document in the repository states why it was chosen. Ahead-of-time precompilation shortens cold start (`src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj:16`). It is partly offset by bundle compression, which trades a smaller download for decompression work at every launch (`:17`).
- **Download size appears to be the second goal** — INFERRED intent, same basis: compression plus trimming both shrink a self-contained bundle that would otherwise carry an entire runtime (`:14`, `:17`, `:19`).
- **Zero-prerequisite install appears to be the third goal** — INFERRED intent, same basis: self-contained means the target machine needs nothing pre-installed (`:14`).
- **Concurrency**: none introduced by this feature. The entry point is strictly sequential and blocking; the underlying framework contract is asynchronous but the product deliberately consumes it by blocking (`src/Xcaciv.Cupcake.Core/Loop.cs:37`, `:62`, `:65`). A reimplementation in a language without an async/blocking mismatch simply writes it synchronously.
- **No permission or integrity check happens at startup.** The entry point does not verify who is running it, does not check that the plugin directory is trusted, and does not verify plugin signatures. The framework applies its own per-plugin path sandboxing during load (OUT-OF-REPO: `src/Xcaciv.Command.FileLoader/Crawler.cs:83-91` (framework v2.1.2)), which is the only guard in the path.
- **No caching** of any kind at this layer. Plugins are re-scanned from disk on every launch.
- **No pagination, no thresholds, no retries** in this feature.
- **Internationalization: none.** Every string is hard-coded English, and there is no resource/locale mechanism (`src/Xcaciv.Cupcake.Lit/Program.cs:16`; `src/Xcaciv.Cupcake.Core/Loop.cs:47`, `:53`).
- **Character-encoding requirement**: the default prompt is the single character U+0190 followed by `> ` (`src/Xcaciv.Cupcake.Core/Loop.cs:16`). The delivered terminal must be able to render a non-ASCII Latin-extended glyph, or the prompt appears as a replacement box. Worth flagging to a reimplementer targeting a legacy code page.
- **Accessibility**: the windowed-subsystem marking (R12) is the single largest accessibility problem — INFERRED on the same basis as QUIRK-3: a screen-reader user launching from a graphical shell would get no interface at all. Beyond that, the product's output relies on fixed foreground/background colour pairs chosen by the presentation layer — one pair for ordinary output, one for status text, one for the prompt — with no high-contrast and no no-colour mode (`src/Xcaciv.Cupcake.Core/ConsoleContext.cs:23-30`).
- **Reproducibility**: there is no continuous-integration definition, no build script and no publish script anywhere in the repository. The delivered artifact is whatever a developer's local publish produces.
- **Repository hygiene signals**: build output directories are checked in under `obj/` despite being ignored by pattern (`.gitignore:30`), and an empty, untracked directory named `ideas` sits at the repository root (present in the working tree, no files under version control).

---

## Acceptance criteria

1. **Given** the shipping program is launched with no arguments and a valid plugin directory, **when** startup completes, **then** the package-install and package-search commands are both available before the first prompt appears, grouped under one command name with two sub-names. *(R1, R3)*
2. **Given** the shipping program is launched with arbitrary extra text on the command line, **when** it starts, **then** the extra text has no effect whatsoever — same prompt, same behavior as launching with none. *(R9)*
3. **Given** no plugin directory named `packages` exists in the current working directory, **when** the program starts, **then** it prints `No Plugins Found. You may want to check out `install --help``, continues to the prompt, and — after the user types `EXIT` — ends with process status **0**. *(R7, R5)*
4. **Given** a plugin directory exists but loading raises a failure other than "none found", **when** the program starts, **then** standard output contains exactly the line `Error Unable to load commands.` and the process ends with status **1**, with no indication of the underlying cause. *(R6, QUIRK-1)*
5. **Given** any error escapes while a user-typed command is executing, **when** it propagates, **then** the whole shell terminates: one `Error ` line on the ordinary output stream and status **1** — it does not return to the prompt, and nothing is written to the error stream. The text after `Error ` is the blocking bridge's wrapper message with the real message in parentheses, not the real message on its own. *(R4, R33/QUIRK-13 — wrapper text INFERRED; QUIRK-10; error-handling table)*
6. **Given** a successful session, **when** the user types `END`, `EXIT` or `BYEE` — matched case-insensitively (`src/Xcaciv.Cupcake.Core/Loop.cs:57`) — **then** the process ends with status **0** and prints no error line. *(R5)*
7. **Given** the development configuration, **when** the program is built, **then** the result is a console-subsystem, framework-dependent, OS-neutral binary targeting runtime generation 8 with no bundling or trimming. *(R10, R11/E3)*
8. **Given** the release configuration, **when** the publish settings are inspected, **then** all of the following are simultaneously true: windowed subsystem, runtime generation 6, Windows-only, single-file, self-contained, 64-bit x64, ahead-of-time-precompiled, compressed, trimmed, no OS manifest. *(R10–R16/E3)*
9. **Given** the two build configurations, **when** their runtime generation and OS scope are compared, **then** they differ (8/neutral versus 6/Windows) — a QA engineer must record this as a defect, not a variant. *(R10, R11)*
10. **Given** the release configuration, **when** a publish is attempted against this repository as checked in, **then** it fails to resolve dependencies, because no runtime-6 build of either in-repository library or of the external command framework exists. *(R17, QUIRK-6)*
11. **Given** a shipping build produced with trimming enabled, **when** a third-party plugin is dropped into the plugin directory, **then** its commands may be missing from the shell with no error shown to the user, whereas the same plugin works in a development build. *(R13, QUIRK-4; INFERRED outcome)*
12. **Given** the environment variable that names the local package folder is undefined, **when** dependencies are restored, **then** restore fails with a source-not-found error naming the literal placeholder text and produces an empty dependency graph. *(R24, R25)*
13. **Given** the solution's six configuration/platform combinations, **when** the x86 combination is built and compared to the Any-CPU combination, **then** the outputs are identical for every project. *(R19, R20)*
14. **Given** the second executable, **when** it is run, **then** it prints exactly `Hello, World!` and exits with status 0 — it neither starts a shell nor loads any plugin. *(R26, R27)*
15. **Given** the shipping build is launched by double-clicking from a graphical file browser, **when** it starts, **then** no interactive console appears and the program neither prompts nor exits. *(R12, QUIRK-3; INFERRED)*
16. **Given** a session object created with no configuration applied, **when** its defaults are inspected, **then** install-command-enabled is true and the prompt, the exit-word list and the plugin-directory value are all non-empty — and the install-enable flag has no effect on what the entry point registers. *(R30, R31; backed by an existing assertion at `Xcaciv.Cupcake.Core.Tests/LoopTests.cs:154-162`)*
17. **Given** a session driven by a stub presentation that answers `END` to every prompt, **when** the synchronous run path is executed to completion, **then** it returns without error and leaves both the command controller and the environment populated; **and given** the same stub on the asynchronous run path, **then** it likewise terminates — but without registering the built-in commands and without tolerating a missing plugin directory. *(R32; backed by existing assertions at `Xcaciv.Cupcake.Core.Tests/LoopTests.cs:126-138` and `:140-152`)*

---

## Confidence & open questions

### Directly observed (high confidence)

- The complete entry-point sequence, the failure text shape, and the exit statuses — the shipping program is 22 lines long and was read in full.
- Every build-profile setting, both configurations, both executables.
- The dead install-enable switch: declared and defaulted at `src/Xcaciv.Cupcake.Core/Loop.cs:11`, asserted by a test at `Xcaciv.Cupcake.Core.Tests/LoopTests.cs:158`, and read by no code anywhere in the repository (R30/QUIRK-11).
- The divergence between the two session run paths, and the entry point's choice of the tolerant one (R32/QUIRK-12).
- That the fatal-error line is written to the ordinary output stream rather than the error stream (R4, QUIRK-10).
- The solution's six-combination matrix and its inert platform axis.
- The duplicated central version-pin files and their exact pinned values.
- The feed configuration and the *observed, checked-in* restore failure with its exact error text.
- The vestigial status of the second executable, corroborated by commit history (`da1831b` 2022-01-25 adding the "lit" replacement; `7b3a6b5` 2024-05-16 deleting the second executable's only real source file).
- Framework semantics for registration, built-ins, plugin loading and command-line dispatch — read from the reference clone and cited OUT-OF-REPO throughout.

### INFERRED — stated as inference, not observation

- **INFERRED** — the consequence of the windowed-subsystem marking (no console when launched graphically; the loop spinning on empty input). Derived from the observed subsystem setting plus the observed loop and presentation logic; no run was performed.
- **INFERRED** — the consequence of trimming on runtime-loaded plugins (silently missing commands). Derived from the observed trim setting plus the framework's observed run-time, metadata-based discovery; no trimmed build was produced.
- **INFERRED** — that a bundled code module's recorded file location is empty under single-file packaging (QUIRK-5). This is packaging-platform behavior, not repository-observable.
- **INFERRED** — the consequences of suppressing the OS application manifest (compatibility shims, file/registry virtualization, no display-scaling declaration). The setting is observed; the effects are platform knowledge.
- **INFERRED** — that the unused exception-services import at `src/Xcaciv.Cupcake.Lit/Program.cs:4` is a leftover from an abandoned attempt at richer error reporting. Its presence is observed (it entered in the same commit that added the error handler, `569bbd0`, 2024-07-08); the intent is not.
- **INFERRED** — that the text printed for a failure escaping a running user command is the blocking bridge's wrapper message rather than the original message (R33/QUIRK-13). Runtime platform semantics; no run was performed.
- **INFERRED** — that a package name matching both the catch-all routing pattern and the more specific `Xcaciv.` prefix pattern is fetched only from the more specific pattern's feed (R24). Feed-routing precedence is platform knowledge; the declared configuration and the resulting restore failure are directly observed.
- **INFERRED** — which of the two duplicated dependency-pin files each project resolves against (R23). Build-system directory-walk semantics; the two files themselves are observed.
- **INFERRED** — the goals attributed to the shipping profile (startup latency, download size, zero-prerequisite install). The settings are observed; no statement of intent exists anywhere in the repository.

### Could not determine

- **Whether the shipping profile has ever produced a working artifact.** Looked for: build scripts, publish profiles, continuous-integration definitions and packaging manifests across the whole repository tree — none exist. Looked at the checked-in restore state under `src/Xcaciv.Cupcake.Lit/obj/`, which records only a *failed* development-configuration restore. No release artifact, no release restore state, no release log is present.
- **Whether the runtime-6 shipping target was deliberate or a stale copy-paste.** Looked at commit history for the project file; the release property group predates the current framework pins. No commit message, comment or note explains the choice. A reimplementer should treat runtime generation 8 (the development value, matching every library in the repository) as the real intent — but this is a judgement call, not evidence.
- **The intended value of the local package-folder environment variable**, and therefore how a developer is expected to obtain the framework packages. Looked at the feed configuration, both version-pin files, the repository README (`README.md`, two lines total) and the shipping program's README (`src/Xcaciv.Cupcake.Lit/README.md`, three lines: the title `# Cupcake Lit`, a blank line, and `A light and syncronous implementation of Cupcake shell.` — the misspelling is in the source). No documentation anywhere names it or explains the bootstrap. The declared private hosted feed would be the obvious source but is not routed to (R24).
- **What the truncated note at the end of the shipping program's source was going to say** (`src/Xcaciv.Cupcake.Lit/Program.cs:21-22`). Checked the commit that introduced it; the text was never completed.
- **The intended delivered file name and product version.** No project sets any identity property (searched all project files). The reimplementer must decide.
- **Whether any test covers this feature.** Only at its edges — this is now resolved rather than open. Both test projects were read in full. `Xcaciv.Cupcake.Core.Tests/LoopTests.cs` does establish two things this feature depends on: the session's defaults, asserted for presence but not for value (`:154-162` → R30, R31), and that both run paths terminate on the exit word `END` when driven by a stub presentation (`:126-138`, `:140-152` → R32). `Xcaciv.Cupcake.Core.Tests/ConsoleContextTests.cs` exercises only the adjacent console-presentation feature (verbose default, status and output writes, progress arithmetic). `Xcaciv.Command.PackagesTests/` exercises the package-search command and the registry client against the live public feed, i.e. adjacent features again. **Nothing tests the entry point itself, the exit statuses, the failure text, the argument-ignoring behavior, or any build-profile behavior.** Acceptance criteria 1–15 are therefore derived from the configuration and code as read; only 16 and 17 restate assertions that already exist.
- **Whether the framework packages are obtainable from the public feed at the pinned versions.** Not checkable offline. The framework's own build tooling publishes to the public feed by default (OUT-OF-REPO: `BUILD.md:40-53` (framework v2.1.2)), but this repository's routing rules exclude that feed for those packages anyway (R24), so the question does not change the outcome: without the local folder, restore fails.
