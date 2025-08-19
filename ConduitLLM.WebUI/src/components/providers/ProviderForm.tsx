'use client';

import {
  Container,
  Paper,
  Title,
  Text,
  TextInput,
  Button,
  Group,
  Select,
  PasswordInput,
  Alert,
  Divider,
  Stack,
  Card,
  Badge,
  LoadingOverlay,
  ThemeIcon,
  Switch,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { IconAlertCircle, IconInfoCircle, IconCircleCheck, IconArrowLeft, IconServer, IconSparkles, IconEdit } from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { 
  ProviderType, 
  PROVIDER_CONFIG_REQUIREMENTS
} from '@/lib/constants/providers';
import type { ProviderCredentialDto } from '@knn_labs/conduit-admin-client';
import { withAdminClient } from '@/lib/client/adminClient';
import { getProviderTypeFromDto, getProviderDisplayName } from '@/lib/utils/providerTypeUtils';
import { validators } from '@/lib/utils/form-validators';

interface ProviderFormProps {
  mode: 'add' | 'edit';
  providerId?: number;
}

interface ProviderFormData {
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

export function ProviderForm({ mode, providerId }: ProviderFormProps) {
  const router = useRouter();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isTesting, setIsTesting] = useState(false);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);
  const [availableProviders, setAvailableProviders] = useState<ProviderOption[]>([]);
  const [isLoadingProviders, setIsLoadingProviders] = useState(mode === 'add');
  const [existingProvider, setExistingProvider] = useState<ProviderCredentialDto | null>(null);
  const [isLoadingProvider, setIsLoadingProvider] = useState(mode === 'edit');
  const [initialFormValues, setInitialFormValues] = useState<ProviderFormData>(() => ({
    providerType: '',
    providerName: '',
    apiKey: '',
    apiEndpoint: '',
    organizationId: '',
    isEnabled: true,
  }));

  const form = useForm<ProviderFormData>({
    initialValues: initialFormValues,
    validate: {
      providerType: (value) => (mode === 'add' && !value ? 'Provider type is required' : null),
      providerName: (value) => {
        if (mode === 'edit' && !value) {
          return 'Provider name is required';
        }
        return null;
      },
      apiKey: (value) => (mode === 'add' && !value ? 'API key is required' : null),
      apiEndpoint: (value) => {
        if (value && !validators.url(value)) {
          return 'Please enter a valid URL';
        }
        return null;
      },
    },
  });

  // Fetch available providers for add mode
  useEffect(() => {
    if (mode === 'add') {
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
          
          if (providers.length === 0) {
            notifications.show({
              title: 'No Providers Available',
              message: 'All provider types have already been configured.',
              color: 'orange',
            });
            router.push('/llm-providers');
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
      
      void loadProviders();
    }
  }, [mode, router]);

  // Fetch existing provider for edit mode and reinitialize form
  useEffect(() => {
    if (mode === 'edit' && providerId) {
      const loadProvider = async () => {
        setIsLoadingProvider(true);
        try {
          const provider = await withAdminClient(client => 
            client.providers.getById(providerId)
          );
          setExistingProvider(provider);
          
          const apiProvider = provider;
          
          // Create new form values
          const newFormValues: ProviderFormData = {
            providerType: provider.providerType?.toString() ?? '',
            providerName: typeof apiProvider.providerName === 'string' ? apiProvider.providerName : '',
            apiKey: '', // Don't show existing key for security
            apiEndpoint: apiProvider.baseUrl ?? '',
            organizationId: (provider as { organization?: string; organizationId?: string }).organization ?? 
                          (provider as { organization?: string; organizationId?: string }).organizationId ?? '',
            isEnabled: provider.isEnabled === true,
          };
          
          // Update initial values - form will reinitialize via key prop
          setInitialFormValues(newFormValues);
        } catch (error) {
          console.error('Error fetching provider:', error);
          notifications.show({
            title: 'Error',
            message: 'Failed to load provider',
            color: 'red',
          });
          router.push('/llm-providers');
        } finally {
          setIsLoadingProvider(false);
        }
      };
      
      void loadProvider();
    }
  }, [mode, providerId, router]);

  const handleSubmit = async (values: ProviderFormData) => {
    setIsSubmitting(true);
    try {
      if (mode === 'add') {
        let providerName = values.providerName.trim();
        if (!providerName) {
          const selectedProvider = availableProviders.find(p => p.value === values.providerType);
          providerName = selectedProvider?.label ?? 'Unknown Provider';
        }

        // First create the provider (without API key)
        const providerPayload = {
          providerType: parseInt(values.providerType, 10),
          providerName: providerName,
          baseUrl: values.apiEndpoint ?? undefined,
          isEnabled: values.isEnabled,
        };

        const createdProvider = await withAdminClient(client => 
          client.providers.create(providerPayload)
        );

        // Then add the API key as a key credential
        if (values.apiKey) {
          const keyPayload = {
            apiKey: values.apiKey,
            keyName: 'Primary Key',
            organization: values.organizationId ?? undefined,
            isPrimary: true,
            isEnabled: true,
          };

          await withAdminClient(client =>
            client.providers.createKey(createdProvider.id, keyPayload)
          );
        }

        notifications.show({
          title: 'Success',
          message: 'Provider and API key created successfully',
          color: 'green',
        });
      } else {
        // Edit mode - Note: API keys cannot be updated here, only through the keys management page
        const payload = {
          providerName: values.providerName ?? undefined,
          baseUrl: values.apiEndpoint ?? undefined,
          organization: values.organizationId ?? undefined,
          isEnabled: values.isEnabled,
        };

        await withAdminClient(client => 
          client.providers.update(providerId as number, payload)
        );

        notifications.show({
          title: 'Success',
          message: 'Provider updated successfully',
          color: 'green',
        });
      }
      
      router.push('/llm-providers');
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : `Failed to ${mode} provider`;
      
      if (mode === 'add' && errorMessage.includes('already exists')) {
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
      let result;
      
      if (mode === 'add') {
        result = await withAdminClient(client => 
          client.providers.testConfig({
            providerType: parseInt(form.values.providerType, 10),
            apiKey: form.values.apiKey,
            baseUrl: form.values.apiEndpoint ?? undefined,
            organizationId: form.values.organizationId ?? undefined,
          })
        );
      } else {
        result = await withAdminClient(client => 
          client.providers.testConnectionById(providerId as number)
        );
      }
      
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
  const providerTypeNum = parseInt(form.values.providerType, 10) as ProviderType;
  const config = PROVIDER_CONFIG_REQUIREMENTS[providerTypeNum];

  let providerDisplayName = 'Unknown Provider';
  if (mode === 'edit' && existingProvider) {
    try {
      const providerType = getProviderTypeFromDto(existingProvider);
      providerDisplayName = getProviderDisplayName(providerType);
    } catch {
      // Fallback to provider name if available
      const apiProvider = existingProvider;
      providerDisplayName = typeof apiProvider.providerName === 'string' ? apiProvider.providerName : 'Unknown Provider';
    }
  }

  const isLoading = isLoadingProviders || isLoadingProvider;

  return (
    <Container size="md" py="xl">
      <Stack gap="xl">
        <Group justify="space-between" mb="xl">
          <div>
            <Group mb="xs">
              <Button
                variant="subtle"
                leftSection={<IconArrowLeft size={16} />}
                onClick={() => router.push('/llm-providers')}
              >
                Back to Providers
              </Button>
            </Group>
            <Title order={1}>{mode === 'add' ? 'Add' : 'Edit'} LLM Provider</Title>
            <Text c="dimmed" size="lg" mt="xs">
              {mode === 'add' 
                ? 'Configure a new language model provider for your application'
                : 'Update the configuration for this language model provider'
              }
            </Text>
          </div>
          <ThemeIcon size={60} radius="md" variant="light" color="blue">
            {mode === 'add' ? <IconSparkles size={35} /> : <IconEdit size={35} />}
          </ThemeIcon>
        </Group>

        <Paper shadow="sm" p="xl" radius="md" withBorder>
          <form onSubmit={form.onSubmit(handleSubmit)}>
            <Stack gap="lg">
              <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <Card withBorder p="md">
                <Group gap="xs" mb="xs">
                  <ThemeIcon size="sm" variant="light" color="gray">
                    <IconServer size={14} />
                  </ThemeIcon>
                  <Text size="sm" fw={600} c="dimmed">PROVIDER CONFIGURATION</Text>
                </Group>
                
                <Stack gap="md">
                  {mode === 'add' ? (
                    <Select
                      label="Provider Type"
                      placeholder={isLoadingProviders ? "Loading available providers..." : "Select a provider"}
                      data={availableProviders}
                      required
                      disabled={isLoadingProviders || availableProviders.length === 0}
                      {...form.getInputProps('providerType')}
                      size="md"
                    />
                  ) : (
                    <Stack gap="xs">
                      <Group justify="space-between">
                        <Text size="sm" c="dimmed">Provider Type</Text>
                        <Badge size="lg">{providerDisplayName}</Badge>
                      </Group>
                    </Stack>
                  )}

                  <TextInput
                    label={`Provider Name ${mode === 'add' ? '(Optional)' : ''}`}
                    placeholder={mode === 'add' ? "Leave empty to use provider type name" : "Enter a friendly name for this provider"}
                    description="A friendly name to identify this provider (e.g., 'Production OpenAI', 'Dev Ollama')"
                    autoComplete="off"
                    aria-autocomplete="none"
                    list="autocompleteOff"
                    data-form-type="other"
                    {...form.getInputProps('providerName')}
                    size="md"
                  />
                </Stack>
              </Card>

              <Card withBorder p="md">
                <Group gap="xs" mb="xs">
                  <ThemeIcon size="sm" variant="light" color="gray">
                    <IconAlertCircle size={14} />
                  </ThemeIcon>
                  <Text size="sm" fw={600} c="dimmed">AUTHENTICATION</Text>
                </Group>
                
                <Stack gap="md">
                  {mode === 'edit' ? (
                    <Alert icon={<IconInfoCircle size={16} />} color="blue" variant="light">
                      <Text size="sm">
                        API keys cannot be updated here. To manage API keys, use the <Text component="span" fw={600}>Manage Keys</Text> button on the providers list page.
                      </Text>
                    </Alert>
                  ) : (
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
                      size="md"
                    />
                  )}

                  {config && mode === 'add' && (
                    <>
                      {config.requiresOrganizationId && (
                        <TextInput
                          label="Organization ID"
                          placeholder={providerTypeNum === ProviderType.OpenAI ? "Optional OpenAI organization ID" : "Enter organization ID"}
                          required={config.requiresOrganizationId}
                          autoComplete="off"
                          aria-autocomplete="none"
                          list="autocompleteOff"
                          data-form-type="other"
                          {...form.getInputProps('organizationId')}
                          size="md"
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
                          size="md"
                        />
                      )}
                    </>
                  )}

                  <Switch
                    label="Enable provider"
                    description="Whether this provider should be active and available for use"
                    {...form.getInputProps('isEnabled', { type: 'checkbox' })}
                    size="md"
                  />
                </Stack>
              </Card>

              {providerHelp}

              {mode === 'edit' && (
                <Alert icon={<IconInfoCircle size={16} />} color="blue" variant="light">
                  <Text size="sm">
                    Updating provider credentials will automatically refresh the available models list.
                  </Text>
                </Alert>
              )}

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
                  size="md"
                >
                  Test Connection
                </Button>
                <Group>
                  <Button 
                    variant="light" 
                    onClick={() => router.push('/llm-providers')}
                    size="md"
                  >
                    Cancel
                  </Button>
                  <Button 
                    type="submit" 
                    loading={isSubmitting}
                    size="md"
                  >
                    {mode === 'add' ? 'Create' : 'Save'} Provider
                  </Button>
                </Group>
              </Group>
            </Stack>
          </form>
        </Paper>
      </Stack>
    </Container>
  );
}