---
template_id: rfp
title: Request for Proposal
version: 0.1.0
status: draft
related: [rfi, msa, sow]
required_fields:
  - document.number
  - document.date
  - customer.name
  - customer.contact.name
  - customer.contact.email
  - project.name
  - project.summary
  - project.objectives
  - rfp.requirements
  - rfp.evaluation_criteria
  - rfp.submission_deadline
---

# Request for Proposal

**Reference:** {{ document.number }}  
**Issued by:** {{ customer.name }}  
**Issue date:** {{ document.date }}  
**Proposal due:** {{ rfp.submission_deadline }}  
**Confidentiality:** {{ document.confidentiality | default("Confidential") }}

## 1. Introduction

{{ customer.name }} ("the Customer") invites proposals for **{{ project.name }}**.
{% if rfp.rfi_reference %}This RFP follows RFI {{ rfp.rfi_reference }}.{% endif %}

## 2. Customer background

{{ customer.profile }}

## 3. Project overview

{{ project.summary }}

### 3.1 Objectives

{% for objective in project.objectives %}
{{ loop.index }}. {{ objective }}
{% endfor %}

### 3.2 Current situation

{{ project.background }}

## 4. Scope of work

### 4.1 In scope

{% for item in rfp.scope.in_scope %}
- {{ item }}
{% endfor %}

### 4.2 Out of scope

{% for item in rfp.scope.out_of_scope %}
- {{ item }}
{% endfor %}

## 5. Requirements

Priority uses MoSCoW: **M**ust, **S**hould, **C**ould, **W**on't (this time).

| ID | Category | Requirement | Priority |
| --- | --- | --- | --- |
{% for r in rfp.requirements %}
| {{ r.id }} | {{ r.category }} | {{ r.text }} | {{ r.priority }} |
{% endfor %}

## 6. Expected deliverables

{% for d in rfp.deliverables %}
- **{{ d.name }}:** {{ d.description }}
{% endfor %}

## 7. Target timeline

- Desired start: {{ project.start_date }}
- Desired completion: {{ project.end_date }}

## 8. Proposal structure

Proposals must follow this structure so that they can be compared:

1. Executive summary
2. Understanding of requirements
3. Proposed solution and approach
4. Project plan, milestones, and staffing
5. Response to each requirement in section 5 (compliance matrix)
6. Commercial proposal (see section 10)
7. Relevant references and team CVs
8. Assumptions, dependencies, and risks
9. Deviations from the contractual terms in section 11

## 9. Evaluation criteria

| Criterion | Weight |
| --- | --- |
{% for c in rfp.evaluation_criteria %}
| {{ c.name }} | {{ c.weight }} % |
{% endfor %}

[GUIDANCE: Weights must total 100 %. The generator validates this.]

## 10. Commercial requirements

- Currency: {{ pricing.currency }}
- Preferred pricing model: {{ pricing.model | default("to be proposed by the bidder") }}
- Provide a price breakdown per deliverable or phase, the rate card for all roles, and expected expenses.
- Prices must remain valid for {{ rfp.price_validity_days | default(90) }} days after the submission deadline.

## 11. Contractual framework

{% if msa.reference %}
The engagement will be governed by Master Service Agreement {{ msa.reference }}.
{% else %}
The successful bidder will be asked to sign the Customer's Master Service Agreement and a Statement of Work.
{% endif %}
Bidders must list any requested deviations in their proposal.

## 12. Procurement timeline

| Step | Date |
| --- | --- |
{% for step in rfp.timeline %}
| {{ step.name }} | {{ step.date }} |
{% endfor %}

## 13. Submission instructions

- Submit electronically to {{ customer.contact.email }} by **{{ rfp.submission_deadline }}**.
- Late submissions may be rejected.
- Clarification questions must be received by {{ rfp.questions_deadline | default("[date]") }}; answers are shared with all bidders.

## 14. General conditions

- This RFP does not oblige the Customer to award a contract.
- Bidders bear all costs of preparing their proposal.
- All information in this RFP is confidential and may be used only to prepare a proposal.

**Contact:** {{ customer.contact.name }}, {{ customer.contact.email }}
