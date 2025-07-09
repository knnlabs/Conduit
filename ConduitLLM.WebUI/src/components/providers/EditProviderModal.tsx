'use client';

import {
  TextInput,
  Switch,
  Button,
  Stack,
  Group,
  Text,
  PasswordInput,
  Alert,
  Divider,
  Badge,
  Card,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useUpdateProvider, useTestProviderConnection } from '@/hooks/useConduitAdmin';
import { IconInfoCircle, IconCircleCheck, IconCircleX } from '@tabler/icons-react';
import { useState, useMemo } from 'react';
import { FormModal } from '@/components/common/FormModal';

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
  const testProviderConnection = useTestProviderConnection();
  const [testingConnection, setTestingConnection] = useState(false);

  const form = useForm<EditProviderForm>({
    initialValues: {
      apiKey: '',
      apiEndpoint: '',
      organizationId: '',
      isEnabled: true,
    },
    validate: {
      // No validation needed - all fields are optional
    },
  });


  // Memoize initial values to prevent unnecessary re-renders
  // Must be called before any conditional returns to satisfy React hooks rules
  const initialValues = useMemo(() => ({
    apiKey: '', // Don't show existing API key for security
    apiEndpoint: provider?.apiEndpoint || '',
    organizationId: provider?.organizationId || '',
    isEnabled: provider?.isEnabled ?? true,
  }), [provider?.apiEndpoint, provider?.organizationId, provider?.isEnabled]);

  const handleTestConnection = async () => {
    if (!provider) return;
    
    setTestingConnection(true);
    try {
      // Test with the current form values (if provided) or existing provider values
      await testProviderConnection.mutateAsync({
        providerName: provider.providerName,
        apiKey: form.values.apiKey || undefined, // Only send if user entered a new key
        apiEndpoint: form.values.apiEndpoint || provider.apiEndpoint || undefined,
        organizationId: form.values.organizationId || provider.organizationId || undefined,
        isEnabled: form.values.isEnabled,
      });
    } catch (error) {
      // Error is already handled by the hook's onError
    } finally {
      setTestingConnection(false);
    }
  };

  if (!provider) {
    return null;
  }

  // Create a mutation wrapper that handles the payload transformation
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const mutation: any = {
    ...updateProvider,
    mutate: (values: EditProviderForm, options?: Parameters<typeof updateProvider.mutate>[1]) => {
      const payload = {
        id: parseInt(provider.id), // Include ID in payload as required by API
        apiKey: values.apiKey?.trim() ? values.apiKey.trim() : undefined, // Only update if provided
        apiEndpoint: values.apiEndpoint?.trim() ? values.apiEndpoint.trim() : undefined,
        organizationId: values.organizationId?.trim() ? values.organizationId.trim() : undefined,
        isEnabled: values.isEnabled,
      };
      console.log('Updating provider with payload:', payload);
      updateProvider.mutate({
        id: provider.id,
        data: payload,
      }, options);
    },
    mutateAsync: async (values: EditProviderForm) => {
      const payload = {
        id: parseInt(provider.id), // Include ID in payload as required by API
        apiKey: values.apiKey?.trim() ? values.apiKey.trim() : undefined, // Only update if provided
        apiEndpoint: values.apiEndpoint?.trim() ? values.apiEndpoint.trim() : undefined,
        organizationId: values.organizationId?.trim() ? values.organizationId.trim() : undefined,
        isEnabled: values.isEnabled,
      };
      return updateProvider.mutateAsync({
        id: provider.id,
        data: payload,
      });
    },
  };

  const providerType = PROVIDER_TYPES.find(p => p.value === provider.providerName);

  return (
    <FormModal
      opened={opened}
      onClose={onClose}
      title="Edit Provider"
      size="md"
      form={form}
      mutation={mutation}
      entityType="Provider"
      isEdit={true}
      submitText="Save Changes"
      initialValues={initialValues}
    >
      {(form) => (
        <>
          {/* Provider Info Card */}
          <Card withBorder>
            <Stack gap="xs">
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Provider</Text>
                <Badge>{providerType?.label || provider.providerName || 'Unknown'}</Badge>
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

          <PasswordInput
            label="API Key"
            placeholder="Leave empty to keep existing key"
            description="Only enter if you want to update the API key"
            {...form.getInputProps('apiKey')}
          />

          {(provider.providerName === 'azure' || provider.providerName === 'custom') && (
            <TextInput
              label="API Endpoint"
              placeholder="Custom API endpoint URL"
              {...form.getInputProps('apiEndpoint')}
            />
          )}

          {provider.providerName === 'openai' && (
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

          <Group justify="flex-start" mt="md">
            <Button 
              variant="light"
              onClick={handleTestConnection}
              loading={testingConnection}
              disabled={updateProvider.isPending || testProviderConnection.isPending}
            >
              Test Connection
            </Button>
          </Group>
        </>
      )}
    </FormModal>
  );
}