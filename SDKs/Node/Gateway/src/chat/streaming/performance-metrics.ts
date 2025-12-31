/**
 * Performance metrics utilities for chat streaming
 * Provides calculation and tracking helpers
 */

import type {
  StreamingPerformanceMetrics,
  UsageData,
  MetricsEventData,
  MessageMetadata
} from './types';

/**
 * Metrics calculator for streaming performance
 */
export class PerformanceMetricsCalculator {
  private startTime: number = 0;
  private firstTokenTime: number = 0;
  private hasReceivedFirstToken: boolean = false;
  private accumulatedMetrics: Partial<StreamingPerformanceMetrics & UsageData & MetricsEventData> = {};

  /**
   * Mark the start of a request
   */
  startRequest(): void {
    this.startTime = Date.now();
    this.firstTokenTime = 0;
    this.hasReceivedFirstToken = false;
    this.accumulatedMetrics = {};
  }

  /**
   * Mark when the first token is received
   */
  markFirstToken(): void {
    if (!this.hasReceivedFirstToken) {
      this.firstTokenTime = Date.now();
      this.hasReceivedFirstToken = true;
    }
  }

  /**
   * Update accumulated metrics
   */
  updateMetrics(
    metrics: Partial<StreamingPerformanceMetrics | UsageData | MetricsEventData>
  ): void {
    Object.assign(this.accumulatedMetrics, metrics);
  }

  /**
   * Calculate final metrics
   */
  calculateFinalMetrics(): MessageMetadata {
    const endTime = Date.now();
    const totalDuration = this.startTime ? (endTime - this.startTime) / 1000 : 0;

    // Get token counts with fallbacks
    const totalTokens = this.accumulatedMetrics.total_tokens 
      ?? this.accumulatedMetrics.completion_tokens 
      ?? this.accumulatedMetrics.tokens_generated 
      ?? 0;

    // Calculate tokens per second with preference for completion tokens
    const tokensPerSecond = (this.accumulatedMetrics as StreamingPerformanceMetrics).completion_tokens_per_second
      ?? (this.accumulatedMetrics as StreamingPerformanceMetrics).tokens_per_second
      ?? (totalTokens > 0 && totalDuration > 0 ? totalTokens / totalDuration : 0);

    // Get latency with fallbacks
    const latencyMs = (this.accumulatedMetrics as MetricsEventData).total_latency_ms
      ?? (this.accumulatedMetrics as StreamingPerformanceMetrics).total_latency_ms
      ?? totalDuration * 1000;

    // Calculate time to first token
    const timeToFirstToken = this.firstTokenTime > 0 && this.startTime > 0
      ? this.firstTokenTime - this.startTime 
      : (this.accumulatedMetrics as StreamingPerformanceMetrics).time_to_first_token_ms;

    return {
      tokensUsed: totalTokens,
      tokensPerSecond,
      latency: latencyMs,
      provider: this.accumulatedMetrics.provider,
      model: this.accumulatedMetrics.model,
      promptTokens: this.accumulatedMetrics.prompt_tokens,
      completionTokens: this.accumulatedMetrics.completion_tokens,
      timeToFirstToken,
      streaming: true,
    };
  }

  /**
   * Get current metrics snapshot
   */
  getCurrentMetrics(): Readonly<Partial<StreamingPerformanceMetrics & UsageData & MetricsEventData>> {
    return { ...this.accumulatedMetrics };
  }

  /**
   * Calculate tokens per second from current state
   */
  getCurrentTokensPerSecond(): number {
    const currentTime = Date.now();
    const elapsed = this.startTime ? (currentTime - this.startTime) / 1000 : 0;
    
    const tokens = this.accumulatedMetrics.completion_tokens 
      ?? this.accumulatedMetrics.tokens_generated 
      ?? 0;

    return elapsed > 0 && tokens > 0 ? tokens / elapsed : 0;
  }

  /**
   * Reset the calculator
   */
  reset(): void {
    this.startTime = 0;
    this.firstTokenTime = 0;
    this.hasReceivedFirstToken = false;
    this.accumulatedMetrics = {};
  }
}

/**
 * Utility functions for metrics processing
 */
export class MetricsUtils {
  /**
   * Extract tokens per second from various metric formats
   */
  static extractTokensPerSecond(
    metrics: StreamingPerformanceMetrics | MetricsEventData
  ): number | undefined {
    return ('completion_tokens_per_second' in metrics) 
      ? metrics.completion_tokens_per_second 
      : metrics.tokens_per_second;
  }

  /**
   * Extract total latency from various metric formats
   */
  static extractTotalLatency(
    metrics: StreamingPerformanceMetrics | MetricsEventData
  ): number | undefined {
    return metrics.total_latency_ms;
  }

  /**
   * Extract provider information
   */
  static extractProvider(
    metrics: StreamingPerformanceMetrics | MetricsEventData
  ): string | undefined {
    return metrics.provider;
  }

  /**
   * Extract model information
   */
  static extractModel(
    metrics: StreamingPerformanceMetrics | MetricsEventData
  ): string | undefined {
    return metrics.model;
  }

  /**
   * Merge multiple metric objects
   */
  static mergeMetrics<T extends Record<string, unknown>>(
    ...metrics: Partial<T>[]
  ): Partial<T> {
    return Object.assign({}, ...metrics);
  }

  /**
   * Check if metrics indicate completion
   */
  static isComplete(metrics: MetricsEventData): boolean {
    return Boolean(metrics.total_latency_ms);
  }

  /**
   * Format metrics for display
   */
  static formatMetrics(metadata: MessageMetadata): Record<string, string> {
    const formatted: Record<string, string> = {};

    if (metadata.tokensUsed) {
      formatted['Tokens Used'] = metadata.tokensUsed.toString();
    }

    if (metadata.tokensPerSecond) {
      formatted['Tokens/Second'] = metadata.tokensPerSecond.toFixed(1);
    }

    if (metadata.latency) {
      formatted['Latency'] = `${metadata.latency.toFixed(0)}ms`;
    }

    if (metadata.timeToFirstToken) {
      formatted['Time to First Token'] = `${metadata.timeToFirstToken.toFixed(0)}ms`;
    }

    if (metadata.provider) {
      formatted['Provider'] = metadata.provider;
    }

    if (metadata.model) {
      formatted['Model'] = metadata.model;
    }

    if (metadata.promptTokens) {
      formatted['Prompt Tokens'] = metadata.promptTokens.toString();
    }

    if (metadata.completionTokens) {
      formatted['Completion Tokens'] = metadata.completionTokens.toString();
    }

    return formatted;
  }

  /**
   * Calculate efficiency metrics
   */
  static calculateEfficiency(metadata: MessageMetadata): {
    tokensPerMs?: number;
    efficiencyScore?: number;
    latencyPerToken?: number;
  } {
    const result: {
      tokensPerMs?: number;
      efficiencyScore?: number;
      latencyPerToken?: number;
    } = {};

    if (metadata.tokensUsed && metadata.latency) {
      result.tokensPerMs = metadata.tokensUsed / metadata.latency;
      result.latencyPerToken = metadata.latency / metadata.tokensUsed;
    }

    if (metadata.tokensPerSecond && metadata.timeToFirstToken) {
      // Simple efficiency score: higher tokens/sec with lower time to first token is better
      result.efficiencyScore = metadata.tokensPerSecond / (metadata.timeToFirstToken / 1000);
    }

    return result;
  }

  /**
   * Compare two sets of metrics
   */
  static compareMetrics(
    baseline: MessageMetadata,
    current: MessageMetadata
  ): {
    tokensPerSecondDelta?: number;
    latencyDelta?: number;
    efficiencyDelta?: number;
  } {
    const result: {
      tokensPerSecondDelta?: number;
      latencyDelta?: number;
      efficiencyDelta?: number;
    } = {};

    if (baseline.tokensPerSecond && current.tokensPerSecond) {
      result.tokensPerSecondDelta = current.tokensPerSecond - baseline.tokensPerSecond;
    }

    if (baseline.latency && current.latency) {
      result.latencyDelta = current.latency - baseline.latency;
    }

    const baselineEfficiency = this.calculateEfficiency(baseline).efficiencyScore;
    const currentEfficiency = this.calculateEfficiency(current).efficiencyScore;

    if (baselineEfficiency && currentEfficiency) {
      result.efficiencyDelta = currentEfficiency - baselineEfficiency;
    }

    return result;
  }
}