import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const range = searchParams.get('range') || '7d';
    const keys = searchParams.get('keys')?.split(',').filter(Boolean);
    const adminClient = getServerAdminClient();
    
    // Get virtual key analytics from SDK using the enhanced method that includes all required fields
    const vkAnalytics = await adminClient.analytics.getVirtualKeyAnalyticsEnhanced({
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
      const keyInfo = allKeys.items.find((k: any) => k.id?.toString() === vkData.keyId || k.keyName === vkData.keyName);
      
      return {
        id: vkData.keyId || keyInfo?.id?.toString() || vkData.keyName,
        name: vkData.keyName || keyInfo?.keyName || `Key ${vkData.keyId}`,
        status: keyInfo?.isEnabled ? 'active' : 'inactive',
        created: keyInfo?.createdAt || new Date().toISOString(),
        lastUsed: vkData.usage?.lastUsed || new Date().toISOString(),
        usage: {
          requests: vkData.usage?.requests || 0,
          requestsChange: vkData.usage?.requestsChange || 0, // Now provided by enhanced SDK
          tokens: vkData.usage?.tokens || 0,
          tokensChange: vkData.usage?.tokensChange || 0, // Now provided by enhanced SDK
          cost: vkData.usage?.cost || 0,
          costChange: vkData.usage?.costChange || 0, // Now provided by enhanced SDK
          errorRate: vkData.usage?.errorRate || 0,
        },
        quotas: {
          requests: {
            used: vkData.usage?.requests || 0,
            limit: keyInfo?.rateLimit || 10000,
            period: 'day' as const,
          },
          tokens: {
            used: vkData.usage?.tokens || 0,
            limit: vkData.tokenLimit || 1000000, // Now from SDK metadata
            period: (vkData.tokenPeriod || 'month') as 'hour' | 'day' | 'month',
          },
          cost: {
            used: vkData.usage?.cost || 0,
            limit: keyInfo?.maxBudget || 500,
            period: keyInfo?.budgetDuration?.toLowerCase() as 'day' | 'month' || 'month',
          },
        },
        providers: vkData.providerBreakdown || [],
        models: vkData.modelBreakdown || [],
        endpoints: vkData.endpointBreakdown || [], // Now provided by enhanced SDK
      };
    }) || [];
    
    // Use per-key time series data from enhanced SDK
    const timeSeries: Record<string, any[]> = {};
    virtualKeys.forEach((key: any, index: number) => {
      // Each virtual key now has its own time series from the enhanced SDK
      const vkData = vkAnalytics.virtualKeys?.[index];
      timeSeries[key.id] = vkData?.timeSeries || [];
    });
    
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
