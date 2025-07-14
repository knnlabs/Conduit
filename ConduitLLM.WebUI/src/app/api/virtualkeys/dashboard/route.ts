import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { searchParams } = new URL(req.url);
    const period = searchParams.get('period') || '30d';
    const adminClient = getServerAdminClient();
    
    // Calculate date range
    const now = new Date();
    const startDate = new Date();
    
    switch (period) {
      case '7d':
        startDate.setDate(now.getDate() - 7);
        break;
      case '30d':
        startDate.setDate(now.getDate() - 30);
        break;
      case '90d':
        startDate.setDate(now.getDate() - 90);
        break;
      default:
        startDate.setDate(now.getDate() - 30);
    }
    
    // Get all virtual keys with their usage
    const [allKeys, vkAnalytics] = await Promise.all([
      adminClient.virtualKeys.list(1, 100),
      adminClient.analytics.getVirtualKeyAnalytics({
        timeRange: period,
        includeUsagePatterns: true,
        includeQuotaStatus: true,
      }),
    ]);
    
    // Get cost analytics for the same period
    const costAnalytics = await adminClient.analytics.getCostAnalytics({
      startDate: startDate.toISOString(),
      endDate: now.toISOString(),
      groupBy: 'day',
    });
    
    // Transform virtual keys data with real usage
    const virtualKeysData = allKeys.items.map((key: any) => {
      const analytics = vkAnalytics.virtualKeys.find(
        (vk: any) => vk.keyId === key.id || vk.keyName === key.keyName
      );
      
      return {
        id: key.id,
        name: key.keyName,
        status: key.isEnabled ? 'active' : 'inactive',
        requests: analytics?.usage?.requests || 0,
        cost: analytics?.usage?.cost || 0,
        budget: key.maxBudget || 0,
        budgetUsed: key.maxBudget > 0 
          ? ((analytics?.usage?.cost || 0) / key.maxBudget) * 100 
          : 0,
      };
    });
    
    // Calculate summary metrics
    const totalRequests = virtualKeysData.reduce((sum, key) => sum + key.requests, 0);
    const totalCost = virtualKeysData.reduce((sum, key) => sum + key.cost, 0);
    const activeKeys = virtualKeysData.filter(key => key.status === 'active').length;
    const averageBudgetUsed = virtualKeysData.length > 0 
      ? virtualKeysData.reduce((sum, key) => sum + key.budgetUsed, 0) / virtualKeysData.length
      : 0;
    
    // Calculate growth rates (comparing to previous period)
    // TODO: SDK should provide these comparisons
    const requestsGrowth = 12.5; // Placeholder
    const costGrowth = 8.3; // Placeholder
    const activeKeysGrowth = 15; // Placeholder
    
    // Generate time series data for charts
    const timeSeriesData = costAnalytics.trends?.map((trend: any) => ({
      date: new Date(trend.period).toLocaleDateString(),
      requests: Math.floor(totalRequests / costAnalytics.trends.length), // Distribute evenly as placeholder
      cost: trend.cost,
    })) || [];
    
    // Model usage distribution from analytics
    const topModels = vkAnalytics.usagePatterns?.topModels || [];
    const totalModelRequests = topModels.reduce((sum: number, model: any) => sum + (model.requests || 0), 0);
    
    const modelUsage = topModels.map((model: any) => ({
      name: model.model,
      value: model.requests || 0,
      percentage: totalModelRequests > 0 ? Math.round(((model.requests || 0) / totalModelRequests) * 100) : 0,
      model: model.model,
    }));
    
    return NextResponse.json({
      virtualKeys: virtualKeysData,
      summary: {
        totalRequests,
        totalCost,
        activeKeys,
        averageBudgetUsed,
        requestsGrowth,
        costGrowth,
        activeKeysGrowth,
      },
      timeSeriesData,
      modelUsage,
    });
  } catch (error) {
    return handleSDKError(error);
  }
}