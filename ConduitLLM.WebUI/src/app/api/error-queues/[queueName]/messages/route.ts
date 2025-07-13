import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ queueName: string }> }
) {
  const auth = requireAuth(request);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const { searchParams } = new URL(request.url);
    const { queueName: rawQueueName } = await params;
    const queueName = decodeURIComponent(rawQueueName);
    
    const options = {
      page: searchParams.get('page') 
        ? parseInt(searchParams.get('page')!) 
        : undefined,
      pageSize: searchParams.get('pageSize') 
        ? parseInt(searchParams.get('pageSize')!) 
        : undefined,
      includeHeaders: searchParams.get('includeHeaders') === 'true',
      includeBody: searchParams.get('includeBody') === 'true',
    };

    const result = await adminClient.errorQueues.getErrorMessages(queueName, options);
    
    return NextResponse.json(result);
  } catch (error: any) {
    console.error('Error fetching error messages:', error);
    return NextResponse.json(
      { error: error.message || 'Failed to fetch error messages' },
      { status: error.statusCode || 500 }
    );
  }
}