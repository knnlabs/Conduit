import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { v4 as uuidv4 } from 'uuid';
import { ChatSession, ChatMessage, ChatParameters } from '../types';

interface ChatStore {
  sessions: ChatSession[];
  activeSessionId: string | null;
  
  createSession: (model: string, parameters?: Partial<ChatParameters>) => string;
  deleteSession: (sessionId: string) => void;
  setActiveSession: (sessionId: string) => void;
  
  addMessage: (sessionId: string, message: Omit<ChatMessage, 'id' | 'timestamp'>) => void;
  updateMessage: (sessionId: string, messageId: string, update: Partial<ChatMessage>) => void;
  deleteMessage: (sessionId: string, messageId: string) => void;
  
  updateSessionParameters: (sessionId: string, parameters: Partial<ChatParameters>) => void;
  updateSessionModel: (sessionId: string, model: string) => void;
  updateSessionTitle: (sessionId: string, title: string) => void;
  
  clearAllSessions: () => void;
  getActiveSession: () => ChatSession | null;
}

const DEFAULT_PARAMETERS: ChatParameters = {
  temperature: 0.7,
  maxTokens: 2048,
  topP: 1,
  frequencyPenalty: 0,
  presencePenalty: 0,
  responseFormat: 'text',
  stream: true,
};

const useChatStoreBase = create<ChatStore>()(
  persist(
    (set, get) => ({
      sessions: [],
      activeSessionId: null,

      createSession: (model, parameters = {}) => {
        const sessionId = uuidv4();
        const newSession: ChatSession = {
          id: sessionId,
          title: 'New Chat',
          messages: [],
          model,
          createdAt: new Date(),
          updatedAt: new Date(),
          parameters: { ...DEFAULT_PARAMETERS, ...parameters },
        };
        
        set((state) => ({
          sessions: [...state.sessions, newSession],
          activeSessionId: sessionId,
        }));
        
        return sessionId;
      },

      deleteSession: (sessionId) => {
        set((state) => ({
          sessions: state.sessions.filter((s) => s.id !== sessionId),
          activeSessionId:
            state.activeSessionId === sessionId
              ? state.sessions.find((s) => s.id !== sessionId)?.id || null
              : state.activeSessionId,
        }));
      },

      setActiveSession: (sessionId) => {
        set({ activeSessionId: sessionId });
      },

      addMessage: (sessionId, message) => {
        const newMessage: ChatMessage = {
          ...message,
          id: uuidv4(),
          timestamp: new Date(),
        };

        set((state) => ({
          sessions: state.sessions.map((session) =>
            session.id === sessionId
              ? {
                  ...session,
                  messages: [...session.messages, newMessage],
                  updatedAt: new Date(),
                  title:
                    session.messages.length === 0 && message.role === 'user'
                      ? message.content.slice(0, 50) + (message.content.length > 50 ? '...' : '')
                      : session.title,
                }
              : session
          ),
        }));
      },

      updateMessage: (sessionId, messageId, update) => {
        set((state) => ({
          sessions: state.sessions.map((session) =>
            session.id === sessionId
              ? {
                  ...session,
                  messages: session.messages.map((msg) =>
                    msg.id === messageId ? { ...msg, ...update } : msg
                  ),
                  updatedAt: new Date(),
                }
              : session
          ),
        }));
      },

      deleteMessage: (sessionId, messageId) => {
        set((state) => ({
          sessions: state.sessions.map((session) =>
            session.id === sessionId
              ? {
                  ...session,
                  messages: session.messages.filter((msg) => msg.id !== messageId),
                  updatedAt: new Date(),
                }
              : session
          ),
        }));
      },

      updateSessionParameters: (sessionId, parameters) => {
        set((state) => ({
          sessions: state.sessions.map((session) =>
            session.id === sessionId
              ? {
                  ...session,
                  parameters: { ...session.parameters, ...parameters },
                  updatedAt: new Date(),
                }
              : session
          ),
        }));
      },

      updateSessionModel: (sessionId, model) => {
        set((state) => ({
          sessions: state.sessions.map((session) =>
            session.id === sessionId
              ? {
                  ...session,
                  model,
                  updatedAt: new Date(),
                }
              : session
          ),
        }));
      },

      updateSessionTitle: (sessionId, title) => {
        set((state) => ({
          sessions: state.sessions.map((session) =>
            session.id === sessionId
              ? {
                  ...session,
                  title,
                  updatedAt: new Date(),
                }
              : session
          ),
        }));
      },

      clearAllSessions: () => {
        set({ sessions: [], activeSessionId: null });
      },

      getActiveSession: () => {
        const state = get();
        return state.sessions.find((s) => s.id === state.activeSessionId) || null;
      },
    }),
    {
      name: 'conduit-chat-storage',
      version: 1,
    }
  )
);

// Export with SSR protection
export const useChatStore = ((state) => useChatStoreBase(state)) as typeof useChatStoreBase;