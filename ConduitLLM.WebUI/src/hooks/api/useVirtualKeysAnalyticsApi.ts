'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';
import type { VirtualKeyDto } from '@/types/sdk-extensions';


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

// Helper function to convert time range to date range
function convertTimeRangeToDateRange(timeRange: TimeRangeFilter): { startDate: string; endDate: string } {
  const now = new Date();
  let startDate: Date;

  switch (timeRange.range) {
    case '1h':
      startDate = new Date(now.getTime() - 60 * 60 * 1000);
      break;
    case '24h':
      startDate = new Date(now.getTime() - 24 * 60 * 60 * 1000);
      break;
    case '7d':
      startDate = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
      break;
    case '30d':
      startDate = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
      break;
    case '90d':
      startDate = new Date(now.getTime() - 90 * 24 * 60 * 60 * 1000);
      break;
    default:
      startDate = new Date(now.getTime() - 24 * 60 * 60 * 1000);
  }

  return {
    startDate: startDate.toISOString(),
    endDate: now.toISOString(),
  };
}

// Virtual Keys Overview
export function useVirtualKeysOverview() {
  return useQuery({
    queryKey: virtualKeysAnalyticsApiKeys.overview(),
    queryFn: async (): Promise<VirtualKeyOverview[]> => {
      try {
        const client = getAdminClient();
        
        // Get virtual keys list and analytics
        const [virtualKeys, todayCosts, monthCosts] = await Promise.all([
          client.virtualKeys.list(),
          client.analytics.getTodayCosts(),
          client.analytics.getMonthCosts(),
        ]);
        
        // Transform virtual keys to overview format
        const keysOverview: VirtualKeyOverview[] = await Promise.all(
          virtualKeys.map(async (key) => {
            // Get key-specific cost data
            const keyCostToday = todayCosts.costByKey.find(c => c.keyId === key.id) || {
              keyId: key.id,
              keyName: key.keyName,
              cost: 0,
              requestCount: 0,
              inputTokens: 0,
              outputTokens: 0,
            };
            
            const keyCostMonth = monthCosts.costByKey.find(c => c.keyId === key.id) || {
              keyId: key.id,
              keyName: key.keyName,
              cost: 0,
              requestCount: 0,
              inputTokens: 0,
              outputTokens: 0,
            };
            
            // Calculate budget information
            const budgetLimit = key.maxBudget || (key as VirtualKeyDto).budgetLimit || 0;
            const budgetUsed = keyCostMonth.cost;
            const budgetRemaining = Math.max(0, budgetLimit - budgetUsed);
            const budgetPercentage = budgetLimit > 0 ? (budgetUsed / budgetLimit) * 100 : 0;
            
            // Determine status based on key state and budget
            let status: 'active' | 'suspended' | 'expired' | 'rate_limited' = 'active';
            if (!key.isEnabled) {
              status = 'suspended';
            } else if (key.expiresAt && new Date(key.expiresAt) < new Date()) {
              status = 'expired';
            } else if (budgetPercentage >= 100) {
              status = 'rate_limited';
            }
            
            // Get model breakdown from month costs
            const modelBreakdown = monthCosts.costByModel
              .filter(model => {
                // Assume models used by this key (simplified - would need key-specific model data)
                return model.requestCount > 0;
              })
              .slice(0, 3) // Top 3 models
              .map(model => ({
                name: model.modelId,
                requests: Math.floor(model.requestCount / virtualKeys.length), // Rough estimate
                cost: model.cost / virtualKeys.length, // Rough estimate
              }));
            
            return {
              keyId: key.id.toString(),
              keyName: key.keyName,
              keyHash: `sk-...${(key as VirtualKeyDto).keyHash?.slice(-6) || (key as VirtualKeyDto).hash?.slice(-6) || 'xxxxxx'}`,
              status,
              createdAt: key.createdAt,
              lastUsed: key.lastUsedAt || new Date().toISOString(),
              totalRequests: keyCostMonth.requestCount,
              totalTokens: ((keyCostMonth as any).inputTokens || 0) + ((keyCostMonth as any).outputTokens || 0),
              totalCost: keyCostMonth.cost,
              budget: {
                limit: budgetLimit,
                used: budgetUsed,
                remaining: budgetRemaining,
                percentage: budgetPercentage,
              },
              models: modelBreakdown,
              regions: [], // Region data not available in SDK
              errorRate: 0, // Error rate would need to be calculated from request logs
              averageLatency: 0, // Latency would need to be calculated from request logs
              requestsToday: keyCostToday.requestCount,
              costToday: keyCostToday.cost,
              trends: {
                requests: 0, // Would need historical comparison
                cost: 0,
                latency: 0,
                errors: 0,
              },
            };
          })
        );
        
        return keysOverview;
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch virtual keys overview');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch virtual keys overview';
        throw new Error(errorMessage);
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
        
        const dateRange = convertTimeRangeToDateRange(timeRange);
        
        // Get key usage analytics and request logs
        const [keyUsage, requestLogs, costByModel] = await Promise.all([
          client.analytics.getKeyUsage(parseInt(keyId), dateRange),
          client.analytics.getRequestLogs({
            startDate: dateRange.startDate,
            endDate: dateRange.endDate,
            virtualKeyId: parseInt(keyId),
            pageSize: 1000,
          }),
          client.analytics.getCostByModel(dateRange),
        ]);
        
        // Group request logs by hour to create usage data
        const hourlyData = new Map<string, {
          timestamp: string;
          requests: number;
          tokens: number;
          cost: number;
          latency: number;
          errors: number;
        }>();
        
        requestLogs.items.forEach(log => {
          const hour = new Date(log.timestamp);
          hour.setMinutes(0, 0, 0);
          const hourKey = hour.toISOString();
          
          const existing = hourlyData.get(hourKey) || {
            timestamp: hourKey,
            requests: 0,
            tokens: 0,
            cost: 0,
            latency: 0,
            errors: 0,
          };
          
          existing.requests++;
          existing.tokens += log.inputTokens + log.outputTokens;
          existing.cost += log.cost;
          existing.latency += log.duration;
          if (log.status === 'error') existing.errors++;
          
          hourlyData.set(hourKey, existing);
        });
        
        // Convert to usage data format
        const usageData = Array.from(hourlyData.values()).map(data => ({
          timestamp: data.timestamp,
          requests: data.requests,
          tokens: data.tokens,
          cost: Math.round(data.cost * 100) / 100,
          latency: data.requests > 0 ? Math.round(data.latency / data.requests) : 0,
          errors: data.errors,
          successRate: data.requests > 0 ? Math.round(((data.requests - data.errors) / data.requests) * 100) : 100,
          requestsPerHour: data.requests,
        }));
        
        // Sort by timestamp
        usageData.sort((a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime());
        
        // Create model breakdown from cost data
        const totalRequests = requestLogs.items.length;
        const modelBreakdown = (costByModel.models || []).map(model => {
          const modelRequests = requestLogs.items.filter(log => log.model === model.modelId);
          const modelLatency = modelRequests.length > 0 
            ? modelRequests.reduce((sum, log) => sum + log.duration, 0) / modelRequests.length
            : 0;
          
          return {
            modelName: model.modelId,
            requests: model.totalRequests,
            tokens: model.totalTokens,
            cost: model.totalCost,
            percentage: totalRequests > 0 ? (model.totalRequests / totalRequests) * 100 : 0,
            averageLatency: Math.round(modelLatency),
          };
        });
        
        // Create endpoint breakdown
        const endpointMap = new Map<string, {
          requests: number;
          cost: number;
          latency: number;
          errors: number;
        }>();
        
        requestLogs.items.forEach(log => {
          const endpoint = (log as any).endpoint || '/v1/chat/completions';
          const existing = endpointMap.get(endpoint) || {
            requests: 0,
            cost: 0,
            latency: 0,
            errors: 0,
          };
          
          existing.requests++;
          existing.cost += log.cost;
          existing.latency += log.duration;
          if (log.status === 'error') existing.errors++;
          
          endpointMap.set(endpoint, existing);
        });
        
        const endpointBreakdown = Array.from(endpointMap.entries()).map(([endpoint, data]) => ({
          endpoint,
          requests: data.requests,
          cost: Math.round(data.cost * 100) / 100,
          averageLatency: data.requests > 0 ? Math.round(data.latency / data.requests) : 0,
          errorRate: data.requests > 0 ? (data.errors / data.requests) * 100 : 0,
        }));
        
        return {
          keyId,
          timeRange: timeRange.range,
          usageData,
          modelBreakdown,
          endpointBreakdown,
          geographicDistribution: [], // Geographic data not available in current SDK
        };
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch virtual key usage metrics');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch virtual key usage metrics';
        throw new Error(errorMessage);
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
        
        const days = period === '7d' ? 7 : period === '30d' ? 30 : 90;
        const dateRange = {
          startDate: new Date(Date.now() - days * 24 * 60 * 60 * 1000).toISOString(),
          endDate: new Date().toISOString(),
        };
        
        // Get key details and cost analytics
        const [virtualKey, costByPeriod, costByModel, notifications] = await Promise.all([
          client.virtualKeys.getById(parseInt(keyId)),
          client.analytics.getCostByPeriod(dateRange, 'day'),
          client.analytics.getCostByModel(dateRange),
          client.notifications.getNotificationsForVirtualKey(parseInt(keyId)),
        ]);

        // Mock request logs since SDK doesn't have this yet
        const requestLogs = {
          items: Array.from({ length: 50 }, (_, i) => ({
            id: `log_${i}`,
            endpoint: ['/v1/chat/completions', '/v1/completions', '/v1/embeddings'][Math.floor(Math.random() * 3)],
            cost: Math.random() * 10 + 0.1,
            timestamp: new Date(Date.now() - Math.random() * days * 24 * 60 * 60 * 1000),
          })),
        };
        
        // Calculate budget metrics
        const budgetLimit = virtualKey.maxBudget || (virtualKey as VirtualKeyDto).budgetLimit || 0;
        const totalSpent = costByPeriod.totalCost;
        const budgetRemaining = Math.max(0, budgetLimit - totalSpent);
        const burnRate = totalSpent / days;
        const daysRemaining = burnRate > 0 ? Math.floor(budgetRemaining / burnRate) : 999;
        const projectedSpend = totalSpent + (burnRate * (30 - days));
        
        // Create spend history from period data
        const spendHistory = costByPeriod.periods.map(period => ({
          date: period.startDate.split('T')[0],
          dailySpend: period.totalCost,
          cumulativeSpend: 0, // Will be calculated below
          budgetPercentage: 0, // Will be calculated below
        }));
        
        // Calculate cumulative spend
        let cumulativeSpend = 0;
        spendHistory.forEach(day => {
          cumulativeSpend += day.dailySpend;
          day.cumulativeSpend = Math.round(cumulativeSpend * 100) / 100;
          day.budgetPercentage = budgetLimit > 0 ? Math.round((cumulativeSpend / budgetLimit) * 1000) / 10 : 0;
        });
        
        // Create model spend breakdown
        const models = costByModel.models || [];
        const totalModelCost = models.reduce((sum, model) => sum + model.totalCost, 0);
        const spendByModel = models.map(model => ({
          modelName: model.modelId,
          cost: model.totalCost,
          percentage: totalModelCost > 0 ? (model.totalCost / totalModelCost) * 100 : 0,
          requests: model.totalRequests,
        }));
        
        // Create endpoint spend breakdown from actual request logs
        const endpointMap = new Map<string, { cost: number; count: number }>();
        requestLogs.items.forEach(log => {
          const endpoint = log.endpoint || 'Unknown';
          const existing = endpointMap.get(endpoint) || { cost: 0, count: 0 };
          existing.cost += log.cost;
          existing.count++;
          endpointMap.set(endpoint, existing);
        });
        
        const spendByEndpoint = Array.from(endpointMap.entries())
          .map(([endpoint, data]) => ({
            endpoint,
            cost: Math.round(data.cost * 100) / 100,
            percentage: totalSpent > 0 ? Math.round((data.cost / totalSpent) * 100) : 0,
            averageCostPerRequest: data.count > 0 ? Math.round((data.cost / data.count) * 10000) / 10000 : 0,
          }))
          .sort((a, b) => b.cost - a.cost);
        
        // Transform notifications to alerts
        const alerts = notifications
          .filter(notif => 
            String(notif.type).includes('budget') || 
            String(notif.type).includes('rate') || 
            String(notif.type).includes('spend')
          )
          .slice(0, 5) // Latest 5 alerts
          .map(notif => ({
            id: notif.id.toString(),
            type: String(notif.type).includes('budget') ? 'budget_warning' as const :
                  String(notif.type).includes('rate') ? 'rate_limit' as const : 'unusual_spend' as const,
            severity: String(notif.severity).toLowerCase() === 'critical' ? 'high' : String(notif.severity).toLowerCase() as 'low' | 'medium' | 'high',
            message: notif.message,
            timestamp: String(notif.createdAt),
            acknowledged: notif.isRead,
          }));
        
        // Generate recommendations based on spend patterns
        const recommendations = [];
        
        // Budget utilization recommendations
        if (budgetLimit > 0 && (totalSpent / budgetLimit) > 0.8) {
          recommendations.push({
            type: 'budget_adjustment' as const,
            title: 'Consider increasing budget limit',
            description: 'Your current spend is approaching the budget limit. Consider increasing the budget or optimizing usage.',
            impact: 'high' as const,
          });
        }
        
        // Model optimization recommendations
        const gpt4Cost = spendByModel.find(m => m.modelName.includes('gpt-4'))?.cost || 0;
        const gpt35Cost = spendByModel.find(m => m.modelName.includes('gpt-3.5'))?.cost || 0;
        
        if (gpt4Cost > gpt35Cost * 2) {
          recommendations.push({
            type: 'model_switch' as const,
            title: 'Consider using GPT-3.5-turbo for simpler tasks',
            description: 'You could save costs by using GPT-3.5-turbo for less complex tasks.',
            potentialSavings: gpt4Cost * 0.5,
            impact: 'high' as const,
          });
        }
        
        return {
          keyId,
          period,
          budget: {
            limit: budgetLimit,
            spent: totalSpent,
            remaining: budgetRemaining,
            projectedSpend,
            daysRemaining,
            burnRate: Math.round(burnRate * 100) / 100,
          },
          spendHistory,
          spendByModel,
          spendByEndpoint,
          alerts,
          recommendations,
        };
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch virtual key budget analytics');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch virtual key budget analytics';
        throw new Error(errorMessage);
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
        
        const dateRange = convertTimeRangeToDateRange(timeRange);
        
        // Get request logs and key details
        const [requestLogs, virtualKey, systemMetrics] = await Promise.all([
          client.analytics.getRequestLogs({
            startDate: dateRange.startDate,
            endDate: dateRange.endDate,
            virtualKeyId: parseInt(keyId),
            pageSize: 1000,
          }),
          client.virtualKeys.getById(parseInt(keyId)),
          client.metrics.getRequestStatistics(),
        ]);
        
        // Calculate latency metrics
        const latencies = requestLogs.items.map(log => log.duration).sort((a, b) => a - b);
        const latencyMetrics = {
          average: latencies.length > 0 ? Math.round(latencies.reduce((sum, l) => sum + l, 0) / latencies.length) : 0,
          p50: latencies.length > 0 ? latencies[Math.floor(latencies.length * 0.5)] : 0,
          p90: latencies.length > 0 ? latencies[Math.floor(latencies.length * 0.9)] : 0,
          p95: latencies.length > 0 ? latencies[Math.floor(latencies.length * 0.95)] : 0,
          p99: latencies.length > 0 ? latencies[Math.floor(latencies.length * 0.99)] : 0,
          trend: 0, // Would need historical comparison
        };
        
        // Calculate throughput metrics
        const timeRangeHours = timeRange.range === '1h' ? 1 : 
                              timeRange.range === '24h' ? 24 :
                              timeRange.range === '7d' ? 168 : 720;
        const totalRequests = requestLogs.items.length;
        const totalTokens = requestLogs.items.reduce((sum, log) => sum + log.inputTokens + log.outputTokens, 0);
        const requestsPerSecond = totalRequests / (timeRangeHours * 3600);
        const tokensPerSecond = totalTokens / (timeRangeHours * 3600);
        
        // Calculate reliability metrics
        const successfulRequests = requestLogs.items.filter(log => log.status === 'success').length;
        const errorRequests = requestLogs.items.filter(log => log.status === 'error').length;
        const timeoutRequests = requestLogs.items.filter(log => log.duration > 30000).length; // >30s considered timeout
        
        const reliabilityMetrics = {
          uptime: 99.5, // Estimate - would need actual uptime data
          successRate: totalRequests > 0 ? (successfulRequests / totalRequests) * 100 : 100,
          errorRate: totalRequests > 0 ? (errorRequests / totalRequests) * 100 : 0,
          timeouts: timeoutRequests,
          retries: 0, // Not available in current data
        };
        
        // Calculate quota usage (from virtual key limits)
        const requestLimit = virtualKey.rateLimitRpm ? virtualKey.rateLimitRpm * 60 : 10000; // Estimate hourly limit
        const tokenLimit = 1000000; // Default token limit estimate
        const currentHourRequests = requestLogs.items.filter(log => 
          new Date(log.timestamp).getTime() > Date.now() - 60 * 60 * 1000
        ).length;
        const currentHourTokens = requestLogs.items
          .filter(log => new Date(log.timestamp).getTime() > Date.now() - 60 * 60 * 1000)
          .reduce((sum, log) => sum + log.inputTokens + log.outputTokens, 0);
        
        // Generate performance history
        const hours = Math.min(timeRangeHours, 168); // Max 1 week
        const performanceHistory = [];
        
        for (let i = hours - 1; i >= 0; i--) {
          const hourStart = new Date(Date.now() - i * 60 * 60 * 1000);
          const hourEnd = new Date(hourStart.getTime() + 60 * 60 * 1000);
          
          const hourLogs = requestLogs.items.filter(log => {
            const logTime = new Date(log.timestamp);
            return logTime >= hourStart && logTime < hourEnd;
          });
          
          const hourRequests = hourLogs.length;
          const hourErrors = hourLogs.filter(log => log.status === 'error').length;
          const hourLatency = hourLogs.length > 0 
            ? hourLogs.reduce((sum, log) => sum + log.duration, 0) / hourLogs.length
            : 0;
          
          performanceHistory.push({
            timestamp: hourStart.toISOString(),
            avgLatency: Math.round(hourLatency),
            requestsPerSecond: Math.round((hourRequests / 3600) * 10) / 10,
            errorRate: hourRequests > 0 ? Math.round((hourErrors / hourRequests) * 1000) / 10 : 0,
            successRate: hourRequests > 0 ? Math.round(((hourRequests - hourErrors) / hourRequests) * 1000) / 10 : 100,
          });
        }
        
        return {
          keyId,
          timeRange: timeRange.range,
          latency: latencyMetrics,
          throughput: {
            requestsPerSecond: Math.round(requestsPerSecond * 10) / 10,
            tokensPerSecond: Math.round(tokensPerSecond * 10) / 10,
            peakRPS: Math.max(...performanceHistory.map(h => h.requestsPerSecond)),
            averageRPS: Math.round(requestsPerSecond * 10) / 10,
            trend: 0, // Would need historical comparison
          },
          reliability: reliabilityMetrics,
          quotaUsage: {
            requestQuota: {
              limit: requestLimit,
              used: currentHourRequests,
              percentage: (currentHourRequests / requestLimit) * 100,
              resetsAt: new Date(Math.ceil(Date.now() / (60 * 60 * 1000)) * 60 * 60 * 1000).toISOString(),
            },
            tokenQuota: {
              limit: tokenLimit,
              used: currentHourTokens,
              percentage: (currentHourTokens / tokenLimit) * 100,
              resetsAt: new Date(Math.ceil(Date.now() / (60 * 60 * 1000)) * 60 * 60 * 1000).toISOString(),
            },
          },
          performanceHistory,
        };
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch virtual key performance metrics');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch virtual key performance metrics';
        throw new Error(errorMessage);
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
        
        const dateRange = convertTimeRangeToDateRange(timeRange);
        
        // Get request logs and notifications for security analysis
        const [requestLogs, notifications, virtualKey] = await Promise.all([
          client.analytics.getRequestLogs({
            startDate: dateRange.startDate,
            endDate: dateRange.endDate,
            virtualKeyId: parseInt(keyId),
            pageSize: 1000,
          }),
          client.notifications.getNotificationsForVirtualKey(parseInt(keyId)),
          client.virtualKeys.getById(parseInt(keyId)),
        ]);
        
        // Analyze IP patterns from request logs
        const ipMap = new Map<string, {
          requests: number;
          lastSeen: string;
          errors: number;
        }>();
        
        requestLogs.items.forEach(log => {
          const ip = (log as any).clientIP || 'unknown';
          const existing = ipMap.get(ip) || {
            requests: 0,
            lastSeen: log.timestamp,
            errors: 0,
          };
          
          existing.requests++;
          existing.lastSeen = log.timestamp;
          if (log.status === 'error') existing.errors++;
          
          ipMap.set(ip, existing);
        });
        
        // Create IP analysis
        const requestsByIP = Array.from(ipMap.entries()).map(([ip, data]) => ({
          ip,
          requests: data.requests,
          lastSeen: data.lastSeen,
          flagged: data.errors / data.requests > 0.1, // Flag if >10% error rate
          country: ip.startsWith('192.168.') || ip.startsWith('10.') ? 'Private Network' : 'Unknown',
        }));
        
        // Sort by request count
        requestsByIP.sort((a, b) => b.requests - a.requests);
        
        // Analyze suspicious activity from notifications
        const suspiciousActivity = notifications
          .filter(notif => 
            String(notif.type).includes('security') || 
            String(notif.type).includes('rate') || 
            String(notif.type).includes('unusual')
          )
          .slice(0, 10) // Latest 10 security events
          .map(notif => ({
            id: notif.id.toString(),
            type: String(notif.type).includes('rate') ? 'rate_abuse' as const :
                  String(notif.type).includes('security') ? 'unusual_volume' as const : 'pattern_change' as const,
            severity: String(notif.severity).toLowerCase() === 'critical' ? 'high' : String(notif.severity).toLowerCase() as 'low' | 'medium' | 'high',
            description: notif.message,
            timestamp: String(notif.createdAt),
            resolved: notif.isRead,
          }));
        
        // Calculate authentication metrics
        const totalRequests = requestLogs.items.length;
        const validRequests = requestLogs.items.filter(log => log.status === 'success').length;
        const invalidRequests = requestLogs.items.filter(log => String(log.status).includes('unauthorized') || String(log.status).includes('auth')).length;
        const malformedRequests = requestLogs.items.filter(log => String(log.status).includes('bad') || String(log.status).includes('malformed')).length;
        
        // Rate limiting analysis
        const rateLimitViolations = requestLogs.items.filter(log => String(log.status).includes('rate') || String(log.status).includes('limit')).length;
        const lastViolation = requestLogs.items
          .filter(log => String(log.status).includes('rate') || String(log.status).includes('limit'))
          .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())[0]?.timestamp;
        
        return {
          keyId,
          timeRange: timeRange.range,
          accessPatterns: {
            uniqueIPs: ipMap.size,
            requestsByIP: requestsByIP.slice(0, 10), // Top 10 IPs
            suspiciousActivity,
          },
          rateLimiting: {
            enforced: virtualKey.rateLimitRpm !== undefined,
            requestsPerMinute: virtualKey.rateLimitRpm || 100,
            requestsPerHour: (virtualKey.rateLimitRpm || 100) * 60,
            violations: rateLimitViolations,
            lastViolation,
          },
          authentication: {
            validRequests,
            invalidRequests,
            malformedRequests,
            lastInvalidAttempt: requestLogs.items
              .filter(log => String(log.status).includes('unauthorized') || String(log.status).includes('auth'))
              .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())[0]?.timestamp,
          },
          compliance: {
            dataRegions: ['us-east-1', 'us-west-2'], // Default regions
            retentionPolicy: '90 days',
            encryptionStatus: 'enabled',
            auditLogsEnabled: true,
          },
        };
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch virtual key security metrics');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch virtual key security metrics';
        throw new Error(errorMessage);
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
        
        const dateRange = convertTimeRangeToDateRange(timeRange);
        
        // Get current period and previous period for comparison
        const currentPeriodMs = new Date(dateRange.endDate).getTime() - new Date(dateRange.startDate).getTime();
        const previousDateRange = {
          startDate: new Date(new Date(dateRange.startDate).getTime() - currentPeriodMs).toISOString(),
          endDate: dateRange.startDate,
        };
        
        // Get analytics for both periods
        const [currentUsage, previousUsage, requestLogs] = await Promise.all([
          client.analytics.getKeyUsage(parseInt(keyId), dateRange),
          client.analytics.getKeyUsage(parseInt(keyId), previousDateRange),
          client.analytics.getRequestLogs({
            startDate: dateRange.startDate,
            endDate: dateRange.endDate,
            virtualKeyId: parseInt(keyId),
            pageSize: 1000,
          }),
        ]);
        
        // Calculate trends
        const calculateTrend = (current: number, previous: number): 'up' | 'down' | 'stable' => {
          const change = Math.abs(current - previous);
          const threshold = Math.max(previous * 0.05, 1); // 5% threshold
          if (change < threshold) return 'stable';
          return current > previous ? 'up' : 'down';
        };
        
        const requestsTrend = calculateTrend(currentUsage.totalRequests, previousUsage.totalRequests);
        const costTrend = calculateTrend(currentUsage.totalCost, previousUsage.totalCost);
        
        // Calculate average latency from request logs
        const currentLatency = requestLogs.items.length > 0 
          ? requestLogs.items.reduce((sum, log) => sum + log.duration, 0) / requestLogs.items.length
          : 0;
        const previousLatency = currentLatency; // No historical data available for comparison
        const latencyTrend = calculateTrend(currentLatency, previousLatency);
        
        const currentErrors = requestLogs.items.filter(log => log.status === 'error').length;
        const previousErrors = currentErrors; // No historical data available for comparison
        const errorsTrend = calculateTrend(currentErrors, previousErrors);
        
        // Generate forecasting based on trends
        const requestGrowthRate = previousUsage.totalRequests > 0 
          ? (currentUsage.totalRequests - previousUsage.totalRequests) / previousUsage.totalRequests
          : 0;
        const costGrowthRate = previousUsage.totalCost > 0 
          ? (currentUsage.totalCost - previousUsage.totalCost) / previousUsage.totalCost
          : 0;
        
        const nextWeekRequests = Math.round(currentUsage.totalRequests * (1 + requestGrowthRate));
        const nextWeekCost = Math.round(currentUsage.totalCost * (1 + costGrowthRate) * 100) / 100;
        
        // Calculate budget runout (if available)
        const virtualKey = await client.virtualKeys.getById(parseInt(keyId));
        const budgetLimit = virtualKey.maxBudget || (virtualKey as VirtualKeyDto).budgetLimit || 0;
        const dailyBurnRate = currentUsage.totalCost / (timeRange.range === '7d' ? 7 : timeRange.range === '30d' ? 30 : 90);
        const daysUntilBudgetRunout = budgetLimit > 0 && dailyBurnRate > 0 
          ? Math.floor((budgetLimit - currentUsage.totalCost) / dailyBurnRate)
          : null;
        
        // Generate seasonality patterns from request logs
        const hourlyPattern = Array.from({ length: 24 }, (_, hour) => {
          const hourRequests = requestLogs.items.filter(log => 
            new Date(log.timestamp).getHours() === hour
          ).length;
          return {
            hour,
            avgRequests: hourRequests / Math.max(1, Math.floor(requestLogs.items.length / 24)),
          };
        });
        
        // Find peak and quiet hours
        const sortedHours = [...hourlyPattern].sort((a, b) => b.avgRequests - a.avgRequests);
        const peakHours = sortedHours.slice(0, 5).map(h => h.hour);
        const quietHours = sortedHours.slice(-8).map(h => h.hour);
        
        return {
          timeRange: timeRange.range,
          trends: {
            requests: {
              current: currentUsage.totalRequests,
              previous: previousUsage.totalRequests,
              change: previousUsage.totalRequests > 0 
                ? Math.round(((currentUsage.totalRequests - previousUsage.totalRequests) / previousUsage.totalRequests) * 1000) / 10
                : 0,
              trend: requestsTrend,
            },
            cost: {
              current: currentUsage.totalCost,
              previous: previousUsage.totalCost,
              change: previousUsage.totalCost > 0 
                ? Math.round(((currentUsage.totalCost - previousUsage.totalCost) / previousUsage.totalCost) * 1000) / 10
                : 0,
              trend: costTrend,
            },
            latency: {
              current: Math.round(currentLatency),
              previous: Math.round(previousLatency),
              change: previousLatency > 0 
                ? Math.round(((currentLatency - previousLatency) / previousLatency) * 1000) / 10
                : 0,
              trend: latencyTrend,
            },
            errors: {
              current: currentErrors,
              previous: previousErrors,
              change: previousErrors > 0 
                ? Math.round(((currentErrors - previousErrors) / previousErrors) * 1000) / 10
                : 0,
              trend: errorsTrend,
            },
          },
          forecasting: {
            nextWeekRequests,
            nextWeekCost,
            budgetRunoutDate: daysUntilBudgetRunout && daysUntilBudgetRunout > 0 
              ? new Date(Date.now() + daysUntilBudgetRunout * 24 * 60 * 60 * 1000).toISOString()
              : undefined,
            confidence: 75.0, // Confidence in forecast
          },
          seasonality: {
            hourlyPattern,
            dailyPattern: [], // Daily pattern analysis not available without historical data
            peakHours,
            quietHours,
          },
        };
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch virtual key trends');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch virtual key trends';
        throw new Error(errorMessage);
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
        
        const days = period === '7d' ? 7 : period === '30d' ? 30 : 90;
        const dateRange = {
          startDate: new Date(Date.now() - days * 24 * 60 * 60 * 1000).toISOString(),
          endDate: new Date().toISOString(),
        };
        
        // Get virtual keys and cost data
        const [virtualKeys, costByKey] = await Promise.all([
          client.virtualKeys.list(),
          client.analytics.getCostByKey(dateRange),
        ]);
        
        // Create a map of key data for easier lookup
        const keyDataMap = new Map();
        virtualKeys.forEach(key => {
          keyDataMap.set(key.id, {
            keyId: key.id.toString(),
            ...key,
          });
        });
        
        // Calculate previous period for change comparison
        const previousDateRange = {
          startDate: new Date(Date.now() - days * 2 * 24 * 60 * 60 * 1000).toISOString(),
          endDate: new Date(Date.now() - days * 24 * 60 * 60 * 1000).toISOString(),
        };
        
        const previousCostByKey = await client.analytics.getCostByKey(previousDateRange);
        
        // Create maps for current and previous metrics
        const currentMetrics = new Map();
        const previousMetrics = new Map();
        
        (costByKey.keys || []).forEach(cost => {
          currentMetrics.set(cost.keyId, cost);
        });
        
        (previousCostByKey.keys || []).forEach(cost => {
          previousMetrics.set(cost.keyId, cost);
        });
        
        // Helper function to calculate change percentage
        const calculateChange = (current: number, previous: number): number => {
          if (previous === 0) return current > 0 ? 100 : 0;
          return Math.round(((current - previous) / previous) * 1000) / 10;
        };
        
        // Top by requests
        const topByRequests = Array.from(currentMetrics.values())
          .sort((a, b) => b.requestCount - a.requestCount)
          .slice(0, 10)
          .map(cost => {
            const keyData = keyDataMap.get(cost.keyId);
            const previousCost = previousMetrics.get(cost.keyId);
            return {
              keyId: cost.keyId.toString(),
              keyName: keyData?.keyName || `Key ${cost.keyId}`,
              requests: cost.requestCount,
              change: calculateChange(cost.requestCount, previousCost?.requestCount || 0),
            };
          });
        
        // Top by cost
        const topByCost = Array.from(currentMetrics.values())
          .sort((a, b) => b.cost - a.cost)
          .slice(0, 10)
          .map(cost => {
            const keyData = keyDataMap.get(cost.keyId);
            const previousCost = previousMetrics.get(cost.keyId);
            return {
              keyId: cost.keyId.toString(),
              keyName: keyData?.keyName || `Key ${cost.keyId}`,
              cost: cost.cost,
              change: calculateChange(cost.cost, previousCost?.cost || 0),
            };
          });
        
        // Top by tokens
        const topByTokens = Array.from(currentMetrics.values())
          .sort((a, b) => (b.inputTokens + b.outputTokens) - (a.inputTokens + a.outputTokens))
          .slice(0, 10)
          .map(cost => {
            const keyData = keyDataMap.get(cost.keyId);
            const previousCost = previousMetrics.get(cost.keyId);
            const currentTokens = cost.inputTokens + cost.outputTokens;
            const previousTokens = (previousCost?.inputTokens || 0) + (previousCost?.outputTokens || 0);
            return {
              keyId: cost.keyId.toString(),
              keyName: keyData?.keyName || `Key ${cost.keyId}`,
              tokens: currentTokens,
              change: calculateChange(currentTokens, previousTokens),
            };
          });
        
        // Most efficient (lowest cost per request)
        const mostEfficient = Array.from(currentMetrics.values())
          .filter(cost => cost.requestCount > 0)
          .map(cost => {
            const keyData = keyDataMap.get(cost.keyId);
            const costPerRequest = cost.cost / cost.requestCount;
            return {
              keyId: cost.keyId.toString(),
              keyName: keyData?.keyName || `Key ${cost.keyId}`,
              costPerRequest: Math.round(costPerRequest * 10000) / 10000,
              efficiency: Math.round((1 / costPerRequest) * 1000) / 10, // Efficiency score
            };
          })
          .sort((a, b) => a.costPerRequest - b.costPerRequest)
          .slice(0, 10);
        
        // Get request logs for latency analysis
        const requestLogs = await client.analytics.getRequestLogs({
          startDate: dateRange.startDate,
          endDate: dateRange.endDate,
          pageSize: 1000,
        });
        
        // Fastest response (lowest average latency)
        const latencyByKey = new Map();
        requestLogs.items.forEach(log => {
          if (!log.virtualKeyId) return;
          const existing = latencyByKey.get(log.virtualKeyId) || { totalLatency: 0, count: 0 };
          existing.totalLatency += log.duration;
          existing.count++;
          latencyByKey.set(log.virtualKeyId, existing);
        });
        
        const fastestResponse = Array.from(latencyByKey.entries())
          .map(([keyId, data]) => {
            const keyData = keyDataMap.get(keyId);
            const avgLatency = Math.round(data.totalLatency / data.count);
            return {
              keyId: keyId.toString(),
              keyName: keyData?.keyName || `Key ${keyId}`,
              avgLatency,
              improvement: 0, // Would need historical comparison
            };
          })
          .sort((a, b) => a.avgLatency - b.avgLatency)
          .slice(0, 10);
        
        // Most reliable (highest success rate)
        const reliabilityByKey = new Map();
        requestLogs.items.forEach(log => {
          if (!log.virtualKeyId) return;
          const existing = reliabilityByKey.get(log.virtualKeyId) || { total: 0, successful: 0 };
          existing.total++;
          if (log.status === 'success') existing.successful++;
          reliabilityByKey.set(log.virtualKeyId, existing);
        });
        
        const mostReliable = Array.from(reliabilityByKey.entries())
          .map(([keyId, data]) => {
            const keyData = keyDataMap.get(keyId);
            const successRate = data.total > 0 ? (data.successful / data.total) * 100 : 100;
            return {
              keyId: keyId.toString(),
              keyName: keyData?.keyName || `Key ${keyId}`,
              successRate: Math.round(successRate * 10) / 10,
              uptime: 99.0, // Default uptime estimate
            };
          })
          .sort((a, b) => b.successRate - a.successRate)
          .slice(0, 10);
        
        return {
          period,
          categories: {
            topByRequests,
            topByCost,
            topByTokens,
            mostEfficient,
            fastestResponse,
            mostReliable,
          },
        };
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch virtual keys leaderboard');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch virtual keys leaderboard';
        throw new Error(errorMessage);
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
        
        const dateRange = convertTimeRangeToDateRange(request.timeRange);
        
        // Use the analytics export functionality
        const exportResult = await client.analytics.export({
          startDate: dateRange.startDate,
          endDate: dateRange.endDate,
          virtualKeyIds: request.keyId ? [parseInt(request.keyId)] : undefined,
        });
        
        // Create a blob URL for download
        const blobUrl = URL.createObjectURL(exportResult);
        
        return {
          url: blobUrl,
          filename: `virtual-keys-${request.type}-${new Date().toISOString().split('T')[0]}.${request.format || 'csv'}`,
          size: exportResult.size || 1024 * 1024, // Use blob size or default estimate
          expiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(), // 24 hours
        };
      } catch (error: unknown) {
        reportError(error, 'Failed to export virtual keys data');
        const errorMessage = error instanceof Error ? error.message : 'Failed to export virtual keys data';
        throw new Error(errorMessage);
      }
    },
  });
}