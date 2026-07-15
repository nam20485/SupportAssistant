# Plan for New GH Issue Plan and Development Tracking System

## Description

Use GH issues and project functionality to specify and track plans and development for new apps, features, and defects. 

## Strategy

Organize the plans and development into phases, epcis, stories, and tasks using GH issues, sub-issue features, and task checkbox items inside issues (with the checkbox list item/issue link integration connection).

Use GH projects, milestones, labels, and the projects board views to organize the issues, e.g. phases and to easily view project state and completion etc. 

Issue hierarchy relationships are captured using two methods at each level:

1. **Task list** checkbox items are linked to the child issues using GH's task list item/issue linking functionality. ([tasklist docs link](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/about-tasklists)) At each level of the hierarchy, the task list items are linked to the child issues (i.e. plan items -> epics, epics' items -> stories, stories' items -> tasks).

2. **Sub-issues** are linked to the parent issues using GH's sub-issue functionality. (GH docs [sub-issues docs link](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/adding-sub-issues)) At each level of the hierarchy, the sub-issues are linked to the parent issues (i.e. stories are linked to their parent epic issue, and epics are linked to their parent plan issue).

## Plan/Issue Hierarchy Structure

There are four levels of the hierarchy:

1. Main application plan issue
2. Epics
3. Stories
4. Tasks

The top level is the overall complete application plan, which is broken down into epics, the epics are broken down into stories, and the stories are broken down into tasks. Tasks are the atomic units of work.

The top-level plan's epics can optionally be organized by grouping them into phases, for large, multi-part plans, or when the the effort needs to be divided so some work is completed as part of the current development effort, while other work is deferred to a later development effort.

## Creating the Plan/Issue Hierarchy Structure

Create an overall plan issue, and then create sub-issues for epics, with sub-issues for epics' stories, and  another level of sub-issues for the stories' individual tasks. Each issue will have checkbox task lists, whose items represent the child issues (i.e. epics' listg are the stories, and stories' list are the tasks)

**IMPORTANT**: When creating the checkbox child item tasklists inside issues, always use the GH list item/issue link functionality to link the task items to the respective child issues.

**IMPORTANT**: When creating children of an GH issue, always use the GH Sub-Issue functionality to create sub issues instead of manually linking them together by adding  a link to the child issue in the parent issue. 

Create a project and milestones for the plan, and then connect the issues' project and milestone fields to the relevant project and milestone.  

**IMPORTANT**: Always connect issues' project and milestone fields to the relevant project and milestone.

Use GH blocking/blocked by functionality to capture the dependencies between issues. (GH docs [blocking/blocked by docs link](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/creating-issue-dependencies#marking-an-issue-as-blocked-by-or-blocking-another-issue))

**IMPORTANT**: Always use the GH blocking/blocked by functionality to capture the dependencies between issues.

This way, we can easily keep track of our current progress, what's completed, and what's remaining. 

## Implementation of the System

### Outputs

The outputs of this plan are:

#### Skills

Two skills. One for initializing the plan/issue/project/milestone/etc. hierarchy structure in a given GH repo (given GH repo URL or GH repo slug, `$ghrepo`), and another for implementing an issue in a GH repo where this system has been set up (given issue num. or title, `$ghissue`, if not provided, the current issue will be found from the GH plan isses and project board views).

- a new skill for creating the plan/issue/project/milestone/etc. hierarchy structure in a given GH repo (given GH repo URL or GH repo slug, `$ghrepo`)
- a new skill for implementing an issue in a GH repo where this system has been set up (given issue num. or title, `$ghissue`, if not provided, the current issue will be found from the GH plan isses and project board views)

#### Templates

Issue templates for each level of the hierarchy are located in [`docs/plans/gh-issue-tracking/ISSUE_TEMPLATE`](./ISSUE_TEMPLATE):

- **Application Plan** ([`ISSUE_TEMPLATE/application-plan.md`](./ISSUE_TEMPLATE/application-plan.md)) — top-level plan issue covering the overall application: overview, goals, technology stack, features, system architecture, phased implementation plan, mandatory requirements, acceptance criteria, risks, timeline, and success metrics.
- **Epic** ([`ISSUE_TEMPLATE/epic.md`](./ISSUE_TEMPLATE/epic.md)) — epic-level issue scoped to a single project/component: overview, goals, component-specific technology stack, epic stories, component architecture, and a story-based implementation plan.
- **Story** ([`ISSUE_TEMPLATE/story.md`](./ISSUE_TEMPLATE/story.md)) — story-level issue: objective, in/out of scope, task plan, acceptance criteria, validation commands, dependencies, risks & mitigations, test strategy, and rollback steps.
**Task** ([`ISSUE_TEMPLATE/task.md`](./ISSUE_TEMPLATE/task.md)) — task-level issue: description, acceptance criteria, validation commands, dependencies, risks & mitigations, test strategy, and rollback steps.

When creating the plan/issue hierarchy, use the application-plan template for the top-level plan issue, the epic template for each epic sub-issue, and the story template for each story sub-issue, and the task template for each task sub-issue.

#### Scripts

Create scripts for use by the skills so they can be used to initialize the plan/issue/project/milestone/etc. hierarchy structure in a given GH repo (given GH repo URL or GH repo slug. This way each time the skill runs it run deterministically and does not need determine how to create the hierarchy structure and items each time. The scripts should be creatd for each of the oepratirons the scripts need to perfrom, not for the overall skill itself.
