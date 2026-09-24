using ProposalGenerator.Validation.Registry;
using ProposalGenerator.Validation.Rules;
using static ProposalGenerator.Validation.FindingCodes;

namespace ProposalGenerator.Validation.Tests;

public sealed class ReferenceConsistencyRuleTests
{
    [Theory]
    [InlineData(DocumentType.Sow)]
    [InlineData(DocumentType.ChangeRequest)]
    [InlineData(DocumentType.Rfp)]
    public async Task Reference_listed_in_document_references_passes(DocumentType type)
    {
        var result = await Fixture.CheckAsync(type, _ => { });

        FindingAssert.None(result, ReferenceMismatch);
        FindingAssert.None(result, ReferenceNotListed);
    }

    [Fact]
    public async Task Reference_with_a_different_number_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("msa")["reference"] = "MSA-2026-999");

        var finding = FindingAssert.Single(result, ReferenceMismatch, FindingSeverity.Error, "$.msa.reference");
        Assert.Contains("„MSA-2026-001“", finding.Message);
    }

    [Fact]
    public async Task Reference_with_a_different_version_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.ChangeRequest, d => d.Obj("sow")["version"] = "1.1");

        FindingAssert.Single(result, ReferenceMismatch, FindingSeverity.Error, "$.sow.version");
    }

    [Fact]
    public async Task Rfi_reference_of_an_rfp_that_differs_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Rfp, d => d.Obj("rfp")["rfi_reference"] = "RFI-2026-002");

        FindingAssert.Single(result, ReferenceMismatch, FindingSeverity.Error, "$.rfp.rfi_reference");
    }

    [Fact]
    public async Task Reference_missing_from_document_references_is_a_warning()
    {
        var result = await Fixture.CheckAsync(DocumentType.ChangeRequest, d =>
        {
            var references = d.Obj("document")["references"]!.AsArray();
            references.RemoveAt(1);
        });

        FindingAssert.Single(result, ReferenceNotListed, FindingSeverity.Warning, "$.msa.reference");
        FindingAssert.None(result, ReferenceMismatch);
    }
}

public sealed class PredecessorRuleTests
{
    private static PlausibilityCheckerOptions RegistryOnly(IDocumentRegistry registry) => new() { DocumentRegistry = registry };

    [Fact]
    public async Task Existing_predecessor_passes()
    {
        var result = await Fixture.CheckAsync(DocumentType.ChangeRequest, _ => { }, RegistryOnly(Fixture.Registry()));

        FindingAssert.None(result, PredecessorUnverified);
        FindingAssert.None(result, PredecessorNotFound);
        FindingAssert.None(result, PredecessorVersionNotFound);
    }

    [Fact]
    public async Task Missing_registry_is_reported_as_unverified_per_reference()
    {
        var result = await Fixture.CheckAsync(DocumentType.ChangeRequest, _ => { }, new PlausibilityCheckerOptions());

        Assert.Equal(["$.msa.reference", "$.sow.reference"],
            result.Findings.Where(f => f.Code == PredecessorUnverified).Select(f => f.Path).Order());
        Assert.All(result.Findings, f => Assert.Equal(FindingSeverity.Info, f.Severity));
    }

    [Fact]
    public async Task Unknown_predecessor_fails()
    {
        var registry = new InMemoryDocumentRegistry().Add(DocumentType.Msa, "MSA-2026-001", "1.0", Fixture.ToElement(Fixture.Load("msa")));

        var result = await Fixture.CheckAsync(DocumentType.ChangeRequest, _ => { }, RegistryOnly(registry));

        var finding = FindingAssert.Single(result, PredecessorNotFound, FindingSeverity.Error, "$.sow.reference");
        Assert.Contains("„SOW-2026-001“", finding.Message);
    }

    [Fact]
    public async Task Predecessor_of_another_type_with_the_same_number_is_not_found()
    {
        var registry = new InMemoryDocumentRegistry().Add(DocumentType.Rfp, "MSA-2026-001", "1.0", Fixture.ToElement(Fixture.Load("msa")));

        var result = await Fixture.CheckAsync(DocumentType.Sow, _ => { }, RegistryOnly(registry));

        FindingAssert.Single(result, PredecessorNotFound, FindingSeverity.Error, "$.msa.reference");
    }

    [Fact]
    public async Task Predecessor_version_that_does_not_exist_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.ChangeRequest, d =>
        {
            d.Obj("sow")["version"] = "2.0";
            d.Obj("document").Item("references", 0)["version"] = "2.0";
        }, RegistryOnly(Fixture.Registry()));

        var finding = FindingAssert.Single(result, PredecessorVersionNotFound, FindingSeverity.Error, "$.sow.version");
        Assert.Contains("vorhanden: 1.0", finding.Message);
    }

    [Fact]
    public async Task Party_names_equal_to_the_predecessor_pass_ignoring_repeated_whitespace()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("supplier")["name"] = " Fabrikam  Consulting GmbH ", RegistryOnly(Fixture.Registry()));

        FindingAssert.None(result, PartyNameMismatch);
    }

    [Fact]
    public async Task Supplier_name_that_differs_from_the_msa_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("supplier")["name"] = "Fabrikam Consulting AG", RegistryOnly(Fixture.Registry()));

        var finding = FindingAssert.Single(result, PartyNameMismatch, FindingSeverity.Error, "$.supplier.name");
        Assert.Contains("„Fabrikam Consulting GmbH“", finding.Message);
    }

    [Fact]
    public async Task Customer_name_of_an_rfp_that_differs_from_the_rfi_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Rfp, d => d.Obj("customer")["name"] = "Contoso Handel GmbH", RegistryOnly(Fixture.Registry()));

        FindingAssert.Single(result, PartyNameMismatch, FindingSeverity.Error, "$.customer.name");
    }

    [Fact]
    public async Task Original_value_equal_to_the_sow_total_passes_and_is_computed()
    {
        var result = await Fixture.CheckAsync(DocumentType.ChangeRequest, _ => { }, RegistryOnly(Fixture.Registry()));

        FindingAssert.None(result, CrOriginalValueMismatch);
        Assert.Equal(184000m, result.Computed["$.cr.impact.original_value"]);
    }

    [Fact]
    public async Task Original_value_that_differs_from_the_sow_total_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.ChangeRequest, d =>
        {
            d.Obj("cr").Obj("impact")["original_value"] = 180000;
            d.Obj("cr").Obj("impact")["revised_value"] = 192000;
        }, RegistryOnly(Fixture.Registry()));

        var finding = FindingAssert.Single(result, CrOriginalValueMismatch, FindingSeverity.Error, "$.cr.impact.original_value");
        Assert.Contains("184.000,00", finding.Message);
    }

    [Fact]
    public async Task Reference_without_version_uses_the_latest_registered_version()
    {
        var sow = Fixture.Load("sow");
        var amended = Fixture.Load("sow");
        amended.Obj("pricing")["total"] = 196000;
        var registry = new InMemoryDocumentRegistry()
            .Add(DocumentType.Sow, "SOW-2026-001", "1.10", Fixture.ToElement(amended))
            .Add(DocumentType.Sow, "SOW-2026-001", "1.2", Fixture.ToElement(sow));

        var result = await Fixture.CheckAsync(DocumentType.ChangeRequest, d =>
        {
            d.Obj("sow").Remove("version");
            d.Obj("cr").Obj("impact")["original_value"] = 196000;
            d.Obj("cr").Obj("impact")["revised_value"] = 208000;
        }, RegistryOnly(registry));

        FindingAssert.None(result, CrOriginalValueMismatch);
    }

    [Theory]
    [InlineData("1.2", "1.10", -1)]
    [InlineData("1.0", "1", 0)]
    [InlineData("2.0", "1.9", 1)]
    [InlineData("1.0-a", "1.0-b", -1)]
    public void Versions_compare_numerically(string left, string right, int expected) =>
        Assert.Equal(expected, Math.Sign(VersionComparer.Instance.Compare(left, right)));
}
