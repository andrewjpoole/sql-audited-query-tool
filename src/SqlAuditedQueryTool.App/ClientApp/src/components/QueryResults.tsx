import { useState, useEffect } from 'react';
import type { QueryResult, QueryResultSet } from '../api/queryApi';
import ExecutionPlanView from './ExecutionPlanView';
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

type ActiveTabType = number | 'execution-plan';

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
  
  const [activeTab, setActiveTab] = useState<ActiveTabType>(0);

  // Normalize result to always work with resultSets array
  // Filter out empty result sets (0 rows and 0 columns)
  const resultSets: QueryResultSet[] = result
    ? result.resultSets?.length
      ? result.resultSets.filter(rs => rs.rowCount > 0 || rs.columns.length > 0)
      : result.columns && result.rows && result.columns.length > 0
      ? [{ columns: result.columns, rows: result.rows, rowCount: result.rowCount || 0 }]
      : []
    : [];

  const totalRows = resultSets.reduce((sum, rs) => sum + rs.rowCount, 0);
  const hasExecutionPlan = result?.executionPlanXml != null;

  // Auto-switch to execution plan tab if there are no result sets (Estimated mode)
  useEffect(() => {
    if (hasExecutionPlan && resultSets.length === 0) {
      setActiveTab('execution-plan');
    } else if (!hasExecutionPlan || resultSets.length > 0) {
      setActiveTab(0);
    }
  }, [hasExecutionPlan, resultSets.length]);

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

          {!loading && !error && result && totalRows === 0 && !hasExecutionPlan && (
            <div className="qr-status">
              <span>Query returned 0 rows ({result.executionTimeMs}ms)</span>
            </div>
          )}

          {!loading && !error && result && (resultSets.length > 0 || hasExecutionPlan) && (
            <div className="qr-content">
              {/* Tab navigation - show tabs only if we have multiple result sets OR execution plan */}
              {/* Only show result set tabs if there are actual result sets, and plan tab if there's a plan */}
              {(resultSets.length > 1 || (resultSets.length > 0 && hasExecutionPlan)) && (
                <div className="qr-tabs">
                  {resultSets.map((_, idx) => (
                    <button
                      key={idx}
                      className={`qr-tab ${activeTab === idx ? 'qr-tab--active' : ''}`}
                      onClick={() => setActiveTab(idx)}
                    >
                      Result Set {idx + 1}
                    </button>
                  ))}
                  {hasExecutionPlan && (
                    <button
                      className={`qr-tab ${activeTab === 'execution-plan' ? 'qr-tab--active' : ''}`}
                      onClick={() => setActiveTab('execution-plan')}
                    >
                      📊 Execution Plan
                    </button>
                  )}
                </div>
              )}
              
              {/* Tab content */}
              <div className="qr-tab-content">
                {activeTab === 'execution-plan' ? (
                  result.executionPlanXml && (
                    <ExecutionPlanView planXml={result.executionPlanXml} />
                  )
                ) : (
                  // Show result sets in stacked view if only one result set and no execution plan tabs
                  (resultSets.length === 1 && !hasExecutionPlan) ? (
                    <div className="qr-content-stacked">
                      {resultSets.map((rs, idx) => renderResultSet(rs, idx))}
                    </div>
                  ) : resultSets.length > 0 ? (
                    renderResultSet(resultSets[activeTab as number], activeTab as number)
                  ) : (
                    <div className="qr-status">
                      <span>No result sets returned - see Execution Plan tab</span>
                    </div>
                  )
                )}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
