'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Card,
  LoadingOverlay,
  Alert,
  Select,
  ActionIcon,
  Tooltip,
} from '@mantine/core';
import {
  IconAlertCircle,
  IconRefresh,
  IconAlertTriangle,
  IconCircleX,
  IconKey,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { notifications } from '@mantine/notifications';
import { withAdminClient } from '@/lib/client/adminClient';
import { ProviderErrorDashboard } from '@/components/provider-errors/ProviderErrorDashboard';
import { ProviderErrorTable } from '@/components/provider-errors/ProviderErrorTable';
import { RecentErrorsList } from '@/components/provider-errors/RecentErrorsList';
import { useProviderErrors } from '@/hooks/useProviderErrors';

const TIME_WINDOWS = [
  { value: '1', label: 'Last 1 hour' },
  { value: '6', label: 'Last 6 hours' },
  { value: '24', label: 'Last 24 hours' },
  { value: '168', label: 'Last 7 days' },
];

export default function ProviderErrorsPage() {
  const [timeWindow, setTimeWindow] = useState('24');
  const [isRefreshing, setIsRefreshing] = useState(false);
  
  const {
    stats,
    summaries,
    recentErrors,
    isLoading,
    error,
    refresh,
  } = useProviderErrors(parseInt(timeWindow));

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await refresh();
    setIsRefreshing(false);
    notifications.show({
      title: 'Refreshed',
      message: 'Provider error data has been refreshed',
      color: 'teal',
    });
  };

  const handleClearErrors = async (keyId: number, reenableKey: boolean) => {
    try {
      await withAdminClient(client =>
        client.providerErrors.clearKeyErrors(keyId, {
          reEnableKey: reenableKey,
          confirmReenable: reenableKey,
          reason: 'Manual clear from error dashboard',
        })
      );

      notifications.show({
        title: 'Success',
        message: `Errors cleared${reenableKey ? ' and key re-enabled' : ''}`,
        color: 'teal',
      });

      await refresh();
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: 'Failed to clear errors',
        color: 'red',
      });
    }
  };

  return (
    <Stack>
      <LoadingOverlay visible={isLoading} />
      
      <Group justify="space-between" align="flex-end">
        <div>
          <Title order={2}>Provider Error Monitoring</Title>
          <Text size="sm" c="dimmed" mt={4}>
            Monitor and manage API provider errors and key health
          </Text>
        </div>
        
        <Group>
          <Select
            label="Time Window"
            value={timeWindow}
            onChange={(value) => setTimeWindow(value ?? '24')}
            data={TIME_WINDOWS}
            style={{ width: 160 }}
          />
          
          <Tooltip label="Refresh data">
            <ActionIcon
              variant="light"
              size="lg"
              onClick={handleRefresh}
              loading={isRefreshing}
              mt={24}
            >
              <IconRefresh size={20} />
            </ActionIcon>
          </Tooltip>
        </Group>
      </Group>

      {error && (
        <Alert
          icon={<IconAlertCircle size={16} />}
          title="Error loading data"
          color="red"
        >
          {error}
        </Alert>
      )}

      {!error && (
        <>
          <ProviderErrorDashboard stats={stats} />
          
          <Card>
            <Stack>
              <Group>
                <IconAlertTriangle size={20} />
                <Text fw={500}>Provider Summary</Text>
              </Group>
              <ProviderErrorTable 
                summaries={summaries}
                onClearErrors={handleClearErrors}
              />
            </Stack>
          </Card>

          <Card>
            <Stack>
              <Group>
                <IconCircleX size={20} />
                <Text fw={500}>Recent Errors</Text>
              </Group>
              <RecentErrorsList errors={recentErrors} />
            </Stack>
          </Card>
        </>
      )}
    </Stack>
  );
}