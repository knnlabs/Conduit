// Analytics type definitions
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

export interface TimeRangeFilter {
  range: '1h' | '24h' | '7d' | '30d' | '90d' | 'custom';
  startDate?: string;
  endDate?: string;
}

// Usage Analytics Types
export interface UsageSummary {
  totalRequests: number;
  totalTokens: number;
  totalErrors: number;
  successRate: number;
  averageLatency: number;
  activeUsers: number;
  activeEndpoints: number;
  requestTrend: number;
  tokenTrend: number;
}

export interface UsageMetrics {
  timestamp: string;
  requests: number;
  tokens: number;
  errors: number;
  latency: number;
  successRate: number;
  [key: string]: unknown;
}

export interface RequestVolumeData {
  timestamp: string;
  count: number;
  successful: number;
  failed: number;
  rate: number;
}

export interface TokenUsageData {
  timestamp: string;
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  averageTokensPerRequest: number;
}

export interface ErrorAnalyticsData {
  errorType: string;
  count: number;
  percentage: number;
  trend: number;
  examples: Array<{
    timestamp: string;
    message: string;
    endpoint: string;
  }>;
}

export interface LatencyMetrics {
  p50: number;
  p75: number;
  p90: number;
  p95: number;
  p99: number;
  average: number;
  min: number;
  max: number;
}

export interface UserAnalytics {
  userId: string;
  requests: number;
  tokens: number;
  errors: number;
  cost: number;
  lastActive: string;
  topEndpoints: string[];
}

export interface EndpointUsage {
  endpoint: string;
  method: string;
  requests: number;
  errors: number;
  averageLatency: number;
  successRate: number;
  topUsers: string[];
}

// Virtual Key Analytics Types
export interface VirtualKeyOverview {
  keyId: string;
  name: string;
  status: 'active' | 'inactive' | 'suspended';
  created: string;
  lastUsed: string;
  requests: number;
  tokens: number;
  cost: number;
  budget: number;
  budgetUsage: number;
  rateLimitHits: number;
  tags: string[];
}

export interface VirtualKeyUsageMetrics {
  timestamp: string;
  requests: number;
  tokens: number;
  cost: number;
  errors: number;
  latency: number;
  models: Record<string, number>;
  endpoints: Record<string, number>;
}

export interface VirtualKeyBudgetAnalytics {
  currentSpend: number;
  budgetLimit: number;
  projectedSpend: number;
  daysUntilLimit: number;
  spendRate: number;
  alerts: Array<{
    type: 'warning' | 'critical';
    message: string;
    threshold: number;
  }>;
  recommendations: string[];
}

export interface VirtualKeyPerformanceMetrics {
  latency: LatencyMetrics;
  throughput: {
    requestsPerSecond: number;
    tokensPerSecond: number;
    peakThroughput: number;
  };
  reliability: {
    uptime: number;
    errorRate: number;
    successRate: number;
  };
  capacity: {
    currentLoad: number;
    peakLoad: number;
    headroom: number;
  };
}

export interface VirtualKeySecurityMetrics {
  accessPatterns: {
    uniqueIPs: number;
    uniqueUserAgents: number;
    topIPs: Array<{ ip: string; count: number }>;
    suspiciousActivity: number;
  };
  rateLimiting: {
    hitsToday: number;
    hitsTrend: number;
    topOffenders: Array<{ identifier: string; hits: number }>;
  };
  authentication: {
    failedAttempts: number;
    successfulAttempts: number;
    suspiciousPatterns: string[];
  };
}

export interface VirtualKeyTrends {
  timestamp: string;
  requests: number;
  cost: number;
  tokens: number;
  errors: number;
  prediction: {
    nextPeriodRequests: number;
    nextPeriodCost: number;
    confidence: number;
  };
}

export interface VirtualKeyLeaderboard {
  metric: 'requests' | 'cost' | 'tokens' | 'efficiency';
  entries: Array<{
    keyId: string;
    keyName: string;
    value: number;
    rank: number;
    change: number;
    percentile: number;
  }>;
}

export interface VirtualKeyComparison {
  keys: string[];
  metrics: {
    requests: Record<string, number>;
    cost: Record<string, number>;
    tokens: Record<string, number>;
    errors: Record<string, number>;
    latency: Record<string, number>;
    efficiency: Record<string, number>;
  };
  rankings: Record<string, Record<string, number>>;
}