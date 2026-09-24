using System.Text.Json;

namespace ProposalGenerator.Validation;

/// <summary>
/// Field paths the rules read. Defaults follow the JSON Schemas in <c>schemas/</c> and the field catalogue in
/// <c>templates/README.md</c>. Paths are dot-separated property names relative to the document root
/// (for example <c>pricing.payment_schedule</c>); item paths are relative to an array item (for example <c>amount</c>).
/// Override single paths with an object initializer or load them from JSON with <see cref="FromJson"/>.
/// </summary>
public sealed class PlausibilityFieldPaths
{
    /// <summary>Document type inside the document (<c>document.type</c>).</summary>
    public string DocumentType { get; init; } = "document.type";

    /// <summary>Issue date of the document; also the reference date for rate-card validity.</summary>
    public string DocumentDate { get; init; } = "document.date";

    /// <summary>Array of predecessor references (<c>document.references</c>).</summary>
    public string DocumentReferences { get; init; } = "document.references";

    /// <summary>Item path of the predecessor document type in a reference entry.</summary>
    public string ReferenceType { get; init; } = "type";

    /// <summary>Item path of the predecessor document number in a reference entry.</summary>
    public string ReferenceNumber { get; init; } = "number";

    /// <summary>Item path of the predecessor document version in a reference entry.</summary>
    public string ReferenceVersion { get; init; } = "version";

    /// <summary>Item path of the predecessor document date in a reference entry.</summary>
    public string ReferenceDate { get; init; } = "date";

    /// <summary>Supplier name.</summary>
    public string SupplierName { get; init; } = "supplier.name";

    /// <summary>Customer name.</summary>
    public string CustomerName { get; init; } = "customer.name";

    /// <summary>Project start date.</summary>
    public string ProjectStartDate { get; init; } = "project.start_date";

    /// <summary>Project end date.</summary>
    public string ProjectEndDate { get; init; } = "project.end_date";

    /// <summary>Number of the governing MSA (SOW, change request, RFP).</summary>
    public string MsaReference { get; init; } = "msa.reference";

    /// <summary>Version of the governing MSA.</summary>
    public string MsaVersion { get; init; } = "msa.version";

    /// <summary>Date of the governing MSA.</summary>
    public string MsaDate { get; init; } = "msa.date";

    /// <summary>Number of the SOW amended by a change request.</summary>
    public string SowReference { get; init; } = "sow.reference";

    /// <summary>Version of the SOW amended by a change request.</summary>
    public string SowVersion { get; init; } = "sow.version";

    /// <summary>Date of the SOW amended by a change request.</summary>
    public string SowDate { get; init; } = "sow.date";

    /// <summary>Number of the RFI that precedes an RFP.</summary>
    public string RfpRfiReference { get; init; } = "rfp.rfi_reference";

    /// <summary>Pricing model (<c>fixed_price</c>, <c>time_and_materials</c>, <c>capped_tm</c>).</summary>
    public string PricingModel { get; init; } = "pricing.model";

    /// <summary>Document currency (ISO 4217).</summary>
    public string PricingCurrency { get; init; } = "pricing.currency";

    /// <summary>Total price.</summary>
    public string PricingTotal { get; init; } = "pricing.total";

    /// <summary>Cap of a capped time-and-materials engagement.</summary>
    public string PricingCap { get; init; } = "pricing.cap";

    /// <summary>Array of rate-card lines in the document.</summary>
    public string RateCardLines { get; init; } = "pricing.rate_card";

    /// <summary>Item path of the role of a rate-card line.</summary>
    public string RateCardLineRole { get; init; } = "role";

    /// <summary>Item path of the daily rate of a rate-card line.</summary>
    public string RateCardLineDailyRate { get; init; } = "daily_rate";

    /// <summary>Item path of the estimated days of a rate-card line.</summary>
    public string RateCardLineDays { get; init; } = "days";

    /// <summary>Item path of the subtotal of a rate-card line.</summary>
    public string RateCardLineSubtotal { get; init; } = "subtotal";

    /// <summary>Array of payment plan entries.</summary>
    public string PaymentSchedule { get; init; } = "pricing.payment_schedule";

    /// <summary>Item path of the amount of a payment plan entry.</summary>
    public string PaymentAmount { get; init; } = "amount";

    /// <summary>Item path of the optional percentage of a payment plan entry.</summary>
    public string PaymentPercentage { get; init; } = "percentage";

    /// <summary>Item path of the planned date of a payment plan entry.</summary>
    public string PaymentDate { get; init; } = "date";

    /// <summary>Array of SOW milestones.</summary>
    public string Milestones { get; init; } = "sow.milestones";

    /// <summary>Item path of the date of a milestone.</summary>
    public string MilestoneDate { get; init; } = "date";

    /// <summary>Array of SOW deliverables.</summary>
    public string Deliverables { get; init; } = "sow.deliverables";

    /// <summary>Item path of the due date of a deliverable.</summary>
    public string DeliverableDueDate { get; init; } = "due_date";

    /// <summary>Array of RFP deliverables.</summary>
    public string RfpDeliverables { get; init; } = "rfp.deliverables";

    /// <summary>Array of RFP evaluation criteria.</summary>
    public string EvaluationCriteria { get; init; } = "rfp.evaluation_criteria";

    /// <summary>Item path of the weight (percent) of an evaluation criterion.</summary>
    public string EvaluationWeight { get; init; } = "weight";

    /// <summary>RFP proposal submission deadline.</summary>
    public string RfpSubmissionDeadline { get; init; } = "rfp.submission_deadline";

    /// <summary>RFP clarification questions deadline.</summary>
    public string RfpQuestionsDeadline { get; init; } = "rfp.questions_deadline";

    /// <summary>Array of RFP timeline steps.</summary>
    public string RfpTimeline { get; init; } = "rfp.timeline";

    /// <summary>RFI response deadline.</summary>
    public string RfiResponseDeadline { get; init; } = "rfi.response_deadline";

    /// <summary>RFI clarification questions deadline.</summary>
    public string RfiQuestionsDeadline { get; init; } = "rfi.questions_deadline";

    /// <summary>Array of RFI timeline steps.</summary>
    public string RfiTimeline { get; init; } = "rfi.timeline";

    /// <summary>Item path of the date of an RFI or RFP timeline step.</summary>
    public string TimelineDate { get; init; } = "date";

    /// <summary>Requested decision date of a change request.</summary>
    public string CrDecisionDue { get; init; } = "cr.decision_due";

    /// <summary>Array of change-request cost items.</summary>
    public string CrCostItems { get; init; } = "cr.impact.cost_items";

    /// <summary>Item path of the role or category of a cost item.</summary>
    public string CrCostItemCategory { get; init; } = "category";

    /// <summary>Item path of the quantity (for example days) of a cost item.</summary>
    public string CrCostItemQuantity { get; init; } = "quantity";

    /// <summary>Item path of the rate of a cost item.</summary>
    public string CrCostItemRate { get; init; } = "rate";

    /// <summary>Item path of the amount of a cost item.</summary>
    public string CrCostItemAmount { get; init; } = "amount";

    /// <summary>Net change of the SOW value; may be negative.</summary>
    public string CrNetCost { get; init; } = "cr.impact.cost";

    /// <summary>SOW value before the change.</summary>
    public string CrOriginalValue { get; init; } = "cr.impact.original_value";

    /// <summary>SOW value after the change.</summary>
    public string CrRevisedValue { get; init; } = "cr.impact.revised_value";

    /// <summary>Array of milestones whose dates the change request shifts.</summary>
    public string CrImpactMilestones { get; init; } = "cr.impact.milestones";

    /// <summary>Item path of the current date of a shifted milestone.</summary>
    public string CrMilestoneCurrentDate { get; init; } = "current_date";

    /// <summary>Item path of the revised date of a shifted milestone.</summary>
    public string CrMilestoneRevisedDate { get; init; } = "revised_date";

    /// <summary>Amount of the MSA liability cap (a money object).</summary>
    public string LiabilityCapAmount { get; init; } = "legal.liability_cap.amount";

    /// <summary>Item path of the display name of array items such as milestones and deliverables; used in messages.</summary>
    public string ItemName { get; init; } = "name";

    /// <summary>Name of properties that hold a currency code anywhere in the document.</summary>
    public string CurrencyPropertyName { get; init; } = "currency";

    /// <summary>Pricing model value for a fixed price.</summary>
    public string FixedPriceModel { get; init; } = "fixed_price";

    /// <summary>Pricing model value for time and materials.</summary>
    public string TimeAndMaterialsModel { get; init; } = "time_and_materials";

    /// <summary>Pricing model value for capped time and materials.</summary>
    public string CappedTimeAndMaterialsModel { get; init; } = "capped_tm";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>
    /// Reads paths from a JSON object whose property names match this class, for example
    /// <c>{ "PricingTotal": "commercials.total" }</c>. Omitted properties keep their defaults; unknown ones are rejected.
    /// </summary>
    /// <exception cref="JsonException">The JSON is invalid or contains an unknown property.</exception>
    /// <exception cref="ArgumentException">A path is empty or malformed.</exception>
    public static PlausibilityFieldPaths FromJson(string json)
    {
        var paths = JsonSerializer.Deserialize<PlausibilityFieldPaths>(json, JsonOptions)
            ?? throw new JsonException("The field path configuration must be a JSON object.");
        paths.Validate();
        return paths;
    }

    /// <summary>Checks that every path is a non-empty, dot-separated list of non-empty property names.</summary>
    /// <exception cref="ArgumentException">A path is empty or malformed.</exception>
    public void Validate()
    {
        foreach (var property in typeof(PlausibilityFieldPaths).GetProperties())
        {
            var value = (string?)property.GetValue(this);
            if (string.IsNullOrWhiteSpace(value) || value.Split('.').Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException($"Field path '{property.Name}' must be a dot-separated list of property names, but was '{value}'.");
            }
        }
    }
}
