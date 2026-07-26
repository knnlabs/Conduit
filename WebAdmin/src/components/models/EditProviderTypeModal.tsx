'use client';

import { useState, useEffect } from 'react';
import { Modal, TextInput, Select, Switch, Button, Group, Stack, NumberInput, MultiSelect, SimpleGrid, Checkbox, Text } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notify } from '@/lib/notifications';
import { useAdminClient } from '@/lib/client/adminClient';
import type {
  ProviderConfigurationDefinition,
  ProviderTypeAssociationInput
} from '@/lib/admin-api';

interface EditProviderTypeModalProps {
  isOpen: boolean;
  modelId: number;
  association: (ProviderTypeAssociationInput & { id?: number }) | null;
  onClose: () => void;
  onSave: () => void;
}

const MODALITY_OPTIONS = ['text', 'image', 'audio', 'video', 'file'];

export function EditProviderTypeModal({ 
  isOpen, 
  modelId, 
  association, 
  onClose, 
  onSave 
}: EditProviderTypeModalProps) {
  const [loading, setLoading] = useState(false);
  const [providerTypes, setProviderTypes] = useState<Array<{ value: string; label: string }>>([]);
  const { executeWithAdmin } = useAdminClient();

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const loadProviderTypes = async () => {
      try {
        const schema = await executeWithAdmin(client =>
          client.providers.getConfigurationSchema()
        );
        setProviderTypes(
          Object.values(schema)
            .filter((entry): entry is ProviderConfigurationDefinition => entry !== undefined)
            .map(entry => ({
              value: String(entry.providerTypeId),
              label: entry.displayName,
            }))
        );
      } catch (error) {
        console.warn('Failed to load provider types:', error);
        notify.error('Failed to load provider types');
      }
    };

    void loadProviderTypes();
    // executeWithAdmin is intentionally omitted because the hook does not return a stable callback.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen]);

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
      providerVariation: '',
      overrideModalities: false,
      inputModalities: [] as string[],
      outputModalities: [] as string[],
      overrideOperations: false,
      supportsChat: false,
      supportsStreaming: false,
      supportsFunctionCalling: false,
      supportsImageGeneration: false,
      supportsVideoGeneration: false,
      supportsEmbeddings: false,
      supportsSpeechToText: false,
      supportsTextToSpeech: false,
      supportsRerank: false
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
        providerVariation: association.providerVariation ?? '',
        overrideModalities:
          (association.inputModalities !== null && association.inputModalities !== undefined)
          || (association.outputModalities !== null && association.outputModalities !== undefined),
        inputModalities: association.inputModalities ?? [],
        outputModalities: association.outputModalities ?? [],
        overrideOperations:
          association.operationalCapabilities !== null
          && association.operationalCapabilities !== undefined,
        supportsChat: association.operationalCapabilities?.supportsChat ?? false,
        supportsStreaming: association.operationalCapabilities?.supportsStreaming ?? false,
        supportsFunctionCalling: association.operationalCapabilities?.supportsFunctionCalling ?? false,
        supportsImageGeneration: association.operationalCapabilities?.supportsImageGeneration ?? false,
        supportsVideoGeneration: association.operationalCapabilities?.supportsVideoGeneration ?? false,
        supportsEmbeddings: association.operationalCapabilities?.supportsEmbeddings ?? false,
        supportsSpeechToText: association.operationalCapabilities?.supportsSpeechToText ?? false,
        supportsTextToSpeech: association.operationalCapabilities?.supportsTextToSpeech ?? false,
        supportsRerank: association.operationalCapabilities?.supportsRerank ?? false
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
        providerVariation: values.providerVariation || undefined,
        inputModalities: values.overrideModalities ? values.inputModalities : null,
        outputModalities: values.overrideModalities ? values.outputModalities : null,
        operationalCapabilities: values.overrideOperations ? {
          supportsChat: values.supportsChat,
          supportsStreaming: values.supportsStreaming,
          supportsFunctionCalling: values.supportsFunctionCalling,
          supportsImageGeneration: values.supportsImageGeneration,
          supportsVideoGeneration: values.supportsVideoGeneration,
          supportsEmbeddings: values.supportsEmbeddings,
          supportsSpeechToText: values.supportsSpeechToText,
          supportsTextToSpeech: values.supportsTextToSpeech,
          supportsRerank: values.supportsRerank,
          supportsVision: values.overrideModalities && values.inputModalities.includes('image')
        } : null,
        capabilitySource: values.overrideModalities || values.overrideOperations ? 'manual' : null
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

          <Switch
            label="Override directional modalities"
            description="Off inherits the canonical model. Empty selections explicitly mean unsupported."
            {...form.getInputProps('overrideModalities', { type: 'checkbox' })}
          />
          {form.values.overrideModalities && (
            <>
              <MultiSelect
                label="Accepted inputs"
                data={MODALITY_OPTIONS}
                {...form.getInputProps('inputModalities')}
              />
              <MultiSelect
                label="Produced outputs"
                data={MODALITY_OPTIONS}
                {...form.getInputProps('outputModalities')}
              />
              <Text size="xs" c="dimmed">Video input and video generation are independent.</Text>
            </>
          )}

          <Switch
            label="Override operations"
            description="Off inherits all operation flags from the canonical model."
            {...form.getInputProps('overrideOperations', { type: 'checkbox' })}
          />
          {form.values.overrideOperations && (
            <SimpleGrid cols={2}>
              <Checkbox label="Chat" {...form.getInputProps('supportsChat', { type: 'checkbox' })} />
              <Checkbox label="Streaming" {...form.getInputProps('supportsStreaming', { type: 'checkbox' })} />
              <Checkbox label="Function Calling" {...form.getInputProps('supportsFunctionCalling', { type: 'checkbox' })} />
              <Checkbox label="Image Generation" {...form.getInputProps('supportsImageGeneration', { type: 'checkbox' })} />
              <Checkbox label="Video Generation" {...form.getInputProps('supportsVideoGeneration', { type: 'checkbox' })} />
              <Checkbox label="Embeddings" {...form.getInputProps('supportsEmbeddings', { type: 'checkbox' })} />
              <Checkbox label="Speech to Text" {...form.getInputProps('supportsSpeechToText', { type: 'checkbox' })} />
              <Checkbox label="Text to Speech" {...form.getInputProps('supportsTextToSpeech', { type: 'checkbox' })} />
              <Checkbox label="Rerank" {...form.getInputProps('supportsRerank', { type: 'checkbox' })} />
            </SimpleGrid>
          )}

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
