---
name: implementation-workflow
description: Implement a scoped code or configuration change from investigation through verification. Use for features, bug fixes, refactoring required by a task, integrations, or repository configuration changes.
---

# Implementation Workflow

1. Translate the request into explicit acceptance criteria and constraints.
2. Inspect relevant code, tests, configuration, and documentation. Search for existing patterns before adding new ones.
3. Identify all affected surfaces, including callers, public contracts, persistence, configuration, tests, and user documentation.
4. Make the smallest coherent change that fully addresses the requirement.
5. Preserve type safety, compatibility, error visibility, and security boundaries.
6. Add or update targeted tests and directly related documentation.
7. Run formatting, targeted tests, and the narrowest relevant build or lint command. Escalate validation when failures or risk warrant it.
8. Review the diff for unrelated edits, accidental secrets, generated artifacts, and incomplete changes.
9. Summarize the outcome, validation, and residual risks without claiming checks that were not run.
