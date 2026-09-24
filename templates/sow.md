---
template_id: sow
title: Statement of Work
version: 0.2.0
status: in_review
related: [msa, change-request]
# Field paths follow the shared schema (schemas/sow.schema.json, owned by #7).
# `list[].field` means "required for each item when the list has items".
# Every placeholder, loop source, and condition field appears in exactly one of
# required_fields or optional_fields.
required_fields:
  - document.number
  - document.version
  - document.date
  - document.status
  - supplier.name
  - customer.name
  - msa.reference
  - project.name
  - project.summary
  - project.objectives
  - sow.scope.in_scope
  - sow.deliverables
  - sow.deliverables[].id
  - sow.deliverables[].name
  - sow.deliverables[].acceptance_criteria
  - sow.deliverables[].due_date
  - sow.milestones
  - sow.milestones[].name
  - sow.milestones[].date
  - sow.phases[].name
  - sow.phases[].description
  - sow.roles[].role
  - sow.roles[].party
  - sow.roles[].responsibilities
  - pricing.model
  - pricing.currency
  - pricing.total
  - pricing.rate_card[].role
  - pricing.rate_card[].daily_rate
  - pricing.payment_schedule[].trigger
  - pricing.payment_schedule[].amount
optional_fields:
  - msa.version
  - msa.date
  - project.background
  - project.start_date
  - project.end_date
  - sow.scope.out_of_scope
  - sow.phases
  - sow.deliverables[].description
  - sow.milestones[].deliverables
  - sow.roles
  - sow.roles[].name
  - sow.customer_dependencies
  - sow.assumptions
  - sow.acceptance_days
  - sow.reporting_cadence
  - sow.steering_cadence
  - pricing.cap
  - pricing.rate_card
  - pricing.rate_card[].days
  - pricing.rate_card[].subtotal
  - pricing.payment_schedule
  - pricing.payment_schedule[].date
  - pricing.expenses_policy
  - pricing.tax_note
  - supplier.signatory.name
  - supplier.signatory.title
  - customer.signatory.name
  - customer.signatory.title
# Cross-field rules this template relies on, because it cannot compute values.
# The schema (#7) and check_plausibility (#20) own their implementation and finding codes.
validation_rules:
  - id: sow.msa_reference_required
    severity: error
    fields:
      - msa.reference
    rule: Reject the document when msa.reference is missing or empty.
  - id: sow.pricing_model_allowed
    severity: error
    fields:
      - pricing.model
    rule: pricing.model is one of fixed_price, time_and_materials, capped_tm.
  - id: sow.rate_card_subtotal
    severity: error
    fields:
      - pricing.rate_card[].daily_rate
      - pricing.rate_card[].days
      - pricing.rate_card[].subtotal
    rule: Each rate card subtotal equals daily_rate multiplied by days.
  - id: sow.rate_card_total
    severity: error
    fields:
      - pricing.rate_card[].subtotal
      - pricing.total
    rule: When pricing.rate_card has items, the sum of all subtotals equals pricing.total.
  - id: sow.rate_card_required_for_tm
    severity: error
    fields:
      - pricing.model
      - pricing.rate_card
    rule: When pricing.model is time_and_materials or capped_tm, pricing.rate_card has at least one item.
  - id: sow.cap_required
    severity: error
    fields:
      - pricing.model
      - pricing.cap
      - pricing.total
    rule: When pricing.model is capped_tm, pricing.cap is present and pricing.total does not exceed it.
  - id: sow.cap_only_for_capped_tm
    severity: warning
    fields:
      - pricing.model
      - pricing.cap
    rule: When pricing.model is not capped_tm, pricing.cap is absent, because the template does not render it.
  - id: sow.payment_schedule_total
    severity: error
    fields:
      - pricing.model
      - pricing.payment_schedule[].amount
      - pricing.total
    rule: When pricing.model is fixed_price and pricing.payment_schedule has items, the amounts sum to pricing.total.
  - id: sow.milestone_deliverables_exist
    severity: warning
    fields:
      - sow.milestones[].deliverables
      - sow.deliverables[].id
    rule: Every deliverable ID linked to a milestone exists in sow.deliverables.
review:
  # Approval records. Update when the review is complete (#8 delivery lead, #44 legal).
  delivery_lead: { status: pending, reviewer: null, date: null }
  legal: { status: pending, reviewer: null, date: null }
changelog:
  - version: 0.2.0
    changes: >-
      Separate sections for fixed price, time and materials, and time and materials
      with cap; restrict syntax to the shared subset; add optional_fields,
      validation rules, review records, the MSA version, and the legal disclaimer.
  - version: 0.1.0
    changes: First draft.
---

# Statement of Work

{% if document.status != "final" %}
> **Draft – not legal advice.** This document was generated from a template. It is not legal advice
> and must be reviewed by qualified legal counsel before it is signed or shared with the other party.
{% endif %}

**SOW No.:** {{ document.number }} | **Version:** {{ document.version }} | **Date:** {{ document.date }}  
**Project:** {{ project.name }}  
**Governing agreement:** Master Service Agreement {{ msa.reference }}{% if msa.version %}, version {{ msa.version }}{% endif %}{% if msa.date %}, dated {{ msa.date }}{% endif %}

This Statement of Work ("SOW") is entered into between **{{ supplier.name }}** ("Supplier") and
**{{ customer.name }}** ("Customer") under the governing agreement named above. The terms of the
governing agreement apply to this SOW. If this SOW conflicts with the governing agreement, the governing
agreement prevails unless this SOW expressly states that it overrides a specific clause.

## 1. Background and objectives

{{ project.summary }}

{% if project.background %}
{{ project.background }}
{% endif %}

The objectives of this SOW are:

{% for objective in project.objectives %}- {{ objective }}
{% endfor %}

## 2. Scope of services

### 2.1 In scope

{% for item in sow.scope.in_scope %}- {{ item }}
{% endfor %}

### 2.2 Out of scope

{% if sow.scope.out_of_scope %}
{% for item in sow.scope.out_of_scope %}- {{ item }}
{% endfor %}
{% endif %}

Any work not listed in section 2.1 is out of scope and requires a Change Request.

## 3. Approach and phases

{% if sow.phases %}
{% for phase in sow.phases %}
### {{ phase.name }}

{{ phase.description }}
{% endfor %}
{% else %}
The Supplier plans the approach with the Customer at the start of the project.
{% endif %}

## 4. Deliverables

| ID | Deliverable | Description | Acceptance criteria | Due date |
| --- | --- | --- | --- | --- |
{% for d in sow.deliverables %}| {{ d.id }} | {{ d.name }} | {% if d.description %}{{ d.description }}{% else %}–{% endif %} | {{ d.acceptance_criteria }} | {{ d.due_date }} |
{% endfor %}

## 5. Timeline and milestones

{% if project.start_date %}- Start date: {{ project.start_date }}
{% endif %}{% if project.end_date %}- End date: {{ project.end_date }}
{% endif %}

| Milestone | Target date | Linked deliverables |
| --- | --- | --- |
{% for m in sow.milestones %}| {{ m.name }} | {{ m.date }} | {% for linked in m.deliverables %}{{ linked }} {% endfor %}|
{% endfor %}

## 6. Roles and responsibilities

{% if sow.roles %}
| Role | Party | Name | Responsibilities |
| --- | --- | --- | --- |
{% for r in sow.roles %}| {{ r.role }} | {% if r.party == "supplier" %}Supplier{% endif %}{% if r.party == "customer" %}Customer{% endif %} | {% if r.name %}{{ r.name }}{% else %}To be confirmed{% endif %} | {{ r.responsibilities }} |
{% endfor %}
{% else %}
Each Party names a project manager at the start of the project. The project managers agree further roles in writing.
{% endif %}

## 7. Customer dependencies

The Customer provides the following in time for the Supplier to perform the services:

{% if sow.customer_dependencies %}
{% for dep in sow.customer_dependencies %}- {{ dep }}
{% endfor %}
{% else %}
- Timely access to the people, information, and systems the services require.
{% endif %}

If the Customer does not meet a dependency, the Supplier is not responsible for the resulting delay,
and the Parties agree the impact through a Change Request.

## 8. Assumptions

{% if sow.assumptions %}
{% for a in sow.assumptions %}- {{ a }}
{% endfor %}
{% else %}
The Parties have not agreed any assumptions beyond those in this SOW.
{% endif %}

If an assumption proves incorrect, the Parties agree any resulting impact through a Change Request.

## 9. Acceptance procedure

1. The Supplier notifies the Customer in writing that a Deliverable is complete.
2. The Customer reviews the Deliverable against its acceptance criteria within {% if sow.acceptance_days %}{{ sow.acceptance_days }}{% else %}10{% endif %} business days.
3. The Customer either accepts the Deliverable in writing or lists specific non-conformities.
4. The Supplier corrects the non-conformities and resubmits the Deliverable; steps 2 and 3 repeat.
5. If the Customer does not respond within the review period, the Deliverable is deemed accepted.

## 10. Commercials

All amounts are in {{ pricing.currency }}.

{% if pricing.model == "fixed_price" %}
### 10.1 Fixed price

The fixed price for all services and Deliverables in this SOW is **{{ pricing.currency }} {{ pricing.total }}**,
excluding taxes. The fixed price covers the scope in section 2.1. Additional work requires a Change Request.
{% endif %}
{% if pricing.model == "time_and_materials" %}
### 10.1 Time and materials

The Supplier charges the services on a time-and-materials basis at the rates in section 10.2, based on
the time actually spent. The estimated total is **{{ pricing.currency }} {{ pricing.total }}**, excluding
taxes. The estimate is not binding; the Supplier informs the Customer promptly if it expects to exceed it.
{% endif %}
{% if pricing.model == "capped_tm" %}
### 10.1 Time and materials with cap

The Supplier charges the services on a time-and-materials basis at the rates in section 10.2, based on
the time actually spent. The estimated total is **{{ pricing.currency }} {{ pricing.total }}**, excluding taxes.

Total fees under this SOW will not exceed the cap of **{{ pricing.currency }} {{ pricing.cap }}**, excluding
taxes, without an approved Change Request. The Supplier notifies the Customer in writing as soon as it
expects fees to reach the cap.
{% endif %}

{% if pricing.rate_card %}
### 10.2 Rate card

{% if pricing.model == "fixed_price" %}
The rates below were used to calculate the fixed price and apply to additional work agreed through a Change Request.
{% endif %}

| Role | Daily rate ({{ pricing.currency }}) | Estimated days | Subtotal ({{ pricing.currency }}) |
| --- | --- | --- | --- |
{% for r in pricing.rate_card %}| {{ r.role }} | {{ r.daily_rate }} | {% if r.days %}{{ r.days }}{% else %}–{% endif %} | {% if r.subtotal %}{{ r.subtotal }}{% else %}–{% endif %} |
{% endfor %}| **Total** | | | **{{ pricing.total }}** |
{% endif %}

### 10.3 Payment schedule

{% if pricing.payment_schedule %}
| Trigger | Amount ({{ pricing.currency }}) | Planned date |
| --- | --- | --- |
{% for p in pricing.payment_schedule %}| {{ p.trigger }} | {{ p.amount }} | {% if p.date %}{{ p.date }}{% else %}On trigger{% endif %} |
{% endfor %}
{% else %}
The Supplier invoices monthly in arrears. Payment terms follow the governing agreement.
{% endif %}

### 10.4 Expenses and taxes

{% if pricing.expenses_policy %}{{ pricing.expenses_policy }}{% else %}The Customer reimburses pre-approved travel expenses at cost.{% endif %}
{% if pricing.tax_note %}{{ pricing.tax_note }}{% else %}All amounts exclude value-added tax, which is charged at the applicable statutory rate.{% endif %}

## 11. Change management

Either Party may request changes using the Change Request template. A change becomes effective only when
both Parties sign it. Each Change Request cites this SOW by number and version and states its impact on
scope, schedule, fees, resources, and risk.

## 12. Governance and reporting

- Status report: {% if sow.reporting_cadence %}{{ sow.reporting_cadence }}{% else %}weekly{% endif %}
- Steering committee: {% if sow.steering_cadence %}{{ sow.steering_cadence }}{% else %}monthly{% endif %}
- Escalation path: project managers, then sponsors, then executive management

## 13. Term

This SOW takes effect on the date of the last signature and ends when all Deliverables are accepted,
unless terminated earlier under the governing agreement.

## Signatures

| For the Supplier | For the Customer |
| --- | --- |
| {{ supplier.name }} | {{ customer.name }} |
| Name: {% if supplier.signatory.name %}{{ supplier.signatory.name }}{% else %}______________{% endif %} | Name: {% if customer.signatory.name %}{{ customer.signatory.name }}{% else %}______________{% endif %} |
| Title: {% if supplier.signatory.title %}{{ supplier.signatory.title }}{% else %}______________{% endif %} | Title: {% if customer.signatory.title %}{{ customer.signatory.title }}{% else %}______________{% endif %} |
| Date: ______________ | Date: ______________ |
| Signature: ______________ | Signature: ______________ |
