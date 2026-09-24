namespace ProposalGenerator.Knowledge;

/// <summary>What the ingestion did (or, in a dry run, would do) with a file.</summary>
public enum IngestionAction
{
    /// <summary>The file was not in the vector store and has been added.</summary>
    Created,

    /// <summary>The content changed; the new version has been added and the old one removed.</summary>
    Updated,

    /// <summary>The vector store already has this exact content.</summary>
    Unchanged,

    /// <summary>The file no longer exists locally and has been removed (only with pruning).</summary>
    Removed,

    /// <summary>The file no longer exists locally but was kept, because pruning is off.</summary>
    Kept,
}

/// <summary>Result for one file.</summary>
/// <param name="SourcePath">Path relative to the knowledge directory.</param>
/// <param name="Action">What happened.</param>
/// <param name="FileId">ID of the file that is now current; null in a dry run for new content, and for removed files.</param>
public sealed record IngestionEntry(string SourcePath, IngestionAction Action, string? FileId);

/// <summary>Result of an ingestion run.</summary>
/// <param name="VectorStoreId">ID of the vector store; null in a dry run when the store does not exist yet.</param>
/// <param name="VectorStoreCreated">True when the store was created (or, in a dry run, would be created).</param>
/// <param name="DryRun">True when nothing was changed.</param>
/// <param name="Entries">Per-file results ordered by path.</param>
public sealed record IngestionReport(string? VectorStoreId, bool VectorStoreCreated, bool DryRun, IReadOnlyList<IngestionEntry> Entries)
{
    /// <summary>Number of entries with the given action.</summary>
    public int Count(IngestionAction action) => Entries.Count(e => e.Action == action);

    /// <summary>True when the run changed (or would change) the vector store.</summary>
    public bool HasChanges => VectorStoreCreated || Entries.Any(e => e.Action is IngestionAction.Created or IngestionAction.Updated or IngestionAction.Removed);
}

/// <summary>Options of an ingestion run.</summary>
public sealed record IngestionOptions
{
    /// <summary>Name of the vector store to create or update.</summary>
    public string VectorStoreName { get; init; } = KnowledgeIngestor.DefaultVectorStoreName;

    /// <summary>Remove managed files whose source no longer exists locally.</summary>
    public bool Prune { get; init; }

    /// <summary>Only report what would change.</summary>
    public bool DryRun { get; init; }
}

/// <summary>
/// Synchronizes local knowledge files with a vector store. Idempotent: each file is stored with its source path and
/// the SHA-256 of its content as attributes, and a file whose hash is already present is not uploaded again.
/// A changed file is added before its old version is removed, so the store never lacks the file.
/// Files without the <see cref="ManagedByAttribute"/> attribute were not added by this tool and are never touched.
/// </summary>
public sealed class KnowledgeIngestor(IVectorStoreGateway gateway)
{
    /// <summary>Default vector store name.</summary>
    public const string DefaultVectorStoreName = "proposal-generator-knowledge";

    /// <summary>Attribute that marks files managed by this tool.</summary>
    public const string ManagedByAttribute = "managed_by";

    /// <summary>Value of <see cref="ManagedByAttribute"/>.</summary>
    public const string ManagedByValue = "proposal-generator-knowledge";

    /// <summary>Attribute with the path relative to the knowledge directory.</summary>
    public const string SourcePathAttribute = "source_path";

    /// <summary>Attribute with the lower-case hex SHA-256 of the uploaded content.</summary>
    public const string ContentHashAttribute = "content_sha256";

    /// <summary>Runs the ingestion.</summary>
    /// <exception cref="ArgumentException">Two files have the same source path, or the store name is empty.</exception>
    public async Task<IngestionReport> IngestAsync(IReadOnlyList<KnowledgeFile> files, IngestionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.VectorStoreName);
        var duplicate = files.GroupBy(f => f.SourcePath, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"The source path '{duplicate.Key}' occurs more than once.", nameof(files));
        }

        var storeId = await gateway.FindVectorStoreAsync(options.VectorStoreName, cancellationToken).ConfigureAwait(false);
        var created = storeId is null;
        if (storeId is null && !options.DryRun)
        {
            storeId = await gateway.CreateVectorStoreAsync(
                options.VectorStoreName,
                new Dictionary<string, string> { [ManagedByAttribute] = ManagedByValue },
                cancellationToken).ConfigureAwait(false);
        }

        var stored = storeId is null
            ? []
            : (await gateway.ListFilesAsync(storeId, cancellationToken).ConfigureAwait(false))
                .Where(f => f.Attributes.TryGetValue(ManagedByAttribute, out var by) && by == ManagedByValue
                    && f.Attributes.ContainsKey(SourcePathAttribute))
                .ToArray();
        var storedByPath = stored.ToLookup(f => f.Attributes[SourcePathAttribute], StringComparer.Ordinal);

        var entries = new List<IngestionEntry>();
        foreach (var file in files)
        {
            var existing = storedByPath[file.SourcePath].ToArray();
            var current = existing.FirstOrDefault(f => f.Attributes.TryGetValue(ContentHashAttribute, out var hash) && hash == file.ContentSha256);
            var obsolete = existing.Where(f => f != current).ToArray();

            if (current is not null)
            {
                // Duplicates of an unchanged file are left over from an interrupted run and are cleaned up.
                await RemoveAsync(storeId!, obsolete, options, cancellationToken).ConfigureAwait(false);
                entries.Add(new IngestionEntry(file.SourcePath, IngestionAction.Unchanged, current.FileId));
                continue;
            }

            string? fileId = null;
            if (!options.DryRun)
            {
                var attributes = new Dictionary<string, string>
                {
                    [ManagedByAttribute] = ManagedByValue,
                    [SourcePathAttribute] = file.SourcePath,
                    [ContentHashAttribute] = file.ContentSha256,
                };
                fileId = await gateway.AddFileAsync(storeId!, UploadName(file.SourcePath), file.Content, attributes, cancellationToken).ConfigureAwait(false);
            }

            await RemoveAsync(storeId!, obsolete, options, cancellationToken).ConfigureAwait(false);
            entries.Add(new IngestionEntry(file.SourcePath, existing.Length == 0 ? IngestionAction.Created : IngestionAction.Updated, fileId));
        }

        var localPaths = files.Select(f => f.SourcePath).ToHashSet(StringComparer.Ordinal);
        foreach (var group in storedByPath.Where(g => !localPaths.Contains(g.Key)).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            if (options.Prune)
            {
                await RemoveAsync(storeId!, [.. group], options, cancellationToken).ConfigureAwait(false);
            }

            entries.Add(new IngestionEntry(group.Key, options.Prune ? IngestionAction.Removed : IngestionAction.Kept, options.Prune ? null : group.First().FileId));
        }

        return new IngestionReport(storeId, created, options.DryRun, [.. entries.OrderBy(e => e.SourcePath, StringComparer.Ordinal)]);
    }

    /// <summary>File name used for the upload; the directory separators of nested files become underscores.</summary>
    public static string UploadName(string sourcePath) => sourcePath.Replace('/', '_');

    private async Task RemoveAsync(string storeId, IReadOnlyList<StoredFile> files, IngestionOptions options, CancellationToken cancellationToken)
    {
        if (options.DryRun)
        {
            return;
        }

        foreach (var file in files)
        {
            await gateway.RemoveFileAsync(storeId, file.FileId, cancellationToken).ConfigureAwait(false);
        }
    }
}
