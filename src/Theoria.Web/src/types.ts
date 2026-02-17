/**
 * TypeScript types mirroring the C# DTOs from Theoria.Shared.
 * Kept in sync manually — both sides use the same shapes so the
 * web frontend can deserialize API responses directly.
 */

export enum ContentType {
    Pdf = 'Pdf',
    Markdown = 'Markdown',
    Html = 'Html',
}

export interface SearchResultItem {
    title: string;
    url: string | null;
    snippet: string;
    score: number;
    sourceType: ContentType;
    /** Whether the source is from a known academic / scholarly domain. */
    isScholarly: boolean;
    /** Display domain of the source (e.g., "jstor.org"). */
    domain: string | null;
}

export interface SearchResult {
    query: string;
    totalMatches: number;
    elapsedMilliseconds: number;
    items: SearchResultItem[];
}
