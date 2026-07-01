---
agent: agent
description: This prompt is to perform a task review against the uncommitted changes in a .NET repository. It will check the implementation against the current Spec Kit task list and perform a code quality review.
model: MAI-Code-1-Flash
tools: [execute, read, edit, search, web, agent, todo]
---

Review the implementation against the current Spec Kit task list.

For each completed task:
- Confirm whether it appears fully implemented.
- Identify any missing work.
- Identify any deviations from the specification or plan.

Then perform a code quality review and identify:
- Bugs
- Security issues
- Maintainability concerns
- Architectural concerns

Ignore stylistic preferences and focus only on issues that would concern an experienced .NET developer during a pull request review.