'use client';

import {
  Card,
  Stack,
  Group,
  Text,
  Title,
  Badge,
  ThemeIcon,
  Alert,
  Progress,
  Table,
} from '@mantine/core';
import {
  IconTarget,
  IconArrowRight,
  IconServer,
  IconInfoCircle,
  IconAlertTriangle,
  IconShield,
} from '@tabler/icons-react';
import { Provider } from '../../../types/routing';

interface ProviderSelectionProps {
  selectedProvider?: Provider;
  fallbackChain: Provider[];
  routingDecision: {
    strategy: string;
    reason: string;
    fallbackUsed: boolean;
    processingTimeMs: number;
  };
}

export function ProviderSelection({
  selectedProvider,
  fallbackChain,
  routingDecision,
}: ProviderSelectionProps) {
  const getProviderTypeColor = (type: string) => {
    switch (type) {
      case 'primary': return 'blue';
      case 'backup': return 'orange';
      case 'special': return 'green';
      default: return 'gray';
    }
  };

  const getProviderIcon = (type: string) => {
    switch (type) {
      case 'primary': return <IconServer size={16} />;
      case 'backup': return <IconShield size={16} />;
      case 'special': return <IconTarget size={16} />;
      default: return <IconServer size={16} />;
    }
  };

  const getStrategyDescription = (strategy: string) => {
    switch (strategy) {
      case 'priority':
        return 'Selected based on provider priority ranking';
      case 'round_robin':
        return 'Selected using round-robin load balancing';
      case 'least_latency':
        return 'Selected provider with lowest response time';
      case 'cost_optimized':
        return 'Selected most cost-effective provider';
      default:
        return 'Selected using default routing strategy';
    }
  };

  const getProcessingTimeColor = (time: number) => {
    if (time < 5) return 'green';
    if (time < 15) return 'yellow';
    return 'red';
  };

  return (
    <Card shadow="sm" p="md" radius="md" withBorder>
      <Stack gap="md">
        {/* Header */}
        <Group justify="space-between" align="flex-start">
          <div>
            <Group align="center" gap="sm">
              <IconTarget size={20} color="blue" />
              <Title order={5}>Provider Selection</Title>
            </Group>
            <Text size="sm" c="dimmed">
              Selected provider and fallback chain based on routing rules
            </Text>
          </div>
          <Group>
            <Badge
              variant="light"
              color={getProcessingTimeColor(routingDecision.processingTimeMs)}
            >
              {routingDecision.processingTimeMs.toFixed(2)}ms
            </Badge>
            {routingDecision.fallbackUsed && (
              <Badge color="orange" variant="light">
                Fallback Used
              </Badge>
            )}
          </Group>
        </Group>

        {/* Selected Provider */}
        {selectedProvider ? (
          <Card withBorder p="md" bg="blue.0">
            <Group justify="space-between" align="center">
              <Group>
                <ThemeIcon
                  size="lg"
                  variant="light"
                  color={getProviderTypeColor(selectedProvider.type)}
                >
                  {getProviderIcon(selectedProvider.type)}
                </ThemeIcon>
                <div>
                  <Text fw={600} size="lg">{selectedProvider.name}</Text>
                  <Group gap="xs" mt={2}>
                    <Badge
                      variant="light"
                      color={getProviderTypeColor(selectedProvider.type)}
                      size="sm"
                    >
                      {selectedProvider.type}
                    </Badge>
                    <Badge variant="light" size="sm">
                      Priority: {selectedProvider.priority}
                    </Badge>
                    <Badge
                      variant="light"
                      color={selectedProvider.isEnabled ? 'green' : 'red'}
                      size="sm"
                    >
                      {selectedProvider.isEnabled ? 'Enabled' : 'Disabled'}
                    </Badge>
                  </Group>
                </div>
              </Group>
              <div style={{ textAlign: 'right' }}>
                <Text size="sm" fw={500}>Selected Provider</Text>
                <Text size="xs" c="dimmed">ID: {selectedProvider.id}</Text>
                {selectedProvider.endpoint && (
                  <Text size="xs" c="dimmed">{selectedProvider.endpoint}</Text>
                )}
              </div>
            </Group>
          </Card>
        ) : (
          <Alert icon={<IconAlertTriangle size="1rem" />} title="No Provider Selected" color="orange">
            <Text size="sm">
              No provider was selected for this request. This could indicate a configuration issue
              or that all providers are disabled.
            </Text>
          </Alert>
        )}

        {/* Routing Decision Details */}
        <Card withBorder p="md">
          <Stack gap="sm">
            <Group align="center" gap="sm">
              <IconInfoCircle size={16} color="blue" />
              <Text fw={500}>Routing Decision</Text>
            </Group>
            
            <Table>
              <Table.Tbody>
                <Table.Tr>
                  <Table.Td style={{ width: '140px' }}>
                    <Text size="sm" fw={500}>Strategy</Text>
                  </Table.Td>
                  <Table.Td>
                    <Group gap="xs">
                      <Badge variant="light" color="blue">
                        {routingDecision.strategy.replace('_', ' ')}
                      </Badge>
                      <Text size="sm" c="dimmed">
                        {getStrategyDescription(routingDecision.strategy)}
                      </Text>
                    </Group>
                  </Table.Td>
                </Table.Tr>
                <Table.Tr>
                  <Table.Td>
                    <Text size="sm" fw={500}>Reason</Text>
                  </Table.Td>
                  <Table.Td>
                    <Text size="sm">{routingDecision.reason}</Text>
                  </Table.Td>
                </Table.Tr>
                <Table.Tr>
                  <Table.Td>
                    <Text size="sm" fw={500}>Processing Time</Text>
                  </Table.Td>
                  <Table.Td>
                    <Group gap="xs">
                      <Progress
                        value={Math.min((routingDecision.processingTimeMs / 20) * 100, 100)}
                        size="sm"
                        w={100}
                        color={getProcessingTimeColor(routingDecision.processingTimeMs)}
                      />
                      <Text
                        size="sm"
                        color={getProcessingTimeColor(routingDecision.processingTimeMs)}
                      >
                        {routingDecision.processingTimeMs.toFixed(2)}ms
                      </Text>
                    </Group>
                  </Table.Td>
                </Table.Tr>
                <Table.Tr>
                  <Table.Td>
                    <Text size="sm" fw={500}>Fallback Used</Text>
                  </Table.Td>
                  <Table.Td>
                    <Badge
                      variant="light"
                      color={routingDecision.fallbackUsed ? 'orange' : 'green'}
                    >
                      {routingDecision.fallbackUsed ? 'Yes' : 'No'}
                    </Badge>
                  </Table.Td>
                </Table.Tr>
              </Table.Tbody>
            </Table>
          </Stack>
        </Card>

        {/* Fallback Chain */}
        {fallbackChain.length > 0 && (
          <Card withBorder p="md">
            <Stack gap="sm">
              <Group align="center" gap="sm">
                <IconShield size={16} color="orange" />
                <Text fw={500}>Fallback Chain</Text>
                <Badge size="sm" variant="light">
                  {fallbackChain.length} providers
                </Badge>
              </Group>
              
              <Text size="sm" c="dimmed" mb="sm">
                These providers will be tried in order if the selected provider fails:
              </Text>
              
              <Group gap="sm" wrap="nowrap" style={{ overflowX: 'auto' }}>
                {fallbackChain.map((provider, index) => (
                  <Group key={provider.id} gap="xs" wrap="nowrap">
                    <Card withBorder p="sm" style={{ minWidth: '160px' }}>
                      <Group gap="xs">
                        <ThemeIcon
                          size="sm"
                          variant="light"
                          color={getProviderTypeColor(provider.type)}
                        >
                          {getProviderIcon(provider.type)}
                        </ThemeIcon>
                        <div>
                          <Text size="sm" fw={500}>{provider.name}</Text>
                          <Group gap={4}>
                            <Badge size="xs" variant="light" color={getProviderTypeColor(provider.type)}>
                              {provider.type}
                            </Badge>
                            <Badge size="xs" variant="light">
                              P{provider.priority}
                            </Badge>
                          </Group>
                        </div>
                      </Group>
                    </Card>
                    
                    {index < fallbackChain.length - 1 && (
                      <IconArrowRight size={16} color="gray" />
                    )}
                  </Group>
                ))}
              </Group>
            </Stack>
          </Card>
        )}

        {/* No Fallback */}
        {fallbackChain.length === 0 && selectedProvider && (
          <Alert icon={<IconInfoCircle size="1rem" />} title="No Fallback Configured" color="blue">
            <Text size="sm">
              No fallback providers are configured. If the selected provider fails, 
              the request will return an error.
            </Text>
          </Alert>
        )}
      </Stack>
    </Card>
  );
}