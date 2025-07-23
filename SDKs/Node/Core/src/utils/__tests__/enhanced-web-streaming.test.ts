import { describe, it, expect, beforeEach, afterEach, jest } from '@jest/globals';
import { createEnhancedWebStream } from '../enhanced-web-streaming';
import { StreamError } from '../errors';
import type { EnhancedSSEEventType } from '../../models/enhanced-streaming';

// Mock TextDecoder
global.TextDecoder = jest.fn().mockImplementation(() => ({
  decode: (chunk: Uint8Array, options?: { stream?: boolean }) => {
    return new TextDecoder().decode(chunk, options);
  }
}));

describe('enhanced-web-streaming', () => {
  let mockReader: any;
  let mockStream: ReadableStream<Uint8Array>;

  beforeEach(() => {
    mockReader = {
      read: jest.fn(),
      releaseLock: jest.fn()
    };
    
    mockStream = {
      getReader: jest.fn().mockReturnValue(mockReader)
    } as any;
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe('createEnhancedWebStream', () => {
    it('should create an enhanced streaming response', () => {
      const result = createEnhancedWebStream(mockStream);
      
      expect(result).toBeDefined();
      expect(result[Symbol.asyncIterator]).toBeDefined();
      expect(result.toArray).toBeDefined();
      expect(result.cancel).toBeDefined();
    });

    it('should handle abort signal', () => {
      const abortController = new AbortController();
      const mockAbort = jest.fn();
      const abortControllerSpy = jest.spyOn(AbortController.prototype, 'abort').mockImplementation(mockAbort);
      
      createEnhancedWebStream(mockStream, { signal: abortController.signal });
      abortController.abort();
      
      expect(mockAbort).toHaveBeenCalled();
      abortControllerSpy.mockRestore();
    });
  });

  describe('SSE parsing', () => {
    const encoder = new TextEncoder();

    async function* createMockReader(chunks: string[]) {
      for (const chunk of chunks) {
        yield encoder.encode(chunk);
      }
    }

    it('should parse content events', async () => {
      const chunks = [
        'data: {"id":"123","object":"chat.completion.chunk","choices":[{"delta":{"content":"Hello"}}]}\n\n'
      ];
      
      let readIndex = 0;
      mockReader.read.mockImplementation(async () => {
        if (readIndex < chunks.length) {
          return { done: false, value: encoder.encode(chunks[readIndex++]) };
        }
        return { done: true };
      });

      const stream = createEnhancedWebStream(mockStream);
      const events = await stream.toArray();
      
      expect(events).toHaveLength(1);
      expect(events[0].type).toBe('content');
      expect(events[0].data).toMatchObject({
        id: '123',
        object: 'chat.completion.chunk',
        choices: [{ delta: { content: 'Hello' } }]
      });
    });

    it('should parse metrics events', async () => {
      const chunks = [
        'event: metrics\ndata: {"tokens_per_second":42.5,"tokens_generated":10}\n\n'
      ];
      
      let readIndex = 0;
      mockReader.read.mockImplementation(async () => {
        if (readIndex < chunks.length) {
          return { done: false, value: encoder.encode(chunks[readIndex++]) };
        }
        return { done: true };
      });

      const stream = createEnhancedWebStream(mockStream);
      const events = await stream.toArray();
      
      expect(events).toHaveLength(1);
      expect(events[0].type).toBe('metrics');
      expect(events[0].data).toMatchObject({
        tokens_per_second: 42.5,
        tokens_generated: 10
      });
    });

    it('should parse metrics-final events', async () => {
      const chunks = [
        'event: metrics-final\ndata: {"total_latency_ms":1500,"completion_tokens":25}\n\n'
      ];
      
      let readIndex = 0;
      mockReader.read.mockImplementation(async () => {
        if (readIndex < chunks.length) {
          return { done: false, value: encoder.encode(chunks[readIndex++]) };
        }
        return { done: true };
      });

      const stream = createEnhancedWebStream(mockStream);
      const events = await stream.toArray();
      
      expect(events).toHaveLength(1);
      expect(events[0].type).toBe('metrics-final');
      expect(events[0].data).toMatchObject({
        total_latency_ms: 1500,
        completion_tokens: 25
      });
    });

    it('should handle [DONE] marker', async () => {
      const chunks = [
        'data: [DONE]\n\n'
      ];
      
      let readIndex = 0;
      mockReader.read.mockImplementation(async () => {
        if (readIndex < chunks.length) {
          return { done: false, value: encoder.encode(chunks[readIndex++]) };
        }
        return { done: true };
      });

      const stream = createEnhancedWebStream(mockStream);
      const events = await stream.toArray();
      
      expect(events).toHaveLength(1);
      expect(events[0].type).toBe('done');
      expect(events[0].data).toBe('[DONE]');
    });

    it('should handle multiple events in one chunk', async () => {
      const chunks = [
        'data: {"id":"1","object":"chat.completion.chunk"}\n\nevent: metrics\ndata: {"tokens_per_second":30}\n\n'
      ];
      
      let readIndex = 0;
      mockReader.read.mockImplementation(async () => {
        if (readIndex < chunks.length) {
          return { done: false, value: encoder.encode(chunks[readIndex++]) };
        }
        return { done: true };
      });

      const stream = createEnhancedWebStream(mockStream);
      const events = await stream.toArray();
      
      expect(events).toHaveLength(2);
      expect(events[0].type).toBe('content');
      expect(events[0].data).toMatchObject({ id: '1' });
      expect(events[1].type).toBe('metrics');
      expect(events[1].data).toMatchObject({ tokens_per_second: 30 });
    });

    it('should handle split chunks', async () => {
      const chunks = [
        'event: met',
        'rics\ndata: {"tokens_per_second"',
        ':42.5}\n\n'
      ];
      
      let readIndex = 0;
      mockReader.read.mockImplementation(async () => {
        if (readIndex < chunks.length) {
          return { done: false, value: encoder.encode(chunks[readIndex++]) };
        }
        return { done: true };
      });

      const stream = createEnhancedWebStream(mockStream);
      const events = await stream.toArray();
      
      expect(events).toHaveLength(1);
      expect(events[0].type).toBe('metrics');
      expect(events[0].data).toMatchObject({ tokens_per_second: 42.5 });
    });
  });

  describe('error handling', () => {
    const encoder = new TextEncoder();

    it('should handle malformed JSON', async () => {
      const onError = jest.fn();
      const chunks = [
        'data: {invalid json}\n\n'
      ];
      
      let readIndex = 0;
      mockReader.read.mockImplementation(async () => {
        if (readIndex < chunks.length) {
          return { done: false, value: encoder.encode(chunks[readIndex++]) };
        }
        return { done: true };
      });

      const stream = createEnhancedWebStream(mockStream, { onError });
      const events = await stream.toArray();
      
      expect(events).toHaveLength(0);
      expect(onError).toHaveBeenCalledWith(expect.any(StreamError));
      expect(onError.mock.calls[0][0].message).toContain('Failed to parse SSE content event');
    });

    it('should handle timeout', async () => {
      const timeout = 100; // 100ms timeout
      
      mockReader.read.mockImplementation(async () => {
        // Simulate a hanging stream
        await new Promise(resolve => setTimeout(resolve, 200));
        return { done: false, value: new Uint8Array() };
      });

      const stream = createEnhancedWebStream(mockStream, { timeout });
      
      await expect(stream.toArray()).rejects.toThrow(StreamError);
      await expect(stream.toArray()).rejects.toThrow(`Stream timeout after ${timeout}ms`);
    });

    it('should handle large chunks', async () => {
      const largeData = 'x'.repeat(2 * 1024 * 1024); // 2MB
      const chunks = [`data: "${largeData}"\n\n`];
      
      let readIndex = 0;
      mockReader.read.mockImplementation(async () => {
        if (readIndex < chunks.length) {
          return { done: false, value: encoder.encode(chunks[readIndex++]) };
        }
        return { done: true };
      });

      const stream = createEnhancedWebStream(mockStream);
      
      await expect(stream.toArray()).rejects.toThrow(StreamError);
      await expect(stream.toArray()).rejects.toThrow('Stream chunk too large');
    });

    it('should handle malformed SSE lines', async () => {
      const onError = jest.fn();
      const chunks = [
        'invalid: line without data or event\n',
        'data: {"valid": true}\n\n'
      ];
      
      let readIndex = 0;
      mockReader.read.mockImplementation(async () => {
        if (readIndex < chunks.length) {
          return { done: false, value: encoder.encode(chunks[readIndex++]) };
        }
        return { done: true };
      });

      const stream = createEnhancedWebStream(mockStream, { onError });
      const events = await stream.toArray();
      
      expect(events).toHaveLength(1);
      expect(events[0].data).toMatchObject({ valid: true });
      expect(onError).toHaveBeenCalledWith(expect.any(StreamError));
      expect(onError.mock.calls[0][0].message).toContain('Malformed SSE line');
    });

    it('should handle empty event data', async () => {
      const onError = jest.fn();
      const chunks = [
        'event: metrics\ndata: \n\n'
      ];
      
      let readIndex = 0;
      mockReader.read.mockImplementation(async () => {
        if (readIndex < chunks.length) {
          return { done: false, value: encoder.encode(chunks[readIndex++]) };
        }
        return { done: true };
      });

      const stream = createEnhancedWebStream(mockStream, { onError });
      const events = await stream.toArray();
      
      expect(events).toHaveLength(0);
      expect(onError).toHaveBeenCalledWith(expect.any(StreamError));
      expect(onError.mock.calls[0][0].message).toBe('Empty event data');
    });
  });

  describe('stream control', () => {
    it('should cancel the stream', async () => {
      const abortSpy = jest.fn();
      jest.spyOn(AbortController.prototype, 'abort').mockImplementation(abortSpy);
      
      mockReader.read.mockImplementation(async () => {
        // Infinite stream
        return { done: false, value: new Uint8Array() };
      });

      const stream = createEnhancedWebStream(mockStream);
      stream.cancel();
      
      expect(abortSpy).toHaveBeenCalled();
    });

    it('should release reader lock on completion', async () => {
      const chunks = ['data: {"test": true}\n\n'];
      
      let readIndex = 0;
      mockReader.read.mockImplementation(async () => {
        if (readIndex < chunks.length) {
          return { done: false, value: new TextEncoder().encode(chunks[readIndex++]) };
        }
        return { done: true };
      });

      const stream = createEnhancedWebStream(mockStream);
      await stream.toArray();
      
      expect(mockReader.releaseLock).toHaveBeenCalled();
    });

    it('should release reader lock on error', async () => {
      mockReader.read.mockRejectedValue(new Error('Read error'));

      const stream = createEnhancedWebStream(mockStream);
      
      try {
        await stream.toArray();
      } catch {
        // Expected error
      }
      
      expect(mockReader.releaseLock).toHaveBeenCalled();
    });
  });
});