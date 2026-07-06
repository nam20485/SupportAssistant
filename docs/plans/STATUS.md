# SupportAssistant — Current Project Status

**Last verified:** 2026-07-05 (against current source tree on `development`)
**Head commit:** `b758191` — "Merge pull request #7 from nam20485/dev/inference-first"
**Branch state:** `dev/inference-first` was merged into `development` via PR #7. `development` is **62 commits ahead of `master`**; the `development → master` merge is the outstanding integration step.

> This document supersedes the historical status reports in [`docs/.archived/`](./.archived/),
> several of which contained **overstated or false "complete" claims**. This file reflects what the
> source code actually does, verified by reading it. The 2026-07-03 snapshot (simulated inference,
> dormant Phase 4) is **superseded**: Stages 0–3 of the inference-first plan are now implemented.

---

## 1. How the application actually runs today

`src/SupportAssistant/App.axaml.cs` (`ConfigureServices`) now registers the **full agent pipeline**
on top of the RAG pipeline. The chat flow is:

`ChatViewModel` → (`IAgentOrchestrator.ProcessQueryAsync` → RAG context + `ISLMService` + tools) **or**
the plain-RAG fallback (`IResponseGenerationService`) when the orchestrator is absent or errors
(`ChatViewModel.cs:168-170`).

Inference is **real** and delegates to the external **`InferenceEngine.Core`** library:
`TextEmbeddingEngine` (MiniLM embeddings) and `TextGenerationEngine` (Phi-3 generation), both
constructed in DI (`App.axaml.cs:117-143`).

---

## 2. What is genuinely DONE (real, working source code)

| Area | Evidence | Notes |
|------|----------|-------|
| **Stage 0 — Prereqs / licensing** | `f09bd0a` AGPL re-license; `ab4c6c4` adopt `InferenceEngine.Core` + OnnxRuntime 1.24.1 + `Microsoft.ML.Tokenizers` | LICENSE/NOTICE updated; package ref added |
| **Stage 1 — Real inference (consume library engines)** | `App.axaml.cs:117-143`; `Engines/`; `26f7084` | Pivoted from "build app-side engines" to consuming the library's `TextEmbeddingEngine`/`TextGenerationEngine`. Tokenizer moved into the library (`ca4ca41`) |
| **Stage 2 — Agent wired into the app** | `App.axaml.cs:158-172`; `ChatViewModel.cs:168-170`; `0be852f` | `IToolRegistry`, `ISecurityManager`, `IAgentOrchestrator`, `ISLMService` all registered; orchestrator consumes RAG context; plain-RAG retained as fallback |
| **Stage 3.1 — Minimal tools** | `Tools/ListDirectoryTool.cs`, `GetSystemInfoTool.cs`, `WriteFileContentsTool.cs`, `FileSystem/ReadFileContentsTool.cs` | Registered via reflection (`ToolRegistry.DiscoverAndRegisterTools()`, `App.axaml.cs:161`). `RegisterCoreTools()` body is still commented out but unused |
| **Stage 3.2 — Real Human-in-the-Loop approval** | `Security/SecurityManager.cs:61-92`; `Security/AvaloniaUserInteraction.cs`; `App.axaml.cs:157,164` | `RequestApprovalAsync` uses the real Avalonia dialog via injected `IUserInteraction`. `SimulateUserApprovalAsync` is now only the fallback when no `IUserInteraction` is injected |
| **Stage 3.3 — Real backup + restore** | `SecurityManager.cs:152-187` (`RestoreBackupAsync` does `File.Copy`/`File.Delete`) | Restores originals from backup, or deletes files the tool created. Registry backup/restore still stubbed until registry tools exist |
| **RAG pipeline + embeddings** | `OnnxEmbeddingService` → `TextEmbeddingEngine` (real MiniLM) | Hash fallback retained **only** when the model is genuinely unavailable |
| **Avalonia UI / settings / onboarding / packaging / CI** | Views, ViewModels, scripts, `.github/workflows/` | Unchanged from prior verified state |

### Formerly-overstated claims — now RESOLVED
The 2026-07-03 STATUS flagged these as false/misleading; they are now backed by code:
- "SLM simulated / no Phi-3" → **resolved**: `OnnxSLMService` over `TextGenerationEngine`.
- "Embeddings are a placeholder" → **resolved**: real MiniLM via the library engine.
- "Agent/tools not in DI / dormant" → **resolved**: all registered; orchestrator drives the chat path.
- "Approval simulated / restore stubbed" → **resolved** (see Stage 3.2/3.3).

---

## 3. What is NOT done yet

### A. Stage 4 — Streaming (UI) — BLOCKED on an upstream library fix
- `OnnxSLMService` already calls `TextGenerationEngine.PredictStreamingAsync` but **buffers it to a one-shot string**; `ChatViewModel` renders the full response (no live token streaming). The `EnableStreaming` settings flag exists but is not consumed end-to-end.
- **Blocking dependency:** `InferenceEngine.Core`'s `TextGenerationEngine` has a decode-delta defect (library review finding **H3**) that corrupts/drops/duplicates streamed text. Stage 4 must wait until that fix ships upstream **and** SupportAssistant bumps the `InferenceEngine.Core` package ref. (The one-shot generation path already inherits H3 today.)
- Remaining work (post-unblock): expose `ISLMService.StreamResponseAsync`, make `ChatViewModel` consume the stream token-by-token (with one-shot fallback), add parity/cancellation tests.

### B. Merge `development → master`
- 62 commits of Stage 0–3 work are on `development` but **not on `master`** (the default/publish branch, `origin/HEAD`). Open the `development → master` PR.

### C. Minor leftovers
- `IOnnxRuntimeService` is still registered in DI (`App.axaml.cs:112`) although Stage 1 routes inference through the library; the plan's T1.8 called for removing it.
- `ToolRegistry.RegisterCoreTools()` body is still fully commented (`ToolRegistry.cs:261-268`); registration is done via `DiscoverAndRegisterTools()` instead. Cosmetic — pick one mechanism.

### D. Deferred (plan §9 — separate plans, not blocking)
- More tools: `CreateDirectory`/`DeleteFile`, INI/Registry/Environment, `GetRunningProcesses`/`GetNetworkConfiguration`/`GetEventLogEntries`, network diagnostics (`PingHost`/`TraceRoute`/`DNSLookup`/`PortScan`), system monitoring.
- Audit-trail viewer UI + permissions-management UI (Phase 4.4/4.6).
- Penetration / security-hardening pass, execution sandboxing, system restore points.

---

## 4. Cross-repository dependency

SupportAssistant **consumes** `InferenceEngine.Core` (`intel-agency/inference-engine-lib`) as a NuGet
package. The library's post-review fix plan lives at
[`docs/review-fix-plan.md` in that repo](https://github.com/intel-agency/inference-engine-lib/blob/development/docs/review-fix-plan.md)
(Phase 1: concurrency, timeouts, decode-delta, disposal). **Bump the package ref here once those
fixes are published** before finishing Stage 4.

---

## 5. Where to find implementation information

| Need | Document |
|------|----------|
| Active staged plan (Stages 0–4) | [`.kilo/plans/1783230906618-phase4-agent-inference-first-plan.md`](../../.kilo/plans/1783230906618-phase4-agent-inference-first-plan.md) |
| Inference refactor detail | [`docs/plans/inference-engine-refactor-plan.md`](./inference-engine-refactor-plan.md) |
| Streaming contract (Stage 4 target) | [`docs/plans/inference-engine-streaming-upstream-spec.md`](./inference-engine-streaming-upstream-spec.md) |
| Overall app spec & requirements | `issue_description.md` (root) |
| DI wiring (what's running) | `src/SupportAssistant/App.axaml.cs` (`ConfigureServices`) |
| Chat flow | `src/SupportAssistant/ViewModels/ChatViewModel.cs` |
| Inference engines | `src/SupportAssistant.Core/Engines/`, `Agent/OnnxSLMService.cs` |
| Tool/Agent/Security code | `src/SupportAssistant.Core/Tools/`, `Agent/`, `Security/` |

### Archived (historical status snapshots — read with skepticism)
`docs/.archived/` holds prior status docs whose "complete/production-ready" assertions about SLM
integration were **not** backed by code at the time. Kept for history only.

---

## 6. Bottom line

- **Actually running today:** a tool-augmented agent over **real** MiniLM embeddings + **real** Phi-3
  generation (via `InferenceEngine.Core`), with real Human-in-the-Loop approval and backup/restore —
  all wired into the chat path (Stages 0–3).
- **Known quality gap:** generation (one-shot today, streaming later) inherits the library's H3
  decode-delta defect until the upstream fix + a package bump land.
- **Next steps:** (B) merge `development → master`; (A) finish Stage 4 streaming after the library
  fix + package bump; (D) the deferred tools/UI/hardening tail.
