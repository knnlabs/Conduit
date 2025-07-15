import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(req: NextRequest) {

  try {
    const { searchParams } = new URL(req.url);
    const timeRange = searchParams.get('timeRange') || '30d';
    const groupBy = searchParams.get('groupBy') || 'day';
    
    const adminClient = getServerAdminClient();
    
    // Parse time range
    const now = new Date();
    const startDate = new Date();
    
    switch (timeRange) {
      case '7d':
        startDate.setDate(now.getDate() - 7);
        break;
      case '30d':
        startDate.setDate(now.getDate() - 30);
        break;
      case '90d':
        startDate.setDate(now.getDate() - 90);
        break;
      case 'ytd':
        startDate.setMonth(0, 1);
        break;
      default:
        startDate.setDate(now.getDate() - 30);
    }
    
    // Get cost analytics from Admin SDK
    const costAnalytics = await adminClient.analytics.getCostAnalytics({
      startDate: startDate.toISOString(),
      endDate: now.toISOString(),
      groupBy: groupBy as 'hour' | 'day' | 'week' | 'month',
    });

    // Get model usage analytics
    const modelAnalytics = await adminClient.analytics.getModelUsageAnalytics({
      startDate: startDate.toISOString(),
      endDate: now.toISOString(),
    });

    // Calculate daily average
    const dayCount = Math.ceil((now.getTime() - startDate.getTime()) / (1000 * 60 * 60 * 24));
    const averageDailyCost = costAnalytics.totalCost / dayCount;

    // Calculate projected monthly spend
    const daysInMonth = new Date(now.getFullYear(), now.getMonth() + 1, 0).getDate();
    const dayOfMonth = now.getDate();
    const projectedMonthlySpend = (costAnalytics.totalCost / dayOfMonth) * daysInMonth;

    // Format provider costs
    const providerCosts = costAnalytics.breakdown?.byProvider?.map(provider => ({
      provider: provider.name,
      cost: provider.cost,
      usage: provider.percentage || 0,
      trend: 0, // Default trend value since not available in current SDK
    })) || [];

    // Format model usage
    const modelUsage = modelAnalytics.models?.map(model => ({
      model: model.model,
      provider: model.provider,
      requests: model.totalRequests,
      tokensIn: Math.floor(model.totalTokens * 0.6) || 0, // Estimate 60% input tokens
      tokensOut: Math.floor(model.totalTokens * 0.4) || 0, // Estimate 40% output tokens
      cost: model.totalCost || 0,
    })) || [];

    // Format daily costs
    const dailyCosts = costAnalytics.trends?.map(trend => {
      const providers: Record<string, number> = {};
      
      // Distribute cost across providers based on breakdown
      providerCosts.forEach(provider => {
        providers[provider.provider] = (trend.cost * provider.usage) / 100;
      });

      return {
        date: trend.period,
        cost: trend.cost,
        providers,
      };
    }) || [];

    const response = {
      totalSpend: costAnalytics.totalCost,
      averageDailyCost,
      projectedMonthlySpend: costAnalytics.projections?.monthly || projectedMonthlySpend,
      monthlyBudget: null, // Budget feature not yet available in SDK
      projectedTrend: costAnalytics.trends?.length > 0 ? costAnalytics.trends[costAnalytics.trends.length - 1]?.changePercentage : null,
      providerCosts,
      modelUsage,
      dailyCosts,
      timeRange,
      lastUpdated: new Date().toISOString(),
    };
    
    return NextResponse.json(response);
  } catch (error) {
    return handleSDKError(error);
  }
}