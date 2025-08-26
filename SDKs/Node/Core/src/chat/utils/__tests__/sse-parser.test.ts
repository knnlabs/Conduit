/**
 * Unit tests for sse-parser utilities
 */

import { SSEParser, SSEEventType, type SSEEvent } from '../sse-parser';

describe('sse-parser', () => {
  describe('SSEParser', () => {
    let parser: SSEParser;

    beforeEach(() => {
      parser = new SSEParser();
    });

    it('should parse simple data-only event', () => {
      const chunk = 'data: {"message": "Hello"}\n\n';
      const events = parser.processChunk(chunk);
      
      expect(events).toHaveLength(1);
      expect(events[0]).toEqual({
        event: SSEEventType.Content,
        data: { message: "Hello" }
      });
    });

    it('should parse event with explicit event type', () => {
      const chunk = 'event: metrics\ndata: {"tokens": 100}\n\n';
      const events = parser.processChunk(chunk);
      
      expect(events).toHaveLength(1);
      expect(events[0]).toEqual({
        event: SSEEventType.Metrics,
        data: { tokens: 100 }
      });
    });

    it('should handle [DONE] marker', () => {
      const chunk = 'data: [DONE]\n\n';
      const events = parser.processChunk(chunk);
      
      expect(events).toHaveLength(1);
      expect(events[0]).toEqual({
        event: SSEEventType.Content,
        data: '[DONE]'
      });
    });

    it('should handle multiple events in one chunk', () => {
      const chunk = 'data: {"first": true}\n\ndata: {"second": true}\n\n';
      const events = parser.processChunk(chunk);
      
      expect(events).toHaveLength(2);
      expect(events[0].data).toEqual({ first: true });
      expect(events[1].data).toEqual({ second: true });
    });

    it('should handle incomplete events across chunks', () => {
      // First chunk with incomplete event
      const chunk1 = 'event: metrics\ndata: {"tok';
      let events = parser.processChunk(chunk1);
      expect(events).toHaveLength(0);

      // Second chunk completes the event
      const chunk2 = 'ens": 100}\n\n';
      events = parser.processChunk(chunk2);
      expect(events).toHaveLength(1);
      expect(events[0]).toEqual({
        event: SSEEventType.Metrics,
        data: { tokens: 100 }
      });
    });

    it('should handle non-JSON data as string', () => {
      const chunk = 'data: plain text\n\n';
      const events = parser.processChunk(chunk);
      
      expect(events).toHaveLength(1);
      expect(events[0]).toEqual({
        event: SSEEventType.Content,
        data: 'plain text'
      });
    });

    it('should flush remaining events', () => {
      // Incomplete event without final newlines (but with complete data line)
      const chunk = 'data: {"pending": true}\n';
      const events = parser.processChunk(chunk);
      expect(events).toHaveLength(0); // No events yet because no empty line

      // Flush should return the pending event
      const flushedEvent = parser.flush();
      expect(flushedEvent).toEqual({
        event: SSEEventType.Content,
        data: { pending: true }
      });
    });

    it('should handle different event types', () => {
      const chunks = [
        'event: content\ndata: {"text": "hello"}\n\n',
        'event: metrics\ndata: {"tokens": 50}\n\n',
        'event: metrics-final\ndata: {"total": 100}\n\n',
        'event: error\ndata: {"error": "failed"}\n\n'
      ];

      const expectedTypes = [
        SSEEventType.Content,
        SSEEventType.Metrics, 
        SSEEventType.MetricsFinal,
        SSEEventType.Error
      ];

      chunks.forEach((chunk, index) => {
        const events = parser.processChunk(chunk);
        expect(events).toHaveLength(1);
        expect(events[0].event).toBe(expectedTypes[index]);
      });
    });

    it('should handle empty lines within events', () => {
      const chunk = 'event: metrics\n\ndata: {"tokens": 100}\n\n';
      const events = parser.processChunk(chunk);
      
      expect(events).toHaveLength(1);
      expect(events[0]).toEqual({
        event: SSEEventType.Metrics,
        data: { tokens: 100 }
      });
    });
  });

  describe('SSEEventType enum', () => {
    it('should have correct string values', () => {
      expect(SSEEventType.Content).toBe('content');
      expect(SSEEventType.Metrics).toBe('metrics');
      expect(SSEEventType.MetricsFinal).toBe('metrics-final');
      expect(SSEEventType.Error).toBe('error');
    });
  });
});