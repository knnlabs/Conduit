import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const timeRange = searchParams.get('timeRange') || '1h';
    
    const adminClient = getServerAdminClient();
    
    // NOTE: Removed fake data calls. The following endpoints don't exist in the backend:
    // - adminClient.metrics.getSystemMetrics() -> /api/dashboard/metrics/system (fake data)
    // - adminClient.metrics.getPerformanceMetrics() -> /api/dashboard/metrics/performance (fake data)  
    // - adminClient.metrics.getProviderMetrics() -> /api/dashboard/metrics/providers (fake data)
    // - adminClient.analytics.getCostAnalytics() -> /api/cost-analytics (fake data)
    // - adminClient.analytics.getUsageAnalytics() -> /api/usage-analytics (fake data)
    
    // Only use real endpoints or return null/empty values
    let providerHealth = null;
    try {
      providerHealth = await adminClient.providerHealth.getHealthSummary();
    } catch (error) {
      // If provider health endpoint fails, continue with null data
      console.warn('Provider health endpoint unavailable:', error);
    }

    // Return dashboard data with real data only - null/empty where not available
    const dashboardData = {
      // Key metrics - all null/0 since fake endpoints removed
      metrics: {
        totalRequests: 0,
        avgResponseTime: 0,
        errorRate: 0,
        uptime: 0,
        p95ResponseTime: 0,
        errorCount: 0,
        requestsTrend: 0,
        responseTimeTrend: 0,
        errorRateTrend: 0,
      },
      // System health - null since fake endpoints removed
      system: {
        cpu: { usage: 0, cores: 0 },
        memory: { used: 0, total: 0, percentage: 0 },
        disk: { used: 0, total: 0, percentage: 0 },
        network: { in: 0, out: 0 },
        services: [],
        uptime: 0,
      },
      // Performance data - empty since fake endpoints removed
      performance: {
        timeSeries: [],
        endpoints: [],
        errorDistribution: [],
      },
      // Provider data - only real health data if available
      providers: {
        metrics: [],
        health: providerHealth?.providers || [],
      },
      // Cost data - null since fake endpoints removed
      costs: {
        totalSpend: 0,
        byProvider: [],
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