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
  Title,
  Text,
  Divider,
  Alert,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconInfoCircle } from '@tabler/icons-react';
import { useCreateModelMapping } from '@/hooks/useModelMappingsApi';
import { useProviders } from '@/hooks/useProviderApi';
import { ProviderModelSelect } from './ProviderModelSelect';
import type { CreateModelProviderMappingDto } from '@knn_labs/conduit-admin-client';
import type { ProviderCredentialDto } from '@knn_labs/conduit-admin-client';

interface CreateModelMappingModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess?: () => void;
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

export function CreateModelMappingModal({ 
  isOpen, 
  onClose, 
  onSuccess 
}: CreateModelMappingModalProps) {
  const createMapping = useCreateModelMapping();
  const { providers, isLoading: providersLoading } = useProviders();

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
      supportsStreaming: true, // Most models support streaming
      supportsVideoGeneration: false,
      supportsEmbeddings: false,
      maxContextLength: undefined,
      maxOutputTokens: undefined,
      isDefault: false,
    },
    validate: {
      modelId: (value) => !value?.trim() ? 'Model ID is required' : null,
      providerId: (value) => !value?.trim() ? 'Provider is required' : null,
      providerModelId: (value) => !value?.trim() ? 'Provider model ID is required' : null,
      priority: (value) => value < 0 || value > 1000 ? 'Priority must be between 0 and 1000' : null,
    },
  });

  const handleSubmit = async (values: FormValues) => {
    const createData: CreateModelProviderMappingDto = {
      modelId: values.modelId,
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
      supportsVideoGeneration: values.supportsVideoGeneration,
      supportsEmbeddings: values.supportsEmbeddings,
      maxContextLength: values.maxContextLength,
      maxOutputTokens: values.maxOutputTokens,
      isDefault: values.isDefault,
    };

    await createMapping.mutateAsync(createData);
    form.reset();
    onSuccess?.();
    onClose();
  };

  const providerOptions = providers?.map((p: ProviderCredentialDto) => ({
    value: p.id.toString(),
    label: p.providerName,
  })) || [];

  const handleCapabilitiesDetected = (capabilities: Record<string, boolean>) => {
    // Update form values based on detected capabilities
    Object.entries(capabilities).forEach(([key, value]) => {
      if (form.values.hasOwnProperty(key)) {
        form.setFieldValue(key as keyof FormValues, value);
      }
    });
  };

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title="Create Model Mapping"
      size="lg"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          <Alert icon={<IconInfoCircle size={16} />} color="blue">
            Model mappings allow you to route requests for a specific model ID to any provider that supports it.
          </Alert>

          {!providersLoading && providerOptions.length === 0 && (
            <Alert icon={<IconInfoCircle size={16} />} color="yellow">
              No providers configured. Please configure at least one LLM provider before creating model mappings.
            </Alert>
          )}

          <TextInput
            label="Model ID"
            placeholder="e.g., gpt-4, claude-3-opus, my-custom-model"
            description="The model identifier that clients will request"
            required
            {...form.getInputProps('modelId')}
          />

          <Select
            label="Provider"
            placeholder={providersLoading ? "Loading providers..." : providerOptions.length === 0 ? "No providers configured" : "Select provider"}
            description="The provider that will handle requests for this model"
            data={providerOptions}
            required
            disabled={providersLoading || providerOptions.length === 0}
            nothingFoundMessage="No providers found. Please configure a provider first."
            {...form.getInputProps('providerId')}
          />

          <ProviderModelSelect
            providerId={form.values.providerId}
            value={form.values.providerModelId}
            onChange={(value) => form.setFieldValue('providerModelId', value)}
            onCapabilitiesDetected={handleCapabilitiesDetected}
            label="Provider Model ID"
            placeholder="e.g., gpt-4-1106-preview"
            description="The actual model ID to use with the selected provider"
            required
            error={form.errors.providerModelId as string | undefined}
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
            label="Enable mapping immediately"
            description="Start routing requests through this mapping right away"
            {...form.getInputProps('isEnabled', { type: 'checkbox' })}
          />

          <Divider label="Capabilities" labelPosition="center" />

          <Text size="sm" c="dimmed">
            Select the capabilities supported by this model. These affect routing decisions.
          </Text>

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

          <Divider label="Context Limits (Optional)" labelPosition="center" />

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
              loading={createMapping.isPending}
            >
              Create Mapping
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}