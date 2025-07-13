import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

export async function GET(request: NextRequest) {
  const auth = requireAuth(request);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const result = await adminClient.errorQueues.getHealth();
    
    return NextResponse.json(result);
  } catch (error: any) {
    console.error('Error fetching error queue health:', error);
    return NextResponse.json(
      { error: error.message || 'Failed to fetch error queue health' },
      { status: error.statusCode || 500 }
    );
  }
}