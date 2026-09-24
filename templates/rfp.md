---
template_id: rfp
title: Request for Proposal
version: 0.2.0
status: in_review
related: [rfi, msa, sow]
# Field paths follow the shared schema (owned by #7) and its parity tests.
# A path through a list addresses each item, for example rfp.requirements.priority.
# required_fields: required at every level of the schema; the document is
#   rejected when one is missing.
# optional_fields: every other field the template uses. Each one is rendered
#   inside an if/for guard, with default text where the section needs it.
required_fields:
  - document.number
  - document.date
  - document.language
  - customer.name
  - customer.contact.name
  - customer.contact.email
  - project.name
  - project.summary
  - project.objectives
  - rfp.requirements
  - rfp.requirements.id
  - rfp.requirements.text
  - rfp.requirements.priority
  - rfp.evaluation_criteria
  - rfp.evaluation_criteria.name
  - rfp.evaluation_criteria.weight
  - rfp.submission_deadline
optional_fields:
  - document.confidentiality
  - customer.profile
  - project.background
  - project.start_date
  - project.end_date
  - rfp.rfi_reference
  - rfp.scope
  - rfp.scope.in_scope
  - rfp.scope.out_of_scope
  - rfp.requirements.category
  - rfp.deliverables
  - rfp.deliverables.name
  - rfp.deliverables.description
  - rfp.price_validity_days
  - rfp.questions_deadline
  - rfp.timeline
  - rfp.timeline.name
  - rfp.timeline.date
  - pricing
  - pricing.currency
  - pricing.model
  - msa
  - msa.reference
  - msa.date
---

# Request for Proposal

**Reference:** {{ document.number }}  
**Issued by:** {{ customer.name }}  
**Issue date:** {{ document.date }}  
**Proposal due:** {{ rfp.submission_deadline }}  
**Confidentiality:** {% if document.confidentiality %}{{ document.confidentiality }}{% else %}Confidential{% endif %}{% if rfp.rfi_reference %}  
**Preceding RFI:** {{ rfp.rfi_reference }}{% endif %}

## 1. Introduction

{{ customer.name }} ("the Customer") invites proposals for **{{ project.name }}**.
{% if rfp.rfi_reference %}
This RFP follows Request for Information {{ rfp.rfi_reference }} and builds on the market
information gathered through it.
{% endif %}
This RFP requests binding, comparable proposals. Bidders must follow the proposal structure in
section 8 and the submission instructions in section 13.

## 2. Customer background

{% if customer.profile %}{{ customer.profile }}{% else %}{{ customer.name }} shares further details about its organisation with bidders on request.{% endif %}

## 3. Project overview

{{ project.summary }}

### 3.1 Objectives
{% for objective in project.objectives %}
1. {{ objective }}{% endfor %}

### 3.2 Current situation

{% if project.background %}{{ project.background }}{% else %}No further background is provided at this stage.{% endif %}

## 4. Scope of work

{% if rfp.scope %}### 4.1 In scope
{% for item in rfp.scope.in_scope %}
- {{ item }}{% endfor %}

### 4.2 Out of scope
{% if rfp.scope.out_of_scope %}{% for item in rfp.scope.out_of_scope %}
- {{ item }}{% endfor %}{% else %}
No exclusions are defined beyond the requirements in section 5.{% endif %}{% else %}The scope of work is defined by the requirements in section 5 and the deliverables in section 6.{% endif %}

## 5. Requirements

Each requirement has a MoSCoW priority: **Must** (mandatory), **Should** (important),
**Could** (desirable), or **Won't** (explicitly not required this time).
Proposals that do not meet every Must requirement may be excluded from evaluation.

| ID | Category | Requirement | Priority |
| --- | --- | --- | --- |{% for r in rfp.requirements %}
| {{ r.id }} | {% if r.category %}{{ r.category }}{% endif %} | {{ r.text }} | {% if r.priority == "must" %}Must{% else %}{% if r.priority == "should" %}Should{% else %}{% if r.priority == "could" %}Could{% else %}{% if r.priority == "wont" %}Won't{% else %}{{ r.priority }}{% endif %}{% endif %}{% endif %}{% endif %} |{% endfor %}

## 6. Expected deliverables
{% if rfp.deliverables %}{% for d in rfp.deliverables %}
- **{{ d.name }}**{% if d.description %}: {{ d.description }}{% endif %}{% endfor %}{% else %}
Bidders propose the deliverables needed to meet the requirements in section 5.{% endif %}

## 7. Target timeline

- Desired start: {% if project.start_date %}{{ project.start_date }}{% else %}to be proposed by the bidder{% endif %}
- Desired completion: {% if project.end_date %}{{ project.end_date }}{% else %}to be proposed by the bidder{% endif %}

## 8. Proposal structure

Proposals must follow this structure so that they can be compared:

1. Executive summary
2. Understanding of requirements
3. Proposed solution and approach
4. Project plan, milestones, and staffing
5. Compliance matrix: for each requirement in section 5, state whether it is fully met, partly met, or not met, with a short explanation
6. Commercial proposal (see section 10)
7. Relevant references and team CVs
8. Assumptions, dependencies, and risks
9. Requested deviations from the contractual framework in section 11

## 9. Evaluation criteria

The Customer evaluates each proposal against the criteria below. Each weight is the criterion's
share of the total score; the weights total 100 %.

| Criterion | Weight |
| --- | --- |{% for c in rfp.evaluation_criteria %}
| {{ c.name }} | {{ c.weight }} % |{% endfor %}

## 10. Commercial requirements

{% if pricing %}- Currency: {% if pricing.currency %}{{ pricing.currency }}{% else %}to be stated by the bidder{% endif %}
- Preferred pricing model: {% if pricing.model == "fixed_price" %}Fixed price{% else %}{% if pricing.model == "time_and_materials" %}Time and materials{% else %}{% if pricing.model == "capped_tm" %}Time and materials with a cap{% else %}to be proposed by the bidder{% endif %}{% endif %}{% endif %}{% else %}- Currency: to be stated by the bidder
- Preferred pricing model: to be proposed by the bidder{% endif %}
- Provide a price breakdown per deliverable or phase, the rate card for all roles, and expected expenses.
- Prices must remain valid for {% if rfp.price_validity_days %}{{ rfp.price_validity_days }}{% else %}90{% endif %} days after the submission deadline.

## 11. Contractual framework

{% if msa %}The engagement will be governed by Master Service Agreement {{ msa.reference }}{% if msa.date %} of {{ msa.date }}{% endif %}, with a Statement of Work for this project.{% else %}The successful bidder will be asked to sign the Customer's Master Service Agreement and a Statement of Work.{% endif %}
Bidders must list any requested deviations in their proposal.

## 12. Procurement timeline

| Step | Date |
| --- | --- |
| RFP issued | {{ document.date }} |{% if rfp.questions_deadline %}
| Clarification questions due | {{ rfp.questions_deadline }} |{% endif %}
| Proposals due | {{ rfp.submission_deadline }} |{% if rfp.timeline %}{% for step in rfp.timeline %}
| {{ step.name }} | {{ step.date }} |{% endfor %}{% endif %}

Dates after the submission deadline are indicative and may change.

## 13. Submission instructions

- Format: PDF, with the commercial proposal (item 6 in section 8) as a separate file
- Language: {% if document.language == "de" %}German{% else %}English{% endif %}
- Submit electronically to {{ customer.contact.email }} by **{{ rfp.submission_deadline }}**.
- Late submissions may be rejected.
- {% if rfp.questions_deadline %}Clarification questions must be received by {{ rfp.questions_deadline }}{% else %}Send clarification questions to {{ customer.contact.name }} as early as possible{% endif %}; answers are shared with all bidders.

## 14. General conditions

- This RFP does not oblige the Customer to award a contract.
- Bidders bear all costs of preparing their proposal.
- All information in this RFP is confidential and may be used only to prepare a proposal.

**Contact:** {{ customer.contact.name }}, {{ customer.contact.email }}
