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
  MultiSelect,
  Title,
  Text,
  Divider,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useEffect } from 'react';
import { useUpdateModelMapping } from '@/hooks/useModelMappingsApi';
import { useProviders } from '@/hooks/useProviderApi';
import type { UIModelMapping, UIProvider } from '@/lib/types/mappers';
import type { UpdateModelProviderMappingDto } from '@/types/api-types';

interface EditModelMappingModalProps {
  isOpen: boolean;
  onClose: () => void;
  mapping: UIModelMapping | null;
  onSave?: () => void;
}

interface FormValues {
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
  // Metadata
  maxContextLength?: number;
  maxOutputTokens?: number;
}

export function EditModelMappingModal({
  isOpen,
  onClose,
  mapping,
  onSave,
}: EditModelMappingModalProps) {
  const updateMapping = useUpdateModelMapping();
  const { providers } = useProviders();

  const form = useForm<FormValues>({
    initialValues: {
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
      maxContextLength: undefined,
      maxOutputTokens: undefined,
    },
  });

  // Update form when mapping changes
  useEffect(() => {
    if (mapping) {
      form.setValues({
        providerId: mapping.targetProvider,
        providerModelId: mapping.targetModel,
        priority: mapping.priority || 100,
        isEnabled: mapping.isActive,
        supportsVision: mapping.supportsVision || false,
        supportsImageGeneration: mapping.supportsImageGeneration || false,
        supportsAudioTranscription: mapping.supportsAudioTranscription || false,
        supportsTextToSpeech: mapping.supportsTextToSpeech || false,
        supportsRealtimeAudio: mapping.supportsRealtimeAudio || false,
        supportsFunctionCalling: mapping.supportsFunctionCalling || false,
        supportsStreaming: mapping.supportsStreaming || false,
        maxContextLength: mapping.maxContextLength,
        maxOutputTokens: mapping.maxOutputTokens,
      });
    }
  }, [mapping]);

  const handleSubmit = async (values: FormValues) => {
    if (!mapping) return;

    const updateData: UpdateModelProviderMappingDto = {
      providerId: values.providerId,
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
      maxContextLength: values.maxContextLength,
      maxOutputTokens: values.maxOutputTokens,
    };

    await updateMapping.mutateAsync({
      id: parseInt(mapping.id, 10),
      data: updateData,
    });

    onSave?.();
    onClose();
  };

  const providerOptions = providers?.map((p: UIProvider) => ({
    value: p.name,
    label: p.name,
  })) || [];

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title="Edit Model Mapping"
      size="lg"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          <div>
            <Text size="sm" c="dimmed" mb={4}>Model</Text>
            <Text fw={500}>{mapping?.sourceModel}</Text>
          </div>

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

          <Switch
            label="Realtime Audio"
            {...form.getInputProps('supportsRealtimeAudio', { type: 'checkbox' })}
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