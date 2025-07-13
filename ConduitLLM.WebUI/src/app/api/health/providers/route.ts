import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerAdminClient } from '@/lib/server/adminClient';

// TODO: Remove this mock data generator when SDK provides provider health endpoints
// SDK methods needed:
// - adminClient.providers.getHealth(providerId?: string) - for individual provider health
// - adminClient.providers.listWithHealth() - for all providers with health status
// - adminClient.providers.getHealthMetrics(providerId: string) - for detailed health metrics
function generateProviderHealth(providers: any[]) {
  return providers.map(provider => ({
    id: provider.id.toString(),
    name: provider.name,
    status: provider.isEnabled 
      ? Math.random() > 0.1 ? 'healthy' : Math.random() > 0.5 ? 'degraded' : 'unhealthy'
      : 'unknown',
    lastChecked: new Date().toISOString(),
    responseTime: Math.floor(Math.random() * 200) + 50,
    uptime: 95 + Math.random() * 4.9,
    errorRate: Math.random() * 10,
    details: Math.random() > 0.8 ? {
      lastError: 'Connection timeout',
      consecutiveFailures: Math.floor(Math.random() * 5),
      lastSuccessfulCheck: new Date(Date.now() - Math.random() * 3600000).toISOString(),
    } : undefined,
    _warning: 'Health data is simulated. SDK provider health methods are not yet available.',
  }));
}

export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    
    // Try to get provider health data from SDK
    try {
      // TODO: Replace with actual SDK call when available
      // const providerHealth = await adminClient.providers.listWithHealth();
      // return NextResponse.json(providerHealth);
      
      // For now, check if the SDK has provider health methods
      // @ts-ignore - SDK methods may not be typed yet
      if (adminClient.providers && adminClient.providers.listWithHealth) {
        // @ts-ignore - SDK methods may not be typed yet
        const providerHealth = await adminClient.providers.listWithHealth();
        return NextResponse.json(providerHealth);
      }
    } catch (sdkError) {
      console.warn('SDK provider health methods not available:', sdkError);
    }
    
    // Fallback: Get all providers and generate mock health data
    const providersResponse = await adminClient.providers.list();
    const providerHealth = generateProviderHealth(providersResponse.items);
    
    return NextResponse.json(providerHealth);
  } catch (error) {
    return handleSDKError(error);
  }
}
