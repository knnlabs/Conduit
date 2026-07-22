'use client';

import { type ReactNode, type FormEvent } from 'react';
import { Modal, Stack, Group, Button, type ModalProps } from '@mantine/core';

interface EntityFormModalProps {
  /** Whether the modal is open. */
  opened: boolean;
  /** Close handler (also used by the Cancel button). */
  onClose: () => void;
  /** Modal title. */
  title: ReactNode;
  /** Mantine modal size. Default: 'lg'. */
  size?: ModalProps['size'];
  /** Submit handler for the form, e.g. `form.onSubmit(handleSubmit)`. */
  onSubmit: (event?: FormEvent<HTMLFormElement>) => void;
  /** Whether the submit button shows a loading state. */
  loading?: boolean;
  /** Submit button label. */
  submitLabel: ReactNode;
  /** Whether the submit button is disabled. */
  submitDisabled?: boolean;
  /** Cancel button label. Default: 'Cancel'. */
  cancelLabel?: ReactNode;
  /** Form fields. */
  children: ReactNode;
}

/**
 * Standard entity create/edit modal shell: a Mantine Modal wrapping a form with
 * a Stack of fields and a right-aligned Cancel/Submit footer. Removes the
 * repeated Modal + form + Group footer boilerplate from entity modals; pair with
 * useFormModal for the submit lifecycle.
 */
export function EntityFormModal({
  opened,
  onClose,
  title,
  size = 'lg',
  onSubmit,
  loading,
  submitLabel,
  submitDisabled,
  cancelLabel = 'Cancel',
  children,
}: EntityFormModalProps) {
  return (
    <Modal opened={opened} onClose={onClose} title={title} size={size}>
      <form onSubmit={onSubmit}>
        <Stack>
          {children}
          <Group justify="flex-end">
            <Button variant="subtle" onClick={onClose}>
              {cancelLabel}
            </Button>
            <Button type="submit" loading={loading} disabled={submitDisabled}>
              {submitLabel}
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
