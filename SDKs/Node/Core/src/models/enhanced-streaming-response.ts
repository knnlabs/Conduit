/**
 * Enhanced streaming response that doesn't require BaseStreamChunk constraint
 * This allows for heterogeneous event types in the stream
 */
export interface EnhancedStreamingResponse<T> {
  /**
   * Async iterator for consuming stream events
   */
  [Symbol.asyncIterator](): AsyncIterator<T>;
  
  /**
   * Collects all events and returns them as an array
   */
  toArray(): Promise<T[]>;
  
  /**
   * Cancels the stream
   */
  cancel(): void;
}