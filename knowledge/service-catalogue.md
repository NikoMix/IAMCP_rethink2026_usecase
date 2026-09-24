---
title: Service catalogue
supplier: Fabrikam Consulting GmbH (fictional)
catalogue_id: SC-2026
valid_from: 2026-01-01
valid_to: 2026-12-31
sample_data: true
---

# Service catalogue – Fabrikam Consulting GmbH (fictional)

> **Fictional sample data.** Fabrikam Consulting GmbH is not a real company. This catalogue exists
> only for the IAMCP GitHub hands-on workshop. Replace it with your own catalogue before real use.

This catalogue describes the standard services that Fabrikam Consulting offers. Use it to phrase
scope, deliverables, assumptions and exclusions in proposals.

**Rules for the proposal agent**

- Take daily rates only from the rate card `rate-card.json`. This catalogue deliberately contains no prices.
- The effort figures below are typical ranges for estimation, not commitments.
- If a customer requests a service that is not in this catalogue, say so and ask the account manager
  how to proceed. Do not invent a service.
- Cite this catalogue (service ID and file name) when you use it.

## SVC-01 Discovery Workshop

- **Summary:** Structured workshop series that clarifies business goals, current state, constraints
  and success measures for a planned initiative.
- **Typical deliverables:** workshop summary, prioritised use-case backlog, high-level roadmap.
- **Typical roles and effort:** Engagement Manager 2–3 days, Solution Architect 3–5 days,
  Business Analyst 3–5 days.
- **Typical duration:** 2–3 weeks.
- **Customer dependencies:** availability of business and IT stakeholders for up to four half-day sessions.
- **Out of scope:** implementation, detailed design, licence procurement.
- **Preferred pricing model:** fixed price.

## SVC-02 Cloud Readiness Assessment

- **Summary:** Assessment of applications, infrastructure and operating model against the target
  cloud platform, including a migration wave plan.
- **Typical deliverables:** application inventory with migration pattern per application,
  readiness report, wave plan, cost estimate for the target platform.
- **Typical roles and effort:** Cloud Architect 8–12 days, Security Consultant 2–4 days,
  Business Analyst 4–6 days, Project Manager 2–4 days.
- **Typical duration:** 4–6 weeks.
- **Customer dependencies:** read access to inventory tools and architecture documentation.
- **Out of scope:** migration execution, licence optimisation contracts.
- **Preferred pricing model:** fixed price.

## SVC-03 Landing Zone Setup

- **Summary:** Build of a secure, policy-governed cloud foundation with identity, networking,
  monitoring and cost management, delivered as infrastructure as code.
- **Typical deliverables:** landing zone design, infrastructure-as-code repository, policy set,
  operations handbook, handover session.
- **Typical roles and effort:** Cloud Architect 10–15 days, DevOps Engineer 15–25 days,
  Security Consultant 5–8 days, Project Manager 4–6 days.
- **Typical duration:** 6–10 weeks.
- **Customer dependencies:** tenant and subscription owner rights, network address plan, named platform owner.
- **Out of scope:** workload migration, 24/7 operations.
- **Preferred pricing model:** fixed price or capped time and materials.

## SVC-04 Application Modernisation

- **Summary:** Re-platforming or refactoring of an existing application to cloud-native services,
  including CI/CD and automated tests.
- **Typical deliverables:** target architecture, modernised application release, CI/CD pipeline,
  test automation suite, runbook.
- **Typical roles and effort:** Solution Architect 10–20 days, Senior Developer 30–60 days,
  Developer 40–80 days, DevOps Engineer 10–20 days, Test Engineer 15–30 days, Project Manager 10–20 days.
- **Typical duration:** 3–6 months.
- **Customer dependencies:** source code access, product owner with decision authority, test environments.
- **Out of scope:** functional redesign beyond agreed user stories, data migration of archived data.
- **Preferred pricing model:** time and materials or capped time and materials.

## SVC-05 Data Platform

- **Summary:** Design and build of an analytical data platform with ingestion pipelines, a governed
  data model and self-service reporting.
- **Typical deliverables:** data platform architecture, ingestion pipelines, curated data model,
  data quality checks, reporting starter pack.
- **Typical roles and effort:** Solution Architect 8–12 days, Data Engineer 40–70 days,
  Data Scientist 5–10 days, Business Analyst 10–15 days, Project Manager 8–12 days.
- **Typical duration:** 3–5 months.
- **Customer dependencies:** access to source systems, named data owners, agreed data classification.
- **Out of scope:** master data management, operational reporting on source systems.
- **Preferred pricing model:** time and materials.

## SVC-06 Generative AI Proof of Concept

- **Summary:** Time-boxed proof of concept for a generative AI agent on Azure AI Foundry, including
  grounding on customer documents, evaluation and a responsible AI review.
- **Typical deliverables:** use-case definition, working prototype, evaluation report with quality
  metrics, responsible AI assessment, recommendation for production.
- **Typical roles and effort:** AI Engineer 15–25 days, Solution Architect 4–6 days,
  Data Scientist 5–8 days, UX Designer 3–5 days, Engagement Manager 2–3 days.
- **Typical duration:** 6–8 weeks.
- **Customer dependencies:** sample documents cleared for use, test users, an Azure subscription with model quota.
- **Out of scope:** production hardening, 24/7 support, fine-tuning of foundation models.
- **Preferred pricing model:** fixed price.

## SVC-07 DevOps and CI/CD Enablement

- **Summary:** Introduction of trunk-based development, automated pipelines, infrastructure as code
  and quality gates for one or more product teams.
- **Typical deliverables:** pipeline templates, branch and release policy, quality gate definitions,
  team enablement sessions.
- **Typical roles and effort:** DevOps Engineer 15–25 days, Senior Developer 5–10 days, Trainer 3–5 days.
- **Typical duration:** 4–8 weeks.
- **Customer dependencies:** administrator access to the source control and pipeline platform.
- **Out of scope:** licence procurement, operation of build agents after handover.
- **Preferred pricing model:** time and materials.

## SVC-08 Security and Compliance Review

- **Summary:** Review of a solution against security baselines and applicable regulations, with a
  prioritised remediation plan.
- **Typical deliverables:** threat model, findings report with severity ratings, remediation plan.
- **Typical roles and effort:** Security Consultant 8–15 days, Solution Architect 2–4 days.
- **Typical duration:** 3–5 weeks.
- **Customer dependencies:** architecture documentation, read access to configuration, named security contact.
- **Out of scope:** penetration testing by accredited testers, legal advice, certification audits.
- **Preferred pricing model:** fixed price.

## SVC-09 Training and Enablement

- **Summary:** Role-based training for customer teams, delivered on site or remotely.
- **Typical deliverables:** training plan, training material, hands-on labs, attendance summary.
- **Typical roles and effort:** Trainer 1 day per training day plus 1–2 days preparation per new topic,
  Technical Writer 2–5 days for new material.
- **Typical duration:** 1–4 weeks.
- **Customer dependencies:** training rooms or virtual meeting platform, participant list, lab subscriptions.
- **Out of scope:** certification exam fees.
- **Preferred pricing model:** fixed price per training day.

## SVC-10 Hypercare

- **Summary:** Time-boxed post-go-live support with defined response times during business hours.
- **Typical deliverables:** hypercare plan, incident log, handover report to operations.
- **Typical roles and effort:** Developer or DevOps Engineer 5–10 days per month, Project Manager 1–2 days per month.
- **Typical duration:** 4–8 weeks after go-live.
- **Customer dependencies:** incident management tool access, named operations contact.
- **Out of scope:** 24/7 support, new features.
- **Preferred pricing model:** time and materials with a monthly cap.
