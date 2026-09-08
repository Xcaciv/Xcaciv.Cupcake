### 7.3 Credential Management & Secret Storage

**Description**

The product is an interactive terminal chat and debugging assistant that talks to two remote AI providers and one local model runtime. The two remote providers need long-lived bearer secrets: a single API key for the hosted OpenAI-compatible service, and an access-key / secret-key pair for the cloud model-inference service. An earlier generation of the product kept those secrets in plaintext inside the user's settings file, which exposed them to anything that could read the file — other local processes, malware, backup copies, and accidental commits into version control. Credential Management is the subsystem that ends that practice while remaining able to read files written by the old one.

It does five things. It **supplies** each secret through more than one channel so a user is never forced to write a secret into a file. It **resolves** a secret deterministically when several channels hold a value, using a fixed three-tier priority that the user cannot reorder: process environment variables first, then an operating-system-managed encrypted credential vault (only when the user has explicitly opted in), then the deprecated plaintext field in the settings file. It **stores** a secret into the OS vault on request. It **migrates** legacy users off plaintext storage through an interactive wizard, and warns them on every settings load until they finish. And it **reports** — for each of the three logical credentials — whether a value is present and which channel supplied it, without ever printing the value in the normal status display.

One product boundary must be stated up front because it is deliberate and asserted by tests, not incidental: the encrypted-vault tier exists on Microsoft Windows only. The availability probe is a bare "is the running operating system Windows" test. On every other host the vault reads return "no value", vault writes and deletes return "failed", the enable command is refused, the store command always fails, and one branch of the migration wizard aborts. The environment-variable tier and the plaintext-settings-file tier are platform-neutral, so on macOS and Linux the only two channels available are environment variables and a plaintext file. Priority ordering, status display, masking, source labels, the plaintext warning, and the wizard shell are all platform-neutral; only the wording of menus, help and remediation text changes with the probe. Whether a clone generalizes the vault tier to other platforms' keychains is an open product decision recorded in Open Questions.

**User stories**

- **US-3.1** — As an interactive operator, I want to supply each provider secret through a process environment variable, so that I never have to write a secret into a file on disk.
- **US-3.2** — As an interactive operator, I want a single, fixed, documented priority order across all credential channels, so that I can predict exactly which value the product will use when more than one channel holds one.
- **US-3.3** — As an operator on a platform with an OS-managed encrypted credential store, I want to keep my secrets in that store instead of in a file, so that they are encrypted at rest and scoped to my operating-system account.
- **US-3.4** — As an operator, I want the product to ask for my explicit consent before it starts using the OS credential store, so that no integration with an OS security service is turned on behind my back.
- **US-3.5** — As an operator, I want to place a named credential into the OS credential store from the command line, so that I can configure the product without leaving the session.
- **US-3.6** — As an operator upgrading from an older version, I want to be told loudly, on every startup, that plaintext secrets are still sitting in my settings file, so that I cannot forget to deal with them.
- **US-3.7** — As an operator upgrading from an older version, I want a guided wizard that shows me how to move each secret to a safer channel and then offers to erase the plaintext copies, so that migration is a single guided task rather than manual file editing.
- **US-3.8** — As an operator, I want a status view that tells me whether each credential is set and which channel it came from, without printing the secret, so that I can diagnose configuration over a shared screen or a pasted transcript.
- **US-3.9** — As an operator, I want the product to tell me at startup whether the selected provider actually has the credentials it needs, and exactly which commands or variables would fix it if not, so that I do not discover the problem on my first chat turn.
- **US-3.10** — As an operator, I want the product to refuse to accept a secret through the ordinary settings-change command, so that I cannot accidentally re-create the plaintext-in-a-file problem it was built to remove.
- **US-3.11** — As an operator using the full-screen graphical shell, I want a credentials panel with a masked entry field, an enable toggle, and a migration launcher, so that I can manage secrets without typing them in the clear.
- **US-3.12** — As provider-integration logic inside the product, I want to read each secret as an already-resolved plain string that is empty when unavailable, so that I never need to know which channel supplied it.

**Use cases**

---

**UC-3.1 — Resolve a credential value (realizes US-3.1, US-3.2, US-3.12)**

*Preconditions:* A settings record is loaded in memory. Zero or more of the five recognized environment variables are set in the process environment. The vault-enabled flag on the in-memory settings record is either true or false.

*Main flow:*
1. A consumer reads one of the three resolved credential values.
2. The system walks that credential's environment-variable name list in declared order.
3. For each name, it reads the process environment. The first name whose value is non-null and non-empty wins.
4. The system returns that value verbatim — no trimming, no unquoting, no case change.

*Alternate flows:*
- **A1 — No environment variable supplies a value, vault enabled.** If the vault-enabled flag is true, the system reads the vault entry whose fixed name corresponds to this credential. If the read returns a non-empty string, that string is returned.
- **A2 — Vault disabled.** If the vault-enabled flag is false, the vault tier is skipped entirely, even on a platform that has a vault and even when an entry with the right name exists.
- **A3 — Vault returns nothing or an empty string.** Resolution falls through to the deprecated settings-file field for that credential, which is returned as-is, including when it is the empty string.
- **A4 — Unknown credential identity.** If the internal credential key is not one of the three known keys, resolution returns "no value" (null). The three exposed properties never expose this; they coalesce it to the empty string.

*Error flows:*
- **E1 — Vault unavailable on this platform.** The vault adapter returns "no value" silently. No message, no log, no exception. Resolution falls through to the settings-file tier.
- **E2 — Any fault inside the vault operation** (policy denial, corrupted store, exhausted handles). Swallowed at two layers; indistinguishable from "the entry is simply not there". Resolution falls through. Nothing is printed and nothing is recorded.
- **E3 — Vault entry exists but holds a zero-length blob.** Treated as "not found", not as an empty string; resolution falls through.
- **E4 — Vault entry exists but was written by another tool in a different text encoding.** The bytes are decoded as UTF-16 little-endian regardless and the mis-decoded string is returned as if valid. There is no validation and no rejection.

*Postconditions:* A string is returned to the consumer (never null for a known credential). Nothing is cached; the next read repeats the whole procedure. No process-global state is mutated.

---

**UC-3.2 — Report credential status without disclosure (realizes US-3.8)**

*Preconditions:* The operator is at an interactive prompt in a running session.

*Main flow:*
1. The operator issues the settings command with no arguments.
2. The system prints the general settings block, which always names the active provider.
3. Within that block it prints a credentials section listing all three logical credentials.
4. For each credential it runs the resolution procedure (UC-3.1) and prints the literal `(not set)` when the resolved value is null or empty, and the literal `***set***` otherwise.
5. For each credential it independently runs a second resolution pass to determine the source label, and prints that label in square brackets after the status token.
6. It prints a line reporting whether the OS credential vault integration is `Enabled` or `Disabled`, taken from the vault-enabled flag.

*Alternate flows:*
- **A1 — Source is an environment variable.** The label names the specific variable that won, for example `environment variable (AWS_ACCESS_KEY_ID)`.
- **A2 — Source is the vault.** The label is exactly `Windows Credential Manager`.
- **A3 — Source is the deprecated file field.** The label is exactly `settings file (deprecated)`.
- **A4 — No channel supplied a value.** The label is exactly `not set`.
- **A5 — Unrecognized credential-type name passed to the source lookup.** The lookup returns `not set` rather than raising an error.

*Error flows:*
- **E1 — Any fault while building the status output.** The whole settings command wraps every failure and returns the message `Error setting <key>: <message>`.

*Postconditions:* No secret value appears anywhere in the output. The command reports success. On an enabled vault-capable host this single command performs up to six vault round-trips (three credentials × two independent passes).

---

**UC-3.3 — Enable the OS credential vault with explicit consent (realizes US-3.3, US-3.4)**

*Preconditions:* The operator is at an interactive prompt. Standard input is attached and readable.

*Main flow:*
1. The operator issues the enable-vault command (the settings command with the single argument `enablewincred`).
2. The system checks vault availability for this platform.
3. It prints a three-line explanatory banner describing what enabling means.
4. It prompts on standard output `Do you want to enable Windows Credential Manager for secure credential storage? (y/N): ` and blocks reading one line from standard input.
5. It lower-cases the answer. Only `y` and `yes` are affirmative.
6. On an affirmative answer it sets the vault-enabled flag to true on the settings record, writes the whole settings record to the settings file, and prints a confirmation plus two how-to lines showing the store-credential command and an example.
7. The command reports success with the message `Windows Credential Manager integration enabled.`

*Alternate flows:*
- **A1 — Declined.** Any answer other than `y`/`yes` after lower-casing — including an empty line and end-of-input — prints `Windows Credential Manager integration not enabled.`, changes no state, and writes no file.
- **A2 — Non-interactive enable.** The operator instead issues the settings command with key `useWindowsCredentialManager` and a boolean value. No prompt is shown. Setting it to false is always accepted on any platform; setting it to true is accepted only on a platform with a vault. On success the command echoes `Set usewindowscredentialmanager = <value>` with the key lower-cased, and the settings file is written by the general save-after-change path.
- **A3 — Graphical shell.** The operator ticks the enable checkbox in the settings dialog's credentials tab and confirms. The flag is copied into the settings record and the whole record is saved, with **no** availability check.

*Error flows:*
- **E1 — Vault unavailable on this platform.** The system prints `Windows Credential Manager is not available on this platform.`, makes no prompt and no state change, and the command reports failure with `Failed to enable Windows Credential Manager integration.`
- **E2 — Declined by the operator.** Reported to the operator as a **failure** result carrying `Failed to enable Windows Credential Manager integration.` — a deliberate "no" and a broken operation are indistinguishable at the command layer.
- **E3 — Any fault inside the enable flow.** Prints `Error enabling Windows Credential Manager: <message>` and reports failure.
- **E4 — Non-boolean value on the non-interactive path.** Error message `useWindowsCredentialManager must be 'true' or 'false'`.
- **E5 — Non-interactive enable of true on a platform without a vault.** Error message `Windows Credential Manager is not available on this platform.`; the flag is unchanged.
- **E6 — Settings file cannot be written.** The write failure prints `Error saving settings: <message>` and is then swallowed; the command still reports success and the change is lost at the next restart.

*Postconditions:* On success the vault-enabled flag is true both in memory and on disk, and the vault tier becomes eligible during resolution.

---

**UC-3.4 — Store a secret into the OS credential vault (realizes US-3.5)**

*Preconditions:* The vault-enabled flag is true on the in-memory settings record the command holds.

*Main flow:*
1. The operator issues the settings command with key `wincred`, a credential-type token, and a value.
2. The system verifies at least three whitespace-separated tokens are present.
3. It verifies the vault-enabled flag on the in-memory settings record is true.
4. It takes the credential type as the second token with the operator's original casing preserved, and the value as all remaining tokens rejoined with exactly one space between each.
5. The system checks vault availability for this platform.
6. It **re-loads the settings record from disk**, discarding the caller's in-memory object for the rest of this operation.
7. It lower-cases the credential type and maps it, through an alias table, to one of the three fixed vault entry names.
8. It encodes the value as UTF-16 little-endian and writes it into the vault as a generic credential with local-machine persistence, the fixed account label `ChatDbg`, and the fixed comment `ChatDbg API Credential`.
9. On a successful write it sets the vault-enabled flag to true on the freshly loaded settings record and saves that record to disk.
10. It prints `Credential stored securely in Windows Credential Manager: <type>`, echoing the operator's original casing, and the command reports success with the same sentence.

*Alternate flows:*
- **A1 — Graphical shell.** The operator opens the manage-credentials sub-dialog, types a credential type in a plain field and a value in a masked field, and saves. The dialog does not check the vault-enabled flag first.

*Error flows:*
- **E1 — Exactly two tokens** (`wincred` plus a type, no value). Error message, on two lines: `Usage: /set wincred <credential-type> <value>` then `Example: /set wincred azureApiKey your-api-key`.
- **E2 — Exactly one token** (`wincred` alone). A different, earlier, generic argument guard fires first and the operator sees `Usage: /set <key> <value>` instead; the credential-specific usage text is unreachable. Both messages must exist, at their respective token counts.
- **E3 — Vault-enabled flag is false.** Error message, on three lines: `Windows Credential Manager is not enabled. Enable it first with:` / `/set useWindowsCredentialManager true` / `Or use: /set enablewincred`. **Zero** store operations are attempted — this is a hard gate, not an optimization.
- **E4 — Vault unavailable on this platform.** The system prints `Windows Credential Manager is not available on this platform.` and the command reports `Failed to store credential: <type>`.
- **E5 — Unknown credential type after alias lookup.** The system prints `Unknown credential type: <type>` and `Valid types: azureApiKey, awsAccessKey, awsSecretKey`, and the command reports `Failed to store credential: <type>`. No vault entry is created.
- **E6 — The vault write fails** (quota, policy, oversized value). The system prints `Failed to store credential in Windows Credential Manager: <type>` and the command reports `Failed to store credential: <type>`. The vault-enabled flag is not touched and settings are not saved. No size limit is stated and no guidance is given.
- **E7 — Plaintext credentials exist in the settings file.** The mid-operation reload in step 6 re-runs the entire settings-load path, so the plaintext warning and the full migration-instructions block — including the plaintext secret values — are printed again in the middle of storing a secret.
- **E8 — Unsaved in-memory settings changes.** Because step 6 discards the caller's object and step 9 writes back the freshly loaded one, any unsaved in-memory settings changes are silently lost, and the object the shell continues to use is not updated.
- **E9 — Value containing runs of whitespace.** The command-line tokenizer splits on the single space character and discards empty tokens, so runs of two or more spaces collapse to one, and leading/trailing whitespace, tabs, and newlines are lost. A secret containing any of those cannot be entered through this command at all.

*Postconditions:* On success one vault entry holds the value; the vault-enabled flag is true on disk; nothing is written to the settings file's plaintext credential fields.

---

**UC-3.5 — Migrate legacy plaintext credentials (realizes US-3.6, US-3.7)**

*Preconditions:* The operator is at an interactive prompt, standard input is readable, and the settings record the wizard is handed has at least one non-empty plaintext credential field.

*Main flow:*
1. The operator issues the settings command with the single argument `migrate`.
2. The system verifies at least one of the three settings-file credential fields is non-empty.
3. It prints `Migrating credentials from JSON to secure storage...`.
4. It prints a three-option menu whose wording depends on vault availability (see FR-3.51).
5. It prompts `Select migration option (1-3): ` and reads a trimmed line.
6. For option `1`, it prints the environment-variable migration instructions (UC-3.6 step 3).
7. It prompts `Would you like to remove credentials from the settings file now? (y/N): ` and reads a lower-cased line.
8. On `y` or `yes` it sets all three settings-file credential fields to the empty string, saves the settings record to disk, prints `Credentials removed from settings file.` and returns a "migrated" outcome.
9. The command reports success with the message `Migration completed successfully.`

*Alternate flows:*
- **A1 — Option `2` on a vault-capable platform.** For each of the three credentials whose settings-file field is non-empty, the system writes it into the corresponding fixed vault entry and prints a per-credential success line. If at least one write succeeded it sets the vault-enabled flag to true, saves settings, and prints `Windows Credential Manager integration enabled`. The plaintext fields are **not** cleared by this step. The flow then continues at main-flow step 7.
- **A2 — Option `3` on a vault-capable platform.** The system prints the environment-variable instructions, then prints `You can also optionally enable Windows Credential Manager:` and runs the full interactive consent flow of UC-3.3, which prompts for `y`/`N` a second time. The flow then continues at main-flow step 7.
- **A3 — Option `3` on a platform without a vault.** Only the environment-variable instructions are printed; the optional-enable half is skipped. The flow continues at step 7.
- **A4 — Cleanup declined.** Any answer other than `y`/`yes` leaves the plaintext fields intact and returns a "not migrated" outcome, whereupon the command reports success with the message `No credentials found to migrate or migration cancelled.` — even when a vault migration in A1 fully succeeded.
- **A5 — Graphical shell launcher.** The operator opens the migrate-credentials sub-dialog, chooses one of three radio options, and confirms. The selected radio value is read and then discarded; the same console-driven wizard runs regardless. The dialog then reports `Credentials migrated successfully` unconditionally.

*Error flows:*
- **E1 — Nothing to migrate.** The wizard returns immediately with no output at all, and the command reports **success** with `No credentials found to migrate or migration cancelled.`
- **E2 — Option out of range or empty.** The system prints `Migration cancelled.` and returns a "not migrated" outcome; the command still reports success.
- **E3 — Option `2` on a platform without a vault.** The system prints `Windows Credential Manager is not available on this platform.` and aborts the wizard **before** the cleanup prompt; the settings file is left byte-identical.
- **E4 — All vault writes fail under option `2`.** Nothing is enabled, nothing is saved, and **no** failure message is printed — a silent no-op. The wizard proceeds to the cleanup prompt.
- **E5 — Any fault during migration.** The system prints `Error during migration: <message>` and returns a "not migrated" outcome.
- **E6 — Wizard invoked from the graphical shell's chat box.** The command holds a stale, default-constructed settings record whose plaintext fields are always empty, so E1 always fires and the operator is told there is nothing to migrate even when the settings file is full of plaintext secrets. The dialog launcher (A5) does not have this problem because it is handed the loaded record.
- **E7 — Wizard prompts under a full-screen graphical shell.** The menus and both consent prompts are written to standard output beneath the full-screen interface and block on standard input that cannot be typed, so the wizard is unusable there while still emitting the plaintext secrets into the terminal scrollback.

*Postconditions:* If cleanup was confirmed, all three settings-file credential fields are the empty string on disk and the standing plaintext warning stops appearing on subsequent loads. Vault entries created by A1 are never removed by this product.

---

**UC-3.6 — Warn about plaintext credentials at settings load (realizes US-3.6)**

*Preconditions:* A settings load is starting. The load path runs at session startup and again inside the store-a-secret operation.

*Main flow:*
1. The system reads and deserializes the settings file.
2. It tests whether **any** of the three settings-file credential fields is non-empty.
3. If so it prints `??  WARNING: Credentials found in settings file. For security, please migrate to environment variables:` (two literal question-mark characters followed by two spaces).
4. It then prints the environment-variable migration instructions block: the header `To migrate to environment variables, run these commands:`, then one group per populated field, then the footer `Or add them to your system environment variables for persistence.`
5. Independently, it inspects the vault-enabled flag and prints one notice, or none (see FR-3.44).

*Alternate flows:*
- **A1 — Settings file absent.** A default settings record is created **and immediately written to disk**, then returned. The newly written file contains the vault-enabled flag as false and all three plaintext credential keys present as empty strings. No warning is printed.
- **A2 — Deserialization yields nothing.** A default settings record is substituted.
- **A3 — No plaintext credentials present.** Steps 3 and 4 are skipped entirely; nothing at all is printed by the instruction builder.

*Error flows:*
- **E1 — File unreadable or malformed.** The system prints `Error loading settings: <message>` and returns a default settings record. Every stored value is silently discarded for the session: the vault-enabled flag reverts to false, so the vault tier disappears, and the plaintext fields revert to empty. The next save overwrites the unreadable file, permanently destroying whatever secrets it held.

*Postconditions:* The plaintext instruction block **discloses the actual secret values** on standard output, and does so on every settings load while any plaintext field is populated. This directly contradicts the feature's own "report without disclosure" goal and is recorded as QUIRK-3.3.

---

**UC-3.7 — Startup credential diagnostics (realizes US-3.9)**

*Preconditions:* The session has loaded settings, loaded a system prompt, and printed its welcome banner. The banner itself always carries a standing three-line security notice.

*Main flow:*
1. The system reads the configured provider name.
2. It locates the registered service for that provider.
3. It asks that service whether it considers itself configured.
4. If configured, it prints exactly one provenance line naming the channel that supplied the credential.

*Alternate flows:*
- **A1 — Hosted OpenAI-compatible provider, configured.** Prints `Azure credentials loaded from: <source>` when the resolved key is non-empty.
- **A2 — Cloud model-inference provider, configured.** Prints `AWS credentials loaded from: <source>` when either the resolved access key or the resolved secret key is non-empty, but the source label is **always** looked up for the access key.
- **A3 — Local model runtime.** Prints `Local LLM model loaded from: <model path>`; no credential is involved.

*Error flows:*
- **E1 — No provider configured (empty name).** Prints `Warning: No AI provider configured.` plus a hint listing the three provider names, then stops.
- **E2 — Unknown provider name.** Prints `Warning: Unknown AI provider: <provider>` and stops; no credential diagnostics run.
- **E3 — Provider reports itself not configured.** Prints `Warning: <Provider Display Name> service is not configured.` followed by a numbered remediation block: item `1.` names the environment variables for that provider; item `2.` names the enable and store commands, and is included **only** when the platform has a vault. For the hosted provider, when the endpoint is also empty, an additional line tells the operator to set the endpoint.
- **E4 — Cloud model-inference provider holding only half a key pair.** It reports itself configured on the strength of the access key alone, the reassuring provenance line prints, the remediation block is suppressed, and the underlying client silently falls back to the vendor SDK's own ambient credential chain — an undocumented fourth credential channel. The failure, if any, surfaces later as an unrelated request error.
- **E5 — Provider used at chat time with no usable credential.** The provider raises a configuration error and the shell surfaces `Error: <Provider> service is not configured. Use environment variables or Windows Credential Manager to configure credentials securely.`

*Postconditions:* Exactly one of a provenance line or a remediation block has been printed for the active provider. No secret value is printed.

---

**UC-3.8 — Refuse a legacy credential-setting key (realizes US-3.10)**

*Preconditions:* None. This applies on every platform and in every state.

*Main flow:*
1. The operator issues the settings command with one of the three legacy credential keys and a value.
2. The system recognizes the key and refuses it without inspecting or retaining the value.
3. It returns a failure result whose message is built from a fixed template: a lead line naming the credential in friendly form, a `## Secure Options:` heading, item `1. Environment Variables (Recommended):` with one `set <VARNAME>=your-credential` line per accepted variable in declared order, optionally item `2. Windows Credential Manager (Secure Option):` with the enable and store command lines, and a closing line `This keeps your credentials secure and out of configuration files.`
4. No settings save is performed.

*Alternate flows:*
- **A1 — Platform without a vault.** Item `2.` is omitted from the message entirely.

*Error flows:*
- **E1 — Any fault while building the refusal.** Returned as `Error setting <key>: <message>`.

*Postconditions:* The typed value appears in no file, no vault entry, and no in-memory field. There is no code path anywhere in the product that writes a non-empty value into the three plaintext settings-file credential fields; they can only become non-empty by hand-editing the file or by an older build.

---

**UC-3.9 — Manage credentials from the graphical settings dialog (realizes US-3.11)**

*Preconditions:* The full-screen graphical shell is running and the operator has opened the settings dialog.

*Main flow:*
1. The operator selects the credentials tab, which shows a heading, an enable checkbox initialized from the vault-enabled flag, a manage-credentials button, a migrate-credentials button, and static help text listing the three tiers in order.
2. The operator opens the manage-credentials sub-dialog, types a credential type into a plain field and the secret into a **masked** field, and saves.
3. The system calls the store-into-vault operation, closes the sub-dialog, and shows `Credential saved successfully`.
4. On confirming the settings dialog, the checkbox value is copied into the vault-enabled flag and the whole settings record is saved.

*Alternate flows:*
- **A1 — Either field blank or whitespace.** Saving is a silent no-op.

*Error flows:*
- **E1 — The store operation returns a failure.** The dialog ignores the returned outcome and still shows `Credential saved successfully`.
- **E2 — The store operation raises.** The dialog shows `Failed to save credential: <message>`.
- **E3 — Vault-enabled flag is false.** The dialog does not check it and calls the store operation anyway, unlike the console command which refuses.
- **E4 — Platform without a vault.** The checkbox is accepted and the flag is persisted as true with no availability check, after which every subsequent settings load prints the "enabled but not available" warning forever, and every credential resolution wastes a vault probe that can only ever return nothing. The equivalent console command refuses the same action.
- **E5 — Console prompts raised by the migration launcher.** They are written beneath the full-screen interface and cannot be answered (see UC-3.5 E7).

*Postconditions:* One vault entry may have been created; the vault-enabled flag may have been persisted.

---

**Functional requirements**

*Credential identities and channels*

- **FR-3.1** The system shall recognize exactly three logical credentials: the hosted-provider API key, the cloud-inference access key, and the cloud-inference secret key. No fourth credential exists in this feature. (realizes US-3.12)
- **FR-3.2** The hosted-provider API key shall be read from exactly one environment variable, `CHATDBG_AZURE_API_KEY`. There is no vendor-standard alias for it. (realizes US-3.1)
- **FR-3.3** The cloud-inference access key shall be read from `CHATDBG_AWS_ACCESS_KEY` first, then `AWS_ACCESS_KEY_ID`. (realizes US-3.1)
- **FR-3.4** The cloud-inference secret key shall be read from `CHATDBG_AWS_SECRET_KEY` first, then `AWS_SECRET_ACCESS_KEY`. (realizes US-3.1)
- **FR-3.5** The system shall never write any environment variable. Environment variables are a read-only channel.
- **FR-3.6** The three vault entry names shall be the exact literals `ChatDbg:AzureApiKey`, `ChatDbg:AwsAccessKey`, and `ChatDbg:AwsSecretKey`. The colon is part of each name.
- **FR-3.7** The three deprecated settings-file credential field names shall be `azureApiKey`, `awsAccessKey`, and `awsSecretKey`, each defaulting to the empty string.

*Resolution*

- **FR-3.8** Credential resolution shall evaluate channels in strictly this order: (1) environment variables, (2) OS credential vault, (3) settings-file field. This order shall not be configurable or reorderable by any command, setting, or environment variable. (realizes US-3.2)
- **FR-3.9** Within tier 1 the variable names shall be tried in their declared order and the first non-empty value shall win; the product-specific name therefore always beats the vendor-standard alias. (realizes US-3.2)
- **FR-3.10** An environment variable that exists but holds the empty string shall be treated as absent, and resolution shall continue to the next name and then the next tier.
- **FR-3.11** Tier 2 shall be attempted only when the vault-enabled flag on the live in-memory settings record is true, regardless of platform and regardless of whether a matching vault entry exists. (realizes US-3.4)
- **FR-3.12** A vault read that returns an empty string shall be treated as absent and resolution shall fall through to tier 3.
- **FR-3.13** Tier 3 shall return the stored settings-file string unconditionally, including when it is the empty string.
- **FR-3.14** An unknown internal credential key shall resolve to "no value" (null); the three exposed credential properties shall coalesce that to the empty string so a consumer always receives a string. (realizes US-3.12)
- **FR-3.15** Resolution shall be recomputed on every single read. Nothing shall be cached or memoized; changing an environment variable or writing a vault entry mid-session shall change the very next read with no restart and no explicit invalidation step. *(This has a measurable cost — see FR-3.75 — and is a design property, not a tunable.)*
- **FR-3.16** Consumers shall treat an empty resolved value as "no credential" and shall not be able to determine which channel supplied a non-empty value from the value alone. (realizes US-3.12)

*Source reporting*

- **FR-3.17** The system shall provide a credential-source lookup taking a credential-type name, lower-casing it before matching, and accepting `azureapikey`, `awsaccesskey`, and `awssecretkey`. (realizes US-3.8)
- **FR-3.18** The source lookup shall return one of exactly four shapes: `environment variable (<VARNAME>)` naming the actual variable found, `Windows Credential Manager`, `settings file (deprecated)`, or `not set`. (realizes US-3.8)
- **FR-3.19** The source lookup shall probe channels in the identical order as FR-3.8 and shall perform a **second, independent** resolution pass rather than reusing a value produced by FR-3.8.
- **FR-3.20** The source lookup shall never include a secret value in its output. (realizes US-3.8)
- **FR-3.21** An unrecognized credential-type name passed to the source lookup shall return the literal `not set`, not an error. (realizes US-3.8)

*Status display*

- **FR-3.22** The settings command with zero arguments shall print a status block that always names the active provider, and shall report success. (realizes US-3.8)
- **FR-3.23** The status block shall contain a credentials section headed `Credentials (secure):` with one line per credential, labelled `- Azure API Key:`, `- AWS Access Key:`, and `- AWS Secret Key:` in that order. (realizes US-3.8)
- **FR-3.24** Each credential line shall show the literal `(not set)` when the resolved value is null or empty, and the literal `***set***` otherwise, followed by the source label in square brackets. (realizes US-3.8)
- **FR-3.25** The status block shall never render a secret value under any condition. (realizes US-3.8)
- **FR-3.26** The status block shall include a line reporting `- Windows Credential Manager: Enabled` or `Disabled` taken from the vault-enabled flag.

*Enabling the vault*

- **FR-3.27** The settings command shall accept exactly two keys with a single argument and no value: `migrate` and `enablewincred`. Every other key with a single argument shall produce the error `Usage: /set <key> <value>`. (realizes US-3.4, US-3.7)
- **FR-3.28** The enable-vault flow shall, on a platform without a vault, print `Windows Credential Manager is not available on this platform.`, prompt for nothing, change no state, and report failure with `Failed to enable Windows Credential Manager integration.` (realizes US-3.4)
- **FR-3.29** On a vault-capable platform the enable-vault flow shall print an explanatory banner, then prompt exactly `Do you want to enable Windows Credential Manager for secure credential storage? (y/N): ` and block on one line of standard input. (realizes US-3.4)
- **FR-3.30** Only the lower-cased answers `y` and `yes` shall be affirmative at this prompt. Every other answer, including an empty line and end-of-input, shall be treated as a decline. (realizes US-3.4)
- **FR-3.31** On an affirmative answer the system shall set the vault-enabled flag to true, persist the **entire** settings record to the settings file, print a confirmation and two how-to lines (the store command and the example `Example: /set wincred azureApiKey your-api-key`), and report success with `Windows Credential Manager integration enabled.` (realizes US-3.4)
- **FR-3.32** On a decline the system shall print `Windows Credential Manager integration not enabled.`, change no state, write no file, and report **failure** with `Failed to enable Windows Credential Manager integration.` (realizes US-3.4)
- **FR-3.33** The settings command with key `useWindowsCredentialManager` shall parse a boolean from the joined remaining arguments; a non-boolean shall produce the error `useWindowsCredentialManager must be 'true' or 'false'`.
- **FR-3.34** Setting `useWindowsCredentialManager` to true on a platform without a vault shall be refused with `Windows Credential Manager is not available on this platform.` and shall leave the flag unchanged. Setting it to false shall always be accepted on every platform.
- **FR-3.35** On success the non-interactive path shall echo `Set usewindowscredentialmanager = <value>` with the key rendered in lower case.

*Storing a secret*

- **FR-3.36** The store-a-secret command shall require at least three whitespace-separated tokens. With exactly two tokens it shall produce the two-line message `Usage: /set wincred <credential-type> <value>` / `Example: /set wincred azureApiKey your-api-key`. (realizes US-3.5)
- **FR-3.37** The store-a-secret command shall refuse to act when the vault-enabled flag on its in-memory settings record is false, producing the three-line message `Windows Credential Manager is not enabled. Enable it first with:` / `/set useWindowsCredentialManager true` / `Or use: /set enablewincred`, and shall invoke the store-into-vault operation **exactly zero** times. This is a hard gate that must be observable in a test. (realizes US-3.5)
- **FR-3.38** The credential type shall be taken from the second token with the operator's casing preserved for display, and lower-cased only for alias lookup. (realizes US-3.5)
- **FR-3.39** The stored value shall be all tokens from the third onward rejoined with exactly one space between each. Because the command line is split on the single space character with empty tokens discarded, runs of two or more spaces collapse to one, and leading whitespace, trailing whitespace, tabs and newlines are unrecoverably lost.
- **FR-3.40** The write-path alias table shall accept, case-insensitively: `azureapikey` or `azure` → `ChatDbg:AzureApiKey`; `awsaccesskey` or `awsaccess` → `ChatDbg:AwsAccessKey`; `awssecretkey` or `awssecret` → `ChatDbg:AwsSecretKey`. Anything else shall print `Unknown credential type: <type>` and `Valid types: azureApiKey, awsAccessKey, awsSecretKey`, and fail. (realizes US-3.5)
- **FR-3.41** The read-path key map (from internal camelCase key to entry name) and the write-path alias map (from a user-typed, lower-cased type string) shall be distinct maps with different accepted inputs; the read path is case-**sensitive** while the write path is case-insensitive with extra aliases.
- **FR-3.42** The store operation shall re-load the settings record from disk before writing, mutate that freshly loaded record, and save it — discarding the caller's in-memory record for the remainder of the operation.
- **FR-3.43** On a successful vault write the system shall set the vault-enabled flag to true on the freshly loaded record, save it, print `Credential stored securely in Windows Credential Manager: <type>` echoing the operator's original casing, and report success with the same sentence. On a failed write it shall print `Failed to store credential in Windows Credential Manager: <type>`, report `Failed to store credential: <type>`, leave the flag untouched, and save nothing.

*Load-time warnings*

- **FR-3.44** On every settings load, after deserialization, the system shall print exactly one of: `?? Windows Credential Manager integration is enabled for secure credential storage.` when the vault-enabled flag is true and the platform has a vault; `??  WARNING: Windows Credential Manager is enabled in settings but not available on this platform.` when the flag is true and the platform has none; or nothing at all when the flag is false. *(The leading question marks are literal characters in the shipped output — see QUIRK-3.15.)*
- **FR-3.45** "Plaintext credentials present" shall be true if and only if at least one of the three settings-file credential fields is non-empty. (realizes US-3.6)
- **FR-3.46** When plaintext credentials are present, every settings load shall print `??  WARNING: Credentials found in settings file. For security, please migrate to environment variables:` followed by the environment-variable instructions block. (realizes US-3.6)
- **FR-3.47** The environment-variable instructions block shall contain, for a populated hosted-provider key, the lines `For Azure OpenAI:` and `  set CHATDBG_AZURE_API_KEY=<value>`; for a populated access key, `For AWS Bedrock:` and `  set CHATDBG_AWS_ACCESS_KEY=<value>`; and for a populated secret key, only `  set CHATDBG_AWS_SECRET_KEY=<value>` with no group header of its own. Where `<value>` is the actual stored plaintext secret. (realizes US-3.7)
- **FR-3.48** When the instruction list is non-empty it shall be preceded by `To migrate to environment variables, run these commands:` and followed by `Or add them to your system environment variables for persistence.` When it is empty, nothing at all shall be printed.
- **FR-3.49** The instructions builder shall accept an "environment variables only" mode flag from its callers and shall produce byte-identical output whether or not the flag is set; the flag is inert.

*Migration wizard*

- **FR-3.50** The migration wizard shall return a "not migrated" outcome immediately, with no output at all, when no settings-file credential field is populated. (realizes US-3.7)
- **FR-3.51** The wizard menu shall always print `1. Environment Variables (Recommended - works on all platforms)` as option 1. On a vault-capable platform options 2 and 3 shall read `2. Windows Credential Manager (Secure Windows-specific storage)` and `3. Both (Environment Variables + Windows Credential Manager option)`. On a platform without a vault they shall read `2. Windows Credential Manager (Not available on this platform)` and `3. Environment Variables only`.
- **FR-3.52** The wizard shall prompt `Select migration option (1-3): ` and shall dispatch only on the exact trimmed strings `1`, `2`, and `3`. Any other input, including empty, shall print `Migration cancelled.` and return a "not migrated" outcome.
- **FR-3.53** Option `2` on a platform without a vault shall print `Windows Credential Manager is not available on this platform.` and abort the wizard **before** the cleanup prompt, leaving the settings file unchanged.
- **FR-3.54** Option `2` on a vault-capable platform shall, for each of the three credentials whose settings-file field is non-empty, write that value into the fixed entry name and print on success one of `Azure API Key migrated to Windows Credential Manager`, `AWS Access Key migrated to Windows Credential Manager`, or `AWS Secret Key migrated to Windows Credential Manager`. (realizes US-3.7)
- **FR-3.55** If at least one bulk-migration write succeeded, the system shall set the vault-enabled flag to true, save settings, and print `Windows Credential Manager integration enabled`. If none succeeded it shall enable nothing, save nothing, and print nothing.
- **FR-3.56** Bulk migration shall not clear the settings-file plaintext fields. Clearing shall happen only through the separate cleanup confirmation.
- **FR-3.57** Option `3` shall print the environment-variable instructions and then, only on a vault-capable platform, print `You can also optionally enable Windows Credential Manager:` and run the full interactive consent flow of FR-3.29 through FR-3.32, which prompts a second time.
- **FR-3.58** After the chosen branch completes (except the abort in FR-3.53), the wizard shall prompt `Would you like to remove credentials from the settings file now? (y/N): ` and read a lower-cased line.
- **FR-3.59** Only the lower-cased answers `y` and `yes` shall be affirmative at the cleanup prompt. On an affirmative answer the system shall set all three settings-file credential fields to the empty string, save the settings record, print `Credentials removed from settings file.`, and return a "migrated" outcome. (realizes US-3.7)
- **FR-3.60** Confirming the cleanup shall be the **only** action that makes the wizard return a "migrated" outcome. Any other path, including a fully successful vault migration whose cleanup was declined, shall return "not migrated".
- **FR-3.61** The migration command shall always return a **success** result regardless of outcome, carrying `Migration completed successfully.` for a "migrated" outcome and `No credentials found to migrate or migration cancelled.` otherwise.
- **FR-3.62** The migration command shall invoke the migration operation exactly once, passing the command's own live settings record rather than a freshly loaded one.
- **FR-3.63** Any fault during migration shall print `Error during migration: <message>` and return a "not migrated" outcome.

*Legacy key refusal*

- **FR-3.64** The settings keys `azureapikey`, `awsaccesskey`, and `awssecretkey` shall be recognized and always refused with a failure result, on every platform and in every state. (realizes US-3.10)
- **FR-3.65** The refusal message shall be built from a fixed template: a lead line `For security, <Friendly Name> is no longer set via this command.`; a `## Secure Options:` heading; `1. Environment Variables (Recommended):` with one `   set <VARNAME>=your-credential` line per accepted variable in declared order; and a closing `This keeps your credentials secure and out of configuration files.` Friendly names are `Azure API Key`, `AWS Access Key`, and `AWS Secret Key`. (realizes US-3.10)
- **FR-3.66** The refusal message shall include a second option block naming `/set enablewincred` and `/set wincred <type> your-credential` **only** on a platform with a vault.
- **FR-3.67** The typed value shall never be stored anywhere. No code path in the product shall write a non-empty value into any of the three settings-file credential fields; they may become non-empty only by hand-editing the file or by a file written by an older build. (realizes US-3.10)

*Command-surface rules*

- **FR-3.68** The settings command key shall be lower-cased before dispatch; credential-type arguments shall not be lower-cased at the command layer.
- **FR-3.69** The keys `wincred`, `enablewincred`, `migrate`, and the three refused legacy credential keys shall all suppress the command's generic save-settings step. A non-credential settings key shall perform exactly one save.
- **FR-3.70** Any unhandled fault inside the settings command shall be returned as `Error setting <key>: <message>`.
- **FR-3.71** An unknown settings key shall produce `Unknown setting: <key>. Valid keys: provider, modelId, temperature, maxTokens, azureEndpoint, awsRegion, systemPrompt, enableLogProbabilities, logProbabilitiesTopK, useWindowsCredentialManager, enablewincred, wincred, migrate`. This list is incomplete with respect to the keys actually accepted and shall be reproduced verbatim.

*Vault primitive contract*

- **FR-3.72** The OS credential vault adapter shall expose exactly four operations — availability probe, read-by-entry-name, write-by-entry-name, delete-by-entry-name — each synchronous, blocking, total, and guaranteed never to raise. Read returns an optional string, write and delete return booleans, availability returns a boolean.
- **FR-3.73** The availability probe shall be a bare "is the running operating system Windows" test and shall never attempt any vault access. It shall not attempt to determine whether the vault service actually responds; a host whose credential service is disabled by policy is therefore reported as available and every operation fails later with the generic failure message.
- **FR-3.74** On a platform without a vault, read shall return "no value", write shall return false, delete shall return false, and the adapter shall emit no message of its own. Higher layers own the user-visible "not available on this platform" message.
- **FR-3.75** Every vault operation shall swallow all faults and degrade to the "not found"/"failed" result, emitting no message, no log entry, and no rethrow. A genuine vault fault shall therefore be indistinguishable from a missing entry at every layer.
- **FR-3.76** A stored secret shall be encoded as UTF-16 little-endian text; the declared blob size shall be the **byte** count, i.e. twice the character count. Readers shall decode with the same encoding.
- **FR-3.77** A vault entry whose stored blob has zero length shall read back as "not found", not as an empty string.
- **FR-3.78** Every written entry shall be a **generic**-kind credential (kind constant `1`) with persistence scope `2` meaning local-machine persistence — surviving logoff and reboot on that machine and explicitly **not** roaming with a domain profile — a fixed comment `ChatDbg API Credential`, and an account label defaulting to the literal `ChatDbg`. Reserved flag fields shall be zero.
- **FR-3.79** No length, character-set, emptiness, or content validation shall be performed on a secret before storage. Any string is encoded and handed to the operating system as-is; an oversized value is refused by the operating system and surfaces only as the generic store failure. *(INFERRED: the platform's documented generic-credential blob limit is 2560 bytes, i.e. 1280 UTF-16 characters; the limit is platform documentation, not code, and the code contains only the absence of a check.)*
- **FR-3.80** The read path shall match on entry name only and shall ignore both the account label and the comment. Two operating-system users on one machine get separate entry sets because the operating system scopes them; one operating-system user cannot keep two product profiles apart.
- **FR-3.81** A delete-by-entry-name primitive shall exist in the adapter and shall have **zero** call sites in the product. No command, menu item, or wizard step removes a stored secret; the cleanup confirmation clears only the settings-file fields. Rotation and revocation must be done in the operating system's own credential interface.
- **FR-3.82** Writing to a never-before-used entry name shall return a boolean exactly equal to the availability probe — true on Windows, false on every other platform. This equality is an asserted requirement.

*Storage location and persistence*

- **FR-3.83** The settings file shall live at `<user profile directory>/.ChatDbg/settings.json` by default. If the resolved base directory is blank, or resolving it raises, the base directory shall fall back to the operating system temp directory with the file name unchanged, silently and with no message. *(This relocates the plaintext credential slots into a directory that is world-readable on most systems.)*
- **FR-3.84** The settings base directory and the file name shall both be overridable at construction of the settings store, and the store shall report the resolved settings-file path. A clone must expose this seam or the feature is untestable.
- **FR-3.85** The settings directory shall be created on demand at save time.
- **FR-3.86** The settings file shall be written pretty-printed with camelCase key naming; explicitly named fields override the naming policy. The three deprecated plaintext credential fields shall be serialized; the three resolved credential values shall be excluded from serialization. Preserving exactly this split is required for both backward compatibility and non-disclosure.
- **FR-3.87** When the settings file is absent, a default settings record shall be created **and immediately written to disk**, and the file so created shall contain the vault-enabled flag as `false` and all three plaintext credential keys present as `""`.
- **FR-3.88** A settings load that raises shall print `Error loading settings: <message>` and return a default settings record, silently discarding whatever the file held — reverting the vault-enabled flag to false for the session and the plaintext fields to empty — and the next save shall overwrite the unreadable file.
- **FR-3.89** A settings save that raises shall print `Error saving settings: <message>` and return normally with no failure signal, so every caller reports success while nothing reached disk.
- **FR-3.90** The default of the vault-enabled flag shall be `false`.
- **FR-3.91** No permission, mode, or access-control-list restriction shall be applied to the settings file or its directory; the only permission model is whatever the operating system's defaults provide. There is no privileged mode and nothing this feature does requires elevated rights.

*Startup diagnostics*

- **FR-3.92** The welcome banner shall always print the three-line standing notice: `Security Enhancement: Credentials are now managed via environment variables` / `   or Windows Credential Manager for improved security.` / `   See '/set' command for more details.` (realizes US-3.9)
- **FR-3.93** With no provider configured, startup shall print `Warning: No AI provider configured.` plus a hint listing the three provider names, and shall run no further credential diagnostics. (realizes US-3.9)
- **FR-3.94** With a provider name that has no registered service, startup shall print `Warning: Unknown AI provider: <provider>` and shall run no further credential diagnostics. (realizes US-3.9)
- **FR-3.95** When the selected provider reports itself not configured, startup shall print `Warning: <Provider Display Name> service is not configured.` followed by a remediation block numbered `1.` for environment variables and `2.` for the vault commands, where item `2.` appears only on a vault-capable platform. For the hosted provider, when the endpoint is also empty, an extra line shall instruct the operator to set the endpoint. (realizes US-3.9)
- **FR-3.96** When the selected provider reports itself configured, startup shall print exactly one provenance line: `Azure credentials loaded from: <source>` for the hosted provider when its resolved key is non-empty; `AWS credentials loaded from: <source>` for the cloud-inference provider when either its resolved access key or its resolved secret key is non-empty, with the source label always looked up for the **access** key; or `Local LLM model loaded from: <model path>` for the local runtime. (realizes US-3.9)
- **FR-3.97** The cloud-inference provider shall report itself configured when it has a model identifier and a non-empty resolved access key, without inspecting the secret key at all; its underlying client shall supply explicit static credentials only when **both** halves are non-empty and shall otherwise fall back to the vendor SDK's own ambient credential chain — an undocumented fourth credential channel a clone inherits unless it closes it deliberately.

*Graphical surface*

- **FR-3.98** The graphical settings dialog shall present a credentials tab containing a heading `Credential Management`, a checkbox `Enable Windows Credential Manager` initialized from the vault-enabled flag, a `Manage Credentials...` button, a `Migrate Credentials...` button, and static help text listing `1. Environment variables (recommended)`, `2. Windows Credential Manager (Windows only)`, `3. Settings file (deprecated)`. (realizes US-3.11)
- **FR-3.99** Confirming the settings dialog shall copy the checkbox value into the vault-enabled flag and save the whole settings record, with **no** platform availability check.
- **FR-3.100** The manage-credentials sub-dialog shall offer a plain `Credential Type:` field, a **masked** `Value:` field, help text naming `azureApiKey / awsAccessKey / awsSecretKey`, and save/cancel actions. Saving with either field blank or whitespace shall be a silent no-op. (realizes US-3.11)
- **FR-3.101** The manage-credentials save action shall call the store-into-vault operation without first checking the vault-enabled flag, shall show `Credential saved successfully` regardless of the returned outcome, and shall show `Failed to save credential: <message>` only when the operation raises.
- **FR-3.102** The migrate-credentials sub-dialog shall present explanatory text and a three-way radio group labelled `Environment Variables`, `Windows Credential Manager`, and `Both`. The selected value shall be read and then discarded; the same console-driven wizard shall run in all three cases, after which the dialog shall show `Credentials migrated successfully` unconditionally, or `Migration failed: <message>` when the operation raises.
- **FR-3.103** The settings command instance available to the graphical shell's chat box shall be bound to a settings record that is not the one the windows and dialogs use, with the consequence that the store command's enable gate always sees false and the migration command's plaintext check always sees empty fields. The dialog-driven migration launcher shall be bound to the loaded record and shall not have this problem, so the product offers two migration entry points operating on different data.

*Non-functional properties that are behaviorally observable*

- **FR-3.104** There shall be no caching, no memoization, no time-to-live, no invalidation step, and no batch-read facility for resolved credentials. A single status command on a vault-enabled vault-capable host performs up to six vault round-trips.
- **FR-3.105** There shall be no locking, no file lock, and no atomic rename around settings load or save. The store-a-secret path performs a whole-file read-modify-write that loses a concurrent edit outright; two instances of the product sharing one settings file interleave writes with no detection.
- **FR-3.106** Consent and menu prompts shall block the calling thread on standard input with no timeout and no cancellation signal.
- **FR-3.107** There shall be no structured logging, no log sink, and no audit record of any credential operation. Nothing records when a secret was stored, rotated, or read; the only trace is a line on the console.
- **FR-3.108** Secrets shall be handled as ordinary immutable text values with no protected-memory type, no zeroing after use, no pinning, and freely made copies at every tier boundary.
- **FR-3.109** All user-visible text shall be a hard-coded single-language literal. There is no message catalogue, no formatting indirection, and no locale awareness.
- **FR-3.110** The console store-a-secret path shall provide no masked entry; the secret is typed in the clear, echoed to the terminal, and lands in shell history. Only the graphical value field is masked. *(A clone is advised to provide masked entry on both surfaces; that change is an Open Question, not a silent fix.)*

**External technology**

*Requires: an operating-system-managed, encrypted-at-rest, per-user secret store addressable by an opaque entry name (native OS credential-store API; entries created as "generic" credentials with an explicit persistence scope). Source used: Microsoft Windows Credential Manager, reached through direct native calls to `advapi32.dll` (`CredReadW`, `CredWriteW`, `CredDeleteW`, `CredFree`) with a manually marshalled credential structure. Reimplementer notes: this is the only encrypted-at-rest channel in the product. The stored blob is UTF-16 little-endian and its declared size is in bytes (twice the character count). Persistence scope `2` means local-machine — it explicitly does not roam with a domain profile, contradicting the source's own documentation. Entry names contain a colon (`ChatDbg:AzureApiKey`). The natural equivalents elsewhere are the macOS keychain and a Secret-Service/keyring daemon on Linux, but the source deliberately reports "unavailable" rather than substituting one, and a test pins write-returns-false off-Windows. Decide explicitly whether your clone keeps or generalizes that refusal (Open Question OQ-3.2). No specific encryption algorithm is a requirement — "the operating system encrypts it" is.*

*Requires: native-library interoperation with manual memory management (C ABI, structure marshalling, UTF-16 string allocation). Source used: platform invoke with `AllocHGlobal`/`FreeHGlobal`, `Marshal.Copy`, `PtrToStructure`, with unsafe blocks enabled in the core library. Reimplementer notes: if your target language has a first-class keyring binding this layer disappears entirely. What must survive is the adapter's public shape: read returns an optional string, write returns a boolean, delete returns a boolean, availability returns a boolean, and none of the four ever raises. Every native handle and allocated buffer must be released on every path including the failure paths.*

*Requires: read access to the process environment (POSIX / Windows environment variables). Source used: the standard runtime environment API. Reimplementer notes: read-only; the product never sets a variable. A variable that exists but holds the empty string must be treated as absent, and lookups must be repeated on every read because nothing is cached.*

*Requires: local document persistence for non-secret configuration, human-readable and hand-editable, backward-compatible with files written by earlier versions (JSON, pretty-printed, camelCase keys). Source used: a structured-document serializer with pretty-printing, a camelCase naming policy, explicit per-field name overrides, and explicit exclusion of computed and resolved members. Reimplementer notes: the file format is a compatibility requirement — a clone must read `{"useWindowsCredentialManager": false, "azureApiKey": "", "awsAccessKey": "", "awsSecretKey": ""}` and files where those three string fields are non-empty. Preserve exactly the split between the three serialized legacy fields and the three excluded resolved values, or you either break backward compatibility or leak resolved secrets to disk.*

*Requires: user home / profile directory discovery with a temp-directory fallback (filesystem). Source used: the user-profile special-folder lookup, falling back to the platform temp path when the result is blank or the lookup raises. Reimplementer notes: default path `<home>/.ChatDbg/settings.json`. The fallback silently relocates the file — plaintext credential slots included — into a temp directory that is world-readable on most systems, with no message to the user.*

*Requires: line-oriented interactive console input and output for consent prompts and wizard menus. Source used: direct standard-output writes and standard-input line reads performed inside the service layer itself. Reimplementer notes: this is a hard coupling in the source and the direct cause of the graphical shell's unusable wizard. The reads block with no timeout and no cancellation. A clone should inject an interaction port so both shells and the tests can supply their own implementation — but note that changing this changes observable behavior and belongs in the Open Questions decision, not in a silent refactor.*

*Requires: a full-screen terminal user-interface toolkit offering tabbed dialogs, checkboxes, modal sub-dialogs, radio groups, and a masked text field. Source used: Terminal.Gui. Reimplementer notes: needed only for the graphical credential surface. The masked-entry capability is the one thing the console path lacks. The toolkit owns the terminal, which is precisely why the service layer's own standard-output prompts are invisible and unanswerable there.*

*Requires: a command-line tokenizer. Source used: a split on the single space character discarding empty tokens. Reimplementer notes: this determines exactly which secrets are typeable — single internal spaces survive, runs of spaces collapse to one, tabs and newlines are impossible. Pin this behavior or change it deliberately.*

*Requires: a hosted OpenAI-compatible chat service (HTTPS, API-key header authentication) — consumer of this feature only. Source used: Azure OpenAI, via its SDK and a raw HTTP path that sets an `api-key` header. Reimplementer notes: listed here only because it is the sink for one resolved secret; its own behavior is specified in the provider section.*

*Requires: a cloud model-inference service (HTTPS, request signing with an access-key / secret-key pair) — consumer of this feature only. Source used: the Amazon Bedrock Runtime SDK, constructed either with explicit static credentials or with the SDK's own credential chain. Reimplementer notes: its "am I configured" test ignores the secret half of the pair entirely, and its fallback to the SDK's ambient credential chain is a fourth, undocumented credential channel that a clone inherits unless it closes it.*

*Requires: randomized identifier generation (test-only). Source used: GUID generation, used to build never-before-written vault entry names. Reimplementer notes: keep an equivalent so the "unknown entry reads as absent" test stays honest.*

*Requires: a mocking / test-double framework (test-only). Source used: Moq, used to assert the store-into-vault operation is invoked exactly zero times when the enable flag is false. Reimplementer notes: the credential-store boundary must remain an injectable interface or FR-3.37 becomes unassertable.*

**Acceptance criteria**

- **AC-3.1** *Given* `CHATDBG_AZURE_API_KEY` is set to `from-env` and the settings file's `azureApiKey` field holds `from-json`, *when* the resolved hosted-provider key is read, *then* it equals exactly `from-env`.
- **AC-3.2** *Given* no credential environment variables are set and the vault-enabled flag is `false`, and the settings file's `azureApiKey` field holds `stored-value`, *when* the resolved hosted-provider key is read, *then* it equals exactly `stored-value`.
- **AC-3.3** *Given* `CHATDBG_AWS_ACCESS_KEY` is set to `from-env`, *when* the credential source for the mixed-case name `awsAccessKey` is requested, *then* the returned string contains the phrase `environment variable` (compared case-insensitively) and names `CHATDBG_AWS_ACCESS_KEY`.
- **AC-3.4** *Given* `CHATDBG_AWS_ACCESS_KEY` is unset and `AWS_ACCESS_KEY_ID` is set to `vendor-value`, *when* the resolved access key is read, *then* it equals `vendor-value` and its source reads exactly `environment variable (AWS_ACCESS_KEY_ID)`.
- **AC-3.5** *Given* `CHATDBG_AWS_ACCESS_KEY` is set to `product-value` and `AWS_ACCESS_KEY_ID` is set to `vendor-value`, *when* the resolved access key is read, *then* it equals `product-value`.
- **AC-3.6** *Given* `CHATDBG_AZURE_API_KEY` is set to the empty string and `azureApiKey` in the settings file holds `stored-value`, *when* the resolved hosted-provider key is read, *then* it equals `stored-value` — an empty variable is treated as absent.
- **AC-3.7** *Given* the vault-enabled flag is `false`, *when* the operator runs `/set wincred azureApiKey value`, *then* the command reports failure and the store-into-vault operation is invoked exactly **zero** times.
- **AC-3.8** *Given* a settings record whose three plaintext credential fields are all `""`, *when* the operator runs `/set migrate`, *then* the migration operation is invoked exactly once with that same settings record and the command returns a **success** result carrying `No credentials found to migrate or migration cancelled.`
- **AC-3.9** *Given* a randomly generated vault entry name that was never written, *when* it is read, *then* "no value" is returned and no exception escapes.
- **AC-3.10** *Given* a randomly generated vault entry name, *when* a value is written to it, *then* the returned boolean equals the platform-availability probe — `true` on Windows and `false` on every other platform.
- **AC-3.11** *Given* a non-Windows host, *when* the operator runs `/set useWindowsCredentialManager true`, *then* the command fails with `Windows Credential Manager is not available on this platform.` and the flag remains `false`; *and when* they run `/set useWindowsCredentialManager false`, *then* it succeeds and echoes `Set usewindowscredentialmanager = false`.
- **AC-3.12** *Given* the vault-enabled flag is `true` on Windows, the vault entry `ChatDbg:AzureApiKey` holds `sk-live-1234`, and no hosted-provider environment variable is set, *when* the operator runs bare `/set`, *then* the Azure line reads `***set***` with source `[Windows Credential Manager]` and the substring `sk-live-1234` appears nowhere in the output.
- **AC-3.13** *Given* no credential is available through any channel, *when* the operator runs bare `/set`, *then* each of the three credential lines reads `(not set)` with source `[not set]`, and the block includes `- Windows Credential Manager: Disabled`.
- **AC-3.14** *Given* Windows and the vault-enabled flag `true` with all three credentials resolving from the vault, *when* the operator runs bare `/set`, *then* the output contains exactly three `***set***` tokens, three `[Windows Credential Manager]` labels, the line `- Windows Credential Manager: Enabled`, and no secret substring.
- **AC-3.15** *Given* Windows, *when* the operator runs `/set enablewincred` and answers `N` or presses Enter, *then* the flag stays `false`, the settings file is not rewritten, `Windows Credential Manager integration not enabled.` is printed, and the command reports **failure** with `Failed to enable Windows Credential Manager integration.`
- **AC-3.16** *Given* Windows, *when* the operator runs `/set enablewincred` and answers `yes`, *then* the flag becomes `true`, the settings file is rewritten containing `"useWindowsCredentialManager": true`, and the command reports success with `Windows Credential Manager integration enabled.`
- **AC-3.17** *Given* a non-Windows host, *when* the operator runs `/set enablewincred`, *then* `Windows Credential Manager is not available on this platform.` is printed, **no** prompt is shown, and the command reports failure.
- **AC-3.18** *Given* the vault-enabled flag is `true` on Windows, *when* the operator runs `/set wincred awsaccess my secret value`, *then* the value stored under `ChatDbg:AwsAccessKey` is exactly `my secret value` and the alias `awsaccess` is accepted.
- **AC-3.19** *Given* the vault-enabled flag is `true` on Windows, *when* the operator runs `/set wincred AZURE hunter2`, *then* the alias resolves case-insensitively to `ChatDbg:AzureApiKey`, the stored value is exactly `hunter2`, and the confirmation echoes the operator's casing: `Credential stored securely in Windows Credential Manager: AZURE`.
- **AC-3.20** *Given* the vault-enabled flag is `true` on Windows, *when* the operator runs `/set wincred azureApiKey a  b` with two spaces between `a` and `b`, *then* the value stored under `ChatDbg:AzureApiKey` is `a b` with a single space.
- **AC-3.21** *Given* the vault-enabled flag is `true`, *when* the operator runs `/set wincred bogusType x`, *then* the console shows `Unknown credential type: bogusType` and `Valid types: azureApiKey, awsAccessKey, awsSecretKey`, the command reports `Failed to store credential: bogusType`, and no vault entry is created.
- **AC-3.22** *Given* any platform, *when* the operator runs `/set wincred` with no further tokens, *then* the message is `Usage: /set <key> <value>`; *and when* they run `/set wincred azureApiKey` with exactly two tokens, *then* the message is `Usage: /set wincred <credential-type> <value>` followed by `Example: /set wincred azureApiKey your-api-key`.
- **AC-3.23** *Given* a settings file containing `"azureApiKey": ""`, `"awsAccessKey": "AKIA-legacy"`, `"awsSecretKey": ""`, *when* settings are loaded, *then* standard output carries `??  WARNING: Credentials found in settings file. For security, please migrate to environment variables:`, then `To migrate to environment variables, run these commands:`, then exactly the two lines `For AWS Bedrock:` and `  set CHATDBG_AWS_ACCESS_KEY=AKIA-legacy`, then `Or add them to your system environment variables for persistence.` — with no hosted-provider and no secret-key lines, and the plaintext value **is** disclosed.
- **AC-3.24** *Given* a settings file with all three credential fields `""`, *when* settings are loaded, *then* no plaintext warning and no instruction block appear.
- **AC-3.25** *Given* a settings file containing plaintext credentials, *when* the operator runs `/set migrate`, selects option `1`, and answers `y` to the cleanup prompt, *then* all three credential fields in the settings file become `""`, the file is rewritten, `Credentials removed from settings file.` is printed, and the command reports `Migration completed successfully.`
- **AC-3.26** *Given* a settings file with `"awsAccessKey": "AKIA-legacy"` on a **non-Windows** host, *when* the operator runs `/set migrate` and enters `2`, *then* menu line 2 read `2. Windows Credential Manager (Not available on this platform)`, the wizard printed `Windows Credential Manager is not available on this platform.`, the cleanup prompt is **never shown**, the settings file is byte-identical afterwards, and the command nevertheless returns a **success** result reading `No credentials found to migrate or migration cancelled.`
- **AC-3.27** *Given* a settings file with `"awsAccessKey": "AKIA-legacy"` on Windows with the vault reachable, *when* the operator runs `/set migrate`, enters `2`, and answers `n` to the cleanup prompt, *then* the vault entry `ChatDbg:AwsAccessKey` holds `AKIA-legacy`, `"useWindowsCredentialManager": true` is on disk, the plaintext field **still** holds `AKIA-legacy`, and the command nevertheless reports `No credentials found to migrate or migration cancelled.`
- **AC-3.28** *Given* a settings file with plaintext credentials on Windows, *when* the operator runs `/set migrate` and enters `4`, *then* `Migration cancelled.` is printed, the cleanup prompt is never shown, and the command still returns a **success** result reading `No credentials found to migrate or migration cancelled.`
- **AC-3.29** *Given* any platform and any state, *when* the operator runs `/set azureapikey sk-123`, `/set awsaccesskey AKIA-1`, or `/set awssecretkey s3cr3t`, *then* each returns an **error**, no settings save occurs, the typed value appears in no file and no vault entry, and the message names that credential's environment variables in declared order (`CHATDBG_AZURE_API_KEY`; `CHATDBG_AWS_ACCESS_KEY` then `AWS_ACCESS_KEY_ID`; `CHATDBG_AWS_SECRET_KEY` then `AWS_SECRET_ACCESS_KEY`) and includes the `/set enablewincred` and `/set wincred` lines **only** on Windows.
- **AC-3.30** *Given* provider `azure`, endpoint `https://r.openai.azure.com/`, model identifier `gpt-4`, and `CHATDBG_AZURE_API_KEY` set to `sk-live`, *when* the shell starts, *then* exactly one line `Azure credentials loaded from: environment variable (CHATDBG_AZURE_API_KEY)` is printed and no `Warning: Azure OpenAI service is not configured.` block appears.
- **AC-3.31** *Given* provider `bedrock`, model identifier `claude-3`, `CHATDBG_AWS_ACCESS_KEY` set to `AKIA-x`, and no secret key in any channel, *when* the shell starts, *then* it prints `AWS credentials loaded from: environment variable (CHATDBG_AWS_ACCESS_KEY)` and **no** remediation block — the product declares itself configured while holding half a key pair.
- **AC-3.32** *Given* provider `bedrock` with the access key resolved from an environment variable and the secret key resolved from the vault, *when* the shell starts, *then* the provenance line names the **environment variable** source only, because the label is always looked up for the access key.
- **AC-3.33** *Given* a vault entry `ChatDbg:AzureApiKey` written by another tool holding a zero-length blob, *when* the hosted-provider key is resolved with the flag `true` on Windows, *then* the entry is treated as absent, resolution falls through to the settings-file field and returns `""` when that is empty, with no message and no error anywhere.
- **AC-3.34** *Given* a settings file whose contents are not a valid structured document, *when* settings are loaded, *then* standard output carries `Error loading settings: <message>`, the session runs with all defaults (`useWindowsCredentialManager` back to `false`, all three plaintext fields back to `""`), and the next save overwrites the unreadable file, permanently discarding whatever secrets it held.
- **AC-3.35** *Given* the settings directory does not exist and no settings file exists, *when* settings are loaded, *then* the directory is created, a file is written containing `"useWindowsCredentialManager": false` and all three plaintext credential keys as `""`, and the returned record has a non-empty model identifier.
- **AC-3.36** *Given* a settings store constructed against the base directory `<temp>/ChatDbgSettingsTests/<random id>`, *when* settings with provider `bedrock` are saved and then loaded, *then* the loaded provider is `bedrock` and the reported settings-file path starts with that base directory, compared case-insensitively.
- **AC-3.37** *Given* a read-only settings file, *when* the operator runs `/set useWindowsCredentialManager true` on Windows, *then* `Error saving settings: <message>` is printed, the command still answers `Set usewindowscredentialmanager = true`, and after a restart the flag is `false` again.
- **AC-3.38** *Given* a **non-Windows** host, *when* the operator ticks `Enable Windows Credential Manager` in the graphical settings dialog's credentials tab and confirms, *then* the settings file is written with `"useWindowsCredentialManager": true` — no refusal, unlike the console command — and every subsequent load prints `??  WARNING: Windows Credential Manager is enabled in settings but not available on this platform.`
- **AC-3.39** *Given* the graphical shell is running with a settings file full of plaintext credentials, *when* the operator types `/set migrate` into the chat box, *then* the answer is always `No credentials found to migrate or migration cancelled.`; *and when* the operator instead presses the settings dialog's migrate button, *then* the wizard sees the real plaintext data.
- **AC-3.40** *Given* the graphical manage-credentials sub-dialog on a non-Windows host, *when* the operator enters a credential type and a value and saves, *then* the dialog shows `Credential saved successfully` even though the underlying store operation returned failure.
- **AC-3.41** *Given* a running session with `CHATDBG_AZURE_API_KEY` unset, *when* the variable is set in the process environment and the operator immediately runs bare `/set`, *then* the hosted-provider line reads `***set***` with source `environment variable (CHATDBG_AZURE_API_KEY)` with no restart and no explicit refresh step.
- **AC-3.42** *Given* Windows with the vault-enabled flag `true` and a stored entry, *when* the operator runs `/set useWindowsCredentialManager false` and then bare `/set`, *then* the credential's source is no longer `Windows Credential Manager` and the vault tier is not probed at all.
- **AC-3.43** *Given* a stored vault entry created by this product, *when* the operator searches the entire command surface for a way to delete it, *then* no command, menu item, or wizard step exists; the cleanup confirmation clears only the settings-file fields.

**Quirks**

- **QUIRK-3.1**: The documentation's claim of "no plaintext credential storage on disk" is false as shipped — the three legacy credential fields are still serialized into the settings file on every save, and a freshly created settings file is written containing all three as empty strings; the storage slot and the read path both remain fully live. Evidence: `docs/SECURITY-IMPLEMENTATION.md:18`, `docs/WINCRED-IMPLEMENTATION.md:173`, `Models/ChatSettings.cs:79-86`, `Services/SettingsService.cs:47-53`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.2**: The user manual names the vault commands and the vault-enabled setting but documents **no** environment variable names anywhere, so a reimplementer working from it alone would miss the primary credential channel entirely. Evidence: `README.md:58-60,106`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.3**: The migration instructions interpolate the actual stored secrets into `set CHATDBG_...=<value>` lines on standard output, and that block is printed automatically on **every** settings load whenever plaintext credentials exist — including the extra load performed in the middle of the store-a-secret operation. This directly contradicts the feature's own "report without disclosure" goal. Evidence: `Services/SettingsService.cs:335,341,346` and `:60-64`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.4**: Declining the consent prompt is reported to the operator as a **failure** (`Failed to enable Windows Credential Manager integration.`), conflating "the user said no" with "something broke". Evidence: `Commands/SetCommand.cs:255-257`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.5**: The migration command always returns a success result whatever happened, while the wizard returns "not migrated" whenever the operator declines the *cleanup* prompt — even after a fully successful vault migration. "Migrated to the vault but kept the file copy" is therefore reported as "No credentials found to migrate or migration cancelled." Evidence: `Commands/SetCommand.cs:275-277`, `Services/SettingsService.cs:261`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.6**: The status builder performs a text substitution intended to adapt the credential block for platforms without a vault, but the string it searches for does not occur anywhere in the block being built; the substitution is a permanent no-op and the status output never adapts to platform. Evidence: `Commands/SetCommand.cs:451-455`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.7**: The store-a-secret operation re-reads settings from disk and ignores the caller's record. Consequences: the plaintext warning and the whole secret-bearing instruction block may print again mid-command; any unsaved in-memory settings changes are silently dropped when the fresh record is written back; and the record the shell continues to use is not updated. Evidence: `Services/SettingsService.cs:155`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.8**: The instructions printer accepts an "environment variables only" mode flag which both wizard branches pass explicitly, but the body never consults it; output is byte-identical in all cases, so the flag reads like a feature and is inert. Evidence: `Services/SettingsService.cs:328` versus `:216,232,330-357`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.9**: A corrupt settings file silently reverts the vault-enabled flag to false for the session, so the encrypted tier disappears with only a generic `Error loading settings:` line as warning. Evidence: `Services/SettingsService.cs:78-82`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.10**: Save failures are invisible to every caller — the save operation prints an error and returns normally with no failure signal, so the settings command, the enable flow, the store flow, the bulk migration, the cleanup step and the graphical dialog's confirm button all report success while nothing reached disk. Evidence: `Services/SettingsService.cs:100-103`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.11**: The graphical manage-credentials dialog calls the store operation without checking the vault-enabled flag — unlike the console command, which refuses — and reports `Credential saved successfully` regardless of the returned boolean. Evidence: `UI/SettingsDialog.cs:543-546`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.12**: The graphical migrate-credentials radio selection is read into a local value and never used; the same console-driven wizard runs regardless, writing prompts to a standard output hidden behind a full-screen interface and blocking on input that cannot be typed, and the dialog then reports `Credentials migrated successfully` unconditionally. Evidence: `UI/SettingsDialog.cs:587-589`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.13**: In the graphical shell the settings command is bound to a record that is later replaced by the loaded one, so its enable gate always sees `false` and its plaintext check always sees empty fields — making `/set wincred` unable to succeed and `/set migrate` a guaranteed no-op from the chat box, while the dialog-driven migration button works on the real data. Evidence: `ChatDbg.Shell.Gui/Program.cs:14,38,58`; `UI/ChatWindow.cs:1037`; `UI/SettingsDialog.cs:17-22`. The same project also contains an entire alternative shell class carrying a duplicate copy of the startup credential diagnostics that is never instantiated: `ChatDbg.Shell.Gui/ChatShell.cs:213-295`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.14**: A delete-a-vault-entry primitive is fully implemented and has zero call sites anywhere in the product; there is no command, menu item, or wizard step that removes a stored secret, and the documentation states the delete operation is implemented. Rotation and revocation must be done in the operating system's own credential interface. Evidence: `Models/WindowsCredentialManager.cs:148-163`; `docs/WINCRED-IMPLEMENTATION.md:26`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.15**: Two *separate* text-corruption defects produce mojibake in shipped output and must not be confused. (a) The settings-service source file is pure ASCII with its status glyphs committed as genuine question-mark characters, so the program really prints `??  WARNING: Credentials found in settings file...` and `? Credential stored securely in Windows Credential Manager: <type>`. (b) The settings-command help text and the graphical settings dialog are stored with a raw `0x95` byte — a Windows-1252 bullet that is not valid UTF-8 — so every help bullet decodes to a replacement glyph. Evidence: `Services/SettingsService.cs:62,110,180,257`; `Commands/SetCommand.cs:307-352`; `UI/SettingsDialog.cs`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.16**: The documentation states the vault uses a specific platform encryption facility; the code only calls the generic credential-store API and encryption at rest is whatever the operating system provides. Evidence: `docs/SECURITY-IMPLEMENTATION.md:66,100`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.17**: The two credential test scripts in the scratch folder are manual, assert nothing, exit zero regardless, and end by launching the application, so neither can run unattended. One of them sets scratch variables (`TEMP_AZURE_KEY`, `TEMP_AWS_ACCESS`, `TEMP_AWS_SECRET`) that are **not** credential variable names and are never read by the product — they exist only so the script can echo example values into printed instructions. Evidence: `tmp/test-wincred.cmd:11-13,26-28`; `tmp/test-env-vars.cmd:48-50`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.18**: The documentation shows a "Credential Storage Priority" footer inside the status output that the code never prints. Evidence: `docs/SECURITY-IMPLEMENTATION.md:150-154`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.19**: The graphical settings dialog can enable the vault on a platform that has none — the checkbox is copied straight into the flag and saved with no availability check, while the equivalent console command refuses the same action. Every subsequent load then prints the "enabled but not available" warning forever and every resolution wastes a probe that can only return nothing. Evidence: `UI/SettingsDialog.cs:431-437,501` versus `Commands/SetCommand.cs:219-222`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.20**: The cloud-inference provider reports itself configured on the strength of a model identifier and the **access** key alone, never inspecting the secret key, so the reassuring provenance line prints, the remediation block is suppressed, and the client then falls back silently to the vendor SDK's own credential chain because it requires both halves. The operator is told they are configured and fails later with an unrelated error. Evidence: `Services/BedrockService.cs:23-27`; `Services/DefaultBedrockRuntimeClientFactory.cs:11-20`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.21**: The same configured-check ORs the resolved access key with a direct read of `AWS_ACCESS_KEY_ID`, but the resolution layer already consults that variable as the access key's second name, so the second half of the OR can never decide anything — and it bypasses the very resolution layer this feature exists to provide. Evidence: `Services/BedrockService.cs:26-27`; `Models/ChatSettings.cs:73`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.22**: The startup provenance line for the cloud-inference provider is emitted when *either* half of the key pair is non-empty but always reports the source of the **access** key, so an operator whose secret key came from the vault and whose access key came from the environment is told the credentials came from the environment. Evidence: `ChatDbg/ChatShell.cs:310-312`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.23**: No validation of a secret before storage — no length cap, no character filter, no emptiness check between reading the typed value and handing it to the operating system. An oversized value produces only the generic failure message with no hint of the cause. Evidence: `Models/WindowsCredentialManager.cs:110-127`. *(INFERRED: the platform's documented generic-credential blob limit is 2560 bytes, i.e. 1280 UTF-16 characters; the limit is platform documentation, not code.)* Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.24**: Entries are written with local-machine persistence, which explicitly does not roam with a domain profile, while the documentation promises central policy management, domain security, enterprise backup/restore and enterprise integration — all of which describe the roaming persistence scope that is not used. Evidence: `Models/WindowsCredentialManager.cs:120`; `docs/WINCRED-IMPLEMENTATION.md:179-181`; `docs/SECURITY-IMPLEMENTATION.md:103,242-245`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.25**: A doubly-swallowed fault — the resolution path wraps its vault lookup in a catch-everything around an adapter that already catches everything and returns "no value". The outer handler is unreachable in practice, and together they guarantee that a genuine vault fault (policy denial, corrupted store, exhausted handles) is indistinguishable from "the entry is not there", at every layer, forever, with nothing logged. Evidence: `Models/ChatSettings.cs:137-141`; `Models/WindowsCredentialManager.cs:88-91`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.26**: The store-a-secret operation lower-cases the type for lookup but echoes the operator's original casing in both the success and failure messages, so `/set wincred AZURE hunter2` reports storing a "credential type" named `AZURE` that appears in no documentation and in no valid-types list. Evidence: `Services/SettingsService.cs:157,180,185`; `Commands/SetCommand.cs:243,249-250`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.27**: The status display and the startup provenance line each resolve every credential twice through two independent passes, so a single status command on an enabled vault-capable host performs up to six native vault round-trips and the two passes can in principle disagree if a variable changes between them. Evidence: `Commands/SetCommand.cs:420-422`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.28**: In the graphical shell every credential message — the load-time plaintext warning, the secret-bearing migration instructions, the wizard menus, both consent prompts, the unknown-type errors and the enable banner — is written directly to standard output and read from standard input from inside the service layer, beneath a full-screen interface painted over the same terminal. The output still reaches the terminal scrollback, so the secrets are still disclosed; they are merely invisible while the interface is up, and the prompts block the interface thread waiting for input that cannot be typed. Evidence: `Services/SettingsService.cs` throughout; `ChatDbg.Shell.Gui/Program.cs:71-90`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.29**: The documented consent prompt text does not match the shipped one — the documentation shows `Do you want to enable Windows Credential Manager? (y/N):` while the code asks `Do you want to enable Windows Credential Manager for secure credential storage? (y/N): `. Minor, but it is the string a transcript test would pin. Evidence: `docs/SECURITY-IMPLEMENTATION.md:121`; `Services/SettingsService.cs:120`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.30**: A fourth storage tier — "encrypted credential files" — is listed inside a defense-in-depth list presented as implemented, and does not exist in the code. There are exactly three tiers. Evidence: `docs/SECURITY-IMPLEMENTATION.md:183`. Keep-or-fix decision deferred to Open Questions.
- **QUIRK-3.31**: No settings-file permission hardening exists anywhere — no mode or access-control-list call, no ownership check — and the repository's ignore list does not name the settings file either, so an operator who relocates the settings directory into a working tree gets no protection from the tooling. Evidence: `Services/SettingsService.cs:89-93`; `.gitignore`. Keep-or-fix decision deferred to Open Questions.

**Source notes**

Dossier of record: `/mnt/g/3RD-Party/reversing/output/chatdbg/dossiers/credential-management.md` (feature 6 in `/mnt/g/3RD-Party/reversing/output/chatdbg/inventory.md`).

Source repository: `/mnt/g/3RD-Party/reversing/subject/chatdbg` @ commit `d8c18f61d6bb73666ed97cd4885e877e35558485` (branch `LLamaSharp_support`). All paths below are relative to that root.

Primary evidence:
- `src/Xcaciv.ChatDbg.Core/Models/ChatSettings.cs` — credential identities, environment-variable lists, vault entry names, resolution algorithm, source lookup, plaintext detection.
- `src/Xcaciv.ChatDbg.Core/Models/WindowsCredentialManager.cs` — the four vault primitives, platform probe, encoding, persistence scope, constants.
- `src/Xcaciv.ChatDbg.Core/Services/SettingsService.cs` — settings path resolution and fallback, load/save, plaintext warning, enable flow, store flow, migration wizard, instruction builder, bulk migration.
- `src/Xcaciv.ChatDbg.Core/Commands/SetCommand.cs` — command gating, status block, masking tokens, legacy-key refusal template, help text.
- `src/ChatDbg/ChatShell.cs` — startup credential diagnostics, welcome banner security notice, command tokenizer.
- `src/ChatDbg.Shell.Gui/Program.cs`, `src/ChatDbg.Shell.Gui/UI/SettingsDialog.cs`, `src/ChatDbg.Shell.Gui/UI/ChatWindow.cs`, `src/ChatDbg.Shell.Gui/ChatShell.cs` — graphical credential surface and its divergences.
- `src/Xcaciv.ChatDbg.Core/Services/BedrockService.cs`, `src/Xcaciv.ChatDbg.Core/Services/DefaultBedrockRuntimeClientFactory.cs`, `src/Xcaciv.ChatDbg.Core/Services/AzureOpenAIService.cs` — consumer-side "am I configured" semantics and the ambient-credential-chain fallback.

Test evidence (10 tests across four files):
- `src/Xcaciv.ChatDbg.Core.Tests/Models/ChatSettingsTests.cs`
- `src/Xcaciv.ChatDbg.Core.Tests/Models/WindowsCredentialManagerTests.cs`
- `src/Xcaciv.ChatDbg.Core.Tests/Commands/SetCommandTests.cs`
- `src/Xcaciv.ChatDbg.Core.Tests/Services/SettingsServiceTests.cs`

Documentation consulted (treated as hints; where documentation and code disagree, code is the specification):
- `docs/SECURITY-IMPLEMENTATION.md`, `docs/WINCRED-IMPLEMENTATION.md`, `README.md`, `tmp/test-wincred.cmd`, `tmp/test-env-vars.cmd`, `.gitignore`.
