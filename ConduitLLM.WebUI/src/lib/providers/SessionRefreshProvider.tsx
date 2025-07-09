'use client';

import { useSessionRefresh } from '@/hooks/useSessionRefresh';

export function SessionRefreshProvider({ children }: { children: React.ReactNode }) {
  // This hook will handle automatic session refresh
  useSessionRefresh();
  
  return <>{children}</>;
}