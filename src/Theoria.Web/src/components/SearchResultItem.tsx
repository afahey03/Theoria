import type { SearchResultItem as SearchResultItemType } from '../types';
import './SearchResultItem.css';

interface Props {
    item: SearchResultItemType;
}

/**
 * Renders a single search result with title, URL, domain badge, and snippet.
 * Academic/scholarly sources display a green "Academic" badge.
 * The title links to the original source. The snippet may contain <mark> tags
 * from the engine's snippet generator, rendered via dangerouslySetInnerHTML.
 */
export default function SearchResultItem({ item }: Props) {
    return (
        <div className={`search-result-item${item.isScholarly ? ' scholarly' : ''}`}>
            <div className="result-meta-row">
                {item.domain && <span className="result-domain">{item.domain}</span>}
                {item.isScholarly && (
                    <span className="scholarly-badge" title="From a known academic source">
                        <svg className="scholarly-icon" width="12" height="12" viewBox="0 0 24 24" fill="currentColor">
                            <path d="M12 3L1 9l4 2.18v6L12 21l7-3.82v-6l2-1.09V17h2V9L12 3zm6.82 6L12 12.72 5.18 9 12 5.28 18.82 9zM17 15.99l-5 2.73-5-2.73v-3.72L12 15l5-2.73v3.72z" />
                        </svg>
                        Academic
                    </span>
                )}
            </div>
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
        </div>
    );
}
