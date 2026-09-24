---
template_id: msa
title: Master Service Agreement
version: 0.1.0
status: draft
related: [sow, change-request]
required_fields:
  - document.number
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
---

# Master Service Agreement

**Agreement No.:** {{ document.number }}  
**Effective date:** {{ document.date }}

> [GUIDANCE: Template only, not legal advice. Legal counsel must review all clauses before use.]

## Parties

1. **{{ supplier.name }}**{% if supplier.legal_form %}, a {{ supplier.legal_form }}{% endif %}, registered at {{ supplier.address }}{% if supplier.registration_number %} under number {{ supplier.registration_number }}{% endif %} ("Supplier"); and
2. **{{ customer.name }}**{% if customer.legal_form %}, a {{ customer.legal_form }}{% endif %}, registered at {{ customer.address }}{% if customer.registration_number %} under number {{ customer.registration_number }}{% endif %} ("Customer").

Supplier and Customer are each a "Party" and together the "Parties".

## Recitals

The Customer wishes to engage the Supplier to provide services from time to time, and the Parties
wish to set out the terms that govern all such services.

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

This Agreement starts on the effective date and continues for {{ legal.term_years | default(3) }} years.
It then renews automatically for successive one-year periods unless either Party gives
{{ legal.notice_period_days | default(90) }} days' written notice. Expiry does not affect SOWs in progress.

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
no procedure, a Deliverable is deemed accepted {{ legal.acceptance_days | default(10) }} business days after delivery
unless the Customer notifies material non-conformities in writing.

## 8. Intellectual property

8.1 Each Party retains ownership of its pre-existing intellectual property.  
8.2 {{ legal.ip_clause | default("Upon full payment, the Customer receives a non-exclusive, perpetual licence to use the Deliverables for its internal business purposes.") }}

## 9. Confidentiality

Each Party keeps the other Party's Confidential Information confidential, uses it only to perform this
Agreement, and protects it with at least reasonable care. This obligation survives termination for
{{ legal.confidentiality_years | default(5) }} years.

## 10. Data protection

Where the Supplier processes personal data on behalf of the Customer, the Parties conclude a data processing
agreement that complies with applicable data protection law, including Regulation (EU) 2016/679 (GDPR) where applicable.

## 11. Warranties

The Supplier warrants that the Services are performed with reasonable skill and care, in accordance with
good industry practice. All other warranties are excluded to the extent permitted by law.

## 12. Indemnification

The Supplier indemnifies the Customer against third-party claims alleging that the Deliverables infringe
that third party's intellectual property rights, subject to prompt notice and reasonable cooperation.

## 13. Limitation of liability

13.1 Each Party's aggregate liability under this Agreement is limited to {{ legal.liability_cap }}.  
13.2 Neither Party is liable for indirect or consequential damages or for loss of profit.  
13.3 These limitations do not apply to liability that cannot be limited by law, including for intent or gross negligence.

## 14. Change control

Either Party may request a change to a SOW. A change is binding only when both Parties sign a Change Request
that describes the impact on scope, schedule, and fees.

## 15. Termination

15.1 Either Party may terminate this Agreement or a SOW for material breach that is not remedied within
{{ legal.cure_period_days | default(30) }} days of written notice.  
15.2 On termination, the Customer pays for Services performed and expenses incurred up to the termination date.

## 16. Force majeure

Neither Party is liable for failure to perform due to events beyond its reasonable control.

## 17. Governing law and jurisdiction

This Agreement is governed by the laws of {{ legal.governing_law }}. The courts of {{ legal.jurisdiction }} have exclusive jurisdiction.

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
| Title: {{ supplier.signatory.title }} | Title: {{ customer.signatory.title }} |
| Date: ______________ | Date: ______________ |
| Signature: ______________ | Signature: ______________ |
