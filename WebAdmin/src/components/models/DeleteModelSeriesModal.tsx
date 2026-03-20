'use client';

import { useCallback } from 'react';
import { Modal, Text, Button, Group, Stack, Alert } from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';
import { withAdminClient } from '@/lib/client/adminClient';
import { useConfirmModal } from '@/hooks/useFormModal';
import type { ModelSeriesDto } from '@knn_labs/conduit-admin-client';


interface DeleteModelSeriesModalProps {
  isOpen: boolean;
  series: ModelSeriesDto;
  onClose: () => void;
  onSuccess: () => void;
}

export function DeleteModelSeriesModal({ isOpen, series, onClose, onSuccess }: DeleteModelSeriesModalProps) {
  const confirmAction = useCallback(async () => {
    if (!series.id) throw new Error('Series ID is required');
    await withAdminClient(client => client.modelSeries.delete(series.id as number));
  }, [series.id]);

  const { loading, handleConfirm } = useConfirmModal({
    onClose,
    onSuccess,
    confirmAction,
    successMessage: `Model series "${series.name}" deleted successfully`,
  });

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
          <Button color="red" onClick={() => void handleConfirm()} loading={loading}>
            Delete Series
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
