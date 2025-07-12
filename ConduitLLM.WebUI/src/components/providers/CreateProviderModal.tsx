'use client';

import {
  Modal,
  TextInput,
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
import { IconAlertCircle, IconInfoCircle, IconCircleCheck } from '@tabler/icons-react';
import { useState } from 'react';
import { 
  ProviderType, 
  PROVIDER_CONFIG_REQUIREMENTS, 
  getLLMProviderSelectOptions 
} from '@/lib/constants/providers';

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

// Provider options are now generated from constants
const PROVIDER_TYPES = getLLMProviderSelectOptions();

export function CreateProviderModal({ opened, onClose, onSuccess }: CreateProviderModalProps) {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isTesting, setIsTesting] = useState(false);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);

  const form = useForm<CreateProviderForm>({
    initialValues: {
      providerType: '',
      apiKey: '',
      apiEndpoint: '',
      organizationId: '',
      isEnabled: true,
    },
    validate: {
      providerType: (value) => (!value ? 'Provider type is required' : null),
      apiKey: (value) => (!value ? 'API key is required' : null),
      apiEndpoint: (value) => {
        if (value && !value.startsWith('http://') && !value.startsWith('https://')) {
          return 'API endpoint must start with http:// or https://';
        }
        return null;
      },
    },
  });

  const handleClose = () => {
    form.reset();
    setTestResult(null);
    onClose();
  };

  const handleSubmit = async (values: CreateProviderForm) => {
    setIsSubmitting(true);
    try {
      const payload = {
        providerName: values.providerType, // Use the provider type as-is (e.g., "openai")
        apiKey: values.apiKey,
        apiEndpoint: values.apiEndpoint || undefined,
        organizationId: values.organizationId || undefined,
        isEnabled: values.isEnabled,
      };

      const response = await fetch('/api/providers', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(payload),
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({ error: 'Failed to parse error response' }));
        console.error('Provider creation failed:', errorData);
        throw new Error(errorData.error || errorData.message || `Failed to create provider: ${response.status}`);
      }

      notifications.show({
        title: 'Success',
        message: 'Provider created successfully',
        color: 'green',
      });
      
      handleClose();
      if (onSuccess) {
        onSuccess();
      }
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Failed to create provider';
      
      // Check if it's a duplicate provider error
      if (errorMessage.includes('already exists')) {
        notifications.show({
          title: 'Provider Already Exists',
          message: `A provider of type "${values.providerType}" already exists. Please edit the existing provider or delete it first.`,
          color: 'orange',
        });
      } else {
        notifications.show({
          title: 'Error',
          message: errorMessage,
          color: 'red',
        });
      }
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
      const response = await fetch('/api/providers/test-config', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          providerName: form.values.providerType,
          apiKey: form.values.apiKey,
          apiEndpoint: form.values.apiEndpoint || undefined, // Changed from baseUrl to apiEndpoint
          organizationId: form.values.organizationId || undefined,
        }),
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({ error: `HTTP ${response.status}` }));
        console.error('Provider test failed:', errorData);
        setTestResult({
          success: false,
          message: errorData.error || errorData.message || `Connection failed: ${response.status}`,
        });
      } else {
        const result = await response.json();
        setTestResult({
          success: result.success,
          message: result.message || 'Connection successful',
        });
      }
    } catch (error) {
      setTestResult({
        success: false,
        message: 'Failed to test connection',
      });
    } finally {
      setIsTesting(false);
    }
  };

  const getProviderHelp = (providerType: string) => {
    const config = PROVIDER_CONFIG_REQUIREMENTS[providerType as ProviderType];
    if (!config || !config.helpText) {
      return null;
    }

    return (
      <Alert icon={<IconInfoCircle size={16} />} color="blue" variant="light">
        <Text size="sm">
          {config.helpUrl ? (
            <>
              {config.helpText.split(config.helpUrl)[0]}
              <Text component="span" fw={600}>{config.helpUrl}</Text>
              {config.helpText.split(config.helpUrl)[1] || ''}
            </>
          ) : (
            config.helpText
          )}
        </Text>
      </Alert>
    );
  };

  const providerHelp = getProviderHelp(form.values.providerType);

  return (
    <Modal opened={opened} onClose={handleClose} title="Add LLM Provider" size="lg">
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          <Select
            label="Provider Type"
            placeholder="Select a provider"
            data={PROVIDER_TYPES}
            required
            {...form.getInputProps('providerType')}
          />

          <PasswordInput
            label="API Key"
            placeholder="Enter API key"
            required
            {...form.getInputProps('apiKey')}
          />

          {(() => {
            const config = PROVIDER_CONFIG_REQUIREMENTS[form.values.providerType as ProviderType];
            if (!config) return null;

            return (
              <>
                {config.requiresOrganizationId && (
                  <TextInput
                    label="Organization ID"
                    placeholder="Enter organization ID"
                    required={config.requiresOrganizationId}
                    {...form.getInputProps('organizationId')}
                  />
                )}

                {(config.requiresEndpoint || config.supportsCustomEndpoint) && (
                  <TextInput
                    label={config.requiresEndpoint ? "API Endpoint" : "Custom API Endpoint"}
                    placeholder={config.requiresEndpoint ? "https://api.example.com" : "https://api.example.com (optional)"}
                    required={config.requiresEndpoint}
                    {...form.getInputProps('apiEndpoint')}
                  />
                )}
              </>
            );
          })()}

          {providerHelp}

          {testResult && (
            <Alert
              icon={testResult.success ? <IconCircleCheck size={16} /> : <IconAlertCircle size={16} />}
              color={testResult.success ? 'green' : 'red'}
              variant="light"
            >
              <Text size="sm">{testResult.message}</Text>
            </Alert>
          )}

          <Divider />

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
        </Stack>
      </form>
    </Modal>
  );
}