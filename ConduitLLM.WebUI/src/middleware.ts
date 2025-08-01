import { clerkMiddleware, createRouteMatcher } from '@clerk/nextjs/server';
import { NextResponse } from 'next/server';

// Public routes that don't require authentication
const isPublicRoute = createRouteMatcher([
  '/access-denied',
  '/api/health',
  '/api/chat/completions',
  '/api/model-mappings',
  '/api/discovery/models',
  '/api/videos/generate',
  '/api/videos/tasks/(.*)',
  '/api/images/generate'
]);

export default clerkMiddleware(async (auth, req) => {
  // Skip all auth in development when explicitly disabled
  if (process.env.DISABLE_CLERK_AUTH === 'true' && process.env.NODE_ENV === 'development') {
    return NextResponse.next();
  }

  if (!isPublicRoute(req)) {
    // Get auth state
    const { userId, sessionClaims, redirectToSignIn } = await auth();
    
    // If not authenticated, redirect to sign-in
    if (!userId) {
      return redirectToSignIn();
    }
    
    // Check if user has admin access
    const metadata = sessionClaims?.metadata as { siteadmin?: boolean } | undefined;
    const isAdmin = metadata?.siteadmin === true;
    
    // If not admin, redirect to access-denied
    if (!isAdmin) {
      return NextResponse.redirect(new URL('/access-denied', req.url));
    }
  }
});

export const config = {
  matcher: ['/((?!.*\\..*|_next).*)', '/', '/(api|trpc)(.*)'],
};