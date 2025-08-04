import { NextResponse } from 'next/server';
// GET /api/config/routing/rules - Get all routing rules
export async function GET() {
  // Routing rules endpoint no longer exists in the API
  // Return empty array to indicate no rules are configured
  return NextResponse.json([]);
}

// POST /api/config/routing/rules - Create a new routing rule
export async function POST() {
  // Routing rules endpoint no longer exists in the API
  // Return error response
  return NextResponse.json(
    { error: 'Routing rules are no longer supported in the API' },
    { status: 501 }
  );
}

// PUT /api/config/routing/rules - Bulk update routing rules
export async function PUT() {
  // Routing rules endpoint no longer exists in the API
  // Return error response
  return NextResponse.json(
    { error: 'Routing rules are no longer supported in the API' },
    { status: 501 }
  );
}