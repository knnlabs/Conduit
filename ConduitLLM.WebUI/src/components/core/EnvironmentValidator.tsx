'use client';

import { useEffect } from 'react';
import { validateEnvironment } from '@/config';

/**
 * Component that validates environment configuration on mount
 * Should be included early in the app component tree
 */
export function EnvironmentValidator() {
  useEffect(() => {
    try {
      validateEnvironment();
    } catch (error) {
      console.error('Environment validation failed:', error);
      
      // In development, show a warning but don't crash
      if (process.env.NODE_ENV === 'development') {
        console.warn('Continuing with invalid environment configuration in development mode');
      } else {
        // In production, this would be a critical error
        throw error;
      }
    }
  }, []);

  return null;
}