import { NextRequest, NextResponse } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';

/**
 * POST /api/auth/refresh
 * 
 * Refreshes the current authenticated session.
 * Returns updated session data with new expiration time.
 */
export const POST = withSDKAuth(
  async (request: NextRequest, context) => {
    try {
      const { session } = context;
      
      if (!session || !session.isAuthenticated) {
        return NextResponse.json(
          { error: 'No active session' },
          { status: 401 }
        );
      }
      
      // Calculate new expiration time (24 hours from now)
      const expiresAt = new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString();
      
      // Create updated session
      const updatedSession = {
        ...session,
        expiresAt,
        lastRefreshed: new Date().toISOString()
      };
      
      const response = NextResponse.json({
        success: true,
        expiresAt,
        message: 'Session refreshed successfully'
      });
      
      // Update session cookie with new expiration
      response.cookies.set('conduit_session', JSON.stringify(updatedSession), {
        httpOnly: true,
        secure: process.env.NODE_ENV === 'production',
        sameSite: 'strict',
        maxAge: 24 * 60 * 60, // 24 hours in seconds
      });
      
      return response;
      
    } catch (error) {
      console.error('Session refresh error:', error);
      return NextResponse.json(
        { error: 'Failed to refresh session' },
        { status: 500 }
      );
    }
  },
  { requireAdmin: true }
);