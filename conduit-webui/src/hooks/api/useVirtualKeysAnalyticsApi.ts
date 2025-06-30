'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';

// Query key factory for Virtual Keys Analytics API
export const virtualKeysAnalyticsApiKeys = {
  all: ['virtual-keys-analytics-api'] as const,
  overview: () => [...virtualKeysAnalyticsApiKeys.all, 'overview'] as const,
  usage: () => [...virtualKeysAnalyticsApiKeys.all, 'usage'] as const,
  budget: () => [...virtualKeysAnalyticsApiKeys.all, 'budget'] as const,
  performance: () => [...virtualKeysAnalyticsApiKeys.all, 'performance'] as const,
  security: () => [...virtualKeysAnalyticsApiKeys.all, 'security'] as const,
  trends: () => [...virtualKeysAnalyticsApiKeys.all, 'trends'] as const,
  leaderboard: () => [...virtualKeysAnalyticsApiKeys.all, 'leaderboard'] as const,
} as const;

export interface VirtualKeyOverview {
  keyId: string;
  keyName: string;
  keyHash: string;
  status: 'active' | 'suspended' | 'expired' | 'rate_limited';
  createdAt: string;
  lastUsed: string;
  totalRequests: number;
  totalTokens: number;
  totalCost: number;
  budget: {
    limit: number;
    used: number;
    remaining: number;
    percentage: number;
  };
  models: {
    name: string;
    requests: number;
    cost: number;
  }[];
  regions: string[];
  errorRate: number;
  averageLatency: number;
  requestsToday: number;
  costToday: number;
  trends: {
    requests: number; // percentage change
    cost: number;
    latency: number;
    errors: number;
  };
}

export interface VirtualKeyUsageMetrics {
  keyId: string;
  timeRange: string;
  usageData: {
    timestamp: string;
    requests: number;
    tokens: number;
    cost: number;
    latency: number;
    errors: number;
    successRate: number;
    requestsPerHour: number;
  }[];
  modelBreakdown: {
    modelName: string;
    requests: number;
    tokens: number;
    cost: number;
    percentage: number;
    averageLatency: number;
  }[];
  endpointBreakdown: {
    endpoint: string;
    requests: number;
    cost: number;
    averageLatency: number;
    errorRate: number;
  }[];
  geographicDistribution: {
    region: string;
    requests: number;
    percentage: number;
    averageLatency: number;
  }[];
}

export interface VirtualKeyBudgetAnalytics {
  keyId: string;
  period: string;
  budget: {
    limit: number;
    spent: number;
    remaining: number;
    projectedSpend: number;
    daysRemaining: number;
    burnRate: number; // cost per day
  };
  spendHistory: {
    date: string;
    dailySpend: number;
    cumulativeSpend: number;
    budgetPercentage: number;
  }[];
  spendByModel: {
    modelName: string;
    cost: number;
    percentage: number;
    requests: number;
  }[];
  spendByEndpoint: {
    endpoint: string;
    cost: number;
    percentage: number;
    averageCostPerRequest: number;
  }[];
  alerts: {
    id: string;
    type: 'budget_warning' | 'budget_exceeded' | 'unusual_spend' | 'rate_limit';
    severity: 'low' | 'medium' | 'high' | 'critical';
    message: string;
    timestamp: string;
    acknowledged: boolean;
  }[];
  recommendations: {
    type: 'cost_optimization' | 'model_switch' | 'rate_limiting' | 'budget_adjustment';
    title: string;
    description: string;
    potentialSavings?: number;
    impact: 'low' | 'medium' | 'high';
  }[];
}

export interface VirtualKeyPerformanceMetrics {
  keyId: string;
  timeRange: string;
  latency: {
    average: number;
    p50: number;
    p90: number;
    p95: number;
    p99: number;
    trend: number; // percentage change
  };
  throughput: {
    requestsPerSecond: number;
    tokensPerSecond: number;
    peakRPS: number;
    averageRPS: number;
    trend: number;
  };
  reliability: {
    uptime: number;
    successRate: number;
    errorRate: number;
    timeouts: number;
    retries: number;
  };
  quotaUsage: {
    requestQuota: {
      limit: number;
      used: number;
      percentage: number;
      resetsAt: string;
    };
    tokenQuota: {
      limit: number;
      used: number;
      percentage: number;
      resetsAt: string;
    };
  };
  performanceHistory: {
    timestamp: string;
    avgLatency: number;
    requestsPerSecond: number;
    errorRate: number;
    successRate: number;
  }[];
}

export interface VirtualKeySecurityMetrics {
  keyId: string;
  timeRange: string;
  accessPatterns: {
    uniqueIPs: number;
    requestsByIP: {
      ip: string;
      requests: number;
      lastSeen: string;
      flagged: boolean;
      country?: string;
    }[];
    suspiciousActivity: {
      id: string;
      type: 'unusual_volume' | 'new_location' | 'rate_abuse' | 'pattern_change';
      severity: 'low' | 'medium' | 'high';
      description: string;
      timestamp: string;
      resolved: boolean;
    }[];
  };
  rateLimiting: {
    enforced: boolean;
    requestsPerMinute: number;
    requestsPerHour: number;
    violations: number;
    lastViolation?: string;
  };
  authentication: {
    validRequests: number;
    invalidRequests: number;
    malformedRequests: number;
    lastInvalidAttempt?: string;
  };
  compliance: {
    dataRegions: string[];
    retentionPolicy: string;
    encryptionStatus: 'enabled' | 'disabled';
    auditLogsEnabled: boolean;
  };
}

export interface VirtualKeyTrends {
  timeRange: string;
  trends: {
    requests: {
      current: number;
      previous: number;
      change: number;
      trend: 'up' | 'down' | 'stable';
    };
    cost: {
      current: number;
      previous: number;
      change: number;
      trend: 'up' | 'down' | 'stable';
    };
    latency: {
      current: number;
      previous: number;
      change: number;
      trend: 'up' | 'down' | 'stable';
    };
    errors: {
      current: number;
      previous: number;
      change: number;
      trend: 'up' | 'down' | 'stable';
    };
  };
  forecasting: {
    nextWeekRequests: number;
    nextWeekCost: number;
    budgetRunoutDate?: string;
    confidence: number; // percentage
  };
  seasonality: {
    hourlyPattern: { hour: number; avgRequests: number }[];
    dailyPattern: { day: string; avgRequests: number }[];
    peakHours: number[];
    quietHours: number[];
  };
}

export interface VirtualKeyLeaderboard {
  period: string;
  categories: {
    topByRequests: {
      keyId: string;
      keyName: string;
      requests: number;
      change: number;
    }[];
    topByCost: {
      keyId: string;
      keyName: string;
      cost: number;
      change: number;
    }[];
    topByTokens: {
      keyId: string;
      keyName: string;
      tokens: number;
      change: number;
    }[];
    mostEfficient: {
      keyId: string;
      keyName: string;
      costPerRequest: number;
      efficiency: number;
    }[];
    fastestResponse: {
      keyId: string;
      keyName: string;
      avgLatency: number;
      improvement: number;
    }[];
    mostReliable: {
      keyId: string;
      keyName: string;
      successRate: number;
      uptime: number;
    }[];
  };
}

export interface TimeRangeFilter {
  range: '1h' | '24h' | '7d' | '30d' | '90d';
}

export interface ExportRequest {
  type: 'overview' | 'usage' | 'budget' | 'performance' | 'security' | 'trends' | 'leaderboard';
  keyId?: string;
  timeRange: TimeRangeFilter;
  format?: 'csv' | 'json' | 'pdf';
}

export interface ExportResponse {
  url: string;
  filename: string;
  size: number;
  expiresAt: string;
}

// Virtual Keys Overview
export function useVirtualKeysOverview() {
  return useQuery({
    queryKey: virtualKeysAnalyticsApiKeys.overview(),
    queryFn: async (): Promise<VirtualKeyOverview[]> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.virtualKeys.getAnalyticsOverview();
        
        // Mock virtual keys overview data
        const mockData: VirtualKeyOverview[] = [
          {
            keyId: 'vk_001',
            keyName: 'Production API Key',
            keyHash: 'sk-...abc123',
            status: 'active',
            createdAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 30).toISOString(),
            lastUsed: new Date(Date.now() - 1000 * 60 * 15).toISOString(),
            totalRequests: 125678,
            totalTokens: 2456789,
            totalCost: 456.78,
            budget: {
              limit: 1000,
              used: 456.78,
              remaining: 543.22,
              percentage: 45.68,
            },
            models: [
              { name: 'gpt-4', requests: 45678, cost: 234.56 },
              { name: 'gpt-3.5-turbo', requests: 67890, cost: 123.45 },
              { name: 'dall-e-3', requests: 12110, cost: 98.77 },
            ],
            regions: ['us-east-1', 'us-west-2', 'eu-west-1'],
            errorRate: 0.8,
            averageLatency: 1245,
            requestsToday: 3456,
            costToday: 23.45,
            trends: {
              requests: 12.5,
              cost: 8.9,
              latency: -5.2,
              errors: -15.3,
            },
          },
          {
            keyId: 'vk_002',
            keyName: 'Development Key',
            keyHash: 'sk-...def456',
            status: 'active',
            createdAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 15).toISOString(),
            lastUsed: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(),
            totalRequests: 34567,
            totalTokens: 567890,
            totalCost: 89.12,
            budget: {
              limit: 200,
              used: 89.12,
              remaining: 110.88,
              percentage: 44.56,
            },
            models: [
              { name: 'gpt-3.5-turbo', requests: 28567, cost: 67.34 },
              { name: 'gpt-4', requests: 6000, cost: 21.78 },
            ],
            regions: ['us-east-1'],
            errorRate: 1.2,
            averageLatency: 987,
            requestsToday: 567,
            costToday: 4.56,
            trends: {
              requests: -2.3,
              cost: 1.8,
              latency: 3.1,
              errors: 8.7,
            },
          },
          {
            keyId: 'vk_003',
            keyName: 'Testing Environment',
            keyHash: 'sk-...ghi789',
            status: 'rate_limited',
            createdAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 7).toISOString(),
            lastUsed: new Date(Date.now() - 1000 * 60 * 60 * 6).toISOString(),
            totalRequests: 12345,
            totalTokens: 234567,
            totalCost: 34.56,
            budget: {
              limit: 100,
              used: 34.56,
              remaining: 65.44,
              percentage: 34.56,
            },
            models: [
              { name: 'gpt-3.5-turbo', requests: 12345, cost: 34.56 },
            ],
            regions: ['us-west-2'],
            errorRate: 2.1,
            averageLatency: 1456,
            requestsToday: 234,
            costToday: 2.34,
            trends: {
              requests: 45.2,
              cost: 23.1,
              latency: 12.3,
              errors: 34.5,
            },
          },
          {
            keyId: 'vk_004',
            keyName: 'Analytics Service',
            keyHash: 'sk-...jkl012',
            status: 'suspended',
            createdAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 5).toISOString(),
            lastUsed: new Date(Date.now() - 1000 * 60 * 60 * 24).toISOString(),
            totalRequests: 8901,
            totalTokens: 123456,
            totalCost: 78.90,
            budget: {
              limit: 50,
              used: 78.90,
              remaining: -28.90,
              percentage: 157.8,
            },
            models: [
              { name: 'gpt-4', requests: 8901, cost: 78.90 },
            ],
            regions: ['eu-west-1'],
            errorRate: 0.3,
            averageLatency: 2134,
            requestsToday: 0,
            costToday: 0,
            trends: {
              requests: -100,
              cost: -100,
              latency: 0,
              errors: -100,
            },
          },
        ];

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch virtual keys overview');
        throw new Error(error?.message || 'Failed to fetch virtual keys overview');
      }
    },
    staleTime: 30 * 1000, // 30 seconds
    refetchInterval: 60 * 1000, // Auto-refresh every minute
  });
}

// Virtual Key Usage Metrics
export function useVirtualKeyUsageMetrics(keyId: string, timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...virtualKeysAnalyticsApiKeys.usage(), keyId, timeRange.range],
    queryFn: async (): Promise<VirtualKeyUsageMetrics> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.virtualKeys.getUsageMetrics(keyId, timeRange);
        
        // Generate mock usage data
        const generateUsageData = (hours: number) => {
          const data = [];
          const now = new Date();
          
          for (let i = hours - 1; i >= 0; i--) {
            const timestamp = new Date(now.getTime() - i * 60 * 60 * 1000);
            const requests = Math.floor(50 + Math.random() * 200);
            const tokens = requests * (50 + Math.random() * 100);
            const cost = tokens * 0.002 + Math.random() * 5;
            const errors = Math.floor(requests * (Math.random() * 0.05));
            
            data.push({
              timestamp: timestamp.toISOString(),
              requests,
              tokens: Math.floor(tokens),
              cost: Math.round(cost * 100) / 100,
              latency: Math.round(800 + Math.random() * 800),
              errors,
              successRate: Math.round(((requests - errors) / requests) * 1000) / 10,
              requestsPerHour: Math.round(requests * (0.8 + Math.random() * 0.4)),
            });
          }
          
          return data;
        };

        const hours = timeRange.range === '1h' ? 1 : 
                     timeRange.range === '24h' ? 24 :
                     timeRange.range === '7d' ? 24 * 7 : 
                     timeRange.range === '30d' ? 24 * 30 : 24 * 90;

        const mockData: VirtualKeyUsageMetrics = {
          keyId,
          timeRange: timeRange.range,
          usageData: generateUsageData(Math.min(hours, 168)), // Max 1 week of hourly data
          modelBreakdown: [
            {
              modelName: 'gpt-4',
              requests: 45678,
              tokens: 1234567,
              cost: 234.56,
              percentage: 45.2,
              averageLatency: 1245,
            },
            {
              modelName: 'gpt-3.5-turbo',
              requests: 67890,
              tokens: 1567890,
              cost: 123.45,
              percentage: 32.8,
              averageLatency: 987,
            },
            {
              modelName: 'dall-e-3',
              requests: 12110,
              tokens: 234567,
              cost: 98.77,
              percentage: 22.0,
              averageLatency: 2134,
            },
          ],
          endpointBreakdown: [
            {
              endpoint: '/v1/chat/completions',
              requests: 98765,
              cost: 345.67,
              averageLatency: 1123,
              errorRate: 0.8,
            },
            {
              endpoint: '/v1/images/generations',
              requests: 12110,
              cost: 98.77,
              averageLatency: 2134,
              errorRate: 1.2,
            },
            {
              endpoint: '/v1/embeddings',
              requests: 23456,
              cost: 23.45,
              averageLatency: 567,
              errorRate: 0.3,
            },
          ],
          geographicDistribution: [
            {
              region: 'us-east-1',
              requests: 67890,
              percentage: 55.2,
              averageLatency: 987,
            },
            {
              region: 'us-west-2',
              requests: 34567,
              percentage: 28.1,
              averageLatency: 1245,
            },
            {
              region: 'eu-west-1',
              requests: 20543,
              percentage: 16.7,
              averageLatency: 1567,
            },
          ],
        };

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch virtual key usage metrics');
        throw new Error(error?.message || 'Failed to fetch virtual key usage metrics');
      }
    },
    enabled: !!keyId,
    staleTime: 60 * 1000, // 1 minute
  });
}

// Virtual Key Budget Analytics
export function useVirtualKeyBudgetAnalytics(keyId: string, period: string = '30d') {
  return useQuery({
    queryKey: [...virtualKeysAnalyticsApiKeys.budget(), keyId, period],
    queryFn: async (): Promise<VirtualKeyBudgetAnalytics> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.virtualKeys.getBudgetAnalytics(keyId, period);
        
        // Generate mock budget data
        const days = period === '7d' ? 7 : period === '30d' ? 30 : 90;
        const spendHistory = [];
        const totalBudget = 1000;
        let cumulativeSpend = 0;
        
        for (let i = days - 1; i >= 0; i--) {
          const date = new Date(Date.now() - i * 24 * 60 * 60 * 1000);
          const dailySpend = 5 + Math.random() * 25;
          cumulativeSpend += dailySpend;
          
          spendHistory.push({
            date: date.toISOString().split('T')[0],
            dailySpend: Math.round(dailySpend * 100) / 100,
            cumulativeSpend: Math.round(cumulativeSpend * 100) / 100,
            budgetPercentage: Math.round((cumulativeSpend / totalBudget) * 1000) / 10,
          });
        }

        const mockData: VirtualKeyBudgetAnalytics = {
          keyId,
          period,
          budget: {
            limit: totalBudget,
            spent: cumulativeSpend,
            remaining: totalBudget - cumulativeSpend,
            projectedSpend: cumulativeSpend * 1.2,
            daysRemaining: Math.floor((totalBudget - cumulativeSpend) / (cumulativeSpend / days)),
            burnRate: cumulativeSpend / days,
          },
          spendHistory,
          spendByModel: [
            {
              modelName: 'gpt-4',
              cost: cumulativeSpend * 0.45,
              percentage: 45.0,
              requests: 45678,
            },
            {
              modelName: 'gpt-3.5-turbo',
              cost: cumulativeSpend * 0.33,
              percentage: 33.0,
              requests: 67890,
            },
            {
              modelName: 'dall-e-3',
              cost: cumulativeSpend * 0.22,
              percentage: 22.0,
              requests: 12110,
            },
          ],
          spendByEndpoint: [
            {
              endpoint: '/v1/chat/completions',
              cost: cumulativeSpend * 0.78,
              percentage: 78.0,
              averageCostPerRequest: 0.0034,
            },
            {
              endpoint: '/v1/images/generations',
              cost: cumulativeSpend * 0.15,
              percentage: 15.0,
              averageCostPerRequest: 0.045,
            },
            {
              endpoint: '/v1/embeddings',
              cost: cumulativeSpend * 0.07,
              percentage: 7.0,
              averageCostPerRequest: 0.0002,
            },
          ],
          alerts: [
            {
              id: 'alert_budget_001',
              type: 'budget_warning',
              severity: 'medium',
              message: 'Budget usage is at 75% - consider reviewing spend patterns',
              timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(),
              acknowledged: false,
            },
          ],
          recommendations: [
            {
              type: 'model_switch',
              title: 'Consider using GPT-3.5-turbo for simple tasks',
              description: 'You could save approximately 60% on costs by using GPT-3.5-turbo for simpler completions',
              potentialSavings: 120.45,
              impact: 'high',
            },
            {
              type: 'rate_limiting',
              title: 'Implement request rate limiting',
              description: 'Adding rate limits could help control unexpected usage spikes',
              impact: 'medium',
            },
          ],
        };

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch virtual key budget analytics');
        throw new Error(error?.message || 'Failed to fetch virtual key budget analytics');
      }
    },
    enabled: !!keyId,
    staleTime: 2 * 60 * 1000, // 2 minutes
  });
}

// Virtual Key Performance Metrics
export function useVirtualKeyPerformanceMetrics(keyId: string, timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...virtualKeysAnalyticsApiKeys.performance(), keyId, timeRange.range],
    queryFn: async (): Promise<VirtualKeyPerformanceMetrics> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.virtualKeys.getPerformanceMetrics(keyId, timeRange);
        
        // Generate mock performance history
        const hours = timeRange.range === '1h' ? 1 : 
                     timeRange.range === '24h' ? 24 :
                     timeRange.range === '7d' ? 24 * 7 : 
                     timeRange.range === '30d' ? 24 * 30 : 24 * 90;

        const performanceHistory = [];
        for (let i = Math.min(hours, 168) - 1; i >= 0; i--) {
          const timestamp = new Date(Date.now() - i * 60 * 60 * 1000);
          performanceHistory.push({
            timestamp: timestamp.toISOString(),
            avgLatency: Math.round(800 + Math.random() * 800),
            requestsPerSecond: Math.round((10 + Math.random() * 40) * 10) / 10,
            errorRate: Math.round(Math.random() * 300) / 100,
            successRate: Math.round((97 + Math.random() * 3) * 100) / 100,
          });
        }

        const mockData: VirtualKeyPerformanceMetrics = {
          keyId,
          timeRange: timeRange.range,
          latency: {
            average: 1245,
            p50: 987,
            p90: 1567,
            p95: 2134,
            p99: 3456,
            trend: -5.2,
          },
          throughput: {
            requestsPerSecond: 25.7,
            tokensPerSecond: 1234.5,
            peakRPS: 78.9,
            averageRPS: 25.7,
            trend: 12.3,
          },
          reliability: {
            uptime: 99.8,
            successRate: 98.7,
            errorRate: 1.3,
            timeouts: 23,
            retries: 156,
          },
          quotaUsage: {
            requestQuota: {
              limit: 10000,
              used: 7834,
              percentage: 78.34,
              resetsAt: new Date(Date.now() + 1000 * 60 * 60 * 6).toISOString(),
            },
            tokenQuota: {
              limit: 1000000,
              used: 567890,
              percentage: 56.79,
              resetsAt: new Date(Date.now() + 1000 * 60 * 60 * 6).toISOString(),
            },
          },
          performanceHistory,
        };

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch virtual key performance metrics');
        throw new Error(error?.message || 'Failed to fetch virtual key performance metrics');
      }
    },
    enabled: !!keyId,
    staleTime: 60 * 1000, // 1 minute
  });
}

// Virtual Key Security Metrics
export function useVirtualKeySecurityMetrics(keyId: string, timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...virtualKeysAnalyticsApiKeys.security(), keyId, timeRange.range],
    queryFn: async (): Promise<VirtualKeySecurityMetrics> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.virtualKeys.getSecurityMetrics(keyId, timeRange);
        
        const mockData: VirtualKeySecurityMetrics = {
          keyId,
          timeRange: timeRange.range,
          accessPatterns: {
            uniqueIPs: 23,
            requestsByIP: [
              {
                ip: '192.168.1.100',
                requests: 12456,
                lastSeen: new Date(Date.now() - 1000 * 60 * 15).toISOString(),
                flagged: false,
                country: 'United States',
              },
              {
                ip: '10.0.0.50',
                requests: 8901,
                lastSeen: new Date(Date.now() - 1000 * 60 * 30).toISOString(),
                flagged: false,
                country: 'United States',
              },
              {
                ip: '203.0.113.42',
                requests: 5678,
                lastSeen: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(),
                flagged: true,
                country: 'Unknown',
              },
            ],
            suspiciousActivity: [
              {
                id: 'sus_001',
                type: 'unusual_volume',
                severity: 'medium',
                description: 'Request volume increased by 300% in the last hour',
                timestamp: new Date(Date.now() - 1000 * 60 * 60).toISOString(),
                resolved: false,
              },
              {
                id: 'sus_002',
                type: 'new_location',
                severity: 'low',
                description: 'Requests detected from new geographic location',
                timestamp: new Date(Date.now() - 1000 * 60 * 60 * 3).toISOString(),
                resolved: true,
              },
            ],
          },
          rateLimiting: {
            enforced: true,
            requestsPerMinute: 100,
            requestsPerHour: 5000,
            violations: 12,
            lastViolation: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(),
          },
          authentication: {
            validRequests: 124567,
            invalidRequests: 23,
            malformedRequests: 8,
            lastInvalidAttempt: new Date(Date.now() - 1000 * 60 * 60 * 6).toISOString(),
          },
          compliance: {
            dataRegions: ['us-east-1', 'us-west-2'],
            retentionPolicy: '90 days',
            encryptionStatus: 'enabled',
            auditLogsEnabled: true,
          },
        };

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch virtual key security metrics');
        throw new Error(error?.message || 'Failed to fetch virtual key security metrics');
      }
    },
    enabled: !!keyId,
    staleTime: 2 * 60 * 1000, // 2 minutes
  });
}

// Virtual Key Trends
export function useVirtualKeyTrends(keyId: string, timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...virtualKeysAnalyticsApiKeys.trends(), keyId, timeRange.range],
    queryFn: async (): Promise<VirtualKeyTrends> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.virtualKeys.getTrends(keyId, timeRange);
        
        const mockData: VirtualKeyTrends = {
          timeRange: timeRange.range,
          trends: {
            requests: {
              current: 12567,
              previous: 11234,
              change: 11.9,
              trend: 'up',
            },
            cost: {
              current: 456.78,
              previous: 423.45,
              change: 7.9,
              trend: 'up',
            },
            latency: {
              current: 1245,
              previous: 1312,
              change: -5.1,
              trend: 'down',
            },
            errors: {
              current: 23,
              previous: 45,
              change: -48.9,
              trend: 'down',
            },
          },
          forecasting: {
            nextWeekRequests: 14523,
            nextWeekCost: 512.34,
            budgetRunoutDate: new Date(Date.now() + 1000 * 60 * 60 * 24 * 65).toISOString(),
            confidence: 85.7,
          },
          seasonality: {
            hourlyPattern: Array.from({ length: 24 }, (_, hour) => ({
              hour,
              avgRequests: 50 + Math.random() * 100 + (hour >= 9 && hour <= 17 ? 50 : 0),
            })),
            dailyPattern: [
              { day: 'Monday', avgRequests: 1567 },
              { day: 'Tuesday', avgRequests: 1734 },
              { day: 'Wednesday', avgRequests: 1823 },
              { day: 'Thursday', avgRequests: 1756 },
              { day: 'Friday', avgRequests: 1654 },
              { day: 'Saturday', avgRequests: 987 },
              { day: 'Sunday', avgRequests: 856 },
            ],
            peakHours: [10, 11, 14, 15, 16],
            quietHours: [0, 1, 2, 3, 4, 5, 22, 23],
          },
        };

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch virtual key trends');
        throw new Error(error?.message || 'Failed to fetch virtual key trends');
      }
    },
    enabled: !!keyId,
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

// Virtual Keys Leaderboard
export function useVirtualKeysLeaderboard(period: string = '30d') {
  return useQuery({
    queryKey: [...virtualKeysAnalyticsApiKeys.leaderboard(), period],
    queryFn: async (): Promise<VirtualKeyLeaderboard> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.virtualKeys.getLeaderboard(period);
        
        const mockData: VirtualKeyLeaderboard = {
          period,
          categories: {
            topByRequests: [
              { keyId: 'vk_001', keyName: 'Production API Key', requests: 125678, change: 12.5 },
              { keyId: 'vk_002', keyName: 'Development Key', requests: 34567, change: -2.3 },
              { keyId: 'vk_003', keyName: 'Testing Environment', requests: 12345, change: 45.2 },
              { keyId: 'vk_004', keyName: 'Analytics Service', requests: 8901, change: -15.7 },
            ],
            topByCost: [
              { keyId: 'vk_001', keyName: 'Production API Key', cost: 456.78, change: 8.9 },
              { keyId: 'vk_002', keyName: 'Development Key', cost: 89.12, change: 1.8 },
              { keyId: 'vk_004', keyName: 'Analytics Service', cost: 78.90, change: -12.3 },
              { keyId: 'vk_003', keyName: 'Testing Environment', cost: 34.56, change: 23.1 },
            ],
            topByTokens: [
              { keyId: 'vk_001', keyName: 'Production API Key', tokens: 2456789, change: 10.2 },
              { keyId: 'vk_002', keyName: 'Development Key', tokens: 567890, change: 3.4 },
              { keyId: 'vk_003', keyName: 'Testing Environment', tokens: 234567, change: 34.1 },
              { keyId: 'vk_004', keyName: 'Analytics Service', tokens: 123456, change: -20.5 },
            ],
            mostEfficient: [
              { keyId: 'vk_002', keyName: 'Development Key', costPerRequest: 0.0026, efficiency: 95.2 },
              { keyId: 'vk_003', keyName: 'Testing Environment', costPerRequest: 0.0028, efficiency: 92.1 },
              { keyId: 'vk_001', keyName: 'Production API Key', costPerRequest: 0.0036, efficiency: 87.5 },
              { keyId: 'vk_004', keyName: 'Analytics Service', costPerRequest: 0.0089, efficiency: 65.3 },
            ],
            fastestResponse: [
              { keyId: 'vk_002', keyName: 'Development Key', avgLatency: 987, improvement: 5.2 },
              { keyId: 'vk_001', keyName: 'Production API Key', avgLatency: 1245, improvement: -2.1 },
              { keyId: 'vk_003', keyName: 'Testing Environment', avgLatency: 1456, improvement: -8.7 },
              { keyId: 'vk_004', keyName: 'Analytics Service', avgLatency: 2134, improvement: 12.3 },
            ],
            mostReliable: [
              { keyId: 'vk_001', keyName: 'Production API Key', successRate: 99.2, uptime: 99.8 },
              { keyId: 'vk_002', keyName: 'Development Key', successRate: 98.8, uptime: 99.5 },
              { keyId: 'vk_003', keyName: 'Testing Environment', successRate: 97.9, uptime: 98.2 },
              { keyId: 'vk_004', keyName: 'Analytics Service', successRate: 99.7, uptime: 95.6 },
            ],
          },
        };

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch virtual keys leaderboard');
        throw new Error(error?.message || 'Failed to fetch virtual keys leaderboard');
      }
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
    refetchInterval: 10 * 60 * 1000, // Auto-refresh every 10 minutes
  });
}

// Export Virtual Keys Data
export function useExportVirtualKeysData() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (request: ExportRequest): Promise<ExportResponse> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.virtualKeys.exportData(request);
        
        // Simulate API call delay
        await new Promise(resolve => setTimeout(resolve, 2000));
        
        const mockResponse: ExportResponse = {
          url: `/downloads/virtual-keys-${request.type}-${Date.now()}.${request.format || 'csv'}`,
          filename: `virtual-keys-${request.type}-${new Date().toISOString().split('T')[0]}.${request.format || 'csv'}`,
          size: 1024 * 1024 * 2.5, // 2.5MB
          expiresAt: new Date(Date.now() + 1000 * 60 * 60 * 24).toISOString(), // 24 hours
        };

        return mockResponse;
      } catch (error: any) {
        reportError(error, 'Failed to export virtual keys data');
        throw new Error(error?.message || 'Failed to export virtual keys data');
      }
    },
  });
}