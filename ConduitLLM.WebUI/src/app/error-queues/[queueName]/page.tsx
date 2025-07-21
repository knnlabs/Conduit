'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import {
  Container,
  Stack,
  Paper,
  Title,
  Text,
  Group,
  Button,
  Grid,
  Badge,
  Loader,
  Center,
  Alert,
  TextInput,
  Select,
  Pagination,
} from '@mantine/core';
import {
  IconArrowLeft,
  IconRefresh,
  IconTrash,
  IconAlertTriangle,
  IconSearch,
  IconFilter,
  IconReload,
} from '@tabler/icons-react';
import {
  useErrorQueues,
  useReplayAllMessages,
  useClearQueue,
} from '@/hooks/useErrorQueues';
import { MessageList } from '../components/MessageList';
import { ConfirmationModal } from '@/components/common/ConfirmationModal';
import { formatBytes, formatDateTime } from '@/utils/formatters';

interface InfoItemProps {
  label: string;
  value: string | number;
}

function InfoItem({ label, value }: InfoItemProps) {
  return (
    <div>
      <Text size="sm" c="dimmed">
        {label}
      </Text>
      <Text fw={600}>{value}</Text>
    </div>
  );
}

export default function QueueDetailPage() {
  const params = useParams();
  const router = useRouter();
  const queueName = decodeURIComponent(params.queueName as string);

  const [page, setPage] = useState(1);
  const pageSize = 20;
  const [searchQuery, setSearchQuery] = useState('');
  const [dateFilter, setDateFilter] = useState<string | null>(null);
  const [showReplayConfirm, setShowReplayConfirm] = useState(false);
  const [showClearConfirm, setShowClearConfirm] = useState(false);

  const { data: queuesData, isLoading, error } = useErrorQueues();
  const replayAllMutation = useReplayAllMessages();
  const clearQueueMutation = useClearQueue();

  interface QueueData {
    queueName: string;
    status: 'critical' | 'warning' | 'ok';
    messageCount: number;
    messageBytes: number;
    oldestMessageTimestamp?: string;
    messageRate: number;
  }
  
  const typedQueuesData = queuesData as { queues: QueueData[] } | undefined;
  const queue = typedQueuesData?.queues.find((q: QueueData) => q.queueName === queueName);

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
          title="Failed to load queue details"
          color="red"
        >
          {error.message}
        </Alert>
      </Container>
    );
  }

  if (!queue) {
    return (
      <Container size="xl" py="xl">
        <Alert
          icon={<IconAlertTriangle size={16} />}
          title="Queue not found"
          color="red"
        >
          The error queue &quot;{queueName}&quot; was not found.
        </Alert>
      </Container>
    );
  }

  const handleReplayAll = async () => {
    await replayAllMutation.mutateAsync(queueName);
    setShowReplayConfirm(false);
  };

  const handleClearQueue = async () => {
    await clearQueueMutation.mutateAsync(queueName);
    setShowClearConfirm(false);
    router.push('/error-queues');
  };

  const getStatusColor = () => {
    switch (queue?.status) {
      case 'critical':
        return 'red';
      case 'warning':
        return 'yellow';
      case 'ok':
        return 'green';
      default:
        return 'gray';
    }
  };

  return (
    <Container size="xl" py="xl">
      <Stack gap="lg">
        {/* Header */}
        <Group justify="space-between" align="flex-start">
          <div>
            <Group gap="xs" mb="xs">
              <Button
                variant="subtle"
                leftSection={<IconArrowLeft size={16} />}
                onClick={() => router.push('/error-queues')}
              >
                Back to Error Queues
              </Button>
            </Group>
            <Title order={2}>{queue?.queueName}</Title>
          </div>
          <Badge size="lg" variant="light" color={getStatusColor()}>
            {queue?.status.toUpperCase()}
          </Badge>
        </Group>

        {/* Queue Info */}
        <Paper shadow="xs" p="lg" radius="md">
          <Grid>
            <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
              <InfoItem
                label="Total Messages"
                value={queue?.messageCount.toLocaleString() ?? '0'}
              />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
              <InfoItem label="Size" value={formatBytes(queue?.messageBytes ?? 0)} />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
              <InfoItem
                label="Oldest Message"
                value={
                  queue?.oldestMessageTimestamp
                    ? formatDateTime(queue.oldestMessageTimestamp)
                    : 'N/A'
                }
              />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
              <InfoItem
                label="Message Rate"
                value={`${queue?.messageRate.toFixed(1) ?? '0'}/min`}
              />
            </Grid.Col>
          </Grid>

          <Group mt="md">
            <Button
              leftSection={<IconRefresh size={16} />}
              variant="light"
              onClick={() => window.location.reload()}
            >
              Refresh
            </Button>
            <Button
              leftSection={<IconReload size={16} />}
              variant="filled"
              color="blue"
              onClick={() => setShowReplayConfirm(true)}
              disabled={queue?.messageCount === 0}
            >
              Replay All Messages
            </Button>
            <Button
              leftSection={<IconTrash size={16} />}
              variant="filled"
              color="red"
              onClick={() => setShowClearConfirm(true)}
              disabled={queue?.messageCount === 0}
            >
              Clear Queue
            </Button>
          </Group>
        </Paper>

        {/* Filters */}
        <Paper shadow="xs" p="md" radius="md">
          <Group>
            <TextInput
              placeholder="Search by message ID or correlation ID..."
              leftSection={<IconSearch size={16} />}
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.currentTarget.value)}
              style={{ flex: 1 }}
            />
            <Select
              placeholder="Filter by date"
              leftSection={<IconFilter size={16} />}
              value={dateFilter}
              onChange={setDateFilter}
              data={[
                { value: '1h', label: 'Last hour' },
                { value: '24h', label: 'Last 24 hours' },
                { value: '7d', label: 'Last 7 days' },
                { value: '30d', label: 'Last 30 days' },
              ]}
              clearable
              w={200}
            />
          </Group>
        </Paper>

        {/* Message List */}
        <MessageList
          queueName={queueName}
          page={page}
          pageSize={pageSize}
          searchQuery={searchQuery}
          dateFilter={dateFilter}
        />

        {/* Pagination */}
        {(queue?.messageCount ?? 0) > pageSize && (
          <Center>
            <Pagination
              value={page}
              onChange={setPage}
              total={Math.ceil((queue?.messageCount ?? 0) / pageSize)}
              boundaries={1}
              siblings={2}
            />
          </Center>
        )}
      </Stack>

      {/* Confirmation Modals */}
      <ConfirmationModal
        opened={showReplayConfirm}
        onClose={() => setShowReplayConfirm(false)}
        onConfirm={() => void handleReplayAll()}
        title="Replay All Messages"
        message={`Are you sure you want to replay all ${queue?.messageCount ?? 0} messages in this queue? They will be re-queued for processing.`}
        confirmText="Replay All"
        confirmColor="blue"
        loading={replayAllMutation.isPending}
      />

      <ConfirmationModal
        opened={showClearConfirm}
        onClose={() => setShowClearConfirm(false)}
        onConfirm={() => void handleClearQueue()}
        title="Clear Error Queue"
        message={`Are you sure you want to delete all ${queue?.messageCount ?? 0} messages from this queue? This action cannot be undone.`}
        confirmText="Clear Queue"
        confirmColor="red"
        loading={clearQueueMutation.isPending}
      />
    </Container>
  );
}