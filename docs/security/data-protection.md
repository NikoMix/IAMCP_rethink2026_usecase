# Data protection, EU data residency and retention

> **Not legal advice.** This concept is an engineering view of how the Commercial Proposal Generator processes personal and confidential data on Azure. It doesn't replace legal review (#44), a data protection impact assessment by the data protection officer, or your own reading of the Microsoft contract terms. Legal review is tracked in #44.

| | |
|---|---|
| **Issue** | #41 (epic #37) |
| **Audience** | Product owner, developers, the IaC author (#3), the data protection officer, legal reviewers |
| **Status** | Proposed. Durations and decisions marked **ASSUMPTION** or **DECISION** need owner confirmation. |
| **Sources checked** | 2026-09-24, against the Microsoft pages listed in [Sources](#sources). Azure behaviour changes often, so recheck the sources before go-live. |

## Contents

1. [Summary](#1-summary)
2. [Data inventory and classification](#2-data-inventory-and-classification)
3. [EU data residency](#3-eu-data-residency)
4. [Retention and deletion](#4-retention-and-deletion)
5. [Telemetry without customer data](#5-telemetry-without-customer-data)
6. [GDPR notes](#6-gdpr-notes)
7. [Requirements checklist](#7-requirements-checklist)
8. [Verifying the acceptance criteria](#8-verifying-the-acceptance-criteria)
9. [Open decisions for the owner](#9-open-decisions-for-the-owner)
10. [Statements not verified](#10-statements-not-verified)
11. [Sources](#sources)

## 1. Summary

- **One EU region for everything.** Deploy every resource into one EU member-state region. The default is `swedencentral`. Geo-redundant replication stays off.
- **Model deployment type.** Use `DataZoneStandard` by default, which keeps inference processing inside the EU Data Boundary. Use `Standard` if the owner wants processing kept inside a single Azure geography. Global deployment types are never allowed, because they can process prompts in any Azure region [S1][S3].
- **Agent state.** Foundry Agent Service keeps conversations, files and vector stores until they're explicitly deleted [S7], so the application must delete them itself. Blob lifecycle rules act as a safety net for blobs only.
- **Retention (ASSUMPTION).** Drafts are kept for 30 days after the last change and generated documents for 90 days after creation. Uploads, agent files and vector stores are kept for up to 7 days. Logs and telemetry are kept for 30 days.
- **Telemetry.** Telemetry holds identifiers, counts and durations only. It never holds names, email addresses, amounts or document text. GenAI content recording is off everywhere except local development [S30].
- **GDPR.** Microsoft acts as a processor under the Microsoft Products and Services Data Protection Addendum (DPA) [S14][S15]. The owner remains the controller and must confirm the legal basis, the DPA/AVV and the TOMs with legal review (#44).

## 2. Data inventory and classification

### 2.1 Classification levels

**ASSUMPTION:** The organisation's own classification scheme isn't in the repository, so this document uses four working levels. Map them to the corporate scheme when it's available.

| Level | Meaning | Examples in this system |
|---|---|---|
| **Public** | Can be published | Product name, public template structure |
| **Internal** | Employees only | Agent instructions, empty templates, the service catalogue |
| **Confidential** | Business secret. Disclosure harms the company or the customer. | Prices, rate cards, discounts, contract terms, liability caps |
| **Confidential – personal** | Confidential **and** personal data under Art. 4(1) GDPR | Names and contact details of contacts and signatories, chat transcripts, account manager identities |

### 2.2 What the system processes

| # | Data category | Content | Personal data? | Class | Source |
|---|---|---|---|---|---|
| D1 | Customer master data | Company name, address, VAT ID, industry | Only for sole traders | Confidential | Chat, uploads |
| D2 | Contacts and signatories | Name, role, email, phone, signature block | **Yes** | Confidential – personal | Chat, uploads |
| D3 | Commercial terms | Prices, day rates, discounts, payment terms, currency | No | Confidential | Chat, rate card (#19) |
| D4 | Contract terms | Scope, deliverables, SLAs, liability, term, termination | No, unless a clause names people | Confidential | Templates (#8–#12), chat |
| D5 | Chat transcripts | User prompts and agent responses, including D1–D4 | **Yes** | Confidential – personal | Agent Service conversations |
| D6 | Uploaded customer documents | Incoming RFPs and RFIs (#22), existing SOWs (#21) | Usually yes | Confidential – personal | User upload |
| D7 | Derived retrieval data | Chunks and embeddings of D6 in agent vector stores | Same as the source | Same as the source | Agent Service |
| D8 | Drafts | Structured data model (#7) plus a reference to the conversation | **Yes** | Confidential – personal | Application (#35) |
| D9 | Generated documents | DOCX and PDF for SOW, RFI, RFP, MSA and CR, including versions (#29) | **Yes** | Confidential – personal | Rendering (#24, #26) |
| D10 | Knowledge base | Service catalogue, rate card, reference projects (#19) | Possibly (named references) | Confidential | Maintained internally |
| D11 | User identity | Entra object ID, display name, UPN of account managers | **Yes** (employees) | Confidential – personal | Entra ID (#5) |
| D12 | Telemetry and logs | Traces, metrics, request logs, diagnostics | **Must not** contain D1–D9 values | Internal | App, platform |
| D13 | Microsoft abuse monitoring data | Samples of prompts and completions, only when flagged | Possibly | Held by Microsoft | Microsoft [S3][S4] |

### 2.3 Where the data is stored and who can access it

| Data | Storage location (target design) | Who can access it |
|---|---|---|
| D8 drafts | Blob container `drafts/` in the Storage account in the EU region | The owning user through the app (#35). The app's managed identity. No human data-plane roles in production. |
| D9 generated documents | Blob container `documents/` | The owning user (download, #28). Approvers (#34). The app's managed identity. |
| D6 uploads | Blob container `uploads/`. Agent Service files. | The owning user. The app's managed identity. The agent (the Foundry project identity). |
| D5 transcripts | Foundry Agent Service conversations. Basic setup uses Microsoft-managed storage in the resource's region [S7]. Standard setup uses Cosmos DB in your subscription [S9]. | The app's managed identity (Foundry agent role, #5). Foundry project users. |
| D7 vector stores | Agent Service. Basic setup uses Microsoft-managed storage. Standard setup uses Azure AI Search [S9]. | As for D5 |
| D10 knowledge base | Agent Service files and vector store for the project | Maintainers of the knowledge base (#19) |
| D11 identity | Entra ID (tenant). The object ID is also stored as the draft owner. | Entra administrators. The app. |
| D12 telemetry | Log Analytics workspace behind Application Insights, in the EU region | Operators with Log Analytics Reader (#42) |
| D13 abuse monitoring | Microsoft abuse monitoring store in the geography of the Foundry resource [S3] | Authorised Microsoft employees only, and only for flagged content. For deployments in the EEA, the reviewers are located in the EEA [S3][S4]. |

Microsoft commits that its personnel outside the geography may operate systems remotely, but won't access customer data without your authorisation [S12]. The blob container names are assumptions. Align them with the application stories.

## 3. EU data residency

### 3.1 Region

**Requirement:** Deploy every resource (Foundry account and project, model deployment, Storage, Log Analytics, Application Insights, Container Apps environment and apps, and Cosmos DB and AI Search if standard agent setup is used) into **one** region, taken from a single `location` parameter.

Allowed regions are EU member-state regions that meet both conditions:

1. Microsoft lists them as supporting Foundry Agent Service [S10].
2. They offer Data Zone Standard deployments for current GPT models [S2].

| Region | Agents [S10] | Data Zone Standard, EU [S2] | Standard (regional), GPT models [S2] | Note |
|---|---|---|---|---|
| `swedencentral` (**default**) | Yes | Yes | gpt-4.1, gpt-4.1-mini, gpt-4o, gpt-4o-mini, gpt-5.1, o1, o4-mini | The only EU region with a broad set of Standard deployments |
| `germanywestcentral` | Yes | Yes | None (embeddings only) | |
| `francecentral` | Yes | Yes | gpt-4.1-mini, gpt-4o (2024-11-20) | |
| `italynorth` | Yes | Yes | None | File search isn't available in this region [S10]. No paired region [S22]. |
| `polandcentral` | Yes | Yes | None | No paired region [S22] |
| `spaincentral` | Yes | Yes | None | No paired region [S22] |
| `westeurope` | Yes | Yes | gpt-4.1-mini | |

The region-availability snapshot was taken on 2026-09-24 from [S2]. Before you pick a model, recheck the page, because availability changes with every model release.

Excluded regions:

- `norwayeast` and `switzerlandnorth` are inside the EU Data Boundary (EFTA) but aren't EU member states [S13].
- `uksouth` is outside the EU Data Boundary.

Azure Container Apps (`Microsoft.App/managedEnvironments`) is available in all seven allowed regions. This was checked with `az provider show -n Microsoft.App` on 2026-09-24, not on a documentation page (see U6).

The Foundry Agent Service FAQ says that "endpoints are regional, and data is stored in the same region as the endpoint" [S7]. The Agent Service privacy page says data is stored "within the same geography" as the resource [S6]. This document assumes the broader statement (geography) applies.

### 3.2 Model deployment type

The deployment type decides **where inference is processed**. Data at rest stays in the resource's geography for every deployment type [S1][S3].

| Type (SKU) | Where prompts and completions are processed | Allowed? |
|---|---|---|
| `GlobalStandard`, `GlobalProvisionedManaged`, `GlobalBatch` | Any Azure region where the model is deployed [S1][S3] | **No**, because processing can leave the EU |
| `DeveloperTier` | Any Azure region. No data residency guarantee [S1]. | **No** |
| `DataZoneStandard`, `DataZoneProvisionedManaged` | Within the Microsoft-defined EU data zone [S1][S3] | **Yes (default)** |
| `Standard`, `ProvisionedManaged` | Within the resource's Azure geography. Can move between regions of that geography for operational reasons [S1][S3]. | **Yes**. Strictest option, but few models are available. |

Notes:

- **Scope of the EU data zone.**
  - [S3] says processing happens in "that or any other European Union Member Nation".
  - [S1] says the EU data zone follows the EU Data Boundary, which "can include" EFTA countries such as Norway and Switzerland.
  - Microsoft "can add regions to either data zone without prior notice" [S1].
  
  Treat Data Zone EU as **EU plus EFTA** (see U3).
- **Standard is geography-bound, not region-bound.** [S2] says Standard processes "in the region associated with your deployment". [S1] and [S3] say "within the customer-specified Azure geography". The broader statement is assumed (see U4).
- **Previews.** Preview services "typically store customer data in the United States but may store it globally" [S12], and they can follow different privacy practices, including for abuse monitoring [S3]. Don't use preview models or preview features with real customer data.
- **Content filtering (Guardrails)** runs synchronously inside the deployment and doesn't store prompts [S3]. Responsible AI controls are covered in #40.

**DECISION D1:** The default is `DataZoneStandard` in `swedencentral`, because it offers current models inside the EU Data Boundary. Choose `Standard` in `swedencentral` if the owner requires processing in one geography (Sweden), and accept the smaller set of models.

### 3.3 Foundry Agent Service storage (threads, files, vector stores)

Foundry Agent Service is stateful. It stores conversations and responses, plus files and vector stores [S7].

| Setup | Where agent state is stored | Encryption | Notes |
|---|---|---|---|
| **Basic** | A Microsoft-managed storage account that is logically separated [S7], in the region or geography of the Foundry resource [S6][S7] | Microsoft-managed keys only [S7] | You can't reach it with Azure Storage tools, so deletion is possible only through the Agent API or SDK |
| **Standard** | Your own resources: Azure Storage (files), Azure Cosmos DB (conversations and agent metadata), Azure AI Search (vector stores) [S8][S9] | Customer-managed keys supported [S7][S8] | Requires Cosmos DB with at least 3000 RU/s and an AI Search service [S9]. All three resources must be in the same allowed region. |

Other points:

- Projects are the unit of isolation. Agents in one project can't access data from another project [S8].
- **Retention:** "Data persists unless you explicitly delete it. To delete agent data, use the API or SDK to delete threads, files, or vector stores" [S7]. There's no built-in expiry. A third-party summary claimed that basic setup data expires after 60 days, but that claim doesn't match [S7] and must not be relied on.
- When the Azure OpenAI **Responses API** is called directly, response data is kept for 30 days by default unless it's deleted, or unless `store=false` is used where supported [S11].
- Tools that call external services, such as Grounding with Bing Search, are governed by their own terms. Some are outside the DPA [S6]. Don't enable Bing grounding for customer data.

**DECISION D2:** Use **basic setup** for the workshop, which matches the scope of #3. The application deletes agent data itself (APP-05). For production use with real customer data, consider **standard setup**, which adds data ownership, customer-managed keys and the ability to inspect storage directly.

### 3.4 Abuse monitoring

By default, Microsoft runs abuse monitoring on models sold by Azure [S3][S4]:

- Content classifiers and pattern detection run on prompts and completions. Automated review, including by LLMs, doesn't store the content [S4].
- If content is flagged and automated review isn't sufficient, **authorised Microsoft employees may review it** through Secure Access Workstations with just-in-time approval. For deployments in the EEA, these reviewers are located in the EEA [S3][S4].
- The abuse monitoring store for human review is logically separated per resource and located in the **geography of the Foundry resource**. This also applies to Global and Data Zone deployments [S3].

**Modified abuse monitoring** turns off this data storage and human review. Automated review can still run [S3]. Only customers managed by a Microsoft account team, or in an eligible program, can apply [S5]. After approval, the resource shows `ContentLogging: false` in its capabilities [S3].

**DECISION D4:** The workshop doesn't apply for modified abuse monitoring, because it uses synthetic data. For production, the owner and the data protection officer decide whether the residual risk (flagged samples, EEA-based human review) is acceptable for the contract data involved. If it isn't, and the organisation is eligible, apply through the form linked from [S4]. How long flagged samples are kept isn't stated in the sources (see U2).

## 4. Retention and deletion

### 4.1 Retention periods

All durations are **ASSUMPTIONS** for the owner to confirm (decision D3). The guiding principle is that the proposal generator is **not the system of record**. After approval (#34), the account manager files the final document in the company's CRM or document management system. Statutory retention obligations under commercial and tax law, for example for offers sent as commercial letters, are met there. Confirm this in #44.

| Data | Retention (ASSUMPTION) | Trigger | Enforced by |
|---|---|---|---|
| D8 drafts (`drafts/`) | **30 days** after the last change | Inactivity | Cleanup job (APP-05), with a lifecycle rule as backstop (INF-14) |
| D5 agent conversations and responses for a draft | Deleted **together with the draft**, so at most 30 days after the last change | Draft deletion | Cleanup job through the Agent API (APP-05) |
| D9 generated documents (`documents/`), all versions (#29) | **90 days** after creation | Age | Lifecycle rule (INF-14). The cleanup job removes index entries. |
| D6 uploads (`uploads/`) and agent files | **7 days** after upload, or sooner once extraction is done | Age | Lifecycle rule for blobs. Cleanup job for agent files (APP-06). |
| D7 per-draft vector stores | **7 days**, or together with the draft, whichever comes first | Age | Cleanup job (APP-06) |
| D10 knowledge base | Until replaced. Review every year. | Content change | Maintainers (#19) |
| D12 Log Analytics and Application Insights | **30 days** | Age | Workspace retention (INF-15) |
| Blob soft delete | **+7 days** after any deletion | Deletion | Blob service policy (INF-12) |
| D13 abuse monitoring samples | Controlled by Microsoft. Not documented (U2). | Flagging | Microsoft. Reduce with modified abuse monitoring (D4). |

**Maximum time until data is physically gone.** For blobs, the worst case is the retention period, plus up to 24 hours before a new or changed lifecycle policy first runs, plus the run duration, plus 7 days of soft delete. A lifecycle delete on an account with soft delete enabled puts the blob into the soft-deleted state for the soft-delete period [S17]. If the Log Analytics retention period is shortened, Azure Monitor waits 30 days before it removes the data [S25].

### 4.2 How deletion is enforced

1. **Blob lifecycle management (infrastructure, backstop).** A `managementPolicies/default` rule deletes the base blobs, snapshots and versions under each container prefix once they pass the rule's age [S17]. Lifecycle rules only reach the Storage account. They can't delete Agent Service data or Log Analytics data.
2. **Scheduled cleanup job (application, primary).** A daily Container Apps job with the `Schedule` trigger and a cron expression [S33] runs these steps:
   1. Finds drafts past retention by reading draft metadata.
   2. Deletes the linked agent conversations and responses, files and vector stores through the Agent Service API [S7].
   3. Deletes the draft blobs and the document index entries.
   4. Logs counts and IDs only, with no content.
   
   The job must be idempotent: a resource that's already gone counts as success. It must also be observable: a failed run raises an alert (#42).
3. **Log retention (infrastructure).** Set the workspace retention to 30 days. Application Insights data is stored in the linked Log Analytics workspace [S28], so the same retention applies.
4. **Soft delete limited to 7 days.** This protects against accidental deletion while keeping the erasure delay short. Keep blob versioning off. Previous versions are kept until they're explicitly deleted or removed by a lifecycle policy [S18], so if versioning is ever turned on, the lifecycle rules must also delete versions.

### 4.3 Data subject requests (GDPR Articles 15–21)

Microsoft provides tools for data subject requests (DSRs) on customer data and system-generated logs in Azure [S16]. Microsoft, as processor, supports the controller in responding to them [S15]. For this system:

| Request | Procedure |
|---|---|
| **Access or export** (Art. 15, 20) | An authorised operator searches drafts and documents for the person's name or email address. This needs the DSR tooling in APP-10. The operator exports the matching draft JSON and documents. Conversations are exported through the Agent API by using the conversation ID stored in the draft. |
| **Erasure** (Art. 17) | Run the cleanup routine for the affected drafts and documents immediately. Remove or overwrite the person's data in drafts that must be kept. Soft-deleted blobs are purged when the 7-day soft-delete period ends. |
| **Rectification** (Art. 16) | The user edits the draft (#33) and regenerates the document |
| **Telemetry** | By design, telemetry holds no subject data (section 5), so nothing needs purging. If data leaks into logs by mistake, use the Log Analytics Delete Data API [S26][S27]. That API *marks* records as deleted rather than physically removing them straight away [S27]. Physical removal happens when retention expires. Fix the code that caused the leak. |
| **Employee (account manager) data** | The draft owner's object ID links a user to their drafts. When a user leaves, the owner decides whether to reassign or delete their drafts. |

Document the procedure, roles and response times in the operations documentation (#43).

## 5. Telemetry without customer data

Acceptance criterion 2 of #41 requires that telemetry contains no names, email addresses or amounts.

- **GenAI content recording** captures user messages, tool arguments and model outputs in traces, and should be enabled "only in development" [S30]. It's controlled by these settings:
  - the environment variable `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT`
  - in .NET, the AppContext switch `Azure.Experimental.TraceGenAIMessageContent`, which takes priority over the environment variable [S30]
  - the environment variable `AZURE_EXPERIMENTAL_ENABLE_GENAI_TRACING`, which the framework samples use [S30]
  
  These settings don't cover your own instrumentation. For example, the Python `@trace_function` decorator always records parameters and return values [S30]. Custom .NET spans need the same care.
- Microsoft advises you to redact personal data before it reaches telemetry and to treat trace data like production logs [S29]. Don't put sensitive data in URLs [S28].
- Application Insights uses the client IP address for geolocation and then zeroes the stored IP field by default [S28]. Keep this default.
- Among the resource log categories for the Foundry account are `Audit`, `RequestResponse`, `AzureOpenAIRequestUsage` and `Trace` [S31]. The sources don't document whether `RequestResponse` or `Trace` contain prompt text (U1). Enable only `Audit` and metrics until this is verified.
- Microsoft recommends filtering out or anonymising personal data before it's ingested, rather than purging it afterwards [S26].

Application rules (APP-01 to APP-03) are listed in the checklist.

## 6. GDPR notes

This section is not legal advice. Legal review happens in #44, and the data protection impact assessment (DSFA) is done by the data protection officer. Both are out of scope for #41.

### 6.1 Roles and contract (DPA / AVV)

- The operating company is the **controller**. Microsoft acts as a **processor** for customer data in Azure services and processes it according to the controller's documented instructions, as set out in the Product Terms and the DPA [S15].
- The DPA is an addendum to the Product Terms. When you subscribe to a product under the Product Terms, the DPA defines the data processing and security terms [S14]. The DPA governs data processing by models sold by Azure [S3] and by Foundry Agent Service [S6]. It may not cover external tools, for example Grounding with Bing Search, which is a Microsoft-as-controller service [S6].
- In German practice, the contract required by Art. 28 GDPR is called the **AVV** (*Auftragsverarbeitungsvertrag*, data processing agreement). **Action for legal (#44):** confirm that the organisation's Azure agreement incorporates the current DPA, file it as the AVV, and review the subprocessor list and transfer terms in the DPA.
- **EU Data Boundary:** For Azure regional services deployed in an EU Data Boundary region, customer data and pseudonymised personal data are stored and processed in the EU and EFTA. Limited documented transfers remain [S13].

### 6.2 Legal basis (candidates for legal review)

| Processing | Candidate legal basis under Art. 6(1) GDPR | Comment |
|---|---|---|
| Contact and signatory data of B2B customers (D2) in offers and contracts | (b) contract or pre-contractual steps, where the person is the contracting party. Otherwise (f) legitimate interest. | Contacts at a corporate customer are usually not the contracting party themselves. |
| Account manager data (D11) | Employment context (Art. 88 GDPR and national law) | Confirm with HR and legal |
| Chat transcripts (D5) | The same basis as the data they contain, limited by the retention in section 4 | |
| Telemetry (D12) | (f) legitimate interest in running a secure, working service | Designed without personal content |

Other duties for the owner: information to data subjects (Art. 13 and 14), a record of processing activities (Art. 30), and a DPIA screening (Art. 35) by the data protection officer.

### 6.3 Technical and organisational measures (TOMs, Art. 32)

| Measure | Implementation | Issue |
|---|---|---|
| Access control | Entra ID sign-in, least-privilege RBAC, and managed identity instead of keys (`disableLocalAuth` on Foundry [S24], `allowSharedKeyAccess: false` on Storage [S19]) | #5 |
| Separation | Each user sees only their own drafts. Foundry projects isolate agent data [S8]. | #35 |
| Encryption at rest | Azure Storage uses AES-256 and can't be disabled [S23]. Foundry stored data uses AES-256 by default, with an optional customer-managed key [S3]. | #3 |
| Encryption in transit | Minimum TLS 1.2 and HTTPS only | #3 |
| No anonymous access | `allowBlobPublicAccess: false` [S20] | #3 |
| Data minimisation | No personal data in telemetry. Uploads and vector stores are deleted early. | #41, #42 |
| Pseudonymisation | Logs contain object IDs and draft IDs only | #42 |
| Storage limitation | Retention and deletion concept (section 4) | #41, #35 |
| Residency | One EU region and a Data Zone EU or Standard deployment (section 3) | #3 |
| Integrity and misuse protection | Guardrails, Prompt Shields, and separating instructions from document content | #40 |
| Availability | LRS or ZRS storage and 7-day soft delete | #3 |
| Audit | Foundry `Audit` diagnostic logs and Entra sign-in logs | #3, #42 |
| Secret protection | Secret scanning and push protection | #5 |
| Human review | Approval before a document is final | #34 |
| Known gap | Public network endpoints, because private endpoints are a non-goal of #3 | Production follow-up |

## 7. Requirements checklist

Each requirement is traceable to an issue. The **INF** items go to the IaC session (#3) and were sent to the coordinator on 2026-09-24. The **APP** items belong to later stories. The **GOV** items are owner or legal actions.

### 7.1 Infrastructure (for #3; owner of `infra/`)

| ID | Resource | Property | Value | Rationale | Issue |
|---|---|---|---|---|---|
| INF-01 | All resources | `location` parameter | `@allowed(['swedencentral','germanywestcentral','francecentral','italynorth','polandcentral','spaincentral','westeurope'])`, default `swedencentral`. Every resource uses this one parameter. No second region. | Section 3.1 | #3, #41 |
| INF-02 | Deployment outputs | Outputs | Endpoints only. No keys or connection strings that contain keys. | Keyless | #3, #5 |
| INF-03 | `Microsoft.CognitiveServices/accounts` (Foundry) | `properties.disableLocalAuth` | `true` | Keyless [S24] | #5 |
| INF-04 | Model deployment | `sku.name` | Parameter `@allowed(['DataZoneStandard','Standard'])`, default `DataZoneStandard`. Never `Global*` or `DeveloperTier`. | Section 3.2 [S1] | #3, #41 |
| INF-05 | Model deployment | `model.name` and `model.version` | A GA model that [S2] lists for the chosen SKU and region. No preview models. | Previews may store data outside the geography [S12] | #3, #16 |
| INF-06 | Agent setup | Capability host | Basic setup for the workshop (D2). For standard setup, Cosmos DB, AI Search and Storage must all use the same `location`. | Section 3.3 | #3 |
| INF-07 | Foundry account | Diagnostic settings | Send to the workspace: category `Audit` plus `AllMetrics`. Don't send `RequestResponse` or `Trace` until U1 is resolved. | Section 5 | #42 |
| INF-08 | `Microsoft.Storage/storageAccounts` | `allowSharedKeyAccess` | `false` | Keyless [S19] | #5 |
| INF-09 | Storage account | `allowBlobPublicAccess` | `false`. Every container uses `publicAccess: 'None'`. | [S20] | #3 |
| INF-10 | Storage account | `minimumTlsVersion`, `supportsHttpsTrafficOnly`, `defaultToOAuthAuthentication` | `'TLS1_2'`, `true`, `true` | Encryption in transit, Entra by default | #3, #5 |
| INF-11 | Storage account | `sku.name` | `Standard_LRS` or `Standard_ZRS`. No GRS, RA-GRS, GZRS or RA-GZRS. | LRS and ZRS keep data in the primary region [S21]. Geo-redundancy copies it to a paired region, and `polandcentral`, `spaincentral` and `italynorth` have no pair [S22]. | #3, #41 |
| INF-12 | `blobServices/default` | `deleteRetentionPolicy`, `containerDeleteRetentionPolicy`, `isVersioningEnabled` | Enabled with `days: 7`. Enabled with `days: 7`. `false`. | Bounded recovery window. Lifecycle deletes become soft deletes [S17][S18]. | #41 |
| INF-13 | Blob containers | Names | `drafts`, `documents`, `uploads` (ASSUMPTION, align with the app) | Retention differs per container | #35, #28, #22 |
| INF-14 | `managementPolicies/default` | Rules (`Delete`) | `drafts/`: base blob `daysAfterModificationGreaterThan: 30`. `documents/`: `daysAfterCreationGreaterThan: 90`. `uploads/`: `daysAfterCreationGreaterThan: 7`. Each rule also deletes `snapshot` and `version` (`daysAfterCreationGreaterThan`, same number of days). Durations are D3 assumptions. | Backstop for deletion [S17] | #41, #35 |
| INF-15 | `Microsoft.OperationalInsights/workspaces` | `retentionInDays`, `sku` | `30`, `PerGB2018` | Section 4.1. The first 31 days are included in the ingestion price [S25]. | #41, #42 |
| INF-16 | `Microsoft.Insights/components` | `WorkspaceResourceId`, `location`, `DisableIpMasking` | The workspace from INF-15, the same `location`, not `true`. `DisableLocalAuth: true` is recommended if the app ingests with Entra ID. | Keep the default IP zeroing [S28] | #42, #5 |
| INF-17 | `Microsoft.App/managedEnvironments` | `appLogsConfiguration` | `destination: 'log-analytics'`, pointing to the workspace from INF-15 | One retention setting [S32] | #42 |
| INF-18 | Container app (web) | Environment variables | `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=false`. Don't set `AZURE_EXPERIMENTAL_ENABLE_GENAI_TRACING=true` outside development. | Section 5 [S30] | #42, #41 |
| INF-19 | `Microsoft.App/jobs` (added once the app exists) | `triggerType`, `cronExpression` | `Schedule`, daily (for example `0 3 * * *`), running the cleanup from APP-05 with the same managed identity | Deletes agent data [S7][S33] | #41, #35 |
| INF-20 | Any other resource added later (Content Safety, Key Vault, AI Search, alerts) | `location` | The same INF-01 parameter. Check any resource type that only supports `global` (for example action groups for #42 alerts) with the owner. | Section 3.1 | #40, #42 |

### 7.2 Application (later stories)

| ID | Requirement | Issue |
|---|---|---|
| APP-01 | Structured logging uses an allowlist of fields: IDs, document type, template version, status, durations, token counts. Never log prompt text, tool arguments, document content, customer or contact names, email addresses, phone numbers or amounts. Don't destructure domain objects into logs. | #42, #41 |
| APP-02 | Keep GenAI content recording off in every deployed environment. Don't set the AppContext switch `Azure.Experimental.TraceGenAIMessageContent`. Custom spans must not record arguments. | #42 |
| APP-03 | Exception messages and validation errors name the field, not the value. Routes and query strings contain IDs only. | #42, #32 |
| APP-04 | Store drafts under `drafts/{ownerObjectId}/{draftId}/`. Record the agent conversation ID(s), file IDs and vector store IDs in the draft metadata so they can be deleted later. Enforce owner checks on every read. | #35, #5 |
| APP-05 | Daily cleanup job: finds drafts past retention, deletes their agent conversations and responses, files and vector stores through the Agent Service API, then deletes the blobs and index entries. Idempotent. Logs counts only. Alerts on failure. Retention periods are configuration values, not constants. | #41, #35, #42 |
| APP-06 | Delete uploads (source files, agent files, per-draft vector stores) once extraction finishes, and after 7 days at the latest. | #22, #21 |
| APP-07 | Don't rely on service defaults for retention. If the Responses API is called directly, use `store=false` where no server-side state is needed, or delete responses explicitly [S11]. | #16, #18 |
| APP-08 | The knowledge base (#19) holds only approved internal content. Reference projects contain no personal data of customer contacts and are anonymised unless the customer agreed to be named. | #19 |
| APP-09 | Tests, golden files and evaluation datasets use synthetic data only. The repository contains no real customer data. | #38, #39, #14 |
| APP-10 | DSR support: a role-restricted operator procedure or tool to find, export and delete drafts and documents by contact name or email (section 4.3). | #41, #43 |
| APP-11 | The UI tells users that drafts and documents are deleted automatically (show the dates) and that final documents must be filed in the system of record. | #34, #28, #44 |
| APP-12 | Don't enable Grounding with Bing Search or other external-data tools for conversations that contain customer data [S6]. | #16, #40 |
| APP-13 | Data from another customer is never returned (checked by #40 red-teaming). Agent data is scoped per draft or conversation. | #40 |

### 7.3 Governance (owner, legal, data protection officer)

| ID | Action | Issue |
|---|---|---|
| GOV-01 | Confirm the DPA is part of the Azure agreement and file it as the AVV. Review the subprocessors. | #44 |
| GOV-02 | Confirm the legal bases in section 6.2 and the Art. 13 and 14 information. | #44 |
| GOV-03 | Add a record of processing activities (Art. 30) entry. Do a DPIA screening (Art. 35) with the data protection officer. | #44 |
| GOV-04 | Confirm or change the retention periods (D3) and confirm the "not the system of record" assumption. | #41 |
| GOV-05 | Decide D1 (deployment type) and D2 (basic or standard agent setup). | #3, #41 |
| GOV-06 | Decide D4 (modified abuse monitoring) before production. | #41, #40 |
| GOV-07 | Workshop rule: lab environments use synthetic data only. Real customer data is never entered. | #43 |

## 8. Verifying the acceptance criteria

Each check needs a **positive control** that proves it can detect a failure. A check that can't produce a positive result proves nothing when it finds nothing.

### AC 1: All resources are in the EU

```powershell
# Expect no rows. Replace the placeholders; 'swedencentral' is the chosen region.
az resource list --resource-group <resource-group> `
  --query "[?location!='swedencentral'].{name:name, type:type, location:location}" -o table

# Expect only DataZoneStandard or Standard.
az cognitiveservices account deployment list -g <resource-group> -n <foundry-account> `
  --query "[].{name:name, sku:sku.name}" -o table
```

- **Count check:** Compare the number of resources returned by `az resource list --resource-group <resource-group> --query "length(@)"` with the number the Bicep template declares. An empty filter result means nothing if the resource group is empty.
- **Positive control:** Run the first query with a different region in the filter, for example `?location!='westeurope'`. It must return every resource.

### AC 2: No names, email addresses or amounts in telemetry

1. In a test environment with content recording **off**, create a draft that contains unique synthetic canary values. For example:
   - contact `Zyxwv Canary41`
   - email `canary41@example.invalid`
   - amount `987654.32`, which appears as `987.654,32` in German and `987,654.32` in English (#27)
2. Generate a DOCX and a PDF, then wait about 10 minutes for ingestion.
3. Search the workspace. Expect 0 rows:

   ```kusto
   search "Canary41" or "canary41@example.invalid" or "987654" or "987.654,32" or "987,654.32"
   | where TimeGenerated > ago(1h)
   | summarize count() by $table
   ```

4. **Positive control:** Emit one log line that contains `CONTROL41-<random>` from a test-only code path, or repeat the run with content recording on in a development environment. The same query shape must find it.

### AC 3: Data and files are deleted after retention

1. In a test environment, set the cleanup retention to a few minutes through configuration (APP-05), and set the lifecycle rule to 1 day.
2. Create two drafts, each with an upload and a generated document. Make one older than the retention period and keep one recent.
3. Run the cleanup job. For the expired draft, expect:
   - the blobs are gone from the normal listing and appear only with `--include d` until soft delete expires
   - the agent conversation, file and vector store requests return *not found*
4. **Positive control:** The recent draft and all its agent resources still exist.
5. Check the lifecycle rule separately after 24 to 48 hours, because runs can take up to 24 hours to start [S17].

## 9. Open decisions for the owner

| ID | Decision | Default in this document |
|---|---|---|
| D1 | Model deployment type | `DataZoneStandard` in `swedencentral` |
| D2 | Agent setup | Basic setup for the workshop. Standard setup for production. |
| D3 | Retention periods | 30 days for drafts, 90 days for documents, 7 days for uploads and vector stores, 30 days for logs, 7 days of soft delete |
| D4 | Modified abuse monitoring | Not applied for the workshop. Decide before production. |
| D5 | Classification scheme | Four working levels (section 2.1) |

## 10. Statements not verified

These points couldn't be confirmed from a current Microsoft source, or the sources contradict each other. They're flagged, not assumed.

| ID | Statement | Status and handling |
|---|---|---|
| U1 | Whether the Foundry `RequestResponse` or `Trace` diagnostic logs contain prompt or completion text | Not documented in [S31]. Keep these categories off (INF-07) until a test shows they don't contain the text. |
| U2 | How long Microsoft keeps abuse monitoring samples that were flagged for human review | Not stated in [S3] or [S4]. Ask Microsoft or the account team if it matters for D4. |
| U3 | Whether Data Zone EU is limited to EU member states or includes EFTA | [S3] says EU member nations. [S1] says it follows the EU Data Boundary, which includes EFTA [S13]. [S2] lists `norwayeast` and `switzerlandnorth` under Data Zone Standard. Treat it as EU plus EFTA. |
| U4 | Whether Standard deployments process in one region or anywhere in the geography | [S2] says region. [S1] and [S3] say geography. Treat it as geography. |
| U5 | Whether basic agent setup storage is per region or per geography | [S7] says region. [S6] says geography. Treat it as geography. |
| U6 | Container Apps availability in the allowed regions | Checked with `az provider show -n Microsoft.App` (Azure Resource Manager metadata), not a documentation page |
| U7 | Whether new Agent Service conversations follow the 30-day default of the Responses API | [S7] says agent data persists until deleted. [S11] gives 30 days for direct Responses API use. Delete explicitly in both cases (APP-05, APP-07). |

## Sources

All sources were retrieved on 2026-09-24.

| ID | Source |
|---|---|
| S1 | [Deployment types in Microsoft Foundry Models](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/concepts/deployment-types) |
| S2 | [Region availability for Foundry Models sold by Azure](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure-region-availability) |
| S3 | [Data, privacy, and security for Foundry Models sold by Azure](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/openai/data-privacy) |
| S4 | [Abuse monitoring for Foundry Models sold by Azure](https://learn.microsoft.com/en-us/azure/foundry/openai/concepts/abuse-monitoring) |
| S5 | [Limited access to Models sold by Azure](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/openai/limited-access) |
| S6 | [Data, privacy, and security for Foundry Agent Service](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/agents/data-privacy-security) |
| S7 | [Foundry Agent Service frequently asked questions](https://learn.microsoft.com/en-us/azure/foundry/agents/faq) |
| S8 | [Set up your environment for Foundry Agent Service](https://learn.microsoft.com/en-us/azure/foundry/agents/environment-setup) |
| S9 | [Set up standard agent resources](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/standard-agent-setup) |
| S10 | [Foundry Agent Service limits, quotas, and regions](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/limits-quotas-regions) |
| S11 | [Azure OpenAI Responses API](https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/responses) |
| S12 | [Data residency in Azure](https://azure.microsoft.com/en-us/explore/global-infrastructure/data-residency/) |
| S13 | [What is the EU Data Boundary?](https://learn.microsoft.com/en-us/privacy/eudb/eu-data-boundary-learn) |
| S14 | [Microsoft Products and Services Data Protection Addendum (DPA)](https://www.microsoft.com/licensing/docs/view/Microsoft-Products-and-Services-Data-Protection-Addendum-DPA) |
| S15 | [GDPR overview in Microsoft compliance documentation](https://learn.microsoft.com/en-us/compliance/regulatory/gdpr) |
| S16 | [Azure data subject requests for the GDPR and CCPA](https://learn.microsoft.com/en-us/compliance/regulatory/gdpr-dsr-azure) |
| S17 | [Azure Blob Storage lifecycle management overview](https://learn.microsoft.com/en-us/azure/storage/blobs/lifecycle-management-overview) |
| S18 | [Soft delete for blobs](https://learn.microsoft.com/en-us/azure/storage/blobs/soft-delete-blob-overview) |
| S19 | [Prevent Shared Key authorization for a storage account](https://learn.microsoft.com/en-us/azure/storage/common/shared-key-authorization-prevent) |
| S20 | [Remediate anonymous read access to blob data](https://learn.microsoft.com/en-us/azure/storage/blobs/anonymous-read-access-prevent) |
| S21 | [Azure Storage redundancy](https://learn.microsoft.com/en-us/azure/storage/common/storage-redundancy) |
| S22 | [Azure region pairs](https://learn.microsoft.com/en-us/azure/reliability/regions-paired) |
| S23 | [Azure Storage encryption for data at rest](https://learn.microsoft.com/en-us/azure/storage/common/storage-service-encryption) |
| S24 | [Disable local authentication in Foundry Tools](https://learn.microsoft.com/en-us/azure/ai-services/disable-local-auth) |
| S25 | [Manage data retention in a Log Analytics workspace](https://learn.microsoft.com/en-us/azure/azure-monitor/logs/data-retention-configure) |
| S26 | [Manage personal data in Azure Monitor Logs](https://learn.microsoft.com/en-us/azure/azure-monitor/logs/personal-data-mgmt) |
| S27 | [Delete data from a Log Analytics workspace by using the Delete Data API](https://learn.microsoft.com/en-us/azure/azure-monitor/logs/delete-log-data) |
| S28 | [Application Insights FAQ: data collection, retention, storage, and privacy](https://learn.microsoft.com/en-us/azure/azure-monitor/app/application-insights-faq) |
| S29 | [Agent tracing concepts](https://learn.microsoft.com/en-us/azure/foundry/observability/concepts/trace-agent-concept) |
| S30 | [Add client-side tracing to Foundry agents](https://learn.microsoft.com/en-us/azure/foundry/observability/how-to/trace-agent-client-side) and [Configure tracing for AI agent frameworks](https://learn.microsoft.com/en-us/azure/foundry/observability/how-to/trace-agent-framework) |
| S31 | [Monitoring data reference for Azure OpenAI](https://learn.microsoft.com/en-us/azure/foundry/openai/monitor-openai-reference) |
| S32 | [Log storage and monitoring options in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/log-options) |
| S33 | [Jobs in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/jobs) |
