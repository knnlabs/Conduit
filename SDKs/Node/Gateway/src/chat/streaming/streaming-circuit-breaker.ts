/**
 * Streaming-specific circuit breaker manager
 *
 * Wraps the core CircuitBreaker for chat streaming with:
 * - Model change reset (per Issue #896)
 * - Error filtering (only retryable errors count)
 * - Callback integration for UI updates
 */

import {
  CircuitBreaker,
  isCircuitBreakerOpenError,
  type CircuitBreakerStats,
  type CircuitBreakerCallbacks,
  type CircuitState
} from '@knn_labs/conduit-common';
import type { StreamingCircuitBreakerConfig, CircuitBreakerEvent, StreamingError } from './types';

/**
 * Default configuration for streaming circuit breaker
 */
const DEFAULT_CONFIG: Required<StreamingCircuitBreakerConfig> = {
  enabled: true,
  failureThreshold: 3,
  failureWindowMs: 60000,  // 60 seconds
  resetTimeoutMs: 30000,   // 30 seconds
  enableLogging: false
};

/**
 * Streaming-specific circuit breaker manager
 *
 * Manages a single global circuit breaker for chat streaming with
 * model-change reset and error filtering.
 */
export class StreamingCircuitBreakerManager {
  private readonly config: Required<StreamingCircuitBreakerConfig>;
  private readonly circuitBreaker: CircuitBreaker;
  private currentModel: string | null = null;

  // Callbacks for external integration
  private onCircuitStateChange?: (event: CircuitBreakerEvent) => void;
  private onCircuitOpen?: (stats: CircuitBreakerStats) => void;

  constructor(
    config: StreamingCircuitBreakerConfig = {},
    callbacks?: {
      onCircuitStateChange?: (event: CircuitBreakerEvent) => void;
      onCircuitOpen?: (stats: CircuitBreakerStats) => void;
    }
  ) {
    this.config = { ...DEFAULT_CONFIG, ...config };
    this.onCircuitStateChange = callbacks?.onCircuitStateChange;
    this.onCircuitOpen = callbacks?.onCircuitOpen;

    // Create core circuit breaker with callbacks
    const coreCallbacks: CircuitBreakerCallbacks = {
      onOpen: (stats) => {
        this.onCircuitOpen?.(stats);
      },
      onStateChange: (oldState, newState, stats) => {
        this.onCircuitStateChange?.({
          previousState: oldState,
          newState,
          stats
        });
      }
    };

    this.circuitBreaker = new CircuitBreaker(
      {
        failureThreshold: this.config.failureThreshold,
        failureWindowMs: this.config.failureWindowMs,
        resetTimeoutMs: this.config.resetTimeoutMs,
        enableLogging: this.config.enableLogging,
        // Only count retryable errors as failures
        shouldCountAsFailure: (error) => this.isCircuitBreakerRelevantError(error)
      },
      coreCallbacks
    );
  }

  /**
   * Check if circuit breaker is enabled
   */
  isEnabled(): boolean {
    return this.config.enabled;
  }

  /**
   * Get current circuit state
   */
  getState(): CircuitState {
    return this.circuitBreaker.getState();
  }

  /**
   * Check if execution can proceed
   * @throws CircuitBreakerOpenError if circuit is open
   */
  checkOpen(): void {
    if (!this.config.enabled) return;
    this.circuitBreaker.checkOpen();
  }

  /**
   * Check if retry should be disabled due to circuit state
   */
  shouldDisableRetry(): boolean {
    if (!this.config.enabled) return false;
    return !this.circuitBreaker.canExecute();
  }

  /**
   * Record a successful streaming operation
   */
  recordSuccess(): void {
    if (!this.config.enabled) return;
    this.circuitBreaker.recordSuccess();
  }

  /**
   * Record a failed streaming operation
   */
  recordFailure(error: unknown): void {
    if (!this.config.enabled) return;
    this.circuitBreaker.recordFailure(error);
  }

  /**
   * Handle model change - reset circuit per Issue #896
   */
  handleModelChange(newModel: string): void {
    if (this.currentModel !== null && this.currentModel !== newModel) {
      // Reset the circuit when switching models
      this.circuitBreaker.reset();
    }
    this.currentModel = newModel;
  }

  /**
   * Get circuit breaker stats
   */
  getStats(): CircuitBreakerStats {
    return this.circuitBreaker.getStats();
  }

  /**
   * Reset the circuit breaker
   */
  reset(): void {
    this.circuitBreaker.reset();
    this.currentModel = null;
  }

  /**
   * Update callbacks dynamically
   */
  updateCallbacks(callbacks: {
    onCircuitStateChange?: (event: CircuitBreakerEvent) => void;
    onCircuitOpen?: (stats: CircuitBreakerStats) => void;
  }): void {
    this.onCircuitStateChange = callbacks.onCircuitStateChange;
    this.onCircuitOpen = callbacks.onCircuitOpen;
  }

  /**
   * Determine if an error should count toward circuit breaker
   * Only transient/retryable errors should count
   */
  private isCircuitBreakerRelevantError(error: unknown): boolean {
    // Don't count circuit breaker errors themselves
    if (isCircuitBreakerOpenError(error)) {
      return false;
    }

    // Check for streaming error with status
    if (error && typeof error === 'object') {
      const streamingError = error as StreamingError;

      // Retryable status codes (same as existing retry logic)
      if (streamingError.status !== undefined) {
        return [408, 429, 500, 502, 503, 504].includes(streamingError.status);
      }

      if (streamingError.statusCode !== undefined) {
        return [408, 429, 500, 502, 503, 504].includes(streamingError.statusCode);
      }

      // Explicitly marked as retryable
      if (streamingError.retryable === true) {
        return true;
      }

      // Network error codes
      if (streamingError.code === 'ECONNREFUSED' ||
          streamingError.code === 'ETIMEDOUT' ||
          streamingError.code === 'ECONNRESET') {
        return true;
      }
    }

    // Network/fetch errors
    if (error instanceof Error) {
      const message = error.message.toLowerCase();
      return message.includes('network') ||
             message.includes('timeout') ||
             message.includes('fetch') ||
             message.includes('connection');
    }

    return false;
  }
}

// Re-export for convenience
export { isCircuitBreakerOpenError };
