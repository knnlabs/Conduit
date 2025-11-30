/**
 * Server-Sent Events parser for chat streaming
 * Extracted from WebAdmin for reuse in other applications
 */

/**
 * SSE event types from Core API
 * Combines OpenAI-compatible standard events with Conduit-specific extensions
 */
export enum SSEEventType {
  /** Standard OpenAI content chunks containing delta updates */
  Content = 'content',
  /** Conduit extension: Model reasoning/thinking content separate from main response */
  Reasoning = 'reasoning',
  /** Conduit extension: Tool/function execution status and progress updates */
  ToolExecuting = 'tool-executing',
  /** Conduit extension: Individual tool execution results (optional, for detailed logging) */
  ToolResult = 'tool-result',
  /** Conduit extension: Real-time performance metrics during streaming */
  Metrics = 'metrics',
  /** Conduit extension: Final performance metrics at stream completion */
  MetricsFinal = 'metrics-final',
  /** Conduit extension: Error events during streaming */
  Error = 'error'
}

/**
 * Parsed SSE event
 */
export interface SSEEvent {
  event?: SSEEventType;
  data: unknown;
}

/**
 * Parse SSE events that may include event type
 * Format can be:
 * - data: {...} (default content event)
 * - event: metrics\ndata: {...}
 * - event: metrics-final\ndata: {...}
 */
export class SSEParser {
  private buffer = '';
  private currentEvent: Partial<SSEEvent> = {};

  /**
   * Process a chunk of data and extract complete events
   */
  processChunk(chunk: string): SSEEvent[] {
    this.buffer += chunk;
    const events: SSEEvent[] = [];
    
    const lines = this.buffer.split('\n');
    this.buffer = lines.pop() ?? ''; // Keep incomplete line in buffer
    
    for (const line of lines) {
      if (line.trim() === '') {
        // Empty line signals end of event
        if (this.currentEvent.data !== undefined) {
          events.push(this.createEvent());
          this.currentEvent = {};
        }
        continue;
      }
      
      if (line.startsWith('event:')) {
        this.currentEvent.event = line.slice(6).trim() as SSEEventType;
      } else if (line.startsWith('data:')) {
        const data = line.slice(5).trim();
        
        if (data === '[DONE]') {
          events.push({ event: SSEEventType.Content, data: '[DONE]' });
          this.currentEvent = {};
        } else {
          try {
            this.currentEvent.data = JSON.parse(data);
          } catch {
            // If not JSON, store as string
            this.currentEvent.data = data;
          }
        }
      }
    }
    
    return events;
  }
  
  /**
   * Get any remaining buffered event
   */
  flush(): SSEEvent | null {
    if (this.currentEvent.data !== undefined) {
      const event = this.createEvent();
      this.currentEvent = {};
      this.buffer = '';
      return event;
    }
    return null;
  }
  
  private createEvent(): SSEEvent {
    return {
      event: this.currentEvent.event ?? SSEEventType.Content,
      data: this.currentEvent.data
    };
  }
}

/**
 * Helper to parse SSE stream with proper event handling
 */
export async function* parseSSEStream(
  reader: ReadableStreamDefaultReader<Uint8Array>
): AsyncGenerator<SSEEvent, void, unknown> {
  const decoder = new TextDecoder();
  const parser = new SSEParser();
  
  while (true) {
    const { done, value } = await reader.read();
    
    if (done) {
      // Flush any remaining event
      const lastEvent = parser.flush();
      if (lastEvent) {
        yield lastEvent;
      }
      break;
    }
    
    const chunk = decoder.decode(value, { stream: true });
    const events = parser.processChunk(chunk);
    
    for (const event of events) {
      yield event;
    }
  }
}