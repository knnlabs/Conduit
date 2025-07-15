import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ queueName: string }> }
) {
  // Management operations not available in Admin SDK yet
  return NextResponse.json({ error: 'Queue clearing not implemented - Admin SDK management operations needed' }, { status: 501 });
}