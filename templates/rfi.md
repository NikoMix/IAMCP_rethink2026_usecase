---
template_id: rfi
title: Request for Information / Expression of Interest
version: 0.1.0
status: draft
related: [rfp]
required_fields:
  - document.number
  - document.date
  - customer.name
  - customer.contact.name
  - customer.contact.email
  - project.name
  - project.summary
  - rfi.response_deadline
  - rfi.questions
---

# Request for Information / Expression of Interest

**Reference:** {{ document.number }}  
**Issued by:** {{ customer.name }}  
**Issue date:** {{ document.date }}  
**Response deadline:** {{ rfi.response_deadline }}  
**Confidentiality:** {{ document.confidentiality | default("Confidential") }}

## 1. Introduction

{{ customer.name }} ("the Issuer") invites qualified organisations to express their interest
in, and share information about, **{{ project.name }}**.

This RFI is for information and market research only. It is not a request for a binding offer,
and it does not commit the Issuer to issue a Request for Proposal (RFP) or to award a contract.

## 2. About the Issuer

{{ customer.profile }}

[GUIDANCE: Two to four sentences about the organisation, its industry, and its size.]

## 3. Purpose of this RFI

{{ project.summary }}

The Issuer intends to use the responses to:

{% for purpose in rfi.purposes %}
- {{ purpose }}
{% endfor %}

## 4. Background and current situation

{{ project.background }}

## 5. Areas of interest

{% for area in rfi.areas_of_interest %}
- **{{ area.title }}:** {{ area.description }}
{% endfor %}

## 6. Information requested

Please structure your response using the sections below.

### 6.1 Company profile

- Legal name, headquarters, and year of incorporation
- Number of employees and relevant certifications (for example ISO 27001, Microsoft partner designations)
- Local presence in {{ rfi.region | default("the Issuer's region") }}

### 6.2 Capabilities and experience

- Relevant products, services, or solutions
- Up to three reference projects of similar size and complexity, including customer permission to be contacted

### 6.3 Specific questions

| # | Question | Guidance |
| --- | --- | --- |
{% for q in rfi.questions %}
| {{ loop.index }} | {{ q.text }} | {{ q.guidance | default("") }} |
{% endfor %}

### 6.4 Indicative approach and pricing (optional)

An outline of your proposed approach and a non-binding indicative price range or pricing model.

## 7. Response format and submission

- Format: {{ rfi.response_format | default("PDF, maximum 10 pages excluding appendices") }}
- Language: {{ document.language | default("English") }}
- Submit by email to {{ customer.contact.email }} no later than **{{ rfi.response_deadline }}**.

## 8. Indicative timeline

| Milestone | Date |
| --- | --- |
{% for m in rfi.timeline %}
| {{ m.name }} | {{ m.date }} |
{% endfor %}

## 9. Questions and clarifications

Send clarification questions to {{ customer.contact.name }} ({{ customer.contact.email }}) by
{{ rfi.questions_deadline | default("[date]") }}. The Issuer may share anonymised questions and answers with all respondents.

## 10. Terms of this RFI

- Respondents bear all costs of preparing their response.
- The Issuer may amend or cancel this RFI at any time.
- Information that respondents provide is treated as confidential and is used solely for evaluation.
- Responses do not create any contractual relationship.

**Contact:** {{ customer.contact.name }}, {{ customer.contact.email }}{% if customer.contact.phone %}, {{ customer.contact.phone }}{% endif %}
