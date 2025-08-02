import { NextResponse } from 'next/server';

// GET /api/providers/[id]/models/[modelId] - Get details for a specific model
export async function GET() {
  // Provider model endpoints no longer exist in the API
  // Return error response
  return NextResponse.json(
    { error: 'Model details endpoint is no longer supported in the API' },
    { status: 501 }
  );
}