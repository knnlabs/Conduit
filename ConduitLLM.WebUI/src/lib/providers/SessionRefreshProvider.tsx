'use client';

export function SessionRefreshProvider({ children }: { children: React.ReactNode }) {
  // No authentication - no session refresh needed
  return <>{children}</>;
}