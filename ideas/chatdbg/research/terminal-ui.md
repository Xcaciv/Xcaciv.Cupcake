# Terminal user interface and rich console rendering

Research date: **2026-08-28/29**. Every version number, publish date and capability claim below was checked against nuget.org, the GitHub API, or a working build on this machine (SDK 10.0.400, linux-x64/WSL2). Claims marked **[verified locally]** were produced by compiling and running probe code against the actual released packages; the probe project lives at `/tmp/claude-1000/-mnt-g-3RD-Party-reversing/0ec1a1a1-b2b5-4e74-ab1f-585812e55090/scratchpad/probe`.

The single biggest thing a 2025-trained model gets wrong here: **Terminal.Gui v2 shipped GA on 2026-04-28** and is now at 2.4.17. It is not alpha, not beta, not "coming". Everything in the old "v1 is the only stable option" advice is dead.

---

## Bottom line — the recommendation in three sentences

Make **Spectre.Console 0.57.2** the app's only always-on renderer — every token heat map, token-card grid, log-prob table and chat turn is a `Spectre.Console.Rendering.IRenderable` produced by a pure function over the token model — because it is the only one of the two libraries that degrades correctly when stdout is a pipe or a file (Terminal.Gui v2 writes **literally zero bytes** to a redirected stdout **[verified locally]**), and because it costs exactly one transitive dependency.

Add **Terminal.Gui 2.4.17** (GA, .NET 10-only, MIT) as a *second, optional* front end used for bounded interactive episodes — the settings dialog, the system-prompt picker, and a scrollable full-screen token inspector — driven either by its new `AppModel.Inline` mode (renders into the primary scrollback buffer below the shell prompt, sizes itself to content, leaves its output in history — the Claude Code / Copilot CLI shape) or by a full-screen `--tui` mode the user opts into.

Never let the two libraries write to the terminal at the same time: use **`Terminal.Gui.Interop.Spectre` 2.4.17**'s `SpectreView`, which renders a Spectre `IRenderable` against a `TextWriter.Null` console and paints the resulting `Segment`s into Terminal.Gui's cell buffer, so exactly one library owns the escape-sequence stream at any instant — this is the precise bug the source application has today, and the bridge is the fix.

---

## Landscape — the real options

| Package | Latest stable | Published | Status | Verdict |
|---|---|---|---|---|
| [`Terminal.Gui`](https://www.nuget.org/packages/Terminal.Gui/) | **2.4.17** | 2026-07-07 | **GA** (v2.0.0 GA 2026-04-28) | Production-ready full-screen TUI. `net10.0` only. Take it — but as an optional mode, not the default. |
| `Terminal.Gui` 1.x | **1.19.0** | 2025-06-12 | **Maintenance / effectively end-of-life** | 14 months without a release; nuget page says "v1 is now in maintenance mode". Do not start new work on it. |
| [`Terminal.Gui.Interop.Spectre`](https://www.nuget.org/packages/Terminal.Gui.Interop.Spectre) | **2.4.17** | 2026-07-07 | GA but **very new and very unused** | First published 2026-05-29 (v2.4.3); 7.1K downloads total, 273 on current. 2 source files, ~9 KB. Works **[verified locally]** — but you are an early adopter. |
| [`Terminal.Gui.Templates`](https://www.nuget.org/packages/Terminal.gui.templates) | 2.4.17 | 2026-07-07 | GA | `dotnet new` templates. Convenience only. |
| [`Spectre.Console`](https://www.nuget.org/packages/spectre.console) | **0.57.2** | 2026-07-02 | **Stable-versioned, but still pre-1.0** | 55M downloads. The right default. Read the risk section on 0.x breaking changes. |
| [`Spectre.Console.Ansi`](https://www.nuget.org/packages/Spectre.Console.Ansi/) | 0.57.2 | 2026-07-02 | GA, new since 0.55.0 | Standalone ANSI writer, **zero dependencies**. Useful if you ever want colour without the widget engine. |
| [`Spectre.Console.Testing`](https://www.nuget.org/packages/Spectre.Console.Testing/) | 0.57.2 | 2026-07-02 | GA | `TestConsole`. Used by the .NET SDK and Cake. Take it. |
| [`Spectre.Console.Cli`](https://www.nuget.org/packages/Spectre.Console.Cli/) | 0.55.0 | 2026-04-03 | Separate repo since 0.54.0; being prepped for 1.0 | **Do not use.** Explicitly not trimmable and not AOT-safe, and you already have Xcaciv.Command. |
| [`Consolonia.Core`](https://www.nuget.org/packages/Consolonia.Core/) | 12.0.3.13 | 2026-08-27 | Stable-versioned, actively developed | Avalonia-XAML-in-a-terminal. 62.5K downloads *total* (vs Terminal.Gui's 2.0M). Drags in the whole Avalonia stack. Second-best only if the team is already deep in Avalonia XAML. |
| [`PrettyPrompt`](https://www.nuget.org/packages/PrettyPrompt/) | 6.0.5 | 2026-08-16 | Actively maintained; `net10.0`; **MPL-2.0** | A `Console.ReadLine` replacement with syntax highlighting, completion menus, history and multi-line input. The best answer for the REPL *input line* if you go Spectre-primary. Licence is file-level copyleft — fine to link, but state it. |
| Plain `System.Console` | n/a | n/a | Always there | The mandatory fallback path, not a UI strategy. |

Both primary repos are healthy: [tui-cs/Terminal.Gui](https://github.com/tui-cs/Terminal.Gui) — 11,186 stars, 51 open issues, last push 2026-08-27, MIT; [spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console) — 11,602 stars, 184 open issues, last push 2026-08-27, MIT.

Note the org move: **`gui-cs` → `tui-cs`** (rename landed in v2.4.10, 2026-06-23). Old `gui-cs.github.io` doc links still resolve; new canonical home is <https://tui-cs.github.io/Terminal.Gui/>.

---

## Analysis

### 1. Terminal.Gui: the v1 vs v2 situation

**v2 is GA and has been for four months.** From the GitHub releases API on `tui-cs/Terminal.Gui`:

| Tag | Published | Kind |
|---|---|---|
| v1.19.0 | 2025-06-12 | last v1 release |
| v2.0.0-beta.1 | 2026-03-04 | prerelease |
| v2.0.0-rc.1 … rc.7 | 2026-04-20 → 04-28 | prerelease |
| **v2.0.0** | **2026-04-28** | **GA** |
| v2.1.0 | 2026-05-08 | GA |
| v2.2.0 | 2026-05-19 | GA |
| v2.4.0 | 2026-05-23 | GA |
| **v2.4.17** | **2026-07-07** | **current stable** |
| 2.4.18-develop.53 | 2026-08-27 | prerelease (nightly `develop`) |

Seventeen patch releases in ten weeks after GA is a lot of churn; read that as "actively hardened", not "unstable" — but pin an exact version and do not float.

**Is it production-ready?** Yes, with three qualifications:

1. **It is `net10.0`-only.** `~/.nuget/packages/terminal.gui/2.4.17/lib/` contains exactly one TFM: `net10.0` **[verified locally]**. Not a problem here (the source already targets net10.0 and `global.json` pins SDK 10.0.100-rc.1) — but it forecloses any netstandard consumption of the UI layer.
2. **The published docs track `develop`, not the release.** Three concrete drifts I hit **[verified locally]**: `Application.GetRegisteredDriverNames()` and `DriverRegistry.GetRegisteredDrivers()` are in [drivers.md](https://github.com/tui-cs/Terminal.Gui/blob/develop/docfx/docs/drivers.md) but *do not exist* in 2.4.17; [application.md](https://github.com/tui-cs/Terminal.Gui/blob/develop/docfx/docs/application.md) shows `RunnableWrapper<ColorPicker>` but the shipped type is `RunnableWrapper<TView, TResult>`. Budget time for doc-vs-reality mismatches.
3. **The dependency footprint exploded.** v1.19.0 depended on `NStack`. v2.4.17's nuspec declares **twelve** direct dependencies **[verified locally]**:

   ```
   ColorHelper 1.8.1 · JetBrains.Annotations 2026.2.0 · Markdig 1.3.2
   Microsoft.Extensions.Configuration{,.Binder,.Json} 10.0.7
   Microsoft.Extensions.Logging.Abstractions 10.0.7 · Microsoft.Extensions.Options 10.0.9
   System.IO.Abstractions 22.1.1 · TextMateSharp 2.0.4 · TextMateSharp.Grammars 2.0.4
   Wcwidth 4.0.1
   ```

   `TextMateSharp` pulls `Onigwrap`, which ships a **native** `libonigwrap.so` / `.dll`. See the AOT section — this has real consequences for "self-contained binary".

   The upside: those dependencies exist because **v2 ships a real `Markdown` view** (`Terminal.Gui.Views.Markdown`, with `MarkdownCodeBlock`, `MarkdownTable`, link activation, text selection and `Copy()` **[verified locally]** in the shipped XML docs). For an LLM chat client whose responses *are* markdown with fenced code blocks, that is not bloat — it is the feature you would otherwise have written.

**Breaking changes v1 → v2.** The [migration guide](https://tui-cs.github.io/Terminal.Gui/docs/migratingfromv1.html) is thorough; these are the ones that will actually bite this codebase:

| Area | v1 (what the source uses) | v2 |
|---|---|---|
| Namespaces | everything in `Terminal.Gui` | split into `Terminal.Gui.App`, `.ViewBase`, `.Views`, `.Drawing`, `.Drivers`, `.Text`, `.Testing`, `.Configuration` |
| App model | `static Application.Init()` / `Application.Run()` / `Application.Shutdown()` | `using IApplication app = Application.Create(); app.Init(); app.Run(view);` — `Shutdown()` obsolete, use `Dispose()`; "Whoever creates it, owns it" |
| Draw override | `public override void OnDrawContent(Rect contentArea)` | `protected override bool OnDrawingContent(DrawContext? ctx)` returning `bool` |
| Geometry | `Rect`, `Bounds` | `System.Drawing.Rectangle`, `Viewport` — and `Viewport.Location` can now be non-zero (scrolling) |
| Drawing calls | `Driver.SetAttribute(...)`, `Driver.AddStr(...)` | `View.SetAttribute(...)`, `View.AddStr(col,row,str)`. Views must **not** touch `Application.Driver` |
| Colour | `Attribute.Make()`, `Color.Brown`, `ColorScheme`, `Colors.Base` | `new Attribute(fg,bg,TextStyle)`, `Color.Yellow`, `Scheme` + `VisualRole`, ARGB32 truecolor `Color` struct |
| Text | `NStack.ustring` | `string` + `StringExtensions.GetColumns()` |
| Events | custom delegates, `Button.Clicked` | `EventHandler<T>`, `Button.Accepting`, `Enter`/`Leave` → `HasFocusChanged` |
| Focus | `CanFocus` defaults true, `TabIndex` | `CanFocus` **defaults to false — must opt in**, `TabStop = TabBehavior.TabStop` |
| Layout | `LayoutStyle`, `AutoSize`, `Pos.At()` | declarative only: `Dim.Auto()`, `Pos.Absolute()` |
| Scrolling | `ScrollView` | removed — every `View` scrolls, `SetContentSize()` |
| Input | `KeyCode`, `MouseClick` | `Key` enum, `KeyBindings`/`MouseBindings` → `Command` |

Concretely: the source's `src/ChatDbg.Shell.Gui/UI/LogProbHeatmapView.cs` is 88 lines and uses **seven** removed APIs — `OnDrawContent(Rect)`, `Bounds`, `Driver.SetAttribute`, `Driver.AddStr`, `Colors.Base`, `Terminal.Gui.Attribute(Color,Color)` with 16-colour `Color`, and `Color.Brown`. It is a rewrite, not a port. `SettingsDialog.cs` (608 lines) and `SystemPromptsDialog.cs` (697 lines) will be hit hardest by the `CanFocus` default flip and the `Dim.Auto()` change.

Here is that whole view rewritten for v2 — this exact code compiles and runs against 2.4.17 **[verified locally]**:

```csharp
using Terminal.Gui.ViewBase; using Terminal.Gui.Drawing; using Terminal.Gui.Text;
using TgColor = Terminal.Gui.Drawing.Color;
using TgAttribute = Terminal.Gui.Drawing.Attribute;

sealed class HeatMap : View
{
    public (string Tok, double P)[] Tokens = [];

    protected override bool OnDrawingContent(DrawContext? ctx)
    {
        int x = 0, y = 0;
        foreach (var (tok, p) in Tokens)
        {
            string t = tok.Replace("\n", "\\n");
            int w = t.GetColumns();                       // Unicode-aware, NOT .Length
            if (x + w > Viewport.Width) { x = 0; y++; }
            if (y >= Viewport.Height) break;
            byte g = (byte)(60 + 195 * p);
            SetAttribute(new TgAttribute(new TgColor(10,10,10), new TgColor((byte)(255-g), g, 60)));
            AddStr(x, y, t);
            x += w;
        }
        return true;
    }
}
```

Running it headless and dumping the driver cell buffer gives, for tokens `("The",.97) (" quick",.81) (" 世界",.34) (" 🙂",.05) (" fox",.62)` **[verified locally]**:

```
|The quick 世 界  🙂  fox            |
cell(1,1) 'T' attr=[#0A0A0A,#06F93C,None]
cell(1,4) ' ' attr=[#0A0A0A,#26D93C,None]
cell(1,15)' ' attr=[#0A0A0A,#BA453C,None]
```

Per-token 24-bit background attributes land in the buffer, and each cell is individually assertable. That is the whole testability story in one snippet.

### 2. Spectre.Console, and whether the two libraries fight

**Version.** 0.57.2, 2026-07-02. Recent history is fast and *not* semver-safe:

- **0.54.0** (2025-11-12) — `Spectre.Console.Cli` [moved to its own repository](https://github.com/spectreconsole/spectre.console/releases/tag/0.54.0) and is now versioned independently; new `Spectre.Console.Cli.Testing` package.
- **0.55.0** (2026-04-03) — flagged `> [!CAUTION] There are breaking changes in this release`. Introduced `Spectre.Console.Ansi`; **`Style` changed from a class to a struct** and link/URL info moved to a new `Link` type; obsolete members removed.
- **0.55.2** (2026-04-17) — variation selectors, ZWJ sequences and surrogate pairs in length calculation.
- **0.56.0** (2026-06-05) — "[Ensure redirected output works as expected](https://github.com/spectreconsole/spectre.console/pull/2098)".
- **0.57.0** (2026-06-11) — new box border styles (beveled, dashed, dotted, heavy, rounded variants).

**What it is best at.** Shipped widgets (from `src/Spectre.Console/Widgets` on `main`): `Table`, `Grid`, `Panel`, `Rows`, `Columns`, `Align`, `Padder`, `Rule`, `Text`, `Paragraph`, `Markup`, `Canvas`, `Tree`, `Calendar`, `TextPath`, `ProgressBar`, `FigletText`, `Layout`, and under `Charts/`: `BarChart` and `BreakdownChart`. Under `Live/`: `LiveDisplay`, `Progress`, `Status`. Plus prompts (`TextPrompt`, `SelectionPrompt`, `MultiSelectionPrompt`, `ConfirmationPrompt`).

Its real strength for this app is the **measure/render two-phase model**: `IRenderable.Measure(RenderOptions, maxWidth)` then `Render(...)` producing `Segment`s. A `Table` measures all columns, picks widths, then renders each cell knowing its space ([rendering model docs](https://spectreconsole.net/console/explanation/understanding-rendering-model/)). That means your token-card grid re-lays-out for free on any width — including a width you invent for a test.

**Do they fight over the terminal? In v1, yes — and the source application has this bug today.** `src/ChatDbg.Shell.Gui/Program.cs` line 20 creates `IConsoleFormatter consoleFormatter = new SpectreConsoleFormatter();` and line 84 passes it into `ChatWindow`, which passes it to commands like `DemoLogProbsCommand`. Those commands call `AnsiConsole.Write(grid)` (`SpectreConsoleFormatter.cs:100`) — i.e. they write escape sequences directly to stdout *while Terminal.Gui owns the screen* (`Application.Init()` at line 71, `Application.Run()` at line 90). Terminal.Gui's cell buffer has no idea those bytes happened, so its next dirty-cell repaint leaves the Spectre output half-overwritten; and because v1 runs on the alternate screen buffer, whatever survives is destroyed when `Application.Shutdown()` restores the primary buffer.

**In v2 there is a sanctioned answer.** [`Terminal.Gui.Interop.Spectre`](https://www.nuget.org/packages/Terminal.Gui.Interop.Spectre) ships two files. `SpectreView : View` holds an `IRenderable` and, in `OnDrawingContent`, does:

```csharp
private static readonly IAnsiConsole _nullConsole = AnsiConsole.Create(new AnsiConsoleSettings
{
    Out = new AnsiConsoleOutput(TextWriter.Null)
});
...
RenderOptions renderOptions = RenderOptions.Create(_nullConsole, null);
Measurement measurement = renderable.Measure(renderOptions, maxWidth);
List<Segment> segments = [.. renderable.Render(renderOptions, maxWidth)];
```

then walks the segments, converts each `Segment.Style` to a Terminal.Gui `Attribute` via `SpectreMarkupBridge.ToAttribute()`, and paints grapheme by grapheme with `AddStr`. **Spectre never touches the terminal.** ([source](https://github.com/tui-cs/Terminal.Gui/blob/develop/Terminal.Gui.Interop.Spectre/SpectreView.cs))

I confirmed it works. Rendering a Spectre `Table` with a CJK row and an emoji row inside a Terminal.Gui `Window`, then dumping the driver cell grid (`·` = empty cell, `_` = space) **[verified locally]**:

```
r0: ┌|─|─|─|─|─|─|─|┬|─|─|─|─|─|─|┐
r1: │|_|T|o|k|e|n|_|│|_|p|_|_|_|_|│
r2: ├|─|─|─|─|─|─|─|┼|─|─|─|─|─|─|┤
r3: │|_|h|e|l|l|o|_|│|_|0|.|9|3|_|│
r4: │|_|世|_|界|_|_|_|│|_|0|.|4|1|_|│
r5: │|_|🙂|_|a|b|_|_|│|_|0|.|0|2|_|│
r6: └|─|─|─|─|─|─|─|┴|─|─|─|─|─|─|┘
```

Every `│` lands in column 8 and column 15 on every row: the wide CJK glyphs occupy one cell plus a continuation cell, the emoji likewise, and the table stays aligned. The bridge is correct on the hard case.

**Three constraints on the bridge:**

1. `SpectreView` is documented as "a **read-only** `View`". You get *rendering*, not interaction — no Spectre `SelectionPrompt` or `LiveDisplay` inside a Terminal.Gui app.
2. The null console's detected profile drives `RenderOptions`. Widgets that branch on capability read that profile, not the real terminal — e.g. `Canvas.Measure` picks `pixelWidth = options.Unicode ? 1 : 2` ([Canvas.cs](https://github.com/spectreconsole/spectre.console/blob/main/src/Spectre.Console/Widgets/Canvas.cs)). Colour is *not* downsampled at this stage (downsampling happens when writing ANSI), so truecolor survives into `Attribute`s — confirmed above. But if you use `Canvas` or `BarChart` through the bridge, check the glyphs.
3. **Namespace collisions.** The bridge's own source needs `using TgAttribute = Terminal.Gui.Drawing.Attribute; using TgColor = Terminal.Gui.Drawing.Color; using SpectreColor = global::Spectre.Console.Color;`. In my probe I additionally needed `using SpectreTable = Spectre.Console.Table;`. Comparing the two packages' XML doc files, the exactly-colliding public simple names are `Padding`, `Region`, `StringExtensions`, `TreeNode` **[verified locally]**, plus the ambiguities the compiler raises for `Color`, `Attribute`, `Style`, `Table` and `Cell` depending on your `using` set. Establish alias conventions in a `GlobalUsings.cs` on day one.

### 3. The three shapes, costed

| | Plain REPL + Spectre | Full-screen Terminal.Gui | Hybrid (Spectre default, TG for episodes) |
|---|---|---|---|
| **Testability** | Best. `TestConsole` from `Spectre.Console.Testing` captures output as a plain string; renderables are pure functions of the model, so golden-master tests are trivial and width-parameterised. | Good, and better than you'd expect: `Terminal.Gui.Testing.InputInjector` with `InjectKey`/`InjectMouse`/`InjectSequence`, `Terminal.Gui.Time.VirtualTimeProvider`, and the `ansi` driver for CI — all present in 2.4.17 **[verified locally]**. You assert on `app.Driver.Contents[r,c].Grapheme` / `.Attribute`. But you're asserting on a 2D grid, which is brittle to layout tweaks. | Both, and you get to choose per surface. Cost: two assertion styles in one suite. |
| **Cross-platform** | Excellent. One code path; Spectre's `Legacy` capability is now `[Obsolete]` and hardcoded `false` ([Capabilities.cs](https://github.com/spectreconsole/spectre.console/blob/main/src/Spectre.Console/Capabilities.cs)), i.e. it assumes Win10+ VT everywhere. | Good but three code paths: v2 ships `windows` (Win32 `ReadConsoleInputW`/`WriteConsoleW` + double buffering), `ansi` (raw `poll`/`read`/`tcsetattr` on Unix, `ReadFile`/`WriteFile` P/Invoke on Windows) and `dotnet` (pure `System.Console`) drivers, auto-selected by platform ([drivers.md](https://github.com/tui-cs/Terminal.Gui/blob/develop/docfx/docs/drivers.md)). Three drivers = three bug surfaces; the `dotnet` driver's own docs warn "`System.ReadKey` has bugs on Windows". | Same as the TUI row, but the blast radius is one dialog rather than the whole app. |
| **Screen-reader accessibility** | Least bad. Linear, append-only output is what screen readers were built for. Still noisy: box-drawing borders get read aloud ("box drawing light horizontal"), so a `--plain` mode that drops borders matters. | Effectively zero. See §5. | Least bad by default; the inaccessible mode is opt-in. |
| **What you can build** | Streaming chat, tables, grids of cards, rules, charts, prompts. **No** scrollback pane, **no** mouse, **no** modal, **no** side-by-side panes that survive a resize. | Everything: scrollable `TableView` over 100k rows, `Markdown` view, `TabView`, mouse, dialogs, `TreeView`, `GraphView`. | Everything, with the cost that the *same* token data has two presenters. |

### 4. Rendering dense token data specifically

| Operation | Best tool | Why |
|---|---|---|
| **Heat-mapped text** (each token background-coloured by confidence, inline, reflowing) | Spectre `Paragraph`/`Markup` for the scrollback path; a hand-written `View` for the TUI path | Spectre gives you correct wrapping + width for free; the hand-written `View` gives you scrolling and per-cell hit-testing. Both are ~30 lines. Neither library has a "heat map" primitive — you build it. |
| **Responsive grid of token cards** | Spectre `Grid` of `Panel`s (what the source already does — `SpectreConsoleFormatter.cs:61-100`), or `Columns` | `Grid` measures and re-flows on width. `Columns` auto-wraps a list of renderables into as many columns as fit — closer to "responsive" than the source's fixed-column `Grid`. |
| **Detail table** (token, p, top-K alternatives) | Spectre `Table` | Per-column `Width()`, `NoWrap()`, `Centered()`, row separators, footers, title. The source already uses exactly this (`SpectreConsoleFormatter.cs:109-144`). Keep it. |
| **Probability map over the vocabulary** (thousands of rows, scroll + sort + filter) | Terminal.Gui `TableView` | Spectre `Table` renders *all* rows every time — it has no virtualisation. `TableView` in v2 takes generic collections, checkboxes, tree structures and custom cell rendering, and scrolls. This is the one surface that genuinely needs the TUI. |
| **Token attribution back to input spans** | Terminal.Gui `Markdown` view or a custom `View` with `LineCanvas` | You need selection and link-style activation to jump from an output token to its source span. `Terminal.Gui.Views.Markdown` already has `GetSelectedText()`, `Copy()`, `ActivateLink()`, `FindLinkUrlAt()` **[verified locally]**. |
| **Streaming tokens as they arrive** | Spectre `Progress`/`Status`, or plain `Console.Write` — **not `LiveDisplay`** | See the redirection trap in §5. |
| **Distribution sparkline / top-K bars** | Spectre `BarChart` / `BreakdownChart` | Already there; no third library needed. |

**Colour depth.** The two libraries detect independently and you must configure both.

*Spectre* stores detected state in `AnsiConsole.Profile.Capabilities`: `ColorSystem` (`NoColors`/`Legacy`/`Standard`/`EightBit`/`TrueColor`), `Ansi`, `Links`, `Interactive`, `Unicode`, `AlternateBuffer` ([capabilities reference](https://spectreconsole.net/console/reference/capabilities-reference)). It reads `NO_COLOR`, `TERM`, `COLORTERM` (`truecolor`/`24bit`), `ConEmuANSI`, and recognises CI systems (most get `Interactive=false`; GitHub Actions gets `Ansi=true`). Downsampling is automatic and correct — the *same* `[rgb(255,120,0)]tok[/]` emits **[verified locally]**:

```
ColorSystemSupport.TrueColor  →  ESC[38;2;255;120;0m tok ESC[0m
ColorSystemSupport.Standard   →  ESC[33m            tok ESC[0m
```

*Terminal.Gui v2* exposes `IDriver.ColorCapabilities` (`NoColor`/`Colors16`/`Colors256`/`TrueColor`) and `IDriver.Force16Colors`. Running the probe under different environments **[verified locally]**:

| Environment | Reported |
|---|---|
| WSL, `TERM=xterm-256color`, `WT_SESSION` set | `{ Term = xterm-256color, ColorTerm = , TermProgram = , IsWindowsTerminal = True, Capability = TrueColor }`, `Force16Colors=False` |
| `NO_COLOR=1` | `Capability = NoColor`, **`Force16Colors=True`** |
| `TERM=dumb` | `Capability = NoColor`, `Force16Colors=True` |
| `TERM=xterm`, no `COLORTERM`, no `WT_SESSION` | **`Capability = TrueColor`** |

Two things fall out. First, **Terminal.Gui v2 does honour `NO_COLOR`** — good, and better than the single stale issue in its tracker suggested. Second, **the last row is an over-optimistic detection**: bare `TERM=xterm` with no `COLORTERM` is not a truecolor guarantee, yet the driver reports `TrueColor`. Ship a `--colors {auto|truecolor|256|16|none}` switch that sets `Driver.Force16Colors` / `ColorSystemSupport` explicitly, and default the *quantisation of your palette* (not just the emitted sequence) off the reported capability. Also note: `IDriver.SupportsTrueColor` reported `True` even in the `NoColor` case **[verified locally]** — read `ColorCapabilities`, never `SupportsTrueColor`.

Terminal.Gui v2 additionally has `Color.None`, which emits `CSI 39m`/`CSI 49m` so the terminal's own (possibly translucent/acrylic) background shows through, and resolves real default colours via OSC 10/11 queries at startup ([drawing.md](https://github.com/tui-cs/Terminal.Gui/blob/develop/docfx/docs/drawing.md)). For a heat map you want explicit backgrounds, but for the chat log `Color.None` is the right default so the app doesn't fight the user's theme. It also ships `ColorQuantizer`, `Gradient`/`GradientFill` and `LineCanvas` — a gradient is exactly the right primitive for a continuous confidence scale, and beats the source's six-bucket `switch` on `Color.Green`/`Brown`/`BrightRed`.

**Windows Terminal vs conhost.** Spectre no longer really cares — `Capabilities.Legacy` is `[Obsolete("This property will be removed in a future version")]` and set to `false` unconditionally, and the facade picks the legacy backend only when `Profile.Capabilities.Ansi` is false ([AnsiConsoleFacade.cs](https://github.com/spectreconsole/spectre.console/blob/main/src/Spectre.Console/Internal/Backends/AnsiConsoleFacade.cs)). Terminal.Gui's `windows` driver goes native — `ReadConsoleInputW`, `WriteConsoleW`, `CreateConsoleScreenBuffer`/`SetConsoleActiveScreenBuffer` for double buffering, `GetConsoleScreenBufferInfoEx` for size, `WINDOW_BUFFER_SIZE_EVENT` for immediate resize — with an ANSI/VT path when VT mode is available and Win32 `CONSOLE_CURSOR_INFO` as the legacy fallback. Practical consequence: **Terminal.Gui degrades better on old conhost than Spectre does**, and Spectre's truecolor output on a pre-VT console will be wrong. Both are fine on Windows Terminal.

**Unicode and emoji width.** Both libraries do it properly, by different routes. Spectre calls `UnicodeCalculator.GetWidth` behind a 65,536-entry per-`char` cache ([Cell.cs](https://github.com/spectreconsole/spectre.console/blob/main/src/Spectre.Console/Internal/Cell.cs)); Terminal.Gui takes a direct `Wcwidth 4.0.1` dependency and exposes `StringExtensions.GetColumns()` / `RuneExtensions.GetColumns()` plus a `GraphemeHelper`. Empirically, every row of a redirected Spectre table containing `hello`, `世界` and `🙂` measured **exactly 16 display columns** **[verified locally]**.

**This is where the source app is wrong.** `LogProbHeatmapView.cs:38` computes `var tokenWidth = tokenText.Length;` — UTF-16 code-unit count. For a CJK token that under-counts by 2×; for an emoji token (surrogate pair, 2 columns) it *over*-counts by 1 while under-counting display width. Since LLM tokenizers routinely emit CJK fragments and emoji, the heat map's line wrapping is already broken for non-Latin content. Replace with `GetColumns()` in the TUI path, and let Spectre measure in the console path.

**Right-to-left.** Not supported by either, and don't plan on it. Terminal.Gui issue [#3322 "Support Arabic text in TextView and in ListView"](https://github.com/tui-cs/Terminal.Gui/issues/3322) has been open since 2024-03-14; the older [#894 "RTL support"](https://github.com/tui-cs/Terminal.Gui/issues/894) and [#2221 "layout mirroring Right to Left flow"](https://github.com/tui-cs/Terminal.Gui/issues/2221) are closed without implementation. A GitHub issue search for `bidi` returns **zero** results in both repositories **[verified locally]**. If a model emits Arabic or Hebrew tokens, they will render in logical order, unshaped, with broken bidi runs. Document it as a known limitation; do not promise it.

### 5. Accessibility and non-interactive use

**When you redirect Terminal.Gui v2 to a file, you get an empty file.** I ran a full `app.Run(window)` loop with the `ansi` driver, stdout redirected, with two `AddTimeout` callbacks — one writing via `AnsiConsole.MarkupLine`, one stopping the app **[verified locally]**:

```
stdout bytes: 38
altbuf enter: False   altbuf exit: False   ESC count: 0
content: 'SPECTRE-WROTE-HERE\nSPECTRE-WROTE-HERE\n'
```

Terminal.Gui contributed **zero bytes** — not even `CSI ?1049h`. Only Spectre's writes survived. That is arguably the right behaviour (no escape-sequence garbage in your log), but it means **a Terminal.Gui-primary app produces nothing at all when piped**. `chatdbg ... > transcript.txt` would yield an empty transcript.

**Spectre degrades correctly and I measured how.** Same probe, stdout redirected **[verified locally]**:

```
ColorSystem=EightBit  Ansi=False  Interactive=False  Unicode=True  Links=False  Width=80  Height=24
┌───────┬──────┐
│ Token │ p    │
├───────┼──────┤
│ hello │ 0.93 │
│ 世界  │ 0.41 │
│ 🙂    │ 0.02 │
└───────┴──────┘
truecolor sample
```

Zero ESC bytes in the file. Note `Width=80 Height=24` — with no TTY, Spectre falls back to 80×24, so wide tables get wrapped to 80 columns in your log regardless of the terminal you launched from. Set `Profile.Width` explicitly when you know better (e.g. from a `--width` flag or `$COLUMNS`).

**The `LiveDisplay` trap — do not use it for token streaming.** `Progress` and `Status` both check `caps.Interactive && caps.Ansi` and swap in a `FallbackProgressRenderer` / `FallbackStatusRenderer` ([Progress.cs](https://github.com/spectreconsole/spectre.console/blob/main/src/Spectre.Console/Live/Progress/Progress.cs)), producing clean plain text when redirected **[verified locally]**:

```
loading: 99%
still thinking
```

`LiveDisplay` has **no such fallback** — [LiveDisplayRenderer](https://github.com/spectreconsole/spectre.console/blob/main/src/Spectre.Console/Live/LiveDisplayRenderer.cs) unconditionally emits a `PositionCursor` renderable plus the whole target on every refresh. Redirected, three `ctx.Refresh()` calls produced **[verified locally]**:

```
live tick 0live tick 0live tick 1live tick 1live tick 2live tick 2live tick 2
```

Duplicated, run together, unusable. If the token stream is rendered with `LiveDisplay`, every piped transcript is corrupt.

**Screen readers.** Neither library has any screen-reader support, and a GitHub issue search for `accessibility` / `screen reader` returns effectively nothing in either repo **[verified locally]** — one tangentially-titled closed issue in Terminal.Gui, six unrelated hits in Spectre. This is an ecosystem-wide gap, not a library choice. The [OSnews piece "The text mode lie: why modern TUIs are a nightmare for accessibility"](https://www.osnews.com/story/144892/the-text-mode-lie-why-modern-tuis-are-a-nightmare-for-accessibility/) (2026-05-05) states the mechanism plainly: modern TUI frameworks treat the terminal as "a 2D grid of pixels, where every character cell is a pixel", "the cursor jumps all over the place with every screen update, which makes screen readers go nuts", and box-drawing decoration gets announced literally ("box drawing light horizontal"). Its recommendation — and a commenter's, which is the practical one — is that "every TUI based program should have a standard argument based CLI version for which the TUI is merely a wrapper".

**So the app needs three output modes, not two:**

| Mode | Trigger | Renderer |
|---|---|---|
| `rich` | TTY, colour available, no `--plain` | Spectre renderables with colour and borders; Terminal.Gui for opt-in episodes |
| `plain` | `--plain`, `NO_COLOR`, `TERM=dumb`, `Ansi=false`, or `!Console.IsOutputRedirected == false` | Spectre with `ColorSystemSupport.NoColors` and border-less tables (`Table.NoBorder()`), or straight `TextWriter` |
| `data` | `--format json` / `--format csv` | No rendering at all — serialise the token model. This is also what makes the token introspection *scriptable*, which is arguably the feature's highest-value use. |

That third mode is not extra work: `Xcaciv.Command`'s `IResult<T>` already carries a `ResultFormat` enum with `General`, `Object`, `CSV`, `TDL`, `YAML`, `JSON` **[verified locally, from [ResultFormat.cs](https://github.com/Xcaciv/Xcaciv.Command/blob/main/src/Xcaciv.Command.Interface/ResultFormat.cs)]**. Use it.

### 6. Recommendation, and how to keep the UI layer swappable

**Take the hybrid, Spectre-primary.**

```
Terminal.Gui        2.4.17     (GA, MIT, net10.0)   — optional TUI episodes
Terminal.Gui.Interop.Spectre 2.4.17 (GA, MIT)       — the only bridge
Spectre.Console     0.57.2     (pre-1.0, MIT)       — the default renderer
Spectre.Console.Testing 0.57.2 (MIT)                — test only
PrettyPrompt        6.0.5      (MPL-2.0)            — optional, REPL input line
```

Deliberately **not** taken: `Spectre.Console.Cli` (not trim/AOT-safe, and `Xcaciv.Command` already owns command dispatch), `Consolonia` (Avalonia stack for no gain here).

**The layering that makes this swappable.** `Xcaciv.Command`'s `IIoContext` is already the outer seam, and its shape constrains the design in a way worth naming explicitly. From [IIoContext.cs](https://github.com/Xcaciv/Xcaciv.Command/blob/main/src/Xcaciv.Command.Interface/IIoContext.cs) **[verified locally]**:

```csharp
public partial interface IIoContext : ICommandContext<IIoContext>
{
    bool HasPipedInput { get; }
    Task<string> PromptForCommand(string prompt);
    Task OutputChunk(IResult<string> message);
    Task SetStatusMessage(string message);
    Task AddTraceMessage(string message);
    Task<int> SetProgress(int total, int step);
    void SetOutputEncoder(IOutputEncoder encoder);   // IOutputEncoder: string Encode(string)
    ...
}
```

Everything crossing the command boundary is a **`string`**. A `Spectre.Console.Rendering.IRenderable` cannot travel through `OutputChunk`. That is a feature, not an obstacle, and it dictates the architecture:

1. **Commands never render.** `InspectCommand`, `TokenizeCommand`, `LogProbsCommand` compute a `TokenIntrospection` model and emit it through `OutputChunk` with `ResultFormat.JSON` (or `Object`). They take no dependency on Spectre or Terminal.Gui. They are unit-testable against `MemoryIoContext` with zero UI in the loop.
2. **One presentation assembly** — call it `ChatDbg.Presentation` — owns the pure functions `IRenderable RenderHeatMap(TokenIntrospection, RenderStyle)`, `IRenderable RenderTokenCards(...)`, `IRenderable RenderDetailTable(...)`. It references **Spectre.Console only**. These are the golden-master test targets: render at width 40, 80, 200 into a `TestConsole`, assert on the string.
3. **Two host adapters implement `IIoContext`:**
   - `ConsoleIoContext` — writes with `IAnsiConsole` (injected, never the `AnsiConsole` static, so tests substitute `TestConsole`), calling the presentation functions and `AnsiConsole.Write(renderable)`. `SetProgress` maps to Spectre `Progress`; `SetStatusMessage` to `Status`. **Never `LiveDisplay`.**
   - `TuiIoContext` — same presentation functions, but the resulting `IRenderable` goes into a `SpectreView.Renderable` inside a Terminal.Gui layout. `PromptForCommand` becomes a `TextField` with `PopupAutocomplete`; `SetProgress` a `ProgressBar` view.
   
   Both are ~150 lines. The expensive layout logic lives in exactly one place.
4. **Choose the adapter at composition, from capability not preference:** `--plain` or `NO_COLOR` or `Console.IsOutputRedirected` → plain `ConsoleIoContext`; `--format json` → a serialising `IIoContext` that ignores the presentation layer entirely; `--tui` → `TuiIoContext` in `AppModel.FullScreen`; default TTY → `ConsoleIoContext`, with Terminal.Gui invoked in `AppModel.Inline` only for the settings/prompt dialogs.

**On `AppModel.Inline`** — this is the feature that makes the hybrid clean, and it is real in the shipped 2.4.17 (`IApplication.AppModel`, `AppModel.Inline`, `IApplication.ForceInlinePosition` all present in the shipped XML docs **[verified locally]**). Per [application.md](https://github.com/tui-cs/Terminal.Gui/blob/develop/docfx/docs/application.md), inline mode stays in the **primary scrollback buffer**, skips `CSI ?1049h`, discovers the cursor row via a CPR (`ESC[6n`) query, sizes the app region to content, grows down (or scrolls up when there's no room), and on exit "the cursor moves to the row below the rendered region so the shell prompt appears naturally" and **`CSI ?1049l` is not emitted** — the widget's output stays in scrollback. Critically: "In inline mode, `ClearContents()` initializes cells with `IsDirty = false`. Only cells explicitly drawn by the app are flushed — the rest of the visible terminal stays untouched." That is exactly what lets a Terminal.Gui dialog appear *inside* a Spectre-rendered chat transcript without eating it.

I confirmed the content sizing works in the shipped package: with `AppModel.Inline` and a three-item `ListView`, `app.Screen` shrank from `{0,0,80,25}` to `{0,0,80,3}` after the first `LayoutAndDraw()` **[verified locally]**.

**Unit-testing the TUI path.** `Terminal.Gui.Testing` is in the box (`InputInjector`, `IInputInjector.InjectKey/InjectMouse/InjectSequence`, `InputInjectionExtensions`, `Terminal.Gui.Time.VirtualTimeProvider` **[verified locally]**). The pattern from [input-injection.md](https://github.com/tui-cs/Terminal.Gui/blob/develop/docfx/docs/input-injection.md):

```csharp
VirtualTimeProvider time = new();
using IApplication app = Application.Create(time);
app.Init(DriverRegistry.Names.ANSI);          // deterministic, works in CI with no TTY
app.InjectKey(Key.Tab); app.InjectKey(Key.Enter);
time.Advance(TimeSpan.FromMilliseconds(50));
// assert on app.Driver.Contents[r, c].Grapheme / .Attribute
```

The docs' own guidance — "Use the **ANSI driver** for testing - it's cross-platform and deterministic", "Always use virtual time for tests" — matches what I observed: the ANSI driver initialises and lays out cleanly with no TTY attached.

---

## What this application specifically needs

| Concrete operation the app performs | What it maps to |
|---|---|
| Stream an assistant reply token by token into the transcript | `IAnsiConsole.Write(new Markup(...))` per chunk, or Spectre `Status` for the "thinking" phase. **Never `LiveDisplay`** — it corrupts redirected transcripts (measured above). The transcript must survive `> file`. |
| Render an assistant reply that is markdown with fenced code | Console path: Spectre `Markup` + `Panel`, or push the raw markdown through and let the user's pager handle it. TUI path: `Terminal.Gui.Views.Markdown`, which already does code blocks (`MarkdownCodeBlock`), tables (`MarkdownTable`), links and selection — this is why Terminal.Gui drags in Markdig + TextMateSharp, and here you actually want it. |
| Heat-mapped token text coloured by log-prob | Presentation function returning a Spectre `Paragraph` of styled spans (console) / the 20-line `HeatMap : View` shown in §1 (TUI). Use a `Gradient` over confidence, not the source's six-bucket `switch`. **Use `GetColumns()`, not `.Length`** — the source's current wrapping is broken for CJK and emoji tokens. |
| Grid of token cards with top-K alternatives | Spectre `Grid` of `Panel`s — which is already what `SpectreConsoleFormatter.cs:61-100` and `TokenProbabilityVisualizer.cs:116-155` do. Consider `Columns` instead for true responsive wrapping. Keep this code; it survives the rebuild almost unchanged. |
| Detail table: `?` / Token / Probability / Top Alternatives | Spectre `Table` with `TableColumn("Token").Width(20)` etc. — again already correct in `SpectreConsoleFormatter.cs:109-144`. |
| Probability map over the whole vocabulary (scroll, sort, filter) | **Terminal.Gui `TableView`.** Spectre `Table` has no virtualisation; it renders every row on every write. This single surface is the strongest argument for keeping Terminal.Gui at all. |
| Token attribution: click an output token, highlight the input span | Terminal.Gui custom `View` with `MouseBindings` → `Command`, plus `LineCanvas` for the connector. No console-mode equivalent; degrade to a printed index table. |
| Settings dialog / named system prompt picker | Terminal.Gui `Dialog` in `AppModel.Inline` — it appears under the prompt, is dismissed, and leaves the transcript intact. The alternative (Spectre `SelectionPrompt` + `TextPrompt`) is fine and simpler if you want to drop Terminal.Gui entirely. |
| Command entry with history and completion | `PrettyPrompt` 6.0.5 in console mode (syntax highlighting, completion menu, history, multi-line — MPL-2.0), or Terminal.Gui `TextField` + `PopupAutocomplete` + `SingleWordSuggestionGenerator` in TUI mode. `IIoContext.PromptForCommand(string)` is the seam. |
| Ships as a self-contained binary for Windows and Linux | **Both libraries are Native-AOT-clean.** `dotnet publish -r linux-x64 -p:PublishAot=true -p:SuppressTrimAnalysisWarnings=false` on a project referencing Terminal.Gui 2.4.17 + Spectre.Console 0.57.2 + the interop produced **zero IL/AOT warnings** and a working 21.8 MB native binary **[verified locally]**; trimmed self-contained with `TrimMode=full` was 28 MB, also zero warnings. Both assemblies carry `IsTrimmable` plus proper `RequiresDynamicCode`/`RequiresUnreferencedCode` annotations **[verified locally]**. |
| …but "self-contained" has an asterisk | The AOT publish emitted **`libonigwrap.so` next to the binary** **[verified locally]** — Onigwrap (native Oniguruma) comes in via TextMateSharp via Terminal.Gui's Markdown view. It is *not* one file. Also present: `fr-FR`/`ja-JP` satellite dirs (set `<SatelliteResourceLanguages>en</SatelliteResourceLanguages>`, which the source's Compact config already does). If a literal single file is a hard requirement, either use `PublishSingleFile` with `IncludeNativeLibrariesForSelfExtract=true` (the source's SingleFile config, which self-extracts at run time) or drop Terminal.Gui from the shipped binary. |
| Stores provider secrets, so must not leak them into a rendered transcript | Argues for the `data`/`plain` mode being a first-class path rather than a fallback: `--format json` never goes near a renderer, so there is one code path to audit for redaction rather than three. |

---

## Risks, sharp edges and what you give up

**What you give up by choosing Spectre-primary:** a persistent full-screen dashboard. The chat transcript scrolls away in the terminal's scrollback rather than living in a pane you can re-scroll independently of the input box. If the product vision is "an IDE in the terminal", this recommendation is wrong and you should go Terminal.Gui-primary — accepting that piping the app produces an empty file and that you need a separate non-interactive entry point.

**Spectre.Console is still 0.x, and it means it.** 0.55.0 shipped `> [!CAUTION] There are breaking changes` and converted `Style` from a class to a struct — which silently changes null-check and reference-equality semantics in any code that held a `Style?`. There is **no announced 1.0 for Spectre.Console itself**; only `Spectre.Console.Cli` was split out to be prepared for 1.0. Pin the exact version, read every minor release note, and keep the presentation assembly small so an upgrade is a bounded diff.

**`Terminal.Gui.Interop.Spectre` is a two-file package with 7.1K lifetime downloads.** It works, and I verified the hard cases (wide chars, emoji, table alignment, style→attribute conversion). But if it goes unmaintained you own ~200 lines of segment-walking code. That is an acceptable bus factor precisely *because* it is 200 lines — read them before you depend on them.

**Terminal.Gui's docs are ahead of its releases.** Three concrete drifts found in one afternoon (§1). Verify every API against the shipped XML docs (`~/.nuget/packages/terminal.gui/2.4.17/lib/net10.0/Terminal.Gui.xml`) before writing against a doc snippet.

**Seventeen patch releases in ten weeks.** v2.4.x is moving fast. Pin, run `dotnet list package --outdated` on a schedule, and read release notes rather than floating.

**The `LiveDisplay` corruption is the most likely bug you will actually ship.** It looks perfect in a terminal and destroys every piped transcript. Add an integration test that runs the app with redirected stdout and asserts the output contains no `\x1b` and no duplicated lines.

**Colour detection is over-optimistic in Terminal.Gui** (`TERM=xterm` alone → `TrueColor`) and `SupportsTrueColor` is not the property to read. Provide an explicit override and default conservatively when `COLORTERM` is absent.

**RTL and bidi are simply absent** from both libraries. If the app is used with Arabic or Hebrew prompts, token-level rendering will be wrong. Say so in the docs.

**Screen-reader accessibility is not achievable with either library.** The mitigation is architectural — always-available `--plain` and `--format json` modes — not a library swap.

**Xcaciv.Command / Xcaciv.Cupcake / Xcaciv.Loader are not on nuget.org.** A search of the nuget.org query API for `Xcaciv` returns two hits, both `XCBatch.*` **[verified locally]**; the frameworks live only at [github.com/Xcaciv/Xcaciv.Command](https://github.com/Xcaciv/Xcaciv.Command), [Xcaciv.Cupcake](https://github.com/Xcaciv/Xcaciv.Cupcake), [Xcaciv.Loader](https://github.com/Xcaciv/Xcaciv.Loader). Consumption is via submodule, project reference, or a private feed — a build/release risk to plan for, though outside this area's scope.

**Two presenters for the same data is real duplication.** The mitigation (one presentation assembly returning `IRenderable`, consumed by both adapters via `SpectreView`) collapses most of it, but the TUI path still needs its own interaction code — mouse bindings, focus order, scroll state. Budget it honestly; do not pretend the hybrid is free.

---

## Unconfirmed

- **The v2.0.0 GA release-note body.** The GitHub API returns a release body for `v2.0.0` that is an accumulated changelog reaching back to v1.9.0 PRs, with no crisp "v2 is GA" statement. GA status is inferred from the tag being `prerelease: false`, from nuget.org listing 2.4.17 as the latest **stable**, and from `v2.0.0-rc.7` immediately preceding it — all consistent, but I found no single announcement sentence. Looked at: `api.github.com/repos/tui-cs/Terminal.Gui/releases`, the [milestone](https://github.com/gui-cs/Terminal.Gui/milestone/7), the repo README.
- **Terminal.Gui v1's formal support policy.** nuget.org's package page for 1.19.0 renders the phrase "v1 is now in maintenance mode", but I found no dated EOL statement, no security-patch commitment, and no explicit "v1 will receive no further releases". Treat 1.19.0 (2025-06-12) as the terminal v1 release, but the *policy* is unconfirmed.
- **Behaviour of `AppModel.Inline` on the Windows driver.** [application.md](https://github.com/tui-cs/Terminal.Gui/blob/develop/docfx/docs/application.md) describes inline mode entirely in terms of `AnsiOutput`, `AnsiSizeMonitor` and the CPR (`ESC[6n`) query. Whether the `windows` driver supports inline mode, and how it behaves on legacy conhost where CPR may not be answered, is not documented and I could not test it on this Linux host. The docs say the `AnsiStartupGate` "times out and rendering proceeds from row 0" if the terminal never responds — which on a real shell would overwrite the user's scrollback. **Test this on Windows conhost before shipping inline mode.**
- **`ForceInlinePosition` in 2.4.17.** The property exists in the shipped API, but setting `app.ForceInlinePosition = new Point(0, 10)` *before* `Init()` left `app.Screen.Y == 0` in my probe. Either the ordering matters, or the shipped behaviour differs from the `develop` docs. Unresolved.
- **Whether `Canvas`/`BarChart` render correctly through `SpectreView`.** I verified `Table` (including CJK and emoji). `Canvas.Measure` branches on `options.Unicode`, which comes from the bridge's `TextWriter.Null` console profile rather than the real terminal, so half-block vs double-space glyph selection may be wrong. Not tested.
- **`LiveDisplay` under a *non-redirected* but non-ANSI console** (e.g. legacy conhost with `Ansi=false`). I measured the redirected case only. The legacy backend's handling of `ControlCode` segments was not inspected.
- **NativeAOT publish on `win-x64`.** I verified `linux-x64` AOT succeeds with zero warnings on this machine (gcc-based link). The Windows link step uses MSVC and was not exercised. The source repo's `build-compact.bat`/`.ps1` already do `win-x64` AOT against v1 + Spectre 0.51.1, so the toolchain path exists — but not with these versions.
- **Actual screen-reader behaviour** of a Spectre-rendered transcript under NVDA/JAWS/Orca. I have the mechanism (cited above) but no measurement. If accessibility is a stated requirement rather than a nice-to-have, this needs a real user test, not a literature review.
- **`Xcaciv.Cupcake`'s own console model.** I read `Xcaciv.Command`'s `IIoContext`, `IOutputEncoder` and `ResultFormat`, but did not enumerate Cupcake's shell loop or whether it already commits to a specific renderer. Looked at: the repo listing via GitHub search (the contents API rate-limited before I could walk the tree).
- **Consolonia's real-world maturity.** I confirmed `Consolonia.Core` 12.0.3.13 (2026-08-27) is a stable-versioned release depending on Avalonia 12.0.3, with 62.5K lifetime downloads. I did not evaluate its rendering fidelity, colour-depth handling, or AOT story — it was ruled out on adoption and dependency weight, not on measured deficiency.
