import { NextResponse } from 'next/server'
import type { NextRequest } from 'next/server'
import { getAuthMode } from '@/lib/auth/auth-mode'

const publicPaths = ['/login', '/api/auth/validate', '/api/auth/logout', '/api/health']

// Clerk-specific public paths
const clerkPublicPaths = ['/sign-in', '/sign-up', '/api/health']

// Paths that require authentication but should not have session parsing
// (SSE endpoints need special handling)
const streamingPaths = ['/api/admin/events/stream']

// Security headers to apply to all responses
const securityHeaders = {
  'X-Content-Type-Options': 'nosniff',
  'X-Frame-Options': 'DENY',
  'X-XSS-Protection': '1; mode=block',
  'Referrer-Policy': 'strict-origin-when-cross-origin',
  'Permissions-Policy': 'camera=(), microphone=(), geolocation=()',
};

// Helper function to add security headers to response
const addSecurityHeaders = (response: NextResponse) => {
  Object.entries(securityHeaders).forEach(([key, value]) => {
    response.headers.set(key, value);
  });
  
  // Add CSP header for production
  if (process.env.NODE_ENV === 'production') {
    response.headers.set(
      'Content-Security-Policy',
      "default-src 'self'; " +
      "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
      "style-src 'self' 'unsafe-inline'; " +
      "img-src 'self' data: https: blob:; " +
      "font-src 'self' data:; " +
      "connect-src 'self' ws: wss: https:; " +
      "frame-ancestors 'none';"
    );
  }
  
  return response;
};

// Conduit auth middleware logic
function conduitAuthMiddleware(request: NextRequest) {
  const { pathname } = request.nextUrl
  
  // Allow public paths
  if (publicPaths.some(path => pathname.startsWith(path))) {
    const response = NextResponse.next();
    return addSecurityHeaders(response);
  }
  
  // Allow static assets and Next.js internals
  if (
    pathname.startsWith('/_next') ||
    pathname.startsWith('/favicon.ico') ||
    pathname.includes('.')
  ) {
    const response = NextResponse.next();
    return addSecurityHeaders(response);
  }
  
  // Check for authentication cookie
  const sessionCookie = request.cookies.get('conduit_session')
  
  if (!sessionCookie) {
    // For API routes, return 401 JSON response
    if (pathname.startsWith('/api/')) {
      const response = new NextResponse(
        JSON.stringify({ error: 'Unauthorized' }),
        { 
          status: 401,
          headers: { 'Content-Type': 'application/json' }
        }
      );
      return addSecurityHeaders(response);
    }
    
    // For other routes, redirect to login page
    const loginUrl = new URL('/login', request.url)
    loginUrl.searchParams.set('from', pathname)
    const response = NextResponse.redirect(loginUrl);
    return addSecurityHeaders(response);
  }
  
  try {
    // Parse and validate session
    const session = JSON.parse(sessionCookie.value)
    
    // Check if session is expired
    if (session.expiresAt && new Date(session.expiresAt) < new Date()) {
      // For API routes, return 401
      if (pathname.startsWith('/api/')) {
        const response = new NextResponse(
          JSON.stringify({ error: 'Session expired' }),
          { 
            status: 401,
            headers: { 'Content-Type': 'application/json' }
          }
        )
        response.cookies.delete('conduit_session')
        return addSecurityHeaders(response);
      }
      
      // Clear expired cookie and redirect to login
      const response = NextResponse.redirect(new URL('/login', request.url))
      response.cookies.delete('conduit_session')
      return response
    }
    
    // Check if user is authenticated
    if (!session.isAuthenticated) {
      // For API routes, return 401
      if (pathname.startsWith('/api/')) {
        return new NextResponse(
          JSON.stringify({ error: 'Not authenticated' }),
          { 
            status: 401,
            headers: { 'Content-Type': 'application/json' }
          }
        )
      }
      const response = NextResponse.redirect(new URL('/login', request.url));
      return addSecurityHeaders(response);
    }
    
    const response = NextResponse.next();
    return addSecurityHeaders(response);
  } catch {
    // Invalid session cookie
    if (pathname.startsWith('/api/')) {
      const response = new NextResponse(
        JSON.stringify({ error: 'Invalid session' }),
        { 
          status: 401,
          headers: { 'Content-Type': 'application/json' }
        }
      )
      response.cookies.delete('conduit_session')
      return response
    }
    
    // Redirect to login for non-API routes
    const response = NextResponse.redirect(new URL('/login', request.url))
    response.cookies.delete('conduit_session')
    return addSecurityHeaders(response);
  }
}

// Clerk middleware logic
async function clerkAuthMiddleware(request: NextRequest) {
  const { pathname } = request.nextUrl;
  
  // Allow Clerk public paths
  if (clerkPublicPaths.some(path => pathname.startsWith(path))) {
    const response = NextResponse.next();
    return addSecurityHeaders(response);
  }
  
  // Allow static assets and Next.js internals
  if (
    pathname.startsWith('/_next') ||
    pathname.startsWith('/favicon.ico') ||
    pathname.includes('.')
  ) {
    const response = NextResponse.next();
    return addSecurityHeaders(response);
  }
  
  // Import Clerk auth helpers
  const { auth } = await import('@clerk/nextjs/server');
  const { userId } = await auth();
  
  // If not authenticated
  if (!userId) {
    // For API routes, return 401 JSON response
    if (pathname.startsWith('/api/')) {
      const response = new NextResponse(
        JSON.stringify({ error: 'Unauthorized' }),
        { 
          status: 401,
          headers: { 'Content-Type': 'application/json' }
        }
      );
      return addSecurityHeaders(response);
    }
    
    // Redirect to Clerk sign-in page
    const signInUrl = new URL('/sign-in', request.url);
    signInUrl.searchParams.set('redirect_url', pathname);
    return NextResponse.redirect(signInUrl);
  }
  
  // Check admin status for authenticated users
  const { isClerkAdmin } = await import('@/lib/auth/clerk-helpers');
  const isAdmin = await isClerkAdmin();
  
  if (!isAdmin) {
    // For API routes, return 403 JSON response
    if (pathname.startsWith('/api/')) {
      const response = new NextResponse(
        JSON.stringify({ error: 'Forbidden: Admin access required' }),
        { 
          status: 403,
          headers: { 'Content-Type': 'application/json' }
        }
      );
      return addSecurityHeaders(response);
    }
    
    // For UI routes, show an access denied page or redirect
    const response = new NextResponse(
      'Access Denied: Admin privileges required',
      { status: 403 }
    );
    return addSecurityHeaders(response);
  }
  
  // Continue with the request
  const response = NextResponse.next();
  return addSecurityHeaders(response);
}

// Main middleware function that delegates based on auth mode
export function middleware(request: NextRequest) {
  const authMode = getAuthMode()
  
  if (authMode === 'clerk') {
    return clerkAuthMiddleware(request)
  }
  
  return conduitAuthMiddleware(request)
}

export const config = {
  matcher: [
    /*
     * Match all request paths except for the ones starting with:
     * - api/auth (authentication endpoints)
     * - _next/static (static files)
     * - _next/image (image optimization files)
     * - favicon.ico (favicon file)
     */
    '/((?!api/auth|_next/static|_next/image|favicon.ico).*)',
  ],
}