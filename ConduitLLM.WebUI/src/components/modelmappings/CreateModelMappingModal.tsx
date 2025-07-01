'use client';

import {
  Modal,
  TextInput,
  Switch,
  Button,
  Stack,
  Group,
  Text,
  Select,
  NumberInput,
  MultiSelect,
  Alert,
  Divider,
  Badge,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useCreateModelMapping, useProviders } from '@/hooks/api/useAdminApi';
import { IconAlertCircle, IconInfoCircle } from '@tabler/icons-react';
import { useEffect, useState } from 'react';
import { notifications } from '@mantine/notifications';

interface CreateModelMappingModalProps {
  opened: boolean;
  onClose: () => void;
}

interface CreateModelMappingForm {
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

// Common model presets for quick setup
const MODEL_PRESETS = [
  { 
    internal: 'gpt-4', 
    provider: 'gpt-4-0125-preview',
    capabilities: ['chat', 'function_calling', 'vision', 'json_mode', 'streaming', 'code_generation', 'reasoning']
  },
  { 
    internal: 'gpt-3.5-turbo', 
    provider: 'gpt-3.5-turbo-0125',
    capabilities: ['chat', 'function_calling', 'json_mode', 'streaming']
  },
  { 
    internal: 'claude-3-opus', 
    provider: 'claude-3-opus-20240229',
    capabilities: ['chat', 'vision', 'streaming', 'code_generation', 'reasoning']
  },
  { 
    internal: 'claude-3-sonnet', 
    provider: 'claude-3-sonnet-20240229',
    capabilities: ['chat', 'vision', 'streaming', 'code_generation']
  },
  { 
    internal: 'gemini-pro', 
    provider: 'gemini-1.5-pro',
    capabilities: ['chat', 'function_calling', 'vision', 'streaming', 'reasoning']
  },
];

export function CreateModelMappingModal({ opened, onClose }: CreateModelMappingModalProps) {
  const createModelMapping = useCreateModelMapping();
  const { data: providers } = useProviders();
  const [selectedProvider, setSelectedProvider] = useState<any>(null);
  const [showPresets, setShowPresets] = useState(true);

  const form = useForm<CreateModelMappingForm>({
    initialValues: {
      internalModelName: '',
      providerModelName: '',
      providerName: '',
      isEnabled: true,
      capabilities: ['chat'],
      priority: 100,
    },
    validate: {
      internalModelName: (value) => {
        if (!value || value.trim().length === 0) {
          return 'Internal model name is required';
        }
        if (value.length < 3) {
          return 'Model name must be at least 3 characters';
        }
        if (!/^[a-zA-Z0-9-_.]+$/.test(value)) {
          return 'Model name can only contain letters, numbers, hyphens, dots, and underscores';
        }
        return null;
      },
      providerModelName: (value) => {
        if (!value || value.trim().length === 0) {
          return 'Provider model name is required';
        }
        return null;
      },
      providerName: (value) => {
        if (!value) {
          return 'Provider is required';
        }
        return null;
      },
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

  // Update selected provider info when provider changes
  useEffect(() => {
    if (form.values.providerName) {
      const provider = providers?.find((p: any) => p.providerName === form.values.providerName);
      setSelectedProvider(provider);
    }
  }, [form.values.providerName, providers]);

  const handleSubmit = async (values: CreateModelMappingForm) => {
    try {
      const payload = {
        internalModelName: values.internalModelName.trim(),
        providerModelName: values.providerModelName.trim(),
        providerName: values.providerName,
        isEnabled: values.isEnabled,
        capabilities: values.capabilities,
        priority: values.priority,
      };

      await createModelMapping.mutateAsync(payload);
      
      // Reset form and close modal on success
      form.reset();
      onClose();
    } catch (error) {
      // Error is handled by the mutation hook
      console.error('Create model mapping error:', error);
    }
  };

  const handleClose = () => {
    form.reset();
    setSelectedProvider(null);
    setShowPresets(true);
    onClose();
  };

  const applyPreset = (preset: typeof MODEL_PRESETS[0]) => {
    form.setValues({
      internalModelName: preset.internal,
      providerModelName: preset.provider,
      capabilities: preset.capabilities,
    });
    setShowPresets(false);
  };

  const providerOptions = providers?.map((p: any) => ({
    value: p.providerName,
    label: p.providerName,
    disabled: !p.isEnabled,
  })) || [];

  return (
    <Modal
      opened={opened}
      onClose={handleClose}
      title="Create Model Mapping"
      size="lg"
      centered
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          {showPresets && MODEL_PRESETS.length > 0 && (
            <>
              <Alert icon={<IconInfoCircle size={16} />} color="blue" variant="light">
                <Text size="sm" fw={500} mb="xs">Quick Setup - Popular Models</Text>
                <Group gap="xs">
                  {MODEL_PRESETS.map((preset) => (
                    <Button
                      key={preset.internal}
                      size="xs"
                      variant="light"
                      onClick={() => applyPreset(preset)}
                    >
                      {preset.internal}
                    </Button>
                  ))}
                </Group>
              </Alert>
              <Divider label="Or configure manually" labelPosition="center" />
            </>
          )}

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
          />

          {selectedProvider && (
            <Alert 
              icon={<IconInfoCircle size={16} />} 
              color={selectedProvider.healthStatus === 'healthy' ? 'green' : 'orange'} 
              variant="light"
            >
              <Group gap="xs">
                <Text size="sm">
                  Provider Status:
                </Text>
                <Badge size="sm" variant="light" color={
                  selectedProvider.healthStatus === 'healthy' ? 'green' : 'red'
                }>
                  {selectedProvider.healthStatus}
                </Badge>
                <Text size="sm" c="dimmed">
                  • {selectedProvider.modelsAvailable} models available
                </Text>
              </Group>
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
            description="Whether this mapping should be active immediately"
            {...form.getInputProps('isEnabled', { type: 'checkbox' })}
          />

          <Alert icon={<IconAlertCircle size={16} />} color="orange" variant="light">
            <Text size="sm">
              Model mappings allow you to route requests from a standardized model name to specific provider implementations.
              Multiple mappings can exist for the same internal model name with different priorities.
            </Text>
          </Alert>

          <Group justify="flex-end" mt="md">
            <Button 
              variant="subtle" 
              onClick={handleClose}
              disabled={createModelMapping.isPending}
            >
              Cancel
            </Button>
            <Button 
              type="submit" 
              loading={createModelMapping.isPending}
              disabled={!form.isValid()}
            >
              Create Mapping
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}