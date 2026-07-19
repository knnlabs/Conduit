import { NextResponse } from 'next/server';

/**
 * Health check endpoint for WebAdmin.
 * Returns minimal information to avoid exposing sensitive details.
 * WebAdmin runs behind Clerk authentication, but this endpoint is intentionally
 * simple to support external monitoring services.
 */
export async function GET() {
  try {
    return NextResponse.json({ status: 'ok' });
  } catch {
    return NextResponse.json({ status: 'error' }, { status: 500 });
  }
}