using ProposalGenerator.Rendering.Templates;

namespace ProposalGenerator.Rendering.Tests;

/// <summary>YAML front matter parsing and validation.</summary>
public sealed class TemplateDefinitionTests
{
    [Fact]
    public void ValidFrontMatter_IsParsed()
    {
        var definition = Parse(
            "---\r\ntemplate_id: change-request\r\ntitle: Change Request\r\nversion: 0.1.0\r\nstatus: draft\r\n" +
            "related: [sow]\r\nrequired_fields:\r\n  - document.number\r\n  - cr.impact.cost\r\n---\r\n# Body\r\n",
            DocumentType.ChangeRequest);

        Assert.Equal(DocumentType.ChangeRequest, definition.DocumentType);
        Assert.Equal("change-request", definition.TemplateId);
        Assert.Equal("Change Request", definition.Title);
        Assert.Equal("0.1.0", definition.Version);
        Assert.Equal(["document.number", "cr.impact.cost"], definition.RequiredFields);
        Assert.Equal(11, definition.BodyStartLine);
        Assert.StartsWith("# Body", definition.Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("# No front matter\n", "must start with")]
    [InlineData("---\ntemplate_id: sow\nversion: 1\nrequired_fields: []\n", "no closing")]
    [InlineData("---\n---\nBody\n", "empty")]
    [InlineData("---\ntemplate_id: [sow\n---\nBody\n", "invalid")]
    [InlineData("---\ntemplate_id: rfp\nversion: 1\nrequired_fields: []\n---\nBody\n", "template_id is 'rfp', expected 'sow'")]
    [InlineData("---\ntemplate_id: sow\nrequired_fields: []\n---\nBody\n", "version is missing")]
    [InlineData("---\ntemplate_id: sow\nversion: 1\n---\nBody\n", "required_fields is missing")]
    [InlineData("---\ntemplate_id: sow\nversion: 1\nrequired_fields: [a..b, ' c', ok.path]\n---\nBody\n", "'a..b', ' c'")]
    public void InvalidFrontMatter_FailsWithTemplateDefinitionException(string content, string expectedMessage)
    {
        var exception = Assert.Throws<TemplateDefinitionException>(() => Parse(content, DocumentType.Sow));

        Assert.Equal("test.md", exception.Source);
        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DocumentType.Sow, "sow")]
    [InlineData(DocumentType.Rfi, "rfi")]
    [InlineData(DocumentType.Rfp, "rfp")]
    [InlineData(DocumentType.Msa, "msa")]
    [InlineData(DocumentType.ChangeRequest, "change-request")]
    public void TemplateIds_RoundTrip(DocumentType documentType, string templateId)
    {
        Assert.Equal(templateId, documentType.ToTemplateId());
        Assert.True(DocumentTypeExtensions.TryParseTemplateId(templateId, out var parsed));
        Assert.Equal(documentType, parsed);
    }

    [Theory]
    [InlineData("SOW")]
    [InlineData("change_request")]
    [InlineData("")]
    [InlineData(null)]
    public void UnknownTemplateIds_DoNotParse(string? templateId) =>
        Assert.False(DocumentTypeExtensions.TryParseTemplateId(templateId, out _));

    private static TemplateDefinition Parse(string content, DocumentType documentType) =>
        TemplateDefinitionParser.Parse(new TemplateSource(documentType, "test.md", content));
}
