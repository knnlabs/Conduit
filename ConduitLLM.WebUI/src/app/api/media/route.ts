import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(request: NextRequest) {
  try {
    const searchParams = request.nextUrl.searchParams;
    const virtualKeyId = searchParams.get('virtualKeyId');
    
    if (!virtualKeyId) {
      return NextResponse.json({ error: 'virtualKeyId is required' }, { status: 400 });
    }

    const adminClient = getServerAdminClient();
    const data = await adminClient.media.getMediaByVirtualKey(parseInt(virtualKeyId, 10));
    
    return NextResponse.json(data ?? []);
  } catch (error) {
    console.warn('Error fetching media:', error);
    return handleSDKError(error);
  }
}

export async function DELETE(request: NextRequest) {
  try {
    const body = await request.json() as { mediaId?: string };
    const { mediaId } = body;

    if (!mediaId) {
      return NextResponse.json({ error: 'mediaId is required' }, { status: 400 });
    }

    const adminClient = getServerAdminClient();
    const data = await adminClient.media.deleteMedia(mediaId);
    
    return NextResponse.json(data);
  } catch (error) {
    console.warn('Error deleting media:', error);
    return handleSDKError(error);
  }
}