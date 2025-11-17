import { useCallback } from 'react';

export interface UsageMetrics {
  totalRequests: number;
  totalCost: number;
  totalTokens: number;
  activeVirtualKeys: number;
  requestsChange: number;
  costChange: number;
  tokensChange: number;
  virtualKeysChange: number;
}

export interface TimeSeriesData {
  timestamp: string;
  requests: number;
  cost: number;
  tokens: number;
}

export interface ProviderUsage {
  provider: string;
  requests: number;
  cost: number;
  tokens: number;
  percentage: number;
}

export interface ModelUsage {
  model: string;
  provider: string;
  requests: number;
  cost: number;
  tokens: number;
}

export interface VirtualKeyUsage {
  keyName: string;
  requests: number;
  cost: number;
  tokens: number;
  lastUsed: string;
}

export interface EndpointUsage {
  endpoint: string;
  requests: number;
  avgDuration: number;
  errorRate: number;
}

export interface UsageAnalyticsResponse {
  metrics: UsageMetrics;
  timeSeries: TimeSeriesData[];
  providerUsage: ProviderUsage[];
  modelUsage: ModelUsage[];
  virtualKeyUsage: VirtualKeyUsage[];
  endpointUsage: EndpointUsage[];
}

export function useFetchAnalytics() {
  const fetchAnalytics = useCallback(async (timeRange: string): Promise<UsageAnalyticsResponse> => {
    const response = await fetch(`/api/usage-analytics?range=${timeRange}`);
    if (!response.ok) {
      throw new Error('Failed to fetch analytics');
    }
    const data = await response.json() as UsageAnalyticsResponse;
    return data;
  }, []);

  return { fetchAnalytics };
}

export function useExportAnalytics() {
  const handleExport = useCallback(async (timeRange: string) => {
    try {
      const response = await fetch(`/api/usage-analytics/export?range=${timeRange}`);
      if (!response.ok) throw new Error('Export failed');

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `usage-analytics-${timeRange}-${new Date().toISOString()}.csv`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
    } catch (error) {
      console.error('Export failed:', error);
      throw error;
    }
  }, []);

  return { handleExport };
}