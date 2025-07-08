'use client';

import {
  TextInput,
  Switch,
  Stack,
  Group,
  Text,
  Select,
  NumberInput,
  MultiSelect,
  Alert,
  Divider,
  Card,
  Badge,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useUpdateModelMapping, useProviders } from '@/hooks/api/useAdminApi';
import { IconAlertCircle, IconInfoCircle } from '@tabler/icons-react';
import { useEffect, useState } from 'react';
import { FormModal } from '@/components/common/FormModal';
import { validators } from '@/lib/utils/form-validators';
import { formatters } from '@/lib/utils/formatters';

import type { ModelProviderMappingDto } from '@knn_labs/conduit-admin-client';

interface EditModelMappingModalProps {
  opened: boolean;
  onClose: () => void;
  modelMapping: ModelProviderMappingDto | null;
}

interface EditModelMappingForm {
  internalModelName: string;
  providerModelName: string;
  providerName: string;
  isEnabled: boolean;
  capabilities: string[];
  priority: number;
}

const CAPABILITY_OPTIONS = [
  { value: 'chat', label: 'Chat Completion' },
  { value: 'embedding', label: 'Embeddings' },
  { value: 'function_calling', label: 'Function Calling' },
  { value: 'vision', label: 'Vision/Image Understanding' },
  { value: 'json_mode', label: 'JSON Mode' },
  { value: 'streaming', label: 'Streaming' },
  { value: 'code_generation', label: 'Code Generation' },
  { value: 'reasoning', label: 'Advanced Reasoning' },
];

export function EditModelMappingModal({ opened, onClose, modelMapping }: EditModelMappingModalProps) {
  const updateModelMappingMutation = useUpdateModelMapping();
  const { data: providers } = useProviders();
  interface ProviderInfo {
    healthStatus: string;
    modelsAvailable: number;
  }
  
  const [selectedProvider, setSelectedProvider] = useState<ProviderInfo | null>(null);

  // Create a mutation wrapper that handles the id
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const mutation: any = {
    ...updateModelMappingMutation,
    mutate: (values: EditModelMappingForm, options?: Parameters<typeof updateModelMappingMutation.mutate>[1]) => {
      if (!modelMapping) return;
      updateModelMappingMutation.mutate({
        id: modelMapping.id.toString(),
        data: {
          ...values,
          capabilities: values.capabilities.join(','),
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        } as any,
      }, options);
    },
    mutateAsync: async (values: EditModelMappingForm) => {
      if (!modelMapping) throw new Error('No model mapping to update');
      return updateModelMappingMutation.mutateAsync({
        id: modelMapping.id.toString(),
        data: {
          ...values,
          capabilities: values.capabilities.join(','),
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        } as any,
      });
    },
  };

  const form = useForm<EditModelMappingForm>({
    initialValues: {
      internalModelName: '',
      providerModelName: '',
      providerName: '',
      isEnabled: true,
      capabilities: [],
      priority: 100,
    },
    validate: {
      internalModelName: (value) => {
        const requiredError = validators.required('Internal model name')(value);
        if (requiredError) return requiredError;
        
        const minLengthError = validators.minLength('Model name', 3)(value);
        if (minLengthError) return minLengthError;
        
        return null;
      },
      providerModelName: validators.required('Provider model name'),
      providerName: validators.required('Provider'),
      priority: (value) => {
        if (value < 0 || value > 1000) {
          return 'Priority must be between 0 and 1000';
        }
        return null;
      },
      capabilities: (value) => {
        if (!value || value.length === 0) {
          return 'At least one capability must be selected';
        }
        return null;
      },
    },
  });

  // Update form when model mapping changes
  useEffect(() => {
    if (modelMapping) {
      // Build capabilities array from boolean flags
      const capabilities = [];
      if (modelMapping.supportsVision) capabilities.push('vision');
      if (modelMapping.supportsImageGeneration) capabilities.push('image_generation');
      if (modelMapping.supportsAudioTranscription) capabilities.push('audio_transcription');
      if (modelMapping.supportsTextToSpeech) capabilities.push('text_to_speech');
      if (modelMapping.supportsRealtimeAudio) capabilities.push('realtime_audio');
      if (modelMapping.supportsFunctionCalling) capabilities.push('function_calling');
      if (modelMapping.supportsStreaming) capabilities.push('streaming');
      
      form.setValues({
        internalModelName: modelMapping.modelId,
        providerModelName: modelMapping.providerModelId,
        providerName: modelMapping.providerId,
        isEnabled: modelMapping.isEnabled,
        capabilities: capabilities,
        priority: modelMapping.priority,
      });
      
      // Find and set the selected provider
      const provider = providers?.find((p: unknown) => (p as { providerName: string }).providerName === modelMapping.providerId);
      if (provider && typeof provider === 'object' && 'healthStatus' in provider && 'modelsAvailable' in provider) {
        setSelectedProvider(provider as ProviderInfo);
      } else {
        setSelectedProvider(null);
      }
    }
  }, [modelMapping, providers, form]);

  const handleClose = () => {
    setSelectedProvider(null);
    onClose();
  };

  if (!modelMapping) {
    return null;
  }

  const providerOptions = providers?.map((p: unknown) => {
    const provider = p as { providerName: string };
    return {
      value: provider.providerName,
      label: provider.providerName,
    };
  }) || [];

  return (
    <FormModal
      opened={opened}
      onClose={handleClose}
      title="Edit Model Mapping"
      size="lg"
      form={form}
      mutation={mutation}
      entityType="Model mapping"
      isEdit={true}
      submitText="Save Changes"
      initialValues={undefined}
    >
      {(form) => (
        <>
          {/* Mapping Info Card */}
          <Card withBorder>
            <Stack gap="xs">
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Created</Text>
                <Text size="sm">{formatters.date(modelMapping.createdAt)}</Text>
              </Group>
              {modelMapping.updatedAt && (
                <Group justify="space-between">
                  <Text size="sm" c="dimmed">Last Updated</Text>
                  <Text size="sm">{formatters.date(modelMapping.updatedAt)}</Text>
                </Group>
              )}
            </Stack>
          </Card>

          <Divider />

          <TextInput
            label="Internal Model Name"
            placeholder="e.g., gpt-4, claude-3-opus"
            description="The name clients will use to request this model"
            required
            {...form.getInputProps('internalModelName')}
          />

          <Select
            label="Provider"
            placeholder="Select provider"
            description="The provider that will handle requests for this model"
            required
            data={providerOptions}
            searchable
            {...form.getInputProps('providerName')}
            onChange={(value) => {
              form.setFieldValue('providerName', value || '');
              const provider = providers?.find((p: unknown) => (p as { providerName: string }).providerName === value);
              if (provider && typeof provider === 'object' && 'healthStatus' in provider && 'modelsAvailable' in provider) {
                setSelectedProvider(provider as ProviderInfo);
              } else {
                setSelectedProvider(null);
              }
            }}
          />

          {selectedProvider && (
            <Alert icon={<IconInfoCircle size={16} />} color="blue" variant="light">
              <Text size="sm">
                Provider has {selectedProvider.modelsAvailable} models available.
                Status: <Badge size="sm" variant="light" color={
                  selectedProvider.healthStatus === 'healthy' ? 'green' : 'red'
                }>
                  {selectedProvider.healthStatus}
                </Badge>
              </Text>
            </Alert>
          )}

          <TextInput
            label="Provider Model Name"
            placeholder="e.g., gpt-4-0125-preview, claude-3-opus-20240229"
            description="The actual model name used by the provider"
            required
            {...form.getInputProps('providerModelName')}
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
            description="Whether this mapping should be active and available for use"
            {...form.getInputProps('isEnabled', { type: 'checkbox' })}
          />

          <Alert icon={<IconAlertCircle size={16} />} color="orange" variant="light">
            <Text size="sm">
              Changes to model mappings take effect immediately and will affect all incoming requests.
            </Text>
          </Alert>
        </>
      )}
    </FormModal>
  );
}