'use client';

import { useState } from 'react';
import {
  Card,
  Stack,
  Group,
  Text,
  Badge,
  NumberInput,
  Switch,
  Button,
  Select,
  Title,
  Progress,
} from '@mantine/core';
import {
  IconGripVertical,
  IconDeviceFloppy,
  IconActivity,
} from '@tabler/icons-react';
import { DragDropContext, Droppable, Draggable } from '@hello-pangea/dnd';
import { notifications } from '@mantine/notifications';
import { ProviderPriority, RoutingConfiguration, LoadBalancerHealth } from '../../types/routing';

interface ProviderPriorityListProps {
  providers: ProviderPriority[];
  config: RoutingConfiguration | null;
  health: LoadBalancerHealth | null;
  onUpdateProviders: (providers: ProviderPriority[]) => void;
  onUpdateConfig: (config: Partial<RoutingConfiguration>) => void;
}

export function ProviderPriorityList({
  providers,
  config,
  health,
  onUpdateProviders,
  onUpdateConfig,
}: ProviderPriorityListProps) {
  const [localProviders, setLocalProviders] = useState<ProviderPriority[]>(providers);
  const [localConfig, setLocalConfig] = useState<Partial<RoutingConfiguration>>(config ?? {});
  const [hasChanges, setHasChanges] = useState(false);

  const handleProviderChange = (index: number, field: keyof ProviderPriority, value: number | boolean | string) => {
    const updated = localProviders.map((provider, i) =>
      i === index ? { ...provider, [field]: value } : provider
    );
    setLocalProviders(updated);
    setHasChanges(true);
  };

  const handleConfigChange = (field: keyof RoutingConfiguration, value: number | boolean | string) => {
    const updated = { ...localConfig, [field]: value };
    setLocalConfig(updated);
    setHasChanges(true);
  };

  const handleDragEnd = (result: { destination?: { index: number } | null; source: { index: number } }) => {
    if (!result.destination) return;

    const items = Array.from(localProviders);
    const [reorderedItem] = items.splice(result.source.index, 1);
    items.splice(result.destination.index, 0, reorderedItem);

    // Update priorities based on new order
    const updated = items.map((provider, index) => ({
      ...provider,
      priority: items.length - index,
    }));

    setLocalProviders(updated);
    setHasChanges(true);
  };

  const handleSave = async () => {
    try {
      onUpdateProviders(localProviders);
      if (Object.keys(localConfig).length > 0) {
        onUpdateConfig(localConfig);
      }
      setHasChanges(false);
      notifications.show({
        title: 'Success',
        message: 'Provider settings saved successfully',
        color: 'green',
      });
    } catch {
      // Error handling is done in the hook
    }
  };

  const handleReset = () => {
    setLocalProviders(providers);
    setLocalConfig(config ?? {});
    setHasChanges(false);
  };

  const getHealthStatus = (providerId: string) => {
    if (!health) return null;
    const node = health.nodes.find(n => n.id === providerId);
    return node?.status ?? null;
  };

  const getHealthColor = (status: string | null) => {
    switch (status) {
      case 'healthy': return 'green';
      case 'unhealthy': return 'red';
      case 'draining': return 'yellow';
      default: return 'gray';
    }
  };

  return (
    <Stack gap="md">
      {/* Configuration Settings */}
      <Card shadow="sm" p="md" radius="md" withBorder>
        <Title order={5} mb="md">Routing Configuration</Title>
        <Group grow>
          <Select
            label="Default Strategy"
            data={[
              { value: 'round_robin', label: 'Round Robin' },
              { value: 'least_latency', label: 'Least Latency' },
              { value: 'cost_optimized', label: 'Cost Optimized' },
              { value: 'priority', label: 'Priority Based' },
            ]}
            value={localConfig.defaultStrategy ?? config?.defaultStrategy}
            onChange={(value) => value && handleConfigChange('defaultStrategy', value)}
          />
          <div>
            <Text size="sm" fw={500} mb="xs">Fallback Enabled</Text>
            <Switch
              checked={localConfig.fallbackEnabled ?? config?.fallbackEnabled ?? false}
              onChange={(e) => handleConfigChange('fallbackEnabled', e.target.checked)}
            />
          </div>
        </Group>
        <Group grow mt="md">
          <NumberInput
            label="Timeout (ms)"
            value={localConfig.timeoutMs ?? config?.timeoutMs}
            onChange={(value) => handleConfigChange('timeoutMs', Number(value))}
            min={1000}
            max={60000}
            step={1000}
          />
          <NumberInput
            label="Max Concurrent Requests"
            value={localConfig.maxConcurrentRequests ?? config?.maxConcurrentRequests}
            onChange={(value) => handleConfigChange('maxConcurrentRequests', Number(value))}
            min={1}
            max={1000}
          />
        </Group>
      </Card>

      {/* Provider Priorities */}
      <Card shadow="sm" p="md" radius="md" withBorder>
        <Group justify="space-between" mb="md">
          <Title order={5}>Provider Priorities</Title>
          {hasChanges && (
            <Group>
              <Button variant="subtle" onClick={handleReset}>
                Reset
              </Button>
              <Button leftSection={<IconDeviceFloppy size={16} />} onClick={() => void handleSave()}>
                Save Changes
              </Button>
            </Group>
          )}
        </Group>

        <DragDropContext onDragEnd={handleDragEnd}>
          <Droppable droppableId="providers">
            {(provided) => (
              <div {...provided.droppableProps} ref={provided.innerRef}>
                <Stack gap="sm">
                  {localProviders.map((provider, index) => {
                    const healthStatus = getHealthStatus(provider.providerId);
                    return (
                      <Draggable
                        key={provider.providerId}
                        draggableId={provider.providerId}
                        index={index}
                      >
                        {(provided) => (
                          <Card
                            ref={provided.innerRef}
                            {...provided.draggableProps}
                            withBorder
                            p="md"
                          >
                            <Group justify="space-between" align="center">
                              <Group>
                                <div {...provided.dragHandleProps}>
                                  <IconGripVertical size={16} color="gray" />
                                </div>
                                <div>
                                  <Group align="center" gap="xs">
                                    <Text fw={500}>{provider.providerName}</Text>
                                    {healthStatus && (
                                      <Badge
                                        variant="light"
                                        color={getHealthColor(healthStatus)}
                                        size="xs"
                                      >
                                        {healthStatus}
                                      </Badge>
                                    )}
                                    <Badge
                                      variant="light"
                                      color={provider.isEnabled ? 'green' : 'gray'}
                                      size="xs"
                                    >
                                      {provider.isEnabled ? 'Active' : 'Disabled'}
                                    </Badge>
                                  </Group>
                                  <Text size="xs" c="dimmed">
                                    ID: {provider.providerId}
                                  </Text>
                                </div>
                              </Group>

                              <Group>
                                <NumberInput
                                  label="Priority"
                                  value={provider.priority}
                                  onChange={(value) =>
                                    handleProviderChange(index, 'priority', Number(value))
                                  }
                                  min={1}
                                  max={100}
                                  w={80}
                                  size="xs"
                                />
                                {provider.weight !== undefined && (
                                  <NumberInput
                                    label="Weight"
                                    value={provider.weight}
                                    onChange={(value) =>
                                      handleProviderChange(index, 'weight', Number(value))
                                    }
                                    min={1}
                                    max={100}
                                    w={80}
                                    size="xs"
                                  />
                                )}
                                <Switch
                                  label="Enabled"
                                  checked={provider.isEnabled}
                                  onChange={(e) =>
                                    handleProviderChange(index, 'isEnabled', e.target.checked)
                                  }
                                  size="sm"
                                />
                              </Group>
                            </Group>

                            {health?.distribution[provider.providerId] && (
                              <div style={{ marginTop: 8 }}>
                                <Text size="xs" c="dimmed" mb={4}>
                                  Traffic Distribution: {health.distribution[provider.providerId]}%
                                </Text>
                                <Progress
                                  value={health.distribution[provider.providerId]}
                                  size="xs"
                                  color={getHealthColor(healthStatus)}
                                />
                              </div>
                            )}
                          </Card>
                        )}
                      </Draggable>
                    );
                  })}
                  {provided.placeholder}
                </Stack>
              </div>
            )}
          </Droppable>
        </DragDropContext>

        {localProviders.length === 0 && (
          <Text c="dimmed" ta="center" py="xl">
            No providers configured
          </Text>
        )}
      </Card>

      {/* Health Status */}
      {health && (
        <Card shadow="sm" p="md" radius="md" withBorder>
          <Title order={5} mb="md">Load Balancer Health</Title>
          <Group align="center" mb="md">
            <Badge
              variant="light"
              color={getHealthColor(health.status)}
              leftSection={<IconActivity size={12} />}
            >
              {health.status.toUpperCase()}
            </Badge>
            <Text size="xs" c="dimmed">
              Last check: {new Date(health.lastCheck).toLocaleString()}
            </Text>
          </Group>

          <Stack gap="xs">
            {health.nodes.map((node) => (
              <Group key={node.id} justify="space-between" p="xs">
                <Group>
                  <Badge
                    variant="dot"
                    color={getHealthColor(node.status)}
                    size="sm"
                  >
                    {node.endpoint}
                  </Badge>
                </Group>
                <Group gap="md">
                  <Text size="xs" c="dimmed">
                    {node.totalRequests} requests
                  </Text>
                  <Text size="xs" c="dimmed">
                    {node.avgResponseTime}ms avg
                  </Text>
                  <Text size="xs" c="dimmed">
                    {node.activeConnections} active
                  </Text>
                </Group>
              </Group>
            ))}
          </Stack>
        </Card>
      )}
    </Stack>
  );
}