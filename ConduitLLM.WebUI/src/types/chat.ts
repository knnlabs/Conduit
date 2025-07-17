// Chat-related type definitions

export interface ChatMessage {
  id?: string;
  role: 'system' | 'user' | 'assistant';
  content: string | Array<TextContent | ImageContent>;
  metadata?: MessageMetadata;
  parentId?: string; // For conversation branching
}

export interface TextContent {
  type: 'text';
  text?: string;
}

export interface ImageContent {
  type: 'image_url';
  image_url?: {
    url: string;
    detail?: 'auto' | 'low' | 'high';
  };
}

export interface MessageMetadata {
  model?: string;
  finishReason?: string;
  tokenCount?: number;
  timestamp?: string;
  tps?: number;
}

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

export interface TokenUsage {
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
}

export interface StreamingMetadata {
  startTime: number;
  tokenCount: number;
  tps: number;
  isPaused?: boolean;
}