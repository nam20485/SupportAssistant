# gh-issue-tracking-init

An AI agent skill that initializes (or re-syncs) a GitHub issue-based planning
hierarchy for a repository: a `plan → epic → story → task` tree of linked
sub-issues, backed by a GitHub Project (v2) board, milestones, and labels.

This file is the human-facing overview. For the agent-facing trigger/orchestration
contract, see [`SKILL.md`](./SKILL.md). For script-level reference, see
[`scripts/README.md`](./scripts/README.md).

---

## What it does

Given a target repository (`$ghrepo`) and a plan (a filled plan document, or a
description of the epics/stories/tasks), this skill builds out:

1. **A canonical label taxonomy** — level labels (`plan`, `epic`, `story`, `task`),
   priority labels (`P0`–`P3`), area labels, and cross-cutting status labels
   (`blocked`, `needs-review`, `wontfix`).
2. **Milestones** — conceptual work groups (e.g. `POC`, `MVP`, `UI`, `Server`),
   assigned to each epic and all of its descendants.
3. **A GitHub Project (v2) board** with custom fields (`Level`, `Priority`,
   `Estimate`, and an optional `Phase`) for tracking work.
4. **The issue hierarchy itself** — one GitHub issue per plan/epic/story/task,
   created from the templates in
   [`docs/plans/gh-issue-tracking/ISSUE_TEMPLATE/`](../../../docs/plans/gh-issue-tracking/ISSUE_TEMPLATE/),
   with numbered titles (`Plan: <Name>`, `Epic 1: <Name>`, `Story 1.1: <Name>`,
   `Task 1.1.1: <Name>`).
5. **Sub-issue links** connecting every parent to its children.
6. **Blocking/blocked-by dependencies** between issues, where the plan calls for them.

The full design rationale (why sub-issues instead of task lists, why phases and
milestones are separate axes, the label taxonomy, etc.) lives in
[`docs/plans/gh-issue-tracking/gh-issue-tracking-plan.md`](../../../docs/plans/gh-issue-tracking/gh-issue-tracking-plan.md),
with the decision history in
[`gh-issue-tracking-plan-feedback.md`](../../../docs/plans/gh-issue-tracking/gh-issue-tracking-plan-feedback.md).

## How it works

### Architecture

```
gh-issue-tracking-init/
├─ README.md         <- you are here (human overview)
├─ SKILL.md           <- agent-facing trigger + orchestration contract
└─ scripts/           <- self-contained PowerShell operation scripts
   ├─ common.ps1                 shared helpers (auth, gh wrapper, id lookups)
   ├─ common-auth.ps1            vendored: gh CLI auth bootstrap
   ├─ import-labels.ps1          vendored: generic label import/sync
   ├─ create-milestones.ps1      vendored: generic milestone creation
   ├─ labels.json                the canonical label taxonomy (data)
   ├─ ensure-labels.ps1          op: sync labels.json to a repo
   ├─ ensure-project.ps1         op: create/link Project + custom fields
   ├─ ensure-issue.ps1           op: idempotent create/update one issue
   ├─ link-sub-issue.ps1         op: attach a child issue as a sub-issue
   ├─ set-project-fields.ps1     op: add issue to board + set field values
   ├─ set-dependency.ps1         op: record a "blocked by" relationship
   ├─ README.md                  script-level reference + smoke test
   └─ tests/
      └─ GhIssueTracking.Tests.ps1   Pester suite (29 tests)
```

- **The skill** (`SKILL.md`) is what an AI agent reads to decide *what* to build
  from a plan and in what order. It doesn't contain any executable code — it's
  a Markdown contract describing inputs, conventions, and an ordered sequence of
  script invocations.
- **The scripts** (`scripts/`) do the actual work. There is **one script per
  discrete GitHub operation** (not one big script), so the skill composes small,
  independently-testable pieces rather than running one monolithic routine.
- **Self-contained:** `common-auth.ps1`, `import-labels.ps1`, and
  `create-milestones.ps1` are vendored (copied) into `scripts/` from the
  repository-root `scripts/` directory, where general-purpose copies also live.
  This skill has zero dependency on anything outside its own folder — you can
  copy `gh-issue-tracking-init/` into another repo and it will work unmodified.

### Design principles

- **Sub-issues are the only hierarchy mechanism.** GitHub's older "tasklist
  block" feature (which linked checklist items to child issues) was retired in
  April 2025 in favor of native sub-issues. This skill uses sub-issues
  exclusively for parent↔child relationships; plain markdown checklists are
  only used for in-issue items like acceptance criteria, never for hierarchy.
- **Idempotent by design.** Every script matches what already exists (by exact
  issue title, by label name, by milestone title, by existing sub-issue link,
  by existing dependency) and skips or updates rather than duplicating. The
  whole skill can be re-run safely after the plan changes.
- **Dry-run everywhere.** Every operation script accepts `-DryRun` and prints
  exactly what it *would* do without mutating anything.
- **Two independent grouping axes.** *Phase* (an optional Project field) is for
  pushing large chunks of work into a future development effort; *Milestone*
  (a native GitHub milestone) is for conceptual groups like `MVP` or `UI`. An
  epic can carry both, independently.
- **Status lives on the board, not in labels.** Workflow state (`Todo`, `In
  Progress`, `Done`, ...) is the Project's built-in `Status` field — labels are
  reserved for level, priority, area, and cross-cutting flags.

## How to use it

### As an AI agent skill

Ask an agent that has this skill available to set up issue tracking for a repo,
e.g.:

> "Set up GH issue tracking for `owner/repo` using this plan: ..."
> "Initialize the plan/issue hierarchy in `owner/repo`."

The agent reads [`SKILL.md`](./SKILL.md), parses your plan into the numbered
plan→epic→story→task structure, and composes the scripts below — always doing a
`-DryRun` pass first and showing you the plan before applying it.

### Manually, by running the scripts directly

You don't need an AI agent to use this — every script works standalone. Run
them from the repository root (or adjust the path):

```pwsh
$Skill = './.agents/skills/gh-issue-tracking-init/scripts'
$Repo  = 'owner/repo'
$Owner = 'owner'

# 1. Labels, milestones, and the Project (always preview first)
& "$Skill/ensure-labels.ps1"     -Repo $Repo -DryRun
& "$Skill/ensure-project.ps1"    -Owner $Owner -Repo $Repo -DryRun
& "$Skill/create-milestones.ps1" -Repo $Repo -Titles 'MVP' -DryRun

# 2. Apply once you're happy with the preview (drop -DryRun)
& "$Skill/ensure-labels.ps1"     -Repo $Repo
& "$Skill/ensure-project.ps1"    -Owner $Owner -Repo $Repo   # note the printed project number
& "$Skill/create-milestones.ps1" -Repo $Repo -Titles 'MVP'

# 3. Create issues top-down (each call prints the new/matched issue number)
$epic = & "$Skill/ensure-issue.ps1" -Repo $Repo -Title 'Epic 1: Core' -BodyFile ./epic1.md -Labels epic -Milestone MVP

# 4. Link hierarchy, set board fields, and record dependencies
& "$Skill/link-sub-issue.ps1"     -Repo $Repo -ParentNumber $plan -ChildNumber $epic
& "$Skill/set-project-fields.ps1" -Owner $Owner -ProjectNumber 3 -Repo $Repo -IssueNumber $epic -Level epic -Priority P1 -Status Todo
& "$Skill/set-dependency.ps1"     -Repo $Repo -IssueNumber $task -BlockedByNumber $story
```

See [`scripts/README.md`](./scripts/README.md) for the full operation reference
(every script's parameters) and a complete worked example that builds a small
plan→epic→story→task tree end to end.

### Prerequisites

- **PowerShell 7+** (`pwsh`).
- **GitHub CLI (`gh`)**, authenticated (`gh auth status`) with the **`project`**
  scope. `$GITHUB_TOKEN` is honored automatically.
- Sub-issues, issue dependencies, and Projects v2 fields are driven through
  `gh api` / `gh project`, so this works even on `gh` versions that predate
  `gh issue create --parent` (verified against `gh` 2.46.0).

## Tests and how to use them

The Pester suite lives at
[`scripts/tests/GhIssueTracking.Tests.ps1`](./scripts/tests/GhIssueTracking.Tests.ps1)
and covers, **with no repo mutation**:

- `common.ps1`'s helper functions (`Get-RepoParts`, `Find-IssueNumberByTitle`,
  `Get-IssueDbId`, `Invoke-GhJson`) with `gh` fully mocked.
- The label taxonomy in `labels.json` (canonical labels present; workflow-state
  labels like `in-progress`/`done` intentionally absent).
- Every operation script's **contract**: it parses without errors, it exposes a
  `-DryRun` switch, and it rejects a malformed `-Repo`.
- **Self-containment**: the three vendored helper scripts are actually present
  in this directory (not just referenced from elsewhere).

Run the full suite from the repository root:

```bash
pwsh -NoProfile -Command "Invoke-Pester -Path .agents/skills/gh-issue-tracking-init/scripts/tests -Output Detailed"
```

Or with a plain pass/fail summary:

```bash
pwsh -NoProfile -Command "Invoke-Pester -Path .agents/skills/gh-issue-tracking-init/scripts/tests"
```

**What the Pester suite does *not* cover:** actually creating/updating things
on GitHub — that requires a real, authenticated `gh` session against a real
repository. For that, run the **smoke test** documented in
[`scripts/README.md`](./scripts/README.md#smoke-test-run-against-a-throwaway-repo):
it walks through building and validating a complete tiny hierarchy
(plan → epic → story → task, with sub-issue links, board fields, and a
dependency) against a **throwaway test repository** — always preview with
`-DryRun` first, and never point it at a production repo. Re-running the same
steps should report skips/updates rather than creating duplicates, which is the
idempotency guarantee in action.

## Known limitations

- **Project views can't be created via `gh`/API.** `ensure-project.ps1` prints
  the four intended views (By Phase, By Status, By Epic, Current work) for you
  to add once in the Project UI.
- **Built-in `Status` field options.** `gh` 2.46 can't add options to an
  existing single-select field, so extra `Status` values (`In Review`,
  `Blocked`) may need a one-time manual add in the UI.

## Out of scope (for now)

- **Defects/bugs** — no `defect` template or label yet.
- **The issue-implementation skill** — a separate, deferred skill that would
  pick the "current" issue to work on and implement it; this skill only builds
  the hierarchy.

## Related documents

- [`docs/plans/gh-issue-tracking/gh-issue-tracking-plan.md`](../../../docs/plans/gh-issue-tracking/gh-issue-tracking-plan.md) — the full design plan.
- [`docs/plans/gh-issue-tracking/gh-issue-tracking-plan-feedback.md`](../../../docs/plans/gh-issue-tracking/gh-issue-tracking-plan-feedback.md) — the decision record behind the design.
- [`docs/plans/gh-issue-tracking/gh-issue-tracking-post-implementation.md`](../../../docs/plans/gh-issue-tracking/gh-issue-tracking-post-implementation.md) — what was actually built, verified facts, and known limitations.
- [`docs/plans/gh-issue-tracking/ISSUE_TEMPLATE/`](../../../docs/plans/gh-issue-tracking/ISSUE_TEMPLATE/) — the four issue body templates used by `ensure-issue.ps1`.
