using System.Text.Json;
using Theoria.Engine.Indexing;
using Theoria.Shared.Models;

namespace Theoria.Engine.Storage;

/// <summary>
/// Persists the inverted index to disk as JSON and reloads it on startup.
///
/// Design decisions:
///   - JSON chosen for human readability and debuggability during development.
///   - In production, a binary format (MessagePack, Protobuf) could be swapped in.
///   - Files are written atomically (write to temp, then rename) to avoid corruption.
///   - Async I/O throughout.
/// </summary>
public sealed class IndexStorage
{
    private readonly string _storagePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Creates an IndexStorage that reads/writes index data in the given directory.
    /// The directory is created automatically if it does not exist.
    /// </summary>
    public IndexStorage(string storagePath)
    {
        _storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));
        Directory.CreateDirectory(_storagePath);
    }

    private string IndexFilePath => Path.Combine(_storagePath, "index.json");
    private string DocumentsFilePath => Path.Combine(_storagePath, "documents.json");
    private string DocLengthsFilePath => Path.Combine(_storagePath, "doc_lengths.json");
    private string DocContentsFilePath => Path.Combine(_storagePath, "doc_contents.json");

    /// <summary>
    /// Saves the entire inverted index to disk.
    /// Uses a write-to-temp-then-rename strategy for atomicity.
    /// </summary>
    public async Task SaveAsync(InvertedIndex index, CancellationToken cancellationToken = default)
    {
        // Convert Dictionary<string, Posting> values back to List<Posting> for serialization
        var indexData = index.GetRawIndex()
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Values.ToList());

        await WriteJsonFileAsync(IndexFilePath, indexData, cancellationToken);
        await WriteJsonFileAsync(DocumentsFilePath, index.GetRawDocuments().ToDictionary(), cancellationToken);
        await WriteJsonFileAsync(DocLengthsFilePath, index.GetRawDocLengths().ToDictionary(), cancellationToken);
        await WriteJsonFileAsync(DocContentsFilePath, index.GetRawDocContents().ToDictionary(), cancellationToken);
    }

    /// <summary>
    /// Loads the inverted index from disk. Returns false if no saved index exists.
    /// </summary>
    public async Task<bool> LoadAsync(InvertedIndex index, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(IndexFilePath))
            return false;

        var rawIndex = await ReadJsonFileAsync<Dictionary<string, List<Posting>>>(IndexFilePath, cancellationToken);
        var documents = await ReadJsonFileAsync<Dictionary<string, DocumentMetadata>>(DocumentsFilePath, cancellationToken);
        var docLengths = await ReadJsonFileAsync<Dictionary<string, int>>(DocLengthsFilePath, cancellationToken);
        var docContents = await ReadJsonFileAsync<Dictionary<string, string>>(DocContentsFilePath, cancellationToken);

        if (rawIndex is null || documents is null || docLengths is null || docContents is null)
            return false;

        index.LoadFrom(rawIndex, documents, docLengths, docContents);
        return true;
    }

    // --- Private helpers ---

    private static async Task WriteJsonFileAsync<T>(string filePath, T data, CancellationToken ct)
    {
        var tempPath = filePath + ".tmp";
        await using var stream = File.Create(tempPath);
        await JsonSerializer.SerializeAsync(stream, data, JsonOptions, ct);
        await stream.FlushAsync(ct);
        stream.Close();

        // Atomic rename
        File.Move(tempPath, filePath, overwrite: true);
    }

    private static async Task<T?> ReadJsonFileAsync<T>(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath)) return default;

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
    }
}
