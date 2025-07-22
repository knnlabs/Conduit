import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { getServerCoreClient } from '@/lib/server/coreClient';

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const timeRange = searchParams.get('range') ?? '7d';
    
    const adminClient = getServerAdminClient();
    
    // Get Core SDK client for real-time metrics (with fallback)
    let coreClient = null;
    try {
      coreClient = await getServerCoreClient();
    } catch (error) {
      console.warn('Core SDK client unavailable, continuing without real-time metrics:', error);
    }
    
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
      // Fetch data from Admin SDK and Core SDK in parallel
      const promises = [
        adminClient.costDashboard.getCostSummary(
          'daily', // timeframe
          startDate.toISOString().split('T')[0], // Extract date part only
          now.toISOString().split('T')[0]
        ),
        adminClient.analytics.getRequestLogs({
          startDate: startDate.toISOString(),
          endDate: now.toISOString(),
          pageSize: 100, // Maximum allowed page size
          page: 1
        }),
        // Previous period data for change calculations
        adminClient.costDashboard.getCostSummary(
          'daily', // timeframe
          previousStartDate.toISOString().split('T')[0],
          previousEndDate.toISOString().split('T')[0]
        ).catch(() => null), // Allow this to fail gracefully
        
        // Real-time metrics from Core SDK (with fallback - check if metrics service exists)
        (coreClient && 'metrics' in coreClient) ? 
          (coreClient as { metrics: { getCurrentMetrics: () => Promise<unknown> } }).metrics.getCurrentMetrics().catch(() => null) : 
          Promise.resolve(null),
        (coreClient && 'metrics' in coreClient) ? 
          (coreClient as { metrics: { getKPISummary: () => Promise<unknown> } }).metrics.getKPISummary().catch(() => null) : 
          Promise.resolve(null)
      ];

      const [
        currentCostSummary,
        currentRequestLogs,
        previousCostSummary,
        currentMetrics,
        kpiSummary
      ] = await Promise.all(promises);

      // Import types for proper typing
      type CostDashboardDto = { 
        totalCost: number; 
        last24HoursCost: number;
        last7DaysCost: number;
        last30DaysCost: number;
        topModelsBySpend: Array<{ name: string; cost: number; percentage: number }>;
        topProvidersBySpend: Array<{ name: string; cost: number; percentage: number }>;
        topVirtualKeysBySpend: Array<{ name: string; cost: number; percentage: number }>;
      };
      type RequestLogPage = { totalCount: number; items: Array<{ 
        virtualKeyName?: string;
        timestamp: string;
        cost?: number;
        inputTokens?: number;
        outputTokens?: number;
        provider?: string;
        model?: string;
        status?: string;
        duration?: number;
      }> };
      type KPISummary = { 
        business?: { 
          activeVirtualKeys?: number;
          costBurnRatePerHour?: number;
          averageCostPerRequest?: number;
        };
        systemHealth?: {
          overallHealthPercentage?: number;
          errorRate?: number;
          responseTimeP95?: number;
          activeConnections?: number;
        };
        performance?: {
          requestsPerSecond?: number;
          activeRequests?: number;
          averageResponseTime?: number;
        };
      };

      // Calculate main metrics from available data, enhanced with real-time metrics
      const totalRequests = (currentRequestLogs as RequestLogPage)?.totalCount ?? (currentRequestLogs as RequestLogPage)?.items?.length ?? 0;
      const totalCost = (currentCostSummary as CostDashboardDto)?.totalCost ?? 0;
      // Calculate total tokens from request logs since CostDashboard doesn't provide it
      const totalTokens = (currentRequestLogs as RequestLogPage)?.items?.reduce((sum, log) => 
        sum + (log.inputTokens ?? 0) + (log.outputTokens ?? 0), 0) ?? 0;
      
      // Get unique virtual keys count (enhanced with real-time data if available)
      const uniqueKeysFromLogs = new Set(
        ((currentRequestLogs as RequestLogPage)?.items ?? [])
          .filter((log) => log.virtualKeyName)
          .map((log) => log.virtualKeyName)
      ).size;
      
      // Use real-time active virtual keys count if available, otherwise fall back to log data
      const activeVirtualKeys = ((kpiSummary as KPISummary)?.business?.activeVirtualKeys) ?? uniqueKeysFromLogs;

      // Calculate change percentages (derive from cost summary data)
      let requestsChange = 0;
      const typedPreviousCostSummary = previousCostSummary as CostDashboardDto | null;
      
      // Since CostDashboard doesn't provide request counts, we'll calculate from logs
      // For now, set to 0 since we don't have previous period logs
      requestsChange = 0;
      
      const costChange = (typedPreviousCostSummary && typedPreviousCostSummary.totalCost > 0)
        ? ((totalCost - typedPreviousCostSummary.totalCost) / typedPreviousCostSummary.totalCost) * 100 
        : 0;
      // Since we calculate tokens from logs, we can't get previous period tokens easily
      const tokensChange = 0;
      const virtualKeysChange = 0; // This would require historical tracking

      // Build metrics object with real-time enhancements
      const typedKpiSummary = kpiSummary as KPISummary | null;
      const metrics = {
        totalRequests,
        totalCost,
        totalTokens,
        activeVirtualKeys,
        requestsChange: isFinite(requestsChange) ? requestsChange : 0,
        costChange: isFinite(costChange) ? costChange : 0,
        tokensChange: isFinite(tokensChange) ? tokensChange : 0,
        virtualKeysChange,
        // Add real-time system health indicators if available
        ...(typedKpiSummary ? {
          systemHealth: {
            overallHealthPercentage: typedKpiSummary.systemHealth?.overallHealthPercentage,
            errorRate: typedKpiSummary.systemHealth?.errorRate,
            responseTimeP95: typedKpiSummary.systemHealth?.responseTimeP95,
            activeConnections: typedKpiSummary.systemHealth?.activeConnections
          }
        } : {}),
        // Add real-time performance metrics if available
        ...(typedKpiSummary ? {
          realTimeMetrics: {
            requestsPerSecond: typedKpiSummary.performance?.requestsPerSecond,
            activeRequests: typedKpiSummary.performance?.activeRequests,
            averageResponseTime: typedKpiSummary.performance?.averageResponseTime,
            costBurnRatePerHour: typedKpiSummary.business?.costBurnRatePerHour,
            averageCostPerRequest: typedKpiSummary.business?.averageCostPerRequest
          }
        } : {})
      };

      // Generate time series data by grouping request logs by time buckets
      interface TimeBucket {
        requests: number;
        cost: number;
        tokens: number;
      }
      const timeSeriesMap = new Map<number, TimeBucket>();
      const bucketSize = timeRange === '24h' ? 3600000 : 86400000; // 1 hour or 1 day in ms
      
      const typedRequestLogs = currentRequestLogs as RequestLogPage;
      typedRequestLogs.items.forEach((log) => {
        const bucketTime = Math.floor(new Date(log.timestamp).getTime() / bucketSize) * bucketSize;
        const bucket: TimeBucket = timeSeriesMap.get(bucketTime) ?? { requests: 0, cost: 0, tokens: 0 };
        bucket.requests += 1;
        bucket.cost += log.cost ?? 0;
        bucket.tokens += (log.inputTokens ?? 0) + (log.outputTokens ?? 0);
        timeSeriesMap.set(bucketTime, bucket);
      });

      const timeSeries = Array.from(timeSeriesMap.entries())
        .sort(([a], [b]) => a - b)
        .map(([timestamp, data]: [number, TimeBucket]) => ({
          timestamp: new Date(timestamp).toISOString(),
          ...data
        }));

      // Enhanced provider usage calculation with statistical analysis
      interface ProviderStats {
        requests: number;
        cost: number;
        tokens: number;
        errors: number;
        totalDuration: number;
        minCost: number;
        maxCost: number;
        avgResponseTime: number;
      }
      const providerMap = new Map<string, ProviderStats>();
      
      typedRequestLogs.items.forEach((log) => {
        const provider = log.provider ?? 'unknown';
        const existing: ProviderStats = providerMap.get(provider) ?? { 
          requests: 0, 
          cost: 0, 
          tokens: 0, 
          errors: 0,
          totalDuration: 0,
          minCost: Infinity,
          maxCost: 0,
          avgResponseTime: 0
        };
        
        existing.requests += 1;
        existing.cost += log.cost ?? 0;
        existing.tokens += (log.inputTokens ?? 0) + (log.outputTokens ?? 0);
        existing.totalDuration += log.duration ?? 0;
        
        // Track cost statistics
        if (log.cost) {
          existing.minCost = Math.min(existing.minCost, log.cost);
          existing.maxCost = Math.max(existing.maxCost, log.cost);
        }
        
        // Track errors
        if (log.status && log.status !== 'success' && log.status !== '200') {
          existing.errors += 1;
        }
        
        providerMap.set(provider, existing);
      });

      // Calculate totals for percentage calculations
      const totalProviderRequests = Array.from(providerMap.values()).reduce((sum: number, p: ProviderStats) => sum + p.requests, 0);
      const totalProviderCost = Array.from(providerMap.values()).reduce((sum: number, p: ProviderStats) => sum + p.cost, 0);
      
      // Enhanced provider usage with statistics
      const providerUsage = Array.from(providerMap.entries()).map(([provider, data]: [string, ProviderStats]) => ({
        provider,
        requests: data.requests,
        cost: data.cost,
        tokens: data.tokens,
        percentage: totalProviderRequests > 0 ? (data.requests / totalProviderRequests) * 100 : 0,
        costPercentage: totalProviderCost > 0 ? (data.cost / totalProviderCost) * 100 : 0,
        errorRate: data.requests > 0 ? (data.errors / data.requests) * 100 : 0,
        avgResponseTime: data.requests > 0 ? data.totalDuration / data.requests : 0,
        avgCostPerRequest: data.requests > 0 ? data.cost / data.requests : 0,
        minCost: data.minCost === Infinity ? 0 : data.minCost,
        maxCost: data.maxCost
      })).sort((a, b) => b.requests - a.requests); // Sort by usage

      // Enhanced model usage calculation with performance metrics
      interface ModelStats {
        requests: number;
        cost: number;
        tokens: number;
        provider: string;
        model: string;
        errors: number;
        totalDuration: number;
        inputTokens: number;
        outputTokens: number;
        successfulRequests: number;
      }
      const modelMap = new Map<string, ModelStats>();
      typedRequestLogs.items.forEach((log) => {
        const model = log.model ?? 'unknown';
        const provider = log.provider ?? 'unknown';
        const key = `${provider}/${model}`;
        const existing: ModelStats = modelMap.get(key) ?? { 
          requests: 0, 
          cost: 0, 
          tokens: 0, 
          provider, 
          model,
          errors: 0,
          totalDuration: 0,
          inputTokens: 0,
          outputTokens: 0,
          successfulRequests: 0
        };
        
        existing.requests += 1;
        existing.cost += log.cost ?? 0;
        existing.inputTokens += log.inputTokens ?? 0;
        existing.outputTokens += log.outputTokens ?? 0;
        existing.tokens = existing.inputTokens + existing.outputTokens;
        existing.totalDuration += log.duration ?? 0;
        
        // Track success/error rates
        if (log.status === 'success' || log.status === '200') {
          existing.successfulRequests += 1;
        } else {
          existing.errors += 1;
        }
        
        modelMap.set(key, existing);
      });

      // Enhanced model usage with performance statistics      
      const modelUsage = Array.from(modelMap.values())
        .map((model: ModelStats) => ({
          model: model.model,
          provider: model.provider,
          requests: model.requests,
          cost: model.cost,
          tokens: model.tokens,
          inputTokens: model.inputTokens,
          outputTokens: model.outputTokens,
          avgCostPerRequest: model.requests > 0 ? model.cost / model.requests : 0,
          avgTokensPerRequest: model.requests > 0 ? model.tokens / model.requests : 0,
          avgResponseTime: model.requests > 0 ? model.totalDuration / model.requests : 0,
          successRate: model.requests > 0 ? (model.successfulRequests / model.requests) * 100 : 0,
          errorRate: model.requests > 0 ? (model.errors / model.requests) * 100 : 0,
          efficiency: model.cost > 0 ? model.tokens / model.cost : 0 // Tokens per dollar
        }))
        .sort((a, b) => b.requests - a.requests)
        .slice(0, 10); // Top 10 models by usage

      // Enhanced virtual key usage calculation with performance analytics
      interface VirtualKeyStats {
        requests: number;
        cost: number;
        tokens: number;
        lastUsed: string;
        firstUsed: string;
        errors: number;
        totalDuration: number;
        uniqueModels: Set<string>;
        uniqueProviders: Set<string>;
        successfulRequests: number;
      }
      const keyMap = new Map<string, VirtualKeyStats>();
      typedRequestLogs.items.forEach((log) => {
        if (!log.virtualKeyName) return;
        const existing: VirtualKeyStats = keyMap.get(log.virtualKeyName) ?? { 
          requests: 0, 
          cost: 0, 
          tokens: 0, 
          lastUsed: log.timestamp,
          firstUsed: log.timestamp,
          errors: 0,
          totalDuration: 0,
          uniqueModels: new Set<string>(),
          uniqueProviders: new Set<string>(),
          successfulRequests: 0
        };
        
        existing.requests += 1;
        existing.cost += log.cost ?? 0;
        existing.tokens += (log.inputTokens ?? 0) + (log.outputTokens ?? 0);
        existing.totalDuration += log.duration ?? 0;
        
        // Track usage patterns
        if (log.model) existing.uniqueModels.add(log.model);
        if (log.provider) existing.uniqueProviders.add(log.provider);
        
        // Track timestamps
        if (new Date(log.timestamp) > new Date(existing.lastUsed)) {
          existing.lastUsed = log.timestamp;
        }
        if (new Date(log.timestamp) < new Date(existing.firstUsed)) {
          existing.firstUsed = log.timestamp;
        }
        
        // Track success/error rates
        if (log.status === 'success' || log.status === '200') {
          existing.successfulRequests += 1;
        } else {
          existing.errors += 1;
        }
        
        keyMap.set(log.virtualKeyName, existing);
      });

      // Enhanced virtual key usage with analytics
      const virtualKeyUsage = Array.from(keyMap.entries())
        .map(([keyName, data]: [string, VirtualKeyStats]) => {
          const daysSinceFirstUsed = Math.max(1, Math.ceil((new Date().getTime() - new Date(data.firstUsed).getTime()) / (1000 * 60 * 60 * 24)));
          const avgRequestsPerDay = data.requests / daysSinceFirstUsed;
          
          return {
            keyName,
            requests: data.requests,
            cost: data.cost,
            tokens: data.tokens,
            lastUsed: data.lastUsed,
            firstUsed: data.firstUsed,
            avgCostPerRequest: data.requests > 0 ? data.cost / data.requests : 0,
            avgResponseTime: data.requests > 0 ? data.totalDuration / data.requests : 0,
            successRate: data.requests > 0 ? (data.successfulRequests / data.requests) * 100 : 0,
            errorRate: data.requests > 0 ? (data.errors / data.requests) * 100 : 0,
            uniqueModelsUsed: data.uniqueModels.size,
            uniqueProvidersUsed: data.uniqueProviders.size,
            avgRequestsPerDay,
            daysSinceFirstUsed,
            efficiency: data.cost > 0 ? data.tokens / data.cost : 0
          };
        })
        .sort((a, b) => b.requests - a.requests)
        .slice(0, 10); // Top 10 virtual keys by usage

      // Enhanced endpoint performance analysis
      interface EndpointStats {
        requests: number;
        totalDuration: number;
        errors: number;
        successfulRequests: number;
        minDuration: number;
        maxDuration: number;
        durations: number[];
      }
      const endpointMap = new Map<string, EndpointStats>();
      typedRequestLogs.items.forEach((log) => {
        const endpoint = '/chat/completions'; // Most requests go to this endpoint
        const existing: EndpointStats = endpointMap.get(endpoint) ?? {
          requests: 0,
          totalDuration: 0,
          errors: 0,
          successfulRequests: 0,
          minDuration: Infinity,
          maxDuration: 0,
          durations: []
        };
        
        existing.requests += 1;
        const duration = log.duration ?? 0;
        existing.totalDuration += duration;
        
        if (duration > 0) {
          existing.minDuration = Math.min(existing.minDuration, duration);
          existing.maxDuration = Math.max(existing.maxDuration, duration);
          existing.durations.push(duration);
        }
        
        if (log.status === 'success' || log.status === '200') {
          existing.successfulRequests += 1;
        } else {
          existing.errors += 1;
        }
        
        endpointMap.set(endpoint, existing);
      });

      // Calculate advanced endpoint metrics
      const endpointUsage = Array.from(endpointMap.entries())
        .map(([endpoint, data]: [string, EndpointStats]) => {
          // Calculate percentiles
          const sortedDurations = data.durations.sort((a: number, b: number) => a - b);
          const p50 = sortedDurations[Math.floor(sortedDurations.length * 0.5)] ?? 0;
          const p95 = sortedDurations[Math.floor(sortedDurations.length * 0.95)] ?? 0;
          const p99 = sortedDurations[Math.floor(sortedDurations.length * 0.99)] ?? 0;
          
          return {
            endpoint,
            requests: data.requests,
            avgDuration: data.requests > 0 ? data.totalDuration / data.requests : 0,
            minDuration: data.minDuration === Infinity ? 0 : data.minDuration,
            maxDuration: data.maxDuration,
            p50Duration: p50,
            p95Duration: p95,
            p99Duration: p99,
            errorRate: data.requests > 0 ? (data.errors / data.requests) * 100 : 0,
            successRate: data.requests > 0 ? (data.successfulRequests / data.requests) * 100 : 0,
            requestsPerMinute: data.requests / ((now.getTime() - startDate.getTime()) / (1000 * 60)),
            // Use real-time metrics if available for comparison
            realTimeAvgDuration: typedKpiSummary?.performance?.averageResponseTime,
            realTimeErrorRate: typedKpiSummary?.systemHealth?.errorRate
          };
        })
        .slice(0, 5); // Top 5 endpoints

      const response = {
        metrics,
        timeSeries,
        providerUsage,
        modelUsage,
        virtualKeyUsage,
        endpointUsage,
        timeRange,
        lastUpdated: new Date().toISOString(),
        // Include information about data sources
        dataSources: {
          historicalData: true, // Always available from Admin SDK
          realTimeMetrics: !!currentMetrics && !!kpiSummary, // Available if Core SDK connected
          coreSDKConnected: !!coreClient
        }
      };

      return NextResponse.json(response);

    } catch (sdkError) {
      // Enhanced error handling with detailed information for partial failures
      console.warn('Analytics data fetch failed, attempting graceful recovery:', sdkError);
      
      // Try to get at least basic data if possible
      let partialData = null;
      const errorDetails = {
        type: 'partial_failure',
        message: 'Unable to fetch some analytics data',
        timestamp: new Date().toISOString(),
        services: {
          adminSDK: false,
          coreSDK: false,
          costSummary: false,
          requestLogs: false,
          realTimeMetrics: false
        }
      };

      try {
        // Attempt to get cost summary separately as a fallback
        const fallbackCostSummary = await adminClient.costDashboard.getCostSummary(
          'daily', // timeframe
          startDate.toISOString().split('T')[0],
          now.toISOString().split('T')[0]
        );
        
        partialData = {
          totalCost: fallbackCostSummary.totalCost ?? 0,
          totalTokens: 0, // Not available from CostDashboard
          // Basic provider data from cost summary
          providerUsage: fallbackCostSummary.topProvidersBySpend?.map((provider: { name: string; cost: number; percentage: number }) => ({
            provider: provider.name,
            requests: 0, // Not available from CostDashboard
            cost: provider.cost,
            tokens: 0,
            percentage: provider.percentage
          })) ?? []
        };
        
        errorDetails.services.adminSDK = true;
        errorDetails.services.costSummary = true;
        errorDetails.message = 'Partial analytics data available - request logs and real-time metrics unavailable';
        
      } catch (fallbackError) {
        console.error('Complete analytics failure:', fallbackError);
        errorDetails.message = 'Analytics services temporarily unavailable';
      }

      // Return structured response with available data and error information
      const response = {
        metrics: {
          totalRequests: 0,
          totalCost: partialData?.totalCost ?? 0,
          totalTokens: partialData?.totalTokens ?? 0,
          activeVirtualKeys: 0,
          requestsChange: 0,
          costChange: 0,
          tokensChange: 0,
          virtualKeysChange: 0
        },
        timeSeries: [],
        providerUsage: partialData?.providerUsage ?? [],
        modelUsage: [],
        virtualKeyUsage: [],
        endpointUsage: [],
        timeRange,
        lastUpdated: new Date().toISOString(),
        // Enhanced error information for frontend handling
        error: errorDetails,
        warning: errorDetails.message,
        dataSources: {
          historicalData: errorDetails.services.costSummary,
          realTimeMetrics: false,
          coreSDKConnected: !!coreClient,
          partialDataAvailable: !!partialData
        }
      };

      return NextResponse.json(response, { status: 200 }); // Return 200 for partial data
    }

  } catch (error) {
    return handleSDKError(error);
  }
}
