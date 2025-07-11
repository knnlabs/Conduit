'use client';

import {
  Modal,
  TextInput,
  Switch,
  Button,
  Group,
  Text,
  PasswordInput,
  Alert,
  Divider,
  Stack,
  Card,
  Badge,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { IconInfoCircle, IconCircleCheck, IconCircleX } from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { validators } from '@/lib/utils/form-validators';
import { 
  ProviderType, 
  PROVIDER_DISPLAY_NAMES, 
  PROVIDER_CONFIG_REQUIREMENTS 
} from '@/lib/constants/providers';

interface Provider {
  id: string;
  name: string;
  type: string;
  isEnabled: boolean;
  healthStatus?: 'healthy' | 'unhealthy' | 'unknown';
  lastHealthCheck?: string;
  endpoint?: string;
  supportedModels: string[];
  configuration: Record<string, unknown>;
  createdDate: string;
  modifiedDate: string;
  models?: string[];
}

interface EditProviderModalProps {
  opened: boolean;
  onClose: () => void;
  provider: Provider | null;
  onSuccess?: () => void;
}

interface EditProviderForm {
  providerName: string;
  apiKey?: string;
  apiEndpoint?: string;
  organizationId?: string;
  isEnabled: boolean;
}

// Use provider display names from constants

export function EditProviderModal({ opened, onClose, provider, onSuccess }: EditProviderModalProps) {
  const [isSubmitting, setIsSubmitting] = useState(false);

  const form = useForm<EditProviderForm>({
    initialValues: {
      providerName: '',
      apiKey: '',
      apiEndpoint: '',
      organizationId: '',
      isEnabled: true,
    },
    validate: {
      providerName: validators.required('Provider name'),
      apiEndpoint: (value) => {
        if (value && !validators.url(value)) {
          return 'Please enter a valid URL';
        }
        return null;
      },
    },
  });

  // Update form when provider changes
  useEffect(() => {
    if (provider) {
      form.setValues({
        providerName: provider.name,
        apiKey: '', // Don't show existing key for security
        apiEndpoint: provider.endpoint || '',
        organizationId: (provider.configuration?.organizationId as string) || '',
        isEnabled: provider.isEnabled,
      });
    }
  }, [provider]);

  const handleSubmit = async (values: EditProviderForm) => {
    if (!provider) return;

    setIsSubmitting(true);
    try {
      const payload = {
        id: parseInt(provider.id, 10),
        providerName: values.providerName,
        apiKey: values.apiKey || undefined, // Only send if changed
        apiEndpoint: values.apiEndpoint || undefined,
        organizationId: values.organizationId || undefined,
        isEnabled: values.isEnabled,
      };

      const response = await fetch(`/api/providers/${provider.id}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(payload),
      });

      if (!response.ok) {
        throw new Error('Failed to update provider');
      }

      notifications.show({
        title: 'Success',
        message: 'Provider updated successfully',
        color: 'green',
      });
      
      onClose();
      if (onSuccess) {
        onSuccess();
      }
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: error instanceof Error ? error.message : 'Failed to update provider',
        color: 'red',
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleClose = () => {
    form.reset();
    onClose();
  };

  if (!provider) {
    return null;
  }

  const providerDisplayName = provider.type 
    ? PROVIDER_DISPLAY_NAMES[provider.type as ProviderType] 
    : provider.type;
  const getHealthIcon = (status?: string) => {
    switch (status) {
      case 'healthy':
        return <IconCircleCheck size={16} color="var(--mantine-color-green-6)" />;
      case 'unhealthy':
        return <IconCircleX size={16} color="var(--mantine-color-red-6)" />;
      default:
        return null;
    }
  };

  return (
    <Modal opened={opened} onClose={handleClose} title="Edit Provider" size="lg">
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          {/* Provider Info Card */}
          <Card withBorder>
            <Stack gap="xs">
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Provider Type</Text>
                <Badge>{providerDisplayName || provider.type || 'Unknown'}</Badge>
              </Group>
              {provider.healthStatus && (
                <Group justify="space-between">
                  <Text size="sm" c="dimmed">Health Status</Text>
                  <Group gap="xs">
                    {getHealthIcon(provider.healthStatus)}
                    <Text size="sm" fw={500} c={
                      provider.healthStatus === 'healthy' ? 'green' :
                      provider.healthStatus === 'unhealthy' ? 'red' : 'gray'
                    }>
                      {provider.healthStatus}
                    </Text>
                  </Group>
                </Group>
              )}
              {provider.models && provider.models.length > 0 && (
                <Group justify="space-between">
                  <Text size="sm" c="dimmed">Models Available</Text>
                  <Text size="sm">{provider.models.length}</Text>
                </Group>
              )}
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
            placeholder="Enter provider name"
            required
            {...form.getInputProps('providerName')}
          />

          <PasswordInput
            label="API Key"
            placeholder="Leave empty to keep existing key"
            description="Only enter if you want to update the API key"
            {...form.getInputProps('apiKey')}
          />

          {(() => {
            const config = PROVIDER_CONFIG_REQUIREMENTS[provider.type as ProviderType];
            if (!config) return null;

            return (
              <>
                {(config.requiresEndpoint || config.supportsCustomEndpoint) && (
                  <TextInput
                    label={config.requiresEndpoint ? "API Endpoint" : "Custom API Endpoint"}
                    placeholder={config.requiresEndpoint ? "API endpoint URL" : "Custom API endpoint URL (optional)"}
                    {...form.getInputProps('apiEndpoint')}
                  />
                )}

                {config.requiresOrganizationId && (
                  <TextInput
                    label="Organization ID"
                    placeholder={provider.type === ProviderType.OpenAI ? "Optional OpenAI organization ID" : "Organization ID"}
                    {...form.getInputProps('organizationId')}
                  />
                )}
              </>
            );
          })()}

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

          <Divider />

          <Group justify="flex-end">
            <Button variant="light" onClick={handleClose}>
              Cancel
            </Button>
            <Button type="submit" loading={isSubmitting}>
              Save Changes
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}