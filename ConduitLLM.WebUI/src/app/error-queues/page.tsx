'use client';

import { useState } from 'react';
import {
  Container,
  Grid,
  Card,
  Text,
  Group,
  Stack,
  Paper,
  ThemeIcon,
  Badge,
  Title,
  Button,
  TextInput,
  Select,
  Loader,
  Center,
  Alert,
} from '@mantine/core';
import {
  IconAlertTriangle,
  IconMailbox,
  IconClock,
  IconRefresh,
  IconSearch,
  IconFilter,
  IconDownload,
} from '@tabler/icons-react';
import { useErrorQueues } from '@/hooks/useErrorQueues';
import { ErrorQueueTable } from './components/ErrorQueueTable';
import { ErrorQueueCharts } from './components/ErrorQueueCharts';
import { formatRelativeTime } from '@/utils/formatters';
// SDK types are now properly handled in useErrorQueues hook

interface SummaryCardProps {
  title: string;
  value: string | number;
  icon: React.ReactNode;
  status?: 'ok' | 'warning' | 'critical';
  trend?: { value: number; isPositive: boolean };
}

function SummaryCard({ title, value, icon, status, trend }: SummaryCardProps) {
  const getStatusColor = () => {
    switch (status) {
      case 'critical':
        return 'red';
      case 'warning':
        return 'yellow';
      case 'ok':
        return 'green';
      default:
        return 'blue';
    }
  };

  return (
    <Card shadow="sm" radius="md" p="lg">
      <Group justify="space-between" align="flex-start">
        <Stack gap="xs">
          <Text size="sm" c="dimmed" fw={500}>
            {title}
          </Text>
          <Text size="xl" fw={700}>
            {value}
          </Text>
          {trend && (
            <Badge
              size="sm"
              variant="light"
              color={trend.isPositive ? 'green' : 'red'}
            >
              {trend.isPositive ? '↑' : '↓'} {Math.abs(trend.value)}%
            </Badge>
          )}
        </Stack>
        <ThemeIcon
          size="xl"
          radius="xl"
          variant="light"
          color={getStatusColor()}
        >
          {icon}
        </ThemeIcon>
      </Group>
    </Card>
  );
}

export default function ErrorQueuesPage() {
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<string | null>(null);

  const { data, isLoading, error, refetch } = useErrorQueues({
    includeEmpty: false,
    queueNameFilter: searchQuery || undefined,
  });

  if (isLoading) {
    return (
      <Center h="100vh">
        <Loader size="lg" />
      </Center>
    );
  }

  if (error) {
    return (
      <Container size="xl" py="xl">
        <Alert
          icon={<IconAlertTriangle size={16} />}
          title="Failed to load error queues"
          color="red"
        >
          {error.message}
        </Alert>
      </Container>
    );
  }

  const summary = data?.summary;
  const queues = data?.queues ?? [];

  // Filter queues by status if selected
  const filteredQueues = statusFilter
    ? queues.filter((q) => q.status === statusFilter)
    : queues;

  // Calculate oldest message across all queues
  const oldestMessage = queues.reduce((oldest: Date | null, queue) => {
    if (!queue.oldestMessageTimestamp) return oldest;
    const queueOldest = new Date(queue.oldestMessageTimestamp);
    return !oldest || queueOldest < oldest ? queueOldest : oldest;
  }, null);

  return (
    <Container size="xl" py="xl">
      <Stack gap="lg">
        {/* Header */}
        <Group justify="space-between" align="center">
          <Title order={2}>Error Queues</Title>
          <Group>
            <Button
              leftSection={<IconRefresh size={16} />}
              variant="light"
              onClick={() => void refetch()}
            >
              Refresh
            </Button>
            <Button
              leftSection={<IconDownload size={16} />}
              variant="light"
              disabled
            >
              Export CSV
            </Button>
          </Group>
        </Group>

        {/* Summary Cards */}
        <Grid>
          <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
            <SummaryCard
              title="Total Error Queues"
              value={summary?.totalQueues ?? 0}
              icon={<IconMailbox size={20} />}
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
            <SummaryCard
              title="Total Messages"
              value={summary?.totalMessages ?? 0}
              icon={<IconAlertTriangle size={20} />}
              status={(() => {
                const totalMessages = summary?.totalMessages ?? 0;
                if (totalMessages > 1000) return 'critical';
                if (totalMessages > 100) return 'warning';
                return 'ok';
              })()}
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
            <SummaryCard
              title="Critical Queues"
              value={summary?.criticalQueues?.length ?? 0}
              icon={<IconAlertTriangle size={20} />}
              status={
                (summary?.criticalQueues?.length ?? 0) > 0 ? 'critical' : 'ok'
              }
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
            <SummaryCard
              title="Oldest Message"
              value={
                oldestMessage
                  ? formatRelativeTime(oldestMessage)
                  : 'No messages'
              }
              icon={<IconClock size={20} />}
            />
          </Grid.Col>
        </Grid>

        {/* Filters */}
        <Paper shadow="xs" p="md" radius="md">
          <Group>
            <TextInput
              placeholder="Search queues..."
              leftSection={<IconSearch size={16} />}
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.currentTarget.value)}
              style={{ flex: 1 }}
            />
            <Select
              placeholder="Filter by status"
              leftSection={<IconFilter size={16} />}
              value={statusFilter}
              onChange={setStatusFilter}
              data={[
                { value: 'ok', label: 'OK' },
                { value: 'warning', label: 'Warning' },
                { value: 'critical', label: 'Critical' },
              ]}
              clearable
              w={200}
            />
          </Group>
        </Paper>

        {/* Error Queue Table */}
        <ErrorQueueTable
          queues={filteredQueues}
          onRefresh={() => void refetch()}
        />

        {/* Charts Section */}
        <ErrorQueueCharts queues={queues} />
      </Stack>
    </Container>
  );
}