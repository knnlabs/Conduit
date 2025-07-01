'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';

// Query key factory for Usage Analytics API
export const usageAnalyticsApiKeys = {
  all: ['usage-analytics-api'] as const,
  metrics: () => [...usageAnalyticsApiKeys.all, 'metrics'] as const,
  requests: () => [...usageAnalyticsApiKeys.all, 'requests'] as const,
  tokens: () => [...usageAnalyticsApiKeys.all, 'tokens'] as const,
  errors: () => [...usageAnalyticsApiKeys.all, 'errors'] as const,
  latency: () => [...usageAnalyticsApiKeys.all, 'latency'] as const,
  users: () => [...usageAnalyticsApiKeys.all, 'users'] as const,
  endpoints: () => [...usageAnalyticsApiKeys.all, 'endpoints'] as const,
} as const;

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

export interface TimeRangeFilter {
  range: '1h' | '24h' | '7d' | '30d' | '90d' | 'custom';
  startDate?: string;
  endDate?: string;
}

// Usage Metrics Hook
export function useUsageMetrics(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...usageAnalyticsApiKeys.metrics(), timeRange],
    queryFn: async (): Promise<UsageMetrics> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.analytics.getUsageMetrics(timeRange);
        
        // Mock data for now
        const mockData: UsageMetrics = {
          totalRequests: 127456,
          totalTokens: 5847293,
          totalUsers: 23,
          averageLatency: 847,
          errorRate: 2.3,
          requestsPerSecond: 14.7,
          tokensPerRequest: 45.9,
          successRate: 97.7,
          uniqueVirtualKeys: 23,
          activeProviders: 4,
          requestsTrend: 15.2,
          tokensTrend: 18.7,
          errorsTrend: -12.4,
          latencyTrend: -5.3,
        };

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch usage metrics');
        throw new Error(error?.message || 'Failed to fetch usage metrics');
      }
    },
    staleTime: 30 * 1000, // 30 seconds
    refetchInterval: 60 * 1000, // 1 minute
  });
}

// Request Volume Analytics
export function useRequestVolumeAnalytics(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...usageAnalyticsApiKeys.requests(), timeRange],
    queryFn: async (): Promise<RequestVolumeData[]> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.analytics.getRequestVolume(timeRange);
        
        // Generate mock request volume data
        const generateMockData = (hours: number): RequestVolumeData[] => {
          const data: RequestVolumeData[] = [];
          const now = new Date();
          
          for (let i = hours - 1; i >= 0; i--) {
            const timestamp = new Date(now.getTime() - i * 60 * 60 * 1000);
            const baseRequests = 1200 + Math.random() * 800;
            const failedRequests = Math.floor(baseRequests * (0.01 + Math.random() * 0.04));
            const successfulRequests = Math.floor(baseRequests - failedRequests);
            const tokensProcessed = Math.floor(baseRequests * (40 + Math.random() * 30));
            
            data.push({
              timestamp: timestamp.toISOString(),
              requests: Math.floor(baseRequests),
              successfulRequests,
              failedRequests,
              averageLatency: Math.floor(500 + Math.random() * 1000),
              tokensProcessed,
            });
          }
          
          return data;
        };

        const hours = timeRange.range === '1h' ? 1 : 
                     timeRange.range === '24h' ? 24 :
                     timeRange.range === '7d' ? 24 * 7 : 
                     timeRange.range === '30d' ? 24 * 30 : 24;

        return generateMockData(hours);
      } catch (error: any) {
        reportError(error, 'Failed to fetch request volume analytics');
        throw new Error(error?.message || 'Failed to fetch request volume analytics');
      }
    },
    staleTime: 30 * 1000, // 30 seconds
  });
}

// Token Usage Analytics
export function useTokenUsageAnalytics(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...usageAnalyticsApiKeys.tokens(), timeRange],
    queryFn: async (): Promise<TokenUsageData[]> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.analytics.getTokenUsage(timeRange);
        
        // Generate mock token usage data
        const generateMockData = (hours: number): TokenUsageData[] => {
          const data: TokenUsageData[] = [];
          const now = new Date();
          
          for (let i = hours - 1; i >= 0; i--) {
            const timestamp = new Date(now.getTime() - i * 60 * 60 * 1000);
            const inputTokens = Math.floor(15000 + Math.random() * 10000);
            const outputTokens = Math.floor(25000 + Math.random() * 15000);
            const totalTokens = inputTokens + outputTokens;
            const cost = (totalTokens * 0.0001) + Math.random() * 5;
            const requests = 800 + Math.random() * 400;
            
            data.push({
              timestamp: timestamp.toISOString(),
              inputTokens,
              outputTokens,
              totalTokens,
              cost: Math.round(cost * 100) / 100,
              averageTokensPerRequest: Math.round((totalTokens / requests) * 10) / 10,
            });
          }
          
          return data;
        };

        const hours = timeRange.range === '1h' ? 1 : 
                     timeRange.range === '24h' ? 24 :
                     timeRange.range === '7d' ? 24 * 7 : 
                     timeRange.range === '30d' ? 24 * 30 : 24;

        return generateMockData(hours);
      } catch (error: any) {
        reportError(error, 'Failed to fetch token usage analytics');
        throw new Error(error?.message || 'Failed to fetch token usage analytics');
      }
    },
    staleTime: 30 * 1000, // 30 seconds
  });
}

// Error Analytics
export function useErrorAnalytics(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...usageAnalyticsApiKeys.errors(), timeRange],
    queryFn: async (): Promise<ErrorAnalyticsData[]> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.analytics.getErrorAnalytics(timeRange);
        
        // Mock error analytics data
        const mockData: ErrorAnalyticsData[] = [
          {
            errorType: 'Rate Limit Exceeded',
            count: 234,
            percentage: 45.2,
            lastOccurrence: new Date(Date.now() - 1000 * 60 * 15).toISOString(),
            affectedEndpoints: ['/v1/chat/completions', '/v1/images/generations'],
            examples: [
              {
                message: 'Rate limit exceeded for virtual key: vk_prod_001',
                timestamp: new Date(Date.now() - 1000 * 60 * 20).toISOString(),
                virtualKey: 'vk_prod_001',
                provider: 'OpenAI',
              },
              {
                message: 'Rate limit exceeded for virtual key: vk_dev_002',
                timestamp: new Date(Date.now() - 1000 * 60 * 45).toISOString(),
                virtualKey: 'vk_dev_002',
                provider: 'OpenAI',
              },
            ],
          },
          {
            errorType: 'Authentication Failed',
            count: 156,
            percentage: 30.1,
            lastOccurrence: new Date(Date.now() - 1000 * 60 * 5).toISOString(),
            affectedEndpoints: ['/v1/chat/completions', '/v1/audio/transcriptions'],
            examples: [
              {
                message: 'Invalid API key provided',
                timestamp: new Date(Date.now() - 1000 * 60 * 10).toISOString(),
                virtualKey: 'vk_invalid_001',
              },
              {
                message: 'Virtual key not found or disabled',
                timestamp: new Date(Date.now() - 1000 * 60 * 30).toISOString(),
                virtualKey: 'vk_disabled_003',
              },
            ],
          },
          {
            errorType: 'Model Not Available',
            count: 89,
            percentage: 17.2,
            lastOccurrence: new Date(Date.now() - 1000 * 60 * 60).toISOString(),
            affectedEndpoints: ['/v1/chat/completions'],
            examples: [
              {
                message: 'Model gpt-4-turbo not available for provider',
                timestamp: new Date(Date.now() - 1000 * 60 * 90).toISOString(),
                provider: 'Azure OpenAI',
              },
            ],
          },
          {
            errorType: 'Timeout',
            count: 39,
            percentage: 7.5,
            lastOccurrence: new Date(Date.now() - 1000 * 60 * 120).toISOString(),
            affectedEndpoints: ['/v1/images/generations', '/v1/audio/speech'],
            examples: [
              {
                message: 'Request timeout after 30 seconds',
                timestamp: new Date(Date.now() - 1000 * 60 * 150).toISOString(),
                provider: 'MiniMax',
              },
            ],
          },
        ];

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch error analytics');
        throw new Error(error?.message || 'Failed to fetch error analytics');
      }
    },
    staleTime: 60 * 1000, // 1 minute
  });
}

// Latency Metrics
export function useLatencyMetrics(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...usageAnalyticsApiKeys.latency(), timeRange],
    queryFn: async (): Promise<LatencyMetrics[]> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.analytics.getLatencyMetrics(timeRange);
        
        // Mock latency metrics data
        const mockData: LatencyMetrics[] = [
          {
            endpoint: '/v1/chat/completions',
            averageLatency: 847,
            p50: 654,
            p90: 1234,
            p95: 1567,
            p99: 2845,
            requestCount: 45678,
            slowestRequests: [
              {
                latency: 8934,
                timestamp: new Date(Date.now() - 1000 * 60 * 30).toISOString(),
                virtualKey: 'vk_slow_001',
                model: 'gpt-4',
              },
              {
                latency: 7234,
                timestamp: new Date(Date.now() - 1000 * 60 * 45).toISOString(),
                virtualKey: 'vk_slow_002',
                model: 'claude-3-opus',
              },
            ],
          },
          {
            endpoint: '/v1/images/generations',
            averageLatency: 15678,
            p50: 12345,
            p90: 23456,
            p95: 28901,
            p99: 45678,
            requestCount: 8934,
            slowestRequests: [
              {
                latency: 67890,
                timestamp: new Date(Date.now() - 1000 * 60 * 60).toISOString(),
                virtualKey: 'vk_image_001',
                model: 'dall-e-3',
              },
            ],
          },
          {
            endpoint: '/v1/audio/transcriptions',
            averageLatency: 3456,
            p50: 2345,
            p90: 5678,
            p95: 7890,
            p99: 12345,
            requestCount: 12345,
            slowestRequests: [
              {
                latency: 23456,
                timestamp: new Date(Date.now() - 1000 * 60 * 90).toISOString(),
                virtualKey: 'vk_audio_001',
                model: 'whisper-1',
              },
            ],
          },
        ];

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch latency metrics');
        throw new Error(error?.message || 'Failed to fetch latency metrics');
      }
    },
    staleTime: 60 * 1000, // 1 minute
  });
}

// User Analytics
export function useUserAnalytics(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...usageAnalyticsApiKeys.users(), timeRange],
    queryFn: async (): Promise<UserAnalytics[]> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.analytics.getUserAnalytics(timeRange);
        
        // Mock user analytics data
        const mockData: UserAnalytics[] = [
          {
            virtualKeyId: 'vk_prod_001',
            virtualKeyName: 'Production API',
            totalRequests: 23456,
            totalTokens: 1234567,
            totalCost: 456.78,
            averageLatency: 734,
            errorRate: 1.2,
            lastActivity: new Date(Date.now() - 1000 * 60 * 5).toISOString(),
            topModels: ['gpt-4', 'claude-3-opus', 'dall-e-3'],
            topEndpoints: ['/v1/chat/completions', '/v1/images/generations'],
            requestsOverTime: [
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 3).toISOString(), requests: 1245 },
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(), requests: 1567 },
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 1).toISOString(), requests: 1890 },
            ],
          },
          {
            virtualKeyId: 'vk_dev_002',
            virtualKeyName: 'Development Key',
            totalRequests: 12345,
            totalTokens: 567890,
            totalCost: 234.56,
            averageLatency: 892,
            errorRate: 3.4,
            lastActivity: new Date(Date.now() - 1000 * 60 * 15).toISOString(),
            topModels: ['gpt-3.5-turbo', 'claude-3-sonnet'],
            topEndpoints: ['/v1/chat/completions', '/v1/audio/transcriptions'],
            requestsOverTime: [
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 3).toISOString(), requests: 678 },
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(), requests: 789 },
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 1).toISOString(), requests: 901 },
            ],
          },
          {
            virtualKeyId: 'vk_test_003',
            virtualKeyName: 'Testing Environment',
            totalRequests: 5678,
            totalTokens: 234567,
            totalCost: 89.34,
            averageLatency: 654,
            errorRate: 5.6,
            lastActivity: new Date(Date.now() - 1000 * 60 * 30).toISOString(),
            topModels: ['gpt-3.5-turbo'],
            topEndpoints: ['/v1/chat/completions'],
            requestsOverTime: [
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 3).toISOString(), requests: 234 },
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(), requests: 345 },
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 1).toISOString(), requests: 456 },
            ],
          },
        ];

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch user analytics');
        throw new Error(error?.message || 'Failed to fetch user analytics');
      }
    },
    staleTime: 60 * 1000, // 1 minute
  });
}

// Endpoint Usage Analytics
export function useEndpointUsageAnalytics(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...usageAnalyticsApiKeys.endpoints(), timeRange],
    queryFn: async (): Promise<EndpointUsage[]> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.analytics.getEndpointUsage(timeRange);
        
        // Mock endpoint usage data
        const mockData: EndpointUsage[] = [
          {
            endpoint: '/v1/chat/completions',
            method: 'POST',
            totalRequests: 67890,
            averageLatency: 847,
            errorRate: 2.1,
            successRate: 97.9,
            tokensPerRequest: 45.7,
            costPerRequest: 0.0234,
            popularModels: ['gpt-4', 'gpt-3.5-turbo', 'claude-3-opus'],
            requestsOverTime: [
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 3).toISOString(), requests: 2345 },
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(), requests: 3456 },
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 1).toISOString(), requests: 4567 },
            ],
          },
          {
            endpoint: '/v1/images/generations',
            method: 'POST',
            totalRequests: 12345,
            averageLatency: 15678,
            errorRate: 3.2,
            successRate: 96.8,
            tokensPerRequest: 0,
            costPerRequest: 0.0456,
            popularModels: ['dall-e-3', 'dall-e-2', 'minimax-image'],
            requestsOverTime: [
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 3).toISOString(), requests: 456 },
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(), requests: 567 },
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 1).toISOString(), requests: 678 },
            ],
          },
          {
            endpoint: '/v1/audio/transcriptions',
            method: 'POST',
            totalRequests: 8901,
            averageLatency: 3456,
            errorRate: 1.8,
            successRate: 98.2,
            tokensPerRequest: 67.8,
            costPerRequest: 0.0123,
            popularModels: ['whisper-1'],
            requestsOverTime: [
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 3).toISOString(), requests: 234 },
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(), requests: 345 },
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 1).toISOString(), requests: 456 },
            ],
          },
          {
            endpoint: '/v1/audio/speech',
            method: 'POST',
            totalRequests: 5678,
            averageLatency: 4567,
            errorRate: 2.5,
            successRate: 97.5,
            tokensPerRequest: 34.5,
            costPerRequest: 0.0089,
            popularModels: ['tts-1', 'tts-1-hd'],
            requestsOverTime: [
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 3).toISOString(), requests: 123 },
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(), requests: 234 },
              { timestamp: new Date(Date.now() - 1000 * 60 * 60 * 1).toISOString(), requests: 345 },
            ],
          },
        ];

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch endpoint usage analytics');
        throw new Error(error?.message || 'Failed to fetch endpoint usage analytics');
      }
    },
    staleTime: 60 * 1000, // 1 minute
  });
}

// Export usage data
export function useExportUsageData() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ 
      type, 
      timeRange, 
      format = 'csv' 
    }: { 
      type: 'metrics' | 'requests' | 'tokens' | 'errors' | 'latency' | 'users' | 'endpoints';
      timeRange: TimeRangeFilter;
      format?: 'csv' | 'json' | 'xlsx';
    }) => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.analytics.exportUsageData({ type, timeRange, format });
        
        // Mock export functionality
        const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
        const filename = `usage-${type}-${timestamp}.${format}`;
        
        // Simulate file generation delay
        await new Promise(resolve => setTimeout(resolve, 2000));
        
        return {
          filename,
          url: `#mock-download-${filename}`,
          size: Math.floor(Math.random() * 2000000) + 100000, // Random file size
        };
      } catch (error: any) {
        reportError(error, 'Failed to export usage data');
        throw new Error(error?.message || 'Failed to export usage data');
      }
    },
  });
}