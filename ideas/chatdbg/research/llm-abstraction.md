# LLM client abstraction and multi-provider orchestration

*Research date: 2026-08-28. Every version number, publish date and capability claim below was checked against nuget.org, the NuGet v3 registration API, live vendor documentation, or by disassembling the shipped assembly. Items I could not verify are listed under **Unconfirmed**, not guessed.*

---

## Bottom line — the recommendation in three sentences

Code the application's conversation, history, settings and tool-hosting layer against **`Microsoft.Extensions.AI.Abstractions` 10.9.0 `IChatClient`** (GA, shipped 2026-08-11), because it is the only abstraction that all three of your backends adapt to today and it gives you the `ChatClientBuilder` middleware pipeline you need for function invocation, telemetry and logging without buying an agent runtime you do not want. But **`IChatClient` cannot carry per-token log probabilities** — there is no `logprobs` property on `ChatOptions`, `ChatResponse`, `ChatMessage` or `ChatResponseUpdate`, and I verified this against the 10.9.0 API surface — so the token-introspection feature, which is this application's whole reason to exist, must be built as a **second, parallel capability channel**: a `DelegatingChatClient` placed *innermost* in the pipeline that reads `RawRepresentation` off each streaming update, downcasts it to the provider's native type, and projects a typed `TokenTelemetry` record into `ChatResponse.AdditionalProperties` (which *is* JSON-serialisable, unlike `RawRepresentation`, which is `[JsonIgnore]`). Request-side, you inject the provider's native options object through **`ChatOptions.RawRepresentationFactory`** — for OpenAI that means handing back a `ChatCompletionOptions { IncludeLogProbabilities = true, TopLogProbabilityCount = 20 }`, and for Amazon Bedrock it means accepting that **no Bedrock API — Converse, ConverseStream or InvokeModel on any first-party foundation model — returns log probabilities at all**, so Bedrock is a degraded-telemetry backend by design and your UI must say so.

---

## Landscape — the real options

### Core abstraction

| Package | Latest stable | Published | Status | Verdict |
|---|---|---|---|---|
| `Microsoft.Extensions.AI.Abstractions` | **10.9.0** | 2026-08-11 | **GA** (first stable 9.5.0, 2025-05-16) | The right contract to code against. `IChatClient`, `ChatOptions`, `ChatResponse`. Targets net8/9/10 + netstandard2.0 + net462. On .NET 10 it has **zero transitive dependencies**. |
| `Microsoft.Extensions.AI` | **10.9.0** | 2026-08-11 | **GA** | The middleware layer: `ChatClientBuilder`, `FunctionInvokingChatClient`, `DistributedCachingChatClient`, `OpenTelemetryChatClient`, `LoggingChatClient`, `DelegatingChatClient`. Reference this from the host, not from your tool libraries. |

Sources: [nuget.org/packages/Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI/), [nuget.org/packages/Microsoft.Extensions.AI.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.AI.Abstractions/), [GA announcement, 2025-05-21](https://devblogs.microsoft.com/dotnet/ai-vector-data-dotnet-extensions-ga/).

### Provider adapters — which actually ship an `IChatClient`

| Adapter | Latest | Published | Status | Logprobs reachable? | Verdict |
|---|---|---|---|---|---|
| `Microsoft.Extensions.AI.OpenAI` | **10.9.0** | 2026-08-11 | **GA** | **Yes**, via `RawRepresentation` | First-party, tracks `OpenAI` 2.12.x (`[2.12.0, 2.13.0)`). `AsIChatClient()` on `OpenAI.Chat.ChatClient`. Use this. |
| `OpenAI` (official SDK) | **2.13.0** | 2026-08-10 | **GA** | **Yes**, natively typed | The logprob-bearing layer. Also the *currently recommended* way to hit Azure OpenAI (see below). |
| `Azure.AI.OpenAI` | **2.1.0** | **2024-12-06** | **GA but stale — 20 months without a stable release** | via the OpenAI types it re-exports | Latest stable is from Dec 2024. Ten prereleases since; newest is `2.9.0-beta.1` (2026-03-13). **Do not take a hard dependency.** |
| `AWS.Bedrock.MEAI` | **1.0.0** | **2026-08-11** | **GA, 17 days old** | **No** — provider has none to give | The AWS-blessed successor. `AmazonBedrockRuntimeClient.AsIChatClient(modelId)` → Converse/ConverseStream. Repo: [aws/aws-dotnet-ai](https://github.com/aws/aws-dotnet-ai). |
| `AWSSDK.Extensions.Bedrock.MEAI` | 4.0.101.8 | 2026-08-11 | **DEPRECATED** (every version flagged) | No | Renamed and relocated. NuGet's deprecation record names `AWS.Bedrock.MEAI` as the replacement. Do not start here. |
| `AWSSDK.BedrockRuntime` | **4.0.101.4** | **2026-08-24** | **GA, actively shipping** | **No — verified by disassembly** | The transport. Needed underneath `AWS.Bedrock.MEAI`. |
| `LLamaSharp` | **0.27.0** | 2026-04-26 | **Active but pre-1.0** | **Yes — and richer than any hosted API** | GGUF via llama.cpp (pinned to llama.cpp commit `3f7c29d`). Ships `LLamaExecutorExtensions.AsChatClient()`. Depends on `Microsoft.Extensions.AI.Abstractions` 10.4.1. |
| `OllamaSharp` | **5.4.30** | 2026-07-24 | **GA, actively shipping** | **Yes**, natively typed | Implements `IChatClient` explicitly on `OllamaApiClient`. Has typed `Logprobs`/`TopLogprobs`/`Logprob`. Not in your brief's backend list but worth knowing. |
| `Microsoft.ML.OnnxRuntimeGenAI(.Managed)` | **0.15.2** | 2026-08-06 | **GA** | Not through its `IChatClient` | Ships `OnnxRuntimeGenAIChatClient`. **Consumes ONNX, not GGUF** — wrong format for your stated local-model requirement. |
| `Microsoft.Extensions.AI.Ollama` | 9.7.0-preview.1.25356.2 | 2025-07-08 | **DEPRECATED** | — | NuGet deprecation message points to `OllamaSharp`. Dead end. |
| `Microsoft.Extensions.AI.AzureAIInference` | 10.0.0-preview.1.25559.3 | 2025-11-11 | **Preview, stalled ~9 months** | — | The underlying Azure AI Inference beta SDK is slated for retirement 2026-08-26. Avoid. |

### Orchestration frameworks (the layer above `IChatClient`)

| Package | Latest | Published | Status | Verdict for this app |
|---|---|---|---|---|
| `Microsoft.Agents.AI` (Microsoft Agent Framework) | **1.19.0** | 2026-08-22 | **GA** (1.0 on 2026-04-03) | Overkill. It is an *agent* runtime — threads, workflows, multi-agent handoff, A2A. You need none of it. |
| `Microsoft.SemanticKernel` | **1.80.0** | 2026-08-18 | **GA but in maintenance-plus** | Officially superseded by Agent Framework for new work; critical fixes only, supported ≥1 year past AF GA. Do not start a 2026 greenfield here. |
| raw `Microsoft.Extensions.AI` | 10.9.0 | 2026-08-11 | GA | **This one.** |

---

## Analysis

### 1. Microsoft.Extensions.AI — contract, pipeline, streaming, provider coverage

**Status.** GA since `9.5.0` (2025-05-16), announced [2025-05-21](https://devblogs.microsoft.com/dotnet/ai-vector-data-dotnet-extensions-ga/). Now at 10.9.0 tracking the .NET 10 release train. The version-number jump 9.x → 10.x is train alignment, not a rewrite.

**The contract.** `IChatClient` has exactly three members that matter:

```csharp
Task<ChatResponse> GetResponseAsync(
    IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default);

IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
    IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default);

object? GetService(Type serviceType, object? serviceKey = null);   // service-discovery escape hatch
```

`GetService` is the under-used member and it matters for you — it lets a decorator or the app ask a pipeline "does anything in here implement `ITokenTelemetryExtractor`?" without type-sniffing the whole chain. Use it.

**The pipeline.** `ChatClientBuilder` composes `DelegatingChatClient`s. Confirmed extension methods on the current docs ([learn.microsoft.com/dotnet/ai/ichatclient](https://learn.microsoft.com/en-us/dotnet/ai/ichatclient), doc updated 2026-08-19):

| Method | Type it installs | Package |
|---|---|---|
| `.UseFunctionInvocation()` | `FunctionInvokingChatClient` | `Microsoft.Extensions.AI` |
| `.UseDistributedCache(IDistributedCache)` | `DistributedCachingChatClient` | `Microsoft.Extensions.AI` + a cache impl |
| `.UseOpenTelemetry(sourceName:, configure:)` | `OpenTelemetryChatClient` | `Microsoft.Extensions.AI` |
| `.UseLogging(ILoggerFactory)` | `LoggingChatClient` | `Microsoft.Extensions.AI` |
| `.ConfigureOptions(Action<ChatOptions>)` | options-defaulting delegator | `Microsoft.Extensions.AI` |
| `.Use(...)` | your own delegate or factory | `Microsoft.Extensions.AI` |

Also present in 10.9.0 and marked **experimental**: `MessageCountingChatReducer` / `SummarizingChatReducer` (history trimming) and `RoutingChatClient` / `FailoverChatClient` / `OrderedFailoverChatClient` (multi-provider failover). The failover clients are tempting for a three-backend shell — but they are experimental, and a debugging tool that silently fails over to a *different model* would corrupt the very telemetry you are collecting. Do not use them; make backend selection explicit and user-visible.

**Ordering is load-bearing, not stylistic.** Because `FunctionInvokingChatClient` performs multiple round-trips to the inner client and collapses them into one `ChatResponse` that has exactly **one** `RawRepresentation` slot, a telemetry decorator placed *outside* it sees only the final round-trip. And because `DistributedCachingChatClient` round-trips the response through `System.Text.Json` (verified: `DistributedCachingChatClient.cs` calls `JsonSerializer.SerializeToUtf8Bytes(value, ...typeof(ChatResponse))`), and `RawRepresentation` is `[JsonIgnore]` (verified in `ChatResponse.cs` line 111), **every cache hit returns a response with no raw representation and therefore no logprobs**. The correct order is:

```
UseLogging → UseOpenTelemetry → UseDistributedCache → UseFunctionInvocation → UseTokenTelemetry → <provider>
```

Telemetry innermost, adjacent to the provider. Then project the captured telemetry into `AdditionalProperties` so it survives caching and history persistence.

**Streaming.** `GetStreamingResponseAsync` returns `IAsyncEnumerable<ChatResponseUpdate>`. `ChatResponseUpdate` carries its own `RawRepresentation` and `AdditionalProperties`. The abstraction's own XML docs warn that `ToChatResponseAsync()` is lossy in exactly the way that hurts you:

> "the provided conversions might be lossy, for example, if multiple updates all have different `RawRepresentation` objects whereas there's only one slot for such an object available in `ChatResponse.RawRepresentation`."
> — [`ChatResponseUpdate` remarks, 10.9.0](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.chatresponseupdate?view=net-10.0-pp)

**This is the single most important operational fact in this document.** In a streaming session, logprobs arrive spread across N updates. If you buffer the stream and call `ToChatResponseAsync()`, you keep one raw object and throw away N−1. You must harvest per-update, as the stream flows.

**Provider adapter matrix — each verified individually:**

| Backend | Ships an `IChatClient`? | Package + entry point | Evidence |
|---|---|---|---|
| OpenAI (hosted) | **Yes** | `Microsoft.Extensions.AI.OpenAI` 10.9.0 → `new OpenAI.Chat.ChatClient(...).AsIChatClient()` | [package README](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI.OpenAI/README.md) |
| Azure OpenAI | **Yes**, via the same adapter | `OpenAI.Chat.ChatClient` pointed at `https://<res>.openai.azure.com/openai/v1/` then `.AsIChatClient()` | [Azure supported-languages doc, updated 2026-07-22](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/supported-languages) |
| Amazon Bedrock | **Yes** | `AWS.Bedrock.MEAI` 1.0.0 → `AmazonBedrockRuntimeClient.AsIChatClient(modelId)` | verified: `AsIChatClient` string present in shipped assembly |
| Ollama | **Yes** | `OllamaSharp` 5.4.30 — `OllamaApiClient` implements `IChatClient` explicitly | verified in assembly |
| ONNX Runtime GenAI | **Yes** | `Microsoft.ML.OnnxRuntimeGenAI.Managed` 0.15.2 → `OnnxRuntimeGenAIChatClient` | verified: type present; nuspec depends on `MEAI.Abstractions` 9.8.0 |
| LLamaSharp (GGUF) | **Yes** | `LLamaSharp` 0.27.0 → `LLamaExecutorExtensions.AsChatClient()` on an `ILLamaExecutor` | verified: `LLamaExecutorChatClient` + `AsChatClient` in assembly, nuspec depends on `MEAI.Abstractions` 10.4.1 |

All six confirmed. The abstraction genuinely does cover your three named backends plus two you did not ask about.

---

### 2. **Can `Microsoft.Extensions.AI` carry per-token log probabilities?**

**No, not in any typed form — the data is dropped at the abstraction boundary, and it is recoverable only through `RawRepresentation`.**

I enumerated the complete public surface of all four candidate types against `Microsoft.Extensions.AI.Abstractions` v10.9.0:

| Type | Every property | Any logprob member? |
|---|---|---|
| `ChatOptions` | `AdditionalProperties`, `AllowBackgroundResponses`, `AllowMultipleToolCalls`, `ContinuationToken`, `ConversationId`, `FrequencyPenalty`, `Instructions`, `MaxOutputTokens`, `ModelId`, `PresencePenalty`, `RawRepresentationFactory`, `Reasoning`, `ResponseFormat`, `Seed`, `StopSequences`, `Temperature`, `ToolMode`, `Tools`, `TopK`, `TopP` | **None** |
| `ChatResponse` | `AdditionalProperties`, `ContinuationToken`, `ConversationId`, `CreatedAt`, `FinishReason`, `Messages`, `ModelId`, `RawRepresentation`, `ResponseId`, `Text`, `Usage` | **None** |
| `ChatResponseUpdate` | `AdditionalProperties`, `AuthorName`, `Contents`, `ContinuationToken`, `ConversationId`, `CreatedAt`, `FinishReason`, `MessageId`, `ModelId`, `RawRepresentation`, `ResponseId`, `Role`, `Text` | **None** |
| `UsageDetails` (via `ChatResponse.Usage`) | token *counts* only | **None** |

> **Trap: `ChatOptions.TopK` is not what you want.** Its own documentation reads *"the number of most probable tokens that the model considers when generating the next part of the text"* — that is **top-k sampling**, a generation-time constraint. It is not `top_logprobs`, which is a *reporting* parameter asking the service to disclose the K best alternatives it considered. Setting `ChatOptions.TopK = 20` changes what the model generates and returns you nothing. Confusing these two would be a silent, plausible-looking bug in a token-introspection tool.

**Precisely how a caller recovers logprobs through the abstraction.**

*Request side — `ChatOptions.RawRepresentationFactory`.* This is a `Func<IChatClient, object?>`. Verified in `OpenAIChatClient.ToOpenAIOptions` (dotnet/extensions `main`, line 605):

```csharp
if (options.RawRepresentationFactory?.Invoke(this) is not ChatCompletionOptions result)
{
    result = new();
}
result.FrequencyPenalty ??= options.FrequencyPenalty;   // note ??= — your values win
result.MaxOutputTokenCount ??= options.MaxOutputTokens;
// ...
```

Every mapped property uses `??=`, so **anything you set on the raw options object is preserved and the abstraction fills only the gaps**. That is the sanctioned injection point:

```csharp
var options = new ChatOptions
{
    Temperature = 0.7f,
    RawRepresentationFactory = _ => new ChatCompletionOptions
    {
        IncludeLogProbabilities = true,
        TopLogProbabilityCount   = 20,   // OpenAI's documented max
    }
};
```

The same mechanism exists on the Responses path (`OpenAIResponsesChatClient`, line 963, expecting a `CreateResponseOptions`) and on `AWS.Bedrock.MEAI` (verified: `get_RawRepresentationFactory` present in the assembly).

*Response side — `RawRepresentation`.* Verified assignments in `Microsoft.Extensions.AI.OpenAI` on `main`:

| Location | Assignment | Gives you |
|---|---|---|
| `OpenAIChatClient.cs:506` and `:583` | `ChatMessage.RawRepresentation` / `ChatResponse.RawRepresentation = openAICompletion` | `OpenAI.Chat.ChatCompletion` → `.ContentTokenLogProbabilities` |
| `OpenAIChatClient.cs:383` | `ChatResponseUpdate.RawRepresentation = update` | `OpenAI.Chat.StreamingChatCompletionUpdate` → `.ContentTokenLogProbabilities` |
| `OpenAIResponsesChatClient.cs:144` | `ChatResponse.RawRepresentation = responseResult` | `OpenAI.Responses.ResponseResult` |
| `OpenAIResponsesChatClient.cs:397` | `ChatResponseUpdate.RawRepresentation = streamingUpdate` | `StreamingResponseOutputTextDeltaUpdate` → `.TokenLogProbabilities` |
| `OpenAIResponsesChatClient.cs:1742` | `AIContent.RawRepresentation = part` | `ResponseContentPart` → `.OutputTextTokenLogProbabilities` |

So the recovery code is a downcast:

```csharp
await foreach (var update in client.GetStreamingResponseAsync(history, options, ct))
{
    if (update.RawRepresentation is StreamingChatCompletionUpdate raw &&
        raw.ContentTokenLogProbabilities is { Count: > 0 } lps)
    {
        foreach (ChatTokenLogProbabilityDetails lp in lps)
        {
            // lp.Token          : string
            // lp.LogProbability : float
            // lp.Utf8Bytes      : ReadOnlyMemory<byte>?   <- the byte-exact token, for tokenisation analysis
            // lp.TopLogProbabilities : IReadOnlyList<ChatTokenTopLogProbabilityDetails>
            //        each with .Token, .LogProbability, .Utf8Bytes
        }
    }
    Render(update.Text);
}
```

**Three hard constraints on that recovery, all verified:**

1. `RawRepresentation` is `[JsonIgnore]` on `ChatResponse`, `ChatResponseUpdate` **and** `ChatMessage` (`ChatResponse.cs:111`, `ChatResponseUpdate.cs:115`, `ChatMessage.cs:99`). It never survives serialisation. Your conversation-history store will not persist it. Your response cache will not return it.
2. `AdditionalProperties` (`AdditionalPropertiesDictionary`) is **not** `[JsonIgnore]` and does serialise. It is the correct carrier for telemetry you want to keep. On deserialisation values return as `JsonElement`, so define an explicit DTO and a `JsonSerializerContext` rather than relying on round-trip typing.
3. The abstraction has no `logprobs` request property, so **you cannot ask for logprobs generically**. Each provider needs its own `RawRepresentationFactory` lambda. That is a per-provider strategy object in your design, not an if-statement.

**Is Microsoft going to fix this?** I searched `dotnet/extensions` issues for "logprobs": **two results, both closed, neither about logprobs** (#7291 "How to access chat.completion.chunk properties that aren't serialized to ChatResponseUpdate?", closed 2026-02-13; #6208 about `reasoning_content`, closed 2025-03-28). There is no open tracking issue and no sign of typed logprob support landing. **Design as though it never will.** The pattern in #7291 — user wants an unmapped provider field, is pointed at `RawRepresentation` — is the established answer.

---

### 3. Semantic Kernel vs Microsoft Agent Framework vs raw Microsoft.Extensions.AI

Microsoft's own positioning, from the Agent Framework team's post ([devblogs.microsoft.com/agent-framework, 2025-10-07](https://devblogs.microsoft.com/agent-framework/semantic-kernel-and-microsoft-agent-framework/)): Agent Framework is *"the successor to Semantic Kernel for building AI agents"*, built by the same team, *"think of it as Semantic Kernel v2.0"*. SK gets critical bugs and security fixes and support for *"at least one year after Microsoft Agent Framework leaves Preview and is Generally Available"*; *"the majority of new features will be built for Microsoft Agent Framework."* Agent Framework 1.0 GA'd 2026-04-03; `Microsoft.Agents.AI` is at **1.19.0** (2026-08-22), shipping roughly weekly. `Microsoft.SemanticKernel` is at **1.80.0** (2026-08-18) and still moving, but its charter is narrowing.

The three layers, honestly described:

| Layer | What it is *for* | Cost to you |
|---|---|---|
| **`Microsoft.Extensions.AI`** | A model-call abstraction plus a middleware pipeline. It calls a model, it invokes functions, it emits telemetry. It has no opinion about agents, threads, memory or planning. | You write your own turn loop, history model and settings — which you were going to write anyway, and which is where a *debugger* wants full control. |
| **Microsoft Agent Framework** | An agent runtime: `AIAgent`, agent threads with server-side state, workflows, handoff/group-chat orchestration, A2A, hosted-agent deployment. | Buys you nothing here and imposes its own conversation-state model on top of yours. `ChatClientAgent` wraps *any* `IChatClient`, so nothing is lost by skipping it — you can adopt it later over the same clients. |
| **Semantic Kernel** | Kernel/plugins/planners/filters, the 2023-era plugin abstraction. | Its plugin model directly duplicates `Xcaciv.Command` + `Xcaciv.Loader`. Two competing tool systems in one binary is a maintenance tax with no offsetting benefit, on a framework Microsoft has told you is in succession. |

**Verdict for this application: raw `Microsoft.Extensions.AI`.** The decisive argument is not "less is more" — it is that **agent frameworks are built to hide the model call, and this application's product is the model call.** Agent Framework's value proposition is abstracting away round-trips, tool loops and state so you can think about goals. You need the opposite: byte-exact visibility into every round-trip, every token, every alternative the sampler rejected. Adding a layer whose job is to normalise and summarise that away, and then reaching around it, is strictly worse than not adding it. You also already have tool definition, discovery and execution (`Xcaciv.Command`, `Xcaciv.Loader`) — SK's plugins and AF's `AIFunction` registry would compete with them, not complement them.

*Second best: Microsoft Agent Framework.* It wins the moment the product grows a feature that needs multiple cooperating models — a "critic" model scoring a "generator" model's tokens, a debugging workflow with fan-out/fan-in over several candidate completions, or server-side hosted threads. `ChatClientAgent` sits on the same `IChatClient` instances you would build anyway, so this is an additive migration, not a rewrite. Semantic Kernel is third and I would not start there in 2026.

---

### 4. The official OpenAI .NET SDK and `Azure.AI.OpenAI`

**`OpenAI` 2.13.0** (published 2026-08-10, GA, MIT, owner `OpenAIOfficial`, targets netstandard2.0 / net8.0 / net10.0). Confirmed tag `OpenAI_2.13.0` in [openai/openai-dotnet](https://github.com/openai/openai-dotnet). Note that `Microsoft.Extensions.AI.OpenAI` 10.9.0 pins `[2.12.0, 2.13.0)` — **it has not yet floated to 2.13.0**, so referencing both pulls 2.12.0 unless you override, and overriding to 2.13.0 is unsupported until the MEAI adapter widens the range.

**Logprobs on Chat Completions** — exact names, read from `api/released/net10.0/OpenAI.Chat.net10.0.cs`:

*Request* — `OpenAI.Chat.ChatCompletionOptions`:
- `public bool? IncludeLogProbabilities { get; set; }`
- `public int? TopLogProbabilityCount { get; set; }` — integer 0–20; requires `IncludeLogProbabilities = true`

*Response* — `OpenAI.Chat.ChatCompletion` and `OpenAI.Chat.StreamingChatCompletionUpdate` both expose:
- `public IReadOnlyList<ChatTokenLogProbabilityDetails> ContentTokenLogProbabilities { get; }`
- `public IReadOnlyList<ChatTokenLogProbabilityDetails> RefusalTokenLogProbabilities { get; }`

*Shapes:*
```csharp
public class ChatTokenLogProbabilityDetails {
    public string Token { get; }
    public float  LogProbability { get; }
    public ReadOnlyMemory<byte>? Utf8Bytes { get; }
    public IReadOnlyList<ChatTokenTopLogProbabilityDetails> TopLogProbabilities { get; }
}
public class ChatTokenTopLogProbabilityDetails {
    public string Token { get; }
    public float  LogProbability { get; }
    public ReadOnlyMemory<byte>? Utf8Bytes { get; }
}
```

`Utf8Bytes` is the byte-exact token payload. For a tool doing tokenisation analysis and attribution back to input spans, this is the field that lets you reassemble tokens into byte offsets without guessing at a tokenizer — it is more valuable to you than `Token`, because `Token` is a lossy UTF-8 decode that mangles tokens straddling a multi-byte codepoint. **Attribute on `Utf8Bytes`, display `Token`.**

**Logprobs on the Responses API** (newer surface, `OpenAI.Responses`, gated behind `[Experimental("OPENAI001")]`):
- Request: `CreateResponseOptions.TopLogProbabilityCount`, plus adding `IncludedResponseProperty.MessageOutputTextLogProbabilities` to the `Included` collection
- Response: `ResponseContentPart.OutputTextTokenLogProbabilities` → `IReadOnlyList<ResponseTokenLogProbabilityDetails>`
- Streaming: `StreamingResponseOutputTextDeltaUpdate.TokenLogProbabilities` and `StreamingResponseOutputTextDoneUpdate.TokenLogProbabilities`
- Shapes mirror Chat: `ResponseTokenLogProbabilityDetails { Token, LogProbability, Utf8Bytes, TopLogProbabilities }`

Both surfaces work. **Prefer Chat Completions for this application**: its logprob types are non-experimental (no `OPENAI001` suppression needed), the streaming shape is one flat list per chunk rather than a discriminated event union, and MEAI's `OpenAIChatClient` puts the whole `StreamingChatCompletionUpdate` in `RawRepresentation` in one place.

**`Azure.AI.OpenAI` — the stale-package problem.** Latest **stable is 2.1.0, published 2024-12-06**. Since then, ten prereleases and no stable: `2.2.0-beta.1` (2025-02-08) … `2.9.0-beta.1` (2026-03-13). *Twenty months without a stable release, and five months since even the last beta.* Meanwhile Microsoft's own current Azure OpenAI .NET guidance ([supported-languages, doc date 2026-07-20](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/supported-languages)) does not mention `Azure.AI.OpenAI` at all. It says:

```
dotnet add package OpenAI
dotnet add package Azure.Identity
```

…and constructs a plain `OpenAI.Chat.ChatClient` against `https://YOUR-RESOURCE-NAME.openai.azure.com/openai/v1/` with a `BearerTokenPolicy` over `DefaultAzureCredential`, scope `https://ai.azure.com/.default`. Tested there with `OpenAI` 2.12.0 and `Azure.Identity` 1.21.0.

**Relationship, stated plainly: Azure has converged onto the OpenAI SDK.** `Azure.AI.OpenAI` was a companion library that subclassed the OpenAI client to add Azure-specific request/response models; the Azure `/openai/v1/` endpoint makes that mostly unnecessary. For you this is unambiguously good — it means **one client type, one options type, one logprob code path for both your hosted backends**, differing only in base URL and credential.

---

### 5. AWS SDK for .NET Bedrock — the sharp edge

**`AWSSDK.BedrockRuntime` 4.0.101.4**, published **2026-08-24**, GA, actively shipping (550 published versions), targets net8.0 / netcoreapp3.1 / netstandard2.0 / net472, requires `AWSSDK.Core >= 4.0.102.1`.

**The three call shapes:**

| API | Shape | Tool calling | Streaming | Model-agnostic |
|---|---|---|---|---|
| `InvokeModel` / `InvokeModelWithResponseStream` | raw model-native JSON in `Body` (a `MemoryStream`), raw JSON out | model-specific | yes (`WithResponseStream`) | **No** — you hand-write each family's schema |
| `Converse` | unified `messages` / `system` / `inferenceConfig` / `toolConfig` | **yes, unified** | no | **Yes** |
| `ConverseStream` | same, event stream | yes | **yes** | **Yes** |

`Converse` escape hatches: `additionalModelRequestFields` (arbitrary JSON merged into the model-native request) and `additionalModelResponseFieldPaths` (up to **10** RFC 6901 JSON Pointers naming model-native response fields to surface back in `additionalModelResponseFields`).

#### Does *any* of them expose per-token log probabilities?

**`Converse` / `ConverseStream`: No. Categorically.** I read the full [Converse API reference](https://docs.aws.amazon.com/bedrock/latest/APIReference/API_runtime_Converse.html). The response schema is `additionalModelResponseFields`, `metrics { latencyMs }`, `output`, `performanceConfig`, `serviceTier`, `stopReason`, `trace`, `usage { inputTokens, outputTokens, totalTokens, cache* }`. There is **no logprobs field anywhere**, and `inferenceConfig` accepts only `maxTokens`, `stopSequences`, `temperature`, `topP` — not even `topK`, which must go through `additionalModelRequestFields`.

**`InvokeModel`: Yes, but only in two narrow cases, neither of which is a first-party foundation model on Converse.**

| Route | Request field | Response field | Which models | Status |
|---|---|---|---|---|
| **Bedrock Custom Model Import** | `"return_logprobs": true` | `logprobs` (generated tokens) **and** `prompt_logprobs` (input tokens) | Only models you imported yourself (Llama, Mistral, Qwen, …), **imported after 2025-07-31** | GA. [AWS ML blog, 2025-09-12](https://aws.amazon.com/blogs/machine-learning/unlock-model-insights-with-log-probability-support-for-amazon-bedrock-custom-model-import/) |
| **Cohere Command (legacy text)** | `"return_likelihoods": "GENERATION" \| "ALL" \| "NONE"` | `generations[].likelihood` (mean) and `generations[].token_likelihoods: [{token, likelihood}]` | `cohere.command-text-v14` and siblings — the *legacy* completion models, **not** Command R / R+ | [Cohere Command params doc](https://docs.aws.amazon.com/bedrock/latest/userguide/model-parameters-cohere-command.html). Note: **no top-K alternatives**, only the chosen token's likelihood. And these are the oldest models in the catalogue. |

**Anthropic Claude on Bedrock: no logprobs, at any layer.** The Anthropic API does not expose token logprobs on any surface, so Bedrock has nothing to pass through. This matters because Claude is what most people actually run on Bedrock.

**Amazon Nova, Meta Llama (hosted), Mistral (hosted), AI21 Jamba, Cohere Command R/R+: no documented logprobs on Converse.** For the hosted (non-imported) versions, `additionalModelRequestFields` would need the model's own inference server to accept a logprobs flag *and* Converse's response normaliser to leave the field somewhere a JSON Pointer can reach. I found no AWS documentation asserting either. Treat as unsupported.

**Verified by disassembly.** I downloaded `awssdk.bedrockruntime.4.0.101.4.nupkg`, extracted `lib/net8.0/AWSSDK.BedrockRuntime.dll`, and scanned every ASCII string ≥5 chars (2,272 distinct). Strings containing `logprob`, `LogProb` or `likelihood`: **zero**. Control strings `ConverseRequest`, `InvokeModelRequest`, `ConverseStreamRequest`, `InferenceConfiguration`, `additionalModelResponseFieldPaths`: all present. **There is no typed logprob support anywhere in the AWS SDK for .NET Bedrock.** Any logprobs you get from Bedrock come out of a `MemoryStream` you parse yourself.

**And the MEAI adapter inherits that.** Same scan on `AWS.Bedrock.MEAI` 1.0.0 `lib/net8.0`: `logprob` — zero hits; `likelihood` — zero hits. Present: `AsIChatClient`, `ConverseAsync`, `ConverseStreamAsync`, `get_RawRepresentation` / `set_RawRepresentation`, `get_RawRepresentationFactory`, `get_AdditionalModelResponseFields`. So the adapter is a faithful Converse wrapper with working escape hatches — it just has nothing to put through them.

**One genuinely promising, unverified route: Bedrock's OpenAI-compatible endpoint.** Bedrock now exposes `https://bedrock-runtime.{region}.amazonaws.com/openai/v1/chat/completions` (SigV4 or `AWS_BEARER_TOKEN_BEDROCK`) and a `bedrock-mantle` variant. Per the [API compatibility matrix](https://docs.aws.amazon.com/bedrock/latest/userguide/models-api-compatibility.html), the models with Chat Completions support are the OpenAI-family and open-weights models: `gpt-oss-120b`, `gpt-oss-20b`, GPT OSS Safeguard 120B/20B, GPT-5.6 Sol/Terra/Luna, and the Qwen3 family. **If `logprobs`/`top_logprobs` pass through that endpoint, then a single `OpenAI.Chat.ChatClient` pointed at a Bedrock base URL would give you full OpenAI-shaped logprobs on Bedrock-hosted open-weights models — one code path for OpenAI, Azure and part of Bedrock.** The AWS doc defers parameter details to OpenAI's own reference and does not enumerate supported or unsupported fields, so **this is UNCONFIRMED**. It is the single highest-value 30-minute experiment on this list: point `ChatClient` at that URL with `IncludeLogProbabilities = true` against `openai.gpt-oss-20b` and see whether `ContentTokenLogProbabilities` comes back populated, empty, or 400s.

---

### 6. Recommendation — what layer to code against, and how to keep the telemetry

**Code against `IChatClient`. Add one capability channel beside it. Never let the two be confused.**

**A. Two-channel design.** `IChatClient` carries the conversation. A parallel `ITokenTelemetryExtractor` carries the introspection. Define, in your own assembly:

```csharp
public interface ITokenTelemetryExtractor
{
    bool   SupportsTokenTelemetry { get; }
    string CapabilityNote { get; }              // for the UI: "Bedrock Converse does not return logprobs"
    void   ConfigureRequest(ChatOptions options, int topK);
    IReadOnlyList<TokenStep>? Extract(object? rawRepresentation);
}

public sealed record TokenStep(
    string Token,
    ReadOnlyMemory<byte> Utf8Bytes,
    float  LogProbability,
    IReadOnlyList<TokenAlternative> TopAlternatives,
    int    SequenceIndex);
```

One implementation per backend. `ConfigureRequest` sets `ChatOptions.RawRepresentationFactory`; `Extract` downcasts `RawRepresentation`. Register the extractor so `IChatClient.GetService<ITokenTelemetryExtractor>()` finds it, and let the shell query capability *before* the user asks for a heat map — so the UI says "log probabilities unavailable on this backend" instead of rendering an empty grid.

**B. `TokenTelemetryChatClient : DelegatingChatClient`, innermost.** It calls `ConfigureRequest` on the way in, and on the way out harvests **per streaming update** — never after `ToChatResponseAsync()` — accumulating into a `List<TokenStep>` which it writes to `AdditionalProperties["chatdbg.tokenTelemetry"]` on the final update and on the composed response. Because `AdditionalProperties` is JSON-serialisable and `RawRepresentation` is not, this is what makes the telemetry survive your history store and your response cache. Pipeline order, restated because it is the thing most likely to be got wrong:

```csharp
IChatClient client = new ChatClientBuilder(providerClient)
    .UseLogging(loggerFactory)
    .UseOpenTelemetry(sourceName: "chatdbg")
    .UseDistributedCache(cache)          // cache hits lose RawRepresentation — telemetry must already be in AdditionalProperties
    .UseFunctionInvocation()             // multi-round-trip; collapses RawRepresentation to one slot
    .Use((inner, sp) => new TokenTelemetryChatClient(inner, extractor))   // innermost: sees every raw object
    .Build(services);
```

**C. Per-backend truth, and design honestly around it.**

| Backend | Client | Request switch | What you get |
|---|---|---|---|
| OpenAI hosted | `OpenAI.Chat.ChatClient(model, key).AsIChatClient()` | `ChatCompletionOptions { IncludeLogProbabilities = true, TopLogProbabilityCount = 20 }` | Full: chosen token + logprob + `Utf8Bytes` + up to 20 alternatives, streaming and non-streaming |
| Azure OpenAI | same type, `Endpoint = https://<res>.openai.azure.com/openai/v1/`, `BearerTokenPolicy` | identical | Same as above, subject to per-deployment parameter support |
| Amazon Bedrock (Converse) | `AmazonBedrockRuntimeClient.AsIChatClient(modelId)` | none exists | **Text and token counts only.** No logprobs. Extractor reports `SupportsTokenTelemetry = false`. |
| Local GGUF | `LLamaSharp` executor `.AsChatClient()` — or bypass | n/a | **The richest of the three** (see D) |

**D. The local backend is where this application's marquee features actually live — build it first.** LLamaSharp 0.27.0's assembly exposes `llama_get_logits` / `llama_get_logits_ith` (surfaced as `GetLogits` / `GetLogitsIth`), `LLamaTokenDataArray` / `LLamaTokenDataArrayNative` with `Softmax`, `LLamaTokenData` (id, logit, p), `LLamaVocabNative` / `llama_n_vocab`, and `Tokenize` / `DeTokenize` over `llama_tokenize` / `llama_detokenize` — all verified present in the shipped DLL. That is **the full logit vector over the entire vocabulary at every position**, not a truncated top-20.

Your feature list maps onto it directly, and onto nothing else:
- *probability maps over the vocabulary* — only possible here. OpenAI caps `TopLogProbabilityCount` at 20; Bedrock gives zero. A local GGUF gives you all ~128k logits per step.
- *tokenization analysis* — `llama_tokenize`/`llama_detokenize` against the actual model vocabulary, not a re-implementation that might disagree with the model.
- *token attribution back to input spans* — needs prompt-token scoring, which means either a local forward pass over the prompt or Bedrock Custom Model Import's `prompt_logprobs`. **Hosted OpenAI does not return prompt logprobs at all** — `ContentTokenLogProbabilities` covers *output* tokens only. So input attribution is a **local-only feature** in this design.

Therefore: `AsChatClient()` for the ordinary conversational path so the local model looks like every other backend, plus a **direct `LLamaContext` path underneath** for the introspection features that no `IChatClient` shape can express. Do not try to force full-vocabulary probability maps through `RawRepresentation` — build the deep-inspection view against `LLamaContext`/`BatchedExecutor` directly and let the abstraction handle only chat.

**Second-best overall, and when it wins.** *Skip `IChatClient` entirely and write your own three-implementation `IChatBackend` over `OpenAI` 2.13.0, `AWSSDK.BedrockRuntime` 4.0.101.4 and `LLamaSharp` 0.27.0.* This wins if, after prototyping, you find you are fighting the abstraction more than using it — specifically if you end up downcasting `RawRepresentation` on **every single call**, at which point `IChatClient` is pure overhead with a lossy-conversion hazard attached. It also wins if you need Bedrock `InvokeModel` with `return_logprobs` for Custom Model Import, since `AWS.Bedrock.MEAI` only speaks Converse and you would be reaching past it anyway. The signal to switch: if fewer than ~60% of your calls go through the abstraction without a downcast, the abstraction is not paying for itself.

**What you give up by choosing `IChatClient`.** (1) A layer of indirection between you and the wire, in a tool whose value proposition is proximity to the wire. (2) `RawRepresentation` downcasts are *unchecked* — a provider adapter changing what it stows there is a runtime `null`, not a compile error; you need integration tests per provider that assert telemetry is non-empty. (3) The streaming lossiness described above is a real footgun that only bites at runtime. (4) You inherit MEAI's release cadence: `Microsoft.Extensions.AI.OpenAI` pins `OpenAI [2.12.0, 2.13.0)`, so **you cannot adopt new OpenAI SDK features until the MEAI adapter widens its range** — today that is a two-and-a-half-week lag, but it is a lag you did not previously have.

---

## What this application specifically needs — tied to concrete operations

| Operation the app performs | What it needs | What I recommend, and why |
|---|---|---|
| Send a turn to any of three backends and stream tokens to the terminal | One streaming contract across OpenAI, Bedrock, local GGUF | `IChatClient.GetStreamingResponseAsync` — the only contract all three implement today (verified for all three). |
| Show per-token logprobs with top-K alternatives as a heat map | Chosen-token logprob + K alternatives, per position, arriving *during* the stream | `RawRepresentation` harvested per `ChatResponseUpdate`. **Not** after `ToChatResponseAsync()` — that keeps one raw object and discards the rest. |
| Ask for top-K alternatives at all | A `top_logprobs`-equivalent request switch | `ChatOptions.RawRepresentationFactory` → `ChatCompletionOptions.TopLogProbabilityCount` (0–20). **Not `ChatOptions.TopK`**, which is top-k *sampling* and changes generation instead of reporting. |
| Tokenization analysis — byte-exact token boundaries | The raw token bytes, not a lossy string | `ChatTokenLogProbabilityDetails.Utf8Bytes` (hosted) or `llama_tokenize`/`llama_detokenize` (local). Attribute on bytes, display the string. |
| Probability map over the whole vocabulary | Full logit vector per step | **Local GGUF only.** `LLamaContext.GetLogitsIth()` + `LLamaTokenDataArray.Softmax()` + `llama_n_vocab`. No hosted API returns this; OpenAI caps at 20 alternatives, Bedrock at zero. |
| Token attribution back to input spans | *Prompt*-token logprobs | **Local only** in practice. OpenAI's `ContentTokenLogProbabilities` is output-only. Bedrock has `prompt_logprobs` but exclusively for Custom Model Import via `InvokeModel`. |
| Persist a session with its telemetry and reopen it later | Telemetry that survives JSON | Project into `AdditionalProperties` (serialises) — **never** rely on `RawRepresentation` (`[JsonIgnore]`, verified in all three abstraction types). |
| Cache repeated calls during development | Cache that does not silently destroy telemetry | `UseDistributedCache` above your telemetry decorator, so telemetry is already in `AdditionalProperties` when the response is serialised. Otherwise every cache hit returns a logprob-free response and the heat map goes blank for no visible reason. |
| Execute tools defined by `Xcaciv.Command`, loaded by `Xcaciv.Loader` | Tool-call plumbing that does not duplicate your framework | `AIFunctionFactory.Create` thin-wrapping each `Xcaciv.Command` command, plus `.UseFunctionInvocation()`. **Not** SK plugins or AF agent tools — those are competing registries. |
| Named system prompts, settings, secrets | Nothing from the LLM layer | `ChatOptions.Instructions` for per-request system text. Keep prompt/settings/secret storage in your own layer; do not model it as agent state. |
| Self-contained single-file binary, Windows + Linux | AOT/trim-friendly dependencies | `MEAI.Abstractions` has **zero dependencies on net10.0**. Note the exceptions: `LLamaSharp` needs native llama.cpp backend packages per RID, and `Microsoft.ML.OnnxRuntimeGenAI` needs native runtimes — neither is single-file-trivial. `AWSSDK.*` and `OpenAI` are managed-only. |
| Target framework | Modern .NET | **.NET 10.** Verified against [releases-index.json](https://raw.githubusercontent.com/dotnet/core/main/release-notes/releases-index.json): 10.0.11 (2026-08-11), `active`, **LTS, EOL 2028-11-14**. .NET 9 and 8 both go to `maintenance` and are EOL 2026-11-10 — under three months away. .NET 11 is at `preview.7`. Every package above supports net8.0+; nothing blocks .NET 10. |

---

## Risks, sharp edges and what you give up

1. **The abstraction drops the app's core datum. This is architectural, not incidental.** No typed logprobs anywhere in MEAI 10.9.0, no open tracking issue in `dotnet/extensions`. Every logprob you display travels through an unchecked `object?` downcast. *Mitigation:* one integration test per backend asserting `Extract()` returns a non-empty list against a live call, run in CI on a schedule, not just on commit.

2. **Streaming `RawRepresentation` is lossy on composition — and it is documented as such.** Buffering updates then calling `ToChatResponseAsync()` silently discards all but one raw object. Harvest per update. This is a five-line mistake that produces a plausible-looking, mostly-empty heat map.

3. **`ChatOptions.TopK` is a decoy.** It is top-k sampling. In a token-introspection tool, mistaking it for `top_logprobs` changes model behaviour *and* returns nothing, which is the worst combination of failure modes.

4. **`RawRepresentation` is `[JsonIgnore]` in `ChatResponse`, `ChatResponseUpdate` and `ChatMessage`.** Caching and history persistence both destroy it. `AdditionalProperties` is the only carrier that survives.

5. **Bedrock is a structurally degraded backend for this application.** Converse has no logprobs; the .NET SDK contains zero logprob strings; Claude — Bedrock's most-used family — has none upstream. Only Custom Model Import (`InvokeModel` + `return_logprobs`) and legacy Cohere Command (`return_likelihoods`, chosen-token only, no alternatives) offer anything, and neither goes through `AWS.Bedrock.MEAI`. *Design consequence:* the backend-selection UI must surface a capability badge, and the token-inspection commands must degrade explicitly rather than render empty grids.

6. **`AWS.Bedrock.MEAI` 1.0.0 is 17 days old with ~5,200 downloads.** It replaces a package deprecated at 4.0.101.8 whose *entire* version history is now flagged. The rename is AWS-official (repo `aws/aws-dotnet-ai`, deprecation record names the successor), so this is a maturity risk rather than an abandonment risk — but pin the exact version and expect churn. *Fallback:* `AWSSDK.BedrockRuntime` 4.0.101.4 directly with a hand-written `IChatClient`; you lose the adapter, not the capability, since the adapter carries no telemetry you would miss.

7. **`Azure.AI.OpenAI` stable is 20 months old.** Do not depend on it. Use `OpenAI` 2.12.x against the Azure `/openai/v1/` endpoint per current Microsoft guidance. *Corollary risk:* that guidance is itself recent, so the Azure v1 endpoint's per-deployment parameter support may differ from OpenAI's — verify logprobs on your specific deployment before promising the feature. There is a documented history of Azure OpenAI rejecting or silently ignoring `logprobs` on some API versions and model families ([Azure/azure-sdk-for-net#42340](https://github.com/Azure/azure-sdk-for-net/issues/42340), plus multiple Microsoft Q&A reports).

8. **`Microsoft.Extensions.AI.OpenAI` pins `OpenAI [2.12.0, 2.13.0)`.** Referencing both resolves 2.12.0. You cannot adopt 2.13.0 features until the adapter widens. Currently a ~2.5-week lag; historically small; but it is a coupling you are accepting.

9. **`LLamaSharp` is pre-1.0 at 0.27.0** and pins a specific llama.cpp commit. Its release cadence is irregular — 0.25.0 (2025-08-16) then nothing until 0.26.0 (2026-02-15), a six-month gap. It depends on `MEAI.Abstractions` 10.4.1 while you will be on 10.9.0; that resolves fine but means the maintainers are not tracking MEAI closely. *This is the highest-severity single-package risk in the plan*, because the local backend is where your distinguishing features live. *Fallback:* `llama.cpp`'s own `llama-server` (OpenAI-compatible, and it does return logprobs) driven through `OpenAI.Chat.ChatClient` — but that costs you `GetLogits()` full-vocabulary access, which is the whole point. There is no clean substitute; budget for possibly maintaining a LLamaSharp fork.

10. **`Microsoft.ML.OnnxRuntimeGenAI` is the wrong tool despite looking right.** It has an `IChatClient` and it is first-party GA — but it consumes ONNX, not GGUF. Its `IChatClient` also exposes no logits (verified: no `Logits`/`Softmax`/`RawRepresentation` strings in `Microsoft.ML.OnnxRuntimeGenAI.Managed` 0.15.2). Mentioned only to rule it out.

11. **Two experimental MEAI features you will be tempted by.** Chat reduction (`SummarizingChatReducer`) is attractive for long debugging sessions but rewrites history — poison for a tool whose output must be reproducible. Chat routing/failover is attractive for three backends but silently substituting models would invalidate every measurement. Both are marked experimental. Avoid both.

**What you give up by taking this recommendation, stated plainly:** a layer of indirection in a tool whose value is proximity to the wire; compile-time safety on the telemetry path; the freedom to adopt OpenAI SDK releases the week they ship; and any pretence that the three backends are equivalent — Bedrock will always be the poor cousin, and the honest thing is to show that in the UI rather than paper over it.

---

## Unconfirmed

Everything below is stated as unknown rather than guessed. Each entry names where I looked.

1. **Whether Bedrock's OpenAI-compatible Chat Completions endpoint accepts `logprobs`/`top_logprobs` and returns them.** *Looked at:* [inference-chat-completions-mantle.html](https://docs.aws.amazon.com/bedrock/latest/userguide/inference-chat-completions-mantle.html) (defers to OpenAI's reference for "complete API details" and enumerates no supported/unsupported fields), [models-api-compatibility.html](https://docs.aws.amazon.com/bedrock/latest/userguide/models-api-compatibility.html) (lists which models support Chat Completions but no parameter detail), plus targeted searches for a 2026 Bedrock logprobs announcement (none found). **This is the highest-value open question in this document** — if it works, `gpt-oss-*` and Qwen3 on Bedrock become full-telemetry backends through the same `OpenAI.Chat.ChatClient` code path. Test empirically: `IncludeLogProbabilities = true` against `openai.gpt-oss-20b` on `https://bedrock-runtime.{region}.amazonaws.com/openai/v1`.

2. **Whether any hosted (non-imported) Bedrock foundation model returns logprobs via `Converse`'s `additionalModelRequestFields` + `additionalModelResponseFieldPaths` combination.** *Looked at:* the full Converse API reference request/response schema, the Cohere Command parameter page, the Custom Model Import logprobs blog. No documentation asserts this for any model. I did not test it live. My assessment is that it does not work, but I did not prove a negative.

3. **Whether Azure OpenAI's `/openai/v1/` endpoint honours `IncludeLogProbabilities` for current GPT-5-family deployments.** *Looked at:* the [supported-languages doc](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/supported-languages) (no logprobs example), [Azure/azure-sdk-for-net#42340](https://github.com/Azure/azure-sdk-for-net/issues/42340), and several Microsoft Q&A threads reporting `logprobs` being rejected or silently dropped on specific API versions/models. Historical support was inconsistent. Current v1-endpoint behaviour per model family is unverified — test against your actual deployment before committing the feature.

4. **Exact publish date of `AWSSDK.Extensions.Bedrock.MEAI`'s deprecation.** NuGet's registration API reports every version as deprecated with the message naming `AWS.Bedrock.MEAI`, but does not date the deprecation event. `AWS.Bedrock.MEAI` 1.0.0 published 2026-08-11, which brackets it.

5. **`Microsoft.Extensions.AI.Evaluation` 10.9.0 relevance.** GA and current, but I did not investigate whether it exposes token-level scoring primitives useful here. Out of scope for this brief.

6. **Whether `Microsoft.ML.OnnxRuntimeGenAI`'s `Generator.GetOutput(...)` can retrieve a `"logits"` tensor in 0.15.2.** I confirmed the native entry point `OgaGenerator_GetOutput` exists in the managed assembly but did not confirm the valid output names or that logits are among them. Moot for this application (ONNX ≠ GGUF), noted for completeness.

7. **Ollama's exact current logprobs surface.** Search results indicate Ollama v0.12.11 (Nov 2025) added `logprobs`/`top_logprobs` to both the native and OpenAI-compatible endpoints, and I verified `OllamaSharp` 5.4.30 ships typed `Logprobs`, `TopLogprobs`, `LogProbability`, `Logprob` and `LogitsAll` members. But [ollama/ollama#16117](https://github.com/ollama/ollama/issues/16117), which requested logprobs on the OpenAI-compatible endpoint, is **closed as not planned**, which contradicts the secondary sources. I could not reconcile these against primary Ollama documentation. Not on your backend list, so I did not pursue it further — but if you add Ollama, verify the endpoint behaviour yourself.

8. **`Xcaciv.Cupcake`, `Xcaciv.Command`, `Xcaciv.Loader`.** None of these resolve on nuget.org (checked `xcaciv.command`, `xcaciv.loader`, `xcaciv.cupcake`, `xcaciv.command.core`, `xcaciv.command.interface` against the flat-container API — all 404). They are presumably GitHub-sourced or privately fed. I therefore could not verify their target frameworks or confirm .NET 10 compatibility, and my `AIFunctionFactory`-wrapping recommendation for tool hosting assumes a conventional command/parameter model without having inspected the actual API. Verify before designing against it.

9. **Runtime behaviour of `ChatResponse.AdditionalProperties` round-tripping through `DistributedCachingChatClient`.** I confirmed by source that `AdditionalProperties` lacks `[JsonIgnore]` and that the caching client uses `JsonSerializer` over `typeof(ChatResponse)`. I did not run it. Custom DTOs in `AdditionalProperties` will need a `JsonSerializerContext` registered on the caching client's `JsonSerializerOptions`, and I have not verified that path end-to-end — particularly under Native AOT, where the default reflection-based resolver is unavailable.
