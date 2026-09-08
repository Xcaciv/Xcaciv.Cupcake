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
