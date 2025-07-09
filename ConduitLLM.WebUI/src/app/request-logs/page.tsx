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
  Pagination,
  LoadingOverlay,
  Alert,
  ActionIcon,
  Tooltip,
  Modal,
  Code,
  ScrollArea,
  Tabs,
  NumberInput,
  Switch,
} from '@mantine/core';
import { DateTimePicker } from '@mantine/dates';
import {
  IconSearch,
  IconRefresh,
  IconDownload,
  IconEye,
  IconFilter,
  IconX,
  IconAlertCircle,
  IconCode,
  IconChartBar,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { useRequestLogs, useExportRequestLogs, type RequestLog } from '@/hooks/api/useRequestLogsApi';

interface RequestLogFilters {
  search: string;
  method: string;
  statusCode: string;
  provider: string;
  virtualKey: string;
  dateFrom: Date | null;
  dateTo: Date | null;
  minResponseTime: number | null;
  maxResponseTime: number | null;
  showErrors: boolean;
}

export default function RequestLogsPage() {
  const [currentPage, setCurrentPage] = useState(1);
  const [selectedLog, setSelectedLog] = useState<RequestLog | null>(null);
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure(false);
  const [filtersOpened, { toggle: toggleFilters }] = useDisclosure(false);
  const [autoRefresh, setAutoRefresh] = useState(false);
  
  const [filters, setFilters] = useState<RequestLogFilters>({
    search: '',
    method: '',
    statusCode: '',
    provider: '',
    virtualKey: '',
    dateFrom: null,
    dateTo: null,
    minResponseTime: null,
    maxResponseTime: null,
    showErrors: false,
  });

  // Convert filters to API format
  const apiFilters = {
    pageNumber: currentPage,
    pageSize: 20,
    status: filters.statusCode as 'success' | 'error' | 'timeout' | undefined,
    virtualKeyId: filters.virtualKey ? parseInt(filters.virtualKey, 10) : undefined,
    startDate: filters.dateFrom ? filters.dateFrom.toISOString() : undefined,
    endDate: filters.dateTo ? filters.dateTo.toISOString() : undefined,
    minResponseTime: filters.minResponseTime || undefined,
    maxResponseTime: filters.maxResponseTime || undefined,
  };

  // Use the API hook
  const { data, isLoading, refetch, error } = useRequestLogs(apiFilters);
  const exportMutation = useExportRequestLogs();

  // Extract data from response
  const logs = data?.items || [];
  const totalPages = data?.totalPages || 1;

  // Reset to page 1 when filters change (except for currentPage)
  useEffect(() => {
    setCurrentPage(1);
  }, [filters.search, filters.method, filters.statusCode, filters.provider, filters.virtualKey, filters.dateFrom, filters.dateTo, filters.minResponseTime, filters.maxResponseTime, filters.showErrors]);

  useEffect(() => {
    if (autoRefresh) {
      const interval = setInterval(() => {
        refetch();
      }, 5000);
      return () => clearInterval(interval);
    }
    return undefined;
  }, [autoRefresh, refetch]);

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

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'success':
        return 'green';
      case 'error':
        return 'red';
      case 'timeout':
        return 'orange';
      default:
        return 'gray';
    }
  };

  const getResponseTimeColor = (responseTime: number) => {
    if (responseTime < 500) return 'green';
    if (responseTime < 1000) return 'yellow';
    return 'red';
  };

  const formatFileSize = (bytes: number) => {
    const sizes = ['B', 'KB', 'MB', 'GB'];
    if (bytes === 0) return '0 B';
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    return `${(bytes / Math.pow(1024, i)).toFixed(1)} ${sizes[i]}`;
  };

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 4,
    }).format(amount);
  };

  const clearFilters = () => {
    setFilters({
      search: '',
      method: '',
      statusCode: '',
      provider: '',
      virtualKey: '',
      dateFrom: null,
      dateTo: null,
      minResponseTime: null,
      maxResponseTime: null,
      showErrors: false,
    });
  };

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>Request Logs</Title>
          <Text c="dimmed">Monitor and analyze API request logs</Text>
        </div>

        <Group>
          <Switch
            label="Auto-refresh"
            checked={autoRefresh}
            onChange={(event) => setAutoRefresh(event.currentTarget.checked)}
          />
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={() => refetch()}
            loading={isLoading}
          >
            Refresh
          </Button>
          <Button
            leftSection={<IconDownload size={16} />}
            onClick={handleExport}
            loading={exportMutation.isPending}
            disabled={exportMutation.isPending}
          >
            Export CSV
          </Button>
        </Group>
      </Group>

      {/* Filters */}
      <Card withBorder>
        <Group justify="space-between" mb="md">
          <Text fw={600}>Filters</Text>
          <Group>
            <Button
              variant="subtle"
              size="xs"
              leftSection={<IconX size={14} />}
              onClick={clearFilters}
            >
              Clear All
            </Button>
            <Button
              variant="light"
              size="xs"
              leftSection={<IconFilter size={14} />}
              onClick={toggleFilters}
            >
              {filtersOpened ? 'Hide' : 'Show'} Advanced
            </Button>
          </Group>
        </Group>

        <Stack gap="md">
          <Group grow>
            <TextInput
              placeholder="Search logs..."
              leftSection={<IconSearch size={16} />}
              value={filters.search}
              onChange={(event) => setFilters({ ...filters, search: event.currentTarget.value })}
            />
            <Select
              placeholder="Method"
              data={[
                { value: '', label: 'All Methods' },
                { value: 'GET', label: 'GET' },
                { value: 'POST', label: 'POST' },
                { value: 'PUT', label: 'PUT' },
                { value: 'DELETE', label: 'DELETE' },
              ]}
              value={filters.method}
              onChange={(value) => setFilters({ ...filters, method: value || '' })}
            />
            <Select
              placeholder="Status"
              data={[
                { value: '', label: 'All Status' },
                { value: 'success', label: 'Success' },
                { value: 'error', label: 'Error' },
                { value: 'timeout', label: 'Timeout' },
              ]}
              value={filters.statusCode}
              onChange={(value) => setFilters({ ...filters, statusCode: value || '' })}
            />
          </Group>

          {filtersOpened && (
            <Stack gap="md">
              <Group grow>
                <Select
                  placeholder="Provider"
                  data={[
                    { value: '', label: 'All Providers' },
                    { value: 'OpenAI', label: 'OpenAI' },
                    { value: 'Anthropic', label: 'Anthropic' },
                    { value: 'Azure', label: 'Azure' },
                    { value: 'System', label: 'System' },
                  ]}
                  value={filters.provider}
                  onChange={(value) => setFilters({ ...filters, provider: value || '' })}
                />
                <Select
                  placeholder="Virtual Key"
                  data={[
                    { value: '', label: 'All Keys' },
                    { value: 'vk_123', label: 'Production API Key' },
                    { value: 'vk_456', label: 'Test Key' },
                    { value: 'vk_789', label: 'Admin Key' },
                  ]}
                  value={filters.virtualKey}
                  onChange={(value) => setFilters({ ...filters, virtualKey: value || '' })}
                />
              </Group>

              <Group grow>
                <DateTimePicker
                  label="From Date"
                  placeholder="Select start date"
                  value={filters.dateFrom}
                  onChange={(value) => setFilters({ ...filters, dateFrom: value as Date | null })}
                />
                <DateTimePicker
                  label="To Date"
                  placeholder="Select end date"
                  value={filters.dateTo}
                  onChange={(value) => setFilters({ ...filters, dateTo: value as Date | null })}
                />
              </Group>

              <Group grow>
                <NumberInput
                  label="Min Response Time (ms)"
                  placeholder="0"
                  min={0}
                  value={filters.minResponseTime ?? undefined}
                  onChange={(value) => setFilters({ ...filters, minResponseTime: value as number })}
                />
                <NumberInput
                  label="Max Response Time (ms)"
                  placeholder="10000"
                  min={0}
                  value={filters.maxResponseTime ?? undefined}
                  onChange={(value) => setFilters({ ...filters, maxResponseTime: value as number })}
                />
              </Group>

              <Switch
                label="Show only errors"
                checked={filters.showErrors}
                onChange={(event) => setFilters({ ...filters, showErrors: event.currentTarget.checked })}
              />
            </Stack>
          )}
        </Stack>
      </Card>

      {/* Logs Table */}
      <Card>
        <div style={{ position: 'relative' }}>
          <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
          
          {error && (
            <Alert icon={<IconAlertCircle size={16} />} color="red" mb="md">
              <Text fw={500}>Error loading request logs</Text>
              <Text size="sm">{(error as Error).message}</Text>
            </Alert>
          )}
          
          <ScrollArea>
            <Table striped highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Timestamp</Table.Th>
                  <Table.Th>Type</Table.Th>
                  <Table.Th>Model</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th>Duration</Table.Th>
                  <Table.Th>Virtual Key</Table.Th>
                  <Table.Th>Provider</Table.Th>
                  <Table.Th>IP Address</Table.Th>
                  <Table.Th>Tokens</Table.Th>
                  <Table.Th>Cost</Table.Th>
                  <Table.Th>Actions</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {logs.map((log: RequestLog) => (
                  <Table.Tr key={log.id}>
                    <Table.Td>
                      <Text size="xs">
                        {new Date(log.timestamp).toLocaleString()}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Badge variant="light" size="sm">
                        POST
                      </Badge>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm" style={{ maxWidth: 200 }} truncate>
                        {log.model || 'N/A'}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Badge color={getStatusColor(log.status)} variant="light">
                        {log.status}
                      </Badge>
                    </Table.Td>
                    <Table.Td>
                      <Badge color={getResponseTimeColor(log.duration)} variant="light">
                        {log.duration}ms
                      </Badge>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm" style={{ maxWidth: 120 }} truncate>
                        {log.virtualKeyName || 'N/A'}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm">{log.provider || 'N/A'}</Text>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm">{log.ipAddress}</Text>
                    </Table.Td>
                    <Table.Td>
                      <Text size="xs">
                        {log.inputTokens} / {log.outputTokens} tokens
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm">
                        {log.cost ? formatCurrency(log.cost) : 'N/A'}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Tooltip label="View details">
                        <ActionIcon
                          variant="subtle"
                          size="sm"
                          onClick={() => {
                            setSelectedLog(log);
                            openModal();
                          }}
                        >
                          <IconEye size={16} />
                        </ActionIcon>
                      </Tooltip>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </ScrollArea>

          {logs.length === 0 && !isLoading && (
            <Text c="dimmed" ta="center" py="xl">
              No request logs found. Try adjusting your filters.
            </Text>
          )}
        </div>

        {totalPages > 1 && (
          <Group justify="center" mt="lg">
            <Pagination
              value={currentPage}
              onChange={setCurrentPage}
              total={totalPages}
              size="sm"
            />
          </Group>
        )}
      </Card>

      {/* Log Details Modal */}
      <Modal
        opened={modalOpened}
        onClose={closeModal}
        title="Request Log Details"
        size="xl"
      >
        {selectedLog && (
          <Tabs defaultValue="overview">
            <Tabs.List>
              <Tabs.Tab value="overview" leftSection={<IconChartBar size={16} />}>
                Overview
              </Tabs.Tab>
              <Tabs.Tab value="request" leftSection={<IconCode size={16} />}>
                Request
              </Tabs.Tab>
              <Tabs.Tab value="response" leftSection={<IconCode size={16} />}>
                Response
              </Tabs.Tab>
            </Tabs.List>

            <Tabs.Panel value="overview" pt="md">
              <Stack gap="md">
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
                    <Badge color={getStatusColor(selectedLog.status)}>
                      {selectedLog.status}
                    </Badge>
                  </div>
                  <div>
                    <Text size="sm" fw={500}>Duration</Text>
                    <Badge color={getResponseTimeColor(selectedLog.duration)}>
                      {selectedLog.duration}ms
                    </Badge>
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
              </Stack>
            </Tabs.Panel>

            <Tabs.Panel value="request" pt="md">
              <Stack gap="md">
                <div>
                  <Text size="sm" fw={500} mb="xs">Request Headers</Text>
                  <Code block>
                    {JSON.stringify({
                      'Content-Type': 'application/json',
                      'Authorization': 'Bearer ***',
                      'User-Agent': selectedLog.userAgent,
                      'X-Forwarded-For': selectedLog.ipAddress,
                    }, null, 2)}
                  </Code>
                </div>
                
                <div>
                  <Text size="sm" fw={500} mb="xs">Request Body</Text>
                  <Code block>
                    {true ? 
                      JSON.stringify({
                        model: selectedLog.model,
                        messages: [
                          { role: 'user', content: 'Hello, how are you?' }
                        ],
                        temperature: 0.7,
                        max_tokens: 150
                      }, null, 2) : 
                      'No request body'
                    }
                  </Code>
                </div>
              </Stack>
            </Tabs.Panel>

            <Tabs.Panel value="response" pt="md">
              <Stack gap="md">
                <div>
                  <Text size="sm" fw={500} mb="xs">Response Headers</Text>
                  <Code block>
                    {JSON.stringify({
                      'Content-Type': 'application/json',
                      'X-Response-Time': `${selectedLog.duration}ms`,
                      'X-Model': selectedLog.model,
                      'X-Request-ID': selectedLog.id,
                    }, null, 2)}
                  </Code>
                </div>
                
                <div>
                  <Text size="sm" fw={500} mb="xs">Response Body</Text>
                  <Code block>
                    {selectedLog.status === 'error' ? 
                      JSON.stringify({
                        error: {
                          message: selectedLog.errorMessage || 'Request failed',
                          type: 'api_error',
                          code: selectedLog.status
                        }
                      }, null, 2) :
                      JSON.stringify({
                        id: selectedLog.id,
                        object: 'chat.completion',
                        created: Math.floor(new Date(selectedLog.timestamp).getTime() / 1000),
                        model: selectedLog.model,
                        choices: [
                          {
                            index: 0,
                            message: {
                              role: 'assistant',
                              content: 'Hello! I\'m doing well, thank you for asking. How can I help you today?'
                            },
                            finish_reason: 'stop'
                          }
                        ],
                        usage: {
                          prompt_tokens: 12,
                          completion_tokens: 18,
                          total_tokens: 30
                        }
                      }, null, 2)
                    }
                  </Code>
                </div>
              </Stack>
            </Tabs.Panel>
          </Tabs>
        )}
      </Modal>
    </Stack>
  );
}