'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';

// Query key factory for Analytics API
export const analyticsApiKeys = {
  all: ['analytics-api'] as const,
  costs: () => [...analyticsApiKeys.all, 'costs'] as const,
  usage: () => [...analyticsApiKeys.all, 'usage'] as const,
  trends: () => [...analyticsApiKeys.all, 'trends'] as const,
  providers: () => [...analyticsApiKeys.all, 'providers'] as const,
  models: () => [...analyticsApiKeys.all, 'models'] as const,
  virtualKeys: () => [...analyticsApiKeys.all, 'virtual-keys'] as const,
} as const;

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

// Cost Analytics Hooks
export function useCostSummary(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...analyticsApiKeys.costs(), 'summary', timeRange],
    queryFn: async (): Promise<CostSummary> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.analytics.getCostSummary(timeRange);
        
        // Mock data for now
        const mockData: CostSummary = {
          totalSpend: 1247.86,
          totalBudget: 2000.00,
          totalRequests: 45237,
          totalTokens: 1847293,
          activeVirtualKeys: 12,
          averageCostPerRequest: 0.0276,
          averageCostPerToken: 0.0000067,
          spendTrend: 12.5,
          requestTrend: 8.3,
        };

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch cost summary');
        throw new Error(error?.message || 'Failed to fetch cost summary');
      }
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
    refetchInterval: 10 * 60 * 1000, // 10 minutes
  });
}

export function useCostTrends(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...analyticsApiKeys.trends(), timeRange],
    queryFn: async (): Promise<CostTrendData[]> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.analytics.getCostTrends(timeRange);
        
        // Generate mock trend data based on time range
        const generateMockTrends = (days: number): CostTrendData[] => {
          const trends: CostTrendData[] = [];
          const now = new Date();
          
          for (let i = days - 1; i >= 0; i--) {
            const date = new Date(now);
            date.setDate(date.getDate() - i);
            
            const baseSpend = 45 + Math.random() * 30;
            const requests = Math.floor(1200 + Math.random() * 800);
            const tokens = Math.floor(requests * (40 + Math.random() * 20));
            
            trends.push({
              date: date.toISOString().split('T')[0],
              spend: Math.round(baseSpend * 100) / 100,
              requests,
              tokens,
              averageCostPerRequest: Math.round((baseSpend / requests) * 10000) / 10000,
            });
          }
          
          return trends;
        };

        const days = timeRange.range === '24h' ? 1 : 
                    timeRange.range === '7d' ? 7 :
                    timeRange.range === '30d' ? 30 : 
                    timeRange.range === '90d' ? 90 : 7;

        return generateMockTrends(days);
      } catch (error: any) {
        reportError(error, 'Failed to fetch cost trends');
        throw new Error(error?.message || 'Failed to fetch cost trends');
      }
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

export function useProviderCosts(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...analyticsApiKeys.providers(), timeRange],
    queryFn: async (): Promise<ProviderCostData[]> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.analytics.getProviderCosts(timeRange);
        
        // Mock provider data
        const mockData: ProviderCostData[] = [
          {
            provider: 'OpenAI',
            spend: 567.43,
            requests: 18234,
            percentage: 45.5,
            models: ['gpt-4', 'gpt-3.5-turbo', 'dall-e-3'],
            averageCost: 0.0311,
          },
          {
            provider: 'Anthropic',
            spend: 398.67,
            requests: 12456,
            percentage: 32.0,
            models: ['claude-3-opus', 'claude-3-sonnet', 'claude-3-haiku'],
            averageCost: 0.0320,
          },
          {
            provider: 'Azure OpenAI',
            spend: 189.23,
            requests: 7892,
            percentage: 15.2,
            models: ['azure-gpt-4', 'azure-gpt-35-turbo'],
            averageCost: 0.0240,
          },
          {
            provider: 'MiniMax',
            spend: 92.53,
            requests: 6655,
            percentage: 7.4,
            models: ['minimax-chat', 'minimax-image', 'video-01'],
            averageCost: 0.0139,
          },
        ];

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch provider costs');
        throw new Error(error?.message || 'Failed to fetch provider costs');
      }
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

export function useModelCosts(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...analyticsApiKeys.models(), timeRange],
    queryFn: async (): Promise<ModelCostData[]> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.analytics.getModelCosts(timeRange);
        
        // Mock model data
        const mockData: ModelCostData[] = [
          {
            model: 'gpt-4',
            provider: 'OpenAI',
            spend: 345.67,
            requests: 8924,
            tokens: 1247839,
            averageCostPerRequest: 0.0387,
            averageCostPerToken: 0.0000277,
          },
          {
            model: 'claude-3-opus',
            provider: 'Anthropic',
            spend: 287.43,
            requests: 6732,
            tokens: 982456,
            averageCostPerRequest: 0.0427,
            averageCostPerToken: 0.0000292,
          },
          {
            model: 'gpt-3.5-turbo',
            provider: 'OpenAI',
            spend: 156.78,
            requests: 15634,
            tokens: 1543287,
            averageCostPerRequest: 0.0100,
            averageCostPerToken: 0.0000102,
          },
          {
            model: 'claude-3-sonnet',
            provider: 'Anthropic',
            spend: 111.24,
            requests: 5724,
            tokens: 734582,
            averageCostPerRequest: 0.0194,
            averageCostPerToken: 0.0000151,
          },
          {
            model: 'dall-e-3',
            provider: 'OpenAI',
            spend: 89.43,
            requests: 2156,
            tokens: 0, // No tokens for image generation
            averageCostPerRequest: 0.0415,
            averageCostPerToken: 0,
          },
        ];

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch model costs');
        throw new Error(error?.message || 'Failed to fetch model costs');
      }
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

export function useVirtualKeyCosts(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...analyticsApiKeys.virtualKeys(), timeRange],
    queryFn: async (): Promise<VirtualKeyCostData[]> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.analytics.getVirtualKeyCosts(timeRange);
        
        // Mock virtual key data
        const mockData: VirtualKeyCostData[] = [
          {
            keyId: 'vk_prod_001',
            keyName: 'Production API',
            spend: 456.78,
            budget: 500.00,
            requests: 18456,
            usagePercentage: 91.4,
            isOverBudget: false,
            lastActivity: new Date(Date.now() - 1000 * 60 * 15).toISOString(), // 15 minutes ago
            topModels: ['gpt-4', 'claude-3-opus'],
          },
          {
            keyId: 'vk_dev_002',
            keyName: 'Development Key',
            spend: 234.56,
            budget: 300.00,
            requests: 12034,
            usagePercentage: 78.2,
            isOverBudget: false,
            lastActivity: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(), // 2 hours ago
            topModels: ['gpt-3.5-turbo', 'claude-3-sonnet'],
          },
          {
            keyId: 'vk_test_003',
            keyName: 'Testing Environment',
            spend: 89.34,
            budget: 100.00,
            requests: 5672,
            usagePercentage: 89.3,
            isOverBudget: false,
            lastActivity: new Date(Date.now() - 1000 * 60 * 30).toISOString(), // 30 minutes ago
            topModels: ['gpt-3.5-turbo'],
          },
          {
            keyId: 'vk_exp_004',
            keyName: 'Experimental Features',
            spend: 156.78,
            budget: 150.00,
            requests: 3245,
            usagePercentage: 104.5,
            isOverBudget: true,
            lastActivity: new Date(Date.now() - 1000 * 60 * 60 * 6).toISOString(), // 6 hours ago
            topModels: ['dall-e-3', 'minimax-video'],
          },
        ];

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch virtual key costs');
        throw new Error(error?.message || 'Failed to fetch virtual key costs');
      }
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

// Export cost data
export function useExportCostData() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ 
      type, 
      timeRange, 
      format = 'csv' 
    }: { 
      type: 'summary' | 'trends' | 'providers' | 'models' | 'virtual-keys';
      timeRange: TimeRangeFilter;
      format?: 'csv' | 'json' | 'xlsx';
    }) => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.analytics.exportCostData({ type, timeRange, format });
        
        // Mock export functionality
        const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
        const filename = `cost-${type}-${timestamp}.${format}`;
        
        // Simulate file generation delay
        await new Promise(resolve => setTimeout(resolve, 2000));
        
        return {
          filename,
          url: `#mock-download-${filename}`,
          size: Math.floor(Math.random() * 1000000) + 50000, // Random file size
        };
      } catch (error: any) {
        reportError(error, 'Failed to export cost data');
        throw new Error(error?.message || 'Failed to export cost data');
      }
    },
  });
}

// Real-time cost alerts
export function useCostAlerts() {
  return useQuery({
    queryKey: [...analyticsApiKeys.all, 'alerts'],
    queryFn: async () => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.analytics.getCostAlerts();
        
        // Mock alert data
        const mockAlerts = [
          {
            id: 'alert_001',
            type: 'budget_exceeded',
            severity: 'high',
            message: 'Virtual key "Experimental Features" has exceeded its budget by 4.5%',
            virtualKeyId: 'vk_exp_004',
            createdAt: new Date(Date.now() - 1000 * 60 * 30).toISOString(),
            acknowledged: false,
          },
          {
            id: 'alert_002',
            type: 'budget_warning',
            severity: 'medium',
            message: 'Virtual key "Production API" is at 91% of budget',
            virtualKeyId: 'vk_prod_001',
            createdAt: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(),
            acknowledged: false,
          },
          {
            id: 'alert_003',
            type: 'unusual_spending',
            severity: 'low',
            message: 'Spending spike detected for model "gpt-4" (+35% from average)',
            modelId: 'gpt-4',
            createdAt: new Date(Date.now() - 1000 * 60 * 60 * 4).toISOString(),
            acknowledged: true,
          },
        ];

        return mockAlerts;
      } catch (error: any) {
        reportError(error, 'Failed to fetch cost alerts');
        return [];
      }
    },
    staleTime: 1 * 60 * 1000, // 1 minute
    refetchInterval: 2 * 60 * 1000, // 2 minutes
  });
}