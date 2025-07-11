import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { authConfig } from '@/lib/auth/config';

// POST /api/auth/login - Admin login endpoint
export async function POST(request: NextRequest) {
  try {
    const { password } = await request.json();

    if (!password) {
      return NextResponse.json(
        { error: 'Password is required' },
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

    // Validate the password using auth config service
    const isValid = authConfig.verifyAdminPassword(password);

    if (!isValid) {
      return NextResponse.json(
        { error: 'Invalid password' },
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
      console.error('Failed to get WebUI virtual key:', error);
      // Don't fail authentication if virtual key retrieval fails
    }

    const response = NextResponse.json({
      success: true,
      sessionId,
      expiresAt,
      message: 'Login successful',
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