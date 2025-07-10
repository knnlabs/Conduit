import { NextRequest, NextResponse } from 'next/server';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const systemInfo = await adminClient.system.getSystemInfo();
    return NextResponse.json(systemInfo);
  } catch (error) {
    console.error('Failed to get system info:', error);
    return NextResponse.json(
      { error: 'Failed to get system info' },
      { status: 500 }
    );
  }
}