'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';

// Query key factory for Auth API
export const authApiKeys = {
  all: ['auth-api'] as const,
  session: () => [...authApiKeys.all, 'session'] as const,
} as const;

export interface AuthSession {
  isAuthenticated: boolean;
  adminKeyHash?: string;
  sessionId?: string;
  loginTime?: string;
  expiresAt?: string;
}

export interface LoginRequest {
  adminKey: string;
  rememberMe?: boolean;
}

export interface LoginResponse {
  success: boolean;
  sessionId: string;
  expiresAt: string;
  message?: string;
}

// Simple admin key authentication
export function useLogin() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async (request: LoginRequest): Promise<LoginResponse> => {
      try {
        // Validate admin key with server-side API
        const response = await fetch('/api/auth/validate', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            adminKey: request.adminKey,
          }),
        });

        if (!response.ok) {
          const errorData = await response.json().catch(() => ({}));
          throw new Error(errorData.error || 'Authentication failed');
        }

        const authResult: LoginResponse = await response.json();
        
        // Calculate session expiration based on rememberMe
        const expiresAt = new Date(
          Date.now() + (request.rememberMe ? 7 * 24 * 60 * 60 * 1000 : 24 * 60 * 60 * 1000)
        ).toISOString();
        
        const session: AuthSession = {
          isAuthenticated: true,
          adminKeyHash: btoa(request.adminKey).substring(0, 16) + '...', // Store a hash/preview, not the actual key
          sessionId: authResult.sessionId,
          loginTime: new Date().toISOString(),
          expiresAt,
        };
        
        // Store session in localStorage
        localStorage.setItem('conduit_session', JSON.stringify(session));
        localStorage.setItem('conduit_admin_key', request.adminKey); // Store for API calls
        
        return {
          ...authResult,
          expiresAt, // Use our calculated expiration
        };
      } catch (error: any) {
        reportError(error, 'Failed to authenticate with admin key');
        throw new Error(error?.message || 'Authentication failed');
      }
    },
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: authApiKeys.all });
    },
  });
}

export function useLogout() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async (): Promise<void> => {
      try {
        // Call logout endpoint to clear server-side session
        const response = await fetch('/api/auth/logout', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
        });

        if (!response.ok) {
          throw new Error('Logout failed');
        }
        
        // Clear client-side session data
        localStorage.removeItem('conduit_session');
        localStorage.removeItem('conduit_admin_key');
      } catch (error: any) {
        reportError(error, 'Failed to logout');
        // Continue with logout even if API call fails
        localStorage.removeItem('conduit_session');
        localStorage.removeItem('conduit_admin_key');
      }
    },
    onSuccess: () => {
      queryClient.clear();
    },
  });
}

export function useCurrentSession() {
  return useQuery({
    queryKey: authApiKeys.session(),
    queryFn: async (): Promise<AuthSession | null> => {
      try {
        const storedSession = localStorage.getItem('conduit_session');
        const adminKey = localStorage.getItem('conduit_admin_key');
        
        if (!storedSession || !adminKey) {
          return { isAuthenticated: false };
        }
        
        const session: AuthSession = JSON.parse(storedSession);
        
        // Check if session is expired
        if (session.expiresAt && new Date(session.expiresAt) < new Date()) {
          localStorage.removeItem('conduit_session');
          localStorage.removeItem('conduit_admin_key');
          return { isAuthenticated: false };
        }
        
        // TODO: Optionally validate the admin key is still valid with the backend
        // const client = getAdminClient();
        // const isValid = await client.auth.validateSession(session.sessionId);
        
        return session;
      } catch (error: any) {
        reportError(error, 'Failed to fetch current session');
        return { isAuthenticated: false };
      }
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
    retry: false,
  });
}

export function useValidateAdminKey() {
  return useMutation({
    mutationFn: async (adminKey: string): Promise<{ isValid: boolean; message?: string }> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.auth.validateAdminKey(adminKey);
        
        // Mock validation
        await new Promise(resolve => setTimeout(resolve, 500));
        
        if (!adminKey || adminKey.trim().length === 0) {
          return { isValid: false, message: 'Admin key is required' };
        }
        
        if (adminKey.length < 10) {
          return { isValid: false, message: 'Invalid admin key format' };
        }
        
        return { isValid: true, message: 'Admin key is valid' };
      } catch (error: any) {
        reportError(error, 'Failed to validate admin key');
        throw new Error(error?.message || 'Failed to validate admin key');
      }
    },
  });
}

export function useExtendSession() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async (hours: number = 24): Promise<{ expiresAt: string }> => {
      try {
        const storedSession = localStorage.getItem('conduit_session');
        
        if (!storedSession) {
          throw new Error('No active session found');
        }
        
        const session: AuthSession = JSON.parse(storedSession);
        const newExpiresAt = new Date(Date.now() + hours * 60 * 60 * 1000).toISOString();
        
        const updatedSession: AuthSession = {
          ...session,
          expiresAt: newExpiresAt,
        };
        
        localStorage.setItem('conduit_session', JSON.stringify(updatedSession));
        
        return { expiresAt: newExpiresAt };
      } catch (error: any) {
        reportError(error, 'Failed to extend session');
        throw new Error(error?.message || 'Failed to extend session');
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: authApiKeys.session() });
    },
  });
}

// Utility function to get the admin key (for API calls)
export function getStoredAdminKey(): string | null {
  if (typeof window === 'undefined') return null;
  return localStorage.getItem('conduit_admin_key');
}

// Utility function to check if user is authenticated
export function isAuthenticated(): boolean {
  if (typeof window === 'undefined') return false;
  
  const storedSession = localStorage.getItem('conduit_session');
  if (!storedSession) return false;
  
  try {
    const session: AuthSession = JSON.parse(storedSession);
    
    if (!session.isAuthenticated) return false;
    
    // Check expiration
    if (session.expiresAt && new Date(session.expiresAt) < new Date()) {
      localStorage.removeItem('conduit_session');
      localStorage.removeItem('conduit_admin_key');
      return false;
    }
    
    return true;
  } catch {
    return false;
  }
}