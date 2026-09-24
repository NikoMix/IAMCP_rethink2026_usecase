# IAMCP_rethink2026_usecase
This is the demo for the IAMCP GitHub Hands-on Workshop

## Use case: commercial proposal generator

An agent on Azure AI Foundry helps create commercial documents from reviewed templates and
exports them as Word (`.docx`) and PDF.

- **Templates:** [`templates/`](templates/) contains first drafts for the Statement of Work, RFI, RFP, Master Service Agreement, and Change Request.
- **Backlog:** the [Commercial Proposal Generator – Backlog](https://github.com/users/NikoMix/projects/18) project contains epics, features, and stories across milestones M0 to M5.

## Getting started

### Prerequisites

- [.NET SDK 9.0](https://dotnet.microsoft.com/download/dotnet/9.0), feature band 9.0.200 or later.
  [`global.json`](global.json) pins `9.0.200` with `rollForward: latestFeature`, so any newer
  9.0.x SDK is used, but a .NET 10 SDK alone is **not** accepted.
- PowerShell 7 (`pwsh`) to run the CI helper script locally (optional).

### Build, lint and test

The solution uses the XML solution format, [`ProposalGenerator.slnx`](ProposalGenerator.slnx)
(supported from SDK 9.0.200). CI runs exactly these commands:

```shell
dotnet restore ProposalGenerator.slnx
dotnet build ProposalGenerator.slnx --no-restore --configuration Release
dotnet format ProposalGenerator.slnx --verify-no-changes --no-restore
dotnet test ProposalGenerator.slnx --no-build --configuration Release --logger trx --results-directory TestResults
pwsh ./.github/workflows/scripts/Assert-TestsExecuted.ps1 -ResultsDirectory TestResults
```

The last step fails when no tests executed, because `dotnet test` exits 0 when a filter matches
nothing. Run the API locally with `dotnet run --project src/ProposalGenerator.Api` and open `/health`.

### Repository layout

| Path | Contents |
| --- | --- |
| `src/ProposalGenerator.<Area>/` | Application projects, for example `ProposalGenerator.Api` (ASP.NET Core minimal API). |
| `tests/ProposalGenerator.<Area>.Tests/` | xUnit test projects, one per source project. |
| `templates/` | Commercial document templates and the field catalog. |
| `.github/workflows/` | CI workflow ([`ci.yml`](.github/workflows/ci.yml)) and its helper scripts. |
| `Directory.Build.props` | Shared settings: `Nullable`, `ImplicitUsings`, `LangVersion latest`, `TreatWarningsAsErrors`. |

### Adding a project

1. Create it under `src/` or `tests/` with an explicit `<TargetFramework>net9.0</TargetFramework>`.
   Do not repeat the settings from `Directory.Build.props`.
2. Use explicit `PackageReference` versions; central package management is not enabled.
3. Add it to the solution: `dotnet sln ProposalGenerator.slnx add <path-to-csproj>`. CI builds and
   tests everything in the solution.
