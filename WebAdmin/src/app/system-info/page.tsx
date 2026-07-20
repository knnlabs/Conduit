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
  IconSettings,
} from '@tabler/icons-react';
import { useState, useEffect, useCallback } from 'react';
import { notify } from '@/lib/notifications';
import { SystemInfoDto, GlobalSettingDto, GlobalSettingCacheStats } from '@knn_labs/conduit-admin-client';
import { withAdminClient } from '@/lib/client/adminClient';
import { SystemOverviewTab } from './SystemOverviewTab';
import { SystemServicesTab } from './SystemServicesTab';
import { SystemEnvironmentTab } from './SystemEnvironmentTab';
import { SystemDependenciesTab } from './SystemDependenciesTab';
import { GlobalSettingsTab, type FunctionDiscoveryCacheStatistics } from './GlobalSettingsTab';
import { modals } from '@mantine/modals';




export default function SystemInfoPage() {
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [systemInfo, setSystemInfo] = useState<SystemInfoDto | null>(null);
  const [activeTab, setActiveTab] = useState<string | null>('overview');
  const [error, setError] = useState<string | null>(null);
  const [globalSettings, setGlobalSettings] = useState<GlobalSettingDto[]>([]);
  const [globalSettingsCacheStats, setGlobalSettingsCacheStats] = useState<GlobalSettingCacheStats | null>(null);
  const [functionDiscoveryCacheStats, setFunctionDiscoveryCacheStats] = useState<FunctionDiscoveryCacheStatistics | null>(null);
  const [isLoadingSettings, setIsLoadingSettings] = useState(false);

  const fetchSystemInfo = useCallback(async () => {
    try {
      setError(null);

      // Fetch system info (required)
      const systemData = await withAdminClient(client => client.system.getSystemInfo());
      setSystemInfo(systemData);

    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      console.error('Error fetching system info:', errorMessage);
      setError(errorMessage);
      notify.error(new Error(errorMessage));
    } finally {
      setIsLoading(false);
    }
  }, []);

  const fetchFunctionDiscoveryCache = useCallback(async () => {
    // Function discovery cache stats endpoint doesn't exist in SDK yet
    // Set to null until the backend endpoint is implemented
    setFunctionDiscoveryCacheStats(null);
  }, []);

  const fetchGlobalSettings = useCallback(async () => {
    setIsLoadingSettings(true);
    try {
      const result = await withAdminClient(client => client.settings.getGlobalSettings());
      setGlobalSettings(result.settings);

      // Fetch cache stats separately
      const stats = await withAdminClient(client => client.settings.getCacheStats());
      setGlobalSettingsCacheStats(stats);

      // Fetch function discovery cache stats
      await fetchFunctionDiscoveryCache();
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      console.error('Error fetching global settings:', errorMessage);
      notify.error(new Error(`Failed to load global settings: ${errorMessage}`));
    } finally {
      setIsLoadingSettings(false);
    }
  }, [fetchFunctionDiscoveryCache]);

  useEffect(() => {
    void fetchSystemInfo();
    void fetchGlobalSettings();
  }, [fetchSystemInfo, fetchGlobalSettings]);

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await fetchSystemInfo();
    setIsRefreshing(false);
    notify.success('System information updated', 'Refreshed');
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

    notify.success('System information exported successfully', 'Exported');
  };

  const handleUpdateSetting = async (id: number, value: string, description?: string) => {
    try {
      // Find the setting to get its key
      const setting = globalSettings.find(s => s.id === id);
      if (!setting) {
        throw new Error('Setting not found');
      }

      await withAdminClient(client =>
        client.settings.updateGlobalSetting(setting.key, value, description)
      );

      notify.success(`Setting "${setting.key}" updated successfully`);

      // Refresh settings
      await fetchGlobalSettings();
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      notify.error(new Error(`Failed to update setting: ${errorMessage}`));
      throw error;
    }
  };

  const handleCreateSetting = async (key: string, value: string, description?: string) => {
    try {
      await withAdminClient(client =>
        client.settings.createGlobalSetting({ key, value, description })
      );

      notify.success(`Setting "${key}" created successfully`);

      // Refresh settings
      await fetchGlobalSettings();
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      notify.error(new Error(`Failed to create setting: ${errorMessage}`));
      throw error;
    }
  };

  const handleDeleteSetting = async (id: number, key: string) => {
    modals.openConfirmModal({
      title: 'Delete Global Setting',
      children: (
        <Text size="sm">
          Are you sure you want to delete the setting <strong>{key}</strong>?
          <br /><br />
          This action cannot be undone and will affect all services using this setting.
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => {
        void (async () => {
          try {
            await withAdminClient(client => client.settings.deleteGlobalSetting(key));

            notify.success(`Setting "${key}" deleted successfully`);

            // Refresh settings
            await fetchGlobalSettings();
          } catch (error) {
            const errorMessage = error instanceof Error ? error.message : 'Unknown error';
            notify.error(new Error(`Failed to delete setting: ${errorMessage}`));
          }
        })();
      },
    });
  };

  const handleReloadCache = async () => {
    try {
      await withAdminClient(client => client.settings.reloadCache());

      notify.success('Cache reloaded successfully');

      // Refresh cache stats
      const stats = await withAdminClient(client => client.settings.getCacheStats());
      setGlobalSettingsCacheStats(stats);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      notify.error(new Error(`Failed to reload cache: ${errorMessage}`));
      throw error;
    }
  };

  const handleInvalidateFunctionDiscoveryCache = async () => {
    try {
      await withAdminClient(client => client.system.invalidateDiscoveryCache());

      notify.success('Function discovery cache invalidated successfully');

      // Refresh cache stats
      await fetchFunctionDiscoveryCache();
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      notify.error(new Error(`Failed to invalidate function discovery cache: ${errorMessage}`));
      throw error;
    }
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
                {systemInfo?.operatingSystem?.description ?? 'Unknown'}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                {systemInfo?.operatingSystem?.architecture ?? 'Unknown Architecture'}
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
                {systemInfo?.runtime?.runtimeVersion ?? 'Unknown'}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                OS: {systemInfo?.operatingSystem?.description ?? 'Unknown'}
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
                {systemInfo?.runtime?.uptime ?? 'Unknown'}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                Version: {systemInfo?.version?.appVersion ?? 'Unknown'}
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
                {systemInfo?.database?.connected ? 'Connected' : 'Disconnected'}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                {systemInfo?.database?.provider ?? 'Unknown Provider'}
              </Text>
            </div>
            <ThemeIcon 
              color={systemInfo?.database?.connected ? 'green' : 'red'}
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
          <Tabs.Tab value="settings" leftSection={<IconSettings size={16} />}>
            Global Settings
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

        <Tabs.Panel value="settings" pt="md">
          <GlobalSettingsTab
            settings={globalSettings}
            cacheStats={globalSettingsCacheStats}
            functionDiscoveryCacheStats={functionDiscoveryCacheStats}
            onUpdate={handleUpdateSetting}
            onCreate={handleCreateSetting}
            onDelete={handleDeleteSetting}
            onReloadCache={handleReloadCache}
            onInvalidateFunctionDiscoveryCache={handleInvalidateFunctionDiscoveryCache}
            isLoading={isLoadingSettings}
          />
        </Tabs.Panel>
      </Tabs>

      <LoadingOverlay visible={isRefreshing} />
    </Stack>
  );
}
