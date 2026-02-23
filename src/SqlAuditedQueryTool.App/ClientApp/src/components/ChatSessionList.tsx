import type { ChatSession } from '../hooks/useChatHistory';
import './ChatSessionList.css';

interface ChatSessionListProps {
  sessions: ChatSession[];
  currentSessionId: string | null;
  onSelectSession: (sessionId: string) => void;
  onNewSession: () => void;
  onDeleteSession: (sessionId: string) => void;
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

export default function ChatSessionList({
  sessions,
  currentSessionId,
  onSelectSession,
  onNewSession,
  onDeleteSession,
}: ChatSessionListProps) {
  return (
    <div className="csl">
      <div className="csl-header">
        <span className="csl-title">Chat History</span>
        <button
          className="csl-new"
          onClick={onNewSession}
          title="Start a new chat session"
        >
          ✚ New
        </button>
      </div>
      
      {sessions.length === 0 ? (
        <div className="csl-empty">No chat history yet. Start a new chat!</div>
      ) : (
        <ul className="csl-list">
          {sessions.map((session) => (
            <li
              key={session.id}
              className={`csl-item ${session.id === currentSessionId ? 'csl-item--active' : ''}`}
            >
              <div
                className="csl-item-content"
                onClick={() => onSelectSession(session.id)}
              >
                <div className="csl-item-title">{session.title}</div>
                <div className="csl-item-meta">
                  <span>{formatTimestamp(session.timestamp)}</span>
                  <span>•</span>
                  <span>{session.messages.length} msg</span>
                </div>
              </div>
              <button
                className="csl-item-delete"
                onClick={(e) => {
                  e.stopPropagation();
                  onDeleteSession(session.id);
                }}
                title="Delete this chat session"
              >
                🗑️
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
