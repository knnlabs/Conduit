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
  Card,
  Badge,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useUpdateModelMapping, useProviders } from '@/hooks/api/useAdminApi';
import { IconAlertCircle, IconInfoCircle } from '@tabler/icons-react';
import { useEffect, useState } from 'react';
// Removed unused notifications import

interface ModelMapping {
  id: string;
  internalModelName: string;
  providerModelName: string;
  providerName: string;
  isEnabled: boolean;
  capabilities: string[];
  priority: number;
  createdAt: string;
  lastUsed?: string;
  requestCount: number;
}

interface EditModelMappingModalProps {
  opened: boolean;
  onClose: () => void;
  modelMapping: ModelMapping | null;
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
  const updateModelMapping = useUpdateModelMapping();
  const { data: providers } = useProviders();
  interface ProviderInfo {
    healthStatus: string;
    modelsAvailable: number;
  }
  
  const [selectedProvider, setSelectedProvider] = useState<ProviderInfo | null>(null);

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
        if (!value || value.trim().length === 0) {
          return 'Internal model name is required';
        }
        if (value.length < 3) {
          return 'Model name must be at least 3 characters';
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

  // Update form when model mapping changes
  useEffect(() => {
    if (modelMapping) {
      form.setValues({
        internalModelName: modelMapping.internalModelName,
        providerModelName: modelMapping.providerModelName,
        providerName: modelMapping.providerName,
        isEnabled: modelMapping.isEnabled,
        capabilities: modelMapping.capabilities,
        priority: modelMapping.priority,
      });
      
      // Find and set the selected provider
      const provider = providers?.find((p: unknown) => (p as { providerName: string }).providerName === modelMapping.providerName);
      if (provider && typeof provider === 'object' && 'healthStatus' in provider && 'modelsAvailable' in provider) {
        setSelectedProvider(provider as ProviderInfo);
      } else {
        setSelectedProvider(null);
      }
    }
  }, [modelMapping, providers, form]);

  const handleSubmit = async (values: EditModelMappingForm) => {
    if (!modelMapping) return;

    try {
      const payload = {
        internalModelName: values.internalModelName.trim(),
        providerModelName: values.providerModelName.trim(),
        providerName: values.providerName,
        isEnabled: values.isEnabled,
        capabilities: values.capabilities,
        priority: values.priority,
      };

      await updateModelMapping.mutateAsync({
        id: modelMapping.id,
        data: payload,
      });
      
      onClose();
    } catch (error: unknown) {
      // Error is handled by the mutation hook
      console.error('Update model mapping error:', error);
    }
  };

  const handleClose = () => {
    form.reset();
    setSelectedProvider(null);
    onClose();
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
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
    <Modal
      opened={opened}
      onClose={handleClose}
      title="Edit Model Mapping"
      size="lg"
      centered
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          {/* Mapping Info Card */}
          <Card withBorder>
            <Stack gap="xs">
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Created</Text>
                <Text size="sm">{formatDate(modelMapping.createdAt)}</Text>
              </Group>
              {modelMapping.lastUsed && (
                <Group justify="space-between">
                  <Text size="sm" c="dimmed">Last Used</Text>
                  <Text size="sm">{formatDate(modelMapping.lastUsed)}</Text>
                </Group>
              )}
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Request Count</Text>
                <Text size="sm">{modelMapping.requestCount.toLocaleString()}</Text>
              </Group>
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

          <Group justify="flex-end" mt="md">
            <Button 
              variant="subtle" 
              onClick={handleClose}
              disabled={updateModelMapping.isPending}
            >
              Cancel
            </Button>
            <Button 
              type="submit" 
              loading={updateModelMapping.isPending}
              disabled={!form.isValid()}
            >
              Save Changes
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}