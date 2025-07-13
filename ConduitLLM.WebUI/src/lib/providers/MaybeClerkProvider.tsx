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
    return <ClerkProvider>{children}</ClerkProvider>;
  }

  return <>{children}</>;
}
