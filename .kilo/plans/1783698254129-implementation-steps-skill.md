# Plan: `implementation-steps` Agent Skill

## Goal

Create a project-agnostic agent skill that orchestrates a 4-stage planning-and-implementation pipeline for software integration work. Each stage produces or consumes a standardized planning document, following the structure established in the SupportAssistant project's docs.

## Skill Location

`~/.agents/skills/implementation-steps/` (global, since it's used across many projects)

## Directory Structure

```
implementation-steps/
├── SKILL.md                            # Router: describes stages, args, workflow
├── references/
│   ├── get-status.md                   # Stage 1 instructions
│   ├── plan.md                         # Stage 2 instructions
│   ├── ws-plan.md                      # Stage 3 instructions
│   └── develop-ws.md                   # Stage 4 instructions
└── assets/
    └── templates/
        ├── status-template.md          # Integration status template
        ├── plan-template.md            # Implementation plan template
        └── ws-plan-template.md         # Workstream development plan template
```

**Rationale:** Single skill with reference files per stage (progressive disclosure). SKILL.md stays under 150 lines; each stage's full instructions (~150-250 lines) load only when that stage is active.

## SKILL.md Structure

YAML frontmatter:
```yaml
---
name: implementation-steps
description: >-
  4-stage planning and implementation pipeline for software integration work.
  Creates integration status docs, implementation plans, workstream development plans,
  and executes workstream implementation. Use when asked to plan integration work,
  check what's committed vs remaining, create a workstream plan, or implement a workstream.
  Stages: get-status, plan, ws-plan, develop-ws.
  USE FOR: integration planning, workstream planning, implementation status, 
  develop workstream, create implementation plan.
---
```

Body contents:
- Stage overview table with arguments and output files
- Routing logic: read the matching reference file based on `$stage`
- Output conventions (directory, naming pattern)
- Cross-stage dependencies (status → plan → ws-plan → develop-ws)

## Stage Specifications

### Stage 1: `get-status`
- **Argument:** `$stage = get-status` (optional: `$project-name`, `$output-dir`)
- **Output:** `docs/plans/<project>-integration-status.md`
- **Reference:** `references/get-status.md`
- **Instructions cover:**
  - Analyze the codebase and any upstream/external dependency
  - For each API surface the upstream exposes, determine: committed? partial? not used? not wired?
  - Fill the status template sections (Bottom Line table → per-surface sections A-H)
  - Evidence-based: cite file paths, `rg` results, commit hashes
  - Include "Recommended next steps" section
- **Template:** `assets/templates/status-template.md`

### Stage 2: `plan`
- **Argument:** `$stage = plan` (optional: `$status-doc`, `$project-name`, `$output-dir`)
- **Output:** `docs/plans/<project>-implementation-plan.md`
- **Reference:** `references/plan.md`
- **Instructions cover:**
  - Read the status doc (from stage 1 or existing); turn "Remaining" items into ordered workstreams
  - Write "Workstream overview & sequencing" table with Status legend, priority, dependencies, size
  - Write "Progress log" section (reverse-chronological, dated entries with PR #s)
  - Write "Guiding principles" (constraints for all workstreams)
  - For each workstream: Goal, Tasks (checkboxed with file-level detail), Acceptance criteria, Risks, **Implementation Notes** section (empty, filled after WS ships)
  - Definition of Done (per workstream)
  - Quick reference table of key files touched
- **Template:** `assets/templates/plan-template.md`
- **Key emphasis:** Progress log and WS overview table are living documents — instructions to maintain them as work lands

### Stage 3: `ws-plan`
- **Argument:** `$stage = ws-plan` (optional: `$plan-doc`, `$workstream-num`, `$output-dir`)
- **Output:** `docs/plans/ws<N>-<slug>-development-plan.md`
- **Reference:** `references/ws-plan.md`
- **Instructions cover:**
  - Read the implementation plan to extract the target workstream's scope
  - Scope decision (what's in / what's deferred)
  - Objective & success criteria
  - Library/API facts (verified signatures, threading, timing)
  - Task breakdown: file-level with code snippets/signatures
  - Tests: framework, new test file, specific test cases
  - Build & verify commands
  - Branch & PR workflow (create branch, commit strategy, push, open PR)
  - PR body template
  - Definition of Done
  - Out of scope (explicit)
- **Template:** `assets/templates/ws-plan-template.md`

### Stage 4: `develop-ws`
- **Argument:** `$stage = develop-ws`, `$workstream-plan = <path>` (required)
- **Reference:** `references/develop-ws.md`
- **Instructions cover:**
  - Read the workstream development plan fully before starting
  - Read the parent implementation plan to understand cross-WS context
  - Check the parent plan's Progress log and WS overview table for current state
  - Execute tasks in order from the ws-plan; check off `[ ]` → `[x]` as completed
  - Create the feature branch (per the plan's branch workflow)
  - Commit in logical chunks matching the plan's suggested commit strategy
  - Run build & verify commands after each task group
  - **Progress tracking (mandatory):**
    - After each task/PR lands: update the ws-plan's task checkboxes
    - After each task: update the parent plan's Progress log with a dated entry
    - After the WS completes: update the parent plan's WS overview table status column
    - After the WS completes: fill in the parent plan's **Implementation Notes** section for that WS
  - Open the PR per the plan's PR workflow
  - If blocked: document in the ws-plan's notes, update status to ⏸️ in the parent plan
- **Note:** User may skip stage 1 if the status is already clear. If skipped, stage 2 should still gather context before creating the plan.

## Template Design

All three templates are generalized versions of the existing SupportAssistant documents, with:
- `<placeholder>` markers for project-specific values
- Helpful descriptions in comments/blockquotes explaining what goes in each section
- Examples where they clarify intent (not overly prescriptive)
- **Implementation Notes** sections in the plan template (per WS, empty, to be filled after implementation)
- **Progress log** sections with clear maintenance instructions

## File Outputs Naming Convention

| Stage | Output Pattern | Example |
|-------|---------------|---------|
| `get-status` | `<project>-integration-status.md` | `auth-integration-status.md` |
| `plan` | `<project>-implementation-plan.md` | `auth-implementation-plan.md` |
| `ws-plan` | `ws<N>-<slug>-development-plan.md` | `ws1-oauth-flow-development-plan.md` |

Output directory: `docs/plans/` by default. If `docs/plans/` doesn't exist, create it.

## Implementation Tasks

1. Create `~/.agents/skills/implementation-steps/` directory structure
2. Write `SKILL.md` — router with stage overview, argument parsing, and reference file pointers
3. Generalize the 3 existing templates:
   - Strip SupportAssistant-specific content (project names, file paths, code snippets)
   - Add helpful descriptions/examples at each `<placeholder>`
   - Add Implementation Notes sections to plan-template.md (per WS)
   - Add clear maintenance instructions for Progress log sections
4. Write `references/get-status.md` — stage 1 instructions (~150 lines)
5. Write `references/plan.md` — stage 2 instructions (~200 lines)
6. Write `references/ws-plan.md` — stage 3 instructions (~150 lines)
7. Write `references/develop-ws.md` — stage 4 instructions (~200 lines)

## Validation

- Verify SKILL.md frontmatter passes `skills-ref validate` naming rules
- Verify SKILL.md body is under 500 lines
- Verify each reference file is under 300 lines
- Verify all template `<placeholder>` markers are consistent
- Verify no SupportAssistant-specific content remains in any file
- Spot-check: describe a hypothetical project and mentally trace each stage's instructions against its template
