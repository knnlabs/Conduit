'use client';

interface AuthProviderProps {
  children: React.ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
  // No authentication required - pass through children
  return <>{children}</>;
}