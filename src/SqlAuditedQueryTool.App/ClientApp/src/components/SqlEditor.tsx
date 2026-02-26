import { useRef, useCallback, useImperativeHandle, forwardRef } from 'react';
import Editor, { type OnMount } from '@monaco-editor/react';
import type * as Monaco from 'monaco-editor';
import { v4 as uuidv4 } from 'uuid';

export interface SqlEditorHandle {
  insertTextAtCursor: (text: string) => void;
  setError: (errorMessage: string | null) => void;
}

interface SqlEditorProps {
  value: string;
  onChange: (value: string) => void;
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

const SqlEditor = forwardRef<SqlEditorHandle, SqlEditorProps>(function SqlEditor({ value, onChange }, ref) {
  const editorRef = useRef<Monaco.editor.IStandaloneCodeEditor | null>(null);
  const monacoRef = useRef<typeof Monaco | null>(null);

  useImperativeHandle(ref, () => ({
    insertTextAtCursor(text: string) {
      const editor = editorRef.current;
      if (!editor) return;
      const selection = editor.getSelection();
      if (!selection) return;
      editor.executeEdits('schema-insert', [
        { range: selection, text, forceMoveMarkers: true },
      ]);
      editor.focus();
    },
    setError(errorMessage: string | null) {
      const editor = editorRef.current;
      const monaco = monacoRef.current;
      if (!editor || !monaco) return;
      
      const model = editor.getModel();
      if (!model) return;

      if (!errorMessage) {
        monaco.editor.setModelMarkers(model, 'sql-errors', []);
        return;
      }

      // Try to parse line/column from error message
      // SQL Server format: "Error Number:156,State:1,Class:15" or "Incorrect syntax near..."
      const lineMatch = errorMessage.match(/line (\d+)/i);
      const line = lineMatch ? parseInt(lineMatch[1], 10) : 1;
      
      monaco.editor.setModelMarkers(model, 'sql-errors', [
        {
          startLineNumber: line,
          startColumn: 1,
          endLineNumber: line,
          endColumn: model.getLineMaxColumn(line),
          message: errorMessage,
          severity: monaco.MarkerSeverity.Error,
        },
      ]);
    },
  }));

  const handleMount: OnMount = useCallback((editor, monaco) => {
    editorRef.current = editor;
    monacoRef.current = monaco;

    const CONTEXT_GROUP = '9_sql_helpers';

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
  }, []);

  return (
    <Editor
      height="100%"
      defaultLanguage="sql"
      theme="vs-dark"
      value={value}
      onChange={(v) => onChange(v ?? '')}
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
      }}
    />
  );
});

export default SqlEditor;
