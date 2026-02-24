import { useState, useCallback, useRef } from 'react';
import TabbedSqlEditor from './components/TabbedSqlEditor';
import type { SqlEditorHandle } from './components/TabbedSqlEditor';
import QueryResults from './components/QueryResults';
import ChatPanel from './components/ChatPanel';
import QueryHistory from './components/QueryHistory';
import SchemaTreeView from './components/SchemaTreeView';
import { executeQuery } from './api/queryApi';
import type { QueryResult, ChatMessage } from './api/queryApi';
import type { HistoryEntry } from './components/QueryHistory';
import { useChatHistory } from './hooks/useChatHistory';
import { useVerticalResize } from './hooks/useVerticalResize';
import './App.css';

export default function App() {
  const [sql, setSql] = useState('');

  // Query results state - now stored per tab
  const [tabResults, setTabResults] = useState<Record<string, QueryResult | null>>({});
  const [activeTabId, setActiveTabId] = useState<string>('default');
  const [queryLoading, setQueryLoading] = useState(false);
  const [queryError, setQueryError] = useState<string | null>(null);
  const [resultsCollapsed, setResultsCollapsed] = useState(false);
  
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
    initialHeight: 400,
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
      const result = await executeQuery(trimmed);
      console.log(`Frontend: Received ${result.resultSets?.length || 0} result set(s) from backend`);
      if (result.resultSets?.length) {
        result.resultSets.forEach((rs, idx) => {
          console.log(`  Result set ${idx + 1}: ${rs.rowCount} rows, ${rs.columns.length} columns`);
        });
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
  }, [sql]);

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
      const result = await executeQuery(trimmed);
      console.log(`Frontend: Received ${result.resultSets?.length || 0} result set(s) from backend`);
      if (result.resultSets?.length) {
        result.resultSets.forEach((rs, idx) => {
          console.log(`  Result set ${idx + 1}: ${rs.rowCount} rows, ${rs.columns.length} columns`);
        });
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
  }, []);

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
          const result = await executeQuery(newSql);
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
    [],
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

  // Handle tab changes
  const handleActiveTabChange = useCallback((tabId: string) => {
    setActiveTabId(tabId);
  }, []);

  return (
    <div className="app">
      <header className="app-header">
        <h1>SQL Audited Query Tool</h1>
        <span className="app-header-badge">Read-Only</span>
      </header>

      <div className="main-area">
        <SchemaTreeView onInsertText={handleInsertText} />

        <QueryHistory entries={history} onSelect={handleHistorySelect} />

        <div className="center-area">
          <div className="editor-panel" style={{ height: `${editorHeight}px` }}>
            <TabbedSqlEditor 
              ref={editorRef} 
              value={sql} 
              onChange={setSql} 
              onExecute={handleExecute}
              onExecuteSelection={handleExecuteSelection}
              onActiveTabChange={handleActiveTabChange}
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
        </div>

        <ChatPanel
          onInsertSql={handleInsertSql}
          onInsertAndExecute={handleInsertAndExecute}
          onAiExecutedQuery={handleAiExecutedQuery}
          sessions={sessions}
          currentSessionId={currentSessionId}
          onNewSession={handleNewChatSession}
          onLoadSession={handleLoadChatSession}
          onDeleteSession={handleDeleteChatSession}
          onUpdateSession={handleUpdateChatSession}
        />
      </div>
    </div>
  );
}
