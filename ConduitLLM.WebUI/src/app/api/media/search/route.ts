import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(request: NextRequest) {
  try {
    const searchParams = request.nextUrl.searchParams;
    const pattern = searchParams.get('pattern');
    
    if (!pattern) {
      return NextResponse.json({ error: 'pattern is required' }, { status: 400 });
    }

    const adminClient = getServerAdminClient();
    const data = await adminClient.media.searchMedia(pattern);
    
    return NextResponse.json(data);
  } catch (error) {
    console.error('Error searching media:', error);
    return handleSDKError(error);
  }
}