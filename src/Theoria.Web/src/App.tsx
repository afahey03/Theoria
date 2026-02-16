import { useSearch } from './useSearch';
import SearchBar from './components/SearchBar';
import SearchResults from './components/SearchResults';
import './App.css';

/**
 * Root application component for the Theoria web frontend.
 * Users type a query and get real-time search results from the internet,
 * ranked and snippeted by the Theoria BM25 engine.
 */
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
    } = useSearch();

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
                <p className="app-subtitle">Theology &amp; Philosophy Search Engine</p>
            </header>

            <main>
                <SearchBar
                    query={query}
                    onQueryChange={setQuery}
                    onSearch={executeSearch}
                    isLoading={isLoading}
                />
                <SearchResults
                    result={result}
                    isLoading={isLoading}
                    error={error}
                    hasMore={hasMore}
                    onLoadMore={loadMore}
                />
            </main>
        </div>
    );
}
