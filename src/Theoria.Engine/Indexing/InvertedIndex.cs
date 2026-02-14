using System.Collections.Concurrent;
using Theoria.Shared.Interfaces;
using Theoria.Shared.Models;

namespace Theoria.Engine.Indexing;

/// <summary>
/// Thread-safe inverted index mapping terms to postings lists.
///
/// Design decisions:
///   - ConcurrentDictionary ensures safe concurrent reads during search
///     and safe writes during incremental indexing.
///   - Document metadata is stored separately so it can be retrieved
///     without scanning post listings.
///   - Document lengths (token counts) are tracked for BM25 average-
///     document-length computation.
///   - Average document length is cached (O(1) reads, invalidated on mutation).
///   - A forward index (docId → terms) enables O(terms-in-doc) removal.
/// </summary>
public sealed class InvertedIndex
{
    // term -> { docId -> Posting }  — O(1) lookup per doc per term
    private readonly ConcurrentDictionary<string, Dictionary<string, Posting>> _index = new(StringComparer.Ordinal);

    // docId -> metadata
    private readonly ConcurrentDictionary<string, DocumentMetadata> _documents = new(StringComparer.Ordinal);

    // docId -> number of tokens in the document (for BM25 normalization)
    private readonly ConcurrentDictionary<string, int> _docLengths = new(StringComparer.Ordinal);

    // docId -> original raw content (needed for snippet generation)
    private readonly ConcurrentDictionary<string, string> _docContents = new(StringComparer.Ordinal);

    // Forward index: docId -> set of terms the document contains (for fast removal)
    private readonly ConcurrentDictionary<string, HashSet<string>> _docTerms = new(StringComparer.Ordinal);

    private readonly ITokenizer _tokenizer;

    // Lock used when mutating postings lists for a single document
    // to ensure atomicity of add/remove operations.
    private readonly object _writeLock = new();

    // Cached average document length — invalidated on add/remove
    private double _cachedAvgDocLength;
    private bool _avgDocLengthDirty = true;

    public InvertedIndex(ITokenizer tokenizer)
    {
        _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
    }

    // --- Public read-only accessors used by scorer / search engine ---

    /// <summary>Total number of indexed documents.</summary>
    public int DocumentCount => _documents.Count;

    /// <summary>Average document length across the corpus (in tokens). Cached for O(1) access.</summary>
    public double AverageDocumentLength
    {
        get
        {
            if (_avgDocLengthDirty)
            {
                _cachedAvgDocLength = _docLengths.IsEmpty ? 0 : _docLengths.Values.Average();
                _avgDocLengthDirty = false;
            }
            return _cachedAvgDocLength;
        }
    }

    /// <summary>
    /// Returns the postings list for a given term, or an empty list if the term is absent.
    /// </summary>
    public IReadOnlyList<Posting> GetPostings(string term)
    {
        return _index.TryGetValue(term, out var postings) ? postings.Values.ToList() : [];
    }

    /// <summary>
    /// Returns the number of documents containing the given term.
    /// </summary>
    public int GetDocumentFrequency(string term)
    {
        return _index.TryGetValue(term, out var postings) ? postings.Count : 0;
    }

    /// <summary>
    /// Returns the posting for a specific term in a specific document, or null if not found.
    /// O(1) lookup instead of O(n) scan through the postings list.
    /// </summary>
    public Posting? GetPosting(string term, string docId)
    {
        if (_index.TryGetValue(term, out var postings) && postings.TryGetValue(docId, out var posting))
            return posting;
        return null;
    }

    /// <summary>Returns document metadata by Id, or null if not found.</summary>
    public DocumentMetadata? GetDocument(string docId)
    {
        _documents.TryGetValue(docId, out var doc);
        return doc;
    }

    /// <summary>Returns the token count for a document.</summary>
    public int GetDocumentLength(string docId)
    {
        return _docLengths.TryGetValue(docId, out var len) ? len : 0;
    }

    /// <summary>Returns the raw content for snippet generation.</summary>
    public string? GetDocumentContent(string docId)
    {
        _docContents.TryGetValue(docId, out var content);
        return content;
    }

    /// <summary>Returns all document IDs currently in the index.</summary>
    public IReadOnlyCollection<string> GetAllDocumentIds() => _documents.Keys.ToArray();

    // --- Mutation methods ---

    /// <summary>
    /// Adds or updates a document in the index.
    /// If the docId already exists, the old postings are removed first (incremental re-index).
    /// </summary>
    public void AddDocument(DocumentMetadata metadata, string content)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var tokens = _tokenizer.Tokenize(content);

        lock (_writeLock)
        {
            // Remove old postings if re-indexing
            if (_documents.ContainsKey(metadata.Id))
            {
                RemoveDocumentPostings(metadata.Id);
            }

            _documents[metadata.Id] = metadata;
            _docLengths[metadata.Id] = tokens.Count;
            _docContents[metadata.Id] = content;
            _avgDocLengthDirty = true;

            // Track which terms this document contains (forward index)
            var termSet = new HashSet<string>(StringComparer.Ordinal);

            // Build postings for this document
            for (int i = 0; i < tokens.Count; i++)
            {
                var term = tokens[i];
                termSet.Add(term);

                var postings = _index.GetOrAdd(term, _ => new Dictionary<string, Posting>(StringComparer.Ordinal));

                if (postings.TryGetValue(metadata.Id, out var existing))
                {
                    existing.TermFrequency++;
                    existing.Positions.Add(i);
                }
                else
                {
                    postings[metadata.Id] = new Posting
                    {
                        DocId = metadata.Id,
                        TermFrequency = 1,
                        Positions = [i]
                    };
                }
            }

            _docTerms[metadata.Id] = termSet;
        }
    }

    /// <summary>
    /// Removes all index data for a document.
    /// </summary>
    public void RemoveDocument(string docId)
    {
        lock (_writeLock)
        {
            RemoveDocumentPostings(docId);
            _documents.TryRemove(docId, out _);
            _docLengths.TryRemove(docId, out _);
            _docContents.TryRemove(docId, out _);
            _docTerms.TryRemove(docId, out _);
            _avgDocLengthDirty = true;
        }
    }

    /// <summary>
    /// Clears the entire index.
    /// </summary>
    public void Clear()
    {
        lock (_writeLock)
        {
            _index.Clear();
            _documents.Clear();
            _docLengths.Clear();
            _docContents.Clear();
            _docTerms.Clear();
            _avgDocLengthDirty = true;
        }
    }

    // --- Serialization helpers (used by IndexStorage) ---

    /// <summary>Returns the raw index dictionary for serialization.</summary>
    internal ConcurrentDictionary<string, Dictionary<string, Posting>> GetRawIndex() => _index;

    /// <summary>Returns all metadata for serialization.</summary>
    internal ConcurrentDictionary<string, DocumentMetadata> GetRawDocuments() => _documents;

    /// <summary>Returns all doc lengths for serialization.</summary>
    internal ConcurrentDictionary<string, int> GetRawDocLengths() => _docLengths;

    /// <summary>Returns all doc contents for serialization.</summary>
    internal ConcurrentDictionary<string, string> GetRawDocContents() => _docContents;

    /// <summary>Bulk-loads data from storage. Only call during startup before any queries.</summary>
    internal void LoadFrom(
        Dictionary<string, List<Posting>> index,
        Dictionary<string, DocumentMetadata> documents,
        Dictionary<string, int> docLengths,
        Dictionary<string, string> docContents)
    {
        lock (_writeLock)
        {
            // Convert List<Posting> → Dictionary<string, Posting> for O(1) lookup
            foreach (var kvp in index)
            {
                var dict = new Dictionary<string, Posting>(kvp.Value.Count, StringComparer.Ordinal);
                foreach (var posting in kvp.Value)
                    dict[posting.DocId] = posting;
                _index[kvp.Key] = dict;
            }

            foreach (var kvp in documents) _documents[kvp.Key] = kvp.Value;
            foreach (var kvp in docLengths) _docLengths[kvp.Key] = kvp.Value;
            foreach (var kvp in docContents) _docContents[kvp.Key] = kvp.Value;

            // Rebuild forward index from loaded postings
            foreach (var (term, postings) in _index)
            {
                foreach (var docId in postings.Keys)
                {
                    var termSet = _docTerms.GetOrAdd(docId, _ => new HashSet<string>(StringComparer.Ordinal));
                    termSet.Add(term);
                }
            }

            _avgDocLengthDirty = true;
        }
    }

    // --- Private helpers ---

    private void RemoveDocumentPostings(string docId)
    {
        // Use forward index for O(terms-in-doc) removal instead of O(total-terms)
        if (_docTerms.TryGetValue(docId, out var terms))
        {
            foreach (var term in terms)
            {
                if (_index.TryGetValue(term, out var postings))
                {
                    postings.Remove(docId);
                    if (postings.Count == 0)
                        _index.TryRemove(term, out _);
                }
            }
        }
    }
}
