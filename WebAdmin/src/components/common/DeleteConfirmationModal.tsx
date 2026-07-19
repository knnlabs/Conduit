'use client';

import { useCallback } from 'react';
import { Modal, Text, Button, Group, Stack, Alert } from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';
import { useConfirmModal } from '@/hooks/useFormModal';

interface DeleteConfirmationModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  /** Modal title, e.g. "Delete Model" */
  title: string;
  /** The type of item being deleted, e.g. "model", "model series" */
  itemLabel: string;
  /** Display name of the item being deleted */
  itemName: string;
  /** Additional context about the consequences of deletion */
  description: string;
  /** Text for the confirm button, e.g. "Delete Model" */
  confirmButtonText: string;
  /** Notification message on success */
  successMessage: string;
  /** Async function that performs the deletion */
  deleteAction: () => Promise<unknown>;
}

export function DeleteConfirmationModal({
  isOpen,
  onClose,
  onSuccess,
  title,
  itemLabel,
  itemName,
  description,
  confirmButtonText,
  successMessage,
  deleteAction,
}: DeleteConfirmationModalProps) {
  const confirmAction = useCallback(() => deleteAction(), [deleteAction]);

  const { loading, handleConfirm } = useConfirmModal({
    onClose,
    onSuccess,
    confirmAction,
    successMessage,
  });

  return (
    <Modal opened={isOpen} onClose={onClose} title={title} size="md">
      <Stack>
        <Alert icon={<IconAlertTriangle size={16} />} color="red" variant="light">
          <Text fw={500}>Warning: This action cannot be undone</Text>
        </Alert>

        <Text>
          Are you sure you want to delete the {itemLabel} <strong>{itemName}</strong>?
        </Text>

        <Text size="sm" c="dimmed">
          {description}
        </Text>

        <Group justify="flex-end">
          <Button variant="subtle" onClick={onClose} disabled={loading}>
            Cancel
          </Button>
          <Button color="red" onClick={() => void handleConfirm()} loading={loading}>
            {confirmButtonText}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
