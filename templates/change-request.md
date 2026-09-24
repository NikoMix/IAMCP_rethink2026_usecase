---
template_id: change-request
title: Change Request
version: 0.1.0
status: draft
related: [sow, msa]
required_fields:
  - document.number
  - document.date
  - supplier.name
  - customer.name
  - sow.reference
  - cr.title
  - cr.requested_by
  - cr.description
  - cr.reason
  - cr.impact.cost
  - cr.impact.schedule
---

# Change Request

| Field | Value |
| --- | --- |
| Change Request No. | {{ document.number }} |
| Title | {{ cr.title }} |
| Date | {{ document.date }} |
| Related SOW | {{ sow.reference }}{% if sow.date %} dated {{ sow.date }}{% endif %} |
| Governing agreement | {{ msa.reference | default("as stated in the SOW") }} |
| Requested by | {{ cr.requested_by }} |
| Priority | {{ cr.priority | default("Medium") }} |
| Requested decision date | {{ cr.decision_due | default("[date]") }} |

## 1. Description of the change

{{ cr.description }}

## 2. Reason for the change

{{ cr.reason }}

## 3. Changes to scope

| Change | SOW section | Current | Proposed |
| --- | --- | --- | --- |
{% for c in cr.scope_changes %}
| {{ c.type }} | {{ c.section }} | {{ c.current }} | {{ c.proposed }} |
{% endfor %}

[GUIDANCE: Use the change types Add, Modify, or Remove.]

## 4. Impact analysis

### 4.1 Cost

| Item | Role or category | Days or quantity | Rate | Amount ({{ pricing.currency }}) |
| --- | --- | --- | --- | --- |
{% for i in cr.impact.cost_items %}
| {{ i.item }} | {{ i.category }} | {{ i.quantity }} | {{ i.rate }} | {{ i.amount }} |
{% endfor %}

- Original SOW value: {{ pricing.currency }} {{ cr.impact.original_value }}
- Net change: {{ pricing.currency }} {{ cr.impact.cost }}
- Revised SOW value: {{ pricing.currency }} {{ cr.impact.revised_value }}

### 4.2 Schedule

{{ cr.impact.schedule }}

| Milestone | Current date | Revised date |
| --- | --- | --- |
{% for m in cr.impact.milestones %}
| {{ m.name }} | {{ m.current_date }} | {{ m.revised_date }} |
{% endfor %}

### 4.3 Resources

{{ cr.impact.resources | default("No change to resources.") }}

### 4.4 Risks

{% for r in cr.impact.risks %}
- {{ r }}
{% endfor %}

## 5. Assumptions

{% for a in cr.assumptions %}
- {{ a }}
{% endfor %}

## 6. Terms

All terms of the related SOW and the governing agreement remain unchanged, except as expressly amended
by this Change Request. This Change Request becomes effective when both Parties sign it.

## 7. Decision

- [ ] Approved
- [ ] Approved with conditions: ______________________________
- [ ] Rejected. Reason: ______________________________

## Signatures

| For the Supplier | For the Customer |
| --- | --- |
| {{ supplier.name }} | {{ customer.name }} |
| Name: {{ supplier.signatory.name }} | Name: {{ customer.signatory.name }} |
| Title: {{ supplier.signatory.title }} | Title: {{ customer.signatory.title }} |
| Date: ______________ | Date: ______________ |
| Signature: ______________ | Signature: ______________ |
