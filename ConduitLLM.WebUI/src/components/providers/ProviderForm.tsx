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
import { IconAlertCircle, IconInfoCircle, IconCircleCheck, IconArrowLeft, IconServer, IconSparkles, IconEdit } from '@tabler/icons-react';
import { 
  ProviderType, 
  PROVIDER_CONFIG_REQUIREMENTS,
} from '@knn_labs/conduit-admin-client';
import { useProviderFormLogic } from './ProviderFormLogic';
import { useProviderFormHandlers } from './ProviderFormHandlers';

interface ProviderFormProps {
  mode: 'add' | 'edit';
  providerId?: number;
}

export function ProviderForm({ mode, providerId }: ProviderFormProps) {
  // Use split logic hooks
  const logic = useProviderFormLogic(mode, providerId);
  const handlers = useProviderFormHandlers({ mode, providerId, logic });

  const {
    form,
    isSubmitting,
    isTesting,
    testResult,
    availableProviders,
    isLoadingProviders,
    providerDisplayName,
    isLoading,
  } = logic;

  const {
    handleSubmit,
    handleTestConnection,
    handleBack,
    handleCancel,
  } = handlers;

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

  return (
    <Container size="md" py="xl">
      <Stack gap="xl">
        <Group justify="space-between" mb="xl">
          <div>
            <Group mb="xs">
              <Button
                variant="subtle"
                leftSection={<IconArrowLeft size={16} />}
                onClick={handleBack}
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
                    onClick={handleCancel}
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