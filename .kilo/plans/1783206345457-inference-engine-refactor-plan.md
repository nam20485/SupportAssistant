# SupportAssistant — Inference Refactor Plan (InferenceEngine.Core)

**Status:** Draft / implementation-ready
**Supersedes:** the inference portions of [`STATUS.md`](../docs/STATUS.md) §4.A (re-starting point). The agent/tool/framework work in §4.B–§4.D is explicitly **deferred** (see Scope).
**Intended location:** `docs/INFERENCE_ENGINE_REFACTOR_PLAN.md` (written here in `.kilo/plans/` due to plan-mode edit permissions; relocate freely).
**Date:** 2026-07-04
**Target:** `src/SupportAssistant.Core`, `src/SupportAssistant` (DI), `src/SupportAssistant.Tests`

---

## 1. Context & rationale

[`STATUS.md`](../docs/STATUS.md) establishes that **no real inference exists** today:

- `ResponseGenerationService.GenerateWithModelAsync` always routes to `GenerateSimpleResponseAsync` (canned strings + `Task.Delay(100)`) — `ResponseGenerationService.cs:231`.
- `OnnxEmbeddingService.GenerateOnnxEmbeddingAsync` is a placeholder that falls back to deterministic hash vectors — `OnnxEmbeddingService.cs:198`.
- `OnnxRuntimeService` only detects providers and builds `SessionOptions`; it performs no inference — `OnnxRuntimeService.cs`.
- `AgentOrchestrator.SimulateSLMResponseAsync` is a placeholder behind every ReAct step — `Agent/IAgentOrchestrator.cs:623`.

**Change of course:** instead of hand-rolling tokenizer + ONNX session + HW-accel + model-download, adopt the external [`InferenceEngine.Core`](https://github.com/intel-agency/inference-engine-lib) library, which provides:

| Capability (from InferenceEngine.Core) | What it replaces in SupportAssistant |
| --- | --- |
| `BaseInferenceEngine<TIn,TOut>` ONNX session lifecycle | `OnnxRuntimeService.CreateSessionOptions` / session juggling |
| Cross-platform HW accel: DirectML (Win), CoreML (macOS), ROCm (Linux x64) + automatic CPU fallback with full `InferenceFallbackReason` diagnostics | Hand-rolled DirectML probe + `IsDirectMLAvailable()` |
| `WarmupOnLoad` (dummy run to pre-compile GPU graphs) | (new — not done today) |
| `ModelProviders` (`LocalModelProvider`, `DownloadingModelProvider` multi-URL + cache, `ModelRegistry`) | `DefaultConfigurationService.GetModelPath` ("look in a few paths, give up") |
| `InferenceEngineInfo` diagnostic snapshot (`Provider`, `IsFallback`, `FallbackReason`, ordered `Checks`) | `IOnnxRuntimeService.GetAvailableProviders` string array |
| `OnSessionInitialized` callback | (new) |

### What InferenceEngine.Core does NOT solve (stays ours)

1. **Tokenization** — the library is pure tensor-in/tensor-out. A tokenizer is a hard prerequisite (STATUS §4.A item 1).
2. **Autoregressive generation / streaming** — the public contract is one-shot `PredictAsync` (exactly one `session.Run`, then `PostProcessWithContext`). An LLM/SLM decode loop requires N `session.Run` calls (see Phase 3 and Appendix A).

### Resolved design decisions

| # | Decision | Choice |
| --- | --- | --- |
| D1 | License | **Re-license SupportAssistant MIT → `AGPL-3.0-or-later`** to permit binary distribution with the AGPL dependency. |
| D2 | Scope | **Embeddings + SLM generation via InferenceEngine.Core.** Defer §4.B (agent DI wiring), §4.C (approval UI/restore), §4.D (Phase 4.3+ tools). |
| D3 | Streaming | **Decode loop inside our service**: subclass `BaseInferenceEngine<string,string>`, reuse `LoadAsync`, override `PredictAsync` with an autoregressive KV-cache loop, add `StreamAsync` (`IAsyncEnumerable<string>`), extend `ISLMService` with a streaming method. |
| D4 | Tokenizer | **`Microsoft.ML.Tokenizers`** (Phi-3 tokenizer). |
| D5 | Plan location | `.kilo/plans/` (this file); intended home `docs/INFERENCE_ENGINE_REFACTOR_PLAN.md`. |

---

## 2. Target architecture (old → new)

```
OLD                                   NEW
OnnxRuntimeService (DI singleton)  →  retired; BaseInferenceEngine owns session+accel
IOnnxRuntimeService                →  retired; Settings UI reads InferenceEngineInfo
OnnxEmbeddingService (stub)        →  delegates to TextEmbeddingEngine : BaseInferenceEngine<string, float[]>
ResponseGenerationService          →  injects ISLMService; GenerateWithModelAsync calls the real SLM
 (GenerateSimpleResponseAsync)     →  kept only as last-resort fallback
(new) OnnxSLMService : ISLMService →  wraps Phi3GenerationEngine : BaseInferenceEngine<string, string>
ISLMService (one method)           →  + StreamResponseAsync(IAsyncEnumerable<string>)
DefaultConfigurationService        →  supplies model names/URLs + InferenceEngineOptions mapping
```

New folder: `src/SupportAssistant.Core/Engines/` containing the two engine subclasses (mirrors `InferenceEngine.Examples/FaceDetectionEngine.cs`).

### Key API facts that constrain the design (verified)

- `BaseInferenceEngine.PredictAsync` is `virtual` (`BaseInferenceEngine.cs:264`) and calls `session.Run` **once**. Overriding it for the SLM decode loop is supported.
- `_session`, `_runOptions` are `protected readonly`/`protected` (`:21-22`); `InputMetadata`, `OutputMetadata`, `PrimaryInputName` are public (`:29-31`). A subclass can run its own decode loop against the session.
- `ModelName`, `ModelDownloadUrls`, `ModelRegistry`, `ModelCacheDirectory` are all overridable (`:53,60,66,74`). Per-engine model registration needs no upstream change.
- `InferenceEngineOptions.ModelPath` internally creates a `LocalModelProvider`; `ModelProvider` overrides all resolution (`InferenceEngineOptions.cs:21-25`).
- Library uses `Microsoft.ML.OnnxRuntime` **1.24.1**; SupportAssistant pins **1.19.2** (`SupportAssistant.Core.csproj:15-16`). Adopting it forces the upgrade.

---

## 3. Implementation tasks (ordered)

### Phase 0 — Prerequisites & licensing

- **T0.1 Re-license to AGPL-3.0-or-later.**
  - `Directory.Build.props`: change `PackageLicenseExpression` MIT → `AGPL-3.0-or-later` (`:50`); set `PackageRequireLicenseAcceptance` false → `true` (`:51`); update `Copyright`/`PackageTags`.
  - Add root `LICENSE` (AGPL-3.0 full text) and `NOTICE` attributing `InferenceEngine.Core` (© 2026 Artificial Intelligence Agency, AGPL-3.0-or-later, https://github.com/intel-agency/inference-engine-lib).
  - Update `README.md` license note.
- **T0.2 NuGet source + package pin.** `nuget.config` already targets nuget.org. Verify `InferenceEngine.Core` is restorable from nuget.org at a stable version; if it lags, add the GitHub Packages feed `https://nuget.pkg.github.com/intel-agency/index.json` (needs a read PAT in CI). Pin an explicit `<PackageReference Include="InferenceEngine.Core" Version="…" />`.
- **T0.3 ONNX Runtime upgrade 1.19.2 → 1.24.1.**
  - `SupportAssistant.Core.csproj`: bump `Microsoft.ML.OnnxRuntime` to `1.24.1`; remove the direct `Microsoft.ML.OnnxRuntime.DirectML` ref (it arrives transitively on Windows RID via InferenceEngine.Core).
  - Build must stay clean under `TreatWarningsAsErrors=true` (`Directory.Build.props:27`).
  - Verify no 1.19→1.24 breaking API changes touched (`InferenceSession`, `SessionOptions.AppendExecutionProvider_*`, `NamedOnnxValue`).

### Phase 1 — Project wiring & model registration

- **T1.1 Add package refs** to `SupportAssistant.Core.csproj`: `InferenceEngine.Core` + `Microsoft.ML.Tokenizers` (+ transitives).
- **T1.2 Register models.** Two ONNX models, supplied via per-engine `ModelDownloadUrls` overrides (no upstream `models.json` edit required):
  - Embedding: **all-MiniLM-L6-v2** ONNX (384-dim, matches `DefaultConfigurationService.GetEmbeddingDimension()`). URLs: HuggingFace `sentence-transformers/all-MiniLM-L6-v2` ONNX export (+ a mirror).
  - SLM: **Phi-3-mini-4k-instruct** ONNX (already referenced by `DefaultConfigurationService`). URLs: HuggingFace `microsoft/Phi-3-mini-4k-instruct-onnx` (+ mirror).
  - Each engine **also** honors an explicit local path via `InferenceEngineOptions.ModelPath` (`LocalModelProvider`) when `DefaultConfigurationService` finds a pre-bundled file — enables offline / first-run-without-download.
- **T1.3 Tokenizer assets.** Ship/load the Phi-3 `tokenizer.json` (and the MiniLM tokenizer for embeddings) from the app data dir; engines load them via `Microsoft.ML.Tokenizers`.

### Phase 2 — Embedding engine (clean one-shot win)

- **T2.1 `Engines/TextEmbeddingEngine : BaseInferenceEngine<string, float[]>`.**
  - `protected override string ModelName => "all-MiniLM-L6-v2.onnx";` (override `ModelDownloadUrls`).
  - `PreProcessWithContext(string text)`: tokenize → `input_ids` + `attention_mask` int64 tensors (sequence shape from `InputMetadata`).
  - `PostProcessWithContext(output, ctx)`: read `last_hidden_state` (or the model's actual output name from `OutputMetadata`), mean-pool over the sequence (masked), L2-normalize → `float[]`.
  - Keep dimension discovery from `OutputMetadata` (replace hardcoded 384).
- **T2.2 Refactor `OnnxEmbeddingService`** to delegate: `InitializeAsync` → `engine.LoadAsync()`; `GenerateEmbeddingAsync` → `await engine.PredictAsync(text)`. Preserve `IEmbeddingService` and the existing hash fallback **only** when the model is unavailable (graceful degradation). `CalculateCosineSimilarity` unchanged.
- **T2.3 Remove `IOnnxRuntimeService`/`OnnxRuntimeService`** from the embedding path. (See T4.2 for the residual diagnostics surface.)

### Phase 3 — SLM engine + streaming service (headline)

- **T3.1 `Engines/Phi3GenerationEngine : BaseInferenceEngine<string, string>`.**
  - `ModelName` = Phi-3 ONNX; override `ModelDownloadUrls`.
  - **Override `PredictAsync`** to run the autoregressive loop (call `LoadAsync()` first if not loaded — base no longer does it once you override):
    1. Tokenize prompt (Phi-3 tokenizer) → initial `input_ids` + `attention_mask` (+ `position_ids`/`cache_position` if the export requires).
    2. **Prefill**: `_session.Run(...)` with the full prompt; capture `logits` + `present_key_values` (KV cache).
    3. **Decode loop**: sample next token (greedy default; virtual `SampleNextToken` for top-k/top-p/temperature/repetition-penalty); append token; grow `attention_mask`; feed `past_key_values := present_key_values` from previous step; `_session.Run(...)`. Stop on EOS or `MaxTokens`.
    4. Detokenize generated ids → `string`.
  - **Discovery task (must do first):** inspect the chosen Phi-3 ONNX export's `InputMetadata`/`OutputMetadata` at load to learn exact KV-cache input/output names (`past_key_values.*` ↔ `present_key_values.*`), whether `use_cache_branch` toggles exist, and whether prefill+decode share one graph. This is the main model-specific risk.
  - **Add `StreamAsync(string prompt, GenerationParams? p, CancellationToken ct) : IAsyncEnumerable<string>`**: same loop but `yield return` each decoded token-chunk (detokenized incrementally), honoring `ct`.
- **T3.2 Extend `ISLMService`** (`Agent/IAgentOrchestrator.cs:19`): add
  `IAsyncEnumerable<string> StreamResponseAsync(string prompt, CancellationToken cancellationToken = default);`
  Implement **`OnnxSLMService : ISLMService`**: holds a `Phi3GenerationEngine`; `IsAvailable` = engine loaded; `GenerateResponseAsync` = `engine.PredictAsync`; `StreamResponseAsync` = `engine.StreamAsync`.
- **T3.3 Wire `ResponseGenerationService`** to depend on `ISLMService` (new ctor param). `GenerateWithModelAsync` calls `ISLMService.GenerateResponseAsync(prompt)` (or streams into the UI). `GenerateSimpleResponseAsync` stays as the catch-all fallback when the SLM is unavailable.

### Phase 4 — DI & configuration

- **T4.1 `App.axaml.cs.ConfigureServices`** (`:97`):
  - Add singletons: `TextEmbeddingEngine`, `Phi3GenerationEngine`, `OnnxSLMService` (as `ISLMService`).
  - Rewire `OnnxEmbeddingService`/`IEmbeddingService` to consume the embedding engine; remove the `IOnnxRuntimeService` registration.
  - Build `InferenceEngineOptions` from settings (GPU toggle → `UseGpuAcceleration`, `DeviceId`, `WarmupOnLoad`).
- **T4.2 Config & diagnostics surface.** Update `DefaultConfigurationService`/settings to expose model names, GPU toggle, and an `InferenceEngineInfo` snapshot (provider, `IsFallback`, `FallbackReason`, `Checks`). The Settings UI replaces `IsDirectMLAvailable()` text with the rich provider/fallback display. Use the `OnSessionInitialized` callback to populate it on the background-init path (`BackgroundTaskService`).

### Phase 5 — Validation

- **T5.1 Clean build** (`dotnet build -c Release`) — 0 warnings under `TreatWarningsAsErrors`.
- **T5.2 Tests** (`SupportAssistant.Tests`, was 134/134):
  - `TextEmbeddingEngine`: similar-text cosine > dissimilar-text; dimension == output-metadata last dim.
  - `Phi3GenerationEngine` / `OnnxSLMService`: **one-shot output == concatenation of streamed chunks** (parity invariant); EOS stops the loop; `CancellationToken` aborts mid-stream; respects `MaxTokens`.
  - Run full suite; keep the 100% baseline.
- **T5.3 Manual smoke**: real embedding similarity search over the knowledge base; real Phi-3 generation end-to-end; verify CPU path **and** DirectML path; confirm no synthetic fallback fires unless the model is genuinely missing.

---

## 4. Risks & open questions

| Risk / question | Mitigation |
| --- | --- |
| KV-cache I/O name/shape discovery for the specific Phi-3 ONNX export (prefill vs decode graph, `use_cache_branch`, `past/present_key_values` naming) | T3.1 discovery task; if the export lacks a KV-cache graph, fall back to full-recompute-per-step (O(n²), acceptable for short contexts) and revisit. |
| ONNX Runtime 1.19.2 → 1.24.1 API/breaking changes under `TreatWarningsAsErrors` | T0.3 clean-build gate before any feature work. |
| DirectML native asset packaging in a single-file/publish build (existing `PublishSingleFile` Release path) | Verify native libs deploy for win-x64/win-arm64; the library already gates DirectML on Windows RID. |
| First-run model download UX (Phi-3 is large) | `DownloadingModelProvider` cache + progress surface; honor pre-bundled `LocalModelProvider` for offline. |
| `InferenceEngine.Core` restore in CI (nuget.org vs GitHub Packages) | T0.2: confirm feed; add read PAT to CI secrets if GitHub Packages needed. |
| AGPL re-license requires all contributors to agree | Confirm contributor/ownership situation before T0.1. |
| `ISLMService` streaming addition ripples to `AgentOrchestrator` ReAct path | Keep `GenerateResponseAsync` as the orchestrator's default; streaming is opt-in for the chat UI. |

---

## 5. Deferred to follow-on plans (out of scope here)

Per decision D2, these remain owned by [`PHASE_4_IMPLEMENTATION_PLAN.md`](../docs/PHASE_4_IMPLEMENTATION_PLAN.md) and STATUS §4.B–§4.D:

- §4.B — register `ToolRegistry`, `ISecurityManager`, `IAgentOrchestrator`, `ISLMService` end-to-end and route `ChatViewModel` through the orchestrator (the `ISLMService` implemented here is the prerequisite).
- §4.C — real Human-in-the-Loop approval UI, real `RestoreBackupAsync`, uncommented `RegisterCoreTools()`.
- §4.D — Phase 4.3+ tools (file/config/system-info/network/diagnostic).

---

## Appendix A — Feasibility: adding tensor-in/out streaming to upstream InferenceEngine.Core

**Question:** Could pure tensor-in/out **streaming** generation be added to the upstream `InferenceEngine.Core` package so consumers (like this SLM engine) don't reimplement the decode loop — while preserving the existing one-shot behavior and letting the consumer choose one, the other, or both?

**Verdict: Feasible.** The base class already exposes everything a decode loop needs.

### A.1 Why the current contract can't stream

`IInferenceEngine<TInput,TOutput>.PredictAsync` runs `PreProcessWithContext → _session.Run(once) → PostProcessWithContext`. Autoregressive generation needs N forward passes (prefill + per-token decode with KV-cache feedback), so it cannot be expressed through the single-`Run` pipeline. The session and metadata, however, are already accessible to subclasses (`_session`, `_runOptions` protected; `InputMetadata`/`OutputMetadata`/`PrimaryInputName` public), so a loop *can* be authored against them today — just not exposed generically.

### A.2 Proposed upstream addition (non-breaking)

**New optional interface — a streaming engine still *is* a one-shot engine:**

```csharp
public interface IStreamingInferenceEngine<TInput, TOutput, TStep>
    : IInferenceEngine<TInput, TOutput>
{
    /// One token/logit/chunk per yield; the consumer decides how to render or accumulate.
    IAsyncEnumerable<TStep> PredictStreamingAsync(
        TInput input, CancellationToken cancellationToken = default);
}
```

**New abstract base — reuses `BaseInferenceEngine` for session/accel/warmup/model-download/diagnostics:**

```csharp
public abstract class BaseStreamingInferenceEngine<TInput, TOutput, TStep>
    : BaseInferenceEngine<TInput, TOutput>, IStreamingInferenceEngine<TInput, TOutput, TStep>
{
    // One-shot consumers see no change: PredictAsync runs the loop to completion.
    public override async Task<TOutput> PredictAsync(TInput input)
    {
        var steps = new List<TStep>();
        await foreach (var s in PredictStreamingAsync(input)) steps.Add(s);
        return BuildFinalOutput(steps);
    }

    public abstract IAsyncEnumerable<TStep> PredictStreamingAsync(
        TInput input, CancellationToken cancellationToken = default);

    // Model-aware hooks (engine knows its I/O contract):
    protected abstract (List<NamedOnnxValue> Inputs, object? State) BuildStepInputs(
        TInput input, object? state, int step);          // prefill (step 0) vs decode (step>0)
    protected abstract (TStep Step, object? NextState, bool IsEos) ProcessStepOutputs(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs, object? state);

    protected virtual TStep SampleNextToken(/* logits, params */) { /* greedy default */ }
    protected abstract TOutput BuildFinalOutput(IReadOnlyList<TStep> steps);
}
```

Because `BaseStreamingInferenceEngine` **inherits** `BaseInferenceEngine`, all existing lifecycle code (`LoadAsync`, provider selection, warmup, `ModelProviders`, `InferenceEngineInfo`) is reused unchanged; `IInferenceEngine` one-shot behavior is preserved by routing `PredictAsync` through the streaming loop. A consumer can program to **either** `IInferenceEngine` (one-shot) **or** `IStreamingInferenceEngine` (streaming) **on the same instance**.

### A.3 Requirements to land it upstream

1. **New types**: `IStreamingInferenceEngine<,,>` and `BaseStreamingInferenceEngine<,,>` in `InferenceEngine.Core` (no changes to `IInferenceEngine` / `BaseInferenceEngine`).
2. **KV-cache plumbing helper** (protected): the model-agnostic part of carrying `present_key_values` outputs back as `past_key_values` inputs, growing `attention_mask`/`position_ids`/`cache_position`. Because HF-optimum ONNX exports vary in I/O names, the **engine** owns tensor assembly (`BuildStepInputs`) and output parsing (`ProcessStepOutputs`); the base supplies the loop skeleton + cancellation + sampling defaults.
3. **Stopping rules**: EOS-token + max-steps, evaluated in the loop.
4. **Sampling**: virtual `SampleNextToken` with greedy default and overridable top-k/top-p/temperature/repetition-penalty.
5. **Cancellation**: thread through `CancellationToken` to `_session.Run` and `IAsyncEnumerable`.
6. **TFM/async**: native `IAsyncEnumerable` on net10 (no `Microsoft.Bcl.AsyncInterfaces` needed for this repo's net10.0 target).
7. **Tests**: parity (`BuildFinalOutput(stream) == PredictAsync`), cancellation mid-stream, KV-cache correctness vs. full-recompute, EOS/max-token stops.
8. **Docs**: README section + a `StreamingTextGenerationExample` mirroring `FaceDetectionEngine`.

### A.4 What stays the consumer's job (by design)

Tokenization and detokenization remain **outside** the library — it streams model-level `TStep`s (token ids / logits / text chunks). This keeps the library pure tensor-in/out and model-agnostic; detokenization and text framing are consumer concerns (as they are in this plan's `Phi3GenerationEngine`).

### A.5 Recommendation

Implement SupportAssistant's SLM streaming locally now (Phase 3) to unblock the product, then propose A.2–A.3 as an upstream contribution to `intel-agency/inference-engine-lib` (branch from `development`, target `development`). Once merged, `Phi3GenerationEngine` can collapse from a hand-written decode loop into a `BaseStreamingInferenceEngine<string,string,string>` subclass with three small hooks — a clean migration with no one-shot regression.
