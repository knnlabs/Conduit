'use client';

import { useCallback, useEffect } from 'react';
import { Modal, TextInput, Button, Stack, Group } from '@mantine/core';
import { useForm } from '@mantine/form';
import { withAdminClient } from '@/lib/client/adminClient';
import { useFormModal } from '@/hooks/useFormModal';
import type { ModelAuthorDto, UpdateModelAuthorDto } from '@knn_labs/conduit-admin-client';


interface EditModelAuthorModalProps {
  isOpen: boolean;
  author: ModelAuthorDto;
  onClose: () => void;
  onSuccess: () => void;
}

export function EditModelAuthorModal({ isOpen, author, onClose, onSuccess }: EditModelAuthorModalProps) {
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

  const submitAction = useCallback(
    async (values: UpdateModelAuthorDto) => {
      if (!author.id) throw new Error('Author ID is required');
      await withAdminClient(client => client.modelAuthors.update(author.id as number, values));
    },
    [author.id]
  );

  const { loading, handleSubmit, handleClose } = useFormModal({
    form,
    onClose,
    onSuccess,
    submitAction,
    successMessage: 'Author updated successfully',
  });

  return (
    <Modal
      opened={isOpen}
      onClose={handleClose}
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
            <Button variant="subtle" onClick={handleClose}>
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
