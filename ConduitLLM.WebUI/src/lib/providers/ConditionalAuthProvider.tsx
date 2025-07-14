import { ClerkProvider } from '@clerk/nextjs';
import { AuthProvider } from './AuthProvider';
import { getAuthMode } from '@/lib/auth/auth-mode';

interface ConditionalAuthProviderProps {
  children: React.ReactNode;
}

export function ConditionalAuthProvider({ children }: ConditionalAuthProviderProps) {
  // Check if Clerk is enabled - this runs on the server
  const authMode = getAuthMode();
  
  if (authMode === 'clerk') {
    return (
      <ClerkProvider>
        {children}
      </ClerkProvider>
    );
  }
  
  // Use existing Conduit auth provider
  return <AuthProvider>{children}</AuthProvider>;
}