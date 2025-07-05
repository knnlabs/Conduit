'use client';

import {
  TextInput,
  Switch,
  Button,
  Stack,
  Group,
  Text,
  Textarea,
  PasswordInput,
  Alert,
  Divider,
  Badge,
  Card,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useUpdateProvider } from '@/hooks/api/useAdminApi';
import { IconInfoCircle, IconCircleCheck, IconCircleX } from '@tabler/icons-react';
import { useState } from 'react';
import { FormModal } from '@/components/common/FormModal';
import { validators } from '@/lib/utils/form-validators';

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
        const requiredError = validators.required('Provider name')(value);
        if (requiredError) return requiredError;
        
        const minLengthError = validators.minLength('Provider name', 3)(value);
        if (minLengthError) return minLengthError;
        
        return null;
      },
    },
  });


  const handleTestConnection = async () => {
    if (!provider || !onTest) return;
    
    setTestingConnection(true);
    await onTest(provider);
    setTestingConnection(false);
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
        providerName: values.providerName.trim(),
        description: values.description?.trim() || undefined,
        apiKey: values.apiKey?.trim() || undefined, // Only update if provided
        apiEndpoint: values.apiEndpoint?.trim() || undefined,
        organizationId: values.organizationId?.trim() || undefined,
        isEnabled: values.isEnabled,
      };
      updateProvider.mutate({
        id: provider.id,
        data: payload,
      }, options);
    },
    mutateAsync: async (values: EditProviderForm) => {
      const payload = {
        providerName: values.providerName.trim(),
        description: values.description?.trim() || undefined,
        apiKey: values.apiKey?.trim() || undefined, // Only update if provided
        apiEndpoint: values.apiEndpoint?.trim() || undefined,
        organizationId: values.organizationId?.trim() || undefined,
        isEnabled: values.isEnabled,
      };
      return updateProvider.mutateAsync({
        id: provider.id,
        data: payload,
      });
    },
  };

  const providerType = PROVIDER_TYPES.find(p => p.value === provider.providerType);

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
      initialValues={{
        providerName: provider.providerName,
        description: provider.description || '',
        apiKey: '', // Don't show existing API key for security
        apiEndpoint: provider.apiEndpoint || '',
        organizationId: provider.organizationId || '',
        isEnabled: provider.isEnabled,
      }}
    >
      {(form) => (
        <>
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

          <Group justify="flex-start" mt="md">
            <Button 
              variant="light"
              onClick={handleTestConnection}
              loading={testingConnection}
              disabled={updateProvider.isPending || !onTest}
            >
              Test Connection
            </Button>
          </Group>
        </>
      )}
    </FormModal>
  );
}