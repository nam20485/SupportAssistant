# Repository Guidelines

Local-first Avalonia desktop assistant (AGPL-3.0-or-later) that runs ONNX models on-device via `InferenceEngine.Core` and can act through user-approved tools. Roadmap and workstream plans live under `docs/plans/`.

## Project Structure & Module Organization

- `src/SupportAssistant` — Avalonia UI (Views/ViewModels), DI composition in `App.axaml.cs`, and Avalonia-backed security prompts under `Security/`.
- `src/SupportAssistant.Core` — inference/embeddings (`Services/`, `Engines/`), agent orchestration (`Agent/`), tool registry (`Tools/`), and approval/audit (`Security/`). UI depends on Core; keep platform UI out of Core.
- `src/SupportAssistant.Tests` — xUnit suites mirroring Core/UI areas (`Services/`, `Agent/`, `Tools/`, `Security/`, `UI/`).
- `scripts/` — Windows packaging helpers; `Directory.Build.props` / `Directory.Build.targets` set solution-wide versioning, warnings-as-errors, and Linux ONNX native-lib P/Invoke wiring.
- `nuget.config` requires `NUGET_AUTH_TOKEN` (GitHub PAT with `read:packages`) for the private `intel-agency` feed that hosts `InferenceEngine.Core`. Do not commit credentials.

## Build, Test, and Development Commands

SDK pin: `global.json` → .NET 10; projects target `net10.0`.

```bash
export NUGET_AUTH_TOKEN=<github-pat-with-read:packages>
dotnet restore SupportAssistant.sln
dotnet build SupportAssistant.sln -c Release
dotnet test SupportAssistant.sln -c Release
dotnet test SupportAssistant.sln --filter FullyQualifiedName~InferenceDiagnosticsServiceTests
dotnet run --project src/SupportAssistant/SupportAssistant.csproj
dotnet publish src/SupportAssistant/SupportAssistant.csproj -c Release -r win-x64 --self-contained true -o dist/win-x64
```

Linux + AMD RDNA2 (gfx1031 / RX 6700 XT, also gfx1032/1034): export `HSA_OVERRIDE_GFX_VERSION=10.3.0` in the **launching** environment before starting the app or tests that exercise MIGraphX. Without it, rocBLAS SIGABRTs (not a catchable .NET fallback). Interactive shells often get this from `~/.bashrc`; IDE Debug/Release launches should use `.vscode/launch.json` (already sets the var). Setting the variable inside `Main()` is too late. If the override is missing but the ROCm stack is otherwise ready, SupportAssistant forces CPU, may show a legacy-GPU dialog (`Ai.BypassLegacyRocmGpuDialog` skips it), and writes `supportassistant-rocm-preflight.log`.

Packaging: `scripts/build-simple.bat` or `scripts/build-distribution.ps1` (see `docs/PACKAGING.md`). CI workflow: `.github/workflows/build-and-package.yml`.

## Coding Style & Naming Conventions

C# with `Nullable` enabled, `ImplicitUsings`, `LangVersion` latest, and `TreatWarningsAsErrors` (solution-wide). Prefer `I`-prefixed interfaces, `*Service` / `*ViewModel` suffixes, and Avalonia compiled bindings. Do not enable publish trimming (ReactiveUI incompatibility).

## Testing Guidelines

xUnit + FluentAssertions + Moq; coverlet collector is referenced. Mirror production folders under `src/SupportAssistant.Tests`. Name tests `*Tests.cs`; use `[Fact]` / `[Theory]` as elsewhere in the suite.

## Commit & Pull Request Guidelines

History uses Conventional Commits: `feat|fix|docs|test|build|chore(scope): …` (e.g. `feat(diagnostics): …`, `fix(build): …`). PRs should state intent, link plan/task IDs when applicable, and note how you validated (`dotnet build` / `dotnet test`). No PR template is checked in.

## Learned User Preferences

- Prefer fuller briefings (short paragraphs with selective bullets) over terse bullet-only summaries when asking for explanations of doc or plan changes.
- Prefer clean process exit (exit code 0) even when noise is upstream/third-party; for swallowed non-fatal exceptions and `UnobservedTaskException` `SetObserved()` paths, emit a one-line stderr record (type + short detail)—no stacks or multi-line dumps.
- Do not break Debug F5 while adding Release workflows; keep `launch.json` Debug launch intact and put Release-without-debugger on a task.

## Learned Workspace Facts

- Cursor lacks C# Dev Kit: `.vscode/tasks.json` must use process/shell `dotnet` tasks (e.g. `dotnet build`), not `"type": "dotnet"`.
- Debug via F5 (`launch.json` + `HSA_OVERRIDE_GFX_VERSION`); Release without the debugger via Tasks → `dotnet: run (Release)`—launch configs always attach the debugger.
- `docs/plans/STATUS.md` is the live project status snapshot; prefer it over bannered/historical plans. ASP.NET/Blazor/GCP web-template AI modules were moved to `docs/.archived/ai_instruction_modules-web-template/` and are not live instructions for this Avalonia app.
- On Linux, Avalonia DBus IME is off by default (`X11PlatformOptions.EnableIme = false`) to avoid ibus-portal Destroy/`InputContext_*` exit failures; opt in with `AVALONIA_IM_MODULE=ibus` (or `fcitx`/`xim`).
- Execution-provider changes in Settings need an app restart; inference engines are singletons built at startup.
- Out-of-process inference (`--worker` / `OutOfProcessBaseInferenceEngine`) is planned as WS5 for Linux/ROCm LLVM collision risk; GUI chat-on-GPU remains unverified until that lands.
- **The WS5 collision is reproduced, not hypothetical** (2026-07-10, this Linux/RDNA2 dev host): Mesa and MIGraphX/comgr both link LLVM and re-register the same CommandLine flag → `LLVM ERROR: inconsistency in registered CommandLine options` (native `abort()`, uncatchable). **Interim mitigation shipped the same day:** `InferenceOptionsFactory.Create`'s `allowGpuInProcess` defaults to `false`, so every in-process caller on Linux (chat send, KB indexing, Settings probe — all in the GUI process) is CPU-forced by default; only the isolated `RocmExecutionProviderSmokeTests` opts in to exercise the real GPU path. `rg 'WS5:' src/` finds every touch point. GPU/ROCm inference itself is not broken (that test proves MIGraphX works fine out-of-process) — it is disabled in-GUI until WS5's real out-of-process worker lands. Full detail: `docs/plans/inference-engine-integration-status.md` §C.
