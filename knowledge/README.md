# Knowledge base

Company knowledge the proposal agent uses via **Foundry File Search**. It is also the source of the rate card the
`check_plausibility` tool uses to verify daily rates
(see [`src/ProposalGenerator.Validation`](../src/ProposalGenerator.Validation/README.md)).

> **All content in this folder is fictional sample data** for the IAMCP GitHub hands-on workshop.
> Fabrikam Consulting GmbH and every customer named here are invented. No real companies, people or personal data
> appear here. The rates are not an offer. Replace the content before any real use.

## Content

| File | Format | Content |
| --- | --- | --- |
| [`service-catalogue.md`](service-catalogue.md) | Markdown with YAML front matter | Catalogue `SC-2026` with ten services (`SVC-01` to `SVC-10`). Each service has a summary, typical deliverables, roles and effort, duration, customer dependencies, out-of-scope items and the preferred pricing model. It deliberately has no prices. |
| [`rate-card.json`](rate-card.json) | JSON | Rate card `RC-2026`: 18 roles with `id`, `role`, `aliases`, `daily_rate` and `description`. Rates are net EUR per person day and valid from `2026-01-01` to `2026-12-31`. |
| [`reference-projects.md`](reference-projects.md) | Markdown with YAML front matter | Four reference projects (`REF-2025-01` to `REF-2024-04`), each with customer and industry, whether it may be named, period, services, team, challenge, solution and outcome. |

Only `.md` and `.json` files are ingested. Both formats work with File Search. This README, other `README.*` files
and anything whose name starts with a dot (for example `.gitattributes`) are skipped. Subfolders are allowed. Their
files are uploaded with `/` replaced by `_` in the file name.

Stable IDs (`SVC-…`, `REF-…`, role `id`) let the agent cite a source precisely. Headings and short paragraphs keep
File Search chunks self-contained.

### Rate card format

The `check_plausibility` tool reads `rate-card.json` with `RateCardLoader`, so these fields are required:

```json
{
  "rate_card_id": "RC-2026",
  "currency": "EUR",
  "valid_from": "2026-01-01",
  "valid_to": "2026-12-31",
  "roles": [
    { "id": "developer", "role": "Developer", "aliases": ["Software Developer"], "daily_rate": 1000 }
  ]
}
```

- Dates use the `YYYY-MM-DD` format, and both bounds are inclusive. A document is checked against the rate card that
  is valid on its `document.date`.
- Role names are matched against `id`, `role` and `aliases`, ignoring case and extra whitespace.
- Other fields such as `$comment`, `notes` and `description` are only for people and for File Search.

## Replacing the sample data with real data

1. Replace the files, or add new `.md`/`.json` files, keeping the structure above. Keep one topic per heading and
   keep the stable IDs.
2. Remove the "fictional" labels, the `sample_data: true` front matter and the `$comment` in the rate card.
3. For a new price list, give the rate card a new `rate_card_id` and new validity dates. Documents dated outside
   the validity period get the error `RATE_CARD_NOT_VALID`.
4. Include only information the agent may put into customer documents. Do not add personal data, secrets or
   internal-only margins; everything in this folder can appear in generated proposals.
5. Run `dotnet test tests/ProposalGenerator.Knowledge.Tests/ProposalGenerator.Knowledge.Tests.csproj`.
   `ShippedKnowledgeTests` checks the rate card structure and that sample files are labelled fictional, so update
   that test once the content is real.
6. Ingest again (see below). Only changed files are uploaded.

## Ingesting into Foundry

`src/ProposalGenerator.Knowledge` is a console tool that creates the vector store if it does not exist and keeps it
in sync with this folder. It uses the official Foundry SDK (`Azure.AI.Projects` with `Azure.AI.Extensions.OpenAI`) and
signs in keylessly with `DefaultAzureCredential`.

Prerequisites:

- The .NET 9 SDK, and a Foundry project created by the infrastructure in `infra/`.
- A signed-in identity (`az login`, or `azd auth login`) with the **Azure AI User** role (or higher) on the Foundry
  project.
- The project endpoint, for example
  `https://<resource>.services.ai.azure.com/api/projects/<project>`, in `AZURE_AI_PROJECT_ENDPOINT` or passed as
  `--endpoint`.

```powershell
# List the files and content hashes without connecting to Azure
dotnet run --project src/ProposalGenerator.Knowledge -- --local-only

# Show what would change, without changing anything
dotnet run --project src/ProposalGenerator.Knowledge -- --dry-run

# Create or update the vector store
dotnet run --project src/ProposalGenerator.Knowledge --

# Also remove files that were deleted from this folder
dotnet run --project src/ProposalGenerator.Knowledge -- --prune
```

| Option | Default | Meaning |
| --- | --- | --- |
| `--endpoint <url>` | `$AZURE_AI_PROJECT_ENDPOINT` | Foundry project endpoint (https only). |
| `--source <directory>` | `knowledge` | Folder to ingest. |
| `--vector-store-name <name>` | `proposal-generator-knowledge` | Vector store to create or update. |
| `--prune` | off | Remove managed files whose source file no longer exists. Without it they are reported as `Kept`. |
| `--dry-run` | off | Compare and report only. |
| `--local-only` | off | Discover and hash files locally; no Azure connection. |

Exit codes: `0` success, `1` ingestion failed (message on stderr), `2` invalid arguments, `130` cancelled.

### How the synchronisation works

- Each file is stored with the attributes `managed_by=proposal-generator-knowledge`, `source_path` and
  `content_sha256`. The SHA-256 is computed after normalising line endings to LF, so a checkout with CRLF produces
  the same hash.
- A file whose hash is already in the store is **Unchanged** and is not uploaded again. Running the tool twice in a
  row changes nothing.
- A changed file is uploaded and indexed first. Only then is the old version detached and deleted, so File Search
  never lacks the file. If indexing fails, the new upload is removed and the old version stays.
- Files in the store without `managed_by=proposal-generator-knowledge`, for example uploaded by hand, are never
  touched.
- More than one vector store with the same name is an error. The tool does not guess which one to use.

### Using the store in the agent

Attach the vector store to the agent's File Search tool, for example
`ResponseTool.CreateFileSearchTool([vectorStoreId])`. The agent definition lives in `agent/` (issues #16 and #18). The
store ID is printed by the ingestion run and belongs in configuration, not in source control.
