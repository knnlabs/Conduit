import { NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
// GET /api/config/routing/health - Get load balancer health
export async function GET() {

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
        const response = await adminClient.providers.list();
        
        // Handle paginated response or array
        let providers;
        if (Array.isArray(response)) {
          providers = response;
        } else if ('items' in response && Array.isArray(response.items)) {
          providers = response.items;
        } else {
          providers = [];
        }
        
        // Define a type that matches the actual API response
        type ApiProviderResponse = {
          id?: number;
          providerType?: number;
          providerName?: string;
          baseUrl?: string;
          apiBase?: string;
          isEnabled?: boolean;
          organization?: string;
        };
        
        const typedProviders = providers as ApiProviderResponse[];
        
        // Create health response from provider data
        const health = {
          status: typedProviders.some((p) => p.isEnabled === true) ? 'healthy' : 'unhealthy',
          lastCheck: new Date().toISOString(),
          nodes: typedProviders
            .filter((p) => p.isEnabled === true && p.id !== undefined)
            .map((provider) => ({
              id: (provider.id ?? 0).toString(),
              endpoint: provider.baseUrl ?? provider.apiBase ?? 'unknown',
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