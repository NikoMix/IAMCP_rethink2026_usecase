Snapshot of the JSON schemas published by the Data model session (issue #7) on branch
`nikomix-json-schema-data-model`. The six `*.schema.json` files are byte-identical at commits
be0749932b4a81f004a862ed15ca82c65063550f and 91d1219; `../schema-examples/` is a copy of
`schemas/examples/` at 91d1219.

They let the agent tests run against realistic cross-file `$ref`s without depending on
`schemas/` being present on this branch. The tests pass this directory to the loader as the
schema directory override and never read the repository's `schemas/`. Refresh the copies when
the schemas change.
