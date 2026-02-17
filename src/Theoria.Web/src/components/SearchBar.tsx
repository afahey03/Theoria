import { type FormEvent, useRef, useState, useEffect } from 'react';
import './SearchBar.css';

interface Props {
    query: string;
    onQueryChange: (value: string) => void;
    onSearch: (overrideQuery?: string) => void;
    isLoading: boolean;
    history?: string[];
    onClearHistory?: () => void;
}

/**
 * Search input with a submit button and optional search history dropdown.
 * Pressing Enter or clicking Search triggers the query.
 * Press "/" anywhere on the page to focus the search bar.
 */
export default function SearchBar({
    query,
    onQueryChange,
    onSearch,
    isLoading,
    history = [],
    onClearHistory,
}: Props) {
    const inputRef = useRef<HTMLInputElement>(null);
    const [showHistory, setShowHistory] = useState(false);

    // Global "/" shortcut to focus search
    useEffect(() => {
        const handleKeyDown = (e: KeyboardEvent) => {
            if (
                e.key === '/' &&
                !e.ctrlKey && !e.metaKey &&
                document.activeElement?.tagName !== 'INPUT' &&
                document.activeElement?.tagName !== 'TEXTAREA'
            ) {
                e.preventDefault();
                inputRef.current?.focus();
            }
        };
        document.addEventListener('keydown', handleKeyDown);
        return () => document.removeEventListener('keydown', handleKeyDown);
    }, []);

    const handleSubmit = (e: FormEvent) => {
        e.preventDefault();
        setShowHistory(false);
        onSearch();
    };

    const handleHistoryClick = (q: string) => {
        onQueryChange(q);
        setShowHistory(false);
        onSearch(q);
    };

    return (
        <div className="search-bar-wrapper">
            <form className="search-bar" onSubmit={handleSubmit}>
                <div className="search-input-wrapper">
                    <input
                        ref={inputRef}
                        type="text"
                        className="search-input"
                        value={query}
                        onChange={(e) => onQueryChange(e.target.value)}
                        onFocus={() => history.length > 0 && setShowHistory(true)}
                        onBlur={() => setTimeout(() => setShowHistory(false), 200)}
                        placeholder='Search theology & philosophy...  (press "/" to focus)'
                        disabled={isLoading}
                        aria-label="Search query"
                        autoComplete="off"
                    />
                    {showHistory && history.length > 0 && (
                        <div className="search-history-dropdown" role="listbox">
                            <div className="history-header">
                                <span>Recent searches</span>
                                {onClearHistory && (
                                    <button
                                        className="history-clear"
                                        onClick={(e) => {
                                            e.preventDefault();
                                            onClearHistory();
                                        }}
                                        type="button"
                                    >
                                        Clear
                                    </button>
                                )}
                            </div>
                            {history.slice(0, 8).map((h, i) => (
                                <button
                                    key={i}
                                    className="history-item"
                                    onMouseDown={() => handleHistoryClick(h)}
                                    type="button"
                                    role="option"
                                >
                                    <svg className="history-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                                        <circle cx="12" cy="12" r="10" />
                                        <polyline points="12 6 12 12 16 14" />
                                    </svg>
                                    {h}
                                </button>
                            ))}
                        </div>
                    )}
                </div>
                <button type="submit" className="search-button" disabled={isLoading || !query.trim()}>
                    {isLoading ? 'Searching...' : 'Search'}
                </button>
            </form>
        </div>
    );
}
