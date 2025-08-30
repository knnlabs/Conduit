'use client';

import { useState, useEffect } from 'react';
import { Modal, TextInput, Select, Switch, Button, Group, Stack, NumberInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useAdminClient } from '@/lib/client/adminClient';

interface ProviderTypeAssociation {
  id?: number;
  identifier: string;
  provider?: string;
  isPrimary?: boolean;
  maxInputTokens?: number | null;
  maxOutputTokens?: number | null;
  speedScore?: number | null;
  qualityScore?: number | null;
  providerVariation?: string | null;
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
      isPrimary: false,
      maxInputTokens: null as number | null,
      maxOutputTokens: null as number | null,
      speedScore: null as number | null,
      qualityScore: null as number | null,
      providerVariation: ''
    },
    validate: {
      identifier: (value) => !value ? 'Identifier is required' : null,
      provider: (value) => !value ? 'Provider type is required' : null,
      speedScore: (value) => {
        if (value !== null && value !== undefined) {
          if (value < 0.01 || value > 100) {
            return 'Speed score must be between 0.01 and 100';
          }
        }
        return null;
      },
      qualityScore: (value) => {
        if (value !== null && value !== undefined) {
          if (value < 0 || value > 1) {
            return 'Quality score must be between 0 and 1';
          }
        }
        return null;
      }
    }
  });

  useEffect(() => {
    if (association) {
      form.setValues({
        identifier: association.identifier ?? '',
        provider: association.provider ?? '',
        isPrimary: association.isPrimary ?? false,
        maxInputTokens: association.maxInputTokens ?? null,
        maxOutputTokens: association.maxOutputTokens ?? null,
        speedScore: association.speedScore ?? null,
        qualityScore: association.qualityScore ?? null,
        providerVariation: association.providerVariation ?? ''
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
            isPrimary: values.isPrimary,
            // TODO: Add these fields once SDK is regenerated
            // maxInputTokens: values.maxInputTokens,
            // maxOutputTokens: values.maxOutputTokens,
            // speedScore: values.speedScore,
            // qualityScore: values.qualityScore,
            // providerVariation: values.providerVariation || undefined
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
            isPrimary: values.isPrimary,
            // TODO: Add these fields once SDK is regenerated
            // maxInputTokens: values.maxInputTokens,
            // maxOutputTokens: values.maxOutputTokens,
            // speedScore: values.speedScore,
            // qualityScore: values.qualityScore,
            // providerVariation: values.providerVariation || undefined
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

          <TextInput
            label="Provider Variation"
            placeholder="e.g., GGUF, Q4_K_M, instruct"
            description="Model variation or quantization level"
            {...form.getInputProps('providerVariation')}
          />

          <NumberInput
            label="Speed Score"
            placeholder="1.0"
            description="Relative speed (1.0 = baseline, 2.0 = 2x faster)"
            min={0.01}
            max={100}
            decimalScale={2}
            {...form.getInputProps('speedScore')}
          />

          <NumberInput
            label="Quality Score"
            placeholder="0.95"
            description="Quality relative to original (0.0-1.0)"
            min={0}
            max={1}
            decimalScale={2}
            step={0.05}
            {...form.getInputProps('qualityScore')}
          />

          <NumberInput
            label="Max Input Tokens"
            placeholder="128000"
            description="Provider-specific override for max input tokens"
            min={0}
            {...form.getInputProps('maxInputTokens')}
          />

          <NumberInput
            label="Max Output Tokens"
            placeholder="4096"
            description="Provider-specific override for max output tokens"
            min={0}
            {...form.getInputProps('maxOutputTokens')}
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