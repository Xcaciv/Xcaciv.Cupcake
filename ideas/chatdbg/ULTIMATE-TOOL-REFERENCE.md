# ChatDbg — Ultimate Tool Reference

> **Deliverable 4 of 4.** A *hypothetical* specification of the complete tool surface for a rebuilt ChatDbg,
> structured as a Cupcake-patterned shell whose entire capability set is attribute-declared, dynamically-loadable
> tools.
>
> **This document is a handoff brief.** It is written to be consumed by an architect — human or model — who will
> produce the architecture documentation from it. It specifies *what tools exist, what they accept, what they
> guarantee, and how they compose*. It deliberately stops short of architecture: no assembly diagrams, no class
> models, no sequence diagrams of internal collaborators. Those are the next artifact, and this is its input.

---

## 0. Orientation

### 0.1 What this is, and what it is not

| | |
|---|---|
| **Is** | A tool catalog: 117 tools in 9 packages, each with its registration, parameters, pipeline behaviour, environment interaction, failure modes, security posture and traceability. |
| **Is** | A host specification: how a Cupcake-patterned shell composes, starts, dispatches, loads packages, and shuts down. |
| **Is** | A conventions contract: the shared environment namespace, result formats, error and audit rules that every tool must honour. |
| **Is not** | An architecture. There are no internal component diagrams, no dependency-injection graphs, no layering rules. |
| **Is not** | An implementation. Attribute declarations appear because they *are* the specification language of the tool framework; no method bodies do. |
| **Is not** | A description of the existing product. It is the *target*. Where it departs from the source, it says so and says why. |

### 0.2 The three fixed constraints

This specification is written against three components that are **given, not chosen**:

| Component | Role | Version | Reference |
|---|---|---|---|
| **Xcaciv.Cupcake** | The host pattern — how the shell is composed, how it loops, how it contributes tools | pattern, not a dependency | `synthesis/ref-cupcake.md` (521 lines) |
| **Xcaciv.Command** | How tools are *defined* and *executed* — the delegate contract, the attribute vocabulary, the typed parameter system, the pipeline | 3.3.x | `synthesis/ref-command.md` (2,145 lines) |
| **Xcaciv.Loader** | How tool packages are *loaded* — isolation, path policy, integrity verification, unload | 2.1.2 | `synthesis/ref-loader.md` (1,060 lines) |

Everything else — runtime, inference libraries, rendering, storage — is chosen in `TECH-STACK-TARGET.md` and is out of scope here.

### 0.3 Provenance: where the behaviour came from

Every tool in this catalog is either **ported** or **new**.

- **Ported** tools trace to observed behaviour in the source product at commit `d8c18f61d6bb73666ed97cd4885e877e35558485`, by way of a feature dossier and a PRD subsection. Their defaults, valid ranges, path expressions and error strings are the *real* ones, not invented ones.
- **New** tools have no ancestor in the source. Each is marked **NEW** and carries a stated reason for existing.

Of the 117 tools, **63 are ported and 54 are new**. The new ones cluster where the source had a capability but no way to reach it: the source shipped three fully-implemented commands that neither front end ever registered, a diagnostic-logging component whose configuration surface no user could touch, and a credential store with no way to list, rotate or scan what it held.

### 0.4 How to read a tool entry

Each tool is specified as:

1. **Registration** — command name, root command, description, usage prototype.
2. **Parameters** — one row each: name, kind (ordered / named / flag / suffix), type, required, default, allowed values or range, help text. Values carried from the source are real; parameters with no ancestor are marked NEW.
3. **Pipeline behaviour** — source, filter, sink, or not pipeable; what one piped chunk means.
4. **Environment interaction** — keys read, keys written, whether environment-modifying rights are needed.
5. **Failure modes** — bad input, missing prerequisites, upstream pipeline errors, and what the operator sees for each.
6. **Security and audit** — whether any parameter or output carries a secret and must be masked; whether the action is destructive and needs confirmation.
7. **Traceability** — the PRD feature subsection and the source command it descends from, or NEW.

---

## 0.5 Package map

| # | Package | Root | Owns | Trust required | Ships in box? | Tools |
|---|---|---|---|---|---|---|
| 1 | `ChatDbg.Tools.SessionConversation` | `CHAT` | Conversational turns and the conversation record | Network (via backend) | Yes | 12 |
| 2 | `ChatDbg.Tools.ConfigurationProfiles` | `SET` | Every tunable, the settings file, named profiles | Filesystem (user profile) | Yes | 16 |
| 3 | `ChatDbg.Tools.CredentialsSecretStorage` | `CRED` | Secret supply, resolution, storage, rotation, redaction | OS keystore + filesystem | Yes | 13 |
| 4 | `ChatDbg.Tools.SystemPromptLibrary` | `PROMPT` | Named instruction prompts and their lifecycle | Filesystem (user profile) | Yes | 14 |
| 5 | `ChatDbg.Tools.ModelBackends` | `MODEL` | The backend registry, selection, capability probing, local model load | Network + native library | Yes | 11 |
| 6 | `ChatDbg.Tools.TokenIntrospection` | `TOKEN` | Log-probability capture, tokenization, probability maps, attribution, statistics | Native library (local paths) | Yes | 13 |
| 7 | `ChatDbg.Tools.PresentationVisualization` | `VIEW` | Layout, heat mapping, themes, terminal capability, degradation | Terminal only | Yes | 13 |
| 8 | `ChatDbg.Tools.DiagnosticsObservability` | `DIAG` | Engine diagnostics, capture, rotation, export, health check, audit | Filesystem + native log hook | Yes | 12 |
| 9 | `ChatDbg.Tools.ToolPackageManagement` | `PKG` | Acquiring, verifying, trusting, updating and removing tool packages | **Elevated** — writes the plugin root | Yes (must be) | 13 |

Package 9 is the one package that **cannot** be acquired at runtime, because it is the thing that does the acquiring. It ships in the box and its own trust decisions are made by the host, not by itself.

---

## 0.6 What changes relative to the source product

The tool surface is a redesign, not a transcription. The substantive departures:

| # | Source behaviour | Target behaviour | Why |
|---|---|---|---|
| 1 | Two front ends with **divergent** command surfaces, hand-wired separately in each | One tool set, one controller, both front ends driving it | The source's full-screen host silently omits a backend and mutates a settings record nothing reads. Divergence was not a feature; it was drift. |
| 2 | Invented probability data presented as measured | **Never fabricate — owner-ratified, decision D-001 (`DECISIONS.md`).** Declared capability, honest refusal, provenance on every derived value; a capability-absent provider **auto-disables** the log-probabilities setting with a switch-provider instruction, the enabling UI is **disabled** where the platform allows it, and view attempts produce a **non-blocking** notice | The product exists to help a user judge model confidence. Inventing confidence inverts its purpose. |
| 3 | Three implemented commands registered by neither host | Every tool in the catalog is reachable, or it is not in the catalog | Shipping unreachable code as if complete is the defect; the fix is registration discipline. |
| 4 | Capability discovered by attempting and catching | Capability is a **declared, queryable value**, probed once and cached | A backend that cannot supply alternatives should say so before the call, not after. |
| 5 | No cancellation anywhere | Cancellation is part of the tool contract; a long generation is interruptible | An unabortable generation up to the maximum response length is a hard freeze. |
| 6 | Secrets resolved by side-effecting property reads on a serialization record | Secrets resolved once at an explicit boundary; the settings type is structurally incapable of holding one | Prevents the class of bug where a secret round-trips into a plaintext settings file. |
| 7 | GPU backend an unconditional dependency (~550 MB, 88% of output) | Acceleration backends are opt-in per artifact | Nobody should download a CUDA payload to run a cloud-backed chat. |
| 8 | Single flat command namespace, ad-hoc argument splitting | Root-grouped commands with typed, validated, attribute-declared parameters | Validation before execution is the framework's injection defense, and it generates the help for free. |
| 9 | No pipelines | Every tool declares its pipeline role; analysis composes | Token analysis is inherently a data-processing task, and the framework already threads pipelines. |

---

## 0.7 Handoff brief — what the architect should produce from this

This document deliberately answers *what* and leaves *how* open. The architecture documentation that follows it should decide and record:

1. **Assembly and project topology** — how the 9 packages, the contract assembly, the shared core and the two front ends map onto build artifacts, and which of them are separately versioned and separately shipped.
2. **The contract boundary** — precisely which types cross the plugin isolation boundary, and therefore which assembly must be pinned in the host's default load context and never duplicated.
3. **Internal composition** — the service graph behind the tools: what is a singleton for the process, what is per-session, what is per-invocation, and where the backend resolver sits.
4. **The introspection port** — the internal interface that carries per-token distributions from three structurally different backends into one presentation model, including how a backend that cannot supply a tier declares that.
5. **State ownership** — which component owns the conversation record, the settings, the loaded model handle and the diagnostic buffer, and what the threading rules are around each.
6. **Failure and cancellation propagation** — how an interrupt reaches native inference, how a pipeline stage failure surfaces, and what the disposal order is at shutdown.
7. **The test architecture** — how a tool is tested without a backend, how the terminal surface is tested, and how native inference is exercised without a multi-gigabyte model in the loop.
8. **The 74 open design questions** recorded across the package chapters, each of which needs a decision or an explicit deferral.

Where this document states a number, a default, a range or a path, treat it as a requirement traceable to the PRD. Where it states a *preference*, it says so.

### Companion documents

| Document | Answers |
|---|---|
| `PRD-ChatDbg.md` | What the product must *do*, implementation-agnostic, with 1,354 functional requirements and their acceptance criteria |
| `TECH-STACK-AS-BUILT.md` | What the source actually used, and which of its decisions must survive a port |
| `TECH-STACK-TARGET.md` | What the rebuild should use, with 30 architecture decision records |
| **This document** | What the tool surface is |
| `synthesis/ref-{cupcake,command,loader}.md` | The three fixed frameworks, in full |
| `DECISIONS.md` | Owner decisions that bind these specifications — D-001: no fabricated telemetry, auto-disable + guidance, UI gating |

---
---

# Part I — The Host

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

---
---

# Part II — The Tool Packages

## 1. ChatDbg.Tools.SessionConversation — Session & Conversation

### Purpose and boundary

This package owns **the conversational turn and the conversation record**. Everything that adds, removes, reorders, reads, saves, restores, names or switches the record of what was said lives here, plus the one operation that makes a record grow by talking to a model: sending a turn and streaming the reply back.

Concretely, this package owns:

- **The turn** — composing a user turn, dispatching it to whatever backend is currently active, streaming the reply as it arrives, and writing both sides into the record (`CHAT SEND`, `CHAT RETRY`).
- **The record as a directly editable artifact** — append, inject at a position, pop the last turn, clear, list (`CHAT APPEND`, `CHAT INJECT`, `CHAT POP`, `CHAT CLEAR`, `CHAT LIST`).
- **The record as a file** — export to disk, import from disk, in a byte-compatible format (`CHAT EXPORT`, `CHAT IMPORT`).
- **The session** — the identity, name, and persistence of a conversation, and switching between several of them (`CHAT SESSIONS`, `CHAT USE`, `CHAT NAME`).
- **The `IConversationStore` and `IConversationBackend` contracts** (declared in the contract-only assembly `ChatDbg.Tools.Abstractions`) — the two seams every tool in this package talks through.

This package explicitly does **NOT** own:

| Not owned | Owned by | Why the line is here |
|---|---|---|
| Which provider is active, its endpoint, region, model id, local-model file, context size, GPU layers, thread count, batch size; probing whether a backend is configured | **`ChatDbg.Tools.Providers`** (root `MODEL`) — PRD 7.6, 7.7, 7.8 | `CHAT SEND` consumes an `IConversationBackend`; it never constructs an Azure client, an AWS client, or a `LLamaWeights`. Keeping the SDKs and the native `llama.cpp` binaries out of this assembly is what lets the record tools load under a Strict security policy. |
| Reading/writing settings, validation ranges, the settings file | **`ChatDbg.Tools.Settings`** (root `CONFIG`) — PRD 7.2 | This package *reads* settings from the environment context; it never writes the settings document. The per-turn `-temperature`/`-maxtokens`/`-topk` overrides here are transient and never persisted. |
| API keys, the OS keystore, environment credential resolution, credential-source reporting | **`ChatDbg.Tools.Credentials`** (root `CRED`) — PRD 7.3 | No tool in this package accepts, prints, or stores a secret. |
| System prompt authoring, storage, seeding, selection | **`ChatDbg.Tools.Prompts`** (root `PROMPT`) — PRD 7.5 | `CHAT SEND -prompt <name>` *names* a prompt; resolving that name to text is the Prompts package's job, and the resolved system prompt is never stored in the record (source behaviour: providers prepend it separately). |
| Tokenization, log-probability math, attribution, alternatives, analysis export | **`ChatDbg.Tools.TokenAnalysis`** (root `TOKEN`) — PRD 7.9, 7.10 | This package *carries* per-token log-probability payloads on messages and round-trips them through export/import. It never interprets them. |
| Heatmaps, grids, tables, colour banding, the `№` table, terminal width maths | **`ChatDbg.Tools.Render`** (root `VIEW`) — PRD 7.12 | Tools here emit chunks; the IO context and the render package decide what a chunk looks like. |
| Native log capture and daily log files | **`ChatDbg.Tools.Diagnostics`** (root `LOG`) — PRD 7.11 | |
| The REPL, the prompt string, the exit commands, dispatch, `HELP` | **the host** (`ChatDbg.Shell.Core`, the Cupcake `Loop`) — PRD 7.1, 7.13, 7.14 | The source product's hand-rolled `Dictionary<string, ICommand>` and its `/`-prefix rule are gone; dispatch is `CommandController.Run`, help is generated from attributes. |

**One boundary worth stating twice:** the source product kept exactly one conversation record per process, in a field on the shell, never autosaved and discarded at exit (chat-history rule 40). This package inverts that: the record is owned by an `IConversationStore` behind the seam, the canonical copy is a session file, and no tool holds it in a static.

---

### Package manifest

| Property | Value |
|---|---|
| Assembly name | `ChatDbg.Tools.SessionConversation.dll` |
| Package key on disk | `SessionConversation` — laid out as `{packageRoot}/SessionConversation/bin/ChatDbg.Tools.SessionConversation.dll` (the crawler's `*/bin/*.dll` mask) |
| Root command | `CHAT` (`[CommandRoot("CHAT", "Conversation and session tools")]` on every class) |
| Contract assemblies targeted | `Xcaciv.Command.Interface` **3.3.4** and `Xcaciv.Command.Core` **3.3.4** — and nothing else from the framework. Never ship a private copy of `Xcaciv.Command.Interface`: the crawler catches `ReflectionTypeLoadException` and reports exactly that cause. |
| Other references | `ChatDbg.Tools.Abstractions` (contract-only: `IConversationStore`, `IConversationBackend`, `ConversationRecord`, `ConversationTurn`, `TokenLogProbability`). No SDKs, no `HttpClient`, no P/Invoke. |
| TFM | `net10.0`, `LangVersion 14`, `Nullable` + `ImplicitUsings` enabled — matching the framework's default. A `net8.0;net10.0` multi-target is available behind the framework's `UseNet08` switch. |
| Elevated trust required | **No.** No reflection emit, no dynamic assemblies, no unsafe blocks, no unmanaged code. Loads cleanly under `AssemblySecurityPolicy.Strict` with `DisallowDynamicAssemblies = true` and `EnforceBasePathRestriction = true`. |
| Network access | **None, directly.** `CHAT SEND` / `CHAT RETRY` reach the network only through an injected `IConversationBackend`. If no backend is supplied, they fail with a stated message; nothing else in the package is affected. |
| Filesystem access | **Yes, and this is the package's one real capability.** Three roots: (1) the session store, `<LocalApplicationData>/ChatDbg/sessions/` — `%LOCALAPPDATA%\ChatDbg\sessions` on Windows, `$XDG_DATA_HOME/ChatDbg/sessions` or `~/.local/share/ChatDbg/sessions` on Linux/macOS; (2) arbitrary user-named paths for `CHAT EXPORT` / `CHAT IMPORT`, optionally confined by `CHAT_EXPORT_ROOT` / `CHAT_IMPORT_ROOT`; (3) the user home directory, read only to expand `~`. |
| OS keystore | **Never.** Delegated to `ChatDbg.Tools.Credentials`. |
| Native libraries | **None.** |
| Environment-modifying registration | **Not required.** Every value this package persists is written under a key prefixed with its own root command name, so the host registers it as `controller.AddCommand("SessionConversation", new …())` with `modifiesEnvironment` left at its default `false`. |
| Safe to load in a restricted host? | **Yes.** Under a filesystem-restricted host, `CHAT EXPORT`/`CHAT IMPORT`/`CHAT SESSIONS`/`CHAT USE` degrade to a stated failure ("session store is unavailable in this host"); `CHAT SEND`, `APPEND`, `INJECT`, `POP`, `CLEAR`, `LIST` continue to work against an in-memory store. Under a network-restricted host, only `SEND` and `RETRY` degrade. No tool in this package escalates, and none needs a confirmation dialog it cannot render. |

**Attribute conventions used throughout this package** (they are load-bearing, and two of them are workarounds for real framework traps):

1. `[CommandRoot("CHAT", …)]` is on **every** class. It must be — `AbstractCommand.RootCommand` throws `InvalidOperationException` when the attribute is absent, and a host that enumerates roots for help would trip it.
2. **`AllowedValues` silently makes its first element the default when `DefaultValue` is empty, and explicitly setting `DefaultValue = ""` alongside an allow-list makes the parser throw.** Therefore every allow-listed parameter in this package puts its **source-preserving default first in the list** and also states `DefaultValue` explicitly, and every "no override" parameter carries a sentinel first value (`inherit`) rather than an empty default.
3. Free text and paths use `[CommandParameterSuffix]`, declared **last and only once per class**, because the suffix parameter re-joins all remaining tokens with single spaces. That reproduces the source's space-collapsing behaviour exactly (chat-history rule 41) — deliberately, because file-compatibility and muscle memory both depend on it. `AllowedValues` is **not** enforced on suffix parameters; no suffix parameter in this package declares one.
4. Numeric ranges (temperature 0.0–2.0, top-K 1–20, …) **cannot** be expressed as attributes. Each range is declared in the parameter's `ValueDescription` so it appears in generated help, and enforced inside the tool, returning the source's exact rejection text as a `Failure` chunk rather than throwing.
5. Every tool that must emit more than one chunk on the non-piped path (`SEND`, `LIST`, `POP -count > 1`, `SESSIONS`, `IMPORT -emit`, `RETRY`) **overrides `Main`**, because `AbstractCommand.Main` emits exactly one chunk when `HasPipedInput` is false. Those overrides reproduce the base class's piped contract faithfully: forward failed upstream chunks verbatim, skip empty ones, call `OnStartPipe`/`OnEndPipe` around the loop.
6. Every tool guards against `ProcessParameters` returning an **empty dictionary when invoked with zero arguments** — no defaults, no flags, no field injection. `CHAT POP`, `CHAT CLEAR`, `CHAT LIST`, `CHAT SESSIONS` and `CHAT NAME` are all commonly invoked bare, so each carries sane field initialisers *and* dictionary fallbacks.

---

### Tool catalog

---

#### 1.1 `CHAT SEND` — send a conversational turn and stream the reply

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Send", "Send a turn to the active model backend and stream the reply",
    Prototype = "CHAT SEND <message...> [-provider inherit|azure|bedrock|llama] [-model <id>] " +
                "[-temperature <0.0-2.0>] [-maxtokens <1-8192>] [-logprobs inherit|on|off] " +
                "[-topk <1-20>] [-prompt <name>] [-stream on|off] [-format text|json] " +
                "[-timeout <seconds>] [-norecord] [-rollback]",
    Version = "1.0.0")]
[CommandParameterNamed("provider", "Backend for this turn only (inherit = use the configured provider)",
    DefaultValue = "inherit", AllowedValues = new[]{ "inherit", "azure", "bedrock", "llama" })]
[CommandParameterNamed("model", "Model id for this turn only; empty = the configured model")]
[CommandParameterNamed("temperature", "Sampling temperature, 0.0-2.0", DataType = typeof(double), ShortAlias = "t")]
[CommandParameterNamed("maxtokens", "Maximum reply tokens, 1-8192", DataType = typeof(int))]
[CommandParameterNamed("logprobs", "Request per-token log probabilities",
    DefaultValue = "inherit", AllowedValues = new[]{ "inherit", "on", "off" })]
[CommandParameterNamed("topk", "Alternatives per token when log probabilities are on, 1-20", DataType = typeof(int))]
[CommandParameterNamed("prompt", "System prompt name for this turn only")]
[CommandParameterNamed("stream", "Emit the reply incrementally as it arrives",
    DefaultValue = "on", AllowedValues = new[]{ "on", "off" })]
[CommandParameterNamed("format", "Output shape",
    DefaultValue = "text", AllowedValues = new[]{ "text", "json" })]
[CommandParameterNamed("timeout", "Abandon the call after N seconds; 0 = wait forever",
    DataType = typeof(int), DefaultValue = "0")]
[CommandFlag("norecord", "Do not write this turn or its reply into the conversation record")]
[CommandFlag("rollback", "Remove the user turn from the record if the backend call fails")]
[CommandParameterSuffix("message", "The text to send", IsRequired = true, UsePipe = true)]
[CommandHelpRemarks("A message that begins with '-' is not captured by the suffix parameter. Pipe it instead: SAY \"-x\" | CHAT SEND")]
[CommandHelpRemarks("Streaming requires backend support. Backends that cannot stream emit one chunk at the end; the tool does not fail.")]
public sealed class SendCommand : AbstractCommand { /* overrides Main, HandleExecution, HandlePipedChunk */ }
```

| Registration | Value |
|---|---|
| Command name | `SEND` |
| Root command | `CHAT` |
| Description | Send a turn to the active model backend and stream the reply |
| Usage prototype | `CHAT SEND <message...> [-provider inherit\|azure\|bedrock\|llama] [-model <id>] [-temperature <0.0-2.0>] [-maxtokens <1-8192>] [-logprobs inherit\|on\|off] [-topk <1-20>] [-prompt <name>] [-stream on\|off] [-format text\|json] [-timeout <seconds>] [-norecord] [-rollback]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `message` | suffix | `string` | yes (unless piped) | — | any text; remaining tokens joined with single spaces | The text to send. `UsePipe = true`, so when piped it is supplied by the upstream chunk and is not demanded on the command line. |
| `provider` | named | `string` | no | `inherit` | `inherit`, `azure`, `bedrock`, `llama` | Per-turn backend override. `inherit` reads `CHATDBG_PROVIDER`. Source default is `azure`. |
| `model` | named | `string` | no | `""` | free text | Per-turn model override. Source default `gpt-4`; for `llama` this is a path to a `.gguf` file that must exist. |
| `temperature` | named | `double` | no | *(from `CHATDBG_TEMPERATURE`, source default `0.7`)* | **0.0 – 2.0 inclusive** | Rejection text preserved: `Temperature must be a number between 0 and 2`. |
| `maxtokens` | named | `int` | no | *(from `CHATDBG_MAX_TOKENS`, source default `1000`)* | **1 – 8192 inclusive** | Rejection text preserved: `MaxTokens must be a number between 1 and 8192`. |
| `logprobs` | named | `string` | no | `inherit` | `inherit`, `on`, `off` | `inherit` reads `CHATDBG_ENABLE_LOGPROBS` (source default `false`). |
| `topk` | named | `int` | no | *(from `CHATDBG_LOGPROBS_TOPK`, source default `5`)* | **1 – 20 inclusive** | Rejection text preserved: `LogProbabilitiesTopK must be a number between 1 and 20`. |
| `prompt` | named | `string` | no | `""` | free text | **NEW.** System prompt name for this turn only; empty uses `CHATDBG_SYSTEM_PROMPT_NAME` (source default `default`). Earns its place because comparing two system prompts on the same question is the product's core use case and the source forced a persistent `/prompt use` between them. |
| `stream` | named | `string` | no | `on` | `on`, `off` | **NEW.** The source had no streaming, no spinner, no elapsed display, and blocked the prompt until the provider answered. |
| `format` | named | `string` | no | `text` | `text`, `json` | **NEW.** `json` emits one object per turn (`role`, `content`, `elapsedMs`, `logProbabilities`) and sets `ResultFormat.JSON`. |
| `timeout` | named | `int` | no | `0` | `0` (unlimited) – `86400` | **NEW.** The source has no cancellation token anywhere; a wedged local model wedges the shell. |
| `norecord` | flag | `bool` | no | `false` | presence = true | **NEW.** Ask without polluting the context under test — the product's stated purpose. |
| `rollback` | flag | `bool` | no | `false` | presence = true | **NEW.** Source behaviour (preserved as the default) leaves an orphan user turn in the record when the call fails, and documents `/pop` as the remedy. |

**Pipeline behaviour — both.** Accepts piped input: **one chunk is one complete user turn**. Each surviving chunk is sent as an independent request against the *same* record and the same overrides, and produces the reply for that chunk; the record grows by two turns per chunk unless `-norecord`. This turns `CHAT SEND` into a batch prompt runner. Produces piped output: with `-stream on` and a streaming backend, one chunk per arriving segment plus a terminal chunk carrying elapsed time; with `-stream off`, exactly one chunk per turn. Declares `ResultFormat.General` for `-format text` and `ResultFormat.JSON` for `-format json`, because downstream token tools need to know whether they are parsing prose or a turn object. Because streaming needs many chunks on the non-piped path, this tool **overrides `Main`**.

**Environment interaction.** Reads (always with `storeDefault: false`, so that a mere read does not flip `HasChanged` and cause a write-back): `CHATDBG_PROVIDER`, `CHATDBG_MODEL_ID`, `CHATDBG_TEMPERATURE`, `CHATDBG_MAX_TOKENS`, `CHATDBG_ENABLE_LOGPROBS`, `CHATDBG_LOGPROBS_TOPK`, `CHATDBG_SYSTEM_PROMPT_NAME`, and `CHAT_SESSION` (this package's own bucket). Writes, into its own bucket only: `CHAT_LAST_ELAPSED_MS`, `CHAT_LAST_TURN_ROLE`, `CHAT_TURN_COUNT`. **Does not need environment-modifying permission** — every written key carries the `CHAT_` root prefix, so the host routes it into this package's private bucket without `ModifiesEnvironment = true`. Declares in `GetDefaultEnvironment()`: `{ "SESSION", "default" }`, `{ "TURN_COUNT", "0" }` — the host seeds these as `CHAT_SESSION` / `CHAT_TURN_COUNT`.

**Failure modes.**

| Condition | User sees |
|---|---|
| No message and no pipe | `ArgumentException("Missing required parameter message")` is raised inside parameter processing and reduced by the executor to `Error executing CHAT (see trace for more info)`; the specific text reaches the trace only. Mitigation: `ValueDescription` and `Prototype` both spell the requirement, and `CommandHelpRemarks` names the pipe alternative. |
| Message begins with `-` | The suffix parameter takes its default (empty), so the turn is refused with `CHAT SEND received no message. A message beginning with '-' must be piped: SAY "…" \| CHAT SEND`. |
| Dangling named parameter (`… -temperature` at end of line) | Framework raises `ArgumentOutOfRangeException` during parsing; surfaces as the generic executor failure chunk. Documented in help remarks. |
| `-temperature` / `-maxtokens` / `-topk` out of range | `Failure` chunk carrying the source's exact rejection string. Nothing is sent; nothing is recorded. |
| `-provider` names a backend the host has no adapter for | `Failure`: `Unknown AI provider: {name}` — source text preserved. |
| Backend present but not configured | `Failure`: `{Provider} service is not configured. Use environment variables or the OS credential store to configure credentials securely.` followed by `Type 'CRED STATUS' to see current configuration and setup instructions.` — the source's two-line advisory, retargeted at the Credentials package. |
| **No backend at all** (package crawled into a restricted host with no `IConversationBackend` registered) | `Failure`: `No conversation backend is available in this host. Record tools (CHAT LIST/INJECT/POP/CLEAR/IMPORT/EXPORT) still work.` This is the package's principal degrade-don't-fail path. |
| Backend returns an envelope with no text | The literal `Error: Response text expected, none received.` is recorded as the assistant turn **and** the envelope's own error message is emitted as a second failure chunk. *(Deliberate improvement: the source spelled this placeholder four different ways across four code paths, and the console shell silently dropped the envelope's error message and elapsed time.)* |
| Log probabilities requested but none returned | Two-line note, source text preserved: `Note: Log probabilities were requested but none were returned by the model.` / `This could be due to the model not supporting this feature or an API limitation.` The turn is still recorded. |
| Backend throws | `Failure(message, exception)`. The user turn remains in the record unless `-rollback` was given. Nothing is written to `Console` — the source wrote diagnostics straight to stdout, which painted over the full-screen shell. |
| `-timeout` elapses | `Failure`: `CHAT SEND timed out after {n}s`. The user turn remains (or is removed under `-rollback`). |
| Downstream error arriving through the pipe | A failed upstream chunk is forwarded verbatim and **no request is made for it** — reproducing `AbstractCommand.Main`'s contract inside the `Main` override. Empty upstream chunks are skipped. A failure originating downstream cannot reach this stage at all; the pipeline is one-directional. |
| Stage timeout configured on the host's `PipelineConfiguration` | The stage is cancelled, a *status message* (not an output chunk) reads `Stage 'CHAT' exceeded timeout of {n} seconds`. Any in-flight turn is not recorded. |

**Security and audit.** No parameter and no output carries a secret **by construction** — there is no credential parameter, and the backend resolves credentials itself. But the `message` suffix and the reply routinely contain the user's source code and stack traces, and the framework emits exactly one `AuditEvent` per execution carrying `Parameters = ioContext.Parameters` verbatim. **The shipped `AuditMaskingConfiguration.ApplyMasking` only rewrites `-name=value` tokens and is effectively non-functional for this framework's space-separated syntax**, so the host MUST supply its own `IAuditMaskingConfiguration` that redacts the whole tail of a `CHAT SEND` invocation, or configure the audit logger to record parameter *count* rather than parameter *values* for this root. This is a host-level obligation stated here because this tool is the reason it exists. Not destructive and not irreversible in the local sense — but it spends money and leaves a record on a third-party service, so `-norecord` is deliberately about the local record only and says so in its help. No confirmation prompt: a chat turn is the shell's ordinary case.

**Traceability.** PRD **7.6 AI Provider Abstraction & Hosted OpenAI** (primary), with **7.4 Chat History**, **7.9 Token Probability Analysis** (`-logprobs`, `-topk`), **7.5 System Prompt Management** (`-prompt`), **7.13 Line-Oriented Shell**. Source ancestor: the console shell's chat-turn path — any typed line not beginning with `/`, handled by `ChatShell.SendMessageAsync` (`src/ChatDbg/ChatShell.cs:343-410`) and its GUI twin (`ChatWindow.cs:420-497`).

---

#### 1.2 `CHAT APPEND` — append a fabricated turn to the end of the record

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Append", "Append a message to the end of the conversation record",
    Prototype = "CHAT APPEND <role> <content...> [-command] [-quiet]")]
[CommandParameterOrdered("role", "Message role",
    DefaultValue = "user", AllowedValues = new[]{ "user", "assistant", "system" })]
[CommandFlag("command", "Mark the message as a command so backends exclude it from every request")]
[CommandFlag("quiet", "Emit nothing on success")]
[CommandParameterSuffix("content", "Message text", IsRequired = false, UsePipe = true)]
public sealed class AppendCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `APPEND` |
| Root command | `CHAT` |
| Description | Append a message to the end of the conversation record |
| Usage prototype | `CHAT APPEND <role> <content...> [-command] [-quiet]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `role` | ordered | `string` | **yes** (ordered params default `IsRequired = true`) | `user` | `user`, `assistant`, `system` | Lower-cased before validation and before storage, so `User`, `USER`, `user` all store as `user`. Rejection text preserved: `Role must be one of: user, assistant, system`. |
| `content` | suffix | `string` | no | `""` | any text | An empty message is accepted — the source accepted one too (only reachable there through the GUI dialog). |
| `command` | flag | `bool` | no | `false` | presence = true | **NEW.** Sets the record's `isCommand` flag. The flag exists in the file format and every hosted backend honours it by excluding the message from requests, but **nothing in the source ever set it** — it was a dormant hook reachable only by hand-editing an exported file. Earns its place: a note-to-self turn the model never sees is genuinely useful when debugging a prompt. |
| `quiet` | flag | `bool` | no | `false` | presence = true | **NEW.** Keeps batch appends from flooding the transcript. |

**Pipeline behaviour — both.** Accepts piped input: **one chunk is the content of one message**, appended with the `role` given on the command line (`content` carries `UsePipe = true`, so it is not demanded when piped). Produces one confirmation chunk per append, or nothing under `-quiet` — an empty success chunk is dropped by the host, which is the framework's idiomatic way to stay silent. `ResultFormat.General`.

**Environment interaction.** Reads `CHAT_SESSION` (`storeDefault: false`). Writes `CHAT_TURN_COUNT` into its own bucket. No environment-modifying permission needed.

**Failure modes.** Role outside the whitelist → `Failure` with the preserved text. Zero arguments → the framework's early return produces an empty parameter dictionary; the tool detects the missing ordered parameter itself and returns `Failure`: `Usage: CHAT APPEND <role> <content...>`. Store unavailable → `Failure`: `Conversation record is unavailable in this host.` Failed upstream chunks forward verbatim; empty ones are skipped by `AbstractCommand.Main` before `HandlePipedChunk` is reached.

**Security and audit.** No secrets. The `content` tail has the same audit-masking obligation as `CHAT SEND`. Not destructive — append is additive and the record is not truncated.

**Traceability.** PRD **7.4 Chat History**. Source ancestor: `ChatHistory.AddMessage(role, content, isCommand, logProbabilities)` (`Models/ChatHistory.cs:14-24`) — a real product operation that had no user-facing command; the shells called it directly. Surfacing it as a tool is what makes the record composable in a pipeline.

---

#### 1.3 `CHAT INJECT` — insert a fabricated turn at a position

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Inject", "Insert a message into the conversation record at a position",
    Prototype = "CHAT INJECT <role> <message...> [-position <n>] [-strict] [-quiet]")]
[CommandParameterOrdered("role", "Message role",
    DefaultValue = "user", AllowedValues = new[]{ "user", "assistant", "system" })]
[CommandParameterNamed("position", "Zero-based insertion index; -1 appends",
    DataType = typeof(int), DefaultValue = "-1", ShortAlias = "p")]
[CommandFlag("strict", "Fail instead of appending when the position is out of range")]
[CommandFlag("quiet", "Emit nothing on success")]
[CommandParameterSuffix("message", "Message text", IsRequired = false, UsePipe = true)]
[CommandHelpRemarks("Piped injections keep their upstream order: chunk n is inserted at position + n.")]
public sealed class InjectCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `INJECT` |
| Root command | `CHAT` |
| Description | Insert a message into the conversation record at a position |
| Usage prototype | `CHAT INJECT <role> <message...> [-position <n>] [-strict] [-quiet]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `role` | ordered | `string` | yes | `user` | `user`, `assistant`, `system` | Lower-cased first. Rejection text preserved: `Role must be one of: user, assistant, system`. |
| `message` | suffix | `string` | no | `""` | any text | Empty accepted (source accepted it). |
| `position` | named | `int` | no | `-1` | `-1` (append) or **`0 ≤ position < current message count`**; anything else appends | **Changed shape, same semantics.** The source read the position from the *last positional token*, and only when there were strictly more than two arguments — so `/inject user 42` injected the text `42` while `/inject user 42 7` injected `42` at 7, and an unparseable trailing token silently became part of the message. That is a genuine trap and is **deliberately not reproduced**; the value range and the out-of-range-appends rule are preserved exactly. |
| `strict` | flag | `bool` | no | `false` | presence = true | **NEW.** With `-strict`, an out-of-range position is a `Failure` instead of a silent append. Earns its place: the source reported `at position 99` after appending at index 2 — it told the user something that did not happen. |
| `quiet` | flag | `bool` | no | `false` | presence = true | **NEW.** |

**Success text.** `Injected {role} message at position {actualIndex}: {message}` — with ` at position …` omitted entirely when no position was requested, exactly as the source did. *(Deliberate correction: the index reported is the index actually used. When it differs from the requested one the chunk reads `Injected {role} message at position {actual} (requested {requested}, appended at end): {message}`.)*

**Pipeline behaviour — both.** Accepts piped input: **one chunk is the text of one message**. `OnStartPipe` resets an ordinal; chunk *n* is inserted at `position + n`, so a piped sequence lands in upstream order rather than reversed. With `position = -1` every chunk appends, which is the same thing. Produces one confirmation chunk per injection. `ResultFormat.General`.

**Environment interaction.** Reads `CHAT_SESSION`. Writes `CHAT_TURN_COUNT`. No environment-modifying permission needed.

**Failure modes.** Fewer than the required arguments → `Failure`: `Usage: CHAT INJECT <role> <message...> [-position <n>]` (source text, retargeted). Bad role → preserved rejection text; record untouched. `-position` given a non-integer → the framework marks the parameter invalid and the tool falls back to `-1` (append) with a warning chunk, rather than folding the token into the message. `-strict` with an out-of-range position → `Failure`: `Position {n} is outside the valid range 0..{count-1}`. Store unavailable → stated failure. Failed upstream chunks forward verbatim.

**Security and audit.** No secrets. Injection **fabricates provenance**: an injected `assistant` turn is indistinguishable from a real one in the record and is sent to the backend as context on the next turn. That is the whole point of the feature, but it means an exported record is not evidence of what a model said. The audit event records the role and the fact of injection; the host's masking configuration should treat the message tail as it treats `CHAT SEND`. Not destructive — nothing is removed.

**Traceability.** PRD **7.4 Chat History**. Source ancestor: `/inject <role> <message> [position]` (`Commands/InjectCommand.cs`, `Models/ChatHistory.cs:34-51`) and the GUI's 70×15 "Inject Message" dialog.

---

#### 1.4 `CHAT POP` — remove the last turn (or the last N)

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Pop", "Remove the last message from the conversation record",
    Prototype = "CHAT POP [-count <n>] [-preview <chars>] [-format text|json] [-yes] [-quiet]")]
[CommandParameterNamed("count", "How many messages to remove from the end, 1-1000",
    DataType = typeof(int), DefaultValue = "1", ShortAlias = "n")]
[CommandParameterNamed("preview", "Characters of removed content to echo back, 0-500",
    DataType = typeof(int), DefaultValue = "50")]
[CommandParameterNamed("format", "Output shape",
    DefaultValue = "text", AllowedValues = new[]{ "text", "json" })]
[CommandFlag("yes", "Skip the confirmation prompt", ShortAlias = "y")]
[CommandFlag("quiet", "Emit nothing on success")]
[CommandHelpRemarks("Removed messages are emitted as output chunks, so a pop can be piped to CHAT EXPORT before it is lost.")]
public sealed class PopCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `POP` |
| Root command | `CHAT` |
| Description | Remove the last message from the conversation record |
| Usage prototype | `CHAT POP [-count <n>] [-preview <chars>] [-format text\|json] [-yes] [-quiet]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `count` | named | `int` | no | `1` | **1 – 1000** | **NEW.** The source ignored all arguments, so `/pop 3` removed exactly one message. Default 1 preserves that. |
| `preview` | named | `int` | no | **`50`** | 0 – 500 | The source's magic number: 50 characters of the removed content echoed in the confirmation. Preserved as the default. |
| `format` | named | `string` | no | `text` | `text`, `json` | **NEW.** `json` emits the full removed message (role, content, timestamp, isCommand, logProbabilities) as one object per pop, so a pop is recoverable. Sets `ResultFormat.JSON`. |
| `yes` | flag | `bool` | no | `false` | presence = true | **NEW.** Required when `count > 1` in a non-interactive or piped context; see below. |
| `quiet` | flag | `bool` | no | `false` | presence = true | **NEW.** |

**Success text.** `Removed last message: [{role}] {preview}` — with the literal `...` appended **only when truncation actually occurred**, and the cut taken on **text-element (grapheme) boundaries**. *(Two deliberate corrections: the source appended `...` unconditionally, so a two-character message popped as `Removed last message: [user] hi...` and an empty one as `Removed last message: [user] ...`; and it sliced at 50 UTF-16 code units, so a surrogate pair or combining sequence straddling offset 50 was cut in half.)* With `count > 1` the chunks are emitted newest-first and a final chunk reads `Removed {n} messages from the conversation record`.

**Pipeline behaviour — output only.** Does **not** accept piped input; `HandlePipedChunk` returns the explanatory string `Unsupported pop method for {chunk} (piped)` rather than throwing, following the framework's own convention for pipe-incapable tools. Produces piped output: one chunk per removed message, so `CHAT POP -count 3 -format json | CHAT EXPORT ~/discarded.json` saves what is about to be lost. Because `count > 1` needs many chunks on the non-piped path, this tool **overrides `Main`**.

**Environment interaction.** Reads `CHAT_SESSION`. Writes `CHAT_TURN_COUNT`. No environment-modifying permission needed.

**Failure modes.** Empty record → `Failure`: `Chat history is empty` (source text preserved). `count` out of range → `Failure`: `Count must be a number between 1 and 1000`. `count` greater than the record length → removes everything present and reports the actual number removed, no error. Store unavailable → stated failure. Downstream errors cannot reach this stage.

**Security and audit.** No secrets, but the emitted chunks contain the removed conversation content, so `-format json` output inherits the same masking obligation. **Destructive and irreversible in the record** — this is the tool's whole job. Confirmation policy: with `count == 1` no confirmation (matching the source, where `/pop` was the documented one-keystroke remedy for a failed turn); with `count > 1` the tool calls `IIoContext.PromptForCommand("Remove {n} messages? (y/N) ")` and accepts `y`/`yes` case-insensitively, matching every other confirmation in the source product. **When `HasPipedInput` is true a prompt is meaningless** — the contract says `PromptForCommand` is only defined for a non-piped context — so in a pipeline `count > 1` without `-yes` is refused with `Refusing to remove {n} messages inside a pipeline without -yes`.

**Traceability.** PRD **7.4 Chat History**. Source ancestor: `/pop` (`Commands/PopCommand.cs`, `Models/ChatHistory.cs:26-32`) and GUI *File ▸ Pop Last Message*.

---

#### 1.5 `CHAT CLEAR` — empty the record

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Clear", "Clear the conversation record",
    Prototype = "CHAT CLEAR [-keep none|system] [-newsession] [-backup <path...>] [-yes]")]
[CommandParameterNamed("keep", "Roles to preserve while clearing",
    DefaultValue = "none", AllowedValues = new[]{ "none", "system" })]
[CommandFlag("newsession", "Also mint a new session identifier and creation timestamp")]
[CommandFlag("yes", "Skip the confirmation prompt", ShortAlias = "y")]
[CommandParameterSuffix("backup", "Write the record to this path before clearing", IsRequired = false)]
public sealed class ClearCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `CLEAR` |
| Root command | `CHAT` |
| Description | Clear the conversation record |
| Usage prototype | `CHAT CLEAR [-keep none\|system] [-newsession] [-backup <path...>] [-yes]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `keep` | named | `string` | no | `none` | `none`, `system` | **NEW.** `system` preserves messages whose lower-cased role is `system`, in order. Earns its place: the common debugging loop is "reset the conversation but keep the seeded system turn", which the source could only do by re-injecting by hand. |
| `newsession` | flag | `bool` | no | `false` | presence = true | **NEW.** Source behaviour, preserved as the default: clearing resets **only** the message list — the session identifier and the creation timestamp survive. |
| `backup` | suffix | `string` | no | `""` | a file path; `~` expanded | **NEW.** Writes the record through the same code path as `CHAT EXPORT` before emptying it. Earns its place: clear is the one operation with no undo and no prior write to disk. |
| `yes` | flag | `bool` | no | `false` | presence = true | **NEW.** |

**Success text.** `Cleared {n} messages from chat history` — with `n` captured **before** the clear, and `Cleared 0 messages from chat history` returned as a **success** on an empty record, exactly as the source did. With `-keep system` the chunk reads `Cleared {n} messages from chat history ({k} system messages kept)`.

**Pipeline behaviour — neither.** No piped input (`HandlePipedChunk` returns `Unsupported clear method for {chunk} (piped)`), one output chunk. `ResultFormat.General`. A destructive whole-record operation has no sensible per-chunk meaning.

**Environment interaction.** Reads `CHAT_SESSION`. Writes `CHAT_TURN_COUNT` (to `0`) and, under `-newsession`, `CHAT_SESSION_ID`. No environment-modifying permission needed.

**Failure modes.** Empty record → **success**, not an error (preserved). `-backup` given but unwritable → `Failure` carrying the underlying reason **and the record is not cleared**; backup failure is fail-closed. Store unavailable → stated failure.

**Security and audit.** No secrets. **Destructive and irreversible; requires confirmation.** The tool prompts `Clear {n} messages? (y/N) ` unless `-yes` or `-backup` was given, and refuses outright inside a pipeline without `-yes`. This is a deliberate departure: the source had *no* confirmation anywhere, and the GUI placed *Clear History* directly beneath *Pop Last Message* in the same menu, one keystroke apart.

**Traceability.** PRD **7.4 Chat History**. Source ancestor: `/clear` (`Commands/ClearCommand.cs`, `Models/ChatHistory.cs:53-56`) and GUI *File ▸ Clear History*.

---

#### 1.6 `CHAT LIST` — emit the record as chunks *(NEW)*

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("List", "Emit the conversation record, one message per chunk",
    Prototype = "CHAT LIST [-role any|user|assistant|system] [-from <n>] [-count <n>] " +
                "[-format text|json|csv] [-index] [-logprobs] [-last]")]
[CommandParameterNamed("role", "Only emit messages with this role",
    DefaultValue = "any", AllowedValues = new[]{ "any", "user", "assistant", "system" })]
[CommandParameterNamed("from", "Zero-based index of the first message to emit",
    DataType = typeof(int), DefaultValue = "0")]
[CommandParameterNamed("count", "How many messages to emit; 0 = all remaining",
    DataType = typeof(int), DefaultValue = "0")]
[CommandParameterNamed("format", "Output shape",
    DefaultValue = "text", AllowedValues = new[]{ "text", "json", "csv" })]
[CommandFlag("index", "Prefix each chunk with its zero-based index")]
[CommandFlag("logprobs", "Include per-token log probabilities (json only)")]
[CommandFlag("last", "Emit only the final matching message")]
public sealed class ListCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `LIST` |
| Root command | `CHAT` |
| Description | Emit the conversation record, one message per chunk |
| Usage prototype | `CHAT LIST [-role any\|user\|assistant\|system] [-from <n>] [-count <n>] [-format text\|json\|csv] [-index] [-logprobs] [-last]` |

**Why it is NEW and why it earns its place.** The source product had **no way at all** to see the conversation record in the plain-console shell — the transcript scrolled past and was gone; only the full-screen shell re-rendered it, and only for human eyes. A pipeline needs a *source*, and every downstream capability in the rebuild (token analysis, filtered export, re-running a prompt set against a different model, feeding `REGIF`) needs turns as discrete chunks. `CHAT LIST` is the single tool that makes this package composable.

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `role` | named | `string` | no | `any` | `any`, `user`, `assistant`, `system` | Compared **case-insensitively** against the stored role. *(Deliberate correction: the source's GUI compared the stored role to the literal `assistant` case-sensitively in six places, so an imported message stored as `Assistant` was invisible to the token panel while still rendering normally.)* |
| `from` | named | `int` | no | `0` | 0 – record length | Out of range yields no chunks and a success. |
| `count` | named | `int` | no | `0` | 0 (all remaining) – 100000 | |
| `format` | named | `string` | no | `text` | `text`, `json`, `csv` | `text` → `[{role}] {content}`; `json` → one message object per chunk with the exported key names; `csv` → `index,role,timestamp,isCommand,content`. Sets `ResultFormat.General` / `.JSON` / `.CSV` respectively, so downstream tools can branch on the format rather than sniffing. |
| `index` | flag | `bool` | no | `false` | presence = true | Zero-based, matching `-position` on `CHAT INJECT`. Note the source's *rendered* indices in the probability views were 1-based and absolute; those belong to the Render package, not here. |
| `logprobs` | flag | `bool` | no | `false` | presence = true | Ignored for `text` and `csv`. |
| `last` | flag | `bool` | no | `false` | presence = true | Emits the final message matching `-role`. `CHAT LIST -role assistant -last -format json` is the canonical feed for the token tools. |

**Pipeline behaviour — output only.** Refuses piped input with `Unsupported list method for {chunk} (piped)`. Produces one chunk per message — a true pipeline source. **Overrides `Main`**, because the non-piped path of `AbstractCommand` emits exactly one chunk. Declares a non-`General` output format for `json` and `csv` because those shapes are contracts other packages parse.

**Environment interaction.** Reads `CHAT_SESSION` only. Writes nothing. No environment-modifying permission needed.

**Failure modes.** Empty record → success with **zero chunks** (the host drops empty successes), not an error. `from`/`count` non-numeric → parameter marked invalid, tool falls back to the declared default and emits a warning chunk. Store unavailable → stated failure. Zero arguments is the common case and is explicitly handled: the empty parameter dictionary maps to "all messages, text format".

**Security and audit.** No secrets in parameters. **Output is the whole conversation**, so a `CHAT LIST` in a shell whose output is being logged copies the transcript into that log. This is the tool the host's audit configuration should be least worried about at the *parameter* level and most careful about at the *sink* level. Not destructive; read-only.

**Traceability.** **NEW** — no direct ancestor. Nearest source behaviour: the GUI shell's `RefreshChatHistory` rendering loop (`ChatWindow.cs:500-620`), which produced the same information for the screen only. PRD **7.4 Chat History**, with **7.12 Output Rendering** for `-format`.

---

#### 1.7 `CHAT EXPORT` — write the record to a file

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Export", "Export the conversation record to a file",
    Prototype = "CHAT EXPORT <file_path...> [-format json|jsonl|md|text] [-noclobber] [-quiet]")]
[CommandParameterNamed("format", "File format",
    DefaultValue = "json", AllowedValues = new[]{ "json", "jsonl", "md", "text" })]
[CommandFlag("noclobber", "Fail instead of overwriting an existing file")]
[CommandFlag("quiet", "Emit nothing on success")]
[CommandParameterSuffix("path", "Destination path; a leading ~ is expanded", IsRequired = true, UsePipe = false)]
[CommandHelpRemarks("Runs of consecutive spaces in a path collapse to one - quote-stripping happens before argument tokenization.")]
[CommandHelpRemarks("Set CHAT_EXPORT_ROOT to confine exports to one directory tree.")]
public sealed class ExportCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `EXPORT` |
| Root command | `CHAT` |
| Description | Export the conversation record to a file |
| Usage prototype | `CHAT EXPORT <file_path...> [-format json\|jsonl\|md\|text] [-noclobber] [-quiet]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `path` | suffix | `string` | **yes** | — | any path; leading `~/`, `~\` or bare `~` expanded to the OS home directory | Remaining tokens joined with single spaces — the source's behaviour, preserved, quirk included. |
| `format` | named | `string` | no | `json` | `json`, `jsonl`, `md`, `text` | `json` is **byte-compatible with the source** (see below). `jsonl`/`md`/`text` are **NEW** and earn their place because a Markdown transcript is what people actually paste into an issue, and JSON Lines streams. |
| `noclobber` | flag | `bool` | no | `false` | presence = true | **NEW.** Source overwrote silently with no warning; that stays the default so re-exporting to the same path keeps working. |
| `quiet` | flag | `bool` | no | `false` | presence = true | **NEW.** |

**Path handling — preserved exactly unless marked.**
- Leading `~/` expands to the OS home (`%USERPROFILE%` on Windows, `$HOME` on Unix). *(Deliberate improvement: `~\` and a bare `~` now expand too. The source expanded only the literal two characters `~/`, so `~\chats\a.json` on Windows was written to a directory literally named `~`.)*
- **The default extension is appended only when the resolved file name has no extension at all** — so `notes.txt` stays `notes.txt` and is written as JSON into a `.txt` file, and `.history` counts as already having an extension and gets no suffix. Preserved. The appended extension now follows `-format`: `.json`, `.jsonl`, `.md`, `.txt`.
- Missing parent directories are created. Preserved.
- **Writes are atomic**: temp file in the destination directory, `fsync`, then rename over the target. *(Deliberate improvement: the source overwrote in place with a whole-file write, so an interrupted export left a truncated, unparseable file where a valid history used to be.)*
- **Optional containment**: when the environment value `CHAT_EXPORT_ROOT` is non-empty, the fully-resolved destination must be inside it — checked component-wise with a relative-path test and after resolving symlinks, never with a string `StartsWith`. Outside → `Failure`. Unset (the default) reproduces the source's total absence of path restrictions.

**`-format json` file format — must match the source byte for byte.** Top-level keys in declaration order `messages`, `sessionId`, `createdAt`; per message `role`, `content`, `timestamp`, `isCommand`, `logProbabilities`; per token entry `token`, `logprob`, `top_alternatives` (note the snake_case outliers — they are load-bearing). Pretty-printed with **2-space** indentation. Timestamps ISO-8601 UTC round-trip with exactly **seven** fractional digits and a `Z` suffix. `null` written literally for absent lists; keys never omitted. Escaping is the **aggressive** profile: apostrophe becomes `\u0027`, backtick becomes `\u0060`, plus becomes `\u002B`, `<`/`>`/`&` are escaped, and all non-ASCII is escaped as `\uXXXX`. Numbers in shortest round-trip form (`-5`, not `-5.0`). UTF-8, no BOM. There is no schema version field and no format negotiation.

**Pipeline behaviour — both.** Accepts piped input: **one chunk is one message record in the `-format` shape** (the shape `CHAT LIST -format json` emits). When piped, the tool writes *those* messages instead of the live record, using `OnStartPipe` to open the temp file and `OnEndPipe` to close and rename it — so `CHAT LIST -format json | REGIF … | CHAT EXPORT ~/subset.json` exports a filtered subset. A chunk that does not parse in the declared format is reported as a failure chunk and skipped; the file is still written from the chunks that did parse, and the summary names the skip count. Produces one summary chunk. `ResultFormat.General`.

**Environment interaction.** Reads `CHAT_SESSION`, `CHAT_EXPORT_ROOT` (both `storeDefault: false`). Writes `CHAT_LAST_EXPORT_PATH` into its own bucket. Declares `{ "EXPORT_ROOT", "" }` in `GetDefaultEnvironment()`. No environment-modifying permission needed.

**Failure modes.**

| Condition | User sees |
|---|---|
| No path | `Failure`: `Usage: CHAT EXPORT <file_path>` (source text, retargeted). |
| Permission denied, invalid path characters, path too long, disk full, directory creation refused | `Failure`: `Failed to export chat history to: {resolved path}` **followed by the underlying reason**, with the exception attached to the result. *(Deliberate correction: the source swallowed the exception, printed the reason to stdout — which painted over the full-screen shell — and handed the user a cause-free message, so a full disk and a permission denial were indistinguishable.)* No tool in this package writes to `Console`; diagnostics go to `IIoContext.AddTraceMessage`. |
| `-noclobber` and the file exists | `Failure`: `Refusing to overwrite existing file: {path} (omit -noclobber to replace it)`. |
| Outside `CHAT_EXPORT_ROOT` | `Failure`: `Path {path} is outside the configured export root {root}`. |
| Interrupted mid-write | The previous file is intact; the temp file is orphaned and cleaned up on the next export to the same directory. |
| Unparseable piped chunk | One failure chunk naming the chunk's correlation id; the export continues. |

**Security and audit.** The `path` parameter is not a secret, but the **file content is the entire conversation**, which routinely contains pasted code, tokens and internal URLs. Two consequences: (1) the file inherits the process umask and is written with owner-only permissions where the platform supports it; (2) the audit event records the resolved path and the message count, never the content. **Destructive**: it silently overwrites an existing file, which is the source's behaviour and is preserved deliberately so that re-exporting a working session keeps working — `-noclobber` is the opt-in guard, and the atomic write means the destructive step is now all-or-nothing rather than a truncation. No confirmation prompt, for the same reason.

**Traceability.** PRD **7.4 Chat History**. Source ancestor: `/export <file_path>` (`Commands/ExportCommand.cs`, `Services/ChatHistoryService.cs:29-43`) and GUI *File ▸ Export History…* (save dialog pre-filled `{home}/chat_history.json`).

---

#### 1.8 `CHAT IMPORT` — load a record from a file

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Import", "Import a conversation record from a file",
    Prototype = "CHAT IMPORT <file_path...> [-mode replace|append] [-validate warn|off|strict] " +
                "[-maxbytes <n>] [-emit] [-quiet]")]
[CommandParameterNamed("mode", "Replace the record or append to it",
    DefaultValue = "replace", AllowedValues = new[]{ "replace", "append" })]
[CommandParameterNamed("validate", "How to treat messages whose role is outside user|assistant|system",
    DefaultValue = "warn", AllowedValues = new[]{ "warn", "off", "strict" })]
[CommandParameterNamed("maxbytes", "Refuse files larger than this; 0 = unlimited",
    DataType = typeof(long), DefaultValue = "33554432")]
[CommandFlag("emit", "Emit one chunk per imported message instead of a summary")]
[CommandFlag("quiet", "Emit nothing on success")]
[CommandParameterSuffix("path", "Source path; a leading ~ is expanded", IsRequired = true, UsePipe = true)]
public sealed class ImportCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `IMPORT` |
| Root command | `CHAT` |
| Description | Import a conversation record from a file |
| Usage prototype | `CHAT IMPORT <file_path...> [-mode replace\|append] [-validate warn\|off\|strict] [-maxbytes <n>] [-emit] [-quiet]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `path` | suffix | `string` | yes (unless piped) | — | any path; leading `~` expanded | `UsePipe = true`. **No extension is defaulted on import** — preserved, including the asymmetry that makes `CHAT EXPORT mychats` (writes `mychats.json`) followed by `CHAT IMPORT mychats` fail. *(Small correction: on a miss, if `{path}.json` exists the failure adds `(did you mean {path}.json?)`.)* |
| `mode` | named | `string` | no | `replace` | `replace`, `append` | `replace` is the source's only behaviour — it empties the message list and refills it in file order, and **mutates the live record in place rather than swapping it**, which matters because every tool and both UIs hold the same reference. `append` is **NEW** and earns its place because merging a saved probe into a live session is otherwise impossible. |
| `validate` | named | `string` | no | `warn` | `warn`, `off`, `strict` | **NEW default of `warn` is a deliberate improvement.** The source performed **zero** validation on import: roles were not checked against the whitelist `/inject` enforced, content was unbounded, timestamps unchecked, and the `isCommand` flag honoured as read — so a file could introduce a role that hosted backends variously drop, forward to the remote service, or coerce to `user`. `off` reproduces the source exactly; `warn` imports but emits one trace note per offending message; `strict` refuses the file. |
| `maxbytes` | named | `long` | no | **`33554432`** (32 MiB) | 0 (unlimited) – 1073741824 | **NEW.** The source read the whole file into a string before parsing with no size check, no streaming and no cap. |
| `emit` | flag | `bool` | no | `false` | presence = true | **NEW.** One chunk per imported message, so an import can feed a pipeline directly. |
| `quiet` | flag | `bool` | no | `false` | presence = true | **NEW.** |

**Preserved semantics.** The imported **session identifier overwrites** the live one. Messages arrive in file order. Unknown keys are ignored; key matching is **case-sensitive**. Comments and trailing commas are rejected. A JSON object with no `messages` key yields an empty list and replaces the live session id with a freshly generated one, reported as `Successfully imported 0 messages`. *(Deliberate correction: the imported `createdAt` is now restored instead of silently discarded. The source copied the session id on one line and never copied the creation timestamp anywhere, so a re-exported file carried the importing process's start time — a silent rewrite of history metadata.)*

**Success text.** `Successfully imported {n} messages from: {resolved path}`. Failure text: `Failed to import chat history from: {resolved path}` **plus the underlying reason** (source correction, as for export).

**Pipeline behaviour — both.** Accepts piped input: **one chunk is one file path**. Each path is imported in order; with `-mode replace` the last file wins and the tool emits a warning chunk on the second and subsequent paths (`Each replace discards the previous import; use -mode append to merge`), with `-mode append` they concatenate. Produces one summary chunk per path, or one chunk per message under `-emit`. `ResultFormat.General`, or `.JSON` under `-emit`. Because `-emit` needs many chunks on the non-piped path, this tool **overrides `Main`**.

**Environment interaction.** Reads `CHAT_SESSION`, `CHAT_IMPORT_ROOT` (`storeDefault: false`). Writes `CHAT_LAST_IMPORT_PATH`, `CHAT_TURN_COUNT`, `CHAT_SESSION_ID`. Declares `{ "IMPORT_ROOT", "" }`. No environment-modifying permission needed.

**Failure modes.** No path → `Failure`: `Usage: CHAT IMPORT <file_path>`. Missing file → `Failure`: `File not found: {path}` (source text, now delivered as a chunk rather than printed to stdout), plus the `.json` hint. Malformed JSON → `Failure`: `Error importing chat history: {parser reason}` (source text) — **the live record is left completely untouched**, preserved. A file whose content is the literal `null` takes the same path as a parse error. File larger than `maxbytes` → `Failure`: `File exceeds the {n}-byte import limit; raise -maxbytes to override`. `-validate strict` with a bad role → `Failure` naming the offending index and role; nothing imported. Outside `CHAT_IMPORT_ROOT` → stated failure. Failed upstream chunks forward verbatim.

**Security and audit.** No secrets in parameters. **This is the package's principal untrusted-input surface**: an imported file is attacker-controllable content that becomes model context on the next turn (a prompt-injection vector), can carry a role that bypasses the whitelist, and can set the `isCommand` flag to hide a message from the model while leaving it in the transcript. `-validate warn` and `-maxbytes` are the mitigations that ship on by default; `CHAT_IMPORT_ROOT` is the mitigation an operator can add. **Destructive** in `-mode replace` — it discards the entire live record with no undo. Confirmation policy: when the live record is non-empty and `-mode replace` is in effect, the tool prompts `Replace {n} messages with the contents of {file}? (y/N) ` unless `-yes`-equivalent is implied by a non-interactive context, in which case it proceeds (matching the source, which never asked) but emits a warning chunk naming the discarded count. Inside a pipeline it never prompts.

**Traceability.** PRD **7.4 Chat History**. Source ancestor: `/import <file_path>` (`Commands/ImportCommand.cs`, `Services/ChatHistoryService.cs:45-65`) and GUI *File ▸ Import History…* (open dialog rooted at the home directory).

---

#### 1.9 `CHAT SESSIONS` — list saved sessions *(NEW)*

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Sessions", "List saved conversation sessions",
    Prototype = "CHAT SESSIONS [-format text|json|csv] [-sort name|used|created|size] [-filter <text>]")]
[CommandParameterNamed("format", "Output shape",
    DefaultValue = "text", AllowedValues = new[]{ "text", "json", "csv" })]
[CommandParameterNamed("sort", "Ordering",
    DefaultValue = "used", AllowedValues = new[]{ "used", "name", "created", "size" })]
[CommandParameterNamed("filter", "Only list sessions whose name contains this text")]
public sealed class SessionsCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `SESSIONS` |
| Root command | `CHAT` |
| Description | List saved conversation sessions |
| Usage prototype | `CHAT SESSIONS [-format text\|json\|csv] [-sort name\|used\|created\|size] [-filter <text>]` |

**Why NEW, and why it earns its place.** The source had **no autosave and no autoload**: the record was created empty at process start and lost at process end unless the user explicitly exported it. It carried a session identifier that nothing ever read — not for lookup, not for file naming, not for logging. That is the largest single capability gap in the product, and the whole point of a model-debugging tool is to run the *same* conversation against different settings and compare. Making the session a named, listable, switchable object closes it. `SESSIONS` is the read half.

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text`, `json`, `csv` | `text` → `{marker}{name}  {messages} msgs  last used {timestamp}`, where marker is `*` for the active session. Sets `ResultFormat` accordingly. |
| `sort` | named | `string` | no | `used` | `used`, `name`, `created`, `size` | `used` is descending (most recent first); `name` is ordinal ascending. |
| `filter` | named | `string` | no | `""` | free text | Case-insensitive substring match against the session name. |

**Pipeline behaviour — output only.** Refuses piped input with an explanatory string. Emits one chunk per session, so `CHAT SESSIONS -filter bug- | CHAT USE` works (`CHAT USE`'s ordered `name` parameter declares `UsePipe = true`). **Overrides `Main`.**

**Environment interaction.** Reads `CHAT_SESSION` (to mark the active one) and `CHAT_SESSION_ROOT` (`storeDefault: false`). Writes nothing. Declares `{ "SESSION_ROOT", "" }` — empty means the platform default location.

**Failure modes.** Session directory missing → **success with a single chunk** `No saved sessions.` (creating the directory is `CHAT USE`'s job, not this one's). Directory unreadable → `Failure` with the reason. A session file that will not parse is **skipped**, not fatal, and reported as one failure chunk naming the file — failure isolation per file, mirroring the framework crawler's per-package isolation. Zero arguments is the common case and is handled explicitly.

**Security and audit.** No secrets. Session **names** are user-chosen and appear in audit records; content does not. Read-only, not destructive. The listing reflects only the session root; it never enumerates arbitrary directories.

**Traceability.** **NEW.** Nearest source ancestor: the unused `sessionId` field on the record (`Models/ChatHistory.cs:8-12`). PRD **7.4 Chat History**.

---

#### 1.10 `CHAT USE` — switch to (or create) a named session *(NEW)*

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Use", "Switch the active conversation session",
    Prototype = "CHAT USE <name> [-create yes|no] [-nosave] [-fork]")]
[CommandParameterOrdered("name", "Session name", IsRequired = true, UsePipe = true)]
[CommandParameterNamed("create", "Create the session if it does not exist",
    DefaultValue = "yes", AllowedValues = new[]{ "yes", "no" })]
[CommandFlag("nosave", "Discard unsaved changes to the current session instead of saving them")]
[CommandFlag("fork", "Copy the current record into the new session instead of starting empty")]
public sealed class UseCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `USE` |
| Root command | `CHAT` |
| Description | Switch the active conversation session |
| Usage prototype | `CHAT USE <name> [-create yes\|no] [-nosave] [-fork]` |

**Why NEW.** The write half of the session feature. `-fork` is the tool that makes the product's core experiment cheap: branch the conversation at its current state, then change one setting and continue in the branch, leaving the original intact for comparison.

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** | — | 1–128 characters | `UsePipe = true`, so a piped chunk supplies it. Sanitized for use as a file name by replacing every invalid filename character with `_` — the same rule the source used for system-prompt file names. |
| `create` | named | `string` | no | `yes` | `yes`, `no` | `no` turns a typo into an error rather than a new empty session. |
| `nosave` | flag | `bool` | no | `false` | presence = true | Default saves the current record before switching, so switching is non-destructive. |
| `fork` | flag | `bool` | no | `false` | presence = true | Copies the current record into the target session (which must not already exist) and mints a new session identifier for the copy. |

**Success text.** `Switched to session '{name}' ({n} messages)`, or `Created session '{name}'`, or `Forked '{from}' into '{name}' ({n} messages)`.

**Pipeline behaviour — both, but pathologically.** Accepts piped input (`name` declares `UsePipe = true`); **one chunk is one session name**, and each switches the active session, so a multi-chunk pipe leaves the *last* name active. That is genuinely useful with `CHAT SESSIONS -filter x | CHAT USE` where the filter yields one result, and confusing otherwise — so when more than one chunk arrives the tool emits a warning chunk (`{n} session names arrived; '{last}' is now active`). Produces one confirmation chunk per switch. `ResultFormat.General`.

**Environment interaction.** Reads `CHAT_SESSION_ROOT`. **Writes `CHAT_SESSION` and `CHAT_SESSION_ID`** into this package's bucket — and this is the mechanism by which every other tool in the package sees the switch on its next invocation, without any tool holding process state and without `ModifiesEnvironment = true`, because both keys carry the `CHAT_` root prefix. **This is the single most important environment interaction in the package.**

**Failure modes.** Missing name → `Failure`: `Usage: CHAT USE <name>`. `-create no` and the session does not exist → `Failure`: `No session named '{name}'. Run CHAT SESSIONS to list, or omit -create no to create it.` `-fork` onto an existing name → `Failure`: `Session '{name}' already exists; forking will not overwrite it.` Saving the current session fails → `Failure` **and the switch does not happen** (fail-closed; the user does not silently lose the current record). Session root not creatable → `Failure`: `Session store is unavailable in this host.` — and the package continues to work in memory-only mode with a warning, which is the restricted-host degrade path. Sanitized name collides with an existing different name → `Failure` naming both.

**Security and audit.** No secrets. **Path-traversal surface**: the session name becomes a file name. Two defences, both required — sanitize invalid filename characters to `_`, *then* verify component-wise that the resolved path is inside the session root after symlink resolution (never a string `StartsWith`). A name of `../../etc/x` sanitizes and then fails containment. Destructive only under `-nosave`, which discards unsaved changes to the current session; that flag prompts `Discard unsaved changes to '{current}'? (y/N) ` unless piped.

**Traceability.** **NEW.** PRD **7.4 Chat History**.

---

#### 1.11 `CHAT NAME` — show, set, or rename the current session *(NEW)*

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Name", "Show or set the name and identifier of the current session",
    Prototype = "CHAT NAME [<new_name...>] [-id <guid>] [-format text|json]")]
[CommandParameterNamed("id", "Set the session identifier explicitly", DataType = typeof(Guid))]
[CommandParameterNamed("format", "Output shape",
    DefaultValue = "text", AllowedValues = new[]{ "text", "json" })]
[CommandParameterSuffix("newname", "New session name; omit to show the current one", IsRequired = false)]
public sealed class NameCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `NAME` |
| Root command | `CHAT` |
| Description | Show or set the name and identifier of the current session |
| Usage prototype | `CHAT NAME [<new_name...>] [-id <guid>] [-format text\|json]` |

**Why NEW.** The naming half of the session feature, and the only place a reproducible experiment can pin an identifier. The source generated a random session id at construction, overwrote it wholesale on import, and never read it for anything.

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `newname` | suffix | `string` | no | `""` | 1–128 characters | Absent → the tool *shows* the current name, id, message count and creation timestamp. Present → renames, moving the session file. |
| `id` | named | `Guid` | no | *(unset)* | canonical hyphenated UUID | Sets the session identifier. Format preserved from the source: canonical lowercase hyphenated form. |
| `format` | named | `string` | no | `text` | `text`, `json` | |

**Zero-argument behaviour is the common case** and is explicitly designed around the framework's early return: with no arguments `ProcessParameters` yields an **empty dictionary**, no defaults are applied and no fields are injected, so the tool must treat "empty dictionary" as "show the current session" rather than as "rename to the default value". Stated here because it is the exact shape of the trap.

**Pipeline behaviour — output only.** Refuses piped input (a rename per chunk is meaningless and destructive). One output chunk. `ResultFormat.General` / `.JSON`.

**Environment interaction.** Reads `CHAT_SESSION`, `CHAT_SESSION_ROOT`. Writes `CHAT_SESSION` and `CHAT_SESSION_ID` on a rename or an `-id` change. No environment-modifying permission needed.

**Failure modes.** Rename onto an existing session name → `Failure`: `Session '{name}' already exists.` `-id` not a valid UUID → the framework marks the parameter invalid; the tool returns `Failure`: `Session id must be a UUID.` Session store unavailable → shows the in-memory session and warns that the rename cannot be persisted. Name sanitization and containment as for `CHAT USE`.

**Security and audit.** No secrets. Changing a session identifier **breaks the link between a saved record and any analysis previously derived from it** — the tool says so in the confirmation chunk. Renaming is a file move within the session root, containment-checked. Not destructive to content.

**Traceability.** **NEW.** Nearest source ancestor: `sessionId` on the record and the fact that import overwrote it. PRD **7.4 Chat History**.

---

#### 1.12 `CHAT RETRY` — re-run the last user turn with different settings *(NEW)*

```csharp
[CommandRoot("CHAT", "Conversation and session tools")]
[CommandRegister("Retry", "Discard the last reply and re-send the preceding user turn",
    Prototype = "CHAT RETRY [-provider inherit|azure|bedrock|llama] [-model <id>] " +
                "[-temperature <0.0-2.0>] [-maxtokens <1-8192>] [-logprobs inherit|on|off] " +
                "[-topk <1-20>] [-prompt <name>] [-stream on|off] [-format text|json] " +
                "[-timeout <seconds>] [-keep] [-yes]")]
[CommandParameterNamed("provider", "Backend for this retry only",
    DefaultValue = "inherit", AllowedValues = new[]{ "inherit", "azure", "bedrock", "llama" })]
[CommandParameterNamed("model", "Model id for this retry only")]
[CommandParameterNamed("temperature", "Sampling temperature, 0.0-2.0", DataType = typeof(double), ShortAlias = "t")]
[CommandParameterNamed("maxtokens", "Maximum reply tokens, 1-8192", DataType = typeof(int))]
[CommandParameterNamed("logprobs", "Request per-token log probabilities",
    DefaultValue = "inherit", AllowedValues = new[]{ "inherit", "on", "off" })]
[CommandParameterNamed("topk", "Alternatives per token, 1-20", DataType = typeof(int))]
[CommandParameterNamed("prompt", "System prompt name for this retry only")]
[CommandParameterNamed("stream", "Emit the reply incrementally",
    DefaultValue = "on", AllowedValues = new[]{ "on", "off" })]
[CommandParameterNamed("format", "Output shape",
    DefaultValue = "text", AllowedValues = new[]{ "text", "json" })]
[CommandParameterNamed("timeout", "Abandon the call after N seconds; 0 = wait forever",
    DataType = typeof(int), DefaultValue = "0")]
[CommandFlag("keep", "Emit the discarded reply as the first output chunk")]
[CommandFlag("yes", "Skip the confirmation prompt", ShortAlias = "y")]
public sealed class RetryCommand : AbstractCommand { }
```

| Registration | Value |
|---|---|
| Command name | `RETRY` |
| Root command | `CHAT` |
| Description | Discard the last reply and re-send the preceding user turn |
| Usage prototype | `CHAT RETRY [-provider …] [-model …] [-temperature <0.0-2.0>] [-maxtokens <1-8192>] [-logprobs …] [-topk <1-20>] [-prompt <name>] [-stream on\|off] [-format text\|json] [-timeout <seconds>] [-keep] [-yes]` |

**Why NEW, and why it earns its place.** This is the product's central loop expressed as one command. The source made the user do it by hand every time: `/pop` the assistant turn, retype the question (or lose it), `/set temperature 0.3`, ask again — and if the provider call had failed, the orphan user turn was left behind with `/pop` documented as the remedy. `CHAT RETRY -temperature 0.3` is that whole sequence, and because the overrides are per-invocation and transient, it does not disturb the configured settings. Sweeping a parameter — the same question at five temperatures — becomes five keystrokes apiece.

**Parameters** are `CHAT SEND`'s override set verbatim (same names, types, defaults, ranges and rejection texts — see §1.1), minus `message`, `norecord` and `rollback`, plus:

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `keep` | flag | `bool` | no | `false` | presence = true | Emits the discarded assistant turn as the **first** output chunk so it can be piped somewhere before it is gone. |
| `yes` | flag | `bool` | no | `false` | presence = true | Skips the confirmation. Required inside a pipeline. |

**Behaviour.** Locate the last message whose lower-cased role is `user`; discard every message after it (in practice the one assistant reply, but a fabricated tail is handled the same way); re-send the located user turn with the overrides applied; append the new reply. If the last turn is already a `user` turn with no reply after it — the state the source left behind after a failed provider call — nothing is discarded and the turn is simply re-sent.

**Pipeline behaviour — output only.** Refuses piped input (`Unsupported retry method for {chunk} (piped)`); a retry has no per-chunk meaning. Produces piped output: the discarded reply under `-keep`, then the streamed reply chunks, then a terminal chunk with elapsed time. **Overrides `Main`.** `ResultFormat.General` / `.JSON`.

**Environment interaction.** Identical to `CHAT SEND` (§1.1): reads the seven `CHATDBG_*` settings plus `CHAT_SESSION`; writes `CHAT_LAST_ELAPSED_MS`, `CHAT_TURN_COUNT`. No environment-modifying permission needed.

**Failure modes.** No `user` turn in the record → `Failure`: `Nothing to retry: no user message in the conversation record.` All the backend failure modes of `CHAT SEND` apply verbatim (unknown provider, not configured, no backend, no reply text, no log probabilities returned, timeout). **On a backend failure the discarded reply is not restored** — which is why `-keep` exists and why the confirmation exists; the failure chunk says so explicitly.

**Security and audit.** No secrets. **Destructive**: it removes the previous reply, irreversibly unless `-keep` was given. Confirmation: prompts `Discard the last reply and re-send? (y/N) ` unless `-yes` or `-keep`, and refuses inside a pipeline without `-yes`. Cost note as for `CHAT SEND` — a retry is a fresh billable call. Audit-masking obligation as for `CHAT SEND`, though `RETRY` itself passes no message text on the command line, which makes it the *safer* of the two to leave unmasked.

**Traceability.** **NEW.** Source ancestors it composes: the chat-turn path (`ChatShell.cs:343-410`), `/pop` (`Commands/PopCommand.cs`), and the documented orphan-turn remedy (`ChatShell.cs:404-408`). PRD **7.4 Chat History** and **7.6 AI Provider Abstraction**.

---

### Pipeline compositions

All six run through `CommandController.Run(line, ioContext, environmentContext)`. Remember the framework's two-stage tokenization: `PipelineParser` splits on unquoted `|` and **consumes the quotes**, then each segment is re-tokenized by the argument regex — which is exactly why every free-text and path parameter in this package is a **suffix** parameter that re-joins the remaining tokens with single spaces.

**1. Ask with a colder temperature and inspect the token probabilities — crosses into `ChatDbg.Tools.TokenAnalysis`.**

```
CHAT SEND -temperature 0.2 -logprobs on -topk 10 -format json why does this deadlock | TOKEN INSPECT -top 5 | VIEW GRID -maxalt 5
```

The user gets the reply streaming into the terminal as `CHAT SEND` produces chunks, each chunk a turn object carrying its per-token log probabilities; `TOKEN INSPECT` (package `ChatDbg.Tools.TokenAnalysis`, PRD 7.9/7.10) converts each into a token analysis; `VIEW GRID` (package `ChatDbg.Tools.Render`, PRD 7.12) lays the tokens out as cards, one card per 40 terminal columns with a minimum of one column, capped at 5 alternatives per card with a `+ N more` summary — the source's grid geometry, preserved. Because all three stages run concurrently on bounded channels, the first tokens are being rendered while the model is still generating.

**2. Re-run every question in this conversation against a different model, without touching the record.**

```
CHAT LIST -role user | CHAT SEND -norecord -model gpt-4o -temperature 0 -format json | CHAT EXPORT ~/sweep-gpt4o.jsonl -format jsonl
```

`CHAT LIST` emits one chunk per user turn; `CHAT SEND` treats each chunk as an independent prompt and, because of `-norecord`, leaves the live record exactly as it was; `CHAT EXPORT` — in its piped mode, where one chunk is one message record — writes the answers as JSON Lines. The user gets a side-by-side corpus for a model comparison and a conversation that is still fit to be a control.

**3. Save a filtered slice of the conversation — using the framework's own built-in `REGIF` filter.**

```
CHAT LIST -format json -logprobs | REGIF "\"role\":\"assistant\"" | CHAT EXPORT ~/answers-only.json -noclobber
```

`REGIF` is one of the framework's built-in commands (registered by `RegisterBuiltInCommands()`), and it filters by returning an **empty success** for non-matching chunks, which the host drops. The user gets a JSON file containing only the assistant turns with their log probabilities, and `-noclobber` means a second run reports `Refusing to overwrite existing file` instead of silently replacing it.

**4. Save what you are about to destroy.**

```
CHAT POP -count 4 -format json | CHAT EXPORT ~/rolled-back.json
```

`CHAT POP` emits the four removed messages, newest first, as JSON objects; `CHAT EXPORT` writes them. The user gets the record trimmed back four turns **and** a file from which those turns can be re-imported with `CHAT IMPORT ~/rolled-back.json -mode append`. This composition is the undo the source product never had. Note that `-count 4` inside a pipeline requires `-yes` — the tool cannot prompt when `HasPipedInput` is true — so the real line is `CHAT POP -count 4 -yes -format json | CHAT EXPORT ~/rolled-back.json`.

**5. Jump to a session by fuzzy name.**

```
CHAT SESSIONS -filter deadlock -format text | CHAT USE
```

`CHAT SESSIONS` emits one chunk per matching session; `CHAT USE`'s ordered `name` parameter declares `UsePipe = true`, so it is fed from the chunk instead of the command line. With one match the user is switched to it; with several, the last wins and a warning chunk names it. `CHAT USE` writes `CHAT_SESSION` into the package's environment bucket, so the very next `CHAT SEND` in the same shell is already in the new session.

**6. Seed a conversation from a file, ask, and branch — crosses into `ChatDbg.Tools.Prompts`.**

```
PROMPT SHOW security-expert | CHAT INJECT system -position 0 -quiet
CHAT SEND -prompt security-expert review the auth middleware
CHAT USE auth-review-cold -fork
CHAT RETRY -temperature 0 -keep | CHAT EXPORT ~/cold-answer.md -format md
```

Four lines rather than one pipeline, because they are four decisions. `PROMPT SHOW` (package `ChatDbg.Tools.Prompts`, PRD 7.5) emits the prompt body; `CHAT INJECT` places it at the head of the record as a `system` turn. The question is asked. `CHAT USE -fork` copies the whole conversation into a new session with a new identifier, leaving the original untouched. `CHAT RETRY -temperature 0 -keep` re-asks the same question deterministically in the fork, emits the discarded warm answer first, and the Markdown export captures both. That is the product's central experiment, and it is four commands.

---

### Design notes for the architect

**What this package holds.** Almost nothing, deliberately. `CommandFactory` constructs a **fresh command instance per execution**, and `CommandExecutor` does **not** dispose the instance it executed — so any state a tool holds in a field is per-invocation at best and leaked at worst. Therefore:

- The conversation record lives behind **`IConversationStore`**, a host-registered singleton resolved through the framework's DI path. Every tool takes it as a constructor dependency and touches the record only through it.
- The *identity* of the current session lives in the **environment context**, as `CHAT_SESSION` / `CHAT_SESSION_ID` in this package's private bucket. Because every key carries the root-command prefix, the host does not need to register any tool in this package with `modifiesEnvironment: true` — the framework routes prefixed writes into the command's own bucket and hands them back on the next invocation. This is the entire cross-invocation state mechanism, and it is worth being explicit that it costs nothing and needs no elevated permission.
- **Non-obvious framework detail to verify at integration**: sub-commands are dispatched through their root, so the command key the controller passes to `GetChild(commandName)` is `CHAT`, not `SEND`. All twelve tools therefore **share one environment bucket** and must read the `CHAT_`-prefixed forms of the keys they declare in `GetDefaultEnvironment()`. That sharing is a feature here (it is how `USE` talks to `SEND`), but it means two tools must never declare the same default key with different meanings.
- Per-pipe state (the injection ordinal, the export temp-file handle, the chunk counter) lives in instance fields initialised in `OnStartPipe` and flushed in `OnEndPipe`, which run only on the piped path.

**What this package must not hold.** No static conversation record. No `HttpClient`, no provider SDK, no native library, no credential — those belong to `ChatDbg.Tools.Providers` and `ChatDbg.Tools.Credentials`, and keeping them out is precisely what lets this assembly load under `AssemblySecurityPolicy.Strict` with `DisallowDynamicAssemblies = true`. No `Console` calls: the source product wrote persistence diagnostics straight to stdout, which painted stray text over the full-screen shell on every export or import failure. Everything a tool wants to say goes out as an `IResult<string>` chunk, a status message, or `AddTraceMessage`.

**How it stays testable.** The two seams — `IConversationStore` and `IConversationBackend` — are the whole test strategy. A hermetic suite drives every tool against an in-memory store and a scripted backend that replays canned replies and log-probability payloads, using the framework's `MemoryIoContext` (whose `Output` bag fills only when there is no output pipe, i.e. on the last stage or a standalone run) and a plain `EnvironmentContext`. A second, separate suite exercises the file-backed store and export/import round-trips against a temp directory, including the byte-for-byte format assertions: 2-space indentation, seven-digit UTC fractional seconds, the aggressive escaping profile (apostrophe as `\u0027`, backtick as `\u0060`, plus as `\u002B`, non-ASCII as `\uXXXX`), shortest-round-trip numbers, key order, `null` written literally. Keep those two suites in separate projects; never mix the hermetic one with anything that touches a real disk or a real network.

**When a capability is unavailable.** The rule is *degrade at the smallest granularity that still tells the truth*:

| Missing | Behaviour |
|---|---|
| No `IConversationBackend` (restricted host, or the Providers package was not loaded) | `SEND` and `RETRY` fail with `No conversation backend is available in this host. Record tools (CHAT LIST/INJECT/POP/CLEAR/IMPORT/EXPORT) still work.` The other ten tools are unaffected. |
| Backend present but not configured | The source's exact two-line advisory, retargeted at `CRED STATUS`. No network call is attempted. |
| Backend cannot stream | `-stream on` is honoured as a request, not a demand: one chunk arrives at the end and a trace note records that streaming was unavailable. **Never a failure.** |
| Backend returns no log probabilities | The source's two-line note, and the turn is still recorded. **Never a failure** — hosted models and local models differ here and the user should not have to know which. |
| Local backend ignores most of the record | Stated, not silently absorbed: the source's local-model paths read **only the content of the last message whose role case-insensitively equals `user`** and ignored the is-a-command flag entirely, so injecting or popping assistant and system turns changed nothing the model saw. `CHAT INJECT` and `CHAT POP` emit a one-line trace note when the active provider is `llama` and the affected role is not `user`. Reproducing the behaviour silently would make the product lie about its own core feature. |
| Session store unreadable or uncreatable | The package falls back to a memory-only store for the process, emits one warning chunk at first use, and `SESSIONS`/`USE`/`NAME` report `Session store is unavailable in this host.` Editing, sending and exporting continue. |
| Non-interactive or piped context where a confirmation is required | Never prompt into a pipe — `PromptForCommand` is only meaningful when `HasPipedInput` is false. `POP -count > 1`, `CLEAR` and `RETRY` refuse with a message naming `-yes`. `IMPORT -mode replace` proceeds (matching the source, which never asked) but emits a warning chunk naming the discarded count. |
| Windows-only capabilities | There are none in this package. The source's only Windows-bound behaviour was credential storage, which lives in `ChatDbg.Tools.Credentials`. Every path here resolves through the platform's own home and local-application-data locations, and the `~` expansion accepts `~/`, `~\` and a bare `~` on all platforms. |

**Where it should degrade rather than fail — and where it must not.** Degrade: a missing backend, a non-streaming backend, absent log probabilities, an unwritable session store, an unparseable session file in a listing, a chunk that will not parse mid-export. Fail closed, always: a `-backup` that cannot be written before a `CLEAR`; a save that fails during a `CHAT USE` switch; a path outside a configured `CHAT_EXPORT_ROOT` / `CHAT_IMPORT_ROOT`; a session name that fails containment after sanitization; an import larger than `-maxbytes`. The distinction is whether the user could lose data they cannot get back — if yes, refuse.

**Two quirks deliberately preserved, and why.** First, **space collapsing**: every free-text and path parameter joins its tokens with single spaces, so `~/my  chats/a.json` still becomes `~/my chats/a.json`. This is not laziness — it is the framework's suffix-parameter contract and the source's tokenizer behaving identically, and pretending otherwise would require a quoting layer the pipeline parser already ate. It is documented in `CommandHelpRemarks` on every affected tool. Second, **`CHAT EXPORT` still overwrites silently**, because re-exporting a working session to the same path is the source's normal case and breaking it would be gratuitous; the atomicity fix removes the actual harm (a truncated file where a valid history used to be) without changing the contract, and `-noclobber` is there for the careful.

**One quirk deliberately abandoned.** The source read `/inject`'s position from the *last positional token*, and only when there were strictly more than two arguments — so `/inject user 42` injected the text `42`, `/inject user 42 7` injected `42` at position 7, and an unparseable trailing token silently became part of the message while the GUI dialog silently discarded it. Two paths, two incompatible rules, both undiscoverable. `-position` is a named parameter here, and that is a change users will notice; it is called out in this package's migration notes and in the tool's help remarks.

---

## 2. ChatDbg.Tools.ConfigurationProfiles — Configuration & Profiles

### 2.1 Purpose and boundary

**What this package owns.** Every tunable that decides *which back end, which model, how creative, how long, where, and with what back-end-specific tuning* a ChatDbg session runs — and the document those tunables live in. Concretely:

- The **settings record**: the 21 persisted fields of the source product's `ChatSettings` (plus the deliberate additions listed in §2.3), their exact defaults, their exact inclusive ranges, their exact wire names.
- The **settings document**: where it resolves to (`~/.ChatDbg/settings.json`, with a temp-directory fallback), how it is loaded, how a corrupt document degrades, how it is validated, how it is written, and how it is reset.
- The **write surface**: viewing, reading one key, writing one key, clearing one key, restoring defaults, importing and exporting whole documents, and validating a candidate document before it is adopted.
- **Named configuration profiles** — a complete set of records that can be listed, switched, captured, dropped and diffed. This is entirely new (§2.1.3).
- The **publication of effective configuration** into the host's controller environment under `CHATDBG_*` keys, so that every other tool package reads configuration from one authoritative place instead of sharing a mutable object (§2.6.1).

**What this package explicitly does NOT own.**

| Not owned | Owned by | Boundary rule |
|---|---|---|
| Secret **values** — resolving, storing, reading, migrating, or the OS keystore itself | `ChatDbg.Tools.Credentials` (root `CRED`) | This package owns the *toggle* (`useOsCredentialStore`) and the three deprecated in-document secret slots it is trying to retire. It asks `CRED` two questions only: "is a credential store available on this platform?" and "where does secret *X* resolve from?" It never sees, prints, pipes or persists a secret value. The source's `/set wincred`, `/set enablewincred`, `/set migrate` and the three blocked keys `azureApiKey` / `awsAccessKey` / `awsSecretKey` all move to `CRED`; `SET VALUE` refuses those keys and names the `CRED` tool that replaces each one. |
| The **meaning** of a system prompt: its body, its store, create/edit/delete | `ChatDbg.Tools.Prompts` (root `PROMPT`) | This package persists the prompt **name** only, and calls `PROMPT` to validate that the name resolves. The prompt *body* is never persisted (source parity). |
| What a provider **does** with `temperature`, `maxTokens`, `azureEndpoint`, `azureApiVersion`, `awsRegion`, the GGUF file | `ChatDbg.Tools.Providers` (root `AI`) | This package validates and stores; `AI` decides whether a provider "is configured", builds requests, and reports capability. `SET VALUE provider llama` does not load a model. |
| Token-probability **analysis and rendering** semantics | `ChatDbg.Tools.TokenProbability` (root `LOGPROB`) and `ChatDbg.Tools.Rendering` (root `RENDER`) | The five display fields (`enableLogProbabilities`, `logProbabilitiesTopK`, `showAllTokens`, `gridViewForTokens`, `gridViewMaxAlternatives`) are *stored* here and are settable through `SET VALUE`; the source's convenience surface `/logprobs enable|top|showall|grid|list|gridmaxalt` is re-homed on `LOGPROB`, which writes through this package's settings-store port. Both surfaces share one validator, so the source's split-brain wording (`Top-K value must be…` vs `LogProbabilitiesTopK must be…`) disappears. |
| Chat history, transcripts, injection/pop/clear | `ChatDbg.Tools.History` (root `CHAT`) | No overlap. |
| Diagnostic log capture and export | `ChatDbg.Tools.Diagnostics` (root `LOG`) | `SET` emits no log files; it emits audit events like every other tool. |
| Shell prompt, exit words, package directory, theme | The host (`ChatDbg.Shell.Core`) | Host personality is host configuration, not product configuration. `SET` never rewrites the loop. |

#### 2.1.1 The `SET` name collision — resolved, not ignored

`Xcaciv.Command` ships a built-in top-level command named `SET` (`Xcaciv.Command/Commands/SetCommand.cs`, registered under package key `Default` with `modifiesEnvironment: true`) whose contract is `SET <varname> <value>` writing a **shell environment variable**. This package claims `SET` as a `[CommandRoot]`, which the registry resolves by replacement in registration order.

The decision: **the host registers built-ins first and this package second, so `SET` becomes a root**, and this package ships `SET ENV` (§2.4.16) reproducing the built-in's behaviour byte-for-byte — including `UsePipe` on the value and the `OnStartPipe` clear-then-append accumulation idiom. Nothing is lost, and the product's most-typed verb keeps the meaning its users expect. The host must not call `RegisterBuiltInCommands()` *after* loading packages, and the packaging check in §2.6.8 asserts that ordering.

#### 2.1.2 Fidelity posture

Every default, bound and message in §2.3 is carried verbatim from the source unless a row is marked **DEVIATION**, in which case the reason is stated inline. There are eleven deliberate deviations and no accidental ones.

#### 2.1.3 Why profiles exist at all

The source explicitly provides none: *"No profiles, no per-workspace settings, no config-file layering, no `--config` override at runtime."* But the product's whole reason for having ~24 tunables is that a user moves between an Azure deployment, a Bedrock model and a local GGUF rig, each with a different endpoint, model id, context size and GPU layer count. In the source, switching rigs means six to nine `/set` commands typed from memory, every one of which overwrites the single document — and there is no way back. Profiles are the smallest addition that makes the existing tunables usable: a named, complete, validated snapshot with `SET PROFILE <name>` to switch and `SET DIFF` to see what changed. They are additive — a user who never types `PROFILE` sees the source's exact one-document behaviour, because the document `~/.ChatDbg/settings.json` *is* the profile named `default`.

---

### 2.2 Package manifest

| Property | Value |
|---|---|
| Assembly / package id | `Xcaciv.ChatDbg.Tools.ConfigurationProfiles` |
| Root command | `SET` (claimed as `[CommandRoot("SET", "Configuration, tunables and profiles")]`; see §2.1.1) |
| Sub-commands | 16 (§2.4) |
| Contract assemblies targeted | `Xcaciv.Command.Interface` **3.3.4** and `Xcaciv.Command.Core` **3.3.4** — and nothing else from the framework. No reference to `Xcaciv.Command` (the host), per the Cupcake rule "tool → SDK, never tool → host". |
| Target framework | `net10.0` (`LangVersion 14`, `ImplicitUsings`, `Nullable` enabled), single TFM across the graph |
| Third-party dependencies | `System.Text.Json` (in-box). `YamlDotNet` **16.3.0** only if `-format yaml` ships in v1; otherwise none. **No** `System.IO.Abstractions` in the package itself — filesystem access is behind this package's own `ISettingsStore` port (§2.6.3). |
| Elevated trust required | **No.** No P/Invoke, no native library, no registry, no service control, no admin-only path. |
| Filesystem reach | Read/write exactly two directory subtrees: the settings base directory (default `<user profile>/.ChatDbg/`, override via `SET_BASEDIR`) and its `profiles/` child. Plus **read-only, existence-check only** access to an arbitrary path when validating `modelId` under the `llama` provider. It never writes outside the base directory and never deletes anything but a profile document. |
| Network reach | **None.** This package makes no outbound connection of any kind. Endpoint and region are strings it validates and stores; it never dials them. |
| OS keystore reach | **None directly.** It calls `ChatDbg.Tools.Credentials` for the availability predicate and for provenance strings. |
| Native libraries | None. |
| Process environment | Reads five credential variable **names** for provenance reporting (never their values into output), and three optional startup overrides (§2.4 per-tool tables). Writes none of the process environment; writes only the framework's `IEnvironmentContext`. |
| Environment-modifying permission | **Required for 6 of 16 tools.** `SET VALUE`, `SET UNSET`, `SET RESET`, `SET IMPORT`, `SET PROFILE` and `SET ENV` must be registered with `modifiesEnvironment: true` because they publish `CHATDBG_*` globals other packages read. The other ten write only their own `SET_`-prefixed bucket and need no elevation. |
| Safe in a restricted host | **Yes, with one caveat.** Loadable under `AssemblySecurityPolicy` with `DisallowDynamicAssemblies = true`; it emits no dynamic code and reflects only over its own attributes (which `AbstractCommand` does anyway). The caveat: in a host whose settings base directory is not writable, the package must be loaded with `SET_READONLY=true`, in which case all twelve mutating tools degrade to a single explanatory failure and the six read tools continue to work (§2.6.6). |
| Audit posture | Every tool is audit-logged once per execution by `CommandExecutor`. Three tools carry parameters that can hold a path; none carries a secret. The package supplies its own `IAuditMaskingConfiguration` additions (§2.6.7) because the framework's masking only rewrites `-name=value` tokens and is effectively non-functional for `-name value` syntax. |

---

### 2.3 The settings key catalog (normative)

Every tool in this package refers back to this one table rather than restating ranges. It is also the table `SET KEYS` (§2.4.9) emits at runtime, so the help text, the validator, the GUI and the machine-readable schema can never drift apart — the source had three copies of this list and all three disagreed (source quirk Q7: the "unknown setting" error named 13 of ~26 keys).

Key matching is **case-insensitive**; the canonical form below is what is echoed and emitted.

| Key (canonical) | Aliases accepted | Wire name in the document | Type | Default | Valid range / values | Violation message (verbatim from source unless noted) |
|---|---|---|---|---|---|---|
| `provider` | — | `provider` | enum text | `azure` | `azure`, `bedrock`, `llama`; stored lowercase | `Provider must be 'azure', 'bedrock', or 'llama'` |
| `modelId` | `model` | `modelId` | free text | `gpt-4` | any text; **when the active provider is exactly `llama` and the value is non-empty, the value must name an existing file** | `LLama model file not found: <path>` + newline + `Make sure you've specified the correct path to a GGUF model file.` |
| `temperature` | — | `temperature` | decimal | `0.7` | **0.0 … 2.0 inclusive** | `Temperature must be a number between 0 and 2` |
| `maxTokens` | — | `maxTokens` | integer | `1000` | **1 … 8192 inclusive** | `MaxTokens must be a number between 1 and 8192` |
| `azureEndpoint` | — | `azureEndpoint` | free text, nullable | *(null)* | source enforces nothing. **DEVIATION:** a *warning-only* URL shape check is emitted (`Warning: '<v>' does not look like an https:// endpoint.`) and the value is still stored, preserving "accept anything". One trailing `/` is trimmed on read by the provider package, as in source. | — (warning only) |
| `azureApiVersion` | — | `azureApiVersion` | free text | `2023-12-01-preview` | non-empty; shape `yyyy-MM-dd[-preview]` warned, not enforced | `azureApiVersion must not be empty` — **NEW / DEVIATION**: the source hard-codes this in the request URL while `/logprobs debug` tells the user to "use a recent API version", advice they cannot act on (source quirk Q33). Making it a real key is the fix. |
| `awsRegion` | — | `awsRegion` | free text | `us-east-1` | no validation (source parity — any string is a region) | — |
| `systemPrompt` | `systemPromptName` | `systemPromptName` | free text | `default` | when `ChatDbg.Tools.Prompts` is reachable, the name must resolve; when it is not, the name is stored unvalidated (source parity) | `System prompt not found: <name>. Use '/prompt list' to see available prompts or '/prompt create <name>' to create a new one.` — **DEVIATION:** re-worded to the rebuilt verbs: `System prompt not found: <name>. Use 'PROMPT LIST' to see available prompts or 'PROMPT NEW <name>' to create one.` |
| `enableLogProbabilities` | `logprobs` | `enableLogProbabilities` | boolean | `false` | `true` / `false`. **DEVIATION:** the framework converter also accepts `1/0/yes/no/on/off`; the source accepted only `true`/`false`. The broader set is adopted deliberately — it is the framework's documented boolean grammar and rejecting `yes` inside a framework that accepts it everywhere else is a worse surprise than the widened grammar. | `enableLogProbabilities must be 'true' or 'false'` |
| `logProbabilitiesTopK` | `logtopk`, `topk` | `logProbabilitiesTopK` | integer | `5` | **1 … 20 inclusive** | `logProbabilitiesTopK must be a number between 1 and 20` |
| `showAllTokens` | — | `showAllTokens` | boolean | `false` | as above | `showAllTokens must be 'true' or 'false'` |
| `gridViewForTokens` | `tokensgrid` | `gridViewForTokens` | boolean | `false` | as above | `gridViewForTokens must be 'true' or 'false'` |
| `gridViewMaxAlternatives` | `gridmaxalt` | `gridViewMaxAlternatives` | integer | `5` | **1 … 20 inclusive** | `gridViewMaxAlternatives must be a number between 1 and 20` |
| `useOsCredentialStore` | `useWindowsCredentialManager`, `wincred` | `useWindowsCredentialManager` | boolean | `false` | `true` only when `CRED` reports a store is available on this OS | `useOsCredentialStore must be 'true' or 'false'` / `No OS credential store is available on this platform (<os>).` — **DEVIATION:** the key is renamed because the capability is no longer Windows-only (§2.6.5); the **wire name is unchanged** so existing documents keep loading, and the old name stays as an alias forever. |
| `llamaContextSize` | — | `llamaContextSize` | integer | `4096` | **512 … 32768 inclusive** | `llamaContextSize must be a number between 512 and 32768` |
| `llamaGpuLayerCount` | `llamaGpuLayers` | `llamaGpuLayerCount` | integer | `0` | **0 … 100 inclusive**; 0 = CPU-only | `llamaGpuLayerCount must be a number between 0 and 100. 0 means CPU-only, higher numbers move more layers to GPU.` |
| `llamaGpuDevice` | — | `llamaGpuDevice` | free text, nullable | *(null)* | no validation; conventionally `0` or `0,1` | — |
| `llamaThreads` | — | `llamaThreads` | integer | `0` | **0 … 64 inclusive**; 0 = system default | `llamaThreads must be a number between 0 and 64. 0 means system default.` |
| `llamaBatchSize` | — | `llamaBatchSize` | integer | `512` | **1 … 2048 inclusive** | `llamaBatchSize must be a number between 1 and 2048` |
| `azureApiKey` | — | `azureApiKey` | free text | `""` | **write-blocked.** Always emitted to the document (empty when unset), source parity. | `For security, Azure API Key is no longer set via this command.` + the secure-options guide, now naming `CRED PUT azure` and `CHATDBG_AZURE_API_KEY` |
| `awsAccessKey` | — | `awsAccessKey` | free text | `""` | write-blocked, always emitted | as above, naming `CHATDBG_AWS_ACCESS_KEY` and `AWS_ACCESS_KEY_ID` |
| `awsSecretKey` | — | `awsSecretKey` | free text | `""` | write-blocked, always emitted | as above, naming `CHATDBG_AWS_SECRET_KEY` and `AWS_SECRET_ACCESS_KEY` |
| `profile` | — | `activeProfile` | free text | `default` | must name an existing profile; set by `SET PROFILE`, **read-only through `SET VALUE`** | `Use 'SET PROFILE <name>' to change the active profile.` — **NEW** |

**Range semantics are inclusive on both ends, everywhere** — `0` and `2` are both valid temperatures, `1` and `8192` are both valid token caps, `1` and `20` are both valid Top-K values, `512` and `32768` are both valid context sizes, `0` and `100` are both valid GPU-layer counts, `0` and `64` are both valid thread counts, `1` and `2048` are both valid batch sizes. This is source behaviour and is preserved exactly.

**Three keys the source persists but never consumes** — `llamaGpuDevice`, `llamaThreads`, `llamaBatchSize` (source quirk Q6). They remain settable, validated, persisted and displayed here, because removing them would break existing documents; `SET SHOW` and `SET KEYS` mark them `(not yet consumed by the local runtime)` so the user is not misled the way the source's README misled them.

#### 2.3.1 The document

- Location: `<base>/settings.json`, where `<base>` is, in order: the `SET_BASEDIR` environment value if set; else `CHATDBG_HOME` if set; else `<user profile>/.ChatDbg`; else — if the profile lookup is empty, whitespace or throws — the OS temp directory. (Source parity, with the two environment overrides marked **NEW**.)
- Profiles live at `<base>/profiles/<name>.json`. The active profile's content is mirrored into `<base>/settings.json` on every switch, so a v1 document and a profile-aware document are the same file shape and older builds keep working.
- Format: **indented** JSON with the exact lower-camel wire names above (the file is meant to be hand-edited). The three deprecated secret slots are always written, empty when unset. Computed/derived members and the system-prompt **body** are never written.
- **DEVIATION — culture.** Every number in the document and every number accepted on the command line is parsed and formatted with **invariant culture**, unconditionally, in every build configuration. The source used ambient culture, and forced invariant globalization only in its `Compact` and `SingleFile` builds — so the same `SET VALUE temperature 0.7` was accepted by the shipped binary and rejected by a debug build on a comma-decimal locale, and a document written by one could not be read by the other (source quirk Q15). Packaging must never change input validation.
- **DEVIATION — atomicity.** Writes are temp-file-plus-rename inside the base directory, not whole-file overwrite in place. The source had no lock, no atomic replace and no backup, so two shells clobbered each other silently. Last-writer-still-wins, but a crashed or racing writer can no longer leave a truncated document.

---

### 2.4 Tool catalog

All sixteen classes carry `[CommandRoot("SET", "Configuration, tunables and profiles")]` and are invoked as `SET <sub-command> …`. All parameter attributes sit **on the class** (`AttributeTargets.Class, AllowMultiple = true`), never on properties or fields; injected fields are `public` **instance fields**, never properties.

Two framework facts shape every tool below and are not repeated per tool:

1. **Zero arguments means zero parameter processing.** `AbstractCommand.ProcessParameters` returns an empty dictionary when `io.Parameters.Length == 0` — no defaults applied, no flags materialised, no field injection. Every tool therefore states its bare-invocation behaviour explicitly and codes `parameters.TryGetValue(...) && p.IsValid ? … : <literal default>` for every read.
2. **Parse-time exceptions are invisible to the user.** An `ArgumentException` from a missing required parameter or an `AllowedValues` violation is caught by `CommandExecutor` and reduced to `Error executing SET (see trace for more info)`. So this package declares `AllowedValues` **only** where a generic error is acceptable (low-cardinality display options), and validates high-value inputs — setting keys, numeric ranges, profile names — inside `HandleExecution`, returning `CommandResult<string>.Failure(<the exact message from §2.3>)`.

---

#### 2.4.1 `SET SHOW` — render the current configuration

| | |
|---|---|
| Command | `SHOW` |
| Root command | `SET` |
| Description | Show the effective configuration, or one section of it, in human or machine form |
| Usage prototype | `SET SHOW [<section>] [-format text\|json\|yaml\|csv] [-profile <name>] [-defaults] [-changed]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Show", "Show the effective configuration",
    Prototype = "SET SHOW [<section>] [-format text|json|yaml|csv] [-profile <name>] [-defaults] [-changed]")]
[CommandParameterOrdered("section", "Section to show", IsRequired = false, DefaultValue = "all",
    AllowedValues = new[] { "all", "general", "provider", "azure", "aws", "llama", "logprobs", "credentials", "paths" })]
[CommandParameterNamed("format", "Output shape",
    DefaultValue = "text", AllowedValues = new[] { "text", "json", "yaml", "csv" }, ShortAlias = "f")]
[CommandParameterNamed("profile", "Show this profile instead of the active one", ShortAlias = "p")]
[CommandFlag("defaults", "Show built-in defaults rather than stored values")]
[CommandFlag("changed", "Show only values that differ from the built-in default", ShortAlias = "c")]
[CommandHelpRemarks("Secret values are never rendered. Credential rows show status and provenance only.")]
[CommandHelpRemarks("'-format json' emits the document's wire names and is round-trippable through SET IMPORT.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `section` | ordered | `string` | no | `all` | `all`, `general`, `provider`, `azure`, `aws`, `llama`, `logprobs`, `credentials`, `paths` | Which block of the dump to render |
| `format` | named | `string` | no | `text` | `text`, `json`, `yaml`, `csv` | **NEW** — the source rendered one fixed human block only |
| `profile` | named | `string` | no | *(active profile)* | any existing profile name | **NEW** — inspect another profile without switching to it |
| `defaults` | flag | `bool` | no | `false` | — | **NEW** — render the built-in defaults, for "what would RESET give me?" |
| `changed` | flag | `bool` | no | `false` | — | **NEW** — render only drift from defaults; the fastest bug report a user can produce |

**`text` output is the source's dump, preserved.** Section headers and labels are carried verbatim: `Current Settings:` with `- Provider:`, `- Model ID:`, `- Temperature:`, `- Max Tokens:`, `- Azure Endpoint:` (`(not set)` when empty), `- AWS Region:`, `- System Prompt:`, `- Log Probabilities:` (`Enabled`/`Disabled`), `- Log Probabilities Top-K:`, `- Show All Tokens:` (`Yes` / `No (sample only)`), `- Token Display:` (`Grid Layout` / `List Layout`), `- Grid View Max Alternatives:`, `- OS Credential Store:` (`Enabled`/`Disabled`); then `Credentials (secure):` with `***set***` / `(not set)` plus a bracketed provenance string; then `LLama Provider Settings:` with `- Context Size:`, `- GPU Layer Count:` (` (CPU-only)` when 0), `- GPU Device:` (`(default)` when unset), `- Threads:` (`(system default)` when 0), `- Batch Size:`. **DEVIATION:** the source's trailing space after a non-zero GPU layer count (quirk Q28) is removed, and the source's dead find-and-replace over the dump (quirk Q8) is not reproduced. The three static help blocks the source appended to every dump are moved to `SET KEYS` and `HELP SET SHOW`; a dump is a dump.

**Pipeline behaviour** — **produces** piped output; **does not accept** piped input. Piped invocation returns the explanatory failure `SET SHOW does not read piped input. Did you mean 'SET IMPORT' or 'SET VALIDATE'?` rather than throwing. Non-piped `HandleExecution` may emit exactly one chunk under `AbstractCommand`, so the whole dump is one chunk with embedded newlines; that is correct here, because a settings dump is one document and splitting it per line would let a downstream filter silently halve it. `OutputFormat` is set from `-format`: `ResultFormat.General` for `text`, `ResultFormat.JSON`, `ResultFormat.YAML`, `ResultFormat.CSV` for the others — declared so the host's `AbstractTextIo` subclass can pick a renderer (the framework itself never branches on it; encoding is the host's job).

**Environment interaction** — reads `SET_FILE`, `SET_BASEDIR`, `SET_PROFILE`, `SET_REDACT` (default `true`). Reads the *names* `CHATDBG_AZURE_API_KEY`, `CHATDBG_AWS_ACCESS_KEY`, `AWS_ACCESS_KEY_ID`, `CHATDBG_AWS_SECRET_KEY`, `AWS_SECRET_ACCESS_KEY` from the process environment to report provenance, **never their values**. Writes nothing. Needs no environment-modifying permission.

**Failure modes** — unknown `section`: parse-time `AllowedValues` rejection, surfacing as the generic executor message (accepted here: the prototype and `HELP` list all nine). Unknown `-profile`: `Profile '<name>' not found. Run 'SET PROFILES' to list them.` Unreadable/corrupt document: the tool **still succeeds**, rendering built-in defaults plus a first line `Warning: the settings document could not be parsed (<reason>); showing built-in defaults. The file has not been modified.` — the source's degrade-to-defaults behaviour, but now visible in the command result instead of only on stdout. Base directory unresolvable: renders with the temp-directory path and a `paths` warning row. Downstream errors: not applicable (no piped input).

**Security and audit** — no parameter and no output line carries a secret; the credential section is status-and-provenance only, and the source's habit of echoing plaintext secrets during migration (quirk Q9) has no equivalent here. `-profile` values appear in audit parameters and are treated as non-sensitive names. Non-destructive; no confirmation.

**Traceability** — PRD **7.2 Settings & Configuration** (primary), **7.3 Credential Management** (provenance rows only), **7.12 Output Rendering** (`-format`). Descends from `/set` with no arguments (`Commands/SetCommand.cs:401-458`), the windowed Settings dialog's read path, and the plain-console startup banner. `-format`, `-profile`, `-defaults`, `-changed` and the `section` filter are **NEW**.

---

#### 2.4.2 `SET GET` — read exactly one value

| | |
|---|---|
| Command | `GET` |
| Root command | `SET` |
| Description | Print the value of one setting, and nothing else |
| Usage prototype | `SET GET <key> [-profile <name>] [-source]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Get", "Print the value of one setting", Prototype = "SET GET <key> [-profile <name>] [-source]")]
[CommandParameterOrdered("key", "Setting key to read", UsePipe = true)]
[CommandParameterNamed("profile", "Read from this profile instead of the active one", ShortAlias = "p")]
[CommandFlag("source", "Append where the value came from (document, default, environment override)")]
[CommandHelpRemarks("With piped input, each incoming chunk is treated as one key and one value is emitted per chunk.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `key` | ordered, `UsePipe = true` | `string` | yes (unless piped) | — | any canonical key or alias in §2.3 | Setting to read |
| `profile` | named | `string` | no | *(active)* | existing profile name | Read another profile's value |
| `source` | flag | `bool` | no | `false` | — | Append ` [document]` / ` [built-in default]` / ` [environment override]` |

**Pipeline behaviour** — **both**. As a source: `SET GET temperature` emits one chunk, the bare value with no label, so it composes. As a filter: one incoming chunk means **one setting key**; each chunk is looked up and one value chunk is emitted, in order. An unknown key in a piped chunk emits a failure chunk naming that key and processing continues with the next chunk — a bad key must not kill a batch. `OutputFormat` is `ResultFormat.General` (bare scalar) so the value can be consumed by any downstream tool; with `-source` it stays `General` because the annotation is human text.

**Environment interaction** — reads `SET_FILE`, `SET_BASEDIR`, `SET_PROFILE`. Writes nothing. No elevation.

**Failure modes** — unknown key: `Unknown setting: <key>. Run 'SET KEYS' for the full list.` (**DEVIATION** — the source's inline "Valid keys:" list named 13 of ~26 and rotted, quirk Q7; delegating to `SET KEYS` cannot rot). Write-blocked secret key: succeeds and returns the *status word* `***set***` or `(not set)` — never the value. Missing key with no pipe: parse-time required-parameter failure, so the usage prototype is the user's only cue; the help remark states it. Unparseable document: same visible-warning degrade as `SET SHOW`, then the default value.

**Security and audit** — the `key` parameter can name a secret slot; the **output is masked unconditionally** and cannot be unmasked by any flag. This package registers `azureApiKey`, `awsAccessKey`, `awsSecretKey` and `apikey` in its audit masking configuration, but see §2.6.7 — framework masking does not cover `-name value` syntax, which is exactly why no tool here ever accepts a secret as a parameter. Non-destructive.

**Traceability** — PRD **7.2**. **NEW** — the source had no single-value read; the only way to see one setting was the 46-line dump. This tool is what makes every pipeline in §2.5 possible.

---

#### 2.4.3 `SET VALUE` — change one tunable

| | |
|---|---|
| Command | `VALUE` |
| Root command | `SET` |
| Description | Validate and store one setting, then persist the document |
| Usage prototype | `SET VALUE <key> <value…> [-profile <name>] [-clamp] [-dryrun] [-quiet]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Value", "Validate and store one setting",
    Prototype = "SET VALUE <key> <value...> [-profile <name>] [-clamp] [-dryrun] [-quiet]")]
[CommandParameterOrdered("key", "Setting key to write")]
[CommandParameterSuffix("value", "New value; all remaining words, joined with single spaces", UsePipe = true)]
[CommandParameterNamed("profile", "Write to this profile instead of the active one", ShortAlias = "p")]
[CommandFlag("clamp", "Pull an out-of-range number to the nearest bound instead of rejecting it")]
[CommandFlag("dryrun", "Validate and report, but do not mutate or persist", ShortAlias = "n")]
[CommandFlag("quiet", "Suppress the confirmation line", ShortAlias = "q")]
[CommandHelpRemarks("Ranges are inclusive at both ends. Run 'SET KEYS' for every key, default and range.")]
[CommandHelpRemarks("Secrets cannot be set here. Use the CRED tools or the documented environment variables.")]
[CommandHelpRemarks("Quote a value containing '|' so the pipeline parser does not split your command line.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `key` | ordered | `string` | yes | — | any canonical key or alias in §2.3. **No `AllowedValues` is declared** — the tool validates in-body so the user sees the source's precise `Unknown setting: <key>` message instead of the executor's generic one | Setting to write |
| `value` | suffix, `UsePipe = true` | `string` | yes (unless piped) | — | per-key, per §2.3 | Everything after the key, joined with single spaces. Runs of spaces collapse — source parity |
| `profile` | named | `string` | no | *(active)* | existing profile name | **NEW** — edit a profile you are not running |
| `clamp` | flag | `bool` | no | `false` | — | **NEW** — makes the source's *second*, hidden policy explicit (§2.6.4) |
| `dryrun` | flag | `bool` | no | `false` | — | **NEW** |
| `quiet` | flag | `bool` | no | `false` | — | **NEW** — for pipelines that only want the side effect |

**Pipeline behaviour** — **accepts and produces**. Because `value` carries `UsePipe = true`, `… | SET VALUE temperature` reads the value from the pipe and the command line carries only the key. One incoming chunk means **one candidate value for the named key**; each chunk is validated and applied and the tool emits `Set <key> = <value>` per chunk, or an empty success under `-quiet` (which the host drops, so a quiet pipeline stage is silent — the `SET`/`REGIF` idiom). `OnStartPipe` snapshots the record and opens one write transaction; `OnEndPipe` commits **once**, so a 500-chunk pipe produces one document write, not 500. That is a deliberate improvement on the source's save-on-every-change, without losing the guarantee: after `OnEndPipe` the document reflects every applied chunk. Non-piped invocation writes immediately, exactly as the source did.

**Environment interaction** — reads `SET_FILE`, `SET_BASEDIR`, `SET_PROFILE`, `SET_STRICT` (default `true`; `false` makes `-clamp` the default), `SET_READONLY`. **Writes** the effective-configuration mirror: on success it republishes the changed key as `CHATDBG_<UPPER_SNAKE_KEY>` in the controller environment (e.g. `CHATDBG_TEMPERATURE`, `CHATDBG_MAX_TOKENS`, `CHATDBG_AZURE_API_VERSION`). **This tool must be registered with `modifiesEnvironment: true`** — without it the framework confines its writes to the `SET_` bucket and no other package sees the change. Reads no process environment variable except the three startup overrides.

**Failure modes**

| Situation | What the user sees |
|---|---|
| Unknown key | `Unknown setting: <key>. Run 'SET KEYS' for the full list.` Nothing mutated, nothing written. |
| Key given, no value, no pipe | `Usage: SET VALUE <key> <value>` — the source's arity rule, minus its consequence: a value *may* now be empty via `SET UNSET` (§2.4.4). |
| Out of range, `-clamp` absent | The exact per-key message from §2.3. Nothing mutated, nothing written. |
| Out of range, `-clamp` present | Success with `Set <key> = <bound> (clamped from <input>)`. The clamp is always reported — the source's dialog clamped silently. |
| Unparseable number | `<key> must be a number between <lo> and <hi>` — even under `-clamp`. The source's dialog silently kept the old value; silence about garbage input is not a feature. |
| Write-blocked secret key | The secure-options guide, now naming `CRED PUT <type>` and the documented environment variables. Nothing stored anywhere. |
| `modelId` under provider `llama`, file missing | `LLama model file not found: <path>` + `Make sure you've specified the correct path to a GGUF model file.` |
| System prompt name does not resolve | `System prompt not found: <name>. Use 'PROMPT LIST' …` |
| `useOsCredentialStore true` with no store on this OS | `No OS credential store is available on this platform (<os>).` |
| Document write fails | **DEVIATION:** `Failed to save settings: <reason>. The change is active for this session only.` returned as a **failure result**. The source printed to stdout and still reported success (quirk Q5) — invisible in the windowed shell. |
| `SET_READONLY=true` | `The settings document is read-only in this host. Values can be inspected but not changed.` |
| Failure chunk arrives on the pipe | `AbstractCommand.Main` forwards it downstream untouched and never calls `HandlePipedChunk`; the transaction stays open and later good chunks still apply. `OnEndPipe` commits what succeeded and appends `<n> chunk(s) failed upstream and were not applied.` |

**Security and audit** — the `value` suffix parameter is the one place in this package where a user could type something sensitive (e.g. an endpoint with an embedded token). Because framework audit masking only rewrites `-name=value` tokens, this tool declares that `azureEndpoint` values are recorded in audit metadata **hashed, not literal**, and the three secret keys never reach the audit record at all (the command fails before storing). Not destructive in the "irreversible" sense — every write is recoverable via `SET RESET <key>` or a profile — so no confirmation is required.

**Traceability** — PRD **7.2** (primary), **7.3** (blocked keys), **7.5** (system prompt name), **7.6 / 7.7 / 7.8** (the keys each provider consumes), **7.9** (the five display keys). Descends from `/set <key> <value…>` (`Commands/SetCommand.cs:28-294`). `-profile`, `-clamp`, `-dryrun`, `-quiet` and pipe-fed values are **NEW**.

---

#### 2.4.4 `SET UNSET` — clear a text setting

| | |
|---|---|
| Command | `UNSET` |
| Root command | `SET` |
| Description | Return one setting to empty (text keys) or to its built-in default (typed keys) |
| Usage prototype | `SET UNSET <key> [-profile <name>] [-dryrun]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Unset", "Clear one setting", Prototype = "SET UNSET <key> [-profile <name>] [-dryrun]")]
[CommandParameterOrdered("key", "Setting key to clear", UsePipe = true)]
[CommandParameterNamed("profile", "Clear in this profile instead of the active one", ShortAlias = "p")]
[CommandFlag("dryrun", "Report what would be cleared without writing", ShortAlias = "n")]
[CommandHelpRemarks("Nullable text keys become empty; typed keys return to the built-in default from SET KEYS.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `key` | ordered, `UsePipe = true` | `string` | yes (unless piped) | — | any key in §2.3 except the three write-blocked secret slots and `profile` | Setting to clear |
| `profile` | named | `string` | no | *(active)* | existing profile name | Target profile |
| `dryrun` | flag | `bool` | no | `false` | — | Validate only |

**Pipeline behaviour** — **accepts and produces**. One chunk = one key to clear; one confirmation chunk out per key. Same one-transaction `OnStartPipe`/`OnEndPipe` commit discipline as `SET VALUE`. `ResultFormat.General`.

**Environment interaction** — identical to `SET VALUE`, including the `CHATDBG_*` republish and the `modifiesEnvironment: true` requirement (clearing `azureEndpoint` must propagate, or the provider package keeps dialling a stale host).

**Failure modes** — unknown key: as `SET GET`. Attempt to clear a secret slot: `Secrets are cleared with 'CRED CLEAR <type>', not here.` Attempt to clear `provider`, `temperature` or any other non-nullable typed key: succeeds, restoring the §2.3 default, and says so — `Cleared temperature (restored default 0.7)`. Read-only host and write-failure behaviour identical to `SET VALUE`.

**Security and audit** — no secrets. Reversible via `SET VALUE` or a saved profile; no confirmation required.

**Traceability** — PRD **7.2**. **NEW**, and it closes a real hole: the source's arity rule rejected a bare `/set azureEndpoint`, and there was no reset, unset or clear verb anywhere, so **no text setting could ever be returned to empty from the command surface** — only by hand-editing or deleting the document. The windowed dialog could clear those fields, so the two surfaces disagreed about what was expressible.

---

#### 2.4.5 `SET RESET` — restore built-in defaults

| | |
|---|---|
| Command | `RESET` |
| Root command | `SET` |
| Description | Restore one key, one section, or the whole record to built-in defaults |
| Usage prototype | `SET RESET [<key-or-section>] [-profile <name>] [-force] [-dryrun] [-backup]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Reset", "Restore built-in defaults",
    Prototype = "SET RESET [<key-or-section>] [-profile <name>] [-force] [-dryrun] [-backup]")]
[CommandParameterOrdered("target", "Key or section to reset", IsRequired = false, DefaultValue = "all")]
[CommandParameterNamed("profile", "Reset this profile instead of the active one", ShortAlias = "p")]
[CommandFlag("force", "Confirm a whole-record reset", ShortAlias = "y")]
[CommandFlag("dryrun", "List what would change without writing", ShortAlias = "n")]
[CommandFlag("backup", "Copy the current document to <base>/backups/ before writing")]
[CommandHelpRemarks("'SET RESET all' rewrites every field and requires -force. A single key does not.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `target` | ordered | `string` | no | `all` | any key in §2.3, any section name from `SET SHOW`, or `all` | What to restore |
| `profile` | named | `string` | no | *(active)* | existing profile name | Target profile |
| `force` | flag | `bool` | no | `false` | — | Required when `target` is `all` or a section |
| `dryrun` | flag | `bool` | no | `false` | — | Emits the same lines a real run would, prefixed `would reset` |
| `backup` | flag | `bool` | no | `false` | — | **NEW** — the source never backed anything up |

**Pipeline behaviour** — **produces only**. Emits one line per field actually changed (`reset <key>: <old> -> <default>`), so a reset is auditable at a glance and `SET RESET -dryrun | REGIF llama` answers "what would this touch?". Piped input is refused with `SET RESET does not read piped input. Pipe keys into 'SET UNSET' instead.` `ResultFormat.General`; `-format` is intentionally absent because the output is a change log, not a record.

**Environment interaction** — reads `SET_FILE`, `SET_BASEDIR`, `SET_PROFILE`, `SET_CONFIRM` (default `true`; when `false`, `-force` is not demanded — for non-interactive hosts and test harnesses). **Writes** the full `CHATDBG_*` mirror, because a whole-record reset changes almost every published key. Requires `modifiesEnvironment: true`.

**Failure modes** — `target` names nothing recognisable: `Unknown setting or section: <target>. Run 'SET KEYS' or 'SET SHOW -format csv'.` Whole-record or section reset without `-force` while `SET_CONFIRM` is `true`: **fails safe** with `This resets <n> settings in profile '<p>'. Re-run with -force to confirm.` — nothing is touched. Backup directory not writable under `-backup`: the reset is **abandoned**, not performed without the backup. Write failure: reported as a failure result, with the note that the in-memory record has already been reset for this session.

**Security and audit** — **destructive and, without `-backup`, irreversible.** Confirmation is mandatory for `all` and for sections. It never touches the three secret slots (those are `CRED`'s to clear) and never deletes the document itself — a reset rewrites content, it does not unlink the file. Audit metadata records the target and the count of fields changed.

**Traceability** — PRD **7.2**. **NEW.** The source had no reset path at all: *"Deleted: never. There is no reset command, no 'restore defaults', and no file deletion path. The only way back to defaults is to delete the file out-of-band."* A product with ~24 tunables and eight numeric ranges needs a way home.

---

#### 2.4.6 `SET MODEL` — read or change the model identifier

| | |
|---|---|
| Command | `MODEL` |
| Root command | `SET` |
| Description | Show or change the model id / GGUF path for the active provider |
| Usage prototype | `SET MODEL [<model id or path…>] [-profile <name>] [-nocheck]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Model", "Show or change the model identifier",
    Prototype = "SET MODEL [<model id or path...>] [-profile <name>] [-nocheck]")]
[CommandParameterSuffix("modelid", "New model id, or a GGUF file path for the local provider",
    IsRequired = false, UsePipe = true)]
[CommandParameterNamed("profile", "Change the model in this profile", ShortAlias = "p")]
[CommandFlag("nocheck", "Skip the local-model file existence check")]
[CommandHelpRemarks("With no argument this prints the current model and changes nothing.")]
[CommandHelpRemarks("Under provider 'llama' the value must name an existing GGUF file unless -nocheck is given.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `modelid` | suffix, `UsePipe = true` | `string` | no | *(none — read mode)* | any text; existing file path when provider is `llama` | New model identifier |
| `profile` | named | `string` | no | *(active)* | existing profile name | Target profile |
| `nocheck` | flag | `bool` | no | `false` | — | **NEW** — the deliberate escape hatch for the one case the check gets wrong (a path that will exist by the time the model loads) |

**Pipeline behaviour** — **accepts and produces**. Bare `SET MODEL` emits `Current model: <id>` and writes nothing (source parity). With a value it emits `Changed model from '<old>' to '<new>'` (source parity, verbatim). Piped: one chunk = one candidate model id; each is validated and applied in order, last one wins, one confirmation chunk each. `ResultFormat.General`.

**Environment interaction** — reads `SET_FILE`, `SET_BASEDIR`, `SET_PROFILE`; republishes `CHATDBG_MODEL_ID`. Requires `modifiesEnvironment: true` when it writes.

**Failure modes** — empty or whitespace-only value: `A model id is required. Run 'SET MODEL' with no arguments to see the current one.` — **DEVIATION**, the source accepted empty-ish input here. Provider is `llama` and the file does not exist: the same message `SET VALUE modelId` gives, `LLama model file not found: <path>` + `Make sure you've specified the correct path to a GGUF model file.` — **DEVIATION**, and the important one: in the source, `/model` bypassed every rule `/set modelId` enforced while writing the identical field (quirk Q1). Two commands writing one field must agree. Write failure: failure result, as `SET VALUE`.

**Security and audit** — the value may be a filesystem path; paths are recorded in audit parameters as given. Not destructive; the previous model id is in the confirmation line, so the undo is obvious.

**Traceability** — PRD **7.2**, **7.6 / 7.7 / 7.8**. Descends from `/model` (`Commands/ModelCommand.cs:23-34`). `-profile`, `-nocheck`, pipe support and validation parity are **NEW**.

---

#### 2.4.7 `SET PATH` — where configuration lives

| | |
|---|---|
| Command | `PATH` |
| Root command | `SET` |
| Description | Report the resolved settings document, base directory, profile directory and how each was chosen |
| Usage prototype | `SET PATH [-format text\|json] [-verify]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Path", "Report configuration file locations",
    Prototype = "SET PATH [-format text|json] [-verify]")]
[CommandParameterNamed("format", "Output shape", DefaultValue = "text", AllowedValues = new[] { "text", "json" }, ShortAlias = "f")]
[CommandFlag("verify", "Also report existence, writability and size for each path")]
[CommandHelpRemarks("The base directory falls back to the OS temp directory when the user profile cannot be resolved.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text`, `json` | Output shape |
| `verify` | flag | `bool` | no | `false` | — | Adds existence / writable / byte-size / last-write columns |

**Pipeline behaviour** — **produces only**; refuses piped input with an explanatory string. `ResultFormat.JSON` under `-format json`, else `General`. The bare text form emits the settings document path alone on the first line so `SET PATH | …` composes with any path-consuming tool.

**Environment interaction** — reads `SET_BASEDIR`, `SET_FILE`, `SET_PROFILE`, plus the process variables `CHATDBG_HOME` and `CHATDBG_SETTINGS_FILE` (**NEW** overrides). Writes `SET_FILE` and `SET_BASEDIR` back into its own bucket so later tools in the same pipeline resolve identically — a same-bucket write, so no elevation.

**Failure modes** — user profile lookup empty, whitespace or throwing: succeeds, reporting the OS temp directory and the reason (`user profile could not be resolved; using the temp directory`). Under `-verify`, an unwritable base directory is reported as a row, not an error — this tool's job is to tell the truth about the filesystem, never to fail because of it.

**Security and audit** — emits absolute filesystem paths, which can leak a username in the home-directory component. Audit records the flags only, not the resolved output. Non-destructive.

**Traceability** — PRD **7.2**, **7.11 Diagnostic Logging** (it is the first line of every good bug report). Descends from the plain-console startup banner's `Settings file: <path>` line — **the only place the source ever told the user where their configuration lived**, and one the windowed shell never printed. Promoting it to a tool is **NEW**.

---

#### 2.4.8 `SET VALIDATE` — check a record before trusting it

| | |
|---|---|
| Command | `VALIDATE` |
| Root command | `SET` |
| Description | Validate the active record, a profile, or a piped document against the key catalog |
| Usage prototype | `SET VALIDATE [<profile>] [-strict] [-format text\|json\|csv]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Validate", "Validate a configuration record",
    Prototype = "SET VALIDATE [<profile>] [-strict] [-format text|json|csv]")]
[CommandParameterOrdered("profile", "Profile to validate", IsRequired = false, DefaultValue = "", UsePipe = true)]
[CommandFlag("strict", "Treat warnings (unknown keys, suspicious endpoints) as failures")]
[CommandParameterNamed("format", "Output shape", DefaultValue = "text", AllowedValues = new[] { "text", "json", "csv" }, ShortAlias = "f")]
[CommandHelpRemarks("Piped input is treated as a JSON settings document, one complete document per chunk.")]
[CommandHelpRemarks("Validation never mutates anything; use SET IMPORT to adopt a document that passes.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `profile` | ordered, `UsePipe = true` | `string` | no | `""` (= active record) | existing profile name | What to validate |
| `strict` | flag | `bool` | no | `false` | — | Promote warnings to failures |
| `format` | named | `string` | no | `text` | `text`, `json`, `csv` | Report shape |

**Checks performed** — every §2.3 range and enum; provider name **case-sensitivity** (a hand-edited `"Azure"` is reported as `provider 'Azure' will not match any known provider; use 'azure'` — the source silently produced `Warning: Unknown AI provider: Azure` at startup and dropped every chat turn thereafter); `modelId` file existence when `provider` is `llama`; system-prompt name resolution via `PROMPT`; `useOsCredentialStore` set on a platform with no store; unknown keys present in the document; keys whose value is a number formatted for a non-invariant culture; and the three deprecated secret slots being non-empty — reported as `azureApiKey is stored in plaintext in the settings document. Run 'CRED MIGRATE'.` **with the value never shown**.

**Pipeline behaviour** — **accepts and produces**. One incoming chunk = one complete JSON settings document, validated independently; one report per chunk. This is the shape that makes `SET SHOW -format json | SET VALIDATE -strict` and `SET PROFILES | SET VALIDATE` work. `ResultFormat.JSON` / `CSV` / `General` per `-format`. A clean record under `-format csv` emits **no rows and an empty success**, which the host drops — so a validation stage is silent when everything is fine, and only speaks when something is wrong.

**Environment interaction** — reads `SET_FILE`, `SET_BASEDIR`, `SET_PROFILE`, `SET_STRICT`. Writes nothing at all — this tool is pure. No elevation.

**Failure modes** — piped chunk is not valid JSON: a failure chunk `Chunk <n> is not a JSON object: <parser message>`, and the next chunk is still processed. Named profile missing: `Profile '<name>' not found.` Findings present: the tool returns **success** with the findings as output unless `-strict`, in which case it returns a failure whose message is the finding count — so a script can branch on it. Upstream failure chunks pass straight through untouched.

**Security and audit** — reads documents that may contain plaintext secrets in the deprecated slots and **must never echo one**; findings name the key, never the value. Non-destructive, no confirmation.

**Traceability** — PRD **7.2**. **NEW.** The source validated only at the moment of a `/set` write; a hand-edited or copied document was read verbatim with no validation whatsoever, and the user discovered the problem as a runtime chat failure.

---

#### 2.4.9 `SET KEYS` — the machine-readable key catalog

| | |
|---|---|
| Command | `KEYS` |
| Root command | `SET` |
| Description | List every setting key with its type, default, range and current value |
| Usage prototype | `SET KEYS [<group>] [-format text\|csv\|json] [-changed]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Keys", "List every setting key, type, default and range",
    Prototype = "SET KEYS [<group>] [-format text|csv|json] [-changed]")]
[CommandParameterOrdered("group", "Key group", IsRequired = false, DefaultValue = "all",
    AllowedValues = new[] { "all", "general", "azure", "aws", "llama", "logprobs", "credentials" })]
[CommandParameterNamed("format", "Output shape", DefaultValue = "csv", AllowedValues = new[] { "text", "csv", "json" }, ShortAlias = "f")]
[CommandFlag("changed", "List only keys whose current value differs from the default", ShortAlias = "c")]
[CommandHelpRemarks("Default output is CSV, one key per chunk, so it composes directly into SET GET.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `group` | ordered | `string` | no | `all` | `all`, `general`, `azure`, `aws`, `llama`, `logprobs`, `credentials` | Which family of keys |
| `format` | named | `string` | no | `csv` | `text`, `csv`, `json` | Output shape |
| `changed` | flag | `bool` | no | `false` | — | Drift only |

**Pipeline behaviour** — **produces only**, and it is the package's primary pipeline **source**. It overrides `Main` (legal, and necessary: `AbstractCommand`'s non-piped path emits exactly one chunk) to emit **one chunk per key**, so downstream filters and per-key tools compose naturally. Columns: `key,aliases,type,default,min,max,allowed,current,consumed`. `ResultFormat.CSV` by default, `JSON`, or `General` for the aligned human table. Piped input is refused with an explanatory string.

**Environment interaction** — reads `SET_FILE`, `SET_BASEDIR`, `SET_PROFILE` for the `current` column. Writes nothing. No elevation.

**Failure modes** — unreadable document: still succeeds, with the `current` column filled from defaults and a leading warning row. Unknown group: parse-time `AllowedValues` rejection (acceptable; all seven groups are in the prototype).

**Security and audit** — the `current` column for the three secret slots is `***set***` / `(not set)`, never a value. Non-destructive.

**Traceability** — PRD **7.1 Command System & Dispatch** (discoverability) and **7.2**. **NEW.** It replaces three drifting hand-maintained lists in the source — the `/set` long help (~72 lines), the settings dump's static "Setup Commands" block, and the `Unknown setting:` error's 13-of-26 key list — with one generated catalog.

---

#### 2.4.10 `SET IMPORT` — adopt a whole document

| | |
|---|---|
| Command | `IMPORT` |
| Root command | `SET` |
| Description | Validate and adopt a complete settings document from a file or the pipe |
| Usage prototype | `SET IMPORT [<path>] [-profile <name>] [-merge] [-dryrun] [-force] [-backup]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Import", "Adopt a settings document",
    Prototype = "SET IMPORT [<path>] [-profile <name>] [-merge] [-dryrun] [-force] [-backup]")]
[CommandParameterOrdered("path", "Document to import", IsRequired = false, DefaultValue = "", UsePipe = true)]
[CommandParameterNamed("profile", "Import into this profile instead of the active one", ShortAlias = "p")]
[CommandFlag("merge", "Apply only the keys present in the document; keep the rest")]
[CommandFlag("dryrun", "Validate and report the change list without writing", ShortAlias = "n")]
[CommandFlag("force", "Adopt despite validation warnings", ShortAlias = "y")]
[CommandFlag("backup", "Copy the current document to <base>/backups/ before writing")]
[CommandHelpRemarks("Without -merge the document replaces every key; absent keys revert to their defaults.")]
[CommandHelpRemarks("Secret keys in an imported document are ignored and reported, never stored.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `path` | ordered, `UsePipe = true` | `string` | yes (unless piped) | `""` | readable file path | JSON (or YAML, if that format shipped) document |
| `profile` | named | `string` | no | *(active)* | profile name; created if absent | Import target |
| `merge` | flag | `bool` | no | `false` | — | Patch rather than replace |
| `dryrun` | flag | `bool` | no | `false` | — | Change list only |
| `force` | flag | `bool` | no | `false` | — | Required when validation produces warnings |
| `backup` | flag | `bool` | no | `false` | — | Pre-write snapshot |

**Pipeline behaviour** — **accepts and produces**. One incoming chunk = one complete JSON document; each is validated in full and, if it passes, applied. Emits one `imported <key>: <old> -> <new>` line per changed key, and a final count. `OnStartPipe` opens one transaction; `OnEndPipe` commits once. `ResultFormat.General`.

**Environment interaction** — reads `SET_FILE`, `SET_BASEDIR`, `SET_PROFILE`, `SET_CONFIRM`, `SET_READONLY`. **Writes** the full `CHATDBG_*` mirror; requires `modifiesEnvironment: true`.

**Failure modes** — path missing or unreadable: `Cannot read '<path>': <reason>`. Not valid JSON: `<path> is not a valid settings document: <parser message>`. Validation findings without `-force`: fails with the finding list, nothing written — a bad document must never half-apply. Findings with `-force`: applies the valid keys, skips the invalid ones, and names each skipped key. Secret keys present: ignored, with `Ignored 2 credential field(s) in the imported document; secrets are managed by CRED.` Write failure: failure result naming the reason, with a note that the in-memory record now differs from disk.

**Security and audit** — reads a user-supplied file that may contain plaintext secrets; those values are **never stored, never echoed, never logged**. The path parameter is audited. **Destructive without `-merge`** — it replaces the record — so `-force`/confirmation applies whenever validation is not clean, and `-backup` is strongly recommended in the help text.

**Traceability** — PRD **7.2**. **NEW.** The source had no import, no merge and no layering of any kind; the only way to move a configuration between machines was to copy the file by hand and hope it parsed.

---

#### 2.4.11 `SET PROFILE` — show or switch the active profile

| | |
|---|---|
| Command | `PROFILE` |
| Root command | `SET` |
| Description | Show the active configuration profile, or switch to another one |
| Usage prototype | `SET PROFILE [<name>] [-dryrun]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Profile", "Show or switch the active configuration profile",
    Prototype = "SET PROFILE [<name>] [-dryrun]")]
[CommandParameterOrdered("name", "Profile to activate", IsRequired = false, DefaultValue = "", UsePipe = true)]
[CommandFlag("dryrun", "Report what switching would change without switching", ShortAlias = "n")]
[CommandHelpRemarks("With no name this prints the active profile and changes nothing.")]
[CommandHelpRemarks("Switching validates the target first; an invalid profile is never activated.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered, `UsePipe = true` | `string` | no | `""` (= read mode) | an existing profile name; `[A-Za-z0-9._-]{1,64}` | Profile to activate |
| `dryrun` | flag | `bool` | no | `false` | — | Preview the switch as a change list |

**Pipeline behaviour** — **accepts and produces**. Bare invocation emits `Active profile: <name>` and changes nothing. With a name it validates, activates, mirrors the profile into `<base>/settings.json`, and emits `Switched to profile '<name>' (provider <p>, model <m>).` Piped: one chunk = one profile name; the last valid one wins, one line each. `ResultFormat.General`.

**Environment interaction** — reads `SET_BASEDIR`, `SET_PROFILE`, and the process variable `CHATDBG_PROFILE` (**NEW**, honoured at first resolution so a shell can be launched into a profile). **Writes** `CHATDBG_PROFILE` and the whole `CHATDBG_*` mirror; requires `modifiesEnvironment: true` — a switch that other packages cannot see is a bug, not a switch.

**Failure modes** — profile not found: `Profile '<name>' not found. Run 'SET PROFILES' to list them.` Profile exists but fails validation: `Profile '<name>' has <n> problem(s) and was not activated. Run 'SET VALIDATE <name>'.` — the active profile is untouched. Name fails the character rule: `A profile name may contain letters, digits, dot, dash and underscore only.` Mirror write fails: the switch is **rolled back in memory** and reported, so the session and the document never disagree.

**Security and audit** — no secrets (a profile document never holds one; secret slots are stripped on save, §2.4.13). Reversible by switching back; no confirmation. The profile name is audited.

**Traceability** — PRD **7.2**. **NEW** — see §2.1.3 for why the package earns its name.

---

#### 2.4.12 `SET PROFILES` — list profiles

| | |
|---|---|
| Command | `PROFILES` |
| Root command | `SET` |
| Description | List every saved profile, one per chunk |
| Usage prototype | `SET PROFILES [-format text\|csv\|json] [-verbose]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Profiles", "List saved configuration profiles",
    Prototype = "SET PROFILES [-format text|csv|json] [-verbose]")]
[CommandParameterNamed("format", "Output shape", DefaultValue = "text", AllowedValues = new[] { "text", "csv", "json" }, ShortAlias = "f")]
[CommandFlag("verbose", "Include provider, model, last-used and validity for each profile", ShortAlias = "v")]
[CommandHelpRemarks("The active profile is marked with a leading '*' in text form and an isActive column otherwise.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text`, `csv`, `json` | Output shape |
| `verbose` | flag | `bool` | no | `false` | — | Adds provider, model, last-used timestamp, validity |

**Pipeline behaviour** — **produces only**, one chunk per profile (overrides `Main` for the same reason `SET KEYS` does). In `text` form each chunk is the bare profile name, so `SET PROFILES | SET VALIDATE` and `SET PROFILES | SET DIFF -against active` work with no glue. Refuses piped input with an explanatory string. `ResultFormat.CSV` / `JSON` / `General`.

**Environment interaction** — reads `SET_BASEDIR`, `SET_PROFILE`. Writes nothing. No elevation.

**Failure modes** — profile directory missing: succeeds, emitting the single implicit profile `default` (the one-document world the source lived in). Unreadable profile document: emitted with `(unreadable)` in verbose form rather than aborting the listing. Directory unreadable: failure result naming the path and the reason.

**Security and audit** — none; names and metadata only. Non-destructive.

**Traceability** — PRD **7.2**. **NEW.**

---

#### 2.4.13 `SET SAVEPROFILE` — capture the current record as a profile

| | |
|---|---|
| Command | `SAVEPROFILE` |
| Root command | `SET` |
| Description | Save the current configuration as a named profile |
| Usage prototype | `SET SAVEPROFILE <name> [-from <profile>] [-overwrite] [-describe <text…>]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("SaveProfile", "Save the current configuration as a named profile",
    Prototype = "SET SAVEPROFILE <name> [-from <profile>] [-overwrite] [-describe <text...>]")]
[CommandParameterOrdered("name", "Name for the new profile", UsePipe = true)]
[CommandParameterNamed("from", "Copy this profile instead of the live record")]
[CommandFlag("overwrite", "Replace an existing profile of the same name", ShortAlias = "y")]
[CommandParameterSuffix("describe", "Free-text description stored with the profile", IsRequired = false)]
[CommandHelpRemarks("Credential fields are stripped: a profile document never contains a secret slot.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered, `UsePipe = true` | `string` | yes (unless piped) | — | `[A-Za-z0-9._-]{1,64}`, not `default` unless `-overwrite` | Profile name |
| `from` | named | `string` | no | *(live record)* | existing profile name | Copy source |
| `overwrite` | flag | `bool` | no | `false` | — | Required to replace |
| `describe` | suffix | `string` | no | `""` | any text | Stored as `description` in the profile document |

**Pipeline behaviour** — **accepts and produces**. One chunk = one profile name to create (useful for scripted fan-out of a base configuration). Emits `Saved profile '<name>' (<n> settings).` per chunk. `ResultFormat.General`.

**Environment interaction** — reads `SET_BASEDIR`, `SET_PROFILE`, `SET_READONLY`. Writes only its own `SET_LAST_PROFILE` bucket key — saving a profile does not change the active configuration, so **no `modifiesEnvironment` elevation is required**. That asymmetry with `SET PROFILE` is deliberate and is the cheapest way to keep the elevated set small.

**Failure modes** — name already exists without `-overwrite`: `Profile '<name>' already exists. Re-run with -overwrite to replace it.` Invalid name: the character-rule message from §2.4.11. `-from` names nothing: `Profile '<name>' not found.` Profile directory not writable: failure result naming the path. Read-only host: the standard read-only refusal.

**Security and audit** — **the three secret slots are stripped on save, unconditionally.** A profile document is designed to be shared, mailed and committed; it must never be able to carry a credential. That is stated in a help remark so the user can rely on it. `-overwrite` replaces a document, so it is the confirmation gate.

**Traceability** — PRD **7.2**. **NEW.**

---

#### 2.4.14 `SET DROPPROFILE` — delete a profile

| | |
|---|---|
| Command | `DROPPROFILE` |
| Root command | `SET` |
| Description | Permanently delete a saved profile |
| Usage prototype | `SET DROPPROFILE <name> [-force] [-backup]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("DropProfile", "Delete a saved profile",
    Prototype = "SET DROPPROFILE <name> [-force] [-backup]")]
[CommandParameterOrdered("name", "Profile to delete", UsePipe = true)]
[CommandFlag("force", "Confirm the deletion", ShortAlias = "y")]
[CommandFlag("backup", "Copy the profile to <base>/backups/ before deleting")]
[CommandHelpRemarks("The active profile and the profile named 'default' cannot be deleted.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered, `UsePipe = true` | `string` | yes (unless piped) | — | an existing profile, not the active one, not `default` | Profile to delete |
| `force` | flag | `bool` | no | `false` | — | **Required** unless `SET_CONFIRM=false` |
| `backup` | flag | `bool` | no | `false` | — | Pre-delete snapshot |

**Pipeline behaviour** — **accepts and produces**. One chunk = one profile name; each deletion is confirmed individually and a refusal on one name does not stop the rest. Emits `Deleted profile '<name>'.` per success. `ResultFormat.General`. Note that `-force` applies to the whole invocation, which is the point: a piped bulk delete is an explicit act.

**Environment interaction** — reads `SET_BASEDIR`, `SET_PROFILE`, `SET_CONFIRM`, `SET_READONLY`. Writes nothing outside its bucket; no elevation.

**Failure modes** — no `-force` with `SET_CONFIRM=true`: `This permanently deletes profile '<name>'. Re-run with -force.` — nothing deleted. Target is the active profile: `Profile '<name>' is active. Switch away with 'SET PROFILE <other>' first.` Target is `default`: `The 'default' profile cannot be deleted. Use 'SET RESET all -force' to return it to built-in defaults.` Not found: `Profile '<name>' not found.` Backup requested but the backup directory is unwritable: **the deletion is abandoned**. Delete fails: failure result naming the path and reason.

**Security and audit** — **destructive and irreversible without `-backup`.** Confirmation mandatory. No secrets involved (profiles carry none). The deleted name and the backup path go into audit metadata.

**Traceability** — PRD **7.2**. **NEW.**

---

#### 2.4.15 `SET DIFF` — compare two configurations

| | |
|---|---|
| Command | `DIFF` |
| Root command | `SET` |
| Description | Report the keys that differ between two profiles, or between a profile and the built-in defaults |
| Usage prototype | `SET DIFF <left> [<right>] [-against active\|defaults\|<profile>] [-format text\|csv\|json]` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Diff", "Compare two configurations",
    Prototype = "SET DIFF <left> [<right>] [-against active|defaults|<profile>] [-format text|csv|json]")]
[CommandParameterOrdered("left", "First profile, or 'active'", UsePipe = true)]
[CommandParameterOrdered("right", "Second profile", IsRequired = false, DefaultValue = "")]
[CommandParameterNamed("against", "Comparison target when only one side is given", DefaultValue = "defaults")]
[CommandParameterNamed("format", "Output shape", DefaultValue = "text", AllowedValues = new[] { "text", "csv", "json" }, ShortAlias = "f")]
[CommandHelpRemarks("Emits one chunk per differing key, so a clean comparison is silent.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `left` | ordered, `UsePipe = true` | `string` | yes (unless piped) | — | a profile name, or `active`, or `defaults` | Left side |
| `right` | ordered | `string` | no | `""` | as above | Right side; when omitted, `-against` decides |
| `against` | named | `string` | no | `defaults` | `active`, `defaults`, or a profile name | Right side for single-sided comparison |
| `format` | named | `string` | no | `text` | `text`, `csv`, `json` | Output shape |

**Pipeline behaviour** — **accepts and produces**, and it overrides `Main` to emit **one chunk per differing key** (`<key>: <left> | <right>`). One incoming chunk = one left-hand profile name compared against `-against`, which is what makes `SET PROFILES | SET DIFF -against active` a per-profile drift report. **A clean comparison emits nothing** — an empty success that the host drops — so the tool is silent when there is no news, following the framework's `REGIF` filter idiom. `ResultFormat.CSV` / `JSON` / `General`.

**Environment interaction** — reads `SET_BASEDIR`, `SET_PROFILE`. Writes nothing. No elevation.

**Failure modes** — either side not found: `Profile '<name>' not found.` as a failure chunk; other chunks continue. A side that is unreadable or unparseable: reported as one finding row rather than aborting. Both sides identical: empty success (silence), which is a documented outcome, not an error.

**Security and audit** — a diff can surface the value of any non-secret key; secret slots are compared by **presence only**, rendered `***set*** | (not set)`. Non-destructive.

**Traceability** — PRD **7.2**, **7.11**. **NEW.**

---

#### 2.4.16 `SET ENV` — write a shell environment variable

| | |
|---|---|
| Command | `ENV` |
| Root command | `SET` |
| Description | Set a shell environment variable (the framework's built-in `SET`, preserved under the new root) |
| Usage prototype | `SET ENV <varname> <value>` |

```csharp
[CommandRoot("SET", "Configuration, tunables and profiles")]
[CommandRegister("Env", "Set a shell environment value", Prototype = "SET ENV <varname> <value>")]
[CommandParameterOrdered("key", "Key used to access value")]
[CommandParameterOrdered("value", "Value stored for accessing", UsePipe = true)]
[CommandHelpRemarks("This command modifies the shell environment outside its own context.")]
[CommandHelpRemarks("Piped input is accumulated: the variable is cleared, then every chunk is appended.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `key` | ordered | `string` | yes | — | any environment variable name | Variable to write |
| `value` | ordered, `UsePipe = true` | `string` | yes (unless piped) | — | any text | Value, or the accumulated pipe contents |

**Pipeline behaviour** — **accepts, produces nothing visible.** Exactly the framework built-in's contract: `OnStartPipe` clears the variable; each chunk **appends** to it; every result is an empty success, which the host drops, so the tool is silent. Non-piped invocation writes once and returns an empty success. `ResultFormat.General`.

**Environment interaction** — writes an arbitrary **global** environment key, so it **must** be registered with `modifiesEnvironment: true` — this is the one tool in the package whose whole purpose is a global write. It reads nothing.

**Failure modes** — missing key or value with no pipe: parse-time required-parameter failure (the executor's generic message; this matches the built-in exactly and the prototype is the cue). Nothing else can fail.

**Security and audit** — a user can put anything in an environment variable, including a token. Environment writes **are** audit-logged by the framework's `EnvironmentContext.SetValue` → `LogEnvironmentChange`, and that path *does* redact by variable name — so `SET ENV CHATDBG_AZURE_API_KEY …` is masked in the audit record even though the command-line parameter is not (§2.6.7). The help text warns that the value is visible in shell history regardless.

**Traceability** — PRD **7.1**. Descends from the **framework's** built-in `SET` (`Xcaciv.Command/Commands/SetCommand.cs`), not from ChatDbg. It exists solely so that claiming `SET` as a root command costs the user nothing (§2.1.1).

---

### 2.5 Pipeline compositions

Six worked examples. The pipeline parser splits on `|`; each stage runs in its own child environment and its own child IO context, and chunks stream lazily through a bounded channel, so a long listing reaches the next stage before the first stage finishes.

**1. What have I actually changed?**

```
SET KEYS -changed -format csv | REGIF "llama"
```

`SET KEYS` emits one CSV row per key whose current value differs from the built-in default; the framework's built-in `REGIF` passes through only rows matching `llama` and returns empty success for the rest, which the host drops. **The user gets** a short list of exactly the local-model tunables they have drifted from stock — the fastest possible answer to "why is my GGUF rig slow and my colleague's is not". In the source this required reading a 46-line dump and remembering eight defaults.

**2. Validate the live record before blaming the provider.**

```
SET SHOW -format json | SET VALIDATE -strict
```

Stage one emits the active record as one JSON chunk with the document's wire names. Stage two treats that chunk as a complete document, runs every check in §2.4.8, and — because `-strict` is set — returns a **failure** if anything is wrong, with the finding list as output. **The user gets** silence when the configuration is sound and a precise finding list when it is not: `provider 'Azure' will not match any known provider; use 'azure'`, `modelId '/models/mistral.gguf' does not exist`, `useOsCredentialStore is true but no store is available on linux`. This is the composition a host should run at startup in place of the source's ad-hoc self-check.

**3. Which of my profiles have drifted from what I am running?**

```
SET PROFILES | SET DIFF -against active -format csv
```

`SET PROFILES` emits one bare profile name per chunk. `SET DIFF` treats each chunk as a left-hand side, compares it to the active configuration, and emits one row per differing key — **and emits nothing at all for a profile that matches**. **The user gets** a compact drift report across every saved profile, with the identical ones silently absent.

**4. Promote a profile after checking it, without switching to it.**

```
SET SHOW -profile gpu-rig -format json | SET IMPORT -profile staging -merge -dryrun
```

Stage one renders a profile the user is *not* running. Stage two validates that document against the key catalog and reports exactly which keys in `staging` would change — `imported llamaGpuLayerCount: 0 -> 32` — **and writes nothing**, because of `-dryrun`. **The user gets** a reviewable change list before committing. Drop `-dryrun` to apply it.

**5. Cross-package — resolve the configured system prompt end to end.**

```
SET GET systemPrompt | PROMPT SHOW
```

`SET GET` emits the bare prompt **name** with no label — which is why it is designed to emit a bare scalar. `PROMPT SHOW` (package `ChatDbg.Tools.Prompts`, PRD 7.5) takes one prompt name per chunk and emits that prompt's body. **The user gets** the actual text their next chat turn will be primed with. The source could not do this: the prompt body was resolved once at startup, never persisted, and never printable from the settings surface.

**6. Cross-package — configuration handed to a diagnosis.**

```
SET SHOW -changed -format json | CHAT ASK "Why would this configuration produce empty log-probability output?"
```

Stage one emits only the drift, as JSON, with every credential rendered as status-and-provenance and no value anywhere. Stage two (`ChatDbg.Tools.History` / session package, PRD 7.4 and 7.6) sends it as context alongside the question. **The user gets** a model answer grounded in their real configuration — and, because `SET SHOW` cannot emit a secret under any flag, a configuration they can paste into a bug report or a chat with a stranger without auditing it first. That property is a design constraint of this package, not a happy accident: it is why the credential rows are provenance-only in every one of the four output formats.

---

### 2.6 Design notes for the architect

#### 2.6.1 The state this package holds — and the one it must not

**Holds:** one in-memory settings record per process, loaded once, plus the resolved paths and the active profile name. That is all.

**Must not hold:** a shared mutable object handed out to other packages. The source made the settings record "the integration bus between commands" — every command was constructed with a reference to one `ChatSettings` instance — and it produced the product's worst bug: the windowed shell built its commands against a defaults record and then rebound the *name* to the loaded record, so `/set`, `/model`, `/logprobs` and `/prompt use` all mutated and persisted an object the UI never read, silently overwriting the user's document with mostly-default values on every command (quirk Q4, and its visible symptom in the Change Model dialog, Q26). Rebinding a name does not rebind captured references.

The rebuild's answer is that **the environment is the bus**. This package publishes the effective configuration into the controller environment as `CHATDBG_*` keys on every successful write, and every other package reads its configuration from `IEnvironmentContext` with a documented default — `env.GetValue("CHATDBG_TEMPERATURE", "0.7", storeDefault: false)`. That gives three properties the source lacked: there is exactly one writer; a reader that starts late still gets the current value; and no reader can mutate what it read. Note `GetValue`'s `storeDefault` defaults to **true**, which would flip `HasChanged` and cause a write-back on a mere read — every consumer must pass `storeDefault: false`, and that belongs in the cross-package conventions chapter.

The six tools that publish (`SET VALUE`, `SET UNSET`, `SET RESET`, `SET IMPORT`, `SET PROFILE`, and `SET ENV`) must be registered by the host with `modifiesEnvironment: true`; the other ten must not be. `ModifiesEnvironment` is a property of the *registration*, not of the class — there is no attribute for it — so this package must ship a documented registration manifest and the host's composition root must honour it. A tool registered without the flag can still persist its own state under a `SET_`-prefixed key; that is how the read-only tools cache resolved paths.

#### 2.6.2 One validator, three surfaces

The source had the same bound written out in up to four places with three different wordings: `/set logProbabilitiesTopK 0` said `LogProbabilitiesTopK must be a number between 1 and 20` while `/logprobs top 0` said `Top-K value must be a number between 1 and 20`, and the windowed dialog silently clamped instead of saying anything. The rebuild has exactly one key catalog (§2.3), one validator that consumes it, and one message per key. `LOGPROB`'s convenience verbs, the future GUI, and `SET VALUE` all call the same validator and produce the same sentence. `SET KEYS` emits that catalog at runtime so help text cannot drift from behaviour.

#### 2.6.3 Testability

- **The store is a port.** `ISettingsStore` has the six operations the source's persistence interface had — save, load, report path, and the three that were credential-flavoured are *gone* from this package (they moved to `CRED`, which is where their interactive terminal I/O belongs). What remains is pure: load, save, resolve path, list/read/write/delete profile. No tool touches `System.IO` directly, so every tool is unit-testable against an in-memory store, and the source's "did it save exactly once" assertions remain expressible verbatim.
- **No terminal I/O inside a service.** The source's settings service read `Console.ReadLine()` inside migration and keystore-enable flows, which meant those flows were invisible and unanswerable under the windowed shell (its Migrate button called straight into a blocking stdin read while the UI owned the screen). Nothing in this package prompts. Confirmation is a **flag** (`-force`) plus an environment switch (`SET_CONFIRM`), which works identically in a REPL, a full-screen shell, a pipeline and a test.
- **The clock and the platform are ports too** — profile `lastUsed` timestamps and the "is an OS credential store available" predicate both come through injected abstractions, so a test can pin them.
- **Parameter parsing is the framework's**, not the package's. There is no hand-rolled `string.Split`; the source's ad-hoc splitting is what produced the "value is everything after the key, re-joined with single spaces, runs of spaces collapse" quirk. That behaviour is preserved deliberately (it is what a suffix parameter does), but now it is the framework's documented behaviour rather than an accident.

#### 2.6.4 Reject or clamp — one policy, made visible

The source ran two contradictory out-of-range policies: text commands rejected, the windowed dialog clamped to the same bounds and saved silently, and additionally ignored unparseable input without a word. A user who typed `5` into the temperature box got `2` and no notification; the same `5` typed at the prompt got an error.

The rebuild picks **reject** as the default (`SET_STRICT=true`) and makes clamping an explicit, always-reported opt-in (`-clamp`, or `SET_STRICT=false` for a host that wants dialog-like behaviour globally). Unparseable input is **never** silently ignored under either policy. A GUI built on this package therefore behaves the same as the prompt unless it deliberately sets `SET_STRICT=false`, and even then the user is told about every clamp.

#### 2.6.5 Cross-platform behaviour

Everything in this package is portable except the one predicate it delegates. Concretely:

| Capability | Windows | Linux / macOS |
|---|---|---|
| Settings document location | `%USERPROFILE%\.ChatDbg\settings.json` | `$HOME/.ChatDbg/settings.json` — the same literal dotted directory name, which ports unchanged |
| Base-directory fallback | OS temp directory when the profile lookup is empty or throws | identical |
| Every range, default, message and format | identical | identical |
| `useOsCredentialStore` | `CRED` reports available (Credential Manager) | `CRED` reports available where a Secret Service or Keychain backend is present; otherwise the key can be read but not set to `true`, with the message `No OS credential store is available on this platform (<os>).` |
| A document carrying `useWindowsCredentialManager: true` copied from Windows to Linux | n/a | `SET VALIDATE` reports it as a finding and `SET SHOW` renders `- OS Credential Store: Enabled (no store on this platform)`. It does **not** silently keep warning on every launch forever as the source did, and it does not auto-clear the user's setting either — a machine-specific fact must not rewrite a portable document. |
| Path separators, case sensitivity | joined platform-neutrally; profile names are matched case-insensitively and stored lowercase so a profile set does not fracture between an NTFS and an ext4 home directory | identical |

The source's windowed dialog was the one place that skipped the platform gate entirely — it wrote the keystore toggle with no check, and its credential sub-dialog reported `Credential saved successfully` even where no keystore existed (quirks Q18, Q19). There is no surface in this package that can turn the toggle on where the capability is absent.

#### 2.6.6 Where to degrade rather than fail

Degrading is the default posture; failing is reserved for a request that cannot be honoured.

| Condition | Behaviour |
|---|---|
| Settings document absent | Create it with all built-in defaults, silently, on first resolution. Source parity. |
| Settings document corrupt or unparseable | **Degrade.** Read tools succeed against built-in defaults with a visible one-line warning; the corrupt file is left untouched, never deleted, never auto-rewritten. A write tool refuses until the user runs `SET RESET all -force -backup` or `SET IMPORT`, so a fat-fingered `SET VALUE` cannot silently discard a document the user could still repair by hand. This is a deliberate tightening: the source would happily overwrite the corrupt file on the next successful `/set`. |
| User profile directory unresolvable | **Degrade** to the OS temp directory and say so in `SET PATH`. Source parity. |
| Base directory read-only (`SET_READONLY=true`, or a write that fails with a permission error) | **Degrade to read-only mode**: the six read tools work normally; the ten mutating tools return one clear failure explaining that the change is active for this session only. Never a crash, never a silent success. |
| Profile directory missing | **Degrade**: the world contains exactly one profile, `default`, which is the settings document itself. |
| `ChatDbg.Tools.Prompts` not loaded | **Degrade**: `systemPrompt` names are stored unvalidated, with a one-line note. Source parity — the source did exactly this when the prompt service was absent. |
| `ChatDbg.Tools.Credentials` not loaded | **Degrade**: the credential section of `SET SHOW` renders `(provenance unavailable — CRED tools not loaded)` and `useOsCredentialStore` cannot be set to `true`. Configuration viewing must never depend on the credential package being present. |
| A capability is absent on the current back end (e.g. log probabilities on a provider that cannot return them) | **Store it anyway, and annotate.** `SET VALUE enableLogProbabilities true` succeeds and appends `Note: the active provider (<p>) does not report token probabilities.` This package's job is to record intent; the provider package's job is to report capability. Refusing to store a value because today's provider ignores it would make profiles useless — a profile is written for the back end it targets, not the one currently selected. The three local-model tunables the runtime does not yet consume are annotated the same way. |
| Whole-record reset, profile deletion, non-clean import | **Fail closed** without `-force`. These are the only four places the package refuses to act on a well-formed request. |

#### 2.6.7 Audit and masking — what the framework will not do for you

`AuditMaskingConfiguration` only rewrites tokens of the form `-name=value`; the framework's own `-name value` syntax is **not** masked, and the argument tokenizer strips `=` anyway. Treat command-line parameter masking as non-functional. Three consequences are baked into this package's design:

1. **No tool accepts a secret as a parameter.** That is enforced by the write-blocked keys, and it is the reason `SET VALUE azureApiKey …` fails rather than storing.
2. **Environment-change auditing does work**, because `StructuredAuditLogger.LogEnvironmentChange` redacts by *variable name*. So `SET ENV` is genuinely masked where a hypothetical `SET VALUE apikey` would not have been.
3. The package still contributes `azureApiKey`, `awsAccessKey`, `awsSecretKey` to `RedactedParameterNames` — belt and braces for a host that fixes the masker later — and records the `azureEndpoint` value in audit metadata **hashed**, since an endpoint occasionally carries an embedded token.

The host should enable `StructuredAuditLogger` and assign it explicitly to the controller; DI registration alone does not push it onto `CommandController`, which initialises its own `NoOpAuditLogger`.

#### 2.6.8 Loading, registration and packaging

- The assembly references `Xcaciv.Command.Interface` and `Xcaciv.Command.Core` only, and must **not** ship a private copy of the interface assembly — the crawler reports exactly that as a `ReflectionTypeLoadException` cause.
- Under `Xcaciv.Loader`, this package needs no elevated policy: load it with a per-instance `AssemblySecurityPolicy` carrying the host's forbidden-directory list and `DisallowDynamicAssemblies = true`, `basePathRestriction` set to the tools directory (**never** `"*"`), `learningMode: false` on the integrity verifier, discovery by contract (`GetTypes<ICommandDelegate>()`) rather than by class name, and one collectible context per package disposed deterministically.
- The registration manifest the host must honour: 16 sub-commands under root `SET`; `modifiesEnvironment: true` for `VALUE`, `UNSET`, `RESET`, `IMPORT`, `PROFILE`, `ENV`; `false` for the rest. Load order matters exactly once: built-ins first, this package second (§2.1.1).
- **Do not register these commands as DI singletons.** `SET VALUE`, `SET UNSET` and `SET IMPORT` hold per-pipe transaction state across `OnStartPipe` → `HandlePipedChunk` → `OnEndPipe`; a singleton instance would leak that state between runs and between pipeline stages. Transient only.
- `CommandExecutor` does **not** dispose the command instance it executes. Any tool that opens a write transaction must therefore also close it in `OnEndPipe` and in the non-piped path — never rely on `DisposeAsync` to flush a pending document write.

#### 2.6.9 Open questions worth a decision before implementation

1. **Environment prefix for sub-commands.** The framework seeds `GetDefaultEnvironment()` values under the command name and hands the child context keys prefixed `{COMMANDNAME}_`; for a sub-command it is not settled in the reference whether that name is the root (`SET`) or the sub-command (`SHOW`). This spec assumes the root, and every tool reads defensively — `SET_FILE` first, bare `FILE` second — until the host pins it with a test.
2. **YAML.** `-format yaml` costs a `YamlDotNet` dependency in a package that otherwise has none. Ship JSON and CSV in v1; add YAML only if a real user asks.
3. **Profile inheritance.** A `base` field on a profile document (profile B inherits A and overrides three keys) is the obvious next step and is deliberately *not* in v1: it turns validation into a graph problem and `SET DIFF` into a three-way merge. Revisit once profiles have users.

---

## 3. ChatDbg.Tools.CredentialsSecretStorage — Credentials & Secret Storage

> Root command: **`CRED`** · Package: `ChatDbg.Tools.CredentialsSecretStorage`
> Behavioural source of truth: `dossiers/credential-management.md` (feature 6 of the inventory).
> Framework contract: `synthesis/ref-command.md` (Xcaciv.Command **3.3.4**). Host pattern: `synthesis/ref-cupcake.md` §8. Loading: `synthesis/ref-loader.md` §10.

---

### 3.0 Purpose and boundary

**What this package owns.**

`CRED` owns the *identity, provenance, custody and disclosure* of the long-lived provider secrets ChatDbg needs to talk to a hosted OpenAI-compatible service and to a cloud model-inference service. Concretely it owns:

1. **The credential slot registry** — the fixed set of logical secrets (`azureApiKey`, `awsAccessKey`, `awsSecretKey`), the environment-variable names that supply each one and their order, the OS-keystore entry names, and the deprecated settings-file field names.
2. **The resolution algorithm** — the strict three-tier priority (process environment → enabled OS secret store → deprecated settings-file field), evaluated fresh on every read, with empty-string treated as absent at every tier.
3. **Provenance reporting** — answering *which channel supplied this value* without ever emitting the value.
4. **Custody operations** — writing, rotating and deleting a secret in an OS-managed store, across Windows, macOS and Linux.
5. **Migration and hygiene** — moving a user off plaintext-in-a-settings-file, scanning for plaintext leakage, and masking secrets that would otherwise transit a pipeline or a log.
6. **Masking policy** — the single authority on what a masked secret looks like (`***set***`, `(not set)`, `[REDACTED]`) and on the rule that no tool in the product prints a live secret value.

**What this package explicitly does NOT own.**

| Not owned | Owned instead by |
|---|---|
| The settings record itself, its schema, its file path, its serialization, its validation ranges, and the `/set`-style generic key/value surface | **`ChatDbg.Tools.SettingsConfiguration`** (root `SET`) — PRD 7.2. `CRED` reads the credential-relevant members of that record through the host environment and never serializes `settings.json` itself. |
| Provider selection, endpoint/region configuration, model identifiers, and the actual request signing or `api-key` header emission | **`ChatDbg.Tools.Providers`** (root `AI`) — PRD 7.6, and the Bedrock/marketplace package — PRD 7.7. `CRED` hands them a resolved string and nothing else. |
| Local model file paths and GGUF loading (which need no secret at all) | **`ChatDbg.Tools.LocalInference`** (root `LLAMA`) — PRD 7.8. |
| Log sinks, log files and log export | **`ChatDbg.Tools.DiagnosticLogging`** (root `LOG`) — PRD 7.11. `CRED REDACT` is a *filter* those logs pass through; it owns no sink. |
| Terminal rendering, colour, heat-maps and dialogs | **`ChatDbg.Tools.OutputRendering`** (root `OUT`) — PRD 7.12 — and the two shells, PRD 7.13 / 7.14. |
| Command dispatch, help generation, the pipeline itself | The host + Xcaciv.Command — PRD 7.1. |

**The boundary rule that matters most:** the source product's `SetCommand` reached across this line — it was 464 lines that owned settings keys *and* credential semantics, and its credential paths re-loaded and re-wrote the whole settings file behind the caller's back (dossier Q7). The rebuild cuts that: `CRED` mutates **no** file owned by `SET`. It publishes its own state into its own environment bucket and lets the settings package mirror it.

---

### 3.1 Package manifest

| Property | Value |
|---|---|
| Assembly / package name | `ChatDbg.Tools.CredentialsSecretStorage` (`ChatDbg.Tools.CredentialsSecretStorage.dll`) |
| Root command | `CRED` (`[CommandRoot("CRED", "Credential and secret storage")]` on every tool class) |
| Contract assemblies targeted | `Xcaciv.Command.Interface` **3.3.4**, `Xcaciv.Command.Core` **3.3.4** (`AbstractCommand`, `IResult<string>`, `CommandResult<T>`, the seven class-targeted attributes, `IParameterValue`). No reference to the host. |
| Target framework | `net10.0` (matches the source product's pinned `net10.0`; a `net8.0` asset is buildable from the same sources if the host is pinned to the framework's .NET 8 opt-in) |
| Elevated trust required | **No.** Every operation runs with the interactive user's own rights. No administrator/root path exists, no elevation is requested, and nothing this package does needs it — OS keystores are per-user by construction. |
| Network reach | **None.** `CRED` never opens a socket. It resolves secrets; the provider packages spend them. A `CRED` tool that made a network call would be a defect. |
| Filesystem reach | Read of the settings directory (default `<user profile>/.ChatDbg/`, temp-directory fallback) for the deprecated plaintext tier and for `CRED SCAN`; read/write of `<settings dir>/secrets.protected` **only** when the `encfile` backend is selected. No write to `settings.json`. |
| OS keystore reach | Windows Credential Manager (generic credentials); macOS Keychain (generic passwords); Linux Secret Service / libsecret (default collection). Per-user scope only. |
| Native libraries | `advapi32.dll` (`CredReadW`/`CredWriteW`/`CredDeleteW`/`CredFree`), `Security.framework` (`SecItem*`), `libsecret-1.so.0`. **These live in three satellite backend assemblies, not in the tool assembly.** The tool assembly is pure managed code with no P/Invoke. |
| Safe to load in a restricted host | **Yes, for the tool assembly.** It contains no dynamic code generation and no interop, so it survives `DisallowDynamicAssemblies = true` and passes a preflight-enabled policy. The three native backend satellites will *not* pass `AssemblySecurityPolicy.Strict` preflight — that is expected and designed for: when a backend cannot be loaded, `CRED` reports that store as unavailable with a reason and keeps working on the environment and file tiers. See §3.4 "Degradation". |
| Loader posture | One `AssemblyContext` per package, `isCollectible: true`, `basePathRestriction: <toolsRoot>` (never `"*"`), integrity verifier with `learningMode: false`, discovery by contract via `GetTypes<ICommandDelegate>()` — per `ref-loader.md` §10 steps 1–9. |
| Registration paths supported | All three of `ref-cupcake.md` rule 26: dropped into the package directory (each tool class has a public parameterless constructor); registered in-process by the shell under a host package key; or resolved through `Xcaciv.Command.DependencyInjection` with an injected `ISecretStore`. |
| `ModifiesEnvironment` | **No tool in this package requires it.** Every key `CRED` writes carries the `CRED_` prefix and therefore lands in this root's private bucket without global-write permission. A host that *wants* the store flag visible as a global variable may register `CRED ENABLE`/`CRED DISABLE` with `modifiesEnvironment: true`; this specification recommends against it. |

**Credential slot registry (preserved verbatim from the source).**

| Slot id (canonical, camelCase) | Write-path aliases | Process env vars, **in order** | Store entry name | Deprecated settings field |
|---|---|---|---|---|
| `azureApiKey` | `azure` | `CHATDBG_AZURE_API_KEY` | `ChatDbg:AzureApiKey` | `azureApiKey` |
| `awsAccessKey` | `awsaccess` | `CHATDBG_AWS_ACCESS_KEY`, then `AWS_ACCESS_KEY_ID` | `ChatDbg:AwsAccessKey` | `awsAccessKey` |
| `awsSecretKey` | `awssecret` | `CHATDBG_AWS_SECRET_KEY`, then `AWS_SECRET_ACCESS_KEY` | `ChatDbg:AwsSecretKey` | `awsSecretKey` |

Slot ids are matched case-insensitively (source lower-cases before matching; `awsAccessKey` and `AWSACCESSKEY` both work). The Azure key has exactly **one** environment variable — there is no vendor-standard alias for it. Within tier 1 the product-specific name always beats the vendor-standard name.

**Store backends.**

| Backend id | Platform | Entry identity | Notes |
|---|---|---|---|
| `wincred` | Windows | Generic credential (type `1`), target `ChatDbg:<Slot>`, blob = secret as **UTF-16LE**, blob size in **bytes** (2× character count), persistence **`2` = local machine**, user name `ChatDbg`, comment `ChatDbg API Credential`, flags `0` | Byte-for-byte compatible with the source product; entries written by the source are read by the rebuild and vice-versa. A zero-length blob reads back as *absent*, not as empty string. |
| `keychain` | macOS | Generic password, service `ChatDbg`, account `<Slot>` | **NEW** — see §3.4 "Deliberate improvements", item 4. |
| `secretservice` | Linux | Secret Service item in the default collection, attributes `application=ChatDbg`, `slot=<Slot>` | **NEW**. Requires a running secret-service provider (gnome-keyring, KWallet's SS bridge, `keepassxc`); absence is reported, not fatal. |
| `encfile` | any | `<settings dir>/secrets.protected`, per-user-scoped OS data protection where the platform offers it, otherwise a passphrase-derived key | **NEW** — the "encrypted credential files (future enhancement)" the source's docs promised and never built (dossier Q31). Explicitly labelled *weaker than a keystore* everywhere it appears. |
| `none` | any | — | Store tier disabled. The default. |

**Compatibility note on the legacy flag.** The source's boolean `useWindowsCredentialManager` (default **`false`**) is preserved in the settings record and continues to serialize. `CRED` reads and writes it through the host environment key `CRED_STORE_ENABLED`; the settings package mirrors the two. When `CRED_STORE_BACKEND` is `wincred` the two are exactly equivalent, so an old settings file keeps working unchanged.

---

### 3.2 Tool catalog

**Conventions that apply to every tool in this package.** Stated once here rather than repeated thirteen times.

* **All parameter attributes are class-targeted.** `[CommandParameterOrdered]`, `[CommandParameterNamed]`, `[CommandFlag]`, `[CommandParameterSuffix]` are `AttributeTargets.Class, AllowMultiple = true, Inherited = false`. They are declared on the tool class, never on a property or field.
* **Ordered parameters are `IsRequired = true` by default.** Where a slot id is optional, the attribute says `IsRequired = false` explicitly.
* **A secret value is never a command-line parameter.** Two independent framework facts force this and they are not negotiable:
  1. The argument tokenizer scrubs arguments to `[-_0-9A-Za-z .*?\[\]|"~!@#$%^&*()]`, deleting `/`, `\`, `;`, `<`, `>`, `` ` ``, `=`, `+`, `,`, `:` and `{}` **before the tool sees them** (`NamesValidator.cs:22,51-60`). Base64 and URL-safe API keys would be silently mangled — a far worse failure than the source's whitespace collapsing.
  2. `AuditEvent.Parameters` is populated from `ioContext.Parameters` verbatim, and `AuditMaskingConfiguration.ApplyMasking` only rewrites `-name=value` tokens — the framework's own `-name value` form is **not masked**. Any secret typed as a parameter is written to the audit log in the clear.
  Therefore every tool that ingests a secret takes it **from the pipe** or **from a no-echo prompt**, and from nowhere else. This also repairs the source's defect where `/set wincred <type> <value>` echoed secrets to the terminal and into shell history, and where runs of two or more spaces inside a secret collapsed to one and tabs/newlines were untypeable.
* **No-echo prompting.** `IIoContext` offers only `Task<string> PromptForCommand(string prompt)`. The host's IO context (the `AbstractTextIo` subclass required by `ref-cupcake.md` rule 18) MUST honour the convention that a prompt string beginning with the sentinel `secret:` is read with terminal echo suppressed and is never written to the shell's own history. Where echo cannot be suppressed (a redirected stdin, a terminal that refuses raw mode, the full-screen shell before its driver is up), the tool emits the warning `Terminal echo cannot be suppressed on this input; the value you type will be visible.` and requires the `-force` flag to continue. This is a **host requirement**, recorded here because the package cannot enforce it alone.
* **Masking tokens.** `***set***` and `(not set)` in status output; `[REDACTED]` in audit and redaction output. These three literals are the package's contract with the rest of the product.
* **Source labels**, verbatim from the source and asserted by its tests: `environment variable (<VARNAME>)` — always naming the actual variable that won — `Windows Credential Manager`, `settings file (deprecated)`, `not set`. **NEW** labels added for the generalized backends: `macOS Keychain`, `Secret Service`, `encrypted file (weak)`, and the diagnostic-only `provider SDK ambient chain (unverified)`.
* **Empty is absent.** At every tier, an empty string is treated as no value and resolution falls through. A zero-length store blob is absent. Only the deprecated file tier returns its stored string unconditionally, including empty — preserved from the source.
* **`AllowedValues` is used only for closed enumerations, never for slot ids.** The attribute's initialiser silently promotes `AllowedValues[0]` to `DefaultValue` when no default was declared, so an ordered parameter with an allow-list can quietly acquire a default the author never intended — a `CRED SET` that defaulted to `azureApiKey` because the argument went missing would be a genuine hazard. Slot ids are therefore validated inside the tool and rejected with the source's own message pair (`Unknown credential type: <as typed>` / `Valid types: azureApiKey, awsAccessKey, awsSecretKey`), while closed sets like `-format`, `-store`, `-to` and `-tier` use `AllowedValues` with an explicit `DefaultValue` and get parse-time enforcement for free. Note that allow-lists are enforced for ordered and named parameters but **not** for suffix parameters — no tool in this package uses a suffix parameter.
* **Zero-argument invocations inject nothing.** `AbstractCommand.ProcessParameters` early-returns an empty dictionary when `io.Parameters.Length == 0`: no defaults are applied, no flags materialise, no field injection runs. Every tool below is specified to fall back to its declared default in that case, and its field initialisers carry the same default.
* **Failures are data.** Tools return `CommandResult<string>.Failure(message, ex)`; they do not throw. A thrown exception would be reduced by the host to `Error executing CRED (see trace for more info)` and the user would lose the remediation text.
* **Confirmation.** Destructive tools (`CRED REMOVE`, `CRED ROTATE`, `CRED MIGRATE -purge`, `CRED SET` over an existing entry) prompt for confirmation via `PromptForCommand`. The **only** affirmative answers are `y` and `yes`, compared after lower-casing — preserved exactly from the source (`Y`/`YES` work; `yeah`, `1`, `true` do not). A `-yes` flag pre-answers the prompt for non-interactive use; when a tool is running as a pipeline stage and no `-yes` was given, it refuses rather than blocking on a prompt nobody can answer.

---

#### 3.2.1 `CRED LIST` — masked status of every credential slot

| Field | Value |
|---|---|
| Command | `LIST` |
| Root command | `CRED` |
| Description | List every credential slot with a masked status and the channel it resolved from. |
| Usage prototype | `CRED LIST [<slot>] [-format table\|csv\|json] [-profile <name>] [-showempty] [-nocache]` |

```csharp
[CommandRoot("CRED", "Credential and secret storage")]
[CommandRegister("LIST", "List credential slots with masked status and resolved source",
    Prototype = "CRED LIST [<slot>] [-format table|csv|json] [-profile <name>] [-showempty] [-nocache]",
    Version   = "1.0.0")]
[CommandParameterOrdered("slot", "Credential slot to report; omit for all three",
    IsRequired = false, UsePipe = true)]
[CommandParameterNamed("format", "Output shape",
    DefaultValue = "table", AllowedValues = new[] { "table", "csv", "json" })]
[CommandParameterNamed("profile", "Credential profile namespace", DefaultValue = "default")]
[CommandFlag("showempty", "Include slots that resolved to nothing", ShortAlias = "e")]
[CommandFlag("nocache", "Bypass the per-invocation resolution cache")]
[CommandHelpRemarks("Never prints a secret value. Status is exactly '***set***' or '(not set)'.")]
public sealed class CredListCommand : AbstractCommand { /* ... */ }
```

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `slot` | ordered | `string` | no (`IsRequired = false`) | *(none — all three slots)* | `azureApiKey`, `awsAccessKey`, `awsSecretKey`, plus aliases `azure`, `awsaccess`, `awssecret`; matched case-insensitively | Credential slot to report; omit for all three. Fed by the pipe when piped (`UsePipe = true`). |
| `format` | named | `string` | no | `table` | `table`, `csv`, `json` | Output shape. `csv` emits one row per slot with a header; `json` emits one object per slot. |
| `profile` | named | `string` | no | `default` | any name matching `[-_0-9A-Za-z]{1,32}` | **NEW.** Credential profile namespace (§3.4 item 8). `default` maps to the legacy un-namespaced entry names. |
| `showempty` | flag | `bool` | no | `false` | — | Include slots whose resolved value is empty. Without it, `(not set)` slots are still listed in `table` mode (matching the source's fixed three-line block) but suppressed in `csv`/`json` so downstream filters see only live slots. |
| `nocache` | flag | `bool` | no | `false` | — | **NEW.** Force a fresh probe of every tier for every slot, defeating the per-invocation cache. |

**Output, `table` mode** — the source's block, preserved line-for-line so transcript tests keep passing:

```
Credentials (secure):
- Azure API Key: ***set*** [environment variable (CHATDBG_AZURE_API_KEY)]
- AWS Access Key: (not set) [not set]
- AWS Secret Key: ***set*** [Windows Credential Manager]
- Secret store: Enabled (wincred)
```

The fourth line replaces the source's `- Windows Credential Manager: Enabled|Disabled` and names the active backend; on a host where the flag is on but the backend cannot answer it reads `Enabled (wincred, unavailable: not supported on this platform)`.

* **Pipeline behaviour** — **both**. Accepts piped input: one chunk is one slot id (or alias); the tool resolves that slot and emits one status row per chunk, so `CRED SCAN | CRED LIST` annotates whatever the scanner found. Produces piped output: one chunk per slot row. Declares `ResultFormat.CSV` when `-format csv`, `ResultFormat.JSON` when `-format json`, otherwise `ResultFormat.General` — the format is metadata riding on each chunk (the framework never branches on it; the host's `HandleOutputChunk` renders on it).
* **Environment interaction** — **reads** the process variables `CHATDBG_AZURE_API_KEY`, `CHATDBG_AWS_ACCESS_KEY`, `AWS_ACCESS_KEY_ID`, `CHATDBG_AWS_SECRET_KEY`, `AWS_SECRET_ACCESS_KEY`. **Reads** the host environment keys `CRED_STORE_ENABLED`, `CRED_STORE_BACKEND`, `CRED_SETTINGS_PATH`, `CRED_PROFILE` (all with `storeDefault: false` — a pure read that must not flip `HasChanged`). **Writes** nothing. Needs no environment-modifying permission.
* **Failure modes** — an unrecognised `slot` yields `CommandResult<string>.Failure("Unknown credential type: <as typed>")` followed by the source's second line `Valid types: azureApiKey, awsAccessKey, awsSecretKey`; the tool echoes the user's original casing, as the source did. A store that throws is reported as a distinct fourth outcome, `[store error: <reason>]`, instead of the source's silent fall-through to the next tier — the user sees that the vault failed rather than concluding the entry does not exist. A failure chunk arriving from upstream is forwarded verbatim by `AbstractCommand.Main` and never reaches this tool's chunk handler. Zero arguments is a valid invocation (all three slots, `table`).
* **Security and audit** — no parameter and no output carries a secret; the only masked artefacts are the two status tokens. `AuditEvent.Parameters` for this tool is safe to log verbatim. Not destructive; no confirmation.
* **Traceability** — PRD **7.3 Credential Management**. Descends from the credential block of bare `/set` (`SetCommand.cs:401-463`, dossier B3) and from `ChatSettings.GetCredentialSource` (B2). `-profile`, `-nocache`, `-showempty`, `csv`/`json` output and the `[store error]` outcome are **NEW**.

---

#### 3.2.2 `CRED SOURCE` — provenance of one credential, and nothing else

| Field | Value |
|---|---|
| Command | `SOURCE` |
| Root command | `CRED` |
| Description | Report which channel supplied a credential, without disclosing its value. |
| Usage prototype | `CRED SOURCE <slot> [-profile <name>] [-all] [-quiet]` |

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `slot` | ordered | `string` | **yes** | — | the three slot ids + three aliases, case-insensitive | Credential slot to trace. Supplied by the pipe when piped (`UsePipe = true`). |
| `profile` | named | `string` | no | `default` | `[-_0-9A-Za-z]{1,32}` | **NEW.** Profile namespace to probe. |
| `all` | flag | `bool` | no | `false` | — | **NEW.** Report *every* tier that holds a value, in priority order, marking the winner — instead of only the winner. Values are never shown; each tier reports `holds a value` or `empty`. |
| `quiet` | flag | `bool` | no | `false` | — | **NEW.** Emit only the bare label (`environment variable (CHATDBG_AZURE_API_KEY)`) with no slot prefix, for scripting. |

**Output.** Default: `Azure API Key: environment variable (CHATDBG_AZURE_API_KEY)`. Unrecognised slot: the literal `not set` — the source's behaviour, which answers `not set` rather than erroring for an unknown type name, and which a test pins by asserting only that a winning environment tier's answer *contains* the phrase `environment variable`, case-insensitively. With `-all`:

```
azureApiKey
  1 environment variable (CHATDBG_AZURE_API_KEY)  holds a value   <- winner
  2 Windows Credential Manager                    empty
  3 settings file (deprecated)                    holds a value
```

That third line is a genuine finding — it means a plaintext copy is still on disk — and it is the input `CRED MIGRATE` wants.

* **Pipeline behaviour** — **both**. One piped chunk is one slot id; one output chunk per input chunk. `ResultFormat.General` by default, `ResultFormat.CSV` under `-all` (tier, label, occupancy, winner). Chaining `CRED LIST -format csv | CRED SOURCE -all` is the intended provenance audit.
* **Environment interaction** — identical read set to `CRED LIST`. Writes nothing.
* **Failure modes** — unknown slot → the literal `not set` as a **success** result (source-preserving; use `CRED LIST` if you want a hard error on a typo). Store throws → the tier line reads `store error: <reason>` and the probe continues to the next tier, so the winner is still computed. Missing prerequisite is impossible: this tool has none. Upstream failure chunks pass through untouched.
* **Security and audit** — no secret in any parameter or in any output. Safe to log verbatim. Not destructive.
* **Traceability** — PRD **7.3**. Descends from `ChatSettings.GetCredentialSource(string)` (dossier B2, rules R39–R41) and from the startup provenance line `Azure credentials loaded from: <source>` (B13). `-all`, `-quiet` and `-profile` are **NEW**; `-all` is what closes the source's defect where a user whose secret key came from the store and whose access key came from the environment was told, flatly, that "AWS credentials" came from the environment.

---

#### 3.2.3 `CRED SET` — put a secret into a store

| Field | Value |
|---|---|
| Command | `SET` |
| Root command | `CRED` |
| Description | Store a secret in the OS secret store. The value is never typed as an argument. |
| Usage prototype | `CRED SET <slot> [-store wincred\|keychain\|secretservice\|encfile] [-profile <name>] [-yes] [-force] [-noverify]` |

```csharp
[CommandRoot("CRED", "Credential and secret storage")]
[CommandRegister("SET", "Store a secret in the OS secret store (value read from a no-echo prompt or the pipe)",
    Prototype = "CRED SET <slot> [-store <backend>] [-profile <name>] [-yes] [-force] [-noverify]",
    Version   = "1.0.0")]
[CommandParameterOrdered("slot", "Credential slot to write")]
[CommandParameterNamed("store", "Backend to write to; default is the enabled backend",
    DefaultValue = "auto",
    AllowedValues = new[] { "auto", "wincred", "keychain", "secretservice", "encfile" })]
[CommandParameterNamed("profile", "Credential profile namespace", DefaultValue = "default")]
[CommandFlag("yes", "Pre-answer the overwrite confirmation", ShortAlias = "y")]
[CommandFlag("force", "Proceed even when terminal echo cannot be suppressed")]
[CommandFlag("noverify", "Skip the read-back verification after writing")]
[CommandHelpRemarks("There is deliberately no <value> parameter. The secret arrives from a no-echo prompt, or as one piped chunk.")]
[CommandHelpRemarks("The argument tokenizer strips / \\ = + : ; and other characters from parameters; a secret passed as an argument would be silently corrupted and written to the audit log.")]
public sealed class CredSetCommand : AbstractCommand { /* ... */ }
```

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `slot` | ordered | `string` | **yes** | — | `azureApiKey`\|`azure`, `awsAccessKey`\|`awsaccess`, `awsSecretKey`\|`awssecret`, case-insensitive | Credential slot to write. The lookup lower-cases; messages echo the casing you typed. |
| `store` | named | `string` | no | `auto` | `auto`, `wincred`, `keychain`, `secretservice`, `encfile` | **NEW** (the source had only the implicit Windows store). `auto` resolves to `CRED_STORE_BACKEND`, else to the single available backend for this OS, else fails with the platform message. |
| `profile` | named | `string` | no | `default` | `[-_0-9A-Za-z]{1,32}` | **NEW.** Namespace for the entry name. `default` writes the legacy names (`ChatDbg:AzureApiKey`), any other profile writes `ChatDbg:<profile>:AzureApiKey`. |
| `yes` | flag | `bool` | no | `false` | — | **NEW.** Pre-answers the overwrite confirmation. Required when running as a pipeline stage. |
| `force` | flag | `bool` | no | `false` | — | **NEW.** Continue when the terminal cannot suppress echo. |
| `noverify` | flag | `bool` | no | `false` | — | **NEW.** Skip the read-back check. Present only for stores that are known to be write-only under policy. |
| *(the secret)* | **not a parameter** | `string` | **yes** | — | 1 … 1280 UTF-16 characters (see below) | Read from `secret:` prompt or taken as one piped chunk. |

**Pre-flight gate — preserved.** The store tier must already be enabled (`CRED_STORE_ENABLED` true). If it is not, the tool makes **no** store call at all and returns the source's message:

```
Windows Credential Manager is not enabled. Enable it first with:
/set useWindowsCredentialManager true
Or use: /set enablewincred
```

restated for the rebuild as `Secret store is not enabled. Enable it first with: CRED ENABLE`. The source pins the "no store call is made" half of this with a never-called mock assertion; that assertion survives the port unchanged and is the reason the gate is a hard requirement rather than an optimisation.

**Value validation — NEW, and a deliberate improvement.** The source performed no length, character-set or emptiness check between reading the typed value and handing it to the OS, so an oversized value surfaced only as a generic failure. This tool rejects an empty or whitespace-only value (`A secret cannot be empty.`), warns above 1200 UTF-16 characters and refuses above **1280** (the platform's documented generic-credential blob ceiling of 2560 bytes, since the blob is UTF-16LE and its declared size is in bytes), and refuses a value containing a control character other than none, naming the offending code point without printing the value. Leading, trailing and internal whitespace are preserved exactly — because the value never passes through the tokenizer.

* **Pipeline behaviour** — **accepts piped input, produces piped output**. One piped chunk is **one complete secret value**; the tool writes it to the selected backend for the `slot` given on the command line and emits one confirmation chunk. This is how a secret reaches the tool from a password manager without ever touching the terminal or the argument list. Piped mode requires `-yes` (there is no one to answer the overwrite prompt) and never prompts. Output is `ResultFormat.General`, one line: `Credential stored securely in <backend display name>: <slot as typed>` on success — the source's message shape with the backend name generalized. With `-noverify` the line gains the suffix ` (not verified)`.
* **Environment interaction** — **reads** `CRED_STORE_ENABLED`, `CRED_STORE_BACKEND`, `CRED_PROFILE`, `CRED_CONFIRM` (`storeDefault: false`). **Writes** `CRED_LAST_WRITE_SLOT` and `CRED_LAST_WRITE_BACKEND` into its own bucket for `CRED DOCTOR` to report. Both keys carry the `CRED_` prefix, so **no environment-modifying permission is needed**. It never writes a process environment variable, and it never writes `settings.json` — unlike the source, which reloaded the settings record from disk mid-command, re-emitted the plaintext warning (secrets and all), silently discarded the caller's unsaved changes, and rewrote the file.
* **Failure modes** — store not enabled → the gate message above, no store call, `Failure`. Unknown slot → `Unknown credential type: <as typed>` + `Valid types: azureApiKey, awsAccessKey, awsSecretKey`, `Failure`. Backend unavailable on this OS → `Secret store '<backend>' is not available on this platform.` (the source's exact sentiment, with the backend named), `Failure`, no state change. Empty/oversized/control-character value → the validation messages above, no write. Write refused by the OS → `Failed to store credential: <slot>` plus, **NEW**, the OS reason on a second line — the source swallowed every exception and gave the user nothing to act on. Read-back verification mismatch → `Wrote <slot> but read back a different value; the store may be shared with another tool.` and a non-zero result. Terminal echo unavailable and no `-force` → refusal with the echo warning. An upstream failure chunk is forwarded by the base class and never reaches the write path — a secret is never derived from a failed stage.
* **Security and audit** — **the value is a secret and never appears in a parameter, in output, in a prompt echo, or in an audit record.** `AuditEvent.Parameters` for this tool contains only the slot id and the flags, all of which are safe. The `slot` id is deliberately not redacted so that audits can answer "which credential was written, when". The action is **destructive when it overwrites an existing entry** and therefore requires confirmation (`Overwrite the existing <slot> entry in <backend>? (y/N): `, affirmative `y`/`yes` only) or `-yes`. Writing a *new* entry is not destructive and is not confirmed.
* **Traceability** — PRD **7.3**. Descends from `/set wincred <credential-type> <value>` (`SetCommand.cs:228-250`, `SettingsService.cs:145-194`, dossier B6) and the vault write primitive (B7). The removal of the value parameter, the read-back verification, value validation, `-store`, `-profile`, `-yes`, `-force`, `-noverify`, the OS failure reason, and the refusal to touch `settings.json` are **NEW**.

---

#### 3.2.4 `CRED ROTATE` — NEW — replace a live secret and prove the replacement took

| Field | Value |
|---|---|
| Command | `ROTATE` |
| Root command | `CRED` |
| Description | Replace a stored secret with a new value, verify it, and report what the credential now resolves from. |
| Usage prototype | `CRED ROTATE <slot> [-store <backend>] [-profile <name>] [-keepbackup] [-yes]` |

**Why it earns its place.** The source product could *write* a secret and could never delete one; its delete primitive had zero call sites in the entire repository, and its own documentation claimed delete was implemented. Consequently there was no rotation story at all: a user with a leaked API key had to leave the old entry in place and go to the operating system's own credential UI. Key rotation is table stakes for anything holding a long-lived bearer secret, and rotation is not simply "write again" — it must verify the new value landed, and it must tell the user whether a *higher-priority* tier is still shadowing the store, which is the failure that makes people believe rotation silently did nothing.

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `slot` | ordered | `string` | **yes** | — | the three slot ids + aliases | Credential slot to rotate. |
| `store` | named | `string` | no | `auto` | `auto`, `wincred`, `keychain`, `secretservice`, `encfile` | Backend holding the entry. |
| `profile` | named | `string` | no | `default` | `[-_0-9A-Za-z]{1,32}` | Profile namespace. |
| `keepbackup` | named | `string` | no | *(none)* | `[-_0-9A-Za-z]{1,32}` | Copy the outgoing value to `ChatDbg:<name>:<Slot>` before overwriting, so a bad rotation can be undone. The backup entry is itself a secret and is reported, never printed. |
| `yes` | flag | `bool` | no | `false` | — | Pre-answer the rotation confirmation. |

* **Pipeline behaviour** — **accepts piped input, produces piped output**. One piped chunk is the **new** secret value. Non-piped, the new value is read from the `secret:` prompt, twice, and the two entries must match (`The two values did not match; nothing was changed.`). Output is one `ResultFormat.General` chunk summarising: the backend written, whether read-back verified, whether a backup was kept, and — the important part — the credential's *current effective source*, e.g. `Rotated awsSecretKey in Windows Credential Manager. WARNING: awsSecretKey still resolves from environment variable (AWS_SECRET_ACCESS_KEY); the rotated value is being shadowed.`
* **Environment interaction** — reads the same set as `CRED SET`; writes `CRED_LAST_ROTATE_SLOT` and `CRED_LAST_ROTATE_UTC` into its own bucket. No environment-modifying permission needed. Never writes a process environment variable.
* **Failure modes** — no existing entry → `No stored value for <slot> in <backend>; use CRED SET to create one.` Store unavailable → the platform message, no change. Write succeeds but read-back differs → the old value is restored from the in-memory copy where the backend supports it, and the result is a `Failure` naming the inconsistency; where restore is impossible the message says so explicitly rather than pretending. Backup requested but backup write fails → the rotation is **not** performed (`Refusing to rotate without the requested backup.`). Upstream failure chunk → forwarded, no rotation.
* **Security and audit** — the new value, the old value and any backup are all secrets: never printed, never in a parameter, never in an audit record. The audit record carries slot, backend, profile, whether a backup was taken, and the outcome. **Destructive and irreversible without `-keepbackup`** — confirmation is mandatory (`Rotate <slot> in <backend>? The current value will be replaced. (y/N): `) unless `-yes`.
* **Traceability** — **NEW**. Its ancestor is the unreferenced delete primitive and the write primitive of `WindowsCredentialManager.cs` (dossier B7, Q14, Q28) and open question 5 ("is there any intended cleanup/rotation lifecycle?" — answered: yes, here). PRD **7.3**.

---

#### 3.2.5 `CRED REMOVE` — NEW — delete a stored secret

| Field | Value |
|---|---|
| Command | `REMOVE` |
| Root command | `CRED` |
| Description | Delete a credential entry from a secret store, or clear a deprecated plaintext field. |
| Usage prototype | `CRED REMOVE <slot> [-tier store\|file\|all] [-store <backend>] [-profile <name>] [-yes]` |

**Why it earns its place.** The source shipped a fully-written delete operation with **zero** call sites; no command, menu item or wizard step could remove a stored secret, and the migration wizard's cleanup step cleared only the plaintext settings fields. Revocation — the thing you do first when a key leaks — was impossible inside the product. This tool wires the primitive up and extends it to the file tier so that "remove this credential from my machine" is one command rather than a trip through three different operating-system UIs.

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `slot` | ordered | `string` | **yes** | — | the three slot ids + aliases | Credential slot to delete. |
| `tier` | named | `string` | no | `store` | `store`, `file`, `all` | Which custody tier to clear. `file` blanks the deprecated settings field (via the settings package, see below); `all` does both. The **environment tier is never touched** — a tool does not reach into the user's shell. |
| `store` | named | `string` | no | `auto` | `auto`, `wincred`, `keychain`, `secretservice`, `encfile` | Backend to delete from. |
| `profile` | named | `string` | no | `default` | `[-_0-9A-Za-z]{1,32}` | Profile namespace. |
| `yes` | flag | `bool` | no | `false` | — | Pre-answer the deletion confirmation. |

* **Pipeline behaviour** — **accepts piped input, produces piped output**. One piped chunk is one slot id, so `CRED SCAN -tier file | CRED REMOVE -tier file -yes` clears exactly what the scanner found. One output chunk per deletion: `Removed <slot> from <backend>.` / `Cleared the deprecated settings-file field for <slot>.` / `No entry to remove for <slot> in <backend>.` (the last is a **success**, not a failure — deletion is idempotent). `ResultFormat.General`.
* **Environment interaction** — reads `CRED_STORE_ENABLED`, `CRED_STORE_BACKEND`, `CRED_SETTINGS_PATH`, `CRED_PROFILE`. Writes `CRED_LAST_REMOVE_SLOT` into its own bucket. No environment-modifying permission needed. For `-tier file`/`all` it does **not** edit `settings.json` itself: it emits a `SET CLEARCRED <slot>` request chunk that the settings package consumes, keeping the file-ownership boundary intact; when that package is not loaded the tool reports `Cannot clear the settings-file tier: the SET package is not loaded.` and the store deletion still proceeds.
* **Failure modes** — store unavailable → platform message, no change. Delete refused by the OS → `Failed to remove credential: <slot>` plus the OS reason. Unknown slot → the `Unknown credential type` pair. Refusal to run unconfirmed as a pipeline stage without `-yes`. Upstream failure chunks forwarded untouched.
* **Security and audit** — carries no secret value in any parameter or output. **Destructive and irreversible** — confirmation is mandatory (`Permanently remove <slot> from <backend>? (y/N): `) unless `-yes`. The audit record carries slot, tier, backend, profile and outcome; this is the record an incident review will want, so it is deliberately not masked.
* **Traceability** — **NEW**. Ancestor: the dead `DeleteCredential` primitive (`WindowsCredentialManager.cs:148-163`, dossier B7/Q14/Q28) and the wizard's settings-file cleanup step (B9 step 7). PRD **7.3**.

---

#### 3.2.6 `CRED ENABLE` — turn on the secret-store tier, with consent

| Field | Value |
|---|---|
| Command | `ENABLE` |
| Root command | `CRED` |
| Description | Enable the OS secret-store credential tier after explicit consent. |
| Usage prototype | `CRED ENABLE [-store wincred\|keychain\|secretservice\|encfile\|auto] [-yes]` |

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `store` | named | `string` | no | `auto` | `auto`, `wincred`, `keychain`, `secretservice`, `encfile` | **NEW** (source: implicit Windows only). Backend to enable. `auto` picks the first *available* backend in the order `wincred`, `keychain`, `secretservice`, `encfile` — which on any single OS is at most two candidates. |
| `yes` | flag | `bool` | no | `false` | — | **NEW.** Pre-answer the consent prompt. Consent is still recorded in the audit event. |

**Consent flow — preserved.** When the backend is available the tool prints the source's banner, verbatim in spirit:

```
Enabling secure credential storage...
This will allow ChatDbg to securely store credentials using <backend display name>.
Credentials will be encrypted and stored securely by the operating system.
```

then prompts `Do you want to enable <backend display name> for secure credential storage? (y/N): ` and reads a line. Only `y` and `yes`, after lower-casing, are affirmative; anything else — including empty input and end-of-input — declines. On acceptance it sets the store tier on and prints:

```
Secure credential storage enabled.
You can now store credentials using: CRED SET <credential-type>
Example: CRED SET azureApiKey
```

Note the example no longer carries a value, because the value is never an argument.

**Declining is a success, not a failure — a deliberate improvement.** The source returned an *error* result reading `Failed to enable Windows Credential Manager integration.` when the user answered "n", conflating a deliberate decision with a breakage. This tool returns a success result carrying `Secure credential storage not enabled.` and changes no state.

* **Pipeline behaviour** — **produces piped output; refuses piped input.** One chunk, `ResultFormat.General`. Piped input is rejected with the explanatory string `CRED ENABLE does not accept piped input; it requires interactive consent. Use -yes for unattended enablement.` rather than an exception (`ref-cupcake.md` rule 29). With `-yes` and piped input it still declines, because a consent decision must not be derivable from an upstream stage's data.
* **Environment interaction** — **reads** `CRED_STORE_ENABLED`, `CRED_STORE_BACKEND`. **Writes** `CRED_STORE_ENABLED = true` and `CRED_STORE_BACKEND = <backend>` into its own bucket; the settings package mirrors those to `useWindowsCredentialManager` (when the backend is `wincred`) and to a new `secretStoreBackend` field. No environment-modifying permission needed for the prefixed keys; if the host wants these as globals it must register this tool with `modifiesEnvironment: true`, which this specification advises against.
* **Failure modes** — backend unavailable on this platform → **no prompt, no state change**, message `<backend display name> is not available on this platform.` (preserving the source's refusal semantics and the test that pins them). `-store auto` with no available backend → `No secret store is available on this platform. Credentials can still be supplied through environment variables; run CRED ENV for the variable names.` — a degradation, not a failure. Persistence failure → **reported**, unlike the source, which caught the save failure, printed a line, and returned success anyway so that `/set useWindowsCredentialManager true` on a read-only settings file cheerfully answered `Set usewindowscredentialmanager = true` and was silently lost at the next restart.
* **Security and audit** — no secret in any parameter or output. Not destructive (the inverse operation is `CRED DISABLE`), but it changes the trust posture of the product, so the audit record carries backend, whether consent was interactive or `-yes`, and the outcome.
* **Traceability** — PRD **7.3**. Descends from `/set enablewincred` (`SettingsService.EnableWindowsCredentialManagerAsync`, dossier B4) and the `true` half of `/set useWindowsCredentialManager` (B5). `-store`, `-yes`, the multi-backend picker, the success-on-decline change and the surfaced persistence failure are **NEW**.

---

#### 3.2.7 `CRED DISABLE` — turn the secret-store tier off

| Field | Value |
|---|---|
| Command | `DISABLE` |
| Root command | `CRED` |
| Description | Disable the OS secret-store credential tier. Stored entries are left untouched. |
| Usage prototype | `CRED DISABLE [-purge] [-yes]` |

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `purge` | flag | `bool` | no | `false` | — | **NEW.** Also delete every `ChatDbg:*` entry this profile owns from the backend, as `CRED REMOVE` would. Without it, entries survive and reappear the moment the tier is re-enabled. |
| `yes` | flag | `bool` | no | `false` | — | **NEW.** Pre-answer the purge confirmation. |

**Always permitted.** Disabling is accepted on every platform regardless of backend availability — preserved exactly from the source, where setting the flag `false` was always allowed while setting it `true` was refused off-Windows. This is what lets a user recover a settings file that some other machine (or the source product's GUI checkbox, which never checked availability) left with the flag stuck on.

* **Pipeline behaviour** — **produces piped output; refuses piped input** with an explanatory string. One `ResultFormat.General` chunk: `Secure credential storage disabled.` plus, when entries remain, the advisory `3 stored entries were left in place; run CRED DISABLE -purge to remove them.`
* **Environment interaction** — reads and writes `CRED_STORE_ENABLED` (set to `false`) in its own bucket; leaves `CRED_STORE_BACKEND` intact so re-enabling remembers the choice. No environment-modifying permission needed.
* **Failure modes** — nothing to disable (already off) → success with `Secure credential storage is already disabled.` `-purge` on an unavailable backend → the tier is still disabled and the message says `Could not purge: <backend> is not available on this platform; entries remain.` — degradation, not failure. Persistence failure → reported.
* **Security and audit** — no secret in any parameter or output. **`-purge` is destructive and irreversible** and requires confirmation (`Permanently remove all stored ChatDbg credentials from <backend>? (y/N): `) unless `-yes`. Plain disable is not destructive and is not confirmed.
* **Traceability** — PRD **7.3**. Descends from the `false` half of `/set useWindowsCredentialManager` (dossier B5, rule R22). `-purge` is **NEW**.

---

#### 3.2.8 `CRED STORES` — NEW — what secret storage this machine actually offers

| Field | Value |
|---|---|
| Command | `STORES` |
| Root command | `CRED` |
| Description | Enumerate secret-store backends with live availability, capabilities and the reason any is unusable. |
| Usage prototype | `CRED STORES [-format table\|csv\|json] [-probe]` |

**Why it earns its place.** The source decided store availability with a bare "is the running OS Windows" test. It never asked whether the credential service actually answered, so a Windows host with the credential service disabled by policy was treated as available and every operation failed later with a generic message; and on macOS and Linux the product silently degraded to "environment variables or a plaintext file" with nothing telling the user that their only encrypted-at-rest option was missing. A user cannot make an informed custody decision without knowing what their machine can do. This tool is that answer, and it is also the tool `CRED DOCTOR` and `CRED ENABLE -store auto` consult.

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `table` | `table`, `csv`, `json` | Output shape. |
| `probe` | flag | `bool` | no | `false` | — | Perform a **live** round-trip against each backend — write, read back and delete a throwaway entry named `ChatDbg:Probe:<guid>` — instead of only checking that the platform and native library are present. Mirrors the source's own test technique of using a randomly generated, never-written entry name to guarantee a miss. |

**Output columns.** `backend`, `platform`, `available` (`yes` / `no`), `reason` (empty when available; otherwise e.g. `not supported on this platform`, `libsecret-1.so.0 not found`, `no secret service is running`, `blocked by policy`, `backend assembly not loaded (restricted host)`), `encrypted-at-rest` (`yes` / `weak` for `encfile`), `roams` (`no` for `wincred` — entries are written with local-machine persistence and explicitly do **not** roam with a domain profile, contradicting every enterprise/roaming claim the source's documentation made), `max-secret-chars` (`1280` for `wincred`, backend-specific elsewhere), `entries` (count of `ChatDbg:*` entries in the active profile, or `-` when unavailable).

* **Pipeline behaviour** — **source only.** Produces one chunk per backend; refuses piped input with an explanatory string. `ResultFormat.CSV` / `ResultFormat.JSON` under `-format`, otherwise `General`.
* **Environment interaction** — reads `CRED_STORE_BACKEND`, `CRED_STORE_ENABLED`, `CRED_PROFILE` (`storeDefault: false`). Writes nothing.
* **Failure modes** — a backend that throws during probing is reported as `available = no` with the exception's message as `reason`; the tool never fails as a whole because one backend is broken. `-probe` write that succeeds but whose cleanup delete fails → the row is still `available = yes` and a warning line names the leftover probe entry so a human can remove it. In a restricted host where the native satellites were refused by the loader's security policy, every native backend reports `backend assembly not loaded (restricted host)` and `encfile` remains available — this is the designed degradation, not an error.
* **Security and audit** — no secret in any parameter or output; probe values are random throwaways and are still never printed. Not destructive, except that `-probe` briefly creates and deletes a uniquely-named entry — which is why the probe name is namespaced under `ChatDbg:Probe:` and can never collide with a real slot.
* **Traceability** — **NEW**. Ancestor: the availability probe `WindowsCredentialManager.IsAvailable` (dossier B7, rules R19–R21) and the platform-conditional wording scattered through the source's wizard, `/set` help and startup hints. PRD **7.3**, with a reporting overlap into **7.2 Settings & Configuration**.

---

#### 3.2.9 `CRED MIGRATE` — move off plaintext-in-a-file, without printing the plaintext

| Field | Value |
|---|---|
| Command | `MIGRATE` |
| Root command | `CRED` |
| Description | Move credentials out of the deprecated settings-file fields into a secret store or environment variables. |
| Usage prototype | `CRED MIGRATE [<slot>] [-to env\|store\|both] [-store <backend>] [-purge] [-dryrun] [-yes]` |

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `slot` | ordered | `string` | no (`IsRequired = false`) | *(all populated slots)* | the three slot ids + aliases | Migrate one slot only. Fed by the pipe when piped (`UsePipe = true`). |
| `to` | named | `string` | no | `env` | `env`, `store`, `both` | **NEW as a parameter** — the source made this an interactive three-item menu accepting only the exact trimmed strings `1`, `2`, `3`. `env` = option 1, `store` = option 2, `both` = option 3, with identical semantics. |
| `store` | named | `string` | no | `auto` | `auto`, `wincred`, `keychain`, `secretservice`, `encfile` | **NEW.** Backend for `-to store` / `-to both`. |
| `purge` | flag | `bool` | no | `false` | — | Blank the deprecated settings-file fields after a successful migration. Corresponds to the source's second confirmation prompt, `Would you like to remove credentials from the settings file now? (y/N): `. |
| `dryrun` | flag | `bool` | no | `false` | — | **NEW.** Report exactly what would move where, change nothing. |
| `yes` | flag | `bool` | no | `false` | — | **NEW.** Pre-answer the purge confirmation. Required in a pipeline. |

**Behaviour, and the one thing that changes.** The source's wizard printed the migration instructions by interpolating **the actual plaintext secret** into `set CHATDBG_AZURE_API_KEY=<value>` lines — and it printed that block automatically on *every* settings load while any plaintext field was populated, and again inside the store-a-secret path, and again from behind a full-screen terminal UI where the user could not see it but the terminal scrollback could. That is the single worst defect in the feature: a tool whose stated purpose is "report without disclosure" was the product's most reliable secret-disclosure channel.

**This tool never prints a secret.** `-to env` emits a copy-paste block with a **placeholder**, plus the name of the slot whose value the user must paste in:

```
To migrate to environment variables, run these commands and paste each value yourself:
  set CHATDBG_AZURE_API_KEY=<paste the azureApiKey value>
  set CHATDBG_AWS_ACCESS_KEY=<paste the awsAccessKey value>
Or add them to your system environment variables for persistence.
Run 'CRED REVEAL azureApiKey' if you need to recover a value you no longer have.
```

`CRED REVEAL` is deliberately **not** a tool in this package (see §3.4). Recovery of a value the user themselves stored in plaintext is the operating system's job, and the message says so on platforms where it applies. `-to store` and `-to both` copy the value directly from the file tier into the backend without any human handling and therefore never need to disclose it — those are the recommended paths, and `-to env` exists only because the environment tier is the one channel that works everywhere.

**Truthful reporting — a deliberate improvement.** The source's `/set migrate` returned a **success** result whatever happened, and its service returned `false` whenever the user declined the *cleanup* prompt even if the store migration had fully succeeded — so "migrated to the vault but kept the file copy" was reported to the user as `No credentials found to migrate or migration cancelled.` This tool reports per-slot outcomes and an accurate summary: `2 of 3 credentials migrated to Windows Credential Manager; settings-file copies retained (use -purge to remove them).`

* **Pipeline behaviour** — **both.** One piped chunk is one slot id, so `CRED SCAN -tier file | CRED MIGRATE -to store -yes` migrates exactly what the scan found. Produces one chunk per slot outcome plus a final summary chunk. `ResultFormat.General`, or `ResultFormat.CSV` under `-dryrun` so the plan is machine-readable.
* **Environment interaction** — **reads** `CRED_SETTINGS_PATH`, `CRED_STORE_ENABLED`, `CRED_STORE_BACKEND`, `CRED_PROFILE`. **Reads** the five process environment variables to report whether a target variable is already occupied (migrating into an occupied variable would be shadowed and the tool says so). **Writes** `CRED_LAST_MIGRATION_UTC` into its own bucket. It **never writes a process environment variable** — the source never did either, and a tool cannot change its parent shell's environment anyway; pretending otherwise is how users end up believing a migration happened. For `-purge` it emits `SET CLEARCRED <slot>` request chunks for the settings package rather than editing `settings.json` itself.
* **Failure modes** — nothing to migrate → **success** with `No credentials found in the settings file.` (the source returned `false` silently here and let the command report success with a message that conflated "nothing to do" with "cancelled"). `-to store` with no available backend → per-slot failure `Secret store '<backend>' is not available on this platform.` and the whole run aborts **before** any purge, so a failed migration can never lose the only copy. A partial migration with `-purge` purges **only** the slots that verifiably landed. Store write failure → that slot is reported failed, its file copy is retained, and the summary counts it. Upstream failure chunks are forwarded and never trigger a migration.
* **Security and audit** — the migrated values are secrets: read from the file tier, written to the backend, never printed and never placed in a parameter. The audit record carries slot ids, target tier, backend and outcomes only. **`-purge` is destructive and irreversible** and requires confirmation (`Remove credentials from the settings file now? (y/N): `, affirmative `y`/`yes`) unless `-yes`. The migration itself is not destructive.
* **Traceability** — PRD **7.3**. Descends from `/set migrate` (`SettingsService.MigrateCredentialsAsync`, dossier B9), the environment-instructions printer (B10) and the bulk store migration (B11). The placeholder-instead-of-plaintext change, `-to`, `-store`, `-dryrun`, `-yes`, per-slot outcomes, occupied-variable detection and the abort-before-purge rule are **NEW**. It also answers the source's dangling "environment variables only" mode flag, which two call sites passed and the body never read, and the graphical shell's three-way radio group whose selection was read into a local variable and then discarded.

---

#### 3.2.10 `CRED SCAN` — NEW — find plaintext secrets without printing them

| Field | Value |
|---|---|
| Command | `SCAN` |
| Root command | `CRED` |
| Description | Report where plaintext credentials are sitting on disk or in the environment, with never a value. |
| Usage prototype | `CRED SCAN [-path <file-or-dir>] [-tier file\|env\|all] [-format table\|csv\|json] [-strict]` |

**Why it earns its place.** The source detected plaintext credentials only as a side effect of loading settings, and its reaction was to print the secrets. It also created a fresh settings file containing the three legacy credential slots on first run, wrote them on every save, never restricted the file's permissions, never added `settings.json` to the repository ignore list, and fell back to the world-readable temp directory whenever the home directory could not be resolved — which silently relocates a file containing plaintext secrets. There was no way to ask "am I leaking?" and get an answer that was not itself a leak. `CRED SCAN` is that question, and it is safe to run in CI, in a pre-commit hook, or over a colleague's machine.

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `path` | named | `string` | no | the active settings file (`CRED_SETTINGS_PATH`, else `<user profile>/.ChatDbg/settings.json`, else the temp-directory fallback) | an existing file or directory | File or directory to scan. Quote it — the tokenizer splits unquoted `.`, `/`, `\` and `:`. |
| `tier` | named | `string` | no | `all` | `file`, `env`, `all` | Which custody tiers to inspect. `env` reports which of the five process variables are occupied, never their contents. |
| `format` | named | `string` | no | `table` | `table`, `csv`, `json` | Output shape. `csv` emits `slot,tier,location,severity,fingerprint`. |
| `strict` | flag | `bool` | no | `false` | — | Treat *any* finding as a failure result, so a CI stage fails the build. Without it, findings are reported as a successful run. |

**Findings and severity.** `high` — a non-empty deprecated settings-file field (the exact condition the source used for its warning: *any* of the three fields non-empty). `high` — a settings file located under the OS temp directory, or inside a directory that contains a `.git` folder. `medium` — a settings file whose permissions are readable by users other than the owner (`0o077` bits set on POSIX; a non-owner ACE on Windows), a check the source never performed. `low` — an occupied environment variable, reported for completeness because environment variables are visible to every process the user runs and leak into `ps`, crash dumps and CI logs.

**Fingerprints, not values.** Each finding carries a `fingerprint`: the first 4 characters of a salted SHA-256 of the value, rendered as hex. It is enough to answer "is the value in the file the same one the store holds?" — which is exactly the question a migration audit asks — and it discloses nothing. The salt is per-machine, generated once, stored alongside the settings, and never emitted.

* **Pipeline behaviour** — **both.** Accepts piped input: one chunk is one filesystem path to scan, so `FIND -name settings.json | CRED SCAN` works. Produces one chunk per finding; a clean scan produces one chunk `No plaintext credentials found.` `ResultFormat.CSV`/`JSON` under `-format`.
* **Environment interaction** — **reads** `CRED_SETTINGS_PATH`, `CRED_PROFILE`, and probes the five process variables for occupancy under `-tier env`/`all`. **Writes** `CRED_LAST_SCAN_FINDINGS` (a count) into its own bucket. No environment-modifying permission needed.
* **Failure modes** — path does not exist → `Failure` naming the path. Path is unreadable → the finding is reported as `severity = unknown, reason = access denied` rather than aborting the whole scan. A file that is not JSON, or is corrupt, is reported as `could not parse; cannot rule out plaintext credentials` — deliberately *not* silently skipped, because the source's own load path silently reverted to defaults on a corrupt file and discarded whatever it held. `-strict` with findings → a `Failure` result whose message is the finding count. Upstream failure chunks are forwarded untouched.
* **Security and audit** — no secret in any parameter or output; fingerprints are one-way and salted. Not destructive. The audit record is safe verbatim and is genuinely useful: it is the only record in the product of *when* a leak was detected.
* **Traceability** — **NEW**. Ancestor: the load-time plaintext warning and `ChatSettings.HasPlaintextCredentials` (dossier B8, rule R42), plus the unaddressed permissions and temp-fallback risks (R43, R51). PRD **7.3**, with reporting overlap into **7.2**.

---

#### 3.2.11 `CRED REDACT` — NEW — the pipeline's secret filter

| Field | Value |
|---|---|
| Command | `REDACT` |
| Root command | `CRED` |
| Description | Mask any live credential value, and anything shaped like a credential, in text flowing through the pipe. |
| Usage prototype | `CRED REDACT [-token <text>] [-mode live\|pattern\|both] [-minlength <n>] [-fail]` |

**Why it earns its place.** Every serious disclosure defect in the source product was the same defect wearing a different hat: a secret reached an output channel. The migration instructions interpolated secrets into stdout; the load path re-emitted them on every start; the full-screen shell pushed them into terminal scrollback where nobody could see them but everybody could scroll back to them; `/set wincred` echoed them into shell history; and the framework's own audit masking does not work for `-name value` syntax. Fixing those one at a time leaves the next one. A pipeline-native redactor fixes the class: any package's output becomes safe by composition, `LOG EXPORT | CRED REDACT` is one keystroke, and the rule "nothing leaves this product unfiltered" becomes enforceable rather than aspirational.

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `token` | named | `string` | no | `[REDACTED]` | 1–32 characters | Replacement text. Default matches the framework's own `AuditMaskingConfiguration.RedactionPlaceholder`. |
| `mode` | named | `string` | no | `both` | `live`, `pattern`, `both` | `live` masks only exact matches of the currently-resolved secret values; `pattern` masks anything matching the credential-shape rules below; `both` does both. |
| `minlength` | named | `int` | no | `8` | 4 – 256 | Shortest live value that will be masked. Guards against a one-character secret turning every output into `[REDACTED]`. A live value shorter than this is reported once as a warning on the status channel, never in output. |
| `fail` | flag | `bool` | no | `false` | — | Convert any chunk that required redaction into a **failure** chunk, so a pipeline that was never supposed to carry a secret stops instead of silently continuing. |

**Pattern rules (`pattern` / `both`).** `sk-` followed by 20 or more base64url characters; `AKIA`/`ASIA` followed by 16 uppercase alphanumerics; a 40-character base64 run adjacent to the words `secret`, `key` or `token`; the value half of `KEY=value` / `KEY: value` where the key name matches the framework's own default redaction patterns (`*password*`, `*secret*`, `*token*`, `*key*`, `*credential*`). Patterns are heuristics and are documented as such; `live` mode is exact and is the guarantee.

**It overrides `Main`.** This is the one tool in the package that does **not** use `AbstractCommand`'s template method unchanged. `AbstractCommand.Main` forwards a failed upstream chunk verbatim and skips empty chunks, so `HandlePipedChunk` never sees a failure — and a failure chunk's `ErrorMessage` is exactly where an upstream tool's exception text will have interpolated a connection string or a bearer token. `CRED REDACT` overrides `Main` (legal — it is not sealed) so that it reads `io.ReadInputPipeChunks()` itself and redacts **both** `Output` and `ErrorMessage` on every chunk, success or failure, before re-emitting it. A redactor that cannot see failures is not a redactor.

* **Pipeline behaviour** — **filter: requires piped input, produces piped output.** One piped chunk is one unit of text to be scrubbed; the tool emits exactly one chunk per input chunk, preserving the upstream chunk's `IsSuccess`, `CorrelationId` and **`OutputFormat`** — so a CSV stage stays CSV and a JSON stage stays JSON through the filter. Invoked without a pipe it returns the explanatory string `CRED REDACT is a pipeline filter; give it input, e.g. 'LOG EXPORT | CRED REDACT'.` rather than throwing.
* **Environment interaction** — **reads** the five process credential variables and the enabled store (it must know the live values in order to mask them), plus `CRED_REDACT_TOKEN`, `CRED_REDACT_MODE`, `CRED_REDACT_MINLENGTH`. **Writes** `CRED_REDACT_COUNT` (redactions performed in this run) from `OnEndPipe`, into its own bucket. No environment-modifying permission needed.
* **Failure modes** — no live secrets resolvable and `mode = live` → the tool still runs, masks nothing, and emits one status-channel note `No live credentials to match; pattern mode is off.` so the user is not misled into thinking output was scrubbed. A store read that throws while gathering live values → that tier is skipped, a status note names the failure, and pattern mode continues; the tool does **not** fail open silently. `-fail` converts a redacted chunk into `CommandResult<string>.Failure("A credential value was found in this stream and was masked.")`. Upstream failure chunks are *not* forwarded verbatim — they are redacted first, then forwarded with their failure status intact.
* **Security and audit** — this tool **reads live secret values into memory by design**; that is its function. It never emits them, never places them in a parameter, and never writes them anywhere. Its audit record carries the redaction count and the mode, which is a genuinely useful signal (a spike in redactions means something upstream started printing secrets). Not destructive. Values are held for the duration of one `Main` invocation only and are dropped in `OnEndPipe`; the specification acknowledges (see §3.4) that .NET strings cannot be reliably zeroed and does not pretend otherwise, unlike the source's documentation, which claimed "minimal credential lifetime in memory" while the code did nothing whatsoever to achieve it.
* **Traceability** — **NEW**. Ancestor: the masking tokens of the status display (dossier B3, rule R38) and, inversely, the plaintext-printing defects of B10/B8/B6 and the full-screen-shell disclosure. PRD **7.3**, composing with **7.11 Diagnostic Logging** and **7.12 Output Rendering**.

---

#### 3.2.12 `CRED ENV` — NEW — name the environment channel, and say whether it is occupied

| Field | Value |
|---|---|
| Command | `ENV` |
| Root command | `CRED` |
| Description | List the environment variables each credential slot reads, in priority order, with occupancy and copy-paste templates. |
| Usage prototype | `CRED ENV [<slot>] [-shell auto\|cmd\|powershell\|bash\|fish] [-format table\|csv\|json]` |

**Why it earns its place.** Environment variables are the source product's *primary* credential channel — the only one that works on every platform, the one the migration wizard recommends first — and the product's README documented **none** of the five variable names. They existed only in `/set` help text, startup hints, a docs folder and a manual test script. A user reading the front door of the product could not discover the main way in. This tool is the discoverable answer, and it also states the priority rule that surprises people: the product-specific `CHATDBG_AWS_ACCESS_KEY` beats the vendor-standard `AWS_ACCESS_KEY_ID`, and an existing-but-empty variable is treated as absent and falls through.

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `slot` | ordered | `string` | no (`IsRequired = false`) | *(all three)* | the three slot ids + aliases | Slot to describe. Fed by the pipe when piped (`UsePipe = true`). |
| `shell` | named | `string` | no | `auto` | `auto`, `cmd`, `powershell`, `bash`, `fish` | Syntax for the copy-paste template. `auto` detects the host shell; on Windows it emits the source's `set NAME=value` form, elsewhere `export NAME=value`. |
| `format` | named | `string` | no | `table` | `table`, `csv`, `json` | Output shape. |

**Output.** Per slot, each variable in declared priority order, with `occupied` (`yes`/`no`/`empty — treated as absent`), and one template line per variable using a **placeholder**, never a value:

```
azureApiKey
  1  CHATDBG_AZURE_API_KEY   occupied
     set CHATDBG_AZURE_API_KEY=<your Azure API key>
awsAccessKey
  1  CHATDBG_AWS_ACCESS_KEY  not set
  2  AWS_ACCESS_KEY_ID       occupied   <- currently winning
```

* **Pipeline behaviour** — **both.** One piped chunk is one slot id; one group of chunks per slot. `ResultFormat.CSV`/`JSON` under `-format`.
* **Environment interaction** — **reads** the five process variables for occupancy only (never their contents) and `SHELL`/`ComSpec`/`PSModulePath` for `-shell auto`. **Writes** nothing, in either the process environment or the host environment. Needs no environment-modifying permission.
* **Failure modes** — unknown slot → `Unknown credential type: <as typed>` + `Valid types: azureApiKey, awsAccessKey, awsSecretKey`, `Failure`. Shell detection failure under `-shell auto` → falls back to the platform default and says so. This tool has no prerequisites and cannot fail for want of a store. Upstream failure chunks forwarded untouched.
* **Security and audit** — no secret in any parameter or output; occupancy is a boolean, and templates carry placeholders. Safe to log verbatim. Not destructive.
* **Traceability** — **NEW**. Ancestor: the environment-variable lists inside `ChatSettings` (dossier B1, rules R2–R4), the `/set` help text, and the blocked-legacy-key refusal template that was the only place the names were shown to users. PRD **7.3**, with documentation overlap into **7.2**.

---

#### 3.2.13 `CRED DOCTOR` — NEW — is this machine actually able to talk to the provider?

| Field | Value |
|---|---|
| Command | `DOCTOR` |
| Root command | `CRED` |
| Description | Diagnose credential readiness for a provider end to end, and print remediation. |
| Usage prototype | `CRED DOCTOR [-provider azure\|bedrock\|llama\|all] [-format text\|json] [-strict]` |

**Why it earns its place.** The source's startup diagnostics were duplicated verbatim in two shells, one of which was dead code, and they carried two real defects: the cloud-inference provider reported itself *configured* when only the **access** half of a key pair was present — never looking at the secret key — so the user was told `AWS credentials loaded from: <source>`, the remediation block was suppressed, and the SDK then silently fell back to its own ambient credential chain and failed at request time with an unrelated error. And the provenance line for that provider always reported the **access** key's source even when it was emitted because the **secret** key was present. A user cannot debug that. `CRED DOCTOR` is the tool that says the true thing.

| Parameter | Kind | Declared type | Required | Default | Allowed / range | Help text |
|---|---|---|---|---|---|---|
| `provider` | named | `string` | no | `all` | `azure`, `bedrock`, `llama`, `all` | Which provider's credential requirements to check. `azure` is the product's default provider. `llama` needs no credential and reports so in one line. |
| `format` | named | `string` | no | `text` | `text`, `json` | `text` is the human remediation report; `json` is machine-readable for CI. |
| `strict` | flag | `bool` | no | `false` | — | Return a failure result when any check fails, for use as a CI gate or a pre-flight stage. |

**Checks performed.** Per provider: every required slot resolves to a non-empty value (**both halves of a key pair, individually**); each slot's winning tier and variable name; whether a lower-priority tier is being shadowed and by what; store tier enabled/available/reachable; whether the SDK's own ambient credential chain would be consulted as an undocumented fourth channel, reported as `provider SDK ambient chain (unverified)` so it stops being invisible; whether the settings file holds plaintext copies (delegating to the same logic as `CRED SCAN`); whether the settings file is over-permissive or living in the temp directory; and, for `azure`, whether the endpoint is configured at all — the source's own remediation reminded the user that a key without `azureEndpoint` is not a working configuration.

**Output** preserves the source's remediation shape and its provider-specific text, generalized. For example, for `bedrock` with a half key pair:

```
bedrock: NOT READY
  awsAccessKey  OK    environment variable (AWS_ACCESS_KEY_ID)
  awsSecretKey  MISSING
  -> A key pair needs both halves. Supply the secret key:
       set CHATDBG_AWS_SECRET_KEY=<your AWS secret access key>
     or store it:  CRED ENABLE  then  CRED SET awsSecretKey
  !  With only one half present the provider SDK will fall back to its own
     ambient credential chain; requests may fail with an unrelated error.
```

* **Pipeline behaviour** — **both.** Accepts piped input: one chunk is one provider name, so `AI PROVIDERS | CRED DOCTOR` checks each one. Produces one chunk per check plus a per-provider verdict chunk. `ResultFormat.JSON` under `-format json`, otherwise `General`.
* **Environment interaction** — **reads** the five process variables, `CRED_STORE_ENABLED`, `CRED_STORE_BACKEND`, `CRED_SETTINGS_PATH`, `CRED_PROFILE`, and the settings package's `SET_PROVIDER` / `SET_AZUREENDPOINT` / `SET_AWSREGION` / `SET_MODELID` mirrors (`storeDefault: false`). **Writes** `CRED_LAST_DOCTOR_VERDICT` into its own bucket. No environment-modifying permission needed.
* **Failure modes** — unknown provider name → `Failure` listing the four accepted values. Settings mirrors unavailable because the `SET` package is not loaded → each dependent check reports `unknown (SET package not loaded)` and the run continues; endpoint and region checks degrade to advisory. Store unreachable → reported as a finding, not as a tool failure. `-strict` with any failing check → a `Failure` whose message names the first failing check. Upstream failure chunks forwarded untouched.
* **Security and audit** — no secret in any parameter or output; every value is reported as present/absent with a source label. Safe to log verbatim, and the audit record is the most useful one in the package for support. Not destructive.
* **Traceability** — **NEW as a tool**, but its content is a faithful port of the startup credential diagnostics (`ChatShell.cs:239-321`, dossier B13) with the half-key-pair and wrong-provenance defects corrected. PRD **7.3**, reporting into **7.6 AI Provider Abstraction & Hosted OpenAI**, **7.7 Managed Cloud Model Marketplace** and **7.8 Local Model Inference**.

---

### 3.3 Pipeline compositions

Six worked examples. Two of them cross a package boundary.

---

**1. Audit every credential's provenance, including shadowed tiers.**

```
CRED LIST -format csv -showempty | CRED SOURCE -all
```

`CRED LIST` emits one CSV row per slot and, because the rows carry the slot id as their first field, the downstream stage's `slot` parameter (`UsePipe = true`) is fed by the pipe instead of the command line. `CRED SOURCE -all` then expands each into a full tier ladder. The user gets, for each of the three credentials, every channel that holds a value, in priority order, with the winner marked — and immediately sees the two situations that confuse people most: a store entry being shadowed by a stale environment variable, and a plaintext file copy that survived a migration. Nothing in the output is a secret.

---

**2. Take a secret from a password manager into the OS store without it ever touching the terminal.**

```
SH "op read op://Private/ChatDbg/azure-api-key" | CRED REDACT -mode pattern -fail | CRED SET azureApiKey -yes
```

The first stage is the host's shell-out tool. The middle stage is a **guard**, not a masker: `-mode pattern -fail` turns the pipeline into a failure the instant the retrieved text looks like something *other* than a well-formed key, catching the common accident where the password-manager command printed an error message and the error message got stored as the API key. `CRED SET` consumes one chunk as one complete value — no tokenizer scrubbing, no shell history, no audit-log parameter — and writes it to the enabled backend, verifying by read-back. The user gets `Credential stored securely in Windows Credential Manager: azureApiKey`. (In `-mode pattern` the redactor is deliberately not masking the value it passes on; that is the one composition where `-fail` matters more than the mask, and it is why `mode` and `fail` are separate knobs.)

---

**3. Clean up a machine that has plaintext secrets on disk — plan first, then act.**

```
CRED SCAN -tier file -format csv | CRED MIGRATE -to store -dryrun
CRED SCAN -tier file -format csv | CRED MIGRATE -to store -purge -yes
```

The first line is a rehearsal: the scan finds the populated deprecated fields and emits one chunk per slot, the migration reports exactly which slot would go to which backend and what would be purged, and changes nothing. The second line performs it. Because `CRED MIGRATE` aborts before any purge when the backend is unavailable or any write failed, the run can never leave the user with no copy of a secret. The user gets a per-slot outcome list and a truthful summary — `3 of 3 credentials migrated to macOS Keychain; settings-file copies removed.` — which is precisely what the source product could not say, since its wizard reported success on cancellation and reported "nothing to migrate" on a fully successful migration whose cleanup prompt was declined.

---

**4. Cross-package — make a diagnostic bundle safe to attach to a bug report.**

```
LOG EXPORT -since 24h | CRED REDACT -mode both | OUT FILE -path "./chatdbg-diagnostics.txt"
```

`LOG EXPORT` belongs to **`ChatDbg.Tools.DiagnosticLogging`** (root `LOG`, PRD 7.11) and `OUT FILE` to **`ChatDbg.Tools.OutputRendering`** (root `OUT`, PRD 7.12); the middle stage is the only one this package owns. Because `CRED REDACT` overrides `Main` and scrubs failure chunks as well as successful ones, a stack trace in the log that interpolated an endpoint URL with an embedded key is masked too — which the framework's own audit masking would not have caught, since it only rewrites `-name=value` tokens. The user gets a file they can attach without reading it line by line first, and a status line reporting how many redactions were performed. This composition is the reason `CRED REDACT` exists as a tool rather than as a helper class inside the logging package: redaction has to be the last thing before an output sink, wherever that sink lives.

---

**5. Cross-package — a pre-flight gate before a long chat session or a batch run.**

```
AI PROVIDERS -format csv | CRED DOCTOR -strict -format json | OUT JSON -pretty
```

`AI PROVIDERS` belongs to **`ChatDbg.Tools.Providers`** (root `AI`, PRD 7.6). Each provider name arrives as a chunk, `CRED DOCTOR` checks that provider's credential requirements end to end, and `-strict` turns any failing check into a failure chunk — which the host surfaces and which stops a scripted run before it burns twenty minutes discovering that only half an AWS key pair was configured. The user gets a machine-readable readiness report and a non-zero outcome exactly when something is genuinely wrong.

---

**6. Answer "what can this machine actually store, and where should I put my key?"**

```
CRED STORES -probe -format table | REGIF "available"
```

`REGIF` is one of the framework's built-in commands and filters chunks by regular expression, emitting empty successes for non-matches (which the host drops). The user gets only the rows for backends that survived a live write/read/delete round-trip — not the ones that merely exist on paper. That distinction is the whole point: the source product decided store availability by asking whether the operating system was Windows, so a Windows machine whose credential service was disabled by policy was declared available and every subsequent operation failed with a message that explained nothing.

---

### 3.4 Design notes for the architect

**State this package holds.**

* **Per-invocation only:** a resolution cache keyed by `(slot, profile)`, alive for exactly one `Main` call and discarded at its end (defeatable with `-nocache`). This exists because the source's status display resolved every credential **twice** — once for the masked status, once for the source label — which on an enabled Windows host meant up to **six** native round-trips for one status command, with the two passes free to disagree if a variable changed between them. One consistent snapshot per command, recomputed on the next command, preserves the source's "no memoization across invocations" contract (change an environment variable mid-process and the very next command sees it) while removing the incoherence.
* **In its own environment bucket:** `CRED_STORE_ENABLED`, `CRED_STORE_BACKEND`, `CRED_PROFILE`, and the `CRED_LAST_*` breadcrumbs. All are `CRED_`-prefixed, so they persist to this root's private bucket with no `ModifiesEnvironment` grant. Note the framework trap: `IEnvironmentContext.GetValue` defaults to `storeDefault: true`, so *reading* a missing key writes the default back and flips `HasChanged`. Every read in this package passes `storeDefault: false` explicitly; only deliberate writes go through `SetValue`.
* **In the OS store:** the secrets themselves, which the package does not own so much as *address*.

**State this package must NOT hold.**

* **No secret at rest that it manages itself**, except the explicitly-weak `encfile` backend, which is labelled as such at every appearance.
* **No copy of `settings.json`, and no write to it.** The source's store-a-secret path re-loaded the settings record from disk mid-command, re-emitted the load-time plaintext warning (secrets included), silently discarded whatever unsaved changes the caller held, and rewrote the file — three distinct bugs from one boundary violation. `CRED` emits requests; `SET` owns the file.
* **No process-environment writes, ever.** Tier 1 is read-only. A tool that appeared to set `CHATDBG_AZURE_API_KEY` would change only its own process and mislead the user about what happens after restart.
* **No long-lived secret in a field.** `CRED REDACT` is the only tool that holds live values across chunks, and only for the span of one pipe.
* **No cross-invocation credential cache.** The source's "recomputed on every read" behaviour is a feature: it is what makes "export the variable and try again" work without a restart.

**Honesty about memory.** The source's documentation claimed "minimal credential lifetime in memory" and the code did nothing to achieve it — no protected string type, no zeroing, copies made freely at every tier boundary. This specification does not repeat the claim. Secrets are ordinary managed strings; the mitigations that are real are (a) never printing them, (b) never putting them in a parameter or an audit record, (c) never holding them beyond one command, and (d) telling the truth in the documentation about (a)–(c).

**Testability.**

* **The store is an interface.** `ISecretStore` with `TryRead(entry) -> string?`, `Write(entry, value) -> outcome`, `Delete(entry) -> outcome`, `Probe() -> availability + reason`. The source already had to keep this seam so that a mock could assert the store operation was invoked **zero** times when the enable flag was false; that assertion ports directly and must keep passing.
* **The environment is an interface.** An `IProcessEnvironment` with a single `Get(name)` member. The source's tests mutated real process variables and restored them in a `finally`, which made them order-sensitive and unparallelizable; a seam removes that.
* **The interaction port is an interface.** All prompting goes through `IIoContext.PromptForCommand` — never a direct `Console.ReadLine`. This is the single biggest structural fix: the source's service layer wrote prompts to stdout and blocked on stdin *from inside the service*, which is why its migration wizard was untestable, why the graphical shell's migration button produced prompts nobody could answer behind a full-screen UI, and why exactly **zero** of its ten tests covered the enable flow, the store-write flow, the wizard, the store branch of resolution, or any prompt parsing. With prompting on the IO context, `MemoryIoContext.PromptAnswers` scripts every consent path in a unit test.
* **Every tool class has a public parameterless constructor** (the loader's only activation path is `Activator.CreateInstance`) plus an internal constructor taking `(ISecretStore, IProcessEnvironment, ISettingsMirror)` for tests and for in-process registration.
* **Round-trip fixtures.** The `wincred` backend's byte-level contract — UTF-16LE blob, byte-count size, generic type `1`, persistence `2`, user name `ChatDbg`, comment `ChatDbg API Credential` — is pinned by a fixture that writes with the rebuild and reads with a byte-level reader, so compatibility with entries written by the source product does not silently rot.
* **The "never printed" property is testable.** Every tool's output is run through a fixture that asserts no chunk contains any of the seeded secret values. That is a property test over the whole package, and it is the one that would have caught the source's migration-instructions defect.

**Capability unavailable — what happens instead of failing.**

| Situation | Behaviour |
|---|---|
| No secret store on this OS (or every backend refused) | The store tier is simply absent. Resolution runs on tiers 1 and 3. `CRED ENABLE` reports `No secret store is available on this platform. Credentials can still be supplied through environment variables; run CRED ENV for the variable names.` — a **degradation with a next step**, where the source degraded silently to "environment variables or a plaintext file" and told the user nothing. |
| Backend present but service not responding (credential service disabled by policy, no keyring daemon, locked keychain) | Reported as `available = no` with the real reason, distinct from "not supported on this platform" and distinct from "entry not found". The source collapsed all three into one silent null, at two nested layers of swallowed exceptions, guaranteeing that a policy denial was indistinguishable from an absent entry, forever, with nothing logged. |
| Native backend satellite refused by the loader's security policy (restricted host) | The tool assembly still loads and runs. `CRED STORES` reports `backend assembly not loaded (restricted host)`; `encfile` remains selectable; tiers 1 and 3 are unaffected. |
| `SET` package not loaded | Settings-file tier and endpoint checks report `unknown (SET package not loaded)`. `CRED REMOVE -tier file` and `CRED MIGRATE -purge` report that they cannot clear the file tier and complete the parts they can. |
| Terminal cannot suppress echo | Warn, and require `-force`. Never silently echo a secret. |
| Running as a pipeline stage where a prompt would be needed | Refuse with an explanatory string naming the flag that makes it unattended (`-yes`). Never block on a prompt nobody can answer — the source's wizard did exactly that behind a full-screen UI. |
| A settings file that will not parse | Report `could not parse; cannot rule out plaintext credentials`. Do **not** replace it with defaults. The source's load path returned a defaults record on any parse error, which silently reverted the store-enabled flag to false for the session and discarded whatever the file held. |

**Deliberate improvements over the source, enumerated so a reviewer can object to each one individually.**

1. **No tool ever prints a secret.** Kills the migration-instructions disclosure, the every-load re-emission, and the terminal-scrollback leak.
2. **No secret is ever a command-line parameter.** Forced by the tokenizer's character scrubbing and by the framework's non-functional parameter masking; also removes the source's whitespace-collapsing quirk and its shell-history leak.
3. **Delete and rotate exist.** The source shipped a delete primitive with zero call sites and documented it as implemented.
4. **The store tier is cross-platform.** The source refused off-Windows and a test pinned the refusal; the dossier records this as an open product question. The rebuild answers it: **generalize**, keeping the Windows entry format byte-identical, keeping the refusal semantics per-backend (`keychain` is unavailable on Linux exactly as `wincred` is unavailable on macOS), and keeping `encfile` as an everywhere-fallback that is honestly labelled weak.
5. **Availability is a live probe, not an OS check.**
6. **Declining consent is a success.** The source reported a deliberate "no" as `Failed to enable…`.
7. **Outcomes are reported truthfully**, per slot, with partial results named.
8. **Profiles.** `-profile` namespaces store entries as `ChatDbg:<profile>:<Slot>`, with `default` mapping to the legacy names. The source wrote every entry with the fixed account label `ChatDbg` and matched on entry name only, so one OS user could not keep two ChatDbg configurations apart; the dossier records this as an unresolved question.
9. **Persistence and store failures are surfaced**, with the OS's reason where one exists. The source swallowed every exception at two layers and swallowed save failures entirely, so `/set useWindowsCredentialManager true` against a read-only settings file reported success and was lost at the next restart.
10. **Value validation before storage**, with the platform's 1280-character UTF-16 ceiling stated rather than discovered as a generic failure.
11. **One surface, one rule.** The source's console command refused to enable the store off-Windows while its GUI checkbox happily persisted the flag, after which every load warned forever and every read wasted a probe that could only return nothing. Every `CRED` tool goes through the same gate, and both shells drive the same tools.

**What is deliberately NOT a tool.**

* **`CRED REVEAL`** — a tool that prints a stored secret. It would be the single most useful capability for exfiltration in the product and would destroy the "no tool ever prints a secret" property that every other guarantee here rests on. Recovering a value the user themselves stored is the operating system's own credential UI's job, and `CRED MIGRATE -to env` says so in the one place a user genuinely needs it. If a future requirement forces it, it must be a separate, separately-loadable package with its own audit stream, so that a host can refuse to load it.
* **A tool that writes process environment variables.** It cannot work across a restart and would mislead.
* **A `CRED` tool that talks to a provider to validate a key.** That is the provider package's job and it costs money and network reach this package deliberately does not have. `CRED DOCTOR` diagnoses *custody*, not *acceptance*; the two are different questions and conflating them is how you get a diagnostic that fails because a rate limit was hit.

---

## 4. ChatDbg.Tools.SystemPromptLibrary — System Prompt Library

> Root command: **`PROMPT`**
> Source ancestor: feature 7 *System Prompt Management* (`Core/Services/SystemPromptService.cs`, `Core/Models/SystemPrompt.cs`, `Core/Commands/PromptCommand.cs`, `Gui/UI/SystemPromptsDialog.cs`).
> PRD traceability: **7.5 System Prompt Management** (owner), with named touch-points into 7.1, 7.2, 7.6, 7.11, 7.13, 7.14.

---

### 4.0 Purpose and boundary

**What this package owns.** The *library of named instruction texts* and everything that manipulates it:

* the on-disk prompt store (one record per prompt: `name`, `content`, `description`, `createdAt`, `lastUsedAt`);
* the built-in starter set of four prompts (`default`, `code-reviewer`, `algorithm-helper`, `security-expert`) and the rule that seeds them;
* listing, filtering, showing, creating, editing, renaming, duplicating, deleting, importing and exporting prompts;
* prompt metadata — description, created-at, last-used-at — including *when* last-used is stamped;
* the *act of selection*: naming which prompt is active and publishing that name plus its text so other packages can consume it;
* store diagnostics: where the store is, which files are unreadable, which names collide.

**What this package does NOT own.**

| Not owned | Owner |
|---|---|
| The persisted `systemPromptName` setting, the settings document at `~/.ChatDbg/settings.json`, and the `SET`-style key/value surface (`/set systemPrompt <n>` in the source) | **ChatDbg.Tools.Settings** (root `SET`) — PRD 7.2. `PROMPT USE` publishes the selection; the Settings package persists it. |
| Injecting the active prompt text into a model request as the system message | **ChatDbg.Tools.Providers** (root `AI`) — PRD 7.6/7.8. This package never calls a model. |
| Conversation turns, message injection, history import/export | **ChatDbg.Tools.ChatHistory** (root `HISTORY`) — PRD 7.4. A system prompt is not a chat message here. |
| Any secret, credential or keystore access | **ChatDbg.Tools.Credentials** (root `CRED`) — PRD 7.3. Prompt text is *not* a secret and is stored in clear text. |
| Counting or visualising the tokens a prompt costs | **ChatDbg.Tools.TokenInspection** (root `TOKEN`) — PRD 7.10. `PROMPT SHOW -content-only` pipes into it. |
| Rendering, colour, heat-maps, dialogs | **ChatDbg.Tools.Rendering** / the host shell — PRD 7.12/7.13/7.14. Every tool here emits text or a declared result format; nothing here draws. |
| Log files and log export | **ChatDbg.Tools.DiagnosticLogging** (root `LOG`) — PRD 7.11. `PROMPT DOCTOR` pipes into it. |

**Deliberate deviation from the source, stated once.** The source shipped two prompt surfaces — a text command and a Terminal.Gui dialog — that disagreed about duplicate protection, the active-prompt delete guard, last-used stamping and export defaults (dossier Q6, Q13, R21, R25). **This package is the single surface.** A graphical shell drives these same tools; it does not carry a second implementation. Every behavioural divergence recorded as Q6/Q8/Q13/Q19/Q20/Q21/Q27 in the dossier is therefore resolved *in favour of the command path*, except where a row below says otherwise.

---

### 4.1 Package manifest

| Attribute | Value |
|---|---|
| Assembly / package | `ChatDbg.Tools.SystemPromptLibrary` (`ChatDbg.Tools.SystemPromptLibrary.dll`) |
| Root command | `PROMPT` (declared once as `[CommandRoot("PROMPT", "System prompt library")]` on every tool class) |
| Contract assemblies targeted | `Xcaciv.Command.Interface` **3.3.4**, `Xcaciv.Command.Core` **3.3.4** (`AbstractCommand`, `IResult<string>`, `Dictionary<string,IParameterValue>` piped-chunk contract). No reference to `Xcaciv.Command` (the host) and none to any ChatDbg shell — the dependency arrow points tool → SDK only (Cupcake rule 4). |
| Target framework | `net10.0`, `ImplicitUsings`, `Nullable`, `IsPackable=true`, no `AllowUnsafeBlocks` |
| Elevated trust | **No.** No P/Invoke, no `Reflection.Emit`, no dynamic assemblies, no unsafe code, no process spawning *except* the opt-in `PROMPT EDIT -editor` path (below). Designed to load cleanly under `AssemblySecurityPolicy.Strict` with `DisallowDynamicAssemblies = true` and preflight enabled (ref-loader §10 Step 4). |
| Network | **None.** No socket is opened by any tool in this package. |
| Filesystem reach | Two directories, both configurable, both required to be *directories the tool resolves itself* — never a caller-supplied path (see §4.2): **(a)** the prompt store, default per-user local-application-data `/ChatDbg/system_prompts` (Windows `%LocalAppData%\ChatDbg\system_prompts`, Linux `$XDG_DATA_HOME/ChatDbg/system_prompts` → `~/.local/share/ChatDbg/system_prompts`, macOS `~/Library/Application Support/ChatDbg/system_prompts`); **(b)** the exchange directory used by `IMPORT`/`EXPORT`, default per-user documents directory, falling back to the home directory when the documents folder resolves empty (fixes dossier Q18 — it must **never** silently become the process working directory). |
| OS keystore | **None.** |
| Native libraries | **None.** |
| Process spawn | Only `PROMPT EDIT -editor`, which launches `$VISUAL`/`$EDITOR` (fallback `notepad` on Windows, `nano` then `vi` elsewhere). This is the one capability a restricted host should switch off; see `CHATDBG_PROMPT_ALLOW_EDITOR`. |
| Safe to load in a restricted host | **Yes**, with two conditions: the store directory must be writable (otherwise the package degrades to read-only, §4.16), and `CHATDBG_PROMPT_ALLOW_EDITOR` must be `false` if external editors are not permitted. Under a fully read-only policy every mutating tool fails with one clear message rather than throwing. |
| Environment-modifying registration | Exactly **one** tool needs it: `PROMPT USE`. Register as `controller.AddCommand("ChatDbg.Prompts", typeof(PromptUseCommand), modifiesEnvironment: true)`. Every other tool registers with the default `false`. |
| Audit posture | No parameter or output in this package is a secret. `-text` and `description` values *do* land verbatim in `AuditEvent.Parameters`; see §4.17. |

---

### 4.2 Conventions that apply to every tool in this package

These are stated once and referenced by each tool's tables.

**C1 — No tool in this package accepts a filesystem path as a parameter.** The framework's argument tokenizer (`NamesValidator.GetArgumentsFromCommandline`, ref-command §8.3) splits unquoted tokens on `[\w-]+` and strips `\ / : = , ; < > { } +` **even inside quotes**. `"/home/u/p.txt"` reaches a command as `homeup.txt`. Path parameters are therefore *unimplementable* in this framework and are replaced by **basenames resolved against the exchange directory** (`-file <basename>`), which also gives path confinement for free. Absolute paths remain reachable through two un-tokenized channels: the interactive prompt (`IIoContext.PromptForCommand`, whose return value is **not** tokenized) and the environment (`CHATDBG_PROMPT_EXCHANGE_DIR`, set by the host from configuration, not by the `SET` command — `SET`'s value token is tokenized too). This is a deliberate, documented deviation from the source's raw-path `export`/`import` (dossier B9/B10, Q17).

**C2 — Content is never authored inline where fidelity matters.** The same tokenizer strips `: ; < > / \ { } + = ,` from every argument, so `-text "You are ChatDBG: help users…"` loses the colon. Inline `-text` exists for short, alphanumeric content only and every tool that offers it says so in its help. The fidelity-preserving content channels are, in order of preference: **the pipe** (piped chunks are delivered verbatim as `IResult<string>.Output`, untouched by the tokenizer), **`-file <basename>`**, and **the interactive editor** (`PromptForCommand` loop, also verbatim).

**C3 — Zero-argument invocations get no parameter defaults.** `AbstractCommand.ProcessParameters` returns an empty dictionary when `io.Parameters.Length == 0` (ref-command §3.4) — no defaults applied, no flags materialised, no field injection. Every tool here is specified to fall back to its documented default *in code*, using `parameters.TryGetValue(k, out var p) && p.IsValid ? p.GetValue<T>() : <default>`. The tables below give the effective default, which is what the user observes whether or not any argument was typed.

**C4 — Name matching is exact, ordinal, and case-preserving, on every operating system.** The source inherited case sensitivity from the host file system (dossier R6) — case-insensitive on Windows, case-sensitive on Linux, so the same store behaved differently per OS. This package pins **ordinal, case-sensitive** matching everywhere and stores an explicit index, so `Default` and `default` are two prompts on every platform.

**C5 — File names are sanitized with one fixed, platform-independent forbidden set.** The source delegated to the host OS: 41 forbidden characters on Windows, 2 on Linux, making stores non-portable (dossier R3, R5, Q21). This package pins the **Windows set** on all platforms — code points 0–31 plus `" < > | : * ? \ /` — each replaced by `_`, and additionally refuses (rather than silently collides) when two distinct names sanitize to the same file name. Reserved Windows device names (`CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`, `LPT1`–`LPT9`) are prefixed with `_` on every platform.

**C6 — Ordering.** Default listing order is ascending by name using **culture-sensitive** collation, exactly as the source (dossier R11: `a, a_b, a-b, ab, B`, *not* the ordinal `B, a, a-b, a_b, ab`). `-collation ordinal` switches to byte order. Note for the architect: the source's size-optimised publish profiles disabled globalization and silently changed this ordering — the package therefore ships its own collation selection rather than inheriting the runtime's.

**C7 — Timestamps.** Stored UTC, ISO-8601 round-trip with `Z`. Rendered converted to local time in the culture's general short date/time pattern (date + `HH:mm`, no seconds), and the literal `Never used` when `lastUsedAt` is absent — both verbatim from the source (dossier R24, R27). `-format json` emits raw UTC instead.

**C8 — On-disk record.** One JSON file per prompt, `<sanitized name>.json`, five keys in declaration order: `name`, `content`, `description`, `createdAt`, `lastUsedAt`. `lastUsedAt` is always written, as an explicit `null` when unset (dossier Q24 — preserved, because omitting it changes nothing for readers and keeps the file self-describing). **Deviation:** every write is indented, including the seeded built-ins — the source seeded compact and wrote indented afterwards (Q1), a cosmetic accident with no consumer. **Deviation:** every write is temp-file-plus-atomic-rename with an advisory lock file, closing the source's clobber/truncation window (Q16).

**C9 — Failure is data, not an exception.** Every tool returns `CommandResult<string>.Failure(message, ex)` rather than throwing. A throw would be reduced by `CommandExecutor` to `"Error executing <CMD> (see trace for more info)"` with the real reason visible only in the trace (ref-command §5.8), which is exactly the source's opaque `Error processing prompt command: <message>` behaviour and is worth improving on.

**C10 — Environment keys are global and explicit.** This package declares an **empty `GetDefaultEnvironment()`** on every tool. Rationale: the host seeds declared defaults under the `{COMMANDNAME}_` prefix (ref-command §2.4), and for sub-commands the command name is the *verb* (`LIST`, `USE`), which would produce collision-prone keys such as `LIST_DIR` shared with every other package's `LIST`. Instead each tool reads fully-qualified global keys (`CHATDBG_PROMPT_*`) with `storeDefault: false`, so a mere read never flips `HasChanged` and never provokes a write-back.

---

### 4.3 Tool catalog

Fourteen tools. Nine are ported; five are `NEW`.

| Tool | Origin | One line |
|---|---|---|
| `PROMPT STATUS` | ported: bare `/prompt` | Show the active prompt and its text |
| `PROMPT LIST` | ported: `/prompt list` | Enumerate, filter and sort the library |
| `PROMPT SHOW` | ported: `/prompt show <n>` | Show one prompt with its metadata, or just its text |
| `PROMPT USE` | ported: `/prompt use <n>`, `/set systemPrompt <n>` | Select the active prompt |
| `PROMPT CREATE` | ported: `/prompt create <n> [desc]` | Create a new prompt |
| `PROMPT EDIT` | ported: `/prompt edit <n>` | Replace a prompt's content and/or description |
| `PROMPT RENAME` | **NEW** | Rename a prompt, preserving all metadata |
| `PROMPT COPY` | **NEW** | Duplicate a prompt under a new name |
| `PROMPT DELETE` | ported: `/prompt delete <n>` | Delete a prompt |
| `PROMPT IMPORT` | ported: `/prompt import <n> <file> [desc]` | Import a prompt from the exchange directory |
| `PROMPT EXPORT` | ported: `/prompt export <n> [file]` | Export a prompt to the exchange directory |
| `PROMPT RESET` | **NEW** | Restore missing (or all) built-in starter prompts |
| `PROMPT PATH` | **NEW** | Report the store and exchange directories |
| `PROMPT DOCTOR` | **NEW** | Diagnose the store: unreadable files, collisions, dangling active name |

---

#### 4.3.1 `PROMPT STATUS` — show the active prompt

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Status", "Show the currently active system prompt",
    Prototype = "PROMPT STATUS [-format text|json] [-content-only]",
    Alias = "CURRENT", Version = "1.0.0")]
[CommandParameterNamed("format", "Output shape",
    DefaultValue = "text", AllowedValues = new[] { "text", "json" })]
[CommandFlag("content-only", "Emit only the prompt text, with no headings")]
[CommandHelpRemarks("The source product spelled this as a bare '/prompt'. The framework requires a sub-command on a root, so the bare form is unavailable; STATUS is its replacement.")]
```

| Field | Value |
|---|---|
| Command | `STATUS` |
| Root | `PROMPT` |
| Description | Show the currently active system prompt and its text |
| Prototype | `PROMPT STATUS [-format text\|json] [-content-only]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text`, `json` | Output shape. `json` sets `ResultFormat.JSON` on the chunk. |
| `content-only` | flag | `bool` | no | `false` | — | Emit only the prompt text — the pipe-friendly form. **NEW** |

**Pipeline behaviour.** Produces piped output; does **not** accept piped input (it has no per-item work to do). One chunk. When `-content-only` is set the chunk is exactly the prompt text with no trailing hints, so it composes cleanly into `TOKEN COUNT` or `AI ASK`. Without it the chunk reproduces the source's five-part block verbatim:

```
Current system prompt: <activeName>

Content:
<text>

Use 'PROMPT LIST' to see all available prompts
Use 'PROMPT USE <name>' to switch to another prompt
```

(The source printed `<n>` as its name placeholder everywhere, including the README — dossier Q22. This package prints `<name>`; recorded as a deliberate deviation because `<n>` reads as "a number".)

**Environment interaction.** Reads `CHATDBG_SYSTEM_PROMPT` (active name; default `default`), `CHATDBG_SYSTEM_PROMPT_TEXT` (runtime active text), `CHATDBG_PROMPT_DIR`. Writes nothing. No environment-modifying permission needed.

**Failure modes.** Active name names a prompt that no longer exists → the block is still emitted, using `CHATDBG_SYSTEM_PROMPT_TEXT` if the host set it, otherwise the built-in fallback text, plus one line `(the prompt named '<name>' is no longer in the library — run 'PROMPT DOCTOR')`. Store unreadable → success with the runtime text and the same advisory. Never fails.

**Security and audit.** No secret. Non-destructive. `-content-only` output may be long; the audit event records parameters only, not output.

**Traceability.** PRD 7.5; source `/prompt` with no arguments (`Commands/PromptCommand.cs:58-73`), plus the startup banner's `System Prompt: <name>` line (`src/ChatDbg/ChatShell.cs:201`).

---

#### 4.3.2 `PROMPT LIST` — enumerate the library

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("List", "List the system prompts in the library",
    Prototype = "PROMPT LIST [-name-like <glob>] [-text-like <words>] [-sort name|created|lastused] [-collation culture|ordinal] [-format text|names|csv|json] [-limit <n>] [-unused] [-builtin]")]
[CommandParameterNamed("name-like", "Case-insensitive glob over prompt names, e.g. code-*")]
[CommandParameterNamed("text-like", "Case-insensitive substring match over prompt content")]
[CommandParameterNamed("sort", "Sort key", DefaultValue = "name",
    AllowedValues = new[] { "name", "created", "lastused" })]
[CommandParameterNamed("collation", "Name-ordering rule", DefaultValue = "culture",
    AllowedValues = new[] { "culture", "ordinal" })]
[CommandParameterNamed("format", "Output shape", DefaultValue = "text",
    AllowedValues = new[] { "text", "names", "csv", "json" })]
[CommandParameterNamed("limit", "Maximum entries to emit; 0 means all",
    DataType = typeof(int), DefaultValue = "0")]
[CommandFlag("unused", "Only prompts that have never been used")]
[CommandFlag("builtin", "Only the four built-in starter prompts")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name-like` | named | `string` | no | *(none — no filter)* | glob, `*` and `?` | Filter by name. **NEW** |
| `text-like` | named | `string` | no | *(none)* | free text (tokenizer-limited, see C2) | Filter by content substring, case-insensitive. **NEW** |
| `sort` | named | `string` | no | `name` | `name`, `created`, `lastused` | Sort key. Source always sorted by name (R11). **NEW** for the other two. |
| `collation` | named | `string` | no | `culture` | `culture`, `ordinal` | `culture` reproduces the source's ordering exactly. **NEW** |
| `format` | named | `string` | no | `text` | `text`, `names`, `csv`, `json` | `text` = source's three-line entry block; `names` = one bare name per chunk. **NEW** |
| `limit` | named | `int` | no | `0` | `0`–`1000`; clamped, `0` = unlimited | Bound the output. **NEW** (Cupcake rule 45: clamp result counts). |
| `unused` | flag | `bool` | no | `false` | — | Only prompts whose `lastUsedAt` is unset. **NEW** |
| `builtin` | flag | `bool` | no | `false` | — | Only `default`, `code-reviewer`, `algorithm-helper`, `security-expert`. **NEW** |

**Pipeline behaviour.** **Source only** — emits piped output, refuses piped input. Because `AbstractCommand`'s non-piped path emits exactly one chunk (ref-command §3.3), this tool **overrides `Main`** to yield **one chunk per prompt**, so downstream stages see individual records and backpressure works. `-format names` makes each chunk a bare prompt name, which is the canonical feed for `SHOW`, `EXPORT`, `DELETE` and `USE`. Declared output shape: `ResultFormat.General` for `text`/`names`, `ResultFormat.CSV` for `csv`, `ResultFormat.JSON` for `json` — metadata only, since the framework never branches on it (ref-command §10.5); the host renderer is what reads it. When piped input is present the tool emits a single explanatory failure chunk, `PROMPT LIST does not accept piped input; it is a pipeline source.` (Cupcake rule 29.)

Text entry shape, verbatim from the source (dossier B2, acceptance criterion 3):

```
- <name>[ (current)]
  Description: <description>
  Created: <local date HH:mm>, Last used: <local date HH:mm>      ← or "Never used"
```

with a blank line after each entry, preceded by `Available system prompts:` and a blank line, and — **fixing dossier Q2** — closed by `System prompts directory: <the real absolute path>` instead of the source's accidental rendering of a .NET collection type name (`System.Collections.Generic.List` of `SystemPrompt`). Empty store → the single line `No system prompts found.`, a **success** (source B2, acceptance criterion 5).

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_SYSTEM_PROMPT` (for the ` (current)` marker). Writes nothing.

**Failure modes.** Store directory missing → it is created, then the built-in seeding rule runs (§4.3.12), so the first list on a clean machine shows four prompts. Store directory unreadable or vanished mid-run → one failure chunk `Cannot read the prompt library at <path>: <reason>` — the source let this escape as the generic `Error processing prompt command:` (Q26). Individual unreadable/corrupt files → **skipped, and counted**, with a trailing line `1 file could not be read; run 'PROMPT DOCTOR' for details.`; the source wrote a raw line to stdout that the GUI shell swallowed entirely (R9). `limit` outside range → clamped silently. Unknown `format`/`sort` value → `ArgumentException` from the allow-list at parse time, surfaced by the host as `Error executing LIST (see trace for more info)`; the prototype and `HELP PROMPT LIST` carry the allowed values.

**Security and audit.** No secret. Non-destructive. `-text-like` values appear in the audit event's parameter array; harmless.

**Traceability.** PRD 7.5; source `/prompt list` (`Commands/PromptCommand.cs:76-108`) and the GUI list pane (`UI/SystemPromptsDialog.cs:131-143`).

---

#### 4.3.3 `PROMPT SHOW` — show one prompt

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Show", "Show one system prompt and its metadata",
    Prototype = "PROMPT SHOW <name> [-content-only] [-format text|json]")]
[CommandParameterOrdered("name", "Name of the prompt to show", UsePipe = true)]
[CommandFlag("content-only", "Emit only the prompt text")]
[CommandParameterNamed("format", "Output shape", DefaultValue = "text",
    AllowedValues = new[] { "text", "json" })]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** (unless piped — `UsePipe = true`) | — | any existing prompt name | The prompt to show. |
| `content-only` | flag | `bool` | no | `false` | — | Emit only the text, no headings. **NEW** |
| `format` | named | `string` | no | `text` | `text`, `json` | `json` emits the whole record including raw UTC timestamps. **NEW** |

**Pipeline behaviour.** **Both.** As a source it emits one chunk. As a filter it accepts piped input: **one chunk = one prompt name** (trailing/leading whitespace trimmed; empty chunks are already dropped by `AbstractCommand.Main`), and emits one chunk per input name, so `PROMPT LIST -format names | PROMPT SHOW -content-only` concatenates the whole library. Because `name` is declared `UsePipe = true`, it is excluded from command-line parsing when piped, so the name need not — and must not — be given twice.

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_SYSTEM_PROMPT` (to mark the shown prompt as current). Writes nothing. Does **not** stamp `lastUsedAt` — matching the source exactly (dossier R25: only selection stamps).

**Failure modes.** Missing name and no pipe → failure `Please specify a prompt name: PROMPT SHOW <name>` (source wording, `<n>` → `<name>`). Unknown name → failure `Prompt not found: <name>` (verbatim). Corrupt record file → **distinguishable from missing**, unlike the source (which reported both as "not found", dossier error table): `Prompt '<name>' exists but could not be read: <reason>`. Piped chunk that is not a known name → one failure chunk for that item; the stream continues with the next chunk.

**Security and audit.** No secret. Non-destructive. Prompt content is user-authored text and is echoed to output — a host that pipes prompt content into an audit sink should be aware it is unbounded in length.

**Traceability.** PRD 7.5; source `/prompt show <n>` (`Commands/PromptCommand.cs:110-143`) and GUI **Show** (`UI/SystemPromptsDialog.cs:168-202`).

---

#### 4.3.4 `PROMPT USE` — select the active prompt

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Use", "Make a prompt the active system prompt",
    Prototype = "PROMPT USE <name> [-no-stamp]")]
[CommandParameterOrdered("name", "Name of the prompt to activate", UsePipe = true)]
[CommandFlag("no-stamp", "Do not update the prompt's last-used timestamp")]
[CommandHelpRemarks("Register this tool with modifiesEnvironment: true. Without it the selection cannot reach the global environment and the tool falls back to the store marker file.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** (unless piped) | — | any existing prompt name | The prompt to activate. |
| `no-stamp` | flag | `bool` | no | `false` | — | Suppress the `lastUsedAt` update. **NEW** — exists so scripted/bulk selection does not rewrite records. |

**Pipeline behaviour.** **Both.** As a filter, one chunk = one prompt name; each chunk performs a selection and emits `Now using system prompt: <name>` (verbatim source wording). Last-selection-wins, which makes `… | PROMPT USE` in a multi-name pipeline a legal but pointless composition — documented rather than blocked.

**Environment interaction.** This is the only tool here that writes globals.
* Reads: `CHATDBG_PROMPT_DIR`, `CHATDBG_SYSTEM_PROMPT` (previous value, for the audit trail).
* Writes: `CHATDBG_SYSTEM_PROMPT` = the selected name, and `CHATDBG_SYSTEM_PROMPT_TEXT` = the selected text.
* **Requires environment-modifying permission**: registered with `modifiesEnvironment: true` so `CommandController.Run` promotes the child environment into globals (ref-command §7.3a). A package-directory load registers with `modifiesEnvironment: false` by default, so in that mode the promotion silently would not happen — see the degradation rule below.
* `CHATDBG_SYSTEM_PROMPT_TEXT` is **runtime-only and must never be persisted**, mirroring the source's `[JsonIgnore]` on the active-text field (dossier R19). The Settings package persists only the *name*.

**Order of effects**, matching the source (dossier B4) with the persistence step relocated:
1. Resolve the name in the store (fail if unknown — no state changes).
2. Publish name and text to the global environment.
3. Stamp `lastUsedAt = UtcNow` and rewrite the record atomically, unless `-no-stamp`.
4. Write the store marker file `<store>/.active` (**NEW** — the degradation path).
5. Emit `Now using system prompt: <name>`.

The source's step "persist settings to disk" is *not* performed here: publishing to the environment is the contract, and **ChatDbg.Tools.Settings** persists `systemPromptName` in response (source R22 — settings were saved immediately on selection; that guarantee is preserved, the owner of it moves).

**Failure modes.** Missing name and no pipe → `Please specify a prompt name: PROMPT USE <name>`. Unknown name → `Prompt not found: <name>` and **no state change of any kind** (source acceptance criterion 7). Store read-only → the selection still publishes to the environment and the tool returns success with `Selected '<name>'; the last-used timestamp could not be written (library is read-only).` — degrade, do not fail. Not registered as environment-modifying → the publish cannot reach globals; the tool detects this (it re-reads its own child environment), writes the `.active` marker, and returns success with `Selected '<name>'. This host did not grant environment write permission, so the selection was recorded in the library only; restart or 'SET systemPrompt <name>' to apply it.` This is the single most important degrade-don't-fail path in the package.

**Security and audit.** No secret. Not destructive, but it *is* a state change with process-wide effect: the audit event records the command, the parameter array, and — via `EnvironmentContext.SetValue`'s `LogEnvironmentChange` — the old and new values of `CHATDBG_SYSTEM_PROMPT`. `CHATDBG_SYSTEM_PROMPT_TEXT` is a full prompt body and will be written in full into the environment-change audit record; a host that wants terse audit lines should add `CHATDBG_SYSTEM_PROMPT_TEXT` to `IAuditMaskingConfiguration.RedactedParameterNames`, which *does* work on the environment-change path (ref-command §11.4). No confirmation required.

**Traceability.** PRD 7.5 (primary) and 7.2 (the persisted setting it feeds); source `/prompt use <n>` (`Commands/PromptCommand.cs:145-171`), `/set systemPrompt <n>` (`Commands/SetCommand.cs:138-161`), GUI **Use** (`UI/SystemPromptsDialog.cs:205-226`). The GUI's failure to stamp last-used (Q6/R25) is not reproduced; stamping is now uniform.

---

#### 4.3.5 `PROMPT CREATE` — create a prompt

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Create", "Create a new system prompt",
    Prototype = "PROMPT CREATE <name> [-text <words>] [-file <basename>] [-force] [<description words...>]")]
[CommandParameterOrdered("name", "Name of the new prompt")]
[CommandParameterNamed("text", "Prompt content, inline (see remarks about lossy tokenization)")]
[CommandParameterNamed("file", "Basename of a file in the exchange directory to use as the content")]
[CommandFlag("force", "Overwrite an existing prompt of the same name")]
[CommandParameterSuffix("description", "Description for the new prompt", IsRequired = false)]
[CommandHelpRemarks("Inline -text is lossy: the argument tokenizer strips ':' '/' '\\' ';' '<' '>' '{' '}' '+' '=' ',' even inside quotes. For real prompt text pipe it in, use -file, or run PROMPT EDIT.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** | — | non-empty after sanitization (C5) | Name of the new prompt. |
| `text` | named | `string` | no | *(built-in default text — see below)* | tokenizer-limited (C2) | Inline content. **NEW** (the source's create never accepted content). |
| `file` | named | `string` | no | *(none)* | basename only, no separators (C1); must exist in the exchange directory | Content from a file. **NEW** |
| `force` | flag | `bool` | no | `false` | — | Overwrite an existing prompt. **NEW** |
| `description` | suffix | `string` | no | `Custom prompt: <name>` | free text (tokenizer-limited) | All remaining words become the description — the source's exact behaviour. |

When neither `-text`, `-file` nor piped input supplies content, the new prompt gets the **built-in default text**, verbatim from the source (dossier R20):

> `You are ChatDBG, a helpful debugging assistant. Help users analyze code, debug issues, and understand programming concepts.`

`createdAt` = now (UTC); `lastUsedAt` = unset.

**Pipeline behaviour.** **Both, with a special piped contract.** As a source it emits one chunk. When piped, **the entire upstream stream is the new prompt's content**: `OnStartPipe` opens a buffer, each chunk is appended with a newline separator, `OnEndPipe` writes the record once and the per-chunk return value is an **empty success** so nothing is echoed mid-stream (the host drops empty successes — ref-command §3.3). The confirmation chunk is emitted from `OnEndPipe`'s flush. This is the fidelity-preserving authoring path (C2): `HISTORY SHOW -last | PROMPT CREATE from-session` captures text the tokenizer would otherwise mangle. If the pipe yields nothing at all, the prompt is created with the built-in default text and the message notes it.

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_PROMPT_EXCHANGE_DIR`, `CHATDBG_PROMPT_MAX_CHARS`. Writes nothing. No environment-modifying permission needed.

**Failure modes.** Missing name → `Please specify a prompt name: PROMPT CREATE <name> [description]`. Name already exists and no `-force` → `Prompt already exists: <name>. Use 'PROMPT EDIT <name>' to modify it.` (source wording, command spelling updated) — **creation stays non-destructive**, and the GUI's silent overwrite (Q6) is *not* reproduced. Name empty or whitespace after sanitization → `'<name>' is not a usable prompt name.` (the source accepted these, R4). Name collides with an existing file after sanitization → `'<name>' would overwrite the storage of '<other>'; choose a different name.` (fixes R5). `-file` names a file that does not exist in the exchange directory → `File not found in the exchange directory: <basename>. Run 'PROMPT PATH' to see where that is.` `-file` and `-text` both given → `Specify only one of -text and -file.` Content longer than `CHATDBG_PROMPT_MAX_CHARS` → `Content is <n> characters; the limit is <max>. Raise CHATDBG_PROMPT_MAX_CHARS or shorten it.` Store read-only → `The prompt library at <path> is read-only.` Upstream failure chunk arriving through the pipe → forwarded verbatim by `AbstractCommand.Main` and the buffer is discarded without writing anything: **a broken upstream never produces a half-written prompt.**

**Security and audit.** No secret, but see §4.17: an inline `-text` value is recorded in the audit event's parameter array in full. Non-destructive without `-force`; with `-force` it is an overwrite and therefore requires confirmation (see §4.17 confirmation rule).

**Traceability.** PRD 7.5; source `/prompt create <n> [description]` (`Commands/PromptCommand.cs:173-211`) and GUI **Create…** (`UI/SystemPromptsDialog.cs:228-338`), whose Name/Content-required validation is preserved and whose missing duplicate check is not.

---

#### 4.3.6 `PROMPT EDIT` — replace content and/or description

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Edit", "Replace a prompt's content and/or description",
    Prototype = "PROMPT EDIT <name> [-text <words>] [-file <basename>] [-editor] [-description <words>] [-append]")]
[CommandParameterOrdered("name", "Name of the prompt to edit")]
[CommandParameterNamed("text", "Replacement content, inline (lossy — see remarks)")]
[CommandParameterNamed("file", "Basename of a file in the exchange directory to use as the content")]
[CommandParameterNamed("description", "Replacement description")]
[CommandFlag("editor", "Open the content in $VISUAL/$EDITOR")]
[CommandFlag("append", "Append to the existing content instead of replacing it")]
[CommandHelpRemarks("With no content source, EDIT enters interactive mode: type the new content and end with a line containing only END. Ctrl-D / end-of-input also ends the edit.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** | — | any existing prompt name | The prompt to edit. |
| `text` | named | `string` | no | *(none)* | tokenizer-limited (C2) | Inline replacement content. **NEW** |
| `file` | named | `string` | no | *(none)* | basename only (C1) | Replacement content from a file. **NEW** |
| `description` | named | `string` | no | *(unchanged)* | free text | Replacement description. **NEW at the command surface** — the source could only change a description through the GUI dialog (dossier B7). |
| `editor` | flag | `bool` | no | `false` | — | Round-trip the content through `$VISUAL`/`$EDITOR`. **NEW**, and refused when `CHATDBG_PROMPT_ALLOW_EDITOR` is `false`. |
| `append` | flag | `bool` | no | `false` | — | Append rather than replace. **NEW** |

**Content-source precedence** (exactly one is used, in this order): piped input → `-file` → `-text` → `-editor` → interactive `END`-terminated entry. Giving two explicit sources is an error, not a silent precedence win.

**Interactive mode** reproduces the source's console editor (dossier B7) through `IIoContext.PromptForCommand`, which is un-tokenized and therefore lossless:

```
Editing system prompt: <name>
Enter the new content below. Type 'END' on a line by itself when finished.
Current content:
<existing text>

New content (END to finish):
```

Terminated by a line equal to `END` (case-sensitive, untrimmed — the source's exact sentinel). **Fixes dossier Q4:** end-of-input also terminates the loop, and an accumulated-length ceiling (`CHATDBG_PROMPT_MAX_CHARS`) bounds it, so a closed or redirected stdin can no longer spin forever appending blank lines. Trailing whitespace and newlines are stripped from the result, as in the source. Interactive mode is **unavailable when `HasPipedInput` is true** (contract of `PromptForCommand`) — which is fine, since a pipe is itself a content source.

**Pipeline behaviour.** **Both**, with the same accumulate-then-flush contract as `CREATE`: one chunk is one line/segment of the new content, chunks return empty successes, and `OnEndPipe` performs the single atomic write and emits `Updated system prompt: <name>` (verbatim source wording).

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_PROMPT_EXCHANGE_DIR`, `CHATDBG_PROMPT_MAX_CHARS`, `CHATDBG_PROMPT_ALLOW_EDITOR`, `CHATDBG_SYSTEM_PROMPT` (to detect editing the active prompt), and `VISUAL` / `EDITOR` only under `-editor`. Writes `CHATDBG_SYSTEM_PROMPT_TEXT` **only when the edited prompt is the active one** — the source refreshed the in-memory active text in the console path but not in the GUI path (B7 vs Q6); this package always refreshes, and because that write is a global it needs `modifiesEnvironment: true` to take effect. When the host has not granted it, the tool degrades: the record is saved and the message adds `Restart or run 'PROMPT USE <name>' for the change to take effect in this session.`

**Failure modes.** Missing name → `Please specify a prompt name: PROMPT EDIT <name>`. Unknown name → `Prompt not found: <name>. Use 'PROMPT CREATE <name>' to create it.` (verbatim). Two content sources → `Specify only one content source (-text, -file, -editor or a pipe).` `-editor` with editors disabled → `External editors are disabled in this host (CHATDBG_PROMPT_ALLOW_EDITOR=false). Use -file or pipe the content in.` `-editor` with no editor resolvable → names the variables it tried. Editor exits non-zero or the temp file is unchanged → **no write**, message `Edit cancelled; '<name>' is unchanged.` Content exceeds the ceiling → same message as `CREATE`. Upstream failure through the pipe → forwarded, buffer discarded, record untouched.

**Security and audit.** No secret. **Destructive in the sense that it overwrites content that cannot be recovered** — see the confirmation rule in §4.17: `EDIT` writes a single-generation backup `<store>/.backup/<sanitized>.json` before replacing, which `PROMPT DOCTOR` reports and which is the cheapest possible undo. `-editor` spawns a process; a restricted host must disable it.

**Traceability.** PRD 7.5; source `/prompt edit <n>` (`Commands/PromptCommand.cs:241-283`) and GUI **Edit…** (`UI/SystemPromptsDialog.cs:340-431`). The GUI's ability to edit descriptions is folded in; its failure to refresh the active text is not.

---

#### 4.3.7 `PROMPT RENAME` — rename a prompt  **NEW**

**Why it earns its place.** In the source the only way to rename was export → import → delete, which silently reset `createdAt`, dropped the description, and (per Q12) could not round-trip metadata at all. Renaming is the single most common library-maintenance action and it should not destroy provenance.

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Rename", "Rename a prompt, preserving its description and timestamps",
    Prototype = "PROMPT RENAME <name> <newname> [-force]")]
[CommandParameterOrdered("name", "Existing prompt name")]
[CommandParameterOrdered("newname", "New prompt name")]
[CommandFlag("force", "Overwrite an existing prompt at the new name")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** | — | existing prompt | Prompt to rename. **NEW** |
| `newname` | ordered | `string` | **yes** | — | usable name (C5), must not collide after sanitization | New name. **NEW** |
| `force` | flag | `bool` | no | `false` | — | Overwrite an existing prompt at the new name. **NEW** |

**Pipeline behaviour.** Neither source nor filter in a meaningful sense: it emits one confirmation chunk and **declines piped input** with `PROMPT RENAME does not accept piped input (it needs two names).` — the explanatory-refusal convention (Cupcake rule 29).

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_SYSTEM_PROMPT`. Writes `CHATDBG_SYSTEM_PROMPT` **only when the renamed prompt was the active one**, so the active selection follows the rename instead of dangling — requires `modifiesEnvironment: true`, degrading with an advisory when absent.

**Failure modes.** Either name missing → `Please specify both names: PROMPT RENAME <name> <newname>`. Unknown source → `Prompt not found: <name>`. Target exists without `-force` → `Prompt already exists: <newname>. Use -force to replace it.` New name unusable or colliding after sanitization → the `CREATE` wording. Write failure mid-operation → the operation is a write-new-then-delete-old sequence, so a crash leaves **both** copies rather than none; the message says which state the library is in and `PROMPT DOCTOR` reports the duplicate.

**Security and audit.** No secret. **Destructive with `-force`** → confirmation required. `createdAt` is preserved; `lastUsedAt` is preserved; only `name` changes.

**Traceability.** **NEW** — no source ancestor. PRD 7.5.

---

#### 4.3.8 `PROMPT COPY` — duplicate a prompt  **NEW**

**Why it earns its place.** The source's two-step authoring flow (`create` then `edit`) always started from the same generic default text, so deriving a variant of `security-expert` meant re-typing or re-importing it. Duplication is the natural way to fork a persona, and it is explicitly in this package's scope.

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Copy", "Duplicate a prompt under a new name",
    Prototype = "PROMPT COPY <name> <newname> [-force] [<description words...>]",
    Alias = "DUPLICATE")]
[CommandParameterOrdered("name", "Prompt to copy")]
[CommandParameterOrdered("newname", "Name of the copy")]
[CommandFlag("force", "Overwrite an existing prompt at the new name")]
[CommandParameterSuffix("description", "Description for the copy", IsRequired = false)]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** | — | existing prompt | Source prompt. **NEW** |
| `newname` | ordered | `string` | **yes** | — | usable name (C5) | Name of the copy. **NEW** |
| `force` | flag | `bool` | no | `false` | — | Overwrite an existing target. **NEW** |
| `description` | suffix | `string` | no | `Copy of <name>` | free text | Description for the copy. **NEW** |

The copy gets `createdAt` = now (it is a new record) and `lastUsedAt` unset; content is byte-identical to the source prompt.

**Pipeline behaviour.** Emits one chunk. Declines piped input, explanatorily, for the same reason as `RENAME`.

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`. Writes nothing. No environment-modifying permission.

**Failure modes.** As `RENAME`, minus the active-prompt concern (the original is untouched, so the active selection never dangles).

**Security and audit.** No secret. Destructive only with `-force` → confirmation required.

**Traceability.** **NEW** — no source ancestor. PRD 7.5.

---

#### 4.3.9 `PROMPT DELETE` — delete a prompt

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Delete", "Delete a system prompt",
    Prototype = "PROMPT DELETE <name> [-force] [-yes]", Alias = "REMOVE")]
[CommandParameterOrdered("name", "Name of the prompt to delete", UsePipe = true)]
[CommandFlag("force", "Delete even if it is the active prompt")]
[CommandFlag("yes", "Skip the confirmation question")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** (unless piped) | — | existing prompt | Prompt to delete. |
| `force` | flag | `bool` | no | `false` | — | Override the active-prompt guard. **NEW** — the source's command path refused unconditionally, the GUI path had no guard at all (R21). |
| `yes` | flag | `bool` | no | `false` | — | Skip the interactive confirmation. **NEW** — required for non-interactive and piped use. |

**Confirmation.** Interactive and not `-yes` → `IIoContext.PromptForCommand("Delete prompt '<name>'? [y/N] ")`, defaulting to **No** on anything but `y`/`yes` (case-insensitive). The source's GUI listed **Yes** first and focused it, so a stray Enter deleted the alphabetically-first prompt (Q19 + Q27); defaulting to No is the deliberate correction. When `HasPipedInput` is true, `PromptForCommand` is not meaningful, so **`-yes` is mandatory in a pipeline**: without it each piped chunk fails with `Refusing to delete '<name>' in a pipeline without -yes.`

**Pipeline behaviour.** **Both.** As a filter, one chunk = one prompt name; each deletion emits `Deleted system prompt: <name>` (verbatim) or one failure chunk, and the stream continues — a single bad name never aborts a bulk delete.

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_SYSTEM_PROMPT`. Writes nothing. When `-force` deletes the active prompt, the tool does **not** silently leave a dangling selection: it emits an additional line `The active prompt was deleted; 'PROMPT STATUS' will fall back to the built-in default text until you run 'PROMPT USE'.` (The source's GUI left exactly this dangling state with no notice — R21.)

**Failure modes.** Missing name → `Please specify a prompt name: PROMPT DELETE <name>`. Unknown name → `Prompt not found: <name>`. Active prompt without `-force` → `Cannot delete the currently active prompt. Switch to another prompt first with 'PROMPT USE <name>'.` (source wording; `<n>` → `<name>`). Already gone → treated as success with `Prompt '<name>' was already absent.` (the source's store silently succeeded here — dossier error table; making it explicit costs nothing). Store read-only → `The prompt library at <path> is read-only.` Deleting the **last** prompt is allowed and warns: `The library is now empty; the four built-in prompts will be restored on the next launch (or run 'PROMPT RESET' now).` — this is the source's R15 behaviour, surfaced instead of surprising.

**Security and audit.** No secret. **Irreversible and destructive** → confirmation required by default (see above); the deleted record is copied to `<store>/.backup/` first, so `PROMPT DOCTOR` can report one generation of recoverable deletions. The audit event's parameter array carries the deleted prompt's name — deliberately, since deletion is the action most worth auditing here.

**Traceability.** PRD 7.5; source `/prompt delete <n>` (`Commands/PromptCommand.cs:213-239`) and GUI **Delete** (`UI/SystemPromptsDialog.cs:433-459`).

---

#### 4.3.10 `PROMPT IMPORT` — import a prompt from a file

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Import", "Import a prompt from a file in the exchange directory",
    Prototype = "PROMPT IMPORT <name> <basename> [-format text|json|auto] [-force] [<description words...>]")]
[CommandParameterOrdered("name", "Name to give the imported prompt")]
[CommandParameterOrdered("basename", "File name (no directories) inside the exchange directory")]
[CommandParameterNamed("format", "Interpretation of the file", DefaultValue = "auto",
    AllowedValues = new[] { "auto", "text", "json" })]
[CommandFlag("force", "Overwrite an existing prompt of the same name")]
[CommandParameterSuffix("description", "Description for the imported prompt", IsRequired = false)]
[CommandHelpRemarks("Only a bare file name is accepted: the argument tokenizer removes path separators. Run 'PROMPT PATH' to see, or CHATDBG_PROMPT_EXCHANGE_DIR to change, the directory that name is resolved against.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** | — | usable name (C5) | Name for the imported prompt. |
| `basename` | ordered | `string` | **yes** | — | file name only; no `/`, `\`, `..` (C1); must exist in the exchange directory | Source file. |
| `format` | named | `string` | no | `auto` | `auto`, `text`, `json` | `text` = the whole file is the content (source behaviour); `json` = a full five-field record; `auto` detects a `PROMPT EXPORT -format json` document and otherwise treats it as text. **NEW** — closes the source's lossy round trip (Q12). |
| `force` | flag | `bool` | no | `false` | — | Overwrite an existing prompt. **NEW** — the source imported over an existing prompt *silently* (B10); this package refuses without `-force`. |
| `description` | suffix | `string` | no | `Imported from: <basename>` | free text | Description. Source default preserved verbatim; note the GUI stored an empty string instead (Q6), which is not reproduced. |

`createdAt` = now, `lastUsedAt` = unset, for `-format text`. For `-format json` the record's own `createdAt`/`lastUsedAt`/`description` are honoured, which is what makes prompts shareable between machines.

**Pipeline behaviour.** **Both.** As a filter, one chunk = one **basename** to import; the prompt name is then derived from the file's stem (sanitized per C5) unless `name` was given on the command line, enabling `LOG LIST -dir exchange | PROMPT IMPORT` style bulk loads. Emits, per import, the source's four-line block verbatim:

```
Imported system prompt: <name>
Description: <description>
Length: <n> characters

Use 'PROMPT USE <name>' to start using it.
```

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_PROMPT_EXCHANGE_DIR`, `CHATDBG_PROMPT_MAX_CHARS`. Writes nothing.

**Failure modes.** Fewer than two operands and no pipe → `Please specify both a prompt name and a file name: PROMPT IMPORT <name> <basename> [description]` (source wording, path→basename). File absent → `File not found in the exchange directory: <basename>` (source said `File not found: <path>`; changed because the path is now implicit — `PROMPT PATH` shows it). Basename containing a separator or `..` → `Only a file name is accepted; run 'PROMPT PATH' to see the exchange directory.` File not valid UTF-8 → `<basename> is not readable as text.` `-format json` on a file that is not a prompt record → `<basename> is not a prompt document; import it with -format text to take it as raw content.` (The source, given a store `.json` file, cheerfully made the JSON itself the prompt text — Q12.) Existing name without `-force` → `Prompt already exists: <name>. Use -force to replace it.` Over the size ceiling → as `CREATE`. Any read/save failure → `Error importing prompt: <reason>` (source wording preserved).

**Security and audit.** No secret. **Path confinement is a security property here**, not a convenience: the exchange directory is the only place `IMPORT` will read from, so a prompt library command can never be turned into an arbitrary-file-read primitive (the source read any path the process could reach — dossier "Path handling / injection surface"). Overwrites only with `-force` → confirmation required in that case. Imported content is untrusted text that will later be sent to a model as a system instruction; the package does not attempt to sanitize it, but `DOCTOR` flags records whose content is suspiciously large or contains control characters.

**Traceability.** PRD 7.5; source `/prompt import <n> <file_path> [description]` (`Commands/PromptCommand.cs:328-378`) and GUI **Import…** (`UI/SystemPromptsDialog.cs:461-626`), whose non-browsing "Browse…" box (Q11) is replaced by `PROMPT PATH` plus a plain basename.

---

#### 4.3.11 `PROMPT EXPORT` — export a prompt to a file

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Export", "Export a prompt to a file in the exchange directory",
    Prototype = "PROMPT EXPORT <name> [-file <basename>] [-format text|json] [-force]")]
[CommandParameterOrdered("name", "Prompt to export", UsePipe = true)]
[CommandParameterNamed("file", "Destination file name (no directories); defaults to a derived name")]
[CommandParameterNamed("format", "Export shape", DefaultValue = "text",
    AllowedValues = new[] { "text", "json" })]
[CommandFlag("force", "Overwrite an existing destination file")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | **yes** (unless piped) | — | existing prompt | Prompt to export. |
| `file` | named | `string` | no | `chatdbg_prompt_<sanitized name>.txt` (text) / `chatdbg_prompt_<sanitized name>.json` (json) | file name only (C1) | Destination inside the exchange directory. Source's default file name preserved verbatim. |
| `format` | named | `string` | no | `text` | `text`, `json` | `text` writes only the prompt content, exactly as the source did; `json` writes the full five-field record so a round trip is lossless. **NEW** |
| `force` | flag | `bool` | no | `false` | — | Overwrite an existing file. **NEW** — the source overwrote silently (B9). |

**Destination resolution** — this is where the source was worst. The command path defaulted to the user's Documents folder, the GUI path to the home folder without sanitizing the name, and on Linux with no XDG user-dirs configuration the Documents lookup returned the **empty string**, silently writing into the process working directory and reporting a bare relative name (Q13, Q18). This package resolves **one** directory, in this order, and `PROMPT PATH` prints it: `CHATDBG_PROMPT_EXCHANGE_DIR` → the per-user documents directory when non-empty → the per-user home directory. **It never falls back to the working directory**, and it creates the directory if absent.

**Pipeline behaviour.** **Both.** As a filter, one chunk = one prompt name, so `PROMPT LIST -format names | PROMPT EXPORT -format json` backs up the whole library; the per-chunk `-file` default is derived per prompt, and giving an explicit `-file` in a multi-name pipeline is an error rather than a silent last-writer-wins. Output per export: `Exported system prompt to: <full path>` (source wording, now always with a real directory).

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_PROMPT_EXCHANGE_DIR`. Writes nothing.

**Failure modes.** Missing name and no pipe → `Please specify a prompt name: PROMPT EXPORT <name> [-file <basename>]`. Unknown name → `Prompt not found: <name>`. Destination exists without `-force` → `<basename> already exists in the exchange directory; use -force to overwrite.` Destination unwritable / disk full → `Error exporting prompt: <reason>` (source wording). Exchange directory cannot be created → names the directory and the reason; this is the one place the tool tells the user to set `CHATDBG_PROMPT_EXCHANGE_DIR`.

**Security and audit.** No secret. Confined write: a caller cannot direct the write outside the exchange directory (the source could write anywhere the process could reach). Overwrites only with `-force` → confirmation required in that case.

**Traceability.** PRD 7.5; source `/prompt export <n> [file_path]` (`Commands/PromptCommand.cs:286-326`) and GUI **Export…** (`UI/SystemPromptsDialog.cs:628-696`).

---

#### 4.3.12 `PROMPT RESET` — restore the built-in starter set  **NEW**

**Why it earns its place.** The source seeded its four built-ins **only when the store loaded zero prompts, checked at every launch** (R14). Consequences: deleting all prompts silently resurrected them at the next start; a store containing only unreadable files counted as empty and was overwritten (R15); and there was no way to get `code-reviewer` back after deleting it, short of emptying the whole library. Making seeding an explicit, idempotent tool removes all three surprises.

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Reset", "Restore the built-in starter prompts",
    Prototype = "PROMPT RESET [<name>] [-force] [-list]")]
[CommandParameterOrdered("name", "Restore only this built-in", IsRequired = false)]
[CommandFlag("force", "Overwrite built-ins that have been modified")]
[CommandFlag("list", "Show what would be restored, and change nothing")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | no | *(all four)* | `default`, `code-reviewer`, `algorithm-helper`, `security-expert` | Which built-in to restore. **NEW** |
| `force` | flag | `bool` | no | `false` | — | Overwrite a built-in whose content has been changed. **NEW** |
| `list` | flag | `bool` | no | `false` | — | Dry run. **NEW** |

**The built-in set, verbatim** (source `Services/SystemPromptService.cs:168-198`; written in this fixed order, though listings re-sort — R13):

| Name | Description | Content |
|---|---|---|
| `default` | `Default system prompt for general debugging assistance` | `You are ChatDBG, a helpful debugging assistant. Help users analyze code, debug issues, and understand programming concepts.` |
| `code-reviewer` | `System prompt for code review assistance` | `You are ChatDBG in code review mode. Analyze code for bugs, security issues, performance problems, and maintainability concerns. Provide specific, actionable feedback with examples of how to improve the code.` |
| `algorithm-helper` | `System prompt for algorithm assistance` | `You are ChatDBG in algorithm mode. Help users understand, design, and optimize algorithms. Provide step-by-step explanations, time and space complexity analysis, and pseudocode when helpful.` |
| `security-expert` | `System prompt for security-focused assistance` | `You are ChatDBG in security expert mode. Help users identify and fix security vulnerabilities in their code. Focus on common issues like injection attacks, authentication problems, authorization flaws, data exposure, and insecure dependencies.` |

Seeded records get `createdAt` = the UTC instant of seeding and `lastUsedAt` unset (R17).

**Automatic seeding is retained but narrowed.** The store still seeds on first use when the directory is **absent or contains no files at all** — that is what makes a fresh install useful (acceptance criteria 1 and 2). It no longer seeds when the directory contains files that merely failed to parse (fixing R15); that case now produces the `DOCTOR` advisory instead.

**Pipeline behaviour.** Source only; **overrides `Main`** to emit one chunk per restored (or would-be-restored) prompt. Declines piped input explanatorily.

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_PROMPT_SEED`. Writes nothing.

**Failure modes.** Unknown built-in name → `'<name>' is not a built-in prompt. The built-ins are: default, code-reviewer, algorithm-helper, security-expert.` Built-in exists and differs, without `-force` → per-prompt chunk `'<name>' exists and has been modified; use -force to restore the original.` (a **success** chunk, not a failure — it is a report, not an error). Store read-only → one failure chunk naming the path.

**Security and audit.** No secret. **`-force` is destructive** (it discards user edits to a built-in) → confirmation required, and the previous record is copied to `<store>/.backup/` first.

**Traceability.** **NEW** as a tool; the seeding behaviour it exposes descends from `Services/SystemPromptService.cs:157-204`. PRD 7.5.

---

#### 4.3.13 `PROMPT PATH` — report the library's locations  **NEW**

**Why it earns its place.** The source *tried* to print the store directory in `/prompt list` and printed a .NET type name instead, because the "get directory" accessor existed on the concrete store but not on the abstraction the command held (Q2). Users had no supported way to find their prompts. With paths no longer expressible as parameters (C1), knowing the two directories is a prerequisite for `IMPORT` and `EXPORT`, so it becomes a first-class tool.

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Path", "Show where the prompt library and exchange directory are",
    Prototype = "PROMPT PATH [-format text|json] [-which store|exchange|backup|all]")]
[CommandParameterNamed("format", "Output shape", DefaultValue = "text",
    AllowedValues = new[] { "text", "json" })]
[CommandParameterNamed("which", "Which location to report", DefaultValue = "all",
    AllowedValues = new[] { "store", "exchange", "backup", "all" })]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text`, `json` | Output shape. **NEW** |
| `which` | named | `string` | no | `all` | `store`, `exchange`, `backup`, `all` | Which location. `-which store` emits the bare path, for piping. **NEW** |

Reports, per location: the absolute path, whether it exists, whether it is writable, how it was resolved (environment override vs. platform default), and — for the store — the number of records and unreadable files.

**Pipeline behaviour.** Source only (one chunk for a single `-which`, several when `all`). Declines piped input.

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_PROMPT_EXCHANGE_DIR`. Writes nothing.

**Failure modes.** None that fail: a missing or unwritable directory is *reported*, not thrown. This is the tool a user runs when something else failed, so it must work when everything else does not.

**Security and audit.** Emits absolute filesystem paths containing the OS user name — worth knowing if audit output is shipped off-box, but not a secret. Non-destructive.

**Traceability.** **NEW**; corrects source `Commands/PromptCommand.cs:102` (Q2) and supersedes the missing sixth capability on `Services/ISystemPromptService.cs`. PRD 7.5, with a nod to 7.11.

---

#### 4.3.14 `PROMPT DOCTOR` — diagnose the library  **NEW**

**Why it earns its place.** The dossier records seven distinct silent-corruption modes: unparseable files skipped with a message that the GUI shell swallowed (R9, Q26), files whose JSON `name` disagrees with their file name and are therefore unfetchable (R8), lossy sanitization collisions (R5), stores made non-portable by per-OS forbidden-character sets (Q21), a settings file pointing at a prompt that no longer exists (R21), a `null` JSON document skipped with no message at all (R10), and truncated files left by a crash mid-write (Q16). Every one of them presents to the user as "my prompt disappeared". One tool that names them is worth more than any amount of defensive code elsewhere.

```csharp
[CommandRoot("PROMPT", "System prompt library")]
[CommandRegister("Doctor", "Check the prompt library for problems",
    Prototype = "PROMPT DOCTOR [-format text|csv|json] [-fix]", Alias = "CHECK")]
[CommandParameterNamed("format", "Output shape", DefaultValue = "text",
    AllowedValues = new[] { "text", "csv", "json" })]
[CommandFlag("fix", "Apply the safe repairs (rename mismatched files, quarantine unreadable ones)")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text`, `csv`, `json` | Output shape; `csv` sets `ResultFormat.CSV` for a log sink. **NEW** |
| `fix` | flag | `bool` | no | `false` | — | Apply only the reversible repairs. **NEW** |

**Checks performed**, one chunk each: unreadable/unparseable files (with the parse error); `null` documents; file-name ↔ record-name mismatches; sanitization collisions; names that would be unreachable on another operating system (C5 portability warning); records with empty name or empty content; content over the size ceiling or containing control characters; a `CHATDBG_SYSTEM_PROMPT` that names no existing prompt; missing built-ins; stale lock files; backups available in `<store>/.backup/`; store and exchange directory writability.

**`-fix` is deliberately narrow**: it renames a mismatched file to match its record name (never the reverse — the record is authoritative), moves an unreadable file to `<store>/.quarantine/` so listing stops tripping over it, and removes lock files older than an hour. It **never** deletes a record, never edits content, and never changes the active selection.

**Pipeline behaviour.** Source only; **overrides `Main`** to emit one chunk per finding, terminating with a summary chunk. A clean library emits exactly one chunk: `No problems found in <n> prompts.` Declines piped input.

**Environment interaction.** Reads `CHATDBG_PROMPT_DIR`, `CHATDBG_PROMPT_EXCHANGE_DIR`, `CHATDBG_SYSTEM_PROMPT`, `CHATDBG_PROMPT_MAX_CHARS`. Writes nothing.

**Failure modes.** Store directory unreadable → a single failure chunk naming the path and the reason (this is the one condition `DOCTOR` cannot diagnose around). `-fix` unable to write → each repair reports its own failure and the rest continue.

**Security and audit.** No secret. `-fix` moves files but destroys nothing → no confirmation required for the moves; it is nonetheless recorded in the audit event like any other command.

**Traceability.** **NEW**; addresses source rules R5, R8, R9, R10, R15, R21 and quirks Q16, Q21, Q26. PRD 7.5, feeding 7.11.

---

### 4.4 Environment variables

| Key | Read by | Written by | Default | Notes |
|---|---|---|---|---|
| `CHATDBG_PROMPT_DIR` | all | none | per-user local-app-data `/ChatDbg/system_prompts` | **NEW** — the source had a constructor override used only by tests (dossier B0), so there was no supported way to relocate a library. Host-set only (a path cannot survive the argument tokenizer, C1). |
| `CHATDBG_PROMPT_EXCHANGE_DIR` | `IMPORT`, `EXPORT`, `CREATE -file`, `EDIT -file`, `PATH`, `DOCTOR` | none | documents dir, else home dir; **never** the working directory | **NEW** — replaces the source's three inconsistent defaults (Q13, Q18). |
| `CHATDBG_SYSTEM_PROMPT` | `STATUS`, `LIST`, `SHOW`, `USE`, `EDIT`, `DELETE`, `RENAME`, `DOCTOR` | `USE`, `RENAME` (global; needs `modifiesEnvironment: true`) | `default` | The active prompt **name**. Same default as the source's `systemPromptName` setting (R18). Persisted by ChatDbg.Tools.Settings, not here. |
| `CHATDBG_SYSTEM_PROMPT_TEXT` | `STATUS`, providers | `USE`, `EDIT` (when editing the active prompt) | the built-in default text (R20) | **Runtime only — must never be persisted** (R19). |
| `CHATDBG_PROMPT_SEED` | store bootstrap, `RESET` | none | `true` | `false` disables automatic seeding of the built-ins entirely. **NEW** |
| `CHATDBG_PROMPT_MAX_CHARS` | `CREATE`, `EDIT`, `IMPORT`, `DOCTOR` | none | `1000000` | **NEW** — the source had no length bound anywhere, and the interactive editor could grow without limit (Q4). |
| `CHATDBG_PROMPT_ALLOW_EDITOR` | `EDIT` | none | `true` | **NEW** — set `false` in a restricted host to forbid process spawning. |
| `VISUAL`, `EDITOR` | `EDIT -editor` only | none | `notepad` (Windows), else `nano` then `vi` | **NEW**; standard POSIX convention. |

Every read uses `storeDefault: false` so that reading configuration never marks the environment changed (ref-command §7.1). No tool in this package reads any `CHATDBG_*` credential variable, and none reads the process environment directly — everything goes through `IEnvironmentContext`, which is what makes the package testable (§4.16).

---

### 4.5 Pipeline compositions

**1. Back up the whole library, losslessly.**

```
PROMPT LIST -format names | PROMPT EXPORT -format json
```

`LIST` emits one chunk per prompt name; `EXPORT` consumes each as its `name` operand (`UsePipe = true`) and writes `chatdbg_prompt_<name>.json` into the exchange directory, one confirmation line per prompt. Unlike the source's text export, the JSON form carries description, `createdAt` and `lastUsedAt`, so re-importing restores the library rather than four undated blanks (Q12).

**2. Prune the prompts nobody ever selected.**

```
PROMPT LIST -unused -format names | PROMPT DELETE -yes
```

The user sees one `Deleted system prompt: <name>` per removed prompt. The active prompt cannot be caught by this: it has a `lastUsedAt` (`USE` stamps it) so `-unused` excludes it, and even if it did not, `DELETE`'s active-prompt guard refuses without `-force`. Each deleted record is copied to `<store>/.backup/` first.

**3. Cross-package: budget a persona against the model's context window.**  *(into ChatDbg.Tools.TokenInspection, PRD 7.10)*

```
PROMPT SHOW security-expert -content-only | TOKEN COUNT -model gpt-4
```

`SHOW -content-only` emits the prompt text and nothing else; `TOKEN COUNT` reports how many tokens every request will spend before the user has typed a word. This is the composition the source could not express at all — its `/tokenize` took inline text, which the argument tokenizer would have mangled anyway.

**4. Cross-package: switch persona and persist the choice.**  *(into ChatDbg.Tools.Settings, PRD 7.2)*

```
PROMPT USE code-reviewer | SET SAVE
```

`USE` publishes `CHATDBG_SYSTEM_PROMPT` and `CHATDBG_SYSTEM_PROMPT_TEXT` to the global environment and emits `Now using system prompt: code-reviewer`; the last stage's `SET SAVE` writes `systemPromptName` into the settings document. Note the framework rule this depends on: in a pipeline only the **last** stage's `ModifiesEnvironment` promotes its bucket to globals (ref-command §7.3c), so `SET SAVE` must be the environment-modifying stage — which it is, being the port of the source's `SET`.

**5. Cross-package: record a library health check.**  *(into ChatDbg.Tools.DiagnosticLogging, PRD 7.11)*

```
PROMPT DOCTOR -format csv | LOG APPEND -channel prompts
```

One chunk per finding, tagged `ResultFormat.CSV`, appended as rows to the diagnostic log. The source wrote its "could not read this file" notices to raw stdout, where the full-screen shell rendered them invisible (R9); this composition is the replacement.

**6. Fork a persona and edit it in one flow.**

```
PROMPT COPY security-expert appsec-team | PROMPT EDIT -editor
```

`COPY` emits the new name; `EDIT` consumes it and opens `$EDITOR` on the content. In a host with `CHATDBG_PROMPT_ALLOW_EDITOR=false` the second stage fails with one clear message and the copy still exists — degradation without loss.

---

### 4.6 Design notes for the architect

**What state this package holds.** Exactly one thing: the prompt store on disk, plus a derived in-memory index (name → file path → record) that is invalidated on every mutation and on a directory-timestamp change. The index is the one place this package deviates from the source's "re-read everything, twice, every time" model (Q3): the source's *observable* property — that an external edit to a prompt file is picked up immediately — is preserved by stamping the index with the directory's last-write time and re-reading when it moves. Nothing else is cached.

**What this package must not hold.** It must not hold the settings document, the active-prompt *persistence*, an `HttpClient`, a model handle, a chat history, or a reference to the host shell. In particular it must not hold the active prompt as private mutable state: the active name lives in the environment where every package can read it, and the active text is derived. The temptation — a `CurrentPrompt` property on a service — is what produced the source's worst defect, where the GUI's command objects mutated an orphaned settings instance and every `/prompt use` silently overwrote the user's provider, model, temperature and endpoint with construction-time defaults (Q8, Q20).

**Sub-commands, not a monolith.** The source's `PromptCommand` was one 380-line class with a `switch` on `args[0]`, which is why its usage line was wrong for `import` (Q22) and why five subcommands had near-identical but subtly different name-missing messages. Here each verb is its own `AbstractCommand` under `[CommandRoot("PROMPT", …)]`, so the framework generates help, validates arity, enforces allow-lists and routes `PROMPT LIST --HELP` without a line of dispatch code. Two framework consequences must be designed for, not discovered: a root invoked bare throws `InvalidOperationException` listing its sub-commands, which is why bare `/prompt` becomes `PROMPT STATUS`; and `AbstractCommand.RootCommand` throws when `[CommandRoot]` is absent — harmless here because every class carries it, but a reason never to make a tool in this package "top-level just this once".

**One chunk versus many.** `AbstractCommand`'s non-piped path emits exactly one chunk. `LIST`, `RESET` and `DOCTOR` are inherently multi-record, so they override `Main` and yield per record; everything else stays on the template method. The rule for reviewers: if a tool's output is a list, it overrides `Main`, and if it does not, its output is a paragraph. `CREATE` and `EDIT` invert this for their piped path — they consume many chunks and emit one, using `OnStartPipe`/`OnEndPipe` to buffer and flush, returning empty successes per chunk so nothing is echoed mid-stream.

**Testability.** Three seams and no more: `IPromptStore` (fetch, list, save, delete, stamp-last-used, **and the store directory** — the sixth capability the source omitted from its abstraction, which is precisely why its list output printed a type name, Q2); `IClock` for `UtcNow`, so timestamp assertions are deterministic; and `IIoContext`/`IEnvironmentContext`, which the framework already supplies as parameters. `MemoryIoContext` drives every tool in a hermetic suite with no filesystem at all; a second suite runs the real store against a temporary directory. The source's test coverage here was six assertions that pinned neither the seeding count, nor any built-in's text, nor any ordering, nor any error string — this package's acceptance suite pins all four, including the culture-sensitive ordering case (`a, a_b, a-b, ab, B`) that a naive ordinal implementation gets wrong. Split the suites the way Cupcake splits its: hermetic tests at the repo root, no network in either (this package has none).

**Capability unavailable on the current backend.** This package has no backend, so "unavailable" here means an OS or host restriction, and the rule is uniform: **report the restriction and continue with everything else.** A read-only store leaves every read tool fully functional and gives each write tool one specific message naming the path. A host that did not grant `modifiesEnvironment` leaves `USE` able to select, stamp and record — it just says so instead of pretending. A host with editors disabled leaves `EDIT` with four other content sources. Absent `$EDITOR`, the platform fallback chain is tried before failing. The only unconditional hard failure is a store directory that cannot be created at all — and even that is now a tool's failure chunk rather than the source's uncaught constructor exception that prevented the whole application from starting.

**Cross-platform, explicitly.** The source inherited five behaviours from its host OS and got a different product on each (dossier "Platform coupling"). All five are pinned here: file-name sanitization uses one fixed set on every platform (C5); name matching is ordinal on every platform (C4); collation is a parameter, not an ambient (C6); the export directory never degrades to the working directory; and the store's default location follows each platform's own convention (`%LocalAppData%`, `$XDG_DATA_HOME`, `~/Library/Application Support`) while the *layout inside it* is identical. A prompt library copied from Linux to Windows must open, list and select identically — that is an acceptance criterion, not an aspiration.

**Where it degrades rather than fails.** Unreadable file → skipped, counted, reported by `DOCTOR`, listing still succeeds. Missing active prompt → falls back to the built-in default text with one advisory line, never blocks a chat turn. Empty store → seeded, or if seeding is disabled, an empty list and a hint. Missing exchange directory → created. Lock contention → one retry with backoff, then a message naming the lock holder's age. Store directory vanishes mid-session → the next command reports it; the package does not cache its way into pretending otherwise.

**Considered and deliberately excluded.** `PROMPT DIFF` (compare two prompts, or a prompt against an exchange file) is genuinely useful but belongs to a text-utility package, not here; if it is ever wanted, it should live beside `REGIF` as a general filter, not as a fourteenth prompt verb. Prompt *versioning* (a full history per prompt) was rejected in favour of the single-generation `.backup` copy: it would double the store's shape, and the exchange directory plus a real version-control system covers the serious case.

---

### 4.7 Security and audit summary

| Concern | Position |
|---|---|
| Secrets | **None** in this package. No parameter and no output carries a credential, and no tool reads a credential variable. Prompt content is user-authored clear text and is stored unencrypted, exactly as in the source. |
| Audit masking | `AuditEvent.Parameters` records every argument token, including an inline `-text` or a description. The framework's masking only rewrites `-name=value` tokens and the tokenizer strips `=` anyway, so parameter masking is effectively non-functional (ref-command §11.4). The mitigation is structural: the fidelity-preserving content channels are the pipe, `-file` and the interactive editor — **none of which passes through the parameter array**. Document inline `-text` as convenience-only. |
| Environment-change audit | `USE` writes `CHATDBG_SYSTEM_PROMPT_TEXT`, whose value is a whole prompt body, and `LogEnvironmentChange` records old and new values. Variable-name-based redaction *does* work on that path; a host that wants terse records adds that key to `RedactedParameterNames`. |
| Path safety | No tool accepts a path. Store and exchange directories are resolved by the package; `IMPORT`/`EXPORT`/`-file` accept a bare file name and reject separators and `..`. The store's own file names are always `<store>/<sanitized>.json`, which cannot escape the directory. This is stricter than the source, which read and wrote any path the process could reach. |
| Destructive actions requiring confirmation | `DELETE` (always, unless `-yes`); `EDIT` (overwrites content — one backup generation, no prompt); `CREATE -force`, `IMPORT -force`, `EXPORT -force`, `RENAME -force`, `COPY -force`, `RESET -force` (each overwrites something that exists — confirm unless `-yes`). Confirmation is `PromptForCommand` defaulting to **No**; in a pipeline, where prompting is meaningless, the corresponding `-yes`/`-force` must be explicit or the operation is refused. |
| Untrusted content | An imported prompt becomes a system instruction sent to a model. The package does not attempt to filter instruction-injection attempts — that is not a check it can make correctly — but `DOCTOR` reports oversized content and control characters, and `SHOW` always displays the full text before `USE` applies it. |
| Loader posture | No dynamic assemblies, no reflection-emit, no native code, no network. Ships as `packages/ChatDbg.Tools.SystemPromptLibrary/<version>/bin/` for `AddPackageDirectory` + `LoadCommands`, and is hash-allowlisted in the host's integrity store (`learningMode: false`). The only capability worth a policy decision is `EDIT -editor`'s process spawn, which is gated by `CHATDBG_PROMPT_ALLOW_EDITOR`. |

---

## 5. ChatDbg.Tools.ModelBackends — Model Backends

### Purpose and boundary

**What this package owns.** Everything about *which model answers, on what hardware, and whether it can do what you are about to ask of it.* Concretely:

1. **The backend registry** — enumerating the model backends the host has loaded (hosted Azure OpenAI, Amazon Bedrock, a locally-run GGUF engine, plus any backend contributed later by a dropped-in package), their display names, their registry keys, and their readiness.
2. **Selection** — switching the active backend, and choosing which model/deployment/foundation-model/weight-file that backend should use.
3. **Discovery** — listing the models a backend can actually serve, rather than making the user guess a free-text identifier.
4. **Verification** — probing connectivity, credential resolution and entitlement *before* a chat turn burns a conversation on a misconfiguration.
5. **Capability reporting** — answering, per backend and per model, whether token log-probabilities, streaming, tool calling and prompt-side probabilities are available, and with what ceilings.
6. **Local engine lifecycle** — loading a weight file into memory with its hardware-acceleration settings, reporting what is resident, retuning the acceleration knobs, and unloading.

**What this package explicitly does NOT own.**

| Not owned | Owner | Why the split |
|---|---|---|
| The settings store, the `SET`-style key/value surface, persistence, and generation parameters (`temperature`, `maxTokens`, `logProbabilitiesTopK`, system-prompt name) | **ChatDbg.Tools.Settings** (root `SET`, PRD 7.2) | MODEL *reads* these and *writes three of them through* that package's environment keys; it never opens the settings document. The source's single 464-line `SetCommand` conflated all of this — the rebuild does not. |
| Secret acquisition, storage, the OS keystore, migration, and the "where did this credential come from" label | **ChatDbg.Tools.Credentials** (root `CRED`, PRD 7.3) | MODEL consumes a *resolved* secret and a *source label*. No tool in this package accepts, prints, logs or returns a secret value. See **Security and audit** on every tool. |
| Chat turns, conversation history, injection/pop/import/export | **ChatDbg.Tools.History** (root `CHAT`, PRD 7.4) | MODEL never sends a conversation. `MODEL TEST -deep` sends a synthetic one-token probe that is never appended to history. |
| System prompt bodies and their catalogue | **ChatDbg.Tools.Prompts** (root `PROMPT`, PRD 7.5) | The local engine binds a system prompt at load time; MODEL asks the prompt package for the resolved body, it does not resolve names. |
| Token log-probability maths, the top-K analysis surface, tokenization and attribution | **ChatDbg.Tools.Tokens** (root `TOKEN`, PRD 7.9/7.10) | MODEL reports *whether* a backend can produce log-probabilities. What is done with them is that package's business. |
| Diagnostic log capture, buffering, daily files and log export | **ChatDbg.Tools.Diagnostics** (root `DIAG`, PRD 7.11) | The local engine's native log stream is *emitted* here and *owned* there. `MODEL LOAD` writes lines into the diagnostics sink; it has no `export-logs` verb. |
| Colour, heat-maps, grids, tables, theming | **ChatDbg.Tools.Render** / host `IIoContext` (PRD 7.12) | Every tool here emits plain rows plus a declared `ResultFormat`; the host decides how they look. |

---

### Package manifest

The package ships as **two assemblies under one root command**, because the framework merges sub-commands into an existing root description (`CommandRegistry.AddCommand(ICommandDescription)` merges sub-command dictionaries when the root already exists; `CommandParameters.CreatePackageDescription` synthesises or merges the root). This lets a hardened host load the cloud half and refuse the native half.

| Property | `ChatDbg.Tools.ModelBackends` (primary) | `ChatDbg.Tools.ModelBackends.Local` (satellite) |
|---|---|---|
| Assembly / package id | `ChatDbg.Tools.ModelBackends` | `ChatDbg.Tools.ModelBackends.Local` |
| Root command | `MODEL` | `MODEL` (merged into the same root) |
| Tools contributed | `LIST`, `SHOW`, `USE`, `SELECT`, `CATALOG`, `CAPS`, `TEST` | `LOAD`, `UNLOAD`, `STATUS`, `TUNE` |
| Contract assemblies targeted | `Xcaciv.Command.Interface` **3.3.4** + `Xcaciv.Command.Core` **3.3.4** (no reference to `Xcaciv.Command`, none to the host) | same |
| Target framework | `net10.0`, `ImplicitUsings`, `Nullable` enabled, no `AllowUnsafeBlocks` | `net10.0`, `ImplicitUsings`, `Nullable` enabled |
| Elevated trust required | **No.** Managed code only: HTTP/TLS, JSON, no reflection-emit, no P/Invoke | **Yes, effectively.** Loads a native shared library (`llama.dll` / `libllama.so` / `libllama.dylib`) via the engine binding; uses `dynamic`/reflection on the engine's context type |
| Network reach | Outbound HTTPS to the configured Azure OpenAI endpoint host and to the AWS Bedrock regional endpoints only. No other host. No inbound | **None** |
| Filesystem reach | Read-only: nothing outside the catalog cache file it owns (`<app-data>/ChatDbg/catalog.json`) | Read: the weight file and its directory; the model-search roots. Write: none of its own (the log sink belongs to `DIAG`) |
| OS keystore | **Indirect only** — through `ChatDbg.Tools.Credentials`. This package never calls a credential API | None |
| Native libraries | None | The inference backend native set for the running RID (CPU baseline; CUDA / Vulkan / Metal when present) |
| Safe to load in a restricted host | **Yes.** Loads cleanly under `AssemblySecurityPolicy.Strict` with `DisallowDynamicAssemblies = true`, `EnforceBasePathRestriction = true`, and an integrity allow-list | **No.** Preflight under `Strict` will reject the engine binding's dependencies, and a fault inside the native layer is an uncatchable process kill. Load it only in a host that has accepted in-process native code, or run it out of process (see **Design notes**) |
| Audit posture | Every tool is registered so that its `AuditEvent.Parameters` can be logged verbatim: **no tool in this package ever accepts a secret as a parameter** | same |
| Environment-modifying registration | `USE`, `SELECT` → `modifiesEnvironment: true` | `TUNE` → `modifiesEnvironment: true`; `LOAD`, `UNLOAD`, `STATUS` → `false` |

> **Framework caveat carried into the design.** `ICommandController.AddCommand(packageKey, ICommandDelegate, bool)` keeps only the instance's `Type`; a *fresh* instance is constructed per execution and `CommandExecutor` never disposes it. No tool here may hold live state on the instance. The resident local model, the HTTP transports and the catalog cache all live in a package-internal process singleton reached through the host's `IServiceProvider` (or, in a DI-free host, a static accessor inside the satellite assembly). See **Design notes — what state this package holds**.

---

### Tool catalog

Registration shape shared by every tool in the package:

```csharp
[CommandRoot("Model", "Model backends: registry, selection, capabilities and local engine lifecycle")]
[CommandRegister("<Verb>", "<one line>", Prototype = "MODEL <VERB> …", Version = "1.0.0")]
```

`CommandRootAttribute.Command` and `CommandRegisterAttribute.Command` are normalised to **UPPERCASE** by `NamesValidator`; parameter `Name`s are normalised to **lowercase**. Invocation is therefore `model list`, `MODEL LIST`, `Model List` — all equivalent. The parameter dictionary handed to `HandleExecution` is `StringComparer.OrdinalIgnoreCase`.

---

#### 5.1 `MODEL LIST` — enumerate the registered backends

| | |
|---|---|
| Command | `LIST` |
| Root command | `MODEL` |
| Description | List every registered model backend with its key, display name and readiness |
| Prototype | `MODEL LIST [-only all\|configured\|unconfigured\|active\|unavailable] [-format text\|keys\|csv\|json] [-v]` |

```csharp
[CommandRoot("Model", "Model backends: registry, selection, capabilities and local engine lifecycle")]
[CommandRegister("List", "List every registered model backend with its key, display name and readiness",
    Prototype = "MODEL LIST [-only all|configured|unconfigured|active|unavailable] [-format text|keys|csv|json] [-v]")]
[CommandParameterNamed("only", "Which backends to include",
    AllowedValues = new[] { "all", "configured", "unconfigured", "active", "unavailable" })]
[CommandParameterNamed("format", "Output shape",
    AllowedValues = new[] { "text", "keys", "csv", "json" })]
[CommandFlag("verbose", "Include endpoint/region/model-path detail on each row", ShortAlias = "v")]
[CommandHelpRemarks("Emits ONE CHUNK PER BACKEND, so it composes directly into MODEL TEST, MODEL CAPS and MODEL USE.")]
[CommandHelpRemarks("An empty registry is not an error: the tool reports it and names the recovery command.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `only` | named | `string` | no | `all` (first allowed value auto-becomes the default) | `all`, `configured`, `unconfigured`, `active`, `unavailable` | Which backends to include |
| `format` | named | `string` | no | `text` | `text`, `keys`, `csv`, `json` | Output shape. `keys` emits the bare registry key per chunk — the pipe-friendly form |
| `verbose` / `-v` | flag | `bool` (always) | no | `false` | — | Append endpoint host, region or weight-file path to each row (never a secret) |

**Pipeline behaviour.** Produces piped output; does **not** accept piped input (a piped invocation returns an explanatory chunk rather than throwing, per the framework convention). It emits **one chunk per backend**, which `AbstractCommand`'s non-piped path cannot do — the template method yields exactly one chunk when `HasPipedInput` is false. `LIST` therefore **overrides `Main`** (legal; the method is not sealed) and yields a `CommandResult<string>.Success(row, OutputFormat)` per backend, plus a final zero-length success (silently dropped by the host) when the registry is empty and a message chunk has already been emitted. `OutputFormat` is set from `-format`: `ResultFormat.General` for `text`/`keys`, `ResultFormat.CSV` for `csv`, `ResultFormat.JSON` for `json`, so downstream stages can read `pipedChunk.OutputFormat` and the host renderer can pick a table style. (The framework does not act on `OutputFormat` itself — encoding is the host's job — so `-format` also genuinely changes the text.)

**Environment interaction.** Reads (with `storeDefault: false`): `CHATDBG_PROVIDER` (to mark the active row). Writes nothing. Declares `GetDefaultEnvironment()` → `{ "FORMAT", "text" }` so a host can pin a house default; remember the host stores that as `LIST_FORMAT` and the tool reads the prefixed key. Does **not** need environment-modifying permission.

**Failure modes.**

| Condition | Behaviour | User sees |
|---|---|---|
| No backends registered | Not an error. One informational chunk, then normal completion | `No model backends are loaded. Drop a backend package under .\packages\<name>\bin, then restart, or run 'PACKAGE SEARCH model' to find one.` |
| A backend's readiness probe throws | The row is still emitted, marked `error` with the exception's message truncated to 200 chars | `bedrock   Amazon Bedrock        error: <short reason>` |
| A backend has no native runtime for this RID (local engine on an unsupported RID) | Row marked `unavailable (<reason>)`; never an exception | `llama     Local LLM (GGUF)      unavailable (no native runtime for linux-musl-arm64)` |
| `-only` given an unlisted value | Framework rejects at parse time with `ArgumentException` before `HandleExecution`; the host reduces it to `Error executing LIST (see trace for more info)` and traces the specific message | Generic failure line; the specific reason is in the trace. Mitigated by listing the allowed values in the help text and in `Prototype` |
| Invoked with piped input | Explanatory chunk, no throw | `MODEL LIST does not consume piped input. Did you mean 'MODEL CAPS' or 'MODEL TEST'?` |

**Security and audit.** No parameter or output carries a secret; `-v` prints endpoint *hosts* and file *paths*, never keys, and never the credential value or the settings-file secret slot. Non-destructive; no confirmation. Safe to audit-log verbatim.

**Traceability.** PRD **7.6** (AI Provider Abstraction) with reads from **7.2**. Descends from the shells' hard-wired provider registry (`ChatShell.InitializeAIServices` → `Dictionary<string, IAIService>` keyed `azure`/`bedrock`/`llama`) and from the start-up configuration self-check block that printed `Warning: Unknown AI provider: …` / `Warning: <name> service is not configured.`. **Improvement over source (deliberate):** the source registry was fixed at compile time in two places that disagreed (one dead shell class registered only two of three backends); this registry is whatever the loader found, and there is exactly one of it.

---

#### 5.2 `MODEL SHOW` — describe one backend's effective configuration

| | |
|---|---|
| Command | `SHOW` |
| Root command | `MODEL` |
| Description | Show the active (or named) backend's configuration, effective request parameters and credential source |
| Prototype | `MODEL SHOW [<backend>] [-format text\|csv\|json] [-effective]` |

```csharp
[CommandRegister("Show", "Show a backend's configuration, effective request parameters and credential source",
    Prototype = "MODEL SHOW [<backend>] [-format text|csv|json] [-effective]")]
[CommandParameterOrdered("backend", "Registry key of the backend (default: the active one)",
    IsRequired = false, UsePipe = true)]
[CommandParameterNamed("format", "Output shape", AllowedValues = new[] { "text", "csv", "json" })]
[CommandFlag("effective", "Show the values that would actually go on the wire after backend-specific clamping")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `backend` | ordered | `string` | **no** (`IsRequired = false` — ordered parameters are required by default, so this is an explicit opt-out) | the value of `CHATDBG_PROVIDER`, else `azure` | any registered key, case-insensitive | Which backend to describe |
| `format` | named | `string` | no | `text` | `text`, `csv`, `json` | Output shape |
| `effective` | flag | `bool` | no | `false` | — | Apply backend/model-specific clamping before printing (e.g. Anthropic-on-Bedrock caps temperature at 1.0 even though the product accepts 0.0–2.0) |

Fields reported (all read-only, all from the settings package):

| Field | Source default | Range enforced by `SET` | Note |
|---|---|---|---|
| provider | `azure` | `azure` \| `bedrock` \| `llama` + any loaded key | lookup is case-insensitive (the source's console shell was case-sensitive, the GUI was not — unified here) |
| model id / deployment / weight path | `gpt-4` | free text | for `bedrock` the id doubles as the foundation-model id; for `llama` it is a filesystem path |
| azure endpoint | `null` | unvalidated free text | printed host-only unless `-effective` |
| azure api version | `2023-12-01-preview` | — | source hard-coded this literal; here it is a settings key with that value as default |
| aws region | `us-east-1` | unvalidated free text | |
| temperature | `0.7` | `0.0`–`2.0` inclusive | `-effective` shows `1.0` for `anthropic.*` on Bedrock and flags the clamp |
| max tokens | `1000` | `1`–`8192` inclusive | source sent this to Azure **only** when log-probabilities were on; the rebuild always sends it and says so |
| log-probabilities enabled | `false` | `true`/`false` | |
| log-probabilities top-K | `5` | `1`–`20` inclusive | this value also constrains the *local sampler*, not only reporting — see `MODEL CAPS` |
| llama context size | `4096` | `512`–`32768` inclusive | |
| llama gpu layers | `0` (CPU only) | `0`–`100` inclusive | |
| llama gpu device | *(empty)* | free text, e.g. `0` or `0,1` | |
| llama threads | `0` (= host default) | `0`–`64` inclusive | |
| llama batch size | `512` | `1`–`2048` inclusive | |
| credential source | `not set` | — | one of `environment variable (<NAME>)`, `OS keystore`, `settings file (deprecated)`, `not set`, `unavailable on this platform` |

**Pipeline behaviour.** Both. As a source it emits one chunk (one record). As a sink it accepts piped input where **one chunk is one backend key**, describing each in turn — so `MODEL LIST -format keys | MODEL SHOW` prints a full configuration dump. `backend` carries `UsePipe = true`, so when piped the framework does not demand it on the command line. `OutputFormat` follows `-format`.

**Environment interaction.** Reads every key in the table above from the shell environment context with `storeDefault: false` (a plain read must not mark the environment changed). Reads the OS **process** environment only indirectly, by asking the credentials package for the *source label* — never the value. Writes nothing; not environment-modifying.

**Failure modes.** Unknown backend key → a `Failure` chunk `Unknown backend '<k>'. Registered: azure, bedrock, llama.` (not an exception — failures are data). Backend registered but unavailable on this platform → a normal success row with `status: unavailable (<reason>)`. A piped failure chunk is forwarded verbatim by `AbstractCommand.Main` before `HandlePipedChunk` is ever called, so upstream errors pass through unaltered and are not misreported as configuration problems.

**Security and audit.** Prints the credential **source label** only. Endpoint is shown host-only (`example.openai.azure.com`) unless `-effective`, which shows the full composed request URL *with the deployment segment* but still no key. Never prints the deprecated settings-file secret slots even when populated. Non-destructive.

**Traceability.** PRD **7.6**, **7.7**, **7.8**, reading **7.2**/**7.3**. Descends from `/set` with no arguments (the settings listing, which printed `- AWS Access Key: ***set*** [environment variable (…)]`), from the console shell's start-up banner (Provider / Model / System Prompt / settings-file path) and from its `LLama Configuration:` block.

---

#### 5.3 `MODEL USE` — switch the active backend

| | |
|---|---|
| Command | `USE` |
| Root command | `MODEL` |
| Description | Make a registered backend the active one and report its readiness |
| Prototype | `MODEL USE <backend> [-no-verify] [-yes]` |

```csharp
[CommandRegister("Use", "Make a registered backend the active one and report its readiness",
    Prototype = "MODEL USE <backend> [-no-verify] [-yes]")]
[CommandParameterOrdered("backend", "Registry key of the backend to activate", UsePipe = true)]
[CommandFlag("no-verify", "Switch without running the readiness check")]
[CommandFlag("yes", "Do not prompt when switching away from a backend with a resident local model")]
[CommandHelpRemarks("Registered with modifiesEnvironment: true — this is one of only three tools here that write a global setting.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `backend` | ordered | `string` | **yes** (ordered default) | — | any registered key; built-ins `azure`, `bedrock`, `llama`; matched case-insensitively | Backend to activate |
| `no-verify` | flag | `bool` | no | `false` | — | Skip the post-switch readiness report (useful in scripts) — **NEW** |
| `yes` | flag | `bool` | no | `false` | — | Confirm, non-interactively, that a resident local model may stay loaded / be dropped — **NEW** |

> **Why `backend` is not declared with `AllowedValues`.** The source restricted the value to the literal set `azure|bedrock|llama` and rejected anything else. `AllowedValues` is an `init`-only compile-time array; a registry that grows by dropping a package into `.\packages` cannot be expressed that way. Validation is therefore done against the live registry inside the tool, and the built-in three are named in `ValueDescription`, `Prototype` and the failure message so the user still sees a closed list when only the built-ins are loaded.

**Pipeline behaviour.** Accepts piped input and produces piped output. **One chunk = one backend key**; the tool switches to it and emits a one-line confirmation. Piping more than one key is legal and the last one wins — the emitted rows make the sequence visible, which is why the tool does not silently swallow all but the last. `backend` is the `UsePipe = true` parameter (there is at most one per command by convention). `OutputFormat = General`.

**Environment interaction.** Writes `CHATDBG_PROVIDER` (lower-cased, exactly as the source stored it). Because that is a **global** key with no command-name prefix, the tool must be registered `modifiesEnvironment: true`, otherwise the host routes the write into this command's private bucket and the change is invisible to every other tool. Also writes its own audit breadcrumb `USE_PREVIOUS` (command-prefixed, so it persists without needing global rights). Reads `CHATDBG_PROVIDER` with `storeDefault: false` before overwriting so it can report the transition.

**Failure modes.**

| Condition | Behaviour |
|---|---|
| Unknown key | `Failure` chunk: `Unknown backend 'x'. Registered: azure, bedrock, llama. Run 'MODEL LIST' to see them all.` Nothing is written. |
| Known key, backend not configured | The switch **still happens** (matching the source, where `/set provider` never required readiness), followed by a warning row naming the remediation route for that backend — environment-variable names for cloud backends, `MODEL SELECT <path>` for the local one. Exit is a success chunk plus a warning chunk, not a failure. |
| Switch lands on a backend whose declared capability record cannot supply token log probabilities **while capture is on** | **Owner decision D-001:** the switch succeeds, then this tool **turns capture off** (`TOKEN_ENABLED` → `false`, persisted) and appends the auto-disable notice chunk: `Token log probabilities disabled: provider '{provider}' does not support them. Use a provider that does (see MODEL CAPS){, e.g. '{example}'}.` — naming a configured capable backend where one exists. Informational, not a failure; the turn loop is unaffected. The same rule runs at session start (§A.3) when loaded settings combine capture-on with a capability-absent provider. |
| Known key, backend unavailable on this platform | Switch is **refused** with a failure chunk naming the reason (`no native runtime for <rid>`), because activating it guarantees every subsequent turn fails. |
| A local model is resident and the target is a cloud backend | Interactive host (`HasPipedInput == false`): prompt `Unload the resident model '<name>' (frees <n> MB)? [y/N]`. Non-interactive: **requires `-yes`**, otherwise refuses with a failure chunk explaining that `PromptForCommand` is only meaningful off-pipe. Default answer is *no* — the model stays resident so switching back is instant. |
| Downstream stage fails after the switch | Nothing is rolled back. The switch is already committed to the environment; the failure chunk propagates. Documented, not repaired: rollback across pipeline stages is not something the framework offers. |

**Security and audit.** No secret. **Environment-modifying → audited twice:** once as an `AuditEvent` for the execution, and once as a `LogEnvironmentChange` for `CHATDBG_PROVIDER` (the environment-change path is the one where the framework's redaction actually works, since it matches on the variable name). Not destructive on its own; destructive only through the optional unload, which is confirmation-gated.

**Traceability.** PRD **7.6** (with **7.1** for dispatch). Descends from `/set provider <azure|bedrock|llama>` (`Provider must be 'azure', 'bedrock', or 'llama'`).

---

#### 5.4 `MODEL SELECT` — choose the model the active backend will serve

| | |
|---|---|
| Command | `SELECT` |
| Root command | `MODEL` |
| Description | Set the model id, deployment name or weight-file handle for a backend |
| Prototype | `MODEL SELECT <model> [-revision <r>] [-backend <key>] [-force]` |

```csharp
[CommandRegister("Select", "Set the model id, deployment name or weight-file handle for a backend",
    Prototype = "MODEL SELECT <model> [-revision <r>] [-backend <key>] [-force]")]
[CommandParameterOrdered("model", "Catalog handle, model id, deployment name, or weight-file handle", UsePipe = true)]
[CommandParameterNamed("revision", "Version suffix to re-attach after the ':' the tokenizer removes (e.g. 0 for '…-v1:0')")]
[CommandParameterNamed("backend", "Backend to change (default: the active one)")]
[CommandFlag("force", "Skip catalog and file-existence validation")]
[CommandHelpRemarks("The argument tokenizer removes ':' '/' '\\' and '=' even inside quotes. Prefer a catalog handle from MODEL CATALOG, or pipe the value in.")]
[CommandHelpRemarks("Registered with modifiesEnvironment: true.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `model` | ordered | `string` | yes | — | a catalog handle (`[-_0-9A-Za-z]`), or a raw id | The model to select |
| `revision` | named | `string` | no | *(empty)* | `[-_0-9A-Za-z.]` | **NEW.** Re-attaches a `:`-delimited version suffix that the framework's tokenizer strips: `MODEL SELECT "anthropic.claude-3-sonnet-20240229-v1" -revision 0` reconstructs `anthropic.claude-3-sonnet-20240229-v1:0` |
| `backend` | named | `string` | no | active backend | any registered key | Change a non-active backend's model without switching to it |
| `force` | flag | `bool` | no | `false` | — | **NEW.** Bypass validation. Required to set an id the catalog does not know, or a weight path that does not yet exist |

**Validation, per backend** (this is where the rebuild is stricter than the source, deliberately):

- **azure** — the value is the *deployment* name and doubles as a URL path segment. Rejected if it contains a character that would need URL-encoding (the source interpolated it into the URL unencoded). Warned, not rejected, if it is absent from the cached deployment catalog.
- **bedrock** — checked against the cached foundation-model / inference-profile catalog. If the id resolves to an Anthropic model, the tool reports the family it will use. Family detection is `^(?:[a-z]{2}\.)?anthropic\.` case-insensitive, which — unlike the source's bare `anthropic.` prefix test — also matches cross-region inference profiles such as `us.anthropic.…`.
- **llama** — the value must resolve to an existing file. The source's `/set modelId` did this check and its `/model` command did not, while both wrote the same field; here there is one path and it always checks. A **`.gguf` extension check and a GGUF magic-number read** are performed as a *warning*, not a rejection, preserving the source's behaviour that any existing file counts as configured (a zero-byte file passes readiness) while telling the user it will not load.

**Pipeline behaviour.** Both. **One chunk = one candidate model id or handle**; the tool validates and selects it, emitting one confirmation row per chunk (last write wins, every step visible). This is the safe channel for values containing `:` `/` `\`, because pipe payloads are not passed through the command-line tokenizer. `MODEL CATALOG -format ids | REGIF "^anthropic" | MODEL SELECT` is the canonical form. `OutputFormat = General`.

**Environment interaction.** Writes `CHATDBG_MODEL_ID` (global → `modifiesEnvironment: true`). Reads `CHATDBG_PROVIDER`, `CHATDBG_MODEL_ID`, and, for `llama`, the model-search roots key `CHATDBG_MODEL_PATHS` (**NEW**, `storeDefault: false`).

**Failure modes.**

| Condition | User sees |
|---|---|
| Value arrived mangled by the tokenizer (contains no `:`/`/` but the catalog has exactly one id whose stripped form matches) | Failure chunk that *names the mangling*: `'anthropic.claude-3-sonnet-20240229-v10' looks like 'anthropic.claude-3-sonnet-20240229-v1:0' with the ':' removed by argument tokenization. Re-run with -revision 0, or pipe the id in.` This is the single most valuable error message in the package. |
| Unknown id, catalog available | Failure chunk listing the three closest catalog entries, plus `use -force to set it anyway`. |
| Unknown id, catalog unavailable (offline / no permission) | **Degrades**: accepts the value with a warning row `catalog unavailable (<reason>); id not verified`. Never blocks configuration because discovery failed. |
| Local weight file missing | Failure chunk `Model file not found: <path>` — the source's exact wording — plus `Make sure you've specified the correct path to a GGUF model file.` |
| Local file exists but is not GGUF | Success + warning row: `warning: '<file>' has no GGUF magic; MODEL LOAD will fail.` |
| Downstream error arrives through the pipe | Forwarded unchanged by the framework before this tool sees it. |

**Security and audit.** No secret. A model id is not sensitive, but it is *tenant-identifying* for a private Azure deployment, so the audit event carries it as-is by design and the host is expected to scope its audit sink accordingly. Not destructive; changing the local model id does **not** unload a resident model (that happens lazily at the next `MODEL LOAD`, matching the source's load-on-path-change rule) — this is stated in the confirmation row so the user is not surprised.

**Traceability.** PRD **7.6**/**7.7**/**7.8**, writing **7.2**. Descends from `/model <id…>` (`Changed model from 'old' to 'new'`) and from `/set modelId <id>` with its local-file pre-check. The two source commands are merged here because they wrote the same field with different rules.

---

#### 5.5 `MODEL CATALOG` — list the models a backend can actually serve — **NEW**

| | |
|---|---|
| Command | `CATALOG` |
| Root command | `MODEL` |
| Description | List the models, deployments or weight files available to a backend |
| Prototype | `MODEL CATALOG [-backend <key>] [-filter <substring>] [-take <n>] [-format text\|ids\|csv\|json] [-refresh]` |

```csharp
[CommandRegister("Catalog", "List the models, deployments or weight files available to a backend",
    Prototype = "MODEL CATALOG [-backend <key>] [-filter <substring>] [-take <n>] [-format text|ids|csv|json] [-refresh]")]
[CommandParameterNamed("backend", "Backend to interrogate (default: the active one)")]
[CommandParameterNamed("filter", "Case-insensitive substring match on id and display name")]
[CommandParameterNamed("take", "Maximum entries to return", DataType = typeof(int), DefaultValue = "50")]
[CommandParameterNamed("format", "Output shape", AllowedValues = new[] { "text", "ids", "csv", "json" })]
[CommandFlag("refresh", "Bypass the cache and re-query the backend")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `backend` | named | `string` | no | active backend | any registered key | Which backend to interrogate |
| `filter` | named | `string` | no | *(empty)* | free text, trimmed; **bounded to 200 characters**, longer input truncated | Case-insensitive substring on id and display name. For regular expressions, pipe through `REGIF` |
| `take` | named | `int` | no | `50` | **clamped to `1`–`200`** (`Math.Clamp`) | Maximum entries |
| `format` | named | `string` | no | `text` | `text`, `ids`, `csv`, `json` | `ids` emits the bare id per chunk — the pipe-friendly form |
| `refresh` | flag | `bool` | no | `false` | — | Ignore the cache; always hits the network / rescans the disk |

Per-backend semantics:

| Backend | What it lists | Cost | Cache TTL |
|---|---|---|---|
| `azure` | Deployments on the configured resource. If the deployment-listing surface is not reachable with the configured credential, **degrades** to a single row for the configured deployment plus `catalog not available for this backend; showing configured model only` | one metadata call, no tokens | 15 min |
| `bedrock` | Foundation models and inference profiles entitled to the account in the configured region, each row carrying id, provider, and the streaming/tool-calling flags the service reports | one metadata call, no tokens | 15 min |
| `llama` | `*.gguf` files under the model-search roots (`CHATDBG_MODEL_PATHS`, defaulting to the directory of the currently configured weight file), with size in whole MB, quantisation tag parsed from the filename where present, and a stable short **handle** | filesystem scan only | 60 s |

**Handles** are the point of this tool. Each row is assigned a token-safe handle drawn from `[-_0-9A-Za-z]` (e.g. `llama3-8b-q4km`, `claude-3-sonnet`), which survives the framework's argument tokenizer intact and can be typed directly into `MODEL SELECT` and `MODEL LOAD`. Handles are stable for the lifetime of the cache entry and are printed in every format.

**Pipeline behaviour.** Produces piped output, **one chunk per catalog row**; overrides `Main` for the same reason `LIST` does. Does not accept piped input (returns an explanatory chunk). `OutputFormat` follows `-format`.

**Environment interaction.** Reads `CHATDBG_PROVIDER`, `CHATDBG_AZURE_ENDPOINT`, `CHATDBG_AZURE_API_VERSION`, `CHATDBG_AWS_REGION`, `CHATDBG_MODEL_PATHS`, all with `storeDefault: false`; obtains resolved credentials from the credentials package. Writes the cache timestamp under its own command-prefixed key `CATALOG_FETCHED_AT` (private bucket, no global rights needed). Not environment-modifying.

**Failure modes.** Network/credential failure → **degrades**, never throws: a warning row naming the cause plus whatever local knowledge exists (the configured model). HTTP non-2xx → the status and a **truncated, redacted** excerpt of the body (max 200 characters, credential-shaped substrings masked); the source echoed entire upstream error bodies into the terminal, which is how quota text, request ids and echoed prompt fragments reached the screen. Model-search root missing → warning row, empty result, no exception. `take` non-numeric → parse-time `ArgumentException`; the specific text lands in the trace, so the range is repeated in the help.

**Security and audit.** Credentials are used, never emitted. Endpoint hosts and region names appear in output. Read-only, no confirmation.

**Why this NEW tool earns its place.** In the source, the model identifier was free text with **no validation on any path**, and the shipped default (`gpt-4`) is not a valid Bedrock identifier — so the default configuration of the Bedrock backend was guaranteed to fail, with the failure surfacing only as an opaque service error at the end of a chat turn. A catalog turns the product's most common misconfiguration into a pick-list.

**Traceability.** **NEW.** PRD **7.7** (Managed Cloud Model Marketplace) is the closest section; it also serves **7.6** and **7.8**. No source ancestor.

---

#### 5.6 `MODEL CAPS` — report what a backend can actually do — **NEW**

> **D-001 makes this tool load-bearing:** it is the declared-capability authority that the auto-disable notice, the enable-refusal text and the full-screen shell's disabled-control explanation all point the user at. Capability is read from the backend's declared record (refined by the cached probe where one has run) — never inferred from a failed call. A backend that cannot supply token log probabilities is reported here as such, by name, before any turn is attempted.

| | |
|---|---|
| Command | `CAPS` |
| Root command | `MODEL` |
| Description | Report a backend's capabilities: log-probabilities, streaming, tool calling, ceilings |
| Prototype | `MODEL CAPS [<backend>] [-only all\|logprobs\|streaming\|tools\|limits] [-format text\|csv\|json] [-probe]` |

```csharp
[CommandRegister("Caps", "Report a backend's capabilities: log-probabilities, streaming, tool calling, ceilings",
    Prototype = "MODEL CAPS [<backend>] [-only all|logprobs|streaming|tools|limits] [-format text|csv|json] [-probe]")]
[CommandParameterOrdered("backend", "Backend to report on (default: the active one)", IsRequired = false, UsePipe = true)]
[CommandParameterNamed("only", "Restrict the report to one capability group",
    AllowedValues = new[] { "all", "logprobs", "streaming", "tools", "limits" })]
[CommandParameterNamed("format", "Output shape", AllowedValues = new[] { "text", "csv", "json" })]
[CommandFlag("probe", "Verify the declared capability against the live service instead of reporting the declared matrix")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `backend` | ordered | `string` | no (`IsRequired = false`) | active backend | any registered key | Backend to report on |
| `only` | named | `string` | no | `all` | `all`, `logprobs`, `streaming`, `tools`, `limits` | Restrict the report |
| `format` | named | `string` | no | `text` | `text`, `csv`, `json` | Output shape |
| `probe` | named→**flag** | `bool` | no | `false` | — | Ask the service rather than the table. Costs one metadata call, or (for `logprobs`) one 1-token completion. Implies the same cost warning as `MODEL TEST -deep` |

Capabilities reported, and the honest answers the rebuild must give:

| Capability | `azure` (hosted OpenAI) | `bedrock` | `llama` (local GGUF) |
|---|---|---|---|
| Per-token log-probabilities | **yes** — request `logprobs: true`, `top_logprobs: K` | **no** — no Bedrock API returns them, on any first-party foundation model | **yes**, and richest of the three: the full vocabulary distribution is reachable in-process |
| Top-K reporting ceiling | **20** (service maximum, and the product's own `1`–`20` range agrees) | n/a | vocabulary size; the product still clamps the *reported* K to `1`–`20` |
| Prompt-side (input token) log-probabilities | no | no | **yes**, in principle |
| Streaming | yes | yes | yes |
| Tool / function calling | yes | yes | **partial** — depends on the weight file's chat template; reported as `partial (template-dependent)` |
| Temperature ceiling | `2.0` | `1.0` for `anthropic.*`; model-dependent otherwise | `2.0` |
| Max output tokens ceiling | model-dependent, product cap `8192` | model-dependent, product cap `8192` | product cap `8192`; falls back to `512` if the setting is ≤ 0 |
| Context window | model-dependent (from the catalog) | model-dependent (from the catalog) | from the weight file's metadata when loaded; otherwise the configured `512`–`32768` value |
| Cancellation of an in-flight turn | yes (**NEW** — the source threaded no cancellation anywhere) | yes (**NEW**) | best-effort at token boundaries (**NEW**) |

**Pipeline behaviour.** Both. **One chunk = one backend key** on input; one chunk per backend (or per capability row when `-format csv`/`json`) on output — so it overrides `Main` for the multi-row case. This is the tool that makes `MODEL LIST -format keys | MODEL CAPS -only logprobs` a one-line capability matrix. `OutputFormat` follows `-format`; `json` is declared so the token package can consume it structurally.

**Environment interaction.** Reads `CHATDBG_PROVIDER`, `CHATDBG_MODEL_ID`, `CHATDBG_LOGPROBS_TOPK` (`storeDefault: false`). With `-probe`, additionally needs resolved credentials. Writes nothing. Not environment-modifying.

**Failure modes.** `-probe` with an unconfigured backend → falls back to the declared matrix and marks every probed row `declared (not probed: backend not configured)`. Probe network failure → same degradation with the reason attached. Unknown backend → failure chunk listing registered keys. **A capability is never reported as `yes` because a probe failed to disprove it**; the two states are `declared` and `verified`, and the output says which.

**Security and audit.** No secret in parameters or output. `-probe` makes a billable call on cloud backends — reported in the output as `probe cost: 1 metadata call` or `probe cost: 1 completion (≤ 1 token)`. Read-only; no confirmation, because the cost is bounded and disclosed.

**Why this NEW tool earns its place.** The source shipped two capability lies that this tool exists to end. (1) When Azure returned no log-probability block, the backend **fabricated** one — 15 sampled words at a uniform `ln(0.9)` ≈ 90 % confidence with three canned alternatives — and returned it *indistinguishably from real data*, so a user studying "model confidence" could be reading invented numbers. (2) The Bedrock backend put `logprobs` and `top_logprobs` members into every request payload, where they are not part of any real Bedrock contract, and then parsed a response shape no real model emits. **The rebuild does not fabricate.** `MODEL CAPS` is the single place that says what is really available, and the token package is specified to refuse `TOKEN LOGPROBS ENABLE` on a backend whose caps report `logprobs: no`.

**Traceability.** **NEW.** PRD **7.9** (Token Probability Analysis) is the consumer; the tool itself sits in **7.6**. Its ancestors are the source's *symptoms*, not its code: the Azure fabrication path and the two-line notice `Note: Log probabilities were requested but none were returned by the model. / This could be due to the model not supporting this feature or an API limitation.`

---

#### 5.7 `MODEL TEST` — verify connectivity, credentials and entitlement — **NEW**

| | |
|---|---|
| Command | `TEST` |
| Root command | `MODEL` |
| Description | Probe a backend end to end: configuration, credential resolution, reachability, entitlement |
| Prototype | `MODEL TEST [<backend>] [-timeout <s>] [-deep] [-format text\|csv\|json]` |

```csharp
[CommandRegister("Test", "Probe a backend end to end: configuration, credentials, reachability, entitlement",
    Prototype = "MODEL TEST [<backend>] [-timeout <s>] [-deep] [-format text|csv|json]")]
[CommandParameterOrdered("backend", "Backend to test (default: the active one)", IsRequired = false, UsePipe = true)]
[CommandParameterNamed("timeout", "Per-stage timeout in seconds", DataType = typeof(int), DefaultValue = "30")]
[CommandParameterNamed("format", "Output shape", AllowedValues = new[] { "text", "csv", "json" })]
[CommandFlag("deep", "Also send a 1-token completion to prove entitlement (billable)")]
[CommandHelpRemarks("Stages run in order and stop at the first hard failure; each stage reports pass/fail/skip with a reason.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `backend` | ordered | `string` | no (`IsRequired = false`) | active backend | any registered key | Backend to test |
| `timeout` | named | `int` | no | `30` | **clamped to `1`–`300`** | **NEW.** Per-stage timeout. The source configured no timeout anywhere — the only bound was the transport's ~100 s default and the UI simply sat on `Thinking...` |
| `deep` | flag | `bool` | no | `false` | — | Send a minimal completion. Billable on cloud backends; on the local backend it loads the weights |
| `format` | named | `string` | no | `text` | `text`, `csv`, `json` | Output shape |

Stages, in order (one output chunk per stage):

| # | Stage | `azure` | `bedrock` | `llama` |
|---|---|---|---|---|
| 1 | **configured** | endpoint, resolved key and model id all non-empty | model id non-empty **and** an access key resolvable | model id non-empty **and** the file exists |
| 2 | **credential source** | reports the winning leg of the chain: process env → OS keystore (opt-in) → settings file (deprecated) | same, for the AWS pair, and reports whether a **session token** is present | `n/a (no credential required)` |
| 3 | **shape** | endpoint parses as an absolute HTTPS URI; deployment name is URL-safe; api-version non-empty | region is a syntactically plausible region name; model id family is recognised | file has GGUF magic; size in whole MB |
| 4 | **reachable** | TLS handshake + one metadata request | one metadata request | `n/a` |
| 5 | **entitled** (`-deep`) | 1-token completion against the configured deployment | 1-token invocation against the configured model | load weights + create context, then unload if it was not resident before |
| 6 | **capability agreement** | compares what `MODEL CAPS` declares with what stage 5 actually returned (e.g. a log-probability block genuinely present) | reports `logprobs: unavailable — expected` | reports the effective sampler top-K |

**Cross-platform note.** Stage 2 must report `OS keystore: unavailable on this platform` on Linux and macOS rather than silently skipping it. The source's credential chain collapsed to *process environment → settings file* off Windows with **no indication whatsoever**, while the shipped documentation instructed Linux users to enable the keystore — a setup that could not work on the platform it targeted. Naming the unavailability is the fix.

**Pipeline behaviour.** Both. **One chunk = one backend key** on input; **one chunk per stage** on output (so `MODEL LIST -format keys | MODEL TEST` produces a readable stage-by-stage report for every backend). Overrides `Main` on the non-piped path to emit multiple stage chunks. A stage that fails emits a `CommandResult<string>.Failure(...)`, which the host re-wraps and records; subsequent stages emit `skipped (previous stage failed)` as successes so the report is complete rather than truncated. `OutputFormat` follows `-format`.

**Environment interaction.** Reads every configuration key `MODEL SHOW` reads, plus asks the credentials package to resolve secrets. Reads the OS **process** environment only through that package. Writes its own `TEST_LAST_RESULT` and `TEST_LAST_RUN_AT` under the command-prefixed private bucket. Not environment-modifying.

**Failure modes.**

| Condition | User sees |
|---|---|
| Not configured | Stage 1 fails with the *specific missing item(s)*, plus the exact remediation for the platform: environment-variable names for cloud backends, `MODEL SELECT` for local. Never the source's message `…is not properly configured. Please set AzureEndpoint, AzureApiKey, and ModelId.`, which named internal identifiers rather than commands. |
| DNS / TLS / connect failure | Stage 4 fails with the transport reason and the **host only**, never the full URL with the key header. |
| HTTP non-2xx | Stage 4 or 5 fails with the numeric status **and** its symbolic name (the source printed only the symbolic name), plus a body excerpt truncated to 200 characters with credential-shaped substrings masked. |
| Timeout | The stage fails as `timeout after <n>s`; remaining stages are skipped. The source had no timeout, so this state did not exist. |
| Temperature above the model's real ceiling | Stage 6 warns: `temperature 1.5 exceeds this model's ceiling of 1.0; requests will be rejected upstream`. |
| Downstream error arriving through the pipe | Forwarded verbatim by the framework; `TEST` never re-labels an upstream failure as a backend problem. |

**Security and audit.** This is the tool most likely to touch a secret, and therefore the one most tightly specified: it **never accepts a credential as a parameter**, **never prints one**, and **never writes one to the trace**. That is not a preference — the framework's own audit masking only rewrites `-name=value` tokens, while the framework's tokenizer strips `=`, so parameter masking is effectively non-functional for this command syntax. The only safe rule is "secrets never appear in a command line", and this package enforces it structurally. `-deep` is billable and is therefore opt-in, disclosed in the output, and refused when `MODEL CAPS` reports the backend unavailable. Not destructive.

**Why this NEW tool earns its place.** The source performed **no** format validation, no URL parsing, no scheme check, no key-shape check and no connectivity probe anywhere; an endpoint typo surfaced only as a wrapped exception at the end of a chat turn, and a missing AWS *secret* key still reported "configured" because readiness checked only the access key. `MODEL TEST` converts a class of silent misconfiguration into a two-second answer.

**Traceability.** **NEW.** PRD **7.6**/**7.7**/**7.8**, consuming **7.3**. Its ancestor is the console shell's start-up *self-check* block (`Warning: <name> service is not configured.` plus the numbered remediation list) — promoted from an un-runnable start-up side effect into a first-class, re-runnable, pipeable tool.

---

#### 5.8 `MODEL LOAD` — bring a local weight file resident

*(Satellite assembly `ChatDbg.Tools.ModelBackends.Local`.)*

| | |
|---|---|
| Command | `LOAD` |
| Root command | `MODEL` |
| Description | Load a GGUF weight file into memory with its hardware-acceleration settings |
| Prototype | `MODEL LOAD [<model>] [-context <n>] [-gpu-layers <n>] [-threads <n>] [-batch <n>] [-device <spec>] [-no-mmap] [-mlock] [-force] [-yes]` |

```csharp
[CommandRegister("Load", "Load a GGUF weight file into memory with its hardware-acceleration settings",
    Prototype = "MODEL LOAD [<model>] [-context <n>] [-gpu-layers <n>] [-threads <n>] [-batch <n>] [-device <spec>] [-no-mmap] [-mlock] [-force] [-yes]")]
[CommandParameterOrdered("model", "Catalog handle or configured weight file (default: the configured model)",
    IsRequired = false, UsePipe = true)]
[CommandParameterNamed("context",    "Context window in tokens",   DataType = typeof(int))]
[CommandParameterNamed("gpu-layers", "Layers to offload to GPU",   DataType = typeof(int))]
[CommandParameterNamed("threads",    "Worker threads, 0 = host default", DataType = typeof(int))]
[CommandParameterNamed("batch",      "Batch size",                 DataType = typeof(int))]
[CommandParameterNamed("device",     "GPU device selector, e.g. 0 or 0,1")]
[CommandFlag("no-mmap", "Disable memory-mapping of the weight file")]
[CommandFlag("mlock",   "Lock the weights in physical memory")]
[CommandFlag("force",   "Reload even when the same file is already resident")]
[CommandFlag("yes",     "Confirm tearing down a resident model non-interactively")]
[CommandHelpRemarks("Paths are mangled by argument tokenization. Use a handle from MODEL CATALOG, pipe the path in, or set it once with MODEL SELECT.")]
[CommandHelpRemarks("Hardware options given here apply to THIS load only; MODEL TUNE persists them.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `model` | ordered | `string` | no (`IsRequired = false`) | `CHATDBG_MODEL_ID` | catalog handle, or an id/path arriving through the pipe | Which weight file to load |
| `context` | named | `int` | no | `CHATDBG_LLAMA_CONTEXT_SIZE`, itself defaulting to `4096` | **`512`–`32768` inclusive** | Context window. A configured value ≤ 0 falls back to `4096` — the rebuild uses one constant here, where the source used `2048` at load time and `4096` in its state snapshot |
| `gpu-layers` | named | `int` | no | `CHATDBG_LLAMA_GPU_LAYERS`, default `0` | **`0`–`100` inclusive**; `0` = CPU only | Layers offloaded to the GPU |
| `threads` | named | `int` | no | `CHATDBG_LLAMA_THREADS`, default `0` | **`0`–`64` inclusive**; `0` = host default | **Applied for real.** The source validated, persisted and displayed this value and then never passed it to the engine |
| `batch` | named | `int` | no | `CHATDBG_LLAMA_BATCH_SIZE`, default `512` | **`1`–`2048` inclusive** | **Applied for real** (same story as `threads`) |
| `device` | named | `string` | no | `CHATDBG_LLAMA_GPU_DEVICE`, default empty | free text: an index or comma-separated indices, e.g. `0`, `0,1` | **Applied for real** (same story). Ignored with a warning where the platform's accelerator has no device index (see cross-platform note) |
| `no-mmap` | flag | `bool` | no | `false` (i.e. mmap **on**) | — | **NEW.** The source hard-wired memory-mapping on with no way to disable it |
| `mlock` | flag | `bool` | no | `false` | — | **NEW.** The source hard-wired memory-locking off |
| `force` | flag | `bool` | no | `false` | — | **NEW.** The source reloaded **only** when the *path* changed, so changing context size, GPU layers, threads or batch had no effect until the process restarted. `-force` is the escape hatch |
| `yes` | flag | `bool` | no | `false` | — | **NEW.** Non-interactive confirmation for tearing down a resident model |

**Behaviour, preserved from the source.** Reload is required when no weights are resident, no context exists, or the requested path differs from the resident one — plus, now, when `-force` is given or when any hardware option differs from the resident configuration (the latter two are the improvement). Teardown order on reload is strict and unchanged: session → executor → context (disposed) → weights (disposed), each reference cleared *before* the new load begins. Weight loading and context creation run off the calling thread. The system prompt is bound **once**, at load time, into a fresh session — so a system-prompt change still requires a reload, and the tool now says so instead of leaving the user to discover it.

**Progress and logging.** Emits, in order: the resolved path, the file size in whole MB, `Loading model with context size: <n>, GPU layers: <m>`, then load-complete with elapsed time. Progress is routed through `IIoContext.SetProgress` / `SetStatusMessage` — status, not command output — so a quiet host redirects it to the trace instead of the transcript. The engine's own native log stream is handed to the `DIAG` package's sink; this tool does not own a log file.

**Pipeline behaviour.** Both. **One chunk = one weight-file path or catalog handle.** Piping is the recommended way to pass a real filesystem path, because pipe payloads bypass the argument tokenizer that would otherwise strip `\`, `/` and `:` from the value. Piping several paths loads them **in sequence**, unloading each before the next — deliberately, since two resident multi-gigabyte models is rarely what anyone means. Output is one summary chunk per load. `OutputFormat = General`; with `-format` absent there is no structured shape to declare.

**Environment interaction.** Reads `CHATDBG_MODEL_ID`, `CHATDBG_LLAMA_CONTEXT_SIZE`, `CHATDBG_LLAMA_GPU_LAYERS`, `CHATDBG_LLAMA_THREADS`, `CHATDBG_LLAMA_BATCH_SIZE`, `CHATDBG_LLAMA_GPU_DEVICE`, `CHATDBG_SYSTEM_PROMPT_BODY`, all with `storeDefault: false`. Writes `LOAD_RESIDENT_PATH`, `LOAD_RESIDENT_AT`, `LOAD_EFFECTIVE_CONTEXT`, `LOAD_EFFECTIVE_GPU_LAYERS` into its own command-prefixed bucket — which persists across invocations **without** environment-modifying permission, and is exactly what `MODEL STATUS` reads. Registered `modifiesEnvironment: false`.

**Failure modes.**

| Condition | Behaviour |
|---|---|
| Weight file missing at load time | `Failure` chunk `Model file not found: <path>` (source wording preserved) |
| Not a GGUF / incompatible architecture | `Failure` chunk carrying the engine's message plus the four numbered candidate causes the source enumerated: incompatible model format; missing or incompatible native libraries; insufficient memory; runtime/library version incompatibility — and, added here, *model architecture newer than the bundled engine build* |
| Context creation fails | `Failure` chunk `Failed to create context: <reason>`, weights already released |
| GPU requested but no GPU backend present for this RID | **Degrades**: loads on CPU, emits a warning row `gpu-layers 32 requested but no GPU backend is available (<reason>); loaded CPU-only`. Never a hard failure |
| `-device` given on a platform whose accelerator has no device index | Warning row `device selector ignored on this platform (<accelerator>)`; load proceeds |
| Insufficient memory | `Failure` chunk with the requested context size and a concrete suggestion to lower it |
| A different model is already resident | Interactive: prompt. Non-interactive: **requires `-yes`**, else a failure chunk explaining why (`PromptForCommand` is only meaningful when there is no input pipe) |
| Uncatchable native fault (access violation inside the engine) | **The process dies; no managed handler can intercept it.** Documented, not repaired at this layer. Mitigations the tool applies pre-emptively: refuse a `-context` above the range, warn when both a CPU and a GPU native backend are present for the same RID, warn on non-ASCII or space-bearing paths, and record the intended load into the diagnostics sink *before* calling the engine, so the last line of the daily log names the file that killed the shell |
| Downstream error through the pipe | Forwarded unchanged |

**Security and audit.** No secret. **Destructive**: loading over a resident model discards that model's session, which is where the local backend's multi-turn memory actually lives — so the conversation the engine remembers is lost even though the transcript in `CHAT` is untouched. Confirmation is therefore required, interactively or via `-yes`. The audit event records the resolved path, the effective context size and the effective GPU layer count.

**Traceability.** PRD **7.8** (Local Model Inference). Descends from the source's implicit `EnsureModelLoadedAsync` — a private, lazy, first-chat-turn side effect with no user-facing command, whose parameters were only observable through a line echoed to standard output. Promoting it to an explicit tool is what makes `-force`, `-threads`, `-batch` and `-device` meaningful at all.

---

#### 5.9 `MODEL UNLOAD` — release the resident local model

*(Satellite assembly.)*

| | |
|---|---|
| Command | `UNLOAD` |
| Root command | `MODEL` |
| Description | Release the resident weight file, its context and its session |
| Prototype | `MODEL UNLOAD [-yes] [-quiet]` |

```csharp
[CommandRegister("Unload", "Release the resident weight file, its context and its session",
    Prototype = "MODEL UNLOAD [-yes] [-quiet]")]
[CommandFlag("yes",   "Confirm non-interactively")]
[CommandFlag("quiet", "Suppress the summary row; report only failures")]
[CommandHelpRemarks("Unloading discards the engine's retained conversation state. The transcript in CHAT is not affected.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `yes` | flag | `bool` | no | `false` | — | Confirm without prompting. Mandatory when there is no interactive prompt available |
| `quiet` | flag | `bool` | no | `false` | — | Emit nothing on success |

**Pipeline behaviour.** Neither meaningfully. It declares no piped-input handling and, when invoked with an input pipe, returns an explanatory chunk (`MODEL UNLOAD takes no piped input.`) rather than throwing — the framework convention for a tool that cannot participate. It emits one summary chunk (or none, with `-quiet`). Placed last in a pipeline it acts as a sink; that is the only sensible composition.

**Environment interaction.** Reads and then clears its own `LOAD_*` bucket keys. Reads nothing global. Not environment-modifying.

**Failure modes.** Nothing resident → **not an error**: `No local model is resident.` as a success chunk (idempotent by design, so scripts can call it unconditionally). Native release fails or reports that references remain → warning row `unload reported incomplete: <reason>; memory may not be reclaimed until process exit`, recorded as a leak metric in the diagnostics sink, not as a failure. A generation is in flight → the tool waits on the same process-wide serialisation lock the engine uses and reports `waiting for an in-flight generation…` as a status message; there is no timeout, matching the source, and this is called out in **Design notes** as the one place a bounded wait should be added.

**Security and audit.** No secret. **Destructive and irreversible for in-engine conversation state** → confirmation required (interactive prompt, or `-yes`). Reloading is possible but the retained session is gone.

**Traceability.** PRD **7.8**. Descends from the source's `Dispose` path (session and executor references dropped, context disposed, weights disposed, log component disposed), which was reachable only at shell shutdown and only from one of the two shells — the windowed host never tore its backends down at all.

---

#### 5.10 `MODEL STATUS` — report the resident engine's runtime state — **NEW**

*(Satellite assembly.)*

| | |
|---|---|
| Command | `STATUS` |
| Root command | `MODEL` |
| Description | Report what is resident locally, with what settings, and whether those settings are stale |
| Prototype | `MODEL STATUS [-format text\|csv\|json] [-stale]` |

```csharp
[CommandRegister("Status", "Report what is resident locally, with what settings, and whether they are stale",
    Prototype = "MODEL STATUS [-format text|csv|json] [-stale]")]
[CommandParameterNamed("format", "Output shape", AllowedValues = new[] { "text", "csv", "json" })]
[CommandFlag("stale", "Report only the settings that differ between the resident model and the current configuration")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text`, `csv`, `json` | Output shape |
| `stale` | flag | `bool` | no | `false` | — | Show only the drifted settings, and exit with a failure chunk when any exist — the script-friendly form |

Reported: resident path and handle; file size (whole MB); load timestamp and load duration; **effective** context size, GPU layers, threads, batch size, device, mmap/mlock; which native backend actually bound (CPU / CUDA / Vulkan / Metal) and its RID; the accelerator's reported device name when available; approximate resident bytes; the effective sampler top-K; and a **drift table** naming every configured value that differs from the value the resident model was loaded with.

> The drift table is the reason this tool exists. In the source, changing context size, GPU layers, threads, batch size or the system prompt after the model was loaded had **no effect whatsoever** until the model *path* changed or the process restarted — and nothing said so. `MODEL STATUS -stale` makes that condition visible and `MODEL LOAD -force` clears it.

Context accounting is reported honestly: tokens actually held in the engine's context, distinguished from generated-token counts. The source's per-step "model state" snapshot reported prompt-free counters (`totalTokensProcessed` and `contextTokenCount` were both just the generated-token index, and `remainingContext` therefore over-reported free space); those numbers were decorative and gated nothing. Where the engine cannot supply a real number, the field is reported as `unknown`, never as a synthesised one.

**Pipeline behaviour.** Produces piped output; does not accept piped input. Emits one chunk per reported group (`model`, `hardware`, `context`, `drift`), so it overrides `Main`. `OutputFormat` follows `-format`; `json` exists so `MODEL STATUS -format json | DIAG REPORT` can attach it to a bug report.

**Environment interaction.** Reads the `LOAD_*` private bucket and all `CHATDBG_LLAMA_*` keys with `storeDefault: false`. Writes nothing. Not environment-modifying.

**Failure modes.** Nothing resident → a single success chunk `No local model is resident. Run 'MODEL LOAD' or start a chat turn to load one.` With `-stale` and nothing resident → the same chunk, still a success (nothing has drifted). Engine present but unresponsive → the fields it cannot answer are `unknown` with a reason; the tool never blocks waiting for the engine.

**Security and audit.** No secret. Filesystem paths appear. Read-only.

**Traceability.** **NEW.** PRD **7.8**. Ancestors are internal-only: the source's load-time log lines, its per-token synthetic state snapshot, and the console banner's `LLama Configuration:` block — none of which reported what was *actually resident*.

---

#### 5.11 `MODEL TUNE` — persist the local hardware-acceleration settings

*(Satellite assembly.)*

| | |
|---|---|
| Command | `TUNE` |
| Root command | `MODEL` |
| Description | Persist the local engine's hardware-acceleration settings, optionally applying them now |
| Prototype | `MODEL TUNE [-context <n>] [-gpu-layers <n>] [-threads <n>] [-batch <n>] [-device <spec>] [-apply] [-reset] [-yes]` |

```csharp
[CommandRegister("Tune", "Persist the local engine's hardware-acceleration settings, optionally applying them now",
    Prototype = "MODEL TUNE [-context <n>] [-gpu-layers <n>] [-threads <n>] [-batch <n>] [-device <spec>] [-apply] [-reset] [-yes]")]
[CommandParameterNamed("context",    "Context window in tokens", DataType = typeof(int))]
[CommandParameterNamed("gpu-layers", "Layers to offload to GPU", DataType = typeof(int))]
[CommandParameterNamed("threads",    "Worker threads, 0 = host default", DataType = typeof(int))]
[CommandParameterNamed("batch",      "Batch size", DataType = typeof(int))]
[CommandParameterNamed("device",     "GPU device selector, e.g. 0 or 0,1")]
[CommandFlag("apply", "Reload the resident model so the new settings take effect now")]
[CommandFlag("reset", "Restore every hardware setting to its default")]
[CommandFlag("yes",   "Confirm the reload -apply implies, non-interactively")]
[CommandHelpRemarks("Registered with modifiesEnvironment: true. Without -apply the new values take effect at the next load.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `context` | named | `int` | no | unchanged (default `4096`) | **`512`–`32768` inclusive** | Context window in tokens |
| `gpu-layers` | named | `int` | no | unchanged (default `0`) | **`0`–`100` inclusive**; `0` = CPU only | Layers offloaded to the GPU |
| `threads` | named | `int` | no | unchanged (default `0`) | **`0`–`64` inclusive**; `0` = host default | Worker threads |
| `batch` | named | `int` | no | unchanged (default `512`) | **`1`–`2048` inclusive** | Batch size |
| `device` | named | `string` | no | unchanged (default empty) | free text; `0`, `0,1`, … | GPU device selector |
| `apply` | flag | `bool` | no | `false` | — | **NEW.** Reload now instead of at the next load |
| `reset` | flag | `bool` | no | `false` | — | **NEW.** Restore all five to their defaults |
| `yes` | flag | `bool` | no | `false` | — | **NEW.** Non-interactive confirmation for the reload `-apply` implies |

**Out-of-range policy: reject, do not clamp.** The source had two contradictory policies for the same values — the text commands rejected out-of-range input and changed nothing, while the windowed settings dialog silently clamped to the same bounds and saved the clamped value, and additionally ignored unparseable input while leaving the previous value in place. This package standardises on **reject**, matching the command-line half, and states the bounds in `ValueDescription` and `Prototype`. Ranges are inclusive at both ends, exactly as the source enforced them.

With no options at all, `MODEL TUNE` prints the current values and changes nothing — the same courtesy the source's `/model` with no arguments extended.

**Pipeline behaviour.** Produces piped output (one confirmation chunk); does not accept piped input, and says so when piped. `OutputFormat = General`.

**Environment interaction.** Reads and writes `CHATDBG_LLAMA_CONTEXT_SIZE`, `CHATDBG_LLAMA_GPU_LAYERS`, `CHATDBG_LLAMA_THREADS`, `CHATDBG_LLAMA_BATCH_SIZE`, `CHATDBG_LLAMA_GPU_DEVICE`. These are **global** keys owned by the settings package, so the tool must be registered `modifiesEnvironment: true`; without it every write lands in a private bucket and nothing else in the shell would see the change. It does **not** write the settings document itself — persistence is the settings package's job, triggered by the global environment change.

**Failure modes.** Out-of-range → failure chunk naming the value, the bound and the accepted range (`LlamaGpuLayerCount must be a number between 0 and 100. 0 means CPU-only, higher numbers move more layers to GPU.` — source wording preserved for the values the source validated). Unparseable → the framework marks the parameter invalid at parse time; the tool reports `-threads: 'abc' is not an integer` from the parameter's own `ValidationError` rather than letting the generic `Error executing TUNE` line be the whole story. `-apply` with nothing resident → settings are still written, and the output says `nothing resident; new settings will apply at the next load`. `-apply` with a resident model and no interactive prompt → requires `-yes`. `-gpu-layers` above 0 on a host with no GPU backend → **warning, not rejection**: the setting is stored (the user may be preparing a machine) and `MODEL LOAD` will degrade to CPU with its own warning.

**Cross-platform behaviour.** `threads = 0` means the host's default degree of parallelism on every OS. `gpu-layers` maps to CUDA or Vulkan offload on Windows and Linux and to Metal offload on Apple silicon. `device` is an accelerator device index on CUDA/Vulkan and is **ignored with a warning** on Metal, which exposes no equivalent selector. On a RID with no GPU-capable native backend at all, both `gpu-layers` and `device` are accepted and stored but reported as inert.

**Security and audit.** No secret. Not destructive without `-apply`; with `-apply` it inherits `MODEL LOAD`'s teardown semantics and its confirmation requirement. Environment-modifying → each key change is audit-logged individually through the environment-change path.

**Traceability.** PRD **7.8**, writing **7.2**. Descends from `/set llamaContextSize`, `/set llamaGpuLayers` (alias `llamaGpuLayerCount`), `/set llamaGpuDevice`, `/set llamaThreads`, `/set llamaBatchSize`, and from the windowed shell's "LLama Settings" tab. Three of those five settings were validated, persisted and displayed by the source and then **never applied to inference**; here they are applied, which is why they belong with the loader rather than with the general settings surface.

---

### Pipeline compositions

**1. Probe every backend in one line.**

```
MODEL LIST -format keys | MODEL TEST -timeout 10
```

`MODEL LIST -format keys` emits one chunk per registry key (`azure`, `bedrock`, `llama`). `MODEL TEST` consumes one key per chunk and emits one chunk per stage, so the user gets a complete stage-by-stage readiness report for the whole registry — the thing the source only ever produced as an un-repeatable start-up side effect, and only for the one active provider. Ten-second per-stage bound; an unconfigured backend fails at stage 1 and the remaining stages report `skipped`.

**2. Capability matrix, filtered to what matters.**

```
MODEL LIST -format keys | MODEL CAPS -only logprobs -format csv
```

Three rows of CSV: `azure,logprobs,yes,top_logprobs<=20,declared` / `bedrock,logprobs,no,-,declared` / `llama,logprobs,yes,full-vocabulary,declared`. Declaring `ResultFormat.CSV` on each chunk lets the host render a table and lets a downstream stage parse rather than scrape. This composition is the honest replacement for the source's fabricated 90 %-confidence tokens.

**3. Pick a Bedrock model from the catalog, using a framework built-in as the filter.**

```
MODEL CATALOG -backend bedrock -format ids -take 100 | REGIF "^anthropic" | MODEL SELECT -backend bedrock
```

`CATALOG` emits one bare id per chunk; `REGIF` (a framework built-in, package key `Default`) drops every chunk that does not match by returning an empty success, which the host silently discards; `SELECT` receives only the survivors, validates each against the same catalog, and reports the final selection. The composition also side-steps the argument tokenizer entirely: ids containing `.` and `:` travel through the pipe, never through the command line.

**4. Cross-package — let capability decide whether introspection is even legal.**

```
MODEL CAPS -only logprobs -format json | TOKEN LOGPROBS ENABLE -topk 20
```

`MODEL CAPS` emits one JSON chunk describing the active backend's log-probability support and ceiling. `TOKEN LOGPROBS ENABLE` (package **ChatDbg.Tools.Tokens**, PRD 7.9) consumes it and **refuses** when `logprobs: no`, and clamps `-topk` to the reported ceiling when it is lower than 20. This is the composition that structurally prevents the source's two capability lies: no fabricated probabilities on a backend that has none, and no `top_logprobs: 20` sent to a service that ignores it.

**5. Cross-package — attach a hardware snapshot to a bug report.**

```
MODEL STATUS -format json | DIAG REPORT -title "gguf load crash"
```

`MODEL STATUS` emits its `model` / `hardware` / `context` / `drift` groups as JSON chunks; `DIAG REPORT` (package **ChatDbg.Tools.Diagnostics**, PRD 7.11) folds them into a report alongside the tail of the daily engine log. For the local backend's signature failure — an uncatchable native access violation that kills the shell — this is the only forensic trail there is, which is why `MODEL LOAD` writes its intent to the diagnostics sink *before* it calls the engine.

**6. Retune and apply in one pass.**

```
MODEL TUNE -context 8192 -gpu-layers 32 -apply -yes | MODEL STATUS -stale
```

`TUNE` persists, reloads, and emits a confirmation; `STATUS -stale` then reports an empty drift table — the proof that the settings actually took effect, which in the source they never did without a path change or a restart.

---

### Design notes for the architect

**State this package holds.**

- A **process singleton** per assembly, resolved from the host's `IServiceProvider` (or a static accessor when the host has no container): the backend registry projection, the shared HTTP transports (one per cloud backend, created once, never per command), the catalog cache, and — in the satellite — the resident weight handle, its context, its session, and the two serialisation locks (one for generation, one for loading).
- Nothing on a command instance. The controller keeps only the registered `Type` and constructs a fresh instance per execution, and the executor never disposes it. A tool that cached a transport on `this` would create one per invocation and leak it; the source did exactly that, and its finaliser then failed to release it because it took the non-disposing branch. Own the transport in the singleton, dispose it with the host.
- Per-command breadcrumbs in the environment (`LOAD_RESIDENT_PATH`, `CATALOG_FETCHED_AT`, `TEST_LAST_RESULT`). These survive between invocations *without* environment-modifying rights because the host routes command-name-prefixed keys into the command's private bucket. Note the prefix is the **sub-command** name (`LOAD_`, not `MODEL_LOAD_`), which is a collision risk across packages: keep the names distinctive, and treat the private bucket as a cache, never as a source of truth.

**State this package must not hold.** Secrets of any kind — not in a field, not in the environment, not in a parameter, not in a log line, not in an error message. Settings values (read them, never cache them; the source re-read configuration on every call and that was one of its better decisions). Conversation history. Rendering state. A snapshot of "the active backend" — read `CHATDBG_PROVIDER` each time, so a `SET` from another package is visible immediately.

**Two locks, and the one bounded wait to add.** Generation and model loading are serialised process-wide, exactly as the source did it. Keep that. The one change: the source's waits were unbounded and un-cancellable, so a second caller blocked forever with no feedback. Give both waits a timeout and a status message, and have `MODEL UNLOAD` and `MODEL LOAD` report `waiting for an in-flight generation…` rather than appearing hung. Note also that the source disposed a *static* lock from *instance* teardown — harmless only because exactly one instance ever existed. Do not reproduce that.

**Testability.** Every backend sits behind a factory seam — the source already had `IAzureOpenAIClientFactory` and `IBedrockRuntimeClientFactory`, and they existed purely so tests could avoid the network; keep them and add one for the local engine so `LOAD`/`STATUS`/`UNLOAD` are drivable against a fake. Split the suites by dependency class: one hermetic suite driven by hand-written fakes and a recording HTTP stub, one integration suite that touches the network and is never run in the fast loop. The source's stub *ignored the request object entirely*, which is why not one of its tests asserted a URL, an API version, a header name, a body shape, message ordering, the command-message exclusion or the role mapping. Use a **recording** stub so those become assertions rather than code readings.

**Parameter-handling rules that apply to every tool here.** A command invoked with **zero arguments** receives an empty parameter dictionary: no defaults are applied, no flags are materialised, no field injection happens. Every tool must therefore carry its own fallback (`parameters.TryGetValue(k, out var p) && p.IsValid ? p.GetValue<T>() : fallback`) and must not rely on `DefaultValue` alone. `GetValue<T>` demands `T` equal the declared `DataType` exactly; an `int` parameter must be read as `int`. Return failures as data (`CommandResult<string>.Failure`), never as exceptions — a throw is reduced by the host to `Error executing <CMD> (see trace for more info)`, which is useless to the user. And because parse-time `ArgumentException`s are likewise flattened to that generic line, every range and allow-list in this package is repeated in `ValueDescription` and `Prototype` so the user can see it before they trip it.

**When a capability is unavailable on the current backend.** Report, do not simulate. The single worst behaviour inherited from the source was manufacturing per-token confidence data when the service returned none and returning it with no marker, so a debugging tool presented invented numbers as measurement. The rebuild's contract is: `MODEL CAPS` states what exists; tools that need a capability ask first; a missing capability produces a named, actionable message; and no tool in this package ever synthesises data that looks like a measurement. Where the source *did* synthesise (fabricated log-probabilities on Azure, dead `logprobs`/`top_logprobs` members on every Bedrock request, temperature-derived "probabilities" and a `-1` token-id sentinel in the local analysis records), the rebuild either produces the real value or reports `unknown`.

**Where to degrade rather than fail.**

| Situation | Degrade to |
|---|---|
| No backends loaded at all | `MODEL LIST` reports it and names the recovery command; the shell still starts. A zero-plugin shell is a valid shell |
| Catalog unreachable (offline, no permission, backend has no listing surface) | Show the configured model, mark it unverified, allow selection |
| OS keystore absent (Linux, macOS, or a Windows host with the feature off) | Report `unavailable on this platform`, fall back to process environment then the deprecated settings slot. **Say so** — the source degraded silently while its own documentation told Linux users to enable it |
| GPU backend absent, or fewer devices than requested | Load CPU-only with a warning; never refuse the load |
| A capability probe fails | Report `declared (not probed: <reason>)`. Never upgrade an unproven capability to `verified` |
| A model id cannot be verified | Accept with a warning. Discovery failing must not block configuration |
| Native unload reports remaining references | Warn, record a leak metric, continue |

**Where to fail loudly instead.** A backend that is registered but has no native runtime for this RID must refuse activation, because every subsequent turn would fail. An out-of-range hardware value must be rejected, not clamped. A weight file that does not exist must fail at `SELECT`, not at the first chat turn. And a missing or corrupt integrity store in the host must fail startup rather than silently becoming "allow everything" — that is the host's rule, but this package is the one whose satellite assembly makes it matter.

**Cross-platform posture.** Nothing in the primary assembly is OS-coupled: URL composition, header auth, JSON, and parsing behave identically everywhere. The satellite is coupled to the availability of native binaries per RID, and its accelerator story differs by platform (CUDA/Vulkan on Windows and Linux, Metal on Apple silicon, CPU everywhere). The one inherited Windows-only leg — the OS credential vault — is not in this package at all; it belongs to the credentials package, and this package consumes only a platform-neutral "resolved value plus source label". Remediation text must hide options the running platform cannot offer, which is the one thing the source got exactly right.

**A last framework caveat worth designing around.** A root command invoked with no sub-command, or with an unknown one, produces a poor error on the asynchronous dispatch path the executor actually uses. Have the host map a bare `MODEL` to `HELP MODEL` before dispatch, and make sure the `MODEL` root's description reads as a menu, because for many users that string is the first thing they will see.

---

## 6. ChatDbg.Tools.TokenIntrospection — Token Introspection

> **Root command:** `TOKEN` · **Assembly:** `ChatDbg.Tools.TokenIntrospection.dll` · **Contract:** `Xcaciv.Command.Interface` + `Xcaciv.Command.Core` **3.3.4** (`net10.0`)
>
> This package is the rebuilt product's differentiator. It carries features **7.9 Token Probability Analysis** and **7.10 Token Inspection** in full, plus the derived-statistics layer the source never had.

---

### 6.1 Purpose and boundary

**What this package owns.**

| Owned concern | Source ancestry |
|---|---|
| The **capture policy** — whether per-token confidence is requested at all, and with how many top-K alternatives | `enableLogProbabilities`, `logProbabilitiesTopK` (BR-01, BR-02) |
| **Tokenization** of arbitrary text into an ordered token sequence with vocabulary IDs and character spans | `/tokenize`, `TokenInspectionService.AnalyzePrompt` |
| The **probability map** — the distribution the model weighed at each generation step, chosen token plus top-K runners-up | `/inspect` stage D, `TokenInspectionService.GenerateProbabilityMap` |
| **Attribution** — which span of prompt or prior output influenced a generated token | `/inspect` stage E, `BuildAttributionMap` |
| **Derived statistics** — entropy, perplexity, negative log-likelihood, margin, confidence bands, low-confidence ratio | *no ancestor — the source computed none* |
| **Selection/sampling** — which tokens are presented when the set is large (5-per-segment beginning/middle/end rule) | BR-10, BR-11, BR-12 |
| The **canonical analysis document** — its schema, its identity, its serialization, its round-trip | `TokenAnalysis` / `TokenLogProbability` / `TokenProbabilityMapResult` |
| **Viewing and exporting** an analysis: projection to text, JSON, CSV; writing to and reading from disk | `show-analysis` (stub), `export-analysis` (stub), `LLamaSharpService.SaveAnalysesToJson` |
| The **offline demonstration** dataset | `/demologprobs` |

**What this package explicitly does NOT own.**

| Not owned | Owner | Why the line is here |
|---|---|---|
| Calling a model, holding a session, or parsing a provider's wire format | **`ChatDbg.Tools.Providers`** (root `AI`) — 7.6 / 7.7 / 7.8 | This package consumes an abstraction (`IProbabilitySource`) and never speaks HTTPS, never loads a native inference backend, and never sees a credential. That is what makes it restricted-host-safe (§6.2). |
| Loading GGUF weights, managing a native context, GPU offload | **`ChatDbg.Tools.Providers`** | The source's `TokenInspectionService` loaded the model itself — twice per `/inspect` (Q9) — bypassing the chat path's caching and its concurrency gates. The rebuild delegates. |
| Drawing anything: colour, grid geometry, heat maps, panels, themes, terminal-width probing | **`ChatDbg.Tools.Rendering`** (root `RENDER`) — 7.12 | This package decides *which* tokens and *what* the numbers are; rendering decides what they look like. Confidence **bands** (a named enum) are ours; the green/lime/yellow/orange/red palette is theirs. |
| Persisting settings to `~/.ChatDbg/settings.json` | **`ChatDbg.Tools.Settings`** (root `CONFIG`) — 7.2 | We write the process environment; durable persistence is one owner, one file, one writer. |
| Storing conversation turns, `/import`, `/export` of chat history | **`ChatDbg.Tools.History`** (root `CHAT`) — 7.4 | An analysis references a message by id; it does not contain the conversation. |
| Capturing the native library's log stream, `export-logs` | **`ChatDbg.Tools.Diagnostics`** (root `LOG`) — 7.11 | The source's `ExportLogsCommand` sat beside `ExportTokenAnalysisCommand` and shared a test class; they are separate concerns and separate packages here. |
| Credentials, endpoints, API keys of any kind | **`ChatDbg.Tools.Credentials`** — 7.3 | **No tool in this package accepts, reads, stores, emits or logs a secret.** See §6.2. |

**The one-sentence boundary:** *this package turns a model's token-level uncertainty into a document, and turns that document into numbers, filters, files and text — it never produces the uncertainty and never paints it.*

---

### 6.2 Package manifest

| Property | Value |
|---|---|
| **Assembly name** | `ChatDbg.Tools.TokenIntrospection` |
| **Root command** | `TOKEN` (uppercased by `NamesValidator.GetValidCommandName`; every tool carries `[CommandRoot("TOKEN", "Token-level introspection of model output")]`) |
| **Contract assembly version** | `Xcaciv.Command.Interface` **3.3.4** and `Xcaciv.Command.Core` **3.3.4** — the shipped csproj `<Version>`, not the `3.3.0`/`3.3.3`/`3.2.2` figures in the framework's own docs. Nothing else from the framework is referenced. **The plugin DLL must not ship a private copy of `Xcaciv.Command.Interface`** — `Crawler` catches `ReflectionTypeLoadException` and reports precisely that cause, and the package is skipped silently. |
| **Target framework** | `net10.0` (framework default). Build with `/p:UseNet08=true` only if the host is pinned to `net8.0`; the package has no `net10.0`-only API dependency. |
| **Elevated trust** | **No.** No tool is registered with `modifiesEnvironment: true`. Every key this package writes is prefixed `TOKEN_`, which `CommandController.Run` routes into the package's own private environment bucket without any elevation. |
| **Network** | **None.** No tool opens a socket. Provider traffic belongs to `ChatDbg.Tools.Providers`. |
| **Filesystem** | **Two tools only** — `TOKEN EXPORT` (write) and `TOKEN LOAD` (read), both confined to the directory named by `TOKEN_EXPORTDIR`. No tool accepts an absolute path on the command line (see §6.3.10 for why that is impossible, not merely discouraged). All other tools are filesystem-free. |
| **OS keystore** | **Never.** No credential surface at all. |
| **Native libraries** | **None.** The package is pure managed, trim-safe and AOT-safe: no `Reflection.Emit`, no `Expression.Compile`, no late binding. This is a deliberate correction of QUIRK-Q14, where the source's reflection-based logit accessor failed **silently** under the project's own trimmed/AOT publish configuration and reported 100 % confidence in everything. |
| **Safe to load in a restricted host** | **Yes.** It passes `AssemblySecurityPolicy.Strict` preflight (no dynamic-code constructs), needs no wildcard `basePathRestriction`, and is safe to run under `learningMode: false` integrity verification. Under a restricted host with no `IProbabilitySource` registered, `TOKEN SPLIT` / `MAP` / `INSPECT` fail with a stated reason; `TOKEN DEMO`, `LOAD`, `FILTER`, `STATS`, `SHOW`, `EXPORT`, `CAPTURE` and `DIAG` remain fully functional. |
| **Host services required** | A single shared contract assembly, `ChatDbg.Introspection.Abstractions`, loaded in the **default** load context and referenced by both host and package, exposing `ITokenizer`, `IProbabilitySource`, `IAttributionModel`, `IAnalysisStore` and a static `IntrospectionServices` registry the host populates at startup. This indirection is mandatory: `CommandFactory` activates plugin commands through `AssemblyContext.ActivateInstance<ICommandDelegate>`, which uses the **public parameterless constructor only** — there is no DI injection into an ALC-loaded plugin command. |
| **Audit sensitivity** | **Medium — content, not credentials.** `AuditEvent.Parameters` is `ioContext.Parameters` verbatim, and `AuditMaskingConfiguration.ApplyMasking` only rewrites `-name=value` tokens, which this framework's `-name value` syntax never produces. Therefore **any prompt text typed as a command-line argument is written to the audit log in the clear.** See §6.5 for the mitigation. |

**Environment keys.** All are read with the `TOKEN_` prefix applied by `ControllerEnvironmentContext.GetChild("TOKEN")`; all are declared unprefixed in `GetDefaultEnvironment()`.

| Key (as read) | Default | Range | Written by | Source ancestry |
|---|---|---|---|---|
| `TOKEN_CAPTURE` | `false` | `true`/`false` | `TOKEN CAPTURE` | `enableLogProbabilities`, default **false** (BR-01) |
| `TOKEN_TOPK` | `5` | **1–20 inclusive** | `TOKEN CAPTURE` | `logProbabilitiesTopK`, default **5** (BR-02) |
| `TOKEN_MODE` | `sample` | `sample`\|`all` | `TOKEN CAPTURE`, `TOKEN SHOW` | `showAllTokens`, default **false** ⇒ sampled (BR-03) |
| `TOKEN_LAYOUT` | `list` | `list`\|`grid` | `TOKEN CAPTURE`, `TOKEN SHOW` | `gridViewForTokens`, default **false** ⇒ list (BR-04) |
| `TOKEN_GRIDMAXALT` | `5` | **1–20 inclusive** | `TOKEN CAPTURE`, `TOKEN SHOW` | `gridViewMaxAlternatives`, default **5** (BR-05) |
| `TOKEN_SAMPLESIZE` | `5` | 1–100 | `TOKEN CAPTURE` | **NEW** — the source hard-coded 5 in four places (BR-10) |
| `TOKEN_MAXGEN` | `10` | 1–8192 | `TOKEN CAPTURE` | `min(10, maxTokens)` (M3, M5) |
| `TOKEN_CONTEXT` | `0` (`0` = use the provider's configured context) | 0 or 512–32768 | `TOKEN CAPTURE` | **NEW** — replaces the hard-coded 512/2048 (M1, M2, Q8) |
| `TOKEN_LOWCONF` | `0.30` | 0.0–1.0 | `TOKEN CAPTURE` | **NEW** — the source's lowest colour-band boundary (BR-22) reused as a threshold |
| `TOKEN_EXPORTDIR` | platform application-data path (§6.6) | absolute directory | host / `TOKEN CAPTURE` | **NEW** — the source accepted arbitrary paths on the command line |
| `TOKEN_LAST` | *(unset)* | analysis id | every analysis-producing tool | **NEW** — replaces the source's single mutable "last generation" list |
| `TOKEN_KEEP` | `8` | 1–64 | `TOKEN CAPTURE` | **NEW** — bound on the in-process analysis ring |

**Global (unprefixed) keys this package *reads only*,** owned by `ChatDbg.Tools.Settings` and written by a command registered `modifiesEnvironment: true`: `PROVIDER`, `MODELID`, `TEMPERATURE`, `MAXTOKENS`. This is the only cross-package configuration channel that exists — a command's private bucket is invisible to every other command, so anything two packages must agree on has to be global.

---

### 6.3 Tool catalog

Thirteen tools. Eight are ported; five are marked **NEW** and each states why it earns its place.

Conventions that hold for **every** tool in the package and are therefore stated once:

- **Parameter attributes go on the class**, never on members (`AttributeTargets.Class, AllowMultiple = true`). Parameter names normalise to **lowercase**; command names to **UPPERCASE**.
- **Almost nothing is declared `IsRequired`.** An `ArgumentException` raised inside `CommandParameters` is caught by `CommandExecutor` and reduced to `Error executing TOKEN (see trace for more info)` — the user never sees *which* parameter was wrong. Every tool therefore declares ordered parameters with `IsRequired = false` and validates in the body, returning `CommandResult<string>.Failure` with a message the user can act on. This is how the source's exact strings (`Top-K value must be a number between 1 and 20`) survive into the rebuild.
- **`AllowedValues` is declared only where a generic error is acceptable.** Where the source pins an exact message, the candidate list lives in `ValueDescription` and validation is in-body. Where `AllowedValues` *is* declared, the first entry is chosen to match the source's default, because `AllowedValues[0]` silently becomes `DefaultValue` when no default is set.
- **Zero arguments ⇒ zero parameter processing.** `AbstractCommand.ProcessParameters` returns an empty dictionary immediately when `io.Parameters.Length == 0`: no defaults applied, no flags materialised, no field injection. Every tool's bare form is therefore designed to be meaningful with no parameters at all (usually "report status" or "operate on the last analysis").
- **A failed upstream chunk never reaches `HandlePipedChunk`.** `AbstractCommand.Main` forwards failures verbatim and skips empty successes. So every tool in this package propagates downstream errors correctly without writing a line of code for it, and a failure travels to the end of the pipeline and out to the user while later stages keep running.
- **Unknown chunk kinds are passed through unchanged.** Any tool that receives an analysis-document chunk whose `kind` it does not understand re-emits it verbatim. This is what lets new record kinds be added without breaking existing pipelines.
- **`OutputFormat` is metadata.** No framework code branches on it; the host's IO context does. Tools set it in the constructor, or reassign it in `HandleExecution` before building results when a `-format` parameter changes the shape.
- **Reading environment is a pure read.** Every `env.GetValue(...)` call passes `storeDefault: false`; the default `true` would mark the child environment changed and trigger a write-back on every invocation.

#### The analysis document (the wire and file format)

One chunk = one JSON object = one line. `ResultFormat.JSON`. A well-formed analysis stream is exactly one `analysis` chunk, then N `token` chunks in ascending `index`, then zero or more `attribution` chunks, then zero or one `stats` chunk.

| Record | Fields |
|---|---|
| `analysis` | `kind`, `analysisId` (short, URL-safe, 12 chars), `schemaVersion` (`1`), `createdUtc`, `source` (`live`\|`demo`\|`file`), `synthetic` (bool), `provider`, `modelId`, `topK`, `temperature`, `contextSize`, `promptTokenCount`, `generatedTokenCount`, `truncatedTopK` (always `true` for live data — alternatives are the top-K, not the vocabulary), `prompt` (present unless `-redact`) |
| `token` | `kind`, `analysisId`, `index` (**0-based**), `step`, `tokenId` (`-1` = unavailable), `text`, `charStart`, `charEnd` (`-1`/`-1` = unknown, never the source's `n/a` string), `logprob` (natural log, `null` when unavailable), `probability` (`exp(logprob)`, 0–1), `entropy`, `normalizedEntropy`, `margin`, `band`, `alternatives[]`, `synthetic`, `estimated` |
| `alternatives[]` element | `tokenId`, `text`, `logprob`, `probability`. Nested alternatives are always absent (BR-19). Ordered **descending by probability** — the source imposed ordering in exactly one surface (BR-33) and trusted arrival order everywhere else; the rebuild normalises once, at the producer. |
| `attribution` | `kind`, `analysisId`, `tokenIndex`, `tokenId`, `tokenText`, `influencingText`, `influenceSpan` (`{start,end}` or `null`), `influenceScore` (0.0–1.0), `method` (`recency-window`\|`attention`\|`gradient`) |
| `stats` | `kind`, `analysisId`, `n`, `meanProbability`, `medianProbability`, `minProbability`, `maxProbability`, `meanNegLogLikelihood`, `perplexity`, `meanEntropy`, `meanNormalizedEntropy`, `meanMargin`, `bands` (`{veryHigh,high,medium,low,veryLow}` counts), `lowConfidenceCount`, `lowConfidenceRatio`, `threshold`, `entropyUnit` (`nats`\|`bits`), `entropyIsTruncated` (always `true` for live top-K data) |

**Indexing rule, stated once for the whole product:** the document is **0-based**; every human-facing rendering is **1-based**. The source had grid cards, both table renderers and the plain-text report numbering from 1 while the terminal-UI panel numbered from 0 (QUIRK-Q15) — the same token was `#7` in one view and `6:` in another. One rule, applied everywhere.

**Probability scale rule, stated once:** probabilities are **0–1 internally**, formatted to a percentage only at the display edge. The source carried three incompatible scales simultaneously, producing `0.92%` for a 92 %-confident token and a doubled `%%` in several formatters (QUIRK-Q3). Confidence bands keep the source's boundaries, renormalised: **≥ 0.90 `very-high`, ≥ 0.70 `high`, ≥ 0.50 `medium`, ≥ 0.30 `low`, else `very-low`**, evaluated top-down, inclusive at the lower bound (BR-22).

**No-fabrication rule, stated once — owner-ratified as decision D-001 (2026-08-29, `DECISIONS.md`):** no tool in this package ever invents data and presents it as measured. When a provider returns no probabilities, the analysis carries zero `token` chunks and the `analysis` chunk says why; it does not silently substitute a synthetic list as the source's cloud adapter did (BR-50, verified by a test that pinned the fabrication). `TOKEN DEMO` is the sole fabricating tool, every record it emits is stamped `synthetic: true`, and every downstream tool carries that stamp forward.

D-001 adds the *capability-absence* behaviour on top of the no-fabrication rule: when capture is **on** and the active backend's declared capability record (`MODEL CAPS`) says it **cannot** supply token log probabilities, the product **turns capture off** (`TOKEN_ENABLED` → `false`, persisted) and emits the auto-disable notice — `Token log probabilities disabled: provider '{provider}' does not support them. Use a provider that does (see MODEL CAPS).` — naming a configured capable provider where one exists. This fires on backend switch (`MODEL USE` owns it, §5) and at session start (the host owns it, §A.3); it never fires on a *transient* absence from a backend that declares the capability, which instead produces the honest two-line "none were returned" notice. In the full-screen shell the enabling control is rendered disabled with the same explanation while a capability-absent backend is active, and probability-view attempts produce a **non-blocking** transient status notice, never a modal.

---

#### 6.3.1 `TOKEN CAPTURE` — capture policy

| | |
|---|---|
| **Command** | `CAPTURE` |
| **Root** | `TOKEN` |
| **Description** | `Configure per-token confidence capture` |
| **Prototype** | `TOKEN CAPTURE [status\|on\|off] [-topk 1-20] [-mode sample\|all] [-layout list\|grid] [-gridmaxalt 1-20] [-samplesize n] [-maxgen n] [-lowconf 0.0-1.0] [-print]` |

```csharp
[CommandRoot("TOKEN", "Token-level introspection of model output")]
[CommandRegister("Capture", "Configure per-token confidence capture",
    Prototype = "TOKEN CAPTURE [status|on|off] [-topk 1-20] [-mode sample|all] "
              + "[-layout list|grid] [-gridmaxalt 1-20] [-samplesize n] [-maxgen n] "
              + "[-lowconf 0.0-1.0] [-print]")]
[CommandParameterOrdered("action", "status | on | off  (bare form reports status)", IsRequired = false)]
[CommandParameterNamed("topk", "Alternatives requested per position (1-20)", DataType = typeof(int))]
[CommandParameterNamed("mode", "Which tokens later views present", AllowedValues = new[] { "sample", "all" })]
[CommandParameterNamed("layout", "Preferred presentation", AllowedValues = new[] { "list", "grid" })]
[CommandParameterNamed("gridmaxalt", "Alternatives per grid card (1-20)", DataType = typeof(int))]
[CommandParameterNamed("samplesize", "Tokens per sampled segment", DataType = typeof(int))]
[CommandParameterNamed("maxgen", "Token budget for TOKEN MAP", DataType = typeof(int))]
[CommandParameterNamed("lowconf", "Low-confidence threshold", DataType = typeof(double))]
[CommandFlag("print", "Emit the resulting policy as key=value lines", ShortAlias = "p")]
[CommandHelpRemarks("Bare 'TOKEN CAPTURE' reports the current policy and changes nothing.")]
[CommandHelpRemarks("Policy lives in this package's private environment bucket; pipe -print into CONFIG SET to persist it.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `action` | ordered | `string` | no | *(absent ⇒ status)* | `status`, `on`, `off` — **validated in-body**, not via `AllowedValues`, so the source's `Unknown subcommand: {arg}. ` message survives | What to do |
| `topk` | named | `int` | no | `5` | **1–20 inclusive** (BR-02) | Alternatives requested per position |
| `mode` | named | `string` | no | `sample` | `sample`, `all` — `sample` first so it becomes the auto-default (BR-03) | Which tokens later views present |
| `layout` | named | `string` | no | `list` | `list`, `grid` — `list` first (BR-04) | Preferred presentation |
| `gridmaxalt` | named | `int` | no | `5` | **1–20 inclusive** (BR-05) | Alternatives per grid card |
| `samplesize` | named | `int` | no | `5` | 1–100 — **NEW** (source hard-coded 5, BR-10) | Tokens per sampled segment |
| `maxgen` | named | `int` | no | `10` | 1–8192 — source used `min(10, maxTokens)` (M5) | Token budget for `TOKEN MAP` |
| `lowconf` | named | `double` | no | `0.30` | 0.0–1.0 — **NEW** (BR-22 boundary) | Low-confidence threshold |
| `print` | flag | `bool` | n/a | `false` | presence ⇒ true | **NEW** — emit policy as `key=value` for piping |

**Pipeline behaviour.** Neither source nor sink by default: bare and mutating forms return `Success(string.Empty)`, which the host drops — the same silence the built-in `SET` uses. With `-print` it becomes a **source**, emitting one `key=value` line per policy value (`ResultFormat.General`). It accepts piped input only in the `-print`-less mutating form, where one chunk is one `key=value` line applying one policy change, so `CONFIG GET -section token | TOKEN CAPTURE` restores a saved policy.

**Environment.** Reads and writes all eleven `TOKEN_*` keys. Reads global `PROVIDER`/`MODELID` for the status report only. **Needs no environment-modifying permission** — every key it writes is prefixed with its own root command name, so `CommandController.Run` routes it into the `TOKEN` bucket automatically.

**Failure modes.**

| Condition | Result |
|---|---|
| `-topk 0`, `-topk 21`, `-topk abc`, `-topk 1.5` | Failure, message exactly `Top-K value must be a number between 1 and 20`. No state change, no write. (Preserves BR-02's reject-don't-clamp semantics; the source's settings dialog clamped instead, an inconsistency the rebuild deliberately drops in favour of rejection — see §6.5.) |
| `-gridmaxalt` out of range | Failure, `Grid max alternatives value must be a number between 1 and 20` |
| `-topk` as the final token with no value | The framework throws `ArgumentOutOfRangeException` inside `CommandParameters` **before** the tool runs; the user sees `Error executing TOKEN (see trace for more info)`. Mitigated by naming the value in the prototype and by a help remark; it cannot be fixed from inside the tool. |
| Unknown `action` | Failure, `Unknown subcommand: {arg}. ` followed by the valid-option list — the trailing space after the period is preserved verbatim from the source |
| `on` while the active backend's declared capability record says it cannot supply token log probabilities | **Refusal (D-001):** failure with exactly `Cannot enable token log probabilities: provider '{provider}' does not support them. Switch providers first (see MODEL CAPS).` No state change, no write. The full-screen shell's equivalent control is disabled with this same text as its explanation. |
| `-lowconf` outside 0.0–1.0 | Failure, `Low-confidence threshold must be between 0.0 and 1.0` |
| Piped chunk not of the form `key=value` | That chunk fails with `Not a policy assignment: {chunk}`; the failure travels downstream and later chunks are still processed |

**Security and audit.** No secret in any parameter or output. `-print` emits policy values only — never the model id, endpoint or provider credentials. Non-destructive; no confirmation required.

**Traceability.** **7.9 Token Probability Analysis** (and 7.2 for the persisted keys). Descends from `/logprobs`, `/logprobs enable|disable|top <n>|showall|showsample|grid|list|gridmaxalt <n>` and the equivalent `/set enableLogProbabilities|logProbabilitiesTopK|showAllTokens|gridViewForTokens|gridViewMaxAlternatives` keys with their aliases `logprobs`, `logtopk`, `tokensgrid`, `gridmaxalt`. `-samplesize`, `-maxgen`, `-lowconf` and `-print` are **NEW**.

---

#### 6.3.2 `TOKEN DIAG` — capture diagnostics

| | |
|---|---|
| **Command** | `DIAG` |
| **Root** | `TOKEN` |
| **Description** | `Explain why token confidence is or is not available` |
| **Prototype** | `TOKEN DIAG [-verbose]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `verbose` | flag | `bool` | n/a | `false` | presence ⇒ true | Include backend capability probe details |

**Pipeline behaviour.** Source only. Emits `ResultFormat.General` lines; with `-verbose`, a trailing `ResultFormat.JSON` capability record. Ignores piped input (declares no `UsePipe` parameter and returns its report unchanged per chunk would be meaningless, so it overrides `Main` to emit its report once and drain any input pipe without acting on it).

**Environment.** Reads all `TOKEN_*` policy keys plus globals `PROVIDER`, `MODELID`. Writes nothing.

**Failure modes.** Cannot fail on input. When no `IProbabilitySource` is registered it reports `Backend: none registered — TOKEN SPLIT/MAP/INSPECT are unavailable in this host` and still succeeds, because the whole purpose of the tool is to explain absence.

**Security and audit.** The report names the provider and model but **masks the endpoint host to its scheme and registrable domain** and never prints a key, token or region credential. This is a deliberate tightening: the source's `/logprobs debug` echoed the configured Azure endpoint verbatim.

**Traceability.** **7.9**. Descends from `/logprobs debug`. One correction is deliberate: the source's static troubleshooting text advised an API version (`2023-05-15`) that the code did not send (`2023-12-01-preview`) and that predates the parameter it relies on (QUIRK-Q6). `TOKEN DIAG` asks the registered `IProbabilitySource` for the version and capability set it will actually use and prints that; it holds no hard-coded provider knowledge.

---

#### 6.3.3 `TOKEN SPLIT` — tokenize text

| | |
|---|---|
| **Command** | `SPLIT` |
| **Root** | `TOKEN` |
| **Description** | `Split text into model tokens with IDs and character spans` |
| **Prototype** | `TOKEN SPLIT ["text"] [-bos\|-nobos] [-context n] [-format json\|text]` |

```csharp
[CommandRoot("TOKEN", "Token-level introspection of model output")]
[CommandRegister("Split", "Split text into model tokens with IDs and character spans",
    Prototype = "TOKEN SPLIT [\"text\"] [-bos|-nobos] [-context n] [-format json|text]")]
[CommandParameterNamed("context", "Context window for the tokenizer load (0 = provider default)",
    DataType = typeof(int), DefaultValue = "0")]
[CommandParameterNamed("format", "Output shape", AllowedValues = new[] { "json", "text" })]
[CommandFlag("nobos", "Do not prepend the beginning-of-sequence token")]
[CommandParameterSuffix("text", "Text to tokenize", IsRequired = false, UsePipe = true)]
[CommandHelpRemarks("Quote the text. The argument tokenizer splits unquoted '.', '/' and ':' into separate words.")]
[CommandHelpRemarks("Piped text is tokenized one chunk at a time; each chunk becomes its own analysis.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `text` | suffix | `string` | no (in-body check) | *(none)* | any text; **all remaining tokens joined with single spaces into one string** | Text to tokenize. `UsePipe = true` — omitted from parsing when piped |
| `nobos` | flag | `bool` | n/a | `false` | presence ⇒ true | **NEW** — suppress the BOS marker the source always prepended |
| `context` | named | `int` | no | `0` | `0`, or 512–32768 | **NEW** — replaces the source's hard-coded 512 (M1, Q8); `0` means "use the provider's configured `llamaContextSize`", which the source ignored entirely |
| `format` | named | `string` | no | `json` | `json`, `text` | `json` emits the analysis document; `text` emits the source's fixed-width `Index \| Token ID \| Token Text` table |

**Pipeline behaviour.** **Both.** As a source it overrides `Main` to emit one `analysis` chunk followed by one `token` chunk per token — `AbstractCommand`'s non-piped path can emit only a single chunk, so multi-chunk emission requires the override, which the framework explicitly permits. As a filter, **one piped chunk is one complete text to tokenize**, producing its own analysis id; a 20-line piped document therefore yields 20 independent analyses, which is exactly what you want for `CHAT EXPORT | TOKEN SPLIT | TOKEN STATS`. Declares `ResultFormat.JSON` (or `General` under `-format text`) because downstream tools parse the records rather than reading them.

**Environment.** Reads `TOKEN_CONTEXT`, `TOKEN_KEEP`, `TOKEN_LAST` and global `MODELID`, `PROVIDER`. Writes `TOKEN_LAST` (its own bucket). No environment-modifying permission needed.

**Failure modes.**

| Condition | Result |
|---|---|
| No text and no pipe | Failure: `Please provide text to split. Usage: TOKEN SPLIT "<text>"` (the source's `Please provide text to tokenize.` wording, adapted to the new name) |
| No `ITokenizer` registered in the host | Failure: `No tokenizer is available in this host. Register a local inference backend, or use TOKEN DEMO for sample data.` — **never** a heuristic fallback presented as real |
| Provider is not a tokenizing provider | Failure: `Tokenization requires a local model. Set a local provider with 'CONFIG SET provider llama'.` The source gated on the exact lowercase string `"llama"`; the rebuild gates on a *capability* the provider advertises, so a second local backend needs no code change here |
| Model not configured, or the model file does not exist | Failure: `Model not configured or file not found. Use 'CONFIG SET modelId <path-to-model>' first.` (preserved wording) |
| Backend throws mid-load | Failure: `Error splitting text: {message}` — the source's double-prefixing (`Error during token inspection: Error analyzing prompt: <root>`) is deliberately collapsed to one layer |
| Piped chunk is empty | Skipped by `AbstractCommand.Main` before the tool sees it |
| Upstream failure chunk | Forwarded verbatim; the tool is not invoked for it |

**Security and audit.** ⚠️ **The `text` suffix parameter is content-bearing and is written verbatim into `AuditEvent.Parameters`.** The framework's masking cannot help. Mitigation, in order of preference: (1) pipe text in — piped payloads are not part of `AuditEvent.Parameters`; (2) the host registers an `IAuditMaskingConfiguration` whose `RedactedParameterNames` includes `text`, accepting that it only fires for `-name=value` forms; (3) the host disables audit parameter capture for this root. This package declares `text`, `prompt` and `note` as its content-bearing parameter names so a host can honour (2) mechanically. Non-destructive; no confirmation required.

**Traceability.** **7.10 Token Inspection**. Descends from `/tokenize` (`TokenizeCommand`) and `TokenInspectionService.AnalyzePrompt`. Two source behaviours are deliberately **not** preserved: the placeholder text `<token_{id}>` (the source never decoded a token to a string — QUIRK-Q5; the rebuild requires `ITokenizer.Decode` and reports a real string, falling back to `<token_{id}>` only when the backend genuinely has no decoder, in which case `estimated: true` is set on every record), and the uniform-distribution character-span estimator, which produced spans "that always *look* like a clean segmentation even though they bear no relation to real token boundaries". The rebuild emits real spans when the backend supplies them and `charStart = charEnd = -1` when it does not. `-nobos`, `-context` and `-format` are **NEW**.

---

#### 6.3.4 `TOKEN MAP` — probability map

| | |
|---|---|
| **Command** | `MAP` |
| **Root** | `TOKEN` |
| **Description** | `Map the probability distribution the model weighed at each generation step` |
| **Prototype** | `TOKEN MAP ["prompt"] [-n tokens] [-topk 1-20] [-context n] [-temperature t] [-seed s]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `prompt` | suffix | `string` | no (in-body check) | *(none)* | joined remaining tokens; `UsePipe = true` | Prompt to continue |
| `n` | named | `int` | no | `10` (`TOKEN_MAXGEN`) | 1–8192; the source capped at `min(10, maxTokens)` regardless of the 1000 default (M3, M5) | Tokens to generate |
| `topk` | named | `int` | no | `5` (`TOKEN_TOPK`) | **1–20 inclusive** (BR-02). Note the source's own inspection service applied *no* clamp here at all (Q14), accepting 0 and negative values; the rebuild applies the same 1–20 rule everywhere | Alternatives per position |
| `context` | named | `int` | no | `0` | `0`, or 512–32768 | **NEW** — replaces the hard-coded 2048 (M2, Q8) |
| `temperature` | named | `double` | no | global `TEMPERATURE` | 0.0–2.0 | Sampling temperature for this run only; does not mutate global settings |
| `seed` | named | `long` | no | *(backend default)* | any | **NEW** — makes a run reproducible; the source's stage D was non-deterministic while printing deterministic fabricated numbers |

**Pipeline behaviour.** **Both.** Source: overrides `Main`, emitting one `analysis` chunk then one `token` chunk per generated step, streamed as generation proceeds so a downstream `TOKEN FILTER | TOKEN SHOW` starts printing before the model finishes. Filter: one piped chunk is one prompt. `ResultFormat.JSON`. **Backpressure note:** at one chunk per token with the framework's default `MaxChannelQueueSize` of 10 000 and `BackpressureMode.Block`, a long generation blocks rather than dropping — the correct choice, and the host should not switch this root to `DropOldest`.

**Environment.** Reads `TOKEN_TOPK`, `TOKEN_MAXGEN`, `TOKEN_CONTEXT`, `TOKEN_KEEP`, globals `PROVIDER`, `MODELID`, `TEMPERATURE`, `MAXTOKENS`. Writes `TOKEN_LAST`.

**Failure modes.** Same preconditions and messages as `TOKEN SPLIT`, plus:

| Condition | Result |
|---|---|
| Provider supports generation but not per-token probabilities | Succeeds, emits the `analysis` chunk with `truncatedTopK: false`, `generatedTokenCount` set, and `token` chunks whose `logprob` is `null` and `estimated: false`. A companion note chunk carries the source's exact text: `Note: Log probabilities were requested but none were returned by the model.` / `This could be due to the model not supporting this feature or an API limitation.` **No data is fabricated.** |
| Generation produces nothing | Succeeds with an `analysis` chunk and zero `token` chunks; `TOKEN STATS` downstream reports `n: 0` rather than dividing by zero |
| Backend throws mid-generation | Partial stream already emitted, then one failure chunk `Error generating probability map: {message}`. Chunks already downstream are not retracted — the analysis is marked `partial: true` in a trailing record |
| Model in use by another generation | The provider abstraction serialises; the tool reports `Waiting for the model…` via `io.SetStatusMessage` and does not spin. The source's `/inspect` bypassed the chat path's semaphores entirely and could attempt a concurrent native load |

**Security and audit.** `prompt` is content-bearing — see §6.3.3. Non-destructive; consumes model time and, for a hosted provider, **incurs cost** — the tool calls `io.SetProgress(n, step)` so the host can show what it is spending. No confirmation required (generation is not irreversible), but a host may choose to gate it.

**Traceability.** **7.10** (and 7.9 for the top-K contract). Descends from `TokenInspectionService.GenerateProbabilityMap` and `/inspect` stage D. The decisive correction: the source **fabricated every number** in this stage — token IDs `1000 + i`, a constant log-probability of `-2.5` on every token, and exactly `topK` alternatives named `{word}_alt{j+1}` with log-probabilities `-3.0 - j` — while running real inference and presenting the result as measurement (QUIRK-Q3). `TOKEN MAP` reports what the backend measured or reports nothing. `-context`, `-temperature` and `-seed` are **NEW**.

---

#### 6.3.5 `TOKEN ATTRIBUTE` — influence attribution

| | |
|---|---|
| **Command** | `ATTRIBUTE` |
| **Root** | `TOKEN` |
| **Description** | `Attribute each generated token to the input span that influenced it` |
| **Prototype** | `TOKEN ATTRIBUTE [-method recency\|attention\|gradient] [-window n] [-prompttail n] [-cutover n] [-id analysisid]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `method` | named | `string` | no | `recency` | `recency`, `attention`, `gradient` — `recency` first so it is the auto-default | Attribution model. `attention`/`gradient` require a backend that exposes them; otherwise the tool refuses rather than silently downgrading |
| `window` | named | `int` | no | `5` | 1–512 | Look-back window for `recency` (M12: the source concatenated tokens `[max(0, i-5), i)`) |
| `prompttail` | named | `int` | no | `50` | 1–4096 | Characters of prompt tail used for early tokens (M11) |
| `cutover` | named | `int` | no | `3` | 0–1024 | Token index at which attribution switches from prompt-tail to look-back window (M10) |
| `id` | named | `string` | no | `TOKEN_LAST` | 12-char analysis id | Analysis to attribute when not piped |

**Pipeline behaviour.** **Both, and filter-shaped by design.** Inherits `AbstractCommand`: one piped chunk in, one chunk out. `analysis` chunks pass through unchanged; `token` chunks pass through and are buffered (`OnStartPipe` clears the buffer, so the tool is safe even if the host registered it as a DI singleton); at `OnEndPipe` the accumulated attribution cannot be yielded — so the tool overrides `Main` to interleave, emitting each `token` chunk followed immediately by its `attribution` chunk once the look-back window is satisfiable. Non-piped, it loads the analysis named by `-id` (or `TOKEN_LAST`) from the in-process store and emits the same stream. `ResultFormat.JSON`.

**Environment.** Reads `TOKEN_LAST`. Writes nothing.

**Failure modes.**

| Condition | Result |
|---|---|
| Not piped and no `-id` and `TOKEN_LAST` unset | Failure: `No analysis to attribute. Run TOKEN MAP first, or pipe an analysis in.` (The source's docs implied a "no analyses available" message that no code ever produced.) |
| `-id` names an analysis that has aged out of the ring | Failure: `Analysis '{id}' is no longer in memory. Re-run it, or load it with TOKEN LOAD.` |
| `-method attention` on a backend with no attention export | Failure: `Attribution method 'attention' is not available from the current backend. Available: recency.` **The tool never silently substitutes a weaker method.** |
| `-window 0`, `-cutover -1` etc. | Failure naming the parameter and its range |
| Upstream failure chunk | Forwarded verbatim |

**Security and audit.** `influencingText` is a verbatim slice of the prompt and therefore content-bearing; it is subject to the same `-redact` treatment in `TOKEN EXPORT`. Non-destructive.

**Traceability.** **7.10**. Descends from `/inspect` stage E and `TokenInspectionService.BuildAttributionMap`. Behaviours preserved: the 3-token cut-over, the 50-character prompt tail, the ≤5-token look-back window, one entry per generated token with `attributionIndex == tokenIndex`, and the guarantee that the attribution list is the same length as the generated-token list. Behaviours corrected: the source stamped a **constant `InfluenceScore = 0.8`** on every entry, labelled in its own comments as a "Placeholder for a real influence calculation" (M13). The rebuild makes `recency` report a genuine recency weight normalised to sum to 1.0 across the window, and marks `method` on every record so a reader can never mistake a heuristic for a measurement. The source also truncated the influencing text to 37 characters + `...` **before** escaping, splitting escape sequences (M14); the rebuild carries the full text in the document and lets `TOKEN SHOW` truncate at the display edge.

---

#### 6.3.6 `TOKEN INSPECT` — the composite run

| | |
|---|---|
| **Command** | `INSPECT` |
| **Root** | `TOKEN` |
| **Description** | `Tokenize, generate, map probabilities and attribute in one run` |
| **Prototype** | `TOKEN INSPECT ["text"] [-n tokens] [-topk 1-20] [-yes] [-stopafter split\|map\|attribute]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `text` | suffix | `string` | no (in-body check) | *(none)* | joined remaining tokens; `UsePipe = true` | Text to inspect |
| `n` | named | `int` | no | `10` | 1–8192 | Tokens to generate (`min(10, maxTokens)` in the source, M5) |
| `topk` | named | `int` | no | `5` | 1–20 | Alternatives per position |
| `yes` | flag | `bool` | n/a | `false` | presence ⇒ true | Skip the generation-consent prompt |
| `stopafter` | named | `string` | no | `attribute` | `split`, `map`, `attribute` — `attribute` cannot be first (it would become the auto-default correctly, and it is the source's full behaviour) | Where to stop |

**Pipeline behaviour.** Source and filter, identical to `TOKEN MAP` but emitting the union of all three stages' records for one analysis id. `ResultFormat.JSON`.

**The consent gate — the one behaviour that most needed rebuilding.** The source printed `Do you want to continue with probability analysis? This will generate tokens and analyze their probabilities. (y/n)` and **blocked on `Console.ReadLine()`**, which has no console under a full-screen terminal UI and no answerer in a pipeline (Q7). The rebuild:

1. When `io.HasPipedInput == false` **and** `-yes` was not given: `await io.PromptForCommand("Continue with generation? (y/n)")`. Only `y`/`yes` (case-insensitive, invariant) proceed — every other answer, including empty and end-of-input, stops after the split stage and still returns success, preserving the source's semantics exactly.
2. When `io.HasPipedInput == true`: **never prompt.** `PromptForCommand` is contractually meaningful only when not piped. Without `-yes`, the tool stops after the split stage and emits one note chunk: `Generation skipped: pass -yes to generate inside a pipeline.`
3. `-stopafter split` makes the gate moot and is the scriptable form.

**Environment.** Union of `TOKEN SPLIT`, `TOKEN MAP` and `TOKEN ATTRIBUTE`. Writes `TOKEN_LAST` once, for the single composite analysis.

**Failure modes.** The union of the three stages', with one structural difference from the source: the model is loaded **once**, not twice. The source ran a 512-context load for tokenization and a separate 2048-context load for generation with a full unload between (Q9), making a multi-gigabyte model pay twice per command. Additionally, where the source returned `success` from a declined consent gate in a way indistinguishable from a completed analysis, the rebuild's trailing `analysis` record carries `stoppedAfter: "split"` so a consumer can tell.

**Security and audit.** `text` is content-bearing (§6.3.3). Generation may incur provider cost. Non-destructive.

**Traceability.** **7.10**. Descends from `/inspect` (`InspectCommand`), preserving its precondition ordering — argument count is checked **before** provider, which is checked **before** model file, so a bare `TOKEN INSPECT` on a perfectly configured llama setup still reports the argument error. `-yes` and `-stopafter` are **NEW**; they exist because the source's gate made the command unusable non-interactively.

---

#### 6.3.7 `TOKEN STATS` — derived statistics **NEW**

**Why it earns its place.** The source stored a log-probability and derived `exp(logprob)` and nothing else. Every question a user actually asks — *how confident was this answer overall? where was it guessing? is this model more certain than that one?* — needs an aggregate, and the README's four stated use cases ("understanding model confidence", "identifying uncertain parts", "debugging unexpected outputs", "tuning prompts") are all aggregate questions. This is the smallest tool that answers them, and it is pure arithmetic over a document, so it runs with no backend, no model and no network.

| | |
|---|---|
| **Command** | `STATS` |
| **Root** | `TOKEN` |
| **Description** | `Compute entropy, perplexity and confidence statistics over an analysis` |
| **Prototype** | `TOKEN STATS [-id analysisid] [-threshold 0.0-1.0] [-unit nats\|bits] [-format json\|csv\|text] [-perstep]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `id` | named | `string` | no | `TOKEN_LAST` | 12-char analysis id | Analysis to summarise when not piped |
| `threshold` | named | `double` | no | `0.30` (`TOKEN_LOWCONF`) | 0.0–1.0 | Low-confidence cut-off (the source's lowest colour band, BR-22) |
| `unit` | named | `string` | no | `nats` | `nats`, `bits` | Entropy unit |
| `format` | named | `string` | no | `json` | `json`, `csv`, `text` | Output shape; sets `OutputFormat` to `JSON`/`CSV`/`General` accordingly |
| `perstep` | flag | `bool` | n/a | `false` | presence ⇒ true | Also emit per-token entropy/margin rows instead of the summary alone |

**Definitions (normative).** For the selected token set of size *n*, with chosen-token probability *pᵢ = exp(logprobᵢ)* clamped to (0, 1]:

- `meanNegLogLikelihood` = −(1/n) Σ ln pᵢ
- `perplexity` = exp(meanNegLogLikelihood)
- per-step `entropy` Hᵢ = −Σ_k q_k ln q_k over the chosen token **plus** its top-K alternatives, renormalised so Σ q_k = 1. **This is a truncated top-K entropy and therefore a lower bound on the true distribution's entropy**; `entropyIsTruncated` is always `true` for live data and the tool says so in `-format text`. Reporting a bound honestly is the point; the source reported nothing.
- `normalizedEntropy` = Hᵢ / ln(K+1) ∈ [0, 1], so runs with different top-K are comparable.
- `margin` = p₁ − p₂ (chosen minus best alternative); `null` when the record has no alternatives.
- `bands` = counts per the fixed boundaries ≥0.90 / ≥0.70 / ≥0.50 / ≥0.30 / else.

**Pipeline behaviour.** **Both, and it is the one tool that must aggregate.** `HandlePipedChunk` cannot emit an end-of-stream summary (`OnEndPipe` returns `void`), so `STATS` overrides `Main`: it consumes the whole input stream, passes every `analysis`/`token`/`attribution` chunk through unchanged, and appends one `stats` chunk per analysis id at end of stream — so `… | TOKEN STATS | TOKEN SHOW` shows both the tokens and the summary. With `-perstep` it enriches each `token` chunk with `entropy`, `normalizedEntropy` and `margin` in place. `-format csv` emits a header row and one row per analysis (or per token with `-perstep`) and declares `ResultFormat.CSV`.

**Environment.** Reads `TOKEN_LOWCONF`, `TOKEN_LAST`. Writes nothing.

**Failure modes.**

| Condition | Result |
|---|---|
| `n = 0` (no token records) | **Succeeds** with `n: 0` and every aggregate `null` — no division by zero, no error. An empty analysis is a legitimate answer |
| Every record has `logprob: null` | Succeeds; `perplexity`/`meanNegLogLikelihood` are `null`, `bands` all zero, and a `note` field explains that the provider returned no probabilities |
| A record has `logprob > 0` (not a log-probability) | That record is excluded, `excludedRecords` is incremented, and the summary carries `warning: "n records carried non-negative log-probabilities and were excluded"`. The source accepted and exponentiated positive values without comment, which is how its demonstration data produced probabilities of e⁷⁰ (QUIRK-Q2, BR-09) |
| `-threshold` outside 0.0–1.0 | Failure naming the range |
| Not piped, no `-id`, `TOKEN_LAST` unset | Failure: `No analysis to summarise. Pipe one in, or run TOKEN MAP first.` |

**Security and audit.** Emits **numbers only** — no token text appears in a `stats` record. `TOKEN STATS -format csv` is therefore the safe artefact to paste into a bug report, and the composition `TOKEN LOAD run42 | TOKEN STATS -format csv` is the recommended way to share a finding without sharing the prompt. Non-destructive.

**Traceability.** **7.9** / **7.10**. **NEW** — no ancestor. The nearest thing in the source was `TokenLogProbability.Probability` (`exp(logprob)`), whose sole test asserted `25` for a stored `ln(0.25)` and could not pass (QUIRK-Q1); this tool's contract is `probability = exp(logprob)` on a 0–1 scale, and the corresponding test asserts `0.25`.

---

#### 6.3.8 `TOKEN FILTER` — confidence filter **NEW**

**Why it earns its place.** The framework's own idiom for narrowing a stream is a filter command that returns empty-success for records it rejects (`REGIF`). The single most common introspection task — *show me only where the model was unsure* — has no expression in the source at all; it required reading every token in a grid and looking for a colour. One composable primitive replaces that.

| | |
|---|---|
| **Command** | `FILTER` |
| **Root** | `TOKEN` |
| **Description** | `Keep only the token records that match a confidence predicate` |
| **Prototype** | `TOKEN FILTER [-below p] [-above p] [-band veryhigh\|high\|medium\|low\|verylow] [-marginbelow m] [-range start end] [-mode sample\|all] [-samplesize n] [-dedupe] [-invert]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `below` | named | `double` | no | *(unset)* | 0.0–1.0 | Keep records with probability strictly below this |
| `above` | named | `double` | no | *(unset)* | 0.0–1.0 | Keep records with probability at or above this |
| `band` | named | `string` | no | *(unset)* | `veryhigh`, `high`, `medium`, `low`, `verylow` | Keep records in this confidence band |
| `marginbelow` | named | `double` | no | *(unset)* | 0.0–1.0 | Keep records whose chosen-vs-runner-up margin is below this — near-ties |
| `range` | named | `int` ×2 | no | *(unset)* | two integers, `start` ≤ `end` | Keep records whose 0-based index is in `[start, end]` |
| `mode` | named | `string` | no | `sample` (`TOKEN_MODE`) | `sample`, `all` | `all` keeps every record; `sample` applies the beginning/middle/end rule below |
| `samplesize` | named | `int` | no | `5` (`TOKEN_SAMPLESIZE`) | 1–100 | Records per sampled segment |
| `dedupe` | flag | `bool` | n/a | `false` | presence ⇒ true | **NEW** — drop records selected by more than one segment |
| `invert` | flag | `bool` | n/a | `false` | presence ⇒ true | Keep exactly the records the predicate rejects |

**The sampling rule, preserved exactly (BR-10, BR-11, BR-12).** With `-mode sample` and no other predicate: if the record count ≤ `3 × samplesize` (**15** by default) every record is kept, un-segmented. Otherwise exactly three segments of `samplesize` records are kept — beginning at index `0`, middle at index `count / 2 − samplesize / 2` (integer division; `count/2 − 2` at the default), end at index `count − samplesize`. **Segments may overlap or leave gaps for counts just above the floor and no de-duplication is performed** — at count 16 the segments are `0–4`, `6–10`, `11–15`, so index 5 is never shown and nothing is duplicated. That is the source's behaviour and it is preserved by default; `-dedupe` is the opt-in correction. Records keep their **absolute** `index` throughout, which fixes QUIRK-Q9 (the source's demonstration path renumbered sampled tokens 1…15, losing their true positions 1-5, 11-15, 21-25).

**Pipeline behaviour.** **Filter — its natural and primary form.** Inherits `AbstractCommand`. One piped chunk is one document record. `analysis`, `attribution` and `stats` chunks pass through unchanged (a filter must never strip an analysis's header). `token` chunks that match are re-emitted verbatim; `token` chunks that do not match return `CommandResult<string>.Success(string.Empty, …)`, which the host drops — the framework's canonical filter idiom. Sampling predicates need the total count, so `-mode sample` buffers `token` chunks per analysis id in `Main` and flushes the selection when the analysis ends; `OnStartPipe` clears that buffer. Non-piped, it filters the analysis named by `-id`/`TOKEN_LAST`. `ResultFormat.JSON`.

**Environment.** Reads `TOKEN_MODE`, `TOKEN_SAMPLESIZE`, `TOKEN_LOWCONF`, `TOKEN_LAST`. Writes nothing.

**Failure modes.** No predicate at all with `-mode all` is a no-op pass-through, not an error. `-below` above `-above` yields an empty result set, not an error. `-range` with a missing second value is caught by the framework before the tool runs (`Error executing TOKEN …`), which is why the prototype spells both values. A record with `logprob: null` is treated as unmatched by every probability predicate and is dropped unless `-invert`.

**Security and audit.** Passes content through; adds none. Non-destructive.

**Traceability.** **7.9**. **NEW** as a tool; the sampling half descends directly from the source's shared sampler (`DemoLogProbsCommand`, `ChatShell`, `TokenProbabilityVisualizer`, `SpectreConsoleFormatter` — the same logic in four places, three of them unreachable at runtime). Collapsing it to one implementation was the source's own recorded recommendation.

---

#### 6.3.9 `TOKEN SHOW` — view an analysis

| | |
|---|---|
| **Command** | `SHOW` |
| **Root** | `TOKEN` |
| **Description** | `Render an analysis as readable text` |
| **Prototype** | `TOKEN SHOW [-id analysisid] [-layout list\|grid] [-maxalt 1-20] [-alts 0-20] [-attribution] [-stats] [-width n]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `id` | named | `string` | no | `TOKEN_LAST` | 12-char analysis id | Analysis to render when not piped |
| `layout` | named | `string` | no | `list` (`TOKEN_LAYOUT`) | `list`, `grid` — `list` first (BR-04) | Presentation shape |
| `maxalt` | named | `int` | no | `5` (`TOKEN_GRIDMAXALT`) | 1–20 (BR-05) | Alternatives drawn per grid card before a `+N more` marker |
| `alts` | named | `int` | no | `3` | 0–20 | Alternatives listed per list row (the source's shared helper defaulted to 3, BR-25) |
| `attribution` | flag | `bool` | n/a | `false` | presence ⇒ true | Include attribution lines |
| `stats` | flag | `bool` | n/a | `false` | presence ⇒ true | Include the summary block |
| `width` | named | `int` | no | `0` | `0`, or 20–500 | `0` = let the renderer decide. **This tool never queries the terminal itself** |

**Formatting contract preserved from the source (these are pinned behaviours, not suggestions).**

- Token text is escaped for display: newline → `\n`, carriage return → `\r`, tab → `\t`, NUL → `\0`; a null token renders as the literal `(null)` (BR-21). Unlike the source, this escaping is applied on **every** path — the source's plain-text report skipped it even though the helper sat eleven lines above in the same file, so a token containing a newline broke the table (QUIRK-Q22), and the terminal-UI panel escaped only three of the four characters and would fault on a genuinely null token (BR-39).
- Probability text is fixed-point with **2 decimals** plus a percent sign: `0.421234` → `42.12%` (BR-23, on the corrected 0–1 scale). Exactly one `%`; the source produced a doubled `%%` on six call sites (QUIRK-Q3).
- The compact alternatives description lists at most `alts` entries, comma-separated as `{escaped token} ({percentage})`, and appends ` (+ {count − alts} more)` when the list is longer; an empty or null list renders as the literal `(none)` (BR-25).
- Numbering is **1-based on display** for every layout (§6.3, indexing rule).

**Pipeline behaviour.** **Sink-shaped filter.** Inherits `AbstractCommand`; one piped chunk is one document record, and each produces zero or more rendered lines as one `ResultFormat.General` chunk. `analysis` chunks render the header; `token` chunks render a row or a card; `stats` chunks render the summary block; `attribution` chunks render only with `-attribution`. Non-piped it renders `-id`/`TOKEN_LAST`. Because it emits `General`, it is normally the last stage — but nothing prevents `… | TOKEN SHOW | REGIF "very-low"`.

**Environment.** Reads `TOKEN_LAYOUT`, `TOKEN_GRIDMAXALT`, `TOKEN_MODE`, `TOKEN_LAST`. Writes nothing.

**Failure modes.**

| Condition | Result |
|---|---|
| Not piped, no `-id`, `TOKEN_LAST` unset | Failure: `No analysis to show. Pipe one in, or run TOKEN MAP first.` |
| Analysis exists but has zero token records | Succeeds, emitting the single line `No token probability data available.` — the source had two variants of this string differing only in a trailing period (BR-38 vs §B6-7); the rebuild uses one, with the period. **D-001:** when the cause is a capability-absent provider, the line is instead `No token probability data for this message: provider '{provider}' does not supply it.` — and in the full-screen shell this arrives as a **transient status-bar notice, never a modal**; the view attempt is non-blocking in every surface |
| Records carry `synthetic: true` | Every rendered header is prefixed `SAMPLE DATA —` so demonstration output can never be mistaken for a measurement |
| Records carry `estimated: true` | The header notes `(token text estimated; backend has no decoder)` |
| Upstream failure chunk | Forwarded verbatim and rendered by the host as an error, after which `SHOW` continues with later chunks |

**Security and audit.** Renders token text; content-bearing on **output**, not on input. Non-destructive.

**Traceability.** **7.10** and **7.12 Output Rendering**. Descends from `ShowTokenAnalysisCommand` — which in the source was a pure stub that parsed `--top`, `--state` and `--range` into locals and returned a fixed informational message, read no data, rendered nothing, and **was not registered in either shell**, so typing it produced `Unknown command`. It also descends from the console shell's own render path, which was the only working renderer. `--state` has no successor here (model-state snapshots belong to `ChatDbg.Tools.Diagnostics`); `--range` is superseded by `TOKEN FILTER -range`, which is composable and does not need a `-1` sentinel whose meaning the source never defined.

---

#### 6.3.10 `TOKEN EXPORT` — write an analysis to disk

| | |
|---|---|
| **Command** | `EXPORT` |
| **Root** | `TOKEN` |
| **Description** | `Write an analysis to a file in the analysis directory` |
| **Prototype** | `TOKEN EXPORT <name> [-format json\|jsonl\|csv\|md] [-id analysisid] [-redact] [-force]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | no (in-body check) | *(none)* | **Filename stem only**: `[A-Za-z0-9_-]{1,64}`. Not a path — see below | Destination file stem |
| `format` | named | `string` | no | `json` | `json`, `jsonl`, `csv`, `md` | `json` = one document object; `jsonl` = the wire format verbatim; `csv` = one row per token; `md` = the source's plain-text report shape |
| `id` | named | `string` | no | `TOKEN_LAST` | 12-char analysis id | Analysis to export when not piped |
| `redact` | flag | `bool` | n/a | `false` | presence ⇒ true | **NEW** — replace every `text` field with `<t:{tokenId}>` and drop `prompt`/`influencingText`, keeping all numbers |
| `force` | flag | `bool` | n/a | `false` | presence ⇒ true | Overwrite an existing file without confirmation |

**Why the parameter is a name and not a path — this is a hard constraint, not a preference.** The framework's argument tokenizer matches unquoted tokens as `[\w-]+`, so `C:\logs\run.json` arrives as four separate arguments `C`, `logs`, `run`, `json`. Quoting does not save it: the quoted span is then filtered by `InvalidParameterChars`, which strips `\`, `/`, `:`, `=`, `,` and `;` **inside quotes too**, yielding `C:logsrun.json` → `Clogsrun.json`. **A filesystem path cannot survive this framework's command line.** The destination directory therefore comes from `TOKEN_EXPORTDIR`, and the command line supplies only a stem; the extension is chosen by `-format`. This also removes the entire path-traversal surface: the tool joins `TOKEN_EXPORTDIR` with a stem it has already validated against `[A-Za-z0-9_-]{1,64}`, resolves the result to a full path, and verifies component-wise containment (`Path.GetRelativePath` must not start with `..` and must not be rooted) plus symlink resolution before opening the file — the `StartsWith` containment check the loader library itself uses is not sufficient.

**Pipeline behaviour.** **Sink.** Accepts a piped analysis stream (one chunk = one record) and accumulates it; at end of stream it writes one file per analysis id encountered, then emits **one** `ResultFormat.General` confirmation line per file: `Wrote {n} token records to {name}.{ext}`. Non-piped it exports `-id`/`TOKEN_LAST`. It never emits the analysis itself, so it terminates a pipeline cleanly. Multi-analysis input writes `{name}-{analysisId}.{ext}` for the second and subsequent analyses rather than clobbering.

**Environment.** Reads `TOKEN_EXPORTDIR`, `TOKEN_LAST`. Writes `TOKEN_LASTEXPORT` (the resolved filename, so a subsequent `TOKEN LOAD` can be typed without remembering it).

**Failure modes.**

| Condition | Result |
|---|---|
| No `name` | Failure: `Usage: TOKEN EXPORT <name> [-format json\|jsonl\|csv\|md]` — modelled on the source's three-line usage block, minus its final line about needing provider integration |
| `name` fails the stem pattern | Failure: `Export name must be 1-64 characters of letters, digits, '-' or '_'. Directory comes from TOKEN_EXPORTDIR.` |
| `TOKEN_EXPORTDIR` unset, missing, or not writable | Failure naming the directory and the reason. **It does not fall back to the current directory or the temp directory.** The source's settings layer silently fell back to the OS temp directory when the profile path was unavailable, which meant a user could not tell where their data went |
| Target exists and `-force` absent | **Confirmation required.** Not piped: `io.PromptForCommand("{file} exists. Overwrite? (y/n)")`; only `y`/`yes` proceeds. Piped: no prompt is possible, so it fails with `{file} exists. Re-run with -force to overwrite.` |
| Write fails (disk full, permission, path too long) | **Failure**, with the OS message. The source swallowed every export failure into a logged error and told the caller nothing; worse, its `export-analysis` returned **success** with a "Note:" message while creating no file at all, so the shell printed a `✓` for an export that never happened |
| Zero token records | Writes the header-only document and reports `Wrote 0 token records to {file}` — an empty analysis is a legitimate artefact |

**Security and audit.** ⚠️ **This is the only tool that persists content.** An exported analysis contains the prompt and the model's output verbatim; if the user pasted a secret into a prompt, `TOKEN EXPORT` writes it to disk in cleartext. Two mitigations are specified: `-redact` produces a numerically complete but content-free document suitable for sharing, and the confirmation gate above ensures no silent overwrite. **Destructive/irreversible:** yes, when overwriting — hence `-force` and the prompt. The `name` parameter carries no secret and needs no masking; the *file* does, and the host should place `TOKEN_EXPORTDIR` inside the user's private profile.

**Traceability.** **7.10**. Descends from `ExportTokenAnalysisCommand` (a stub that validated nothing, wrote nothing and returned success) and from `LLamaSharpService.SaveAnalysesToJson`, the real, indented-JSON exporter that no command could reach. `-redact`, `-force`, the `csv`/`md`/`jsonl` formats and the containment checks are **NEW**.

---

#### 6.3.11 `TOKEN LOAD` — read an analysis back **NEW**

**Why it earns its place.** Every other tool in this package operates on a document; without a way to get a document back from disk, the entire surface is trapped inside one process lifetime and cannot be tested, compared or shared. `LOAD` is what makes `STATS`, `FILTER`, `SHOW` and `DIFF` runnable with no model, no backend and no network — which is also what makes them unit-testable and what makes this package safe in a restricted host.

| | |
|---|---|
| **Command** | `LOAD` |
| **Root** | `TOKEN` |
| **Description** | `Read a previously exported analysis` |
| **Prototype** | `TOKEN LOAD <name> [-format auto\|json\|jsonl\|csv] [-as alias]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered | `string` | no (in-body check) | *(none)* | `[A-Za-z0-9_-]{1,64}` stem, resolved under `TOKEN_EXPORTDIR` | File stem to read |
| `format` | named | `string` | no | `auto` | `auto`, `json`, `jsonl`, `csv` | `auto` picks by extension, then by sniffing the first byte |
| `as` | named | `string` | no | *(the file's own id)* | 12-char id pattern | **NEW** — rename the analysis on load, so two files can coexist in the ring for `TOKEN DIFF` |

**Pipeline behaviour.** **Source.** Overrides `Main` to stream the file record by record — one `analysis` chunk then one `token` chunk per record — so a very large analysis never has to be materialised. `ResultFormat.JSON`. Ignores piped input.

**Environment.** Reads `TOKEN_EXPORTDIR`, `TOKEN_LASTEXPORT`, `TOKEN_KEEP`. Writes `TOKEN_LAST`.

**Failure modes.**

| Condition | Result |
|---|---|
| No `name` and `TOKEN_LASTEXPORT` unset | Failure: `Usage: TOKEN LOAD <name>` |
| Stem fails the pattern, or resolves outside `TOKEN_EXPORTDIR` | Failure: `Refusing to read outside the analysis directory.` — the containment check is component-wise plus symlink-resolved, not a `StartsWith` prefix test |
| File not found | Failure: `No analysis named '{name}' in {dir}.` |
| Malformed JSON at line *k* | Failure naming the line number, following the framework's own trust-store precedent of reporting the offending line. Records already emitted are not retracted; the stream ends with the failure chunk |
| `schemaVersion` newer than this build understands | Failure: `Analysis '{name}' uses schema version {v}; this build understands {n}.` **It does not attempt a best-effort parse** |
| `schemaVersion` older | Succeeds with an upgrade shim and a note chunk naming the migration applied |
| Duplicate id already in the ring | The loaded analysis takes the id and evicts the older one, unless `-as` was given |

**Security and audit.** Reads content into memory and onto the pipe. The path containment above is the security boundary; there is no other. Non-destructive.

**Traceability.** **NEW** — the source had no import path for analyses at all (its `/import` handled chat history, a different owner). Sits on the 7.10 / 7.4 boundary and is deliberately placed here because it reads the *analysis* schema, not the *history* schema.

---

#### 6.3.12 `TOKEN DEMO` — offline sample data

| | |
|---|---|
| **Command** | `DEMO` |
| **Root** | `TOKEN` |
| **Description** | `Emit a deterministic sample analysis with no model call` |
| **Prototype** | `TOKEN DEMO [-topk 1-20] [-seed s] [-tokens n]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `topk` | named | `int` | no | `5` (`TOKEN_TOPK`) | 1–20 | Alternatives per token |
| `seed` | named | `long` | no | `42` | any | Generator seed — the source's constant (BR-14) |
| `tokens` | named | `int` | no | `25` | 1–500 | How many sample tokens to emit; `25` reproduces the source's fixed sentence exactly |

**Preserved sample data (BR-13 – BR-20).** With default parameters the fixed sentence *"This is a sample response with token probability analysis. You can see how the model assigned probabilities to each token and what alternatives it considered."* yields exactly **25** tokens, in order: `This, is, a, sample, response, with, token, probability, analysis, You, can, see, how, the, model, assigned, probabilities, to, each, token, and, what, alternatives, it, considered` — splitting on space, newline, tab, `.`, `,`, `!`, `?` and discarding empties. The hard-coded plausible alternatives are preserved verbatim (`sample` → `example`, `test`, `demo`; `response` → `reply`, `answer`, `output`; `token` → `word`, `symbol`, `element`; `probability` → `likelihood`, `chance`, `confidence`; `analysis` → `evaluation`, `assessment`, `examination`; anything else → `{token}_1/_2/_3`), as is the filler rule that pads to top-K with `alt_{0..999}` entries and the truncation to the first `min(topK, count)` alternatives in generation order. The simulated elapsed time is **0.5 seconds**.

**Three deliberate corrections.**
1. **Values are stored as real log-probabilities.** The source wrote numbers in the range **[70, 98)** into the *log-probability* field, which display code then exponentiated — rendering "probabilities" of e⁷⁰ ≈ 2.5×10³⁰ and putting every token in the top colour band (QUIRK-Q2). Those numbers were plainly meant as percentages. `TOKEN DEMO` stores `ln(percent/100)`, so the derived probability lands in [0.70, 0.98) exactly as intended and the confidence bands finally distribute.
2. **Determinism is portable.** The source seeded the platform RNG with 42, which is reproducible within one runtime and not across a port. `TOKEN DEMO` uses a specified deterministic generator (a documented 64-bit xorshift), so the same seed yields the same bytes on every OS, runtime and architecture — a property the source's own dossier flagged as the actual requirement.
3. **Everything is stamped `synthetic: true`** on both the `analysis` record and every `token` record, and `TOKEN SHOW` prefixes the header `SAMPLE DATA —`. The source's demonstration data was indistinguishable from measurement once it left the command.

**Pipeline behaviour.** **Source only.** Overrides `Main`; emits one `analysis` chunk and `tokens` `token` chunks. `ResultFormat.JSON`. Ignores piped input.

**Environment.** Reads `TOKEN_TOPK`, `TOKEN_KEEP`. Writes `TOKEN_LAST`.

**Failure modes.** `-topk` or `-tokens` out of range fails with the range in the message. It cannot fail for any environmental reason: no model, no file, no network, no backend. **That is its whole purpose** — the source's demonstration mode existed precisely to avoid paid API calls, and it is the tool that proves the rest of the package works in a restricted host.

**Security and audit.** No secret, no user content, no filesystem, no network. Non-destructive. The single safest tool in the package and the right first thing to run in a new host.

**Traceability.** **7.9**. Descends from `/demologprobs` (`DemoLogProbsCommand`). One structural change: the source's demo command rendered the visualization itself when a formatter had been injected and rendered nothing when one had not — and *neither shell wired it up correctly*, so on the plain console it printed only `✓ Sample token probability analysis generated` and under the terminal UI it wrote ANSI escapes beneath a full-screen application that owned the screen (QUIRK-Q7). `TOKEN DEMO` renders nothing at all; it emits a document, and `TOKEN SHOW` renders it. The README's promise that the command "generates sample data to show how the visualization works" is honoured by the composition `TOKEN DEMO | TOKEN SHOW`.

---

#### 6.3.13 `TOKEN DIFF` — compare two analyses **NEW**

**Why it earns its place.** The source's stated purposes include "tuning prompts for better results" and "debugging unexpected outputs" — both are *comparative*: the same prompt at two temperatures, before and after a system-prompt edit, one model against another. The source could not express the comparison at all; a user had to read two grids side by side. This is the smallest tool that answers "what changed", and like `STATS` it is pure arithmetic over documents, so it needs no backend.

| | |
|---|---|
| **Command** | `DIFF` |
| **Root** | `TOKEN` |
| **Description** | `Compare two analyses token by token` |
| **Prototype** | `TOKEN DIFF <baseline> [-against analysisid] [-align text\|index] [-minshift d] [-format json\|text]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `baseline` | ordered | `string` | no (in-body check) | *(none)* | 12-char analysis id, or a `TOKEN LOAD` alias | The analysis to compare against |
| `against` | named | `string` | no | piped stream, else `TOKEN_LAST` | 12-char analysis id | The other analysis |
| `align` | named | `string` | no | `text` | `text`, `index` | `text` aligns on token text with a longest-common-subsequence pass so insertions do not shift everything; `index` compares position for position |
| `minshift` | named | `double` | no | `0.05` | 0.0–1.0 | Only report positions whose probability moved by at least this much |
| `format` | named | `string` | no | `json` | `json`, `text` | Output shape |

**Pipeline behaviour.** **Filter.** The piped stream is the "against" side; `baseline` names the other. Emits one `diff` record per aligned position (`kind`, `baselineIndex`, `againstIndex`, `text`, `baselineProbability`, `againstProbability`, `shift`, `bandChange`, `alternativesGained[]`, `alternativesLost[]`) plus one trailing `diffsummary` record with mean absolute shift, perplexity delta and band migration counts. `ResultFormat.JSON`, or `General` with `-format text`. Non-piped it compares `baseline` against `-against`/`TOKEN_LAST`.

**Environment.** Reads `TOKEN_LAST`. Writes nothing.

**Failure modes.** A missing side fails with `Analysis '{id}' not found. Load it with TOKEN LOAD, or run it first.` Comparing an analysis with itself succeeds and reports all-zero shifts. Comparing a `synthetic: true` analysis against a live one succeeds but stamps `syntheticInvolved: true` on the summary. Analyses with different `topK` are comparable for probabilities but not for entropy; the summary carries `entropyComparable: false` rather than silently comparing incomparable numbers.

**Security and audit.** Content-bearing on output (it prints token text). `-format json` plus a downstream `TOKEN STATS`-style numeric projection is the shareable form. Non-destructive.

**Traceability.** **7.9**. **NEW** — no ancestor of any kind.

---

### 6.4 Pipeline compositions

**1. Find where the model was guessing, in one line.**

```
TOKEN MAP "explain the CAP theorem" -n 60 -topk 10 | TOKEN FILTER -below 0.5 -mode all | TOKEN SHOW -alts 3
```

`MAP` streams 60 token records as they are generated; `FILTER` drops every record at or above 50 % confidence, returning empty-success for them so the host silently discards them; `SHOW` prints the survivors with their three best alternatives. The user sees only the uncertain spans — with absolute 1-based indices, so they can be located in the full output — and sees them appearing while generation is still running, because every stage runs concurrently and the channels are pulled lazily.

**2. Summarise a run without ever showing its content.**

```
TOKEN LOAD run42 | TOKEN STATS -format csv -threshold 0.3
```

`LOAD` streams the file from `TOKEN_EXPORTDIR`; `STATS` passes every record through, accumulates, and appends one CSV row: `analysisId,n,meanProbability,medianProbability,minProbability,maxProbability,meanNegLogLikelihood,perplexity,meanEntropy,meanNormalizedEntropy,meanMargin,veryHigh,high,medium,low,veryLow,lowConfidenceCount,lowConfidenceRatio`. The `stats` record contains **no token text**, so this is the artefact to paste into an issue. Runs with no model, no network and no backend.

**3. See the visualization the source promised but never delivered.**

```
TOKEN DEMO -topk 5 | TOKEN STATS -perstep | TOKEN SHOW -layout grid -maxalt 5
```

`DEMO` emits the 25-token sample set with real log-probabilities; `STATS -perstep` enriches each record with entropy, normalized entropy and margin and appends a summary; `SHOW` draws the grid, prefixing the header `SAMPLE DATA —`. This is the smoke test for a fresh host: it exercises the document schema, the statistics layer and the renderer with no model, no file and no credential, and it is the README's promise finally honoured.

**4. Archive an analysis without archiving the prompt.**

```
TOKEN MAP "review this contract clause: ..." -n 200 | TOKEN ATTRIBUTE -window 8 | TOKEN EXPORT clause-review -format jsonl -redact
```

`MAP` streams; `ATTRIBUTE` interleaves an attribution record after each token; `EXPORT` accumulates and writes `{TOKEN_EXPORTDIR}/clause-review.jsonl` with every `text`, `prompt` and `influencingText` field replaced by `<t:{tokenId}>` placeholders, keeping every number intact. Then one confirmation line. If `clause-review.jsonl` already exists the tool prompts (or fails with a `-force` hint when piped), so an archive is never silently clobbered.

**5. Cross-package: chat, capture, summarise, draw.** *(crosses into `ChatDbg.Tools.Providers` and `ChatDbg.Tools.Rendering`)*

```
AI ASK "why did the deploy fail" -capture | TOKEN STATS -perstep | TOKEN FILTER -band low -mode all | RENDER HEATMAP -scale 0-1
```

`AI ASK -capture` (Providers, 7.6/7.8) performs the turn and emits the assistant text plus the analysis document, honouring `TOKEN_CAPTURE` and `TOKEN_TOPK` from this package's environment bucket via the global settings it shares. `TOKEN STATS -perstep` adds per-token entropy and margin. `TOKEN FILTER -band low` narrows to the `low` confidence band. `RENDER HEATMAP` (Rendering, 7.12) paints the survivors — reading the `band` field this package computed and mapping it to the source's green/lime/yellow/orange/red palette. Four packages' worth of concern, one line, and each stage is independently testable because the boundary between them is a documented record, not a shared object.

**6. Cross-package: make a capture policy durable.**

```
TOKEN CAPTURE on -topk 10 -mode all -print | CONFIG SET -section token
```

`TOKEN CAPTURE` applies the policy to its own environment bucket and, because of `-print`, also emits it as `key=value` lines. `CONFIG SET` (Settings, 7.2) is the only command in the product registered with `modifiesEnvironment: true` and the only writer of `~/.ChatDbg/settings.json`, so it takes those lines and persists them. This replaces the source's arrangement in which `/logprobs` wrote the whole settings document itself on every mutation — and in which three of the five keys it wrote were never read back on the next start of the console shell (QUIRK-Q5), so display preferences silently reverted at every restart.

**7. Compare two prompts with a framework built-in in the chain.**

```
TOKEN LOAD baseline -as base | TOKEN DIFF base -against last -format text | REGIF "band-drop"
```

`LOAD` re-reads a saved run under a stable alias; `DIFF` aligns it against the most recent analysis by token text and emits one line per shifted position; `REGIF` (a framework built-in, needing no code from us) keeps only the lines reporting a band drop. Nothing in this pipeline touches a model.

---

### 6.5 Design notes for the architect

**What state this package holds.** Exactly two things. (1) A **bounded, process-scoped analysis ring** — the last `TOKEN_KEEP` analyses (default 8), keyed by a 12-character id, behind an `IAnalysisStore` interface with an in-memory default the host may replace. It must be process-scoped and not instance-scoped: `CommandFactory` constructs a **new command instance per execution**, so nothing survives on `this` between two `TOKEN` invocations. (2) **Policy in the environment**, under `TOKEN_*` keys in the package's own bucket, which the host round-trips for free. That is all. The source held one mutable "last generation" list that was cleared at the start of every generation, so the answer to "show me the previous run" was always "gone"; a small ring with explicit ids costs nothing and makes `DIFF` possible.

**What it must not hold.** No credential, endpoint, region, or key — ever, in any field, on any path, in any log line. No conversation: an analysis references a message id and never embeds the chat history. No provider client, HTTP handler, socket, or native model handle: those are borrowed through `IProbabilitySource` for the duration of one call and never cached here. No `System.Diagnostics.Trace`-only error swallowing: the source's diagnostic channel absorbed a whole class of failures invisibly (the logit accessor returning an empty candidate list from a bare `catch` with no log line, so the feature reported total confidence in everything — QUIRK-Q14). Every failure in this package either becomes a `Failure` chunk the user sees or an explicit note record; nothing is written only to the trace.

**How it stays testable.** Three deliberate properties. First, **every tool except `SPLIT`, `MAP` and `INSPECT` is a pure function over documents** — no model, no network, no clock beyond `createdUtc`, no filesystem except `LOAD`/`EXPORT`. `STATS`, `FILTER`, `SHOW`, `DIFF` and `DEMO` are testable with a string fixture and an assertion. Second, the three impure tools depend on `ITokenizer` / `IProbabilitySource` / `IAttributionModel` interfaces resolved through `IntrospectionServices`, which is a settable static registry — a test substitutes a fake and never loads a model. This is the direct answer to the source's own recorded verdict: *"the inspection service is `static`, so no seam exists for substituting a tokenizer; consequently the only tests are the two file-not-found guards."* Third, **`TOKEN DEMO` is the integration fixture**: its output is deterministic across platforms by construction, so `TOKEN DEMO | TOKEN STATS` has a byte-exact expected value, and the whole pipeline machinery is covered without a model. Add one hermetic suite (documents and fakes) and one backend suite (a small real GGUF, opt-in, skipped by default) — the same split the reference host uses for its offline and network tests.

**Instance state and the DI trap.** `CommandFactory` prefers `serviceProvider.GetService(commandType)` when the type resolves in the default context. If a host registers these tools as **singletons**, the same instance is reused across executions *and across pipeline stages* — which would corrupt `FILTER`'s sampling buffer and `ATTRIBUTE`'s look-back window. Two defences, both required: every per-pipe field is reset in `OnStartPipe` rather than in the constructor, and the package's registration guidance says **transient**. When loaded as a plugin from a package directory this cannot arise (activation is per-execution through the ALC), but a host that project-references the assembly and wires it through DI can hit it.

**Concurrency.** The framework starts **all pipeline stages concurrently** and the stages communicate only through bounded channels, so the tools need no locks among themselves. The one shared mutable is the analysis ring, which is a concurrent dictionary with a bounded eviction policy. Model access is serialised inside `IProbabilitySource`, not here — the source's `/tokenize` and `/inspect` bypassed the chat path's process-wide generation semaphores and constructed their own model instances, so a command issued during a chat turn attempted a concurrent native load. Delegating removes that risk rather than duplicating the gate.

**Cancellation.** The source had none anywhere: a long generation could not be aborted. `IProbabilitySource` takes a `CancellationToken`, and `MAP`/`INSPECT` honour the pipeline's `StageTimeoutSeconds` — which is the only `PipelineConfiguration` timeout the framework actually reads (`ExecutionTimeoutSeconds`, `MaxStageOutputBytes` and `MaxStageOutputItems` are declared and never enforced). A host that wants a wall-clock cap on generation must set `StageTimeoutSeconds`; on expiry the framework completes the stage with a **status message**, not an output chunk, so the host's IO context must surface status for the user to see why the stream stopped.

**When a capability is unavailable — degrade, or refuse, but never invent.** The rule is uniform and it is the single most important correction in this chapter.

| Missing capability | Behaviour |
|---|---|
| No local backend at all (hosted-only, or restricted host) | `DEMO`, `LOAD`, `FILTER`, `STATS`, `SHOW`, `EXPORT`, `DIFF`, `CAPTURE`, `DIAG` all work fully. `SPLIT`, `MAP`, `INSPECT` fail with a message naming the missing capability and pointing at `TOKEN DEMO`. **Degrade the surface, not the truth.** |
| Provider generates but returns no probabilities | The analysis is produced with `token` records carrying `logprob: null`, plus the source's exact two-line note. **No synthetic substitution** — the source's cloud adapter fabricated a 15-token list at `ln(0.9)` with three hard-coded alternatives and returned it as if measured, a behaviour its own test suite pinned |
| Backend decodes token IDs but exposes no text | Records carry `<token_{id}>` **and** `estimated: true`; `SHOW` says so in the header. The source printed the same placeholder with no marker and its README claimed real decoded strings |
| Backend exposes no character offsets | `charStart = charEnd = -1`. The source computed a uniform-distribution estimate that produced contiguous, plausible-looking spans bearing no relation to real boundaries |
| Only top-K alternatives available (always, for real providers) | `truncatedTopK: true` and `entropyIsTruncated: true`; entropy is documented as a lower bound |
| Attribution method unavailable | Refuse, naming the available methods. Never silently downgrade `attention` to `recency` |
| Terminal is not attached, or output is redirected | Irrelevant — **no tool in this package queries the console**. The source derived grid column count from `Console.WindowWidth` with no guard around the query itself, so a redirected session could surface as a generic `Error getting AI response`. Width is a `SHOW` parameter with `0` meaning "renderer decides" |

**Cross-platform posture.** Nothing in this package is gated on an operating system — that was already true of the source feature, and it stays true. Four environmental couplings are handled explicitly. *Analysis directory:* `TOKEN_EXPORTDIR` defaults to `%APPDATA%\ChatDbg\analyses` on Windows and to `$XDG_DATA_HOME/ChatDbg/analyses` (falling back to `~/.local/share/ChatDbg/analyses`) on Linux and macOS, because the .NET application-data special folder can resolve to an empty string on some Unix configurations; if the resolved directory cannot be created, `EXPORT`/`LOAD` **fail loudly** rather than silently redirecting to the temp directory as the source's settings layer did. *Native backends:* not this package's problem by construction — CPU-versus-CUDA is entirely inside `IProbabilitySource`. *Trimming and AOT:* the package is pure managed with no reflection over external types, so it survives the aggressive publish configurations that broke the source's late-bound logit lookup. *Culture:* every number written into a document uses the invariant culture and every number parsed from one is parsed with it — the source formatted with the ambient culture, so a comma-decimal locale produced documents that a period-decimal locale could not read back, and the source's own `Compact`/`SingleFile` publish profiles enabled invariant globalization, meaning **the same build produced different output depending on how it was published**.

**Where it should degrade rather than fail.** A zero-token analysis is a success, not an error. A `stats` record over an empty set reports `n: 0` with null aggregates. An analysis whose records all lack probabilities still renders its text. A record with a corrupt log-probability is excluded and counted, not fatal. An unknown chunk kind is passed through, not rejected. An older schema version is migrated with a note. Conversely, it should **fail** — loudly and specifically — when asked to write outside its directory, when asked to overwrite without permission, when asked for an attribution method the backend cannot provide, when the trust boundary of a path check is ambiguous, and whenever the only alternative is to present a number the model did not produce.

**One number, one place.** Every threshold in this chapter is either a source constant preserved verbatim (top-K 1–20 default 5; grid alternatives 1–20 default 5; sample size 5; sample floor 15; look-back 5; prompt tail 50; attribution cut-over 3; generation budget 10; band boundaries 0.90/0.70/0.50/0.30; demo seed 42; demo elapsed 0.5 s; demo token count 25) or a named environment key with a stated default. The source had the same constants spread across four unreachable copies of the same sampler and two same-named formatting helpers with contradictory contracts, so the identical token rendered as `92.00%` through one path and `9200.00000%%` through the other. There is one sampler, one formatter, one scale and one indexing rule in this package, and every one of them is a parameter.

---

## 7. ChatDbg.Tools.PresentationVisualization — Presentation & Visualization

**Root command:** `VIEW` · **Assembly:** `ChatDbg.Tools.PresentationVisualization` · **Contract:** `Xcaciv.Command.Interface` / `Xcaciv.Command.Core` **3.3.4**

---

### 7.0 Purpose and boundary

#### What this package owns

Everything between *a structured record* and *the glyphs a human sees in a terminal*. Concretely:

| Owned concern | Source ancestry |
|---|---|
| Dense card **grid** vs. detailed **list/table** layout for token-probability records, including column packing, card composition, and the `+ N more` overflow indicator | `IConsoleFormatter.DisplayTokenGrid` / `DisplayTokenTable`; `SpectreConsoleFormatter.cs:55-145`; `BasicConsoleFormatter.cs:72-112` |
| **Confidence heat-mapping** — every probability→colour band table, the scale each one expects, and the contrast rule | `TokenFormatters.cs:38-45` (D1), `ChatShell.cs:657-672` (D2), `ChatWindow.cs:99-113` (D3), `LogProbHeatmapView.cs:60-77` (D4) |
| **How many alternatives** are drawn per token and how the surplus is announced | `gridViewMaxAlternatives` (default 5, range 1–20); the hard caps of 2 (plain table) and 3 (styled table) |
| **Volume control** — showing every token vs. beginning/middle/end sampling, and the exact slice arithmetic | `showAllTokens`; E1 (5/5/5, threshold 15) and E2 (10/10/10, threshold 30) |
| **Colour themes** and global palette slots | `ThemeManager.cs:16-44` |
| **Degradation** to plain text when output is redirected, colour is unavailable, the font lacks the glyphs, or the terminal is too narrow to lay anything out | `BasicConsoleFormatter` (the markup-stripping renderer); the *absence* of any such guard is Q17/Q28 |
| Horizontal **rules**, **line wrapping**, **transcript** composition (role banners, alignment, blank-line separation) | `BasicConsoleFormatter.cs:41-66`; `ChatWindow.cs:500-622`, `:735-788` |
| Probability **number formatting** (precision, percent sign, culture pinning) | `TokenFormatters.cs:52-55`; `BasicConsoleFormatter.cs:202-205` |
| **Token text escaping** for display (control characters, null tokens, markup-literal brackets) | section F of the rendering dossier, all four variants |

#### What this package explicitly does NOT own

| Not owned | Owning package | Boundary rule |
|---|---|---|
| Requesting log-probabilities from a backend, the top-K *request* budget, the log→probability arithmetic, the demo data generator | `ChatDbg.Tools.TokenProbability` (root `LOGPROB`) — PRD 7.9 | We never compute a probability; we receive `logProb` and render `exp(logProb)`. `logProbabilitiesTopK` (default 5, range 1–20) bounds what *can* be drawn but is set there, not here. |
| Tokenization, attribution, inspection reports, analysis export | `ChatDbg.Tools.TokenInspection` (root `TOKEN`) — PRD 7.10 | |
| The durable settings file, its wire key names, and its validation semantics | `ChatDbg.Tools.Configuration` (root `CONFIG`) — PRD 7.2 | We read presentation preferences from the framework environment and delegate every durable write to `CONFIG`. See §7.3 design note D2. |
| Message storage, import/export, the `has-probabilities` predicate | `ChatDbg.Tools.ChatHistory` (root `HISTORY`) — PRD 7.4 | `VIEW TRANSCRIPT` renders messages it is handed; it never reads or mutates history. |
| Prompt text and prompt files | `ChatDbg.Tools.SystemPrompts` (root `PROMPT`) — PRD 7.5 |
| Credentials, secret resolution, OS keystore | `ChatDbg.Tools.Credentials` (root `CRED`) — PRD 7.3 |
| Diagnostic log sinks and log export | `ChatDbg.Tools.Diagnostics` (root `DIAG`) — PRD 7.11 |
| Provider adapters and inference | `ChatDbg.Tools.Providers` / `ChatDbg.Tools.LocalModel` — PRD 7.6–7.8 |
| **The screen itself** — the REPL loop, the prompt string, the menu bar, dialogs, panel docking, scroll geometry, the status-message timer | **The host** (`ChatDbg.Shell.Core` + its two composition roots) — PRD 7.13/7.14 | This is the hard line. Following the Cupcake pattern, the host owns one `AbstractTextIo` subclass and decides pipe-vs-terminal routing; tools emit `IResult<string>` chunks and never touch the console. The full-screen host's probability *panel* is a host view that consumes `VIEW TOKENS` output; the panel's 50-column default, `max(longest + 5, 50)` sizing, 2/4-column indents and 3000 ms status lifetime are host constants, recorded here only so the renderer's output fits them. |

> **The one deliberate exception**, argued in §7.3 D4: `VIEW CAPS` is the single tool permitted to interrogate the console device and the *process* environment. It exists so that no other tool in this package — or any other package — has to.

---

### 7.1 Package manifest

| Property | Value |
|---|---|
| **Assembly name** | `ChatDbg.Tools.PresentationVisualization.dll` |
| **Root command** | `VIEW` (`[CommandRoot("VIEW", "Presentation and visualization tools")]` on every class; normalized to uppercase by `NamesValidator`) |
| **Contract assembly version targeted** | `Xcaciv.Command.Interface` **3.3.4** and `Xcaciv.Command.Core` **3.3.4**. Nothing else. A tool assembly must not carry a private copy of the interface assembly built against another version — `Crawler` reports exactly that as a `ReflectionTypeLoadException` and skips the whole package (`Crawler.cs:150-166`). |
| **Target framework** | `net10.0` (framework default per `Directory.Build.props:5`; the source product also targets `net10.0`). `net8.0` multi-target is available via the framework's `UseNet08` opt-in if the host needs it. `ImplicitUsings` and `Nullable` enabled; `IsPackable` true; no `AllowUnsafeBlocks`. |
| **Elevated trust required** | **No.** |
| **Network** | **None.** No socket, no HTTP client, no DNS. |
| **Filesystem** | **None.** No path is read or written by any tool. (Rendering *to a file* is the host's redirection concern; `VIEW PLAIN` exists to make redirected output correct.) |
| **OS keystore** | **None.** |
| **Native libraries / P-Invoke** | **None in the package.** Enabling virtual-terminal processing on legacy Windows consoles is a **host** responsibility performed once at start-up; `VIEW CAPS` only *observes* the result. |
| **Console device access** | `VIEW CAPS` only — reads console width/height, `IsOutputRedirected`, and a fixed list of process environment variables (§7.2.11). Every other tool reads the published capability values from the framework environment. |
| **Process environment variables read** | Only by `VIEW CAPS`: `NO_COLOR`, `FORCE_COLOR`, `CLICOLOR`, `CLICOLOR_FORCE`, `COLORTERM`, `TERM`, `TERM_PROGRAM`, `WT_SESSION`, `ConEmuANSI`, `COLUMNS`, `LINES`, `LANG`, `LC_ALL`, `LC_CTYPE`. **NEW** — the source product read *no* environment variable on any rendering path (verified by repository-wide search). |
| **Reflection emit / dynamic assemblies** | None. The package loads cleanly under `AssemblySecurityPolicy.Strict` (the framework default, `Crawler.cs:31`) and passes preflight. |
| **Safe to load in a restricted host** | **Yes** — this is the safest package in the product. It is a pure function of (input chunks, parameters, environment) plus one isolated capability probe. Recommended for a locked-down deployment even when every other package is disabled. |
| **Discovery layout** | `«packageRoot»/ChatDbg.Tools.PresentationVisualization/bin/ChatDbg.Tools.PresentationVisualization.dll` — the `*/bin/*.dll` mask `Crawler.CrawlPackagePaths` expects (`Crawler.cs:189-209`). |
| **Registration requiring `modifiesEnvironment: true`** | `VIEW LAYOUT`, `VIEW THEME`, `VIEW PALETTE`, `VIEW CAPS`. These four write the shared `CHATDBG_VIEW_*` globals; every other tool is a pure reader and **must** be registered with the default `modifiesEnvironment: false`. See §7.3 D2. |
| **Audit** | Every execution emits exactly one `AuditEvent` from `CommandExecutor`'s `finally` (`CommandExecutor.cs:231-255`). No parameter in this package carries a secret, so masking is irrelevant here — which is fortunate, because `AuditMaskingConfiguration.ApplyMasking` only rewrites `-name=value` and the framework's own `-name value` form is never masked (`AuditMaskingConfiguration.cs:95-119`). |

#### Shared environment contract

All presentation state is held as **global** framework environment keys, written only by the four setter tools and read by everyone. This is the package's entire mutable surface.

| Key | Type | Default | Range / allowed | Written by | Source ancestry |
|---|---|---|---|---|---|
| `CHATDBG_VIEW_LAYOUT` | string | `list` | `grid` \| `list` \| `flow` | `VIEW LAYOUT` | `gridViewForTokens` (default **false** = list) |
| `CHATDBG_VIEW_SHOWALL` | bool | `false` | `true` \| `false` | `VIEW LAYOUT` | `showAllTokens` (default **false** = sample) |
| `CHATDBG_VIEW_MAXALT` | int | `5` | **1–20** | `VIEW LAYOUT` | `gridViewMaxAlternatives` (default **5**, range **1–20**) |
| `CHATDBG_VIEW_THEME` | string | `auto` | `auto` \| `dark` \| `light` \| `contrast` \| `mono` | `VIEW THEME` | **NEW** (source had one hard-coded dark theme, no setting) |
| `CHATDBG_VIEW_COLOR` | string | `auto` | `auto` \| `always` \| `never` | `VIEW THEME` | **NEW** |
| `CHATDBG_VIEW_UNICODE` | string | `auto` | `auto` \| `on` \| `off` | `VIEW THEME` | **NEW** |
| `CHATDBG_VIEW_PALETTE` | string | `bands10` | `bands5` \| `bands10` \| `bands6bg` \| `mono` | `VIEW PALETTE` | D1–D4; default changed — see §7.3 D3 |
| `CHATDBG_VIEW_SCALE` | string | `unit` | `unit` (0–1) \| `percent` (0–100) | `VIEW PALETTE` | **NEW** — makes Q1 impossible to reproduce silently |
| `CHATDBG_VIEW_PRECISION` | int | `5` | 0–5 | `VIEW LAYOUT` | table/grid used 5 decimals; the shared formatter used 2 |
| `CHATDBG_VIEW_WIDTH` | int | `0` | 0 = auto, else 20–1000 | `VIEW THEME` | responsive width query (`terminalWidth / 40`) |
| `CHATDBG_VIEW_CAPS_*` | various | — | see §7.2.11 | `VIEW CAPS` | **NEW** |

Reads are always `env.GetValue(key, fallback, storeDefault: false)` — a pure read. `storeDefault` defaults to **true** in the framework, which would flip `HasChanged` and trigger a write-back on a mere read (`IEnvironmentContext.cs:50`); no tool in this package may rely on that default.

---

### 7.2 Tool catalog

Thirteen tools. Every class carries `[CommandRoot("VIEW", "Presentation and visualization tools")]`; it is omitted from the snippets below for brevity. Every parameter attribute goes **on the class** (`AttributeTargets.Class, AllowMultiple = true`) — never on a property or field.

#### The piped-chunk contract used across this package

Three chunk shapes travel this package's pipes. A tool declares which it accepts.

| Shape | One chunk means | Wire form |
|---|---|---|
| **Token record** (`ResultFormat.JSON`) | exactly one generated token, in emission order | one single-line JSON object: `{"index":0,"token":" the","logProb":-0.6931,"alternatives":[{"token":" a","logProb":-1.9}]}`. `index` is the token's **true absolute position** in the full response and survives every filter — this is what preserves the source's "displayed number = `startIndex + position`" guarantee. `alternatives` may be absent, empty or null. `token` may be null. |
| **Chat message** (`ResultFormat.JSON`) | one message | `{"role":"assistant","content":"…","timestamp":"…","hasProbabilities":true}` |
| **Text line** (`ResultFormat.General`) | one line of already-rendered or plain text | may itself contain `\n`; downstream tools treat an embedded newline as a soft break, never as a chunk boundary |

Universal rules, derived from `AbstractCommand.Main` (`AbstractCommand.cs:79-91`) and `CommandExecutor` (`:190-212`):

* A **failed** upstream chunk is forwarded verbatim, unrendered, and the pipeline continues. No tool here ever renders an error as if it were data.
* An **empty-output** success chunk is dropped by the host; returning `Success(string.Empty, …)` is how a filter swallows an input.
* A chunk that cannot be parsed becomes a `Failure` naming the offending chunk's `CorrelationId`; the pipeline is **not** aborted.
* Quote any argument containing `.`, `/`, `:` or `\` — the argument tokenizer strips them (`NamesValidator.cs:22`).

---

#### 7.2.1 `VIEW TOKENS` — render token records as a grid or a table

The workhorse. Everything the source's `DisplayTokenGrid` / `DisplayTokenTable` pair did, unified into one tool with one alternatives rule.

```csharp
[CommandRegister("Tokens", "Render token probability records as a grid or a table",
    Prototype = "VIEW TOKENS [<layout>] [-alts <n>] [-columns <n>] [-start <n>] [-precision <n>] "
              + "[-palette <name>] [-scale unit|percent] [-width <n>] [-plain] [-all] [-nowrapguard]")]
[CommandParameterOrdered("layout", "Layout to draw", IsRequired = false,
    AllowedValues = new[] { "list", "grid" })]
[CommandParameterNamed("alts",      "Alternatives drawn per token before '+ N more'", DataType = typeof(int))]
[CommandParameterNamed("columns",   "Grid columns; 0 = responsive", DataType = typeof(int), DefaultValue = "0")]
[CommandParameterNamed("start",     "Numbering offset of the first record", DataType = typeof(int), DefaultValue = "0")]
[CommandParameterNamed("precision", "Decimal places in the probability cell", DataType = typeof(int))]
[CommandParameterNamed("palette",   "Heat palette", AllowedValues = new[] { "bands10", "bands5", "bands6bg", "mono" })]
[CommandParameterNamed("scale",     "Probability scale of the input", AllowedValues = new[] { "unit", "percent" })]
[CommandParameterNamed("width",     "Render width in columns; 0 = detect", DataType = typeof(int), DefaultValue = "0")]
[CommandFlag("plain",  "Emit unstyled ASCII regardless of terminal capability", ShortAlias = "p")]
[CommandFlag("all",    "Ignore the sampling preference and draw every record supplied")]
[CommandHelpRemarks("Piped input is one JSON token record per chunk. Records are drawn in the order received; nothing is reordered.")]
[CommandHelpRemarks("Grid layout buffers a row at a time; list layout streams. Neither buffers the whole response.")]
```

| Registration | |
|---|---|
| Command | `TOKENS` |
| Root | `VIEW` |
| Description | Render token probability records as a grid or a table |
| Prototype | `VIEW TOKENS [<layout>] [-alts <n>] [-columns <n>] [-start <n>] [-precision <n>] [-palette <name>] [-scale unit\|percent] [-width <n>] [-plain] [-all]` |

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `layout` | ordered | `string` | no | `CHATDBG_VIEW_LAYOUT` → `list` | `list`, `grid` | Card grid or detail table. `list` is the source default (`gridViewForTokens = false`). |
| `alts` | named | `int` | no | `CHATDBG_VIEW_MAXALT` → `5` | **1–20** | Alternatives per token before `+ N more`. Source default **5**, range **1–20**. |
| `columns` | named | `int` | no | `0` | 0 = responsive, else 1–40 | Grid only. `0` reproduces `max(1, width / 40)` — 40 columns per card, minimum 1. Any value `> 0` overrides. |
| `start` | named | `int` | no | `0` | ≥ 0 | Numbering offset, so a slice shows its true position in the full response. Displayed number = `start + position + 1` (1-based). |
| `precision` | named | `int` | no | `CHATDBG_VIEW_PRECISION` → `5` | 0–5 | Decimals in the probability cell. Source: **5** in both table renderers, **2** in the shared value formatter. |
| `palette` | named | `string` | no | `CHATDBG_VIEW_PALETTE` → `bands10` | `bands10`, `bands5`, `bands6bg`, `mono` | See §7.2.9 for each band table. |
| `scale` | named | `string` | no | `CHATDBG_VIEW_SCALE` → `unit` | `unit`, `percent` | Declares whether an incoming probability is 0–1 or 0–100. **NEW.** |
| `width` | named | `int` | no | `0` | 0 = detect, else 20–1000 | Overrides the detected terminal width. Clamped at a floor of **20**. |
| `plain` | flag | `bool` | n/a | `false` | — | Force the dependency-free ASCII renderer: no colour, no box-drawing, no markup. |
| `all` | flag | `bool` | n/a | `false` | — | Draw every record received, ignoring `CHATDBG_VIEW_SHOWALL`. Sampling itself is `VIEW SAMPLE`'s job; this flag only suppresses the *warning* that a sampled stream is being drawn. |

**Rendering contract — list layout (ASCII / `-plain`), preserved byte-for-byte from `BasicConsoleFormatter`:**

* Five-line minimum frame: border, caption row, border, one row per record, border. An **empty** record set still draws the four-line empty frame (source behaviour, preserved deliberately — it is how a user distinguishes "no data" from "not run"). A **null** set draws nothing and succeeds (Q23 fixed).
* Column inner widths **7 / 20 / 12 / 38**; separator segments **9 / 22 / 14 / 40**; total line width **90**.
* Captions `Token #`, `Text`, `Probability`, `Top Alternatives`.
* Token text: escape-formatted, then truncated at **20** characters by keeping the first **17** and appending `...`.
* Alternative token text: truncated at **10** characters by keeping the first **7** and appending `...`.
* Probability: `-precision` decimals plus **exactly one** `%`, invariant culture, **no space before the sign** — this fixes Q2 (the doubled `%`) and pins the culture so a packaged invariant-globalization build and a development en-US build render identically.
* Empty/absent alternatives → the literal `(none)`; a null token → `(null)`.
* Overflow: cells that exceed their column are truncated with `...` rather than breaking the frame (source overflowed and broke alignment).

**Rendering contract — list layout (styled):** rounded border, expanding, four columns — centred number column captioned `#`, `Token` width **20**, centred `Probability`, `Top Alternatives` width **50**. The number is drawn dim. Q8's `№`-vs-`?` encoding casualty is resolved in favour of a single intentional `#`.

**Rendering contract — grid layout:** one rounded, expanding card per record; header = the 1-based number prefixed `#` in dim; body lines `Token: «escaped»`, `Prob: «coloured percent»`, then `Alternatives:` and one `- «escaped» («coloured percent»)` line per shown alternative, then a dim `+ N more` when any were withheld. The alternatives block is omitted entirely when there are none. Rows are flushed as they fill; a short final row is padded with empty cards so the grid stays rectangular.

**Alternatives rule (deliberate unification).** Shown = `min(alts, available)`; surplus announced as `+ N more`. The source hard-capped the plain table at **2** and the styled table at **3**, honouring the setting only in grid cards — which is precisely the inconsistency README documented wrongly (Q25). One rule now applies to all three surfaces. The withheld-count suffix is ` (+N more)` with no space after the plus in ASCII list layout (source `BasicConsoleFormatter` form) and `+ N more` on its own dim line in grid cards (source `SpectreConsoleFormatter` form).

| Aspect | Behaviour |
|---|---|
| **Pipeline** | **Accepts and produces.** One input chunk = one JSON **token record**. Output is `ResultFormat.General` — rendered terminal text, one chunk per emitted line (list) or per completed row (grid). It declares `General` rather than a structured format because the payload is glyphs for a human, not data for a machine; a downstream stage should be `VIEW PLAIN` or a sink, never a parser. |
| | Non-piped invocation with no records is legal: it emits the source's exact notice `Note: Log probabilities were requested but none were returned by the model.` / `This could be due to the model not supporting this feature or an API limitation.` and reports success. |
| | Overrides `Main` (legal — `AbstractCommand.Main` is not sealed, §3.3) because neither the table frame nor grid row-packing maps 1:1 onto input chunks. The override reproduces the base template's contract exactly: failures forwarded verbatim, empty outputs skipped, `OnStartPipe`/`OnEndPipe` honoured. |
| **Environment** | Reads `CHATDBG_VIEW_LAYOUT`, `_MAXALT`, `_SHOWALL`, `_PRECISION`, `_PALETTE`, `_SCALE`, `_WIDTH`, `CHATDBG_VIEW_CAPS_*`. Writes nothing. **Does not need environment-modifying permission.** |
| **Failure modes** | Malformed JSON chunk → `Failure("VIEW TOKENS: chunk {correlationId} is not a token record: {reason}")`; pipeline continues, other chunks still render. · `alts`/`precision`/`columns` out of range → parse-time `ArgumentException`, which the executor reduces to `Error executing TOKENS (see trace for more info)` with detail in the trace — so the tool *also* re-validates and returns the source's own wording, `Grid max alternatives value must be a number between 1 and 20`, as a `Failure` for any value that reaches it. · Terminal width unavailable → 80 columns, trace message, render proceeds. · Width below 20 → clamped to 20 (fixes Q17: the source's wrapper looped forever at width 4–5 and threw at 0–3). · Upstream failure chunk → forwarded untouched. · Null token / null alternatives → `(null)` / `(none)`, never a throw (fixes Q22). |
| **Security & audit** | No parameter or output carries a secret. Output *content* is model- or user-supplied text rendered verbatim — a host that ships audit records off-box should audit command names and parameters only, never chunk payloads. Non-destructive; no confirmation required. |
| **Traceability** | PRD **7.12 Output Rendering** (primary); **7.9 Token Probability Analysis** (consumer of its records); **7.14 Full-Screen Terminal Shell** (the probability panel consumes this output). Descends from `IConsoleFormatter.DisplayTokenGrid` / `DisplayTokenTable`, `BasicConsoleFormatter.cs:72-112`, `SpectreConsoleFormatter.cs:55-195`, and the live plain-REPL visualiser at `ChatShell.cs:412-687`. |

---

#### 7.2.2 `VIEW SAMPLE` — reduce a long token stream to a representative sample

```csharp
[CommandRegister("Sample", "Reduce a long record stream to beginning/middle/end slices",
    Prototype = "VIEW SAMPLE [-size <n>] [-preset token|flow] [-captions] [-dedupe] [-max <n>] [-all]")]
[CommandParameterNamed("size",   "Records per slice", DataType = typeof(int), DefaultValue = "5")]
[CommandParameterNamed("preset", "Slice profile", DefaultValue = "token", AllowedValues = new[] { "token", "flow" })]
[CommandParameterNamed("max",    "Maximum records buffered", DataType = typeof(int), DefaultValue = "100000")]
[CommandFlag("captions", "Emit 'Beginning/Middle/End Tokens:' caption chunks between slices")]
[CommandFlag("dedupe",   "Drop records that appear in more than one slice")]
[CommandFlag("all",      "Pass every record through unchanged")]
[CommandHelpRemarks("Slices are computed from the true record count, so this tool buffers the stream before emitting.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `size` | named | `int` | no | `5` | 1–50 | Records per slice. Source constant **5** (`ChatShell.cs:435`, `TokenProbabilityVisualizer.cs:39`, `DemoLogProbsCommand.cs:182`). Upper bound is **NEW**. |
| `preset` | named | `string` | no | `token` | `token`, `flow` | `token` = size 5, threshold `size × 3` = **15**, middle start `count/2 − size/2` (= `count/2 − 2`), end start `count − 5`. `flow` = size **10**, threshold **30**, middle start `floor((count − 10) / 2)` — the *different* formula the heat-map view used (E2), preserved rather than harmonised. |
| `max` | named | `int` | no | `100000` | 1–10 000 000 | **NEW.** Buffer ceiling. Beyond it the tool emits the first `max` records and a trace warning rather than growing without bound. |
| `captions` | flag | `bool` | n/a | `false` | — | Emit the source's blue slice captions `Beginning Tokens:`, `Middle Tokens:`, `End Tokens:` as text chunks, with a blank chunk before the middle and end captions. Off by default because in a pipe the captions would be parsed as token records by the next stage. |
| `dedupe` | flag | `bool` | n/a | `false` | — | **NEW.** Off by default so the source's overlapping slices are reproduced exactly (16 records → indices 0–4, 6–10, 11–15, with 11 appearing twice). |
| `all` | flag | `bool` | n/a | `false` | — | Identity pass-through; equivalent to `showAllTokens = true`. |

| Aspect | Behaviour |
|---|---|
| **Pipeline** | **Accepts and produces**, and is the canonical middle stage. One chunk in = one **token record** (or any JSON object carrying an `index`); one chunk out = the same chunk, unmodified, when it survives the sample. It **echoes the upstream chunk's `OutputFormat`** rather than declaring one of its own, because it reshapes the *set*, never the record. Record `index` values are never rewritten, so a downstream `VIEW TOKENS` numbers each slice with its true absolute position — the property the source's demo command lost by concatenating slices and renumbering from 0. |
| | Non-piped: emits `Failure("VIEW SAMPLE reads records from a pipe. Try: LOGPROB SHOW -last | VIEW SAMPLE | VIEW TOKENS")` — the explanatory-refusal pattern (Cupcake rule 29), never a throw. |
| | Overrides `Main`: the middle and end slices are unknowable until the stream ends, so records are buffered to `max`, then emitted in one pass. |
| **Environment** | Reads `CHATDBG_VIEW_SHOWALL` (a `true` value makes the tool a pass-through unless `-size` was given explicitly). Writes nothing. No environment-modifying permission. |
| **Failure modes** | Record without a usable position → kept, in arrival order, with a trace note. · `size` out of range → `Failure("Sample size must be a number between 1 and 50")`. · Buffer ceiling reached → first `max` records emitted, warning traced, success. · Zero records → emits nothing and succeeds (matching the source helper, which returns nothing for a null or empty list and lets the caller print `No token probability data available`). · Upstream failure chunk → forwarded verbatim and **not** counted toward the sample. |
| **Security & audit** | No secrets. Non-destructive. |
| **Traceability** | PRD **7.12 Output Rendering**. Descends from E1 (`ChatShell.cs:435-481`, `TokenProbabilityVisualizer.cs:39-88`, `DemoLogProbsCommand.cs:179-200`) and E2 (`LogProbHeatmapView.cs:79-88`). |

---

#### 7.2.3 `VIEW HEAT` — flowing heat-mapped paragraph

Revives the source's `LogProbHeatmapView` — a complete, carefully written surface that **nothing in the product ever constructed** (Q13). It is the most legible confidence view the source contained and it never shipped.

```csharp
[CommandRegister("Heat", "Paint generated text as a flowing paragraph coloured by confidence",
    Prototype = "VIEW HEAT [-palette <name>] [-scale unit|percent] [-width <n>] [-height <n>] [-legend] [-plain] [-all]")]
[CommandParameterNamed("palette", "Heat palette", DefaultValue = "bands6bg",
    AllowedValues = new[] { "bands6bg", "bands10", "bands5", "mono" })]
[CommandParameterNamed("scale",   "Probability scale of the input", AllowedValues = new[] { "unit", "percent" })]
[CommandParameterNamed("width",   "Wrap width; 0 = detect", DataType = typeof(int), DefaultValue = "0")]
[CommandParameterNamed("height",  "Maximum rows; 0 = unlimited", DataType = typeof(int), DefaultValue = "0")]
[CommandFlag("legend", "Append a band legend under the paragraph")]
[CommandFlag("plain",  "Encode confidence as symbols instead of colour")]
[CommandFlag("all",    "Draw every record instead of sampling")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `palette` | named | `string` | no | `bands6bg` | `bands6bg`, `bands10`, `bands5`, `mono` | `bands6bg` is the view's own background palette: `≥0.9` green, `≥0.7` bright green, `≥0.5` brown, `≥0.3` bright yellow, `≥0.1` red, else bright red — with the foreground black at `≥0.5` and white below, for contrast. |
| `scale` | named | `string` | no | `CHATDBG_VIEW_SCALE` → `unit` | `unit`, `percent` | |
| `width` | named | `int` | no | `0` | 0 = detect, else 20–1000 | Wrap column. |
| `height` | named | `int` | no | `0` | 0 = unlimited, else ≥ 1 | Row ceiling. The source stopped drawing silently at the view height with no indicator; here, reaching the ceiling appends a dim `… +N tokens not shown` line. |
| `legend` | flag | `bool` | n/a | `false` | — | **NEW.** Band → colour key, so the picture is self-describing in a screenshot. |
| `plain` | flag | `bool` | n/a | `false` | — | **NEW.** Confidence as a symbol ladder (`█ ▓ ▒ ░ ·`, ASCII `# = - . :` when Unicode is off) instead of colour — the accessibility gap the source had no answer for. |
| `all` | flag | `bool` | n/a | `false` | — | Overrides `CHATDBG_VIEW_SHOWALL`. |

| Aspect | Behaviour |
|---|---|
| **Pipeline** | **Accepts and produces.** One chunk in = one **token record**; output is `ResultFormat.General`, one chunk per completed row. Streams row-by-row when drawing every record; buffers when it must sample (see `VIEW SAMPLE`, which is the preferred way to sample — prefer `VIEW SAMPLE -preset flow | VIEW HEAT -all`). |
| | **Wrapping is fixed relative to the source.** A token wider than the wrap width is broken across rows rather than written past the right edge (Q27); a run of long tokens can no longer burn one row each. Cursor advance is measured in **display cells**, not code units (Q29). |
| **Environment** | Reads `CHATDBG_VIEW_PALETTE`, `_SCALE`, `_SHOWALL`, `_WIDTH`, `_UNICODE`, `CHATDBG_VIEW_CAPS_*`. Writes nothing. No environment-modifying permission. |
| **Failure modes** | Null token → `(null)` painted in the band colour, never a throw. · Malformed chunk → per-chunk `Failure`, pipeline continues. · No colour available → automatic fall-back to the `-plain` symbol ladder with a trace note, never a blank paragraph. · Width unavailable → 80, clamped to a floor of 20. |
| **Security & audit** | No secrets. Non-destructive. |
| **Traceability** | PRD **7.12 Output Rendering**; **7.14** for the surface it was written for. Descends from `LogProbHeatmapView.cs` (defined, never instantiated) and D4/E2. |

---

#### 7.2.4 `VIEW TRANSCRIPT` — render chat messages

```csharp
[CommandRegister("Transcript", "Render chat messages as an aligned, wrapped transcript",
    Prototype = "VIEW TRANSCRIPT [-width <n>] [-padding <n>] [-bubble <n>] [-marker auto|on|off] [-plain] [-timestamps]")]
[CommandParameterNamed("width",   "Viewport width; 0 = detect", DataType = typeof(int), DefaultValue = "0")]
[CommandParameterNamed("padding", "Left indent in columns", DataType = typeof(int), DefaultValue = "2")]
[CommandParameterNamed("bubble",  "Body width as a percentage of usable width", DataType = typeof(int), DefaultValue = "75")]
[CommandParameterNamed("marker",  "Probability-availability marker", DefaultValue = "auto",
    AllowedValues = new[] { "auto", "on", "off" })]
[CommandFlag("plain",      "Unstyled output: role banners only, no colour")]
[CommandFlag("timestamps", "Prefix each role banner with the message timestamp")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `width` | named | `int` | no | `0` | 0 = detect, else 20–1000 | Viewport width. Usable width = `width − 4`. |
| `padding` | named | `int` | no | `2` | 0–40 | Left indent. Source constant **2**. |
| `bubble` | named | `int` | no | `75` | 25–100 | Body wrap as a percentage of usable width. Source: **three quarters** (`((width − 4) × 3) / 4`). |
| `marker` | named | `string` | no | `auto` | `auto`, `on`, `off` | Appends the `◊` indicator (ASCII `*`) under an assistant message that carries probability data — the affordance the full-screen host turns into a clickable lozenge. `auto` = on when the message declares `hasProbabilities`. |
| `plain` | flag | `bool` | n/a | `false` | — | |
| `timestamps` | flag | `bool` | n/a | `false` | — | **NEW.** The source showed the timestamp only in the panel header. |

**Rendering contract:** one role banner line `[«role lowercased»]`, then the wrapped body, then one blank line. **User** messages are right-aligned; every other role is left-aligned at the padding offset. Wrapping splits on explicit newlines first, then breaks at the last space within the limit (the space is kept at the end of the emitted line); with no space available it hard-cuts — but only at a **grapheme-cluster** boundary and measured in display cells. Blank wrapped lines are skipped. Role palettes: `user` = white on dark grey, `assistant` = bright yellow on blue, `system` = green on black; an unrecognised role falls back to the system palette. Role matching is **case-insensitive everywhere** — the source lower-cased in the transcript and compared raw elsewhere, so an imported `Assistant` message got a marker the panel then refused to honour (Q19).

| Aspect | Behaviour |
|---|---|
| **Pipeline** | **Accepts and produces.** One chunk in = one **chat message** JSON object; one chunk out = that message's fully rendered block (banner + wrapped body + separator), `ResultFormat.General`. Uses `HandlePipedChunk` — the mapping is genuinely 1:1. Non-piped: explanatory refusal naming `HISTORY SHOW | VIEW TRANSCRIPT`. |
| **Environment** | Reads `CHATDBG_VIEW_WIDTH`, `_THEME`, `_COLOR`, `_UNICODE`, `CHATDBG_VIEW_CAPS_*`. Writes nothing. No environment-modifying permission. |
| **Failure modes** | Message without `role` → rendered with the system palette and banner `[unknown]`. · Message without `content` → banner only, then the separator. · Width ≤ 5 → clamped to the 20-column floor; **the infinite loop and the negative-substring throw of Q17 are structurally impossible**, because the wrap limit is `max(8, …)` before the loop is entered. · Malformed chunk → per-chunk `Failure`. |
| **Security & audit** | Renders message content **verbatim**, including anything a user pasted. No redaction is performed and none is claimed — a host that logs rendered transcripts is logging conversation content and must say so. Non-destructive. |
| **Traceability** | PRD **7.14 Full-Screen Terminal Shell** (primary), **7.12 Output Rendering**, **7.4 Chat History** (data source). Descends from `ChatWindow.cs:500-622` and the wrapper at `:735-788`. |

---

#### 7.2.5 `VIEW LAYOUT` — set or show the layout preferences

```csharp
[CommandRegister("Layout", "Set or show layout, alternatives budget and volume preferences",
    Prototype = "VIEW LAYOUT [grid|list|flow] [-alts <n>] [-precision <n>] [-all] [-sample] [-nopersist]")]
[CommandParameterOrdered("mode", "Layout to make current", IsRequired = false, UsePipe = true,
    AllowedValues = new[] { "list", "grid", "flow" })]
[CommandParameterNamed("alts",      "Alternatives per token (1-20)", DataType = typeof(int))]
[CommandParameterNamed("precision", "Probability decimal places (0-5)", DataType = typeof(int))]
[CommandFlag("all",       "Show every token by default")]
[CommandFlag("sample",    "Show beginning/middle/end samples by default")]
[CommandFlag("nopersist", "Change this session only; do not write the settings file")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `mode` | ordered (`UsePipe = true`) | `string` | no | — (report-only when absent) | `list`, `grid`, `flow` | `list` and `grid` are the source's two layouts; `flow` selects `VIEW HEAT` as the default renderer — **NEW**, and the reason the dead heat-map view becomes reachable. Fed from the pipe when piped, exactly like the framework's own `SET`. |
| `alts` | named | `int` | no | unchanged | **1–20** | Source default **5**; rejected outside 1–20 with the source's wording. |
| `precision` | named | `int` | no | unchanged | 0–5 | **NEW** as a setting; the values 5 and 2 are the source's. |
| `all` | flag | `bool` | n/a | `false` | — | Sets `CHATDBG_VIEW_SHOWALL = true` (`/logprobs showall`). |
| `sample` | flag | `bool` | n/a | `false` | — | Sets `CHATDBG_VIEW_SHOWALL = false` (`/logprobs showsample`). This is the source default. |
| `nopersist` | flag | `bool` | n/a | `false` | — | **NEW.** By default a change is both applied and persisted — matching the source, which saved on every mutation. |

**Messages, preserved verbatim:** `Token probability analysis will use grid view layout.` · `Token probability analysis will use list view layout.` · `Token probability analysis will show all tokens.` · `Token probability analysis will show token samples (beginning, middle, end).` · `Grid view will show up to {n} alternatives per token.` · errors `Grid max alternatives value must be a number between 1 and 20` and `Please specify a number: VIEW LAYOUT -alts <number>`.

| Aspect | Behaviour |
|---|---|
| **Pipeline** | **Accepts piped input, produces (almost) none.** One chunk in = one **text line** naming a layout; the tool applies it and returns `Success(string.Empty)`, which the host drops — so `SAY grid \| VIEW LAYOUT` is silent, the idiom `SET` uses. Non-piped, it emits one confirmation line, `ResultFormat.General`. Both `all` and `sample` given → `Failure`, no change. |
| **Environment** | **Writes** `CHATDBG_VIEW_LAYOUT`, `CHATDBG_VIEW_SHOWALL`, `CHATDBG_VIEW_MAXALT`, `CHATDBG_VIEW_PRECISION` as **globals**, and therefore **must be registered with `modifiesEnvironment: true`** (`controller.AddCommand(pkg, new LayoutCommand(), true)`); without it the writes land in a private bucket no other tool reads. Reads the same keys to report current state. |
| **Failure modes** | Out-of-range value → source-worded `Failure`, nothing written, nothing persisted. · Unknown mode → the allow-list rejects it at parse time; the tool additionally returns the source's `Unknown subcommand: {name}.` style listing. · Persistence unavailable (`CONFIG` package not loaded) → the session change **still applies**, and the user is told `Layout changed for this session; settings could not be saved (CONFIG tools are not loaded).` — degrade, never fail. |
| **Security & audit** | No secrets. **Mutating but reversible**; no confirmation required. The persist path is a durable write delegated to the settings owner — which is exactly what fixes Q18, where the source's full-screen host persisted a stale settings object and silently discarded the user's other stored values. |
| **Traceability** | PRD **7.12 Output Rendering**, **7.2 Settings & Configuration** (delegated persistence). Descends from `/logprobs grid`, `/logprobs list`, `/logprobs showall`, `/logprobs showsample`, `/logprobs gridmaxalt <n>`, and the `/set gridViewForTokens|showAllTokens|gridViewMaxAlternatives` keys. |

---

#### 7.2.6 `VIEW THEME` — colour theme, colour policy, glyph policy — **NEW**

The source had exactly one theme, hard-coded, applied unconditionally at start-up, with no setting, no switcher and no way back (`ThemeManager.cs`). This tool earns its place because the *same* renderer now has to serve a dark terminal, a light terminal, a redirected file, a CI log and a screen reader — and because "degrade to plain text when output is redirected" is impossible to honour without a policy knob.

```csharp
[CommandRegister("Theme", "Select the colour theme, colour policy and glyph policy",
    Prototype = "VIEW THEME [auto|dark|light|contrast|mono] [-color auto|always|never] "
              + "[-unicode auto|on|off] [-width <n>] [-preview] [-nopersist]")]
[CommandParameterOrdered("name", "Theme to make current", IsRequired = false, UsePipe = true,
    AllowedValues = new[] { "auto", "dark", "light", "contrast", "mono" })]
[CommandParameterNamed("color",   "Colour policy", AllowedValues = new[] { "auto", "always", "never" })]
[CommandParameterNamed("unicode", "Glyph policy",  AllowedValues = new[] { "auto", "on", "off" })]
[CommandParameterNamed("width",   "Fixed render width; 0 = detect", DataType = typeof(int), DefaultValue = "0")]
[CommandFlag("preview",   "Render a swatch of the theme instead of applying it")]
[CommandFlag("nopersist", "Change this session only")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered (`UsePipe = true`) | `string` | no | report-only | `auto`, `dark`, `light`, `contrast`, `mono` | `dark` is the source's exact palette (below). `auto` picks `dark` when the terminal declares a dark background or the policy is unknown — i.e. it reproduces the source's behaviour on an unclassified terminal. `mono` drops colour entirely and encodes with symbols. |
| `color` | named | `string` | no | `auto` | `auto`, `always`, `never` | `auto` = colour only when a console is attached, output is not redirected, and no `NO_COLOR` is set. `always` forces ANSI even when redirected (for `less -R`). `never` is the plain-text degradation switch. |
| `unicode` | named | `string` | no | `auto` | `auto`, `on`, `off` | `off` substitutes ASCII for `◊`→`*`, `✓`→`[ok]`, `✗`→`[!]`, `#`, and `+-|` for box drawing. `auto` decides from the declared locale and terminal. |
| `width` | named | `int` | no | `0` | 0 = detect, else 20–1000 | Pins the render width — the switch that makes golden-file tests possible. |
| `preview` | flag | `bool` | n/a | `false` | — | Emits a swatch of every slot and every heat band without changing anything. |
| `nopersist` | flag | `bool` | n/a | `false` | — | |

**The `dark` theme is the source's, slot for slot** (`ThemeManager.cs:16-44`):

| Slot | Normal | Focus | Hot-normal | Hot-focus | Disabled |
|---|---|---|---|---|---|
| Base | white / black | bright yellow / dark grey | bright cyan / black | bright yellow / dark grey | grey / black *(the source left this unset — Q14 — which can render black on black; it is now specified)* |
| Dialog | white / dark grey | bright yellow / dark grey | bright cyan / dark grey | bright yellow / dark grey | grey / dark grey *(specified)* |
| Menu | white / dark grey | bright yellow / black | bright cyan / dark grey | bright yellow / black | grey / dark grey |
| Error | bright red / black | bright red / dark grey | bright red / black | bright yellow / dark grey | grey / black *(specified)* |

`light` and `contrast` are **NEW**; `contrast` guarantees a ≥ 7:1 luminance ratio on every pair, which the source's bright-yellow-on-blue assistant banner does not meet.

| Aspect | Behaviour |
|---|---|
| **Pipeline** | Accepts one **text line** naming a theme (`UsePipe`), returning empty success. Produces one confirmation line, or the swatch under `-preview`, as `ResultFormat.General`. |
| **Environment** | **Writes** globals `CHATDBG_VIEW_THEME`, `CHATDBG_VIEW_COLOR`, `CHATDBG_VIEW_UNICODE`, `CHATDBG_VIEW_WIDTH` → **requires `modifiesEnvironment: true`**. Reads `CHATDBG_VIEW_CAPS_*` to resolve `auto`. It does **not** read the process environment; `NO_COLOR` and friends reach it only through the values `VIEW CAPS` published. |
| **Failure modes** | Unknown theme → allow-list rejection plus a `Failure` listing the five names. · `always` requested on a terminal with no colour support → applied anyway, with the warning `Colour forced; this terminal did not advertise colour support.` · `unicode on` with a non-UTF-8 locale → applied, with a warning naming the substituted glyphs. · Persistence unavailable → session-only, told plainly. |
| **Security & audit** | No secrets. Reversible; no confirmation. |
| **Traceability** | PRD **7.12 Output Rendering**, **7.14 Full-Screen Terminal Shell**. Descends from `ThemeManager.cs` (theme slots) — **the colour/glyph/width policy layer is NEW.** |

---

#### 7.2.7 `VIEW PALETTE` — choose and inspect the confidence heat map — **NEW**

The source contained **four mutually contradictory heat maps**, two of which were unreachable, one of which was fed the wrong scale so that *every real token rendered red* (Q1, Q28). A user who read the README saw a spectrum the product could not draw. This tool exists to make the palette an explicit, inspectable, testable choice, and to make the scale mismatch impossible to reproduce silently.

```csharp
[CommandRegister("Palette", "Select or inspect the probability-to-colour mapping",
    Prototype = "VIEW PALETTE [bands10|bands5|bands6bg|mono] [-scale unit|percent] [-show] [-check <p>] [-nopersist]")]
[CommandParameterOrdered("name", "Palette to make current", IsRequired = false, UsePipe = true,
    AllowedValues = new[] { "bands10", "bands5", "bands6bg", "mono" })]
[CommandParameterNamed("scale", "Probability scale the palette is fed",
    DefaultValue = "unit", AllowedValues = new[] { "unit", "percent" })]
[CommandParameterNamed("check", "Report the band a single probability lands in", DataType = typeof(double))]
[CommandFlag("show",      "Render every band with its threshold and colour")]
[CommandFlag("nopersist", "Change this session only")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `name` | ordered (`UsePipe = true`) | `string` | no | report-only | see below | |
| `scale` | named | `string` | no | `unit` | `unit`, `percent` | `unit` = 0…1 (what `exp(logProb)` produces); `percent` = 0…100. |
| `check` | named | `double` | no | — | any finite | **NEW.** Prints the band a value lands in — the one-line diagnostic that would have caught Q1. |
| `show` | flag | `bool` | n/a | `false` | — | |
| `nopersist` | flag | `bool` | n/a | `false` | — | |

**The four palettes, preserved exactly:**

| Palette | Scale it expects | Bands |
|---|---|---|
| `bands5` | **percent** (0–100) | `≥ 90` green · `≥ 70` lime · `≥ 50` yellow · `≥ 30` orange · else red. The 30–50 band is named `orange`; two shell-local copies named it `orange3`. **A bare `orange` is not in the styled library's palette (Q6), so this package emits `orange3` and records the divergence here.** |
| `bands10` | **unit** (0–1) | bucket = `clamp(floor(p × 10), 0, 9)`; 0 bright red · 1 red · 2 bright magenta · 3 magenta · 4 bright blue · 5 blue · 6 cyan · 7 bright cyan · 8 bright yellow · 9 bright green, all on black. The only palette the shipped product ever actually displayed. |
| `bands6bg` | **unit** (0–1) | background `≥0.9` green · `≥0.7` bright green · `≥0.5` brown · `≥0.3` bright yellow · `≥0.1` red · else bright red; foreground black at `≥ 0.5`, white below. |
| `mono` | either | **NEW.** Five symbol bands at the `bands5` thresholds, no colour at all: `█ ▓ ▒ ░ ·` (ASCII `# = - . :`). |

**The scale rule that fixes Q1:** a palette declares the scale it expects; the incoming record declares the scale it carries (`-scale`, defaulting to `unit`, which is what `exp(logProb)` produces). When they differ, the value is **converted**, not misread. Feeding `bands5` a unit value now yields the correct band instead of universal red; feeding `bands10` a percent value no longer clamps everything to bucket 9.

| Aspect | Behaviour |
|---|---|
| **Pipeline** | Accepts one **text line** naming a palette (`UsePipe`); returns empty success. `-show` and `-check` produce `ResultFormat.General` text. |
| **Environment** | **Writes** `CHATDBG_VIEW_PALETTE`, `CHATDBG_VIEW_SCALE` → **requires `modifiesEnvironment: true`**. |
| **Failure modes** | Unknown palette → allow-list rejection plus a listing. · `-check` with a value outside the declared scale → the band is still reported, prefixed `out of range:` (matching the source's clamping semantics rather than throwing). · No colour available → `-show` renders the `mono` ladder and says so. |
| **Security & audit** | No secrets. Reversible. |
| **Traceability** | PRD **7.12 Output Rendering**. Descends from D1–D4 (`TokenFormatters.cs:38-45`, `ChatShell.cs:657-672`, `ChatWindow.cs:99-113`, `LogProbHeatmapView.cs:60-77`). **The selection surface, the scale declaration and the `mono` palette are NEW.** |

---

#### 7.2.8 `VIEW PLAIN` — degrade styled output to plain text

The source's `BasicConsoleFormatter` was a complete dependency-free renderer that **no host ever selected** (Q10). Its markup-stripping half is the correct answer to redirected output, and it becomes a first-class pipeline filter here.

```csharp
[CommandRegister("Plain", "Strip styling markup, leaving plain text",
    Prototype = "VIEW PLAIN [-mode strip|escape|ansi] [-ascii] [-tabs <n>]")]
[CommandParameterNamed("mode", "How to treat markup", DefaultValue = "strip",
    AllowedValues = new[] { "strip", "escape", "ansi" })]
[CommandParameterNamed("tabs", "Spaces per tab; 0 = leave tabs alone", DataType = typeof(int), DefaultValue = "0")]
[CommandFlag("ascii", "Also fold non-ASCII decoration to ASCII equivalents")]
[CommandHelpRemarks("Put this last in a pipeline whose output is redirected to a file or a log.")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `mode` | named | `string` | no | `strip` | `strip`, `escape`, `ansi` | `strip` removes bracketed markup spans and ANSI SGR sequences. `escape` doubles brackets so they survive a *downstream* styling engine. `ansi` resolves markup into real escape sequences (for `-color always`). |
| `tabs` | named | `int` | no | `0` | 0–16 | **NEW.** Tab expansion, so a redirected table stays aligned. |
| `ascii` | flag | `bool` | n/a | `false` | — | Folds `◊ ✓ ✗ № ─ │ ╭` and friends to ASCII. |

**The stripping rule is the source's, with one deliberate correction.** A `[` opens a markup span and is dropped; the next `]` closes it and is dropped; a `]` outside a span is kept. The source's rule made an **unclosed `[` silently swallow the rest of the line**, and it was not the inverse of the styled escaper, so doubled brackets were mangled (Q-note in section F). Here: `[[` and `]]` are recognised as escaped literals and emitted as single brackets, and an unterminated `[` is emitted literally with a trace note rather than eating the line.

| Aspect | Behaviour |
|---|---|
| **Pipeline** | **Accepts and produces**, one chunk in → one chunk out, `HandlePipedChunk`. One chunk = one **text line**. It re-declares `ResultFormat.General` because after stripping, the payload is by definition plain text. Non-piped, it plainifies its own ordered argument if given, otherwise returns an explanatory refusal. |
| **Environment** | Reads `CHATDBG_VIEW_UNICODE`, `CHATDBG_VIEW_COLOR`, `CHATDBG_VIEW_CAPS_REDIRECTED`. Writes nothing. No environment-modifying permission. |
| **Failure modes** | Never fails on content — any string is plainifiable. Upstream failure chunks are forwarded verbatim, **still styled**, because a failure's `ErrorMessage` is the host's to render. `tabs` out of range → source-style `Failure`. |
| **Security & audit** | No secrets. Non-destructive. Note that stripping markup **does not** sanitise content: a model reply containing raw ANSI is neutralised by `strip`, which is a real (if incidental) safety property worth relying on when piping model output to a terminal. |
| **Traceability** | PRD **7.12 Output Rendering**, **7.13 Line-Oriented Shell**. Descends from `BasicConsoleFormatter.cs:17-22`, `:152-179` and `SpectreConsoleFormatter.cs:230-235`. |

---

#### 7.2.9 `VIEW WRAP` — width-aware line wrapping — **NEW**

Extracted because the source wrote this logic once, inside a full-screen window, where it had a guaranteed infinite loop at viewport width 4–5 and an unguarded throw at width 0–3 (Q17), and measured in code units so every wide or combining glyph mis-wrapped (Q29). A wrapper is a pure function; it belongs in its own testable tool, and every other renderer here calls the same implementation.

```csharp
[CommandRegister("Wrap", "Wrap text to a width, measured in display cells",
    Prototype = "VIEW WRAP [-width <n>] [-indent <n>] [-hanging <n>] [-align left|right|center] [-hard]")]
[CommandParameterNamed("width",   "Wrap width; 0 = detect", DataType = typeof(int), DefaultValue = "0")]
[CommandParameterNamed("indent",  "Left indent in columns", DataType = typeof(int), DefaultValue = "0")]
[CommandParameterNamed("hanging", "Extra indent for continuation lines", DataType = typeof(int), DefaultValue = "0")]
[CommandParameterNamed("align",   "Alignment within the width", DefaultValue = "left",
    AllowedValues = new[] { "left", "right", "center" })]
[CommandFlag("hard", "Break mid-word when no space fits, instead of overflowing")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `width` | named | `int` | no | `0` | 0 = detect; effective value clamped to **≥ 8** | Wrap column. |
| `indent` | named | `int` | no | `0` | 0–40 | Source transcript padding is **2**. |
| `hanging` | named | `int` | no | `0` | 0–40 | **NEW.** |
| `align` | named | `string` | no | `left` | `left`, `right`, `center` | `right` reproduces the transcript's right-aligned user messages. |
| `hard` | flag | `bool` | n/a | `true` in effect | — | Source behaviour: hard-cut at the limit when no space is available. Kept as the default; clearing it lets a long token overflow instead. |

**Contract:** split on explicit newlines first; then greedily break at the **last space within the limit**, keeping that space at the end of the emitted line (source behaviour, which matters for byte-identical output); with no space available, hard-cut at the limit — never inside a grapheme cluster. Widths are display cells: a wide East-Asian glyph counts 2, a combining sequence counts 1. Blank wrapped lines are skipped.

| Aspect | Behaviour |
|---|---|
| **Pipeline** | **Accepts and produces**, `HandlePipedChunk`, one chunk in → one chunk out containing the wrapped block with embedded newlines. Echoes the upstream `OutputFormat` when it is `General`; otherwise re-declares `General`, because wrapping a JSON record would corrupt it — and it refuses with a `Failure` if the upstream chunk declares `JSON`, rather than silently mangling data. |
| **Environment** | Reads `CHATDBG_VIEW_WIDTH`, `CHATDBG_VIEW_CAPS_COLUMNS`, `CHATDBG_VIEW_CAPS_WIDECHARS`. Writes nothing. |
| **Failure modes** | `width` between 1 and 7 → clamped to 8, warning traced, output produced. `width` 0 with no detectable terminal → 80. **There is no input for which this tool loops or throws** — that is its entire reason for existing as a separate, unit-tested tool. |
| **Security & audit** | No secrets. Non-destructive. |
| **Traceability** | PRD **7.12 Output Rendering**, **7.14**. Descends from `ChatWindow.cs:735-788` — **extracted, bounded and cell-aware; NEW as a tool.** |

---

#### 7.2.10 `VIEW RULE` — horizontal separator with an optional caption

```csharp
[CommandRegister("Rule", "Emit a horizontal rule with an optional caption",
    Prototype = "VIEW RULE [<caption>] [-width <n>] [-char <c>] [-align left|center] [-plain]")]
[CommandParameterSuffix("caption", "Caption text", IsRequired = false)]
[CommandParameterNamed("width", "Rule width; 0 = detect", DataType = typeof(int), DefaultValue = "80")]
[CommandParameterNamed("char",  "Character the rule is drawn with", DefaultValue = "-")]
[CommandParameterNamed("align", "Caption alignment", DefaultValue = "center",
    AllowedValues = new[] { "center", "left" })]
[CommandFlag("plain", "ASCII dashes rather than the theme's line glyph")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `caption` | suffix | `string` | no | *(empty)* | any text | All remaining tokens, joined by single spaces, as one string. Markup is stripped before measuring and before printing. Declared last; there is exactly one suffix parameter. |
| `width` | named | `int` | no | `80` | 0 = detect, else 20–1000 | Source constant **80**. |
| `char` | named | `string` | no | `-` | one character | **NEW.** |
| `align` | named | `string` | no | `center` | `center`, `left` | Source default is **centred**. |
| `plain` | flag | `bool` | n/a | `false` | — | |

**Geometry, preserved exactly:** no caption → **80** dash characters. Centred → `«left dashes» «space» «caption» «space» «right dashes»`, left dashes = `floor(remaining / 2)`, right = `remaining − left`, **total exactly 80**. Left-justified → `«caption» «space» «dashes»` with dash count `80 − (length + 2)`, **total 79** — one short of the centred form. That asymmetry is the source's and is preserved because tests and golden files depend on it; it is recorded here so nobody "fixes" it by accident.

**The one correction:** a caption longer than `width − 2` produced a negative dash count and a hard failure in the source (Q15). Here the caption is truncated with a trailing `…` (ASCII `...`) to fit, a trace message is emitted, and the rule is drawn.

| Aspect | Behaviour |
|---|---|
| **Pipeline** | **Produces only — a source, not a filter.** Given piped input it returns the explanatory string `VIEW RULE does not read piped input; it emits a separator. Place it before or after a pipeline, not inside one.` and succeeds without consuming the pipe (Cupcake rule 29). Output is one `ResultFormat.General` chunk. |
| **Environment** | Reads `CHATDBG_VIEW_WIDTH`, `_UNICODE`, `_THEME`, `CHATDBG_VIEW_CAPS_*`. Writes nothing. |
| **Failure modes** | `char` longer than one character → `Failure("Rule character must be exactly one character")`. · `width` out of range → clamped with a trace note. · Caption with unbalanced brackets → **stripped, not parsed**; the styled path escapes it before styling, so the markup error the source's styled rule raised cannot occur. |
| **Security & audit** | No secrets. Non-destructive. |
| **Traceability** | PRD **7.12 Output Rendering**. Descends from `IConsoleFormatter.WriteRule` and `BasicConsoleFormatter.cs:41-66`. |

---

#### 7.2.11 `VIEW CAPS` — probe and publish terminal capabilities — **NEW**

The single seam between this package and the physical world. It earns its place three times over: the source queried the terminal width on a path whose failure mode differs by platform and guarded only the zero case; it had no notion of redirected output, colour depth, or glyph support; and without a published capability record every renderer would need device access, which would make the whole package untestable and unloadable in a restricted host.

```csharp
[CommandRegister("Caps", "Probe the terminal and publish its capabilities",
    Prototype = "VIEW CAPS [-format text|json] [-refresh] [-assume <profile>] [-quiet]")]
[CommandParameterNamed("format", "Report shape", DefaultValue = "text",
    AllowedValues = new[] { "text", "json" })]
[CommandParameterNamed("assume", "Publish a fixed profile instead of probing",
    AllowedValues = new[] { "none", "dumb", "ansi16", "ansi256", "truecolor", "redirected" })]
[CommandFlag("refresh", "Re-probe even if capabilities were already published")]
[CommandFlag("quiet",   "Publish without emitting a report")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text`, `json` | |
| `assume` | named | `string` | no | `none` | `none`, `dumb`, `ansi16`, `ansi256`, `truecolor`, `redirected` | **NEW.** Forces a profile — the switch that makes every other tool in this package deterministically testable and lets CI pin its rendering. |
| `refresh` | flag | `bool` | n/a | `false` | — | Capabilities are probed once per session; a terminal resize is the reason to re-probe. |
| `quiet` | flag | `bool` | n/a | `false` | — | |

**Published globals:**

| Key | Type | Meaning | Fallback when unknown |
|---|---|---|---|
| `CHATDBG_VIEW_CAPS_COLUMNS` | int | usable width | `COLUMNS`, then **80** |
| `CHATDBG_VIEW_CAPS_ROWS` | int | usable height | `LINES`, then **24** |
| `CHATDBG_VIEW_CAPS_COLOR` | string | `none` \| `ansi16` \| `ansi256` \| `truecolor` | `none` |
| `CHATDBG_VIEW_CAPS_REDIRECTED` | bool | standard output is not a terminal | `true` (the safe assumption) |
| `CHATDBG_VIEW_CAPS_UNICODE` | bool | UTF-8 output encoding declared | `false` |
| `CHATDBG_VIEW_CAPS_WIDECHARS` | bool | East-Asian width table available | `false` |
| `CHATDBG_VIEW_CAPS_MOUSE` | bool | terminal reports mouse events | `false` |
| `CHATDBG_VIEW_CAPS_PLATFORM` | string | `windows` \| `linux` \| `macos` \| `other` | `other` |

**Process environment variables read** (the only tool that reads any): `NO_COLOR` (any value ⇒ colour `none`), `FORCE_COLOR` / `CLICOLOR_FORCE` (⇒ colour forced on), `CLICOLOR=0` (⇒ off), `COLORTERM` (`truecolor`/`24bit` ⇒ truecolor), `TERM` (`dumb` ⇒ none; `*-256color` ⇒ ansi256), `TERM_PROGRAM`, `WT_SESSION` and `ConEmuANSI` (Windows terminals that support VT), `COLUMNS` / `LINES`, `LANG` / `LC_ALL` / `LC_CTYPE` (UTF-8 detection).

**Cross-platform behaviour** — the source ran on any OS but assumed a console was always there:

* **Windows** — modern terminals (Windows Terminal, ConEmu, VS Code) report truecolor; a legacy `conhost` without virtual-terminal processing reports `ansi16`, and the host is expected to have enabled VT at start-up; when it has not, `VIEW CAPS` detects the failure and publishes `ansi16` rather than emitting escapes that would appear as garbage. Code page other than 65001 ⇒ `UNICODE=false`.
* **Linux / macOS** — resolved from `TERM`, `COLORTERM` and the locale. `TERM=dumb` ⇒ `color=none`, `unicode=false`.
* **Any OS, redirected** — `REDIRECTED=true`, `COLOR=none`, `COLUMNS` from the environment or **80**. This is the state in which the whole package renders exactly what the source's unused `BasicConsoleFormatter` would have rendered.
* **No console attached at all** (service, CI, `dotnet test`) — every probe is wrapped; a throwing width query degrades to 80 rather than escaping to the caller, which is the one platform-divergent failure the source did not handle.

| Aspect | Behaviour |
|---|---|
| **Pipeline** | **Produces only.** Piped input → explanatory refusal. Output is `ResultFormat.General` for `-format text` and **`ResultFormat.JSON`** for `-format json` — declared because this output is genuinely machine-readable and is meant to be filtered (`VIEW CAPS -format json \| REGIF …`). |
| **Environment** | **Writes** all `CHATDBG_VIEW_CAPS_*` globals → **requires `modifiesEnvironment: true`**. Reads the process environment (above). |
| **Failure modes** | Any probe that throws is caught individually; that one capability takes its fallback and a trace message names it. The tool cannot fail: worst case it publishes the fully conservative profile (80 × 24, no colour, redirected, ASCII) and reports success. |
| **Security & audit** | Publishes no secret. The values it writes are environment-shaped and land in the audit log's `EnvironmentChange` events, which *do* apply variable-name redaction correctly — none of these names match a redaction pattern, and none should. Non-destructive. |
| **Traceability** | PRD **7.12 Output Rendering**, **7.13 Line-Oriented Shell**, **7.14 Full-Screen Terminal Shell**. **NEW** — the source's only ancestor is the bare terminal-width query at `SpectreConsoleFormatter.cs:58` / `ChatShell.cs:503-506`. |

---

#### 7.2.12 `VIEW STATUS` — report the current presentation state

```csharp
[CommandRegister("Status", "Report the current presentation settings",
    Prototype = "VIEW STATUS [-format text|json] [-verbose]")]
[CommandParameterNamed("format", "Report shape", DefaultValue = "text",
    AllowedValues = new[] { "text", "json" })]
[CommandFlag("verbose", "Include capability detail and the resolved effective values")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text`, `json` | |
| `verbose` | flag | `bool` | n/a | `false` | — | Adds the `VIEW CAPS` record and, for each setting, whether the value came from a default, the environment or an explicit change. |

**Text report** reproduces the display half of the source's `/logprobs` status block, in order: `- Display Mode: Show all tokens` \| `Show token samples (beginning, middle, end)`; `- View Mode: Grid layout` \| `List layout`; `- Grid View Max Alternatives: {n}`; then **NEW** lines `- Theme: {name}`, `- Palette: {name} ({scale} scale)`, `- Colour: {policy} (detected {caps})`, `- Glyphs: {policy}`, `- Width: {n} ({source})`. The capture flag and top-K belong to `LOGPROB STATUS` and are deliberately **not** reprinted here — the source's single status block spanned two features and that is precisely why changing a display option from the full-screen host reported a value nothing was using (Q18).

| Aspect | Behaviour |
|---|---|
| **Pipeline** | **Produces only.** Piped input → explanatory refusal. `ResultFormat.General`, or `ResultFormat.JSON` under `-format json`. |
| **Environment** | Reads every `CHATDBG_VIEW_*` key. Writes nothing — and specifically uses `storeDefault: false` so that merely asking for the status cannot flip `HasChanged` and trigger a write-back. |
| **Failure modes** | A missing key reports its documented default and is annotated `(default)`. There is no input that fails. |
| **Security & audit** | No secrets. Non-destructive. |
| **Traceability** | PRD **7.12 Output Rendering**, **7.2 Settings**. Descends from the display half of `LogProbsCommand.cs:129-156` and `/set`'s report lines `- Show All Tokens`, `- Token Display`, `- Grid View Max Alternatives`. |

---

#### 7.2.13 `VIEW PREVIEW` — render a built-in fixture through the current settings — **NEW**

The source's `/demologprobs` had two jobs: fabricate sample probability data, and draw it. The data half belongs to `ChatDbg.Tools.TokenProbability` (`LOGPROB DEMO`). The drawing half belongs here — and needs to exist independently, because in the source the plain shell constructed the demo command **without** a renderer, so `/demologprobs` there drew nothing at all despite the README promising a visual demo (Q4); and in the full-screen shell it wrote styled tables straight over the terminal the UI owned (Q11).

```csharp
[CommandRegister("Preview", "Render a built-in fixture through the current presentation settings",
    Prototype = "VIEW PREVIEW [tokens|transcript|palette|theme|all] [-width <n>] [-seed <n>] [-plain]")]
[CommandParameterOrdered("subject", "What to preview", IsRequired = false, DefaultValue = "tokens",
    AllowedValues = new[] { "tokens", "transcript", "palette", "theme", "all" })]
[CommandParameterNamed("width", "Render width; 0 = detect", DataType = typeof(int), DefaultValue = "0")]
[CommandParameterNamed("seed",  "Fixture seed", DataType = typeof(int), DefaultValue = "42")]
[CommandFlag("plain", "Force the ASCII renderer")]
```

| Parameter | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `subject` | ordered | `string` | no | `tokens` | `tokens`, `transcript`, `palette`, `theme`, `all` | |
| `width` | named | `int` | no | `0` | 0 = detect, else 20–1000 | |
| `seed` | named | `int` | no | `42` | any | Source seed **42**, so the fixture is byte-stable across runs. |
| `plain` | flag | `bool` | n/a | `false` | — | |

**The `tokens` fixture** is the source's, and is deliberately *corrected*: the sample sentence is unchanged — `This is a sample response with token probability analysis. You can see how the model assigned probabilities to each token and what alternatives it considered.` — tokenised by splitting on space, newline, tab, `.`, `,`, `!`, `?` and dropping empties, yielding **25 tokens**. With the default sample mode (25 > 15) that means 15 drawn records at source indices 0–4, 10–14, 20–24, **numbered with their true positions** rather than renumbered 1–15. Confidence values are generated in the source's `70.0…98.0` range but are written as **probabilities**, not as log-probabilities: the source wrote percentages into the log-probability field, so the demo rendered figures like `2.5 × 10^32 %` and coloured everything green — the one thing a colour demonstration must not do (Q5). The alternative sets are the source's five hand-written groups (`sample`→`example`/`test`/`demo`, `response`→`reply`/`answer`/`output`, `token`→`word`/`symbol`/`element`, `probability`→`likelihood`/`chance`/`confidence`, `analysis`→`evaluation`/`assessment`/`examination`, matched case-insensitively) and the generic `«token»_1/_2/_3` ladder for everything else, rescaled the same way.

| Aspect | Behaviour |
|---|---|
| **Pipeline** | **Produces only.** Piped input → explanatory refusal. `ResultFormat.General`. |
| **Environment** | Reads every `CHATDBG_VIEW_*`. Writes nothing. |
| **Failure modes** | None reachable — the fixture is embedded and total. If the current settings would produce nothing (for example a zero-width terminal), the width floor applies and the preview still renders. |
| **Security & audit** | No secrets. Non-destructive. |
| **Traceability** | PRD **7.12 Output Rendering**. Descends from the *rendering* half of `DemoLogProbsCommand.cs:44-116` — **NEW as a tool**, with the data-fabrication half left to `LOGPROB DEMO`. |

---

### 7.3 Pipeline compositions

**1 — The canonical confidence read (sampled grid).**

```
LOGPROB SHOW -last | VIEW SAMPLE -size 5 | VIEW TOKENS grid -alts 5 -columns 0
```

`LOGPROB SHOW` (package `ChatDbg.Tools.TokenProbability`) streams one JSON token record per chunk for the most recent assistant message. `VIEW SAMPLE` buffers, then re-emits the first 5, 5 from `count/2 − 2`, and the last 5 — dropping nothing when the response is 15 tokens or shorter. `VIEW TOKENS` packs them into `max(1, terminalWidth / 40)` columns of rounded cards, each headed `#«true index»`, each showing up to 5 alternatives and a dim `+ N more`. **What the user gets:** the source's intended grid, with every card numbered by its real position in the response — which the source's own demo path lost.

**2 — Redirected output, fully degraded.** *(crosses into the host's redirection, and is the plain-text degradation path)*

```
VIEW CAPS -quiet | LOGPROB SHOW -last | VIEW TOKENS list -plain -precision 5 | VIEW PLAIN -ascii -tabs 4
```

`VIEW CAPS` publishes `REDIRECTED=true`, `COLOR=none`, `COLUMNS=80`. `VIEW TOKENS -plain` draws the 90-character ASCII frame with inner widths 7/20/12/38, tokens truncated at 17 + `...`, probabilities as `50.00000%` — one percent sign, invariant, no space. `VIEW PLAIN` folds any residual decoration to ASCII and expands tabs. **What the user gets:** a table that is byte-identical whether it lands in a terminal, a log file or a CI artifact — and identical between a development build and an invariant-globalization packaged build, which the source's two forms (`50.00000 %%` vs `50.00000%%`) were not.

**3 — Capability-driven theme selection.** *(crosses into the framework's own built-in `REGIF`)*

```
VIEW CAPS -format json | REGIF "\"color\": *\"none\"" | VIEW THEME mono
```

`VIEW CAPS` emits one JSON chunk. `REGIF` (built-in) passes it through only when the colour capability is `none`; otherwise it returns an empty success and the chunk is dropped by the host. `VIEW THEME` has `UsePipe = true` on its ordered `name` parameter, so it applies `mono` **only if a chunk arrives**, and stays silent either way. **What the user gets:** a shell that switches itself to the symbol-encoded palette on a colourless terminal, expressed as one line with no host logic.

**4 — Reviewing a conversation.** *(crosses into `ChatDbg.Tools.ChatHistory`)*

```
HISTORY SHOW -last 10 | VIEW TRANSCRIPT -width 100 -marker on -timestamps | VIEW PLAIN
```

`HISTORY SHOW` streams one JSON message per chunk. `VIEW TRANSCRIPT` renders each as `[role]` plus a body wrapped to 75 % of 96 usable columns, right-aligned for `user`, with a `◊` marker under any assistant message carrying probability data. `VIEW PLAIN` strips styling for a mail-able transcript. **What the user gets:** the full-screen shell's transcript, in a line-oriented shell, with no full-screen shell.

**5 — The flowing heat map that never shipped.**

```
LOGPROB SHOW -last | VIEW SAMPLE -preset flow | VIEW HEAT -all -palette bands6bg -legend
```

`-preset flow` applies the *other* sampling rule the source contained — slice 10, threshold 30, middle start `floor((count − 10) / 2)` — and `VIEW HEAT -all` then paints every record it receives as a continuous paragraph with each token's background coloured by confidence and its foreground flipped black/white at the 0.5 boundary. **What the user gets:** `LogProbHeatmapView` — code the source compiled but never constructed — reachable for the first time, with a legend and without its silent-truncation and long-token overflow bugs.

**6 — Setting a preference from a pipe, the framework's own idiom.**

```
SAY grid | VIEW LAYOUT
```

`SAY` (built-in) emits `grid`; `VIEW LAYOUT`'s ordered `mode` parameter is `UsePipe = true`, so it is fed from the chunk rather than demanded on the command line, applies the change, persists through `CONFIG`, and returns an empty success — silent, exactly as the framework's own `SET` behaves. **What the user gets:** presentation preferences that are scriptable from any producer.

---

### 7.4 Design notes for the architect

**D1 — What state this package holds: none that survives the process.** Every tool is a pure function of (input chunks, parameters, framework environment). There is no static mutable field, no cache, no singleton, no file. That is deliberate: `CommandFactory` creates a fresh instance per execution, but a host using the DI extension may register a tool as a singleton, and a pipeline runs **every stage concurrently** (`PipelineExecutor.cs:110-120`) — so any instance field that outlived one `Main` call would be a race. The only per-instance fields permitted are those scoped by `OnStartPipe`/`OnEndPipe` (a grid row buffer, a heat-map cursor, a sample buffer), and they are reset in `OnStartPipe`, never in the constructor.

**D2 — What it must not hold: the settings file.** Presentation preferences are *owned* by `ChatDbg.Tools.Configuration` and *published* into the framework environment as `CHATDBG_VIEW_*` globals. This package reads them and, on an explicit change, delegates the durable write. Three consequences, all deliberate:

* Only four tools (`LAYOUT`, `THEME`, `PALETTE`, `CAPS`) may be registered with `modifiesEnvironment: true`; a global write from any other registration lands in a private per-command bucket that nothing reads (`CommandController.cs:268-281`). Getting this wrong produces the exact class of bug as Q18: a command that reports success and changes nothing.
* Sub-command names are scoped inside their root by the registry, but the environment prefix the framework applies is the **command** name, not the root (`ControllerEnvironmentContext.cs:119-133`) — so a per-command key here would be `LAYOUT_…`, colliding with any other package's `LAYOUT`. Fully-qualified global keys avoid this entirely and are the reason every key in §7.1 begins `CHATDBG_VIEW_`.
* Durable persistence has exactly one owner, so a mutation can never write back a stale snapshot of unrelated settings — the failure that silently discarded a user's stored configuration in the source's full-screen host.

**D3 — The palette default is a deliberate, recorded deviation.** The source's default rendering path fed a 0…1 probability to a palette whose thresholds are 90/70/50/30, so **every real token rendered red** (Q1, Q28) — a user never saw the spectrum the README advertised. This package defaults to `bands10`, the only palette the shipped product actually displayed correctly, and makes the scale an explicit declaration (`-scale unit|percent`) that is *converted* rather than misread. `bands5` remains available and keeps its exact thresholds; it is simply no longer fed the wrong units. `VIEW PALETTE -check <p>` exists so the next such mismatch is a one-line diagnosis instead of a five-year-old quirk.

**D4 — Testability rests on one seam.** `VIEW CAPS` is the only tool that touches a device or the process environment; everything else reads the values it published. So a test sets `CHATDBG_VIEW_CAPS_COLUMNS=80`, `_COLOR=none`, `_UNICODE=false` (or runs `VIEW CAPS -assume redirected -quiet`) and every renderer becomes a deterministic string function, drivable through `MemoryIoContext` with no console at all. The source could not do this: its renderers queried the terminal inline, and there was **no test project for either shell** — the styled renderer, the theme, the heat-map view and the full-screen window had zero coverage. Golden-file tests are expected for: the 90-character table, the 80-character centred rule and the 79-character left-justified rule, the truncation points (17 + `...`, 7 + `...`), `(none)`, `(null)`, the `+ N more` suffix in both its forms, and each palette's band boundaries.

**D5 — When a capability is missing on the current backend.** Nothing here needs a backend. What it needs is *records*, and a backend that returns none is normal, not exceptional: `VIEW TOKENS` emits the source's two-line notice and succeeds. A backend that returns tokens without alternatives renders `(none)` per row and drops the alternatives column budget to zero. A backend whose top-K is lower than `-alts` simply shows fewer with no `+ N more`. None of these is an error, and none of them may produce a `Failure` chunk — a `Failure` in this package means *this package could not draw*, never *the model did not supply*.

**D6 — When a capability is missing on the current operating system.** There is no OS-conditional code here and there must not be; the source had none on its rendering path either, and that was one of its genuine strengths. What differs by platform is *terminal capability*, and it is detected, not assumed:

| Situation | Degradation, never failure |
|---|---|
| Legacy Windows console without VT processing | `ansi16`, no truecolor, ASCII box drawing; the host's failure to enable VT is detected, not compensated for by emitting raw escapes |
| Non-UTF-8 code page or locale | ASCII substitutions for `◊ ✓ ✗ #` and all box drawing; East-Asian width measurement disabled and reported |
| `TERM=dumb`, CI, redirection | plain ASCII, colour off, 80 columns |
| Terminal-width query throws (a real platform divergence the source did not handle) | 80 columns, trace message, render proceeds |
| Terminal narrower than 20 columns | clamped to 20; **no loop, no negative substring, no crash** |
| No console attached at all | fully conservative profile; every tool still produces output |

**D7 — Where to degrade rather than fail, stated as a rule.** A rendering tool may return `Failure` for exactly three reasons: a chunk it cannot parse, a parameter outside its declared range, and an upstream failure it is forwarding. **Everything else degrades**: missing colour becomes symbols, missing glyphs become ASCII, missing width becomes 80, an over-long caption is truncated, an over-long cell is ellipsised, a null token becomes `(null)`, an absent alternatives list becomes `(none)`, an unavailable settings owner becomes a session-only change with an honest message. The source's rendering layer failed hard in five places where it should have degraded (Q15 rule width, Q17 narrow terminal ×2, Q22 null token, Q23 null list) and degraded silently in two where it should have said something (Q27 heat-map truncation, Q20 stale panel geometry). Both directions are corrected here, and the correction is the same principle: **a renderer that cannot draw perfectly must still draw, and must say what it gave up.**

**D8 — Two sharp edges of the framework the host must absorb.** First, invoking the bare root — `VIEW` with no sub-command — throws `InvalidOperationException` from `CommandFactory`, and the async path the executor actually uses is less forgiving than the sync one on an unknown sub-command (`CommandFactory.cs:159-176`). The host must catch that and render usage; this package cannot register a default sub-command to absorb it. Second, parse errors raised inside `ProcessParameters` surface to the user only as `Error executing {command} (see trace for more info)`, with the specific message in the trace — which is why every tool here **re-validates its own ranges after parsing** and returns the source's exact wording as a `Failure`. The allow-lists are the first line of defence; the re-validation is what the user actually reads.

**D9 — Duplication is the failure mode to design against.** The source contained the same ~250 lines of visualisation logic in three drifted copies, the sampling constant `5` in four places, the column divisor `40` in four, the token-escaping rule written four times with three different behaviours, and the five-band palette written four times with two names for the same band (Q12). Each of those is now a single implementation behind a single tool: one sampler (`VIEW SAMPLE`), one wrapper (`VIEW WRAP`), one escaper (shared internal, exercised through `VIEW PLAIN`), one palette table (`VIEW PALETTE`). If a future surface needs token rendering, it composes `VIEW TOKENS` — it does not copy it.

---

## 8. ChatDbg.Tools.DiagnosticsObservability — Diagnostics & Observability

> Root command: **`DIAG`**. Every tool in this package is a sub-command invoked as `DIAG <VERB> …`.
> Framework: `Xcaciv.Command` **3.3.4** contracts (`ICommandDelegate`, `AbstractCommand`, `IIoContext`,
> `IEnvironmentContext`, `IResult<string>`), hosted by a Cupcake-pattern shell and loaded through
> `Xcaciv.Loader`. Where this chapter and memory disagree with `synthesis/ref-command.md`, the reference wins.

---

### 8.1 Purpose and boundary

**What this package owns.**

1. **The diagnostic recorder** — the process-wide, thread-safe, timestamped text buffer that captures (a) every
   line the native inference engine emits through its process-global log-sink hook and (b) every application-level
   entry any part of the product chooses to record. This is the rebuilt form of
   `Services/TokenInspection/LLamaSharpLogConfig.cs`.
2. **Buffering, thresholded auto-flush, daily rolling files, retention and pruning.** The source had a buffer, a
   10 000-character threshold and date-stamped file names; it had *no* retention, *no* size cap and *no* pruning
   (diagnostic-logging dossier, business rule 18). Retention is new here and is marked as such.
3. **Export** — writing a durable snapshot a user can attach to a bug report. This is the rebuilt (and repaired)
   form of the `export-logs` command, which in the source was a stub registered in neither shell and which wrote
   no file at all (QUIRK-1, QUIRK-2, QUIRK-3).
4. **The environment health check ("doctor")** — a single command that reports runtime and build-profile facts,
   backend availability, native-library resolution, credential *resolution provenance* (never values), and
   configuration validity against the product's real ranges.
5. **Audit-stream configuration** — turning the framework's `IAuditLogger` on, choosing its sink, and configuring
   redaction, plus honestly reporting what that redaction can and cannot mask.

**What this package explicitly does NOT own.**

| Not owned | Owner |
|---|---|
| Loading models, creating inference contexts, running generation, arming the *engine's* own behaviour | **`ChatDbg.Tools.Inference`** (root `LLM`) — PRD 7.8. `DIAG` installs the sink; `LLM` is what makes the engine talk into it. |
| Per-token probability capture, token analyses, heatmaps, `TokenAnalysis` JSON export | **`ChatDbg.Tools.TokenAnalysis`** (root `TOKEN`) — PRD 7.9 / 7.10. The source's folder placement (`Services/TokenInspection/LLamaSharpLogConfig.cs`) is misleading; there is no dependency (QUIRK-7). |
| Reading, writing, validating or persisting settings | **`ChatDbg.Tools.Configuration`** (root `CFG`) — PRD 7.2. `DIAG DOCTOR` *validates a snapshot* against the documented ranges; it never writes one. |
| Resolving, storing, migrating or displaying credentials | **`ChatDbg.Tools.Credentials`** (root `CRED`) — PRD 7.3. `DIAG DOCTOR` reports which **tier** would win and whether a value is present; it never reads, prints, fingerprints beyond a length class, or copies secret material. |
| Rendering — colour, markup, heatmaps, tables, themes | **`ChatDbg.Tools.Rendering`** (root `RENDER`) — PRD 7.12. `DIAG` emits plain text and declares an `OutputFormat`; how it is painted is the host's `IIoContext` and `RENDER`'s business. |
| Producing distribution artefacts, running the release pipeline, publishing | **`ChatDbg.Tools.Packaging`** (root `PKG`) — PRD 7.15. `DIAG BUILDINFO` *reads* the identity of the artefact it is running inside; it never builds one. |
| Chat history, system prompts, provider clients | `ChatDbg.Tools.History` (7.4), `ChatDbg.Tools.Prompts` (7.5), `ChatDbg.Tools.Providers` (7.6/7.7). |

**The governing philosophy, preserved verbatim from the source:** *diagnostics must never break the thing they are
diagnosing.* Every operation degrades rather than throws. The one place the rebuild deliberately deviates is that
the source made a completely dead logging subsystem **indistinguishable from a healthy one** (diagnostic-logging
acceptance criterion 20, QUIRK-11): every failure went to a debug-trace sink that the shipping build configurations
compile away. This package keeps the swallow but adds an **always-available, in-band health signal** —
`DIAG STATUS` and `DIAG DOCTOR` report fault counts and the last internal failure.

---

### 8.2 Package manifest

| Facet | Value |
|---|---|
| Assembly / package name | `ChatDbg.Tools.DiagnosticsObservability` |
| Root command | `DIAG` (`[CommandRoot("DIAG", "Diagnostics and observability")]`, normalized to uppercase by `NamesValidator`) |
| Contract assembly targeted | `Xcaciv.Command.Interface` **3.3.4** + `Xcaciv.Command.Core` **3.3.4** (`AbstractCommand`). No reference to `Xcaciv.Command` (the host assembly) — the dependency arrow points tool → SDK only (Cupcake rule 4). |
| Target framework | `net10.0` (framework default per ref-command §1.2). Matches the source product's TFM. |
| Elevated trust required | **No.** No administrative rights, no driver access, no service control, no registry writes. |
| Filesystem reach | **Write:** the configured log root (default `<roaming app data>/ChatDbg/Logs`) and any path the user names to `DIAG EXPORT`. **Read:** the log root, `AppContext.BaseDirectory` and its `runtimes/<rid>/native/` subtree (presence + size + mtime only), the settings/prompt roots (existence + writability probe only, never content). **Delete:** only inside the configured log root, only from `DIAG ROTATE`, only with `-apply`. |
| Network reach | **None by default.** `DIAG DOCTOR -probe endpoints` opts in to an outbound reachability check of the configured Azure endpoint / AWS region endpoint. Off unless asked; never sends credentials; never sends a chat payload. |
| OS keystore reach | **Presence probe only.** On Windows, "does an entry named `ChatDbg:AzureApiKey` exist?" via the platform credential API. It never reads the blob. On macOS/Linux the probe reports "no OS keystore integration on this platform". |
| Native library reach | **Metadata only by default** — file existence, size, timestamp, architecture header of `llama.dll` / `libllama.so` / `libllama.dylib` under `runtimes/<rid>/native/`. An **actual load probe** (`DIAG DOCTOR -probe native`) is opt-in and runs **out of process**, because the documented failure mode is a hard `0xC0000005` access violation that no managed handler can intercept and that would otherwise kill the shell. |
| Dynamic code generation | **None.** No `System.Reflection.Emit`, no `Assembly.Load` of user input, no expression compilation. The package is compatible with `AssemblySecurityPolicy { DisallowDynamicAssemblies = true }` (ref-loader §3.1, step 4). |
| Reflection use | Read-only metadata on assemblies already loaded (`AssemblyInformationalVersionAttribute`, `TargetFrameworkAttribute`, `RuntimeInformation`, `AppContext` switches). No name-based type resolution, so it survives `TrimMode=full`. |
| Safe to load in a restricted host | **Yes**, with named degradations: with no writable log root it runs memory-only; with no native engine it captures application entries only; with no keystore it reports tier availability; with `-probe` flags refused by host policy it reports `skipped (policy)` rather than failing. |
| Audit posture | Registered with the host's `IAuditLogger`. Three tools are registered `modifiesEnvironment: true` (see §8.4); all others write only their own prefixed bucket. |

---

### 8.3 Tool catalog

Eleven tools. Five are direct descendants of source behaviour, six are NEW and each states why it earns its place.

**Two framework facts that shape every tool below, stated once:**

* **Zero arguments ⇒ empty parameter dictionary.** `AbstractCommand.ProcessParameters` early-returns when
  `io.Parameters.Length == 0` (ref-command §3.4), so **no defaults are applied, no flags materialise as `false`,
  and no field injection happens**. Every tool here is specified to behave correctly when invoked bare, using the
  documented default in the parameter table as its in-code fallback.
* **Parameter parse errors are invisible to the user.** An `ArgumentException` from a missing required parameter
  surfaces only as `Error executing DIAG (see trace for more info)` (ref-command §5.8). Therefore **no tool in this
  package declares a required parameter whose absence has a good error message**: path-like ordered parameters are
  declared `IsRequired = false` and the tool returns a hand-written `CommandResult<string>.Failure(...)` carrying
  the same usage text the source printed.

---

#### 8.3.1 `DIAG CAPTURE` — arm, disarm and configure diagnostic capture

**Registration**

| Field | Value |
|---|---|
| Command | `CAPTURE` |
| Root command | `DIAG` |
| Description | `Arm engine diagnostic capture and configure the recorder's sinks and thresholds` |
| Prototype | `DIAG CAPTURE [on|off|status] [-dir <path>] [-file on|off] [-console on|off] [-trace on|off] [-buffer <chars>] [-level <min>] [-pattern <name>] [-timestamps local|localoffset|utc] [-redact on|off]` |

```csharp
[CommandRoot("DIAG", "Diagnostics and observability")]
[CommandRegister("Capture", "Arm engine diagnostic capture and configure the recorder's sinks and thresholds",
    Prototype = "DIAG CAPTURE [on|off|status] [-dir <path>] [-file on|off] …", Version = "1.0.0")]
[CommandParameterOrdered("state", "Turn capture on, off, or report its state",
    IsRequired = false, DefaultValue = "on", AllowedValues = new[] { "on", "off", "status" })]
[CommandParameterNamed("dir", "Directory that holds the rolling daily log files")]
[CommandParameterNamed("file", "Write the buffer to the rolling daily file",
    DefaultValue = "on", AllowedValues = new[] { "on", "off" })]
[CommandParameterNamed("console", "Echo every entry to standard output",
    DefaultValue = "off", AllowedValues = new[] { "on", "off" })]
[CommandParameterNamed("trace", "Echo every entry to the platform debug/trace stream",
    DefaultValue = "on", AllowedValues = new[] { "on", "off" })]
[CommandParameterNamed("buffer", "Auto-flush threshold in characters", DataType = typeof(int), DefaultValue = "10000")]
[CommandParameterNamed("level", "Minimum level retained", DefaultValue = "TRACE",
    AllowedValues = new[] { "TRACE", "DEBUG", "INFO", "WARN", "ERROR", "FATAL" })]
[CommandParameterNamed("pattern", "Daily file-name pattern", DefaultValue = "chatdbg_{0:yyyyMMdd}.log")]
[CommandParameterNamed("timestamps", "Entry timestamp rendering", DefaultValue = "local",
    AllowedValues = new[] { "local", "localoffset", "utc" })]
[CommandParameterNamed("redact", "Mask credential-shaped text on ingest", DefaultValue = "on",
    AllowedValues = new[] { "on", "off" })]
[CommandHelpRemarks("Arming is idempotent: the engine sink is installed at most once per process.")]
[CommandHelpRemarks("The engine's log-sink hook is process-global and last-writer-wins; only one recorder may own it.")]
public sealed class CaptureCommand : AbstractCommand { … }
```

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `state` | ordered | `string` | no (`IsRequired=false`; ordered params are required by default — opted out) | `on` | `on` \| `off` \| `status` | Arm, disarm, or report without changing anything. |
| `dir` | named | `string` | no | platform log root: `<roaming app data>/ChatDbg/Logs` — `%APPDATA%\ChatDbg\Logs` (Windows), `$XDG_CONFIG_HOME/ChatDbg/Logs` else `~/.config/ChatDbg/Logs` (Linux/macOS) | any writable directory; must resolve to an absolute path | Directory for the rolling daily files. Created recursively on first use. |
| `file` | named | `string`→bool | no | `on` | `on` \| `off` | Source default: file logging **on**. |
| `console` | named | `string`→bool | no | `off` | `on` \| `off` | Source default: console echo **off**. Interleaves log lines into the chat transcript when on. |
| `trace` | named | `string`→bool | no | `on` | `on` \| `off` | Source default: debug-stream echo **on**. **NEW behaviour:** this is a *runtime* switch here; in the source it was compiled out of every non-`DEBUG` build (QUIRK-11). |
| `buffer` | named | `int` | no | `10000` | `0` … `10000000` characters; comparison is strictly greater-than, so the flush fires on the append that pushes length to `threshold + 1`; `0` means "flush every engine line" | Auto-flush threshold, in **characters** of accumulated buffer text — not bytes, not entries. |
| `level` | named | `string` | no | `TRACE` | `TRACE` \| `DEBUG` \| `INFO` \| `WARN` \| `ERROR` \| `FATAL` | **NEW.** Minimum retained level. The source had no level filtering at all and no way to suppress the per-token `DEBUG` firehose (business rule 10). |
| `pattern` | named | `string` | no | `chatdbg_{0:yyyyMMdd}.log` | a composite-format string whose `{0}` is the local date | **Deliberate deviation:** the source's pattern was `llamasharp_yyyyMMdd.log`. The rebuilt recorder captures every backend, not just the local one. `DIAG TAIL` / `FILES` / `ROTATE` still recognise the legacy `llamasharp_*.log` name for backward compatibility. |
| `timestamps` | named | `string` | no | `local` | `local` \| `localoffset` \| `utc` | `local` preserves the source's `yyyy-MM-dd HH:mm:ss.fff`. **Deliberate deviation:** always rendered with **invariant culture**, fixing the source's build-dependent double shape (QUIRK-13). |
| `redact` | named | `string`→bool | no | `on` | `on` \| `off` | **NEW.** Masks credential-shaped text on ingest (see Security below). |

**Pipeline behaviour** — **Neither.** It is a configuration verb; the non-piped `AbstractCommand` path emits exactly
one chunk, a one-line confirmation such as
`Capture armed. dir=/home/u/.config/ChatDbg/Logs file=on trace=on console=off buffer=10000 level=TRACE`.
Invoked in a pipeline it follows Cupcake rule 29 and returns an explanatory `Failure`
(`DIAG CAPTURE does not accept piped input. Pipe into DIAG RECORD or DIAG EXPORT instead.`) rather than throwing.
`OutputFormat = ResultFormat.General`.

**Environment interaction** — Registered `modifiesEnvironment: true`, so it writes the **global** shell variables the
rest of the package reads:

| Written (global) | Meaning |
|---|---|
| `CHATDBG_DIAG_DIR` | resolved absolute log root |
| `CHATDBG_DIAG_FILE`, `CHATDBG_DIAG_CONSOLE`, `CHATDBG_DIAG_TRACE` | sink switches |
| `CHATDBG_DIAG_BUFFER` | threshold in characters |
| `CHATDBG_DIAG_LEVEL`, `CHATDBG_DIAG_PATTERN`, `CHATDBG_DIAG_TIMESTAMPS`, `CHATDBG_DIAG_REDACT` | filter and format policy |
| `CHATDBG_DIAG_ARMED` | `true` once the engine sink is installed |

`GetDefaultEnvironment()` returns those keys with the defaults above; the host seeds them under the
`CAPTURE_` prefix (ref-command §2.4), so the tool reads its own seeded copy as `CAPTURE_DIR` etc. and publishes
the shared, unprefixed `CHATDBG_DIAG_*` names for its siblings. All sibling reads use
`GetValue(key, default, storeDefault: false)` — `storeDefault` defaults to `true` and a bare read would otherwise
flip `HasChanged` and trigger a write-back (ref-command §7.1).
It reads **no OS environment variables** and needs no environment-modifying OS permission.

**Failure modes**

| Situation | Behaviour |
|---|---|
| `state` is not in the allow-list | Rejected at parse time by `AllowedValues`; the user sees the generic executor message, so the help text and `Prototype` carry the three legal values. |
| `-buffer` is non-numeric or negative | Parameter is materialised **invalid** (`IsValid == false`); the tool falls back to `10000` and emits `Warning: -buffer '<raw>' is not a non-negative integer; using 10000.` **Deviation:** the source accepted any integer including negatives with no validation (business rule, max-buffer-size valid range: none enforced). |
| `-dir` cannot be created (permissions, read-only volume, invalid path) | **Never throws.** Capture arms in **memory-only** mode, the fault counter increments, and the confirmation reads `Capture armed (memory-only): log directory '<path>' is not writable — file sink disabled.` **This is the deliberate repair of the source's worst failure** (business rule 19 + acceptance criterion 20): the source left the recorder un-armed and reported it on a channel that does not exist in a shipping build. |
| Native engine sink cannot be installed (native library absent, wrong architecture, blocked) | Capture arms anyway, in **application-entries-only** mode. Reports `engine sink: unavailable — <reason>`. The recorder is **still armed** (the source's armed latch was set after sink installation, so a sink failure silently disabled everything — this rebuild sets the latch first). |
| `-dir` changed after arming | **Repaired.** Directory creation is re-evaluated on every flush, not only at arm time. The source gated creation on the file-logging switch at arm time only, making "turn file logging on later" permanently broken (QUIRK-19, business rule 23). |
| A second `DIAG CAPTURE on` | Idempotent no-op for the sink; switch changes still apply. Preserves the source's arm-idempotency (business rule 6). |
| Another recorder already owns the process-global engine hook | Reports `engine sink: owned by another recorder (last-writer-wins)` and refuses to steal it unless `-force`. The source silently allowed last-writer-wins with earlier recorders still believing themselves armed (business rule 21). |
| Downstream pipe error | Not applicable — this tool is never a pipeline stage. |

**Security and audit** — No parameter carries a secret. `-dir` is a path and may contain a user name; it is recorded
in the audit event as-is (paths are not treated as secrets). The tool is **not destructive**: turning capture off
does not discard the buffer. **Redaction is configured here and applied on ingest**: with `-redact on` (default) the
recorder rewrites, before anything reaches the buffer, any occurrence of the five credential variable names' values
and any text matching `set CHATDBG_(AZURE_API_KEY|AWS_ACCESS_KEY|AWS_SECRET_KEY)=…`, `AWS_ACCESS_KEY_ID=…`,
`AWS_SECRET_ACCESS_KEY=…`, or a bearer-token/`sk-`/`AKIA`-shaped literal, to `[REDACTED]`. This exists because the
source's credential-migration wizard prints plaintext secrets to standard output on **every settings load** where
plaintext credentials exist (credential-management QUIRK-3) — with console capture on, those would land verbatim in
a file users are told to attach to bug reports.

**Traceability** — PRD **7.11 Diagnostic Logging**. Descends from `LLamaSharpLogConfig.ConfigureLogging()` and the
five never-reachable switches (`LogDirectory`, `EnableFileLogging`, `EnableDebugOutput`, `EnableConsoleOutput`,
`MaxBufferSize`, business rule 24 / QUIRK-14). The runtime configuration surface itself is **NEW** — the source
required a recompile to change any of them, while its own documentation told users to "set it in config".

---

#### 8.3.2 `DIAG STATUS` — report the recorder's own health

**Registration**

| Field | Value |
|---|---|
| Command | `STATUS` |
| Root command | `DIAG` |
| Description | `Report capture state, sink health, buffer occupancy and internal faults` |
| Prototype | `DIAG STATUS [-format text|json|csv] [-v]` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text` \| `json` \| `csv` | Output shape. Sets `OutputFormat` to `General` / `JSON` / `CSV` accordingly. |
| `verbose` | flag (`ShortAlias = "v"`) | `bool` | no | `false` (flags are always materialised as bool) | — | Include per-sink counters, the last five internal faults, and the resolved policy values. |

**Pipeline behaviour** — **Produces piped output; accepts none.** Overrides `Main` so it can emit **one chunk per
reported row** (`AbstractCommand`'s non-piped path emits exactly one chunk, ref-command §3.3), which is what makes
`DIAG STATUS | REGIF "unavailable"` work. With `-format json` it emits a **single** chunk carrying one JSON object
and declares `ResultFormat.JSON`; with `-format csv` it emits a header chunk then one chunk per row and declares
`ResultFormat.CSV`. Format is metadata only — the framework never branches on it (ref-command §10.5) — but the
host's `IIoContext` and `RENDER` do.

Reported fields: `armed`, `engineSink` (`installed` / `unavailable(<reason>)` / `owned-elsewhere`), `mode`
(`file+memory` / `memory-only`), `logDir`, `dirWritable`, `todayFile`, `todayFileBytes`, `bufferChars`,
`bufferThreshold`, `entriesBuffered`, `entriesFlushed`, `entriesDropped`, `lastFlushUtc`, `minLevel`,
`redaction`, `timestampMode`, `faultCount`, `lastFault`, `recorderOwner` (`di-singleton` / `package-static`).

**Environment interaction** — Reads `CHATDBG_DIAG_*` globals with `storeDefault: false`. Writes nothing. No
environment-modifying permission needed.

**Failure modes** — Cannot fail: with no recorder yet constructed it reports `armed=false, mode=none` and exits
successfully. An unreadable log directory yields `dirWritable=false` plus the reason string, not an error. Invoked
with a piped input it returns the explanatory `Failure` (Cupcake rule 29).

**Security and audit** — No secrets. Paths appear in output. Non-destructive; no confirmation.

**Traceability** — PRD **7.11**. **NEW.** The source had no status surface of any kind; its acceptance criterion 20
records that a totally dead logging subsystem was indistinguishable from a healthy one on every channel. This tool
exists to make that impossible, and is the "out-of-band health signal" the dossier explicitly recommends.

---

#### 8.3.3 `DIAG TAIL` — read entries out of the buffer and the rolling files

**Registration**

| Field | Value |
|---|---|
| Command | `TAIL` |
| Root command | `DIAG` |
| Description | `Emit the most recent diagnostic entries, one chunk per entry` |
| Prototype | `DIAG TAIL [<file>] [-n <count>] [-level <min>] [-grep <regex>] [-since <duration>] [-source buffer|file|both] [-format text|json|csv]` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `file` | ordered, `UsePipe = true` | `string` | no | *(unset — use `-source`)* | an existing file inside the log root, or an absolute path | The log file to read. When piped, this value arrives from the upstream chunk and is dropped from command-line parsing (ref-command §3.5). |
| `n` | named | `int` | no | `50` | `1` … `100000` | How many entries to emit, counted from the end. |
| `level` | named | `string` | no | `TRACE` | `TRACE` \| `DEBUG` \| `INFO` \| `WARN` \| `ERROR` \| `FATAL` | Minimum level. Entries whose level token is not in the vocabulary (engine-sourced tokens such as `Info`, `Warn`) are normalised case-insensitively; unrecognised tokens are treated as `INFO` and never dropped. |
| `grep` | named | `string` | no | *(none)* | a .NET regular expression; bounded to 1 000 characters | Keep only entries whose message matches. |
| `since` | named | `string` | no | *(none)* | `<n>s` \| `<n>m` \| `<n>h` \| `<n>d`, or an ISO-8601 instant | Keep only entries at or after this point. Compared in the recorder's configured timestamp mode. |
| `source` | named | `string` | no | `both` | `buffer` \| `file` \| `both` | `both` merges the un-flushed buffer with today's file in timestamp order — **this is the repair of QUIRK-4**, where the source's forced flushes meant the buffer a user read was almost always empty. |
| `format` | named | `string` | no | `text` | `text` \| `json` \| `csv` | `json` emits one JSON object per entry (`ts`, `level`, `origin`, `message`) and declares `ResultFormat.JSON`. |

**Pipeline behaviour** — **Both.**
*As a source* it overrides `Main` and emits **one chunk per entry**, so downstream filters see lines, not a blob.
*As a filter* each piped chunk is interpreted as **one log-file path**; the tool tails that file with the same
`-n` / `-level` / `-grep` / `-since` policy and emits that file's surviving entries. A chunk naming a file outside
the configured log root is refused with a per-chunk `Failure` and the pipeline continues. A chunk that is empty or
already a failure never reaches `HandlePipedChunk` at all (ref-command §3.3, §2.7).
Declares `ResultFormat.General` / `.JSON` / `.CSV` per `-format`.

**Environment interaction** — Reads `CHATDBG_DIAG_DIR`, `CHATDBG_DIAG_PATTERN`, `CHATDBG_DIAG_TIMESTAMPS`
(`storeDefault: false`). Persists nothing. No OS environment access.

**Failure modes**

| Situation | Behaviour |
|---|---|
| `-n` non-numeric or out of range | Clamped to `[1, 100000]` with a one-line `Warning:` chunk, then proceeds. |
| `-grep` is not a valid regex | Single `Failure`: `DIAG TAIL: '-grep <pattern>' is not a valid regular expression: <reason>`. Nothing is emitted. |
| Named file does not exist | `Failure`: `DIAG TAIL: no such log file '<path>'. Try 'DIAG FILES'.` |
| Named file is outside the log root and not absolute-and-explicit | `Failure`: refused, with the resolved root named. Path containment is checked component-wise via `Path.GetRelativePath`, not `StartsWith`, and link targets are resolved (ref-loader §10 step 2). |
| Log root missing / capture never armed | Emits from the buffer only; if that is empty, emits nothing and succeeds. (An empty success with empty `Output` is dropped by the host, ref-command §2.3.) |
| Zero arguments | Uses every default above — `-n 50`, `-source both`. Safe by construction (§8.3 preamble). |
| Downstream stage fails | The failure chunk propagates; `TAIL` neither retries nor swallows. Backpressure is the pipeline's bounded channel. |

**Security and audit** — No parameter carries a secret, but **output may**: log content is arbitrary text produced
by a native engine and by application code. Ingest-time redaction (§8.3.1) is the primary control; `DIAG TAIL`
applies it a second time on read for files written by an older build with redaction off. Audit records the
parameters, not the output. Non-destructive.

**Traceability** — PRD **7.11**. Descends from `LLamaSharpLogConfig.GetLogs()` (operation A4), which was public API
with **no user-facing surface anywhere in the product**. The filtering, merging and per-entry chunking are **NEW**.

---

#### 8.3.4 `DIAG RECORD` — write an application entry, and journal a pipeline

**Registration**

| Field | Value |
|---|---|
| Command | `RECORD` |
| Root command | `DIAG` |
| Description | `Record an application-level diagnostic entry; as a pipeline stage, journal every chunk that passes through` |
| Prototype | `DIAG RECORD [-level TRACE|DEBUG|INFO|WARN|ERROR|FATAL] [-tag <name>] [-quiet] <message…>` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `level` | named | `string` | no | `INFO` | `TRACE` \| `DEBUG` \| `INFO` \| `WARN` \| `ERROR` \| `FATAL` | Severity label. **Deliberate deviation:** the source accepted *any* free string as a level and its own two halves disagreed (`WARNING` in tests vs `WARN` in production, business rule 9). |
| `tag` | named | `string` | no | `app` | 1–32 characters of `[A-Za-z0-9_-]` | **NEW.** Origin marker written into the entry as `[<tag>]`, so a merged file can be attributed. Engine-sourced entries carry `[engine]`. |
| `quiet` | flag | `bool` | no | `false` | — | In a pipeline, swallow each chunk after recording it instead of passing it through. |
| `message` | suffix, `UsePipe = true` | `string` | no | *(none)* | all remaining tokens joined by single spaces into **one string** (ref-command §4.7) | The entry text. When piped, the message is the upstream chunk's `Output`. |

**Pipeline behaviour** — **Both, as a tee.** Non-piped it records one entry and emits a one-line confirmation.
Piped, `HandlePipedChunk` records `pipedChunk.Output` as one entry at `-level` with `-tag`, then re-emits the chunk
**verbatim** so the tool is transparent in the middle of a pipeline; with `-quiet` it returns
`CommandResult<string>.Success(string.Empty, OutputFormat)` — the host drops empty successes (ref-command §2.3), so
the stage becomes a pure sink, exactly the idiom `SET` uses to stay silent. `OnStartPipe` / `OnEndPipe` bracket the
run and write a `[diag] journal opened` / `… closed, N chunks` pair, giving every pipeline a delimited region in
the log. The re-emitted chunk keeps the upstream `CorrelationId` so a log entry and a pipeline stage can be joined.

**Environment interaction** — Reads `CHATDBG_DIAG_LEVEL`, `CHATDBG_DIAG_REDACT`. Persists its own chunk counter as
`RECORD_CHUNKS` in its private bucket (allowed without `modifiesEnvironment` because the key carries the command's
prefix, ref-command §7.3). No OS environment access.

**Failure modes**

| Situation | Behaviour |
|---|---|
| No message and no pipe | `Failure`: `DIAG RECORD: nothing to record. Usage: DIAG RECORD [-level INFO] <message…>` |
| `-level` outside the allow-list | Rejected at parse time; help names the six values. |
| Recorder in memory-only mode | Records normally, into memory. No error. |
| Recorder not yet armed | **Records anyway** — the buffer exists from construction; arming concerns only the engine sink. Preserves the source's behaviour, where application entries accumulated even when arming had failed. |
| A chunk cannot be recorded (buffer at hard cap) | The oldest entries are dropped, `entriesDropped` increments, and a single `[diag] N entries dropped (buffer cap)` marker is inserted. The chunk still passes through. **NEW** — the source grew without bound whenever file logging was off (business rule 13). |
| Downstream failure | Propagates unchanged; the journal entry has already been written. |

**Security and audit** — **The `message` suffix parameter can carry anything the user types**, including a pasted
secret, and the framework's `AuditMaskingConfiguration` only rewrites `-name=value` tokens, so a space-separated
message is **not masked in the audit record** (ref-command §11.4). Therefore: the audit metadata for `DIAG RECORD`
records the *message length and hash*, never the message text, and the help remarks say plainly "do not paste
secrets into `DIAG RECORD`". Ingest redaction still applies to the buffer copy. Non-destructive.

**Traceability** — PRD **7.11**. Descends from `LLamaSharpLogConfig.LogMessage(level, message)` (operation A3), which
in the source was callable only by the inference service. The user-facing verb and the pipeline-journal role are
**NEW**, and earn their place because they make every other package's output traceable through one log: without a
sink, a pipeline is unobservable.

---

#### 8.3.5 `DIAG FLUSH` — drain the buffer to the rolling daily file

**Registration**

| Field | Value |
|---|---|
| Command | `FLUSH` |
| Root command | `DIAG` |
| Description | `Append the buffered entries to today's rolling log file and empty the buffer` |
| Prototype | `DIAG FLUSH [-quiet]` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `quiet` | flag | `bool` | no | `false` | — | Suppress the confirmation line; still reports failures. |

**Pipeline behaviour** — **Neither.** One confirmation chunk:
`Flushed 412 entries (31 204 chars) to /home/u/.config/ChatDbg/Logs/chatdbg_20260828.log`. Piped, it returns the
explanatory `Failure`.

**Environment interaction** — Reads `CHATDBG_DIAG_DIR`, `CHATDBG_DIAG_FILE`, `CHATDBG_DIAG_PATTERN`. Writes nothing.

**Failure modes**

| Situation | Behaviour |
|---|---|
| File logging is **off** | No file is created or touched **and the buffer is not drained** — the source's exact behaviour (acceptance criterion 8). Reports `File sink is off; nothing flushed. 412 entries remain buffered.` **The rebuild adds the report**; the source was a silent no-op. |
| Buffer is empty | No file is created. Reports `Buffer empty; no file written.` (Source: acceptance criterion 14, a daily file exists only on days with at least one flushed entry.) |
| Directory missing, disk full, file locked, permission denied | **Never throws. The buffer is preserved**, exactly as in the source (the append throws before the clear, so nothing is lost, business rule / acceptance criterion 9). Returns a `Failure` naming the reason, and increments the fault counter so `DIAG STATUS` shows it. |
| Called concurrently with an engine-driven auto-flush | Serialised by the recorder's re-entrant buffer lock; append order is preserved. The auto-flush path re-enters the same lock from inside the locked append, so re-entrancy on the same thread is load-bearing (diagnostic-logging business rule 8). |

**Security and audit** — No secrets in parameters. Writes a file whose content may contain redacted log text.
**Semi-destructive** (it empties the buffer) but not *irreversible* — the content moves to durable storage, which is
the point — so no confirmation is required.

**Traceability** — PRD **7.11**. Direct descendant of `LLamaSharpLogConfig.FlushToFile()` (operation A6), including
its append-not-truncate semantics, its empty-buffer guard, and its preserve-on-failure guarantee. The user-facing
verb and the reporting are **NEW** (the source flushed only from five hard-coded points inside the inference
service).

---

#### 8.3.6 `DIAG EXPORT` — write a support snapshot

**Registration**

| Field | Value |
|---|---|
| Command | `EXPORT` |
| Root command | `DIAG` |
| Description | `Write diagnostic entries to a file suitable for attaching to a bug report` |
| Prototype | `DIAG EXPORT [<filepath>] [-source buffer|file|both|all] [-days <n>] [-append] [-force] [-redact on|off] [-format text|json]` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `filepath` | ordered | `string` | no (`IsRequired = false`, so the tool can print the source's usage text itself) | *(none)* | any writable path; parent directory created recursively; a bare file name resolves against the process working directory | Destination file. |
| `source` | named | `string` | no | `both` | `buffer` \| `file` \| `both` \| `all` | `both` = un-flushed buffer + today's file, merged in timestamp order. `all` = every retained daily file plus the buffer. **This is the repair of QUIRK-4**: in the source, export snapshotted only the buffer, which the forced flushes had almost always just emptied, so the ordinary post-generation export produced a 0-byte file. |
| `days` | named | `int` | no | `1` | `1` … `3650` | With `-source all`, how many days back to include. |
| `append` | flag | `bool` | no | `false` | — | Append instead of overwrite. **Deviation:** the source always truncated (acceptance criterion 17). |
| `force` | flag | `bool` | no | `false` | — | Required to overwrite an existing **non-empty** file. **Deviation:** the source overwrote unconditionally with no prompt and no extension check (business rule 11). |
| `redact` | named | `string`→bool | no | *(inherits `CHATDBG_DIAG_REDACT`, default `on`)* | `on` \| `off` | Re-apply masking on the way out. |
| `format` | named | `string` | no | `text` | `text` \| `json` | `text` reproduces the source's `[ts] [LEVEL] message` line shape; `json` writes one JSON object per line. |

**Pipeline behaviour** — **Accepts piped input; produces a one-line summary.** Piped, **one chunk means one line to
write**: `HandlePipedChunk` appends `pipedChunk.Output` to the destination and returns an empty success (swallowed
by the host), and `OnEndPipe` closes the file and the summary is emitted from the non-piped path's counterpart. This
makes `… | DIAG EXPORT ./bug.log` the universal "capture this pipeline to a file" sink. `-source` is ignored while
piped (the pipe *is* the source) and a `Warning:` chunk says so once.

**Environment interaction** — Reads `CHATDBG_DIAG_DIR`, `CHATDBG_DIAG_PATTERN`, `CHATDBG_DIAG_REDACT`. Writes
`EXPORT_LAST_PATH` into its own prefixed bucket so `DIAG STATUS -v` can report the last export. No OS environment
access.

**Failure modes**

| Situation | Behaviour |
|---|---|
| No path given | `Failure` whose message reproduces the source's three lines, adapted: `Usage: DIAG EXPORT <filepath>` / `Example: DIAG EXPORT chatdbg-debug.log` / `Tip: 'DIAG EXPORT -source all -days 7 bug.log' captures the whole week.` **Deviation from QUIRK-3:** the source returned *success* for a path it ignored and *failure* only for no path at all — an inversion its own tests locked in. Here, no path is a failure and a supplied path really writes. |
| Destination exists, non-empty, no `-append`/`-force` | `Failure`: `DIAG EXPORT: '<path>' already exists (14 KB). Use -append or -force.` |
| Nothing to write | Writes **no file** and returns `Success` with `Nothing to export (buffer empty, no matching files).` **Deviation:** the source wrote a 0-byte file unconditionally and told the caller nothing (QUIRK-18, acceptance criterion 18). |
| Parent directory cannot be created, disk full, permission denied | `Failure` naming the reason; fault counter increments. **Deviation:** the source swallowed every export failure and the caller could not distinguish success from failure (operation A7). |
| Path is a directory | `Failure`: `'<path>' is a directory.` (The source performed no such check.) |
| Downstream error arriving through the pipe | Failure chunks are forwarded verbatim by `AbstractCommand.Main` before `HandlePipedChunk` is reached; the destination file is still closed cleanly in `OnEndPipe`, and the summary records `wrote N lines, forwarded M upstream failures`. |

**Security and audit** — Output may contain log text; masking is applied unless explicitly disabled, and
`-redact off` is recorded in the audit event as a metadata flag so a reviewer can see that an unmasked export was
taken. Writing a file is a side effect but not irreversible destruction; overwriting an existing non-empty file
**is** treated as destructive and gated behind `-force`. Encoding is **UTF-8 with no byte-order mark**, matching
the source, because the whole support workflow is "grep this file and attach it".

**Traceability** — PRD **7.11**. Descends from the `export-logs` command (`Commands/ExportLogsCommand.cs`) — a stub
that validated argument count, ignored the path, wrote nothing, and was registered in neither shell — and from
`LLamaSharpLogConfig.SaveLogsToFile(path)` (operation A7), the only export path that actually did anything. This
tool is the resolution of the dossier's own open question: *"implement the documented behavior: resolve the active
recorder, and write the union of the buffered and already-flushed content to the requested path."*

---

#### 8.3.7 `DIAG CLEAR` — discard buffered entries

**Registration**

| Field | Value |
|---|---|
| Command | `CLEAR` |
| Root command | `DIAG` |
| Description | `Discard the in-memory diagnostic buffer without writing it anywhere` |
| Prototype | `DIAG CLEAR -force [-flush]` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `force` | flag | `bool` | no | `false` | — | **Required.** Without it the tool reports what *would* be discarded and does nothing. |
| `flush` | flag | `bool` | no | `false` | — | Flush to the daily file first, then clear — the non-destructive alternative, offered inline because it is almost always what the user meant. |

**Pipeline behaviour** — **Neither.** One confirmation chunk.

**Environment interaction** — Reads `CHATDBG_DIAG_FILE` (to decide whether `-flush` is even possible). Writes nothing.

**Failure modes** — Without `-force`: `Success` with
`Would discard 412 entries (31 204 chars). Re-run with -force, or use -force -flush to keep them.`
With `-flush` and a failing flush: nothing is cleared, a `Failure` reports the flush reason — content is never
discarded because a write failed.

**Security and audit** — **Destructive and irreversible**: discarded content is written nowhere, exactly as in the
source (operation A5). Hence the mandatory `-force`. Audited with the discarded entry count in metadata.

**Traceability** — PRD **7.11**. Descends from `LLamaSharpLogConfig.ClearLogs()` (operation A5). The confirmation
gate and the `-flush` alternative are **NEW**; the source's method discarded silently on the first call.

---

#### 8.3.8 `DIAG FILES` — enumerate the diagnostic artefacts

**Registration**

| Field | Value |
|---|---|
| Command | `FILES` |
| Root command | `DIAG` |
| Description | `List the rolling log files and the product's per-user state locations` |
| Prototype | `DIAG FILES [-days <n>] [-roots] [-format text|json|csv]` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `days` | named | `int` | no | `30` | `1` … `3650` | Include files whose day-stamp falls within this many days of today. |
| `roots` | flag | `bool` | no | `false` | — | Also emit the product's per-user state roots rather than only log files. |
| `format` | named | `string` | no | `text` | `text` \| `json` \| `csv` | Output shape; `csv` declares `ResultFormat.CSV`. |

**Pipeline behaviour** — **Produces piped output; accepts none.** Overrides `Main` to emit **one chunk per file**,
each chunk being the file's **absolute path** in `text` mode — chosen deliberately so the chunk is directly
consumable by `DIAG TAIL`, `DIAG ROTATE` and `DIAG EXPORT`. In `csv`/`json` mode each chunk additionally carries
`day`, `bytes`, `modifiedUtc`, `pattern` (`chatdbg` or legacy `llamasharp`).

With `-roots`, three extra chunks name the product's per-user roots and their writability. The source scattered
state across **three different roots under two naming conventions** — settings at `<user profile>/.ChatDbg`,
prompts at `<local app data>/ChatDbg/system_prompts`, logs at `<roaming app data>/ChatDbg/Logs`
(packaging QUIRK-17, diagnostic-logging QUIRK-8) — and documented none of them. This flag is the only place in the
rebuilt product that tells a user where their state actually lives.

**Environment interaction** — Reads `CHATDBG_DIAG_DIR`, `CHATDBG_DIAG_PATTERN`. Writes nothing.

**Failure modes** — Missing log root: emits nothing and succeeds (with `-roots`, still reports the root and
`exists=false`). Unreadable directory: one `Failure` naming the reason. Zero arguments: `-days 30`, no roots.

**Security and audit** — Paths only; no content is read. Non-destructive.

**Traceability** — PRD **7.11**, with a reporting borrow from **7.15 Packaging & Release** (the per-user roots a
packaged artefact writes into). **NEW** — the source had no enumeration surface and no documentation of the
locations on any platform other than Windows.

---

#### 8.3.9 `DIAG ROTATE` — retention, pruning and compaction

**Registration**

| Field | Value |
|---|---|
| Command | `ROTATE` |
| Root command | `DIAG` |
| Description | `Apply retention policy to the rolling log files: prune by age, cap total size, optionally compress` |
| Prototype | `DIAG ROTATE [<file>] [-days <n>] [-maxtotal <MiB>] [-maxfile <MiB>] [-compress none|gzip] [-apply] [-format text|csv]` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `file` | ordered, `UsePipe = true` | `string` | no | *(none — scan the log root)* | a path inside the configured log root | Act on one file. When piped, supplied by the upstream chunk. |
| `days` | named | `int` | no | `14` | `1` … `3650` | Delete daily files older than this many days. Today's file is never deleted. |
| `maxtotal` | named | `int` | no | `256` | `1` … `65536` MiB | Cap on the total size of the log root; oldest files are removed first until the cap is met. |
| `maxfile` | named | `int` | no | `64` | `1` … `4096` MiB | A single day's file above this size is rolled to `<name>.1`, `<name>.2`, … and a fresh file is started. |
| `compress` | named | `string` | no | `none` | `none` \| `gzip` | Compress files older than one day in place, to `<name>.gz`. `DIAG TAIL` and `DIAG EXPORT` transparently read `.gz`. |
| `apply` | flag | `bool` | no | `false` | — | **Required to modify anything.** Without it the tool is a dry run and every emitted chunk is prefixed `WOULD `. |
| `format` | named | `string` | no | `text` | `text` \| `csv` | Output shape. |

**Pipeline behaviour** — **Both.** As a source it scans the log root and emits one chunk per candidate action
(`WOULD DELETE …` / `DELETED …` / `COMPRESSED …` / `ROLLED …`). As a filter each piped chunk is one candidate file
path — so `DIAG FILES -days 365 | DIAG ROTATE -days 14 -apply` is the canonical retention pipeline. Chunks naming
paths outside the configured log root are refused per-chunk with a `Failure` and the pipeline continues.

**Environment interaction** — Registered `modifiesEnvironment: true` so `-days` / `-maxtotal` / `-maxfile` /
`-compress` persist as the global retention policy (`CHATDBG_DIAG_RETAIN_DAYS`, `CHATDBG_DIAG_MAX_TOTAL_MB`,
`CHATDBG_DIAG_MAX_FILE_MB`, `CHATDBG_DIAG_COMPRESS`), which the recorder consults after each flush so retention is
also enforced continuously rather than only when the verb is typed. `GetDefaultEnvironment()` seeds all four.

**Failure modes**

| Situation | Behaviour |
|---|---|
| No `-apply` | Dry run. Always the default, because this is the only tool in the package that deletes user data. |
| Path outside the log root | Refused with the resolved root named. Containment is checked component-wise after link resolution, never by `StartsWith` (ref-loader §10, step 2). |
| A file is locked or undeletable | Per-file `Failure`; the run continues to the next file and the summary reports `n deleted, m failed`. |
| Compression unavailable on the host | Reports `compress: unavailable` and falls back to `none` rather than failing the run. |
| Zero arguments | Dry-run scan with the defaults. Nothing is ever deleted by a bare invocation. |
| Downstream error | Propagated; already-applied deletions are not undone (and cannot be) — which is exactly why `-apply` is opt-in. |

**Security and audit** — **Destructive and irreversible.** Gated by `-apply`, confined to the configured log root,
and every applied action is written to the audit stream with the file path, size and age in metadata, and to the
recorder itself as a `WARN` entry so the log records its own pruning. No parameter carries a secret.

**Traceability** — PRD **7.11**. **NEW.** The source had **no retention policy of any kind**: "rotation" meant only
a date-stamped file name, with no size cap, no compression, no retention limit and no deletion of old files, and a
long-lived install grew unboundedly on disk (business rule 18, and an explicit "could not determine whether the
daily file was ever intended to be pruned"). This tool earns its place because a diagnostic subsystem that cannot
be bounded is one operators turn off.

---

#### 8.3.10 `DIAG DOCTOR` — environment health check

**Registration**

| Field | Value |
|---|---|
| Command | `DOCTOR` |
| Root command | `DIAG` |
| Description | `Check runtime, backend availability, native libraries, credential resolution and configuration validity` |
| Prototype | `DIAG DOCTOR [<check>] [-only <ids>] [-probe none|native|endpoints|all] [-severity info|warn|error] [-format text|json|csv] [-v]` |

```csharp
[CommandRoot("DIAG", "Diagnostics and observability")]
[CommandRegister("Doctor", "Check runtime, backend availability, native libraries, credential resolution and configuration validity",
    Prototype = "DIAG DOCTOR [<check>] [-only <ids>] [-probe none|native|endpoints|all] …", Version = "1.0.0")]
[CommandParameterOrdered("check", "Run a single check group", IsRequired = false, DefaultValue = "all",
    AllowedValues = new[] { "all", "runtime", "build", "backend", "native", "credentials", "config", "storage" },
    UsePipe = true)]
[CommandParameterNamed("probe", "Perform active probes as well as passive inspection",
    DefaultValue = "none", AllowedValues = new[] { "none", "native", "endpoints", "all" })]
[CommandParameterNamed("severity", "Minimum severity to report",
    DefaultValue = "info", AllowedValues = new[] { "info", "warn", "error" })]
[CommandParameterNamed("format", "Output shape", DefaultValue = "text",
    AllowedValues = new[] { "text", "json", "csv" })]
[CommandParameterNamed("only", "Comma-separated check ids to run")]
[CommandFlag("verbose", "Include the evidence behind each verdict", ShortAlias = "v")]
[CommandHelpRemarks("DOCTOR never prints a credential value. It reports which resolution tier would win.")]
[CommandHelpRemarks("'-probe native' loads the inference backend OUT OF PROCESS: a hard native crash cannot kill this shell.")]
public sealed class DoctorCommand : AbstractCommand { … }
```

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `check` | ordered, `UsePipe = true` | `string` | no | `all` | `all` \| `runtime` \| `build` \| `backend` \| `native` \| `credentials` \| `config` \| `storage` | Which group to run. Note `AllowedValues[0]` also auto-populates the default (ref-command §4.1). |
| `only` | named | `string` | no | *(none)* | comma-separated check ids, e.g. `NAT-002,CFG-004` | Run exactly these checks. |
| `probe` | named | `string` | no | `none` | `none` \| `native` \| `endpoints` \| `all` | Opt in to **active** probing. `none` is passive inspection only — no native load, no network. |
| `severity` | named | `string` | no | `info` | `info` \| `warn` \| `error` | Minimum severity emitted. |
| `format` | named | `string` | no | `text` | `text` \| `json` \| `csv` | `json` emits one object per check with `id`, `group`, `status`, `summary`, `evidence`, `remedy`; declares `ResultFormat.JSON`. |
| `verbose` | flag (`ShortAlias = "v"`) | `bool` | no | `false` | — | Include the evidence string and the remedy for passing checks too. |

**The checks.** Each emits one chunk with a status of `pass` / `warn` / `fail` / `skipped`.

*Group `runtime`*

| Id | Checks | Fails / warns when |
|---|---|---|
| `RUN-001` | Runtime version and TFM of the loaded product assemblies (`net10.0` expected — the source's four projects all targeted `net10.0`). | Major version differs from the built-against TFM. |
| `RUN-002` | OS, architecture, and the resolved runtime identifier (`win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`). | Reports `osx-*` as **supported-but-never-released**: the source documented macOS triples but built none (packaging P10, Q6). |
| `RUN-003` | Globalization mode. | `warn` when running invariant-only, because that changes string comparison and number/date formatting — the source forced it in `Compact` and `SingleFile` but not in Debug/Release, so the same source emitted two timestamp shapes and parsed `/set temperature 0.7` differently on a comma-decimal locale (QUIRK-13, settings Q15). |
| `RUN-004` | Working set and available memory against the configured `llamaContextSize`. | `warn` below the documented 8 GB build/run guidance. |

*Group `build`* (delegates its facts to, but does not depend on, `ChatDbg.Tools.Packaging`)

| Id | Checks | Fails / warns when |
|---|---|---|
| `BLD-001` | Assembly identity metadata present (version, title, company, product, copyright). | `warn` when absent — the source's `Compact` profile stripped exactly this metadata while `SingleFile` kept it, so a support engineer could not tell one `Compact` build from another (Q35, R19/R33). |
| `BLD-002` | Trimming / AOT / single-file status. | `warn` when trimmed, `error` when `TrimMode=full`: all of the product's persistence runs through reflection-driven JSON with no serializer context, no trim annotations and every trim diagnostic suppressed (Q13, R76/R77). |
| `BLD-003` | Stack-trace metadata and framework message resources. | `warn` when stack traces are stripped or framework messages are symbolic resource keys, because **every `ERROR` entry this package writes interpolates a whole exception** and degrades to an opaque key with no message and no frames in exactly the configurations users run (Q12/R21/R24). |
| `BLD-004` | Runtime-configuration side-car presence for a bundled build. | `error` when a bundled, JIT artefact was published without it — the suspected fatal defect in the profile that actually ships (R38/Q22/F15). |

*Group `backend`*

| Id | Checks |
|---|---|
| `BCK-001` | Configured provider (`azure` \| `bedrock` \| `llama`) and whether the package that implements it is loaded. |
| `BCK-002` | Azure: endpoint present and syntactically an HTTPS URL; deployment/model id non-empty. `-probe endpoints` adds a TLS reachability check (no credentials sent). |
| `BCK-003` | Bedrock: region present and in the known region list; model id non-empty. `-probe endpoints` resolves the regional endpoint host. |
| `BCK-004` | Local: model path set, file exists, extension is `.gguf`, size is plausible. **Reproduces the source's weakness explicitly**: its "is this configured?" check reported *configured* for **any existing file, including a zero-byte non-GGUF one** (local-inference acceptance criterion 2). `DOCTOR` reports `warn: model file is not a GGUF container (magic bytes 00 00 00 00)` — a header read, not a load. |

*Group `native`*

| Id | Checks |
|---|---|
| `NAT-001` | Presence of the backend native library for the resolved RID: `runtimes/win-x64/native/llama.dll`, `runtimes/linux-x64/native/libllama.so`, `runtimes/osx-{x64,arm64}/native/libllama.dylib`. Reports path, size, mtime. |
| `NAT-002` | **Both CPU and CUDA-12 backends present at once.** `warn` — the source referenced both unconditionally in the same project, and its own troubleshooting document names shipping both as a cause of native load failure and recommends keeping one (packaging P12, local-inference external-technology table). |
| `NAT-003` | Architecture of the native image vs the process architecture. `fail` on a mismatch. |
| `NAT-004` | Platform prerequisites: on Windows, the Microsoft C++ runtime redistributables; on Linux, `glibc` version and the presence of `libgomp`; on macOS, code-signing/quarantine attributes on the dylib. Each is a documented cause of the `0xC0000005`-class failure this package exists to diagnose. |
| `NAT-005` | CUDA availability when the CUDA backend is present: driver version and device count, read from the driver API without initialising a context. `skipped` on hosts with no NVIDIA driver — not a failure. |
| `NAT-006` | **Active load probe.** `skipped` unless `-probe native`. Runs **out of process**: a helper process loads the backend and the model header and reports back. A hard native access violation kills the helper, and `DOCTOR` reports `fail: native load crashed the probe process (exit 0xC0000005) — see the daily log`. **This is the single most valuable check in the package**, because in the source that crash killed the shell and left only whatever had already been flushed. |

*Group `credentials`* — **presence and provenance only, never values**

| Id | Checks |
|---|---|
| `CRD-001` | The resolution tiers, in the source's exact order: **environment variables → OS credential vault → settings-file field**. Reports which tier *would* win for each of the three credentials. |
| `CRD-002` | Environment tier, by declared order, first non-empty wins, empty string treated as absent: `CHATDBG_AZURE_API_KEY`; `CHATDBG_AWS_ACCESS_KEY` then `AWS_ACCESS_KEY_ID`; `CHATDBG_AWS_SECRET_KEY` then `AWS_SECRET_ACCESS_KEY`. Output is `set` / `unset` and the winning variable **name**. |
| `CRD-003` | Vault tier: on Windows, existence of `ChatDbg:AzureApiKey` / `ChatDbg:AwsAccessKey` / `ChatDbg:AwsSecretKey`. On macOS/Linux: `unavailable — no OS keystore integration on this platform; available tiers are environment variables and the settings file`. **Improvement on the source's probe**, which was a bare "is the OS Windows" test that never asked whether the vault service actually answers, so a Windows host with the credential service disabled by policy looked healthy and failed later with a generic message. `DOCTOR` performs a real read-of-a-nonexistent-name probe and distinguishes *available* from *present-but-refusing*. |
| `CRD-004` | **Plaintext-in-settings detection.** `warn` when any of `azureApiKey` / `awsAccessKey` / `awsSecretKey` is non-empty in the settings file, with the remedy naming the environment variables — **and without printing the value**. The source's migration wizard interpolated the actual plaintext secret into `set CHATDBG_…=<value>` lines and did so on **every settings load** (credential Q3). This package will not reproduce that. |
| `CRD-005` | **Half a key pair.** `warn` when an AWS access key resolves but its secret does not, or vice versa — the source declared itself configured in that state (credential acceptance criterion 24). |

*Group `config`* — validated against the product's real ranges

| Id | Setting | Valid range (inclusive) |
|---|---|---|
| `CFG-001` | `temperature` | `0.0` … `2.0` |
| `CFG-002` | `maxTokens` | `1` … `8192` |
| `CFG-003` | `logProbabilitiesTopK` | `1` … `20` |
| `CFG-004` | `gridViewMaxAlternatives` | `1` … `20` |
| `CFG-005` | `llamaContextSize` | `512` … `32768` |
| `CFG-006` | `llamaGpuLayerCount` | `0` … `100` |
| `CFG-007` | `llamaThreads` | `0` … `64` |
| `CFG-008` | `llamaBatchSize` | `1` … `2048` |
| `CFG-009` | `provider` | `azure` \| `bedrock` \| `llama` |
| `CFG-010` | Inert-setting warnings | `warn`: `llamaGpuDevice`, `llamaThreads` and `llamaBatchSize` are validated, clamped, persisted and displayed but **reach no runtime** (settings Q6); `warn`: `maxTokens` is **not sent by the Azure path** unless log probabilities are on (settings Q32). Reporting a setting that silently does nothing is exactly the doctor's job. |
| `CFG-011` | Culture sensitivity of the persisted document | `warn` when the settings file was written under a comma-decimal locale by a non-invariant build and would fail to re-parse under the shipped invariant build (settings Q15). |

*Group `storage`*

| Id | Checks |
|---|---|
| `STO-001` | The three per-user roots exist and are writable: settings `<user profile>/.ChatDbg`, prompts `<local app data>/ChatDbg/system_prompts`, logs `<roaming app data>/ChatDbg/Logs`. |
| `STO-002` | `fail` when the app-data root resolves to the **empty string** (no `HOME` on a Unix-like host), which in the source silently turned the log directory into the *relative* path `ChatDbg/Logs` under the process working directory with nothing guarding it. The rebuild refuses that and names a deterministic fallback. |
| `STO-003` | `warn` when the settings store has fallen back to the temporary directory — the source did that inside a bare catch-all, so on a locked-down or service account settings appeared to save and then vanished (packaging Q18). |
| `STO-004` | Free space in the log root against the retention cap. |

**Pipeline behaviour** — **Both.** As a source it overrides `Main` and emits **one chunk per check**, so
`DIAG DOCTOR | REGIF "fail"` is a working health gate. As a filter, **one piped chunk means one check id or group
name**, so a check list can be generated elsewhere and fed in. With `-format json` it emits one JSON object per
check and declares `ResultFormat.JSON`; with `-format csv`, a header chunk then one row per check
(`ResultFormat.CSV`).

**Environment interaction** — Reads the `CHATDBG_DIAG_*` shell globals for its own configuration, and reads the
**OS process environment** (a different store from `IEnvironmentContext`) for credential-variable *presence*:
`CHATDBG_AZURE_API_KEY`, `CHATDBG_AWS_ACCESS_KEY`, `AWS_ACCESS_KEY_ID`, `CHATDBG_AWS_SECRET_KEY`,
`AWS_SECRET_ACCESS_KEY`. It **never writes an OS environment variable** and needs no environment-modifying
permission. It writes `DOCTOR_LAST_RUN` and `DOCTOR_LAST_STATUS` into its own prefixed bucket so the shell can show
a health badge without re-running the checks.

**Failure modes**

| Situation | Behaviour |
|---|---|
| A single check throws | That check reports `fail` with the exception message as evidence; **the run continues**. A doctor that dies on its first sick patient is useless. |
| `-only` names an unknown id | `Warning:` chunk listing the valid ids, then runs the rest. |
| `-probe native` refused by host policy or unavailable | `skipped (policy)` — never a failure. |
| `-probe endpoints` with no network | `NET unreachable` reported as `warn`, not `fail`: an offline machine running the local provider is a perfectly healthy machine. |
| Settings file absent | `config` group reports `pass — no settings file; built-in defaults apply` and lists them (`provider azure`, `modelId gpt-4`, `temperature 0.7`, `maxTokens 1000`, `awsRegion us-east-1`, `systemPromptName default`, log probabilities off, `logProbabilitiesTopK 5`, `llamaContextSize 4096`, `llamaGpuLayerCount 0`, `llamaThreads 0`, `llamaBatchSize 512`). |
| Zero arguments | Runs `all` at `severity info` with `probe none`. Safe, read-only, offline. |
| Downstream error | Propagated; the doctor does not retry. |

**Security and audit** — **This is the tool most likely to leak a secret, and it is specified never to.** No check
reads secret material: environment checks read *names* and test for emptiness; the vault check tests for entry
existence; the settings check tests for a non-empty field. Output carries `set` / `unset` / `present` and a
**source label** (`environment variable (AWS_ACCESS_KEY_ID)`, `OS credential vault`, `settings file (deprecated)`),
never a value, never a prefix, never a hash. Audit metadata records the check ids run and the aggregate verdict.
The tool is entirely **read-only** except for `-probe native`, which spawns a short-lived helper process — that is
recorded in the audit event and is the only reason a host might want to withhold it.

**Traceability** — PRD **7.11 Diagnostic Logging**, drawing evidence owned by **7.2 Settings & Configuration**,
**7.3 Credential Management**, **7.6/7.8 Provider Abstraction & Local Inference**, and **7.15 Packaging & Release**.
**NEW as a command.** In the source these facts existed only as scattered one-line startup prints (`Azure credentials
loaded from: …`, `Warning: … service is not configured`), a four-cause error string pasted into a chat reply after
a model-load failure, and a 300-line hand-written troubleshooting document for a native access violation. This tool
earns its place by turning that document into an executable check.

---

#### 8.3.11 `DIAG AUDIT` — configure the audit stream

**Registration**

| Field | Value |
|---|---|
| Command | `AUDIT` |
| Root command | `DIAG` |
| Description | `Configure structured command-execution auditing and its redaction policy` |
| Prototype | `DIAG AUDIT [on|off|status] [-sink log|stdout|file] [-path <file>] [-redact <names>] [-placeholder <text>]` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `state` | ordered | `string` | no | `status` | `on` \| `off` \| `status` | Enable, disable, or report. |
| `sink` | named | `string` | no | `log` | `log` \| `stdout` \| `file` | `log` routes audit events into this package's recorder (so they land in the daily file); `stdout` matches the framework's shipped `StructuredAuditLogger` default; `file` writes JSON lines to `-path`. |
| `path` | named | `string` | no | `<log root>/audit_{0:yyyyMMdd}.jsonl` | any writable path | Destination when `-sink file`. |
| `redact` | named | `string` | no | `password,pwd,secret,token,apikey,api_key,connectionstring,conn,credential` | comma-separated names | Parameter and variable names whose values are masked. Default reproduces the framework's `AuditMaskingConfiguration` defaults exactly. |
| `placeholder` | named | `string` | no | `[REDACTED]` | 1–32 characters | Replacement text. Matches the framework default. |

**Pipeline behaviour** — **Neither.** One chunk: a confirmation, or, for `status`, a short report including the
honest caveat below.

**Environment interaction** — Registered `modifiesEnvironment: true`; writes `CHATDBG_DIAG_AUDIT`,
`CHATDBG_DIAG_AUDIT_SINK`, `CHATDBG_DIAG_AUDIT_PATH`. The audit logger itself is installed by the **host** via
`IEnvironmentContext.SetAuditLogger` / `CommandController.AuditLogger`; this tool sets policy, and the host reads
it at the next dispatch. Where the host refuses (audit locked by policy), the tool reports `refused by host policy`
rather than failing.

**Failure modes** — Unwritable `-path`: `Failure`, and auditing stays in its previous state rather than silently
degrading to nothing. `status` with no audit logger installed: `Success` with `auditing: not installed by host`.

**Security and audit** — This tool configures the very mechanism that records it, so its own event is emitted
**after** the change with the before/after policy in metadata. It must state, in `status` output and in its help
remarks, the framework's real limitation: **`AuditMaskingConfiguration` only rewrites `-name=value` /
`--name=value` tokens; the framework's own space-separated `-name value` form is not masked, and the argument
tokenizer strips `=` anyway** (ref-command §11.4). The honest consequence, printed verbatim by `DIAG AUDIT status`,
is: *"Parameter masking is unreliable for this framework's syntax — never pass a secret as a command parameter."*
Variable-name-based redaction on the `EnvironmentChange` path **does** work and is reported as such.

**Traceability** — PRD **7.11**, with a boundary to **7.1 Command System & Dispatch** (the host owns installing the
logger; this tool owns the policy). **NEW.** The source had no audit trail at all. It earns its place because this
rebuild's tools are dynamically loaded from a plugin directory, and a plugin host with no record of what ran is not
defensible.

---

#### 8.3.12 `DIAG BUILDINFO` — identify the running artefact

**Registration**

| Field | Value |
|---|---|
| Command | `BUILDINFO` |
| Root command | `DIAG` |
| Description | `Report the identity and publish profile of the running artefact` |
| Prototype | `DIAG BUILDINFO [-format text|json|csv] [-packages]` |

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `format` | named | `string` | no | `text` | `text` \| `json` \| `csv` | Output shape. |
| `packages` | flag | `bool` | no | `false` | — | Also list every loaded tool package: name, version, origin path, and whether it was integrity-verified at load. |

**Pipeline behaviour** — **Produces piped output; accepts none.** Overrides `Main` to emit one chunk per fact:
product version, informational version, commit id when present, TFM, RID, publish profile inference
(`Debug` / `Release` / `Compact`-like / `SingleFile`-like, deduced from AOT, trim, single-file, invariant-globalization
and resource-key switches), globalization mode, stack-trace metadata availability, and the loaded-package inventory.

**Environment interaction** — None written. Reads no OS environment variables.

**Failure modes** — Metadata stripped (the source's `Compact` profile removed assembly identity entirely, Q35): each
missing fact reports `unknown (metadata stripped)` rather than failing. A package whose origin path cannot be read
reports `origin: unavailable`.

**Security and audit** — Emits file paths of loaded packages, which is provenance information a support engineer
needs and an attacker gains little from. Read-only; no confirmation.

**Traceability** — PRD **7.15 Packaging & Release** (reporting side only) with a home in **7.11**. **NEW.** The
source declared its version in **three** independent, unreconciled places — two project files and a hard-coded
`ChatDbg v1.0` literal in the GUI about box (Q34/R81) — and the profile that shipped stripped the metadata that
would have distinguished builds. This tool earns its place because every bug report this package's exports feed
into needs to name the artefact that produced it.

---

### 8.4 Host registration requirements

Three tools must be registered with `modifiesEnvironment: true`, because `ModifiesEnvironment` is a property of the
**registration**, not an attribute, and without it a command can persist only keys carrying its own command-name
prefix (ref-command §7.3):

```csharp
controller.AddCommand("ChatDbg.Diagnostics", new CaptureCommand(recorder), modifiesEnvironment: true);
controller.AddCommand("ChatDbg.Diagnostics", new RotateCommand(recorder),  modifiesEnvironment: true);
controller.AddCommand("ChatDbg.Diagnostics", new AuditCommand(),           modifiesEnvironment: true);
// every other DIAG tool is registered without it
```

The recorder is a **DI singleton** (`services.AddSingleton<IDiagnosticRecorder, DiagnosticRecorder>()`), and the
tools that need it are registered as singletons too — otherwise `CommandFactory` constructs a fresh instance per
execution and, since `CommandExecutor` **does not dispose the instance it executes** (ref-command §2.6), a recorder
owned by a command instance would be orphaned with its buffer un-flushed. That is precisely the source's defect:
its inference service's finalizer skipped recorder teardown, so a GC-reclaimed service lost its entire buffer with
no final flush (diagnostic-logging business rule 22).

---

### 8.5 Pipeline compositions

**1. Assemble a support bundle for a bug report** *(within the package)*

```
DIAG FILES -days 7 | DIAG TAIL -level WARN -n 500 | DIAG EXPORT ./chatdbg-support.log -append
```

`FILES` emits one absolute path per daily file in the last week; `TAIL` opens each and emits only `WARN`/`ERROR`/`FATAL`
entries, newest 500 per file; `EXPORT` writes each surviving entry as a line and finishes with
`Wrote 1 284 lines from 6 files to /home/u/chatdbg-support.log`. The user gets exactly the artefact the source's
documentation promised and its code never produced.

**2. Gate a session on a healthy environment** *(crosses into the framework built-ins)*

```
DIAG DOCTOR -severity error -format text | REGIF "^fail" | SAY
```

`DOCTOR` emits one chunk per check; the built-in `REGIF` filters to failing rows by returning an empty success for
non-matches (which the host drops, ref-command §3.3); `SAY` prints what survives. Nothing printed means nothing
failed — a usable exit condition for a startup script.

**3. Journal a chat turn and its diagnostics together** *(crosses into `ChatDbg.Tools.Inference`)*

```
LLM CHAT "why did the model load fail?" | DIAG RECORD -level INFO -tag chat
```

`LLM CHAT` streams the assistant's reply chunk by chunk; `DIAG RECORD` writes each chunk into the diagnostic buffer
tagged `[chat]` **and re-emits it verbatim**, so the user still sees the reply while the transcript and the engine's
own native log lines end up interleaved, in timestamp order, in the same daily file. `OnStartPipe`/`OnEndPipe`
bracket the turn with `journal opened` / `journal closed, N chunks`.

**4. Mine the per-token diagnostic firehose** *(crosses into `ChatDbg.Tools.TokenAnalysis`)*

```
DIAG TAIL -source all -grep "^Token [0-9]+: " -n 20000 -format json | TOKEN HISTOGRAM -bins 32
```

The source wrote one `DEBUG Token n: '<text>'` entry per generated token on the hot loop whenever probability
capture was on — the highest-volume producer in the product, and previously unreachable. `TAIL` emits them as JSON
objects, one chunk per entry; `TOKEN HISTOGRAM` consumes the stream and renders a distribution. Streaming is real,
so the histogram starts filling before the tail finishes.

**5. Enforce retention without ever risking a wrong delete**

```
DIAG FILES -days 365 | DIAG ROTATE -days 14 -maxtotal 256          # dry run: every line reads "WOULD DELETE …"
DIAG FILES -days 365 | DIAG ROTATE -days 14 -maxtotal 256 -apply   # same plan, applied
```

The same pipeline is run twice — once to read the plan, once to execute it. `ROTATE` refuses any chunk naming a path
outside the configured log root, so a mis-generated upstream cannot direct it at arbitrary files.

**6. Prove a build is safe to trust, and file the proof** *(crosses into `ChatDbg.Tools.Packaging`)*

```
DIAG BUILDINFO -packages -format json | DIAG RECORD -level INFO -tag build -quiet ; DIAG FLUSH
```

Every fact about the running artefact and every loaded tool package is written into the diagnostic log and nothing
is echoed to the terminal (`-quiet` swallows the chunks); `DIAG FLUSH` puts it on disk immediately. Every subsequent
export therefore carries the artefact identity the source's shipping profile had stripped from the binary.

---

### 8.6 Design notes for the architect

**What this package holds.** Exactly one piece of long-lived state: the **recorder** — a buffer, its sink switches,
its policy (threshold, minimum level, timestamp mode, redaction, retention), an armed latch, fault counters, and a
re-entrant lock. Nothing else. The lock's re-entrancy is load-bearing and must be asserted by a test: the
threshold-triggered auto-flush is invoked from *inside* the locked append, so a non-reentrant lock deadlocks the
engine's callback thread (diagnostic-logging business rule 8).

**What this package must not hold.** No model, no inference context, no provider client, no HTTP handler, no
credential value, no settings record, no chat history, no rendering state. `DIAG DOCTOR` reads settings and
credential *provenance* through read-only ports supplied by the host — it does not link against
`ChatDbg.Tools.Configuration` or `ChatDbg.Tools.Credentials`, because a tool package must depend on the SDK only
(Cupcake rule 4). If those ports are not supplied, the affected check groups report `skipped (no provider)` and the
rest of the doctor still runs.

**Ownership of the process-global engine hook.** The native engine's log-sink registration is process-global,
set-only, and last-writer-wins. Exactly one recorder may own it, ownership is recorded, and a second claimant is
refused unless forced. The source silently allowed a later recorder to steal the hook while earlier recorders
continued to believe themselves armed (business rule 21). Equally: the source's disposal path **never unregistered
the callback**, so engine lines kept accumulating into a buffer that would never be flushed again (business rule 20).
The rebuilt recorder unregisters on disposal where the binding permits, and where it does not, it swaps the sink's
target to a no-op sentinel so the abandoned buffer can be collected.

**Testability.** Four seams, all constructor-injected, all with in-memory fakes:
`IClock` (so timestamp shape, day rollover and `-since` are deterministic), `IFileStore` (append, overwrite, exists,
enumerate, delete — so the flush-failure, missing-directory, empty-buffer and retention branches become testable),
`IEngineSink` (so arm/re-arm/steal and the callback path can be exercised with no native library present), and
`IEnvironmentProbe` (OS environment, special folders, RID, runtime switches — so the doctor's every verdict is
reproducible on one machine). This is a direct response to the source's testing posture: **two of its seven recorder
tests asserted nothing at all**, the arm-idempotency test structurally could not reach the armed path because the
native sink registration was swallowed on a host with no native library, and **every failure branch was unreached**
(QUIRK-15, QUIRK-16). The rebuilt equivalents of acceptance criteria 6, 7, 10, 11, 18, 19 and 24 must be real,
asserting tests. Tests must not write into the real temp directory and leave files behind (QUIRK-9).

**When a capability is unavailable — degrade, never fail.** The rule is uniform: *report the degradation in-band and
carry on.*

| Missing capability | Behaviour |
|---|---|
| Native inference engine (any OS) | Capture arms in **application-entries-only** mode; `STATUS` says `engineSink: unavailable (<reason>)`; `DOCTOR` `native` group reports it as `fail` with the platform-specific remedy. Chat with the hosted providers is unaffected. |
| Writable log root | **Memory-only** mode with a bounded buffer that drops oldest and counts drops. `FLUSH` and `EXPORT -source file` report the reason. The product still runs. |
| OS credential keystore (Linux, macOS, or Windows with the service disabled by policy) | `DOCTOR` reports the tier as `unavailable` and names the tiers that remain (environment variables, settings file), rather than pretending Windows-ness implies a working vault. Neither `CAPTURE` nor any other verb is affected. |
| CUDA / GPU | `skipped`, never `fail`. CPU inference is a first-class configuration. |
| Compression | `ROTATE -compress gzip` degrades to `none` with a warning; retention by age and total size still applies. |
| Network | `-probe endpoints` reports `unreachable` as `warn`. An offline machine running the local provider is healthy. |
| Audit logger not installed by the host | `DIAG AUDIT status` says so; no other tool changes behaviour. |
| Host DI container absent | The recorder falls back to a package-scoped process singleton; `STATUS` reports `recorderOwner: package-static` so the difference is visible rather than mysterious. |

**Cross-platform posture, stated because the source's was not.** Nothing in the source's logging code was
Windows-only — every primitive it used was portable — but its *packaging and documentation* were: both shells
defaulted the runtime identifier to `win-x64`, every documented log path was `%APPDATA%\ChatDbg\Logs`, and the only
configuration example used the literal `C:\Logs\LLamaSharp`. This package resolves paths through platform-neutral
well-known-folder lookups on every OS, **refuses the empty-root case** rather than silently writing a
working-directory-relative `ChatDbg/Logs` when `HOME` is unset, names native libraries per platform
(`llama.dll` / `libllama.so` / `libllama.dylib`), and reports `osx-x64` / `osx-arm64` as first-class RIDs even
though the source never built one.

**Where to degrade rather than fail, in one sentence each.**
Arming with an unwritable directory → memory-only, not un-armed.
Flush with the sink off → report and retain, not silently retain.
Export with nothing to write → say so and write no file, not a 0-byte file.
A single doctor check that throws → mark that check failed, not the run.
A rotation candidate that is locked → skip it and report, not abort the sweep.
A piped chunk naming an out-of-root path → refuse that chunk, not the pipeline.
And, above everything: **a diagnostic subsystem that has failed must say so on a channel that exists in the shipping
build** — the one lesson the source's own acceptance criteria record as its worst defect.

---

## 9. ChatDbg.Tools.ToolPackageManagement — Tool Package Management

> Root command: **`PKG`** · Assembly: `ChatDbg.Tools.ToolPackageManagement`
> Source ancestor: **none.** The source product had no runtime extension mechanism at all — its 18 command classes were `new`-ed by hand inside each shell's `InitializeCommands()` and stored in a `Dictionary<string, ICommand>`; three of them were never registered by either shell and were reachable only from tests. This package is derived from `ref-cupcake.md` §7 (`Xcaciv.Command.Packages` — `SearchCommand`, `InstallCommand`, `NugetWrapper`), Cupcake §8 rules 26 and 42–47, and `ref-loader.md` §10 (the twelve-step secure-loading checklist).
> Framework contract: Xcaciv.Command **3.3.4**; loader: Xcaciv.Loader **2.1.2**.
> PRD traceability: **7.1 Command System & Dispatch** (owner of the extension mechanism) with named touch-points into 7.11 (audit/diagnostics) and 7.15 (artefact identity, RIDs, native payloads).

---

### 9.0 Purpose and boundary

**What this package owns.** The *supply chain of capability*. Everything between "a tool exists somewhere" and "a tool is a command this shell will run":

1. **Discovery** — searching a package feed for tool packages, and managing which feeds are consulted.
2. **Acquisition** — downloading a package, re-reading its identity from the artefact rather than trusting the request, extracting it into the layout the crawler scans, and recording what was placed where.
3. **Inventory** — enumerating what is installed, what is loaded, what commands each package contributes, what each package *asked* for and what it was *granted*.
4. **Integrity** — computing and re-checking SHA-256 digests of every file a package ships, maintaining the hash allowlist that `AssemblyIntegrityVerifier` consumes, and detecting drift between "what we installed" and "what is on disk now".
5. **Provenance** — verifying publisher identity (package signature, and platform code-signature where one exists), because `Xcaciv.Loader` verifies bytes and **never** verifies a publisher (ref-loader §4.3).
6. **Trust** — the decision itself: promoting a package from *quarantined* to *sandboxed* to *trusted*, and the record of who decided, when, and against which digest.
7. **Lifecycle** — updating, rolling back, removing, re-scanning, and reporting why a package did not load.
8. **Reproducibility** — a portable lockfile of package identities and content digests, because the loader's own trust store is keyed by absolute machine paths and is therefore not portable (ref-loader §4.2, finding B15).

**What this package explicitly does NOT own.**

| Not owned | Owned by | Where the line is |
|---|---|---|
| Constructing the `ICommandController`, calling `RegisterBuiltInCommands()` / `AddPackageDirectory()` / `LoadCommands()`, the loop, the prompt, `HELP`, exit codes, the startup trust gate | **the host** (`Xcaciv.ChatDbg.Host` — `HostStartup`, `Loop`, `PackageTrustGate`), host chapter §A.3 steps 7–8 | `PKG` is a **tool**, and the dependency arrow points tool → SDK, never tool → host (Cupcake §8 rule 4). It reaches the loader and the registry only through the seams in §9.1. `PKG RELOAD` *asks* the host to re-scan; it never calls `LoadCommands()` itself, and it cannot construct an `AssemblyContext`. |
| The settings document, profiles, validation ranges, and the generic key/value write surface | **`ChatDbg.Tools.ConfigurationProfiles`** (root `SET`) — PRD 7.2 | `PKG` publishes `CHATDBG_PKG_*` into the environment; the configuration package is what persists any of it across sessions. `PKG` writes no settings file. |
| API keys, feed credentials, the OS keystore, masking policy | **`ChatDbg.Tools.CredentialsSecretStorage`** (root `CRED`) — PRD 7.3 | A private feed's token is a credential like any other: `PKG` asks `CRED` for a resolved value by *slot name* at the moment of the request and never stores, prints, pipes or logs it. `PKG SOURCE` stores a source's **credential slot name**, never a secret. |
| Log sinks, rolling files, the support snapshot, the audit stream's configuration | **`ChatDbg.Tools.DiagnosticsObservability`** (root `DIAG`) — PRD 7.11 | `PKG DOCTOR` produces a report; `DIAG RECORD` is what journals it. `PKG` emits audit *events* through the host's `IAuditLogger` like every tool, and owns no sink. |
| Rendering — tables, colour, truncation, terminal width | **`ChatDbg.Tools.Render`** (root `VIEW`) — PRD 7.12 | `PKG LIST` emits rows with a declared `ResultFormat`; what a row *looks like* is the front end's business. |
| Producing the product's own release artefacts, RIDs, trimming profiles, the CI pipeline | **PRD 7.15 Packaging & Release** (build-time, not a runtime tool) | `PKG` consumes packages; it does not build them. `PKG DOCTOR` *reads* RID and TFM metadata to explain a load failure, which is the only overlap. |
| Model backends, chat turns, prompts, token analysis | their own packages | A tool package may *contain* any of those; `PKG` never interprets what a package does. |

**The boundary rule that matters most.** Cupcake ships a shell that advertises `install --help` as the cure for a plugin-less shell while `InstallCommand` installs nothing and `NugetWrapper.InstallPackage` leaves extraction and dependency resolution as `TODO`s. This package closes that loop — *search → acquire → verify → trust → load* — and every step it adds is a step where a trust decision has to be made explicit rather than implied. **Acquiring a tool package is the single highest-privilege act this product performs**: a loaded plugin runs with the host's full trust, in-process, and can read files, open sockets and P/Invoke (ref-loader §10 step 10). Every design choice below follows from that sentence.

---

### 9.1 Package manifest

| Facet | Value |
|---|---|
| Assembly / package name | `ChatDbg.Tools.ToolPackageManagement` (`ChatDbg.Tools.ToolPackageManagement.dll`) |
| Root command | `PKG` — `[CommandRoot("PKG", "Tool package management")]` on every class (uppercased by `NamesValidator`) |
| Contract assemblies targeted | `Xcaciv.Command.Interface` **3.3.4** + `Xcaciv.Command.Core` **3.3.4** (`AbstractCommand`). **No reference to `Xcaciv.Command`** (the host assembly) and **no reference to `Xcaciv.Loader`** — see "Why no loader reference" below. |
| Other references | `ChatDbg.Tools.Abstractions` (contract-only) for `IPackageFeed`, `IPackageStore`, `IPackageManifest`, `ITrustStore`, `ISignatureVerifier`, `ILoaderControl`, `TrustLevel`, `ICancellationSignal`. `NuGet.Protocol` for the default feed implementation — **and that dependency lives in the feed adapter registered by the host, not in the command classes** (§9.5). |
| Target framework | `net10.0`, single TFM across the graph (Cupcake §8 rule 5) |
| Distribution | **In-box only.** Compiled into the host and registered by the executable under package key `inbox` (Cupcake §8 rule 26, path (a)). It is **never** loaded from the package directory, because it is the tool that manages the package directory: a shell with zero packages must still be able to run `pkg --help`, which is the exact recovery line `Loop` prints on `NoPluginsFoundException`. |
| Elevated trust required | **No, and deliberately so.** Installing writes only under the *user* package root and the data root. Installing into the **system** package root (`CHATDBG_PACKAGE_DIR`, next to the executable, read-only to the runtime account by design — ref-loader §10 step 1) is refused with an explanatory failure that names the administrative action the operator must take out-of-band. `PKG` never elevates, never prompts for elevation, and never writes to a directory it also loads from *in the same session*. |
| Network reach | **Outbound HTTPS only, to the configured feed(s), and only from `SEARCH`, `INSTALL`, `UPDATE`, `SOURCE TEST` and `LOCK -restore`.** HTTP is refused, not downgraded (Cupcake §7: `Uri.Scheme == Uri.UriSchemeHttps` or `InvalidOperationException`). Every other tool is offline. `CHATDBG_PKG_NETWORK=off` makes the whole package offline and the five network tools degrade to a named failure rather than hanging. |
| Filesystem reach | **Write:** the quarantine directory (`<DATA_ROOT>/quarantine`), the user package root (`CHATDBG_USER_PACKAGE_DIR`), the trust store (`CHATDBG_TRUST_STORE`), the lockfile (`CHATDBG_PKG_LOCK_PATH`), the source registry (`<DATA_ROOT>/pkg-sources.json`). **Read:** both package roots, `AppContext.BaseDirectory` (for TFM/RID comparison in `DOCTOR`). **Delete:** only inside the user package root and quarantine, only from `REMOVE`, `UPDATE -prune` and `LOCK -clean`, only after confirmation. |
| OS keystore reach | **Indirect only.** A private feed's token is fetched through `CRED` by slot name at request time. `PKG` holds no keystore code and no P/Invoke. |
| Native libraries | **None of its own.** It *inspects* native payloads that packages ship (`runtimes/<rid>/native/*`) as metadata — existence, size, architecture header, RID folder name — and never loads one. |
| Dynamic code generation | **None.** No `Reflection.Emit`, no expression compilation, no `Assembly.Load`. The package is compatible with `DisallowDynamicAssemblies = true`, and it must be, because it is the package that tells other packages they have to be. |
| Reflection use | Read-only metadata over already-loaded assemblies (`AssemblyInformationalVersion`, `TargetFramework`) and **metadata-only** inspection of candidate packages through the host's loader seam. No name-based type resolution, so it survives `TrimMode=full`. |
| Safe to load in a restricted host | **Yes**, with named degradations: no network → discovery and acquisition fail with a clear message, inventory/verify/trust/remove keep working; no writable user root → install and update refuse and say why; no signature verifier on this OS → `CHATDBG_PKG_SIGNATURE_POLICY` drops from `require` to `prefer` with a startup warning (§9.5); no trust store → **the host refuses to load packages at all** (exit 4) and `PKG` reports the same reason rather than offering to create one silently. |
| Environment-modifying registration | **Seven of thirteen tools** — `INSTALL`, `UPDATE`, `REMOVE`, `TRUST`, `UNTRUST`, `SOURCE`, `RELOAD` — registered `modifiesEnvironment: true`. `SEARCH`, `LIST`, `SHOW`, `VERIFY`, `DOCTOR`, `LOCK` are **not**: they write only their own prefixed bucket. |

**Why no `Xcaciv.Loader` reference.** The loading rules in ref-loader §10 — canonicalization, component-wise containment, symlink resolution, policy construction, the integrity verifier, the twelve event subscriptions, `isCollectible`, deterministic unload — are **host policy**, and the host implements them once in `PackageTrustGate`. If `PKG` referenced `Xcaciv.Loader` it would be a second place where `basePathRestriction` could be got wrong, and it would drag an AGPL-3.0-only dependency (ref-loader finding B1) into an assembly that is also meant to be redistributable as an ordinary tool package. `PKG` therefore *computes and records* the facts a trust decision needs (digests, signatures, manifest claims) and *asks* `ILoaderControl` to act on them.

---

### 9.2 Conventions that shape every tool in this package

Six facts govern the whole catalogue. They are stated once here and assumed thereafter.

**C1 — Neither a URL nor a filesystem path can be a parameter.** `NamesValidator.GetArgumentsFromCommandline` matches unquoted tokens as `[\w-]+` and then deletes every character outside `[-_0-9A-Za-z .*?\[\]|"~!@#$%^&*()]` — `:` `/` `\` `=` `,` `;` `<` `>` `{` `}` `+` are stripped **even inside quotes** (ref-command §8.3). `https://api.nuget.org/v3/index.json` arrives as `httpsapi.nuget.orgv3index.json`. There are exactly three lossless channels and this package uses all three: **the pipe** (an `IResult<string>.Output` payload is never tokenized), **`IIoContext.PromptForCommand`** (its return value is never tokenized), and **the environment/config** (host-set, not `SET`-set — `SET`'s own value token is tokenized too). Every tool that would otherwise want a URL or path says so in a `CommandHelpRemarks` and offers the pipe.

**C2 — Package identity is `id[@version]`, and it must be quoted.** `ChatDbg.Tools.Foo` unquoted splits into three tokens on the `.`; quoted, it survives whole because `.` is inside the allowed set. Versions (`1.4.2`, `1.5.0-beta.3`) survive quoted for the same reason. Every tool therefore accepts one **quoted** `id[@version]` token, or takes identities from the pipe, one per chunk. `PKG` **never** takes a package's directory path.

**C3 — Zero arguments ⇒ empty parameter dictionary.** `AbstractCommand.ProcessParameters` early-returns when `io.Parameters.Length == 0`: no defaults applied, no flags materialised as `false`, no field injection (ref-command §3.4). Every tool below behaves correctly when invoked bare, using the documented default in its parameter table as its in-code fallback, read defensively as `parameters.TryGetValue(k, out var p) && p.IsValid ? p.GetValue<T>() : fallback`.

**C4 — Parse errors are invisible.** An `ArgumentException` for a missing required parameter surfaces only as `Error executing PKG (see trace for more info)` (ref-command §5.8). Therefore **no tool in this package declares a required parameter.** Ordered identity parameters are declared `IsRequired = false` and the tool returns a hand-written `Failure` carrying real usage text. Every named parameter carries a `DefaultValue`, so a dangling `-name` at end-of-line uses the default instead of throwing `ArgumentOutOfRangeException`.

**C5 — The four states of a package, and the layout they live in.**

| State | Meaning | On disk | Loaded? | Env-modifying rights? |
|---|---|---|---|---|
| `quarantined` | Downloaded, identity re-read from the artefact, not yet verified or decided | `<DATA_ROOT>/quarantine/{id}/{version}/` | never | no |
| `blocked` | An explicit deny: a digest mismatch, a failed signature under `require`, or an operator `PKG UNTRUST -block` | stays in quarantine, or is moved back into it from the package root | never | no |
| `sandboxed` | Verified and installed; its commands run | `{root}/{id}/{version}/bin/*.dll` | yes | **no** — writes land only in its own command-prefixed bucket |
| `trusted` | An operator granted it, interactively, against a named digest | same | yes | yes, and only for the command names its manifest declares |

The on-disk layout `{root}/{id}/{version}/bin/` is not a preference: `Crawler.CrawlPackagePaths` scans `*/{subDirectory}/*.dll` with `subDirectory = "bin"`, and derives the package key as `{fileNameWithoutExtension}-{relativeDirectoryWithSeparatorsRemoved}` (ref-command §11.1). `NugetWrapper.InstallPackage` already lays out `{targetDirectory}/{id}/{version}/` (Cupcake §7); this package supplies the `bin` level Cupcake left as a `TODO`.

**C6 — Two roots, one of which is off by default.** `CHATDBG_PACKAGE_DIR` (beside the executable, read-only to the runtime account) is the **system** root; `CHATDBG_USER_PACKAGE_DIR` (`<DATA_ROOT>/tools`) is the **user** root and is scanned **only when `CHATDBG_ALLOW_USER_PACKAGES=true`**, which is set from the command line or configuration at startup and is read-only at runtime (host §C.4). `PKG INSTALL` writes to the user root; if user packages are disabled it installs into quarantine and tells the user exactly which switch enables them. This is the direct application of ref-loader §10 step 1: *never install into a directory the host loads from while it is loading from it*.

**Environment keys this package owns.** All are written by the seven environment-modifying tools and read by all thirteen with `storeDefault: false` (ref-command §7.1 — a bare read otherwise writes the default back and flips `HasChanged`).

| Key | Type | Default | Written by | Meaning |
|---|---|---|---|---|
| `CHATDBG_PKG_SOURCE` | string (feed **name**, not URL) | `nuget.org` | `SOURCE` | The active feed. The URL lives in the source registry file; only the name is environment data (C1). |
| `CHATDBG_PKG_PRERELEASE` | bool | `false` | `SOURCE`, `INSTALL`, `UPDATE` | Default for the `-prerelease` flag. |
| `CHATDBG_PKG_NETWORK` | enum `on` \| `off` | `on` | `SOURCE` | Global offline switch. |
| `CHATDBG_PKG_TIMEOUT` | int seconds, `5`–`600` | `60` | `SOURCE` | Per-feed-request timeout. **NEW** — Cupcake's `NugetWrapper` defaults every call to `CancellationToken.None`, i.e. no timeout at all. |
| `CHATDBG_PKG_SIGNATURE_POLICY` | enum `require` \| `prefer` \| `off` | `require` where a verifier exists, else `prefer` (§9.5) | `SOURCE`, `TRUST` | Whether an unsigned or unverifiable package may be installed / trusted. |
| `CHATDBG_PKG_QUARANTINE_DIR` | absolute path | `<DATA_ROOT>/quarantine` | host only (RO) | Staging area. Never scanned by the crawler. |
| `CHATDBG_PKG_LOCK_PATH` | absolute path | `<DATA_ROOT>/tools.lock.json` | host only (RO) | Lockfile location (C1: a path can only arrive this way). |
| `CHATDBG_PKG_COUNT` | int | computed | `INSTALL`, `REMOVE`, `UPDATE`, `RELOAD` | Installed package count. |
| `CHATDBG_PKG_LOADED` | int | computed | `RELOAD` | Packages whose commands are in the registry. |
| `CHATDBG_PKG_RELOAD_PENDING` | bool | `false` | `INSTALL`, `REMOVE`, `UPDATE`, `TRUST`, `UNTRUST`, `RELOAD` | Set when the on-disk set no longer matches the loaded set. The host renders a `*` in the prompt while it is true. |
| `CHATDBG_PKG_LAST_VERIFY` | ISO-8601 UTC | *(empty)* | `VERIFY` | When the installed set was last checked against the allowlist. |

**OS environment variables read** (never written): `NUGET_LOCAL_PACKAGES` — a local development feed, honoured exactly as Cupcake §8 rule 36 prescribes, and always mapped only to first-party `ChatDbg.*` / `Xcaciv.*` id patterns; `HTTPS_PROXY` / `NO_PROXY` — consumed by the HTTP stack, never parsed by this package. **No tool in this package needs OS-environment-modifying permission**, and none writes an OS environment variable.

---

### 9.3 Tool catalog

Thirteen tools. Two are ports of Cupcake's `Xcaciv.Command.Packages` commands; eleven are **NEW**, and each states why it earns its place.

---

#### 9.3.1 `PKG SEARCH` — find tool packages on a feed

**Registration**

| Field | Value |
|---|---|
| Command | `SEARCH` |
| Root command | `PKG` |
| Description | `Search a package feed for installable tool packages` |
| Prototype | `PKG SEARCH <terms...> [-source <name>] [-take <n>] [-verbosity quiet\|normal\|detailed] [-prerelease]` |

```csharp
[CommandRoot("PKG", "Tool package management")]
[CommandRegister("Search", "Search a package feed for installable tool packages",
    Prototype = "PKG SEARCH <terms...> [-source <name>] [-take <n>] [-verbosity quiet|normal|detailed] [-prerelease]",
    Version = "1.0.0")]
[CommandParameterNamed("source", "Feed name from PKG SOURCE LIST", DefaultValue = "")]
[CommandParameterNamed("take", "Maximum results to return", DataType = typeof(int), DefaultValue = "20")]
[CommandParameterNamed("verbosity", "Level of detail per result",
    AllowedValues = new[] { "quiet", "normal", "detailed" }, DefaultValue = "normal")]
[CommandFlag("prerelease", "Include prerelease versions in the results", ShortAlias = "pre")]
[CommandParameterSuffix("terms", "Search terms")]
[CommandHelpRemarks("-source names a feed registered with PKG SOURCE ADD. A URL cannot be typed here: the argument tokenizer removes ':' and '/'.")]
[CommandHelpRemarks("Example: PKG SEARCH chatdbg tools -take 5 -verbosity detailed")]
[CommandHelpRemarks("Piping: PKG SEARCH logprob -verbosity quiet | PKG INSTALL -dry")]
public sealed class SearchCommand : AbstractCommand { … }
```

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `terms` | suffix | `string` | no | *(empty ⇒ empty output)* | free text; **trimmed**; whitespace-only returns empty; **truncated at 200 characters** | All remaining tokens joined by single spaces become the query. Source parity: Cupcake trims, returns `string.Empty` for whitespace-only, and truncates above 200 chars. |
| `source` | named | `string` | no | `""` ⇒ `CHATDBG_PKG_SOURCE` ⇒ the built-in `nuget.org` entry, URL `https://api.nuget.org/v3/index.json` | a name from `PKG SOURCE LIST` | **NEW shape** — Cupcake took the raw URL; C1 makes that unusable, so this takes a registered *name*. |
| `take` | named | `int` | no | `20` | **clamped to `[1, 100]`** via `Math.Clamp`; non-numeric is rejected | Source parity, exactly: Cupcake's default `"20"` and clamp `[1,100]`. |
| `verbosity` | named | `string` | no | `normal` | `quiet` \| `normal` \| `detailed`; an unrecognised value falls back to `normal` (the `default:` arm) | `quiet` → ids only. `normal` → `{id} {version} : {summary}`. `detailed` → adds download count, `Published`, `Authors`, `License` and **`Vulnerabilities:{count}`**, rows separated by `---`. Source parity. |
| `prerelease` | flag | `bool` | n/a | `false`, seeded from `CHATDBG_PKG_PRERELEASE` when the flag is absent **and** other arguments were supplied (C3) | presence = true | Include prerelease versions. |

**Pipeline behaviour** — **Source.** It **refuses piped input** (Cupcake §8 rule 29 and `SearchCommand.HandlePipedChunk`'s explicit decline): `HandlePipedChunk` returns `Failure("PKG SEARCH does not accept piped input. Pipe its output into PKG INSTALL, or use PKG SHOW to look up one id.")` rather than throwing. It **produces** piped output and, **deviating deliberately from Cupcake**, emits **one chunk per result row** instead of one chunk containing `string.Join("\n", …)`. That requires overriding `Main` — `AbstractCommand`'s non-piped path emits exactly one chunk (ref-command §3.3) — and the override is the whole reason `PKG SEARCH -verbosity quiet | PKG INSTALL` is expressible. `OutputFormat = ResultFormat.General`, or `JSON` when `CHATDBG_OUTPUT_FORMAT=json` and `-verbosity detailed` (host §D.1: a stable-shaped record set a script would consume).

**Environment interaction** — Reads `CHATDBG_PKG_SOURCE`, `CHATDBG_PKG_PRERELEASE`, `CHATDBG_PKG_NETWORK`, `CHATDBG_PKG_TIMEOUT`, `CHATDBG_OUTPUT_FORMAT` (all `storeDefault: false`). `GetDefaultEnvironment()` declares `("TAKE","20")`, `("VERBOSITY","normal")`, which the host seeds as `SEARCH_TAKE` / `SEARCH_VERBOSITY`. **Writes nothing**; registered without `modifiesEnvironment`. Needs no environment-modifying permission.

**Failure modes**

| Situation | What the user sees |
|---|---|
| No terms, or whitespace only | Empty successful output — silently nothing, exactly as Cupcake. (A successful empty chunk is dropped by the executor, ref-command §3.3.) |
| `-source` names an unregistered feed | `Failure("unknown feed 'corp' — run 'PKG SOURCE LIST'")`. Never falls back to the default silently. |
| The resolved feed URL is not HTTPS | `Failure("insecure or invalid package source URL for feed 'x'. HTTPS is required.")` — the source's literal check, moved from an exception to a failure chunk so it does not become `Error executing PKG (see trace)`. |
| `-take` non-numeric | Parameter is invalid; the tool falls back to `20` and prepends `Warning: -take '<raw>' is not a number; using 20.` Cupcake threw `InvalidOperationException` here. |
| Feed unreachable / DNS / TLS failure / timeout | `Failure("feed 'nuget.org' did not respond within 60s — check the network, or PKG SOURCE USE <other>", ex)`. One retry on a transient transport failure, then give up. |
| `CHATDBG_PKG_NETWORK=off` | `Failure("network access is disabled (CHATDBG_PKG_NETWORK=off)")` — immediate, no socket opened. |
| Zero results | Successful single chunk `no packages matched '<terms>' on feed 'nuget.org'`. Not a failure. |
| Piped input | The explanatory failure above. |

**Security and audit** — No parameter carries a secret; a private feed's token is resolved through `CRED` at request time and never appears in a parameter, in output, or in an audit record. `-verbosity detailed` surfaces `Vulnerabilities:{count}` from the feed, which is a trust signal the operator should see *before* installing (Cupcake §8 rule 45). Not destructive. The audit event records the feed **name**, the term count and the result count — **never** the raw query, because a query is user text.

**Traceability** — PRD **7.1**. `origin: ported:Xcaciv.Command.Packages/SearchCommand` — the only fully implemented command in Cupcake's package project. Behaviour preserved verbatim: defaults, the `[1,100]` clamp, the 200-character truncation, the three verbosity shapes and their `default:` arm, and the piped-input refusal. Deviations: named-feed instead of raw URL (C1), per-row chunks, timeout, and a failure chunk in place of two thrown `InvalidOperationException`s.

---

#### 9.3.2 `PKG INSTALL` — acquire a package into quarantine, verify it, and place it

**Registration**

| Field | Value |
|---|---|
| Command | `INSTALL` |
| Root command | `PKG` |
| Description | `Download, verify and install a tool package` |
| Prototype | `PKG INSTALL "<id[@version]>" [-source <name>] [-into user\|quarantine] [-prerelease] [-dry] [-deps allow\|reject] [-force]` |

```csharp
[CommandRoot("PKG", "Tool package management")]
[CommandRegister("Install", "Download, verify and install a tool package",
    Prototype = "PKG INSTALL \"<id[@version]>\" [-source <name>] [-into user|quarantine] [-prerelease] [-dry] [-deps allow|reject] [-force]",
    Version = "1.0.0")]
[CommandParameterOrdered("package", "Package identity as id or id@version — quote it", IsRequired = false, UsePipe = true)]
[CommandParameterNamed("source", "Feed name from PKG SOURCE LIST", DefaultValue = "")]
[CommandParameterNamed("into", "Destination root",
    AllowedValues = new[] { "user", "quarantine" }, DefaultValue = "user")]
[CommandParameterNamed("deps", "Policy for package dependencies",
    AllowedValues = new[] { "allow", "reject" }, DefaultValue = "reject")]
[CommandFlag("prerelease", "Allow a prerelease version to satisfy the request", ShortAlias = "pre")]
[CommandFlag("dry", "Resolve, download and verify, but do not place the package")]
[CommandFlag("force", "Skip the confirmation prompt when overwriting an installed version")]
[CommandHelpRemarks("Quote the id: unquoted 'ChatDbg.Tools.Foo' is split into three tokens by the argument tokenizer.")]
[CommandHelpRemarks("Installed packages are SANDBOXED. They do not get environment-modifying rights until PKG TRUST grants them.")]
[CommandHelpRemarks("Commands appear after PKG RELOAD, or on the next start.")]
public sealed class InstallCommand : AbstractCommand { … }
```

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `package` | ordered, `UsePipe = true` | `string` | no (C4) | *(none — absence is a hand-written failure)* | `id` or `id@version`; id matches `[A-Za-z0-9._-]{1,128}`; version is a SemVer 2.0 string | The package to install. Omit the version to take the highest stable (or highest prerelease with `-prerelease`). Fed by the pipe when piped. |
| `source` | named | `string` | no | `CHATDBG_PKG_SOURCE` | a registered feed name | Feed to resolve from. **NEW** vs Cupcake (which had no `-source` on install at all). |
| `into` | named | `string` | no | `user` | `user` \| `quarantine` | `user` places into `CHATDBG_USER_PACKAGE_DIR` after verification; `quarantine` stops after verification and leaves the package staged. **NEW.** |
| `deps` | named | `string` | no | `reject` | `allow` \| `reject` | Whether transitive package dependencies may be acquired. **Default is `reject`**, deliberately: Cupcake left dependency resolution a `TODO`, and each additional assembly is another thing the integrity allowlist must cover and another `deps.json` probe path that the loader does **not** confine to the base path (ref-loader finding B10). `allow` resolves the closure, verifies every file, and lists every added assembly in the confirmation. **NEW.** |
| `prerelease` | flag | `bool` | n/a | `false` (seeded from `CHATDBG_PKG_PRERELEASE`) | presence = true | Permit a prerelease to satisfy an unpinned request. |
| `dry` | flag | `bool` | n/a | `false` | presence = true | Resolve → download → re-read identity → hash → verify signature → report, and stop. Nothing is placed, nothing is trusted, quarantine is cleaned up afterwards. **NEW**, and the recommended first move for any package the operator has not seen before. |
| `force` | flag | `bool` | n/a | `false` | presence = true | Skip the overwrite confirmation only. **It does not skip verification and it cannot grant trust.** |

**Pipeline behaviour** — **Filter.** With a pipe, **one chunk is one `id[@version]`** (leading/trailing whitespace trimmed; a chunk that is a `quiet`-verbosity `PKG SEARCH` row is exactly this shape); `package` is declared `UsePipe = true`, so it is not demanded on the command line when piped (ref-command §3.5). Each chunk yields exactly one result chunk — a confirmation or a failure — so a ten-package install produces ten rows and one bad id does not stop the other nine (a failure chunk travels to the end of the pipeline and downstream stages keep running, ref-command §9.6). `OnStartPipe` opens one feed session and one quarantine transaction; `OnEndPipe` publishes the counters and sets `CHATDBG_PKG_RELOAD_PENDING`. Without a pipe it emits one chunk. `OutputFormat = General`.

**Environment interaction** — Reads `CHATDBG_PKG_SOURCE`, `CHATDBG_PKG_PRERELEASE`, `CHATDBG_PKG_NETWORK`, `CHATDBG_PKG_TIMEOUT`, `CHATDBG_PKG_SIGNATURE_POLICY`, `CHATDBG_PKG_QUARANTINE_DIR`, `CHATDBG_USER_PACKAGE_DIR`, `CHATDBG_ALLOW_USER_PACKAGES`, `CHATDBG_TRUST_STORE`. **Writes globals** `CHATDBG_PKG_COUNT`, `CHATDBG_PKG_RELOAD_PENDING` — registered `modifiesEnvironment: true`. Needs no OS-environment permission.

**The acquisition sequence** (each step names its failure):

1. **Resolve** id → concrete `PackageIdentity` on the named feed (`FindPackageVersionsAsync` equivalent). A nonexistent package yields an empty version list rather than an exception — Cupcake's own test pins that — so this is a clean failure, not a crash.
2. **Download** the `.nupkg` as `{id}.{version}.nupkg` into quarantine (Cupcake's exact filename shape).
3. **Re-read identity from the artefact**, not from the request (Cupcake §8 rule 46). A mismatch between requested and actual id/version is a **hard stop**, quarantine is purged, and the event is audited as `PackageRejected`.
4. **Verify the package signature** per `CHATDBG_PKG_SIGNATURE_POLICY`. `require` → unsigned or unverifiable is a hard stop. `prefer` → a warning is attached to the confirmation and the package is marked `signature: none` in the store. `off` → skipped, and the confirmation says so.
5. **Extract** into `<quarantine>/{id}/{version}/`, with zip-slip refusal (every entry's resolved path must remain under the destination, checked component-wise, not by `StartsWith` — ref-loader finding B8), a per-entry and total size ceiling, an entry-count ceiling, and rejection of any absolute or `..`-bearing entry name.
6. **Hash every extracted file** with SHA-256, Base64-encoded — the exact shape `AssemblyIntegrityVerifier` computes and `AssemblyHashStore` persists.
7. **Inspect** the payload: TFM folders, RID folders, native payloads, the presence of `[CommandRegister]`-bearing types (metadata-only, through the loader seam), the manifest's declared commands and `modifiesEnvironment` list, and whether a private copy of `Xcaciv.Command.Interface` is shipped (a package that ships one causes `ReflectionTypeLoadException` at crawl time and the crawler will silently skip it — so this is a **hard stop** with a message that names the offending file).
8. **Place** — move `{id}/{version}/` under the destination root and put the assemblies in its `bin` sub-directory, so the crawler's `*/bin/*.dll` mask finds them. The move is atomic per version directory; a partial move is rolled back.
9. **Record** — write the file digests into the trust store as *known but sandboxed*, with **absolute paths** (a relative path in the CSV never matches on lookup — ref-loader finding B7), and write the store's own digest into the package record.
10. **Signal** — set `CHATDBG_PKG_RELOAD_PENDING=true` and tell the user to run `PKG RELOAD`.

**Failure modes**

| Situation | What the user sees |
|---|---|
| No `package` argument and no pipe | `Failure("PKG INSTALL needs a package id. Usage: PKG INSTALL \"ChatDbg.Tools.Foo@1.2.0\". Quote the id.")` |
| Id contains characters the tokenizer ate (e.g. arrived as `ChatDbgToolsFoo`) | `Failure("'ChatDbgToolsFoo' is not a known package id — did you forget to quote it? The tokenizer removes '.' from unquoted tokens.")` — the single most valuable message in the package. |
| Version not found / package not found | `Failure("no version of 'X' matches (prerelease: off) on feed 'nuget.org'")` |
| Signature required and absent/invalid | `Failure("'X@1.2.0' is unsigned; CHATDBG_PKG_SIGNATURE_POLICY=require. Inspect it with 'PKG INSTALL \"X@1.2.0\" -dry', then lower the policy deliberately if you accept the risk.")`; audited `PackageRejected`. |
| Extraction hazard (zip-slip, absolute entry, size/entry ceiling) | Hard stop, quarantine purged, `SecurityViolation` audit record, `Failure("package 'X@1.2.0' contains an unsafe archive entry and was rejected")`. The entry name is written to the trace, not to the user. |
| Version directory already exists | Interactive typed confirmation naming `id@version` (host §D.4). `-force` skips it. Piped and non-interactive: **refused** — `Failure("'X@1.2.0' is already installed; re-run with -force")`. |
| `-into user` with `CHATDBG_ALLOW_USER_PACKAGES=false` | Package is left in quarantine and `Failure("user packages are disabled; 'X@1.2.0' is staged in quarantine. Start with --allow-user-packages, or install it into the system root out-of-band.")` |
| System root chosen (not offered as a value, but reachable if the two roots are configured equal) | `Failure("the system package root is read-only by design; install into the user root or place the package with your deployment tooling")` |
| Dependencies needed but `-deps reject` | `Failure("'X@1.2.0' needs 2 package dependencies; re-run with -deps allow to acquire and verify them, or install them individually")`, listing them. |
| Disk full / permission denied mid-extract | Rollback, quarantine purged, `Failure` naming the destination as `~/…` and the OS error. |
| Upstream failure chunk arrives on the pipe | Forwarded verbatim without attempting an install (`AbstractCommand.Main` does this before `HandlePipedChunk` is reached, ref-command §3.3). |

**Security and audit** — **This is the highest-privilege tool in the product.** No parameter carries a secret (a private-feed token is resolved through `CRED` at request time and never enters the parameter array). The tool is **destructive only in the overwrite case**, which is gated by §D.4. It **cannot grant trust**: everything it installs is `sandboxed`, and that is not overridable by a flag — trust is a separate, interactive, unforceable act (§9.3.8). The audit record carries `packageId`, `resolvedVersion`, `feedName`, `sha256` of the `.nupkg`, `signatureStatus`, `filesPlaced`, `destinationRoot`, `dry`, `forced`, and **never** an expected-vs-actual digest pair (host §D.3 sends those to the security log).

**Traceability** — PRD **7.1**. `origin: ported:Xcaciv.Command.Packages/InstallCommand + NugetWrapper.InstallPackage`. Cupcake's `InstallCommand` echoed its arguments and installed nothing, and `NugetWrapper.InstallPackage` stopped at "download + create directory" with extraction and dependency resolution as `TODO`s. Everything from step 5 onward is **NEW** and is the reason this package exists.

---

#### 9.3.3 `PKG LIST` — enumerate installed packages

**Registration**

| Field | Value |
|---|---|
| Command | `LIST` |
| Root command | `PKG` |
| Description | `List installed tool packages, their versions, trust levels and load state` |
| Prototype | `PKG LIST [<name-filter>] [-state all\|loaded\|unloaded\|quarantined\|blocked] [-trust any\|sandboxed\|trusted] [-root any\|system\|user] [-fields <set>] [-format text\|json\|csv]` |

```csharp
[CommandRegister("List", "List installed tool packages, their versions, trust levels and load state",
    Prototype = "PKG LIST [<name-filter>] [-state …] [-trust …] [-root …] [-fields …] [-format …]", Version = "1.0.0")]
[CommandParameterOrdered("filter", "Case-insensitive substring of the package id", IsRequired = false, DefaultValue = "")]
[CommandParameterNamed("state", "Filter by load state",
    AllowedValues = new[] { "all", "loaded", "unloaded", "quarantined", "blocked" }, DefaultValue = "all")]
[CommandParameterNamed("trust", "Filter by trust level",
    AllowedValues = new[] { "any", "sandboxed", "trusted" }, DefaultValue = "any")]
[CommandParameterNamed("root", "Filter by package root",
    AllowedValues = new[] { "any", "system", "user" }, DefaultValue = "any")]
[CommandParameterNamed("fields", "Fields per row",
    AllowedValues = new[] { "id", "id-version", "summary", "full" }, DefaultValue = "summary")]
[CommandParameterNamed("format", "Output shape",
    AllowedValues = new[] { "text", "json", "csv" }, DefaultValue = "text")]
[CommandHelpRemarks("-fields id emits one bare id per row: the shape PKG VERIFY, PKG SHOW, PKG UPDATE and PKG REMOVE consume from a pipe.")]
public sealed class ListCommand : AbstractCommand { … }
```

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `filter` | ordered | `string` | no | `""` (everything) | any substring; matched case-insensitively against the id | Narrow the listing. |
| `state` | named | `string` | no | `all` | `all` \| `loaded` \| `unloaded` \| `quarantined` \| `blocked` | `loaded` = its commands are in the registry now; `unloaded` = installed but not registered (usually pending a reload). |
| `trust` | named | `string` | no | `any` | `any` \| `sandboxed` \| `trusted` | |
| `root` | named | `string` | no | `any` | `any` \| `system` \| `user` | |
| `fields` | named | `string` | no | `summary` | `id` \| `id-version` \| `summary` \| `full` | `id` → one bare id per row (the pipe shape). `summary` → id, version, trust, state, command count. `full` → adds root, TFM, RIDs, signature status, digest prefix (12 chars), installed-at, source feed. |
| `format` | named | `string` | no | `text`, or `CHATDBG_OUTPUT_FORMAT` when it is set and `-format` is absent | `text` \| `json` \| `csv` | |

**Pipeline behaviour** — **Source.** Refuses piped input with an explanation naming `PKG SHOW`. Emits **one chunk per package** (overriding `Main`, as in `SEARCH`), so the row count is the package count and a downstream stage sees them one at a time. Declares `OutputFormat = ResultFormat.JSON` or `CSV` when `-format` asks for it, `General` otherwise — this is a record set with a stable field order, deterministic and invariant-culture (host §D.1 criteria 1–4). Field order for `csv`/`json` is fixed and documented: `id, version, trust, state, root, commands, tfm, rids, signature, sha256Prefix, installedAt, feed`.

**Environment interaction** — Reads both package roots, `CHATDBG_ALLOW_USER_PACKAGES`, `CHATDBG_TRUST_STORE`, `CHATDBG_OUTPUT_FORMAT`. Writes nothing. Not `modifiesEnvironment`.

**Failure modes** — A missing user root is **not** an error: it lists the system root and notes `user packages: disabled` / `not present` on the status channel. An unreadable package directory is skipped with one warning row per skipped directory (never a hard failure — the crawler itself skips silently, and this tool exists partly to make those silences visible). A corrupt trust store is a **failure**, not a degradation: `Failure("trust store at ~/… is unreadable (line 14); refusing to report trust levels")`, because reporting "sandboxed" for a package whose record could not be read would be a lie. Zero matches → one successful chunk `no packages match`. Piped input → the explanatory failure.

**Security and audit** — No secrets. Not destructive. `full` shows only the **first 12 characters** of a digest, never a full hash and never an expected-vs-actual pair. Package **paths** are rendered `~/…`-relative (host §D.2).

**Traceability** — PRD **7.1**. **NEW.** Reason: Cupcake's host loads from a directory and offers no way to see what is in it; the crawler *silently skips* every package it cannot load, so without an inventory tool the difference between "not installed" and "installed but rejected" is invisible. `PKG LIST` is also the pipeline source that makes every other tool in this package composable.

---

#### 9.3.4 `PKG SHOW` — everything known about one package

**Registration**

| Field | Value |
|---|---|
| Command | `SHOW` |
| Root command | `PKG` |
| Description | `Show the manifest, contents, commands and trust record of one package` |
| Prototype | `PKG SHOW "<id[@version]>" [-section all\|manifest\|commands\|files\|trust\|deps] [-format text\|json]` |

```csharp
[CommandRegister("Show", "Show the manifest, contents, commands and trust record of one package",
    Prototype = "PKG SHOW \"<id[@version]>\" [-section …] [-format text|json]", Version = "1.0.0")]
[CommandParameterOrdered("package", "Package identity as id or id@version — quote it", IsRequired = false, UsePipe = true)]
[CommandParameterNamed("section", "Which part to render",
    AllowedValues = new[] { "all", "manifest", "commands", "files", "trust", "deps" }, DefaultValue = "all")]
[CommandParameterNamed("format", "Output shape", AllowedValues = new[] { "text", "json" }, DefaultValue = "text")]
[CommandHelpRemarks("Omit the version to show the highest installed version.")]
[CommandHelpRemarks("'commands' lists what the package would register, read from assembly metadata without loading it.")]
public sealed class ShowCommand : AbstractCommand { … }
```

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `package` | ordered, `UsePipe = true` | `string` | no (C4) | *(none)* | `id[@version]`, quoted | Which package. From the pipe when piped. |
| `section` | named | `string` | no | `all` | `all` \| `manifest` \| `commands` \| `files` \| `trust` \| `deps` | `manifest` — id, version, authors, licence, description, declared `modifiesEnvironment` names, declared contract version. `commands` — every `[CommandRegister]` type, its root, prototype and parameters, read from **metadata only**. `files` — every shipped file with size and digest prefix. `trust` — level, who granted it, when, against which digest, signature status. `deps` — package and assembly dependencies, and any `deps.json` probe path that points outside the package directory. |
| `format` | named | `string` | no | `text` | `text` \| `json` | |

**Pipeline behaviour** — **Filter.** One piped chunk is one `id[@version]`; one detail block is emitted per chunk. Without a pipe it emits one chunk. `OutputFormat = JSON` when `-format json`, else `General`.

**Environment interaction** — Reads the roots, the trust store path, `CHATDBG_OUTPUT_FORMAT`. Writes nothing. Not `modifiesEnvironment`.

**Failure modes** — Unknown id → `Failure("'X' is not installed. 'PKG LIST' shows what is, 'PKG SEARCH X' looks for it.")`. Ambiguous id with several installed versions and no `@version` → shows the highest and names the others on the status channel. A package whose assemblies cannot be read as metadata → the other sections still render and `commands` reports `unreadable: <reason>` — this is the case `PKG DOCTOR` exists to explain, and the message says so. Upstream failure chunks pass through untouched.

**Security and audit** — No secrets. Not destructive. `deps` deliberately surfaces probe paths that escape the package directory, because dependency loads are verified against the forbidden-directory list but **not** confined to `basePathRestriction` (ref-loader finding B10) — an escaping probe path is a finding an operator must see before granting trust.

**Traceability** — PRD **7.1**. **NEW.** Reason: `PKG TRUST` asks a human to make a security decision; a human cannot make it without seeing what the package contains, what it will register, what it asked for, and where its dependencies come from. `SHOW` is the evidence for that decision.

---

#### 9.3.5 `PKG VERIFY` — re-check installed packages against the allowlist

**Registration**

| Field | Value |
|---|---|
| Command | `VERIFY` |
| Root command | `PKG` |
| Description | `Re-hash installed packages and check them against the trust store` |
| Prototype | `PKG VERIFY ["<id[@version]>"] [-scope one\|installed\|loaded\|all] [-signature on\|off] [-repair] [-quiet]` |

```csharp
[CommandRegister("Verify", "Re-hash installed packages and check them against the trust store",
    Prototype = "PKG VERIFY [\"<id[@version]>\"] [-scope …] [-signature on|off] [-repair] [-quiet]", Version = "1.0.0")]
[CommandParameterOrdered("package", "Package to verify — quote it", IsRequired = false, DefaultValue = "", UsePipe = true)]
[CommandParameterNamed("scope", "What to verify when no package is named",
    AllowedValues = new[] { "one", "installed", "loaded", "all" }, DefaultValue = "installed")]
[CommandParameterNamed("signature", "Also re-check the publisher signature",
    AllowedValues = new[] { "on", "off" }, DefaultValue = "on")]
[CommandFlag("repair", "Re-record digests for a SANDBOXED package whose files changed legitimately")]
[CommandFlag("quiet", "Emit only failures")]
[CommandHelpRemarks("-repair never applies to a TRUSTED package: a trusted package whose bytes changed must be re-trusted by a human.")]
public sealed class VerifyCommand : AbstractCommand { … }
```

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `package` | ordered, `UsePipe = true` | `string` | no | `""` | `id[@version]` | Verify one package. |
| `scope` | named | `string` | no | `installed` | `one` \| `installed` \| `loaded` \| `all` | `installed` — both roots' placed packages. `loaded` — only those whose commands are registered. `all` — adds quarantine. |
| `signature` | named | `string`→bool | no | `on` | `on` \| `off` | Re-checking a signature costs a certificate-chain build; `off` for a fast digest-only sweep. |
| `repair` | flag | `bool` | n/a | `false` | presence = true | Re-record digests, **sandboxed packages only**. |
| `quiet` | flag | `bool` | n/a | `false` | presence = true | Suppress `ok` rows. |

**Pipeline behaviour** — **Filter.** One piped chunk is one `id[@version]`, so `PKG LIST -fields id | PKG VERIFY` is the canonical sweep; the tool emits **one verdict chunk per package** (`ok` / `changed` / `missing` / `unknown` / `signature-failed` / `blocked`), and a `changed` verdict is emitted as a **failure chunk** so `-quiet` piped into a filter stage yields exactly the packages that need attention. Without a pipe it verifies `-scope` and emits one chunk per package plus a trailing summary. `OutputFormat = General`, or `CSV`/`JSON` under `CHATDBG_OUTPUT_FORMAT`.

**Environment interaction** — Reads the roots, `CHATDBG_TRUST_STORE`, `CHATDBG_PKG_SIGNATURE_POLICY`. Writes `CHATDBG_PKG_LAST_VERIFY` — **into its own bucket** (`VERIFY_LAST`) and, because it is *not* registered `modifiesEnvironment`, the global `CHATDBG_PKG_LAST_VERIFY` is published by the host's post-dispatch mirror rather than by the tool. (This is the deliberate asymmetry of §9.1: a read-only tool never acquires global write rights just to publish a timestamp.)

**Failure modes**

| Situation | What the user sees |
|---|---|
| Digest mismatch | Failure chunk `'X@1.2.0': 2 files changed since install (a.dll, b.dll) — the package will be REFUSED at next load`. The **expected/actual pair goes to the security log, never to the user** (host §D.3). Audited `HashMismatch`. |
| File missing | Failure chunk naming the file; the package is marked incomplete. |
| File present but not in the allowlist | Failure chunk `'X@1.2.0' ships a file that was not recorded at install (c.dll)` — this is the case the loader reports as "no trusted hash found" at load time, in strict mode. |
| Signature no longer valid (expired, revoked, chain broken) | Failure chunk naming the reason; under `CHATDBG_PKG_SIGNATURE_POLICY=require` the package is additionally marked `blocked` and will not load. Expiry alone, on a signature that was valid at install time and carries a trusted timestamp, is reported as a **warning**, not a block. |
| `-repair` on a trusted package | `Failure("'X' is TRUSTED; -repair is refused. Run 'PKG UNTRUST \"X\"' then re-trust it after reviewing the change.")` |
| Trust store unreadable | `Failure` naming the file and the line, exit-worthy at startup but only a failure here. |
| Package root missing entirely | Not a failure: `no installed packages to verify`. |

**Security and audit** — No secrets. `-repair` is a **trust-adjacent** write and is audited as `TrustGrant`-class event `DigestRerecord` with the package id, the file list and the old/new digest **prefixes**. It is not classed destructive (no data is lost) but it is refused on trusted packages, which is the whole point.

**Traceability** — PRD **7.1**. **NEW.** Reason: `AssemblyIntegrityVerifier` only runs at load, and a mismatch there is a `SecurityException` that arrives as a startup failure with no explanation of *which* file changed. There is also a real TOCTOU window between hashing and loading (ref-loader §4.3), and the mitigation — a read-only package root — is an operational property that drifts. `VERIFY` makes drift visible before it becomes a failed start.

---

#### 9.3.6 `PKG UPDATE` — find and apply newer versions

**Registration**

| Field | Value |
|---|---|
| Command | `UPDATE` |
| Root command | `PKG` |
| Description | `Check for and install newer versions of installed packages` |
| Prototype | `PKG UPDATE ["<id>"] [-check] [-to <version>] [-source <name>] [-allow major\|minor\|patch] [-prerelease] [-keep <n>] [-prune] [-force]` |

```csharp
[CommandRegister("Update", "Check for and install newer versions of installed packages",
    Prototype = "PKG UPDATE [\"<id>\"] [-check] [-to <version>] [-allow major|minor|patch] [-prerelease] [-keep <n>] [-prune] [-force]",
    Version = "1.0.0")]
[CommandParameterOrdered("package", "Package id — quote it. Omit to consider every installed package.",
    IsRequired = false, DefaultValue = "", UsePipe = true)]
[CommandParameterNamed("to", "Exact target version", DefaultValue = "")]
[CommandParameterNamed("source", "Feed name", DefaultValue = "")]
[CommandParameterNamed("allow", "Largest version step permitted",
    AllowedValues = new[] { "patch", "minor", "major" }, DefaultValue = "minor")]
[CommandParameterNamed("keep", "Previous versions to retain for rollback",
    DataType = typeof(int), DefaultValue = "1")]
[CommandFlag("check", "Report available updates without installing anything")]
[CommandFlag("prerelease", "Consider prerelease versions", ShortAlias = "pre")]
[CommandFlag("prune", "Delete retained older versions beyond -keep")]
[CommandFlag("force", "Skip confirmation prompts")]
[CommandHelpRemarks("PKG UPDATE -check emits one bare id per updatable package: pipe it straight back into PKG UPDATE.")]
[CommandHelpRemarks("An update never inherits trust. A trusted package updated to a new version returns to SANDBOXED until re-trusted.")]
public sealed class UpdateCommand : AbstractCommand { … }
```

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `package` | ordered, `UsePipe = true` | `string` | no | `""` = every installed package | quoted id | What to update. |
| `to` | named | `string` | no | `""` = highest permitted by `-allow` | a SemVer string; may be **lower** than the installed version (a deliberate rollback) | Exact target. |
| `source` | named | `string` | no | the feed the package came from, else `CHATDBG_PKG_SOURCE` | registered feed name | **A package is updated from the feed it came from by default** — silently switching feeds is a supply-chain substitution. |
| `allow` | named | `string` | no | `minor` | `patch` \| `minor` \| `major` | Largest permitted step. `major` requires confirmation even with `-force` absent. |
| `keep` | named | `int` | no | `1` | `0`–`10` | Previous version directories retained for rollback. `0` deletes the old version immediately (and is therefore destructive). |
| `check` | flag | `bool` | n/a | `false` | presence = true | Dry run; emits one bare id per updatable package. |
| `prerelease` | flag | `bool` | n/a | `CHATDBG_PKG_PRERELEASE` | presence = true | |
| `prune` | flag | `bool` | n/a | `false` | presence = true | Delete retained versions beyond `-keep`. Destructive. |
| `force` | flag | `bool` | n/a | `false` | presence = true | Skips confirmations for overwrite and for a `major` step. Never skips verification or trust. |

**Pipeline behaviour** — **Filter.** One piped chunk is one id (the shape `-check` and `PKG LIST -fields id` both emit), one result chunk per package. `-check` emits **only ids** so the output is directly re-consumable: `PKG UPDATE -check | PKG UPDATE -force` is the intended "apply everything" idiom. `OutputFormat = General`.

**Environment interaction** — Reads everything `INSTALL` reads. Writes `CHATDBG_PKG_COUNT`, `CHATDBG_PKG_RELOAD_PENDING`; registered `modifiesEnvironment: true`.

**Failure modes** — Package not installed → `Failure` naming `PKG INSTALL`. No newer version → successful `'X' is up to date (1.2.0)`. A newer version exists but exceeds `-allow` → successful chunk `'X' 1.2.0 → 2.0.0 available; exceeds -allow minor` (an *available but withheld* update is information, not an error). Signature/verification failure on the new version → the **old version stays in place and stays loadable**, quarantine is purged, `Failure` explains; this fail-safe rollback is the reason the sequence installs beside rather than over. Feed unreachable → per-package failure, other packages continue. `-to` names a nonexistent version → failure listing the available ones. Downstream/upstream pipe failures propagate per the framework.

**Security and audit** — **The trust reset is the security-critical behaviour**: a new version is new bytes from a remote party, so an updated package returns to `sandboxed` and its environment-modifying grant is revoked until a human re-trusts it. `-force` cannot alter that. `-prune` and `-keep 0` are destructive and follow §D.4 (typed confirmation, refused non-interactively without `-force`). Audit: `packageId`, `fromVersion`, `toVersion`, `feedName`, `signatureStatus`, `trustReset: true`, `pruned` count.

**Traceability** — PRD **7.1**. **NEW.** Reason: Cupcake has no update path at all, and a tool-package host without one accumulates unpatched third-party code in-process — the `Vulnerabilities:{count}` field its own search command surfaces has no remedy without this tool.

---

#### 9.3.7 `PKG REMOVE` — uninstall a package

**Registration**

| Field | Value |
|---|---|
| Command | `REMOVE` |
| Root command | `PKG` |
| Description | `Uninstall a tool package and purge its trust and digest records` |
| Prototype | `PKG REMOVE "<id[@version]>" [-versions this\|old\|all] [-keep-trust] [-purge-quarantine] [-force]` |

```csharp
[CommandRegister("Remove", "Uninstall a tool package and purge its trust and digest records",
    Prototype = "PKG REMOVE \"<id[@version]>\" [-versions this|old|all] [-keep-trust] [-purge-quarantine] [-force]",
    Version = "1.0.0")]
[CommandParameterOrdered("package", "Package identity — quote it", IsRequired = false, UsePipe = true)]
[CommandParameterNamed("versions", "Which installed versions to remove",
    AllowedValues = new[] { "this", "old", "all" }, DefaultValue = "this")]
[CommandFlag("keep-trust", "Leave the trust record in place for a later reinstall of the same digest")]
[CommandFlag("purge-quarantine", "Also delete any staged copy of this package")]
[CommandFlag("force", "Skip the typed confirmation")]
[CommandHelpRemarks("A package whose commands are loaded in this process is removed from disk and deregistered at the next reload or start; assembly unload is not guaranteed.")]
public sealed class RemoveCommand : AbstractCommand { … }
```

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `package` | ordered, `UsePipe = true` | `string` | no (C4) | *(none)* | `id[@version]`, quoted | What to remove. |
| `versions` | named | `string` | no | `this` | `this` \| `old` \| `all` | `this` — the named version, or the highest if unversioned. `old` — every version except the highest. `all` — every version. |
| `keep-trust` | flag | `bool` | n/a | `false` | presence = true | Retain the trust record, so reinstalling the *same digest* returns to `trusted` without a new human decision. Off by default: removal is normally a revocation too. |
| `purge-quarantine` | flag | `bool` | n/a | `false` | presence = true | Also delete staged copies. |
| `force` | flag | `bool` | n/a | `false` | presence = true | Skip the typed confirmation (§D.4). |

**Pipeline behaviour** — **Filter.** One chunk is one `id[@version]`; one result per chunk. **Piped removal without `-force` is refused** (§D.4 rule 4): a typed confirmation cannot be issued into a pipeline, and `PromptForCommand` is contractually meaningless when `HasPipedInput` is true.

**Environment interaction** — Reads the roots, the trust store. Writes `CHATDBG_PKG_COUNT`, `CHATDBG_PKG_RELOAD_PENDING`; `modifiesEnvironment: true`.

**Failure modes**

| Situation | What the user sees |
|---|---|
| Not installed | `Failure("'X' is not installed")` |
| Installed in the **system** root | `Failure("'X' lives in the read-only system package root and cannot be removed by this tool")`, naming the root as `~/…`. |
| Its commands are currently loaded | Files are deleted, records purged, and the confirmation reads `removed 'X@1.2.0'; its 4 commands stay registered until reload or restart`. **Never claims an unload that did not happen** — `AssemblyContext.Unload()` is terminal, can fail while any reference survives, and there is no reload-in-place (ref-loader §2.4, findings B22–B24). |
| Files locked by the OS (Windows, assembly loaded) | Deletion is deferred: the version directory is renamed to `{version}.pending-remove`, excluded from every scan, and swept at the next start. The user is told, in one sentence, that the removal completes at restart. |
| Partial delete | The package is marked `blocked` so it cannot load in a half-removed state, and the failure names what remains. |
| Confirmation declined | Silent success with an empty chunk — no change, no error (§D.4 rule 3). |

**Security and audit** — **Destructive and irreversible** (the package must be re-downloaded). Confirmation required, `-force` permitted. Purging trust records is itself audited (`TrustRevoked`). Audit: `packageId`, `versionsRemoved`, `filesDeleted`, `trustPurged`, `deferred`, `forced`.

**Traceability** — PRD **7.1**. **NEW.** Reason: an install path without a remove path is a one-way door, and "delete the folder yourself" leaves the digest allowlist and the trust record behind — stale allowlist entries are exactly how a future package with the same path silently inherits trust.

---

#### 9.3.8 `PKG TRUST` — grant a package environment-modifying rights

**Registration**

| Field | Value |
|---|---|
| Command | `TRUST` |
| Root command | `PKG` |
| Description | `Grant a verified package the rights its manifest declares` |
| Prototype | `PKG TRUST "<id[@version]>" [-grants declared\|none] [-note <words...>]` |

```csharp
[CommandRegister("Trust", "Grant a verified package the rights its manifest declares",
    Prototype = "PKG TRUST \"<id[@version]>\" [-grants declared|none] [-note <words...>]", Version = "1.0.0")]
[CommandParameterOrdered("package", "Package identity — quote it", IsRequired = false)]
[CommandParameterNamed("grants", "Which declared rights to grant",
    AllowedValues = new[] { "declared", "none" }, DefaultValue = "declared")]
[CommandParameterSuffix("note", "Free-text reason recorded with the grant", IsRequired = false, DefaultValue = "")]
[CommandHelpRemarks("There is no -force. Trust is granted interactively, by a human, or not at all.")]
[CommandHelpRemarks("You will be asked to type the package id and the first 8 characters of its SHA-256 digest.")]
[CommandHelpRemarks("Run 'PKG SHOW \"<id>\"' first: the grant covers every command the manifest declares.")]
public sealed class TrustCommand : AbstractCommand { … }
```

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `package` | ordered | `string` | no (C4) | *(none)* | `id[@version]`, quoted | Which package. **No `UsePipe`** — deliberately: a trust decision must not be reachable from a pipeline. |
| `grants` | named | `string` | no | `declared` | `declared` \| `none` | `declared` grants global environment writes for exactly the command names in the manifest's `modifiesEnvironment` list. `none` promotes the package to `trusted` for *loading* purposes (its digests become an explicit allowlist entry) without any environment rights. |
| `note` | suffix | `string` | no | `""` | free text (tokenizer-limited) | Recorded verbatim in the trust store and the audit record: *why* this was trusted. |

**Pipeline behaviour** — **Not pipeable, in either direction.** `HandlePipedChunk` returns `Failure("PKG TRUST cannot run in a pipeline: granting trust requires an interactive confirmation.")`. It emits one chunk. `OutputFormat = General`.

**The grant sequence** — every step is mandatory and none is skippable:

1. The package must be **installed** and **verified now** (a fresh digest computation, not a cached verdict). A mismatch aborts.
2. The signature must satisfy `CHATDBG_PKG_SIGNATURE_POLICY`. Under `require`, an unsigned package **cannot** be trusted.
3. The front end must be interactive. On `CHATDBG_FRONTEND=batch`, or with redirected input, the tool **fails**: `Failure("trust cannot be granted non-interactively")`.
4. A summary is printed: id, version, root, signature subject, digest prefix, the command names to be registered, and the exact global environment keys the manifest requests.
5. `PromptForCommand` asks the operator to type `<id> <first 8 hex of SHA-256>`. Anything else → silent no-change (an empty successful chunk).
6. The trust record is written: id, version, per-file digests (absolute paths), signature subject and thumbprint, granted command names, the note, the operator identity the host knows, and an ISO-8601 UTC timestamp.
7. `CHATDBG_PKG_RELOAD_PENDING=true`, because the grant takes effect when the host re-registers the affected command types with `modifiesEnvironment: true` (host §C.6 step 3).

**Environment interaction** — Reads the roots, `CHATDBG_TRUST_STORE`, `CHATDBG_PKG_SIGNATURE_POLICY`, `CHATDBG_FRONTEND`. Writes `CHATDBG_PKG_RELOAD_PENDING`; `modifiesEnvironment: true`.

**Failure modes** — Not installed / not verified / signature short of policy / non-interactive / typed answer mismatched / manifest declares a right the host reserves (a `CHATDBG_`-prefixed key, or a command name the package does not actually contain) — each is a distinct, named failure, and **every one of them leaves the package exactly as it was**. A manifest that declares `modifiesEnvironment` for a command it does not ship is treated as a **hostile manifest**: the grant is refused entirely, not partially, and the event is audited as `PackageRejected`.

**Security and audit** — **The most security-sensitive tool in the product.** Per host §D.4 rule 5, `-force` does not exist here, and the tool always fails on a non-interactive front end. The audit record (`TrustGrant`) carries id, version, digest **prefix**, signature thumbprint, granted command names, the note and the timestamp; the full digest goes to the security log. No secret is ever a parameter or an output. The tool never *reads* a package's code and never loads it — trust is granted on identity and integrity, not on inspection results this tool produced.

**Traceability** — PRD **7.1**. **NEW.** Reason: ref-loader §4.1 makes the production posture explicit — `learningMode: false`, allowlist-only — which means *something* has to put entries in the allowlist, deliberately, with a human in the loop. Cupcake's trust-on-first-use default (`learningMode` defaults to `true`) is precisely the failure this tool prevents.

---

#### 9.3.9 `PKG UNTRUST` — revoke a grant, or block a package outright

**Registration**

| Field | Value |
|---|---|
| Command | `UNTRUST` |
| Root command | `PKG` |
| Description | `Revoke a package's trust grant, or block it from loading` |
| Prototype | `PKG UNTRUST "<id[@version]>" [-block] [-scope grants\|all] [-note <words...>] [-force]` |

```csharp
[CommandRegister("Untrust", "Revoke a package's trust grant, or block it from loading",
    Prototype = "PKG UNTRUST \"<id[@version]>\" [-block] [-scope grants|all] [-note <words...>] [-force]", Version = "1.0.0")]
[CommandParameterOrdered("package", "Package identity — quote it", IsRequired = false, UsePipe = true)]
[CommandParameterNamed("scope", "How far to revoke",
    AllowedValues = new[] { "grants", "all" }, DefaultValue = "grants")]
[CommandFlag("block", "Also refuse to load this package until it is explicitly re-trusted")]
[CommandFlag("force", "Skip the typed confirmation")]
[CommandParameterSuffix("note", "Reason recorded with the revocation", IsRequired = false, DefaultValue = "")]
[CommandHelpRemarks("Revoking is always allowed, always cheap, and always safe: this is the tool you reach for when unsure.")]
public sealed class UntrustCommand : AbstractCommand { … }
```

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `package` | ordered, `UsePipe = true` | `string` | no | *(none)* | `id[@version]`, quoted | Which package. Piping **is** permitted here — mass revocation is a safe direction. |
| `scope` | named | `string` | no | `grants` | `grants` \| `all` | `grants` — drop environment-modifying rights, keep the digest allowlist so it still loads as `sandboxed`. `all` — also remove the allowlist entries, so it will not load at all until reinstalled or re-trusted. |
| `block` | flag | `bool` | n/a | `false` | presence = true | Write an explicit deny that survives reinstallation of the same version. |
| `force` | flag | `bool` | n/a | `false` | presence = true | Skip the typed confirmation. Permitted (unlike `TRUST`) because revocation reduces privilege. |
| `note` | suffix | `string` | no | `""` | free text | Recorded with the revocation. |

**Pipeline behaviour** — **Filter.** One chunk is one id; one result per chunk. Piped without `-force` is refused per §D.4; piped *with* `-force` is the supported mass-revocation form.

**Environment interaction** — Writes `CHATDBG_PKG_RELOAD_PENDING`; `modifiesEnvironment: true`.

**Failure modes** — Not installed and not blocked → `Failure`. Already untrusted → successful no-op chunk saying so. `-block` on a package currently loaded → the block is recorded, the package keeps running until reload/restart, and the message says exactly that. Trust store unwritable → `Failure`; **the tool never reports a revocation it did not persist**.

**Security and audit** — Destructive in the trust sense (`TrustRevoked` / `PackageBlocked` audit records) but never deletes files. It is the deliberate counterweight to `TRUST`: granting is hard, revoking is easy.

**Traceability** — PRD **7.1**. **NEW**, named in host §D.4's destructive-operation set.

---

#### 9.3.10 `PKG SOURCE` — manage the feeds this shell will talk to

**Registration**

| Field | Value |
|---|---|
| Command | `SOURCE` |
| Root command | `PKG` |
| Description | `List, add, remove, select and test package feeds` |
| Prototype | `PKG SOURCE <list\|add\|remove\|use\|test\|map> [<name>] [-cred <slot>] [-map <pattern>] [-prerelease on\|off] [-network on\|off] [-timeout <s>] [-force]` |

```csharp
[CommandRegister("Source", "List, add, remove, select and test package feeds",
    Prototype = "PKG SOURCE <list|add|remove|use|test|map> [<name>] [-cred <slot>] [-map <pattern>] …", Version = "1.0.0")]
[CommandParameterOrdered("action", "What to do",
    IsRequired = false, DefaultValue = "list",
    AllowedValues = new[] { "list", "add", "remove", "use", "test", "map" })]
[CommandParameterOrdered("name", "Feed name", IsRequired = false, DefaultValue = "")]
[CommandParameterNamed("cred", "Credential slot name held by CRED for a private feed", DefaultValue = "")]
[CommandParameterNamed("map", "Package id pattern this feed serves, e.g. ChatDbg.*", DefaultValue = "*")]
[CommandParameterNamed("prerelease", "Default prerelease policy",
    AllowedValues = new[] { "on", "off" }, DefaultValue = "off")]
[CommandParameterNamed("network", "Global network switch",
    AllowedValues = new[] { "on", "off" }, DefaultValue = "on")]
[CommandParameterNamed("timeout", "Feed request timeout in seconds", DataType = typeof(int), DefaultValue = "60")]
[CommandFlag("force", "Skip confirmation when removing a feed that packages were installed from")]
[CommandHelpRemarks("A feed URL cannot be typed as an argument: the tokenizer removes ':' and '/'. Pipe it in, or answer the prompt: PKG SOURCE ADD corp  then paste the URL when asked.")]
[CommandHelpRemarks("Every declared feed must have a -map entry. An unmapped feed is never consulted — the same dead-configuration trap as Cupcake's unmapped 'github' source.")]
public sealed class SourceCommand : AbstractCommand { … }
```

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `action` | ordered | `string` | no | `list` | `list` \| `add` \| `remove` \| `use` \| `test` \| `map` | The verb. |
| `name` | ordered | `string` | no | `""` | `[A-Za-z0-9._-]{1,64}` | Feed name. Required in practice for every action but `list`; its absence yields a hand-written failure (C4). |
| `cred` | named | `string` | no | `""` | a slot name known to `CRED` | **Names** the credential; the value is fetched from `CRED` per request and never stored here. |
| `map` | named | `string` | no | `*` | a glob over package ids | Package-source mapping (Cupcake §8 rule 35). `ChatDbg.*` and `Xcaciv.*` route to a first-party feed; `*` to the default. |
| `prerelease` | named | `string`→bool | no | `off` | `on` \| `off` | Sets `CHATDBG_PKG_PRERELEASE`. |
| `network` | named | `string`→bool | no | `on` | `on` \| `off` | Sets `CHATDBG_PKG_NETWORK`. `off` makes the whole package offline. |
| `timeout` | named | `int` | no | `60` | `5`–`600` seconds | Sets `CHATDBG_PKG_TIMEOUT`. Out-of-range values are clamped and the clamp is reported. |
| `force` | flag | `bool` | n/a | `false` | presence = true | Confirmation skip for `remove`. |

**Pipeline behaviour** — **Filter, asymmetrically.** `list` is a source (one chunk per feed: name, host, mapping, credential slot name, HTTPS status, active marker). `add` is the one tool in this package that **consumes a piped chunk as a value rather than an identity**: one chunk is the feed **URL**, which is the only way a URL can reach a command intact (C1). Interactively, `add` without a pipe prompts for the URL via `PromptForCommand`, which is likewise untokenized. Every other action refuses piped input with a message naming these two channels.

**Environment interaction** — Writes globals `CHATDBG_PKG_SOURCE`, `CHATDBG_PKG_PRERELEASE`, `CHATDBG_PKG_NETWORK`, `CHATDBG_PKG_TIMEOUT`, `CHATDBG_PKG_SIGNATURE_POLICY`; `modifiesEnvironment: true`. Persists the feed registry to `<DATA_ROOT>/pkg-sources.json` (`0600` where the OS supports it). Reads the OS variable `NUGET_LOCAL_PACKAGES` and, when present, offers it as a pre-registered feed named `local`, mapped to `ChatDbg.*` and `Xcaciv.*` only (Cupcake §8 rule 36).

**Failure modes**

| Situation | What the user sees |
|---|---|
| `add` with a non-HTTPS URL | `Failure("insecure package source URL. HTTPS is required.")` — the literal policy, enforced in the acquiring layer where it belongs (Cupcake §8 rule 45), and never softened by a flag. `file://` is refused too, except for the `local` feed derived from `NUGET_LOCAL_PACKAGES`, which is refused if it points outside the user's own directories. |
| `add` with a malformed URL, or a URL that arrived mangled | `Failure("that does not parse as an absolute URL — pipe it in or paste it at the prompt; typing it as an argument strips ':' and '/'.")` |
| `add` for an existing name | Refused unless `-force`; never silently replaces a feed. |
| `remove` of a feed packages were installed from | Typed confirmation; those packages remain installed but their `update` source is lost and the message says so. |
| `use` of an unknown name | `Failure` listing the known names. |
| `test` | Performs one authenticated service-index request and reports `ok`, `unauthorized`, `unreachable`, `not-https`, or `timeout`, with the elapsed milliseconds. Never prints a response body. |
| A feed with no `-map` entry | Registered but reported as `INACTIVE (no mapping)` in `list`, and `add` warns at the moment of creation. |

**Security and audit** — `-cred` carries a **slot name**, never a secret; a slot name is not masked (it is not sensitive) but the resolved value never enters this tool. Changing the active feed, adding a feed, and turning the signature policy down are all audited (`SourceChanged`, and `PolicyWeakened` when `require` → `prefer` → `off`). Lowering `CHATDBG_PKG_SIGNATURE_POLICY` is treated as destructive-adjacent: it requires a typed confirmation naming the new level, and it is refused non-interactively.

**Traceability** — PRD **7.1**. **NEW.** Reason: Cupcake reads its source from a single environment value with a hardcoded fallback and has no way to see, test or constrain it; its `NuGet.config` even ships a declared-but-unmapped source that is silently never consulted. Source-selection policy must live in exactly one place (Cupcake §8 rule 44), and this is that place.

---

#### 9.3.11 `PKG RELOAD` — re-scan the package roots without restarting

**Registration**

| Field | Value |
|---|---|
| Command | `RELOAD` |
| Root command | `PKG` |
| Description | `Re-scan the package roots and report what changed` |
| Prototype | `PKG RELOAD [-scan verify\|fast] [-report diff\|full] [-dry]` |

```csharp
[CommandRegister("Reload", "Re-scan the package roots and report what changed",
    Prototype = "PKG RELOAD [-scan verify|fast] [-report diff|full] [-dry]", Version = "1.0.0")]
[CommandParameterNamed("scan", "Whether to re-verify digests before registering",
    AllowedValues = new[] { "verify", "fast" }, DefaultValue = "verify")]
[CommandParameterNamed("report", "How much to report",
    AllowedValues = new[] { "diff", "full" }, DefaultValue = "diff")]
[CommandFlag("dry", "Report what would change without asking the host to register anything")]
[CommandHelpRemarks("Newly installed packages register immediately. Removed or replaced packages keep running until the process restarts: assembly unload is not guaranteed.")]
public sealed class ReloadCommand : AbstractCommand { … }
```

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `scan` | named | `string` | no | `verify` | `verify` \| `fast` | `verify` re-hashes every candidate before registering (the production posture). `fast` skips re-hashing and relies on the load-time verifier alone — offered because a large root makes re-hashing measurable, and refused entirely when the signature policy is `require`. |
| `report` | named | `string` | no | `diff` | `diff` \| `full` | `diff` lists appeared/disappeared/failed packages and commands; `full` lists the whole registry. |
| `dry` | flag | `bool` | n/a | `false` | presence = true | Report only. |

**Pipeline behaviour** — **Source.** Refuses piped input. Emits one chunk per changed package plus a summary chunk. `OutputFormat = General`.

**What it actually does, and what it honestly cannot** — It calls `ILoaderControl.RescanAsync(...)`, which the host implements as: verify → `AddPackageDirectory(root)` for each accepted root → `LoadCommands()` → re-register manifest-declared environment-modifying commands for `trusted` packages. **New packages become live immediately.** Removed, downgraded or replaced packages do **not** disappear from the running process: `AssemblyContext.Unload()` is terminal, returns four indistinguishable falses, and fails while any reference to a plugin type, instance or delegate survives — and the framework's `CommandExecutor` does not dispose the command instances it creates. The tool therefore reports, in plain words, `4 commands added; 2 commands from removed packages remain registered until restart`, and sets `CHATDBG_PKG_RELOAD_PENDING` to `false` only when the loaded set matches the on-disk set exactly. **It never claims a hot-unload it did not achieve.**

**Environment interaction** — Writes `CHATDBG_PKG_LOADED`, `CHATDBG_PKG_COUNT`, `CHATDBG_PKG_RELOAD_PENDING`; `modifiesEnvironment: true`.

**Failure modes** — `NoPluginsFoundException` from the loader is caught and rendered as the Cupcake-shaped survivable message `No tool packages found. Try 'pkg --help'.` — never fatal (Cupcake §8 rule 38). A package that fails verification is skipped, named, and pointed at `PKG DOCTOR`. A `SecurityException` **and** an `ArgumentOutOfRangeException` from the loading layer are both caught (confinement failure is the latter, ref-loader finding B6) and reported as security denials, not as argument bugs. A package that throws while its types are enumerated is skipped by the crawler; this tool reports the skip, which is the whole reason it prints a diff. A concurrent reload is refused: `Failure("a reload is already in progress")`.

**Security and audit** — Not destructive. Audited as `PackagesReloaded` with the added/removed/failed counts and the elapsed time. `-scan fast` is audited explicitly, because it is a deliberate reduction in assurance.

**Traceability** — PRD **7.1**. **NEW.** Reason: this is the exact link Cupcake left unbuilt — its `RunAsync` parks the `TODO`s *"figure out how to handle non existing controller: download, compile"* and *"support NuGet style directory structure"*, and its `install --help` advice is unreachable because installing does nothing and nothing re-scans. `PKG RELOAD` closes *search → install → reload → first-class command*, and is honest about the half of it the runtime cannot deliver.

---

#### 9.3.12 `PKG DOCTOR` — explain why a package did not load

**Registration**

| Field | Value |
|---|---|
| Command | `DOCTOR` |
| Root command | `PKG` |
| Description | `Diagnose why a tool package is not loading` |
| Prototype | `PKG DOCTOR ["<id>"] [-scope one\|all\|roots] [-depth quick\|full] [-format text\|json]` |

```csharp
[CommandRegister("Doctor", "Diagnose why a tool package is not loading",
    Prototype = "PKG DOCTOR [\"<id>\"] [-scope one|all|roots] [-depth quick|full] [-format text|json]", Version = "1.0.0")]
[CommandParameterOrdered("package", "Package id — quote it", IsRequired = false, DefaultValue = "", UsePipe = true)]
[CommandParameterNamed("scope", "What to diagnose",
    AllowedValues = new[] { "one", "all", "roots" }, DefaultValue = "all")]
[CommandParameterNamed("depth", "How deep to inspect",
    AllowedValues = new[] { "quick", "full" }, DefaultValue = "quick")]
[CommandParameterNamed("format", "Output shape", AllowedValues = new[] { "text", "json" }, DefaultValue = "text")]
[CommandHelpRemarks("Pipe the report into DIAG RECORD to keep it, or into DIAG EXPORT to attach it to a bug report.")]
public sealed class DoctorCommand : AbstractCommand { … }
```

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `package` | ordered, `UsePipe = true` | `string` | no | `""` | quoted id | Diagnose one package. |
| `scope` | named | `string` | no | `all` | `one` \| `all` \| `roots` | `roots` checks only the roots themselves — existence, canonical form, writability, symlinks, containment. |
| `depth` | named | `string` | no | `quick` | `quick` \| `full` | `full` adds metadata-only assembly inspection: TFM, referenced contract version, `[CommandRegister]` presence, RID folders, `deps.json` probe paths. |
| `format` | named | `string` | no | `text` | `text` \| `json` | |

**The checks, in order** — each maps to a specific silent failure in the loading stack:

| # | Check | The silence it breaks |
|---|---|---|
| 1 | Root exists, is a directory, is **not writable by non-owners** | ref-loader §10 step 1: a writable plugin root defeats integrity verification, and nothing warns. |
| 2 | Root was actually accepted | `AddPackageDirectory` → `VerifiedSourceDirectories.AddDirectory` **silently returns false**; the only downstream symptom is `NoPluginsFoundException`. |
| 3 | Path canonicalization, component-wise containment, symlink resolution | The library's containment test is `StartsWith`, which admits a sibling-prefix directory, and it does not resolve reparse points (findings B8). |
| 4 | Layout matches `*/bin/*.dll` | A package extracted one level too high or too deep is simply never seen. |
| 5 | File extension is `.dll`/`.exe` | An extensionless file **passes** `VerifyPath` (finding B9) and then fails later, confusingly. |
| 6 | Path components against the forbidden list **for this OS** | The built-in `Strict` list is Windows-shaped and inert on Linux/macOS (finding B11); this reports which list is in force. |
| 7 | Digest present in the allowlist, and matching | In `learningMode: false` an unknown file is refused with "no trusted hash found", which reads like corruption. |
| 8 | Signature status against the policy | |
| 9 | `[CommandRegister]` present on at least one type | `CommandRegistry.AddCommand` **traces a warning and silently returns without registering** a type that lacks it. |
| 10 | Contract assembly version, and whether the package ships a private copy | A private `Xcaciv.Command.Interface` yields `ReflectionTypeLoadException`; the crawler catches it and skips the package. |
| 11 | TFM compatibility with the host's runtime | |
| 12 | RID folders vs the running RID; native payload presence and architecture | The source product's own worst failure class — a native payload for the wrong RID surfaced as a hard access violation, not a managed error. |
| 13 | `deps.json` probe paths that leave the package directory | Dependency loads are **not** confined to `basePathRestriction` (finding B10). |
| 14 | Root command / command-name collisions with already-registered commands | Registration replaces on duplicate; a package can silently shadow a built-in. |
| 15 | Declared `modifiesEnvironment` names that the package does not actually contain | The hostile-manifest case `PKG TRUST` refuses. |

**Pipeline behaviour** — **Filter.** One piped chunk is one id. Emits one chunk per finding (severity, check name, package, one-sentence explanation, one-sentence remedy) plus a summary; a finding of severity `error` is emitted as a **failure chunk**, so `PKG DOCTOR | REGIF` and `PKG DOCTOR | DIAG RECORD` both do the obvious thing. `OutputFormat = JSON` under `-format json`, else `General`.

**Environment interaction** — Reads the roots, trust store, signature policy, `CHATDBG_ALLOW_USER_PACKAGES`, and the host's build info for TFM/RID comparison. Writes nothing; not `modifiesEnvironment`.

**Failure modes** — Never fails as a whole. An unreadable file becomes a finding, not an exception. `-depth full` on a package that cannot be opened for metadata reports the OS error as a finding. The **one** thing it will not do is load a package to find out what is wrong with it: every check is path-, metadata- or digest-based, so running `DOCTOR` can never execute third-party code. That constraint is why the check list is exactly this list.

**Security and audit** — No secrets. Not destructive. Paths rendered `~/…`. Findings are audited as counts by severity, not as text.

**Traceability** — PRD **7.1**, with a direct debt to **7.11** (the source's diagnostic-logging feature existed largely because native load failures were undiagnosable). **NEW.** Reason: the loading stack's dominant failure mode is *silence* — three separate layers skip, trace or return `false` rather than reporting — so a host built on it needs one tool whose entire job is to convert those silences into sentences.

---

#### 9.3.13 `PKG LOCK` — a portable, reproducible package set

**Registration**

| Field | Value |
|---|---|
| Command | `LOCK` |
| Root command | `PKG` |
| Description | `Write, check or restore a lockfile of installed packages and their digests` |
| Prototype | `PKG LOCK [<write\|check\|restore>] [-scope installed\|trusted] [-trust preserve\|drop] [-clean] [-force]` |

```csharp
[CommandRegister("Lock", "Write, check or restore a lockfile of installed packages and their digests",
    Prototype = "PKG LOCK [write|check|restore] [-scope installed|trusted] [-trust preserve|drop] [-clean] [-force]",
    Version = "1.0.0")]
[CommandParameterOrdered("action", "What to do", IsRequired = false, DefaultValue = "check",
    AllowedValues = new[] { "write", "check", "restore" })]
[CommandParameterNamed("scope", "Which packages the lockfile covers",
    AllowedValues = new[] { "installed", "trusted" }, DefaultValue = "installed")]
[CommandParameterNamed("trust", "Whether restore re-applies recorded trust grants",
    AllowedValues = new[] { "preserve", "drop" }, DefaultValue = "drop")]
[CommandFlag("clean", "On restore, remove installed packages the lockfile does not list")]
[CommandFlag("force", "Skip confirmations")]
[CommandHelpRemarks("The lockfile path comes from CHATDBG_PKG_LOCK_PATH — a path cannot be typed as an argument.")]
[CommandHelpRemarks("-trust preserve re-grants trust on another machine only when the digest matches exactly, and is audited as a grant on that machine.")]
public sealed class LockCommand : AbstractCommand { … }
```

**Parameters**

| Name | Kind | Type | Required | Default | Allowed / range | Help |
|---|---|---|---|---|---|---|
| `action` | ordered | `string` | no | `check` | `write` \| `check` \| `restore` | `check` is the default because it changes nothing. |
| `scope` | named | `string` | no | `installed` | `installed` \| `trusted` | |
| `trust` | named | `string` | no | `drop` | `preserve` \| `drop` | **Defaults to `drop`**: trust is a per-machine, per-operator decision, and copying grants between machines is exactly the mistake a portable lockfile invites. |
| `clean` | flag | `bool` | n/a | `false` | presence = true | Destructive: removes packages absent from the lockfile. |
| `force` | flag | `bool` | n/a | `false` | presence = true | Skip confirmations for `restore` overwrite and `-clean`. |

**The lockfile** — deterministic JSON, stable key order, invariant culture, ISO-8601 UTC: schema version, generator version, and per package `id`, `version`, `feedName`, `nupkgSha256`, per-file `{relativePath, sha256}`, `signatureSubject`, `signatureThumbprint`, `installedAt`, and — only under `-scope trusted` — the granted command names and the grant note. **Paths inside it are relative to the package root**, which is the entire point: the loader's own `AssemblyHashStore` CSV is keyed by absolute machine paths and is therefore useless on another machine (finding B15). `restore` regenerates that CSV *on the target machine* from these relative digests, with absolute paths, at install time.

**Pipeline behaviour** — **Filter.** `check` emits one chunk per drifted or missing package (as a failure chunk) — so `PKG LOCK CHECK | PKG INSTALL` reinstalls exactly what is missing. `restore` accepts piped ids to restore a subset. `write` is a source emitting one confirmation chunk naming the path and the package count.

**Environment interaction** — Reads `CHATDBG_PKG_LOCK_PATH`, both roots, the trust store, the feed registry. `restore` writes `CHATDBG_PKG_COUNT` and `CHATDBG_PKG_RELOAD_PENDING`; the command is registered **without** `modifiesEnvironment` and publishes through the host mirror, except that `restore` delegates its installs to `INSTALL`'s code path, which holds the rights. (Stated plainly: `LOCK` never writes a global directly.)

**Failure modes** — Lockfile absent on `check`/`restore` → `Failure` naming the path and `PKG LOCK WRITE`. Malformed lockfile → `Failure` naming the JSON path and position; never partially applied. A digest in the lockfile that does not match what the feed serves now → the package is **not** installed and the row is reported as `tampered-or-republished`, which is the single most valuable signal a lockfile can give. `-clean` without confirmation on a non-interactive front end → refused. A `restore` that fails halfway leaves every already-installed package intact and reports what remains.

**Security and audit** — The lockfile contains **no secrets** (a private feed appears by name, never with its credential) and is written `0600` where supported. It is **not** a trust anchor: `restore -trust preserve` re-grants only on an exact digest match, and every such grant is audited on the receiving machine as a `TrustGrant` with `source: lockfile`. `-clean` is destructive and confirmed.

**Traceability** — PRD **7.1**, and **7.15** by analogy (reproducible artefacts). **NEW.** Reason: the trust store is unsigned, absolute-path-keyed and non-portable; teams that share a tool set need a way to reproduce it that does not mean copying a file that authorizes DLLs by path. This is that way, and its default (`-trust drop`) keeps the human decision on each machine.

---

### 9.4 Pipeline compositions

Five worked examples. Every one of them is expressible only because `SEARCH`, `LIST`, `UPDATE -check`, `VERIFY` and `LOCK CHECK` emit **one chunk per row** and `INSTALL`, `SHOW`, `VERIFY`, `UPDATE`, `REMOVE`, `UNTRUST` and `DOCTOR` accept **one identity per chunk**.

**1 — Discover, inspect, then decide.**
```
PKG SEARCH chatdbg logprob -take 5 -verbosity quiet | PKG INSTALL -dry
```
Five candidate ids stream out of the feed as five chunks; each is resolved, downloaded to quarantine, its identity re-read from the artefact, its signature checked and every shipped file hashed — and then nothing is placed. The user gets five verdict rows: id, resolved version, signature subject, digest prefix, file count, and any finding (unsigned, ships a private contract assembly, wants environment-modifying rights). Quarantine is cleaned afterwards. This is the composition an operator should run before *any* first-time install, and it is exactly what Cupcake could not do: its `SearchCommand` returned one joined blob and its `InstallCommand` refused piped input outright.

**2 — Apply every safe update, then make the shell live.**
```
PKG UPDATE -check | PKG UPDATE -allow minor -force | PKG RELOAD
```
Stage one emits one bare id per updatable package. Stage two updates each within a minor-version bound, installing beside the current version, verifying, and **resetting each updated package to `sandboxed`** — a new version is new bytes and inherits no grant. Stage three re-scans and prints the honest diff: `6 commands added; 1 command from a replaced package remains registered until restart`. Note the framework's semantics doing useful work here: a failed update in stage two is a failure chunk that travels to the end of the pipeline while the other packages keep updating, and `PKG RELOAD` still runs.

**3 — Integrity sweep, keeping only the problems.**
```
PKG LIST -fields id | PKG VERIFY -quiet | REGIF "changed|missing|unknown"
```
`PKG LIST -fields id` is a pure identity source; `PKG VERIFY -quiet` emits nothing for healthy packages (an empty success is dropped by the executor — the sanctioned filter idiom) and a failure chunk for each unhealthy one; the framework's built-in `REGIF` narrows to the three verdicts worth waking someone for. Run from a scheduled batch front end this is a tamper monitor, and it needs no code beyond three attribute-declared tools and one built-in.

**4 — Cross-package: diagnose a silent package and file the evidence.** (`DIAG` is `ChatDbg.Tools.DiagnosticsObservability`, PRD 7.11.)
```
PKG DOCTOR "ChatDbg.Tools.Weather" -depth full | DIAG RECORD -level warn -source pkg | DIAG EXPORT
```
`PKG DOCTOR` walks its fifteen checks without loading a line of the package's code and emits one chunk per finding, errors as failure chunks. `DIAG RECORD` journals each finding into the diagnostic buffer with a level and a source tag, forwarding each chunk unchanged. `DIAG EXPORT` writes the support snapshot and returns a one-line confirmation naming the file. The user ends with an attachable artefact that says, in sentences, why a package the crawler silently skipped was silently skipped — the exact information that is otherwise available nowhere.

**5 — Reproduce a colleague's tool set on a fresh machine.**
```
PKG LOCK CHECK | PKG INSTALL -deps allow | PKG RELOAD
```
`LOCK CHECK` compares the lockfile at `CHATDBG_PKG_LOCK_PATH` against what is installed and emits a failure chunk per missing or drifted package, each carrying the exact `id@version`. `INSTALL` acquires each from the feed the lockfile names, and refuses any package whose content digest does not match the lockfile's — a republished or tampered version fails loudly instead of installing quietly. Everything lands `sandboxed`; nothing inherits the originating machine's trust, so the new operator makes their own `PKG TRUST` decisions with `PKG SHOW` in front of them.

---

### 9.5 Design notes for the architect

**What state this package holds: none that survives a command.** Every tool class is constructed fresh per execution by `CommandFactory` (and, if registered in a container, **must be registered transient** — a singleton command instance is reused across executions *and across pipeline stages*, which would leak a half-finished quarantine transaction into the next package). All durable state lives behind four seams in `ChatDbg.Tools.Abstractions`: `IPackageFeed` (search, versions, download, identity read), `IPackageStore` (quarantine, placement, inventory, the lockfile), `ITrustStore` (digests, grants, blocks), `ILoaderControl` (verify-and-rescan, registry introspection). The only per-instance state is per-pipe accumulation set up in `OnStartPipe` and flushed in `OnEndPipe`. Remember that `CommandRegistry.GetEnvironment` **instantiates every registered command once at startup** to collect `GetDefaultEnvironment()` and throws the instance away: every constructor here must be cheap and side-effect free — no directory creation, no file read, no feed connection.

**What this package must not hold.** No secret, ever — not in a parameter, not in the environment, not in the lockfile, not in an audit record; a private feed's credential is a *slot name* resolved through `CRED` at the instant of the request. No `AssemblyContext`, no `AssemblySecurityPolicy`, no `AssemblyIntegrityVerifier` — those are host policy and exist once, in `PackageTrustGate`. No settings file. No log sink. And no cached "verified" verdict that outlives the command that produced it: `PKG TRUST` re-hashes at the moment of the grant precisely because a verdict from five minutes ago is a TOCTOU window with a user interface on it.

**How it stays testable.** The four seams are the whole answer. The hermetic suite drives every tool against an in-memory feed (a fixed catalogue, deterministic digests, injectable failures: not-found, wrong identity in the artefact, bad signature, zip-slip entry, oversized entry, timeout), an in-memory store over a temp directory, and a fake `ILoaderControl` — no network, no real NuGet, no real assembly load. Cupcake's own package tests hit live nuget.org, which is why they belong in a **separate integration project** (Cupcake §8 rule 7): the integration suite runs the real feed, a real `.nupkg` download-and-delete, a real extraction into a temp root and a real crawl, all behind a skip-unless-configured guard. Two properties are worth pinning explicitly because they are easy to regress: **piped and non-piped paths must produce the same verdicts** for the same identities, and **every destructive tool must refuse when `HasPipedInput` is true and `-force` is absent**.

**When a capability is unavailable — degrade, and say which.**

| Missing | Behaviour |
|---|---|
| Network (`off`, or unreachable) | `SEARCH`, `INSTALL`, `UPDATE`, `SOURCE TEST`, `LOCK RESTORE` fail with one named sentence each. `LIST`, `SHOW`, `VERIFY`, `TRUST`, `UNTRUST`, `REMOVE`, `RELOAD`, `DOCTOR`, `LOCK CHECK/WRITE` are fully functional offline. **Managing what you already have never requires a network.** |
| A signature verifier for this OS | Windows: Authenticode plus the package signature. macOS: the package signature, plus `codesign` for native payloads when the platform tool is present. Linux: the package signature only — there is no platform code-signature to check. Where verification of a class is impossible, `CHATDBG_PKG_SIGNATURE_POLICY` drops from `require` to `prefer` **at startup, with a warning naming the class that cannot be verified** — never silently, and never below `prefer`. |
| A writable user root, or `CHATDBG_ALLOW_USER_PACKAGES=false` | Acquisition still runs and stops at quarantine; the message names the switch. The shell keeps working with whatever the system root already holds. |
| An OS credential store (private feeds) | The feed is reported `unauthenticated` in `SOURCE LIST` and requests to it fail with `unauthorized`; other feeds are unaffected. |
| No packages at all | Not an error anywhere. `NoPluginsFoundException` is caught and rendered as `No tool packages found. Try 'pkg --help'.` — on **every** run path, unlike Cupcake, whose async path treats it as fatal. |
| The trust store | **This is the one thing that does not degrade.** A missing or corrupt trust store means the host refuses to load packages at all (exit 4) and `PKG` reports the same reason. An empty allowlist plus learning mode would be "trust everything", which is the failure this whole package exists to prevent. |

**Where it degrades rather than fails, and where it must not.** Degrade: an unreadable package directory (skip it, report it), a feed that is down (fail that feed, keep the rest), a package whose metadata cannot be read (report every other section), an unload that does not happen (say so, defer to restart), a signature class that cannot be verified on this OS (lower the policy loudly). Never degrade: a digest mismatch, a failed extraction-safety check, an identity that differs from what was requested, a manifest that declares rights for commands it does not ship, a non-interactive trust grant, or writing into the system package root. Each of those is a hard stop with an audit record, because each is indistinguishable from an attack.

**The three non-obvious decisions.** *First*, trust is **not** a flag on install: no argument, on any tool, can produce a trusted package — the only path is `PKG TRUST`, interactively, typing back the id and a digest prefix. That makes the privileged act expensive on purpose, and it is the one place where a worse user experience is the correct design. *Second*, updates **reset** trust, because a version bump is a fresh delivery of third-party bytes and inheriting a grant across it would make `PKG TRUST` a one-time formality. *Third*, this package **never reads a URL or a path from the command line**: it is not a stylistic choice but a consequence of `NamesValidator`'s argument scrub, and rather than pretend otherwise it routes every such value through the pipe, the interactive prompt, or host-set configuration — and says so in `CommandHelpRemarks` on every affected tool, so the user learns the rule from the tool that needed it.

---

# Part III — Appendices

## Appendix A — Complete tool index

All 117 tools, alphabetical within package. **Origin** is `ported` (traces to observed source behaviour) or **NEW**.


### A.1 `CHAT` — Session & Conversation (12 tools)

| Tool | Purpose | Pipeline role | Origin |
|---|---|---|---|
| `CHAT APPEND` | Append a fabricated turn (role + content) to the end of the conversation record without calling any model; can set the dormant isCommand flag so backends exclude the message from every request. | both — one piped chunk is the content of one message appended with the command-line role; emits one confirmation chunk per append (silent under -quiet via an empty success) | ported:ChatHistory.AddMessage(role, content, isCommand, logProbabilities) Models/ChatHistory.cs:14-24 — a real product operation with no user-facing command |
| `CHAT CLEAR` | Empty the conversation record, optionally preserving system-role turns, optionally minting a new session identifier, and optionally writing a backup file first; preserves 'Cleared 0 messages' as a success and the rule that session id and creation timestamp survive. | not-pipeable — refuses piped input, emits one output chunk | ported:/clear Commands/ClearCommand.cs and Models/ChatHistory.cs:53-56 |
| `CHAT EXPORT` | Write the conversation record to a file, byte-compatible with the source's JSON format (key order, 2-space indent, seven-digit UTC fractional seconds, aggressive escaping, shortest-round-trip numbers), plus new jsonl/md/text formats, atomic temp-and-rename writes and optional export-root containment. | both (sink-leaning) — one piped chunk is one message record in the -format shape, written instead of the live record; emits one summary chunk | ported:/export <file_path> Commands/ExportCommand.cs and Services/ChatHistoryService.cs:29-43 |
| `CHAT IMPORT` | Load a conversation record from a file, replacing or appending, with a new default of warn-level role validation, a 32 MiB size cap, and optional import-root containment; preserves in-place mutation of the live record, session-id overwrite and the no-extension-defaulting asymmetry. | both — one piped chunk is one file path imported in order; emits one summary chunk per path, or one chunk per message under -emit; overrides Main for -emit | ported:/import <file_path> Commands/ImportCommand.cs and Services/ChatHistoryService.cs:45-65 |
| `CHAT INJECT` | Insert a fabricated turn at a chosen zero-based position in the record, with the source's role whitelist and out-of-range-appends rule preserved and its last-positional-token position parsing deliberately replaced by a named -position parameter. | both — one piped chunk is one message body, chunk n inserted at position + n so upstream order is preserved; one confirmation chunk per injection | ported:/inject <role> <message> [position] Commands/InjectCommand.cs and Models/ChatHistory.cs:34-51 |
| `CHAT LIST` | Emit the conversation record one message per chunk, filtered by role and range, in text, JSON or CSV — the pipeline source every other tool composes against. | source — refuses piped input; emits one chunk per message; declares ResultFormat.JSON or .CSV so downstream tools branch on format rather than sniffing; overrides Main | **NEW** |
| `CHAT NAME` | Show, set or rename the current session's friendly name, and optionally pin its session identifier to a specific UUID for reproducible experiments. | source (output only) — refuses piped input because a rename per chunk is meaningless and destructive; emits one chunk | **NEW** |
| `CHAT POP` | Remove the last message (or the last N) from the record and emit the removed messages so they can be saved before they are lost; preserves the 50-character preview default while fixing the unconditional ellipsis and the UTF-16 mid-surrogate cut. | source (output only) — refuses piped input with an explanatory string; emits one chunk per removed message newest-first plus a summary; overrides Main for -count > 1 | ported:/pop Commands/PopCommand.cs and Models/ChatHistory.cs:26-32 |
| `CHAT RETRY` | Discard the last reply and re-send the preceding user turn with per-invocation overrides (temperature, model, provider, top-K, system prompt), optionally emitting the discarded reply first — the product's central compare-two-settings loop as one command. | source (output only) — refuses piped input; emits the discarded reply under -keep, then the streamed reply chunks, then an elapsed-time chunk; overrides Main | **NEW** |
| `CHAT SEND` | Send a conversational turn to the active model backend and stream the reply, recording both sides in the conversation record; carries per-turn overrides for provider, model, temperature, max tokens, log probabilities, top-K, system prompt, streaming, output format and timeout. | both — accepts piped input (one chunk = one complete user turn, sent as an independent request) and produces piped output (one chunk per streamed segment plus a terminal elapsed-time chunk); overrides Main to emit multiple chunks on the non-piped path | ported:the console shell's chat-turn path (any typed line not beginning with '/'), ChatShell.SendMessageAsync src/ChatDbg/ChatShell.cs:343-410 |
| `CHAT SESSIONS` | List saved conversation sessions with name, message count, created and last-used timestamps and an active marker, sortable and filterable, in text, JSON or CSV. | source — refuses piped input; emits one chunk per session; unparseable session files are skipped as isolated failures; overrides Main | **NEW** |
| `CHAT USE` | Switch the active conversation session, creating it when absent or forking the current record into it, saving the current session first by default; writes CHAT_SESSION into the package environment bucket, which is how every other tool sees the switch. | both — the ordered name parameter declares UsePipe, so one piped chunk is one session name; emits one confirmation chunk per switch and warns when more than one name arrives | **NEW** |

### A.2 `SET` — Configuration & Profiles (16 tools)

| Tool | Purpose | Pipeline role | Origin |
|---|---|---|---|
| `SET DIFF` | Report the keys that differ between two profiles, or between a profile and the built-in defaults | filter (one chunk = one left-hand profile) and source; silent when identical | **NEW** |
| `SET DROPPROFILE` | Permanently delete a saved profile; requires -force, refuses the active profile and 'default' | filter (one chunk = one profile name) | **NEW** |
| `SET ENV` | Set a shell environment variable — the framework built-in SET preserved under the new root | sink (accumulates piped chunks into the variable; emits only empty successes) | ported:the Xcaciv.Command built-in SET command (Xcaciv.Command/Commands/SetCommand.cs) |
| `SET GET` | Print the value of exactly one setting, optionally with its provenance | filter (one chunk = one setting key; one value chunk out per key) and source | **NEW** |
| `SET IMPORT` | Validate and adopt a complete settings document from a file or the pipe, replacing or merging | filter (one chunk = one complete document) and sink | **NEW** |
| `SET KEYS` | Emit the machine-readable key catalog: key, aliases, type, default, min, max, allowed values, current value | source (overrides Main to emit one chunk per key) | **NEW** |
| `SET MODEL` | Show or change the model identifier / GGUF path, with the same validation SET VALUE modelId applies | filter and source | ported:/model (Commands/ModelCommand.cs:23-34) |
| `SET PATH` | Report the resolved settings document, base directory and profile directory, and how each was chosen | source | ported:the plain-console startup banner line 'Settings file: <path>' (ChatDbg/ChatShell.cs:192-208) |
| `SET PROFILE` | Show the active configuration profile, or validate and switch to another | filter and source | **NEW** |
| `SET PROFILES` | List every saved profile, one per chunk, optionally with provider/model/last-used/validity | source (one chunk per profile) | **NEW** |
| `SET RESET` | Restore one key, one section, or the whole record to built-in defaults | source (emits one line per field changed; refuses piped input) | **NEW** |
| `SET SAVEPROFILE` | Capture the current record (or another profile) as a named profile, with secret slots stripped | filter (one chunk = one profile name to create) | **NEW** |
| `SET SHOW` | Render the effective configuration (all or one section) in text/JSON/YAML/CSV, with secrets shown as status-and-provenance only | source (produces piped output; refuses piped input with an explanatory string) | ported:/set with no arguments (Commands/SetCommand.cs:401-458) plus the windowed Settings dialog read path |
| `SET UNSET` | Clear a text setting to empty, or return a typed setting to its built-in default | filter (one chunk = one key to clear) | **NEW** |
| `SET VALIDATE` | Validate the active record, a named profile, or a piped JSON document against the key catalog | filter (one chunk = one complete JSON settings document) and sink under -strict | **NEW** |
| `SET VALUE` | Validate and store one tunable, then persist the document | filter (value arrives via UsePipe; one chunk = one candidate value; one transaction committed in OnEndPipe) | ported:/set <key> <value...> (Commands/SetCommand.cs:28-294) |

### A.3 `CRED` — Credentials & Secret Storage (13 tools)

| Tool | Purpose | Pipeline role | Origin |
|---|---|---|---|
| `CRED DISABLE` | Disable the secret-store tier (always permitted on every platform), optionally purging every ChatDbg entry from the backend. | source (refuses piped input) | ported:the false half of /set useWindowsCredentialManager |
| `CRED DOCTOR` | Diagnose credential readiness per provider end to end - both halves of a key pair individually, winning tier, shadowing, store reachability, the SDK ambient chain, plaintext copies and file permissions - with remediation text. | filter (one piped chunk = one provider name; one chunk per check plus a verdict; source when unpiped) | ported:startup credential diagnostics (ChatShell.cs:239-321), with the half-key-pair and wrong-provenance defects corrected |
| `CRED ENABLE` | Enable the OS secret-store credential tier after explicit y/yes consent, selecting a backend; declining is a success, not a failure. | source (refuses piped input with an explanatory string - consent must not derive from an upstream stage) | ported:/set enablewincred + the true half of /set useWindowsCredentialManager |
| `CRED ENV` | Name the environment variables each slot reads, in priority order, with occupancy and shell-specific copy-paste templates carrying placeholders. | filter (one piped chunk = one slot id; source when unpiped) | **NEW** |
| `CRED LIST` | List every credential slot with a masked status (***set*** / (not set)) and the channel it resolved from, never a value. | filter (accepts piped slot ids via UsePipe; emits one row chunk per slot; also runs as a source with no pipe) | ported:credential block of bare `/set` (SetCommand.cs:401-463) + ChatSettings.GetCredentialSource |
| `CRED MIGRATE` | Move credentials out of the deprecated settings-file fields into a store or environment variables, emitting placeholders instead of plaintext, with per-slot truthful outcomes and abort-before-purge safety. | filter (one piped chunk = one slot id; one outcome chunk per slot plus a summary) | ported:/set migrate wizard (SettingsService.cs:196-268), env-instruction printer (:328-360), bulk vault migration (:289-326) |
| `CRED REDACT` | Mask live credential values and credential-shaped patterns in text flowing through the pipe; overrides Main so it also scrubs upstream FAILURE chunks' ErrorMessage. | filter (requires piped input; one chunk in, one chunk out, preserving IsSuccess, CorrelationId and OutputFormat) | **NEW** |
| `CRED REMOVE` | Delete a credential entry from a store and/or clear the deprecated plaintext settings field; idempotent, confirmed, never touches the environment tier. | filter (one piped chunk = one slot id; one outcome chunk per deletion) | **NEW** |
| `CRED ROTATE` | Replace a stored secret, verify by read-back, optionally keep a backup entry, and warn when a higher-priority tier still shadows the new value. | filter/sink (one piped chunk = the new secret value; emits one summary chunk) | **NEW** |
| `CRED SCAN` | Find plaintext credentials on disk or occupied environment variables and report them with salted 4-char fingerprints, severities and file-permission findings - never a value. | filter (one piped chunk = one filesystem path; one chunk per finding; source when unpiped) | **NEW** |
| `CRED SET` | Store a secret in an OS secret store. Deliberately has NO value parameter - the secret arrives from a no-echo prompt or as one piped chunk. | filter/sink (one piped chunk = one complete secret value; emits one confirmation chunk) | ported:/set wincred <credential-type> <value> (SetCommand.cs:228-250, SettingsService.cs:145-194) |
| `CRED SOURCE` | Report which tier supplied one credential, with -all expanding the full priority ladder and marking the winner. | filter (one piped chunk = one slot id; one provenance chunk out; source when unpiped) | ported:ChatSettings.GetCredentialSource(string) + the startup 'credentials loaded from:' line |
| `CRED STORES` | Enumerate secret-store backends with LIVE availability, capabilities, roaming/encryption facts, max secret length and the reason any backend is unusable. | source (one chunk per backend; refuses piped input) | **NEW** |

### A.4 `PROMPT` — System Prompt Library (14 tools)

| Tool | Purpose | Pipeline role | Origin |
|---|---|---|---|
| `PROMPT COPY` | Duplicate a prompt under a new name so a persona can be forked without re-typing it. | source | **NEW** |
| `PROMPT CREATE` | Create a new prompt with content from a pipe, a file, inline text, or the built-in default text. | sink | ported:/prompt create <n> [description] (and GUI Create dialog) |
| `PROMPT DELETE` | Delete a prompt, with the active-prompt guard, a No-defaulted confirmation and a single-generation backup. | filter | ported:/prompt delete <n> (and GUI Delete button) |
| `PROMPT DOCTOR` | Diagnose the library: unreadable files, name/file mismatches, sanitization collisions, dangling active name, missing built-ins; -fix applies only reversible repairs. | source | **NEW** |
| `PROMPT EDIT` | Replace a prompt's content and/or description via pipe, file, inline text, $EDITOR, or END-terminated interactive entry. | sink | ported:/prompt edit <n> (and GUI Edit dialog) |
| `PROMPT EXPORT` | Export a prompt into the exchange directory as text (content only) or JSON (lossless round trip). | filter | ported:/prompt export <n> [file_path] |
| `PROMPT IMPORT` | Import a prompt from a bare file name inside the confined exchange directory, as text or as a full JSON record. | filter | ported:/prompt import <n> <file_path> [description] |
| `PROMPT LIST` | Enumerate, filter and sort the prompt library; emits one chunk per prompt in text/names/csv/json. | source | ported:/prompt list |
| `PROMPT PATH` | Report the store, exchange and backup directories, how each was resolved, and whether each is writable. | source | **NEW** |
| `PROMPT RENAME` | Rename a prompt while preserving description, createdAt and lastUsedAt, and follow the active selection. | not-pipeable | **NEW** |
| `PROMPT RESET` | Restore missing (or, with -force, modified) built-in starter prompts explicitly instead of relying on empty-store auto-seeding. | source | **NEW** |
| `PROMPT SHOW` | Show one prompt with its description and timestamps, or only its content. | filter | ported:/prompt show <n> (and GUI Show button) |
| `PROMPT STATUS` | Show the active system prompt, its text and metadata; -content-only emits just the text for piping. | source | ported:/prompt (bare, no arguments) |
| `PROMPT USE` | Select the active prompt: publishes name and text to the global environment and stamps last-used. | filter | ported:/prompt use <n>, /set systemPrompt <n>, GUI Use button |

### A.5 `MODEL` — Model Backends (11 tools)

| Tool | Purpose | Pipeline role | Origin |
|---|---|---|---|
| `MODEL CAPS` | Report per-backend capabilities and ceilings: log-probabilities, top-K reporting max, prompt-side logprobs, streaming, tool calling, temperature/token/context ceilings, cancellation; states declared vs verified. | filter (one chunk = one backend key in; one chunk per backend or per capability row out) | **NEW** |
| `MODEL CATALOG` | List the models, deployments or GGUF files a backend can actually serve, assigning token-safe short handles; caches with TTL. | source (one chunk per catalog row; -format ids emits bare ids for piping; refuses piped input) | **NEW** |
| `MODEL LIST` | Enumerate every registered model backend with key, display name, readiness and active marker; emits one chunk per backend. | source (produces piped output, one chunk per backend; refuses piped input with an explanatory chunk; overrides Main to emit multiple chunks) | ported:shell provider registry (Dictionary<string,IAIService> keyed azure/bedrock/llama) + console start-up configuration self-check block |
| `MODEL LOAD` | Bring a GGUF weight file resident with its hardware-acceleration settings; strict teardown ordering on reload; -force applies changed hardware settings the source could only apply on a path change. | filter (one chunk = one weight path or catalog handle, loaded in sequence with unload between; one summary chunk per load) | ported:LLamaSharpService.EnsureModelLoadedAsync (private lazy first-chat-turn side effect, promoted to an explicit tool) |
| `MODEL SELECT` | Set the model id / deployment name / weight-file handle for a backend, with per-backend validation and a -revision escape for ':'-suffixed ids stripped by the tokenizer. | filter (one chunk = one candidate model id or handle; the safe channel for values containing ':' '/' '\\') | ported:`/model <id…>` merged with `/set modelId <id>` (the two source commands wrote the same field under different rules) |
| `MODEL SHOW` | Describe one backend's configuration, effective request parameters after backend-specific clamping, and credential source label (never a value). | filter (accepts piped input where one chunk = a backend key; produces one record chunk per input) | ported:`/set` with no arguments (settings listing) + console start-up banner + `LLama Configuration:` block |
| `MODEL STATUS` | Report what is resident locally — path, size, load time, effective hardware settings, bound native backend and RID, honest context accounting — plus a drift table of settings that differ from the resident load. | source (one chunk per group: model, hardware, context, drift; refuses piped input) | **NEW** |
| `MODEL TEST` | Six-stage end-to-end probe: configured, credential source, shape, reachable, entitled (-deep, billable), capability agreement. | filter (one chunk = one backend key in; one chunk per stage out, failed stages followed by 'skipped' successes) | **NEW** |
| `MODEL TUNE` | Persist the five local hardware-acceleration settings (context, gpu-layers, threads, batch, device) with reject-not-clamp validation; -apply reloads so they take effect now; modifiesEnvironment:true. | source (one confirmation chunk; refuses piped input) | ported:`/set llamaContextSize|llamaGpuLayers|llamaGpuDevice|llamaThreads|llamaBatchSize` and the windowed shell's 'LLama Settings' tab |
| `MODEL UNLOAD` | Release the resident weight file, context and session; idempotent; confirmation-gated because it discards the engine's retained conversation state. | sink / not-pipeable (declines piped input with an explanatory chunk; emits one summary chunk, or none with -quiet) | ported:LLamaSharpService.Dispose teardown path (reachable only at shell shutdown, and only from one of the two shells) |
| `MODEL USE` | Make a registered backend active and report its readiness; registered modifiesEnvironment:true to write CHATDBG_PROVIDER. | filter (one chunk = one backend key; emits a confirmation row per switch) | ported:`/set provider <azure|bedrock|llama>` |

### A.6 `TOKEN` — Token Introspection (13 tools)

| Tool | Purpose | Pipeline role | Origin |
|---|---|---|---|
| `TOKEN ATTRIBUTE` | Attribute each generated token to the prompt tail or the look-back window of prior tokens that influenced it, with a real recency weight and a named method. | both (filter-shaped: passes analysis/token chunks through and interleaves attribution chunks) | ported:/inspect stage E and TokenInspectionService.BuildAttributionMap |
| `TOKEN CAPTURE` | Configure per-token confidence capture: on/off, top-K 1-20 (default 5), display mode, layout, grid max alternatives, sample size, generation budget, low-confidence threshold; bare form reports status. | neither by default (empty success, like built-in SET); source with -print (key=value lines); filter when fed key=value assignments | ported:/logprobs [enable|disable|top n|showall|showsample|grid|list|gridmaxalt n] and /set enableLogProbabilities|logProbabilitiesTopK|showAllTokens|gridViewForTokens|gridViewMaxAlternatives |
| `TOKEN DEMO` | Emit a deterministic, cross-platform-reproducible 25-token sample analysis with no model call; every record stamped synthetic:true. | source | ported:/demologprobs (DemoLogProbsCommand) |
| `TOKEN DIAG` | Explain why token confidence is or is not available: current policy, provider/model, backend capability probe, and the actual API version the provider will send. | source | ported:/logprobs debug |
| `TOKEN DIFF` | Compare two analyses token by token with text or index alignment, reporting per-position probability shift, band change, alternatives gained/lost, and a summary with perplexity delta. | filter (piped stream is the 'against' side; baseline named as an ordered parameter) | **NEW** |
| `TOKEN EXPORT` | Write an analysis to json/jsonl/csv/md in the analysis directory, with -redact to strip all content and keep the numbers, and confirmation before overwrite. | sink (accumulates a piped analysis stream, writes one file per analysis id, emits one confirmation line) | ported:export-analysis (ExportTokenAnalysisCommand stub) and LLamaSharpService.SaveAnalysesToJson |
| `TOKEN FILTER` | Keep only token records matching a confidence predicate (-below/-above/-band/-marginbelow/-range), or apply the source's beginning/middle/end sampling rule; rejects via empty-success so the host drops them. | filter (primary form); also operates on a named or last analysis when not piped | **NEW** |
| `TOKEN INSPECT` | Composite run: tokenize, generate, map probabilities and attribute in one analysis, with a pipeline-safe consent gate via io.PromptForCommand and a -yes bypass. | both (source and filter, emitting the union of all three stages for one analysis id) | ported:/inspect (InspectCommand) |
| `TOKEN LOAD` | Read a previously exported analysis back into the session and stream it record by record, with schema-version checking and path containment. | source | **NEW** |
| `TOKEN MAP` | Map the probability distribution the model weighed at each generation step: chosen token plus top-K alternatives, streamed as generation proceeds. | both (source streams one chunk per generated token; filter: one piped chunk = one prompt) | ported:TokenInspectionService.GenerateProbabilityMap and /inspect stage D |
| `TOKEN SHOW` | Render an analysis as readable text in list or grid layout, preserving the source's escaping, 2-decimal percentage format and '(+ N more)' alternatives suffix; marks synthetic and estimated data. | sink-shaped filter (one document record in, rendered lines out as ResultFormat.General) | ported:show-analysis (ShowTokenAnalysisCommand stub) and the console shell's render path |
| `TOKEN SPLIT` | Split text into model tokens with real decoded text, vocabulary IDs and character spans; emits an analysis document. | both (source: overrides Main to emit analysis chunk + one token chunk per token; filter: one piped chunk = one text to tokenize) | ported:/tokenize (TokenizeCommand) and TokenInspectionService.AnalyzePrompt |
| `TOKEN STATS` | Compute entropy (truncated top-K, nats or bits), normalized entropy, perplexity, mean negative log-likelihood, margin, confidence-band counts and low-confidence ratio over an analysis. | both (overrides Main: passes every chunk through and appends one stats chunk per analysis id at end of stream; -perstep enriches token chunks in place) | **NEW** |

### A.7 `VIEW` — Presentation & Visualization (13 tools)

| Tool | Purpose | Pipeline role | Origin |
|---|---|---|---|
| `VIEW CAPS` | Probe the terminal once and publish width, height, colour depth, redirection, Unicode support, wide-character support, mouse support and platform as CHATDBG_VIEW_CAPS_* globals; -assume forces a fixed profile for deterministic tests and CI. | source — produces one report chunk; declares ResultFormat.JSON under -format json because the output is genuinely machine-readable and meant to be filtered | **NEW** |
| `VIEW HEAT` | Paint generated text as a continuous flowing paragraph with each token's background coloured by confidence, honouring the 6-band background palette and the black/white contrast flip at 0.5. | filter — one JSON token record per chunk in, ResultFormat.General rows out; streams when drawing all records, buffers when sampling | ported:LogProbHeatmapView.cs (defined in source but never instantiated) and heat map D4 |
| `VIEW LAYOUT` | Set or report layout (grid/list/flow), alternatives budget (1-20, default 5), probability precision and show-all-vs-sample volume mode; persists through the Configuration package by default. | sink with pipe-fed parameter — ordered 'mode' declares UsePipe=true, returns empty success when piped (the framework SET idiom); emits one confirmation line when not piped | ported:/logprobs grid|list|showall|showsample|gridmaxalt and /set gridViewForTokens|showAllTokens|gridViewMaxAlternatives |
| `VIEW PALETTE` | Select or inspect the probability-to-colour mapping among the source's four contradictory heat maps, declare the scale (unit 0-1 vs percent 0-100) so values are converted rather than misread, and diagnose a single probability with -check. | sink with pipe-fed parameter — ordered 'name' declares UsePipe=true; -show and -check produce ResultFormat.General text | ported:heat maps D1-D4 (TokenFormatters.cs:38-45, ChatShell.cs:657-672, ChatWindow.cs:99-113, LogProbHeatmapView.cs:60-77); the selection surface, scale declaration and mono palette are new |
| `VIEW PLAIN` | Strip styling markup and ANSI sequences (or escape them, or resolve them to real escapes), fold decoration to ASCII and expand tabs — the correct answer to redirected output. | filter — one text line per chunk in, one plain line per chunk out (HandlePipedChunk); re-declares ResultFormat.General | ported:BasicConsoleFormatter.cs:17-22 and :152-179 markup stripper, plus SpectreConsoleFormatter.cs:230-235 bracket escaping |
| `VIEW PREVIEW` | Render a built-in deterministic fixture (seed 42, the source's 25-token sample sentence and hand-written alternative sets) through the current settings, so layout, theme and palette can be seen without a backend call. | source — ResultFormat.General; piped input gets an explanatory refusal | **NEW** |
| `VIEW RULE` | Emit an 80-character horizontal rule with an optional caption, preserving the centred (exactly 80 chars) and left-justified (exactly 79 chars) geometries and the markup-stripped caption measurement. | source — not pipeable as a filter; given piped input it returns an explanatory string and succeeds without consuming the pipe | ported:IConsoleFormatter.WriteRule and BasicConsoleFormatter.cs:41-66 |
| `VIEW SAMPLE` | Reduce a long record stream to beginning/middle/end slices using the source's exact arithmetic (size 5, threshold 15, middle start count/2-2, end start count-5) or the 'flow' preset (size 10, threshold 30, middle start floor((count-10)/2)). | filter — buffers the stream (bounded by -max, default 100000), re-emits surviving records unmodified and echoes the upstream OutputFormat; record indices are never rewritten | ported:E1 sampling in ChatShell.cs:435-481 / TokenProbabilityVisualizer.cs:39-88 / DemoLogProbsCommand.cs:179-200, and E2 in LogProbHeatmapView.cs:79-88 |
| `VIEW STATUS` | Report the current presentation settings — display mode, view mode, alternatives budget, theme, palette and scale, colour policy, glyph policy and width — with provenance under -verbose. | source — ResultFormat.General, or ResultFormat.JSON under -format json; reads every key with storeDefault:false so asking cannot cause a write-back | ported:the display half of LogProbsCommand.cs:129-156 and the /set report lines |
| `VIEW THEME` | Select the colour theme (auto/dark/light/contrast/mono), the colour policy (auto/always/never), the glyph policy (auto/on/off) and a fixed render width — the switches that make redirected-output degradation and deterministic tests possible. | sink with pipe-fed parameter — ordered 'name' declares UsePipe=true; emits a confirmation line or a swatch under -preview | ported:ThemeManager.cs:16-44 supplies the dark palette slot-for-slot; the colour/glyph/width policy layer is new |
| `VIEW TOKENS` | Render token-probability records as a dense card grid or a detail table, preserving the source's 90-char ASCII frame (inner widths 7/20/12/38), 17+'...' token truncation, 7+'...' alternative truncation, responsive max(1, width/40) grid columns and the '+ N more' overflow indicator. | filter — accepts one JSON token record per chunk, produces ResultFormat.General rendered text; overrides Main because table frames and grid row packing are not 1:1 with input chunks | ported:IConsoleFormatter.DisplayTokenGrid/DisplayTokenTable, BasicConsoleFormatter.cs:72-112, SpectreConsoleFormatter.cs:55-195, ChatShell.cs:412-687 |
| `VIEW TRANSCRIPT` | Render chat messages as an aligned, wrapped transcript: '[role]' banner, 2-column padding, 75% bubble width, right-aligned user messages, blank-line separation and the diamond probability marker. | filter — one JSON chat message per chunk in, one fully rendered block per chunk out (HandlePipedChunk, genuinely 1:1) | ported:ChatWindow.cs:500-622 transcript rendering and the wrapper at :735-788 |
| `VIEW WRAP` | Wrap text to a width measured in display cells rather than code units, with indent, hanging indent and alignment, clamped so no input can loop or throw. | filter — one text line per chunk in, one wrapped block per chunk out; refuses with a Failure if the upstream chunk declares JSON rather than corrupting a record | ported:ChatWindow.cs:735-788 wrapping logic, extracted as its own bounded, cell-aware tool |

### A.8 `DIAG` — Diagnostics & Observability (12 tools)

| Tool | Purpose | Pipeline role | Origin |
|---|---|---|---|
| `DIAG AUDIT` | Configure structured command-execution auditing, its sink and its redaction policy, and report the framework's real masking limitation. | not-pipeable | **NEW** |
| `DIAG BUILDINFO` | Report the identity and inferred publish profile of the running artefact plus, with -packages, the loaded tool-package inventory and their load-time verification status. | source | **NEW** |
| `DIAG CAPTURE` | Arm/disarm engine diagnostic capture and configure the recorder's sinks, buffer threshold, level filter, file-name pattern, timestamp mode and redaction. | not-pipeable | ported:LLamaSharpLogConfig.ConfigureLogging() plus the five compile-time-only switches (LogDirectory, EnableFileLogging, EnableDebugOutput, EnableConsoleOutput, MaxBufferSize) |
| `DIAG CLEAR` | Discard the in-memory diagnostic buffer without writing it anywhere; requires -force, offers -flush as the non-destructive alternative. | not-pipeable | ported:LLamaSharpLogConfig.ClearLogs() (operation A5) |
| `DIAG DOCTOR` | Environment health check across runtime, build profile, backend availability, native library resolution, credential resolution provenance, configuration validity and storage; one chunk per check. | source / filter (piped chunk = one check id or group name) | **NEW** |
| `DIAG EXPORT` | Write diagnostic entries to a caller-named file suitable for attaching to a bug report, sourced from the buffer, today's file, both merged, or every retained day. | sink (piped chunk = one line to write); also usable non-piped | ported:Commands/ExportLogsCommand.cs (the unregistered stub) + LLamaSharpLogConfig.SaveLogsToFile(path) (operation A7) |
| `DIAG FILES` | List the rolling log files (path, day, bytes, mtime, pattern) and, with -roots, the product's three per-user state locations and their writability. | source (emits one absolute path per chunk, directly consumable by TAIL/ROTATE/EXPORT) | **NEW** |
| `DIAG FLUSH` | Append the buffered entries to today's rolling log file and empty the buffer; report what was written or why nothing was. | not-pipeable | ported:LLamaSharpLogConfig.FlushToFile() (operation A6) |
| `DIAG RECORD` | Record an application-level entry at a given level and tag; as a pipeline stage, journal every chunk that passes through and re-emit it verbatim (or swallow it with -quiet). | filter (tee) / sink with -quiet | ported:LLamaSharpLogConfig.LogMessage(level, message) (operation A3) |
| `DIAG ROTATE` | Apply retention policy to the rolling log files: prune by age, cap total size, roll oversized files, optionally gzip. Dry-run by default; -apply required to modify anything. | source / filter (piped chunk = one candidate file path) | **NEW** |
| `DIAG STATUS` | Report capture state, engine-sink health, buffer occupancy, resolved policy, fault count and last internal fault. | source | **NEW** |
| `DIAG TAIL` | Emit the most recent diagnostic entries from the buffer, the daily files, or both merged, with level/regex/since filtering; one chunk per entry. | source / filter (piped chunk = one log-file path) | ported:LLamaSharpLogConfig.GetLogs() (operation A4, which had no user-facing surface) |

### A.9 `PKG` — Tool Package Management (13 tools)

| Tool | Purpose | Pipeline role | Origin |
|---|---|---|---|
| `PKG DOCTOR` | Run fifteen path, layout, digest, signature, metadata, TFM/RID and manifest checks - without loading any package code - to explain why a package was silently skipped. | filter (one chunk per finding; errors are failure chunks) | **NEW** |
| `PKG INSTALL` | Download to quarantine, re-read identity from the artefact, verify signature and digests, extract safely into {root}/{id}/{version}/bin, record digests; installs SANDBOXED, never trusted. | filter (one chunk = one quoted id[@version]; one verdict chunk per input) | ported:Xcaciv.Command.Packages/InstallCommand + NugetWrapper.InstallPackage (both stubs in Cupcake; steps 5-10 are new) |
| `PKG LIST` | Enumerate installed packages with version, trust level, load state, root, command count; -fields id emits the identity shape every other tool consumes. | source (one chunk per package; JSON/CSV result format) | **NEW** |
| `PKG LOCK` | Write, check or restore a portable lockfile of package identities and relative-path content digests; regenerates the machine-local absolute-path hash store on restore. | filter (CHECK emits drifted/missing ids straight into PKG INSTALL) | **NEW** |
| `PKG RELOAD` | Verify and re-scan the package roots, ask the host to register newly present packages, and report the honest diff including what stays registered until restart. | source (refuses piped input) | **NEW** |
| `PKG REMOVE` | Uninstall a package, purge its digest and trust records, defer file deletion when assemblies are locked; destructive, typed confirmation. | filter (piped removal requires -force) | **NEW** |
| `PKG SEARCH` | Search a package feed for installable tool packages; quiet/normal/detailed rows, take clamped to 1-100, HTTPS-only sources. | source (refuses piped input; emits one chunk per result row) | ported:Xcaciv.Command.Packages/SearchCommand (Cupcake ref-cupcake §7) |
| `PKG SHOW` | Manifest, declared commands (metadata-only), files with digest prefixes, trust record and dependency probe paths for one package - the evidence for a trust decision. | filter (one chunk = one id[@version]) | **NEW** |
| `PKG SOURCE` | List, add, remove, select, test and map package feeds; enforces HTTPS, requires a package-source mapping per feed, stores credential slot names never secrets. | filter for ADD (one chunk = the feed URL, the only lossless channel), source for LIST, refuses input elsewhere | **NEW** |
| `PKG TRUST` | Interactively grant a verified package the environment-modifying rights its manifest declares, recorded against a named digest and signature. | not-pipeable (in either direction; no -force exists) | **NEW** |
| `PKG UNTRUST` | Revoke a grant, optionally drop the digest allowlist entries or block the package from loading entirely. | filter (mass revocation with -force) | **NEW** |
| `PKG UPDATE` | Check for and apply newer versions within a semver bound, installing beside the current version with rollback retention; an update always resets trust to sandboxed. | filter (-check emits bare ids re-consumable by the same tool) | **NEW** |
| `PKG VERIFY` | Re-hash installed packages against the trust allowlist and re-check signatures; reports changed/missing/unknown/signature-failed before it becomes a failed start. | filter (one chunk = one id; unhealthy verdicts are failure chunks) | **NEW** |

**Total: 117 tools.**


---

## Appendix B — Environment key registry

Every key any tool reads or writes. The host seeds the read-only ones at startup; the rest are written by the tool that owns them. Collisions across packages are a design error — this table exists so they are visible.

| Key | Declared by |
|---|---|
| `AWS_ACCESS_KEY_ID` | CRED, DIAG, MODEL, SET |
| `AWS_SECRET_ACCESS_KEY` | CRED, DIAG, MODEL, SET |
| `AWS_SESSION_TOKEN` | MODEL |
| `CATALOG_FETCHED_AT` | MODEL |
| `CHATDBG_ALLOW_USER_PACKAGES` | PKG |
| `CHATDBG_AWS_ACCESS_KEY` | CRED, DIAG, MODEL, SET |
| `CHATDBG_AWS_REGION` | MODEL, SET |
| `CHATDBG_AWS_SECRET_KEY` | CRED, DIAG, MODEL, SET |
| `CHATDBG_AZURE_API_KEY` | CRED, DIAG, MODEL, SET |
| `CHATDBG_AZURE_API_VERSION` | MODEL, SET |
| `CHATDBG_AZURE_ENDPOINT` | MODEL, SET |
| `CHATDBG_DATA_ROOT` | PKG |
| `CHATDBG_DIAG_ARMED` | DIAG |
| `CHATDBG_DIAG_AUDIT` | DIAG |
| `CHATDBG_DIAG_AUDIT_PATH` | DIAG |
| `CHATDBG_DIAG_AUDIT_SINK` | DIAG |
| `CHATDBG_DIAG_BUFFER` | DIAG |
| `CHATDBG_DIAG_COMPRESS` | DIAG |
| `CHATDBG_DIAG_CONSOLE` | DIAG |
| `CHATDBG_DIAG_DIR` | DIAG |
| `CHATDBG_DIAG_FILE` | DIAG |
| `CHATDBG_DIAG_LEVEL` | DIAG |
| `CHATDBG_DIAG_MAX_FILE_MB` | DIAG |
| `CHATDBG_DIAG_MAX_TOTAL_MB` | DIAG |
| `CHATDBG_DIAG_PATTERN` | DIAG |
| `CHATDBG_DIAG_REDACT` | DIAG |
| `CHATDBG_DIAG_RETAIN_DAYS` | DIAG |
| `CHATDBG_DIAG_TIMESTAMPS` | DIAG |
| `CHATDBG_DIAG_TRACE` | DIAG |
| `CHATDBG_ENABLE_LOGPROBS` | CHAT |
| `CHATDBG_FRONTEND` | PKG |
| `CHATDBG_GRID_MAX_ALT` | SET |
| `CHATDBG_GRID_VIEW` | SET |
| `CHATDBG_HOME` | SET |
| `CHATDBG_LLAMA_BATCH_SIZE` | MODEL, SET |
| `CHATDBG_LLAMA_CONTEXT_SIZE` | MODEL, SET |
| `CHATDBG_LLAMA_GPU_DEVICE` | MODEL, SET |
| `CHATDBG_LLAMA_GPU_LAYERS` | MODEL, SET |
| `CHATDBG_LLAMA_THREADS` | MODEL, SET |
| `CHATDBG_LOGPROBS_ENABLED` | MODEL, SET |
| `CHATDBG_LOGPROBS_TOPK` | CHAT, MODEL, SET |
| `CHATDBG_MAX_TOKENS` | CHAT, MODEL, SET |
| `CHATDBG_MODEL_ID` | CHAT, MODEL, SET |
| `CHATDBG_MODEL_PATHS` | MODEL |
| `CHATDBG_OUTPUT_FORMAT` | PKG |
| `CHATDBG_PACKAGE_DIR` | PKG |
| `CHATDBG_PKG_COUNT` | PKG |
| `CHATDBG_PKG_LAST_VERIFY` | PKG |
| `CHATDBG_PKG_LOADED` | PKG |
| `CHATDBG_PKG_LOCK_PATH` | PKG |
| `CHATDBG_PKG_NETWORK` | PKG |
| `CHATDBG_PKG_PRERELEASE` | PKG |
| `CHATDBG_PKG_QUARANTINE_DIR` | PKG |
| `CHATDBG_PKG_RELOAD_PENDING` | PKG |
| `CHATDBG_PKG_SIGNATURE_POLICY` | PKG |
| `CHATDBG_PKG_SOURCE` | PKG |
| `CHATDBG_PKG_TIMEOUT` | PKG |
| `CHATDBG_PROFILE` | SET |
| `CHATDBG_PROMPT_ALLOW_EDITOR` | PROMPT |
| `CHATDBG_PROMPT_DIR` | PROMPT |
| `CHATDBG_PROMPT_EXCHANGE_DIR` | PROMPT |
| `CHATDBG_PROMPT_MAX_CHARS` | PROMPT |
| `CHATDBG_PROMPT_SEED` | PROMPT |
| `CHATDBG_PROVIDER` | CHAT, MODEL, SET |
| `CHATDBG_SETTINGS_FILE` | SET |
| `CHATDBG_SHOW_ALL_TOKENS` | SET |
| `CHATDBG_SYSTEM_PROMPT` | PROMPT |
| `CHATDBG_SYSTEM_PROMPT_BODY` | MODEL |
| `CHATDBG_SYSTEM_PROMPT_NAME` | CHAT, SET |
| `CHATDBG_SYSTEM_PROMPT_TEXT` | PROMPT |
| `CHATDBG_TEMPERATURE` | CHAT, MODEL, SET |
| `CHATDBG_TRUST_STORE` | PKG |
| `CHATDBG_USER_PACKAGE_DIR` | PKG |
| `CHATDBG_USE_OS_KEYSTORE` | MODEL |
| `CHATDBG_VERBOSE` | PKG |
| `CHATDBG_VIEW_CAPS_COLOR` | VIEW |
| `CHATDBG_VIEW_CAPS_COLUMNS` | VIEW |
| `CHATDBG_VIEW_CAPS_MOUSE` | VIEW |
| `CHATDBG_VIEW_CAPS_PLATFORM` | VIEW |
| `CHATDBG_VIEW_CAPS_REDIRECTED` | VIEW |
| `CHATDBG_VIEW_CAPS_ROWS` | VIEW |
| `CHATDBG_VIEW_CAPS_UNICODE` | VIEW |
| `CHATDBG_VIEW_CAPS_WIDECHARS` | VIEW |
| `CHATDBG_VIEW_COLOR` | VIEW |
| `CHATDBG_VIEW_LAYOUT` | VIEW |
| `CHATDBG_VIEW_MAXALT` | VIEW |
| `CHATDBG_VIEW_PALETTE` | VIEW |
| `CHATDBG_VIEW_PRECISION` | VIEW |
| `CHATDBG_VIEW_SCALE` | VIEW |
| `CHATDBG_VIEW_SHOWALL` | VIEW |
| `CHATDBG_VIEW_THEME` | VIEW |
| `CHATDBG_VIEW_UNICODE` | VIEW |
| `CHATDBG_VIEW_WIDTH` | VIEW |
| `CHAT_EXPORT_ROOT` | CHAT |
| `CHAT_IMPORT_ROOT` | CHAT |
| `CHAT_LAST_ELAPSED_MS` | CHAT |
| `CHAT_LAST_EXPORT_PATH` | CHAT |
| `CHAT_LAST_IMPORT_PATH` | CHAT |
| `CHAT_LAST_TURN_ROLE` | CHAT |
| `CHAT_SESSION` | CHAT |
| `CHAT_SESSION_ID` | CHAT |
| `CHAT_SESSION_ROOT` | CHAT |
| `CHAT_TURN_COUNT` | CHAT |
| `CLICOLOR` | VIEW |
| `CLICOLOR_FORCE` | VIEW |
| `COLORTERM` | VIEW |
| `COLUMNS` | VIEW |
| `CRED_CONFIRM` | CRED |
| `CRED_LAST_DOCTOR_VERDICT` | CRED |
| `CRED_LAST_MIGRATION_UTC` | CRED |
| `CRED_LAST_REMOVE_SLOT` | CRED |
| `CRED_LAST_ROTATE_SLOT` | CRED |
| `CRED_LAST_ROTATE_UTC` | CRED |
| `CRED_LAST_SCAN_FINDINGS` | CRED |
| `CRED_LAST_WRITE_BACKEND` | CRED |
| `CRED_LAST_WRITE_SLOT` | CRED |
| `CRED_PROFILE` | CRED |
| `CRED_REDACT_COUNT` | CRED |
| `CRED_REDACT_MINLENGTH` | CRED |
| `CRED_REDACT_MODE` | CRED |
| `CRED_REDACT_TOKEN` | CRED |
| `CRED_SETTINGS_PATH` | CRED |
| `CRED_STORE_BACKEND` | CRED |
| `CRED_STORE_ENABLED` | CRED |
| `ComSpec` | CRED |
| `ConEmuANSI` | VIEW |
| `DOCTOR_LAST_RUN` | DIAG |
| `DOCTOR_LAST_STATUS` | DIAG |
| `EDITOR` | PROMPT |
| `EXPORT_LAST_PATH` | DIAG |
| `FORCE_COLOR` | VIEW |
| `HTTPS_PROXY` | PKG |
| `LANG` | VIEW |
| `LC_ALL` | VIEW |
| `LC_CTYPE` | VIEW |
| `LINES` | VIEW |
| `LIST_FORMAT` | MODEL |
| `LOAD_EFFECTIVE_CONTEXT` | MODEL |
| `LOAD_EFFECTIVE_GPU_LAYERS` | MODEL |
| `LOAD_RESIDENT_AT` | MODEL |
| `LOAD_RESIDENT_PATH` | MODEL |
| `MAXTOKENS` | TOKEN |
| `MODELID` | TOKEN |
| `NO_COLOR` | VIEW |
| `NO_PROXY` | PKG |
| `NUGET_LOCAL_PACKAGES` | PKG |
| `PROVIDER` | TOKEN |
| `PSModulePath` | CRED |
| `RECORD_CHUNKS` | DIAG |
| `SEARCH_TAKE` | PKG |
| `SEARCH_VERBOSITY` | PKG |
| `SET_AUTOSAVE` | SET |
| `SET_AWSREGION` | CRED |
| `SET_AZUREENDPOINT` | CRED |
| `SET_BASEDIR` | SET |
| `SET_CONFIRM` | SET |
| `SET_CULTURE` | SET |
| `SET_FILE` | SET |
| `SET_LAST_PROFILE` | SET |
| `SET_MODELID` | CRED |
| `SET_PROFILE` | SET |
| `SET_PROVIDER` | CRED |
| `SET_READONLY` | SET |
| `SET_REDACT` | SET |
| `SET_STRICT` | SET |
| `SHELL` | CRED |
| `TEMPERATURE` | TOKEN |
| `TERM` | VIEW |
| `TERM_PROGRAM` | VIEW |
| `TEST_LAST_RESULT` | MODEL |
| `TEST_LAST_RUN_AT` | MODEL |
| `TOKEN_CAPTURE` | TOKEN |
| `TOKEN_CONTEXT` | TOKEN |
| `TOKEN_EXPORTDIR` | TOKEN |
| `TOKEN_GRIDMAXALT` | TOKEN |
| `TOKEN_KEEP` | TOKEN |
| `TOKEN_LAST` | TOKEN |
| `TOKEN_LASTEXPORT` | TOKEN |
| `TOKEN_LAYOUT` | TOKEN |
| `TOKEN_LOWCONF` | TOKEN |
| `TOKEN_MAXGEN` | TOKEN |
| `TOKEN_MODE` | TOKEN |
| `TOKEN_SAMPLESIZE` | TOKEN |
| `TOKEN_TOPK` | TOKEN |
| `USE_PREVIOUS` | MODEL |
| `VERIFY_LAST` | PKG |
| `VISUAL` | PROMPT |
| `WT_SESSION` | VIEW |

**188 distinct keys.** 20 are declared by more than one package and need an ownership decision: `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `CHATDBG_AWS_ACCESS_KEY`, `CHATDBG_AWS_REGION`, `CHATDBG_AWS_SECRET_KEY`, `CHATDBG_AZURE_API_KEY`, `CHATDBG_AZURE_API_VERSION`, `CHATDBG_AZURE_ENDPOINT`, `CHATDBG_LLAMA_BATCH_SIZE`, `CHATDBG_LLAMA_CONTEXT_SIZE`, `CHATDBG_LLAMA_GPU_DEVICE`, `CHATDBG_LLAMA_GPU_LAYERS`, `CHATDBG_LLAMA_THREADS`, `CHATDBG_LOGPROBS_ENABLED`, `CHATDBG_LOGPROBS_TOPK`, `CHATDBG_MAX_TOKENS`, `CHATDBG_MODEL_ID`, `CHATDBG_PROVIDER`, `CHATDBG_SYSTEM_PROMPT_NAME`, `CHATDBG_TEMPERATURE`

---

## Appendix C — Open design questions

Questions the package authors could not settle. Each needs a decision or an explicit deferral in the architecture document.


### C.1 `CHAT` — Session & Conversation

1. Environment prefix confirmation: sub-commands are dispatched through their root, so the key CommandController passes to GetChild(commandName) should be CHAT, not SEND — meaning all twelve tools share one environment bucket keyed CHAT_. The chapter states this and flags it for verification at integration; if the framework actually keys per sub-command, every GetDefaultEnvironment declaration and every prefixed read in the package changes.
2. How IConversationBackend reaches a tool when the package is crawled from the plugin directory rather than registered in-process. CommandFactory only consults an IServiceProvider when Type.GetType resolves the type, which it will not for an isolated AssemblyContext load. The chapter's answer is that SEND/RETRY degrade with a stated failure in that case, but a host that wants a crawled package to talk to a backend needs a mechanism the framework does not currently offer.
3. Whether CHAT EXPORT should keep the source's silent overwrite as the default (chosen) or flip to -noclobber-by-default. The chapter preserves silent overwrite and removes the actual harm via atomic temp-and-rename, but this is the one preserved quirk most likely to be challenged.
4. Whether restoring the imported createdAt (a deliberate correction of source quirk Q7) can break byte-for-byte round-trip expectations for anyone diffing exported files against source-produced ones.
5. Confirmation UX inside a pipeline: PromptForCommand is only defined when HasPipedInput is false, so POP -count>1, CLEAR and RETRY refuse rather than prompt. If the host later gains an out-of-band confirmation channel, that refusal should become a prompt.
6. Whether -validate should default to warn (chosen) or off (exact source behaviour). warn changes observable output for existing files containing non-whitelisted roles.
7. Session file format and whether a session file is simply an exported conversation document plus a name/lastUsed sidecar, or a distinct schema. The chapter assumes the former but does not pin it.
8. Audit masking: the framework's shipped AuditMaskingConfiguration.ApplyMasking only rewrites -name=value tokens and is effectively non-functional for space-separated syntax, so CHAT SEND's full prompt text lands in audit events verbatim. The host must supply its own IAuditMaskingConfiguration; the exact contract for that is left to the host chapter.

### C.2 `SET` — Configuration & Profiles

9. Environment prefix for sub-commands: the framework prefixes seeded environment values with '{COMMANDNAME}_', but it is not settled in ref-command.md whether that name is the root token (SET) or the sub-command (SHOW). The spec assumes the root and reads defensively (SET_FILE, then bare FILE) until the host pins it with a test.
10. Whether -format yaml ships in v1: it costs a YamlDotNet 16.3.0 dependency in a package that otherwise has none. JSON and CSV are sufficient for every composition in the chapter.
11. Profile inheritance (a 'base' field so profile B inherits A and overrides three keys) is deliberately out of v1: it turns validation into a graph problem and SET DIFF into a three-way merge.
12. Whether the framework's widened boolean grammar (1/0/yes/no/on/off) should be adopted for boolean keys, as specified, or narrowed back to the source's strict true/false. The spec adopts the framework grammar deliberately.
13. Whether a corrupt settings document should block writes until an explicit SET RESET/SET IMPORT (as specified) or be silently overwritten on the next successful write (source behaviour).
14. Whether the host should run 'SET SHOW -format json | SET VALIDATE -strict' at startup in place of the source's ad-hoc configuration self-check, and what it should do with a non-clean result.

### C.3 `CRED` — Credentials & Secret Storage

15. Generalizing the store tier off Windows contradicts a source test that asserts a vault write returns false on every non-Windows host (WindowsCredentialManagerTests.cs:18-26). The spec deliberately generalizes; that test must be rewritten as 'wincred is unavailable off Windows' rather than deleted, and the product owner must sign off on the change.
16. Ownership of the settings-file tier: CRED emits `SET CLEARCRED <slot>` request chunks rather than editing settings.json. That inter-package request protocol is asserted here but not specified anywhere - the SET package chapter must define the chunk grammar, or the two packages must agree on a shared contract assembly.
17. The no-echo prompt convention (a prompt string prefixed `secret:` is read with echo suppressed) is a HOST requirement that IIoContext cannot express - PromptForCommand takes only a string. Either the ChatDbg IO context adopts the sentinel convention, or the framework needs a PromptForSecret member.
18. Whether `encfile` should exist at all. It is the only tier where this package stores a secret itself, and on a platform with no OS keystore it is either a real improvement over plaintext or a false sense of security depending on how the key is derived. The key-derivation policy is unspecified here.
19. Whether the per-machine salt used for CRED SCAN fingerprints should be shared across machines so that two machines' scans can be compared. Sharing it makes fingerprints correlatable (useful for fleet audits) and slightly more attackable.
20. The AWS provider's fallback to the SDK's own ambient credential chain is a fourth, undocumented credential channel the rebuild inherits unless it is closed. CRED DOCTOR reports it; nobody has decided whether the provider package should disable it.
21. Whether CRED ENABLE/DISABLE should be registered with modifiesEnvironment:true so the store flag is a global variable. The spec recommends no (CRED_-prefixed keys reach the private bucket without the grant), but that makes the flag invisible to tools that do not know the bucket convention.
22. Backup entries created by CRED ROTATE -keepbackup have no expiry and no lifecycle. Nothing prunes them, which reintroduces a milder version of the source's 'stored secrets are never cleaned up' problem.

### C.4 `PROMPT` — System Prompt Library

23. Registration mode for PROMPT USE: it needs modifiesEnvironment: true to publish CHATDBG_SYSTEM_PROMPT globally, but package-directory loading registers every discovered command with modifiesEnvironment: false. Is the package always registered in-process by the composition root (Cupcake path (a)), or must the store marker file <store>/.active become the primary selection channel with the environment as a mirror?
24. Ownership of persistence for the active prompt name: this chapter has USE publish to the environment and ChatDbg.Tools.Settings persist systemPromptName. That requires Settings to observe an environment change or to be invoked as a later pipeline stage (PROMPT USE | SET SAVE). Confirm with the Settings chapter which mechanism is contractual.
25. Does the host's IIoContext implementation propagate an output encoder and branch on ResultFormat? The framework never applies IOutputEncoder itself, so PROMPT LIST -format csv/json is metadata only unless the shell's HandleOutputChunk renders on it.
26. The argument tokenizer strips ':' '/' '\' ';' '<' '>' '{' '}' '+' '=' ',' even inside quotes. This chapter routes all paths and all real content around it (basenames, pipe, -file, interactive prompt). If the host is permitted to fork or wrap NamesValidator to preserve quoted spans verbatim, several parameters (-text, -description, real paths) could be restored — is that fork acceptable?
27. Backup policy: one generation in <store>/.backup is specified. Should PROMPT DOCTOR be able to restore from it (a RESTORE verb), or is that deliberately left to the exchange directory plus real version control?
28. Should the exchange directory default to the documents folder (source's command-path default) or to a dedicated ChatDbg subdirectory? The chapter keeps the documents folder for continuity, but a dedicated directory would make confinement easier to explain and to lock down.
29. Culture-sensitive collation is the default to match the source exactly, but the source's size-optimised publish profiles disabled globalization and silently changed it. Should the shipped release pin -collation ordinal by default and keep culture as opt-in?
30. PROMPT EDIT -editor spawns a process, the only such capability in the package. Confirm whether the restricted-host profile disables it by default (CHATDBG_PROMPT_ALLOW_EDITOR=false) rather than enabling it by default as specified here.

### C.5 `MODEL` — Model Backends

31. Argument tokenization strips ':' '/' '\\' and '=' even inside quoted spans (NamesValidator.InvalidParameterChars), so no filesystem path, endpoint URL, or Bedrock model id such as 'anthropic.claude-3-sonnet-20240229-v1:0' can survive the command line. The chapter routes around it with catalog handles, piped values, and a -revision parameter — but if the framework ever relaxes the filter, MODEL SELECT and MODEL LOAD should take raw values directly and the workaround should be retired.
32. GetDefaultEnvironment() values are seeded under the SUB-COMMAND name prefix (LOAD_, TEST_, CATALOG_), not the root, so private-bucket keys can collide with an identically-named sub-command in another package. Confirm whether the host should namespace buckets, or whether this package should abandon the private bucket entirely and keep its breadcrumbs in the process singleton.
33. Should the local satellite assembly be run out of process by default? An access violation inside the native engine is uncatchable and kills the shell with the user's unsaved conversation; a supervised subprocess isolates it but loses raw logits, prompt-token logits and custom sampling stages.
34. The source clamped the request-side top-K to 1-20 AND used the same setting as the local sampler's top-K, so local generation was always top-5 limited out of the box even with introspection off. Decide whether MODEL TUNE should expose a separate sampler top-K, or whether the coupling stays (and is merely disclosed by MODEL CAPS).
35. Fabricated log-probabilities are removed. Confirm with the product owner that the demo affordance the fabrication served is fully covered by an explicit demo command elsewhere, since MODEL CAPS will now report 'logprobs: no' for Bedrock where the source silently showed a populated visualisation.
36. Bedrock family detection is widened from the source's bare 'anthropic.' prefix to '^(?:[a-z]{2}\\.)?anthropic\\.' so cross-region inference profiles match. Confirm no other geography prefix shape is in use before pinning that regex.
37. The generation and model-load locks are process-wide, unbounded and un-cancellable in the source. This chapter specifies a bounded wait plus a status message; the timeout value is not yet chosen.
38. Catalog TTLs (15 min cloud, 60 s local) and the handle-stability guarantee are asserted, not derived from any source behaviour; they need a product decision.
39. MODEL CAPS -probe on a cloud backend can cost a billable completion. Confirm whether the default should stay 'declared only' or whether a host-level policy should forbid -probe entirely in shared/CI contexts.

### C.6 `TOKEN` — Token Introspection

40. Sibling package names and root commands are assumed, not confirmed: ChatDbg.Tools.Providers (AI), ChatDbg.Tools.Rendering (RENDER), ChatDbg.Tools.Settings (CONFIG), ChatDbg.Tools.History (CHAT), ChatDbg.Tools.Diagnostics (LOG). If the other chapters chose different names or roots, §6.1 and the compositions in §6.4 need to be reconciled.
41. How does a plugin-loaded command reach host services? Xcaciv.Command's CommandFactory activates ALC-loaded plugin commands via a public parameterless constructor with no DI injection, so I specified a shared ChatDbg.Introspection.Abstractions assembly in the default load context plus a static IntrospectionServices registry. The architect should confirm this is the product-wide pattern rather than a per-package invention.
42. Environment seeding for sub-commands is unverified: CommandController.Run derives the command name from the first token, so a child environment is built as GetChild("TOKEN") and only the TOKEN bucket is copied, while CommandRegistry.GetEnvironment seeds per-registered-command buckets (and would try to instantiate the synthetic root, whose FullTypeName is empty). Every tool therefore supplies its own in-code fallback defaults; whether the framework actually seeds anything for sub-commands should be tested against 3.3.4.
43. Command-line file paths are unusable in this framework (the argument tokenizer strips backslash, forward slash, colon, equals inside quotes as well as outside), so EXPORT/LOAD take a filename stem resolved against TOKEN_EXPORTDIR. If the product needs arbitrary destinations, the host must add a path-alias mechanism — this is a product-wide decision, not a package-local one.
44. Whether the settings-dialog clamp semantics survive: the source rejected out-of-range top-K from commands but silently clamped in the GUI settings dialog (1-20). This chapter rejects everywhere; if the rebuilt GUI reintroduces a dialog, the divergence must be resolved in one direction.
45. Whether the source's verbatim user-visible strings should be preserved character-for-character where they contain defects, e.g. 'Error: Response text expected, none recieved.' (misspelling) and 'Unknown subcommand: {arg}. ' (trailing space). This chapter preserves the trailing space and drops the misspelling by not carrying that string; the product needs one policy.
46. Whether the truncated top-K entropy should be reported at all, given it is only a lower bound on the true distribution's entropy. The alternative is to report it only when the backend can supply full-vocabulary logits. Currently it is always reported with entropyIsTruncated:true.
47. Attribution beyond the recency heuristic: -method attention and -method gradient are declared and refuse when unavailable, but no backend contract for attention export or gradient attribution is specified here. Whoever owns ChatDbg.Tools.Providers must decide whether IAttributionModel is real or the two methods should be dropped from the allow-list.
48. Whether TOKEN INSPECT should exist at all given SPLIT + MAP + ATTRIBUTE compose to the same thing. It is kept for source parity and for the single-model-load optimisation, but it is the one tool in the catalog that is pure convenience.
49. Analysis retention: TOKEN_KEEP defaults to 8 in-memory analyses. Whether analyses should instead be written through to TOKEN_EXPORTDIR automatically (making the ring a cache rather than the store) is undecided and affects TOKEN DIFF's ergonomics.

### C.7 `VIEW` — Presentation & Visualization

50. Chunk granularity for VIEW TOKENS output: one chunk per rendered line, per grid row, or one chunk for the whole rendering? Per-line streams best through the bounded channel but means a downstream stage sees a table in pieces; the chapter specifies per-line (list) and per-row (grid), which should be confirmed against the host's terminal sink.
51. VIEW SAMPLE, VIEW TOKENS (grid) and VIEW HEAT (sampling) must buffer, which defeats the framework's lazy streaming for long responses. Is the 100000-chunk default ceiling right, and should the host lower it via PipelineConfiguration.MaxChannelQueueSize instead?
52. The framework prefixes per-command environment keys with the COMMAND name, not the root, so a sub-command named LAYOUT or STATUS would collide across packages. The chapter routes around this with fully-qualified CHATDBG_VIEW_* globals and modifiesEnvironment:true registrations — confirm the host is willing to grant that permission to four tools.
53. Invoking the bare root 'VIEW' throws InvalidOperationException from CommandFactory and the async path is less forgiving than the sync one on an unknown sub-command. The host must catch and render usage; no tool can absorb it. Confirm where that mapping lives.
54. Should the default palette really change from the source's bands5 to bands10? The chapter argues yes (bands5 fed unit-scale values rendered every real token red), but it is a visible behavioural change and needs an explicit sign-off.
55. Should the plain-table alternatives cap of 2 and the styled-table cap of 3 be preserved as per-surface compatibility defaults rather than unified on the setting? The chapter unifies; a -compat flag was considered and dropped.
56. Should the left-justified rule stay 79 characters while the centred rule is 80? Preserved as the source's asymmetry, but it looks like a defect to anyone reading the output.
57. Precision default is 5 (matching both table renderers) while the shared value formatter used 2. Which should a fresh installation see?
58. Who owns enabling virtual-terminal processing on legacy Windows consoles — the chapter assigns it to the host at start-up and has VIEW CAPS only observe the result. Confirm.
59. Does the full-screen host consume VIEW TOKENS output as text into its probability panel, or does it need a structured intermediate shape? The panel's 50-column default, max(longest+5,50) sizing and 0-based numbering are host constants that the renderer's 1-based output must be reconciled with.
60. Should VIEW TRANSCRIPT offer any redaction hook? It renders message content verbatim, including anything a user pasted, and a host that logs its output is logging conversation content.

### C.8 `DIAG` — Diagnostics & Observability

61. Daily file-name pattern: the chapter renames the default from 'llamasharp_yyyyMMdd.log' to 'chatdbg_yyyyMMdd.log' because the rebuilt recorder captures all three backends, and keeps read-compatibility with the legacy name. If the support workflow's documented artefact name is treated as a contract, this must be reverted.
62. Whether DIAG RECORD's suffix message parameter should be blocked entirely for interactive use, given that the framework's audit masking cannot mask space-separated parameters. The chapter mitigates by recording only length+hash in audit metadata, but a stricter reading argues for a prompt-based input path instead.
63. Whether the recorder should own or merely borrow the process-global native log-sink hook when the binding offers no unregister. The chapter specifies swapping to a no-op sentinel on disposal; the underlying binding's actual capability was not verified.
64. Whether DIAG DOCTOR's read-only ports into settings and credential provenance should be host-supplied interfaces or a formal cross-package query contract. The chapter chose host-supplied ports to keep the tool -> SDK dependency arrow, at the cost of check groups reporting 'skipped (no provider)' in a bare host.
65. Whether the DIAG tools that need shared configuration should use global environment variables (requiring modifiesEnvironment: true on CAPTURE/ROTATE/AUDIT) or a host-injected policy object. The chapter chose globals to stay inside the framework's documented propagation rules; a policy object would be simpler but less inspectable via ENV.
66. Whether -probe native's out-of-process helper is acceptable in a restricted host, or whether it should be a separate opt-in package so the base DIAG package never spawns a process.
67. Retention defaults (14 days, 256 MiB total, 64 MiB per file) are invented -- the source had no retention at all and the dossier records 'could not determine whether the daily file was ever intended to be pruned'. These need a product decision.
68. Whether DIAG DOCTOR should also fail (not warn) when maxTokens is inert on the Azure non-logprobs path and when llamaGpuDevice/llamaThreads/llamaBatchSize reach no runtime, or whether those source quirks should be fixed in the owning packages instead of merely reported here.

### C.9 `PKG` — Tool Package Management

69. Who owns the operator identity recorded in a trust grant? The host knows only an OS user name; a shared machine or a service account makes 'who granted this' ambiguous, and the chapter assumes the host can supply something better than the process account.
70. Should PKG RELOAD attempt a real collectible-ALC unload at all? The chapter deliberately never claims one (unload is terminal, returns four indistinguishable falses, and CommandExecutor does not dispose command instances). If the host later holds every plugin reference behind a single draining wrapper, a genuine hot-swap becomes possible and PKG RELOAD's contract would change.
71. Package signature verification on Linux has no platform code-signature analogue; the design drops the policy from require to prefer with a warning. Is a detached-signature scheme over the package manifest (operator-supplied trusted keys) worth specifying, or does that duplicate what the feed's own package signing already provides?
72. -deps allow acquires a transitive closure, but Xcaciv.Loader does not confine dependency loads to basePathRestriction. Should PKG instead flatten and rewrite deps.json at install time, or refuse any package whose deps.json probes outside its own directory?
73. Whether PKG should be able to install into the system package root at all under an explicit elevated-install mode, or whether that must remain entirely out-of-band deployment tooling as specified here.
74. The lockfile records feed names, but a name is machine-local. A shared lockfile referencing feed 'corp' restores only if the receiving machine has a feed by that name; embedding the URL instead would make the lockfile a redirection vector. The chapter chose names - this trade-off is worth a second opinion.

**74 open design questions.**
