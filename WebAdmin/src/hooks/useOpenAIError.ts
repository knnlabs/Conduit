/**
 * Hook for handling OpenAI-compatible error responses
 */

import { useState, useCallback, useRef, useEffect } from 'react';
import { notifications } from '@mantine/notifications';
import { 
  parseApiError, 
  parseSDKError, 
  parseStreamingError,
  isRetryableError,
  getRetryDelay,
  formatErrorForConsole,
  getNotificationMessage,
  type AppError
} from '@/utils/errorHandling';

export interface UseOpenAIErrorOptions {
  showNotifications?: boolean;
  autoRetry?: boolean;
  maxRetries?: number;
  onError?: (error: AppError) => void;
  onRetry?: (attempt: number) => void;
}

export interface UseOpenAIErrorReturn {
  error: AppError | null;
  isRetrying: boolean;
  retryAttempt: number;
  clearError: () => void;
  handleError: (error: unknown, response?: Response) => AppError;
  handleStreamingError: (event: MessageEvent) => void;
  retry: () => Promise<void>;
}

export function useOpenAIError(options: UseOpenAIErrorOptions = {}): UseOpenAIErrorReturn {
  const {
    showNotifications = true,
    autoRetry = false,
    maxRetries = 3,
    onError,
    onRetry,
  } = options;
  
  const [error, setError] = useState<AppError | null>(null);
  const [isRetrying, setIsRetrying] = useState(false);
  const [retryAttempt, setRetryAttempt] = useState(0);
  
  const retryTimeoutRef = useRef<NodeJS.Timeout | undefined>(undefined);
  const retryCallbackRef = useRef<(() => Promise<void>) | null>(null);
  
  // Clear any pending retry on unmount
  useEffect(() => {
    return () => {
      if (retryTimeoutRef.current) {
        clearTimeout(retryTimeoutRef.current);
      }
    };
  }, []);
  
  /**
   * Clear the current error
   */
  const clearError = useCallback(() => {
    setError(null);
    setRetryAttempt(0);
    setIsRetrying(false);
    if (retryTimeoutRef.current) {
      clearTimeout(retryTimeoutRef.current);
    }
  }, []);
  
  /**
   * Handle an error from an API response or SDK call
   */
  const handleError = useCallback((err: unknown, response?: Response): AppError => {
    let appError: AppError;
    
    // Parse the error based on its type
    if (response) {
      appError = parseApiError(response, err);
    } else {
      appError = parseSDKError(err);
    }
    
    // Log to console in development
    formatErrorForConsole(appError);
    
    // Update state
    setError(appError);
    
    // Show notification if enabled
    if (showNotifications) {
      notifications.show({
        title: appError.title,
        message: getNotificationMessage(appError),
        color: appError.severity === 'error' ? 'red' : 'orange',
        autoClose: appError.retryAfter ? false : 5000,
      });
    }
    
    // Call error callback
    onError?.(appError);
    
    // Handle auto-retry if enabled
    if (autoRetry && isRetryableError(appError) && retryAttempt < maxRetries) {
      const delay = getRetryDelay(appError, retryAttempt + 1);
      
      setIsRetrying(true);
      retryTimeoutRef.current = setTimeout(() => {
        void retry();
      }, delay);
    }
    
    return appError;
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [showNotifications, onError, autoRetry, maxRetries]);
  
  /**
   * Handle errors from SSE streaming
   */
  const handleStreamingError = useCallback((event: MessageEvent) => {
    const streamError = parseStreamingError(event);
    if (streamError) {
      handleError(streamError);
    }
  }, [handleError]);
  
  /**
   * Retry the last failed operation
   */
  const retry = useCallback(async () => {
    if (!error || !retryCallbackRef.current) {
      return;
    }
    
    setIsRetrying(true);
    setRetryAttempt(prev => prev + 1);
    
    // Call retry callback
    onRetry?.(retryAttempt + 1);
    
    try {
      await retryCallbackRef.current();
      // Success - clear error
      clearError();
    } catch (err) {
      // Retry failed - handle the new error
      handleError(err);
    } finally {
      setIsRetrying(false);
    }
  }, [error, retryAttempt, onRetry, clearError, handleError]);
  
  return {
    error,
    isRetrying,
    retryAttempt,
    clearError,
    handleError,
    handleStreamingError,
    retry,
  };
}

/**
 * Hook for handling errors with automatic retry countdown
 */
export function useOpenAIErrorWithCountdown(options: UseOpenAIErrorOptions = {}) {
  const [countdown, setCountdown] = useState<number | null>(null);
  const countdownIntervalRef = useRef<NodeJS.Timeout | undefined>(undefined);
  
  const errorHandler = useOpenAIError({
    ...options,
    onError: (error) => {
      // Start countdown if there's a retry-after value
      if (error.retryAfter) {
        setCountdown(error.retryAfter);
      }
      options.onError?.(error);
    },
  });
  
  // Handle countdown
  useEffect(() => {
    if (countdown === null || countdown <= 0) {
      if (countdownIntervalRef.current) {
        clearInterval(countdownIntervalRef.current);
      }
      return;
    }
    
    countdownIntervalRef.current = setInterval(() => {
      setCountdown(prev => {
        if (prev === null || prev <= 1) {
          return null;
        }
        return prev - 1;
      });
    }, 1000);
    
    return () => {
      if (countdownIntervalRef.current) {
        clearInterval(countdownIntervalRef.current);
      }
    };
  }, [countdown]);
  
  // Auto-retry when countdown reaches zero
  useEffect(() => {
    if (countdown === 0 && errorHandler.error?.isRecoverable) {
      void errorHandler.retry();
      setCountdown(null);
    }
  }, [countdown, errorHandler]);
  
  return {
    ...errorHandler,
    countdown,
  };
}