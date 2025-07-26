import { NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

// GET /api/settings/system-info - Get system information
export async function GET() {
  try {
    const adminClient = getServerAdminClient();
    const systemInfo = await adminClient.system.getSystemInfo();
    return NextResponse.json(systemInfo);
  } catch (error) {
    return handleSDKError(error);
  }
}
