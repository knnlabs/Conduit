/**
 * Circuit breaker module exports
 *
 * Provides circuit breaker pattern implementation for preventing cascading failures.
 */

// Types and interfaces
export {
  CircuitState,
  type CircuitBreakerConfig,
  type CircuitBreakerStats,
  type CircuitBreakerCallbacks
} from './types';

// Errors
export {
  CircuitBreakerOpenError,
  isCircuitBreakerOpenError
} from './errors';

// Main class
export { CircuitBreaker } from './CircuitBreaker';
