/**
 * Retry strategy types and utilities for local HTTP clients
 * Supports both fixed delay and exponential backoff patterns.
 */

/**
 * Type of retry strategy to use
 */
export enum RetryStrategyType {
  /** Fixed delay between retries (Admin client pattern) */
  FIXED_DELAY = "fixed_delay",
  /** Exponential backoff with optional jitter. */
  EXPONENTIAL_BACKOFF = "exponential_backoff",
  /** Custom array of delays */
  CUSTOM_DELAYS = "custom_delays",
}

/**
 * Fixed delay retry configuration
 * Used by the Admin client for simple retry patterns
 */
export interface FixedDelayConfig {
  type: RetryStrategyType.FIXED_DELAY;
  /** Maximum number of retry attempts */
  maxRetries: number;
  /** Delay between retries in milliseconds */
  delayMs: number;
  /** Optional custom condition to determine if error is retryable */
  retryCondition?: (error: unknown) => boolean;
}

/**
 * Exponential backoff retry configuration
 * Used by Gateway requests that need jittered exponential backoff.
 */
export interface ExponentialBackoffConfig {
  type: RetryStrategyType.EXPONENTIAL_BACKOFF;
  /** Maximum number of retry attempts */
  maxRetries: number;
  /** Initial delay in milliseconds */
  initialDelayMs: number;
  /** Maximum delay cap in milliseconds */
  maxDelayMs: number;
  /** Multiplication factor for each retry */
  factor: number;
  /** Whether to add random jitter to prevent thundering herd */
  jitter?: boolean;
  /** Optional custom condition to determine if error is retryable */
  retryCondition?: (error: unknown) => boolean;
}

/**
 * Custom delays retry configuration
 * Allows specifying exact delay for each retry attempt
 */
export interface CustomDelaysConfig {
  type: RetryStrategyType.CUSTOM_DELAYS;
  /** Array of delays in milliseconds for each retry attempt */
  delays: number[];
  /** Optional custom condition to determine if error is retryable */
  retryCondition?: (error: unknown) => boolean;
}

/**
 * Union type for all retry strategy configurations
 */
export type RetryStrategy =
  | FixedDelayConfig
  | ExponentialBackoffConfig
  | CustomDelaysConfig;

/**
 * Calculate the delay for a retry attempt based on the strategy
 * @param strategy - The retry strategy configuration
 * @param attempt - The current attempt number (1-based)
 * @returns Delay in milliseconds before the next retry
 */
export function calculateRetryDelay(
  strategy: RetryStrategy,
  attempt: number,
): number {
  switch (strategy.type) {
    case RetryStrategyType.FIXED_DELAY:
      return strategy.delayMs;

    case RetryStrategyType.EXPONENTIAL_BACKOFF: {
      const delay = Math.min(
        strategy.initialDelayMs * Math.pow(strategy.factor, attempt - 1),
        strategy.maxDelayMs,
      );
      if (strategy.jitter) {
        // Add up to 1 second of random jitter
        return delay + Math.random() * 1000;
      }
      return delay;
    }

    case RetryStrategyType.CUSTOM_DELAYS: {
      // Use the last delay if attempt exceeds array length
      const index = Math.min(attempt - 1, strategy.delays.length - 1);
      return strategy.delays[index];
    }
  }
}

/**
 * Get the maximum number of retries for a strategy
 * @param strategy - The retry strategy configuration
 * @returns Maximum number of retry attempts
 */
export function getMaxRetries(strategy: RetryStrategy): number {
  switch (strategy.type) {
    case RetryStrategyType.FIXED_DELAY:
    case RetryStrategyType.EXPONENTIAL_BACKOFF:
      return strategy.maxRetries;
    case RetryStrategyType.CUSTOM_DELAYS:
      return strategy.delays.length;
  }
}

/**
 * Check if an error should be retried based on the strategy's condition
 * @param strategy - The retry strategy configuration
 * @param error - The error to check
 * @returns Whether the error should trigger a retry
 */
export function shouldRetryWithStrategy(
  strategy: RetryStrategy,
  error: unknown,
): boolean {
  if (strategy.retryCondition) {
    return strategy.retryCondition(error);
  }
  // Default: don't retry if no condition specified
  return false;
}

/**
 * Default retry strategies for each client type
 */
export const DEFAULT_RETRY_STRATEGIES = {
  /** Gateway default: exponential backoff with jitter. */
  gateway: {
    type: RetryStrategyType.EXPONENTIAL_BACKOFF,
    maxRetries: 3,
    initialDelayMs: 1000,
    maxDelayMs: 30000,
    factor: 2,
    jitter: true,
  } as ExponentialBackoffConfig,

  /** Admin client default: fixed delay */
  admin: {
    type: RetryStrategyType.FIXED_DELAY,
    maxRetries: 3,
    delayMs: 1000,
  } as FixedDelayConfig,
};
