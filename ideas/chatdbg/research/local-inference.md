# Local open-weights model inference in .NET with token-level introspection

*Research date: 2026-08-28. Every version number, package, date and capability claim below was checked against nuget.org, the GitHub API, or upstream source on the live web on that date. Where I could not confirm something, it is listed under **Unconfirmed** rather than guessed.*

---

## Bottom line — the recommendation in three sentences

Stay on **LLamaSharp**, but move from the pinned 0.25.0 to **0.27.0** (the current NuGet stable, published 2026-04-26, bound to llama.cpp commit `3f7c29d3`), and rewrite the introspection path to use the **low-level `BatchedExecutor` + `Conversation.Sample()` → `LLamaTokenDataArray.Softmax()`** idiom instead of the reflection-based `GetLogits` probe the source app currently uses, because that idiom returns a genuine `Span<float>` over the entire vocabulary and is the only .NET-hosted option that can produce true full-vocabulary probability maps, per-prompt-token logits for input attribution, and a custom sampling chain in one process. Split the backend packages behind `Condition`-guarded `PackageReference`s and per-RID publishes so the CUDA blobs (213 MiB Windows + 215 MiB Linux) never ship in the CPU-only binary, and register `LLamaSharp.Backend.Vulkan` as the GPU fallback for AMD/Intel machines. If in-process native code is judged too dangerous for a debugging shell — and there is a real argument that it is, since an access violation inside `ggml` takes the whole shell down with the user's unsaved conversation — the second-best option is a supervised **`llama-server` subprocess** speaking its native `/completion` endpoint with `n_probs`, which isolates the crash and still returns per-generated-token top-K logprobs, at the cost of losing prompt-token logits, raw logits, and custom sampling stages.

---

## Landscape — the real options

| Option | Current version | Status | Last published | One-line verdict |
|---|---|---|---|---|
| **LLamaSharp** | **0.27.0** | GA (0.x but stable release cadence) | **2026-04-26** ([nuget](https://www.nuget.org/packages/LLamaSharp)) | The only .NET option that gives raw logits over the full vocab in-process. Recommended. |
| LLamaSharp `v0.29.0` | tagged on GitHub **2026-08-24** | **Tagged but NOT on NuGet as of 2026-08-28** ([releases](https://github.com/SciSharp/LLamaSharp/releases)) | — | Don't plan on it. It removes `LLamaSharp.SemanticKernel`, `LLamaSharp.kernel-memory` and `LLama.Experimental`. Watch for the NuGet push. |
| LLamaSharp.Backend.Cpu | 0.27.0, **34.65 MiB** nupkg | GA | 2026-04-26 | Mandatory baseline. Ships Metal for `osx-arm64`, and win-arm64 as of 0.27.0. |
| LLamaSharp.Backend.Cuda12 (+`.Windows` 213 MiB, `.Linux` 215 MiB) | 0.27.0 | GA | 2026-04-26 | Huge. Must be RID-gated or made an optional side-load. |
| LLamaSharp.Backend.Vulkan (+`.Windows` 19 MiB, `.Linux` 19 MiB) | 0.27.0 | GA | 2026-04-26 | The pragmatic GPU fallback: AMD/Intel/NVIDIA, 1/11th the size of CUDA. |
| LLamaSharp.Backend.Cuda11 | 0.24.0 | **Dropped** — no 0.25/0.26/0.27 build | 2025-05-14 | Do not reference. Effectively abandoned. |
| LLamaSharp.Backend.OpenCL | 0.13.0 | **Abandoned** — 2+ years stale, 5.4k lifetime downloads | **2024-06-04** | Dead. Vulkan superseded it upstream. |
| `LLamaSharp.Backend.MacMetal` | 0.7.0 | **Abandoned** | (old) | Superseded — Metal now ships inside `Backend.Cpu` as `libggml-metal.dylib`. |
| **Microsoft.ML.OnnxRuntimeGenAI** | **0.15.2** | **Preview** — the docs still say "*this API is in preview and is subject to change*" ([docs](https://onnxruntime.ai/docs/genai/)) | 2026-08-06 | Cannot load GGUF at runtime. Last-token logits only. Wrong tool for this app. |
| **OllamaSharp** | **5.4.30** | GA | 2026-07-24 | Does **not** model `logprobs`/`top_logprobs` — verified against its `ChatRequest`/`ChatResponseStream` source. Server has it; the client doesn't. |
| **Ollama server** | v0.33.2 (2026-08-27) | GA | — | `logprobs` + `top_logprobs` (0–20) landed in **v0.12.11**, confirmed in `api/types.go`. Capped at 20; no prompt logprobs. |
| **llama-server** (llama.cpp `tools/server`) | build **b10687** (2026-08-29) | GA | — | `n_probs` (aliased `logprobs`) returns `completion_probabilities[].top_logprobs`, plus `/tokenize?with_pieces=true`. No prompt-token logprobs. |
| **LM Studio** server | ≥ 0.3.39 | GA | — | Added candidate-token logprobs via `/v1/responses` `top_logprobs`. Adds a desktop-app deployment dependency. |
| **vLLM** | — | GA (Linux/CUDA, Python) | — | The only server that returns **`prompt_logprobs`**. Wrong platform for a self-contained Windows+Linux CLI. |
| **OpenAI .NET SDK** (`OpenAI` 2.13.0) | 2.13.0 | GA | — | `ChatCompletionOptions.IncludeLogProbabilities` / `TopLogProbabilityCount` → `ContentTokenLogProbabilities[].TopLogProbabilities`. This is your client for *any* OpenAI-shaped local server. |
| **Microsoft.Extensions.AI** | 10.9.0 (2026-08-11) | GA | — | `ChatOptions` has **no** logprobs concept. Verified in source. It cannot be the abstraction for this feature. |

---

## Analysis

### 1. LLamaSharp: version, llama.cpp binding, backends, sizes, selection, failure modes

#### Version and what changed since the pinned 0.25.0

The source app pins `LLamaSharp` / `LLamaSharp.Backend.Cpu` / `LLamaSharp.Backend.Cuda12` all at **0.25.0** in `src/Xcaciv.ChatDbg.Core/Xcaciv.ChatDbg.Core.csproj`. Current NuGet stable is **0.27.0**.

| Version | NuGet date | llama.cpp commit | llama.cpp commit date | Highlights |
|---|---|---|---|---|
| 0.24.0 | 2025-05-14 | `ceda28ef` | — | Android, linux-arm64, tensor overrides, `LLamaReranker` |
| **0.25.0** (pinned) | 2025-08-16 | `11dd5a44` | **2025-07-26** | Memory-efficient context handling; **`DefaultSamplingPipeline` unsealed**; stable M.E.AI.Abstractions |
| 0.26.0 | 2026-02-15 | `506bb6e0` | 2026-01-11 | Gemma 3n; MTMD multimodal; Vulkan SDK → 1.4.335.0; Blackwell `sm_120` in the CUDA 12 backend (PR #1338); fixes a crash when grammar optimization + `TopK == 0` |
| **0.27.0** (current) | **2026-04-26** | `3f7c29d3` | **2026-04-16** | **win-arm64 CPU**; Qwen3.5/Gemma4; context-overflow fix; musl detection now by RID not distro name; Linux `RUNPATH`/`$ORIGIN` fixes; removed hardcoded `n_seq_max` |
| 0.29.0 | **not on NuGet** (tag 2026-08-24) | `815a2a59` (b10221) | 2026-08-01 | Seq-ID pooling in `BatchedExecutor` (fixes SeqMax overflow crashes); **removes SemanticKernel, KernelMemory, `LLama.Experimental`**; `noavx` build fix |

Sources: [LLamaSharp README version map](https://github.com/SciSharp/LLamaSharp/blob/v0.27.0/README.md), [releases API](https://github.com/SciSharp/LLamaSharp/releases), commit dates via the llama.cpp GitHub API.

**Concrete upgrade reasons for this app, not generic ones:**
- 0.26.0's `sm_120` addition is the difference between "works on an RTX 50-series dev box" and "CUDA backend silently produces garbage or fails to load."
- 0.26.0 fixes the *Extended grammar optimization crash when TopK is 0* (PR #1282). An introspection tool that wants the *unfiltered* distribution is exactly the caller that sets `TopK = 0`.
- 0.27.0's musl RID detection matters if the self-contained Linux binary is ever run in an Alpine container.
- 0.27.0's `RUNPATH`/`$ORIGIN` work is directly relevant to single-file self-extract on Linux, where the extracted `libllama.so` must find `libggml.so` next to it.

**The lag is real and must be stated.** llama.cpp is at build **b10687** (2026-08-29). NuGet's newest LLamaSharp is bound to a llama.cpp commit from **2026-04-16** — roughly four and a half months and several thousand upstream builds behind. Practically: any GGUF whose architecture landed in llama.cpp after mid-April 2026 will fail to load with `unknown model architecture`, and there is no workaround short of `NativeLibraryConfig.LLama.WithLibrary(path)` pointed at your own build. The FAQ entry for exactly this error was added in 0.27.0 (PR #1371) because it is the single most common user report.

#### Backend package family — real measured sizes

Measured by `Content-Length` on the `.nupkg` from `api.nuget.org/v3-flatcontainer`, 2026-08-28:

| Package | 0.27.0 nupkg size | Notes |
|---|---|---|
| `LLamaSharp` (managed) | **0.35 MiB** | `netstandard2.0` + `net8.0`. `net8.0` asset is what a net10.0 app resolves. |
| `LLamaSharp.Backend.Cpu` | **34.65 MiB** | Contains 10 RID/variant sets — see below |
| `LLamaSharp.Backend.Cuda12` | 0.05 MiB | Meta-package only |
| `LLamaSharp.Backend.Cuda12.Windows` | **213.8 MiB** | |
| `LLamaSharp.Backend.Cuda12.Linux` | **215.1 MiB** | |
| `LLamaSharp.Backend.Vulkan` | 0.05 MiB | Meta-package only |
| `LLamaSharp.Backend.Vulkan.Windows` | **19 MiB** | |
| `LLamaSharp.Backend.Vulkan.Linux` | **19 MiB** | |
| `LLamaSharp.Backend.Cuda11` | last built 0.24.0 | Dropped |
| `LLamaSharp.Backend.OpenCL` | 3 MiB, last built 0.13.0 (2024-06-04) | Abandoned |

`LLamaSharp.Backend.Cpu` 0.27.0 contains (verified by unzipping the package):

```
linux-arm64/native/                     {libllama,libggml,libggml-base,libggml-cpu,libmtmd}.so
linux-x64/native/{avx,avx2,avx512,noavx}/    same 5 .so
linux-musl-x64/native/{avx,avx2,avx512,noavx}/ same 5 .so
osx-arm64/native/                       + libggml-metal.dylib, libggml-blas.dylib   ← Metal lives here
osx-x64/native/  and  osx-x64/native/rosetta2/
win-arm64/native/                       ← new in 0.27.0
win-x64/native/{avx,avx2,avx512,noavx}/
```

There is **no separate Metal backend package** — Metal ships as `libggml-metal.dylib` inside `Backend.Cpu` for `osx-arm64`. The old `LLamaSharp.Backend.MacMetal` stops at 0.7.0 and must not be referenced.

#### How backends get onto disk — the part that surprises people

The backend packages do **not** use NuGet's standard `runtimes/<rid>/native/` asset convention. They ship the binaries under a private `LLamaSharpRuntimes/` folder plus a `build/netstandard2.0/LLamaSharp.Backend.Cpu.props` that copies them as MSBuild `Content` items. The governing condition, verbatim from the props inside the 0.27.0 package:

```xml
<ItemGroup Condition="Exists('$(MSBuildThisFileDirectory)..\..\LLamaSharpRuntimes\$(RuntimeIdentifier)')">
  <Content Include="...\LLamaSharpRuntimes\$(RuntimeIdentifier)\**">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>runtimes\$(RuntimeIdentifier)\%(RecursiveDir)%(Filename)%(Extension)</Link>
  </Content>
</ItemGroup>

<!-- Fallback for when RuntimeIdentifier is not specified -->
<ItemGroup Condition="!Exists('...\LLamaSharpRuntimes\$(RuntimeIdentifier)')">
  <Content Include="...\LLamaSharpRuntimes\**\*.*"> ... </Content>
</ItemGroup>
```

Consequences that matter to a "self-contained binary for Windows and Linux":

- **A plain `dotnet build` with no `RuntimeIdentifier` copies every RID and every AVX variant of every referenced backend into `bin/`.** With the source app's current unconditional `Cpu + Cuda12` reference set, that is roughly **460 MiB of native payload per configuration folder**. This is why the repo's `bin/Debug/net10.0/Xcaciv.ChatDbg.Shell.deps.json` enumerates `linux-arm64`, `linux-musl-x64`, `osx-*`, `win-x64/{avx,avx2,avx512,noavx}` *and* `cuda12` all at once.
- **Always publish with an explicit `-r win-x64` / `-r linux-x64`.** The repo's `build-singlefile.bat` already does `dotnet restore ... -r win-x64` then `dotnet publish ... -r win-x64`; keep that discipline in the rebuild and make the RID-less build fail loudly rather than quietly produce a half-gigabyte folder.
- Because these are `Content`, not NuGet native assets, `PublishSingleFile` needs **`IncludeAllContentForSelfExtract=true`** (not just `IncludeNativeLibrariesForSelfExtract`) to bundle them. The existing `SingleFile` configuration sets both, which is correct.

#### How backends get *selected* at runtime

`NativeApi`'s static constructor calls `NativeLibrary.SetDllImportResolver` and delegates to `DefaultNativeLibrarySelectingPolicy`, which yields candidate relative paths in order (source: `LLama/Native/Load/DefaultNativeLibrarySelectingPolicy.cs`, `NativeLibraryWithCuda.cs`, `SystemInfo.cs` at `v0.27.0`):

1. If `NativeLibraryConfig.LLama.WithLibrary(path)` was set — **that path only, no fallback**.
2. If CUDA is enabled: `runtimes/{os}/native/cuda{major}/libllama.{so|dll}` where `{major}` comes from `SystemInfo.CudaMajorVersion`.
3. If Vulkan is enabled: the Vulkan variant.
4. AVX ladder, descending: `avx512` → `avx2` → `avx` → `noavx` (all four only when `WithAutoFallback(true)`, which is the default).
5. macOS / generic fallback.

Two details make this fail in the field far more often than the docs suggest:

- **CUDA detection never asks the driver.** `SystemInfo.GetCudaMajorVersion()` reads the `CUDA_PATH` environment variable (Windows), then `CUDA_VERSION`, then `/usr/local/cuda`, then directories from `LD_LIBRARY_PATH`, and in each case parses `version.json` for `libcublas.version`. A machine with a perfectly good NVIDIA *driver* and no CUDA *Toolkit* installed returns `-1`, the CUDA candidate is never yielded, and you silently get CPU inference at a tenth of the speed. This is issue [#990 "Not loading cuda backend on laptop"](https://github.com/SciSharp/LLamaSharp/issues/990), still open. The workaround is `NativeLibraryConfig.LLama.WithCuda().SkipCheck()`, which forces the cuda12-then-cuda11 candidates to be tried regardless of detection.
- **Vulkan detection spawns `vulkaninfo` as a child process.** No `vulkaninfo` on `PATH` → no Vulkan, even with a working driver. It also means a process spawn on first model load, which costs startup latency and can trip endpoint-protection heuristics on locked-down Windows machines.
- **Only CUDA 11 and 12 are recognised** (`NativeLibraryWithCuda.GetCudaPath` is called with 12 then 11). CUDA 13 support is [open feature request #1360](https://github.com/SciSharp/LLamaSharp/issues/1360), filed 2026-03-21, unresolved.
- The whole selection policy is inside `#if NET6_0_OR_GREATER`. Irrelevant for a .NET 10 target, but it means the `netstandard2.0` asset has no runtime backend selection at all.

**Known native-load failure modes**, and the exact message users will paste into your issue tracker. LLamaSharp catches `DllNotFoundException` and throws `LLama.Exceptions.RuntimeError` with this text (verbatim from `NativeApi.Load.cs`):

> "The native library cannot be correctly loaded. It could be one of the following reasons: 1. No LLamaSharp backend was installed… 2. You are using a device with only CPU but installed cuda backend… 3. One of the dependency of the native library is missed… 4. Try to compile llama.cpp yourself…"

Observed causes, ranked by how likely they are to bite this app:

| Failure | Cause | Mitigation |
|---|---|---|
| `RuntimeError: Failed to load the native library` on 0.25.0 | Reported as [#1275](https://github.com/SciSharp/LLamaSharp/issues/1275) against exactly the version this app pins | Move to 0.27.0 |
| macOS ARM64 load failure | [#1277](https://github.com/SciSharp/LLamaSharp/issues/1277); addressed by the `@loader_path` / `$ORIGIN` RPATH work in 0.26.0/0.27.0 | 0.27.0 |
| `libllama.so` loads but `libggml.so` doesn't resolve | Sibling-library resolution after single-file self-extract | 0.27.0 RPATH fixes; verify extract dir is on the loader path |
| `noavx` build crashes with SIGILL | `GGML_F16C`/`GGML_BMI2` were wrongly enabled in the noavx build — [#1407](https://github.com/SciSharp/LLamaSharp/issues/1407), fixed only in **0.29.0** (not yet on NuGet) | On truly ancient CPUs, 0.27.0 `noavx` is still suspect. **UNCONFIRMED** whether 0.27.0 is affected. |
| Silent CPU fallback on a CUDA box | `CUDA_PATH` absent (see above) | `SkipCheck()` + surface `DryRun` result to the user |
| `unknown model architecture` | GGUF newer than the pinned llama.cpp | Show the bound llama.cpp commit in `/inspect`; allow `WithLibrary()` override |

`NativeLibraryConfig.LLama.WithLogCallback(ILogger).DryRun(out var lib)` returns the library that *would* load without committing to it. For a debugging shell this belongs in a `/backend` or `/diag` command — it is the single highest-value diagnostic you can expose, and it is free.

---

### 2. LLamaSharp's APIs by level — and where the logits actually live

There are three tiers, and the app currently uses the wrong one for its distinguishing feature.

| Tier | Types | What you get | Introspection ceiling |
|---|---|---|---|
| **High** | `ChatSession`, `InteractiveExecutor`, `InstructExecutor`, `StatelessExecutor`, `LlamaExecutorChatClient` (`IChatClient`) | `IAsyncEnumerable<string>` of decoded text; anti-prompts; chat templating | **Text only.** No logits, no token IDs, no probabilities. |
| **Mid** | `LLamaContext`, `LLamaWeights`, `StreamingTokenDecoder`, `ISamplingPipeline` | `Tokenize`/`DeTokenize`; you supply the sampling pipeline | Token IDs and a pluggable sampler, but no direct distribution readout |
| **Low** | `BatchedExecutor` + `Conversation`, `SafeLLamaContextHandle`, `SafeLLamaSamplerChainHandle`, `LLamaTokenDataArray`, `SafeLlamaModelHandle.Vocabulary` | **Raw `Span<float>` logits**, per-position, full vocab | Everything this app needs |

#### Obtaining raw logits and the full distribution

`LLamaContext` itself does **not** expose `GetLogits` — this is the trap the source app fell into. The methods are on `SafeLLamaContextHandle` (`LLama/Native/SafeLLamaContextHandle.cs`, 0.27.0):

```csharp
public Span<float> GetLogits(int numTokens = 1);   // [numTokens × n_vocab], last row = last token
public Span<float> GetLogitsIth(int i);            // n_vocab floats for output slot i
```

The ergonomic route is `BatchedExecutor` / `Conversation` (`LLama/Batched/Conversation.cs`):

```csharp
public int GetSampleIndex(int offset = 0);
public Span<float> Sample(int offset = 0);         // → Executor.Context.NativeHandle.GetLogitsIth(index)
public void Prompt(ReadOnlySpan<LLamaToken> tokens, bool allLogits = false);
```

`Prompt(tokens, allLogits: true)` is the load-bearing one. It tells llama.cpp to emit logits for **every** prompt token, not just the last, and then `Sample(offset)` walks backwards through them — the XML doc spells out that for a 5-token prompt, offset 4 is the first token's logits and offset 0 the fifth's. **This is the only mechanism in the entire .NET ecosystem that gives you per-prompt-token distributions**, and it is precisely what "token attribution back to input spans" requires.

Turning logits into a probability distribution is `LLamaTokenDataArray` (`LLama/Native/LLamaTokenDataArray.cs`). The canonical pattern, lifted verbatim from LLamaSharp's own `BatchedExecutorBeamSearch.cs` example:

```csharp
var logitsArr = LLamaTokenDataArray.Create(conversation.Sample());
logitsArr.Softmax();                       // sorts descending AND fills Probability
// after Softmax the array is in descending probability order
for (var i = 0; i < topK; i++) {
    var item = logitsArr.Data.Span[i];     // item.ID, item.Logit, item.Probability
}
```

`Softmax()` sorts the whole `n_vocab` array descending by logit and computes the softmax with `System.Numerics.Tensors.TensorPrimitives.SoftMax` — SIMD, over the full vocabulary. That last part is the correctness point: probabilities are normalised over **all** ~128k–256k tokens.

Token text comes from `SafeLlamaModelHandle.Vocabulary`:

```csharp
public string? LLamaTokenToString(LLamaToken? token, bool isSpecialToken);
public int Count { get; }                  // n_vocab — the size of the probability map
public LLamaToken? BOS, EOS, EOT, Newline, Pad, Mask, SEP, ...
```

Plus `LLamaTokenAttr` for classifying a token as control/special/byte/unknown — useful for rendering the tokenization grid honestly.

**What the source app does today, and why it is wrong.** `src/Xcaciv.ChatDbg.Core/Services/LLamaSharpService.cs`, `ComputeTopKFromCurrentLogits`:

```csharp
var mi = context.GetType().GetMethod("GetLogits", Type.EmptyTypes);
if (mi is null) return new List<TokenLogProbability>();
var raw = mi.Invoke(context, null);
...
var indices = Enumerable.Range(0, logits.Length).OrderByDescending(i => logits[i]).Take(count).ToArray();
var maxLogit = indices.Select(i => logits[i]).Max();
var exps = indices.Select(i => Math.Exp(logits[i] - maxLogit)).ToArray();
var sumExp = exps.Sum();
```

and, in `ResolveTokenStringSafe`, `var dyn = (dynamic)context; string s = dyn.TokenToString(tokenId);`

Four separate defects, all fixed by moving to the low-level API:

1. **The reflection probe always returns empty.** `LLamaContext` has no public parameterless `GetLogits`; it is on `context.NativeHandle`. `mi` is `null`, the method returns an empty list, and the whole top-K feature is silently dead. The `catch { }` hides it.
2. **The softmax denominator is wrong.** Even if the reflection worked, it exponentiates only the top-K logits and divides by their sum. That yields *conditional-within-top-K* probabilities that always total 1.0 — so the displayed "probability" of the top token is systematically inflated and the numbers are not comparable across steps or against a hosted provider's logprobs. The denominator must be over the full vocabulary; `LLamaTokenDataArray.Softmax()` does exactly this.
3. **`OrderByDescending` over the full vocab, per generated token**, allocates an index array and does an O(V log V) LINQ sort at every step. For a 256k vocab and a 500-token response that is 128 million comparisons plus boxing. `Softmax()`'s span sort is the floor; a partial selection (bounded min-heap of size K) is better still if you only need top-K.
4. **`dynamic` and `GetMethod` are trim- and AOT-hostile.** The repo's `Compact` configuration sets `PublishAot=true; PublishTrimmed=true; TrimMode=full; SuppressTrimAnalysisWarnings=true`. `dynamic` drags in the entire DLR; reflection over a trimmed type will not find the method. `SuppressTrimAnalysisWarnings` is exactly the setting that turns this into a silent runtime failure instead of a build error.

#### Explicit tokenize / detokenize

```csharp
// LLamaContext
public LLamaToken[] Tokenize(string text, bool addBos = true, bool special = false);
public string DeTokenize(IReadOnlyList<LLamaToken> tokens);

// SafeLlamaModelHandle
public LLamaToken[] Tokenize(string text, bool addBos, bool special, Encoding encoding);
public uint TokenToSpan(LLamaToken token, Span<byte> dest, int lstrip = 0, bool special = false);
```

`StreamingTokenDecoder` handles the multi-byte-UTF-8-split-across-tokens problem for incremental display; use it for streaming output and use `TokenToSpan` for the static tokenization grid so you can show raw bytes when a token is not valid standalone UTF-8. Note the model handle's `Tokenize` does **not** give character offsets — for the "token attribution back to input spans" view you must reconstruct offsets by detokenizing prefixes, or by accumulating `TokenToSpan` byte lengths and mapping back through the original UTF-8 byte array. That is the honest cost; nothing in llama.cpp hands you offsets.

#### The sampling-pipeline API as it stands in 0.27.0

```csharp
public interface ISamplingPipeline : IDisposable
{
    LLamaToken Sample(SafeLLamaContextHandle ctx, int index);
    void Apply(SafeLLamaContextHandle ctx, LLamaTokenDataArray data);
    void Reset();
    void Accept(LLamaToken token);
}

public abstract class BaseSamplingPipeline : ISamplingPipeline
{
    protected abstract SafeLLamaSamplerChainHandle CreateChain(SafeLLamaContextHandle context);
    public virtual void Apply(SafeLLamaContextHandle ctx, ref LLamaTokenDataArrayNative data);  // zero-copy overload
}
```

`DefaultSamplingPipeline` was **unsealed in 0.25.0** (PR #1208), so you can subclass it. Its `CreateChain` builds, in this exact order: logit bias → penalties → `AddTopK` → `AddTypical` → `AddTopP` → `AddMinP` → `AddTemperature` → `AddDistributionSampler(seed)`. Defaults: `Temperature 0.75`, `TopK 40`, `TopP 0.9`, `MinP 0.1`, `TypicalP 1`, `RepeatPenalty 1`, `PenaltyCount 64`, `MinKeep 1`, `GrammarOptimization = Extended`.

The full chain-stage vocabulary on `SafeLLamaSamplerChainHandle` (0.27.0):

`AddGreedySampler`, `AddDistributionSampler(seed)`, `AddTopK`, `AddTopNSigma`, `AddTopP`, `AddMinP`, `AddTypical`, `AddTemperature`, `AddDynamicTemperature`, `AddXTC`, `AddAdaptiveP`, `AddPenalties`, `AddDry`, `AddLogitBias`, `AddGrammar`, `AddLazyGrammar`, `AddFillInMiddleInfill`, `AddClone`, and — the interesting one — **`AddCustom<TSampler>(TSampler sampler) where TSampler : class, ICustomSampler`**.

There is **no `AddSoftmax`**; llama.cpp removed `llama_sampler_init_softmax`. Normalisation is your job, via `LLamaTokenDataArray.Softmax()`.

`ICustomSampler` is the hook that makes an *observing* pipeline possible without changing sampling behaviour:

```csharp
public interface ICustomSampler : IDisposable
{
    string Name { get; }
    void Apply(ref LLamaTokenDataArrayNative tokenData);  // may modify logits or select a token
    void Accept(LLamaToken token);
    void Reset();
    ICustomSampler Clone();
}
```

Insert a no-op `ICustomSampler` at a chosen point in the chain and it sees the *live* candidate array at that stage — before penalties, after top-K, whatever you place it next to. That is how you build a "what did each sampler stage actually do to this token's chances" view, which is a genuinely differentiated debugging feature no hosted API can offer. The contract has one sharp edge, called out in its own XML doc: **if you modify logits you must set `tokenData.Sorted = false`**, or downstream stages will read a stale sort order and you get silently wrong sampling.

Chain-level introspection also exists: `GetName(int index)` returns the human-readable name of stage *i*, so a `/sampler` command can print the live chain.

---

### 3. Alternatives, and the exact condition under which each wins

#### Microsoft.ML.OnnxRuntimeGenAI 0.15.2 — **preview, and wrong for this app**

- **Status: preview.** The landing page still carries "*Note: this API is in preview and is subject to change*." Versions are 0.x; 0.14.0 → 0.15.0 → 0.15.2 shipped between 2026-05-26 and 2026-08-06, i.e. it is actively developed but still churning.
- Package: `Microsoft.ML.OnnxRuntimeGenAI` 0.15.2 (**104 MiB** native) → depends on `Microsoft.ML.OnnxRuntimeGenAI.Managed` + `Microsoft.ML.OnnxRuntime` 1.28.0. CUDA and DirectML variants exist; **`.DirectML` is stuck at 0.14.1 (2026-06-02)** while the base is at 0.15.2 — a version-skew hazard.
- **It cannot load a GGUF file.** The supported flow is ONNX. GGUF is only an *offline conversion input* to the Python `onnxruntime_genai.models.builder` tool (`python -m onnxruntime_genai.models.builder -m model_name -i path_to_gguf_file -o out -p precision -e ep`). A CLI whose stated feature is "point it at a `.gguf`" would need to ship Python. That alone disqualifies it.
- **Logits: last token only, and not bound in C#.** The C API has `OgaGenerator_GetLogits`, documented as "*it only contains the last token logits even in prompt processing*." Grepping `src/csharp/NativeMethods.cs` at `v0.15.2` for "logit" returns **nothing** — it is not P/Invoked. The C# `Generator` class exposes `GetInput(name)` / `GetOutput(name)` returning an `OgaTensor`, so `GetOutput("logits")` is the only route, and only if the exported graph names that output. `ComputeLogits()` shown in the [C# API docs](https://onnxruntime.ai/docs/genai/api/csharp.html) **no longer exists** in `src/csharp/Generator.cs` on `main` — that documentation page is stale.
- Tokenizer: `Encode`/`Decode`/`EncodeBatch`/`DecodeBatch`/`CreateStream`/`ApplyChatTemplate`. **No character offsets.**
- Model architectures supported: AMD OLMo, ChatGLM, DeepSeek, ERNIE 4.5, Fara, Gemma, gpt-oss, Granite, HunYuan Dense V1, InternLM2, Llama, Mistral, Nemotron, Phi (language + vision), Qwen (language + vision), SmolLM3, Whisper.
- **When it wins:** if the app pivots to shipping *one specific pre-converted* model (a Phi-4-mini ONNX INT4, say) and needs first-class DirectML/QNN/NPU acceleration on Windows-on-ARM. Then ONNX Runtime's execution-provider story beats llama.cpp's. It does not win here, because "load a GGUF the user picked" is a stated requirement.

#### OllamaSharp 5.4.30 + Ollama server — **server can, client can't**

- **The change you were told about is real and I confirmed it in source.** `ollama/api/types.go` on `main` defines, on both the generate and chat request types:
  ```go
  Logprobs bool `json:"logprobs,omitempty"`
  // TopLogprobs is the number of most likely tokens to return at each token position...
  // Valid values are 0-20. Default is 0 (only return the selected token's logprob).
  TopLogprobs int `json:"top_logprobs,omitempty"`
  ```
  with response types `Logprob { Token, Logprob, Bytes, TopLogprobs []TokenLogprob }`. Shipped in **v0.12.11**. Latest Ollama is **v0.33.2** (2026-08-27).
- **OllamaSharp does not surface it.** I read `src/OllamaSharp/Models/Chat/ChatRequest.cs`, `RequestOptions.cs`, `ChatResponseStream.cs` and `ChatDoneResponseStream.cs` on `main` (5.4.30, published 2026-07-24): grep for "logprob" returns zero hits. `RequestOptions` models `MiroStat`, `NumCtx`, `NumGpu`, `TopK`, `MinP`, `TypicalP`, `TfsZ`… but no logprobs. So with OllamaSharp you would be hand-rolling the HTTP call or subclassing the request/response types anyway — at which point the client library is buying you very little.
- Hard ceiling: **`top_logprobs` max 20**, generated tokens only, no prompt logprobs, no raw logits, no custom sampling chain.
- Note that Ollama's `/api/docs` markdown does **not** document logprobs even though the Go types support it — the feature is real but undocumented in `docs/api.md` as of today.
- **When it wins:** if the user already runs Ollama and you want zero packaging cost and zero native-crash exposure, and top-20 alternatives per generated token is judged good enough. It is not good enough for probability maps over the vocabulary or input attribution.

#### llama-server subprocess over HTTP — **the credible second place**

This is the strongest alternative and deserves detail. From `tools/server/README.md` on `master` and the server sources:

- `POST /completion` accepts **`n_probs`** — "*If greater than 0, the response also contains the probabilities of top N tokens for each generated token given the sampling settings*" — and returns `completion_probabilities[]`, each item carrying `id`, `token`, `bytes`, `logprob` and a nested `top_logprobs[]` array of up to `n_probs` entries.
- **`post_sampling_probs`** toggles between *pre-sampling* (raw softmax of the logits, ignoring sampler settings) and *post-sampling* (after the whole sampler chain) probabilities. Having both is genuinely useful for a debugging tool: it shows what the model thought versus what your samplers left standing.
- In `server-schema.cpp`, `n_probs` carries `->add_alias("logprobs")`, and in `server-common.cpp` the OpenAI-compatible path maps `logprobs: true` + `top_logprobs: N` onto `n_probs`, defaulting to 20. I found **no hard cap** on `n_probs` in the native endpoint — it is clamped only by `max_probs`, the candidate array size. So `/completion` with a large `n_probs` gets you much closer to a full probability map than Ollama's hard 20.
- `POST /tokenize` supports **`with_pieces: true`**, returning `[{"id":123,"piece":"Hello"}, ...]`, with `piece` as a byte list when it isn't valid UTF-8. `POST /detokenize` and `POST /apply-template` round it out. That covers the tokenization-analysis view cleanly over HTTP.
- Other useful endpoints: `/props`, `/slots`, `/metrics` (Prometheus), `/slots/{id}?action=save|restore|erase` for prompt-cache management, `/models/load` and `/models/unload` for swapping GGUFs without restarting.
- **What it cannot do:** no prompt-token logprobs (no `echo` equivalent); no raw logits; no custom sampler stage inserted mid-chain; no `Prompt(allLogits: true)`. It also introduces a serialization hop — a 256k-entry probability map as JSON is ~5–10 MB per token, which is untenable, so full probability maps are effectively off the table over HTTP.
- Client: use the **OpenAI .NET SDK 2.13.0** against `/v1/chat/completions` (`ChatCompletionOptions.IncludeLogProbabilities = true; TopLogProbabilityCount = n`, read `ContentTokenLogProbabilities[].TopLogProbabilities`) for the chat path, and a plain `HttpClient` + `System.Text.Json` for `/completion` when you want `n_probs` beyond 20 or `post_sampling_probs`. There is **no well-maintained .NET package that supervises a llama-server subprocess** — a NuGet search for "llama server" surfaces only low-download one-offs (`LMSupply.Llama` 0.42.5 at ~17k downloads, `Dmon.Providers.LlamaCpp` 0.2.0 at 120). You would write the process supervisor yourself: ~200 lines for spawn, port selection, `/health` polling, stderr capture, graceful shutdown and orphan reaping.
- **When it wins:** when process isolation is worth more than depth of introspection — a debugging shell that must never lose the user's conversation to a native crash, or a deployment where a shared server serves several clients. Also when you need bleeding-edge llama.cpp (b10687 today vs. LLamaSharp's April binding).

#### LM Studio / vLLM

- **LM Studio** added logprobs for candidate tokens; per its docs the `top_logprobs` parameter is available on `/v1/responses` from **0.3.39** onward. It is a GUI desktop application the user must install, run, and load a model into. For a self-contained CLI that is a deployment dependency you do not control. Useful as an *optional* endpoint (it is OpenAI-shaped, so the same client code as llama-server) but not as the shipped default.
- **vLLM** is the only server that returns **`prompt_logprobs`** — logprobs for every prompt token, the exact primitive that input attribution wants. It has also supported `echo=True` for prompt logprobs on the OpenAI endpoint. But it is Python, effectively Linux + NVIDIA, and heavyweight. For a cross-platform Windows/Linux self-contained binary it is a non-starter. **This is the strongest argument for LLamaSharp**: `Conversation.Prompt(tokens, allLogits: true)` gives you vLLM's `prompt_logprobs` capability in-process, on Windows, on CPU, with no Python.

#### Capability matrix

| | LLamaSharp low-level | llama-server HTTP | Ollama | LM Studio | ORT GenAI |
|---|---|---|---|---|---|
| Loads a `.gguf` the user picks | ✅ | ✅ | via `ollama create` | ✅ | ❌ (offline convert) |
| Raw `float[]` logits, full vocab | ✅ `Span<float>` | ❌ | ❌ | ❌ | last token only, unbound in C# |
| Per-token top-K logprobs (generated) | ✅ unbounded | ✅ `n_probs`, no hard cap | ✅ max 20 | ✅ | manual from tensor |
| Full probability map over vocabulary | ✅ | ✗ practical (JSON size) | ❌ | ❌ | manual |
| **Prompt-token logits (input attribution)** | ✅ `allLogits: true` | ❌ | ❌ | ❌ | ❌ |
| Pre- vs post-sampling probabilities | ✅ via `ICustomSampler` placement | ✅ `post_sampling_probs` | ❌ | ❌ | ❌ |
| Custom sampler stage | ✅ `ICustomSampler` | sampler *order* only | ❌ | ❌ | custom scoring |
| Explicit tokenize/detokenize | ✅ | ✅ `with_pieces` | ✅ `/api/embed`-adjacent | ✅ | ✅ no offsets |
| Character offsets for spans | ✗ reconstruct | ✗ reconstruct | ✗ | ✗ | ✗ |
| Crash isolated from host | ❌ | ✅ | ✅ | ✅ | ❌ |
| Extra runtime deps for the user | none | bundled binary | Ollama install | LM Studio install | none |

---

### 4. The in-process-native-library tradeoff

**Packaging.** Covered above: 34.65 MiB CPU baseline; +19 MiB per platform for Vulkan; +214 MiB per platform for CUDA 12. The non-standard `LLamaSharpRuntimes/` + props layout means the RID must be explicit or you copy everything. A realistic self-contained `win-x64` CPU+Vulkan publish is on the order of **70–90 MiB** before compression; adding CUDA takes it past 280 MiB. **UNCONFIRMED**: exact published-folder sizes — I did not run a publish.

**AOT.** `LLamaSharp.csproj` at 0.27.0 declares `netstandard2.0;net8.0`, `LangVersion 13`, `AllowUnsafeBlocks`, and **does not set `IsAotCompatible` or `IsTrimmable`**. There are no AOT/trim-tagged issues in the repo (my title searches for "AOT", "single file" and "trim" returned nothing). Separately, `PublishAot=true` **silently ignores `IncludeNativeLibrariesForSelfExtract=true`** ([dotnet/sdk#49995](https://github.com/dotnet/sdk/issues/49995)) and the SDK gets into a broken state when `PublishAot` and `PublishSingleFile` are both set. The repo's `Compact` configuration sets `PublishAot=true; PublishTrimmed=true; TrimMode=full`. **Conclusion: do not plan on Native AOT for the shell process while LLamaSharp is in-process.** The library is not annotated, the backend natives are `Content` items AOT won't bundle, and the current code's `dynamic`/reflection would break even if they were. `PublishSingleFile` + `SelfContained` + `IncludeAllContentForSelfExtract` (the repo's existing `SingleFile` configuration) is the supported shape.

**Trimming.** `TrimMode=full` with `SuppressTrimAnalysisWarnings=true` over an unannotated library is how you ship a binary that builds clean and fails at runtime. If you trim at all, use `TrimMode=partial` (as the repo's `SingleFile` config already does), and add `<TrimmerRootAssembly Include="LLamaSharp" />` so nothing in it is removed. Better: don't trim the LLamaSharp path.

**Crash blast radius.** This is the strongest argument against in-process, and it should be weighed seriously for *this* app specifically. An access violation or `abort()` inside `ggml`/`llama.cpp` — CUDA OOM, a malformed GGUF, a KV-cache overflow, a bad quantization — terminates the .NET process immediately. `try/catch` does not help; SEH/`SIGSEGV` from native code is not a CLR exception. The user loses the conversation history, the loaded system prompt, and any in-flight work. Historical evidence that this is not theoretical: [#860 "App crashes with CUDA error in ggml-cuda.cu:1503"](https://github.com/SciSharp/LLamaSharp/issues/860), [#1231 "Cuda graph errors"](https://github.com/SciSharp/LLamaSharp/issues/1231), [#1091 "CUDA errors with two GPUs"](https://github.com/SciSharp/LLamaSharp/issues/1091), and the `BatchedExecutor` SeqMax-overflow crash fixed only in 0.29.0 ([#1386](https://github.com/SciSharp/LLamaSharp/pull/1386)) — which matters because `BatchedExecutor` is exactly what I am recommending you use. **Mitigation that costs nothing: persist conversation history to disk on every turn, before inference, not after.**

**AssemblyLoadContext — a hazard specific to this rebuild.** `Xcaciv.Loader`'s own README shows the intended usage as a *collectible, unloadable* context:

```csharp
using (var context = new AssemblyContext(dllPath, basePathRestriction: AppDomain.CurrentDomain.BaseDirectory))
{ ... } // Unload
```

If the local-inference provider is packaged as a loadable plugin, this breaks in two ways. First, `NativeApi`'s static constructor calls `NativeLibrary.SetDllImportResolver` on its own assembly and loads `libllama` — **a native library loaded from a collectible ALC pins that ALC and prevents unload**, so `/unload` will appear to work while the 4 GB of model weights stay resident. Second, if LLamaSharp is resolved *inside* the plugin's ALC rather than the default one, and two plugin loads occur, you get two `LLamaSharp` assembly identities each trying to register a resolver and load `llama.dll` into the same process — undefined at best. **Recommendation: load `LLamaSharp` into the default `AssemblyLoadContext` (make it a direct reference of the host, not of a plugin), and expose it to `Xcaciv.Command` tools through a shared abstraction assembly.** The commands (`/tokenize`, `/inspect`, `/showtokenanalysis`) can live in plugins; the LLamaSharp handle must not.

**Startup time.** Native library resolution is lazy (first P/Invoke), so process start is unaffected — but the *first* model call pays for: CUDA detection (env var reads + a `version.json` parse), Vulkan detection (**a `vulkaninfo` child process spawn** — tens to hundreds of milliseconds, and it can hang on a broken driver), the AVX ladder walk, `dlopen`/`LoadLibrary`, then GGUF `mmap` and, if `GpuLayerCount > 0`, VRAM upload. For a multi-GB model on a cold page cache this is seconds to tens of seconds. **Design consequence: model load must be an explicit `/load` command with a progress indicator, never lazy-on-first-message.** The source app already runs `LLamaWeights.LoadFromFile` inside `Task.Run` — keep that, and prefer `LLamaWeights.LoadFromFileAsync` which the 0.27.0 examples use.

**Out-of-process, honestly costed.** A supervised `llama-server` isolates every one of those crashes and lets you restart transparently. What you pay: a process supervisor you write and maintain; port allocation and collision handling; `/health` polling before first request; stderr plumbing so native errors reach the user; orphan cleanup when the shell is killed; a second binary to sign and ship (llama.cpp's own release artifacts, not a NuGet package, so you own the update cadence); a localhost socket that a corporate firewall or endpoint agent may block; and — the real loss — no raw logits, no prompt-token logits, no custom sampler stage.

---

### 5. GPU acceleration configuration surface

All defaults verified against `LLama/Common/ModelParams.cs` and `LLama/Extensions/IContextParamsExtensions.cs` at `v0.27.0`.

| Knob | Type | LLamaSharp default | Meaning and the portability trap |
|---|---|---|---|
| `GpuLayerCount` | `int` on `IModelParams` | **20** | `n_gpu_layers`. **This default is a trap.** It is neither 0 (pure CPU) nor -1/999 (offload everything). On a CPU-only backend it is inert; on a small GPU it can OOM; on a big GPU it leaves most of the model on the CPU and the user concludes "GPU acceleration doesn't work." **Set it explicitly, per machine.** |
| `MainGpu` | `int` | 0 | Which device holds small tensors / is the single device under `SplitMode.None`. |
| `SplitMode` | `GPUSplitMode?` | null (llama.cpp default) | `None` / `Layer` / `Row` across multiple GPUs. |
| `TensorSplits` | `TensorSplitsCollection` | empty | Per-GPU proportions. |
| `TensorBufferOverrides` | `List<TensorBufferOverride>` | empty | Equivalent to llama.cpp `--override-tensor` / `-ot`. Lets you keep, e.g., MoE expert tensors on CPU while attention goes to GPU. Added in 0.24.0. |
| `ContextSize` | `uint?` on `IContextParams` | **null → `n_ctx = 0`** | 0 means "use the model's trained context length." Fine, but the app should *display* the resolved value — otherwise "why did my long conversation get truncated" is unanswerable. |
| `BatchSize` (`n_batch`) | `uint` | **512** | Logical batch: max tokens submitted to `llama_decode` at once. Governs prompt-processing throughput. |
| `UBatchSize` (`n_ubatch`) | `uint` | **512** | Physical micro-batch actually computed. Bigger = more VRAM, more parallelism. |
| `SeqMax` | `uint` | **1** | Max concurrent sequences. **`BatchedExecutor` with forked conversations needs this > 1.** 0.27.0 removed a hardcoded override (PR #1354); 0.29.0 adds seq-ID pooling to stop overflow crashes. If you fork conversations for beam-style analysis, raise this. |
| `Threads` (`n_threads`) | `int?` | **`max(Environment.ProcessorCount / 2, 1)`** | Generation threads. Halving is a reasonable hyperthreading heuristic but wrong on heterogeneous cores (Intel P/E-core, Apple silicon). Make it user-settable. |
| `BatchThreads` (`n_threads_batch`) | `int?` | same formula | Prompt-processing threads. Usually wants to be higher than `Threads`. |
| `TypeK` / `TypeV` | `GGMLType?` | **`GGML_TYPE_F16`** | KV-cache quantization. Dropping to Q8_0 roughly halves KV memory. Relevant if the app keeps long conversations in context. |
| `FlashAttention` | `bool?` | **null → `AUTO`** | 0.27.0 maps null to `LLAMA_FLASH_ATTENTION_TYPE_AUTO`, true/false to ENABLED/DISABLED. Auto is right; expose the override for debugging. |
| `NoKqvOffload` | `bool` | false (i.e. KQV **is** offloaded) | |
| `OpOffload`, `KVUnified`, `SwaFull` | `bool?` | null; `kv_unified` is forced true before the optional override | Newer llama.cpp knobs. |
| `DefragThreshold` | `float?` | null → **-1** (disabled) | KV-cache defrag. |
| `UseMemorymap` / `UseDirectIO` / `UseMemoryLock` | `bool` | mmap true | `UseDirectIO` takes precedence over mmap where supported. mmap keeps first-load fast; mlock keeps the model from being paged out. |
| `RopeFrequencyBase/Scale`, `Yarn*` | | null → llama.cpp defaults (`yarn_ext_factor -1`, `attn_factor 1`, `beta_fast 32`, `beta_slow 1`) | Context extension. |

**Portability**: every one of these is a llama.cpp concept and maps 1:1 onto `llama-server` command-line flags (`-ngl`, `-c`, `-b`, `-ub`, `-t`, `-tb`, `--split-mode`, `-ts`, `-ot`, `--cache-type-k/v`, `-fa`). That is a real design asset: a single settings record can drive either the in-process path or the subprocess path with a mechanical translation, which makes the fallback in §6 cheap to build.

---

### 6. Recommendation, with the fallback for machines lacking a native backend

**Primary: LLamaSharp 0.27.0, low-level `BatchedExecutor` path, in the host's default `AssemblyLoadContext`.**

Package references, RID-gated:

```xml
<!-- always -->
<PackageReference Include="LLamaSharp" Version="0.27.0" />
<PackageReference Include="LLamaSharp.Backend.Cpu" Version="0.27.0" />

<!-- opt-in via /p:GpuBackend=vulkan|cuda12 ; never both, never unconditional -->
<ItemGroup Condition="'$(GpuBackend)' == 'vulkan'">
  <PackageReference Include="LLamaSharp.Backend.Vulkan" Version="0.27.0" />
</ItemGroup>
<ItemGroup Condition="'$(GpuBackend)' == 'cuda12'">
  <PackageReference Include="LLamaSharp.Backend.Cuda12" Version="0.27.0" />
</ItemGroup>
```

and publish with an explicit RID, always. Make the RID-less publish an MSBuild error.

Runtime startup, before the first model load:

```csharp
NativeLibraryConfig.All
    .WithLogCallback(logger)
    .WithAutoFallback(true);          // AVX ladder → noavx
NativeLibraryConfig.LLama
    .WithCuda(settings.PreferCuda)    // and .SkipCheck(true) when the user asserts a CUDA box
    .WithVulkan(settings.PreferVulkan);

if (!NativeLibraryConfig.All.DryRun(out var llama, out var mtmd))
    // report precisely which candidates were tried, then fall back to §"fallback ladder"
```

**Fallback ladder for a machine with no working native backend** — four rungs, each strictly weaker and each cheap to implement because the settings model is shared:

1. **CPU `noavx`.** `WithAutoFallback(true)` already walks avx512 → avx2 → avx → noavx. This covers essentially every x64 and arm64 machine. It is slow but it works, and 0.27.0 added win-arm64. Surface a warning when the selected variant is below the CPU's actual capability, since that usually means a copy/publish problem rather than a hardware limit.
2. **User-supplied native library.** `NativeLibraryConfig.LLama.WithLibrary(path)` accepts a `libllama` the user built or downloaded. Expose it as a setting. This is also the escape hatch for a GGUF newer than the pinned llama.cpp — the single most common real-world failure.
3. **Out-of-process `llama-server`.** Same GGUF, same settings translated to CLI flags, `n_probs` for top-K and `post_sampling_probs` for the pre/post comparison, `/tokenize?with_pieces=true` for the tokenization grid. **Degrade the UI explicitly**: grey out the full-probability-map and input-attribution views with a tooltip saying they require the in-process backend, rather than showing an empty grid. Client = `HttpClient` for `/completion`, or `OpenAI` 2.13.0 for `/v1/chat/completions`.
4. **Ollama or LM Studio, if already running.** Detect on `localhost:11434` / `localhost:1234`. Top-20 alternatives per generated token only. This is a convenience rung, not a supported configuration.

**Second-best overall, and when it wins.** If the team decides that a shell used for *debugging* must never die mid-session, invert the ladder: make **`llama-server` subprocess the default** and LLamaSharp the opt-in "deep introspection mode." That wins when (a) the native crash log in the first month of dogfooding is worse than expected, (b) the app needs a llama.cpp newer than LLamaSharp's binding often enough to matter, or (c) you want one shared model instance across multiple shell windows. **What you give up by choosing LLamaSharp instead:** process isolation, a four-month-fresher llama.cpp, and the ability to update the inference engine without shipping a new .NET build.

**What you give up by choosing LLamaSharp at all**, stated plainly: Native AOT is off the table for the shell; the publish is 70–300 MiB depending on backend; every native crash is your crash; you are pinned to a llama.cpp from April 2026 until SciSharp ships again; the API docs at `scisharp.github.io/LLamaSharp/latest/` currently redirect to **0.25.0**, so you will be reading source rather than docs; and `LLamaSharp.SemanticKernel` / `LLamaSharp.kernel-memory` are being deleted in 0.29.0, so don't build on them.

---

## What this application specifically needs

Tying each recommendation to an operation the app actually performs, based on the commands present in `src/Xcaciv.ChatDbg.Core/Commands/` (`TokenizeCommand`, `InspectCommand`, `ShowTokenAnalysisCommand`, `ExportTokenAnalysisCommand`) and `Services/TokenInspectionService.cs`.

| App operation | Required primitive | Where it comes from | Status in the source app |
|---|---|---|---|
| **`/tokenize <text>`** — token IDs, offsets, statistics | `Tokenize` + per-token piece text + byte lengths | `LLamaContext.Tokenize` + `Vocabulary.LLamaTokenToString` + `TokenToSpan` for raw bytes | Works today (`context.Tokenize(text, true)`), but shows no offsets. Reconstruct offsets from accumulated `TokenToSpan` byte counts against the original UTF-8 buffer. |
| **Per-token logprob with top-K alternatives** | Full-vocab logits at each generated position, softmaxed over the whole vocab | `Conversation.Sample()` → `LLamaTokenDataArray.Create(...).Softmax()` → take first K | **Broken.** `ComputeTopKFromCurrentLogits` reflects for a non-existent `LLamaContext.GetLogits()`, returns empty, and its softmax denominator is over the top-K subset only. Must be rewritten. |
| **Probability map over the vocabulary** | The complete `n_vocab` distribution, plus token text for the interesting slice | `Softmax()` fills `Probability` for all `Vocabulary.Count` entries; `LLamaTokenToString` names them | Only achievable in-process. HTTP transport of a 256k-entry map is impractical, which is the decisive argument against the server option. |
| **Token attribution back to input spans** | Logits at each **prompt** position, not just generated ones | `Conversation.Prompt(tokens, allLogits: true)` then `Sample(offset)` walking backwards | **No HTTP-based option can do this.** Not llama-server, not Ollama, not LM Studio, not ORT GenAI. Only vLLM (`prompt_logprobs`) and LLamaSharp's low-level path. This single requirement is what makes LLamaSharp the answer. |
| **"What did each sampler stage do"** (a natural next feature for a debugging shell) | Observation of the candidate array between chain stages | A no-op `ICustomSampler` inserted at a chosen index; `chain.GetName(i)` to label stages | Not present today; cheap to add once on the low-level path. Note the `Sorted = false` contract. |
| **Deterministic replay for comparison** | Fixed seed through the whole chain | `DefaultSamplingPipeline.Seed` → `AddDistributionSampler(seed)` | Present. Also set `cache_prompt`-equivalent expectations: llama.cpp warns that logits are not bit-identical across different batch sizes, so exact reproducibility across a prompt-cache hit is not guaranteed. |
| **Heat-map / grid rendering** | `(tokenId, piece, logit, probability)` tuples per position | `LLamaTokenData { ID, Logit, Probability }` from the sorted array | Data shape already matches what a Spectre.Console grid wants. |
| **Self-contained Windows + Linux binary** | Native payload bundled and discoverable after self-extract | `PublishSingleFile` + `SelfContained` + `IncludeAllContentForSelfExtract` + explicit `-r` | Present in the `SingleFile` configuration and correct. The `Compact` (`PublishAot`) configuration cannot work with LLamaSharp and should be deleted or retargeted to a no-local-inference build. |
| **Uniform provider abstraction across Azure OpenAI / Bedrock / local** | An interface that carries logprobs | **Not `Microsoft.Extensions.AI`** — `ChatOptions` has no logprobs member (verified in `dotnet/extensions` source) | Define your own `ITokenIntrospectingChatProvider` returning a rich per-token record, and implement it three times. Use `IChatClient` only for the plain-text path. |
| **Plugin-loaded tools via `Xcaciv.Loader`** | Native library must not be pinned inside a collectible ALC | Reference `LLamaSharp` from the host, expose a shared abstraction to plugins | Design decision for the rebuild; getting it wrong makes model unload silently fail. |

---

## Risks, sharp edges and what you give up

1. **LLamaSharp 0.29.0 is tagged but unpublished.** Tag `v0.29.0` dated 2026-08-24 exists on GitHub; `api.nuget.org` lists only up to 0.27.0 as of 2026-08-28. Do not write a csproj against 0.29.0. When it lands, it removes `LLamaSharp.SemanticKernel`, `LLamaSharp.kernel-memory` and `LLama.Experimental` — if you take a dependency on those now they become dead ends. It also carries the `BatchedExecutor` seq-ID pooling fix, which is directly relevant to the path I recommend.
2. **Four-month llama.cpp lag.** 0.27.0 binds `3f7c29d3` (2026-04-16); upstream is at b10687 (2026-08-29). New model architectures will not load. Print the bound commit in a `/version` or `/backend` command so the failure is diagnosable rather than mysterious.
3. **The `GpuLayerCount = 20` default.** Neither off nor full. Set it explicitly and show the resolved value.
4. **CUDA detection reads env vars, not the driver.** Driver-only machines silently fall to CPU. `SkipCheck(true)` is the override; expose it.
5. **Vulkan detection spawns `vulkaninfo`.** Startup latency, and a hard dependency on the Vulkan tools being on `PATH`.
6. **CUDA 13 is unsupported** and CUDA 11 was dropped after 0.24.0. Machines outside the CUDA-12 window get CPU or Vulkan.
7. **Non-standard native asset layout.** `LLamaSharpRuntimes/` + a props file, not `runtimes/`. Publish without `-r` and you get every RID. This is the single most likely cause of "why is the binary 500 MB."
8. **`PublishAot` is incompatible with this design.** LLamaSharp is not AOT- or trim-annotated, and `IncludeNativeLibrariesForSelfExtract` is ignored under AOT.
9. **A native crash kills the shell.** Persist conversation state before every inference call, not after.
10. **Collectible `AssemblyLoadContext` + native library = an ALC that never unloads.** Keep LLamaSharp out of `Xcaciv.Loader`'s plugin contexts.
11. **`Microsoft.Extensions.AI` cannot carry logprobs.** Its `ChatOptions` has no such member. Build your own introspection contract; don't wait for MEAI to grow one.
12. **The existing top-K softmax is mathematically wrong**, and the reflection that feeds it never succeeds. Both must be fixed, not ported.
13. **`ICustomSampler` sorted-flag contract.** Modify logits without setting `Sorted = false` and downstream stages read a stale order — a silent wrong-output bug, not a crash.
14. **Documentation is behind the code.** `scisharp.github.io/LLamaSharp/latest/` redirects to `0.25.0`. Treat the tagged source as the spec. Likewise the ONNX Runtime GenAI C# API page documents `ComputeLogits()`, which no longer exists.
15. **No character offsets anywhere.** Neither llama.cpp, LLamaSharp, nor ORT GenAI hands you token→character spans. Every option requires you to reconstruct them from byte lengths.

---

## Unconfirmed

Things I could not verify on the live web, and where I looked.

1. **Whether the `noavx` `GGML_F16C`/`GGML_BMI2` bug ([#1407](https://github.com/SciSharp/LLamaSharp/issues/1407), fixed in 0.29.0 by PR #1408) affects 0.27.0.** The 0.29.0 release notes list the fix; I did not diff the CI build flags between v0.27.0 and v0.29.0 to establish whether the bad flags were present in the 0.27.0 `noavx` artifacts. If you must support pre-AVX hardware, test explicitly. *Looked at: LLamaSharp releases API, issue titles.*
2. **Actual published-folder sizes** for `dotnet publish -c SingleFile -r win-x64` and `-r linux-x64` with CPU-only and with CPU+Vulkan. My figures are compressed `.nupkg` sizes from `Content-Length` on `api.nuget.org`; on-disk expansion and single-file compression will both differ. *No publish was run.*
3. **Whether `LLamaSharp.Backend.Vulkan` covers macOS.** Only `.Windows` and `.Linux` sub-packages exist (0.25.0–0.27.0). Metal ships inside `Backend.Cpu` for `osx-arm64` (I confirmed `libggml-metal.dylib` is in the package), so macOS is presumably covered that way, but I did not verify that Metal is actually *enabled* in that build rather than merely present. *Looked at: nuget flat container index for `llamasharp.backend.vulkan.*`, unzipped `llamasharp.backend.cpu.0.27.0.nupkg`.*
4. **Ollama's exact response shape for streaming logprobs**, and whether the OpenAI-compatible `/v1/chat/completions` path on the current v0.33.2 returns populated `logprobs.content[]` or still the empty-array behaviour reported in third-party issues. `api/types.go` proves the native types exist; I did not run a server. *Looked at: `ollama/api/types.go` on main, `docs/api.md` (which does not document the feature), issue #16117.*
5. **Whether `LLamaSharp.Backend.Cpu.Android` matters here.** It exists at 0.27.0 with ~2.6k downloads. Out of scope for a Windows/Linux CLI, not investigated.
6. **A precise upper bound on llama-server's `n_probs`.** `server-schema.cpp` sets no `set_hard_limits` on the `n_probs` field (unlike `min_keep`, which is limited to `0..INT32_MAX`), and `server-context.cpp` clamps it to `max_probs` at emit time. What `max_probs` resolves to in practice — full vocab, or the post-sampler candidate count — I did not trace. *Looked at: `tools/server/server-schema.cpp`, `server-context.cpp` lines ~1953–2000, `common/common.h`.*
7. **Whether `OgaGenerator_GetOutput("logits")` works on the standard Microsoft-published ONNX model artifacts**, i.e. whether those graphs export a node named `logits`. The C API's `GetLogits` (last-token only) is documented but not bound in C#. *Looked at: `src/ort_genai_c.h` and `src/csharp/{Generator,NativeMethods}.cs` at v0.15.2.*
8. **`Xcaciv.Cupcake`, `Xcaciv.Command` and `Xcaciv.Loader` are not on nuget.org** (queried `api.nuget.org/v3-flatcontainer` for all three plus `.Core`/`.Interface` variants — all 404). They exist as GitHub repos under the `Xcaciv` org, last pushed 2026-07-26 (`Loader`, `Command`) and 2026-02-24 (`Cupcake`). My ALC analysis is based on the `Xcaciv.Loader` README's documented usage pattern (`using (var context = new AssemblyContext(...)) { } // Unload`), not on reading its source. Confirm the ALC collectibility model before finalising the plugin boundary.
9. **Whether llama.cpp ships prebuilt `llama-server` binaries for every RID this app targets** (win-x64, win-arm64, linux-x64, linux-arm64, linux-musl-x64) in its release artifacts. Relevant only if you take the subprocess route. *Not checked.*

---

### Sources

- [LLamaSharp on nuget.org](https://www.nuget.org/packages/LLamaSharp) · [Backend.Cpu](https://www.nuget.org/packages/LLamaSharp.Backend.Cpu) · [Backend.Cuda12](https://www.nuget.org/packages/LLamaSharp.Backend.Cuda12)
- [LLamaSharp releases](https://github.com/SciSharp/LLamaSharp/releases) · [README version map](https://github.com/SciSharp/LLamaSharp/blob/v0.27.0/README.md)
- Source at tag `v0.27.0`: [`ISamplingPipeline.cs`](https://github.com/SciSharp/LLamaSharp/blob/v0.27.0/LLama/Sampling/ISamplingPipeline.cs), [`BaseSamplingPipeline.cs`](https://github.com/SciSharp/LLamaSharp/blob/v0.27.0/LLama/Sampling/BaseSamplingPipeline.cs), [`DefaultSamplingPipeline.cs`](https://github.com/SciSharp/LLamaSharp/blob/v0.27.0/LLama/Sampling/DefaultSamplingPipeline.cs), [`LLamaTokenDataArray.cs`](https://github.com/SciSharp/LLamaSharp/blob/v0.27.0/LLama/Native/LLamaTokenDataArray.cs), [`SafeLLamaContextHandle.cs`](https://github.com/SciSharp/LLamaSharp/blob/v0.27.0/LLama/Native/SafeLLamaContextHandle.cs), [`SafeLLamaSamplerHandle.cs`](https://github.com/SciSharp/LLamaSharp/blob/v0.27.0/LLama/Native/SafeLLamaSamplerHandle.cs), [`Batched/Conversation.cs`](https://github.com/SciSharp/LLamaSharp/blob/v0.27.0/LLama/Batched/Conversation.cs), [`Native/Load/`](https://github.com/SciSharp/LLamaSharp/tree/v0.27.0/LLama/Native/Load), [`BatchedExecutorBeamSearch.cs`](https://github.com/SciSharp/LLamaSharp/blob/v0.27.0/LLama.Examples/Examples/BatchedExecutorBeamSearch.cs)
- [llama.cpp server README](https://github.com/ggml-org/llama.cpp/blob/master/tools/server/README.md) · [`server-schema.cpp`](https://github.com/ggml-org/llama.cpp/blob/master/tools/server/server-schema.cpp) · [`server-context.cpp`](https://github.com/ggml-org/llama.cpp/blob/master/tools/server/server-context.cpp)
- [Ollama `api/types.go`](https://github.com/ollama/ollama/blob/main/api/types.go) · [Ollama v0.12.11 release](https://github.com/ollama/ollama/releases/tag/v0.12.11) · [OpenAI-compat logprobs issue #16117](https://github.com/ollama/ollama/issues/16117)
- [OllamaSharp source](https://github.com/awaescher/OllamaSharp/tree/main/src/OllamaSharp/Models)
- [ONNX Runtime GenAI docs](https://onnxruntime.ai/docs/genai/) · [C# API page (stale)](https://onnxruntime.ai/docs/genai/api/csharp.html) · [`src/csharp/Generator.cs`](https://github.com/microsoft/onnxruntime-genai/blob/main/src/csharp/Generator.cs) · [model builder README](https://github.com/microsoft/onnxruntime-genai/blob/v0.15.2/src/python/py/models/README.md)
- [openai-dotnet `ChatTests.cs` logprobs usage](https://github.com/openai/openai-dotnet/blob/main/tests/Chat/ChatTests.cs)
- [`Microsoft.Extensions.AI.Abstractions` `ChatOptions.cs`](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI.Abstractions/ChatCompletion/ChatOptions.cs)
- [LM Studio server docs](https://lmstudio.ai/docs/developer/core/server) · [vLLM `prompt_logprobs` PR #1328](https://github.com/vllm-project/vllm/pull/1328)
- [dotnet/sdk#49995 — `PublishAot` + `IncludeNativeLibrariesForSelfExtract`](https://github.com/dotnet/sdk/issues/49995)
- [Xcaciv.Loader](https://github.com/Xcaciv/Xcaciv.Loader) · [Xcaciv.Command](https://github.com/Xcaciv/Xcaciv.Command) · [Xcaciv.Cupcake](https://github.com/Xcaciv/Xcaciv.Cupcake)
