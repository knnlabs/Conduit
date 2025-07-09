import { NextResponse } from 'next/server'
import type { NextRequest } from 'next/server'

const publicPaths = ['/login', '/api/auth/validate', '/api/auth/logout']

// Security headers to apply to all responses
const securityHeaders = {
  'X-Content-Type-Options': 'nosniff',
  'X-Frame-Options': 'DENY',
  'X-XSS-Protection': '1; mode=block',
  'Referrer-Policy': 'strict-origin-when-cross-origin',
  'Permissions-Policy': 'camera=(), microphone=(), geolocation=()',
};

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl
  
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