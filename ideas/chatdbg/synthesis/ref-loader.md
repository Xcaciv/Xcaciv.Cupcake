# Xcaciv.Loader — Reference for Secure Dynamic Assembly / Plugin Loading

**Purpose of this document:** a precise, evidence-backed reference for a host application that will load
tool packages (plugins) through `Xcaciv.Loader`. Every claim is anchored to `file:line`.

**Source repo (read-only, analyzed at commit `9aa730c` "Add Securability badge to README"):**
`refs/Xcaciv.Loader`. All paths below are repo-relative.

**Reading key:** ✅ = verified behavior in source · ⚠️ = gap, gotcha, or residual risk the host must handle ·
📄 = documentation claim that source does not fully support.

---

## 1. Version, target frameworks, multi-framework story

### 1.1 Version

| Fact | Value | Evidence |
|---|---|---|
| NuGet package version | **2.1.2** | `src/Xcaciv.Loader/Xcaciv.Loader.csproj:8` |
| `FileVersion` / `AssemblyVersion` | **2.1.1.0** | `src/Xcaciv.Loader/Xcaciv.Loader.csproj:9-10` |
| PackageId / AssemblyName / RootNamespace | `Xcaciv.Loader` | `Xcaciv.Loader.csproj:16-17,22` |
| **License** | **AGPL-3.0-only, `PackageRequireLicenseAcceptance=True`** | `Xcaciv.Loader.csproj:34-35` |
| Last CHANGELOG release entry | `[2.1.0] - 2025-12-22`; 2.1.1/2.1.2 are **unreleased/undocumented** | `CHANGELOG.md:48`; `CHANGELOG.md:8` `[Unreleased]` |

⚠️ **License is the single biggest adoption decision.** AGPL-3.0-only is strong network copyleft. A host
application that links this library and is exposed over a network may be required to offer its own source.
Resolve this before designing around the library. The release notes' claim of a permissive posture is not
present; `Xcaciv.Loader.csproj:34` is authoritative.

⚠️ `SECURITY.md:5` states verbatim: **"This library is not supported."** Vulnerability reporting is
"Use GitHub" (`SECURITY.md:9`). There is no security SLA. Treat the library as vendored code you own.

⚠️ Package version 2.1.2 ships with `AssemblyVersion` 2.1.1.0 and no CHANGELOG entry — binding redirects and
provenance auditing will see a version skew (`Xcaciv.Loader.csproj:8-10` vs `CHANGELOG.md:48`).

⚠️ `README.md:3` embeds a "Securability" badge pointing at `https://localhost:3001/...` — a placeholder that
will never render. Cosmetic, but a signal of release hygiene.

### 1.2 Target frameworks

```xml
<TargetFrameworks Condition="'$(UseNet10)' == 'true'">net8.0;net10.0</TargetFrameworks>
<TargetFrameworks Condition="'$(UseNet10)' != 'true'">net8.0</TargetFrameworks>
```
`src/Xcaciv.Loader/Xcaciv.Loader.csproj:4-5`

- **Default build produces a net8.0-only package.** Multi-targeting (`net8.0;net10.0`) requires opting in with
  `/p:UseNet10=true` (`Xcaciv.Loader.csproj:4`, `build.ps1:52-57`, `docs/multi-framework.md:31-44`).
- Test project and test assemblies use a *different* pattern — a single `TargetFramework` that is **switched**,
  not multiplied: `net8.0` unless `UseNet10=true`, then `net10.0`
  (`src/Xcaciv.LoaderTests/Xcaciv.LoaderTests.csproj:5-8`, `src/TestAssembly/zTestAssembly.csproj:4-7`).
  📄 `docs/multi-framework.md:51-58` documents only this switch pattern; it is stale with respect to the
  library's actual multi-targeting at `Xcaciv.Loader.csproj:4-5`.
- Build script: `build.ps1:4` (`-UseNet10` switch), `build.ps1:52-57` (emits `dotnet build /p:UseNet10=true`).

**Implication for a net10 host:** if you consume the public NuGet package, you almost certainly get the
**net8.0** asset running on the net10 runtime. That is supported, but you inherit net8.0 compilation
semantics. If you need a genuine net10.0 asset, build from source with `/p:UseNet10=true` or vendor it.

### 1.3 Dependencies and build posture

- Central Package Management is on: `src/Directory.Packages.props:3`.
- The library's **only** `PackageReference` is `Microsoft.SourceLink.GitHub` with `PrivateAssets="All"`
  (`Xcaciv.Loader.csproj:103`). `System.Reflection.Metadata` 8.0.0 is *declared* in
  `src/Directory.Packages.props:8` but not referenced by the library — on net8.0+ it comes from the shared
  framework. **Runtime dependency footprint: zero third-party packages.** This matches
  `docs/spec-architecture-dynamic-assembly-loading.md` CON-002 ("No external dependencies beyond the .NET runtime").
- `Nullable` and `ImplicitUsings` enabled; `TreatWarningsAsErrors` and `IsTrimmable` in both Debug and Release
  (`Xcaciv.Loader.csproj:13-14,85-94`).
- Trimming/AOT: the class is annotated `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]`
  (`src/Xcaciv.Loader/AssemblyContext.cs:20`) and IL2026/IL2067/IL2072 warnings are suppressed wholesale in
  `src/Xcaciv.Loader/GlobalSuppressions.cs:11-26`. ⚠️ The suppressions are *silencing*, not *solving*: dynamic
  loading is fundamentally incompatible with aggressive trimming. Do not trim a host that loads plugins.
- `src/Xcaciv.Loader/GlobalSuppressions.cs:9` grants `InternalsVisibleTo("Xcaciv.LoaderTests")` — this is how
  the internal `AssemblyPreflightAnalyzer` is testable.

---

## 2. The `IAssemblyContext` / `AssemblyContext` contract

### 2.1 The interface surface (what you can program against)

`src/Xcaciv.Loader/IAssemblyContext.cs:6` — `public interface IAssemblyContext : IDisposable, IAsyncDisposable`

| Member | Line | Notes |
|---|---|---|
| `string FilePath { get; }` | `:11` | Resolved absolute path |
| `string FullAssemblyName { get; }` | `:16` | Empty until loaded |
| `object? CreateInstance(string className)` | `:24` | Loose, returns null on miss |
| `T CreateInstance<T>(string className)` | `:32` | Throws on miss |
| `T CreateInstance<T>(Type classType)` | `:41` | |
| `IEnumerable<Type>? GetTypes()` | `:47` | |
| `IEnumerable<Type>? GetTypes(Type baseType)` | `:54` | Concrete types only |
| `IEnumerable<Type> GetTypes<T>()` | `:60` | Concrete types only |
| `Version GetVersion()` | `:66` | |
| `bool Unload()` | `:73` | |
| `Task<bool> UnloadAsync()` | `:79` | |
| `void EnableGlobalDynamicAssemblyMonitoring()` | `:92` | Audit-only, documented as such at `:87-91` |

⚠️ **The interface exposes no security surface.** `SecurityPolicy`, `IntegrityVerifier`, `LoadTimeout`,
`BasePathRestriction`, and **all six events** are declared on the concrete `AssemblyContext`, not on
`IAssemblyContext` (`AssemblyContext.cs:66,90,116,148,171,200,210,221,260,276`). A host that stores plugins as
`IAssemblyContext` **cannot subscribe to the audit trail or inspect policy**. Design consequence: either
(a) hold the concrete `AssemblyContext` inside your plugin-host abstraction and wire events at construction,
or (b) define your own host-side interface that carries the events you need. Do not build your plugin registry
on the bare `IAssemblyContext`.

### 2.2 Creation

Two constructors:

**Path-based** — `AssemblyContext.cs:399-420`
```csharp
public AssemblyContext(
    string filePath,
    string? fullName = null,
    bool isCollectible = true,          // :402  collectible by default
    string basePathRestriction = ".",   // :403  DEFAULT IS "." (current dir), NOT wildcard
    AssemblySecurityPolicy? securityPolicy = null,      // :404  -> Default policy (:409)
    AssemblyIntegrityVerifier? integrityVerifier = null) // :405 -> null == disabled (:410)
```
Construction order (`:407-419`):
1. `ArgumentException.ThrowIfNullOrWhiteSpace(filePath)` — `:407`
2. policy defaults to `AssemblySecurityPolicy.Default` — `:409`
3. `FilePath = VerifyPath(filePath, BasePathRestriction, SecurityPolicy)` — `:412` — **all path security
   happens here, at construction time, before any I/O of the assembly.**
4. `SetLoadContext(...)` creates the `AssemblyLoadContext` — `:413`, `:466-471`
5. If `basePathRestriction == "*"`, raise `WildcardPathRestrictionUsed` — `:416-419`

⚠️ Step 5 is **unobservable**: the event is raised inside the constructor, before the caller can subscribe.
This is confirmed by the test `Constructor_WildcardRestriction_EventFiresDuringConstruction`
(`src/Xcaciv.LoaderTests/SecurityViolationTests.cs:173`) and by
`EventTests.Constructor_ForbiddenPath_ThrowsBeforeEventCanBeSubscribed`
(`src/Xcaciv.LoaderTests/EventTests.cs:258`). The host must therefore enforce "no wildcard" **itself**, in its
own code, before calling the constructor. Do not rely on the event.

**Name-based** — `AssemblyContext.cs:444-459`
```csharp
public AssemblyContext(AssemblyName assemblyName, bool isCollectible = true,
                       string basePathRestriction = ".", ...)
```
Sets `FilePath = String.Empty` (`:453`) and defers to `LoadFromAssemblyName` (`:811`).

⚠️ **The name-based constructor is effectively broken for instantiation.** Both `CreateInstance(string)`
(`:900-905`) and `CreateInstance<T>(string)` (`:959-964`) begin with `if (!File.Exists(this.FilePath))
{ ... throw new FileNotFoundException(...); }`. With `FilePath == ""`, `File.Exists("")` is `false`, so **every**
`CreateInstance` call on a name-based context throws `FileNotFoundException` before the assembly is ever
consulted. Only `GetTypes()` / `GetTypes<T>()` / `GetVersion()` work on a name-based context (they route through
`LoadAssembly()` at `:852` without the file check). **Host guidance: always use the path-based constructor.**

⚠️ Name-based loads also cannot be preflighted — `docs/security-features-v2.md:38` states this explicitly, and
the code confirms it: `LoadFromName` (`:799-841`) performs **no** preflight and **no** integrity verification;
it only enforces `DisallowDynamicAssemblies` (`:815-819`). A second reason to avoid it.

### 2.3 Loading and type resolution

- `LoadAssembly()` (`:852-883`) is the single funnel and memoizes: returns cached `assembly` (`:857-860`), else
  if `isLoaded` re-finds it in `loadContext.Assemblies` (`:861-866`), else dispatches to `LoadFromPath()`
  (`:868-871`) or `LoadFromName()` (`:872-875`), else `InvalidOperationException` (`:878`).
- Loading is **lazy** — nothing is read from disk at construction beyond `VerifyPath`'s checks. The first
  `CreateInstance`/`GetTypes`/`GetVersion` triggers the load.
- **Dependency resolution** — `LoadContext_Resolving` (`:565-585`), hooked at `:469`. Two strategies, in order:
  1. `AssemblyDependencyResolver` over the plugin's own directory (`:569-575`) — reads `*.deps.json`.
  2. Naive fallback: `<plugin dir>/<simple name>.dll` (`:577-582`).
  Each success raises `DependencyResolved` (`:573`, `:580`) then routes through the private
  `LoadFromPath(context, path)` (`:587-632`).

**Type resolution — two different algorithms, and the difference is security-relevant:**

| API | Search scope | Match rule | Miss behavior |
|---|---|---|---|
| `CreateInstance(string)` `:894-941` | **All assemblies in the load context**, dependencies included: `loadContext.Assemblies.SelectMany(o => o.GetTypes())` (`:917`) | `t.FullName.EndsWith(className)` after prefixing `'.'` if the name is unqualified (`:915`) | returns `null` (`:919`) |
| `CreateInstance<T>(string)` `:953-1005` | **Only the primary assembly**: `assembly.GetTypes()` (`:976`) | same suffix rule (`:968`) | throws `TypeNotFoundException` (`:980`) |
| `CreateInstance<T>(Type)` `:1017-1043` | caller-supplied `Type` | — | `ArgumentNullException` (`:1021`) |

⚠️ **Suffix matching + first-match-wins is a type-confusion hazard.** Asking for `"Plugin"` matches any type
whose full name ends in `.Plugin`, in *declaration/enumeration order*. A malicious or careless plugin that ships
a dependency defining `Anything.Plugin` can win the match — especially through the non-generic
`CreateInstance(string)`, which searches dependencies too. **Host guidance: always pass a fully-qualified type
name, and prefer discovery via `GetTypes<TContract>()` (`:1094-1098`) over name lookup.** `GetTypes<T>` filters
by `typeof(T).IsAssignableFrom(o) && !o.IsInterface && !o.IsAbstract`, which is contract-driven and unambiguous.

- Activation is `Activator.CreateInstance(instanceType)` via `ActivateInstance<T>` (`:1053-1064`) — **parameterless
  constructor only**. There is no DI, no constructor injection, no factory hook. Plugins must expose a public
  parameterless ctor; inject dependencies through an `Initialize`-style method on your own contract.

### 2.4 Unloading and collectibility

- `SetLoadContext` (`:466-471`) creates `new AssemblyLoadContext(fullName, isCollectible)` — collectible by
  default via the ctor's `isCollectible = true` (`:402`, `:446`).
- `Unload()` (`:1229-1266`), under `lock (syncLock)` (`:1233`):
  - returns `false` if disposed (`:1231`), if `loadContext is null` (`:1235-1236`), if **not collectible**
    (`:1238-1239`), or if **never loaded** (`:1241-1242`) — four silent `false` returns, none distinguishable.
  - on success: nulls `assembly`, **nulls `loadContext`**, calls `context.Unload()`, clears `isLoaded`,
    raises `AssemblyUnloaded(path, true)` (`:1246-1255`).
  - on exception: raises `AssemblyUnloaded(path, false)` then wraps in `InvalidOperationException` (`:1257-1264`).
- ⚠️ **Unload is terminal for the context object.** Because `loadContext` is set to `null` (`:1248`), any later
  call routes through `ValidateLoadContext()` (`:638-644`) and throws
  `InvalidOperationException("Load context is not set...")`. There is **no reload**. To reload a plugin, dispose
  the old context and construct a new one.
- ⚠️ **`Unload()` returning `true` does not mean the assembly left memory.** `AssemblyLoadContext.Unload()` is a
  request; actual collection requires every reference (instances, types, delegates, static roots) to be
  unreachable and a GC to run. The library's own tests force `GC.Collect(); GC.WaitForPendingFinalizers();` to
  demonstrate unload (`src/Xcaciv.LoaderTests/AssemblyContextTests.cs:109-110`). The spec acknowledges this:
  "Unloading assemblies that have static references: May not fully unload"
  (`docs/spec-architecture-dynamic-assembly-loading.md`, Edge Cases).
- `UnloadAsync()` (`:1328-1376`) wraps the *same* synchronous body in `Task.Run(..., disposalTokenSource.Token)`
  (`:1336`, `:1370`) and swallows `OperationCanceledException` → `false` (`:1372-1375`). The XML docs are honest
  that this only avoids blocking the caller (`:1276-1282`).

### 2.5 Lifetime and disposal

- `Dispose(bool)` (`:1382-1401`): under `lock (syncLock)` calls `Unload()`, cancels + disposes
  `disposalTokenSource`, and `RemoveGlobalSubscriber(this)` (`:1389-1396`), then sets `disposed = true` (`:1400`).
- `Dispose()` (`:1406-1410`) → `Dispose(true)` + `SuppressFinalize`.
- `DisposeAsync()` (`:1416-1451`): `await UnloadAsync()` with **exceptions swallowed** (`:1421-1428`), then
  cancels/disposes the CTS under lock, tolerating a concurrent `ObjectDisposedException` (`:1431-1447`).
  ⚠️ `DisposeAsync` does **not** call `RemoveGlobalSubscriber` — only `Dispose(bool)` does (`:1395`). A context
  disposed via `DisposeAsync` stays in the global monitor's list until its `WeakReference` is collected and the
  handler prunes it (`:512-516`). Harmless (weak ref, audit-only) but worth knowing.
- Finalizer `~AssemblyContext()` (`:1456-1459`) calls `Dispose(false)`, which **skips** unload and the CTS —
  it only flips `disposed`. Finalization is not a cleanup path; **always dispose deterministically**.
- `ThrowIfDisposed()` (`:650-656`) guards `CreateInstance*`, `GetTypes*`, `GetVersion`,
  `EnableGlobalDynamicAssemblyMonitoring` (`:480`, `:667`, `:801`, `:854`, `:896`, `:955`, `:1019`, `:1073`,
  `:1085`, `:1096`, `:1122`) and throws `ObjectDisposedException`.
- Canonical pattern (`README.md:8-12`, `src/Xcaciv.Loader/readme.md:13-19`): `using` scope == plugin lifetime.

### 2.6 Load timeout

`LoadTimeout` (`:260`) defaults to **30 seconds**; `Timeout.InfiniteTimeSpan` disables it (`:704-707`).
When enabled (`:709-747`) the load runs on `Task.Run` with a linked CTS
(`:712-719`) and `loadTask.Wait(LoadTimeout, cts.Token)` (`:721`); expiry throws `TimeoutException` (`:723-724`,
`:729-741`); `AggregateException` is unwrapped (`:742-746`).

⚠️ **The timeout unblocks the caller; it does not abort the load.** `AssemblyLoadContext.LoadFromAssemblyPath`
is uninterruptible — the thread-pool thread keeps going and may complete the load (and run module initializers)
after you have already thrown. Treat `TimeoutException` as "this context is now in an unknown state": dispose it
and do not reuse it.

⚠️ It is `init`-only (`:260`), so it must be set with an object initializer at construction (`:246-256`).

### 2.7 Thread safety — the honest picture

**What is actually synchronized:**
- `syncLock` (`:36`) guards **only** `Unload()` (`:1233`), the body inside `UnloadAsync()`'s task (`:1338`),
  `Dispose(bool)` (`:1389`), and the CTS teardown in `DisposeAsync()` (`:1431`).
- `globalMonitorLock` (`:24`) guards the static global-monitor subscriber list (`:481`, `:508`, `:541`).
- `AssemblyHashStore` has its own `lockObject` and locks every operation
  (`src/Xcaciv.Loader/AssemblyHashStore.cs:31,64,90,112,123,159,228,247`).
- Security policy is **immutable and instance-based** (`init`-only properties,
  `AssemblySecurityPolicy.cs:68,74,83`) — this is the v2 change that removed static mutable state and made
  parallel use safe (`CHANGELOG.md:115-118`).

**What is NOT synchronized:**
⚠️ The **entire load path is lock-free**: `LoadAssembly()` (`:852`), `LoadFromPath()` (`:665`),
`LoadFromName()` (`:799`), and both `CreateInstance(string)` overloads mutate the non-volatile fields
`assembly` (`:306`), `assemblyName` (`:286`), and `isLoaded` (`:301`) with no lock and no memory barrier
(`:757-758`, `:821`, `:864`, `:870-882`). Two threads calling `CreateInstance` on the *same* context for the
first time can both enter `LoadFromPath()`, both call `LoadFromAssemblyPath`, and race on the field writes.
⚠️ `disposed` (`:31`) is a plain `bool`, read outside the lock in `Unload` (`:1231`), `UnloadAsync` (`:1330`),
and `ThrowIfDisposed` (`:652`) — visibility across threads is not guaranteed.

**What the tests actually prove** (`src/Xcaciv.LoaderTests/ThreadSafetyTests.cs`): concurrent operations do not
deadlock and do not observably corrupt in practice — `ConcurrentLoad_MultipleContexts_ThreadSafe:39`,
`ConcurrentLoad_SameAssembly_NoRaceConditions:79`, `ConcurrentUnload_MultipleContexts_NoDeadlock:160`,
`LoadUnload_ConcurrentOperations_NoDeadlock:191`, `CreateInstance_ConcurrentCalls_SameContext_ThreadSafe:331`,
`Dispose_WhileOperationsInProgress_ThreadSafe:383`, `DisposeAsync_Concurrent_OnlyDisposesOnce:422`,
`GetTypes_ConcurrentCalls_ThreadSafe:446`, `VerifyPath_ConcurrentCalls_ThreadSafe:504`,
`StressTest_ManyOperations_Concurrent:571`. Note their assertion strength: several assert only
`Assert.True(true)` (`:278`, `:436`) or a tautology (`:213` `Assert.True(loadCompleted || !loadCompleted)`) —
they demonstrate *absence of deadlock*, not *absence of races*.
📄 `docs/ssem-scoring-methodology.md:318` scores Availability 9/10 on "excellent thread safety"; the code
supports "safe across contexts", not "safe within one context".

**Host rule:** treat one `AssemblyContext` as owned by one logical plugin and **serialize its first load**
(e.g. `Lazy<T>` with `LazyThreadSafetyMode.ExecutionAndPublication`, or your own lock in the plugin wrapper).
Different contexts in parallel are genuinely fine — that is what the instance-based policy design bought.

---

## 3. The security model in full

`docs/security-features-v2.md:7` frames it as defense in depth; `docs/V2.0-Release Notes.md:248-281` enumerates
five layers. Mapped to code:

| Layer | Mechanism | Code |
|---|---|---|
| 1. Base path restriction | `basePathRestriction` confinement | `AssemblyContext.cs:1192-1196` |
| 2. Security policy | forbidden directories + extension + dynamic-assembly flag | `AssemblySecurityPolicy.cs`; `AssemblyContext.cs:1159-1171,752-756` |
| 3. Integrity verification (opt-in) | SHA-256/384/512 vs. hash store | `AssemblyIntegrityVerifier.cs:121-165` |
| 4. Input validation (opt-in, caller-invoked) | `AssemblyPathValidator` | `AssemblyPathValidator.cs` |
| 5. Audit events | six events | `AssemblyContext.cs:66,90,116,148,171,200` |
| (2.1 addition) Preflight metadata scan | Reflection.Emit / Expressions.Compile detection | `AssemblyPreflightAnalyzer.cs:25-121`, gated at `AssemblyContext.cs:685-696,595-606` |

### 3.1 Instance-based policies — and what they replaced

`AssemblySecurityPolicy` (`src/Xcaciv.Loader/AssemblySecurityPolicy.cs:18`) is an **immutable value object**
with `init`-only members: `StrictMode` (`:68`), `DisallowDynamicAssemblies` (`:74`),
`ForbiddenDirectories` (`:83`).

Three ways to obtain one:
- `AssemblySecurityPolicy.Default` (`:48`) — forbidden set = `["grouppolicy", "systemprofile"]` (`:21-25`);
  `DisallowDynamicAssemblies = false` (`:108` with `strictMode=false`).
- `AssemblySecurityPolicy.Strict` (`:63`) — forbidden set adds `windows, system32, programfiles,
  programfiles(x86), programdata, winevt\logs, credentials, windows defender,
  appdata\local\microsoft\credentials` (`:28-35`); **`DisallowDynamicAssemblies = true`** (`:108`), which also
  **turns on preflight** (`AssemblyContext.cs:685`, `:595`).
- `new AssemblySecurityPolicy(string[] forbiddenDirectories)` (`:131-138`) — custom list;
  ⚠️ this ctor forces `StrictMode = false` **and** `DisallowDynamicAssemblies = false` (`:135-136`), so a custom
  policy silently loses dynamic-assembly blocking and preflight unless you re-enable it with an object
  initializer: `new AssemblySecurityPolicy(dirs) { DisallowDynamicAssemblies = true }`
  (pattern shown at `docs/security-features-v2.md:114-118`).

**What replaced the v1 model.** v1 configured security through **process-global static state**:
`AssemblyContext.SetStrictDirectoryRestriction(bool)` and `IsStrictDirectoryRestrictionEnabled()`. Both survive
as `[Obsolete]` **no-ops**: the setter only writes a `Debug.WriteLine` (`AssemblyContext.cs:318-325`) and the
getter **unconditionally returns `false`** (`AssemblyContext.cs:335-336`). Removal is planned for v3.0.0 /
2026-06-01 (`CHANGELOG.md:136-140`). The replacement is the per-instance `securityPolicy` constructor parameter
(`AssemblyContext.cs:404`, `:409`), migration shown at `CHANGELOG.md:147-165`. Stated benefits:
parallel-test safety, per-context policies, no static race
(`CHANGELOG.md:115-118`, `:239-243`).

⚠️ **Migration trap:** code that still calls `SetStrictDirectoryRestriction(true)` **compiles, emits only an
obsolescence warning, and silently runs with `Default` policy.** Grep any ported code for both symbols and
delete them. `IsStrictDirectoryRestrictionEnabled()` returning a hardcoded `false` will also silently break any
host logic that branches on it.

**On the wildcard `"*"`:** it was not removed — it was demoted and instrumented.
- The constructor default is `"."` (current directory), i.e. restricted (`AssemblyContext.cs:403`, `:447`).
- The **static** `VerifyPath` still defaults to `"*"` (`:1139`) — so any host code that calls
  `AssemblyContext.VerifyPath(path)` with one argument gets **no base-path confinement at all**, only the
  extension and forbidden-directory checks. ⚠️ Easy to get wrong; always pass the restriction explicitly.
- `"*"` sets the effective base to the file's own directory (`:1175-1178`) and skips the containment check
  (`:1192`), i.e. it permits loading from anywhere not on the forbidden list.
- Using it raises `WildcardPathRestrictionUsed` (`:416-419`) — but see §2.2: raised inside the ctor, so
  unsubscribable. Documentation is emphatic that `"*"` is test-only
  (`src/Xcaciv.Loader/readme.md:7,44,60-71,78`; `AssemblyContext.cs:184-189`, `:346-353`).
- ⚠️ Note the library's *own tests* use `basePathRestriction: "*"` throughout
  (e.g. `ThreadSafetyTests.cs:52,91,126`, `AssemblyContextTests.cs:145,158`,
  `IntegrityVerificationIntegrationTests.cs:221`). Do not copy test code into host code.

### 3.2 Allowed-path restriction — exact semantics of `VerifyPath`

`AssemblyContext.VerifyPath(string filePath, string basePathRestriction = "*", AssemblySecurityPolicy? policy = null)`
— `AssemblyContext.cs:1139-1223`. It is **public and static**, so a host can pre-flight a path without
constructing a context. Steps:

1. `policy ??= AssemblySecurityPolicy.Default` (`:1142`); `ThrowIfNullOrWhiteSpace(filePath)` (`:1145`).
2. `Path.GetFullPath(filePath)` — canonicalization; `..` segments are resolved, **not rejected** (`:1149-1155`;
   the comment at `:1149-1151` admits this leniency exists "for compatibility with tests").
3. **Extension check** (`:1159-1165`): throws `SecurityException` unless the extension is `.dll`/`.exe`
   (case-insensitive) — **but only if the extension is non-empty**. ⚠️ **A path with no extension passes.**
   This is deliberate and asserted by the test
   `VerifyPath_NoExtension_ThrowsSecurityException` (`src/Xcaciv.LoaderTests/SecurityViolationTests.cs:266-278`),
   which — despite its name — asserts that **no exception is thrown**. Rejected extensions are covered at
   `SecurityViolationTests.cs:228-244` (`.txt/.so/.dylib/.config/.json`).
4. **Forbidden-directory check** (`:1168-1171`) → `SecurityException("Loading assemblies from system directories
   is not allowed: ...")`.
5. **Base-path confinement** (`:1174-1196`): if `"*"`, base := the file's own directory (`:1177`); otherwise
   `Path.GetFullPath(basePathRestriction)` (`:1181`). Empty base → `ArgumentOutOfRangeException` (`:1185-1189`).
   Containment test is
   `fullFilePath.StartsWith(effectiveBase, StringComparison.OrdinalIgnoreCase)` (`:1192`) →
   `ArgumentOutOfRangeException("Path was not within the restricted path of ...")` (`:1194-1195`).
6. Missing-file check is a **warning only** — `Debug.WriteLine`, no throw (`:1198-1203`).
7. `UnauthorizedAccessException` / `PathTooLongException` / `NotSupportedException` / `IOException` are all
   re-thrown as `SecurityException` (`:1207-1222`).

⚠️ **Prefix matching is not path-component matching** (`:1192`). Base `C:\App\Plugins` also admits
`C:\App\PluginsEvil\x.dll`. Mitigate by passing a base path with a trailing separator, or by validating
containment yourself (`Path.GetRelativePath` + no leading `..`) before calling.
⚠️ Symlinks/junctions/hardlinks are not resolved (`Path.GetFullPath` does not follow reparse points). A symlink
inside the plugin directory can point outside it. If plugin directories are writable by anyone but the host,
resolve the real path yourself.

`AssemblySecurityPolicy.ContainsForbiddenDirectory` (`:149-173`) lowercases, converts `/`→`\`, and **strips all
spaces** (`:155,159`), then matches a forbidden name as a **path component** — `\name\`, `name\` at start,
or `\name` at end, or whole-string equality (`:162-166`).
⚠️ This matching is **Windows-shaped**: it normalizes to backslashes and the strict list is Windows paths
(`:28-35`). On Linux/macOS the `Strict` list is essentially inert — `/usr/lib`, `/etc`, `$HOME/.ssh` are not
covered. **A cross-platform host must supply its own custom forbidden list per OS.**
⚠️ Space-stripping means `"windows defender"` matches `"WindowsDefender"` — intentional (`:148`), but it also
means a legitimate directory named e.g. `Program Files Backup` normalizes toward `programfilesbackup` and
does *not* match `programfiles` as a component — check your own directory names against the normalization.

### 3.3 What a violation produces

| Condition | Event raised | Exception thrown | Code |
|---|---|---|---|
| Forbidden directory | none (static method) | `SecurityException` | `:1168-1171` |
| Bad extension | none | `SecurityException` | `:1160-1165` |
| Outside base path | none | `ArgumentOutOfRangeException` | `:1192-1196` |
| Path I/O / access failure | none | `SecurityException` (wrapping) | `:1207-1222` |
| Wildcard used | `WildcardPathRestrictionUsed` (unsubscribable) | none | `:416-419` |
| Preflight indicator under Strict | `SecurityViolation(path, "Preflight policy violation: …")` | `SecurityException` | `:685-696`, `:595-606` |
| Dynamic assembly + `DisallowDynamicAssemblies` | `SecurityViolation(path, "Dynamic assemblies are disallowed by security policy.")` | `SecurityException` | `:752-756`, `:815-819` |
| Integrity mismatch / unknown hash | `HashMismatchDetected` (mismatch only) then `AssemblyLoadFailed` | `SecurityException` | `AssemblyIntegrityVerifier.cs:140-144,159-162`; `AssemblyContext.cs:768-772` |
| Security failure during dependency resolution | `SecurityViolation(path, ex.Message)` | rethrown | `:613-619` |

**The v2 guarantee that matters most:** *no silent failures*. Security exceptions are raised as an event and
then **always rethrown** (`:613-619`, `:768-772`) — `CHANGELOG.md:119-121` records this as a deliberate
breaking change (REL-001). A host can therefore rely on `catch (SecurityException)` as the single deny signal.

⚠️ **`SecurityException` is `System.Security.SecurityException`, and it is used for three distinct semantics**:
policy denial, integrity failure, and *path plumbing failure* (`:1207-1222` — a `PathTooLongException` arrives
as a "security" exception). Do not infer "attack" from the type alone; read `.Message` or, better, correlate
with the `SecurityViolation` event which carries a structured reason string.

⚠️ **Confinement failure is `ArgumentOutOfRangeException`, not `SecurityException`** (`:1194`). A host that
catches only `SecurityException` will let a path-escape attempt surface as an argument bug. Catch both.

---

## 4. Integrity verification

Two collaborating classes, **disabled by default** (pass `integrityVerifier: null` / omit → `null`,
`AssemblyContext.cs:405,410`; and even a default-constructed verifier is disabled, `AssemblyIntegrityVerifier.cs:61-67`).

### 4.1 `AssemblyIntegrityVerifier` — `src/Xcaciv.Loader/AssemblyIntegrityVerifier.cs`

```csharp
new AssemblyIntegrityVerifier(
    bool enabled,
    bool learningMode = true,                       // :95  NOTE: learning is the DEFAULT
    HashAlgorithmName algorithm = default,          // :96  default => SHA256 (:101)
    AssemblyHashStore? hashStore = null)            // :97  null => fresh empty store (:102)
```

**How hashes are computed** — `ComputeHash` (`:174-194`):
- Opens the file with `File.OpenRead` (`:183`) and hashes the **entire file bytes** — not the assembly identity,
  not a strong name, not an Authenticode signature.
- Algorithm dispatch by name: `SHA256` / `SHA384` / `SHA512` (`:185-191`); anything else →
  `NotSupportedException` (`:190`).
- Encoded as **Base64** (`:193`), not hex.

**How they are checked** — `VerifyIntegrity(filePath)` (`:121-165`):
1. `if (!Enabled) return;` (`:123-124`) — the fast exit.
2. `ThrowIfNullOrWhiteSpace` (`:126`); `FileNotFoundException` if absent (`:128-131`).
3. Compute the actual hash (`:133`).
4. **Known path** (`hashStore.TryGetHash` succeeds, `:135`): ordinal string compare (`:138`). On mismatch →
   raise `HashMismatchDetected(filePath, expected, actual)` (`:140`) then throw `SecurityException` whose message
   contains both hashes (`:141-144`).
5. **Unknown path** (`:147-164`):
   - `learningMode == true` → `hashStore.AddOrUpdate(path, hash)` and raise `HashLearned(path, hash)`
     (`:150-155`). **Trust on first use.**
   - `learningMode == false` → throw `SecurityException("No trusted hash found for assembly: …")` (`:158-162`).
     **This is the production posture: allowlist-only.**

Management helpers: `TrustAssembly` (`:205-211`, compute+store), `UntrustAssembly` (`:218-224`),
`IsTrusted` (`:231-237`), plus `Enabled`/`LearningMode`/`Algorithm`/`HashStore` accessors (`:26,31,36,41`).

**Where it is invoked in the load path:**
- Primary load: `AssemblyContext.cs:699` — after `File.Exists` (`:677`) and after preflight (`:685-696`),
  immediately before `LoadFromAssemblyPath` (`:707`/`:718`).
- Dependency load: `AssemblyContext.cs:609` — inside the private `LoadFromPath(context, path)`, so **dependencies
  are verified too** when a verifier is attached. ✅ This is the strongest part of the design.
- ⚠️ **Never invoked for name-based loads** (`LoadFromName`, `:799-841` — no call site).

**What happens on mismatch, end to end** (`IntegrityVerificationIntegrationTests.AssemblyContext_StrictMode_TamperedFile_ThrowsSecurityException`,
`src/Xcaciv.LoaderTests/IntegrityVerificationIntegrationTests.cs:198-224`): trust the file, append bytes to it,
then `CreateInstance` → `HashMismatchDetected` fires → `SecurityException` propagates out of
`AssemblyIntegrityVerifier.VerifyIntegrity` → caught by `AssemblyContext.LoadFromPath`'s
`catch (SecurityException)` (`:768`) → `AssemblyLoadFailed(path, ex)` raised (`:771`) → **rethrown** (`:772`).
The assembly is **never** passed to `LoadFromAssemblyPath`. ✅ Fail-closed.

Other integration coverage: no verifier / disabled verifier load normally (`:48`, `:61`); learning-mode first
load learns and second load verifies (`:78`, `:109`); strict mode with no hash throws (`:144-157`); strict mode
with a valid hash succeeds (`:170`); dev→prod workflow (`:229`); independent per-assembly verification (`:276`);
`HashLearned` event capture (`:320-352`); algorithms differ (`:356`); combined path + integrity (`:393`).

### 4.2 `AssemblyHashStore` — `src/Xcaciv.Loader/AssemblyHashStore.cs`

- In-memory `Dictionary<string,string>` + `lockObject`; **every** operation is locked (`:30-31,36-45,64,90,112,123,159,228,247`). ✅ Thread-safe.
- **Keying**: `AddOrUpdate` (`:57-68`), `TryGetHash` (`:80-94`), and `Remove` (`:105-116`) all pass the path
  through `NormalizePath` → `Path.GetFullPath` (`:263-266`), producing an **absolute** key. Comparison is the
  dictionary's default ordinal, i.e. **case-sensitive** — documented at `:14-18` and `:78`.
  ⚠️ On Windows this means `C:\Plugins\A.dll` and `c:\plugins\a.dll` are two entries for one file. **Canonicalize
  case in the host** before trusting or verifying.
- **Persistence is CSV, not JSON** (`:23-26`): a `#`-comment header (`:154-155`) then `FilePath,Hash` lines,
  with RFC-style quoting via `EscapeCsvField` (`:271-278`) / `ParseCsvLine` (`:348`).
  - `SaveToFile` (`:141-165`) snapshots under lock then writes (`:145-149`) — note it writes the **normalized
    absolute** keys.
  - `LoadFromFile` (`:178-231`) **clears the store first** (`:225`) and parses strictly: wrong field count or an
    empty field → `FormatException` naming the line number (`:206-217`); comments/blank lines skipped (`:198-200`).
  - `MergeFromFile(path, overwriteExisting = false)` (`:247-299`) is the additive variant (`:294`).
  - ⚠️ **`LoadFromFile` / `MergeFromFile` store the path exactly as written in the file** (`:219`, `:288`) —
    they do **not** call `NormalizePath`, contradicting the doc comment at `:110` and `:178`. Since `TryGetHash`
    normalizes the *lookup* to an absolute path, **any relative path in the CSV will never match** and the
    assembly will be treated as unknown (→ blocked in strict mode). **Write absolute paths into the CSV.**
- ⚠️ **The hash store is not portable.** Keys are absolute machine paths, so a hash file produced on a build
  agent is useless on a host that installs plugins elsewhere. Options: generate the CSV at install time on the
  target machine (learning mode, then `SaveToFile`), or build the store in memory at startup by mapping your
  package manifest's per-file digests onto resolved absolute paths via `AddOrUpdate`.
- ⚠️ **The hash file itself is an unprotected trust anchor.** It is plain CSV with no signature and no integrity
  of its own. Anyone who can write the CSV can authorize any DLL. Store it where only the host's install process
  can write, or better: derive trust from a signed package manifest at load time rather than from a file on disk.

### 4.3 Residual risks

⚠️ **TOCTOU.** The hash is computed (`:699`/`:609`) and then the file is opened again by the runtime
(`:707`/`:718`/`:611`). Between those two reads the file can be replaced. Narrow but real; mitigate by holding
an open `FileShare.Read`-only handle across the window, or by keeping plugin directories non-writable at runtime.
⚠️ **Learning mode is TOFU.** `learningMode` defaults to `true` (`:95`). Shipping a host with
`new AssemblyIntegrityVerifier(enabled: true)` and nothing else gives you an audit trail and **no enforcement**
— every new file is trusted on sight. Production must pass `learningMode: false` explicitly.
⚠️ **This is not code signing.** There is no Authenticode/strong-name/publisher check anywhere in the library
(grep confirms: no `X509`, no `StrongName`). It proves "these exact bytes are the bytes I recorded", not
"these bytes came from a party I trust". If publisher identity matters, verify signatures in the host before
handing the path to `AssemblyContext`.

---

## 5. Preflight analysis and scanning

Two unrelated things share a naming prefix — keep them distinct.

### 5.1 `AssemblyPreflightAnalyzer` — pre-load static inspection

`src/Xcaciv.Loader/AssemblyPreflightAnalyzer.cs:14` — **`internal static`**, so a host **cannot call it
directly**; it is reachable only through `AssemblySecurityPolicy.Strict`
(`GlobalSuppressions.cs:9` exposes it to tests only).

**What it inspects** — `Analyze(string assemblyPath)` (`:25-121`), using `System.Reflection.Metadata`'s
`PEReader`/`MetadataReader` (`:39-46`) so the assembly is **never executed and never loaded into the process**:
1. **Type definitions** whose namespace starts with `System.Reflection.Emit` (`:49-59`) → indicator
   `"Type in namespace '<ns>'"`.
2. **Assembly references** whose name starts with `System.Reflection.Emit` (`:62-72`) → `"References assembly '<name>'"`.
3. **Member references** named `Compile` whose declaring type's namespace starts with `System.Linq.Expressions`
   (`:75-105`) → `"Member '<ns>.Compile' reference"` / `"… definition"`.

Result shape (`:16-23`): `HasEmitNamespaceTypes`, `ReferencesEmitAssemblies`, `HasLinqExpressionsCompile`,
`IReadOnlyList<string> Indicators`, and `HasAnyIndicators` (`:22`).

**What can be rejected, and when.** Preflight runs **only when `SecurityPolicy.StrictMode` is true**:
- primary load — `AssemblyContext.cs:685-696`
- dependency load — `AssemblyContext.cs:595-606`

On `HasAnyIndicators`, the code joins the indicators into a reason, raises
`SecurityViolation(path, $"Preflight policy violation: {reason}")`, and throws
`SecurityException("Preflight policy violation under strict policy: Reflection.Emit/LINQ Compile detected.")`
(`:600-605`, `:690-695`). ✅ Fail-closed, and it applies to dependencies as well as the plugin itself.

⚠️ **Fails open on parse trouble.** No metadata (`:41-44`), missing/blank path (`:32-35`), or **any exception at
all** (`:107-112`, an empty catch with the comment "`@copilot: ignoe this empty catch block`") yields a neutral
result with no indicators — i.e. **the load proceeds**. A deliberately malformed or obfuscated PE therefore
bypasses preflight silently. There is no event for "preflight could not run".
⚠️ **Coarse and false-positive-prone.** `System.Linq.Expressions...Compile` is used by EF Core, AutoMapper,
serializers, DI containers, and most expression-based plumbing. Under `Strict`, a large fraction of ordinary
libraries will be rejected. Expect to run `Strict` only over plugins you also control, or to build a custom
policy: `new AssemblySecurityPolicy(myForbiddenDirs) { DisallowDynamicAssemblies = true }` gives you dynamic-load
blocking **without** preflight (because preflight is gated on `StrictMode` (`:685`), which the custom ctor forces
to `false` at `AssemblySecurityPolicy.cs:135`).
⚠️ **Metadata-only means evadable.** `docs/security-features-v2.md:40-42` says it plainly: it cannot see runtime
decisions, string-driven late reflection, or obfuscation. Treat it as a lint, not a sandbox.
⚠️ Name-based loads are never preflighted (`docs/security-features-v2.md:38`; no call site in `LoadFromName`).

Test assets that exercise it: `src/zTestRiskyAssembly/DynamicTypeCreator.cs` (AssemblyBuilder/TypeBuilder) and
`src/zTestLinqExpressions/ExpressionCompiler.cs` (Expression.Lambda + Compile), both described at
`docs/TESTING-DYNAMIC-ASSEMBLY-MONITORING.md:9-35`.

### 5.2 `AssemblyScanner` — post-load type discovery (not a security control)

`src/Xcaciv.Loader/AssemblyScanner.cs:17` — public static utility:
- `GetLoadedTypes<T>()` (`:43-48`) — scans **every assembly in the AppDomain** and filters to concrete
  implementors of `T`. ⚠️ Expensive and AppDomain-wide; it will also return host types. Documented as costly at `:29`.
- `GetTypes<T>(Assembly)` (`:72-78`) — scan one assembly; `ArgumentNullException` on null (`:74`). **Prefer this.**
- `GetTypesFromAssembly` (`:89-100`) tolerates `ReflectionTypeLoadException` by returning the types that did
  load (`:95-99`) — resilient to a plugin with a broken dependency.
- `AssemblyContext.GetLoadedTypes<T>()` is `[Obsolete]` and forwards here (`AssemblyContext.cs:1109-1114`);
  removal planned v3.0.0 (`CHANGELOG.md:141-143`).

Note the asymmetry: `AssemblyContext.GetTypes*` (`:1071-1098`) does **not** swallow `ReflectionTypeLoadException`
— it will propagate out of `assembly.GetTypes()`. Wrap it, or use `AssemblyScanner.GetTypes<T>(assembly)` for
robust discovery.

---

## 6. Dynamic assembly monitoring

### 6.1 The threat

Reflection.Emit (`AssemblyBuilder`, `TypeBuilder`, `DynamicMethod`) and `Expression.Compile()` let a plugin
**generate and execute code that was never on disk** — so nothing you validated statically (path, extension,
hash, preflight) constrains it. It is the standard escape hatch from an allowlist-of-bytes model, and the way a
benign-looking DLL becomes a runtime code loader. `CHANGELOG.md:58` states the intent: "Blocks dynamic/in-memory
assembly loads under strict policy to reduce runtime injection risk."

### 6.2 Local enforcement — `DisallowDynamicAssemblies`

- Flag: `AssemblySecurityPolicy.DisallowDynamicAssemblies`, `init`-only (`AssemblySecurityPolicy.cs:74`).
- Defaults: `Strict` → **true**; `Default` → false; custom-list ctor → false
  (`AssemblySecurityPolicy.cs:108`, `:136`). Asserted by
  `DisallowDynamicAssembliesTests` (`docs/TESTING-DYNAMIC-ASSEMBLY-MONITORING.md:107-119`).
- Enforcement points — after the assembly object exists, check `loadedAssembly.IsDynamic`:
  - path load: `AssemblyContext.cs:752-756`
  - name load: `AssemblyContext.cs:815-819`
  Both raise `SecurityViolation(..., "Dynamic assemblies are disallowed by security policy.")` then throw
  `SecurityException`.
- ⚠️ **Scope is narrow — and this is the key limitation.** The check only fires for assemblies loaded *through
  this context*. An `Assembly` produced by `AssemblyBuilder` inside plugin code is never routed through
  `LoadFromPath`/`LoadFromName`, so this flag **does not stop a loaded plugin from emitting code**. It is
  preflight (§5.1) that tries to keep such a plugin from loading at all. `docs/security-features-v2.md:25`:
  "It cannot prevent other code in the process from using Reflection.Emit. For stronger isolation, consider
  separate processes."

### 6.3 Global monitoring — `EnableGlobalDynamicAssemblyMonitoring()`

**Mechanism** — `AssemblyContext.cs:478-495`:
- `ThrowIfDisposed()` first (`:480`); then under `globalMonitorLock` (`:481`):
  - de-duplicates by identity scan over the subscriber list (`:484-487`) — so repeated calls add one entry
    (test: `..._CallMultipleTimes_NoDuplicates`, `docs/TESTING-DYNAMIC-ASSEMBLY-MONITORING.md:85-88`);
  - attaches **one** process-wide handler the first time:
    `AppDomain.CurrentDomain.AssemblyLoad += GlobalAssemblyLoadHandler` (`:489-493`).
- Handler `GlobalAssemblyLoadHandler` (`:497-537`): ignores non-dynamic assemblies (`:501-502`); builds an
  identifier from `Location` or `FullName` (`:504-506`); walks subscribers **backwards**, pruning dead
  `WeakReference`s (`:510-516`); raises `SecurityViolation(identifier, "Global monitor: Dynamic assembly load
  detected.")` **only** on contexts whose policy sets `DisallowDynamicAssemblies` (`:519-522`); detaches the
  handler when the list empties (`:526-530`); and wraps everything in `catch { }` (`:533-536`) so monitoring can
  never break the host's load path.
- Deregistration: `RemoveGlobalSubscriber` (`:539-557`), called from `Dispose(bool)` (`:1395`).

**What it costs:**
- ⚠️ **A process-wide `AppDomain.AssemblyLoad` handler on every assembly load in the process** — host, framework,
  and plugin alike (`:491`). Each load takes `globalMonitorLock` (`:508`) and iterates all subscribers. For a
  handful of contexts this is negligible; for hundreds it is a serialized hot spot on a path that is otherwise
  lock-free. `docs/TESTING-DYNAMIC-ASSEMBLY-MONITORING.md:268` lists performance measurement as *future* work —
  **no benchmark exists**.
- ⚠️ **Zero enforcement value.** `IAssemblyContext.cs:87-91` and `docs/security-features-v2.md:65` both state it
  is audit-only: it cannot prevent emit and does not throw. You learn that dynamic code appeared, after it has.
- ⚠️ **Global static state in an otherwise instance-based design** (`AssemblyContext.cs:24-26`). Subscriptions
  outlive the context that made them until GC prunes the weak reference, and `DisposeAsync` does not deregister
  (§2.5). Tests that rely on this behave nondeterministically — see the GC-forcing test
  `..._DisposedContext_RemovedFromSubscribers` (`src/Xcaciv.LoaderTests/GlobalDynamicAssemblyMonitoringTests.cs:210`,
  GC forced at `:234-236`).
- ⚠️ Detection is best-effort even for Expressions: the LINQ integration test asserts only "monitoring completes
  without error" because whether `Expression.Compile` surfaces a dynamic assembly depends on runtime internals
  (`GlobalDynamicAssemblyMonitoringTests.cs:563-564`).

**Verdict for a host:** enable it in high-assurance deployments as *telemetry* — a tripwire that says "a plugin
generated code" — and route it to your SIEM. Do not treat it as a control, and measure the load-path cost
yourself before turning it on in a service that loads many assemblies.

Coverage: `GlobalDynamicAssemblyMonitoringTests` (strict raises `:21`; default does not `:83`; multi-context
`:141`; disposed pruning `:210`; concurrency `:275`; message content `:330`; disposed → `ObjectDisposedException`
`:384`; dedupe `:423`; risky-assembly integration `:475`; LINQ integration `:529`), catalogued in
`docs/TESTING-DYNAMIC-ASSEMBLY-MONITORING.md:39-157`.

---

## 7. Events and observability

Six events, all on the concrete `AssemblyContext` (see §2.1 — **not** on `IAssemblyContext`), all documented as
"may be raised from any thread; handlers must be thread-safe".

| # | Event | Signature | Declared | Raised at | Subscribe? |
|---|---|---|---|---|---|
| 1 | `AssemblyLoaded` | `Action<string filePath, string assemblyName, Version?>` | `:66` | `:761-764` (path), `:824-827` (name) | ✅ inventory / audit |
| 2 | `AssemblyLoadFailed` | `Action<string filePath, Exception>` | `:90` | `:623`, `:629`, `:680`, `:771`, `:777`, `:782`, `:787`, `:833`, `:838`, `:903`, `:962` | ✅ **required** — alert on repeats |
| 3 | `AssemblyUnloaded` | `Action<string filePath, bool success>` | `:116` | `:1254`, `:1260`, `:1359`, `:1365` | ✅ leak detection |
| 4 | `SecurityViolation` | `Action<string filePath, string reason>` | `:148` | `:521` (global monitor), `:603`, `:616` (dependency), `:693`, `:754`, `:817` | ✅ **critical** |
| 5 | `DependencyResolved` | `Action<string dependencyName, string resolvedPath>` | `:171` | `:573`, `:580` | ✅ supply-chain visibility |
| 6 | `WildcardPathRestrictionUsed` | `Action<string filePath>` | `:200` | `:418` only | ⚠️ **unsubscribable** (§2.2) |

Plus two on the verifier: `HashMismatchDetected(path, expected, actual)`
(`AssemblyIntegrityVerifier.cs:47`, raised `:140`) and `HashLearned(path, hash)` (`:53`, raised `:154`).

**What a host should subscribe to, and why:**
1. `SecurityViolation` → **critical/alert**. The reason string distinguishes preflight, dynamic-assembly, global
   monitor, and dependency-path violations. This is your primary attack signal.
2. `AssemblyLoadFailed` → **error**, with correlation. A burst of failures against one plugin path is a probing
   signal (`AssemblyContext.cs:80` says exactly this).
3. `HashMismatchDetected` → **critical/alert**. Tamper detection; log expected vs. actual and quarantine the file.
4. `DependencyResolved` → **info**, but keep it: it is the only record of *where* each transitive dependency came
   from, which is what you will want during an incident (`AssemblyContext.cs:162`).
5. `AssemblyLoaded` → **info**. Name + version + path is your loaded-plugin inventory.
6. `AssemblyUnloaded` with `success == false` → **warning**: objects are still referenced, and the memory will
   not be reclaimed (`AssemblyContext.cs:104`).
7. `HashLearned` → **warning in production**. In a correctly configured production host (`learningMode: false`)
   this event can never fire; if it does, someone shipped a learning-mode verifier.

⚠️ **Subscribe immediately after construction and before the first `CreateInstance`/`GetTypes`** — loading is
lazy (§2.3), so all load-time events fire on that first call, not at construction. The only events you can never
catch are the constructor-time ones (`WildcardPathRestrictionUsed`, and any `SecurityException` from
`VerifyPath`).
⚠️ Events are raised with `?.Invoke` on the **calling thread** for path loads, but on a **thread-pool thread**
when `LoadTimeout` is active (`:717-719`) and for `UnloadAsync` (`:1336`). Handlers must be thread-safe and must
not throw — an exception from a handler propagates into the load path.

Event coverage: `src/Xcaciv.LoaderTests/EventTests.cs` — success `:36`, parameter correctness `:63`, multiple
subscribers `:89`, file-not-found `:115`, bad image `:140`, unload success `:179`, wildcard `:340`, explicit
restriction `:368`, ordering `:424`/`:449`, unsubscribe `:480`.

---

## 8. Exceptions and how a host should handle each

### 8.1 `TypeNotFoundException` — `src/Xcaciv.Loader/Exceptions/TypeNotFoundException.cs:8`

- `public class TypeNotFoundException : Exception` with `TypeName` (`:13`) and `AssemblyName` (`:18`) properties;
  message `"Type '<typeName>' was not found in assembly '<assemblyName>'"` (`:26`, `:39`).
- Thrown **only** from `CreateInstance<T>(string className)` (`AssemblyContext.cs:980`).
- ⚠️ It does **not** derive from `TypeLoadException`, so `catch (TypeLoadException)` will not catch it.
- ⚠️ `CreateInstance(string)` (non-generic) returns **`null`** for the same condition (`:919`) — inconsistent.
- **Host handling:** this is a *contract* error, not a security event — the package does not contain the entry
  type you expected. Reject the package at registration time with a clear message naming `TypeName` and
  `AssemblyName`; do not retry. Better: avoid it by enumerating with `GetTypes<TContract>()` and failing when the
  set is empty or ambiguous.

### 8.2 `SecurityException` (`System.Security`) — the deny signal

Sources (all listed in §3.3): forbidden directory (`:1170`), bad extension (`:1164`), preflight violation
(`:604`, `:694`), dynamic assembly (`:755`, `:818`), integrity mismatch or unknown hash
(`AssemblyIntegrityVerifier.cs:141`, `:159`), and path-plumbing failures wrapped at `:1207-1222`.

**Host handling:** **fail closed and do not retry.** Quarantine the package, mark it untrusted in your registry,
emit a security-severity log correlating the exception with the `SecurityViolation` event (which carries the
machine-readable reason), and surface a generic message to end users — do not echo the exception text, which
contains full paths and both hashes (`AssemblyIntegrityVerifier.cs:142-144`). Distinguish the wrapped I/O cases
(`:1207-1222`) as operational, not adversarial, if you need clean alerting.

### 8.3 Everything else the load path can throw

| Exception | Meaning | Where | Host handling |
|---|---|---|---|
| `ArgumentOutOfRangeException` | **path escaped the base restriction** | `:1194` | Treat as a **security** deny, same as `SecurityException`. Easy to miss — catch it explicitly. |
| `ArgumentException` | null/empty/whitespace path, class name, or invalid assembly name | `:407`, `:898`, `:957`, `:839`; `AssemblyPathValidator.cs:232`, `:249` | Caller bug / bad manifest. Reject at validation time. |
| `ArgumentNullException` | null `AssemblyName` or `Type` | `:451`, `:1021` | Caller bug. |
| `FileNotFoundException` | file missing at load or at `CreateInstance`; dependency not found | `:679`, `:834`, `:902`, `:961` | Package incomplete/moved. Reinstall or re-resolve; not a security event by itself. **Also the symptom of using the name-based ctor (§2.2).** |
| `BadImageFormatException` | not a valid managed assembly (wrong arch, corrupt, native DLL) | `:788`, and rethrown at `:626-630` | Reject the package. Wrapped with the path added. |
| `FileLoadException` | assembly found but unloadable (locked, bad manifest) | `:783` | Transient or corrupt; safe to retry once, then reject. |
| `TimeoutException` | load exceeded `LoadTimeout` | `:723`, `:732`, `:738` | Dispose the context and abandon it (§2.6). Possible DoS signal — log it. |
| `InvalidOperationException` | no path and no name (`:878`); load context null/unset (`:642`); **unload failed** (`:1261`, `:1366`); activation failures — no parameterless ctor (`:927`, `:995`, `:1033`), ctor threw (`:931`, `:999`, `:1037`), ctor inaccessible (`:935`, `:1003`, `:1041`), instance null (`:1060`) | | Mostly plugin-contract errors — report which plugin and why; from `Unload` it means live references remain. |
| `TypeLoadException` | type could not be loaded / `ReflectionTypeLoadException` wrapped | `:939`, `:987` | Broken/mismatched dependency in the package. |
| `InvalidCastException` | instance does not implement `T` | `:991`, `:1029` | Contract mismatch — the plugin claims a type that doesn't implement your interface. Reject. |
| `ObjectDisposedException` | use after dispose | `:654` | Host lifecycle bug. |
| `NotSupportedException` | unsupported hash algorithm | `AssemblyIntegrityVerifier.cs:190` | Config error at startup. |
| `FormatException` | malformed hash CSV, with line number | `AssemblyHashStore.cs:206-217` | Config error — fail startup loudly; a corrupt trust store must not degrade to "trust nothing quietly". |

⚠️ **A single deny check is not enough.** The minimum security-correct catch set for a load attempt is
`SecurityException` **and** `ArgumentOutOfRangeException`. Everything else is availability/contract handling.

---

## 9. SSEM scoring methodology — what it is and why it matters here

`docs/ssem-scoring-methodology.md` (635 lines, v1.0, dated 2025-11-29).

**What it is.** SSEM — **Securable Software Engineering Model** — is a self-assessment rubric scoring a codebase
0–10 on three pillars (`:26-30`): **Maintainability**, **Trustworthiness**, **Reliability**. Its thesis
(`:34-36`): "Security is not a static state but a dynamic capability" — the goal is software that can be
*evolved* securely, not merely audited clean once. It is presented as derived from **FIASSE** (`:40-45`) with
four principles: Derived Integrity, **Canonical Input Handling**, **Transparency**, Resilient Coding — and as
extending **ISO/IEC 25010** (`:438-440`, `:450-453`), with supporting references to OWASP, Microsoft SDL, NIST
SSDF, and defense-in-depth (`:455-494`).

**Scoring mechanics.** 0–10 per pillar with grade bands (9.0+ Excellent, 8.0+ Good, 7.0+ Adequate, 6.0+ Fair,
<6.0 Poor) (`:72-78`); overall is the simple average (`:80`). Weights: Maintainability 33% (`:119`),
Trustworthiness 34% (`:167`), Reliability 33% (`:215`). Sub-attributes and their intra-pillar weights:
Analyzability 40% / Modifiability 30% / Testability 30% (`:125,139,153`); Confidentiality 30% /
Authenticity & Accountability 35% / Integrity 35% (`:173,187,201`); Operational Integrity 30% / Resilience 40% /
Availability 30% (`:221,235,249`). Scores come from code, architecture, documentation, testing, and security
review (`:88-94`), adjusted for severity, scope, mitigations, and standards alignment (`:108-113`). Appendix A
(`:508+`) is a plain checklist.

**Its self-assessment of this library.**

| Pillar | v1.x | v2.0 | Evidence |
|---|---|---|---|
| Maintainability | 7.0 | 8.5 | `:269-284`, `:326-338` |
| Trustworthiness | 9.0 | 9.5 | `:286-302`, `:340-351` |
| Reliability | 8.0 | 9.0 | `:303-319`, `:353-365` |
| **Overall** | **8.0** | **8.9** | `:320`, `:367` |

The v1.x weaknesses it names are exactly the things v2 changed: static mutable state, `VerifyPath` ~103 lines,
mixed concerns in `GetLoadedTypes` (`:276-279`); no cryptographic integrity verification (`:294-296`); silent
failures in dependency resolution, overly broad catches, no timeouts (`:310-313`).

**Why it matters to a security-conscious host — and how to read it critically.**
- ✅ It is a **traceability artifact**: every v2 change maps to a scored deficiency
  (`:377-394` phase ledger, `:398-411` impact table). That makes the library's security posture *auditable as
  intent*, not just as code, and it explains why the API looks the way it does (instance policies, mandatory
  rethrow, timeouts, hash verification).
- ✅ It tells you where the author believes the weak spots remain — most usefully Availability 8/10 with the
  note "good thread safety, **no timeouts**" (`:363`, stale — timeouts exist at `:260`) and Analyzability 8/10.
- ⚠️ **It is a self-assessment with no external validation.** Scores are assigned by the same party that wrote
  the code; there is no rubric for evidence, no independent reviewer, and no reproducible measurement. A 9.5/10
  "Trustworthiness" is an assertion, not a finding.
- ⚠️ **The numbers disagree across documents.** The methodology concludes **8.9/10** (`:367`); the release notes
  claim **9.0/10 (Excellent)** (`docs/V2.0-Release Notes.md:18`, `:38`). Rounding, but it illustrates the
  softness.
- ⚠️ **Several scores are contradicted by this analysis.** Trustworthiness "Integrity: 10/10 (crypto verification
  added)" (`:349`) coexists with a preflight analyzer that fails open (`AssemblyPreflightAnalyzer.cs:107-112`)
  and a hash store that silently mis-keys relative CSV paths (`AssemblyHashStore.cs:219`). Reliability
  "Operational Integrity: 10/10 (no silent failures)" (`:361`) coexists with `Unload()`'s four indistinguishable
  `false` returns (`AssemblyContext.cs:1235-1242`) and `DisposeAsync`'s swallowed exceptions (`:1425-1428`).
  Availability 8–9/10 "excellent thread safety" coexists with a completely unlocked load path (§2.7).
- ⚠️ Test claims should be read as of a snapshot: "194 tests, 188 passed, 6 skipped, 96.9%"
  (`docs/V2.0-Release Notes.md:113-121`). Several of those skips are security tests disabled for environment
  reasons — `VerifyPath_PathTraversalToSystemDirectory_ThrowsSecurityException` is `[Theory(Skip = …)]`
  (`src/Xcaciv.LoaderTests/SecurityViolationTests.cs:95`) and
  `LoadFromPath_ForbiddenDirectory_RaisesSecurityViolationEvent` is `[Fact(Skip = …)]` (`:285`). **Path-traversal
  rejection is therefore not covered by a running test.**

**Bottom line:** use SSEM as a map of the author's intent and a checklist for your own review — not as
assurance. The right posture is: adopt the library for its architecture (instance policies, mandatory rethrow,
audit events, collectible contexts), and independently verify the specific behaviors your threat model depends on.

---

## 10. Prescriptive checklist — how a host should load tool packages safely

Ordered. Each step names the API call and the failure handling.

### Step 0 — Decide before you build (one time)
- **Resolve the AGPL-3.0-only license** (`Xcaciv.Loader.csproj:34`) with counsel. This gates everything.
- **Accept that the library is unsupported** (`SECURITY.md:5`) → vendor the source, or pin the exact version and
  own the patching.
- **Decide the target asset**: default package is net8.0-only (`Xcaciv.Loader.csproj:4-5`); build with
  `/p:UseNet10=true` if you need a net10 asset.
- **Do not enable trimming/AOT** in the host (`GlobalSuppressions.cs:11-26` suppresses, does not solve).
- **Define your plugin contract interface** with a **public parameterless constructor** requirement plus an
  `Initialize(...)` method for dependencies — `Activator.CreateInstance` is the only activation path
  (`AssemblyContext.cs:1057`).
- **Wrap the library.** Build a host-side `IToolPackage` that *holds* a concrete `AssemblyContext`, because
  `IAssemblyContext` exposes no events and no policy (§2.1).

### Step 1 — Establish the plugin root and lock it down
```csharp
var pluginRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "tools"));
```
- Use a **dedicated directory**, never `AppContext.BaseDirectory` itself, never a user-writable location.
- Make it **read-only to the runtime account** at install time. This is your main mitigation for the
  integrity TOCTOU window (§4.3) and for symlink games (§3.2).
- **Failure:** if the directory is missing or writable by non-admins, refuse to start. Fail loudly at startup —
  never degrade to "load anyway".

### Step 2 — Validate and canonicalize the candidate path (before touching `AssemblyContext`)
```csharp
if (!AssemblyPathValidator.IsSafePath(candidate)) reject();               // AssemblyPathValidator.cs:129
var path = AssemblyPathValidator.ValidateAndSanitize(candidate,           // :225
                                                     resolveRelativeToBase: false);
if (!AssemblyPathValidator.HasValidAssemblyExtension(path)) reject();     // :173  closes the empty-extension hole
path = Path.GetFullPath(path);
```
- `ValidateAndSanitize` runs `IsSafePath` → sanitize → optional resolve → extension check (`:229-254`).
  `IsSafePath` rejects null bytes, `..`, `*`/`?`, and `<`/`>`/`|` (`:132-151`).
- **Then add the two checks the library does not do:**
  - **Component-wise containment** (the library uses `StartsWith`, `AssemblyContext.cs:1192`):
    `var rel = Path.GetRelativePath(pluginRoot, path); if (rel.StartsWith("..") || Path.IsPathRooted(rel)) reject();`
  - **Real-path resolution** to defeat symlinks: `new FileInfo(path).ResolveLinkTarget(true)` and re-check containment.
  - **Case canonicalization** if you will key a hash store by this path (`AssemblyHashStore.cs:14-18`).
- **Failure:** `ArgumentException` from `ValidateAndSanitize` (`:232`, `:249`) → reject the package, log at
  warning, do not retry.

### Step 3 — Verify publisher identity yourself (the library does not)
- Check Authenticode / your own signed package manifest **before** the path reaches `AssemblyContext`.
  Integrity verification (§4) proves bytes match a record; it does not prove provenance.
- **Failure:** reject and quarantine.

### Step 4 — Build the security policy
```csharp
// Preferred for third-party tool packages you do NOT control:
var policy = new AssemblySecurityPolicy(forbiddenDirs)      // AssemblySecurityPolicy.cs:131
             { DisallowDynamicAssemblies = true };          // re-enable: ctor forces false at :136

// Only for packages you build and control (preflight will reject most real libraries):
var policy = AssemblySecurityPolicy.Strict;                 // :63  StrictMode + DisallowDynamic + preflight
```
- Supply `forbiddenDirs` **per OS** — the built-in `Strict` list is Windows-only (`:28-35`, §3.2).
- Remember: a custom-list policy silently disables `DisallowDynamicAssemblies` **and** preflight
  (`:135-136`; preflight is gated on `StrictMode`, `AssemblyContext.cs:685`). Re-enable the flag explicitly, and
  accept that you lose preflight unless you use `Strict`.
- **Never** call `SetStrictDirectoryRestriction` — it is a no-op (`AssemblyContext.cs:318-325`).

### Step 5 — Build the integrity verifier (production = allowlist)
```csharp
var store = new AssemblyHashStore();
store.LoadFromFile(trustedHashesCsv);                       // AssemblyHashStore.cs:178  (absolute paths only!)
var verifier = new AssemblyIntegrityVerifier(
    enabled: true,
    learningMode: false,                                    // MANDATORY in production (:150-162)
    algorithm: HashAlgorithmName.SHA256,
    hashStore: store);
verifier.HashMismatchDetected += (p,e,a) => securityLog.Critical(...);   // :47
verifier.HashLearned          += (p,h) => securityLog.Warning("learning mode in prod!", p); // :53
```
- Write **absolute** paths into the CSV — relative paths never match (`AssemblyHashStore.cs:219` vs `:88`).
- Generate the store **on the target machine at install time** (learning mode + `SaveToFile`, `:141`), or build
  it in memory from your signed manifest with `AddOrUpdate` (`:57`).
- Protect the CSV: it is an unsigned trust anchor (§4.3).
- **Failure:** `FormatException` naming a line number (`:206-217`) or `FileNotFoundException` (`AssemblyHashStore.cs:184`) → **fail
  host startup**. A missing or corrupt trust store must never silently become "allow everything" (which is what
  an empty store + `learningMode: true` would give you).

### Step 6 — Construct the context (this is where path security executes)
```csharp
var ctx = new AssemblyContext(
    filePath: path,
    fullName: packageId,                 // names the ALC — shows up in diagnostics
    isCollectible: true,                 // required for unload  (AssemblyContext.cs:402)
    basePathRestriction: pluginRoot,     // NEVER "*"            (:403)
    securityPolicy: policy,              // (:404)
    integrityVerifier: verifier)         // (:405)
{
    LoadTimeout = TimeSpan.FromSeconds(30)   // init-only (:260); Timeout.InfiniteTimeSpan disables
};
```
- `VerifyPath` runs here (`:412`) — extension, forbidden directories, containment.
- **Failure handling — catch both:**
  - `catch (SecurityException)` → forbidden directory or bad extension → **security deny**, quarantine, alert.
  - `catch (ArgumentOutOfRangeException)` → **path escaped `pluginRoot`** → **security deny**, alert. Do not let
    this fall through as a generic argument bug.
  - `catch (ArgumentException)` → empty/invalid input → reject as a manifest error.
- Never pass `"*"`, and never call the one-argument `AssemblyContext.VerifyPath(path)` — its default is `"*"`
  (`:1139`).

### Step 7 — Subscribe to events *now*, before any load
```csharp
ctx.SecurityViolation   += (p,r) => securityLog.Critical("VIOLATION {r} {p}", r, p);   // :148
ctx.AssemblyLoadFailed  += (p,e) => log.Error(e, "load failed {p}", p);                // :90
ctx.DependencyResolved  += (n,p) => log.Info("dep {n} <- {p}", n, p);                  // :171
ctx.AssemblyLoaded      += (p,n,v) => inventory.Record(p, n, v);                       // :66
ctx.AssemblyUnloaded    += (p,ok) => { if (!ok) log.Warn("unload failed {p}", p); };    // :116
```
- Loading is lazy (§2.3), so subscribing here still catches every load-time event.
- Handlers must be **thread-safe and non-throwing** — they may run on a thread-pool thread (`:717-719`, `:1336`)
  and an exception propagates into the load path.
- `WildcardPathRestrictionUsed` cannot be caught (§2.2) — enforce "no wildcard" in your own code instead.

### Step 8 — (Optional, high-assurance) Enable global monitoring as telemetry
```csharp
ctx.EnableGlobalDynamicAssemblyMonitoring();     // :478   audit-only, never blocks
```
- Adds a process-wide `AssemblyLoad` handler (`:491`) — measure the cost on your load volume first (§6.3).
- **Failure:** `ObjectDisposedException` if the context is already disposed (`:480`). Treat any resulting
  `SecurityViolation` as an incident signal (a plugin generated code at runtime), not as a block.

### Step 9 — Discover types by contract, not by name
```csharp
var candidates = ctx.GetTypes<IMyToolContract>().ToList();      // :1094  concrete implementors only
if (candidates.Count == 0) reject("no contract implementation");
if (candidates.Count  > 1) reject("ambiguous: " + string.Join(",", candidates.Select(t => t.FullName)));
var tool = ctx.CreateInstance<IMyToolContract>(candidates[0]);  // :1017  Type overload — no name matching
```
- This is the **first call that touches the disk**: preflight (`:685`), integrity (`:699`), timeout (`:704`),
  and the dynamic-assembly check (`:752`) all execute here.
- Avoid `CreateInstance(string)` entirely — it suffix-matches across **dependencies** (`:917`) and returns `null`
  on miss (§2.3). If you must use a name, use `CreateInstance<T>(string)` with a **fully-qualified** name
  (`:953`, searches only the primary assembly at `:976`).
- **Failure handling:**
  - `SecurityException` → preflight / dynamic-assembly / integrity denial → **quarantine, alert, do not retry**.
  - `TimeoutException` (`:723`) → **dispose the context and abandon it**; the load may still be running (§2.6).
    Log as a possible DoS.
  - `FileNotFoundException` (`:679`) / `BadImageFormatException` (`:788`) / `FileLoadException` (`:783`) →
    package broken or incomplete; reject, or retry once for `FileLoadException`.
  - `TypeNotFoundException` (`:980`) / `InvalidCastException` (`:991`) / `InvalidOperationException` from
    activation (`:927`,`:931`,`:935`) → **contract violation**: report which package and which type, reject.
  - `TypeLoadException` (`:987`) → missing/mismatched dependency inside the package.
- **Concurrency:** serialize the first load per context (§2.7) — `Lazy<T>` with
  `LazyThreadSafetyMode.ExecutionAndPublication`, or a lock in your wrapper. Different packages may load in
  parallel freely.

### Step 10 — Run the tool behind your own boundary
- The library gives you **isolation of assembly identity, not of privilege**. A loaded plugin runs with the
  host's full trust: it can read files, open sockets, and P/Invoke. `docs/security-features-v2.md:25` recommends
  separate processes for real isolation.
- For untrusted tool packages, **run them out of process** (child process with a restricted token / container /
  seccomp) and use `Xcaciv.Loader` *inside* that process. Everything above is defense in depth around loading;
  none of it is a sandbox.

### Step 11 — Unload deterministically
```csharp
// sync
using (ctx) { /* ... */ }                 // Dispose -> Unload + deregister global monitor (:1389-1396)
// or async
await ctx.DisposeAsync();                 // :1416  NOTE: does not deregister global monitor (:1395 only)
```
- Drop **every** reference to plugin instances, types, and delegates **first** — otherwise the ALC stays alive
  and `AssemblyUnloaded` reports `success == false` (`:1260`).
- `Unload()` is **terminal** for the context (`:1248`); to reload, dispose and construct a new context (§2.4).
- **Failure:** `InvalidOperationException` from `Unload` (`:1261`) means live references remain → log a warning,
  keep the context in a "draining" state, and re-check after a GC. Never call the finalizer path
  (`Dispose(false)` skips unload entirely, `:1382-1400`).
- Track unload failures as a **leak metric** — repeated failures on hot-reload are how a plugin host runs out of
  memory.

### Step 12 — Operate
- Alert on `SecurityViolation` and `HashMismatchDetected`; alert on `HashLearned` in production (it should be
  impossible).
- Keep the `DependencyResolved` log — it is your only record of transitive load provenance.
- ⚠️ Note that dependency resolution loads with `VerifyPath(path, "*", policy)` (`:592`) — **dependencies are not
  confined to `basePathRestriction`**, only to the forbidden-directory list. In practice resolution only probes
  the plugin's own directory (`:569-582`), but a crafted `*.deps.json` can point elsewhere. If this matters,
  strip or validate `deps.json` at install time, or pre-hash every file the package ships and run with
  `learningMode: false` so an unexpected dependency path is rejected as "no trusted hash" (`:159`).

### Quick reference — the six rules that matter most
1. **Never** `basePathRestriction: "*"`, and never the one-arg `VerifyPath` (default is `"*"`, `:1139`).
2. **Always** `learningMode: false` in production (`AssemblyIntegrityVerifier.cs:150-162`).
3. **Catch `SecurityException` *and* `ArgumentOutOfRangeException`** — confinement failure is the latter (`:1194`).
4. **Use the path-based constructor**; the name-based one breaks `CreateInstance` and skips preflight+integrity (§2.2).
5. **Discover by contract** (`GetTypes<T>`), not by suffix-matched class name (§2.3).
6. **One context per package, serialize its first load, dispose deterministically** (§2.7, §2.5).

---

## Appendix A — File map

| File | Role |
|---|---|
| `src/Xcaciv.Loader/IAssemblyContext.cs` | Public interface (93 lines) — no events, no policy |
| `src/Xcaciv.Loader/AssemblyContext.cs` | The engine (1460 lines) — ctors, load, resolve, types, unload, dispose, `VerifyPath`, global monitor |
| `src/Xcaciv.Loader/AssemblySecurityPolicy.cs` | Immutable policy: forbidden dirs, strict mode, dynamic-assembly flag (174) |
| `src/Xcaciv.Loader/AssemblyPathValidator.cs` | Caller-invoked input sanitization (256) |
| `src/Xcaciv.Loader/AssemblyIntegrityVerifier.cs` | SHA-256/384/512 verification engine (238) |
| `src/Xcaciv.Loader/AssemblyHashStore.cs` | Thread-safe hash map + CSV persistence (389) |
| `src/Xcaciv.Loader/AssemblyPreflightAnalyzer.cs` | `internal` metadata-only Emit/Expressions scan (122) |
| `src/Xcaciv.Loader/AssemblyScanner.cs` | Type discovery helpers (101) |
| `src/Xcaciv.Loader/Exceptions/TypeNotFoundException.cs` | Custom exception (43) |
| `src/Xcaciv.Loader/GlobalSuppressions.cs` | `InternalsVisibleTo` + IL trim suppressions (26) |
| `docs/security-features-v2.md` | Authoritative security summary (155) |
| `docs/ssem-scoring-methodology.md` | SSEM rubric + self-assessment (635) |
| `docs/TESTING-DYNAMIC-ASSEMBLY-MONITORING.md` | Monitoring/preflight test catalogue (272) |
| `docs/multi-framework.md` | Build targeting (60) — partly stale |
| `docs/MIGRATION-v1-to-v2.md` | v1→v2 migration (485) |
| `CHANGELOG.md` | History through 2.1.0 (408) |
| `src/Xcaciv.LoaderTests/*.cs` | 11 suites: context, hash store, verifier, path validator, dynamic-assembly policy, events, global monitoring, integrity integration, security, security violations, thread safety |
| `src/zTestRiskyAssembly/`, `src/zTestLinqExpressions/` | Deliberately risky fixtures (Reflection.Emit, `Expression.Compile`) |

## Appendix B — Findings a host should carry into design review

| # | Finding | Evidence | Impact |
|---|---|---|---|
| B1 | AGPL-3.0-only license | `Xcaciv.Loader.csproj:34` | **Blocking** — legal review required |
| B2 | "This library is not supported" | `SECURITY.md:5` | **High** — vendor and own patching |
| B3 | Load path is completely unlocked; `disposed`/`assembly`/`isLoaded` non-volatile | `AssemblyContext.cs:852-883,752-758,31` | **High** — serialize first load per context |
| B4 | Preflight fails **open** on any parse error (empty catch) | `AssemblyPreflightAnalyzer.cs:107-112` | **High** — malformed PE bypasses the scan silently |
| B5 | Name-based ctor makes `CreateInstance` always throw; skips preflight + integrity | `AssemblyContext.cs:453,900,959,799-841` | **High** — use the path ctor only |
| B6 | Confinement failure is `ArgumentOutOfRangeException`, not `SecurityException` | `AssemblyContext.cs:1194` | **High** — easy to mis-catch |
| B7 | `LoadFromFile`/`MergeFromFile` don't normalize paths → relative CSV entries never match | `AssemblyHashStore.cs:219,288` vs `:88` | **High** — silent trust-store failure |
| B8 | Base-path check is `StartsWith`, not component-wise; no symlink resolution | `AssemblyContext.cs:1192` | **Medium-High** — sibling-prefix + symlink escape |
| B9 | Extensionless paths pass `VerifyPath` (asserted by a misleadingly named test) | `AssemblyContext.cs:1160`; `SecurityViolationTests.cs:266-278` | **Medium** — add your own extension gate |
| B10 | Dependency loads use `"*"` — not confined to `basePathRestriction` | `AssemblyContext.cs:592` | **Medium** — validate/strip `deps.json` |
| B11 | Forbidden-directory list is Windows-only | `AssemblySecurityPolicy.cs:28-35,155` | **Medium** on Linux/macOS — supply a custom list |
| B12 | Custom policy ctor silently disables dynamic blocking **and** preflight | `AssemblySecurityPolicy.cs:135-136`; gate at `AssemblyContext.cs:685` | **Medium** — re-enable explicitly |
| B13 | `WildcardPathRestrictionUsed` is raised in the ctor → unsubscribable | `AssemblyContext.cs:416-419` | **Medium** — enforce in host code |
| B14 | `learningMode` defaults to `true` | `AssemblyIntegrityVerifier.cs:95` | **Medium** — TOFU in production if forgotten |
| B15 | Hash CSV is an unsigned trust anchor keyed by absolute machine paths | `AssemblyHashStore.cs:263-266,141-165` | **Medium** — protect it; not portable |
| B16 | `CreateInstance(string)` suffix-matches across **dependencies**, first match wins | `AssemblyContext.cs:915-919` | **Medium** — type confusion; use `GetTypes<T>` |
| B17 | Timeout unblocks the caller but cannot abort the load | `AssemblyContext.cs:709-747` | **Medium** — dispose after `TimeoutException` |
| B18 | Integrity check → load is a TOCTOU window | `AssemblyContext.cs:699` → `:707` | **Medium** — keep plugin dirs read-only |
| B19 | Path-traversal and forbidden-dir-event tests are **skipped** | `SecurityViolationTests.cs:95,285` | **Medium** — write your own coverage |
| B20 | Global monitoring adds a process-wide `AssemblyLoad` handler under a global lock, unbenchmarked, audit-only | `AssemblyContext.cs:491,508`; `docs/TESTING-DYNAMIC-ASSEMBLY-MONITORING.md:268` | **Medium** — measure before enabling |
| B21 | `IAssemblyContext` exposes no events/policy | `IAssemblyContext.cs:6-92` | **Medium** — wrap the concrete type |
| B22 | `Unload()` has four indistinguishable `false` returns; unload is terminal | `AssemblyContext.cs:1235-1248` | **Low-Medium** — no reload; opaque diagnostics |
| B23 | `DisposeAsync` swallows unload exceptions and skips global-monitor deregistration | `AssemblyContext.cs:1421-1428,1395` | **Low-Medium** |
| B24 | Finalizer skips unload entirely | `AssemblyContext.cs:1382-1400,1456-1459` | **Low** — always dispose deterministically |
| B25 | Deprecated statics are silent no-ops (`IsStrictDirectoryRestrictionEnabled()` hardcoded `false`) | `AssemblyContext.cs:318-336` | **Low-Medium** — silent downgrade for ported code |
| B26 | Version skew: pkg 2.1.2 / assembly 2.1.1.0 / CHANGELOG stops at 2.1.0 | `Xcaciv.Loader.csproj:8-10`; `CHANGELOG.md:48` | **Low** — provenance noise |
| B27 | SSEM scores are self-assessed and disagree between documents (8.9 vs 9.0) | `ssem-scoring-methodology.md:367`; `V2.0-Release Notes.md:18` | **Low** — read as intent, not assurance |
