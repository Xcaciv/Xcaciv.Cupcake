# Feature Inventory — Xcaciv.Cupcake

> Source (read-only): `/mnt/g/reversing/reversing/subject/Xcaciv.Cupcake`
> Pinned commit: `a05dc6f00d4510ac073f6dd02fbfba2ffb76a5b6` (branch `main`, authored 2025-12-26)
> Upstream: https://github.com/Xcaciv/Xcaciv.Cupcake · License: BSD 3-Clause (© 2022 Alton Crossley)
> Surveyed: 2026-08-31 · 25 commits total · 12 source files, 949 lines of C#

## What the product is

"A sweet shell" (README). Xcaciv.Cupcake is a **host application for an extensible,
plugin-driven command shell**. The repository does not implement command parsing,
piping, or the command registry itself — those come from an external command-framework
dependency. What this repository contributes is: the interactive read-eval-print
session, a colour-aware console presentation adapter, a package-manager command set
(search / install) that pulls shell plugins from a package registry, the shell's
startup/plugin-load sequence and error handling, and the shippable executables.

## Conventions note (given verbatim to every analysis sub-agent)

- **Language / platform**: C# on .NET 8 (`net8.0`) for all projects; one Release
  configuration retargets `net6.0-windows`. Nullable reference types and implicit
  usings enabled everywhere.
- **Layout**: production code under `src/<Project>/`; test projects are siblings of
  `src/` at the repo root (`Xcaciv.Cupcake.Core.Tests/`, `Xcaciv.Command.PackagesTests/`).
- **Projects**: `Xcaciv.Cupcake.Core` (session loop + console adapter + exception),
  `Xcaciv.Command.Packages` (package-manager commands + registry client),
  `Xcaciv.Cupcake.Lit` (shipping executable), `Xcaciv.Cupcake` (empty scaffold executable).
- **Dependency management**: NuGet Central Package Management — versions are pinned in
  `Directory.Packages.props` (root and `src/`), never in the `.csproj` files.
- **Framework dependency**: the shell is built on `Xcaciv.Command` 2.1.1 /
  `Xcaciv.Command.Core` 2.1.0 / `Xcaciv.Command.Interface` 2.1.0 — an external,
  separately-versioned command framework by the same author. It supplies the command
  controller, the command/IO/environment context contracts, the parameter-declaration
  attributes, command-line parsing, `|` pipelines, auto-generated help, plugin
  assembly loading, and the built-in `SAY`/`SET`/`ENV`/`REGIF` commands.
  **This package is NOT resolvable from the public NuGet feed** (nuget.org returns 404);
  it is published to a private GitHub Packages feed. Reference source for the pinned
  major version was read from the public repo at tag `v2.1.2`
  (commit `f34dedca8dc6d690290b2139acbd3e9b8264349c`) — cite that as OUT-OF-REPO evidence,
  distinct from in-repo evidence.
- **Command authoring idiom**: a command is a class deriving from the framework's
  `AbstractCommand`, decorated with declarative attributes (`CommandRoot`,
  `CommandRegister`, `CommandParameterOrdered`, `CommandParameterNamed`, `CommandFlag`)
  that declare the command's name, grouping, parameters, defaults, allowed values and
  help text. It overrides a synchronous execution method and a piped-chunk method.
- **IO idiom**: presentation is an implementation of the framework's IO-context contract
  (`AbstractTextIo` base). The session loop never touches the console directly — it
  talks only to the IO context, which is why the session is testable with a fake.
- **Async idiom**: the framework contract is async throughout; the "Lit" shell
  deliberately consumes it synchronously by blocking (`.Wait()` / `.Result`).
- **Test idiom**: xUnit. `Xcaciv.Cupcake.Core.Tests` uses hand-written fakes of the three
  framework contracts (IO, controller, environment); `Xcaciv.Command.PackagesTests`
  contains **live integration tests that hit the public nuget.org API over the network**.
- **In-repo evidence for the framework contract**: `Xcaciv.Cupcake.Core.Tests/LoopTests.cs`
  lines 8–122 hand-implement the full IO / controller / environment contracts, so the
  required member surface is directly observable inside the subject repo.

## Churn / vitality

Hot files by commit touch count: `src/Directory.Packages.props` (8),
`src/Xcaciv.Cupcake.Core/Loop.cs` (7), `src/Xcaciv.Command.Packages/SearchCommand.cs` (7),
`NuGet.config` (7). The living heart is the package-search command and the session loop;
dependency-version churn dominates everything else. The whole `src/Xcaciv.Cupcake/`
executable is vestigial (a two-line "Hello, World!" scaffold, untouched since the
directory restructure).

## Features

| # | Name | Kind | Evidence paths | Depends on | Complexity |
|---|---|---|---|---|---|
| F1 | Interactive Shell Session | user-facing feature | `src/Xcaciv.Cupcake.Core/Loop.cs`; `Xcaciv.Cupcake.Core.Tests/LoopTests.cs` | F2, F3, F9, F10 | M |
| F2 | Console Presentation & Interaction | user-facing feature | `src/Xcaciv.Cupcake.Core/ConsoleContext.cs`; `Xcaciv.Cupcake.Core.Tests/ConsoleContextTests.cs` | F10 | M |
| F3 | Plugin Discovery & Command Registration | platform capability | `src/Xcaciv.Cupcake.Core/Loop.cs:37-54,105-112`; `src/Xcaciv.Cupcake.Lit/Program.cs:9-12` | F10 | M |
| F4 | Package Search Command | user-facing feature | `src/Xcaciv.Command.Packages/SearchCommand.cs`; `Xcaciv.Command.PackagesTests/SearchCommandTests.cs` | F6, F8, F10 | L |
| F5 | Package Install Command | user-facing feature (incomplete) | `src/Xcaciv.Command.Packages/InstallCommand.cs`; `src/Xcaciv.Command.Packages/NugetWrapper.cs:103-128` | F6, F10 | M |
| F6 | Package Registry Client | integration | `src/Xcaciv.Command.Packages/NugetWrapper.cs`; `Xcaciv.Command.PackagesTests/NugetWrapperTests.cs` | — | L |
| F7 | Shell Distribution & Entry Points | platform capability | `src/Xcaciv.Cupcake.Lit/*`; `src/Xcaciv.Cupcake/*`; `Xcaciv.Cupcake.sln` | F1, F3 | M |
| F8 | Configuration & Settings | cross-cutting | `Directory.Packages.props`; `src/Directory.Packages.props`; `NuGet.config`; `Loop.cs:11-26`; `SearchCommand.cs:24-29` | F10 | M |
| F9 | Error Handling & Failure Reporting | cross-cutting | `src/Xcaciv.Cupcake.Core/Exceptions/LoadingException.cs`; `Loop.cs:39-54,77-85`; `Lit/Program.cs:14-19` | — | M |
| F10 | Command Extensibility Contract | platform capability | `Xcaciv.Cupcake.Core.Tests/LoopTests.cs:8-122`; attribute usage in `SearchCommand.cs:11-17`, `InstallCommand.cs:14-16`; OUT-OF-REPO `Xcaciv.Command` @ v2.1.2 | — | L |
| F11 | Input Validation & Supply-Chain Safety | cross-cutting | `SearchCommand.cs:31-58`; `NuGet.config`; `Loop.cs:24,42` | F4, F6 | M |

All eleven form a single fan-out tier: dossiers cite each other's *interfaces*, never
each other's text, so no dossier blocks on another.

### Explicitly out of scope as features (recorded, not dossiered)

- **`src/Xcaciv.Cupcake/` executable** — a two-line console scaffold that prints
  "Hello, World!" and references `Xcaciv.Cupcake.Core` without using it. Vestigial;
  belongs in the PRD's Non-Goals, and is covered as an observation inside F7.
- **Test projects as a product feature** — their content feeds acceptance criteria
  throughout, and their network-dependence is an observation in F6/F11, but "having
  tests" is not a feature of the shell.
- **`ideas/` directory** — present on disk, empty, and untracked by git.

## Known-at-recon issues to carry into the PRD

1. `NuGet.config` declares a package source `%NUGET_LOCAL_PACKAGES%`. The token is not
   expanded on non-Windows hosts, so `dotnet restore` fails with NU1301 for every
   project ("The local source '.../%NUGET_LOCAL_PACKAGES%' doesn't exist"). Verified by
   running restore at the pinned commit.
2. The pinned `Xcaciv.Command` 2.1.1 package is absent from nuget.org (HTTP 404 on the
   flat-container index), so the build additionally cannot resolve its core dependency
   without access to the private GitHub Packages feed.
3. `Loop.RunAsync` never calls the built-in-command registration step that `Loop.Run`
   calls, and does not catch the "no plugins found" case — the two session entry points
   have divergent startup behaviour.
4. `ConsoleContext.SetProgress` computes `total / step`, which is a ratio, not a
   percentage, and divides by zero when `step` is 0. The test asserts the current
   behaviour (`SetProgress(100, 10) == 10`) rather than a percentage (which would be 10
   only by coincidence).
5. `Loop.EnableInstallCommand` is a public setting that nothing reads.
6. Release configuration of the shipping executable retargets `net6.0-windows` while
   Debug targets `net8.0`, and turns on trimming — which is hazardous for a host that
   loads plugin assemblies by reflection.
