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
    
    // Get media records and virtual key info
    const [mediaRecords, virtualKey] = await Promise.all([
      adminClient.media.getMediaByVirtualKey(parseInt(virtualKeyId, 10)),
      adminClient.virtualKeys.get(virtualKeyId).catch(() => null), // Fallback if key not found
    ]);
    
    // Get virtual key group info if we have a virtual key
    let virtualKeyGroup = null;
    if (virtualKey?.virtualKeyGroupId) {
      try {
        virtualKeyGroup = await adminClient.virtualKeyGroups.get(virtualKey.virtualKeyGroupId);
      } catch {
        // If group fetch fails, continue without group info
      }
    }
    
    // Enhance media records with virtual key group information
    const enhancedMediaRecords = (mediaRecords ?? []).map(record => ({
      ...record,
      virtualKeyGroupId: virtualKey?.virtualKeyGroupId,
      virtualKeyGroupName: virtualKeyGroup?.groupName,
    }));
    
    return NextResponse.json(enhancedMediaRecords);
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