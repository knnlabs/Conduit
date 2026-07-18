'use client';

import { notifications } from '@mantine/notifications';

import { getErrorMessage } from '@/lib/utils/error-utils';

/**
 * Centralized notification utilities. Use these instead of calling
 * `notifications.show()` directly to ensure consistent styling and
 * error message extraction across the application.
 */
export const notify = {
  success: (message: string, title = 'Success') =>
    notifications.show({ title, message, color: 'green' }),

  error: (error: unknown, fallbackMessage = 'An error occurred') =>
    notifications.show({
      title: 'Error',
      message: getErrorMessage(error) || fallbackMessage,
      color: 'red',
    }),

  warning: (message: string, title = 'Warning') =>
    notifications.show({ title, message, color: 'yellow' }),

  info: (message: string, title = 'Info') =>
    notifications.show({ title, message, color: 'blue' }),

  /**
   * Shows a loading notification that can be updated later via `updateLoading`.
   */
  loading: (id: string, message: string, title = 'Loading') =>
    notifications.show({
      id,
      title,
      message,
      loading: true,
      autoClose: false,
    }),

  /**
   * Updates a loading notification to show success or failure.
   */
  updateLoading: (
    id: string,
    options: { success: boolean; message: string; title?: string }
  ) =>
    notifications.update({
      id,
      title: options.title ?? (options.success ? 'Success' : 'Error'),
      message: options.message,
      color: options.success ? 'green' : 'red',
      loading: false,
      autoClose: 5000,
    }),
};
