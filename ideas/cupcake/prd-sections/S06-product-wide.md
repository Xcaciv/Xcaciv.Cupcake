## 6. Product-Wide Requirements

The rules in this section are inherited by everything. They are stated once here because they appear
in several feature dossiers and a clone that re-derives them feature-by-feature will derive them
inconsistently. Feature subsections (§7) cite a `GR-` rather than restating it; where a feature
*narrows* a product-wide rule, the narrowing lives in the feature and says so.

**Where these rules come from.** The overwhelming majority of them are properties of the **Command
Service** — the required capability the product obtains rather than implements (§7.1). They are
nonetheless product-wide *requirements*, not background: every Command in the product, built-in,
host-linked or supplied by a Plugin, inherits them, and a clone that reproduces the Shell without
them reproduces a different product. Requirements resting **only** on evidence from the external
capability's source are marked **OUT-OF-REPO only** at the point of claim, and every such citation
carries the version caveat in *Source notes*.

**The product itself was never executed.** The product does not build at the pinned commit
(established; see *Source notes*), so almost every statement is read from source. Three exceptions are
stated at the point of claim, each established by running an *equivalent construct* in a scratch
directory outside the source tree rather than by running the product: the containment primitive of
GR-48; the shadowed visibility flag of GR-33; and the directory enumeration behind the Plugin scan of
GR-50, which was executed against a real filesystem holding a correct Plugin layout and **raises**
instead of matching. That third result is load-bearing for this whole section — it is why GR-41a,
GR-49 and GR-50 read as they now do, and why **no Plugin is ever loaded from disk on a real
filesystem**. Register rows: Q-82 and Q-83.

---

### 6.1 Command-line semantics every Command inherits

- **GR-1** — The Session shall pass a submitted Command line to the Command Service **byte for
  byte**. No trimming, case folding, expansion, aliasing, history substitution or rewriting shall be
  performed before dispatch. Every rule in §6.1 and §6.2 is therefore the Command Service's, and is
  identical for a Built-in Command, a Host-linked Command and a Plugin Command.
  (`src/Xcaciv.Cupcake.Core/Loop.cs:62,94`)

- **GR-2** — **OUT-OF-REPO only.** The **Command name** shall be derived from the line as follows, in
  this order: trim the line; take the text before the first space, or the whole line when there is no
  space; strip leading and trailing hyphens; **delete** every character outside letters, digits,
  space, `-` and `_`; upper-case the result. Command names are upper-cased identically at
  registration, so invocation is case-insensitive and declared casing is cosmetic. Deletion is
  silent — an offending character is removed, never rejected.
  (OUT-OF-REPO: `src/Xcaciv.Command.Interface/CommandDescription.cs:18,27,52-64`, framework v2.1.2)

- **GR-3** — **OUT-OF-REPO only.** **Arguments** shall be extracted by scanning the line for matches
  of an alternation — *either* a double-quoted run *or* a run of word characters and hyphens — in
  that order. The **first match is discarded** as the Command name. Each remaining match shall then
  have every character outside the argument allow-list **deleted**, and surrounding double quotes
  trimmed. The argument allow-list is exactly: letters, digits, space, and
  `- _ . * ? [ ] | " ~ ! @ # $ % ^ & * ( )`. Anything else — including `:`, `/`, `\`, `;`, `,`, `+`,
  `=`, `<`, `>`, `{`, `}` — is deleted with no message.
  (OUT-OF-REPO: `src/Xcaciv.Command.Interface/CommandDescription.cs:23,70-79`, framework v2.1.2)

- **GR-4** — **QUIRK. OUT-OF-REPO only.** Because an unquoted token matches only word characters and
  hyphens, and because separators are discarded rather than preserved, **a dotted, colonned or
  slashed token typed without double quotes is shredded into fragments before any Command sees it**.
  Worked consequences, derived by replaying the documented tokenizer and allow-list against sample
  input: `PACKAGE SEARCH cake.nuget` delivers the two arguments `cake` and `nuget`, and only `cake`
  binds to the first Positional Parameter; `PACKAGE SEARCH ../../etc/passwd` delivers `etc` and
  `passwd`; `SET PackageSourceUrl https://api.nuget.org/v3/index.json` delivers `PackageSourceUrl`,
  `https`, `api`, `nuget`, `org`, `v3`, `index`, `json`. Double-quoting keeps the run whole but does
  not restore the deleted characters: the same address quoted arrives as the single mangled token
  `httpsapi.nuget.orgv3index.json`. The practical consequence is that **the Registry Endpoint setting
  cannot be set from the Prompt at all** — a Shell Operator must seed it programmatically — and that
  the product's own primary domain identifiers (dotted Plugin names, registry addresses) are the
  exact shapes the tokenizer destroys. Register row: Q-43.
  (OUT-OF-REPO: `src/Xcaciv.Command.Interface/CommandDescription.cs:23,70-79`, framework v2.1.2;
  consuming site `src/Xcaciv.Command.Packages/SearchCommand.cs:24-35`)

- **GR-5** — **QUIRK. OUT-OF-REPO only.** A Session Variable reference of the form `%NAME%` shall
  survive to a Command **only inside a double-quoted run**. The percent sign is on the argument
  allow-list, so it is not deleted, but it is not part of the unquoted token pattern, so an unquoted
  reference arrives as the bare token `NAME` and nothing expands it. The echo Built-in Command's own
  help remark instructs the user to use double quotes for exactly this reason. A reference that does
  survive quoting but names a key that is not set comes back **with doubled delimiters** — the failed
  match is re-wrapped in a fresh pair of percent signs, so `SAY "%NOPE%"` renders `%%NOPE%%` — and the
  failed lookup itself performs the mutating read of GR-26. Register row: Q-65.
  (OUT-OF-REPO: `src/Xcaciv.Command.Interface/CommandDescription.cs:23,70-79`,
  `src/Xcaciv.Command/Commands/SayCommand.cs:15,25,33-48`, framework v2.1.2)

- **GR-6** — **QUIRK. OUT-OF-REPO only.** The choice between the Pipeline path and the single-Command
  path shall be a **naive search for the delimiter character `|` anywhere in the raw line**, performed
  *before* any quote or escape handling. A line whose only delimiter is quoted or backslash-escaped
  is therefore still routed to the Pipeline machinery; the Pipeline splitter then yields a single
  stage. Such a line runs through a bounded Stage Channel, its Output appears only after the stage
  completes, and — unlike the single-Command path — the caller's own argument list is never
  overwritten (GR-10). Quoting or escaping a delimiter cannot avoid the Pipeline machinery, only its
  splitting. The second half of the same asymmetry: the Pipeline splitter **consumes** quote
  characters before each stage is re-tokenised (GR-7), so double quotes protect a dotted argument at
  the top level (GR-4) but not inside a Pipeline, where the tokenizer sees the stage text already
  unquoted. Register row: Q-63.
  (OUT-OF-REPO: `src/Xcaciv.Command/CommandController.cs:234-246`,
  `src/Xcaciv.Command.Interface/CommandSyntax.cs:9-12`, quote characters consumed at
  `src/Xcaciv.Command/PipelineParser.cs:66-77,99,121`, framework v2.1.2)

- **GR-7** — **OUT-OF-REPO only.** Pipeline splitting shall obey: `|` separates stages; `"` groups
  text and honours escapes; `'` groups text literally with **no** escape processing; `\` escapes
  `|`, `"`, `'` and itself; each segment is trimmed; **empty segments are dropped**, so `a || b` is
  two stages, not three. An unclosed quote of either kind shall abort the **whole line** with the
  message `Unbalanced <quote-char> quote in pipeline.` and no stage shall execute.
  (OUT-OF-REPO: `src/Xcaciv.Command/PipelineParser.cs:24-91,129`, framework v2.1.2)

- **GR-8** — **OUT-OF-REPO only.** Every Pipeline stage shall run on its **own child Interaction
  Context**, be stamped with its 1-based stage number and the stage total, read the previous stage's
  Stage Channel as input, and write a **fresh** Stage Channel as output — including the last stage.
  All stages shall be started at once and run **concurrently**; the Pipeline is complete only when
  every stage has finished, and only then is the final stage's Stage Channel drained into the parent
  Interaction Context, which is what renders it. Consequences a clone must preserve: no stage renders
  its own Output; a stage with an input channel runs the Per-chunk invocation once per non-empty
  upstream chunk, otherwise the Direct invocation runs; empty chunks are never delivered and empty
  results are never forwarded.
  (OUT-OF-REPO: `src/Xcaciv.Command/PipelineExecutor.cs:41-63,78-104,179-193`,
  `src/Xcaciv.Command.Core/AbstractCommand.cs:152-173`,
  `src/Xcaciv.Command/CommandExecutor.cs:196-201`, framework v2.1.2)

- **GR-9** — **OUT-OF-REPO only.** Stage Channel defaults, none of which the product ever overrides,
  shall be: capacity **10,000** items; full-buffer policy **Block** (the producer waits; the
  alternatives are drop-oldest and drop-newest); whole-Pipeline timeout **0 = none**; per-stage
  timeout **0 = none**; per-stage output byte cap **0 = unlimited**; per-stage output item cap
  **0 = unlimited**. INFERRED consequence, not exercised by any test in either tree: because the
  final stage's channel is drained only after every stage has completed, and the default policy
  blocks, **a final stage producing more than 10,000 chunks stalls forever**. The two stage-completion
  messages the capability can emit — `Stage '<NAME>' exceeded timeout of <N> seconds` and
  `Stage '<NAME>' was cancelled` — are unreachable under the product's stock settings, because both
  require configuration the product never performs; a clone must still provide them for embedders who
  do configure a timeout. Register rows: Q-64 (the stall) and Q-36 (the unreachable messages).
  (OUT-OF-REPO: `src/Xcaciv.Command.Interface/PipelineConfiguration.cs:15-71`,
  `src/Xcaciv.Command/PipelineExecutor.cs:41-63,122,157-164`, framework v2.1.2; the product configures
  nothing — repo-wide search finds no assignment)

- **GR-10** — **OUT-OF-REPO only.** On the single-Command path the Command Service shall **overwrite
  the caller's own Interaction Context argument list** with the parsed arguments and then create a
  child Interaction Context seeded with that same list. The root Interaction Context's argument list
  is therefore not the empty list it was constructed with after the first Command runs. On the
  Pipeline path the caller's list shall **not** be replaced; each stage's child receives that stage's
  own parsed arguments.
  (OUT-OF-REPO: `src/Xcaciv.Command/CommandController.cs:240-246`,
  `src/Xcaciv.Command/PipelineExecutor.cs:85-87`, framework v2.1.2)

- **GR-11** — **OUT-OF-REPO only.** A Command Group shall be dispatched by matching the **first
  remaining argument, upper-cased**, against the group's member table; on a match that token is
  **removed from the argument list** before the member runs, so a member never sees its own name.
  Matching is case-insensitive: the member table is keyed by the upper-cased declared name and the
  typed token is upper-cased before lookup. A Command Group's own Command Registration Record carries
  no executable identity, so invoking the group with a non-matching or absent first argument fails as
  an ordinary **Command execution error (GR-38)** — the Output line
  `Error executing <GROUP> (see trace for more info)` followed by the Status line
  `**Error: Command type name is empty.`, with the Session surviving — and **not** as an unknown
  Command, never as a "missing sub-command" message, and never as a listing of the members that do
  exist. Register row: Q-72.
  (OUT-OF-REPO: `src/Xcaciv.Command/CommandFactory.cs:43-62`, empty-identity raise at `:60-63`,
  `src/Xcaciv.Command/CommandExecutor.cs:184,228-229`,
  `src/Xcaciv.Command.Core/CommandParameters.cs:184-198`,
  `src/Xcaciv.Command.Interface/Attributes/CommandRegisterAttribute.cs:27-32`, framework v2.1.2)

- **GR-12** — **OUT-OF-REPO only.** A help request shall be recognised when **any** argument equals
  `--HELP`, `-?` or `/?`, compared case-insensitively; the bare Command name `HELP` shall list every
  registered Command, one line each, in no defined order. **QUIRK:** of those three spellings only the
  double-dash form survives argument tokenization — a typed `-?` arrives as the bare token `-` and a
  typed `/?` disappears entirely (GR-3) — so **only `--HELP` is reachable from the Prompt**.
  **QUIRK, INFERRED:** requesting help on a member of a Command Group prints the **group's one-line
  member listing**, not the member's own parameter help, because the help branch is taken on the group
  and member selection happens only on the execute path; there is consequently **no invocation at the
  Prompt that prints a grouped member's full parameter help**. Typing the help word with an argument
  (`HELP <name>`) also ignores the argument and prints the full listing. Register rows: Q-44 (two of
  the three spellings unreachable) and Q-45 (`HELP <name>` ignores its argument; a grouped member's
  own help is unreachable).
  (OUT-OF-REPO: `src/Xcaciv.Command/HelpService.cs:165-175`,
  `src/Xcaciv.Command/CommandExecutor.cs:27,58-81`,
  `src/Xcaciv.Command.Interface/CommandDescription.cs:70-79`,
  `src/Xcaciv.Command/CommandRegistry.cs:81-84`, framework v2.1.2)

---

### 6.2 Argument-binding semantics every Command inherits

- **GR-13** — **OUT-OF-REPO only.** Argument binding shall be **opt-in by the Command**, not performed
  by the Command Service. The Command Service hands over the raw token list; a Command that wants a
  name-to-value map calls the inherited binder itself. Both Commands the product ships do call it.
  A clone that binds automatically changes when binding errors surface.
  (OUT-OF-REPO: `src/Xcaciv.Command.Core/AbstractCommand.cs:178-192`, framework v2.1.2; call site
  `src/Xcaciv.Command.Packages/SearchCommand.cs:22`)

- **GR-14** — **OUT-OF-REPO only.** Binding order shall be fixed: **Positional Parameters, then Flags,
  then Named Parameters, then suffix parameters**. Each phase consumes from, and removes from, one
  shared working token list, so an earlier phase sees tokens a later phase would have claimed.
  (OUT-OF-REPO: `src/Xcaciv.Command.Core/AbstractCommand.cs:185-189`, framework v2.1.2)

- **GR-15** — **QUIRK. OUT-OF-REPO only.** When a Command is invoked with **zero** arguments the
  binder shall return an **empty** map immediately: no required-parameter check runs, no allow-list
  check runs, and **no declared default is substituted**. A Command that reads a defaulted parameter
  out of the map therefore fails on the *lookup*, and the user sees whatever message that Command
  raises for a missing value rather than a message about the parameter they actually omitted. This is
  the single most misleading behaviour in the binding contract and it is inherited by every Command.
  The product's own instance of it is register row Q-5: a Package Search typed with no arguments
  reports that the Result-limit option must be an integer, not that the search term is missing.
  (OUT-OF-REPO: `src/Xcaciv.Command.Core/AbstractCommand.cs:178-180`, framework v2.1.2; observable
  consequence at `src/Xcaciv.Command.Packages/SearchCommand.cs:42-45`)

- **GR-16** — **OUT-OF-REPO only.** A Positional Parameter shall be taken from the **front** of the
  remaining tokens. If the working list is exhausted, a parameter that is not required and has a
  non-empty declared default binds that default; one that is required raises
  `Missing required parameter <name>`. If the front token **begins with `-`**, the parameter is not
  consumed: it raises `Missing required parameter <name>` when required with no default, and otherwise
  is **silently omitted from the map with no default substituted** — the token is left in place for
  the Flag and Named phases. A suffix parameter in the same position behaves differently: it binds its
  declared default whenever that default is non-empty.
  (OUT-OF-REPO: `src/Xcaciv.Command.Core/CommandParameters.cs:86-132,134-167`, framework v2.1.2)

- **GR-17** — **QUIRK. OUT-OF-REPO only.** Because Positional binding runs **before** Flags and Named
  Parameters are stripped (GR-14, GR-16), **positional arguments must be typed before any option**.
  `<COMMAND> -take 2 XCBatch` fails with `Missing required parameter search_terms`, while
  `<COMMAND> XCBatch -take 2` succeeds. Every passing test in the source puts the positional first.
  This ordering constraint is a product-wide property of the Command line, not a property of any one
  Command. Register row: Q-4.
  (OUT-OF-REPO: `src/Xcaciv.Command.Core/CommandParameters.cs:105-130`,
  `src/Xcaciv.Command.Tests/ParameterBoundsTests.cs:182-198`, framework v2.1.2;
  `Xcaciv.Command.PackagesTests/SearchCommandTests.cs:14,30,45,60,75,88,102,117`)

- **GR-18** — **OUT-OF-REPO only.** A Flag shall be matched by a token that **starts with `-`** and
  matches one or two leading hyphens immediately followed by the declared name or short alias; both
  `-name` and `--name` are accepted. The matched token is removed from the working list. First match
  in token order wins.
  (OUT-OF-REPO: `src/Xcaciv.Command.Core/CommandParameters.cs:12-33`, framework v2.1.2)

- **GR-19** — **QUIRK. OUT-OF-REPO only.** Every declared Flag key shall be added to the bound map
  **unconditionally**, carrying the literal text `True` or `False`. **Presence in the map therefore
  says nothing about whether the user typed the Flag; only the value does.** Any Command that tests
  presence rather than value has a Flag that is permanently on. This is inherited by every Command and
  is the mechanism behind the product's always-on Prerelease inclusion (§7.8, register row Q-1).
  (OUT-OF-REPO: `src/Xcaciv.Command.Core/CommandParameters.cs:34`, framework v2.1.2; consuming site
  `src/Xcaciv.Command.Packages/SearchCommand.cs:47`)

- **GR-20** — **OUT-OF-REPO only.** A Named Parameter shall consume **the matched token and the token
  immediately after it**, removing both. When unmatched: bind the declared default if it is non-empty;
  otherwise raise `Missing required parameter <name>` if required; otherwise bind the empty string.
  The key is added unconditionally. **QUIRK, INFERRED:** the following token is read with no bounds
  check, so a Named Parameter typed as the **last token with no value after it** is not diagnosed —
  the binder reads past the end of the list and the user sees a generic Command execution error
  (GR-38) carrying a raw index message rather than a product message. The Session survives; only the
  Command fails. Register row: Q-59.
  (OUT-OF-REPO: `src/Xcaciv.Command.Core/CommandParameters.cs:38-82`, read-past-end at `:52-56`,
  framework v2.1.2)

- **GR-21** — **QUIRK. OUT-OF-REPO only.** Option **names** shall be matched with no end anchor, so any
  token that *begins* with one or two hyphens followed by the declared name also matches: a parameter
  declared `take` is satisfied by the token `-taken`, and a one-letter short alias `t` is satisfied by
  `-take`, swallowing the wrong option **and the token after it**. A token that merely embeds the name
  (`--myverbosity` against a parameter declared `verbosity`) does not match, because the hyphens must
  be adjacent to the name. First match in token order wins, for Flags (GR-18) and Named Parameters
  alike. Register row: Q-58.
  (OUT-OF-REPO: `src/Xcaciv.Command.Core/CommandParameters.cs:17-19,43-52`, framework v2.1.2)

- **GR-22** — **OUT-OF-REPO only.** When a parameter declares an allowed-value list, a bound value
  outside it shall raise `Invalid value for parameter <name>, this parameter has an allow list.` The
  value comparison **ignores case**. Scope of the check, which is not uniform: for a Named Parameter it
  runs on whatever ends up bound, including a substituted default and the empty string bound when
  nothing was supplied; for a Positional Parameter it runs **only when a token was actually taken from
  the front of the list**, so an exhausted list binds the declared default *unchecked* and a leading
  option binds nothing and checks nothing; suffix parameters carry no allowed-value declaration and are
  never checked.
  (OUT-OF-REPO: `src/Xcaciv.Command.Core/CommandParameters.cs:76-80,91-103,113-130`, framework v2.1.2)

- **GR-23** — **QUIRK. OUT-OF-REPO only.** Parameter **names** shall be normalised to lower case (and
  stripped of characters outside letters, digits, space, `-` and `_`) when the declaration is read, and
  the bound map is keyed by those lower-cased names — but the **token match against a name is
  case-sensitive**. Therefore `-VERBOSITY quiet` matches no declaration at all: both tokens are left
  unconsumed and the parameter silently falls back to its declared default, or raises
  `Missing required parameter <name>` when required with no default. Values, by contrast, are compared
  case-insensitively (GR-22), so `-verbosity QUIET` is accepted and binds the literal `QUIET` as typed.
  A declaration whose name contains upper-case letters is unreachable in the form its author wrote it.
  This asymmetry — **names case-sensitive, values case-insensitive** — is the mechanism behind the
  silent detail-level downgrade in §7.8: a value passes the allow-list gate case-insensitively and is
  then matched case-sensitively by the Command, which falls through to its standard rendering with no
  message. That fallback branch is **not** dead code; it is the branch that hides the defect. Register
  rows: Q-60 for the name/value asymmetry itself, Q-2 for the §7.8 downgrade it produces.
  (OUT-OF-REPO: `src/Xcaciv.Command.Core/CommandParameters.cs:17-19,43-45,52,76-77`,
  `src/Xcaciv.Command.Interface/Attributes/AbstractCommandParameter.cs:19-23`, framework v2.1.2;
  consuming site `src/Xcaciv.Command.Packages/SearchCommand.cs:16,63-83`)

- **GR-24** — **OUT-OF-REPO only.** Binding failures shall surface as ordinary Command failures, not as
  a distinct diagnostic class: the exact texts `Missing required parameter <name>` and
  `Invalid value for parameter <name>, this parameter has an allow list.` reach the user only as the
  `**Error: <message>` Status line of GR-38, behind the generic Output line
  `Error executing <NAME> (see trace for more info)`.
  (OUT-OF-REPO: `src/Xcaciv.Command.Core/CommandParameters.cs:72,79,100,117,125,148,163`,
  `src/Xcaciv.Command/CommandExecutor.cs:228-229`, framework v2.1.2)

---

### 6.3 Session Variable semantics

- **GR-25** — **OUT-OF-REPO only.** Session Variable keys shall be normalised to **upper case on both
  read and write**, making the store case-insensitive. A value written as `PackageSourceUrl` is stored
  and listed as `PACKAGESOURCEURL` and is retrievable under any casing.
  (OUT-OF-REPO: `src/Xcaciv.Command/EnvironmentContext.cs:71-74,94-97`, framework v2.1.2)

- **GR-26** — **QUIRK. OUT-OF-REPO only.** A read of a **missing** key shall, by default, **write the
  supplied fallback into the store under that key** and mark the scope changed. The contract has no
  raise-if-absent read at all; a caller wanting a non-mutating read must opt out, and nothing in the
  product does. **How far that write travels is decided by GR-27, not by this rule.** A read creates
  the entry **in the reading Command's own child scope**; whether it survives the Command's return
  depends on the Command's Command Registration Record. For a Command **not** declared
  environment-modifying — which includes the Package Search Host-linked Command, registered with the
  flag left at its default, and the echo Built-in Command, which performs this read for every `%NAME%`
  it fails to resolve — the entry is discarded with the child and **no later variable dump lists it**.
  The side effect is observable only in two places: inside a Command that *is* declared
  environment-modifying, and on a **direct-invocation** path where a host passes its own Session
  Variable store straight to the Command with no child scoping — which is exactly what the
  repository's own tests do. The mutating-read rule itself is inherited product-wide and a read shall
  not be assumed side-effect-free anywhere: it is a live trap for any clone that registers Package
  Search, the echo Built-in, or any other reading Command as environment-modifying, because the
  invisible write becomes a visible one with no other change. Register row: Q-10.
  (OUT-OF-REPO: `src/Xcaciv.Command/EnvironmentContext.cs:94-110`, seeding and write-back at
  `src/Xcaciv.Command/CommandExecutor.cs:187,216-219`, flag default at
  `src/Xcaciv.Command/CommandController.cs:190`,
  `src/Xcaciv.Command/Commands/SayCommand.cs:33-48`, framework v2.1.2; consuming sites
  `src/Xcaciv.Command.Packages/SearchCommand.cs:24` and `src/Xcaciv.Cupcake.Lit/Program.cs:11`;
  direct-invocation observation at `Xcaciv.Command.PackagesTests/SearchCommandTests.cs:15,31,90`)

- **GR-27** — **OUT-OF-REPO only.** Every Command shall execute against a **child scope seeded with a
  copy** of the caller's values, not a view of them. Values shall flow back to the caller's scope only
  when the Command's Command Registration Record declares it as changing Session Variables **and** the
  child reports having changed. The flag defaults to **off** at every registration entry point, so a
  Command is non-modifying unless its registrar deliberately says otherwise; in the shipping Shell
  only one Built-in Command — the variable setter — is registered with it. Everything a
  non-modifying Command writes, including the mutating reads of GR-26, is therefore discarded when it
  returns. **QUIRK:** the seeding copy is performed through the same write path that sets the changed
  marker, so any child of a non-empty store is born "changed" — the second half of the guard is
  therefore effectively inert and the declared flag is the only real gate; the child also never
  records its parent, although the contract declares a lineage field for it. Register rows: Q-67 for
  the meaningless change flag, Q-62 for where the flag is stamped (GR-28).
  (OUT-OF-REPO: child seeding at `src/Xcaciv.Command/CommandExecutor.cs:187`, write-back gate at
  `:216-219`, flag defaulting to off at `src/Xcaciv.Command/CommandController.cs:190,198` and the one
  Built-in registered with it at `:183`, unrecorded lineage field and change marker at
  `src/Xcaciv.Command/EnvironmentContext.cs:31,35-38,45-55,84`, framework v2.1.2)

- **GR-28** — **QUIRK. OUT-OF-REPO only.** The "may change Session Variables" flag shall be recorded on
  the **Command Group's** Command Registration Record, not on the member's. Registering a grouped
  Command builds a group record with the member nested inside it and stamps the flag onto the group;
  a second member joining an existing group is merged in and cannot change the flag. The **first**
  registration of a group therefore decides write-back for every member of it, and a group registered
  without the flag can never write back whatever its members declare. Latent in the product — neither
  Host-linked Command sets the flag, and the only Command that does is an ungrouped Built-in Command —
  but a clone that reproduces grouping must reproduce or deliberately fix this. Register row: Q-62.
  (OUT-OF-REPO: `src/Xcaciv.Command/CommandRegistry.cs:24-35,55-60`,
  `src/Xcaciv.Command.Core/CommandParameters.cs:179-209`,
  `src/Xcaciv.Command/CommandExecutor.cs:216`, framework v2.1.2)

- **GR-29** — Session Variables shall start **empty**, shall never be seeded from the host platform's
  own environment, and shall be discarded when the Session ends. There is no settings file, no profile,
  no export and no import anywhere in the product; the only in-product way to set one is the variable
  setter Built-in Command, and the only realistic way to set the Registry Endpoint is for a Shell
  Operator to seed it programmatically, because the tokenizer destroys addresses typed at the Prompt
  (GR-4). (`src/Xcaciv.Cupcake.Core/Loop.cs:26`; repo-wide census finds no settings file and no
  argument parsing; OUT-OF-REPO for the empty initial store:
  `src/Xcaciv.Command/EnvironmentContext.cs:18,33`, framework v2.1.2)

---

### 6.4 Output and diagnostics conventions

- **GR-30** — The product shall have exactly **four** presentation channels, and their pipe behaviour
  shall differ:

  | Channel | Carries | Pipeable? | Rendered as |
  |---|---|---|---|
  | **Output** | a Command's result chunks, and the Command Service's not-found / help / failure text | **Yes** — routed to the Stage Channel when one is attached, otherwise rendered | Blue on Black, one line, styling reset afterwards |
  | **Status line** | transient progress and state text, including the Command Service's `**Error: <message>` line and the Progress reading | **No** — always rendered, from any Interaction Context, subject only to Status Visibility | Yellow on DarkBlue, one line, styling reset afterwards |
  | **Diagnostic trace** | developer detail, including everything the product silently skips | **No** — see GR-33 | not rendered at all in practice |
  | **Prompt** | the Prompt text, followed by one line read from the user | **No** | Green on Black, **no** trailing line break, **no** styling reset |

  (`src/Xcaciv.Cupcake.Core/ConsoleContext.cs:23-30,53-60,66-72,90-103`; OUT-OF-REPO for routing and
  trace: `src/Xcaciv.Command.Core/AbstractTextIo.cs:66-74,167-176`, framework v2.1.2)

- **GR-31** — **OUT-OF-REPO only.** Output shall be rendered **only when no Stage Channel is
  attached**; with one attached the chunk is written to the channel and never rendered. **Empty Output
  shall be dropped** before it reaches the channel or the renderer, so a Command that returns nothing
  produces no line at all and is indistinguishable from a Command that was never run.
  (OUT-OF-REPO: `src/Xcaciv.Command.Core/AbstractTextIo.cs:66-74`,
  `src/Xcaciv.Command/CommandExecutor.cs:196-200`, framework v2.1.2)

- **GR-32** — **Status Visibility shall gate the Status line and nothing else.** Output, the Prompt and
  Diagnostic trace are unaffected by it. When Status Visibility is off, Status text is written to a
  debug sink and the operation returns without touching the terminal. **QUIRK:** that sink is stripped
  from optimised builds, so in a shipping build a suppressed Status line is recorded **nowhere at all**
  — and because the Command Service puts a failure's **actual message** on the Status line while
  Output carries only the generic `Error executing <NAME> (see trace for more info)` line (GR-38),
  turning Status Visibility off leaves the user knowing **that** a Command failed and nothing about
  **why**, with the detail recorded nowhere in a shipping build. Output itself is never gated: the
  renderer for Output has no visibility test at all. Status Visibility defaults to on, and the product
  never changes it. **QUIRK:** child Interaction Contexts do not inherit it — a child is constructed
  without the setting and reverts to the default — so a Session made quiet becomes chatty again inside
  every Command execution and every Pipeline stage. Register rows: Q-32 for the vanished detail,
  Q-28 for the non-inheriting child.
  (`src/Xcaciv.Cupcake.Core/ConsoleContext.cs:13,31,38-47,90-103`, ungated Output renderer at
  `:53-60`; OUT-OF-REPO for the generic Output line and the failure Status line:
  `src/Xcaciv.Command/CommandExecutor.cs:228-229`, framework v2.1.2)

- **GR-33** — **QUIRK. Diagnostic trace shall be effectively invisible.** The trace router consults the
  **inherited** visibility flag, which defaults to off; the Presentation Adapter declares its own,
  separate visibility flag that **shadows** rather than sets it, and never assigns the inherited one.
  Trace text therefore never reaches the terminal however the Shell is configured — it goes to the
  platform diagnostic sink, where nothing in the product ever listens (no trace log file is ever
  attached). Had the inherited flag been on, trace lines would have been emitted through the **Output**
  channel prefixed with a tab character then the literal `TRACE: `; a clone enabling that path must
  preserve the prefix. The consequence is product-wide and load-bearing: every
  `see trace for more info` message points at a channel the user cannot see, and every silently skipped
  Plugin, rejected Plugin Directory and displaced Command name is recorded only there. Register row:
  Q-27.
  (`src/Xcaciv.Cupcake.Core/ConsoleContext.cs:31`; OUT-OF-REPO:
  `src/Xcaciv.Command.Core/AbstractTextIo.cs:25,157-176`, framework v2.1.2)

- **GR-34** — Every message described in this document shall reach the terminal **verbatim** — no
  escaping, quoting, wrapping, trimming, truncation or width limit is applied on the presentation path.
  **QUIRK:** an output-encoder hook exists in the contract and is documented there as being applied to
  every chunk before it is displayed or piped, but the shared implementation **accepts and discards**
  it and the Presentation Adapter does not override that; the product also leaves the encoder at its
  default no-op. There is consequently **no point in the product where untrusted remote text is
  neutralised before it is written to the terminal** — Package Listing summaries, author strings and
  licence text arrive from the Package Registry and are rendered raw, control characters and terminal
  escape sequences included. Register row: Q-33.
  (`src/Xcaciv.Cupcake.Core/ConsoleContext.cs:53-60`; OUT-OF-REPO:
  `src/Xcaciv.Command.Core/AbstractTextIo.cs:121-125`,
  `src/Xcaciv.Command.Interface/IIoContext.cs:166-171`,
  `src/Xcaciv.Command/CommandController.cs:108,230`, framework v2.1.2)

- **GR-35** — Channel identity shall be conveyed by **colour alone**. There is no prefix, symbol,
  indentation or severity marker distinguishing Output from a Status line from the Prompt; the only
  textual marker anywhere in the design is the unreachable trace prefix of GR-33 and the `**Error: `
  prefix the Command Service puts on failure Status lines. On a monochrome terminal, with output
  redirected, or for a colour-blind or screen-reader user, the three channels are indistinguishable.
  A clone aiming at parity keeps the colours; one aiming at accessibility adds textual markers and
  should record that as a deliberate divergence.
  (`src/Xcaciv.Cupcake.Core/ConsoleContext.cs:23-30,53-60,90-103`)

- **GR-36** — **QUIRK.** **All error and crash text shall be written to standard output; the product
  shall never write to an error stream.** This holds for the startup guidance line, for every
  per-Command failure line and Status line, and for the process-level crash line. A caller redirecting
  standard output therefore swallows every diagnostic the product produces, and a caller reading
  standard output receives failure text interleaved with results. The **only** machine-readable failure
  signal is the process exit status (GR-44). Register row: Q-38.
  (`src/Xcaciv.Cupcake.Lit/Program.cs:16`; `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:53-60,90-103`;
  repo-wide search for an error-stream write returns no production hits)

---

### 6.5 Error conventions

- **GR-37** — **The Session shall install no guard of its own around Command dispatch or around the
  Prompt read.** Everything that keeps a Session alive across a failing Command is the Command
  Service's per-Command absorption (GR-38); everything the Command Service does not absorb —
  a Pipeline quoting failure (GR-7), a cancellation, an argument the Command Service rejects
  outright — escapes the whole Session, reaches the process guard, and **ends the process**, losing the
  Session and every Session Variable set during it. One mistyped quote costs the user their Session.
  Register rows: Q-41 for the absent guard, Q-42 for the unbalanced quote that exploits it.
  (`src/Xcaciv.Cupcake.Core/Loop.cs:56-66,89-101`; `src/Xcaciv.Cupcake.Lit/Program.cs:14-19`)

- **GR-38** — **OUT-OF-REPO only.** Any failure raised **inside** a Command — including every binding
  failure of §6.2 — shall be absorbed by the Command Service, which emits, in this order: the Output
  line `Error executing <NAME> (see trace for more info)`, then the Status line `**Error: <message>`,
  then the full detail on the Diagnostic trace; the Session then continues and prompts again. `<NAME>`
  is the resolved registry key, which for a grouped Command is the **Command Group name**, not the
  member the user typed. The advertised trace detail is invisible (GR-33).
  (OUT-OF-REPO: `src/Xcaciv.Command/CommandExecutor.cs:222-231`, framework v2.1.2)

- **GR-39** — **OUT-OF-REPO only.** An unresolved Command name shall produce the Output line
  `Command [NAME] not found. Try 'HELP'` — where `NAME` is the normalised, upper-cased name of GR-2 and
  `HELP` is the configurable help word at its default — plus the trace line
  `Command [NAME] not found.` without the trailing clause. The Session survives. A registered name whose
  Command Registration Record comes back empty produces `Command [NAME] not found.` on Output with no
  trailing clause.
  (OUT-OF-REPO: `src/Xcaciv.Command/CommandExecutor.cs:27,52-54,83-87`, framework v2.1.2)

- **GR-40** — **OUT-OF-REPO only.** A Command that reports a **failure result** rather than raising
  shall have its own failure text written to the **Output** channel — visually identical to ordinary
  results — or, when it supplied none, the substituted text
  `Command [NAME] reported failure (CorrelationId: <id>).` with a fresh identifier per run. Any attached
  detail goes to the Diagnostic trace. The Session survives.
  (OUT-OF-REPO: `src/Xcaciv.Command/CommandExecutor.cs:205-212`, framework v2.1.2)

- **GR-41** — Startup failures shall be **fatal and uniform**. The two Session entry points differ in
  **which failures they tolerate**, never in what they say. (They diverge in other ways too — which
  Commands they register, and one extra Status line — but those belong to §7.5 and register row Q-17;
  only the tolerance difference is a failure-contract rule.)
  - In the **blocking** entry point the guarded region covers three steps (register Built-in Commands,
    nominate the Plugin Directory, load Commands) and has **two** catches: No Plugins Available is
    caught and answered with the guidance line of GR-42; **any other** failure is replaced by a
    Startup Load Failure.
  - In the **background** entry point the guarded region covers only two steps (nominate the Plugin
    Directory, load Commands) and has **one** catch. There is **no tolerant branch**, so **every**
    failure — No Plugins Available included — is wrapped as a Startup Load Failure. A Shell embedded
    through that entry point therefore dies on the very condition the blocking one forgives.

  The Startup Load Failure's summary is the fixed, cause-independent literal `Unable to load commands.`
  — **identical in both entry points**, carrying no path, no Plugin name and no cause text. The
  underlying cause **shall be preserved structurally on the failure and never displayed**; nothing in
  the product reads it back. It escapes the Session, reaches the process guard, and ends the process
  with exit status 1 (GR-43, GR-44). A clone that helpfully prints the cause chain is better software
  and is **not** faithful; flag it as a deliberate divergence. Register row for the entry-point
  divergence: Q-17. (`src/Xcaciv.Cupcake.Core/Loop.cs:39-54` versus `:77-85`;
  `src/Xcaciv.Cupcake.Core/Exceptions/LoadingException.cs:6-13`; `src/Xcaciv.Cupcake.Lit/Program.cs:16`)

- **GR-41a** — **QUIRK.** The Command Service raises **two unrelated sibling conditions** for
  "nothing found", and the product's whole first-run experience turns on keeping them apart. A clone
  must not collapse them.
  - **"No verified Plugin Directory"** — raised when the list of directories that survived the
    Verified Directory rule is empty. This is the No Plugins Available condition of §4. The blocking
    entry point **catches** it and prints the guidance line of GR-42; the background entry point does
    not (GR-41).
  - **"The Plugin Directory holds no plugin binaries"** — a *different* condition, carrying the text
    `No packages found in <full absolute path>.`, raised by the Plugin Scanner after enumeration
    returns nothing. Neither entry point catches it, so it is fatal: `Error Unable to load commands.`
    and exit status 1.

  **As shipped, the second condition is unreachable** and the distinction is worse than merely
  inverted. The Plugin Scanner's search mask cannot match on a real filesystem (GR-50), so a verified
  Plugin Directory produces a **directory-not-found** condition — a third condition, sibling to
  neither, and caught by neither — before enumeration can report emptiness. The accurate product-wide
  rule is therefore: **any** Plugin Directory that survives verification is fatal, populated or empty
  alike; **only a missing one is tolerated**; and no Plugin can ever load. The friendlier outcome is
  the one where the user has done *less* setup. The two-signal design is nevertheless sound and a
  clone that repairs the mask meets both conditions immediately, so the keep-or-fix decision still has
  to be taken. Register rows: Q-11, Q-82, Q-83; see also §7.4 FR-4.42 and §7.6 FR-6.32.
  (`src/Xcaciv.Cupcake.Core/Loop.cs:45-54,82-85`; OUT-OF-REPO:
  `src/Xcaciv.Command/CommandLoader.cs:42-45` for the caught condition,
  `src/Xcaciv.Command.FileLoader/Crawler.cs:174,176,178,180` for the other two, framework v2.1.2)

- **GR-42** — The only remediation guidance the product ever gives shall be the single line
  ``No Plugins Found. You may want to check out `install --help` `` — backticks literal, no trailing
  period after the backtick — written on the **Output** channel, not as a Status line. Three
  consequences a clone must keep. It is rendered in Output colours. It is **not** suppressed when
  Status Visibility is off, unlike the startup Status lines beside it, so a Shell started quiet
  swallows `Loading Commands` and still prints the guidance — narration split across two channels
  (register row Q-76). And it is emitted **only** in the blocking entry point, and only on the "no
  verified Plugin Directory" branch (GR-41a) — which, as shipped, means only when the Plugin Directory
  does not exist, since any directory that does exist takes the fatal path instead (GR-50). The
  invocation it names cannot in fact resolve (register row Q-12), so the one piece of remediation
  advice the product offers is also wrong.
  (`src/Xcaciv.Cupcake.Core/Loop.cs:45-50`; `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:53-60,90-103`)

- **GR-43** — The process-level guard shall print exactly one line to standard output, formatted as the
  literal word `Error`, one space, then the failure's summary message, and nothing else: no colon, no
  severity token, no timestamp, no cause chain, no stack. It sets no colours of its own, so the line
  renders in whatever colours were last applied — after a Prompt, that is Prompt colours (GR-30). For
  a Startup Load Failure the summary is the fixed literal of GR-41, so the printed line is
  `Error Unable to load commands.` and the preserved cause is discarded unread. Register row: Q-39.
  (`src/Xcaciv.Cupcake.Lit/Program.cs:16`)

- **GR-44** — The process shall terminate with exit status **`1`** on any failure that reaches the
  process guard, immediately on printing. It is the only non-zero status the product produces — there
  is no taxonomy of failure codes. INFERRED: normal termination yields status `0`; no success-path exit
  status is written anywhere in the source. (`src/Xcaciv.Cupcake.Lit/Program.cs:18`;
  `src/Xcaciv.Cupcake.Core/Loop.cs:66-68`)

- **GR-45** — **QUIRK, INFERRED.** Because the blocking Session waits synchronously on asynchronous work
  for both Command dispatch and the Prompt read, a failure escaping either shall reach the process guard
  **re-wrapped in a generic multi-error envelope**, so the printed line reads
  `Error One or more errors occurred. (<real message>)` rather than the real message alone. The
  background Session entry point, which awaits rather than blocks, surfaces the original. The structural
  claim follows directly from the blocking calls; only the envelope's exact wording is inferred, because
  it is runtime behaviour rather than a source literal. A clone written synchronously will print a
  cleaner, different message — decide that deliberately. Register row: Q-40.
  (`src/Xcaciv.Cupcake.Core/Loop.cs:37,62,65` versus `:94,96`; `src/Xcaciv.Cupcake.Lit/Program.cs:16`)

- **GR-46** — **QUIRK.** The product shall **silently correct invalid values rather than reporting
  them**, and shall report absence as an empty result rather than as a failure. Observed instances, all
  product-wide in character: a Result limit outside its bounds is clamped with no message; a search term
  over its length cap is truncated with no message; a blank search term returns empty Output with no
  message and no request; a Package Registry query for a name that exists nowhere returns an empty list,
  not an error. A user cannot tell that what they asked for was changed, and cannot distinguish
  "no matches" from "something went wrong" — and, because every Package Registry Client call site
  passes the discard sink (GR-63), cannot distinguish either of those from a Registry outage. A clone
  that starts reporting these is more helpful and less faithful; flag it as a deliberate divergence,
  and take the decision together with the zero-results decision, because "emit an explicit no matches"
  is unimplementable until the outage case is settled too. Register rows: Q-73 for the silent
  corrections, Q-7 for the silent zero-result, Q-75 for the discarded Registry diagnostics.
  (`src/Xcaciv.Command.Packages/SearchCommand.cs:46,50-58`;
  `src/Xcaciv.Command.Packages/NugetWrapper.cs:26-31,34-50`)

- **GR-47** — **QUIRK.** Work the product does not perform, and operations it does not support, shall be
  announced as **ordinary Output**, never as failures, and shall not affect the exit status. Package
  Install answers `Not installing ` plus its comma-joined arguments; Package Search fed from a Pipeline
  answers `Unsupported search method for <chunk> (piped)` immediately followed by its comma-joined
  arguments, with **no separator at all** — so the first argument runs straight into the closing
  parenthesis. A caller cannot distinguish "did nothing" from "succeeded". Register row: Q-74, whose
  disposition is to route refusals to the failure channel *and* add the missing separator.
  (`src/Xcaciv.Command.Packages/InstallCommand.cs:19-27`;
  `src/Xcaciv.Command.Packages/SearchCommand.cs:88-91`)

---

### 6.6 Plugin trust and isolation

- **GR-48** — **QUIRK. INFERRED. OUT-OF-REPO only.** A candidate Plugin Directory shall be canonicalised to an
  absolute path, and **its parent** shall be tested for containment against a boundary that defaults to
  the process working directory, using a containment test that **ignores the boundary's own final path
  segment**. The Shell never sets a boundary of its own and exposes no way to set one, so the default
  always applies. The net effect is that **the effective boundary is one level above the working
  directory**: arbitrarily nested sub-paths are accepted, **sibling directory trees are accepted**, and
  **the working directory itself is rejected** as a Plugin Directory. Worked outcomes, with a working
  directory of `C:\a\work`: `C:\a\work\packages` accepted; `C:\a\work\x\y\packages` accepted;
  `C:\a\evil\packages` accepted; `C:\a\work` rejected; `C:\a\packages` rejected; `C:\b\evil\packages`
  rejected; `D:\evil\packages` rejected. With a working directory of `C:\app`, `C:\Windows\System32` is
  accepted. The containment primitive's behaviour was confirmed by executing it against these exact path
  shapes; **the product itself was never run**. A clone must not restate this as "the Plugin Directory
  must be inside the working directory" — that is the rule the code appears to intend and is not the
  rule it implements. Treat the containment layer as "same volume, roughly the same neighbourhood", not
  as a sandbox. Register row: Q-14.
  (OUT-OF-REPO: `src/Xcaciv.Command.FileLoader/VerifiedSourceDirectories.cs:17,66-89,100-115`,
  parent-of at `:103`, boundary comparison at `:105-108`, framework v2.1.2;
  `src/Xcaciv.Cupcake.Core/Loop.cs:24,42,79`)

- **GR-49** — **QUIRK. OUT-OF-REPO only.** A Plugin Directory that fails verification — because it does not
  exist, is not a directory, or fails GR-48 — shall be **silently dropped**: the registration call
  simply reports failure and the Session discards the answer. No message, no trace of which path was
  tried, and no distinction between "missing" and "rejected by the boundary" ever reaches the user; the
  only symptom is the eventual No Plugins Available guidance — and only in the blocking entry point,
  since the background one wraps that condition as fatal too (GR-41). A populated, readable Plugin
  Directory elsewhere on disk therefore looks exactly like an empty installation. Note the perverse
  incentive this creates once GR-50 is taken into account: because a directory that *passes*
  verification is fatal at startup and a directory that fails it is merely dropped, silent rejection is
  the outcome that leaves the Shell usable. The guidance line is the **only** path to a Prompt, and a
  blank or whitespace-only configured Plugin Directory does not even reach it — nomination rejects that
  by raising, and the raise is not the tolerated kind, so it is fatal (register row Q-80).
  (OUT-OF-REPO: `src/Xcaciv.Command.FileLoader/VerifiedSourceDirectories.cs:66-89`,
  blank-directory raise at `src/Xcaciv.Command/CommandLoader.cs:26-30`, framework v2.1.2;
  `src/Xcaciv.Cupcake.Core/Loop.cs:42,79`)

- **GR-50** — **QUIRK. Established by execution; the scanner's own evidence is OUT-OF-REPO.** The
  Plugin Scanner shall search each verified Plugin Directory recursively for the layout
  `<root>/<any directory>/<sub-directory>/<binary>`, where the sub-directory name defaults to `bin` —
  the product passes no argument and therefore takes that default — and the binary pattern is the
  fixed literal `*.dll`, the managed-module extension on every platform, **not** a platform-derived
  shared-library extension. (A clone reading this as "the platform's shared library extension" would
  build a mask looking for `.so` or `.dylib` on a POSIX host; the product does not.) The sub-directory
  must sit **at least one level below the root**; depth beyond that is unconstrained. **Every** binary
  in a matching sub-directory is a candidate, including support libraries shipped beside the Plugin;
  the binary's file name need not match its directory's. Each surviving binary becomes one Plugin,
  whose internal key is its base file name joined by a hyphen to its path below the root with
  separators removed. Scanning switches from sequential to parallel **strictly above 50** discovered
  binaries.

  **That layout is specification, not observed behaviour: the scan cannot match anything on a real
  filesystem.** The three parts are joined into a *single* search mask — a wildcard segment, the
  sub-directory name, then the binary pattern, giving `*/bin/*.dll` — and handed to one recursive
  enumeration rooted at the verified Plugin Directory. The **directory portion of a search mask is
  joined to the root literally and its wildcard is never expanded**. Executed during verification
  against a real filesystem holding a correct layout (`<root>/HelloPkg/bin/Hello.dll` and
  `<root>/Other/bin/Other.dll`), the enumeration returned neither those files nor an empty set: it
  **raised a directory-not-found condition naming the literal path `<root>/*/bin`**. Both the recursive
  and the top-level enumeration modes were tested and both raise; a control run with the plain pattern
  `*.dll` recursively found both files, isolating the mask as the cause. Executed on the analysis host
  with a current runtime; the enumeration semantic is long-standing and INFERRED to be identical on the
  runtime the product targets. The product itself was still never run.

  Four consequences a clone must design around, all of them product-wide. **(1) No Plugin is ever
  loaded from disk** — the product's headline extensibility mechanism does not function as shipped, so
  everything §6.6 says about isolation (GR-51), silent skipping (GR-52) and displacement (GR-53) is
  reachable only through the Host-linked Commands, never through a Plugin. **(2) Any** verified Plugin
  Directory is fatal at startup, populated or empty alike, because the raise precedes any inspection
  (GR-41a). **(3)** The "no plugin binaries" signal (`No packages found in <path>.`) is unreachable on
  the shipping path, since it requires an empty sub-directory filter and the Shell always takes the
  `bin` default. **(4)** The only startup that reaches a usable Prompt is the one where the Plugin
  Directory does not exist — also the default state, because the compiled-in default uses a backslash
  separator that is an ordinary filename character on a POSIX host. The framework's own Scanner tests
  pass because they run against a mock filesystem that INFERRED expands the wildcard segment; the
  production path uses the real filesystem and cannot. A clone must **design** the discovery rule
  rather than port it. Register rows: Q-82, Q-83, Q-11, Q-25.
  (OUT-OF-REPO: `src/Xcaciv.Command.FileLoader/Crawler.cs:18,20,171-190,203-213`, the fixed pattern at
  `:20`, the single mask composed at `:176` and enumerated at `:178`,
  `src/Xcaciv.Command/CommandController.cs:169`,
  `src/Xcaciv.Command.Interface/ICommandController.cs:29`, framework v2.1.2;
  `src/Xcaciv.Cupcake.Core/Loop.cs:24,43,80`)

- **GR-51** — **OUT-OF-REPO only.** Each Plugin binary shall be inspected **and later instantiated**
  inside its **own isolated load context whose file-access restriction is that binary's own
  directory** — no parent, no sibling Plugin, nowhere else on disk. The restriction is applied twice:
  once during discovery and again when a Command is constructed for execution. The default policy is the
  strict one, base-path restriction is enforced, dynamic code emission is disabled by default, and when
  emission is disabled the strict policy is **forced regardless of any other configuration**. The
  product configures none of this and accepts every default. A clone on a platform without module
  isolation must substitute process-level isolation or an out-of-process Plugin protocol; it must not
  silently drop the restriction. Latent as shipped — the scan never delivers a binary to isolate
  (GR-50) — which makes this rule pure specification today and load-bearing the moment the scan is
  repaired; it must not be dropped on the grounds that nothing exercises it.
  (OUT-OF-REPO: `src/Xcaciv.Command.FileLoader/Crawler.cs:31,83-91`,
  `src/Xcaciv.Command/CommandFactory.cs:83-101`,
  `src/Xcaciv.Command/AssemblySecurityConfiguration.cs:16-36`, framework v2.1.2;
  `src/Xcaciv.Cupcake.Core/Loop.cs:41-43`)

- **GR-52** — **QUIRK. OUT-OF-REPO only.** A Plugin that fails to load shall be **silently skipped**, not
  fatal. Every per-Plugin failure — a security-policy violation, a missing file, an unreadable or
  wrong-architecture binary, a malformed Command declaration, or any other error — is caught, written to
  the Diagnostic trace, and that Plugin skipped; the rest of the scan continues. A Plugin contributing
  no valid Commands is dropped from the registry entirely. Nothing reaches the Session and **the user is
  told nothing**. Combined with GR-33, the record of a skipped Plugin exists on a channel nobody can
  read: silently skipping executable code the product was asked to load is a security-relevant absence,
  not a convenience. Latent as shipped — GR-50 means no Plugin binary is ever reached — but it is the
  first rule a clone that repairs the scan will exercise. Register row: Q-15.
  (OUT-OF-REPO: `src/Xcaciv.Command.FileLoader/Crawler.cs:116-119,125-156`, framework v2.1.2)

- **GR-53** — **QUIRK. OUT-OF-REPO only.** Command registration shall be **last-write-wins by name**, with one
  exception: when the incoming record carries members and a record of that name already exists, the
  members are **merged into** the existing record rather than replacing it — which is how two Commands
  declaring the same Command Group become one entry with two members. A Plugin that declares a name
  already held by a Built-in Command or a Host-linked Command therefore **displaces it with no warning,
  no trace line and no user-visible notice** — silently re-pointing an existing verb at Plugin code is
  a supply-chain gap, not a convenience, and it is the seventh of §7.4's keep-or-fix decisions.
  Re-registration of the same name is consequently idempotent, which is the only reason the product's
  double registration of Built-in Commands is harmless; a clone whose registry rejects duplicates
  breaks that path. Register rows: Q-71 for the silent displacement, Q-16 for the double registration
  that idempotence renders harmless. Latent as shipped — for the reason given in GR-50, no Plugin ever
  reaches the registry — and live the moment the scan is repaired.
  (OUT-OF-REPO: `src/Xcaciv.Command/CommandRegistry.cs:20-36`, framework v2.1.2;
  `src/Xcaciv.Cupcake.Core/Loop.cs:41,108`)

- **GR-54** — **OUT-OF-REPO only.** A Command shall be **constructed fresh for every execution**. When a
  Command is registered by instance — as the Lit Shell does for both Host-linked Commands — only the
  instance's type identity is retained and the instance itself is discarded. No Command instance is ever
  cached or reused, so a Command carries no state between invocations and a clone must not rely on one:
  state placed on a registered object does not survive registration, let alone execution. Register
  row: Q-61.
  (OUT-OF-REPO: `src/Xcaciv.Command/CommandRegistry.cs:63-67`,
  `src/Xcaciv.Command/CommandFactory.cs:38-56`, framework v2.1.2;
  `src/Xcaciv.Cupcake.Lit/Program.cs:10-11`)

- **GR-55** — **A Plugin shall be trusted on the strength of its location alone.** No signature check, no
  hash or checksum comparison, no publisher identity check, no trusted-publisher list, no first-use
  prompt and no policy file exists anywhere in the product, at any point between a Package Archive
  arriving from a Package Registry and the code inside it being loaded and executed in-process. Publisher
  and known-vulnerability data *are* fetched and displayed at the highest Detail level and then
  discarded; nothing in the product acts on them. A history note a clone team should have: three
  host-configurable settings — certificate validation, Package Archive signature verification, and an
  allowed-thumbprint list — were added and then removed as unused; **no enforcement code for them ever
  existed**. Do not ship them as inert settings; that is exactly what was removed here.
  (`src/Xcaciv.Command.Packages/NugetWrapper.cs:81-128`;
  `src/Xcaciv.Command.Packages/SearchCommand.cs:72-77`; `src/Xcaciv.Cupcake.Core/Loop.cs:6-26` — the
  settings are absent at the pinned commit; added in commit `b0ca736`, removed in commit `907c535`
  "Remove unused settings from Loop class and tests")

---

### 6.7 Absence requirements — what the product deliberately or accidentally does *not* do

Stated so that a reimplementer does not invent them. Each is an **observed absence**, established by
repo-wide search, not an oversight in this document. A clone may add any of them, but must record the
addition as a deliberate divergence.

- **GR-56** — **No authentication, authorization or identity of any kind.** There is no user model, no
  role, no credential store, no API key, no per-source credential, no elevation check, no
  multi-tenancy, and no permission check before any action. Every line typed at the Prompt is
  dispatched. The Package Registry is queried **anonymously**; only anonymous public registries work.
  The only authorization-shaped concepts in the whole product are the write-back flag of GR-27, the
  containment boundary of GR-48, and the per-Plugin isolation of GR-51.
  (`src/Xcaciv.Command.Packages/NugetWrapper.cs:20-128`; `src/Xcaciv.Cupcake.Core/Loop.cs:1-113`;
  `src/Xcaciv.Cupcake.Lit/Program.cs:1-22`)

- **GR-57** — **No persistence of anything.** Nothing survives a Session: no settings file, no user
  profile, no Session Variable store on disk, no install ledger, no trust store, no allow-list file, no
  error log, no correlation identifier of the product's own, and no error history. The command registry
  is in-memory and is never pruned or re-scanned. There is no configuration file, no command-line switch
  and no host-environment import at startup — the shipping program never reads its own argument list, so
  text typed after the program name is ignored entirely. (`src/Xcaciv.Cupcake.Lit/Program.cs:7-19`;
  repo-wide census of the 27 tracked files finds no settings file, no CI definition and no logging
  dependency)

- **GR-58** — **No command history, completion, or line editing.** The Session holds exactly one line of
  text and overwrites it on the next read, so the previous line is unrecoverable. Whatever editing the
  user gets is whatever the host terminal provides for reading one line.
  (`src/Xcaciv.Cupcake.Core/Loop.cs:56,65,90,96`; `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:66-72`)

- **GR-59** — **No job control and no background execution.** Dispatch is strictly serial and fully
  awaited: the next Prompt is not displayed until the previous Command line has completely finished.
  There is no way to background a Command, interleave two Commands, suspend, resume, or list running
  work. The background Session entry point moves the same serial loop onto a worker; it introduces no
  parallelism. The one place concurrency exists is inside a single Pipeline, whose stages all run at
  once (GR-8) — and the Presentation Adapter is **not** thread-safe, although the contract it implements
  requires it to be: painting is a non-atomic set-colours/write/reset sequence against process-global
  terminal state, so two concurrent stages can interleave lines and bleed colours (register row Q-35).
  (`src/Xcaciv.Cupcake.Core/Loop.cs:62-65,94-96`; `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:53-60`;
  OUT-OF-REPO: `src/Xcaciv.Command.Interface/IIoContext.cs:21-23`,
  `src/Xcaciv.Command/PipelineExecutor.cs:41-63,103`, framework v2.1.2)

- **GR-60** — **No timeouts anywhere.** Waiting for input is unbounded; waiting for a Command to finish
  is unbounded; the outbound Package Registry call has no deadline; there is no idle timeout, no maximum
  Session length, no retry, no back-off and no rate limiting. The Command Service offers per-stage and
  whole-Pipeline timeouts and output caps, and all of them default to "no limit" and are never
  configured (GR-9). A hung Command hangs the Session forever, and a hostile or slow Package Registry
  hangs it with it.
  (`src/Xcaciv.Cupcake.Core/Loop.cs:62,65,94,96`; `src/Xcaciv.Command.Packages/SearchCommand.cs:60`;
  `src/Xcaciv.Command.Packages/NugetWrapper.cs:24,29`; OUT-OF-REPO:
  `src/Xcaciv.Command.Interface/PipelineConfiguration.cs:29-50`, framework v2.1.2)

- **GR-61** — **No cancellation is wired up, anywhere.** The Command Service accepts a cancellation
  signal and the Session never supplies one; the Package Registry Client's operations default to
  "never cancelled" and only two of its six operations — search and dependency resolution — expose the
  parameter at all, with download and version enumeration hard-wiring it to "never cancelled" and the
  archive-metadata read taking no cancellation parameter of any kind, being synchronous. No interrupt
  handler is installed: a terminal interrupt is handled by whatever the runtime does by default, and
  teardown is whatever the runtime performs. There is no way for a user to abandon a running Command.
  (`src/Xcaciv.Cupcake.Core/Loop.cs:62,94`;
  `src/Xcaciv.Command.Packages/NugetWrapper.cs:24,46,56,84,103-109`;
  absence of any interrupt handling across `src/Xcaciv.Cupcake.Core/Loop.cs:1-113` and
  `src/Xcaciv.Cupcake.Lit/Program.cs:1-22`)

- **GR-62** — **No integrity or provenance verification of downloaded code, and no transactional
  install.** Restating GR-55 as the download-path absences a clone must design for: the transfer writes
  the response body straight to the target path in create-or-truncate mode, **overwriting whatever is
  there** with no existence check, no prompt, no backup, no temporary path and no atomic move; it
  reports success **unconditionally**, so a truncated or substituted archive is indistinguishable from a
  complete one; the Package Identity used to build the destination directory is then re-read **from
  inside the downloaded archive**, not from the identity that was requested, and a mismatch is neither
  detected nor reported; extraction is unimplemented, so **no** entry-name sanitisation, path-traversal
  rejection, absolute-path or link rejection, per-entry size cap or entry-count cap exists anywhere in
  the product; and there is no staging area and no cleanup of partial state on failure. Note also that
  the transport rule requiring an absolute encrypted-transport address is **not** a shared control — it
  lives inside the Package Search Command's own body, so the version-enumeration path builds a registry
  handle from a supplied address with no check at all.
  (`src/Xcaciv.Command.Packages/NugetWrapper.cs:39-40,81-101,103-109,111-128`;
  `src/Xcaciv.Command.Packages/SearchCommand.cs:32`)

- **GR-63** — **No observability.** There is no log file, no structured event, no metrics, and no audit
  sink installed — the Command Service offers an audit capability that records one event per Command
  execution and one per Session Variable change, and the product installs none, so every audit record is
  emitted into a discarding default — and the redaction policy that audit capability offers is dormant
  in any case: the executor hands the audit record the **raw** token list, and the masking a structured
  sink would apply covers only tokens shaped `-name=value`, never the two-token `-name value` form the
  binder actually reads (GR-20), so a secret typed as a Named Parameter would reach an audit record
  unmasked (register row Q-70). The Package Registry Client accepts a diagnostic sink at every entry
  point and **every call site in the product passes the discard sink**, so a Registry outage, a feed
  error and a genuine zero-result
  search are indistinguishable to the user; a supply-chain client that cannot report transport failure
  has no failure signal at all (register row Q-75). There is no unhandled-failure hook and no
  unobserved-background-work hook: a failure on a background worker is not covered by the process
  guard. A calling script can learn *that* the product failed (GR-44) and nothing about *why*, because
  the single most informative artefact — the preserved cause — is discarded unread (GR-41).
  (`src/Xcaciv.Command.Packages/NugetWrapper.cs:23,46,55,83`; `src/Xcaciv.Cupcake.Lit/Program.cs:4,7-19`;
  `src/Xcaciv.Cupcake.Core/Loop.cs:1-113`; OUT-OF-REPO for the unused capability:
  `src/Xcaciv.Command.Interface/IAuditLogger.cs:9-20,51-54`,
  `src/Xcaciv.Command/CommandExecutor.cs:241-256`, raw token list recorded at `:248`,
  dormant redaction at `src/Xcaciv.Command.Interface/AuditMaskingConfiguration.cs:94-118`,
  `src/Xcaciv.Command/StructuredAuditLogger.cs:41,55`, framework v2.1.2)

- **GR-64** — **QUIRK. No end-of-input concept.** The Prompt read maps an exhausted input stream to the **empty
  string**, which the Session treats as a blank line, so a caller redirecting a finite input stream into
  the product without a terminating Exit-vocabulary word gets an **unbounded Prompt loop that never
  exits, produces no output and never terminates**. There is no way for a caller to distinguish "the
  user pressed Enter on a blank line" from "there is no more input". This is a hang, not a quirk of
  presentation. Register row: Q-22.
  (`src/Xcaciv.Cupcake.Core/ConsoleContext.cs:71`; `src/Xcaciv.Cupcake.Core/Loop.cs:57-66,91-100`)

- **GR-65** — **No reload, no rescan, no file watching, and no pagination.** Plugin discovery is
  attempted exactly once per process launch; a newly installed Plugin would be visible only after
  restarting the Shell, and there is no reload Command. (As shipped the attempt never yields a Plugin
  at all and is fatal wherever a Plugin Directory exists — GR-50, GR-41a — so restarting does not help
  either; the absence of a reload path is nonetheless a real design absence a clone inherits.)
  Nothing anywhere in the product pages output: help listings, Session
  Variable dumps and Pipeline output are emitted in full, and the one page-size-like setting — the
  Result limit — always requests a single page at offset zero, so results beyond its ceiling are
  unreachable by any configuration.
  (`src/Xcaciv.Cupcake.Core/Loop.cs:41-43,79-80`; `src/Xcaciv.Command.Packages/NugetWrapper.cs:29`)

- **GR-66** — **No internationalisation and no localisation.** Every fixed string — the Prompt, the Exit
  vocabulary, the startup Status lines, the guidance line, the failure summary, the crash prefix, the
  Progress reading template and every message the Command Service emits — is a hard-coded English
  literal inline at its use site. There is no resource file anywhere in the repository, no message
  catalogue, no formatting indirection and no locale handling. One non-ASCII character exists: the
  Prompt's leading glyph, U+0190 LATIN CAPITAL LETTER OPEN E, which requires a terminal and font that
  can render that exact code point — it is easily mis-named as a reversed epsilon or a Greek letter,
  both of which are different code points, and substituting one changes what the user sees.
  (`src/Xcaciv.Cupcake.Core/Loop.cs:16,37,47,53,75,84,87`;
  `src/Xcaciv.Cupcake.Core/ConsoleContext.cs:20`; `src/Xcaciv.Cupcake.Lit/Program.cs:16`; repo-wide
  search for resource files returns zero)

---

### Source notes

*Product terms.* Everything above is written in the product vocabulary of §4. The evidence citations
are file paths from the two source trees and are the only place source-language names appear.

*In-repo evidence* — the subject repository at commit
`a05dc6f00d4510ac073f6dd02fbfba2ffb76a5b6`, paths repo-relative.

*Out-of-repo evidence* — the external command framework `Xcaciv.Command` / `Xcaciv.Command.Core` /
`Xcaciv.Command.Interface`, read from its public repository at tag `v2.1.2`, commit
`f34dedca8dc6d690290b2139acbd3e9b8264349c`. That tag is **one patch release ahead** of the versions the
product pins (2.1.1 / 2.1.0 / 2.1.0), and the pinned artefacts could not be fetched. Requirements
resting **only** on this evidence are marked **OUT-OF-REPO only** at the point of claim: GR-2, GR-3,
GR-4, GR-5, GR-6, GR-7, GR-8, GR-9, GR-10, GR-11, GR-12, GR-13, GR-14, GR-15, GR-16, GR-17, GR-18,
GR-19, GR-20, GR-21, GR-22, GR-23, GR-24, GR-25, GR-26, GR-27, GR-28, GR-31, GR-38, GR-39, GR-40,
GR-48, GR-49, GR-51, GR-52, GR-53 and GR-54. Where such a requirement also carries an in-repo
citation, that citation is a **consuming site** — where the product exercises the rule — not
independent evidence for the rule itself. The remainder rest on in-repo evidence, or on both. Two
requirements rest on a third kind of evidence and are marked separately: **GR-50** and **GR-41a**,
whose scan *rule* is out-of-repo but whose decisive consequence — that the mask raises rather than
matching — was **established by execution** (see below).

*Build state.* Two independent, established reasons the product does not build at the pinned commit,
and therefore could not be run to confirm any of the above. First, the Package Search Command's result
accumulator had its declaration deleted in commit `e1123b2` while five uses of the identifier remained;
HEAD still has five uses and no declaration, and no framework version defines such a member. Second,
dependency restore fails for **every** project: the feed that claims all first-party package names is an
unexpanded environment token and is treated as a non-existent relative directory; the private hosted feed
is declared but given no routing rule and so is never consulted; and the framework packages are absent
from the public index, which answers 404. Both are established. The **only** residual uncertainty is that
the exact pinned framework artefacts could not be fetched to confirm the base-class surface — which is
also why every `OUT-OF-REPO` citation above carries the version caveat.

*Executed, in a scratch directory outside the source tree, not by running the product:* the containment
primitive of GR-48 against the exact path shapes tabulated there; the truncating whole-number arithmetic
and the divide-by-zero behind the Progress reading referenced in §7.3; the shadowed visibility split of
GR-33, confirming that the Presentation Adapter's own flag reads on while the inherited flag reads off
simultaneously; and — added during verification, and the most consequential of the three — the
directory enumeration behind GR-50. A real directory tree was built holding a correct Plugin layout
(`<root>/HelloPkg/bin/Hello.dll`, `<root>/Other/bin/Other.dll`) and enumerated with the product's own
composed mask `*/bin/*.dll`. It returned neither file and did not return an empty set: it raised a
directory-not-found condition naming the literal path `<root>/*/bin`. Recursive and top-level
enumeration modes both raise. A control run with the plain pattern `*.dll` recursively found both
files, proving the layout correct and isolating the mask as the cause. Run on the analysis host with a
current runtime; the semantic is long-standing and INFERRED identical on the runtime the product
targets. GR-41a, GR-42, GR-49 and GR-50 are all written from this result. **The product itself was
still never run.**
