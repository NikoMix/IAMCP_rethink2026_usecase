using System.IO.Compression;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ProposalGenerator.Rendering.Docx;

/// <summary>Assembles, post-processes, validates and normalises a DOCX package.</summary>
internal static class DocxPackageBuilder
{
    /// <summary>The Office version whose schema the output is validated against.</summary>
    public const FileFormatVersions ValidationTarget = FileFormatVersions.Office2016;

    private static readonly DateTimeOffset FixedEntryTimestamp = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static byte[] Build(
        string markdown,
        IDocxStyleSheet styleSheet,
        IReadOnlyList<IDocxPostProcessor> postProcessors,
        DocxRenderContext context,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        using (var document = WordprocessingDocument.Create(buffer, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();

            // The SDK generates a random relationship ID for the main part; a fixed one keeps the output deterministic.
            document.ChangeIdOfPart(mainPart, "rIdMainDocument");

            var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>("rIdStyles");
            stylesPart.Styles = styleSheet.CreateStyles();

            var settingsPart = mainPart.AddNewPart<DocumentSettingsPart>("rIdSettings");
            settingsPart.Settings = new Settings(
                new Compatibility(new CompatibilitySetting
                {
                    Name = CompatSettingNameValues.CompatibilityMode,
                    Uri = "http://schemas.microsoft.com/office/word",
                    Val = "15",
                }));

            MarkdownDocxWriter.Write(markdown, mainPart);
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var processor in postProcessors)
            {
                processor.Process(document, context);
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (context.Options.ValidateOutput)
            {
                var errors = Validate(document);
                if (errors.Count > 0)
                {
                    throw new DocxValidationException(errors);
                }
            }
        }

        return Normalize(buffer.ToArray());
    }

    /// <summary>Returns every Open XML validation error as <c>part path: description</c>.</summary>
    public static IReadOnlyList<string> Validate(WordprocessingDocument document) =>
        new OpenXmlValidator(ValidationTarget)
            .Validate(document)
            .Select(e => $"{e.Part?.Uri}{e.Path?.XPath}: {e.Description}")
            .ToArray();

    /// <summary>
    /// Rewrites the ZIP container with a fixed timestamp on every entry, so identical input
    /// produces byte-identical output (needed for golden-file tests, #38).
    /// </summary>
    private static byte[] Normalize(byte[] package)
    {
        using var source = new ZipArchive(new MemoryStream(package, writable: false), ZipArchiveMode.Read);
        using var output = new MemoryStream();
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                var copy = target.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                copy.LastWriteTime = FixedEntryTimestamp;
                using var from = entry.Open();
                using var to = copy.Open();
                from.CopyTo(to);
            }
        }

        return output.ToArray();
    }
}
