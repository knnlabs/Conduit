import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';

export async function GET(request: NextRequest) {
  try {
    const { searchParams } = new URL(request.url);
    const includeEmpty = searchParams.get('includeEmpty') === 'true';
    const minMessages = searchParams.get('minMessages') ? parseInt(searchParams.get('minMessages')!) : undefined;
    const queueNameFilter = searchParams.get('queueNameFilter') || undefined;

    const adminClient = getServerAdminClient();
    const response = await adminClient.errorQueues.getErrorQueues({
      includeEmpty,
      minMessages,
      queueNameFilter,
    });
    
    return NextResponse.json(response);
  } catch (error) {
    return handleSDKError(error);
  }
}