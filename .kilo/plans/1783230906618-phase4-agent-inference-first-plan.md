# Phase 4 Agent Implementation — Inference-First Critical Path

**Status:** Implementation-ready (planning complete)
**Date:** 2026-07-04
**Owner repo:** `nam20485/SupportAssistant` (head `8749562`, build clean, 134/134 tests, .NET 10)
**Plan type:** Ordered task list for an implementation-capable agent.

## 0. What this plan is

A single coherent, end-to-end critical path that turns SupportAssistant from a **simulated** RAG shell into a **real, running, tool-augmented agent**. It supersedes the inference framing of [`STATUS.md`](../../docs/plans/STATUS.md) §4 and reconciles [`PHASE_4_IMPLEMENTATION_PLAN.md`](../../docs/plans/PHASE_4_IMPLEMENTATION_PLAN.md) (which wrongly treats Phase 4.1/4.2 as "complete") with reality: **no real inference runs and the Phase 4 framework is dormant library code, not wired into the app.**

It is **inference-first**, then agent wiring, then minimal tools/approval, then (additive) streaming. The long tail of tools and the audit/permissions UI are explicitly deferred (§9).

### Deep-detail docs (do NOT re-derive — read these)
- [`docs/plans/inference-engine-refactor-plan.md`](../../docs/plans/inference-engine-refactor-plan.md) — full inference refactor (Phases 0–5, risks, exact API facts about `InferenceEngine.Core`). This plan adopts it and **updates its Streaming decision (D3)** per §1 below.
- [`docs/plans/inference-engine-streaming-upstream-spec.md`](../../docs/plans/inference-engine-streaming-upstream-spec.md) — the upstream streaming contract this plan will swap onto in Stage 4.

---

## 1. Resolved decisions

| # | Decision | Choice | Notes |
|---|---|---|---|
| D1 | Inference stack | Adopt external **`InferenceEngine.Core`** (`intel-agency/inference-engine-lib`) | One-shot inference already works on this Debian/AMD-ROCm host in published `1.1.0-dev-13`. Replaces `OnnxRuntimeService`/DirectML probe + all simulated generation/embeddings. |
| D2 | SLM sequencing | **One-shot generation now; swap to streaming later (additive)** | See §6 rollout. Avoids blocking on the external streaming team. |
| D3 | Plan boundary | **Full critical path**, inference detailed first | Long tail of tools deferred (§9). |
| D4 | Tokenizer | `Microsoft.ML.Tokenizers` | Phi-3 + MiniLM `tokenizer.json`. Library stays pure tensor-in/out; tokenization is ours. |
| D5 | Models | **Two** ONNX models | MiniLM all-MiniLM-L6-v2 (embeddings, 384-dim) **and** Phi-3-mini-4k-instruct (generation). Today `IConfigurationService` exposes only one `GetModelPath()` — fix in Stage 1. |
| D6 | Routing | `ChatViewModel` → orchestrator (consumes RAG + SLM + tools); plain-RAG path retained as fallback | |
| D7 | Identity | Single local user; `userId` from `Environment.UserName` | No auth system. |
| D8 | Approval boundary | Core `IUserInteraction` interface, Avalonia impl, injected into `SecurityManager` | Keeps `SupportAssistant.Core` UI-free. |

### Verified code facts the executor must respect
- `App.axaml.cs:100-131` `ConfigureServices` registers **only** RAG services + ViewModels. `ToolRegistry`/`ISecurityManager`/`IAgentOrchestrator`/`ISLMService` are **not** registered.
- `ChatViewModel.cs:129-216` uses only `QueryProcessingService → ContextRetrievalService → ResponseGenerationService`.
- `ResponseGenerationService.cs:231-249` `GenerateWithModelAsync` **always** routes to `GenerateSimpleResponseAsync` (canned strings), even when an `InferenceSession` exists.
- `OnnxEmbeddingService.cs:198-213` `GenerateOnnxEmbeddingAsync` is a placeholder → hash fallback.
- `AgentOrchestrator.cs:59,468` `ProcessQueryAsync` uses `SimulateSLMResponseAsync`; only `ExecuteReActCycleAsync` honors a real `_slmService`.
- `SecurityManager.cs:59,80-92` approval simulated; `RestoreBackupAsync` returns `true` without restoring.
- `ToolRegistry.cs:261` `RegisterCoreTools()` body is all commented-out; `DiscoverAndRegisterTools()` (reflection) exists.
- `ReadFileContentsTool.cs` is a complete, real tool — the reference pattern for new tools.
- No `InferenceEngine.Core` reference exists in source/csproj today (only in the planning docs).
- `Directory.Build.props:27` `TreatWarningsAsErrors=true`; `SupportAssistant.Core.csproj:15-16` pins OnnxRuntime **1.19.2** (lib needs **1.24.1**).

---

## 2. Constraints & boundaries

- net10.0; `TreatWarningsAsErrors=true` (every stage must build 0-warning).
- App target: `WinExe`, MSIX, RIDs `win-x64;win-arm64`, `PublishSingleFile` + `IncludeNativeLibrariesForSelfExtract`. Dev host is **Debian + AMD GPU** (ROCm). Verify both the DirectML/Windows path and the ROCm/Linux dev path.
- `SupportAssistant.Core` must stay UI-free (no Avalonia refs) — cross-boundary concerns go through interfaces.
- Verification is non-mutating: `dotnet restore`, `dotnet build -c Release`, `dotnet test`. No model files committed (download/cache only).
- All new public types additive; existing tests stay green.

---

## 3. Stage 0 — Prerequisites & licensing  *(gate; do first)*

- **T0.1 AGPL re-license — GATING, needs owner sign-off + contributor consent.** `InferenceEngine.Core` is AGPL-3.0-or-later; SupportAssistant is currently MIT (`Directory.Build.props:50`). To distribute binaries that link it, re-license SupportAssistant to `AGPL-3.0-or-later`: update `PackageLicenseExpression`, `PackageRequireLicenseAcceptance` false→true, `Copyright`/`PackageTags`; add root `LICENSE` (AGPL-3.0) + `NOTICE` attributing the lib; update `README.md`. **If sign-off is denied, this entire approach is blocked — escalate before proceeding.**
- **T0.2 Restore the package.** Add `<PackageReference Include="InferenceEngine.Core" Version="1.1.0-dev-13" />` to `SupportAssistant.Core.csproj`; confirm `dotnet restore` works from nuget.org, else add GitHub Packages feed `https://nuget.pkg.github.com/intel-agency/index.json` (+ read PAT in CI secrets).
- **T0.3 OnnxRuntime 1.19.2 → 1.24.1.** Bump `Microsoft.ML.OnnxRuntime`; remove the direct `Microsoft.ML.OnnxRuntime.DirectML` ref (transitive on Windows RID via the lib). Build clean; confirm no 1.19→1.24 breakage in `InferenceSession`/`SessionOptions.AppendExecutionProvider_*`/`NamedOnnxValue`.
- **T0.4 Tokenizer assets.** Add `Microsoft.ML.Tokenizers`. Arrange load paths for Phi-3 + MiniLM `tokenizer.json` from app data dir (no commit of large model files).

---

## 4. Stage 1 — Real inference (embeddings + one-shot SLM)

> Detail lives in `inference-engine-refactor-plan.md` Phases 1–5; execute it, with the **Streaming decision updated to D2 (one-shot now)**. Tasks below are the ordered checkpoints.

- **T1.1 Project wiring.** Add `InferenceEngine.Core` + `Microsoft.ML.Tokenizers` refs (T0.2/T0.4 done). New folder `src/SupportAssistant.Core/Engines/`.
- **T1.2 Model split config.** Extend `IConfigurationService` / `DefaultConfigurationService` to expose **two** model identities/paths + `InferenceEngineOptions` mapping (GPU toggle → `UseGpuAcceleration`, `DeviceId`, `WarmupOnLoad`).
- **T1.3 `Engines/TextEmbeddingEngine : BaseInferenceEngine<string, float[]>`.** MiniLM model; `PreProcessWithContext` tokenizes → `input_ids`+`attention_mask`; `PostProcessWithContext` reads output (name from `OutputMetadata`), mean-pools (masked), L2-normalizes. Dimension from `OutputMetadata` (replace hardcoded 384).
- **T1.4 Refactor `OnnxEmbeddingService`** to delegate to the engine (`InitializeAsync`→`engine.LoadAsync()`; `GenerateEmbeddingAsync`→`engine.PredictAsync`). Keep the hash fallback **only** when the model is genuinely unavailable. `CalculateCosineSimilarity` unchanged.
- **T1.5 `Engines/Phi3GenerationEngine : BaseInferenceEngine<string, string>`.** Override `PredictAsync` with the autoregressive loop: tokenize prompt → **prefill** `_session.Run` (capture `logits` + KV cache) → **decode** loop (greedy default; virtual `SampleNextToken` for top-k/top-p/temp/repeat-penalty); grow `attention_mask`; carry `present_key_values`→`past_key_values`; stop on EOS or `MaxTokens`; detokenize → string.
  - **Discovery task (do first):** inspect the chosen Phi-3 ONNX export's `InputMetadata`/`OutputMetadata` at load for exact KV-cache I/O names, `use_cache_branch`, and whether prefill+decode share one graph. If no KV-cache graph, fall back to full-recompute-per-step (O(n²), fine for short contexts). *This is the main model-specific risk.*
- **T1.6 `OnnxSLMService : ISLMService`** (`Agent/ISLMService.cs`): holds a `Phi3GenerationEngine`; `IsAvailable` = engine loaded; `GenerateResponseAsync` = `engine.PredictAsync`. *(Do **not** add `StreamResponseAsync` yet — Stage 4.)*
- **T1.7 Wire `ResponseGenerationService`** to depend on `ISLMService` (new ctor param). `GenerateWithModelAsync` calls the real SLM; `GenerateSimpleResponseAsync` stays only as the catch-all fallback when the SLM is unavailable.
- **T1.8 DI (`App.axaml.cs:100-131`).** Register `TextEmbeddingEngine`, `Phi3GenerationEngine`, `OnnxSLMService` (as `ISLMService`); rewire `IEmbeddingService` to the embedding engine; remove `IOnnxRuntimeService` registration. Build `InferenceEngineOptions` from settings; use `OnSessionInitialized` to surface `InferenceEngineInfo` (provider, `IsFallback`, `FallbackReason`, `Checks`) on the `BackgroundTaskService` init path; Settings UI replaces `IsDirectMLAvailable()` text with the rich snapshot.
- **T1.9 Validation.** `dotnet build -c Release` 0 warnings; tests — `TextEmbeddingEngine` cosine (similar > dissimilar; dim == metadata last-dim), `Phi3GenerationEngine`/`OnnxSLMService` (one-shot output non-empty, EOS stops, `MaxTokens` respected, `CancellationToken` aborts); keep 134/134; manual smoke — real embedding search over the KB + real Phi-3 generation, both CPU and GPU, confirm no synthetic fallback fires unless the model is missing.

**Stage 1 exit = real embeddings + real (non-streaming) generation running end-to-end via the engine lib.**

---

## 5. Stage 2 — Wire the agent into the app  *(STATUS §4.B)*

- **T2.1 Implement `ToolRegistry.RegisterCoreTools()`** (or use `DiscoverAndRegisterTools()`) so the registry actually contains tools once Stage 3 lands. Until then it may be empty; don't register non-existent tools.
- **T2.2 Register agent services in DI (`App.axaml.cs`):** `IToolRegistry`→`ToolRegistry`, `ISecurityManager`→`SecurityManager`, `IAgentOrchestrator`→`AgentOrchestrator`. On construction, `orchestrator.RegisterSLMService(slmService)`.
- **T2.3 Make the orchestrator consume RAG context.** Currently `AgentOrchestrator.ProcessQueryAsync` builds its own prompt and ignores `ContextRetrievalService`. Inject `IContextRetrievalService` + `IQueryProcessingService` and fold retrieved KB context into `BuildInitialPrompt`, so tool-augmented answers are still grounded in the KB. *(Keep `ExecuteReActCycleAsync`'s existing real-SLM branch.)*
- **T2.4 Route `ChatViewModel` through the orchestrator.** Inject `IAgentOrchestrator` into `ChatViewModel`; in `SendMessageAsync` call `orchestrator.ProcessQueryAsync(userId, query, ct)` (userId per D7). Render `AgentResponse.ResponseText`; surface `ToolExecutions`/`Errors` in the message metadata. **Retain the plain RAG path** (`ResponseGenerationService`) as the fallback when the SLM is unavailable or the orchestrator errors. Use `CancellationTokenSource` bound to `CancelProcessingCommand`.
- **T2.5 Validation.** Build clean; add `ChatViewModel`→orchestrator test (mock orchestrator) verifying routing + fallback; full suite green.

**Stage 2 exit = a user message flows through the real agent loop (RAG + real SLM); tools still empty, so it behaves like grounded chat until Stage 3.**

---

## 6. Stage 3 — Minimal tool set + real Human-in-the-Loop approval  *(STATUS §4.C + sliver of §4.D)*

Goal: prove the full modifying path (approval → backup → execute → audit) end-to-end with a small, representative tool set.

- **T3.1 Tools (new, follow `ReadFileContentsTool.cs` pattern):**
  - `ListDirectory` (read-only, `ToolCategory.FileSystem`, `RequiresApproval=false`).
  - `GetSystemInfo` (read-only, `ToolCategory.System`).
  - `WriteFileContents` (modifying, `IsModifying=true`, `RequiresApproval=true`) — exercises the whole approval+backup path.
  - Register them in `ToolRegistry.RegisterCoreTools()`.
- **T3.2 Real HITL approval.** Replace `SecurityManager.SimulateUserApprovalAsync` (`SecurityManager.cs:227`). Define `IUserInteraction` in `SupportAssistant.Core` with `Task<ToolApprovalResult> RequestApprovalAsync(ITool tool, Dictionary<string,object> parameters, string preview, CancellationToken ct)`. Implement in the Avalonia app (modal dialog showing `tool.GetExecutionPreview(...)` + parameters). Inject `IUserInteraction` into `SecurityManager`. Honor "remember this decision" via the existing `_rememberedApprovals` map + `ValidityDuration`.
- **T3.3 Real backup + restore.** `SecurityManager.CreateBackupAsync` must actually copy the target file (for file-modifying tools) into a backup dir keyed by `BackupId`. `RestoreBackupAsync` must restore from that backup. (Registry backup/restore stays stubbed until registry tools exist.)
- **T3.4 Validation.** Build clean; tests — read-only tool runs without approval; `WriteFileContents` triggers the approval dialog path (mock `IUserInteraction` → approve writes file + creates backup; deny returns failure without writing); backup→modify→restore round-trips; audit trail (`GetAuditTrailAsync`) records each execution. Full suite green.

**Stage 3 exit = the agent can really act on the system with safe, audited, user-approved modifications.**

---

## 7. Stage 4 — Streaming swap (additive; when upstream ships)  *(inference-engine-streaming-upstream-spec.md)*

Triggered **only after** `IStreamingInferenceEngine<,,>` / `BaseStreamingInferenceEngine<,,>` lands upstream.

- **T4.1 Migrate** `Phi3GenerationEngine` from `BaseInferenceEngine<string,string>` to `BaseStreamingInferenceEngine<string,string,string>` (implement `BuildStepInputs`/`ProcessStepOutputs`/`BuildFinalOutput`; the one-shot `PredictAsync` is now routed through the stream by the base).
- **T4.2 Extend `ISLMService`** with `IAsyncEnumerable<string> StreamResponseAsync(string prompt, CancellationToken ct = default)`.
- **T4.3 Stream into the UI.** `ChatViewModel` consumes `StreamResponseAsync` (when settings `Ai.EnableStreaming` is on), appending tokens live; falls back to one-shot otherwise.
- **T4.4 Validation.** Parity invariant: `BuildFinalOutput(stream) == PredictAsync` for identical input/options; cancellation mid-stream leaves the instance clean; KV-cache correctness vs full-recompute; EOS/MaxSteps stop. No one-shot regression.

**Stage 4 exit = real streamed generation, non-breaking, parity-guaranteed.**

---

## 8. Data flow (target, after Stage 2)

```
ChatViewModel.SendMessageAsync(userMessage)
  → IAgentOrchestrator.ProcessQueryAsync(userId, query, ct)
      ├─ IQueryProcessingService.ProcessQueryAsync  (existing RAG)
      ├─ IContextRetrievalService.RetrieveContextAsync → KB context folded into prompt
      ├─ ISLMService.GenerateResponseAsync (OnnxSLMService → Phi3GenerationEngine)
      ├─ AgentOrchestrator.ParseToolCalls → ExecuteToolCallAsync per call
      │     ├─ ITool.ValidateParameters
      │     ├─ ISecurityManager.ValidateExecutionAsync
      │     ├─ (if modifying) RequestApprovalAsync → IUserInteraction dialog
      │     ├─ (if modifying) CreateBackupAsync
      │     ├─ ITool.ExecuteAsync
      │     └─ ISecurityManager.LogExecutionAsync
      └─ GenerateFollowUpResponseAsync (real SLM) → AgentResponse
  → ChatViewModel renders ResponseText + tool/audit metadata
Fallback: if SLM unavailable or orchestrator errors → plain RAG (ResponseGenerationService).
Embeddings: TextEmbeddingEngine (Stage 1) backs KnowledgeBaseService/FileVectorStorageService throughout.
```

---

## 9. Deferred (out of scope — separate plans)

- Phase 4.3+ tools beyond the Stage 3 minimal set: `CreateDirectory`/`DeleteFile`, INI/Registry/Environment, `GetRunningProcesses`/`GetNetworkConfiguration`/`GetEventLogEntries`, network diagnostics (`PingHost`/`TraceRoute`/`DNSLookup`/`PortScan`), system monitoring. Pattern is established by Stage 3; add incrementally.
- Audit-trail viewer + permissions-management UI (Phase 4.4/4.6).
- Penetration/security hardening pass (Phase 4.4 Week 11), execution sandboxing, system restore points.

---

## 10. Risks & open questions

| Risk / question | Mitigation / owner action |
|---|---|
| **AGPL re-license needs contributor consent** (T0.1, gating) | Owner sign-off before any Stage 0 merge. If denied, the engine-lib approach is blocked — escalate. |
| `InferenceEngine.Core` restore feed (nuget.org vs GitHub Packages) | T0.2 verify; add PAT to CI if needed. |
| OnnxRuntime 1.19.2→1.24.1 breakage under `TreatWarningsAsErrors` | T0.3 clean-build gate before feature work. |
| Phi-3 KV-cache I/O name/shape variance (prefill vs decode; `use_cache_branch`; `past/present_key_values` naming) | T1.5 discovery task; full-recompute-per-step fallback. |
| DirectML native assets in single-file publish (win-x64/arm64) | Verify native libs deploy; lib already gates DirectML on Windows RID. |
| First-run model download UX (Phi-3 is large) | `DownloadingModelProvider` cache + progress; honor pre-bundled `LocalModelProvider` for offline. |
| Streaming migration breaks one-shot | Stage 4 parity test gates it; additive/non-breaking by design. |

## 11. Assumptions stated for the executor

- AGPL re-license is the **one gating prerequisite needing owner sign-off** before Stage 0 merges.
- `userId` = `Environment.UserName` (single local user; no auth).
- Approval crosses Core/UI via `IUserInteraction` (Core interface, Avalonia impl) injected into `SecurityManager`.
- Embeddings (MiniLM, 384-dim) and generation (Phi-3) are **two** ONNX models with separate config paths.
- The plain-RAG path is always retained as fallback when the SLM is unavailable.
