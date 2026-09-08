# Xcaciv.Command — Tool Authoring & Tool Hosting Reference

**Scope.** This is a complete, source-verified reference for defining tools (commands) and hosting/executing them with the
`Xcaciv.Command` framework. It is written so a tool author never needs to open the framework source.

**Source of truth.** Every claim below is cited as `path:line` against the repository snapshot at commit `b7200de`
("Merge pull request #95 from Xcaciv/Xcaciv-patch-2"). Paths are relative to the repo root.
**Where the shipped documentation disagrees with the code, the code wins**, and the disagreement is called out
inline and again in §16 ("Documentation vs. source disagreements").

---

## 1. Version, packages, and target frameworks

### 1.1 Current version

All six shippable projects carry `<Version>3.3.4</Version>`:

| Package | csproj | Version line |
|---|---|---|
| `Xcaciv.Command` | `src/Xcaciv.Command/Xcaciv.Command.csproj` | `:5` |
| `Xcaciv.Command.Core` | `src/Xcaciv.Command.Core/Xcaciv.Command.Core.csproj` | `:6` |
| `Xcaciv.Command.Interface` | `src/Xcaciv.Command.Interface/Xcaciv.Command.Interface.csproj` | `:13` |
| `Xcaciv.Command.FileLoader` | `src/Xcaciv.Command.FileLoader/Xcaciv.Command.FileLoader.csproj` | `:6` |
| `Xcaciv.Command.DependencyInjection` | `src/Xcaciv.Command.DependencyInjection/Xcaciv.Command.DependencyInjection.csproj` | `:5` |
| `Xcaciv.Command.Extensions.Commandline` | `src/Xcaciv.Command.Extensions.Commandline/Xcaciv.Command.Extensions.Commandline.csproj` | `:7` |

**Disagreement:** `README.md:94` says "3.3.0 (Current)" and `CHANGELOG.md:8` tops out at `[3.3.0] - 2026-01-11`;
`src/Directory.Packages.props:14-17` pins the *consumed* `Xcaciv.Command*` PackageVersions at `3.3.3`.
`docs/QUICK_REFERENCE.md:1,199` still says "V3.2.2". **The built/produced version is 3.3.4** (the csproj values).

### 1.2 Target frameworks — .NET 10 default, .NET 8 opt-in

`Directory.Build.props:1-11`:

```xml
<UseNet08 Condition="'$(UseNet08)' == ''">false</UseNet08>          <!-- :4  -->
<XcacivBaseTargetFramework>net10.0</XcacivBaseTargetFramework>       <!-- :5  -->
<XcacivTargetFrameworks>$(XcacivBaseTargetFramework)</XcacivTargetFrameworks>                     <!-- :6 -->
<XcacivTargetFrameworks Condition="'$(UseNet08)' == 'true'">net8.0;$(XcacivBaseTargetFramework)</XcacivTargetFrameworks> <!-- :7 -->
<LangVersion>14</LangVersion>                                        <!-- :10 -->
```

* **Default build → `net10.0` only.**
* **`./build.ps1 -UseNet08` → `net8.0;net10.0` multi-target.** Tests auto-skip when multi-targeting because the test
  projects are single-TFM (`CHANGELOG.md:45-49`, `docs/QUICK_REFERENCE.md:13-14`).
* Every project consumes the variable: e.g. `src/Xcaciv.Command/Xcaciv.Command.csproj:10`,
  `src/Xcaciv.Command.Interface/Xcaciv.Command.Interface.csproj:9`.
* **C# 14** is explicitly selected (`Directory.Build.props:10`), and the source uses C# 14 features — notably the
  `field` keyword in attribute property setters (`src/Xcaciv.Command.Interface/Attributes/CommandRegisterAttribute.cs:26-30`).
  A consuming project targeting an older LangVersion can still *use* these packages; it only matters if you compile the framework.
* `Nullable` and `ImplicitUsings` are enabled in all projects; Debug and Release both set
  `TreatWarningsAsErrors=True` for `Xcaciv.Command` and `Xcaciv.Command.Core`
  (`src/Xcaciv.Command/Xcaciv.Command.csproj:41-47`).
* License: `AGPL-3.0-only` on every package.

### 1.3 Dependency versions (`src/Directory.Packages.props`, central package management)

| Package | Version | Line |
|---|---|---|
| `System.IO.Abstractions` | 22.1.0 | `:7` |
| `Xcaciv.Loader` | 2.1.2 | `:8` |
| `Microsoft.Extensions.Configuration.Abstractions` | 10.0.1 | `:9` |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.1 | `:10` |
| `Microsoft.Extensions.Options` (+ `.ConfigurationExtensions`) | 10.0.1 | `:11-12` |
| `System.CommandLine` | 2.0.1 | `:13` |
| `YamlDotNet` | 16.3.0 | `:18` |

`ManagePackageVersionsCentrally=true` (`src/Directory.Packages.props:3`) — `PackageReference` elements carry **no**
`Version` attribute.

### 1.4 Assembly / package layout (what a tool author references)

* **`Xcaciv.Command.Interface`** — contracts only, no implementation dependencies. Contains `ICommandDelegate`,
  `IIoContext`, `IEnvironmentContext`, `IResult<T>`, `CommandResult<T>`, `ResultFormat`, all attributes, all
  `IParameterValue` types, `NamesValidator`, `PipelineConfiguration`, `AuditEvent`, exceptions.
* **`Xcaciv.Command.Core`** — `AbstractCommand`, `AbstractTextIo`, `CommandParameters`, `CommandDescription`,
  `DefaultParameterConverter`, `ParameterCollectionBuilder`, caching factories.
* **`Xcaciv.Command`** — the host: `CommandController`, `CommandExecutor`, `CommandFactory`, `CommandRegistry`,
  `PipelineExecutor`, `PipelineParser`, `HelpService`, `MemoryIoContext`, `EnvironmentContext`,
  `ControllerEnvironmentContext`, built-in commands, encoders, audit loggers.
* **`Xcaciv.Command.FileLoader`** — `Crawler`, `VerifiedSourceDirectories` (plugin discovery + path restriction).
* **`Xcaciv.Command.DependencyInjection`** — `ServiceCollectionExtensions`.
* **`Xcaciv.Command.Extensions.Commandline`** — `CommandLineCommand<T>` adapter for `System.CommandLine`.

**A tool assembly needs only `Xcaciv.Command.Interface` + `Xcaciv.Command.Core`.** Plugin DLLs must *not* carry a
private copy of the interface assembly compiled against a different version — the crawler catches
`ReflectionTypeLoadException` and reports exactly this cause (`src/Xcaciv.Command.FileLoader/Crawler.cs:150-166`).

---

## 2. `ICommandDelegate` — the full tool contract

`src/Xcaciv.Command.Interface/ICommandDelegate.cs:22-69`

```csharp
public interface ICommandDelegate : IAsyncDisposable   // :22
{
    string Command { get; }                                                        // :28
    string RootCommand { get; }                                                    // :33
    IAsyncEnumerable<IResult<string>> Main(IIoContext ioContext, IEnvironmentContext env); // :49
    Dictionary<string, string> GetDefaultEnvironment();                            // :62
    List<ICommandParameter> GetParameters();                                       // :68
}
```

That is the **entire** interface — five members plus `IAsyncDisposable.DisposeAsync()`.

### 2.1 `string Command { get; }` (`:28`)
The invocation token. Contract: "Must be alphanumeric and contain no spaces." `AbstractCommand` derives it from
`[CommandRegister]` and normalizes with `NamesValidator` (see §3.1).

### 2.2 `string RootCommand { get; }` (`:33`)
The parent command when this is a sub-command (`GIT` for `GIT COMMIT`). **Empty string means "no root"**;
that is what every hand-written `ICommandDelegate` in the repo returns
(`src/Xcaciv.Command.Extensions.Commandline/CommandLineCommand.cs:29`,
`src/tests/Xcaciv.Command.Tests/TestImplementations/TestCommandsWithEnvironment.cs:17`).
WARNING: `AbstractCommand.RootCommand` **throws** if `[CommandRoot]` is absent — see §3.2 gotcha.

### 2.3 `IAsyncEnumerable<IResult<string>> Main(IIoContext, IEnvironmentContext)` (`:49`) — the async-enumerable output model

This is the whole output model. The command is an **async stream producer**:

* Each `yield return` emits **one discrete output chunk** as an `IResult<string>`.
* The host consumes the stream with `await foreach` and forwards each chunk
  (`src/Xcaciv.Command/CommandExecutor.cs:188-213`).
* **Streaming is real** — the enumerator is pulled lazily, so a long-running command's early chunks reach the next
  pipeline stage before the command finishes. Backpressure is applied by the bounded channel (§9).
* Chunk disposition by the host (`src/Xcaciv.Command/CommandExecutor.cs:190-212`):
  * `result == null` → skipped (`:190-193`).
  * `result.IsSuccess && !string.IsNullOrEmpty(result.Output)` → `ioContext.OutputChunk(result)` (`:195-201`).
    **Successful chunks with null/empty `Output` are silently dropped.**
  * `!result.IsSuccess` → recorded in a failure list, re-wrapped as
    `CommandResult<string>.Failure(message, result.Exception)` and emitted; `result.Exception?.ToString()` is written
    to the trace (`:203-212`).
* The environment passed in is a **child context**, isolated from the parent unless the command's registration says
  `ModifiesEnvironment = true` (`ICommandDelegate.cs:44-47`; enforcement at `CommandExecutor.cs:186,215-218`).

**Exceptions thrown out of `Main` are caught by the host** (`CommandExecutor.cs:223-230`): a failure chunk
`"Error executing {commandKey} (see trace for more info)"` is emitted, the status message is set to
`"**Error: " + ex.Message`, and `ex.ToString()` goes to the trace. The process is not torn down.

### 2.4 `Dictionary<string,string> GetDefaultEnvironment()` (`:62`)

Declares the environment variables this tool consumes **and their defaults**. Per the contract remarks (`:56-61`):

> Values are scoped in the global environment with the prefix of the command name. If `FETCH` returns
> `("TIMEOUT","30")`, the command reads `TIMEOUT` but the global store holds `FETCH_TIMEOUT`.

Mechanics:
* `CommandRegistry.GetEnvironment(ICommandFactory)` instantiates every registered command (root and sub), calls
  `GetDefaultEnvironment()`, and pushes the result into a fresh `ControllerEnvironmentContext` under the command name
  (`src/Xcaciv.Command/CommandRegistry.cs:88-142`). Each probe instance is disposed in a `finally`
  (`:133-141`).
* At execution, `ControllerEnvironmentContext.GetChild(commandName)` copies those values into the command's child
  environment **with the `COMMANDNAME_` prefix applied** (`src/Xcaciv.Command/ControllerEnvironmentContext.cs:119-133`).
  WARNING: So inside `Main`, the key you read is **`FETCH_TIMEOUT`**, not `TIMEOUT`.
  Return `new Dictionary<string,string>()` (the `AbstractCommand` default, `AbstractCommand.cs:275-278`) if you need none.

### 2.5 `List<ICommandParameter> GetParameters()` (`:68`)

Parameter metadata for help/host introspection. `AbstractCommand` auto-implements it from the attributes
(`AbstractCommand.cs:285-293`); hand-rolled commands typically return an empty list.

### 2.6 Disposal — `IAsyncDisposable`

`ICommandDelegate : IAsyncDisposable` (`:22`), so every command has `ValueTask DisposeAsync()`.
* `AbstractCommand.DisposeAsync()` returns `ValueTask.CompletedTask` and is `virtual` — override it to release
  resources (`AbstractCommand.cs:66-69`).
* **Who calls it:** `CommandRegistry.AddCommandDefaults` disposes the probe instance it creates for
  `GetDefaultEnvironment()` (`CommandRegistry.cs:133-141`).
  WARNING: **`CommandExecutor` does *not* dispose the command instance it executes** — inspect
  `CommandExecutor.ExecuteCommandWithErrorHandling` (`CommandExecutor.cs:169-256`): it `await using`s the *child
  environment* (`:186`) but never the `commandInstance` created at `:184`. Do not rely on the host to dispose your
  tool after a run; hold no unmanaged resource across an execution that you cannot also release inside `Main`.

### 2.7 The `HandlePipedChunk(IResult<string>)` signature introduced in 3.2.3

`HandlePipedChunk` is **not** on `ICommandDelegate`; it is an abstract member of `AbstractCommand`.
Exact current signature — `src/Xcaciv.Command.Core/AbstractCommand.cs:255`:

```csharp
public abstract IResult<string> HandlePipedChunk(
    IResult<string> pipedChunk,
    Dictionary<string, IParameterValue> parameters,
    IEnvironmentContext env);
```

* Before 3.2.3 the first parameter was `string` (`CHANGELOG.md:23-32`, `COMMAND_TEMPLATE.md:399-421`).
* Access the payload with `pipedChunk.Output` (nullable → use `?? string.Empty`), the status with
  `pipedChunk.IsSuccess`, and diagnostics with `pipedChunk.ErrorMessage` / `pipedChunk.Exception` /
  `pipedChunk.CorrelationId` / `pipedChunk.OutputFormat`.
* WARNING: **Important behavioural nuance the template gets wrong:** when you inherit `AbstractCommand`,
  `HandlePipedChunk` **never receives a failed chunk and never receives an empty chunk**. `AbstractCommand.Main`
  filters first (`AbstractCommand.cs:79-91`): failures are `yield return`ed straight through (`:81-85`) and
  `string.IsNullOrEmpty(pipedResult.Output)` chunks are `continue`d (`:87`). The `if (!pipedChunk.IsSuccess) return
  pipedChunk;` idiom in `COMMAND_TEMPLATE.md:326-333` is harmless but dead code under `AbstractCommand`.
  It *is* live if you implement `ICommandDelegate` directly and read `ReadInputPipeChunks()` yourself.

---

## 3. `AbstractCommand` — what you get free, what you must override

`src/Xcaciv.Command.Core/AbstractCommand.cs:12` — `public abstract class AbstractCommand : ICommandDelegate`.

### 3.1 Attribute-driven `Command` resolution (`:18-38`)

```csharp
public string Command {
    get {
        if (String.IsNullOrEmpty(_command)) {
            var registration = Attribute.GetCustomAttribute(GetType(), typeof(CommandRegisterAttribute))
                               as CommandRegisterAttribute;                      // :25
            if (registration == null)
                throw new InvalidOperationException("CommandRegisterAttribute is required for all commands"); // :28
            _command = registration.Command;                                     // :30
        }
        return _command;
    }
    set { _command = NamesValidator.GetValidCommandName(value); }                // :36  (uppercases)
}
```

Lazy, cached in `_command` (`:15`), and **throws `InvalidOperationException` if `[CommandRegister]` is missing**.

### 3.2 Attribute-driven `RootCommand` resolution (`:40-60`) — and its trap

Identical shape, reading `CommandRootAttribute`, and **throwing
`InvalidOperationException("CommandRootAttribute is required for all commands")` when the attribute is absent**
(`:50`).

WARNING — **Gotcha:** most commands are *not* sub-commands and carry no `[CommandRoot]`. Reading `.RootCommand` on such a
command throws. Nothing in the shipped host reads `ICommandDelegate.RootCommand`
(grep: only the declaration `ICommandDelegate.cs:33`, the `CommandLineCommand` implementation `:29`, and this
property), so the built-in `SAY`/`SET`/`ENV`/`REGIF` never trip it. But **your host must not call `.RootCommand`
on an `AbstractCommand` unless you know `[CommandRoot]` is present** — or you must set the `RootCommand` setter
(`:56-59`) first, which pre-populates `_rootCommand` and suppresses the throw.

### 3.3 The `Main` template method (`:71-101`) — the piped vs non-piped split

```csharp
public async IAsyncEnumerable<IResult<string>> Main(IIoContext io, IEnvironmentContext environment)
{
    var processedParameters = ProcessParameters(io);                    // :73
    if (io.HasPipedInput)                                               // :75
    {
        OnStartPipe(processedParameters, environment);                  // :77
        await foreach (var pipedResult in io.ReadInputPipeChunks())     // :79
        {
            if (!pipedResult.IsSuccess) { yield return pipedResult; continue; }   // :81-85
            if (string.IsNullOrEmpty(pipedResult.Output)) continue;               // :87
            yield return HandlePipedChunk(pipedResult, processedParameters, environment); // :89-90
        }
        OnEndPipe(processedParameters, environment);                    // :93
    }
    else
    {
        yield return HandleExecution(processedParameters, environment); // :98-99
    }
}
```

Consequences you must design around:
* **Non-piped path emits exactly one chunk.** If your tool needs to emit many chunks in non-piped mode, you must
  override `Main` yourself (legal — it is not `sealed`); `AbstractCommand` gives you no multi-chunk hook.
* **Piped path emits one chunk per surviving input chunk.** Return `CommandResult<string>.Success(string.Empty, …)`
  to swallow an input (the host drops empty successes, `CommandExecutor.cs:197`) — that is exactly how `REGIF`
  filters (`src/Xcaciv.Command/Commands/RegifCommand.cs:57`) and how `SET` stays silent
  (`src/Xcaciv.Command/Commands/SetCommand.cs:41`).
* `OnStartPipe` / `OnEndPipe` are `protected virtual` no-ops (`:262-273`) — use them to initialise/flush per-pipe
  state. They run **only on the piped path**.
* Dead code note: `var parameterArray = io.Parameters ?? Array.Empty<string>();` at `:97` is unused.

### 3.4 `ProcessParameters(IIoContext)` (`:109-129`)

```csharp
public Dictionary<string, IParameterValue> ProcessParameters(IIoContext io)
{
    var hasPipedInput = io.HasPipedInput;
    if (io.Parameters.Length == 0)
        return new Dictionary<string, IParameterValue>(StringComparer.OrdinalIgnoreCase);  // :112-115
    var commandParameters = new CommandParameters();                                       // :117
    var processedParameters = commandParameters.ProcessParameters(
        io.Parameters,
        GetOrderedParameters(hasPipedInput),
        GetFlagParameters(),
        GetNamedParameters(hasPipedInput),
        GetSuffixParameters(hasPipedInput));                                               // :118-123
    SetParameterFields(processedParameters, io);                                           // :126
    return processedParameters;
}
```

WARNING — **Critical gotcha at `:112-115`: when the command is invoked with *zero* arguments, `ProcessParameters` returns an
empty dictionary immediately.** No defaults are applied, no flags are materialised as `false`, and **no field
injection happens**. A tool whose parameters are all optional-with-defaults gets *nothing* when called bare.
Always code defensively:
`parameters.TryGetValue("x", out var p) && p.IsValid ? p.GetValue<string>() : "fallback"`.
(This is precisely why `examples/AllowedValuesExample.cs:44-46` writes `Configuration ?? "Debug"`.)

The dictionary is **`StringComparer.OrdinalIgnoreCase`** (`:114`, and `CommandParameters.cs:109`), so lookups by any
casing work even though the attribute lowercases stored names (§4.9).

### 3.5 Attribute collectors, and `UsePipe` filtering (`:186-237`)

| Method | Line | Behaviour |
|---|---|---|
| `GetOrderedParameters(bool hasPipedInput)` | `:186-195` | `Attribute.GetCustomAttributes(..., typeof(CommandParameterOrderedAttribute))`; when piped, drops entries with `UsePipe == true` |
| `GetNamedParameters(bool hasPipedInput)` | `:202-211` | same, for named |
| `GetFlagParameters()` | `:217-221` | flags are **never** filtered by pipe |
| `GetSuffixParameters(bool hasPipedInput)` | `:228-237` | same filtering as ordered/named |

`UsePipe = true` means "this parameter's value arrives from the pipe when we are piped, so do not demand it on the
command line." That is how `SET <key> <value>` becomes `… | SET <key>`
(`src/Xcaciv.Command/Commands/SetCommand.cs:14-15`) and `REGIF <regex> <string>` becomes `… | REGIF <regex>`
(`src/Xcaciv.Command/Commands/RegifCommand.cs:15-16`).

### 3.6 `OutputFormat` (`:14`)

```csharp
public ResultFormat OutputFormat { get; protected set; } = ResultFormat.General;
```

`protected set` → assign it from your constructor. Pass it to every result you build:
`CommandResult<string>.Success(value, this.OutputFormat)`. See §10 for what it actually does (metadata, not encoding).

### 3.7 What you MUST override

Exactly two abstract members:

```csharp
public abstract IResult<string> HandleExecution(
    Dictionary<string, IParameterValue> parameters, IEnvironmentContext env);          // :245
public abstract IResult<string> HandlePipedChunk(
    IResult<string> pipedChunk, Dictionary<string, IParameterValue> parameters, IEnvironmentContext env); // :255
```

WARNING: `examples/AllowedValuesExample.cs:32-52` declares `BuildCommand : AbstractCommand` overriding **only**
`HandleExecution` — **that example does not compile** against the current abstract class.

### 3.8 What you MAY override

| Member | Line | Default |
|---|---|---|
| `ValueTask DisposeAsync()` | `:66-69` | `ValueTask.CompletedTask` |
| `void OnStartPipe(Dictionary<string,IParameterValue>, IEnvironmentContext)` | `:262-264` | no-op, `protected virtual` |
| `void OnEndPipe(...)` | `:271-273` | no-op, `protected virtual` |
| `Dictionary<string,string> GetDefaultEnvironment()` | `:275-278` | empty dictionary, `public virtual` |
| `List<ICommandParameter> GetParameters()` | `:285-293` | aggregates Ordered + Flag + Named + Suffix attributes (all with `hasPipedInput:false`), `public virtual` |
| `IAsyncEnumerable<IResult<string>> Main(...)` | `:71` | the template method above — override only for full control |
| `string Command` / `string RootCommand` setters | `:34-37`, `:56-59` | override the attribute-derived value |

---

## 4. Every attribute in `Xcaciv.Command.Interface/Attributes`

All seven are declared in `src/Xcaciv.Command.Interface/Attributes/`. **All of them target `AttributeTargets.Class`.**
None targets properties or fields.

> WARNING — **The single largest documentation error in the repo:** `docs/learn/api-attributes.md` claims
> `[AttributeUsage(AttributeTargets.Property)]` for every parameter attribute (`:136, :207, :255, :304`) and shows them
> decorating properties. That is **wrong**. `docs/learn/getting-started-create-command.md:23,58-101` repeats the error.
> Source: `CommandParameterOrderedAttribute.cs:13`, `CommandParameterNamedAttribute.cs:9`, `CommandFlagAttribute.cs:13`,
> `CommandParameterSuffixAttribute.cs:9` — **all `AttributeTargets.Class, AllowMultiple = true, Inherited = false`.**

### 4.1 `AbstractCommandParameterAttribute` (base class)

`src/Xcaciv.Command.Interface/Attributes/AbstractCommandParameterAttribute.cs:9`

```csharp
public abstract class AbstractCommandParameterAttribute : Attribute, ICommandParameter
```

It implements `ICommandParameter` (`src/Xcaciv.Command.Interface/ICommandParameter.cs:11-58`), so the attribute
instance *is* the parameter descriptor used for help and validation.

| Member | Line | Accessor | Default | Meaning |
|---|---|---|---|---|
| `ParameterIndication Indication` | `:11` | `{ get; init; }` | `ParameterIndication.NAMED` | How the parameter is written on the command line. Each concrete subclass overwrites it in its ctor. |
| `bool IsRequired` | `:12` | `{ get; init; }` | `false` | When true and unsatisfied → `ArgumentException("Missing required parameter {Name}")` at parse time. |
| `string ValueDescription` | `:18` | `{ get; set; }` | `String.Empty` | Human help text for the value. Set from ctor arg 2. |
| `string DefaultValue` | `:24-32` | `{ get; set; }` (validating) | `String.Empty` | Value used when absent. **Setting it runs `ValidateDefaultValue` against `AllowedValues`** (`:29`). Satisfies `IsRequired` for named parameters (`CommandParameters.cs:196-203`). |
| `string Name` | `:36-40` | `{ get; set; }` (normalizing) | `"TODO"` (`_helpName`, `:14`) | Identifier. Setter runs `NamesValidator.GetValidCommandName(value, false)` → **strips non `[-_0-9A-Za-z ]` chars and LOWERCASES** (`:39`). |
| `Type DataType` | `:44` | `{ get; set; }` | `typeof(string)` | Target conversion type. Must be supported by the converter (§5.5) or `ValidateAndConvert` throws `ArgumentException`. |
| `string[] AllowedValues` | `:51-70` | `{ get; init; }` | `Array.Empty<string>()` | Allow-list, case-insensitive. See semantics below. |
| `string ShortAlias` | `:91` | `{ get; set; }` | `String.Empty` | Alternate token, e.g. `-u` for `-username`. Matched by `-{1,2}alias` regex for named params and flags (`CommandParameters.cs:141-142, 174-175`). |
| `string CommandPrototype` | `:93` | `{ get; set; }` | `String.Empty` | Per-parameter prototype string. **Declared on `ICommandParameter:52` but never read by the framework** (grep: no consumer). |
| `bool UsePipe` | `:95` | `{ get; set; }` | `false` | "This parameter is fed by the pipe." When `HasPipedInput`, `AbstractCommand` excludes it from parsing (§3.5). `ICommandParameter.cs:54-57`: *"There can only be one parameter with this flag set to true per command"* — **not enforced anywhere in code**; it is a convention. |
| `override string ToString()` | `:101-107` | | | `$"{GetIndicator(),-18} {GetValueDescription()}".Trim()` — the help line. |
| `virtual string GetIndicator()` | `:109-112` | | | Base: `$"<{_helpName}>"`. |
| `virtual string GetValueDescription()` | `:114-117` | | | Base: `ValueDescription`. |

**`AllowedValues` init semantics (`:51-70`) — read carefully:**
1. `_allowedValues = value ?? []` (`:56`).
2. **Auto-default:** if the list is non-empty *and* `_defaultValue` is empty, `_defaultValue = _allowedValues[0]`
   (`:58-62`). The first allowed value silently becomes the default.
3. **Validation:** if `_defaultValue` is non-empty it is checked against the list
   `StringComparer.OrdinalIgnoreCase`; failure throws
   `ArgumentException($"Default value '{d}' is not in the allowed values list for parameter '{Name}'. Allowed values: …")`
   (`:64-68`, `:72-85`).
4. Because `DefaultValue`'s setter *also* validates (`:29`), **the check is order-independent inside an object
   initializer / attribute named-argument list**.
5. WARNING: `AllowedValues` is **`init`-only**. `docs/AllowedValues-validation-feature.md:83-88` shows
   `param.AllowedValues = new[]{...};` *after* construction — that does not compile.

### 4.2 `CommandRegisterAttribute` — REQUIRED on every command class

`src/Xcaciv.Command.Interface/Attributes/CommandRegisterAttribute.cs`

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]  // :9
public class CommandRegisterAttribute : Attribute
{
    public CommandRegisterAttribute(string command, string description);           // :17-21
    public string Command { get; set; }   // :26-30  setter: NamesValidator.GetValidCommandName(value) → UPPERCASE
    public string Version { get; set; } = "0.0.0";                                  // :34
    public string Description { get; set; }                                         // :38
    public string Prototype { get; set; } = String.Empty;                           // :43
    public string Alias { get; set; } = String.Empty;                               // :48
}
```

* **Constructor args:** `(string command, string description)` — **two positional args, no `prototype` parameter.**
  WARNING: `docs/learn/api-attributes.md:13-16` invents a third ctor arg `string prototype = "todo"`. It does not exist;
  `Prototype` is a named property.
* `Command` is normalized to **UPPERCASE** by `NamesValidator.GetValidCommandName(value)` (default `upper: true`,
  `src/Xcaciv.Command.Interface/NamesValidator.cs:31-43`). Uses the C# 14 `field` keyword (`:29`).
* `Alias` and `Version` are stored but **never read by the framework** (grep across `src/`): they are metadata only.
* `Prototype` drives help: `HelpService.BuildHelp` prints `baseCommand.Prototype` unless it equals the literal
  `"todo"` (case-insensitive), in which case it synthesises a prototype from the parameter indicators
  (`src/Xcaciv.Command/HelpService.cs:93-100`).
  WARNING: The *default* is `String.Empty`, **not** `"todo"` — so leaving `Prototype` unset prints a blank usage line
  rather than auto-generating one. Set `Prototype = "todo"` explicitly to get auto-generation, or write a real one.
* Missing this attribute:
  * `AbstractCommand.Command` throws `InvalidOperationException` (`AbstractCommand.cs:28`).
  * `CommandParameters.CreatePackageDescription` throws
    `InvalidOperationException($"{type.FullName} implements ICommandDelegate but does not have BaseCommandAttribute…")`
    (`src/Xcaciv.Command.Core/CommandParameters.cs:88`).
  * `CommandRegistry.AddCommand(string, Type, bool)` traces a warning and **silently returns without registering**
    (`src/Xcaciv.Command/CommandRegistry.cs:42-47`).

### 4.3 `CommandRootAttribute` — sub-command grouping

`src/Xcaciv.Command.Interface/Attributes/CommandRootAttribute.cs`

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]  // :9
public class CommandRootAttribute : Attribute
{
    public CommandRootAttribute(string command = "", string description = "");     // :17-21  (both optional)
    public string Command { get; set; } = String.Empty;  // :26-30  setter → UPPERCASE via NamesValidator
    public string Description { get; set; } = "TODO";                               // :34
    public string Alias { get; set; } = String.Empty;                               // :39
}
```

Applying `[CommandRoot("do","does stuff")]` **plus** `[CommandRegister("SAY", …)]` registers the class as
sub-command `SAY` under root `DO`, invoked as `do say <text>`
(worked example: `src/tests/zTestCommandPackage/DoSayCommand.cs:13-16`).

Registration mechanics — `CommandParameters.CreatePackageDescription` (`CommandParameters.cs:35-91`):
* Both names are re-normalized to uppercase (`:43-44`).
* If no `ICommandDescription` exists yet for the root, a synthetic root description is created whose
  `FullTypeName` is **empty** and whose `SubCommands` dictionary (OrdinalIgnoreCase, `:53`) holds this class (`:48-63`).
* Otherwise the sub-command is merged into the existing root (`:66-74`).
* `CommandRegistry.AddCommand(ICommandDescription)` merges sub-command dictionaries when the root already exists
  (`CommandRegistry.cs:24-35`).

Dispatch: `CommandFactory.CreateCommand` treats `Parameters[0]` as the sub-command key, normalizes it uppercase, and
**shifts it off the parameter array** via `SetParameters(Parameters[1..])` before instantiating
(`src/Xcaciv.Command/CommandFactory.cs:43-73`). Unknown sub-command →
`InvalidOperationException` listing the available ones (`:66-72`). Root invoked with no sub-command →
`InvalidOperationException($"Command '{X}' requires a sub-command. Available sub-commands: …")` (`:78-86`).

WARNING: `CommandFactory.CreateCommandAsync` (`:159-176`) uses `ioContext.Parameters[0].ToUpper()` instead of
`NamesValidator.GetValidCommandName`, and on a *miss* falls through to
`CreateCommand(commandDescription.FullTypeName, …)` — which for a synthetic root is empty and throws
`InvalidOperationException("Command type name is empty.")` (`:99-102`). The async path is therefore less forgiving
than the sync path; this is the path the executor actually uses (`CommandExecutor.cs:184`).

### 4.4 `CommandParameterOrderedAttribute` — positional

`src/Xcaciv.Command.Interface/Attributes/CommandParameterOrderedAttribute.cs`

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]  // :13
public class CommandParameterOrderedAttribute : AbstractCommandParameterAttribute
{
    public CommandParameterOrderedAttribute(string name, string description)       // :22-29
    {
        Name = name; ValueDescription = description;
        IsRequired = true;                       // :26  <-- REQUIRED BY DEFAULT
        Indication = ParameterIndication.ORDERED;// :28
    }
    public override string GetValueDescription();// :30-38  appends " (Allowed values: a, b, c)"
}
```

* **`IsRequired` defaults to `true`** for ordered parameters (opposite of the base class). Opt out with
  `IsRequired = false`.
* `GetIndicator()` is **not** overridden → `<name>` (base, `AbstractCommandParameterAttribute.cs:111`).
* Ordered parameters are consumed **in declaration order**, from the front of the argument list
  (`CommandParameters.cs:219-268`); they must therefore precede named/flag arguments on the command line
  (class doc: `CommandParameterOrderedAttribute.cs:10-12`).

### 4.5 `CommandParameterNamedAttribute` — `-name value`

`src/Xcaciv.Command.Interface/Attributes/CommandParameterNamedAttribute.cs`

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]  // :9
public class CommandParameterNamedAttribute : AbstractCommandParameterAttribute
{
    public CommandParameterNamedAttribute(string name, string description);        // :12-16
    public override string GetIndicator()      => $"-{_helpName}";                 // :18-21
    public override string GetValueDescription();                                  // :23-31  appends allowed values
}
```

* `Indication` stays `NAMED` (base default). `IsRequired` stays `false`.
* Matched by regex `-{1,2}{Name}` and `-{1,2}{ShortAlias}`, **case-insensitive**, and the token must literally start
  with `-` (`CommandParameters.cs:173-182`). So `-verbosity`, `--verbosity`, `-VERBOSITY` all match.
* The **next** array element is consumed as the value; both tokens are removed from the working list (`:184-189`).
  WARNING: If the flag is the *last* token with no value after it, `parameterList[valueIndex]` throws
  `ArgumentOutOfRangeException` (`:185`) — there is no friendly error for a dangling named parameter.

### 4.6 `CommandFlagAttribute` — presence = true

`src/Xcaciv.Command.Interface/Attributes/CommandFlagAttribute.cs`

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]  // :13
public class CommandFlagAttribute : AbstractCommandParameterAttribute
{
    public CommandFlagAttribute(string name, string description)                   // :22-27
    { Name = name; ValueDescription = description; Indication = ParameterIndication.FLAG; }
    public override string GetIndicator()       => $"-{_helpName}";                // :29-32
    public override string GetValueDescription()=> $"Flag: {ValueDescription}";    // :34-37
}
```

Runtime behaviour (`CommandParameters.ProcessFlags`, `CommandParameters.cs:132-160`):
* Same `-{1,2}name` / `-{1,2}alias` case-insensitive matching (`:140-142`), token must start with `-` (`:148`).
* Present → the matched token is removed from the list; absent → nothing removed.
* **A flag is ALWAYS materialised in the dictionary**, as `"true"` or `"false"`, and **always with
  `typeof(bool)`** (`:157-158`) — `DataType`, `DefaultValue`, `IsRequired` and `AllowedValues` on a flag attribute
  are **ignored** by the parser. (Declaring `DataType = typeof(bool)` as
  `src/tests/Xcaciv.Command.Tests/Commands/TestSubCommand.cs:19` does is harmless but redundant.)
* Read with `parameters["flagname"].GetValue<bool>()`.

### 4.7 `CommandParameterSuffixAttribute` — capture the rest

`src/Xcaciv.Command.Interface/Attributes/CommandParameterSuffixAttribute.cs`

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]  // :9
public class CommandParameterSuffixAttribute : AbstractCommandParameterAttribute
{
    public CommandParameterSuffixAttribute(string name, string description)        // :12-18
    { Name = name; ValueDescription = description; Indication = ParameterIndication.SUFFIX; }
    public override string GetValueDescription();                                  // :19-27  appends allowed values
}
```

Runtime behaviour (`CommandParameters.ProcessSuffixParameters`, `CommandParameters.cs:273-316`):
* Consumes **all remaining tokens joined with a single space** into one string value, then clears the list
  (`:312-314`). It is a **`string`, not a `string[]`** —
  WARNING: `docs/learn/api-attributes.md:352` and `getting-started-create-command.md:101` show `public string[] Args` /
  `string[] Messages`. Wrong: the value is one joined string.
* Empty list → uses `DefaultValue` when `!IsRequired && DefaultValue != ""` (`:283-287`), else throws
  `ArgumentException("Missing required parameter …")` when `IsRequired` (`:288-291`), else skipped entirely (`:292`).
* If the next token starts with `-`, the suffix parameter takes its `DefaultValue` (or throws if required) and the
  tokens are left alone (`:297-309`).
* WARNING: **`AllowedValues` is NOT enforced for suffix parameters.** The method has no allow-list check, unlike ordered
  (`:258-262`) and named (`:206-210`). `docs/parameter-help-text-consistency.md:33-38,74-79` implies suffix
  allow-lists give "validation" — they only change the *help text*.
* `GetIndicator()` is not overridden → `<name>`.
* Because a suffix eats everything, declare **at most one**, and declare it last.

### 4.8 `CommandHelpRemarksAttribute`

`src/Xcaciv.Command.Interface/Attributes/CommandHelpRemarksAttribute.cs`

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]  // :9
public class CommandHelpRemarksAttribute : Attribute
{
    public CommandHelpRemarksAttribute(string remarks);                            // :16-19
    public string Remarks { get; set; }                                            // :23
}
```

Purely a help artefact. `HelpService.BuildHelp` emits a `Remarks:` section with a blank line before each remark
(`src/Xcaciv.Command/HelpService.cs:108-116`). Apply it multiple times — see `SayCommand`
(`src/Xcaciv.Command/Commands/SayCommand.cs:16-17`).

### 4.9 Name normalization — the rule that bites

| Attribute property | Normalizer | Result |
|---|---|---|
| `CommandRegisterAttribute.Command` | `NamesValidator.GetValidCommandName(value)` (`CommandRegisterAttribute.cs:29`) | **UPPERCASE** |
| `CommandRootAttribute.Command` | same (`CommandRootAttribute.cs:29`) | **UPPERCASE** |
| `AbstractCommandParameterAttribute.Name` | `NamesValidator.GetValidCommandName(value, false)` (`AbstractCommandParameterAttribute.cs:39`) | **lowercase** |

`NamesValidator.GetValidCommandName` (`src/Xcaciv.Command.Interface/NamesValidator.cs:31-43`): trims, takes the text
before the first space, trims leading/trailing `-`, deletes every character not matching `[-_\da-zA-Z ]`
(compiled regex `:17`), then upper/lowercases.

WARNING: **So a parameter named `"Key"` is stored as `"key"`.** It doesn't matter for dictionary lookups (the dictionary is
`OrdinalIgnoreCase`) or for field injection (case-insensitive `TryGetValue`), but it *does* show up in help text as
lowercase, and it means a parameter name containing e.g. `.` or `:` is silently mangled.

---

## 5. The type-safe parameter system

### 5.1 `IParameterValue` / `IParameterValue<out T>`

`src/Xcaciv.Command.Interface/Parameters/IParameterValue.cs`

```csharp
public interface IParameterValue<out T> : IParameterValue { T GetValue(); }        // :6-12
public interface IParameterValue                                                    // :17
{
    string  Name            { get; }   // :22
    Type    DataType        { get; }   // :27  declared parameter type, non-null
    string  RawValue        { get; }   // :32  the original string token
    object? UntypedValue    { get; }   // :37  boxed converted value (or InvalidParameterValue)
    string? ValidationError { get; }   // :42
    bool    IsValid         { get; }   // :47
    T    GetValue<T>();                // :52
    bool TryGetValue<T>(out T value);  // :57
}
```

WARNING: `RawValue` **is public on the interface** (`:32`) despite `CHANGELOG.md:93` claiming "Removed
`IParameterValue.RawValue` — Made internal to prevent bypassing type system". It is public and is used by tests
(`src/tests/Xcaciv.Command.Tests/Commands/FieldInjectionTestCommand.cs:70`). Treat the changelog as aspirational.

### 5.2 `AbstractParameterValue<T>` — the conversion/diagnostics engine

`src/Xcaciv.Command.Interface/Parameters/AbstractParameterValue.cs:6`

Constructor `(string name, string raw, object? value, bool isValid, string? validationError)` (`:20-31`):
throws `ArgumentNullException` on blank `name` (`:22-24`); sets `DataType = typeof(T)` (`:30`).

**`GetValue<TResult>()` (`:38-79`) — the exact failure ladder:**

1. `!IsValid` → **`InvalidOperationException`** (`:42-48`):
   ```
   Cannot access parameter '{Name}' due to validation error: {ValidationError}
   Raw value: '{RawValue}'
   Expected type: {DataType.Name}
   Hint: only access value if valid.
   ```
2. `DataType != typeof(TResult)` → **`InvalidCastException`** (`:51-59`):
   ```
   Type mismatch for parameter '{Name}':
     Stored as: {DataType.Name}
     Requested as: {TResult.Name}
     Raw value: '{RawValue}'
   Hint: Ensure you're requesting the correct type.
   ```
   WARNING: This is an **exact type equality** check. `GetValue<int>()` on a parameter declared
   `DataType = typeof(long)` throws even though the value would widen fine.
3. `UntypedValue == null` → last-chance `TryConvert(typeof(TResult), …)` (`:61-64`).
4. `UntypedValue is TResult` → return it (`:66-69`).
5. Otherwise **`InvalidCastException`** with the richest message (`:71-78`), naming the *actual* stored type, the
   value, the requested type, the declared `DataType`, and the raw string.

**`TryGetValue<TResult>(out TResult)` (`:81-103`)** — never throws: returns `false` when `!IsValid` or
`UntypedValue is InvalidParameterValue` (`:85-88`); tries `TryConvert` on null (`:90-94`); pattern-matches (`:96-100`).
Note it does **not** apply the strict `DataType == typeof(TResult)` gate, which is why field injection (§5.7) can
target a field whose type differs from `DataType` and still succeed.

**`TryConvert(Type, out object?)` (`:115-157`)** — private three-step fallback on `RawValue`:
1. reflection over a static `TryParse(string, out T)` (`:120-134`);
2. `TypeDescriptor.GetConverter(targetType).ConvertFrom(RawValue)` inside try/catch (`:137-146`);
3. `Convert.ChangeType(RawValue, targetType)` inside try/catch (`:149-154`).

### 5.3 The concrete `Parameter*` types

All in `src/Xcaciv.Command.Interface/Parameters/`, all primary-constructor one-liners deriving
`AbstractParameterValue<T>`, all with signature `(string name, string raw, T value, bool isValid, string validationError)`:

| Type | File:line | `T` |
|---|---|---|
| `ParameterString` | `ParameterString.cs:7` | `string` |
| `ParameterBool` | `ParameterBool.cs:7` | `bool` |
| `ParameterLong` | `ParameterLong.cs:7` | `long` |
| `ParameterDecimal` | `ParameterDecimal.cs:7` | `decimal` |
| `ParameterDouble` | `ParameterDouble.cs:7` | `double` |
| `ParameterFloat` | `ParameterFloat.cs:7` | `float` |
| `ParameterGuid` | `ParameterGuid.cs:7` | `Guid` |
| `ParameterDateTime` | `ParameterDateTime.cs:7` | `DateTime` |
| `ParameterJson` | `ParameterJson.cs:8` | `System.Text.Json.JsonElement` |

WARNING: **The framework never instantiates any of them.** Both factories construct
`ParameterValue<T>` (`src/Xcaciv.Command.Interface/Parameters/ParameterValue.cs:8`) via
`typeof(ParameterValue<>).MakeGenericType(dataType)`
(`ParameterValueFactory.cs:28`, `Core/Parameters/ParameterValueFactoryCaching.cs:51-52`).
The `Parameter*` classes exist as convenience/strong-typed constructors for hand-built values (and note there is
**no `ParameterInt`** even though `int` is a supported `DataType`). Do not type-test for them; test
`IParameterValue<T>` or just call `GetValue<T>()`.

### 5.4 `InvalidParameterValue`

`src/Xcaciv.Command.Interface/Parameters/InvalidParameterValue.cs:7-17` — sealed singleton sentinel,
`InvalidParameterValue.Instance` (`:14`), `ToString() => "[Invalid Parameter Value]"` (`:16`).
It occupies `UntypedValue` when conversion fails, so the parameter object stays non-null and correctly typed while
signalling invalidity. `TryGetValue` explicitly rejects it (`AbstractParameterValue.cs:85`).

### 5.5 `IParameterConverter` and `DefaultParameterConverter` — the conversion & validation pipeline

Contract: `src/Xcaciv.Command.Interface/Parameters/IParameterConverter.cs:8-52`
(`CanConvert` `:15`, `Convert` `:23`, `ConvertWithValidation` `:32`, `ValidateAndConvert` `:46`, generic
`ValidateAndConvert<T>` `:51`), plus the `ParameterConversionResult` DTO (`:57-95`).

Implementation: `src/Xcaciv.Command.Core/Parameters/DefaultParameterConverter.cs:10`.

**Supported types** (`:12-24`): `string, int, long, double, float, decimal, bool, Guid, DateTime, JsonElement`.
`CanConvert` unwraps `Nullable<T>` first (`:32`). WARNING: **`DateTimeOffset` and `TimeSpan` are NOT supported**, contrary
to `CHANGELOG.md:74`.

**`Convert(string, Type)` (`:37-150`) — exact rules:**
* `targetType == null` → failure result "Target type cannot be null." (`:39-41`).
* **Empty/null input for any non-`string` target → failure "Cannot convert empty string to non-string type."**
  (`:42-45`). WARNING: Practical consequence: an *absent, non-required, no-default* named `int` parameter is created with
  `rawValue == ""` and comes back **invalid** — always guard with `IsValid`.
* `string` → passthrough (`:50-53`).
* `int` / `long`: `TryParse(NumberStyles.Integer, CultureInfo.InvariantCulture)` (`:59-72`).
* `float` / `double` / `decimal`: `TryParse(NumberStyles.Float, InvariantCulture)` (`:74-96`).
* `bool`: `bool.TryParse` first, then the extra literals `1|yes|on|true` → `true`, `0|no|off|false` → `false`
  (case-insensitive) (`:99-112`).
* `Guid`: `Guid.TryParse` (`:115-120`).
* `DateTime`: `DateTime.TryParse(value, InvariantCulture, DateTimeStyles.None, …)` (`:123-128`).
* `JsonElement`: `JsonDocument.Parse(value).RootElement.Clone()`; `JsonException` → failure with the parser message
  (`:131-142`).
* Anything else → `"Unsupported type '{Name}'."` (`:144`). Any thrown exception → `"Conversion failed: {msg}"` (`:146-149`).
* **All numeric parsing is InvariantCulture** — `"1,5"` is not a valid decimal here.

**`ConvertWithValidation` (`:152-181`)** — `string` passthrough (`:160-163`); on failure sets `error` and returns
`InvalidParameterValue.Instance` (`:167-172`); on a null success value returns `GetDefaultValue(targetType)`
(`:174-178` → `Activator.CreateInstance` for value types, `InvalidParameterValue.Instance` for reference types,
`:246-257`).

**`ValidateAndConvert(name, raw, type, out validationError, out isValid)` (`:183-222`)** — the method the parser
actually calls:
* Throws `ArgumentException` for blank `parameterName` (`:185-186`), `ArgumentNullException` for null type (`:188-189`).
* Throws **`ArgumentException($"Converter does not support type '{X}' for parameter '{p}'.")`** when
  `!CanConvert(targetType)` (`:191-194`). WARNING: This is a *hard throw out of parameter processing*, i.e. a tool that
  declares `DataType = typeof(TimeSpan)` fails the whole command, not just that parameter.
* `isValid = (error == null)`; `validationError = error ?? string.Empty` (`:197-198`).
* Post-conversion **type-safety audit**: if the produced object's type is neither the target nor assignable to it nor
  the `Nullable` underlying type → **`InvalidOperationException("Type safety violation: Converter returned X but
  parameter 'p' expects Y.")`** (`:200-218`).
* Returns `convertedValue ?? InvalidParameterValue.Instance` (`:221`).

`ValidateAndConvert<T>` (`:224-241`) is the compile-time-typed wrapper; returns `default!` when invalid (`:228-231`).

### 5.6 Factories (creation + caching)

`IParameterValueFactory.Create(name, raw, value, dataType, isValid, validationError)` →
`src/Xcaciv.Command.Interface/Parameters/IParameterValueFactory.cs:21`.

| Implementation | File | Behaviour |
|---|---|---|
| `ParameterValueFactory` (**default**) | `src/Xcaciv.Command.Interface/Parameters/ParameterValueFactory.cs:10` | `MakeGenericType` + `Activator.CreateInstance` on **every** call (`:28-32`); throws `ArgumentNullException` for null `dataType` (`:25-26`). |
| `ParameterValueFactoryCaching` | `src/Xcaciv.Command.Core/Parameters/ParameterValueFactoryCaching.cs:14` | Two `ConcurrentDictionary` caches: constructed generic types (`:20`) and factory delegates (`:26`). Fast path hits `_factoryCache` (`:45-48`). **Bounded to 100 type entries** (`:71-74`) — beyond that it still works, just uncached. Unwraps `TargetInvocationException` to preserve exception semantics (`:63-67`). `ClearCache()` at `:82-86`. |

WARNING: `src/Xcaciv.Command.Interface/Parameters/ParameterValueFactoryCaching.cs` is a **0-byte file** — the caching
factory lives only in `Xcaciv.Command.Core`, namespace `Xcaciv.Command.Core.Parameters`.

Companion: `NamesValidatorCaching` (`src/Xcaciv.Command.Core/Parameters/NamesValidatorCaching.cs:13`) — static
`ConcurrentDictionary` keyed `"{commandLine}|{upper}"` (`:19,31`), **bounded to 1000 entries** (`:41-44`), delegating
to `NamesValidator`; `GetArgumentsFromCommandline` is deliberately **not** cached (`:55-60`); `ClearCache()` at `:65-68`.
Nothing in the shipped host wires either caching type in — you inject them yourself
(`new CommandParameters(converter, new ParameterValueFactoryCaching())`, `CommandParameters.cs:25-29`).

### 5.7 Parameter **field injection**

Docs: `docs/parameter-field-injection-implementation.md`, `docs/parameter-field-injection-refactoring.md`,
`docs/examples/parameter-field-injection-example.md`. **Authoritative implementation:**
`src/Xcaciv.Command.Core/AbstractCommand.cs:136-179`.

```csharp
private void SetParameterFields(Dictionary<string, IParameterValue> processedParameters, IIoContext io)
{
    if (processedParameters == null || processedParameters.Count == 0) return;            // :138-139
    var publicFields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);  // :144
    if (publicFields.Length == 0) return;                                                 // :146-147
    foreach (var field in publicFields)
    {
        if (processedParameters.TryGetValue(field.Name, out var parameterValue)           // :152  (OrdinalIgnoreCase)
            && parameterValue != null && parameterValue.IsValid)                          // :153-154
        {
            try {
                var getValue = typeof(IParameterValue).GetMethod(nameof(IParameterValue.TryGetValue)); // :159
                var generic  = getValue.MakeGenericMethod(field.FieldType);                // :162
                var args = new object?[] { null };
                var success = (bool)generic.Invoke(parameterValue, args)!;                 // :164
                if (success && args[0] != null) field.SetValue(this, args[0]);             // :166-169
            }
            catch (Exception) {
                io.AddTraceMessage($"Failed to set field '{fieldName}' from parameter.").Wait(); // :175
            }
        }
    }
}
```

Rules a tool author must know:
1. **Only `public` instance FIELDS.** Not properties, not private/protected fields
   (`BindingFlags.Public | BindingFlags.Instance`, `:144`).
   WARNING: Every shipped doc example that decorates/injects a **property** (`docs/learn/api-attributes.md`,
   `docs/learn/getting-started-create-command.md`) is wrong. Correct usage is
   `examples/AllowedValuesExample.cs:34-37` and
   `src/tests/Xcaciv.Command.Tests/Commands/FieldInjectionTestCommand.cs:22-27`:
   ```csharp
   public string? FirstParam;   // a field, no { get; set; }
   public bool    FlagParam;
   ```
2. **Match is by field name vs parameter key, case-insensitive** (the dictionary comparer, `:114`/`CommandParameters.cs:109`).
3. It uses **`TryGetValue<TFieldType>`**, not `GetValue<T>` — so the strict `DataType == typeof(T)` gate is bypassed,
   and injection silently no-ops when types don't line up.
4. **Only valid parameters are injected** (`:154`); invalid ones leave the field at its declared default.
5. **`null` results are never assigned** (`:166`) — a field keeps its initialiser when the value is null.
6. **Failures are swallowed** and reported only as a trace message (`:173-176`) — the command still runs.
   Note the `.Wait()` on `:175`: a synchronous block inside parameter processing.
7. Runs **once per `ProcessParameters` call**, i.e. once per `Main` invocation, **before** `HandleExecution` /
   `OnStartPipe` / any `HandlePipedChunk` — so fields are populated in all three (`AbstractCommand.cs:73,126`).
8. **Zero arguments ⇒ zero injection** (§3.4 early return at `:112-115`).
9. **The dictionary is still authoritative.** Fields are a convenience; the `parameters` argument always carries
   everything.
10. Historical note: the docs say field injection lives in `CommandFactory`
    (`docs/examples/parameter-field-injection-example.md:5-13`). It was **moved to `AbstractCommand`**
    (`docs/parameter-field-injection-refactoring.md:1-41`) and that is where it is today. `CommandFactory` no longer
    touches fields (verify: `src/Xcaciv.Command/CommandFactory.cs` has no `SetValue`/`GetFields`).

### 5.8 The parser: `CommandParameters`

`src/Xcaciv.Command.Core/CommandParameters.cs:15`. Constructor
`CommandParameters(IParameterConverter? converter = null, IParameterValueFactory? factory = null)` defaults to
`DefaultParameterConverter` + `ParameterValueFactory` (`:25-29`).

`ProcessParameters(string[] parameters, ordered[], flags[], named[], suffix[])` (`:102-118`) runs the phases in a
**fixed order against a mutable working list** (`:110-115`):

1. **Ordered** (`ProcessOrderedParameters`, `:219-268`) — for each declared ordered attribute, in declaration order:
   * empty list → use `DefaultValue` when `!IsRequired && DefaultValue != ""` (`:229-233`); throw
     `ArgumentException("Missing required parameter {Name}")` when `IsRequired` (`:234-237`); else skip (`:238`).
   * take `parameterList[0]`; if it is empty and a `DefaultValue` exists, substitute it (`:242-245`).
   * **if the token starts with `-`**, treat it as a named/flag token: do **not** consume it; throw only if the
     parameter is required and has no default (`:249-255`). *(Comment at `:247-248`: this precludes negative numbers
     as positional values.)*
   * else enforce `AllowedValues` (`OrdinalIgnoreCase`) → `ArgumentException("Invalid value for parameter {Name}, this
     parameter has an allow list.")` (`:258-262`); create the value; **`RemoveAt(0)`** (`:264-265`).
2. **Flags** (`:132-160`) — see §4.6. Always writes a `bool` entry.
3. **Named** (`:165-214`) — see §4.5. Missing → `DefaultValue` if non-empty (`:196-199`), else throw when
   `IsRequired` (`:200-203`). Allow-list enforced (`:206-210`). Value created with `parameter.DataType` (`:212`).
   WARNING: the allow-list check runs **even when the parameter was absent and has no default**, so an absent
   allow-listed parameter with an empty default throws "Invalid value … allow list" — but in practice
   `AllowedValues` auto-populates `DefaultValue` (§4.1), so this only bites if you set `DefaultValue = ""` explicitly.
4. **Suffix** (`:273-316`) — see §4.7.

Every value goes through `CreateParameterValue` → `_converter.ValidateAndConvert(...)` → `_factory.Create(...)`
(`:123-127`).

**Where parse errors surface:** `ProcessParameters` is called at the top of `AbstractCommand.Main` (`:73`), inside the
`await foreach` the executor drives, so an `ArgumentException` from a missing/invalid parameter is caught by
`CommandExecutor.ExecuteCommandWithErrorHandling` (`CommandExecutor.cs:223-230`) and surfaces as a failure chunk
`"Error executing {command} (see trace for more info)"` plus a trace entry — **the user does not see the specific
"Missing required parameter X" text on stdout**, only in the trace. Plan your `ValueDescription`/help accordingly.

### 5.9 `ParameterCollection` and `ParameterCollectionBuilder` (secondary API)

`ParameterCollection` (`src/Xcaciv.Command.Interface/Parameters/ParameterCollection.cs:11`) — an
`IEnumerable<IParameterValue>` keyed `OrdinalIgnoreCase` (`:17`) with `Count` (`:35`), `Add` (`:40,51`),
`Contains` (`:62`), `GetParameter` (nullable, `:70`), `GetParameterRequired`/`Get` (throw `KeyNotFoundException`,
`:81,92`), `TryGet` (`:103`), `GetValue<T>` (throws `InvalidOperationException` on invalid, `:117-130`),
`GetAsValueType<T>` (`:135`), `GetValueOrDefault<T>` (`:144-153`), `GetNames` (`:158`),
`GetInvalidParameters` (`:166`), `IsValid` / `AreAllValid()` (`:174,179`), and an indexer (`:197-206`).

`ParameterCollectionBuilder` (`src/Xcaciv.Command.Core/Parameters/ParameterCollectionBuilder.cs:10`) builds a
collection from a `Dictionary<string,string>` + attribute array:
* `Build` (`:34-66`) — **aggregates** all validation errors then throws one
  `ArgumentException("Parameter validation failed:\n  - …")` (`:59-63`).
* `BuildStrict` (`:76-100`) — **throws on the first** failure (`:90-94`).
* Both only consider attributes whose `Name` is present in the input dictionary (`:42-44`, `:83-85`).

WARNING: Neither is used by `AbstractCommand`; they are for hosts that pre-tokenize parameters themselves.

---

## 6. `IIoContext` — I/O, piping, prompting, progress

`src/Xcaciv.Command.Interface/IIoContext.cs:28` — `public partial interface IIoContext : ICommandContext<IIoContext>`.

### 6.1 Inherited from `ICommandContext<T>`

`src/Xcaciv.Command.Interface/ICommandContext.cs:9-32` (also `: IAsyncDisposable`):

| Member | Line | Meaning |
|---|---|---|
| `Guid Id { get; }` | `:14` | context identity |
| `string Name { get; }` | `:18` | friendly name for output |
| `Guid? Parent { get; }` | `:22` | parent context id, null at the root |
| `Task<T> GetChild()` | `:31` | create a child context. Doc comment mentions `childArguments`/`pipeline` params that **do not exist** in the signature. |

### 6.2 `IIoContext` members

| Member | Line | Contract |
|---|---|---|
| `bool HasPipedInput { get; }` | `:38` | true when an input pipe was attached. Drives `AbstractCommand`'s execution split. |
| `string[] Parameters { get; }` | `:48` | tokenized args for this command (command name already stripped). May be empty, never null in practice. |
| `void SetInputPipe(ChannelReader<IResult<string>> reader)` | `:58` | host-only; wires the upstream stage. |
| `IAsyncEnumerable<IResult<string>> ReadInputPipeChunks()` | `:69` | consume upstream chunks; completes when the upstream writer completes. |
| `Task<string> PromptForCommand(string prompt)` | `:80` | interactive input. Contract: only meaningful when `HasPipedInput == false`. |
| `void SetOutputPipe(ChannelWriter<IResult<string>> writer)` | `:90` | host-only; wires the downstream stage. |
| `Task OutputChunk(IResult<string> message)` | `:102` | emit one output unit. **Prioritises the pipe when one is set.** |
| `Task SetStatusMessage(string message)` | `:114` | transient status; replaces the previous status. NOT command output. |
| `Task AddTraceMessage(string message)` | `:126` | diagnostics for developers; not normally shown. |
| `Task<int> SetProgress(int total, int step)` | `:138` | progress; contract says it returns percent complete (0-100). |
| `Task Complete(string? message)` | `:149` | finalise/close pipes. |
| `Task SetParameters(string[] parameters)` | `:160` | host-only; used for sub-command shifting. |
| `void SetOutputEncoder(IOutputEncoder encoder)` | `:172` | host propagates the controller's encoder. |
| `int? PipelineStage { get; }` | `:178` | 1-based stage, `null` when standalone. |
| `int? PipelineTotalStages { get; }` | `:184` | total stages, `null` when standalone. |
| `void SetPipelineStage(int stage, int totalStages)` | `:191` | host-only; audit/diagnostic metadata. |

### 6.3 The piping model, end to end

1. `PipelineExecutor` creates a **bounded** `Channel<IResult<string>>` between consecutive stages
   (`src/Xcaciv.Command/PipelineExecutor.cs:104-108`).
2. Stage *n*'s context gets `SetOutputPipe(channel.Writer)`; stage *n+1*'s context gets
   `SetInputPipe(channel.Reader)` (`:101,108`).
3. `SetInputPipe` sets `HasPipedInput = true` (`src/Xcaciv.Command.Core/AbstractTextIo.cs:104-108`).
4. Commands read with `await foreach (var chunk in io.ReadInputPipeChunks())`
   (`AbstractTextIo.cs:91-99` — yields nothing and returns immediately if no input pipe, `:93`).
5. Commands write by `yield return`ing from `Main`; the executor calls `OutputChunk`, which writes to the output pipe
   when one exists, otherwise to the terminal sink (`AbstractTextIo.cs:67-74`).
6. `Complete(message)` sets a status message if given and calls `outputPipe?.TryComplete()`
   (`AbstractTextIo.cs:148-156`) — that is what lets the downstream `await foreach` terminate.
   `DisposeAsync()` simply calls `Complete()` (`:143-146`).

### 6.4 Child context creation

`GetChild()` returns a **new** `IIoContext`. The pipeline calls it once per stage
(`PipelineExecutor.cs:93`), then `SetParameters(args)` (`:94`), `SetPipelineStage(...)` (`:97`), and the pipe wiring.
`MemoryIoContext.GetChild()` (`src/Xcaciv.Command/MemoryIoContext.cs:20-31`) is the reference implementation:
it constructs `new MemoryIoContext(Name + "Child", Parameters, Id)`, registers the child in a `ConcurrentBag`
(`:24`), and **inherits the parent's pipes if already set** (`:26-28`).

### 6.5 Shipped implementations

**`AbstractTextIo`** — `src/Xcaciv.Command.Core/AbstractTextIo.cs:23`, primary constructor
`(string name, string[] parameters, Guid? parentId = default)`.

Provides for free: `Id` (new GUID, `:27`), `Name` (`:29`), `Parent` (`:31`), `HasPipedInput` (`:33`),
`Parameters` + `SetParameters` (`:35-41`), `PipelineStage`/`PipelineTotalStages`/`SetPipelineStage` (`:43-51`),
`SetInputPipe`/`SetOutputPipe` (`:104-116`), `ReadInputPipeChunks` (`:91-99`), `OutputChunk` (virtual, `:67-74`),
`Complete` (`:148-156`), `DisposeAsync` (`:143-146`), `SetTraceLog(string)` (`:158-166`),
`AddTraceMessage` (virtual, `:168-177` — echoes to output as `\tTRACE: …` when `Verbose == true` (`:25,170-173`),
otherwise `Trace.WriteLine`), and a **no-op `SetOutputEncoder`** (`:121-125`).

You must implement: `Task<IIoContext> GetChild()` (`:60`), `Task HandleOutputChunk(IResult<string>)` (`:80`),
`Task<string> PromptForCommand(string)` (`:86`), `Task<int> SetProgress(int,int)` (`:132`),
`Task SetStatusMessage(string)` (`:138`).

**`MemoryIoContext`** — `src/Xcaciv.Command/MemoryIoContext.cs:13`, primary constructor
`(string name = "MemoryIo", string[]? parameters = default, Guid parentId = default)`.
* `ConcurrentBag<string> Output` (`:17`), `ConcurrentBag<MemoryIoContext> Children` (`:16`),
  `ConcurrentDictionary<string,string> PromptAnswers` (`:18`).
* `HandleOutputChunk` appends `result.Output` on success, `$"ERROR: {result.ErrorMessage}"` on failure (`:33-44`).
  WARNING: Because `AbstractTextIo.OutputChunk` short-circuits to the pipe, **`Output` only fills when there is no output
  pipe** — i.e. on the last pipeline stage / standalone runs, or on the root context that
  `PipelineExecutor.CollectPipelineOutput` writes to (`PipelineExecutor.cs:209-213`).
* `SetProgress` appends `"Progress: {step} of {total}"` and returns `step` (**not a percentage** — contradicts
  `IIoContext.cs:132-137`) (`:57-61`).
* `SetStatusMessage` appends `"Status: {message}"` (`:63-67`).
* `PromptForCommand` appends `"PROMPT> {prompt}:"` and returns `PromptAnswers[prompt]` if present, else echoes the
  prompt back (`:46-55`).

There is **no console implementation shipped** — a host writes its own `AbstractTextIo` subclass.

---

## 7. `IEnvironmentContext` and `IControllerEnvironmentContext`

### 7.1 `IEnvironmentContext` — the command-scoped view

`src/Xcaciv.Command.Interface/IEnvironmentContext.cs:25` — `: ICommandContext<IEnvironmentContext>`.

| Member | Line | Contract |
|---|---|---|
| `void SetValue(string key, string value)` | `:37` | Overwrites; marks `HasChanged`; audit-logged. Keys case-insensitive. |
| `string GetValue(string key, string defaultValue = "", bool storeDefault = true)` | `:50` | Case-insensitive lookup. **`storeDefault` defaults to `true`, so a miss WRITES the default back and flips `HasChanged`.** |
| `Dictionary<string,string> GetEnvironment()` | `:60` | Snapshot copy; mutating it does nothing. |
| `bool HasChanged { get; }` | `:70` | Whether anything was set. Drives propagation. |
| `void UpdateEnvironment(Dictionary<string,string> dictionary)` | `:81` | Merge-in, overwriting matching keys. |
| `void SetAuditLogger(IAuditLogger auditLogger)` | `:86` | Host-only. |
| + `Id`, `Name`, `Parent`, `Task<IEnvironmentContext> GetChild()`, `DisposeAsync()` | via `ICommandContext<T>` | |

Implementation `EnvironmentContext` (`src/Xcaciv.Command/EnvironmentContext.cs:12`):
* Storage is `ConcurrentDictionary<string,string>(StringComparer.OrdinalIgnoreCase)` (`:18`).
  WARNING: The interface doc says keys are "stored as uppercase" (`IEnvironmentContext.cs:30`) — **they are not
  upper-cased**, they are merely compared case-insensitively.
* Ctors: `()`, `(Guid? parent)`, `(Dictionary<string,string> environment, Guid? parent = null)` (`:33-44`).
* `GetChild()` **copies the current values into a brand-new context** and propagates the audit logger (`:51-61`) —
  this is the isolation boundary: the child starts with a snapshot and its writes never reach the parent object.
* `SetValue` uses `AddOrUpdate`, traces the old→new transition, sets `HasChanged = true`, and calls
  `_auditLogger?.LogEnvironmentChange(key, oldValue, addValue, "system", DateTime.UtcNow)` (`:77-92`).
* `GetValue` stores the default on miss when `storeDefault` (`:98-111`).
* `UpdateEnvironment` is just a `SetValue` loop (`:120-126`) — so it marks `HasChanged` and audit-logs each key.

### 7.2 `IControllerEnvironmentContext` — the host-scoped view

`src/Xcaciv.Command.Interface/IControllerEnvironmentContext.cs:25` — `: ICommandContext<IControllerEnvironmentContext>`.

| Member | Line |
|---|---|
| `void SetValue(string key, string value, string commandName)` | `:37` |
| `Task<IEnvironmentContext> GetChild(string commandName)` | `:44` |
| `Dictionary<string,string> GetEnvironment()` | `:54` |
| `Dictionary<string,string> GetEnvironment(string commandName, bool prefix = true)` | `:64` |
| `bool HasChanged { get; }` | `:74` |
| `void UpdateEnvironment(Dictionary<string,string> dictionary, string commandName)` | `:85` |
| `void UpdateEnvironment(Dictionary<string,string> dictionary)` | `:96` |
| `void SetAuditLogger(IAuditLogger auditLogger)` | `:101` |
| `List<string> GetCommandEnvironmentNames()` | `:107` |
| + `Task<IControllerEnvironmentContext> GetChild()` from `ICommandContext<T>` | |

Implementation `ControllerEnvironmentContext` (`src/Xcaciv.Command/ControllerEnvironmentContext.cs:10`) keeps **two
stores**:
* `_environment : IEnvironmentContext` — the **global** variables (`:20`).
* `_commandEnvironment : ConcurrentDictionary<string, ConcurrentDictionary<string,string>>` (OrdinalIgnoreCase at
  both levels) — **per-command** variables (`:16`).

Key behaviours:
* `HasChanged` is `field || _environment.HasChanged` (`:28-35`).
* `GetChild()` (no args) clones the global child **and** copies the per-command map (`:102-107`).
* **`GetChild(string commandName)`** (`:119-133`) is what a command actually receives:
  it takes a global child and then writes that command's stored variables into it, **prefixing each key with
  `{COMMANDNAME}_`** unless it already carries the prefix (`:125-130`).
* `GetEnvironment(commandName, prefix = true)` returns that command's bucket, prefixed or raw (`:152-175`); empty
  `commandName` returns the global snapshot (`:154-157`).
* `SetValue(key, value, commandName = "")` (`:189-214`): with no command name, or when the key does **not** start
  with `{commandName}_`, it writes to the **global** store (`:191-202`); otherwise it writes the prefixed key into the
  command bucket and sets `HasChanged` (`:204-213`).
* `UpdateEnvironment(dictionary)` (`:222-226`) **filters out any key prefixed with a known command name**
  (`RemoveCommandPrefixedValues`, `:233-247`) before merging into the global store — this is what stops a
  command from smuggling another command's namespace into globals.
* `UpdateEnvironment(dictionary, commandName)` (`:257-282`) strips the `{commandName}_` prefix and writes into that
  command's bucket.
* `SetAuditLogger` forwards to `_environment` only (`:287-290`) — **per-command bucket writes are not audit-logged**.

### 7.3 The `ModifiesEnvironment` rule — how changes propagate back

`ModifiesEnvironment` is a property of the **registration**, not of the command class:
`ICommandDescription.ModifiesEnvironment` (`src/Xcaciv.Command.Interface/ICommandDescription.cs:22`), set from the
`modifiesEnvironment` argument of `AddCommand` (`src/Xcaciv.Command/CommandRegistry.cs:57-60`). There is **no
attribute** for it.

Three distinct propagation paths, all in the host:

**(a) Single command, `CommandController.Run` (`src/Xcaciv.Command/CommandController.cs:260-282`):**
```csharp
var childEnv = await env.GetChild(commandName);                        // :266
await ExecuteCommandInternal(commandName, ioContext, childEnv, ct);    // :267
if (childEnv.HasChanged && _commandRegistry.TryGetCommand(commandName, out var desc))  // :268
{
    if (desc?.ModifiesEnvironment == true)
        env.UpdateEnvironment(childEnv.GetEnvironment());              // :271-273  -> GLOBAL
    else
    {
        // only allow commands to update their own environment values
        var envUpdate = childEnv.GetEnvironment()
            .Where(x => x.Key.StartsWith(commandName + "_", OrdinalIgnoreCase))
            .ToDictionary(x => x.Key.Substring(commandName.Length + 1), x => x.Value);
        env.UpdateEnvironment(envUpdate, commandName);                 // :278-279  -> COMMAND BUCKET
    }
}
```
**This is the whole rule:** a command **without** `ModifiesEnvironment` can still persist state — but **only** under
keys it prefixed with its own command name, and those land in its private bucket. A command **with**
`ModifiesEnvironment = true` writes to the global store (and `UpdateEnvironment(dict)` still filters other commands'
prefixes, `:222-226`).

**(b) Inner executor layer (`src/Xcaciv.Command/CommandExecutor.cs:186,215-218`):** `CommandExecutor` takes yet
another `GetChild()` and merges back **only** when `commandDescription.ModifiesEnvironment && childEnv.HasChanged`.
So there are **two nested child environments** per command execution.

**(c) Pipeline (`src/Xcaciv.Command/PipelineExecutor.cs:112-120`):** each stage gets
`environmentContext.GetChild(commandName)` and, if `HasChanged`, unconditionally writes back with
`environmentContext.UpdateEnvironment(childEnv.GetEnvironment(), commandName)` — i.e. **into that command's bucket**,
regardless of `ModifiesEnvironment`. Then, back in `CommandController.Run` (`:252-258`), **only the last stage's**
command is checked for `ModifiesEnvironment`, and if set, its bucket (unprefixed) is promoted to the global
environment.

Practical guidance for a tool author:
* To read config: `env.GetValue("MYCMD_TIMEOUT", "30")` (remember §2.4's prefix), or declare it in
  `GetDefaultEnvironment()` and let the host seed it.
* To persist your own state across invocations: write `env.SetValue("MYCMD_STATE", value)` — it survives in your
  bucket without needing `ModifiesEnvironment`.
* To write a **global** variable (the `SET` command's job): the host must register you with
  `AddCommand(pkg, new MyCmd(), modifiesEnvironment: true)`.
* Beware `GetValue`'s `storeDefault: true` default: merely *reading* a missing variable sets `HasChanged` and causes a
  write-back.

---

## 8. `ICommandController` — hosting the tools

`src/Xcaciv.Command.Interface/ICommandController.cs:3-69`

```csharp
public interface ICommandController
{
    void RegisterBuiltInCommands();                                                     // :9
    void AddPackageDirectory(string directory);                                          // :15
    void LoadCommands(string subDirectory = "bin");                                      // :20
    Task Run(string commandLine, IIoContext output, IControllerEnvironmentContext env);  // :27
    Task Run(string commandLine, IIoContext output, IControllerEnvironmentContext env, CancellationToken ct); // :35
    void AddCommand(ICommandDescription command);                                        // :40
    void AddCommand(string packageKey, Type commandType, bool modifiesEnvironment = false); // :48
    void AddCommand(string packageKey, ICommandDelegate command, bool modifiesEnvironment = false); // :60
    IControllerEnvironmentContext GetEnvironment();                                      // :68
}
```

WARNING: **`GetHelpAsync` is NOT on `ICommandController`, and not a public member of `CommandController` either.** It lives
on `ICommandExecutor` (`src/Xcaciv.Command.Interface/ICommandExecutor.cs:34,39`) and is reached only indirectly —
see §8.4. `docs/QUICK_REFERENCE.md:48-49,105` and `CHANGELOG.md:180` show `await controller.GetHelpAsync(...)`; that
**does not compile** against 3.3.4.

### 8.1 Construction — `CommandController` (`src/Xcaciv.Command/CommandController.cs:27`)

| Constructor | Line | Use |
|---|---|---|
| `CommandController()` | `:45-48` | all defaults |
| `CommandController(ICrawler crawler)` | `:54-63` | custom plugin discovery |
| `CommandController(ICrawler crawler, string restrictedDirectory)` | `:70-74` | **plugin sandbox root** |
| `CommandController(IVerifiedSourceDirectories dirs)` | `:80-89` | custom directory verification (testing) |
| `CommandController(ICommandRegistry?, ICommandLoader?, IPipelineExecutor?, ICommandExecutor?, ICommandFactory?, IServiceProvider?)` | `:94-114` | full DI seam |

Defaults wired in the DI constructor (`:102-113`): `CommandRegistry`, `CommandFactory(serviceProvider)`,
`HelpService`, `CommandExecutor(registry, factory, helpService)`,
`CommandLoader(new Crawler(), new VerifiedSourceDirectories(new FileSystem()))`, `PipelineExecutor`,
`NoOpAuditLogger`, `NoOpEncoder`.

Also `CommandControllerFactory.Create(CommandControllerOptions?)`
(`src/Xcaciv.Command/CommandControllerFactory.cs:19-53`) — a DI-free builder that honours `RestrictedDirectory`
(`:25-34`), `EnableDefaultCommands` (`:37-40`), and `PackageDirectories` + `LoadCommands()` (`:43-50`).

### 8.2 Registration

* **`RegisterBuiltInCommands()`** (`CommandController.cs:182-190`) — registers under package key `"Default"`:
  `RegifCommand`, `SayCommand`, `SetCommand` (**with `modifiesEnvironment: true`**), `EnvCommand`.
* **`AddCommand(string packageKey, ICommandDelegate command, bool modifiesEnvironment = false)`** (`:195-198`)
  → `CommandRegistry.AddCommand(packageKey, command.GetType(), modifiesEnvironment)` (`CommandRegistry.cs:64-68`).
  WARNING: **The instance you pass is discarded** — only its `Type` is registered, and the factory constructs a *new*
  instance per execution. Do not pre-configure state on the instance you hand to `AddCommand`.
* **`AddCommand(string packageKey, Type commandType, bool modifiesEnvironment = false)`** (`:203-206`) — requires
  `[CommandRegister]`; otherwise traces and silently skips (`CommandRegistry.cs:42-47`). `PackageDescription.FullPath`
  is set to `commandType.Assembly.Location` (`:49-53`).
* **`AddCommand(ICommandDescription command)`** (`:211-214`) — direct description injection, merging sub-commands
  into an existing root when present (`CommandRegistry.cs:24-35`).
* **`AddPackageDirectory(string directory)`** (`:165-168`) → `VerifiedSourceDirectories.AddDirectory`, which
  **silently returns `false` and does not add** the directory if it fails restriction/existence verification
  (`src/Xcaciv.Command.FileLoader/VerifiedSourceDirectories.cs:82-89`).
* **`LoadCommands(string subDirectory = "bin")`** (`:174-177`) → `CommandLoader.LoadCommands` (`CommandLoader.cs:38-55`),
  which throws `NoPluginsFoundException("No base package directory configured. …")` when no directory was accepted
  (`:42-45`), then crawls each directory and registers every discovered command.
* **`GetEnvironment()`** (`:323-326`) → `CommandRegistry.GetEnvironment(_commandFactory)`, building a fresh
  `ControllerEnvironmentContext` pre-seeded with every registered command's `GetDefaultEnvironment()`, bucketed by
  command name (`CommandRegistry.cs:88-142`).

### 8.3 Execution — `Run`

Four overloads on the class (`CommandController.cs:219-283`):
* `Run(string, IIoContext, IControllerEnvironmentContext)` → delegates with `CancellationToken.None` (`:219-222`).
* `Run(string, IIoContext, IEnvironmentContext)` (**class-only convenience**, `:224-227`).
* `Run(string, IIoContext, IEnvironmentContext, CancellationToken)` (**class-only**, `:229-234`) — wraps a plain
  `IEnvironmentContext` in a `new ControllerEnvironmentContext(env)` when it isn't already one (`:232`).
* `Run(string commandText, IIoContext, IControllerEnvironmentContext, CancellationToken)` — the real one (`:236-283`).

The real one, in order:
1. Null-checks all three arguments (`:238-240`); `cancellationToken.ThrowIfCancellationRequested()` (`:242`).
2. `env.SetAuditLogger(_auditLogger)` (`:244`).
3. `ioContext.SetOutputEncoder(_outputEncoder)` (`:246`).
4. **Branch on `'|'`**: `commandText.IndexOf(CommandSyntax.PipelineDelimiter) >= 0` (`:248`).
   * **Pipeline path** (`:250-258`): take a controller-level child env, run `_pipelineExecutor.ExecuteAsync(...)`,
     then promote the **last stage's** bucket to globals iff that command has `ModifiesEnvironment` (§7.3c).
   * **Single path** (`:262-281`): `NamesValidator.GetValidCommandName(commandText)` for the name,
     `NamesValidator.GetArgumentsFromCommandline(commandText)` for the args, `ioContext.SetParameters(args)`,
     `env.GetChild(commandName)`, execute, then the propagation rule of §7.3a.

**Argument tokenization** — `NamesValidator.GetArgumentsFromCommandline`
(`src/Xcaciv.Command.Interface/NamesValidator.cs:51-60`):
```csharp
Regex.Matches(commandLine, @"[\""].*?[\""]|[\w-]+")
     .Select(o => InvalidParameterChars.Replace(o.Value, "").Trim('"'))
     .Skip(1)   // first match is the command itself
```
with `InvalidParameterChars = [^-_\da-zA-Z .*?\[\]|""~!@#$%^&*\(\)]+` (`:22`).
WARNING — Consequences you must design around:
* Unquoted tokens are matched by `[\w-]+` — **`.` `/` `\` `:` `=` `%` break tokens apart**. `C:\dir\file.txt`
  arrives as `C`, `dir`, `file`, `txt`. **Quote any path, URL, or regex.**
* Quoted `"…"` spans are kept whole, then filtered by `InvalidParameterChars` (which *does* allow `. * ? [ ] | " ~ ! @ # $ % ^ & ( )`)
  and finally `Trim('"')`. `\` `/` `:` `=` `,` `;` `<` `>` `{` `}` `+` are stripped **even inside quotes**.
* That is why `SAY` documents "Use double quotes to include environment variables in the format `%var%`"
  (`src/Xcaciv.Command/Commands/SayCommand.cs:16`) — unquoted, `%` is dropped and the token splits.

### 8.4 Help routing

`CommandController.ExecuteCommandInternal` (`:285-297`) intercepts help **before** dispatch:
* `IsHelpRequest(commandKey, parameters)` (`:299-307`) — true when the command name equals `HelpCommand`
  (default `"HELP"`, settable via the `HelpCommand` property `:119-127`), **or** when
  `HelpService.IsHelpRequest(parameters)` matches any of `--HELP`, `-?`, `/?` (case-insensitive)
  (`src/Xcaciv.Command/HelpService.cs:154-164`).
* `HandleHelpRequest` (`:309-317`) resolves the target (`HELP <cmd>` → `<cmd>`; `<cmd> --HELP` → `<cmd>`; bare `HELP`
  → `""`) and calls `_commandExecutor.GetHelpAsync(target, io, env, ct)`.
* `CommandExecutor.GetHelpAsync` (`CommandExecutor.cs:66-84`) → empty target lists all commands
  (`OutputAllCommands`, `:86-113`); otherwise detailed help (`OutputCommandHelp`, `:115-167`).
  The cancellation-token overload is documented as accepting the token "for API consistency" and ignoring it (`:81-83`).

`HelpService.BuildHelp` output shape (`HelpService.cs:22-120`):
```
[<RootCommand> ]<COMMAND>:
  <Description>

Usage:
  <Prototype>            (or an auto-generated indicator list when Prototype == "todo")

Options:
  <indicator>        <value description>
  ...

Remarks:

<remark 1>

<remark 2>
```
Requires `[CommandRegister]` or it throws `InvalidOperationException` (`:32-35`). Named parameters render as
`-name <value|allowed|values>` in the generated prototype (`:78-83`).
`BuildOneLineHelp` (`:122-152`) prints `"-\t{BaseCommand,-12} [Has sub-commands]"` for roots, `"{Command,-12} {Description}"`
otherwise; it caches resolved `Type`s in a `ConcurrentDictionary` keyed `"{fullTypeName}|{assemblyPath}"` (`:21,172-237`).

### 8.5 Host-level properties on `CommandController`

| Property | Line | Notes |
|---|---|---|
| `string HelpCommand` | `:119-127` | blank/whitespace resets to `"HELP"`; normalized by `NamesValidator`; pushed to the executor. |
| `IAuditLogger AuditLogger` | `:133-141` | null resets to `NoOpAuditLogger`; pushed to the executor. |
| `IOutputEncoder OutputEncoder` | `:146-150` | null resets to `NoOpEncoder`. |
| `PipelineConfiguration PipelineConfig` | `:155-159` | proxies `_pipelineExecutor.Configuration`; null → `ArgumentNullException`. |
| `protected ICommandRegistry CommandRegistry` | `:36` | for subclasses. |

### 8.6 `CommandControllerOptions`

`src/Xcaciv.Command.Interface/CommandControllerOptions.cs:7`

| Member | Line | Default |
|---|---|---|
| `const string SectionName = "Xcaciv:Command"` | `:12` | config binding key |
| `string HelpCommand` | `:18` | `"HELP"` |
| `bool EnableDefaultCommands` | `:24` | `true` |
| `string[] PackageDirectories` | `:29` | empty |
| `string? RestrictedDirectory` | `:35` | `null` |
| `bool Verbose` | `:41` | `false` |

WARNING: Only `CommandControllerFactory.Create` actually honours these (`CommandControllerFactory.cs:19-53`), and it ignores
`HelpCommand` and `Verbose`. The DI extension registers the options object but nothing consumes it (§13).

### 8.7 The `AuditLogger` hook

Set `controller.AuditLogger = yourLogger` (`CommandController.cs:133-141`). It is then:
* pushed into `CommandExecutor.AuditLogger` (`:139`), which fires **one `AuditEvent` per command execution** in a
  `finally` block (`CommandExecutor.cs:231-255`);
* pushed into the environment on every `Run` via `env.SetAuditLogger(_auditLogger)` (`:244`), which propagates to
  every `EnvironmentContext` child (`EnvironmentContext.cs:55-59`), so every `SetValue` fires
  `LogEnvironmentChange` (`EnvironmentContext.cs:91`).

See §11.4 for the payload.

---

## 9. The pipeline

### 9.1 Parsing `|`

`CommandSyntax` (`src/Xcaciv.Command.Interface/CommandSyntax.cs:7-13`):
`PipelineDelimiter = '|'`, `DoubleQuote = '"'`, `SingleQuote = '\''`, `EscapeChar = '\\'`.

`PipelineParser.ParsePipeline(string)` (`src/Xcaciv.Command/PipelineParser.cs:24-94`):
* Empty/whitespace → `Array.Empty<string>()` (`:26-29`).
* `\|`, `\"`, `\'`, `\\` outside quotes unescape to the literal character (`:43-53`).
* An **unquoted** `|` ends a segment; segments are trimmed and **empty segments are dropped** (`:56-66`).
* `"…"` → escape processing **inside** for `\"`, `\\`, `\|` (`:110-119`); `'…'` → literal, no escapes (`:78-80,103`).
* Quotes are consumed (not emitted) — `ParseQuotedString` returns the index past the closing quote (`:100-133`).
* **Unbalanced quote → `InvalidOperationException($"Unbalanced {quoteChar} quote in pipeline.")`** (`:132`).

WARNING — Two-stage tokenization: `PipelineParser` splits on `|` and strips quotes; then each segment goes through
`NamesValidator.GetValidCommandName` / `GetArgumentsFromCommandline` (`PipelineExecutor.cs:91-92`), which
re-tokenizes with the regex of §8.3. **Quotes removed by `PipelineParser` are no longer available to protect a
token from the argument regex.** In practice this means a quoted argument with spaces inside a pipeline is
re-split into separate arguments — which is exactly the case a `[CommandParameterSuffix]` handles (it re-joins
them with spaces).

### 9.2 How stages are threaded

`PipelineExecutor.CreatePipelineStages` (`src/Xcaciv.Command/PipelineExecutor.cs:72-129`), per segment:
1. `NamesValidator.GetValidCommandName(command)` / `GetArgumentsFromCommandline(command)` (`:91-92`).
2. `childIoContext = await ioContext.GetChild()` then `SetParameters(args)` (`:93-94`).
3. `SetPipelineStage(currentStage, totalStages)` (1-based) (`:97`).
4. If a previous channel exists → `childIoContext.SetInputPipe(prev.Reader)` (`:99-102`).
5. Create the **next** bounded channel and `SetOutputPipe(writer)` (`:104-108`).
6. `tasks.Add(ExecuteStageAsync())` — **the stage task is started immediately**, so **all stages run concurrently**
   (`:110-120`).
7. `LastStageCommand = commandName` (`:123`).

Every stage gets its own environment child: `environmentContext.GetChild(commandName)` (`:114`), and on completion
merges back into that command's bucket when `HasChanged` (`:116-119`).

`ExecuteAsync` (`:33-70`) then:
* `Task.WhenAll(tasks)` raced against `Task.Delay(Timeout.Infinite, cancellationToken)` (`:49-52`);
  if cancellation wins → `throw new OperationCanceledException(cancellationToken)` (`:54-58`).
* `await allStagesTask` to surface stage exceptions (`:61`).
* Re-check cancellation (`:64-67`).
* `CollectPipelineOutput(outputChannel, ioContext, ct)` — drains the **final** channel and forwards each chunk to the
  root context via `ioContext.OutputChunk(...)` (`:200-214`).

WARNING: Because the final stage also has an output pipe, its results go to the last channel, not directly to the root
context — the drain at `:209-213` is what makes pipeline output visible.

### 9.3 Backpressure modes

`PipelineBackpressureMode` (`src/Xcaciv.Command.Interface/PipelineBackpressureMode.cs:6-22`):
`DropOldest = 0`, `DropNewest = 1`, `Block = 2`.

Mapped to `BoundedChannelFullMode` in `PipelineExecutor.GetChannelFullMode` (`:216-222`):
`DropOldest → BoundedChannelFullMode.DropOldest`, `DropNewest → DropNewest`, `Block → Wait`, anything else →
`ArgumentOutOfRangeException`.

Channel construction (`:104-107`):
```csharp
Channel.CreateBounded<IResult<string>>(new BoundedChannelOptions(Configuration.MaxChannelQueueSize)
{ FullMode = GetChannelFullMode(Configuration.BackpressureMode) });
```

### 9.4 `PipelineConfiguration`

`src/Xcaciv.Command.Interface/PipelineConfiguration.cs:9` — **namespace `Xcaciv.Command`** (not `.Interface`), and
type-forwarded for binary compatibility (`src/Xcaciv.Command/PipelineConfiguration.TypeForwarding.cs`,
`CHANGELOG.md:56`).

| Property | Line | Default | Effect |
|---|---|---|---|
| `int MaxChannelQueueSize` | `:15` | `10_000` | bounded channel capacity |
| `PipelineBackpressureMode BackpressureMode` | `:23` | `Block` | full-channel policy |
| `int ExecutionTimeoutSeconds` | `:29` | `0` (none) | WARNING: **declared but never read** by `PipelineExecutor` |
| `int StageTimeoutSeconds` | `:36` | `0` (none) | per-stage `CancelAfter` (`PipelineExecutor.cs:150-153`) |
| `long MaxStageOutputBytes` | `:43` | `0` (none) | WARNING: **declared but never enforced** |
| `int MaxStageOutputItems` | `:50` | `0` (none) | WARNING: **declared but never enforced** |
| `void Validate()` | `:55-71` | | throws `InvalidOperationException` for non-positive `MaxChannelQueueSize` or negative timeouts/limits. Not called automatically. |

Separate DI-binding shape `PipelineOptions` (`src/Xcaciv.Command.Interface/PipelineOptions.cs:6`):
`SectionName = "Xcaciv:Command:Pipeline"` (`:11`), `MaxChannelQueueSize = 100` (`:17`),
`BackpressureMode = "Wait"` (`:24`), and `GetBackpressureMode()` mapping
`WAIT|BLOCK → Block`, `DROPOLDEST → DropOldest`, `DROPNEWEST → DropNewest`, default `Block` (`:29-38`).
WARNING: **Nothing maps `PipelineOptions` onto `PipelineConfiguration`** — the DI extension registers the options but no
code reads them (§13). The two defaults also differ (100 vs 10 000).

### 9.5 Channel completion

* A stage runs inside `await using (childContext)` (`PipelineExecutor.cs:144`), so `DisposeAsync` →
  `Complete()` → `outputPipe?.TryComplete()` (`AbstractTextIo.cs:143-156`) **always** fires, even on exception.
* On the success path `Complete(null)` is also called explicitly (`PipelineExecutor.cs:168`).
* Completing the writer is what terminates the downstream `ReadAllAsync` loop
  (`AbstractTextIo.cs:95`) and the final drain (`PipelineExecutor.cs:209`).
* Verified by `src/tests/Xcaciv.Command.Tests/PipelineChannelCompletionTests.cs:18-75` (2- and 3-stage pipelines
  complete within 5 s and each consumer completes exactly once).

### 9.6 Error and cancellation propagation between stages

* **Command failures are data, not exceptions.** A failing command yields `CommandResult<string>.Failure(...)`;
  `CommandExecutor` re-emits it as a chunk (`CommandExecutor.cs:203-212`); the chunk travels down the channel;
  `AbstractCommand.Main` in the *next* stage yields it straight through without calling `HandlePipedChunk`
  (`AbstractCommand.cs:81-85`). **A failure therefore flows to the end of the pipeline and out to the user, and
  downstream stages keep running.**
* **Unknown command:** `CommandExecutor.ExecuteAsync` emits
  `CommandResult<string>.Failure($"Command [{key}] not found. Try 'HELP'")` and traces it (`:61-63`) — the pipeline
  does not abort (`src/tests/Xcaciv.Command.Tests/PipelineErrorTests.cs:26-75` asserts graceful completion for a bad
  command in first/middle/last position).
* **Thrown exceptions inside a stage** are caught by `CommandExecutor` (`:223-230`) and converted to a failure chunk.
  Exceptions escaping `RunStageAsync` propagate through `Task.WhenAll` and out of `ExecuteAsync` (`:61`).
* **Stage timeout:** with `StageTimeoutSeconds > 0`, a linked CTS cancels the stage; the handler traces
  `"Pipeline stage timeout: {cmd} (exceeded {n}s)"` and calls
  `Complete($"Stage '{cmd}' exceeded timeout of {n} seconds")` (`:170-181`) — the message becomes a **status
  message**, not an output chunk (`AbstractTextIo.cs:150-153`).
* **Stage cancelled with no timeout configured** → traces `"Pipeline stage cancelled unexpectedly: {cmd}"` and
  completes with `"Stage '{cmd}' was cancelled"` (`:182-186`).
* **Parent cancellation** → trace, `Complete(null)` for a graceful downstream close, then rethrow
  (`:159-166`, `:188-194`), surfacing as `OperationCanceledException` from `ExecuteAsync`.
* Each stage traces `"Pipeline stage start: {commandName}"` on entry (`:143`).

---

## 10. Results, formats and output encoders

### 10.1 `IResult<out T>`

`src/Xcaciv.Command.Interface/IResult.cs:7-38`

| Member | Line | Meaning |
|---|---|---|
| `bool IsSuccess` | `:12` | |
| `string? ErrorMessage` | `:17` | non-null only on failure |
| `Exception? Exception` | `:22` | optional |
| `string CorrelationId` | `:27` | trace correlation across stages |
| `T? Output` | `:32` | **nullable** — always `?? string.Empty` it |
| `ResultFormat OutputFormat` | `:37` | rendering hint |

### 10.2 `CommandResult<T>`

`src/Xcaciv.Command.Interface/CommandResult.cs:6-29` — `public sealed record CommandResult<T> : IResult<T>`,
all members `init`. `CorrelationId` defaults to `Guid.NewGuid().ToString()` (`:11`);
`OutputFormat` defaults to `ResultFormat.General` (`:14`).

```csharp
public static CommandResult<T> Success(T? output, ResultFormat format = ResultFormat.General); // :16-21
public static CommandResult<T> Failure(string? errorMessage = null, Exception? exception = null); // :23-28
```
WARNING: `Failure` does **not** accept a format — a failure always carries `ResultFormat.General`.

### 10.3 `ResultFormat`

`src/Xcaciv.Command.Interface/ResultFormat.cs:12-49` — each member carries a `[Description]` MIME-ish hint:

| Member | Line | `[Description]` |
|---|---|---|
| `General` | `:18` | `text` |
| `Object` | `:24` | `application/text` |
| `CSV` | `:30` | `text/csv` |
| `TDL` | `:36` | `application/tdl` |
| `YAML` | `:42` | `application/x-yaml` |
| `JSON` | `:48` | `application/json` |

### 10.4 `IOutputEncoder` and the shipped encoders

`src/Xcaciv.Command.Interface/IOutputEncoder.cs:19-34` — one method `string Encode(string output)`; contract says
implementations must be **idempotent and thread-safe** (`:26`).
* `NoOpEncoder` — identity (`:42-48`), the default everywhere.
* `HtmlEncoder` — `System.Net.WebUtility.HtmlEncode` (`src/Xcaciv.Command/Encoders/HtmlEncoder.cs:18-21`).
* `JsonEncoder` — `JsonSerializer.Serialize(output)` with the surrounding quotes stripped
  (`src/Xcaciv.Command/Encoders/JsonEncoder.cs:18-24`).

### 10.5 How `OutputFormat` "drives encoding" — WARNING: it doesn't

The intended design (per `IIoContext.cs:162-172` and `IOutputEncoder.cs:12-18`) is:
`CommandController.OutputEncoder` → `ioContext.SetOutputEncoder(encoder)` on every `Run`
(`CommandController.cs:246`) → the context applies `Encode` to every `OutputChunk`.

**In the shipped code this last step does not happen.** `AbstractTextIo.SetOutputEncoder`
(`src/Xcaciv.Command.Core/AbstractTextIo.cs:121-125`) has an empty body with the comment
*"Default implementation does nothing; subclasses can override if they need to apply encoding."*
A repository-wide grep shows `IOutputEncoder.Encode` is called **only from `src/tests/…/OutputEncodingTests.cs`** —
never from any production path.

Therefore, precisely:
* `IResult<string>.OutputFormat` / `AbstractCommand.OutputFormat` is **metadata that rides along with each chunk**.
  It is readable downstream (`pipedChunk.OutputFormat`, e.g.
  `src/tests/Xcaciv.Command.Tests/Commands/TestSubCommand.cs:58`) and by the terminal sink. **No framework code
  branches on it.**
* Encoding is entirely the **host's** job: subclass `AbstractTextIo`, store the encoder in your
  `SetOutputEncoder` override, and apply it in your `HandleOutputChunk` — optionally switching on
  `result.OutputFormat`.

---

## 11. Security model

Primary doc: `SECURITY.md`. WARNING: It is **stale** — it cites Xcaciv.Loader 2.0.1 (`:25`),
`AssemblySecurityPolicy.Default` (`:59`), "No backpressure; channels are unbounded by default" (`:225`), and command
signatures (`IAsyncEnumerable<string> HandleExecution(IIoContext, IEnvironmentContext)`, `:96,141,178`) that no
longer exist. The code below supersedes it.

### 11.1 Plugin directory restriction

Two independent layers.

**(a) Which directories may be scanned — `VerifiedSourceDirectories`**
(`src/Xcaciv.Command.FileLoader/VerifiedSourceDirectories.cs:5`):
* `RestrictedDirectory` is set by `SetRestrictedDirectory` (`:120-123`) — e.g. from
  `new CommandController(crawler, restrictedDirectory)` (`CommandController.cs:70-74`).
* `VerifyRestrictedPath(IPath, filePath, restrictedPath, shouldThrow)` (`:100-115`) resolves both paths to full
  paths and requires `new Uri(fullRestrictedPath).IsBaseOf(new Uri(fullFilePath))`. If `restrictedPath` is empty it
  **defaults to `Directory.GetCurrentDirectory()`** (`:105`).
* `VerifyFile` (`:49-58`) and `VerifyDirectory` (`:66-75`) add existence + attribute checks.
* `AddDirectory` verifies the directory and **silently returns false** on failure (`:82-89`) — a mis-configured
  package directory produces no error, just no plugins (and then `NoPluginsFoundException` at load time,
  `CommandLoader.cs:42-45`).

**(b) How the assembly is loaded — per-plugin `AssemblyContext` sandbox (Xcaciv.Loader 2.1.2).**

`Crawler.LoadPackageDescriptions` (`src/Xcaciv.Command.FileLoader/Crawler.cs:66-178`):
```csharp
var basePathRestriction = Path.GetDirectoryName(binPath) ?? Directory.GetCurrentDirectory();  // :83
using (var context = new AssemblyContext(binPath,
        basePathRestriction: basePathRestriction,
        securityPolicy: _securityPolicy))                                                      // :85-89
```
with `_securityPolicy` defaulting to **`AssemblySecurityPolicy.Strict`** (`:31`), overridable via
`SetSecurityPolicy` (`:54-58`).

`CommandFactory.CreateCommand(fullTypeName, packagePath)` (`src/Xcaciv.Command/CommandFactory.cs:97-157`) does the
same for instantiation:
* `Type.GetType(fullTypeName)` first; if it resolves and a `IServiceProvider` supplies it, use DI (`:104-113`);
  otherwise `AssemblyContext.ActivateInstance<ICommandDelegate>(commandType)` (`:112`).
* Otherwise create an isolated `AssemblyContext` restricted to `Path.GetDirectoryName(packagePath)` (`:124-140`).
* `SecurityException` → wrapped `InvalidOperationException` naming the policy, `EnforceBasePathRestriction`, and the
  base path (`:142-150`).
* `FileNotFoundException | FileLoadException | BadImageFormatException` → wrapped `InvalidOperationException`
  (`:151-156`).

`HelpService.GetCommandType` uses the same sandbox (`AssemblySecurityPolicy.Strict`,
`src/Xcaciv.Command/HelpService.cs:201-207`) and refuses to load when it cannot compute a base path (`:194-199`).

**Discovery layout convention** — `Crawler.CrawlPackagePaths` (`Crawler.cs:189-209`):
search mask is `*/{subDirectory}/*.dll` (default `subDirectory = "bin"`), recursive; missing base directory →
`DirectoryNotFoundException`; no matches → `NoPackageDirectoryFoundException`. Package key is
`"{fileNameWithoutExt}-{relativeDirWithSeparatorsRemoved}"` (`:227-233`). Parallel crawl kicks in above
`Crawler.ParallelizeAt` (default 50) DLLs (`:18,202-208`).
```
verified_base_directory/
  PluginName1/bin/PluginName1.dll
  PluginName2/bin/PluginName2.dll
```
**Failure isolation:** every per-package exception (`SecurityException`, file/load/format,
`ReflectionTypeLoadException`, catch-all) is traced and the package is **skipped**, not fatal
(`Crawler.cs:122-171`); per-*type* failures are also caught and skipped (`:112-116`).
Packages with zero valid commands are not added (`:174`).

### 11.2 Per-instance security policies — `AssemblySecurityConfiguration`

`src/Xcaciv.Command/AssemblySecurityConfiguration.cs:10`

| Property | Line | Default |
|---|---|---|
| `AssemblySecurityPolicy SecurityPolicy` | `:16` | `AssemblySecurityPolicy.Strict` |
| `bool AllowReflectionEmit` | `:22` | `false` |
| `bool EnforceBasePathRestriction` | `:29` | `true` |
| `string[] AllowedDependencyPrefixes` | `:36` | empty |
| `void Validate()` | `:41-49` | throws if `Default` policy is combined with a non-empty allow-list |

Applied via `CommandFactory.SetSecurityConfiguration(config)` (`CommandFactory.cs:31-36`, which calls `Validate()`).
Effective behaviour (`CommandFactory.cs:124-138`):
* `EnforceBasePathRestriction == false` → `basePathRestriction` becomes `"."`.
* **`AllowReflectionEmit == false` forces `AssemblySecurityPolicy.Strict`** regardless of `SecurityPolicy` (`:130-132`).
* WARNING: `AllowedDependencyPrefixes` is validated but **never passed to `AssemblyContext`** — it is currently inert.

### 11.3 Parameter validation as injection defense

* **Command names** are scrubbed to `[-_0-9A-Za-z ]` and case-normalized (`NamesValidator.cs:17,31-43`).
* **Arguments** are scrubbed to `[-_0-9A-Za-z .*?\[\]|"~!@#$%^&*()]` (`NamesValidator.cs:22,51-60`) — shell
  metacharacters `` ` ``, `;`, `<`, `>`, `\`, `/`, `$(`, `{}` are removed *before* a command sees them.
* **Type conversion happens before execution.** A parameter declared `typeof(int)` can never deliver a non-numeric
  string into your code; failures become `IsValid == false` with an explanatory `ValidationError`
  (§5.5), and `GetValue<T>()` throws rather than returning unvalidated text (`AbstractParameterValue.cs:42-48`).
* **Allow-lists** (`AllowedValues`) are enforced for ordered (`CommandParameters.cs:258-262`) and named
  (`:206-210`) parameters — case-insensitive, throwing `ArgumentException`. WARNING: **Not enforced for suffix
  parameters** (§4.7) and irrelevant for flags (§4.6).
* **Environment isolation:** every command runs against a snapshot child; only `ModifiesEnvironment` registrations
  reach globals, and `ControllerEnvironmentContext.UpdateEnvironment` filters foreign command prefixes
  (`ControllerEnvironmentContext.cs:222-247`).
* **Author responsibilities** (`SECURITY.md:367-412`): never echo secrets; always supply defaults for env reads;
  prefer allow-lists; dispose resources; avoid unbounded loops; don't reconstruct shell commands from parameters
  (`SECURITY.md:158-165`).

### 11.4 Audit logging

**`IAuditLogger`** — `src/Xcaciv.Command.Interface/IAuditLogger.cs:9-55`:
```csharp
void LogCommandExecution(string commandName, string[] parameters, DateTime executedAt,
                         TimeSpan duration, bool success, string? errorMessage = null);  // :20-26
void LogEnvironmentChange(string variableName, string? oldValue, string? newValue,
                          string changedBy, DateTime changedAt);                          // :36-41
void LogAuditEvent(AuditEvent auditEvent);                                                // :48  (preferred)
IAuditMaskingConfiguration? MaskingConfiguration { get; set; }                             // :54
```

**`AuditEvent`** — `src/Xcaciv.Command.Interface/AuditEvent.cs:10-68`, a `sealed class` with `init` members:
`CorrelationId` (default new GUID, `:16`), **`required string CommandName`** (`:21`), `PackageOrigin` (`:26`),
**`required string[] Parameters`** (`:31`), `ExecutedAt` (default `DateTime.UtcNow`, `:36`), `Duration` (`:41`),
`Success` (`:46`), `ErrorMessage` (`:51`), `PipelineStage` (`:57`), `PipelineTotalStages` (`:62`),
`IReadOnlyDictionary<string,string>? Metadata` (`:67`).

**Emission point** — `CommandExecutor.ExecuteCommandWithErrorHandling`'s `finally` (`CommandExecutor.cs:231-255`):
start time, duration, `Success = (resultFailures.Count == 0)`, `PackageOrigin = PackageDescription?.FullPath ??
"built-in"`, `Parameters = ioContext.Parameters`, and the pipeline stage numbers from the IO context.
**Exactly one event per command execution**, including per pipeline stage.

**`IAuditMaskingConfiguration`** — `src/Xcaciv.Command.Interface/IAuditMaskingConfiguration.cs:4-12`:
`RedactedParameterNames`, `RedactedParameterPatterns`, `RedactionPlaceholder` (all `init`),
`string[] ApplyMasking(string[])`, `bool ShouldRedact(string)`.

**`AuditMaskingConfiguration`** — `src/Xcaciv.Command/AuditMaskingConfiguration.cs:12`:
* Default redacted names (`:18-29`): `password, pwd, secret, token, apikey, api_key, connectionstring, conn, credential`.
* Default patterns (`:34-41`): `*password*, *secret*, *token*, *key*, *credential*`, matched by a simple
  `*`-wildcard matcher (`:66-88`).
* `RedactionPlaceholder = "[REDACTED]"` (`:46`).
* `ApplyMasking` (`:95-119`): WARNING — **only rewrites tokens of the form `-name=value` / `--name=value`** — it splits on
  `'='` and requires two parts (`:107-111`). The framework's own `-name value` (space-separated) form is **NOT
  masked**, and the argument tokenizer strips `=` anyway (§8.3). **Treat parameter masking as effectively
  non-functional for this framework's own syntax; do not put secrets in command-line parameters.**

**`StructuredAuditLogger`** — `src/Xcaciv.Command/StructuredAuditLogger.cs:12`:
* Writes one JSON line per event to a `TextWriter` (`Console.Out` by default, `:25-36`), camelCase, non-indented (`:14-18`).
* `MaskingConfiguration` defaults to a fresh `AuditMaskingConfiguration` (`:41`).
* `LogAuditEvent` (`:97-123`) emits `{eventType:"CommandExecution", correlationId, commandName, packageOrigin,
  parameters, timestamp, durationMs, success, errorMessage, pipelineStage, pipelineTotalStages, metadata}`.
* `LogEnvironmentChange` (`:73-92`) emits `{eventType:"EnvironmentChange", timestamp, variableName, oldValue,
  newValue, changedBy}` with **variable-name-based redaction** of old/new values (`:85-86`) — this path *does* work,
  because it matches on the variable name.
* `LogCommandExecution` (`:47-68`) is a shim onto `LogAuditEvent`.

**`NoOpAuditLogger`** — `src/Xcaciv.Command/NoOpAuditLogger.cs:10` — every method empty; the default.

`changedBy` is hard-coded to `"system"` at the only call site (`EnvironmentContext.cs:91`).

### 11.5 Exceptions

`src/Xcaciv.Command.Interface/Exceptions/`: abstract base `XcCommandException : Exception` (`XcCommandException.cs:9`);
`InValidConfigurationException : XcCommandException` (`InValidConfigurationException.cs:9`);
and three that derive **directly from `Exception`**, not from `XcCommandException`:
`NoPackageDirectoryFoundException` (`:3`), `NoPluginFilesFoundException` (`:3`), `NoPluginsFoundException` (`:3`).
WARNING: You cannot catch all framework exceptions with `catch (XcCommandException)`.

---

## 12. The built-in commands (worked examples of the conventions)

Registered by `RegisterBuiltInCommands()` under package key `"Default"` (`CommandController.cs:182-190`).

### 12.1 `SAY` — `src/Xcaciv.Command/Commands/SayCommand.cs`

```csharp
[CommandRegister("Say", "Like echo but more valley.", Prototype = "SAY <thing to print>")]   // :14
[CommandParameterSuffix("text", "Text to output")]                                            // :15
[CommandHelpRemarks("Use double quotes to include environment variables in the format %var%.")] // :16
[CommandHelpRemarks("Piped input will be evalueated for env vars before being passed out.")]    // :17
public class SayCommand : AbstractCommand
```
* `HandleExecution` (`:20-30`): reads `text`; if it contains `%`, runs `ProcessEnvValues`; returns
  `Success(value, this.OutputFormat)`. Falls back to `Success(string.Empty, …)` when `text` is missing/invalid.
* `ProcessEnvValues` (`:32-48`): `Regex(@"%(.\w*?)%")` replace, substituting `env.GetValue(variable)`; leaves the
  token in place (wrapped in extra `%`) when the variable resolves empty.
* `HandlePipedChunk` (`:50-55`): expands env vars in `pipedChunk.Output ?? string.Empty` and passes it through.
* **Conventions demonstrated:** suffix capture, `TryGetValue`+`IsValid` guard, `this.OutputFormat` propagation,
  multiple help remarks, symmetric non-piped/piped behaviour. Note the class is `public`.

### 12.2 `SET` — `src/Xcaciv.Command/Commands/SetCommand.cs`

```csharp
[CommandRegister("Set", "Set environment values", Prototype = "SET <varname> <value>")]  // :13
[CommandParameterOrdered("Key", "Key used to access value")]                              // :14
[CommandParameterOrdered("Value", "Value stored for accessing", UsePipe = true)]          // :15
[CommandHelpRemarks("This is a special command that is able to modify the Env outside its own context.")] // :16
internal class SetCommand : AbstractCommand
```
* `HandleExecution` (`:19-30`): both ordered params → `env.SetValue(key, value)`; returns **empty success**
  (nothing to display).
* `HandlePipedChunk` (`:32-42`): **appends** each chunk — `env.SetValue(key, env.GetValue(key) + pipedChunk.Output)`.
* `OnStartPipe` (`:44-49`): clears the variable (`environment.SetValue(key, String.Empty)`) before accumulation.
* **Registered with `modifiesEnvironment: true`** (`CommandController.cs:188`) — this is the only built-in that
  writes globals.
* **Conventions demonstrated:** `UsePipe` on the second ordered parameter (so `… | SET NAME` works), `OnStartPipe`
  for per-pipe initialisation, empty-success to stay silent, and the `internal` visibility pattern (fine — the
  factory reflects over the type).

### 12.3 `ENV` — `src/Xcaciv.Command/Commands/EnvCommand.cs`

```csharp
[CommandRegister("ENV", "Output all environment variables", Prototype = "ENV")]  // :13
internal class EnvCommand : AbstractCommand
```
Both `HandleExecution` (`:16-24`) and `HandlePipedChunk` (`:26-34`) enumerate `env.GetEnvironment()` and build
`"{Key} = {Value}\n"` lines. WARNING: The interpolated verbatim string `@$"…\n"` emits a **literal backslash-n**, not a
newline. **Conventions demonstrated:** zero-parameter command, environment enumeration.

### 12.4 `REGIF` — `src/Xcaciv.Command/Commands/RegifCommand.cs`

```csharp
[CommandRegister("REGIF", "Regular expression filter. Outputs the string if it matches",
    Prototype = @"<some command> | regif ""<regex expression>"" ""<string to check>""")]  // :14
[CommandParameterOrdered("Regex", "Regular Expression")]                                   // :15
[CommandParameterOrdered("String", "String to match", UsePipe = true)]                     // :16
public class RegifCommand : AbstractCommand
```
* Instance state: `protected Regex? expression` (`:22`), compiled lazily and cached across chunks
  (`:32`, `:51-54`).
* `HandleExecution` (`:26-45`): compiles the regex, tests `string`, returns the match or empty.
* `HandlePipedChunk` (`:47-62`): compiles once, returns the input when it matches, **`string.Empty` when it does
  not** — the host drops empty successes, so non-matches vanish. That is the filter idiom.
* **Conventions demonstrated:** stateful command instance across a pipe, `UsePipe` on the data parameter,
  filtering via empty-success.

---

## 13. Dependency injection — `Xcaciv.Command.DependencyInjection`

`src/Xcaciv.Command.DependencyInjection/ServiceCollectionExtensions.cs:14`

| Extension | Line | Effect |
|---|---|---|
| `AddXcacivCommand(this IServiceCollection)` | `:16-19` | delegates to the configure overload with a no-op |
| `AddXcacivCommand(this IServiceCollection, Action<CommandControllerOptions>)` | `:21-46` | `services.Configure(configure)` then `TryAddSingleton` for: `ICommandRegistry→CommandRegistry`, `ICommandFactory→CommandFactory`, `ICommandExecutor→CommandExecutor`, `IPipelineExecutor→PipelineExecutor`, `ICommandLoader→CommandLoader`, `ICrawler→Crawler`, `IVerifiedSourceDirectories→new VerifiedSourceDirectories(new FileSystem())`, `IAuditLogger→NoOpAuditLogger`, `IOutputEncoder→NoOpEncoder`, `IHelpService→HelpService`, `ICommandController→CommandController` |
| `AddXcacivCommand(this IServiceCollection, IConfiguration)` | `:48-62` | binds `CommandControllerOptions` from `"Xcaciv:Command"` and `PipelineOptions` from `"Xcaciv:Command:Pipeline"`, then calls the configure overload |
| `WithAuditLogger<T>()` | `:64-69` | `Replace(Singleton<IAuditLogger, T>)` |
| `WithStructuredAuditLogging()` | `:71-75` | `Replace(Singleton<IAuditLogger, StructuredAuditLogger>)` |
| `WithStructuredAuditLogging(TextWriter output)` | `:83-89` | `AddSingleton<IAuditLogger>(_ => new StructuredAuditLogger(output))` |
| `WithOutputEncoder<T>()` | `:91-96` | `Replace(Singleton<IOutputEncoder, T>)` |
| `ConfigurePipeline(Action<PipelineOptions>)` | `:98-104` | `services.Configure(configure)` |

**All lifetimes are singleton.** `TryAdd*` means your own prior registration wins.

WARNING — Sharp edges you must handle yourself:
1. **The registered `ICommandController` is `CommandController`, resolved by the container — which will pick the
   greediest constructor it can satisfy.** With all seven services registered, that is the six-arg DI constructor
   (`CommandController.cs:94-114`), so the container-built `CommandFactory` (with an `IServiceProvider`) is used.
   Note `CommandFactory`'s own registered constructor is `CommandFactory(IServiceProvider? = null)`
   (`CommandFactory.cs:20-24`).
2. **`CommandControllerOptions` and `PipelineOptions` are bound but never applied.** Nothing reads
   `IOptions<CommandControllerOptions>` (grep) — `EnableDefaultCommands`, `PackageDirectories`,
   `RestrictedDirectory`, `HelpCommand`, `Verbose` and every `PipelineOptions` value are inert under DI. You must
   still call `RegisterBuiltInCommands()`, `AddPackageDirectory()`, `LoadCommands()`, and set
   `controller.PipelineConfig` yourself. `CommandControllerFactory.Create` is the only code path that honours the
   options object.
3. **The registered `IAuditLogger`/`IOutputEncoder` are not automatically pushed onto the controller** —
   `CommandController` initialises its own `NoOpAuditLogger`/`NoOpEncoder` (`:109-110`). Assign
   `controller.AuditLogger = sp.GetRequiredService<IAuditLogger>()` explicitly.
4. **Commands themselves are resolvable through DI**: register your command type in the container and
   `CommandFactory.CreateCommand` will prefer `_serviceProvider.GetService(commandType)` when
   `Type.GetType(fullTypeName)` resolves (`CommandFactory.cs:107-111`). WARNING: For a **singleton** registration this
   means the *same instance* is reused across executions and across pipeline stages — avoid per-run mutable state
   (`RegifCommand`'s cached `expression` would leak between runs). Register commands **transient** if you rely on
   per-execution state.

---

## 14. The canonical tool skeleton

Verified line-by-line against `AbstractCommand` (`src/Xcaciv.Command.Core/AbstractCommand.cs`), the attribute
sources (§4), `CommandResult<T>` (`src/Xcaciv.Command.Interface/CommandResult.cs`), and the built-in commands.
This compiles against **3.3.4**.

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Xcaciv.Command.Core;                       // AbstractCommand
using Xcaciv.Command.Interface;                  // IResult<T>, CommandResult<T>, ResultFormat,
                                                 // IIoContext, IEnvironmentContext, ICommandParameter
using Xcaciv.Command.Interface.Attributes;       // CommandRegister, CommandRoot, CommandParameter*, CommandFlag,
                                                 // CommandHelpRemarks
using Xcaciv.Command.Interface.Parameters;       // IParameterValue

namespace MyApp.Tools;

// ─────────────────────────────────────────────────────────────────────────────
// 1. REGISTRATION.  [CommandRegister] is MANDATORY: without it AbstractCommand.Command
//    throws InvalidOperationException and CommandRegistry silently refuses to register.
//    Ctor is exactly (command, description); Prototype/Alias/Version are named properties.
//    Prototype defaults to "" (blank usage line). Use the literal "todo" to make
//    HelpService auto-generate a prototype from the parameter indicators.
//    The command name is normalized to UPPERCASE.
// ─────────────────────────────────────────────────────────────────────────────
[CommandRegister("Fetch", "Fetch a resource and emit its body",
    Prototype = "FETCH <url> [-timeout <n>] [-format json|text] [-v] [<extra words...>]",
    Alias     = "GET",        // metadata only - the framework never dispatches on Alias
    Version   = "1.0.0")]     // metadata only

// 2. OPTIONAL SUB-COMMAND GROUPING.  With [CommandRoot("net", ...)] this becomes `NET FETCH ...`.
//    OMIT this attribute for a top-level command - but then never read AbstractCommand.RootCommand,
//    because its getter throws when the attribute is absent (AbstractCommand.cs:50).
// [CommandRoot("net", "Networking commands")]

// 3. PARAMETERS.  ALL parameter attributes go ON THE CLASS (AttributeTargets.Class,
//    AllowMultiple = true).  NOT on properties or fields.
//    Parsing order is fixed: Ordered -> Flags -> Named -> Suffix.

//    ORDERED: positional, consumed front-to-back in declaration order.
//    IsRequired defaults to TRUE for ordered parameters - opt out explicitly.
[CommandParameterOrdered("url", "Resource URL to fetch", UsePipe = true)]

//    FLAG: presence => true.  ALWAYS materialised as bool; DataType/DefaultValue/AllowedValues ignored.
//    Matched as -v / --v / -verbose / --verbose (case-insensitive) when ShortAlias is set.
[CommandFlag("verbose", "Emit trace-level detail", ShortAlias = "v")]

//    NAMED: -name value.  Not required by default.  DataType drives conversion & GetValue<T>.
[CommandParameterNamed("timeout", "Timeout in seconds",
    DataType     = typeof(int),
    DefaultValue = "30",
    ShortAlias   = "t")]

//    NAMED with an ALLOW-LIST.  Enforced case-insensitively at parse time (ArgumentException on
//    violation).  Because DefaultValue is set BEFORE AllowedValues here, the setter validates it;
//    if you omit DefaultValue entirely, AllowedValues[0] ("text") becomes the default automatically.
[CommandParameterNamed("format", "Output shape",
    DefaultValue  = "text",
    AllowedValues = new[] { "text", "json" })]

//    SUFFIX: swallows ALL remaining tokens, joined with single spaces, as ONE string.
//    Declare at most one, and declare it last.  NOTE: AllowedValues is NOT enforced for suffix.
[CommandParameterSuffix("note", "Free-text note appended to the output", IsRequired = false)]

// 4. HELP REMARKS - repeatable; rendered under a "Remarks:" heading.
[CommandHelpRemarks("Quote any URL or path: the argument tokenizer splits unquoted '.', '/' and ':'.")]
[CommandHelpRemarks("When piped, the 'url' parameter is supplied by the upstream chunk (UsePipe = true).")]
public sealed class FetchCommand : AbstractCommand
{
    // ─────────────────────────────────────────────────────────────────────────
    // 5. PARAMETER FIELD INJECTION (optional convenience).
    //    ONLY public INSTANCE FIELDS are injected - never properties, never private fields.
    //    Matched to parameter names case-insensitively; injected via TryGetValue<TFieldType>,
    //    so a type mismatch silently leaves the field alone.
    //    WARNING: Nothing is injected when the command is invoked with ZERO arguments
    //      (AbstractCommand.ProcessParameters early-returns at :112-115),
    //      so always keep a sane field initialiser AND a dictionary fallback.
    // ─────────────────────────────────────────────────────────────────────────
    public string? Url;         // <- from [CommandParameterOrdered("url", ...)]
    public int     Timeout;     // <- from [CommandParameterNamed("timeout", DataType = typeof(int))]
    public bool    Verbose;     // <- from [CommandFlag("verbose", ...)]
    public string? Format;      // <- from [CommandParameterNamed("format", ...)]
    public string? Note;        // <- from [CommandParameterSuffix("note", ...)]

    // Per-instance state. A fresh instance is created per execution by CommandFactory,
    // UNLESS you registered the type as a DI singleton - see §13.4.
    private HttpClient? _http;
    private int _chunkCount;

    public FetchCommand()
    {
        // 6. OUTPUT FORMAT - metadata carried on every IResult; the host may render on it.
        //    protected set, so assign it here.  Default is ResultFormat.General.
        OutputFormat = ResultFormat.General;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. NON-PIPED PATH.  Called exactly ONCE, and may emit exactly ONE chunk.
    //    (Override Main yourself if you need multiple chunks without a pipe.)
    // ─────────────────────────────────────────────────────────────────────────
    public override IResult<string> HandleExecution(
        Dictionary<string, IParameterValue> parameters,
        IEnvironmentContext env)
    {
        // Always guard: TryGetValue + IsValid, then GetValue<T>() with T == the declared DataType.
        // GetValue<T> throws InvalidOperationException when !IsValid and InvalidCastException on
        // any type mismatch (exact type equality - GetValue<int> on a long parameter throws).
        var url = parameters.TryGetValue("url", out var pUrl) && pUrl.IsValid
            ? pUrl.GetValue<string>()
            : (Url ?? string.Empty);

        var timeout = parameters.TryGetValue("timeout", out var pTo) && pTo.IsValid
            ? pTo.GetValue<int>()        // declared DataType = typeof(int)
            : 30;

        var verbose = parameters.TryGetValue("verbose", out var pV) && pV.IsValid
            && pV.GetValue<bool>();      // flags are always bool

        var format = parameters.TryGetValue("format", out var pF) && pF.IsValid
            ? pF.GetValue<string>()
            : "text";

        var note = parameters.TryGetValue("note", out var pN) && pN.IsValid
            ? pN.GetValue<string>()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(url))
        {
            // Failures are DATA, not exceptions. Note: Failure() takes no ResultFormat.
            return CommandResult<string>.Failure("FETCH requires a URL. Try 'HELP FETCH'.");
        }

        // 8. ENVIRONMENT.
        //    Values declared in GetDefaultEnvironment() are seeded by the host under the
        //    "{COMMANDNAME}_" prefix - so read FETCH_BASE_URL, not BASE_URL.
        //    WARNING: GetValue's storeDefault defaults to TRUE: a miss WRITES the default back
        //      and flips HasChanged.  Pass storeDefault:false for a pure read.
        var baseUrl = env.GetValue("FETCH_BASE_URL", "https://example.invalid", storeDefault: false);

        //    A command may persist its OWN state without ModifiesEnvironment, as long as the key
        //    carries its command-name prefix - the host routes it to this command's private bucket.
        env.SetValue("FETCH_LAST_URL", url);
        //    Writing an UNPREFIXED global key requires the host to have registered this command with
        //    modifiesEnvironment: true - e.g. controller.AddCommand("MyPkg", new FetchCommand(), true);

        try
        {
            var body = DoFetch(baseUrl, url, timeout, format, verbose);
            var payload = string.IsNullOrEmpty(note) ? body : $"{body}\n-- {note}";
            return CommandResult<string>.Success(payload, this.OutputFormat);
        }
        catch (Exception ex)
        {
            // Prefer returning a Failure over throwing: a throw is caught by CommandExecutor and
            // reduced to "Error executing FETCH (see trace for more info)".
            return CommandResult<string>.Failure($"FETCH failed for '{url}': {ex.Message}", ex);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9. PIPED PATH.  Called ONCE PER UPSTREAM CHUNK - and only for chunks that are
    //    BOTH successful AND non-empty: AbstractCommand.Main already forwards failures
    //    verbatim (:81-85) and skips empty outputs (:87).
    //    Signature since v3.2.3: the first argument is IResult<string>, not string.
    // ─────────────────────────────────────────────────────────────────────────
    public override IResult<string> HandlePipedChunk(
        IResult<string> pipedChunk,
        Dictionary<string, IParameterValue> parameters,
        IEnvironmentContext env)
    {
        // Output is nullable - always coalesce.
        var input = pipedChunk.Output ?? string.Empty;

        // Available for correlation / diagnostics, even though a failure never reaches here
        // under AbstractCommand:  pipedChunk.IsSuccess, .ErrorMessage, .Exception,
        //                        .CorrelationId, .OutputFormat
        _chunkCount++;

        var timeout = parameters.TryGetValue("timeout", out var pTo) && pTo.IsValid
            ? pTo.GetValue<int>()
            : 30;

        try
        {
            var body = DoFetch(env.GetValue("FETCH_BASE_URL", "https://example.invalid", false),
                               input, timeout, Format ?? "text", Verbose);

            // Return an EMPTY success to swallow this chunk (the host drops empty successes) -
            // that is how REGIF implements filtering.
            return CommandResult<string>.Success(body, this.OutputFormat);
        }
        catch (Exception ex)
        {
            return CommandResult<string>.Failure($"FETCH failed for '{input}': {ex.Message}", ex);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 10. PIPE LIFECYCLE HOOKS - protected virtual, invoked ONLY on the piped path,
    //     around the whole chunk loop (AbstractCommand.cs:77, :93).
    // ─────────────────────────────────────────────────────────────────────────
    protected override void OnStartPipe(
        Dictionary<string, IParameterValue> processedParameters,
        IEnvironmentContext environment)
    {
        _chunkCount = 0;
        _http ??= new HttpClient();
        base.OnStartPipe(processedParameters, environment);
    }

    protected override void OnEndPipe(
        Dictionary<string, IParameterValue> processedParameters,
        IEnvironmentContext environment)
    {
        environment.SetValue("FETCH_CHUNKS", _chunkCount.ToString());
        base.OnEndPipe(processedParameters, environment);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 11. DEFAULT ENVIRONMENT.  Declare the variables this tool consumes and their defaults.
    //     The host stores them as "FETCH_BASE_URL"/"FETCH_RETRIES"; inside the command you read
    //     the PREFIXED keys (see step 8).
    // ─────────────────────────────────────────────────────────────────────────
    public override Dictionary<string, string> GetDefaultEnvironment() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "BASE_URL", "https://example.invalid" },
            { "RETRIES",  "3" },
        };

    // 12. Optional: override only if you are NOT using attribute-based parameters.
    //     The default aggregates Ordered + Flag + Named + Suffix attributes.
    // public override List<ICommandParameter> GetParameters() => base.GetParameters();

    // ─────────────────────────────────────────────────────────────────────────
    // 13. DISPOSAL.  ICommandDelegate : IAsyncDisposable.  Override for owned resources.
    //     WARNING: CommandExecutor does NOT dispose the executing instance
    //       (CommandExecutor.cs:169-256); only CommandRegistry's GetDefaultEnvironment probe
    //       disposes what it creates.  So release anything critical inside Main/OnEndPipe as well.
    // ─────────────────────────────────────────────────────────────────────────
    public override async ValueTask DisposeAsync()
    {
        _http?.Dispose();
        _http = null;
        await base.DisposeAsync().ConfigureAwait(false);
    }

    private string DoFetch(string baseUrl, string url, int timeoutSeconds, string format, bool verbose)
        => $"[{format}] {baseUrl} -> {url} (timeout {timeoutSeconds}s{(verbose ? ", verbose" : "")})";
}
```

### 14.1 Hosting that tool

```csharp
using Xcaciv.Command;
using Xcaciv.Command.Interface;

var controller = new CommandController();
controller.RegisterBuiltInCommands();                       // SAY, SET, ENV, REGIF
controller.AddCommand("MyPkg", new FetchCommand());         // only the TYPE is kept
// controller.AddCommand("MyPkg", new FetchCommand(), modifiesEnvironment: true); // to write globals

controller.AuditLogger  = new StructuredAuditLogger();      // JSON lines to Console.Out
controller.PipelineConfig = new PipelineConfiguration
{
    MaxChannelQueueSize = 1_000,
    BackpressureMode    = PipelineBackpressureMode.Block,
    StageTimeoutSeconds = 30,
};

var io  = new MemoryIoContext();                            // or your own AbstractTextIo subclass
var env = controller.GetEnvironment();                      // pre-seeded with every GetDefaultEnvironment()

await controller.Run("FETCH \"https://example.com/a\" -timeout 5 -format json -v", io, env);
await controller.Run("SAY \"https://example.com/a\" | FETCH -format json | REGIF \"^\\[json\\]\"", io, env);

foreach (var line in io.Output) Console.WriteLine(line);
```

### 14.2 Implementing `ICommandDelegate` directly (when you need full control of the stream)

Use this when a single command must emit **many** chunks in the non-piped case, or must see failed upstream chunks.
Reference implementations: `src/tests/zTestCommandPackage/EchoCommand.cs:14-82` and
`src/Xcaciv.Command.Extensions.Commandline/CommandLineCommand.cs:20-113`.

```csharp
[CommandRegister("ECHO", "Emit each parameter as its own chunk")]
public class EchoCommand : ICommandDelegate
{
    public string Command     => "ECHO";
    public string RootCommand => string.Empty;   // empty => no root; never throws

    public Dictionary<string, string> GetDefaultEnvironment() => new();
    public List<ICommandParameter>    GetParameters()         => new();

    public async IAsyncEnumerable<IResult<string>> Main(IIoContext io, IEnvironmentContext env)
    {
        await io.AddTraceMessage("ECHO start");
        if (io.HasPipedInput)
        {
            await foreach (var chunk in io.ReadInputPipeChunks())
            {
                if (!chunk.IsSuccess) { yield return chunk; continue; }   // YOU handle propagation here
                if (!string.IsNullOrEmpty(chunk.Output))
                    yield return CommandResult<string>.Success(chunk.Output);
            }
        }
        else
        {
            foreach (var p in io.Parameters)                              // many chunks, no pipe
                yield return CommandResult<string>.Success(p);
        }
        await io.AddTraceMessage("ECHO end");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

### 14.3 Author's checklist

1. `[CommandRegister("NAME", "description")]` on the class — **required**.
2. Parameter attributes **on the class**, never on members.
3. Ordered parameters are **required by default**; set `IsRequired = false` when optional.
4. At most one `[CommandParameterSuffix]`, declared last; its value is one joined **string**.
5. Mark the pipe-fed parameter `UsePipe = true`.
6. Inject via **public fields** only; still guard with the `parameters` dictionary — zero-arg invocations inject nothing.
7. `GetValue<T>()` requires `T` to equal the declared `DataType` exactly; check `IsValid` first, or use `TryGetValue<T>`.
8. Return `CommandResult<string>.Success(value, this.OutputFormat)`; return `Success(string.Empty, …)` to emit nothing.
9. Return `CommandResult<string>.Failure(msg, ex)` instead of throwing.
10. Quote paths/URLs/regexes on the command line — the tokenizer splits unquoted `.` `/` `:` `\` and strips many symbols.
11. Environment reads use the `COMMANDNAME_` prefix; pass `storeDefault:false` for pure reads.
12. Override `DisposeAsync` for owned resources — and don't count on the executor calling it.

---

## 15. Quick API index

| I need to… | Use | Where |
|---|---|---|
| Define a tool | `class X : AbstractCommand` + `[CommandRegister]` | `AbstractCommand.cs:12`, `CommandRegisterAttribute.cs:10` |
| Emit output | `CommandResult<string>.Success(text, OutputFormat)` | `CommandResult.cs:16` |
| Emit an error | `CommandResult<string>.Failure(msg, ex)` | `CommandResult.cs:23` |
| Read a parameter | `p.IsValid ? p.GetValue<T>() : fallback` | `AbstractParameterValue.cs:38` |
| Read a parameter safely | `p.TryGetValue<T>(out var v)` | `AbstractParameterValue.cs:81` |
| Read config | `env.GetValue("CMD_KEY", "default", storeDefault:false)` | `IEnvironmentContext.cs:50` |
| Persist private state | `env.SetValue("CMD_KEY", value)` | `CommandController.cs:276-279` |
| Persist a global | register with `modifiesEnvironment: true` | `CommandController.cs:271-273` |
| Trace | `await io.AddTraceMessage(msg)` | `IIoContext.cs:126` |
| Show progress | `await io.SetProgress(total, step)` | `IIoContext.cs:138` |
| Ask the user | `await io.PromptForCommand(prompt)` | `IIoContext.cs:80` |
| Host a controller | `new CommandController()` + `RegisterBuiltInCommands()` | `CommandController.cs:45,182` |
| Load plugins | `AddPackageDirectory(dir)` + `LoadCommands("bin")` | `CommandController.cs:165,174` |
| Run | `await controller.Run(line, io, env, ct)` | `CommandController.cs:236` |
| Show help | run `"HELP"` / `"HELP CMD"` / `"CMD --HELP"` | `CommandController.cs:299-317` |
| Tune the pipeline | `controller.PipelineConfig = new PipelineConfiguration { … }` | `CommandController.cs:155` |
| Audit | `controller.AuditLogger = new StructuredAuditLogger()` | `CommandController.cs:133` |
| Wire DI | `services.AddXcacivCommand(configuration)` | `ServiceCollectionExtensions.cs:48` |

---

## 16. Documentation vs. source disagreements (source wins)

| # | Claim | Where claimed | Source reality |
|---|---|---|---|
| 1 | Current version is 3.3.0 / 3.3.3 / 3.2.2 | `README.md:94`, `src/Directory.Packages.props:14-17`, `docs/QUICK_REFERENCE.md:1,199` | **3.3.4** in all six csproj `<Version>` elements |
| 2 | Parameter attributes target **properties** | `docs/learn/api-attributes.md:136,207,255,304`; `docs/learn/getting-started-create-command.md:23,58-101` | `AttributeTargets.Class, AllowMultiple = true, Inherited = false` on all four (`CommandParameterOrderedAttribute.cs:13`, `CommandParameterNamedAttribute.cs:9`, `CommandFlagAttribute.cs:13`, `CommandParameterSuffixAttribute.cs:9`) |
| 3 | Parameter ctors take `required`/`usePipe`/`allowedValues` args; property is `Required`; there is a `Description` property | `docs/learn/api-attributes.md:139-151,210-221,307-317` | Ctors are `(string name, string description)` only; properties are `IsRequired`, `UsePipe`, `AllowedValues`, `ValueDescription` (`AbstractCommandParameterAttribute.cs:11-95`) |
| 4 | `CommandRegisterAttribute` has a third ctor arg `prototype = "todo"` | `docs/learn/api-attributes.md:13-16`; `getting-started-create-command.md:49` | Two-arg ctor (`CommandRegisterAttribute.cs:17`); `Prototype` is a named property defaulting to `String.Empty` (`:43`) |
| 5 | Suffix parameters produce `string[]` | `docs/learn/api-attributes.md:352`; `getting-started-create-command.md:101` | Remaining tokens are **joined into one string** (`CommandParameters.cs:312-313`) |
| 6 | `HandleExecution(string[] parameters, …) → string`; `HandlePipedChunk(string, …)`; `Help()` override; `env.GetVariable` | `docs/learn/getting-started-create-command.md:26-34,111-121,147-162,172-175`; `docs/parameter-field-injection-implementation.md:127-131`; `SECURITY.md:96,141,178` | `IResult<string> HandleExecution(Dictionary<string,IParameterValue>, IEnvironmentContext)` (`AbstractCommand.cs:245`) and `IResult<string> HandlePipedChunk(IResult<string>, …)` (`:255`); no `Help()` on the interface; the accessor is `GetValue` (`IEnvironmentContext.cs:50`) |
| 7 | `Required = true` in attribute usage | `docs/parameter-field-injection-implementation.md:108-109`; `docs/examples/parameter-field-injection-example.md:25-26` | The property is **`IsRequired`** (`AbstractCommandParameterAttribute.cs:12`) |
| 8 | Field injection lives in `CommandFactory` | `docs/examples/parameter-field-injection-example.md:5-13,38` | Moved to `AbstractCommand.SetParameterFields` (`AbstractCommand.cs:136-179`); `CommandFactory` no longer touches fields |
| 9 | `controller.GetHelpAsync(...)` | `docs/QUICK_REFERENCE.md:48-49,84-85,105`; `CHANGELOG.md:180` | `GetHelpAsync` exists only on `ICommandExecutor` (`ICommandExecutor.cs:34,39`); help is reached via `Run("HELP …")` or `--HELP` (`CommandController.cs:299-317`) |
| 10 | `ICommandDelegate` includes `Help` and `OneLineHelp` | `COMMAND_TEMPLATE.md:19` | Interface has only `Command`, `RootCommand`, `Main`, `GetDefaultEnvironment`, `GetParameters` (`ICommandDelegate.cs:22-69`) |
| 11 | `pipedChunk.ResultFormat` | `COMMAND_TEMPLATE.md:66` | The property is `OutputFormat` (`IResult.cs:37`) |
| 12 | Check `pipedChunk.IsSuccess` inside `HandlePipedChunk` to handle upstream failure | `COMMAND_TEMPLATE.md:50-56,326-333,392-393` | Under `AbstractCommand`, failed and empty chunks never reach `HandlePipedChunk` (`AbstractCommand.cs:81-87`). Only relevant when implementing `ICommandDelegate` directly |
| 13 | `RawValue` was removed / made internal | `CHANGELOG.md:93` | `IParameterValue.RawValue` is **public** (`IParameterValue.cs:32`) |
| 14 | Converter supports `DateTimeOffset` and `TimeSpan` | `CHANGELOG.md:74` | `SupportedTypes` = string, int, long, double, float, decimal, bool, Guid, DateTime, JsonElement (`DefaultParameterConverter.cs:12-24`) |
| 15 | `AllowedValues` can be assigned after construction | `docs/AllowedValues-validation-feature.md:83-88` | It is `init`-only (`AbstractCommandParameterAttribute.cs:53`) |
| 16 | Suffix `AllowedValues` gives validation | `docs/parameter-help-text-consistency.md:33-38,74-79` | Only affects help text; `ProcessSuffixParameters` has **no** allow-list check (`CommandParameters.cs:273-316`) |
| 17 | "No backpressure; channels are unbounded by default"; Xcaciv.Loader **2.0.1**; `AssemblySecurityPolicy.Default` | `SECURITY.md:225,25,59` | Bounded channels with three backpressure modes (`PipelineExecutor.cs:104-107,216-222`); Loader **2.1.2** (`src/Directory.Packages.props:8`); default policy is **`Strict`** (`Crawler.cs:31`, `AssemblySecurityConfiguration.cs:16`) |
| 18 | Output encoders are applied to every `OutputChunk` | `IIoContext.cs:162-172`; `IOutputEncoder.cs:12-18` | `AbstractTextIo.SetOutputEncoder` is an empty no-op (`AbstractTextIo.cs:121-125`); `Encode` is called from tests only |
| 19 | `SetProgress` returns percent complete | `IIoContext.cs:132-137` | `MemoryIoContext.SetProgress` returns `step` (`MemoryIoContext.cs:57-61`) |
| 20 | Environment keys are "stored as uppercase" | `IEnvironmentContext.cs:30`; `IControllerEnvironmentContext.cs:30` | Stored verbatim in an `OrdinalIgnoreCase` dictionary (`EnvironmentContext.cs:18`) |
| 21 | `examples/AllowedValuesExample.cs` compiles | `examples/AllowedValuesExample.cs:32-52` | It overrides only `HandleExecution`; `HandlePipedChunk` is also abstract (`AbstractCommand.cs:255`) |
| 22 | `PipelineOptions` / `CommandControllerOptions` configure the running controller under DI | `PipelineOptions.cs`, `CommandControllerOptions.cs`, `ServiceCollectionExtensions.cs:48-62` | Nothing reads `IOptions<…>`; only `CommandControllerFactory.Create` honours the controller options (`CommandControllerFactory.cs:19-53`) |
| 23 | `PipelineConfiguration.ExecutionTimeoutSeconds` / `MaxStageOutputBytes` / `MaxStageOutputItems` limit execution | `PipelineConfiguration.cs:29,43,50` | Only `StageTimeoutSeconds`, `MaxChannelQueueSize`, `BackpressureMode` are read by `PipelineExecutor` |
| 24 | Audit masking protects parameter secrets | `SECURITY.md:199-210`; `AuditMaskingConfiguration.cs:91-94` | `ApplyMasking` only handles `-name=value`; the framework's `-name value` syntax is unmasked, and `=` is stripped by the tokenizer (`NamesValidator.cs:22`) |
| 25 | `README.md:20` calls the interface `Xc.Command.ICommandDelegate` | `README.md:20` | The namespace is `Xcaciv.Command.Interface` (`ICommandDelegate.cs:8`) |
