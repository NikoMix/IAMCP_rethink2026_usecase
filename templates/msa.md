---
template_id: msa
title: Master Service Agreement
version: 0.2.0
status: draft
related: [sow, change-request]
required_fields:
  - document.number
  - document.version
  - document.date
  - supplier.name
  - supplier.address
  - supplier.signatory.name
  - customer.name
  - customer.address
  - customer.signatory.name
  - legal.governing_law
  - legal.jurisdiction
  - legal.payment_terms_days
  - legal.liability_cap
optional_fields:
  - supplier.legal_form
  - supplier.registration_number
  - supplier.signatory.title
  - customer.legal_form
  - customer.registration_number
  - customer.signatory.title
  - legal.term_years
  - legal.notice_period_days
  - legal.acceptance_days
  - legal.ip_model
  - legal.confidentiality_years
  - legal.dpa_reference
  - legal.cure_period_days
clauses:
  - { number: 1, title: Definitions }
  - { number: 2, title: Scope and Statements of Work }
  - { number: 3, title: Term }
  - { number: 4, title: Fees and payment }
  - { number: 5, title: Personnel }
  - { number: 6, title: Customer responsibilities }
  - { number: 7, title: Acceptance }
  - { number: 8, title: Intellectual property }
  - { number: 9, title: Confidentiality }
  - { number: 10, title: Data protection }
  - { number: 11, title: Warranties }
  - { number: 12, title: Indemnification }
  - { number: 13, title: Limitation of liability }
  - { number: 14, title: Change control }
  - { number: 15, title: Termination }
  - { number: 16, title: Force majeure }
  - { number: 17, title: Governing law and jurisdiction }
  - { number: 18, title: General }
---

# Master Service Agreement

**Agreement No.:** {{ document.number }}

**Version:** {{ document.version }}

**Effective date:** {{ document.date }}

> **Notice:** This document was generated from a template. It is not legal advice. Qualified legal
> counsel must review it before it is signed or sent to a customer or supplier.

[GUIDANCE: Legal counsel has not yet reviewed this template (see backlog issue 44). Keep this marker until the review is complete. A document that still contains a GUIDANCE marker must not be marked as final.]

## Parties

- **{{ supplier.name }}**{% if supplier.legal_form %} ({{ supplier.legal_form }}){% endif %}, registered at {{ supplier.address }}{% if supplier.registration_number %} under number {{ supplier.registration_number }}{% endif %} ("Supplier"); and
- **{{ customer.name }}**{% if customer.legal_form %} ({{ customer.legal_form }}){% endif %}, registered at {{ customer.address }}{% if customer.registration_number %} under number {{ customer.registration_number }}{% endif %} ("Customer").

Supplier and Customer are each a "Party" and together the "Parties".

## Recitals

The Customer wishes to engage the Supplier to provide services from time to time, and the Parties
wish to set out the terms that govern all such services.

## Referencing this Agreement

Statements of Work and Change Requests identify this Agreement by its Agreement No. and version,
"Master Service Agreement {{ document.number }}, version {{ document.version }}", and cite its clauses by
section number, for example "section 13 of Master Service Agreement {{ document.number }}". The section
numbers 1 to 18 are fixed and are not renumbered in later versions of this Agreement.

## 1. Definitions

- **Agreement:** this Master Service Agreement, including all Statements of Work and Change Requests.
- **Statement of Work (SOW):** a document executed under this Agreement that describes specific Services.
- **Change Request:** a document executed under section 14 that amends a SOW.
- **Deliverables:** work products provided to the Customer under a SOW.
- **Confidential Information:** any non-public information disclosed by one Party to the other.

## 2. Scope and Statements of Work

2.1 The Supplier provides the services described in each SOW ("Services").

2.2 Each SOW is governed by this Agreement. If a SOW conflicts with this Agreement, this Agreement prevails unless the SOW expressly states otherwise.

2.3 Neither Party is obliged to enter into any SOW.

## 3. Term

3.1 This Agreement starts on the effective date and continues for an initial term of {% if legal.term_years %}{{ legal.term_years }}{% else %}3{% endif %} years.

3.2 It then renews automatically for successive one-year periods unless either Party gives
{% if legal.notice_period_days %}{{ legal.notice_period_days }}{% else %}90{% endif %} days' written notice before the end of the current term.

3.3 Expiry or termination of this Agreement does not affect SOWs in progress, which remain governed by this Agreement until they are completed or terminated.

## 4. Fees and payment

4.1 Fees are specified in each SOW.

4.2 The Supplier invoices as set out in the SOW. Invoices are payable within {{ legal.payment_terms_days }} days of receipt.

4.3 Fees exclude value-added tax and other applicable taxes, which are charged at the statutory rate.

4.4 Pre-approved reasonable expenses are reimbursed at cost.

## 5. Personnel

5.1 The Supplier assigns suitably qualified personnel and remains responsible for any subcontractors.

5.2 The Supplier's personnel comply with the Customer's reasonable site and security policies that have been communicated in writing.

## 6. Customer responsibilities

The Customer provides timely access, information, decisions, and resources as specified in each SOW.
The Supplier is not responsible for delays caused by the Customer's failure to do so.

## 7. Acceptance

Deliverables are accepted according to the acceptance procedure in the relevant SOW. If a SOW contains
no procedure, a Deliverable is deemed accepted {% if legal.acceptance_days %}{{ legal.acceptance_days }}{% else %}10{% endif %} business days after delivery
unless the Customer notifies material non-conformities in writing.

## 8. Intellectual property

8.1 Each Party retains ownership of its pre-existing intellectual property.

{% if legal.ip_model == "assignment" %}8.2 Upon full payment, the Supplier assigns to the Customer all rights in the Deliverables created specifically for the Customer, excluding the Supplier's pre-existing intellectual property, for which the Customer receives a non-exclusive, perpetual licence to the extent needed to use the Deliverables.{% else %}8.2 Upon full payment, the Customer receives a non-exclusive, perpetual licence to use the Deliverables for its internal business purposes.{% endif %}

## 9. Confidentiality

Each Party keeps the other Party's Confidential Information confidential, uses it only to perform this
Agreement, and protects it with at least reasonable care. This obligation survives termination for
{% if legal.confidentiality_years %}{{ legal.confidentiality_years }}{% else %}5{% endif %} years.

## 10. Data protection

10.1 Each Party complies with applicable data protection law, including Regulation (EU) 2016/679 (GDPR) where applicable.

{% if legal.dpa_reference %}10.2 Where the Supplier processes personal data on behalf of the Customer, the data processing agreement {{ legal.dpa_reference }} under Article 28 GDPR applies and prevails over this Agreement in matters of data protection.{% else %}10.2 Where the Supplier processes personal data on behalf of the Customer, the Parties conclude a data processing agreement under Article 28 GDPR before the processing starts. That agreement prevails over this Agreement in matters of data protection.{% endif %}

## 11. Warranties

The Supplier warrants that the Services are performed with reasonable skill and care, in accordance with
good industry practice. All other warranties are excluded to the extent permitted by law.

## 12. Indemnification

The Supplier indemnifies the Customer against third-party claims alleging that the Deliverables infringe
that third party's intellectual property rights, subject to prompt notice and reasonable cooperation.

## 13. Limitation of liability

13.1 Each Party's aggregate liability under this Agreement is limited to {{ legal.liability_cap }}.

13.2 Neither Party is liable for indirect or consequential damages or for loss of profit.

13.3 These limitations do not apply to liability that cannot be limited by law, including liability for intent or gross negligence, for injury to life, body, or health, and under mandatory product liability law.

## 14. Change control

Either Party may request a change to a SOW. A change is binding only when both Parties sign a Change Request
that references this Agreement and the SOW and describes the impact on scope, schedule, and fees.

## 15. Termination

15.1 Either Party may terminate this Agreement or a SOW for material breach that is not remedied within
{% if legal.cure_period_days %}{{ legal.cure_period_days }}{% else %}30{% endif %} days of written notice.

15.2 The right of either Party to terminate for good cause remains unaffected.

15.3 On termination, the Customer pays for Services performed and expenses incurred up to the termination date.

## 16. Force majeure

Neither Party is liable for failure to perform due to events beyond its reasonable control.

## 17. Governing law and jurisdiction

This Agreement is governed by the laws of {{ legal.governing_law }}. The courts of {{ legal.jurisdiction }} have exclusive jurisdiction.

[GUIDANCE: Confirm the governing law and place of jurisdiction with legal counsel. The working assumption is German law; the jurisdiction is usually the Supplier's registered seat.]

## 18. General

18.1 This Agreement is the entire agreement between the Parties regarding its subject matter.

18.2 Amendments must be in writing and signed by both Parties.

18.3 If any provision is invalid, the remaining provisions remain in effect.

18.4 Neither Party may assign this Agreement without the other Party's prior written consent, which may not be unreasonably withheld.

## Signatures

| For the Supplier | For the Customer |
| --- | --- |
| {{ supplier.name }} | {{ customer.name }} |
| Name: {{ supplier.signatory.name }} | Name: {{ customer.signatory.name }} |
| Title: {% if supplier.signatory.title %}{{ supplier.signatory.title }}{% else %}______________{% endif %} | Title: {% if customer.signatory.title %}{{ customer.signatory.title }}{% else %}______________{% endif %} |
| Date: ______________ | Date: ______________ |
| Signature: ______________ | Signature: ______________ |
