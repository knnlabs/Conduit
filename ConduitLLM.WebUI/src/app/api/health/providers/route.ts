import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(req: NextRequest) {
  try {
    const adminClient = getServerAdminClient();
    
    // Use the SDK's getHealth method to get health status for all providers
    const healthResponse = await adminClient.providers.getHealth();
    
    // Transform the response to match the expected format for the UI
    const providerHealth = healthResponse.providers.map(provider => ({
      id: provider.id,
      name: provider.name,
      status: provider.status,
      lastChecked: provider.lastChecked,
      responseTime: provider.responseTime,
      uptime: provider.uptime,
      errorRate: provider.errorRate,
      successRate: 100 - provider.errorRate,
      details: provider.details,
    }));
    
    return NextResponse.json(providerHealth);
  } catch (error) {
    return handleSDKError(error);
  }
}
