import { NextResponse } from 'next/server';

// GET /api/providers/[id]/models - Get available models for a specific provider
export async function GET() {
  // Provider model endpoints no longer exist in the API
  // Return empty array to indicate no models available
  return NextResponse.json([]);
}
