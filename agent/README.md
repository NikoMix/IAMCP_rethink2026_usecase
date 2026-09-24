# Proposal agent definition

The proposal agent collects the data for one commercial document in a dialog and returns JSON that
the generator merges into the templates. This directory is its versioned, declarative definition.
`src/ProposalGenerator.Agent` loads and validates it, deploys it to the Foundry Agent Service, and
validates every reply at run time.

| File | Purpose |
| --- | --- |
| `proposal-agent.yaml` | Name, version, description, model, tools, and response format |
| `instructions.md` | System instructions: role, document types, dialog and legal rules, languages |
| `response-envelope.schema.json` | JSON Schema of every reply (status, questions, document, ...) |
| `tools/check_plausibility.parameters.json` | Parameters of the `check_plausibility` function tool |

## Definition format

`proposal-agent.yaml` uses `schemaVersion: 1`. Paths are relative to the YAML file. Unknown keys,
missing files, invalid JSON or JSON Schema, duplicate JSON properties, unresolvable `$ref`s, and
unset configuration values all fail the load with an `AgentDefinitionException` that lists every
problem at once.

Environment-specific values are `${NAME}` references, resolved from environment variables at
deploy time. A field is either a literal or exactly one reference.

| Variable | Used for |
| --- | --- |
| `FOUNDRY_MODEL_DEPLOYMENT_NAME` | `model.deployment`, the model deployment in the Foundry project |
| `FOUNDRY_VECTOR_STORE_ID` | `file_search` vector store with the knowledge base (#19) |
| `FOUNDRY_PROJECT_ENDPOINT` | Foundry project endpoint (CLI only, not part of the definition) |

Increase `version` when you change the definition. The deployer detects changes by content, but the
version is recorded in the agent metadata as `definition-version`.

## Deploying

```powershell
$env:FOUNDRY_MODEL_DEPLOYMENT_NAME = "<deployment>"
$env:FOUNDRY_VECTOR_STORE_ID = "<vector store id>"

# Validate and print the desired state; no Azure call.
dotnet run --project src/ProposalGenerator.Agent.Cli -- deploy --dry-run

# Create or update the agent. Authenticates with DefaultAzureCredential (az login, managed identity).
$env:FOUNDRY_PROJECT_ENDPOINT = "https://<account>.services.ai.azure.com/api/projects/<project>"
dotnet run --project src/ProposalGenerator.Agent.Cli -- deploy
```

Options: `--definition <path>` (default `agent/proposal-agent.yaml`) and `--schemas <directory>`
(overrides `responseFormat.schemaDirectory`, default `../schemas`). Exit codes: `0` success,
`1` invalid definition, `2` usage or missing endpoint, `3` the service rejected the request.

The deploy is idempotent. The deployer computes a SHA-256 fingerprint over the canonical desired
state (name, description, model, parameters, instructions, tools, response format) and stores it
in the agent version metadata as `definition-sha256`.

- No agent with the name exists: version 1 is created (`Created`).
- The latest version has the same fingerprint, model, and instructions: nothing is written (`Unchanged`).
- Anything differs, including instructions edited in the portal: a new version is created (`Updated`).

Instructions are normalised to LF line endings, so the git checkout settings do not affect the fingerprint.

The SDK is `Azure.AI.Projects.Agents` (`AgentAdministrationClient`). All calls go through
`IFoundryAgentClient`, so the tests use an in-memory fake and never contact Azure.

## Response format

The model gets a single self-contained JSON schema, built from the envelope and the document
schemas in `schemas/<doctype>.schema.json`:

- Each document schema, and every file it references (for example `common.schema.json`), is
  embedded under `$defs/<file key>`. Every `$ref` is rewritten to a local pointer, and `$id` and
  `$schema` are removed.
- `documentType` becomes an enum of the configured document types plus `null`.
- `document` becomes `anyOf` the document schemas or `null`.

Strict mode is off (`strict: false`). Strict structured outputs require every property to be
required, and they do not support `pattern`, `format`, or `minItems`, all of which the document
schemas use. The model therefore gets the schema as guidance, and the application enforces it.

## Validating replies

`StructuredAgentConversation` sends a user message and returns only validated replies. For each reply:

1. The text must be one JSON object without duplicate properties, valid against the envelope schema.
2. For `awaiting_confirmation` and `complete`, the agent must have called `check_plausibility` in
   this turn. The numbers in its `computed` output overwrite the model's values at the given paths.
   `complete` is rejected while an `error` finding is open.
3. The document is validated against the **original** schema of its type, not against the bundle.

An invalid reply triggers a repair message in the same conversation. The message lists each failing
JSON Pointer and its validation message. After `MaxRepairAttempts` corrections (default 2, allowed
range 0 to 10), `StructuredOutputException` reports the errors, the failing field paths, the number of
attempts, and the last output. Invalid output is never returned.

## `check_plausibility` contract

The Knowledge & plausibility session (#20) implements the handler.

- Input: `{ "documentType": "<doctype>", "document": { ... } }`
- Output: `{ "findings": [{ "code", "severity": "error|warning|info", "path", "message" }], "computed": { "<path>": <number> } }`

Paths in `computed` and `findings` are relative to the document. They can be JSON Pointers
(`/pricing/total`) or dotted paths (`$.pricing.total`, `pricing.payment_schedule[0].amount`).

## Testing

```powershell
dotnet test tests/ProposalGenerator.Agent.Tests/ProposalGenerator.Agent.Tests.csproj
```

The tests are offline. They use a snapshot of the schemas and of the Data model examples in
`tests/ProposalGenerator.Agent.Tests/Fixtures`. The projects target `net9.0`. On a machine with
only a newer runtime, set `DOTNET_ROLL_FORWARD=Major` for the test run.

Offline tests cannot cover how the model behaves: refusing legal advice, asking instead of
inventing, choosing the language. The instructions state these rules; verify them with the
evaluation set (#39) against a deployed agent.
