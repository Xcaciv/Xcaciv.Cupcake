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
