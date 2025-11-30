import { describe, it, expect } from '@jest/globals';
import {
  EnhancedSSEEventType,
  isChatCompletionChunk,
  isStreamingMetrics,
  isFinalMetrics,
  type StreamingMetrics,
  type FinalMetrics,
  type EnhancedStreamEvent
} from '../enhanced-streaming';

describe('enhanced-streaming types', () => {
  describe('EnhancedSSEEventType', () => {
    it('should have correct event type values', () => {
      expect(EnhancedSSEEventType.Content).toBe('content');
      expect(EnhancedSSEEventType.Metrics).toBe('metrics');
      expect(EnhancedSSEEventType.MetricsFinal).toBe('metrics-final');
      expect(EnhancedSSEEventType.Error).toBe('error');
      expect(EnhancedSSEEventType.Done).toBe('done');
    });
  });

  describe('isChatCompletionChunk', () => {
    it('should return true for valid chat completion chunks', () => {
      const chunk = {
        id: 'chatcmpl-123',
        object: 'chat.completion.chunk',
        created: 1234567890,
        model: 'gpt-4',
        choices: []
      };
      
      expect(isChatCompletionChunk(chunk)).toBe(true);
    });

    it('should return false for invalid objects', () => {
      expect(isChatCompletionChunk(null)).toBe(false);
      expect(isChatCompletionChunk(undefined)).toBe(false);
      expect(isChatCompletionChunk('string')).toBe(false);
      expect(isChatCompletionChunk(123)).toBe(false);
      expect(isChatCompletionChunk({})).toBe(false);
      expect(isChatCompletionChunk({ object: 'wrong.type' })).toBe(false);
    });
  });

  describe('isStreamingMetrics', () => {
    it('should return true for valid streaming metrics', () => {
      const metrics: StreamingMetrics = {
        current_tokens_per_second: 42.5,
        tokens_generated: 100,
        elapsed_ms: 2000
      };
      
      expect(isStreamingMetrics(metrics)).toBe(true);
    });

    it('should return true for partial streaming metrics', () => {
      expect(isStreamingMetrics({ current_tokens_per_second: 30 })).toBe(true);
      expect(isStreamingMetrics({ tokens_generated: 50 })).toBe(true);
      expect(isStreamingMetrics({ elapsed_ms: 1000 })).toBe(true);
    });

    it('should return false for invalid objects', () => {
      expect(isStreamingMetrics(null)).toBe(false);
      expect(isStreamingMetrics(undefined)).toBe(false);
      expect(isStreamingMetrics('string')).toBe(false);
      expect(isStreamingMetrics(123)).toBe(false);
      expect(isStreamingMetrics({})).toBe(false);
      expect(isStreamingMetrics({ unrelated_field: true })).toBe(false);
    });

    it('should handle snake_case field names', () => {
      const metrics = {
        request_id: 'req-123',
        elapsed_ms: 1500,
        tokens_generated: 25,
        current_tokens_per_second: 16.67,
        time_to_first_token_ms: 120,
        avg_inter_token_latency_ms: 60
      };
      
      expect(isStreamingMetrics(metrics)).toBe(true);
    });
  });

  describe('isFinalMetrics', () => {
    it('should return true for valid final metrics', () => {
      const metrics: FinalMetrics = {
        tokens_per_second: 35.5,
        total_latency_ms: 3000,
        completion_tokens: 150
      };
      
      expect(isFinalMetrics(metrics)).toBe(true);
    });

    it('should return true for partial final metrics', () => {
      expect(isFinalMetrics({ tokens_per_second: 40 })).toBe(true);
      expect(isFinalMetrics({ total_latency_ms: 2500 })).toBe(true);
      expect(isFinalMetrics({ completion_tokens: 100 })).toBe(true);
    });

    it('should return false for invalid objects', () => {
      expect(isFinalMetrics(null)).toBe(false);
      expect(isFinalMetrics(undefined)).toBe(false);
      expect(isFinalMetrics('string')).toBe(false);
      expect(isFinalMetrics(123)).toBe(false);
      expect(isFinalMetrics({})).toBe(false);
      expect(isFinalMetrics({ unrelated_field: true })).toBe(false);
    });

    it('should handle complete final metrics', () => {
      const metrics: FinalMetrics = {
        total_latency_ms: 2500,
        time_to_first_token_ms: 150,
        tokens_per_second: 42.0,
        prompt_tokens_per_second: 200,
        completion_tokens_per_second: 42.0,
        provider: 'openai',
        model: 'gpt-4',
        streaming: true,
        avg_inter_token_latency_ms: 59.5,
        prompt_tokens: 50,
        completion_tokens: 105,
        total_tokens: 155
      };
      
      expect(isFinalMetrics(metrics)).toBe(true);
    });
  });

  describe('EnhancedStreamEvent', () => {
    it('should properly type content events', () => {
      const event: EnhancedStreamEvent = {
        type: EnhancedSSEEventType.Content,
        data: {
          id: 'chatcmpl-123',
          object: 'chat.completion.chunk',
          created: 1234567890,
          model: 'gpt-4',
          choices: [{
            index: 0,
            delta: { content: 'Hello' },
            finish_reason: null
          }]
        }
      };
      
      expect(event.type).toBe('content');
      expect(isChatCompletionChunk(event.data)).toBe(true);
    });

    it('should properly type metrics events', () => {
      const event: EnhancedStreamEvent = {
        type: EnhancedSSEEventType.Metrics,
        data: {
          current_tokens_per_second: 45.2,
          tokens_generated: 30
        }
      };
      
      expect(event.type).toBe('metrics');
      expect(isStreamingMetrics(event.data)).toBe(true);
    });

    it('should properly type final metrics events', () => {
      const event: EnhancedStreamEvent = {
        type: EnhancedSSEEventType.MetricsFinal,
        data: {
          tokens_per_second: 38.5,
          total_latency_ms: 2600,
          completion_tokens: 100
        }
      };
      
      expect(event.type).toBe('metrics-final');
      expect(isFinalMetrics(event.data)).toBe(true);
    });

    it('should properly type done events', () => {
      const event: EnhancedStreamEvent = {
        type: EnhancedSSEEventType.Done,
        data: '[DONE]'
      };
      
      expect(event.type).toBe('done');
      expect(event.data).toBe('[DONE]');
    });

    it('should properly type error events', () => {
      const event: EnhancedStreamEvent = {
        type: EnhancedSSEEventType.Error,
        data: 'Stream error'
      };
      
      expect(event.type).toBe('error');
      expect(typeof event.data).toBe('string');
    });
  });
});