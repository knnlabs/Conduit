import type { ChatCompletionChunk } from './chat';
import type { ImageGenerationChunk } from './images';
import type { AudioTranscriptionChunk, AudioTranslationChunk } from './audio';
import type { EmbeddingChunk } from './embeddings';

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
  onProgress?: (event: ProgressEvent) => void;
  onStart?: () => void;
  onEnd?: () => void;
  timeout?: number; // Timeout in milliseconds
}

export interface ProgressEvent {
  loaded: number;
  total?: number;
  percentage?: number;
}

/**
 * Base interface for all streaming chunks
 */
export interface BaseStreamChunk {
  id: string;
  object: string;
  created: number;
}

/**
 * Generic streaming response type
 */
export interface StreamingResponse<T extends BaseStreamChunk> {
  /**
   * Async iterator for consuming stream chunks
   */
  [Symbol.asyncIterator](): AsyncIterator<T>;
  
  /**
   * Collects all chunks and returns the complete response
   */
  toArray(): Promise<T[]>;
  
  /**
   * Transforms the stream with a custom function
   */
  map<U>(fn: (chunk: T) => U | Promise<U>): AsyncGenerator<U, void, unknown>;
  
  /**
   * Filters stream chunks based on a predicate
   */
  filter(predicate: (chunk: T) => boolean | Promise<boolean>): AsyncGenerator<T, void, unknown>;
  
  /**
   * Takes only the first n chunks from the stream
   */
  take(n: number): AsyncGenerator<T, void, unknown>;
  
  /**
   * Skips the first n chunks from the stream
   */
  skip(n: number): AsyncGenerator<T, void, unknown>;
  
  /**
   * Cancels the stream
   */
  cancel(): void;
}

/**
 * Type-safe streaming event types
 */
export enum StreamEventType {
  ChatCompletion = 'chat.completion.chunk',
  ImageGeneration = 'image.generation.chunk',
  AudioTranscription = 'audio.transcription.chunk',
  AudioTranslation = 'audio.translation.chunk',
  Embedding = 'embedding.chunk',
  Error = 'error',
  Done = 'done',
}

/**
 * Union type for all possible streaming chunks
 */
export type AnyStreamChunk = 
  | ChatCompletionChunk
  | ImageGenerationChunk
  | AudioTranscriptionChunk
  | AudioTranslationChunk
  | EmbeddingChunk;

/**
 * Stream error event
 */
export interface StreamErrorEvent {
  type: StreamEventType.Error;
  error: {
    message: string;
    code?: string;
    status?: number;
  };
}

/**
 * Stream done event
 */
export interface StreamDoneEvent {
  type: StreamEventType.Done;
  usage?: {
    prompt_tokens: number;
    completion_tokens: number;
    total_tokens: number;
  };
}