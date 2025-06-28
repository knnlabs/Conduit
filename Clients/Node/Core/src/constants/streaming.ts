/**
 * Server-Sent Events (SSE) and streaming constants.
 */
export const STREAM_CONSTANTS = {
  DONE_MARKER: '[DONE]',
  DATA_PREFIX: 'data: ',
  SSE_COMMENT_PREFIX: ':',
  EVENT_PREFIX: 'event: ',
  ID_PREFIX: 'id: ',
  RETRY_PREFIX: 'retry: ',
} as const;

/**
 * SSE field names.
 */
export const SSE_FIELDS = {
  DATA: 'data',
  EVENT: 'event',
  ID: 'id',
  RETRY: 'retry',
} as const;

/**
 * Stream event types.
 */
export const STREAM_EVENTS = {
  MESSAGE: 'message',
  ERROR: 'error',
  DONE: 'done',
  CHUNK: 'chunk',
} as const;

export type StreamEvent = typeof STREAM_EVENTS[keyof typeof STREAM_EVENTS];

/**
 * Streaming helper utilities.
 */
export const StreamingHelpers = {
  /**
   * Check if a line indicates the stream is done.
   */
  isDoneMarker: (data: string): boolean =>
    data === STREAM_CONSTANTS.DONE_MARKER,

  /**
   * Check if a line is an SSE data line.
   */
  isDataLine: (line: string): boolean =>
    line.startsWith(STREAM_CONSTANTS.DATA_PREFIX),

  /**
   * Check if a line is an SSE comment.
   */
  isCommentLine: (line: string): boolean =>
    line.startsWith(STREAM_CONSTANTS.SSE_COMMENT_PREFIX),

  /**
   * Extract data from an SSE data line.
   */
  extractData: (line: string): string => {
    if (!StreamingHelpers.isDataLine(line)) {
      throw new Error('Line is not a data line');
    }
    return line.slice(STREAM_CONSTANTS.DATA_PREFIX.length);
  },

  /**
   * Check if a line is an SSE event line.
   */
  isEventLine: (line: string): boolean =>
    line.startsWith(STREAM_CONSTANTS.EVENT_PREFIX),

  /**
   * Extract event type from an SSE event line.
   */
  extractEvent: (line: string): string => {
    if (!StreamingHelpers.isEventLine(line)) {
      throw new Error('Line is not an event line');
    }
    return line.slice(STREAM_CONSTANTS.EVENT_PREFIX.length);
  },

  /**
   * Parse an SSE line into its components.
   */
  parseSseLine: (line: string): { field: string; value: string } | null => {
    const colonIndex = line.indexOf(':');
    if (colonIndex === -1) {
      return null;
    }

    const field = line.slice(0, colonIndex);
    let value = line.slice(colonIndex + 1);
    
    // Remove leading space if present
    if (value.startsWith(' ')) {
      value = value.slice(1);
    }

    return { field, value };
  },
} as const;