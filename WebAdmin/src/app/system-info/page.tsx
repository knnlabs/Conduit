'use client';

import {
  Stack,
  Title,
  Text,
  Card,
  Group,
  Button,
  ThemeIcon,
  SimpleGrid,
  LoadingOverlay,
  Alert,
  Tabs,
} from '@mantine/core';
import {
  IconServer,
  IconDatabase,
  IconBrandDocker,
  IconRefresh,
  IconDownload,
  IconAlertTriangle,
  IconClock,
  IconLock,
  IconPackage,
  IconBolt,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { notifications } from '@mantine/notifications';
import { SystemInfoDto } from '@knn_labs/conduit-admin-client';
import { withAdminClient } from '@/lib/client/adminClient';
import { formatUptime } from './helpers';
import { SystemOverviewTab } from './SystemOverviewTab';
import { SystemServicesTab } from './SystemServicesTab';
import { SystemEnvironmentTab } from './SystemEnvironmentTab';
import { SystemDependenciesTab } from './SystemDependenciesTab';




export default function SystemInfoPage() {
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [systemInfo, setSystemInfo] = useState<SystemInfoDto | null>(null);
  const [activeTab, setActiveTab] = useState<string | null>('overview');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void fetchSystemInfo();
  }, []);

  const fetchSystemInfo = async () => {
    try {
      setError(null);
      
      const data = await withAdminClient(client => 
        client.system.getSystemInfo()
      );
      
      setSystemInfo(data);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      console.error('Error fetching system info:', errorMessage);
      setError(errorMessage);
      notifications.show({
        title: 'Error',
        message: errorMessage,
        color: 'red',
      });
    } finally {
      setIsLoading(false);
    }
  };

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await fetchSystemInfo();
    setIsRefreshing(false);
    notifications.show({
      title: 'Refreshed',
      message: 'System information updated',
      color: 'green',
    });
  };

  const handleExport = () => {
    const exportData = {
      timestamp: new Date().toISOString(),
      system: systemInfo,
    };
    
    const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `system-info-${new Date().toISOString()}.json`;
    a.click();
    URL.revokeObjectURL(url);
    
    notifications.show({
      title: 'Exported',
      message: 'System information exported successfully',
      color: 'green',
    });
  };

  if (isLoading) {
    return (
      <Stack>
        <Card shadow="sm" p="md" radius="md" pos="relative" mih={200}>
          <LoadingOverlay visible={true} />
        </Card>
      </Stack>
    );
  }

  if (error && !systemInfo) {
    return (
      <Stack gap="xl">
        <Card shadow="sm" p="md" radius="md">
          <Alert
            icon={<IconAlertTriangle size={16} />}
            title="Failed to load system information"
            color="red"
          >
            {error}
          </Alert>
        </Card>
      </Stack>
    );
  }

  return (
    <Stack gap="xl">
      <Card shadow="sm" p="md" radius="md">
        <Group justify="space-between" align="center">
          <div>
            <Title order={2}>System Information</Title>
            <Text size="sm" c="dimmed" mt={4}>
              Runtime environment and service configuration
            </Text>
          </div>
          <Group>
            <Button
              variant="light"
              leftSection={<IconRefresh size={16} />}
              onClick={() => void handleRefresh()}
              loading={isRefreshing}
            >
              Refresh
            </Button>
            <Button
              variant="filled"
              leftSection={<IconDownload size={16} />}
              onClick={handleExport}
            >
              Export
            </Button>
          </Group>
        </Group>
      </Card>

      <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }} spacing="md">
        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Platform
              </Text>
              <Text size="xl" fw={700} mt={4}>
                {systemInfo?.runtime?.os ?? 'Unknown'}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                {systemInfo?.runtime?.architecture ?? 'Unknown Architecture'}
              </Text>
            </div>
            <ThemeIcon color="blue" variant="light" size={48} radius="md">
              <IconBrandDocker size={24} />
            </ThemeIcon>
          </Group>
        </Card>

        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                .NET Runtime
              </Text>
              <Text size="xl" fw={700} mt={4}>
                {systemInfo?.runtime?.dotnetVersion ?? 'Unknown'}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                Environment: {systemInfo?.environment ?? 'Unknown'}
              </Text>
            </div>
            <ThemeIcon color="green" variant="light" size={48} radius="md">
              <IconPackage size={24} />
            </ThemeIcon>
          </Group>
        </Card>

        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                System Uptime
              </Text>
              <Text size="xl" fw={700} mt={4}>
                {systemInfo?.uptime ? formatUptime(systemInfo.uptime) : 'Unknown'}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                Version: {systemInfo?.version ?? 'Unknown'}
              </Text>
            </div>
            <ThemeIcon color="teal" variant="light" size={48} radius="md">
              <IconClock size={24} />
            </ThemeIcon>
          </Group>
        </Card>

        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Database
              </Text>
              <Text size="xl" fw={700} mt={4}>
                {systemInfo?.database?.isConnected ? 'Connected' : 'Disconnected'}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                {systemInfo?.database?.provider ?? 'Unknown Provider'}
              </Text>
            </div>
            <ThemeIcon 
              color={systemInfo?.database?.isConnected ? 'green' : 'red'} 
              variant="light" 
              size={48} 
              radius="md"
            >
              <IconDatabase size={24} />
            </ThemeIcon>
          </Group>
        </Card>
      </SimpleGrid>

      <Tabs value={activeTab} onChange={setActiveTab}>
        <Tabs.List>
          <Tabs.Tab value="overview" leftSection={<IconServer size={16} />}>
            Overview
          </Tabs.Tab>
          <Tabs.Tab value="services" leftSection={<IconBolt size={16} />}>
            Services
          </Tabs.Tab>
          <Tabs.Tab value="environment" leftSection={<IconLock size={16} />}>
            Environment
          </Tabs.Tab>
          <Tabs.Tab value="dependencies" leftSection={<IconPackage size={16} />}>
            Dependencies
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="overview" pt="md">
          <SystemOverviewTab systemInfo={systemInfo} />
        </Tabs.Panel>

        <Tabs.Panel value="services" pt="md">
          <SystemServicesTab systemInfo={systemInfo} />
        </Tabs.Panel>

        <Tabs.Panel value="environment" pt="md">
          <SystemEnvironmentTab systemInfo={systemInfo} />
        </Tabs.Panel>

        <Tabs.Panel value="dependencies" pt="md">
          <SystemDependenciesTab systemInfo={systemInfo} />
        </Tabs.Panel>
      </Tabs>

      <LoadingOverlay visible={isRefreshing} />
    </Stack>
  );
}