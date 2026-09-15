# MAF-EVALUATION.md

> Decision record: is Microsoft Agent Framework (MAF) modular enough to be the agent substrate for the Cupcake pivot, and what must Cupcake abstract itself?
> Evaluated 2026-09-07 against MAF .NET 1.20.0 (released 2026-08-31) and Microsoft.Extensions.AI (MEAI) 10.9.0.
> Companion documents: `TECH_STACK_SUGGESTED.md` (layering), `TECH_STACK.md` (concrete .NET 10 stack), `../../project-pivot.md` (product scope).

---

## 1. Verdict

**Adopt MAF as the agent runtime. Do not re-abstract what it already abstracts. Build the layers it does not have.**

MAF is a thin set of abstract classes (`AIAgent`, `AgentSession`, `AIContextProvider`, `ChatHistoryProvider`, `AgentSessionStore`) layered over MEAI (`IChatClient`, `AITool`/`AIFunction`, `DelegatingChatClient`). Every built-in behaviour is a decorator, provider, or middleware that can be removed, reordered, or replaced. The framework's own harness and protocol adapters are built from the same public seams Cupcake would use.

What MAF does **not** own, and Cupcake therefore must:

| Cupcake owns | Why MAF cannot supply it |
|---|---|
| Permission rule engine (opencode-style `allow` / `ask` / `deny` per action and resource pattern) | MAF approval is binary per function (`ApprovalRequiredAIFunction`), decided before function middleware runs. No rule language, no per-argument decisions, no "always allow" ledger outside the Harness. |
| Identity and credential store | Nothing beyond `AgentIsolationKeyProvider` claim scoping and provider SDK credentials. |
| Siloing / sandboxing | No in-process sandbox exists in .NET. Hyperlight is a preview micro-VM whose package is not yet on nuget.org. |
| Transport / input-interface plugins (Console, REST, ESB, reverse shell, ACP, ANP) | MAF ships A2A, AG-UI, and OpenAI-compatible HTTP adapters only, all prerelease. ACP and ANP have no MAF support. |
| Durable, replayable event log (SQLite) | MAF has load/save hooks, not an event model. "MAF doesn't include a general-purpose durable session store." |
| Model catalog and config data model (opencode census) | MEAI has routing mechanisms, not a catalog. |
| Planning methodology contract | MAF has no planner abstraction; it has slots (context providers, todo/mode providers, Magentic manager). |

Status summary at evaluation time:

| Area | Status |
|---|---|
| MAF core (`Microsoft.Agents.AI`, `.Abstractions`, `.Workflows`, `.Harness`, `.OpenAI`, `.GitHub.Copilot`) | **GA**, 1.20.0. GA since 1.0 on 2026-04-02. |
| `Microsoft.Agents.AI.Hosting`, `.Hosting.OpenAI`, `.Hosting.A2A.AspNetCore`, `.Hosting.AGUI.AspNetCore`, `.A2A`, `.Anthropic` | **preview / alpha**, 1.20.0-preview.260831.1 |
| `Microsoft.Agents.AI.Mcp` | **alpha** (pins ModelContextProtocol 2.1.0) |
| `Microsoft.Agents.AI.DurableTask` | **preview**, lagging at 1.16.0-preview |
| Compaction framework | **experimental** (`MAAI001` pragma required) |
| MEAI routing clients (`RoutingChatClient`, `FailoverChatClient`) | **experimental** (`MEAI001`) |
| Agent Hooks (AGENT-HOOKS-0.1 fail-closed policy contract) | **Python only**; not available for .NET |
| Hyperlight CodeAct sandbox | **preview**, package unavailable on nuget.org |
| Release cadence | 1.18.0 (08-18), 1.19.0 (08-22), 1.20.0 (08-31). Breaking rename `AgentThread` to `AgentSession` already shipped in 1.x. |

---

## 2. Mapping Cupcake's concerns onto MAF seams

Legend for "Replaceable": **Full** = swap the implementation with a Cupcake or third-party one through a public abstract class or delegate; **Partial** = extension points exist but the model is constrained; **None** = MAF has nothing here.

### 2.1 Bot loop

| | |
|---|---|
| MAF seam | `AIAgent` (abstract: `RunAsync`, `RunStreamingAsync`, `CreateSessionAsync`, `SerializeSession`, `DeserializeSessionAsync`). Default loop is `ChatClientAgent` over MEAI `FunctionInvokingChatClient`. `HarnessAgent` (GA) composes loop, per-service-call history persistence, compaction, todo/mode providers, approval middleware, OTel. `LoopAgent` + `LoopEvaluators` for bounded outer loops. |
| Loop tunables | `MaximumIterations` on run options; `FunctionInvocationContext.Terminate` from function middleware; agent run middleware via `agent.AsBuilder().Use(runFunc, runStreamingFunc)`; Harness `Disable*` options for every default capability. |
| Replaceable | **Full.** Subclass `AIAgent`, or wrap with `DelegatingAIAgent` / run middleware. |
| Constraint | Function-calling middleware is only supported on agents built on `FunctionInvokingChatClient`. A fully bespoke loop forfeits tool middleware, approval handling, and every Harness feature. |
| Cupcake decision | `IChatLoopController` from `TECH_STACK_SUGGESTED.md` becomes a **thin port over `AIAgent`**, implemented as `DelegatingAIAgent` plus function middleware. Do not write a bespoke loop. |

### 2.2 Proxy / cache

| | |
|---|---|
| MAF/MEAI seam | `DelegatingChatClient`, `ChatClientBuilder.Use(...)`, `UseDistributedCache` (`DistributedCachingChatClient`), `UseLogging`, `UseOpenTelemetry`, `ConfigureOptions`. Agent-level: run middleware. Remote proxying: `A2AAgent` wraps a remote agent as an `AIAgent`. |
| Replaceable | **Full.** |
| Cupcake decision | Nothing to abstract. Cupcake owns only the pipeline composition policy (which decorators, in which order, per profile). |

### 2.3 Orchestration

| | |
|---|---|
| MAF seam | `Microsoft.Agents.AI.Workflows`: `Executor`, `WorkflowBuilder` edges, Sequential, Concurrent, Handoff (GA), GroupChat (subclass `RoundRobinGroupChatManager`, override `ShouldTerminateAsync`), Magentic (`MagenticWorkflowBuilder` with a manager agent). `workflow.AsAIAgent(...)` makes any orchestration an `AIAgent`. Human-in-the-loop via `RequestInfoEvent` / `SendResponseAsync`. |
| Replaceable | **Full.** A custom orchestrator is another `AIAgent`. |
| Cupcake decision | Funfetti's Build/Plan/Review modes are **context providers** (Harness `AgentModeProvider` pattern), not workflows. Workflows are reserved for multi-agent pipelines (Review agent critiques Build agent, background sub-agents). |

### 2.4 Planning methodology

| | |
|---|---|
| MAF seam | No planner abstraction. Harness provides `TodoProvider`, `AgentModeProvider` (plan / execute), `AgentSkillsProvider`, `BackgroundAgentsProvider`. Magentic manager keeps a task ledger. |
| Replaceable | **None** to replace; **slots** exist. |
| Cupcake decision | Define `IPlanningStrategy` in Core. Each implementation is an `AIContextProvider` that injects instructions, tools, and todo state per invocation. Concierge system prompt and Funfetti mode prompts are strategy configurations. |

### 2.5 Tool routing

| | |
|---|---|
| MAF/MEAI seam | Everything is `AITool` / `AIFunction`. Sources: `ChatOptions.Tools`, per-invocation tools returned by an `AIContextProvider` (`AIContext.Tools`), MCP (`McpClientTool` derives from `AIFunction`), other agents (`AsAIFunction`). |
| Replaceable | **Full.** |
| Cupcake decision | One adapter projects each `Xcaciv.Command` `ICommandDescription` (ordered / named parameters, flags, `IParameterValue<T>`) to `AIFunction` JSON schema via `AIFunctionFactory.Create(MethodInfo, createInstanceFunc, options)`. Per-mode visibility is a context provider. A curated allowlist, never "everything the parser knows". |

### 2.6 Policy enforcement and `AskOrExecute()`

| | |
|---|---|
| MAF/MEAI seam | `ApprovalRequiredAIFunction` makes `FunctionInvokingChatClient` emit `ToolApprovalRequestContent` instead of invoking; caller replies with `requestContent.CreateResponse(bool)` in a new user message on the same session. Function-calling middleware can deny (return an error result to the model) or `Terminate`. Harness adds `ToolApprovalAgent` with standing approvals, queued requests, and `ToolApprovalAgentOptions.AutoApprovalRules`. |
| Replaceable | **Partial.** Approval is binary and per function. The approval decision is made by `FunctionInvokingChatClient` **before** function middleware runs, so middleware cannot turn an allowed call into an ask. |
| Cupcake decision | `AskOrExecute()` on `ICommand` is the **default effect** for the command. The opencode rule engine (effective rule set = agent baked-in rules, then session rules, then user config; last-matching-rule-wins; `deny` beats `ask` beats `allow`; doom-loop guard) lives in Cupcake Core. It is enforced by a Cupcake `PolicyGatedAIFunction` wrapper that evaluates per call and per argument and yields one of: invoke, `ToolApprovalRequestContent`, or a policy error result. Model the verdicts as `allow` / `deny` / `transform` / `escalate` to match AGENT-HOOKS-0.1 so the .NET Agent Hooks port can be adopted when it ships. |

### 2.7 Interoperability protocols

| Protocol | MAF support | Cupcake work |
|---|---|---|
| MCP (client) | Official `ModelContextProtocol` SDK; `McpClientTool` is an `AIFunction`. `Microsoft.Agents.AI.Mcp` (alpha) adds long-running tasks (2026-07-28 Tasks extension) and skills. | Register servers from config; per-server trust level feeds the rule engine. |
| MCP (server) | Not a MAF package. `ModelContextProtocol` server hosting accepts `McpServerTool.Create(AIFunction)`. | Expose the curated command allowlist as an MCP server (Lit and Funfetti `mcp` subcommand). |
| A2A (client) | `Microsoft.Agents.AI.A2A` (preview): `A2ACardResolver` + `GetAIAgentAsync` returns an `AIAgent`. | None. |
| A2A (server) | `Microsoft.Agents.AI.Hosting.A2A.AspNetCore` (preview): `AddA2AServer(name)`, `MapA2AHttpJson` / `MapA2AJsonRpc`. Keyed DI overrides for `IAgentHandler`, `AgentSessionStore`, `ITaskStore`. Background responses not yet supported. | Provide durable `AgentSessionStore` and `ITaskStore` over the SQLite event log. |
| AG-UI | `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` (preview). | Optional web front end later. |
| OpenAI-compatible HTTP | `Microsoft.Agents.AI.Hosting.OpenAI` (alpha): `MapOpenAIResponses`. | Optional REST input plugin. |
| ACP (Zed Agent Client Protocol) | **None.** JSON-RPC 2.0 over stdio. Community .NET packages exist (`dotacp.*` 2026.7.19, `AcpKit.*` 0.0.4, `AgentClientProtocol` 0.1.5), all small and unofficial. | Custom adapter: ACP session ops map to `AgentSession` + `AgentSessionStore`; `prompt` maps to `RunStreamingAsync`; permission requests map to the rule engine's `ask` effect. See `../opencode/dossiers/ide-integration-acp.md`. |
| ANP (Agent Network Protocol) | **None.** No .NET implementation exists anywhere. | Entirely custom if ever pursued. Not planned. |

Observation: every MAF protocol adapter is the same shape as Cupcake's planned input-interface plugin: translate the wire format, resolve a session by continuation id, call `AIAgent.RunStreamingAsync`, persist the session. Cupcake defines `IAgentTransport` (or similar) in Core; MAF's adapters become reference implementations, not dependencies of the contract.

### 2.8 Model routing

| | |
|---|---|
| MEAI seam | MEAI 10.9.0 `RoutingChatClient` (override `SelectClientAsync(RoutingContext, ct)`), `SemanticRoutingChatClient`, `FailoverChatClient`, `OrderedFailoverChatClient`. All `[Experimental("MEAI001")]`. MAF 1.19 added session-persisted chat client routing. |
| Constraint | A router wrapped **around** `FunctionInvokingChatClient` selects once per run; place it inside for per-iteration routing. Streaming failures after first token are terminal. |
| Replaceable | **Full.** |
| Cupcake decision | Cupcake owns the model catalog (provider / model / pricing / context window / capability flags including logprobs and top-k), the credential lookup, and the routing policy. The mechanism is MEAI's. |

### 2.9 State management

| | |
|---|---|
| MAF seam | `AgentSession` (serializable; `ChatClientAgentSession` carries a `StateBag`). `ProviderSessionState<T>` for per-provider state keyed by `StateKey`. `AgentSessionStore` (abstract `SaveSessionAsync` / `GetSessionAsync` / `DeleteSessionAsync`) for hosted continuation ids, wrapped by `IsolationKeyScopedAgentSessionStore`. Workflows: `ICheckpointStore`, `JsonCheckpointStore`, `FileSystemJsonCheckpointStore`, `CheckpointManager.CreateJson(store)`. |
| Gaps | No durable store ships. Checkpoints are in-memory by default; no distributed agent runtime yet. Known issues serializing sessions with pending approvals (agent-framework #2365, #5189). |
| Replaceable | **Full.** |
| Cupcake decision | The SQLite durable, replayable event log from the opencode dossier is the system of record. It is exposed to MAF through three adapters: `AgentSessionStore`, `ChatHistoryProvider`, and `JsonCheckpointStore`. Session state is projected from events; MAF never sees the log directly. |

### 2.10 Memory

| | |
|---|---|
| MAF seam | `AIContextProvider` (`InvokingCoreAsync` returns `AIContext` of instructions, messages, tools; `InvokedCoreAsync` observes results). `ChatHistoryProvider` (`ProvideChatHistoryAsync` / `StoreChatHistoryAsync`). `ChatHistoryMemoryProvider` over `Microsoft.Extensions.VectorData`. `Microsoft.Agents.AI.Mem0` (preview). Harness `FileMemoryProvider`. Compaction: `CompactionProvider` with `Truncation`, `SlidingWindow`, `ToolResult`, `Summarization`, `Pipeline` strategies; `CompactionStrategy` is subclassable. |
| Constraint | Register `CompactionProvider` on the `ChatClientBuilder` via `UseAIContextProviders`, not on `ChatClientAgentOptions`; the latter persists synthetic summaries into stored history. |
| Replaceable | **Full.** |
| Cupcake decision | Storage backend and compaction policy are configuration. Cross-session memory (opencode "durable state") is a Cupcake `AIContextProvider` over the SQLite store. |

### 2.11 Messaging

| | |
|---|---|
| MAF seam | In-process message passing between workflow executors. Durable Task extension for cross-process (Durable Task Scheduler or Azure Functions). No ESB abstraction. |
| Replaceable | **None.** |
| Cupcake decision | REST, ESB, and reverse-shell input plugins are entirely Cupcake's. They only need to call `RunStreamingAsync` and persist a session. |

---

## 3. The eight abstract component layers

| Layer | MAF / MEAI provides | Cupcake provides |
|---|---|---|
| Model access | `IChatClient`, `IEmbeddingGenerator`, provider adapters, routing and failover clients | Model catalog, credential resolution, logprob capture middleware (see §5) |
| Tool access | `AITool` / `AIFunction`, MCP client tools, agent-as-tool | `Xcaciv.Command` to `AIFunction` projection, allowlist |
| Orchestration | `AIAgent`, Workflows, Harness, `LoopAgent` | Mode configuration, planning strategies |
| Durability | Session serialization, `AgentSessionStore`, `ICheckpointStore`, Durable Task extension (Azure-oriented, preview) | SQLite event log and its three MAF adapters |
| Policy enforcement | `ApprovalRequiredAIFunction`, function middleware, Harness auto-approval rules. Agent Hooks not in .NET. | Rule engine, `PolicyGatedAIFunction`, doom-loop guard, audit via `Xcaciv.Command` `IAuditLogger` |
| Identity | `AgentIsolationKeyProvider`, claims-based isolation for ASP.NET hosts, provider SDK credentials | Principal model, credential / integration store, per-session isolation keys. Entra Agent ID optional. |
| Siloing | None in-process. Hyperlight preview. | Working-directory boundaries, per-session isolation, the "no shell execution" rule, `Xcaciv.Loader` load-time admission control |
| Observability | MEAI `OpenTelemetryChatClient`, MAF `OpenTelemetryAgent` (`UseOpenTelemetry`), workflow events, GenAI semantic conventions | `ActivitySource` / `Meter` naming, always-instrumented never-exported default, NDJSON diagnostics log |

---

## 4. Architecture decisions

| ID | Decision | Consequence |
|---|---|---|
| MAF-1 | Layer 1 `Core` depends on `Microsoft.Extensions.AI.Abstractions` and `Microsoft.Agents.AI.Abstractions`. | Departs from `TECH_STACK_SUGGESTED.md`'s "no framework types in Core" for these two packages only. Both are BCL-style abstraction packages with no transitive dependencies on .NET 10. `ILanguageModel`, `IEmbeddingModel`, `IChatSession` are **not** created; `IChatClient`, `IEmbeddingGenerator`, `AgentSession` are used directly. Provider registry and extension-point contracts remain Cupcake's. |
| MAF-2 | The agent loop is `ChatClientAgent` or `HarnessAgent`, never a bespoke loop. | `IChatLoopController` is a port over `AIAgent`. Retain function middleware and Harness features. |
| MAF-3 | Policy is a Cupcake rule engine enforced by an `AIFunction` wrapper. | `AskOrExecute()` is the per-command default effect; rules override. Verdict model mirrors AGENT-HOOKS-0.1. |
| MAF-4 | SQLite event log is the system of record; MAF sees it through `AgentSessionStore`, `ChatHistoryProvider`, `JsonCheckpointStore`. | Replay, fork, and share semantics stay Cupcake's. |
| MAF-5 | Transports are Cupcake plugins implementing one Core contract; MAF hosting adapters are optional references. | ACP and REST ship first; A2A and AG-UI ride MAF prerelease packages when needed. |
| MAF-6 | Token introspection stays at the `IChatClient` layer (chatdbg ADR-4 upheld). | See §5. |
| MAF-7 | Pin MAF and MEAI versions together; upgrade in lockstep. | Weekly minor releases and a breaking rename in 1.x make floating versions unsafe. |

---

## 5. Reconciling chatdbg ADR-4

`../chatdbg/TECH-STACK-TARGET.md` rejected MAF and Semantic Kernel for ChatDbg because "agent frameworks are built to hide the model call, and this application's product *is* the model call." That reasoning was correct for ChatDbg and remains valid for the ChatDbg features folded into Cupcake (probability logs, top-k, token inspection).

Why the two decisions coexist:

- Cupcake's product is the **agent**; ChatDbg's was the model call. MAF fits Cupcake.
- MAF does not touch the wire. `ChatClientAgent` calls whatever `IChatClient` it is given. chatdbg's `ITokenIntrospectingBackend` port and its `ITokenTelemetryExtractor` discovered via `GetService` sit **below** the agent, inside the `ChatClientBuilder` pipeline.
- `FunctionInvokingChatClient` collapses N round trips into one `ChatResponse` with a single `RawRepresentation`. Logprob capture must therefore be a chat-client middleware placed **inside** the function-invocation decorator, harvesting per streaming update. This was already chatdbg's design; MAF changes nothing about it.
- The Harness's per-service-call history persistence is compatible: it persists after each model call, which is the same granularity the introspection channel needs.

Rule: **anything that needs byte-level or per-round-trip visibility is chat-client middleware; anything that needs conversation-level control is agent middleware or a context provider.**

---

## 6. Risks and caveats

| Risk | Severity | Mitigation |
|---|---|---|
| Hosting, A2A, Mcp, Anthropic, DurableTask packages are prerelease | Medium | Keep them out of Layer 1 and Layer 2. Only Host projects and transport plugins reference them. |
| Compaction and routing are experimental (`MAAI001`, `MEAI001`) | Low | Wrap behind Cupcake options so a pragma lives in one file. |
| Function middleware requires `FunctionInvokingChatClient` | Medium | MAF-2. |
| Session serialization with pending approvals is buggy | Medium | Event log stores approval requests as events; session store rehydrates without relying on MAF serialization of `ToolApprovalRequestContent`. Re-test on each MAF upgrade. |
| Checkpoints in-memory by default; no distributed runtime | Low for terminal products; Medium for Sommelier-hosted agents | `JsonCheckpointStore` over SQLite. Do not plan multi-node agent hosting on MAF yet. |
| Release cadence and breaking changes in 1.x | Medium | MAF-7. Central package management. Read release notes every upgrade. |
| Agent Hooks absent in .NET | Low | MAF-3 verdict model keeps the door open. |
| `Microsoft.Agents.AI.Mcp` pins `ModelContextProtocol` 2.1.0 while 2.2.0 is current | Low | NuGet resolves upward; test. Use the MAF MCP package only for long-running tasks. |
| Hyperlight unavailable; no sandbox | Accepted | Product rule: no Cupcake agent executes shell commands. Third-party tools run as MCP servers in their own process. |

---

## 7. Sources

- MAF 1.0 announcement: https://devblogs.microsoft.com/agent-framework/microsoft-agent-framework-version-1-0/
- BUILD 2026 announcements (Harness GA, Hosted Agents, CodeAct): https://devblogs.microsoft.com/agent-framework/microsoft-agent-framework-at-build-2026-announce/
- Layered SDK design (loops, workflows, harnesses): https://commandline.microsoft.com/agent-framework-layered-sdk-loops-workflows-harnesses/
- Middleware: https://learn.microsoft.com/en-us/agent-framework/agents/middleware/
- Agent Harness: https://learn.microsoft.com/en-us/agent-framework/concepts/harness
- Agent Hooks: https://learn.microsoft.com/en-us/agent-framework/agents/agent-hooks
- Tool approval: https://learn.microsoft.com/en-us/agent-framework/agents/tools/tool-approval
- Conversations and memory: https://learn.microsoft.com/en-us/agent-framework/agents/conversations/
- Compaction: https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/compaction
- Self-hosting and `AgentSessionStore`: https://learn.microsoft.com/en-us/agent-framework/hosting/self-hosting/
- A2A hosting (.NET): https://learn.microsoft.com/en-us/agent-framework/hosting/self-hosting/a2a/dotnet
- A2A agent package: https://www.nuget.org/packages/Microsoft.Agents.AI.A2A
- Durable extension: https://learn.microsoft.com/en-us/agent-framework/hosting/azure-functions
- Checkpoint limitations discussion: https://github.com/microsoft/agent-framework/discussions/2305
- MEAI routing and failover: https://devblogs.microsoft.com/dotnet/routing-and-failover-for-microsoft-extensions-ai/
- Releases: https://github.com/microsoft/agent-framework/releases
- Hyperlight package: https://github.com/microsoft/agent-framework/tree/main/dotnet/src/Microsoft.Agents.AI.Hyperlight
- Package metadata verified against api.nuget.org flat container on 2026-09-07.
- ACP: https://zed.dev/acp
- ANP: https://github.com/agent-network-protocol/AgentNetworkProtocol
