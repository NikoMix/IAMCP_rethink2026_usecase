using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProposalGenerator.Schemas.Tests;

/// <summary>Example instances under <c>schemas/examples/</c>.</summary>
internal static class ExampleFiles
{
    public const string ManifestFileName = "expected-errors.json";

    /// <summary>Document type of an example, taken from the file name prefix before the first dot.</summary>
    public static string DocumentTypeOf(string fileName) => fileName[..fileName.IndexOf('.')];

    public static IReadOnlyList<string> ValidFileNames() => JsonFileNames(RepositoryLayout.ValidExamplesDirectory);

    public static IReadOnlyList<string> InvalidFileNames() =>
        JsonFileNames(RepositoryLayout.InvalidExamplesDirectory).Where(name => name != ManifestFileName).ToList();

    public static JsonElement LoadValid(string fileName) => Load(Path.Combine(RepositoryLayout.ValidExamplesDirectory, fileName));

    public static JsonElement LoadInvalid(string fileName) => Load(Path.Combine(RepositoryLayout.InvalidExamplesDirectory, fileName));

    public static JsonObject LoadValidObject(string documentType) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryLayout.ValidExamplesDirectory, documentType + ".example.json")))!.AsObject();

    public static IReadOnlyDictionary<string, ExpectedError> Manifest()
    {
        var path = Path.Combine(RepositoryLayout.InvalidExamplesDirectory, ManifestFileName);
        return JsonSerializer.Deserialize<Dictionary<string, ExpectedError>>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"{ManifestFileName} is empty.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static List<string> JsonFileNames(string directory) =>
        Directory.GetFiles(directory, "*.json")
            .Select(path => Path.GetFileName(path))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    private static JsonElement Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }
}

/// <summary>The intended failure of an invalid example: the field path and the violated keyword.</summary>
internal sealed record ExpectedError(string DocumentType, string Path, string Keyword);
