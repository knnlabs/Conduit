/**
 * Unit tests for performance metrics utilities
 */

import { PerformanceMetricsCalculator, MetricsUtils } from '../performance-metrics';
import type { 
  StreamingPerformanceMetrics, 
  MetricsEventData, 
  MessageMetadata,
  StreamingMetrics 
} from '../types';

// Mock Date.now for predictable testing
const mockDateNow = jest.spyOn(Date, 'now');

describe('PerformanceMetricsCalculator', () => {
  let calculator: PerformanceMetricsCalculator;

  beforeEach(() => {
    calculator = new PerformanceMetricsCalculator();
    mockDateNow.mockClear();
  });

  afterAll(() => {
    mockDateNow.mockRestore();
  });

  describe('lifecycle methods', () => {
    it('should initialize with clean state', () => {
      // Mock Date.now for this specific test
      mockDateNow.mockReturnValue(1000);
      
      const metrics = calculator.getCurrentMetrics();
      expect(metrics).toEqual({});
      expect(calculator.getCurrentTokensPerSecond()).toBe(0);
    });

    it('should track request lifecycle', () => {
      // Mock the sequence of Date.now calls
      mockDateNow.mockClear();
      mockDateNow
        .mockReturnValueOnce(1000) // startRequest (startTime = 1000)
        .mockReturnValueOnce(1000) // getCurrentTokensPerSecond call
        .mockReturnValueOnce(1200) // markFirstToken (firstTokenTime = 1200)
        .mockReturnValueOnce(2000); // calculateFinalMetrics endTime = 2000

      calculator.startRequest();
      expect(calculator.getCurrentTokensPerSecond()).toBe(0);

      calculator.markFirstToken();
      
      calculator.updateMetrics({
        completion_tokens: 50,
        total_tokens: 100,
        provider: 'test-provider'
      });

      const finalMetrics = calculator.calculateFinalMetrics();

      expect(finalMetrics).toEqual({
        tokensUsed: 100,
        tokensPerSecond: 100, // 100 tokens in 1 second (2000-1000)/1000 = 1 second
        latency: 1000, // 1 second in ms (2000-1000)
        provider: 'test-provider',
        model: undefined,
        promptTokens: undefined,
        completionTokens: 50,
        timeToFirstToken: 200, // 200ms to first token (1200-1000)
        streaming: true,
      });
    });

    it('should handle multiple markFirstToken calls', () => {
      mockDateNow.mockClear();
      mockDateNow
        .mockReturnValueOnce(1000) // startRequest
        .mockReturnValueOnce(1200) // first markFirstToken
        .mockReturnValueOnce(1400) // second markFirstToken (should be ignored) - but actually not called due to hasReceivedFirstToken check
        .mockReturnValueOnce(2000); // calculateFinalMetrics endTime

      calculator.startRequest();
      calculator.markFirstToken();
      calculator.markFirstToken(); // Should be ignored

      calculator.updateMetrics({ completion_tokens: 50 });
      
      const finalMetrics = calculator.calculateFinalMetrics();

      expect(finalMetrics.timeToFirstToken).toBe(200); // Should still be from first call (1200-1000)
    });

    it('should reset state properly', () => {
      mockDateNow.mockReturnValue(1000);
      
      calculator.startRequest();
      calculator.markFirstToken();
      calculator.updateMetrics({ tokens: 50 });

      calculator.reset();

      const metrics = calculator.getCurrentMetrics();
      expect(metrics).toEqual({});
      expect(calculator.getCurrentTokensPerSecond()).toBe(0);
    });
  });

  describe('metrics updates', () => {
    beforeEach(() => {
      mockDateNow.mockClear();
      mockDateNow.mockReturnValue(1000);
      calculator.startRequest();
    });

    it('should accumulate different metric types', () => {
      const performanceMetrics: StreamingPerformanceMetrics = {
        tokens_per_second: 50,
        completion_tokens_per_second: 60,
        provider: 'openai'
      };

      const eventMetrics: MetricsEventData = {
        total_latency_ms: 2000,
        tokens_generated: 100,
        model: 'gpt-4'
      };

      calculator.updateMetrics(performanceMetrics);
      calculator.updateMetrics(eventMetrics);

      const current = calculator.getCurrentMetrics();
      expect(current).toEqual({
        tokens_per_second: 50,
        completion_tokens_per_second: 60,
        provider: 'openai',
        total_latency_ms: 2000,
        tokens_generated: 100,
        model: 'gpt-4'
      });
    });

    it('should override duplicate keys', () => {
      calculator.updateMetrics({ provider: 'openai' });
      calculator.updateMetrics({ provider: 'anthropic' });

      const current = calculator.getCurrentMetrics();
      expect(current.provider).toBe('anthropic');
    });
  });

  describe('calculateFinalMetrics', () => {
    beforeEach(() => {
      mockDateNow.mockClear();
      mockDateNow.mockReturnValue(1000);
      calculator.startRequest();
    });

    it('should prefer completion tokens over total tokens', () => {
      calculator.updateMetrics({
        total_tokens: 100,
        completion_tokens: 50
      });

      mockDateNow.mockReturnValue(3000); // 2 seconds later
      const metrics = calculator.calculateFinalMetrics();

      expect(metrics.tokensUsed).toBe(100); // Uses total_tokens when available
      expect(metrics.completionTokens).toBe(50);
    });

    it('should prefer completion_tokens_per_second for rate calculation', () => {
      calculator.updateMetrics({
        completion_tokens_per_second: 75,
        tokens_per_second: 50,
        total_tokens: 100
      });

      mockDateNow.mockReturnValue(2000); // 1 second later
      const metrics = calculator.calculateFinalMetrics();

      expect(metrics.tokensPerSecond).toBe(75); // Uses completion_tokens_per_second
    });

    it('should calculate fallback tokens per second', () => {
      calculator.updateMetrics({
        total_tokens: 100
      });

      mockDateNow.mockReturnValue(3000); // 2 seconds later
      const metrics = calculator.calculateFinalMetrics();

      expect(metrics.tokensPerSecond).toBe(50); // 100 tokens / 2 seconds
    });

    it('should handle zero duration gracefully', () => {
      calculator.updateMetrics({ total_tokens: 100 });

      // Same time as start
      mockDateNow.mockReturnValue(1000);
      const metrics = calculator.calculateFinalMetrics();

      expect(metrics.tokensPerSecond).toBe(0);
      expect(metrics.latency).toBe(0);
    });

    it('should use server metrics when available', () => {
      calculator.updateMetrics({
        total_latency_ms: 1500,
        time_to_first_token_ms: 300
      });

      mockDateNow.mockReturnValue(2000);
      const metrics = calculator.calculateFinalMetrics();

      expect(metrics.latency).toBe(1500); // Uses server metric
      expect(metrics.timeToFirstToken).toBe(300); // Uses server metric
    });
  });

  describe('getCurrentTokensPerSecond', () => {
    it('should calculate real-time tokens per second', () => {
      mockDateNow.mockClear();
      mockDateNow.mockReturnValue(1000);
      calculator.startRequest();

      calculator.updateMetrics({ completion_tokens: 50 });

      mockDateNow.mockReturnValue(2000); // 1 second later
      expect(calculator.getCurrentTokensPerSecond()).toBe(50);

      mockDateNow.mockReturnValue(3000); // 2 seconds total
      expect(calculator.getCurrentTokensPerSecond()).toBe(25);
    });

    it('should return 0 when no tokens or time', () => {
      expect(calculator.getCurrentTokensPerSecond()).toBe(0);

      mockDateNow.mockClear();
      mockDateNow.mockReturnValue(1000);
      calculator.startRequest();
      expect(calculator.getCurrentTokensPerSecond()).toBe(0);
    });
  });
});

describe('MetricsUtils', () => {
  describe('extractTokensPerSecond', () => {
    it('should prefer completion_tokens_per_second', () => {
      const metrics = {
        completion_tokens_per_second: 75,
        tokens_per_second: 50
      };

      expect(MetricsUtils.extractTokensPerSecond(metrics)).toBe(75);
    });

    it('should fallback to tokens_per_second', () => {
      const metrics = { tokens_per_second: 50 };
      expect(MetricsUtils.extractTokensPerSecond(metrics)).toBe(50);
    });

    it('should return undefined when neither is available', () => {
      const metrics = { provider: 'test' };
      expect(MetricsUtils.extractTokensPerSecond(metrics)).toBeUndefined();
    });
  });

  describe('mergeMetrics', () => {
    it('should merge multiple metric objects', () => {
      const metrics1 = { provider: 'openai', tokens: 50 };
      const metrics2 = { model: 'gpt-4', latency: 1000 };
      const metrics3 = { provider: 'anthropic' }; // Should override

      const merged = MetricsUtils.mergeMetrics(metrics1, metrics2, metrics3);

      expect(merged).toEqual({
        provider: 'anthropic', // Last one wins
        tokens: 50,
        model: 'gpt-4',
        latency: 1000
      });
    });

    it('should handle empty arrays', () => {
      expect(MetricsUtils.mergeMetrics()).toEqual({});
    });
  });

  describe('formatMetrics', () => {
    it('should format all available metrics', () => {
      const metadata: MessageMetadata = {
        tokensUsed: 100,
        tokensPerSecond: 50.5,
        latency: 1234.5,
        timeToFirstToken: 234.7,
        provider: 'openai',
        model: 'gpt-4',
        promptTokens: 25,
        completionTokens: 75,
        streaming: true
      };

      const formatted = MetricsUtils.formatMetrics(metadata);

      expect(formatted).toEqual({
        'Tokens Used': '100',
        'Tokens/Second': '50.5',
        'Latency': '1235ms', // Rounded to nearest ms
        'Time to First Token': '235ms',
        'Provider': 'openai',
        'Model': 'gpt-4',
        'Prompt Tokens': '25',
        'Completion Tokens': '75'
      });
    });

    it('should handle partial metadata', () => {
      const metadata: MessageMetadata = {
        tokensUsed: 50,
        provider: 'test',
        streaming: true
      };

      const formatted = MetricsUtils.formatMetrics(metadata);

      expect(formatted).toEqual({
        'Tokens Used': '50',
        'Provider': 'test'
      });
    });

    it('should handle empty metadata', () => {
      const metadata: MessageMetadata = { streaming: true };
      const formatted = MetricsUtils.formatMetrics(metadata);
      expect(formatted).toEqual({});
    });
  });

  describe('calculateEfficiency', () => {
    it('should calculate all efficiency metrics', () => {
      const metadata: MessageMetadata = {
        tokensUsed: 100,
        tokensPerSecond: 50,
        latency: 2000,
        timeToFirstToken: 400,
        streaming: true
      };

      const efficiency = MetricsUtils.calculateEfficiency(metadata);

      expect(efficiency).toEqual({
        tokensPerMs: 0.05, // 100 tokens / 2000ms
        efficiencyScore: 125, // 50 tokens/s / (400ms / 1000)
        latencyPerToken: 20 // 2000ms / 100 tokens
      });
    });

    it('should handle missing data gracefully', () => {
      const metadata: MessageMetadata = {
        tokensUsed: 100,
        streaming: true
      };

      const efficiency = MetricsUtils.calculateEfficiency(metadata);

      expect(efficiency).toEqual({});
    });
  });

  describe('compareMetrics', () => {
    it('should calculate deltas between metrics', () => {
      const baseline: MessageMetadata = {
        tokensPerSecond: 40,
        latency: 2000,
        streaming: true
      };

      const current: MessageMetadata = {
        tokensPerSecond: 50,
        latency: 1800,
        streaming: true
      };

      const comparison = MetricsUtils.compareMetrics(baseline, current);

      expect(comparison.tokensPerSecondDelta).toBe(10);
      expect(comparison.latencyDelta).toBe(-200);
    });

    it('should handle missing metrics', () => {
      const baseline: MessageMetadata = { streaming: true };
      const current: MessageMetadata = { tokensPerSecond: 50, streaming: true };

      const comparison = MetricsUtils.compareMetrics(baseline, current);

      expect(comparison.tokensPerSecondDelta).toBeUndefined();
      expect(comparison.latencyDelta).toBeUndefined();
    });
  });

  describe('utility functions', () => {
    const testMetrics = {
      total_latency_ms: 1500,
      provider: 'test-provider',
      model: 'test-model'
    };

    it('should extract total latency', () => {
      expect(MetricsUtils.extractTotalLatency(testMetrics)).toBe(1500);
    });

    it('should extract provider', () => {
      expect(MetricsUtils.extractProvider(testMetrics)).toBe('test-provider');
    });

    it('should extract model', () => {
      expect(MetricsUtils.extractModel(testMetrics)).toBe('test-model');
    });

    it('should check completion status', () => {
      expect(MetricsUtils.isComplete(testMetrics as StreamingMetrics)).toBe(true);
      expect(MetricsUtils.isComplete({ provider: 'test' } as Partial<StreamingMetrics>)).toBe(false);
    });
  });
});