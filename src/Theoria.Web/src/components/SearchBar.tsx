import type { FormEvent } from 'react';
import './SearchBar.css';

interface Props {
    query: string;
    onQueryChange: (value: string) => void;
    onSearch: () => void;
    isLoading: boolean;
}

/**
 * Search input with a submit button.
 * Pressing Enter or clicking Search triggers the query.
 */
export default function SearchBar({ query, onQueryChange, onSearch, isLoading }: Props) {
    const handleSubmit = (e: FormEvent) => {
        e.preventDefault();
        onSearch();
    };

    return (
        <form className="search-bar" onSubmit={handleSubmit}>
            <input
                type="text"
                className="search-input"
                value={query}
                onChange={(e) => onQueryChange(e.target.value)}
                placeholder="Search theology & philosophy..."
                disabled={isLoading}
            />
            <button type="submit" className="search-button" disabled={isLoading || !query.trim()}>
                Search
            </button>
        </form>
    );
}
