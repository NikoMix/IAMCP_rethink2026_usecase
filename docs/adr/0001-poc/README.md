# ADR 0001 proof of concept

This is a standalone console project that provides the evidence for [ADR 0001](../0001-architecture.md). It:

- lints the five templates in `templates/` against the template syntax subset;
- renders the SOW template with fictional sample data: Scriban (Liquid mode) → Markdown → Markdig AST → Open XML SDK → DOCX;
- converts the DOCX to PDF with LibreOffice headless.

The project is not part of any solution and is not production code. The production renderer is `src/ProposalGenerator.Rendering/` (issue #24). It must implement the renderer contract that `SubsetRenderer.cs` demonstrates.

## Prerequisites

- A .NET SDK that can build `net9.0`. The project sets `RollForward=Major`, so it also runs on a .NET 10 runtime when no .NET 9 runtime is installed. See ADR risk R1.
- For PDF: LibreOffice (`soffice`) on `PATH`, in `SOFFICE_PATH` or passed with `--soffice`, or one of the container commands below.

## Commands

Run these from `docs/adr/0001-poc`:

```powershell
dotnet build
dotnet run --no-build -- selfcheck                     # renderer and lint rules, with negative controls
dotnet run --no-build -- lint ../../.. --details        # all templates: incompatibilities and conformance of the adapted templates
dotnet run --no-build -- render ../../.. out            # out/sow.adapted.md, out/sow.md, out/sow.docx (+ out/sow.pdf when soffice is found)
dotnet run --no-build -- render ../../.. out --require-pdf   # exit 2 when soffice is missing
```

Exit codes:

| Command | 0 | 1 | 2 |
| --- | --- | --- | --- |
| `selfcheck` | all checks pass | a check or negative control failed | - |
| `lint` | all adapted templates conform | an adapted template has a violation, a parse error, a remaining Jinja construct, or a failed strict render | - |
| `render` | DOCX written and valid; PDF written, or skipped without `--require-pdf` | a render error or an Open XML validation error | `--require-pdf` was set and soffice was not found |

`out/` is ignored by Git.

## Files

| File | Purpose |
| --- | --- |
| `TemplateSource.cs` | Splits the YAML front matter (`title`, `required_fields`) from the template body. |
| `SyntaxLint.cs` | Incompatibility rules INC-01..INC-10 and the subset grammar. |
| `JinjaToSubset.cs` | The mechanical migration: `loop.` → `forloop.`, `default(x)` → `default: x`, `join(x)` → `join: x`, `elif` → `elsif`. |
| `SubsetRenderer.cs` | Reference renderer contract: the standalone-tag rule, presence semantics, Markdown escaping of values, strict variables, required fields, and guidance stripping. |
| `DocxWriter.cs` | Markdig AST → Open XML: A4 pages, styles, numbering, tables with a repeating header row, and a "Page X of Y" footer. |
| `PdfConverter.cs` | LibreOffice headless conversion with a private profile and a timeout. |
| `SelfCheck.cs` | Executable evidence for the rules. |
| `sample-data/sow.sample.json` | Fictional SOW data (Contoso / Fabrikam, reserved `.example` e-mail domains). It deliberately contains `\|`, `*…*`, `_` and a missing role name. |

## PDF with LibreOffice

The command the PoC runs:

```text
soffice -env:UserInstallation=file:///<temp-profile-dir> --headless --norestore --convert-to pdf --outdir <outDir> <outDir>/sow.docx
```

The following container variants were **not executed** in this environment; see "Results". They are the commands to use where Docker is available.

A plain LibreOffice image, with Liberation fonts (metric-compatible with Arial, Times New Roman and Courier New):

```dockerfile
FROM debian:bookworm-slim
RUN apt-get update \
 && apt-get install -y --no-install-recommends libreoffice-writer-nogui fonts-liberation \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /work
ENTRYPOINT ["soffice", "--headless", "--norestore", "--convert-to", "pdf", "--outdir", "/work"]
```

Save the Dockerfile above as `Dockerfile.soffice`, then run:

```powershell
docker build -t adr0001-soffice -f Dockerfile.soffice .
docker run --rm -v "${PWD}/out:/work" adr0001-soffice /work/sow.docx
```

Gotenberg (an HTTP API around LibreOffice):

```powershell
docker run --rm -p 3000:3000 gotenberg/gotenberg:8
curl --request POST http://localhost:3000/forms/libreoffice/convert --form files=@out/sow.docx -o out/sow.pdf
```

## Results (2026-09-24, Windows, .NET SDK 10.0.400, running on the .NET 10 runtime via RollForward)

| Step | Command | Exit | Observed |
| --- | --- | ---: | --- |
| Build | `dotnet build -nologo -v q` | 0 | 0 warnings, 0 errors (`TreatWarningsAsErrors`) |
| Self-check | `dotnet run --no-build -- selfcheck` | 0 | `checks=10 negative-controls=8 failures=0`; `lint rules=11 failures=0`; the subset grammar sample shows 7 violations for Jinja and 0 for the subset |
| Lint | `dotnet run --no-build -- lint ../../.. --details` | 0 | 5 templates discovered. Raw subset violations: CR 4, MSA 6, RFI 7, RFP 5, SOW 9. Adapted: 0 violations, 0 parse errors, 0 Jinja constructs remaining, strict render ok |
| Lint negative control | the same lint against a temporary copy of `templates/`, where `sow.md` uses `{{ project.name \| upcase }}` | 1 | `sow ... adapted: subset-violations=1`, `lint: 1 template(s) do not conform after adaptation` |
| DOCX | `dotnet run --no-build -- render ../../.. out` | 0 | Markdown AST: 23 headings, 6 tables (rows incl. header 4,3,3,3,2,6), 8 lists. DOCX: 135 paragraphs, 6 tables, **0 Open XML validation errors** (Office2019). The first run reported 8 errors (table border order; empty `w:before`). They were fixed, which shows that the validator gate can fail. |
| PDF | same command | 0 (2 with `--require-pdf`) | **Blocked.** `Get-Command soffice` found nothing, and `C:\Program Files\LibreOffice\program\soffice.exe` does not exist. `docker --version` fails ("The term 'docker' is not recognized"), and `wsl --status` reports "The Windows Subsystem for Linux is not installed". Installing system software on this shared machine was out of scope. |

Layout fidelity was observed in Microsoft Word, as a reference for the DOCX. The pages were rendered through Word COM (`Pages(i).EnhMetaFileBits`). This is not the LibreOffice PDF:

- 3 A4 pages. All text is Arial. The six tables have 5/3/4/4/3/2 columns. Every table's header row has `HeadingFormat = true`, and the header row visibly repeats when the Deliverables table breaks onto page 2.
- Values that contain `|` and `*monitoring*` render literally inside their cell. No extra column appears and no text turns bold. `SAP_QA` keeps its underscore.
- Ordered lists restart at 1 for each list (section 1 objectives, section 9 acceptance steps).
- The empty role name renders as `TBD` (`default: "TBD"`).
- **Finding F1, column widths:** columns are equal width (fixed layout with no per-column widths). The narrow `ID` column in the Deliverables table gets 20 % of the width. The renderer (#24) should derive widths from the content or from a per-template hint.
- **Finding F2, field cache:** the footer's `NUMPAGES` field is written with the cached value `1`. Before repagination, Word showed "Page 1 of 2" on page 1; after `Repaginate()` it showed "Page 1 of 3". PDF converters recompute fields, but a DOCX opened without repagination can show a stale total.
- **Finding F3, raw codes:** `pricing.model` renders the raw enum value `capped_tm`. Templates should map codes to labels with `if`/`elsif` (owner #8/#12).
- **Finding F4, numbers:** amounts render verbatim (`200000`, `EUR 184000`). There is no thousands separator. See ADR open question Q5.

The expected LibreOffice fidelity risk is font substitution. The DOCX names Arial, and the container must install `fonts-liberation` so Liberation Sans keeps the line breaks and page count. This could not be measured here. Measuring it (page count and a visual diff against the Word reference) is the first acceptance check for the PDF service.
