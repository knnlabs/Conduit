import { NextRequest, NextResponse } from 'next/server';
import type { OverallMediaStorageStats, MediaStorageStats } from '@/app/media-assets/types';

export async function GET(request: NextRequest) {
  try {
    const searchParams = request.nextUrl.searchParams;
    const type = searchParams.get('type') ?? 'overall';
    const virtualKeyId = searchParams.get('virtualKeyId');

    let url = `${process.env.ADMIN_API_URL}/api/admin/media/stats`;
    
    if (type === 'by-provider') {
      url = `${process.env.ADMIN_API_URL}/api/admin/media/stats/by-provider`;
    } else if (type === 'by-type') {
      url = `${process.env.ADMIN_API_URL}/api/admin/media/stats/by-type`;
    } else if (type === 'virtual-key' && virtualKeyId) {
      url = `${process.env.ADMIN_API_URL}/api/admin/media/stats/virtual-key/${virtualKeyId}`;
    }

    const response = await fetch(url, {
      headers: new Headers([
        ['X-Master-Key', process.env.CONDUIT_MASTER_KEY ?? ''],
      ]),
    });

    if (!response.ok) {
      throw new Error(`Failed to fetch stats: ${response.statusText}`);
    }

    const data = await response.json() as OverallMediaStorageStats | MediaStorageStats | Record<string, number>;
    return NextResponse.json(data);
  } catch (error) {
    console.error('Error fetching media stats:', error);
    return NextResponse.json(
      { error: 'Failed to fetch media stats' },
      { status: 500 }
    );
  }
}