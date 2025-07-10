'use client';

import { useEffect, useCallback, useRef } from 'react';
import { useAuthStore } from '@/stores/useAuthStore';

const SESSION_DURATION = 24 * 60 * 60 * 1000; // 24 hours in milliseconds
const REFRESH_BEFORE_EXPIRY = 60 * 60 * 1000; // Refresh 1 hour before expiry
const REFRESH_CHECK_INTERVAL = 5 * 60 * 1000; // Check every 5 minutes

export function useSessionRefresh() {
  const { user, logout } = useAuthStore();
  const refreshTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const checkIntervalRef = useRef<NodeJS.Timeout | null>(null);
  
  const refreshSession = useCallback(async () => {
    try {
      const response = await fetch('/api/auth/refresh', {
        method: 'POST',
        credentials: 'include',
      });
      
      if (!response.ok) {
        console.error('Session refresh failed');
        // If refresh fails, logout the user
        logout();
        return false;
      }
      
      const data = await response.json();
      console.log('Session refreshed successfully, expires at:', data.expiresAt);
      return true;
    } catch (error) {
      console.error('Session refresh error:', error);
      logout();
      return false;
    }
  }, [logout]);
  
  const scheduleRefresh = useCallback((expiresAt: string) => {
    // Clear any existing timeout
    if (refreshTimeoutRef.current) {
      clearTimeout(refreshTimeoutRef.current);
    }
    
    const expiryTime = new Date(expiresAt).getTime();
    const now = Date.now();
    const timeUntilExpiry = expiryTime - now;
    
    // Schedule refresh 1 hour before expiry
    const refreshTime = timeUntilExpiry - REFRESH_BEFORE_EXPIRY;
    
    if (refreshTime > 0) {
      refreshTimeoutRef.current = setTimeout(() => {
        refreshSession();
      }, refreshTime);
    } else {
      // If less than 1 hour remaining, refresh immediately
      refreshSession();
    }
  }, [refreshSession]);
  
  const checkSessionExpiry = useCallback(async () => {
    try {
      // Get session from cookie
      const cookies = document.cookie.split(';');
      const sessionCookie = cookies.find(c => c.trim().startsWith('conduit_session='));
      
      if (!sessionCookie) {
        return;
      }
      
      const sessionValue = decodeURIComponent(sessionCookie.split('=')[1]);
      const session = JSON.parse(sessionValue);
      
      if (session.expiresAt) {
        const expiryTime = new Date(session.expiresAt).getTime();
        const now = Date.now();
        const timeUntilExpiry = expiryTime - now;
        
        // If less than 1 hour until expiry, refresh now
        if (timeUntilExpiry < REFRESH_BEFORE_EXPIRY) {
          await refreshSession();
        }
      }
    } catch (error) {
      console.error('Failed to check session expiry:', error);
    }
  }, [refreshSession]);
  
  useEffect(() => {
    if (!user || !user.isAuthenticated) {
      // Clear any existing timeouts if user is not authenticated
      if (refreshTimeoutRef.current) {
        clearTimeout(refreshTimeoutRef.current);
        refreshTimeoutRef.current = null;
      }
      if (checkIntervalRef.current) {
        clearInterval(checkIntervalRef.current);
        checkIntervalRef.current = null;
      }
      return;
    }
    
    // Check session expiry periodically
    checkSessionExpiry();
    checkIntervalRef.current = setInterval(checkSessionExpiry, REFRESH_CHECK_INTERVAL);
    
    // Cleanup on unmount
    return () => {
      if (refreshTimeoutRef.current) {
        clearTimeout(refreshTimeoutRef.current);
      }
      if (checkIntervalRef.current) {
        clearInterval(checkIntervalRef.current);
      }
    };
  }, [user, checkSessionExpiry]);
  
  return { refreshSession };
}