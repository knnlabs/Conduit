import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerAdminClient } from '@/lib/server/adminClient';

// Mock provider health data generator
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
      
      // TODO: The SDK should provide a single method to get comprehensive health data
      // Currently we need to make multiple calls per provider which is inefficient
      
      // Get detailed health data for each provider
      const providerDetailsPromises = healthSummary.providers.map(async (provider: any) => {
        try {
          // For now, skip the methods that don't exist
          const health = await adminClient.providerHealth.getProviderHealth(provider.id || provider.name);
          const history = null as any; // Method doesn't exist yet
          const metrics = null as any; // Method doesn't exist yet
          
          return {
            id: provider.id || provider.name,
            name: provider.name,
            status: health.status || provider.status || 'unknown',
            uptime: (health as any).uptime || provider.uptime || 0,
            responseTime: (health as any).averageResponseTime || (health as any).responseTime || provider.responseTime || 0,
            errorRate: (health as any).errorRate || provider.errorRate || 0,
            successRate: (health as any).successRate || (100 - ((health as any).errorRate || 0)),
            lastCheck: (health as any).lastChecked || (health as any).lastCheck || provider.lastChecked || new Date().toISOString(),
            endpoints: metrics?.metrics?.endpoints || (health as any).endpoints || [],
            models: metrics?.metrics?.models || (health as any).models || [],
            rateLimit: metrics?.metrics?.rateLimit || (health as any).rateLimit || {
              requests: { used: 0, limit: 0, reset: new Date().toISOString() },
              tokens: { used: 0, limit: 0, reset: new Date().toISOString() },
            },
            recentIncidents: history.incidents || [],
            history: history.dataPoints || [],
          };
        } catch (error) {
          console.warn(`Failed to get details for provider ${provider.name}:`, error);
          // Return basic info if detailed fetch fails
          return {
            id: provider.id || provider.name,
            name: provider.name,
            status: provider.status || 'unknown',
            uptime: provider.uptime || 0,
            responseTime: provider.responseTime || 0,
            errorRate: provider.errorRate || 0,
            successRate: provider.successRate || 0,
            lastCheck: provider.lastChecked || new Date().toISOString(),
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
          availability: point.availability || (100 - (point.errorRate || 0)),
        }));
        
        // Create metrics from provider data (no separate metrics property)
        metrics[provider.id] = {
          totalRequests: 0, // Will be populated by separate SDK call if needed
          failedRequests: 0, // Will be populated by separate SDK call if needed
          avgResponseTime: provider.responseTime,
          p95ResponseTime: 0, // Will be populated by separate SDK call if needed
          p99ResponseTime: 0, // Will be populated by separate SDK call if needed
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
