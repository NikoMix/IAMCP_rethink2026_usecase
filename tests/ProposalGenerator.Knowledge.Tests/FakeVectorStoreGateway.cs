using ProposalGenerator.Knowledge;

namespace ProposalGenerator.Knowledge.Tests;

/// <summary>In-memory <see cref="IVectorStoreGateway"/> that records every call.</summary>
internal sealed class FakeVectorStoreGateway : IVectorStoreGateway
{
    private int _nextId;

    public Dictionary<string, string> StoresByName { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, List<StoredFile>> FilesByStore { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, byte[]> Uploads { get; } = new(StringComparer.Ordinal);

    public List<string> Calls { get; } = [];

    /// <summary>Upload names for which <see cref="AddFileAsync"/> fails.</summary>
    public HashSet<string> FailingUploads { get; } = new(StringComparer.Ordinal);

    public IEnumerable<string> MutatingCalls => Calls.Where(c => !c.StartsWith("find", StringComparison.Ordinal) && !c.StartsWith("list", StringComparison.Ordinal));

    public IReadOnlyList<StoredFile> Files(string storeId) => FilesByStore[storeId];

    public string AddExisting(string storeId, IReadOnlyDictionary<string, string> attributes)
    {
        var id = $"file-{++_nextId}";
        FilesByStore[storeId].Add(new StoredFile(id, attributes));
        return id;
    }

    public string AddStore(string name)
    {
        var id = $"vs-{++_nextId}";
        StoresByName[name] = id;
        FilesByStore[id] = [];
        return id;
    }

    public Task<string?> FindVectorStoreAsync(string name, CancellationToken cancellationToken)
    {
        Calls.Add($"find {name}");
        return Task.FromResult(StoresByName.GetValueOrDefault(name));
    }

    public Task<string> CreateVectorStoreAsync(string name, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        Calls.Add($"create {name}");
        return Task.FromResult(AddStore(name));
    }

    public Task<IReadOnlyList<StoredFile>> ListFilesAsync(string vectorStoreId, CancellationToken cancellationToken)
    {
        Calls.Add($"list {vectorStoreId}");
        return Task.FromResult<IReadOnlyList<StoredFile>>([.. FilesByStore[vectorStoreId]]);
    }

    public Task<string> AddFileAsync(string vectorStoreId, string fileName, ReadOnlyMemory<byte> content, IReadOnlyDictionary<string, string> attributes, CancellationToken cancellationToken)
    {
        Calls.Add($"add {fileName}");
        if (FailingUploads.Contains(fileName))
        {
            throw new InvalidOperationException($"Indexing '{fileName}' failed.");
        }

        var id = AddExisting(vectorStoreId, new Dictionary<string, string>(attributes));
        Uploads[id] = content.ToArray();
        return Task.FromResult(id);
    }

    public Task RemoveFileAsync(string vectorStoreId, string fileId, CancellationToken cancellationToken)
    {
        Calls.Add($"remove {fileId}");
        FilesByStore[vectorStoreId].RemoveAll(f => f.FileId == fileId);
        return Task.CompletedTask;
    }
}
