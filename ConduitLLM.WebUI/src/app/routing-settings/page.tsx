'use client';

import {
  Stack,
  Title,
  Text,
  Card,
  Grid,
  Group,
  Button,
  Select,
  Switch,
  NumberInput,
  TextInput,
  Badge,
  Table,
  ScrollArea,
  ActionIcon,
  Tooltip,
  Modal,
  Tabs,
  JsonInput,
  Alert,
  Paper,
  ThemeIcon,
  Code,
  Slider,
  SegmentedControl,
  Checkbox,
  MultiSelect,
  Accordion,
  LoadingOverlay,
  Progress,
} from '@mantine/core';
import {
  IconRoute,
  IconSettings,
  IconPlus,
  IconTrash,
  IconEdit,
  IconCheck,
  IconX,
  IconRefresh,
  IconAlertCircle,
  IconInfoCircle,
  IconArrowUp,
  IconArrowDown,
  IconCopy,
  IconTestPipe,
  IconScale,
  IconNetwork,
  IconBrandSpeedtest,
  IconClock,
  IconRepeat,
  IconShieldCheck,
  IconFilter,
  IconSortAscending,
  IconDeviceFloppy,
  IconDownload,
} from '@tabler/icons-react';
import { useState } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { DragDropContext, Droppable, Draggable } from '@hello-pangea/dnd';
import { 
  useRoutingConfig, 
  useUpdateRoutingConfig,
  type RoutingRule,
  type LoadBalancer,
  type RetryPolicy,
} from '@/hooks/api/useConfigurationApi';
import { formatNumber } from '@/lib/utils/formatting';


const strategyDescriptions = {
  round_robin: 'Distributes requests evenly across all providers',
  weighted: 'Routes requests based on assigned weights',
  least_connections: 'Routes to provider with fewest active connections',
  response_time: 'Routes to provider with lowest average response time',
  hash: 'Routes based on consistent hashing of request attributes',
};

export default function RoutingSettingsPage() {
  const [activeTab, setActiveTab] = useState<string | null>('rules');
  
  // Fetch data using the configuration API hooks
  const { data: routingData, isLoading: routingLoading, refetch: refetchRouting } = useRoutingConfig();
  const updateConfigMutation = useUpdateRoutingConfig();
  
  const routingRules = routingData?.routingRules || [];
  const loadBalancers = routingData?.loadBalancers || [];
  const retryPolicies = routingData?.retryPolicies || [];
  const configuration = routingData?.configuration || {
    enableFailover: true,
    enableLoadBalancing: true,
    requestTimeoutSeconds: 30,
    circuitBreakerThreshold: 5,
  };
  
  const [ruleModalOpened, { open: openRuleModal, close: closeRuleModal }] = useDisclosure(false);
  const [balancerModalOpened, { open: openBalancerModal, close: closeBalancerModal }] = useDisclosure(false);
  const [retryModalOpened, { open: openRetryModal, close: closeRetryModal }] = useDisclosure(false);
  
  const [editingRule, setEditingRule] = useState<RoutingRule | null>(null);
  const [editingBalancer, setEditingBalancer] = useState<LoadBalancer | null>(null);
  const [editingRetry, setEditingRetry] = useState<RetryPolicy | null>(null);

  // Form states
  const [ruleFormData, setRuleFormData] = useState<{
    name: string;
    conditions: any[];
    actions: any[];
    description: string;
    enabled: boolean;
  }>({
    name: '',
    conditions: [],
    actions: [],
    description: '',
    enabled: true,
  });

  const handleDragEnd = (result: any) => {
    if (!result.destination) return;

    const items = Array.from(routingRules);
    const [reorderedItem] = items.splice(result.source.index, 1);
    items.splice(result.destination.index, 0, reorderedItem);

    // Update priorities based on new order
    const updatedItems = items.map((item, index) => ({
      ...item,
      priority: index + 1,
    }));

    // TODO: Update via API when endpoint is available
    notifications.show({
      title: 'Rules Reordered',
      message: 'Routing rule priorities have been updated',
      color: 'green',
    });
  };

  const handleAddRule = () => {
    setEditingRule(null);
    setRuleFormData({
      name: '',
      conditions: [],
      actions: [],
      description: '',
      enabled: true,
    });
    openRuleModal();
  };

  const handleEditRule = (rule: RoutingRule) => {
    setEditingRule(rule);
    setRuleFormData({
      name: rule.modelAlias,
      conditions: [],
      actions: [],
      description: '',
      enabled: rule.isEnabled,
    });
    openRuleModal();
  };

  const handleDeleteRule = (id: number) => {
    // TODO: Delete via API when endpoint is available
    notifications.show({
      title: 'Rule Deleted',
      message: 'Routing rule has been removed',
      color: 'red',
    });
  };

  const handleToggleRule = (rule: RoutingRule) => {
    // TODO: Update via API when endpoint is available
    notifications.show({
      title: 'Rule Updated',
      message: `${rule.modelAlias} has been ${rule.isEnabled ? 'disabled' : 'enabled'}`,
      color: 'blue',
    });
  };

  const handleTestRule = (rule: RoutingRule) => {
    notifications.show({
      title: 'Testing Rule',
      message: `Testing routing rule: ${rule.modelAlias}`,
      color: 'blue',
      loading: true,
    });

    setTimeout(() => {
      notifications.show({
        title: 'Test Complete',
        message: 'Routing rule is working correctly',
        color: 'green',
      });
    }, 2000);
  };

  const handleSaveConfiguration = async () => {
    try {
      await updateConfigMutation.mutateAsync({
        enableFailover: configuration.enableFailover,
        enableLoadBalancing: configuration.enableLoadBalancing,
        requestTimeoutSeconds: configuration.requestTimeoutSeconds,
        circuitBreakerThreshold: configuration.circuitBreakerThreshold,
      });
      notifications.show({
        title: 'Configuration Saved',
        message: 'Routing settings have been saved successfully',
        color: 'green',
      });
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: 'Failed to save routing configuration',
        color: 'red',
      });
    }
  };

  const handleImportConfiguration = () => {
    notifications.show({
      title: 'Import Configuration',
      message: 'Select a configuration file to import',
      color: 'blue',
    });
  };

  const handleExportConfiguration = () => {
    notifications.show({
      title: 'Export Started',
      message: 'Exporting routing configuration...',
      color: 'blue',
    });
  };

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <div>
          <Title order={1}>Routing Settings</Title>
          <Text c="dimmed">Configure request routing, load balancing, and failover strategies</Text>
        </div>
        <Group>
          <Button
            variant="light"
            leftSection={<IconCopy size={16} />}
            onClick={handleImportConfiguration}
          >
            Import
          </Button>
          <Button
            variant="light"
            leftSection={<IconDownload size={16} />}
            onClick={handleExportConfiguration}
          >
            Export
          </Button>
          <Button
            leftSection={<IconDeviceFloppy size={16} />}
            onClick={handleSaveConfiguration}
          >
            Save Changes
          </Button>
        </Group>
      </Group>

      <Tabs value={activeTab} onChange={setActiveTab}>
        <Tabs.List>
          <Tabs.Tab value="rules" leftSection={<IconRoute size={16} />}>
            Routing Rules
          </Tabs.Tab>
          <Tabs.Tab value="loadbalancing" leftSection={<IconScale size={16} />}>
            Load Balancing
          </Tabs.Tab>
          <Tabs.Tab value="retry" leftSection={<IconRepeat size={16} />}>
            Retry Policies
          </Tabs.Tab>
          <Tabs.Tab value="advanced" leftSection={<IconSettings size={16} />}>
            Advanced Settings
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="rules" pt="md">
          {/* Routing Rules */}
          <Card withBorder>
            <LoadingOverlay visible={routingLoading} />
            <Group justify="space-between" mb="md">
              <div>
                <Text fw={600}>Model Routing Rules</Text>
                <Text size="sm" c="dimmed">Configure how models are routed to providers</Text>
              </div>
              <Button
                leftSection={<IconRefresh size={16} />}
                onClick={() => refetchRouting()}
                variant="light"
              >
                Refresh
              </Button>
            </Group>

            <ScrollArea>
              <Table striped highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Model Alias</Table.Th>
                    <Table.Th>Provider Model</Table.Th>
                    <Table.Th>Provider</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Actions</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {routingRules.map((rule) => (
                    <Table.Tr key={rule.id}>
                      <Table.Td>
                        <Text fw={500}>{rule.modelAlias}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Code>{rule.providerModelName}</Code>
                      </Table.Td>
                      <Table.Td>
                        <Badge variant="light" color={rule.provider.isEnabled ? 'blue' : 'gray'}>
                          {rule.provider.name}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Switch
                          checked={rule.isEnabled}
                          onChange={() => handleToggleRule(rule)}
                          size="sm"
                        />
                      </Table.Td>
                      <Table.Td>
                        <Group gap="xs">
                          <Tooltip label="Test Route">
                            <ActionIcon
                              variant="subtle"
                              size="sm"
                              onClick={() => handleTestRule(rule)}
                            >
                              <IconTestPipe size={16} />
                            </ActionIcon>
                          </Tooltip>
                        </Group>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          </Card>

          {/* Rule Statistics */}
          <Grid mt="md">
            <Grid.Col span={{ base: 12, md: 4 }}>
              <Card withBorder>
                <Group justify="space-between">
                  <div>
                    <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                      Active Routes
                    </Text>
                    <Text size="xl" fw={700}>
                      {routingRules.filter(r => r.isEnabled).length}
                    </Text>
                  </div>
                  <ThemeIcon size="xl" radius="md" variant="light">
                    <IconRoute size={24} />
                  </ThemeIcon>
                </Group>
              </Card>
            </Grid.Col>

            <Grid.Col span={{ base: 12, md: 4 }}>
              <Card withBorder>
                <Group justify="space-between">
                  <div>
                    <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                      Total Routes
                    </Text>
                    <Text size="xl" fw={700}>
                      {routingRules.length}
                    </Text>
                  </div>
                  <ThemeIcon size="xl" radius="md" variant="light" color="blue">
                    <IconFilter size={24} />
                  </ThemeIcon>
                </Group>
              </Card>
            </Grid.Col>

            <Grid.Col span={{ base: 12, md: 4 }}>
              <Card withBorder>
                <Group justify="space-between">
                  <div>
                    <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                      Request Volume
                    </Text>
                    <Text size="xl" fw={700}>
                      {formatNumber(routingData?.statistics?.totalRequests || 0)}
                    </Text>
                  </div>
                  <ThemeIcon size="xl" radius="md" variant="light" color="green">
                    <IconBrandSpeedtest size={24} />
                  </ThemeIcon>
                </Group>
              </Card>
            </Grid.Col>
          </Grid>
        </Tabs.Panel>

        <Tabs.Panel value="loadbalancing" pt="md">
          {/* Load Balancer Configurations */}
          <Card withBorder>
            <LoadingOverlay visible={routingLoading} />
            <Group justify="space-between" mb="md">
              <div>
                <Text fw={600}>Load Balancer Configuration</Text>
                <Text size="sm" c="dimmed">Configure load distribution between providers</Text>
              </div>
            </Group>

            <Stack gap="md">
              {loadBalancers.map((balancer) => (
                <Paper key={balancer.id} p="md" withBorder>
                  <Group justify="space-between" mb="md">
                    <div>
                      <Group gap="xs">
                        <Text fw={500}>{balancer.name}</Text>
                        <Badge variant="light">
                          {balancer.algorithm.replace('_', ' ')}
                        </Badge>
                      </Group>
                      <Text size="sm" c="dimmed" mt={4}>
                        {strategyDescriptions[balancer.algorithm as keyof typeof strategyDescriptions] || balancer.algorithm}
                      </Text>
                    </div>
                    <Group gap="xs">
                      <Text size="sm" c="dimmed">
                        Health check every {balancer.healthCheckInterval}s
                      </Text>
                    </Group>
                  </Group>

                  <ScrollArea>
                    <Table>
                      <Table.Thead>
                        <Table.Tr>
                          <Table.Th>Endpoint</Table.Th>
                          <Table.Th>URL</Table.Th>
                          <Table.Th>Weight</Table.Th>
                          <Table.Th>Response Time</Table.Th>
                          <Table.Th>Status</Table.Th>
                        </Table.Tr>
                      </Table.Thead>
                      <Table.Tbody>
                        {balancer.endpoints.map((endpoint, index) => (
                          <Table.Tr key={index}>
                            <Table.Td>{endpoint.name}</Table.Td>
                            <Table.Td>
                              <Code>{endpoint.url}</Code>
                            </Table.Td>
                            <Table.Td>
                              <Badge variant="light">{endpoint.weight}%</Badge>
                            </Table.Td>
                            <Table.Td>{endpoint.responseTime}ms</Table.Td>
                            <Table.Td>
                              <Badge 
                                variant="dot" 
                                color={endpoint.healthStatus === 'healthy' ? 'green' : endpoint.healthStatus === 'degraded' ? 'yellow' : 'red'}
                              >
                                {endpoint.healthStatus}
                              </Badge>
                            </Table.Td>
                          </Table.Tr>
                        ))}
                      </Table.Tbody>
                    </Table>
                  </ScrollArea>
                </Paper>
              ))}
            </Stack>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="retry" pt="md">
          {/* Retry Policies */}
          <Card withBorder>
            <LoadingOverlay visible={routingLoading} />
            <Group justify="space-between" mb="md">
              <div>
                <Text fw={600}>Retry Policies</Text>
                <Text size="sm" c="dimmed">Configure retry behavior for failed requests</Text>
              </div>
            </Group>

            <Stack gap="md">
              {retryPolicies.map((policy) => (
                <Paper key={policy.id} p="md" withBorder>
                  <Group justify="space-between">
                    <div>
                      <Text fw={500}>{policy.name}</Text>
                      
                      <Group gap="xl" mt="sm">
                        <div>
                          <Text size="xs" c="dimmed">Max Retries</Text>
                          <Text size="sm" fw={500}>{policy.maxRetries}</Text>
                        </div>
                        <div>
                          <Text size="xs" c="dimmed">Initial Delay</Text>
                          <Text size="sm" fw={500}>{policy.initialDelay}ms</Text>
                        </div>
                        <div>
                          <Text size="xs" c="dimmed">Max Delay</Text>
                          <Text size="sm" fw={500}>{policy.maxDelay}ms</Text>
                        </div>
                        <div>
                          <Text size="xs" c="dimmed">Backoff Multiplier</Text>
                          <Text size="sm" fw={500}>{policy.backoffMultiplier}x</Text>
                        </div>
                      </Group>
                      
                      <Group gap="xs" mt="sm">
                        <Text size="xs" c="dimmed">Retryable status codes:</Text>
                        {policy.retryableStatusCodes.map(status => (
                          <Badge key={status} size="xs" variant="light">
                            {status}
                          </Badge>
                        ))}
                      </Group>
                    </div>
                  </Group>
                </Paper>
              ))}
            </Stack>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="advanced" pt="md">
          {/* Advanced Settings */}
          <Stack gap="md">
            {/* Circuit Breaker Settings */}
            <Card withBorder>
              <Text fw={600} mb="md">Circuit Breaker Configuration</Text>
              <Grid>
                <Grid.Col span={{ base: 12, md: 6 }}>
                  <NumberInput
                    label="Failure Threshold"
                    description="Number of failures before opening circuit"
                    value={configuration.circuitBreakerThreshold}
                    min={1}
                    max={100}
                    disabled
                  />
                </Grid.Col>
                <Grid.Col span={{ base: 12, md: 6 }}>
                  <NumberInput
                    label="Request Timeout"
                    description="Maximum time for a request in seconds"
                    value={configuration.requestTimeoutSeconds}
                    min={1}
                    max={600}
                    suffix=" seconds"
                    disabled
                  />
                </Grid.Col>
              </Grid>
            </Card>

            {/* Feature Toggles */}
            <Card withBorder>
              <Text fw={600} mb="md">Feature Configuration</Text>
              <Stack gap="md">
                <Switch
                  label="Enable Failover"
                  description="Automatically switch to backup providers on failure"
                  checked={configuration.enableFailover}
                  disabled
                />
                <Switch
                  label="Enable Load Balancing"
                  description="Distribute requests across multiple providers"
                  checked={configuration.enableLoadBalancing}
                  disabled
                />
              </Stack>
            </Card>

            {/* Provider Distribution */}
            {routingData?.statistics?.providerDistribution && routingData.statistics.providerDistribution.length > 0 && (
              <Card withBorder>
                <Text fw={600} mb="md">Provider Usage Statistics</Text>
                <ScrollArea>
                  <Table>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Provider</Table.Th>
                        <Table.Th>Requests</Table.Th>
                        <Table.Th>Success Rate</Table.Th>
                        <Table.Th>Avg Latency</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {routingData.statistics.providerDistribution.map((provider, index) => (
                        <Table.Tr key={index}>
                          <Table.Td>{provider.provider}</Table.Td>
                          <Table.Td>{formatNumber(provider.requestCount)}</Table.Td>
                          <Table.Td>
                            <Group gap="xs">
                              <Text size="sm">{provider.successRate.toFixed(1)}%</Text>
                              <Progress 
                                value={provider.successRate} 
                                size="xs" 
                                style={{ width: 60 }}
                                color={provider.successRate > 95 ? 'green' : provider.successRate > 90 ? 'yellow' : 'red'}
                              />
                            </Group>
                          </Table.Td>
                          <Table.Td>{provider.avgLatency.toFixed(0)}ms</Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </ScrollArea>
              </Card>
            )}
          </Stack>
        </Tabs.Panel>
      </Tabs>

    </Stack>
  );
}