import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/config/routing/health - Get load balancer health
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    
    try {
      // Try to fetch load balancer health from SDK
      const health = await adminClient.configuration.getLoadBalancerHealth();
      return NextResponse.json(health);
    } catch (error) {
      console.warn('Failed to fetch load balancer health:', error);
      
      // Fallback to provider health check
      try {
        const response = await adminClient.providers.list(1, 100);
        const providers = response.items || response || [];
        
        // Create health response from provider data
        const health = {
          status: providers.some((p: any) => p.isEnabled) ? 'healthy' : 'unhealthy',
          lastCheck: new Date().toISOString(),
          nodes: providers
            .filter((p: any) => p.isEnabled)
            .map((provider: any) => ({
              id: provider.id,
              endpoint: provider.endpoint || provider.apiEndpoint || 'unknown',
              status: provider.isEnabled ? 'healthy' : 'disabled',
              weight: 100,
              totalRequests: 0,
              avgResponseTime: 0,
              activeConnections: 0,
              lastHealthCheck: new Date().toISOString()
            })),
          distribution: {}
        };
        
        return NextResponse.json(health);
      } catch (fallbackError) {
        console.warn('Failed to fetch providers for health check:', fallbackError);
        
        // Return minimal health response
        return NextResponse.json({
          status: 'unknown',
          lastCheck: new Date().toISOString(),
          nodes: [],
          distribution: {}
        });
      }
    }
  } catch (error) {
    console.error('Error fetching load balancer health:', error);
    return handleSDKError(error);
  }
}