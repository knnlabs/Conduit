'use client';

import {
  Modal,
  TextInput,
  Select,
  NumberInput,
  Switch,
  MultiSelect,
  Alert,
  Text,
  Loader,
  Center,
  Button,
  Group,
  Stack,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconAlertCircle } from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { notifications } from '@mantine/notifications';
import { ProviderModelSelect } from '@/components/common/ProviderModelSelect';
import type { CreateModelProviderMappingDto } from '@/types/api-types';

interface CreateModelMappingModalProps {
  opened: boolean;
  onClose: () => void;
  onSuccess?: () => void;
}

interface FormValues {
  modelId: string;
  providerId: string;
  providerModelId: string;
  priority: number;
  isEnabled: boolean;
  capabilities: string[];
}

interface Provider {
  id: number;
  name: string;
  providerType: string;
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

export function CreateModelMappingModal({ opened, onClose, onSuccess }: CreateModelMappingModalProps) {
  const [providers, setProviders] = useState<Provider[]>([]);
  const [providersLoading, setProvidersLoading] = useState(true);
  const [providersError, setProvidersError] = useState<Error | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

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

  // Fetch providers on mount
  useEffect(() => {
    fetchProviders();
  }, []);

  const fetchProviders = async () => {
    try {
      setProvidersLoading(true);
      const response = await fetch('/api/providers');
      if (!response.ok) {
        throw new Error('Failed to fetch providers');
      }
      const data = await response.json();
      setProviders(data);
    } catch (err) {
      setProvidersError(err instanceof Error ? err : new Error('Unknown error'));
    } finally {
      setProvidersLoading(false);
    }
  };

  const handleClose = () => {
    form.reset();
    onClose();
  };

  const handleSubmit = async (values: FormValues) => {
    // Ensure capabilities is always an array
    const capabilities = Array.isArray(values.capabilities) ? values.capabilities : [];
    
    const mappingData = {
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
      setIsSubmitting(true);
      const response = await fetch('/api/model-mappings', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(mappingData),
      });

      if (!response.ok) {
        throw new Error('Failed to create model mapping');
      }

      notifications.show({
        title: 'Success',
        message: 'Model mapping created successfully',
        color: 'green',
      });
      
      handleClose();
      if (onSuccess) {
        onSuccess();
      }
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: error instanceof Error ? error.message : 'Failed to create model mapping',
        color: 'red',
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  // Handle loading/error states
  if (providersLoading) {
    return (
      <Modal opened={opened} onClose={onClose} title="Create Model Mapping" size="lg">
        <Center py="xl">
          <Loader size="sm" />
        </Center>
      </Modal>
    );
  }

  if (providersError) {
    return (
      <Modal opened={opened} onClose={onClose} title="Create Model Mapping" size="lg">
        <Alert icon={<IconAlertCircle size={16} />} color="red">
          Failed to load providers. Please try again.
        </Alert>
      </Modal>
    );
  }

  const providerOptions = Array.isArray(providers) 
    ? providers.map((provider) => ({
        value: String(provider.id),
        label: provider.name,
        disabled: false,
      }))
    : [];

  return (
    <Modal 
      opened={opened} 
      onClose={onClose} 
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
              loading={isSubmitting}
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