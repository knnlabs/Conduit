'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Card,
  Table,
  Badge,
  TextInput,
  Select,
  ActionIcon,
  Pagination,
  Modal,
  Code,
  ScrollArea,
  Tabs,
  Grid,
  Paper,
  Alert,
} from '@mantine/core';
import {
  IconSearch,
  IconFilter,
  IconDownload,
  IconRefresh,
  IconEye,
  IconClock,
  IconUser,
  IconApi,
  IconAlertCircle,
} from '@tabler/icons-react';
import { useState, useEffect, useCallback } from 'react';
import { usePaginatedData } from '@/hooks/usePaginatedData';
import { TableSkeleton } from '@/components/common/LoadingState';
import { TableEmptyState } from '@/components/common/EmptyState';
import { formatters } from '@/lib/utils/formatters';

interface RequestLog {
  id: string;
  timestamp: string;
  method: string;
  path: string;
  statusCode: number;
  duration: number;
  virtualKeyId?: string;
  virtualKeyName?: string;
  provider?: string;
  model?: string;
  tokenUsage?: {
    prompt: number;
    completion: number;
    total: number;
  };
  cost?: number;
  error?: string;
  userAgent?: string;
  ipAddress?: string;
  requestBody?: any;
  responseBody?: any;
}

interface RequestLogFilters {
  search?: string;
  virtualKeyId?: string;
  provider?: string;
  statusCode?: string;
  method?: string;
  dateFrom?: string;
  dateTo?: string;
}

export default function RequestLogsPage() {
  const [logs, setLogs] = useState<RequestLog[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [filters, setFilters] = useState<RequestLogFilters>({});
  const [selectedLog, setSelectedLog] = useState<RequestLog | null>(null);
  const [virtualKeys, setVirtualKeys] = useState<Array<{ value: string; label: string }>>([]);
  const [providers, setProviders] = useState<Array<{ value: string; label: string }>>([]);

  const {
    paginatedData,
    page,
    totalPages,
    pageSize,
    handlePageChange,
    totalItems,
  } = usePaginatedData(logs, { defaultPageSize: 20 });

  const fetchLogs = useCallback(async () => {
    try {
      setIsLoading(true);
      const queryParams = new URLSearchParams();
      
      Object.entries(filters).forEach(([key, value]) => {
        if (value) queryParams.append(key, value);
      });

      const response = await fetch(`/api/request-logs?${queryParams}`);
      if (!response.ok) {
        throw new Error('Failed to fetch logs');
      }
      const data = await response.json();
      setLogs(data.logs || []);
      
      // Extract unique virtual keys and providers for filters
      const virtualKeyNames = data.logs
        .map((log: RequestLog) => log.virtualKeyName)
        .filter((name: string | undefined) => Boolean(name)) as string[];
      const uniqueVirtualKeys = [...new Set(virtualKeyNames)];
      setVirtualKeys(uniqueVirtualKeys.map(name => ({ value: name, label: name })));
      
      const providerNames = data.logs
        .map((log: RequestLog) => log.provider)
        .filter((provider: string | undefined) => Boolean(provider)) as string[];
      const uniqueProviders = [...new Set(providerNames)];
      setProviders(uniqueProviders.map(name => ({ value: name, label: name })));
    } catch (error) {
      console.error('Error fetching logs:', error);
    } finally {
      setIsLoading(false);
    }
  }, [filters]);

  useEffect(() => {
    fetchLogs();
  }, [fetchLogs]);

  const getStatusColor = (statusCode: number): string => {
    if (statusCode >= 200 && statusCode < 300) return 'green';
    if (statusCode >= 300 && statusCode < 400) return 'blue';
    if (statusCode >= 400 && statusCode < 500) return 'orange';
    return 'red';
  };

  const getMethodColor = (method: string): string => {
    switch (method) {
      case 'GET': return 'blue';
      case 'POST': return 'green';
      case 'PUT': return 'orange';
      case 'PATCH': return 'yellow';
      case 'DELETE': return 'red';
      default: return 'gray';
    }
  };

  const handleExport = async () => {
    try {
      const queryParams = new URLSearchParams();
      Object.entries(filters).forEach(([key, value]) => {
        if (value) queryParams.append(key, value);
      });

      const response = await fetch(`/api/request-logs/export?${queryParams}`);
      if (!response.ok) throw new Error('Export failed');

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `request-logs-${new Date().toISOString()}.csv`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
    } catch (error) {
      console.error('Export failed:', error);
    }
  };

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>Request Logs</Title>
          <Text c="dimmed">View and analyze API request history</Text>
        </div>
        <Group>
          <Button
            variant="light"
            leftSection={<IconDownload size={16} />}
            onClick={handleExport}
          >
            Export
          </Button>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={fetchLogs}
            loading={isLoading}
          >
            Refresh
          </Button>
        </Group>
      </Group>

      {/* Filters */}
      <Card withBorder>
        <Card.Section withBorder inheritPadding py="xs">
          <Group justify="space-between">
            <Text fw={500}>Filters</Text>
            <Button
              size="xs"
              variant="subtle"
              onClick={() => {
                setFilters({});
                handlePageChange(1);
              }}
            >
              Clear All
            </Button>
          </Group>
        </Card.Section>
        <Card.Section inheritPadding py="md">
          <Grid>
            <Grid.Col span={{ base: 12, md: 4 }}>
              <TextInput
                placeholder="Search by path or ID..."
                leftSection={<IconSearch size={16} />}
                value={filters.search || ''}
                onChange={(e) => setFilters({ ...filters, search: e.currentTarget.value })}
              />
            </Grid.Col>
            <Grid.Col span={{ base: 12, md: 4 }}>
              <Select
                placeholder="Virtual Key"
                data={virtualKeys}
                value={filters.virtualKeyId || null}
                onChange={(value) => setFilters({ ...filters, virtualKeyId: value || undefined })}
                clearable
              />
            </Grid.Col>
            <Grid.Col span={{ base: 12, md: 4 }}>
              <Select
                placeholder="Provider"
                data={providers}
                value={filters.provider || null}
                onChange={(value) => setFilters({ ...filters, provider: value || undefined })}
                clearable
              />
            </Grid.Col>
            <Grid.Col span={{ base: 12, md: 3 }}>
              <Select
                placeholder="Status Code"
                data={[
                  { value: '2xx', label: '2xx Success' },
                  { value: '4xx', label: '4xx Client Error' },
                  { value: '5xx', label: '5xx Server Error' },
                ]}
                value={filters.statusCode || null}
                onChange={(value) => setFilters({ ...filters, statusCode: value || undefined })}
                clearable
              />
            </Grid.Col>
            <Grid.Col span={{ base: 12, md: 3 }}>
              <Select
                placeholder="Method"
                data={['GET', 'POST', 'PUT', 'PATCH', 'DELETE']}
                value={filters.method || null}
                onChange={(value) => setFilters({ ...filters, method: value || undefined })}
                clearable
              />
            </Grid.Col>
            <Grid.Col span={{ base: 12, md: 3 }}>
              <TextInput
                type="datetime-local"
                placeholder="From"
                value={filters.dateFrom || ''}
                onChange={(e) => setFilters({ ...filters, dateFrom: e.currentTarget.value })}
              />
            </Grid.Col>
            <Grid.Col span={{ base: 12, md: 3 }}>
              <TextInput
                type="datetime-local"
                placeholder="To"
                value={filters.dateTo || ''}
                onChange={(e) => setFilters({ ...filters, dateTo: e.currentTarget.value })}
              />
            </Grid.Col>
          </Grid>
        </Card.Section>
      </Card>

      {/* Logs Table */}
      <Card withBorder>
        <ScrollArea>
          {isLoading ? (
            <TableSkeleton rows={10} columns={8} />
          ) : (
            <Table striped highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Timestamp</Table.Th>
                  <Table.Th>Method</Table.Th>
                  <Table.Th>Path</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th>Duration</Table.Th>
                  <Table.Th>Virtual Key</Table.Th>
                  <Table.Th>Provider</Table.Th>
                  <Table.Th>Actions</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {paginatedData.length === 0 ? (
                  <TableEmptyState 
                    colSpan={8}
                    title="No logs found"
                    description="No request logs match your filters"
                  />
                ) : (
                  paginatedData.map((log) => (
                    <Table.Tr key={log.id}>
                      <Table.Td>
                        <Text size="xs">{formatters.date(log.timestamp)}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Badge color={getMethodColor(log.method)} variant="light">
                          {log.method}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm" style={{ maxWidth: '200px', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                          {log.path}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Badge color={getStatusColor(log.statusCode)} variant="light">
                          {log.statusCode}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{log.duration}ms</Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{log.virtualKeyName || '-'}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{log.provider || '-'}</Text>
                      </Table.Td>
                      <Table.Td>
                        <ActionIcon
                          variant="subtle"
                          onClick={() => setSelectedLog(log)}
                        >
                          <IconEye size={16} />
                        </ActionIcon>
                      </Table.Td>
                    </Table.Tr>
                  ))
                )}
              </Table.Tbody>
            </Table>
          )}
        </ScrollArea>
        
        {!isLoading && totalPages > 1 && (
          <Card.Section withBorder inheritPadding py="xs">
            <Group justify="space-between">
              <Text size="sm" c="dimmed">
                Showing {((page - 1) * pageSize) + 1}-{Math.min(page * pageSize, totalItems)} of {totalItems} logs
              </Text>
              <Pagination
                value={page}
                onChange={handlePageChange}
                total={totalPages}
                size="sm"
              />
            </Group>
          </Card.Section>
        )}
      </Card>

      {/* Log Details Modal */}
      <Modal
        opened={!!selectedLog}
        onClose={() => setSelectedLog(null)}
        title={selectedLog ? `Request Log Details - ${selectedLog.id}` : ''}
        size="xl"
      >
        {selectedLog && (
          <Tabs defaultValue="overview">
            <Tabs.List>
              <Tabs.Tab value="overview" leftSection={<IconAlertCircle size={16} />}>
                Overview
              </Tabs.Tab>
              <Tabs.Tab value="request" leftSection={<IconApi size={16} />}>
                Request
              </Tabs.Tab>
              <Tabs.Tab value="response" leftSection={<IconApi size={16} />}>
                Response
              </Tabs.Tab>
            </Tabs.List>

            <Tabs.Panel value="overview" pt="md">
              <Stack gap="md">
                <Paper withBorder p="md">
                  <Grid>
                    <Grid.Col span={6}>
                      <Text size="sm" c="dimmed">Timestamp</Text>
                      <Text fw={500}>{formatters.date(selectedLog.timestamp)}</Text>
                    </Grid.Col>
                    <Grid.Col span={6}>
                      <Text size="sm" c="dimmed">Duration</Text>
                      <Text fw={500}>{selectedLog.duration}ms</Text>
                    </Grid.Col>
                    <Grid.Col span={6}>
                      <Text size="sm" c="dimmed">Method</Text>
                      <Badge color={getMethodColor(selectedLog.method)} variant="light">
                        {selectedLog.method}
                      </Badge>
                    </Grid.Col>
                    <Grid.Col span={6}>
                      <Text size="sm" c="dimmed">Status Code</Text>
                      <Badge color={getStatusColor(selectedLog.statusCode)} variant="light">
                        {selectedLog.statusCode}
                      </Badge>
                    </Grid.Col>
                    <Grid.Col span={12}>
                      <Text size="sm" c="dimmed">Path</Text>
                      <Code block>{selectedLog.path}</Code>
                    </Grid.Col>
                    {selectedLog.virtualKeyName && (
                      <Grid.Col span={6}>
                        <Text size="sm" c="dimmed">Virtual Key</Text>
                        <Text fw={500}>{selectedLog.virtualKeyName}</Text>
                      </Grid.Col>
                    )}
                    {selectedLog.provider && (
                      <Grid.Col span={6}>
                        <Text size="sm" c="dimmed">Provider</Text>
                        <Text fw={500}>{selectedLog.provider}</Text>
                      </Grid.Col>
                    )}
                    {selectedLog.model && (
                      <Grid.Col span={6}>
                        <Text size="sm" c="dimmed">Model</Text>
                        <Text fw={500}>{selectedLog.model}</Text>
                      </Grid.Col>
                    )}
                    {selectedLog.tokenUsage && (
                      <>
                        <Grid.Col span={4}>
                          <Text size="sm" c="dimmed">Prompt Tokens</Text>
                          <Text fw={500}>{selectedLog.tokenUsage.prompt}</Text>
                        </Grid.Col>
                        <Grid.Col span={4}>
                          <Text size="sm" c="dimmed">Completion Tokens</Text>
                          <Text fw={500}>{selectedLog.tokenUsage.completion}</Text>
                        </Grid.Col>
                        <Grid.Col span={4}>
                          <Text size="sm" c="dimmed">Total Tokens</Text>
                          <Text fw={500}>{selectedLog.tokenUsage.total}</Text>
                        </Grid.Col>
                      </>
                    )}
                    {selectedLog.cost !== undefined && (
                      <Grid.Col span={6}>
                        <Text size="sm" c="dimmed">Cost</Text>
                        <Text fw={500}>${selectedLog.cost.toFixed(4)}</Text>
                      </Grid.Col>
                    )}
                    {selectedLog.ipAddress && (
                      <Grid.Col span={6}>
                        <Text size="sm" c="dimmed">IP Address</Text>
                        <Text fw={500}>{selectedLog.ipAddress}</Text>
                      </Grid.Col>
                    )}
                    {selectedLog.userAgent && (
                      <Grid.Col span={12}>
                        <Text size="sm" c="dimmed">User Agent</Text>
                        <Code block>{selectedLog.userAgent}</Code>
                      </Grid.Col>
                    )}
                    {selectedLog.error && (
                      <Grid.Col span={12}>
                        <Text size="sm" c="dimmed">Error</Text>
                        <Code block color="red">{selectedLog.error}</Code>
                      </Grid.Col>
                    )}
                  </Grid>
                </Paper>
              </Stack>
            </Tabs.Panel>

            <Tabs.Panel value="request" pt="md">
              <Stack gap="md">
                {selectedLog.requestBody ? (
                  <Code block>
                    {JSON.stringify(selectedLog.requestBody, null, 2)}
                  </Code>
                ) : (
                  <Text c="dimmed" ta="center">No request body</Text>
                )}
              </Stack>
            </Tabs.Panel>

            <Tabs.Panel value="response" pt="md">
              <Stack gap="md">
                {selectedLog.responseBody ? (
                  <Code block>
                    {JSON.stringify(selectedLog.responseBody, null, 2)}
                  </Code>
                ) : (
                  <Text c="dimmed" ta="center">No response body</Text>
                )}
              </Stack>
            </Tabs.Panel>
          </Tabs>
        )}
      </Modal>
    </Stack>
  );
}