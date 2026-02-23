import { useHorizontalResize } from '../hooks/useHorizontalResize';
import './QueryHistory.css';

export interface HistoryEntry {
  sql: string;
  timestamp: string;
  rowCount: number | null;
  source?: 'user' | 'ai';
}

interface QueryHistoryProps {
  entries: HistoryEntry[];
  onSelect: (sql: string) => void;
}

function firstLine(sql: string): string {
  const line = sql.trim().split('\n')[0] ?? '';
  return line.length > 60 ? line.slice(0, 57) + '…' : line;
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString();
}

export default function QueryHistory({ entries, onSelect }: QueryHistoryProps) {
  const { width, handleMouseDown } = useHorizontalResize({
    initialWidth: 260,
    minWidth: 200,
    maxWidth: 600,
    storageKey: 'queryHistoryWidth',
  });

  if (entries.length === 0) {
    return (
      <div className="qh-empty">No queries executed yet in this session.</div>
    );
  }

  return (
    <div className="qh" style={{ width: `${width}px` }}>
      <div className="qh-resize-handle" onMouseDown={handleMouseDown} />
      <div className="qh-title">Query History</div>
      <ul className="qh-list">
        {[...entries].reverse().map((entry, i) => (
          <li
            key={i}
            className="qh-item"
            onClick={() => onSelect(entry.sql)}
            title={entry.sql}
          >
            <div className="qh-item-header">
              <div className="qh-item-sql">{firstLine(entry.sql)}</div>
              {entry.source && (
                <span className={`qh-item-badge qh-item-badge--${entry.source}`}>
                  {entry.source === 'ai' ? '🤖' : '👤'}
                </span>
              )}
            </div>
            <div className="qh-item-meta">
              <span>{formatTime(entry.timestamp)}</span>
              {entry.rowCount != null && (
                <span>
                  {entry.rowCount} row{entry.rowCount !== 1 ? 's' : ''}
                </span>
              )}
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
