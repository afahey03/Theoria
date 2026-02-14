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
 * Displays the list of search results, loading indicator, or error message.
 * Shows a "Load more" button when additional results are available.
 */
export default function SearchResults({ result, isLoading, error, hasMore, onLoadMore }: Props) {
    if (isLoading) {
        return <div className="search-status">Searching the web...</div>;
    }

    if (error) {
        return <div className="search-status search-error">Error: {error}</div>;
    }

    if (!result) {
        return null;
    }

    return (
        <div className="search-results">
            <p className="search-meta">
                {result.totalMatches} result{result.totalMatches !== 1 ? 's' : ''} found
                in {result.elapsedMilliseconds.toFixed(1)}ms
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
