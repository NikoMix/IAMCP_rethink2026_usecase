using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using WTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using WTableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using WTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;

namespace ProposalGenerator.Rendering.Docx;

/// <summary>
/// Converts Markdown to WordprocessingML through the Markdig syntax tree.
/// </summary>
/// <remarks>
/// Supported: ATX/setext headings (levels 1–4, deeper levels use Heading 4), paragraphs, bold,
/// italic, inline code, hard line breaks, links (as their text), bullet and ordered lists
/// (nested), GFM pipe tables with a header row, block quotes of paragraphs (Quote style), and
/// thematic breaks (as page breaks).
/// Every other block or inline throws <see cref="UnsupportedMarkdownException"/>.
/// </remarks>
internal sealed partial class MarkdownDocxWriter
{
    /// <summary>A4 portrait in twentieths of a point.</summary>
    internal const int PageWidth = 11906;

    internal const int PageHeight = 16838;

    internal const int PageMargin = 1418;

    internal const int TextWidth = PageWidth - (2 * PageMargin);

    private const int IndentPerLevel = 720;

    private const int BulletAbstractNumberId = 1;

    private const int OrderedAbstractNumberId = 2;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .Build();

    private readonly List<NumberingInstance> numberingInstances = [];

    private readonly string source;

    private MarkdownDocxWriter(string source)
    {
        this.source = source;
    }

    /// <summary>Parses <paramref name="markdown"/> and writes it as the body of <paramref name="mainPart"/>.</summary>
    public static void Write(string markdown, MainDocumentPart mainPart)
    {
        var writer = new MarkdownDocxWriter(markdown);
        var ast = Markdown.Parse(markdown, Pipeline);

        var body = new Body();
        foreach (var block in ast)
        {
            body.Append(writer.ConvertBlock(block));
        }

        body.Append(new SectionProperties(
            new PageSize { Width = PageWidth, Height = PageHeight },
            new PageMargin
            {
                Top = PageMargin,
                Right = PageMargin,
                Bottom = PageMargin,
                Left = PageMargin,
                Header = 709,
                Footer = 709,
                Gutter = 0,
            }));

        mainPart.Document = new Document(body);

        if (writer.numberingInstances.Count > 0)
        {
            var numberingPart = mainPart.AddNewPart<NumberingDefinitionsPart>("rIdNumbering");
            numberingPart.Numbering = writer.CreateNumbering();
        }
    }

    private IEnumerable<OpenXmlElement> ConvertBlock(Block block) => block switch
    {
        HeadingBlock heading => [ConvertHeading(heading)],
        ParagraphBlock paragraph => [ConvertParagraph(paragraph)],
        ListBlock list => ConvertList(list, level: 0),
        MdTable table => [ConvertTable(table)],
        QuoteBlock quote => ConvertQuote(quote),
        ThematicBreakBlock => [new Paragraph(new Run(new Break { Type = BreakValues.Page }))],
        LinkReferenceDefinitionGroup => [],
        _ => throw Unsupported(block),
    };

    /// <summary>
    /// Markdig does not recognise a pipe table that is directly followed by a text line, and
    /// parses the whole table as one paragraph of pipes and dashes. A paragraph containing a
    /// table delimiter row is therefore a broken table. The check runs on the Markdown source,
    /// where substituted values are escaped, so data cannot trigger it.
    /// </summary>
    private Paragraph ConvertParagraph(ParagraphBlock paragraph)
    {
        var text = source.Substring(paragraph.Span.Start, Math.Max(0, paragraph.Span.Length));
        if (text.Split('\n').Any(line => TableDelimiterRow().IsMatch(line)))
        {
            throw Unsupported(paragraph, "Table not recognised (add a blank line after the last table row)");
        }

        return CreateParagraph(paragraph.Inline, properties: null);
    }

    [GeneratedRegex(@"^\s*\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)+\|?\s*$|^\s*\|\s*:?-+:?\s*\|\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TableDelimiterRow();

    private static List<OpenXmlElement> ConvertQuote(QuoteBlock quote)
    {
        var elements = new List<OpenXmlElement>();
        foreach (var child in quote)
        {
            if (child is not ParagraphBlock paragraph)
            {
                throw Unsupported(child);
            }

            elements.Add(CreateParagraph(
                paragraph.Inline,
                new ParagraphProperties { ParagraphStyleId = new ParagraphStyleId { Val = DocxStyleIds.Quote } }));
        }

        return elements;
    }

    private Paragraph ConvertHeading(HeadingBlock heading) =>
        CreateParagraph(
            heading.Inline,
            new ParagraphProperties { ParagraphStyleId = new ParagraphStyleId { Val = DocxStyleIds.Heading(heading.Level) } });

    private List<OpenXmlElement> ConvertList(ListBlock list, int level)
    {
        var numberId = AddNumberingInstance(list, level);
        var elements = new List<OpenXmlElement>();

        foreach (var item in list.Cast<ListItemBlock>())
        {
            var numbered = false;
            foreach (var child in item)
            {
                switch (child)
                {
                    case ParagraphBlock paragraph:
                        elements.Add(CreateParagraph(paragraph.Inline, ListParagraphProperties(numbered ? null : numberId, level)));
                        numbered = true;
                        break;
                    case ListBlock nested:
                        if (!numbered)
                        {
                            elements.Add(new Paragraph(ListParagraphProperties(numberId, level)));
                            numbered = true;
                        }

                        elements.AddRange(ConvertList(nested, level + 1));
                        break;
                    default:
                        throw Unsupported(child);
                }
            }

            if (!numbered)
            {
                elements.Add(new Paragraph(ListParagraphProperties(numberId, level)));
            }
        }

        return elements;
    }

    private static ParagraphProperties ListParagraphProperties(int? numberId, int level)
    {
        var properties = new ParagraphProperties { ParagraphStyleId = new ParagraphStyleId { Val = DocxStyleIds.ListParagraph } };
        if (numberId is { } id)
        {
            properties.NumberingProperties = new NumberingProperties(
                new NumberingLevelReference { Val = level },
                new NumberingId { Val = id });
        }
        else
        {
            properties.Indentation = new Indentation { Left = Twips(IndentPerLevel * (level + 1)) };
        }

        return properties;
    }

    private int AddNumberingInstance(ListBlock list, int level)
    {
        var numberId = numberingInstances.Count + 1;
        var instance = new NumberingInstance(
            new AbstractNumId { Val = list.IsOrdered ? OrderedAbstractNumberId : BulletAbstractNumberId })
        {
            NumberID = numberId,
        };

        if (list.IsOrdered)
        {
            // Instances that share an abstract definition continue each other's count unless the start is overridden.
            var start = int.TryParse(list.OrderedStart, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 1;
            instance.Append(new LevelOverride(new StartOverrideNumberingValue { Val = start }) { LevelIndex = level });
        }

        numberingInstances.Add(instance);
        return numberId;
    }

    private Numbering CreateNumbering()
    {
        var numbering = new Numbering(
            CreateAbstractNumbering(BulletAbstractNumberId, ordered: false),
            CreateAbstractNumbering(OrderedAbstractNumberId, ordered: true));
        numbering.Append(numberingInstances);
        return numbering;
    }

    private static AbstractNum CreateAbstractNumbering(int abstractNumberId, bool ordered)
    {
        string[] bullets = ["\u2022", "\u25E6", "\u25AA"];
        NumberFormatValues[] formats = [NumberFormatValues.Decimal, NumberFormatValues.LowerLetter, NumberFormatValues.LowerRoman];

        var abstractNum = new AbstractNum(new MultiLevelType { Val = MultiLevelValues.HybridMultilevel })
        {
            AbstractNumberId = abstractNumberId,
        };

        for (var level = 0; level < 9; level++)
        {
            abstractNum.Append(new Level(
                new StartNumberingValue { Val = 1 },
                new NumberingFormat { Val = ordered ? formats[level % formats.Length] : NumberFormatValues.Bullet },
                new LevelText { Val = ordered ? $"%{level + 1}." : bullets[level % bullets.Length] },
                new LevelJustification { Val = LevelJustificationValues.Left },
                new PreviousParagraphProperties(new Indentation
                {
                    Left = Twips(IndentPerLevel * (level + 1)),
                    Hanging = "360",
                }))
            {
                LevelIndex = level,
            });
        }

        return abstractNum;
    }

    private WTable ConvertTable(MdTable table)
    {
        var rows = table.Cast<MdTableRow>().ToList();
        if (rows.Count == 0 || !rows[0].IsHeader)
        {
            throw Unsupported(table, "Table without header row");
        }

        var columnCount = rows.Max(r => r.Count);
        var columnWidth = TextWidth / columnCount;

        var grid = new TableGrid();
        for (var i = 0; i < columnCount; i++)
        {
            grid.Append(new GridColumn { Width = Twips(columnWidth) });
        }

        var result = new WTable(
            new TableProperties(
                new TableStyle { Val = DocxStyleIds.TableGrid },
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableLook { Val = "04A0", FirstRow = true, LastRow = false, FirstColumn = false, LastColumn = false, NoHorizontalBand = true, NoVerticalBand = true }),
            grid);

        foreach (var row in rows)
        {
            var wordRow = new WTableRow();
            if (row.IsHeader)
            {
                wordRow.Append(new TableRowProperties(new TableHeader()));
            }

            for (var column = 0; column < columnCount; column++)
            {
                var cell = column < row.Count ? (MdTableCell)row[column] : null;
                var alignment = column < table.ColumnDefinitions.Count ? table.ColumnDefinitions[column].Alignment : null;
                wordRow.Append(ConvertCell(cell, alignment, row.IsHeader, columnWidth));
            }

            result.Append(wordRow);
        }

        return result;
    }

    private WTableCell ConvertCell(MdTableCell? cell, TableColumnAlign? alignment, bool header, int width)
    {
        var wordCell = new WTableCell(new TableCellProperties(
            new TableCellWidth { Width = Twips(width), Type = TableWidthUnitValues.Dxa }));

        var justification = alignment switch
        {
            TableColumnAlign.Center => JustificationValues.Center,
            TableColumnAlign.Right => JustificationValues.Right,
            _ => (JustificationValues?)null,
        };

        var paragraphs = 0;
        foreach (var block in cell ?? Enumerable.Empty<Block>())
        {
            if (block is not ParagraphBlock paragraph)
            {
                throw Unsupported(block);
            }

            var properties = new ParagraphProperties();
            if (justification is { } value)
            {
                properties.Justification = new Justification { Val = value };
            }

            wordCell.Append(CreateParagraph(paragraph.Inline, properties, new InlineFormat(Bold: header, Italic: false, Code: false)));
            paragraphs++;
        }

        if (paragraphs == 0)
        {
            wordCell.Append(new Paragraph());
        }

        return wordCell;
    }

    private static Paragraph CreateParagraph(ContainerInline? inline, ParagraphProperties? properties, InlineFormat format = default)
    {
        var paragraph = new Paragraph();
        if (properties is not null && properties.HasChildren)
        {
            paragraph.Append(properties);
        }

        var runs = new RunBuilder();
        if (inline is not null)
        {
            AppendInlines(inline, format, runs);
        }

        paragraph.Append(runs.Build());
        return paragraph;
    }

    private static void AppendInlines(ContainerInline container, InlineFormat format, RunBuilder runs)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    runs.AddText(literal.Content.ToString(), format);
                    break;
                case EmphasisInline emphasis:
                    var nested = emphasis.DelimiterCount >= 2
                        ? format with { Bold = true }
                        : format with { Italic = true };
                    AppendInlines(emphasis, nested, runs);
                    break;
                case LineBreakInline lineBreak:
                    if (lineBreak.IsHard)
                    {
                        runs.AddBreak(format);
                    }
                    else
                    {
                        runs.AddText(" ", format);
                    }

                    break;
                case CodeInline code:
                    runs.AddText(code.Content, format with { Code = true });
                    break;
                case HtmlEntityInline entity:
                    runs.AddText(entity.Transcoded.ToString(), format);
                    break;
                case AutolinkInline autolink:
                    runs.AddText(autolink.Url, format);
                    break;
                case LinkInline { IsImage: false } link:
                    AppendInlines(link, format, runs);
                    break;
                case LinkDelimiterInline delimiter:
                    runs.AddText(delimiter.ToLiteral(), format);
                    AppendInlines(delimiter, format, runs);
                    break;
                default:
                    throw Unsupported(inline);
            }
        }
    }

    private static string Twips(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static UnsupportedMarkdownException Unsupported(MarkdownObject node, string? construct = null) =>
        new(construct ?? node.GetType().Name, node.Line + 1);

    private readonly record struct InlineFormat(bool Bold, bool Italic, bool Code);

    /// <summary>Collects text runs and merges adjacent text with the same formatting.</summary>
    private sealed class RunBuilder
    {
        private readonly List<Run> runs = [];
        private readonly StringBuilder pending = new();
        private InlineFormat pendingFormat;

        public void AddText(string text, InlineFormat format)
        {
            if (text.Length == 0)
            {
                return;
            }

            if (pending.Length > 0 && format != pendingFormat)
            {
                Flush();
            }

            pendingFormat = format;
            pending.Append(text);
        }

        public void AddBreak(InlineFormat format)
        {
            Flush();
            runs.Add(new Run(CreateRunProperties(format), new Break()).RemoveEmptyProperties());
        }

        public IEnumerable<Run> Build()
        {
            Flush();
            return runs;
        }

        private void Flush()
        {
            if (pending.Length == 0)
            {
                return;
            }

            runs.Add(new Run(
                CreateRunProperties(pendingFormat),
                new Text(pending.ToString()) { Space = SpaceProcessingModeValues.Preserve }).RemoveEmptyProperties());
            pending.Clear();
        }

        private static RunProperties CreateRunProperties(InlineFormat format)
        {
            var properties = new RunProperties();
            if (format.Code)
            {
                properties.Append(new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas", ComplexScript = "Consolas" });
            }

            if (format.Bold)
            {
                properties.Append(new Bold());
            }

            if (format.Italic)
            {
                properties.Append(new Italic());
            }

            return properties;
        }
    }
}

internal static class RunExtensions
{
    public static Run RemoveEmptyProperties(this Run run)
    {
        if (run.RunProperties is { HasChildren: false } properties)
        {
            properties.Remove();
        }

        return run;
    }
}
