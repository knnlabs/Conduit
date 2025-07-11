import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerAdminClient } from '@/lib/server/adminClient';

// Mock data for provider health - in production, this would come from actual health checks
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
  }));
}

export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    
    // Get all providers
    const providersResponse = await adminClient.providers.list();
    
    // Generate health data for each provider
    const providerHealth = generateProviderHealth(providersResponse.items);
    
    return NextResponse.json(providerHealth);
  } catch (error) {
    return handleSDKError(error);
  }
}
