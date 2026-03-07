import { useState, useRef, useEffect } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import type { ChatMessage, QuerySuggestion, QueryResult } from '../api/queryApi';
import { chatStream } from '../api/queryApi';
import type { ChatSession } from '../hooks/useChatHistory';
import { useHorizontalResize } from '../hooks/useHorizontalResize';
import { useVerticalResize } from '../hooks/useVerticalResize';
import './ChatPanel.css';

// Detect SQL code blocks in markdown-style messages
function extractSqlBlocks(text: string): string[] {
  const sqlBlockRegex = /```sql\n([\s\S]*?)```/gi;
  const matches: string[] = [];
  let match;
  while ((match = sqlBlockRegex.exec(text)) !== null) {
    matches.push(match[1].trim());
  }
  return matches;
}

// Strip SQL code blocks from text when a suggestion card will show the same SQL
function stripSqlCodeBlocks(text: string): string {
  return text.replace(/```sql\n[\s\S]*?```/gi, '').trim();
}

function formatTimestamp(iso: string): string {
  const date = new Date(iso);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  if (diffDays < 7) return `${diffDays}d ago`;
  return date.toLocaleDateString();
}

interface ChatPanelProps {
  appMode: 'query' | 'simulator';
  onInsertSql: (sql: string) => void;
  onInsertAndExecute: (sql: string) => void;
  onSendToSimulator: (sql: string) => void;
  onAiExecutedQuery?: (sql: string, result: QueryResult) => void;
  sessions: ChatSession[];
  currentSessionId: string | null;
  onNewSession: () => string;
  onLoadSession: (sessionId: string) => void;
  onDeleteSession: (sessionId: string) => void;
  onUpdateSession: (sessionId: string, messages: ChatMessage[]) => void;
  gitHubIssueNumber?: number;
  azDoWorkItemId?: number;
}

export default function ChatPanel({
  appMode,
  onInsertSql,
  onInsertAndExecute,
  onSendToSimulator,
  onAiExecutedQuery,
  sessions,
  currentSessionId,
  onNewSession,
  onLoadSession,
  onDeleteSession,
  onUpdateSession,
  gitHubIssueNumber,
  azDoWorkItemId,
}: ChatPanelProps) {
  const [chatsExpanded, setChatsExpanded] = useState(false);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [streamStatus, setStreamStatus] = useState<string>('');
  const [streamingText, setStreamingText] = useState('');
  const [thinkingContent, setThinkingContent] = useState('');
  const [isThinking, setIsThinking] = useState(false);
  const [thinkingCollapsed, setThinkingCollapsed] = useState(false);
  const [inputHistory, setInputHistory] = useState<string[]>([]);
  const [historyIndex, setHistoryIndex] = useState(-1);
  const [collapsed, setCollapsed] = useState(false);
  const listRef = useRef<HTMLDivElement>(null);
  const textAreaRef = useRef<HTMLTextAreaElement>(null);
  const abortControllerRef = useRef<AbortController | null>(null);

  const { width, handleMouseDown: handlePanelResize } = useHorizontalResize({
    initialWidth: 360,
    minWidth: 280,
    maxWidth: 800,
    storageKey: 'chatPanelWidth',
    direction: 'left',
  });

  const { height: textAreaHeight, handleMouseDown: handleTextAreaResize } = useVerticalResize({
    initialHeight: 80,
    minHeight: 40,
    maxHeight: 300,
    storageKey: 'chatTextAreaHeight',
    direction: 'up',
  });

  // Get current session messages
  const currentSession = sessions.find((s) => s.id === currentSessionId);
  const messages = currentSession?.messages || [];

  useEffect(() => {
    if (listRef.current) {
      listRef.current.scrollTop = listRef.current.scrollHeight;
    }
  }, [messages, streamingText, thinkingContent]);

  const handleSend = async () => {
    const text = input.trim();
    if (!text || loading) return;

    const userMsg: ChatMessage = {
      role: 'user',
      content: text,
      timestamp: new Date().toISOString(),
    };

    // Add to input history
    setInputHistory((prev) => [...prev, text]);
    setHistoryIndex(-1);

    // Ensure we have a session - onNewSession returns the new session ID
    const sessionId = currentSessionId || onNewSession();

    const updated = [...messages, userMsg];
    onUpdateSession(sessionId, updated);
    setInput('');
    setLoading(true);
    setStreamStatus('');

    const controller = new AbortController();
    abortControllerRef.current = controller;

    let assistantContent = '';
    let thinkingAccumulator = '';
    let finalSuggestion: QuerySuggestion | undefined;
    let finalExecutedQuery: string | undefined;
    let finalExecutedResult: QueryResult | undefined;

    setStreamingText('');
    setThinkingContent('');
    setIsThinking(false);
    setThinkingCollapsed(false);

    try {
      await chatStream(
        text,
        updated,
        (event) => {
          if (event.type === 'tool_start') {
            setStreamStatus(`🔧 ${event.tool === 'execute_sql_query' ? 'Running query...' : 'Executing tool...'}`);
          } else if (event.type === 'tool_result') {
            setStreamStatus(event.success ? '✅ Query complete' : '❌ Tool failed');
            setTimeout(() => setStreamStatus(''), 1000);
          } else if (event.type === 'schema_retry') {
            setStreamStatus(`🔄 Fixing schema issues (attempt ${event.attempt}/${event.maxAttempts})...`);
          } else if (event.type === 'thinking') {
            thinkingAccumulator += event.content || '';
            setThinkingContent(thinkingAccumulator);
            setIsThinking(true);
          } else if (event.type === 'text') {
            // First text event collapses thinking
            if (thinkingAccumulator && !thinkingCollapsed) {
              setIsThinking(false);
              setThinkingCollapsed(true);
            }
            assistantContent += event.content || '';
            setStreamingText(assistantContent);
          } else if (event.type === 'done') {
            if (event.message) assistantContent = event.message;
            finalSuggestion = event.suggestion;
            finalExecutedQuery = event.executedQuery;
            finalExecutedResult = event.executedResult;
          }
        },
        gitHubIssueNumber,
        azDoWorkItemId,
        controller.signal,
      );

      // Create final assistant message
      const assistantMsg: ChatMessage = {
        role: 'assistant',
        content: assistantContent || 'No response',
        timestamp: new Date().toISOString(),
        suggestion: finalSuggestion,
      };
      onUpdateSession(sessionId, [...updated, assistantMsg]);
      
      // If AI executed a query, notify parent
      if (finalExecutedQuery && finalExecutedResult && onAiExecutedQuery) {
        onAiExecutedQuery(finalExecutedQuery, finalExecutedResult);
      }
    } catch (err) {
      const isCancelled = err instanceof Error && err.name === 'AbortError';
      const errorMsg: ChatMessage = {
        role: 'assistant',
        content: `Error: ${isCancelled ? 'Request cancelled.' : err instanceof Error ? err.message : 'Unknown error'}`,
        timestamp: new Date().toISOString(),
      };
      onUpdateSession(sessionId, [...updated, errorMsg]);
    } finally {
      abortControllerRef.current = null;
      setLoading(false);
      setStreamStatus('');
      setStreamingText('');
      setThinkingContent('');
      setIsThinking(false);
      setThinkingCollapsed(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    } else if (e.key === 'ArrowUp' && !e.shiftKey) {
      e.preventDefault();
      if (inputHistory.length === 0) return;
      
      const newIndex = historyIndex === -1 
        ? inputHistory.length - 1 
        : Math.max(0, historyIndex - 1);
      
      setHistoryIndex(newIndex);
      setInput(inputHistory[newIndex]);
      
      // Move cursor to end after setting value
      setTimeout(() => {
        if (textAreaRef.current) {
          textAreaRef.current.selectionStart = textAreaRef.current.value.length;
          textAreaRef.current.selectionEnd = textAreaRef.current.value.length;
        }
      }, 0);
    } else if (e.key === 'ArrowDown' && !e.shiftKey) {
      e.preventDefault();
      if (historyIndex === -1) return;
      
      const newIndex = historyIndex + 1;
      if (newIndex >= inputHistory.length) {
        setHistoryIndex(-1);
        setInput('');
      } else {
        setHistoryIndex(newIndex);
        setInput(inputHistory[newIndex]);
      }
      
      // Move cursor to end after setting value
      setTimeout(() => {
        if (textAreaRef.current) {
          textAreaRef.current.selectionStart = textAreaRef.current.value.length;
          textAreaRef.current.selectionEnd = textAreaRef.current.value.length;
        }
      }, 0);
    }
  };

  // Auto-expand textarea based on content
  useEffect(() => {
    if (textAreaRef.current) {
      // Reset height to measure scrollHeight accurately
      textAreaRef.current.style.height = 'auto';
      const newHeight = Math.min(Math.max(textAreaRef.current.scrollHeight, 80), 300);
      textAreaRef.current.style.height = `${newHeight}px`;
    }
  }, [input]);

  return (
    <div className={`chat${collapsed ? ' chat--collapsed' : ''}`} style={{ width: collapsed ? undefined : `${width}px` }}>
      <div className="chat-resize-handle" onMouseDown={handlePanelResize} />
      {collapsed ? (
        <>
          <button className="chat-btn-collapse chat-btn-collapse--collapsed" onClick={() => setCollapsed(false)} title="Expand">
            ◀
          </button>
          <div className="chat-header-collapsed">💬 Chat Assistant</div>
        </>
      ) : (
        <>
          <div className="chat-header">
            <button className="chat-btn-collapse" onClick={() => setCollapsed(true)} title="Collapse">
              ▶
            </button>
            <span className="chat-header-title">💬 Chat Assistant</span>
          </div>

      {/* Chats section */}
      <div className="chat-sessions">
        <div className="chat-sessions-header" onClick={() => setChatsExpanded((v) => !v)}>
          <span className="chat-sessions-title">
            {chatsExpanded ? '▼' : '▶'} Chats ({sessions.length})
          </span>
          <button
            className="chat-sessions-new"
            onClick={(e) => {
              e.stopPropagation();
              onNewSession();
            }}
            title="Start a new chat"
          >
            ✚ New
          </button>
        </div>
        {chatsExpanded && sessions.length > 0 && (
          <div className="chat-sessions-list">
            {sessions.map((session) => (
              <div
                key={session.id}
                className={`chat-session-item ${session.id === currentSessionId ? 'chat-session-item--active' : ''}`}
              >
                <div
                  className="chat-session-content"
                  onClick={() => onLoadSession(session.id)}
                >
                  <div className="chat-session-title">{session.title}</div>
                  <div className="chat-session-meta">
                    {formatTimestamp(session.timestamp)} • {session.messages.length} msg
                  </div>
                </div>
                <button
                  className="chat-session-delete"
                  onClick={(e) => {
                    e.stopPropagation();
                    if (confirm(`Delete chat "${session.title}"?`)) {
                      onDeleteSession(session.id);
                    }
                  }}
                  title="Delete this chat"
                >
                  🗑️
                </button>
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="chat-messages" ref={listRef}>
        {messages.length === 0 && (
          <div className="chat-empty">
            Ask me about your database or for help writing SQL queries.
          </div>
        )}
        {messages.map((msg, i) => {
          const hasSuggestion = !!msg.suggestion;
          const displayContent = msg.role === 'assistant' && hasSuggestion
            ? stripSqlCodeBlocks(msg.content)
            : msg.content;
          const sqlBlocks = msg.role === 'assistant' && !hasSuggestion ? extractSqlBlocks(msg.content) : [];
          return (
            <div key={i} className={`chat-bubble chat-bubble--${msg.role}`}>
              {displayContent && (
                <div className="chat-bubble-content">
                  {msg.role === 'assistant' ? (
                    <ReactMarkdown remarkPlugins={[remarkGfm]}>{displayContent}</ReactMarkdown>
                  ) : (
                    displayContent
                  )}
                </div>
              )}
              {msg.suggestion && (
                <SuggestionCard
                  suggestion={msg.suggestion}
                  onInsert={onInsertSql}
                  onInsertAndExecute={onInsertAndExecute}
                  onSendToSimulator={onSendToSimulator}
                  appMode={appMode}
                />
              )}
              {sqlBlocks.map((sqlBlock, idx) => (
                <div key={idx} className="chat-sql-block">
                  <pre className="chat-sql-code">{sqlBlock}</pre>
                  <button
                    className="chat-sql-insert"
                    onClick={() => onInsertSql(sqlBlock)}
                    title="Insert this query into the editor"
                  >
                    📝 Insert into Editor
                  </button>
                </div>
              ))}
              <div className="chat-bubble-time">
                {new Date(msg.timestamp).toLocaleTimeString()}
              </div>
            </div>
          );
        })}
        {loading && (
          <div className="chat-bubble chat-bubble--assistant">
            {/* Thinking section */}
            {thinkingContent && (
              <div className="chat-thinking-section">
                {isThinking ? (
                  <details open className="chat-thinking-details">
                    <summary className="chat-thinking-summary">
                      <span className="chat-thinking-indicator">💭 Thinking<span className="chat-thinking-dots" /></span>
                    </summary>
                    <div className="chat-thinking-text">{thinkingContent}</div>
                  </details>
                ) : (
                  <details className="chat-thinking-details">
                    <summary className="chat-thinking-summary">
                      💭 Reasoning ({thinkingContent.length} chars — click to expand)
                    </summary>
                    <div className="chat-thinking-text">{thinkingContent}</div>
                  </details>
                )}
              </div>
            )}
            {/* Streaming response text */}
            {streamingText ? (
              <div className="chat-bubble-content">
                <ReactMarkdown remarkPlugins={[remarkGfm]}>{streamingText}</ReactMarkdown>
              </div>
            ) : !thinkingContent ? (
              <div className="chat-typing">
                <span /><span /><span />
              </div>
            ) : null}
            <div className="chat-typing-actions">
              {streamStatus && (
                <div className="chat-typing-status">{streamStatus}</div>
              )}
              <button
                className="chat-typing-cancel"
                onClick={() => abortControllerRef.current?.abort()}
                title="Cancel request"
              >
                ✕ Cancel
              </button>
            </div>
          </div>
        )}
      </div>

      <div className="chat-input-area">
        <div className="chat-input-wrapper">
          <div className="chat-input-resize-handle chat-input-resize-handle--visible" onMouseDown={handleTextAreaResize} />
          <textarea
            ref={textAreaRef}
            className="chat-input"
            style={{ height: `${textAreaHeight}px` }}
            placeholder="Ask about your database…"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            disabled={loading}
          />
        </div>
        <button
          className="chat-send"
          onClick={handleSend}
          disabled={!input.trim() || loading}
        >
          Send
        </button>
      </div>
        </>
      )}
    </div>
  );
}

function SuggestionCard({
  suggestion,
  onInsert,
  onInsertAndExecute,
  onSendToSimulator,
  appMode,
}: {
  suggestion: QuerySuggestion;
  onInsert: (sql: string) => void;
  onInsertAndExecute: (sql: string) => void;
  onSendToSimulator: (sql: string) => void;
  appMode: 'query' | 'simulator';
}) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(suggestion.sql);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Fallback: select-copy not needed in modern browsers
    }
  };

  const warningsBlock = suggestion.schemaWarnings && suggestion.schemaWarnings.length > 0 ? (
    <div className="suggestion-schema-warnings">
      <div className="suggestion-schema-warnings-title">⚠️ Schema Validation Warnings</div>
      <ul>
        {suggestion.schemaWarnings.map((w, i) => (
          <li key={i}>{w}</li>
        ))}
      </ul>
    </div>
  ) : null;

  if (suggestion.isFixQuery) {
    return (
      <div className="suggestion suggestion--fix">
        <div className="suggestion-banner">
          ⚠️ FIX QUERY — Must be run in a separate tool with write access
        </div>
        {warningsBlock}
        <pre className="suggestion-sql">{suggestion.sql}</pre>
        <div className="suggestion-explain">{suggestion.explanation}</div>
        <div className="suggestion-actions">
          <button
            className="suggestion-btn suggestion-btn--copy"
            onClick={handleCopy}
          >
            {copied ? '✅ Copied!' : '📋 Copy'}
          </button>
          <button
            className="suggestion-btn suggestion-btn--simulator"
            onClick={() => onSendToSimulator(suggestion.sql)}
          >
            🔬 Send to Simulator
          </button>
          <button
            className="suggestion-btn suggestion-btn--insert"
            onClick={() => onInsert(suggestion.sql)}
          >
            Insert into Editor
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="suggestion suggestion--read">
      {warningsBlock}
      <pre className="suggestion-sql">{suggestion.sql}</pre>
      <div className="suggestion-explain">{suggestion.explanation}</div>
      <div className="suggestion-actions">
        <button
          className="suggestion-btn suggestion-btn--copy"
          onClick={handleCopy}
        >
          {copied ? '✅ Copied!' : '📋 Copy'}
        </button>
        {appMode === 'query' && suggestion.isReadOnly !== false && (
          <button
            className="suggestion-btn suggestion-btn--execute"
            onClick={() => onInsertAndExecute(suggestion.sql)}
          >
            ▶ Insert &amp; Execute
          </button>
        )}
        {suggestion.isReadOnly === false && (
          <button
            className="suggestion-btn suggestion-btn--simulator"
            onClick={() => onSendToSimulator(suggestion.sql)}
          >
            🔬 Run in Simulator
          </button>
        )}
        <button
          className="suggestion-btn suggestion-btn--insert"
          onClick={() => onInsert(suggestion.sql)}
        >
          Insert into Editor
        </button>
      </div>
    </div>
  );
}
