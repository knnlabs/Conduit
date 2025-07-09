import { NextRequest, NextResponse } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { getWebUIVirtualKey, ensureWebUIVirtualKey } from '@/utils/virtualKeyManagement';

/**
 * GET /api/auth/virtual-key
 * 
 * Returns the WebUI virtual key for authenticated admin sessions.
 * If the key doesn't exist, it creates one automatically.
 */
export const GET = withSDKAuth(
  async (request: NextRequest, context) => {
    try {
      const { adminClient } = context;
      
      if (!adminClient) {
        return NextResponse.json(
          { error: 'Admin client not available' },
          { status: 500 }
        );
      }
      
      // Try to get existing virtual key
      let virtualKey = await getWebUIVirtualKey(adminClient);
      
      if (!virtualKey) {
        // Key doesn't exist, create a new one
        console.log('WebUI virtual key not found, creating new one');
        const result = await ensureWebUIVirtualKey(adminClient);
        virtualKey = result.key;
      }
      
      return NextResponse.json({ 
        virtualKey,
        source: 'server'
      });
      
    } catch (error) {
      console.error('Failed to retrieve virtual key:', error);
      
      // Try to get from session if available
      const session = context.session;
      if (session && 'virtualKey' in session && session.virtualKey) {
        return NextResponse.json({ 
          virtualKey: session.virtualKey,
          source: 'session'
        });
      }
      
      return NextResponse.json(
        { error: 'Failed to retrieve virtual key' },
        { status: 500 }
      );
    }
  },
  { requireAdmin: true }
);