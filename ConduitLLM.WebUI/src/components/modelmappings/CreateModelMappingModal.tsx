'use client';

import {
  Modal,
  TextInput,
  Select,
  NumberInput,
  Switch,
  MultiSelect,
  Button,
  Stack,
  Group,
  Alert,
  Text,
  Loader,
  Center,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconAlertCircle } from '@tabler/icons-react';
import { useCreateModelMapping, useProviders } from '@/hooks/useConduitAdmin';
import { ProviderModelSelect } from '@/components/common/ProviderModelSelect';
import type { CreateModelProviderMappingDto } from '@knn_labs/conduit-admin-client';

interface CreateModelMappingModalProps {
  opened: boolean;
  onClose: () => void;
}

interface FormValues {
  modelId: string;
  providerId: string;
  providerModelId: string;
  priority: number;
  isEnabled: boolean;
  capabilities: string[];
}

const CAPABILITY_OPTIONS = [
  { value: 'vision', label: 'Vision/Image Understanding' },
  { value: 'function_calling', label: 'Function Calling' },
  { value: 'streaming', label: 'Streaming' },
  { value: 'image_generation', label: 'Image Generation' },
  { value: 'audio_transcription', label: 'Audio Transcription' },
  { value: 'text_to_speech', label: 'Text to Speech' },
  { value: 'realtime_audio', label: 'Realtime Audio' },
];

export function CreateModelMappingModal({ opened, onClose }: CreateModelMappingModalProps) {
  const { data: providers, isLoading: providersLoading, error: providersError } = useProviders();
  const createMutation = useCreateModelMapping();

  const form = useForm<FormValues>({
    initialValues: {
      modelId: '',
      providerId: '',
      providerModelId: '',
      priority: 100,
      isEnabled: true,
      capabilities: ['streaming'],
    },
    validate: {
      modelId: (value) => {
        if (!value) return 'Internal model name is required';
        if (value.length < 3) return 'Model name must be at least 3 characters';
        if (!/^[a-zA-Z0-9-_.]+$/.test(value)) {
          return 'Model name can only contain letters, numbers, hyphens, dots, and underscores';
        }
        return null;
      },
      providerId: (value) => !value ? 'Provider is required' : null,
      providerModelId: (value) => !value ? 'Provider model name is required' : null,
      priority: (value) => {
        if (value < 0 || value > 1000) return 'Priority must be between 0 and 1000';
        return null;
      },
      capabilities: (value) => {
        if (!value || value.length === 0) return 'At least one capability must be selected';
        return null;
      },
    },
  });

  const handleSubmit = async (values: FormValues) => {
    // Ensure capabilities is always an array
    const capabilities = Array.isArray(values.capabilities) ? values.capabilities : [];
    
    const mappingData: CreateModelProviderMappingDto = {
      modelId: values.modelId.trim(),
      providerId: values.providerId.trim(),
      providerModelId: values.providerModelId.trim(),
      priority: values.priority,
      isEnabled: values.isEnabled,
      supportsVision: capabilities.includes('vision'),
      supportsFunctionCalling: capabilities.includes('function_calling'),
      supportsStreaming: capabilities.includes('streaming'),
      supportsImageGeneration: capabilities.includes('image_generation'),
      supportsAudioTranscription: capabilities.includes('audio_transcription'),
      supportsTextToSpeech: capabilities.includes('text_to_speech'),
      supportsRealtimeAudio: capabilities.includes('realtime_audio'),
      capabilities: capabilities.join(','),
    };

    try {
      await createMutation.mutateAsync(mappingData);
      form.reset();
      onClose();
    } catch (error) {
      console.error('Failed to create model mapping:', error);
    }
  };

  const handleClose = () => {
    form.reset();
    onClose();
  };

  // SIMPLIFIED: Just show loading/error states, don't try to map undefined data
  if (providersLoading) {
    return (
      <Modal opened={opened} onClose={handleClose} title="Create Model Mapping" size="lg">
        <Center py="xl">
          <Loader size="sm" />
        </Center>
      </Modal>
    );
  }

  if (providersError) {
    return (
      <Modal opened={opened} onClose={handleClose} title="Create Model Mapping" size="lg">
        <Alert icon={<IconAlertCircle size={16} />} color="red">
          Failed to load providers. Please try again.
        </Alert>
      </Modal>
    );
  }

  // FIXED: Ensure providers is an array before trying to map
  console.log('CreateModelMappingModal providers data:', {
    providers,
    type: typeof providers,
    isArray: Array.isArray(providers),
  });
  
  const providerOptions = Array.isArray(providers) 
    ? providers.map((provider) => {
        console.log('Processing provider:', provider);
        return {
          value: provider.providerName,
          label: provider.providerName,
          disabled: !provider.isEnabled,
        };
      })
    : [];

  return (
    <Modal
      opened={opened}
      onClose={handleClose}
      title="Create Model Mapping"
      size="lg"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          {providerOptions.length === 0 ? (
            <Alert icon={<IconAlertCircle size={16} />} color="orange" variant="light">
              <Text size="sm">
                No providers configured. Please configure at least one provider first.
              </Text>
            </Alert>
          ) : (
            <>
              <Select
                label="Provider"
                placeholder="Select provider"
                description="The provider that will handle requests for this model"
                required
                data={providerOptions}
                searchable
                {...form.getInputProps('providerId')}
              />

              <ProviderModelSelect
                providerId={form.values.providerId}
                value={form.values.providerModelId}
                onChange={(value) => form.setFieldValue('providerModelId', value)}
                onCapabilitiesDetected={(detectedCapabilities) => {
                  // Only update capabilities if user hasn't manually selected any
                  if (form.values.capabilities.length === 1 && form.values.capabilities[0] === 'streaming') {
                    form.setFieldValue('capabilities', detectedCapabilities);
                  }
                }}
                label="Provider Model Name"
                placeholder="Select or type a model name"
                description="The actual model name used by the provider"
                required
                error={form.errors.providerModelId as string | undefined}
              />

              <TextInput
                label="Internal Model Name"
                placeholder="e.g., gpt-4, claude-3-opus"
                description="The name clients will use to request this model"
                required
                {...form.getInputProps('modelId')}
              />

              <MultiSelect
                label="Capabilities"
                placeholder="Select model capabilities"
                description="Features this model supports"
                required
                data={CAPABILITY_OPTIONS}
                {...form.getInputProps('capabilities')}
              />

              <NumberInput
                label="Priority"
                placeholder="100"
                description="Higher priority mappings are used first (0-1000)"
                min={0}
                max={1000}
                {...form.getInputProps('priority')}
              />

              <Switch
                label="Enable mapping"
                description="Whether this mapping should be active immediately"
                {...form.getInputProps('isEnabled', { type: 'checkbox' })}
              />

              <Alert icon={<IconAlertCircle size={16} />} color="blue" variant="light">
                <Text size="sm">
                  Model mappings route requests from a standardized model name to specific provider implementations.
                  Multiple mappings can exist for the same model with different priorities.
                </Text>
              </Alert>
            </>
          )}

          <Group justify="flex-end" mt="md">
            <Button variant="subtle" onClick={handleClose}>
              Cancel
            </Button>
            <Button
              type="submit"
              loading={createMutation.isPending}
              disabled={providerOptions.length === 0}
            >
              Create Mapping
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}