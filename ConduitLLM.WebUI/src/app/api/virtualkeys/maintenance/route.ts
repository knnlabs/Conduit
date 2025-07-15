import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

// POST /api/virtualkeys/maintenance - Run maintenance on virtual keys
export async function POST(req: NextRequest) {

  try {
    const adminClient = getServerAdminClient();
    const result = await adminClient.virtualKeys.maintenance();
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}