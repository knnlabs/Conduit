import { NextRequest, NextResponse } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { virtualKeyRateLimiter } from '@/lib/middleware/rateLimiter';

/**
 * GET /api/auth/virtual-key
 * 
 * Returns the WebUI virtual key for authenticated admin sessions.
 * If the key doesn't exist, it creates one automatically.
 */
export const GET = withSDKAuth(
  async (request: NextRequest, context) => {
    try {
      // Apply rate limiting
      const rateLimit = await virtualKeyRateLimiter(request);
      
      if (!rateLimit.allowed) {
        return NextResponse.json(
          { 
            error: 'Too many requests',
            retryAfter: Math.ceil((rateLimit.resetTime - Date.now()) / 1000)
          },
          { 
            status: 429,
            headers: {
              'X-RateLimit-Limit': '5',
              'X-RateLimit-Remaining': '0',
              'X-RateLimit-Reset': new Date(rateLimit.resetTime).toISOString(),
              'Retry-After': Math.ceil((rateLimit.resetTime - Date.now()) / 1000).toString()
            }
          }
        );
      }
      
      const { adminClient } = context;
      
      if (!adminClient) {
        return NextResponse.json(
          { error: 'Admin client not available' },
          { status: 500 }
        );
      }
      
      // Get or create WebUI virtual key using Admin SDK
      const virtualKey = await adminClient.system.getWebUIVirtualKey();
      
      const response = NextResponse.json({ 
        virtualKey,
        source: 'server'
      });
      
      // Add rate limit headers
      response.headers.set('X-RateLimit-Limit', '5');
      response.headers.set('X-RateLimit-Remaining', rateLimit.remaining.toString());
      response.headers.set('X-RateLimit-Reset', new Date(rateLimit.resetTime).toISOString());
      
      return response;
      
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