### 7.15 Packaging, Build & Release Distribution

**Description**

Packaging, Build & Release Distribution is the machinery that turns the source tree into something a stranger can actually run. The product is a locally-run terminal application: it is not installed from an app store, it is not fetched from a package registry, and it has no installer. It is delivered as **one downloadable executable file per operating system**, which a user copies onto a machine and runs with nothing pre-installed — no managed runtime, no framework, no dependency manager, no companion configuration file. This feature exists to make that promise true and to make it repeatable.

Three problems are in scope. First, **zero-prerequisite distribution**: every dependency — the language runtime itself, all third-party libraries, and all large native machine-learning payloads — must be bundled inside the one delivered file. Second, **size and start-up discipline**: a chat and debugging shell that a developer launches many times a day must not be a hundred-megabyte download with a half-second cold start, so the feature defines two named build profiles that trade build time against artefact size and launch latency, and applies aggressive dead-code removal, compression, size-first code generation, English-only resource retention and culture-invariant operation to get there. Third, **repeatable, auditable publication**: a maintainer must be able to cut a versioned public release for two platforms at once, from a clean machine, by filling in one short form, and receive a tagged release with generated notes and both binaries attached without hand-uploading anything.

The feature is also, deliberately, the place where all the *packaging consequences* of the rest of the product are recorded. Aggressive dead-code removal is only safe if nothing important is reached by name at run time; culture-invariant operation changes how the shipped program compares and formats strings; suppressing debug metadata changes what a crash report looks like; and the decision that the running program writes all of its state into per-user directories rather than beside itself is exactly what makes a single read-only downloaded file a viable unit of distribution. Those constraints are stated here as contracts that every other feature must honour. The source as analysed contains a substantial number of defects in this area — a malformed toolchain pin, a pipeline that installs the wrong major runtime line, local scripts that inspect an output folder a successful build never creates, and a shipping profile that suppresses a start-up file the shipping profile needs — all of which are recorded verbatim in **Quirks** rather than silently repaired.

---

**User stories**

- **US-15.1** — As an end user, I want to download exactly one file for my operating system and run it with nothing else installed, so that I can try the product without setting up a language runtime or a package manager.
- **US-15.2** — As an end user on a POSIX system, I want the release page to tell me the single extra step my platform needs (setting the execute permission bit), so that I am not stuck at a "permission denied" message.
- **US-15.3** — As an end user, I want the downloaded program to keep its settings, prompts and logs somewhere under my own user account rather than beside the executable, so that I can put the binary on a read-only or shared location and so that a second copy of the binary shares the same state.
- **US-15.4** — As an end user or operator, I want to be able to configure a packaged binary entirely through environment variables, so that nothing has to be shipped, installed, or edited alongside it.
- **US-15.5** — As a release manager, I want to publish a versioned release for both supported platforms by filling in one form with a version string and a pre-release checkbox, so that cutting a release is a single deliberate action rather than a manual build-and-upload chore.
- **US-15.6** — As a release manager, I want the two platform binaries built in parallel and published only when *both* succeed, so that a half-published release can never exist.
- **US-15.7** — As a release manager, I want a release body generated for me that lists the exact asset file names, the system requirements, and the three usage steps, so that a downloader has instructions without me writing them each time.
- **US-15.8** — As a release manager, I want to mark a release as a pre-release, so that early builds are visibly distinguished from stable ones.
- **US-15.9** — As a release manager, I want a run summary at the end of the pipeline showing the release version, the version scraped from the project, the pre-release flag, the two asset names, and a direct link to the published release, so that I can confirm what was published without leaving the run page.
- **US-15.10** — As a consumer of the release (a human or a download script), I want the asset file names to be fixed and predictable, so that automation can fetch them by name.
- **US-15.11** — As a developer on a workstation, I want scripted local builds of each distribution profile, so that I can smoke-test what a downloader will actually receive without running the whole publication pipeline.
- **US-15.12** — As a developer on a workstation, I want an interactive chooser that offers the native profile, the bundled profile, or both, so that I do not have to remember which script does what.
- **US-15.13** — As a developer on a workstation, I want each build to report where it wrote its output and to list the produced executables with their sizes, so that I can confirm the artefact exists and check it against the size budget.
- **US-15.14** — As a developer or an automated caller, I want a failed build to return the build tool's own non-zero exit code, so that the failure is detectable by whatever invoked the script.
- **US-15.15** — As a maintainer, I want the build toolchain pinned to one exact version with a controlled roll-forward policy, so that every machine and every build agent compiles the product the same way.
- **US-15.16** — As a maintainer, I want the shipped binary to carry the product's identity metadata (version, title, company, product, copyright), so that a support engineer can tell which build a user is running.

*(Not written as stories, because the source supports no such capability: there is no automatic release on a code change, no draft or approval gate, no rollback, no checksum or signature, no macOS or ARM artefact, no installer, no test gate, and no publication of the full-screen terminal shell. See FR-15.90 through FR-15.97.)*

---

**Use cases**

#### UC-15.1 — Produce the smallest native artefact locally (realizes US-15.11, US-15.13, US-15.14)

**Preconditions:** A workstation running the Windows family. The pinned build toolchain is installed and the pin document parses. Network access is available for dependency restore. The repository is checked out. *(A native ahead-of-time compiler and linker is also required but is named nowhere in the product's prerequisites — see QUIRK-15.9.)*

**Main flow:**
1. The operator runs the compact build script from any working directory; the script first anchors the working directory to its own location, so every later path is repository-relative.
2. The script prints `Building ChatDbg with Compact configuration...` and `This will create the smallest possible binary using AOT compilation and trimming.`
3. The script prints `Restoring packages for win-x64 runtime...` and restores dependencies for the 64-bit Windows platform triple. The restore names the platform triple only; it never names the configuration.
4. The script prints `Cleaning previous builds...` and cleans the prior output of the `Compact` configuration.
5. The script prints `Building and publishing Compact configuration...` and publishes the console-shell project in the `Compact` configuration, self-contained, for the 64-bit Windows platform triple, with re-restore disabled.
6. On success the script prints `Build completed successfully!` followed by `Output location: src\ChatDbg\bin\Compact\net9.0\win-x64\publish\`.
7. The script lists the executables found under that path with their sizes.
8. The script waits for a keypress and exits with code 0.

**Alternate flows:**
- **A1 — The scripted variant that reports in mebibytes:** the same sequence, printed in colour, and each executable is reported as `Executable: <file name> - Size: <n> MB` where `<n>` is mebibytes rounded to exactly 2 decimal places. It closes with `Press any key to continue...`.
- **A2 — An explicit platform triple is supplied on the command line:** the profile's built-in default of 64-bit Windows is only applied when no platform triple was supplied, so the explicit value always wins.

**Error flows:**
- **E1 — The toolchain pin document cannot be parsed:** the build tool refuses to run at all and reports a configuration-parse failure, not a compile failure. Nothing is built. *(At the analysed commit this is the state of the source — see QUIRK-15.1. INFERRED consequence; not executed.)*
- **E2 — Restore produced no assets for the requested platform triple:** the publish fails with a message of the form *"project.assets.json doesn't have a target for 'net9.0/win-x64'"*. The documented remedy is to use the interactive script instead, or to hard-delete the project's intermediate directory and the configuration's binary directory and restore again.
- **E3 — Publish returns a non-zero code:** the script prints `Build failed with error code <n>` and exits with that same code `<n>`. The mebibyte-reporting variant prints the same sentence in red and exits with the same code.
- **E4 — The native compiler and linker are absent:** the publish fails during native compilation. Nothing in the product installs or even names this toolchain; the stated machine prerequisites are only a managed toolchain, 8 GB of RAM and solid-state storage. *(INFERRED — see QUIRK-15.9.)*
- **E5 — The output listing finds nothing:** the script's post-build listing inspects a hard-coded folder named for the *previous* framework version, while a successful build writes to a folder named for the current one. A successful build therefore prints the shell's own "file not found" text and looks like a build that produced nothing. See QUIRK-15.11.
- **E6 — The script is invoked headlessly:** it reaches the closing keypress prompt and blocks forever rather than returning. See QUIRK-15.13.

**Postconditions:** On success, a per-configuration/per-framework/per-platform publish folder contains a native executable, no symbol side-car, no runtime-configuration side-car, and no non-English resource payloads — but *also* the separate per-platform native inference payloads, because this profile sets no bundling switch (see QUIRK-15.8). The prior output of that configuration has been deleted.

---

#### UC-15.2 — Produce the bundled single-file artefact locally (realizes US-15.11, US-15.13, US-15.14)

**Preconditions:** As UC-15.1, minus the native compiler and linker.

**Main flow:**
1. The operator runs the single-file build script. It anchors the working directory to its own location.
2. It prints `Building ChatDbg with SingleFile configuration...` and `This creates a single executable file that can be run standalone.`
3. It prints `Cleaning previous builds...`, cleans, then force-deletes the `SingleFile` binary directory outright.
4. It prints `Restoring packages for win-x64 runtime...` and restores for the 64-bit Windows platform triple.
5. It prints `Building and publishing SingleFile configuration...` and publishes the console-shell project in the `SingleFile` configuration, self-contained, for that platform triple, with re-restore disabled.
6. On success it prints `Build completed successfully!`, then `Output location: src\ChatDbg\bin\SingleFile\net9.0\win-x64\publish\`, then `Executable files:`, then for each executable a line of the form `  <file name> - Size: <n> bytes`.
7. It asserts, verbatim, `The executable is standalone and can be copied to any Windows machine.` followed by `No .NET runtime installation required.`
8. It waits for a keypress and exits with code 0.

**Alternate flows:** none — this script offers no options.

**Error flows:**
- **E1 — Publish returns a non-zero code:** the script prints `Build failed with error code <n>`, then `Troubleshooting:`, then exactly three numbered items — `1. Ensure .NET 9 SDK is installed`, `2. Check network connectivity for package restore`, `3. Verify project file syntax` — and exits with code `<n>`. The first item names a stale runtime line; see QUIRK-15.16.
- **E2 — The output listing finds nothing:** as UC-15.1 E5, for the same reason.
- **E3, E4, E5** — the pin-parse failure, the restore/platform-triple mismatch and the headless hang behave exactly as UC-15.1 E1, E2 and E6.

**Postconditions:** One executable file exists in the publish folder, containing the runtime, all managed libraries, all native libraries and all content files, compressed. The `SingleFile` binary directory was destroyed and rebuilt.

---

#### UC-15.3 — Choose a build profile interactively (realizes US-15.12)

**Preconditions:** As UC-15.1.

**Main flow:**
1. The operator runs the interactive build script. It anchors the working directory to its own location.
2. It prints this menu block verbatim, then prompts on the same line:

```
ChatDbg Build Options
=====================
1. Compact (AOT) - Smallest binary with native compilation (single .exe)
2. SingleFile - Single file executable with JIT compilation
3. Both - Build both configurations

Select build option (1-3):
```

3. The operator types `1`, `2` or `3`.
4. For the native profile the script prints `Building ChatDbg with Compact (AOT) configuration...` and `This creates the smallest possible native binary using AOT compilation.`, then the banner `=== COMPACT (AOT) BUILD ===`, then `Cleaning previous Compact builds...`, cleans, hard-deletes the project's **whole** intermediate directory and the `Compact` binary directory, prints `Restoring packages for Compact configuration...`, restores for the 64-bit Windows platform triple, publishes with re-restore disabled, and on success prints `Compact build completed successfully!` and `Output: <path>`.
5. For the bundled profile the script prints `Building ChatDbg with SingleFile configuration...` and `This creates a single executable file with JIT compilation.`, then `=== SINGLE FILE BUILD ===`, `Cleaning previous SingleFile builds...`, hard-deletes the `SingleFile` binary directory, prints `Restoring packages for SingleFile configuration...`, restores, publishes, and on success prints `SingleFile build completed successfully!` and `Output: <path>`.
6. For choice `3` it prints `Building both configurations...` and runs the native build first, then the bundled build.
7. After each build it prints a listing section headed `<config> Files:` and, per executable, `  <file name> - <n> bytes (~<n> bytes)` — the same byte count twice — or the literal `  No executable files found` when the folder holds none. It then prints `All files in <config> output:` and a full listing.
8. It closes with `Build process completed!`, then `Notes:`, then exactly two lines: `- Compact (AOT): Native compilation, fastest startup, smallest size` and `- SingleFile: JIT compilation, single file, faster builds`.
9. It waits for a keypress and exits.

**Alternate flows:**
- **A1 — Any input other than `1`, `2` or `3`:** the script prints `Invalid choice. Building Compact by default.` and proceeds with the native build.
- **A2 — Empty input (the operator presses Enter without typing):** identical to A1 — the choice variable is left unset, so none of the three equality tests match and the default branch runs.

**Error flows:**
- **E1 — A publish returns a non-zero code:** the script prints `Compact build failed with error code <n>` or `SingleFile build failed with error code <n>` and **continues to the next step anyway**. The listing section then reports `  No executable files found`. The script's own exit code never reflects the failure. See QUIRK-15.14.
- **E2 — Choice `3` ("Both"):** the second build cannot start, because the listing subroutine run after the first build overwrites the process executable-search-path variable with the publish folder. The build tool is reported as not recognised. Choices `1` and `2` are unaffected in practice because only shell-internal commands follow their listing. See QUIRK-15.12.
- **E3 — The listing always reports nothing:** as UC-15.1 E5.
- **E4 — Side effect of the hard-delete:** deleting the *whole* intermediate directory destroys restore state for every other configuration as a side effect of building one. This is simultaneously why this script is reliable and why it is slow.

**Postconditions:** Zero, one or two artefacts exist. Restore state for all configurations of the console-shell project has been destroyed.

---

#### UC-15.4 — Publish a versioned release for both platforms (realizes US-15.5 through US-15.10)

**Preconditions:** The operator holds permission to dispatch the release pipeline. The publication credential is present as a repository secret named `GH_PATT`. The tag about to be created does not already exist.

**Main flow:**
1. The operator opens the release pipeline and supplies two inputs: `version` (required, free string, pre-filled `v1.0.0`) and `prerelease` (optional boolean, default `false`), then starts the run. There is no other trigger — no push, no tag, no schedule.
2. Two build jobs fan out and run in parallel, one per platform row: the `windows` row on the latest Windows agent producing a `.exe` suffix, and the `linux` row on the latest Ubuntu agent producing no suffix. Their display names render as `Build windows Binary` and `Build linux Binary`.
3. Each row, in strict order: checks out the source; installs the managed toolchain; restores dependencies for the row's platform triple (without naming a configuration); publishes the console-shell project in the `SingleFile` configuration, self-contained, with re-restore disabled and with bundling and in-bundle compression re-asserted explicitly on the command line, into `./publish/<row key>/`.
4. Each row derives three names: the **project version**, scraped by matching the version element in the console-shell project file (falling back to the literal `1.0.0` when no match is found); the **built executable name**, the fixed base name `Xcaciv.ChatDbg.Shell` plus the row's suffix; and the **release file name**, `chatdbg-<row key>-<platform triple><suffix>` — yielding exactly `chatdbg-windows-win-x64.exe` and `chatdbg-linux-linux-x64`.
5. Each row renames the built file in place to the release file name, echoes its long listing, and measures and echoes its byte size.
6. Each row uploads exactly one file as the sole member of an intermediate artefact named `chatdbg-<row key>-binary`, retained for **7 days**.
7. The release job waits for **both** rows to succeed, then checks out the source again, downloads all intermediate artefacts into `./artifacts/`, and prints a recursive listing of every downloaded file. This yields the fixed paths `./artifacts/chatdbg-windows-binary/chatdbg-windows-win-x64.exe` and `./artifacts/chatdbg-linux-binary/chatdbg-linux-linux-x64`.
8. The release job resolves the release version: the operator's `version` input verbatim if non-empty; otherwise `v` prefixed to the freshly re-scraped project version.
9. The release job writes a fixed-template notes document to a file named `release_notes.md` in its working directory, interpolating only the release version.
10. The release job creates the release: tag = the release version; title = `ChatDbg <release version>`; body = the notes file; pre-release = true only when the input string is exactly `true`; draft = false, i.e. immediately public; assets = the two hard-coded file paths; authentication = the `GH_PATT` secret.
11. The release job appends a run-summary block naming the release version, the scraped project version, the raw pre-release input, both asset names, and a hyperlink to the release page constructed from the repository slug and the tag.

**Alternate flows:**
- **A1 — Pre-release checked:** the created release is flagged as a pre-release; the assets, notes and tag are otherwise identical.
- **A2 — Version input left at its pre-filled value:** the release is tagged `v1.0.0`.
- **A3 — Fallback release version:** because the `version` input is *required*, the "otherwise use `v` + project version" branch is unreachable through the dispatch form.

**Error flows:**
- **E1 — The toolchain pin document cannot be parsed:** every row fails before compiling; nothing is published. *(INFERRED — see QUIRK-15.1.)*
- **E2 — The installed toolchain does not satisfy the pin or the target framework:** restore or publish fails before any compilation, both rows fail, fail-fast cancels, the release job never runs and **no release is produced**. At the analysed commit the pipeline installs a *major runtime line older than the one the projects target*, so this is the pipeline's actual state. See QUIRK-15.2. *(INFERRED — the pipeline was not run.)*
- **E3 — The version scrape finds no match:** it falls back silently to `1.0.0`, and a release can be published carrying a wrong project version in its summary.
- **E4 — The project file contains more than one version element:** the scrape emits *every* match, producing a multi-line step output that the step-output file format rejects. The step fails and takes the whole release with it. See QUIRK-15.6.
- **E5 — The rename cannot find the expected built executable** (for example because the project base name changed): the step fails, the row fails, fail-fast cancels the other row, and nothing publishes.
- **E6 — One platform row fails for any reason:** the release job is skipped entirely. The surviving row's artefact remains downloadable as an intermediate for 7 days but is never published. Because no fail-fast opt-out is declared, the default applies and the *other* row is cancelled rather than allowed to finish.
- **E7 — The release tag already exists:** release creation fails with *"Cannot create releases with existing tag names."*
- **E8 — The publication credential is missing or insufficient:** release creation fails at the final step, *after* both binaries were built, renamed and uploaded.
- **E9 — The operator's version string contains shell metacharacters:** the string is interpolated into shell command text before the shell sees it, so the injected commands execute on the build agent — in the same job that holds the long-lived publication credential. See QUIRK-15.5.

**Postconditions:** Either a public, non-draft release exists carrying exactly two executable assets and the generated notes, or nothing at all was published. There is no draft state, no staging state and no rollback path. Intermediate artefacts expire 7 days after the run; the released assets persist indefinitely.

---

#### UC-15.5 — Download and run the released artefact (realizes US-15.1, US-15.2, US-15.3, US-15.4)

**Preconditions:** A machine running Windows 10 or 11, or a modern Linux distribution. No managed runtime installed. A writable per-user profile area and a writable temporary area.

**Main flow:**
1. The user opens the published release page and reads the generated notes.
2. The user downloads the asset matching their platform: `chatdbg-windows-win-x64.exe` or `chatdbg-linux-linux-x64`.
3. On the POSIX platform the user sets the execute permission bit, exactly as the notes' step 2 instructs.
4. The user runs the file directly. No installation, no runtime download, no companion file.
5. On first start the program creates and populates its per-user state: a settings document at `<user profile>/.ChatDbg/settings.json` (written with defaults on the very first read), a seeded prompt directory at `<local application data>/ChatDbg/system_prompts/`, and — only if inference logging runs — a log directory at `<application data>/ChatDbg/Logs`.
6. Nothing is written into the directory the executable was copied to.
7. On clean shutdown the process exits with code `0`.

**Alternate flows:**
- **A1 — Configuration supplied by environment:** when `CHATDBG_AZURE_API_KEY` or `CHATDBG_AWS_ACCESS_KEY` is set, the environment value is used **in preference to** any stored value, and the reported credential source contains the phrase `environment variable`. When `AWS_ACCESS_KEY_ID` is present, the cloud client switches to the ambient credential chain.
- **A2 — Second copy of the same binary:** because all state is per-user rather than per-install, a second copy of the executable elsewhere on the machine shares the same settings, prompts and logs.
- **A3 — Platform-specific capability absent:** the operating-system credential vault exists on only one platform. On the other platform reading a credential that does not exist simply returns "no value" — it never throws and never surfaces a platform error — so the same single binary runs normally on both.

**Error flows:**
- **E1 — Unhandled failure at run time:** the program prints exactly one line, `Fatal error: <exception message>`, on standard output and exits with code `1`. There is no stack trace and no log file. In a native `Compact` build the message is a short symbolic key rather than a sentence, and no stack frames exist at all.
- **E2 — A code path removed by dead-code elimination is reached:** the failure appears as a type-load, missing-method or missing-member error surfaced through E1. Because all dead-code-analysis diagnostics are suppressed at build time, there is no earlier warning.
- **E3 — A name-based platform lookup was removed:** the miss is swallowed and treated as an empty result, so the feature silently returns nothing and **no error is reported at all**. See QUIRK-15.4.
- **E4 — The user-profile lookup fails or resolves empty:** the settings store silently falls back to writing into the temporary directory and reports nothing. Settings appear to save and then vanish. See QUIRK-15.19.
- **E5 — No writable temporary area:** the bundled artefact fails to start, because in-bundle compression together with both self-extraction switches forces an unpack to a per-user cache before any application code runs. This contradicts the product's own "copy one file anywhere and run it" claim. See QUIRK-15.7. *(INFERRED.)*
- **E6 — The bundled artefact will not start at all:** the shipping profile suppresses the runtime-configuration side-car that a bundled, just-in-time artefact needs, so the host reports a start-up configuration failure before any application code runs. See QUIRK-15.3. *(INFERRED; corroborated by the absence of a runtime-options block in the one committed pre-built artefact, which was deliberately not executed.)*
- **E7 — A native inference backend fails to load:** a native access-violation code surfaces instead of a managed error. Documented packaging-related causes: the native payloads are missing from the expected per-platform native folder; the backend is incompatible with the toolchain version; security software is blocking native library execution (a Windows-only hazard); or the graphics-vendor driver does not match the GPU backend. Documented packaging remedies: drop the GPU backend, or downgrade the whole inference stack.
- **E8 — Culture-sensitive behaviour differs from development:** the packaged artefact runs culture-invariant with only English resources present, so comparison, casing and formatting results can differ from what a developer saw. This is intended, not a fault.

**Postconditions:** The program has run without writing anything next to itself, and its exit code is `0` on clean shutdown or `1` after any unhandled failure.

---

#### UC-15.6 — Bump the product version (realizes US-15.16)

**Preconditions:** The maintainer intends to publish a new version.

**Main flow:**
1. Edit the three version facts in the console-shell project descriptor: product version (3-part), assembly version (4-part) and file version (4-part).
2. Repeat all three, identically, in the graphical-shell project descriptor — the two descriptors declare version metadata independently and must be edited in lock-step.
3. Separately edit the hard-coded version string shown in the graphical shell's about box; it is not derived from build metadata and drifts silently otherwise.
4. Dispatch the release pipeline with a matching `v`-prefixed version input.

**Alternate flows:** none. There is no version-bump automation of any kind.

**Error flows:**
- **E1 — Only one descriptor edited:** the two shells report different versions; nothing detects it.
- **E2 — The about-box string not edited:** the graphical shell reports a version that disagrees in both value and shape with the declared one. See QUIRK-15.17.
- **E3 — The dispatch version does not match the edited version:** the release tag and the binary's embedded version disagree; nothing detects it, and the run summary shows both side by side without comparing them.

**Postconditions:** Three independent version facts have been hand-reconciled, or have silently diverged.

---

**Functional requirements**

*Toolchain, framework and repository-wide compile settings*

- **FR-15.1** — The build toolchain SHALL be pinned repository-wide to one exact toolchain version, expressed in a machine-readable pin document at the repository root. (realizes US-15.15)
- **FR-15.2** — The pin SHALL declare a roll-forward policy of "newest patch of the newest feature band within the same major and minor version, never a different major or minor". (realizes US-15.15)
- **FR-15.3** — The pinned toolchain version in the source as analysed is a **pre-release** build. A reimplementation SHALL record whether depending on a pre-release toolchain is intentional.
- **FR-15.4** — The pin document SHALL be valid, parseable structured text. At the analysed commit it is **not** — see QUIRK-15.1 — and this gates every build, local and automated.
- **FR-15.5** — All four projects (the console shell, the graphical shell, the shared library and the test project) SHALL compile against one single target framework identifier, corresponding to runtime major version 10.
- **FR-15.6** — The only repository-wide compiler setting SHALL be "use the newest language version available", applied by directory inheritance to all four projects.
- **FR-15.7** — Implicit namespace imports SHALL be enabled in all four projects, so source files omit the common import preamble.
- **FR-15.8** — Compile-time null-state checking SHALL be enabled in all four projects. It is diagnostic only: nothing anywhere promotes warnings to errors, so a nullability violation never fails a build or a release.
- **FR-15.9** — Raw pointer and unmanaged-memory access SHALL be permitted in the shared library only, and SHALL NOT be permitted in the two executables or the test project. It is required by the local-inference integration.
- **FR-15.10** — There SHALL be no repository-wide warning level, no warnings-as-errors setting, no deterministic-build flag, no continuous-integration-build flag and no source-link configuration. Reproducible builds are consequently not achievable.
- **FR-15.11** — There SHALL be no dead-code-safety annotation, no statically-analysable serialization context, no dead-code-elimination root list and no linker descriptor anywhere in the source. Combined with FR-15.24 and FR-15.33, this means nothing was ever checked for safety under dead-code elimination.

*Build profiles*

- **FR-15.12** — Exactly four named build profiles SHALL exist: `Debug`, `Release`, `Compact` and `SingleFile`. (realizes US-15.11)
- **FR-15.13** — Only `Debug` and `Release` SHALL be enumerated in the workspace descriptor. `Compact` and `SingleFile` SHALL be reachable only by naming an individual executable project and configuration explicitly on a command line.
- **FR-15.14** — `Compact` and `SingleFile` SHALL be defined only in the two executable projects, byte-identically between them. The shared library and the test project SHALL define no configuration-conditioned settings at all.
- **FR-15.15** — Under `Compact` the product SHALL be compiled ahead-of-time to native machine code. (realizes US-15.11)
- **FR-15.16** — Under `Compact` unreferenced code SHALL be removed by reachability analysis.
- **FR-15.17** — Under `Compact` the runtime SHALL be bundled with the application (self-contained), so no runtime prerequisite exists on the target machine. (realizes US-15.1)
- **FR-15.18** — Under both distribution profiles, when the caller supplies **no** target platform triple, the build SHALL default to 64-bit Windows. The condition SHALL be "only when empty", so an explicitly supplied triple always wins.
- **FR-15.19** — Under both distribution profiles a native launcher stub SHALL be emitted so the artefact is directly executable.
- **FR-15.20** — Under `Compact` the dead-code-elimination granularity SHALL be **full** — every assembly is trimmed, including the framework. This is maximum size reduction and maximum risk to reflection-based code paths.
- **FR-15.21** — Under `SingleFile` the dead-code-elimination granularity SHALL be **partial** — only assemblies that opt in are trimmed. This is a deliberate concession so that reflection-heavy dependencies survive; it is why the *shipping* profile is the gentler one.
- **FR-15.22** — Under `SingleFile` the product SHALL be bundled into one file while retaining just-in-time compilation. (realizes US-15.1)
- **FR-15.23** — Under `SingleFile` the bundled payload SHALL be compressed inside the executable, trading a decompression cost at first start for download size.
- **FR-15.24** — Under both distribution profiles all dead-code-analysis warnings SHALL be suppressed. Unsafe patterns are silently accepted and surface only at run time.
- **FR-15.25** — Under `SingleFile` native libraries SHALL be included in the self-extraction set; without this, platform-specific native payloads are not carried inside the one file.
- **FR-15.26** — Under `SingleFile` **all** content files SHALL be included in the self-extraction set.
- **FR-15.27** — Under both distribution profiles no debug information of any kind SHALL be produced: no symbol side-car and no in-binary debug data.
- **FR-15.28** — Under both distribution profiles the runtime-configuration side-car SHALL NOT be emitted. This is harmless for the native profile (its host reads no such file) and is the suspected fatal defect in the bundled profile that actually ships — see QUIRK-15.3.
- **FR-15.29** — Under both distribution profiles only English localisation resources SHALL be retained; all other language payloads SHALL be dropped.
- **FR-15.30** — Under both distribution profiles the compile SHALL be explicitly optimised. The flag must be set explicitly because neither profile name is one the build tool recognises as optimised by default.
- **FR-15.31** — The shared library, which holds all domain logic, SHALL be compiled **unoptimised** under both distribution profiles, because it declares no configuration-conditioned settings and the profile names are not recognised as optimised. See QUIRK-15.30. *(INFERRED from the absence of the setting plus the profile-name mismatch; not directly asserted anywhere, and no build was run.)*
- **FR-15.32** — Under both distribution profiles the artefact SHALL run culture-invariant (no culture data). Culture-sensitive comparison, casing and formatting collapse to invariant behaviour. (realizes US-15.1 — it removes a per-machine dependency)
- **FR-15.33** — Under both distribution profiles framework exception and message strings SHALL be replaced with short symbolic keys. **This is user-visible:** run-time error text in a distribution build is an identifier, not a sentence.
- **FR-15.34** — Under `Compact` the native code generator SHALL be tuned for size rather than speed.
- **FR-15.35** — Under `Compact` identical method bodies SHALL be folded together, which reduces size and makes stack frames ambiguous.
- **FR-15.36** — Under `Compact` stack-trace metadata SHALL NOT be embedded. **This is user-visible:** crash reports and caught-exception traces carry no frame names.
- **FR-15.37** — Under `Compact` native symbols SHALL be stripped from the produced binary.
- **FR-15.38** — Under `Compact` assembly-identity metadata SHALL NOT be synthesised and the target-framework marker SHALL NOT be stamped. A `Compact` artefact therefore loses its version, title, company, product and copyright. See QUIRK-15.25.
- **FR-15.39** — Under `SingleFile`, in direct contrast to FR-15.38, assembly-identity metadata **SHALL** be synthesised and the target-framework marker **SHALL** be stamped, so the released artefact carries version `1.0.0`, title, company, product and copyright. (realizes US-15.16)
- **FR-15.40** — `Compact` SHALL set no bundling switch, and therefore SHALL NOT produce a single file while the native inference payloads are referenced. Its output is a **folder**. See QUIRK-15.8.

*Product identity metadata*

- **FR-15.41** — Product version SHALL be the 3-part string `1.0.0`; assembly version and file version SHALL both be the 4-part string `1.0.0.0`. These are declared independently and identically in **both** executable project descriptors and nowhere else, and must be edited in lock-step. (realizes US-15.16)
- **FR-15.42** — Fixed identity strings SHALL be: title `ChatDbg Shell` (used by *both* executables — the graphical one is not differentiated), description `Cross-platform chat debugging tool with AWS Bedrock and Azure OpenAI support`, company `Xcaciv`, product `ChatDbg`, copyright `Copyright © Xcaciv 2024`.
- **FR-15.43** — The graphical shell's about box SHALL display the hard-coded literal `ChatDbg v1.0`, which no build step, script or pipeline stage updates and which already disagrees in shape with the declared `1.0.0`. See QUIRK-15.17.

*Dependency pinning*

- **FR-15.44** — Third-party dependency versions SHALL be pinned exactly, listed per project, with no central version file and no committed lock file. See **External technology** for the full pinned set.
- **FR-15.45** — Three dependencies (the two hosted-provider clients and the console-rendering library) SHALL be referenced redundantly by the console-shell executable even though the shared library already references them, requiring manual synchronisation of duplicated pins.
- **FR-15.46** — The test project SHALL be explicitly marked non-packable and explicitly flagged as a test project, and its runner-integration package SHALL contribute no compile-time or transitive surface. *(Standard test-adapter isolation, recorded so it is not re-flagged.)*
- **FR-15.47** — Both executable projects SHALL exclude named source sub-trees from compilation. Most of the named folders no longer exist on disk, making the exclusions inert; the graphical project excludes one folder that does exist and then re-includes by name the only two files in it, so that exclusion is also inert.

*Local build scripts*

- **FR-15.48** — Four local build scripts SHALL exist: a compact build, a compact build that reports sizes in mebibytes, a single-file build, and an interactive chooser. All four target the **console shell only** and the **64-bit Windows platform triple only**. (realizes US-15.11)
- **FR-15.49** — Every script SHALL first anchor its working directory to the script's own location, so all later paths are repository-relative.
- **FR-15.50** — Ordering guarantee: every script SHALL restore with an explicit platform triple *before* publishing, and SHALL publish with re-restore disabled.
- **FR-15.51** — The restore step SHALL pass only the platform triple and SHALL NOT pass the configuration name, so restore resolves assets under the default configuration.
- **FR-15.52** — The simple compact script SHALL restore *before* cleaning, so the clean can invalidate what restore just produced. The interactive script SHALL invert this: clean → hard-delete intermediates → hard-delete the configuration's binaries → restore → publish. See QUIRK-15.15.
- **FR-15.53** — The interactive script's hard-delete SHALL remove the console-shell project's **whole** intermediate directory — not just the configuration being built — destroying restore state for every other configuration as a side effect.
- **FR-15.54** — The interactive chooser SHALL print its six-line menu block verbatim as reproduced in UC-15.3 step 2, prompt on the same line, and map `1` → native build, `2` → bundled build, `3` → both (native first). (realizes US-15.12)
- **FR-15.55** — Any other input — including empty input from pressing Enter — SHALL print `Invalid choice. Building Compact by default.` and proceed with the native build. (realizes US-15.12)
- **FR-15.56** — Each script SHALL, on success, print `Build completed successfully!` (or `<profile> build completed successfully!` in the interactive script), print the publish folder path, and enumerate the produced executables. (realizes US-15.13)
- **FR-15.57** — The mebibyte-reporting script SHALL print, per executable, `Executable: <file name> - Size: <n> MB` with `<n>` rounded to exactly **2** decimal places; the byte-reporting scripts SHALL print `  <file name> - Size: <n> bytes`; the interactive script SHALL print `  <file name> - <n> bytes (~<n> bytes)` with the same byte count twice. Units are therefore inconsistent across scripts. See QUIRK-15.20 and QUIRK-15.21. (realizes US-15.13)
- **FR-15.58** — When the expected folder holds no executables, the interactive script SHALL print the literal `  No executable files found`; the byte-reporting batch scripts SHALL surface the shell's own "file not found" text; the mebibyte-reporting script SHALL print nothing at all.
- **FR-15.59** — Every script SHALL hard-code its post-build listing path with the **previous** framework version, so it inspects a folder a successful build never creates. See QUIRK-15.11. The verbatim stale paths are `src\ChatDbg\bin\Compact\net9.0\win-x64\publish\` and `src\ChatDbg\bin\SingleFile\net9.0\win-x64\publish\`.
- **FR-15.60** — The three single-purpose scripts SHALL propagate the build tool's exit code on failure, printing `Build failed with error code <n>` and exiting with `<n>`. (realizes US-15.14)
- **FR-15.61** — The single-file script SHALL additionally print, on failure, `Troubleshooting:` followed by exactly three numbered lines: `1. Ensure .NET 9 SDK is installed`, `2. Check network connectivity for package restore`, `3. Verify project file syntax`. The first item names a runtime line older than the one the projects require. See QUIRK-15.16. (realizes US-15.14)
- **FR-15.62** — The interactive script SHALL, on failure, print `<profile> build failed with error code <n>` and **continue**; its overall exit code SHALL NOT reflect the failure. See QUIRK-15.14.
- **FR-15.63** — Every script SHALL block on a keypress before exiting, making all four unsuitable for unattended or automated invocation. See QUIRK-15.13.
- **FR-15.64** — The single-file script SHALL print, verbatim on success, `The executable is standalone and can be copied to any Windows machine.` and `No .NET runtime installation required.` (realizes US-15.1)
- **FR-15.65** — The interactive script SHALL close with `Build process completed!`, then `Notes:`, then the two lines `- Compact (AOT): Native compilation, fastest startup, smallest size` and `- SingleFile: JIT compilation, single file, faster builds`.
- **FR-15.66** — No script SHALL build, run or mention the test project; there is no local quality gate.
- **FR-15.67** — All four scripts are written for the Windows family only (two shell dialects, backslash paths, Windows-only directory-removal syntax, a Windows-only keypress call, the Windows executable suffix). **No POSIX developer build script exists at all**, even though a POSIX binary is one of the two released artefacts.

*Release pipeline*

- **FR-15.68** — The release pipeline SHALL be **manual-dispatch only**. There SHALL be no push, tag, schedule or merge trigger. (realizes US-15.5)
- **FR-15.69** — The dispatch form SHALL accept exactly two inputs: `version` — required, free string, pre-filled `v1.0.0` — and `prerelease` — optional, boolean, default `false`. (realizes US-15.5, US-15.8)
- **FR-15.70** — Exactly two platform rows SHALL be defined: row key `windows` on the latest Windows agent with platform triple `win-x64` and suffix `.exe`; row key `linux` on the latest Ubuntu agent with platform triple `linux-x64` and no suffix. (realizes US-15.6)
- **FR-15.71** — The two rows SHALL run in parallel and SHALL be fully independent. Job display names interpolate the row key verbatim and therefore render lower-cased as `Build windows Binary` and `Build linux Binary`. See QUIRK-15.22.
- **FR-15.72** — Ordering guarantee, per row and in strict order: check out source → install the managed toolchain → restore for the row's platform triple → publish → derive names → rename → upload.
- **FR-15.73** — The publish SHALL name the console-shell project, the `SingleFile` configuration, self-contained, the row's platform triple, re-restore disabled, and SHALL re-assert bundling and in-bundle compression explicitly on the command line even though the named configuration already sets both. *(Redundant, not wrong. Note that command-line properties become global and flow to referenced projects too.)*
- **FR-15.74** — The publish output directory SHALL be explicitly `./publish/<row key>`, bypassing the default per-configuration/per-framework/per-platform layout and thereby side-stepping the stale-path problem of FR-15.59.
- **FR-15.75** — The publish command SHALL be written twice, once per host shell dialect, gated on the row key; any change must be applied to both copies.
- **FR-15.76** — The project version SHALL be scraped by text pattern from the raw console-shell project descriptor, falling back to the literal `1.0.0` when unmatched. It SHALL NOT be read from build output metadata. Renaming or reformatting the version element silently degrades the result to `1.0.0`.
- **FR-15.77** — The built executable base name SHALL be the fixed literal `Xcaciv.ChatDbg.Shell`, with no override declared anywhere.
- **FR-15.78** — The release file name SHALL be `chatdbg-<row key>-<platform triple><suffix>`, producing exactly `chatdbg-windows-win-x64.exe` and `chatdbg-linux-linux-x64`. These names are a public contract that download automation may rely on. (realizes US-15.10)
- **FR-15.79** — Each row SHALL rename the built file in place to the release file name, echo its long listing, and measure and echo its byte size. The measured size SHALL be written to a step output that **nothing ever reads** — no size appears in the notes, the release body or the run summary. See QUIRK-15.23.
- **FR-15.80** — The renaming step SHALL run under a POSIX-style shell **even on the Windows agent**, which therefore requires such a shell to be present on that agent.
- **FR-15.81** — Each row SHALL upload exactly one file as the sole member of an intermediate artefact named `chatdbg-<row key>-binary`, with a retention window of **7 days**. *(Tunable default — 7 days is the pipeline's chosen value, not a business rule.)*
- **FR-15.82** — The release job SHALL run only after **both** rows succeed. If either row fails, the release job SHALL be skipped and nothing SHALL be published. (realizes US-15.6)
- **FR-15.83** — No fail-fast opt-out SHALL be declared, so the default applies and one row's failure cancels the other. (realizes US-15.6)
- **FR-15.84** — Ordering guarantee, release job, in strict order: check out source again → download all intermediate artefacts into `./artifacts/` → print a recursive listing of every downloaded file → resolve the release version → write the notes document → create the release → append the run summary.
- **FR-15.85** — The downloaded artefact paths SHALL be exactly `./artifacts/chatdbg-windows-binary/chatdbg-windows-win-x64.exe` and `./artifacts/chatdbg-linux-binary/chatdbg-linux-linux-x64`.
- **FR-15.86** — The release version SHALL be the operator's `version` input verbatim when non-empty; otherwise `v` prefixed to the freshly re-scraped project version. Because the input is required, the fallback branch is unreachable through the dispatch form.
- **FR-15.87** — The release notes document SHALL be written to a file named `release_notes.md` in the release job's working directory, from a **fixed template that interpolates only the release version**. It SHALL NOT list actual file sizes, checksums, the commit hash, or a change log. (realizes US-15.7) The template, verbatim:

```
## ChatDbg Release <release version>

### Features
- Cross-platform chat debugging tool
- Support for AWS Bedrock and Azure OpenAI
- Interactive console interface with Spectre.Console

### Downloads
- **Windows x64**: `chatdbg-windows-win-x64.exe` - Single file executable for Windows 10/11
- **Linux x64**: `chatdbg-linux-linux-x64` - Single file executable for Linux distributions

### System Requirements
- No .NET runtime installation required (self-contained)
- Windows 10/11 (for Windows build) or modern Linux distribution (for Linux build)

### Usage
1. Download the appropriate binary for your platform
2. Make executable (Linux): `chmod +x chatdbg-linux-linux-x64`
3. Run the application: `./chatdbg-linux-linux-x64` or `chatdbg-windows-win-x64.exe`

Built with .NET 9 using SingleFile publishing for optimal deployment.
```

  *(This is a user-visible document, reproduced exactly. Two of its lines are defects a reimplementation must decide about: the third Features bullet names a third-party rendering library in end-user product copy — QUIRK-15.24 — and the trailing provenance line names a runtime major version the projects no longer target — QUIRK-15.16.)*

- **FR-15.88** — The release SHALL be created with: tag = the release version; display title = `ChatDbg <release version>`; body = the notes file; pre-release = true **only when the operator's input string is exactly `true`**; draft = `false`, i.e. immediately public with no review gate; assets = the two **hard-coded** file paths of FR-15.85; authentication = a repository secret named `GH_PATT` holding a long-lived personal access token rather than the ambient job credential. See QUIRK-15.33. (realizes US-15.5, US-15.8)
- **FR-15.89** — Adding a platform row SHALL require a second, separate edit, because the release asset paths are hard-coded rather than derived from the platform matrix.
- **FR-15.90** — After publication the pipeline SHALL append a run-summary block, verbatim in structure: (realizes US-15.9)

```
## Release Created Successfully!

**Release Version:** <release version>
**Project Version:** <scraped project version>
**Pre-release:** <the raw input value>

### Binaries Created:
- Windows x64: `chatdbg-windows-win-x64.exe`
- Linux x64: `chatdbg-linux-linux-x64`

[View Release](https://github.com/<owner>/<repo>/releases/tag/<release version>)
```

- **FR-15.91** — The pipeline SHALL restore **without naming a configuration** and then publish a *different* configuration with re-restore disabled — structurally the same mismatch the local scripts hit. It has not caused a failure only because the output directory is overridden. See QUIRK-15.10.
- **FR-15.92** — The version scrape SHALL emit **every** match rather than the first. This is harmless while exactly one version element exists; a second produces a multi-line step output the format rejects. See QUIRK-15.6.
- **FR-15.93** — The operator-supplied version string SHALL be interpolated into shell command text before the shell sees it, in a conditional test, an assignment and the notes document. See QUIRK-15.5.
- **FR-15.94** — The release job SHALL download into a directory name the repository's own ignore rules exclude, so a local reproduction of the job would silently hide its own downloads from repository-aware tooling. See QUIRK-15.26.
- **FR-15.95** — The publishing component SHALL be referenced by a floating major-version tag rather than a pinned revision.
- **FR-15.96** — The release job SHALL check out the source again for the sole purpose of re-scraping the version.
- **FR-15.97** — The pipeline SHALL declare **no** explicit permissions block, and SHALL have no environment protection rule, no approval gate and no draft state. A dispatch goes straight to a public release.

*What is deliberately not built or published*

- **FR-15.98** — The **graphical terminal shell SHALL NOT be released**. It carries identical `Compact` and `SingleFile` profiles but no script and no pipeline step ever targets it. Only the plain console shell is scripted and published. This means the released binary is not the interface the product documentation presents as primary. See QUIRK-15.27.
- **FR-15.99** — **No tests SHALL be executed anywhere in the release pipeline or in any build script**, although a suite of 100 test cases across 38 files exists. There is no quality gate of any kind between a dispatch and a public, non-draft release. See QUIRK-15.31.
- **FR-15.100** — **No macOS artefact SHALL be produced**, despite documentation listing macOS platform triples (`osx-x64`, `osx-arm64`) as supported targets and two documents showing exactly how to add the row. See QUIRK-15.37.
- **FR-15.101** — **No 32-bit and no ARM target SHALL be produced**, on any operating system.
- **FR-15.102** — **No library package SHALL be produced** for the shared core library; it carries no version metadata and no packaging settings.
- **FR-15.103** — **No installer, archive, checksum file, signature, attestation or software bill of materials SHALL be produced.** Raw executables are attached directly.
- **FR-15.104** — **No licence file, notice file or written offer of source SHALL be attached to the release**, even though the repository ships a strong copyleft licence. A reimplementation must decide how the equivalent obligations are met. See QUIRK-15.32.
- **FR-15.105** — There SHALL be no dependency cache, no build cache and no incremental-build optimisation in the pipeline; every run restores from scratch. Local scripts go further and deliberately destroy incremental state before every build.

*Contracts the packaged artefact owes to the rest of the product*

- **FR-15.106** — **Process exit contract.** A packaged artefact SHALL exit with code `0` on clean shutdown, and with code `1` after any unhandled failure, preceded by exactly one line `Fatal error: <exception message>` on standard output. This is the only machine-readable signal a packaged artefact emits. There is no stack trace and no log file on that path.
- **FR-15.107** — **Dead-code-elimination contract.** Any code reached only by reflection, dynamic type discovery or name-based lookup is at risk of removal. `Compact` trims across the whole framework; `SingleFile` trims conservatively. Because analysis diagnostics are suppressed, violations appear only as run-time failures in the packaged build.
- **FR-15.108** — **Globalisation contract.** Packaged builds run culture-invariant with only English resources present, so culture-sensitive behaviour differs between a development build and a packaged build.
- **FR-15.109** — **Diagnostics contract.** Packaged builds carry no debug information; `Compact` additionally carries no stack-trace metadata and substitutes symbolic keys for framework message text. Error text a user reports from a packaged build will not match the text seen in development.
- **FR-15.110** — **Asset-naming contract.** Exactly the two literal file names of FR-15.78 SHALL be published, and they SHALL be referenced by those same literal names in the generated notes and the run summary. (realizes US-15.10)
- **FR-15.111** — **Read-only-install contract.** A packaged artefact SHALL write nothing into the directory it was copied to. All persistent state SHALL resolve through well-known per-user directory lookups at run time. (realizes US-15.3)
- **FR-15.112** — Those per-user locations SHALL be exactly three, under **two different naming conventions**: the settings document at `<user profile>/.ChatDbg/settings.json`; the prompt directory at `<local application data>/ChatDbg/system_prompts/`; the log directory at `<application data>/ChatDbg/Logs`. See QUIRK-15.18. (realizes US-15.3)
- **FR-15.113** — When the user-profile lookup throws or resolves empty, the settings store SHALL silently fall back to `<temp>/settings.json` inside a catch-all and report nothing. See QUIRK-15.19.
- **FR-15.114** — Loading settings when no settings document exists SHALL return a populated default object rather than failing, **and** SHALL write those defaults on that first read, so a freshly downloaded binary starts on a machine with no prior state and no shipped configuration file. The default model identifier SHALL be non-empty. (realizes US-15.1)
- **FR-15.115** — Constructing the prompt store SHALL create its directory and seed the default prompts, so first run of a downloaded binary provisions itself. (realizes US-15.1)
- **FR-15.116** — **Environment-configuration contract.** Credential values SHALL be read from environment variables **in preference to** stored values, and the reported credential source SHALL contain the phrase `environment variable`. The recognised names are `CHATDBG_AZURE_API_KEY`, `CHATDBG_AWS_ACCESS_KEY`, and `AWS_ACCESS_KEY_ID` (whose mere presence switches the cloud client to the ambient credential chain). A packaged single file is therefore fully configurable through the environment with no companion file. (realizes US-15.4)
- **FR-15.117** — **One-source-two-platforms contract.** The released artefacts SHALL be built for two platform triples from one unmodified source tree with one build profile: no per-platform source variant, no per-platform profile, no conditional compilation symbol anywhere.
- **FR-15.118** — The single genuinely platform-specific runtime capability — the operating-system credential vault — SHALL be guarded by a run-time operating-system check rather than a compile-time one. Writing a credential SHALL succeed exactly when the host operating system is the supported one; reading a credential that does not exist SHALL return "no value" on **every** operating system, never throwing and never surfacing a platform error. This guard is what makes FR-15.117 possible. (realizes US-15.1)
- **FR-15.119** — Both distribution profiles SHALL carry the large native machine-learning inference payloads, which are per-platform, not trimmable, and referenced as **two mutually exclusive backends simultaneously** (a processor backend and a version-12 graphics-vendor compute backend) on every platform build. The payloads land under a `<platform triple>/native/` folder before bundling. The backend set is therefore a packaging decision, not only a feature decision; documented troubleshooting explicitly proposes removing the graphics backend as a remedy.
- **FR-15.120** — The bundled artefact SHALL unpack itself to a per-user cache directory before it runs, because compression and both self-extraction switches are enabled together. See QUIRK-15.7. *(INFERRED from documented platform behaviour; not asserted anywhere in the source.)*
- **FR-15.121** — All persistent state — settings, prompts, history, provider payloads and inference records — SHALL be loaded and saved through reflection-driven structured serialization with **no statically-analysable serializer context at any call site**, inside artefacts built with dead-code removal on and all its diagnostics silenced. This is the single sharpest incompatibility in the feature. See QUIRK-15.28.
- **FR-15.122** — The inference layer SHALL resolve one platform method **by literal name at run time** and SHALL treat "not found" as an empty result, so removal by dead-code elimination produces silence rather than an error. See QUIRK-15.4.
- **FR-15.123** — The hosted-provider clients SHALL be produced by concrete factory objects constructed directly at the call site — not resolved from a container, not discovered by name, not located by scanning — so that this part of the object graph is visible to reachability analysis. It is the only part of the persistence and provider graph that is.
- **FR-15.124** — The inference logger's defaults in a packaged artefact SHALL be: file logging **on**, debug output **on**, console output **off**, in-memory buffer cap **10000** entries, log directory non-null. *(10000 is a tunable default, not a business rule.)*

*Size, time and machine budgets — documented targets, not enforced*

- **FR-15.125** — The following are the only quantified quality bars this feature has. **None of them is enforced by any check.** A reimplementation SHOULD decide whether to enforce them.

| Profile / artefact | Stated size | Stated cold start | Stated build time |
|---|---|---|---|
| `Compact` (native) | 8–15 MB | < 100 ms | 5–15 min |
| `SingleFile` (bundled) | 15–25 MB | 200–500 ms | 1–3 min |
| Ordinary `Release` | 50–100 MB | 500 ms+ | < 1 min |
| Released asset, either platform | ~13–25 MB | — | 3–8 min per platform |
| Released asset, per-platform refinement | Windows ~13–15 MB; Linux ~15–17 MB | — | — |

- **FR-15.126** — The documented machine floor for the native profile SHALL be 8 GB or more of RAM with solid-state storage recommended. It omits the native compiler and linker entirely. See QUIRK-15.9.
- **FR-15.127** — Ground truth for the size budget: the only measured artefact is one committed pre-built 64-bit Linux executable of **15,677,171 bytes (≈14.95 MiB)** with 65 bundled assemblies, which **predates the local-inference dependency** and therefore does not evidence the current size. The current size is unknown. (See Open Questions.)

*Repository hygiene*

- **FR-15.128** — A 15,677,171-byte pre-built binary at `test-publish/Xcaciv.ChatDbg.Shell` SHALL be tracked in version control, produced and consumed by nothing, and not covered by any ignore rule. Every clone downloads it, and its embedded manifest declares a runtime version the source no longer targets, making it actively misleading evidence. See QUIRK-15.29.

---

**External technology**

*Requires: a managed-language compiler and build toolchain, pinnable to an exact version with a controlled roll-forward policy (no wire protocol). Source used: the .NET SDK, pinned in `global.json` to `10.0.100-rc.1.25451.107` with `rollForward: latestFeature`; target framework `net10.0`; `LangVersion` `latest`. Reimplementer notes: the pinned build is a pre-release. The pin document must be validated in CI — in the source it is malformed and gates every build. The port needs an equivalent "exact toolchain version plus bounded roll-forward" mechanism, or it loses the guarantee that every machine builds identically.*

*Requires: a declarative project and build description system with directory-inherited defaults and named, condition-selectable configurations (no wire protocol). Source used: MSBuild project files (`.csproj`), `Directory.Build.props`, and a solution descriptor (`.sln`, format 12.00). Reimplementer notes: configuration-conditioned property blocks are how all four profiles are expressed. The workspace descriptor enumerates only two of the four configurations, so the two distribution profiles are invisible to an IDE and reachable only from a command line.*

*Requires: ahead-of-time native compilation of a managed program, with size-preference code generation, identical-body folding, stack-trace-metadata suppression and symbol stripping. Source used: .NET Native AOT (`PublishAot`, `IlcOptimizationPreference=Size`, `IlcFoldVTables`, `IlcGenerateStackTraceData=false`, `StripSymbols`). Reimplementer notes: needed for the `Compact` profile only, which is never actually released. It additionally requires a host C/C++ compiler and linker that nothing in the source installs — budget for a second toolchain on every machine and every agent that builds it.*

*Requires: whole-program dead-code elimination (tree shaking) with at least two selectable levels of aggressiveness. Source used: the .NET IL trimmer (`PublishTrimmed`, `TrimMode` = `full` for `Compact` and `partial` for `SingleFile`, with `SuppressTrimAnalysisWarnings`). Reimplementer notes: two granularities are required, not one — the shipping profile is deliberately the gentler one so that reflection-heavy dependencies survive. Suppressing the analysis warnings is what makes every trimming defect invisible until run time; a port should reconsider that.*

*Requires: a single-file self-extracting bundler with in-bundle compression and native-payload inclusion. Source used: .NET single-file publish (`PublishSingleFile`, `EnableCompressionInSingleFile`, `IncludeNativeLibrariesForSelfExtract`, `IncludeAllContentForSelfExtract`). Reimplementer notes: this is the profile that actually ships. Compression plus both self-extraction switches means the "single file" unpacks to a per-user cache before running — a writable temporary area becomes a hard runtime requirement, and first start pays a decompression cost.*

*Requires: self-contained runtime embedding so the target machine needs no pre-installed runtime, plus a native launcher stub. Source used: .NET self-contained deployment (`SelfContained`, `UseAppHost`). Reimplementer notes: this is the whole product promise; do not substitute anything that reintroduces a runtime prerequisite.*

*Requires: a culture-data-free ("invariant") runtime mode and symbolic framework message keys. Source used: `InvariantGlobalization`, `UseSystemResourceKeys`. Reimplementer notes: both materially change user-visible behaviour in shipped builds — string comparison and formatting semantics, and error message text. Carry them only with that trade-off understood.*

*Requires: platform-triple ("runtime identifier") targeting so one source tree produces per-platform binaries. Source used: .NET RIDs `win-x64` and `linux-x64`; documented but never built: `osx-x64`, `osx-arm64`. Reimplementer notes: the triple strings appear inside the public release asset names, so changing the triple vocabulary changes the download contract.*

*Requires: a dependency manager supporting exact-version pins declared per project. Source used: NuGet `PackageReference`. Pinned set: AWSSDK.BedrockRuntime 4.0.7.3; Azure.AI.OpenAI 2.1.0; Spectre.Console 0.51.1; Terminal.Gui 1.19.0 (graphical shell only); LLamaSharp 0.25.0; LLamaSharp.Backend.Cpu 0.25.0; LLamaSharp.Backend.Cuda12 0.25.0; Microsoft.NET.Test.Sdk 17.12.0; xunit 2.9.1; xunit.runner.visualstudio 2.8.1; Moq 4.20.69; coverlet.collector 6.0.2. Reimplementer notes: no central version file and no committed lock file exist, so three pins are duplicated between the shared library and the console executable and must be synchronised by hand. An internal convention document claims central package management is in use; it is not.*

*Requires: shipping large per-platform native machine-learning inference payloads inside the distributable. Source used: LLamaSharp backend packages (processor and CUDA-12 variants, both referenced simultaneously), extracted to `<platform triple>/native/` with per-platform library file names. Reimplementer notes: this is the dominant size and packaging constraint, and it is why the native profile cannot produce a single file. Decide explicitly whether to ship one backend, both, or make them optional side-loads.*

*Requires: reflection-driven structured serialization for every persisted document (JSON). Source used: the platform-provided reflection-mode JSON serializer, with no source-generated context anywhere. Reimplementer notes: this is the single biggest incompatibility with the chosen dead-code-removal strategy. Either pick a serialization approach that is statically analysable, or do not trim.*

*Requires: permission to compile raw pointer and unmanaged-memory access, scoped to one component. Source used: `AllowUnsafeBlocks` on the shared library only. Reimplementer notes: required by the local-inference integration. Decide early whether the target language permits this and whether it survives an ahead-of-time or trimmed build.*

*Requires: an operating-system credential vault reached through a native platform API, guarded at run time. Source used: the Windows credential API entry points `CredReadW`, `CredWriteW`, `CredDeleteW`, `CredFree` in `advapi32.dll`. Reimplementer notes: a port needs an equivalent per-platform secret store **plus** the same run-time guard, or the one-binary-two-platforms property is lost.*

*Requires: well-known per-user directory resolution (user profile, local application data, roaming application data). Source used: platform special-folder lookups. Reimplementer notes: three different roots are used for one product. A port must pick per-platform conventions deliberately; the source did not, and there is no uninstall story.*

*Requires: hosted continuous-integration with manual dispatch, typed inputs, matrix fan-out onto per-platform agents, artefact upload and download between jobs with retention control, and a fan-in job dependency. Source used: GitHub Actions on `windows-latest` and `ubuntu-latest` agents, with `actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/upload-artifact@v4`, `actions/download-artifact@v4`. Reimplementer notes: both agent images are floating "latest" tags, so nothing about the build environment is reproducible. There is no dependency or build cache.*

*Requires: a release publication service supporting tagging, a notes body, a pre-release flag, a draft flag and binary asset attachment. Source used: GitHub Releases via `softprops/action-gh-release@v1` (a floating major tag). Reimplementer notes: the source publishes non-draft, so there is no review gate and no rollback. Pin the publishing component by revision, not a floating tag.*

*Requires: a credential authorising release publication. Source used: a repository secret named `GH_PATT` holding a long-lived personal access token, supplied to the publication step; the pipeline declares no permissions block. Reimplementer notes: prefer a short-lived, scoped, ambient job credential. The source's own documentation claims no external secret is required, which is false.*

*Requires: a POSIX-style shell available on **both** agent operating systems for scripting steps. Source used: `shell: bash` steps, including on the Windows agent, using `grep` with Perl-compatible regular expressions, `mv`, `ls`, `stat`, `wc` and heredocs. Reimplementer notes: the rename and version-scrape steps assume this; a port that cannot guarantee it must rewrite those steps per platform.*

*Requires: local developer build scripting on the maintainer's workstation platform. Source used: three Windows command-interpreter scripts (`.bat`) and one PowerShell script (`.ps1`). Reimplementer notes: all four are Windows-only and all four block on a keypress, so there is no non-interactive path and no POSIX developer script at all, despite a POSIX binary being released.*

*Requires: code-coverage collection integrated with the test runner. Source used: `coverlet.collector` 6.0.2. Reimplementer notes: present but never invoked by any script or pipeline step; there is no coverage gate.*

*Requires: a copyleft source licence governing binary redistribution. Source used: GNU General Public License version 3 (`LICENSE`). Reimplementer notes: the published release attaches only executables — no licence text, no notice, no source offer. A port must decide and document how the equivalent obligations are met.*

---

**Acceptance criteria**

- **AC-15.1** — **Given** a clean checkout, **when** the toolchain pin document is parsed with a strict structured-text parser, **then** it parses successfully. *(At the analysed commit it does not — the file is 96 bytes and its last 6 bytes are `  }\n}}`, one closing brace too many. This is a failing criterion the clone must fix.)*
- **AC-15.2** — **Given** a clean checkout, **when** the workspace configuration list is inspected, **then** only `Debug` and `Release` appear, and building `Compact` or `SingleFile` requires naming an individual executable project and configuration explicitly.
- **AC-15.3** — **Given** the pinned toolchain installed, **when** the console-shell project is published in the `SingleFile` configuration for platform triple `win-x64`, self-contained, **then** the publish folder contains exactly one executable named `Xcaciv.ChatDbg.Shell.exe` and no separate runtime, library or symbol files.
- **AC-15.4** — **Given** that publish, **when** the produced executable's file metadata is read, **then** it reports product version `1.0.0`, file version `1.0.0.0`, company `Xcaciv`, product `ChatDbg`, and copyright `Copyright © Xcaciv 2024`.
- **AC-15.5** — **Given** a publish in the `Compact` configuration, **when** the produced executable's file metadata is read, **then** that identity metadata is **absent**.
- **AC-15.6** — **Given** a `Compact` build, **when** the running program raises an unhandled framework exception, **then** the message shown is a short symbolic key rather than a sentence, and no stack frames are available.
- **AC-15.7** — **Given** a build in either distribution profile, **when** the running program performs a culture-sensitive string comparison on a machine whose locale is `tr-TR`, **then** it behaves as if the invariant culture were active.
- **AC-15.8** — **Given** a successful local build, **when** the invoking script prints its post-build listing, **then** it names the actual produced file and its size. *(At the analysed commit it reports nothing, because the listing path is hard-coded to `src\ChatDbg\bin\Compact\net9.0\win-x64\publish\` while output lands under the folder named for the *current* framework version.)*
- **AC-15.9** — **Given** the interactive build chooser, **when** the operator enters `9`, **then** the script prints exactly `Invalid choice. Building Compact by default.` and proceeds with the native build.
- **AC-15.10** — **Given** the interactive build chooser, **when** the operator presses Enter without typing anything, **then** the observable result is identical to AC-15.9.
- **AC-15.11** — **Given** the interactive build chooser, **when** the operator selects `3`, **then** both builds complete. *(At the analysed commit the second build fails because the listing subroutine overwrites the executable-search-path variable; a correct clone must not exhibit this.)*
- **AC-15.12** — **Given** any local build script, **when** it is invoked from a non-interactive context, **then** it returns rather than blocking. *(At the analysed commit all four block forever on a keypress.)*
- **AC-15.13** — **Given** the single-file build script and a publish that returns exit code `1`, **when** the script finishes, **then** standard output contains `Build failed with error code 1`, then `Troubleshooting:`, then the three numbered items verbatim, and the script's own exit code is `1`.
- **AC-15.14** — **Given** the interactive build script and a publish that returns exit code `1`, **when** the script finishes, **then** it printed `Compact build failed with error code 1`, then printed `  No executable files found`, and its own exit code did **not** reflect the failure.
- **AC-15.15** — **Given** the release pipeline dispatched with `version` = `v1.2.3` and `prerelease` unchecked, **when** both platform rows succeed, **then** a public, non-draft release exists tagged `v1.2.3`, titled `ChatDbg v1.2.3`, carrying exactly two assets named `chatdbg-windows-win-x64.exe` and `chatdbg-linux-linux-x64`.
- **AC-15.16** — **Given** that same dispatch, **when** the release body is read, **then** it matches the FR-15.87 template exactly, with `v1.2.3` substituted and nothing else changed — including the Downloads section naming both asset file names verbatim, the System Requirements section stating that no runtime installation is required and naming Windows 10/11 and modern Linux, and the Usage section's three numbered steps including `chmod +x chatdbg-linux-linux-x64`.
- **AC-15.17** — **Given** a dispatch with `prerelease` checked, **when** the release is created, **then** it is flagged as a pre-release; **and given** a dispatch with it unchecked, **then** it is not.
- **AC-15.18** — **Given** a dispatch in which the `linux` row fails, **when** the run completes, **then** no release exists, and the `windows` row was cancelled rather than allowed to finish.
- **AC-15.19** — **Given** a completed dispatch, **when** the intermediate artefacts are listed, **then** each is named `chatdbg-windows-binary` or `chatdbg-linux-binary`, holds exactly one file, and expires exactly 7 days after the run.
- **AC-15.20** — **Given** a completed dispatch, **when** the run summary is read, **then** it contains the release version, the scraped project version, the raw pre-release input value, both asset names, and a link of the form `https://github.com/<owner>/<repo>/releases/tag/v1.2.3`.
- **AC-15.21** — **Given** the pipeline as analysed, **when** it is dispatched, **then** it fails during toolchain setup or restore, because it installs the `9.0.x` line while the projects target runtime major version 10 and the pin demands `10.0.100-rc.1.25451.107`. A correct clone must install a toolchain satisfying both.
- **AC-15.22** — **Given** a dispatch with `version` set to `v1.0.0"; echo pwned; #`, **when** the release job runs, **then** the injected text is **not** executed on the agent. *(At the analysed commit it is; a correct clone must pass the input through an environment variable rather than into command text.)*
- **AC-15.23** — **Given** a project descriptor containing **two** version elements, **when** the version scrape runs, **then** the first match is taken. *(At the analysed commit every match is emitted, producing a multi-line step output that the step-output format rejects and failing the whole release.)*
- **AC-15.24** — **Given** the pipeline as analysed and a dispatch that reaches the release job, **when** the release is created, **then** it is created without any approval, review or draft state.
- **AC-15.25** — **Given** a dispatch that reuses an existing tag such as `v1.2.3`, **when** the release job reaches publication, **then** it fails with `Cannot create releases with existing tag names.` and no release is created.
- **AC-15.26** — **Given** a downloaded POSIX release asset on a machine with no managed runtime installed, **when** the user sets the execute permission bit and runs it, **then** the program starts; on clean exit it returns exit code `0`; on an unhandled failure it prints a single line beginning `Fatal error: ` and returns exit code `1`.
- **AC-15.27** — **Given** a working bundled artefact, **when** the environment variable `CHATDBG_AZURE_API_KEY` is set to `from-env` while a stored key `from-json` also exists, **then** the running program uses `from-env` and its reported credential source contains the text `environment variable`.
- **AC-15.28** — **Given** a working bundled artefact run for the first time on a machine with no prior state, **when** the program starts and then exits, **then** exactly three per-user locations exist — `<user profile>/.ChatDbg/settings.json`, `<local application data>/ChatDbg/system_prompts/` seeded with the default prompts, and (only if inference logging ran) `<application data>/ChatDbg/Logs` — **and** no file has been written into the directory the executable was copied to.
- **AC-15.29** — **Given** a bundled artefact, **when** a token-analysis record with a nested candidate list and a nested model-state object is written and read back, **then** the record survives with its floating-point probability equal to 3 decimal places. This round trip must be executed **against the packaged artefact**, not only against a development build.
- **AC-15.30** — **Given** a machine with no writable temporary directory, **when** the bundled artefact is launched, **then** it starts. *(At the analysed commit it does not, because compression plus both self-extraction switches force an unpack first. A correct clone must either not require extraction or must document the requirement.)*
- **AC-15.31** — **Given** a bundled artefact built from the analysed commit, **when** it is launched, **then** the host reports a runtime-configuration failure before any application code runs, because the runtime-configuration side-car is suppressed in the shipping profile. A correct clone must emit that side-car inside the bundle and start successfully. *(INFERRED at the analysed commit.)*
- **AC-15.32** — **Given** a machine with no native C/C++ compiler or linker installed, **when** the console-shell project is published in the `Compact` configuration for `win-x64`, **then** the publish fails during native compilation. A correct clone must either install that toolchain as a documented prerequisite or must not offer an ahead-of-time profile.
- **AC-15.33** — **Given** a completed `Compact` publish for `linux-x64`, **when** the publish folder is listed, **then** it contains the executable **plus** separate native inference payloads under `linux-x64/native/` — i.e. it is a folder, not the "single native executable" the documentation and the menu banner promise.
- **AC-15.34** — **Given** the analysed commit, **when** the whole test suite is run, **then** 100 test cases across 38 files execute and none of them touches a build configuration, a publish output path, a framework identifier or the release pipeline. A correct clone must add at least one test that asserts a property of the *packaged* artefact.
- **AC-15.35** — **Given** a published release, **when** its assets are enumerated, **then** exactly two files are present, both executables — no licence copy, no notice file, no checksum, no signature, no source offer — although the repository ships GNU GPL v3.
- **AC-15.36** — **Given** a fresh clone on a POSIX workstation, **when** the developer looks for a local build script, **then** at the analysed commit none exists. A correct clone must provide a scripted build path on every platform it releases for.
- **AC-15.37** — **Given** the graphical shell, **when** its about box is opened, **then** at the analysed commit it shows the literal `ChatDbg v1.0` regardless of the declared `1.0.0`. A correct clone must derive the displayed version from build metadata.
- **AC-15.38** — **Given** the release pipeline, **when** every step is enumerated, **then** no step builds the graphical shell, no step packages the shared library, and no step runs any test.
- **AC-15.39** — **Given** a build of the two distribution profiles, **when** the shared library's compiled output is inspected for optimisation, **then** it is optimised. *(At the analysed commit it is INFERRED to be unoptimised, because the library declares no configuration-conditioned settings and the profile names are not recognised as optimised by default.)*
- **AC-15.40** — **Given** a completed dispatch, **when** the release notes, release body and run summary are searched for the measured artefact size, **then** it appears nowhere — the size is computed, echoed and stored into a step output that nothing reads.

---

**Quirks**

- *QUIRK-15.1: The toolchain pin document is not valid structured text — it carries one closing brace too many after the top-level object (file length 96 bytes; last 6 bytes `  }\n}}`). Any strict parser rejects it, so the build tool refuses to start with a parse error rather than falling back to a default, gating every build both local and automated. Evidence: `global.json:6`. INFERRED consequence — not executed. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.2: The release pipeline installs the wrong major toolchain line — it requests `9.0.x` while the pin demands `10.0.100-rc.1.25451.107` and all four projects target `net10.0`. Both platform rows would fail before compilation, fail-fast cancels, the release job never runs, and nothing is published. Combined with QUIRK-15.1 the pipeline at this commit cannot produce a release. Evidence: `.github/workflows/build-release.yml:36-39` vs `global.json:3` and `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:5`. INFERRED — the pipeline was not run. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.3: The shipping bundled profile suppresses the runtime-configuration side-car that a bundled, just-in-time artefact needs in order to start. The identical setting was copied verbatim from the native profile, where it is harmless. The one committed pre-built artefact contains an uncompressed dependency manifest but no runtime-options block, corroborating this. Evidence: `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:79` (vs `:39` for the native profile); `test-publish/Xcaciv.ChatDbg.Shell`. INFERRED — the binary was deliberately not executed. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.4: The inference layer resolves a platform method by literal name at run time and treats "not found" as an empty result, so whole-framework dead-code elimination can remove the target and the packaged build reports no error at all — the feature simply returns nothing. Evidence: `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:423` (literal `"GetLogits"`), null-check at `:424-427`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.5: The operator-supplied version string is pasted into shell command text before the shell sees it, inside a conditional test, an assignment, and the generated notes document. A dispatch value containing shell metacharacters executes as commands on the agent — in the same job that holds the long-lived publication credential. Evidence: `.github/workflows/build-release.yml:136-137,148` and `:181`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.6: The version scrape emits every match rather than the first. Harmless while exactly one version element exists; a second produces a multi-line step output the step-output file format rejects, failing the step and taking the whole release with it. Evidence: `.github/workflows/build-release.yml:73,133`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.7: The shipped "single file" unpacks itself to a per-user cache directory before it runs, because in-bundle compression and both self-extraction switches are enabled together. This contradicts the product's own promise "copy it to any machine and run it, no installation" on any machine with no writable temporary area, and it means first start pays a decompression cost the documented 200–500 ms budget does not obviously account for. Evidence: `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:86,87,88`; claim at `build-singlefile.bat:30-31` and `.github/workflows/build-release.yml:160`. INFERRED from documented platform behaviour. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.8: The native profile does not produce a single file and cannot while the native inference payloads are referenced — it sets no bundling switch at all, so the two backends land beside the executable as separate per-platform payloads. Its output is a folder, directly contradicting both the documentation and the interactive script's own menu line `1. Compact (AOT) - Smallest binary with native compilation (single .exe)`. Evidence: no bundling switch anywhere in `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:23-60`; payload location at `docs/LLamaSharp-Troubleshooting-0xC0000005.md:26-30`; both backends at `src/Xcaciv.ChatDbg.Core/Xcaciv.ChatDbg.Core.csproj:13-15`; claim at `build-compact-robust.bat:4` and `docs/compact-build.md:14,22`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.9: Nothing installs the native compiler and linker that ahead-of-time compilation requires. No script, no pipeline step and no prerequisite list mentions one; the stated prerequisites are a managed toolchain, 8 GB of RAM and solid-state storage. The native profile therefore cannot complete on a stock developer machine or a stock agent, and because nothing in the pipeline ever exercises that profile the gap is invisible. Evidence: `docs/compact-build.md:168-173`; absent across `build-compact.bat`, `build-compact.ps1`, `build-compact-robust.bat`, `.github/workflows/build-release.yml`. INFERRED. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.10: The pipeline restores without naming a configuration and then publishes a different configuration with re-restore disabled — structurally identical to the local-script failure the documentation already describes. It has not caused a failure only because the publish output directory is overridden. Evidence: restore at `.github/workflows/build-release.yml:42` vs publish at `:48,51` and `:60,63`; documented symptom at `docs/compact-build.md:132-140`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.11: Every local build script hard-codes a post-build listing path containing the previous framework version, while a successful build writes under the current one. A successful build therefore looks like a build that produced nothing: two scripts print the shell's own "file not found" text, one prints `  No executable files found`, and the mebibyte-reporting script prints nothing at all. Evidence: `build-compact.bat:20,22`; `build-compact.ps1:23,27`; `build-singlefile.bat:21,25`; `build-compact-robust.bat:57,58,80,81` vs `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:5`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.12: The interactive script's file-listing subroutine destroys the process executable search path by assigning its first argument to a variable whose name collides with it. Menu option `3` ("Both") therefore builds the native profile, prints its listing, and then cannot launch the build tool for the second build. Options `1` and `2` are unaffected in practice because only shell-internal commands follow. Evidence: `build-compact-robust.bat:88` (`set "path=%~1"`), second build invoked at `:75`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.13: Every local build script blocks on a keypress before exiting, so any headless or scripted invocation hangs forever rather than returning. There is no non-interactive path. Evidence: `build-compact.bat:29`, `build-compact.ps1:41-42`, `build-singlefile.bat:43`, `build-compact-robust.bat:113`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.14: Failure handling is inconsistent across the four scripts — three propagate the build tool's exit code, the interactive one prints a message and continues, so its overall exit code never reflects a build failure. Evidence: `build-compact.bat:23-27`, `build-singlefile.bat:32-40`, `build-compact.ps1:34-38` vs `build-compact-robust.bat:59-61,82-84`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.15: The simple compact script restores before cleaning, so the clean can invalidate what restore just produced. This is the documented cause of the "no target for the requested framework/platform pair" failure, whose documented remedy is the interactive script or a manual hard-delete. Evidence: `build-compact.bat:9-15` vs `build-compact-robust.bat:43-52`; `docs/compact-build.md:132-140`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.16: Stale runtime-version claims are printed to users in two places: the single-file script's failure checklist tells the operator to install a version older than the one the projects require, and the generated release notes end with a provenance line naming that same older version. Evidence: `build-singlefile.bat:37`; `.github/workflows/build-release.yml:168` vs `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:5`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.17: The version fact is declared in three independent places that no build step reconciles — the two executable project descriptors and a hard-coded literal in the graphical shell's about box. The about box already disagrees in shape (`v1.0` against `1.0.0`) and will drift silently on the first version bump. Evidence: `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:10-12`, `src/ChatDbg.Shell.Gui/Xcaciv.ChatDbg.Shell.Gui.csproj:10-12`, `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:1131`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.18: A packaged artefact scatters its state across three different per-user roots under two naming conventions — settings in a dot-prefixed folder under the user profile, prompts under local application data, logs under roaming application data. There is no uninstall story and none of the three locations is documented. Evidence: `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:15-18`, `src/Xcaciv.ChatDbg.Core/Services/SystemPromptService.cs:14-23`, `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs:20`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.19: The settings store silently falls back to the temporary directory inside a bare catch-all when the user-profile lookup throws or resolves empty. On a locked-down or service account, settings appear to save and then vanish, with nothing reported. Evidence: `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:20-31`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.20: The interactive script prints the same byte count twice on one line, formatted as though the parenthesised value were an approximation. Evidence: `build-compact-robust.bat:94`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.21: Size units are inconsistent across the four scripts — one reports mebibytes to two decimal places, the other three report raw bytes — so two developers comparing outputs from two scripts compare different units. Evidence: `build-compact.ps1:31` vs `build-singlefile.bat:26`, `build-compact-robust.bat:94`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.22: Job display names interpolate the platform matrix key verbatim, so they render lower-cased as `Build windows Binary` and `Build linux Binary`. Evidence: `.github/workflows/build-release.yml:18`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.23: The measured artefact size is computed, echoed and written to a step output that nothing ever reads. No size appears in the release notes, the release body or the run summary, so the only quantified quality bar this feature has is never checked against reality. Evidence: `.github/workflows/build-release.yml:94-101` with no consumer anywhere in the file. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.24: A third-party library name is leaked into user-facing product copy — the generated release notes advertise the console-rendering library to end users as though it were a feature. Evidence: `.github/workflows/build-release.yml:153`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.25: The `Compact` profile strips the very identity metadata the `SingleFile` profile is careful to keep, in otherwise byte-identical configuration blocks, so a native artefact carries no version, title, company, product or copyright and a support engineer cannot tell one build from another. Only the bundled profile ships, so this is latent rather than active. Evidence: `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:48-49` vs `:91-92`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.26: The release job downloads into a directory name the repository's own ignore rules exclude. Harmless on an ephemeral agent; a local reproduction of the release job silently hides its own downloads from every repository-aware tool. Evidence: `.github/workflows/build-release.yml:122` vs `.gitignore:29`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.27: The released binary is not the interface the product documentation describes. The user manual presents the full-screen terminal interface as the product; the pipeline ships only the plain console shell, and the graphical shell — which carries complete distribution profiles — is never built or published by anything. A user who reads the documentation and downloads the release gets a different program. Evidence: `README.md:21-38` vs `.github/workflows/build-release.yml:42,47,59`; graphical profiles at `src/ChatDbg.Shell.Gui/Xcaciv.ChatDbg.Shell.Gui.csproj:23-97`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.28: All persistent state is loaded and saved through reflection-driven structured serialization, with no statically-analysable serializer context at any call site, inside artefacts built with dead-code removal on and every analysis diagnostic silenced. No trim-safety annotation, root list or linker descriptor exists anywhere in the source, and the project's own documentation prescribes exactly the remedy the code does not use. Settings, prompts and history can silently lose properties or fail to deserialize in a packaged build, surfacing only as `Fatal error: <message>` with exit code 1. Evidence: call sites `SettingsService.cs:35,56,97`, `SystemPromptService.cs:57,88,106`, `ChatHistoryService.cs:35,57`, `BedrockService.cs:107,115,126`, `AzureOpenAIService.cs:165`, `LLamaSharpService.cs:637`; trimming at `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:26,66`; diagnostics silenced at `:35,75`; prescribed remedy at `docs/compact-build.md:145`. INFERRED consequence — the setup is directly observed. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.29: A 15,677,171-byte pre-built binary is tracked in version control, produced and consumed by nothing and not covered by any ignore rule, so every clone downloads it. Its embedded manifest declares a runtime version the source no longer targets, making it actively misleading as size evidence. Evidence: `test-publish/Xcaciv.ChatDbg.Shell` (committed 2025-09-29) vs `.gitignore:10-24`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.30: The shared library — which holds all of the product's domain logic — is compiled unoptimised inside both distribution profiles, because the optimise flag is set only in the executable projects and neither distribution profile name is one the build tool recognises as optimised by default. All the domain logic ships unoptimised inside an otherwise size-optimised artefact. Evidence: `src/Xcaciv.ChatDbg.Core/Xcaciv.ChatDbg.Core.csproj:3-8` (no configuration-conditioned group) vs `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj:45,85`. INFERRED — no build was run. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.31: The release pipeline runs no tests, and neither does any build script, although a 100-case suite exists — so there is no quality gate of any kind between a dispatch and a public, non-draft release. Evidence: `.github/workflows/build-release.yml:32-109`; no script mentions `src/Xcaciv.ChatDbg.Core.Tests/`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.32: The release attaches only executables although the repository ships a strong copyleft licence — no licence copy, notice file, source offer, checksum or signature is produced or attached. Evidence: `LICENSE:1-2` (GNU GPL v3); `.github/workflows/build-release.yml:178-180`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.33: Publication authenticates with a repository secret holding a long-lived personal access token rather than the ambient job credential, and the pipeline declares no permissions block at all — while the project's own documentation claims "No external secrets or API keys required" and "minimal required permissions". Evidence: `.github/workflows/build-release.yml:181` and the absence of any permissions key, vs `docs/github-actions-release.md:130,140-142`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.34: The repository's internal convention document describes a different product entirely — a serverless-functions application with a storage emulator and packages that exist nowhere in this repository — and additionally claims central package management is in use (no such file exists) and forbids command-line builds (every script and every pipeline step is a command line). The file that is supposed to encode this repository's build conventions was copied from another repository and never reconciled; a reimplementer must treat it as non-evidence. Evidence: `.github/copilot-instructions.md:230-240,238,240,246-248,253-259`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.35: Documentation files are encoding-corrupted — every emoji in the setup-completion document was committed as `?` or `??`, including in headings and intended checkmarks. Cosmetic, but it is the visible symptom of a code-page mismatch in the authoring and commit path that could equally affect a resource or template file. Evidence: `docs/release-setup-complete.md:3,35,55,71,91,120,128,142,145-149`; same corruption at `IMPLEMENTATION_SUMMARY.md:12,58,59,68,75-77`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.36: The product's user manual contains no build, install, prerequisite, download or getting-started section at all — its only install-adjacent mention is an aside about graphics-vendor libraries — so a downloader has no documented path from release page to running program except the three-line Usage block inside the generated release notes. Evidence: `README.md` headings at `:1,5,21,39,93,204,270`, sole install-adjacent hit at `README.md:159`. Keep-or-fix decision deferred to Open Questions.*
- *QUIRK-15.37: All three packaging documents describe a previous runtime generation and name platform triples (`osx-x64`, `osx-arm64`) that no build ever produces, while the one packaging-adjacent document written later warns that the native inference payloads may not be compatible with the current runtime generation at all. Evidence: `docs/compact-build.md:97-98,112-116,170`, `docs/github-actions-release.md:57,161-164`, `docs/release-setup-complete.md:107-112` vs `src/*/*.csproj:4-5`, `global.json:3`, `.github/workflows/build-release.yml:22-30`, and `docs/LLamaSharp-Troubleshooting-0xC0000005.md:18,26-30`. Keep-or-fix decision deferred to Open Questions.*

*Recorded as deliberate, not quirks, so they are not re-flagged: the bundled profile trims only partially while the native profile trims fully (a deliberate concession to reflection-heavy dependencies, and the reason the shipping profile is the gentler one); bundling and compression are re-asserted on the pipeline command line although the named configuration already sets them (redundant, not wrong); the graphical project excludes source folders that no longer exist and then re-includes by name the only two files in the one that does (inert); and the test project's runner-integration package is asset-filtered and marked private (standard test-adapter isolation).*

---

**Source notes**

Primary dossier: `/mnt/g/3RD-Party/reversing/output/chatdbg/dossiers/packaging-build-release.md`. Feature boundaries from `/mnt/g/3RD-Party/reversing/output/chatdbg/inventory.md` (feature 15, "Packaging, Build & Release Distribution"). Repository analysed at pinned commit `d8c18f61d6bb73666ed97cd4885e877e35558485` (dated 2025-10-14) under `/mnt/g/3RD-Party/reversing/subject/chatdbg/`; all paths below are relative to that root.

Build profiles and project metadata:
- `src/ChatDbg/Xcaciv.ChatDbg.Shell.csproj` — target framework `:5`; implicit imports `:6`; null-state checking `:7`; version metadata `:10-12`; identity strings `:15-19`; `Compact` profile `:23-60`; `SingleFile` profile `:63-97`; source exclusions `:98-105`; dependency pins `:107-111`
- `src/ChatDbg.Shell.Gui/Xcaciv.ChatDbg.Shell.Gui.csproj` — byte-identical profile blocks `:23-97`; exclusions and re-inclusions `:98-115`; dependency pins `:117-122`
- `src/Xcaciv.ChatDbg.Core/Xcaciv.ChatDbg.Core.csproj` — `:3-8` (no configuration-conditioned group; unmanaged-memory permission at `:7`); dependency pins `:10-17`, inference backends `:13-15`
- `src/Xcaciv.ChatDbg.Core.Tests/Xcaciv.ChatDbg.Core.Tests.csproj` — `:3-9`, non-packable and test-project flags `:7-8`, pins `:11-20`, adapter isolation `:14-17`
- `Directory.Build.props:1-5`; `global.json:2-6`; `Xcaciv.ChatDbg.sln:2-4,24-45`; `.gitignore:10-29`; `LICENSE:1-2`

Local build scripts: `build-compact.bat`, `build-compact.ps1`, `build-singlefile.bat`, `build-compact-robust.bat` (menu `:2-8`, default branch `:12-16`, native build `:42-61`, bundled build `:66-84`, listing subroutine `:87-98`, closing block `:107-113`).

Release pipeline: `.github/workflows/build-release.yml` — triggers and inputs `:3-14`; job name `:18`; matrix `:20-30`; toolchain install `:36-39`; restore `:42`; publish `:44-66`; name derivation `:68-82`; rename and size `:84-102`; upload `:104-109`; join `:113`; checkout and download `:116-122`; version resolution `:129-143`; notes template `:145-169`; publication `:171-182`; run summary `:184-196`.

Runtime contracts a packaged artefact must honour: `src/ChatDbg/Program.cs:8-14`; `src/ChatDbg.Shell.Gui/Program.cs:97-103`; `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs:11,15-31,35,47-53,56,97`; `src/Xcaciv.ChatDbg.Core/Services/SystemPromptService.cs:11-29,57,88,106`; `src/Xcaciv.ChatDbg.Core/Services/ChatHistoryService.cs:35,57`; `src/Xcaciv.ChatDbg.Core/Services/TokenInspection/LLamaSharpLogConfig.cs:20`; `src/Xcaciv.ChatDbg.Core/Models/ChatSettings.cs:88-98,174-181`; `src/Xcaciv.ChatDbg.Core/Models/WindowsCredentialManager.cs:11-20,59,103,150,171`; `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs:423-427,637`; `src/Xcaciv.ChatDbg.Core/Services/BedrockService.cs:27,107,115,126`; `src/Xcaciv.ChatDbg.Core/Services/AzureOpenAIService.cs:165`; `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs:1131`.

Tests that pin packaging-relevant invariants (none of which is itself a packaging test): `src/Xcaciv.ChatDbg.Core.Tests/Models/WindowsCredentialManagerTests.cs:10-26`; `.../Services/SettingsServiceTests.cs:12-58`; `.../Services/SystemPromptServiceTests.cs:12-31`; `.../Services/TokenInspection/TokenAnalysisTests.cs:158-170`; `.../Services/TokenInspection/LLamaSharpLogConfigTests.cs:10-22`; `.../Models/ChatSettingsTests.cs:11,28-39,55-71`; `.../Services/DefaultFactoriesTests.cs:10-34`.

Documentation consulted, all of it treated as claims requiring code confirmation: `docs/compact-build.md` (profiles, budgets, prerequisites, troubleshooting), `docs/github-actions-release.md`, `docs/release-setup-complete.md`, `docs/LLamaSharp-Troubleshooting-0xC0000005.md`, `README.md`, `IMPLEMENTATION_SUMMARY.md`, `.github/copilot-instructions.md`. Measured ground truth: the committed pre-built artefact `test-publish/Xcaciv.ChatDbg.Shell` (15,677,171 bytes), inspected but deliberately not executed.
