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
import { useState, useEffect } from 'react';
import { notifications } from '@mantine/notifications';
import { SystemInfoDto, LLMCacheControlDto, GlobalSettingDto, GlobalSettingCacheStats, FunctionDiscoveryCacheStatistics } from '@knn_labs/conduit-admin-client';
import { withAdminClient } from '@/lib/client/adminClient';
import { formatUptime } from './helpers';
import { SystemOverviewTab } from './SystemOverviewTab';
import { SystemServicesTab } from './SystemServicesTab';
import { SystemEnvironmentTab } from './SystemEnvironmentTab';
import { SystemDependenciesTab } from './SystemDependenciesTab';
import { GlobalSettingsTab } from './GlobalSettingsTab';
import { modals } from '@mantine/modals';




export default function SystemInfoPage() {
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [systemInfo, setSystemInfo] = useState<SystemInfoDto | null>(null);
  const [cacheStatus, setCacheStatus] = useState<LLMCacheControlDto | null>(null);
  const [isTogglingCache, setIsTogglingCache] = useState(false);
  const [activeTab, setActiveTab] = useState<string | null>('overview');
  const [error, setError] = useState<string | null>(null);
  const [globalSettings, setGlobalSettings] = useState<GlobalSettingDto[]>([]);
  const [globalSettingsCacheStats, setGlobalSettingsCacheStats] = useState<GlobalSettingCacheStats | null>(null);
  const [functionDiscoveryCacheStats, setFunctionDiscoveryCacheStats] = useState<FunctionDiscoveryCacheStatistics | null>(null);
  const [isLoadingSettings, setIsLoadingSettings] = useState(false);

  useEffect(() => {
    void fetchSystemInfo();
    void fetchGlobalSettings();
  }, []);

  const fetchSystemInfo = async () => {
    try {
      setError(null);

      // Fetch system info (required)
      const systemData = await withAdminClient(client => client.system.getSystemInfo());
      setSystemInfo(systemData);

      // Fetch cache status separately (optional, non-blocking)
      withAdminClient(client => client.configuration.getLLMCacheStatus())
        .then(setCacheStatus)
        .catch((error) => {
          // Silently fail for cache status - it's optional
          console.warn('Failed to fetch LLM cache status:', error);
          setCacheStatus(null);
        });

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

  const handleCacheToggle = (newValue: boolean) => {
    const action = newValue ? 'enable' : 'disable';

    modals.openConfirmModal({
      title: `${action === 'enable' ? 'Enable' : 'Disable'} LLM Cache`,
      children: (
        <Text size="sm">
          Are you sure you want to {action} LLM response caching?
          {!newValue && (
            <>
              <br /><br />
              This will:
              <br />• Increase latency for repeated requests
              <br />• Increase provider API costs
              <br />• Apply to all Core API instances immediately
            </>
          )}
        </Text>
      ),
      labels: { confirm: 'Confirm', cancel: 'Cancel' },
      confirmProps: { color: newValue ? 'green' : 'red' },
      onConfirm: () => {
        void (async () => {
          setIsTogglingCache(true);
          try {
            const updatedStatus = await withAdminClient(client =>
              client.configuration.toggleLLMCache({ enabled: newValue })
            );

            setCacheStatus(updatedStatus);

          notifications.show({
            title: 'Success',
            message: `LLM cache ${newValue ? 'enabled' : 'disabled'} successfully`,
            color: 'green',
          });
        } catch (error) {
          const errorMessage = error instanceof Error ? error.message : 'Unknown error';
          console.error('Error toggling cache:', errorMessage);

          notifications.show({
            title: 'Error',
            message: `Failed to ${action} cache: ${errorMessage}`,
            color: 'red',
          });
        } finally {
          setIsTogglingCache(false);
        }
        })();
      },
    });
  };

  const fetchGlobalSettings = async () => {
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
      notifications.show({
        title: 'Error',
        message: `Failed to load global settings: ${errorMessage}`,
        color: 'red',
      });
    } finally {
      setIsLoadingSettings(false);
    }
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

      notifications.show({
        title: 'Success',
        message: `Setting "${setting.key}" updated successfully`,
        color: 'green',
      });

      // Refresh settings
      await fetchGlobalSettings();
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      notifications.show({
        title: 'Error',
        message: `Failed to update setting: ${errorMessage}`,
        color: 'red',
      });
      throw error;
    }
  };

  const handleCreateSetting = async (key: string, value: string, description?: string) => {
    try {
      await withAdminClient(client =>
        client.settings.createGlobalSetting({ key, value, description })
      );

      notifications.show({
        title: 'Success',
        message: `Setting "${key}" created successfully`,
        color: 'green',
      });

      // Refresh settings
      await fetchGlobalSettings();
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      notifications.show({
        title: 'Error',
        message: `Failed to create setting: ${errorMessage}`,
        color: 'red',
      });
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

            notifications.show({
              title: 'Success',
              message: `Setting "${key}" deleted successfully`,
              color: 'green',
            });

            // Refresh settings
            await fetchGlobalSettings();
          } catch (error) {
            const errorMessage = error instanceof Error ? error.message : 'Unknown error';
            notifications.show({
              title: 'Error',
              message: `Failed to delete setting: ${errorMessage}`,
              color: 'red',
            });
          }
        })();
      },
    });
  };

  const handleReloadCache = async () => {
    try {
      await withAdminClient(client => client.settings.reloadCache());

      notifications.show({
        title: 'Success',
        message: 'Cache reloaded successfully',
        color: 'green',
      });

      // Refresh cache stats
      const stats = await withAdminClient(client => client.settings.getCacheStats());
      setGlobalSettingsCacheStats(stats);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      notifications.show({
        title: 'Error',
        message: `Failed to reload cache: ${errorMessage}`,
        color: 'red',
      });
      throw error;
    }
  };

  const fetchFunctionDiscoveryCache = async () => {
    try {
      const stats = await withAdminClient(client => client.system.getFunctionDiscoveryCacheStats());
      setFunctionDiscoveryCacheStats(stats);
    } catch (error) {
      // Silently fail - cache stats are optional
      console.warn('Failed to fetch function discovery cache stats:', error);
      setFunctionDiscoveryCacheStats(null);
    }
  };

  const handleInvalidateFunctionDiscoveryCache = async () => {
    try {
      await withAdminClient(client => client.system.invalidateFunctionDiscoveryCache());

      notifications.show({
        title: 'Success',
        message: 'Function discovery cache invalidated successfully',
        color: 'green',
      });

      // Refresh cache stats
      await fetchFunctionDiscoveryCache();
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      notifications.show({
        title: 'Error',
        message: `Failed to invalidate function discovery cache: ${errorMessage}`,
        color: 'red',
      });
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
          <Tabs.Tab value="settings" leftSection={<IconSettings size={16} />}>
            Global Settings
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="overview" pt="md">
          <SystemOverviewTab
            systemInfo={systemInfo}
            cacheStatus={cacheStatus}
            onCacheToggle={handleCacheToggle}
            isTogglingCache={isTogglingCache}
          />
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