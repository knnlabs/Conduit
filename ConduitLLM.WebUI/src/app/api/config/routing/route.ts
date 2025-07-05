import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';

interface ModelMapping {
  id: number;
  modelId: string;
  providerModelId?: string;
  isEnabled: boolean;
  provider?: {
    providerName?: string;
  } | string;
}

interface Provider {
  id: number;
  providerName: string;
  isEnabled: boolean;
  baseUrl?: string;
  healthStatus?: string;
}

export const GET = withSDKAuth(
  async (_request, context) => {
    try {
      // Get router configuration from SDK
      const routerConfig = await withSDKErrorHandling(
        async () => context.adminClient!.settings.getRouterConfiguration(),
        'get router configuration'
      );

      // Get model mappings to build routing rules
      const modelMappings = await withSDKErrorHandling(
        async () => context.adminClient!.modelMappings.list({
          pageNumber: 1,
          pageSize: 100,
          isEnabled: true,
        }),
        'get model mappings'
      );

      // Get providers for load balancing info
      const providers = await withSDKErrorHandling(
        async () => context.adminClient!.providers.list({
          pageNumber: 1,
          pageSize: 100,
        }),
        'get providers'
      );

      // Build routing rules from model mappings
      const routingRules = modelMappings.map((mapping: ModelMapping) => ({
        id: String(mapping.id),
        modelAlias: mapping.modelId,
        providerModelName: mapping.providerModelId || mapping.modelId,
        isEnabled: mapping.isEnabled,
        provider: {
          name: typeof mapping.provider === 'object' ? mapping.provider?.providerName || 'Unknown' : mapping.provider || 'Unknown',
          isEnabled: providers.find((p: Provider) => p.providerName === (typeof mapping.provider === 'object' ? mapping.provider?.providerName : mapping.provider))?.isEnabled || false,
        },
      }));

      // Build load balancers from providers
      const loadBalancers = providers
        .filter((provider: Provider) => provider.isEnabled)
        .map((provider: Provider) => ({
          id: String(provider.id),
          name: provider.providerName,
          algorithm: routerConfig.routingStrategy || 'round-robin',
          healthCheckInterval: routerConfig.healthCheckInterval || 60,
          failoverThreshold: routerConfig.circuitBreakerThreshold || 5,
          endpoints: [
            {
              name: provider.providerName,
              url: provider.baseUrl || '',
              weight: 1,
              healthStatus: provider.healthStatus || 'unknown',
              responseTime: 0,
            },
          ],
        }));

      // Define retry policies based on router configuration
      const retryPolicies = [
        {
          id: 'default',
          name: 'Default Retry Policy',
          maxRetries: routerConfig.maxRetries || 3,
          initialDelay: routerConfig.retryDelay || 1000,
          maxDelay: 10000,
          backoffMultiplier: 2,
          retryableStatusCodes: [429, 502, 503, 504],
        },
        {
          id: 'aggressive',
          name: 'Aggressive Retry Policy',
          maxRetries: 5,
          initialDelay: 500,
          maxDelay: 30000,
          backoffMultiplier: 1.5,
          retryableStatusCodes: [429, 500, 502, 503, 504],
        },
        {
          id: 'conservative',
          name: 'Conservative Retry Policy',
          maxRetries: 2,
          initialDelay: 2000,
          maxDelay: 5000,
          backoffMultiplier: 2,
          retryableStatusCodes: [503, 504],
        },
      ];

      // Get real metrics from the metrics service
      let metricsData;
      try {
        metricsData = await withSDKErrorHandling(
          async () => context.adminClient!.metrics.getAllMetrics(),
          'get system metrics'
        );
      } catch (_error) {
        // If metrics aren't available, use defaults
        metricsData = null;
      }
      
      // Build statistics from real metrics or defaults
      const statistics = {
        totalRequests: metricsData?.metrics?.requests?.totalRequests || 0,
        providerDistribution: providers.map((provider: Provider) => ({
          provider: provider.providerName,
          requestCount: 0, // Would need provider-specific metrics
          successRate: metricsData ? (100 - (metricsData.metrics?.requests?.errorRate || 0)) / 100 : 0.95,
          avgLatency: metricsData?.metrics?.requests?.averageResponseTime || 0,
        })),
        failoverEvents: 0, // Not available in current metrics
        loadBalancerHealth: metricsData?.isHealthy ? 100 : 0,
      };

      // Build configuration object
      const configuration = {
        enableFailover: routerConfig.fallbackEnabled || true,
        enableLoadBalancing: routerConfig.loadBalancingEnabled || false,
        requestTimeoutSeconds: 30, // Default timeout
        circuitBreakerThreshold: routerConfig.circuitBreakerThreshold || 5,
        routingStrategy: routerConfig.routingStrategy || 'priority',
        maxRetries: routerConfig.maxRetries || 3,
        retryDelay: routerConfig.retryDelay || 1000,
        healthCheckEnabled: routerConfig.healthCheckEnabled || true,
        healthCheckInterval: routerConfig.healthCheckInterval || 60,
        circuitBreakerEnabled: routerConfig.circuitBreakerEnabled || true,
        circuitBreakerDuration: routerConfig.circuitBreakerDuration || 60,
      };

      const response = {
        timestamp: new Date().toISOString(),
        routingRules,
        loadBalancers,
        retryPolicies,
        statistics,
        configuration,
      };

      return transformSDKResponse(response);
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const PUT = withSDKAuth(
  async (request, context) => {
    try {
      const body = await request.json();
      
      // Build router configuration update
      const updateRequest: Record<string, unknown> = {};
      
      if (body.routingStrategy !== undefined) {
        updateRequest.routingStrategy = body.routingStrategy;
      }
      
      if (body.enableFailover !== undefined) {
        updateRequest.fallbackEnabled = body.enableFailover;
      }
      
      if (body.maxRetries !== undefined) {
        updateRequest.maxRetries = body.maxRetries;
      }
      
      if (body.retryDelay !== undefined) {
        updateRequest.retryDelay = body.retryDelay;
      }
      
      if (body.enableLoadBalancing !== undefined) {
        updateRequest.loadBalancingEnabled = body.enableLoadBalancing;
      }
      
      if (body.healthCheckEnabled !== undefined) {
        updateRequest.healthCheckEnabled = body.healthCheckEnabled;
      }
      
      if (body.healthCheckInterval !== undefined) {
        updateRequest.healthCheckInterval = body.healthCheckInterval;
      }
      
      if (body.circuitBreakerEnabled !== undefined) {
        updateRequest.circuitBreakerEnabled = body.circuitBreakerEnabled;
      }
      
      if (body.circuitBreakerThreshold !== undefined) {
        updateRequest.circuitBreakerThreshold = body.circuitBreakerThreshold;
      }
      
      if (body.circuitBreakerDuration !== undefined) {
        updateRequest.circuitBreakerDuration = body.circuitBreakerDuration;
      }
      
      // Update router configuration
      await withSDKErrorHandling(
        async () => context.adminClient!.settings.updateRouterConfiguration(updateRequest),
        'update router configuration'
      );
      
      return transformSDKResponse(
        {
          success: true,
          message: 'Routing configuration updated successfully',
          updatedFields: Object.keys(updateRequest).length,
        },
        { status: 200 }
      );
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);