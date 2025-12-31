/**
 * Circuit breaker implementation for preventing cascading failures
 *
 * Implements the circuit breaker pattern with three states:
 * - CLOSED: Normal operation, counting failures
 * - OPEN: Circuit tripped, rejecting requests
 * - HALF_OPEN: Testing recovery with limited requests
 */

import { CircuitState } from './types';
import type { CircuitBreakerConfig, CircuitBreakerStats, CircuitBreakerCallbacks } from './types';
import { CircuitBreakerOpenError } from './errors';

/**
 * Default configuration values matching Issue #896 requirements
 */
const DEFAULT_CONFIG: Required<Omit<CircuitBreakerConfig, 'shouldCountAsFailure'>> = {
  failureThreshold: 3,
  failureWindowMs: 60000,    // 60 seconds
  resetTimeoutMs: 30000,     // 30 seconds
  successThreshold: 1,
  enableLogging: false
};

interface FailureRecord {
  timestamp: number;
  error: unknown;
}

/**
 * Circuit breaker implementation for preventing cascading failures
 *
 * State machine:
 * - CLOSED: Normal operation, counting failures
 * - OPEN: Circuit tripped, rejecting requests
 * - HALF_OPEN: Testing recovery with limited requests
 */
export class CircuitBreaker {
  private readonly config: Required<Omit<CircuitBreakerConfig, 'shouldCountAsFailure'>> &
                          Pick<CircuitBreakerConfig, 'shouldCountAsFailure'>;
  private readonly callbacks: CircuitBreakerCallbacks;

  // State tracking
  private state: CircuitState = CircuitState.CLOSED;
  private failures: FailureRecord[] = [];
  private halfOpenSuccesses: number = 0;

  // Statistics
  private totalFailures: number = 0;
  private totalSuccesses: number = 0;
  private rejectedRequests: number = 0;
  private circuitOpenedAt: number | null = null;
  private lastFailureAt: number | null = null;
  private lastSuccessAt: number | null = null;

  constructor(
    config: CircuitBreakerConfig = {},
    callbacks: CircuitBreakerCallbacks = {}
  ) {
    this.config = {
      ...DEFAULT_CONFIG,
      ...config
    };
    this.callbacks = callbacks;
  }

  /**
   * Get current state of the circuit
   * Automatically transitions OPEN -> HALF_OPEN after timeout
   */
  getState(): CircuitState {
    // Check if OPEN circuit should transition to HALF_OPEN
    if (this.state === CircuitState.OPEN && this.circuitOpenedAt !== null) {
      const elapsed = Date.now() - this.circuitOpenedAt;
      if (elapsed >= this.config.resetTimeoutMs) {
        this.transitionTo(CircuitState.HALF_OPEN);
      }
    }
    return this.state;
  }

  /**
   * Get circuit breaker statistics
   */
  getStats(): CircuitBreakerStats {
    const currentState = this.getState();
    return {
      state: currentState,
      consecutiveFailures: this.getConsecutiveFailuresInWindow(),
      totalFailures: this.totalFailures,
      totalSuccesses: this.totalSuccesses,
      circuitOpenedAt: this.circuitOpenedAt,
      timeUntilHalfOpen: this.calculateTimeUntilHalfOpen(),
      lastFailureAt: this.lastFailureAt,
      lastSuccessAt: this.lastSuccessAt,
      rejectedRequests: this.rejectedRequests
    };
  }

  /**
   * Check if a request can proceed
   * Returns true if circuit is CLOSED or HALF_OPEN
   */
  canExecute(): boolean {
    const state = this.getState();
    return state !== CircuitState.OPEN;
  }

  /**
   * Check if request should proceed, throwing if circuit is open
   * @throws CircuitBreakerOpenError if circuit is OPEN
   */
  checkOpen(): void {
    const state = this.getState();
    if (state === CircuitState.OPEN) {
      this.rejectedRequests++;
      const stats = this.getStats();
      this.callbacks.onRejected?.(stats);

      throw new CircuitBreakerOpenError(
        `Circuit breaker is open. Try again in ${Math.ceil((stats.timeUntilHalfOpen ?? 0) / 1000)} seconds.`,
        stats,
        stats.timeUntilHalfOpen
      );
    }
  }

  /**
   * Record a successful request
   */
  recordSuccess(): void {
    this.totalSuccesses++;
    this.lastSuccessAt = Date.now();

    const currentState = this.getState();

    if (currentState === CircuitState.HALF_OPEN) {
      this.halfOpenSuccesses++;
      this.log('debug', `Half-open success ${this.halfOpenSuccesses}/${this.config.successThreshold}`);

      if (this.halfOpenSuccesses >= this.config.successThreshold) {
        this.transitionTo(CircuitState.CLOSED);
      }
    } else if (currentState === CircuitState.CLOSED) {
      // Clear failure history on success in CLOSED state
      this.failures = [];
    }
  }

  /**
   * Record a failed request
   */
  recordFailure(error: unknown): void {
    // Check if this error should count as a failure
    if (this.config.shouldCountAsFailure && !this.config.shouldCountAsFailure(error)) {
      this.log('debug', 'Error not counted as failure by custom filter');
      return;
    }

    const now = Date.now();
    this.totalFailures++;
    this.lastFailureAt = now;

    const currentState = this.getState();

    if (currentState === CircuitState.HALF_OPEN) {
      // Any failure in HALF_OPEN immediately reopens the circuit
      this.log('warn', 'Failure in half-open state, reopening circuit');
      this.transitionTo(CircuitState.OPEN, error);
      return;
    }

    if (currentState === CircuitState.CLOSED) {
      // Add to failure history
      this.failures.push({ timestamp: now, error });

      // Clean up old failures outside the window
      this.pruneOldFailures();

      // Check if we should trip the circuit
      const consecutiveFailures = this.getConsecutiveFailuresInWindow();
      this.log('debug', `Consecutive failures: ${consecutiveFailures}/${this.config.failureThreshold}`);

      if (consecutiveFailures >= this.config.failureThreshold) {
        this.transitionTo(CircuitState.OPEN, error);
      }
    }
  }

  /**
   * Manually reset the circuit to CLOSED state
   * Use with caution - typically for testing or admin override
   */
  reset(): void {
    this.log('info', 'Circuit manually reset');
    this.transitionTo(CircuitState.CLOSED);
    this.failures = [];
    this.totalFailures = 0;
    this.totalSuccesses = 0;
    this.rejectedRequests = 0;
  }

  // Private methods

  private transitionTo(newState: CircuitState, triggerError?: unknown): void {
    const oldState = this.state;
    if (oldState === newState) return;

    this.state = newState;
    const stats = this.getStats();

    this.log('info', `Circuit state change: ${oldState} -> ${newState}`);

    switch (newState) {
      case CircuitState.OPEN:
        this.circuitOpenedAt = Date.now();
        this.halfOpenSuccesses = 0;
        this.callbacks.onOpen?.(stats, triggerError);
        break;

      case CircuitState.HALF_OPEN:
        this.halfOpenSuccesses = 0;
        this.callbacks.onHalfOpen?.(stats);
        break;

      case CircuitState.CLOSED:
        this.circuitOpenedAt = null;
        this.failures = [];
        this.halfOpenSuccesses = 0;
        this.callbacks.onClose?.(stats);
        break;
    }

    this.callbacks.onStateChange?.(oldState, newState, stats);
  }

  private pruneOldFailures(): void {
    const cutoff = Date.now() - this.config.failureWindowMs;
    this.failures = this.failures.filter(f => f.timestamp >= cutoff);
  }

  private getConsecutiveFailuresInWindow(): number {
    this.pruneOldFailures();
    return this.failures.length;
  }

  private calculateTimeUntilHalfOpen(): number | null {
    if (this.state !== CircuitState.OPEN || this.circuitOpenedAt === null) {
      return null;
    }

    const elapsed = Date.now() - this.circuitOpenedAt;
    const remaining = this.config.resetTimeoutMs - elapsed;
    return remaining > 0 ? remaining : 0;
  }

  private log(_level: 'debug' | 'info' | 'warn' | 'error', message: string): void {
    if (this.config.enableLogging) {
      console.warn(`[CircuitBreaker] ${message}`);
    }
  }
}
