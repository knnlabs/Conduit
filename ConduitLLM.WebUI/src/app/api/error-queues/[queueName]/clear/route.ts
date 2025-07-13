import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ queueName: string }> }
) {
  const auth = requireAuth(request);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    // TODO: Implement clear queue endpoint in Admin API
    // For now, return a mock response
    return NextResponse.json({ success: true });
  } catch (error: any) {
    console.error('Error clearing queue:', error);
    return NextResponse.json(
      { error: error.message || 'Failed to clear queue' },
      { status: error.statusCode || 500 }
    );
  }
}