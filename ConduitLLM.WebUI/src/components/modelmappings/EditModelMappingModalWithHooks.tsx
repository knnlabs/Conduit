'use client';

import {
  Modal,
  TextInput,
  Switch,
  Stack,
  Group,
  NumberInput,
  Button,
  Select,
  Divider,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useEffect } from 'react';
import { useUpdateModelMapping, useModelMappings } from '@/hooks/useModelMappingsApi';
import { useProviders } from '@/hooks/useProviderApi';
import type { ProviderCredentialDto, ModelProviderMappingDto, UpdateModelProviderMappingDto } from '@knn_labs/conduit-admin-client';
import { getProviderTypeFromDto, getProviderDisplayName } from '@/lib/utils/providerTypeUtils';

interface EditModelMappingModalProps {
  isOpen: boolean;
  onClose: () => void;
  mapping: ModelProviderMappingDto | null;
  onSave?: () => void;
}

interface FormValues {
  modelId: string;
  providerId: string;
  providerModelId: string;
  priority: number;
  isEnabled: boolean;
  // Capabilities
  supportsVision: boolean;
  supportsImageGeneration: boolean;
  supportsAudioTranscription: boolean;
  supportsTextToSpeech: boolean;
  supportsRealtimeAudio: boolean;
  supportsFunctionCalling: boolean;
  supportsStreaming: boolean;
  supportsVideoGeneration: boolean;
  supportsEmbeddings: boolean;
  // Metadata
  maxContextLength?: number;
  maxOutputTokens?: number;
  isDefault: boolean;
}

export function EditModelMappingModal({
  isOpen,
  onClose,
  mapping,
  onSave,
}: EditModelMappingModalProps) {
  const updateMapping = useUpdateModelMapping();
  const { providers } = useProviders();
  const { mappings } = useModelMappings();

  const form = useForm<FormValues>({
    initialValues: {
      modelId: '',
      providerId: '',
      providerModelId: '',
      priority: 100,
      isEnabled: true,
      supportsVision: false,
      supportsImageGeneration: false,
      supportsAudioTranscription: false,
      supportsTextToSpeech: false,
      supportsRealtimeAudio: false,
      supportsFunctionCalling: false,
      supportsStreaming: false,
      supportsVideoGeneration: false,
      supportsEmbeddings: false,
      maxContextLength: undefined,
      maxOutputTokens: undefined,
      isDefault: false,
    },
    validate: {
      modelId: (value) => {
        if (!value?.trim()) return 'Model alias is required';
        
        // Check for duplicates, but exclude the current mapping being edited
        const duplicate = mappings.find(m => 
          m.modelId === value && m.id !== (mapping?.id ?? 0)
        );
        
        if (duplicate) {
          return 'Model alias already exists';
        }
        
        return null;
      },
    },
  });

  // Update form when mapping changes
  useEffect(() => {
    if (mapping && providers) {
      
      // The mapping.providerId is now a numeric ID
      const providerIdForForm = mapping.providerId?.toString() ?? '';
      
      
      const formData = {
        modelId: mapping.modelId,
        providerId: providerIdForForm, // Use the numeric ID for the form
        providerModelId: mapping.providerModelId,
        priority: mapping.priority ?? 100,
        isEnabled: mapping.isEnabled,
        supportsVision: mapping.supportsVision ?? false,
        supportsImageGeneration: mapping.supportsImageGeneration ?? false,
        supportsAudioTranscription: mapping.supportsAudioTranscription ?? false,
        supportsTextToSpeech: mapping.supportsTextToSpeech ?? false,
        supportsRealtimeAudio: mapping.supportsRealtimeAudio ?? false,
        supportsFunctionCalling: mapping.supportsFunctionCalling ?? false,
        supportsStreaming: mapping.supportsStreaming ?? false,
        supportsVideoGeneration: mapping.supportsVideoGeneration ?? false,
        supportsEmbeddings: mapping.supportsEmbeddings ?? false,
        maxContextLength: mapping.maxContextLength,
        maxOutputTokens: mapping.maxOutputTokens,
        isDefault: mapping.isDefault ?? false,
      };
      form.setValues(formData);
    }
  }, [mapping, providers, form]);

  const handleSubmit = async (values: FormValues) => {
    if (!mapping) return;

    const updateData: UpdateModelProviderMappingDto = {
      modelId: values.modelId,
      providerId: parseInt(values.providerId, 10), // Send numeric ID directly
      providerModelId: values.providerModelId,
      priority: values.priority,
      isEnabled: values.isEnabled,
      supportsVision: values.supportsVision,
      supportsImageGeneration: values.supportsImageGeneration,
      supportsAudioTranscription: values.supportsAudioTranscription,
      supportsTextToSpeech: values.supportsTextToSpeech,
      supportsRealtimeAudio: values.supportsRealtimeAudio,
      supportsFunctionCalling: values.supportsFunctionCalling,
      supportsStreaming: values.supportsStreaming,
      supportsVideoGeneration: values.supportsVideoGeneration,
      supportsEmbeddings: values.supportsEmbeddings,
      maxContextLength: values.maxContextLength,
      maxOutputTokens: values.maxOutputTokens,
      isDefault: values.isDefault,
    };

    try {
      await updateMapping.mutateAsync({
        id: mapping.id,
        data: updateData,
      });

      onSave?.();
      onClose();
    } catch (error) {
      console.error('[EditModal] Update failed:', error);
    }
  };

  const providerOptions = providers?.map((p: ProviderCredentialDto) => {
    try {
      const providerType = getProviderTypeFromDto(p);
      return {
        value: p.id.toString(), // Form uses string representation of numeric ID
        label: getProviderDisplayName(providerType),
      };
    } catch {
      return {
        value: p.id.toString(),
        label: 'Unknown Provider',
      };
    }
  }) || [];

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title="Edit Model Mapping"
      size="lg"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          <TextInput
            label="Model Alias"
            placeholder="e.g., gpt-4-turbo"
            description="The alias used to reference this model in API calls"
            required
            {...form.getInputProps('modelId')}
          />

          <Select
            label="Provider"
            placeholder="Select provider"
            data={providerOptions}
            {...form.getInputProps('providerId')}
          />

          <TextInput
            label="Provider Model ID"
            placeholder="e.g., gpt-4-turbo"
            description="The model identifier used by the provider"
            {...form.getInputProps('providerModelId')}
          />

          <NumberInput
            label="Priority"
            placeholder="100"
            description="Higher priority mappings are preferred (0-1000)"
            min={0}
            max={1000}
            {...form.getInputProps('priority')}
          />

          <Switch
            label="Enable mapping"
            description="Disabled mappings will not be used for routing"
            {...form.getInputProps('isEnabled', { type: 'checkbox' })}
          />

          <Divider label="Capabilities" labelPosition="center" />

          <Group grow>
            <Switch
              label="Vision"
              {...form.getInputProps('supportsVision', { type: 'checkbox' })}
            />
            <Switch
              label="Streaming"
              {...form.getInputProps('supportsStreaming', { type: 'checkbox' })}
            />
          </Group>

          <Group grow>
            <Switch
              label="Function Calling"
              {...form.getInputProps('supportsFunctionCalling', { type: 'checkbox' })}
            />
            <Switch
              label="Image Generation"
              {...form.getInputProps('supportsImageGeneration', { type: 'checkbox' })}
            />
          </Group>

          <Group grow>
            <Switch
              label="Audio Transcription"
              {...form.getInputProps('supportsAudioTranscription', { type: 'checkbox' })}
            />
            <Switch
              label="Text to Speech"
              {...form.getInputProps('supportsTextToSpeech', { type: 'checkbox' })}
            />
          </Group>

          <Group grow>
            <Switch
              label="Realtime Audio"
              {...form.getInputProps('supportsRealtimeAudio', { type: 'checkbox' })}
            />
            <Switch
              label="Video Generation"
              {...form.getInputProps('supportsVideoGeneration', { type: 'checkbox' })}
            />
          </Group>

          <Switch
            label="Embeddings"
            {...form.getInputProps('supportsEmbeddings', { type: 'checkbox' })}
          />

          <Divider label="Context Limits" labelPosition="center" />

          <Group grow>
            <NumberInput
              label="Max Context Length"
              placeholder="e.g., 128000"
              description="Maximum input tokens"
              min={0}
              {...form.getInputProps('maxContextLength')}
            />
            <NumberInput
              label="Max Output Tokens"
              placeholder="e.g., 4096"
              description="Maximum output tokens"
              min={0}
              {...form.getInputProps('maxOutputTokens')}
            />
          </Group>

          <Group justify="flex-end" mt="md">
            <Button variant="subtle" onClick={onClose}>
              Cancel
            </Button>
            <Button 
              type="submit" 
              loading={updateMapping.isPending}
            >
              Save Changes
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}