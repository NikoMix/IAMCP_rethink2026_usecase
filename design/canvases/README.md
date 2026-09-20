# Design canvases

Persisted UI design canvases that enrich user stories with concrete desktop UI.

Each canvas lives in its own directory, named `NNN-kebab-slug`:

```text
design/canvases/
  007-workshop-dashboard/
    canvas.md      # specification, decisions, UI acceptance criteria
    mockup.html    # self-contained desktop mockup
    assets/        # optional exports and images
```

## Reopening a canvas

1. Read `canvas.md` for intent, decisions, open questions, and the linked story.
2. Open `mockup.html` in a browser to inspect the current design.
3. Continue refinement with the Designer agent, which adds a revision rather than overwriting history.

## Linking to work

- `canvas.md` frontmatter records the `story` and optional `epic` it serves.
- The user story issue links back to the canvas directory in its design canvas field.
- UI acceptance criteria in the approved canvas are copied into the story so implementation and tests can verify them.

Templates and the full workflow live in the [ui-design-canvas skill](../../.github/skills/ui-design-canvas/SKILL.md).
