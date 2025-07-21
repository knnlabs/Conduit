'use client';

import { useState, useEffect, useCallback } from 'react';
import {
  Card,
  Stack,
  Group,
  Text,
  Title,
  TextInput,
  Button,
  Alert,
  Center,
  Loader,
} from '@mantine/core';
import {
  IconSearch,
  IconRefresh,
  IconAlertCircle,
  IconDeviceFloppy,
  IconX,
} from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import { ProviderPriority } from '../../types/routing';
import { ProviderList } from './components/ProviderList';
import { BulkActions } from './components/BulkActions';
import { ProviderStats } from './components/ProviderStats';
import { useProviderPriorities } from '../../hooks/useProviderPriorities';

interface ProviderDisplay extends ProviderPriority {
  statistics: {
    usagePercentage: number;
    successRate: number;
    avgResponseTime: number;
  };
  type: 'primary' | 'backup' | 'special';
}

interface ProviderPriorityManagerProps {
  onLoadingChange: (loading: boolean) => void;
}

export function ProviderPriorityManager({ onLoadingChange }: ProviderPriorityManagerProps) {
  const [providers, setProviders] = useState<ProviderDisplay[]>([]);
  const [originalProviders, setOriginalProviders] = useState<ProviderDisplay[]>([]);
  const [filteredProviders, setFilteredProviders] = useState<ProviderDisplay[]>([]);
  const [hasChanges, setHasChanges] = useState(false);
  const [filter, setFilter] = useState('');
  const [refreshKey, setRefreshKey] = useState(0);

  const {
    getProviderPriorities,
    updateProviderPriorities,
    getLoadBalancerHealth,
    isLoading,
    error,
  } = useProviderPriorities();

  useEffect(() => {
    onLoadingChange(isLoading);
  }, [isLoading, onLoadingChange]);

  const loadData = useCallback(async () => {
    try {
      const [providersData, healthData, providerHealthData] = await Promise.all([
        getProviderPriorities(),
        getLoadBalancerHealth().catch(() => null),
        fetch('/api/health/providers')
          .then(res => res.json() as Promise<Array<{
            id: string;
            name: string;
            status: string;
            lastChecked: string;
            responseTime: number;
            uptime: number;
            errorRate: number;
            successRate: number;
            details: unknown;
          }>>)
          .catch(() => []),
      ]);

      // Create a map of provider health data
      const healthMap = new Map(
        providerHealthData.map(h => [h.id, h])
      );

      // Transform provider data to include statistics and type
      const providersWithStats: ProviderDisplay[] = providersData.map(provider => {
        const health = healthMap.get(provider.providerId);
        
        return {
          ...provider,
          statistics: {
            usagePercentage: healthData?.distribution[provider.providerId] ?? 0,
            successRate: typeof health?.uptime === 'number' ? health.uptime : 0,
            avgResponseTime: typeof health?.responseTime === 'number' ? health.responseTime : 0,
          },
          type: determineProviderType(provider.providerName),
        };
      });

      setProviders(providersWithStats);
      setOriginalProviders(JSON.parse(JSON.stringify(providersWithStats)) as ProviderDisplay[]);
      setHasChanges(false);
    } catch {
      // Error is handled by the hook
    }
  }, [getProviderPriorities, getLoadBalancerHealth]);

  useEffect(() => {
    void loadData();
  }, [refreshKey, loadData]);

  useEffect(() => {
    // Filter providers based on search term
    if (!filter.trim()) {
      setFilteredProviders(providers);
    } else {
      const filtered = providers.filter(provider => 
        provider.providerName.toLowerCase().includes(filter.toLowerCase()) ||
        provider.providerId.toLowerCase().includes(filter.toLowerCase()) ||
        provider.type.toLowerCase().includes(filter.toLowerCase())
      );
      setFilteredProviders(filtered);
    }
  }, [providers, filter]);


  const determineProviderType = (providerName: string): 'primary' | 'backup' | 'special' => {
    const name = providerName.toLowerCase();
    if (name.includes('primary') || name.includes('openai') || name.includes('anthropic')) {
      return 'primary';
    }
    if (name.includes('backup') || name.includes('azure') || name.includes('fallback')) {
      return 'backup';
    }
    return 'special';
  };

  const handleProviderUpdate = useCallback((index: number, updates: Partial<ProviderDisplay>) => {
    setProviders(prev => {
      const updated = [...prev];
      updated[index] = { ...updated[index], ...updates };
      return updated;
    });
    setHasChanges(true);
  }, []);

  const handleBulkAction = useCallback((action: 'enable-all' | 'disable-all' | 'reset') => {
    switch (action) {
      case 'enable-all':
        setProviders(prev => prev.map(p => ({ ...p, isEnabled: true })));
        setHasChanges(true);
        break;
      case 'disable-all': {
        const enabledCount = providers.filter(p => p.isEnabled).length;
        if (enabledCount <= 1) {
          notifications.show({
            title: 'Action Prevented',
            message: 'At least one provider must remain enabled',
            color: 'orange',
          });
          return;
        }
        setProviders(prev => prev.map(p => ({ ...p, isEnabled: false })));
        setHasChanges(true);
        break;
      }
      case 'reset':
        setProviders(JSON.parse(JSON.stringify(originalProviders)) as ProviderDisplay[]);
        setHasChanges(false);
        break;
    }
  }, [providers, originalProviders]);

  const handleSave = async () => {
    try {
      // Validate priorities are unique
      const priorities = providers.map(p => p.priority);
      const uniquePriorities = new Set(priorities);
      if (priorities.length !== uniquePriorities.size) {
        notifications.show({
          title: 'Validation Error',
          message: 'All provider priorities must be unique',
          color: 'red',
        });
        return;
      }

      // Ensure at least one provider is enabled
      const enabledProviders = providers.filter(p => p.isEnabled);
      if (enabledProviders.length === 0) {
        notifications.show({
          title: 'Validation Error',
          message: 'At least one provider must be enabled',
          color: 'red',
        });
        return;
      }

      const providersData: ProviderPriority[] = providers.map(({ statistics, type, ...provider }) => {
        // eslint-disable-next-line @typescript-eslint/no-unused-vars
        const unusedStatistics = statistics;
        // eslint-disable-next-line @typescript-eslint/no-unused-vars
        const unusedType = type;
        return provider;
      });
      await updateProviderPriorities(providersData);
      setOriginalProviders(JSON.parse(JSON.stringify(providers)) as ProviderDisplay[]);
      setHasChanges(false);
      
      notifications.show({
        title: 'Success',
        message: 'Provider priorities saved successfully',
        color: 'green',
      });
    } catch {
      // Error is handled by the hook
    }
  };

  const handleCancel = () => {
    setProviders(JSON.parse(JSON.stringify(originalProviders)) as ProviderDisplay[]);
    setHasChanges(false);
  };

  const handleRefresh = () => {
    setRefreshKey(prev => prev + 1);
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
            <Title order={4}>Provider Priority Management</Title>
            <Text c="dimmed" size="sm" mt={4}>
              Configure provider priorities, enable/disable providers, and monitor routing statistics
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

      {/* Statistics Overview */}
      <ProviderStats providers={providers} />

      {/* Search and Bulk Actions */}
      <Card shadow="sm" p="md" radius="md" withBorder>
        <Group justify="space-between" align="center">
          <TextInput
            placeholder="Search providers..."
            leftSection={<IconSearch size={16} />}
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
            rightSection={
              filter && (
                <Button
                  variant="subtle"
                  size="xs"
                  onClick={() => setFilter('')}
                  p={0}
                >
                  <IconX size={14} />
                </Button>
              )
            }
            style={{ flex: 1, maxWidth: 300 }}
          />
          <BulkActions
            providers={providers}
            onAction={handleBulkAction}
            disabled={isLoading}
          />
        </Group>
      </Card>

      {/* Provider List */}
      {(() => {
        if (isLoading && providers.length === 0) {
          return (
            <Center h={300}>
              <Loader />
            </Center>
          );
        }
        
        if (filteredProviders.length === 0) {
          return (
            <Card shadow="sm" p="xl" radius="md" withBorder>
              <Center h={200}>
                <Stack align="center" gap="md">
                  <Text size="lg" fw={500}>
                    {filter ? 'No providers match your search' : 'No providers configured'}
                  </Text>
                  <Text c="dimmed" size="sm">
                    {filter 
                      ? 'Try adjusting your search terms'
                      : 'Provider priorities will appear here once you configure LLM providers'
                    }
                  </Text>
                </Stack>
              </Center>
            </Card>
          );
        }
        
        return (
          <ProviderList
            providers={filteredProviders}
            originalProviders={providers}
            onProviderUpdate={handleProviderUpdate}
            isLoading={isLoading}
          />
        );
      })()}

      {/* Save/Cancel Actions */}
      {hasChanges && (
        <Card shadow="sm" p="md" radius="md" withBorder bg="blue.0">
          <Group justify="space-between" align="center">
            <div>
              <Text fw={500} size="sm">Unsaved Changes</Text>
              <Text size="xs" c="dimmed">
                You have modified provider priorities. Save your changes to apply them.
              </Text>
            </div>
            <Group>
              <Button variant="subtle" onClick={handleCancel}>
                Cancel
              </Button>
              <Button 
                leftSection={<IconDeviceFloppy size={16} />}
                onClick={() => void handleSave()}
                loading={isLoading}
              >
                Save Changes
              </Button>
            </Group>
          </Group>
        </Card>
      )}
    </Stack>
  );
}