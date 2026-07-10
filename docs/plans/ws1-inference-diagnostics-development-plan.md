# WS1 Development Plan — Surface Inference Diagnostics

**Status:** Implemented — PR #10 open against `development`
**Date:** 2026-07-08 (implemented 2026-07-10)
**Base branch:** `development` (branched off `daa6a95`; plan originally authored at `892a34c`)
**Feature branch:** `feat/inference-diagnostics` → PR [#10](https://github.com/nam20485/SupportAssistant/pull/10)
**Target PR base:** `development`
**Parent plans:** [`inference-integration-implementation-plan.md`](./inference-integration-implementation-plan.md) §2 (WS1); status: [`inference-engine-integration-status.md`](./inference-engine-integration-status.md) §D

This is a self-contained, file-level plan for **one focused PR**. It is **purely additive** (see scope
decision below). Read it top-to-bottom; the branch/PR commands are at the end.

---

## 0. Scope decision (read first)

WS1 ships **diagnostics surfacing only** — new types, new wiring, new UI, new tests. It does **not**
remove anything. Specifically:

- **In scope:** `EngineDiagnostics` DTO + `IInferenceDiagnosticsService`, wire `OnSessionInitialized`
  into both engines, show the real provider + fallback in Settings, add tests.
- **Deferred to WS3 (next PR):** the `BackgroundTaskService` probe refactor and the
  `IOnnxRuntimeService` removal. (Once Settings shows the *real* provider, the ad-hoc DirectML probe
  in `BackgroundTaskService.InitializeOnnxRuntimeAsync` is visibly redundant — that removal is the
  natural WS3 cleanup and needs its own reference audit under `TreatWarningsAsErrors=true`.)

This keeps WS1 low-risk, fast to review, and independently revertable.

---

## 1. Objective & success criteria

Tell the user **which execution provider actually loaded and why a fallback occurred**, using the
library's `InferenceEngineInfo` — instead of silently running on CPU. After WS1:

- `rg 'OnSessionInitialized|InferenceEngineInfo' src/SupportAssistant.Core` returns **real wiring**
  (today the only hit is an XML doc comment in `InferenceOptionsFactory.cs:14`).
- Settings → AI Model Settings shows a read-only **"Acceleration Status"** block:
  `Embedding: <Provider>` / `Generation: <Provider>` and, on fallback, the reason
  (e.g. *"Generation fell back to CPU — libmigraphx_c.so.3 missing"*).
- A unit test maps a fallback `InferenceEngineInfo` → the expected `EngineDiagnostics` snapshot.
- `dotnet build` and `dotnet test` are green; **no removals**, no behavioral change to inference.

---

## 2. Library API facts (verified in the local clone)

- `InferenceEngineOptions.OnSessionInitialized` is `Action<InferenceEngineInfo>?`
  (`InferenceEngine.Core/InferenceEngineOptions.cs:58`). It fires **once**, on a **thread-pool
  thread**, after the session is built (incl. fallback). **Consumers that touch UI must marshal to the
  UI thread.** Exceptions in the callback are caught/logged by the library and do not abort the engine.
- `InferenceEngineInfo` is a `sealed record` (`Diagnostics/InferenceEngineInfo.cs`) with:
  `Provider` (`InferenceProvider` enum), `IsFallback` (bool), `FallbackReason`
  (`InferenceFallbackReason` enum), `Checks` (`IReadOnlyList<ProviderDiagnosticCheck>`),
  `ProviderOptions`, `SessionCreatedAt`.
- `ProviderDiagnosticCheck` record: `Name`, `Description`, `Category`, `Result` (`Passed/Failed/Skipped`),
  `Detail`, `Order`.
- `InferenceFallbackReason` enum: `None, UserDisabledGpu, UnsupportedPlatform, PlatformProbeFailed,
  KfdDeviceMissing, HsaRuntimeUnloadable, HipRuntimeMissing, MIGraphXRuntimeMissing,
  ProviderAppendFailed, SessionConstructionFailed, NativeLibraryLoadFailed`.
- `WarmupOnLoad = true` (already set) triggers `LoadAsync` → the callback fires on **first inference**
  (lazy). Settings must therefore tolerate a "Loading…" state until the engine loads.

---

## 3. Task breakdown

### T1.1 — Diagnostics DTO (`SupportAssistant.Core`)
**New file** `src/SupportAssistant.Core/Models/EngineDiagnostics.cs`:

```csharp
using System.Collections.Generic;

namespace SupportAssistant.Core.Models;

/// <summary>UI-facing, library-decoupled snapshot of one engine's loaded provider + fallback trail.</summary>
public sealed record EngineDiagnostics
{
    /// <summary>"Embedding" or "Generation".</summary>
    public string EngineKind { get; init; } = string.Empty;

    /// <summary>The EP that actually loaded (e.g. "DirectML", "MIGraphX", "Cpu").</summary>
    public string Provider { get; init; } = string.Empty;

    public bool IsFallback { get; init; }

    /// <summary>Empty on success; otherwise a short human reason.</summary>
    public string FallbackReason { get; init; } = string.Empty;

    /// <summary>Ordered, human-readable check lines ("rocm.append: Failed — ...").</summary>
    public IReadOnlyList<string> Checks { get; init; } = System.Array.Empty<string>();

    /// <summary>True until the first OnSessionInitialized fires for this engine.</summary>
    public bool IsLoaded { get; init; }
}
```

### T1.2 — Diagnostics service (`SupportAssistant.Core`)
**New file** `src/SupportAssistant.Core/Services/IInferenceDiagnosticsService.cs`:

```csharp
using System;
using SupportAssistant.Core.Models;

namespace SupportAssistant.Core.Services;

public interface IInferenceDiagnosticsService
{
    EngineDiagnostics Embedding { get; }
    EngineDiagnostics Generation { get; }

    /// <summary>Raised (on a thread-pool thread) whenever either engine reports a load.</summary>
    event EventHandler? Updated;
}
```

**New file** `src/SupportAssistant.Core/Services/InferenceDiagnosticsService.cs` — thread-safe
implementation. Keep snapshots as immutable records; lock around the swap + event raise:

```csharp
using System;
using System.Linq;
using System.Threading;
using InferenceEngine.Core.Diagnostics;
using SupportAssistant.Core.Models;

namespace SupportAssistant.Core.Services;

public sealed class InferenceDiagnosticsService : IInferenceDiagnosticsService
{
    private readonly object _gate = new();
    private EngineDiagnostics _embedding = Empty("Embedding");
    private EngineDiagnostics _generation = Empty("Generation");

    public EngineDiagnostics Embedding { get { lock (_gate) return _embedding; } }
    public EngineDiagnostics Generation { get { lock (_gate) return _generation; } }

    public event EventHandler? Updated;

    /// <summary>Called from OnSessionInitialized (thread-pool thread).</summary>
    public void Report(string engineKind, InferenceEngineInfo info)
    {
        var snapshot = Map(engineKind, info);
        EventHandler? handler;
        lock (_gate)
        {
            if (engineKind == "Embedding") _embedding = snapshot; else _generation = snapshot;
            handler = Updated;
        }
        handler?.Invoke(this, EventArgs.Empty);
    }

    private static EngineDiagnostics Map(string kind, InferenceEngineInfo i) => new()
    {
        EngineKind = kind,
        Provider = i.Provider.ToString(),
        IsFallback = i.IsFallback,
        FallbackReason = i.FallbackReason == InferenceFallbackReason.None ? "" : i.FallbackReason.ToString(),
        Checks = i.Checks
            .Select(c => $"{c.Name}: {c.Result}" + (string.IsNullOrEmpty(c.Detail) ? "" : $" — {c.Detail}"))
            .ToList(),
        IsLoaded = true,
    };

    private static EngineDiagnostics Empty(string kind) => new() { EngineKind = kind };
}
```

### T1.3 — Wire `OnSessionInitialized` (`SupportAssistant.Core`)
**Edit** `src/SupportAssistant.Core/Engines/InferenceOptionsFactory.cs`. Change `Create` to accept the
diagnostics service + engine kind and attach the callback (keeps all option-building in one place):

```csharp
public static InferenceEngineOptions Create(
    ISettingsService? settings,
    IInferenceDiagnosticsService? diagnostics = null,
    string engineKind = "Generation")
{
    var useGpu = true;
    if (settings != null)
    {
        var providerName = settings.Settings.Ai.ExecutionProvider?.Trim() ?? string.Empty;
        if (providerName.Equals("CPU", StringComparison.OrdinalIgnoreCase)) useGpu = false;
    }

    var options = new InferenceEngineOptions
    {
        UseGpuAcceleration = useGpu,
        DeviceId = 0,
        WarmupOnLoad = true,
    };

    if (diagnostics is InferenceDiagnosticsService svc)
        options.OnSessionInitialized = info => svc.Report(engineKind, info); // runs on a pool thread

    return options;
}
```
> Keep the existing `WithLocalModel` extension method unchanged.

### T1.4 — Register + wire in DI (`SupportAssistant` app)
**Edit** `src/SupportAssistant/App.axaml.cs` `ConfigureServices`:

1. Register the diagnostics service **before** the engine factories:
   ```csharp
   services.AddSingleton<InferenceDiagnosticsService>();
   services.AddSingleton<IInferenceDiagnosticsService>(sp => sp.GetRequiredService<InferenceDiagnosticsService>());
   ```
2. Pass it into both factories ([`App.axaml.cs:117-128`](../../src/SupportAssistant/App.axaml.cs)):
   ```csharp
   services.AddSingleton(sp =>
   {
       var settings = sp.GetRequiredService<ISettingsService>();
       var diag = sp.GetRequiredService<InferenceDiagnosticsService>();
       return new TextEmbeddingEngine(InferenceOptionsFactory.Create(settings, diag, engineKind: "Embedding"));
   });
   services.AddSingleton(sp =>
   {
       var settings = sp.GetRequiredService<ISettingsService>();
       var diag = sp.GetRequiredService<InferenceDiagnosticsService>();
       return new TextGenerationEngine(InferenceOptionsFactory.Create(settings, diag, engineKind: "Generation"));
   });
   ```
3. Inject `IInferenceDiagnosticsService` into `SettingsViewModel` (add ctor param).

### T1.5 — Surface in Settings UI (`SupportAssistant` app)
**Edit** `src/SupportAssistant/ViewModels/SettingsViewModel.cs`:
- Make it `ReactiveObject` (it already derives `ViewModelBase : ReactiveObject`).
- Add observable, **UI-thread-marshaled** properties that refresh on `Updated`:

```csharp
private readonly IInferenceDiagnosticsService _diag;
// in ctor: _diag = diagnostics; _diag.Updated += OnDiagUpdated; (marshal with RxApp.MainThreadScheduler)

private string _embeddingStatus = "Embedding: loading…";
private string _generationStatus = "Generation: loading…";
private string _fallbackNotice = string.Empty;

public string EmbeddingStatus { get => _embeddingStatus; set => this.RaiseAndSetIfChanged(ref _embeddingStatus, value); }
public string GenerationStatus { get => _generationStatus; set => this.RaiseAndSetIfChanged(ref _generationStatus, value); }
public string FallbackNotice   { get => _fallbackNotice; set => this.RaiseAndSetIfChanged(ref _fallbackNotice, value); }
public bool HasFallback         { get => _hasFallback;   set => this.RaiseAndSetIfChanged(ref _hasFallback, value); }

private void OnDiagUpdated(object? s, EventArgs e) =>
    RxApp.MainThreadScheduler.Schedule(() =>   // callback fires on a pool thread → marshal
    {
        var em = _diag.Embedding; var ge = _diag.Generation;
        EmbeddingStatus  = em.IsLoaded ? $"Embedding: {em.Provider}" : "Embedding: loading…";
        GenerationStatus = ge.IsLoaded ? $"Generation: {ge.Provider}" : "Generation: loading…";
        var fb = new[] { em, ge }.Where(x => x.IsFallback)
            .Select(x => $"{x.EngineKind} fell back to CPU — {x.FallbackReason}");
        FallbackNotice = string.Join("\n", fb);
        HasFallback = fb.Any();
    });
```

**Edit** `src/SupportAssistant/Views/SettingsView.axaml` — insert an **"Acceleration Status"** block
right after the Execution Provider ComboBox (after line 70, inside the "AI Model Settings" Expander):

```xml
<StackPanel Spacing="5" Margin="0,5,0,0">
  <TextBlock Text="Acceleration Status" FontWeight="SemiBold"/>
  <TextBlock Text="{Binding EmbeddingStatus}" FontSize="12"
             Foreground="{DynamicResource AppMutedForeground}"/>
  <TextBlock Text="{Binding GenerationStatus}" FontSize="12"
             Foreground="{DynamicResource AppMutedForeground}"/>
  <TextBlock Text="{Binding FallbackNotice}" IsVisible="{Binding HasFallback}"
             FontSize="12" Foreground="Orange" TextWrapping="Wrap"/>
</StackPanel>
```

> **Disposal:** unsubscribe `Updated` in the VM's teardown if `SettingsViewModel` is ever long-lived;
> it is currently `AddTransient`, so the subscription dies with the view — acceptable, but wire a
> Dispose or weak handler if a leak surfaces.

---

## 4. Tests

Test stack: **xUnit + FluentAssertions** (see `SupportAssistant.Tests/CoreServicesTests.cs` style).
**New file** `src/SupportAssistant.Tests/Services/InferenceDiagnosticsServiceTests.cs`:

- `Report_MapsFallbackSnapshotCorrectly`: construct an `InferenceEngineInfo` with
  `Provider = Cpu`, `IsFallback = true`, `FallbackReason = MIGraphXRuntimeMissing`, one Failed check;
  assert `EngineDiagnostics.Provider == "Cpu"`, `IsFallback == true`, `FallbackReason` non-empty,
  `IsLoaded == true`, and `Checks` contains the check name + "Failed".
- `Report_RaisesUpdated`: subscribe to `Updated`; assert it fires once per `Report`.
- `Report_StoresPerEngineKind`: report Embedding then Generation; assert each getter returns its own
  snapshot independently.
- (Optional) a `SettingsViewModel` test verifying `GenerationStatus` reflects a stubbed service (marshal
  by awaiting a small delay / `RxApp.MainThreadScheduler`).

> The `InferenceEngineInfo` record is `public` with `init` setters, so tests can build instances
> directly without the ONNX session — **no network, no model** required.

---

## 5. Build & verify (run before opening the PR)

From the repo root (`/home/nam20485/src/github/nam20485/SupportAssistant`):

```bash
# 1. Restore (GitHub Packages needs GH_PACKAGES_TOKEN in CI; locally use your nuget.config)
dotnet restore SupportAssistant.sln

# 2. Build — MUST be 0 warnings / 0 errors (TreatWarningsAsErrors=true)
dotnet build SupportAssistant.sln -c Debug

# 3. Test (new diagnostics tests + full regression)
dotnet test SupportAssistant.sln -c Debug --no-build
```

Acceptance gates:
- `dotnet build` exits 0 with no warnings.
- `dotnet test` all green.
- Manual smoke (optional): run the app, open Settings → AI Model Settings, send one chat message
  (triggers load), reopen Settings → see the real `Provider` (and a fallback notice if applicable).

---

## 6. Branch & PR workflow

> Convention seen in history (`b758191 Merge pull request #7 from nam20485/dev/inference-first`):
> short-lived feature branches merged into `development` via PR.

### Create the branch off `development`

```bash
cd /home/nam20485/src/github/nam20485/SupportAssistant
git switch development
git pull --ff-only origin development          # start from latest
git switch -c feat/inference-diagnostics        # WS1 branch
```

### Implement, then commit in logical chunks

Match the existing conventional-commit style (`feat(...)`, `test(...)`, `docs(...)`). Suggested split:

```bash
git add src/SupportAssistant.Core/Models/EngineDiagnostics.cs \
        src/SupportAssistant.Core/Services/IInferenceDiagnosticsService.cs \
        src/SupportAssistant.Core/Services/InferenceDiagnosticsService.cs
git commit -m "feat(diagnostics): add inference diagnostics DTO + service"

git add src/SupportAssistant.Core/Engines/InferenceOptionsFactory.cs \
        src/SupportAssistant/App.axaml.cs
git commit -m "feat(diagnostics): wire OnSessionInitialized into both engines"

git add src/SupportAssistant/ViewModels/SettingsViewModel.cs \
        src/SupportAssistant/Views/SettingsView.axaml
git commit -m "feat(settings): surface real provider + fallback in AI settings"

git add src/SupportAssistant.Tests/Services/InferenceDiagnosticsServiceTests.cs
git commit -m "test(diagnostics): cover fallback mapping + Updated event"
```

### Push and open the PR against `development`

```bash
git push -u origin feat/inference-diagnostics

gh pr create --base development \
  --title "feat(diagnostics): surface real inference provider + fallback (WS1)" \
  --body-file docs/plans/ws1-diagnostics-pr-body.md
```

(`gh pr create` opens the PR on GitHub and prints its URL.)

---

## 7. PR body template

Create `docs/plans/ws1-diagnostics-pr-body.md` and use it with `--body-file` above:

```markdown
## Summary
WS1 of the inference integration plan: surface the real execution provider and CPU-fallback reason
from `InferenceEngine.Core`'s `InferenceEngineInfo` (via `OnSessionInitialized`) into Settings, instead
of silently running on CPU.

## Changes
- New `EngineDiagnostics` DTO + `IInferenceDiagnosticsService` / impl (Core) — library-decoupled,
  thread-safe snapshot store.
- `InferenceOptionsFactory.Create` attaches `OnSessionInitialized` for both engines.
- `App.axaml.cs` registers the service and passes it to the embedding + generation engine factories.
- Settings UI: read-only "Acceleration Status" block (provider per engine + fallback notice).

## Scope
Purely additive — no removals. The `BackgroundTaskService` DirectML-probe refactor and
`IOnnxRuntimeService` removal are deliberately deferred to WS3.

## Tests
- New `InferenceDiagnosticsServiceTests`: fallback snapshot mapping, `Updated` event, per-engine-kind storage.

## Verification
- [x] `dotnet build` 0 warnings / 0 errors (`TreatWarningsAsErrors=true`)
- [x] `dotnet test` green
- [ ] Manual: Settings shows real provider after a chat message triggers engine load

## Plan refs
- `docs/plans/inference-integration-implementation-plan.md` §2 (WS1)
- `docs/plans/inference-engine-integration-status.md` §D
```

---

## 8. Definition of Done

- [x] Branch `feat/inference-diagnostics` off latest `development`.
- [x] T1.1–T1.5 implemented; new tests added and passing.
- [x] `dotnet build` clean (0 warnings); `dotnet test` green (176 passed).
- [x] PR opened against `development` with the body above — PR [#10](https://github.com/nam20485/SupportAssistant/pull/10); CI pending.
- [x] No behavioral change to inference; no removals (those are WS3).
- [x] `rg 'OnSessionInitialized' src/SupportAssistant.Core src/SupportAssistant` returns real wiring
      (`InferenceOptionsFactory.cs:55`).

---

## 9. Out of scope (explicit)

- `BackgroundTaskService` probe refactor + `IOnnxRuntimeService` removal → **WS3**.
- Eager engine `LoadAsync` at startup to populate diagnostics sooner (optional enhancement; current
  behavior: snapshot fills on first inference, "loading…" until then) → backlog.
- Surfacing diagnostics in the onboarding wizard → backlog.
