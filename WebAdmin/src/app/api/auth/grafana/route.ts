import { auth } from '@clerk/nextjs/server';
import { NextResponse } from 'next/server';

/**
 * Authorization subrequest used by the WebAdmin reverse proxy before it
 * forwards browser traffic to Grafana. This route deliberately returns status
 * codes instead of redirects because Nginx auth_request only accepts 2xx,
 * 401, and 403 responses.
 */
export function isDevelopmentAuthBypass(
  nodeEnv = process.env.NODE_ENV,
  authEnabled = process.env.CLERK_AUTH_ENABLED
) {
  return nodeEnv === 'development' && authEnabled !== 'true';
}

export async function GET() {
  const isDevelopmentBypass = isDevelopmentAuthBypass();

  if (isDevelopmentBypass) {
    return new NextResponse(null, { status: 204 });
  }

  const { userId, sessionClaims } = await auth();

  if (!userId) {
    return new NextResponse(null, { status: 401 });
  }

  const metadata = sessionClaims?.metadata as { siteadmin?: boolean } | undefined;
  if (metadata?.siteadmin !== true) {
    return new NextResponse(null, { status: 403 });
  }

  return new NextResponse(null, { status: 204 });
}
