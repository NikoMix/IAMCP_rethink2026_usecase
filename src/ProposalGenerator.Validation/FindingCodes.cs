namespace ProposalGenerator.Validation;

/// <summary>
/// Stable finding codes. Codes are part of the tool contract: never rename or reuse a code;
/// add a new one instead. See README.md for the rule catalogue.
/// </summary>
public static class FindingCodes
{
    /// <summary>The tool arguments are not an object or the document is missing.</summary>
    public const string ArgumentsInvalid = "ARGUMENTS_INVALID";

    /// <summary>The <c>documentType</c> argument is missing or unknown.</summary>
    public const string DocumentTypeInvalid = "DOCUMENT_TYPE_INVALID";

    /// <summary><c>document.type</c> differs from the <c>documentType</c> argument.</summary>
    public const string DocumentTypeMismatch = "DOCUMENT_TYPE_MISMATCH";

    /// <summary>A date field is not a valid <c>YYYY-MM-DD</c> calendar date.</summary>
    public const string DateInvalid = "DATE_INVALID";

    /// <summary>A numeric field is not a JSON number.</summary>
    public const string NumberInvalid = "NUMBER_INVALID";

    /// <summary>An amount, quantity, rate or weight is negative.</summary>
    public const string NegativeValue = "NEGATIVE_VALUE";

    /// <summary>The project end date is before the start date.</summary>
    public const string ProjectPeriodInvalid = "PROJECT_PERIOD_INVALID";

    /// <summary>Two dates that must be in order (for example questions before submission deadline) are not.</summary>
    public const string DeadlineOrderInvalid = "DEADLINE_ORDER_INVALID";

    /// <summary>A milestone date lies outside the project period.</summary>
    public const string MilestoneOutsidePeriod = "MILESTONE_OUTSIDE_PERIOD";

    /// <summary>A deliverable due date lies outside the project period.</summary>
    public const string DeliverableOutsidePeriod = "DELIVERABLE_OUTSIDE_PERIOD";

    /// <summary>A currency is not an ISO 4217 alphabetic code.</summary>
    public const string CurrencyInvalid = "CURRENCY_INVALID";

    /// <summary>The document uses more than one currency.</summary>
    public const string CurrencyMixed = "CURRENCY_MIXED";

    /// <summary>A rate-card line subtotal differs from daily rate times days.</summary>
    public const string RateLineSubtotalMismatch = "RATE_LINE_SUBTOTAL_MISMATCH";

    /// <summary>A time-and-materials total differs from the sum of the rate-card subtotals.</summary>
    public const string PricingTotalMismatch = "PRICING_TOTAL_MISMATCH";

    /// <summary>A fixed price differs from the effort estimate of the rate card.</summary>
    public const string FixedPriceDiffersFromEstimate = "FIXED_PRICE_DIFFERS_FROM_ESTIMATE";

    /// <summary>A capped time-and-materials total exceeds the cap.</summary>
    public const string PricingCapExceeded = "PRICING_CAP_EXCEEDED";

    /// <summary>The payment schedule does not add up to the total price.</summary>
    public const string PaymentScheduleSumMismatch = "PAYMENT_SCHEDULE_SUM_MISMATCH";

    /// <summary>Payment percentages do not add up to 100.</summary>
    public const string PaymentPercentageSumInvalid = "PAYMENT_PERCENTAGE_SUM_INVALID";

    /// <summary>A payment amount differs from its percentage of the total price.</summary>
    public const string PaymentAmountPercentageMismatch = "PAYMENT_AMOUNT_PERCENTAGE_MISMATCH";

    /// <summary>RFP evaluation weights do not add up to 100.</summary>
    public const string EvaluationWeightsSumInvalid = "EVALUATION_WEIGHTS_SUM_INVALID";

    /// <summary>Rates could not be compared with a rate card (none configured or no reference date).</summary>
    public const string RateCardUnverified = "RATE_CARD_UNVERIFIED";

    /// <summary>No rate card is valid on the document date.</summary>
    public const string RateCardNotValid = "RATE_CARD_NOT_VALID";

    /// <summary>The document currency differs from the rate card currency.</summary>
    public const string RateCardCurrencyMismatch = "RATE_CARD_CURRENCY_MISMATCH";

    /// <summary>A role is not in the rate card; the agent must ask for the rate.</summary>
    public const string RoleNotInRateCard = "ROLE_NOT_IN_RATE_CARD";

    /// <summary>A daily rate differs from the rate card.</summary>
    public const string DailyRateMismatch = "DAILY_RATE_MISMATCH";

    /// <summary>A change-request cost item amount differs from quantity times rate.</summary>
    public const string CrCostItemAmountMismatch = "CR_COST_ITEM_AMOUNT_MISMATCH";

    /// <summary>The change-request net cost differs from the sum of its cost items.</summary>
    public const string CrCostSumMismatch = "CR_COST_SUM_MISMATCH";

    /// <summary>The revised SOW value differs from original value plus net cost.</summary>
    public const string CrRevisedValueMismatch = "CR_REVISED_VALUE_MISMATCH";

    /// <summary>The original SOW value differs from the total of the referenced SOW.</summary>
    public const string CrOriginalValueMismatch = "CR_ORIGINAL_VALUE_MISMATCH";

    /// <summary>A predecessor reference differs from the matching <c>document.references</c> entry.</summary>
    public const string ReferenceMismatch = "REFERENCE_MISMATCH";

    /// <summary>A predecessor reference has no matching <c>document.references</c> entry.</summary>
    public const string ReferenceNotListed = "REFERENCE_NOT_LISTED";

    /// <summary>Predecessor documents could not be checked because no document registry is configured.</summary>
    public const string PredecessorUnverified = "PREDECESSOR_UNVERIFIED";

    /// <summary>The referenced predecessor document does not exist.</summary>
    public const string PredecessorNotFound = "PREDECESSOR_NOT_FOUND";

    /// <summary>The referenced predecessor document exists, but not in the referenced version.</summary>
    public const string PredecessorVersionNotFound = "PREDECESSOR_VERSION_NOT_FOUND";

    /// <summary>A party name differs from the predecessor document.</summary>
    public const string PartyNameMismatch = "PARTY_NAME_MISMATCH";

    /// <summary>All codes in catalogue order.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        ArgumentsInvalid, DocumentTypeInvalid, DocumentTypeMismatch,
        DateInvalid, NumberInvalid, NegativeValue,
        ProjectPeriodInvalid, DeadlineOrderInvalid, MilestoneOutsidePeriod, DeliverableOutsidePeriod,
        CurrencyInvalid, CurrencyMixed,
        RateLineSubtotalMismatch, PricingTotalMismatch, FixedPriceDiffersFromEstimate, PricingCapExceeded,
        PaymentScheduleSumMismatch, PaymentPercentageSumInvalid, PaymentAmountPercentageMismatch,
        EvaluationWeightsSumInvalid,
        RateCardUnverified, RateCardNotValid, RateCardCurrencyMismatch, RoleNotInRateCard, DailyRateMismatch,
        CrCostItemAmountMismatch, CrCostSumMismatch, CrRevisedValueMismatch, CrOriginalValueMismatch,
        ReferenceMismatch, ReferenceNotListed,
        PredecessorUnverified, PredecessorNotFound, PredecessorVersionNotFound, PartyNameMismatch,
    ];
}
