export interface VirtualKeyDetail {
  keyId: string;
  keyName: string;
  status: 'active' | 'expired' | 'disabled';
  usage: {
    requests: number;
    requestsChange: number; // NEW: percentage change from previous period
    tokens: number;
    tokensChange: number;   // NEW: percentage change from previous period
    cost: number;
    costChange: number;     // NEW: percentage change from previous period
    lastUsed: string;
    errorRate: number;      // NEW: error rate percentage
  };
  quota: {
    limit: number;
    used: number;
    remaining: number;
    percentage: number;
    resetDate?: string;
  };
  performance: {
    averageLatency: number;
    errorRate: number;
    successRate: number;
  };
  trends: {
    dailyChange: number; // percentage
    weeklyChange: number; // percentage
  };
  endpointBreakdown: {      // NEW: endpoint usage data
    path: string;
    requests: number;
    avgDuration: number;
    errorRate: number;
  }[];
  timeSeries?: {            // NEW: per-key time series
    timestamp: string;
    requests: number;
    tokens: number;
    cost: number;
    errorRate: number;
  }[];
  tokenLimit?: number;      // NEW: token consumption limit (from metadata)
  tokenPeriod?: string;     // NEW: token limit period (from metadata)
}

export interface QuotaAlert {
  keyId: string;
  keyName: string;
  type: 'approaching_limit' | 'exceeded_limit' | 'unusual_activity';
  severity: 'info' | 'warning' | 'critical';
  message: string;
  threshold?: number;
  currentUsage?: number;
}