import { NextRequest, NextResponse } from 'next/server';

export async function GET(req: NextRequest) {
  // This endpoint is deprecated - the analytics endpoints it relies on don't exist
  return NextResponse.json(
    { error: 'This analytics endpoint is no longer available' },
    { status: 501 }
  );
}
