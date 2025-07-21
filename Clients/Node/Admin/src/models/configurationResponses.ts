/**
 * Response type definitions for configuration service methods
 * These interfaces define the structure of API responses for type safety
 */

import type { CircuitBreakerStatus } from './configurationExtended';

/**
 * Response structure for circuit breaker update operations
 */
export interface CircuitBreakerUpdateResponse {
  config: {
    id: string;
    routeId: string;
    failureThreshold: number;
    successThreshold: number;
    timeout: number;
    slidingWindowSize: number;
    minimumNumberOfCalls: number;
    slowCallDurationThreshold: number;
    slowCallRateThreshold: number;
    enabled: boolean;
  };
  state: 'open' | 'closed' | 'half-open';
  metrics: {
    failureRate: number;
    slowCallRate: number;
    numberOfCalls: number;
    numberOfFailedCalls: number;
    numberOfSlowCalls: number;
    numberOfSuccessfulCalls: number;
  };
  stateTransitions: Array<{
    timestamp: string;
    fromState: string;
    toState: string;
    reason: string;
  }>;
  lastStateChange: string;
  nextRetryAttempt?: string;
}

/**
 * Response structure for routing subscription operations
 */
export interface RoutingSubscriptionResponse {
  connectionId: string;
}

/**
 * Response structure containing routing health data  
 */
export interface RoutingHealthData {
  health?: any; // This will be replaced with specific type
  routes?: any; // This will be replaced with specific type
  history?: any; // This will be replaced with specific type
  subscription?: {
    endpoint: string;
    connectionId: string;
    events: string[];
  };
}

/**
 * Transform circuit breaker response to CircuitBreakerStatus
 */
export function transformToCircuitBreakerStatus(response: CircuitBreakerUpdateResponse): CircuitBreakerStatus {
  return {
    config: response.config,
    state: response.state,
    metrics: response.metrics,
    stateTransitions: response.stateTransitions,
    lastStateChange: response.lastStateChange,
    nextRetryAttempt: response.nextRetryAttempt
  };
}