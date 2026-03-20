'use client';

import { useCallback } from 'react';
import { Modal, Text, Button, Group, Stack, Alert } from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';
import { withAdminClient } from '@/lib/client/adminClient';
import { useConfirmModal } from '@/hooks/useFormModal';
import type { ModelDto } from '@knn_labs/conduit-admin-client';


interface DeleteModelModalProps {
  isOpen: boolean;
  model: ModelDto;
  onClose: () => void;
  onSuccess: () => void;
}

export function DeleteModelModal({ isOpen, model, onClose, onSuccess }: DeleteModelModalProps) {
  const confirmAction = useCallback(async () => {
    if (!model.id) throw new Error('Model ID is required');
    await withAdminClient(client => client.models.delete(model.id as number));
  }, [model.id]);

  const { loading, handleConfirm } = useConfirmModal({
    onClose,
    onSuccess,
    confirmAction,
    successMessage: `Model "${model.name}" deleted successfully`,
  });

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title="Delete Model"
      size="md"
    >
      <Stack>
        <Alert icon={<IconAlertTriangle size={16} />} color="red" variant="light">
          <Text fw={500}>Warning: This action cannot be undone</Text>
        </Alert>

        <Text>
          Are you sure you want to delete the model <strong>{model.name}</strong>?
        </Text>

        <Text size="sm" c="dimmed">
          This will permanently remove the model from the system. Any model mappings referencing this model may be affected.
        </Text>

        <Group justify="flex-end">
          <Button variant="subtle" onClick={onClose} disabled={loading}>
            Cancel
          </Button>
          <Button color="red" onClick={() => void handleConfirm()} loading={loading}>
            Delete Model
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
