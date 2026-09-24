namespace ProposalGenerator.Schemas.Tests;

public sealed class ExampleValidationTests
{
    [Fact]
    public void ValidExamples_CoverEveryDocumentType()
    {
        var covered = ExampleFiles.ValidFileNames().Select(ExampleFiles.DocumentTypeOf).Distinct();

        Assert.Equal(RepositoryLayout.DocumentTypes.Order(StringComparer.Ordinal), covered.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(ValidExamples))]
    public void ValidExample_Validates(string fileName)
    {
        var instance = ExampleFiles.LoadValid(fileName);

        var results = SchemaCatalog.Instance.Evaluate(ExampleFiles.DocumentTypeOf(fileName), instance);

        Assert.True(results.IsValid, string.Join(Environment.NewLine, FieldErrors.From(results, instance)));
    }

    [Fact]
    public void InvalidExamples_MatchTheManifestAndCoverEveryDocumentType()
    {
        var manifest = ExampleFiles.Manifest();

        Assert.Equal(ExampleFiles.InvalidFileNames(), manifest.Keys.Order(StringComparer.Ordinal));
        Assert.All(manifest, entry => Assert.Equal(ExampleFiles.DocumentTypeOf(entry.Key), entry.Value.DocumentType));
        Assert.Equal(
            RepositoryLayout.DocumentTypes.Order(StringComparer.Ordinal),
            manifest.Values.Select(e => e.DocumentType).Distinct().Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(InvalidExamples))]
    public void InvalidExample_FailsOnlyForItsIntendedReason(string fileName)
    {
        var expected = ExampleFiles.Manifest()[fileName];
        var instance = ExampleFiles.LoadInvalid(fileName);

        var results = SchemaCatalog.Instance.Evaluate(expected.DocumentType, instance);
        var errors = FieldErrors.From(results, instance);

        Assert.False(results.IsValid);
        Assert.Equal([expected.Path], errors.Select(e => e.Path).Distinct());
        Assert.Contains(errors, e => e.Keyword == expected.Keyword);
    }

    public static TheoryData<string> ValidExamples() => new(ExampleFiles.ValidFileNames());

    public static TheoryData<string> InvalidExamples() => new(ExampleFiles.InvalidFileNames());
}
