using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ProposalGenerator.Rendering.Docx;

/// <summary>Style IDs the DOCX writer references. Every <see cref="IDocxStyleSheet"/> must define them.</summary>
public static class DocxStyleIds
{
    /// <summary>Body text paragraphs.</summary>
    public const string Normal = "Normal";

    /// <summary>Heading levels 1 to 4. Markdown levels 5 and 6 use <see cref="Heading4"/>.</summary>
    public const string Heading1 = "Heading1";

    /// <inheritdoc cref="Heading1"/>
    public const string Heading2 = "Heading2";

    /// <inheritdoc cref="Heading1"/>
    public const string Heading3 = "Heading3";

    /// <inheritdoc cref="Heading1"/>
    public const string Heading4 = "Heading4";

    /// <summary>Bulleted and numbered list items.</summary>
    public const string ListParagraph = "ListParagraph";

    /// <summary>The table style applied to every table.</summary>
    public const string TableGrid = "TableGrid";

    /// <summary>Paragraphs inside a Markdown block quote, such as template guidance notes.</summary>
    public const string Quote = "Quote";

    /// <summary>Returns the heading style for a Markdown heading level.</summary>
    public static string Heading(int level) => level switch
    {
        <= 1 => Heading1,
        2 => Heading2,
        3 => Heading3,
        _ => Heading4,
    };
}

/// <summary>
/// Supplies the style definitions of the generated document. Replace the default
/// <see cref="DefaultDocxStyleSheet"/> to apply a corporate design (#25).
/// </summary>
public interface IDocxStyleSheet
{
    /// <summary>Creates the styles that every ID in <see cref="DocxStyleIds"/> refers to.</summary>
    Styles CreateStyles();
}

/// <summary>A neutral style sheet with Word's built-in style names and conservative formatting.</summary>
public sealed class DefaultDocxStyleSheet : IDocxStyleSheet
{
    /// <summary>The shared instance.</summary>
    public static DefaultDocxStyleSheet Instance { get; } = new();

    /// <inheritdoc />
    public Styles CreateStyles()
    {
        var styles = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri", ComplexScript = "Calibri", EastAsia = "Calibri" },
                    new FontSize { Val = "22" },
                    new FontSizeComplexScript { Val = "22" })),
                new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                    new SpacingBetweenLines { After = "120", Line = "264", LineRule = LineSpacingRuleValues.Auto }))));

        styles.Append(new Style(
            new StyleName { Val = "Normal" },
            new PrimaryStyle())
        {
            Type = StyleValues.Paragraph,
            StyleId = DocxStyleIds.Normal,
            Default = true,
        });

        styles.Append(HeadingStyle(DocxStyleIds.Heading1, "heading 1", 0, sizeHalfPoints: 32, spaceBefore: 360));
        styles.Append(HeadingStyle(DocxStyleIds.Heading2, "heading 2", 1, sizeHalfPoints: 28, spaceBefore: 240));
        styles.Append(HeadingStyle(DocxStyleIds.Heading3, "heading 3", 2, sizeHalfPoints: 24, spaceBefore: 200));
        styles.Append(HeadingStyle(DocxStyleIds.Heading4, "heading 4", 3, sizeHalfPoints: 22, spaceBefore: 200));

        styles.Append(new Style(
            new StyleName { Val = "List Paragraph" },
            new BasedOn { Val = DocxStyleIds.Normal },
            new UIPriority { Val = 34 },
            new PrimaryStyle(),
            new StyleParagraphProperties(
                new SpacingBetweenLines { After = "60" },
                new Indentation { Left = "720" },
                new ContextualSpacing()))
        {
            Type = StyleValues.Paragraph,
            StyleId = DocxStyleIds.ListParagraph,
        });

        styles.Append(new Style(
            new StyleName { Val = "Quote" },
            new BasedOn { Val = DocxStyleIds.Normal },
            new NextParagraphStyle { Val = DocxStyleIds.Normal },
            new UIPriority { Val = 29 },
            new PrimaryStyle(),
            new StyleParagraphProperties(new Indentation { Left = "720", Right = "720" }),
            new StyleRunProperties(new Italic(), new ItalicComplexScript()))
        {
            Type = StyleValues.Paragraph,
            StyleId = DocxStyleIds.Quote,
        });

        styles.Append(new Style(
            new StyleName { Val = "Table Grid" },
            new BasedOn { Val = "TableNormal" },
            new UIPriority { Val = 39 },
            new StyleParagraphProperties(new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }),
            new StyleTableProperties(
                new TableIndentation { Width = 0, Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                    new LeftBorder { Val = BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                    new BottomBorder { Val = BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                    new RightBorder { Val = BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Space = 0, Color = "auto" }),
                new TableCellMarginDefault(
                    new TopMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                    new TableCellLeftMargin { Width = 108, Type = TableWidthValues.Dxa },
                    new BottomMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                    new TableCellRightMargin { Width = 108, Type = TableWidthValues.Dxa })))
        {
            Type = StyleValues.Table,
            StyleId = DocxStyleIds.TableGrid,
        });

        styles.Append(new Style(
            new StyleName { Val = "Normal Table" },
            new UIPriority { Val = 99 },
            new SemiHidden(),
            new UnhideWhenUsed(),
            new StyleTableProperties(
                new TableIndentation { Width = 0, Type = TableWidthUnitValues.Dxa },
                new TableCellMarginDefault(
                    new TopMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                    new TableCellLeftMargin { Width = 108, Type = TableWidthValues.Dxa },
                    new BottomMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                    new TableCellRightMargin { Width = 108, Type = TableWidthValues.Dxa })))
        {
            Type = StyleValues.Table,
            StyleId = "TableNormal",
            Default = true,
        });

        return styles;
    }

    private static Style HeadingStyle(string styleId, string name, int outlineLevel, int sizeHalfPoints, int spaceBefore) =>
        new(
            new StyleName { Val = name },
            new BasedOn { Val = DocxStyleIds.Normal },
            new NextParagraphStyle { Val = DocxStyleIds.Normal },
            new UIPriority { Val = 9 },
            new PrimaryStyle(),
            new StyleParagraphProperties(
                new KeepNext(),
                new KeepLines(),
                new SpacingBetweenLines { Before = spaceBefore.ToString(System.Globalization.CultureInfo.InvariantCulture), After = "120" },
                new OutlineLevel { Val = outlineLevel }),
            new StyleRunProperties(
                new Bold(),
                new BoldComplexScript(),
                new FontSize { Val = sizeHalfPoints.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                new FontSizeComplexScript { Val = sizeHalfPoints.ToString(System.Globalization.CultureInfo.InvariantCulture) }))
        {
            Type = StyleValues.Paragraph,
            StyleId = styleId,
        };
}

/// <summary>Context passed to <see cref="IDocxPostProcessor"/> implementations.</summary>
/// <param name="DocumentType">The rendered document type.</param>
/// <param name="TemplateId">The template's <c>template_id</c>.</param>
/// <param name="TemplateVersion">The template's <c>version</c>.</param>
/// <param name="Data">The document data.</param>
/// <param name="Options">The options of the render call.</param>
public sealed record DocxRenderContext(
    DocumentType DocumentType,
    string TemplateId,
    string TemplateVersion,
    System.Text.Json.JsonElement Data,
    RenderOptions Options);

/// <summary>
/// Adjusts the generated package before it is saved. This is the extension point for header,
/// footer and table of contents (#25), document properties (#28), and watermarks and version
/// history (#29). Post-processors run in registration order, before output validation.
/// </summary>
public interface IDocxPostProcessor
{
    /// <summary>Modifies <paramref name="document"/>.</summary>
    void Process(WordprocessingDocument document, DocxRenderContext context);
}
