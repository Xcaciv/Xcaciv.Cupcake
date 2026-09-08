# Plugin/tool hosting, dynamic loading, and the Model Context Protocol

*Research date: 2026-08-28. Every version number and date below was checked against the live nuget.org v3 API, the GitHub REST API, or Microsoft Learn on that date. Where I could not verify something, it is listed under **Unconfirmed** rather than guessed.*

---

## Bottom line — the recommendation in three sentences

Build the tool surface on **`Microsoft.Extensions.AI` 10.9.0** as the single internal tool abstraction (`AIFunction`), expose ChatDbg's own shell verbs to the model as `AIFunction`s wrapped in `ApprovalRequiredAIFunction`, and use the **official `ModelContextProtocol` C# SDK 2.2.0 (GA, Apache-2.0/MIT, maintained with Microsoft)** as the *only* mechanism by which third-party, user-supplied tools enter the process — out-of-process over stdio, never as a loaded assembly.

Keep **Xcaciv.Command + Xcaciv.Loader** for first-party, in-repo commands that you compile and ship yourself (they give you the parser, pipeline, help generation and `AssemblyLoadContext` plumbing you already know), but treat `AssemblyLoadContext` as a *versioning and packaging* mechanism, not a security boundary — there is no CAS in modern .NET and Microsoft explicitly tells you to use OS boundaries instead.

The expensive consequence you must accept up front: **a plugin-loading ChatDbg cannot be a Native-AOT binary.** Ship `PublishSingleFile` + `SelfContained` + `PublishReadyToRun` instead, or ship two SKUs (an AOT "core" with no plugin loading and a full JIT build with it).

---

## Landscape — the real options

### Tool/plugin hosting

| Option | Current version | Status | Last published | One-line verdict |
|---|---|---|---|---|
| **`System.Runtime.Loader.AssemblyLoadContext`** (in-box) | in-box, .NET 10 | GA, in the shared framework | ships with runtime | The substrate. Free, supported, and the only in-process option — but *not* a security boundary. |
| **`ModelContextProtocol`** | **2.2.0** | **GA** | **2026-08-13** | Official C# MCP SDK, "maintained in collaboration with Microsoft"; implements MCP spec revision **2026-07-28**. This is where third-party tools belong. |
| **`ModelContextProtocol.Core`** | 2.2.0 | GA | 2026-08-13 | Client + low-level server only. On `net10.0` it pulls just `Microsoft.Extensions.AI.Abstractions` and `Microsoft.Extensions.Logging.Abstractions` — 2 dependencies. Use this for ChatDbg's *client* role. |
| **`ModelContextProtocol.AspNetCore`** | 2.2.0 | GA | 2026-08-13 | HTTP/SSE server hosting. ChatDbg does not need it. |
| **`ModelContextProtocol.Extensions.Tasks`** | 2.2.0 | GA package | 2026-08-13 | Long-running tools with polling/durable handles. Redesigned in v2; **the v1.3/v1.4 experimental Tasks extension is incompatible.** |
| **`ModelContextProtocol.Extensions.Apps`** | 2.2.0 | GA package, **feature described as experimental** by the .NET Blog | 2026-08-13 | Server-delivered UI. Irrelevant to a terminal app; skip. |
| **`Microsoft.Extensions.AI`** | **10.9.0** | GA | 2026-08-11 | `AIFunction`, `AIFunctionFactory`, `FunctionInvokingChatClient`, `ApprovalRequiredAIFunction`. The tool-calling spine. |
| **`Microsoft.Extensions.AI.Abstractions`** | 10.9.0 | GA | 2026-08-11 | Where `AIFunction` / `ApprovalRequiredAIFunction` actually live (`Microsoft.Extensions.AI.Abstractions.dll`). |
| **`Xcaciv.Command`** (+ `.Core`, `.Interface`, `.FileLoader`, `.DependencyInjection`, `.Extensions.Commandline`) | **3.3.0 / 3.3.1** per README + GitHub releases | Actively developed; **not on nuget.org** | repo pushed 2026-07-26; release tag `v3.3.1` 2026-02-12 | Good command parser/pipeline. Distribution is the problem — see risks. AGPL-3.0. |
| **`Xcaciv.Loader`** | **2.1.2** | Actively developed; **not on nuget.org** | repo pushed 2026-07-26; only release tag `v2.1.2` 2025-12-29 | Thin, opinionated `AssemblyLoadContext` wrapper with path allowlists, SHA-256 integrity store, and preflight metadata scanning. AGPL-3.0. |
| **`Xcaciv.Cupcake`** | no releases; `Directory.Packages.props` pins `Xcaciv.Command` **2.1.1** | Stale relative to Command 3.3.x | repo pushed 2026-02-24 | The pattern to copy is `Loop.cs`. The NuGet install command is a **stub** — see §5. BSD-3-Clause. |
| **`McMaster.NETCore.Plugins`** | 2.0.0 | GA but **stale** | **2025-01-05** (19 months); repo pushed 2026-03-16, not archived; a `2.0.0-beta.214` was listed 2025-12-28 | The classic third-party `ALC` wrapper (`PluginLoader.CreateFromAssemblyFile`, shared-type unification). Still the best-documented; treat the release gap as a risk. Only relevant if you drop Xcaciv.Loader. |
| **`StreamJsonRpc`** | **2.25.29+RR** | GA, Microsoft-maintained | 2026-06-15 | The Microsoft-blessed answer for cross-process RPC now that Remoting is gone. Second-best to MCP for out-of-process tools; wins when the tool is *yours* and MCP's schema overhead is pure cost. |
| **`Xcaciv.Isolation`** | no releases | Early / abandoned-looking | repo pushed **2025-12-20** | Windows-container isolation without Docker. Windows-only, 8 months idle, no package. Do not build on it. |
| **`System.Runtime.Loader` (NuGet package)** | 4.3.0 | **Abandoned / do not reference** | **2016-11-15** | Legacy .NET Standard shim. `AssemblyLoadContext` has been in-box since .NET Core 2.0. If you see this `PackageReference`, delete it. |

### Package distribution

| Option | Current version | Status | Last published | Verdict |
|---|---|---|---|---|
| **`NuGet.Protocol`** | **7.9.0** | GA, but **"We do not guarantee API stability"** (Microsoft Learn) | 2026-08-11 | Works, and is what Cupcake uses. On `net8.0` it has exactly one direct dependency (`NuGet.Packaging 7.9.0`), which then pulls the rest of the client SDK. |
| **`NuGet.Packaging`** | 7.9.0 | GA, same caveat | 2026-08-11 | `PackageArchiveReader`, `PackageExtractor.ExtractPackageAsync`, `PackageSignatureVerifier`, `ClientPolicyContext`. This is where signature verification lives. |
| `dotnet nuget install/verify` (shell out to the SDK) | ships with SDK | GA | — | The honest alternative: don't embed the client SDK, shell out. Loses in a self-contained binary where no SDK is present. |

---

## Analysis

### 1. `AssemblyLoadContext` fundamentals — the substrate under Xcaciv.Loader

#### What an ALC actually is

An `AssemblyLoadContext` is "a unique scope for `Assembly` instances and `Type` definitions" — a dictionary mapping `AssemblyName.Name` → `Assembly`. Critically, per Microsoft Learn ([Understanding AssemblyLoadContext](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext), doc updated 2026-03-30):

> There's no binary isolation between these dependencies. They're only isolated by not finding each other by name.

That sentence is the whole security story, and it is why §2 says what it says.

**Versioning rule.** A single ALC instance can hold exactly one `Assembly` per simple name. Resolution against an ALC that already has that name loaded succeeds *only if the loaded version is equal to or higher than the requested version.* This is why "plugin needs `System.Text.Json` 12, host has 10" fails, and why "plugin needs 9, host has 10" silently succeeds with the host's copy.

**The shared-interface-assembly rule.** Two types with the same fully-qualified name in two different ALCs are *different types*. The exception message is famously useless:

> `Object of type 'IsolatedType' cannot be converted to type 'IsolatedType'.`

The fix Microsoft documents is: define the contract in a shared assembly and make sure it resolves to **one** `Assembly` instance, which in practice means the Default ALC. Concretely for this app: **`Xcaciv.Command.Interface.dll` (containing `ICommandDelegate`, `IIoContext`, `IEnvironmentContext`, `IResult<T>`, the attribute types, and the whole `Parameters` namespace) must be loaded exactly once, in `AssemblyLoadContext.Default`, and every plugin must reference it without copying it into the plugin directory** (`<Private>false</Private>` / `ExcludeAssets="runtime"`). Ship the contract assembly next to the host, not next to the plugin.

Xcaciv.Loader gets this right *by construction*, though not by an explicit decision. Its `SetLoadContext` does:

```csharp
loadContext = new AssemblyLoadContext(fullName, isCollectible);
this.loadContext.Resolving += LoadContext_Resolving;
```

It subscribes to the **`Resolving` event** rather than overriding the `Load` virtual. That matters: `Resolving` fires only *after* the default probing path has failed. So `Xcaciv.Command.Interface` is found by the Default ALC first, gets shared automatically, and the type-identity problem never appears. The cost is the mirror image — **a plugin can never win a version fight with the host.** If the host has `System.Text.Json` 10.0.0 loaded and the plugin needs 12.0.0, the default probe fails, `Resolving` fires, `AssemblyDependencyResolver` finds the plugin's copy, and you now have *two* `JsonElement` types in the process. That is the classic diamond failure and it will surface as exactly that unhelpful cast message.

**`AssemblyDependencyResolver`.** It reads the plugin's own `*.deps.json` plus the files next to the plugin's main assembly to map an `AssemblyName` to an absolute path. Xcaciv.Loader's usage has a small inefficiency worth noting for a rebuild:

```csharp
private Assembly? LoadContext_Resolving(AssemblyLoadContext context, AssemblyName name)
{
    var filePath = Path.GetDirectoryName(this.FilePath) ?? String.Empty;
    var resolvedPath = (new AssemblyDependencyResolver(filePath)).ResolveAssemblyToPath(name);
    ...
}
```

A fresh `AssemblyDependencyResolver` is constructed **on every resolve event** — it re-parses `deps.json` each time. Cache it in a field. (It also passes a *directory* where the documented sample passes the *main assembly path*; both work, but the sample form is the documented one.)

**Collectibility and unloading.** Xcaciv.Loader defaults `isCollectible: true` and `Dispose()` calls `Unload()`. Per [How to use and debug assembly unloadability](https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability) (doc updated 2026-03-30), unload is **cooperative, not forced** — unlike AppDomains, nothing is aborted. Unloading completes only when:

- no thread has a frame from the ALC's assemblies on its stack, **and**
- nothing outside holds a strong reference (stack slot, JIT-introduced local, static field, or `GCHandleType.Normal`/`Pinned`) to the assembly, a type from it, or an instance of such a type.

The documented list of non-obvious blockers is long: JIT-held registers, `RegisteredWaitHandle` callbacks, non-collectible ALCs created *inside* the collectible one, and — added recently — **fields on your own `AssemblyLoadContext` subclass**, because the runtime holds a strong GC handle to the ALC while unloading is in progress.

Additional documented limits on collectible ALCs: **C++/CLI assemblies are unsupported, and ReadyToRun code is ignored** (so plugins lose R2R startup benefits).

#### The classic failure modes, and where they bite here

| Failure mode | Symptom | Cause | Mitigation for the rebuild |
|---|---|---|---|
| Type identity split | `Object of type 'ICommandDelegate' cannot be converted to type 'ICommandDelegate'` | Contract assembly copied into the plugin folder and loaded into the plugin ALC | Contract in Default ALC only; plugin csproj uses `ExcludeAssets="runtime"` on the contract `PackageReference`; assert `AssemblyLoadContext.GetLoadContext(typeof(ICommandDelegate).Assembly) == Default` at startup |
| Diamond version conflict | Same, but on `System.Text.Json` / `Microsoft.Extensions.*` | Plugin needs a *higher* version than the host loaded | Publish an explicit "host-provided assemblies" allowlist; refuse to load a plugin whose `deps.json` demands a higher version of anything on it, with a readable error |
| Unload never completes | Memory grows across a long REPL session; plugin `.dll` cannot be deleted or upgraded | Any strong reference survives | Never hold the plugin instance past the command invocation; verify with `WeakReference(alc, trackResurrection: true)` + `GC.Collect(); GC.WaitForPendingFinalizers()` in a loop; instrument with the SOS `!dumpheap -type LoaderAllocator` / `!gcroot` workflow the docs describe |
| File lock on Windows | Cannot upgrade a package while the shell is running | `LoadFromAssemblyPath` memory-maps the file; Xcaciv.Loader does not shadow-copy | Copy the package to a per-session temp directory and load from there, or use `LoadFromStream(File.ReadAllBytes(...))` (loses `AssemblyDependencyResolver`'s deps.json path — you must resolve manually) |
| **Per-invocation ALC leak** | ALC/`LoaderAllocator` count grows monotonically | See below | Cache one ALC per package for the process lifetime |

That last one is a real, present bug in the Xcaciv.Command code the rebuild would inherit. `CommandFactory.CreateCommand` does:

```csharp
using var context = new AssemblyContext(
    packagePath,
    basePathRestriction: basePathRestriction,
    securityPolicy: effectivePolicy);

return context.CreateInstance<ICommandDelegate>(fullTypeName);
```

The `using` disposes the `AssemblyContext` — which calls `Unload()` — at the moment the method returns, while the freshly created `ICommandDelegate` (whose type lives in that ALC) is being handed to the caller. Because unload is cooperative, nothing crashes; the ALC simply never finishes unloading, and a **new ALC is created for every single command invocation**. In a batch tool nobody notices. In an interactive chat/debug REPL that a user leaves open for hours and that runs a token-inspection command in a loop, you accumulate one `LoaderAllocator` per invocation. **Fix this in the rebuild: one long-lived `AssemblyContext` per installed package, disposed only on `unload`/exit.**

---

### 2. Current best practice for loading third-party assemblies at runtime — and the security truth

#### There is no CAS. Say what that means, precisely.

From [.NET Framework technologies unavailable on .NET 6+](https://learn.microsoft.com/en-us/dotnet/core/porting/net-framework-tech-unavailable):

> Sandboxing, which relies on the runtime or the framework to constrain which resources a managed application or library uses or runs, isn't supported on .NET Framework and therefore is also not supported on .NET 6+. CAS is no longer treated as a security boundary, because there are too many cases in .NET Framework and the runtime where an elevation of privileges occurs.
>
> Use security boundaries provided by the operating system, such as virtualization, containers, or user accounts, for running processes with the minimum set of privileges.

And on AppDomains:

> Creating more app domains isn't supported, and there are no plans to add this capability in the future. **For code isolation, use separate processes or containers as an alternative. To dynamically load assemblies, use the `AssemblyLoadContext` class.**

Note the deliberate split in that sentence. Isolation → processes. Dynamic loading → ALC. They are not the same problem and .NET only solves the second one.

**What this means concretely for a loaded plugin in ChatDbg's process:** a plugin assembly can call `File.Delete`, open sockets, `P/Invoke` into `libc`, read the process's own memory (including the Azure OpenAI key you just decrypted out of DPAPI/libsecret), spawn processes, and register an `AppDomain.CurrentDomain.AssemblyResolve` handler that hijacks *your* loads. Nothing in `AssemblyLoadContext` stops any of that. There is no `PermissionSet`, no `SecurityTransparent`, no partial trust.

#### Where Xcaciv.Loader's "security" actually sits

Xcaciv.Loader's controls are real and worth having, but it is important to name what they are: **load-time admission control**, not runtime confinement. Reading `AssemblySecurityPolicy.cs` and `AssemblyContext.cs`:

| Control | What it does | What it does *not* do |
|---|---|---|
| `basePathRestriction` | Refuses to load a DLL whose resolved path is outside a named directory. Default is the current directory; the README is emphatic that `"*"` "is equivalent to no security". | Stop a loaded plugin from reading/writing any path at runtime |
| `AssemblySecurityPolicy.Default` | Blocks paths containing `grouppolicy`, `systemprofile` | Anything else |
| `AssemblySecurityPolicy.Strict` | Adds `windows`, `system32`, `programfiles`, `programfiles(x86)`, `programdata`, `winevt\logs`, `credentials`, `windows defender`, `appdata\local\microsoft\credentials`; also sets `DisallowDynamicAssemblies = true` | These are **case-insensitive substring matches on Windows-shaped paths**. On Linux they match essentially nothing. A plugin at `/opt/tools/plugin.dll` passes Strict trivially. |
| `AssemblyPreflightAnalyzer` | Under Strict, scans metadata for `Reflection.Emit` / `Expressions.Compile` indicators and throws `SecurityException` | It is a metadata heuristic. `Type.GetType("System.Reflection.Emit.AssemblyBuilder")` + late binding defeats it. Treat as a speed bump and an audit signal, not a control. |
| `AssemblyIntegrityVerifier` + `AssemblyHashStore` | SHA-256 allowlist of exact file hashes, with a "learning mode" that trusts on first load and a strict mode that throws on mismatch | Learning mode trusts whatever it first sees. This is TOFU, and is only meaningful if the learn step happens somewhere you trust. |
| `EnableGlobalDynamicAssemblyMonitoring` | Audit-only event when a dynamic assembly appears anywhere in the process | Explicitly documented as unable to prevent emit |

That is a defensible **integrity** story (this is the bits I approved) and a weak **confinement** story (this code can only do X). Be honest about which one you are buying.

#### The process-isolation alternative

For anything a *user* names at a prompt, the boundary has to be the OS. The realistic ladder, cheapest to strongest:

1. **Separate process, same user, JSON-RPC over stdio.** Cheap, cross-platform, no privileges gained by the tool that the shell doesn't already have — but it *does* bound blast radius on crash, hang, memory, and native-code faults, and it lets you kill a runaway tool with `Process.Kill`. Two mature transports: **MCP** (schema, discovery, and an ecosystem come free) or **`StreamJsonRpc` 2.25.29** (Microsoft's documented Remoting replacement; leaner if the tool is yours).
2. **Separate process, reduced privileges.** Windows: a restricted token / Job Object with `JOB_OBJECT_LIMIT_ACTIVE_PROCESS` + memory caps, or an AppContainer. Linux: a `seccomp` filter, a user namespace, or `bwrap`. .NET gives you none of this in-box — you are calling OS APIs via P/Invoke or launching a helper. This is real work.
3. **Container / VM.** Strongest, heaviest, and wrong for a self-contained terminal binary that has to run on a developer laptop with no Docker.

For ChatDbg, **rung 1 with MCP is the right rung**, and it happens to be exactly what the MCP SDK gives you for free with `StdioClientTransport`.

---

### 3. The Model Context Protocol and the official C# SDK

#### Current state (verified 2026-08-28)

- Repo: [`modelcontextprotocol/csharp-sdk`](https://github.com/modelcontextprotocol/csharp-sdk) — 4,500 stars, last push **2026-08-27**, not archived, described as *"The official C# SDK for Model Context Protocol servers and clients. Maintained in collaboration with Microsoft."*
- Licensing: the repo's `LICENSE` states the MCP project is **transitioning from MIT to Apache-2.0**; new contributions are Apache-2.0, un-relicensed MIT contributions remain MIT. NuGet reports `Apache-2.0`.
- Release train: `1.0.0` (2026-02-25) → `1.3.0` (2026-05-08) → `1.4.1` (2026-07-09) → **`2.0.0` GA 2026-07-28** → `2.1.0` (2026-08-05) → **`2.2.0` (2026-08-13)**. Nine releases in six months; this is a fast-moving GA library, not a settled one.
- v2.0 implements MCP spec revision **2026-07-28** — per the [.NET Blog announcement](https://devblogs.microsoft.com/dotnet/announcing-v20-of-the-official-mcp-csharp-sdk/), "the largest revision of the protocol since it launched."
- TFMs: `net8.0`, `net9.0`, `net10.0`, `netstandard2.0`.

#### What v2 changed that matters here

- **Stateless by default.** No `initialize` handshake requirement, no `Mcp-Session-Id` pinning. Mostly an HTTP-scaling story; for stdio it means less ceremony.
- **Multi Round-Trip Requests (MRTR).** A tool can throw `InputRequiredException` carrying `InputRequest.ForElicitation(...)` to ask the user something mid-call, and the client resolves the loop when handlers are registered. **For an interactive terminal shell this is genuinely useful** — a tool can ask "which of these 3 files did you mean?" without you inventing a side channel.
- **`[McpHeader]`** promotes parameters into HTTP headers for proxy routing. Irrelevant to stdio.
- **Backward compatible.** v2 clients talk to v1 servers via legacy handshakes; deprecations surface as analyzer warnings `MCP9004`/`MCP9005`/`MCP9006`. **The one hard break: the experimental Tasks extension from 1.3/1.4 is incompatible with v2's redesigned Tasks.**
- **Not yet done: end-to-end auth/authz.** The blog names it as "the next focus." If you ever move past stdio to a remote MCP server that needs OAuth, you are on the leading edge.

#### Client role (what ChatDbg needs)

```csharp
var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "chatdbg-fs",
    Command = "npx",
    Arguments = ["-y", "@modelcontextprotocol/server-everything"],
});

McpClient mcpClient = await McpClient.CreateAsync(transport);
IList<McpClientTool> tools = await mcpClient.ListToolsAsync();

await foreach (var update in chatClient.GetStreamingResponseAsync(
                   messages, new ChatOptions { Tools = [.. tools] }))
{ ... }
```

The load-bearing fact: **`McpClientTool` derives from `Microsoft.Extensions.AI.AIFunction`.** So an MCP tool drops straight into `ChatOptions.Tools` alongside your own commands with zero adapter code, and `FunctionInvokingChatClient` invokes both through the same path. `WithName(string)` / `WithDescription(string)` let you re-label a server's tool for the model without touching the server. (Sources: [MCP client quickstart](https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-mcp-client), [`McpClientTool` API](https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.Client.McpClientTool.html).)

#### Server role (ChatDbg as an MCP server)

Worth building, and cheap:

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
await builder.Build().RunAsync();

[McpServerToolType]
public static class TokenTools
{
    [McpServerTool, Description("Return per-token log probabilities with top-K alternatives for a prompt.")]
    public static Task<LogProbResult> LogProbs(string prompt, int topK = 5) => ...;
}
```

Note the console-logging line: **on stdio, stdout is the protocol channel.** Any stray `Console.WriteLine` corrupts the stream. That is a real integration hazard for an app whose entire personality is rich terminal output via Spectre.Console.

There is also `McpServerTool.Create(AIFunction, McpServerToolCreateOptions?)`, which wraps an existing `AIFunction` as an MCP tool — so **one registration of a ChatDbg command can serve both the in-process model and an external MCP client.** Caveat from the SDK docs: unlike the other `Create` overloads, the `AIFunction` overload "does not provide all of the special parameter handling for MCP-specific concepts, like `McpServer`."

#### When is a tool an in-process assembly, and when an MCP server?

| Decide by | In-process (`Xcaciv.Command` plugin, `AssemblyLoadContext`) | Out-of-process (MCP server over stdio) |
|---|---|---|
| **Who wrote it** | You. In your repo, in your CI, in your signed release. | Anyone else. Especially anything the user names at a prompt. |
| **Latency budget** | Sub-millisecond. Token-map rendering over a 128k vocabulary, per-token attribution over a long transcript. | Tens of ms per call is fine. |
| **Data shape** | Rich CLR objects — `TokenLogProbabilities[]`, spans into the prompt, a `float[]` probability vector. Serializing these to JSON per call is absurd. | JSON-serializable. Strings, numbers, small records. |
| **Needs shell internals** | Needs `IIoContext`, `IEnvironmentContext`, live `ChatHistory`, the decrypted provider credential, the loaded GGUF handle. | Needs none of it, or gets a narrow slice passed as arguments. |
| **Blast radius if hostile** | Total. Your keys, your files, your process. | Bounded by the child process's own privileges; killable; auditable at the transport. |
| **Ecosystem** | Yours alone. | Every MCP server anyone has written. |
| **Lifecycle** | Loaded once, lives for the session. | Spawned per-server; can be restarted, can crash without taking the shell down. |

**Rule of thumb for the rebuild:** if the tool needs a pointer into ChatDbg's own state, it is an in-process command and you compiled it. If it needs the outside world, it is an MCP server and it runs in its own process. There is no third category, and "trusted third-party plugin" is not one.

#### The `.mcp.json` in this repo

`/mnt/g/3RD-Party/reversing/subject/chatdbg/.mcp.json` is:

```json
{ "inputs": [],
  "servers": { "context7": { "type": "stdio", "command": "npx",
                             "args": ["-y", "@upstash/context7-mcp@latest"], "env": {} } } }
```

This is a **development-time** MCP configuration — it wires an MCP server into the coding agents working on ChatDbg, not into ChatDbg itself. It is worth reading as an existence proof rather than a requirement: the team already accepts the `stdio` + `command`/`args` server-config shape, and the rebuild should adopt the same schema for the shell's *runtime* MCP servers so the two are visually identical. Note the sharp edge it demonstrates by example: `@latest` on an `npx` command means the tool you execute can change under you between runs, which is precisely the rug-pull risk described in §5.

---

### 4. Function/tool calling via `Microsoft.Extensions.AI`

#### The primitives (all GA in 10.9.0)

| Type | Assembly | Role |
|---|---|---|
| `AITool` → `AIFunctionDeclaration` → `AIFunction` → `DelegatingAIFunction` | `Microsoft.Extensions.AI.Abstractions.dll` | The tool hierarchy. `AIFunction` carries `Name`, `Description`, `JsonSchema`, `ReturnJsonSchema`, `JsonSerializerOptions`, `UnderlyingMethod`, and `InvokeAsync(AIFunctionArguments, CancellationToken)`. |
| `AIFunctionFactory.Create` | `Microsoft.Extensions.AI.dll` | Five overloads: `(Delegate, AIFunctionFactoryOptions)`, `(Delegate, string? name, string? description, JsonSerializerOptions?)`, `(MethodInfo, object? target, ...)`, `(MethodInfo, Func<AIFunctionArguments,object> createInstanceFunc, AIFunctionFactoryOptions?)`, `(MethodInfo, object?, AIFunctionFactoryOptions)`. The `createInstanceFunc` overload is the DI hook — a fresh receiver per invocation. |
| `FunctionInvokingChatClient` / `.UseFunctionInvocation()` | `Microsoft.Extensions.AI.dll` | Wraps an `IChatClient`; on receiving `FunctionCallContent`, invokes the matching `AIFunction` from `Tools`/`AdditionalTools`, emits `FunctionResultContent`, and loops until no calls remain or `MaximumIterationsPerRequest` is hit. |
| `ApprovalRequiredAIFunction(AIFunction)` | `Microsoft.Extensions.AI.Abstractions.dll` v10.9.0 | A `DelegatingAIFunction` that marks a function as needing user approval. **It does not enforce anything** — the docs are explicit: "it is the responsibility of the invoker to obtain that approval before invoking the function." The pipeline surfaces `FunctionApprovalRequestContent` and consumes `FunctionApprovalResponseContent`. |

#### Mapping `Xcaciv.Command` commands onto `AIFunction`

The two models are close but not identical, and the mismatches are where the work is:

| `Xcaciv.Command` | `AIFunction` | Bridging work |
|---|---|---|
| `[CommandRegister("Name", "desc")]`, `[CommandRoot]` | `AIFunction.Name`, `.Description` | Direct. Flatten `Root Sub` to `root_sub` — most providers require `^[a-zA-Z0-9_-]{1,64}$` for tool names, and a space will be rejected. |
| `[CommandParameterOrdered]`, `[CommandParameterNamed]`, `[CommandFlag]`, `IParameterValue<T>` (`ParameterString`, `ParameterLong`, `ParameterBool`, `ParameterGuid`, `ParameterDateTime`, `ParameterJson`, …) | `AIFunction.JsonSchema` | **This is the real adapter.** Write a `CommandDescriptionToJsonSchema` mapper: ordered params → `required` array in order; named → optional properties; flags → `"type":"boolean"`; each `ParameterX` type → its JSON Schema type. The strong typing that Xcaciv.Command added in 3.1.0 is exactly what makes this mechanical. |
| `IAsyncEnumerable<IResult<string>> Main(IIoContext, IEnvironmentContext)` | `Task<object?> InvokeAsync(...)` | Structural mismatch. A command streams chunks into a pipeline; a tool returns one value. **Buffer the `IAsyncEnumerable` to a string (or a structured record) for the tool path, and keep the streaming path for the human REPL path.** Cap the buffer — an unbounded token dump will blow the context window. |
| `IIoContext` (prompting, status messages, formatted output) | nothing | A tool invoked by the model must not prompt the human directly. Inject a *headless* `IIoContext` that captures output and throws on `PromptForCommand`. If you need mid-call input, that's MCP's `InputRequiredException`/elicitation, not `IIoContext`. |
| `GetDefaultEnvironment()` → `FETCH_TIMEOUT` style globals | — | Resolve from the environment at bind time; do not expose environment variables as tool parameters (they become prompt-injection targets). |
| `ModifiesEnvironment` flag on the description | `ApprovalRequiredAIFunction` | A natural, already-present risk signal: **any command with `ModifiesEnvironment == true` gets wrapped in `ApprovalRequiredAIFunction`.** So does anything that writes files or spends money. |

#### Should the shell's commands be exposed to the model as callable tools?

**Yes, but on a curated allowlist and with approval gating — not "everything the parser knows."** Looking at what ChatDbg's commands actually are (`src/Xcaciv.ChatDbg.Core/Commands/`):

| Command | Expose to model? | Why |
|---|---|---|
| `TokenizeCommand`, `LogProbsCommand`, `ShowTokenAnalysisCommand`, `InspectCommand`, `DemoLogProbsCommand` | **Yes, read-only** | These are the app's reason to exist. "Why did you pick that token?" → model calls `logprobs` on its own last turn and explains itself. This is the single most compelling feature the rebuild can add, and it is only possible because the tool and the model are in the same session. |
| `HelpCommand` | **Yes** | Lets the model answer "how do I…" without you maintaining a duplicate prompt. |
| `ExportCommand`, `ExportLogsCommand`, `ExportTokenAnalysisCommand` | **Approval-gated** | Writes files at a model-chosen path. Classic confused-deputy target. |
| `SetCommand`, `ModelCommand`, `PromptCommand`, `InjectCommand` | **Approval-gated or not at all** | These change settings, switch backends (i.e. spend money elsewhere), swap the system prompt, and inject messages. A model that can rewrite its own system prompt is not a feature. `InjectCommand` in particular should never be model-callable. |
| `ImportCommand` | **No** | Reads arbitrary files into history at a model-chosen path — a direct exfiltration primitive when combined with any outbound tool. |
| `ClearCommand`, `PopCommand` | **No** | Destroys the user's history on the model's initiative. No upside. |
| `ExitCommand`, `QuitCommand` | **Never** | Obviously. |

The deciding heuristic is the "lethal trifecta": a session that simultaneously has (a) access to private data, (b) exposure to untrusted content, and (c) an outbound channel is exploitable. ChatDbg has (a) — credentials, chat history, local files — the moment any MCP server returns attacker-controlled text you have (b), and `Export*`/any network tool is (c). Keeping (c) behind `ApprovalRequiredAIFunction` is the cheapest break in the chain.

---

### 5. NuGet as a plugin distribution channel

#### What Cupcake actually does today (read the code before copying the pattern)

The brief says Xcaciv.Cupcake "uses NuGet.Protocol to install command packages at runtime." **The scaffolding exists; the install does not.** From `src/Xcaciv.Command.Packages/NugetWrapper.cs`:

```csharp
public static void InstallPackage(PackageIdentity identity, SourceRepository repository, string targetDirectory)
{
    ...
    DownloadPackageAsync(identity, repository, targetFilePath).GetAwaiter().GetResult();
    PackageIdentity packageIdentity = GetNuspecData(targetFilePath);
    string packageDirectory = Path.Combine(targetDirectory, packageIdentity.Id, packageIdentity.Version.ToString());
    if (!Directory.Exists(packageDirectory)) Directory.CreateDirectory(packageDirectory);
    // TODO: Extract package to directory
    // TODO: resolve dependencies
    // ZipFile.ExtractToDirectory(targetFilePath, packageDirectory);
}
```

and `InstallCommand.cs`:

```csharp
public override string HandleExecution(string[] parameters, IEnvironmentContext env)
    => "Not installing " + String.Join(',', parameters);
```

What *is* implemented and works: `PackageSearchResource.SearchAsync`, `FindPackageByIdResource.GetAllVersionsAsync`, `DependencyInfoResource.ResolvePackage` (single level, results not walked), `FindPackageByIdResource.CopyNupkgToStreamAsync`, and `PackageArchiveReader.NuspecReader.GetIdentity()`. So: **search, enumerate, download the `.nupkg`, read its identity.** Extraction, transitive dependency resolution, TFM asset selection, and signature verification are all absent.

That is the honest baseline. Treat Cupcake's package layer as a *sketch to finish*, not a component to reuse. Note also that Cupcake pins `Xcaciv.Command` **2.1.1** and `NuGet.Protocol` **7.0.1** (2025-11-24) while Command is at **3.3.x** and NuGet.Protocol at **7.9.0**; and Command shipped breaking changes in 3.0.0 (removed `EnableDefaultCommands()`, `GetHelp()`) and 3.2.3 (`HandlePipedChunk` now takes `IResult<string>` — Cupcake's `InstallCommand` still uses the old `string` signature). **Cupcake as it stands will not compile against current Xcaciv.Command.**

#### What a real implementation needs

Using `NuGet.Protocol` 7.9.0 + `NuGet.Packaging` 7.9.0:

1. `Repository.Factory.GetCoreV3(packageSource)` — with `PackageSource.Credentials` if you want private feeds.
2. `PackageSearchResource.SearchAsync` for discovery.
3. Walk `DependencyInfoResource.ResolvePackage` transitively, or use `NuGet.Resolver` — Cupcake's single-level call is not a resolution.
4. `FindPackageByIdResource.CopyNupkgToStreamAsync` to fetch.
5. **`PackageExtractor.ExtractPackageAsync`** with a `PackageExtractionContext` carrying a `PackageSignatureVerifier` and `ClientPolicyContext` — this is the API that does extraction *and* signature verification together. Rolling your own `ZipFile.ExtractToDirectory` skips verification and re-opens zip-slip.
6. Pick the right `lib/<tfm>` assets with `NuGet.Frameworks` — a package built for `net8.0;net10.0` must not have both folders copied into the plugin directory.

#### Signature verification — and the platform trap

From [NuGet signed-package verification](https://learn.microsoft.com/en-us/dotnet/core/tools/nuget-signed-package-verification):

- **Windows:** always enabled during restore; uses the OS root store.
- **Linux:** unsupported before .NET SDK 6.0.400. **Enabled by default from .NET 8 SDK**; opt out with `DOTNET_NUGET_SIGNATURE_VERIFICATION=false`. Probes `/etc/pki/ca-trust/extracted/pem/objsign-ca-bundle.pem` first, then falls back to the SDK's bundled cert bundle. If the system bundle is present but lacks nuget.org's roots, verification **fails** with `NU3018`/`NU3028`.
- **macOS:** disabled by default, and Microsoft **recommends leaving it disabled** (NuGet/Home#11985, #11986).
- Known issue: verification on Linux is roughly an order of magnitude slower than on Windows ([NuGet/Home#12672](https://github.com/NuGet/Home/issues/12672)).

**The trap for a self-contained ChatDbg:** all of the above describes the *SDK's* restore path, which reads the SDK's certificate bundles. A self-contained single-file binary on a machine with **no .NET SDK installed has no SDK certificate bundle.** On Windows the OS root store carries you; on Linux, if `/etc/pki/ca-trust/extracted/pem/objsign-ca-bundle.pem` is absent (it is not present on Debian/Ubuntu by default) there is nothing to fall back to. **You must ship your own trust anchor.** Two workable answers:

- **Embed the roots.** Ship the code-signing and timestamping roots you accept as an embedded resource and hand them to the verifier explicitly. Now you own a revocation/rotation problem, but it is a *known* one.
- **Don't use author signatures as your trust root at all.** Pin publisher certificate thumbprints or, better, package SHA-512 hashes in a signed manifest that ChatDbg ships and updates — the same shape as Xcaciv.Loader's `AssemblyHashStore`, one layer up. This is what I'd do: it is verifiable offline, it doesn't depend on OS trust stores differing across three platforms, and it composes with the loader's existing integrity check.

Either way: `nuget.org`'s repository signature proves *the package came from nuget.org unmodified*. It proves nothing about the author's intent. The 2026 supply-chain literature on typosquatting and post-publish account compromise applies here in full.

#### The security posture of `install <name-the-user-typed>`

Be blunt in the design doc, because this is the single highest-risk feature in the product:

> `install Foo.Bar` at a ChatDbg prompt means: fetch code chosen by a string the user typed, from a public feed anyone can publish to, and execute it inside the process that currently holds the user's Azure OpenAI key, AWS Bedrock credentials, and complete chat history — with no runtime sandbox, because .NET does not have one.

Mitigations, in the order they buy the most:

1. **Do not `PackageReference`-style install arbitrary nuget.org packages.** Restrict `install` to a **curated feed or an ID prefix allowlist** (`ChatDbg.Tools.*`). Cupcake's `NuGet.config` already demonstrates `packageSourceMapping`, which is exactly the right primitive — extend the idea to runtime.
2. **Two-step confirmation with facts.** Before extracting, show the user: package ID, exact version, publisher, signature status (verified / unsigned / untrusted-root), download count, publish date, and the transitive dependency set. Require an explicit `yes`.
3. **Verify, then pin.** After a successful install, record every extracted assembly's SHA-256 into the Xcaciv.Loader `AssemblyHashStore` and thereafter run the verifier in **strict** (non-learning) mode. This turns a rug-pull into a load-time failure.
4. **Never let the model call `install`.** It must not be an `AIFunction` at any approval level. A model that can install its own tools has no ceiling.
5. **Extract to a per-user, non-world-writable directory** and set `basePathRestriction` to exactly that directory. Not `%TEMP%`, not `/tmp` — the single-file docs give the same warning about extraction directories: "these directories shouldn't be writable by users or services with different privileges. Don't use */tmp* or */var/tmp*."
6. **Prefer MCP servers over NuGet plugins as the extension story.** An MCP server the user configures still runs code they chose, but it runs in its own process and cannot read your key out of memory.

One structural caveat: Microsoft Learn's own [NuGet Client SDK](https://learn.microsoft.com/en-us/nuget/reference/nuget-client-sdk) page says plainly:

> We do not guarantee API stability, as our team's responsibility is tooling, not libraries.

So pin `NuGet.Protocol`/`NuGet.Packaging` to an exact version, wrap them behind one internal interface, and expect to fix things on upgrade.

---

### 6. Recommendation — how discovery, loading, isolation and execution should be structured

#### The four-tier tool model

```
┌─ Tier 0 · Built-in commands ────────────────────────────────────────────┐
│ Compiled into the binary. Xcaciv.Command ICommandDelegate.              │
│ Tokenize, LogProbs, Inspect, Export, Set, Model, Help, …                │
│ Loaded: never — they are just types. Zero ALC involvement.              │
└─────────────────────────────────────────────────────────────────────────┘
┌─ Tier 1 · First-party optional packs ───────────────────────────────────┐
│ Assemblies you built and signed, shipped beside the exe or side-loaded  │
│ from a signed manifest. One long-lived collectible ALC per pack.        │
│ Xcaciv.Loader: strict policy + hash allowlist in non-learning mode.     │
│ Contract (Xcaciv.Command.Interface) resolves from Default ALC only.     │
└─────────────────────────────────────────────────────────────────────────┘
┌─ Tier 2 · MCP servers ──────────────────────────────────────────────────┐
│ Anything third-party. Own process, stdio, killable, restartable.        │
│ ModelContextProtocol(.Core) 2.2.0 → McpClientTool : AIFunction.         │
│ Configured with the same schema shape as the repo's .mcp.json.          │
└─────────────────────────────────────────────────────────────────────────┘
┌─ Tier 3 · ChatDbg-as-MCP-server ────────────────────────────────────────┐
│ `chatdbg mcp` subcommand. AddMcpServer().WithStdioServerTransport().    │
│ Re-exports the Tier-0 read-only introspection tools so Claude Code /    │
│ VS Code / any MCP host can drive ChatDbg's token analysis.              │
└─────────────────────────────────────────────────────────────────────────┘
```

**One registry, one abstraction.** Every tier terminates in an `AIFunction` in a single `ToolRegistry`. Tier 0/1 arrive via `AIFunctionFactory.Create(MethodInfo, createInstanceFunc, options)` driven by the `ICommandDescription` → JSON Schema adapter; Tier 2 arrive as `McpClientTool` (already an `AIFunction`); Tier 3 goes the other way via `McpServerTool.Create(AIFunction, …)`. `ChatOptions.Tools` gets the filtered, approval-wrapped view. There is exactly one place that decides what the model can call.

#### Discovery

- **Tier 0/1:** `Xcaciv.Command`'s existing attribute scan (`[CommandRegister]`, `[CommandRoot]`, parameter attributes) via `CommandLoader`/`Crawler` over a package directory. Do the scan with `MetadataLoadContext` **before** loading for execution — you get the command list for `help` without executing a static constructor from an untrusted assembly.
- **Tier 2:** on startup, read `~/.chatdbg/mcp.json` (same shape as the repo's `.mcp.json`), spawn each configured server lazily on first use, `ListToolsAsync()`, cache. Handle `notifications/tools/list_changed` if you can; otherwise re-list on reconnect.

#### Loading (Tier 1 rules, non-negotiable)

1. **One `AssemblyContext` per package, cached for the session.** Not per invocation. Fix the `using var context = …; return context.CreateInstance<…>()` pattern.
2. **Contract assembly in Default ALC, verified at startup.** Assert `AssemblyLoadContext.GetLoadContext(typeof(ICommandDelegate).Assembly) == AssemblyLoadContext.Default` and fail loudly if not; this one check would prevent the single most common and most confusing plugin bug.
3. **Cache the `AssemblyDependencyResolver`** in the context instead of constructing one per `Resolving` event.
4. **Publish an explicit host-provided assembly list** (`Xcaciv.Command.Interface`, `Microsoft.Extensions.AI.Abstractions`, `System.Text.Json`, `Microsoft.Extensions.Logging.Abstractions`, …) with versions. Refuse a plugin whose `deps.json` demands a higher version of anything on the list, with a message that names both versions.
5. **`basePathRestriction` = the exact package directory. Never `"*"`.** And do not rely on `AssemblySecurityPolicy.Strict`'s forbidden-directory list on Linux — it is a Windows-path substring list.
6. **Integrity verifier in strict, non-learning mode in release builds.** Learning mode is a development convenience; make it require an explicit `--trust-on-first-use` flag.
7. **Shadow-copy before load** so a package can be upgraded without restarting the shell.
8. **Unload only on explicit `unload`/`exit`,** and verify it with `WeakReference(alc, trackResurrection: true)` + a bounded GC loop. If it fails to unload, log it and move on — do not spin.

#### Execution and approval

- One `FunctionInvokingChatClient` in the `ChatClientBuilder` pipeline (`.UseFunctionInvocation()`), with `MaximumIterationsPerRequest` set to something small (5–10) so a tool loop can't burn the user's budget.
- Every non-read-only tool wrapped in `ApprovalRequiredAIFunction`. Remember: **it does not enforce; you do.** Intercept `FunctionApprovalRequestContent`, render a Spectre.Console panel showing tool name, resolved arguments, and origin (`built-in` / `pack:Foo` / `mcp:context7`), and require a keypress. Reply with `FunctionApprovalResponseContent`.
- **Always show the tool's origin.** A tool named `read_file` from an MCP server the user configured last month should not look identical to a built-in.
- Tag every tool invocation into the audit log — `Xcaciv.Command`'s `IAuditLogger`/`StructuredAuditLogger` already exists and already masks values; reuse it rather than inventing a second log.
- Treat every tool *result* as untrusted content. Do not let a tool result be rendered as markup that Spectre.Console will interpret, and do not let it be silently promoted into the system prompt.

#### Deployment

**Accept that plugin loading and Native AOT are mutually exclusive.** The existing `Compact` configuration sets `PublishAot=true`, `PublishTrimmed=true`, `TrimMode=full`, `IlcOptimizationPreference=Size`. That configuration cannot host `AssemblyLoadContext` plugins, cannot use reflection-based `WithToolsFromAssembly()`, and cannot use `AIFunctionFactory.Create` over arbitrary `MethodInfo` without pre-generated `JsonTypeInfo`. Options, in preference order:

1. **Single SKU, `PublishSingleFile` + `SelfContained` + `PublishReadyToRun`, no trimming.** Bigger (~70–90 MB before compression) but everything works. Use `EnableCompressionInSingleFile`. Ship the contract assembly and the plugin directory *outside* the bundle using the documented `ExcludeFromSingleFile` metadata — the single-file docs literally use `Plugin.dll` as the example. Remember `Assembly.Location` returns `""` in single-file; use `AppContext.BaseDirectory` and `Environment.ProcessPath`.
2. **Two SKUs.** `chatdbg` (AOT, Tier 0 only, tiny, instant startup) and `chatdbg-full` (single-file JIT, Tiers 0–3). Doubles CI and support surface.
3. **AOT core + MCP-only extensibility.** Genuinely elegant: an AOT binary can still spawn MCP servers over stdio, because that is `Process.Start` and JSON, not assembly loading. You'd give up Tier 1 entirely and need source-generated `JsonSerializerContext` for every tool schema. If the product can live without in-process third-party plugins, **this is the best answer** — smallest binary, strongest isolation, one SKU.

Also note: `IncludeNativeLibrariesForSelfExtract` (in the current `SingleFile` config) is **not supported with Native AOT and, per the runtime team, likely never will be** ([dotnet/runtime#117986](https://github.com/dotnet/runtime/discussions/117986)). And a local GGUF model means a native `llama.cpp` binary regardless, which already forces you into self-extraction or loose native files.

---

## What this application specifically needs

Tying each recommendation to something ChatDbg actually does:

| Concrete operation ChatDbg performs | What it needs from this area | Recommendation |
|---|---|---|
| Renders a per-token log-probability grid with top-K alternatives over a 100k+ vocabulary | Sub-ms access to `TokenLogProbabilities[]` and `float[]` distributions without JSON round-trips | **Tier 0, in-process.** Never an MCP tool for the *rendering* path. |
| Lets the model explain its own token choice ("why did you pick `foo` over `bar`?") | The model must be able to call into token inspection over the *current session's* data | **Tier 0 exposed as a read-only `AIFunction`.** This is the killer feature and it only works in-process. |
| Talks to Azure OpenAI, Bedrock, and a local GGUF, and holds decrypted credentials in memory | Nothing third-party may share that address space | **Tier 2 (MCP) for all third-party tools.** A loaded plugin can read those keys; a child process cannot. |
| Stores provider secrets in DPAPI / libsecret (`WindowsCredentialManager.cs`) | The secret must never become a tool argument or a tool result | Credentials resolved at bind time inside Tier-0 code; never a parameter on an exposed `AIFunction`; masked by `IAuditMaskingConfiguration` in the audit log |
| `ExportCommand` / `ExportTokenAnalysisCommand` write files at a caller-chosen path | The model may propose a path; the user must approve it | `ApprovalRequiredAIFunction` + Spectre panel showing the **resolved absolute path** before writing |
| `ImportCommand` reads arbitrary files into chat history | Combined with any outbound tool this is an exfiltration primitive | **Not exposed to the model at all.** Human-only. |
| `SetCommand` / `ModelCommand` / `PromptCommand` / `InjectCommand` mutate settings, backend, and system prompt | Self-modification by the model | Not exposed, or approval-gated with the diff shown. `InjectCommand`: never. |
| Interactive REPL a user leaves open for hours, running introspection repeatedly | ALCs must not accumulate per invocation | **Fix the per-invocation `AssemblyContext` disposal.** One cached context per package. |
| `install <package>` at a prompt (the Cupcake pattern) | Curated source, real dependency resolution, real extraction, real verification | Finish `NugetWrapper` with `PackageExtractor.ExtractPackageAsync` + `ClientPolicyContext`; restrict to an ID-prefix allowlist; pin hashes after install; never model-callable |
| Ships as one self-contained file for Windows and Linux | No SDK on the target machine → no SDK certificate bundle for signature verification | Embed your own trust anchors, or verify against a shipped signed hash manifest instead of X.509 |
| Existing `Compact` config uses `PublishAot=true` | AOT forbids `AssemblyLoadContext` plugins | Pick one of the three deployment options above **before** writing the loader, not after |
| Rich Spectre.Console output (grids, heat maps, tables) | If ChatDbg also runs as an MCP stdio server, stdout is the wire | Route *all* human output through an `IConsole` abstraction that switches to stderr in MCP-server mode. This is not optional — one `Console.WriteLine` breaks the protocol. |
| Repo carries `.mcp.json` for its own development | The team already speaks this config shape | Use the identical `{ "servers": { name: { type, command, args, env } } }` schema for the shell's runtime MCP config |

---

## Risks, sharp edges and what you give up

### What you give up by choosing this recommendation

- **You give up in-process third-party extensibility as a headline feature.** Anyone who wants to write a ChatDbg tool writes an MCP server, in whatever language, and takes a process-boundary latency hit and a JSON-serializable data model. For the token-map use case that's a real loss — you cannot cheaply hand an external tool a 128k `float[]`.
- **You give up Native AOT** (unless you take deployment option 3). Slower startup, ~10× larger binary, and the `Compact` build configuration that already exists in the repo becomes dead code.
- **You take a dependency on a six-month-old GA library that shipped nine versions in six months.** `ModelContextProtocol` reached 2.0.0 on 2026-07-28 and is already at 2.2.0. Pin exactly; budget for upgrades; expect the `MCP9004`/`9005`/`9006` deprecation analyzer to fire.
- **You inherit MEAI's version cadence** — `Microsoft.Extensions.AI` shipped 10.4.1 → 10.9.0 in five months. And `ModelContextProtocol.Core` 2.2.0 hard-requires `Microsoft.Extensions.AI.Abstractions >= 10.8.3`, so the two version together whether you like it or not.

### Sharp edges, ranked

1. **The `CommandFactory` per-invocation ALC bug.** Confirmed in current `Xcaciv.Command` source. Highest-value single fix.
2. **Xcaciv packages are not on nuget.org.** Verified: `Xcaciv.Loader`, `Xcaciv.Command`, `Xcaciv.Command.Core`, `Xcaciv.Command.Interface`, and `Xcaciv.Cupcake` all return 404 from `api.nuget.org`; a `q=Xcaciv` search returns only `XCBatch.Core` and `XCBatch.Interfaces`. Cupcake's `NuGet.config` maps `Xcaciv.*` to a `local` source (`%NUGET_LOCAL_PACKAGES%` / `G:\NuGetPackages`) and `Xcaciv.Loader*` to `https://nuget.pkg.github.com/xcaciv/`, which returns **401 to anonymous requests**. **Restore is not reproducible for anyone outside the author's machine or without a GitHub PAT.** Fix before the rebuild: publish to nuget.org, or vendor as submodules/`ProjectReference`.
3. **`Xcaciv.Cupcake` is a version behind a breaking change.** It pins `Xcaciv.Command` 2.1.1; Command 3.0.0 removed `EnableDefaultCommands()`/`GetHelp()` and 3.2.3 changed `HandlePipedChunk` to take `IResult<string>`. Cupcake's own `InstallCommand` still overrides the `string` signature. Copy the *pattern* (`Loop.cs`), not the *code*.
4. **AGPL-3.0.** `Xcaciv.Loader` and `Xcaciv.Command` are AGPL-3.0; ChatDbg's `LICENSE` is GPL-3.0; `Xcaciv.Cupcake` is BSD-3-Clause. GPLv3 §13 permits the combination, but the AGPL portions carry AGPL obligations into the combined work. For a local terminal binary the network-interaction clause is mostly inert — but if ChatDbg ever grows a hosted mode, get this reviewed. The MCP SDK (Apache-2.0, with residual MIT) is one-way compatible with GPLv3 and fine.
5. **`AssemblySecurityPolicy.Strict` is Windows-shaped.** Its forbidden list (`system32`, `programfiles`, `windows defender`, …) is a case-insensitive substring match that matches nothing meaningful on Linux, while the product ships for both. Do not present it as cross-platform protection.
6. **`AssemblyPreflightAnalyzer` is a heuristic.** Metadata scanning for `Reflection.Emit`/`Expressions.Compile` is defeated by late binding. It is an audit signal.
7. **Integrity "learning mode" is trust-on-first-use.** Fine in dev, wrong in release. Gate it behind an explicit flag.
8. **Signature verification is disabled by default on macOS and Microsoft recommends leaving it so.** If the rebuild ever targets macOS, the NuGet-signature story evaporates entirely there. The shipped-hash-manifest approach does not have this problem.
9. **stdout is the MCP wire.** For an app built around Spectre.Console this is a live foot-gun.
10. **`.mcp.json` uses `@latest`.** `npx -y @upstash/context7-mcp@latest` re-resolves on every run. That is a rug-pull vector by construction — appropriate for the dev-time config, unacceptable as a pattern for the shipped runtime config. Pin versions in the runtime schema.
11. **Prompt injection through tool results is unsolved.** OWASP's [MCP Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/MCP_Security_Cheat_Sheet.html) and Microsoft's ["The state of MCP security in 2026"](https://techcommunity.microsoft.com/blog/microsoft-security-blog/the-state-of-mcp-security-in-2026/4531327) both frame prompt injection, tool poisoning (hostile instructions hidden in tool *descriptions* and metadata) and confused-deputy as the core triad. The MCP spec itself says tool annotations "should be considered untrusted, unless obtained from a trusted server." Approval gating and origin labelling are mitigations, not fixes.
12. **`McMaster.NETCore.Plugins` has not had a stable release since 2025-01-05.** If you ever swap Xcaciv.Loader for it, that 19-month gap is the risk you are accepting — offset by 1.8k stars, an active repo (pushed 2026-03-16), and the best documentation in the space.

### Second-best options and when they win

| Decision | Recommendation | Second-best | It wins when |
|---|---|---|---|
| Third-party tool transport | **MCP over stdio (`ModelContextProtocol` 2.2.0)** | `StreamJsonRpc` 2.25.29 | Every tool is yours, you need the lowest possible per-call overhead, and you don't want MCP's schema/discovery ceremony or its release cadence. You lose the entire MCP ecosystem. |
| In-process loader | **Xcaciv.Loader 2.1.2** | `McMaster.NETCore.Plugins` 2.0.0, or hand-rolled `AssemblyLoadContext` + `AssemblyDependencyResolver` (~80 lines) | You need `PluginLoader`'s explicit shared-type unification (`sharedTypes`), or you want no AGPL and no unpublished-package dependency. Hand-rolling wins if you want to own the ~80 lines and skip both — the docs' [Create a .NET Core application with plugins](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support) tutorial is the whole recipe. You lose Xcaciv.Loader's hash store and audit events. |
| Tool abstraction | **`Microsoft.Extensions.AI` `AIFunction`** | Microsoft Agent Framework | You need multi-agent orchestration, workflows, or the AG-UI human-in-the-loop integration. Overkill for a single-agent terminal shell; adds a large dependency for features ChatDbg doesn't have. |
| Package distribution | **Curated feed + `NuGet.Protocol` 7.9.0 with hash pinning** | Shell out to `dotnet nuget install` / `dotnet tool install` | The target machine reliably has an SDK. Then you inherit the SDK's verification, trust stores, and dependency resolution for free and delete a large dependency. Fails for a self-contained binary on a machine with no SDK — which is ChatDbg's stated shipping model. |
| Deployment | **Single-file self-contained, R2R, untrimmed** | AOT core + MCP-only extensibility | You can drop Tier-1 in-process plugins entirely. Smallest binary, strongest isolation, one SKU — genuinely the better architecture if the product can accept it. |

---

## Unconfirmed

Everything below I could not verify on the live web on 2026-08-28, with where I looked.

1. **Whether `Xcaciv.Loader` / `Xcaciv.Command` / `Xcaciv.Cupcake` are consumable by anyone outside the author.** Confirmed *absent* from nuget.org (404 on `api.nuget.org/v3/registration5-gz-semver2/{id}/index.json` for `xcaciv.loader`, `xcaciv.command`, `xcaciv.command.core`, `xcaciv.command.interface`, `xcaciv.cupcake`; `azuresearch-usnc.nuget.org/query?q=Xcaciv` returns only `XCBatch.*`). `https://nuget.pkg.github.com/xcaciv/index.json` returns **401** anonymously, so I could not determine whether the packages exist there, at what versions, or whether the feed is public to authenticated users. **UNCONFIRMED: whether a GitHub PAT can restore them.**
2. **`Xcaciv.Command` exact current package version.** The README says "3.3.0 (Current)"; the GitHub releases API's newest tag is `v3.3.1` (2026-02-12). The repo was pushed 2026-07-26, after that tag. **UNCONFIRMED: what version is actually published to the GitHub Packages feed today.** I read the README and release tags, not the feed.
3. **Whether the `CommandFactory` disposal issue is fixed on `main` after the last release.** I read `src/Xcaciv.Command/CommandFactory.cs` from `refs/heads/main` via `raw.githubusercontent.com`, which reflects the 2026-07-26 push, and the `using var context = …; return context.CreateInstance<…>()` pattern is present there. But I did not run it, and Xcaciv.Loader's `Unload()` may return `false` and no-op in ways I did not trace through all 1,460 lines of `AssemblyContext.cs`. **The functional consequence (ALC accumulation) is inferred from the documented cooperative-unload semantics, not measured.**
4. **MCP C# SDK trimming / Native AOT posture.** The SDK's `Directory.Build.props` sets `LangVersion=preview` and does not set `IsAotCompatible`; a GitHub code search for `IsAotCompatible` in the repo returned no usable result (the search API returned no count for an unauthenticated request). **UNCONFIRMED: whether `ModelContextProtocol.Core` 2.2.0 is annotated trim-safe/AOT-safe.** My claim that `WithToolsFromAssembly()` is reflection-based and therefore trim-hostile follows from what the API must do, not from a documented annotation. `csharp.sdk.modelcontextprotocol.io/v2/concepts/tools.html` returned 404.
5. **`ModelContextProtocol.Extensions.Apps` stability.** The package is versioned 2.2.0 with no prerelease suffix, but the .NET Blog describes server-delivered UI as "(experimental)". I could not find a statement reconciling the two. Irrelevant to ChatDbg either way.
6. **NuGet client SDK trimming/AOT compatibility.** Searches for IL2026/IL3050 warnings specific to `NuGet.Protocol` returned nothing authoritative. Given that the graph reaches `System.Text.Json` reflection paths and `NuGet.Configuration`'s XML settings loader, I *expect* it to be trim-hostile, but **UNCONFIRMED**. This matters for deployment option 1 if you later add trimming.
7. **Exact self-contained-binary behaviour of NuGet signature verification with no SDK present.** The Learn page describes SDK-driven restore. I reasoned about the library path (`PackageSignatureVerifier` + `ClientPolicyContext` from `NuGet.Packaging`) from the API surface and the `PackageExtractor` source referenced in search results. **I did not find documentation of what certificate bundle a *library* consumer gets when no SDK is installed.** Verify this experimentally before committing to X.509 verification as the trust root — it is the reason I recommend a shipped hash manifest instead.
8. **`Xcaciv.Isolation` capabilities.** I read only the repo description ("self-contained dot net 10 developer tooling that hides the plumbing… windows containers without dependency on tools like VMs, Docker or Kubernetes") and confirmed the last push was 2025-12-20 with no releases. I did not read its source. **UNCONFIRMED whether it works**; the 8-month idle period and Windows-only scope make it unsuitable regardless.
9. **`Command.Packages`** (github.com/Xcaciv/Command.Packages, "Package manager for Xcaciv.Command CommandDelegate packages", AGPL-3.0, last push 2025-12-29, no releases). I confirmed it exists but **did not read its source**; it may already contain the finished install logic that Cupcake's `NugetWrapper` stubs out. **Worth checking before rebuilding that layer.**
10. **`.NET 11` timing.** Search results consistently report a GA date of 2026-11-10 with STS support to 2028-11-09, and .NET 10 LTS to roughly 2028-11-14. I did not verify these against `dotnet/core/releases.md` or the official support-policy page directly. Targeting **.NET 10 (LTS)** is the safe call regardless; nothing in this document requires .NET 11.
11. **`AIFunctionFactory.Create` trimming annotations.** I fetched the full Microsoft Learn API page for `AIFunctionFactory.Create` and grepped it for `RequiresUnreferencedCode`, `RequiresDynamicCode`, `IL2026`, `IL3050`, and "trimming" — **no matches**. That is *absence of evidence* in generated reference docs, which routinely omit these attributes. **UNCONFIRMED: whether the `Delegate`/`MethodInfo` overloads carry trim/AOT warnings.** Verify by compiling with `PublishAot=true` before committing to deployment option 3.
12. **Whether `Spectre.Console` (0.57.2, 2026-07-02, still pre-1.0) is AOT-clean.** Not investigated — outside this brief's area, but it gates the same deployment decision.

---

*Files read locally: `/mnt/g/3RD-Party/reversing/subject/chatdbg/.mcp.json`, `/mnt/g/3RD-Party/reversing/subject/chatdbg/global.json`, `/mnt/g/3RD-Party/reversing/subject/chatdbg/src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj`, `/mnt/g/3RD-Party/reversing/subject/chatdbg/LICENSE`, and the `src/Xcaciv.ChatDbg.Core/{Commands,Models,Services}` listing.*
