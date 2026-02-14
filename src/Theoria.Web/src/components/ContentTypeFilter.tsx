import { ContentType } from '../types';
import './ContentTypeFilter.css';

interface Props {
    value: ContentType | undefined;
    onChange: (value: ContentType | undefined) => void;
}

/**
 * Dropdown filter to restrict search results to a specific document type.
 */
export default function ContentTypeFilter({ value, onChange }: Props) {
    return (
        <div className="content-type-filter">
            <label htmlFor="contentType">Filter by type:</label>
            <select
                id="contentType"
                value={value ?? ''}
                onChange={(e) =>
                    onChange(e.target.value ? (e.target.value as ContentType) : undefined)
                }
            >
                <option value="">All types</option>
                <option value={ContentType.Pdf}>PDF</option>
                <option value={ContentType.Markdown}>Markdown</option>
                <option value={ContentType.Html}>HTML</option>
            </select>
        </div>
    );
}
