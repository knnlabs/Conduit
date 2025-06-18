import type { ChatCompletionChunk } from './chat';

export interface SSEMessage {
  data: string;
  event?: string;
  id?: string;
  retry?: number;
}

export type StreamEvent = ChatCompletionChunk | '[DONE]';

export interface StreamOptions {
  signal?: AbortSignal;
  onError?: (error: Error) => void;
}