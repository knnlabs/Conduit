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
<<<<<<< HEAD
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
=======
import { useState, useEffect } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { useRequestLogs, useExportRequestLogs, type RequestLog } from '@/hooks/api/useRequestLogsApi';
import { BaseTable, type ColumnDef } from '@/components/common/BaseTable';
import { StatusIndicator } from '@/components/common/StatusIndicator';
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6

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

<<<<<<< HEAD
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
=======
  const handleExport = () => {
    // Convert filters for export
    const exportFilters = {
      method: filters.method || undefined,
      status: filters.statusCode || undefined,
      virtualKeyId: filters.virtualKey || undefined,
      startDate: filters.dateFrom ? filters.dateFrom.toISOString() : undefined,
      endDate: filters.dateTo ? filters.dateTo.toISOString() : undefined,
    };

    exportMutation.mutate({
      format: 'csv',
      filters: exportFilters,
    });
  };

  // Convert response time to status for color coding
  const getResponseTimeStatus = (responseTime: number): 'healthy' | 'warning' | 'unhealthy' => {
    if (responseTime < 500) return 'healthy';
    if (responseTime < 1000) return 'warning';
    return 'unhealthy';
  };
  
  // Convert request log status to SystemStatusType
  const mapLogStatusToSystemStatus = (status: string): 'completed' | 'failed' | 'warning' => {
    switch (status) {
      case 'success':
        return 'completed';
      case 'error':
        return 'failed';
      case 'timeout':
        return 'warning';
      default:
        return 'failed';
    }
  };

  const formatFileSize = (bytes: number) => {
    const sizes = ['B', 'KB', 'MB', 'GB'];
    if (bytes === 0) return '0 B';
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    return `${(bytes / Math.pow(1024, i)).toFixed(1)} ${sizes[i]}`;
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6
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
<<<<<<< HEAD
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
=======
      <BaseTable
        data={logs}
        isLoading={isLoading}
        error={error}
        searchable
        searchPlaceholder="Search logs by model, IP, virtual key..."
        onRefresh={() => refetch()}
        emptyMessage="No request logs found. Try adjusting your filters."
        minWidth={1200}
        pagination={{
          page: currentPage,
          pageSize: 20,
          total: totalPages * 20, // Approximate total from API
          onPageChange: setCurrentPage,
          onPageSizeChange: (size) => {
            // Could extend API to support dynamic page sizes
            console.log('Page size change requested:', size);
          },
          pageSizeOptions: ['10', '20', '50']
        }}
        columns={[
          {
            key: 'timestamp',
            label: 'Timestamp',
            sortable: true,
            sortType: 'date',
            width: '140px',
            render: (log) => (
              <Text size="xs">
                {new Date(log.timestamp).toLocaleString()}
              </Text>
            )
          },
          {
            key: 'method',
            label: 'Type',
            width: '80px',
            render: () => (
              <Badge variant="light" size="sm">
                POST
              </Badge>
            )
          },
          {
            key: 'model',
            label: 'Model',
            sortable: true,
            filterable: true,
            width: '160px',
            render: (log) => (
              <Text size="sm" style={{ maxWidth: 150 }} truncate>
                {log.model || 'N/A'}
              </Text>
            )
          },
          {
            key: 'status',
            label: 'Status',
            sortable: true,
            filterable: true,
            width: '100px',
            render: (log) => (
              <StatusIndicator
                status={mapLogStatusToSystemStatus(log.status)}
                variant="badge"
                size="sm"
              />
            )
          },
          {
            key: 'duration',
            label: 'Duration',
            sortable: true,
            sortType: 'number',
            width: '100px',
            render: (log) => (
              <StatusIndicator
                status={getResponseTimeStatus(log.duration)}
                variant="badge"
                size="sm"
                context={`${log.duration}ms`}
              />
            )
          },
          {
            key: 'virtualKeyName',
            label: 'Virtual Key',
            sortable: true,
            filterable: true,
            width: '140px',
            render: (log) => (
              <Text size="sm" style={{ maxWidth: 120 }} truncate>
                {log.virtualKeyName || 'N/A'}
              </Text>
            )
          },
          {
            key: 'provider',
            label: 'Provider',
            sortable: true,
            filterable: true,
            width: '100px',
            render: (log) => (
              <Text size="sm">{log.provider || 'N/A'}</Text>
            )
          },
          {
            key: 'ipAddress',
            label: 'IP Address',
            sortable: true,
            filterable: true,
            width: '120px',
            render: (log) => (
              <Text size="sm">{log.ipAddress}</Text>
            )
          },
          {
            key: 'tokens',
            label: 'Tokens',
            sortable: true,
            sortType: 'number',
            accessor: (log) => log.inputTokens + log.outputTokens,
            width: '120px',
            render: (log) => (
              <Text size="xs">
                {log.inputTokens} / {log.outputTokens}
              </Text>
            )
          },
          {
            key: 'cost',
            label: 'Cost',
            sortable: true,
            sortType: 'currency',
            width: '100px',
            render: (log) => (
              <Text size="sm">
                {log.cost ? formatCurrency(log.cost) : 'N/A'}
              </Text>
            )
          }
        ] as ColumnDef<RequestLog>[]}
        customActions={[
          {
            label: 'View Details',
            icon: IconEye,
            onClick: (log) => {
              setSelectedLog(log);
              openModal();
            },
            tooltip: 'View request details'
          }
        ]}
      />
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6

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
<<<<<<< HEAD
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
=======
                <Group grow>
                  <div>
                    <Text size="sm" fw={500}>Timestamp</Text>
                    <Text size="sm" c="dimmed">
                      {new Date(selectedLog.timestamp).toLocaleString()}
                    </Text>
                  </div>
                  <div>
                    <Text size="sm" fw={500}>Model</Text>
                    <Text size="sm" c="dimmed">
                      {selectedLog.model}
                    </Text>
                  </div>
                </Group>

                <Group grow>
                  <div>
                    <Text size="sm" fw={500}>Status</Text>
                    <StatusIndicator
                      status={mapLogStatusToSystemStatus(selectedLog.status)}
                      variant="badge"
                    />
                  </div>
                  <div>
                    <Text size="sm" fw={500}>Duration</Text>
                    <StatusIndicator
                      status={getResponseTimeStatus(selectedLog.duration)}
                      variant="badge"
                      context={`${selectedLog.duration}ms`}
                    />
                  </div>
                </Group>

                <Group grow>
                  <div>
                    <Text size="sm" fw={500}>Virtual Key</Text>
                    <Text size="sm" c="dimmed">
                      {selectedLog.virtualKeyName || 'N/A'}
                    </Text>
                  </div>
                  <div>
                    <Text size="sm" fw={500}>Model</Text>
                    <Text size="sm" c="dimmed">
                      {selectedLog.model || 'N/A'}
                    </Text>
                  </div>
                </Group>

                <Group grow>
                  <div>
                    <Text size="sm" fw={500}>IP Address</Text>
                    <Text size="sm" c="dimmed">{selectedLog.ipAddress}</Text>
                  </div>
                  <div>
                    <Text size="sm" fw={500}>User Agent</Text>
                    <Text size="sm" c="dimmed" style={{ maxWidth: 200 }} truncate>
                      {selectedLog.userAgent || 'N/A'}
                    </Text>
                  </div>
                </Group>

                <Group grow>
                  <div>
                    <Text size="sm" fw={500}>Input Tokens</Text>
                    <Text size="sm" c="dimmed">
                      {selectedLog.inputTokens}
                    </Text>
                  </div>
                  <div>
                    <Text size="sm" fw={500}>Output Tokens</Text>
                    <Text size="sm" c="dimmed">
                      {selectedLog.outputTokens}
                    </Text>
                  </div>
                </Group>

                {selectedLog.cost && (
                  <div>
                    <Text size="sm" fw={500}>Cost</Text>
                    <Text size="sm" c="dimmed">
                      {formatCurrency(selectedLog.cost)}
                    </Text>
                  </div>
                )}
                
                {(selectedLog.inputTokens || selectedLog.outputTokens) && (
                  <div>
                    <Text size="sm" fw={500}>Total Tokens</Text>
                    <Text size="sm" c="dimmed">
                      {(selectedLog.inputTokens + selectedLog.outputTokens).toLocaleString()}
                    </Text>
                  </div>
                )}

                {selectedLog.errorMessage && (
                  <Alert icon={<IconAlertCircle size={16} />} color="red">
                    <Text fw={500}>Error</Text>
                    <Text size="sm">{selectedLog.errorMessage}</Text>
                  </Alert>
                )}
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6
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