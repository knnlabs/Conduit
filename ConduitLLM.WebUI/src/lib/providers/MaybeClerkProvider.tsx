"use client";

import { ClerkProvider } from "@clerk/nextjs";
import React from "react";

interface Props {
  children: React.ReactNode;
}

/**
 * Wraps children in ClerkProvider only when CONDUIT_AUTH_TYPE === "clerk" (client-side check).
 * This avoids bundling Clerk for password-only deployments and keeps hydration consistent.
 */
export function MaybeClerkProvider({ children }: Props) {
  const authType = process.env.NEXT_PUBLIC_CONDUIT_AUTH_TYPE ?? "password";

  if (authType === "clerk") {
    // Check for required Clerk environment variables
    const publishableKey = process.env.NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY;
    
    if (!publishableKey) {
      console.error('Clerk auth enabled but NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY is missing');
      return (
        <div style={{ padding: '20px', color: 'red', textAlign: 'center' }}>
          <h3>Configuration Error</h3>
          <p>Clerk authentication is enabled but NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY is not configured.</p>
          <p>Please check your environment variables.</p>
        </div>
      );
    }

    return <ClerkProvider publishableKey={publishableKey}>{children}</ClerkProvider>;
  }

  return <>{children}</>;
}
