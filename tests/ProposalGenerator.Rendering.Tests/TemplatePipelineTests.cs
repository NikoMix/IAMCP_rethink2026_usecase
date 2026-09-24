using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ProposalGenerator.Rendering.Docx;
using ProposalGenerator.Rendering.Templates;
using ProposalGenerator.Rendering.Tests.Support;

namespace ProposalGenerator.Rendering.Tests;

/// <summary>The whole pipeline — front matter, required fields, Scriban, DOCX — with test-owned templates.</summary>
public sealed class TemplatePipelineTests
{
    private const string DeliverablesTemplate =
        "## Deliverables\n" +
        "\n" +
        "| ID | Deliverable |\n" +
        "| --- | --- |\n" +
        "{% for d in sow.deliverables %}\n" +
        "| {{ d.id }} | {{ d.name }} |\n" +
        "{% endfor %}\n" +
        "\n" +
        "After the table.\n";

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task LoopInTable_RendersOneDataRowPerItem(int count)
    {
        var items = Enumerable.Range(1, count).Select(i => new { id = $"D{i}", name = $"Deliverable {i}" });
        var inspection = await RenderAsync(DeliverablesTemplate, new { sow = new { deliverables = items } });

        var table = Assert.Single(inspection.Tables);
        Assert.Equal(["ID", "Deliverable"], table.Header!.Cells);
        Assert.Equal(count, table.DataRows.Count());
        Assert.Equal(items.Select(i => new[] { i.id, i.name }), table.DataRows.Select(r => r.Cells.ToArray()));
        Assert.Equal("After the table.", inspection.Paragraphs.Last().Text);
    }

    [Fact]
    public async Task LoopInList_RendersOneListWithOneItemPerValue()
    {
        const string body = "Objectives:\n\n{% for o in project.objectives %}\n- {{ o }}\n{% endfor %}\n\nEnd.\n";
        var inspection = await RenderAsync(body, new { project = new { objectives = new[] { "Faster", "Cheaper", "Safer" } } });

        var items = inspection.Paragraphs.Where(p => p.NumberingId is not null).ToList();
        Assert.Equal(["Faster", "Cheaper", "Safer"], items.Select(p => p.Text));
        Assert.Single(items.Select(p => p.NumberingId).Distinct());
    }

    [Theory]
    [InlineData("fixed_price", "Fixed.")]
    [InlineData("time_and_materials", "Not fixed.")]
    public async Task IfElse_SelectsTheBranch(string model, string expected)
    {
        const string body = "{% if pricing.model == \"fixed_price\" %}\nFixed.\n{% else %}\nNot fixed.\n{% endif %}\n";
        var inspection = await RenderAsync(body, new { pricing = new { model } });

        Assert.Equal([expected], inspection.Paragraphs.Select(p => p.Text));
    }

    [Theory]
    [InlineData(true, true, "and:yes or:yes ne:yes")]
    [InlineData(true, false, "and:no or:yes ne:yes")]
    [InlineData(false, false, "and:no or:no ne:no")]
    public async Task Conditions_SupportAndOrAndNotEqual(bool a, bool b, string expected)
    {
        const string body =
            "and:{% if x.a and x.b %}yes{% else %}no{% endif %} " +
            "or:{% if x.a or x.b %}yes{% else %}no{% endif %} " +
            "ne:{% if x.name != \"none\" %}yes{% else %}no{% endif %}\n";
        var inspection = await RenderAsync(body, new { x = new { a, b, name = a ? "some" : "none" } });

        Assert.Equal(expected, Assert.Single(inspection.Paragraphs).Text);
    }

    [Fact]
    public async Task OptionalFieldUnderAbsentParent_IsFalseAndEmpty()
    {
        const string body = "{% if msa.reference %}\nUnder {{ msa.reference }}.\n{% else %}\nNo MSA.\n{% endif %}\n\nRef: [{{ msa.reference }}]\n";
        var inspection = await RenderAsync(body, new { });

        Assert.Equal(["No MSA.", "Ref: []"], inspection.Paragraphs.Select(p => p.Text));
    }

    [Fact]
    public async Task Values_AreWrittenAsLiteralTextAndCannotInjectMarkdown()
    {
        const string hostile = "**bold** | cell # x {{ y }} {% if %} <b>tag</b> [link](https://x.example) \\ _u_";
        const string body = "Text: {{ v }}\n\n| A | B |\n| --- | --- |\n| {{ v }} | end |\n\n# {{ v }}\n";
        var inspection = await RenderAsync(body, new { v = hostile });

        var paragraph = inspection.Paragraphs.First();
        Assert.Equal("Text: " + hostile, paragraph.Text);
        Assert.All(paragraph.Runs, r => Assert.False(r.Bold || r.Italic));

        var table = Assert.Single(inspection.Tables);
        Assert.Equal([hostile, "end"], table.DataRows.Single().Cells);
        Assert.Equal(hostile, Assert.Single(inspection.Headings(1)).Text);
    }

    [Fact]
    public async Task Values_WithLineBreaksStayOnOneLine()
    {
        var inspection = await RenderAsync("| A |\n| --- |\n| {{ v }} |\n", new { v = "first\r\nsecond\n\nthird" });

        Assert.Equal(["first second third"], Assert.Single(inspection.Tables).DataRows.Single().Cells);
    }

    [Fact]
    public async Task Values_ThatLookLikeTableDelimiters_StayText()
    {
        var inspection = await RenderAsync("Separator:\n{{ v }}\n", new { v = "| --- | --- |" });

        Assert.Equal("Separator: | --- | --- |", Assert.Single(inspection.Paragraphs).Text);
    }

    [Fact]
    public async Task Numbers_UseInvariantFormatting()
    {
        var data = SampleData.Parse("""{ "p": { "total": 184000, "rate": 1234.5, "exact": 0.1, "flag": true } }""");
        var inspection = await RenderAsync("{{ p.total }}; {{ p.rate }}; {{ p.exact }}; {{ p.flag }}\n", data);

        Assert.Equal("184000; 1234.5; 0.1; true", Assert.Single(inspection.Paragraphs).Text);
    }

    [Theory]
    [InlineData("{{ customer }}", "object", 7)]
    [InlineData("Items: {{ customer.tags }}", "list", 7)]
    public async Task WritingAnObjectOrList_FailsWithTemplatePosition(string line, string kind, int expectedLine)
    {
        var exception = await Assert.ThrowsAsync<TemplateEvaluationException>(
            () => RenderAsync($"{line}\n", new { customer = new { name = "Contoso", tags = new[] { "a" } } }));

        Assert.Equal(expectedLine, exception.Error.Line);
        Assert.Contains(kind, exception.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingRequiredFields_ListsEveryPathWithReasonAndProducesNoDocument()
    {
        var renderer = InMemoryTemplateSource.Renderer(
            "{{ customer.name }}\n",
            "document.number",
            "customer.name",
            "customer.contact.email",
            "sow.deliverables",
            "project.summary",
            "project.name",
            "pricing.total.amount",
            "supplier.name");
        var data = SampleData.Parse("""
            {
              "document": { "number": "SOW-1" },
              "customer": { "name": null },
              "sow": { "deliverables": [] },
              "project": { "summary": "   ", "name": "Portal" },
              "pricing": { "total": 5 }
            }
            """);

        var exception = await Assert.ThrowsAsync<RequiredFieldsMissingException>(
            () => renderer.RenderDocxAsync(DocumentType.Sow, data, RenderOptions.Default));

        Assert.Equal("sow", exception.TemplateId);
        Assert.Equal(
            [
                new MissingField("customer.name", MissingFieldReason.Null),
                new MissingField("customer.contact.email", MissingFieldReason.Absent),
                new MissingField("sow.deliverables", MissingFieldReason.Empty),
                new MissingField("project.summary", MissingFieldReason.Empty),
                new MissingField("pricing.total.amount", MissingFieldReason.ParentNotObject),
                new MissingField("supplier.name", MissingFieldReason.Absent),
            ],
            exception.MissingFields);
        Assert.Contains("6 field(s)", exception.Message, StringComparison.Ordinal);
        Assert.All(exception.MissingPaths, p => Assert.Contains(p, exception.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RequiredFields_ArePresent_RendersFalseAndZeroValues()
    {
        var renderer = InMemoryTemplateSource.Renderer("{{ a.zero }} {{ a.no }}\n", "a.zero", "a.no");
        var rendered = await renderer.RenderDocxAsync(DocumentType.Sow, SampleData.Parse("""{ "a": { "zero": 0, "no": false } }"""), RenderOptions.Default);

        Assert.Equal("0 false", Assert.Single(DocxInspection.Open(rendered.Content).Paragraphs).Text);
    }

    [Fact]
    public async Task SameInput_ProducesIdenticalBytes()
    {
        var data = new { sow = new { deliverables = new[] { new { id = "D1", name = "One" } } } };
        var first = await RenderDocumentAsync(DeliverablesTemplate + "\n- a\n- b\n\n1. c\n", data);
        var second = await RenderDocumentAsync(DeliverablesTemplate + "\n- a\n- b\n\n1. c\n", data);

        Assert.Equal(first.Content, second.Content);
    }

    [Fact]
    public async Task RenderedDocument_ExposesMetadataAndIsolatedContent()
    {
        var rendered = await RenderDocumentAsync("Text\n", new { });

        Assert.Equal(DocumentType.Sow, rendered.DocumentType);
        Assert.Equal("sow", rendered.TemplateId);
        Assert.Equal("9.9.9", rendered.TemplateVersion);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", rendered.ContentType);
        Assert.Equal(rendered.Length, rendered.Content.Length);

        var copy = rendered.Content;
        copy[0] ^= 0xFF;
        Assert.NotEqual(copy[0], rendered.Content[0]);

        using var stream = rendered.OpenRead();
        using var document = WordprocessingDocument.Open(stream, isEditable: false);
        Assert.Equal("Text", document.MainDocumentPart!.Document!.Body!.InnerText);
    }

    [Fact]
    public async Task DataThatIsNotAnObject_IsRejected()
    {
        var renderer = InMemoryTemplateSource.Renderer("Text\n");
        await Assert.ThrowsAsync<ArgumentException>(
            () => renderer.RenderDocxAsync(DocumentType.Sow, SampleData.Parse("[]"), RenderOptions.Default));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => renderer.RenderDocxAsync(DocumentType.Sow, SampleData.Parse("{}"), null!));
    }

    [Fact]
    public async Task CancelledToken_StopsRendering()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => InMemoryTemplateSource.Renderer("Text\n")
                .RenderDocxAsync(DocumentType.Sow, SampleData.Parse("{}"), RenderOptions.Default, cancellation.Token));
    }

    [Fact]
    public async Task TemplateSourceReturningAnotherType_IsRejected()
    {
        var source = new InMemoryTemplateSource().Add(DocumentType.Rfi, InMemoryTemplateSource.Template(DocumentType.Sow, "Text\n"));

        var exception = await Assert.ThrowsAsync<TemplateDefinitionException>(
            () => new DocxDocumentRenderer(source).RenderDocxAsync(DocumentType.Rfi, SampleData.Parse("{}"), RenderOptions.Default));
        Assert.Contains("template_id", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FileSystemTemplateSource_MissingFile_FailsExplicitly()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pg-rendering-" + Guid.NewGuid().ToString("N"));
        var renderer = new DocxDocumentRenderer(new FileSystemTemplateSource(directory));

        var exception = await Assert.ThrowsAsync<TemplateDefinitionException>(
            () => renderer.RenderDocxAsync(DocumentType.Msa, SampleData.Parse("{}"), RenderOptions.Default));
        Assert.EndsWith("msa.md", exception.Source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostProcessors_RunInOrderWithContextBeforeValidation()
    {
        var calls = new List<string>();
        var first = new DelegatePostProcessor((document, context) =>
        {
            calls.Add($"first:{context.TemplateId}:{context.TemplateVersion}:{context.Data.GetProperty("v").GetString()}");
            document.MainDocumentPart!.Document!.Body!.PrependChild(new Paragraph(new Run(new Text("Added"))));
        });
        var second = new DelegatePostProcessor((_, context) => calls.Add($"second:{context.DocumentType}"));

        var renderer = new DocxDocumentRenderer(
            new InMemoryTemplateSource().Add(DocumentType.Sow, InMemoryTemplateSource.Template(DocumentType.Sow, "Body\n")),
            postProcessors: [first, second]);
        var rendered = await renderer.RenderDocxAsync(DocumentType.Sow, SampleData.Parse("""{ "v": "x" }"""), RenderOptions.Default);

        Assert.Equal(["first:sow:9.9.9:x", "second:Sow"], calls);
        Assert.Equal(["Added", "Body"], DocxInspection.Open(rendered.Content).Paragraphs.Select(p => p.Text));
    }

    [Fact]
    public async Task InvalidPackage_FailsValidationUnlessValidationIsDisabled()
    {
        // A table without rows or grid is schema-invalid.
        var breaker = new DelegatePostProcessor((document, _) => document.MainDocumentPart!.Document!.Body!.PrependChild(new Table()));
        var renderer = new DocxDocumentRenderer(
            new InMemoryTemplateSource().Add(DocumentType.Sow, InMemoryTemplateSource.Template(DocumentType.Sow, "Body\n")),
            postProcessors: [breaker]);

        var exception = await Assert.ThrowsAsync<DocxValidationException>(
            () => renderer.RenderDocxAsync(DocumentType.Sow, SampleData.Parse("{}"), RenderOptions.Default));
        Assert.NotEmpty(exception.Errors);

        var unvalidated = await renderer.RenderDocxAsync(DocumentType.Sow, SampleData.Parse("{}"), RenderOptions.Default with { ValidateOutput = false });
        Assert.NotEmpty(DocxInspection.Open(unvalidated.Content).ValidationErrors);
    }

    [Fact]
    public async Task CustomStyleSheet_ReplacesTheDefaultStyles()
    {
        var renderer = new DocxDocumentRenderer(
            new InMemoryTemplateSource().Add(DocumentType.Sow, InMemoryTemplateSource.Template(DocumentType.Sow, "# Title\n")),
            new MarkerStyleSheet());
        var rendered = await renderer.RenderDocxAsync(DocumentType.Sow, SampleData.Parse("{}"), RenderOptions.Default);

        Assert.Contains(MarkerStyleSheet.MarkerStyleId, DocxInspection.Open(rendered.Content).DefinedStyles);
    }

    private static async Task<DocxInspection> RenderAsync(string body, object data) =>
        DocxInspection.Open((await RenderDocumentAsync(body, data)).Content);

    private static Task<RenderedDocument> RenderDocumentAsync(string body, object data)
    {
        var element = data as JsonElement? ?? JsonSerializer.SerializeToElement(data);
        return InMemoryTemplateSource.Renderer(body).RenderDocxAsync(DocumentType.Sow, element, RenderOptions.Default);
    }

    private sealed class DelegatePostProcessor(Action<WordprocessingDocument, DocxRenderContext> action) : IDocxPostProcessor
    {
        public void Process(WordprocessingDocument document, DocxRenderContext context) => action(document, context);
    }

    private sealed class MarkerStyleSheet : IDocxStyleSheet
    {
        public const string MarkerStyleId = "CorporateMarker";

        public Styles CreateStyles()
        {
            var styles = DefaultDocxStyleSheet.Instance.CreateStyles();
            styles.Append(new Style(new StyleName { Val = "Corporate marker" }) { Type = StyleValues.Paragraph, StyleId = MarkerStyleId });
            return styles;
        }
    }
}
