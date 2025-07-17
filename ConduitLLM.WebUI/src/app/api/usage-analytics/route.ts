import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { getServerCoreClient } from '@/lib/server/coreClient';

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const timeRange = searchParams.get('range') || '7d';
    
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
        adminClient.analytics.getCostSummary(
          startDate.toISOString().split('T')[0], // Extract date part only
          now.toISOString().split('T')[0]
        ),
        adminClient.analytics.getRequestLogs({
          startDate: startDate.toISOString(),
          endDate: now.toISOString(),
          pageSize: 1000,
          page: 1
        }),
        // Previous period data for change calculations
        adminClient.analytics.getCostSummary(
          previousStartDate.toISOString().split('T')[0],
          previousEndDate.toISOString().split('T')[0]
        ).catch(() => null), // Allow this to fail gracefully
        
        // Real-time metrics from Core SDK (with fallback - check if metrics service exists)
        (coreClient && 'metrics' in coreClient) ? (coreClient as any).metrics.getCurrentMetrics().catch(() => null) : Promise.resolve(null),
        (coreClient && 'metrics' in coreClient) ? (coreClient as any).metrics.getKPISummary().catch(() => null) : Promise.resolve(null)
      ];

      const [
        currentCostSummary,
        currentRequestLogs,
        previousCostSummary,
        currentMetrics,
        kpiSummary
      ] = await Promise.all(promises);

      // Calculate main metrics from available data, enhanced with real-time metrics
      const totalRequests = currentRequestLogs.totalCount || currentRequestLogs.items?.length || 0;
      const totalCost = currentCostSummary.totalCost;
      const totalTokens = currentCostSummary.totalInputTokens + currentCostSummary.totalOutputTokens;
      
      // Get unique virtual keys count (enhanced with real-time data if available)
      const uniqueKeysFromLogs = new Set(
        currentRequestLogs.items
          .filter((log: any) => log.virtualKeyName)
          .map((log: any) => log.virtualKeyName)
      ).size;
      
      // Use real-time active virtual keys count if available, otherwise fall back to log data
      const activeVirtualKeys = (kpiSummary?.business?.activeVirtualKeys) ?? uniqueKeysFromLogs;

      // Calculate change percentages (derive from cost summary data)
      let requestsChange = 0;
      if (previousCostSummary && currentCostSummary.costByKey.length > 0 && previousCostSummary.costByKey.length > 0) {
        const currentTotalRequests = currentCostSummary.costByKey.reduce((sum: number, key: any) => sum + key.requestCount, 0);
        const previousTotalRequests = previousCostSummary.costByKey.reduce((sum: number, key: any) => sum + key.requestCount, 0);
        if (previousTotalRequests > 0) {
          requestsChange = ((currentTotalRequests - previousTotalRequests) / previousTotalRequests) * 100;
        }
      }
      
      const costChange = previousCostSummary 
        ? ((totalCost - previousCostSummary.totalCost) / previousCostSummary.totalCost) * 100 
        : 0;
      const tokensChange = previousCostSummary 
        ? (((currentCostSummary.totalInputTokens + currentCostSummary.totalOutputTokens) - 
            (previousCostSummary.totalInputTokens + previousCostSummary.totalOutputTokens)) / 
           (previousCostSummary.totalInputTokens + previousCostSummary.totalOutputTokens)) * 100 
        : 0;
      const virtualKeysChange = 0; // This would require historical tracking

      // Build metrics object with real-time enhancements
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
        ...(kpiSummary && {
          systemHealth: {
            overallHealthPercentage: kpiSummary.systemHealth?.overallHealthPercentage,
            errorRate: kpiSummary.systemHealth?.errorRate,
            responseTimeP95: kpiSummary.systemHealth?.responseTimeP95,
            activeConnections: kpiSummary.systemHealth?.activeConnections
          }
        }),
        // Add real-time performance metrics if available
        ...(kpiSummary && {
          realTimeMetrics: {
            requestsPerSecond: kpiSummary.performance?.requestsPerSecond,
            activeRequests: kpiSummary.performance?.activeRequests,
            averageResponseTime: kpiSummary.performance?.averageResponseTime,
            costBurnRatePerHour: kpiSummary.business?.costBurnRatePerHour,
            averageCostPerRequest: kpiSummary.business?.averageCostPerRequest
          }
        })
      };

      // Generate time series data by grouping request logs by time buckets
      const timeSeriesMap = new Map();
      const bucketSize = timeRange === '24h' ? 3600000 : 86400000; // 1 hour or 1 day in ms
      
      currentRequestLogs.items.forEach((log: any) => {
        const bucketTime = Math.floor(new Date(log.timestamp).getTime() / bucketSize) * bucketSize;
        const bucket = timeSeriesMap.get(bucketTime) || { requests: 0, cost: 0, tokens: 0 };
        bucket.requests += 1;
        bucket.cost += log.cost;
        bucket.tokens += log.inputTokens + log.outputTokens;
        timeSeriesMap.set(bucketTime, bucket);
      });

      const timeSeries = Array.from(timeSeriesMap.entries())
        .sort(([a], [b]) => a - b)
        .map(([timestamp, data]) => ({
          timestamp: new Date(timestamp).toISOString(),
          ...data
        }));

      // Enhanced provider usage calculation with statistical analysis
      const providerMap = new Map();
      const providerErrorMap = new Map(); // Track errors by provider
      
      currentRequestLogs.items.forEach((log: any) => {
        const provider = log.provider || 'unknown';
        const existing = providerMap.get(provider) || { 
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
        existing.cost += log.cost || 0;
        existing.tokens += (log.inputTokens || 0) + (log.outputTokens || 0);
        existing.totalDuration += log.duration || 0;
        
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
      const totalProviderRequests = Array.from(providerMap.values()).reduce((sum: number, p: any) => sum + p.requests, 0);
      const totalProviderCost = Array.from(providerMap.values()).reduce((sum: number, p: any) => sum + p.cost, 0);
      
      // Enhanced provider usage with statistics
      const providerUsage = Array.from(providerMap.entries()).map(([provider, data]: [string, any]) => ({
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
      const modelMap = new Map();
      currentRequestLogs.items.forEach((log: any) => {
        const model = log.model || 'unknown';
        const provider = log.provider || 'unknown';
        const key = `${provider}/${model}`;
        const existing = modelMap.get(key) || { 
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
        existing.cost += log.cost || 0;
        existing.inputTokens += log.inputTokens || 0;
        existing.outputTokens += log.outputTokens || 0;
        existing.tokens = existing.inputTokens + existing.outputTokens;
        existing.totalDuration += log.duration || 0;
        
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
        .map((model: any) => ({
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
      const keyMap = new Map();
      currentRequestLogs.items.forEach((log: any) => {
        if (!log.virtualKeyName) return;
        const existing = keyMap.get(log.virtualKeyName) || { 
          requests: 0, 
          cost: 0, 
          tokens: 0, 
          lastUsed: log.timestamp,
          firstUsed: log.timestamp,
          errors: 0,
          totalDuration: 0,
          uniqueModels: new Set(),
          uniqueProviders: new Set(),
          successfulRequests: 0
        };
        
        existing.requests += 1;
        existing.cost += log.cost || 0;
        existing.tokens += (log.inputTokens || 0) + (log.outputTokens || 0);
        existing.totalDuration += log.duration || 0;
        
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
        .map(([keyName, data]: [string, any]) => {
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
      const endpointMap = new Map();
      currentRequestLogs.items.forEach((log: any) => {
        const endpoint = '/chat/completions'; // Most requests go to this endpoint
        const existing = endpointMap.get(endpoint) || {
          requests: 0,
          totalDuration: 0,
          errors: 0,
          successfulRequests: 0,
          minDuration: Infinity,
          maxDuration: 0,
          durations: [] as number[]
        };
        
        existing.requests += 1;
        const duration = log.duration || 0;
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
        .map(([endpoint, data]: [string, any]) => {
          // Calculate percentiles
          const sortedDurations = data.durations.sort((a: number, b: number) => a - b);
          const p50 = sortedDurations[Math.floor(sortedDurations.length * 0.5)] || 0;
          const p95 = sortedDurations[Math.floor(sortedDurations.length * 0.95)] || 0;
          const p99 = sortedDurations[Math.floor(sortedDurations.length * 0.99)] || 0;
          
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
            realTimeAvgDuration: kpiSummary?.performance?.averageResponseTime,
            realTimeErrorRate: kpiSummary?.systemHealth?.errorRate
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
      let errorDetails = {
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
        const fallbackCostSummary = await adminClient.analytics.getCostSummary(
          startDate.toISOString().split('T')[0],
          now.toISOString().split('T')[0]
        );
        
        partialData = {
          totalCost: fallbackCostSummary.totalCost || 0,
          totalTokens: (fallbackCostSummary.totalInputTokens || 0) + (fallbackCostSummary.totalOutputTokens || 0),
          // Basic provider data from cost summary
          providerUsage: fallbackCostSummary.costByProvider?.map((provider: any) => ({
            provider: provider.providerName || provider.providerId,
            requests: provider.requestCount,
            cost: provider.cost,
            tokens: 0,
            percentage: 0 // Will be calculated if we have total requests
          })) || []
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
          totalCost: partialData?.totalCost || 0,
          totalTokens: partialData?.totalTokens || 0,
          activeVirtualKeys: 0,
          requestsChange: 0,
          costChange: 0,
          tokensChange: 0,
          virtualKeysChange: 0
        },
        timeSeries: [],
        providerUsage: partialData?.providerUsage || [],
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
