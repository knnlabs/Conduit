import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerAdminClient } from '@/lib/server/adminClient';

// Fallback provider health data generator for when SDK calls fail
function generateMockProviderHealth(range: string) {
  const providers = [
    { id: 'openai', name: 'OpenAI' },
    { id: 'anthropic', name: 'Anthropic' },
    { id: 'azure', name: 'Azure OpenAI' },
    { id: 'google', name: 'Google AI' },
    { id: 'replicate', name: 'Replicate' },
    { id: 'cohere', name: 'Cohere' },
  ];

  const now = Date.now();
  const ranges = {
    '1h': { hours: 1, points: 12 },
    '24h': { hours: 24, points: 24 },
    '7d': { hours: 168, points: 28 },
    '30d': { hours: 720, points: 30 },
  };

  const { hours, points } = ranges[range as keyof typeof ranges] || ranges['24h'];

  // Generate provider health data
  const providerHealthData = providers.map(provider => {
    const isHealthy = Math.random() > 0.15;
    const isDegraded = !isHealthy && Math.random() > 0.5;
    const status = isHealthy ? 'healthy' : isDegraded ? 'degraded' : 'down';
    
    return {
      id: provider.id,
      name: provider.name,
      status: status as 'healthy' | 'degraded' | 'down',
      uptime: isHealthy ? 99.5 + Math.random() * 0.49 : isDegraded ? 95 + Math.random() * 4 : 85 + Math.random() * 10,
      responseTime: Math.floor(Math.random() * 200) + 100,
      errorRate: isHealthy ? Math.random() * 2 : isDegraded ? 2 + Math.random() * 8 : 10 + Math.random() * 20,
      successRate: 0,
      lastCheck: new Date().toISOString(),
      endpoints: [
        {
          name: '/v1/chat/completions',
          status: status as 'healthy' | 'degraded' | 'down',
          responseTime: Math.floor(Math.random() * 300) + 150,
          lastCheck: new Date().toISOString(),
        },
        {
          name: '/v1/completions',
          status: Math.random() > 0.1 ? 'healthy' : 'degraded' as 'healthy' | 'degraded' | 'down',
          responseTime: Math.floor(Math.random() * 250) + 100,
          lastCheck: new Date().toISOString(),
        },
        {
          name: '/v1/embeddings',
          status: 'healthy' as 'healthy' | 'degraded' | 'down',
          responseTime: Math.floor(Math.random() * 150) + 50,
          lastCheck: new Date().toISOString(),
        },
        {
          name: '/v1/models',
          status: 'healthy' as 'healthy' | 'degraded' | 'down',
          responseTime: Math.floor(Math.random() * 100) + 20,
          lastCheck: new Date().toISOString(),
        },
      ],
      models: [
        {
          name: provider.id === 'openai' ? 'gpt-4' : provider.id === 'anthropic' ? 'claude-3-opus' : `${provider.id}-large`,
          available: Math.random() > 0.1,
          responseTime: Math.floor(Math.random() * 400) + 200,
          tokenCapacity: {
            used: Math.floor(Math.random() * 900000),
            total: 1000000,
          },
        },
        {
          name: provider.id === 'openai' ? 'gpt-3.5-turbo' : provider.id === 'anthropic' ? 'claude-3-sonnet' : `${provider.id}-medium`,
          available: true,
          responseTime: Math.floor(Math.random() * 300) + 150,
          tokenCapacity: {
            used: Math.floor(Math.random() * 1900000),
            total: 2000000,
          },
        },
      ],
      rateLimit: {
        requests: {
          used: Math.floor(Math.random() * 9000),
          limit: 10000,
          reset: new Date(now + 60 * 60 * 1000).toISOString(),
        },
        tokens: {
          used: Math.floor(Math.random() * 900000),
          limit: 1000000,
          reset: new Date(now + 60 * 60 * 1000).toISOString(),
        },
      },
      recentIncidents: status === 'healthy' ? [] : [
        {
          id: `incident-${provider.id}-1`,
          timestamp: new Date(now - Math.random() * 24 * 60 * 60 * 1000).toISOString(),
          type: status === 'down' ? 'outage' : 'degradation' as 'outage' | 'degradation' | 'rate_limit',
          duration: Math.floor(Math.random() * 60 * 60 * 1000),
          message: status === 'down' ? 'Complete service outage' : 'Increased response times',
          resolved: Math.random() > 0.3,
        },
      ],
    };
  });

  // Calculate success rate
  providerHealthData.forEach(provider => {
    provider.successRate = 100 - provider.errorRate;
  });

  // Generate health history
  const history: Record<string, any[]> = {};
  
  providers.forEach(provider => {
    history[provider.id] = [];
    for (let i = points - 1; i >= 0; i--) {
      const timestamp = new Date(now - (i * (hours / points) * 60 * 60 * 1000));
      history[provider.id].push({
        timestamp: timestamp.toISOString(),
        responseTime: Math.floor(Math.random() * 200) + 100,
        errorRate: Math.random() * 5,
        availability: 95 + Math.random() * 5,
      });
    }
  });

  // Generate metrics
  const metrics: Record<string, any> = {};
  
  providers.forEach(provider => {
    metrics[provider.id] = {
      totalRequests: Math.floor(Math.random() * 100000) + 50000,
      failedRequests: Math.floor(Math.random() * 2000) + 500,
      avgResponseTime: Math.floor(Math.random() * 200) + 150,
      p95ResponseTime: Math.floor(Math.random() * 300) + 250,
      p99ResponseTime: Math.floor(Math.random() * 500) + 400,
      availability: 95 + Math.random() * 5,
    };
  });

  return {
    providers: providerHealthData,
    history,
    metrics,
  };
}

export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { searchParams } = new URL(req.url);
    const range = searchParams.get('range') || '24h';
    const adminClient = getServerAdminClient();
    
    try {
      // Get health summary for all providers
      const healthSummary = await adminClient.providerHealth.getHealthSummary();
      
      // Calculate date range for history
      const now = new Date();
      const ranges = {
        '1h': 1,
        '24h': 24,
        '7d': 168,
        '30d': 720,
      };
      const hours = ranges[range as keyof typeof ranges] || 24;
      const startDate = new Date(now.getTime() - hours * 60 * 60 * 1000).toISOString();
      const endDate = now.toISOString();
      
      // Get detailed health data for each provider
      const providerDetailsPromises = healthSummary.providers.map(async (provider: any) => {
        try {
          // Get detailed health data
          const [health, performance, healthHistory] = await Promise.all([
            adminClient.providerHealth.getProviderHealth(provider.providerId || provider.id || provider.name),
            adminClient.providerHealth.getProviderPerformance(
              provider.providerId || provider.id || provider.name,
              { startDate, endDate }
            ),
            adminClient.providerHealth.getProviderHealthHistory(
              provider.providerId || provider.id || provider.name,
              { startDate, endDate, resolution: 'hour', includeIncidents: true }
            ),
          ]);
          
          // Get recent alerts/incidents
          const alerts = await adminClient.providerHealth.getHealthAlerts({
            providerId: provider.providerId || provider.id || provider.name,
            startDate,
            endDate,
          });
          
          return {
            id: health.providerId || provider.providerId || provider.id || provider.name,
            name: health.providerName || provider.name || provider.providerId,
            status: health.status || 'unknown',
            uptime: health.metrics?.uptime?.percentage || 0,
            responseTime: health.metrics?.latency?.avg || performance.latency?.avg || 0,
            errorRate: health.metrics?.errors?.rate || performance.errors?.rate || 0,
            successRate: 100 - (health.metrics?.errors?.rate || performance.errors?.rate || 0),
            lastCheck: health.metrics?.uptime?.since || new Date().toISOString(),
            endpoints: Object.entries(health.details || {}).map(([name, check]: [string, any]) => ({
              name: `/${name}`,
              status: check.status || 'unknown',
              responseTime: check.latency || 0,
              lastCheck: check.lastChecked || new Date().toISOString(),
            })),
            models: [], // TODO: Get from provider models endpoint if needed
            rateLimit: {
              requests: { 
                used: health.details?.quotaUsage?.details?.used || 0, 
                limit: health.details?.quotaUsage?.details?.limit || 0, 
                reset: new Date().toISOString() 
              },
              tokens: { used: 0, limit: 0, reset: new Date().toISOString() },
            },
            recentIncidents: alerts?.filter((alert: any) => 
              alert.severity === 'critical' || alert.severity === 'error'
            ).map((alert: any) => ({
              id: alert.id,
              timestamp: alert.timestamp,
              type: alert.type === 'outage' ? 'outage' : alert.type === 'performance' ? 'degradation' : 'rate_limit',
              duration: alert.duration || 0,
              message: alert.message,
              resolved: alert.resolved,
            })) || [],
            history: healthHistory.dataPoints || [],
          };
        } catch (error) {
          console.warn(`Failed to get details for provider ${provider.name}:`, error);
          // Return basic info if detailed fetch fails
          return {
            id: provider.providerId || provider.id || provider.name,
            name: provider.name || provider.providerId,
            status: provider.status || 'unknown',
            uptime: provider.uptimePercentage || provider.uptime || 0,
            responseTime: provider.averageResponseTime || provider.responseTime || 0,
            errorRate: provider.errorRate || 0,
            successRate: provider.successRate || (100 - (provider.errorRate || 0)),
            lastCheck: provider.lastCheck || new Date().toISOString(),
            endpoints: [],
            models: [],
            rateLimit: {
              requests: { used: 0, limit: 0, reset: new Date().toISOString() },
              tokens: { used: 0, limit: 0, reset: new Date().toISOString() },
            },
            recentIncidents: [],
            history: [],
          };
        }
      });
      
      const providers = await Promise.all(providerDetailsPromises);
      
      // Transform history data into the expected format
      const history: Record<string, any[]> = {};
      const metrics: Record<string, any> = {};
      
      providers.forEach(provider => {
        history[provider.id] = provider.history.map((point: any) => ({
          timestamp: point.timestamp,
          responseTime: point.averageResponseTime || point.responseTime || 0,
          errorRate: point.errorRate || 0,
          availability: point.availability || point.uptimePercentage || (100 - (point.errorRate || 0)),
        }));
        
        // Create metrics from provider data
        metrics[provider.id] = {
          totalRequests: provider.history.reduce((sum: number, p: any) => sum + (p.requestCount || 0), 0),
          failedRequests: provider.history.reduce((sum: number, p: any) => sum + (p.failedRequests || 0), 0),
          avgResponseTime: provider.responseTime,
          p95ResponseTime: provider.history.reduce((max: number, p: any) => Math.max(max, p.p95ResponseTime || 0), 0),
          p99ResponseTime: provider.history.reduce((max: number, p: any) => Math.max(max, p.p99ResponseTime || 0), 0),
          availability: provider.uptime,
        };
      });
      
      return NextResponse.json({
        providers,
        history,
        metrics,
      });
    } catch (sdkError) {
      // If SDK calls fail, fall back to mock data
      console.warn('Failed to fetch provider health from SDK, using mock data:', sdkError);
      const healthData = generateMockProviderHealth(range);
      
      return NextResponse.json({
        ...healthData,
        _warning: 'Using mock data due to SDK error.',
      });
    }
  } catch (error) {
    return handleSDKError(error);
  }
}
