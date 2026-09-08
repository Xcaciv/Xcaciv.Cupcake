# Orchestrator notes — evidence gathered directly, for Phase 3 synthesis

These are findings the orchestrator established first-hand (by running commands and reading
both the subject repo and the framework reference). They take precedence over dossier claims
where the two disagree, and they must all survive into the PRD.

---

## A. State of the source at the pinned commit

| # | Finding | How verified |
|---|---|---|
| A1 | HEAD is `a05dc6f00d4510ac073f6dd02fbfba2ffb76a5b6` on `main`; 25 commits total; last commit authored 2025-12-26. | `git rev-parse HEAD`, `git log` |
| A2 | Licensed BSD 3-Clause, © 2022 Alton Crossley. | `LICENSE:1-4` |
| A3 | **The solution cannot restore.** Every project fails with NU1301: the package source `%NUGET_LOCAL_PACKAGES%` is never expanded, so NuGet treats the literal token as a directory path and cannot find it. | ran `dotnet restore Xcaciv.Cupcake.sln` at the pinned commit |
| A4 | **The pinned framework package is unreachable.** `Xcaciv.Command` is not published to nuget.org — the flat-container index returns HTTP 404. | `curl https://api.nuget.org/v3-flatcontainer/xcaciv.command/index.json` → 404 BlobNotFound |
| A5 | **`NuGet.config` makes the GitHub feed unusable even with credentials.** Package-source mapping is enabled, `nuget.org` claims pattern `*` and `local` claims `Xcaciv.*`; the declared `github` source is given **no pattern at all**. Under source mapping a source with no matching pattern is never consulted, so every `Xcaciv.*` package can only be sought in the broken `local` source. | `NuGet.config:5-22`; consistent with the NU1301 failure in A3 |
| A6 | **`SearchCommand` does not compile.** The local declaration `List<string> searchResult = [];` was deleted in commit `e1123b2` while all five uses of the identifier were kept; HEAD still has 5 uses and 0 declarations. The framework base class has no such member at v2.1.2 or at framework HEAD, so the identifier is unresolved. | per-commit walk of `git show <c>:src/Xcaciv.Command.Packages/SearchCommand.cs`; `grep -rn searchResult` over both framework checkouts |
| A7 | Residual uncertainty on A6: the exact published `Xcaciv.Command.Core` 2.1.0 assembly could not be obtained (see A4), so the base-class surface at that precise version is unconfirmed. The conclusion is nonetheless strongly evidenced — a base class exposing a member named `searchResult` would be implausible, and no framework tag has one. Record as **QUIRK / near-certain build break**, not as verified compilation. |
| A8 | Source files are UTF-8 with BOM and CRLF line endings. | `hexdump` of `Loop.cs` |
| A9 | The `ideas/` directory exists on disk, is empty, and is untracked. | `ls`, `git ls-files` |

## B. Exact literals (verified character by character — do not paraphrase these)

- Prompt: `Ɛ> ` — U+0190 LATIN CAPITAL LETTER OPEN E, then `>`, then one space. (`Loop.cs:16`)
- Exit vocabulary: `END`, `EXIT`, `BYEE` — note the doubled final E in the third. Matched
  case-insensitively by ordinal comparison. (`Loop.cs:20`, `:57`, `:91`)
- Package directory default: `.\packages` — a backslash-separated relative path. (`Loop.cs:24`)
- Progress template: `{0} progress {1}%` where `{0}` is the context name and `{1}` the computed
  number. (`ConsoleContext.cs:20`)
- No-plugins guidance text: ``No Plugins Found. You may want to check out `install --help` ``
  (`Loop.cs:47`) — note it names `install`, but the command is actually reachable only as
  `PACKAGE INSTALL` (see D6), and `--help` is not one of the framework's help tokens (see C7).
- Load-failure summary: `Unable to load commands.` (`Loop.cs:53`, `:84`)
- Process-level crash line: `Error {message}` (`Lit/Program.cs:16`), written to standard output,
  followed by exit code `1` (`Lit/Program.cs:18`).
- Default registry endpoint: `https://api.nuget.org/v3/index.json` (`SearchCommand.cs:28`)
- Insecure-source rejection: `Insecure or invalid package source URL. HTTPS is required.`
  (`SearchCommand.cs:34`)
- Unparseable count rejection: `The 'take' parameter must be a valid integer value.`
  (`SearchCommand.cs:44`)
- Install command responses: `Not installing ` + comma-joined parameters (`InstallCommand.cs:21`);
  piped form `Not installing {chunk} ` + comma-joined parameters (`InstallCommand.cs:26`).
- Search piped-input response: `Unsupported search method for {chunk} (piped)` + comma-joined
  parameters (`SearchCommand.cs:90`).
- Environment key read for the registry endpoint: `PackageSourceUrl` (`SearchCommand.cs:24`),
  normalized to `PACKAGESOURCEURL` by the framework (see C6).
- Numeric bounds: result count clamped to `[1, 100]` (`SearchCommand.cs:46`); declared default
  `20` (`SearchCommand.cs:15`); search term truncated at `200` characters (`SearchCommand.cs:55-58`).
- Colour scheme (`ConsoleContext.cs:23-30`): output Blue on Black; status Yellow on DarkBlue;
  prompt Green on Black.

## C. Framework semantics — OUT-OF-REPO evidence, `Xcaciv.Command` @ tag v2.1.2, commit `f34dedca8dc6d690290b2139acbd3e9b8264349c`

These describe the **required capability**, not a Cupcake feature. They are what a reimplementer
must reproduce for the shell to behave as it does.

- **C1 — Command-name extraction.** Trim the line; take the text before the first space (or the
  whole line if there is none); strip leading/trailing hyphens; delete every character outside
  `[-_0-9a-zA-Z ]`; uppercase the result. (`CommandDescription.cs:52-64`)
- **C2 — Argument tokenization.** Tokens are matched as *either* a double-quoted run *or* a run of
  word characters and hyphens. Each token then has every character outside
  `[-_0-9a-zA-Z .*?\[\]|"~!@#$%^&*()]` deleted, and surrounding double quotes trimmed. The first
  token (the command name) is dropped. (`CommandDescription.cs:20-24`, `:69-79`)
  **Consequence:** an unquoted token cannot contain a dot, colon, or slash — a URL typed without
  quotes is shredded into fragments. Percent signs survive sanitization but are not part of the
  token pattern, so `%VAR%` must be double-quoted to reach a command intact. This is exactly why
  the built-in echo command's help says to use double quotes for environment variables.
- **C3 — Pipelines.** A line containing `|` is executed as a pipeline. Segments split on unquoted
  `|`; single and double quotes group; backslash escapes `|`, `"`, `'` and itself; segments are
  trimmed and empty segments dropped. Every stage gets its own child IO context, is told its
  1-based stage number and the stage total, reads the previous stage's channel and writes a fresh
  bounded channel. All stages run concurrently; the last channel is drained into the parent
  context's output. (`PipelineParser.cs:25-90`, `PipelineExecutor.cs:66-108`)
- **C4 — Pipeline resource defaults.** Channel capacity 10,000 items; back-pressure policy Block
  (producers wait); whole-pipeline timeout 0 = none; per-stage timeout 0 = none; per-stage output
  byte cap 0 = unlimited; per-stage output item cap 0 = unlimited. (`PipelineConfiguration.cs:15-51`)
- **C5 — Parameter binding order and rules.** Ordered → flags → named → suffix.
  - *Ordered*: consumes the head token if it does not start with `-`; otherwise (or if the list is
    empty) falls back to a declared default, and throws `Missing required parameter <name>` when
    required with no default. Allowed-value lists are enforced with
    `Invalid value for parameter <name>, this parameter has an allow list.`
  - *Flags*: a token starting with `-` matching `-{1,2}<name>` (or a short alias) is removed and the
    flag recorded. **The flag key is added to the result unconditionally**, carrying `True`/`False`.
  - *Named*: same matching; the **next** token becomes the value and both are removed. Missing →
    declared default, else throw if required. Allowed values enforced case-insensitively. The key is
    added unconditionally.
  - If the command is invoked with **zero** arguments, binding returns an **empty** map — no
    defaults are applied at all. (`CommandParameters.cs:12-171`, `AbstractCommand.cs:190-205`)
- **C6 — Environment semantics.** Keys are uppercased on both read and write, so lookups are
  case-insensitive. **Reading a missing key with the default options stores the default under that
  key as a side effect.** Each command executes against a child environment; the parent is updated
  only when the command is registered as environment-modifying and the child actually changed.
  (`EnvironmentContext.cs:70-113`, `CommandExecutor.cs:214-217`)
- **C7 — Help.** A request is any parameter equal to `--HELP`, `-?` or `/?` (case-insensitive). The
  bare command name `HELP` lists every registered command one line each. Help text is generated
  from the declared attributes: name, description, usage prototype, an Options block listing ordered
  parameters, flags, named parameters (rendered `[a|b|c]` when an allowed-value list exists) and
  suffix parameters, then any remarks. (`AbstractCommand.cs:62-146`, `:243-254`;
  `CommandExecutor.cs:57-73`)
  Note `--help` (the string the no-plugins guidance suggests) *is* accepted — the comparison is
  case-insensitive — but `install --help` is not a valid invocation because the command is namespaced
  (see D6).
- **C8 — Unknown command.** Output is `Command [NAME] not found. Try 'HELP'`.
  (`CommandExecutor.cs:83-85`)
- **C9 — Command failure.** Any exception thrown by a command is caught by the framework: the user
  sees `Error executing NAME (see trace for more info)` as output plus a status line
  `**Error: <message>`, and the detail goes to trace. The session is not interrupted.
  (`CommandExecutor.cs:222-228`)
- **C10 — Built-in command set**, registered under package key `Default`: a regular-expression
  filter, an echo that expands `%VAR%` references, an environment setter (the only built-in flagged
  as environment-modifying), and an environment dump. (`CommandController.cs:175-185`)
- **C11 — Plugin discovery.** A package directory is *verified* before use: it must exist and must
  resolve inside a restricted root, which defaults to the process working directory. **A directory
  that fails verification is silently ignored** — the add call returns false and the caller discards
  it. If no verified directory remains, loading raises the "no plugins found" condition. Scanning
  looks inside each package's `bin` sub-directory by default.
  (`VerifiedSourceDirectories.cs:79-86`, `:96-110`; `CommandLoader.cs:37-55`;
  `CommandController.cs:167-171`)
- **C12 — Plugin instantiation.** Each plugin assembly is loaded into an isolated context sandboxed
  to its own directory, under a security policy that is forced to Strict when reflection-emit is
  disallowed. (`CommandFactory.cs:80-113`)
- **C13 — Sub-commands.** A command declaring both a root group and its own name registers under the
  uppercased **group** name, with itself as a sub-command under its uppercased own name. Two commands
  sharing a group merge into one registry entry. At execution the first argument is matched
  (uppercased) against the sub-command table and **removed from the argument list** before the
  sub-command runs. (`CommandParameters.cs:173-215`, `CommandRegistry.cs:20-34`, `CommandFactory.cs:38-55`)
- **C14 — Output plumbing.** Command output goes to the stage's output channel if one is attached,
  otherwise to the adapter's own rendering. Empty output chunks are skipped. Trace messages are
  emitted as output only when the **base** verbosity flag is set; otherwise they go to the platform
  debug trace. (`AbstractTextIo.cs:66-73`, `:162-172`; `CommandExecutor.cs:196-200`)

## D. Consequences for Cupcake — quirks to carry into the PRD

| # | Quirk | Basis |
|---|---|---|
| D1 | **The prerelease flag has no effect; prerelease packages are always included.** The search command decides by asking whether the flag key is *present* in the bound parameters, but flag keys are always present (C5) — carrying `True` or `False` as a value that is never read. | `SearchCommand.cs:47` + C5 |
| D2 | **The verbosity fallback branch is unreachable.** Verbosity is declared with an allowed-value list, so an out-of-list value is rejected during binding (C5) and never reaches the command's own default branch. | `SearchCommand.cs:16,79-82` + C5 |
| D3 | **The verbosity presence check is always true** for the same reason as D1, so the ternary's alternate path is also dead. | `SearchCommand.cs:62` + C5 |
| D4 | **The search term must be the first argument.** It is bound as an ordered parameter and processed before flags and named parameters are stripped, so a leading `-flag` makes binding fail with `Missing required parameter search_terms`. | `SearchCommand.cs:13` + C5 |
| D5 | **Invoking search with no arguments reports a misleading error.** Zero arguments means binding returns an empty map (C5), so the first thing to fail is the result-count lookup — the user is told the count parameter must be an integer, not that the search term is missing. | `SearchCommand.cs:42-45` + C5 |
| D6 | **The commands are namespaced.** Both package commands declare the group `Package`, so they are reachable as `PACKAGE SEARCH …` and `PACKAGE INSTALL …`, not as `search`/`install`. The no-plugins guidance text suggests `install --help`, which will not resolve. | `SearchCommand.cs:11-12`, `InstallCommand.cs:14-15` + C13 |
| D7 | ~~**Reading the registry endpoint pollutes the environment.**~~ **CORRECTED 2026-08-31 — this note was WRONG.** The read does write the default back under the uppercased key, but every Command runs against a **child** variable scope seeded with a copy, and that child is merged back only when the Command is registered as environment-modifying — which the shipping host does not do for either package Command. The write is therefore discarded on return and **no later environment dump lists the key**. It is observable only on a direct-invocation path where a host passes its own store straight to the Command, which is exactly what the repository's own tests do. | `SearchCommand.cs:24`; OUT-OF-REPO `CommandExecutor.cs:187,216-219`, `EnvironmentContext.cs:45-55,94-110`, `CommandController.cs:190`; `Lit/Program.cs:11`; `SearchCommandTests.cs:15,31,90` |
| D8 | **Verbosity shadowing silences trace output.** The console adapter re-declares the verbosity flag rather than setting the inherited one, so the inherited trace path keeps reading the inherited flag, which stays off. Trace messages therefore never reach the console however the adapter is configured. | `ConsoleContext.cs:31` + C14 |
| D9 | **Child contexts ignore the parent's verbosity.** Children are built without passing the parent's setting, so they take the constructor default (on) — a quiet session becomes chatty inside command execution. | `ConsoleContext.cs:38-47` |
| D10 | **Progress reports a ratio, not a percentage**, and divides by zero when the step count is zero. The test pins the current arithmetic rather than a percentage. | `ConsoleContext.cs:79-84`; `ConsoleContextTests.cs:25-31` |
| D11 | **Built-in commands are registered twice** on the default startup path — once by the convenience entry point and once inside the session start. Harmless, because registration overwrites by key, but redundant. | `Loop.cs:108,41` + `CommandRegistry.cs:20-34` |
| D12 | **The two session entry points diverge.** The blocking one registers built-ins and downgrades "no plugins found" to friendly guidance; the async one does neither, so on a fresh install it fails startup outright with the generic load failure. | `Loop.cs:39-54` vs `:77-85` |
| D13 | **Exit words are never executed as commands.** The exit test runs at the top of each iteration, before dispatch, so typing an exit word ends the session without attempting to run it. | `Loop.cs:57-66` |
| D14 | **A settings property named for the install command is never read.** Nothing gates the install command on it. | `Loop.cs:11` (only occurrence) |
| D15 | **The default package directory uses a backslash separator**, which is a literal filename character on POSIX platforms. Combined with C11's silent-ignore rule, a mislocated directory produces no diagnostic at all — just the no-plugins message. | `Loop.cs:24` + C11 |
| D16 | **The release profile changes platform, not just optimisation.** Debug targets .NET 8 cross-platform console; Release targets .NET 6 Windows-only, as a windowed-subsystem single-file self-contained trimmed 64-bit binary. Trimming a host that instantiates plugin types reflectively is hazardous. | `Xcaciv.Cupcake.Lit.csproj:4-24` |
| D17 | **INFERRED: the single-file release build breaks host-registered commands.** Commands compiled into the host are registered with their assembly's file location, and the framework instantiates them by loading that path; in a single-file bundle the location is empty, which the framework treats as "no assembly was defined". | `CommandFactory.cs:64-78` + `CommandRegistry.cs:47-51` + release profile D16. Not executed — reasoned. |
| D18 | **Install is inert.** The command returns a literal refusal. The half-built install routine downloads the archive, reads its identity, and creates a versioned directory, but never extracts and never resolves dependencies — and the directory shape it creates does not match the `bin` sub-directory the loader scans (C11). | `InstallCommand.cs:19-27`; `NugetWrapper.cs:111-128`; `Loop.cs:99` |
| D19 | **Downloads overwrite unconditionally** and are never integrity-checked; package identity is read from the downloaded archive itself rather than from the request. | `NugetWrapper.cs:89`, `:103-108`, `:118` |
| D20 | **Dead code in version enumeration.** A second cache object and a search filter are constructed and never used. | `NugetWrapper.cs:37,43-45` |
| D21 | Error and crash text is written to standard output rather than an error stream, so redirecting output swallows diagnostics. | `Lit/Program.cs:16`; `Loop.cs:47` |

## E. Actors observed

- **Shell user** — types command lines at the prompt, reads rendered output, ends the session.
- **Plugin author** — publishes a command package to a registry; must satisfy the command
  authoring contract and the `bin` layout the loader scans.
- **Shell operator / embedder** — the program that constructs the session, sets the prompt, exit
  vocabulary and package directory, registers host-linked commands, and chooses blocking or async
  execution. In this repo the shipping executable plays this role.
- **Registry service** — the external package index that answers search, version, dependency and
  download requests.
- No authentication, authorization, multi-tenancy, or persistence of user identity exists anywhere
  in the source. Do not invent any.

---

## F. Second-pass framework findings (added after deeper reference reading)

- **C15 — Plugin layout convention.** The scanner looks for compiled plugin files matching
  `<PluginDirectory>/*/<subDirectory>/*.dll` — by default `<PluginDirectory>/*/bin/*.dll` —
  searched recursively. Each discovered file becomes one Plugin whose internal name is the file's
  base name joined by a hyphen to its directory path relative to the Plugin Directory with
  separators removed. A Plugin contributing no valid Commands is dropped. Any per-Plugin load
  failure (security violation, missing file, unreadable or wrong-architecture binary, or any other
  error) is swallowed to the diagnostic trace and that Plugin is skipped — the Session continues.
  Scanning switches to parallel processing above 50 discovered files.
  (OUT-OF-REPO: `src/Xcaciv.Command.FileLoader/Crawler.cs:18,64-152,159-180` (framework v2.1.2))
- **C16 — Two different "nothing found" conditions exist, and they are unrelated types.**
  "No verified Plugin Directory" and "Plugin Directory contains no plugin binaries" are separate,
  sibling error types; neither derives from the other.
  (OUT-OF-REPO: `src/Xcaciv.Command.Interface/Exceptions/NoPluginsFoundException.cs`,
  `.../NoPackageDirectoryFoundException.cs`; `CommandLoader.cs:41-44`; `Crawler.cs:170`)
- **C17 — Help prototype generation.** A Command that does not declare a usage prototype gets the
  sentinel default, and the help builder detects the sentinel and synthesizes the usage line from
  the declared parameters instead. Declared metadata defaults: description and prototype both the
  sentinel `TODO`, version `0.0.0`, alias empty. Parameter names are normalized to lowercase with
  invalid characters stripped, which is why bound parameters are addressed in lowercase.
  (OUT-OF-REPO: `CommandRegisterAttribute.cs:34-51`, `AbstractCommandParameter.cs:16-20`,
  `AbstractCommand.cs:118-126`)

### Additional Cupcake quirks

| # | Quirk | Basis |
|---|---|---|
| D22 | **Creating the Plugin Directory can break startup, while omitting it does not.** If `.\packages` is absent the directory is silently unverified, loading raises the "no verified directory" condition, the Session catches exactly that condition and shows friendly guidance, and the Session continues. If `.\packages` exists but holds no plugin binaries in the expected layout, the scanner raises the *other*, unrelated "no plugin binaries" condition, which the Session does **not** catch — it is wrapped as a Startup Load Failure, escapes the Session, and terminates the process with the crash line and exit code 1. The friendlier outcome is the one where the user has done less setup. | `Loop.cs:42-50` + C11 + C15 + C16 |
| D23 | **The search command advertises a source parameter it never reads.** A named parameter for the registry source is declared, so it appears in generated help and is accepted on the command line, but the endpoint is taken solely from the Session Variable and its built-in fallback. Supplying the parameter changes nothing. | `SearchCommand.cs:14` vs `:24-29` |
| D24 | **Declared parameter metadata is thin.** Neither package Command declares a usage prototype, description version, or alias, so help shows a synthesized usage line and the Commands carry the framework's placeholder metadata defaults. | `SearchCommand.cs:11-17`, `InstallCommand.cs:14-16` + C17 |

---

## G. Findings established during the verification pass (2026-08-31)

### G1 — The Plugin scan cannot succeed on a real filesystem. **Executed, not reasoned.**

The scanner composes its search mask as `<wildcard>/<subdirectory>/<binary pattern>` → `*/bin/*.dll`
and hands it to a recursive directory enumeration rooted at the verified Plugin Directory
(OUT-OF-REPO: `src/Xcaciv.Command.FileLoader/Crawler.cs:20,173-178`).

Tested directly on the analysis host against a real filesystem, with a correct layout in place
(`<root>/HelloPkg/bin/Hello.dll`, `<root>/Other/bin/Other.dll`):

```
mask = */bin/*.dll
  [AllDirectories]    THREW DirectoryNotFoundException: Could not find a part of the path '<root>/*/bin'.
  [TopDirectoryOnly]  THREW DirectoryNotFoundException: Could not find a part of the path '<root>/*/bin'.
  [control *.dll AllDirectories]  OK count=2
```

The directory portion of a search mask is joined to the root **literally**; a wildcard in it is never
expanded. The control run proves the layout was correct and the mask is the sole cause.

**Consequences, all of which outrank the earlier D22:**

1. **No Plugin is ever loaded from disk.** The product's headline extensibility mechanism does not
   function as shipped.
2. **Any Plugin Directory that survives verification is fatal at startup** — populated or empty alike.
   D22 said an *empty* directory was fatal while a populated one worked; that is now superseded.
   Nothing distinguishes them, because the enumeration raises before it can look.
3. The raised condition is a directory-not-found failure, which is neither of the two "nothing found"
   conditions. The tolerant startup branch does not catch it: it becomes a Startup Load Failure
   carrying `Unable to load commands.`, escapes the Session, and ends the process with status 1.
4. The condition `No packages found in <path>.` is **unreachable on the shipping path** — reachable
   only with an empty sub-directory filter, which the product never passes. C16's two-signal
   distinction is therefore real in the framework but moot for this product.
5. The only startup that reaches a usable Prompt is one where the Plugin Directory does **not** exist
   — which is the default state anyway, since the compiled-in default `.\packages` is a literal
   filename on a POSIX host (D15).
6. INFERRED: the framework's own scanner tests pass because they run against a mock filesystem that
   expands the wildcard segment; production uses the real filesystem and cannot.

Caveat: executed on the analysis host's current runtime, not the runtime the product targets. This
enumeration semantic is long-standing and INFERRED to be identical there.

### G2 — D7 was wrong; see the corrected row in section D.

The Session-Variable read side effect is real but scoped to a per-execution child that is discarded.
Recorded here because D7 propagated into three acceptance criteria before verification caught it —
a reminder that a chain of correct framework readings can still yield a wrong conclusion when one
link (the child-scope write-back gate) is missed.
