using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ProposalGenerator.Rendering.Tests.Support;

/// <summary>A read-only view of a rendered DOCX package, extracted with the Open XML SDK.</summary>
internal sealed class DocxInspection
{
    /// <summary>
    /// The schema version the tests validate against. It is deliberately newer than the version
    /// the renderer checks itself (Office 2016), so the tests are an independent check.
    /// </summary>
    public const FileFormatVersions ValidationTarget = FileFormatVersions.Microsoft365;

    private DocxInspection(
        IReadOnlyList<string> validationErrors,
        IReadOnlyList<BlockInfo> blocks,
        IReadOnlyDictionary<int, NumberingInfo> numbering,
        IReadOnlySet<string> definedStyles)
    {
        ValidationErrors = validationErrors;
        Blocks = blocks;
        Numbering = numbering;
        DefinedStyles = definedStyles;
    }

    public IReadOnlyList<string> ValidationErrors { get; }

    /// <summary>Body-level paragraphs and tables, in document order.</summary>
    public IReadOnlyList<BlockInfo> Blocks { get; }

    public IEnumerable<ParagraphInfo> Paragraphs => Blocks.OfType<ParagraphInfo>();

    public IEnumerable<TableInfo> Tables => Blocks.OfType<TableInfo>();

    /// <summary>Numbering instances by <c>w:numId</c>.</summary>
    public IReadOnlyDictionary<int, NumberingInfo> Numbering { get; }

    public IReadOnlySet<string> DefinedStyles { get; }

    /// <summary>Every paragraph's text, including table cells, one paragraph per line.</summary>
    public string AllText => string.Join('\n', Blocks.SelectMany(b => b switch
    {
        ParagraphInfo p => [p.Text],
        TableInfo t => t.Rows.SelectMany(r => r.Cells),
        _ => Array.Empty<string>(),
    }));

    public IEnumerable<ParagraphInfo> Headings(int level) =>
        Paragraphs.Where(p => p.StyleId == $"Heading{level}");

    public static DocxInspection Open(byte[] package)
    {
        using var stream = new MemoryStream(package, writable: false);
        using var document = WordprocessingDocument.Open(stream, isEditable: false);

        var errors = new OpenXmlValidator(ValidationTarget)
            .Validate(document)
            .Select(e => $"{e.Part?.Uri}{e.Path?.XPath}: {e.Description}")
            .ToArray();

        var mainPart = document.MainDocumentPart ?? throw new InvalidOperationException("The package has no main document part.");
        var body = mainPart.Document?.Body ?? throw new InvalidOperationException("The document has no body.");

        var blocks = new List<BlockInfo>();
        foreach (var element in body.ChildElements)
        {
            switch (element)
            {
                case Paragraph paragraph:
                    blocks.Add(ReadParagraph(paragraph));
                    break;
                case Table table:
                    blocks.Add(ReadTable(table));
                    break;
            }
        }

        var styles = mainPart.StyleDefinitionsPart?.Styles?.Elements<Style>()
            .Select(s => s.StyleId?.Value)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal) ?? [];

        return new DocxInspection(errors, blocks, ReadNumbering(mainPart), styles);
    }

    private static ParagraphInfo ReadParagraph(Paragraph paragraph)
    {
        var properties = paragraph.ParagraphProperties;
        var runs = paragraph.Elements<Run>()
            .Select(r => new RunInfo(
                string.Concat(r.Elements<Text>().Select(t => t.Text)),
                r.RunProperties?.Bold is not null,
                r.RunProperties?.Italic is not null))
            .ToArray();

        return new ParagraphInfo(
            properties?.ParagraphStyleId?.Val?.Value,
            string.Concat(runs.Select(r => r.Text)),
            properties?.NumberingProperties?.NumberingId?.Val?.Value,
            properties?.NumberingProperties?.NumberingLevelReference?.Val?.Value,
            paragraph.Descendants<Break>().Any(b => b.Type?.Value == BreakValues.Page),
            runs);
    }

    private static TableInfo ReadTable(Table table)
    {
        var rows = table.Elements<TableRow>()
            .Select(row => new TableRowInfo(
                row.TableRowProperties?.GetFirstChild<TableHeader>() is not null,
                row.Elements<TableCell>()
                    .Select(cell => string.Join('\n', cell.Elements<Paragraph>().Select(p => ReadParagraph(p).Text)))
                    .ToArray(),
                row.Elements<TableCell>()
                    .SelectMany(cell => cell.Elements<Paragraph>().SelectMany(p => ReadParagraph(p).Runs))
                    .ToArray()))
            .ToArray();

        return new TableInfo(table.GetFirstChild<TableProperties>()?.TableStyle?.Val?.Value, rows);
    }

    private static Dictionary<int, NumberingInfo> ReadNumbering(MainDocumentPart mainPart)
    {
        var numbering = mainPart.NumberingDefinitionsPart?.Numbering;
        if (numbering is null)
        {
            return [];
        }

        var abstracts = numbering.Elements<AbstractNum>()
            .ToDictionary(a => a.AbstractNumberId!.Value, a => a);

        var result = new Dictionary<int, NumberingInfo>();
        foreach (var instance in numbering.Elements<NumberingInstance>())
        {
            var abstractId = instance.AbstractNumId!.Val!.Value;
            var formats = abstracts.TryGetValue(abstractId, out var definition)
                ? definition.Elements<Level>().ToDictionary(l => l.LevelIndex!.Value, l => l.NumberingFormat!.Val!.Value)
                : [];
            var starts = instance.Elements<LevelOverride>()
                .Where(o => o.StartOverrideNumberingValue is not null)
                .ToDictionary(o => o.LevelIndex!.Value, o => o.StartOverrideNumberingValue!.Val!.Value);

            result[instance.NumberID!.Value] = new NumberingInfo(abstractId, formats, starts);
        }

        return result;
    }
}

internal abstract record BlockInfo;

internal sealed record RunInfo(string Text, bool Bold, bool Italic);

internal sealed record ParagraphInfo(
    string? StyleId,
    string Text,
    int? NumberingId,
    int? NumberingLevel,
    bool HasPageBreak,
    IReadOnlyList<RunInfo> Runs) : BlockInfo;

internal sealed record TableRowInfo(bool IsHeader, IReadOnlyList<string> Cells, IReadOnlyList<RunInfo> Runs);

internal sealed record TableInfo(string? StyleId, IReadOnlyList<TableRowInfo> Rows) : BlockInfo
{
    public TableRowInfo? Header => Rows.FirstOrDefault(r => r.IsHeader);

    public IEnumerable<TableRowInfo> DataRows => Rows.Where(r => !r.IsHeader);
}

internal sealed record NumberingInfo(
    int AbstractNumberId,
    IReadOnlyDictionary<int, NumberFormatValues> LevelFormats,
    IReadOnlyDictionary<int, int> StartOverrides);
