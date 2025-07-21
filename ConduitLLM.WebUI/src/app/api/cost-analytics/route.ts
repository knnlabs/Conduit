import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(req: NextRequest) {

  try {
    const { searchParams } = new URL(req.url);
    const timeRange = searchParams.get('timeRange') ?? '30d';
    
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
    
    // Get data from actual cost dashboard endpoints
    const [costSummary, costTrends, modelCosts, virtualKeyCosts] = await Promise.all([
      adminClient.costDashboard.getCostSummary('daily', startDate.toISOString(), now.toISOString()),
      adminClient.costDashboard.getCostTrends('daily', startDate.toISOString(), now.toISOString()),
      adminClient.costDashboard.getModelCosts(startDate.toISOString(), now.toISOString()),
      adminClient.costDashboard.getVirtualKeyCosts(startDate.toISOString(), now.toISOString()),
    ]);

    // Calculate daily average
    const dayCount = Math.ceil((now.getTime() - startDate.getTime()) / (1000 * 60 * 60 * 24));
    const averageDailyCost = costSummary.totalCost / dayCount;

    // Calculate projected monthly spend
    const daysInMonth = new Date(now.getFullYear(), now.getMonth() + 1, 0).getDate();
    const dayOfMonth = now.getDate();
    const projectedMonthlySpend = (costSummary.totalCost / dayOfMonth) * daysInMonth;

    // Format provider costs from summary
    const providerCosts = costSummary.topProvidersBySpend?.map(provider => ({
      provider: provider.name,
      cost: provider.cost,
      usage: provider.percentage,
      trend: 0, // Not available in current data
    })) || [];

    // Format model usage
    const modelUsage = modelCosts?.map(model => ({
      model: model.model,
      provider: model.model.includes('/') ? model.model.split('/')[0] : 'unknown',
      requests: model.requestCount,
      tokensIn: 0, // Not available in current endpoint
      tokensOut: 0, // Not available in current endpoint
      cost: model.cost,
    })) || [];

    // Format daily costs from trends
    const dailyCosts = costTrends?.data?.map(trend => {
      const providers: Record<string, number> = {};
      
      // Distribute cost across providers based on breakdown
      providerCosts.forEach(provider => {
        providers[provider.provider] = (trend.cost * provider.usage) / 100;
      });

      return {
        date: trend.date,
        cost: trend.cost,
        providers,
      };
    }) || [];

    // Check if any virtual keys have budgets
    const monthlyBudget = virtualKeyCosts?.find(vk => vk.budgetUsed !== undefined)
      ? virtualKeyCosts.reduce((sum, vk) => sum + (vk.budgetUsed ?? 0) + (vk.budgetRemaining ?? 0), 0)
      : null;

    const response = {
      totalSpend: costSummary.totalCost,
      averageDailyCost,
      projectedMonthlySpend,
      monthlyBudget,
      projectedTrend: costSummary.costChangePercentage,
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