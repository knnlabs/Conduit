import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';

export async function GET(request: NextRequest) {
  try {
    const adminClient = getServerAdminClient();
    const response = await adminClient.errorQueues.getHealth();
    
    return NextResponse.json(response);
  } catch (error) {
    return handleSDKError(error);
  }
}