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
import { IconInfoCircle } from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { validators } from '@/lib/utils/form-validators';
import { 
  ProviderType, 
  PROVIDER_CONFIG_REQUIREMENTS 
} from '@/lib/constants/providers';
import type { ProviderCredentialDto } from '@knn_labs/conduit-admin-client';
import { getProviderTypeFromDto, getProviderDisplayName } from '@/lib/utils/providerTypeUtils';


interface EditProviderModalProps {
  opened: boolean;
  onClose: () => void;
  provider: ProviderCredentialDto | null;
  onSuccess?: () => void;
}

interface EditProviderForm {
  providerName?: string;
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
      providerName: (value) => {
        if (!value) {
          return 'Provider name is required';
        }
        return null;
      },
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
    if (provider && opened) {
      // Define a type that matches the actual API response
      type ApiProviderResponse = ProviderCredentialDto & {
        providerName?: string;
        baseUrl?: string;
        apiBase?: string;
      };
      
      const apiProvider = provider as ApiProviderResponse;
      
      form.setValues({
        providerName: typeof apiProvider.providerName === 'string' ? apiProvider.providerName : '',
        apiKey: '', // Don't show existing key for security
        apiEndpoint: apiProvider.baseUrl ?? apiProvider.apiBase ?? '',
        organizationId: typeof provider.organization === 'string' ? provider.organization : '',
        isEnabled: provider.isEnabled === true,
      });
    }
  }, [provider, opened, form]);

  const handleSubmit = async (values: EditProviderForm) => {
    if (!provider) return;

    setIsSubmitting(true);
    try {
      const payload = {
        providerName: values.providerName ?? undefined,
        apiKey: values.apiKey ?? undefined, // Only send if changed
        apiEndpoint: values.apiEndpoint ?? undefined,
        organizationId: values.organizationId ?? undefined,
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
        const errorData = await response.json().catch(() => ({ error: 'Failed to parse error response' })) as { error?: string; message?: string };
        throw new Error(errorData.error ?? errorData.message ?? `Failed to update provider: ${response.status}`);
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
    // Delay reset to prevent flicker during close animation
    setTimeout(() => {
      form.reset();
    }, 200);
    onClose();
  };

  if (!provider) {
    return null;
  }

  let providerDisplayName = 'Unknown Provider';
  try {
    const providerType = getProviderTypeFromDto(provider);
    providerDisplayName = getProviderDisplayName(providerType);
  } catch {
    // Fallback to provider name if available
    // Define a type that matches the actual API response
    type ApiProviderResponse = ProviderCredentialDto & {
      providerName?: string;
    };
    const apiProvider = provider as ApiProviderResponse;
    providerDisplayName = typeof apiProvider.providerName === 'string' ? apiProvider.providerName : 'Unknown Provider';
  }

  return (
    <Modal opened={opened} onClose={handleClose} title="Edit Provider" size="lg">
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          {/* Provider Info Card */}
          <Card withBorder>
            <Stack gap="xs">
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Provider Type</Text>
                <Badge>{providerDisplayName}</Badge>
              </Group>
              {/* Health status, models, and lastHealthCheck are not available in ProviderCredentialDto */}
            </Stack>
          </Card>

          <Divider />

          <TextInput
            key={`provider-name-${provider.id}`}
            label="Provider Name"
            placeholder="Enter a friendly name for this provider"
            description="A friendly name to identify this provider instance"
            autoComplete="off"
            data-form-type="other"
            {...form.getInputProps('providerName')}
            autoFocus
          />

          <PasswordInput
            label="API Key"
            placeholder="Leave empty to keep existing key"
            description="Only enter if you want to update the API key"
            autoComplete="new-password"
            data-form-type="other"
            {...form.getInputProps('apiKey')}
          />

          {(() => {
            const providerTypeNum = getProviderTypeFromDto(provider);
            const config = PROVIDER_CONFIG_REQUIREMENTS[providerTypeNum];
            if (!config) return null;

            return (
              <>
                {(config.requiresEndpoint || config.supportsCustomEndpoint) && (
                  <TextInput
                    label={config.requiresEndpoint ? "API Endpoint" : "Custom API Endpoint"}
                    placeholder={config.requiresEndpoint ? "API endpoint URL" : "Custom API endpoint URL (optional)"}
                    autoComplete="off"
                    data-form-type="other"
                    {...form.getInputProps('apiEndpoint')}
                  />
                )}

                {config.requiresOrganizationId && (
                  <TextInput
                    label="Organization ID"
                    placeholder={getProviderTypeFromDto(provider) === ProviderType.OpenAI ? "Optional OpenAI organization ID" : "Organization ID"}
                    autoComplete="off"
                    data-form-type="other"
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