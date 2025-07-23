import { clerkMiddleware, createRouteMatcher } from '@clerk/nextjs/server';
import { NextResponse } from 'next/server';

// Public routes that don't require authentication
const isPublicRoute = createRouteMatcher([
  '/access-denied',
  '/api/health'
]);

export default clerkMiddleware(async (auth, req) => {
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