import { useState } from 'react';
import type { QueryResult, QueryResultSet } from '../api/queryApi';
import './QueryResults.css';

interface QueryResultsProps {
  result: QueryResult | null;
  loading: boolean;
  error: string | null;
  collapsed: boolean;
  onToggleCollapse: () => void;
}

type SortDir = 'asc' | 'desc';

interface SortState {
  resultSetIndex: number;
  column: string | null;
  direction: SortDir;
}

export default function QueryResults({
  result,
  loading,
  error,
  collapsed,
  onToggleCollapse,
}: QueryResultsProps) {
  const [sortState, setSortState] = useState<SortState>({
    resultSetIndex: 0,
    column: null,
    direction: 'asc',
  });

  // Normalize result to always work with resultSets array
  const resultSets: QueryResultSet[] = result
    ? result.resultSets?.length
      ? result.resultSets
      : result.columns && result.rows
      ? [{ columns: result.columns, rows: result.rows, rowCount: result.rowCount || 0 }]
      : []
    : [];

  const totalRows = resultSets.reduce((sum, rs) => sum + rs.rowCount, 0);

  const handleSort = (resultSetIndex: number, colName: string) => {
    if (sortState.resultSetIndex === resultSetIndex && sortState.column === colName) {
      setSortState({
        resultSetIndex,
        column: colName,
        direction: sortState.direction === 'asc' ? 'desc' : 'asc',
      });
    } else {
      setSortState({ resultSetIndex, column: colName, direction: 'asc' });
    }
  };

  const getSortedRows = (resultSet: QueryResultSet, resultSetIndex: number) => {
    if (sortState.resultSetIndex !== resultSetIndex || !sortState.column) {
      return resultSet.rows;
    }
    return [...resultSet.rows].sort((a, b) => {
      const va = a[sortState.column!];
      const vb = b[sortState.column!];
      if (va == null && vb == null) return 0;
      if (va == null) return 1;
      if (vb == null) return -1;
      if (typeof va === 'number' && typeof vb === 'number') {
        return sortState.direction === 'asc' ? va - vb : vb - va;
      }
      const sa = String(va);
      const sb = String(vb);
      return sortState.direction === 'asc' ? sa.localeCompare(sb) : sb.localeCompare(sa);
    });
  };

  const renderResultSet = (resultSet: QueryResultSet, index: number) => {
    const sortedRows = getSortedRows(resultSet, index);
    return (
      <div key={index} className="qr-result-set">
        {resultSets.length > 1 && (
          <div className="qr-result-set-header">
            Result Set {index + 1} ({resultSet.rowCount} row{resultSet.rowCount !== 1 ? 's' : ''})
          </div>
        )}
        <div className="qr-table-wrap">
          <table className="qr-table">
          <thead>
            <tr>
              <th className="qr-row-num">#</th>
              {resultSet.columns.map((col) => (
                <th
                  key={col.name}
                  className="qr-th"
                  onClick={() => handleSort(index, col.name)}
                >
                  {col.name}
                  {sortState.resultSetIndex === index && sortState.column === col.name && (
                    <span className="qr-sort-icon">
                      {sortState.direction === 'asc' ? ' ▲' : ' ▼'}
                    </span>
                  )}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {sortedRows.map((row, i) => (
              <tr key={i} className={i % 2 === 1 ? 'qr-stripe' : ''}>
                <td className="qr-row-num">{i + 1}</td>
                {resultSet.columns.map((col) => (
                  <td key={col.name} className="qr-td">
                    {row[col.name] == null ? 'NULL' : String(row[col.name])}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
        </div>
      </div>
    );
  };

  return (
    <div className={`qr ${collapsed ? 'qr--collapsed' : ''}`}>
      <div className="qr-header" onClick={onToggleCollapse}>
        <span className="qr-header-toggle">{collapsed ? '▲' : '▼'}</span>
        <span className="qr-header-title">Results</span>
        {result && (
          <span className="qr-header-meta">
            {totalRows} row{totalRows !== 1 ? 's' : ''}
            {resultSets.length > 1 && ` (${resultSets.length} sets)`} •{' '}
            {result.executionTimeMs}ms
          </span>
        )}
      </div>

      {!collapsed && (
        <div className="qr-body">
          {loading && (
            <div className="qr-status">
              <div className="qr-spinner" />
              <span>Executing query…</span>
            </div>
          )}

          {error && !loading && (
            <div className="qr-status qr-status--error">
              <span className="qr-error-icon">✖</span>
              <span>{error}</span>
            </div>
          )}

          {!loading && !error && !result && (
            <div className="qr-status">
              <span>Results will appear here</span>
            </div>
          )}

          {!loading && !error && result && totalRows === 0 && (
            <div className="qr-status">
              <span>Query returned 0 rows ({result.executionTimeMs}ms)</span>
            </div>
          )}

          {!loading && !error && result && resultSets.length > 0 && totalRows > 0 && (
            <div className="qr-content qr-content--stacked">
              {resultSets.map((rs, idx) => renderResultSet(rs, idx))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
