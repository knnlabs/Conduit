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
import { BaseTable, type ColumnDef } from '@/components/common/BaseTable';
import { StatusIndicator } from '@/components/common/StatusIndicator';

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