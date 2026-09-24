using YamlDotNet.RepresentationModel;

namespace ProposalGenerator.Schemas.Tests;

/// <summary>The YAML front matter of a template in <c>templates/*.md</c>.</summary>
internal sealed record TemplateFrontMatter(
    string FileName,
    string TemplateId,
    IReadOnlyList<string> RequiredFields,
    IReadOnlyList<string> OptionalFields)
{
    public static IReadOnlyList<TemplateFrontMatter> LoadAll() =>
        Directory.GetFiles(RepositoryLayout.TemplatesDirectory, "*.md")
            .Where(path => !string.Equals(Path.GetFileName(path), "README.md", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(Load)
            .ToList();

    public static TemplateFrontMatter Load(string path)
    {
        var fileName = Path.GetFileName(path);
        var lines = File.ReadAllText(path).ReplaceLineEndings("\n").Split('\n');
        if (lines.Length == 0 || lines[0] != "---")
        {
            throw new InvalidDataException($"{fileName} does not start with a '---' front matter line.");
        }

        var end = Array.IndexOf(lines, "---", 1);
        if (end < 0)
        {
            throw new InvalidDataException($"{fileName} has no closing '---' front matter line.");
        }

        var yaml = new YamlStream();
        using (var reader = new StringReader(string.Join('\n', lines[1..end])))
        {
            yaml.Load(reader);
        }

        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        var templateId = root.Children.TryGetValue(new YamlScalarNode("template_id"), out var id)
            ? ((YamlScalarNode)id).Value ?? string.Empty
            : throw new InvalidDataException($"{fileName} has no template_id.");

        return new TemplateFrontMatter(
            fileName,
            templateId,
            ReadList(root, "required_fields", fileName, mandatory: true),
            ReadList(root, "optional_fields", fileName, mandatory: false));
    }

    private static List<string> ReadList(YamlMappingNode root, string key, string fileName, bool mandatory)
    {
        if (!root.Children.TryGetValue(new YamlScalarNode(key), out var node))
        {
            return mandatory
                ? throw new InvalidDataException($"{fileName} has no {key}.")
                : [];
        }

        if (node is not YamlSequenceNode sequence)
        {
            throw new InvalidDataException($"{fileName}: {key} must be a YAML list.");
        }

        return sequence.Children
            .Select(child => child is YamlScalarNode { Value: { Length: > 0 } value }
                ? value.Trim()
                : throw new InvalidDataException($"{fileName}: every {key} entry must be a non-empty string."))
            .ToList();
    }
}
