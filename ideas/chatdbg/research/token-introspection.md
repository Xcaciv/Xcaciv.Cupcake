# Token probability, tokenization, and attribution capabilities across providers

*Research date: 2026-08-28. Every version number, package name and capability claim below was checked against nuget.org, vendor documentation, or vendor source on the live web on that date. Anything I could not verify is marked **UNCONFIRMED** and collected in the final section.*

---

## Bottom line — the recommendation in three sentences

Build the introspection data model around **LLamaSharp 0.27.0** as the reference backend, because it is the only one of the five that hands you the *entire* logit vector for *every* position (`SafeLLamaContextHandle.GetLogitsIth`, `LLamaBatch.Add(..., logits: true)`), which is what makes full-vocabulary probability maps, exact per-position entropy, and teacher-forced attribution possible at all; every other backend is a lossy projection of that model. For the hosted providers use the **OpenAI 2.13.0** .NET package (against both api.openai.com and the Azure `/openai/v1` endpoint) plus **AWSSDK.BedrockRuntime 4.0.101.4** with hand-written `InvokeModel` JSON bodies, and accept that Azure/OpenAI give you top-K ≤ 5–20 on *output tokens only* and **nothing at all on reasoning models**, while stock Bedrock foundation models give you essentially nothing except Bedrock Custom Model Import, which is the one hosted path that returns `prompt_logprobs`. Do **not** route this feature through `Microsoft.Extensions.AI` — its abstractions (v10.9.0) contain no logprob concept whatsoever, so you would be tunnelling your differentiating feature through `RawRepresentation` casts; define your own `ITokenIntrospectingChatBackend` and keep MEAI, if you want it, for the boring chat path.

---

## Landscape — the real options

### Inference / SDK packages

| Package | Latest | Status | Last published | Verdict |
|---|---|---|---|---|
| [`LLamaSharp`](https://www.nuget.org/packages/LLamaSharp) (+ `LLamaSharp.Backend.Cpu`/`Cuda*`/`Vulkan`) | **0.27.0** | 0.x, actively maintained (SciSharp) | 2026-04-26 | **Pick.** Only backend with full-vocab logits at arbitrary positions. 0.x versioning = breaking changes between minors; pin exactly. |
| [`Microsoft.ML.OnnxRuntimeGenAI`](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntimeGenAI) | **0.15.2** | **Preview** (docs literally titled "Generate API (Preview)"; "this API is in preview and is subject to change") | 2026-08-06 | Second-best local. `Generator.GetOutput("logits")` works, but the published C# API page is *stale* and the 0.x preview label is real. |
| [`OpenAI`](https://www.nuget.org/packages/OpenAI) (official .NET SDK) | **2.13.0** | **GA** | 2026-08-10 | **Pick** for OpenAI *and* Azure OpenAI v1. First-class `IncludeLogProbabilities` / `TopLogProbabilityCount`, non-streaming and streaming. |
| [`Azure.AI.OpenAI`](https://www.nuget.org/packages/Azure.AI.OpenAI) | 2.9.0-beta.1 | **Latest stable is 2.1.0 from 2024-12-06** — the active line is beta-only for ~20 months | 2026-03-13 (beta) | **Avoid as the primary path.** A GA-stable dependency that has not moved since Dec 2024 while the beta line ships is a real risk. Use `OpenAI` 2.13.0 against `https://{resource}.openai.azure.com/openai/v1`. |
| [`AWSSDK.BedrockRuntime`](https://www.nuget.org/packages/AWSSDK.BedrockRuntime) | **4.0.101.4** | **GA**, shipping continuously (four releases in Aug 2026 alone) | 2026-08-24 | **Pick** for Bedrock. But note: `Converse` has no logprob surface; you need `InvokeModel` with a raw JSON body. |
| [`OllamaSharp`](https://www.nuget.org/packages/OllamaSharp) | **5.4.30** | GA-ish (5.x), actively maintained | 2026-07-24 | Usable, with a specific gap: it models logprobs on `GenerateRequest` **but not on `ChatRequest`** (verified against `main`). |
| [`Microsoft.Extensions.AI(.Abstractions)`](https://www.nuget.org/packages/Microsoft.Extensions.AI.Abstractions) | **10.9.0** | GA | 2026-08-11 | Fine for plain chat. **Zero** logprob concept in the abstraction — verified by grepping the whole `Microsoft.Extensions.AI.Abstractions` source tree for "logprob": no hits. |

### Tokenization packages

| Package | Latest stable | Latest any | Last published | Verdict |
|---|---|---|---|---|
| [`Microsoft.ML.Tokenizers`](https://www.nuget.org/packages/Microsoft.ML.Tokenizers) | **2.0.0** | 3.0.0-preview.26160.2 | 2.0.0: 2025-11-11 / preview: 2026-03-12 | **Pick.** Broadest family support, offset-carrying tokens, Microsoft-maintained, and Microsoft's own migration guide points other libraries here. |
| `Microsoft.ML.Tokenizers.Data.{Cl100kBase,O200kBase,P50kBase,R50kBase,Gpt2}` | **2.0.0** | 3.0.0-preview.26160.2 | 2025-11-11 / 2026-03-12 | Five data packages exist. **There is no `…Data.O200kHarmony` package** — harmony reuses the O200kBase ranks file. |
| [`SharpToken`](https://www.nuget.org/packages/SharpToken) | **2.0.6** | — | 2026-03-25 | *Not* abandoned (contrary to the common assumption) but tiktoken-only. Second-best if you only ever tokenize OpenAI models. |
| [`TiktokenSharp`](https://www.nuget.org/packages/TiktokenSharp) | **1.2.1** | — | 2026-02-22 | Maintained, tiktoken-only, smaller community. Third choice. |
| [`Tokenizers.DotNet`](https://www.nuget.org/packages/Tokenizers.DotNet) | **1.4.1** | — | 2026-04-16 | Rust HuggingFace-`tokenizers` binding. The escape hatch when you need byte-exact parity with an arbitrary `tokenizer.json`. Native-asset shipping complicates your self-contained single-file binary. |
| [`FastBertTokenizer`](https://www.nuget.org/packages/FastBertTokenizer) | 1.0.28 (stable) | 1.1.30-alpha | stable: 2024-04-30 | **Effectively stale** for stable users; WordPiece/BERT only. Irrelevant here. |

### Attribution / differentiation packages

| Package | Latest | Status | Last published | Verdict |
|---|---|---|---|---|
| [`TorchSharp`](https://www.nuget.org/packages/TorchSharp) + [`libtorch-cpu`](https://www.nuget.org/packages/libtorch-cpu) | 0.107.0 / 2.10.0 | 0.x, actively maintained | 2026-05-07 / 2026-02-13 | Real autograd in .NET. But you would have to **re-implement the transformer in C#** to use it — not viable for this app. |
| [`Microsoft.ML.OnnxRuntime.Training`](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime.Training) | 1.19.2 | **Effectively abandoned for stable users** | **2024-09-03 (~2 years old)** | Do not build on this. |
| [`Microsoft.ML.OnnxRuntime`](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime) | 1.29.0 | GA | 2026-08-12 | Inference only. No gradients. |

### Terminal rendering (touched only because point 4 asks how this is visualised)

| Package | Latest stable | Last published | Verdict |
|---|---|---|---|
| [`Spectre.Console`](https://www.nuget.org/packages/Spectre.Console) | **0.57.2** (0.57.3-alpha.0.8 exists) | 2026-07-02 | The obvious choice for grids/tables/bar charts/canvas. Still pre-1.0 after many years — accept the 0.x API churn. |

### Runtime

.NET 10 is the current GA LTS; [`Microsoft.NETCore.App.Ref`](https://www.nuget.org/packages/Microsoft.NETCore.App.Ref) shows **11.0.0-preview.7.26381.103 (2026-08-11)** as the newest .NET 11 preview. **Target .NET 10.** I found no reason any package above blocks it; `Microsoft.ML.Tokenizers` even still multi-targets netstandard2.0.

---

## Analysis

### 1. Per-backend logprob capability

#### 1a. Azure OpenAI / OpenAI (Chat Completions)

**Request surface.** `logprobs: bool` and `top_logprobs: integer`. The OpenAI TypeSpec vendored into `openai-dotnet` `main` ([`specification/base/typespec/chat/models.tsp:275`](https://github.com/openai/openai-dotnet/blob/main/specification/base/typespec/chat/models.tsp)) documents:

> `/** An integer between 0 and 20 specifying the number of most likely tokens to return at each token position, each with an associated log probability. */`
> `top_logprobs?: int32 | null;`

Note there is **no `@maxValue` constraint** in the spec — the bound is enforced server-side, and the two services enforce different bounds.

**Response surface.** From the [Azure v1 REST reference](https://learn.microsoft.com/en-us/rest/api/microsoft-foundry/azureopenai/chat?view=rest-microsoft-foundry-v1) (`OpenAI.ChatCompletionTokenLogprob`):

> `logprob | number | The log probability of this token, if it is within the top 20 most likely tokens. Otherwise, the value -9999.0 is used to signify that the token is very unlikely.`
> `top_logprobs | array of OpenAI.ChatCompletionTokenLogprobTopLogprobs | List of the most likely tokens and their log probability, at this token position. The number of entries may be fewer than the requested top_logprobs.`

That `-9999.0` sentinel is a real thing you must handle: it is not a log probability, it is a "not in the window" marker, and feeding it into `exp()` silently yields 0 and into a perplexity sum yields nonsense.

**Streaming.** Yes. The Azure v1 GA spec defines `OpenAI.CreateChatCompletionStreamResponseChoiceLogprobs` with `content` and `refusal` arrays of `ChatCompletionTokenLogprob`, alongside the non-streaming `CreateChatCompletionResponseChoicesLogprobs`. In .NET this is `StreamingChatCompletionUpdate.ContentTokenLogProbabilities` ([`OpenAI/src/Custom/Chat/Streaming/StreamingChatCompletionUpdate.cs:72`](https://github.com/openai/openai-dotnet/blob/main/OpenAI/src/Custom/Chat/Streaming/StreamingChatCompletionUpdate.cs)).

**Max K.**
- OpenAI direct: documented 0–20 (TypeSpec, above). The public [logprobs cookbook](https://developers.openai.com/cookbook/examples/using_logprobs), however, currently describes it as *"An integer between 0 and 5"* — the docs are internally inconsistent. Treat 20 as the *type* bound and probe the actual bound at runtime.
- **Azure OpenAI: 5.** The service returns `Invalid value for 'top_logprobs': must be less than or equal to 5.` ([deepeval#843](https://github.com/confident-ai/deepeval/issues/843); [azure-sdk-for-go#22538](https://github.com/Azure/azure-sdk-for-go/issues/22538)) This is runtime validation only — the Azure v1 GA OpenAPI (`azure-v1-v1-generated.yaml`, line 834) declares `top_logprobs: anyOf: [integer, 'null']` with no bound. So you cannot discover the limit from the schema ([`azure-v1-v1-generated.yaml`](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/ai/data-plane/OpenAI.v1/azure-v1-v1-generated.yaml)); you must discover it from a 400.

**The finding that matters most for this app.** Microsoft Learn's [Azure reasoning-models page](https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/reasoning) (`ms.date: 2026-08-20`) states verbatim:

> ### Not Supported
> The following are currently unsupported with reasoning models:
> - `temperature`, `top_p`, `presence_penalty`, `frequency_penalty`, **`logprobs`, `top_logprobs`**, `logit_bias`, `max_tokens`

That list covers the entire GPT-5 series and the o-series. On the OpenAI side the community record is worse and more granular: `gpt-5-chat-latest` returns *"You Are Not Allowed To Request Logprobs From This Model"* ([Aug 2025](https://community.openai.com/t/you-are-not-allowed-to-request-logprobs-from-this-model-gpt-5-chat-latest/1341462); see also ["Logprobs deprecated for gpt-5 models?"](https://community.openai.com/t/logprobs-deprecated-for-gpt-5-models/1355427)), and a [March 2026 thread](https://community.openai.com/t/gpt-5-2-logprobs-support-removed/1378114) reports that on **gpt-5.2, gpt-5.3-codex, gpt-5.4 and gpt-5.4-mini, `top_logprobs` ≥ 2 returns HTTP 500** while `top_logprobs=1` works; OpenAI closed the thread on 2026-08-25 for inactivity without a fix. **So the frontier hosted models are, in practice, the *worst* backends for this application's headline feature.** The models that reliably give you top-K logprobs today are the non-reasoning generation: gpt-4o, gpt-4o-mini, gpt-4.1 (and gpt-4-turbo). Design for that, and design for it to be withdrawn.

**Responses API.** Supported and worth having as a second path: request `top_logprobs` plus `include: ["message.output_text.logprobs"]`. Confirmed present in the **Azure v1 GA** spec (line 19518: `- message.output_text.logprobs`, described as *"Include logprobs with assistant messages"*). In .NET (`OpenAI.Responses`): `CreateResponseOptions.TopLogProbabilityCount` (doc comment: *"An integer between 0 and 20"*), `IncludedResponseProperty.MessageOutputTextLogProbabilities`, and read back via `ResponseContentPart.OutputTextTokenLogProbabilities`.

**Prompt/input-token logprobs: not available.** Chat Completions has no `echo`, no `prompt_logprobs`. This is the single hardest constraint on hosted attribution.

#### 1b. Amazon Bedrock — by model family

`Converse` / `ConverseStream` are dead ends. I read the full [`Converse` request and response syntax](https://docs.aws.amazon.com/bedrock/latest/APIReference/API_runtime_Converse.html): the request carries `inferenceConfig {maxTokens, stopSequences, temperature, topP}` plus `additionalModelRequestFields`, and the response carries `output`, `stopReason`, `usage`, `metrics`, `trace`, `additionalModelResponseFields`. **There is no logprob field anywhere in either direction.** `additionalModelResponseFieldPaths` (max 10 JSON Pointers) could in principle surface a model-native field, but only for models that emit one — and for the base models, none do.

| Bedrock model family | Output logprobs? | Top-K? | Prompt logprobs? | Streaming? | Evidence |
|---|---|---|---|---|---|
| **Anthropic Claude** | **No** | No | No | n/a | [Anthropic's own OpenAI-compat table](https://platform.claude.com/docs/en/api/openai-sdk): request `logprobs` → *"Ignored"*, `top_logprobs` → *"Ignored"*; response `logprobs` → *"Always empty"*. The native Messages API has no such parameter. |
| **Amazon Nova** | **No** (documented parameter set does not include it) | No | No | n/a | The Bedrock Nova page defers to the Nova user guide's "Complete request schema"; no logprob parameter is documented anywhere in the Bedrock parameter docs. *(Absence of documentation, not a documented absence — see Unconfirmed.)* |
| **Meta Llama (3 / 3.1 / 3.2 / 3.3 / 4)** | **No** | No | No | n/a | [Documented request](https://docs.aws.amazon.com/bedrock/latest/userguide/model-parameters-meta.html) is exactly `{prompt, temperature, top_p, max_gen_len}` (+`images`); documented response is exactly `{generation, prompt_token_count, generation_token_count, stop_reason}`. |
| **Mistral (7B/8x7B/Large/Small, text completion)** | **No** | No | No | n/a | [Documented request](https://docs.aws.amazon.com/bedrock/latest/userguide/model-parameters-mistral-text-completion.html) `{prompt, max_tokens, stop, temperature, top_p, top_k}`; response `{outputs:[{text, stop_reason}]}`. |
| **Cohere Command (`cohere.command-text-v14`)** | **Yes, partially** | **No** | **Yes (`ALL`)** | Yes | [`"return_likelihoods"`](https://docs.aws.amazon.com/bedrock/latest/userguide/model-parameters-cohere-command.html): "GENERATION\|ALL\|NONE"` → response `"token_likelihoods": [{"token": string, "likelihood": float}]` plus a sequence-level `"likelihood"` described as *"the average of the token likelihoods"*. `ALL` returns likelihoods for **all** tokens — i.e. prompt tokens too. No alternatives at any position. This is a **legacy generation-API model**; the newer Command R/R+ chat path does not carry it. |
| **AI21** | UNCONFIRMED | — | — | — | Jurassic-2 historically returned `generatedToken.logprob` + `topTokens`; J2 is retired and I did not verify the current Jamba parameter set. |
| **OpenAI gpt-oss-20b / 120b (`openai.gpt-oss-*`)** | UNCONFIRMED | — | — | — | AWS documents the request body only by reference: *"For information about the parameters in the request body and their descriptions, see Create chat completion in the OpenAI documentation."* AWS never states whether `logprobs` is honoured. Probe it. |
| **Custom Model Import (models imported after 2025-11-11)** | **Yes** | **Yes** | **Yes** | **Yes** | See below — this is the good one. |

**Bedrock Custom Model Import is the only hosted path that gives you prompt logprobs.** From [`custom-model-import-advanced-features.html`](https://docs.aws.amazon.com/bedrock/latest/userguide/custom-model-import-advanced-features.html), verbatim:

> Log probability support varies by API format:
> - BedrockCompletion - Output tokens only
> - OpenAICompletion - Prompt and output tokens
> - OpenAIChatCompletion - Prompt and output tokens

- `BedrockCompletion` shape: `"return_logprobs": True` → *"This will return top 1 logprob for each output token."* Response is a bare array of `{tokenId: logprob}` maps:
  `"logprobs": [{"362": -2.1413702964782715}, {"48713": -0.8180374503135681}, ...]` — note these are **token IDs, not strings**, so you need the model's tokenizer to render them.
- `OpenAIChatCompletion` shape: `"logprobs": True, "top_logprobs": N, "prompt_logprobs": N` → OpenAI-shaped `{token, logprob, bytes, top_logprobs:[…]}` objects. `prompt_logprobs` is the field that unlocks real input-side introspection on a hosted backend.
- Streaming: the [AWS launch post](https://aws.amazon.com/blogs/machine-learning/unlock-model-insights-with-log-probability-support-for-amazon-bedrock-custom-model-import/) confirms the feature works with both `InvokeModel` and `InvokeModelWithResponseStream` (published 2025-09-12).
- **Cost caveat, verbatim:** *"Logprobs are not cached. For a request requiring prompt logprobs, the system will ignore the prefix cache and recompute the prefill of full prompt to generate the logprobs. This presents an obvious performance tradeoff when using logprobs."*
- [Supported architectures](https://docs.aws.amazon.com/bedrock/latest/userguide/model-customization-import-model.html) for import: Mistral, Mixtral, Flan, Llama 2/3/3.1/3.2/3.3/Mllama, GPTBigCode, Qwen2/2.5/2-VL/2.5-VL/3, GPT-OSS (us-east-1 only). Weights ≤ 200 GB text / 100 GB multimodal, context < 128K, transformers 4.51.3.
- Max value of `N` for `top_logprobs`/`prompt_logprobs`: **UNCONFIRMED** — AWS's examples use `1` and document no bound.

**[Bedrock's OpenAI-compatible endpoints](https://docs.aws.amazon.com/bedrock/latest/userguide/inference-chat-completions-mantle.html).** Two exist: `https://bedrock-runtime.{region}.amazonaws.com/openai/v1/chat/completions` (recommended) and `https://bedrock-mantle.{region}.api.aws/v1/chat/completions`. Both accept the OpenAI SDK with a Bedrock API key. Neither page enumerates `logprobs` support. Practically this means: **you can point the same `OpenAI` .NET `ChatClient` at Bedrock**, which is a genuinely useful architectural simplification for the gpt-oss models — but treat logprob support there as unproven until probed.

**.NET implication.** `AWSSDK.BedrockRuntime`'s `InvokeModelRequest.Body` is an opaque `MemoryStream`, so nothing in the SDK blocks you from sending `return_logprobs` / `prompt_logprobs`. You will be hand-writing and hand-parsing JSON for Bedrock regardless. That is fine; it is also unavoidable.

#### 1c. LLamaSharp (local GGUF) — the full-fidelity backend

Verified against [`SciSharp/LLamaSharp` `master`](https://github.com/SciSharp/LLamaSharp):

- `SafeLLamaContextHandle.GetLogits(int numTokens = 1)` → `Span<float>` sized `model.Vocab.Count * numTokens`. Doc comment: *"Token logits obtained from the last call to llama_decode. The logits for the last token are stored in the last row. Only tokens with `logits = true` requested are present."* (`SafeLLamaContextHandle.cs:520–545`)
- `SafeLLamaContextHandle.GetLogitsIth(int i)` → `Span<float>` of exactly `model.Vocab.Count` floats. Throws `GetLogitsInvalidIndexException` if that position did not request logits. (`:551–561`)
- `LLamaBatch.Add(LLamaToken token, LLamaPos pos, LLamaSeqId sequence, bool logits)` — **per-token** logits flag, returning *"The index that the token was added at. Use this for GetLogitsIth"*. `AddRange(tokens, start, sequence, bool logitsLast)` is the convenience form. **This is what makes teacher-forced prompt scoring possible.**
- `LLamaTokenDataArray.Create(ReadOnlySpan<float> logits)` + `.Softmax()` → an array of `LLamaTokenData {LLamaToken ID; float Logit; float Probability}` sorted descending. That is your full-vocabulary probability map, computed in one call.
- **Interception point for streaming introspection:** `ISamplingPipeline.Sample(SafeLLamaContextHandle ctx, int index)`, settable via `InferenceParams.SamplingPipeline`. Wrap the real pipeline in a decorator that calls `ctx.GetLogitsIth(index)`, records top-K + entropy + full distribution, then delegates. Clean, no forking, works with the high-level executors.
- Tokenizer comes from the GGUF itself: `SafeLlamaModelHandle.Tokenize(string text, bool addBos, bool special, Encoding encoding)`, `TokenToSpan(LLamaToken token, Span<byte> dest, int lstrip = 0, bool special = false)`, and a `Vocabulary` class exposing `Count`, `BOS`, `EOS`, `EOT`, `SEP`, `Pad`, `Mask`, `Newline`, plus the infill tokens.

| Capability | LLamaSharp |
|---|---|
| Output logprobs | Yes (derive from logits) |
| Top-K alternatives | Yes, **K unbounded** (K = \|V\|) |
| Prompt logprobs | Yes, one decode with per-token logit flags |
| Full vocabulary distribution | **Yes** |
| Streaming | Yes (you own the loop) |
| Attention weights | **No** — see §3 |

**Sharp edge:** full-vocab logits cost `4 × n_positions × |V|` bytes. A 128k-vocab model over a 2,000-token prompt = **~1 GB** if you flag every position at once. Chunk the prefill (e.g. 128 positions per decode, reduce to per-position surprisal + top-K immediately, discard the rest).

#### 1d. ONNX Runtime GenAI

- C API ([`src/ort_genai_c.h`, `main`](https://github.com/microsoft/onnxruntime-genai/blob/main/src/ort_genai_c.h)): `OgaGenerator_GetOutput(const OgaGenerator*, const char* name, OgaTensor** out)` — *"Returns a copy of the model output identified by the given name as an OgaTensor on CPU"* — and a dedicated `OgaGenerator_GetLogits` (*"Returns a copy of the logits from the model as an OgaTensor on CPU"*), plus a setter.
- C# wrapper ([`src/csharp/Generator.cs`, `main`](https://github.com/microsoft/onnxruntime-genai/blob/main/src/csharp/Generator.cs)) exposes `GetOutput(string outputName)` and `GetInput(string)` but **not** `GetLogits`/`SetLogits`. So in .NET the call is `generator.GetOutput("logits")`, then `Tensor.GetData<float>()` → `ReadOnlySpan<float>` and `Tensor.Shape()` → `long[]`.
- **The published C# API docs are stale.** <https://onnxruntime.ai/docs/genai/api/csharp.html> still documents `ComputeLogits()` and a `Tensor` with an `Array Data` property; neither exists in current source. Read the source, not the docs. The page also says: *"Note: this API is in preview and is subject to change."*
- **Prompt-position logits: conditionally yes.** The repo's own Python test asserts logits of shape `[2, 4, 1000]` (batch × prompt-seq × vocab) from `get_output("logits")` after appending a 4-token prompt, then `[2, 1, 1000]` per generated token. So the *runtime* will surface all prompt positions. Whether *your* exported model does depends on whether it was exported with `num_logits_to_keep` trimming (the test suite parameterises over exactly that name). **UNCONFIRMED** for the stock published ONNX model repos.
- `genai_config.json` lets you rename the `logits` output but documents no switch to control prompt-logit production.

| Capability | ORT GenAI 0.15.2 |
|---|---|
| Output logprobs | Yes (from logits) |
| Top-K | Unbounded |
| Prompt logprobs | Yes *if the exported graph emits them* — verify per model |
| Full vocabulary | Yes |
| Streaming | Yes |
| Status | **Preview, 0.x** |

#### 1e. Ollama

Verified against [`ollama/ollama` `api/types.go` on `main`](https://github.com/ollama/ollama/blob/main/api/types.go). **Both** (see also the [`/api/generate` docs](https://docs.ollama.com/api/generate)) `GenerateRequest` and `ChatRequest` carry:

> `// Logprobs specifies whether to return log probabilities of the output tokens.`
> `Logprobs bool \`json:"logprobs,omitempty"\``
> `// TopLogprobs is the number of most likely tokens to return at each token position, each with an associated log probability. Only applies when Logprobs is true.`
> `// Valid values are 0-20. Default is 0 (only return the selected token's logprob).`
> `TopLogprobs int \`json:"top_logprobs,omitempty"\``

Both `GenerateResponse` and `ChatResponse` carry `Logprobs []Logprob`, where `Logprob` embeds `TokenLogprob {Token string; Logprob float64; Bytes []int}` and adds `TopLogprobs []TokenLogprob`. Streaming chunks include the field. Shipped in **Ollama v0.12.11**. **Max K = 20, on both endpoints, streaming included.** Output tokens only — no prompt logprobs.

**The .NET gap you need to know about:** `OllamaSharp` (checked against `main`, ≥ the 5.4.30 on NuGet) implements `Logprobs`/`TopLogprobs` in `Models/Generate.cs` only — on `GenerateRequest`, `GenerateResponseStream`, and a `Logprob` class with nested `TopLogprobs`. Grepping `Models/Chat/` for "logprob" returns **nothing**. So through OllamaSharp you can only get logprobs via `/api/generate`, which means you must apply the chat template yourself (or use `Raw = true`) — and then your token boundaries and the server's agree by construction, which is arguably a feature for this app. Alternatives: PR the field onto `ChatRequest`, or hand-roll the `/api/chat` call.

Also: [issue #13638](https://github.com/ollama/ollama/issues/13638) reports **logprobs are not returned from Ollama Cloud (ollama.com)** even when requested. If you support Ollama's hosted tier, probe rather than assume.

#### 1f. Consolidated capability matrix

| Backend | Chosen-token logprob | Top-K alternatives | Max K | Streaming logprobs | Prompt/input logprobs | Full vocab | Attention |
|---|---|---|---|---|---|---|---|
| OpenAI — gpt-4o / gpt-4.1 (Chat Completions) | ✅ | ✅ | 20 (spec) | ✅ | ❌ | ❌ | ❌ |
| OpenAI — gpt-5.x / o-series | ❌ | ❌ | — | ❌ | ❌ | ❌ | ❌ |
| OpenAI — Responses API (non-reasoning) | ✅ | ✅ | 20 (spec) | ✅ | ❌ | ❌ | ❌ |
| Azure OpenAI — non-reasoning deployments | ✅ | ✅ | **5** (runtime) | ✅ | ❌ | ❌ | ❌ |
| Azure OpenAI — reasoning deployments | ❌ (documented unsupported) | ❌ | — | ❌ | ❌ | ❌ | ❌ |
| Bedrock `Converse`/`ConverseStream` (any model) | ❌ | ❌ | — | ❌ | ❌ | ❌ | ❌ |
| Bedrock — Anthropic Claude | ❌ | ❌ | — | ❌ | ❌ | ❌ | ❌ |
| Bedrock — Nova / Llama / Mistral | ❌ | ❌ | — | ❌ | ❌ | ❌ | ❌ |
| Bedrock — Cohere Command (legacy) | ✅ (`likelihood`) | ❌ | — | ✅ | ✅ (`ALL`) | ❌ | ❌ |
| Bedrock — Custom Model Import (BedrockCompletion) | ✅ | ❌ (top-1 only) | 1 | ✅ | ❌ | ❌ | ❌ |
| Bedrock — Custom Model Import (OpenAI*Completion) | ✅ | ✅ | UNCONFIRMED | ✅ | **✅ `prompt_logprobs`** | ❌ | ❌ |
| Bedrock — gpt-oss via `/openai/v1` | UNCONFIRMED | UNCONFIRMED | — | UNCONFIRMED | ❌ | ❌ | ❌ |
| Ollama (`/api/generate` and `/api/chat`) | ✅ | ✅ | **20** | ✅ | ❌ | ❌ | ❌ |
| **LLamaSharp** | ✅ | ✅ | **\|V\|** | ✅ | **✅** | **✅** | ❌ |
| **ORT GenAI** | ✅ | ✅ | **\|V\|** | ✅ | ✅* | **✅** | ❌ |

\* model-export dependent.

---

### 2. Tokenization in .NET

**`Microsoft.ML.Tokenizers` 2.0.0 (GA, 2025-11-11).** Implementations present in [`dotnet/machinelearning` `src/Microsoft.ML.Tokenizers/Model/`](https://github.com/dotnet/machinelearning/tree/main/src/Microsoft.ML.Tokenizers/Model) on `main` (see also the [how-to guide](https://learn.microsoft.com/en-us/dotnet/ai/how-to/use-tokenizers)):

| Class | Family |
|---|---|
| `BpeTokenizer` | classic BPE from `vocab.json` + `merges.txt` |
| `TiktokenTokenizer` | OpenAI tiktoken (cl100k, o200k, p50k, p50k_edit, r50k, gpt2, **o200k_harmony**) |
| `LlamaTokenizer` | Llama (SentencePiece `tokenizer.model`) |
| `SentencePieceTokenizer` (+ `SentencePieceBpeModel`, `SentencePieceUnigramModel`) | SentencePiece, both BPE and Unigram |
| `WordPieceTokenizer` | WordPiece |
| `BertTokenizer` | BERT (normalizer + WordPiece + special tokens) |
| `CodeGenTokenizer` | CodeGen |
| `Phi2Tokenizer` | Phi-2 |
| `EnglishRobertaTokenizer` | RoBERTa |

So: **BPE ✅, TikToken ✅, SentencePiece ✅ (BPE and Unigram), Llama ✅, WordPiece ✅.**

**Loading a tokenizer for a model.**
- Tiktoken: `Tokenizer t = TiktokenTokenizer.CreateForModel("gpt-5");` — the model→encoding table (`TiktokenTokenizer.cs:1040–1147`) currently maps `gpt-5` through **`gpt-5.6`**, `gpt-4.1`, `gpt-4o`, `chatgpt-4o`, `o1-`/`o3-`/`o4-mini-` → `o200k_base`; `gpt-4`, `gpt-3.5`, `gpt-35*` (Azure deployment names!) → `cl100k_base`; `gpt2` → GPT-2; `ft:` fine-tune prefixes handled. **`gpt-oss-` → `O200kHarmony`**, which resolves (line 1232) to the o200k ranks file plus `CreateHarmonyEncodingSpecialTokens()` — so harmony works with the `Data.O200kBase` package and needs no separate data package (there isn't one).
- Llama / SentencePiece: `LlamaTokenizer.Create(stream)` from a `tokenizer.model`.
- BPE: `BpeTokenizer.Create(vocabStream, mergesStream)`.

**The API that matters for this app** is not `CountTokens` but:

```csharp
IReadOnlyList<EncodedToken> tokens = tokenizer.EncodeToTokens(text, out string? normalized);
```

because `EncodedToken` is:

```csharp
public readonly struct EncodedToken {
    public int Id { get; }
    public string Value { get; }
    public Range Offset { get; }   // "the offset mapping to the original string"
}
```

**`Offset` is the whole ballgame for attribution.** It is the only supported way to draw a rectangle around the source characters that a token came from, which is what "token attribution back to input spans" needs at the presentation layer. Neither SharpToken nor TiktokenSharp gives you this.

Also useful: `GetIndexByTokenCount` / `GetIndexByTokenCountFromEnd` for trimming to a budget without re-encoding, and `EncodeToIds(span, considerNormalization: false, considerPreTokenization: false)` for exercising the tokenizer's stages independently — a genuinely nice "tokenization analysis" feature for a debugging shell.

**Can it tokenize identically to a remote provider's model?** Partially, and you must be honest about the boundary:
- **Yes** at the encoding level for OpenAI models whose encoding is one of the five shipped data packages. Same ranks, same regex (the source notes the patterns are *"based on https://github.com/openai/tiktoken/blob/main/tiktoken_ext/openai_public.py"*), same special-token maps.
- **No** at the *request* level. The server tokenizes a rendered chat template — role headers, tool schemas, `<|im_start|>`-family scaffolding, harmony channel markers, image placeholders — that you do not control and that OpenAI does not publish. Your token count for the message text will not equal `usage.prompt_tokens`, and your token *boundaries* at the joins will differ. Display your count as "content tokens (local estimate)" next to the server's `prompt_tokens` and let the delta be visible; that difference is itself an interesting debugging datum, and pretending it is zero is a lie the users of a *debugging* shell will catch.
- **No** for arbitrary HuggingFace models unless the tokenizer is one of the supported families and you have its `tokenizer.model`/`vocab`+`merges`. There is **no `tokenizer.json` loader** in `Microsoft.ML.Tokenizers` — I looked; the package has `Model/`, `Normalizer/`, `PreTokenizer/`, `Utils/` and no HF-JSON deserializer. For byte-exact parity with an arbitrary HF tokenizer, `Tokenizers.DotNet` 1.4.1 wraps the Rust `tokenizers` crate.
- **For local GGUF models, don't use any of these.** Use the tokenizer embedded in the GGUF via `SafeLlamaModelHandle.Tokenize` / `TokenToSpan`. It is by definition the exact tokenizer the model is running, including the model's special tokens. Using ML.Tokenizers against a GGUF would introduce a second, subtly different tokenizer for no benefit.

**Maintenance verdict.** All three tiktoken libraries are alive — `SharpToken` 2.0.6 (2026-03-25), `TiktokenSharp` 1.2.1 (2026-02-22), `Microsoft.ML.Tokenizers` 2.0.0 GA / 3.0.0-preview. `Microsoft.ML.Tokenizers` wins on breadth (it is the *only* one that covers Llama/SentencePiece/WordPiece, which you need for local models and for tokenization-comparison features), on offsets, and on institutional backing; [Microsoft's own porting guide](https://github.com/dotnet/machinelearning/blob/main/docs/code/microsoft-ml-tokenizers-migration-guide.md) directs `SharpToken` and `Microsoft.DeepDev.TokenizerLib` users to it. `SharpToken` wins only if you never touch a non-OpenAI tokenizer and want a smaller dependency — which this app does not.

---

### 3. Token attribution — what is real and what is hand-waving

Let me be blunt about each of the four techniques.

#### Attention-weight extraction — **not achievable, on any backend, without forking C++**

- **Hosted (OpenAI/Azure/Bedrock):** no API returns attention. Not partially, not for any model.
- **llama.cpp / LLamaSharp:** the attention scores exist as an internal graph tensor conventionally named `kq`, but the compute graph is not meaningfully exposed through the public C API ([llama-cpp-python discussion #1799](https://github.com/abetlen/llama-cpp-python/discussions/1799), [issue #237](https://github.com/abetlen/llama-cpp-python/issues/237)), so there is no supported route to it — you would have to walk graph nodes with `ggml_*` functions from a patched build. Worse, with flash-attention kernels enabled (the default on most modern builds) the attention matrix is **never materialised** — the softmax is fused into the kernel — so there is literally nothing to read. To get attention you would fork llama.cpp, disable flash attention, add an export hook, and ship your own native binaries for Windows and Linux. For a self-contained-binary CLI, that is a whole second product.
- **ORT GenAI:** attention in exported LLM graphs is a fused `com.microsoft.Attention` / `GroupQueryAttention` node. Attention probabilities are not a graph output. You could do ONNX graph surgery to add intermediate outputs, but only for unfused exports, and it would break the optimised kernels you export for in the first place.
- **And even if you could:** the interpretability literature is clear that raw attention is a poor explanation — the "attention is not explanation" line of work ([Faithfulness Violation Test, ICML 2022](https://proceedings.mlr.press/v162/liu22i/liu22i.pdf); [Attention Consistency for LLMs Explanation, EMNLP Findings 2025](https://aclanthology.org/2025.findings-emnlp.91.pdf)) shows large attention weights do not reliably indicate causal importance, different heads attend to different things, aggregate patterns differ from per-head patterns, and attention-rollout aggregates produce diffuse, noisy maps. Shipping an attention heat-map as "why the model said this" would be selling the user a picture that does not answer their question.

**Verdict: do not promise attention visualisation. If a PRD contains it, cut it.**

#### Gradient-based saliency (input × gradient, SmoothGrad) and integrated gradients — **not feasible in .NET for this app**

You need `∂ logit_target / ∂ embedding_input`, which requires reverse-mode autodiff over the model graph.

- `TorchSharp` 0.107.0 (2026-05-07) with `libtorch-cpu` 2.10.0 has real autograd. But TorchSharp gives you tensors and ops, not model architectures — you would have to re-implement Llama/Qwen/Phi forward passes in C# and write safetensors→TorchSharp weight loading, per architecture. That is a multi-month project that duplicates what llama.cpp already does, and it forfeits GGUF quantisation.
- `Microsoft.ML.OnnxRuntime.Training` — the only "gradients in .NET without PyTorch" story — has a **latest stable of 1.19.2 published 2024-09-03**, roughly two years stale. Building your differentiating feature on it would be negligent.
- ORT GenAI and LLamaSharp are inference-only by construction.

Integrated gradients additionally needs *m* forward+backward passes along an interpolation path (typically 20–300), so it is strictly worse than the above.

**Verdict: gradient saliency and integrated gradients are out of scope for a .NET implementation. Say so in the PRD rather than leaving them as aspirational.**

#### Occlusion / leave-one-out perturbation — **real, model-agnostic, and the right answer**

This is the technique that actually works everywhere, and it is a first-class method in the reference tooling for this problem space ([`inseq`](https://github.com/inseq-team/inseq), the standard sequence-attribution library, implements exactly Saliency, Integrated Gradients, **Occlusion**, attention attribution, and contrastive attribution).

The critical detail most implementations get wrong: **occlusion attribution requires teacher-forced scoring of a *fixed* completion.** If you ablate a span and *re-generate*, you are measuring output volatility, not attribution — the sampler noise swamps the signal. You must hold the output tokens `y` constant and re-score them under the ablated prompt.

That requirement is exactly why the backend matrix matters:

| Backend | Can score a fixed completion? | So can it do occlusion attribution? |
|---|---|---|
| LLamaSharp | Yes — decode `prompt' ⧺ y` with `logits:true` at the `y` positions | **Yes, properly** |
| ORT GenAI | Yes — `AppendTokens(prompt' ⧺ y)`, read `GetOutput("logits")` | Yes (subject to prompt-logit export) |
| Bedrock CMI (OpenAI*Completion) | Yes — `prompt_logprobs: 1` over `prompt' ⧺ y` | Yes |
| Bedrock Cohere Command | Yes — `return_likelihoods: "ALL"` | Yes (legacy model only) |
| Ollama | **No** — no prompt logprobs, no echo | Only a degraded "regenerate and diff" |
| OpenAI / Azure OpenAI Chat Completions | **No** | Only a degraded "regenerate and diff" |

#### The defensible implementation for a local model

Three layers, in increasing cost. Ship all three; label each honestly in the UI.

**Layer 1 — Prompt surprisal (1 forward pass, exact, not attribution).**
Tokenize the prompt with the model's own vocab. Build one `LLamaBatch` with `logits: true` at every position (chunked for memory). Decode. For each position `t`, read `GetLogitsIth(t-1)`, softmax, and take `−log p(x_t)`. Render as a heat-map over the input using `EncodedToken.Offset`-equivalent byte offsets from `TokenToSpan`. This answers "which parts of my prompt did the model find unexpected" — a genuinely useful debugging signal, cheap, and *exactly* true. Do not label it attribution.

**Layer 2 — Contrastive occlusion over spans (N+1 prefills, this is the attribution).**
1. Generate `y` normally, recording per-token logprobs from the sampling-pipeline decorator.
2. Compute the baseline score `S₀ = Σ_t log p(y_t | x, y_<t)` — you already have it from step 1.
3. Segment the prompt into candidate spans: conversation messages → lines → sentences → words, chosen by the user's zoom level. Coarse-to-fine keeps N small.
4. For each span `s`: build `x_{-s}` (delete the span, or replace it with a neutral filler of similar token length — deletion shifts positions, replacement preserves them; offer both and note which you used, because they answer slightly different questions). Decode `x_{-s} ⧺ y` with logits at the `y` positions. Compute `S_s`.
5. Attribution of span `s` = `S₀ − S_s`, in nats. Positive = the span *supported* the output; negative = the span *worked against* it. Report in nats-per-output-token so spans are comparable across completions of different length.
6. **Also** report `D_KL(P_full(·|x, y_<t) ‖ P_ablated(·|x_{-s}, y_<t))` per output position, summed. You already have full-vocab distributions on the local backend, so this costs nothing extra and is a much richer measure than the scalar delta — it captures "this span changed what the model was considering" even when it did not change the score of the token that was actually emitted.
7. Cache the KV state for the longest common prefix across ablations. With a system prompt + history that is usually most of the context, this can cut the cost by an order of magnitude.
8. Cost is `O(N)` full prefills. Show the user the estimate ("47 spans × 1,850 tokens ≈ 87k tokens of prefill, ~12 s") and require confirmation. This is a debugging shell; an expensive, explicit, opt-in command is correct.

**Layer 3 — Single-token counterfactual (2 prefills, for the "why this token" question).**
Given a specific output token `y_t` and an alternative `y'_t` from its top-K, compute the *contrastive* occlusion target `log p(y_t) − log p(y'_t)` and attribute *that* across spans. This is what tells the user "the word 'urgent' in your prompt is why it chose 'immediately' over 'soon'", which is the question people actually have.

**What to write in the UI.** "Attribution is measured by ablation: we re-score the same output with each input span removed. It is a causal measure of that span's contribution under this model, not a claim about the model's internal mechanism." That sentence is the difference between a credible tool and a plausible-looking one.

---

### 4. The math of presenting probabilities

**Logprob → probability.** `p = exp(ℓ)`. Two traps:
1. **The `-9999.0` sentinel** (OpenAI/Azure) is not a logprob. Filter it before any arithmetic; render it as "outside top-20", not as `p ≈ 0`.
2. **Cohere's `likelihood`** is a log-likelihood, not a probability, despite the name. Its sequence-level `likelihood` is documented as *"the average of the token likelihoods"* — i.e. mean log-prob, so `PPL = exp(−likelihood)` directly.

**Top-K normalisation — the single most commonly botched thing in these tools.** The K logprobs a provider returns are **absolute probabilities over the full vocabulary**, so `Σ_{i∈K} p_i ≤ 1`. The residual `m = 1 − Σ_{i∈K} p_i` is real mass belonging to the ~200,000 tokens you were not shown.

- **Do not silently renormalise.** Rendering `q_i = p_i / Σ_K p_j` makes a position where the top-5 hold 12% of the mass look identical to one where they hold 99.8%, which destroys precisely the information a debugging tool exists to show.
- **Do** show absolute `p_i` bars plus an explicit "other (`m`)" bar. If you additionally offer a renormalised view for comparing the shortlist, label the toggle and put `m` in the header.
- On LLamaSharp/ORT GenAI, `m = 0` by construction. Make that visible too — "full vocabulary" vs "top-5 of 200,019" is exactly the backend-fidelity signal the user needs.

**Entropy per position.** `H_t = −Σ_{v∈V} p_v log p_v` (nats; divide by `ln 2` for bits).
- **Local backends:** compute it exactly over the full softmax. This is a headline capability no hosted backend can match.
- **Hosted top-K only:** you cannot compute `H_t`. You can bracket it:
  - Lower bound: `H_K = −Σ_{i∈K} p_i log p_i` treating the residual as a single token: `H ≥ H_K − m log m`.
  - Upper bound: residual spread uniformly over `|V| − K` tokens: `H ≤ H_K − m log(m / (|V| − K))`.
  - The bounds need `|V|`, which you know from the tokenizer, not from the API. Render as an interval or as `H_K` with an explicit "truncated at K=5" badge. Reporting a bare number computed from 5 of 200,000 tokens as "entropy" is wrong and, in a tool that exists to be precise, indefensible.
- Normalised entropy `H / log|V| ∈ [0,1]` is the right thing to put in a sparkline, because it is comparable across models with different vocabularies.

**Perplexity.** `PPL = exp(−(1/N) Σ_{t=1..N} log p(y_t))`. Report it over the generated tokens, and separately over the prompt when the backend gives you prompt logprobs (LLamaSharp, ORT GenAI, Bedrock CMI, Cohere `ALL`). Prompt perplexity is the useful one for debugging: it tells you whether your carefully-engineered system prompt reads as natural text or as line noise to this particular model.

**Other per-position scalars worth having:** margin `p₁ − p₂` (how close was the decision), rank of the emitted token within the sorted distribution (a great integer to put in a table — "emitted token was rank 7"), and the sequence logprob `Σ log p` for ranking candidate generations.

**The temperature trap.** Decide and document whether you display the **raw model distribution** (softmax of the raw logits at T=1) or the **post-sampler distribution** (after temperature, top-p, top-k, repetition penalties). On LLamaSharp, `GetLogitsIth` gives you the raw logits *before* the sampler chain runs, and `LLamaTokenDataArray.Softmax()` gives you the T=1 distribution; if you instead read the array *after* `chain.Apply(...)`, you get the truncated, temperature-scaled one. These are different numbers and a debugging tool should be able to show both, side by side, clearly labelled. For hosted APIs, whether returned logprobs are pre- or post-temperature is **UNCONFIRMED** — the docs do not say. Flag it in the UI rather than guessing.

**Standard visualisations, and what they need.**

| Visualisation | Input | Backend requirement |
|---|---|---|
| Inline token heat-map (each token background-coloured by `p` or by surprisal `−log p`) | per-token chosen logprob | Any backend with logprobs |
| Top-K table / horizontal bar chart at a selected position, with "other" bar | top-K + residual | Top-K backends |
| Entropy / surprisal sparkline along the sequence | per-position `H` or `−log p` | Exact: local only; bounded: hosted |
| Vocabulary probability map | full distribution | **Local only.** A literal 200k-wide map is meaningless; render as a **rank–probability curve on log–log axes** (the Zipf view — a heavy tail at a low-confidence position looks visibly different from a sharp one) or as a log-binned histogram of probability mass. This is a genuine differentiator no hosted backend can offer. |
| Attribution heat-map over input spans | occlusion deltas | Backends that can score a fixed completion |
| Confusion / alternatives grid (positions × top-K, cells shaded by `p`) | top-K over a window | Top-K backends |

**Colour, briefly:** sequential scale for probability and surprisal (perceptually uniform — viridis/magma family, not rainbow); **diverging, zero-centred** scale for attribution deltas, because sign is meaningful there. Encode probability as *lightness*, not hue, so it survives a monochrome terminal, and keep a numeric column in the table for anyone who cannot see the colour at all. In `Spectre.Console` 0.57.2, detect truecolor support and degrade to a 24-step greyscale ramp, then to `░▒▓█` block characters.

**One rendering hazard specific to this domain:** a token is a byte sequence, not a character. Both OpenAI (`ChatTokenLogProbabilityDetails.Utf8Bytes`, typed `ReadOnlyMemory<byte>?`) and Ollama (`bytes []int`) return raw bytes *precisely because* a token can be half a UTF-8 codepoint or half an emoji. Rendering `token.Value` directly will produce replacement characters and mis-measured column widths in a grid. Reassemble from the byte fields, and for a still-incomplete sequence render a visible placeholder (e.g. `‹e2 96›`) rather than a broken glyph.

---

## What this application specifically needs

Tying each recommendation to a concrete operation the shell performs:

1. **"Show me the probability of every token you just wrote", streaming, as it types.**
   → OpenAI 2.13.0: `ChatCompletionOptions { IncludeLogProbabilities = true, TopLogProbabilityCount = 5 }`, then read `StreamingChatCompletionUpdate.ContentTokenLogProbabilities` on each update. **Do not** call `.ToChatResponse()` on a MEAI stream and expect to recover them — `ChatResponseUpdate`'s own doc comment warns that *"updates all have different RawRepresentation objects whereas there's only one slot for such an object available in ChatResponse.RawRepresentation"*, so the per-chunk logprobs are dropped on collapse. Capture per-update, into your own model.
   → LLamaSharp: an `ISamplingPipeline` decorator set on `InferenceParams.SamplingPipeline` that records `ctx.GetLogitsIth(index)` before delegating.
   → Ollama: `GenerateRequest { Logprobs = true, TopLogprobs = 20 }` via OllamaSharp's generate path.

2. **"Show me the top-K alternatives at position 14 as a bar chart with the residual mass."**
   → Needs `top_logprobs`. K = 5 on Azure, 20 on OpenAI/Ollama, `|V|` locally. The shell must display `K` and the residual so the user knows how much of the distribution they are looking at.

3. **"Show me a probability map over the whole vocabulary at the position where it went wrong."**
   → **Local only.** `LLamaTokenDataArray.Create(ctx.GetLogitsIth(i))` then `.Softmax()` gives you a descending-sorted `LLamaTokenData[]` over all `|V|` tokens in one call. Render as a rank–probability log–log curve. This is the feature that justifies bundling a local backend at all.

4. **"Which words in my prompt caused that?"**
   → Layer-2 contrastive occlusion on LLamaSharp/ORT GenAI (or Bedrock CMI). Requires per-position logit flags on the batch — `LLamaBatch.Add(token, pos, seq, logits: true)` — which is the specific API that makes it possible.
   → On Azure/OpenAI/Ollama: **disabled**, with a message naming the missing capability. Do not ship a degraded regenerate-and-diff version under the same command name.

5. **"Tokenize this and show me the boundaries, mapped to my source text."**
   → `Microsoft.ML.Tokenizers` `EncodeToTokens` → `EncodedToken.Offset` (`Range` into the original string) for remote models; `SafeLlamaModelHandle.Tokenize` + `TokenToSpan` for local GGUF. Do **not** use the remote tokenizer for a local model or vice-versa.

6. **"Compare how three models tokenize this string."**
   → `TiktokenTokenizer.CreateForModel(...)` for the OpenAI family (note `gpt-35*` Azure deployment names are handled), `LlamaTokenizer.Create(stream)` / `SentencePieceTokenizer` for open-weights, GGUF-embedded for the local model. This is a natural `Xcaciv.Command` tool with a model-name argument.

7. **"Perplexity of this prompt under this model."**
   → LLamaSharp with logits at every prompt position; Bedrock CMI with `prompt_logprobs`; Cohere Command with `return_likelihoods: "ALL"`. Impossible on Azure/OpenAI/Ollama.

8. **"Store the introspection with the conversation."**
   → Because MEAI has no logprob type, define your own serialisable record set and persist it alongside history: `TokenObservation { int Index; int? TokenId; string Text; byte[] Utf8Bytes; double LogProb; int Rank; IReadOnlyList<Alternative> TopK; double ResidualMass; double? Entropy; bool EntropyIsExact; }`. Carry `ResidualMass` and `EntropyIsExact` in the *stored* record, not just the view model — six months later, nobody will remember which backend produced a given transcript.

9. **Hosting under Xcaciv.Cupcake / Xcaciv.Command / Xcaciv.Loader.**
   → Because backends differ so sharply, make capability a *declared, queryable* property, not a try/catch. Each backend plugin (loaded via `Xcaciv.Loader`) exposes:
   ```csharp
   public sealed record IntrospectionCapabilities(
       bool OutputLogProbs, int MaxTopK, bool StreamingLogProbs,
       bool PromptLogProbs, bool FullVocabulary, bool TeacherForcedScoring,
       int? VocabularySize, string ProbeStatus);   // Verified | Assumed | Failed
   ```
   Each `Xcaciv.Command` tool declares the capabilities it requires; the shell greys out and explains, rather than failing at call time. And because several of these limits are *only* discoverable at runtime (Azure's K≤5; gpt-5.x 500-ing at K≥2; Bedrock gpt-oss unknown), ship a **`probe` command**: on first use of a deployment, issue one tiny request at `top_logprobs = MaxTopK`, halve on 400/500 until it succeeds, cache the discovered value against the deployment name in settings. That converts three of my UNCONFIRMEDs into a runtime fact for each user's actual deployment.

---

## Risks, sharp edges and what you give up

**What you give up by centring the design on LLamaSharp.** You accept a `0.x` dependency with breaking changes between minor versions, native backend packages per platform/accelerator that complicate a self-contained single-file publish (`LLamaSharp.Backend.*` carry large native payloads; plan for per-RID publish profiles and expect the Linux and Windows binaries to diverge in size considerably), and a hard coupling to llama.cpp's GGUF support timeline for new architectures. You also give up any hope of showing the *hosted* frontier models the way you show the local one — the richest views in your app will be permanently local-only.

**The hosted-provider risk is the headline risk.** OpenAI is visibly walking away from logprobs on its frontier line: unsupported on all reasoning models per Microsoft's own docs (2026-08-20), 500s at K≥2 on gpt-5.2/5.3/5.4 with a thread closed unresolved on 2026-08-25. A tool whose differentiating feature is token introspection is building on a capability its largest provider is deprecating in practice. Mitigations: (a) make the local backend the *demonstration* backend, not the fallback; (b) route hosted introspection through gpt-4o/gpt-4.1-class deployments explicitly and warn when the selected deployment is a reasoning model; (c) instrument the probe command so you learn about withdrawals from telemetry rather than from bug reports.

**`Azure.AI.OpenAI` is a trap.** Latest stable 2.1.0 (2024-12-06), latest anything 2.9.0-beta.1 (2026-03-13). Taking the stable line means a 20-month-old dependency; taking the active line means shipping a beta. The way out is that Azure now exposes an OpenAI-shaped `/openai/v1` data plane, so use the GA `OpenAI` 2.13.0 package with a custom endpoint for both providers, and keep one client abstraction instead of two.

**ORT GenAI's documentation is actively misleading.** The published C# API page still documents `ComputeLogits()` and a `Tensor.Data` property that do not exist in `main`. Anyone implementing from the docs will write code that does not compile. Budget time for reading `src/csharp/*.cs` directly, and pin the exact version — the API is explicitly declared preview and subject to change.

**OllamaSharp's chat gap is a real, dated defect, not a design choice.** The server supports logprobs on `/api/chat`; the client does not model it. Either PR it upstream, or route Ollama introspection through `/api/generate` with `Raw = true` and own the chat templating — which, for a tool that also wants to show tokenization, is arguably the better architecture anyway, since it makes the exact prompt bytes visible to the user.

**Memory.** Full-vocab logits are `4 × positions × |V|` bytes. Qwen3's 151k vocab over a 4,000-token prompt is ~2.4 GB if you flag every position in one batch. Chunk prefill, reduce eagerly, and never hold more than a window of full distributions in the conversation store — persist top-K plus scalars, and recompute the full map on demand.

**Occlusion attribution is expensive and you must say so.** N+1 prefills. Without KV-prefix caching a 40-span attribution over a 2k-token context is 80k+ tokens of prefill. Gate it behind explicit confirmation with a cost estimate.

**Bedrock's prefix-cache interaction.** AWS states plainly that requesting prompt logprobs on Custom Model Import *"will ignore the prefix cache and recompute the prefill of full prompt"*. In an interactive shell with a long, stable system prompt, turning on prompt logprobs converts every turn from a cached prefill into a full one. Make it a per-command flag, never a global setting.

**Cohere Command is a legacy escape hatch, not a plan.** `cohere.command-text-v14` is on the old generation API; `return_likelihoods` does not exist on the Command R/R+ chat surface. Treat it as a demo of "Bedrock can do this for one model", not as the Bedrock story.

**Do not promise what you cannot deliver.** Attention maps: cut. Gradient saliency: cut. Integrated gradients: cut. If a stakeholder wants those, the honest answer is "that is a Python/PyTorch tool, and it is a different product." What you *can* deliver — exact full-vocabulary distributions, exact entropy, exact prompt perplexity, and causal ablation attribution, all on a local GGUF model, in a terminal, from a single self-contained binary — is genuinely differentiated and, as far as I can find, not offered by any existing .NET tool.

**Second-best options, and when each wins.**
- *Instead of LLamaSharp → ORT GenAI 0.15.2.* Wins if the target hardware is Windows-on-ARM/NPU or DirectML/QNN-accelerated, where ORT's execution providers beat llama.cpp, or if the org already standardises on ONNX artefacts. You lose GGUF (the PRD says GGUF), you take on a preview API, and you inherit the `num_logits_to_keep` export uncertainty.
- *Instead of `Microsoft.ML.Tokenizers` → `Tokenizers.DotNet` 1.4.1.* Wins the moment you must be byte-exact with an arbitrary HuggingFace `tokenizer.json` that is not one of the supported families. You take on a Rust native dependency across two platforms, which fights the self-contained-binary goal.
- *Instead of `Microsoft.ML.Tokenizers` → `SharpToken` 2.0.6.* Wins only for an OpenAI-only build with a hard dependency-size budget. You lose SentencePiece/Llama/WordPiece and, critically, you lose `EncodedToken.Offset`.
- *Instead of a custom backend abstraction → `Microsoft.Extensions.AI` 10.9.0.* Wins if introspection is genuinely optional and you value the middleware ecosystem (function invocation, caching, OpenTelemetry) more than typed logprobs. You would carry logprobs through `ChatOptions.RawRepresentationFactory` on the way in and provider-specific casts of `ChatResponseUpdate.RawRepresentation` on the way out — which is exactly a custom abstraction, just an unsafe and undiscoverable one.

**Graceful degradation — the design.** Five tiers, declared per backend, with a defined fallback at each step:

| Tier | Capability | Backends | Degradation when absent |
|---|---|---|---|
| 0 | Text only | any | Introspection commands hidden; `:caps` explains why |
| 1 | Chosen-token logprob | + Bedrock CMI (BedrockCompletion), Cohere | Heat-map and perplexity available; top-K views disabled |
| 2 | Top-K alternatives | + OpenAI/Azure (non-reasoning), Ollama, Bedrock CMI (OpenAI shapes) | Alternatives table shows K and residual mass; entropy shown as a **bounded interval** with a "truncated at K" badge |
| 3 | Prompt logprobs | + Bedrock CMI (OpenAI shapes), Cohere `ALL` | Prompt heat-map and prompt perplexity available |
| 4 | Full vocabulary + teacher-forced scoring | **LLamaSharp, ORT GenAI** | Vocabulary probability map, exact entropy, and occlusion attribution — all four tiers below are exact, and the residual-mass indicator reads `0.000` |

Two rules make this work in practice. **First: every derived number carries its provenance.** An entropy value knows whether it is exact or a K-truncated bound; a probability knows its residual mass; the transcript stores both. **Second: never fake a tier.** If the user asks for attribution on an Azure deployment, say "attribution requires teacher-forced scoring, which Azure OpenAI does not expose; this is available on the local backend" and offer to re-run the same prompt locally. In a debugging tool, an honest refusal is worth more than a plausible number.

---

## Unconfirmed

Everything below I could not establish from a primary source on 2026-08-28.

1. **Max `top_logprobs` / `prompt_logprobs` on Bedrock Custom Model Import.** AWS's `custom-model-import-advanced-features.html` shows only `1` in every example and states no bound. The response shapes (`prompt_logprobs`, `token_ids`, `kv_transfer_params` in the vision example) strongly suggest a vLLM OpenAI server underneath, which would default to 20, but AWS does not say so. *Looked at:* the advanced-features page, the CMI import page, the Sept-2025 AWS ML blog. **Resolve by probing.**
2. **Whether Bedrock's OpenAI-compatible endpoints (`bedrock-runtime/openai/v1` and `bedrock-mantle`) honour `logprobs`/`top_logprobs` for `openai.gpt-oss-20b/120b`.** AWS documents the request body purely by reference to OpenAI's docs and never enumerates supported parameters. *Looked at:* `inference-chat-completions-mantle.html`, `model-parameters-openai.html`. **Resolve by probing.**
3. **Whether Amazon Nova exposes any log-probability field through an undocumented `additionalModelRequestFields` key.** The Bedrock Nova page defers to the Nova user guide's "Complete request schema", which I did not fetch. I found no logprob parameter in any Bedrock Nova documentation. *Looked at:* `model-parameters-nova.html`, `Converse` API reference.
4. **AI21 Jamba on Bedrock.** Not checked. Jurassic-2 historically returned `tokens[].generatedToken.logprob` and `topTokens` and is retired; I did not verify whether Jamba carries anything equivalent. *Looked at:* nothing directly — this is a genuine gap.
5. **Whether OpenAI/Azure-returned logprobs are pre- or post-temperature.** Neither the OpenAI TypeSpec, the cookbook, nor the Azure reference states it. This materially affects how you label the chart. *Looked at:* `specification/base/typespec/chat/models.tsp`, the logprobs cookbook, the Azure v1 chat reference.
6. **The gpt-5.1 `reasoning_effort: "none"` carve-out on Azure specifically.** Microsoft's reasoning page lists `logprobs`/`top_logprobs` as unsupported for reasoning models with no carve-out; OpenAI community reports say gpt-5.1 with effort `none` accepted them and gpt-5.2+ regressed. I could not confirm current Azure behaviour. *Looked at:* `learn.microsoft.com/azure/foundry/openai/how-to/reasoning`, two OpenAI community threads.
7. **Whether the stock published ONNX model repos (e.g. `microsoft/Phi-*-onnx`) export logits for all prompt positions or only the last.** The onnxruntime-genai test suite parameterises over `num_logits_to_keep`, and its own test models return full-prompt logits, but this is a per-export property. *Looked at:* `test/python/test_onnxruntime_genai_api.py`, `docs/genai/reference/config`. **Resolve by inspecting the target model's ONNX outputs.**
8. **Whether Ollama's OpenAI-compatible `/v1/chat/completions` surface exposes logprobs**, as opposed to the native `/api/generate` and `/api/chat` (which definitely do). Issue #16117 requests it; I did not confirm it landed. *Looked at:* `ollama/ollama` `api/types.go`, `docs.ollama.com/api/generate`, issue titles only.
9. **Whether Ollama Cloud (ollama.com) returns logprobs.** Issue #13638 says it does not. I did not verify a fix. *Looked at:* search result titles only.
10. **Whether `Microsoft.ML.Tokenizers` 3.0.0-preview adds a HuggingFace `tokenizer.json` loader.** I inspected `main` (which corresponds to the 3.0 line) and found no such loader in `Model/`, `Normalizer/`, `PreTokenizer/` or `Utils/`, but I did not read every file. *Looked at:* the GitHub contents listing for `src/Microsoft.ML.Tokenizers` and its `Model/` subdirectory.
11. **Whether OpenAI's current model lineup still uses `o200k_base` for the gpt-5.x series**, or has introduced a newer encoding that `Microsoft.ML.Tokenizers` has not yet mapped. The library maps `gpt-5` through `gpt-5.6` to `O200kBase`, which is the maintainers' current belief; I did not independently verify against `tiktoken`'s `openai_public.py`. *Looked at:* `TiktokenTokenizer.cs` model map only.
12. **Retirement dates for gpt-4o / gpt-4.1 on Azure.** Since these are the deployments that actually support logprobs, their retirement schedule is a direct risk to this feature and should be checked against the Azure model-retirements page before committing.

---

## Sources

**Provider APIs**
- Azure OpenAI chat REST reference (v1 GA) — <https://learn.microsoft.com/en-us/rest/api/microsoft-foundry/azureopenai/chat?view=rest-microsoft-foundry-v1>
- Azure OpenAI v1 GA OpenAPI spec — <https://github.com/Azure/azure-rest-api-specs/blob/main/specification/ai/data-plane/OpenAI.v1/azure-v1-v1-generated.yaml>
- Azure OpenAI reasoning models (unsupported-parameter list; `ms.date` 2026-08-20) — <https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/reasoning>
- OpenAI TypeSpec for chat + responses (vendored in openai-dotnet) — <https://github.com/openai/openai-dotnet/tree/main/specification/base/typespec>
- OpenAI logprobs cookbook — <https://developers.openai.com/cookbook/examples/using_logprobs>
- OpenAI community: "You Are Not Allowed To Request Logprobs From This Model: gpt-5-chat-latest" — <https://community.openai.com/t/you-are-not-allowed-to-request-logprobs-from-this-model-gpt-5-chat-latest/1341462>
- OpenAI community: "Logprobs deprecated for gpt-5 models?" — <https://community.openai.com/t/logprobs-deprecated-for-gpt-5-models/1355427>
- OpenAI community: "GPT 5.2 LogProbs support removed?" (500s at `top_logprobs` ≥ 2 on gpt-5.2/5.3-codex/5.4; closed 2026-08-25) — <https://community.openai.com/t/gpt-5-2-logprobs-support-removed/1378114>
- deepeval#843 — Azure `top_logprobs` ≤ 5 error text — <https://github.com/confident-ai/deepeval/issues/843>
- azure-sdk-for-go#22538 — LogProbs/TopLogProbs rejected by Azure — <https://github.com/Azure/azure-sdk-for-go/issues/22538>

**Amazon Bedrock**
- `Converse` API reference — <https://docs.aws.amazon.com/bedrock/latest/APIReference/API_runtime_Converse.html>
- Advanced API features for imported models (logprobs, `prompt_logprobs`, cache caveat) — <https://docs.aws.amazon.com/bedrock/latest/userguide/custom-model-import-advanced-features.html>
- Custom Model Import — supported architectures & tokenizers — <https://docs.aws.amazon.com/bedrock/latest/userguide/model-customization-import-model.html>
- AWS ML blog, "Unlock model insights with log probability support for Amazon Bedrock Custom Model Import" (2025-09-12) — <https://aws.amazon.com/blogs/machine-learning/unlock-model-insights-with-log-probability-support-for-amazon-bedrock-custom-model-import/>
- Chat Completions API on the `bedrock-mantle` endpoint — <https://docs.aws.amazon.com/bedrock/latest/userguide/inference-chat-completions-mantle.html>
- OpenAI (gpt-oss) models on Bedrock — <https://docs.aws.amazon.com/bedrock/latest/userguide/model-parameters-openai.html>
- Meta Llama parameters — <https://docs.aws.amazon.com/bedrock/latest/userguide/model-parameters-meta.html>
- Mistral text-completion parameters — <https://docs.aws.amazon.com/bedrock/latest/userguide/model-parameters-mistral-text-completion.html>
- Cohere Command parameters (`return_likelihoods`, `token_likelihoods`) — <https://docs.aws.amazon.com/bedrock/latest/userguide/model-parameters-cohere-command.html>
- Amazon Nova parameters — <https://docs.aws.amazon.com/bedrock/latest/userguide/model-parameters-nova.html>
- Anthropic OpenAI-SDK compatibility (`logprobs` → Ignored / Always empty) — <https://platform.claude.com/docs/en/api/openai-sdk>

**Local runtimes**
- LLamaSharp — <https://github.com/SciSharp/LLamaSharp> (`LLama/Native/SafeLLamaContextHandle.cs`, `LLama/Native/LLamaBatch.cs`, `LLama/Native/LLamaTokenDataArray.cs`, `LLama/Sampling/ISamplingPipeline.cs`, `LLama/Common/InferenceParams.cs`, `LLama/Native/SafeLlamaModelHandle.cs`)
- onnxruntime-genai — <https://github.com/microsoft/onnxruntime-genai> (`src/ort_genai_c.h`, `src/csharp/Generator.cs`, `src/csharp/Tensor.cs`, `test/python/test_onnxruntime_genai_api.py`)
- ONNX Runtime GenAI C# API docs (stale; preview notice) — <https://onnxruntime.ai/docs/genai/api/csharp.html>
- ONNX Runtime GenAI config reference — <https://onnxruntime.ai/docs/genai/reference/config.html>
- Ollama API types (`Logprobs`, `TopLogprobs` 0–20 on Generate and Chat) — <https://github.com/ollama/ollama/blob/main/api/types.go>
- Ollama generate API docs — <https://docs.ollama.com/api/generate>
- ollama#16117 (logprobs on the OpenAI-compatible endpoint) — <https://github.com/ollama/ollama/issues/16117>
- ollama#13638 (logprobs not returned from Ollama Cloud) — <https://github.com/ollama/ollama/issues/13638>
- OllamaSharp — <https://github.com/awaescher/OllamaSharp> (`src/OllamaSharp/Models/Generate.cs`; no logprob members under `Models/Chat/`)

**.NET libraries**
- Microsoft.ML.Tokenizers source — <https://github.com/dotnet/machinelearning/tree/main/src/Microsoft.ML.Tokenizers>
- Tokenizer how-to — <https://learn.microsoft.com/en-us/dotnet/ai/how-to/use-tokenizers>
- Tokenizer migration guide — <https://github.com/dotnet/machinelearning/blob/main/docs/code/microsoft-ml-tokenizers-migration-guide.md>
- openai-dotnet — <https://github.com/openai/openai-dotnet> (`OpenAI/src/Custom/Chat/ChatCompletionOptions.cs`, `ChatTokenLogProbabilityDetails.cs`, `Streaming/StreamingChatCompletionUpdate.cs`; `OpenAI.Responses/src/Custom/CreateResponseOptions.cs`, `ResponseContentPart.cs`, `Generated/Models/IncludedResponseProperty.cs`)
- dotnet/extensions — `Microsoft.Extensions.AI.Abstractions` (`ChatOptions.cs`, `ChatResponse.cs`, `ChatResponseUpdate.cs`) — <https://github.com/dotnet/extensions/tree/main/src/Libraries/Microsoft.Extensions.AI.Abstractions>
- NuGet version/date data queried via the registration API — <https://api.nuget.org/v3/registration5-gz-semver2/{id}/index.json>

**Attribution / interpretability**
- inseq (Saliency, Integrated Gradients, Occlusion, attention attribution, contrastive attribution) — <https://github.com/inseq-team/inseq>
- "Rethinking Attention-Model Explainability through Faithfulness Violation Test", ICML 2022 — <https://proceedings.mlr.press/v162/liu22i/liu22i.pdf>
- "Attention Consistency for LLMs Explanation", EMNLP Findings 2025 — <https://aclanthology.org/2025.findings-emnlp.91.pdf>
- llama-cpp-python discussion #1799 / issue #237 — attention weights not exposed — <https://github.com/abetlen/llama-cpp-python/discussions/1799>, <https://github.com/abetlen/llama-cpp-python/issues/237>
