# SupportAssistant — Current Project Status

**Last verified:** 2026-07-03 (against current source tree + `dotnet build`/`dotnet test`)
**Head commit:** 8749562 — "feat: add dark/light theme support across views and converters"
**Build:** Clean — `0 Warning(s) 0 Error(s)` (Release)
**Tests:** **134/134 passing (100%)** on .NET 10

> This document supersedes the historical status reports now in [`docs/.archived/`](./.archived/),
> several of which contained **overstated or false "complete" claims**. This file reflects what the
> source code actually does, verified by reading it and running the build/tests.

---

## 1. How the application actually runs today

`src/SupportAssistant/App.axaml.cs` (DI registration, `ConfigureServices`) wires up **only** the
Phase 1–3 RAG pipeline + UI. The **entire Phase 4 agent/tool system is NOT registered in DI and is
NOT used by any running code path.** The chat flow is:

`ChatViewModel` → `QueryProcessingService` → `ContextRetrievalService` → `ResponseGenerationService`

---

## 2. What is genuinely DONE (real, working source code)

| Area | Evidence | Notes |
|------|----------|-------|
| **Project structure / .NET 10** | `SupportAssistant.sln`, 3 projects, `Directory.Build.props`, `global.json` | Migrated to net10.0 SDK 10.0.301 |
| **Avalonia UI + MVVM** | `Views/`, `ViewModels/`, `Converters/`, `ViewLocator` | MainWindow, ChatView, SettingsView, OnboardingWizard, AboutWindow all real |
| **Settings / Onboarding** | `SettingsService`, `OnboardingWizardViewModel`, first-run flow in `App.axaml.cs` | Real persistence + wizard |
| **ONNX Runtime initialization** | `OnnxRuntimeService.cs` + packages `Microsoft.ML.OnnxRuntime`/`.DirectML` 1.19.2 | Detects providers + DirectML, builds `SessionOptions`. **Only env init — no inference yet** |
| **Knowledge base ingestion / chunking** | `KnowledgeBaseService`, `TextChunkingService`, `FileVectorStorageService` | Real chunking + file-based vector store + cosine similarity search |
| **RAG orchestration** | `QueryProcessingService`, `ContextRetrievalService`, `ResponseGenerationService` | Pipeline Query→Embed→Search→Prompt→"Generate" is wired end-to-end |
| **Tool framework (Phase 4.1 code)** | `Tools/ITool.cs`, `ToolRegistry.cs`, `Security/ISecurityManager.cs`, `Tools/FileSystem/ReadFileContentsTool.cs`, `Agent/IAgentOrchestrator.cs` | Real, compilable, self-contained classes (see §4 for caveats) |
| **Packaging / CI** | `scripts/build-distribution.ps1`, `scripts/build-simple.bat`, `.github/workflows/build-and-package.yml`, `Package.appxmanifest` | Scripts + workflow + MSIX manifest are real |
| **Test suite** | `SupportAssistant.Tests/` — **134 pass / 0 fail** | Accessibility, performance, error-scenario, ChatVM, RAG service tests |

---

## 3. FALSE / OVERSTATED claims (verified against code)

These claims appear in the archived docs but are **not** supported by the source:

1. **"Phase 4.2 SLM Integration — COMPLETE / 100% COMPLETE / production-ready"** ❌ FALSE.
   `AgentOrchestrator.ExecuteReActCycleAsync` / `ProcessQueryAsync` call
   `SimulateSLMResponseAsync` (`Agent/IAgentOrchestrator.cs:623`):
   *"This is a placeholder for actual SLM integration … would call the ONNX Runtime with the Phi-3 model."*
   There is **no real SLM/ONNX call** behind it.

2. **"Complete RAG Pipeline: Local AI with ONNX Runtime + DirectML acceleration"** ⚠️ MISLEADING.
   Runtime/DirectML *initialization* is real; *model inference is not* (see next point).

3. **"SLM inference pipeline with streaming support" / "Phi-3 model integration"** ❌ FALSE.
   `ResponseGenerationService.GenerateWithModelAsync` (`ResponseGenerationService.cs:231`) **always**
   routes to `GenerateSimpleResponseAsync` (`:251`) — canned keyword-matched strings + `Task.Delay(100)`.
   Comment at `:241`: *"For now, use a simplified generation approach. In a full implementation,
   this would tokenize the prompt and run inference."* **No tokenizer, no streaming, no Phi-3 generation.**

4. **Embeddings are real** ❌ MISLEADING.
   `OnnxEmbeddingService.GenerateOnnxEmbeddingAsync` (`OnnxEmbeddingService.cs:198`) is a placeholder →
   falls back to `GenerateFallbackEmbeddingAsync` (deterministic hash-based vectors). Vector search runs,
   but on synthetic embeddings.

5. **"Framework Completeness 95–100%" (Phase 4.1)** ⚠️ OVERSTATED. Approval is simulated
   (`SecurityManager.SimulateUserApprovalAsync`, `ISecurityManager.cs:545`: *"placeholder … would show approval UI"*),
   `RestoreBackupAsync` (`:398`) returns `true` without restoring (*"would restore files/registry"*),
   and `ToolRegistry.RegisterCoreTools()` (`:261`) has every registration commented out.

6. **Agent/tools are "integrated" / "ready for production use"** ❌ FALSE.
   `AgentOrchestrator`, `ISecurityManager`, `ToolRegistry`, `ISLMService` are **not registered in DI**
   (`App.axaml.cs:97`) and **not referenced by `ChatViewModel`**. They are dormant library code.

7. **"93.2% pass rate (124/133)"** ⚠️ STALE. Current run is **134/134 (100%)** after the .NET 10 +
   FluentAssertions 8.x migration. (Stale, not false — reality is better.)

---

## 4. What NEEDS IMPLEMENTATION (re-starting point)

Grouped by priority. Items in **bold** are prerequisites that unblock the most.

### A. Real SLM inference (Phase 2 core gap) — highest priority
- **Tokenizer** for Phi-3 (e.g. Tiktoken/HuggingFace tokenizer) — none exists today.
- **Real ONNX inference loop** in `ResponseGenerationService.GenerateWithModelAsync` (tokenize →
  `InferenceSession.Run` → sample → detokenize). Currently a no-op wrapper.
- **Streaming** response emission (claimed but absent).
- **Real embeddings**: implement `OnnxEmbeddingService.GenerateOnnxEmbeddingAsync` (currently fallback).

### B. Wire Phase 4 into the application
- Register `ToolRegistry`, `ISecurityManager`, `IAgentOrchestrator`, `ISLMService` in
  `App.axaml.cs ConfigureServices`.
- Connect the SLM: implement `ISLMService` (a thin adapter over the real ONNX inference from §A) and
  call `orchestrator.RegisterSLMService(...)`.
- Route `ChatViewModel` through the orchestrator (tool-augmented path) instead of only the plain RAG path.

### C. Finish Phase 4.1 framework gaps
- `SecurityManager.RequestApprovalAsync` → real **Human-in-the-Loop approval UI** (dialogs) — replace
  `SimulateUserApprovalAsync`.
- `RestoreBackupAsync` → actually restore files/registry from backup.
- `ToolRegistry.RegisterCoreTools()` → uncomment/implement once tools exist.

### D. Phase 4.3+ — the actual tools (none implemented yet)
- File tools (WriteFile, ListDirectory, CreateDirectory, DeleteFile).
- Config tools (INI, Registry, Environment).
- System-info tools (GetSystemInfo, processes, network, event log).
- Network/diagnostic tools (Ping, TraceRoute, DNS, ports).
- Audit-trail viewer + permission-settings UI.

> See [`docs/PHASE_4_IMPLEMENTATION_PLAN.md`](./PHASE_4_IMPLEMENTATION_PLAN.md) for the full week-by-week
> Phase 4 roadmap (still the authoritative plan for the work above). Treat its "Phase 4.1/4.2 complete"
> framing as **framework-only**; the real integration/inference work (this section) remains.

---

## 5. Where to find implementation information

| Need | Document |
|------|----------|
| Overall app spec & requirements | [`docs/ai-new-app-template.md`](./ai-new-app-template.md) |
| Original phased implementation plan (the issue) | [`issue_description.md`](../issue_description.md) (root) |
| Detailed Phase 4 roadmap (tools/agent/security) | [`docs/PHASE_4_IMPLEMENTATION_PLAN.md`](./PHASE_4_IMPLEMENTATION_PLAN.md) |
| Architecture rationale (on-device vs cloud, RAG, Phi-3) | [`docs/Architecting AI for Open-Source Windows Applications.md`](./Architecting%20AI%20for%20Open-Source%20Windows%20Applications.md) |
| Build & packaging how-to | [`docs/PACKAGING.md`](./PACKAGING.md) |
| Implementation tips / plan notes | [`docs/ImplementationTips.txt`](./ImplementationTips.txt), [`docs/ImplementationPlan.txt`](./ImplementationPlan.txt) |
| RAG pipeline code | `src/SupportAssistant.Core/Services/` (`ResponseGenerationService`, `ContextRetrievalService`, `QueryProcessingService`, `OnnxEmbeddingService`) |
| Tool/Agent/Security code | `src/SupportAssistant.Core/Tools/`, `src/SupportAssistant.Core/Agent/`, `src/SupportAssistant.Core/Security/` |
| DI wiring (what's actually running) | `src/SupportAssistant/App.axaml.cs` (`ConfigureServices`) |
| Chat flow | `src/SupportAssistant/ViewModels/ChatViewModel.cs` |

### Archived (historical status snapshots — read with skepticism)
Moved to [`docs/.archived/`](./.archived/): `PROJECT_STATUS_ALPHA_v0.1.0.md`,
`PHASE_3_PACKAGING_STATUS.md`, `PHASE_4_1_ARCHITECTURE_COMPLETE.md`,
`PHASE_4_2_SLM_INTEGRATION_COMPLETE.md`, `PHASE_4_2_STATUS_COMPLETE.md`,
`ENVIRONMENT_TRANSITION_SUMMARY.md`. Kept for history; their "complete/production-ready"
assertions about SLM integration are **not** backed by code.

---

## 6. Bottom line

- **Actually shippable today:** a working Avalonia desktop shell with a RAG *pipeline* whose answer
  generation and embeddings are **simulated** (no real AI model runs).
- **Phase 4.1/4.2** exist as **real but dormant framework code** with simulated SLM, simulated
  approval, and stubbed backup-restore — and are **not connected to the app**.
- **The true re-starting point is §4.A:** implement real ONNX/Phi-3 inference + tokenizer, then wire
  the agent system into the running application.
