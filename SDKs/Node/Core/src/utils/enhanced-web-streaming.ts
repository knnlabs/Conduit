import type { StreamOptions } from '../models/streaming';
import type { EnhancedStreamEvent, EnhancedSSEEventType } from '../models/enhanced-streaming';
import type { EnhancedStreamingResponse } from '../models/enhanced-streaming-response';
import { StreamError } from './errors';

/**
 * Creates an enhanced streaming response that preserves SSE event types
 */
export function createEnhancedWebStream(
  stream: ReadableStream<Uint8Array>,
  options?: StreamOptions
): EnhancedStreamingResponse<EnhancedStreamEvent> {
  const abortController = new AbortController();
  
  // If the options signal is aborted, abort our controller too
  if (options?.signal) {
    options.signal.addEventListener('abort', () => abortController.abort());
  }
  
  const generator = enhancedWebStreamAsyncIterator(stream, options);
  
  // Create the enhanced streaming response
  return {
    async *[Symbol.asyncIterator]() {
      yield* generator;
    },
    
    async toArray(): Promise<EnhancedStreamEvent[]> {
      const events: EnhancedStreamEvent[] = [];
      for await (const event of generator) {
        events.push(event);
      }
      return events;
    },
    
    cancel(): void {
      abortController.abort();
    }
  };
}

async function* enhancedWebStreamAsyncIterator(
  stream: ReadableStream<Uint8Array>,
  options?: StreamOptions
): AsyncGenerator<EnhancedStreamEvent, void, unknown> {
  const reader = stream.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  let currentEventType: string | undefined;
  let currentData = '';
  let lineNumber = 0;
  const startTime = Date.now();
  const timeout = options?.timeout ?? 300000; // 5 minutes default

  try {
    while (true) {
      // Check for timeout
      if (Date.now() - startTime > timeout) {
        throw new StreamError(`Stream timeout after ${timeout}ms`);
      }

      const { done, value } = await reader.read();
      
      if (done) {
        break;
      }

      // Validate chunk size
      if (value.length > 1048576) { // 1MB limit per chunk
        throw new StreamError(`Stream chunk too large: ${value.length} bytes`);
      }

      buffer += decoder.decode(value, { stream: true });
      const lines = buffer.split('\n');
      
      // Keep the last line if it's incomplete
      buffer = lines.pop() ?? '';

      for (const line of lines) {
        lineNumber++;
        const trimmedLine = line.trim();
        
        // Empty line signals end of event
        if (trimmedLine === '') {
          if (currentData) {
            const event = processEvent(currentEventType, currentData, options);
            if (event) {
              yield event;
            }
            currentEventType = undefined;
            currentData = '';
          }
          continue;
        }
        
        // Parse event type
        if (line.startsWith('event: ')) {
          const eventType = line.slice(7).trim();
          // Validate event type
          if (eventType.length > 50) {
            if (options?.onError) {
              options.onError(new StreamError(`Invalid event type at line ${lineNumber}: too long`));
            }
            continue;
          }
          currentEventType = eventType;
        } 
        // Parse data
        else if (line.startsWith('data: ')) {
          const data = line.slice(6);
          // Prevent excessive data accumulation
          if (currentData.length + data.length > 1048576) { // 1MB limit
            if (options?.onError) {
              options.onError(new StreamError(`Data too large at line ${lineNumber}`));
            }
            currentData = '';
            currentEventType = undefined;
            continue;
          }
          if (currentData) {
            currentData += '\n' + data;
          } else {
            currentData = data;
          }
        }
        // Ignore other fields or malformed lines
        else if (!line.startsWith(':')) { // Comments start with :
          if (options?.onError) {
            options.onError(new StreamError(`Malformed SSE line at ${lineNumber}: ${line}`));
          }
        }
      }
    }

    // Process any remaining event
    if (currentData) {
      const event = processEvent(currentEventType, currentData, options);
      if (event) {
        yield event;
      }
    }
  } finally {
    reader.releaseLock();
  }
}

function processEvent(
  eventType: string | undefined,
  data: string,
  options?: StreamOptions
): EnhancedStreamEvent | null {
  // Validate data
  if (!data || data.length === 0) {
    if (options?.onError) {
      options.onError(new StreamError('Empty event data'));
    }
    return null;
  }

  // Handle [DONE] marker
  if (data === '[DONE]') {
    return {
      type: 'done' as EnhancedSSEEventType,
      data: '[DONE]'
    };
  }

  // Determine event type
  let type: EnhancedSSEEventType;
  switch (eventType) {
    case 'metrics':
      type = 'metrics' as EnhancedSSEEventType;
      break;
    case 'metrics-final':
      type = 'metrics-final' as EnhancedSSEEventType;
      break;
    case 'error':
      type = 'error' as EnhancedSSEEventType;
      break;
    default:
      // Default to content event for backwards compatibility
      type = 'content' as EnhancedSSEEventType;
  }

  try {
    const parsed = JSON.parse(data);
    
    // Basic validation based on event type
    if (type === 'content' && parsed && typeof parsed === 'object' && !parsed.object) {
      if (options?.onError) {
        options.onError(new StreamError('Invalid content event: missing object field'));
      }
      return null;
    }
    
    return {
      type,
      data: parsed
    };
  } catch (error) {
    if (options?.onError) {
      options.onError(new StreamError(`Failed to parse SSE ${type} event: ${error instanceof Error ? error.message : 'Unknown error'}`, { cause: error }));
    }
    // For non-critical errors, try to return raw data
    if (type === 'error') {
      return {
        type,
        data: { message: data, parse_error: true }
      };
    }
    return null;
  }
}