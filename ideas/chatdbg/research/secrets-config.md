# Cross-platform secret storage, configuration and settings persistence

*Research date: 2026-08-28. Every version number, publish date and capability claim below was checked against nuget.org, Microsoft Learn, AWS docs or the upstream source tree on the date shown. Items I could not confirm are listed under **Unconfirmed** at the end and are marked `UNCONFIRMED` inline.*

*Target assumption: .NET 10 (LTS, GA 2025-11-11, supported to Nov 2028 — [dotnet/core release notes](https://github.com/dotnet/core/blob/main/release-notes/10.0/README.md)). The subject repo already pins `10.0.100-rc.1.25451.107` in `global.json`, so .NET 10 GA is a straight forward-roll. .NET 11 is in preview (Preview 7 shipped 2026-08-11) with GA scheduled 2026-11-10; do **not** target it for a shipping binary.*

---

## Bottom line — the recommendation in three sentences

Stop storing provider keys wherever you can: for Azure OpenAI use an **explicitly constructed `ChainedTokenCredential` from `Azure.Identity` 1.21.0** (not bare `DefaultAzureCredential`), and for Bedrock let the **AWS SDK v4 default credential chain** resolve from the shared `~/.aws/config` SSO/`aws login` session — neither path puts a long-lived secret in your process or on your disk. For the keys you genuinely must persist (an Azure OpenAI API key when the user has no Entra identity, and nothing else), use **`Microsoft.Identity.Client.Extensions.Msal` 4.88.0 `Storage`** as a single encrypted-blob store — it is the only currently-maintained, Microsoft-shipped .NET code that wraps Windows DPAPI + macOS Keychain + Linux libsecret behind one API, already does `chmod 600` / Windows-ACL hardening, and exposes `VerifyPersistence()` so you can *detect* a headless box instead of silently failing open. Configuration (non-secret) goes through `Microsoft.Extensions.Configuration` 10.0.11 with a strict precedence chain into a validated Options object, persisted as `System.Text.Json` source-generated JSON in the correct per-OS config directory, and the settings POCO must **not have a property that can hold a secret at all** — the single largest defect in the source application is exactly that.

---

## Landscape — the real options

### Secret storage

| Package / API | Latest stable | Last published | Status | Verdict |
|---|---|---|---|---|
| [`System.Security.Cryptography.ProtectedData`](https://www.nuget.org/packages/System.Security.Cryptography.ProtectedData/) (Windows DPAPI) | **10.0.11** | 2026-08-11 | **GA**, in-box servicing train (11.0.0-preview.7 also 2026-08-11) | Alive and well-maintained, but **Windows-only** — `Protect`/`Unprotect` throw `PlatformNotSupportedException` on .NET Core/.NET 5+ off Windows ([API docs](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata.protect)). Use it as the Windows *leg* of a store, never as the store. |
| [`Microsoft.Identity.Client.Extensions.Msal`](https://www.nuget.org/packages/Microsoft.Identity.Client.Extensions.Msal) (`Storage`, `MsalCacheHelper`) | **4.88.0** | 2026-08-20 | **GA**. Note: the 2.x line **is** deprecated on nuget.org; the 4.x line ships from the main [MSAL.NET repo](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/tree/main/src/client/Microsoft.Identity.Client.Extensions.Msal) on MSAL's version number. The old standalone `AzureAD/microsoft-authentication-extensions-for-dotnet` repo 404s — it was folded in. | **My pick.** Only Microsoft-shipped DPAPI+Keychain+libsecret abstraction. Public API is genuinely general-purpose (see below). Cost: drags in all of `Microsoft.Identity.Client`. |
| [`Meziantou.Framework.Win32.CredentialManager`](https://www.nuget.org/packages/Meziantou.Framework.Win32.CredentialManager) | **3.0.1** | 2026-07-08 | **GA**, very actively maintained (2.0.0 → 3.0.1 in nine weeks) | Best Windows Credential Manager wrapper. **3.x targets `net10.0` / `net11.0` only** — netstandard2.0 is gone, which is fine here. Windows-only by definition. |
| [`AdysTech.CredentialManager`](https://www.nuget.org/packages/AdysTech.CredentialManager) | **3.1.0** | 2026-02-27 | **GA**, maintained but slower cadence (2.6.0 was 2022-01, then a four-year gap to 3.1.0) | Viable Windows alternative; adds `CredEnumerate`, credential attributes, and `RtlZeroMemory` wiping. 3.1.0 removed the `BinaryFormatter` fallback, which is a real security improvement. Prefer Meziantou for freshness. |
| [`SIL.PasswordStore`](https://www.nuget.org/packages/SIL.PasswordStore) | **0.1.0-beta0023** | 2026-08-21 | **PREVIEW / effectively experimental** | The only cross-platform (WinCred + libsecret) *dedicated* package I found. **~900 total downloads ever**, and it sat dormant from 2022-08-09 to 2026-08-19 before four betas in a week. Interesting to watch, **do not ship on it**. Fallback if you were tempted: roll the two P/Invokes yourself, it is ~200 lines. |
| [`SecureStore`](https://www.nuget.org/packages/SecureStore) (NeoSmart) | **1.3.0** | 2025-12-13 | GA, low cadence (1.2.2 2024-02 → 1.3.0 2025-12) | Encrypted-JSON secrets file, AES-128-CBC, targets net10.0. Solves the *wrong* problem: it needs a key file or passphrase, so you have moved the secret, not protected it. Also CBC is not AEAD. Not recommended. |
| [`GnomeStack.Os.Secrets`](https://www.nuget.org/packages/GnomeStack.Os.Secrets) | 0.1.3 | **2023-12-06** | **ABANDONED** (2 yr 9 mo since last release) | "KeyTar-like" libsecret/WinCred/Keychain wrapper. Right shape, dead. Do not use. |
| `Mjcheetham.SecureStorage` | 0.1.1-alpha | **2018-11-01** | **ABANDONED** (~8 years) | The Git Credential Manager author's early prototype. Its ideas live on inside GCM. Do not use. |
| `NativeCredentialStore` | 2.0.0 | 2024-10-18 | Alive-ish, but **misnamed for this purpose** | It is a wrapper that shells out to `docker-credential-helper`. Not an OS keystore API. |
| `SyntaxCircus.Credentials` 0.1.1 (2026-08-16, 102 downloads), `ClrKernel.Core.Secrets` 0.10.0 (2026-08-29, 814 downloads), `Kuestenlogik.Bowire.Keyring` 2.5.0 (2026-08-26, 1257 downloads) | — | 2026 | **Too new / too small to trust** | All three appeared in a NuGet search for cross-platform credential vaults. Sub-1500 downloads, single-author, no track record. Listed only so nobody thinks I missed them. |
| **In-box .NET cross-platform credential API** | — | — | **DOES NOT EXIST** | There is still no `System.Security.Credentials`-style in-box abstraction in .NET 10 or the .NET 11 previews. `UNCONFIRMED`: I could not locate a live dotnet/runtime API proposal tracking one. |

### Configuration & serialisation

| Package | Latest stable | Last published | Status | Verdict |
|---|---|---|---|---|
| `Microsoft.Extensions.Configuration` (+ `.Json`, `.EnvironmentVariables`, `.CommandLine`, `.Binder`) | **10.0.11** | 2026-08-11 | **GA** | The default. Nothing else is competitive for a .NET CLI. |
| `Microsoft.Extensions.Options` | **10.0.11** | 2026-08-11 | **GA** | Includes the `[OptionsValidator]` compile-time validation source generator. |
| `Microsoft.Extensions.Options.DataAnnotations` | **10.0.11** | 2026-08-11 | **GA** | Supplies `ValidateDataAnnotations()`. |
| `Microsoft.Extensions.Hosting` | **10.0.11** | 2026-08-11 | **GA** | Needed for `ValidateOnStart()` / `AddOptionsWithValidateOnStart<T>()` and generic-host DI, which Xcaciv.Cupcake will already be leaning on. |
| `Microsoft.Extensions.Configuration.UserSecrets` | **10.0.11** | 2026-08-11 | **GA — but this is a development tool, not a secret store.** See §3. | Reference it in test projects if you like. **Never** in the shipped CLI. |
| `System.Text.Json` (in-box) | 10.0.x in-box | 2026-08-11 (11.0.0-preview.7) | **GA** | Source generation, `JsonSourceGenerationOptions`, five built-in naming policies. |
| `Microsoft.AspNetCore.DataProtection` | 10.0.11 | 2026-08-11 | GA | Real, maintained — but its key ring has the same at-rest problem you were trying to solve. Wrong tool here; see Risks. |

### Credential chains (the "store no key at all" path)

| Package | Latest stable | Last published | Status | Verdict |
|---|---|---|---|---|
| [`Azure.Identity`](https://www.nuget.org/packages/Azure.Identity) | **1.21.0** | 2026-04-11 | **GA**. Note 1.14.2 / 1.15.0 / 1.16.0 / 1.17.0 are marked **deprecated** on nuget.org — pin ≥ 1.18.0. | Targets net8.0 / net10.0 / netstandard2.0. Use it, but build the chain explicitly. |
| `Azure.Identity.Broker` | 1.7.0 | — | GA | Only needed for OS-broker / VS Code credential. Skip for a terminal app. |
| [`AWSSDK.BedrockRuntime`](https://www.nuget.org/packages/AWSSDK.BedrockRuntime) | **4.0.101.4** | 2026-08-24 | **GA** | Depends on `AWSSDK.Core` ≥ 4.0.102.1. |
| `AWSSDK.Core` | **4.0.102.1** | 2026-08 | GA | Owns the credential chain. |
| `AWSSDK.SSO` 4.0.100.13 / `AWSSDK.SSOOIDC` 4.0.100.12 | — | 2026 | GA | **Mandatory package references** if you want IAM Identity Center (`aws sso login`) profiles to resolve — otherwise you get a *runtime* exception ([AWS docs](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/creds-idc.html)). |
| **`AWSSDK.Signin` 4.0.101.8** | — | **2026-08-24** | **GA, and new** | Required for the 2026-era `aws login` / `Invoke-AWSLogin` console-credentials flow. This did not exist in 2025 training data. Same "reference it or get a runtime exception" rule. |
| `Azure.AI.OpenAI` | 2.1.0 stable; 2.9.0-beta.1 (2026-03-13) prerelease | — | GA / preview split | Not my area, but relevant: it accepts either `AzureKeyCredential` or a `TokenCredential`, which is what makes the keyless path possible. |

---

## Analysis

### 1. The source application's design — what is actually wrong with it

I read the code rather than the summary. The behaviour lives in `src/Xcaciv.ChatDbg.Core/Models/ChatSettings.cs`, `.../Models/WindowsCredentialManager.cs` and `.../Services/SettingsService.cs`. The stated priority is env var → Windows Credential Manager → plaintext JSON. Here is what is wrong, in descending severity.

**1.1 The "deprecated" plaintext tier is not a fallback, it is the default destination.** `ChatSettings` declares:

```csharp
[JsonPropertyName("azureApiKey")] public string JsonAzureApiKey { get; set; } = "";
[JsonPropertyName("awsAccessKey")] public string JsonAwsAccessKey { get; set; } = "";
[JsonPropertyName("awsSecretKey")] public string JsonAwsSecretKey { get; set; } = "";
```

These have **no `[JsonIgnore]`**, so `SettingsService.SaveSettingsAsync` serialises them straight into `~/.ChatDbg/settings.json`. The comment immediately above that call says *"The JsonIgnore attributes on properties will ensure they're not included"* — that comment is false for exactly the three properties that matter. Meanwhile `UseWindowsCredentialManager` defaults to `false`, so on a clean install the secure tier is **switched off** and the plaintext tier is the only writable one. The design is not "plaintext as last resort"; it is "plaintext unless the user opts out."

**1.2 The precedence order puts the least-protected source first.** Environment variables are the *weakest* of the three on both platforms, and this is a debugging shell — a class of tool that gets attached to, dumped, and screen-shared:
- Linux: readable via `/proc/<pid>/environ` (same-uid, and root), visible in `ps e`, inherited by every child process the shell spawns.
- Windows: readable from another same-user process via `ReadProcessMemory` on the PEB, and captured in every full-memory minidump.
- Both: `Environment.GetEnvironmentVariables()` appears in most crash/diagnostic dumps, and Microsoft's own guidance says so plainly: *"Environment variables are commonly stored as plain, unencrypted text. If the machine or process is compromised, environment variables are accessible to untrusted parties"* ([app-secrets docs](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)).

An env-var-first order also means anyone who can set `CHATDBG_AZURE_API_KEY` in the user's shell profile can silently redirect the tool to their key. To its credit, `GetCredentialSource()` *does* report which tier won — that idea is worth keeping and promoting to a first-class `/status` output.

**1.3 Every failure path is a silent fail-open.** There are three `catch { return null; }` / `catch { return false; }` blocks — in `WindowsCredentialManager.GetCredential`, in `ChatSettings.GetFromWindowsCredentialManager`, and in the `SettingsService` constructor. A locked credential store, an ACL problem, or a marshalling bug is therefore **indistinguishable from "not configured"**, and the resolver falls through to plaintext without a word. This is the textbook credential-store bug: *fail closed with a diagnosable error, never fail open to a weaker tier.*

**1.4 The migration path prints secrets to the terminal.** `SettingsService` builds instructions of the form:

```csharp
instructions.Add($"  set CHATDBG_AZURE_API_KEY={settings.JsonAzureApiKey}");
```

That writes a live API key into terminal scrollback, into any `script`/`tee`/tmux capture, into CI logs if run non-interactively, and into shell history the moment the user pastes it. It also *teaches* the user to store the key in the weakest tier.

**1.5 Secrets live as `System.String` for process lifetime.** `CredRead`'s blob is `Marshal.Copy`'d into a `byte[]`, converted to a `string`, and neither is ever cleared. .NET strings are immutable and GC-relocatable, so copies persist. The right shape is a short-lived `byte[]`/`ReadOnlyMemory<byte>` cleared with `CryptographicOperations.ZeroMemory`, handed straight to the SDK credential object and dropped. (`SecureString` is *not* the answer — Microsoft has explicitly told people not to use it for new development.) Symmetrically, `SetCredential` allocates the blob with `Marshal.AllocHGlobal` and `FreeHGlobal`s it **without zeroing first**, leaving the plaintext key in freed unmanaged memory.

**1.6 The settings file has no permission hardening and is in the wrong place.** `SettingsService` resolves to `Environment.SpecialFolder.UserProfile` + `.ChatDbg` and writes with `File.WriteAllTextAsync`, which inherits the process umask — commonly `0022`, giving a world-readable `0644` file holding an API key. .NET 7+ has `File.SetUnixFileMode` and `FileStreamOptions.UnixCreateMode`; neither is used, and no Windows ACL narrowing is done either. The directory choice is off-convention on both OSes (see §5).

**1.7 The temp-directory fallback is a genuine local-privilege problem.** If the user-profile lookup throws, the constructor silently writes `settings.json` into `Path.GetTempPath()` — `/tmp` on Linux, shared and world-readable by every local user. A credential-bearing file must never land there, and certainly not silently.

**1.8 Windows Credential Manager is storage-at-rest, not an authorisation boundary.** `CredWrite` with `CRED_TYPE.GENERIC` and `Persist = 2` (`CRED_PERSIST_LOCAL_MACHINE` — the magic number is uncommented in the enum and the inline comment is misleading) produces a credential that **any process running as that user can read via `CredRead` with no prompt at all**. It protects the key from another *user* and from offline disk access; it does nothing against same-user malware. That is fine — it is the same guarantee macOS Keychain gives without ACL prompts and libsecret gives on an unlocked keyring — but the design should say so rather than implying vault-grade protection.

**1.9 Interop style is dated for a self-contained/AOT-trending binary.** `[DllImport]` with `CharSet.Unicode` still works, but `[LibraryImport]` (source-generated marshalling, .NET 7+) is the current form and is what trimming and AOT analysers want to see.

**1.10 There is no macOS story, and `IsAvailable()` lies.** `IsAvailable()` returns `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` — it answers "am I on Windows", not "does the store work". Those are different questions, and conflating them is precisely how §1.3 happens.

**1.11 Resolution is done per property read.** `AzureApiKey` is a computed property that runs the whole cascade, including a P/Invoke, on every access. There is no single "resolve once, cache the result, record the provenance" step — which is exactly what you need in order to report the source honestly and to zero the buffer afterwards.

**What is right and should survive the rewrite:** the provenance reporting (`GetCredentialSource`), the instinct to migrate off plaintext, and the explicit opt-in flag for the OS store. Keep all three ideas; fix the mechanism.

---

### 2. Cross-platform secret storage in .NET, as of August 2026

#### 2.1 Capability matrix

| Mechanism | OS | Protects against | Does **not** protect against | Prompts user? | .NET access |
|---|---|---|---|---|---|
| Windows Credential Manager (`CredRead`/`CredWrite`, `CRED_TYPE_GENERIC`) | Windows | Other users; offline disk; casual inspection | Same-user processes (`CredRead` succeeds silently) | No | P/Invoke, or `Meziantou.Framework.Win32.CredentialManager` 3.0.1 / `AdysTech.CredentialManager` 3.1.0 |
| Windows DPAPI (`ProtectedData`, `DataProtectionScope.CurrentUser`) | **Windows only** | Same as above, but you own the ciphertext file | Same-user processes; loss of the user profile / password reset in some configurations | No | `System.Security.Cryptography.ProtectedData` 10.0.11 |
| macOS Keychain (`SecItem*` in Security.framework) | macOS | Other users; offline disk; and *optionally* other apps via ACLs | Same-app-identity processes | Only if the ACL demands it | P/Invoke, or the MSAL extensions' `MacKeyChainAccessor` |
| freedesktop Secret Service / libsecret (gnome-keyring, KWallet via the SS API) | Linux | Other users; offline disk (keyring is encrypted at rest) | Same-user processes once the collection is unlocked | **Yes, to unlock the collection — and that prompt needs a GUI** | P/Invoke to `libsecret-1.so.0`, or the MSAL extensions' `LinuxKeyringAccessor` |
| ACL/`0600` plaintext file | All | Other non-root users | Root; same-user processes; backups; anyone with the disk | No | `File.SetUnixFileMode` / `FileStreamOptions.UnixCreateMode`; `FileSecurity` on Windows |

#### 2.2 What actually happens on a headless Linux box with no keyring

This is the case that decides the architecture, because a terminal debugging shell will be run over SSH, in a container, and in CI.

Git Credential Manager — the most battle-tested cross-platform .NET credential consumer in existence — documents the Secret Service backend as: **"⚠️ Requires a graphical user interface session … A graphical user interface is required in order to show a secure prompt to request a secret collection be unlocked"** ([GCM credential stores doc](https://github.com/git-ecosystem/git-credential-manager/blob/main/docs/credstores.md)). Concretely, on a headless box you get one of:

1. **`DllNotFoundException`** — `libsecret-1.so.0` isn't installed. Very common on minimal/container images, and note this bites *harder* for a self-contained binary, because self-contained publishing does not bring native OS libraries with it. On Alpine/musl it may be absent entirely.
2. **D-Bus failure** — libsecret loads, but there is no session bus (`DBUS_SESSION_BUS_ADDRESS` unset) so `secret_password_lookup` fails or hangs.
3. **Locked collection** — a keyring file exists but `gnome-keyring-daemon` cannot prompt for the unlock passphrase because there is no GUI, so the call fails or blocks.

Your design **must have a defined, visible behaviour** for all three, and the only correct one is: detect, tell the user, and require an explicit opt-in to anything weaker. `Storage.VerifyPersistence()` in the MSAL extensions exists for exactly this — it does a write/read/clear round trip against a `.test` sibling key and throws `MsalCachePersistenceException` if the platform store isn't usable, letting you fail *before* the user has typed a key. GCM's answer to the same problem is a menu of explicit backends (`wincredman`, `dpapi`, `keychain`, `secretservice`, `gpg`/`pass`, `cache`, `plaintext`) with the plaintext one carrying the warning **"This storage mechanism is NOT secure! Secrets and credentials are stored in plaintext files without any security!"** and *"only provided for compatibility and use in environments where no other secure option is available."* Copy that posture verbatim.

The GPG/`pass` backend is worth calling out as the one *genuinely secure* headless-Linux option: GCM supports it and notes you must configure `gpg-agent` with a terminal pinentry (`pinentry-tty` / `pinentry-curses`). It is a reasonable v2 feature for this app and costs nothing but a shell-out to `pass`/`gpg`.

#### 2.3 Why `Microsoft.Identity.Client.Extensions.Msal` and not a hand-rolled trio

Two things about it are non-obvious and decisive.

**It is a general-purpose blob store, not just a token cache.** The shipped public API is:

```
static Storage Storage.Create(StorageCreationProperties, TraceSource logger = null) -> Storage
Storage.ReadData() -> byte[]
Storage.WriteData(byte[] data) -> void
Storage.Clear(bool ignoreExceptions = false) -> void
Storage.VerifyPersistence() -> void

StorageCreationPropertiesBuilder(string cacheFileName, string cacheDirectory)
  .WithLinuxKeyring(schemaName, collection, secretLabel, attribute1, attribute2)
  .WithMacKeyChain(serviceName, accountName)
  .WithLinuxUnprotectedFile()
  .WithUnprotectedFile()
  .CustomizeLockRetry(lockRetryDelay, lockRetryCount)
  .Build()
```

(Verified against `PublicAPI.Shipped.txt` in [the MSAL source tree](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/tree/main/src/client/Microsoft.Identity.Client.Extensions.Msal).) `WriteData(byte[])` takes arbitrary bytes. You give it your own serialised secret bag. The accessor set is `DpApiEncryptedFileAccessor` (Windows), `MacKeyChainAccessor`, `LinuxKeyringAccessor`, and `FileAccessor` (the unprotected fallback).

**It already gets the file-permission details right, which is the part hand-rolled stores fail.** `Accessors/FileWithPermissions.cs` contains a `[DllImport("libc", EntryPoint = "chmod")]`, computes `Convert.ToInt32("600", 8)`, passes it to a POSIX `open(2)` at create time (so the file is never briefly world-readable), does an `lstat(2)` pre-check against symlink attacks, and on Windows builds an explicit `FileSecurity` ACL described in the source as *"'600' mode, i.e. read/write for owner translates to this in Windows."* That is a meaningful amount of security engineering you would otherwise have to write and test on three platforms.

**It is cross-process safe but not thread safe** (per [MSAL docs](https://learn.microsoft.com/en-us/entra/msal/dotnet/how-to/token-cache-serialization)) — so serialise your own calls, which is trivial in a single-user CLI.

**What it costs you:** a transitive `Microsoft.Identity.Client` reference (a full OAuth2/OIDC client you will not use), and — importantly — on Windows the secret lands in a **DPAPI-encrypted file**, *not* as an entry in Windows Credential Manager. Users therefore cannot see or revoke it via `control keymgr.dll` / `cmdkey /list`. The source application's users *could*. That is a real behavioural regression and you should decide it consciously (see the second-best option in §7).

---

### 3. `Microsoft.Extensions.Configuration.UserSecrets` — what it is for, and why it is not this

This is the most common mistake in this space, so, unambiguously: **User Secrets is a development-time convenience that stores your secrets in a plaintext JSON file.** From the current Microsoft Learn page ([Safe storage of app secrets in development](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets), doc updated 2026-07-22):

> **Warning:** Secret Manager doesn't encrypt the stored secrets and shouldn't be treated as a trusted store. It's for development purposes only. The keys and values are stored in a JSON configuration file in the user profile directory.

And the exact locations:

- Windows: `%APPDATA%\Microsoft\UserSecrets\<user_secrets_id>\secrets.json`
- Linux / macOS: `~/.microsoft/usersecrets/<user_secrets_id>/secrets.json`

Three further reasons it is structurally wrong for a **shipped** CLI, beyond "it's plaintext":

1. **It is keyed by `UserSecretsId`, a build-time project GUID**, injected via `[assembly: UserSecretsId]`. It identifies *your source project*, not the end user's installation. Shipping that GUID to every user means every install shares one secrets bucket path — a project-level identifier masquerading as a user-level one.
2. **`AddUserSecrets` is meant to be registered only in the Development environment.** `Host.CreateApplicationBuilder`/`WebApplication.CreateBuilder` add it *only* when `EnvironmentName == Development`. Wiring it unconditionally into a shipped binary defeats the one guard rail it has.
3. **The docs explicitly tell you not to depend on it:** *"Don't write code that depends on the location or format of data saved with Secret Manager. These implementation details might change."*

The honest summary: User Secrets moves a secret out of your **git repo**. It does not move it out of **plaintext**. Those are different threats and only the first is what it solves. Keep it for your own test projects; keep it out of the product.

---

### 4. Managed identity / credential chains — should a desktop CLI prefer them?

**Yes, with a specific and important qualification: prefer them, but build them explicitly rather than reaching for `DefaultAzureCredential`.**

#### 4.1 Azure — `DefaultAzureCredential` and its cost

The current chain (from [Credential chains in the Azure Identity library for .NET](https://learn.microsoft.com/en-us/dotnet/azure/sdk/authentication/credential-chains)) is, in order: Environment → Workload Identity → Managed Identity → Visual Studio → Visual Studio Code → Azure CLI → Azure PowerShell → Azure Developer CLI → Interactive Browser (**off by default**) → Broker.

Microsoft's own guidance, in that same doc, is not to ship it:

> `DefaultAzureCredential` is undoubtedly the easiest way to get started … Once you deploy your app to Azure, you should understand the app's authentication requirements. For that reason, **replace `DefaultAzureCredential` with a specific `TokenCredential` implementation**.

The three stated reasons all bite harder for a **terminal app on a developer laptop** than they do for a service:

- **Debugging challenges.** Nine credentials, each failing with a `CredentialUnavailableException`, and the user sees one generic auth failure. In a chat REPL that is an unacceptable first-run experience.
- **Performance overhead.** Positions 1–3 (Environment, Workload Identity, Managed Identity) *always fail* on a laptop, and `ManagedIdentityCredential` waits for an IMDS endpoint that isn't there. That latency lands on your first token acquisition, i.e. on the user's first prompt.
- **Unpredictable behaviour.** `DefaultAzureCredential` reads ambient environment variables that anyone can set machine-wide, silently changing which identity your tool uses. That is the same class of problem as §1.2, just moved up a layer.

**What to do instead.** Build the chain yourself and name it in `/status`:

```csharp
// Interactive terminal, developer machine:
var cred = new ChainedTokenCredential(
    new AzureCliCredential(),           // `az login` — the overwhelmingly common case
    new AzureDeveloperCliCredential(),  // `azd auth login`
    new DeviceCodeCredential());        // headless/SSH: prints a code + URL to the terminal
```

`DeviceCodeCredential` is the piece that makes this work over SSH: it writes a code and a URL to stdout and the user completes auth on any other device. `InteractiveBrowserCredential` is useless on a headless box and is excluded by default anyway. Note also that Azure.Identity ≥ 1.15.0 supports `AZURE_TOKEN_CREDENTIALS` set to a *specific credential name* (e.g. `AzureCliCredential`) or to `dev`/`prod` as a category filter — a clean escape hatch for CI without recompiling.

For Azure OpenAI specifically, keyless works via the **Cognitive Services OpenAI User** role (`5e0bd9bd-7b93-4f28-af87-19fc36ad61bd`) and a `TokenCredential` passed to the client ([Use keyless connections with Azure OpenAI](https://learn.microsoft.com/en-us/azure/developer/ai/keyless-connections), [Authenticate to Azure OpenAI using .NET](https://learn.microsoft.com/en-us/dotnet/ai/azure-ai-services-authentication)).

#### 4.2 AWS — the default chain, verbatim

From [Credential and profile resolution](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/creds-assign.html) (SDK for .NET **v4**), in order:

1. Credentials explicitly set on the service client *(documented as "isn't the preferred method")*
2. `SessionAWSCredentials` from `AWS_ACCESS_KEY_ID` + `AWS_SECRET_ACCESS_KEY` + `AWS_SESSION_TOKEN` (all three)
3. `BasicAWSCredentials` from `AWS_ACCESS_KEY_ID` + `AWS_SECRET_ACCESS_KEY`
4. `AssumeRoleWithWebIdentityCredentials` from `AWS_WEB_IDENTITY_TOKEN_FILE` + `AWS_ROLE_ARN`
5. Profile named by `AWSConfigs.AWSProfileName`
6. Profile named by `AWS_PROFILE`
7. The `[default]` profile
8. Container credential provider
9. EC2 instance metadata

Profile resolution itself searches the **SDK Store** first "if the platform supports it", then `~/.aws/credentials`, then `~/.aws/config`. Two things a 2025-trained model will get wrong here:

- **The SDK Store is real, Windows-only, and DPAPI-encrypted.** It lives at `%USERPROFILE%\AppData\Local\AWSToolkit\RegisteredAccounts.json` and is accessed via `Amazon.Runtime.CredentialManagement.NetSDKCredentialsFile` ([Using the SDK Store (Windows only)](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/sdk-store.html)). It survives into v4. It is the closest thing AWS ships to a native secure store, and it is shared with the AWS Toolkit for Visual Studio and AWS Tools for PowerShell — but it is Windows-only, so it cannot be *your* store.
- **The shared credentials file is plaintext**, stated flatly in the AWS docs: *"The shared AWS credentials file is a plaintext file."* If you tell users "put your keys in `~/.aws/credentials`", you have not solved the plaintext problem, you have delegated it. That is still better than *your* file, because it is the file every AWS tool already manages, `aws configure` writes it, and it is the file security tooling already scans for. But be honest in the docs about what it is.

**Package references are load-bearing and this is new.** AWS documents that IAM Identity Center resolution requires `AWSSDK.SSO` **and** `AWSSDK.SSOOIDC`, and that the newer browser-based `aws login` / `Invoke-AWSLogin` console-credentials flow requires **`AWSSDK.Signin`** — *"Failure to reference these packages will result in a runtime exception"* ([Authenticating the AWS SDK for .NET with AWS](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/creds-idc.html)). `AWSSDK.Signin` 4.0.101.8 was published **2026-08-24**, four days before this research; the flow issues 15-minute temporary credentials that the v4 SDK auto-refreshes.

#### 4.3 The argument, and the UX cost

**Argument for chains:** the strongest possible secret-storage design is *no secret*. Every long-lived API key you persist is a thing that can be exfiltrated, committed, screenshot, or left behind on a decommissioned laptop, and neither DPAPI nor Keychain nor libsecret protects it from the same-user process that is the realistic threat for a developer tool. A short-lived Entra token or a 15-minute AWS session token has a blast radius measured in minutes. It also removes an entire class of feature from your backlog: rotation, revocation, "where did my key go", and per-machine key sprawl.

**The UX cost, stated honestly, because it is not zero:**

| Cost | Azure path | AWS path |
|---|---|---|
| External dependency | Requires `az` (or `azd`) installed and logged in — a several-hundred-MB Python-based CLI on the user's box | Requires `aws` CLI for `aws sso login` / `aws login` |
| Cold-start friction | First run is a device-code dance in a browser, not "paste key, go" | Same |
| Recurring friction | Token expiry means a periodic re-login prompt mid-session; you must handle a 401 by re-authing gracefully, not by crashing the REPL | SSO sessions expire (typically 8–12 h); console-credential tokens are 15 min but auto-refresh |
| Admin dependency | Somebody must grant the RBAC role. A developer with only an API key handed to them by a colleague **cannot self-serve.** | Somebody must configure Identity Center and a permission set |
| Package weight | `Azure.Identity` pulls MSAL and a good deal of `Azure.Core` | `AWSSDK.SSO` + `AWSSDK.SSOOIDC` + `AWSSDK.Signin` are three extra assemblies in a self-contained binary |
| Air-gapped / offline | Fails outright — no IdP reachable | Fails outright |

**Verdict:** default to the chain, keep the key path as a documented, first-class fallback, and never make the key path the *easy* one. Concretely: `chatdbg auth login` should try the chain, and `chatdbg auth set-key` should require an explicit flag and print what protection tier it landed in.

---

### 5. Configuration for a CLI

#### 5.1 Provider precedence

`Host.CreateApplicationBuilder(args)` gives you, highest priority first ([Configuration in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration), doc updated 2026-08-28):

1. Command-line arguments
2. Environment variables
3. User secrets (Development environment only)
4. `appsettings.{Environment}.json`
5. `appsettings.json`
6. `ChainedConfigurationProvider`

The governing rule is simply *"Adding a configuration provider overrides previous configuration values … the last one added is used."* For a per-user CLI I would clear the defaults and build an explicit chain, lowest → highest:

```csharp
var b = Host.CreateApplicationBuilder(args);
b.Configuration.Sources.Clear();
b.Configuration
    .AddInMemoryCollection(ChatDbgDefaults.Values)                        // 1. shipped defaults
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
                 optional: true, reloadOnChange: false)                   // 2. install-wide
    .AddJsonFile(userSettingsPath, optional: true, reloadOnChange: true)  // 3. per-user
    .AddJsonFile(Path.Combine(Environment.CurrentDirectory, ".chatdbg.json"),
                 optional: true, reloadOnChange: true)                    // 4. per-project
    .AddEnvironmentVariables(prefix: "CHATDBG_")                          // 5. session
    .AddCommandLine(args, switchMappings);                                // 6. this invocation
```

Two mechanics worth pinning down:

- **Hierarchy separator.** Configuration keys nest with `:`. Bash cannot put a `:` in an env var name, so **all platforms accept `__` and translate it to `:`** — `CHATDBG_Llama__ContextSize=8192` binds `Llama:ContextSize`. Document the double underscore; users will get this wrong.
- **`GetRequiredSection`** throws if the section is missing, which is what you want at startup rather than a silently-defaulted object.

#### 5.2 Options pattern and validation

Use `AddOptionsWithValidateOnStart<T>()` so a bad settings file fails at process start with a readable message, not on the user's first prompt eight seconds later:

```csharp
b.Services.AddOptionsWithValidateOnStart<ChatDbgOptions>()
    .Bind(b.Configuration.GetSection(ChatDbgOptions.Section))
    .ValidateDataAnnotations()                       // Microsoft.Extensions.Options.DataAnnotations
    .Validate(o => o.Provider is not "azure" || o.AzureEndpoint is not null,
              "azureEndpoint is required when provider = azure")
    .Validate(o => o.LogProbabilitiesTopK is >= 1 and <= 20,
              "logProbabilitiesTopK must be between 1 and 20");
```

Three current details that matter for this app:

- **`ValidateOnStart()` vs `AddOptionsWithValidateOnStart<T>()`** — both exist; the latter is the cleaner form and is what the current docs lead with. Without either, validation is lazy and fires on first `.Value` access.
- **Recursive validation is opt-in.** DataAnnotations does *not* descend into nested objects or collections by default. Because your options object has a nested `Llama` sub-object and a collection of named system prompts, you need `[ValidateObjectMembers]` on the nested property and `[ValidateEnumeratedItems]` on the collection ([Options pattern](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)). This is easy to miss and silently validates nothing.
- **`[OptionsValidator]` source generator** (in `Microsoft.Extensions.Options` 10.0.11) generates the `IValidateOptions<T>` implementation at compile time — reflection-free, so it survives trimming and NativeAOT. Use it if you go AOT; use `ValidateDataAnnotations()` otherwise. `IValidateOptions<T>` implemented by hand remains the right tool for cross-field rules that need injected services.

#### 5.3 Hot reload

`IOptionsMonitor<T>` + `reloadOnChange: true` gives live reload, and it is genuinely nice in a long-running REPL: the user edits `temperature` in another window and the next turn uses it. Two caveats from the docs:

- Change notification is **file-provider only** (`Json`, `Ini`, `Xml`, `KeyPerFile`, `UserSecrets`) — an env-var change mid-session will never be noticed.
- On network shares and in containers `FileSystemWatcher` is unreliable; the documented workaround is `DOTNET_USE_POLLING_FILE_WATCHER=1`, which polls **every four seconds, non-configurable**.

For a CLI I would use `IOptionsMonitor<T>` for the *display and generation* knobs (temperature, top-K, grid view) and deliberately **not** hot-reload anything that would invalidate live state — provider, model ID, the GGUF model path, `llamaContextSize`. Swapping the loaded llama model out from under an in-flight completion is a bug generator. `IOptionsSnapshot<T>` is scoped and therefore near-useless in a console app with no request scope; ignore it.

#### 5.4 Where the per-user settings file belongs

`Environment.GetFolderPath` mappings, current as of .NET 8+ (the .NET 8 change is documented at [GetFolderPath behavior on Unix](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/8.0/getfolderpath-unix)):

| `SpecialFolder` | Windows | Linux | macOS (.NET 8+) | macOS (.NET 7 and earlier) |
|---|---|---|---|---|
| `ApplicationData` | `%APPDATA%` = `C:\Users\<u>\AppData\Roaming` | `$XDG_CONFIG_HOME`, else `$HOME/.config` | `~/Library/Application Support` (`NSApplicationSupportDirectory`) | `$HOME/.config` |
| `LocalApplicationData` | `%LOCALAPPDATA%` = `C:\Users\<u>\AppData\Local` | `$XDG_DATA_HOME`, else `$HOME/.local/share` | `~/Library/Application Support` | `$HOME/.local/share` |
| `UserProfile` | `C:\Users\<u>` | `$HOME` | `$HOME` | `$HOME` |
| `Personal` / `MyDocuments` | `Documents` | `$XDG_DOCUMENTS_DIR`, else `$HOME/Documents` | `$HOME/Documents` | `$HOME` |

Recommended layout:

| Content | Special folder | Windows | Linux | macOS |
|---|---|---|---|---|
| `settings.json` (non-secret config) | `ApplicationData` + `ChatDbg` | `%APPDATA%\ChatDbg\settings.json` | `${XDG_CONFIG_HOME:-~/.config}/chatdbg/settings.json` | `~/Library/Application Support/ChatDbg/settings.json` |
| Encrypted secret blob | `LocalApplicationData` + `ChatDbg` | `%LOCALAPPDATA%\ChatDbg\secrets.bin` | `${XDG_DATA_HOME:-~/.local/share}/chatdbg/secrets.bin` | (Keychain; file only as fallback) |
| Conversation history, named prompts | `LocalApplicationData` + `ChatDbg` | `%LOCALAPPDATA%\ChatDbg\history\` | `${XDG_DATA_HOME:-~/.local/share}/chatdbg/history/` | `~/Library/Application Support/ChatDbg/history/` |
| Tokenizer / vocab caches, downloaded GGUF metadata | **`$XDG_CACHE_HOME`, else `~/.cache`** — **no `SpecialFolder` maps to this**; read the env var yourself | `%LOCALAPPDATA%\ChatDbg\cache\` | `${XDG_CACHE_HOME:-~/.cache}/chatdbg/` | `~/Library/Caches/ChatDbg/` |

Notes:
- **The secret blob belongs in `LocalApplicationData`, not `ApplicationData`.** `%APPDATA%` roams to the domain profile in an enterprise, and DPAPI-`CurrentUser` ciphertext travelling to another machine is at best fragile. Keep the ciphertext machine-local. (`UNCONFIRMED`: the precise roaming-profile DPAPI master-key behaviour on current Windows Server builds — I did not verify it.)
- **Use lowercase `chatdbg` on Linux, `ChatDbg` on Windows/macOS.** XDG convention is lowercase; the other two are Pascal-cased by convention.
- **Honour `$XDG_*` when set even on paths you compute yourself** — a Linux user who has moved `XDG_CONFIG_HOME` expects tools to follow.
- **Provide a `CHATDBG_CONFIG_DIR` override.** Portable/USB installs, CI, and multi-tenant test rigs all need it, and it lets you write hermetic integration tests without touching the developer's real profile — which the source `SettingsService` cannot do except through its constructor parameter.
- **Never fall back to `Path.GetTempPath()`.** If the config dir is unusable, run in memory-only mode and say so. (Source-app defect §1.7.)

---

### 6. `System.Text.Json` for settings

#### 6.1 Source generation and AOT

Define one context, set options on the attribute so they are baked in at compile time:

```csharp
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ChatDbgSettingsFile))]
[JsonSerializable(typeof(SecretBag))]           // separate type, separate file, separate store
internal partial class ChatDbgJsonContext : JsonSerializerContext;
```

Key facts, from [How to use source generation in System.Text.Json](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation):

- **Two modes.** `Metadata` (needed for deserialisation and for async/streaming) and `Serialization` (the "fast path", write-only). Omit `GenerationMode` and you get both, which is what a settings file wants — you both read and write it.
- **Turn reflection off explicitly.** `<JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>` in the csproj makes accidental reflection-based calls throw an `InvalidOperationException` with a clear message *on CoreCLR too*, instead of only exploding after you publish AOT. It is set automatically when `PublishTrimmed` is on. Do this on day one — the failures are otherwise maddening to diagnose.
- **`object`-typed members must be declared.** Any member typed `object` needs its runtime types explicitly `[JsonSerializable]`'d. Relevant here: don't type any settings member as `object`.
- **Use the generic `JsonStringEnumConverter<TEnum>`**; the non-generic `JsonStringEnumConverter` is **not supported under NativeAOT**. Your `Provider` (`azure`/`bedrock`/`llama`) should be an enum with the generic converter, or a validated string — the source app's untyped `"azure"` string is a validation hole.
- **Companion:** turn on the configuration binding source generator with `<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>` (it is automatic under `PublishAot`) so `.Bind()`/`.Get<T>()` are also reflection-free ([Compile-time configuration source generation](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-generator)).

#### 6.2 Naming policies

`JsonNamingPolicy` currently exposes `CamelCase`, `SnakeCaseLower`, `SnakeCaseUpper`, `KebabCaseLower`, `KebabCaseUpper` (the four non-camel ones added in .NET 8), plus a `PascalCase` property that shows in the .NET 11 preview API docs (`UNCONFIRMED` whether `PascalCase` is present in .NET 10 — verify before using it). For a human-edited settings file, **`CamelCase` is the right choice** — it matches the source app's existing file so users' files keep working, and it matches `appsettings.json` convention. Prefer the compile-time `JsonKnownNamingPolicy.CamelCase` on `[JsonSourceGenerationOptions]` over a runtime `JsonSerializerOptions`, so the generated `Default` context is already configured.

Note that configuration **key** matching is case-insensitive while `System.Text.Json` **property** matching is case-sensitive by default. If you both bind the file through `IConfiguration` and round-trip it through `JsonSerializer`, set `PropertyNameCaseInsensitive = true` on the writer path or you will get a class of bug where a user's hand-edited `MaxTokens` binds through configuration but is dropped on the next save.

#### 6.3 Never round-tripping a secret — the mechanism, not the intention

The source app's defect (§1.1) is a design problem, not an attribute-forgetting problem. `[JsonIgnore]` on a property that *can hold a secret* is one refactor away from being deleted. The fix is to make the settings type **structurally incapable** of holding one:

1. **Two types, two files, two stores.** `ChatDbgSettingsFile` has no secret-shaped member at all — not `AzureApiKey`, not `JsonAzureApiKey`, nothing. `SecretBag` is a separate type that is *only* ever passed to `Storage.WriteData`/read from `ReadData` and never to the settings writer. There is no code path that can put one in the other, because the settings serializer's `JsonSerializerContext` does not know about `SecretBag`.
2. **Non-secret provider metadata *is* allowed in settings** — `azureEndpoint`, `awsRegion`, `modelId`, `provider`, `awsProfileName`, `credentialMode`. An endpoint URL is not a secret and putting it in the settings file is correct and useful.
3. **Add a save-time assertion.** Before writing `settings.json`, run the serialised string through a cheap high-entropy/known-prefix scan (`sk-`, `AKIA`, `ASIA`, a 32+ char base64url run) and **fail the write** if it hits. It is ten lines, it catches the regression that §1.1 represents, and it is easy to unit-test.
4. **`ConfigurationIgnoreAttribute`** (new in **.NET 11**, per the current configuration docs) excludes a property from configuration binding. It is the mirror of `[JsonIgnore]` for the bind direction and is worth adopting when you move to .NET 11 — but note it is a .NET 11 API and does *not* exist on .NET 10, so do not plan around it now.
5. **Redact on display.** `/status` and any settings dump prints `azureApiKey: <set, source: OS keystore, ****abcd>` — never the value. And whatever you do, do not reproduce `GetMigrationInstructions`' habit of echoing the live key (§1.4).

---

## What this application specifically needs

Tying each recommendation to a concrete operation the app performs.

| The app does this | Therefore it needs | Why the recommendation follows |
|---|---|---|
| Talks to **Azure OpenAI** with per-token logprobs (`logprobs` + `top_logprobs`), which is an authenticated data-plane call on every turn | A `TokenCredential` resolved **once** at startup and cached, with automatic refresh | `ChainedTokenCredential(AzureCliCredential, AzureDeveloperCliCredential, DeviceCodeCredential)`. Resolving per-turn (as `ChatSettings.AzureApiKey` does today, §1.11) puts a credential-store P/Invoke on the hot path of an interactive chat loop. |
| Talks to **Amazon Bedrock** | Nothing stored by you at all | Construct `AmazonBedrockRuntimeClient` with no explicit credentials and let the SDK chain run. Reference `AWSSDK.SSO` + `AWSSDK.SSOOIDC` + `AWSSDK.Signin` so `aws sso login` / `aws login` profiles resolve instead of throwing at runtime. Storing AWS long-lived keys yourself in 2026 is indefensible when `aws sso login` exists. |
| Runs a **local GGUF model** | Nothing — a file path is not a secret | The `llamaModelPath`, `llamaContextSize`, `llamaGpuLayerCount`, `llamaThreads`, `llamaBatchSize` knobs all belong in plain `settings.json` and are ideal `IOptionsMonitor` candidates *except* `llamaModelPath` and `llamaContextSize`, which must not hot-swap under a loaded model. |
| Is a **terminal shell run over SSH** on Linux servers | A store that *detects* the absence of a keyring rather than silently degrading | `Storage.VerifyPersistence()` at first-write. On failure: refuse to persist, print what is wrong (`libsecret-1.so.0 not found` vs `no D-Bus session` vs `collection locked`), and offer three named remedies — use the credential chain instead, export an env var for this session only, or `--insecure-file` with a loud, persistent banner. |
| Ships **self-contained** for Windows and Linux | Awareness that self-contained ≠ carries native OS libs | `libsecret-1.so.0` is *not* bundled by self-contained publish. The Linux keyring leg can `DllNotFoundException` on a minimal image. This must be a caught, named condition, not a crash. |
| Persists **conversation history, named system prompts, settings** | Three separate concerns in three separate files, only one of which is a secret store | History is append-heavy and can be large → `LocalApplicationData`, one file per conversation, never round-tripped through the settings serializer. Named prompts are user content, not config → own directory. Settings is small and hand-editable → `ApplicationData`, camelCase, hot-reloadable. |
| Reports **token-level introspection** — logprob grids, probability maps, attribution | Non-secret display knobs that users tweak constantly mid-session | Exactly the case `IOptionsMonitor` + `reloadOnChange: true` was built for. `enableLogProbabilities`, `logProbabilitiesTopK`, `showAllTokens`, `gridViewForTokens`, `gridViewMaxAlternatives` should all be live-reloadable and range-validated (`logProbabilitiesTopK` is capped at 20 by the OpenAI API — validate it, the source app does not). |
| Defines tools via **Xcaciv.Command**, loaded by **Xcaciv.Loader** | A secret-access boundary, because loaded tool assemblies run in-process | **Do not expose the resolved secret to tool code.** Tools should receive a configured client object, or an `IChatCompletionGateway`, never the key. A plugin loader plus an in-process plaintext key is a bad combination; note also that **`Xcaciv.Cupcake`, `Xcaciv.Command` and `Xcaciv.Loader` returned zero results from the nuget.org search API** — they are private/source-referenced, so I could not inspect what configuration surface they already impose. |
| Is hosted in a **Cupcake-patterned** generic host | `Microsoft.Extensions.Hosting` 10.0.11 DI, which you get for free | Register `ISecretStore` and `ICredentialResolver` as singletons; register options with `AddOptionsWithValidateOnStart`; the host's `IHostApplicationLifetime` gives you a clean place to zero secret buffers on shutdown. |
| Already has users with `~/.ChatDbg/settings.json` containing plaintext keys | A **migration** that moves and then *destroys*, and never prints | On first run: detect legacy file → read keys → write to the OS store → **overwrite the key fields in the legacy file with empty strings, save, then re-`chmod`** → print "3 credentials migrated to the OS keystore; the plaintext copies in `<path>` have been cleared. **Rotate these keys** — they have been on disk in plaintext." Rotation advice is mandatory: a key that has sat in a world-readable file must be treated as disclosed. |

---

## Risks, sharp edges and what you give up

**What you give up by picking `Microsoft.Identity.Client.Extensions.Msal`:**

1. **Dependency weight.** It transitively pulls all of `Microsoft.Identity.Client` — a complete OAuth2/OIDC public-client implementation you will not call. For a self-contained single-file binary that is real megabytes. If binary size is a hard requirement, this is the reason to take the second-best option.
2. **NativeAOT is unverified.** `UNCONFIRMED` — I found no authoritative statement that MSAL is NativeAOT-clean, and there is a known open thread that Azure.Identity/Azure.Core under NativeAOT produces runtime exceptions ([Azure/azure-sdk-for-net#38773](https://github.com/Azure/azure-sdk-for-net/issues/38773)). If NativeAOT is a requirement, prototype it in week one, not week twelve. The AWS SDK v4 is explicitly *"safe for Native AOT"* but carries its own caveat about nested types needing `DynamicDependency` hints ([v4 migration guide](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/net-dg-v4.html)).
3. **No entry in the Windows Credential Manager UI.** On Windows this writes a DPAPI-encrypted *file*, so users lose the ability to see and revoke the credential in `control keymgr.dll` / `cmdkey /list` — a capability the source application gave them. This is the most user-visible regression in the recommendation.
4. **API impedance.** The class is called `Storage` and lives in a namespace called `...Extensions.Msal`. Any reviewer will ask why an LLM chat tool depends on an auth library. Wrap it behind your own `ISecretStore` and put a comment explaining the choice, or the next maintainer will "clean it up".
5. **Cross-process, not cross-thread, safety.** Serialise your own calls.

**Sharp edges regardless of choice:**

- **`ProtectedData` throws, it does not degrade.** `PlatformNotSupportedException` on every non-Windows platform, for `Protect` and `Unprotect` alike. Guard with `OperatingSystem.IsWindows()` (which the trimmer understands) rather than `RuntimeInformation.IsOSPlatform`, so the non-Windows branch can actually be trimmed away.
- **DPAPI is bound to the user profile, and profiles get reset.** A domain password reset by an admin, a profile corruption, or a machine reimage can render `CurrentUser` blobs undecryptable. Ciphertext you cannot decrypt must produce "your saved credential could not be decrypted; please re-enter it", not a crash and not a fall-through to plaintext.
- **libsecret on musl.** `DllNotFoundException` on Alpine is the default outcome, not the exception.
- **The Secret Service unlock prompt can block.** Under some configurations the call waits for a prompt that will never appear. Consider a timeout around the first store access.
- **`FileSystemWatcher` under WSL2, network shares and containers.** Hot reload will silently not fire. Document `DOTNET_USE_POLLING_FILE_WATCHER=1` and its fixed four-second interval.
- **Config hot reload + secret store do not compose.** `IOptionsMonitor` reloads a *file*; it will not notice a credential changed in the OS keystore. Offer an explicit `/reload-credentials` command rather than pretending it is automatic.
- **`Azure.Identity` 1.14.2 / 1.15.0 / 1.16.0 / 1.17.0 are deprecated on nuget.org.** Pin ≥ 1.18.0, ideally 1.21.0, and put it in a `Directory.Packages.props` with central package management so a transitive reference cannot drag a deprecated version back in.
- **`Meziantou.Framework.Win32.CredentialManager` 3.x dropped netstandard2.0.** Fine at .NET 10; a problem if any part of the solution still multi-targets.
- **`ConfigurationIgnoreAttribute` is .NET 11 only.** Don't design around it yet.
- **Everything you print is a disclosure channel.** A debugging shell that pretty-prints token grids is a tool people screen-share and paste into issue trackers. Redaction has to be centralised (one `Redact()` helper used by `/status`, error messages, and any config dump), not sprinkled.

**What you give up by choosing the credential chain over stored keys:** the ability to onboard a user who was handed an API key by a colleague and has no Entra/IAM identity of their own. That user is a real persona for a developer tool. Hence: chain first, key path supported and documented, never the default.

---

## Recommendation — the concrete layered scheme

### Configuration precedence (non-secret), lowest wins to highest wins

| # | Source | Reload | Notes |
|---|---|---|---|
| 1 | Compiled-in defaults (`AddInMemoryCollection`) | no | Never empty, never null. |
| 2 | `$(AppContext.BaseDirectory)/appsettings.json` | no | Optional; lets an admin ship an org default next to the binary. |
| 3 | `<config-dir>/settings.json` | **yes** | The file `/set` writes. camelCase, `WriteIndented`. |
| 4 | `./.chatdbg.json` in the working directory | **yes** | Per-project overrides — very natural for a debugging tool. |
| 5 | `CHATDBG_*` environment variables (`__` → `:`) | no | Session/CI scoping. |
| 6 | Command-line arguments | no | This invocation only. |

Bound to a single `ChatDbgOptions` via `AddOptionsWithValidateOnStart<ChatDbgOptions>().Bind(...).ValidateDataAnnotations().Validate(...)`, with `[ValidateObjectMembers]` on the nested `LlamaOptions` and `[ValidateEnumeratedItems]` on the named-prompt collection. `EnableConfigurationBindingGenerator` on.

### Secret resolution precedence, first hit wins

| # | Tier | Persisted? | When it is used | Reported as |
|---|---|---|---|---|
| 0 | `--api-key` on the command line | **never** | One-shot / scripted use | `command-line (this session only)` |
| 1 | `CHATDBG_AZURE_API_KEY` env var | no | CI, containers, deliberate scoping | `environment variable (not persisted)` |
| 2 | **Provider credential chain** — `ChainedTokenCredential` (Azure) / AWS SDK default chain | no secret exists | **The default and preferred path** | `Azure CLI identity` / `AWS SSO profile <name>` |
| 3 | OS secret store via `Storage` (DPAPI file / Keychain / libsecret) | yes, encrypted | User explicitly ran `chatdbg auth set-key` | `OS keystore (DPAPI \| Keychain \| Secret Service)` |
| 4 | `0600` file, **explicit opt-in only** | yes, **plaintext** | `chatdbg auth set-key --insecure-file`, after tier 3 was proven unavailable | `INSECURE FILE — not encrypted` |
| — | Settings file | **never, at any tier** | — | — |

Note the inversion relative to the source app: env var moves *below* the chain, and there is no unconditional plaintext tier at all.

### Defined behaviour when no secure store is available

At the moment the user first tries to persist a secret:

1. Call `Storage.VerifyPersistence()`. If it succeeds → tier 3, done.
2. If it throws → **do not write anything**. Print the specific cause and three named options:
   - `chatdbg auth login` — use `az login` / `aws sso login` instead and store nothing *(recommended)*
   - `export CHATDBG_AZURE_API_KEY=…` — this session only, nothing on disk
   - `chatdbg auth set-key --insecure-file` — writes an **unencrypted** `0600` file
3. If `--insecure-file` is chosen: create the file with `FileStreamOptions.UnixCreateMode = UserRead | UserWrite` (and a narrowed `FileSecurity` ACL on Windows), then `File.SetUnixFileMode` again afterwards because the create-time mode is filtered by umask; record `credentialSource = "insecure-file"` in state; print a **persistent banner on every startup** while that tier is in use.
4. Never fall back automatically. Never fall back silently. Never fall back to `Path.GetTempPath()`.

### Second-best option, and the condition under which it wins

**Hand-rolled `ISecretStore` with three `[LibraryImport]` backends:** Windows Credential Manager via `Meziantou.Framework.Win32.CredentialManager` 3.0.1 (or direct `LibraryImport` of `CredReadW`/`CredWriteW`), Linux via `libsecret-1.so.0` (`secret_password_store_sync` / `secret_password_lookup_sync`), macOS via `Security.framework` `SecItemAdd`/`SecItemCopyMatching`, with the same `0600`-file fallback tier.

**It wins when any of these is true:**
- **NativeAOT or hard binary-size limits are a requirement.** You control every P/Invoke and take no MSAL dependency.
- **You must preserve per-key visibility in the OS credential UI.** One Credential Manager entry per key (`ChatDbg:AzureApiKey`, …) means users can inspect and revoke individual credentials with `cmdkey`, Seahorse, or Keychain Access — matching what the source application's users have today.
- **A security review objects to an auth library appearing in the dependency graph of a chat tool.**

**What that costs you:** you now own `chmod`-before-write ordering, `lstat` symlink checks, Windows ACL construction, D-Bus session detection, `DllNotFoundException` handling on musl, unmanaged-buffer zeroing, and cross-process locking — all of which `FileWithPermissions.cs` and the accessor set already implement and test upstream. Budget it as real work, not a weekend.

**Third option, worth knowing but not recommending here:** shell out to `git credential` and inherit whatever backend Git Credential Manager is already configured with on that machine. Zero native interop, GCM's full backend menu including `gpg`/`pass` for headless Linux, and users often already have it working. Rejected as the primary because it makes a Git installation a hard runtime dependency of an LLM chat tool, and the `git-credential` protocol is a poor fit for non-URL-shaped secrets. Reconsider it if you later want a first-class headless-Linux secure store without writing a `pass` integration yourself.

---

## Unconfirmed

Things I could not verify on the live web, and where I looked:

1. **NativeAOT compatibility of `Microsoft.Identity.Client` / `Microsoft.Identity.Client.Extensions.Msal`.** No authoritative "AOT-supported" statement found on nuget.org, MSAL docs, or the MSAL repo README. I found a related open concern for Azure.Core/Azure.Identity under NativeAOT ([Azure/azure-sdk-for-net#38773](https://github.com/Azure/azure-sdk-for-net/issues/38773)) but nothing conclusive for MSAL itself. **Prototype this before committing if AOT matters.**
2. **`JsonNamingPolicy.PascalCase` availability in .NET 10.** It appears in the [JsonNamingPolicy API page](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonnamingpolicy) whose default moniker is `net-11.0-pp`, with no description text and no "introduced in" annotation. The other five policies are confirmed (CamelCase since .NET Core 3.0; the snake/kebab pair since .NET 8). Verify against your actual SDK before using `PascalCase`.
3. **Whether an in-box cross-platform credential-store API is proposed for .NET.** Multiple searches of dotnet/runtime issues turned up nothing current. I am confident none *exists* in .NET 10 or the .NET 11 previews; I could not confirm whether one is *proposed*.
4. **Precise roaming-profile behaviour of DPAPI `CurrentUser` master keys** on current Windows Server / Entra-joined configurations — i.e. exactly when a `%APPDATA%`-resident DPAPI blob does and does not decrypt on a second machine. I recommend `%LOCALAPPDATA%` specifically to avoid needing this answer.
5. **`Xcaciv.Cupcake`, `Xcaciv.Command`, `Xcaciv.Loader`** — the nuget.org search API (`azuresearch-usnc.nuget.org/query`) returns **0 hits** for all three, with prerelease included. They are private or source-referenced. I therefore could not check whether they already impose a configuration/DI shape (their own `IConfiguration` wiring, an existing settings abstraction, or a plugin isolation boundary that affects where secrets may live). **This is the single largest gap in this analysis** — the DI and configuration recommendations above assume a stock `Microsoft.Extensions.Hosting` generic host, which may need adjusting.
6. **Exact download counts / verified-publisher status** for `SyntaxCircus.Credentials`, `ClrKernel.Core.Secrets` and `Kuestenlogik.Bowire.Keyring` beyond the NuGet search API's `totalDownloads` field. I did not open their repos; I am recommending against them on cadence and adoption alone.
7. **Whether the AWS SDK v4 `SharedCredentialsFile` writer applies restrictive file permissions** when `RegisterProfile` creates `~/.aws/credentials`. The AWS docs warn about plaintext keys but say nothing about mode bits. Assume it does not, if you ever write that file yourself — which you should not.
8. **`SecureStore` 1.3.0's exact cipher/KDF construction.** The nuget description says AES-128-CBC; I did not read the source to check whether it authenticates the ciphertext (an unauthenticated CBC secrets file would be a meaningful weakness). Moot given the recommendation against it.

---

### Source index

nuget.org: [ProtectedData](https://www.nuget.org/packages/System.Security.Cryptography.ProtectedData/) · [Meziantou.Framework.Win32.CredentialManager](https://www.nuget.org/packages/Meziantou.Framework.Win32.CredentialManager) · [AdysTech.CredentialManager](https://www.nuget.org/packages/AdysTech.CredentialManager) · [SIL.PasswordStore](https://www.nuget.org/packages/SIL.PasswordStore) · [SecureStore](https://www.nuget.org/packages/SecureStore) · [Microsoft.Identity.Client.Extensions.Msal](https://www.nuget.org/packages/Microsoft.Identity.Client.Extensions.Msal) · [Azure.Identity](https://www.nuget.org/packages/Azure.Identity) · [AWSSDK.BedrockRuntime](https://www.nuget.org/packages/AWSSDK.BedrockRuntime) · [Microsoft.Extensions.Options](https://www.nuget.org/packages/Microsoft.Extensions.Options/) · [Microsoft.Extensions.Options.DataAnnotations](https://www.nuget.org/packages/Microsoft.Extensions.Options.DataAnnotations/) · [Microsoft.Extensions.Configuration.Binder](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Binder/)

Microsoft Learn: [Safe storage of app secrets in development](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) · [Configuration in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration) · [Options pattern](https://learn.microsoft.com/en-us/dotnet/core/extensions/options) · [Compile-time configuration source generation](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-generator) · [System.Text.Json source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation) · [JsonNamingPolicy](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonnamingpolicy) · [ProtectedData.Protect](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata.protect) · [GetFolderPath behavior on Unix (.NET 8 breaking change)](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/8.0/getfolderpath-unix) · [Credential chains in Azure Identity for .NET](https://learn.microsoft.com/en-us/dotnet/azure/sdk/authentication/credential-chains) · [MSAL token cache serialization](https://learn.microsoft.com/en-us/entra/msal/dotnet/how-to/token-cache-serialization) · [Keyless connections with Azure OpenAI](https://learn.microsoft.com/en-us/azure/developer/ai/keyless-connections) · [File.SetUnixFileMode](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.setunixfilemode)

AWS: [Credential and profile resolution (v4)](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/creds-assign.html) · [Shared AWS credentials file (v4)](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/creds-file.html) · [Using the SDK Store (Windows only)](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/sdk-store.html) · [Authenticating the AWS SDK for .NET with AWS](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/creds-idc.html) · [Migrating to v4](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/net-dg-v4.html)

Source trees: [MSAL extensions (Storage, accessors, FileWithPermissions, PublicAPI.Shipped.txt)](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/tree/main/src/client/Microsoft.Identity.Client.Extensions.Msal) · [Git Credential Manager credential stores](https://github.com/git-ecosystem/git-credential-manager/blob/main/docs/credstores.md) · [dotnet/core .NET 10 release notes](https://github.com/dotnet/core/blob/main/release-notes/10.0/README.md)

Subject repo (read directly): `src/Xcaciv.ChatDbg.Core/Models/ChatSettings.cs`, `src/Xcaciv.ChatDbg.Core/Models/WindowsCredentialManager.cs`, `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs`, `global.json`
