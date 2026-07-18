'use client';

import { useState, useEffect, useCallback } from 'react';
import {
  Container,
  Title,
  Text,
  Button,
  Group,
  Stack,
  Table,
  Badge,
  Select,
  Modal,
  Card,
  LoadingOverlay,
  ActionIcon,
  Checkbox,
  Grid,
  Code
} from '@mantine/core';
import {
  IconRefresh,
  IconEye,
  IconTrash
} from '@tabler/icons-react';
import { notify } from '@/lib/notifications';
import { modals } from '@mantine/modals';
import { useAdminClient } from '@/lib/client/adminClient';
import {
  FunctionExecutionDto,
  FunctionConfigurationDto,
  ExecutionState,
  getExecutionStateName,
} from '../types';

// Helper function for execution state colors
function getExecutionStateBadgeColor(state: ExecutionState): string {
  switch (state) {
    case ExecutionState.Pending:
      return 'yellow';
    case ExecutionState.Running:
      return 'blue';
    case ExecutionState.Completed:
      return 'green';
    case ExecutionState.Failed:
      return 'red';
    case ExecutionState.Cancelled:
      return 'gray';
    case ExecutionState.TimedOut:
      return 'orange';
    default:
      return 'gray';
  }
}

export default function FunctionExecutionsPage() {
  const { executeWithAdmin } = useAdminClient();
  const [executions, setExecutions] = useState<FunctionExecutionDto[]>([]);
  const [configurations, setConfigurations] = useState<FunctionConfigurationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedExecution, setSelectedExecution] = useState<FunctionExecutionDto | null>(null);

  // Filters
  const [filterState, setFilterState] = useState<string>('all');
  const [filterConfigId, setFilterConfigId] = useState<string>('all');
  const [autoRefresh, setAutoRefresh] = useState(false);

  const loadConfigurations = useCallback(async () => {
    try {
      const response = await executeWithAdmin(client =>
        client.functionConfigurations.list()
      );
      setConfigurations(response);
    } catch (err) {
      console.warn('Error loading configurations:', err);
    }
  }, [executeWithAdmin]);

  const loadExecutions = useCallback(async () => {
    try {
      setLoading(true);
      let response;
      if (filterState !== 'all') {
        response = await executeWithAdmin(client =>
          client.functionExecutions.getByState(Number(filterState) as ExecutionState)
        );
      } else if (filterConfigId !== 'all') {
        response = await executeWithAdmin(client =>
          client.functionExecutions.getByConfiguration(Number(filterConfigId))
        );
      } else {
        // For demo purposes, get by state Completed to avoid empty list
        response = await executeWithAdmin(client =>
          client.functionExecutions.getByState(ExecutionState.Completed)
        );
      }

      setExecutions(response);
    } catch (err) {
      console.warn('Error loading executions:', err);
      notify.error(err, 'Failed to load executions');
    } finally {
      setLoading(false);
    }
  }, [filterState, filterConfigId, executeWithAdmin]);

  const loadData = useCallback(async () => {
    await Promise.all([loadConfigurations(), loadExecutions()]);
  }, [loadConfigurations, loadExecutions]);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  useEffect(() => {
    if (!autoRefresh) return;

    const interval = setInterval(() => {
      void loadExecutions();
    }, 5000); // Refresh every 5 seconds

    return () => clearInterval(interval);
  }, [autoRefresh, filterState, filterConfigId, loadExecutions]);

  const handleCleanup = () => {
    modals.openConfirmModal({
      title: 'Cleanup Old Executions',
      children: (
        <Stack gap="md">
          <Text size="sm">
            Enter the number of days. Executions older than this will be permanently deleted.
          </Text>
          <Text size="sm" c="dimmed">
            Default: 30 days
          </Text>
        </Stack>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => {
        void (async () => {
          try {
            const result = await executeWithAdmin(client =>
              client.functionExecutions.cleanup(30)
            );
            notify.success(`Deleted ${(result as { deletedCount?: number }).deletedCount ?? 0} executions`);
            await loadExecutions();
          } catch (err) {
            console.warn('Error cleaning up executions:', err);
            notify.error(err, 'Failed to cleanup executions');
          }
        })();
      },
    });
  };

  const formatDuration = (ms: number | null | undefined): string => {
    if (!ms) return '-';
    if (ms < 1000) return `${Math.round(ms)}ms`;
    return `${(ms / 1000).toFixed(2)}s`;
  };

  const formatCost = (cost: number | null | undefined): string => {
    if (cost === null || cost === undefined) return '-';
    return `$${cost.toFixed(6)}`;
  };

  const getConfigurationName = (configId: number): string => {
    const config = configurations.find((c) => c.id === configId);
    return config?.configurationName ?? `Config #${configId}`;
  };

  return (
    <Container size="xl">
      <Stack gap="md">
        <Group justify="space-between" align="flex-end">
          <div>
            <Title order={2}>Function Executions</Title>
            <Text c="dimmed" size="sm" mt={4}>
              Monitor and track function execution history
            </Text>
          </div>
          <Group gap="xs">
            <Checkbox
              label="Auto Refresh"
              checked={autoRefresh}
              onChange={(e) => setAutoRefresh(e.currentTarget.checked)}
            />
            <Button
              leftSection={<IconRefresh size={16} />}
              variant="subtle"
              onClick={() => void loadExecutions()}
            >
              Refresh
            </Button>
            <Button
              leftSection={<IconTrash size={16} />}
              color="red"
              onClick={() => void handleCleanup()}
            >
              Cleanup Old
            </Button>
          </Group>
        </Group>

        <Card withBorder>
          <Card.Section p="md" withBorder>
            <Group gap="md">
              <Select
                label="State"
                placeholder="All States"
                value={filterState}
                onChange={(value) => {
                  setFilterState(value ?? 'all');
                  setFilterConfigId('all');
                  void loadExecutions();
                }}
                data={[
                  { value: 'all', label: 'All States' },
                  { value: ExecutionState.Pending.toString(), label: 'Pending' },
                  { value: ExecutionState.Running.toString(), label: 'Running' },
                  { value: ExecutionState.Completed.toString(), label: 'Completed' },
                  { value: ExecutionState.Failed.toString(), label: 'Failed' },
                  { value: ExecutionState.Cancelled.toString(), label: 'Cancelled' },
                ]}
                style={{ flex: 1 }}
              />
              <Select
                label="Configuration"
                placeholder="All Configurations"
                value={filterConfigId}
                onChange={(value) => {
                  setFilterConfigId(value ?? 'all');
                  setFilterState('all');
                  void loadExecutions();
                }}
                data={[
                  { value: 'all', label: 'All Configurations' },
                  ...(configurations ?? []).map((config) => ({
                    value: config.id.toString(),
                    label: config.configurationName,
                  })),
                ]}
                style={{ flex: 1 }}
              />
            </Group>
          </Card.Section>
        </Card>

        <Card withBorder>
          <LoadingOverlay visible={loading} />
          <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>ID</Table.Th>
                <Table.Th>Configuration</Table.Th>
                <Table.Th>Virtual Key</Table.Th>
                <Table.Th>State</Table.Th>
                <Table.Th>Duration</Table.Th>
                <Table.Th>Estimated</Table.Th>
                <Table.Th>Actual</Table.Th>
                <Table.Th>Started</Table.Th>
                <Table.Th>Actions</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {(executions ?? []).length === 0 ? (
                <Table.Tr>
                  <Table.Td colSpan={9}>
                    <Text ta="center" c="dimmed" py="xl">
                      {loading ? 'Loading executions...' : 'No executions found'}
                    </Text>
                  </Table.Td>
                </Table.Tr>
              ) : (
                (executions ?? []).map((execution) => (
                  <Table.Tr key={execution.id}>
                    <Table.Td>
                      <Code>{execution.id.substring(0, 8)}...</Code>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm">{getConfigurationName(execution.functionConfigurationId)}</Text>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm">{execution.virtualKeyId}</Text>
                    </Table.Td>
                    <Table.Td>
                      <Badge color={getExecutionStateBadgeColor(execution.state)} variant="light">
                        {getExecutionStateName(execution.state)}
                      </Badge>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm">{formatDuration(execution.duration ?? undefined)}</Text>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm">{formatCost(execution.estimatedCost ?? undefined)}</Text>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm">{formatCost(execution.actualCost ?? undefined)}</Text>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm" c="dimmed">
                        {execution.startedAt ? new Date(execution.startedAt).toLocaleString() : '-'}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <ActionIcon
                        variant="subtle"
                        color="blue"
                        onClick={() => setSelectedExecution(execution)}
                      >
                        <IconEye size={16} />
                      </ActionIcon>
                    </Table.Td>
                  </Table.Tr>
                ))
              )}
            </Table.Tbody>
          </Table>
        </Card>
      </Stack>

      <Modal
        opened={!!selectedExecution}
        onClose={() => setSelectedExecution(null)}
        title="Execution Details"
        size="xl"
      >
        {selectedExecution && (
          <Stack gap="md">
            <Grid gutter="md">
              <Grid.Col span={6}>
                <Text size="sm" fw={500} c="dimmed">Execution ID</Text>
                <Code>{selectedExecution.id}</Code>
              </Grid.Col>
              <Grid.Col span={6}>
                <Text size="sm" fw={500} c="dimmed">State</Text>
                <Badge color={getExecutionStateBadgeColor(selectedExecution.state)} variant="light" mt={4}>
                  {getExecutionStateName(selectedExecution.state)}
                </Badge>
              </Grid.Col>
              <Grid.Col span={6}>
                <Text size="sm" fw={500} c="dimmed">Configuration</Text>
                <Text size="sm">{getConfigurationName(selectedExecution.functionConfigurationId)}</Text>
              </Grid.Col>
              <Grid.Col span={6}>
                <Text size="sm" fw={500} c="dimmed">Virtual Key</Text>
                <Text size="sm">{selectedExecution.virtualKeyId}</Text>
              </Grid.Col>
              <Grid.Col span={6}>
                <Text size="sm" fw={500} c="dimmed">Duration</Text>
                <Text size="sm">{formatDuration(selectedExecution.duration ?? undefined)}</Text>
              </Grid.Col>
              <Grid.Col span={6}>
                <Text size="sm" fw={500} c="dimmed">Costs</Text>
                <Text size="sm">
                  Estimated: {formatCost(selectedExecution.estimatedCost ?? undefined)}<br />
                  Actual: {formatCost(selectedExecution.actualCost ?? undefined)}
                </Text>
              </Grid.Col>
              <Grid.Col span={6}>
                <Text size="sm" fw={500} c="dimmed">Started At</Text>
                <Text size="sm">
                  {selectedExecution.startedAt ? new Date(selectedExecution.startedAt).toLocaleString() : '-'}
                </Text>
              </Grid.Col>
              <Grid.Col span={6}>
                <Text size="sm" fw={500} c="dimmed">Completed At</Text>
                <Text size="sm">
                  {selectedExecution.completedAt ? new Date(selectedExecution.completedAt).toLocaleString() : '-'}
                </Text>
              </Grid.Col>
            </Grid>

            {selectedExecution.errorMessage && (
              <div>
                <Text size="sm" fw={500} c="dimmed" mb="xs">Error Message</Text>
                <Card withBorder p="md" bg="red.0">
                  <Text size="sm" c="red.7">{selectedExecution.errorMessage}</Text>
                </Card>
              </div>
            )}

            {selectedExecution.requestJson && (
              <div>
                <Text size="sm" fw={500} c="dimmed" mb="xs">Request</Text>
                <Card withBorder p="md" bg="gray.0">
                  <Code block style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-all' }}>
                    {JSON.stringify(JSON.parse(selectedExecution.requestJson), null, 2)}
                  </Code>
                </Card>
              </div>
            )}

            {selectedExecution.responseJson && (
              <div>
                <Text size="sm" fw={500} c="dimmed" mb="xs">Response</Text>
                <Card withBorder p="md" bg="gray.0">
                  <Code block style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-all' }}>
                    {JSON.stringify(JSON.parse(selectedExecution.responseJson), null, 2)}
                  </Code>
                </Card>
              </div>
            )}

            {selectedExecution.costCalculationDetails && (
              <div>
                <Text size="sm" fw={500} c="dimmed" mb="xs">Cost Calculation Details</Text>
                <Card withBorder p="md" bg="gray.0">
                  <Code block style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-all' }}>
                    {JSON.stringify(JSON.parse(selectedExecution.costCalculationDetails), null, 2)}
                  </Code>
                </Card>
              </div>
            )}

            <Group justify="flex-end" mt="md">
              <Button onClick={() => setSelectedExecution(null)}>
                Close
              </Button>
            </Group>
          </Stack>
        )}
      </Modal>
    </Container>
  );
}
