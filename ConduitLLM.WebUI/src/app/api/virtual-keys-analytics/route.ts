import { NextResponse } from 'next/server';

export async function GET() {
  // This endpoint is deprecated - the analytics endpoints it relies on don't exist
  return NextResponse.json(
    { error: 'Virtual key analytics endpoints are not available in the backend API' },
    { status: 501 }
  );
}
