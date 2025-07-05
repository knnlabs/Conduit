'use client';

import type {
  DateRange,
  CostByModelResponse,
  CostByKeyResponse,
  UsageMetricsDto,
  VirtualKeyDto,
} from '@/types/sdk-extensions';

// Common time range filter used across all analytics hooks
export interface TimeRangeFilter {
  range: '1h' | '24h' | '7d' | '30d' | '90d' | 'custom';
  startDate?: string;
  endDate?: string;
}

// Common export request types
export interface BaseExportRequest {
  timeRange: TimeRangeFilter;
  format?: 'csv' | 'json' | 'xlsx' | 'pdf';
}

export interface ExportResponse {
  filename: string;
  url: string;
  size: number;
  expiresAt?: string;
}

// Cost Analytics Types
export interface CostTrendData {
  date: string;
  spend: number;
  requests: number;
  tokens: number;
  averageCostPerRequest: number;
}

export interface ProviderCostData {
  provider: string;
  spend: number;
  requests: number;
  percentage: number;
  models: string[];
  averageCost: number;
}

export interface ModelCostData {
  model: string;
  provider: string;
  spend: number;
  requests: number;
  tokens: number;
  averageCostPerRequest: number;
  averageCostPerToken: number;
}

export interface VirtualKeyCostData {
  keyId: string;
  keyName: string;
  spend: number;
  budget: number;
  requests: number;
  usagePercentage: number;
  isOverBudget: boolean;
  lastActivity: string;
  topModels: string[];
}

export interface CostSummary {
  totalSpend: number;
  totalBudget: number;
  totalRequests: number;
  totalTokens: number;
  activeVirtualKeys: number;
  averageCostPerRequest: number;
  averageCostPerToken: number;
  spendTrend: number; // percentage change from previous period
  requestTrend: number;
}

// Usage Analytics Types
export interface UsageMetrics {
  totalRequests: number;
  totalTokens: number;
  totalUsers: number;
  averageLatency: number;
  errorRate: number;
  requestsPerSecond: number;
  tokensPerRequest: number;
  successRate: number;
  uniqueVirtualKeys: number;
  activeProviders: number;
  requestsTrend: number; // percentage change
  tokensTrend: number;
  errorsTrend: number;
  latencyTrend: number;
}

export interface RequestVolumeData {
  timestamp: string;
  requests: number;
  successfulRequests: number;
  failedRequests: number;
  averageLatency: number;
  tokensProcessed: number;
}

export interface TokenUsageData {
  timestamp: string;
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  cost: number;
  averageTokensPerRequest: number;
}

export interface ErrorAnalyticsData {
  errorType: string;
  count: number;
  percentage: number;
  lastOccurrence: string;
  affectedEndpoints: string[];
  examples: {
    message: string;
    timestamp: string;
    virtualKey?: string;
    provider?: string;
  }[];
}

export interface LatencyMetrics {
  endpoint: string;
  averageLatency: number;
  p50: number;
  p90: number;
  p95: number;
  p99: number;
  requestCount: number;
  slowestRequests: {
    latency: number;
    timestamp: string;
    virtualKey?: string;
    model?: string;
  }[];
}

export interface UserAnalytics {
  virtualKeyId: string;
  virtualKeyName: string;
  totalRequests: number;
  totalTokens: number;
  totalCost: number;
  averageLatency: number;
  errorRate: number;
  lastActivity: string;
  topModels: string[];
  topEndpoints: string[];
  requestsOverTime: { timestamp: string; requests: number }[];
}

export interface EndpointUsage {
  endpoint: string;
  method: string;
  totalRequests: number;
  averageLatency: number;
  errorRate: number;
  successRate: number;
  tokensPerRequest: number;
  costPerRequest: number;
  popularModels: string[];
  requestsOverTime: { timestamp: string; requests: number }[];
}

// Virtual Keys Analytics Types
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

// Re-export SDK types that are commonly used
export type { DateRange, CostByModelResponse, CostByKeyResponse, UsageMetricsDto, VirtualKeyDto };