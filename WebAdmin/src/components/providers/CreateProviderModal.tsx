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
import { useState, useEffect } from 'react';
import { 
  ProviderType, 
  PROVIDER_CONFIG_REQUIREMENTS
} from '@knn_labs/conduit-admin-client';
import { withAdminClient } from '@/lib/client/adminClient';
import { getProviderDisplayName } from '@/lib/utils/providerTypeUtils';

interface CreateProviderModalProps {
  opened: boolean;
  onClose: () => void;
  onSuccess?: () => void;
}

interface CreateProviderForm {
  providerType: string;
  providerName: string;
  apiKey: string;
  apiEndpoint?: string;
  organizationId?: string;
  isEnabled: boolean;
}

interface ProviderOption {
  value: string;
  label: string;
}

export function CreateProviderModal({ opened, onClose, onSuccess }: CreateProviderModalProps) {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isTesting, setIsTesting] = useState(false);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);
  const [availableProviders, setAvailableProviders] = useState<ProviderOption[]>([]);
  const [isLoadingProviders, setIsLoadingProviders] = useState(false);

  const form = useForm<CreateProviderForm>({
    initialValues: {
      providerType: '',
      providerName: '',
      apiKey: '',
      apiEndpoint: '',
      organizationId: '',
      isEnabled: true,
    },
    validate: {
      providerType: (value) => (!value ? 'Provider type is required' : null),
      // Provider name is now optional - will use provider type if not provided
      apiKey: (value) => (!value ? 'API key is required' : null),
      apiEndpoint: (value) => {
        if (value && !value.startsWith('http://') && !value.startsWith('https://')) {
          return 'API endpoint must start with http:// or https://';
        }
        return null;
      },
    },
  });

  // Fetch available providers when modal opens
  useEffect(() => {
    const loadProviders = async () => {
      setIsLoadingProviders(true);
      try {
        const providerTypes = await withAdminClient(client => 
          client.providers.getAvailableProviderTypes()
        );
        
        const providers: ProviderOption[] = providerTypes.map(type => ({
          value: type.toString(),
          label: getProviderDisplayName(type)
        }));
        setAvailableProviders(providers);
        
        // If no providers are available, show a notification
        if (providers.length === 0) {
          notifications.show({
            title: 'No Providers Available',
            message: 'All provider types have already been configured.',
            color: 'orange',
          });
          onClose();
        }
      } catch (error) {
        console.error('Error fetching available providers:', error);
        notifications.show({
          title: 'Error',
          message: 'Failed to load available providers',
          color: 'red',
        });
      } finally {
        setIsLoadingProviders(false);
      }
    };
    
    if (opened) {
      void loadProviders();
    }
  }, [opened, onClose]);


  const handleClose = () => {
    form.reset();
    setTestResult(null);
    onClose();
  };

  const handleSubmit = async (values: CreateProviderForm) => {
    setIsSubmitting(true);
    try {
      // If no provider name was entered, use the provider type label
      let providerName = values.providerName.trim();
      if (!providerName) {
        const selectedProvider = availableProviders.find(p => p.value === values.providerType);
        providerName = selectedProvider?.label ?? 'Unknown Provider';
      }

      const payload = {
        providerType: parseInt(values.providerType, 10), // Send numeric provider type
        providerName: providerName,
        apiKey: values.apiKey,
        baseUrl: values.apiEndpoint ?? undefined,
        organization: values.organizationId ?? undefined,
        isEnabled: values.isEnabled,
      };

      await withAdminClient(client => 
        client.providers.create(payload)
      );

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
      const result = await withAdminClient(client => 
        client.providers.testConfig({
          providerType: parseInt(form.values.providerType, 10),
          apiKey: form.values.apiKey,
          baseUrl: form.values.apiEndpoint ?? undefined,
          organizationId: form.values.organizationId ?? undefined,
        })
      );
      
      // Handle new response format  
      const isSuccess = (result.result as string) === 'success';
      
      setTestResult({
        success: isSuccess,
        message: result.message ?? (isSuccess ? 'Connection successful' : 'Connection failed'),
      });
    } catch (error) {
      console.error('Connection test error:', error);
      setTestResult({
        success: false,
        message: 'Failed to test connection',
      });
    } finally {
      setIsTesting(false);
    }
  };

  const getProviderHelp = (providerType: string) => {
    const providerTypeNum = parseInt(providerType, 10) as ProviderType;
    const config = PROVIDER_CONFIG_REQUIREMENTS[providerTypeNum];
    if (!config?.helpText) {
      return null;
    }

    return (
      <Alert icon={<IconInfoCircle size={16} />} color="blue" variant="light">
        <Text size="sm">
          {config.helpUrl ? (
            <>
              {config.helpText.split(config.helpUrl)[0]}
              <Text component="span" fw={600}>{config.helpUrl}</Text>
              {config.helpText.split(config.helpUrl)[1] ?? ''}
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
            placeholder={isLoadingProviders ? "Loading available providers..." : "Select a provider"}
            data={availableProviders}
            required
            disabled={isLoadingProviders || availableProviders.length === 0}
            {...form.getInputProps('providerType')}
          />

          <TextInput
            label="Provider Name (Optional)"
            placeholder="Leave empty to use provider type name"
            description="A friendly name to identify this provider (e.g., 'Production OpenAI', 'Dev Ollama')"
            autoComplete="off"
            aria-autocomplete="none"
            list="autocompleteOff"
            data-form-type="other"
            {...form.getInputProps('providerName')}
          />

          <PasswordInput
            label="API Key"
            placeholder="Enter API key"
            required
            autoComplete="off"
            aria-autocomplete="none"
            list="autocompleteOff"
            data-form-type="other"
            data-lpignore="true"
            {...form.getInputProps('apiKey')}
          />

          {(() => {
            const providerTypeNum = parseInt(form.values.providerType, 10) as ProviderType;
            const config = PROVIDER_CONFIG_REQUIREMENTS[providerTypeNum];
            if (!config) return null;

            return (
              <>
                {config.requiresOrganizationId && (
                  <TextInput
                    label="Organization ID"
                    placeholder="Enter organization ID"
                    required={config.requiresOrganizationId}
                    autoComplete="off"
                    aria-autocomplete="none"
                    list="autocompleteOff"
                    data-form-type="other"
                    {...form.getInputProps('organizationId')}
                  />
                )}

                {(config.requiresEndpoint || config.supportsCustomEndpoint) && (
                  <TextInput
                    label={config.requiresEndpoint ? "API Endpoint" : "Custom API Endpoint"}
                    placeholder={config.requiresEndpoint ? "https://api.example.com" : "https://api.example.com (optional)"}
                    required={config.requiresEndpoint}
                    autoComplete="off"
                    aria-autocomplete="none"
                    list="autocompleteOff"
                    data-form-type="other"
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
              onClick={() => void handleTestConnection()}
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