using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProposalGenerator.Schemas.Tests;

/// <summary>
/// Parity between the template front matter and the schemas (issue #7, acceptance criteria 1 and 2).
/// </summary>
public sealed class TemplateParityTests
{
    [Fact]
    public void Templates_CoverEveryDocumentTypeExactlyOnce()
    {
        var templates = TemplateFrontMatter.LoadAll();

        Assert.Equal(
            RepositoryLayout.DocumentTypes.Order(StringComparer.Ordinal),
            templates.Select(t => t.TemplateId).Order(StringComparer.Ordinal));
        Assert.All(templates, t => Assert.Equal(t.TemplateId + ".md", t.FileName));
        Assert.All(templates, t => Assert.NotEmpty(t.RequiredFields));
    }

    [Theory]
    [MemberData(nameof(TemplateFields))]
    public void TemplateField_IsDeclaredInItsSchema(string templateId, string fieldPath, string list)
    {
        var problem = SchemaPropertyWalker.Check(templateId, fieldPath, mustBeRequired: list == "required_fields");

        Assert.True(problem is null, $"{templateId}.md {list} '{fieldPath}': {problem}");
    }

    [Theory]
    [MemberData(nameof(RequiredTemplateFields))]
    public void DataWithoutRequiredField_FailsAndNamesTheFieldPath(string templateId, string fieldPath)
    {
        var document = ExampleFiles.LoadValidObject(templateId);
        RemoveField(document, fieldPath);
        var instance = JsonSerializer.SerializeToElement(document);

        var results = SchemaCatalog.Instance.Evaluate(templateId, instance);
        var errors = FieldErrors.From(results, instance);

        Assert.False(results.IsValid);
        Assert.Equal([(fieldPath, "required")], errors.Select(e => (e.Path, e.Keyword)));
    }

    [Fact]
    public void Walker_RejectsUndeclaredAndNonRequiredPaths()
    {
        Assert.Null(SchemaPropertyWalker.Check("msa", "supplier.signatory.name", mustBeRequired: true));
        Assert.Null(SchemaPropertyWalker.Check("sow", "project.background", mustBeRequired: false));
        Assert.Null(SchemaPropertyWalker.Check("sow", "sow.deliverables.due_date", mustBeRequired: true));

        Assert.Contains("not required", SchemaPropertyWalker.Check("sow", "project.background", mustBeRequired: true));
        Assert.Contains("not a property", SchemaPropertyWalker.Check("sow", "project.budget", mustBeRequired: false));
        Assert.Contains("not a property", SchemaPropertyWalker.Check("rfi", "supplier.name", mustBeRequired: false));
    }

    public static TheoryData<string, string, string> TemplateFields()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var template in TemplateFrontMatter.LoadAll())
        {
            foreach (var field in template.RequiredFields)
            {
                data.Add(template.TemplateId, field, "required_fields");
            }

            foreach (var field in template.OptionalFields)
            {
                data.Add(template.TemplateId, field, "optional_fields");
            }
        }

        return data;
    }

    public static TheoryData<string, string> RequiredTemplateFields()
    {
        var data = new TheoryData<string, string>();
        foreach (var template in TemplateFrontMatter.LoadAll())
        {
            foreach (var field in template.RequiredFields)
            {
                data.Add(template.TemplateId, field);
            }
        }

        return data;
    }

    private static void RemoveField(JsonObject document, string fieldPath)
    {
        var segments = fieldPath.Split('.');
        JsonNode? parent = document;
        foreach (var segment in segments[..^1])
        {
            parent = parent?[segment];
        }

        var container = parent as JsonObject
            ?? throw new InvalidOperationException($"The valid example has no object at the parent of '{fieldPath}'.");
        Assert.True(container.Remove(segments[^1]), $"The valid example does not contain '{fieldPath}', so removing it proves nothing.");
    }
}
