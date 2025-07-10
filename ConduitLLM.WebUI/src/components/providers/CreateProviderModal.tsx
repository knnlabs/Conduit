'use client';

import {
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
<<<<<<< HEAD
import { notifications } from '@mantine/notifications';
=======
import { useCreateProvider, useTestProviderConnection } from '@/hooks/useConduitAdmin';
import { FormModal } from '@/components/common/FormModal';
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6
import { IconAlertCircle, IconInfoCircle, IconCircleCheck } from '@tabler/icons-react';
import { useState } from 'react';
import { validators } from '@/lib/utils/form-validators';

interface CreateProviderModalProps {
  opened: boolean;
  onClose: () => void;
  onSuccess?: () => void;
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

export function CreateProviderModal({ opened, onClose, onSuccess }: CreateProviderModalProps) {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);
  const [isTesting, setIsTesting] = useState(false);

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
        if (values.providerType === 'custom' && !value) {
          return 'API endpoint is required for custom providers';
        }
        if (value && !validators.url(value)) {
          return 'Please enter a valid URL';
        }
        return null;
      },
    },
  });

<<<<<<< HEAD
  const handleSubmit = async (values: CreateProviderForm) => {
    setIsSubmitting(true);
    try {
      const payload = {
        providerName: values.providerType,
        providerType: values.providerType,
        apiKey: values.apiKey,
        apiEndpoint: values.apiEndpoint || undefined,
        organizationId: values.organizationId || undefined,
        isEnabled: values.isEnabled,
      };
=======
  // Create mutation wrapper for payload transformation
  const mutationWrapper = {
    ...createProvider,
    mutate: (values: CreateProviderForm, options?: any) => {
      const payload = {
        providerName: values.providerType, // Use provider type as the provider name
        apiKey: values.apiKey.trim(),
        apiEndpoint: values.apiEndpoint?.trim() || undefined,
        organizationId: values.organizationId?.trim() || undefined,
        isEnabled: values.isEnabled,
      };
      
      createProvider.mutate(payload, options);
    },
  };
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6

      const response = await fetch('/api/providers', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(payload),
      });

      if (!response.ok) {
        throw new Error('Failed to create provider');
      }

      notifications.show({
        title: 'Success',
        message: 'Provider created successfully',
        color: 'green',
      });

      form.reset();
      setTestResult(null);
      onSuccess?.();
      onClose();
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: error instanceof Error ? error.message : 'Failed to create provider',
        color: 'red',
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleTestConnection = async () => {
    const validation = form.validate();
    if (validation.hasErrors) {
      return;
    }

    setIsTesting(true);
    setTestResult(null);

    try {
      // Create a temporary provider to test
      const tempPayload = {
        providerName: form.values.providerType,
        providerType: form.values.providerType,
        apiKey: form.values.apiKey,
        apiEndpoint: form.values.apiEndpoint || undefined,
        organizationId: form.values.organizationId || undefined,
      };

      // First create the provider temporarily
      const createResponse = await fetch('/api/providers', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(tempPayload),
      });

      if (!createResponse.ok) {
        throw new Error('Failed to create test provider');
      }

      const provider = await createResponse.json();

      // Test the connection
      const testResponse = await fetch(`/api/providers/${provider.id}/test`, {
        method: 'POST',
      });

      if (!testResponse.ok) {
        throw new Error('Failed to test connection');
      }

      const result = await testResponse.json();
      
      setTestResult({
        success: result.isSuccessful,
        message: result.message || (result.isSuccessful ? 'Connection successful!' : 'Connection failed'),
      });

      // Delete the temporary provider
      await fetch(`/api/providers/${provider.id}`, {
        method: 'DELETE',
      });
    } catch (error) {
      setTestResult({
        success: false,
        message: error instanceof Error ? error.message : 'Failed to test connection',
      });
    } finally {
      setIsTesting(false);
    }
  };

  const handleClose = () => {
    form.reset();
    setTestResult(null);
    onClose();
  };

  const getProviderHelp = (providerType: string) => {
    switch (providerType) {
      case 'openai':
        return 'You can find your API key at platform.openai.com/api-keys';
      case 'anthropic':
        return 'Get your API key from console.anthropic.com/settings/keys';
      case 'azure':
        return 'You\'ll need your deployment endpoint and API key from Azure Portal';
      case 'aws-bedrock':
        return 'Ensure your AWS credentials have Bedrock access configured';
      default:
        return null;
    }
  };

  const providerHelp = getProviderHelp(form.values.providerType);

  return (
<<<<<<< HEAD
    <Modal opened={opened} onClose={handleClose} title="Add LLM Provider" size="lg">
      <form onSubmit={form.onSubmit(handleSubmit)}>
=======
    <FormModal
      opened={opened}
      onClose={onClose}
      title="Add Provider"
      form={form}
      mutation={mutationWrapper}
      entityType="provider"
      submitText="Add Provider"
    >
      {(form) => (
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6
        <Stack gap="md">
          <Select
            label="Provider Type"
            placeholder="Select a provider"
            data={PROVIDER_TYPES}
            required
            {...form.getInputProps('providerType')}
          />

          {providerHelp && (
            <Alert icon={<IconInfoCircle size={16} />} color="blue" variant="light">
              <Text size="sm">{providerHelp}</Text>
            </Alert>
          )}

          <PasswordInput
            label="API Key"
            placeholder="Enter your API key"
            required
            {...form.getInputProps('apiKey')}
          />

          <TextInput
            label="API Endpoint"
            placeholder="https://api.example.com"
            {...form.getInputProps('apiEndpoint')}
            description={form.values.providerType === 'custom' ? 'Required for custom providers' : 'Optional - leave empty for default'}
          />

          <TextInput
            label="Organization ID"
            placeholder="Optional organization identifier"
            {...form.getInputProps('organizationId')}
          />

          <Switch
            label="Enable Provider"
            description="Provider will be available for use immediately"
            {...form.getInputProps('isEnabled', { type: 'checkbox' })}
          />

          <Divider />

          {testResult && (
            <Alert
              icon={testResult.success ? <IconCircleCheck size={16} /> : <IconAlertCircle size={16} />}
              color={testResult.success ? 'green' : 'red'}
              variant="light"
            >
              <Text size="sm">{testResult.message}</Text>
            </Alert>
          )}
<<<<<<< HEAD

          <Group justify="space-between">
            <Button
              variant="subtle"
              onClick={handleTestConnection}
              loading={isTesting}
              disabled={isSubmitting}
            >
              Test Connection
            </Button>

            <Group>
              <Button variant="light" onClick={handleClose}>
                Cancel
              </Button>
              <Button type="submit" loading={isSubmitting}>
                Create Provider
              </Button>
            </Group>
          </Group>
=======
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6
        </Stack>
      )}
    </FormModal>
  );
}