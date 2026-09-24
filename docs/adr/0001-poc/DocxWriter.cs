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

namespace Adr0001.Poc;

/// <summary>
/// Builds a DOCX from Markdown through the Markdig AST and the Open XML SDK.
/// Supports the block types the templates use: headings, paragraphs, bullet and ordered lists,
/// pipe tables, block quotes and thematic breaks; inline bold, italic, code, links and hard breaks.
/// </summary>
public sealed class DocxWriter
{
    // A4 with 2.5 cm margins, in twentieths of a point.
    private const int PageWidth = 11906;
    private const int PageHeight = 16838;
    private const int Margin = 1418;
    private const int ContentWidth = PageWidth - (2 * Margin);
    private const string FontName = "Arial";
    private const int BulletAbstractId = 1;
    private const int DecimalAbstractId = 2;
    private const int BulletNumId = 1;

    public static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().Build();

    private readonly Numbering _numbering = new();
    private int _nextNumId = BulletNumId + 1;

    public static void Write(string markdown, string title, string path)
    {
        var writer = new DocxWriter();
        var ast = Markdown.Parse(markdown, Pipeline);

        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        AddStyles(main);
        writer.InitNumbering();

        var body = new Body();
        foreach (var block in ast)
        {
            foreach (var element in writer.ConvertBlock(block, listLevel: -1, numId: 0))
            {
                body.Append(element);
            }
        }

        var footerPart = main.AddNewPart<FooterPart>();
        footerPart.Footer = BuildFooter();
        body.Append(new SectionProperties(
            new FooterReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(footerPart) },
            new PageSize { Width = PageWidth, Height = PageHeight },
            new PageMargin { Top = Margin, Bottom = Margin, Left = Margin, Right = Margin, Header = 709, Footer = 709, Gutter = 0 }));

        main.Document = new Document(body);
        main.AddNewPart<NumberingDefinitionsPart>().Numbering = writer._numbering;

        doc.PackageProperties.Title = title;
        doc.PackageProperties.Creator = "ADR 0001 proof of concept";
    }

    private IEnumerable<OpenXmlElement> ConvertBlock(Block block, int listLevel, int numId)
    {
        switch (block)
        {
            case HeadingBlock heading:
                yield return Para(StyleForHeading(heading.Level), heading.Inline);
                break;

            case ParagraphBlock paragraph when listLevel >= 0:
                {
                    var p = Para("ListParagraph", paragraph.Inline);
                    p.ParagraphProperties!.Append(new NumberingProperties(
                        new NumberingLevelReference { Val = listLevel },
                        new NumberingId { Val = numId }));
                    yield return p;
                    break;
                }

            case ParagraphBlock paragraph:
                yield return Para("Normal", paragraph.Inline);
                break;

            case ListBlock list:
                {
                    var level = listLevel + 1;
                    var id = list.IsOrdered ? NewOrderedNumbering(list) : BulletNumId;
                    foreach (var item in list.OfType<ListItemBlock>())
                    {
                        var first = true;
                        foreach (var child in item)
                        {
                            // Only the first paragraph of an item carries the bullet or number.
                            var childLevel = first || child is ListBlock ? level : -1;
                            foreach (var element in ConvertBlock(child, child is ListBlock ? level : childLevel, id))
                            {
                                if (!first && child is ParagraphBlock && element is Paragraph continuation)
                                {
                                    continuation.ParagraphProperties!.Append(new Indentation { Left = (720 * (level + 1)).ToString() });
                                }

                                yield return element;
                            }

                            first = false;
                        }
                    }

                    break;
                }

            case MdTable table:
                yield return ConvertTable(table);
                yield return new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "0" }));
                break;

            case QuoteBlock quote:
                foreach (var child in quote.OfType<ParagraphBlock>())
                {
                    yield return Para("Quote", child.Inline);
                }

                break;

            case ThematicBreakBlock:
                yield return new Paragraph(new ParagraphProperties(new ParagraphBorders(
                    new BottomBorder { Val = BorderValues.Single, Size = 6, Space = 1, Color = "999999" })));
                break;

            case LeafBlock leaf:
                // Code blocks and HTML blocks are rendered as plain text; templates do not use them.
                yield return new Paragraph(new Run(new Text(leaf.Lines.ToString()) { Space = SpaceProcessingModeValues.Preserve }));
                break;

            default:
                throw new NotSupportedException($"Markdown block {block.GetType().Name} is not supported by the DOCX writer.");
        }
    }

    private WTable ConvertTable(MdTable table)
    {
        var rows = table.OfType<MdTableRow>().ToList();
        var columns = Math.Max(table.ColumnDefinitions.Count, rows.Max(r => r.Count));
        var colWidth = ContentWidth / columns;

        var border = new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 4, Color = "A6A6A6" },
            new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "A6A6A6" },
            new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "A6A6A6" },
            new RightBorder { Val = BorderValues.Single, Size = 4, Color = "A6A6A6" },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "A6A6A6" },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "A6A6A6" });

        var wTable = new WTable(new TableProperties(
            new TableStyle { Val = "TableGrid" },
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
            border,
            new TableLayout { Type = TableLayoutValues.Fixed },
            new TableCellMarginDefault(
                new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                new TableCellLeftMargin { Width = 85, Type = TableWidthValues.Dxa },
                new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                new TableCellRightMargin { Width = 85, Type = TableWidthValues.Dxa })));

        var grid = new TableGrid();
        for (var i = 0; i < columns; i++)
        {
            grid.Append(new GridColumn { Width = colWidth.ToString() });
        }

        wTable.Append(grid);

        foreach (var row in rows)
        {
            var wRow = new WTableRow();
            if (row.IsHeader)
            {
                wRow.Append(new TableRowProperties(new TableHeader(), new CantSplit()));
            }
            else
            {
                wRow.Append(new TableRowProperties(new CantSplit()));
            }

            for (var c = 0; c < columns; c++)
            {
                var cell = c < row.Count ? (MdTableCell)row[c] : null;
                var cellProps = new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = colWidth.ToString() });
                if (row.IsHeader)
                {
                    cellProps.Append(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "DCE6F2" });
                }

                var wCell = new WTableCell(cellProps);
                var align = c < table.ColumnDefinitions.Count ? table.ColumnDefinitions[c].Alignment : null;
                var paragraphs = cell?.OfType<ParagraphBlock>().ToList() ?? [];
                if (paragraphs.Count == 0)
                {
                    wCell.Append(new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "TableText" })));
                }

                foreach (var paragraph in paragraphs)
                {
                    var p = Para("TableText", paragraph.Inline, bold: row.IsHeader);
                    if (align is TableColumnAlign.Right or TableColumnAlign.Center)
                    {
                        p.ParagraphProperties!.Append(new Justification
                        {
                            Val = align == TableColumnAlign.Right ? JustificationValues.Right : JustificationValues.Center,
                        });
                    }

                    wCell.Append(p);
                }

                wRow.Append(wCell);
            }

            wTable.Append(wRow);
        }

        return wTable;
    }

    private static Paragraph Para(string style, ContainerInline? inline, bool bold = false)
    {
        var p = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = style }));
        if (inline is not null)
        {
            foreach (var run in ConvertInlines(inline, bold, italic: false, code: false))
            {
                p.Append(run);
            }
        }

        return p;
    }

    private static IEnumerable<Run> ConvertInlines(ContainerInline container, bool bold, bool italic, bool code)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    yield return TextRun(literal.Content.ToString(), bold, italic, code);
                    break;
                case HtmlEntityInline entity:
                    yield return TextRun(entity.Transcoded.ToString(), bold, italic, code);
                    break;
                case CodeInline codeInline:
                    yield return TextRun(codeInline.Content, bold, italic, code: true);
                    break;
                case LineBreakInline lineBreak when lineBreak.IsHard:
                    yield return new Run(new Break());
                    break;
                case LineBreakInline:
                    yield return TextRun(" ", bold, italic, code);
                    break;
                case EmphasisInline emphasis:
                    foreach (var run in ConvertInlines(emphasis, bold || emphasis.DelimiterCount >= 2, italic || emphasis.DelimiterCount == 1, code))
                    {
                        yield return run;
                    }

                    break;
                case LinkInline link:
                    foreach (var run in ConvertInlines(link, bold, italic, code))
                    {
                        yield return run;
                    }

                    break;
                case ContainerInline nested:
                    foreach (var run in ConvertInlines(nested, bold, italic, code))
                    {
                        yield return run;
                    }

                    break;
                case HtmlInline html:
                    yield return TextRun(html.Tag, bold, italic, code);
                    break;
                default:
                    yield return TextRun(inline.ToString() ?? string.Empty, bold, italic, code);
                    break;
            }
        }
    }

    private static Run TextRun(string text, bool bold, bool italic, bool code)
    {
        var props = new RunProperties();
        if (code)
        {
            props.Append(new RunFonts { Ascii = "Courier New", HighAnsi = "Courier New" });
        }

        if (bold)
        {
            props.Append(new Bold());
        }

        if (italic)
        {
            props.Append(new Italic());
        }

        var run = new Run();
        if (props.HasChildren)
        {
            run.Append(props);
        }

        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    private static string StyleForHeading(int level) => level switch
    {
        1 => "Title",
        2 => "Heading1",
        3 => "Heading2",
        _ => "Heading3",
    };

    private void InitNumbering()
    {
        _numbering.Append(AbstractNumbering(BulletAbstractId, NumberFormatValues.Bullet, ["•", "o", "▪"]));
        _numbering.Append(AbstractNumbering(DecimalAbstractId, NumberFormatValues.Decimal, ["%1.", "%1.%2.", "%1.%2.%3."]));
        _numbering.Append(new NumberingInstance(new AbstractNumId { Val = BulletAbstractId }) { NumberID = BulletNumId });
    }

    private int NewOrderedNumbering(ListBlock list)
    {
        var start = int.TryParse(list.OrderedStart, out var s) ? s : 1;
        var id = _nextNumId++;
        _numbering.Append(new NumberingInstance(
            new AbstractNumId { Val = DecimalAbstractId },
            new LevelOverride(new StartOverrideNumberingValue { Val = start }) { LevelIndex = 0 })
        { NumberID = id });
        return id;
    }

    private static AbstractNum AbstractNumbering(int id, NumberFormatValues format, string[] texts)
    {
        var abstractNum = new AbstractNum(new MultiLevelType { Val = MultiLevelValues.HybridMultilevel }) { AbstractNumberId = id };
        for (var level = 0; level < texts.Length; level++)
        {
            var indent = 720 * (level + 1);
            var runProps = format == NumberFormatValues.Bullet
                ? new NumberingSymbolRunProperties(new RunFonts { Ascii = FontName, HighAnsi = FontName, Hint = FontTypeHintValues.Default })
                : null;
            var lvl = new Level(
                new StartNumberingValue { Val = 1 },
                new NumberingFormat { Val = format },
                new LevelText { Val = texts[level] },
                new LevelJustification { Val = LevelJustificationValues.Left },
                new PreviousParagraphProperties(new Indentation { Left = indent.ToString(), Hanging = "360" }))
            { LevelIndex = level };
            if (runProps is not null)
            {
                lvl.Append(runProps);
            }

            abstractNum.Append(lvl);
        }

        return abstractNum;
    }

    private static Footer BuildFooter()
    {
        static IEnumerable<Run> Field(string instruction) =>
        [
            new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
            new Run(new FieldCode($" {instruction} ") { Space = SpaceProcessingModeValues.Preserve }),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
            new Run(new Text("1")),
            new Run(new FieldChar { FieldCharType = FieldCharValues.End }),
        ];

        var paragraph = new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Footer" }, new Justification { Val = JustificationValues.Right }),
            new Run(new Text("Page ") { Space = SpaceProcessingModeValues.Preserve }));
        paragraph.Append(Field("PAGE"));
        paragraph.Append(new Run(new Text(" of ") { Space = SpaceProcessingModeValues.Preserve }));
        paragraph.Append(Field("NUMPAGES"));
        return new Footer(paragraph);
    }

    private static void AddStyles(MainDocumentPart main)
    {
        var stylesPart = main.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    new RunFonts { Ascii = FontName, HighAnsi = FontName, ComplexScript = FontName, EastAsia = FontName },
                    new FontSize { Val = "20" },
                    new FontSizeComplexScript { Val = "20" },
                    new Languages { Val = "en-GB" })),
                new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                    new SpacingBetweenLines { After = "120", Line = "264", LineRule = LineSpacingRuleValues.Auto }))));

        styles.Append(ParagraphStyle("Normal", "Normal", basedOn: null, isDefault: true));
        styles.Append(ParagraphStyle("Title", "Title", "Normal", sizeHalfPoints: 36, bold: true, before: 0, after: 240, color: "1F3864"));
        styles.Append(ParagraphStyle("Heading1", "heading 1", "Normal", sizeHalfPoints: 28, bold: true, before: 360, after: 120, color: "1F3864", outline: 0));
        styles.Append(ParagraphStyle("Heading2", "heading 2", "Normal", sizeHalfPoints: 24, bold: true, before: 240, after: 80, color: "2F5496", outline: 1));
        styles.Append(ParagraphStyle("Heading3", "heading 3", "Normal", sizeHalfPoints: 22, bold: true, before: 200, after: 60, color: "2F5496", outline: 2));
        styles.Append(ParagraphStyle("ListParagraph", "List Paragraph", "Normal", after: 60));
        styles.Append(ParagraphStyle("TableText", "Table Text", "Normal", sizeHalfPoints: 18, before: 0, after: 0));
        styles.Append(ParagraphStyle("Quote", "Quote", "Normal", italic: true, color: "595959"));
        styles.Append(ParagraphStyle("Footer", "footer", "Normal", sizeHalfPoints: 16, after: 0));
        styles.Append(new Style(
            new StyleName { Val = "Table Grid" },
            new StyleTableProperties(new TableIndentation { Width = 0, Type = TableWidthUnitValues.Dxa }))
        { Type = StyleValues.Table, StyleId = "TableGrid" });

        stylesPart.Styles = styles;
    }

    private static Style ParagraphStyle(
        string id, string name, string? basedOn, bool isDefault = false, int? sizeHalfPoints = null, bool bold = false,
        bool italic = false, int? before = null, int? after = null, string? color = null, int? outline = null)
    {
        var style = new Style(new StyleName { Val = name }) { Type = StyleValues.Paragraph, StyleId = id };
        if (isDefault)
        {
            style.Default = true;
        }

        if (basedOn is not null)
        {
            style.Append(new BasedOn { Val = basedOn });
            style.Append(new NextParagraphStyle { Val = "Normal" });
        }

        style.Append(new PrimaryStyle());

        var pPr = new StyleParagraphProperties();
        if (outline is not null)
        {
            pPr.Append(new KeepNext());
        }

        if (before is not null || after is not null)
        {
            var spacing = new SpacingBetweenLines();
            if (before is not null) { spacing.Before = before.Value.ToString(System.Globalization.CultureInfo.InvariantCulture); }
            if (after is not null) { spacing.After = after.Value.ToString(System.Globalization.CultureInfo.InvariantCulture); }
            pPr.Append(spacing);
        }

        if (outline is not null)
        {
            pPr.Append(new OutlineLevel { Val = outline });
        }

        if (pPr.HasChildren)
        {
            style.Append(pPr);
        }

        var rPr = new StyleRunProperties();
        if (bold)
        {
            rPr.Append(new Bold());
        }

        if (italic)
        {
            rPr.Append(new Italic());
        }

        if (color is not null)
        {
            rPr.Append(new Color { Val = color });
        }

        if (sizeHalfPoints is not null)
        {
            rPr.Append(new FontSize { Val = sizeHalfPoints.ToString() });
        }

        if (rPr.HasChildren)
        {
            style.Append(rPr);
        }

        return style;
    }
}
