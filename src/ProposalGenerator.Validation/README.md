# ProposalGenerator.Validation

Deterministic plausibility checks for proposal documents (SOW, RFI, RFP, MSA and Change Request), exposed to the
agent as the function tool **`check_plausibility`** (issue #20). The agent calls the tool with the collected
document JSON before it generates a document. The tool finds inconsistent sums, dates, currencies, rates and
references, and computes the derived values, so the model does not have to do arithmetic.

The checks are pure .NET with no Azure or network access. Given the same input, rate cards and registry, they always
return the same result.

## Tool contract

| Item | Value |
| --- | --- |
| Name | `check_plausibility` (`PlausibilityTool.ToolName`) |
| Description | `PlausibilityTool.Description` (German, for the model) |
| Parameters | `PlausibilityTool.ParametersSchema`: JSON Schema 2020-12. It requires `documentType` (`sow`, `rfi`, `rfp`, `msa`, `change-request`) and `document` (an object that follows `schemas/<documentType>.schema.json`), and allows no other properties. |
| Handler | `Task<PlausibilityResult> HandleAsync(JsonElement arguments, CancellationToken cancellationToken = default)` |
| Output | `PlausibilityTool.SerializeResult(result)` |

Input:

```json
{ "documentType": "sow", "document": { "document": { "type": "sow", "number": "SOW-2026-001", "...": "..." } } }
```

Output:

```json
{
  "findings": [
    {
      "code": "PAYMENT_SCHEDULE_SUM_MISMATCH",
      "severity": "error",
      "path": "$.pricing.payment_schedule",
      "message": "Die Summe des Zahlungsplans (180.000,00) entspricht nicht dem Gesamtpreis (184.000,00); Differenz 4.000,00."
    }
  ],
  "computed": { "$.pricing.rate_card[0].subtotal": 96000, "$.pricing.rate_card[1].subtotal": 88000 }
}
```

- `findings` are sorted by severity: errors first, then warnings, then infos. Within a severity they keep rule
  order. An empty list means the document is plausible.
- `path` is a JSONPath relative to the document root (`$` is `document`). Findings about the arguments themselves
  use `arguments`, `documentType` or `document` instead.
- `message` is German and meant for the user.
- `computed` is an addition to the contract agreed with the coordinator. It maps a JSONPath to a value the tool
  derived: rate-line subtotals, the time-and-materials total, daily rates from the rate card, payment amounts from
  percentages, change-request cost item amounts, net cost, original value and revised value. Money values are
  rounded to cents, away from zero.
- Invalid arguments never throw. They produce a single `ARGUMENTS_INVALID` or `DOCUMENT_TYPE_INVALID` error.
  Arguments may also arrive as the JSON string a model emits.

### Registration (agent host)

The agent session (issues #16 and #18) registers the tool. With the Foundry SDK the registration looks like this:

```csharp
var rateCard = await RateCardLoader.LoadFileAsync("knowledge/rate-card.json");
var tool = new PlausibilityTool(new PlausibilityCheckerOptions
{
    RateCardProvider = new StaticRateCardProvider([rateCard]),
    DocumentRegistry = registry, // an IDocumentRegistry over the stored SOW/MSA/RFI documents
});

var functionTool = ResponseTool.CreateFunctionTool(
    functionName: tool.Name,
    functionParameters: BinaryData.FromString(tool.ParametersSchema.GetRawText()),
    strictModeEnabled: false,
    functionDescription: tool.Description);

// For each function call item named check_plausibility:
using var arguments = JsonDocument.Parse(functionCall.FunctionArguments);
var result = await tool.HandleAsync(arguments.RootElement, cancellationToken);
var output = PlausibilityTool.SerializeResult(result);
var outputItem = ResponseItem.CreateFunctionCallOutputItem(functionCall.CallId, output);
```

This snippet compiles against `Azure.AI.Extensions.OpenAI` 2.0.0 (OpenAI 2.9.1).

The agent must show error and warning findings to the user and must not generate a document while
`result.HasErrors` is true.

Without a `RateCardProvider` the tool reports the info `RATE_CARD_UNVERIFIED`. Without a `DocumentRegistry` it
reports the info `PREDECESSOR_UNVERIFIED`. All other checks still run.

## Rule catalogue

Codes are stable. They are never renamed or reused; new checks get new codes. `FindingCodes.All` lists all 35.
A rule only checks fields that are present. Missing required fields are the concern of the JSON Schema, not of
this tool.

| Code | Severity | Path (default) | Meaning |
| --- | --- | --- | --- |
| `ARGUMENTS_INVALID` | error | `arguments`, `document` | The arguments are not a JSON object, or `document` is missing or not an object. |
| `DOCUMENT_TYPE_INVALID` | error | `documentType` | `documentType` is missing or unknown. |
| `DOCUMENT_TYPE_MISMATCH` | error | `$.document.type` | `document.type` differs from `documentType`. |
| `DATE_INVALID` | error | the date field | A date is not a valid `YYYY-MM-DD` calendar date. |
| `NUMBER_INVALID` | error | the numeric field | An amount, rate, quantity or weight is not a JSON number. |
| `NEGATIVE_VALUE` | error | the numeric field | A total, cap, liability cap, daily rate, days, subtotal, payment amount or percentage, evaluation weight, cost-item rate, or change-request original or revised value is negative. The change-request net cost and cost-item quantities and amounts may be negative, because a change can reduce scope. |
| `PROJECT_PERIOD_INVALID` | error | `$.project.end_date` | The end date is before the start date. Equal dates are valid. |
| `DEADLINE_ORDER_INVALID` | error | the later date | Dates are out of order: the RFI/RFP questions deadline or response/submission deadline is before `document.date`, the questions deadline is after the response/submission deadline, or the change-request `cr.decision_due` is before `document.date`. Equal dates are valid. |
| `MILESTONE_OUTSIDE_PERIOD` | warning | `$.sow.milestones[i].date` | A milestone lies outside the project period (bounds inclusive). |
| `DELIVERABLE_OUTSIDE_PERIOD` | warning | `$.sow.deliverables[i].due_date` | A deliverable is due outside the project period. |
| `CURRENCY_INVALID` | error | the `currency` field | A currency is not three upper-case letters (ISO 4217 alphabetic code). |
| `CURRENCY_MIXED` | error | the deviating `currency` | The document uses more than one currency. Every property named `currency` is compared with `pricing.currency`, or with the first currency found when there is no pricing currency. |
| `RATE_LINE_SUBTOTAL_MISMATCH` | error | `$.pricing.rate_card[i].subtotal` | The subtotal is not the daily rate times the days. |
| `PRICING_TOTAL_MISMATCH` | error | `$.pricing.total` | A time-and-materials or capped total is not the sum of the subtotals. |
| `FIXED_PRICE_DIFFERS_FROM_ESTIMATE` | info | `$.pricing.total` | A fixed price differs from the rate-card estimate. This is allowed, but should be intentional. |
| `PRICING_CAP_EXCEEDED` | error | `$.pricing.total` | A capped time-and-materials total exceeds `pricing.cap`. |
| `PAYMENT_SCHEDULE_SUM_MISMATCH` | error | `$.pricing.payment_schedule` | The payment amounts do not add up to the total price. |
| `PAYMENT_PERCENTAGE_SUM_INVALID` | error | `$.pricing.payment_schedule` | The payment percentages do not add up to 100. |
| `PAYMENT_AMOUNT_PERCENTAGE_MISMATCH` | error | `$.pricing.payment_schedule[i].amount` | A payment amount is not its percentage of the total. |
| `EVALUATION_WEIGHTS_SUM_INVALID` | error | `$.rfp.evaluation_criteria` | The RFP evaluation weights do not add up to 100. |
| `RATE_CARD_UNVERIFIED` | info | the first rate field, or `$.document.date` | Rates could not be compared: no rate card is configured, or the document has no valid `document.date`. |
| `RATE_CARD_NOT_VALID` | error | `$.document.date` | No configured rate card is valid on the document date. |
| `RATE_CARD_CURRENCY_MISMATCH` | error | `$.pricing.currency` | The document currency differs from the rate-card currency. |
| `ROLE_NOT_IN_RATE_CARD` | warning (SOW line), info (CR cost item) | the `role` or `category` field | The role is not in the rate card by ID, name or alias. The agent must ask for the rate instead of inventing one. Change-request cost items may also be non-personnel costs, hence only info. |
| `DAILY_RATE_MISMATCH` | error | the `daily_rate` or `rate` field | The rate differs from the rate card valid on the document date. |
| `CR_COST_ITEM_AMOUNT_MISMATCH` | error | `$.cr.impact.cost_items[i].amount` | A cost item amount is not the quantity times the rate. |
| `CR_COST_SUM_MISMATCH` | error | `$.cr.impact.cost` | The net cost is not the sum of the cost items. |
| `CR_REVISED_VALUE_MISMATCH` | error | `$.cr.impact.revised_value` | The revised value is not the original value plus the net cost. |
| `CR_ORIGINAL_VALUE_MISMATCH` | error | `$.cr.impact.original_value` | The original value differs from the total of the referenced SOW version. |
| `REFERENCE_MISMATCH` | error | the reference number or version field | A predecessor reference (`msa.*`, `sow.*`, `rfp.rfi_reference`) differs from the `document.references` entry of the same type in number, or in version when both state one. |
| `REFERENCE_NOT_LISTED` | warning | `$.msa.reference`, `$.sow.reference`, `$.rfp.rfi_reference` | A predecessor is referenced but not listed in `document.references`. |
| `PREDECESSOR_UNVERIFIED` | info | the reference field | No document registry is configured, so the predecessor could not be looked up. |
| `PREDECESSOR_NOT_FOUND` | error | the reference field | No document with this type and number exists. A change request must reference an existing SOW. |
| `PREDECESSOR_VERSION_NOT_FOUND` | error | the version field | The document exists, but not in the referenced version. |
| `PARTY_NAME_MISMATCH` | error | `$.supplier.name`, `$.customer.name` | A party name differs from the predecessor document. Whitespace differences are ignored. |

Predecessors checked per document type: a SOW references its MSA; a change request references its SOW and MSA; an
RFP references its RFI and MSA. A reference without a version resolves to the latest version. Versions are compared
numerically, so `1.10` comes after `1.9`.

### Decisions and assumptions

- **Money** is compared after rounding to cents, away from zero (commercial rounding). Percentages and weights are
  compared at two decimals.
- **Rate-card validity** is checked on `document.date`, with inclusive bounds. If several rate cards are valid on
  that date, the one with the latest `valid_from` wins (ties by `rate_card_id`).
- **`percentage`** on payment entries is optional. The current schemas define payments by `amount` only; the
  percentage rules only apply when the field is present.
- **Severities:** an `error` blocks generation. A `warning` must be confirmed or corrected by the user. An `info` is
  a hint, or a check that could not run.

## Field paths

Default paths follow the property names of `schemas/*.schema.json`. All paths are configurable through
`PlausibilityFieldPaths`, which is useful if the schema changes before this project is updated:

```csharp
var options = new PlausibilityCheckerOptions
{
    FieldPaths = PlausibilityFieldPaths.FromJson("""{ "PricingTotal": "commercials.total" }"""),
};
```

`FromJson` accepts the property names of `PlausibilityFieldPaths`. It rejects unknown properties and malformed
paths, so a typo fails at startup instead of silently disabling a rule.

## Tests

```powershell
dotnet test tests/ProposalGenerator.Validation.Tests/ProposalGenerator.Validation.Tests.csproj
```

The test fixtures are the valid examples of the data model (`schemas/examples/valid/`, copied into
`tests/ProposalGenerator.Validation.Tests/Fixtures/`) together with `knowledge/rate-card.json`. Every rule has at
least one passing case and one failing case. Handler tests cover the full JSON round trip, and catalogue tests assert
that every code in `FindingCodes.All` is produced by a rule or the tool.
