import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function POST(request: NextRequest) {
  try {
    const { adminKey } = await request.json();

    if (!adminKey) {
      return NextResponse.json(
        { error: 'Admin key is required' },
        { status: 400 }
      );
    }

    // Get the admin key from environment variable
    const validAdminKey = process.env.CONDUIT_WEBUI_AUTH_KEY;

    if (!validAdminKey) {
      console.error('CONDUIT_WEBUI_AUTH_KEY environment variable is not set');
      return NextResponse.json(
        { error: 'Server configuration error' },
        { status: 500 }
      );
    }

    // Validate the admin key
    const isValid = adminKey === validAdminKey;

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
      console.error('Failed to get WebUI virtual key:', error);
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
    console.error('Auth validation error:', error);
    return NextResponse.json(
      { error: 'Authentication failed' },
      { status: 500 }
    );
  }
}