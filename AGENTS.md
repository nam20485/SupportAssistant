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

Packaging: `scripts/build-simple.bat` or `scripts/build-distribution.ps1` (see `docs/PACKAGING.md`). CI workflow: `.github/workflows/build-and-package.yml`.

## Coding Style & Naming Conventions

C# with `Nullable` enabled, `ImplicitUsings`, `LangVersion` latest, and `TreatWarningsAsErrors` (solution-wide). Prefer `I`-prefixed interfaces, `*Service` / `*ViewModel` suffixes, and Avalonia compiled bindings. Do not enable publish trimming (ReactiveUI incompatibility).

## Testing Guidelines

xUnit + FluentAssertions + Moq; coverlet collector is referenced. Mirror production folders under `src/SupportAssistant.Tests`. Name tests `*Tests.cs`; use `[Fact]` / `[Theory]` as elsewhere in the suite.

## Commit & Pull Request Guidelines

History uses Conventional Commits: `feat|fix|docs|test|build|chore(scope): …` (e.g. `feat(diagnostics): …`, `fix(build): …`). PRs should state intent, link plan/task IDs when applicable, and note how you validated (`dotnet build` / `dotnet test`). No PR template is checked in.
