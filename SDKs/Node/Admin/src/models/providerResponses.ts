/**
 * Response type definitions for provider service methods
 * These interfaces define the structure of API responses for type safety
 */

import { ProviderType } from '@knn_labs/conduit-common';

/**
 * Provider data structure from API responses
 */
export interface ProviderData {
  providerId?: string;
  id?: string;
  providerType?: ProviderType;
  name?: string;
  status?: string;
  lastChecked?: string;
  avgLatency?: number;
  uptime?: {
    percentage?: number;
  };
  metrics?: {
    issues?: {
      rate?: number;
    };
  };
  lastIncident?: {
    message?: string;
  };
  errorRate?: number;
  details?: Record<string, unknown>;
}

/**
 * Health data response from provider health endpoints
 */
export interface HealthDataResponse {
  providerId?: string;
  providerType?: ProviderType;
  status?: string;
  lastChecked?: string;
  avgLatency?: number;
  uptime?: {
    percentage?: number;
  };
  metrics?: {
    issues?: {
      rate?: number;
    };
  };
  lastIncident?: {
    message?: string;
  };
  providers?: ProviderData[];
}

/**
 * Health configuration data structure
 */
export interface HealthConfigurationData {
  id: string;
  providerId: string;
  checkInterval: number;
  timeout: number;
  retryAttempts: number;
  thresholds: {
    responseTime: number;
    errorRate: number;
    uptime: number;
  };
  enabled: boolean;
  createdAt: string;
  updatedAt: string;
}

/**
 * Health history data structure
 */
export interface HealthHistoryData {
  providerId: string;
  timestamp: string;
  status: string;
  responseTime: number;
  errorRate: number;
  availability: number;
}

/**
 * Metrics data response from performance endpoints
 */
export interface MetricsDataResponse {
  providerType?: ProviderType;
  totalRequests?: number;
  failedRequests?: number;
  avgResponseTime?: number;
  p95ResponseTime?: number;
  p99ResponseTime?: number;
  availability?: number;
  endpoints?: Array<{
    name: string;
    status: 'healthy' | 'degraded' | 'down';
    responseTime: number;
    lastCheck: string;
  }>;
  models?: Array<{
    name: string;
    available: boolean;
    responseTime: number;
    tokenCapacity: {
      used: number;
      total: number;
    };
  }>;
  rateLimit?: {
    requests: {
      used: number;
      limit: number;
      reset: string;
    };
    tokens: {
      used: number;
      limit: number;
      reset: string;
    };
  };
  incidents?: Array<{
    id: string;
    timestamp: string;
    type: 'outage' | 'degradation' | 'rate_limit';
    duration: number;
    message: string;
    resolved: boolean;
  }>;
}