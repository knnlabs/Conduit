import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
// GET /api/health/providers/[id] - Get health status for a specific provider
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    
    // Get health status for the specific provider
    const healthResponse = await adminClient.providers.getHealth(id);
    
    // The response should contain the provider in the providers array
    const providerHealth = healthResponse.providers?.[0];
    
    if (!providerHealth) {
      return NextResponse.json(
        { error: 'Provider health not found' },
        { status: 404 }
      );
    }
    
    // Also get detailed metrics for this provider
    const searchParams = req.nextUrl.searchParams;
    const timeRange = searchParams.get('timeRange');
    
    if (searchParams.get('includeMetrics') === 'true') {
      const metricsResponse = await adminClient.providers.getHealthMetrics(
        id,
        timeRange || undefined
      );
      
      return NextResponse.json({
        ...providerHealth,
        metrics: metricsResponse.metrics,
        incidents: metricsResponse.incidents,
      });
    }
    
    return NextResponse.json(providerHealth);
  } catch (error) {
    return handleSDKError(error);
  }
}
