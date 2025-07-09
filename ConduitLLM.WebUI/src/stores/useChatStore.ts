import { create } from 'zustand';
import type { ChatCompletionRequest } from '@knn_labs/conduit-core-client';

type ChatMessage = ChatCompletionRequest['messages'][0];

export interface ChatConversation {
  id: string;
  title: string;
  messages: ChatMessage[];
  model: string;
  systemPrompt?: string;
  createdAt: Date;
  updatedAt: Date;
  parameters: {
    temperature: number;
    top_p: number;
    max_tokens: number;
  };
}

export interface ChatState {
  conversations: ChatConversation[];
  activeConversationId: string | null;
  isStreaming: boolean;
  streamingMessage: string;
  selectedVirtualKey: string;
  selectedModel: string;
  parameters: {
    temperature: number;
    top_p: number;
    max_tokens: number;
  };
  systemPrompt: string;
  
  // Actions
  createConversation: (model: string, title?: string) => string;
  deleteConversation: (id: string) => void;
  setActiveConversation: (id: string) => void;
  addMessage: (conversationId: string, message: ChatMessage) => void;
  updateMessage: (conversationId: string, messageIndex: number, content: string) => void;
  clearConversation: (id: string) => void;
  setSelectedVirtualKey: (key: string) => void;
  setSelectedModel: (model: string) => void;
  updateParameters: (parameters: Partial<ChatState['parameters']>) => void;
  setSystemPrompt: (prompt: string) => void;
  setStreaming: (isStreaming: boolean) => void;
  updateStreamingMessage: (content: string) => void;
  exportConversation: (id: string) => string;
  importConversation: (data: string) => void;
}

const DEFAULT_PARAMETERS = {
  temperature: 0.7,
  top_p: 1.0,
  max_tokens: 1000,
};

const DEFAULT_SYSTEM_PROMPT = "You are a helpful AI assistant. Provide clear, accurate, and helpful responses.";

export const useChatStore = create<ChatState>((set, get) => ({
  conversations: [],
  activeConversationId: null,
  isStreaming: false,
  streamingMessage: '',
  selectedVirtualKey: '',
  selectedModel: '',
  parameters: DEFAULT_PARAMETERS,
  systemPrompt: DEFAULT_SYSTEM_PROMPT,

  createConversation: (model: string, title?: string) => {
    const id = `conv_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
    const conversation: ChatConversation = {
      id,
      title: title || `New Chat - ${new Date().toLocaleString()}`,
      messages: [],
      model,
      systemPrompt: get().systemPrompt,
      createdAt: new Date(),
      updatedAt: new Date(),
      parameters: { ...get().parameters },
    };

    set((state) => ({
      conversations: [...state.conversations, conversation],
      activeConversationId: id,
    }));

    return id;
  },

  deleteConversation: (id: string) => {
    set((state) => ({
      conversations: state.conversations.filter(conv => conv.id !== id),
      activeConversationId: state.activeConversationId === id ? 
        (state.conversations.length > 1 ? state.conversations[0].id : null) : 
        state.activeConversationId,
    }));
  },

  setActiveConversation: (id: string) => {
    const conversation = get().conversations.find(conv => conv.id === id);
    if (conversation) {
      set((state) => ({
        activeConversationId: id,
        selectedModel: conversation.model,
        systemPrompt: conversation.systemPrompt || state.systemPrompt,
        parameters: conversation.parameters,
      }));
    }
  },

  addMessage: (conversationId: string, message: ChatMessage) => {
    set((state) => ({
      conversations: state.conversations.map(conv => 
        conv.id === conversationId 
          ? { 
              ...conv, 
              messages: [...conv.messages, message],
              updatedAt: new Date(),
              title: conv.messages.length === 0 && message.role === 'user' && message.content
                ? message.content.slice(0, 50) + (message.content.length > 50 ? '...' : '')
                : conv.title
            }
          : conv
      ),
    }));
  },

  updateMessage: (conversationId: string, messageIndex: number, content: string) => {
    set((state) => ({
      conversations: state.conversations.map(conv => 
        conv.id === conversationId 
          ? { 
              ...conv, 
              messages: conv.messages.map((msg, index) => 
                index === messageIndex ? { ...msg, content } : msg
              ),
              updatedAt: new Date(),
            }
          : conv
      ),
    }));
  },

  clearConversation: (id: string) => {
    set((state) => ({
      conversations: state.conversations.map(conv => 
        conv.id === id 
          ? { ...conv, messages: [], updatedAt: new Date() }
          : conv
      ),
    }));
  },

  setSelectedVirtualKey: (key: string) => {
    set({ selectedVirtualKey: key });
  },

  setSelectedModel: (model: string) => {
    set({ selectedModel: model });
    
    // Update active conversation model if there is one
    const { activeConversationId } = get();
    if (activeConversationId) {
      set((state) => ({
        conversations: state.conversations.map(conv => 
          conv.id === activeConversationId 
            ? { ...conv, model, updatedAt: new Date() }
            : conv
        ),
      }));
    }
  },

  updateParameters: (newParameters: Partial<ChatState['parameters']>) => {
    const updatedParameters = { ...get().parameters, ...newParameters };
    set({ parameters: updatedParameters });
    
    // Update active conversation parameters if there is one
    const { activeConversationId } = get();
    if (activeConversationId) {
      set((state) => ({
        conversations: state.conversations.map(conv => 
          conv.id === activeConversationId 
            ? { ...conv, parameters: updatedParameters, updatedAt: new Date() }
            : conv
        ),
      }));
    }
  },

  setSystemPrompt: (prompt: string) => {
    set({ systemPrompt: prompt });
    
    // Update active conversation system prompt if there is one
    const { activeConversationId } = get();
    if (activeConversationId) {
      set((state) => ({
        conversations: state.conversations.map(conv => 
          conv.id === activeConversationId 
            ? { ...conv, systemPrompt: prompt, updatedAt: new Date() }
            : conv
        ),
      }));
    }
  },

  setStreaming: (isStreaming: boolean) => {
    set({ isStreaming });
    if (!isStreaming) {
      // When streaming stops, finalize the message
      const { activeConversationId, streamingMessage } = get();
      if (activeConversationId && streamingMessage) {
        // Update the last assistant message with the final content
        set((state) => ({
          conversations: state.conversations.map(conv => {
            if (conv.id === activeConversationId) {
              const updatedMessages = [...conv.messages];
              const lastMessageIndex = updatedMessages.length - 1;
              if (lastMessageIndex >= 0 && updatedMessages[lastMessageIndex].role === 'assistant') {
                updatedMessages[lastMessageIndex] = {
                  ...updatedMessages[lastMessageIndex],
                  content: streamingMessage,
                };
              }
              return { ...conv, messages: updatedMessages, updatedAt: new Date() };
            }
            return conv;
          }),
          streamingMessage: '',
        }));
      } else {
        set({ streamingMessage: '' });
      }
    }
  },

  updateStreamingMessage: (content: string) => {
    set((state) => ({ streamingMessage: state.streamingMessage + content }));
  },

  exportConversation: (id: string) => {
    const conversation = get().conversations.find(conv => conv.id === id);
    if (!conversation) return '';
    
    return JSON.stringify({
      ...conversation,
      exported_at: new Date().toISOString(),
      version: '1.0',
    }, null, 2);
  },

  importConversation: (data: string) => {
    try {
      const parsed = JSON.parse(data);
      if (!parsed.messages || !Array.isArray(parsed.messages)) {
        throw new Error('Invalid conversation format');
      }

      const conversation: ChatConversation = {
        id: `conv_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
        title: parsed.title || 'Imported Conversation',
        messages: parsed.messages,
        model: parsed.model || get().selectedModel,
        systemPrompt: parsed.systemPrompt || get().systemPrompt,
        createdAt: new Date(),
        updatedAt: new Date(),
        parameters: parsed.parameters || DEFAULT_PARAMETERS,
      };

      set((state) => ({
        conversations: [...state.conversations, conversation],
        activeConversationId: conversation.id,
      }));
    } catch (error) {
      console.error('Failed to import conversation:', error);
      throw new Error('Invalid conversation data');
    }
  },
}));

// Persistence layer
if (typeof window !== 'undefined') {
  const STORAGE_KEY = 'conduit-chat-conversations';

  // Load conversations from localStorage on initialization
  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved) {
      const parsed = JSON.parse(saved);
      useChatStore.setState({
        conversations: parsed.conversations || [],
        activeConversationId: parsed.activeConversationId || null,
        selectedVirtualKey: parsed.selectedVirtualKey || '',
        selectedModel: parsed.selectedModel || '',
        parameters: parsed.parameters || DEFAULT_PARAMETERS,
        systemPrompt: parsed.systemPrompt || DEFAULT_SYSTEM_PROMPT,
      });
    }
  } catch (error) {
    console.warn('Failed to load chat conversations from storage:', error);
  }

  // Save conversations to localStorage on state changes
  useChatStore.subscribe((state) => {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify({
        conversations: state.conversations,
        activeConversationId: state.activeConversationId,
        selectedVirtualKey: state.selectedVirtualKey,
        selectedModel: state.selectedModel,
        parameters: state.parameters,
        systemPrompt: state.systemPrompt,
      }));
    } catch (error) {
      console.warn('Failed to save chat conversations to storage:', error);
    }
  });
}