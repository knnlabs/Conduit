/**
 * Unit tests for streaming types
 * These tests verify type definitions and interfaces work as expected
 */

import type {
  StreamingConfig,
  SendMessageOptions,
  StreamMessageOptions,
  StreamingCallbacks,
  ChatCompletionRequest,
  ChatCompletionResponse,
  ChatCompletionChunk,
  StreamingError,
  MessageMetadata,
  StreamingPerformanceMetrics,
  UsageData,
  MetricsEventData
} from '../types';

describe('Streaming Types', () => {
  describe('StreamingConfig', () => {
    it('should accept minimal configuration', () => {
      const config: StreamingConfig = {
        apiEndpoint: '/api/chat'
      };

      expect(config.apiEndpoint).toBe('/api/chat');
    });

    it('should accept full configuration', () => {
      const config: StreamingConfig = {
        apiEndpoint: '/api/chat',
        timeoutMs: 30000,
        trackPerformanceMetrics: true,
        showTokensPerSecond: false,
        useServerMetrics: true,
        enableLogging: true
      };

      expect(config.timeoutMs).toBe(30000);
      expect(config.trackPerformanceMetrics).toBe(true);
    });
  });

  describe('SendMessageOptions', () => {
    it('should accept minimal options', () => {
      const options: SendMessageOptions = {
        model: 'gpt-4'
      };

      expect(options.model).toBe('gpt-4');
    });

    it('should accept full options', () => {
      const options: SendMessageOptions = {
        model: 'gpt-4',
        temperature: 0.7,
        maxTokens: 1000,
        topP: 0.9,
        frequencyPenalty: 0.1,
        presencePenalty: 0.2,
        systemPrompt: 'You are helpful',
        seed: 42,
        stop: ['STOP'],
        responseFormat: 'json_object',
        stream: false,
        dynamicParameters: { custom: 'value' }
      };

      expect(options.temperature).toBe(0.7);
      expect(options.responseFormat).toBe('json_object');
      expect(options.dynamicParameters?.custom).toBe('value');
    });
  });

  describe('StreamMessageOptions', () => {
    it('should enforce stream: true', () => {
      const options: StreamMessageOptions = {
        model: 'gpt-4',
        stream: true, // Must be true
        messages: [
          { role: 'user', content: 'Hello' }
        ]
      };

      expect(options.stream).toBe(true);
    });

    it('should accept images and conversation history', () => {
      const options: StreamMessageOptions = {
        model: 'gpt-4',
        stream: true,
        images: [
          {
            url: 'https://example.com/image.jpg',
            mimeType: 'image/jpeg',
            size: 1024,
            name: 'test.jpg'
          }
        ],
        messages: [
          { role: 'user', content: 'Previous message' },
          { role: 'assistant', content: 'Previous response' }
        ]
      };

      expect(options.images).toHaveLength(1);
      expect(options.messages).toHaveLength(2);
    });
  });

  describe('StreamingCallbacks', () => {
    it('should accept all optional callbacks', () => {
      const callbacks: StreamingCallbacks = {
        onStart: () => {},
        onContent: (content, total) => {
          expect(typeof content).toBe('string');
          expect(typeof total).toBe('string');
        },
        onChunk: (chunk) => {
          expect(chunk.object).toBe('chat.completion.chunk');
        },
        onMetrics: (metrics) => {
          expect(metrics).toBeDefined();
        },
        onTokensPerSecond: (tps) => {
          expect(typeof tps).toBe('number');
        },
        onComplete: (response) => {
          expect(response.content).toBeDefined();
        },
        onError: (error) => {
          expect(error).toBeInstanceOf(Error);
        },
        onAbort: () => {}
      };

      // Test callback type safety
      callbacks.onContent?.('test', 'test content');
      callbacks.onTokensPerSecond?.(50);
    });

    it('should work with partial callbacks', () => {
      const callbacks: StreamingCallbacks = {
        onContent: (content) => content.toUpperCase(),
        onError: (error) => console.error(error.message)
      };

      expect(callbacks.onStart).toBeUndefined();
      expect(callbacks.onContent).toBeDefined();
    });
  });

  describe('ChatCompletionRequest', () => {
    it('should match OpenAI API format', () => {
      const request: ChatCompletionRequest = {
        model: 'gpt-4',
        messages: [
          { role: 'system', content: 'You are helpful' },
          { role: 'user', content: 'Hello' }
        ],
        stream: true,
        temperature: 0.7,
        max_tokens: 1000
      };

      expect(request.messages).toHaveLength(2);
      expect(request.messages[0].role).toBe('system');
    });

    it('should support complex message content', () => {
      const request: ChatCompletionRequest = {
        model: 'gpt-4-vision',
        messages: [
          {
            role: 'user',
            content: [
              { type: 'text', text: 'What is in this image?' },
              {
                type: 'image_url',
                image_url: {
                  url: 'data:image/jpeg;base64,abc123',
                  detail: 'high'
                }
              }
            ]
          }
        ]
      };

      const userMessage = request.messages[0];
      expect(Array.isArray(userMessage.content)).toBe(true);
      
      if (Array.isArray(userMessage.content)) {
        expect(userMessage.content[0].type).toBe('text');
        expect(userMessage.content[1].type).toBe('image_url');
      }
    });

    it('should support dynamic parameters', () => {
      const request: ChatCompletionRequest = {
        model: 'custom-model',
        messages: [{ role: 'user', content: 'Hello' }],
        custom_parameter: 'value',
        another_param: 42
      };

      expect(request.custom_parameter).toBe('value');
      expect(request.another_param).toBe(42);
    });
  });

  describe('ChatCompletionResponse', () => {
    it('should match OpenAI response format', () => {
      const response: ChatCompletionResponse = {
        id: 'chatcmpl-123',
        object: 'chat.completion',
        created: 1677652288,
        model: 'gpt-4',
        choices: [{
          index: 0,
          message: {
            role: 'assistant',
            content: 'Hello! How can I help you?'
          },
          finish_reason: 'stop'
        }],
        usage: {
          prompt_tokens: 10,
          completion_tokens: 20,
          total_tokens: 30
        }
      };

      expect(response.choices).toHaveLength(1);
      expect(response.usage?.total_tokens).toBe(30);
    });
  });

  describe('ChatCompletionChunk', () => {
    it('should match OpenAI streaming chunk format', () => {
      const chunk: ChatCompletionChunk = {
        id: 'chatcmpl-123',
        object: 'chat.completion.chunk',
        created: 1677652288,
        model: 'gpt-4',
        choices: [{
          index: 0,
          delta: {
            content: 'Hello'
          },
          finish_reason: undefined
        }]
      };

      expect(chunk.object).toBe('chat.completion.chunk');
      expect(chunk.choices[0].delta.content).toBe('Hello');
    });
  });

  describe('StreamingError', () => {
    it('should extend Error with additional properties', () => {
      const error: StreamingError = new Error('Test error') as StreamingError;
      error.status = 400;
      error.code = 'invalid_request';
      error.context = 'streaming';
      error.retryable = false;

      expect(error.message).toBe('Test error');
      expect(error.status).toBe(400);
      expect(error.retryable).toBe(false);
    });
  });

  describe('MessageMetadata', () => {
    it('should track comprehensive performance data', () => {
      const metadata: MessageMetadata = {
        tokensUsed: 100,
        tokensPerSecond: 50,
        latency: 2000,
        finishReason: 'stop',
        provider: 'openai',
        model: 'gpt-4',
        promptTokens: 30,
        completionTokens: 70,
        timeToFirstToken: 300,
        streaming: true
      };

      expect(metadata.tokensUsed).toBe(100);
      expect(metadata.streaming).toBe(true);
    });

    it('should work with minimal data', () => {
      const metadata: MessageMetadata = {
        streaming: false
      };

      expect(metadata.streaming).toBe(false);
      expect(metadata.tokensUsed).toBeUndefined();
    });
  });

  describe('Performance Metrics Types', () => {
    it('should define StreamingPerformanceMetrics correctly', () => {
      const metrics: StreamingPerformanceMetrics = {
        tokens_per_second: 50,
        completion_tokens_per_second: 45,
        tokens_generated: 100,
        time_to_first_token_ms: 200,
        total_latency_ms: 2000,
        provider: 'openai',
        model: 'gpt-4'
      };

      expect(metrics.tokens_per_second).toBe(50);
      expect(metrics.completion_tokens_per_second).toBe(45);
    });

    it('should define UsageData correctly', () => {
      const usage: UsageData = {
        prompt_tokens: 25,
        completion_tokens: 75,
        total_tokens: 100
      };

      expect(usage.total_tokens).toBe(100);
    });

    it('should define MetricsEventData correctly', () => {
      const eventData: MetricsEventData = {
        request_id: 'req-123',
        elapsed_ms: 1500,
        tokens_generated: 80,
        current_tokens_per_second: 48,
        completion_tokens_per_second: 52,
        total_latency_ms: 1800,
        tokens_per_second: 50,
        provider: 'anthropic',
        model: 'claude-3'
      };

      expect(eventData.request_id).toBe('req-123');
      expect(eventData.completion_tokens_per_second).toBe(52);
    });
  });

  describe('Type compatibility', () => {
    it('should allow assignment between compatible types', () => {
      // Test that metrics types are compatible
      const performanceMetrics: StreamingPerformanceMetrics = {
        tokens_per_second: 50,
        provider: 'test'
      };

      const eventMetrics: MetricsEventData = {
        tokens_per_second: 50,
        provider: 'test'
      };

      // Should be able to merge these types
      const combined = { ...performanceMetrics, ...eventMetrics };
      expect(combined.tokens_per_second).toBe(50);
      expect(combined.provider).toBe('test');
    });

    it('should work with union types in callbacks', () => {
      const callback: StreamingCallbacks['onMetrics'] = (metrics) => {
        // Should work with either type
        if ('completion_tokens_per_second' in metrics) {
          expect(typeof metrics.completion_tokens_per_second).toBe('number');
        }
        if ('tokens_per_second' in metrics) {
          expect(typeof metrics.tokens_per_second).toBe('number');
        }
      };

      const performanceMetrics: StreamingPerformanceMetrics = {
        completion_tokens_per_second: 50
      };

      const eventMetrics: MetricsEventData = {
        tokens_per_second: 45
      };

      callback?.(performanceMetrics);
      callback?.(eventMetrics);
    });
  });
});