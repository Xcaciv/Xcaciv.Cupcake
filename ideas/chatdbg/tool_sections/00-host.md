## A. The host application

The product is rebuilt as a **Cupcake-patterned shell**: a host that contributes *no* command behaviour of its own,
and a capability surface that is entirely attribute-declared `ICommandDelegate` tools — some compiled in, most
loaded from a package directory. Everything the source product implemented as a hand-wired
`Dictionary<string, ICommand>` becomes a tool package.

Two conventions govern every rule in this chapter:

* **Framework version.** All signatures are **Xcaciv.Command 3.3.4** and **Xcaciv.Loader 2.1.2**. Cupcake itself is
  written against Xcaciv.Command 2.1.x, where `GetChild()` took arguments, output flowed as `string`, and
  `AbstractCommand.HandleExecution` took `string[]`. **The Cupcake *pattern* is version-independent; the Cupcake
  *code* is not.** Every override below is written against 3.3.4.
* **Traceability.** Each rule cites either a Cupcake pattern rule (`Cupcake §8 rule N`) or a named defect in the
  source product that it repairs (`fixes: …`).

---

### A.1 Project layout

| # | Project | Output | TFM | Contents | Why it exists |
|---|---|---|---|---|---|
| 1 | `Xcaciv.ChatDbg.Abstractions` | library | `net10.0` | Host↔tool contracts that are *not* framework contracts: `IModelBackend`, `IModelBackendFactory`, `ICancellationSignal`, `ISecretStore`, `IToolPackageManifest`, `TrustLevel`, `ChatTurn`, `TokenProbability`. References **`Xcaciv.Command.Interface` only**. | Backend tools and presentation tools must agree on a chat/token data shape without either one referencing the host. Cupcake has no analogue because Cupcake's tools share nothing; this product's tools do. |
| 2 | `Xcaciv.ChatDbg.Host` | library | `net10.0` | The entire shell: `Loop`, `HostStartup`, `PackageTrustGate`, `ConsoleIoContext`, `ScreenIoContext`, `BatchIoContext`, `ChatDbgAuditLogger`, `ChatDbgAuditMasking`, `Exceptions/LoadingException`, `Exceptions/TrustException`. **No `Main`.** | **Cupcake §8 rule 1** — the loop is a library so it can be unit-tested with fakes and re-hosted by two executables. |
| 3 | `Xcaciv.ChatDbg.Cli` | `Exe` | `net10.0` | `Program.cs`, ~20 lines. | **Cupcake §8 rule 2** — composition root for the line-oriented shell. |
| 4 | `Xcaciv.ChatDbg.Screen` | `Exe` | `net10.0` | `Program.cs`, ~25 lines. | **Cupcake §8 rule 2** — composition root for the full-screen shell. |
| 5–13 | `Xcaciv.ChatDbg.Tools.<Package>` ×9 | libraries | `net10.0` | One project per package in §F. Each references **`Xcaciv.Command.Interface` + `Xcaciv.Command.Core` + `Xcaciv.ChatDbg.Abstractions`** and *nothing else of ours*. | **Cupcake §8 rule 4** — the dependency arrow points tool → SDK, never tool → host, so the same assembly can be linked in *or* dropped into the package directory unchanged. |
| 14 | `Xcaciv.ChatDbg.Host.Tests` | xunit | `net10.0` | Hermetic. `FakeIoContext`, `FakeController`, `FakeEnvironment`, `FakeTrustGate`. Drives `Loop` end to end with no disk, no network, no native code. | **Cupcake §8 rule 7** — the fast, offline suite. |
| 15 | `Xcaciv.ChatDbg.Tools.IntegrationTests` | xunit | `net10.0` | Network-bound and native-bound: real model endpoints behind a skip-unless-configured guard, real GGUF load, real NuGet feed, real package load from a temp directory. | **Cupcake §8 rule 7** — never mixed with the hermetic suite. |

**Layout on disk.** Products under `src/`; the two test projects at the repository root; solution folders
`Solution Items` (holding `Directory.Packages.props` and `NuGet.config`) and `Tests`
(**Cupcake §8 rule 3**). **One** `Directory.Packages.props` at the root with `ManagePackageVersionsCentrally`
on and no `Version` attribute on any `PackageReference`, grouped under `<!-- Application Versions -->` and
`<!-- Unit Test Versions -->` (**Cupcake §8 rules 31–33**; Cupcake's two duplicated copies are a hazard, not a
feature). `NuGet.config` `<clear />`s inherited sources and gives **every** declared source a
`packageSourceMapping` entry (**Cupcake §8 rule 35** — Cupcake's `github` source has none and is dead).

**Single TFM across the whole graph** (**Cupcake §8 rule 5**; Cupcake's Release-only downgrade of one project to
`net6.0-windows` while its references stay `net8.0` is the anti-pattern). Self-contained distribution is a
`Release`-only `PropertyGroup`, not a second codebase (**Cupcake §8 rule 6**).
*fixes:* the source shipped two hand-rolled build configurations (`Compact` AOT and `SingleFile` JIT) declared
**identically and separately in both shell csproj files**, with contradictory claims about size and about which
one is publishable. One conditional `PropertyGroup`, defined once in `Directory.Build.props`, replaces both.

**What is deliberately *not* a project.** There is no `Xcaciv.ChatDbg.Core` holding domain services. Every service
the source product had — settings, history, prompts, credentials, backends, token inspection, formatting,
logging — is inside the tool package that owns it. A capability that is not reachable as a tool does not exist.
*fixes:* the source's `Core` library grew to hold 18 command classes of which **three were never registered by
either shell** (`ExportLogsCommand`, `ExportTokenAnalysisCommand`, `ShowTokenAnalysisCommand`) and were reachable
only from tests. Under this pattern a class carrying `[CommandRegister]` in a loaded package is registered by
construction; there is no separate wiring step to forget.

---

### A.2 The two executables

Both are composition roots of ~20 lines. **They share everything except the `IIoContext` implementation and the
gesture that raises cancellation.**

| | `Xcaciv.ChatDbg.Cli` | `Xcaciv.ChatDbg.Screen` |
|---|---|---|
| Front end | line-oriented terminal, `Console.ReadLine` | full-screen terminal (alternate screen buffer, panes, scrollback view) |
| IO context | `ConsoleIoContext` | `ScreenIoContext` |
| Non-interactive fallback | `BatchIoContext` when `Console.IsInputRedirected` or `--batch` | `BatchIoContext`; the full-screen front end **refuses** to start on a redirected or non-tty stdin and exits 3 |
| Cancel gesture | `Console.CancelKeyPress` with `e.Cancel = true` | a bound key plus an on-screen **Stop** affordance |
| Everything else | identical | identical |

**Both drive the same tool set through the same `ICommandController`.** Concretely:

* `Xcaciv.ChatDbg.Host.HostStartup.Build(HostOptions)` is the **only** place a controller is constructed, built-ins
  are registered, package directories are added, and packages are loaded. Neither `Program.cs` may call
  `RegisterBuiltInCommands`, `AddPackageDirectory`, `LoadCommands`, or `AddCommand` directly.
* `Loop.Run(IIoContext, ICommandController, IControllerEnvironmentContext, CancellationToken)` takes its three
  collaborators as **parameters** and assigns them to properties — it never constructs one
  (**Cupcake §8 rule 10**; this single decision is what makes the loop testable).
* There is exactly **one** async loop body. Cupcake's `Run`/`RunAsync` diverge in four behaviours beyond awaiting
  — the async path skips built-in registration, treats "no plugins" as fatal, emits an extra status message, and
  wraps the body in `Task.Run`. **That divergence is not reproduced** (**Cupcake §8 rule 16**): there is one
  `RunAsync`, and `Run` is a thin `.GetAwaiter().GetResult()` shim for the synchronous single-file build.
* `RegisterBuiltInCommands()` is called in exactly one place (**Cupcake §8 rule 13**; Cupcake calls it twice on the
  `RunWithDefaults` path).

*fixes:* **the source product's divergent double command surface.** The two shells each ran their own
`InitializeCommands()` and their own dispatcher; the GUI shell additionally carried a **671-line dead near-clone**
of the console `ChatShell` that nothing instantiated, registering a *different* provider set. Worse, the GUI built
every settings-bearing command against a default `ChatSettings` object and then reassigned the variable to the
loaded settings — so `/set` reported built-in defaults instead of the user's file, `/model` persisted a change the
window never honoured, and `/set provider llama` did not change the provider the chat turn used, **while the same
commands worked correctly in the console shell**. A single controller, a single environment context and a single
registration path make that class of defect unrepresentable: there is no second object to diverge from.

**Feature switches that are never read do not exist.** Cupcake declares `EnableInstallCommand` and never reads it
(`Loop.cs:11`, asserted only by a defaults test). Every `Loop` property in this host is consumed on a live path or
is deleted.

---

### A.3 Startup sequence

`HostStartup.Build` executes these steps in this order. Each step names its failure behaviour. Steps 1–3 run
before any status output is possible on the full-screen front end, so their failures are written to stderr and
exit before the alternate screen is entered.

| # | Step | On failure |
|---|---|---|
| 0 | **Announce.** `await io.SetStatusMessage("Loading tools")` **before** doing any work (**Cupcake §8 rule 12**). | — |
| 1 | **Resolve paths.** Compute `CHATDBG_DATA_ROOT` (`--data-root`, then `CHATDBG_DATA_ROOT`, then the OS user-config directory, one root for *all* artefacts), then derive `CHATDBG_CONFIG_PATH`, `CHATDBG_PROMPTS_DIR`, `CHATDBG_LOG_DIR`, `CHATDBG_TRANSCRIPT_DIR`, `CHATDBG_TRUST_STORE`. Create the root if absent. | Cannot create or write the root → stderr `chatdbg: data root '<path>' is not writable`, **exit 3**. Never silently fall back to a temp directory. *fixes:* the source used **three different special folders** for three artefacts of the same app (`UserProfile/.ChatDbg`, `LocalApplicationData/ChatDbg`, `ApplicationData/ChatDbg/Logs`) — which resolve to three different XDG directories on Linux — and silently fell back to `Path.GetTempPath()` when the profile folder was unavailable. |
| 2 | **Load configuration.** Read the config document, apply the named profile (`--profile`, else `CHATDBG_PROFILE`, else `default`), then overlay any `--set key=value` switches. Validate every value against the seeded-key table (§C.4). | Unreadable/malformed → stderr with the file path and the parse position, **exit 3**. A single out-of-range value → the value is rejected, the seeded default is used, and a warning is queued for the first prompt; startup continues. **Never write a default config file as a side effect of reading one** — writing is `CONFIG INIT`'s job. *fixes:* `SettingsService.LoadSettingsAsync` wrote a default `settings.json` to disk merely because none existed, and `SystemPromptService`'s **constructor** created a directory and wrote four prompt files; merely constructing a service mutated the user's disk. |
| 3 | **Resolve credentials.** Ask the secret store, in the fixed order *process environment → OS credential store → configuration file (deprecated)*, for each backend's secret **name only** — the host records *which source answered*, never the value. Values stay inside the Credentials package. | No credential for the selected backend → **not fatal**. The host records `CHATDBG_BACKEND_READY=false` and the first prompt carries `no credential for backend '<x>' — run \`cred set <x>\``. A credential found in the deprecated configuration file → a migration warning naming `cred migrate`. Store unreachable (no OS keychain) → warning, fall through to the next source. |
| 4 | **Select the backend.** Resolve `CHATDBG_BACKEND` to a registered `IModelBackendFactory`. Backends are tools; selection is by name, not by `switch`. | Unknown backend name → warning listing the registered names, `CHATDBG_BACKEND_READY=false`, continue to the prompt. A backend whose native payload will not load (missing llama.cpp for the RID) → the backend deregisters itself, the failure text names the missing RID, and the shell continues with no active backend. **A missing backend is never fatal**, by the same reasoning that makes a plugin-less shell valid. |
| 5 | **Register built-ins.** `controller.RegisterBuiltInCommands()` — one call, one place. Installs `SAY`, `SET` (`modifiesEnvironment: true`), `ENV`, `REGIF` under package key `"Default"`. | Cannot fail. |
| 6 | **Register in-box tools.** For each in-box package, `controller.AddCommand("inbox", typeof(T), modifiesEnvironment)` — **by `Type`, not by instance**. The framework discards the instance you pass to the `ICommandDelegate` overload and constructs a fresh one per execution, so a pre-configured instance is a silent bug. | Cannot fail. A type without `[CommandRegister]` is **traced and silently skipped** by the registry — the host therefore asserts, at startup, that every in-box type it registered is present in the registry afterwards, and exits 2 if one is missing. |
| 7 | **Trust gate the package root** (§E steps 1–5): canonicalize the root, refuse a writable root, load the hash allowlist, verify every candidate DLL, read and check each package manifest, compute a `TrustLevel`. | Missing root → skip to step 9 with zero packages. Unwritable-check failure or corrupt/absent trust store → **exit 4** (`chatdbg: trust store … refusing to load tool packages`). A trust store must never degrade to "allow everything". Individual package failures are per-package, not fatal (§E table). |
| 8 | **Register and load packages.** `controller.AddPackageDirectory(root)` for each *verified* root, then `controller.LoadCommands()` (default `bin` sub-directory). Then re-register, by `Type`, the commands that a **Trusted** manifest declares environment-modifying (§C.6). | `NoPluginsFoundException` is caught specifically, converted to the user-facing line `No tool packages found. Try \`pkg --help\`.`, and **execution continues into the prompt** — a shell with zero packages is a valid shell (**Cupcake §8 rule 38**, applied on *every* run path, unlike Cupcake). Any other exception is wrapped in `LoadingException("Unable to load tools.", ex)` and is fatal, **exit 2** (**Cupcake §8 rule 37**). `AddPackageDirectory` **returns nothing and silently ignores a directory that fails verification** — so the host verifies the directory itself first and logs which roots were accepted. |
| 9 | **Seed the environment.** `controller.GetEnvironment()` returns an `IControllerEnvironmentContext` pre-seeded with every registered command's `GetDefaultEnvironment()`, bucketed per command. The host then writes the global seeded keys of §C.4 onto it and records their values in an immutable snapshot for re-assertion (§C.5). | A tool's declared default that collides with a reserved `CHATDBG_` key is rejected with a warning naming the tool; the host's value wins. |
| 10 | **Wire host-level policy.** `controller.AuditLogger = new ChatDbgAuditLogger(...)`; `controller.OutputEncoder = <front-end encoder>`; `controller.PipelineConfig = new PipelineConfiguration { MaxChannelQueueSize = 10_000, BackpressureMode = Block, StageTimeoutSeconds = 0 }`. **All three must be assigned explicitly**: a DI container registers them but nothing pushes them onto the controller, which initialises its own `NoOpAuditLogger`/`NoOpEncoder`. | Cannot fail. |
| 10a | **Capability reconciliation (owner decision D-001, `DECISIONS.md`).** If `TOKEN_ENABLED` is `true` and the selected backend's declared capability record cannot supply token log probabilities, set `TOKEN_ENABLED=false`, persist, and queue the auto-disable notice for the first prompt: `Token log probabilities disabled: provider '{provider}' does not support them. Use a provider that does (see MODEL CAPS).` The full-screen front end additionally marks its log-probability controls disabled-with-explanation (§B). A backend that *declares* the capability is left alone — transient per-response absence is GR-18's business, not startup's. | Cannot fail; worst case the notice is queued and the setting is already off. |
| 11 | **First prompt.** `await io.SetStatusMessage("Ready")`, flush any queued warnings from steps 2–4 and 10a as ordinary output, then enter the loop. | — |

---

### A.4 Shutdown sequence

1. The loop exits (exit command, EOF, or a fatal escape).
2. `await io.SetStatusMessage("Shutting down")`.
3. **Dispose the tools that own native resources or open model files.** This is the step the framework does not do
   for you: `CommandExecutor` `await using`s the *child environment* but **never disposes the command instance it
   created**, and `CommandRegistry` disposes only the throwaway probe instance it builds to read
   `GetDefaultEnvironment()`. Therefore:
   * A tool must not hold an unmanaged resource across executions that it cannot also release inside `Main`.
   * A resource that genuinely must outlive a single execution — a loaded GGUF weights file, a llama.cpp context,
     an HTTP connection pool — is owned by a **host-registered singleton** exposed through
     `Xcaciv.ChatDbg.Abstractions` (`IModelBackend : IAsyncDisposable`), not by the tool object. The host owns the
     registry of live backends and disposes them here, in reverse registration order, each inside its own
     `try`/`catch` so one hung native finalizer cannot block the rest.
   * Disposal is bounded: 5 s per backend, then the host logs `backend '<x>' did not shut down cleanly` and moves on.
   *fixes:* the GUI shell **never disposed its AI services at all** — `Program.cs` had zero `Dispose` calls, and the
   native LLamaSharp model and context leaked until process exit; the console shell disposed them, so the two
   shells leaked differently.
4. Flush the audit log writer, then the diagnostic log writer.
5. Persist nothing implicitly. Configuration, history and prompts are written by their tools when the user asks;
   shutdown does not write files the user did not request. (An explicitly enabled `CHATDBG_AUTOSAVE_TRANSCRIPT`
   is executed as a final `SESSION EXPORT` dispatch through the normal path, so it is audited like any other.)
6. `await io.Complete(null)` then `await io.DisposeAsync()` — closing pipes and, on the full-screen front end,
   leaving the alternate screen buffer and restoring the terminal in a `finally`.
7. Return the exit code.

---

### A.5 The read–eval–print loop

**Shape** — seed-empty, exit-checked at the top, execute-then-prompt (**Cupcake §8 rule 14**):

```
var line = "";
while (!ExitCommands.Contains(line, StringComparer.OrdinalIgnoreCase))
{
    if (!string.IsNullOrWhiteSpace(line)) await DispatchGuarded(line, io, controller, env);
    line = await io.PromptForCommand(ComposePrompt());
}
```

Blank input is skipped, never dispatched. An exit command is read and then terminates on the next condition
check — it is never dispatched to the controller.

**Prompt composition.** `ComposePrompt()` renders, in order: the active profile when it is not `default`, the
active backend, the active model, and a `*` when the session has unsaved history — then the prompt glyph. It is
built from the *environment context*, never from a field the host caches, so it cannot drift from what the tools
see. The glyph and the whole template are public settable properties on `Loop` with defaults, alongside
`ExitCommands` and `PackageDirectory` (**Cupcake §8 rule 8**): personality is configuration, not loop body.
Default exit set `{ "END", "EXIT", "QUIT", "BYEE" }`, compared `OrdinalIgnoreCase` (**Cupcake §8 rule 9**, plus
`QUIT` because the source product had both `/exit` and `/quit`).

**Line classification.** In order:

1. **Empty / whitespace** → skipped.
2. **Exit command** (whole line, case-insensitive) → loop terminates.
3. **Verbatim tail.** If the line contains the sentinel ` -- `, everything after the first sentinel is *not*
   tokenized. The head is dispatched as a command line; the tail is injected as a single chunk on that command's
   input pipe (see 5). The receiving parameter is declared `UsePipe = true`.
4. **Command line.** The first token, normalized by `NamesValidator.GetValidCommandName`, resolves against the
   registry — or the line contains an unquoted `|`, making it a pipeline. Dispatched with
   `controller.Run(line, io, env, ct)`.
5. **Anything else is a chat turn.** The host creates a bounded single-item `Channel<IResult<string>>`, writes
   `CommandResult<string>.Success(rawLine)`, completes the writer, calls `SetInputPipe(reader)` on a child IO
   context, and dispatches `CHAT`. `CHAT` receives the user's text **byte-for-byte in `HandlePipedChunk`**.
   *fixes:* two things at once. The source classified on a leading `/` and then re-parsed arguments with ad-hoc
   `string.Split` inside each command. And the framework's own argument tokenizer would destroy free text:
   `NamesValidator.GetArgumentsFromCommandline` matches unquoted tokens as `[\w-]+` and strips `\ / : = , ; < > { } +`
   **even inside quotes**, so `C:\dir\file.txt` arrives as four tokens. No user prose, path, URL, or regex may ever
   be passed as a command-line parameter in this product; the raw channel and the verbatim tail are the only
   supported routes, and every tool that accepts such a value declares its parameter `UsePipe = true`.

**Guarded dispatch.** `DispatchGuarded` wraps `controller.Run` in its own `try`/`catch`. A command that throws
reports and returns to the prompt; it never kills the shell. **This is the one place the pattern is knowingly
deviated from** — Cupcake's per-command dispatch is unguarded and a single bad command exits the process
(**Cupcake §8 rule 41** names this as the pattern's main defect). The catch unwraps `AggregateException` and the
`InnerException` chain and prints type + message, not `ex.Message` alone (**Cupcake §8 rule 39**).

**Cancellation of a long generation.** See §D.7. In one line: the host owns a `CancellationTokenSource` per
dispatch, the front end's cancel gesture cancels it, the token is passed to `controller.Run(…, ct)`, and the IO
context also exposes it to the running tool through `ICancellationSignal` because `ICommandDelegate.Main` has no
token parameter.

---

### A.6 Exit codes

| Code | Meaning | Raised by |
|---|---|---|
| `0` | Normal termination — the user typed an exit command, or stdin reached EOF in non-interactive mode. | falling off the end of `Program.cs` |
| `1` | Unhandled exception escaped the loop. Message printed as `type: message` with the inner chain. | the single top-level `try` in `Program.cs` (**Cupcake §8 rule 39**) |
| `2` | Startup tool-loading failure — a `LoadingException` from step 8. | `HostStartup` |
| `3` | Configuration or front-end failure — unreadable config, unwritable data root, full-screen shell on a non-tty. | `HostStartup` |
| `4` | Package trust failure — missing/corrupt trust store, or a package root that failed the lock-down check. | `PackageTrustGate` |

Cupcake defines only `0` and `1` (**Cupcake §8 rule 40**). Codes `2`–`4` are a **deliberate extension**: this host
is scriptable and CI must distinguish "your config is wrong" from "a tool crashed" from "we refused to load code".
No other codes exist; in particular there is no distinct code for a failing *command*, which is data, not an exit
status (§D.2).

---

## B. The I/O context

### B.1 What the host must implement

Exactly **one** base class, three subclasses. Each derives from **`Xcaciv.Command.Core.AbstractTextIo`**
(primary constructor `(string name, string[] parameters, Guid? parentId = default)`) and overrides only the
front-end-specific members (**Cupcake §8 rule 18**):

| Member | Signature (3.3.4) | Obligation |
|---|---|---|
| `GetChild` | `Task<IIoContext> GetChild()` | Construct a child, propagate pipes. **Takes no arguments in 3.3.4** — Cupcake's `GetChild(string[]?)` is the 2.1.x shape. |
| `HandleOutputChunk` | `Task HandleOutputChunk(IResult<string> result)` | The terminal endpoint for one output chunk. **Takes `IResult<string>`, not `string`.** |
| `PromptForCommand` | `Task<string> PromptForCommand(string prompt)` | Interactive input. Must return `?? string.Empty`, never null, so EOF degrades to a blank line the loop skips (**Cupcake §8 rule 25**). |
| `SetProgress` | `Task<int> SetProgress(int total, int step)` | Progress. **Guard `total == 0`** and return a real percentage; the contract says 0–100. |
| `SetStatusMessage` | `Task SetStatusMessage(string message)` | Transient status; replaces the previous status. **Not** command output. |
| `SetOutputEncoder` | `void SetOutputEncoder(IOutputEncoder encoder)` | **Must be overridden.** The base implementation is an empty body, and *no production framework path ever calls `IOutputEncoder.Encode`*. Encoding is entirely the host's job: store the encoder here and apply it in `HandleOutputChunk`, optionally branching on `result.OutputFormat`. |

**What must *not* be overridden:** `OutputChunk`. Tools call `OutputChunk`; the base class decides whether that
goes down a pipe or to `HandleOutputChunk`. The host implements the terminal endpoint only; the framework owns
pipe routing (**Cupcake §8 rule 18**).

**Inherited free** and never reimplemented: `Id`, `Name`, `Parent`, `HasPipedInput`, `Parameters`/`SetParameters`,
`PipelineStage`/`PipelineTotalStages`/`SetPipelineStage`, `SetInputPipe`/`SetOutputPipe`, `ReadInputPipeChunks`,
`Complete`, `DisposeAsync`, `SetTraceLog`, `AddTraceMessage`.

**Constructor shape.** A primary constructor `(string name = "…", string[]? parameters = default, Guid? parentId = default, bool verbose = false)`
forwarding `name`, `[.. parameters ?? []]`, `parentId` to the base (**Cupcake §8 rule 19**). The null-coalesce is
mandatory: spreading a null array throws, and Cupcake's `GetChild` can reach that path
(**Cupcake §8 rule 24**). `Verbose` is set on the **base** property — never shadowed with `public new`, which is
what Cupcake does and which silently leaves the base value unset for `AddTraceMessage` (**Cupcake §8 rule 22**).

### B.2 Child contexts and pipe propagation

`GetChild()` does exactly four things (**Cupcake §8 rule 20**):

1. Construct `new <Self>(Name + "Child", Parameters, Id)` — **derivational naming**, so a nesting chain reads as
   `ConsoleIoChildChild` in status output, and **explicit parentage**, giving the framework a traceable tree.
2. Propagate the input pipe **only when `HasPipedInput && inputPipe != null`**.
3. Propagate the output pipe on a null check alone.
4. Return via `Task.FromResult<IIoContext>(child)` — the contract is async; the work is not.

Piping is inherited downward and the child never negotiates it. The pipeline executor calls `GetChild()` once per
stage, then `SetParameters(args)`, then `SetPipelineStage(stage, total)`, then wires the bounded channel between
consecutive stages. A child registers itself in the parent's child list so the parent can dispose the tree.

**Piped input reaches a tool one way only:** `await foreach (var chunk in io.ReadInputPipeChunks())`, which under
`AbstractCommand` is driven for you and delivers `HandlePipedChunk` **one surviving chunk at a time** — failures
are yielded straight through without reaching your handler, and empty-output chunks are dropped. **Piped output
leaves a tool one way only:** `yield return` from `Main`; the executor calls `OutputChunk`, which prefers the pipe
when one is set. Because the *final* stage also has an output pipe, the pipeline executor drains the last channel
back to the root context — that drain is what makes pipeline output visible at all.

### B.3 Progress and status

* Status is a **separate visual channel** from command output, with its own styling. Quiet mode does not discard
  status — it **redirects to the diagnostic sink** (**Cupcake §8 rule 22**).
* Progress is routed **through** the status channel via a public `ProgressTemplate` format string, so it inherits
  verbosity gating for free (**Cupcake §8 rule 23**). Unlike Cupcake, the status call is **awaited** (Cupcake
  fire-and-forgets it) and the divisor is guarded (Cupcake's `total / step` divides by zero at `step == 0` and is
  integer division mislabelled as a percentage).
* Three independently settable visual channels — command output, status, prompt — each following
  set-style → write → reset, with the prompt deliberately *not* resetting so the user's typed input stays styled
  (**Cupcake §8 rule 21**).
* `AddTraceMessage` is diagnostics for developers and is never shown at the default verbosity. It is where
  parameter-parse failures land: a missing or invalid required parameter throws `ArgumentException` inside
  `ProcessParameters`, which the executor converts to the generic chunk
  `Error executing {command} (see trace for more info)` — **the specific "Missing required parameter X" text never
  reaches stdout**. Every front end therefore offers a one-key/one-command way to show the last trace, and the
  house help style (§D.5) compensates by making `ValueDescription` carry the requirement.

### B.4 The three front ends

| Capability | Line-oriented (`ConsoleIoContext`) | Full-screen (`ScreenIoContext`) | Non-interactive (`BatchIoContext`) |
|---|---|---|---|
| Command output (`HandleOutputChunk`) | styled write to stdout, one chunk per line, colours reset after each write | appended to the transcript pane; the pane auto-scrolls unless the user has scrolled back | plain write to stdout, no styling, no cursor control; encoder set from `CHATDBG_OUTPUT_FORMAT` |
| Failure chunks | `!` prefix in the error style, `ErrorMessage` then the correlation id | modal-free: an inline error row in the transcript, plus a red status line | written to **stderr**, prefixed `error:`, so stdout stays machine-parseable |
| Status (`SetStatusMessage`) | one styled line on stdout, gated by `Verbose` | a dedicated status bar; last message wins; never enters the transcript | **suppressed** at default verbosity; to the diagnostic sink; to stderr when `-verbose` |
| Progress (`SetProgress`) | a single rewritten line via `\r` when stdout is a tty, else one line per 10 % | a progress widget in the status bar | suppressed entirely; the return value is still computed and honest |
| Prompt (`PromptForCommand`) | styled `Console.Write`, then `Console.ReadLine() ?? ""` | an input pane with history and completion drawn from `HELP`'s command list | **reads the next line of stdin**; at EOF returns an exit command so the loop terminates with 0 |
| Secret entry | echo suppressed, no history entry, buffer zeroed | masked field, no history entry, buffer zeroed | **refused** — returns a failure telling the user to use an environment variable or the OS store |
| Destructive confirmation | typed confirmation via `PromptForCommand` | typed confirmation in a confirm pane | **refused unless `-force` was passed** (§D.4) |
| Trace (`AddTraceMessage`) | to the diagnostic sink; echoed only when `-verbose` | to the diagnostic sink and a collapsible pane | to the diagnostic sink only |
| Cancellation gesture | `Console.CancelKeyPress`, `e.Cancel = true` | a bound key and a Stop affordance | `SIGINT`/`SIGTERM` → cancel, then exit 130-free clean shutdown (exit 0 if a turn completed, 1 if it was interrupted mid-write) |
| Child contexts | full support | full support | full support |
| Output encoder | identity by default | identity by default | `JSON` encoder when `CHATDBG_OUTPUT_FORMAT=json`; each chunk emitted as one JSON object with `output`, `format`, `correlationId`, `success` |
| Terminal restoration | none needed | alternate buffer entered on start, restored in `finally` on **every** exit path including exit 1 | none needed |

**Capability-gated controls (owner decision D-001, `DECISIONS.md`).** While the active backend's declared capability record cannot supply token log probabilities: the full-screen front end renders its log-probability enable controls (the Settings toggle and the View-menu capture toggle) **disabled with an explanation** — the control stays focusable and its explanation reads `Cannot enable token log probabilities: provider '{provider}' does not support them. Switch providers first (see MODEL CAPS).`; attempts to open the probability panel for a message with no probability data produce a **transient status-bar notice** (`No token probability data for this message: provider '{provider}' does not supply it.`) — **never a modal, never a blocked interaction**. The line-oriented and non-interactive front ends, which have no stateful controls to disable, apply the same rule as a command refusal (`TOKEN CAPTURE on` / the legacy enable forms fail with the same text) plus a one-line advisory on view attempts. Gating state is re-evaluated on every backend switch and at startup (§A.3 step 10a); no front end ever infers capability itself — all three read the same declared record `MODEL CAPS` reports.

**The contract is satisfied identically by all three.** No tool may branch on which front end it is running under;
a tool that needs to know whether it can prompt asks `io.HasPipedInput` (the contract says `PromptForCommand` is
meaningful only when it is false) or reads the read-only `CHATDBG_FRONTEND` key.
*fixes:* the source product's `Core` library called `Console.*` directly in **nine files and ~119 call sites**,
including interactive `Console.ReadLine()` dialogues inside `SettingsService.EnableWindowsCredentialManagerAsync`
and `MigrateCredentialsFromJsonAsync` — which, under a full-screen shell that owns the terminal, hang or corrupt
the screen. Its `IConsoleFormatter` abstraction existed but was bypassed, and leaked a third-party markup syntax
as its wire format. Here, **a tool that writes to `Console` directly is a defect**, and the only sanctioned output
paths are `OutputChunk`, `SetStatusMessage`, `SetProgress` and `AddTraceMessage`.

---

## C. The environment context

### C.1 The namespace and its two halves

The framework gives the host a two-store `IControllerEnvironmentContext`: a **global** store and a
**per-command** store, and hands each executing tool an isolated `IEnvironmentContext` child.

* **Global keys are `CHATDBG_<AREA>_<NAME>`**, screaming snake case, ASCII, no dots or colons. All comparisons are
  case-insensitive; keys are *not* upper-cased by the framework despite what its own doc comment claims, so the
  host writes them upper-cased and every tool reads them upper-cased.
* **Per-tool keys are `<COMMAND>_<NAME>`** — this prefix is **applied by the framework, not by you**. A tool that
  returns `("TIMEOUT","30")` from `GetDefaultEnvironment()` must read **`MYCMD_TIMEOUT`** inside `Main`, because
  `ControllerEnvironmentContext.GetChild(commandName)` copies the tool's bucket in with the prefix applied.
* **The `CHATDBG_` prefix is reserved and must not collide with any registered command name.** This is not
  cosmetic: `ControllerEnvironmentContext.UpdateEnvironment(dictionary)` **filters out every key prefixed with a
  known command name** before merging into globals, precisely to stop one tool smuggling another's namespace into
  the global store. Because no command is named `CHATDBG`, host globals survive that filter intact.

### C.2 Child isolation

`GetChild()` **copies the current values into a brand-new context**. That copy is the isolation boundary: the
child starts from a snapshot and its writes never reach the parent object. There are **two nested children per
execution** — one taken by `CommandController.Run`, one by `CommandExecutor` — and each merges back only under the
rule in §C.6. In a pipeline, every stage gets its own child keyed by its command name.

**The `storeDefault` trap.** `GetValue(key, defaultValue = "", storeDefault = true)` **writes the default back on a
miss and flips `HasChanged`**. Merely *reading* a variable a tool never set therefore makes the environment look
dirty and triggers a write-back. House rule: **every tool read passes `storeDefault: false`** unless it genuinely
intends to persist the default.

### C.3 Read-only keys and how they are enforced

The framework has no read-only concept: the built-in `SET` is registered `modifiesEnvironment: true` and can
overwrite anything global. The host therefore enforces immutability itself:

1. `HostStartup` records an immutable snapshot of the keys marked **RO** in §C.4.
2. After **every** dispatch returns, `Loop` compares the live global store against that snapshot. Any RO key whose
   value changed is **restored**, an audit record of type `EnvironmentTamper` is written naming the command that
   was running, and the user sees `'<KEY>' is read-only and was restored`.
3. RW keys are not touched. This is a repair, not a lock: it costs one dictionary comparison per prompt and it
   cannot be bypassed by a tool that writes globals through `SET`.

### C.4 Seeded keys

Seeded by the host at step 9 of startup. `RO` = read-only to tools (host-owned, restored per §C.3).
`CFG` = writable through the `CONFIG` tool, which is the only in-box tool registered
`modifiesEnvironment: true` besides the framework's `SET`. `TOOL` = writable by the owning tool.

**Identity and paths**

| Key | Type | Default | Rights |
|---|---|---|---|
| `CHATDBG_HOST_VERSION` | string | assembly informational version | RO |
| `CHATDBG_FRONTEND` | enum `line` \| `screen` \| `batch` | set by the executable | RO |
| `CHATDBG_SESSION_ID` | GUID string | new per process | RO |
| `CHATDBG_SESSION_STARTED` | ISO-8601 UTC | process start | RO |
| `CHATDBG_PROFILE` | string | `default` | CFG |
| `CHATDBG_DATA_ROOT` | absolute path | OS user-config dir + `/chatdbg` | RO |
| `CHATDBG_CONFIG_PATH` | absolute path | `<DATA_ROOT>/config.json` | RO |
| `CHATDBG_PROMPTS_DIR` | absolute path | `<DATA_ROOT>/prompts` | RO |
| `CHATDBG_TRANSCRIPT_DIR` | absolute path | `<DATA_ROOT>/transcripts` | CFG |
| `CHATDBG_LOG_DIR` | absolute path | `<DATA_ROOT>/logs` | CFG |
| `CHATDBG_PACKAGE_DIR` | absolute path | `<AppContext.BaseDirectory>/tools` | RO |
| `CHATDBG_USER_PACKAGE_DIR` | absolute path | `<DATA_ROOT>/tools` | RO |
| `CHATDBG_TRUST_STORE` | absolute path | `<DATA_ROOT>/trusted-tools.csv` | RO |

*One root, derived paths.* fixes: three unrelated special folders in the source.

**Active backend and model**

| Key | Type | Default | Rights |
|---|---|---|---|
| `CHATDBG_BACKEND` | enum, allow-list built from registered backends (`azure`, `bedrock`, `local`) | `azure` | CFG |
| `CHATDBG_BACKEND_READY` | bool | computed at startup | RO |
| `CHATDBG_MODEL_ID` | string | `gpt-4` | CFG |
| `CHATDBG_MODEL_PATH` | absolute path to a local weights file | *(empty)* | CFG |
| `CHATDBG_AZURE_ENDPOINT` | absolute https URI | *(empty)* | CFG |
| `CHATDBG_AWS_REGION` | string | `us-east-1` | CFG |
| `CHATDBG_CRED_SOURCE` | enum `auto` \| `env` \| `os` \| `file` \| `none` | `auto` | CFG |
| `CHATDBG_CRED_STATUS` | string, e.g. `azure:env, bedrock:none` — **source names only, never values** | computed | RO |

*The credential *source* is environment data; the credential *value* never is.* fixes: the source resolved secrets
inside property getters on a serialization DTO, P/Invoking into the OS credential store from
`ChatSettings.AzureApiKey` — reading a "field" was a side-effecting call, and the deprecated plaintext backing
fields were serialized into `settings.json` under `azureApiKey`/`awsAccessKey`/`awsSecretKey`. **No key in this
table ever holds a secret**, and `CHATDBG_CRED_SOURCE=file` is refused with a migration message rather than
supported.

**Sampling**

| Key | Type | Default | Rights |
|---|---|---|---|
| `CHATDBG_TEMPERATURE` | double, `0.0`–`2.0` | `0.7` | CFG |
| `CHATDBG_MAX_TOKENS` | int, `1`–`131072` | `1000` | CFG |
| `CHATDBG_LOCAL_CONTEXT_SIZE` | int, `256`–`131072` | `4096` | CFG |
| `CHATDBG_LOCAL_GPU_LAYERS` | int, `0`–`999` | `0` | CFG |
| `CHATDBG_LOCAL_GPU_DEVICE` | string | *(empty)* | CFG |
| `CHATDBG_LOCAL_THREADS` | int, `0` = system default | `0` | CFG |
| `CHATDBG_LOCAL_BATCH_SIZE` | int, `1`–`4096` | `512` | CFG |

**Introspection**

| Key | Type | Default | Rights |
|---|---|---|---|
| `CHATDBG_LOGPROBS_ENABLED` | bool | `false` | CFG |
| `CHATDBG_LOGPROBS_TOPK` | int, `1`–`20` | `5` | CFG |
| `CHATDBG_TOKEN_SHOW_ALL` | bool | `false` | CFG |
| `CHATDBG_TOKEN_ANALYSIS_DIR` | absolute path | `<DATA_ROOT>/analysis` | CFG |

**Presentation**

| Key | Type | Default | Rights |
|---|---|---|---|
| `CHATDBG_THEME` | enum `dark` \| `light` \| `none` | `dark` | CFG |
| `CHATDBG_OUTPUT_FORMAT` | enum `text` \| `json` \| `csv` \| `yaml` — maps to `ResultFormat` | `text` | CFG |
| `CHATDBG_GRID_VIEW` | bool | `false` | CFG |
| `CHATDBG_GRID_MAX_ALTERNATIVES` | int, `1`–`10` | `5` | CFG |
| `CHATDBG_VERBOSE` | bool | `false` | CFG |
| `CHATDBG_PROMPT_TEMPLATE` | string | `{profile}{backend}:{model}{dirty}> ` | CFG |

**Prompts and session**

| Key | Type | Default | Rights |
|---|---|---|---|
| `CHATDBG_SYSTEM_PROMPT_NAME` | string, allow-list built from the prompt library | `default` | CFG |
| `CHATDBG_HISTORY_LIMIT` | int, `0` = unbounded | `0` | CFG |
| `CHATDBG_AUTOSAVE_TRANSCRIPT` | bool | `false` | CFG |

**Packages and audit**

| Key | Type | Default | Rights |
|---|---|---|---|
| `CHATDBG_ALLOW_USER_PACKAGES` | bool | `false` | RO (set only from the command line or config, never at runtime) |
| `CHATDBG_AUDIT_PATH` | absolute path | `<LOG_DIR>/audit.jsonl` | RO |
| `CHATDBG_AUDIT_ENABLED` | bool | `true` | RO |

**Note on the system prompt body.** `CHATDBG_SYSTEM_PROMPT_NAME` is environment data; **the prompt text is not**.
The body is loaded by the System Prompt Library tool and handed to the backend through
`Xcaciv.ChatDbg.Abstractions`. Putting multi-line prose into the environment would put it into every `ENV` dump
and every audit record.

### C.5 Per-tool defaults

A tool declares what it consumes with `GetDefaultEnvironment()`, returning unprefixed names and defaults. At
startup `CommandRegistry.GetEnvironment` instantiates every registered command once, collects those dictionaries,
and buckets them by command name — then disposes each probe instance. Two consequences a tool author must know:
**your constructor runs at startup, on an instance that is thrown away**, so it must be cheap and side-effect
free; and **inside `Main` you read the prefixed key**.

### C.6 How a tool signals it needs environment-modifying rights

`ModifiesEnvironment` is a property of the **registration**, not of the class — **there is no attribute for it**.
A tool without it can still persist state, but **only** under keys it prefixed with its own command name, which
land in its private bucket and survive across invocations. A tool with it writes to the global store.

The host's grant path:

1. Every tool package ships a manifest, `package.json`, alongside its `bin` directory, listing
   `modifiesEnvironment: ["CONFIG", "PROFILE"]` — command names, not types.
2. In-box packages are registered with `AddCommand(packageKey, type, modifiesEnvironment: true)` for exactly those
   names.
3. Dynamically loaded packages are registered by `LoadCommands()`, which **cannot** carry the flag. After
   `LoadCommands()` the host walks the registry and **re-registers by `Type`** only those commands that (a) are
   named in the manifest's `modifiesEnvironment` list, and (b) come from a package at trust level **Trusted**
   (§E.3). A **Sandboxed** package's request is refused with a warning; the package still loads, its commands
   still run, and its writes still persist in its own bucket.
4. A tool that needs a global write it was not granted returns a failure naming the key and the required trust
   level. It must never attempt to reach globals through the built-in `SET`.

---

## D. Cross-cutting conventions

### D.1 Result formats

Every tool yields `IResult<string>` chunks — `CommandResult<string>.Success(output, format)` or
`CommandResult<string>.Failure(message, exception)`. `ResultFormat` is `General`, `Object`, `CSV`, `TDL`, `YAML`,
`JSON`.

**`OutputFormat` is metadata that rides along with the chunk. No framework code branches on it.** The shipped
`SetOutputEncoder` is an empty body and `IOutputEncoder.Encode` is never called on a production path. Rendering is
therefore the host's job (§B.1), and a declared format is a *promise to the front end and to the next pipeline
stage*, not an instruction to the framework.

**Declare a structured format when, and only when, all four hold:**

1. The output is a **set of records with a stable shape**, not prose. A token table, a probability map, a package
   list, a settings dump, a history listing.
2. A downstream stage or a script would plausibly consume it. If the only consumer is a human reading one line,
   `General` is correct.
3. The tool can emit it **deterministically** — stable field order, stable field names, no locale-dependent
   number or date formatting (ISO-8601 UTC, invariant culture).
4. The tool can also render `General` for the same data, because `CHATDBG_OUTPUT_FORMAT=text` is the default and
   the full-screen front end renders text.

Assign it once, from the constructor, via `OutputFormat` (`protected set`), and pass `this.OutputFormat` to every
`Success(...)` you build. **`Failure` takes no format** — a failure always carries `General`.

House assignments: `TOKEN INSPECT`, `TOKEN MAP`, `SESSION LIST`, `PKG LIST`, `CONFIG LIST`, `CRED STATUS`,
`PROMPT LIST` → `JSON` when `CHATDBG_OUTPUT_FORMAT=json`, else `General`. `CHAT` and `SAY` → always `General`;
model output is prose. Export tools write files and return a one-line `General` confirmation naming the path —
they never return the file body.

### D.2 Error conventions

**A failed result is data, not an exception.** A failing tool yields
`CommandResult<string>.Failure(message, exception)`; the executor records it, re-wraps it, emits it, and writes
`exception.ToString()` to the trace. A chunk carries `IsSuccess`, `ErrorMessage`, `Exception`, `CorrelationId`,
`Output`, `OutputFormat` — the correlation id is what ties a user-visible error to an audit record and a trace
line, and it is always shown.

**What a failed result must carry:**

* `ErrorMessage`: one sentence, present tense, naming *what* was attempted, *why* it failed and *what to do*, e.g.
  `cannot reach the azure endpoint (403 forbidden) — check \`cred status azure\``. No stack traces, no exception
  type names, no full file paths under the user's home (write `~/…`), **never** a secret, a token, a hash pair, or
  a raw provider payload.
* `Exception`: the real exception, for the trace and the audit record only.
* `CorrelationId`: default (a new GUID) unless the tool is propagating a chunk it received, in which case it
  preserves the incoming id.

**Propagation through a pipeline.** A failure chunk travels to the end of the pipeline and out to the user, and
**downstream stages keep running** — `AbstractCommand.Main` yields a failed chunk straight through without
calling `HandlePipedChunk`. An unknown command yields
`Command [X] not found. Try 'HELP'` and the pipeline still completes. **A pipeline does not abort on a failed
stage**; this is the framework's model and the host does not fight it. What the host adds:

* The front end renders every failure chunk distinctly (§B.4) and, at the end of a dispatch that produced any
  failure, prints a one-line summary `N of M stages reported errors`.
* Exceptions that escape a stage entirely propagate through `Task.WhenAll` and out of the pipeline executor; those
  are caught by `DispatchGuarded` (§A.5) and reported without killing the shell.

**Silent drops the author must know about:** a successful chunk with null or empty `Output` is **silently
dropped** by the executor. That is the sanctioned filter idiom (`REGIF` and `SET` both rely on it) — but it means
"success with nothing to say" produces no output at all, and a tool that meant to say something and produced an
empty string looks like it did nothing.

**Three exception tiers at the host boundary** (**Cupcake §8 rules 37–39**):

| Tier | Condition | Policy |
|---|---|---|
| 1 | Expected absence — `NoPluginsFoundException` | Swallowed, replaced by a message that **names the recovery command**, execution continues to the prompt. On **every** run path. |
| 2 | Unexpected startup failure | Wrapped in `LoadingException("Unable to load tools.", ex)`, inner preserved. Exit 2. |
| 3 | Anything escaping the loop | One top-level `try` in `Program.cs`; print `type: message` plus the inner chain, unwrapping `AggregateException`; exit 1. |

Note that framework exceptions **cannot** be caught with one base type: `NoPluginsFoundException`,
`NoPluginFilesFoundException` and `NoPackageDirectoryFoundException` derive directly from `Exception`, not from
`XcCommandException`.

### D.3 The audit trail

The host assigns `controller.AuditLogger = new ChatDbgAuditLogger(...)` explicitly at startup — a DI registration
does not reach the controller by itself. The logger writes **one JSON object per line** to `CHATDBG_AUDIT_PATH`,
appended, `0600` where the OS supports it.

**What is logged.**

* **One `AuditEvent` per command execution**, emitted by the executor in a `finally` — including once per
  pipeline stage. Fields: `CorrelationId`, `CommandName`, `PackageOrigin` (the package's full path, or
  `built-in`), `Parameters`, `ExecutedAt`, `Duration`, `Success`, `ErrorMessage`, `PipelineStage`,
  `PipelineTotalStages`, `Metadata`.
* **One environment-change record per `SetValue`**, with `variableName`, `oldValue`, `newValue`, `changedBy`
  (hard-coded `"system"` by the framework — the host's logger replaces it with the executing command name from
  the ambient correlation id).
* **Host-originated records** the framework does not produce: `SessionStart`, `SessionEnd`, `PackageVerified`,
  `PackageRejected`, `SecurityViolation`, `HashMismatch`, `TrustGrant`, `EnvironmentTamper`, `Cancelled`.

**What must never reach an audit record.** This list is normative.

*Parameter names* — redacted wholesale, matched case-insensitively on the normalized (lowercased) parameter name:
`apikey`, `api_key`, `api-key`, `key`, `secret`, `secretkey`, `accesskey`, `sessiontoken`, `token`, `password`,
`pwd`, `passphrase`, `credential`, `credentials`, `connectionstring`, `conn`, `bearer`, `authorization`, `auth`,
`signature`, `privatekey`. Plus the wildcard patterns `*password*`, `*secret*`, `*token*`, `*key*`,
`*credential*`, `*passwd*`. Replacement is the literal `[REDACTED]`.

*Environment variable names* — redacted in `oldValue`/`newValue` on the same list, plus anything matching
`*_KEY`, `*_SECRET`, `*_TOKEN`, `*_PASSWORD`, `*_CREDENTIAL`, and the exact names `CHATDBG_AZURE_API_KEY`,
`CHATDBG_AWS_ACCESS_KEY`, `CHATDBG_AWS_SECRET_KEY`, `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`,
`AWS_SESSION_TOKEN`. This path **works**, because it matches on the variable name.

*Output fields* — the audit record **never contains tool output of any kind**. Specifically never: the user's
prompt text, the model's completion text, the system prompt body, tokenizer output, token strings, log-probability
alternatives, transcript or history content, file bodies, provider request/response payloads, or the contents of
an exception's `ToString()` for backend calls (which routinely embeds the request body). Where volume matters,
the record carries **counts and a SHA-256 of the content**, never the content: `promptChars`, `completionChars`,
`tokenCount`, `contentHash`.

*Also never:* an expected-vs-actual hash pair from an integrity failure (log `hashMismatch: true` and the file
path; the pair goes to the security log, not the user-readable audit trail), and absolute paths under the user's
home directory (written `~/…`).

**The framework's own masking is effectively non-functional and must not be relied on.**
`AuditMaskingConfiguration.ApplyMasking` only rewrites tokens of the form `-name=value` / `--name=value`, and the
argument tokenizer strips `=` before a command ever sees it. Therefore the host's rule is stronger and simpler:

> **Never put a secret in a command-line parameter.** `CRED SET` reads its value from the secret-entry path
> (§B.4) or from a named environment variable; it never accepts the value as an argument. Any tool that declares a
> parameter whose normalized name is on the list above **fails registration at startup** — the host asserts this
> over `GetParameters()` for every registered command and exits 2 naming the offender.

The host still installs a `ChatDbgAuditMasking : IAuditMaskingConfiguration` carrying the lists above, as defence
in depth, and **additionally** replaces `AuditEvent.Parameters` wholesale with `["<n parameters redacted>"]` for
every command on the sensitive-command list (`CRED *`, `CONFIG SET` when the key matches a secret pattern,
`PKG TRUST`).

### D.4 Destructive-operation confirmation

**Destructive** means: irrecoverable loss of user data, an irreversible trust change, or an overwrite of a file
the user did not name in this invocation. The set is: `SESSION CLEAR`, `SESSION IMPORT` when it replaces rather
than appends, `SESSION EXPORT`/`TOKEN EXPORT`/`DIAG EXPORT` onto an existing path, `CONFIG RESET`,
`CONFIG PROFILE DELETE`, `CRED REMOVE`, `CRED MIGRATE` (it deletes plaintext after copying), `PROMPT DELETE`,
`PKG REMOVE`, `PKG TRUST`, `PKG UNTRUST`, `DIAG PURGE`.

Policy, uniform across all of them:

1. Every destructive tool declares `[CommandFlag("force", "Skip the confirmation prompt.")]`.
2. If `force` is present → proceed. The audit record carries `forced: true`.
3. Else if `io.HasPipedInput` is **false** → call `PromptForCommand` with a prompt that **names the exact object
   and requires typing it back**: `Delete prompt 'security-expert'? Type the name to confirm: `. A non-matching
   answer returns `Success(string.Empty, …)` — silent, no change, no error.
4. Else (piped, or the front end is non-interactive) → **refuse**: return
   `Failure("<op> is destructive and cannot run non-interactively; re-run with -force")`. This is mandatory,
   because `PromptForCommand` is contractually meaningful only when `HasPipedInput == false`, and a prompt written
   into a pipeline would hang.
5. `PKG TRUST` is a special case: `-force` does **not** bypass it. Granting trust always requires an interactive
   typed confirmation of the package id **and** its SHA-256 prefix, and on a non-interactive front end it always
   fails. Trust is granted by a human or not at all.

### D.5 Help

Help is auto-generated. `HELP` with no argument lists every command; `HELP <cmd>` and `<cmd> --HELP` / `-?` / `/?`
render the detail view. The host does not write a help renderer; it sets `controller.HelpCommand` (default
`"HELP"`) and lets the framework route.

**The generated detail view contains, in order:** `[<Root> ]<COMMAND>:`, the description, a `Usage:` block
carrying the prototype, an `Options:` block listing every parameter as `<indicator>  <value description>`, and a
`Remarks:` block. A tool that supplies all of the following gets a complete page for free:

| Element | Source | House style |
|---|---|---|
| Command name | `[CommandRegister("Name", …)]` | One word. Verb for an action (`INSTALL`), noun for a query (`STATUS`). Uppercased by the framework. |
| Description | `[CommandRegister(…, "description")]` | One sentence, imperative mood, no trailing period, ≤ 72 characters, so it fits the one-line list view. Say what it does, not how. |
| Prototype | `Prototype = "…"` named property | **Always set it.** The default is `String.Empty`, which prints a **blank usage line**; the auto-generated prototype is produced only when the value is the literal `"todo"`. Write a real one — `CRED SET <backend>` — or write `"todo"` deliberately. There is no third option that produces useful output. |
| Ordered parameter | `[CommandParameterOrdered("name","desc")]` | **`IsRequired` defaults to `true`** here, unlike every other parameter attribute. Say `IsRequired = false` when it is optional. Description is a noun phrase naming the value and its units or range. |
| Named parameter | `[CommandParameterNamed("name","desc")]` | Optional by default. Give a `DefaultValue` or an `AllowedValues` list; the first allowed value silently becomes the default if you do not. |
| Flag | `[CommandFlag("name","desc")]` | Description states what turning it on does. `DataType`, `DefaultValue`, `IsRequired` and `AllowedValues` are **ignored** on a flag — it is always a `bool`, always present in the dictionary. |
| Suffix parameter | `[CommandParameterSuffix("name","desc")]` | **At most one, declared last.** It consumes all remaining tokens joined by single spaces into **one string** — not an array. `AllowedValues` is **not enforced** for suffix parameters; it only changes the help text, so never rely on it for validation. |
| Remarks | `[CommandHelpRemarks("…")]`, repeatable | One idea per attribute. At least one worked example per tool, written as a literal command line. State the trust level a tool requires and any environment key it reads. |

**Mandatory remarks by category.** A tool that writes globals states so. A tool that reads a secret states which
source it uses. A destructive tool states that `-force` skips confirmation. A tool that cannot participate in a
pipeline says so in its remarks *and* returns an explanatory string from `HandlePipedChunk` rather than throwing
(**Cupcake §8 rule 29**).

### D.6 Naming rules

**Commands.** `[CommandRegister]` is **required on every tool class** — without it `AbstractCommand.Command`
throws `InvalidOperationException`, `CommandParameters.CreatePackageDescription` throws, and
`CommandRegistry.AddCommand` **traces a warning and silently returns without registering**. The host's startup
assertion (§A.3 step 6) exists because of that silent path.

* Command names are normalized to **UPPERCASE**.
* Group related tools with `[CommandRoot("PKG","Tool package management")]` on every member, giving `pkg install`.
  Nine roots, one per package (§F). Grouping is **Cupcake §8 rule 30**.
* **Never read `.RootCommand` on an `AbstractCommand` that has no `[CommandRoot]`** — the property throws. The
  shipped host never reads it; neither may ours. `CHAT` deliberately carries no root, and nothing in the host
  inspects its root.
* A root's sub-command dispatch shifts `Parameters[0]` off the array before instantiating. Invoking a root with no
  sub-command throws `InvalidOperationException` listing the available ones; an unknown sub-command on the async
  path (the one the executor actually uses) is **less forgiving than the sync path** and can surface as
  `Command type name is empty.` The host therefore intercepts a bare root invocation and renders `HELP <root>`
  instead of dispatching.

**Parameters — the normalization trap.** `AbstractCommandParameterAttribute.Name` is normalized by
`NamesValidator.GetValidCommandName(value, false)`, which trims, **takes only the text before the first space**,
trims leading and trailing `-`, **deletes every character not matching `[-_0-9A-Za-z ]`**, and **lowercases**.

Consequences, all normative:

1. `"Key"` is stored as `"key"`. Lookups are case-insensitive so this does not break code, but it *is* what the
   help text shows. **Write parameter names lowercase in the attribute** so the source reads like the help.
2. A name containing `.` `:` `/` `\` `@` or any other punctuation is **silently mangled**. Parameter names use
   `[a-z0-9_-]` only. No dotted names, no namespaced names.
3. A name containing a space is **truncated at the space**. `"max tokens"` becomes `"max"`.
4. Field injection matches a **public instance field** by name, case-insensitively, against the parameter key —
   not a property. `public string? modelId;` is injected; `public string ModelId { get; set; }` is not.
   Injection is silent on failure, never assigns null, and never runs at all when the command was invoked with
   **zero arguments** — the parser returns an empty dictionary immediately, applying no defaults and
   materializing no flags. **Every tool therefore reads defensively:**
   `parameters.TryGetValue("x", out var p) && p.IsValid ? p.GetValue<string>() : fallback`.
5. A named parameter that is the **last token with no value after it** throws `ArgumentOutOfRangeException` with
   no friendly message. Tools declare a `DefaultValue` for every named parameter so the failure mode is
   "used the default", not "crashed".
6. `UsePipe = true` marks the parameter fed by the pipe; the convention is **one per command**, and the framework
   does not enforce it.

**Environment keys:** §C.1. **Package keys:** the host uses `inbox` for compiled-in tools, `Default` is the
framework's own, and dynamically loaded packages get keys derived from the package on disk.

### D.7 Cancellation

**The gap to design around:** `ICommandDelegate.Main(IIoContext, IEnvironmentContext)` **takes no
`CancellationToken`.** `ICommandController.Run` accepts one, but for a single (non-pipeline) command the
framework only checks it *before* dispatch; for a pipeline it races `Task.WhenAll` against the token and can apply
a per-stage timeout. **Nothing in the framework can interrupt a long-running `Main`.** The source product made
this worse by having no `CancellationToken` anywhere in production code at all, so a running generation could not
be stopped by any means short of killing the process.

The host closes the gap in four layers:

1. **Per-dispatch source.** `Loop` creates one `CancellationTokenSource` per dispatch and always calls the
   `Run(commandLine, io, env, cancellationToken)` overload. The source is disposed when the dispatch returns.
2. **The gesture.** The line shell hooks `Console.CancelKeyPress` and sets `e.Cancel = true` so the process is
   **not** terminated; the full-screen shell binds a key and a Stop affordance; the batch front end handles
   `SIGINT`/`SIGTERM`. All three do exactly one thing: cancel the current dispatch's source.
3. **Reaching the tool.** The IO context implements `Xcaciv.ChatDbg.Abstractions.ICancellationSignal`, exposing
   `CancellationToken Token { get; }` set by the host on the root context and propagated to every child in
   `GetChild()`. **A well-behaved tool** pattern-matches `io is ICancellationSignal signal` and honours
   `signal.Token`: it passes it to every async call it makes, checks it between chunks in a piped loop, and on
   cancellation stops work and yields one final `CommandResult<string>.Failure("cancelled")` rather than throwing
   `OperationCanceledException` out of `Main`. A tool that ignores the signal still works; it simply cannot be
   stopped, and the host will report that.
4. **Abandonment.** If the dispatch task has not returned within `CHATDBG_CANCEL_GRACE` (default 5 s) of the
   cancel, the host **stops rendering, prints `cancelled — the tool did not stop; it is still running in the
   background`, writes a `Cancelled` audit record naming the command, and returns to the prompt.** The orphaned
   task is tracked; if it is still running at shutdown, the host waits its 5 s disposal budget and then exits
   anyway. A second cancel gesture within 2 s of the first is a request to exit: the host runs the shutdown
   sequence and exits 1.

**Requirement on backends.** Because a model call is the archetypal long operation, `IModelBackend` methods take a
`CancellationToken` **as a required first-class parameter**. A backend implementation that cannot honour it — a
native inference loop with no cancellation hook — must chunk its work and check between chunks. A backend that
cannot be cancelled is not hostable.

---

## E. Tool package loading and trust

This section applies the Xcaciv.Loader §10 checklist to this product. It is written as the host's obligations,
because **the framework's own loading path does not do most of it**: `Crawler` constructs each `AssemblyContext`
itself with `basePathRestriction` set to the package's parent directory and **`integrityVerifier` omitted** — so
there is *no* hash verification on the framework's load path, and no host-visible events. Everything below that
concerns integrity, manifests and trust therefore runs in the host **before** `AddPackageDirectory` is called.

### E.1 Where packages live, and how the path is locked down

```
<AppContext.BaseDirectory>/tools/            <- the shipped root, CHATDBG_PACKAGE_DIR
    <PackageId>/<Version>/bin/<PackageId>.dll
    <PackageId>/<Version>/package.json
<DATA_ROOT>/tools/                            <- the user root, CHATDBG_USER_PACKAGE_DIR
    …same layout…
```

The layout matches the crawler's search mask, which is `*/<subDirectory>/*.dll` with `subDirectory` defaulting to
`bin`, searched recursively, and it matches the `{id}/{version}/` layout an acquiring tool produces.

Rules:

* Both roots are computed with `Path.GetFullPath(Path.Combine(base, "tools"))`. **Never a relative path.** Cupcake's
  default is `@".\packages"`, resolved against the process working directory — that is a deliberate deviation:
  a relative root means the set of loadable code depends on where the user happened to `cd`.
* The **shipped root must not be writable by the runtime account**. The host checks this at startup; if it is
  writable by a non-administrator, the host **refuses to start** (exit 4) rather than degrading. This is the
  primary mitigation for the integrity check→load TOCTOU window and for symlink games, because the library
  resolves neither.
* The **user root is consulted only when `CHATDBG_ALLOW_USER_PACKAGES=true`**, which can be set from the command
  line or the config file but is read-only at runtime. Every DLL under it must be in the hash allowlist; there is
  no learning mode.
* **Never `basePathRestriction: "*"`**, and never the one-argument `AssemblyContext.VerifyPath(path)` — its
  default *is* `"*"`. The `WildcardPathRestrictionUsed` event is raised inside the constructor and is therefore
  unsubscribable; "no wildcard" is enforced by the host's own code, not by listening for it.

### E.2 Security policy

Two policies, chosen by trust level:

```csharp
// Third-party packages the host does not control:
var policy = new AssemblySecurityPolicy(forbiddenDirsForThisOs)
             { DisallowDynamicAssemblies = true };   // the ctor forces this false — re-enable explicitly

// Only for in-box packages we build and control:
var strict = AssemblySecurityPolicy.Strict;          // StrictMode + DisallowDynamic + preflight
```

* `forbiddenDirsForThisOs` is supplied **per OS**. The built-in `Strict` list is Windows-shaped — it normalizes
  `/` to `\` and names `windows`, `system32`, `programfiles`, `credentials`, … — and is essentially inert on Linux
  and macOS, where `/usr/lib`, `/etc`, `/proc`, `~/.ssh`, `~/.aws`, `~/.config` must be supplied by the host.
* The custom-list constructor **silently sets `StrictMode = false` and `DisallowDynamicAssemblies = false`**. The
  flag is re-enabled explicitly; preflight is lost, because preflight is gated on `StrictMode`, which that
  constructor cannot set.
* **`Strict` is not used for the Model Backends package.** Preflight rejects any assembly referencing
  `System.Reflection.Emit` or calling `System.Linq.Expressions…Compile` — which cloud provider SDKs, serializers
  and DI plumbing all do. Running `Strict` over a backend package would reject it every time. In-box packages that
  are pure command logic get `Strict`; anything carrying a third-party SDK gets the custom policy.
* `Crawler` defaults to `AssemblySecurityPolicy.Strict`, so the host must call `crawler.SetSecurityPolicy(policy)`
  **before** constructing the controller with it:
  `new CommandController(crawler, restrictedDirectory: packageRoot)`.
* **Never call `AssemblyContext.SetStrictDirectoryRestriction`** — it is an obsolete no-op that writes a debug
  line, and `IsStrictDirectoryRestrictionEnabled()` returns a hardcoded `false`. Ported code that branches on
  either silently runs with the `Default` policy.
* The library gives **isolation of assembly identity, not of privilege**. A loaded package runs with the host's
  full trust — it can read files, open sockets, and P/Invoke. Everything here is defence in depth around
  *loading*. Genuinely untrusted packages are out of scope for in-process hosting; the `Sandboxed` trust level
  (§E.3) restricts what the *host* will grant them, not what the OS will let them do.

### E.3 Trust levels

| Level | How it is earned | Granted |
|---|---|---|
| **InBox** | Compiled into the host, or shipped under `CHATDBG_PACKAGE_DIR` with a hash pinned at build time. | Everything, including `modifiesEnvironment`, credential access, and native payloads. |
| **Trusted** | Under either root, with a `package.json` whose detached signature verifies against a publisher key in the host's key store, **and** every file's SHA-256 present in the allowlist. | May declare `modifiesEnvironment`; may read the secret store through `ISecretStore`; may ship native assets. |
| **Sandboxed** | Under a root, every file hash-allowlisted, but unsigned or signed by an unknown key. | Loads and runs. **Refused**: `modifiesEnvironment` (its request is logged and denied), `ISecretStore` (the host does not hand it the interface), and native assets (a package with a `runtimes/` directory is rejected outright). |

Anything that does not reach `Sandboxed` does not load.

### E.4 Integrity verification and the hash allowlist

```csharp
var store = new AssemblyHashStore();
store.LoadFromFile(trustStorePath);                       // absolute paths only
var verifier = new AssemblyIntegrityVerifier(
    enabled:      true,
    learningMode: false,                                  // MANDATORY in production
    algorithm:    HashAlgorithmName.SHA256,
    hashStore:    store);
verifier.HashMismatchDetected += (p,e,a) => security.Critical(...);
verifier.HashLearned          += (p,h) => security.Warning("learning mode in production", p);
```

Normative details, each one a documented hazard:

* **`learningMode` defaults to `true`.** A verifier constructed with `enabled: true` and nothing else trusts every
  new file on sight. Production passes `learningMode: false` explicitly; `HashLearned` firing in production is a
  **warning-severity incident**, because it means someone shipped a learning-mode verifier.
* **Write absolute paths into the CSV.** `LoadFromFile`/`MergeFromFile` store the path exactly as written and do
  **not** normalize it, while `TryGetHash` normalizes the lookup to an absolute path — so any relative entry can
  never match and its assembly is treated as unknown (and, in strict mode, blocked). Silent trust-store failure.
* **Canonicalize case** before writing or looking up: store keys are compared ordinally, so on Windows
  `C:\Tools\A.dll` and `c:\tools\a.dll` are two entries for one file.
* **The store is not portable.** Keys are absolute machine paths, so a CSV built on a build agent is useless on a
  machine that installs elsewhere. The host generates it **on the target machine at install time** (learning mode
  + `SaveToFile`), or builds it **in memory at startup** by mapping the signed manifest's per-file digests onto
  resolved absolute paths with `AddOrUpdate`. The in-memory route is preferred: it removes the CSV as a trust
  anchor entirely.
* **The CSV is an unsigned trust anchor.** Anyone who can write it can authorize any DLL. It lives beside the
  shipped root with install-process-only write permission, and its own SHA-256 is pinned in the config the host
  reads at startup.
* **A corrupt or missing store fails startup, loudly, exit 4.** `FormatException` naming a line number, or
  `FileNotFoundException`, must never degrade into "trust nothing quietly" or "trust everything".
* **The verifier does not run on the framework's load path.** The host therefore performs its own verification
  pass over every candidate `*/bin/*.dll` under each root *before* calling `AddPackageDirectory`, and only
  registers roots whose every package passed. This is also where dependency DLLs shipped inside a package are
  hashed, closing the gap that the framework's crawler leaves open.
* **This is not code signing.** Hash verification proves "these bytes are the bytes I recorded", not "these bytes
  came from a party I trust". Publisher identity is verified by the host in §E.5 step 3, using the manifest
  signature — the library performs no Authenticode, strong-name or publisher check anywhere.

### E.5 The preflight gate — rejection criteria

Run in order, per candidate package, entirely in the host, before any `AssemblyContext` is constructed for real:

1. **Path sanity.** `AssemblyPathValidator.IsSafePath` then `ValidateAndSanitize` (rejects null bytes, `..`,
   `*`/`?`, `<`/`>`/`|`), then `HasValidAssemblyExtension` — which is required because
   `AssemblyContext.VerifyPath` **passes any path with no extension at all**.
2. **Component-wise containment.** The library's containment test is `fullPath.StartsWith(base)`, so base
   `…/tools` also admits `…/toolsEvil/x.dll`. The host uses
   `Path.GetRelativePath(root, path)` and rejects a result that starts with `..` or is rooted.
3. **Real-path resolution.** `new FileInfo(path).ResolveLinkTarget(true)` and re-check containment. The library
   does not follow reparse points, so a symlink inside the package directory can point outside it.
4. **Manifest present and well-formed.** `package.json` beside `bin`, declaring `id`, `version`, `rootCommand`,
   `commands[]`, `modifiesEnvironment[]`, `requiresSecrets`, `nativeAssets`, and per-file `sha256` digests.
5. **Signature.** Detached signature over the manifest verifies against a known publisher key → `Trusted`;
   absent or unknown key → `Sandboxed`; present but **invalid** → **reject** (a broken signature is worse than
   none).
6. **Hash match.** Every file the manifest lists, and every `.dll` actually present under the package, matches the
   allowlist. An **extra** unlisted DLL is a rejection, not a warning.
7. **Capability vs. trust.** A `Sandboxed` package declaring `modifiesEnvironment`, `requiresSecrets`, or
   `nativeAssets` is rejected (or loaded with the capability denied, per §E.3 — `nativeAssets` is a hard reject).
8. **Framework identity.** The package directory must **not** contain a private copy of
   `Xcaciv.Command.Interface.dll` or `Xcaciv.Command.Core.dll`. A plugin carrying its own copy compiled against a
   different version produces `ReflectionTypeLoadException`, which the crawler catches and reports as exactly this
   cause — the host rejects it earlier and with a better message.
9. **Root-command collision.** The declared `rootCommand` must not be one of the nine in-box roots and must not
   collide with an already-accepted package. First accepted wins; the loser is rejected with both package ids
   named. *(Without this the registry silently merges sub-commands into an existing root and a third-party package
   can inject a sub-command under an in-box root.)*
10. **`deps.json` review.** Dependency resolution inside the library loads with `VerifyPath(path, "*", policy)` —
    **dependencies are not confined to the base path**, only to the forbidden-directory list, so a crafted
    `*.deps.json` can point outside the package. The host strips or validates `deps.json` at install time and,
    because every shipped file is hashed with `learningMode: false`, an unexpected dependency path is rejected as
    "no trusted hash".

### E.6 Dynamic-assembly policy

* `DisallowDynamicAssemblies = true` on every policy the host builds. Note its **scope is narrow**: it fires only
  for assemblies loaded *through* a context, so it does **not** stop an already-loaded package from emitting code
  with `AssemblyBuilder`. Preflight is what tries to keep such a package from loading at all — and preflight is
  metadata-only, **fails open on any parse error** (an empty catch), and is evadable by obfuscation. Treat it as a
  lint, not a sandbox.
* `EnableGlobalDynamicAssemblyMonitoring()` is **off by default** and enabled only under
  `CHATDBG_AUDIT_DYNAMIC=true` for high-assurance deployments. It is **audit-only — it never blocks** — and it
  installs a process-wide `AppDomain.AssemblyLoad` handler that takes a global lock on every assembly load in the
  process, with no published benchmark. When it is on, a resulting `SecurityViolation` is an incident signal (a
  package generated code at runtime), routed to the security log, never a block.

### E.7 Events the host subscribes to

Subscribed **immediately after each context is constructed and before the first `GetTypes`/`CreateInstance`**,
because loading is lazy and every load-time event fires on that first call. Handlers are thread-safe and never
throw — they may run on a thread-pool thread, and an exception from a handler propagates into the load path.

| Event | Severity | Host action |
|---|---|---|
| `SecurityViolation(path, reason)` | **critical** | Quarantine the package, mark it untrusted in the registry, alert. The reason string distinguishes preflight / dynamic-assembly / global-monitor / dependency-path violations — this is the primary attack signal. |
| `HashMismatchDetected(path, expected, actual)` | **critical** | Tamper. Quarantine; log expected vs. actual **to the security log only**, never to the user-facing audit trail or the terminal. |
| `AssemblyLoadFailed(path, ex)` | error | Record with correlation. A burst against one path is a probing signal. |
| `HashLearned(path, hash)` | warning | Must be impossible in production; means a learning-mode verifier shipped. |
| `AssemblyUnloaded(path, success:false)` | warning | Live references remain; memory will not be reclaimed. Tracked as a leak metric. |
| `DependencyResolved(name, path)` | info | **Keep it.** It is the only record of where each transitive dependency came from — the thing you will want during an incident. |
| `AssemblyLoaded(path, name, version)` | info | The loaded-package inventory, surfaced by `PKG LIST`. |
| `WildcardPathRestrictionUsed` | — | **Unsubscribable** (raised in the constructor). Enforced in host code instead. |

Because `IAssemblyContext` exposes **no events and no policy** — they are all on the concrete `AssemblyContext` —
the host's `ToolPackage` wrapper holds the **concrete type** and wires the events at construction. A plugin
registry built on the bare interface cannot observe its own audit trail.

### E.8 Discovery, activation and unload

* **Discovery is by contract, never by name.** The crawler discovers `ICommandDelegate` implementors for us; where
  the host inspects a package directly (`PKG VERIFY`, `PKG INFO`), it uses `ctx.GetTypes<ICommandDelegate>()` and
  rejects a package with **zero** implementations or with an ambiguous entry type.
  **`CreateInstance(string)` is never used**: it suffix-matches `FullName.EndsWith(className)` across the primary
  assembly *and every dependency*, first match wins, and returns `null` on a miss — a type-confusion hazard. If a
  name must be used, it is `CreateInstance<T>(string)` with a fully-qualified name, which searches only the
  primary assembly and throws `TypeNotFoundException` on a miss.
* **Activation is `Activator.CreateInstance` — parameterless constructors only.** There is no constructor
  injection at the loader level. Tools receive their collaborators through `IIoContext`, `IEnvironmentContext`,
  and the interfaces in `Xcaciv.ChatDbg.Abstractions` resolved from the host's service provider by
  `CommandFactory`, which prefers `IServiceProvider.GetService(commandType)` when the type resolves. **Register
  tool types transient**, never singleton: a singleton registration reuses the *same instance* across executions
  and across pipeline stages, and any per-run state (a cached regex, an accumulated buffer) leaks between runs.
* **Always the path-based `AssemblyContext` constructor.** The name-based one sets `FilePath` to empty, which
  makes every `CreateInstance` throw `FileNotFoundException` before the assembly is consulted, and it skips both
  preflight and integrity verification.
* **Serialize the first load per context.** The library's entire load path is lock-free and mutates non-volatile
  fields; two threads first-loading the same context race. Different packages may load in parallel freely — that
  is what the instance-based policy design bought. The host wraps each package's first load in a `Lazy<T>` with
  `LazyThreadSafetyMode.ExecutionAndPublication`.
* **`LoadTimeout` is 30 s** and is set with an object initializer (it is `init`-only). A `TimeoutException`
  **unblocks the caller but cannot abort the load** — the thread-pool thread may still complete it and run module
  initializers. The host treats the context as being in an unknown state: dispose it, never reuse it, log it as a
  possible denial-of-service signal, and do not retry that package this session.
* **Unload is terminal and there is no reload.** `Unload()` nulls the load context; any later call throws.
  `ICommandController` has no "remove package" operation. Therefore **`PKG INSTALL`, `PKG UPDATE` and
  `PKG REMOVE` do not mutate the live registry.** They write to a staging directory, update the allowlist, and
  record a pending change; the host applies it at the next start and tells the user
  `installed <id> <version> — restart to activate`. This is a deliberate, honest limit, and it is the closed form
  of the loop Cupcake left open (search → install → *re-scan* → first-class command).
* **Dispose deterministically.** One context per package, `using`/`await using` scoping its lifetime; drop every
  reference to instances, types and delegates first, or `AssemblyUnloaded` reports `success == false`. The
  finalizer path skips unload entirely and is never a cleanup route. `DisposeAsync` does **not** deregister the
  global monitor — only `Dispose` does — so contexts that enabled monitoring are disposed synchronously.
* **The minimum security-correct catch set for any load attempt is `SecurityException` *and*
  `ArgumentOutOfRangeException`.** Confinement failure — the path escaping the base restriction — is an
  `ArgumentOutOfRangeException`, and a host that catches only `SecurityException` will let a path-escape attempt
  surface as a generic argument bug. Conversely `SecurityException` covers three semantics — policy denial,
  integrity failure, and plain path-plumbing failure such as `PathTooLongException` — so "attack" is never
  inferred from the type alone; the host correlates with the `SecurityViolation` event's structured reason.

### E.9 Failure decision table

| Failure at step | What the host does | What the user sees |
|---|---|---|
| **0** Package root missing | Skip the root; continue with zero packages from it. Info record. | Nothing, unless *no* root yielded packages → `No tool packages found. Try \`pkg --help\`.` |
| **0** Shipped root is writable by a non-admin | Abort startup. Security record. | `chatdbg: '<root>' is writable by non-administrators; refusing to load tool packages` → **exit 4** |
| **1** Trust store missing / `FormatException` at line N | Abort startup. Security record naming the line. | `chatdbg: trust store '<path>' is unreadable (line N); refusing to load tool packages` → **exit 4** |
| **2** Path sanity / extension rejected | Reject the package; do not construct a context; warning record. | `tool package '<id>' rejected: invalid package path` |
| **3** Containment or symlink escape (`ArgumentOutOfRangeException`) | Reject; **security**-severity record; quarantine the directory; alert. Do not retry. | `tool package '<id>' rejected: path escapes the package root` |
| **4** Manifest missing or malformed | Reject; warning record. | `tool package '<id>' rejected: missing or invalid package.json` |
| **5** Signature invalid | Reject; **security** record; quarantine. | `tool package '<id>' rejected: signature verification failed` |
| **5** Signature absent / unknown key | Load at `Sandboxed`; info record. | `tool package '<id>' loaded unsigned — environment writes and secret access are denied` |
| **6** Hash unknown (`SecurityException` "no trusted hash") | Reject; **security** record; quarantine. | `tool package '<id>' is not in the trusted list — run \`pkg trust <id>\`` |
| **6** Hash mismatch (`HashMismatchDetected` → `SecurityException`) | Reject; **critical** record with the hash pair **to the security log only**; quarantine; alert. | `tool package '<id>' failed integrity verification and was quarantined` — no hashes, no paths |
| **6** Extra unlisted DLL present | Reject; security record naming the file. | `tool package '<id>' rejected: unexpected file in package` |
| **7** Capability exceeds trust | Load with the capability denied; warning record. `nativeAssets` → reject. | `tool package '<id>': environment-modifying rights denied at trust level 'sandboxed'` |
| **8** Private framework copy present | Reject; warning record. | `tool package '<id>' rejected: ships its own copy of the command framework` |
| **9** Root-command collision | Reject the later package; warning record naming both. | `tool package '<id>' rejected: root command 'X' already provided by '<other>'` |
| **10** `deps.json` points outside the package | Strip it and reload, or reject if the package then fails to resolve. Security record. | `tool package '<id>' rejected: dependency resolution escapes the package` |
| **Load** `SecurityException` from preflight or dynamic-assembly policy | Reject; **security** record with the reason string; quarantine; **no retry**. | `tool package '<id>' was blocked by security policy` |
| **Load** `TimeoutException` | Dispose and abandon the context; error record; do not retry this session. | `tool package '<id>' timed out while loading and was skipped` |
| **Load** `BadImageFormatException` / `FileNotFoundException` | Reject; error record. | `tool package '<id>' is incomplete or corrupt` |
| **Load** `FileLoadException` | Retry **once**, then reject; error record. | `tool package '<id>' could not be loaded` |
| **Load** `ReflectionTypeLoadException` (per type / per package) | Framework skips the type or the package and traces it; the host promotes the trace to a warning record. | `tool package '<id>': N commands could not be loaded` |
| **Discovery** zero `ICommandDelegate` implementors | Package is not added at all (the crawler drops packages with no valid commands); info record. | `tool package '<id>' contains no commands` |
| **Discovery** `TypeNotFoundException` / `InvalidCastException` | Contract violation; reject the package naming the type; error record. | `tool package '<id>' does not implement the tool contract` |
| **Registration** `[CommandRegister]` missing | The registry **traces and silently skips**; the host's post-load assertion catches the discrepancy and turns it into a warning record. | `tool package '<id>': N types were skipped (missing registration attribute)` |
| **Any** unexpected exception during the load phase | Wrapped in `LoadingException("Unable to load tools.", ex)`; inner preserved. | `chatdbg: Unable to load tools. → <inner type>: <inner message>` → **exit 2** |
| **`NoPluginsFoundException`** | Caught specifically; **survivable**; execution continues to the prompt on **every** run path. | `No tool packages found. Try \`pkg --help\`.` then the prompt |

---

## F. Package map

Nine packages. Each owns one root command, one area of the source product's fifteen features, and the entirety of
its own persistence. Every package project references `Xcaciv.Command.Interface`, `Xcaciv.Command.Core` and
`Xcaciv.ChatDbg.Abstractions` — and **never the host** (**Cupcake §8 rule 4**).

| # | Package | Root command | What it owns | Trust required | Ships in the box? |
|---|---|---|---|---|---|
| 1 | **Session & Conversation** | `SESSION` (plus the rootless `CHAT`) | The chat turn itself; conversation history as a first-class object; message injection at a position; pop/clear; import and export of a transcript; the raw-line channel's receiving end. Owns `<DATA_ROOT>/transcripts`. | **InBox** — `CHAT` is the product; it must be present and cannot be replaced by a package. | **Yes**, compiled in and registered by `Type`. |
| 2 | **Configuration & Profiles** | `CONFIG` | Reading, validating, listing and persisting every `CHATDBG_*` CFG key; named profiles; `CONFIG INIT`, `CONFIG RESET`, `CONFIG PROFILE <use\|list\|save\|delete>`. The **only** in-box tool besides the framework's `SET` registered `modifiesEnvironment: true`. Owns `<DATA_ROOT>/config.json`. | **InBox** — it holds the global write grant. | **Yes.** |
| 3 | **Credentials & Secret Storage** | `CRED` | `ISecretStore`; resolution order (process environment → OS credential store → deprecated config file); `CRED SET/REMOVE/STATUS/MIGRATE`; the masked secret-entry path. **Never returns a secret value as output**, only a source name. | **InBox only.** A dynamically loaded package may never provide credential storage, and only a `Trusted` package is handed `ISecretStore` at all. | **Yes**, and it is the one package that can never be acquired at runtime. |
| 4 | **System Prompt Library** | `PROMPT` | Named system prompts; create/edit/delete/list/show/export/import; the four seeded defaults; the allow-list backing `CHATDBG_SYSTEM_PROMPT_NAME`. Owns `<DATA_ROOT>/prompts`. Writes seeds **only when `PROMPT INIT` is run**, never as a constructor side effect. | **Trusted** | **Yes**, but replaceable — a `Trusted` package may supply an alternative library. |
| 5 | **Model Backends** | `MODEL` | `IModelBackendFactory` registration; `MODEL LIST/USE/INFO/TEST`; one sub-package per backend (hosted cloud chat completion, hosted cloud model-invoke, local GGUF inference). Owns the SDK and native dependencies, and the `IModelBackend : IAsyncDisposable` lifetimes the host disposes at shutdown. | **Trusted** (native assets and secret access both require it). **Never `Strict`** — preflight would reject the provider SDKs. | **Yes** for the three in-box backends; **acquired at runtime** for any additional backend. This is the primary extension point. |
| 6 | **Token Introspection** | `TOKEN` | Tokenization, token attribution, log-probability capture and the probability map; `TOKEN TOKENIZE/INSPECT/MAP/ANALYZE/EXPORT`; the enable/disable/top-K surface behind `CHATDBG_LOGPROBS_*`. Consumes `IModelBackend`; produces the structured shapes in `Abstractions`. | **Trusted** | **Yes**, replaceable. |
| 7 | **Presentation & Visualization** | `VIEW` | Themes; the token heat map and grid; table and rule rendering; the `IOutputEncoder` set the host installs; `VIEW THEME/GRID/RENDER`. Renders **only** through `IIoContext` — never `Console`. | **Sandboxed** is sufficient; it needs no environment writes and no secrets. | **Yes**, and it is the package most expected to be replaced at runtime. |
| 8 | **Diagnostics & Observability** | `DIAG` | The diagnostic sink behind `AddTraceMessage`; native-library log capture; `DIAG TAIL/EXPORT/LEVEL/PURGE`; the audit-trail reader (`DIAG AUDIT`, which renders the masked record, never the raw one). Owns `<DATA_ROOT>/logs`. | **Trusted** (it reads the audit trail). | **Yes.** |
| 9 | **Tool Package Management** | `PKG` | Feed search, version enumeration, dependency resolution, download, identity re-read, staged install; `PKG SEARCH/INSTALL/UPDATE/REMOVE/LIST/INFO/VERIFY/TRUST/UNTRUST`. Owns the allowlist and the staging directory. Trust changes always require interactive confirmation (§D.4). | **InBox** — the package that grants trust cannot itself be acquired at runtime. | **Yes.** |

**Notes on the map.**

* **Acquisition rules for `PKG`** follow the acquiring-command pattern: the source feed URL comes from the
  environment with a hardcoded fallback (**Cupcake §8 rule 28**), **HTTPS is mandatory** on the source URL,
  result counts are clamped, input length is bounded, and vulnerability counts are surfaced in detailed output
  (**Cupcake §8 rule 45**). The feed wrapper takes the repository **in** from the caller so source-selection
  policy lives in one place and cannot be bypassed (**Cupcake §8 rule 44**); every async method takes optional
  logger and cancellation token (**Cupcake §8 rule 43**). Package identity is **re-read from the downloaded
  artifact**, never trusted from the request, and laid out as `{id}/{version}/bin/` so the loader finds it on the
  next scan (**Cupcake §8 rule 46**). `PKG SEARCH` offers `verbosity` with `quiet`/`normal`/`detailed` and a
  `default:` arm falling back to `normal` (**Cupcake §8 rule 47**).
* **A package that advertises a recovery command must implement it.** Cupcake's plugin-less message points at
  `install --help`, and `InstallCommand` installs nothing. The message in §A.3 step 8 points at `pkg --help`, and
  `PKG INSTALL` performs a staged install for real.
* **Feature coverage.** The fifteen source features map onto these nine packages plus the host: features 1 and 2
  (the two shells) become §A.2's two composition roots; feature 3 (command system) becomes the framework itself;
  feature 15 (packaging) becomes the `Release` `PropertyGroup` of §A.1. Features 4–14 map one-to-one or two-to-one
  onto packages 1 and 3–8.
