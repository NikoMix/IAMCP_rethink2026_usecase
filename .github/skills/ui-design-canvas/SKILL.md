---
name: ui-design-canvas
description: Create, refine, persist, and reopen desktop UI design canvases that enrich user stories with concrete UI. Use when designing a screen or flow, iterating on a mockup with user feedback, linking a design to a user story, or reopening an existing canvas for implementation.
---

# UI Design Canvas

A canvas is a persisted, reopenable design artifact: a written specification plus a runnable desktop mockup, linked to the user story it serves.

## 1. Locate or create the canvas

Canvases live in `design/canvases/<canvas-id>/`, where `<canvas-id>` is `NNN-kebab-slug` (for example `007-workshop-dashboard`).

Each canvas directory contains:

- `canvas.md` — the specification and decision history. Use [canvas-template.md](./canvas-template.md).
- `mockup.html` — a single self-contained desktop mockup. Use [mockup-template.html](./mockup-template.html).
- `assets/` — optional images or exports.

Before creating anything, search `design/canvases/` for an existing canvas covering the same screen or flow. Reopen and extend it rather than creating a near-duplicate.

To reopen a canvas, read `canvas.md` first for intent, decisions, and open questions, then open `mockup.html` in a browser to inspect the current design.

## 2. Establish the design brief

Confirm and record:

- the linked user story and its acceptance criteria;
- the primary user, their primary task, and the success outcome;
- entry point, exit point, and required data;
- constraints such as permissions, platform, stack, density, and localization.

Ask only questions whose answers change the design. Record anything unresolved under `Open decisions`.

## 3. Design the canvas

Specify, in `canvas.md`:

- layout regions and hierarchy at the target desktop width;
- each component, its purpose, its data, and its behavior;
- interaction flow, including validation and confirmation;
- every state: empty, loading, partial, error, permission-denied, and success;
- keyboard focus order, accessible names, roles, and announcements;
- responsive behavior as the window narrows;
- real interface text, not placeholder filler.

Build `mockup.html` to match the specification. Keep it self-contained with no external network dependencies so it opens reliably from the repository.

## 4. Refine with the user

1. Render the mockup and show it to the user.
2. Present open decisions as a short numbered list, each with a recommendation and its tradeoff.
3. Apply feedback narrowly. Do not redesign unrelated areas.
4. Add a row to `Revision history` and record each accepted decision with its rationale under `Design decisions`.
5. Repeat until the user approves, then set `status: approved`.

## 5. Link the canvas to the story

Keep both directions traceable:

- in `canvas.md` frontmatter, set `story` to the issue number or URL;
- in the user story issue, link the canvas path under the design canvas field;
- derive UI acceptance criteria from the approved canvas and add them to the story as observable, testable statements.

Example UI acceptance criteria:

```markdown
- [ ] Given no records exist, when the dashboard loads, then the empty state and its primary action are shown.
- [ ] Given the request fails, when the error state is shown, then the failure reason and a retry action are available.
- [ ] Given keyboard-only navigation, when tabbing through the toolbar, then focus order follows the visual order and focus is always visible.
```

## 6. Hand off for implementation

Report the canvas path, the approved revision, the UI acceptance criteria, the open decisions that remain, and any component or token reuse the Developer should follow. Do not commit changes or modify the issue without explicit approval.
