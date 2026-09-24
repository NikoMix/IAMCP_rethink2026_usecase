using System.Security.Cryptography;
using System.Text;

namespace ProposalGenerator.Knowledge;

/// <summary>A knowledge file prepared for upload.</summary>
/// <param name="SourcePath">Path relative to the knowledge directory with forward slashes, for example <c>rate-card.json</c>.</param>
/// <param name="Content">Content with line endings normalized to LF; this is what is uploaded and hashed.</param>
/// <param name="ContentSha256">Lower-case hex SHA-256 of <paramref name="Content"/>.</param>
public sealed record KnowledgeFile(string SourcePath, ReadOnlyMemory<byte> Content, string ContentSha256)
{
    /// <summary>Creates a knowledge file from raw bytes, normalizing CRLF and CR line endings to LF.</summary>
    public static KnowledgeFile FromBytes(string sourcePath, ReadOnlySpan<byte> raw)
    {
        var normalized = NormalizeLineEndings(raw);
        return new KnowledgeFile(sourcePath, normalized, Convert.ToHexStringLower(SHA256.HashData(normalized)));
    }

    /// <summary>Creates a knowledge file from text (UTF-8).</summary>
    public static KnowledgeFile FromText(string sourcePath, string text) => FromBytes(sourcePath, Encoding.UTF8.GetBytes(text));

    private static byte[] NormalizeLineEndings(ReadOnlySpan<byte> raw)
    {
        var result = new List<byte>(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] == (byte)'\r')
            {
                result.Add((byte)'\n');
                if (i + 1 < raw.Length && raw[i + 1] == (byte)'\n')
                {
                    i++;
                }
            }
            else
            {
                result.Add(raw[i]);
            }
        }

        return [.. result];
    }
}

/// <summary>Discovers the knowledge files in a directory.</summary>
public static class KnowledgeSource
{
    /// <summary>File extensions that are ingested. Both are supported by Foundry File Search.</summary>
    public static IReadOnlyList<string> SupportedExtensions { get; } = [".md", ".json"];

    /// <summary>
    /// Returns all supported files below <paramref name="directory"/>, ordered by path. README files and files or
    /// directories whose name starts with a dot are skipped, because they describe the knowledge base rather than
    /// being part of it.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    /// <exception cref="InvalidDataException">The directory contains no supported file.</exception>
    public static async Task<IReadOnlyList<KnowledgeFile>> DiscoverAsync(string directory, CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(directory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Knowledge directory '{root}' does not exist.");
        }

        var files = new List<KnowledgeFile>();
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (!IsIngested(relative))
            {
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            files.Add(KnowledgeFile.FromBytes(relative, bytes));
        }

        if (files.Count == 0)
        {
            throw new InvalidDataException($"Knowledge directory '{root}' contains no {string.Join(" or ", SupportedExtensions)} file.");
        }

        return [.. files.OrderBy(f => f.SourcePath, StringComparer.Ordinal)];
    }

    /// <summary>True when a relative path (forward slashes) is part of the knowledge base.</summary>
    public static bool IsIngested(string relativePath)
    {
        var segments = relativePath.Split('/');
        if (segments.Any(s => s.StartsWith('.')))
        {
            return false;
        }

        var name = segments[^1];
        return !Path.GetFileNameWithoutExtension(name).Equals("README", StringComparison.OrdinalIgnoreCase)
            && SupportedExtensions.Contains(Path.GetExtension(name).ToLowerInvariant());
    }
}
