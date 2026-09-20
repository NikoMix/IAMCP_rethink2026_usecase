# Repository Copilot Instructions

## Project context

- This repository supports the IAMCP GitHub Hands-on Workshop demo.
- Treat the repository as the source of truth. Inspect the current files, workflows, and conventions before proposing or making changes.
- Keep guidance technology-neutral until the repository contains an application stack or the task explicitly selects one.

## Working principles

- Clarify requirements that materially affect behavior, scope, security, cost, or architecture.
- Prefer small, complete changes over broad rewrites.
- Preserve existing behavior unless the request explicitly changes it.
- Reuse established patterns and dependencies before introducing new ones.
- Do not add secrets, credentials, tokens, personal data, or environment-specific values to source control.
- Call out assumptions, risks, and unresolved decisions in the final response.

## Implementation workflow

1. Read the relevant documentation and code before editing.
2. Identify acceptance criteria and affected surfaces.
3. Implement the smallest coherent solution that satisfies those criteria.
4. Add or update tests and documentation directly related to the change.
5. Run the narrowest relevant validation first, then broader checks when warranted.
6. Report changed files, validation performed, and any remaining risks.

## Quality standards

- Favor readable, maintainable, and type-safe code.
- Validate inputs at trust boundaries and surface failures explicitly.
- Avoid silent fallbacks, broad exception handling, speculative abstractions, and unrelated cleanup.
- Keep public interfaces backward compatible unless a breaking change is requested and documented.
- Make tests deterministic, independent, and focused on observable behavior.
- Keep documentation concise, task-oriented, and consistent with actual behavior.

## GitHub collaboration

- Write issues with a clear problem statement, user or business value, scoped acceptance criteria, dependencies, and test notes.
- Keep pull requests focused and explain why the change is needed, how it works, and how it was validated.
- Never merge, deploy, publish, close issues, or submit irreversible changes without explicit user authorization.
- When using GitHub or other MCP tools, begin with read-only operations and request confirmation before destructive or externally visible actions.

## Role coordination

- The Backlog Analyst refines needs into implementation-ready work without inventing product decisions.
- The Designer produces and refines UI design canvases, persists them under `design/canvases/`, and links them to the user stories they enrich.
- The Developer implements approved behavior and coordinates required tests and documentation.
- The Test Engineer derives risk-based tests from acceptance criteria and reports reproducible failures.
- The Technical Writer documents verified behavior for the intended audience.
- The Service Reliability Engineer evaluates operability, observability, failure modes, and safe rollout.
- When work crosses roles, preserve traceability from requirement to design, implementation, tests, documentation, and operational readiness.
- For user-facing changes, an approved design canvas is the source of truth for UI behavior, states, and accessibility expectations.

## Exclusions
- Never attempt any update on CODE_OF_CONDUCT.md, CONTRIBUTING.md, LICENSE, SECURITY.md or SUPPORT.md

## Validation

- Use repository-provided build, lint, test, and documentation commands when they exist.
- Do not claim a check passed unless it was run successfully.
- If validation cannot run, state the exact command attempted, the blocker, and the residual risk.
