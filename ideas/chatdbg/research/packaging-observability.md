# Packaging, distribution, startup performance and observability

> Research date: **2026-08-28**. Every version number, publish date and capability claim below was checked against the live web on that date; anything I could not confirm is listed under [Unconfirmed](#unconfirmed--everything-i-could-not-verify-on-the-live-web-and-where-i-looked) rather than guessed at.
>
> Scope: the ChatDbg rebuild — a terminal chat/debug shell hosting **Xcaciv.Cupcake**-patterned application composition, tools defined via **Xcaciv.Command** and loaded via **Xcaciv.Loader**, talking to Azure OpenAI, Amazon Bedrock and a local llama.cpp GGUF model, shipping as a self-contained binary for Windows and Linux.

---

## Bottom line — the recommendation in three sentences

**Ship `net10.0`, self-contained, single-file, ReadyToRun, *untrimmed*, one binary per RID (`win-x64`, `linux-x64`, `linux-arm64`, `win-arm64`), with plugin assemblies deliberately kept *outside* the bundle via `ExcludeFromSingleFile` and the llama.cpp native backends laid out on disk in the `runtimes/` shape LLamaSharp probes for.** Native AOT is **categorically unavailable** to this rebuild — not because of llama.cpp (P/Invoke is fine under AOT), but because `Xcaciv.Loader` loads plugin assemblies at runtime, and Native AOT's documented first limitation is "No dynamic loading, for example, `Assembly.LoadFile`" ([Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)); trimming fails for the same reason and must be off, so the source project's `Compact` configuration cannot be carried forward. For observability, take `OpenTelemetry` 1.18.0 + `Microsoft.Extensions.AI`'s built-in `UseOpenTelemetry()` middleware wired to an **`ActivityListener`/`MeterListener` that is off unless the user opts in**, keep the OTLP exporter package referenced but *unconfigured by default* so the tool never opens a socket it wasn't asked to, and use `Microsoft.Extensions.Logging` with a JSON file sink as the baseline (Serilog only if you need its sink ecosystem).

---

## Landscape — the real options

### Runtime / SDK targets

| Option | Current version | Status | Released / last published | One-line verdict |
|---|---|---|---|---|
| **.NET 10** | 10.0.x (LTS) | **GA** | Nov 2025; supported to **10 Nov 2028** ([releases-and-support](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support)) | **Target this.** Three-year LTS, every packaging feature the rebuild needs, and the source already pins `net10.0`. |
| .NET 11 | 11.0.0-preview.7 (docs "last updated for Preview 7", 12 Aug 2026) | **Preview** | GA expected **Nov 2026** ([what's new](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview)) | STS (24-month) only. Real wins for this app exist (Zstandard in `System.IO.Compression`, `Microsoft.Extensions.Diagnostics` Activity tracing *rules*, Podman multi-arch container publish) but it is preview and STS. **Fallback if you want 11 features: stay on 10, revisit Dec 2026.** |
| .NET 8 / .NET 9 | — | **End of support 10 Nov 2026** ([.NET blog](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/)) | Do not target. |

### Publish modes

| Mode | Status | Verdict for this app |
|---|---|---|
| Framework-dependent | GA | Smallest artifact, but forces users to install a runtime. Wrong for a "download one file and run it" debug shell. |
| Self-contained (folder) | GA | Correct semantics, wrong ergonomics — hundreds of files. Useful as the *container* payload. |
| **Single-file, self-contained, R2R, untrimmed** | GA | **The recommendation.** Bundle managed assemblies + runtime; keep plugins and llama natives loose beside it. |
| Single-file **with** `EnableCompressionInSingleFile` | GA | ~Halves size, costs startup (decompress into memory every launch). Docs explicitly say *"measure both the size change and startup cost"* ([single-file overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)). **Default off** for an interactive REPL; offer as a "slim" asset. |
| ReadyToRun (`PublishReadyToRun`) | GA | **On.** ~30% of the JIT→AOT startup gap for zero code change; costs 2–3× assembly size, which compression partly reclaims. |
| ReadyToRun **Composite** | GA | Off. Docs: *"compilation speed is significantly decreased, and the overall file size of the application is significantly increased"*; only recommended when tiered compilation is disabled. |
| **Native AOT (`PublishAot`)** | GA (the feature) | **Unusable here.** See the definitive answer below. |
| Trimming (`PublishTrimmed`) | GA | **Off.** Fundamentally at odds with `Xcaciv.Loader`. |
| SDK container publish (`/t:PublishContainer`) | GA since .NET 8 SDK | Viable *secondary* channel (CI, headless/scripted use). Not the primary distribution. |
| `dotnet tool` global tool package | GA; **massively upgraded in .NET 10** | **Strong secondary channel.** Multi-RID tool packages + `dotnet tool exec` / `dnx`. |

### Packages you would actually reference

| Package | Latest stable | Published | Status | Verdict |
|---|---|---|---|---|
| `OpenTelemetry` | **1.18.0** | 2026-08-21 | **GA** | Take it. `net8.0`/`netstandard2.0`/`net462`. ([nuget](https://www.nuget.org/packages/OpenTelemetry/)) |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | **1.18.0** | 2026-08-21 | **GA** | Reference it, but **do not configure it by default**. ([nuget](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol/)) |
| `OpenTelemetry.Exporter.Console` | **1.18.0** | 2026-08-21 | GA package, but self-described as *"intended for debugging and learning purposes… not recommended for production use"*, output format unstandardised ([nuget](https://www.nuget.org/packages/OpenTelemetry.Exporter.Console)) | Use for `--trace-to-console` diagnostics only. |
| `Microsoft.Extensions.AI` | **10.9.0** | 2026-08-11 | **GA** (27.2M total downloads) | Take it — it *is* the telemetry middleware. ([nuget](https://www.nuget.org/packages/Microsoft.Extensions.AI/)) |
| `Microsoft.Extensions.AI.Abstractions` | **10.9.0** | 2026-08-11 | **GA** | Carries `UsageDetails`, the unified token-accounting type. |
| `Serilog` | **4.4.0** | 2026-07-10 | GA, actively maintained (4.3.1 Feb 2026, 4.3.0 May 2025) | Optional. See §5. ([nuget](https://www.nuget.org/packages/serilog/)) |
| `LLamaSharp` | **0.27.0** | 2026-04-26 | GA-ish (0.x, but shipping every ~2 months) | Depends on `Microsoft.Extensions.AI.Abstractions >= 10.4.1` and `System.Text.Json >= 10.0.5`. ([nuget](https://www.nuget.org/packages/LLamaSharp)) |
| `AWSSDK.BedrockRuntime` | **4.0.101.4** | 2026-08-24 | **GA**, extremely active | Fine. ([nuget](https://www.nuget.org/packages/AWSSDK.BedrockRuntime)) |
| `Azure.AI.OpenAI` | **2.1.0** | **2024-12-06** | GA **but 20 months stale**; newest is `2.9.0-beta.1` (2026-03-13) | ⚠️ **Stated risk.** The stable line has not shipped in ~20 months while eight prereleases have. Plan for either pinning 2.1.0 or consuming a beta. |
| `Spectre.Console` | **0.57.2** | 2026-07-02 | GA (pre-1.0); prerelease `0.57.3-alpha.0.8` on 2026-08-27 | Core library is AOT-capable as of [PR #1690](https://github.com/spectreconsole/spectre.console/pull/1690); irrelevant here since we aren't AOT-ing. |
| `Spectre.Console.Cli` | 0.5x | — | GA but **explicitly `RequiresDynamicCode`**, "not trimmable and not appropriate for AOT scenarios" ([issue #1332](https://github.com/spectreconsole/spectre.console/issues/1332)) | Doesn't matter for us — but note it, because it would have killed the source project's `Compact` config too. |
| `Terminal.Gui` | **2.4.17** | 2026-07-07 | **GA (v2 shipped)**; prerelease 2.4.18-develop.53 on 2026-08-27; targets `net10.0` | The source pins **1.19.0**. v2 is a breaking redesign (instance-based model, `IRunnable`). ([nuget](https://www.nuget.org/packages/Terminal.Gui)) |
| `sign` (Microsoft signing CLI) | **0.9.1-beta.26371.2** | 2026-08-03 | ⚠️ **Prerelease only — no stable release has ever shipped**; 2.8M total downloads | Requires `--prerelease` to install. Requires Windows x64 + .NET 8 SDK+ + VC++14 runtime. ([nuget](https://www.nuget.org/packages/sign)) |
| `Xcaciv.Loader` / `Xcaciv.Command` / `Xcaciv.Cupcake` | — | — | ⚠️ **Not published on nuget.org.** A `packageid:Xcaciv.Loader` query returns `totalHits: 0`; a broad `q=Xcaciv` search returns only `XCBatch.Core` / `XCBatch.Interfaces` from the same owner | **Stated risk:** the plugin-hosting stack is a private/source dependency. Every claim below about how it loads assemblies is inferred from its stated role, not from published docs. |

---

## Analysis

### 1. The publish modes and their real tradeoffs, as of now

#### Measured numbers

The cleanest current head-to-head I could find is a .NET 11 RC2 benchmark on a Ryzen 9 7950X / Ubuntu 24.04 (minimal ASP.NET Core API; startup = median of 50 cold launches polled to first HTTP 200; memory = `VmRSS` post-warmup) — [startdebugging.net, May 2026](https://startdebugging.net/2026/05/native-aot-vs-readytorun-vs-jit-in-dotnet-11/):

| Metric | Plain JIT | ReadyToRun | Native AOT |
|---|---|---|---|
| Startup (time to first request) | 118 ms | **84 ms** | **37 ms** |
| Steady-state throughput | **412k req/s** | 410k req/s | 396k req/s |
| Working set after warmup | 41 MB | 39 MB | **18 MB** |
| Publish size | 4.3 MB + runtime | 91 MB | **13 MB** |

Corroborating data points: a .NET 10 console app publishes to **1.05 MB** with AOT (13.9% smaller than .NET 9), and the Aspire CLI ships at **~15 MB** AOT ([code.soundaranbu.com](https://code.soundaranbu.com/state-of-nativeaot-net10)). A separate .NET 10 console measurement puts a JIT app at 170 ms total vs. Native AOT at 69 ms.

**Read those numbers carefully for *this* app.** They are a web-server benchmark. This is an interactive REPL where the dominant cost is not process startup but the first llama.cpp model load (seconds, sometimes tens of seconds, for a multi-GB GGUF) or the first HTTPS handshake to Azure/Bedrock. A 118 ms → 37 ms startup improvement is invisible next to loading a 4 GB Q4 model. **Startup optimisation has a low ceiling here and should not be allowed to drive the packaging decision.** ReadyToRun buys the meaningful, free part of it.

#### Mode-by-mode

| Mode | Startup | Binary size | Hard compatibility constraints |
|---|---|---|---|
| **Framework-dependent** | Baseline (JIT everything) | Smallest app (~MB), but user must install a matching runtime | Requires a compatible .NET runtime on the machine. Kills the "self-contained binary" requirement. |
| **Framework-dependent + single-file** | Baseline | Small | Same runtime prerequisite; the single-file API incompatibilities still apply. |
| **Self-contained (folder)** | Baseline | ~70–90 MB unpacked before trimming | Per-RID. Every file loose on disk. |
| **Self-contained + single-file** | Baseline (managed assemblies are loaded **from memory**, not extracted) | One file, ~40–70 MB pre-compression for this dependency set | Per-RID. `Assembly.Location` returns `""`; `Assembly.CodeBase`/`EscapedCodeBase` throw `PlatformNotSupportedException`; `Assembly.GetFile(s)` throw `IOException`; `Module.FullyQualifiedName`/`Module.Name` return `<Unknown>`; `Marshal.GetHINSTANCE` returns `-1`. Use `AppContext.BaseDirectory` and `Environment.ProcessPath` instead. ([single-file overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)) |
| **+ `IncludeNativeLibrariesForSelfExtract=true`** | +extraction on first run per version | One file, larger | **Extraction to disk happens before the app starts**: `DOTNET_BUNDLE_EXTRACT_BASE_DIR` if set, else `$HOME/.net` (Linux/macOS) or `%TEMP%/.net` (Windows). Docs warn these directories must not be writable by differently-privileged users, and that under `systemd` `$HOME` is often undefined so you must set `DOTNET_BUNDLE_EXTRACT_BASE_DIR` explicitly. |
| **+ `IncludeAllContentForSelfExtract=true`** | Slowest (everything hits disk) | Largest | ⚠️ Docs: *"This mode is not recommended: it's a .NET Core 3.1 compatibility mode and might be removed in a future release."* **The source project sets this to `true` in its `SingleFile` configuration** (`src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj`) — do not carry it forward. |
| **+ `EnableCompressionInSingleFile=true`** | Slower — assemblies decompress into memory at launch | Significantly smaller | Docs: *"Compression comes with a performance cost… We recommend that you measure both the size change and startup cost."* |
| **ReadyToRun** | Faster first-use of *all* code, not just `Main` | Assemblies grow **2–3×** | RID-specific. C++/CLI ineligible. JIT still runs (generics across assemblies, native interop, hardware intrinsics the compiler can't prove safe, dynamic methods). Tiered compilation replaces hot R2R methods with JIT output. Cross-compile is supported: Windows-x64 SDK can target Windows x86/x64/Arm64, Linux x64/Arm/Arm64 and macOS x64/Arm64. ([ReadyToRun](https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run)) |
| **Native AOT** | Fastest | Smallest self-contained | The full limitation list is in §2. Platform support (.NET 9+): Windows x64/Arm64/x86, Linux x64/Arm64/Arm, macOS x64/Arm64. Requires a C++ toolchain at publish time (VS "Desktop development with C++" on Windows; `clang` + `zlib1g-dev` on Ubuntu). A Linux AOT binary built on Ubuntu 20.04 runs on 20.04 **and later**, not earlier. |

---

### 2. The hard question: can a plugin-hosting shell that also loads native llama.cpp backends be Native AOT compiled?

**No. Definitively no.** And it is worth being precise about *which* half causes the failure, because the intuitive answer is wrong.

#### The native llama.cpp half is fine

Native AOT supports P/Invoke normally. From [Native code interop with Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/interop): *"The P/Invoke calls in AOT-compiled binaries are bound lazily at runtime by default, for better compatibility."* You can even opt into `<DirectPInvoke>` / `<NativeLibrary>` to statically link `libllama.a`/`llama.lib`, and export managed entry points with `[UnmanagedCallersOnly]`. LLamaSharp's `NativeLibraryConfig` machinery — `WithLibrary(path)`, `WithSearchDirectory(dir)` — is ordinary `NativeLibrary.Load`-shaped work over file paths. **Loading `libllama.so`/`llama.dll` is not the blocker.**

#### The plugin half is fatal

Native AOT's documented limitations, verbatim from [the overview page](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/):

- **"No dynamic loading, for example, `Assembly.LoadFile`."**
- "No runtime code generation, for example, `System.Reflection.Emit`."
- "No C++/CLI."
- "Windows: No built-in COM."
- **"Requires trimming, which has limitations."**
- "Implies compilation into a single file, which has known incompatibilities."
- "Apps include required runtime libraries…"
- "`System.Linq.Expressions` always use their interpreted form, which is slower than runtime generated compiled code."
- "Generic parameters substituted with struct type arguments have specialized code generated for each instantiation… In Native AOT, all instantiations are pre-generated. This can have significant impact to the disk size of the application."
- "Not all the runtime libraries are fully annotated to be Native AOT compatible."

**Why dynamic loading and AOT are fundamentally at odds — the mechanism, not the slogan:**

Native AOT compiles IL to machine code at *publish* time and ships no JIT. The compiled image contains exactly the code the ILC compiler could prove reachable from the entry point, plus the runtime type metadata for exactly those types. There is no compiler in the process at runtime. A plugin assembly discovered on disk at runtime contains IL that was never seen by ILC, so:

1. There is nothing in the process capable of turning that IL into executable code — the JIT is not present, and `Reflection.Emit` is unavailable.
2. Even if there were, the generic instantiations, interface dispatch stubs and reflection metadata the plugin's types would need were never generated, because ILC's whole-program closure did not include them.
3. Consequently `AssemblyLoadContext.LoadFromAssemblyPath` / `Assembly.LoadFile` are not merely slow or lossy under AOT — the code paths throw `PlatformNotSupportedException` ("Operation is not supported on this platform"). `System.Runtime.Loader.AssemblyDependencyResolver` — the type a well-built plugin loader uses to resolve a plugin's private NuGet dependencies from its `.deps.json` — likewise does not support NativeAOT ([dotnet/sdk#42389](https://github.com/dotnet/sdk/issues/42389), still open as a feature request).

This is not a bug awaiting a fix. It is the definition of the deployment model: AOT trades away the ability to execute code that did not exist at compile time, in exchange for having no compiler at runtime.

**Trimming compounds it independently.** From [Known trimming incompatibilities](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities): *"Trimming and dynamic assembly loading is a common problem for systems that support plugins or extensions… Trimming relies on seeing all assemblies at build time, so it knows which code is used and can't be trimmed away. Most plugin systems load third-party code dynamically, so it's not possible for the trimmer to identify what code is needed."* Even the *host* half is at risk: the trimmer will remove the very host types a plugin resolves against, because nothing statically references them. Since AOT **requires** trimming, you inherit this failure even if the loading problem were somehow solved.

#### What the source project actually did, and why it doesn't transfer

`src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj` defines a `Compact` configuration with `PublishAot=true`, `PublishTrimmed=true`, `TrimMode=full`, `IlcOptimizationPreference=Size`, `IlcGenerateStackTraceData=false`, `InvariantGlobalization=true`, `UseSystemResourceKeys=true` — **and `SuppressTrimAnalysisWarnings=true`**. That last property silences precisely the diagnostics that would have told the authors whether the build was sound. `docs/compact-build.md` claims Compact builds land at 8–15 MB with `<100ms` startup and 5–15 minute build times, and SingleFile at 15–25 MB with 200–500 ms startup — but that document also says the output path is `bin\Compact\net9.0\...` while the project targets `net10.0`, and the checked-in GitHub Actions workflow (`.github/workflows/build-release.yml`) installs `dotnet-version: '9.0.x'` and **only ever publishes the `SingleFile` configuration**, never `Compact`. The only published artifact in the tree, `test-publish/Xcaciv.ChatDbg.Shell`, is a **15,677,171-byte stripped linux-x64 ELF** — a SingleFile build. The AOT configuration existed on paper; the shipping pipeline did not use it.

That was survivable only because the source has **no plugin loading at all** — the inventory records "There is **no** dependency-injection container anywhere in the product; all wiring is `new`-in-constructor," and commands are hand-registered into a `Dictionary<string, ICommand>`. The rebuild changes exactly that premise. **The `Compact` configuration must be deleted, not ported.**

#### The correct publish mode for a plugin-hosting shell

| Requirement | Verdict |
|---|---|
| Load plugin assemblies at runtime | Requires a JIT ⇒ **CoreCLR**, not Native AOT |
| Trim the app | **Impossible** while plugins exist ⇒ `PublishTrimmed=false` |
| One downloadable artifact | **`PublishSingleFile=true`** |
| No runtime prerequisite | **`SelfContained=true`** |
| Recover some startup | **`PublishReadyToRun=true`** |
| Plugins must be discoverable and swappable | **Plugins live *outside* the bundle**, in a `plugins/` directory beside the executable, resolved from `AppContext.BaseDirectory` |

The single-file docs support this directly — `ExcludeFromSingleFile` exists for exactly this shape:

```xml
<ItemGroup>
  <Content Update="plugins\**\*.dll">
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    <ExcludeFromSingleFile>true</ExcludeFromSingleFile>
  </Content>
</ItemGroup>
```

**Second-best option, and when it wins:** a **Native AOT core with a fixed, compile-time tool registry** — no `Xcaciv.Loader`, tools registered by a source generator or an explicit `static` table, plugins delivered as *out-of-process* executables spoken to over stdio/MCP rather than as in-process assemblies. This wins if (a) startup latency becomes a genuine product requirement (e.g. the shell is invoked per-keystroke by an editor integration), (b) you must run in a JIT-prohibited environment, or (c) memory footprint matters more than extensibility (18 MB vs. 41 MB RSS in the benchmark above). It costs you the entire in-process plugin architecture, which is the stated design. A third path — **AOT host + out-of-process plugin workers** — preserves both properties at the cost of an IPC layer and process lifecycle management; consider it only if AOT ever becomes non-negotiable.

**What you give up by choosing single-file + R2R + no trimming:** ~37 ms startup becomes ~84–118 ms (invisible against model load); ~18 MB RSS becomes ~41 MB; and the binary is roughly 3–5× larger than an AOT equivalent. You keep: plugins, reflection, `Reflection.Emit`, every NuGet package regardless of AOT annotation, full debugger/profiler support, and a build that takes seconds instead of 5–15 minutes.

---

### 3. Trimming: what breaks it, the warning workflow, and trim-safe serialisation

**You will not be trimming this application.** This section exists for two reasons: (a) if the plugin architecture is ever scoped down, you need to know the workflow; (b) *most of the trim-safety hygiene is worth doing anyway*, because it also makes the app faster, more predictable and safer to ship.

#### What breaks trimming

From [Known trimming incompatibilities](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities):

| Pattern | Why it breaks | Trim-safe alternative |
|---|---|---|
| **Reflection-based serializers** (Newtonsoft.Json) | Reflection over types the trimmer can't see | **Source-generated `System.Text.Json`** |
| `System.Configuration.ConfigurationManager` | Same | Configuration-binding source generator |
| `BinaryFormatter` | Same, plus it's removed for security | Migrate away entirely |
| **Runtime code generation** (`System.Reflection.Emit`) | No IL to compile after trimming | Source generators / generics |
| **Dynamic assembly loading** (`Assembly.LoadFrom`, `AssemblyLoadContext`) | Trimmer can't see plugin code, and removes host code plugins need | **None. This is the blocker.** |
| C++/CLI, built-in COM marshalling (Windows) | Runtime code analysis the trimmer can't model | `ComWrappers` for COM |
| WPF, WinForms | Trim support is *disabled in the SDK* | N/A (we're a terminal app) |

#### The trim-warning workflow

The rule from [Fixing trim warnings](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/fixing-warnings) is blunt: **"An app that uses trimming shouldn't produce any trim warnings."** The source project's `SuppressTrimAnalysisWarnings=true` is the exact opposite of this.

Warnings fall in two families:
- **`RequiresUnreferencedCode`** (callers get **IL2026**) — the code fundamentally can't be analysed.
- **`DynamicallyAccessedMembers`** (IL2070/IL2087/IL2091 family) — reflection over compile-time-known types; satisfiable.

The prescribed order of attack:
1. **Eliminate reflection** — replace `Activator.CreateInstance(Type)` with `new T()` behind a generic; use source generators.
2. **Annotate with `[DynamicallyAccessedMembers]`** — start at the reflection call, then propagate *backwards* through every parameter, field and generic parameter in the call chain until the `typeof(...)` origin. Use the narrowest `DynamicallyAccessedMemberTypes` that works; `All` "significantly increases app size" and drags in members that may themselves carry unresolvable `RequiresUnreferencedCode`.
3. **Mark with `[RequiresUnreferencedCode("…")]`** — for genuinely dynamic code (plugin loading is the canonical example the docs themselves give). Write a message that names the alternative.
4. **Suppress with `[UnconditionalSuppressMessage]`** — last resort, smallest possible scope (extract the call into a local function and annotate that). ⚠️ **`#pragma warning disable` and `[SuppressMessage]` do not work** — "The trimmer operates on compiled assemblies and won't see these suppressions."

Tooling knobs: set `<IsAotCompatible>true</IsAotCompatible>` in libraries (this defaults `IsTrimmable`, `EnableTrimAnalyzer`, `EnableSingleFileAnalyzer`, `EnableAotAnalyzer` to `true`). Set `<TrimmerSingleWarn>false</TrimmerSingleWarn>` to see every warning instead of one summary per assembly. **New in .NET 10:** `<VerifyReferenceAotCompatibility>true</VerifyReferenceAotCompatibility>` emits **IL3058** for any referenced assembly lacking the `IsAotCompatible` metadata — but note that metadata only exists on libraries built with .NET 10+, so older-but-fine packages will warn.

**Note for this app specifically:** even with trimming off, `<EnableSingleFileAnalyzer>true</EnableSingleFileAnalyzer>` **is worth turning on**, because it catches the `Assembly.Location`-returns-empty-string class of bug at build time. Single-file publishing is on our critical path; trimming is not.

#### Making serialisation trim-safe (and worth doing regardless)

The app persists `ChatSettings`, `ChatHistory`, `SystemPrompt`, and exports token-analysis results — all through `System.Text.Json` with explicit `[JsonPropertyName]`/`[JsonIgnore]`. Convert all of it to source generation ([docs](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)):

```csharp
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ChatSettings))]
[JsonSerializable(typeof(ChatHistory))]
[JsonSerializable(typeof(SystemPrompt))]
[JsonSerializable(typeof(TokenInspectionResult))]
[JsonSerializable(typeof(TokenProbabilityMapResult))]
[JsonSerializable(typeof(List<TokenLogProbabilities>))]
internal partial class ChatDbgJsonContext : JsonSerializerContext;
```

Non-obvious rules that will bite this codebase:

- **`object`-typed members are the exception.** "Members declared as `object` are an exception to this rule. The runtime type for a member declared as `object` needs to be specified" with its own `[JsonSerializable]`. Any `Dictionary<string, object>` "additional properties" bag — which provider-response models tend to accumulate — needs every concrete value type declared.
- **`JsonSourceGenerationMode.Serialization`** (fast-path) **is not supported for async serialisation**. Streaming export of a large token-probability map falls back to metadata mode. Leave the default (both modes) unless you have a reason.
- **The non-generic `JsonStringEnumConverter` is not supported by Native AOT.** Use `JsonStringEnumConverter<TEnum>`, or `[JsonSourceGenerationOptions(UseStringEnumConverter = true)]` for a blanket policy.
- **Fail fast on reflection.** Set `<JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>`. This turns a silent reflection fallback into an `InvalidOperationException` with a descriptive message, *consistently on CoreCLR and AOT alike* — you find the gap on your dev box rather than in a user's trimmed build. (It is auto-disabled anyway when `PublishTrimmed` is on.)
- **Plugins contributing serialisable types is the interesting case.** Since each plugin ships its own `JsonSerializerContext`, compose them at load time:
  ```csharp
  options.TypeInfoResolverChain.Add(pluginContext);   // append
  options.TypeInfoResolverChain.Insert(0, hostContext); // host wins
  ```
  Order is significant — the chain returns the first non-null result. This is the correct architecture even untrimmed, because it gives each plugin a compile-time-checked serialisation contract instead of ambient reflection.

---

### 4. Cross-platform distribution

#### Per-RID builds

Single-file, self-contained and R2R are all **per-RID**: *"Single file apps are always OS and architecture specific. You need to publish for each configuration."* The matrix for this app:

| RID | Build on | Notes |
|---|---|---|
| `win-x64` | `windows-latest` | Primary. |
| `win-arm64` | `windows-latest` (cross) | R2R cross-compilation from Windows x64 is supported. |
| `linux-x64` | `ubuntu-latest` | Build on the **oldest** glibc you intend to support; the binary runs on that version and newer, not older. |
| `linux-arm64` | `ubuntu-24.04-arm` runner, or cross-R2R from a Linux x64 SDK | R2R cross-compile Linux x64 → Linux Arm64 is supported. |
| `linux-musl-x64` | Alpine container | Only if you promise Alpine support; **it is a separate RID and a separate llama.cpp backend build.** |
| `osx-arm64` / `osx-x64` | `macos-latest` | Out of stated scope, but if added: publish per-arch and merge with `lipo`, then re-`codesign`. |

⚠️ **The sharpest edge in the whole document is here.** LLamaSharp's backend packages ship *multiple* CPU-feature variants of the same native filenames (`avx/ggml.dll`, `avx2/ggml.dll`, `avx512/ggml.dll`, plus CUDA variants). Publishing self-contained with more than one backend referenced produces **`NETSDK1152: Found multiple publish output files with the same relative path`**. The documented workaround — `<ErrorOnDuplicatePublishOutputFiles>false</ErrorOnDuplicatePublishOutputFiles>` — "results in only one `ggml.dll` and one `llama.dll`, and it's not clear or predictable which one you end up getting." [SciSharp/LLamaSharp#977](https://github.com/SciSharp/LLamaSharp/issues/977) was **closed as "not planned"**; the maintainers pointed at whisper.net's approach of preserving separate runtime folders. The source project references **both** `LLamaSharp.Backend.Cpu` **and** `LLamaSharp.Backend.Cuda12` in `Xcaciv.ChatDbg.Core.csproj`, so the rebuild inherits this collision on day one.

**The workable shape:** do **not** let MSBuild flatten the backends. Keep the `runtimes/<rid>/native/**` tree intact as loose files beside the single-file executable, and call `NativeLibraryConfig.Instance.WithSearchDirectory(Path.Combine(AppContext.BaseDirectory, "runtimes"))` at startup, letting LLamaSharp's own device-probing pick the variant. Ship **one backend family per release asset** (`chatdbg-linux-x64-cpu`, `chatdbg-win-x64-cuda12`) rather than one universal binary — this is what the CUDA/Vulkan split forces anyway, since bundling every backend would add gigabytes.

#### SDK container publishing

`dotnet publish --os linux --arch x64 /t:PublishContainer` ([docs](https://learn.microsoft.com/en-us/dotnet/core/containers/sdk-publish)). Key facts:

- **The .NET SDK creates container images without Docker.** Docker/Podman is needed only to *run* the image locally. You can push straight to a registry (`-p ContainerRegistry=ghcr.io`) or emit a tarball (`-p ContainerArchiveOutputPath=./images/chatdbg.tar.gz`) with **no daemon at all** — which is exactly what a security-scanning CI stage wants.
- Properties: `ContainerRepository`, `ContainerImageTag(s)`, `ContainerRegistry`, `ContainerBaseImage`, `ContainerFamily` (for chiseled/distroless variants).
- **.NET 10:** console apps can create container images without `<EnableSdkContainerSupport>`; new `<ContainerImageFormat>` (`Docker` | `OCI`).
- **.NET 11 (preview):** multi-arch builds with Podman; the SDK now prefers platform-native local runtimes (`wslc` on Windows, `container` on macOS) before Docker/Podman.

**Verdict for this app:** a container is a *good* secondary channel — CI use, scripted/headless invocation, reproducible llama.cpp backends — but a poor primary one. This is an interactive TTY application that reads and writes user-profile config, so containerising it means solving TTY allocation, volume-mounting the GGUF model and the settings directory, and passing credentials in. Ship it, document `docker run -it --rm -v ~/.chatdbg:/root/.chatdbg -v /models:/models`, and do not make it the recommended install. **Do not use a chiseled/distroless base** — a debug shell that can't shell out is a debug shell with one hand tied.

#### `dotnet tool` packaging

.NET 10 turned this from a footnote into a real channel ([.NET 10 SDK what's new](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk)):

- **Multi-RID tool packages.** One package can bundle binaries for every supported platform; the CLI picks the right one at install/run time. Supported variations: framework-dependent platform-agnostic (classic), framework-dependent platform-specific, **self-contained platform-specific**, trimmed platform-specific, **AOT-compiled platform-specific**. "Any publishing options you can use with applications… can apply to tools as well."
- **The `any` RID.** Add `any` to `<RuntimeIdentifiers>` and `dotnet pack` also produces a framework-dependent, platform-agnostic fallback package, so the tool still installs on platforms you never enumerated.
- **`dotnet tool exec`** runs a tool without installing it (prompting before download), honouring a nearby `.config/dotnet-tools.json`. **`dnx <tool>`** is the `npx`-shaped shorthand.
- **`--cli-schema`** emits a JSON description of a command's argument/option/subcommand tree — useful for shell completions and for anything that wants to introspect the tool surface.

**Verdict:** publish `chatdbg` as a multi-RID `dotnet tool` **in the self-contained platform-specific flavour, plus an `any` fallback**. Users who have the SDK get `dnx chatdbg` with zero install; users who don't get the GitHub Release binary. Do **not** publish the trimmed or AOT flavours — same plugin argument as §2.

#### Signing and notarisation

**Windows.** Unsigned Windows binaries hit SmartScreen, which is a hard adoption blocker for a downloaded `.exe`. The current tooling is Microsoft's `sign` CLI (`dotnet tool install --global sign --prerelease`), which supports Authenticode, NuGet, VSIX and ClickOnce, and backs onto Azure Key Vault, **Azure Trusted Signing** (certificate in an Azure HSM, signing by API call, private key never touched) or the local certificate store. ⚠️ **`sign` has never had a stable release** — latest is `0.9.1-beta.26371.2` (2026-08-03) and `--prerelease` is mandatory. It requires Windows x64, .NET 8 SDK+, and the VC++ 14 runtime. Note also that the older `Azure.CodeSigning` package referenced in some docs **has been removed from NuGet**.

**Critically for single-file:** sign the constituent binaries *before* they are bundled. The SDK exposes MSBuild extension points for exactly this — a target `PrepareForBundle` runs before `GenerateSingleFileBundle`, exposing `<FilesToBundle/>` and the `AppHostFile` property. Hook in between:

```xml
<Target Name="SignBeforeBundle" BeforeTargets="GenerateSingleFileBundle" DependsOnTargets="PrepareForBundle">
  <!-- sign each @(FilesToBundle); if the tool copies a file, update the item's path -->
</Target>
```
Then sign the produced single-file executable itself. (If a file comes from the NuGet cache, the signing tool must copy it and rewrite the `FilesToBundle` item to point at the copy.)

**macOS** — out of the stated Windows+Linux scope, but if it is ever added ([docs](https://learn.microsoft.com/en-us/dotnet/core/deploying/macos)):
- The **apphost** is the entry point and must be signed; notarisation is required for distribution outside the App Store.
- **Non-AOT apps require the `com.apple.security.cs.allow-jit` entitlement.** *"For apps published as Native AOT, no entitlements are required."* Since we are explicitly not AOT, `allow-jit` is mandatory.
- Optional: `com.apple.security.get-task-allow` (for `createdump`/`dotnet dump`) and `com.apple.security.cs.debugger`.
- *"Failing to sign and notarize your app might result in the application crashing while executing a restricted operation."*
- Universal binaries: publish per-arch, merge the two executables with `lipo`, then `codesign --force`. Works for both `PublishSingleFile` and `PublishAot` output.

**Linux.** No signing infrastructure is expected. Publish SHA-256 checksums alongside the release assets, and consider a detached GPG signature and/or Sigstore/cosign attestation if the audience warrants it. If you ever ship `.deb`/`.rpm`, repository signing becomes a separate problem.

---

### 5. Observability

#### OpenTelemetry for .NET — current state

`OpenTelemetry` **1.18.0**, GA, published **2026-08-21** ([nuget](https://www.nuget.org/packages/OpenTelemetry/)); the OTLP exporter tracks it at 1.18.0 same-day, targeting `net8.0`/`net9.0`/`net10.0`/`netstandard2.0`/`netstandard2.1`/`net462`. Shipping cadence is roughly monthly (1.17.0 Jul 2026, 1.16.0 Jun 2026, 1.15.3 Apr 2026, 1.14.0 Nov 2025). This is a mature, unambiguously GA stack.

The .NET side is unusually clean because **the instrumentation API is in the BCL**, not the OTel package: `System.Diagnostics.ActivitySource` for spans, `System.Diagnostics.Metrics.Meter` for metrics, `ILogger` for logs. You can instrument the whole app with zero OpenTelemetry references, and only pull in the SDK if and when someone wants to *export*. **For a local developer tool that must not phone home, this is the single most important architectural fact in this section** — see §7.

AOT compatibility, for completeness: the tracking issue [opentelemetry-dotnet#3429](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3429) ("OpenTelemetry .NET SDK is not AOT safe") is **closed**, with `PropertyFetcher` made AOT-compatible in #4675. But instrumentation **assembly scanning is not supported under AOT**, and the auto-instrumentation product (`opentelemetry-dotnet-instrumentation`) relies on startup hooks / the CLR Profiler API, **neither of which works with AOT-published apps**. Moot for us.

#### The GenAI semantic conventions — are they stable? **No.**

This is the question most likely to be answered wrong from stale training data, so it is worth being exact.

**Nothing in the `gen_ai.*` namespace is Stable.** As of 17 July 2026, no GenAI-specific span, event, metric or attribute is marked Stable; everything carries **Development**. Shared cross-cutting attributes used *alongside* them (`error.type`, `server.address`) are Stable, but that is not the `gen_ai` namespace.

**They also moved.** As of semantic-conventions **v1.42.0 (12 June 2026)**, all `gen_ai.*` content was removed from `open-telemetry/semantic-conventions` and now lives in the dedicated **[`open-telemetry/semantic-conventions-genai`](https://github.com/open-telemetry/semantic-conventions-genai)** repository, on its own release cadence. This is an organisational split to let GenAI move fast, **not a graduation to stable**. `https://opentelemetry.io/docs/specs/semconv/gen-ai/` is now a redirect stub.

**Span naming and required attributes** ([gen-ai-spans.md](https://github.com/open-telemetry/semantic-conventions-genai/blob/main/docs/gen-ai/gen-ai-spans.md)):

| Span kind | Name format |
|---|---|
| Inference | `{gen_ai.operation.name} {gen_ai.request.model}` |
| Embeddings | `{gen_ai.operation.name} {gen_ai.request.model}` |
| Retrieval | `{gen_ai.operation.name} {gen_ai.data_source.id}` |
| Fetch response / Memory | `{gen_ai.operation.name}` |

| Attribute | Requirement | Stability |
|---|---|---|
| `gen_ai.operation.name` | Required | Development |
| `gen_ai.provider.name` | Required | Development |
| `error.type` | Conditionally required | **Stable** |

`gen_ai.operation.name` values: `chat`, `text_completion`, `generate_content`, `embeddings`, `retrieval`, `fetch_response`, `execute_tool`, `invoke_agent`, `invoke_workflow`, `plan`, `create_agent`, plus the memory family (`create_memory`, `search_memory`, `update_memory`, `upsert_memory`, `delete_memory`, `create_memory_store`, `delete_memory_store`).

`gen_ai.provider.name` values relevant here: **`azure.ai.openai`**, **`aws.bedrock`**, `openai`, `azure.ai.inference`, `anthropic`, `cohere`, `deepseek`, `gcp.gemini`, `gcp.vertex_ai`, `groq`, `ibm.watsonx.ai`, `mistral_ai`, `moonshot_ai`, `perplexity`, `x_ai`. ⚠️ **There is no enum value for a locally-hosted llama.cpp/GGUF model.** The list is open, so emit a stable custom value (e.g. `llama.cpp`) and document it.

**Token-usage attributes** (all Recommended, all Development):
`gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`, `gen_ai.usage.cache_read.input_tokens`, `gen_ai.usage.cache_write.input_tokens`, `gen_ai.usage.text.input_tokens`, `gen_ai.usage.text.output_tokens`, `gen_ai.usage.image.input_tokens`, `gen_ai.usage.audio.input_tokens`, `gen_ai.usage.reasoning.output_tokens`.

**Content attributes** (privacy-sensitive, opt-in): `gen_ai.system_instructions`, `gen_ai.input.messages`, `gen_ai.output.messages`.

**Metrics** ([gen-ai-metrics.md](https://github.com/open-telemetry/semantic-conventions-genai/blob/main/docs/gen-ai/gen-ai-metrics.md)) — all **Development**:

| Metric | Instrument | Unit | Key attributes |
|---|---|---|---|
| `gen_ai.client.token.usage` | Histogram | `{token}` | `gen_ai.operation.name`, `gen_ai.provider.name`, `gen_ai.token.type` |
| `gen_ai.client.operation.duration` | Histogram | `s` | `gen_ai.operation.name`, `gen_ai.provider.name`, `error.type` |
| `gen_ai.client.operation.time_to_first_chunk` | Histogram | `s` | operation, provider |
| `gen_ai.client.operation.time_per_output_chunk` | Histogram | `s` | operation, provider |
| `gen_ai.execute_tool.duration` | Histogram | `s` | `gen_ai.tool.name`, `gen_ai.tool.type`, `error.type` |
| `gen_ai.invoke_agent.duration` / `.inference_calls` / `.tool_calls` | Histogram | `s` / `{inference_call}` / `{tool_call}` | agent name, error.type |

**Naming churn you must plan for:** `gen_ai.system` → **`gen_ai.provider.name`** (v1.37.0, Aug 2025); `gen_ai.usage.prompt_tokens`/`completion_tokens` → **`gen_ai.usage.input_tokens`/`output_tokens`**. `invoke_agent` split into client and internal variants in v1.41.0 (Apr 2026); retrieval spans and cache-token attributes added in v1.40.0.

#### `Microsoft.Extensions.AI`'s built-in telemetry middleware

This is the reason not to hand-roll GenAI instrumentation. `OpenTelemetryChatClient` (in `Microsoft.Extensions.AI` 10.9.0, GA) is a decorator in the `IChatClient` pipeline:

```csharp
IChatClient client = baseClient
    .AsBuilder()
    .UseOpenTelemetry(sourceName: "Xcaciv.ChatDbg", configure: c => c.EnableSensitiveData = captureContent)
    .Build();
```

From [the source](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI/ChatCompletion/OpenTelemetryChatClient.cs):

- **`ActivitySource` and `Meter` share one name** — the `sourceName` argument, or `OpenTelemetryConsts.DefaultSourceName`. One string enables or disables all of it.
- It emits `gen_ai.operation.name`, `gen_ai.request.model`, `gen_ai.response.model`, `gen_ai.provider.name`, `gen_ai.request.stream`, `gen_ai.conversation.id`, the full request-parameter set (`temperature`, `top_p`, **`top_k`**, `max_tokens`, `seed`, `frequency_penalty`, `presence_penalty`, `stop_sequences`), `gen_ai.output.type`, `gen_ai.tool.definitions`, `gen_ai.response.finish_reasons`, `gen_ai.response.id`, `gen_ai.response.time_to_first_chunk`, and the usage attributes (`gen_ai.usage.input_tokens`, `output_tokens`, `cache_read_input_tokens`, `reasoning_output_tokens`), plus the four histograms above.
- **`EnableSensitiveData`** gates `gen_ai.system_instructions`, `gen_ai.input.messages`, `gen_ai.output.messages`, tool-call arguments and tool-call results. **Default `false`**, unless the env var `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT` is `"true"`.
- It documents itself as following **GenAI semconv v1.41**, "still experimental and subject to change."

⚠️ **Known gap:** it emits span *attributes* but **not** the `gen_ai.client.inference.operation.details` **ActivityEvents** the spec calls for ([microsoft/agent-framework#3637](https://github.com/microsoft/agent-framework/issues/3637)). If you have a backend that reads those events, you must emit them yourself.

**Second-best option, and when it wins:** hand-rolled `ActivitySource`/`Meter` instrumentation in your own provider adapters. It wins if you are *not* routing all three providers through `IChatClient` — and note that the local llama.cpp path is a genuine question mark, since LLamaSharp depends on `Microsoft.Extensions.AI.Abstractions` (so it speaks the types) but a raw `LLamaSharpService` doing token-level probability work may bypass the `IChatClient` pipeline entirely. **You lose:** free spec conformance, free spec *updates*, and the four histograms. Prefer routing everything through `IChatClient` and pushing token-introspection data into `AdditionalProperties`, so one middleware covers all three backends.

#### Structured logging: `Microsoft.Extensions.Logging` vs. Serilog

| | `Microsoft.Extensions.Logging` (+ `Microsoft.Extensions.Logging.Console`) | Serilog 4.4.0 (+ `Serilog.Extensions.Logging`) |
|---|---|---|
| What it is | The abstraction, plus a first-party console/JSON provider | An implementation behind that abstraction |
| Extra dependencies for a single-file binary | **Zero** (in the shared framework) | +3–4 assemblies |
| Structured output | `LoggerMessage` source generators (`[LoggerMessage]`) give allocation-free, compile-time-checked structured logs, no reflection | Message templates, `{@Object}` destructuring |
| Sinks | Console (incl. JSON formatter), Debug, EventSource, OTel `AddOpenTelemetry()` | Enormous ecosystem: rolling files, Seq, Elasticsearch, … |
| Rolling file to disk | ❌ **Not in the box** | ✅ `Serilog.Sinks.File` with rolling/retention |
| Trim/AOT posture | Best in class — source generators end-to-end | Reported as fully supporting AOT and trimmed .NET 10 apps in 4.x |

**Recommendation: `Microsoft.Extensions.Logging` with `[LoggerMessage]` source generators as the API surface everywhere**, because it is dependency-free, allocation-free, and the same `ILogger` that plugins will already have injected. The one place it falls short is the app's existing **"Diagnostic Logging & Log Export"** feature (`/exportlogs`, `LLamaSharpLogConfig`), which wants rolling files on disk. Two ways to close that:

1. Write a small custom `ILoggerProvider` that appends NDJSON to a rotating file under the app's data directory (≈150 lines, zero dependencies). **Preferred** for a single-file binary where every added assembly is bytes in the download.
2. Add Serilog + `Serilog.Sinks.File` behind `Serilog.Extensions.Logging`, keeping `ILogger<T>` as the app-facing API.

**Serilog wins when** you want rolling-file policy, retention and a rich sink set without writing them; when you want `{@Object}` destructuring of the token-analysis models for free; or when operators already run Seq. **You lose** ~4 assemblies of binary size and a second configuration system. Note that Serilog and OpenTelemetry logs are not mutually exclusive — `ILogger` can fan out to both.

---

### 6. Cost and token accounting

#### What each provider actually returns

| Backend | Where usage comes from | Fields |
|---|---|---|
| **Azure OpenAI** (`Azure.AI.OpenAI` → `OpenAI` .NET SDK) | `ChatCompletion.Usage` (`OpenAI.Chat.ChatTokenUsage`) | `InputTokenCount`, `OutputTokenCount`, `TotalTokenCount`; `InputTokenDetails` (`ChatInputTokenUsageDetails`: **`CachedTokenCount`**, `AudioTokenCount`); `OutputTokenDetails` (**`ReasoningTokenCount`**, `AudioTokenCount`). `OutputTokenCount` is the **sum** of displayed and reasoning tokens. ⚠️ **Streaming returns usage only if you request it** (`stream_options: {include_usage: true}`), and then only on a final chunk. |
| **Amazon Bedrock** (`AWSSDK.BedrockRuntime` 4.0.101.4) | `ConverseResponse.Usage` (`TokenUsage`) and `ConverseResponse.Metrics` | `inputTokens`, `outputTokens`, `totalTokens`, **`cacheReadInputTokens`**, **`cacheWriteInputTokens`**, and `cacheDetails` (a per-TTL breakdown of cache writes, sorted 1h before 5m, empty when nothing was written). `metrics.latencyMs` gives server-side latency. `ConverseStream` reports the same in its terminal `metadata` event. ([TokenUsage API ref](https://docs.aws.amazon.com/bedrock/latest/APIReference/API_runtime_TokenUsage.html)) |
| **Local llama.cpp / GGUF** (LLamaSharp 0.27.0) | Nothing is *returned* — you count it | No billing, no server-side usage object. Prompt tokens come from your own tokenisation pass (which this app performs anyway, for `/tokenize` and attribution); output tokens are the count of sampled tokens. Cost is **0**; the interesting metrics are wall-clock, tokens/sec, and time-to-first-token. |

#### How to record it — one type, one place

`Microsoft.Extensions.AI.UsageDetails` (in `Microsoft.Extensions.AI.Abstractions` 10.9.0) is the normalising type, and it maps onto all three ([API ref](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.usagedetails)):

`InputTokenCount`, `OutputTokenCount`, `TotalTokenCount`, `CachedInputTokenCount`, `ReasoningTokenCount`, `InputTextTokenCount`, `OutputTextTokenCount`, `InputAudioTokenCount`, `OutputAudioTokenCount`, `AdditionalCounts` (a dictionary for anything else — e.g. Bedrock's `cacheWriteInputTokens`, which has no first-class property), and **`Add(UsageDetails)`** for accumulating a session total.

The recording plan:

1. **Each provider adapter populates `UsageDetails`** on its `ChatResponse`. The local backend fills it from its own tokeniser — the same tokeniser the token-inspection feature already runs, so this is nearly free.
2. **`UseOpenTelemetry()` turns it into telemetry automatically** — the `gen_ai.client.token.usage` histogram, dimensioned by `gen_ai.token.type` (input/output), `gen_ai.operation.name` and `gen_ai.provider.name`, plus the span attributes.
3. **Cost is a local, offline computation.** Keep a user-editable price table (per model: input $/Mtok, output $/Mtok, cached-input $/Mtok, reasoning $/Mtok) in the settings file, versioned with an `effectiveDate`, defaulting to `null` (= "cost unknown") rather than a baked-in price that silently goes stale. **Do not fetch prices over the network** — that is a phone-home. Render `/cost` as a session table: per-turn tokens, per-turn cost, running total, with an explicit "prices not configured" state.
4. **Persist the running total in the session/history file**, so `/cost` survives a restart and history export carries the accounting with it.
5. **Cached tokens are the accounting trap.** Both Azure OpenAI (`CachedTokenCount`) and Bedrock (`cacheReadInputTokens`) bill cached input at a discount, and Bedrock bills cache *writes* at a premium. A cost model that only multiplies `InputTokenCount` by the input rate will be materially wrong for any prompt-cached workload — and a chat/debug shell that replays a long system prompt every turn is exactly that workload. Compute as: `(input − cached) × inputRate + cached × cachedRate + cacheWrite × cacheWriteRate + (output − reasoning) × outputRate + reasoning × reasoningRate`.

---

### 7. Recommendation

#### The publish matrix

| Property | Value | Why |
|---|---|---|
| `TargetFramework` | `net10.0` | LTS to Nov 2028; already the source's target. Revisit .NET 11 after Nov 2026. |
| `PublishSingleFile` | `true` | One downloadable artifact. |
| `SelfContained` | `true` | No runtime prerequisite. |
| `PublishReadyToRun` | `true` | ~30% startup for free; the only free win available to a JIT app. |
| `PublishReadyToRunComposite` | `false` | Docs recommend it only for TC-disabled apps; huge size and build-time cost. |
| **`PublishTrimmed`** | **`false`** | **Mandatory.** Plugins + trimming = removed host types and unresolvable plugin dependencies. |
| **`PublishAot`** | **`false`** | **Mandatory.** "No dynamic loading." Delete the `Compact` configuration. |
| `EnableCompressionInSingleFile` | `false` for the default asset; `true` for an optional `-slim` asset | Compression trades launch latency for download size; measure before choosing. |
| `IncludeNativeLibrariesForSelfExtract` | `false` | Keep the llama.cpp `runtimes/` tree loose on disk so LLamaSharp's device probing works and so a user can swap a backend without a rebuild. |
| **`IncludeAllContentForSelfExtract`** | **`false` — remove entirely** | Deprecated .NET Core 3.1 compatibility mode; the source sets it to `true`. |
| `SuppressTrimAnalysisWarnings` | **remove entirely** | The source sets it `true`, which is the opposite of the documented practice. |
| `EnableSingleFileAnalyzer` | `true` | Catches `Assembly.Location`-shaped bugs at build time. Single-file *is* on our critical path. |
| `JsonSerializerIsReflectionEnabledByDefault` | `false` | Forces every persisted type through a `JsonSerializerContext`; failures surface as a clear exception on the dev box, not silently in the field. |
| `InvariantGlobalization` | **`false`** | ⚠️ The source sets `true`. This app renders Unicode token text, heat maps and box-drawing tables, and does locale-sensitive number/date formatting for cost tables. `true` also changes string comparison semantics. **Do not carry it forward.** |
| `SatelliteResourceLanguages` | `en` | Legitimate size saving; keep. |
| `UseSystemResourceKeys` | `false` | The source sets `true`, which replaces framework exception messages with bare resource keys. On a **debugging tool**, readable exception text is the product. |
| `DebugType` | `portable` (+ publish the `.pdb` as a release asset) | The source sets `none`, discarding stack line numbers. A debug shell should produce diagnosable crash reports. |
| `<Content Update="plugins\**"> ExcludeFromSingleFile` | `true` | Plugins outside the bundle; loaded from `AppContext.BaseDirectory`. |
| `ErrorOnDuplicatePublishOutputFiles` | leave `true` and **fix the layout** | Do not paper over `NETSDK1152`; ship one backend family per asset with the `runtimes/` tree preserved. |

**Release assets per version:**

```
chatdbg-<ver>-win-x64.zip          # single-file exe + plugins/ + runtimes/ (CPU backend), signed
chatdbg-<ver>-win-x64-cuda12.zip   # same, CUDA 12 backend
chatdbg-<ver>-win-arm64.zip
chatdbg-<ver>-linux-x64.tar.gz
chatdbg-<ver>-linux-x64-cuda12.tar.gz
chatdbg-<ver>-linux-arm64.tar.gz
SHA256SUMS  +  SHA256SUMS.asc
chatdbg.<ver>.nupkg                # multi-RID dotnet tool, self-contained + `any` fallback
ghcr.io/<org>/chatdbg:<ver>        # secondary; OCI, non-chiseled
```

CI: GitHub Actions matrix over the RIDs. ⚠️ **The existing workflow pins `dotnet-version: '9.0.x'` while the projects target `net10.0`** — fix that first. Add `windows-11-arm`/`ubuntu-24.04-arm` runners for the Arm64 legs, or cross-compile R2R from x64.

#### A minimal-but-useful observability setup that does not phone home

The design principle: **instrument unconditionally, export never — unless asked.**

**Always on (zero cost, zero network):**
- One `ActivitySource` and one `Meter`, both named `Xcaciv.ChatDbg`, passed to `UseOpenTelemetry(sourceName: "Xcaciv.ChatDbg")`. With no listener registered, `ActivitySource.StartActivity` returns `null` and `Histogram.Record` is a no-op — the cost is a null check. **You can ship the instrumentation in every build and it never opens a socket.**
- `ILogger` via `Microsoft.Extensions.Logging`, `[LoggerMessage]`-generated, at `Warning` by default, writing to stderr so it never corrupts a piped stdout.
- `EnableSensitiveData = false`. Prompts and completions never enter telemetry unless the user explicitly turns it on. Do **not** honour `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT` silently — surface it in `/settings` so its state is visible.
- **No OpenTelemetry SDK provider is built at startup.** No exporter, no `OTEL_*` env var is consulted, no endpoint is contacted. This is the load-bearing decision.

**Opt-in, explicit, one flag each:**

| Flag / setting | Effect |
|---|---|
| `--diag-log <path>` | Adds a file logger provider; NDJSON, rotating. Satisfies the existing `/exportlogs` feature with no network. |
| `--trace-console` | Builds a `TracerProvider`/`MeterProvider` with **only** `OpenTelemetry.Exporter.Console`. Everything stays on the terminal. |
| `--otlp-endpoint <url>` | The *only* path that opens a socket, and only to the URL the user typed. Never defaulted, never read from `OTEL_EXPORTER_OTLP_ENDPOINT` implicitly. Aim it at a locally-run collector (see below). |
| `--capture-content` | Sets `EnableSensitiveData = true`. Print a one-line warning to stderr every session it is on. |

**Why not just rely on the OTLP defaults?** Because they are the wrong shape for this promise. `OTEL_EXPORTER_OTLP_ENDPOINT` defaults to `localhost:4317` (gRPC) / `localhost:4318` (HTTP) ([exporter README](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)) — harmless in itself, but it means merely *constructing* an exporter starts trying to connect somewhere. Not constructing one is strictly stronger than configuring one to be quiet. Two related notes: `OtlpExporterOptions` setters take precedence over environment variables; and signal-specific variables (`OTEL_EXPORTER_OTLP_TRACES_*`) are honoured with `UseOtlpExporter` but **not** with `AddOtlpExporter`.

**For the user who does want to look:** document `docker run --rm -it -p 18888:18888 -p 4317:18889 mcr.microsoft.com/dotnet/aspire-dashboard:latest`, then `chatdbg --otlp-endpoint http://localhost:4317`. The Aspire dashboard is a standalone OTLP receiver with a UI on :18888, **in-memory storage only** (everything vanishes on restart), no Aspire dependency, and it accepts telemetry from any language ([aspire.dev/dashboard/standalone](https://aspire.dev/dashboard/standalone/)). It is the shortest path from "I want to see my token spend and latency across three providers" to a working waterfall view, and it never leaves the machine.

**Defence in depth:** also honour **`OTEL_SDK_DISABLED=true`**, which makes all three providers return no-op implementations (evaluated at startup only; later changes have no effect). Implemented in OpenTelemetry .NET via [PR #6568](https://github.com/open-telemetry/opentelemetry-dotnet/issues/4155) — see Unconfirmed for the exact version.

**What to actually watch, once exporting:**

| Signal | Why it matters here |
|---|---|
| `gen_ai.client.token.usage` by `gen_ai.provider.name` | The only honest cross-provider cost comparison. |
| `gen_ai.client.operation.duration` by provider, split on `error.type` | Distinguishes "Bedrock is slow" from "Bedrock is throttling". |
| `gen_ai.client.operation.time_to_first_chunk` | The number a REPL user actually feels. |
| A custom `chatdbg.model.load.duration` histogram | Local GGUF load time dominates cold start; it is not covered by any GenAI convention. |
| A custom `chatdbg.local.tokens_per_second` histogram | The headline number for local inference tuning. |
| `chatdbg.plugin.load.duration` + `chatdbg.plugin.load.failures` | With `Xcaciv.Loader` in the picture, plugin load is a new failure surface that did not exist in the source. |

---

## What this application specifically needs — every recommendation tied to a concrete operation

| The app does this | Therefore |
|---|---|
| **Loads tools via `Xcaciv.Loader` at runtime** (new in the rebuild; the source had zero DI and hand-registered commands) | **AOT is off, trimming is off.** This single fact settles §2 and §3. Plugins live in `plugins/` outside the bundle via `ExcludeFromSingleFile`, resolved from `AppContext.BaseDirectory` (not `Assembly.Location`, which returns `""` under single-file). |
| **Loads `libllama.so` / `llama.dll` and GGUF models from disk** | Native P/Invoke is *not* the AOT blocker — but the `runtimes/` layout is a real one. Keep `IncludeNativeLibrariesForSelfExtract=false`, preserve the `runtimes/<rid>/native/**` tree loose beside the exe, and call `NativeLibraryConfig.Instance.WithSearchDirectory(...)`. Ship one backend family per release asset to dodge `NETSDK1152`. |
| **Renders Unicode token text, heat maps, box-drawing grids and locale-formatted numbers** | `InvariantGlobalization=false`. The source's `true` would break sorting, casing and number formatting in a tool whose entire output is formatted text. |
| **Is a *debugging* tool; users will paste its stack traces into issues** | `DebugType=portable` and `UseSystemResourceKeys=false`. Bare resource keys instead of exception messages is an anti-feature here. |
| **Persists `ChatSettings`, `ChatHistory`, `SystemPrompt` as JSON under the user profile** | `JsonSerializerContext` + `JsonSerializerIsReflectionEnabledByDefault=false`. Not because we trim, but because it makes the persistence contract compile-time-checked and fails loudly when a model changes. |
| **Lets plugins contribute serialisable types** | `JsonSerializerOptions.TypeInfoResolverChain` composition — host context first, plugin contexts appended. |
| **Stores provider secrets** (Windows Credential Manager P/Invoke today) | P/Invoke works in every publish mode considered. But secrets must **never** reach telemetry: `EnableSensitiveData=false`, and scrub `gen_ai.*` message attributes if content capture is ever enabled. |
| **Streams tokens and shows per-token logprobs with top-K alternatives** | `time_to_first_chunk` and `time_per_output_chunk` are the metrics that matter, and both are emitted free by `UseOpenTelemetry()`. Also: streaming Azure OpenAI returns usage **only** with `include_usage`; without it, `/cost` reports zero for every streamed turn. |
| **Talks to three backends with different billing models** | Normalise on `UsageDetails`, then compute cost locally from a user-editable, offline price table. Account for `CachedInputTokenCount` and Bedrock's `cacheWriteInputTokens` separately or your numbers are wrong. |
| **Has an `/exportlogs` command and `LLamaSharpLogConfig`** | A rolling NDJSON file `ILoggerProvider`, ~150 dependency-free lines — or Serilog + `Serilog.Sinks.File` if you'd rather buy it. |
| **Is a local developer tool run on a workstation** | No exporter constructed at startup; `--otlp-endpoint` is the only network path; document the standalone Aspire dashboard for the user who wants a UI. |
| **Ships one binary for Windows and Linux** | Single-file self-contained per RID; sign the Windows assets via `PrepareForBundle` → `sign` CLI → sign the bundle; publish SHA-256 sums for Linux. |
| **Would benefit from `dnx chatdbg` for one-shot use** | Multi-RID `dotnet tool` package, self-contained flavour + `any` fallback. Not the trimmed or AOT flavour. |

---

## Risks, sharp edges and what you give up

1. **`Xcaciv.Loader` / `Xcaciv.Command` / `Xcaciv.Cupcake` are not on nuget.org.** `packageid:Xcaciv.Loader` returns `totalHits: 0`; a broad `q=Xcaciv` search returns only `XCBatch.*`. Everything in §2 about *why* AOT fails is derived from the .NET platform's documented behaviour plus the stated role of these libraries — not from their published docs. **Before committing, confirm the loader's actual mechanism** (custom `AssemblyLoadContext`? `AssemblyDependencyResolver`? `Type.GetType` by name? unloadable contexts?). If it turns out to use a compile-time source-generated registry rather than runtime assembly loading, §2's conclusion changes entirely and AOT is back on the table.
2. **`Azure.AI.OpenAI` stable is 20 months old.** 2.1.0 shipped 2024-12-06; eight prereleases have shipped since, the newest `2.9.0-beta.1` on 2026-03-13. Either pin the stale stable and forgo newer Azure-specific features, or ship a beta dependency in a released product. Neither is comfortable. A third option: consume the vanilla `OpenAI` package via `Microsoft.Extensions.AI.OpenAI` and configure the Azure endpoint yourself — **UNCONFIRMED** whether that covers every Azure-specific request/response shape this app uses.
3. **`NETSDK1152` on multi-backend LLamaSharp publish is unresolved upstream.** [#977](https://github.com/SciSharp/LLamaSharp/issues/977) was closed **not planned**. `ErrorOnDuplicatePublishOutputFiles=false` "results in only one `ggml.dll` and one `llama.dll`, and it's not clear or predictable which one you end up getting" — i.e. it silently ships a CPU-feature variant that may `SIGILL` on an older CPU. This needs a deliberate, tested layout, not a suppression flag.
4. **Native-library extraction on Linux under `systemd`.** If you ever do turn on `IncludeNativeLibrariesForSelfExtract`, `$HOME` is often undefined under `systemd` and extraction fails; you must set `DOTNET_BUNDLE_EXTRACT_BASE_DIR` (docs suggest `%h/.net` in the unit file). And the extraction directory "shouldn't be writable by users or services with different privileges" — do not use `/tmp` or `/var/tmp`.
5. **GenAI semantic conventions are Development, in a repo that just moved, with a rename history.** `gen_ai.system` → `gen_ai.provider.name` and `prompt_tokens`/`completion_tokens` → `input_tokens`/`output_tokens` already happened; more will. `Microsoft.Extensions.AI` claims v1.41 conformance and says so with an explicit experimental caveat. **Do not build user-visible features that assume attribute names are stable**, and do not treat a dashboard built on them as a supported contract.
6. **`Microsoft.Extensions.AI` doesn't emit the spec's ActivityEvents.** `gen_ai.client.inference.operation.details` is missing ([agent-framework#3637](https://github.com/microsoft/agent-framework/issues/3637)). If a backend needs them, that's your code.
7. **The `sign` CLI has no stable release, ever.** `--prerelease` is mandatory, it is Windows-x64-only, and the previously documented `Azure.CodeSigning` package was removed from NuGet. Signing infrastructure sitting on a perpetual beta is a supply-chain risk worth naming in the release runbook.
8. **The local backend has no `gen_ai.provider.name` enum value.** You will emit a non-standard value (`llama.cpp`) and any conformant backend will treat it as unknown. Acceptable, but document it.
9. **What you give up by not trimming and not AOT-ing.** Concretely, against the benchmark numbers: ~80 ms of startup, ~23 MB of working set, and a binary roughly 3–5× larger than an AOT build. For an interactive shell that spends its first seconds mapping a multi-gigabyte GGUF into memory, this is the correct trade — but it *is* a trade, and it is irreversible for as long as plugins exist.
10. **Every publish knob the source project set, it set for AOT.** `SuppressTrimAnalysisWarnings`, `InvariantGlobalization`, `UseSystemResourceKeys`, `DebugType=none`, `IncludeAllContentForSelfExtract`, `TrimMode=full` — six settings that made sense only in service of a `Compact` configuration that the release pipeline never actually built. Porting the csproj wholesale would carry all six into a build where none of them is justified.
11. **`Terminal.Gui` v1 → v2 is a breaking migration.** The source pins 1.19.0; current is 2.4.17 with an instance-based model and `IRunnable`. Not a packaging problem, but it lands in the same rebuild and will consume schedule.
12. **`Spectre.Console` and `Terminal.Gui` are both pre-1.0 / recently-major.** Spectre is at 0.57.2 with alpha builds a day old at time of writing. Pin exact versions; do not float.

---

## Unconfirmed — everything I could not verify on the live web, and where I looked

1. **The exact OpenTelemetry .NET version that shipped `OTEL_SDK_DISABLED`.** [Issue #4155](https://github.com/open-telemetry/opentelemetry-dotnet/issues/4155) is **closed** and references PR #6568, and secondary sources describe it as supported across `TracerProvider`/`MeterProvider`/`LoggerProvider` with startup-only evaluation. I could not read the merged PR or the `OpenTelemetry` CHANGELOG entry to pin the version. Verify against `src/OpenTelemetry/CHANGELOG.md` before relying on it as a kill switch.
2. **Whether `LLamaSharp` 0.27.0 is trim- or AOT-annotated.** Its nuget page lists dependencies and TFMs (`net8.0`, `netstandard2.0`) but no `IsAotCompatible` signal, and I could not fetch the current `NativeLibraryConfig` tutorial (`scisharp.github.io/LLamaSharp/latest/...` returns a redirect stub; only 0.12/0.14-era pages are indexed). **Moot for the recommended publish mode**, but unverified.
3. **The precise current API surface of `NativeLibraryConfig` in 0.27.0** — `WithLibrary`, `WithSearchDirectory`, `WithAutoFallback`, `WithCuda` are attested only via 0.12/0.14 docs and search snippets. Confirm against the 0.27.0 assembly before writing the loader glue.
4. **Whether `Xcaciv.Loader` uses `AssemblyLoadContext` at all.** Not on nuget.org (`totalHits: 0`), no GitHub result surfaced. §2's conclusion is conditional on it doing runtime assembly loading. **This is the single most important thing to confirm before acting on this document.**
5. **Whether `Microsoft.Extensions.AI` 10.9.0 is fully AOT/trim-annotated.** Secondary sources list it among AOT-compatible packages; I did not find a first-party statement. Moot here.
6. **`OpenTelemetry` 1.18.0's current AOT/trim posture.** [#3429](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3429) is closed and the OTLP exporter README says nothing about AOT or trimming. Assembly scanning is confirmed unsupported under AOT; the rest is unverified. Moot here.
7. **Concrete startup and size numbers for *this* app in each mode.** The 118/84/37 ms and 4.3 MB/91 MB/13 MB figures are from a minimal ASP.NET Core API on .NET 11 RC2, not from ChatDbg. The source's own `docs/compact-build.md` claims (8–15 MB AOT, <100 ms; 15–25 MB SingleFile, 200–500 ms) are **undated, internally inconsistent** (references `net9.0` paths for a `net10.0` project), and unverified. The only hard datum in the tree is `test-publish/Xcaciv.ChatDbg.Shell` at **15,677,171 bytes**, a stripped linux-x64 ELF. **Measure before you choose compression.**
8. **Whether `Microsoft.Extensions.AI.OpenAI` fully covers Azure OpenAI's Azure-specific surface** as an alternative to the 20-month-stale `Azure.AI.OpenAI` 2.1.0. Not checked.
9. **`ContainerFamily` values and chiseled-image availability for .NET 10/11.** The `sdk-publish` tutorial page doesn't enumerate them; the `publish-configuration` reference page was not fetched. Only relevant if you pursue containers seriously.
10. **`.NET 11` Preview 7's exact preview number/date beyond "docs last updated for Preview 7, 12 Aug 2026"** and whether the Podman multi-arch container work is final. Reported by [what's new in .NET 11](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview); .NET 11 is not recommended here regardless.
11. **Whether the GenAI semconv repo has cut a release *after* the 17 July 2026 snapshot** that promotes anything to Stable. My stability claim is dated to that snapshot ([john-hodge.com](https://john-hodge.com/blog/opentelemetry-genai-semantic-conventions/)) corroborated by the live [gen-ai-spans.md](https://github.com/open-telemetry/semantic-conventions-genai/blob/main/docs/gen-ai/gen-ai-spans.md) and [gen-ai-metrics.md](https://github.com/open-telemetry/semantic-conventions-genai/blob/main/docs/gen-ai/gen-ai-metrics.md), which still show Development throughout. Re-check at implementation time.
12. **Serilog 4.4.0's official AOT/trim statement.** Its nuget description mentions neither; a secondary source claims "full support in Serilog 4.x for ahead-of-time compiled and size-trimmed .NET 10 apps." Unverified against Serilog's own release notes. Moot here.
13. **GitHub Actions Arm64 runner availability/pricing for public repos** (`windows-11-arm`, `ubuntu-24.04-arm`). Not checked; it affects whether Arm64 legs cross-compile or build natively.
