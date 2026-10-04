import { Injectable } from '@angular/core';

export interface StoredMessage {
  role: 'user' | 'bot';
  text: string;
  items?: any[];
  recommendations?: any[];
  warnings?: string[];
  deliveryNotePdf?: string;
  deliveryNoteFileName?: string;
  emojis?: string[];
  timestamp?: string;
}

export interface ChatSession {
  id: string;
  username: string;
  startedAt: string;
  messages: StoredMessage[];
}

const STORAGE_KEY = 'smartstore_sessions';

@Injectable({ providedIn: 'root' })
export class ChatHistoryService {

  private loadAll(): ChatSession[] {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? JSON.parse(raw) : [];
    } catch { return []; }
  }

  private saveAll(sessions: ChatSession[]): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(sessions));
  }

  /** Create a new session for this login, return its id */
  createSession(username: string): string {
    const sessions = this.loadAll();
    const id = `${username}_${Date.now()}`;
    const now = new Date().toLocaleString('he-IL', { dateStyle: 'short', timeStyle: 'short' });
    sessions.push({ id, username, startedAt: now, messages: [] });
    this.saveAll(sessions);
    return id;
  }

  saveMessages(sessionId: string, messages: StoredMessage[]): void {
    const sessions = this.loadAll();
    const s = sessions.find(s => s.id === sessionId);
    if (s) { s.messages = messages; this.saveAll(sessions); }
  }

  getMessages(sessionId: string): StoredMessage[] {
    return this.loadAll().find(s => s.id === sessionId)?.messages ?? [];
  }

  /** Returns sessions visible to this user - only sessions with user messages, excluding current */
  getSessions(username: string, currentSessionId?: string): ChatSession[] {
    const all = this.loadAll()
      .filter(s => s.messages.some(m => m.role === 'user') && s.id !== currentSessionId);
    if (username === 'admin') return [...all].reverse();
    return all.filter(s => s.username === username).reverse();
  }

  clearSession(sessionId: string): void {
    const sessions = this.loadAll().filter(s => s.id !== sessionId);
    this.saveAll(sessions);
  }
}
