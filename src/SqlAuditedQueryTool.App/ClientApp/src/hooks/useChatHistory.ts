import { useState, useEffect } from 'react';
import type { ChatMessage } from '../api/queryApi';

export interface ChatSession {
  id: string;
  timestamp: string;
  messages: ChatMessage[];
  title: string;
}

const STORAGE_KEY = 'chat-sessions';

function loadSessions(): ChatSession[] {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    return stored ? JSON.parse(stored) : [];
  } catch {
    return [];
  }
}

function saveSessions(sessions: ChatSession[]): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(sessions));
  } catch (err) {
    console.error('Failed to save chat sessions:', err);
  }
}

function generateSessionTitle(messages: ChatMessage[]): string {
  const firstUser = messages.find((m) => m.role === 'user');
  if (!firstUser) return 'New Chat';
  
  const preview = firstUser.content.trim().slice(0, 40);
  return preview.length < firstUser.content.trim().length ? preview + '…' : preview;
}

export function useChatHistory() {
  const [sessions, setSessions] = useState<ChatSession[]>(loadSessions);
  const [currentSessionId, setCurrentSessionId] = useState<string | null>(null);

  // Auto-save sessions to localStorage whenever they change
  useEffect(() => {
    saveSessions(sessions);
  }, [sessions]);

  const createNewSession = (): string => {
    const newSession: ChatSession = {
      id: `session-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`,
      timestamp: new Date().toISOString(),
      messages: [],
      title: 'New Chat',
    };
    setSessions((prev) => [newSession, ...prev]);
    setCurrentSessionId(newSession.id);
    return newSession.id;
  };

  const loadSession = (sessionId: string): ChatMessage[] | null => {
    const session = sessions.find((s) => s.id === sessionId);
    if (!session) return null;
    setCurrentSessionId(sessionId);
    return session.messages;
  };

  const updateSession = (sessionId: string, messages: ChatMessage[]): void => {
    setSessions((prev) =>
      prev.map((s) =>
        s.id === sessionId
          ? { ...s, messages, title: generateSessionTitle(messages) }
          : s,
      ),
    );
  };

  const deleteSession = (sessionId: string): void => {
    setSessions((prev) => prev.filter((s) => s.id !== sessionId));
    if (currentSessionId === sessionId) {
      setCurrentSessionId(null);
    }
  };

  const getCurrentSession = (): ChatSession | null => {
    if (!currentSessionId) return null;
    return sessions.find((s) => s.id === currentSessionId) || null;
  };

  return {
    sessions,
    currentSessionId,
    createNewSession,
    loadSession,
    updateSession,
    deleteSession,
    getCurrentSession,
  };
}
