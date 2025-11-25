'use client';

import { useState, useEffect, useCallback, useMemo } from 'react';
import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Card,
  SimpleGrid,
  ThemeIcon,
  LoadingOverlay,
  Alert,
  Menu,
  rem,
} from '@mantine/core';
import {
  IconListDetails,
  IconAlertCircle,
  IconDownload,
  IconFileTypeCsv,
  IconJson,
  IconActivity,
  IconCoin,
  IconClock,
  IconCheck,
} from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import { TablePagination } from '@/components/common/TablePagination';
import { RequestLogsTable } from '@/components/analytics/RequestLogsTable';
import { RequestLogsFilters } from '@/components/analytics/RequestLogsFilters';
import { useRequestLogs, useDistinctModels } from '@/hooks/useRequestLogs';
import type { RequestLogFilters } from '@/hooks/useRequestLogs';
import { exportToCSV, exportToJSON, formatDateForExport } from '@/lib/utils/export';
import { withAdminClient } from '@/lib/client/adminClient';
import type { VirtualKeyDto } from '@knn_labs/conduit-admin-client';

export default function RequestLogsPage() {
  // Pagination state
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);

  // Filter state
  const [filters, setFilters] = useState<RequestLogFilters>({});

  // Virtual keys for filter dropdown
  const [virtualKeys, setVirtualKeys] = useState<VirtualKeyDto[]>([]);

  // Fetch request logs
  const {
    logs,
    totalCount,
    totalPages,
    isLoading,
    error,
    stats,
    refetch,
  } = useRequestLogs({
    page,
    pageSize,
    filters,
  });

  // Fetch distinct models for filter
  const { models } = useDistinctModels();

  // Fetch virtual keys for filter dropdown
  useEffect(() => {
    const fetchVirtualKeys = async () => {
      try {
        const result = await withAdminClient((client) => client.virtualKeys.list(1, 1000));
        const validKeys = result.items.filter(
          (key): key is VirtualKeyDto => key.id !== undefined && key.id !== null
        );
        setVirtualKeys(validKeys);
      } catch (err) {
        console.warn('Error fetching virtual keys:', err);
      }
    };

    void fetchVirtualKeys();
  }, []);

  // Handle page change
  const handlePageChange = useCallback((newPage: number) => {
    setPage(newPage);
  }, []);

  // Handle page size change
  const handlePageSizeChange = useCallback((newPageSize: number) => {
    setPageSize(newPageSize);
    setPage(1); // Reset to first page when changing page size
  }, []);

  // Handle filter changes
  const handleFiltersChange = useCallback((newFilters: RequestLogFilters) => {
    setFilters(newFilters);
    setPage(1); // Reset to first page when changing filters
  }, []);

  // Export handlers
  const handleExportCSV = useCallback(() => {
    if (logs.length === 0) {
      notifications.show({
        title: 'No data to export',
        message: 'There are no request logs to export with the current filters',
        color: 'orange',
      });
      return;
    }

    const exportData = logs.map((log) => ({
      id: log.id,
      timestamp: formatDateForExport(log.timestamp),
      model: log.modelName,
      requestType: log.requestType,
      inputTokens: log.inputTokens,
      outputTokens: log.outputTokens,
      totalTokens: log.inputTokens + log.outputTokens,
      cost: log.cost,
      latencyMs: log.responseTimeMs,
      statusCode: log.statusCode ?? '',
      virtualKeyId: log.virtualKeyId,
      userId: log.userId ?? '',
      clientIp: log.clientIp ?? '',
      requestPath: log.requestPath ?? '',
    }));

    exportToCSV(exportData, `request-logs-${new Date().toISOString().split('T')[0]}`, [
      { key: 'id', label: 'ID' },
      { key: 'timestamp', label: 'Timestamp' },
      { key: 'model', label: 'Model' },
      { key: 'requestType', label: 'Request Type' },
      { key: 'inputTokens', label: 'Input Tokens' },
      { key: 'outputTokens', label: 'Output Tokens' },
      { key: 'totalTokens', label: 'Total Tokens' },
      { key: 'cost', label: 'Cost' },
      { key: 'latencyMs', label: 'Latency (ms)' },
      { key: 'statusCode', label: 'Status Code' },
      { key: 'virtualKeyId', label: 'Virtual Key ID' },
      { key: 'userId', label: 'User ID' },
      { key: 'clientIp', label: 'Client IP' },
      { key: 'requestPath', label: 'Request Path' },
    ]);

    notifications.show({
      title: 'Export successful',
      message: `Exported ${logs.length} request logs`,
      color: 'green',
    });
  }, [logs]);

  const handleExportJSON = useCallback(() => {
    if (logs.length === 0) {
      notifications.show({
        title: 'No data to export',
        message: 'There are no request logs to export with the current filters',
        color: 'orange',
      });
      return;
    }

    exportToJSON(logs, `request-logs-${new Date().toISOString().split('T')[0]}`);

    notifications.show({
      title: 'Export successful',
      message: `Exported ${logs.length} request logs`,
      color: 'green',
    });
  }, [logs]);

  // Statistics cards
  const statCards = useMemo(() => {
    if (!stats) return [];

    return [
      {
        title: 'Total Requests',
        value: stats.totalRequests.toLocaleString(),
        icon: IconActivity,
        color: 'blue',
      },
      {
        title: 'Success Rate',
        value: `${stats.successRate.toFixed(1)}%`,
        icon: IconCheck,
        color: 'green',
      },
      {
        title: 'Total Cost',
        value: `$${stats.totalCost.toFixed(4)}`,
        icon: IconCoin,
        color: 'orange',
      },
      {
        title: 'Avg Latency',
        value: `${Math.round(stats.avgLatency)} ms`,
        icon: IconClock,
        color: 'violet',
      },
    ];
  }, [stats]);

  if (error) {
    return (
      <Stack gap="xl">
        <div>
          <Title order={1}>Request Logs</Title>
          <Text c="dimmed">View individual API request details</Text>
        </div>

        <Alert icon={<IconAlertCircle size={16} />} title="Error loading request logs" color="red">
          {error.message}
        </Alert>
      </Stack>
    );
  }

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>Request Logs</Title>
          <Text c="dimmed">View individual API request details with full metadata</Text>
        </div>

        <Menu shadow="md" width={200}>
          <Menu.Target>
            <Button variant="light" leftSection={<IconDownload size={16} />}>
              Export
            </Button>
          </Menu.Target>

          <Menu.Dropdown>
            <Menu.Item
              leftSection={<IconFileTypeCsv style={{ width: rem(14), height: rem(14) }} />}
              onClick={handleExportCSV}
            >
              Export as CSV
            </Menu.Item>
            <Menu.Item
              leftSection={<IconJson style={{ width: rem(14), height: rem(14) }} />}
              onClick={handleExportJSON}
            >
              Export as JSON
            </Menu.Item>
          </Menu.Dropdown>
        </Menu>
      </Group>

      {/* Statistics Cards */}
      <SimpleGrid cols={{ base: 1, sm: 2, md: 4 }} spacing="lg">
        {statCards.map((stat) => (
          <Card key={stat.title} p="md" withBorder>
            <Group justify="space-between">
              <div>
                <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                  {stat.title}
                </Text>
                <Text fw={700} size="xl">
                  {stat.value}
                </Text>
              </div>
              <ThemeIcon size="lg" variant="light" color={stat.color}>
                <stat.icon size={20} />
              </ThemeIcon>
            </Group>
          </Card>
        ))}
      </SimpleGrid>

      {/* Filters */}
      <Card>
        <Card.Section p="md" withBorder>
          <Group justify="space-between">
            <Group gap="xs">
              <ThemeIcon size="sm" variant="light" color="cyan">
                <IconListDetails size={14} />
              </ThemeIcon>
              <Text fw={600}>Filters</Text>
            </Group>
            <Text size="sm" c="dimmed">
              {totalCount.toLocaleString()} request{totalCount !== 1 ? 's' : ''} found
            </Text>
          </Group>
        </Card.Section>

        <Card.Section p="md">
          <RequestLogsFilters
            filters={filters}
            onFiltersChange={handleFiltersChange}
            models={models}
            virtualKeys={virtualKeys}
            isLoading={isLoading}
            onRefresh={refetch}
          />
        </Card.Section>
      </Card>

      {/* Request Logs Table */}
      <Card>
        <Card.Section p="md" withBorder>
          <Group justify="space-between">
            <Text fw={600}>Request Logs</Text>
            <Text size="sm" c="dimmed">
              Showing {logs.length} of {totalCount.toLocaleString()} entries
            </Text>
          </Group>
        </Card.Section>

        <Card.Section p="md" pt={0} style={{ position: 'relative' }}>
          <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
          <RequestLogsTable data={logs} isLoading={isLoading} />
          {totalCount > 0 && (
            <TablePagination
              total={totalCount}
              page={page}
              pageSize={pageSize}
              onPageChange={handlePageChange}
              onPageSizeChange={handlePageSizeChange}
              pageSizeOptions={['25', '50', '100']}
            />
          )}
        </Card.Section>
      </Card>
    </Stack>
  );
}
