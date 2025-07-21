import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';

// POST /api/admin/security/ip-rules/test - Test an IP address
export async function POST(req: NextRequest) {

  try {
    await req.json();
    
    // IP filtering is not yet available in the current SDK version
    return NextResponse.json({ error: 'IP filtering not available' }, { status: 501 });
  } catch (error) {
    return handleSDKError(error);
  }
}