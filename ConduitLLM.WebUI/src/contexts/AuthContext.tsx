'use client';

import { createContext, useContext, ReactNode } from 'react';

interface AuthContextType {
  isAuthDisabled: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

interface AuthProviderProps {
  children: ReactNode;
  isAuthDisabled: boolean;
}

export function AuthProvider({ children, isAuthDisabled }: AuthProviderProps) {
  return (
    <AuthContext.Provider value={{ isAuthDisabled }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}