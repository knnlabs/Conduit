/**
 * Unit tests for ChatStreamingManager
 */

import { ChatStreamingManager } from '../chat-streaming-manager';
import type { StreamingConfig, StreamingCallbacks, StreamMessageOptions } from '../types';

// Mock fetch globally
const mockFetch = jest.fn();
global.fetch = mockFetch;

// Mock UUID
jest.mock('uuid', () => ({
  v4: () => 'test-uuid-123'
}));

// Mock SSE stream parser
jest.mock('../../utils', () => ({
  parseSSEStream: jest.fn(),
  buildMessageContent: jest.fn((text: string) => text),
  SSEEventType: {
    Content: 'content',
    Metrics: 'metrics',
    MetricsFinal: 'metrics-final',
    Error: 'error'
  }
}));

const { parseSSEStream, SSEEventType } = require('../../utils');

describe('ChatStreamingManager', () => {
  let manager: ChatStreamingManager;
  let mockConfig: StreamingConfig;
  let mockCallbacks: StreamingCallbacks;
  let mockReader: { releaseLock: jest.MockedFunction<() => void> };

  beforeEach(() => {
    mockReader = {
      releaseLock: jest.fn()
    };
    jest.clearAllMocks();
    
    mockConfig = {
      apiEndpoint: '/api/chat/completions',
      timeoutMs: 30000,
      trackPerformanceMetrics: true,
      showTokensPerSecond: true,
      useServerMetrics: true,
      enableLogging: false
    };

    mockCallbacks = {
      onStart: jest.fn(),
      onContent: jest.fn(),
      onChunk: jest.fn(),
      onMetrics: jest.fn(),
      onTokensPerSecond: jest.fn(),
      onComplete: jest.fn(),
      onError: jest.fn(),
      onAbort: jest.fn()
    };

    manager = new ChatStreamingManager(mockConfig);
  });

  describe('constructor', () => {
    it('should initialize with default config values', () => {
      const minimalConfig = { apiEndpoint: '/api/test' };
      const testManager = new ChatStreamingManager(minimalConfig);
      
      expect(testManager.isStreaming()).toBe(false);
      
      const state = testManager.getState();
      expect(state.isStreaming).toBe(false);
      expect(state.totalContent).toBe('');
      expect(state.abortController).toBeNull();
    });

    it('should merge provided config with defaults', () => {
      const customConfig = {
        apiEndpoint: '/custom',
        timeoutMs: 60000,
        enableLogging: true
      };
      
      const testManager = new ChatStreamingManager(customConfig);
      expect(testManager.getState()).toBeDefined();
    });
  });

  describe('sendMessage', () => {
    beforeEach(() => {
      mockFetch.mockResolvedValue({
        ok: true,
        json: jest.fn().mockResolvedValue({
          id: 'test-response',
          object: 'chat.completion',
          created: 1234567890,
          model: 'test-model',
          choices: [{
            index: 0,
            message: { role: 'assistant', content: 'Test response' },
            finish_reason: 'stop'
          }],
          usage: { total_tokens: 10, prompt_tokens: 5, completion_tokens: 5 }
        })
      });
    });

    it('should send non-streaming message successfully', async () => {
      const options = {
        model: 'test-model',
        temperature: 0.7,
        stream: false as const
      };

      const response = await manager.sendMessage('Hello', options);

      expect(response).toBeDefined();
      expect(response.choices[0].message.content).toBe('Test response');
      expect(mockFetch).toHaveBeenCalledWith('/api/chat/completions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: expect.stringContaining('"stream":false'),
        signal: undefined
      });
    });

    it('should include dynamic parameters in request', async () => {
      const options = {
        model: 'test-model',
        dynamicParameters: { custom_param: 'value' }
      };

      await manager.sendMessage('Hello', options);

      const callArgs = mockFetch.mock.calls[0][1];
      const requestBody = JSON.parse(callArgs.body);
      expect(requestBody.custom_param).toBe('value');
    });

    it('should throw error when already streaming', async () => {
      // Use a mock that will set streaming state synchronously
      mockFetch.mockImplementation(async () => {
        // This ensures we're in the streaming state when this returns
        expect(manager.isStreaming()).toBe(true);
        
        return {
          ok: true,
          body: {
            getReader: jest.fn().mockReturnValue({
              releaseLock: jest.fn()
            })
          }
        };
      });

      parseSSEStream.mockImplementation(async function* () {
        // Simulate a stream that can be interrupted
        try {
          await new Promise((resolve) => {
            // Resolve after a short delay to allow the test to proceed
            setTimeout(resolve, 50);
          });
        } catch {
          // Handle any errors gracefully
          return;
        }
      });

      const options = { model: 'test-model', stream: true as const };
      
      // Start first stream (don't await, but start it)
      const streamPromise = manager.streamMessage('Hello', options, mockCallbacks);
      
      // Wait for the async operation to start
      await new Promise(resolve => setTimeout(resolve, 0));
      
      // Now the manager should be in streaming state
      expect(manager.isStreaming()).toBe(true);
      
      // Try to send another message while streaming
      await expect(manager.sendMessage('Hello', { model: 'test' }))
        .rejects.toThrow('Another streaming request is in progress');
        
      // Clean up
      manager.abort();
      await expect(streamPromise).resolves.toBeUndefined();
    });

    it('should handle HTTP errors', async () => {
      // Reset fetch mock to default error behavior
      mockFetch.mockResolvedValue({
        ok: false,
        status: 400,
        statusText: 'Bad Request'
      });

      await expect(manager.sendMessage('Hello', { model: 'test' }))
        .rejects.toThrow('HTTP 400: Bad Request');
    });
  });

  describe('streamMessage', () => {
    let mockResponse: { ok: boolean; body: { getReader: () => typeof mockReader } };

    beforeEach(() => {

      mockResponse = {
        ok: true,
        body: {
          getReader: jest.fn().mockReturnValue(mockReader)
        }
      };

      mockFetch.mockResolvedValue(mockResponse);
    });

    it('should handle successful streaming', async () => {
      const mockEvents = [
        {
          event: SSEEventType.Content,
          data: { choices: [{ delta: { content: 'Hello' } }] }
        },
        {
          event: SSEEventType.Content,
          data: { choices: [{ delta: { content: ' world' } }] }
        },
        { data: '[DONE]' }
      ];

      parseSSEStream.mockImplementation(async function* () {
        for (const event of mockEvents) {
          yield event;
        }
      });

      const options: StreamMessageOptions = {
        model: 'test-model',
        stream: true,
        temperature: 0.7
      };

      await manager.streamMessage('Hello', options, mockCallbacks);

      expect(mockCallbacks.onStart).toHaveBeenCalled();
      expect(mockCallbacks.onContent).toHaveBeenCalledWith('Hello', 'Hello');
      expect(mockCallbacks.onContent).toHaveBeenCalledWith(' world', 'Hello world');
      expect(mockCallbacks.onComplete).toHaveBeenCalledWith({
        content: 'Hello world',
        metadata: expect.objectContaining({ streaming: true })
      });
      expect(mockReader.releaseLock).toHaveBeenCalled();
    });

    it('should handle metrics events', async () => {
      const mockEvents = [
        {
          event: SSEEventType.Metrics,
          data: { tokens_per_second: 50, provider: 'test-provider' }
        },
        { data: '[DONE]' }
      ];

      parseSSEStream.mockImplementation(async function* () {
        for (const event of mockEvents) {
          yield event;
        }
      });

      const options: StreamMessageOptions = {
        model: 'test-model',
        stream: true
      };

      await manager.streamMessage('Hello', options, mockCallbacks);

      expect(mockCallbacks.onMetrics).toHaveBeenCalledWith({
        tokens_per_second: 50,
        provider: 'test-provider'
      });
      expect(mockCallbacks.onTokensPerSecond).toHaveBeenCalledWith(50);
    });

    it('should handle error events', async () => {
      const mockEvents = [
        {
          event: SSEEventType.Error,
          data: { error: 'Test error', statusCode: 400 }
        }
      ];

      parseSSEStream.mockImplementation(async function* () {
        for (const event of mockEvents) {
          yield event;
        }
      });

      const options: StreamMessageOptions = {
        model: 'test-model',
        stream: true
      };

      await expect(manager.streamMessage('Hello', options, mockCallbacks))
        .rejects.toThrow('Stream error: Test error');

      expect(mockCallbacks.onError).toHaveBeenCalledWith(
        expect.objectContaining({
          message: 'Stream error: Test error',
          status: 400
        })
      );
    });

    it('should handle conversation history', async () => {
      parseSSEStream.mockImplementation(async function* () {
        yield { data: '[DONE]' };
      });

      const options: StreamMessageOptions = {
        model: 'test-model',
        stream: true,
        messages: [
          { role: 'user', content: 'Previous message' },
          { role: 'assistant', content: 'Previous response' }
        ]
      };

      await manager.streamMessage('Current message', options, mockCallbacks);

      const callArgs = mockFetch.mock.calls[0][1];
      const requestBody = JSON.parse(callArgs.body);
      expect(requestBody.messages).toHaveLength(3); // Previous 2 + current 1
      expect(requestBody.messages[0].content).toBe('Previous message');
      expect(requestBody.messages[1].content).toBe('Previous response');
      expect(requestBody.messages[2].content).toBe('Current message');
    });

    it('should handle system prompt', async () => {
      parseSSEStream.mockImplementation(async function* () {
        yield { data: '[DONE]' };
      });

      const options: StreamMessageOptions = {
        model: 'test-model',
        stream: true,
        systemPrompt: 'You are a helpful assistant'
      };

      await manager.streamMessage('Hello', options, mockCallbacks);

      const callArgs = mockFetch.mock.calls[0][1];
      const requestBody = JSON.parse(callArgs.body);
      expect(requestBody.messages[0]).toEqual({
        role: 'system',
        content: 'You are a helpful assistant'
      });
    });

    it('should prevent multiple concurrent streams', async () => {
      parseSSEStream.mockImplementation(async function* () {
        // Simulate long-running stream that never sends [DONE]
        await new Promise(resolve => setTimeout(resolve, 100));
        // Never yields anything, simulating hanging stream
      });

      const options: StreamMessageOptions = {
        model: 'test-model',
        stream: true
      };

      // Start first stream
      const firstStream = manager.streamMessage('Hello 1', options, mockCallbacks);

      // Try to start second stream
      await expect(manager.streamMessage('Hello 2', options, mockCallbacks))
        .rejects.toThrow('Another streaming request is already in progress');

      // Clean up first stream
      manager.abort();
      await expect(firstStream).resolves.toBeUndefined(); // Should resolve without error due to abort
    });
  });

  describe('abort', () => {
    it('should abort active streaming', async () => {
      // Mock fetch to throw AbortError when signal is aborted
      mockFetch.mockImplementation(async (url, options) => {
        return new Promise((resolve, reject) => {
          const signal = options?.signal as AbortSignal;
          if (signal) {
            signal.addEventListener('abort', () => {
              const abortError = new Error('The operation was aborted');
              abortError.name = 'AbortError';
              reject(abortError);
            });
          }
          
          // Never resolve to simulate hanging request
          setTimeout(() => {
            if (!signal?.aborted) {
              resolve(mockResponse);
            }
          }, 1000);
        });
      });

      const options: StreamMessageOptions = {
        model: 'test-model',
        stream: true
      };

      const streamPromise = manager.streamMessage('Hello', options, mockCallbacks);
      
      // Give stream time to start
      await new Promise(resolve => setTimeout(resolve, 10));
      
      expect(manager.isStreaming()).toBe(true);
      
      manager.abort();
      
      expect(manager.isStreaming()).toBe(false);
      
      // Stream should complete due to abort
      await expect(streamPromise).resolves.toBeUndefined();
      expect(mockCallbacks.onAbort).toHaveBeenCalled();
    });

    it('should handle abort when not streaming', () => {
      // Reset fetch mock to default behavior
      mockFetch.mockResolvedValue({
        ok: true,
        json: jest.fn().mockResolvedValue({}),
        body: {
          getReader: jest.fn().mockReturnValue(mockReader)
        }
      });
      expect(manager.isStreaming()).toBe(false);
      
      // Should not throw
      expect(() => manager.abort()).not.toThrow();
      
      expect(manager.isStreaming()).toBe(false);
    });
  });

  describe('state management', () => {
    it('should provide read-only state', () => {
      const state = manager.getState();
      
      expect(state).toEqual({
        isStreaming: false,
        totalContent: '',
        startTime: 0,
        metrics: {},
        abortController: null
      });

      // Verify it's a copy, not the original
      state.isStreaming = true;
      expect(manager.getState().isStreaming).toBe(false);
    });

    it('should update state during streaming', async () => {
      // Reset fetch mock to default behavior
      mockFetch.mockResolvedValue({
        ok: true,
        body: {
          getReader: jest.fn().mockReturnValue(mockReader)
        }
      });

      parseSSEStream.mockImplementation(async function* () {
        yield {
          event: SSEEventType.Content,
          data: { choices: [{ delta: { content: 'test' } }] }
        };
        yield { data: '[DONE]' };
      });

      const options: StreamMessageOptions = {
        model: 'test-model',
        stream: true
      };

      await manager.streamMessage('Hello', options, mockCallbacks);

      const finalState = manager.getState();
      expect(finalState.isStreaming).toBe(false);
      expect(finalState.totalContent).toBe('test'); // Content is preserved after streaming
      expect(finalState.abortController).toBeNull();
    });
  });

  describe('timeout handling', () => {
    it('should timeout long requests', async () => {
      const shortTimeoutManager = new ChatStreamingManager({
        ...mockConfig,
        timeoutMs: 100 // Very short timeout
      });

      // Mock fetch to respond to timeout abort signal
      mockFetch.mockImplementation(async (url, options) => {
        return new Promise((resolve, reject) => {
          const signal = options?.signal as AbortSignal;
          if (signal) {
            signal.addEventListener('abort', () => {
              const abortError = new Error('The operation was aborted');
              abortError.name = 'AbortError';
              reject(abortError);
            });
          }
          
          // Simulate a long request that would exceed timeout
          setTimeout(() => {
            if (!signal?.aborted) {
              resolve({
                ok: true,
                body: {
                  getReader: jest.fn().mockReturnValue(mockReader)
                }
              });
            }
          }, 200); // Longer than the 100ms timeout
        });
      });

      const options: StreamMessageOptions = {
        model: 'test-model',
        stream: true
      };

      await expect(shortTimeoutManager.streamMessage('Hello', options, mockCallbacks))
        .resolves.toBeUndefined(); // Should resolve due to timeout abort, not throw

      expect(mockCallbacks.onAbort).toHaveBeenCalled();
    }, 1000);
  });
});