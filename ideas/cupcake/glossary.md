# Glossary — code name → product term

The PRD uses the **product term** exclusively. Code names appear here and in the traceability
appendix only. Where a single product concept has several code names, all are listed.

| Product term | Definition | Code names in the source |
|---|---|---|
| **Shell** | The interactive program a user runs to type commands and read results. Marketed as "a sweet shell". | `Xcaciv.Cupcake`, "Cupcake" |
| **Lit Shell** | The one shipping executable — a deliberately synchronous, lightweight build of the Shell. | `Xcaciv.Cupcake.Lit` |
| **Session** | One run of the Shell: startup, then a repeating prompt/dispatch cycle, then termination. | `Loop`, `Run`, `RunAsync`, `RunWithDefaults` |
| **Prompt** | The text drawn before the cursor to invite a command line. | `Prompt` |
| **Exit vocabulary** | The set of words that end the Session when typed alone. | `ExitCommands` |
| **Command line** | One line of text the user submits: a Command reference plus arguments, optionally several stages joined into a Pipeline. | `inputCommand`, `commandLine` |
| **Command** | A named unit of behavior the Shell can execute. Has declared metadata and two invocation paths. | `ICommandDelegate`, `AbstractCommand`, `SearchCommand`, `InstallCommand` |
| **Command Group** | An optional namespace prefix that gathers related Commands, so they are invoked as `GROUP NAME …`. | `CommandRootAttribute`, "root command", "sub-command" |
| **Built-in Command** | A Command the Command Service supplies itself, needing no Plugin. Four exist: a pattern filter, an echo that expands Session Variables, a variable setter, and a variable dump. | `RegifCommand`, `SayCommand`, `SetCommand`, `EnvCommand` |
| **Host-linked Command** | A Command compiled into the Shell executable and registered directly at startup rather than discovered on disk. | `Controller.AddCommand("internal", …)` |
| **Positional Parameter** | An argument identified by its position in the argument list. | `CommandParameterOrderedAttribute` |
| **Named Parameter** | An argument introduced by its name and followed by its value. May declare a default and an allowed-value list. | `CommandParameterNamedAttribute` |
| **Flag** | A name-only argument whose presence is the whole signal. | `CommandFlagAttribute` |
| **Direct invocation** | Executing a Command against its arguments, with no upstream Pipeline stage. | `HandleExecution` |
| **Per-chunk invocation** | Executing a Command once for each unit of data arriving from an upstream Pipeline stage. | `HandlePipedChunk` |
| **Command Service** | The required capability that registers Commands, resolves a Command line to a Command, binds arguments, runs Pipelines, and generates help. Supplied by an external dependency, not by this repository. | `ICommandController`, `CommandController`, `CommandRegistry`, `CommandExecutor`, `CommandFactory`, `HelpService` |
| **Command Registration Record** | The registry's entry for one Command or Command Group: its invocation name, its sub-entries, where its code came from, and whether it may change Session Variables. | `ICommandDescription`, `CommandDescription` |
| **Interaction Context** | The abstraction through which a Command emits output, status, progress and trace, and through which the Session obtains input. One per Command execution, created as a child of the Session's. | `IIoContext`, `AbstractTextIo`, `ICommandContext` |
| **Presentation Adapter** | The Shell's concrete Interaction Context: renders to a colour terminal and reads typed lines. | `ConsoleContext` |
| **Output** | A discrete unit of a Command's result, rendered to the user or forwarded to the next Pipeline stage. | `OutputChunk`, `HandleOutputChunk` |
| **Status line** | Transient progress or state text, distinct from Output and never piped. | `SetStatusMessage` |
| **Diagnostic trace** | Developer-facing detail, shown only when diagnostics are enabled and otherwise sent to the platform debug channel. | `AddTraceMessage`, `Trace.WriteLine` |
| **Status Visibility** | The setting that decides whether Status lines are rendered or suppressed. | `Verbose` |
| **Progress reading** | A number derived from a total and a step count, formatted into the Status line. | `SetProgress`, `ProgressTemplate` |
| **Pipeline** | A Command line split into stages joined by a delimiter, each stage's Output feeding the next stage's input, all running concurrently. | `PipelineExecutor`, `PipelineParser`, `|` |
| **Stage Channel** | The bounded, ordered buffer carrying Output from one Pipeline stage to the next. | `Channel<string>`, `ChannelReader`, `ChannelWriter` |
| **Session Variables** | A case-insensitive keyed store of text values, readable and writable by Commands, scoped per Command with opt-in write-back to the Session. | `IEnvironmentContext`, `EnvironmentContext`, "env" |
| **Registry Endpoint setting** | The Session Variable naming which Package Registry to query; falls back to a built-in public endpoint. | `PackageSourceUrl` / `PACKAGESOURCEURL` |
| **Plugin** | A separately published bundle of Commands the Shell can discover and load at startup. | "package", "command package", plugin assembly |
| **Plugin Directory** | The on-disk root the Shell scans for Plugins at startup. | `PackageDirectory`, `.\packages` |
| **Plugin Scanner** | The component that walks the Plugin Directory and reports the Commands each Plugin offers. | `Crawler`, `ICrawler`, `CommandLoader` |
| **Verified Directory rule** | The safety rule that a Plugin Directory must exist and resolve inside a restricted root before it is scanned; a directory failing the check is silently skipped. | `VerifiedSourceDirectories`, `VerifyRestrictedPath` |
| **Isolated Plugin Loader** | The mechanism that instantiates a Plugin's Command in a sandbox confined to that Plugin's own directory. | `AssemblyContext`, `AssemblySecurityConfiguration` |
| **No Plugins Available** | The startup condition where no Plugin Directory survived verification, reported to the user as guidance rather than as a failure. | `NoPluginsFoundException` |
| **Startup Load Failure** | The error raised when Plugin loading fails for any reason other than No Plugins Available; carries a fixed summary and the underlying cause. | `LoadingException` |
| **Package Registry** | The external service that indexes Plugins and serves their metadata and archives. | NuGet feed, `SourceRepository`, `PackageSource` |
| **Package Registry Client** | The Shell-side component that performs search, version enumeration, dependency resolution, download, archive-metadata read, and install against a Package Registry. | `NugetWrapper` |
| **Package Listing** | One search result: identity, summary, and in detailed form its download count, publication date, authors, licence and known-vulnerability count. | `IPackageSearchMetadata` |
| **Package Identity** | A Plugin's unique name paired with a specific version. | `PackageIdentity`, `NuGetVersion` |
| **Package Archive** | The single downloadable file containing a Plugin's payload and its identity manifest. | `.nupkg`, `PackageArchiveReader`, `NuspecReader` |
| **Result limit** | The cap on how many Package Listings a search returns. | `take` |
| **Detail level** | The chosen richness of search output: terse, standard, or full. | `verbosity` (`quiet` / `normal` / `detailed`) |
| **Prerelease inclusion** | Whether unreleased Plugin versions appear in search results. | `prerelease`, `includePrerelease` |
| **Package Search** | The Command that queries a Package Registry for Plugins. Invoked as `PACKAGE SEARCH`. | `SearchCommand` |
| **Package Install** | The Command that is meant to place a Plugin into the Plugin Directory. Invoked as `PACKAGE INSTALL`. Currently inert. | `InstallCommand` |
| **Shell Operator** | Whoever constructs and configures a Session — sets the Prompt, exit vocabulary and Plugin Directory, and registers Host-linked Commands. | the shipping executable's startup code |
| **Vestigial scaffold** | The second, empty executable in the solution that prints a greeting and does nothing else. | `src/Xcaciv.Cupcake/Program.cs` |
