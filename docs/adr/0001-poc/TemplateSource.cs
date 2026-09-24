namespace Adr0001.Poc;

/// <summary>A template file split into YAML front matter and Liquid body.</summary>
public sealed record TemplateSource(string Name, string Title, IReadOnlyList<string> RequiredFields, string Body, int BodyStartLine)
{
    public static TemplateSource Load(string path)
    {
        var text = File.ReadAllText(path).Replace("\r\n", "\n");
        var name = Path.GetFileNameWithoutExtension(path);
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            return new TemplateSource(name, name, [], text, 1);
        }

        var end = text.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidDataException($"{path}: front matter is not closed with '---'.");
        }

        var frontMatter = text[4..end];
        var body = text[(end + 5)..];
        // Line 1 is the opening marker; the closing marker follows the last front matter line.
        var bodyStartLine = frontMatter.Count(c => c == '\n') + 4;

        var title = name;
        var required = new List<string>();
        var inRequired = false;
        foreach (var raw in frontMatter.Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("title:", StringComparison.Ordinal))
            {
                title = line["title:".Length..].Trim();
            }

            if (line.StartsWith("required_fields:", StringComparison.Ordinal))
            {
                inRequired = true;
                continue;
            }

            if (inRequired && line.TrimStart().StartsWith("- ", StringComparison.Ordinal))
            {
                required.Add(line.TrimStart()[2..].Trim());
                continue;
            }

            if (inRequired && line.Length > 0 && !char.IsWhiteSpace(line[0]))
            {
                inRequired = false;
            }
        }

        return new TemplateSource(name, title, required, body, bodyStartLine);
    }
}
