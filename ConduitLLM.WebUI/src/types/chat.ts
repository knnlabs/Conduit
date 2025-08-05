// Chat-related type definitions

// Import MessageContent type from local chat types to avoid broken SDK imports
import type { MessageContent } from '@/app/chat/types';

export interface ChatMessage {
  id?: string;
  role: 'system' | 'user' | 'assistant';
  content: string | MessageContent;
  metadata?: { model?: string; finishReason?: string; tokenCount?: number; timestamp?: string; tps?: number; };
  parentId?: string; // For conversation branching
}

export interface Conversation {
  id: string;
  title: string;
  model: string;
  messages: ChatMessage[];
  metadata?: { temperature?: number; topP?: number; maxTokens?: number; systemPrompt?: string; tags?: string[]; [key: string]: string | number | boolean | string[] | undefined; };
  createdAt: string;
  updatedAt: string;
  tokenUsage?: { promptTokens: number; completionTokens: number; totalTokens: number; };
}