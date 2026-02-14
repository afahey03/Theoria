import type { SearchResult } from './types';

/**
 * API client for the Theoria search engine.
 * In development, requests are proxied by Vite to the ASP.NET backend.
 */

const BASE_URL = import.meta.env.VITE_API_URL || '';

/**
 * Performs a live internet search. The backend discovers relevant URLs
 * via DuckDuckGo, fetches each page, scores with BM25, and returns ranked results.
 */
export async function search(
    query: string,
    topN: number = 10,
): Promise<SearchResult> {
    const params = new URLSearchParams({ q: query, topN: topN.toString() });

    const response = await fetch(`${BASE_URL}/search?${params}`);
    if (!response.ok) {
        throw new Error(`Search failed: ${response.statusText}`);
    }
    return response.json();
}
