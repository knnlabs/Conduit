import type { StreamingResponse, StreamOptions } from '../models/streaming';
import { StreamError } from './errors';
import { createStreamingResponse } from './stream-response';

/**
 * Creates a typed streaming response from a web ReadableStream
 */
export function createWebStream<T>(
  stream: ReadableStream<Uint8Array>,
  options?: StreamOptions
): StreamingResponse<T> {
  const abortController = new AbortController();
  
  // If the options signal is aborted, abort our controller too
  if (options?.signal) {
    options.signal.addEventListener('abort', () => abortController.abort());
  }
  
  const generator = webStreamAsyncIterator<T>(stream, options);
  return createStreamingResponse(generator, abortController);
}

async function* webStreamAsyncIterator<T>(
  stream: ReadableStream<Uint8Array>,
  options?: StreamOptions
): AsyncGenerator<T, void, unknown> {
  const reader = stream.getReader();
  const decoder = new TextDecoder();
  let buffer = '';

  try {
    while (true) {
      const { done, value } = await reader.read();
      
      if (done) {
        break;
      }

      buffer += decoder.decode(value, { stream: true });
      const lines = buffer.split('\n');
      
      // Keep the last line if it's incomplete
      buffer = lines.pop() || '';

      for (const line of lines) {
        if (line.trim() === '') continue;
        
        if (line.startsWith('data: ')) {
          const data = line.slice(6);
          
          if (data === '[DONE]') {
            return;
          }

          try {
            const parsed = JSON.parse(data) as T;
            yield parsed;
          } catch (error) {
            if (options?.onError) {
              options.onError(new StreamError('Failed to parse SSE message', { cause: error }));
            }
          }
        }
      }
    }

    // Process any remaining data in buffer
    if (buffer.trim() && buffer.startsWith('data: ')) {
      const data = buffer.slice(6);
      if (data !== '[DONE]') {
        try {
          const parsed = JSON.parse(data) as T;
          yield parsed;
        } catch (error) {
          if (options?.onError) {
            options.onError(new StreamError('Failed to parse final SSE message', { cause: error }));
          }
        }
      }
    }
  } finally {
    reader.releaseLock();
  }
}