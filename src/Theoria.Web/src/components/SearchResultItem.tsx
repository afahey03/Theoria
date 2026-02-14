import type { SearchResultItem as SearchResultItemType } from '../types';
import './SearchResultItem.css';

interface Props {
    item: SearchResultItemType;
}

/**
 * Renders a single search result with title, URL, snippet, and score.
 * The title links to the original source. The snippet may contain <mark> tags
 * from the engine's snippet generator, rendered via dangerouslySetInnerHTML.
 */
export default function SearchResultItem({ item }: Props) {
    return (
        <div className="search-result-item">
            <h3 className="result-title">
                {item.url ? (
                    <a href={item.url} target="_blank" rel="noopener noreferrer">
                        {item.title}
                    </a>
                ) : (
                    item.title
                )}
            </h3>
            {item.url && <p className="result-url">{item.url}</p>}
            <p
                className="result-snippet"
                dangerouslySetInnerHTML={{ __html: item.snippet }}
            />
            <div className="result-meta">
                <span className="result-score">Score: {item.score.toFixed(3)}</span>
            </div>
        </div>
    );
}
