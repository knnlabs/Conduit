import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';
import { authConfig } from '@/lib/auth/config';

// GET /api/auth/validate - Validate current session
export async function GET(request: NextRequest) {
  const auth = requireAuth(request);
  
  if (!auth.isValid) {
    return NextResponse.json(
      { valid: false, message: 'Invalid or expired session' },
      { status: 401 }
    );
  }
  
  // Session is valid
  return NextResponse.json({
    valid: true,
    message: 'Session is valid'
  });
}

export async function POST(request: NextRequest) {
  try {
    const { adminKey } = await request.json();

    if (!adminKey) {
      return NextResponse.json(
        { error: 'Admin key is required' },
        { status: 400 }
      );
    }

    // Check if auth is properly configured
    if (!authConfig.isConfigured()) {
      console.error('Authentication not properly configured');
      return NextResponse.json(
        { error: 'Server configuration error' },
        { status: 500 }
      );
    }

    // Validate the admin key using auth config service
    const isValid = authConfig.verifyAdminPassword(adminKey);

    if (!isValid) {
      return NextResponse.json(
        { error: 'Invalid admin key' },
        { status: 401 }
      );
    }

    // Generate session data
    const sessionId = 'session_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
    const expiresAt = new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(); // 24 hours

    // After successful auth, get WebUI virtual key from Admin SDK
    let virtualKey: string | undefined;
    try {
      const adminClient = getServerAdminClient();
      virtualKey = await adminClient.system.getWebUIVirtualKey();
    } catch (error) {
      // Don't fail authentication if virtual key retrieval fails
    }

    const response = NextResponse.json({
      success: true,
      sessionId,
      expiresAt,
      message: 'Admin access granted',
      virtualKey, // Include virtual key in response
    });

    // Set secure session cookie with admin access
    response.cookies.set('conduit_session', JSON.stringify({
      sessionId,
      isAuthenticated: true,
      expiresAt,
      masterKeyHash: 'admin', // This indicates admin access for the WebUI
      virtualKey, // Include virtual key in session
    }), {
      httpOnly: true, // Prevent XSS
      secure: process.env.NODE_ENV === 'production', // HTTPS only in production
      sameSite: 'strict', // CSRF protection
      maxAge: 24 * 60 * 60, // 24 hours in seconds
    });

    return response;

  } catch (error) {
    return handleSDKError(error);
  }
}
