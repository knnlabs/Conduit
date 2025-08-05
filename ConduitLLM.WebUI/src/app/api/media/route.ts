import { NextRequest, NextResponse } from 'next/server';
import type { MediaRecord } from '@/app/media-assets/types';

export async function GET(request: NextRequest) {
  try {
    const searchParams = request.nextUrl.searchParams;
    const virtualKeyId = searchParams.get('virtualKeyId');
    
    if (!virtualKeyId) {
      return NextResponse.json({ error: 'virtualKeyId is required' }, { status: 400 });
    }

    const response = await fetch(
      `${process.env.ADMIN_API_URL}/api/admin/media/virtual-key/${virtualKeyId}`,
      {
        headers: new Headers([
          ['X-Master-Key', process.env.CONDUIT_MASTER_KEY ?? ''],
        ]),
      }
    );

    if (!response.ok) {
      throw new Error(`Failed to fetch media: ${response.statusText}`);
    }

    const data = await response.json() as MediaRecord[];
    return NextResponse.json(data);
  } catch (error) {
    console.error('Error fetching media:', error);
    return NextResponse.json(
      { error: 'Failed to fetch media' },
      { status: 500 }
    );
  }
}

export async function DELETE(request: NextRequest) {
  try {
    const body = await request.json() as { mediaId?: string };
    const { mediaId } = body;

    if (!mediaId) {
      return NextResponse.json({ error: 'mediaId is required' }, { status: 400 });
    }

    const response = await fetch(
      `${process.env.ADMIN_API_URL}/api/admin/media/${mediaId}`,
      {
        method: 'DELETE',
        headers: new Headers([
          ['X-Master-Key', process.env.CONDUIT_MASTER_KEY ?? ''],
        ]),
      }
    );

    if (!response.ok) {
      throw new Error(`Failed to delete media: ${response.statusText}`);
    }

    const data = await response.json() as { message: string };
    return NextResponse.json(data);
  } catch (error) {
    console.error('Error deleting media:', error);
    return NextResponse.json(
      { error: 'Failed to delete media' },
      { status: 500 }
    );
  }
}