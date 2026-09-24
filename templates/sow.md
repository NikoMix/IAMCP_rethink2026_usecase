---
template_id: sow
title: Statement of Work
version: 0.1.0
status: draft
related: [msa, change-request]
required_fields:
  - document.number
  - document.version
  - document.date
  - supplier.name
  - customer.name
  - msa.reference
  - project.name
  - project.summary
  - project.objectives
  - sow.scope.in_scope
  - sow.deliverables
  - sow.milestones
  - pricing.model
  - pricing.currency
  - pricing.total
---

# Statement of Work

**SOW No.:** {{ document.number }} | **Version:** {{ document.version }} | **Date:** {{ document.date }}  
**Project:** {{ project.name }}  
**Governing agreement:** Master Service Agreement {{ msa.reference }}{% if msa.date %} dated {{ msa.date }}{% endif %}

This Statement of Work ("SOW") is entered into between **{{ supplier.name }}** ("Supplier") and
**{{ customer.name }}** ("Customer") under the governing agreement named above. If this SOW conflicts
with the governing agreement, the governing agreement prevails unless this SOW expressly states otherwise.

## 1. Background and objectives

{{ project.summary }}

{% for objective in project.objectives %}
{{ loop.index }}. {{ objective }}
{% endfor %}

## 2. Scope of services

### 2.1 In scope

{% for item in sow.scope.in_scope %}
- {{ item }}
{% endfor %}

### 2.2 Out of scope

{% for item in sow.scope.out_of_scope %}
- {{ item }}
{% endfor %}

Any work not listed in section 2.1 is out of scope and requires a Change Request.

## 3. Approach and phases

{% for phase in sow.phases %}
### 3.{{ loop.index }} {{ phase.name }}

{{ phase.description }}
{% endfor %}

## 4. Deliverables

| ID | Deliverable | Description | Acceptance criteria | Due date |
| --- | --- | --- | --- | --- |
{% for d in sow.deliverables %}
| {{ d.id }} | {{ d.name }} | {{ d.description }} | {{ d.acceptance_criteria }} | {{ d.due_date }} |
{% endfor %}

## 5. Timeline and milestones

- Start date: {{ project.start_date }}
- End date: {{ project.end_date }}

| Milestone | Target date | Linked deliverables |
| --- | --- | --- |
{% for m in sow.milestones %}
| {{ m.name }} | {{ m.date }} | {{ m.deliverables | join(", ") }} |
{% endfor %}

## 6. Roles and responsibilities

| Role | Party | Name | Responsibilities |
| --- | --- | --- | --- |
{% for r in sow.roles %}
| {{ r.role }} | {{ r.party }} | {{ r.name | default("TBD") }} | {{ r.responsibilities }} |
{% endfor %}

## 7. Customer dependencies

{% for dep in sow.customer_dependencies %}
- {{ dep }}
{% endfor %}

## 8. Assumptions

{% for a in sow.assumptions %}
- {{ a }}
{% endfor %}

If an assumption proves incorrect, the Parties agree any resulting impact through a Change Request.

## 9. Acceptance procedure

1. The Supplier notifies the Customer in writing that a Deliverable is complete.
2. The Customer reviews the Deliverable against its acceptance criteria within {{ sow.acceptance_days | default(10) }} business days.
3. The Customer either accepts the Deliverable in writing or lists specific non-conformities.
4. The Supplier corrects the non-conformities and resubmits the Deliverable; steps 2 and 3 repeat.
5. If the Customer does not respond within the review period, the Deliverable is deemed accepted.

## 10. Commercials

**Pricing model:** {{ pricing.model }} | **Currency:** {{ pricing.currency }}

{% if pricing.model == "fixed_price" %}
The fixed price for the services in this SOW is **{{ pricing.currency }} {{ pricing.total }}**, excluding taxes.
{% else %}
Services are charged on a time-and-materials basis at the rates below.
{% if pricing.cap %}Total fees will not exceed **{{ pricing.currency }} {{ pricing.cap }}** without an approved Change Request.{% endif %}
Estimated total: **{{ pricing.currency }} {{ pricing.total }}**, excluding taxes.
{% endif %}

### 10.1 Rate card

| Role | Daily rate ({{ pricing.currency }}) | Estimated days | Subtotal ({{ pricing.currency }}) |
| --- | --- | --- | --- |
{% for r in pricing.rate_card %}
| {{ r.role }} | {{ r.daily_rate }} | {{ r.days }} | {{ r.subtotal }} |
{% endfor %}

### 10.2 Payment schedule

| Trigger | Amount ({{ pricing.currency }}) | Planned date |
| --- | --- | --- |
{% for p in pricing.payment_schedule %}
| {{ p.trigger }} | {{ p.amount }} | {{ p.date }} |
{% endfor %}

### 10.3 Expenses and taxes

{{ pricing.expenses_policy | default("Pre-approved travel expenses are reimbursed at cost.") }}
{{ pricing.tax_note | default("All amounts exclude value-added tax.") }}

## 11. Change management

Either Party may request changes using the Change Request template. A change becomes effective only when
both Parties sign it. Each Change Request states its impact on scope, schedule, fees, and risk.

## 12. Governance and reporting

- Status report: {{ sow.reporting_cadence | default("weekly") }}
- Steering committee: {{ sow.steering_cadence | default("monthly") }}
- Escalation path: project managers, then sponsors, then executive management

## 13. Term

This SOW takes effect on the date of the last signature and ends when all Deliverables are accepted,
unless terminated earlier under the governing agreement.

## Signatures

| For the Supplier | For the Customer |
| --- | --- |
| {{ supplier.name }} | {{ customer.name }} |
| Name: {{ supplier.signatory.name }} | Name: {{ customer.signatory.name }} |
| Title: {{ supplier.signatory.title }} | Title: {{ customer.signatory.title }} |
| Date: ______________ | Date: ______________ |
| Signature: ______________ | Signature: ______________ |
