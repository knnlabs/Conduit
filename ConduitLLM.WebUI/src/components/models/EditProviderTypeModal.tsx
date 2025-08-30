'use client';

import { useState, useEffect } from 'react';
import { Modal, TextInput, Select, Switch, Button, Group, Stack } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useAdminClient } from '@/lib/client/adminClient';

interface ProviderTypeAssociation {
  id?: number;
  identifier: string;
  provider?: string;
  isPrimary?: boolean;
}

interface EditProviderTypeModalProps {
  isOpen: boolean;
  modelId: number;
  association: ProviderTypeAssociation | null;
  onClose: () => void;
  onSave: () => void;
}

// Available provider types from the enum
const PROVIDER_TYPES = [
  { value: 'OpenAI', label: 'OpenAI' },
  { value: 'Groq', label: 'Groq' },
  { value: 'Replicate', label: 'Replicate' },
  { value: 'Fireworks', label: 'Fireworks' },
  { value: 'OpenAICompatible', label: 'OpenAI Compatible' },
  { value: 'MiniMax', label: 'MiniMax' },
  { value: 'Cerebras', label: 'Cerebras' },
  { value: 'SambaNova', label: 'SambaNova' },
  { value: 'DeepInfra', label: 'DeepInfra' }
];

export function EditProviderTypeModal({ 
  isOpen, 
  modelId, 
  association, 
  onClose, 
  onSave 
}: EditProviderTypeModalProps) {
  const [loading, setLoading] = useState(false);
  const { executeWithAdmin } = useAdminClient();

  const form = useForm({
    initialValues: {
      identifier: '',
      provider: '',
      isPrimary: false
    },
    validate: {
      identifier: (value) => !value ? 'Identifier is required' : null,
      provider: (value) => !value ? 'Provider type is required' : null
    }
  });

  useEffect(() => {
    if (association) {
      form.setValues({
        identifier: association.identifier ?? '',
        provider: association.provider ?? '',
        isPrimary: association.isPrimary ?? false
      });
    } else {
      form.reset();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [association]);

  const handleSubmit = async (values: typeof form.values) => {
    try {
      setLoading(true);
      
      if (association?.id) {
        // Update existing
        const associationId = association.id;
        if (!associationId) {
          throw new Error('Association ID is required for update');
        }
        await executeWithAdmin(client => 
          client.models.updateIdentifier(modelId, associationId, {
            identifier: values.identifier,
            provider: values.provider,
            isPrimary: values.isPrimary
          })
        );
        notifications.show({
          title: 'Success',
          message: 'Provider type association updated',
          color: 'green',
        });
      } else {
        // Create new
        await executeWithAdmin(client => 
          client.models.createIdentifier(modelId, {
            identifier: values.identifier,
            provider: values.provider,
            isPrimary: values.isPrimary
          })
        );
        notifications.show({
          title: 'Success',
          message: 'Provider type association created',
          color: 'green',
        });
      }
      
      onSave();
      onClose();
    } catch (error) {
      console.error('Failed to save provider type association:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to save provider type association',
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
      title={association?.id ? 'Edit Provider Type Association' : 'Add Provider Type Association'}
      size="md"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack>
          <TextInput
            label="Model Identifier"
            placeholder="e.g., gpt-4-turbo, llama-3.1-70b"
            required
            {...form.getInputProps('identifier')}
          />

          <Select
            label="Provider Type"
            placeholder="Select provider type"
            required
            data={PROVIDER_TYPES}
            {...form.getInputProps('provider')}
          />

          <Switch
            label="Primary Identifier"
            description="Mark as the primary identifier for this provider"
            {...form.getInputProps('isPrimary', { type: 'checkbox' })}
          />

          <Group justify="flex-end">
            <Button variant="subtle" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={loading}>
              {association?.id ? 'Update' : 'Create'}
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}