# SupportAssistant — Current Project Status

**Last verified:** 2026-07-10 (against `feat/inference-diagnostics` / WS1 work; Stages 0–3 on `development`)
**Head note:** WS1 (inference diagnostics) implemented on PR [#10](https://github.com/nam20485/SupportAssistant/pull/10). Stages 0–3 remain on `development`.
**Branch state:** `development` is ahead of `master` (default/publish branch); the `development → master` merge is still outstanding. Count drifts — re-check with `git rev-list --count master..development`.

> This document supersedes the historical status reports in [`docs/.archived/`](../.archived/),
> several of which contained **overstated or false "complete" claims**. This file reflects what the
> source code actually does, verified by reading it. The 2026-07-03 snapshot (simulated inference,
> dormant Phase 4) is **superseded**: Stages 0–3 of the inference-first plan are implemented.
> The 2026-07-05 "Stage 4 blocked on H3" note is also **superseded** (see §3.A).

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

### A. Stage 4 — Streaming (UI) — unblocked (consumer-only)
- `OnnxSLMService` already calls `TextGenerationEngine.PredictStreamingAsync` but **buffers it to a one-shot string**; `ChatViewModel` renders the full response (no live token streaming). The `EnableStreaming` settings flag exists but is not consumed end-to-end.
- **Upstream H3 decode-delta** (which previously blocked Stage 4) is **fixed** in `InferenceEngine.Core` **1.1.29** (see [`inference-engine-integration-status.md`](./inference-engine-integration-status.md) §B/G). Remaining work is SupportAssistant-only: expose `ISLMService.StreamResponseAsync`, make `ChatViewModel` consume tokens (with one-shot fallback), add parity/cancellation tests.

### B. Merge `development → master`
- Stage 0–3 work is on `development` but **not on `master`** (the default/publish branch, `origin/HEAD`). Open the `development → master` PR when ready.

### C. Minor leftovers
- `IOnnxRuntimeService` is still registered in DI (`App.axaml.cs`) although Stage 1 routes inference through the library; the plan's T1.8 called for removing it.
- `ToolRegistry.RegisterCoreTools()` body is still fully commented; registration is done via `DiscoverAndRegisterTools()` instead. Cosmetic — pick one mechanism.

### D. Deferred (plan §9 — separate plans, not blocking)
- More tools: `CreateDirectory`/`DeleteFile`, INI/Registry/Environment, `GetRunningProcesses`/`GetNetworkConfiguration`/`GetEventLogEntries`, network diagnostics (`PingHost`/`TraceRoute`/`DNSLookup`/`PortScan`), system monitoring.
- Audit-trail viewer UI + permissions-management UI (Phase 4.4/4.6).
- Penetration / security-hardening pass, execution sandboxing, system restore points.

### E. Done recently — WS1 diagnostics
- `IInferenceDiagnosticsService` / `EngineDiagnostics`, `OnSessionInitialized` wiring, and Settings UI surfacing of provider + fallback (PR #10). Details:
  [`inference-engine-integration-status.md`](./inference-engine-integration-status.md) §D,
  [`ws1-inference-diagnostics-development-plan.md`](./ws1-inference-diagnostics-development-plan.md).

---

## 4. Cross-repository dependency

SupportAssistant **consumes** `InferenceEngine.Core` (`intel-agency/inference-engine-lib`) as a NuGet
package from the private GitHub Packages feed (`nuget.config` + `NUGET_AUTH_TOKEN`). Library review
fixes (including H3) are in the **1.1.29** package currently referenced. Further bumps follow
upstream releases as needed for Stage 4 polish.

---

## 5. Where to find implementation information

| Need | Document |
|------|----------|
| Active remaining workstreams | [`inference-integration-implementation-plan.md`](./inference-integration-implementation-plan.md) |
| Inference integration (committed vs remaining) | [`inference-engine-integration-status.md`](./inference-engine-integration-status.md) |
| Historical inference refactor plan | [`inference-engine-refactor-plan.md`](./inference-engine-refactor-plan.md) (partially superseded — see banner) |
| Streaming contract (Stage 4 target) | [`inference-engine-streaming-upstream-spec.md`](./inference-engine-streaming-upstream-spec.md) |
| Overall app spec & requirements | [`docs/ai-new-app-template.md`](../ai-new-app-template.md) |
| Contributor / agent guide | [`AGENTS.md`](../../AGENTS.md) |
| DI wiring (what's running) | `src/SupportAssistant/App.axaml.cs` (`ConfigureServices`) |
| Chat flow | `src/SupportAssistant/ViewModels/ChatViewModel.cs` |
| Inference engines | `src/SupportAssistant.Core/Engines/`, `Agent/OnnxSLMService.cs` |
| Tool/Agent/Security code | `src/SupportAssistant.Core/Tools/`, `Agent/`, `Security/` |

### Archived (historical — read with skepticism)
`docs/.archived/` holds prior status docs and web-app instruction modules. Kept for history only.
Original Phase 4 checklist: [`docs/.archived/plans/PHASE_4_IMPLEMENTATION_PLAN.md`](../.archived/plans/PHASE_4_IMPLEMENTATION_PLAN.md).

---

## 6. Bottom line

- **Actually running today:** a tool-augmented agent over **real** MiniLM embeddings + **real** Phi-3
  generation (via `InferenceEngine.Core`), with real Human-in-the-Loop approval and backup/restore —
  all wired into the chat path (Stages 0–3), plus **WS1** inference diagnostics in Settings.
- **Next steps:** (B) merge `development → master`; (A) finish Stage 4 streaming (unblocked);
  (C) minor DI/tool-registration cleanups; (D) deferred tools/UI/hardening tail.
