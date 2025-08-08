'use client';

import { useQuery } from '@tanstack/react-query';
import { withAdminClient } from '@/lib/client/adminClient';

// Types for analytics responses
interface UsageAnalytics {
  metrics: {
    totalRequests: number;
    totalCost: number;
    totalTokens: number;
    activeVirtualKeys: number;
    requestsChange: number;
    costChange: number;
    tokensChange: number;
    virtualKeysChange: number;
    systemHealth?: {
      overallHealthPercentage?: number;
      errorRate?: number;
      responseTimeP95?: number;
      activeConnections?: number;
    };
    realTimeMetrics?: {
      requestsPerSecond?: number;
      activeRequests?: number;
      averageResponseTime?: number;
      costBurnRatePerHour?: number;
      averageCostPerRequest?: number;
    };
  };
  timeSeries: Array<{
    timestamp: string;
    requests: number;
    cost: number;
    tokens: number;
  }>;
  providerUsage: Array<{
    provider: string;
    requests: number;
    cost: number;
    tokens: number;
    percentage: number;
    costPercentage: number;
    errorRate: number;
    avgResponseTime: number;
    avgCostPerRequest: number;
    minCost: number;
    maxCost: number;
  }>;
  modelUsage: Array<{
    model: string;
    provider: string;
    requests: number;
    cost: number;
    tokens: number;
    inputTokens: number;
    outputTokens: number;
    avgCostPerRequest: number;
    avgTokensPerRequest: number;
    avgResponseTime: number;
    successRate: number;
    errorRate: number;
    efficiency: number;
  }>;
  virtualKeyUsage: Array<{
    keyName: string;
    requests: number;
    cost: number;
    tokens: number;
    lastUsed: string;
    firstUsed: string;
    avgCostPerRequest: number;
    avgResponseTime: number;
    successRate: number;
    errorRate: number;
    uniqueModelsUsed: number;
    uniqueProvidersUsed: number;
    avgRequestsPerDay: number;
    daysSinceFirstUsed: number;
    efficiency: number;
  }>;
  endpointUsage: Array<{
    endpoint: string;
    requests: number;
    avgDuration: number;
    minDuration: number;
    maxDuration: number;
    p50Duration: number;
    p95Duration: number;
    p99Duration: number;
    errorRate: number;
    successRate: number;
    requestsPerMinute: number;
    realTimeAvgDuration?: number;
    realTimeErrorRate?: number;
  }>;
  timeRange: string;
  lastUpdated: string;
  dataSources: {
    historicalData: boolean;
    realTimeMetrics: boolean;
    coreSDKConnected: boolean;
    partialDataAvailable?: boolean;
  };
  error?: {
    type: string;
    message: string;
    timestamp: string;
    services: {
      adminSDK: boolean;
      coreSDK: boolean;
      costSummary: boolean;
      requestLogs: boolean;
      realTimeMetrics: boolean;
    };
  };
  warning?: string;
}

interface CostAnalytics {
  totalSpend: number;
  averageDailyCost: number;
  projectedMonthlySpend: number;
  monthlyBudget?: number;
  projectedTrend: number;
  providerCosts: Array<{
    provider: string;
    cost: number;
    usage: number;
    trend: number;
  }>;
  modelUsage: Array<{
    model: string;
    provider: string;
    requests: number;
    tokensIn: number;
    tokensOut: number;
    cost: number;
  }>;
  dailyCosts: Array<{
    date: string;
    cost: number;
    providers: Record<string, number>;
  }>;
  timeRange: string;
  lastUpdated: string;
}

interface SystemPerformance {
  metrics: {
    cpu: {
      usage: number;
      cores: number;
      loadAverage: number[];
      temperature: number | null;
    };
    memory: {
      total: number;
      used: number;
      percentage: number;
      swap: { total: number; used: number };
    };
    disk: {
      total: number;
      used: number;
      percentage: number;
      io: { read: number; write: number };
    };
    network: {
      in: number;
      out: number;
      connections: number;
      latency: number;
    };
    uptime: number;
    processCount: number;
    threadCount: number;
  };
  history: Array<{
    timestamp: string;
    cpu: number;
    memory: number;
    disk: number;
    network: { in: number; out: number };
  }>;
  services: Array<{
    name: string;
    status: string;
    uptime: number;
    memory: number;
    cpu: number;
    lastCheck: string;
  }>;
  alerts: Array<{
    id: string;
    type: string;
    severity: string;
    message: string;
    timestamp: string;
    resolved: boolean;
  }>;
  info?: string;
}

// Usage Analytics Hook
export function useUsageAnalytics(timeRange = '7d') {
  return useQuery<UsageAnalytics, Error>({
    queryKey: ['usage-analytics', timeRange],
    queryFn: async () => {
      // Parse time range into date objects
      const now = new Date();
      const startDate = new Date();
      
      switch (timeRange) {
        case '24h':
          startDate.setHours(now.getHours() - 24);
          break;
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
          startDate.setDate(now.getDate() - 7);
      }

      // Calculate previous period for change calculations
      const periodLength = now.getTime() - startDate.getTime();
      const previousStartDate = new Date(startDate.getTime() - periodLength);
      const previousEndDate = new Date(startDate.getTime());

      try {
        // Fetch data from Admin SDK
        const [
          currentCostSummary,
          currentRequestLogs,
          previousCostSummary,
        ] = await Promise.all([
          withAdminClient(client => 
            client.costDashboard.getCostSummary(
              'daily',
              startDate.toISOString().split('T')[0],
              now.toISOString().split('T')[0]
            )
          ),
          withAdminClient(client => 
            client.analytics.getRequestLogs({
              startDate: startDate.toISOString(),
              endDate: now.toISOString(),
              pageSize: 100,
              page: 1
            })
          ),
          withAdminClient(client => 
            client.costDashboard.getCostSummary(
              'daily',
              previousStartDate.toISOString().split('T')[0],
              previousEndDate.toISOString().split('T')[0]
            )
          ).catch(() => null),
        ]);

        // Transform the data into the expected format
        const requestLogs = currentRequestLogs as { totalCount: number; items: unknown[] };
        const costSummary = currentCostSummary as { 
          totalCost: number; 
          topProvidersBySpend?: Array<{ name: string; cost: number; percentage: number }>;
        };
        const prevCostSummary = previousCostSummary as { totalCost: number } | null;

        // Calculate basic metrics
        const totalRequests = requestLogs?.totalCount ?? requestLogs?.items?.length ?? 0;
        const totalCost = costSummary?.totalCost ?? 0;
        const totalTokens = 0; // Would need to calculate from request logs
        const activeVirtualKeys = 0; // Would need to calculate from request logs
        
        // Calculate changes
        const costChange = (prevCostSummary && prevCostSummary.totalCost > 0)
          ? ((totalCost - prevCostSummary.totalCost) / prevCostSummary.totalCost) * 100 
          : 0;

        const response: UsageAnalytics = {
          metrics: {
            totalRequests,
            totalCost,
            totalTokens,
            activeVirtualKeys,
            requestsChange: 0,
            costChange: isFinite(costChange) ? costChange : 0,
            tokensChange: 0,
            virtualKeysChange: 0,
          },
          timeSeries: [],
          providerUsage: costSummary?.topProvidersBySpend?.map(provider => ({
            provider: provider.name,
            requests: 0,
            cost: provider.cost,
            tokens: 0,
            percentage: provider.percentage,
            costPercentage: provider.percentage,
            errorRate: 0,
            avgResponseTime: 0,
            avgCostPerRequest: 0,
            minCost: 0,
            maxCost: 0,
          })) ?? [],
          modelUsage: [],
          virtualKeyUsage: [],
          endpointUsage: [],
          timeRange,
          lastUpdated: new Date().toISOString(),
          dataSources: {
            historicalData: true,
            realTimeMetrics: false,
            coreSDKConnected: false,
          },
        };

        return response;
      } catch (error) {
        console.error('Analytics fetch error:', error);
        throw error;
      }
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
    gcTime: 10 * 60 * 1000, // 10 minutes
    retry: 2,
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 30000),
  });
}

// Cost Analytics Hook
export function useCostAnalytics(timeRange = '30d') {
  return useQuery<CostAnalytics, Error>({
    queryKey: ['cost-analytics', timeRange],
    queryFn: async () => {
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
      
      // Get data from cost dashboard endpoints
      const [costSummary, costTrends, modelCosts, virtualKeyCosts] = await Promise.all([
        withAdminClient(client => 
          client.costDashboard.getCostSummary('daily', startDate.toISOString(), now.toISOString())
        ),
        withAdminClient(client => 
          client.costDashboard.getCostTrends('daily', startDate.toISOString(), now.toISOString())
        ),
        withAdminClient(client => 
          client.costDashboard.getModelCosts(startDate.toISOString(), now.toISOString())
        ),
        withAdminClient(client => 
          client.costDashboard.getVirtualKeyCosts(startDate.toISOString(), now.toISOString())
        ),
      ]);

      // Transform the data
      const summary = costSummary as { 
        totalCost: number; 
        costChangePercentage: number;
        topProvidersBySpend?: Array<{ name: string; cost: number; percentage: number }>;
      };
      const trends = costTrends as { data?: Array<{ date: string; cost: number }> };
      const models = modelCosts as Array<{ model: string; requestCount: number; cost: number }>;
      const vkCosts = virtualKeyCosts as Array<{ budgetUsed?: number; budgetRemaining?: number }>;

      // Calculate daily average
      const dayCount = Math.ceil((now.getTime() - startDate.getTime()) / (1000 * 60 * 60 * 24));
      const averageDailyCost = summary.totalCost / dayCount;

      // Calculate projected monthly spend
      const daysInMonth = new Date(now.getFullYear(), now.getMonth() + 1, 0).getDate();
      const dayOfMonth = now.getDate();
      const projectedMonthlySpend = (summary.totalCost / dayOfMonth) * daysInMonth;

      const response: CostAnalytics = {
        totalSpend: summary.totalCost,
        averageDailyCost,
        projectedMonthlySpend,
        monthlyBudget: vkCosts?.find(vk => vk.budgetUsed !== undefined)
          ? vkCosts.reduce((sum, vk) => sum + (vk.budgetUsed ?? 0) + (vk.budgetRemaining ?? 0), 0)
          : undefined,
        projectedTrend: summary.costChangePercentage,
        providerCosts: summary.topProvidersBySpend?.map(provider => ({
          provider: provider.name,
          cost: provider.cost,
          usage: provider.percentage,
          trend: 0, // Not available
        })) ?? [],
        modelUsage: models?.map(model => ({
          model: model.model,
          provider: model.model.includes('/') ? model.model.split('/')[0] : 'unknown',
          requests: model.requestCount,
          tokensIn: 0, // Not available
          tokensOut: 0, // Not available
          cost: model.cost,
        })) ?? [],
        dailyCosts: trends?.data?.map(trend => {
          const providers: Record<string, number> = {};
          
          // Distribute cost across providers
          summary.topProvidersBySpend?.forEach(provider => {
            providers[provider.name] = (trend.cost * provider.percentage) / 100;
          });

          return {
            date: trend.date,
            cost: trend.cost,
            providers,
          };
        }) ?? [],
        timeRange,
        lastUpdated: new Date().toISOString(),
      };

      return response;
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
    gcTime: 10 * 60 * 1000, // 10 minutes
    retry: 2,
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 30000),
  });
}

// System Performance Hook
export function useSystemPerformance() {
  return useQuery<SystemPerformance, Error>({
    queryKey: ['system-performance'],
    queryFn: async () => {
      // Initialize empty response structure
      const response: SystemPerformance = {
        metrics: {
          cpu: { usage: 0, cores: 0, loadAverage: [], temperature: null },
          memory: { total: 0, used: 0, percentage: 0, swap: { total: 0, used: 0 } },
          disk: { total: 0, used: 0, percentage: 0, io: { read: 0, write: 0 } },
          network: { in: 0, out: 0, connections: 0, latency: 0 },
          uptime: 0,
          processCount: 0,
          threadCount: 0,
        },
        history: [],
        services: [],
        alerts: [],
      };

      try {
        // Try to get system info
        const systemInfo = await withAdminClient(client => 
          client.system.getSystemInfo()
        ).catch(() => null);
        
        if (systemInfo) {
          const info = systemInfo as { uptime: number };
          response.metrics.uptime = info.uptime * 1000; // Convert to milliseconds
          
          // Basic service status from system info
          response.services = [
            {
              name: 'Conduit Core API',
              status: 'healthy',
              uptime: info.uptime * 1000,
              memory: 0,
              cpu: 0,
              lastCheck: new Date().toISOString(),
            }
          ];
        }
      } catch (error) {
        console.warn('Failed to fetch system info:', error);
      }

      try {
        // Try to get system metrics
        const systemMetrics = await withAdminClient(client => 
          client.monitoring.getSystemMetrics()
        ).catch(() => null);
        
        if (systemMetrics) {
          const metrics = systemMetrics as unknown as {
            cpu?: { usage: number; cores?: number[] };
            memory?: { total: number; used: number };
            disk?: { 
              devices?: Array<{ totalSpace: number; usedSpace: number; usagePercent: number }>;
              totalReadBytes?: number;
              totalWriteBytes?: number;
            };
            network?: { totalBytesReceived: number; totalBytesSent: number };
            processes?: Array<{ threads?: number }>;
          };
          
          response.metrics.cpu = {
            usage: metrics.cpu?.usage ?? 0,
            cores: metrics.cpu?.cores?.length ?? 0,
            loadAverage: [],
            temperature: null,
          };
          
          response.metrics.memory = {
            total: metrics.memory?.total ?? 0,
            used: metrics.memory?.used ?? 0,
            percentage: metrics.memory?.used && metrics.memory?.total 
              ? Math.round((metrics.memory.used / metrics.memory.total) * 100) 
              : 0,
            swap: { total: 0, used: 0 },
          };
          
          response.metrics.disk = {
            total: metrics.disk?.devices?.[0]?.totalSpace ?? 0,
            used: metrics.disk?.devices?.[0]?.usedSpace ?? 0,
            percentage: metrics.disk?.devices?.[0]?.usagePercent ?? 0,
            io: {
              read: metrics.disk?.totalReadBytes ?? 0,
              write: metrics.disk?.totalWriteBytes ?? 0,
            },
          };
          
          response.metrics.network = {
            in: metrics.network?.totalBytesReceived ?? 0,
            out: metrics.network?.totalBytesSent ?? 0,
            connections: 0,
            latency: 0,
          };
          
          response.metrics.processCount = metrics.processes?.length ?? 0;
          response.metrics.threadCount = metrics.processes?.reduce(
            (sum: number, p: { threads?: number }) => sum + (p.threads ?? 0), 0
          ) ?? 0;
        }
      } catch (error) {
        console.warn('Failed to fetch system metrics:', error);
      }

      try {
        // Try to get alerts
        const alertsResponse = await withAdminClient(client => 
          client.monitoring.listAlerts({ status: 'active' })
        ).catch(() => null);
        
        if (alertsResponse) {
          const alerts = Array.isArray(alertsResponse) ? alertsResponse : (alertsResponse as { data?: unknown[] }).data ?? [];
          response.alerts = alerts.map((alert: unknown) => {
            const typedAlert = alert as {
              id: string;
              condition?: { type: string };
              severity: string;
              name?: string;
              createdAt: string;
              status: string;
            };
            return {
              id: typedAlert.id,
              type: typedAlert.condition?.type ?? 'system',
              severity: typedAlert.severity,
              message: typedAlert.name ?? 'System alert',
              timestamp: typedAlert.createdAt,
              resolved: typedAlert.status === 'resolved',
            };
          });
        }
      } catch (error) {
        console.warn('Failed to fetch alerts:', error);
      }

      response.info = 'Data availability depends on monitoring service configuration';
      return response;
    },
    staleTime: 30 * 1000, // 30 seconds
    gcTime: 2 * 60 * 1000, // 2 minutes
    retry: 2,
    refetchInterval: 30 * 1000, // Auto-refresh every 30 seconds
  });
}