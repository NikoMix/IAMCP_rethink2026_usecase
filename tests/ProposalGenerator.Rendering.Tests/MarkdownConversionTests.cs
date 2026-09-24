using DocumentFormat.OpenXml.Wordprocessing;
using ProposalGenerator.Rendering.Docx;
using ProposalGenerator.Rendering.Tests.Support;

namespace ProposalGenerator.Rendering.Tests;

/// <summary>Markdown-to-DOCX conversion through the public renderer, with test-owned template text.</summary>
public sealed class MarkdownConversionTests
{
    [Fact]
    public async Task Headings_UseHeadingStylesOneToFour()
    {
        var inspection = await RenderAsync("# One\n\n## Two\n\n### Three\n\n#### Four\n\n##### Five\n\nBody text\n");

        Assert.Equal(
            [("Heading1", "One"), ("Heading2", "Two"), ("Heading3", "Three"), ("Heading4", "Four"), ("Heading4", "Five"), (null, "Body text")],
            inspection.Paragraphs.Select(p => (p.StyleId, p.Text)));
        Assert.Superset(
            new HashSet<string> { DocxStyleIds.Normal, DocxStyleIds.Heading1, DocxStyleIds.Heading2, DocxStyleIds.Heading3, DocxStyleIds.Heading4 },
            new HashSet<string>(inspection.DefinedStyles));
    }

    [Fact]
    public async Task Paragraphs_KeepBoldItalicAndPlainRuns()
    {
        var inspection = await RenderAsync("Plain **bold** and *italic* and ***both***.\n");

        var paragraph = Assert.Single(inspection.Paragraphs);
        Assert.Equal("Plain bold and italic and both.", paragraph.Text);
        Assert.Equal(
            [
                new RunInfo("Plain ", false, false),
                new RunInfo("bold", true, false),
                new RunInfo(" and ", false, false),
                new RunInfo("italic", false, true),
                new RunInfo(" and ", false, false),
                new RunInfo("both", true, true),
                new RunInfo(".", false, false),
            ],
            paragraph.Runs);
    }

    [Fact]
    public async Task BulletList_UsesBulletNumberingDefinition()
    {
        var inspection = await RenderAsync("- First\n- Second\n  - Nested\n- Third\n");

        var items = inspection.Paragraphs.ToList();
        Assert.Equal(["First", "Second", "Nested", "Third"], items.Select(p => p.Text));
        Assert.All(items, p => Assert.Equal(DocxStyleIds.ListParagraph, p.StyleId));
        Assert.Equal([0, 0, 1, 0], items.Select(p => p.NumberingLevel));

        var outer = inspection.Numbering[items[0].NumberingId!.Value];
        var nested = inspection.Numbering[items[2].NumberingId!.Value];
        Assert.Equal(items[0].NumberingId, items[3].NumberingId);
        Assert.Equal(NumberFormatValues.Bullet, outer.LevelFormats[0]);
        Assert.Equal(NumberFormatValues.Bullet, nested.LevelFormats[1]);
    }

    [Fact]
    public async Task OrderedLists_UseDecimalNumberingAndRestartPerList()
    {
        var inspection = await RenderAsync("1. One\n2. Two\n\nBetween\n\n1. Again\n\nAnd\n\n3. Three\n4. Four\n");

        var items = inspection.Paragraphs.Where(p => p.NumberingId is not null).ToList();
        Assert.Equal(["One", "Two", "Again", "Three", "Four"], items.Select(p => p.Text));

        var first = inspection.Numbering[items[0].NumberingId!.Value];
        var second = inspection.Numbering[items[2].NumberingId!.Value];
        var third = inspection.Numbering[items[3].NumberingId!.Value];
        Assert.Equal(3, new[] { items[0], items[2], items[3] }.Select(p => p.NumberingId).Distinct().Count());
        Assert.Equal(NumberFormatValues.Decimal, first.LevelFormats[0]);
        Assert.Equal(first.AbstractNumberId, second.AbstractNumberId);
        Assert.Equal(1, first.StartOverrides[0]);
        Assert.Equal(1, second.StartOverrides[0]);
        Assert.Equal(3, third.StartOverrides[0]);
    }

    [Fact]
    public async Task Table_HasRepeatingHeaderRowBoldHeaderCellsAndDataRows()
    {
        var inspection = await RenderAsync("| ID | Name |\n| --- | :---: |\n| D1 | *First* |\n| D2 | Second |\n");

        var table = Assert.Single(inspection.Tables);
        Assert.Equal(DocxStyleIds.TableGrid, table.StyleId);
        Assert.Equal(3, table.Rows.Count);
        Assert.True(table.Rows[0].IsHeader);
        Assert.Equal(["ID", "Name"], table.Rows[0].Cells);
        Assert.All(table.Rows[0].Runs, r => Assert.True(r.Bold));
        Assert.Equal([["D1", "First"], ["D2", "Second"]], table.DataRows.Select(r => r.Cells));
        Assert.All(table.DataRows, r => Assert.False(r.IsHeader));
        Assert.Contains(table.Rows[1].Runs, r => r.Text == "First" && r.Italic && !r.Bold);
    }

    [Fact]
    public async Task Table_ShortRowsArePaddedToTheColumnCount()
    {
        var inspection = await RenderAsync("| A | B | C |\n| --- | --- | --- |\n| 1 |\n");

        var table = Assert.Single(inspection.Tables);
        Assert.Equal(["1", string.Empty, string.Empty], table.DataRows.Single().Cells);
    }

    [Fact]
    public async Task ThematicBreak_BecomesPageBreak()
    {
        var inspection = await RenderAsync("Before\n\n---\n\nAfter\n");

        Assert.Equal([false, true, false], inspection.Paragraphs.Select(p => p.HasPageBreak));
        Assert.Equal(["Before", string.Empty, "After"], inspection.Paragraphs.Select(p => p.Text));
    }

    [Fact]
    public async Task BlockQuote_UsesQuoteStyle()
    {
        var inspection = await RenderAsync("> Guidance for the author.\n");

        var paragraph = Assert.Single(inspection.Paragraphs);
        Assert.Equal((DocxStyleIds.Quote, "Guidance for the author."), (paragraph.StyleId, paragraph.Text));
    }

    [Fact]
    public async Task LinksAndInlineCode_KeepTheirText()
    {
        var inspection = await RenderAsync("See [the portal](https://portal.example) and `code`.\n");

        Assert.Equal("See the portal and code.", Assert.Single(inspection.Paragraphs).Text);
    }

    [Theory]
    [InlineData("```\ncode block\n```\n", "FencedCodeBlock")]
    [InlineData("<div>html</div>\n", "HtmlBlock")]
    [InlineData("An ![image](https://img.example/a.png) inline.\n", "LinkInline")]
    public async Task UnsupportedConstructs_FailExplicitly(string markdown, string construct)
    {
        var exception = await Assert.ThrowsAsync<UnsupportedMarkdownException>(() => RenderAsync(markdown));
        Assert.Equal(construct, exception.Construct);
    }

    [Fact]
    public async Task TableFollowedDirectlyByText_FailsInsteadOfBecomingAParagraph()
    {
        var exception = await Assert.ThrowsAsync<UnsupportedMarkdownException>(
            () => RenderAsync("Intro\n\n| A | B |\n| --- | --- |\n| 1 | 2 |\nTail text\n"));

        Assert.StartsWith("Table not recognised", exception.Construct, StringComparison.Ordinal);
        Assert.Equal(3, exception.Line);
    }

    [Fact]
    public async Task Output_OpensAndPassesValidation()
    {
        var inspection = await RenderAsync(
            "# Title\n\nText with **bold**.\n\n- a\n  1. b\n\n| H |\n| --- |\n| c |\n\n> note\n\n---\n\nEnd\n");

        Assert.Empty(inspection.ValidationErrors);
    }

    private static async Task<DocxInspection> RenderAsync(string markdown)
    {
        var rendered = await InMemoryTemplateSource.Renderer(markdown)
            .RenderDocxAsync(DocumentType.Sow, SampleData.Parse("{}"), RenderOptions.Default);
        var inspection = DocxInspection.Open(rendered.Content);
        Assert.Empty(inspection.ValidationErrors);
        return inspection;
    }
}
