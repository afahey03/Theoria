using System.Collections.ObjectModel;
using System.Windows.Input;
using Theoria.Engine.Crawling;
using Theoria.Shared.Models;

namespace Theoria.Desktop.ViewModels;

/// <summary>
/// Main view model for the desktop search window.
///
/// Searches the internet in real time via DuckDuckGo + BM25 ranking
/// (same pipeline as the web app).
/// Maintains a search history and supports cancellation.
/// </summary>
public sealed class SearchViewModel : ViewModelBase
{
    private readonly LiveSearchOrchestrator _liveSearch;

    private string _query = string.Empty;
    private bool _isSearching;
    private bool _isLoadingMore;
    private string _statusText = "Ready — type a query to search academic sources";
    private CancellationTokenSource? _searchCts;

    /// <summary>All results from the last search (may be more than currently displayed).</summary>
    private List<SearchResultItem> _allResults = [];
    private int _displayedCount;
    private const int PageSize = 10;
    private const int MaxHistory = 20;

    public SearchViewModel()
    {
        // Create the live search pipeline — no persistent index needed
        var crawler = new WebCrawler();
        var searchProvider = new WebSearchProvider();
        _liveSearch = new LiveSearchOrchestrator(searchProvider, crawler);

        SearchCommand = new RelayCommand(ExecuteSearchAsync, _ => !string.IsNullOrWhiteSpace(Query));
        LoadMoreCommand = new RelayCommand(ExecuteLoadMoreAsync, _ => CanLoadMore);
        CancelSearchCommand = new RelayCommand(_ =>
        {
            _searchCts?.Cancel();
            return Task.CompletedTask;
        }, _ => IsSearching);
    }

    // --- Bindable properties ---

    public string Query
    {
        get => _query;
        set => SetProperty(ref _query, value);
    }

    public bool IsSearching
    {
        get => _isSearching;
        set => SetProperty(ref _isSearching, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    /// <summary>Observable collection of search result items bound to the ListView.</summary>
    public ObservableCollection<SearchResultItem> Results { get; } = [];

    /// <summary>Recent search queries for the history dropdown.</summary>
    public ObservableCollection<string> SearchHistory { get; } = [];

    /// <summary>Whether there are more results to display.</summary>
    public bool CanLoadMore => _displayedCount < _allResults.Count && !_isLoadingMore;

    // --- Commands ---

    public ICommand SearchCommand { get; }
    public ICommand LoadMoreCommand { get; }
    public ICommand CancelSearchCommand { get; }

    // --- Public methods ---

    /// <summary>
    /// Executes a search with a specific query string (used by history dropdown).
    /// </summary>
    public async Task ExecuteSearchFromHistoryAsync(string query)
    {
        Query = query;
        await ExecuteSearchAsync(null);
    }

    // --- Command implementations ---

    private async Task ExecuteSearchAsync(object? _)
    {
        if (string.IsNullOrWhiteSpace(Query)) return;

        // Cancel any previous search
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        // Add to history (deduplicated)
        var trimmed = Query.Trim();
        for (int i = SearchHistory.Count - 1; i >= 0; i--)
        {
            if (SearchHistory[i].Equals(trimmed, StringComparison.OrdinalIgnoreCase))
                SearchHistory.RemoveAt(i);
        }
        SearchHistory.Insert(0, trimmed);
        while (SearchHistory.Count > MaxHistory)
            SearchHistory.RemoveAt(SearchHistory.Count - 1);

        IsSearching = true;
        StatusText = "Searching academic sources...";
        Results.Clear();
        _allResults = [];
        _displayedCount = 0;

        try
        {
            // Live internet search via DuckDuckGo + BM25
            var result = await _liveSearch.SearchAsync(Query, topN: 50, ct);

            _allResults = [.. result.Items];

            // Show first page
            var firstPage = _allResults.Take(PageSize);
            foreach (var item in firstPage)
                Results.Add(item);
            _displayedCount = Results.Count;

            OnPropertyChanged(nameof(CanLoadMore));

            StatusText = $"Found {result.TotalMatches} results in {result.ElapsedMilliseconds / 1000.0:F2}s";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Search cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    private Task ExecuteLoadMoreAsync(object? _)
    {
        if (!CanLoadMore) return Task.CompletedTask;

        _isLoadingMore = true;
        var next = _allResults.Skip(_displayedCount).Take(PageSize);
        foreach (var item in next)
            Results.Add(item);
        _displayedCount = Results.Count;
        _isLoadingMore = false;

        OnPropertyChanged(nameof(CanLoadMore));

        return Task.CompletedTask;
    }
}
