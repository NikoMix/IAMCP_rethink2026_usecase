# Infrastructure: Microsoft Foundry environment (Bicep + azd)

This folder provisions a complete, keyless environment for the proposal generator with one command. Every
service uses Microsoft Entra ID. API keys, shared keys and admin credentials are disabled, and the
deployment outputs contain endpoints and names only.

| Resource | Module | Purpose |
| --- | --- | --- |
| Resource group `rg-<env>` | `main.bicep` | Holds everything. `azd down` deletes it. |
| Microsoft Foundry resource (`Microsoft.CognitiveServices/accounts`, kind `AIServices`) | `modules/foundry.bicep` | Hosts models and the Agent Service. `disableLocalAuth: true`. |
| Foundry project | `modules/foundry.bicep` | Workspace for the proposal agent. It has its own system-assigned identity. |
| Model deployment `chat` | `modules/foundry-model-deployment.bicep` | Default `gpt-5.4-mini` `2026-03-17`, `DataZoneStandard`, 50K TPM. |
| Storage account | `modules/storage.bicep` | Containers `documents` and `drafts`. Shared key and public access are disabled. Drafts expire automatically. |
| Log Analytics + Application Insights | `modules/monitoring.bicep` | Telemetry. Local auth is disabled, so ingestion needs an Entra ID token. |
| Container Apps environment + container registry | `modules/container-apps.bicep` | Hosting for the API and Blazor app. Logs go through diagnostic settings, not workspace keys. The registry admin user is off. |
| User-assigned managed identity `id-app-*` | `modules/identity.bicep` | The identity the application runs as. |
| Role assignments | `modules/role-assignments.bicep` | Least-privilege RBAC. See [Roles granted](#roles-granted). |

Private endpoints and network hardening are out of scope (issue #3, non-goals).

## Prerequisites

- An Azure subscription. Your account needs **Owner**, or **Contributor** plus **Role Based Access Control
  Administrator**, on the subscription, because the template creates a resource group and role assignments.
- [Azure Developer CLI (`azd`)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd).
- Optional: [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) for the verification commands below.
- Model quota for the default model in the chosen region: `gpt-5.4-mini`, deployment type Data Zone Standard, at
  least the configured capacity (default 50K tokens per minute). Check it in the Foundry portal under
  **Quota**, or lower `FOUNDRY_MODEL_CAPACITY`.

## Provision with one command

```powershell
azd auth login
azd up
```

`azd up` prompts for an environment name, a subscription and a location. Only EU regions are offered:
`francecentral`, `germanywestcentral`, `italynorth`, `spaincentral`, `swedencentral` and `westeurope`. Each of
them supports Foundry projects, the Agent Service and the default model as Data Zone Standard (Microsoft Learn,
*Region availability for Foundry Models sold by Azure*, checked 2026-09-24).

No services are deployed yet. `azure.yaml` gets a `services` entry once `src/ProposalGenerator.Api` exists, so
`azd up` currently provisions infrastructure only.

To provision without prompts, set the values first:

```powershell
azd env new proposal-dev
azd env set AZURE_LOCATION swedencentral
azd provision
```

### Configuration

All parameters come from azd environment variables (`azd env set <NAME> <value>`). Nothing environment-specific
is committed. `infra/main.parameters.json` contains placeholders only.

| Variable | Default | Meaning |
| --- | --- | --- |
| `AZURE_ENV_NAME` | prompted | Environment name. Resource names are derived from it. |
| `AZURE_LOCATION` | prompted | EU region (see above). |
| `AZURE_RESOURCE_GROUP` | `rg-<env>` | Resource group name. |
| `AZURE_PRINCIPAL_ID` | set by azd | Your object ID. It receives developer roles. Set it to an empty value to skip them. |
| `AZURE_PRINCIPAL_TYPE` | `User` | Set `ServicePrincipal` when a pipeline identity provisions. |
| `FOUNDRY_PROJECT_NAME` | `proposal-generator` | Foundry project name. |
| `FOUNDRY_MODEL_DEPLOYMENT_NAME` | `chat` | Deployment name the app uses. |
| `FOUNDRY_MODEL_NAME` / `FOUNDRY_MODEL_VERSION` | `gpt-5.4-mini` / `2026-03-17` | Model. Retirement is scheduled for 2027-09-21. |
| `FOUNDRY_MODEL_SKU` | `DataZoneStandard` | `DataZoneStandard` (inference within the EU data zone) or `Standard` (within the region). Global types are not allowed. |
| `FOUNDRY_MODEL_CAPACITY` | `50` | Thousand tokens per minute. |
| `APP_FOUNDRY_ROLE` | `Foundry User` | `Foundry Agent Consumer` is enough if the app only calls agents that already exist. |
| `STORAGE_DRAFT_RETENTION_DAYS` | `30` | Blobs in `drafts` are deleted this many days after their last change. |
| `STORAGE_SOFT_DELETE_DAYS` | `7` | Restore window for deleted blobs and containers. |
| `LOG_RETENTION_DAYS` | `30` | Log Analytics and Application Insights retention. |

The retention defaults are working assumptions until the data protection concept (issue #41) sets binding values.

### Outputs

After provisioning, `azd env get-values` shows these values. They are endpoints, names and the identity's client
ID. None of them is a key, a SAS token or a connection string.

`FOUNDRY_ENDPOINT`, `FOUNDRY_OPENAI_ENDPOINT`, `FOUNDRY_PROJECT_ENDPOINT`, `FOUNDRY_PROJECT_NAME`,
`FOUNDRY_MODEL_DEPLOYMENT_NAME`, `FOUNDRY_RESOURCE_NAME`, `STORAGE_ACCOUNT_NAME`, `STORAGE_BLOB_ENDPOINT`,
`STORAGE_DOCUMENTS_CONTAINER`, `STORAGE_DRAFTS_CONTAINER`, `APPLICATIONINSIGHTS_NAME`,
`LOG_ANALYTICS_WORKSPACE_NAME`, `AZURE_CONTAINER_APPS_ENVIRONMENT_NAME`, `AZURE_CONTAINER_APPS_ENVIRONMENT_ID`,
`AZURE_CONTAINER_REGISTRY_NAME`, `AZURE_CONTAINER_REGISTRY_ENDPOINT`, `APP_IDENTITY_ID`, `APP_IDENTITY_NAME`,
`APP_IDENTITY_CLIENT_ID`, `AZURE_LOCATION`, `AZURE_RESOURCE_GROUP`.

The Application Insights connection string is deliberately not an output, because it embeds the instrumentation
key. Local auth is disabled, so that key cannot ingest telemetry on its own. The container app definition will
read the connection string directly from the resource. For local telemetry, run
`az monitor app-insights component show -g <rg> -a <APPLICATIONINSIGHTS_NAME> --query connectionString -o tsv`
and configure the exporter with an Entra ID credential.

### Re-running is idempotent

Resource names come from `uniqueString(subscription, environment name, location)`, and role assignment names come
from `guid(scope, principal, role)`. Running `azd up` or `azd provision` again converges to the same state
without creating duplicates. `azd provision --preview` shows the what-if result before any change is made.

## Roles granted

| Principal | Role | Scope | Why |
| --- | --- | --- | --- |
| App identity `id-app-*` | Foundry User (`53ca6127-db72-4b80-b1b0-d745d6d5456d`), or Foundry Agent Consumer (`eed3b665-ab3a-47b6-8f48-c9382fb1dad6`) via `APP_FOUNDRY_ROLE` | Foundry **project** | Create and run agents and call the model. Grants no management rights. |
| App identity | Storage Blob Data Contributor (`ba92f5b4-2d11-453d-a403-e96b0029c9fe`) | Containers `documents` and `drafts` only | Read and write generated documents. Grants no account-level or key access. |
| App identity | Monitoring Metrics Publisher (`3913510d-42f4-4e42-8a64-420c390055eb`) | Application Insights component | Required for keyless telemetry ingestion. |
| App identity | AcrPull (`7f951dda-4ed3-4680-a7ca-43fe172d538d`) | Container registry | Pull the app image without registry credentials. |
| Foundry project identity | Foundry User | Foundry resource | Minimum assignment for Foundry projects, as recommended by Microsoft Learn, *Role-based access control for Microsoft Foundry*. |
| Developer (`AZURE_PRINCIPAL_ID`) | Foundry User | Foundry project | Run the app locally with `DefaultAzureCredential`. |
| Developer | Storage Blob Data Contributor | Containers `documents` and `drafts` | Local runs. |

Microsoft renamed the Foundry roles. The former names were *Azure AI User*, *Azure AI Owner* and so on, and the
role IDs did not change. No Owner, Contributor, key-listing or *Cognitive Services ...* role is assigned.
`infra/scripts/Test-Infrastructure.ps1` fails when any other role definition ID appears in the template.

To audit the assignments of the app identity:

```powershell
$principal = az identity show -g <rg> -n <APP_IDENTITY_NAME> --query principalId -o tsv
az role assignment list --assignee $principal --all -o table
```

### Negative test: no role means 403

This test is manual, because automated tests never call Azure (issue #5, test notes). Run it after
provisioning, with a signed-in principal that has **no** role on the project:

```powershell
$token = az account get-access-token --resource https://ai.azure.com --query accessToken -o tsv
curl.exe -s -o NUL -w "%{http_code}`n" -H "Authorization: Bearer $token" "<FOUNDRY_PROJECT_ENDPOINT>/agents?api-version=v1"
```

The path comes from Microsoft Learn, *Microsoft Foundry REST reference* (list agents), checked 2026-09-24.

Expected result: `403`. With Foundry User assigned, the same call returns `200`. Calls with an API key are rejected
in every case, because `disableLocalAuth` is `true`.

## Application side (keyless access and Entra ID sign-in)

`src/ProposalGenerator.Security` gives the API and Blazor hosts the pieces they need:

- `AddProposalGeneratorAzureCredential(configuration)` registers one `TokenCredential` for all Azure SDK
  clients. In `Auto` mode (the default), it uses `ManagedIdentityCredential` when the host exposes a managed
  identity endpoint (`IDENTITY_ENDPOINT` or `MSI_ENDPOINT`, as on Container Apps). Otherwise it uses
  `DefaultAzureCredential` for local development through Azure CLI, azd or Visual Studio. Managed identity
  probing is excluded locally.
- The user-assigned client ID comes from `Azure:Credential:ManagedIdentityClientId`, or from `AZURE_CLIENT_ID`
  when that setting is empty. Set it from the `APP_IDENTITY_CLIENT_ID` output.
- `AddProposalGeneratorWebAppAuthentication` (Blazor, OpenID Connect) and
  `AddProposalGeneratorWebApiAuthentication` (API, JWT bearer) bind Microsoft.Identity.Web from `AzureAd`.
  Every endpoint then requires a signed-in user unless it opts out with `AllowAnonymous()`. Anonymous
  browser requests are redirected to the Entra ID sign-in page, and anonymous API calls get `401`.
- Start-up fails if `AzureAd` contains a `ClientSecret` or any credential source other than
  `SignedAssertionFromManagedIdentity`.

```json
{
  "Azure": { "Credential": { "Mode": "Auto", "ManagedIdentityClientId": "" } },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<from configuration or environment, not committed>",
    "ClientId": "<app registration client ID, not committed>",
    "CallbackPath": "/signin-oidc"
  }
}
```

Sign-in uses the `id_token` response type, so the app registration needs no client secret. Enable **ID tokens**
under *Authentication* and add the redirect URI `https://<app-host>/signin-oidc`. Creating the app registration
is a tenant-level action, so the template does not do it.

### Secret scanning and push protection

Secret scanning and push protection are repository settings, not files. On 2026-09-24,
`gh api repos/NikoMix/IAMCP_rethink2026_usecase --jq .security_and_analysis` reported both as `enabled`.

## Tear down

```powershell
azd down --purge
```

`azd down` deletes the resource group and every resource in it. `--purge` also purges the soft-deleted Foundry
resource. Without it, the resource name and its model quota stay reserved during the soft-delete retention
period. Add `--force` to skip the confirmation prompt.

## Validate offline

```powershell
az bicep build --file infra/main.bicep
az bicep lint --file infra/main.bicep
pwsh infra/scripts/Test-Infrastructure.ps1
```

The script builds and lints the template, failing on any warning. It then asserts the keyless settings, the EU
region list, the absence of Global deployment types, the approved role IDs, that no output looks like a secret,
and that `main.parameters.json` contains no IDs. It needs no Azure sign-in.
