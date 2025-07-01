'use client';

import {
  Modal,
  TextInput,
  Switch,
  Button,
  Stack,
  Group,
  Text,
  Textarea,
  Select,
  PasswordInput,
  Alert,
  Divider,
  Badge,
  Card,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useUpdateProvider } from '@/hooks/api/useAdminApi';
import { IconAlertCircle, IconInfoCircle, IconCircleCheck, IconCircleX } from '@tabler/icons-react';
import { useEffect, useState } from 'react';
import { notifications } from '@mantine/notifications';

interface Provider {
  id: string;
  providerName: string;
  providerType?: string;
  isEnabled: boolean;
  healthStatus: 'healthy' | 'unhealthy' | 'unknown';
  lastHealthCheck?: string;
  modelsAvailable: number;
  createdAt: string;
  apiEndpoint?: string;
  description?: string;
  organizationId?: string;
}

interface EditProviderModalProps {
  opened: boolean;
  onClose: () => void;
  provider: Provider | null;
  onTest?: (provider: Provider) => void;
}

interface EditProviderForm {
  providerName: string;
  description?: string;
  apiKey?: string;
  apiEndpoint?: string;
  organizationId?: string;
  isEnabled: boolean;
}

const PROVIDER_TYPES = [
  { value: 'openai', label: 'OpenAI' },
  { value: 'anthropic', label: 'Anthropic' },
  { value: 'azure', label: 'Azure OpenAI' },
  { value: 'aws-bedrock', label: 'AWS Bedrock' },
  { value: 'google', label: 'Google AI' },
  { value: 'cohere', label: 'Cohere' },
  { value: 'minimax', label: 'MiniMax' },
  { value: 'replicate', label: 'Replicate' },
  { value: 'huggingface', label: 'Hugging Face' },
  { value: 'custom', label: 'Custom Provider' },
];

export function EditProviderModal({ opened, onClose, provider, onTest }: EditProviderModalProps) {
  const updateProvider = useUpdateProvider();
  const [testingConnection, setTestingConnection] = useState(false);

  const form = useForm<EditProviderForm>({
    initialValues: {
      providerName: '',
      description: '',
      apiKey: '',
      apiEndpoint: '',
      organizationId: '',
      isEnabled: true,
    },
    validate: {
      providerName: (value) => {
        if (!value || value.trim().length === 0) {
          return 'Provider name is required';
        }
        if (value.length < 3) {
          return 'Provider name must be at least 3 characters';
        }
        return null;
      },
    },
  });

  // Update form when provider changes
  useEffect(() => {
    if (provider) {
      form.setValues({
        providerName: provider.providerName,
        description: provider.description || '',
        apiKey: '', // Don't show existing API key for security
        apiEndpoint: provider.apiEndpoint || '',
        organizationId: provider.organizationId || '',
        isEnabled: provider.isEnabled,
      });
    }
  }, [provider]);

  const handleSubmit = async (values: EditProviderForm) => {
    if (!provider) return;

    try {
      const payload = {
        providerName: values.providerName.trim(),
        description: values.description?.trim() || undefined,
        apiKey: values.apiKey?.trim() || undefined, // Only update if provided
        apiEndpoint: values.apiEndpoint?.trim() || undefined,
        organizationId: values.organizationId?.trim() || undefined,
        isEnabled: values.isEnabled,
      };

      await updateProvider.mutateAsync({
        id: provider.id,
        data: payload,
      });
      
      onClose();
    } catch (error) {
      // Error is handled by the mutation hook
      console.error('Update provider error:', error);
    }
  };

  const handleTestConnection = async () => {
    if (!provider || !onTest) return;
    
    setTestingConnection(true);
    await onTest(provider);
    setTestingConnection(false);
  };

  const handleClose = () => {
    form.reset();
    onClose();
  };

  if (!provider) {
    return null;
  }

  const providerType = PROVIDER_TYPES.find(p => p.value === provider.providerType);

  return (
    <Modal
      opened={opened}
      onClose={handleClose}
      title="Edit Provider"
      size="md"
      centered
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          {/* Provider Info Card */}
          <Card withBorder>
            <Stack gap="xs">
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Provider Type</Text>
                <Badge>{providerType?.label || provider.providerType || 'Unknown'}</Badge>
              </Group>
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Health Status</Text>
                <Group gap="xs">
                  {provider.healthStatus === 'healthy' && (
                    <IconCircleCheck size={16} color="var(--mantine-color-green-6)" />
                  )}
                  {provider.healthStatus === 'unhealthy' && (
                    <IconCircleX size={16} color="var(--mantine-color-red-6)" />
                  )}
                  <Text size="sm" fw={500} c={
                    provider.healthStatus === 'healthy' ? 'green' :
                    provider.healthStatus === 'unhealthy' ? 'red' : 'gray'
                  }>
                    {provider.healthStatus}
                  </Text>
                </Group>
              </Group>
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Models Available</Text>
                <Text size="sm">{provider.modelsAvailable}</Text>
              </Group>
              {provider.lastHealthCheck && (
                <Group justify="space-between">
                  <Text size="sm" c="dimmed">Last Health Check</Text>
                  <Text size="sm">{new Date(provider.lastHealthCheck).toLocaleString()}</Text>
                </Group>
              )}
            </Stack>
          </Card>

          <Divider />

          <TextInput
            label="Provider Name"
            placeholder="Enter a descriptive name for this provider"
            required
            {...form.getInputProps('providerName')}
          />

          <Textarea
            label="Description"
            placeholder="Optional description for this provider"
            rows={3}
            {...form.getInputProps('description')}
          />

          <PasswordInput
            label="API Key"
            placeholder="Leave empty to keep existing key"
            description="Only enter if you want to update the API key"
            {...form.getInputProps('apiKey')}
          />

          {(provider.providerType === 'azure' || provider.providerType === 'custom') && (
            <TextInput
              label="API Endpoint"
              placeholder="Custom API endpoint URL"
              {...form.getInputProps('apiEndpoint')}
            />
          )}

          {provider.providerType === 'openai' && (
            <TextInput
              label="Organization ID"
              placeholder="Optional OpenAI organization ID"
              {...form.getInputProps('organizationId')}
            />
          )}

          <Switch
            label="Enable provider"
            description="Whether this provider should be active and available for use"
            {...form.getInputProps('isEnabled', { type: 'checkbox' })}
          />

          <Alert icon={<IconInfoCircle size={16} />} color="blue" variant="light">
            <Text size="sm">
              Updating provider credentials will automatically refresh the available models list.
            </Text>
          </Alert>

          <Group justify="space-between" mt="md">
            <Button 
              variant="light"
              onClick={handleTestConnection}
              loading={testingConnection}
              disabled={updateProvider.isPending || !onTest}
            >
              Test Connection
            </Button>
            
            <Group gap="sm">
              <Button 
                variant="subtle" 
                onClick={handleClose}
                disabled={updateProvider.isPending}
              >
                Cancel
              </Button>
              <Button 
                type="submit" 
                loading={updateProvider.isPending}
                disabled={!form.isValid()}
              >
                Save Changes
              </Button>
            </Group>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}