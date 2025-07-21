// Conversation management models


export interface Conversation {
  id: string;
  title: string;
  model: string;
  messages: ChatMessage[];
  metadata?: ConversationMetadata;
  createdAt: string;
  updatedAt: string;
  tokenUsage?: TokenUsage;
}

export interface ConversationMetadata {
  temperature?: number;
  topP?: number;
  maxTokens?: number;
  systemPrompt?: string;
  tags?: string[];
  [key: string]: string | number | boolean | string[] | undefined;
}

export interface ChatMessage {
  id: string;
  role: 'system' | 'user' | 'assistant';
  content: string | Array<{ type: 'text' | 'image_url'; text?: string; image_url?: { url: string } }>;
  metadata?: MessageMetadata;
  parentId?: string; // For conversation branching
}

export interface MessageMetadata {
  model?: string;
  finishReason?: string;
  tokenCount?: number;
  timestamp?: string;
  tps?: number;
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

export interface ConversationExportParams {
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

export interface TokenEstimate {
  messages: number;
  total: number;
  breakdown: {
    messageId?: string;
    role: string;
    tokens: number;
  }[];
}

export interface ConversationStats {
  conversationId: string;
  totalMessages: number;
  totalTokens: number;
  tokenUsage: TokenUsage;
  averageTPS: number;
  duration: number;
  models: { [model: string]: number };
}

export interface UserUsageStats {
  totalTokens: number;
  totalCost: number;
  byModel: { [model: string]: TokenUsage & { cost: number } };
  byDay: { date: string; tokens: number; cost: number }[];
}

export interface ModelUsageStats {
  model: string;
  totalRequests: number;
  totalTokens: number;
  averageLatency: number;
  successRate: number;
}