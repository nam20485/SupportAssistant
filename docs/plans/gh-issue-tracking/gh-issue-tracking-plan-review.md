# Review of GH Issue Tracking Plan

**Date:** 2026-07-13  
**Reviewer:** AI Assistant  
**Document:** [`gh-issue-tracking-plan.md`](./gh-issue-tracking-plan.md)

---

## Summary

The plan proposes a hierarchical issue tracking system using GitHub Issues, Projects, Milestones, and Labels to organize plans and development into plan → phase → epic → story → task structure. The approach leverages native GH features (sub-issues, task lists with issue links) and proposes skills for creating the hierarchy and implementing issues.

---

## Strengths

- **Clear hierarchy**: The plan/phase/epic/story/task structure is explicit and well-defined.
- **Native GH features**: Correctly identifies and uses recent GH features (sub-issues, task list → issue linking).
- **Separation of concerns**: Separates creating the hierarchy from implementing individual issues.
- **Template reference**: Mentions using existing app plan template (though needs clarification).

---

## Critical Gaps and Recommendations

### 1. Missing Issue Templates

**Problem:** The plan references an "app plan issue template located in the repo" but no `.github/ISSUE_TEMPLATE/` directory exists. This is a prerequisite for consistency.

**Recommendation:**
- Create issue templates for each level: `plan.md`, `epic.md`, `story.md`, `task.md`, `defect.md`.
- Templates should include required sections (description, acceptance criteria, dependencies, etc.) and default labels.
- Consider a template hierarchy where the plan template references the phases/epics, etc.

### 2. Label and Milestone Strategy Undefined

**Problem:** The plan mentions using labels and milestones but doesn't specify:
- What labels to create (e.g., `type/plan`, `type/epic`, `type/story`, `type/task`, `type/defect`, `priority/P0-P3`, `area/ai`, `area/ui`, `area/core`, etc.)
- How milestones map to the hierarchy (is a milestone = a phase? an epic? a release?)
- Label conventions for cross-cutting concerns (e.g., `blocked`, `needs-review`, `tech-debt`)

**Recommendation:** Define a label taxonomy and milestone strategy upfront. Example:
- **Type labels:** `plan`, `epic`, `story`, `task`, `defect`, `spike`
- **Priority labels:** `P0` (critical), `P1` (high), `P2` (medium), `P3` (low)
- **Area labels:** `ai/inference`, `ui/avalonia`, `agent`, `security`, `tools`, `docs`
- **Status labels:** `blocked`, `needs-review`, `wontfix`
- **Milestones:** Map to releases (v1.0, v1.1) or phases if phases align with releases

### 3. Dependency and Cross-Cutting Concern Handling

**Problem:** The plan doesn't address:
- How dependencies between epics/stories/tasks are tracked (e.g., "Story A blocked by Story B")
- Cross-cutting work that spans multiple epics (e.g., "add logging infrastructure")
- Parallel work on multiple epics/phases

**Recommendation:**
- Use GH's built-in dependency syntax in issue bodies or leverage the project board's dependency tracking.
- Consider a "foundational" epic for cross-cutting infrastructure work.
- Document how to handle blocked issues (e.g., label them `blocked` and link to the blocking issue).

### 4. Skill Implementation Details Vague

**Problem:** The skill descriptions are high-level:
- "Creating the plan/issue/project/milestone/etc. hierarchy" — no specifics on inputs, outputs, or error handling.
- "Implementing an issue" — unclear how the skill determines "current issue" from project board views, especially if multiple tasks are in progress.

**Recommendation:**