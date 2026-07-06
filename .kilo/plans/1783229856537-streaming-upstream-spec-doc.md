# Plan: Standalone Upstream Streaming Spec for `InferenceEngine.Core`

**Status:** Finalized
**Date:** 2026-07-04
**Author:** planning agent
**Scope decision:** Produce ONE new documentation file only. No source-code changes.
The three referenced plans (`docs/plans/PHASE_4_IMPLEMENTATION_PLAN.md`,
`docs/plans/STATUS.md`, `docs/plans/inference-engine-refactor-plan.md`) are authoritative
context and are NOT re-implemented or rewritten here.

---

## 1. Goal & single deliverable

Create a **standalone, implement-ready handoff spec** that tells the **upstream
`InferenceEngine.Core` maintainers** exactly what to add to their library to offer
tensor-in/out **streaming** generation — while preserving the existing one-shot behavior.

- **Deliverable file (create new):** `docs/plans/inference-engine-streaming-upstream-spec.md`
  - Sibling of `docs/plans/inference-engine-refactor-plan.md`; kebab-case to match house style.
- **Derived from:** Appendix A of `docs/plans/inference-engine-refactor-plan.md` (feasibility sketch),
  expanded into a complete spec.
- **Audience test:** a reviewer who has never read any SupportAssistant plan must be able to
  implement streaming in `InferenceEngine.Core` from this document alone.

## 2. Source facts & hard constraints (carry into the spec)

- All base-class API facts come from Appendix A / §2 of the refactor plan, which marks them
  **"verified"** against the library source (`BaseInferenceEngine.cs`):
  - `BaseInferenceEngine.PredictAsync` is `virtual` and calls `_session.Run` **exactly once**.
  - `_session`, `_runOptions` are `protected readonly` / `protected`.
  - `InputMetadata`, `OutputMetadata`, `PrimaryInputName` are `public`.
  - `ModelName`, `ModelDownloadUrls`, `ModelRegistry`, `ModelCacheDirectory` are overridable.
  - Lifecycle to reuse unchanged: `LoadAsync`, provider selection, `WarmupOnLoad`,
    `ModelProviders`, `InferenceEngineInfo`, `OnSessionInitialized`.
- **Constraint — unverifiable upstream:** `https://github.com/intel-agency/inference-engine-lib`
  returns **404** (private/unreachable). The spec MUST therefore open with a prominent
  **Prerequisite** block instructing the upstream team to reconcile every signature/line-number
  against the current upstream `development` branch before implementing.
- **ONNX Runtime types the loop touches (real, from `Microsoft.ML.OnnxRuntime`):**
  `InferenceSession.Run(IReadOnlyCollection<NamedOnnxValue>)` returns
  `IDisposableReadOnlyCollection<DisposableNamedOnnxValue>`; tensors built via
  `NamedOnnxValue.CreateFromTensor<T>(string name, DenseTensor<T> tensor)`.
- **Tokenization stays OUT of the library** (Appendix A.4): the lib streams model-level
  `TStep`s (token ids / logits / text chunks). Detokenization/framing is a consumer concern.

## 3. Decisions (baked in)

| # | Decision | Choice |
| --- | --- | --- |
| 1 | Filename / location | `docs/plans/inference-engine-streaming-upstream-spec.md` |
| 2 | Appendix A disposition | Keep Appendix A in the refactor plan; **cross-link both ways**. (See §8 follow-up.) |
| 3 | Consumer/migration content | Include a **concise, clearly-separated** "Consumer perspective & migration" section (motivates the proposal; gives upstream a validation consumer). The bulk stays upstream-focused. |
| 4 | Tokenization scope | Out of the library (A.4). `TStep` is model-level. |
| 5 | Async | net10 native `IAsyncEnumerable<T>` — no `Microsoft.Bcl.AsyncInterfaces` (repo targets net10.0). |
| 6 | Compatibility posture | **Non-breaking / additive only** — zero edits to `IInferenceEngine` / `BaseInferenceEngine`. |

## 4. Blueprint of the spec document (section-by-section content to write)

The implementation agent authors `docs/plans/inference-engine-streaming-upstream-spec.md`
with these sections. Include the exact C# shown below.

### 0. Header + Prerequisite block
- Title, status (Draft / proposal), date, intended upstream repo, target branch
  (`branch from development → target development`).
- **Prerequisite:** "Reconcile all signatures against the current `development` branch;
  this spec was authored against a private/404 upstream and cites line numbers from a
  downstream feasibility note."

### 1. Purpose, audience & non-goals
- Purpose: enable N-step autoregressive streaming with KV-cache reuse on top of the existing
  one-shot engine.
- Non-goals: tokenization/detokenization in the lib; text framing; model download (already solved).

### 2. Background & motivation
- One-shot `IInferenceEngine<TInput,TOutput>.PredictAsync` =
  `PreProcessWithContext → _session.Run(once) → PostProcessWithContext`.
- Autoregressive generation needs prefill + per-token decode (N forward passes with KV-cache
  feedback) → cannot be expressed through the single-`Run` pipeline.
- The session + metadata are already subclass-accessible, so a loop *can* be authored today —
  just not exposed generically.

### 3. Current contract recap (verified subclass access)
- Bullet the verified facts from §2 of this plan (protected `_session`/`_runOptions`; public
  `InputMetadata`/`OutputMetadata`/`PrimaryInputName`; virtual `PredictAsync`; reusable lifecycle).

### 4. Design goals & constraints
- Non-breaking; additive; one-shot behavior preserved; consumer chooses one-shot, streaming, or
  both on the same instance; reuse all `BaseInferenceEngine` lifecycle; net10 native
  `IAsyncEnumerable`; model-agnostic loop skeleton (engine owns tensor assembly + output parsing).

### 5. Proposed public API (EXACT C# to include)

```csharp
// New optional interface — a streaming engine still IS a one-shot engine.
public interface IStreamingInferenceEngine<TInput, TOutput, TStep>
    : IInferenceEngine<TInput, TOutput>
{
    /// One token / logit / chunk per yield. The consumer decides how to render or accumulate.
    IAsyncEnumerable<TStep> PredictStreamingAsync(
        TInput input, CancellationToken cancellationToken = default);
}

// New abstract base — reuses BaseInferenceEngine for session/accel/warmup/model-download/diagnostics.
public abstract class BaseStreamingInferenceEngine<TInput, TOutput, TStep>
    : BaseInferenceEngine<TInput, TOutput>, IStreamingInferenceEngine<TInput, TOutput, TStep>
{
    // One-shot consumers see no change: PredictAsync runs the loop to completion.
    public override async Task<TOutput> PredictAsync(TInput input)
    {
        var steps = new List<TStep>();
        await foreach (var s in PredictStreamingAsync(input))
            steps.Add(s);
        return BuildFinalOutput(steps);
    }

    public abstract IAsyncEnumerable<TStep> PredictStreamingAsync(
        TInput input, CancellationToken cancellationToken = default);

    // Model-aware hooks — the engine knows its own I/O contract.
    // step == 0 => prefill; step > 0 => decode with KV-cache feedback.
    protected abstract (List<NamedOnnxValue> Inputs, object? State) BuildStepInputs(
        TInput input, object? state, int step);

    protected abstract (TStep Step, object? NextState, bool IsEos) ProcessStepOutputs(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs, object? state);

    protected virtual TStep SampleNextToken(/* logits + StreamingOptions */) { /* greedy default */ }

    protected abstract TOutput BuildFinalOutput(IReadOnlyList<TStep> steps);
}
```

Because `BaseStreamingInferenceEngine` **inherits** `BaseInferenceEngine`, all lifecycle code is
reused unchanged, and `IInferenceEngine` one-shot behavior is preserved by routing `PredictAsync`
through the streaming loop. A consumer can program to **either** `IInferenceEngine` (one-shot)
**or** `IStreamingInferenceEngine` (streaming) on the same instance.

### 6. Behavioral contract (the "exactly how" details)
- **Prefill (step 0):** tokenize/prompt tensor assembly done by engine in `BuildStepInputs`;
  full-sequence forward pass.
- **Decode (step>0):** feed `past_key_values := present_key_values` from the previous step; grow
  `attention_mask` (+ `position_ids` / `cache_position` if the export requires); one token per step.
- **KV-cache plumbing helper (protected):** model-agnostic carry-back of `present_*` → `past_*`.
  Because HF-optimum ONNX I/O names vary, the **engine** owns tensor assembly
  (`BuildStepInputs`) and output parsing (`ProcessStepOutputs`); the base supplies the loop
  skeleton + cancellation + sampling defaults.
- **Stopping rules:** EOS-token + max-steps; evaluated in the loop; default `MaxSteps`
  overridable via `StreamingOptions`.
- **Sampling:** virtual `SampleNextToken` with greedy default; overridable
  top-k / top-p / temperature / repetition-penalty via `StreamingOptions`.
- **Cancellation:** thread `CancellationToken` into `_session.Run` and the `IAsyncEnumerable`;
  document `OperationCanceledException` semantics and that **partial output is discarded**
  (no half-state leak across runs).
- **Parity invariant (load-bearing):** `BuildFinalOutput(PredictStreamingAsync(...))` MUST equal
  `PredictAsync(...)` for the same input + options.
- **Resource disposal:** `Dispose()` each step's `IDisposableReadOnlyCollection<DisposableNamedOnnxValue>`
  after `ProcessStepOutputs`.
- **Concurrency:** document whether one engine instance allows multiple in-flight streams
  (recommend: **no** — one in-flight stream per instance, since KV-cache state is per-run); a
  one-shot `PredictAsync` and a streaming call must not interleave on the same instance.

### 7. Configuration surface (EXACT C# to include)
```csharp
public sealed class StreamingOptions
{
    public int MaxSteps { get; set; } = 256;
    public IReadOnlyList<int> EosTokenIds { get; set; } = Array.Empty<int>();
    public SamplingParams Sampling { get; set; } = new();      // greedy by default
    public bool IncludePrefillInStream { get; set; } = false;  // emit prompt tokens? default no
    public int? ChunkSize { get; set; } = null;                // batch >=1 decoded steps per yield
}
public sealed class SamplingParams
{
    public float Temperature { get; set; } = 0f;       // 0 => greedy
    public int TopK { get; set; } = 0;                 // 0 => disabled
    public float TopP { get; set; } = 1f;              // 1 => disabled
    public float RepetitionPenalty { get; set; } = 1f; // 1 => disabled
}
```
Note in the spec: `StreamingOptions` is passed to `PredictStreamingAsync` via an overload or set
on the instance; pick one and be consistent (recommend an overload with default = instance options).

### 8. Non-breaking / compatibility analysis
- Concrete proof: **no** changes to `IInferenceEngine<,>` or `BaseInferenceEngine<,>`.
- Only **new** types added; existing one-shot consumers compile + behave identically.
- `BaseStreamingInferenceEngine` derives from `BaseInferenceEngine`, so provider selection,
  warmup, `ModelProviders`, `InferenceEngineInfo`, `OnSessionInitialized` all apply unchanged.

### 9. Implementation task list (ordered, FOR THE UPSTREAM TEAM)
1. Add `IStreamingInferenceEngine<TInput,TOutput,TStep>` (new file).
2. Add `BaseStreamingInferenceEngine<TInput,TOutput,TStep>` implementing the loop skeleton:
   `PredictAsync` (routes through stream), `PredictStreamingAsync` (drives prefill → decode →
   stop), per-step `Dispose`, cancellation.
3. Add protected KV-cache plumbing helper (`present_*` → `past_*`, grow masks/positions) —
   model-agnostic; engine supplies names.
4. Add `StreamingOptions` + `SamplingParams`; wire `SampleNextToken` greedy default + overrides.
5. Thread `CancellationToken` into `_session.Run` and the `IAsyncEnumerable`.
6. Implement stop rules (EOS + max-steps).
7. Tests (see §10).
8. Docs + example (see §11).

### 10. Test requirements (must-pass, listed as acceptance)
- **Parity:** `BuildFinalOutput(stream) == PredictAsync(input)` for identical input + options.
- **Cancellation:** mid-stream `CancellationToken` aborts promptly; no leaked state on the instance.
- **KV-cache correctness:** streaming-with-KV output == full-recompute-per-step output.
- **Stop rules:** EOS token stops the loop; `MaxSteps` stops the loop.
- **Concurrency:** second call on a busy instance is rejected/documented.
- **Determinism:** greedy sampling is deterministic across runs.
- Run under the repo's `TreatWarningsAsErrors=true` gate; keep existing suite green.

### 11. Documentation requirements
- README section: "Streaming inference" with the interface + a minimal subclass sketch.
- New example `StreamingTextGenerationExample` mirroring the existing `FaceDetectionEngine`
  example (subclass `BaseStreamingInferenceEngine<,,>`, implement the 3 hooks, consume the stream).

### 12. Consumer perspective & migration (SupportAssistant; clearly separated, optional read)
- **Before (local hand-written loop):** `Phi3GenerationEngine : BaseInferenceEngine<string,string>`
  overrides `PredictAsync` with its own prefill/decode loop + `StreamAsync`.
- **After (upstream merged):** collapse to
  `Phi3GenerationEngine : BaseStreamingInferenceEngine<string,string,string>` implementing only
  `BuildStepInputs`, `ProcessStepOutputs`, `BuildFinalOutput` (+ optional `SampleNextToken`).
- Include a short before/after code sketch and note zero one-shot regression (parity test gates it).
- Cross-link back to `docs/plans/inference-engine-refactor-plan.md` Phase 3 / Appendix A.

### 13. Open questions / risks (for upstream)
- KV-cache I/O name/shape variance across HF-optimum ONNX exports; `use_cache_branch` toggles;
  whether prefill + decode share one graph or need two. (Engine owns these via the hooks.)
- Full-recompute-per-step fallback (O(n²)) if an export lacks a KV-cache graph.
- Signature reconciliation (see Prerequisite block) due to unreachable upstream source.
- Concurrency model decision (§6).

### 14. Acceptance checklist
- [ ] Self-contained (passes the audience test in §1).
- [ ] Exact C# for interface + base + options included.
- [ ] Behavioral contract covers prefill/decode/KV-cache/stop/sampling/cancellation/parity/disposal/concurrency.
- [ ] Prerequisite (reconcile vs `development`) + upstream-404 caveat present.
- [ ] Test requirements + acceptance criteria enumerated.
- [ ] Non-breaking proof stated.
- [ ] Cross-links to Appendix A and the refactor plan present.

## 5. Ordered tasks for the implementation agent (executes this plan)

1. Create `docs/plans/inference-engine-streaming-upstream-spec.md` following §4's blueprint verbatim
   (copy the C# blocks from §4.5, §4.7, §4.12).
2. Ensure every section in §4 (0–14) is present and in order.
3. Add bidirectional cross-links: new spec → Appendix A / refactor plan; (Appendix A → spec is the
   §8 follow-up).
4. Self-check against the §4.14 acceptance checklist before stopping.
5. Do NOT modify any source file or any of the three referenced plans (Appendix A edit is a
   separate, optional follow-up — see §8).

## 6. Risks / open questions for THIS planning deliverable
- **Upstream unreachable (404):** mitigated by the Prerequisite block (§4.0) and the
  signature-reconciliation acceptance item.
- **Consumer section length:** kept concise and clearly separated so the core team can ignore it.

## 7. Validation (how we know the doc is done)
- The new file exists at the exact path in §1.
- It passes the audience test and the §4.14 checklist.
- No source files, no other plan files touched (Appendix A edit explicitly deferred).

## 8. Deferred / optional follow-up (NOT part of this plan; flag only)
- Optionally trim Appendix A of `docs/plans/inference-engine-refactor-plan.md` to a short pointer
  that links to the new spec, and add the reverse link. This edits an existing plan doc — out of
  scope for this planning turn; an implementation agent can do it as a separate small change.
