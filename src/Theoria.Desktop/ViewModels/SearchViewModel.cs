using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows.Input;
using Theoria.Engine;
using Theoria.Engine.Crawling;
using Theoria.Shared.Interfaces;
using Theoria.Shared.Models;

namespace Theoria.Desktop.ViewModels;

/// <summary>
/// Main view model for the desktop search window.
///
/// Supports two modes:
///   1. Live Search Mode  – searches the internet in real time via DuckDuckGo +
///                          BM25 ranking (same pipeline as the web app).
///   2. Hosted API Mode   – calls Theoria.Api over HTTP (shares the same live search).
///
/// Both modes produce internet search results.
/// The toggle is controlled by <see cref="UseLocalEngine"/>.
/// </summary>
public sealed class SearchViewModel : ViewModelBase
{
    private readonly LiveSearchOrchestrator _liveSearch;
    private readonly HttpClient _httpClient;

    private string _query = string.Empty;
    private bool _useLocalEngine = true;
    private bool _isSearching;
    private bool _isLoadingMore;
    private string _statusText = "Ready";
    private string _apiBaseUrl = "http://localhost:5110";

    /// <summary>All results from the last search (may be more than currently displayed).</summary>
    private List<SearchResultItem> _allResults = [];
    private int _displayedCount;
    private const int PageSize = 10;

    public SearchViewModel()
    {
        // Create the live search pipeline (same components as the API)
        var storagePath = System.IO.Path.Combine(AppContext.BaseDirectory, "theoria-index");
        var engine = SearchEngineFactory.Create(storagePath);
        _ = engine.LoadIndexAsync();

        var crawler = new WebCrawler(engine);
        var searchProvider = new WebSearchProvider();
        _liveSearch = new LiveSearchOrchestrator(searchProvider, crawler);

        _httpClient = new HttpClient();

        SearchCommand = new RelayCommand(ExecuteSearchAsync, _ => !string.IsNullOrWhiteSpace(Query));
        LoadMoreCommand = new RelayCommand(ExecuteLoadMoreAsync, _ => CanLoadMore);
    }

    // --- Bindable properties ---

    public string Query
    {
        get => _query;
        set => SetProperty(ref _query, value);
    }

    public bool UseLocalEngine
    {
        get => _useLocalEngine;
        set
        {
            if (SetProperty(ref _useLocalEngine, value))
                StatusText = value ? "Mode: Live Internet Search" : "Mode: Hosted API";
        }
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

    public string ApiBaseUrl
    {
        get => _apiBaseUrl;
        set => SetProperty(ref _apiBaseUrl, value);
    }

    /// <summary>Observable collection of search result items bound to the ListView.</summary>
    public ObservableCollection<SearchResultItem> Results { get; } = [];

    /// <summary>Whether there are more results to display.</summary>
    public bool CanLoadMore => _displayedCount < _allResults.Count && !_isLoadingMore;

    // --- Commands ---

    public ICommand SearchCommand { get; }
    public ICommand LoadMoreCommand { get; }

    // --- Command implementations ---

    private async Task ExecuteSearchAsync(object? _)
    {
        if (string.IsNullOrWhiteSpace(Query)) return;

        IsSearching = true;
        StatusText = "Searching the web...";
        Results.Clear();
        _allResults = [];
        _displayedCount = 0;

        try
        {
            SearchResult result;

            if (UseLocalEngine)
            {
                // Direct in-process live internet search — fetch up to 50
                result = await _liveSearch.SearchAsync(Query, topN: 50);
            }
            else
            {
                // Call the hosted API
                var response = await _httpClient.GetFromJsonAsync<SearchResult>(
                    $"{ApiBaseUrl}/search?q={Uri.EscapeDataString(Query)}&topN=50");
                result = response ?? new SearchResult { Query = Query, Items = [] };
            }

            _allResults = [.. result.Items];

            // Show first page
            var firstPage = _allResults.Take(PageSize);
            foreach (var item in firstPage)
                Results.Add(item);
            _displayedCount = Results.Count;

            OnPropertyChanged(nameof(CanLoadMore));

            var moreText = _allResults.Count > _displayedCount
                ? $" (scroll for more)"
                : "";
            StatusText = $"Found {result.TotalMatches} results in {result.ElapsedMilliseconds:F1}ms{moreText}";
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

        if (_displayedCount >= _allResults.Count)
            StatusText = StatusText.Replace(" (scroll for more)", "");

        return Task.CompletedTask;
    }
}
