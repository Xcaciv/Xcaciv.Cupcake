/brainstorm 
# Subject

I want to take this "sweet console" named CupCake and add a "sweet agent". The console+agent will have a plugin architecture for input interfaces that will start with Console, but will evolve to include things like REST, ESB or reverse shell over various protocols. The input interface will send direct commands to `Xcaciv.Command.Interface.ICommandController.Run()`. Agent input will be passed to a new agent controller interface that will recieve the command registry of `ICommandController` as tools. 

There are extracted ideas in `ideas/` that should be used as requiremnts and technical implementation details.

The following patterns are important from `ideas/`:
- Chatdbg
  - General prompt and control instruction requirements
  - terminal-hosted UI (aka. full-screen front end) (add tab for console direct commands)
  - Probability Logs for providers that support it (aka confidence capture)
  - Top-k for providers that support it
  - model backends requirements and implementation deatail
- Cupcake (currently incomplete implementation)
  - plugin driven command execution
  - Terminal ui
  - plugin install using custom NuGet registry
  - `Xcaciv.Command` as the backend interface
- Cupcake_next
  - Project layout, organization and layering rules
  - fundamental tech stack specifications
  - tech notes
- opencode (V2 only, **NOT PLUGIN, MCP OR TUI FEATURES**)
  - Config discovery/merge/migration and the canonical data-model census
    - Agent configuration
    - model configuration
  - Session conversation composition
  - Agent Client Protocol (ACP)
  - Session/Conversation history storage and compaction
  - SQLite-backed `Credential`/`Integration` system
  - Actors & Personas
  - Bounded outputs
  - Durable, replayable state
  - Storage, Sync & Share
  - Offline-tolerant by design
  
Each command (used as a tool) will be given a new method on the interface called `AskOrExecute()` that will signify the deafault Agent behavor for the LLM requesting to execute the command, weather it is to ask first or if it is safe to execute the command.

## epic use cases
There are several end user packages and components of Cupcake. **No cupcake agent** supports console command execution (ex. cmd, pwrsh, bash, sh...)

### Cupcake Sommelier (Xcaciv.Cupcake.Sommelier)
The Command Packages nuget server. (implemented https://github.com/Xcaciv/Xcaciv.Cupcake.Sommelier)

### Muffin (Xcaciv.Muffin)
The word picture: a muffin is cupcake without frosting or sprinkels. The technical concept: Muffin is the project that contains all the base 

### Cupcake Lit (Xcaciv.Cupcake.Lit) 
The fully modular terminal console version. It's use case starts by being installable via `dotnet tool` command. Then once installed, a user would execute `cupcake_lit` and use the command pacakge installer via built-in restricted Xcaciv Command console to install the llm chat client, agent connector, chatdbg functionality, and commands individually and interactivly from Cupcake Sommelier NuGet server. 

Cupcake Lit would contain the ability (code namespace Xcaciv.Cupcake.Concierge) to give the user a menu driven or wizard experience to choose and configure Cupcake Command packages (from downloaded nuget packages), download a dotnet template and build a small optimized custom Cupcake binary (aka Concierge Cupcake) that purposly leaves out the plugin architecture, in favor of hardcoded command registration. 

Cupcake lit would have a configurable system prompt that initially focuses on Concierge Cupcake building skills. The `cupcake_lit` TUI would use Terminal.Gui. 

### Concierge Cupcake Agent (Xcaciv.Cupcake.ConciergeAgent)
Dotnet template project used by Cupcake Lit. The Concierge Cupcake's default system prompt would be set at design time with the available commands. Concierge Cupcakes would support headless, non-ineractive execution of prompts. The Concierge Cupcake would use Spectre.Console.

### Cupcake Funfetti (Xcaciv.Cupcake.Funfetti) 
The high polish terminal app (using a highly refined UX via Terminal.Gui) that is intended for agentic code generation. It fully supports the Xcaciv.Command plugin architecture and installing tools (command packages) from Cupcake Sommelier NuGet server via-built in restricted Xcaciv Command console. This coding agent would incorporate opencode-like functionality. It would support configurable agent modes: Build, Plan, Review. These agent modes would each have configrable system prompts and default models.