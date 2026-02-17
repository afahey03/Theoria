import type { SearchResult } from '../types';
import SearchResultItem from './SearchResultItem';
import './SearchResults.css';

interface Props {
    result: SearchResult | null;
    isLoading: boolean;
    error: string | null;
    hasMore?: boolean;
    onLoadMore?: () => void;
}

/**
 * Displays the list of search results, loading skeleton, error message,
 * or an empty state with academic search tips.
 */
export default function SearchResults({ result, isLoading, error, hasMore, onLoadMore }: Props) {
    if (isLoading) {
        return (
            <div className="search-results">
                <div className="search-skeleton">
                    {[1, 2, 3].map((i) => (
                        <div key={i} className="skeleton-item">
                            <div className="skeleton-line skeleton-title" />
                            <div className="skeleton-line skeleton-url" />
                            <div className="skeleton-line skeleton-snippet" />
                            <div className="skeleton-line skeleton-snippet short" />
                        </div>
                    ))}
                </div>
                <p className="search-status searching-text">Searching academic sources...</p>
            </div>
        );
    }

    if (error) {
        return <div className="search-status search-error">Error: {error}</div>;
    }

    if (!result) {
        return (
            <div className="search-tips">
                <h3 className="tips-title">Search Tips for Scholars</h3>
                <ul className="tips-list">
                    <li>Use specific theological terms: <em>"Thomistic natural law"</em></li>
                    <li>Include author names: <em>"Aquinas on the soul"</em></li>
                    <li>Search philosophical concepts: <em>"Aristotelian metaphysics substance"</em></li>
                    <li>Try patristic topics: <em>"Augustine free will grace"</em></li>
                    <li>Explore biblical scholarship: <em>"Pauline epistles justification"</em></li>
                </ul>
                <p className="tips-hint">
                    Press <kbd>/</kbd> to focus the search bar
                </p>
            </div>
        );
    }

    return (
        <div className="search-results">
            <p className="search-meta">
                {result.totalMatches} result{result.totalMatches !== 1 ? 's' : ''} found
                in {(result.elapsedMilliseconds / 1000).toFixed(2)}s
            </p>
            {result.items.length === 0 ? (
                <p className="search-status">No results found for "{result.query}"</p>
            ) : (
                <>
                    {result.items.map((item, idx) => (
                        <SearchResultItem key={idx} item={item} />
                    ))}
                    {hasMore && (
                        <button className="load-more-btn" onClick={onLoadMore}>
                            Load more results
                        </button>
                    )}
                </>
            )}
        </div>
    );
}
