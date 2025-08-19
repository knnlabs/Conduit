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
import { useState, useEffect, useCallback } from 'react';
import { validators } from '@/lib/utils/form-validators';
import { 
  ProviderType, 
  PROVIDER_CONFIG_REQUIREMENTS,
  type ProviderDto
} from '@knn_labs/conduit-admin-client';
import { withAdminClient } from '@/lib/client/adminClient';
import { getProviderTypeFromDto, getProviderDisplayName } from '@/lib/utils/providerTypeUtils';


interface EditProviderModalProps {
  opened: boolean;
  onClose: () => void;
  provider: ProviderDto | null;
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
  const [initialFormValues, setInitialFormValues] = useState<EditProviderForm>(() => ({
    providerName: '',
    apiKey: '',
    apiEndpoint: '',
    organizationId: '',
    isEnabled: true,
  }));

  const form = useForm<EditProviderForm>({
    initialValues: initialFormValues,
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

  // Stable callback for form updates
  const updateForm = useCallback((newFormValues: EditProviderForm) => {
    setInitialFormValues(newFormValues);
    form.setValues(newFormValues);
    form.resetDirty();
  }, [form]);

  // Update form when provider changes
  useEffect(() => {
    if (provider && opened) {
      const apiProvider = provider;
      
      const newFormValues: EditProviderForm = {
        providerName: typeof apiProvider.providerName === 'string' ? apiProvider.providerName : '',
        apiKey: '', // Don't show existing key for security
        apiEndpoint: apiProvider.baseUrl ?? '',
        organizationId: '', // Organization is now managed at the key level
        isEnabled: provider.isEnabled === true,
      };
      
      updateForm(newFormValues);
    }
  }, [provider, opened, updateForm]);

  const handleSubmit = async (values: EditProviderForm) => {
    if (!provider) return;

    setIsSubmitting(true);
    try {
      const payload = {
        providerName: values.providerName ?? undefined,
        baseUrl: values.apiEndpoint ?? undefined,
        organization: values.organizationId ?? undefined,
        isEnabled: values.isEnabled,
      };

      await withAdminClient(client => 
        client.providers.update(provider.id, payload)
      );

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
    const apiProvider = provider;
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
              {/* Health status, models, and lastHealthCheck are not available in ProviderDto */}
            </Stack>
          </Card>

          <Divider />

          <TextInput
            key={`provider-name-${provider.id}`}
            label="Provider Name"
            placeholder="Enter a friendly name for this provider"
            description="A friendly name to identify this provider instance"
            autoComplete="off"
            aria-autocomplete="none"
            list="autocompleteOff"
            data-form-type="other"
            {...form.getInputProps('providerName')}
            autoFocus
          />

          <PasswordInput
            label="API Key"
            placeholder="Leave empty to keep existing key"
            description="Only enter if you want to update the API key"
            autoComplete="off"
            aria-autocomplete="none"
            list="autocompleteOff"
            data-form-type="other"
            data-lpignore="true"
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
                    aria-autocomplete="none"
                    list="autocompleteOff"
                    data-form-type="other"
                    {...form.getInputProps('apiEndpoint')}
                  />
                )}

                {config.requiresOrganizationId && (
                  <TextInput
                    label="Organization ID"
                    placeholder={getProviderTypeFromDto(provider) === ProviderType.OpenAI ? "Optional OpenAI organization ID" : "Organization ID"}
                    autoComplete="off"
                    aria-autocomplete="none"
                    list="autocompleteOff"
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