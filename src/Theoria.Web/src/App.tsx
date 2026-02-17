import { useSearch } from './useSearch';
import SearchBar from './components/SearchBar';
import SearchResults from './components/SearchResults';
import './App.css';

export default function App() {
    const {
        query,
        setQuery,
        result,
        isLoading,
        error,
        executeSearch,
        loadMore,
        hasMore,
        history,
        clearHistory,
    } = useSearch();

    const year = new Date().getFullYear();

    return (
        <div className="app">
            <header className="app-header">
                <a
                    href="https://github.com/afahey03/Theoria/releases/latest/download/TheoriaSetup.exe"
                    className="install-btn"
                    download
                >
                    Install Theoria
                </a>

                <h1 className="app-title">Theoria</h1>
                <p className="app-subtitle">
                    Theology &amp; Philosophy Search Engine
                </p>
            </header>

            <main className="app-main">
                <SearchBar
                    query={query}
                    onQueryChange={setQuery}
                    onSearch={executeSearch}
                    isLoading={isLoading}
                    history={history}
                    onClearHistory={clearHistory}
                />
                <SearchResults
                    result={result}
                    isLoading={isLoading}
                    error={error}
                    hasMore={hasMore}
                    onLoadMore={loadMore}
                />
            </main>

            <footer className="app-footer">
                <div className="footer-content">
                    <div className="footer-left">
                        © {year} Aidan Fahey
                        <span className="footer-divider">·</span>
                        <span className="mit-badge">MIT Licensed</span>
                    </div>

                    <a
                        href="https://github.com/afahey03/Theoria"
                        target="_blank"
                        rel="noopener noreferrer"
                        className="github-link"
                        aria-label="View on GitHub"
                    >
                        {/* GitHub SVG Icon */}
                        <svg
                            height="18"
                            width="18"
                            viewBox="0 0 16 16"
                            fill="currentColor"
                        >
                            <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 
                            0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52
                            -.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07
                            -1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 
                            0 0 .67-.21 2.2.82a7.5 7.5 0 0 1 2-.27c.68 0 1.36.09 2 .27
                            1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12
                            .51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95
                            .29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2
                            0 .21.15.46.55.38A8.013 8.013 0 0 0 16 8
                            c0-4.42-3.58-8-8-8z" />
                        </svg>
                        GitHub
                    </a>
                </div>
            </footer>
        </div>
    );
}
