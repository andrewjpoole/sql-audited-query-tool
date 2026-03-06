import { useState, useCallback, useRef } from 'react';
import TabbedSqlEditor from './components/TabbedSqlEditor';
import type { SqlEditorHandle } from './components/TabbedSqlEditor';
import QueryResults from './components/QueryResults';
import ChatPanel from './components/ChatPanel';
import QueryHistory from './components/QueryHistory';
import SchemaTreeView from './components/SchemaTreeView';
import WriteScriptSimulator from './components/WriteScriptSimulator';
import { executeQuery } from './api/queryApi';
import type { QueryResult, ChatMessage } from './api/queryApi';
import type { HistoryEntry } from './components/QueryHistory';
import { useChatHistory } from './hooks/useChatHistory';
import { useVerticalResize } from './hooks/useVerticalResize';
import './App.css';

export default function App() {
  const [sql, setSql] = useState('');
  const [appMode, setAppMode] = useState<'query' | 'simulator'>('query');
  const [simulatorSql, setSimulatorSql] = useState('');

  // Audit trail context
  const [gitHubIssueNumber, setGitHubIssueNumber] = useState<number | undefined>(undefined);
  const [azDoWorkItemId, setAzDoWorkItemId] = useState<number | undefined>(undefined);

  // Query results state - now stored per tab
  const [tabResults, setTabResults] = useState<Record<string, QueryResult | null>>({});
  const [activeTabId, setActiveTabId] = useState<string>('default');
  const [queryLoading, setQueryLoading] = useState(false);
  const [queryError, setQueryError] = useState<string | null>(null);
  const [resultsCollapsed, setResultsCollapsed] = useState(false);
  
  // Execution plan state
  const [executionPlanMode, setExecutionPlanMode] = useState<'None' | 'Estimated' | 'Actual'>('None');
  
  // Current tab's result
  const queryResult = tabResults[activeTabId] || null;
  
  // Ref to prevent duplicate executions during async operations
  const executingRef = useRef(false);

  // Chat session management
  const {
    sessions,
    currentSessionId,
    createNewSession,
    loadSession,
    updateSession,
    deleteSession,
  } = useChatHistory();

  // Query history state
  const [history, setHistory] = useState<HistoryEntry[]>([]);

  // Editor ref for text insertion
  const editorRef = useRef<SqlEditorHandle>(null);

  // Vertical resize between editor and results
  const { height: editorHeight, handleMouseDown: handleEditorResize } = useVerticalResize({
    initialHeight: 500,
    minHeight: 200,
    maxHeight: 800,
    storageKey: 'editorPanelHeight',
    direction: 'down',
  });

  const handleInsertText = useCallback((text: string) => {
    editorRef.current?.insertTextAtCursor(text);
  }, []);

  const handleExecute = useCallback(async () => {
    const trimmed = sql.trim();
    if (!trimmed || executingRef.current) return;

    // Get current active tab ID
    const currentTabId = editorRef.current?.getActiveTabId() || 'default';

    executingRef.current = true;
    setQueryLoading(true);
    setQueryError(null);
    setResultsCollapsed(false);

    try {
      const result = await executeQuery(trimmed, executionPlanMode, gitHubIssueNumber, azDoWorkItemId);
      console.log(`Frontend: Received ${result.resultSets?.length || 0} result set(s) from backend`);
      if (result.resultSets?.length) {
        result.resultSets.forEach((rs, idx) => {
          console.log(`  Result set ${idx + 1}: ${rs.rowCount} rows, ${rs.columns.length} columns`);
        });
      }
      if (result.executionPlanXml) {
        console.log('Frontend: Received execution plan XML');
      }
      setTabResults((prev) => ({ ...prev, [currentTabId]: result }));
      const totalRows = result.resultSets?.length
        ? result.resultSets.reduce((sum, rs) => sum + rs.rowCount, 0)
        : result.rowCount ?? 0;
      setHistory((prev) => [
        ...prev,
        {
          sql: trimmed,
          timestamp: new Date().toISOString(),
          rowCount: totalRows,
          source: 'user',
        },
      ]);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Query execution failed';
      setQueryError(errorMessage);
      setTabResults((prev) => ({ ...prev, [currentTabId]: null }));
      setHistory((prev) => [
        ...prev,
        { sql: trimmed, timestamp: new Date().toISOString(), rowCount: null, source: 'user' },
      ]);
      // Show error in editor
      editorRef.current?.setError(errorMessage);
    } finally {
      setQueryLoading(false);
      executingRef.current = false;
    }
  }, [sql, executionPlanMode, gitHubIssueNumber, azDoWorkItemId]);

  const handleExecuteSelection = useCallback(async (selection: string) => {
    const trimmed = selection.trim();
    if (!trimmed || executingRef.current) return;

    // Get current active tab ID
    const currentTabId = editorRef.current?.getActiveTabId() || 'default';

    executingRef.current = true;
    setQueryLoading(true);
    setQueryError(null);
    setResultsCollapsed(false);

    try {
      const result = await executeQuery(trimmed, executionPlanMode, gitHubIssueNumber, azDoWorkItemId);
      console.log(`Frontend: Received ${result.resultSets?.length || 0} result set(s) from backend`);
      if (result.resultSets?.length) {
        result.resultSets.forEach((rs, idx) => {
          console.log(`  Result set ${idx + 1}: ${rs.rowCount} rows, ${rs.columns.length} columns`);
        });
      }
      if (result.executionPlanXml) {
        console.log('Frontend: Received execution plan XML');
      }
      setTabResults((prev) => ({ ...prev, [currentTabId]: result }));
      const totalRows = result.resultSets?.length
        ? result.resultSets.reduce((sum, rs) => sum + rs.rowCount, 0)
        : result.rowCount ?? 0;
      setHistory((prev) => [
        ...prev,
        {
          sql: trimmed,
          timestamp: new Date().toISOString(),
          rowCount: totalRows,
          source: 'user',
        },
      ]);
    } catch (err) {
      setQueryError(err instanceof Error ? err.message : 'Query execution failed');
      setTabResults((prev) => ({ ...prev, [currentTabId]: null }));
      setHistory((prev) => [
        ...prev,
        { sql: trimmed, timestamp: new Date().toISOString(), rowCount: null, source: 'user' },
      ]);
    } finally {
      setQueryLoading(false);
      executingRef.current = false;
    }
  }, [executionPlanMode, gitHubIssueNumber, azDoWorkItemId]);

  const handleInsertSql = useCallback((newSql: string) => {
    editorRef.current?.insertTextAtCursor(newSql);
  }, []);

  const handleInsertAndExecute = useCallback(
    (newSql: string) => {
      editorRef.current?.setValue(newSql);
      // Execute after setting value via microtask
      queueMicrotask(async () => {
        if (executingRef.current) return;
        
        // Get current active tab ID
        const currentTabId = editorRef.current?.getActiveTabId() || 'default';
        
        executingRef.current = true;
        setQueryLoading(true);
        setQueryError(null);
        setResultsCollapsed(false);
        try {
          const result = await executeQuery(newSql, executionPlanMode, gitHubIssueNumber, azDoWorkItemId);
          setTabResults((prev) => ({ ...prev, [currentTabId]: result }));
          const totalRows = result.resultSets?.length
            ? result.resultSets.reduce((sum, rs) => sum + rs.rowCount, 0)
            : result.rowCount ?? 0;
          setHistory((prev) => [
            ...prev,
            {
              sql: newSql,
              timestamp: new Date().toISOString(),
              rowCount: totalRows,
              source: 'user',
            },
          ]);
        } catch (err) {
          setQueryError(
            err instanceof Error ? err.message : 'Query execution failed',
          );
          setTabResults((prev) => ({ ...prev, [currentTabId]: null }));
        } finally {
          setQueryLoading(false);
          executingRef.current = false;
        }
      });
    },
    [executionPlanMode, gitHubIssueNumber, azDoWorkItemId],
  );

  const handleHistorySelect = useCallback((selectedSql: string) => {
    editorRef.current?.setValue(selectedSql);
  }, []);

  // Handle AI-executed queries
  const handleAiExecutedQuery = useCallback((executedSql: string, result: QueryResult) => {
    // Get current active tab ID
    const currentTabId = editorRef.current?.getActiveTabId() || 'default';
    
    editorRef.current?.setValue(executedSql);
    setTabResults((prev) => ({ ...prev, [currentTabId]: result }));
    setResultsCollapsed(false);
    const totalRows = result.resultSets?.length
      ? result.resultSets.reduce((sum, rs) => sum + rs.rowCount, 0)
      : result.rowCount ?? 0;
    setHistory((prev) => [
      ...prev,
      {
        sql: executedSql,
        timestamp: new Date().toISOString(),
        rowCount: totalRows,
        source: 'ai',
      },
    ]);
  }, []);

  // Chat session handlers
  const handleNewChatSession = useCallback(() => {
    return createNewSession();
  }, [createNewSession]);

  const handleLoadChatSession = useCallback((sessionId: string) => {
    loadSession(sessionId);
  }, [loadSession]);

  const handleUpdateChatSession = useCallback((sessionId: string, messages: ChatMessage[]) => {
    updateSession(sessionId, messages);
  }, [updateSession]);

  const handleDeleteChatSession = useCallback((sessionId: string) => {
    deleteSession(sessionId);
  }, [deleteSession]);

  const handleSendToSimulator = useCallback((fixSql: string) => {
    setSimulatorSql(fixSql);
    setAppMode('simulator');
  }, []);

  // Handle tab changes
  const handleActiveTabChange = useCallback((tabId: string) => {
    setActiveTabId(tabId);
  }, []);
  
  // Handle execution plan mode changes
  const handleExecutionPlanModeChange = useCallback((mode: 'None' | 'Estimated' | 'Actual') => {
    setExecutionPlanMode(mode);
  }, []);

  return (
    <div className="app">
      <header className="app-header">
        <h1>SQL Audited Query Tool</h1>
        <div className="app-mode-toggle">
          <button 
            className={`mode-btn ${appMode === 'query' ? 'mode-btn--active' : ''}`}
            onClick={() => setAppMode('query')}
          >
            Read Query
          </button>
          <button 
            className={`mode-btn ${appMode === 'simulator' ? 'mode-btn--active mode-btn--simulator' : ''}`}
            onClick={() => setAppMode('simulator')}
          >
            Write Query Simulator
          </button>
        </div>
        <div className="audit-trail-inputs">
          <span className="audit-trail-label">Audit:</span>
          <input
            type="number"
            className="audit-trail-input"
            placeholder="GitHub Issue #"
            value={gitHubIssueNumber ?? ''}
            onChange={(e) => setGitHubIssueNumber(e.target.value ? Number(e.target.value) : undefined)}
            min={1}
          />
          <input
            type="number"
            className="audit-trail-input"
            placeholder="AzDO Work Item #"
            value={azDoWorkItemId ?? ''}
            onChange={(e) => setAzDoWorkItemId(e.target.value ? Number(e.target.value) : undefined)}
            min={1}
          />
        </div>
      </header>

      <div className="main-area">
        <SchemaTreeView onInsertText={appMode === 'query' ? handleInsertText : () => {}} />

        {appMode === 'query' && (
          <QueryHistory entries={history} onSelect={handleHistorySelect} />
        )}

        <div className="center-area">
          {appMode === 'query' ? (
            <>
              <div className="editor-panel" style={{ height: `${editorHeight}px` }}>
                <TabbedSqlEditor 
                  ref={editorRef} 
                  value={sql} 
                  onChange={setSql} 
                  onExecute={handleExecute}
                  onExecuteSelection={handleExecuteSelection}
                  onActiveTabChange={handleActiveTabChange}
                  onShowPlanChange={handleExecutionPlanModeChange}
                />
              </div>

              <div className="resize-handle" onMouseDown={handleEditorResize}>
                <div className="resize-handle-bar" />
              </div>

              <div className="results-panel">
                <QueryResults
                  result={queryResult}
                  loading={queryLoading}
                  error={queryError}
                  collapsed={resultsCollapsed}
                  onToggleCollapse={() => setResultsCollapsed((v) => !v)}
                />
              </div>
            </>
          ) : (
            <WriteScriptSimulator editorHeight={editorHeight} onEditorResize={handleEditorResize} externalSql={simulatorSql} />
          )}
        </div>

        <ChatPanel
          appMode={appMode}
          onInsertSql={handleInsertSql}
          onInsertAndExecute={handleInsertAndExecute}
          onSendToSimulator={handleSendToSimulator}
          onAiExecutedQuery={handleAiExecutedQuery}
          sessions={sessions}
          currentSessionId={currentSessionId}
          onNewSession={handleNewChatSession}
          onLoadSession={handleLoadChatSession}
          onDeleteSession={handleDeleteChatSession}
          onUpdateSession={handleUpdateChatSession}
          gitHubIssueNumber={gitHubIssueNumber}
          azDoWorkItemId={azDoWorkItemId}
        />
      </div>
    </div>
  );
}
