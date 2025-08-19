'use client';

import { useState } from 'react';
import { Modal, TextInput, Switch, Button, Stack, Group } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useAdminClient } from '@/lib/client/adminClient';
import type { CreateModelAuthorDto } from '@knn_labs/conduit-admin-client';


interface CreateModelAuthorModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function CreateModelAuthorModal({ isOpen, onClose, onSuccess }: CreateModelAuthorModalProps) {
  const [loading, setLoading] = useState(false);
  const { executeWithAdmin } = useAdminClient();

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

  const handleSubmit = async (values: CreateModelAuthorDto) => {
    try {
      setLoading(true);
      await executeWithAdmin(client => client.modelAuthors.create(values));
      notifications.show({
        title: 'Success',
        message: 'Author created successfully',
        color: 'green',
      });
      form.reset();
      onSuccess();
    } catch (error) {
      console.error('Failed to create author:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to create author',
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
            <Button variant="subtle" onClick={onClose}>
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