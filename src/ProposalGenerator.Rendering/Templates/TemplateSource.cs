namespace ProposalGenerator.Rendering.Templates;

/// <summary>The raw text of a template file.</summary>
/// <param name="DocumentType">The document type the template belongs to.</param>
/// <param name="SourceName">A human-readable origin, such as the file path, used in error messages.</param>
/// <param name="Content">The complete file content, including YAML front matter.</param>
public sealed record TemplateSource(DocumentType DocumentType, string SourceName, string Content);

/// <summary>
/// Supplies template files. Replace the default <see cref="FileSystemTemplateSource"/> to load
/// templates from another store, for example a corporate-design template repository (#25).
/// </summary>
public interface ITemplateSource
{
    /// <summary>Loads the template for <paramref name="documentType"/>.</summary>
    /// <exception cref="TemplateDefinitionException">The template does not exist or cannot be read.</exception>
    Task<TemplateSource> GetTemplateAsync(DocumentType documentType, CancellationToken cancellationToken);
}

/// <summary>Loads <c>&lt;directory&gt;/&lt;template-id&gt;.md</c> files, for example the repository's <c>templates/</c> folder.</summary>
public sealed class FileSystemTemplateSource : ITemplateSource
{
    private readonly string directory;

    /// <summary>Initializes the source.</summary>
    /// <param name="directory">The directory that contains the template files.</param>
    public FileSystemTemplateSource(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        this.directory = Path.GetFullPath(directory);
    }

    /// <inheritdoc />
    public async Task<TemplateSource> GetTemplateAsync(DocumentType documentType, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, documentType.ToTemplateId() + ".md");
        try
        {
            var content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            return new TemplateSource(documentType, path, content);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new TemplateDefinitionException(path, "The template file does not exist.", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new TemplateDefinitionException(path, "The template file cannot be read.", ex);
        }
    }
}
