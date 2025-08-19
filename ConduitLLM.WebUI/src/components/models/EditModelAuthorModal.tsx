'use client';

import { useState, useEffect } from 'react';
import { Modal, TextInput, Button, Stack, Group } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useAdminClient } from '@/lib/client/adminClient';
import type { ModelAuthorDto, UpdateModelAuthorDto } from '@knn_labs/conduit-admin-client';


interface EditModelAuthorModalProps {
  isOpen: boolean;
  author: ModelAuthorDto;
  onClose: () => void;
  onSuccess: () => void;
}

export function EditModelAuthorModal({ isOpen, author, onClose, onSuccess }: EditModelAuthorModalProps) {
  const [loading, setLoading] = useState(false);
  const { executeWithAdmin } = useAdminClient();

  const form = useForm<UpdateModelAuthorDto>({
    initialValues: {
      name: author?.name ?? '',
      websiteUrl: author?.websiteUrl ?? null,
      // isActive field doesn't exist in ModelAuthorDto
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

  useEffect(() => {
    if (author) {
      form.setValues({
        name: author.name ?? '',
        websiteUrl: author.websiteUrl ?? null,
        // isActive field doesn't exist in ModelAuthorDto
      });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [author]);

  const handleSubmit = async (values: UpdateModelAuthorDto) => {
    try {
      setLoading(true);
      if (!author.id) throw new Error('Author ID is required');
      await executeWithAdmin(client => client.modelAuthors.update(author.id as number, values));
      notifications.show({
        title: 'Success',
        message: 'Author updated successfully',
        color: 'green',
      });
      onSuccess();
    } catch (error) {
      console.error('Failed to update author:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to update author',
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
      title="Edit Author"
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

          {/* isActive field doesn't exist in ModelAuthorDto */}

          <Group justify="flex-end">
            <Button variant="subtle" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={loading}>
              Update Author
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}