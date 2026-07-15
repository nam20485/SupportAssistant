# Assignment-Based Workflow

The assignment-based workflow builds on a task-based process by adding **assignments**: defined
sets of goals, acceptance criteria, and steps.

## Workflow Assignments

* Each assignment describes how to accomplish a specific task, start a new application, or stage work.
* Assignments are given by an orchestrator; perform until finished or reassigned.
* Each type is defined in a workflow assignment definition file under
  `ai_instruction_modules/ai-workflow-assignments/<short-id>/` (or a flat `.md` in that folder).

```
ai_instruction_modules/
  ai-workflow-assignments/
    <assignment_short_id>/   # or <assignment_short_id>.md
```

## Definition format

* **Assignment Title** — descriptive title
* **Assignment Short ID** — unique id
* **Goal** — what success looks like
* **Acceptance Criteria** — completion conditions
* **Assignment** — detailed description
* **Detailed Steps** — how to execute
* **Completion** — finalize / handoff

## Available definitions

* [pr-approval-and-merge.md](ai-workflow-assignments/pr-approval-and-merge.md)
* [perform-task.md](ai-workflow-assignments/perform-task.md)
* [continue-task-work.md](ai-workflow-assignments/continue-task-work.md)
* [create-application.md](ai-workflow-assignments/create-application.md)

For stack-specific rules (Avalonia / .NET 10 / local ONNX), follow [`AGENTS.md`](../AGENTS.md) —
not archived web-app modules under `docs/.archived/ai_instruction_modules-web-template/`.
