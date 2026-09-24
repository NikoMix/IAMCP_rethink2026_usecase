namespace ProposalGenerator.Schemas.Tests;

public sealed class SchemaCatalogTests
{
    [Fact]
    public void Catalog_ContainsCommonAndExactlyOneSchemaPerDocumentType()
    {
        var expectedFiles = RepositoryLayout.DocumentTypes.Select(t => t + ".schema.json")
            .Append(SchemaCatalog.CommonFileName)
            .Order(StringComparer.Ordinal);

        Assert.Equal(expectedFiles, SchemaCatalog.Instance.FileNames.Order(StringComparer.Ordinal));
        Assert.Equal(RepositoryLayout.DocumentTypes.Order(StringComparer.Ordinal), SchemaCatalog.Instance.DocumentTypes.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(SchemaFileNames))]
    public void Schema_UsesDraft2020_12AndAnIdEndingInItsFileName(string fileName)
    {
        var raw = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryLayout.SchemasDirectory, fileName)))!;

        Assert.Equal("https://json-schema.org/draft/2020-12/schema", raw["$schema"]!.GetValue<string>());
        Assert.EndsWith("/schemas/" + fileName, SchemaCatalog.Instance.IdOf(fileName).AbsoluteUri, StringComparison.Ordinal);
    }

    public static TheoryData<string> SchemaFileNames()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(RepositoryLayout.SchemasDirectory, "*.schema.json").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(path));
        }

        return data;
    }
}
