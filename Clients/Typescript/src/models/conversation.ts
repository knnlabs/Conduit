// Conversation management models

export interface Conversation {
  id: string;
  title: string;
  model: string;
  messages: ChatMessage[];
  metadata?: {
    temperature?: number;
    topP?: number;
    maxTokens?: number;
    systemPrompt?: string;
    tags?: string[];
    [key: string]: any;
  };
  createdAt: string;
  updatedAt: string;
  tokenUsage?: TokenUsage;
}

export interface ChatMessage {
  id: string;
  role: 'system' | 'user' | 'assistant';
  content: string | Array<{ type: 'text' | 'image_url'; text?: string; image_url?: { url: string } }>;
  metadata?: {
    model?: string;
    finishReason?: string;
    tokenCount?: number;
    timestamp?: string;
    tps?: number;
  };
  parentId?: string; // For conversation branching
}

export interface TokenUsage {
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
}

export interface ConversationListParams {
  limit?: number;
  offset?: number;
  orderBy?: 'createdAt' | 'updatedAt';
  order?: 'asc' | 'desc';
  search?: string;
  tags?: string[];
}

export interface ConversationExportFormat {
  format: 'json' | 'markdown' | 'pdf' | 'txt';
  includeMetadata?: boolean;
  includeSystemMessages?: boolean;
}

export interface MessageEditParams {
  content: string;
  regenerateFrom?: boolean;
}

export interface StreamControl {
  pause(): void;
  resume(): void;
  cancel(): void;
  onProgress?: (tokens: number, tps: number) => void;
}