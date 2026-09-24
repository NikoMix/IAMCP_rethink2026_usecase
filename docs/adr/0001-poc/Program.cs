using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Markdig;
using Markdig.Syntax;
using Scriban;

namespace Adr0001.Poc;

public static class Program
{
    private const string Usage = """
        Usage:
          dotnet run -- lint <repoRoot> [--details]
          dotnet run -- selfcheck
          dotnet run -- render <repoRoot> <outDir> [--data <sample.json>] [--soffice <path>] [--require-pdf]
        """;

    public static int Main(string[] args)
    {
        try
        {
            return args.FirstOrDefault() switch
            {
                "lint" when args.Length >= 2 => Lint(args[1], args.Contains("--details")),
                "selfcheck" => SelfCheck.Run(),
                "render" when args.Length >= 3 => Render(args[1], args[2], Option(args, "--data"), Option(args, "--soffice"), args.Contains("--require-pdf")),
                _ => Fail(Usage),
            };
        }
        catch (TemplateRenderException ex)
        {
            return Fail(ex.Message);
        }
    }

    private static int Lint(string repoRoot, bool details)
    {
        var files = Directory.GetFiles(Path.Combine(repoRoot, "templates"), "*.md")
            .Where(f => !Path.GetFileName(f).Equals("README.md", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
        Console.WriteLine($"templates discovered: {files.Count}");

        var failures = 0;
        var rules = new SortedDictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var template = TemplateSource.Load(file);
            var rawParse = Template.ParseLiquid(template.Body);
            var incompatibilities = SyntaxLint.FindIncompatibilities(template);
            var rawViolations = SyntaxLint.FindSubsetViolations(template);

            var migrated = template with { Body = JinjaToSubset.Migrate(template.Body) };
            var migratedParse = Template.ParseLiquid(SubsetRenderer.ApplyStandaloneTagRule(migrated.Body));
            var migratedViolations = SyntaxLint.FindSubsetViolations(migrated);
            var remaining = SyntaxLint.FindIncompatibilities(migrated)
                .Where(f => f.Rule is SyntaxLint.LoopVariable or SyntaxLint.FilterCall or SyntaxLint.Elif or SyntaxLint.JinjaComment or SyntaxLint.JinjaOnly or SyntaxLint.WhitespaceMarker)
                .ToList();

            // Strict render with no data proves every top-level name is known and every tag evaluates.
            string? strictError = null;
            try
            {
                SubsetRenderer.Render(migrated, JsonDocument.Parse("{}").RootElement, new RenderOptions { CheckRequiredFields = false });
            }
            catch (TemplateRenderException ex)
            {
                strictError = ex.Message;
            }

            Console.WriteLine(
                $"{template.Name,-15} raw: scriban-parse-errors={rawParse.Messages.Count} subset-violations={rawViolations.Count} | " +
                $"adapted: scriban-parse-errors={migratedParse.Messages.Count} subset-violations={migratedViolations.Count} " +
                $"jinja-remaining={remaining.Count} strict-render={(strictError is null ? "ok" : "FAILED")} required-fields={template.RequiredFields.Count}");
            if (strictError is not null)
            {
                Console.WriteLine("    " + strictError);
            }

            foreach (var group in incompatibilities.GroupBy(f => f.Rule))
            {
                if (!rules.TryGetValue(group.Key, out var perTemplate))
                {
                    perTemplate = [];
                    rules[group.Key] = perTemplate;
                }

                perTemplate[template.Name] = group.Count();
            }

            if (details)
            {
                foreach (var finding in incompatibilities.Where(f => f.Rule != SyntaxLint.UnescapedOutput))
                {
                    Console.WriteLine($"    {template.Name}.md:{finding.Line}: {finding.Rule}: {finding.Snippet}");
                }

                foreach (var finding in migratedViolations)
                {
                    Console.WriteLine($"    ADAPTED {template.Name}.md:{finding.Line}: {finding.Rule}: {finding.Snippet}");
                }
            }

            if (migratedParse.HasErrors || migratedViolations.Count > 0 || remaining.Count > 0 || strictError is not null)
            {
                failures++;
            }
        }

        var names = files.Select(Path.GetFileNameWithoutExtension).ToList();
        Console.WriteLine();
        Console.WriteLine("| Rule | " + string.Join(" | ", names) + " | Total |");
        Console.WriteLine("| --- |" + string.Concat(names.Select(_ => " ---: |")) + " ---: |");
        foreach (var (rule, perTemplate) in rules)
        {
            var counts = names.Select(n => perTemplate.GetValueOrDefault(n!)).ToList();
            Console.WriteLine($"| {rule} | {string.Join(" | ", counts)} | {counts.Sum()} |");
        }

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "lint: all adapted templates conform to the subset" : $"lint: {failures} template(s) do not conform after adaptation");
        return failures == 0 ? 0 : 1;
    }

    private static int Render(string repoRoot, string outDir, string? dataPath, string? sofficePath, bool requirePdf)
    {
        Directory.CreateDirectory(outDir);
        outDir = Path.GetFullPath(outDir);
        dataPath ??= Path.Combine(AppContext.BaseDirectory, "sample-data", "sow.sample.json");

        var template = TemplateSource.Load(Path.Combine(repoRoot, "templates", "sow.md"));
        var adapted = template with { Body = JinjaToSubset.Migrate(template.Body) };
        File.WriteAllText(Path.Combine(outDir, "sow.adapted.md"), adapted.Body);

        var violations = SyntaxLint.FindSubsetViolations(adapted);
        if (violations.Count > 0)
        {
            return Fail($"adapted template violates the subset: {string.Join("; ", violations.Select(v => $"{v.Line}: {v.Snippet}"))}");
        }

        using var data = JsonDocument.Parse(File.ReadAllText(dataPath));
        var markdown = SubsetRenderer.Render(adapted, data.RootElement);
        var mdPath = Path.Combine(outDir, "sow.md");
        File.WriteAllText(mdPath, markdown);
        Console.WriteLine($"markdown: {mdPath} ({markdown.Length} chars)");

        var ast = Markdown.Parse(markdown, DocxWriter.Pipeline);
        var tables = ast.Descendants<Markdig.Extensions.Tables.Table>().ToList();
        Console.WriteLine($"markdown AST: headings={ast.Descendants<Markdig.Syntax.HeadingBlock>().Count()} tables={tables.Count} " +
                          $"table-rows={string.Join(",", tables.Select(t => t.Count))} lists={ast.Descendants<Markdig.Syntax.ListBlock>().Count()}");

        var docxPath = Path.Combine(outDir, "sow.docx");
        DocxWriter.Write(markdown, template.Title, docxPath);
        using (var doc = WordprocessingDocument.Open(docxPath, false))
        {
            var errors = new OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2019).Validate(doc).ToList();
            var body = doc.MainDocumentPart!.Document!.Body!;
            Console.WriteLine($"docx: {docxPath} ({new FileInfo(docxPath).Length} bytes) paragraphs={body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().Count()} " +
                              $"tables={body.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>().Count()} open-xml-validation-errors={errors.Count}");
            foreach (var error in errors.Take(20))
            {
                Console.WriteLine($"    {error.Path?.XPath}: {error.Description}");
            }

            if (errors.Count > 0)
            {
                return 1;
            }
        }

        var soffice = PdfConverter.FindSoffice(sofficePath);
        if (soffice is null)
        {
            Console.WriteLine("pdf: SKIPPED - LibreOffice (soffice) not found. Set SOFFICE_PATH or pass --soffice.");
            Console.WriteLine("     command that would run: " + PdfConverter.Command("soffice", docxPath, outDir));
            return requirePdf ? 2 : 0;
        }

        Console.WriteLine("pdf: running " + PdfConverter.Command(soffice, docxPath, outDir));
        var pdf = PdfConverter.Convert(soffice, docxPath, outDir, TimeSpan.FromMinutes(2));
        Console.WriteLine($"pdf: {pdf} ({new FileInfo(pdf).Length} bytes)");
        return 0;
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}
