import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function POST(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { searchParams } = new URL(req.url);
    const timeRange = searchParams.get('timeRange') || '30d';
    
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
      groupBy: 'day',
    });

    // Get model usage analytics
    const modelAnalytics = await adminClient.analytics.getModelUsageAnalytics({
      startDate: startDate.toISOString(),
      endDate: now.toISOString(),
    });

    // Generate CSV content
    let csv = 'Date,Total Cost,';
    
    // Add provider columns
    const providers = costAnalytics.breakdown?.byProvider?.map(p => p.name) || [];
    csv += providers.join(',') + '\n';
    
    // Add daily data
    costAnalytics.trends?.forEach(trend => {
      csv += `${trend.period},${trend.cost.toFixed(2)},`;
      
      // Add provider costs for this day
      providers.forEach(provider => {
        const providerData = costAnalytics.breakdown?.byProvider?.find(p => p.name === provider);
        const providerCost = providerData ? (trend.cost * (providerData.percentage || 0) / 100).toFixed(2) : '0.00';
        csv += providerCost + ',';
      });
      
      csv = csv.slice(0, -1) + '\n'; // Remove trailing comma and add newline
    });
    
    // Add summary section
    csv += '\n\nSummary\n';
    csv += `Total Spend,${costAnalytics.totalCost.toFixed(2)}\n`;
    csv += `Average Daily Cost,${(costAnalytics.totalCost / (costAnalytics.trends?.length || 1)).toFixed(2)}\n`;
    csv += '\nProvider Breakdown\n';
    csv += 'Provider,Cost,Percentage\n';
    
    costAnalytics.breakdown?.byProvider?.forEach(provider => {
      csv += `${provider.name},${provider.cost.toFixed(2)},${provider.percentage?.toFixed(1)}%\n`;
    });
    
    // Add model usage section
    csv += '\n\nModel Usage\n';
    csv += 'Model,Provider,Requests,Input Tokens,Output Tokens,Cost\n';
    
    modelAnalytics.models?.forEach(model => {
      const inputTokens = Math.floor(model.totalTokens * 0.6); // Estimate 60% input
      const outputTokens = Math.floor(model.totalTokens * 0.4); // Estimate 40% output
      csv += `${model.model},${model.provider},${model.totalRequests},${inputTokens},${outputTokens},${model.totalCost.toFixed(2)}\n`;
    });
    
    // Return CSV as download
    return new NextResponse(csv, {
      headers: {
        'Content-Type': 'text/csv',
        'Content-Disposition': `attachment; filename="cost-report-${timeRange}-${now.toISOString().split('T')[0]}.csv"`,
      },
    });
  } catch (error) {
    return handleSDKError(error);
  }
}