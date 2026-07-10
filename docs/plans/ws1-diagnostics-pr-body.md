## Summary
WS1 of the inference integration plan: surface the real execution provider and CPU-fallback reason
from `InferenceEngine.Core`'s `InferenceEngineInfo` (via `OnSessionInitialized`) into Settings, instead
of silently running on CPU.

## Changes
- New `EngineDiagnostics` DTO + `IInferenceDiagnosticsService` / impl (Core) — library-decoupled,
  thread-safe snapshot store.
- `InferenceOptionsFactory.Create` attaches `OnSessionInitialized` for both engines.
- `App.axaml.cs` registers the service and passes it to the embedding + generation engine factories.
- Settings UI: read-only "Acceleration Status" block (provider per engine + fallback notice),
  UI-thread-marshaled via `RxApp.MainThreadScheduler`.

## Scope
Purely additive — no removals. The `BackgroundTaskService` DirectML-probe refactor and
`IOnnxRuntimeService` removal are deliberately deferred to WS3 (see plan §0 scope decision).

## Tests
- New `InferenceDiagnosticsServiceTests`: fallback snapshot mapping, non-fallback mapping,
  `Updated` event, per-engine-kind storage, and "not loaded until reported".
- Updated `MainWindowViewModelResolutionTests` to mirror the new DI registration.

## Verification
- [x] `dotnet build` 0 warnings / 0 errors (`TreatWarningsAsErrors=true`)
- [x] `dotnet test` green (176 passed)
- [ ] Manual: Settings shows real provider after a chat message triggers engine load

## Plan refs
- `docs/plans/inference-integration-implementation-plan.md` §2 (WS1)
- `docs/plans/inference-engine-integration-status.md` §D
- `docs/plans/ws1-inference-diagnostics-development-plan.md` (file-level plan)
