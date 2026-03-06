import { useState, useRef, useEffect } from 'react';
import Editor, { type OnMount } from '@monaco-editor/react';
import type * as Monaco from 'monaco-editor';
import ExecutionPlanView from './ExecutionPlanView';
import ScriptGeneratorModal from './ScriptGeneratorModal';
import { simulateQuery } from '../api/queryApi';
import type { SimulationResult } from '../api/queryApi';
import './WriteScriptSimulator.css';

interface WriteScriptSimulatorProps {
  editorHeight: number;
  onEditorResize: (e: React.MouseEvent) => void;
}

export default function WriteScriptSimulator({ editorHeight, onEditorResize }: WriteScriptSimulatorProps) {
  const [sql, setSql] = useState('');
  const [simulating, setSimulating] = useState(false);
  const [result, setResult] = useState<SimulationResult | null>(null);
  const [showScriptModal, setShowScriptModal] = useState(false);
  const completionDisposableRef = useRef<Monaco.IDisposable | null>(null);

  useEffect(() => {
    return () => {
      completionDisposableRef.current?.dispose();
    };
  }, []);

  const handleMount: OnMount = (editor, monaco) => {
    // Register schema completion provider (same as TabbedSqlEditor)
    const completionDisposable = monaco.languages.registerCompletionItemProvider('sql', {
      triggerCharacters: ['.', ' '],
      provideCompletionItems: async (
        model: Monaco.editor.ITextModel,
        position: Monaco.Position
      ) => {
        try {
          const wordInfo = model.getWordUntilPosition(position);
          const range = {
            startLineNumber: position.lineNumber,
            startColumn: wordInfo.startColumn,
            endLineNumber: position.lineNumber,
            endColumn: wordInfo.endColumn,
          };
          const textUntilPosition = model.getValueInRange({
            startLineNumber: 1,
            startColumn: 1,
            endLineNumber: position.lineNumber,
            endColumn: position.column,
          });
          const currentLine = model.getLineContent(position.lineNumber);

          const response = await fetch('/api/completions/schema', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              prefix: textUntilPosition,
              context: currentLine,
              cursorLine: position.lineNumber,
            }),
          });

          if (!response.ok) return { suggestions: [] };

          const completions = await response.json();
          const suggestions = completions.map((item: { label: string; kind?: number; detail?: string; documentation?: string }) => ({
            label: item.label,
            kind: item.kind || monaco.languages.CompletionItemKind.Field,
            insertText: item.label,
            detail: item.detail,
            documentation: item.documentation,
            range,
          }));

          return { suggestions };
        } catch {
          return { suggestions: [] };
        }
      },
    });
    completionDisposableRef.current = completionDisposable;

    // Add Ctrl+Enter to simulate
    editor.addAction({
      id: 'simulate-query',
      label: 'Simulate Query',
      keybindings: [monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter],
      run: () => handleSimulate(),
    });
  };

  const handleSimulate = async () => {
    const trimmed = sql.trim();
    if (!trimmed || simulating) return;

    setSimulating(true);
    try {
      const simulationResult = await simulateQuery(trimmed);
      setResult(simulationResult);
    } catch (err) {
      setResult({
        isValid: false,
        validationErrors: [err instanceof Error ? err.message : 'Simulation failed'],
        warnings: [],
        estimatedAffectedRows: null,
        executionPlanXml: null,
        executionMilliseconds: 0,
        succeeded: false,
        errorMessage: err instanceof Error ? err.message : 'Simulation failed',
      });
    } finally {
      setSimulating(false);
    }
  };

  const getAffectedRowsColor = (count: number | null): string => {
    if (count === null) return 'var(--text-secondary)';
    if (count <= 10) return 'var(--success)';
    if (count <= 100) return 'var(--sim-accent-light)';
    return '#ef4444';
  };

  const getOperationType = (): 'UPDATE' | 'INSERT' | 'DELETE' | null => {
    const trimmed = sql.trim().toUpperCase();
    if (trimmed.startsWith('UPDATE')) return 'UPDATE';
    if (trimmed.startsWith('INSERT')) return 'INSERT';
    if (trimmed.startsWith('DELETE')) return 'DELETE';
    return null;
  };

  const formatAffectedRowsText = (count: number | null): string => {
    if (count === null) return 'Unknown';
    const operation = getOperationType();
    const rowWord = count === 1 ? 'row' : 'rows';
    
    if (operation === 'UPDATE') return `${count.toLocaleString()} ${rowWord} updated`;
    if (operation === 'DELETE') return `${count.toLocaleString()} ${rowWord} deleted`;
    if (operation === 'INSERT') return `${count.toLocaleString()} ${rowWord} inserted`;
    return `${count.toLocaleString()} ${rowWord} affected`;
  };

  return (
    <>
      <div className="editor-panel write-simulator-editor" style={{ height: `${editorHeight}px` }}>
        <div className="write-simulator-banner">
          ⚠️ SIMULATION MODE — queries are analysed but never executed
        </div>
        <div className="write-simulator-toolbar">
          <button
            className="write-simulator-btn write-simulator-btn--simulate"
            onClick={handleSimulate}
            disabled={simulating || !sql.trim()}
          >
            {simulating ? '⏳ Simulating...' : '🔬 Simulate'}
          </button>
          <button
            className="write-simulator-btn write-simulator-btn--create"
            onClick={() => setShowScriptModal(true)}
            disabled={!result || !result.isValid}
          >
            📄 Create sql-script-runner scripts
          </button>
        </div>
        <Editor
          height="100%"
          defaultLanguage="sql"
          theme="vs-dark"
          value={sql}
          onChange={(value) => setSql(value || '')}
          onMount={handleMount}
          options={{
            minimap: { enabled: false },
            lineNumbers: 'on',
            wordWrap: 'on',
            fontSize: 14,
            scrollBeyondLastLine: false,
            automaticLayout: true,
            padding: { top: 8 },
            tabSize: 4,
          }}
        />
      </div>

      <div className="resize-handle" onMouseDown={onEditorResize}>
        <div className="resize-handle-bar" />
      </div>

      <div className="results-panel">
        {!result ? (
          <div className="write-simulator-empty">
            <p>Write an UPDATE, INSERT, or DELETE statement above and click <strong>Simulate</strong> to analyse it without executing.</p>
          </div>
        ) : (
          <div className="write-simulator-results">
            {result.validationErrors.length > 0 && (
              <div className="write-simulator-section write-simulator-section--error">
                <h3>❌ Validation Errors</h3>
                <ul>
                  {result.validationErrors.map((error, idx) => (
                    <li key={idx}>{error}</li>
                  ))}
                </ul>
              </div>
            )}

            {result.warnings.length > 0 && (
              <div className="write-simulator-section write-simulator-section--warning">
                <h3>⚠️ Warnings</h3>
                <ul>
                  {result.warnings.map((warning, idx) => (
                    <li key={idx}>{warning}</li>
                  ))}
                </ul>
              </div>
            )}

            {result.estimatedAffectedRows !== null && (
              <div className="write-simulator-section">
                <h3>📊 Estimated Affected Rows</h3>
                <div
                  className="write-simulator-affected-rows"
                  style={{ color: getAffectedRowsColor(result.estimatedAffectedRows) }}
                >
                  {formatAffectedRowsText(result.estimatedAffectedRows)}
                </div>
              </div>
            )}

            {result.executionPlanXml && (
              <div className="write-simulator-section">
                <h3>📈 Execution Plan</h3>
                <ExecutionPlanView planXml={result.executionPlanXml} />
              </div>
            )}

            <div className="write-simulator-footer">
              Execution time: {result.executionMilliseconds}ms
            </div>
          </div>
        )}
      </div>

      {showScriptModal && result && (
        <ScriptGeneratorModal
          sql={sql}
          estimatedAffectedRows={result.estimatedAffectedRows}
          onClose={() => setShowScriptModal(false)}
        />
      )}
    </>
  );
}
