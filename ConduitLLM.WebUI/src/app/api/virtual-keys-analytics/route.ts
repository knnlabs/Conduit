import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const range = searchParams.get('range') || '7d';
    const keys = searchParams.get('keys')?.split(',').filter(Boolean);
    const adminClient = getServerAdminClient();
    
    // Get virtual key analytics from SDK using the modern FetchAnalyticsService
    const vkAnalytics = await adminClient.analytics.getVirtualKeyAnalytics({
      timeRange: range,
      virtualKeyIds: keys,
      includeUsagePatterns: true,
      includeQuotaStatus: true,
    });
    
    // Get usage analytics for time series data
    const usageAnalytics = await adminClient.analytics.getUsageAnalytics({
      timeRange: range,
      includeTimeSeries: true,
      includeProviderBreakdown: true,
      includeModelBreakdown: true,
      includeVirtualKeyBreakdown: true,
      includeEndpointBreakdown: true,
    });
    
    // Get all virtual keys to enrich the data
    const allKeys = await adminClient.virtualKeys.list(1, 100);
    
    // Transform SDK data to match WebUI format
    const virtualKeys = vkAnalytics.virtualKeys?.map((vkData: any) => {
      const keyInfo = allKeys.items.find((k: any) => k.id === vkData.keyId || k.keyName === vkData.keyName);
      
      return {
        id: vkData.keyId || keyInfo?.id || vkData.keyName,
        name: vkData.keyName || keyInfo?.keyName || `Key ${vkData.keyId}`,
        status: keyInfo?.isEnabled ? 'active' : 'inactive',
        created: keyInfo?.createdAt || new Date().toISOString(),
        lastUsed: vkData.lastUsed || new Date().toISOString(),
        usage: {
          requests: vkData.usage?.requests || vkData.requests || 0,
          requestsChange: vkData.usage?.requestsChange || 0,
          tokens: vkData.usage?.tokens || vkData.tokens || 0,
          tokensChange: vkData.usage?.tokensChange || 0,
          cost: vkData.usage?.cost || vkData.cost || 0,
          costChange: vkData.usage?.costChange || 0,
          errorRate: vkData.errorRate || 0,
        },
        quotas: {
          requests: {
            used: vkData.usage?.requests || 0,
            limit: keyInfo?.rateLimit || 10000,
            period: 'day' as const,
          },
          tokens: {
            used: vkData.usage?.tokens || 0,
            limit: 1000000, // Default token limit since SDK doesn't provide this
            period: 'month' as const,
          },
          cost: {
            used: vkData.usage?.cost || 0,
            limit: keyInfo?.maxBudget || 500,
            period: 'month' as const,
          },
        },
        providers: vkData.providerBreakdown || [],
        models: vkData.modelBreakdown || [],
        endpoints: vkData.endpointBreakdown || [],
      };
    }) || [];
    
    // Use time series data from usage analytics
    const timeSeries: Record<string, any[]> = {};
    if (usageAnalytics.timeSeries) {
      // Group time series data by virtual key if available
      virtualKeys.forEach((key: any) => {
        timeSeries[key.id] = usageAnalytics.timeSeries || [];
      });
    } else {
      // If no time series data available, provide empty arrays
      virtualKeys.forEach((key: any) => {
        timeSeries[key.id] = [];
      });
    }
    
    // Use aggregate metrics from SDK or calculate from virtual keys data
    const aggregateMetrics = vkAnalytics.aggregateMetrics || {
      totalRequests: virtualKeys.reduce((sum: number, key: any) => sum + key.usage.requests, 0),
      totalTokens: virtualKeys.reduce((sum: number, key: any) => sum + key.usage.tokens, 0),
      totalCost: virtualKeys.reduce((sum: number, key: any) => sum + key.usage.cost, 0),
      activeKeys: virtualKeys.filter((key: any) => key.status === 'active').length,
      avgErrorRate: virtualKeys.length > 0 
        ? virtualKeys.reduce((sum: number, key: any) => sum + key.usage.errorRate, 0) / virtualKeys.length 
        : 0,
      topKey: virtualKeys.sort((a: any, b: any) => b.usage.requests - a.usage.requests)[0]?.name || '',
    };
    
    return NextResponse.json({
      virtualKeys,
      timeSeries,
      aggregateMetrics,
    });
  } catch (error) {
    console.error('Error fetching virtual key analytics:', error);
    
    // Return empty data structure instead of mock data
    return NextResponse.json({
      virtualKeys: [],
      timeSeries: {},
      aggregateMetrics: {
        totalRequests: 0,
        totalTokens: 0,
        totalCost: 0,
        activeKeys: 0,
        avgErrorRate: 0,
        topKey: '',
      },
      _error: 'Failed to fetch analytics data. Please ensure the Admin API is configured correctly.',
    });
  }
}
