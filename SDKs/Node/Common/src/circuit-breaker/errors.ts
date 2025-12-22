/**
 * Circuit breaker error types
 */

import { ConduitError } from '../errors';
import type { CircuitState, CircuitBreakerStats } from './types';

/**
 * Error thrown when circuit breaker is open and request is rejected
 */
export class CircuitBreakerOpenError extends ConduitError {
  /** Current circuit breaker state */
  public readonly circuitState: CircuitState;

  /** Time until circuit transitions to HALF_OPEN (milliseconds) */
  public readonly timeUntilHalfOpen: number | null;

  /** Circuit breaker statistics at time of rejection */
  public readonly stats: CircuitBreakerStats;

  constructor(
    message: string,
    stats: CircuitBreakerStats,
    timeUntilHalfOpen: number | null
  ) {
    super(message, 503, 'CIRCUIT_BREAKER_OPEN', {
      circuitState: stats.state,
      timeUntilHalfOpen,
      consecutiveFailures: stats.consecutiveFailures,
      totalFailures: stats.totalFailures
    });

    this.circuitState = stats.state;
    this.timeUntilHalfOpen = timeUntilHalfOpen;
    this.stats = stats;
  }
}

/**
 * Type guard for CircuitBreakerOpenError
 */
export function isCircuitBreakerOpenError(error: unknown): error is CircuitBreakerOpenError {
  return error instanceof CircuitBreakerOpenError;
}
