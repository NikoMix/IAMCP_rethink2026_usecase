# Document data schemas

JSON Schemas (draft 2020-12) for the data behind each generated document. They are the contract
between the agent that collects the data, `check_plausibility`, and the renderer.

| File | Content |
| --- | --- |
| `common.schema.json` | Shared `$defs`: parties, contacts, addresses, money, dates, periods, document metadata, deliverables, milestones, payment plan, rate card, roles, assumptions, risks, legal terms |
| `rfi.schema.json`, `rfp.schema.json`, `msa.schema.json`, `sow.schema.json`, `change-request.schema.json` | One schema per document type; each references `common.schema.json` |
| `examples/valid/<type>.example.json` | Fictional data that must validate |
| `examples/invalid/<type>.<defect>.json` | Copies with exactly one defect; `expected-errors.json` names the field path and keyword each must fail on |

Field conventions and the mapping to template placeholders are described in
[`templates/README.md`](../templates/README.md#field-catalog).

## Validating

References are relative (`common.schema.json#/$defs/party`) and resolve against each schema's `$id`.
Register all six files with your validator before evaluating, so nothing is fetched over the network,
and turn on `format` assertion. `tests/ProposalGenerator.Schemas.Tests` shows how to do this with
JsonSchema.Net, and how to turn a `required` error into a field path such as `customer.name`.

```powershell
dotnet test tests/ProposalGenerator.Schemas.Tests/ProposalGenerator.Schemas.Tests.csproj
```

## Changing a schema

1. Edit the schema and, if a template uses the field, the template front matter in the same pull request.
2. Update the valid example so that it still validates and contains every required field.
3. For a new rule, add an invalid example and its entry in `examples/invalid/expected-errors.json`.
4. Run the tests above.
