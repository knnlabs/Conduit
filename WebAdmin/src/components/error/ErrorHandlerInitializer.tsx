'use client';

import { useEffect } from 'react';
import { setupGlobalErrorHandler } from '@/lib/utils/error-handler';

/**
 * Client component to initialize global error handling
 */
export function ErrorHandlerInitializer() {
  useEffect(() => {
    setupGlobalErrorHandler();
  }, []);

  return null;
}