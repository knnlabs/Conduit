import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ queueName: string }> }
) {
  try {
    const { queueName } = await params;
    const { searchParams } = new URL(request.url);
    const page = parseInt(searchParams.get('page') || '1', 10);
    const pageSize = parseInt(searchParams.get('pageSize') || '50', 10);
    const includeHeaders = searchParams.get('includeHeaders') === 'true';
    const includeBody = searchParams.get('includeBody') === 'true';

    const adminClient = getServerAdminClient();
    const response = await adminClient.errorQueues.getErrorMessages(queueName, {
      page,
      pageSize,
      includeHeaders,
      includeBody,
    });
    
    return NextResponse.json(response);
  } catch (error) {
    return handleSDKError(error);
  }
}