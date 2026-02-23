import { useState, useRef, useEffect } from 'react';
import type { ChatMessage, QuerySuggestion, QueryResult } from '../api/queryApi';
import { chat as chatApi } from '../api/queryApi';
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
  open: boolean;
  onClose: () => void;
  onInsertSql: (sql: string) => void;
  onInsertAndExecute: (sql: string) => void;
  onAiExecutedQuery?: (sql: string, result: QueryResult) => void;
  sessions: ChatSession[];
  currentSessionId: string | null;
  onNewSession: () => string;
  onLoadSession: (sessionId: string) => void;
  onDeleteSession: (sessionId: string) => void;
  onUpdateSession: (sessionId: string, messages: ChatMessage[]) => void;
}

export default function ChatPanel({
  open,
  onClose,
  onInsertSql,
  onInsertAndExecute,
  onAiExecutedQuery,
  sessions,
  currentSessionId,
  onNewSession,
  onLoadSession,
  onDeleteSession,
  onUpdateSession,
}: ChatPanelProps) {
  const [chatsExpanded, setChatsExpanded] = useState(false);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const listRef = useRef<HTMLDivElement>(null);

  const { width, handleMouseDown: handlePanelResize } = useHorizontalResize({
    initialWidth: 360,
    minWidth: 280,
    maxWidth: 800,
    storageKey: 'chatPanelWidth',
    direction: 'left',
  });

  const { height: textAreaHeight, handleMouseDown: handleTextAreaResize } = useVerticalResize({
    initialHeight: 60,
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
  }, [messages]);

  const handleSend = async () => {
    const text = input.trim();
    if (!text || loading) return;

    const userMsg: ChatMessage = {
      role: 'user',
      content: text,
      timestamp: new Date().toISOString(),
    };

    // Ensure we have a session - onNewSession returns the new session ID
    const sessionId = currentSessionId || onNewSession();

    const updated = [...messages, userMsg];
    onUpdateSession(sessionId, updated);
    setInput('');
    setLoading(true);

    try {
      const resp = await chatApi(text, updated);
      const assistantMsg: ChatMessage = {
        role: 'assistant',
        content: resp.message,
        timestamp: new Date().toISOString(),
        suggestion: resp.suggestion,
      };
      onUpdateSession(sessionId, [...updated, assistantMsg]);
      
      // If AI executed a query, notify parent
      if (resp.executedQuery && resp.executedResult && onAiExecutedQuery) {
        onAiExecutedQuery(resp.executedQuery, resp.executedResult);
      }
    } catch (err) {
      const isTimeout = err instanceof Error && err.message.includes('timed out');
      const errorMsg: ChatMessage = {
        role: 'assistant',
        content: `${isTimeout ? '⏱️ ' : ''}Error: ${err instanceof Error ? err.message : 'Unknown error'}`,
        timestamp: new Date().toISOString(),
      };
      onUpdateSession(sessionId, [...updated, errorMsg]);
    } finally {
      setLoading(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  if (!open) return null;

  return (
    <div className="chat" style={{ width: `${width}px` }}>
      <div className="chat-resize-handle" onMouseDown={handlePanelResize} />
      <div className="chat-header">
        <span className="chat-header-title">💬 Chat Assistant</span>
        <button className="chat-header-close" onClick={onClose} title="Close chat">
          ✕
        </button>
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
          const sqlBlocks = msg.role === 'assistant' ? extractSqlBlocks(msg.content) : [];
          return (
            <div key={i} className={`chat-bubble chat-bubble--${msg.role}`}>
              <div className="chat-bubble-content">{msg.content}</div>
              {msg.suggestion && (
                <SuggestionCard
                  suggestion={msg.suggestion}
                  onInsert={onInsertSql}
                  onInsertAndExecute={onInsertAndExecute}
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
            <div className="chat-typing">
              <span /><span /><span />
            </div>
          </div>
        )}
      </div>

      <div className="chat-input-area">
        <div className="chat-input-wrapper">
          <div className="chat-input-resize-handle" onMouseDown={handleTextAreaResize} />
          <textarea
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
    </div>
  );
}

function SuggestionCard({
  suggestion,
  onInsert,
  onInsertAndExecute,
}: {
  suggestion: QuerySuggestion;
  onInsert: (sql: string) => void;
  onInsertAndExecute: (sql: string) => void;
}) {
  if (suggestion.isFixQuery) {
    return (
      <div className="suggestion suggestion--fix">
        <div className="suggestion-banner">
          ⚠️ FIX QUERY — Must be run in a separate tool with write access
        </div>
        <pre className="suggestion-sql">{suggestion.sql}</pre>
        <div className="suggestion-explain">{suggestion.explanation}</div>
        <button
          className="suggestion-btn suggestion-btn--insert"
          onClick={() => onInsert(suggestion.sql)}
        >
          Insert into Editor
        </button>
      </div>
    );
  }

  return (
    <div className="suggestion suggestion--read">
      <pre className="suggestion-sql">{suggestion.sql}</pre>
      <div className="suggestion-explain">{suggestion.explanation}</div>
      <div className="suggestion-actions">
        <button
          className="suggestion-btn suggestion-btn--execute"
          onClick={() => onInsertAndExecute(suggestion.sql)}
        >
          ▶ Insert &amp; Execute
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
