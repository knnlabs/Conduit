import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(req: NextRequest) {

  try {
    const adminClient = getServerAdminClient();
    const health = await adminClient.system.getHealth();
    return NextResponse.json(health);
  } catch (error) {
    return handleSDKError(error);
  }
}
