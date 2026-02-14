import { useState, useCallback } from 'react';
import { search } from './api';
import type { SearchResult, SearchResultItem } from './types';

const PAGE_SIZE = 10;

/**
 * Custom hook that manages search state and API interaction.
 * Fetches up to 25 results, displays 10 at a time with "load more".
 */
export function useSearch() {
    const [query, setQuery] = useState('');
    const [allItems, setAllItems] = useState<SearchResultItem[]>([]);
    const [displayedCount, setDisplayedCount] = useState(0);
    const [result, setResult] = useState<SearchResult | null>(null);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const executeSearch = useCallback(async () => {
        if (!query.trim()) return;

        setIsLoading(true);
        setError(null);

        try {
            const searchResult = await search(query, 50);
            setAllItems(searchResult.items);
            setDisplayedCount(Math.min(PAGE_SIZE, searchResult.items.length));
            setResult({
                ...searchResult,
                items: searchResult.items.slice(0, PAGE_SIZE),
            });
        } catch (err) {
            setError(err instanceof Error ? err.message : 'An error occurred');
            setResult(null);
            setAllItems([]);
            setDisplayedCount(0);
        } finally {
            setIsLoading(false);
        }
    }, [query]);

    const loadMore = useCallback(() => {
        setDisplayedCount((prev) => {
            const nextCount = Math.min(prev + PAGE_SIZE, allItems.length);
            setResult((r) =>
                r ? { ...r, items: allItems.slice(0, nextCount) } : r
            );
            return nextCount;
        });
    }, [allItems]);

    const hasMore = allItems.length > 0 && displayedCount < allItems.length;

    return {
        query,
        setQuery,
        result,
        isLoading,
        error,
        executeSearch,
        loadMore,
        hasMore,
    };
}
