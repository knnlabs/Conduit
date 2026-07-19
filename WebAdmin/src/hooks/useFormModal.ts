'use client';

import { useState, useCallback } from 'react';

import type { UseFormReturnType } from '@mantine/form';

import { notify } from '@/lib/notifications';

/**
 * Hook that encapsulates the common modal form submission pattern:
 * loading state, try/catch, notifications, form reset, and close.
 *
 * Replaces the boilerplate found in ~40 modal components:
 * ```ts
 * const [loading, setLoading] = useState(false);
 * const handleSubmit = async (values) => {
 *   setLoading(true);
 *   try {
 *     await api.create(values);
 *     notifications.show({ title: 'Success', ... });
 *     form.reset();
 *     onSuccess?.();
 *     onClose();
 *   } catch (error) {
 *     notifications.show({ title: 'Error', ... });
 *   } finally {
 *     setLoading(false);
 *   }
 * };
 * ```
 *
 * @example
 * const { loading, handleSubmit, handleClose } = useFormModal({
 *   form,
 *   onClose,
 *   onSuccess,
 *   submitAction: (values) => withAdminClient(c => c.models.create(values)),
 *   successMessage: 'Model created successfully',
 * });
 */
export function useFormModal<TValues extends object>({
  form,
  onClose,
  onSuccess,
  submitAction,
  successMessage,
  resetOnClose = true,
}: {
  /** Mantine form instance. */
  form: UseFormReturnType<TValues>;
  /** Called to close the modal (parent state). */
  onClose: () => void;
  /** Called after a successful submission. */
  onSuccess?: () => void;
  /** Async function that performs the actual API call with form values. */
  submitAction: (values: TValues) => Promise<unknown>;
  /** Notification message shown on success. */
  successMessage: string;
  /** Whether to reset the form when the modal closes (default: true). */
  resetOnClose?: boolean;
}) {
  const [loading, setLoading] = useState(false);

  const handleClose = useCallback(() => {
    if (resetOnClose) {
      form.reset();
    }
    onClose();
  }, [form, onClose, resetOnClose]);

  const handleSubmit = useCallback(
    async (values: TValues) => {
      setLoading(true);
      try {
        await submitAction(values);
        notify.success(successMessage);
        form.reset();
        onSuccess?.();
        onClose();
      } catch (error) {
        notify.error(error);
      } finally {
        setLoading(false);
      }
    },
    [submitAction, successMessage, form, onSuccess, onClose]
  );

  return { loading, handleSubmit, handleClose };
}

/**
 * Hook for simple confirmation modals (delete, archive, etc.) that don't use a form.
 *
 * @example
 * const { loading, handleConfirm } = useConfirmModal({
 *   onClose,
 *   onSuccess,
 *   confirmAction: () => withAdminClient(c => c.models.deleteById(id)),
 *   successMessage: 'Model deleted successfully',
 * });
 */
export function useConfirmModal({
  onClose,
  onSuccess,
  confirmAction,
  successMessage,
}: {
  /** Called to close the modal. */
  onClose: () => void;
  /** Called after successful confirmation. */
  onSuccess?: () => void;
  /** Async function that performs the action. */
  confirmAction: () => Promise<unknown>;
  /** Notification message shown on success. */
  successMessage: string;
}) {
  const [loading, setLoading] = useState(false);

  const handleConfirm = useCallback(async () => {
    setLoading(true);
    try {
      await confirmAction();
      notify.success(successMessage);
      onSuccess?.();
      onClose();
    } catch (error) {
      notify.error(error);
    } finally {
      setLoading(false);
    }
  }, [confirmAction, successMessage, onSuccess, onClose]);

  return { loading, handleConfirm };
}
