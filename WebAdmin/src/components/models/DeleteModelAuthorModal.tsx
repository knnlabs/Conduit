'use client';

import { useCallback } from 'react';
import { Modal, Text, Button, Group, Stack, Alert } from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';
import { withAdminClient } from '@/lib/client/adminClient';
import { useConfirmModal } from '@/hooks/useFormModal';
import type { ModelAuthorDto } from '@knn_labs/conduit-admin-client';


interface DeleteModelAuthorModalProps {
  isOpen: boolean;
  author: ModelAuthorDto;
  onClose: () => void;
  onSuccess: () => void;
}

export function DeleteModelAuthorModal({ isOpen, author, onClose, onSuccess }: DeleteModelAuthorModalProps) {
  const confirmAction = useCallback(async () => {
    if (!author.id) throw new Error('Author ID is required');
    await withAdminClient(client => client.modelAuthors.delete(author.id as number));
  }, [author.id]);

  const { loading, handleConfirm } = useConfirmModal({
    onClose,
    onSuccess,
    confirmAction,
    successMessage: `Author "${author.name}" deleted successfully`,
  });

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title="Delete Author"
      size="md"
    >
      <Stack>
        <Alert icon={<IconAlertTriangle size={16} />} color="red" variant="light">
          <Text fw={500}>Warning: This action cannot be undone</Text>
        </Alert>

        <Text>
          Are you sure you want to delete the author <strong>{author.name}</strong>?
        </Text>

        {/* seriesCount field doesn't exist in ModelAuthorDto */}

        <Text size="sm" c="dimmed">
          This will permanently remove the author from the system. Model series created by this author will remain but will no longer be associated with this author.
        </Text>

        <Group justify="flex-end">
          <Button variant="subtle" onClick={onClose} disabled={loading}>
            Cancel
          </Button>
          <Button color="red" onClick={() => void handleConfirm()} loading={loading}>
            Delete Author
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
