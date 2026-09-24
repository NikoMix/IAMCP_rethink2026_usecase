using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ProposalGenerator.Rendering.Templates;
using ProposalGenerator.Rendering.Tests.Support;
using Xunit.Abstractions;

namespace ProposalGenerator.Rendering.Tests;

/// <summary>Renders every repository template in <c>templates/</c> with its fictional sample data.</summary>
public sealed partial class RepositoryTemplateTests(ITestOutputHelper output)
{
    /// <summary>
    /// Templates on the base branch that still use syntax outside the supported subset
    /// (<c>| default(...)</c>, <c>| join</c>, <c>loop.index</c>). For these, the rendering tests
    /// assert the explicit <see cref="TemplateSyntaxException"/> instead of a document. When a
    /// template is fixed, its tests fail with a request to remove it here, so the full checks
    /// start running for it. With <see cref="TestPaths.TemplatesOverrideVariable"/> set, every
    /// template must conform.
    /// </summary>
    private static readonly HashSet<DocumentType> AwaitingSubsetConformance =
    [
        DocumentType.Sow,
        DocumentType.Rfi,
        DocumentType.Rfp,
        DocumentType.Msa,
        DocumentType.ChangeRequest,
    ];

    /// <summary>One value per template that the sample data puts into a table data cell.</summary>
    private static readonly Dictionary<DocumentType, string> SampleTableCell = new()
    {
        [DocumentType.Sow] = "Target architecture",
        [DocumentType.Rfi] = "Welche vergleichbaren Projekte haben Sie umgesetzt?",
        [DocumentType.Rfp] = "Solution quality",
        [DocumentType.Msa] = "Fabrikam Consulting GmbH",
        [DocumentType.ChangeRequest] = "Returns page",
    };

    public static TheoryData<DocumentType> AllDocumentTypes => new(DocumentTypeExtensions.All);

    [Fact]
    public void AllDocumentTypes_CoverFiveTemplates()
    {
        Assert.Equal(5, DocumentTypeExtensions.All.Count);
        Assert.All(DocumentTypeExtensions.All, t => Assert.True(File.Exists(TemplatePath(t)), TemplatePath(t)));
    }

    [Theory]
    [MemberData(nameof(AllDocumentTypes))]
    public async Task Render_ProducesPackageWithoutValidationErrors(DocumentType documentType)
    {
        var inspection = await RenderOrAssertAwaitingAsync(documentType);
        if (inspection is null)
        {
            return;
        }

        Assert.Empty(inspection.ValidationErrors);
        Assert.NotEmpty(inspection.Headings(1));
    }

    [Theory]
    [MemberData(nameof(AllDocumentTypes))]
    public async Task Render_LeavesNoTemplateSyntax(DocumentType documentType)
    {
        var inspection = await RenderOrAssertAwaitingAsync(documentType);
        if (inspection is null)
        {
            return;
        }

        foreach (var token in new[] { "{{", "}}", "{%", "%}" })
        {
            Assert.DoesNotContain(token, inspection.AllText, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(AllDocumentTypes))]
    public async Task Render_KeepsEveryStaticHeadingWithItsHeadingStyle(DocumentType documentType)
    {
        var inspection = await RenderOrAssertAwaitingAsync(documentType);
        if (inspection is null)
        {
            return;
        }

        var expected = StaticHeadings(LoadDefinition(documentType).Body);
        Assert.NotEmpty(expected);
        foreach (var (level, text) in expected)
        {
            Assert.Contains(inspection.Headings(Math.Min(level, 4)), h => h.Text == text);
        }
    }

    [Theory]
    [MemberData(nameof(AllDocumentTypes))]
    public async Task Render_KeepsEveryStaticTableHeader(DocumentType documentType)
    {
        var inspection = await RenderOrAssertAwaitingAsync(documentType);
        if (inspection is null)
        {
            return;
        }

        var expected = StaticTableHeaders(LoadDefinition(documentType).Body);
        Assert.NotEmpty(expected);
        foreach (var header in expected)
        {
            Assert.Contains(inspection.Tables, t => t.Header is { } h && h.Cells.SequenceEqual(header));
        }
    }

    [Theory]
    [MemberData(nameof(AllDocumentTypes))]
    public async Task Render_PutsSampleValueIntoTableDataCell(DocumentType documentType)
    {
        var inspection = await RenderOrAssertAwaitingAsync(documentType);
        if (inspection is null)
        {
            return;
        }

        var cell = SampleTableCell[documentType];
        Assert.Contains(inspection.Tables, t => t.DataRows.Any(r => r.Cells.Contains(cell)));
    }

    [Theory]
    [MemberData(nameof(AllDocumentTypes))]
    public async Task Render_SubstitutesEveryScalarValueTheTemplateOutputs(DocumentType documentType)
    {
        var inspection = await RenderOrAssertAwaitingAsync(documentType);
        if (inspection is null)
        {
            return;
        }

        var data = SampleData.Load(documentType);
        var values = OutputValues(LoadDefinition(documentType).Body, data);
        Assert.NotEmpty(values);
        foreach (var (path, value) in values)
        {
            Assert.True(inspection.AllText.Contains(value, StringComparison.Ordinal), $"'{path}' = '{value}' is missing from the document.");
        }
    }

    [Theory]
    [MemberData(nameof(AllDocumentTypes))]
    public async Task Render_IsDeterministic(DocumentType documentType)
    {
        if (await AssertAwaitingAsync(documentType))
        {
            return;
        }

        var first = await RenderAsync(documentType, SampleData.Load(documentType));
        var second = await RenderAsync(documentType, SampleData.Load(documentType));
        Assert.Equal(first.Content, second.Content);
    }

    [Theory]
    [MemberData(nameof(AllDocumentTypes))]
    public async Task Render_EmptyData_ReportsEveryRequiredFieldAndProducesNoDocument(DocumentType documentType)
    {
        var definition = LoadDefinition(documentType);
        Assert.NotEmpty(definition.RequiredFields);

        var exception = await Assert.ThrowsAsync<RequiredFieldsMissingException>(
            () => RenderAsync(documentType, SampleData.Parse("{}")));

        Assert.Equal(documentType.ToTemplateId(), exception.TemplateId);
        Assert.Equal(definition.RequiredFields, exception.MissingPaths);
        Assert.All(definition.RequiredFields, path => Assert.Contains(path, exception.Message, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(AllDocumentTypes))]
    public async Task Render_OneRequiredFieldRemoved_NamesExactlyThatPath(DocumentType documentType)
    {
        var definition = LoadDefinition(documentType);
        var path = definition.RequiredFields[^1];
        var data = SampleData.LoadNode(documentType);
        RemovePath(data, path);

        var exception = await Assert.ThrowsAsync<RequiredFieldsMissingException>(
            () => RenderAsync(documentType, SampleData.ToElement(data)));

        Assert.Equal([path], exception.MissingPaths);
    }

    [Fact]
    public async Task SowTemplate_ThreeDeliverables_RenderExactlyThreeDataRows()
    {
        if (await AssertAwaitingAsync(DocumentType.Sow))
        {
            return;
        }

        var data = SampleData.LoadNode(DocumentType.Sow);
        var deliverables = new JsonArray();
        foreach (var id in new[] { "D1", "D2", "D3" })
        {
            deliverables.Add(new JsonObject
            {
                ["id"] = id,
                ["name"] = $"Deliverable {id}",
                ["description"] = $"Description of {id}.",
                ["acceptance_criteria"] = $"Criteria for {id}.",
                ["due_date"] = "2026-06-30",
            });
        }

        data["sow"]!["deliverables"] = deliverables;
        var rendered = await RenderAsync(DocumentType.Sow, SampleData.ToElement(data));
        var inspection = DocxInspection.Open(rendered.Content);

        var table = Assert.Single(inspection.Tables, t => t.DataRows.Any(r => r.Cells.Contains("Deliverable D1")));
        Assert.Equal(3, table.DataRows.Count());
        Assert.Equal(["D1", "D2", "D3"], table.DataRows.Select(r => r.Cells[0]));
    }

    private async Task<DocxInspection?> RenderOrAssertAwaitingAsync(DocumentType documentType)
    {
        if (await AssertAwaitingAsync(documentType))
        {
            return null;
        }

        var rendered = await RenderAsync(documentType, SampleData.Load(documentType));
        return DocxInspection.Open(rendered.Content);
    }

    /// <summary>Returns true, after asserting the syntax error, when the template is known not to conform yet.</summary>
    private async Task<bool> AssertAwaitingAsync(DocumentType documentType)
    {
        if (TestPaths.TemplatesOverridden || !AwaitingSubsetConformance.Contains(documentType))
        {
            return false;
        }

        try
        {
            await RenderAsync(documentType, SampleData.Load(documentType));
        }
        catch (TemplateSyntaxException ex)
        {
            Assert.NotEmpty(ex.Errors);
            output.WriteLine($"{documentType.ToTemplateId()}.md is awaiting subset conformance:");
            foreach (var error in ex.Errors)
            {
                output.WriteLine($"  {documentType.ToTemplateId()}.md{error}");
            }

            return true;
        }

        Assert.Fail(
            $"templates/{documentType.ToTemplateId()}.md now renders. Remove {documentType} from " +
            $"{nameof(RepositoryTemplateTests)}.{nameof(AwaitingSubsetConformance)} so the full checks run for it.");
        return false;
    }

    private static Task<RenderedDocument> RenderAsync(DocumentType documentType, JsonElement data) =>
        new DocxDocumentRenderer(new FileSystemTemplateSource(TestPaths.TemplatesDirectory))
            .RenderDocxAsync(documentType, data, RenderOptions.Default);

    private static string TemplatePath(DocumentType documentType) =>
        Path.Combine(TestPaths.TemplatesDirectory, documentType.ToTemplateId() + ".md");

    private static TemplateDefinition LoadDefinition(DocumentType documentType)
    {
        var path = TemplatePath(documentType);
        return TemplateDefinitionParser.Parse(new TemplateSource(documentType, path, File.ReadAllText(path)));
    }

    /// <summary>Markdown headings in the template body that contain no template syntax.</summary>
    private static List<(int Level, string Text)> StaticHeadings(string body) =>
        Lines(body)
            .Select(line => HeadingLine().Match(line))
            .Where(m => m.Success && !m.Value.Contains('{'))
            .Select(m => (m.Groups["marks"].Length, m.Groups["text"].Value.Trim()))
            .ToList();

    /// <summary>Header rows of pipe tables in the template body that contain no template syntax.</summary>
    private static List<string[]> StaticTableHeaders(string body)
    {
        var lines = Lines(body);
        var headers = new List<string[]>();
        for (var i = 0; i + 1 < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith('|') && DelimiterRow().IsMatch(lines[i + 1]) && !lines[i].Contains('{'))
            {
                headers.Add(lines[i].Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray());
            }
        }

        return headers;
    }

    /// <summary>
    /// Every <c>{{ path }}</c> outside a loop whose sample value is a string or number, with the
    /// text the document must contain.
    /// </summary>
    private static List<(string Path, string Value)> OutputValues(string body, JsonElement data)
    {
        var loopVariables = LoopVariable().Matches(body).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var result = new List<(string, string)>();
        foreach (var path in OutputPath().Matches(body).Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal))
        {
            if (loopVariables.Contains(path.Split('.')[0]) || !TryResolve(data, path, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text)
            {
                result.Add((path, text.Trim()));
            }
            else if (value.ValueKind == JsonValueKind.Number)
            {
                result.Add((path, value.GetRawText()));
            }
        }

        return result;
    }

    private static bool TryResolve(JsonElement data, string path, out JsonElement value)
    {
        value = data;
        foreach (var segment in path.Split('.'))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                return false;
            }
        }

        return true;
    }

    private static void RemovePath(JsonObject data, string path)
    {
        var segments = path.Split('.');
        JsonNode? node = data;
        foreach (var segment in segments[..^1])
        {
            node = node?[segment];
        }

        Assert.True(node is JsonObject parent && parent.Remove(segments[^1]), $"The fixture has no '{path}'.");
    }

    private static string[] Lines(string body) => body.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

    [GeneratedRegex(@"^(?<marks>#{1,6})[ \t]+(?<text>.+?)[ \t]*$")]
    private static partial Regex HeadingLine();

    [GeneratedRegex(@"^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)*\|?\s*$")]
    private static partial Regex DelimiterRow();

    [GeneratedRegex(@"\{%-?\s*for\s+([A-Za-z_]\w*)\s+in\s")]
    private static partial Regex LoopVariable();

    [GeneratedRegex(@"\{\{-?\s*([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*-?\}\}")]
    private static partial Regex OutputPath();
}
