import { NextRequest, NextResponse } from 'next/server';

export async function POST(request: NextRequest) {
  try {
    const body = await request.json() as { type?: string; daysToKeep?: number };
    const { type, daysToKeep } = body;

    let url = `${process.env.ADMIN_API_URL}/api/admin/media/cleanup/`;
    
    switch (type) {
      case 'expired':
        url += 'expired';
        break;
      case 'orphaned':
        url += 'orphaned';
        break;
      case 'prune':
        url += 'prune';
        break;
      default:
        return NextResponse.json({ error: 'Invalid cleanup type' }, { status: 400 });
    }

    const response = await fetch(url, {
      method: 'POST',
      headers: new Headers([
        ['X-Master-Key', process.env.CONDUIT_MASTER_KEY ?? ''],
        ['Content-Type', 'application/json'],
      ]),
      body: type === 'prune' ? JSON.stringify({ daysToKeep }) : undefined,
    });

    if (!response.ok) {
      throw new Error(`Failed to cleanup media: ${response.statusText}`);
    }

    const data = await response.json() as { message: string; deletedCount: number };
    return NextResponse.json(data);
  } catch (error) {
    console.error('Error cleaning up media:', error);
    return NextResponse.json(
      { error: 'Failed to cleanup media' },
      { status: 500 }
    );
  }
}