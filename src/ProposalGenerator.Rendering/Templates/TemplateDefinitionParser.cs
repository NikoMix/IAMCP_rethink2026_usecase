using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ProposalGenerator.Rendering.Templates;

/// <summary>A parsed template: its front matter metadata and its body.</summary>
internal sealed record TemplateDefinition(
    DocumentType DocumentType,
    string SourceName,
    string TemplateId,
    string Title,
    string Version,
    IReadOnlyList<string> RequiredFields,
    string Body,
    int BodyStartLine);

/// <summary>Splits a template file into YAML front matter and body, and validates the front matter.</summary>
internal static class TemplateDefinitionParser
{
    private const string Delimiter = "---";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static TemplateDefinition Parse(TemplateSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lines = source.Content.Split('\n');
        if (lines.Length == 0 || lines[0].TrimEnd('\r') != Delimiter)
        {
            throw new TemplateDefinitionException(source.SourceName, "The file must start with a '---' YAML front matter line.");
        }

        var closing = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd('\r') == Delimiter)
            {
                closing = i;
                break;
            }
        }

        if (closing < 0)
        {
            throw new TemplateDefinitionException(source.SourceName, "The YAML front matter has no closing '---' line.");
        }

        var yaml = string.Join('\n', lines[1..closing]);
        FrontMatter? frontMatter;
        try
        {
            frontMatter = Deserializer.Deserialize<FrontMatter?>(yaml);
        }
        catch (YamlException ex)
        {
            throw new TemplateDefinitionException(source.SourceName, $"The YAML front matter is invalid: {ex.Message}", ex);
        }

        if (frontMatter is null)
        {
            throw new TemplateDefinitionException(source.SourceName, "The YAML front matter is empty.");
        }

        var expectedId = source.DocumentType.ToTemplateId();
        if (!string.Equals(frontMatter.TemplateId, expectedId, StringComparison.Ordinal))
        {
            throw new TemplateDefinitionException(
                source.SourceName,
                $"template_id is '{frontMatter.TemplateId}', expected '{expectedId}'.");
        }

        if (string.IsNullOrWhiteSpace(frontMatter.Version))
        {
            throw new TemplateDefinitionException(source.SourceName, "version is missing.");
        }

        if (frontMatter.RequiredFields is null)
        {
            throw new TemplateDefinitionException(source.SourceName, "required_fields is missing.");
        }

        var invalid = frontMatter.RequiredFields
            .Where(p => string.IsNullOrWhiteSpace(p) || p.Split('.').Any(s => s.Length == 0 || s.Trim() != s))
            .ToArray();
        if (invalid.Length > 0)
        {
            throw new TemplateDefinitionException(
                source.SourceName,
                "required_fields contains invalid paths: " + string.Join(", ", invalid.Select(p => $"'{p}'")) + ".");
        }

        var body = string.Join('\n', lines[(closing + 1)..]);
        return new TemplateDefinition(
            source.DocumentType,
            source.SourceName,
            frontMatter.TemplateId,
            frontMatter.Title ?? frontMatter.TemplateId,
            frontMatter.Version,
            frontMatter.RequiredFields,
            body,
            BodyStartLine: closing + 2);
    }

    private sealed class FrontMatter
    {
        public string TemplateId { get; set; } = string.Empty;

        public string? Title { get; set; }

        public string Version { get; set; } = string.Empty;

        public List<string>? RequiredFields { get; set; }
    }
}
