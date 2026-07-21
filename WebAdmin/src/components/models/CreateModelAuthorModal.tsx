'use client';

import { useCallback } from 'react';
import { Modal, TextInput, Switch, Button, Stack, Group } from '@mantine/core';
import { useForm } from '@mantine/form';
import { withAdminClient } from '@/lib/client/adminClient';
import { useFormModal } from '@/hooks/useFormModal';
import type { CreateModelAuthorDto } from '@/lib/admin-api';


interface CreateModelAuthorModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function CreateModelAuthorModal({ isOpen, onClose, onSuccess }: CreateModelAuthorModalProps) {
  const form = useForm<CreateModelAuthorDto>({
    initialValues: {
      name: '',
      websiteUrl: ''
    },
    validate: {
      name: (value) => !value ? 'Name is required' : null,
      websiteUrl: (value) => {
        if (value && !value.startsWith('http://') && !value.startsWith('https://')) {
          return 'Website URL must start with http:// or https://';
        }
        return null;
      }
    }
  });

  const submitAction = useCallback(
    (values: CreateModelAuthorDto) => withAdminClient(client => client.modelAuthors.create(values)),
    []
  );

  const { loading, handleSubmit, handleClose } = useFormModal({
    form,
    onClose,
    onSuccess,
    submitAction,
    successMessage: 'Author created successfully',
  });

  return (
    <Modal
      opened={isOpen}
      onClose={handleClose}
      title="Create New Author"
      size="md"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack>
          <TextInput
            label="Author Name"
            placeholder="e.g., OpenAI"
            required
            {...form.getInputProps('name')}
          />

          <TextInput
            label="Website URL"
            placeholder="https://..."
            {...form.getInputProps('websiteUrl')}
          />

          <Switch
            label="Active"
            {...form.getInputProps('isActive', { type: 'checkbox' })}
          />

          <Group justify="flex-end">
            <Button variant="subtle" onClick={handleClose}>
              Cancel
            </Button>
            <Button type="submit" loading={loading}>
              Create Author
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
