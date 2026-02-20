using System.Text.Json;
using MessagePack;
using MessagePack.Resolvers;
using Theoria.Engine.Indexing;
using Theoria.Shared.Models;

namespace Theoria.Engine.Storage;

/// <summary>
/// Persists the inverted index to disk using MessagePack binary serialization.
///
/// Design decisions:
///   - MessagePack with LZ4 compression: 3-5x faster and 50-70% smaller than JSON.
///   - Consolidated into a single .msgpack file (instead of 4 separate JSON files).
///   - Uses ContractlessStandardResolver — no [MessagePackObject] annotations needed.
///   - Atomic write-to-temp-then-rename to prevent corruption on crash.
///   - Falls back to loading legacy JSON files for backward compatibility.
///   - Internal DTOs (PostingDto, DocMetaDto) decouple storage from domain models.
/// </summary>
public sealed class IndexStorage
{
    private readonly string _storagePath;

    private static readonly MessagePackSerializerOptions MsgPackOptions =
        MessagePackSerializerOptions.Standard
            .WithResolver(ContractlessStandardResolver.Instance)
            .WithCompression(MessagePackCompression.Lz4BlockArray);

    // Legacy JSON options for backward-compatible loading
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
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

    private string BundleFilePath => Path.Combine(_storagePath, "index.msgpack");

    // Legacy JSON file paths (for migration on first load)
    private string LegacyIndexFilePath => Path.Combine(_storagePath, "index.json");
    private string LegacyDocumentsFilePath => Path.Combine(_storagePath, "documents.json");
    private string LegacyDocLengthsFilePath => Path.Combine(_storagePath, "doc_lengths.json");
    private string LegacyDocContentsFilePath => Path.Combine(_storagePath, "doc_contents.json");

    /// <summary>
    /// Saves the entire inverted index to a single MessagePack binary file.
    /// Uses a write-to-temp-then-rename strategy for atomicity.
    /// </summary>
    public async Task SaveAsync(InvertedIndex index, CancellationToken cancellationToken = default)
    {
        // Convert domain types to DTOs for clean serialization
        var bundle = new IndexBundle
        {
            Index = index.GetRawIndex().ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Values.Select(p => new PostingDto
                {
                    DocId = p.DocId,
                    TermFrequency = p.TermFrequency,
                    Positions = [.. p.Positions]
                }).ToList()),
            Documents = index.GetRawDocuments().ToDictionary(
                kvp => kvp.Key,
                kvp => new DocMetaDto
                {
                    Id = kvp.Value.Id,
                    Title = kvp.Value.Title,
                    Url = kvp.Value.Url,
                    ContentType = (int)kvp.Value.ContentType,
                    Path = kvp.Value.Path,
                    LastIndexedTicks = kvp.Value.LastIndexedAt.Ticks
                }),
            DocLengths = index.GetRawDocLengths().ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            DocContents = index.GetRawDocContents().ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };

        var tempPath = BundleFilePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await MessagePackSerializer.SerializeAsync(stream, bundle, MsgPackOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        // Atomic rename
        File.Move(tempPath, BundleFilePath, overwrite: true);
    }

    /// <summary>
    /// Loads the inverted index from disk. Tries MessagePack first, then falls back
    /// to legacy JSON files for backward compatibility.
    /// </summary>
    public async Task<bool> LoadAsync(InvertedIndex index, CancellationToken cancellationToken = default)
    {
        // Try new MessagePack format first
        if (File.Exists(BundleFilePath))
        {
            return await LoadMsgPackAsync(index, cancellationToken);
        }

        // Fall back to legacy JSON format
        if (File.Exists(LegacyIndexFilePath))
        {
            return await LoadLegacyJsonAsync(index, cancellationToken);
        }

        return false;
    }

    private async Task<bool> LoadMsgPackAsync(InvertedIndex index, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(BundleFilePath);
        var bundle = await MessagePackSerializer.DeserializeAsync<IndexBundle>(stream, MsgPackOptions, cancellationToken);

        if (bundle is null) return false;

        // Convert DTOs back to domain types
        var rawIndex = bundle.Index.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(dto => new Posting
            {
                DocId = dto.DocId,
                TermFrequency = dto.TermFrequency,
                Positions = new HashSet<int>(dto.Positions)
            }).ToList());

        var documents = bundle.Documents.ToDictionary(
            kvp => kvp.Key,
            kvp => new DocumentMetadata
            {
                Id = kvp.Value.Id,
                Title = kvp.Value.Title,
                Url = kvp.Value.Url,
                ContentType = (Shared.Enums.ContentType)kvp.Value.ContentType,
                Path = kvp.Value.Path,
                LastIndexedAt = new DateTime(kvp.Value.LastIndexedTicks, DateTimeKind.Utc)
            });

        index.LoadFrom(rawIndex, documents, bundle.DocLengths, bundle.DocContents);
        return true;
    }

    private async Task<bool> LoadLegacyJsonAsync(InvertedIndex index, CancellationToken cancellationToken)
    {
        var rawIndex = await ReadJsonFileAsync<Dictionary<string, List<Posting>>>(LegacyIndexFilePath, cancellationToken);
        var documents = await ReadJsonFileAsync<Dictionary<string, DocumentMetadata>>(LegacyDocumentsFilePath, cancellationToken);
        var docLengths = await ReadJsonFileAsync<Dictionary<string, int>>(LegacyDocLengthsFilePath, cancellationToken);
        var docContents = await ReadJsonFileAsync<Dictionary<string, string>>(LegacyDocContentsFilePath, cancellationToken);

        if (rawIndex is null || documents is null || docLengths is null || docContents is null)
            return false;

        index.LoadFrom(rawIndex, documents, docLengths, docContents);
        return true;
    }

    private static async Task<T?> ReadJsonFileAsync<T>(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath)) return default;
        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
    }
}

// --- Internal DTOs for MessagePack serialization ---
// Separate from domain models to avoid polluting them with serialization concerns.
// Uses plain get/set properties compatible with ContractlessStandardResolver.

internal sealed class IndexBundle
{
    public Dictionary<string, List<PostingDto>> Index { get; set; } = new();
    public Dictionary<string, DocMetaDto> Documents { get; set; } = new();
    public Dictionary<string, int> DocLengths { get; set; } = new();
    public Dictionary<string, string> DocContents { get; set; } = new();
}

internal sealed class PostingDto
{
    public string DocId { get; set; } = "";
    public int TermFrequency { get; set; }
    public int[] Positions { get; set; } = [];
}

internal sealed class DocMetaDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Url { get; set; }
    public int ContentType { get; set; }
    public string? Path { get; set; }
    public long LastIndexedTicks { get; set; }
}
