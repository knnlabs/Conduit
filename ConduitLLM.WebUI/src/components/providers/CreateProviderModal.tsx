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
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useCreateProvider, useTestProviderConnection } from '@/hooks/api/useAdminApi';
import { IconAlertCircle, IconInfoCircle } from '@tabler/icons-react';
import { useState } from 'react';
import { notifications } from '@mantine/notifications';

interface CreateProviderModalProps {
  opened: boolean;
  onClose: () => void;
}

interface CreateProviderForm {
  providerName: string;
  providerType: string;
  description?: string;
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

  const form = useForm<CreateProviderForm>({
    initialValues: {
      providerName: '',
      providerType: '',
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
      providerType: (value) => {
        if (!value) {
          return 'Provider type is required';
        }
        return null;
      },
      apiKey: (value) => {
        if (!value || value.trim().length === 0) {
          return 'API key is required';
        }
        return null;
      },
      apiEndpoint: (value, values) => {
        if (values.providerType === 'custom' && (!value || value.trim().length === 0)) {
          return 'API endpoint is required for custom providers';
        }
        if (value && !isValidUrl(value)) {
          return 'Please enter a valid URL';
        }
        return null;
      },
    },
  });

  const isValidUrl = (string: string) => {
    try {
      new URL(string);
      return true;
    } catch (_error: unknown) {
      return false;
    }
  };

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
      providerName: form.values.providerName.trim(),
      providerType: form.values.providerType,
      credentials: {
        apiKey: form.values.apiKey.trim(),
        apiEndpoint: form.values.apiEndpoint?.trim() || undefined,
        organizationId: form.values.organizationId?.trim() || undefined,
      },
    };

    setTestingConnection(true);
    try {
      await testProviderConnection.mutateAsync(providerConfig);
    } catch (error: unknown) {
      // Error notification is handled by the hook
    } finally {
      setTestingConnection(false);
    }
  };

  const handleSubmit = async (values: CreateProviderForm) => {
    try {
      const payload = {
        providerName: values.providerName.trim(),
        providerType: values.providerType,
        description: values.description?.trim() || undefined,
        credentials: {
          apiKey: values.apiKey.trim(),
          apiEndpoint: values.apiEndpoint?.trim() || undefined,
          organizationId: values.organizationId?.trim() || undefined,
        },
        isEnabled: values.isEnabled,
      };

      await createProvider.mutateAsync(payload);
      
      // Reset form and close modal on success
      form.reset();
      onClose();
    } catch (error: unknown) {
      console.error('Create provider error:', error);
    }
  };

  const handleClose = () => {
    form.reset();
    onClose();
  };

  const selectedProviderInfo = getProviderInfo(form.values.providerType);

  return (
    <Modal
      opened={opened}
      onClose={handleClose}
      title="Add Provider"
      size="lg"
      centered
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          <TextInput
            label="Provider Name"
            placeholder="Enter a name for this provider instance"
            required
            {...form.getInputProps('providerName')}
          />

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

          <Textarea
            label="Description"
            placeholder="Optional description for this provider"
            rows={2}
            {...form.getInputProps('description')}
          />

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

          <Group justify="space-between" mt="md">
            <Button 
              variant="light"
              onClick={handleTestConnection}
              loading={testingConnection}
              disabled={!form.values.apiKey || !form.values.providerType}
            >
              Test Connection
            </Button>
            
            <Group>
              <Button 
                variant="subtle" 
                onClick={handleClose}
                disabled={createProvider.isPending || testingConnection}
              >
                Cancel
              </Button>
              <Button 
                type="submit" 
                loading={createProvider.isPending}
                disabled={!form.isValid() || testingConnection}
              >
                Add Provider
              </Button>
            </Group>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}