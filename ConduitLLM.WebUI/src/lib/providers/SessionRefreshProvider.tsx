'use client';

import { useSessionRefresh } from '@/hooks/useSessionRefresh';
import { getAuthMode } from '@/lib/auth/auth-mode';

export function SessionRefreshProvider({ children }: { children: React.ReactNode }) {
  // Only use session refresh for Conduit auth
  const authMode = getAuthMode();
  
  if (authMode === 'conduit') {
    // This hook will handle automatic session refresh for Conduit auth
    useSessionRefresh();
  }
  
  // Clerk handles its own session management
  return <>{children}</>;
}