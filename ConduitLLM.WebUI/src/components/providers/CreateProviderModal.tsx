'use client';

import {
  Modal,
  TextInput,
  Switch,
  Button,
  Group,
  Text,
  Select,
  PasswordInput,
  Alert,
  Divider,
  Stack,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useCreateProvider, useTestProviderConnection } from '@/hooks/useConduitAdmin';
import { IconAlertCircle, IconInfoCircle, IconCircleCheck } from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { validators } from '@/lib/utils/form-validators';

interface CreateProviderModalProps {
  opened: boolean;
  onClose: () => void;
}

interface CreateProviderForm {
  providerType: string;
  apiKey: string;
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

export function CreateProviderModal({ opened, onClose }: CreateProviderModalProps) {
  const createProvider = useCreateProvider();
  const testProviderConnection = useTestProviderConnection();
  const [testingConnection, setTestingConnection] = useState(false);
  const [testResult, setTestResult] = useState<{ 
    success: boolean; 
    message: string; 
    errorDetails?: string;
    responseTimeMs?: number;
    modelsAvailable?: string[];
  } | null>(null);

  const form = useForm<CreateProviderForm>({
    initialValues: {
      providerType: '',
      apiKey: '',
      apiEndpoint: '',
      organizationId: '',
      isEnabled: true,
    },
    validate: {
      providerType: validators.required('Provider type'),
      apiKey: validators.required('API key'),
      apiEndpoint: (value, values) => {
        if (values.providerType === 'custom' && (!value || value.trim().length === 0)) {
          return 'API endpoint is required for custom providers';
        }
        if (value && value.trim()) {
          return validators.url(value);
        }
        return null;
      },
    },
  });


  const getProviderInfo = (providerType: string) => {
    const info: Record<string, { endpoint?: string; docs?: string; note?: string }> = {
      openai: {
        endpoint: 'https://api.openai.com/v1',
        docs: 'https://platform.openai.com/docs',
        note: 'Use your OpenAI API key from the dashboard',
      },
      anthropic: {
        endpoint: 'https://api.anthropic.com',
        docs: 'https://docs.anthropic.com',
        note: 'Use your Anthropic API key',
      },
      azure: {
        docs: 'https://docs.microsoft.com/azure/cognitive-services/openai/',
        note: 'Enter your Azure OpenAI endpoint and API key',
      },
      minimax: {
        endpoint: 'https://api.minimax.chat/v1',
        docs: 'https://www.minimax.chat/document',
        note: 'Use your MiniMax API key and group ID',
      },
    };
    return info[providerType] || {};
  };

  const handleTestConnection = async () => {
    if (!form.isValid()) {
      form.validate();
      return;
    }

    const providerConfig = {
      providerName: form.values.providerType, // This should be the provider type (e.g., "openai"), not the user's name
      apiKey: form.values.apiKey.trim(),
      apiEndpoint: form.values.apiEndpoint?.trim() || undefined,
      organizationId: form.values.organizationId?.trim() || undefined,
    };

    setTestingConnection(true);
    setTestResult(null);
    try {
      const result = await testProviderConnection.mutateAsync(providerConfig);
      setTestResult(result);
    } catch (error: unknown) {
      // Error notification is handled by the hook
      // But we can also capture error details if available
      if (error instanceof Error) {
        setTestResult({
          success: false,
          message: error.message,
          errorDetails: undefined
        });
      }
    } finally {
      setTestingConnection(false);
    }
  };

  const selectedProviderInfo = getProviderInfo(form.values.providerType);

  // Reset test result when form values change
  useEffect(() => {
    setTestResult(null);
  }, [form.values.apiKey, form.values.providerType, form.values.apiEndpoint]);

  return (
    <Modal opened={opened} onClose={onClose} title="Add Provider" size="lg">
      <form
        onSubmit={form.onSubmit((values) => {
          const payload = {
            providerName: values.providerType, // Use provider type as the provider name
            apiKey: values.apiKey.trim(),
            apiEndpoint: values.apiEndpoint?.trim() || undefined,
            organizationId: values.organizationId?.trim() || undefined,
            isEnabled: values.isEnabled,
          };
          
          createProvider.mutate(payload, {
            onSuccess: () => {
              notifications.show({
                title: 'Success',
                message: 'Provider created successfully',
                color: 'green',
              });
              onClose();
              form.reset();
            },
            onError: (_error: unknown) => {
              notifications.show({
                title: 'Error',
                message: 'Failed to create provider',
                color: 'red',
              });
            },
          });
        })}
      >
        <Stack gap="md">
          <Select
            label="Provider Type"
            placeholder="Select provider type"
            required
            data={PROVIDER_TYPES}
            {...form.getInputProps('providerType')}
          />

          {selectedProviderInfo.note && (
            <Alert icon={<IconInfoCircle size={16} />} color="blue" variant="light">
              <Text size="sm">{selectedProviderInfo.note}</Text>
              {selectedProviderInfo.docs && (
                <Text size="xs" mt={4}>
                  <a 
                    href={selectedProviderInfo.docs} 
                    target="_blank" 
                    rel="noopener noreferrer"
                    style={{ color: 'inherit' }}
                  >
                    View documentation →
                  </a>
                </Text>
              )}
            </Alert>
          )}

          <Divider label="Authentication" labelPosition="left" />

          <PasswordInput
            label="API Key"
            placeholder="Enter your API key"
            required
            {...form.getInputProps('apiKey')}
          />

          <TextInput
            label="API Endpoint"
            placeholder={selectedProviderInfo.endpoint || "Enter API endpoint URL"}
            description={form.values.providerType === 'custom' ? 'Required for custom providers' : 'Leave empty to use default endpoint'}
            {...form.getInputProps('apiEndpoint')}
          />

          {['openai', 'azure'].includes(form.values.providerType) && (
            <TextInput
              label="Organization ID"
              placeholder="Optional organization or deployment ID"
              {...form.getInputProps('organizationId')}
            />
          )}

          <Switch
            label="Enable provider"
            description="Whether this provider should be active and available for use"
            {...form.getInputProps('isEnabled', { type: 'checkbox' })}
          />

          <Alert icon={<IconAlertCircle size={16} />} color="orange" variant="light">
            <Text size="sm">
              <strong>Security:</strong> API keys are encrypted and stored securely. 
              Test the connection before saving to ensure your credentials are valid.
            </Text>
          </Alert>

          <Group justify="flex-start" mt="md">
            <Button 
              variant="light"
              onClick={handleTestConnection}
              loading={testingConnection}
              disabled={!form.values.apiKey || !form.values.providerType}
            >
              Test Connection
            </Button>
          </Group>

          {testResult && (
            <Alert 
              color={testResult.success ? 'green' : 'red'} 
              variant="light"
              icon={testResult.success ? <IconCircleCheck size={16} /> : <IconAlertCircle size={16} />}
              mt="md"
            >
              <Text size="sm" fw={500}>{testResult.message}</Text>
              {testResult.errorDetails && (
                <Text size="xs" c="dimmed" mt={4}>
                  {testResult.errorDetails}
                </Text>
              )}
              {testResult.success && testResult.responseTimeMs && (
                <Text size="xs" c="dimmed" mt={4}>
                  Response time: {testResult.responseTimeMs}ms
                </Text>
              )}
            </Alert>
          )}

          <Group justify="flex-end" mt="md">
            <Button variant="subtle" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={createProvider.isPending}>
              Add Provider
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}