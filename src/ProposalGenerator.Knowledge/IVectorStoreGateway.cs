namespace ProposalGenerator.Knowledge;

/// <summary>A file attached to the vector store, with the attributes stored alongside it.</summary>
/// <param name="FileId">ID of the uploaded file.</param>
/// <param name="Attributes">File attributes; values are strings.</param>
public sealed record StoredFile(string FileId, IReadOnlyDictionary<string, string> Attributes);

/// <summary>
/// The vector store operations the ingestion needs. <see cref="FoundryVectorStoreGateway"/> implements it with the
/// Foundry SDK; tests use an in-memory fake, so the ingestion logic runs without Azure.
/// </summary>
public interface IVectorStoreGateway
{
    /// <summary>Returns the ID of the vector store with the given name, or null when none exists.</summary>
    /// <exception cref="InvalidOperationException">More than one vector store has the name.</exception>
    Task<string?> FindVectorStoreAsync(string name, CancellationToken cancellationToken);

    /// <summary>Creates an empty vector store and returns its ID.</summary>
    Task<string> CreateVectorStoreAsync(string name, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken);

    /// <summary>Lists all files attached to the vector store.</summary>
    Task<IReadOnlyList<StoredFile>> ListFilesAsync(string vectorStoreId, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads a file, attaches it to the vector store together with its attributes, and waits until it is indexed.
    /// Returns the file ID.
    /// </summary>
    /// <exception cref="InvalidOperationException">Indexing failed; the uploaded file has been removed again.</exception>
    Task<string> AddFileAsync(string vectorStoreId, string fileName, ReadOnlyMemory<byte> content, IReadOnlyDictionary<string, string> attributes, CancellationToken cancellationToken);

    /// <summary>Detaches a file from the vector store and deletes the uploaded file.</summary>
    Task RemoveFileAsync(string vectorStoreId, string fileId, CancellationToken cancellationToken);
}
