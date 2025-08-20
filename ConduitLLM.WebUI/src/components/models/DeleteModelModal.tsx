'use client';

import { useState } from 'react';
import { Modal, Text, Button, Group, Stack, Alert } from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import { useAdminClient } from '@/lib/client/adminClient';
import type { ModelDto } from '@knn_labs/conduit-admin-client';


interface DeleteModelModalProps {
  isOpen: boolean;
  model: ModelDto;
  onClose: () => void;
  onSuccess: () => void;
}

export function DeleteModelModal({ isOpen, model, onClose, onSuccess }: DeleteModelModalProps) {
  const [loading, setLoading] = useState(false);
  const { executeWithAdmin } = useAdminClient();

  const handleDelete = async () => {
    try {
      setLoading(true);
      if (!model.id) throw new Error('Model ID is required');
      await executeWithAdmin(client => client.models.delete(model.id as number));
      notifications.show({
        title: 'Success',
        message: `Model "${model.name}" deleted successfully`,
        color: 'green',
      });
      onSuccess();
    } catch (error) {
      console.error('Failed to delete model:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to delete model',
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  };

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
          <Button color="red" onClick={() => void handleDelete()} loading={loading}>
            Delete Model
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}