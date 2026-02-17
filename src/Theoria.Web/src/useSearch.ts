import { useState, useCallback, useEffect } from 'react';
import { search } from './api';
import type { SearchResult, SearchResultItem } from './types';

const PAGE_SIZE = 10;
const HISTORY_KEY = 'theoria_search_history';
const MAX_HISTORY = 20;

/** Load search history from localStorage. */
function loadHistory(): string[] {
    try {
        const raw = localStorage.getItem(HISTORY_KEY);
        return raw ? JSON.parse(raw) : [];
    } catch {
        return [];
    }
}

/** Save search history to localStorage. */
function saveHistory(history: string[]): void {
    try {
        localStorage.setItem(HISTORY_KEY, JSON.stringify(history));
    } catch { /* ignore quota errors */ }
}

/**
 * Custom hook that manages search state, API interaction, and search history.
 * Fetches up to 50 results, displays 10 at a time with "load more".
 * Saves recent queries in localStorage for quick re-searching.
 */
export function useSearch() {
    const [query, setQuery] = useState('');
    const [allItems, setAllItems] = useState<SearchResultItem[]>([]);
    const [displayedCount, setDisplayedCount] = useState(0);
    const [result, setResult] = useState<SearchResult | null>(null);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [history, setHistory] = useState<string[]>(loadHistory);

    // Persist history changes
    useEffect(() => {
        saveHistory(history);
    }, [history]);

    const executeSearch = useCallback(async (overrideQuery?: string) => {
        const q = overrideQuery ?? query;
        if (!q.trim()) return;

        // Add to search history (deduplicated, most recent first)
        setHistory(prev => {
            const filtered = prev.filter(h => h.toLowerCase() !== q.trim().toLowerCase());
            return [q.trim(), ...filtered].slice(0, MAX_HISTORY);
        });

        setIsLoading(true);
        setError(null);

        try {
            const searchResult = await search(q, 50);
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

    const clearHistory = useCallback(() => {
        setHistory([]);
    }, []);

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
        history,
        clearHistory,
    };
}
