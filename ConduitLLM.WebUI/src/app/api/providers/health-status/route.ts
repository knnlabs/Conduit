import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
// GET /api/providers/health-status - Get health status for all providers
export async function GET(req: NextRequest) {

  try {
    const adminClient = getServerAdminClient();
    const healthStatus = await adminClient.providers.getHealthStatus();
    return NextResponse.json(healthStatus);
  } catch (error) {
    return handleSDKError(error);
  }
}