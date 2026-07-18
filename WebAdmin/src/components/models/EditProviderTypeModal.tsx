'use client';

import { useState, useEffect } from 'react';
import { Modal, TextInput, Select, Switch, Button, Group, Stack, NumberInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notify } from '@/lib/notifications';
import { useAdminClient } from '@/lib/client/adminClient';
import { getProviderSelectOptions } from '@/lib/utils/providerTypeUtils';
import type { ProviderTypeAssociationInput } from '@knn_labs/conduit-admin-client';

interface EditProviderTypeModalProps {
  isOpen: boolean;
  modelId: number;
  association: (ProviderTypeAssociationInput & { id?: number }) | null;
  onClose: () => void;
  onSave: () => void;
}

export function EditProviderTypeModal({ 
  isOpen, 
  modelId, 
  association, 
  onClose, 
  onSave 
}: EditProviderTypeModalProps) {
  const [loading, setLoading] = useState(false);
  const { executeWithAdmin } = useAdminClient();

  // Get provider types from the enum utility
  const providerTypes = getProviderSelectOptions();

  const handleClose = () => {
    form.reset();
    onClose();
  };

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
    }
  });

  useEffect(() => {
    if (association) {
      form.setValues({
        identifier: association.identifier ?? '',
        // Provider is now a number from the API, convert to string for the Select component
        provider: association.provider !== null && association.provider !== undefined 
          ? String(association.provider) 
          : '',
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
    setLoading(true);
    
    try {
      const data: ProviderTypeAssociationInput = {
        identifier: values.identifier,
        provider: values.provider ? Number(values.provider) : undefined,
        isPrimary: values.isPrimary,
        maxInputTokens: values.maxInputTokens,
        maxOutputTokens: values.maxOutputTokens,
        speedScore: values.speedScore,
        qualityScore: values.qualityScore,
        providerVariation: values.providerVariation || undefined
      };
      
      if (association?.id) {
        // Update existing
        const associationId = association.id;
        if (!associationId) {
          throw new Error('Association ID is required for update');
        }
        
        await executeWithAdmin(async (client) => {
          // The SDK's updateIdentifier validates internally, no need to pre-validate
          await client.models.updateIdentifier(modelId, associationId, data);
        });
        
        notify.success('Provider type association updated');
      } else {
        // Create new
        await executeWithAdmin(async (client) => {
          // The SDK's createIdentifier validates internally, no need to pre-validate
          await client.models.createIdentifier(modelId, data);
        });
        
        notify.success('Provider type association created');
      }
      
      // Important: Close modal first to prevent UI state issues
      handleClose();
      
      // Then trigger the save callback which will reload data
      // Use setTimeout to ensure the modal close completes first
      setTimeout(() => {
        onSave();
      }, 100);
      
    } catch (error) {
      console.warn('Failed to save provider type association:', error);
      
      // Handle specific error types from SDK
      if (error && typeof error === 'object' && 'name' in error && 'fields' in error) {
        const validationError = error as { name: string; fields: Record<string, string | string[]> };
        if (validationError.name === 'ModelValidationError') {
          Object.entries(validationError.fields).forEach(([field, errorMsg]) => {
            const msg = Array.isArray(errorMsg) ? errorMsg.join(', ') : errorMsg;
            form.setFieldError(field, msg);
          });
        }
      } else {
        notify.error(error, 'Failed to save provider type association');
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      opened={isOpen}
      onClose={handleClose}
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
            data={providerTypes}
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
            <Button variant="subtle" onClick={handleClose}>
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