'use client';

import { useState, useMemo } from 'react';
import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Card,
  Grid,
  Select,
  LoadingOverlay,
  Menu,
  rem,
} from '@mantine/core';
import {
  IconRefresh,
  IconDownload,
  IconFilter,
  IconFileTypeCsv,
  IconBraces,
} from '@tabler/icons-react';
import { ErrorDisplay } from '@/components/common/ErrorDisplay';
import { TablePagination } from '@/components/common/TablePagination';
import { useSecurityDashboardData, useSecurityEventsFiltered } from './hooks';
import { useSecurityDashboardHandlers } from './handlers';
import { SecurityOverviewCard } from './SecurityOverviewCard';
import { QuickStatsCards } from './QuickStatsCards';
import { SecurityEventsTable } from './SecurityEventsTable';
import { ActiveThreatsPanel } from './ActiveThreatsPanel';
import type { SecurityEventFiltersState } from './types';

const SEVERITY_OPTIONS = [
  { value: 'all', label: 'All Severities' },
  { value: 'critical', label: 'Critical' },
  { value: 'high', label: 'High' },
  { value: 'medium', label: 'Medium' },
  { value: 'low', label: 'Low' },
];

export default function SecurityDashboard() {
  const [isExporting, setIsExporting] = useState(false);
  const [selectedSeverity, setSelectedSeverity] = useState<string>('all');
  const [filters, setFilters] = useState<SecurityEventFiltersState>({
    page: 1,
    pageSize: 20,
  });

  const {
    events,
    totalEvents,
    threats,
    overview,
    quickStats,
    isLoading,
    error,
    refetchAll,
  } = useSecurityDashboardData();

  // Filtered events query when severity filter is applied
  const {
    data: filteredEventsData,
    isLoading: isLoadingFiltered,
  } = useSecurityEventsFiltered({
    ...filters,
    severity: selectedSeverity !== 'all' ? selectedSeverity as SecurityEventFiltersState['severity'] : undefined,
  });

  const { handleRefresh, handleExportEvents } = useSecurityDashboardHandlers(
    refetchAll,
    setIsExporting
  );

  // Determine which events to display
  const displayedEvents = useMemo(() => {
    const sourceEvents = filteredEventsData?.items ?? events;
    if (selectedSeverity === 'all') {
      return sourceEvents;
    }
    return sourceEvents.filter(e => e.severity === selectedSeverity);
  }, [filteredEventsData, events, selectedSeverity]);

  // Calculate total for pagination
  const displayTotal = filteredEventsData?.totalCount ?? totalEvents;

  // Paginate displayed events
  const paginatedEvents = useMemo(() => {
    const start = (filters.page - 1) * filters.pageSize;
    return displayedEvents.slice(start, start + filters.pageSize);
  }, [displayedEvents, filters.page, filters.pageSize]);

  const handlePageChange = (page: number) => {
    setFilters(f => ({ ...f, page }));
  };

  const handlePageSizeChange = (pageSize: number) => {
    setFilters(f => ({ ...f, pageSize, page: 1 }));
  };

  const handleSeverityChange = (value: string | null) => {
    setSelectedSeverity(value ?? 'all');
    setFilters(f => ({ ...f, page: 1 }));
  };

  if (error) {
    return (
      <ErrorDisplay
        error={error instanceof Error ? error : new Error(String(error))}
        onRetry={() => void handleRefresh()}
      />
    );
  }

  return (
    <Stack gap="lg">
      {/* Header */}
      <Group justify="space-between" wrap="wrap" gap="md">
        <div>
          <Title order={2}>Security Dashboard</Title>
          <Text size="sm" c="dimmed">
            Monitor security events, threats, and compliance
          </Text>
        </div>
        <Group gap="sm">
          <Select
            value={selectedSeverity}
            onChange={handleSeverityChange}
            data={SEVERITY_OPTIONS}
            leftSection={<IconFilter size={16} />}
            w={160}
            size="sm"
          />
          <Button
            variant="subtle"
            leftSection={<IconRefresh size={16} />}
            onClick={() => void handleRefresh()}
            loading={isLoading}
            size="sm"
          >
            Refresh
          </Button>
          <Menu shadow="md" width={200}>
            <Menu.Target>
              <Button
                variant="light"
                leftSection={<IconDownload size={16} />}
                loading={isExporting}
                size="sm"
              >
                Export
              </Button>
            </Menu.Target>
            <Menu.Dropdown>
              <Menu.Item
                leftSection={<IconBraces style={{ width: rem(14), height: rem(14) }} />}
                onClick={() => void handleExportEvents(displayedEvents, 'json')}
              >
                Export as JSON
              </Menu.Item>
              <Menu.Item
                leftSection={<IconFileTypeCsv style={{ width: rem(14), height: rem(14) }} />}
                onClick={() => void handleExportEvents(displayedEvents, 'csv')}
              >
                Export as CSV
              </Menu.Item>
            </Menu.Dropdown>
          </Menu>
        </Group>
      </Group>

      {/* Security Overview */}
      <SecurityOverviewCard overview={overview} isLoading={isLoading} />

      {/* Quick Stats */}
      <QuickStatsCards stats={quickStats} isLoading={isLoading} />

      {/* Main Content Grid */}
      <Grid gutter="lg">
        {/* Events Table */}
        <Grid.Col span={{ base: 12, lg: 8 }}>
          <Card padding="lg" radius="md" withBorder>
            <Card.Section withBorder inheritPadding py="md">
              <Group justify="space-between">
                <Text fw={600}>Security Events</Text>
                <Text size="sm" c="dimmed">
                  {displayTotal} total events
                </Text>
              </Group>
            </Card.Section>
            <Card.Section p="md" style={{ position: 'relative' }}>
              <LoadingOverlay
                visible={isLoadingFiltered}
                overlayProps={{ blur: 2 }}
              />
              <SecurityEventsTable
                events={paginatedEvents}
                isLoading={isLoading && !events.length}
              />
              {displayTotal > filters.pageSize && (
                <TablePagination
                  total={displayTotal}
                  page={filters.page}
                  pageSize={filters.pageSize}
                  onPageChange={handlePageChange}
                  onPageSizeChange={handlePageSizeChange}
                />
              )}
            </Card.Section>
          </Card>
        </Grid.Col>

        {/* Active Threats Panel */}
        <Grid.Col span={{ base: 12, lg: 4 }}>
          <Card padding="lg" radius="md" withBorder h="100%">
            <Card.Section withBorder inheritPadding py="md">
              <Group justify="space-between">
                <Text fw={600}>Active Threats</Text>
                <Text size="sm" c="dimmed">
                  {threats.filter(t => t.status === 'active').length} active
                </Text>
              </Group>
            </Card.Section>
            <Card.Section p="md">
              <ActiveThreatsPanel threats={threats} isLoading={isLoading} />
            </Card.Section>
          </Card>
        </Grid.Col>
      </Grid>
    </Stack>
  );
}
