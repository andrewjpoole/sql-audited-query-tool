import { useState, useCallback, useImperativeHandle, forwardRef, useEffect, useRef } from 'react';
import Editor, { type OnMount } from '@monaco-editor/react';
import type * as Monaco from 'monaco-editor';
import { v4 as uuidv4 } from 'uuid';
import './TabbedSqlEditor.css';

export interface SqlEditorHandle {
  insertTextAtCursor: (text: string) => void;
  setValue: (text: string) => void;
  executeQuery: () => void;
  executeSelection: () => void;
  getActiveTabId: () => string;
}

interface SqlEditorProps {
  value: string;
  onChange: (value: string) => void;
  onExecute: () => void;
  onExecuteSelection: (selection: string) => void;
  onActiveTabChange?: (tabId: string) => void;
}

interface QueryTab {
  id: string;
  name: string;
  path: string;
  defaultValue: string;
  isDirty: boolean;
}

function formatDate(date: Date): string {
  const pad = (n: number) => n.toString().padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
}

function insertText(editor: Monaco.editor.ICodeEditor, text: string) {
  const selection = editor.getSelection();
  if (!selection) return;
  editor.executeEdits('sql-helper', [
    { range: selection, text, forceMoveMarkers: true },
  ]);
  editor.focus();
}

const TabbedSqlEditor = forwardRef<SqlEditorHandle, SqlEditorProps>(function TabbedSqlEditor({ value, onChange, onExecute, onExecuteSelection, onActiveTabChange }, ref) {
  const [editorRef, setEditorRef] = useState<Monaco.editor.IStandaloneCodeEditor | null>(null);
  const [monacoRef, setMonacoRef] = useState<typeof Monaco | null>(null);
  const completionDisposableRef = useRef<Monaco.IDisposable | null>(null);
  
  const [tabs, setTabs] = useState<QueryTab[]>([
    {
      id: 'default',
      name: 'Query 1',
      path: 'query-1.sql',
      defaultValue: value,
      isDirty: false,
    },
  ]);
  const [activeTabId, setActiveTabId] = useState('default');

  const activeTab = tabs.find((t) => t.id === activeTabId) || tabs[0];

  useImperativeHandle(ref, () => ({
    insertTextAtCursor(text: string) {
      if (!editorRef) return;
      const selection = editorRef.getSelection();
      if (!selection) return;
      editorRef.executeEdits('schema-insert', [
        { range: selection, text, forceMoveMarkers: true },
      ]);
      editorRef.focus();
    },
    setValue(text: string) {
      if (!editorRef) return;
      const model = editorRef.getModel();
      if (!model) return;
      model.setValue(text);
      editorRef.focus();
    },
    executeQuery() {
      onExecute();
    },
    executeSelection() {
      if (!editorRef) return;
      const selection = editorRef.getSelection();
      if (!selection) return;
      const model = editorRef.getModel();
      if (!model) return;
      const selectedText = model.getValueInRange(selection);
      if (selectedText.trim()) {
        onExecuteSelection(selectedText);
      } else {
        onExecute();
      }
    },
    getActiveTabId() {
      return activeTabId;
    },
  }));

  // Cleanup completion provider on unmount
  useEffect(() => {
    return () => {
      if (completionDisposableRef.current) {
        completionDisposableRef.current.dispose();
      }
    };
  }, []);

  const handleMount: OnMount = useCallback((editor, monaco) => {
    setEditorRef(editor);
    setMonacoRef(monaco);

    // Register schema completion provider
    const completionDisposable = monaco.languages.registerCompletionItemProvider('sql', {
      triggerCharacters: ['.', ' '],
      provideCompletionItems: async (
        model: Monaco.editor.ITextModel,
        position: Monaco.Position
      ) => {
        try {
          // Get text before cursor for context
          const textUntilPosition = model.getValueInRange({
            startLineNumber: 1,
            startColumn: 1,
            endLineNumber: position.lineNumber,
            endColumn: position.column,
          });

          // Get current line for additional context
          const currentLine = model.getLineContent(position.lineNumber);

          // Call backend completion API
          const response = await fetch('/api/completions/schema', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              prefix: textUntilPosition,
              context: currentLine,
              cursorLine: position.lineNumber,
            }),
          });

          if (!response.ok) {
            // Graceful degradation - return empty on error
            return { suggestions: [] };
          }

          const completions = await response.json();

          // Transform backend response to Monaco completion items
          const suggestions = completions.map((item: any) => ({
            label: item.label,
            kind: item.kind || monaco.languages.CompletionItemKind.Field,
            insertText: item.insertText || item.label,
            detail: item.detail,
            documentation: item.documentation,
            range: {
              startLineNumber: position.lineNumber,
              startColumn: position.column,
              endLineNumber: position.lineNumber,
              endColumn: position.column,
            },
          }));

          return { suggestions };
        } catch (error) {
          // Graceful degradation - silently fail and return empty
          console.debug('Completion provider error:', error);
          return { suggestions: [] };
        }
      },
    });

    // Store disposable for cleanup
    completionDisposableRef.current = completionDisposable;

    const CONTEXT_GROUP = '9_sql_helpers';

    // Execute Query - F7
    editor.addAction({
      id: 'sql-execute-query',
      label: 'Execute Query',
      keybindings: [monaco.KeyCode.F7],
      contextMenuGroupId: 'navigation',
      contextMenuOrder: 1,
      run: () => {
        onExecute();
      },
    });

    // Run Selection - F8
    editor.addAction({
      id: 'sql-run-selection',
      label: 'Run Selection',
      keybindings: [monaco.KeyCode.F8],
      contextMenuGroupId: 'navigation',
      contextMenuOrder: 2,
      run: (ed) => {
        const selection = ed.getSelection();
        if (!selection) return;
        const model = ed.getModel();
        if (!model) return;
        const selectedText = model.getValueInRange(selection);
        if (selectedText.trim()) {
          onExecuteSelection(selectedText);
        } else {
          onExecute();
        }
      },
    });

    editor.addAction({
      id: 'sql-insert-current-date',
      label: 'SQL Helpers: Insert Current Date',
      keybindings: [monaco.KeyMod.Alt | monaco.KeyCode.KeyD],
      contextMenuGroupId: CONTEXT_GROUP,
      contextMenuOrder: 1,
      run: (ed) => insertText(ed, `'${formatDate(new Date())}'`),
    });

    editor.addAction({
      id: 'sql-insert-new-guid',
      label: 'SQL Helpers: Insert New GUID',
      keybindings: [monaco.KeyMod.Alt | monaco.KeyCode.KeyG],
      contextMenuGroupId: CONTEXT_GROUP,
      contextMenuOrder: 2,
      run: (ed) => insertText(ed, `'${uuidv4()}'`),
    });

    editor.addAction({
      id: 'sql-insert-getdate',
      label: 'SQL Helpers: Insert GETDATE()',
      contextMenuGroupId: CONTEXT_GROUP,
      contextMenuOrder: 3,
      run: (ed) => insertText(ed, 'GETDATE()'),
    });

    editor.addAction({
      id: 'sql-insert-newid',
      label: 'SQL Helpers: Insert NEWID()',
      contextMenuGroupId: CONTEXT_GROUP,
      contextMenuOrder: 4,
      run: (ed) => insertText(ed, 'NEWID()'),
    });

    editor.addAction({
      id: 'sql-wrap-in-select',
      label: 'SQL Helpers: Wrap in SELECT',
      keybindings: [monaco.KeyMod.Alt | monaco.KeyCode.KeyS],
      contextMenuGroupId: CONTEXT_GROUP,
      contextMenuOrder: 5,
      run: (ed) => {
        const selection = ed.getSelection();
        if (!selection) return;
        const selectedText = ed.getModel()?.getValueInRange(selection) ?? '*';
        const wrapped = `SELECT ${selectedText}\nFROM `;
        ed.executeEdits('sql-helper', [
          { range: selection, text: wrapped, forceMoveMarkers: true },
        ]);
        ed.focus();
      },
    });

    editor.addAction({
      id: 'sql-comment-selection',
      label: 'SQL Helpers: Toggle Comment',
      keybindings: [monaco.KeyMod.CtrlCmd | monaco.KeyCode.Slash],
      contextMenuGroupId: CONTEXT_GROUP,
      contextMenuOrder: 6,
      run: (ed) => {
        const selection = ed.getSelection();
        if (!selection) return;
        const model = ed.getModel();
        if (!model) return;

        const startLine = selection.startLineNumber;
        const endLine = selection.endLineNumber;
        const lines: string[] = [];
        for (let i = startLine; i <= endLine; i++) {
          lines.push(model.getLineContent(i));
        }

        const allCommented = lines.every((l) => l.trimStart().startsWith('--'));
        const newLines = allCommented
          ? lines.map((l) => l.replace(/^(\s*)--\s?/, '$1'))
          : lines.map((l) => `-- ${l}`);

        const range = {
          startLineNumber: startLine,
          startColumn: 1,
          endLineNumber: endLine,
          endColumn: model.getLineMaxColumn(endLine),
        };
        ed.executeEdits('sql-helper', [
          { range, text: newLines.join('\n'), forceMoveMarkers: true },
        ]);
        ed.focus();
      },
    });

    editor.focus();
  }, [onExecute, onExecuteSelection]);

  const handleEditorChange = useCallback((newValue: string | undefined) => {
    const val = newValue ?? '';
    onChange(val);
    
    // Mark tab as dirty
    setTabs((prev) =>
      prev.map((tab) =>
        tab.id === activeTabId ? { ...tab, isDirty: true } : tab
      )
    );
  }, [activeTabId, onChange]);

  const handleNewTab = useCallback(() => {
    const newTabNumber = tabs.length + 1;
    const newTab: QueryTab = {
      id: uuidv4(),
      name: `Query ${newTabNumber}`,
      path: `query-${newTabNumber}.sql`,
      defaultValue: '',
      isDirty: false,
    };
    setTabs((prev) => [...prev, newTab]);
    setActiveTabId(newTab.id);
  }, [tabs.length]);

  const handleCloseTab = useCallback((tabId: string, e: React.MouseEvent) => {
    e.stopPropagation();
    
    const tab = tabs.find((t) => t.id === tabId);
    if (tab?.isDirty) {
      if (!confirm(`"${tab.name}" has unsaved changes. Close anyway?`)) {
        return;
      }
    }

    setTabs((prev) => {
      const newTabs = prev.filter((t) => t.id !== tabId);
      if (newTabs.length === 0) {
        // Keep at least one tab
        return [{
          id: 'default',
          name: 'Query 1',
          path: 'query-1.sql',
          defaultValue: '',
          isDirty: false,
        }];
      }
      return newTabs;
    });

    // Switch to another tab if closing the active one
    if (tabId === activeTabId) {
      const currentIndex = tabs.findIndex((t) => t.id === tabId);
      const nextTab = tabs[currentIndex + 1] || tabs[currentIndex - 1] || tabs[0];
      if (nextTab && nextTab.id !== tabId) {
        setActiveTabId(nextTab.id);
      }
    }
  }, [tabs, activeTabId]);

  const handleTabClick = useCallback((tabId: string) => {
    if (!editorRef || !monacoRef) return;
    
    // Get the current model value before switching
    const currentModel = editorRef.getModel();
    if (currentModel) {
      const currentValue = currentModel.getValue();
      onChange(currentValue);
    }
    
    setActiveTabId(tabId);
    onActiveTabChange?.(tabId);
    
    // Focus editor after tab switch
    setTimeout(() => {
      editorRef?.focus();
    }, 0);
  }, [editorRef, monacoRef, onChange, onActiveTabChange]);

  const handleRenameTab = useCallback((tabId: string, e: React.MouseEvent) => {
    e.stopPropagation();
    const tab = tabs.find((t) => t.id === tabId);
    if (!tab) return;

    const newName = prompt('Rename query:', tab.name);
    if (newName && newName.trim()) {
      setTabs((prev) =>
        prev.map((t) => (t.id === tabId ? { ...t, name: newName.trim() } : t))
      );
    }
  }, [tabs]);

  return (
    <div className="tabbed-sql-editor">
      <div className="tab-bar">
        <div className="tab-list">
          {tabs.map((tab) => (
            <div
              key={tab.id}
              className={`tab ${tab.id === activeTabId ? 'tab--active' : ''}`}
              onClick={() => handleTabClick(tab.id)}
              onDoubleClick={(e) => handleRenameTab(tab.id, e)}
              title={`${tab.name} - Double-click to rename`}
            >
              <span className="tab-name">
                {tab.name}
                {tab.isDirty && <span className="tab-dirty">●</span>}
              </span>
              {tabs.length > 1 && (
                <button
                  className="tab-close"
                  onClick={(e) => handleCloseTab(tab.id, e)}
                  title="Close"
                >
                  ×
                </button>
              )}
            </div>
          ))}
        </div>
        <button className="tab-new" onClick={handleNewTab} title="New query (Ctrl+N)">
          +
        </button>
      </div>
      <div className="editor-toolbar">
        <button className="btn-execute-toolbar" onClick={onExecute} title="Execute Query (F7)">
          ▶ Execute
        </button>
        <button className="btn-execute-toolbar" onClick={() => {
          if (!editorRef) return;
          const selection = editorRef.getSelection();
          if (!selection) return;
          const model = editorRef.getModel();
          if (!model) return;
          const selectedText = model.getValueInRange(selection);
          if (selectedText.trim()) {
            onExecuteSelection(selectedText);
          } else {
            onExecute();
          }
        }} title="Run Selection (F8)">
          ▶ Run Selection
        </button>
      </div>
      <div className="editor-container">
        <Editor
          height="100%"
          defaultLanguage="sql"
          theme="vs-dark"
          path={activeTab.path}
          defaultValue={activeTab.defaultValue}
          onChange={handleEditorChange}
          onMount={handleMount}
          options={{
            minimap: { enabled: false },
            lineNumbers: 'on',
            wordWrap: 'on',
            fontSize: 14,
            scrollBeyondLastLine: false,
            automaticLayout: true,
            padding: { top: 8 },
            suggestOnTriggerCharacters: true,
            tabSize: 4,
            quickSuggestions: false,
            wordBasedSuggestions: 'off',
          }}
        />
      </div>
    </div>
  );
});

export default TabbedSqlEditor;
