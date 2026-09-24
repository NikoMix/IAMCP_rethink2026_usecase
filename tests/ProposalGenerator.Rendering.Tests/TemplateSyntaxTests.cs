using ProposalGenerator.Rendering.Tests.Support;

namespace ProposalGenerator.Rendering.Tests;

/// <summary>The template syntax subset: what parses, what is rejected, and where errors point.</summary>
public sealed class TemplateSyntaxTests
{
    /// <summary>Template body line 1 is file line 7 in <see cref="InMemoryTemplateSource.Template"/>.</summary>
    private const int FirstBodyLine = 7;

    [Theory]
    [InlineData("{% if a %}x{% elsif b %}y{% endif %}", "elsif")]
    [InlineData("{{ a | default(\"x\") }}", "Filters")]
    [InlineData("{{ a | default: \"x\" }}", "Filters")]
    [InlineData("{{ a | upcase }}", "Filters")]
    [InlineData("{% for x in list %}{{ forloop.index }}{% endfor %}", "Loop variable 'forloop'")]
    [InlineData("{% for x in list %}{{ loop.index }}{% endfor %}", "Loop variable 'loop'")]
    [InlineData("{% for x in list limit:2 %}{{ x }}{% endfor %}", "not supported")]
    [InlineData("{% assign x = 1 %}", "not supported")]
    [InlineData("{% unless a %}x{% endunless %}", "not supported")]
    [InlineData("{% case a %}{% when 1 %}x{% endcase %}", "not supported")]
    [InlineData("{% capture x %}y{% endcapture %}", "not supported")]
    [InlineData("{% if not a %}x{% endif %}", "not supported")]
    [InlineData("{% if a > 1 %}x{% endif %}", "Operator")]
    [InlineData("{% if a contains \"x\" %}x{% endif %}", "not supported")]
    [InlineData("{{ a[0] }}", "not supported")]
    public async Task UnsupportedConstructs_AreRejectedBeforeRendering(string body, string expectedMessage)
    {
        var exception = await RenderExpectingSyntaxErrorAsync(body + "\n");

        var error = Assert.Single(exception.Errors);
        Assert.Equal(FirstBodyLine, error.Line);
        Assert.Contains(expectedMessage, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryViolation_IsReportedWithItsFileLineAndColumn()
    {
        const string body =
            "# Title\n" +
            "{% for d in list %}\n" +
            "| {{ loop.index }} | {{ d.name | default(\"x\") }} |\n" +
            "{% endfor %}\n" +
            "\n" +
            "  {{ b | upcase }}\n";

        var exception = await RenderExpectingSyntaxErrorAsync(body);

        Assert.Equal(
            [(FirstBodyLine + 2, 6), (FirstBodyLine + 2, 25), (FirstBodyLine + 5, 6)],
            exception.Errors.Select(e => (e.Line, e.Column)));
        Assert.Contains("3 syntax error(s)", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{% if a %}\nText\n", FirstBodyLine)]
    [InlineData("Line\n\nText {{ a. }}\n", FirstBodyLine + 2)]
    [InlineData("{% endfor %}\n", FirstBodyLine)]
    public async Task ParseErrors_PointAtTheTemplateFileLine(string body, int expectedLine)
    {
        var exception = await RenderExpectingSyntaxErrorAsync(body);

        Assert.Contains(exception.Errors, e => e.Line == expectedLine);
        Assert.Equal("sow", exception.TemplateId);
    }

    [Fact]
    public async Task SupportedSubset_Renders()
    {
        const string body =
            "{% if a.flag == true and a.name != \"x\" or a.n == 3 %}\n" +
            "Yes {{ a.name }}.\n" +
            "{% else %}\n" +
            "{% if a.other %}Other{% else %}No{% endif %}\n" +
            "{% endif %}\n" +
            "{% for item in a.list %}\n" +
            "- {{ item.value }}\n" +
            "{% endfor %}\n";

        var rendered = await InMemoryTemplateSource.Renderer(body).RenderDocxAsync(
            DocumentType.Sow,
            SampleData.Parse("""{ "a": { "flag": true, "name": "Ada", "n": 1, "list": [{ "value": "v1" }, { "value": "v2" }] } }"""),
            RenderOptions.Default);

        Assert.Equal(["Yes Ada.", "v1", "v2"], DocxInspection.Open(rendered.Content).Paragraphs.Select(p => p.Text));
    }

    [Fact]
    public async Task StandaloneTagLines_LeaveNoBlankLines_ButInlineTagsStay()
    {
        const string body =
            "| H |\n" +
            "| --- |\n" +
            "  {% for x in list %}\n" +
            "| {{ x }} |\n" +
            "  {% endfor %}\n" +
            "\n" +
            "Tail {% if flag %}on{% endif %} end\n";

        var rendered = await InMemoryTemplateSource.Renderer(body).RenderDocxAsync(
            DocumentType.Sow,
            SampleData.Parse("""{ "list": ["a", "b"], "flag": true }"""),
            RenderOptions.Default);

        var inspection = DocxInspection.Open(rendered.Content);
        var table = Assert.Single(inspection.Tables);
        Assert.Equal([["a"], ["b"]], table.DataRows.Select(r => r.Cells));
        Assert.Equal(["Tail on end"], inspection.Paragraphs.Select(p => p.Text));
    }

    private static Task<TemplateSyntaxException> RenderExpectingSyntaxErrorAsync(string body) =>
        Assert.ThrowsAsync<TemplateSyntaxException>(
            () => InMemoryTemplateSource.Renderer(body).RenderDocxAsync(DocumentType.Sow, SampleData.Parse("{}"), RenderOptions.Default));
}
