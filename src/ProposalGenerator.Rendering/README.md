# ProposalGenerator.Rendering

Renders the document templates in `templates/` (SOW, RFI, RFP, MSA, change request) into Word
documents (.docx). Issue #24.

## Pipeline

1. **Load** `templates/<doctype>.md` through an `ITemplateSource` (default: `FileSystemTemplateSource`).
2. **Parse the front matter** (YAML, YamlDotNet): `template_id`, `version`, `required_fields`.
3. **Check required fields.** Every `required_fields` path must be present in the data and must not be
   `null`, empty, whitespace-only or an empty array. Otherwise `RequiredFieldsMissingException` lists
   **all** missing paths with a reason. No document is produced.
4. **Check the template syntax** against the supported subset (see below). Violations throw
   `TemplateSyntaxException`, which lists every violation with its file line and column.
5. **Render** the template body with Scriban in Liquid mode.
6. **Convert** the Markdown via the Markdig AST (pipe tables enabled) to WordprocessingML with the
   Open XML SDK.
7. **Validate** the package with `OpenXmlValidator` (`RenderOptions.ValidateOutput`, on by default).
   If validation reports errors, the renderer throws `DocxValidationException` rather than returning
   a file that Word would have to repair.

The same input always produces byte-identical output. The relationship IDs and ZIP entry timestamps
are fixed.

## Usage

```csharp
IDocumentRenderer renderer = new DocxDocumentRenderer(new FileSystemTemplateSource("templates"));

using var json = JsonDocument.Parse(File.ReadAllText("sow.json"));
RenderedDocument doc = await renderer.RenderDocxAsync(
    DocumentType.Sow, json.RootElement, RenderOptions.Default, cancellationToken);

await using var output = File.Create("sow.docx");
await doc.OpenRead().CopyToAsync(output, cancellationToken);   // or doc.Content (byte[])
```

The data root must be a JSON object. The field names match the template field catalog in
`templates/README.md`.

## Supported template syntax

The renderer uses the interim subset from the shared cross-session contract until the ADR publishes
the final subset:

| Construct | Example |
| --- | --- |
| Output of a dotted path | `{{ customer.name }}` |
| Loop over a list | `{% for d in deliverables %}...{% endfor %}` |
| Condition | `{% if cond %}...{% else %}...{% endif %}` |
| Conditions: a path, a string/number/boolean literal, `==`, `!=`, `and`, `or` | `{% if a and b != "x" %}` |

The renderer rejects filters (`| default`, `| join`, ...), function calls, `elsif`/`elif`,
`forloop`/`loop` variables, `for` parameters such as `limit:`, other operators, assignments, and
every other tag. Replacement patterns:

- `{{ x | default: "n/a" }}` → `{% if x %}{{ x }}{% else %}n/a{% endif %}`
- `{{ list | join: ", " }}` → `{% for i in list %}{{ i }} {% endfor %}`
- `{{ loop.index }}` → a Markdown ordered list (`1. ...`) or remove it

A tag that stands alone on a line is removed together with that line, so a loop inside a table
does not leave blank lines. An absent optional field renders as empty text and counts as false in
a condition. Writing a whole object or list as text throws `TemplateEvaluationException`.

## Markdown conversion

| Markdown | Word |
| --- | --- |
| `#` to `####` (deeper levels use `Heading4`) | Styles `Heading1` to `Heading4` |
| Paragraph, `**bold**`, `*italic*`, `` `code` ``, hard line breaks, links (text only) | `Normal` with run formatting |
| `-` / `*` lists, `1.` lists (nested) | Bullet and decimal numbering definitions; each ordered list restarts at 1 |
| GFM pipe table | `TableGrid` table. The header row is bold and repeats on each page; short rows are padded. |
| `---` (thematic break) | Page break |
| `>` quote | `Quote` style |

A table must be followed by a blank line. Otherwise Markdig reads the whole table as a paragraph,
so the renderer throws `UnsupportedMarkdownException`. Every other construct (HTML, images, code
blocks, ...) also throws `UnsupportedMarkdownException` instead of being dropped silently.

Data values are inserted as literal text. The renderer escapes Markdown characters and replaces
line breaks with spaces, so values cannot change the document structure. Numbers use invariant
formatting.

## Extension points (not implemented here)

| Later issue | Hook |
| --- | --- |
| #25 Corporate design, header/footer, TOC | `IDocxStyleSheet` (styles; must define `DocxStyleIds`), `IDocxPostProcessor` |
| #26 PDF | Consumes `RenderedDocument` (`Content` / `OpenRead()`) |
| #27 Locale formatting | New `RenderOptions` property; number/date output in `ScribanTemplateRenderer` |
| #28 File naming, document properties | `IDocxPostProcessor` with `DocxRenderContext` (template ID and version, data, options); `RenderedDocument` metadata |
| #29 Watermark, version history | `IDocxPostProcessor`, new `RenderOptions` properties |

Post-processors run in order after the body is written and before validation. Add new
`RenderOptions` settings as init-only properties with backward-compatible defaults.

## Tests

```powershell
dotnet test tests/ProposalGenerator.Rendering.Tests/ProposalGenerator.Rendering.Tests.csproj
```

The tests are offline and use the fictional fixtures in `tests/ProposalGenerator.Rendering.Tests/Fixtures/`.
`RepositoryTemplateTests.AwaitingSubsetConformance` lists repository templates that do not conform
to the subset yet. For those templates, the tests assert that the renderer reports the violations.
When a template becomes conformant, the test fails and asks for it to be removed from the set. To
run the tests against another template folder, where every template must conform, set
`PROPOSALGENERATOR_TEMPLATES_DIR`.

## Dependencies and licenses

| Package | Version | License |
| --- | --- | --- |
| Scriban | 7.4.0 | BSD-2-Clause |
| Markdig | 1.3.2 | BSD-2-Clause |
| YamlDotNet | 18.1.0 | MIT |
| DocumentFormat.OpenXml | 3.5.1 | MIT |
