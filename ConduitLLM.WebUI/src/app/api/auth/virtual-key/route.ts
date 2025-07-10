import { NextRequest, NextResponse } from 'next/server';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerAdminClient } from '@/lib/server/adminClient';

/**
 * GET /api/auth/virtual-key
 * 
 * Returns the WebUI virtual key for authenticated admin sessions.
 * If the key doesn't exist, it creates one automatically.
 */
export async function GET(request: NextRequest) {
  const auth = requireAuth(request);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    
    // Get or create WebUI virtual key using Admin SDK
    const virtualKey = await adminClient.system.getWebUIVirtualKey();
    
    return NextResponse.json({ 
      virtualKey,
      source: 'server'
    });
    
  } catch (error) {
    console.error('Failed to retrieve virtual key:', error);
    
    // Try to get from session if available
    const sessionCookie = request.cookies.get('conduit_session');
    if (sessionCookie) {
      try {
        const session = JSON.parse(sessionCookie.value);
        if (session.virtualKey) {
          return NextResponse.json({ 
            virtualKey: session.virtualKey,
            source: 'session'
          });
        }
      } catch {
        // Ignore parse errors
      }
    }
    
    return NextResponse.json(
      { error: 'Failed to retrieve virtual key' },
      { status: 500 }
    );
  }
}