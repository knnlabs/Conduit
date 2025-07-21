import type { SSEMessage, StreamEvent, StreamingResponse, StreamOptions, ProgressEvent, BaseStreamChunk } from '../models/streaming';
import type { ChatCompletionChunk } from '../models/chat';
import { StreamError } from './errors';
import { StreamingHelpers } from '../constants';
import { createStreamingResponse } from './stream-response';

export function parseSSEMessage(line: string): SSEMessage | null {
  if (!line || StreamingHelpers.isCommentLine(line)) {
    return null;
  }

  const message: SSEMessage = { data: '' };
  const colonIndex = line.indexOf(':');

  if (colonIndex === -1) {
    message.data = line;
  } else {
    const field = line.substring(0, colonIndex);
    const value = line.substring(colonIndex + 1).trim();

    switch (field) {
      case 'data':
        message.data = value;
        break;
      case 'event':
        message.event = value;
        break;
      case 'id':
        message.id = value;
        break;
      case 'retry':
        message.retry = parseInt(value, 10);
        break;
    }
  }

  return message;
}

export function parseSSEStream(text: string): SSEMessage[] {
  const lines = text.split('\n');
  const messages: SSEMessage[] = [];
  let currentMessage: Partial<SSEMessage> = {};

  for (const line of lines) {
    if (line.trim() === '') {
      if (currentMessage.data !== undefined) {
        messages.push(currentMessage as SSEMessage);
        currentMessage = {};
      }
      continue;
    }

    const parsed = parseSSEMessage(line);
    if (parsed) {
      Object.assign(currentMessage, parsed);
    }
  }

  if (currentMessage.data !== undefined) {
    messages.push(currentMessage as SSEMessage);
  }

  return messages;
}

export function parseStreamEvent(data: string): StreamEvent | null {
  if (StreamingHelpers.isDoneMarker(data)) {
    return '[DONE]';
  }

  try {
    return JSON.parse(data) as ChatCompletionChunk;
  } catch {
    throw new StreamError(`Failed to parse stream event: ${data}`);
  }
}

export async function* streamAsyncIterator(
  stream: NodeJS.ReadableStream,
  options?: StreamOptions
): AsyncGenerator<ChatCompletionChunk, void, unknown> {
  let buffer = '';
  let totalBytes = 0;

  try {
    if (options?.onStart) {
      options.onStart();
    }

    for await (const chunk of stream) {
      buffer += chunk.toString();
      totalBytes += chunk.length;
      
      if (options?.onProgress) {
        const progressEvent: ProgressEvent = {
          loaded: totalBytes,
          total: undefined,
          percentage: undefined,
        };
        options.onProgress(progressEvent);
      }
      
      const lines = buffer.split('\n');
      buffer = lines.pop() ?? '';

      for (const line of lines) {
        const trimmedLine = line.trim();
        if (trimmedLine === '' || StreamingHelpers.isCommentLine(trimmedLine)) {
          continue;
        }

        if (StreamingHelpers.isDataLine(trimmedLine)) {
          const data = StreamingHelpers.extractData(trimmedLine);
          
          if (StreamingHelpers.isDoneMarker(data)) {
            if (options?.onEnd) {
              options.onEnd();
            }
            return;
          }

          try {
            const event = JSON.parse(data) as ChatCompletionChunk;
            yield event;
          } catch {
            const streamError = new StreamError(`Failed to parse stream event: ${data}`);
            if (options?.onError) {
              options.onError(streamError);
            }
            throw streamError;
          }
        }
      }
    }

    if (buffer.trim()) {
      console.warn('Unprocessed data in buffer:', buffer);
    }
    
    if (options?.onEnd) {
      options.onEnd();
    }
  } catch (error) {
    if (options?.onError && error instanceof Error) {
      options.onError(error);
    }
    throw error;
  }
}

/**
 * Creates a typed streaming response from a readable stream
 */
export function createTypedStream<T extends BaseStreamChunk>(
  stream: NodeJS.ReadableStream,
  options?: StreamOptions
): StreamingResponse<T> {
  const abortController = new AbortController();
  
  // If the options signal is aborted, abort our controller too
  if (options?.signal) {
    options.signal.addEventListener('abort', () => abortController.abort());
  }
  
  const generator = streamAsyncIterator(stream, options) as AsyncGenerator<T, void, unknown>;
  return createStreamingResponse(generator, abortController);
}