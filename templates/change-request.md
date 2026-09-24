---
template_id: change-request
title: Change Request
version: 0.2.0
status: in_review
related: [sow, msa]
# Field paths follow the shared schema (schemas/change-request.schema.json, owned by #7).
# `sow.*` identifies the parent SOW this Change Request amends, so the generator can
# carry over its number, version, parties, currency, and value (#13).
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
  - sow.reference
  - sow.version
  - pricing.currency
  - cr.title
  - cr.requested_by
  - cr.description
  - cr.reason
  - cr.scope_changes[].type
  - cr.scope_changes[].proposed
  - cr.impact.cost_items[].item
  - cr.impact.cost_items[].amount
  - cr.impact.original_value
  - cr.impact.cost
  - cr.impact.revised_value
  - cr.impact.schedule
  - cr.impact.milestones[].name
  - cr.impact.milestones[].current_date
  - cr.impact.milestones[].revised_date
optional_fields:
  - sow.date
  - msa.reference
  - msa.version
  - msa.date
  - cr.priority
  - cr.decision_due
  - cr.scope_changes
  - cr.scope_changes[].section
  - cr.scope_changes[].current
  - cr.impact.cost_items
  - cr.impact.cost_items[].category
  - cr.impact.cost_items[].quantity
  - cr.impact.cost_items[].rate
  - cr.impact.milestones
  - cr.impact.resources
  - cr.impact.risks
  - cr.assumptions
  - supplier.signatory.name
  - supplier.signatory.title
  - customer.signatory.name
  - customer.signatory.title
# Cross-field rules this template relies on, because it cannot compute values.
# The schema (#7) and check_plausibility (#20) own their implementation and finding codes.
validation_rules:
  - id: cr.sow_reference_required
    severity: error
    fields:
      - sow.reference
      - sow.version
    rule: Reject the Change Request when sow.reference or sow.version is missing or empty.
  - id: cr.revised_value
    severity: error
    fields:
      - cr.impact.original_value
      - cr.impact.cost
      - cr.impact.revised_value
    rule: cr.impact.revised_value equals cr.impact.original_value plus cr.impact.cost.
  - id: cr.cost_items_total
    severity: error
    fields:
      - cr.impact.cost_items[].amount
      - cr.impact.cost
    rule: When cr.impact.cost_items has items, the amounts sum to cr.impact.cost.
  - id: cr.scope_change_type_allowed
    severity: error
    fields:
      - cr.scope_changes[].type
    rule: cr.scope_changes[].type is one of add, modify, remove.
  - id: cr.revised_value_not_negative
    severity: error
    fields:
      - cr.impact.revised_value
    rule: cr.impact.revised_value is zero or greater.
review:
  # Approval records. Update when the review is complete (#44 legal).
  delivery_lead: { status: pending, reviewer: null, date: null }
  legal: { status: pending, reviewer: null, date: null }
changelog:
  - version: 0.2.0
    changes: >-
      Cite the parent SOW by number and version; render the net change
      (cr.impact.cost) with a sign; add original and revised SOW values; label
      scope change types and priorities; restrict syntax to the shared subset;
      add optional_fields, validation rules, review records, and the legal
      disclaimer.
  - version: 0.1.0
    changes: First draft.
---

# Change Request

{% if document.status != "final" %}
> **Draft – not legal advice.** This document was generated from a template. It is not legal advice
> and must be reviewed by qualified legal counsel before it is signed or shared with the other party.
{% endif %}

| Field | Value |
| --- | --- |
| Change Request No. | {{ document.number }} |
| Version | {{ document.version }} |
| Title | {{ cr.title }} |
| Date | {{ document.date }} |
| Parent SOW | {{ sow.reference }}, version {{ sow.version }}{% if sow.date %}, dated {{ sow.date }}{% endif %} |
| Governing agreement | {% if msa.reference %}Master Service Agreement {{ msa.reference }}{% if msa.version %}, version {{ msa.version }}{% endif %}{% if msa.date %}, dated {{ msa.date }}{% endif %}{% else %}As stated in the parent SOW{% endif %} |
| Supplier | {{ supplier.name }} |
| Customer | {{ customer.name }} |
| Requested by | {{ cr.requested_by }} |
| Priority | {% if cr.priority == "low" %}Low{% endif %}{% if cr.priority == "medium" %}Medium{% endif %}{% if cr.priority == "high" %}High{% endif %}{% if cr.priority == "critical" %}Critical{% endif %}{% if cr.priority %}{% else %}Medium{% endif %} |
| Requested decision date | {% if cr.decision_due %}{{ cr.decision_due }}{% else %}Not specified{% endif %} |

## 1. Description of the change

{{ cr.description }}

## 2. Reason for the change

{{ cr.reason }}

## 3. Changes to scope

{% if cr.scope_changes %}
| Change | SOW section | Current | Proposed |
| --- | --- | --- | --- |
{% for c in cr.scope_changes %}| {% if c.type == "add" %}Add{% endif %}{% if c.type == "modify" %}Modify{% endif %}{% if c.type == "remove" %}Remove{% endif %} | {% if c.section %}{{ c.section }}{% else %}–{% endif %} | {% if c.current %}{{ c.current }}{% else %}–{% endif %} | {{ c.proposed }} |
{% endfor %}
{% else %}
This Change Request does not change the scope of the parent SOW.
{% endif %}

## 4. Impact analysis

### 4.1 Cost

All amounts are in {{ pricing.currency }}, excluding taxes.

{% if cr.impact.cost_items %}
| Item | Role or category | Days or quantity | Rate | Amount ({{ pricing.currency }}) |
| --- | --- | --- | --- | --- |
{% for i in cr.impact.cost_items %}| {{ i.item }} | {% if i.category %}{{ i.category }}{% else %}–{% endif %} | {% if i.quantity %}{{ i.quantity }}{% else %}–{% endif %} | {% if i.rate %}{{ i.rate }}{% else %}–{% endif %} | {{ i.amount }} |
{% endfor %}
{% endif %}

| Value | Amount ({{ pricing.currency }}) |
| --- | --- |
| Original SOW value | {{ cr.impact.original_value }} |
| Net change | {% if cr.impact.cost > 0 %}+{% endif %}{{ cr.impact.cost }} |
| Revised SOW value | {{ cr.impact.revised_value }} |

{% if cr.impact.cost < 0 %}
This Change Request reduces the value of the parent SOW.
{% endif %}
{% if cr.impact.cost == 0 %}
This Change Request does not change the value of the parent SOW.
{% endif %}

### 4.2 Schedule

{{ cr.impact.schedule }}

{% if cr.impact.milestones %}
| Milestone | Current date | Revised date |
| --- | --- | --- |
{% for m in cr.impact.milestones %}| {{ m.name }} | {{ m.current_date }} | {{ m.revised_date }} |
{% endfor %}
{% endif %}

### 4.3 Resources

{% if cr.impact.resources %}{{ cr.impact.resources }}{% else %}No change to resources.{% endif %}

### 4.4 Risks

{% if cr.impact.risks %}
{% for r in cr.impact.risks %}- {{ r }}
{% endfor %}
{% else %}
No new risks identified.
{% endif %}

## 5. Assumptions

{% if cr.assumptions %}
{% for a in cr.assumptions %}- {{ a }}
{% endfor %}
{% else %}
The assumptions of the parent SOW apply unchanged.
{% endif %}

## 6. Terms

All terms of the parent SOW and the governing agreement remain unchanged, except as expressly amended
by this Change Request. This Change Request becomes effective when both Parties sign it. It does not
update the parent SOW document itself; the parent SOW and all signed Change Requests together form the
agreed scope.

## 7. Decision

- [ ] Approved
- [ ] Approved with conditions: ______________________________
- [ ] Rejected. Reason: ______________________________

## Signatures

| For the Supplier | For the Customer |
| --- | --- |
| {{ supplier.name }} | {{ customer.name }} |
| Name: {% if supplier.signatory.name %}{{ supplier.signatory.name }}{% else %}______________{% endif %} | Name: {% if customer.signatory.name %}{{ customer.signatory.name }}{% else %}______________{% endif %} |
| Title: {% if supplier.signatory.title %}{{ supplier.signatory.title }}{% else %}______________{% endif %} | Title: {% if customer.signatory.title %}{{ customer.signatory.title }}{% else %}______________{% endif %} |
| Date: ______________ | Date: ______________ |
| Signature: ______________ | Signature: ______________ |
