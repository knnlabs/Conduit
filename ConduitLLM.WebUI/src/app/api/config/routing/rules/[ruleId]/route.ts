import { NextResponse } from 'next/server';
// PATCH /api/config/routing/rules/[ruleId] - Update a routing rule
export async function PATCH() {
  // Routing rules endpoint no longer exists in the API
  // Return error response
  return NextResponse.json(
    { error: 'Routing rules are no longer supported in the API' },
    { status: 501 }
  );
}

// DELETE /api/config/routing/rules/[ruleId] - Delete a routing rule
export async function DELETE() {
  // Routing rules endpoint no longer exists in the API
  // Return error response
  return NextResponse.json(
    { error: 'Routing rules are no longer supported in the API' },
    { status: 501 }
  );
}