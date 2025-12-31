'use client';

import React, { useState, useEffect } from 'react';
import {
  Modal,
  Button,
  Group,
  Stack,
  Alert,
  Text,
  Badge,
  Card,
  Tabs,
  JsonInput,
  TextInput,
  Grid,
  Code,
  Loader,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import {
  IconAlertCircle,
  IconCheck,
  IconX,
  IconInfoCircle,
} from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import { getBrowserCoreClient } from '@/lib/client/browserCoreClient';
import { FunctionConfigurationDto } from '@/app/functions/types';

interface TestFunctionModalProps {
  opened: boolean;
  onClose: () => void;
  configuration: FunctionConfigurationDto;
}

interface FunctionExecutionResponse {
  executionId: string;
  functionConfigurationId: number;
  state: string;
  result?: Record<string, unknown>;
  errorMessage?: string;
  estimatedCost?: number;
  actualCost?: number;
  startedAt?: string;
  completedAt?: string;
  duration?: number;
}

const validateJson = (value: string) => {
  if (!value || value.trim() === '') {
    return null; // Empty is okay for optional fields
  }

  try {
    JSON.parse(value);
    return null;
  } catch {
    return 'Must be valid JSON';
  }
};

const validateParameters = (value: string) => {
  if (!value || value.trim() === '') {
    return 'Parameters are required';
  }

  try {
    const parsed: unknown = JSON.parse(value);
    if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
      return 'Parameters must be a JSON object';
    }
    return null;
  } catch {
    return 'Must be valid JSON';
  }
};

function getStateIcon(state: string): React.ReactNode {
  if (state === 'Completed') return <IconCheck size={16} />;
  if (state === 'Failed') return <IconX size={16} />;
  return null;
}

export function TestFunctionModal({ opened, onClose, configuration }: TestFunctionModalProps) {
  const [isLoading, setIsLoading] = useState(false);
  const [testResult, setTestResult] = useState<FunctionExecutionResponse | null>(null);
  const [activeTab, setActiveTab] = useState<string>('parameters');
  const [, setLoadingSchema] = useState(false);

  const form = useForm({
    initialValues: {
      parameters: '{}',
      metadata: '{}',
      idempotencyKey: '',
    },
    validate: {
      parameters: validateParameters,
      metadata: validateJson,
    },
  });

  // Fetch parameter schema when modal opens
  useEffect(() => {
    if (opened && configuration) {
      setLoadingSchema(true);
      getBrowserCoreClient()
        .then((client) => client.discovery.getFunctionParameters(configuration.id))
        .then((schema) => {
          // Use example request if available, otherwise use empty object
          if (schema.exampleRequest) {
            form.setFieldValue('parameters', JSON.stringify(schema.exampleRequest, null, 2));
          } else if (schema.parameterSchema && typeof schema.parameterSchema === 'object') {
            // Try to build a default request from the schema
            const schemaObj = schema.parameterSchema as { example?: Record<string, unknown> };
            if (schemaObj.example) {
              form.setFieldValue('parameters', JSON.stringify(schemaObj.example, null, 2));
            }
          }
        })
        .catch((error) => {
          // Silently fail - just keep the default empty object
          console.warn('Failed to fetch parameter schema:', error);
        })
        .finally(() => {
          setLoadingSchema(false);
        });
    }
  }, [opened, configuration, form]);

  const handleTest = async (values: typeof form.values) => {
    setIsLoading(true);
    setTestResult(null);

    try {
      // Get the Gateway API client with ephemeral key
      const coreClient = await getBrowserCoreClient();

      // Parse parameters and metadata
      const parameters = JSON.parse(values.parameters) as Record<string, unknown>;
      const metadata = values.metadata ? JSON.parse(values.metadata) as Record<string, unknown> : undefined;

      // Execute the function
      const response = await coreClient.functions.execute({
        functionConfigurationId: configuration.id,
        parameters,
        metadata,
        idempotencyKey: values.idempotencyKey || undefined,
      });

      setTestResult(response as FunctionExecutionResponse);

      // Auto-switch to result tab
      setActiveTab('result');

      // Show success notification
      notifications.show({
        title: 'Function executed',
        message: `Execution completed in ${formatDuration(response.duration ?? 0)}`,
        color: response.state === 'Completed' ? 'green' : 'yellow',
      });
    } catch (error) {
      console.warn('Error executing function:', error);

      // Show error in result
      const errorMessage = error instanceof Error ? error.message : 'Failed to execute function';
      setTestResult({
        executionId: '',
        functionConfigurationId: configuration.id,
        state: 'Failed',
        errorMessage,
      });

      // Auto-switch to result tab
      setActiveTab('result');

      notifications.show({
        title: 'Execution failed',
        message: errorMessage,
        color: 'red',
      });
    } finally {
      setIsLoading(false);
    }
  };

  const handleTestAgain = () => {
    setTestResult(null);
    setActiveTab('parameters');
  };

  const handleClose = () => {
    form.reset();
    setTestResult(null);
    setActiveTab('parameters');
    setLoadingSchema(false);
    onClose();
  };

  const formatDuration = (ms: number): string => {
    if (ms < 1000) {
      return `${ms.toFixed(0)}ms`;
    }
    return `${(ms / 1000).toFixed(2)}s`;
  };

  const formatCost = (cost: number): string => {
    return `$${cost.toFixed(6)}`;
  };

  const getStateColor = (state: string): string => {
    switch (state) {
      case 'Completed':
        return 'green';
      case 'Failed':
        return 'red';
      case 'Running':
        return 'blue';
      case 'Pending':
        return 'yellow';
      default:
        return 'gray';
    }
  };

  return (
    <Modal
      opened={opened}
      onClose={handleClose}
      title={`Test Function: ${configuration.configurationName}`}
      size="xl"
    >
      <form onSubmit={form.onSubmit(handleTest)}>
        <Tabs value={activeTab} onChange={(value) => setActiveTab(value ?? 'parameters')}>
          <Tabs.List>
            <Tabs.Tab value="parameters">Parameters</Tabs.Tab>
            <Tabs.Tab value="advanced">Advanced</Tabs.Tab>
            <Tabs.Tab value="result" disabled={!testResult}>
              Result
            </Tabs.Tab>
          </Tabs.List>

          <Tabs.Panel value="parameters" pt="md">
            <Stack gap="md">
              <Alert icon={<IconInfoCircle size={16} />} color="blue" variant="light">
                <Text size="sm">
                  Provide the parameters required by this function. The format depends on the
                  provider type ({configuration.providerType}).
                </Text>
              </Alert>

              <JsonInput
                label="Function Parameters"
                placeholder='{\n  "query": "example search",\n  "numResults": 5\n}'
                required
                minRows={8}
                maxRows={15}
                formatOnBlur
                autosize
                {...form.getInputProps('parameters')}
              />
            </Stack>
          </Tabs.Panel>

          <Tabs.Panel value="advanced" pt="md">
            <Stack gap="md">
              <Text size="sm" c="dimmed">
                Optional settings for advanced use cases
              </Text>

              <JsonInput
                label="Metadata"
                description="Optional metadata to associate with the execution"
                placeholder='{}'
                minRows={4}
                maxRows={8}
                formatOnBlur
                autosize
                {...form.getInputProps('metadata')}
              />

              <TextInput
                label="Idempotency Key"
                description="Optional key to prevent duplicate executions"
                placeholder="unique-key-123"
                {...form.getInputProps('idempotencyKey')}
              />
            </Stack>
          </Tabs.Panel>

          <Tabs.Panel value="result" pt="md">
            {isLoading ? (
              <Stack align="center" py="xl">
                <Loader size="lg" />
                <Text c="dimmed">Executing function...</Text>
              </Stack>
            ) : testResult ? (
              <Stack gap="md">
                <Group justify="space-between">
                  <Badge
                    color={getStateColor(testResult.state)}
                    variant="filled"
                    size="lg"
                    leftSection={getStateIcon(testResult.state)}
                  >
                    {testResult.state}
                  </Badge>
                  {testResult.duration !== null && testResult.duration !== undefined && (
                    <Text size="sm" c="dimmed">
                      Duration: {formatDuration(testResult.duration)}
                    </Text>
                  )}
                </Group>

                {testResult.errorMessage && (
                  <Alert icon={<IconAlertCircle size={16} />} color="red" variant="filled">
                    <Text size="sm" fw={500}>
                      Error
                    </Text>
                    <Text size="sm">{testResult.errorMessage}</Text>
                  </Alert>
                )}

                {testResult.result && (
                  <div>
                    <Text size="sm" fw={500} mb="xs">
                      Response Data:
                    </Text>
                    <Code block style={{ maxHeight: '400px', overflow: 'auto' }}>
                      {JSON.stringify(testResult.result, null, 2)}
                    </Code>
                  </div>
                )}

                <Card withBorder p="md">
                  <Text size="sm" fw={500} mb="sm">
                    Execution Details
                  </Text>
                  <Grid>
                    {testResult.executionId && (
                      <>
                        <Grid.Col span={4}>
                          <Text size="xs" c="dimmed">
                            Execution ID:
                          </Text>
                        </Grid.Col>
                        <Grid.Col span={8}>
                          <Text size="xs" style={{ fontFamily: 'monospace' }}>
                            {testResult.executionId}
                          </Text>
                        </Grid.Col>
                      </>
                    )}
                    {testResult.startedAt && (
                      <>
                        <Grid.Col span={4}>
                          <Text size="xs" c="dimmed">
                            Started:
                          </Text>
                        </Grid.Col>
                        <Grid.Col span={8}>
                          <Text size="xs">
                            {new Date(testResult.startedAt).toLocaleString()}
                          </Text>
                        </Grid.Col>
                      </>
                    )}
                    {testResult.completedAt && (
                      <>
                        <Grid.Col span={4}>
                          <Text size="xs" c="dimmed">
                            Completed:
                          </Text>
                        </Grid.Col>
                        <Grid.Col span={8}>
                          <Text size="xs">
                            {new Date(testResult.completedAt).toLocaleString()}
                          </Text>
                        </Grid.Col>
                      </>
                    )}
                    {testResult.estimatedCost !== null && testResult.estimatedCost !== undefined && (
                      <>
                        <Grid.Col span={4}>
                          <Text size="xs" c="dimmed">
                            Estimated Cost:
                          </Text>
                        </Grid.Col>
                        <Grid.Col span={8}>
                          <Text size="xs">{formatCost(testResult.estimatedCost)}</Text>
                        </Grid.Col>
                      </>
                    )}
                    {testResult.actualCost !== null && testResult.actualCost !== undefined && (
                      <>
                        <Grid.Col span={4}>
                          <Text size="xs" c="dimmed">
                            Actual Cost:
                          </Text>
                        </Grid.Col>
                        <Grid.Col span={8}>
                          <Text size="xs">{formatCost(testResult.actualCost)}</Text>
                        </Grid.Col>
                      </>
                    )}
                  </Grid>
                </Card>
              </Stack>
            ) : null}
          </Tabs.Panel>
        </Tabs>

        <Group justify="flex-end" mt="xl">
          {testResult ? (
            <>
              <Button variant="default" onClick={handleClose}>
                Close
              </Button>
              <Button onClick={handleTestAgain}>Test Again</Button>
            </>
          ) : (
            <>
              <Button variant="default" onClick={handleClose}>
                Cancel
              </Button>
              <Button type="submit" loading={isLoading}>
                Test Function
              </Button>
            </>
          )}
        </Group>
      </form>
    </Modal>
  );
}
