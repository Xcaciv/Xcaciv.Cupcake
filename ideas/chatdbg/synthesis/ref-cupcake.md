# Reference Pattern: `Xcaciv.Cupcake` ("a sweet shell")

**Source repo (read-only):** `/tmp/claude-1000/-mnt-g-3RD-Party-reversing/0ec1a1a1-b2b5-4e74-ab1f-585812e55090/scratchpad/refs/Xcaciv.Cupcake`
**HEAD:** `a05dc6f` (Merge PR #2 from `Xcaciv/package_updates`)
**All paths below are repo-relative to that root. All `file:line` citations were read directly from HEAD.**

Cupcake is a ~600-line interactive command shell built on the external `Xcaciv.Command` framework. It is small on purpose: it contributes *no* command behavior of its own to the framework's core, and instead demonstrates the **host** half of a host/plugin contract — start, load tools, prompt, dispatch, exit. A new application that must "look like Cupcake" is being asked to reproduce that host shape, not to copy its commands.

---

## 1. Solution and project layout

`Xcaciv.Cupcake.sln` declares **four product projects, two test projects, and two solution folders**.

| Project | Path | TFM | Output | Role |
|---|---|---|---|---|
| `Xcaciv.Cupcake.Core` | `src/Xcaciv.Cupcake.Core/` | `net8.0` | library | The reusable host: `Loop`, `ConsoleContext`, `Exceptions/LoadingException` |
| `Xcaciv.Cupcake` | `src/Xcaciv.Cupcake/` | `net8.0` | `Exe` | Intended "full" host executable — **currently an unimplemented stub** |
| `Xcaciv.Cupcake.Lit` | `src/Xcaciv.Cupcake.Lit/` | `net8.0` (Debug) / `net6.0-windows` (Release) | `Exe`/`WinExe` | The working host executable; the synchronous, single-file-publishable variant |
| `Xcaciv.Command.Packages` | `src/Xcaciv.Command.Packages/` | `net8.0` | library | A *command package* — tools (`InstallCommand`, `SearchCommand`) plus a NuGet protocol wrapper |
| `Xcaciv.Cupcake.Core.Tests` | `Xcaciv.Cupcake.Core.Tests/` | `net8.0` | xunit | Tests `Loop` and `ConsoleContext` against hand-written fakes |
| `Xcaciv.Command.PackagesTests` | `Xcaciv.Command.PackagesTests/` | `net8.0` | xunit | Tests `NugetWrapper` and `SearchCommand` against **live nuget.org** |

Solution folders: `Solution Items` holds `src\Directory.Packages.props` and `NuGet.config` (`Xcaciv.Cupcake.sln:14-19`); `Tests` nests both test projects (`Xcaciv.Cupcake.sln:20-25`, `:112-115`). Product projects live under `src/`; test projects deliberately sit at the **repo root**, not under `src/` — this is what makes the two `Directory.Packages.props` files (§5) load independently.

### Why the split exists

**Core vs. host executable.** `Xcaciv.Cupcake.Core.csproj:4-7` is a plain library (`net8.0`, `ImplicitUsings`, `Nullable` enabled) whose only package dependency is `Xcaciv.Command` (`:14`). It contains the entire shell (`Loop.cs`, `ConsoleContext.cs`) and *no* `Main`. Both executables are therefore ~15 lines of composition. This is the load-bearing split: **the loop is a library so it can be unit-tested and re-hosted; the executable is a composition root so it can differ per distribution channel.** `Xcaciv.Cupcake.Core.Tests` project-references Core directly (`Xcaciv.Cupcake.Core.Tests.csproj:24`) and drives `Loop` with fakes — impossible if the loop lived inside `Program.cs`.

**The "Lit" variant.** `src/Xcaciv.Cupcake.Lit/README.md:1-3` — *"A light and syncronous implementation of Cupcake shell."* Its distinguishing feature is entirely in the csproj: a `Release`-only `PropertyGroup` (`Xcaciv.Cupcake.Lit.csproj:10-23`) that retargets the app to `net6.0-windows`, flips `OutputType` to `WinExe`, and turns on `PublishSingleFile`, `SelfContained`, `RuntimeIdentifier=win-x64`, `PublishReadyToRun`, `EnableCompressionInSingleFile`, `PublishTrimmed`, `NoWin32Manifest`. Debug builds stay `net8.0`/`Exe` so day-to-day development is normal. Lit is the only project that references *both* Core and the command package (`:30-31`) and additionally takes `Xcaciv.Command` directly (`:26`).

> **Caveat on the Release configuration:** the Release `PropertyGroup` downgrades the TFM to `net6.0-windows` while `Xcaciv.Cupcake.Core` and `Xcaciv.Command.Packages` remain fixed at `net8.0` (`Xcaciv.Cupcake.Core.csproj:4`, `Xcaciv.Command.Packages.csproj:4`). A new application should keep a single TFM across the graph or apply the same conditional to every referenced project.

**The command-package project.** `Xcaciv.Command.Packages` (`Xcaciv.Command.Packages.csproj`) depends on `NuGet.Protocol` and `Xcaciv.Command.Core` only (`:10-11`) — *not* on `Xcaciv.Cupcake.Core`. Tools know the command SDK; they do not know the host. That one-way dependency is what allows the same assembly to be either compiled into a host (§4a) or dropped into a package directory (§4c). Its own README states the scope: *"A light package manager for Xcaciv.Command"* (`src/Xcaciv.Command.Packages/README.md:1-3`).

**Two test projects, mirroring the two dependency islands.** `Xcaciv.Cupcake.Core.Tests` is hermetic (fakes only); `Xcaciv.Command.PackagesTests` is network-bound (`NugetWrapperTests.cs:17`, `:37`, `:57`, `:88` all hardcode `https://api.nuget.org/v3/index.json`). Keeping them separate keeps the fast, offline suite runnable without the slow, online one.

**The `Xcaciv.Cupcake` executable is a stub.** `src/Xcaciv.Cupcake/Program.cs` is two lines:
```
1  // See https://aka.ms/new-console-template for more information
2  Console.WriteLine("Hello, World!");
```
It project-references Core (`Xcaciv.Cupcake.csproj:11`) but never uses it. Treat it as a reserved slot for the future async/full host, not as pattern to copy.

---

## 2. The host loop

Everything is in `src/Xcaciv.Cupcake.Core/Loop.cs` (113 lines).

### Configuration surface (`Loop.cs:6-26`)

| Member | Line | Default | Notes |
|---|---|---|---|
| `EnableInstallCommand` | `:11` | `true` | **Declared but never read anywhere in the repo** except the defaults test (`LoopTests.cs:158`). Dead switch. |
| `Prompt` | `:16` | `"Ɛ> "` | Non-ASCII (U+0190, Latin capital open E) with a trailing space |
| `ExitCommands` | `:20` | `{ "END", "EXIT", "BYEE" }` | Compared with `StringComparer.OrdinalIgnoreCase` (`:57`, `:91`) |
| `PackageDirectory` | `:24` | `@".\packages"` | Windows-style relative path, resolved against the process CWD |
| `Controller` | `:25` | `new CommandController()` | `{ get; private set; }` — settable only by `Run`/`RunAsync` |
| `Environment` | `:26` | `new EnvironmentContext()` | same |

`Controller` and `Environment` are eagerly constructed with framework defaults so a caller can mutate the controller (e.g. `AddCommand`) *before* running, then hand it straight back in via `RunWithDefaults`.

### Synchronous `Run(IIoContext, ICommandController, IEnvironmentContext)` — `Loop.cs:32-68`

1. `:34-35` — assign the passed controller/env onto the properties (the loop stores its collaborators; it does not own them).
2. `:37` — `context.SetStatusMessage("Loading Commands").Wait()` — blocking wait on the async IO contract.
3. `:41-43` — load, in order: `controller.RegisterBuiltInCommands()`; `controller.AddPackageDirectory(this.PackageDirectory)`; `controller.LoadCommands()`.
4. `:45-50` — catches `Xcaciv.Command.Interface.Exceptions.NoPluginsFoundException` **specifically** and degrades: `context.OutputChunk("No Plugins Found. You may want to check out \`install --help\`").Wait()`, then **continues into the loop**. A shell with zero plugins is a valid shell.
5. `:51-54` — any other exception is wrapped: `throw new Exceptions.LoadingException("Unable to load commands.", ex)`.
6. `:56-66` — the loop itself:
   ```
   var inputCommand = "";
   while (!this.ExitCommands.Contains(inputCommand, StringComparer.OrdinalIgnoreCase))
   {
       if (!String.IsNullOrEmpty(inputCommand)) controller.Run(inputCommand, context, env).Wait();
       inputCommand = context.PromptForCommand(this.Prompt).Result;
   }
   ```
   The ordering is **execute-then-prompt**, seeded with `""`. The empty seed passes the exit test, skips the guarded execute, and falls through to the first prompt. Exit is evaluated at the top of the loop, so an exit command is read and then terminates on the next condition check — it is never dispatched to the controller. Blank input is silently skipped, not dispatched.
7. Both `.Wait()` (`:62`) and `.Result` (`:65`) are sync-over-async by design — this is what "synchronous" means for Cupcake Lit.

### Asynchronous `RunAsync(...)` — `Loop.cs:70-103`

Same shape, **four concrete differences**:

1. **It does not call `RegisterBuiltInCommands()`.** `:79-80` calls only `AddPackageDirectory` + `LoadCommands`. Compare `:41-43`. An async host gets *no* built-in commands unless it registers them itself.
2. **It does not special-case `NoPluginsFoundException`.** `:82-85` has a single `catch (Exception ex)` that wraps everything in `LoadingException` — so an empty package directory is a **fatal** error on the async path and a **warning** on the sync path.
3. **It emits a second status message**, `await context.SetStatusMessage("Done")` at `:87`, which the sync path never sends.
4. **The loop body is wrapped in `await Task.Run(async () => { ... })`** (`:89-101`), pushing the prompt/dispatch cycle onto a thread-pool thread. Inside, `await controller.Run(...)` (`:94`) and `await context.PromptForCommand(...)` (`:96`) replace the blocking calls. Load-phase awaits use `.ConfigureAwait(false)` (`:75`, `:87`); the loop-body awaits do not.

Two `TODO`s are parked in the async loop at `:98-99`: *"figure out how to handle non existing controller: download, compile"* and *"support NuGet style directory structure"* — the intended growth direction (self-extending shell).

### `RunWithDefaults()` — `Loop.cs:105-112`

```
public Loop RunWithDefaults()
{
    Controller.RegisterBuiltInCommands();
    this.Run(new ConsoleContext("Cupcake Console Context", []), Controller, Environment);
    return this;
}
```

The convenience path: it constructs the default `ConsoleContext` (name `"Cupcake Console Context"`, **empty parameter array**), passes the loop's own eagerly-created `Controller`/`Environment`, and returns `this` for fluent chaining. It is the *only* place a `ConsoleContext` is constructed in the whole product.

> **Note the redundancy:** `RunWithDefaults` calls `RegisterBuiltInCommands()` at `:108` and `Run` calls it again at `:41`. On this path the built-ins are registered twice. Harmless with the framework's replace-on-duplicate registry, but a new host should own that call in exactly one place.

### Testability consequence

Because `Run`/`RunAsync` take `IIoContext`, `ICommandController`, and `IEnvironmentContext` as **parameters**, the entire loop is drivable from a test. `Xcaciv.Cupcake.Core.Tests/LoopTests.cs` supplies `FakeIoContext` (`:8-65`), `FakeController` (`:67-82`), and `FakeEnvironment` (`:84-122`); the fake's `PromptForCommand` returns the constant `"END"` (`LoopTests.cs:36`), so both `Run_ExitsOnEnd` (`:126-138`) and `RunAsync_ExitsOnEnd` (`:140-152`) terminate after exactly one prompt. `Loop_Defaults_Initialized` (`:154-162`) pins the four configuration defaults.

---

## 3. The IO context pattern

`src/Xcaciv.Cupcake.Core/ConsoleContext.cs` (104 lines) is the host's single adapter between the framework's abstract IO contract and a physical terminal.

### Shape

```
ConsoleContext.cs:13
public class ConsoleContext(string name = "ConsoleIo", string[]? parameters = default,
                            Guid? parentId = default, bool verbose = true)
    : AbstractTextIo(name, [.. parameters], parentId)
```

A C# 12 primary constructor forwarding to `AbstractTextIo(name, parameters, parentId)`. `AbstractTextIo` (framework, `Xcaciv.Command.Core`) supplies identity (`Id`, `Name`, `Parent`), `Parameters`/`SetParameters`, pipeline-stage bookkeeping, the `inputPipe`/`outputPipe` channel fields, `OutputChunk` (writes to the output pipe when one is set, else defers to `HandleOutputChunk`), `ReadInputPipeChunks`, `SetInputPipe`/`SetOutputPipe`, `Complete`, `DisposeAsync`, and `AddTraceMessage`. **`ConsoleContext` overrides exactly five members** — everything else is inherited unchanged:

| Override | Line | Behavior |
|---|---|---|
| `GetChild(string[]?)` | `:38-47` | Child construction + pipe propagation |
| `HandleOutputChunk(string)` | `:53-60` | Colored `Console.WriteLine` + `ResetColor` |
| `PromptForCommand(string)` | `:66-72` | Colored `Console.Write(prompt)` + `Console.ReadLine() ?? string.Empty` |
| `SetProgress(int total, int step)` | `:79-84` | Computes and reports progress |
| `SetStatusMessage(string)` | `:90-103` | Verbosity-gated colored status line |

Note what is *not* overridden: `OutputChunk` itself. Commands call `OutputChunk`; the base class decides whether that goes down a pipe or to `HandleOutputChunk`. **The host only implements the terminal endpoint; the framework owns pipe routing.**

### Child contexts and piping propagation (`:38-47`)

```
var child = new ConsoleContext(this.Name + "Child", childParameters, Id);
if (this.HasPipedInput && this.inputPipe != null) child.SetInputPipe(this.inputPipe);
if (this.outputPipe != null) child.SetOutputPipe(this.outputPipe);
return Task.FromResult<IIoContext>(child);
```

Three rules are encoded here:
- **Naming is derivational** — the child's name is the parent's name with `"Child"` appended, so a nesting chain is legible in status output (`ConsoleContextChildChild`).
- **Parentage is explicit** — the parent's `Id` is passed as `parentId`, giving the framework a traceable tree.
- **Pipes are inherited, guarded, and asymmetric** — the input pipe is propagated only when *both* `HasPipedInput` is true *and* `inputPipe` is non-null; the output pipe is propagated on a null check alone. A child therefore participates in the same pipeline as its parent without the parent knowing what the child will do.
- The method is synchronous work returned via `Task.FromResult` — the contract is async, the implementation need not be.

> **Latent trap:** `childParameters` defaults to `null` and is passed straight into the primary constructor, where `[.. parameters]` spreads it. Spreading a `null` array throws at runtime. `RunWithDefaults` (`Loop.cs:110`) correctly passes `[]`; a new host must do the same and should pass `[]` rather than `null` from `GetChild` too.

### Progress and status

`SetProgress(int total, int step)` — `:79-84`:
```
int progress = total / step;
SetStatusMessage(string.Format(ProgressTemplate, this.Name, progress));
return Task.FromResult(progress);
```
Three things to be exact about: (a) the arithmetic is `total / step` — integer division, **not** a percentage, despite `ProgressTemplate` (`:20`) rendering it as `"{0} progress {1}%"` with `{0}` = context name, `{1}` = the value; `ConsoleContextTests.cs:26-31` pins this by asserting `SetProgress(100, 10) == 10`. (b) the `SetStatusMessage` call at `:82` is **not awaited** — fire-and-forget. (c) `step == 0` divides by zero. Progress reporting is routed *through* status messaging, so it inherits verbosity gating for free.

`SetStatusMessage(string)` — `:90-103`: the verbosity gate.
```
if (!Verbose) { Debug.WriteLine(message); return Task.CompletedTask; }
Console.ForegroundColor = StatusForegroundColor;
Console.BackgroundColor = StatusBackgroundColor;
Console.WriteLine(message);
Console.ResetColor();
```
Quiet mode does not discard diagnostics; it **redirects them to `Debug`**. Status output is a separate visual channel from command output — different colors, same stream.

### Color and verbosity configuration surface (`:20-31`)

Every visual decision is a public settable property with a default, so a host can restyle without subclassing:

| Property | Line | Default |
|---|---|---|
| `ProgressTemplate` | `:20` | `"{0} progress {1}%"` |
| `ForegroundColor` / `BackgroundColor` (command output) | `:23-24` | `Blue` / `Black` |
| `StatusForegroundColor` / `StatusBackgroundColor` | `:26-27` | `Yellow` / `DarkBlue` |
| `PromptForegroundColor` / `PromptBackgroundColor` | `:29-30` | `Green` / `Black` |
| `Verbose` | `:31` | ctor parameter, defaults `true` |

Both output paths follow **set-colors → write → `Console.ResetColor()`** (`:55-58`, `:98-101`) so no color leaks between writes. The prompt path (`:68-70`) deliberately **does not reset** — it uses `Console.Write` (no newline) and leaves the prompt colors active so the user's typed input is colored, resetting only on the next output.

> **Verbosity caveat:** `:31` declares `public new bool Verbose { get; set; }`, which **shadows** rather than overrides `AbstractTextIo.Verbose`. Base-class code that consults `this.Verbose` (notably the framework's `AddTraceMessage`) reads the *base* property, which the primary constructor never assigns. A new host should set the base property instead of shadowing it.

---

## 4. How tools/commands are contributed to the host

There are **exactly three** contribution paths, and they differ in *who constructs the tool*, *when it is known*, and *whether the host must be recompiled*.

### (a) In-process instance registration — the Lit path

`src/Xcaciv.Cupcake.Lit/Program.cs:9-12`:
```
var commandLoop = new Xcaciv.Cupcake.Core.Loop();
commandLoop.Controller.AddCommand("internal", new InstallCommand());
commandLoop.Controller.AddCommand("internal", new SearchCommand());
commandLoop.RunWithDefaults();
```
The **executable** constructs live command instances and hands them to the controller under the package key `"internal"`, *before* the loop starts. This works only because `Xcaciv.Cupcake.Lit.csproj:30` project-references `Xcaciv.Command.Packages`, so the tool types are compiled into the app. Characteristics: no assembly loading, no discovery, no isolation, no failure mode — the tools are as available as any other compiled code, and the set is frozen at build time. The overload used is `AddCommand(string packageKey, ICommandDelegate command, bool modifiesEnvironment = false)`; the third argument is omitted, so neither command is declared as environment-modifying.

This is the path for **first-party tools the host ships with and wants guaranteed present**.

### (b) Built-in registration — the framework's own set

`Loop.cs:41` (inside `Run`) and `Loop.cs:108` (inside `RunWithDefaults`): `controller.RegisterBuiltInCommands()`. A single parameterless call that asks the *framework* to install its default command set into the registry (in the framework's `CommandController` this registers `Regif`, `Say`, `Set` — flagged as environment-modifying — and `Env`, all under the package key `"Default"`). The host chooses only whether to call it; it does not choose which commands, cannot pass configuration, and cannot subset the result.

This is the path for **the shell primitives every host wants** (say, set, env). Note again: `Run` calls it, `RunAsync` **does not** (§2).

### (c) External package-directory loading — the runtime-extension path

`Loop.cs:42-43` and `:79-80`, always as an ordered pair:
```
controller.AddPackageDirectory(this.PackageDirectory);   // register ".\packages"
controller.LoadCommands();                                // crawl and load
```
`AddPackageDirectory` registers the root the loader is permitted to crawl; `LoadCommands()` then discovers and loads command assemblies from it. The framework's `LoadCommands` signature is `LoadCommands(string subDirectory = "bin")` — Cupcake always uses the default, so plugins are expected in a `bin` subdirectory beneath each package. Because `PackageDirectory` is `@".\packages"` (`Loop.cs:24`), resolution is relative to the process working directory.

Characteristics, in contrast to (a) and (b): tools are **discovered at runtime**, may not exist, may be added or replaced without recompiling the host, and **failure is a normal condition** — `NoPluginsFoundException` is caught and degraded on the sync path (`Loop.cs:45-50`).

### The exact difference

| | (a) `AddCommand("internal", …)` | (b) `RegisterBuiltInCommands()` | (c) package directory |
|---|---|---|---|
| Who constructs the tool | the host executable, with `new` | the framework | the plugin loader, by reflection |
| When the set is fixed | compile time | compile time (framework's version) | every process start |
| Package key | host-chosen (`"internal"`) | framework-chosen (`"Default"`) | derived from the package on disk |
| Requires a project reference | yes | yes (to the framework) | **no** |
| Can fail | no | no | yes — `NoPluginsFoundException` |
| Extensible without rebuilding the host | no | no | **yes** |
| Called from | `Program.cs` (composition root) | `Loop.Run` / `RunWithDefaults` | `Loop.Run` / `Loop.RunAsync` |

The architectural point: **(a) and (b) run in the composition root and the loop's prologue and cannot fail; (c) is the only path that makes the host's capability set open-ended, and is therefore the only one wrapped in error handling.**

---

## 5. Central package management

`ManagePackageVersionsCentrally` is on and **no `.csproj` in the repo carries a `Version` attribute on any `PackageReference`.**

### Two `Directory.Packages.props`, byte-identical

`Directory.Packages.props` (repo root) and `src/Directory.Packages.props` have the same 19 lines:

```
Directory.Packages.props:3        <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
Directory.Packages.props:7        <PackageVersion Include="NuGet.Protocol"           Version="7.0.1" />
Directory.Packages.props:8        <PackageVersion Include="Xcaciv.Command"           Version="2.1.1" />
Directory.Packages.props:9        <PackageVersion Include="Xcaciv.Command.Core"      Version="2.1.0" />
Directory.Packages.props:10       <PackageVersion Include="Xcaciv.Command.Interface" Version="2.1.0" />
Directory.Packages.props:14       <PackageVersion Include="coverlet.collector"          Version="6.0.4" />
Directory.Packages.props:15       <PackageVersion Include="Microsoft.NET.Test.Sdk"      Version="18.0.1" />
Directory.Packages.props:16       <PackageVersion Include="xunit"                       Version="2.9.3" />
Directory.Packages.props:17       <PackageVersion Include="xunit.runner.visualstudio"   Version="3.1.5" />
```

Both files group versions under two commented headings: `<!-- Applicaiton Versions -->` (`:5`, sic — the typo is in both copies) and `<!-- Unit Test Versions -->` (`:12`). MSBuild walks *up* from each project and stops at the first `Directory.Packages.props` it finds, so `src/*` projects bind to `src/Directory.Packages.props` and the two root-level test projects bind to the root copy. That is why two copies exist — and it is a **duplication hazard**, since the two must be edited in lockstep. Only `src\Directory.Packages.props` is surfaced as a solution item (`Xcaciv.Cupcake.sln:16`).

### How project files reference packages

Every reference is version-free:

- `src/Xcaciv.Cupcake.Core/Xcaciv.Cupcake.Core.csproj:14` — `<PackageReference Include="Xcaciv.Command" />`
- `src/Xcaciv.Command.Packages/Xcaciv.Command.Packages.csproj:10-11` — `NuGet.Protocol`, `Xcaciv.Command.Core`
- `src/Xcaciv.Cupcake.Lit/Xcaciv.Cupcake.Lit.csproj:26` — `Xcaciv.Command`
- `Xcaciv.Cupcake.Core.Tests/Xcaciv.Cupcake.Core.Tests.csproj:10-21` — the four test packages plus `Xcaciv.Command` and `Xcaciv.Command.Interface`
- `Xcaciv.Command.PackagesTests/Xcaciv.Command.PackagesTests.csproj:13-23` — same test set plus `Xcaciv.Command`

Asset metadata still lives on the reference, not in the props file — `coverlet.collector` and `xunit.runner.visualstudio` each carry `<PrivateAssets>all</PrivateAssets>` and the standard `IncludeAssets` list (`Xcaciv.Cupcake.Core.Tests.csproj:10-13`, `:16-19`). **Central management owns *versions*; the project owns *asset flow*.**

### Feed configuration

`NuGet.config` clears inherited sources and defines three (`:4-10`): `nuget.org` (`https://api.nuget.org/v3/index.json`), `github` (`https://nuget.pkg.github.com/xcaciv/index.json`), and `local` (`%NUGET_LOCAL_PACKAGES%` — an environment-variable-driven local folder for developing the framework alongside the host). `packageSourceMapping` (`:12-20`) routes `*` to `nuget.org` and `Xcaciv.*` to `local` — so first-party packages resolve from the developer's local drop while everything else comes from nuget.org.

> **Caveat:** the `github` source has **no** `packageSourceMapping` entry. With source mapping enabled, a source with no patterns is never consulted, making that source effectively inert as configured.

---

## 6. Error handling at the host boundary

Three tiers, each with a distinct policy.

**Tier 1 — expected absence, converted to user-facing text and survived.** `Loop.cs:45-50`:
```
catch (Xcaciv.Command.Interface.Exceptions.NoPluginsFoundException)
{
    context.OutputChunk("No Plugins Found. You may want to check out `install --help`").Wait();
}
```
The exception is swallowed (not even bound to a variable), replaced by a message that **names the recovery action**, and execution falls through into the prompt loop. A commented-out `throw new Exceptions.LoadingException(...)` at `:48-49` records the rejected alternative, alongside `// TODO: download first plugin and GOTO start again! :D`. Only `Run` does this; `RunAsync` has no such catch.

**Tier 2 — unexpected load failure, wrapped in a host-domain exception.** `Loop.cs:51-54` and `:82-85`:
```
catch (Exception ex) { throw new Exceptions.LoadingException("Unable to load commands.", ex); }
```
`LoadingException` (`src/Xcaciv.Cupcake.Core/Exceptions/LoadingException.cs:4-13`) is a bare `Exception` subclass with message-only and message+inner constructors. Its whole purpose is to give the host boundary a single catchable type for "startup failed", with the original cause preserved as `InnerException`.

**Tier 3 — the process boundary.** `src/Xcaciv.Cupcake.Lit/Program.cs:7-19`: the *entire* program body sits in one `try`, and the catch is:
```
catch (Exception ex)
{
    Console.WriteLine($"Error {ex.Message}");
    // exit in error state
    Environment.Exit(1);
}
```
It prints **only `ex.Message`** — no type name, no stack trace, no inner-exception chain — then exits `1`.

### Exit codes

- **0** — implicit, by falling off the end of `Program.cs` after `RunWithDefaults()` returns because the user typed an exit command.
- **1** — `Environment.Exit(1)` on any exception that escapes the loop (`Program.cs:18`).

No other codes exist. There is no distinct code for load failure vs. command failure.

### What is *not* caught

**Per-command execution is unguarded.** `controller.Run(inputCommand, context, env).Wait()` (`Loop.cs:62`) and `await controller.Run(...)` (`:94`) have no surrounding `try`. A command that throws therefore propagates out of the loop, out of `RunWithDefaults`, and into `Program.cs`'s catch — **killing the shell and exiting 1**. For an interactive shell this is the notable gap: a new application should wrap the dispatch call so a single bad command reports and returns to the prompt. (The synchronous `.Wait()`/`.Result` also mean a command fault surfaces as `AggregateException`, whose `.Message` is the generic "One or more errors occurred" — compounding the terse Tier-3 message.)

---

## 7. `Xcaciv.Command.Packages` — tools that acquire more tools

Three files: `InstallCommand.cs`, `SearchCommand.cs`, `NugetWrapper.cs`. This is the project that makes Cupcake a *self-extending* host in principle.

### Commands are declared entirely by attributes

Both commands derive from `AbstractCommand` (framework, `Xcaciv.Command.Core`) and override two methods: `HandleExecution(string[] parameters, IEnvironmentContext env)` and `HandlePipedChunk(string pipedChunk, string[] parameters, IEnvironmentContext env)`. Their CLI surface is pure metadata:

`InstallCommand.cs:14-17`
```
[CommandRoot("Package", "Package commands")]
[CommandRegister("Install", "install a package")]
[CommandParameterOrdered("packagename", "The unique name of the package to install", IsRequired = true)]
```
`SearchCommand.cs:11-17`
```
[CommandRoot("Package", "Package commands")]
[CommandRegister("Search", "search for a package")]
[CommandParameterOrdered("search_terms", "String associated to the desired package.", IsRequired = true)]
[CommandParameterNamed("source",    "The source to search for the package.")]
[CommandParameterNamed("take",      "Limit the number of results to return.", DefaultValue = "20")]
[CommandParameterNamed("verbosity", "The level of detail to display in the output.",
                       AllowedValues = ["quiet", "normal", "detailed"], DefaultValue = "normal")]
[CommandFlag("prerelease", "Include prerelease packages in the search results.")]
```
Both share `CommandRoot("Package", …)`, grouping them under one namespace in the shell. **Help text, defaults, allowed values, required-ness, and ordered-vs-named-vs-flag arity are all declarative** — the host gets `--help` for free and never parses argv itself.

### `InstallCommand` — a deliberate stub

`InstallCommand.cs:19-27`:
```
public override string HandleExecution(string[] parameters, IEnvironmentContext env)
    => "Not installing " + String.Join(',', parameters);
public override string HandlePipedChunk(string pipedChunk, string[] parameters, IEnvironmentContext env)
    => $"Not installing {pipedChunk} " + String.Join(',', parameters);
```
It echoes its arguments and installs nothing. **It never calls `NugetWrapper`.** Note the mismatch with `Loop.cs:47`, which advertises `install --help` as the recovery path for a plugin-less shell — the advertised capability is not yet wired.

### `SearchCommand` — the fully implemented one

`HandleExecution` (`SearchCommand.cs:20-86`), in order:
1. `:22` — `this.ProcessParameters(parameters)` — the base class turns `string[]` into a dictionary per the attributes.
2. `:24-29` — source selection: `env.GetValue("PackageSourceUrl")`, defaulting to `https://api.nuget.org/v3/index.json`. **Configuration reaches a command through the environment context, not through the host.**
3. `:32-35` — **HTTPS is mandatory**: `Uri.TryCreate(..., UriKind.Absolute, out var uri)` must succeed *and* `uri.Scheme == Uri.UriSchemeHttps`, else `InvalidOperationException("Insecure or invalid package source URL. HTTPS is required.")`.
4. `:38-39` — `new PackageSource(url)` → `Repository.Factory.GetCoreV3(packageSource)`.
5. `:42-46` — `take` must parse as an integer (else `InvalidOperationException`) and is **clamped to `[1, 100]`** via `Math.Clamp`.
6. `:47` — `prerelease` is a presence check on the flag.
7. `:50-58` — search terms are trimmed; whitespace-only returns `string.Empty` early; anything over **200 characters is truncated**.
8. `:60` — `NugetWrapper.FindPackageAsync(searchTerms, repository, limit, prerelease).Result` — sync-over-async, consistent with the Lit host.
9. `:62-83` — a `verbosity` switch projecting results into the inherited `searchResult` list: `quiet` → ids only; `normal` → `"{id} {version} : {summary}"`; `detailed` → adds download count, `Published`, `Authors`, `License`, and **`Vulnerabilities:{count}`**, separated by `---`; `default` falls back to the `normal` shape.
10. `:85` — `string.Join("\n", searchResult)`.

`HandlePipedChunk` (`:88-91`) explicitly declines: `$"Unsupported search method for {pipedChunk} (piped)"`. **A command that cannot participate in a pipeline says so in its output rather than throwing.**

### `NugetWrapper` — the NuGet protocol surface

A static utility class (`NugetWrapper.cs:18`) over `NuGet.Protocol` / `NuGet.Protocol.Core.Types`, exposing the full acquire pipeline:

| Method | Lines | NuGet resource used | Purpose |
|---|---|---|---|
| `FindPackageAsync` | `:20-32` | `PackageSearchResource.SearchAsync(term, SearchFilter(includePrerelease), skip: 0, take: limit, logger, ct)` | search |
| `FindPackageVersionsAsync` | `:34-50` | `FindPackageByIdResource.GetAllVersionsAsync` | enumerate versions |
| `ResolveDependenciesAsync` | `:52-73` | `DependencyInfoResource.ResolvePackage` | dependency info for one `PackageIdentity` |
| `DownloadPackageAsync` | `:81-101` | `FindPackageByIdResource.CopyNupkgToStreamAsync` → `FileStream` | fetch the `.nupkg` |
| `GetNuspecData` | `:103-109` | `PackageArchiveReader.NuspecReader.GetIdentity()` | read identity from the downloaded file |
| `InstallPackage` | `:111-128` | composes the above | orchestrate an install |

Design notes worth copying: every async method takes optional `ILogger?` and `CancellationToken?` and defaults them to `NullLogger.Instance` / `CancellationToken.None` (`:23-24`, `:55-56`), and `ResolveDependenciesAsync` defaults its framework to `NuGetFramework.AnyFramework` (`:57`). `SourceRepository` is passed **in** to `FindPackageAsync`/`ResolveDependenciesAsync`/`DownloadPackageAsync` (the caller owns source selection and can therefore enforce policy such as the HTTPS check), while `FindPackageVersionsAsync` (`:39-40`) builds its own from a URL — an inconsistency, and the one method that bypasses the caller's policy.

### `InstallPackage` and what the pattern implies

`NugetWrapper.cs:111-128`:
```
string packageFileName = $"{identity.Id}.{identity.Version}.nupkg";
string targetFilePath  = Path.Combine(targetDirectory, packageFileName);
DownloadPackageAsync(identity, repository, targetFilePath).GetAwaiter().GetResult();
PackageIdentity packageIdentity = GetNuspecData(targetFilePath);
string packageDirectory = Path.Combine(targetDirectory, packageIdentity.Id, packageIdentity.Version.ToString());
if (!Directory.Exists(packageDirectory)) Directory.CreateDirectory(packageDirectory);
// TODO: Extract package to directory
// TODO: resolve dependencies
// ZipFile.ExtractToDirectory(targetFilePath, packageDirectory);
```
It downloads, re-reads identity **from the downloaded artifact rather than trusting the request** (`:118`), and lays out an `{id}/{version}/` directory — a layout that lines up with the package directory the loop crawls (`Loop.cs:24`) and with `RunAsync`'s parked `// TODO: support NuGet style directory structure` (`Loop.cs:99`). **Extraction and dependency resolution are explicit TODOs** (`:125-127`), and nothing calls `InstallPackage`.

**What this implies for a host that acquires tools at runtime.** The loop already loads from a directory (§4c); the package project already knows how to search a remote feed, resolve versions and dependencies, and download to that directory. The intended closed loop is: *`search` finds a tool → `install` downloads and extracts it into `.\packages\{id}\{version}\bin` → the host re-runs `LoadCommands()` (or restarts) → the tool is a first-class command.* Cupcake demonstrates the shape and leaves the last link (extract + re-scan) unimplemented. A new application inheriting this pattern is inheriting an **open-ended, remotely-sourced capability set**, which is exactly why `SearchCommand` already enforces HTTPS (`:32-35`), clamps result counts (`:46`), bounds input length (`:55-58`), and surfaces a vulnerability count in `detailed` output (`:76`) — trust boundaries belong in the acquiring command.

### Test posture

`Xcaciv.Command.PackagesTests/NugetWrapperTests.cs` exercises all four network methods against live nuget.org, including a real download to a temp file that is deleted afterwards (`:83-102`), and asserts a nonexistent package yields an empty version list rather than throwing (`:67-80`). `SearchCommandTests.cs` covers each verbosity level (`:26-69`), the prerelease flag (`:71-82`), source override via `env.SetValue("PackageSourceUrl", …)` (`:84-96`), the `take` limit (`:98-110`), and the piped-chunk refusal (`:128-138`).

---

## 8. The Cupcake host pattern — rules a new application must follow

Prescriptive. Each rule names the evidence it derives from.

### Structure

1. **Put the shell in a library, never in `Program.cs`.** Create a `*.Core` class library holding the loop, the IO context, and host exceptions; give it no `Main`. (`src/Xcaciv.Cupcake.Core/` — `Loop.cs`, `ConsoleContext.cs`, `Exceptions/LoadingException.cs`.)
2. **Make each executable a composition root of roughly a dozen lines**: construct the loop, register in-process tools, call the convenience runner, catch everything. (`src/Xcaciv.Cupcake.Lit/Program.cs:7-19`.)
3. **Put product projects under `src/` and test projects at the repo root**, and group them in the solution with a `Tests` solution folder plus a `Solution Items` folder exposing `Directory.Packages.props` and `NuGet.config`. (`Xcaciv.Cupcake.sln:14-25`, `:112-115`.)
4. **Ship command implementations in a separate project that depends on the command SDK only — never on the host.** The dependency arrow points tool → SDK, never tool → host, so the same assembly can be linked in or dropped in. (`Xcaciv.Command.Packages.csproj:10-11`; contrast `Xcaciv.Cupcake.Lit.csproj:30-31`.)
5. **Every project: `net8.0`, `<ImplicitUsings>enable</ImplicitUsings>`, `<Nullable>enable</Nullable>`; test projects add `<IsPackable>false</IsPackable>` and `<IsTestProject>true</IsTestProject>`.** (All six csproj `PropertyGroup`s; `Xcaciv.Cupcake.Core.Tests.csproj:6-7`.) Keep one TFM across the whole graph — do not repeat the Release-only downgrade at `Xcaciv.Cupcake.Lit.csproj:10-12`.
6. **If you want a self-contained distribution, express it as a `Release`-only `PropertyGroup`, not a separate codebase** — `PublishSingleFile`, `SelfContained`, `RuntimeIdentifier`, `PublishReadyToRun`, `EnableCompressionInSingleFile`, `PublishTrimmed`. Debug stays a plain `Exe`. (`Xcaciv.Cupcake.Lit.csproj:10-23`.)
7. **Split tests by dependency class:** one hermetic suite driven by hand-written fakes, one integration suite that touches the network. Never mix them in one project. (`Xcaciv.Cupcake.Core.Tests/` vs. `Xcaciv.Command.PackagesTests/`.)

### The loop

8. **Expose the loop's whole personality as public settable properties with defaults:** prompt string, exit-command list, package directory, and any feature switches. Do not hardcode them in the loop body. (`Loop.cs:11-24`.)
9. **Match Cupcake's defaults unless you have a reason not to:** prompt `"Ɛ> "` with a trailing space (`Loop.cs:16`); exit commands `{ "END", "EXIT", "BYEE" }` compared case-insensitively via `StringComparer.OrdinalIgnoreCase` (`:20`, `:57`); package directory `.\packages` (`:24`).
10. **`Run` must accept `(IIoContext, ICommandController, IEnvironmentContext)` as parameters and assign them to properties.** Do not construct collaborators inside the loop — this is the single decision that makes the loop testable. (`Loop.cs:32-35`; proof at `LoopTests.cs:126-152`.)
11. **Eagerly initialize `Controller` and `Environment` with framework defaults, exposed as `{ get; private set; }`,** so a caller can mutate the controller before running and then pass it straight back. (`Loop.cs:25-26`, consumed at `Program.cs:10-11` → `Loop.cs:110`.)
12. **Announce the load phase before doing it**: `SetStatusMessage("Loading Commands")` first, then load. (`Loop.cs:37`, `:75`.)
13. **Load in this exact order: built-ins → register package directory → load commands.** (`Loop.cs:41-43`.) Do it once, in one method — do not repeat `RegisterBuiltInCommands()` in both the convenience path and the loop as Cupcake does at `:108` and `:41`.
14. **Write the loop as seed-empty, exit-checked-at-top, execute-then-prompt:**
    ```
    var inputCommand = "";
    while (!ExitCommands.Contains(inputCommand, StringComparer.OrdinalIgnoreCase)) {
        if (!string.IsNullOrEmpty(inputCommand)) /* dispatch */;
        inputCommand = /* prompt */;
    }
    ```
    Blank input is skipped, never dispatched; the exit command is never dispatched. (`Loop.cs:56-66`, `:90-100`.)
15. **Provide a `RunWithDefaults()` that constructs the default IO context, passes the loop's own controller and environment, and returns `this` for chaining.** Pass an **empty array**, not `null`, for parameters. (`Loop.cs:105-112`.)
16. **If you offer both sync and async paths, make them differ only in awaiting.** Cupcake's diverge in four ways — the async path skips built-in registration, treats "no plugins" as fatal, emits an extra `"Done"` status, and wraps the body in `Task.Run` (`Loop.cs:79-89` vs. `:41-50`). **Do not reproduce that divergence**: factor the shared load-and-loop logic and let only the await style differ.
17. **`ConfigureAwait(false)` on the load-phase awaits** of the async path. (`Loop.cs:75`, `:87`.)

### The IO context

18. **Write exactly one class deriving from the framework's `AbstractTextIo`, and override only the terminal-specific members:** `GetChild`, `HandleOutputChunk`, `PromptForCommand`, `SetProgress`, `SetStatusMessage`. Leave `OutputChunk` alone — the base class decides pipe-vs-terminal routing. (`ConsoleContext.cs:38-103`.)
19. **Use a primary constructor with defaults `(name, parameters, parentId, verbose)`** and forward name/parameters/parentId to the base. (`ConsoleContext.cs:13`.)
20. **In `GetChild`: derive the child's name from the parent's, pass the parent's `Id` as `parentId`, then propagate pipes — input pipe only when `HasPipedInput && inputPipe != null`, output pipe on a null check — and return via `Task.FromResult`.** Piping is inherited downward and the child never negotiates it. (`ConsoleContext.cs:38-47`.)
21. **Keep three separate visual channels with independently settable colors**: command output, status, prompt. Every write follows set-colors → write → `Console.ResetColor()`; the prompt uses `Console.Write` (no newline) and intentionally omits the reset so typed input stays colored. (`ConsoleContext.cs:23-30`, `:55-58`, `:68-70`, `:98-101`.)
22. **Gate status output on `Verbose`, and when quiet, redirect to `Debug.WriteLine` rather than discarding.** (`ConsoleContext.cs:90-96`.) Set the **base** `Verbose` property; do not shadow it with `public new` as `ConsoleContext.cs:31` does.
23. **Route progress through the status channel** via a public `ProgressTemplate` format string (`"{0} progress {1}%"`, `{0}` = context name, `{1}` = value) so progress inherits verbosity gating for free. Guard the divisor and `await` the status call — Cupcake does neither (`ConsoleContext.cs:79-84`).
24. **Never dereference a possibly-null parameter array.** Default to `[]`, both in the constructor and when creating children. (`ConsoleContext.cs:13`, `:40`.)
25. **`PromptForCommand` must return `Console.ReadLine() ?? string.Empty`** — never null, so end-of-input degrades to a blank line the loop skips. (`ConsoleContext.cs:71`.)

### Contributing tools

26. **Support all three contribution paths, and keep them in their proper places.** In-process instances registered by the executable under a host-chosen package key (`Program.cs:10-11`, key `"internal"`); the framework's built-in set via one `RegisterBuiltInCommands()` call in the loop's prologue (`Loop.cs:41`); the external package directory via the `AddPackageDirectory(dir)` → `LoadCommands()` pair (`Loop.cs:42-43`). Only the third is fallible, and only the third makes the host extensible without a rebuild.
27. **Declare a command's entire CLI surface with attributes** — root/group, name+description, ordered parameters with `IsRequired`, named parameters with `DefaultValue` and `AllowedValues`, and flags — so help, defaults, and validation are generated rather than written. (`SearchCommand.cs:11-17`.)
28. **A command reads its configuration from `IEnvironmentContext`, not from the host.** (`SearchCommand.cs:24` — `env.GetValue("PackageSourceUrl")` with a hardcoded fallback.)
29. **A command that cannot handle piped input returns an explanatory string rather than throwing.** (`SearchCommand.cs:88-91`.)
30. **Group related commands under a shared `CommandRoot`.** (`InstallCommand.cs:14` and `SearchCommand.cs:11` both `[CommandRoot("Package", "Package commands")]`.)

### Packages and feeds

31. **Turn on `ManagePackageVersionsCentrally` and give every `PackageReference` no `Version`.** (`Directory.Packages.props:3`; all six csproj files.)
32. **Group `PackageVersion` items into commented sections — application versions and unit-test versions.** (`Directory.Packages.props:5`, `:12`.)
33. **Prefer a single `Directory.Packages.props` at the repo root.** Cupcake carries two identical copies (root + `src/`) because its test projects sit outside `src/`; that duplication is a maintenance hazard, not a feature.
34. **Keep asset metadata on the `PackageReference`, not in the props file** — `PrivateAssets`/`IncludeAssets` for `coverlet.collector` and `xunit.runner.visualstudio`. (`Xcaciv.Cupcake.Core.Tests.csproj:10-19`.)
35. **`NuGet.config` must `<clear />` inherited sources, then declare its own,** and use `packageSourceMapping` to route first-party patterns (`Xcaciv.*`) to a local/dev feed and `*` to nuget.org. **Give every declared source a mapping entry** — the unmapped `github` source at `NuGet.config:7` is dead configuration.
36. **Support a local development feed through an environment variable** (`%NUGET_LOCAL_PACKAGES%`, `NuGet.config:8`) so the host and its framework can be developed together.

### Errors and exit

37. **Define one host-domain exception (`LoadingException`) with message-only and message+inner constructors, and wrap every unexpected startup failure in it, preserving the inner exception.** (`Exceptions/LoadingException.cs:4-13`; `Loop.cs:51-54`.)
38. **Treat "no tools found" as a normal, survivable condition:** catch it specifically, emit a message that names the recovery command, and continue to the prompt. (`Loop.cs:45-50`.) Apply this on *every* run path, not just the sync one.
39. **Wrap the whole program body in a single top-level `try`, print a short user-facing error, and `Environment.Exit(1)`.** (`Program.cs:7-19`.) Improve on Cupcake by including the exception type and unwrapping `AggregateException`/`InnerException` — `ex.Message` alone (`Program.cs:16`) is too thin, especially given the sync-over-async `.Wait()` at `Loop.cs:62`.
40. **Exit 0 by falling off the end after an exit command; exit 1 for any unhandled exception.** No other codes.
41. **Wrap the per-command dispatch in its own `try`/`catch` so one failing command reports and returns to the prompt instead of killing the shell.** Cupcake does **not** do this (`Loop.cs:62`, `:94` are unguarded) — this is the pattern's main defect and the one place a new application should knowingly deviate.

### Runtime tool acquisition

42. **If the host can acquire tools at runtime, put the acquisition logic in a wrapper class over the feed protocol** with methods for search, version enumeration, dependency resolution, download, identity read, and install. (`NugetWrapper.cs:20-128`.)
43. **Every async acquisition method takes optional `ILogger?` and `CancellationToken?`, defaulting to `NullLogger.Instance` and `CancellationToken.None`.** (`NugetWrapper.cs:23-24`, `:55-56`.)
44. **Pass the source repository *in* from the caller** so source-selection policy (HTTPS enforcement, allow-lists) lives in one place and cannot be bypassed. (`NugetWrapper.cs:20`, `:52`, `:81`; the exception at `:34-40` is the anti-pattern.)
45. **Enforce trust boundaries in the acquiring command, not the wrapper:** require HTTPS on the source URL (`SearchCommand.cs:32-35`), clamp result counts to a sane range (`:46`), bound input length (`:55-58`), and surface vulnerability counts in verbose output (`:76`).
46. **Re-read package identity from the downloaded artifact rather than trusting the request,** then lay it out as `{packageDirectory}/{id}/{version}/` so the loop's loader finds it on the next scan. (`NugetWrapper.cs:118-124`, aligning with `Loop.cs:24` and the loader's default `bin` subdirectory.)
47. **Offer a `verbosity` parameter with `quiet`/`normal`/`detailed` and a `default:` arm that falls back to `normal`,** joining multi-row output with `"\n"`. (`SearchCommand.cs:62-85`.)

---

## Caveats and known gaps in the reference

Recorded so a new application does not copy them by accident:

- **`Xcaciv.Cupcake` (the non-Lit executable) is an unimplemented `Hello, World!` stub** (`src/Xcaciv.Cupcake/Program.cs:1-2`). Only Lit is a working host.
- **`InstallCommand` installs nothing** (`InstallCommand.cs:19-27`) and never calls `NugetWrapper`, yet `Loop.cs:47` advertises `install --help` as the fix for a plugin-less shell.
- **`NugetWrapper.InstallPackage` is unfinished** — extraction and dependency resolution are `TODO`s (`NugetWrapper.cs:125-127`), and nothing calls the method.
- **`Loop.EnableInstallCommand` is never read** (`Loop.cs:11`; only asserted at `LoopTests.cs:158`).
- **`Run` and `RunAsync` diverge in behavior, not just in awaiting** — see rule 16.
- **Per-command exceptions kill the shell** — see rule 41.
- **`ConsoleContext.Verbose` shadows the base property** (`ConsoleContext.cs:31`) — see rule 22.
- **`SetProgress` computes `total / step`, not a percentage**, despite the `%` in the template (`ConsoleContext.cs:79-84`; pinned by `ConsoleContextTests.cs:29-30`).
- **`[.. parameters]` will throw if `parameters` is null** (`ConsoleContext.cs:13`, reachable from `:40`) — see rule 24.
- **The Release configuration retargets only Lit to `net6.0-windows`** while its project references stay `net8.0` (`Xcaciv.Cupcake.Lit.csproj:10-12`).
- **The `github` NuGet source has no `packageSourceMapping` entry** and is therefore never consulted (`NuGet.config:7`, `:12-20`).
- **Two identical `Directory.Packages.props` files** must be kept in sync by hand.
- **Framework version drift.** Cupcake pins `Xcaciv.Command` 2.1.1 / `.Core` 2.1.0 / `.Interface` 2.1.0 (`Directory.Packages.props:8-10`). A sibling checkout of the framework at `refs/Xcaciv.Command` is version 3.3.4, in which the IO/command contracts have changed materially — `GetChild()` takes no arguments, output flows as `IResult<string>` rather than `string`, `AbstractCommand.HandleExecution` takes a `Dictionary<string, IParameterValue>`, and the controller's `Run` takes an `IControllerEnvironmentContext`. **Cupcake as written compiles against 2.1.x only.** A new application must pick a framework version and write its overrides against that version's signatures; the *pattern* above (which members to override, in what order to load, how to propagate pipes) is version-independent, but the exact signatures are not.
