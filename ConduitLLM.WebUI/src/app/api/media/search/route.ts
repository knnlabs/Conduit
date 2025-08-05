import { NextRequest, NextResponse } from 'next/server';
import type { MediaRecord } from '@/app/media-assets/types';

export async function GET(request: NextRequest) {
  try {
    const searchParams = request.nextUrl.searchParams;
    const pattern = searchParams.get('pattern');
    
    if (!pattern) {
      return NextResponse.json({ error: 'pattern is required' }, { status: 400 });
    }

    const response = await fetch(
      `${process.env.ADMIN_API_URL}/api/admin/media/search?pattern=${encodeURIComponent(pattern)}`,
      {
        headers: new Headers([
          ['X-Master-Key', process.env.CONDUIT_MASTER_KEY ?? ''],
        ]),
      }
    );

    if (!response.ok) {
      throw new Error(`Failed to search media: ${response.statusText}`);
    }

    const data = await response.json() as MediaRecord[];
    return NextResponse.json(data);
  } catch (error) {
    console.error('Error searching media:', error);
    return NextResponse.json(
      { error: 'Failed to search media' },
      { status: 500 }
    );
  }
}