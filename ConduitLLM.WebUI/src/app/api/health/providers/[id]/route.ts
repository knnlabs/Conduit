import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/health/providers/[id] - Get health status for a specific provider
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    
    // Get health summary for all providers
    const healthSummary = await adminClient.providerHealth.getHealthSummary();
    
    // Find the specific provider's health by ID
    const providerHealth = healthSummary.providers?.find(
      (p: any) => p.providerId === parseInt(id, 10)
    );
    
    if (!providerHealth) {
      return NextResponse.json(
        { error: 'Provider health not found' },
        { status: 404 }
      );
    }
    
    return NextResponse.json(providerHealth);
  } catch (error) {
    return handleSDKError(error);
  }
}
