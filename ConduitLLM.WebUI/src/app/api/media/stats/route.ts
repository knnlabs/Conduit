import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(request: NextRequest) {
  try {
    const searchParams = request.nextUrl.searchParams;
    const type = searchParams.get('type') ?? 'overall';
    const virtualKeyId = searchParams.get('virtualKeyId');

    const adminClient = getServerAdminClient();
    
    let data;
    
    if (type === 'by-provider') {
      data = await adminClient.media.getMediaStats('by-provider');
    } else if (type === 'by-type') {
      data = await adminClient.media.getMediaStats('by-type');
    } else if (type === 'virtual-key' && virtualKeyId) {
      data = await adminClient.media.getMediaStats('virtual-key', parseInt(virtualKeyId, 10));
    } else {
      data = await adminClient.media.getMediaStats('overall');
    }

    return NextResponse.json(data ?? {});
  } catch (error) {
    console.warn('Error fetching media stats:', error);
    return handleSDKError(error);
  }
}