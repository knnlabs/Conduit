'use client';

import { useState } from 'react';
import { Modal, Text, Button, Group, Stack, Alert } from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import { useAdminClient } from '@/lib/client/adminClient';
import type { ModelSeriesDto } from '@knn_labs/conduit-admin-client';


interface DeleteModelSeriesModalProps {
  isOpen: boolean;
  series: ModelSeriesDto;
  onClose: () => void;
  onSuccess: () => void;
}

export function DeleteModelSeriesModal({ isOpen, series, onClose, onSuccess }: DeleteModelSeriesModalProps) {
  const [loading, setLoading] = useState(false);
  const { executeWithAdmin } = useAdminClient();

  const handleDelete = async () => {
    try {
      setLoading(true);
      if (!series.id) throw new Error('Series ID is required');
      await executeWithAdmin(client => client.modelSeries.delete(series.id as number));
      notifications.show({
        title: 'Success',
        message: `Model series "${series.name}" deleted successfully`,
        color: 'green',
      });
      onSuccess();
    } catch (error) {
      console.error('Failed to delete model series:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to delete model series',
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
      title="Delete Model Series"
      size="md"
    >
      <Stack>
        <Alert icon={<IconAlertTriangle size={16} />} color="red" variant="light">
          <Text fw={500}>Warning: This action cannot be undone</Text>
        </Alert>

        <Text>
          Are you sure you want to delete the model series <strong>{series.name}</strong>?
        </Text>

        {/* modelCount field doesn't exist in ModelSeriesDto */}

        <Text size="sm" c="dimmed">
          This will permanently remove the model series from the system. Models in this series will remain but will no longer be associated with this series.
        </Text>

        <Group justify="flex-end">
          <Button variant="subtle" onClick={onClose} disabled={loading}>
            Cancel
          </Button>
          <Button color="red" onClick={() => void handleDelete()} loading={loading}>
            Delete Series
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}