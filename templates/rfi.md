---
template_id: rfi
title: Request for Information / Expression of Interest
version: 0.2.0
status: in_review
related: [rfp]
# Field paths follow the shared schema (owned by #7) and its parity tests.
# A path through a list addresses each item, for example rfi.questions.text.
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
  - rfi.response_deadline
  - rfi.questions
  - rfi.questions.text
optional_fields:
  - document.confidentiality
  - customer.profile
  - customer.contact.phone
  - project.background
  - rfi.purposes
  - rfi.areas_of_interest
  - rfi.areas_of_interest.title
  - rfi.areas_of_interest.description
  - rfi.region
  - rfi.questions.guidance
  - rfi.questions_deadline
  - rfi.response_format
  - rfi.timeline
  - rfi.timeline.name
  - rfi.timeline.date
---

# Request for Information / Expression of Interest

**Reference:** {{ document.number }}  
**Issued by:** {{ customer.name }}  
**Issue date:** {{ document.date }}  
**Response deadline:** {{ rfi.response_deadline }}  
**Confidentiality:** {% if document.confidentiality %}{{ document.confidentiality }}{% else %}Confidential{% endif %}

> **Non-binding request:** This RFI is for information and market research only. It is not a
> request for a binding offer, and it does not commit the Issuer to issue a Request for Proposal
> (RFP) or to award a contract.

## 1. Introduction

{{ customer.name }} ("the Issuer") invites qualified organisations to express their interest
in, and share information about, **{{ project.name }}**.

## 2. About the Issuer

{% if customer.profile %}{{ customer.profile }}{% else %}{{ customer.name }} shares further details about its organisation with respondents on request.{% endif %}

## 3. Purpose of this RFI

{{ project.summary }}{% if rfi.purposes %}

The Issuer intends to use the responses to:
{% for purpose in rfi.purposes %}
- {{ purpose }}{% endfor %}{% endif %}

## 4. Background and current situation

{% if project.background %}{{ project.background }}{% else %}No further background is provided at this stage.{% endif %}

## 5. Areas of interest

{% if rfi.areas_of_interest %}The Issuer is particularly interested in the following areas:
{% for area in rfi.areas_of_interest %}
- **{{ area.title }}:** {{ area.description }}{% endfor %}{% else %}The Issuer welcomes information on any capability that is relevant to the purpose in section 3.{% endif %}

## 6. Information requested

Please structure your response using the sections below.

### 6.1 Company profile

- Legal name, headquarters, and year of incorporation
- Number of employees and relevant certifications (for example ISO 27001, Microsoft partner designations)
- Local presence in {% if rfi.region %}{{ rfi.region }}{% else %}the Issuer's region{% endif %}

### 6.2 Capabilities and experience

- Relevant products, services, or solutions
- Up to three reference projects of similar size and complexity, including customer permission to be contacted

### 6.3 Specific questions

Please answer each question and refer to it by its number.
{% for q in rfi.questions %}
1. {{ q.text }}{% if q.guidance %}  
   *Guidance:* {{ q.guidance }}{% endif %}{% endfor %}

### 6.4 Indicative approach and pricing (optional)

An outline of your proposed approach and a non-binding indicative price range or pricing model.
Any figures you provide are for budgeting only and do not constitute an offer.

## 7. Response format and submission

- Format: {% if rfi.response_format %}{{ rfi.response_format }}{% else %}PDF, maximum 10 pages excluding appendices{% endif %}
- Language: {% if document.language == "de" %}German{% else %}English{% endif %}
- Structure: sections 6.1 to 6.4 of this RFI, in that order
- Submission: by email to {{ customer.contact.email }} no later than **{{ rfi.response_deadline }}**

## 8. Indicative timeline

| Milestone | Date |
| --- | --- |
| RFI issued | {{ document.date }} |{% if rfi.questions_deadline %}
| Clarification questions due | {{ rfi.questions_deadline }} |{% endif %}
| Responses due | {{ rfi.response_deadline }} |{% if rfi.timeline %}{% for m in rfi.timeline %}
| {{ m.name }} | {{ m.date }} |{% endfor %}{% endif %}

Dates after the response deadline are indicative and may change.

## 9. Questions and clarifications

Send clarification questions to {{ customer.contact.name }} ({{ customer.contact.email }}){% if rfi.questions_deadline %} by
{{ rfi.questions_deadline }}{% endif %}. The Issuer may share anonymised questions and answers with all respondents.

## 10. Terms of this RFI

- This RFI does not request a binding offer, and a response does not constitute one.
- Respondents bear all costs of preparing their response.
- The Issuer may amend or cancel this RFI at any time.
- Information that respondents provide is treated as confidential and is used solely for market research
  and for preparing any subsequent RFP.
- Responses do not create any contractual relationship.

**Contact:** {{ customer.contact.name }}, {{ customer.contact.email }}{% if customer.contact.phone %}, {{ customer.contact.phone }}{% endif %}
