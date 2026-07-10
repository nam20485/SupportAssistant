# InferenceEngine.Core Integration — Committed vs. Remaining

**Last verified:** 2026-07-10 (WS1 diagnostics committed; Stages 0–3 on `development`)
**Library consumed:** `InferenceEngine.Core` **1.1.29** (bumped `57caa3a`, 2026-07-07, from `1.1.0-dev.18`)
**Upstream guide cross-checked:** [`consumer-integration-guide.md`](https://github.com/intel-agency/inference-engine-lib/blob/main/docs/consumer-integration-guide.md) (intel-agency/inference-engine-lib)

> **What "we" means here.** SupportAssistant is the **consumer**. `InferenceEngine.Core` is the
> external library (`intel-agency/inference-engine-lib`, published as the `InferenceEngine.Core`
> NuGet package). This doc answers: *for each API surface the library exposes, what has SupportAssistant
> committed, and what remains?*
>
> **This supersedes the Stage-4 blocker note in older `STATUS.md` snapshots** (verified 2026-07-05 at
> `b758191`, *before* the `1.1.29` bump): the library's H3 decode-delta defect that blocked UI
> streaming **is fixed upstream and is in the package we now consume.**

---

## 0. Bottom line

| Surface | Library exposes? | SupportAssistant committed? | Remaining |
|---|---|---|---|
| **A. One-shot text engines** (generation + embedding) | ✅ `Text/` | ✅ Done — wired in DI, real inference | Minor cleanup only |
| **B. Streaming text generation** | ✅ `BaseStreamingInferenceEngine` | 🟡 Partial — engine streams internally, but the stream is **buffered to a one-shot string**; the UI never sees tokens | Expose streaming end-to-end to the UI (Stage 4 / WS2) |
| **C. Out-of-process engine** | ✅ `OutOfProcessBaseInferenceEngine` + `Worker/` | ❌ Not used (no reference, no `--worker` entry point) | Opt-in wiring only if/when Linux/ROCm OOP is required |
| **Diagnostics API** (`InferenceEngineInfo`, `Checks`, `OnSessionInitialized`) | ✅ `Diagnostics/` | ✅ Done (WS1) — `IInferenceDiagnosticsService` + Settings UI (PR #10) | Optional: retire ad-hoc DirectML probe (WS3) |
| **Agent + tools + HITL** (SupportAssistant-owned, not library) | n/a | ✅ Stage 3 done (minimal tools) | More tools, audit/permissions UI, hardening |

---

## A. One-shot text inference engines — ✅ COMMITTED

The library ships two ready-made text engines in the `InferenceEngine.Core.Text` namespace, and
SupportAssistant constructs both in DI and routes real inference through them.

- **Embedding** — `TextEmbeddingEngine : BaseInferenceEngine<string, float[]>` (all-MiniLM-L6-v2).
  - `OnnxEmbeddingService.GenerateEmbeddingAsync` calls `_engine.PredictAsync(text)`
    ([`Services/OnnxEmbeddingService.cs:45`](../../src/SupportAssistant.Core/Services/OnnxEmbeddingService.cs)).
  - Hash fallback retained **only** when the model is genuinely unavailable.
- **Generation** — `TextGenerationEngine : BaseStreamingInferenceEngine<string, string, string>` (Phi-3).
  - `OnnxSLMService` wraps it ([`Agent/OnnxSLMService.cs`](../../src/SupportAssistant.Core/Agent/OnnxSLMService.cs)).
  - Constructed in DI ([`App.axaml.cs:117-128`](../../src/SupportAssistant/App.axaml.cs)) via
    `InferenceOptionsFactory.Create` ([`Engines/InferenceOptionsFactory.cs`](../../src/SupportAssistant.Core/Engines/InferenceOptionsFactory.cs)).

**Status:** real, working, in the chat path. No remaining work beyond the minor cleanups in §E.

---

## B. Streaming text generation — 🟡 PARTIAL (Stage 4 open)

The streaming **engine** is consumed, but the stream is **collapsed to a one-shot string** before it
reaches the UI, so the user sees no live token rendering.

**What is committed:**
- `OnnxSLMService.GenerateResponseAsync` drives `TextGenerationEngine.PredictStreamingAsync(...)` and
  `StringBuilder.Append`s each chunk into one string
  ([`OnnxSLMService.cs:48-54`](../../src/SupportAssistant.Core/Agent/OnnxSLMService.cs)). Streaming is
  used *under the hood* so mid-generation cancellation is honored.
- `ChatViewModel.SendMessageAsync` awaits the full string, then adds a single assistant `ChatMessage`
  ([`ChatViewModel.cs:206-233`](../../src/SupportAssistant/ViewModels/ChatViewModel.cs)).

**What is missing (Stage 4):**
- `ISLMService` has **no streaming method** — only `GenerateResponseAsync(...)`
  ([`Agent/ISLMService.cs:19-30`](../../src/SupportAssistant.Core/Agent/ISLMService.cs)). Add e.g.
  `IAsyncEnumerable<string> StreamResponseAsync(...)`.
- `ChatViewModel` does not consume a token stream (no incremental `ChatMessage` updates).
- The `EnableStreaming` settings flag exists but is **not consumed** end-to-end.

**The blocker is GONE.** `STATUS.md` (2026-07-05) recorded Stage 4 as blocked on the library's **H3
decode-delta** defect (streamed chunks could be wrong/duplicated/dropped). That is now resolved:
- The library fix landed (`2f89b64 fix: post-review Phase 1 (... decode-delta ...)`), and
- `TextGenerationEngine.ProcessStepOutputs` now uses a one-step-delayed longest-common-prefix delta
  decode (`AdvanceDecodeState` / `EmittedLen` / `confirmed`), not the old naive
  `Substring(gen.DecodedLength)` (`InferenceEngine.Core/Text/TextGenerationEngine.cs:159-318`).
- SupportAssistant bumped to `InferenceEngine.Core 1.1.29` (`57caa3a`, 2026-07-07), which is
  **post-fix**.

**Remaining work (consumer-only, now unblocked):**
1. Add `ISLMService.StreamResponseAsync` (with a one-shot fallback).
2. Make `OnnxSLMService` expose the `PredictStreamingAsync` stream directly (it already calls it).
3. Make `ChatViewModel` render tokens incrementally (create the assistant message, append chunks).
4. Add parity (`string.Concat(stream) == one-shot`) + cancellation tests.

---

## C. Out-of-process engine — ❌ NOT USED (intentional / opt-in)

The library's escape hatch for the Linux/ROCm Mesa↔comgr LLVM collision is fully present
(`OutOfProcessBaseInferenceEngine`, `OutOfProcessEngineOptions`, `Worker/InferenceWorkerHost`,
`Worker/WorkerProtocol`), but SupportAssistant does **not** consume it.

**Evidence:** `rg 'OutOfProcess|InferenceWorkerHost|WorkerExecutable' src/` → no matches. `Program.cs`
has no `--worker` branch ([`Program.cs:14-28`](../../src/SupportAssistant/Program.cs)).

**Why this is fine for now:** SupportAssistant targets **Windows only**
(`RuntimeIdentifiers = win-x64;win-arm64`, [`SupportAssistant.csproj:22`](../../src/SupportAssistant/SupportAssistant.csproj)),
where DirectML is in-process and the ROCm/GL collision cannot occur. The upstream guide itself marks
the out-of-process path as opt-in ("only route through the worker when the collision is actually
possible").

**Remaining work (only if Linux/ROCm is added later):**
- Add a `--worker inference` entry point in `Program.cs` calling `InferenceWorkerHost.RunAsync`.
- Add a `NeedsOutOfProcess()` opt-in guard and an `OutOfProcessBaseInferenceEngine` subclass.
- Decide on a serializer (default `SystemTextJsonWorkerSerializer` is fine for `string`↔`string`).

---

## D. Diagnostics API — ✅ COMMITTED (WS1)

The library's diagnostics (`InferenceEngineInfo`: `Provider`, `IsFallback`, `FallbackReason`, the
ordered `Checks` trail, and the `OnSessionInitialized` callback) are **wired** into SupportAssistant
via `IInferenceDiagnosticsService` / `EngineDiagnostics`, attached in `InferenceOptionsFactory`, and
surfaced in the AI Settings UI (PR #10, 2026-07-10).

**Evidence:**
- `src/SupportAssistant.Core/Services/InferenceDiagnosticsService.cs`
- `src/SupportAssistant.Core/Engines/InferenceOptionsFactory.cs` (`OnSessionInitialized`)
- `src/SupportAssistant/ViewModels/SettingsViewModel.cs` + Settings view bindings
- Plan: [`ws1-inference-diagnostics-development-plan.md`](./ws1-inference-diagnostics-development-plan.md)

**Remaining (optional / WS3):**
- Retire the ad-hoc DirectML probe once Settings always shows library-reported provider/fallback.

---

## E. Tools, agent, and HITL — ✅ STAGE 3 DONE (tail deferred)

These are **SupportAssistant-owned**, not library features, but are listed because the question asked
for "tools/agents/other things."

**Committed:**
- **Agent** — `AgentOrchestrator` is wired into the chat path with RAG grounding and the real SLM
  (with a simulation fallback). Single-shot `ProcessQueryAsync` is the main path; a multi-step
  `ExecuteReActCycleAsync` exists ([`Agent/AgentOrchestrator.cs`](../../src/SupportAssistant.Core/Agent/AgentOrchestrator.cs)).
- **Tools** — 4 minimal tools, auto-discovered via reflection
  (`ToolRegistry.DiscoverAndRegisterTools()`):
  `ReadFileContentsTool`, `ListDirectoryTool`, `GetSystemInfoTool`, `WriteFileContentsTool`
  ([`Tools/`](../../src/SupportAssistant.Core/Tools/)).
- **HITL + backup/restore** — `SecurityManager` uses the real Avalonia approval dialog
  (`AvaloniaUserInteraction`) and performs real `File.Copy`/`File.Delete` restore
  ([`Security/SecurityManager.cs`](../../src/SupportAssistant.Core/Security/SecurityManager.cs)).

**Remaining (deferred per plan §9):**
- More tools: `CreateDirectory`/`DeleteFile`, INI/Registry/Environment, `GetRunningProcesses`,
  network diagnostics (`PingHost`/`TraceRoute`/`DNSLookup`/`PortScan`), system monitoring.
- Audit-trail viewer UI + permissions-management UI.
- Security-hardening pass, execution sandboxing, system restore points.

**Minor leftovers already flagged in `STATUS.md`:**
- `IOnnxRuntimeService` is still registered in DI (`App.axaml.cs:112`) though Stage 1 routes inference
  through the library; plan T1.8 called for removing it.
- `ToolRegistry.RegisterCoreTools()` body is fully commented out; registration uses
  `DiscoverAndRegisterTools()` instead — pick one mechanism.

---

## F. Merge step still outstanding

62+ commits of Stage 0–3 work sit on `development` but **not** on `master` (the default/publish
branch). The `development → master` PR is the outstanding integration step (independent of the
library work above).

---

## G. Library-side status (for reference)

Verified against the local clone `intel-agency/inference-engine-lib` @ `development` (`7414b5f`).
**All** surfaces the consumer guide describes are implemented in source:

| Surface | Source | Notes |
|---|---|---|
| One-shot base + contract | `BaseInferenceEngine.cs`, `IInferenceEngine.cs` | Original `LoadAsync`/`PredictAsync` lifecycle |
| Streaming base + contract + options | `BaseStreamingInferenceEngine.cs`, `IStreamingInferenceEngine.cs`, `StreamingOptions.cs` | Prefill→decode loop, KV-cache carry, single-in-flight guard |
| Out-of-process | `OutOfProcessBaseInferenceEngine.cs`, `OutOfProcessEngineOptions.cs`, `Worker/` | JSON-pipe IPC, host proxy mirrors worker `EngineInfo` |
| Text engines | `Text/TextGenerationEngine.cs`, `Text/TextEmbeddingEngine.cs`, `Text/TokenizerAssets.cs` | Phi-3 generation (streaming), MiniLM embedding (one-shot) |
| Diagnostics | `Diagnostics/` (`InferenceEngineInfo`, `InferenceProvider`, `InferenceFallbackReason`, `ProviderDiagnosticCheck`, `CheckResult`, `HostPlatform`) | EP trail + fallback reasons |

**Review-fix-plan progress** ([`docs/review-fix-plan.md`](https://github.com/intel-agency/inference-engine-lib/blob/development/docs/review-fix-plan.md)):
Phase 1 (H1–H5, M1–M7, T1–T6) has landed — commits `2f89b64` (post-review Phase 1) → PR #21 `702efec`
→ PR #22 `15acf63` → PR #23 `0d5dcdd` → PR #24 `fca2a80`. In particular:
- **H3 decode-delta** — fixed (one-step-delayed LCP delta decode; confirmed in `TextGenerationEngine.cs:159-318`).
- H1/H2/M6 (OOP concurrency, timeouts, disposal), H4 (KV-carry disposal), H5 (`LoadAsync` lock),
  M3 (`SamplingParams`), M7 (`MaxFrameBytes`) — addressed in the review PRs.

> SupportAssistant's `STATUS.md` (2026-07-05) still lists H3 as the Stage-4 blocker. That is now
> **stale**: the fix is upstream and the package we consume (`1.1.29`) is post-fix. Stage 4 is a
> consumer-only task today.

---

## H. Recommended next steps (priority order)

1. **Finish Stage 4 streaming** (§B / WS2) — blocker removed; expose `ISLMService.StreamResponseAsync` and
   render tokens in `ChatViewModel`, with a one-shot fallback + tests.
2. **Merge `development → master`** (§F / WS6).
3. **Minor cleanups** (§E / WS3) — drop vestigial `IOnnxRuntimeService`, pick one tool-registration path,
   optionally retire the ad-hoc DirectML probe.
4. **Deferred tail** (§E) — more tools, audit/permissions UI, hardening.
5. *(Conditional)* **Out-of-process** (§C) — only if/when a Linux/ROCm OOP target is required.

---

*Verification method: read SupportAssistant source under
`src/SupportAssistant.Core/{Engines,Agent,Tools,Services,Security}` and
`src/SupportAssistant/{Program.cs,App.axaml.cs,ViewModels/}`, plus git history and the WS1 plan.
Older `STATUS.md` claims are not trusted without re-checking code.*
