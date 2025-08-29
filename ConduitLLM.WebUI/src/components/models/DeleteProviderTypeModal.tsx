'use client';

import { Modal, Text, Button, Group, Stack } from '@mantine/core';

interface ProviderTypeAssociation {
  id: number;
  identifier: string;
  provider: string;
  isPrimary: boolean;
}

interface DeleteProviderTypeModalProps {
  isOpen: boolean;
  association: ProviderTypeAssociation | null;
  loading: boolean;
  onClose: () => void;
  onConfirm: () => void;
}

export function DeleteProviderTypeModal({ 
  isOpen, 
  association, 
  loading, 
  onClose, 
  onConfirm 
}: DeleteProviderTypeModalProps) {
  
  if (!association) return null;

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title="Delete Provider Type Association"
      size="sm"
    >
      <Stack>
        <Text>
          Are you sure you want to delete the association for identifier{' '}
          <Text component="span" fw={600}>
            {association.identifier}
          </Text>{' '}
          with provider{' '}
          <Text component="span" fw={600}>
            {association.provider}
          </Text>
          ?
        </Text>

        <Group justify="flex-end">
          <Button variant="subtle" onClick={onClose} disabled={loading}>
            Cancel
          </Button>
          <Button color="red" onClick={onConfirm} loading={loading}>
            Delete
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}