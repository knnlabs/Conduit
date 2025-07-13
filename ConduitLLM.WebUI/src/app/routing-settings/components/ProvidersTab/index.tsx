'use client';

import { useState, useEffect } from 'react';
import {
  Card,
  Stack,
  Group,
  Button,
  Text,
  Alert,
  Center,
  Loader,
  Title,
  SimpleGrid,
} from '@mantine/core';
import {
  IconRefresh,
  IconAlertCircle,
  IconServer,
  IconActivity,
  IconSettings,
} from '@tabler/icons-react';
import { ProviderPriorityList } from './ProviderPriorityList';
import { ProviderPriorityManager } from '../ProviderPriorityManager';
import { useProviderPriorities } from '../../hooks/useProviderPriorities';
import { ProviderPriority, RoutingConfiguration, LoadBalancerHealth } from '../../types/routing';

interface ProvidersTabProps {
  onLoadingChange: (loading: boolean) => void;
}

export function ProvidersTab({ onLoadingChange }: ProvidersTabProps) {
  const [providers, setProviders] = useState<ProviderPriority[]>([]);
  const [config, setConfig] = useState<RoutingConfiguration | null>(null);
  const [health, setHealth] = useState<LoadBalancerHealth | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);
  
  const {
    getProviderPriorities,
    updateProviderPriorities,
    getRoutingConfiguration,
    updateRoutingConfiguration,
    getLoadBalancerHealth,
    isLoading,
    error,
  } = useProviderPriorities();

  useEffect(() => {
    onLoadingChange(isLoading);
  }, [isLoading, onLoadingChange]);

  useEffect(() => {
    loadData();
  }, [refreshKey]);

  const loadData = async () => {
    try {
      const [providersData, configData, healthData] = await Promise.all([
        getProviderPriorities(),
        getRoutingConfiguration(),
        getLoadBalancerHealth().catch(() => null), // Health endpoint might not be available
      ]);
      setProviders(providersData);
      setConfig(configData);
      setHealth(healthData);
    } catch (err) {
      // Error is handled by the hook
    }
  };

  const handleRefresh = () => {
    setRefreshKey(prev => prev + 1);
  };

  const handleUpdateProviders = async (updatedProviders: ProviderPriority[]) => {
    try {
      await updateProviderPriorities(updatedProviders);
      setProviders(updatedProviders);
    } catch (err) {
      // Error is handled by the hook
    }
  };

  const handleUpdateConfig = async (updatedConfig: Partial<RoutingConfiguration>) => {
    try {
      const newConfig = await updateRoutingConfiguration(updatedConfig);
      setConfig(newConfig);
    } catch (err) {
      // Error is handled by the hook
    }
  };

  if (error) {
    return (
      <Alert icon={<IconAlertCircle size="1rem" />} title="Error" color="red">
        {error}
      </Alert>
    );
  }

  return (
    <Stack gap="md">
      {/* Header */}
      <Card shadow="sm" p="md" radius="md" withBorder>
        <Group justify="space-between" align="flex-start">
          <div>
            <Title order={4}>Provider Priority & Configuration</Title>
            <Text c="dimmed" size="sm" mt={4}>
              Manage provider priorities, routing strategies, and load balancing settings
            </Text>
          </div>
          <Button
            leftSection={<IconRefresh size={16} />}
            variant="subtle"
            onClick={handleRefresh}
            loading={isLoading}
          >
            Refresh
          </Button>
        </Group>
      </Card>

      {/* Configuration Overview */}
      {config && (
        <SimpleGrid cols={{ base: 1, sm: 2, md: 3 }} spacing="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <Group gap="sm">
              <IconSettings size={20} color="blue" />
              <div>
                <Text size="sm" fw={500}>Default Strategy</Text>
                <Text size="xs" c="dimmed" style={{ textTransform: 'capitalize' }}>
                  {config.defaultStrategy.replace('_', ' ')}
                </Text>
              </div>
            </Group>
          </Card>

          <Card shadow="sm" p="md" radius="md" withBorder>
            <Group gap="sm">
              <IconActivity size={20} color="green" />
              <div>
                <Text size="sm" fw={500}>Fallback</Text>
                <Text size="xs" c="dimmed">
                  {config.fallbackEnabled ? 'Enabled' : 'Disabled'}
                </Text>
              </div>
            </Group>
          </Card>

          <Card shadow="sm" p="md" radius="md" withBorder>
            <Group gap="sm">
              <IconServer size={20} color="orange" />
              <div>
                <Text size="sm" fw={500}>Timeout</Text>
                <Text size="xs" c="dimmed">
                  {config.timeoutMs}ms
                </Text>
              </div>
            </Group>
          </Card>
        </SimpleGrid>
      )}

      {/* Provider Priorities - New Enhanced Interface */}
      <ProviderPriorityManager
        onLoadingChange={onLoadingChange}
      />
    </Stack>
  );
}