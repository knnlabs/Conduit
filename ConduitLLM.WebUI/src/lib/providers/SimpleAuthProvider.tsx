'use client';

import { ReactNode } from 'react';

export function SimpleAuthProvider({ children }: { children: ReactNode }) {
  // No authentication required - pass through children
  return <>{children}</>;
}