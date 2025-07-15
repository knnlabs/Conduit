import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ queueName: string; messageId: string }> }
) {
  const auth = requireAuth(request);
  if (!auth.isValid) {
    return auth.response!;
  }

  return NextResponse.json({ error: 'Error queue management not available' }, { status: 501 });
}

export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ queueName: string; messageId: string }> }
) {
  const auth = requireAuth(request);
  if (!auth.isValid) {
    return auth.response!;
  }

  return NextResponse.json({ error: 'Error queue management not available' }, { status: 501 });
}