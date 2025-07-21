import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';

// GET /api/admin/security/ip-rules - List all IP rules
export async function GET() {

  try {
    // IP filtering is not yet available in the current SDK version
    return NextResponse.json({ error: 'IP filtering not available' }, { status: 501 });
  } catch (error) {
    return handleSDKError(error);
  }
}

// POST /api/admin/security/ip-rules - Create new IP rule
export async function POST(req: NextRequest) {

  try {
    await req.json();
    
    // IP filtering is not yet available in the current SDK version
    return NextResponse.json({ error: 'IP filtering not available' }, { status: 501 });
  } catch (error) {
    return handleSDKError(error);
  }
}