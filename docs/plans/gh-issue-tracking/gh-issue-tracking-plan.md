# Plan for New GH Issue Plan and Development Tracking System

## Description

Use GH issues and project functionality to specify and track plans and development for new apps, features, and defects. 

## Strategy

Organize the plans and development into phases, epcis, stories, and tasks using GH issues, sub-issue features, and task checkbox items inside issues (with the checkbox list item/issue link integration connection).

Use GH projects, milestones, labels, and the projects board views to organize the issues, e.g. phases and to easily view project state and completion etc. 

## Plan/Issue Hierarchy Structure

The top level is the overall complete application plan, which is broken down into epics, the epics are broken down into stories, and the stories are broken down into tasks.

The top-level plan's epics can optionally be organized by grouping them into phases, for large, multi-part plans, or when the the effort needs to be divided into completing part of the work now and the rest later.

The top-level issue is the overall plan, and the individual stories' tasks are the atomic units of work.

## Creating the Plan/Issue Hierarchy Structure

Create an overall plan issue, and then create sub-issues for epics, with sub-issues for epics' stories, and  another level of sub-issues for the stories' individual tasks. Each issue will have checkbox task lists, whose items represent the child issues (i.e. epics' listg are the stories, and stories' list are the tasks)

**IMPORTANT**: When creating the checkbox child item tasklists inside issues, always use the GH list item/issue link functionality to link the task items to the respective child issues.

**IMPORTANT**: When creating children of an GH issue, always use the GH Sub-Issue functionality to create sub issues instead of manually linking them together by adding  a link to the child issue in the parent issue. 

Create a project and milestones for the plan, and then connect the issues' project and milestone fields to the relevant project and milestone.  

**IMPORTANT**: Always connect issues' project and milestone fields to the relevant project and milestone.

This way, we can easily keep track of our current progress, what's completed, and what's remaining. 

## Implementation of the System

### Outputs

#### Skills

- a new skill for creating the plan/issue/project/milestone/etc. hierarchy structure in a given GH repo (given GH repo URL or GH repo slug, `$ghrepo`)
- a new markdown doc for the plan/issue hierarchy structure and implementation instructions (this is instructions for this system and the skills above), NOT for a specific app/feature/etc.
- a new skill for implementing an issue in a GH repo where this system has been set up (given issue num. or title, `$ghissue`, if not provided, the current issue will be found from the GH plan isses and project board views)

## Templates

Use app plan issue template located in the repo, and the other templates if relevant (i.e. story)

## Skills

And a 2nd skill for implementing an issue in a GH repo where this system has been set up. 