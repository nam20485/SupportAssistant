# Instructions

## General

You are working in **SupportAssistant**, an Avalonia desktop application (C# / .NET 10) that runs
local ONNX inference via `InferenceEngine.Core` and can act through user-approved tools.

Stack facts (do not assume ASP.NET, Blazor, or cloud deployment):

- UI: Avalonia 11 + ReactiveUI + CommunityToolkit.Mvvm (`src/SupportAssistant`)
- Core: agent, tools, RAG/embeddings, security (`src/SupportAssistant.Core`)
- Tests: xUnit + FluentAssertions + Moq (`src/SupportAssistant.Tests`)
- Package feed: `nuget.config` requires `NUGET_AUTH_TOKEN` (GitHub PAT, `read:packages`) for the
  private `intel-agency` feed hosting `InferenceEngine.Core`
- License: AGPL-3.0-or-later

Prefer official docs: [Avalonia](https://docs.avaloniaui.net/),
[ONNX Runtime](https://onnxruntime.ai/docs/),
[Microsoft Learn .NET](https://learn.microsoft.com/dotnet/),
and the InferenceEngine consumer guide in `intel-agency/inference-engine-lib`.

When recommending changes, cite the docs you used. Validate with `dotnet build` / `dotnet test`
(see [`AGENTS.md`](../AGENTS.md)). Offer to apply edits in logical chunks and confirm after each
successful build/test.

## Primary references

Read these before making non-trivial changes:

1. [`AGENTS.md`](../AGENTS.md) — build/test commands, layout, conventions
2. [`docs/plans/STATUS.md`](../docs/plans/STATUS.md) — current project status
3. [`docs/ai-new-app-template.md`](../docs/ai-new-app-template.md) — product/spec (see as-built notes)
4. [`docs/PACKAGING.md`](../docs/PACKAGING.md) — distribution

## Optional workflow modules

Process-oriented assignment definitions (not stack guidance):

- [`ai_instruction_modules/ai-workflow-assignments.md`](../ai_instruction_modules/ai-workflow-assignments.md)

Archived ASP.NET/GCP web-app instruction modules (do **not** follow for this repo):
`docs/.archived/ai_instruction_modules-web-template/`.
