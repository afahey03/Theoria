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
}

export interface SearchResult {
    query: string;
    totalMatches: number;
    elapsedMilliseconds: number;
    items: SearchResultItem[];
}

// --- Crawl & Index types ---

export interface IndexUrlResponse {
    message: string;
    documentId: string;
    title: string;
    contentLength: number;
    linksFound: number;
}

export interface CrawledPageInfo {
    url: string;
    title: string | null;
    success: boolean;
    error: string | null;
    contentLength: number;
}

export interface CrawlResponse {
    message: string;
    totalCrawled: number;
    pagesIndexed: number;
    pagesFailed: number;
    pages: CrawledPageInfo[];
}
