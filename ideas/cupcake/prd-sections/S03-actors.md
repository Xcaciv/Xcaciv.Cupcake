## 3. Actors & Personas

The product supports **four actors**, plus one consumer that exists as a contract rather than as a
persona. Every one is established from source; none is taken from a design document, because the
repository contains none. Read this section as a boundary list — it records who can act on the
product and, just as importantly, who the product does not know exists. §3.6 records the identity,
permission and persistence machinery the product does **not** have, because the single largest risk
in reimplementing this product is inventing an actor the source never modelled.

**A note on the word "operator".** Several source-side descriptions use "operator" loosely for the
person at the keyboard. This document uses the glossary term: a **Shell Operator** is a *program*,
not a person — whoever constructs and configures a Session. The person at the keyboard is the
**Shell user**.

| # | Actor | Kind | Exists at | Established by |
|---|---|---|---|---|
| 3.1 | Shell user | human | run time | the Prompt/dispatch/exit cycle and its single input path (`src/Xcaciv.Cupcake.Core/Loop.cs:32-68`) |
| 3.2 | Shell Operator | program (embedding host) | construction + run time | the Session's public settings surface and three entry points (`src/Xcaciv.Cupcake.Core/Loop.cs:11-26,32,70,105`), exercised by the shipping executable (`src/Xcaciv.Cupcake.Lit/Program.cs:7-13`) |
| 3.3 | Plugin author | human/organisation, outside the process | authoring + publish time | the discovery machinery that exists only to serve them (§7.4) and the authoring contract both in-repo Commands satisfy (`src/Xcaciv.Command.Packages/SearchCommand.cs:11-20,88`) |
| 3.4 | Package Registry | external system | run time | the only outbound conversation the product initiates (`src/Xcaciv.Command.Packages/NugetWrapper.cs:20-101`) |
| 3.5 | Automated consumer | contract, not persona | run time | two process exit statuses plus two in-process invocation paths (`src/Xcaciv.Cupcake.Lit/Program.cs:14-19`) |

---

### 3.1 Shell user

**Who they are.** A person at a text terminal who launches the delivered program and is given a
Session. They are anonymous, unidentified and unprivileged: the product has no notion of who they
are, and every registered Command is available to whoever reaches the Prompt (§3.6). One person, one
Session, one process — there is no second occupant to be separated from.

**What they can do.**

1. **Start a Session by launching the program.** It accepts no arguments — the entry point never
   reads an argument list (`src/Xcaciv.Cupcake.Lit/Program.cs:7-13`) — and there is no switch, no
   settings file and no import of operating-system environment values at startup (whole-repository
   census: 27 tracked files, none of them a settings file).
2. **Type one Command line at the Prompt and submit it.** The Prompt is exactly three characters:
   `Ɛ> ` (U+0190 LATIN CAPITAL LETTER OPEN E, `>`, one space) (`src/Xcaciv.Cupcake.Core/Loop.cs:16`,
   read at `:65`). One line per turn; there is no continuation syntax and no multi-line input.
3. **Invoke any registered Command,** case-insensitively: the four Built-in Commands registered
   under package key `Default` (OUT-OF-REPO: `src/Xcaciv.Command/CommandController.cs:175-185`
   (framework v2.1.2)); the two Host-linked Commands the shipping Shell Operator registers under key
   `internal` (`src/Xcaciv.Cupcake.Lit/Program.cs:10-11`); and — *as the intended contract only* —
   every Command contributed by a Plugin that survived discovery (§7.4). **QUIRK (Q-82): the third
   group is always empty on a real filesystem.** The Plugin Scanner builds its search mask by
   joining a wildcard segment, the sub-directory name and the binary pattern, producing
   `*/bin/*.dll`, and hands it to a recursive enumeration rooted at the verified Plugin Directory;
   the directory portion of a mask is joined to the root literally and its wildcard is never
   expanded, so the enumeration raises a directory-not-found condition naming `<root>/*/bin`
   instead of returning files (OUT-OF-REPO: `src/Xcaciv.Command.FileLoader/Crawler.cs:20,173-178`
   (framework v2.1.2); established by execution against a real filesystem with a correct layout in
   place — see §7.4 FR-4.25). No Plugin is ever loaded from disk, so the command set a Shell user
   can actually reach is exactly the six Commands above.
4. **Join stages into a Pipeline** with `|`; all stages run concurrently and the last stage's Output
   is what they see (OUT-OF-REPO: `src/Xcaciv.Command/PipelineParser.cs:25-90`,
   `src/Xcaciv.Command/PipelineExecutor.cs:66-108` (framework v2.1.2)).
5. **Ask for help.** The bare word `HELP` lists every registered Command, one line each; an argument
   `--HELP` turns any invocation into a help request (OUT-OF-REPO:
   `src/Xcaciv.Command/CommandExecutor.cs:57-73`, `src/Xcaciv.Command/HelpService.cs:165-175`
   (framework v2.1.2)). **QUIRK (Q-44):** of the three help spellings the Command Service accepts,
   only the double-dash one is reachable from the Prompt — argument tokenization reduces a typed
   `-?` to a bare `-` and deletes `/?` entirely (OUT-OF-REPO:
   `src/Xcaciv.Command.Interface/CommandDescription.cs:72`,
   `src/Xcaciv.Command/HelpService.cs:172-174` (framework v2.1.2)). **QUIRK (Q-45), INFERRED:**
   requesting help on a member of a Command Group prints the group's one-line member listing, not
   the member's own parameter help — so `PACKAGE SEARCH --HELP` does not show the search parameters.
6. **Read the three visual channels** the Presentation Adapter paints: Output in Blue on Black,
   Status lines in Yellow on DarkBlue, the Prompt in Green on Black
   (`src/Xcaciv.Cupcake.Core/ConsoleContext.cs:23-30`), plus progress rendered through the template
   `{0} progress {1}%` (`:20`).
7. **Set and read Session Variables** for the life of the Session, through the built-in variable
   setter and variable dump (OUT-OF-REPO: `src/Xcaciv.Command/Commands/SetCommand.cs:12`,
   `src/Xcaciv.Command/Commands/EnvCommand.cs:12` (framework v2.1.2)). The values vanish when the
   process ends (§3.6).
8. **End the Session** by typing `END`, `EXIT` or `BYEE` — note the doubled final E in the third — as
   the whole line, in any case (`src/Xcaciv.Cupcake.Core/Loop.cs:20`, matched at `:57`). **QUIRK
   (Q-21):** the exit test runs at the top of each iteration, *before* dispatch, so an exit word is
   never executed as a Command even if one is registered under that name (`:57-66`).

**What they cannot do.**

1. **Cannot change any Session setting.** The Prompt, the exit vocabulary and the Plugin Directory
   are settable only in code, before the Session starts (`src/Xcaciv.Cupcake.Core/Loop.cs:11-24`),
   and the shipping Shell Operator overrides none of them
   (`src/Xcaciv.Cupcake.Lit/Program.cs:9-12`).
2. **Cannot, in practice, re-point the Registry Endpoint setting** — even though the design offers it
   as the one runtime-changeable setting. The endpoint is read from the Session Variable
   `PackageSourceUrl` (`src/Xcaciv.Command.Packages/SearchCommand.cs:24`), but every typed argument
   passes a character allow-list that deletes `:` and `/`: unquoted, a URL fragments into separate
   tokens; quoted, it collapses to `httpsapi.nuget.orgv3index.json`. Either way the transport gate
   then rejects it with `Insecure or invalid package source URL. HTTPS is required.`
   (`src/Xcaciv.Command.Packages/SearchCommand.cs:32-35`; OUT-OF-REPO:
   `src/Xcaciv.Command.Interface/CommandDescription.cs:20-24,69-79` (framework v2.1.2)). **QUIRK
   (Q-43), INFERRED** — the tokenization and cleansing rules were read from the framework reference and
   replayed against sample input; the product was never run. Only a Shell Operator seeding the store
   in code can change the endpoint (§3.2).
3. **Cannot install a Plugin.** `PACKAGE INSTALL <name>` returns the literal `Not installing `
   followed by the comma-joined parameters (`src/Xcaciv.Command.Packages/InstallCommand.cs:19-22`);
   the piped form returns `Not installing {chunk} ` plus the same joined parameters (`:24-27`).
   Placing a Plugin on disk by hand before startup was the intended delivery path, but as shipped
   it does not work either: **any** Plugin Directory that exists and passes verification is fatal at
   startup, whether it is empty or perfectly populated, because the scan of item 3 raises before it
   can distinguish the two (**QUIRK Q-11 with Q-82**; §7.4 FR-4.25, FR-4.42). No delivery path
   reaches a loaded Plugin today (INFERRED for the hand-placed case: the Shell itself was never run,
   but the enumeration defect was established by execution).
4. **Cannot follow the product's own advice.** The No Plugins Available guidance reads
   ``No Plugins Found. You may want to check out `install --help` ``
   (`src/Xcaciv.Cupcake.Core/Loop.cs:47`), but the Command is reachable only as `PACKAGE INSTALL`,
   so that line names an invocation that cannot resolve. **QUIRK (Q-12).**
5. **Cannot reload or extend the command set during a Session.** Discovery runs once per process
   (`src/Xcaciv.Cupcake.Core/Loop.cs:41-43`); there is no reload Command and no directory watching.
6. **Cannot cancel a running Command.** The Session dispatches through the non-cancellable entry
   (`src/Xcaciv.Cupcake.Core/Loop.cs:62`, `:94`) although the Command Service offers a cancellable
   one (OUT-OF-REPO: `src/Xcaciv.Command.Interface/ICommandController.cs:36,44` (framework v2.1.2)).
7. **Cannot control narration, colour or diagnostics.** Status Visibility is fixed at construction of
   the Presentation Adapter (`src/Xcaciv.Cupcake.Core/ConsoleContext.cs:31`), and Diagnostic trace
   never reaches the terminal at all, because the adapter re-declares the visibility flag instead of
   setting the inherited one that the trace path consults (**QUIRK (Q-27)**, `:31` with OUT-OF-REPO:
   `src/Xcaciv.Command.Core/AbstractTextIo.cs:25,167-176` (framework v2.1.2)).
8. **Cannot be identified, authenticated, or remembered** between Sessions (§3.6).

**Evidence that establishes this actor.** The Session's loop is written around exactly one input
operation — prompt for one line, dispatch it unless it is empty, repeat until an exit word
(`src/Xcaciv.Cupcake.Core/Loop.cs:56-66`) — and the Presentation Adapter implements that operation by
writing the Prompt without a newline and reading one line back
(`src/Xcaciv.Cupcake.Core/ConsoleContext.cs:66-72`). There is no other way into the product.

---

### 3.2 Shell Operator

**Who they are.** A *program*, not a person: whoever constructs a Session, configures it, registers
Host-linked Commands and starts it. The shipping executable plays this role in four statements
(`src/Xcaciv.Cupcake.Lit/Program.cs:9-12`, inside the guard at `:7-19`). The role is a first-class actor because the Session is a
library object with a public settings surface and three entry points, and because the shipping
executable is only one possible caller — the repository's own automated tests are another, driving a
whole Session with substituted collaborators (`Xcaciv.Cupcake.Core.Tests/LoopTests.cs:126-152`).

**What they can do.**

- **Register Host-linked Commands** under a package key of their choosing, by instance — the shipping
  Operator registers both package Commands under the key `internal`
  (`src/Xcaciv.Cupcake.Lit/Program.cs:10-11`). The Command Service also accepts registration by kind
  and by pre-built Command Registration Record (OUT-OF-REPO:
  `src/Xcaciv.Command.Interface/ICommandController.cs:68,76,84` (framework v2.1.2)). **The
  environment-modifying flag is part of that decision and the shipping Operator declines it:** both
  registrations omit the argument, which defaults to off (`src/Xcaciv.Cupcake.Lit/Program.cs:10-11`;
  OUT-OF-REPO: `src/Xcaciv.Command/CommandController.cs:190` (framework v2.1.2)), so every
  Session Variable either Command writes lands in the per-execution child scope the executor seeds
  from the caller's values and is discarded when the Command returns — the merge back into the
  Session's store happens only for a Command whose registration record declares it
  environment-modifying *and* whose child scope changed (OUT-OF-REPO:
  `src/Xcaciv.Command/CommandExecutor.cs:187,216-219`,
  `src/Xcaciv.Command/EnvironmentContext.cs:45-55` (framework v2.1.2)). An Operator that registers
  either Command *with* the flag changes that, and inherits the write-back side effects §7.8
  records — see Q-10.
- **Override the four value settings** before start: the install gate, the Prompt, the exit
  vocabulary and the Plugin Directory, whose default is the relative path `.\packages` — written with
  a backslash separator, which is a literal filename character on POSIX platforms (**QUIRK (Q-25)**)
  (`src/Xcaciv.Cupcake.Core/Loop.cs:11-24`). **That accident is what keeps the shipping build
  usable:** because the default names a directory that does not exist, the nomination is rejected,
  the verified-directory list stays empty, and the load step raises the tolerated No Plugins
  Available condition rather than the fatal one, so startup reaches a Prompt (OUT-OF-REPO:
  `src/Xcaciv.Command/CommandLoader.cs:42-45` (framework v2.1.2);
  `src/Xcaciv.Cupcake.Core/Loop.cs:45-50`). An
  Operator who "fixes" the separator, or points the setting at a directory that genuinely exists,
  converts every startup into a fatal Startup Load Failure (Q-11 with Q-82; §7.4 FR-4.25, FR-4.42,
  FR-4.57). The Prompt and the exit vocabulary are re-read on every iteration, so an Operator holding a reference can change them
  mid-Session (`:57`, `:65`).
- **Choose one of three entry points:** blocking, asynchronous, or the defaults convenience
  (`src/Xcaciv.Cupcake.Core/Loop.cs:32`, `:70`, `:105`). The choice is behavioural, not stylistic —
  **QUIRK (Q-17):** the asynchronous entry registers no Built-in Commands and has no benign branch
  for No Plugins Available, so a fresh install started that way fails startup outright with the
  generic Startup Load Failure (`:39-54` versus `:77-85`).
- **Supply their own Interaction Context, Command Service and Session Variables store** to either run
  entry (`src/Xcaciv.Cupcake.Core/Loop.cs:32`, `:70`).
- **Construct the Presentation Adapter themselves** and set its six colours, its progress template and
  its Status Visibility (`src/Xcaciv.Cupcake.Core/ConsoleContext.cs:13`, `:20-31`). The defaults entry
  instead builds one named `Cupcake Console Context` with an empty parameter list and visibility left
  on (`src/Xcaciv.Cupcake.Core/Loop.cs:110`).
- **Decide how a Startup Load Failure is reported.** The condition carries the fixed summary
  `Unable to load commands.` plus the underlying cause (`src/Xcaciv.Cupcake.Core/Loop.cs:53`, `:84`);
  the shipping Operator writes one line, `Error {message}`, and terminates with exit code `1`
  (`src/Xcaciv.Cupcake.Lit/Program.cs:14-19`).
- **Seed Session Variables in code** before start — the only workable way to re-point the Registry
  Endpoint, given §3.1's tokenization defect.

**What they cannot do.**

- **Cannot assign the Command Service or the Session Variables store as settings.** Both are exposed
  read-only and are replaced only by what is passed to a run entry
  (`src/Xcaciv.Cupcake.Core/Loop.cs:25-26`, `:34-35`, `:72-73`). **QUIRK (Q-16):** the defaults entry
  registers the Built-in Commands onto the Session's own default Command Service and then hands that
  same service to the blocking entry, which registers them a second time; registration overwrites by
  key, so the duplication is harmless but real (`:108`, `:41`).
- **Cannot set the containment boundary for the Plugin Directory through the Session.** Nothing in
  the product calls a boundary setter, and the Session's default Command Service is built with the
  no-argument construction form (`src/Xcaciv.Cupcake.Core/Loop.cs:25`), so the boundary is derived
  from the process working directory (OUT-OF-REPO:
  `src/Xcaciv.Command.FileLoader/VerifiedSourceDirectories.cs:17`, `:105` (framework v2.1.2)). An
  Operator that constructs its own Command Service could set one (OUT-OF-REPO:
  `src/Xcaciv.Command/CommandController.cs:64-72` (framework v2.1.2)), but the contract the Session
  accepts exposes no such member.
- **Cannot learn that a nominated Plugin Directory was rejected.** The add call reports failure by
  return value and the Session discards it (`src/Xcaciv.Cupcake.Core/Loop.cs:42`; OUT-OF-REPO:
  `src/Xcaciv.Command.FileLoader/VerifiedSourceDirectories.cs:82-89` (framework v2.1.2)). Rejection
  is also, as shipped, the *only* outcome that leaves a usable Prompt behind — a nomination that
  succeeds sends startup into the scan that always raises (Q-82) — so the Operator cannot tell the
  one working configuration from the fatal one until the process either prompts or exits `1`.
  **INFERRED containment rule (§7.4), QUIRK (Q-14):** the candidate directory is canonicalised to an
  absolute path and its
  *parent* is tested against a boundary that defaults to the process working directory, using a
  containment test that ignores the boundary's own final segment. The effective boundary is therefore
  one level *above* the working directory: sibling trees are accepted and the working directory
  itself is rejected (OUT-OF-REPO:
  `src/Xcaciv.Command.FileLoader/VerifiedSourceDirectories.cs:100-108` (framework v2.1.2); the
  containment primitive's behaviour was confirmed by executing it against these exact path shapes,
  but the product itself was never run).
- **Cannot gate the Install Command**, despite a Session setting named for it: nothing reads it, and
  the declaration is the name's only occurrence in the product (**QUIRK (Q-24)**,
  `src/Xcaciv.Cupcake.Core/Loop.cs:11`; keep-or-fix decision at OQ-25). Do not reimplement it as a
  permission (§3.6).
- **Cannot pass anything in from the command line.** The shipping Operator reads no arguments, so
  there is no path from the launcher into the Session's settings
  (`src/Xcaciv.Cupcake.Lit/Program.cs:7-13`).
- **Cannot install an audit sink, an output encoder, resource limits or a settings file** — see the
  unused-hook table in §3.6.

**Build-time face — the release engineer.** The same actor at an earlier moment, and the source
treats it as one: the person who builds the delivered artefact owns the build profile, the pinned
dependency versions and the rule mapping upstream feeds to package names. That profile is the entire
distribution specification — the repository contains no installer, no packaging script and no
continuous-integration definition. **QUIRK (Q-51, with Q-52 and Q-53):** the shipping profile changes
platform, not just optimisation — the development profile targets a cross-platform console
application, the shipping
profile a windowed-subsystem, single-file, self-contained, trimmed 64-bit Windows binary
(`src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj:4-24`). **QUIRK (Q-54), INFERRED:** single-file
packaging is expected to break Host-linked Commands, because such a Command is registered with its own module's
file location and instantiated by loading that path, which is empty inside a single-file bundle
(OUT-OF-REPO: `src/Xcaciv.Command/CommandFactory.cs:64-78`,
`src/Xcaciv.Command/CommandRegistry.cs:49-53` (framework v2.1.2)). This actor also owns the reason
the product does not build at the pinned commit (§2.4): the feed claiming all first-party package
names is an unexpanded environment token, the private hosted feed is declared but given no routing
rule and so is never consulted, and the framework packages are absent from the public index
(**QUIRK (Q-55)**, `NuGet.config:5-22`).

---

### 3.3 Plugin author

**Who they are.** A third party who will never see the Shell's source, who writes a Command bundle
and publishes it to a Package Registry, and who expects a user's Shell to pick it up — an
expectation the shipped build never meets, because the disk scan cannot match anything on a real
filesystem (Q-82; §7.4 FR-4.25) and install is inert (Q-46). **Everything in this subsection is
therefore the authoring contract a clone must implement, not behaviour a Plugin author can obtain
from the reference implementation.** No Plugin exists in the repository, so the actor is established indirectly but decisively by three things:
the entire discovery and isolated-loading phase exists to serve nobody else (§7.4); both in-repo
Commands satisfy exactly the authoring contract a Plugin must satisfy
(`src/Xcaciv.Command.Packages/SearchCommand.cs:11-20,88`,
`src/Xcaciv.Command.Packages/InstallCommand.cs:14-27`); and the product's own first-run message tells
the user to go and get what this actor publishes (`src/Xcaciv.Cupcake.Core/Loop.cs:47`).

**What they can do.**

- **Declare a Command's metadata** — invocation name and description, optionally a Command Group, a
  version, a usage prototype and a short alias — and get help generated from it with no extra code
  (OUT-OF-REPO: `src/Xcaciv.Command.Interface/Attributes/CommandRegisterAttribute.cs:27-51`,
  `src/Xcaciv.Command.Core/AbstractCommand.cs:62-146` (framework v2.1.2)).
- **Declare Positional Parameters, Named Parameters (with defaults and allowed-value lists) and
  Flags,** and get binding and allowed-value enforcement supplied (OUT-OF-REPO:
  `src/Xcaciv.Command.Core/CommandParameters.cs:12-171` (framework v2.1.2)).
- **Implement the two invocation paths** — Direct invocation and Per-chunk invocation — so the same
  Command works standalone and as a Pipeline stage
  (`src/Xcaciv.Command.Packages/SearchCommand.cs:20`, `:88`).
- **Emit Output, Status lines, progress readings and Diagnostic trace** through the Interaction
  Context (OUT-OF-REPO: `src/Xcaciv.Command.Core/AbstractTextIo.cs:67-73,132,138,167-176`
  (framework v2.1.2)) — or, as both in-repo Commands do, simply return text and let the Command Service relay it
  (OUT-OF-REPO: `src/Xcaciv.Command/CommandExecutor.cs:196-200` (framework v2.1.2)).
- **Opt into changing Session Variables,** if the Command is registered as environment-modifying and
  its child store actually changed. Every execution runs against a child scope seeded with a copy of
  the caller's values; without the flag that child is discarded on return, so a Command that is not
  registered as environment-modifying cannot alter the Session's store at all (OUT-OF-REPO:
  `src/Xcaciv.Command/CommandExecutor.cs:187,216-219`,
  `src/Xcaciv.Command/EnvironmentContext.cs:45-55` (framework v2.1.2); §3.2, §6.3).
- **Lay the bundle out for discovery:** one folder per Plugin beneath the Plugin Directory, with the
  loadable payload in a `bin` sub-folder, searched recursively (OUT-OF-REPO:
  `src/Xcaciv.Command.FileLoader/Crawler.cs:20,173-178` (framework v2.1.2)). **This is the intended
  layout only.** The mask the scanner actually composes for that layout — `*/bin/*.dll` — has its
  directory portion joined to the root literally, so a correct bundle is never found: the
  enumeration raises a directory-not-found condition naming `<root>/*/bin`, and its mere presence
  makes the Shell fail at startup (**QUIRK Q-82 with Q-11**; §7.4 FR-4.25, FR-4.42, verified by
  execution against a real filesystem). A clone must implement segment-spanning matching itself for
  this layout to mean anything.
- **Publish to a Package Registry** so Package Search surfaces them. The fields a full-detail Package
  Listing renders — download count, publication date, authors, licence and known-vulnerability count
  — are this actor's published metadata (`src/Xcaciv.Command.Packages/SearchCommand.cs:72-77`).

**What they cannot do.**

- **Cannot get their Plugin installed by the product.** Install is inert
  (`src/Xcaciv.Command.Packages/InstallCommand.cs:19-27`), and the unreferenced install routine that
  does exist downloads the archive and creates a versioned directory but never extracts it and never
  resolves dependencies — and the directory shape it creates does not match the layout the scanner
  looks for (`src/Xcaciv.Command.Packages/NugetWrapper.cs:111-128`). **QUIRK (Q-46 with Q-77).**
- **Cannot get their Plugin loaded even by hand.** Hand-placing a correct bundle does not rescue the
  path: the scan raises on the mask before any candidate is returned, and the resulting condition is
  fatal to startup rather than benign (**QUIRK Q-82 with Q-11**; §7.4 FR-4.25, FR-4.42). The
  per-Plugin load machinery below is therefore unreachable as shipped and is documented as the
  contract a clone must honour.
- **Cannot be told why their Plugin did not appear.** Any per-Plugin load failure — security
  violation, missing file, unreadable or wrong-architecture binary, anything — is swallowed to the
  Diagnostic trace and that Plugin is skipped; a Plugin contributing no valid Command is dropped the
  same way (**QUIRK (Q-15)**, OUT-OF-REPO: `src/Xcaciv.Command.FileLoader/Crawler.cs:81-156`
  (framework v2.1.2)). Diagnostic trace never reaches the terminal (§3.1), so the user sees no
  difference between a broken Plugin and an absent one — and as shipped no difference at all, since
  a broken bundle, a perfect bundle and an empty Plugin Directory all produce the identical fatal
  `Error Unable to load commands.` and exit status `1` (Q-82, Q-11).
- **Cannot rely on their declared environment-modifying intent.** The flag is recorded on the Command
  Group, not the member, so the first registration of a group fixes write-back for every member that
  later joins it (**QUIRK (Q-62)**, OUT-OF-REPO: `src/Xcaciv.Command/CommandRegistry.cs:24-35,55-60`,
  `src/Xcaciv.Command.Core/CommandParameters.cs:179-209` (framework v2.1.2)).
- **Cannot claim a name safely.** Registration overwrites by key, so a later registration of the same
  invocation name silently replaces an earlier one — including one held by a Built-in or Host-linked
  Command, with no warning and no trace line (**QUIRK (Q-71)**, OUT-OF-REPO:
  `src/Xcaciv.Command/CommandRegistry.cs:20-36` (framework v2.1.2)).
- **Cannot escape the per-Plugin sandbox.** Each Plugin is instantiated in an isolated loader
  confined to its own directory, under a policy forced to its strict setting when reflection-emit is
  disallowed (OUT-OF-REPO: `src/Xcaciv.Command/CommandFactory.cs:80-113` (framework v2.1.2)).
- **Cannot be verified, and is never asked to be.** There is no signature check, no manifest
  verification, no publisher allow-list and no integrity check on a downloaded archive; a download
  overwrites its target unconditionally and the identity of what was fetched is read from the
  delivered archive rather than from the request
  (`src/Xcaciv.Command.Packages/NugetWrapper.cs:89`, `:103-108`, `:118`). **QUIRK (Q-48).** The
  isolation policy above is the only control aimed at this actor.
- **Cannot count on being loaded at all in the shipping build** — see the trimming hazard in §3.2.

---

### 3.4 Package Registry

**Who they are.** The external service that indexes Plugins and serves their metadata and archives —
an actor rather than a mere dependency, because it is the only party outside the process that the
product initiates conversation with, and because its answers are rendered to the Shell user
essentially verbatim. The product names one by default, `https://api.nuget.org/v3/index.json`
(`src/Xcaciv.Command.Packages/SearchCommand.cs:28`), and will talk to whatever the Registry Endpoint
setting names, provided the value parses as an absolute address whose scheme is `https`.

**What the product asks of it.**

| Operation asked of the registry | Reachable from the Prompt? | Evidence |
|---|---|---|
| Keyword search, from a fixed start offset of 0, up to a result limit | **Yes** — via `PACKAGE SEARCH` | `src/Xcaciv.Command.Packages/NugetWrapper.cs:26-31`, called at `SearchCommand.cs:60` |
| Enumerate every version of a named package | No — nothing in the product calls it | `src/Xcaciv.Command.Packages/NugetWrapper.cs:34-50` |
| Resolve one version's dependency set for a target runtime | No — nothing in the product calls it | `:52-73` |
| Stream a Package Archive into a named file | No — reached only from the unreferenced install routine | `:81-101`, called at `:111-128` |

**What it must supply.** For a Package Listing: identity (name and version) and a summary at the
standard detail level; additionally the download count, publication date, authors, licence and
known-vulnerability count at full detail
(`src/Xcaciv.Command.Packages/SearchCommand.cs:66-77`).

**Constraints the product places on it.** Transport must be `https`, checked before any network call
and rejected with `Insecure or invalid package source URL. HTTPS is required.` (`:32-35`); the result
count is clamped to `[1, 100]` from a declared default of `20` (`:15`, `:46`); the search term is
trimmed, short-circuited when empty, and silently truncated at `200` characters (`:50-58`).

**What it cannot do, and is never asked to do.**

- **Cannot authenticate the product, and is never authenticated by it.** No credential of any kind is
  attached to any request — a source is constructed from a URL and nothing else
  (`src/Xcaciv.Command.Packages/NugetWrapper.cs:20-101`); a repository-wide search for credential-,
  token-, password- or login-shaped identifiers matches nothing but an output label and cancellation
  parameters. Anonymous access is assumed everywhere.
- **Is never verified beyond its transport scheme.** No host allow-list, no certificate pinning, no
  thumbprint check — the whole gate is four lines
  (`src/Xcaciv.Command.Packages/SearchCommand.cs:32-35`).
- **Is trusted for its own answers.** Nothing checks a checksum or signature, and the identity of a
  downloaded Plugin is read from the artefact the registry delivered
  (`src/Xcaciv.Command.Packages/NugetWrapper.cs:118`). **QUIRK (Q-48).**
- **Cannot push, notify or call back.** Every exchange is initiated by the product, inside one
  Command execution, and completed synchronously before that Command returns (`SearchCommand.cs:60`).
- **Prerelease inclusion is not the user's choice to give it.** The flag that would ask the registry
  to exclude unreleased versions is tested for *presence* in the bound parameters, and a declared
  flag is always present, so every search that carries at least one argument asks for prereleases
  (**QUIRK (Q-1)**, `src/Xcaciv.Command.Packages/SearchCommand.cs:47` with OUT-OF-REPO:
  `src/Xcaciv.Command.Core/CommandParameters.cs:34` (framework v2.1.2); keep-or-fix at OQ-4).

A second registry-shaped external system exists at *build* time — the dependency feeds — but it is
owned by the release-engineer face of §3.2 and never contacted by the running product.

---

### 3.5 Automated consumer

**Not a persona — a contract.** The source supports being driven by something other than a human in
exactly three narrow ways, and supports scheduled or unattended operation in none. Both halves matter
to a reimplementer: keep the first three, invent nothing for the fourth.

**Supported.**

1. **A launching process reading the exit status.** Exactly two outcomes exist: normal termination
   when the Session ends, and exit code `1` when anything escapes
   (`src/Xcaciv.Cupcake.Lit/Program.cs:14-19`). **QUIRK (Q-38):** the accompanying line
   `Error {message}` is written to standard output rather than an error stream (`:16`), so a caller
   capturing only the error stream gets a failure code with no text, and a caller redirecting output
   loses the only diagnosis into the same stream as normal results.
2. **In-process direct invocation of a Command.** A host or test harness can call a Command's
   Direct-invocation path with an argument array, bypassing tokenization entirely — which is exactly
   how the repository's own tests drive Package Search
   (`Xcaciv.Command.PackagesTests/SearchCommandTests.cs:13-18`). This path escapes the `:` and `/`
   deletion described in §3.1, so it is the only way a URL-bearing argument reaches a Command intact.
3. **A substituted Interaction Context driving a whole Session.** The Session touches the terminal
   through only three operations — show a Status line, emit one Output chunk, prompt for one line —
   so a stand-in surface can run a Session end to end with no terminal at all
   (`Xcaciv.Cupcake.Core.Tests/LoopTests.cs:8-65,126-152`).

**Not supported — do not build these.**

- **No scheduled, unattended or batch mode.** No argument parsing, no script-file input, no
  run-one-command-and-exit, no non-interactive switch: the entry point reads no arguments and the
  Session's only input source is a single prompt-for-one-line call
  (`src/Xcaciv.Cupcake.Lit/Program.cs:7-13`; `src/Xcaciv.Cupcake.Core/Loop.cs:65`, `:96`). This is a
  recorded non-goal (§2.2 NG-3), not an unresolved question: §11 carries no open question about
  introducing such a mode, only the hang that redirected input produces today (Q-22). A clone that
  wants one is adding a feature, and should record that as a deliberate extension.
- **No end-of-input concept.** Redirected input runs its lines, then the Presentation Adapter
  converts "nothing left" into the empty string (`src/Xcaciv.Cupcake.Core/ConsoleContext.cs:71`),
  which the Session treats as a blank line and prompts again — forever. Feeding a finite stream
  without a trailing exit word produces a non-terminating prompt loop. **QUIRK (Q-22), INFERRED**
  (reasoned from the emptiness guard at `src/Xcaciv.Cupcake.Core/Loop.cs:60` plus the empty-string
  substitution; no test covers it).
- **No machine-readable output.** Every result is colour-painted human text; there is no structured
  output mode, and the Command Service's output-encoding hook is left at its no-op default
  (**QUIRK (Q-33)**, §3.6).
- **No fail-fast on a Command error.** A Command failure is caught by the Command Service, reported as
  text, and the Session continues, so a caller cannot infer per-command success from the exit status
  (OUT-OF-REPO: `src/Xcaciv.Command/CommandExecutor.cs:224-231` (framework v2.1.2)).

---

### 3.6 Roles and permissions that do NOT exist

**The product has no authentication, no authorization, no user accounts, no roles or groups, no
multi-tenancy, no session persistence and no audit trail.** None of these is a disabled feature
awaiting a switch: with the single exception of the audit hook noted below, the concepts are absent
from the source entirely — no identity is ever created, stored, compared or displayed anywhere in the
27 tracked files, and nothing gates any Command, setting or Session Variable on any property of the
caller. This is the part of the specification a reimplementer is most likely to violate out of good
intentions, because a shell that downloads third-party code and loads it into its own process invites
an access-control design. Adding one would change the product. Each absence is recorded below with
the evidence that establishes it, so that introducing any of them becomes a deliberate, documented
decision rather than a silent one. Two of these absences carry an explicit keep-or-add question in
§11.1 — the audit trail at **OQ-29** and the install gate at **OQ-25** — and the rest are recorded
here as evidenced absences rather than as open questions: adding any of them is a decision the
commissioning team must take deliberately, not a gap §11 is waiting to fill.

| Absent concept | Evidence of absence |
|---|---|
| **Authentication** | No credential, secret, token, password or login concept exists in the tracked source; requests to the Package Registry are constructed from a URL and nothing else (`src/Xcaciv.Command.Packages/NugetWrapper.cs:20-101`). The only credential-shaped declaration in the repository is a build-time feed entry that the running product never reads (`NuGet.config:5-22`). |
| **Authorization** | Nothing consults any property of the caller before dispatching a Command. Every registered Command — Built-in, Host-linked or Plugin-supplied — is invocable by whoever reaches the Prompt (`src/Xcaciv.Cupcake.Core/Loop.cs:56-66`). |
| **A permission gate that looks like one but is not** | The Session exposes a boolean setting named for the Install Command; nothing anywhere reads it, and its declaration is the only occurrence of the name (`src/Xcaciv.Cupcake.Core/Loop.cs:11`). **QUIRK (Q-24)**, keep-or-fix at OQ-25 — do not reimplement it as a permission. |
| **User accounts / identity** | The Session has no user field, no login step and no identity parameter; Session Variables are keyed text with no owner (OUT-OF-REPO: `src/Xcaciv.Command/EnvironmentContext.cs:70-113` (framework v2.1.2)). |
| **Roles / groups** | The only privilege-shaped bit in the system is per-Command, not per-caller: a Command Registration Record records whether that Command may write Session Variables back to the Session (OUT-OF-REPO: `src/Xcaciv.Command/CommandRegistry.cs:24-35`, `src/Xcaciv.Command/CommandExecutor.cs:216-219` (framework v2.1.2)). It describes what a Command may do, never who may run it. Do not repurpose it. |
| **Multi-tenancy** | One Session per process, one in-memory command registry, one Session Variables store, no isolation because there is never more than one occupant (`src/Xcaciv.Cupcake.Core/Loop.cs:25-26`, `:32-35`). |
| **Session persistence** | The Session Variables store starts empty on every run and nothing writes it back (OUT-OF-REPO: `src/Xcaciv.Command/EnvironmentContext.cs:18,33` (framework v2.1.2)). There is no command history, no state file, no settings file, no cache directory and no dot-file anywhere in the 27 tracked files, and nothing reads a configuration file or an operating-system environment value at startup. The only on-disk write the product can perform is the unreferenced install routine's download (`src/Xcaciv.Command.Packages/NugetWrapper.cs:89`). |
| **Audit trail** | The Command Service defines an audit-logging contract and installs a discard-everything implementation by default; the product constructs the Command Service with the no-argument form (`src/Xcaciv.Cupcake.Core/Loop.cs:25`) and never assigns a logger, so no execution, no failure and no Session-Variable change is recorded anywhere (OUT-OF-REPO: `src/Xcaciv.Command.Interface/IAuditLogger.cs:9-55`, `src/Xcaciv.Command/NoOpAuditLogger.cs:10-24`, `src/Xcaciv.Command/CommandController.cs:107` (framework v2.1.2)); the keep-or-add decision is OQ-29. |
| **Artefact trust** | No signature verification, no checksum, no publisher allow-list; the transport gate checks the scheme and nothing else (`src/Xcaciv.Command.Packages/SearchCommand.cs:32-35`; §3.4). |

**Hooks the framework offers that the product never uses.** These matter because a reimplementer
reading the framework's surface will find capabilities the product declines, and must not mistake
availability for use. Every row rests on OUT-OF-REPO evidence at tag v2.1.2, one patch release ahead
of the versions the product pins, so the *existence* of each hook at the pinned version is not
confirmed; what is confirmed in-repo is that the product calls nothing of the sort.

| Hook offered by the Command Service | What the product inherits instead | Evidence |
|---|---|---|
| Audit logging, complete with a redaction policy that replaces parameters whose names look like secrets with `[REDACTED]` | a logger that discards everything | OUT-OF-REPO: `src/Xcaciv.Command.Interface/AuditMaskingConfiguration.cs:11-45`, `src/Xcaciv.Command/CommandController.cs:107,128-136` (framework v2.1.2) |
| An explicit containment boundary for the Plugin Directory — settable both as a construction argument and as a settings key | a boundary derived from the process working directory, with the ignore-final-segment behaviour described in §3.2 | OUT-OF-REPO: `src/Xcaciv.Command/CommandController.cs:64-72`, `src/Xcaciv.Command.Interface/CommandControllerOptions.cs:31-35`, `src/Xcaciv.Command.FileLoader/VerifiedSourceDirectories.cs:17,105,120` (framework v2.1.2) |
| Binding the whole controller surface — help keyword, built-in registration, package directories, boundary, narration — from a settings file section | compiled-in defaults only; the product has no settings file | OUT-OF-REPO: `src/Xcaciv.Command.Interface/CommandControllerOptions.cs:9-41` (framework v2.1.2) |
| Cancellable dispatch of a Command line | the non-cancellable entry, so nothing can interrupt a running Command | OUT-OF-REPO: `src/Xcaciv.Command.Interface/ICommandController.cs:36,44` (framework v2.1.2); `src/Xcaciv.Cupcake.Core/Loop.cs:62`, `:94` |
| Output encoding for a target system | a no-op encoder | OUT-OF-REPO: `src/Xcaciv.Command/CommandController.cs:108,141-145` (framework v2.1.2) |
| A configurable help keyword | left at `HELP` | OUT-OF-REPO: `src/Xcaciv.Command/CommandController.cs:36,114-122` (framework v2.1.2) |
| Pipeline resource limits — whole-pipeline timeout, per-stage timeout, per-stage output byte cap, per-stage output item cap | every limit left at its "disabled" default, with a channel capacity of 10,000 items and a blocking back-pressure policy | OUT-OF-REPO: `src/Xcaciv.Command.Interface/PipelineConfiguration.cs:15-51`, `src/Xcaciv.Command/CommandController.cs:150` (framework v2.1.2) |

---

### 3.7 Actor → feature map

Legend: **●** direct — the actor acts on the feature or the feature exists to serve them;
**○** indirect — the actor observes the feature's effects, or reaches it only through another
feature; **—** no relationship in the source.

| Actor | 7.1 | 7.2 | 7.3 | 7.4 | 7.5 | 7.6 | 7.7 | 7.8 | 7.9 | 7.10 | 7.11 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **3.1 Shell user** | ● | ○ | ● | ○ | ● | ● | ○ | ● | ● | ● | ● |
| **3.2 Shell Operator** | ● | ● | ● | ● | ● | ● | ○ | ● | ● | ● | ● |
| **3.3 Plugin author** | ● | ○ | ● | ● | ○ | ● | — | ● | ● | ● | ○ |
| **3.4 Package Registry** | — | ○ | — | ○ | — | ○ | ● | ● | ○ | ● | — |
| **3.5 Automated consumer** | ○ | — | ○ | — | ○ | ● | ○ | ○ | — | — | ● |

**Reading the direct cells.**

- **Shell user** — 7.1 every typed line is tokenized, resolved and dispatched by the required
  capability, and its help, not-found and error text is what they read; 7.3 the whole terminal face;
  7.5 the Session is the product from their seat; 7.6 they read the No Plugins Available guidance,
  the crash line and per-Command error text; 7.8 and 7.9 the two Commands they can type; 7.10 they
  feel every clamp, rejection and silent truncation; 7.11 they launch the delivered artefact. The
  ○ at 7.2 is deliberate: they consume every setting and can change none of them in practice (§3.1).
- **Shell Operator** — the only actor with a ● at 7.2, because the four value settings and every
  presentation setting are theirs alone; 7.4 they nominate the Plugin Directory and register
  Host-linked Commands; 7.6 they receive the Startup Load Failure and choose the reporting; 7.11 they
  *are* the entry point, and their build-time face owns the profile and the feed rules.
- **Plugin author** — 7.1 they implement the command-unit half of the contract; 7.3 their Command
  emits through the Interaction Context; 7.4 their bundle is what discovery is for — and what
  discovery, as shipped, can never find; 7.6 the *existence* of a Plugin Directory is what turns a
  benign startup into a fatal one, whether the bundle inside it is broken, perfect or absent
  (Q-82 with Q-11), and any load failure that would have been reported is what gets silently
  swallowed (Q-15); 7.8 their published metadata is what a Package Listing shows; 7.9 their bundle
  is what install would place, if install worked; 7.10 they are the untrusted supply the isolation
  policy is aimed at.
- **Package Registry** — 7.7 it is the client's sole counterparty; 7.8 it answers the only query the
  product can actually make; 7.10 it is the second of the three trust boundaries.
- **Automated consumer** — 7.6 the two exit statuses are its entire contract; 7.11 it consumes the
  delivered artefact's process behaviour and nothing else.

**Handed to §11, and where each lands.** Three of this section's actor-level decisions are carried in
the §11.1 open-question register and are settled there, not here:

- **OQ-29** — whether the clone should wire up an audit trail, given that the required capability
  offers a fully-specified hook with a redaction policy that the product leaves at its no-op default
  (§3.6; §2.2 NG-9).
- **OQ-25** — whether the never-read install gate of §3.2 should become a real permission, be
  removed, or be kept inert (quirk **Q-24**).
- **OQ-7** — whether the Plugin mechanism was ever expected to work, and which first-run experience
  is intended, now that *any* existing Plugin Directory is fatal and a missing one is the only path
  to a Prompt (§3.1, §3.2, §3.3; quirks **Q-82**, **Q-11**).

Two further decisions this section raises are **keep-or-fix** entries in the §11.2 quirk register
rather than open questions, and are recorded there with a recommended disposition:

- **Q-43** — whether the endpoint immutability that falls out of argument cleansing (§3.1) is kept
  as accidental hardening or fixed. The register recommends fixing it; note that fixing it widens
  the supply-chain surface, which is the same trade-off OQ-6 records for the inert registry-source
  option.
- **Q-14** — whether the containment boundary of §3.2 should be tightened to the working directory,
  which is what its name implies but not what it does. OQ-27 asks the adjacent unanswered question,
  whether that boundary survives symbolic links, junctions and network-share paths at all.

**One thing this section deliberately does not hand to §11:** a non-interactive or batch mode. §3.5
records its absence as an evidenced non-goal (§2.2 NG-3), and §11 carries no question about
introducing one — only **Q-22**, the hang that redirected input produces today. A clone that wants a
batch mode is adding a feature to the product, and must record it as an extension rather than as a
resolution of anything asked here.
