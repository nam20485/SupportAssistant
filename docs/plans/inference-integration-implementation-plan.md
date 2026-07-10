# Inference Integration — Implementation Plan for Remaining Items

**Status:** Draft for implementation · execution tracked in §1 (Status column) + the Progress log below
**Date:** 2026-07-08
**Branch:** implement off `development`; open PRs against `development`
**Companion doc:** [`inference-engine-integration-status.md`](./inference-engine-integration-status.md) (what is committed vs. remaining)
**Library consumed:** `InferenceEngine.Core` **1.1.29** (post-fix; the H3 decode-delta blocker is resolved)

This plan turns the "Remaining" columns of the status doc into ordered, concrete, file-level work. It is
scoped to **SupportAssistant (the consumer)**; the library already exposes every surface we need.

---

## 0. Guiding principles

1. **Additive, with a one-shot fallback on every streaming path.** If streaming or a tool errors, the
   user still gets a full response (the existing `ChatViewModel` plain-RAG/one-shot fallback stays).
2. **Never regress the chat path.** Every workstream keeps `dotnet build` 0-warning/0-error under
   `TreatWarningsAsErrors=true` and the existing `SupportAssistant.Tests` suite green.
3. **Mirror the library's contract, don't reinvent it.** We consume `InferenceEngine.Core` types
   directly; tokenization/decode stays in the library. We do not write app-side ONNX/decode loops.
4. **Feature-flag the new UX.** `AiSettings.EnableStreaming` already exists and is bound in the UI —
   consume it end-to-end; default on, with a one-shot fallback when off.
5. **Keep app/UI types decoupled from library types at the UI edge** by introducing a small immutable
   diagnostics snapshot DTO in `SupportAssistant.Core` (the app layer binds to the DTO, not the
   library struct).

---

## 1. Workstream overview & sequencing

Status legend: ⬜ not started · 🟡 in progress · ✅ merged · ⏸️ blocked/deferred/conditional · ➖ n/a

| # | Workstream | Status | Priority | Depends on | Approx. size |
|---|---|---|---|---|---|
| **WS1** | Surface inference diagnostics | ✅ implemented (PR #10) | High | — | Small–Medium |
| **WS2** | Stage 4 — live streaming to the UI | ⬜ not started | High | WS1 | Medium |
| **WS3** | Minor cleanups (vestigial DI, tool registration) | ⬜ not started | Medium | WS1 | Small |
| **WS4** | Deferred tail (more tools, audit/permissions UI, hardening) | ⏸️ backlog | Low | — | Large / ongoing |
| **WS5** | Out-of-process engine wiring | ⏸️ conditional (Linux/ROCm) | Low | Linux/ROCm target added | Medium |
| **WS6** | Merge `development → master` | ⬜ not started | Process | WS1–WS3 land | — |

**Recommended order:** WS1 → WS2 → WS3, then open the WS6 merge. WS4 is an ongoing backlog; WS5 is
gated on a product decision (Linux/ROCm support) and stays out of scope until then.

---

## Progress log

Reverse-chronological. When a task/PR lands: flip its `[ ]` → `[x]`, set the §1 Status column, and
prepend a dated entry here (include the PR # and key commits).

- **2026-07-10** — WS1 (Surface inference diagnostics) implemented. Branch
  `feat/inference-diagnostics` (4 commits: `8a986d5`, `5316af0`, `e697087`, `ca3b851`) off
  `development` (`daa6a95`); PR [#10](https://github.com/nam20485/SupportAssistant/pull/10) opened. Shipped `EngineDiagnostics` DTO + `IInferenceDiagnosticsService`
  (Core), wired `OnSessionInitialized` into both engines via `InferenceOptionsFactory.Create`,
  registered the service in DI, and surfaced a read-only "Acceleration Status" block in Settings
  (provider per engine + CPU-fallback notice, UI-thread-marshaled). `dotnet build` 0 warnings,
  `dotnet test` 176 green. PR opened against `development`. **Scope held to additive-only** — the
  `BackgroundTaskService` DirectML-probe refactor + `IOnnxRuntimeService` removal stay deferred to
  WS3 per the ws-plan scope decision. One deviation: also updated
  `MainWindowViewModelResolutionTests.BuildProvider` to mirror the new DI registration.
- **2026-07-08** — Plan authored; all workstreams **not started** (⬜). WS1 development plan ready at
  [`ws1-inference-diagnostics-development-plan.md`](./ws1-inference-diagnostics-development-plan.md),
  awaiting go-ahead to branch `feat/inference-diagnostics`.

---

## 2. WS1 — Surface inference diagnostics (High)

**Goal:** Tell the user *which* execution provider actually loaded and *why* a fallback occurred,
using the library's `InferenceEngineInfo` (`Provider`, `IsFallback`, `FallbackReason`, ordered
`Checks`), instead of the ad-hoc `IOnnxRuntimeService` DirectML probe.

### 2.1 Tasks

- [x] **T1.1 — Diagnostics snapshot DTO + service (Core).**
  Add `src/SupportAssistant.Core/Models/EngineDiagnostics.cs`:
  ```csharp
  public sealed record EngineDiagnostics(
      string EngineKind,          // "Embedding" | "Generation"
      string Provider,            // MIGraphX / DirectML / CoreML / Cpu
      bool IsFallback,
      string FallbackReason,      // "" on success; else the enum reason
      IReadOnlyList<string> Checks);
  ```
  Add `src/SupportAssistant.Core/Services/IInferenceDiagnosticsService.cs`:
  ```csharp
  public interface IInferenceDiagnosticsService
  {
      EngineDiagnostics? Embedding { get; }
      EngineDiagnostics? Generation { get; }
      event EventHandler? Updated;   // fires on either engine's load
  }
  ```
  Implementation stores the two snapshots and raises `Updated` from a thread-pool thread.

- [x] **T1.2 — Wire `OnSessionInitialized` into the engines.**
  Today `InferenceOptionsFactory.Create` sets only `UseGpuAcceleration`/`DeviceId`/`WarmupOnLoad`
  ([`InferenceOptionsFactory.cs:34-39`](../../src/SupportAssistant.Core/Engines/InferenceOptionsFactory.cs)).
  Change `Create` to accept the `IInferenceDiagnosticsService` (or have `App.axaml.cs` attach the
  callback) so each engine's `OnSessionInitialized = info => diag.Report(kind, info)` pushes the
  real `InferenceEngineInfo` into the service. Map `InferenceEngineInfo` → `EngineDiagnostics` in one
  place (Core), reading `.Provider`, `.IsFallback`, `.FallbackReason`, and formatting `.Checks`.

- [x] **T1.3 — Register the service + reorder DI.**
  In `App.axaml.cs` `ConfigureServices`, register `IInferenceDiagnosticsService` **before** the
  engine factories, and pass it into `InferenceOptionsFactory.Create` for both the
  `TextEmbeddingEngine` and `TextGenerationEngine` singletons
  ([`App.axaml.cs:117-128`](../../src/SupportAssistant/App.axaml.cs)).

- [x] **T1.4 — Surface in the UI.**
  - `SettingsViewModel`: expose `EmbeddingProvider`/`GenerationProvider` + `IsFallback`/`FallbackReason`
    read-only properties refreshed from `IInferenceDiagnosticsService.Updated`; add a small
    "Acceleration" status block to `SettingsView.axaml` (provider + fallback note, e.g.
    "GPU: DirectML" or "CPU fallback — libmigraphx_c.so.3 missing").
  - `BackgroundTaskViewModel`/`MainWindowViewModel`: optionally show the generation provider in the
    post-startup status line.

- [ ] **T1.5 — Retire the ad-hoc DirectML probe (bridge to WS3).** *Deferred to WS3 per the WS1
  scope decision (purely additive PR).* `BackgroundTaskService.InitializeOnnxRuntimeAsync` currently calls `_onnxService.Initialize()` and
  string-matches "DirectML" vs "CPU"
  ([`BackgroundTaskService.cs:196-222`](../../src/SupportAssistant.Core/Services/BackgroundTaskService.cs)).
  Replace that branch with the real diagnostics snapshot once the engines have loaded (the warmup on
  load triggers `OnSessionInitialized`). This makes `IOnnxRuntimeService`'s provider probe redundant
  (see WS3).

### 2.2 Acceptance — WS1
- A unit test with a fake `InferenceEngineInfo` (fallback path) produces the expected
  `EngineDiagnostics` snapshot (provider, `IsFallback=true`, reason text, checks list).
- Running the app on a machine without the configured provider shows the real fallback reason in
  Settings (not a generic "CPU").
- `EngineInfo`/`OnSessionInitialized` referenced from app code (closes the gap flagged in the status doc).

### 2.3 Risks
- `OnSessionInitialized` fires lazily (on first `LoadAsync`, triggered by warmup or first predict).
  The Settings UI must tolerate `null` until the engine loads (show "Loading…"), not assume it's
  populated at startup.

### 2.4 Implementation Notes (filled 2026-07-10)
- **PR:** `feat/inference-diagnostics` → `development`, PR [#10](https://github.com/nam20485/SupportAssistant/pull/10).
  Key commits: `8a986d5` (DTO+service), `5316af0` (wire + DI), `e697087` (Settings UI), `ca3b851` (tests).
- **Deviation from plan (interface shape).** The ws-plan ships a **non-nullable**
  `EngineDiagnostics Embedding/Generation` with an `IsLoaded` flag (default `false` until the first
  `Report`), instead of the nullable `EngineDiagnostics?` sketched in §2.1 above. This is cleaner
  (no nullable consumers in the UI VM) and fully satisfies the "tolerate Loading…" risk in §2.3 —
  the VM checks `IsLoaded` rather than `null`.
- **Callback attachment lives in the factory.** `InferenceOptionsFactory.Create` does a
  `diagnostics is InferenceDiagnosticsService` type-check and attaches `OnSessionInitialized`. This
  keeps all option-building in one place; the concrete-type check is intentional (the interface is
  library-decoupled and has no `Report` method).
- **Easier than expected.** `InferenceEngineInfo` and `ProviderDiagnosticCheck` are `public` records
  with `init` setters, so tests build fallback snapshots directly — no ONNX session, no model, no
  network. The 5 new tests run in ~1 ms total.
- **Follow-up discovered.** `MainWindowViewModelResolutionTests.BuildProvider` hand-mirrors
  `App.ConfigureServices`; it broke when `SettingsViewModel` gained a ctor param. It was updated to
  mirror the new `IInferenceDiagnosticsService` registration. **A future WS3 cleanup** could replace
  this hand-mirror by exposing `App.ConfigureServices` for test reuse (it is currently `private
  static`), so DI drift can't silently break the resolution test again.
- **Known minor leak (deferred).** `SettingsViewModel` (transient) subscribes to the singleton
  `IInferenceDiagnosticsService.Updated`. The singleton keeps the transient VM alive until the
  handler is unsubscribed. WS1 accepts this (the plan's disposal note); if Settings is opened many
  times, wire a weak handler / `IDisposable` — tracked as a backlog item, not a WS3 blocker.

---

## 3. WS2 — Stage 4: live streaming to the UI (High)

**Goal:** Render generation **token-by-token** in the chat, honoring the existing cancel button,
gated by `AiSettings.EnableStreaming`, with a one-shot fallback. The library fix (H3) is in, so this
is consumer-only.

### 3.1 Design decisions (resolve before coding)

- **D1 — Make `ChatMessage.Content` mutable & observable.** Today `Content` is `init`-only
  ([`ChatMessage.cs:8`](../../src/SupportAssistant/Models/ChatMessage.cs)), so it cannot be updated
  in place as tokens arrive. Convert `ChatMessage` to a `ReactiveObject` (or implement
  `INotifyPropertyChanged`) with a settable `Content` (and an `IsStreaming` flag). Mutating one
  instance avoids rebuilding the `Messages` collection on every token (cheap updates, stable identity
  for scroll/selection).
- **D2 — Stream the *response* text, not the tool-calling reasoning.** The agent loop does
  generate → parse tools → execute → follow-up generate
  ([`AgentOrchestrator.cs:62-131`](../../src/SupportAssistant.Core/Agent/AgentOrchestrator.cs)).
  Streaming the visible final/follow-up text gives the latency win; the reasoning/tool phases stay
  one-shot. (Full multi-turn streaming is deferred — see §3.4.)
- **D3 — `ISLMService` gains a streaming method; one-shot is preserved.**

### 3.2 Tasks

- [ ] **T2.1 — `ISLMService.StreamResponseAsync` (Core).**
  Add to [`ISLMService.cs`](../../src/SupportAssistant.Core/Agent/ISLMService.cs):
  ```csharp
  IAsyncEnumerable<string> StreamResponseAsync(string prompt, CancellationToken cancellationToken = default);
  ```
  `GenerateResponseAsync` stays (one-shot parity + fallback).

- [ ] **T2.2 — Implement in `OnnxSLMService`.**
  `StreamResponseAsync` returns `_engine.PredictStreamingAsync(prompt, options: null, ct)` directly
  (it already drives it internally at [`OnnxSLMService.cs:48-54`](../../src/SupportAssistant.Core/Agent/OnnxSLMService.cs));
  fold the existing availability probe (`_state`) so a failed stream marks the service unavailable as
  today. Keep `GenerateResponseAsync` accumulating the stream (or call `StreamResponseAsync`).

- [ ] **T2.3 — Streaming agent response (Core).**
  Add `IAgentOrchestrator.StreamResponseAsync(userId, query, ct)` returning `IAsyncEnumerable<string>`
  that runs the existing tool-calling phases, then streams the **follow-up response** via
  `ISLMService.StreamResponseAsync` (the `GenerateFollowUpResponseAsync` path,
  [`AgentOrchestrator.cs:310-321`](../../src/SupportAssistant.Core/Agent/AgentOrchestrator.cs)). When no
  tools are called, stream the initial response text instead. Non-streaming `ProcessQueryAsync` is
  retained unchanged.

- [ ] **T2.4 — Consume the stream in `ChatViewModel` (UI).**
  In `SendMessageAsync` ([`ChatViewModel.cs:169-270`](../../src/SupportAssistant/ViewModels/ChatViewModel.cs)):
  - if `_orchestrator != null && settings.Ai.EnableStreaming`: create the assistant `ChatMessage`
    (`IsStreaming = true`), add it, then `await foreach (var chunk in _orchestrator.StreamResponseAsync(...))`
    appending to `message.Content` (raises property changed → UI updates); on completion set
    `IsStreaming = false` and append the tool/metadata footer (current `FormatAgentResponse` logic);
  - else: the current one-shot path unchanged (fallback).
  Keep the existing `_processingCts`/`CancelProcessing` wiring so the cancel button aborts mid-stream.

- [ ] **T2.5 — Plumb `EnableStreaming` into the ViewModel.**
  `ChatViewModel` currently has no settings reference; inject `ISettingsService` (or read the flag at
  send time) so the `EnableStreaming` checkbox ([`SettingsView.axaml:126`](../../src/SupportAssistant/Views/SettingsView.axaml))
  actually controls rendering.

- [ ] **T2.6 — UI polish (optional, same workstream).**
  Show a subtle "typing/streaming" affordance while `IsStreaming` (reuse `IsTyping`); keep
  `ScrollToBottom` firing as `Content` grows (subscribe to the assistant message's property changed).

### 3.3 Acceptance — WS2
- **Parity:** for a fixed prompt, `string.Concat(orchestrator.StreamResponseAsync(...))` equals the
  one-shot `ProcessQueryAsync(...).ResponseText` (modulo the tool/metadata footer formatting).
- **Live rendering:** tokens appear incrementally in the UI while streaming (verified by a UI/VM test
  or manual run).
- **Cancellation:** pressing Cancel mid-stream aborts promptly, leaves a clean partial message (or
  discards per UX choice), and the next send works (library guarantees single-in-flight + clean KV state).
- **Fallback:** with `EnableStreaming = false` (or orchestrator absent), behavior is identical to today.
- Build clean under `TreatWarningsAsErrors=true`; existing tests green.

### 3.4 Out of scope (deferred)
- Streaming the *reasoning* text of the ReAct multi-step cycle
  (`ExecuteReActCycleAsync`, [`AgentOrchestrator.cs:323`](../../src/SupportAssistant.Core/Agent/AgentOrchestrator.cs)).
- Sampling controls: `StreamingOptions` (MaxSteps/EosTokenIds/sampling) are not yet surfaced; the
  engine defaults are used. (Note: per the library review-fix-plan M3, `SamplingParams` was recently
  wired/decided upstream — confirm behavior on bump.)

---

## 4. WS3 — Minor cleanups (Medium)

- [ ] **T3.1 — Remove vestigial `IOnnxRuntimeService` (after WS1 T1.5).** Once
  `BackgroundTaskService` no longer relies on `_onnxService.Initialize()` for the provider probe,
  drop the `IOnnxRuntimeService` registration ([`App.axaml.cs:112`](../../src/SupportAssistant/App.axaml.cs))
  and the `OnnxRuntimeService` implementation, **after** auditing all other usages
  (it's referenced by `BackgroundTaskService` ctor and `CoreServicesTests`). This satisfies plan T1.8.
- [ ] **T3.2 — Single tool-registration mechanism.** `ToolRegistry.RegisterCoreTools()` is fully commented
  out while registration uses `DiscoverAndRegisterTools()` ([`App.axaml.cs:158-163`](../../src/SupportAssistant/App.axaml.cs)).
  Delete the dead `RegisterCoreTools` body and keep reflection discovery — or invert — pick one and
  document it. Cosmetic.
- [ ] **T3.3 — Remove registry backup/restore stub note** if registry tools remain deferred; ensure
  `SecurityManager` restore path comment matches reality
  ([status doc §E](./inference-engine-integration-status.md)).

### Acceptance — WS3
- No compile warnings/errors; `rg 'IOnnxRuntimeService' src/` returns only intended references (or none).
- Tools still register and execute as before (existing tool tests pass).

---

## 5. WS4 — Deferred tail (Low / ongoing backlog)

Not detailed here; tracked as backlog. Grouped for awareness (from the original app spec /
[`docs/ai-new-app-template.md`](../ai-new-app-template.md) deferred capabilities):

- **More tools:** `CreateDirectory`/`DeleteFile`, INI/Registry/Environment, `GetRunningProcesses`,
  network diagnostics (`PingHost`/`TraceRoute`/`DNSLookup`/`PortScan`), system monitoring. Each is a
  self-contained `ITool` in `Tools/` (auto-discovered) with HITL gating for modifying ops.
- **Audit-trail viewer UI + permissions-management UI** (Phase 4.4/4.6).
- **Security hardening:** penetration pass, execution sandboxing, system restore points.

When a tool is picked up, follow the existing pattern: implement `ITool`, set `RequiresApproval`/
`IsModifying`, rely on `SecurityManager` for approval + backup, and add a test.

---

## 6. WS5 — Out-of-process engine (Low / **conditional**)

**Gated on a product decision to support Linux/ROCm.** Today SupportAssistant is Windows-only
(`RuntimeIdentifiers = win-x64;win-arm64`), where DirectML is in-process and the ROCm/Mesa↔comgr LLVM
collision cannot occur — so this is **not** needed now. Do **not** implement unless Linux/ROCm is added.

If/when gated-in, the work is:
- Add a `--worker inference` branch in `Program.cs` calling `InferenceWorkerHost.RunAsync(factory)`
  ([`Program.cs:14-28`](../../src/SupportAssistant/Program.cs)).
- Add a `NeedsOutOfProcess()` opt-in guard (Linux + `libamdhip64.so.7` present) and an
  `OutOfProcessBaseInferenceEngine` subclass; choose `OutOfProcessEngineOptions`
  (`WorkerExecutablePath`, `EngineId`, `WorkerEnvironment`, `EngineOptions`, timeouts).
- Default `SystemTextJsonWorkerSerializer` is adequate for the `string`↔`string` text engines.
- Diagnostics mirror automatically (the host proxy copies the worker's `EngineInfo`), so WS1 keeps
  working unchanged in OOP mode.

---

## 7. WS6 — Merge `development → master` (Process)

`development` is 62+ commits ahead of `master` (the publish/default branch). After WS1–WS3 land and
CI is green on `development`:
- Open `development → master`; review the full commit range (not just the tip).
- Verify the `1.1.29` package restore works in CI (GitHub Packages auth: `GH_PACKAGES_TOKEN`, commit
  `9baf457`) on a clean runner.
- Update [`STATUS.md`](./STATUS.md) and this doc's "Last verified" on merge.

---

## 8. Definition of Done (per workstream)

- [ ] Code builds with **0 warnings / 0 errors** under `TreatWarningsAsErrors=true` (both projects).
- [ ] `dotnet test` green; new tests added for parity/cancellation (WS2) and the diagnostics snapshot (WS1).
- [ ] The one-shot chat path still works as a fallback when streaming/diagnostics are off or error.
- [ ] No app-side ONNX/decode/tokenizer loops introduced — all inference goes through
      `InferenceEngine.Core` engines.
- [ ] `STATUS.md` + this doc updated to reflect what landed; `rg OnSessionInitialized src/` returns a
      real wiring (closes the diagnostics gap), `rg PredictStreaming` shows end-to-end use (WS2).

---

## 9. Quick reference — key files touched

| Workstream | Files |
|---|---|
| WS1 | `Core/Models/EngineDiagnostics.cs` (new), `Core/Services/IInferenceDiagnosticsService.cs` (new), `Core/Engines/InferenceOptionsFactory.cs`, `App.axaml.cs`, `ViewModels/SettingsViewModel.cs`, `Views/SettingsView.axaml`, `Services/BackgroundTaskService.cs` |
| WS2 | `Core/Agent/ISLMService.cs`, `Core/Agent/OnnxSLMService.cs`, `Core/Agent/IAgentOrchestrator.cs`, `Core/Agent/AgentOrchestrator.cs`, `Models/ChatMessage.cs`, `ViewModels/ChatViewModel.cs`, `Views/ChatView.axaml` (streaming affordance) |
| WS3 | `App.axaml.cs`, `Core/Services/OnnxRuntimeService.cs` (remove), `Core/Tools/ToolRegistry.cs` |
| WS5 | `Program.cs`, new OOP subclass + options (only if Linux/ROCm) |

---

*This plan is derived from a direct read of the current `development` source tree and the local
`intel-agency/inference-engine-lib` clone. Task IDs (T1.1, T2.3, …) are stable references for PR
titles and commit messages.*
