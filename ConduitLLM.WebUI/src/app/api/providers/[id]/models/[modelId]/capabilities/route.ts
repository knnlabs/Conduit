import { NextResponse } from 'next/server';

// GET /api/providers/[id]/models/[modelId]/capabilities - Get model capabilities
export async function GET() {
  // Provider model endpoints no longer exist in the API
  // Return a 501 Not Implemented error
  return NextResponse.json(
    { error: 'Model capabilities endpoint is no longer supported in the API' },
    { status: 501 }
  );
}