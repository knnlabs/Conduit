/**
 * Enhanced streaming response interface for handling heterogeneous SSE event types.
 * Unlike the standard StreamingResponse, this doesn't require events to extend BaseStreamChunk,
 * allowing for different event types (content, metrics, errors) in the same stream.
 * 
 * @interface EnhancedStreamingResponse
 * @template T The type of events in the stream (typically EnhancedStreamEvent)
 * @since 0.3.0
 * 
 * @example
 * ```typescript
 * const stream = await client.chat.createEnhancedStream({
 *   model: 'gpt-4',
 *   messages: [{ role: 'user', content: 'Hello!' }],
 *   stream: true
 * });
 * 
 * // Iterate over events
 * for await (const event of stream) {
 *   switch (event.type) {
 *     case 'content':
 *       console.log('Content:', event.data);
 *       break;
 *     case 'metrics':
 *       console.log('Metrics:', event.data);
 *       break;
 *   }
 * }
 * 
 * // Or collect all events
 * const allEvents = await stream.toArray();
 * 
 * // Cancel the stream early
 * stream.cancel();
 * ```
 */
export interface EnhancedStreamingResponse<T> {
  /**
   * Async iterator for consuming stream events.
   * Allows using for-await-of loops to process events as they arrive.
   * 
   * @returns {AsyncIterator<T>} An async iterator that yields events
   */
  [Symbol.asyncIterator](): AsyncIterator<T>;
  
  /**
   * Collects all remaining events in the stream and returns them as an array.
   * This will consume the entire stream, so it should not be used with large streams.
   * 
   * @returns {Promise<T[]>} A promise that resolves to an array of all events
   * @throws {Error} If the stream encounters an error during collection
   */
  toArray(): Promise<T[]>;
  
  /**
   * Cancels the stream, stopping any ongoing data transfer.
   * This is useful for early termination of long-running streams.
   * 
   * @returns {void}
   */
  cancel(): void;
}