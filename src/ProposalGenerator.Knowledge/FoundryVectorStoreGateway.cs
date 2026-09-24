using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using Azure.AI.Projects;
using OpenAI.Files;
using OpenAI.VectorStores;

namespace ProposalGenerator.Knowledge;

/// <summary>
/// <see cref="IVectorStoreGateway"/> over the vector store and file APIs of a Microsoft Foundry project
/// (<c>Azure.AI.Projects</c> with <c>Azure.AI.Extensions.OpenAI</c>). Authentication is keyless via the
/// credential passed to <see cref="AIProjectClient"/>.
/// </summary>
public sealed class FoundryVectorStoreGateway : IVectorStoreGateway
{
    private readonly VectorStoreClient _vectorStores;
    private readonly OpenAIFileClient _files;
    private readonly TimeSpan _indexingTimeout;
    private readonly TimeSpan _pollInterval;

    /// <summary>Creates the gateway for a Foundry project.</summary>
    /// <param name="projectClient">Client of the Foundry project, for example <c>new AIProjectClient(endpoint, new DefaultAzureCredential())</c>.</param>
    /// <param name="indexingTimeout">How long to wait for a file to be indexed; default five minutes.</param>
    /// <param name="pollInterval">How often to check the indexing status; default two seconds.</param>
    public FoundryVectorStoreGateway(AIProjectClient projectClient, TimeSpan? indexingTimeout = null, TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(projectClient);
        _vectorStores = projectClient.ProjectOpenAIClient.GetVectorStoreClient();
        _files = projectClient.ProjectOpenAIClient.GetOpenAIFileClient();
        _indexingTimeout = indexingTimeout ?? TimeSpan.FromMinutes(5);
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(2);
    }

    /// <inheritdoc />
    public async Task<string?> FindVectorStoreAsync(string name, CancellationToken cancellationToken)
    {
        var matches = new List<string>();
        await foreach (var store in _vectorStores.GetVectorStoresAsync(new VectorStoreCollectionOptions(), cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(store.Name, name, StringComparison.Ordinal))
            {
                matches.Add(store.Id);
            }
        }

        return matches.Count switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidOperationException(
                $"{matches.Count} vector stores are named '{name}' ({string.Join(", ", matches)}). Delete the duplicates or choose another name with --vector-store-name."),
        };
    }

    /// <inheritdoc />
    public async Task<string> CreateVectorStoreAsync(string name, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        var options = new VectorStoreCreationOptions { Name = name };
        foreach (var (key, value) in metadata)
        {
            options.Metadata[key] = value;
        }

        VectorStore store = await _vectorStores.CreateVectorStoreAsync(options, cancellationToken).ConfigureAwait(false);
        return store.Id;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredFile>> ListFilesAsync(string vectorStoreId, CancellationToken cancellationToken)
    {
        var files = new List<StoredFile>();
        await foreach (var file in _vectorStores.GetVectorStoreFilesAsync(vectorStoreId, new VectorStoreFileCollectionOptions(), cancellationToken).ConfigureAwait(false))
        {
            files.Add(new StoredFile(file.FileId, ToStrings(file.Attributes)));
        }

        return files;
    }

    /// <inheritdoc />
    public async Task<string> AddFileAsync(string vectorStoreId, string fileName, ReadOnlyMemory<byte> content, IReadOnlyDictionary<string, string> attributes, CancellationToken cancellationToken)
    {
        OpenAIFile uploaded;
        using (var stream = new MemoryStream(content.ToArray(), writable: false))
        {
            uploaded = await _files.UploadFileAsync(stream, fileName, FileUploadPurpose.Assistants, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            // The protocol method attaches the file and its attributes in one request, so an interrupted run
            // cannot leave an attached file without the attributes that make it recognizable as managed.
            var body = BinaryData.FromObjectAsJson(new { file_id = uploaded.Id, attributes });
            await _vectorStores.AddFileToVectorStoreAsync(vectorStoreId, BinaryContent.Create(body), new RequestOptions { CancellationToken = cancellationToken }).ConfigureAwait(false);
            await WaitUntilIndexedAsync(vectorStoreId, uploaded.Id, fileName, cancellationToken).ConfigureAwait(false);
            return uploaded.Id;
        }
        catch (Exception exception) when (exception is ClientResultException or InvalidOperationException or TimeoutException)
        {
            await RemoveUploadAfterFailureAsync(vectorStoreId, uploaded.Id, exception).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RemoveFileAsync(string vectorStoreId, string fileId, CancellationToken cancellationToken)
    {
        await IgnoreNotFoundAsync(() => _vectorStores.RemoveFileFromVectorStoreAsync(vectorStoreId, fileId, cancellationToken)).ConfigureAwait(false);
        await IgnoreNotFoundAsync(() => _files.DeleteFileAsync(fileId, cancellationToken)).ConfigureAwait(false);
    }

    private async Task WaitUntilIndexedAsync(string vectorStoreId, string fileId, string fileName, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + _indexingTimeout;
        while (true)
        {
            VectorStoreFile file = await _vectorStores.GetVectorStoreFileAsync(vectorStoreId, fileId, cancellationToken).ConfigureAwait(false);
            if (file.Status == VectorStoreFileStatus.Completed)
            {
                return;
            }

            if (file.Status == VectorStoreFileStatus.Failed || file.Status == VectorStoreFileStatus.Cancelled)
            {
                throw new InvalidOperationException(
                    $"Indexing '{fileName}' ended with status {file.Status}: {file.LastError?.Code} {file.LastError?.Message}".TrimEnd());
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException($"'{fileName}' was not indexed within {_indexingTimeout.TotalSeconds:0} seconds (status {file.Status}).");
            }

            await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RemoveUploadAfterFailureAsync(string vectorStoreId, string fileId, Exception original)
    {
        try
        {
            await RemoveFileAsync(vectorStoreId, fileId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (ClientResultException cleanup)
        {
            throw new InvalidOperationException(
                $"Adding file {fileId} failed ({original.Message}) and removing the upload failed as well ({cleanup.Message}). Delete file {fileId} manually.",
                original);
        }
    }

    private static async Task IgnoreNotFoundAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (ClientResultException exception) when (exception.Status == 404)
        {
            // Already gone: removal is idempotent.
        }
    }

    private static Dictionary<string, string> ToStrings(IDictionary<string, BinaryData>? attributes)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (attributes is null)
        {
            return result;
        }

        foreach (var (key, value) in attributes)
        {
            using var json = JsonDocument.Parse(value);
            result[key] = json.RootElement.ValueKind == JsonValueKind.String ? json.RootElement.GetString()! : json.RootElement.GetRawText();
        }

        return result;
    }
}
