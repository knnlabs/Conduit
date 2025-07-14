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
    const timeRange = searchParams.get('timeRange') || '1h';
    
    const adminClient = getServerAdminClient();
    
    // Get comprehensive metrics from the Admin SDK
    const [systemMetrics, performanceMetrics, providerMetrics] = await Promise.all([
      adminClient.metrics.getSystemMetrics(timeRange),
      adminClient.metrics.getPerformanceMetrics({ timeRange }),
      adminClient.metrics.getProviderMetrics(timeRange),
    ]);

    // Get additional analytics data for enriched dashboard
    const [costAnalytics, usageAnalytics] = await Promise.all([
      adminClient.analytics.getCostAnalytics(),
      adminClient.analytics.getUsageAnalytics({
        timeRange,
        includeProviderBreakdown: true,
        includeModelBreakdown: true,
      }),
    ]);

    // Get provider health for status indicators
    const providerHealth = await adminClient.providerHealth.getHealthSummary();

    // Combine all data into a comprehensive dashboard response
    const dashboardData = {
      // Key metrics
      metrics: {
        totalRequests: usageAnalytics.metrics?.totalRequests || 0,
        avgResponseTime: performanceMetrics.summary?.averages?.responseTime || 0,
        errorRate: performanceMetrics.summary?.averages?.errorRate || 0,
        uptime: systemMetrics.system?.uptime || 0,
        p95ResponseTime: performanceMetrics.summary?.peaks?.responseTime || 0,
        errorCount: systemMetrics.requests?.failedRequests || 0,
        requestsTrend: performanceMetrics.summary?.trend === 'improving' ? 5 : performanceMetrics.summary?.trend === 'degrading' ? -5 : 0,
        responseTimeTrend: 0, // Calculate from time series if needed
        errorRateTrend: 0, // Calculate from time series if needed
      },
      // System health
      system: {
        cpu: systemMetrics.system?.cpu || { usage: 0, cores: 0 },
        memory: systemMetrics.system?.memory || { used: 0, total: 0, percentage: 0 },
        disk: { used: 0, total: 0, percentage: 0 }, // Not available in SDK yet
        network: { in: 0, out: 0 }, // Not available in SDK yet
        services: [], // TODO: Get from monitoring service when available
        uptime: systemMetrics.system?.uptime || 0,
      },
      // Performance data for charts
      performance: {
        timeSeries: performanceMetrics.timeSeries || [],
        endpoints: systemMetrics.requests?.topEndpoints || [],
        errorDistribution: [], // TODO: Calculate from error data when available
      },
      // Provider data
      providers: {
        metrics: Object.entries(providerMetrics.providers || {}).map(([name, data]: [string, any]) => ({
          name,
          requests: data.usage?.requests || 0,
          errors: data.usage?.errors || 0,
          errorRate: data.usage?.errorRate || 0,
          avgLatency: data.performance?.averageLatency || 0,
          status: data.health?.status || 'unknown',
        })),
        health: providerHealth.providers || [],
      },
      // Cost data
      costs: {
        totalSpend: costAnalytics.totalCost || 0,
        byProvider: costAnalytics.breakdown?.byProvider || [],
      },
      // Timestamp
      timestamp: new Date().toISOString(),
      timeRange,
    };
    
    return NextResponse.json(dashboardData);
  } catch (error) {
    return handleSDKError(error);
  }
}