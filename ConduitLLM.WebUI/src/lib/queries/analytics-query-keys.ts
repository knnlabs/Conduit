'use client';

/**
 * Centralized query key factory for all analytics-related queries
 * This ensures consistent query key structure across all analytics hooks
 */

// Base analytics keys
const analyticsBase = ['analytics'] as const;

// Cost Analytics Query Keys
export const costAnalyticsKeys = {
  all: [...analyticsBase, 'cost'] as const,
  summary: (timeRange: unknown) => [...costAnalyticsKeys.all, 'summary', timeRange] as const,
  trends: (timeRange: unknown) => [...costAnalyticsKeys.all, 'trends', timeRange] as const,
  providers: (timeRange: unknown) => [...costAnalyticsKeys.all, 'providers', timeRange] as const,
  models: (timeRange: unknown) => [...costAnalyticsKeys.all, 'models', timeRange] as const,
  virtualKeys: (timeRange: unknown) => [...costAnalyticsKeys.all, 'virtual-keys', timeRange] as const,
  alerts: () => [...costAnalyticsKeys.all, 'alerts'] as const,
} as const;

// Usage Analytics Query Keys
export const usageAnalyticsKeys = {
  all: [...analyticsBase, 'usage'] as const,
  metrics: (timeRange: unknown) => [...usageAnalyticsKeys.all, 'metrics', timeRange] as const,
  requests: (timeRange: unknown) => [...usageAnalyticsKeys.all, 'requests', timeRange] as const,
  tokens: (timeRange: unknown) => [...usageAnalyticsKeys.all, 'tokens', timeRange] as const,
  errors: (timeRange: unknown) => [...usageAnalyticsKeys.all, 'errors', timeRange] as const,
  latency: (timeRange: unknown) => [...usageAnalyticsKeys.all, 'latency', timeRange] as const,
  users: (timeRange: unknown) => [...usageAnalyticsKeys.all, 'users', timeRange] as const,
  endpoints: (timeRange: unknown) => [...usageAnalyticsKeys.all, 'endpoints', timeRange] as const,
} as const;

// Virtual Keys Analytics Query Keys
export const virtualKeysAnalyticsKeys = {
  all: [...analyticsBase, 'virtual-keys'] as const,
  overview: () => [...virtualKeysAnalyticsKeys.all, 'overview'] as const,
  usage: (keyId: string, timeRange: string) => [...virtualKeysAnalyticsKeys.all, 'usage', keyId, timeRange] as const,
  budget: (keyId: string, period: string) => [...virtualKeysAnalyticsKeys.all, 'budget', keyId, period] as const,
  performance: (keyId: string, timeRange: string) => [...virtualKeysAnalyticsKeys.all, 'performance', keyId, timeRange] as const,
  security: (keyId: string, timeRange: string) => [...virtualKeysAnalyticsKeys.all, 'security', keyId, timeRange] as const,
  trends: (keyId: string, timeRange: string) => [...virtualKeysAnalyticsKeys.all, 'trends', keyId, timeRange] as const,
  leaderboard: (period: string) => [...virtualKeysAnalyticsKeys.all, 'leaderboard', period] as const,
} as const;

// Aggregated key factory for invalidation
export const analyticsKeys = {
  all: analyticsBase,
  cost: costAnalyticsKeys,
  usage: usageAnalyticsKeys,
  virtualKeys: virtualKeysAnalyticsKeys,
  // Helper functions for broad invalidation
  invalidateAll: () => analyticsBase,
  invalidateCost: () => costAnalyticsKeys.all,
  invalidateUsage: () => usageAnalyticsKeys.all,
  invalidateVirtualKeys: () => virtualKeysAnalyticsKeys.all,
} as const;