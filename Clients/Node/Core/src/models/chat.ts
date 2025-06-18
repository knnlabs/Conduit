import type { Usage, ResponseFormat, Tool, ToolCall, FinishReason, PerformanceMetrics } from './common';

export interface ChatCompletionMessage {
  role: 'system' | 'user' | 'assistant' | 'tool';
  content: string | null;
  name?: string;
  tool_calls?: ToolCall[];
  tool_call_id?: string;
}

export interface ChatCompletionRequest {
  model: string;
  messages: ChatCompletionMessage[];
  frequency_penalty?: number;
  logit_bias?: Record<string, number>;
  logprobs?: boolean;
  top_logprobs?: number;
  max_tokens?: number;
  n?: number;
  presence_penalty?: number;
  response_format?: ResponseFormat;
  seed?: number;
  stop?: string | string[];
  stream?: boolean;
  temperature?: number;
  top_p?: number;
  tools?: Tool[];
  tool_choice?: 'none' | 'auto' | { type: 'function'; function: { name: string } };
  user?: string;
}

export interface ChatCompletionChoice {
  index: number;
  message: ChatCompletionMessage;
  logprobs?: unknown;
  finish_reason: FinishReason;
}

export interface ChatCompletionResponse {
  id: string;
  object: 'chat.completion';
  created: number;
  model: string;
  system_fingerprint?: string;
  choices: ChatCompletionChoice[];
  usage: Usage;
  performance?: PerformanceMetrics;
}

export interface ChatCompletionChunkChoice {
  index: number;
  delta: Partial<ChatCompletionMessage>;
  logprobs?: unknown;
  finish_reason: FinishReason;
}

export interface ChatCompletionChunk {
  id: string;
  object: 'chat.completion.chunk';
  created: number;
  model: string;
  system_fingerprint?: string;
  choices: ChatCompletionChunkChoice[];
  usage?: Usage;
  performance?: PerformanceMetrics;
}