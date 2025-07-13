import { NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';
import { authConfig } from './config';

// Import Clerk server helper, always available because package is installed.
// If CONDUIT_AUTH_TYPE !== 'clerk' the call is skipped.
import { auth as clerkAuth } from '@clerk/nextjs/server';

export interface Session {
  userId?: string;
  /** True when authenticated regardless of system */
  isAuthenticated: boolean;
  /** ISO date when session expires (password mode) */
  expiresAt?: string;
  /** Raw session cookie (password mode) */
  raw?: any;
}

function unauthorizedJson() {
  return new NextResponse(
    JSON.stringify({ error: 'Unauthorized' }),
    { status: 401, headers: { 'Content-Type': 'application/json' } }
  );
}

/**
 * getSession – SSR safe helper that returns Session or null regardless of auth system.
 */
export function getSession(request?: NextRequest): Session | null {
  if (authConfig.isClerk()) {
    const { userId } = clerkAuth();
    if (!userId) return null;
    return {
      userId,
      isAuthenticated: true,
    };
  }

  // password mode
  const cookieValue = request
    ? request.cookies.get('conduit_session')?.value
    : cookies().get('conduit_session')?.value;
  if (!cookieValue) return null;
  try {
    const parsed = JSON.parse(cookieValue);
    if (!parsed.isAuthenticated) return null;
    if (parsed.expiresAt && new Date(parsed.expiresAt) < new Date()) {
      return null;
    }
    return {
      ...parsed,
      isAuthenticated: true,
    };
  } catch {
    return null;
  }
}

/**
 * requireAuth – API route helper that enforces authentication and returns NextResponse on failure.
 */
export function requireAuth(request: NextRequest): { isValid: boolean; response?: NextResponse; session?: Session } {
  const session = getSession(request);
  if (!session) {
    return { isValid: false, response: unauthorizedJson() };
  }
  return { isValid: true, session };
}
