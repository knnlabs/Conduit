import { NextResponse } from 'next/server';

// POST /api/providers/[id]/models/refresh - Refresh models for a provider
export async function POST() {
  // Provider model endpoints no longer exist in the API
  // Return error response
  return NextResponse.json(
    { error: 'Model refresh endpoint is no longer supported in the API' },
    { status: 501 }
  );
}