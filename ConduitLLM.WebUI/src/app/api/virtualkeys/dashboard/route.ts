import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(req: NextRequest) {

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
    
    try {
      // Try to get analytics data, but don't fail if unavailable
      let allKeys, vkAnalytics, costAnalytics;
      
      try {
        [allKeys, vkAnalytics, costAnalytics] = await Promise.all([
          adminClient.virtualKeys.list(1, 100),
          adminClient.analytics.getVirtualKeyAnalytics({
            timeRange: period,
            includeUsagePatterns: true,
            includeQuotaStatus: true,
          }),
          adminClient.analytics.getCostAnalytics({
            startDate: startDate.toISOString(),
            endDate: now.toISOString(),
            groupBy: 'day',
          }),
        ]);
      } catch (analyticsError) {
        console.warn('Analytics APIs not available, falling back to basic data:', analyticsError);
        // Just get the virtual keys list
        allKeys = await adminClient.virtualKeys.list(1, 100);
        vkAnalytics = null;
        costAnalytics = null;
      }
      
      // Transform virtual keys data with real usage
      const virtualKeysData = (allKeys?.items || []).map((key: any) => {
        const analytics = vkAnalytics?.virtualKeys?.find(
          (vk: any) => vk.keyId === key.id
        );
        
        // Map status from SDK to dashboard status
        let status = 'inactive';
        if (key.isEnabled) {
          status = analytics?.status || 'active';
        }
        
        return {
          id: key.id,
          name: key.keyName,
          status,
          requests: analytics?.usage?.requests || 0,
          cost: analytics?.usage?.cost || 0,
          budget: key.maxBudget || 0,
          budgetUsed: analytics?.quota?.percentage || 0,
        };
      });
      
      // Calculate summary metrics from aggregate data
      const totalRequests = vkAnalytics.aggregateMetrics?.totalRequests || 0;
      const totalCost = vkAnalytics.aggregateMetrics?.totalCost || 0;
      const activeKeys = vkAnalytics.aggregateMetrics?.activeKeys || 0;
      const averageBudgetUsed = vkAnalytics.aggregateMetrics?.averageUtilization || 0;
      
      // Calculate growth rates (comparing to previous period)
      // Note: Growth rates not available from SDK yet, using null values
      const requestsGrowth = null;
      const costGrowth = null;
      const activeKeysGrowth = null;
      
      // Generate time series data for charts
      const timeSeriesData = costAnalytics.trends?.map((trend: any) => ({
        date: new Date(trend.period).toLocaleDateString(),
        requests: null, // Requests not available in CostTrend
        cost: trend.cost || 0,
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
    } catch (sdkError) {
      console.error('SDK Error in dashboard:', sdkError);
      
      // If analytics methods fail, provide fallback with just virtual keys data
      try {
        const allKeys = await adminClient.virtualKeys.list(1, 100);
        
        const virtualKeysData = (allKeys?.items || []).map((key: any) => ({
          id: key.id,
          name: key.keyName,
          status: key.isEnabled ? 'active' : 'inactive',
          requests: 0,
          cost: 0,
          budget: key.maxBudget || 0,
          budgetUsed: 0,
        }));
        
        // Return minimal data structure
        return NextResponse.json({
          virtualKeys: virtualKeysData,
          summary: {
            totalRequests: 0,
            totalCost: 0,
            activeKeys: virtualKeysData.filter(k => k.status === 'active').length,
            averageBudgetUsed: 0,
            requestsGrowth: null,
            costGrowth: null,
            activeKeysGrowth: null,
          },
          timeSeriesData: [],
          modelUsage: [],
        });
      } catch (fallbackError) {
        throw fallbackError;
      }
    }
  } catch (error) {
    return handleSDKError(error);
  }
}