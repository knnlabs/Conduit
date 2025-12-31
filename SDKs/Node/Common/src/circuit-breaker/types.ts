/**
 * Circuit breaker types and interfaces
 *
 * Provides types for implementing the circuit breaker pattern to prevent
 * cascading failures and protect against sustained service degradation.
 */

/**
 * Circuit breaker states following the standard pattern
 */
export enum CircuitState {
  /** Normal operation - requests pass through, failures tracked */
  CLOSED = 'closed',
  /** Circuit tripped - requests are blocked/rejected immediately */
  OPEN = 'open',
  /** Testing recovery - limited requests allowed to test if service recovered */
  HALF_OPEN = 'half_open'
}

/**
 * Configuration options for the circuit breaker
 */
export interface CircuitBreakerConfig {
  /** Number of consecutive failures to trip the circuit (default: 3) */
  failureThreshold?: number;

  /** Time window in milliseconds for counting failures (default: 60000) */
  failureWindowMs?: number;

  /** Time in milliseconds to wait before transitioning from OPEN to HALF_OPEN (default: 30000) */
  resetTimeoutMs?: number;

  /** Number of successful requests in HALF_OPEN to close circuit (default: 1) */
  successThreshold?: number;

  /** Enable debug logging (default: false) */
  enableLogging?: boolean;

  /** Custom function to determine if an error should count as a failure */
  shouldCountAsFailure?: (error: unknown) => boolean;
}

/**
 * Statistics about the circuit breaker state
 */
export interface CircuitBreakerStats {
  /** Current state of the circuit */
  state: CircuitState;

  /** Number of consecutive failures in current window */
  consecutiveFailures: number;

  /** Total failures since last reset */
  totalFailures: number;

  /** Total successes since last reset */
  totalSuccesses: number;

  /** Timestamp when circuit was opened (null if closed) */
  circuitOpenedAt: number | null;

  /** Time remaining until HALF_OPEN transition in ms (null if not OPEN) */
  timeUntilHalfOpen: number | null;

  /** Timestamp of last failure */
  lastFailureAt: number | null;

  /** Timestamp of last success */
  lastSuccessAt: number | null;

  /** Number of requests rejected while OPEN */
  rejectedRequests: number;
}

/**
 * Callbacks for circuit breaker state changes
 */
export interface CircuitBreakerCallbacks {
  /** Called when circuit transitions to OPEN state */
  onOpen?: (stats: CircuitBreakerStats, error: unknown) => void;

  /** Called when circuit transitions to HALF_OPEN state */
  onHalfOpen?: (stats: CircuitBreakerStats) => void;

  /** Called when circuit transitions to CLOSED state */
  onClose?: (stats: CircuitBreakerStats) => void;

  /** Called when a request is rejected due to OPEN circuit */
  onRejected?: (stats: CircuitBreakerStats) => void;

  /** Called on any state change */
  onStateChange?: (oldState: CircuitState, newState: CircuitState, stats: CircuitBreakerStats) => void;
}
