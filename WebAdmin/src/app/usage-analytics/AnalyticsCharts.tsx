import {
  Grid,
  Card,
  Text,
  Group,
  Badge,
  ThemeIcon,
  Table,
  ScrollArea,
  Skeleton,
  Stack,
} from '@mantine/core';
import {
  AreaChart,
  BarChart,
  DonutChart,
} from '@mantine/charts';
import {
  IconTrendingUp,
  IconApi,
  IconCoins,
  IconUsers,
} from '@tabler/icons-react';
import { CardSkeleton } from '@/components/common/LoadingState';
import { formatters } from '@/lib/utils/formatters';

interface UsageMetrics {
  totalRequests: number;
  totalCost: number;
  totalTokens: number;
  activeVirtualKeys: number;
  requestsChange: number;
  costChange: number;
  tokensChange: number;
  virtualKeysChange: number;
}

interface TimeSeriesData {
  timestamp: string;
  requests: number;
  cost: number;
  tokens: number;
}

interface ProviderUsage {
  provider: string;
  requests: number;
  cost: number;
  tokens: number;
  percentage: number;
}

interface ModelUsage {
  model: string;
  provider: string;
  requests: number;
  cost: number;
  tokens: number;
}

interface VirtualKeyUsage {
  keyName: string;
  requests: number;
  cost: number;
  tokens: number;
  lastUsed: string;
}

interface AnalyticsChartsProps {
  isLoading: boolean;
  metrics: UsageMetrics | null;
  timeSeriesData: TimeSeriesData[];
  providerUsage: ProviderUsage[];
  modelUsage: ModelUsage[];
  virtualKeyUsage: VirtualKeyUsage[];
}

function getChangeColor(change: number): string {
  if (change > 0) return 'green';
  if (change < 0) return 'red';
  return 'gray';
}

export function AnalyticsCharts({
  isLoading,
  metrics,
  timeSeriesData,
  providerUsage,
  modelUsage,
  virtualKeyUsage,
}: AnalyticsChartsProps) {
  return (
    <>
      {/* Key Metrics */}
      <Grid>
        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          {isLoading ? (
            <CardSkeleton height={120} />
          ) : (
            <Card withBorder>
              <Group justify="space-between" mb="md">
                <div>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                    Total Requests
                  </Text>
                  <Text size="xl" fw={700}>
                    {formatters.number(metrics?.totalRequests ?? 0)}
                  </Text>
                </div>
                <ThemeIcon color="blue" variant="light" size="xl">
                  <IconApi size={24} />
                </ThemeIcon>
              </Group>
              <Group gap="xs">
                <Text size="xs" c={getChangeColor(metrics?.requestsChange ?? 0)}>
                  {Math.abs(metrics?.requestsChange ?? 0)}%
                </Text>
                <Text size="xs" c="dimmed">vs previous period</Text>
              </Group>
            </Card>
          )}
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          {isLoading ? (
            <CardSkeleton height={120} />
          ) : (
            <Card withBorder>
              <Group justify="space-between" mb="md">
                <div>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                    Total Cost
                  </Text>
                  <Text size="xl" fw={700}>
                    {formatters.currency(metrics?.totalCost ?? 0)}
                  </Text>
                </div>
                <ThemeIcon color="green" variant="light" size="xl">
                  <IconCoins size={24} />
                </ThemeIcon>
              </Group>
              <Group gap="xs">
                <Text size="xs" c={getChangeColor(metrics?.costChange ?? 0)}>
                  {Math.abs(metrics?.costChange ?? 0)}%
                </Text>
                <Text size="xs" c="dimmed">vs previous period</Text>
              </Group>
            </Card>
          )}
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          {isLoading ? (
            <CardSkeleton height={120} />
          ) : (
            <Card withBorder>
              <Group justify="space-between" mb="md">
                <div>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                    Total Tokens
                  </Text>
                  <Text size="xl" fw={700}>
                    {formatters.shortNumber(metrics?.totalTokens ?? 0)}
                  </Text>
                </div>
                <ThemeIcon color="cyan" variant="light" size="xl">
                  <IconTrendingUp size={24} />
                </ThemeIcon>
              </Group>
              <Group gap="xs">
                <Text size="xs" c={getChangeColor(metrics?.tokensChange ?? 0)}>
                  {Math.abs(metrics?.tokensChange ?? 0)}%
                </Text>
                <Text size="xs" c="dimmed">vs previous period</Text>
              </Group>
            </Card>
          )}
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          {isLoading ? (
            <CardSkeleton height={120} />
          ) : (
            <Card withBorder>
              <Group justify="space-between" mb="md">
                <div>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                    Active Keys
                  </Text>
                  <Text size="xl" fw={700}>
                    {metrics?.activeVirtualKeys ?? 0}
                  </Text>
                </div>
                <ThemeIcon color="orange" variant="light" size="xl">
                  <IconUsers size={24} />
                </ThemeIcon>
              </Group>
              <Group gap="xs">
                <Text size="xs" c={getChangeColor(metrics?.virtualKeysChange ?? 0)}>
                  {Math.abs(metrics?.virtualKeysChange ?? 0)}%
                </Text>
                <Text size="xs" c="dimmed">vs previous period</Text>
              </Group>
            </Card>
          )}
        </Grid.Col>
      </Grid>

      {/* Usage Over Time */}
      <Card withBorder>
        <Card.Section withBorder inheritPadding py="xs">
          <Text fw={500}>Usage Over Time</Text>
        </Card.Section>
        <Card.Section inheritPadding py="md">
          {isLoading ? (
            <Skeleton height={300} />
          ) : (
            <AreaChart
              h={300}
              data={timeSeriesData}
              dataKey="timestamp"
              series={[
                { name: 'requests', color: 'blue.6' },
                { name: 'cost', color: 'green.6' },
              ]}
              curveType="linear"
              withLegend
              legendProps={{ verticalAlign: 'bottom', height: 50 }}
              valueFormatter={(value) => formatters.number(value)}
            />
          )}
        </Card.Section>
      </Card>

      <Grid>
        {/* Provider Usage */}
        <Grid.Col span={{ base: 12, md: 6 }}>
          <Card withBorder h="100%">
            <Card.Section withBorder inheritPadding py="xs">
              <Text fw={500}>Usage by Provider</Text>
            </Card.Section>
            <Card.Section inheritPadding py="md">
              {isLoading ? (
                <Skeleton height={250} />
              ) : (
                <Stack gap="md">
                  <DonutChart
                    h={200}
                    data={providerUsage.map(p => ({
                      name: p.provider,
                      value: p.requests,
                      color: {
                        'OpenAI': 'blue.6',
                        'Anthropic': 'orange.6',
                        'Azure': 'cyan.6',
                        'Google': 'green.6',
                        'Replicate': 'purple.6',
                      }[p.provider] ?? 'gray.6'
                    }))}
                    withLabelsLine
                    withLabels
                    paddingAngle={2}
                  />
                  <ScrollArea h={150}>
                    <Table>
                      <Table.Thead>
                        <Table.Tr>
                          <Table.Th>Provider</Table.Th>
                          <Table.Th>Requests</Table.Th>
                          <Table.Th>Cost</Table.Th>
                          <Table.Th>%</Table.Th>
                        </Table.Tr>
                      </Table.Thead>
                      <Table.Tbody>
                        {providerUsage.map((provider) => (
                          <Table.Tr key={provider.provider}>
                            <Table.Td>{provider.provider}</Table.Td>
                            <Table.Td>{formatters.number(provider.requests)}</Table.Td>
                            <Table.Td>{formatters.currency(provider.cost)}</Table.Td>
                            <Table.Td>{provider.percentage}%</Table.Td>
                          </Table.Tr>
                        ))}
                      </Table.Tbody>
                    </Table>
                  </ScrollArea>
                </Stack>
              )}
            </Card.Section>
          </Card>
        </Grid.Col>

        {/* Top Models */}
        <Grid.Col span={{ base: 12, md: 6 }}>
          <Card withBorder h="100%">
            <Card.Section withBorder inheritPadding py="xs">
              <Text fw={500}>Top Models by Usage</Text>
            </Card.Section>
            <Card.Section inheritPadding py="md">
              {isLoading ? (
                <Skeleton height={250} />
              ) : (
                <ScrollArea h={450}>
                  <Table>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Model</Table.Th>
                        <Table.Th>Provider</Table.Th>
                        <Table.Th>Requests</Table.Th>
                        <Table.Th>Cost</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {modelUsage.slice(0, 10).map((model) => (
                        <Table.Tr key={`${model.provider}-${model.model}`}>
                          <Table.Td>
                            <Text size="sm" fw={500}>{model.model}</Text>
                          </Table.Td>
                          <Table.Td>
                            <Badge size="sm" variant="light">
                              {model.provider}
                            </Badge>
                          </Table.Td>
                          <Table.Td>{formatters.number(model.requests)}</Table.Td>
                          <Table.Td>{formatters.currency(model.cost)}</Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </ScrollArea>
              )}
            </Card.Section>
          </Card>
        </Grid.Col>
      </Grid>

      {/* Virtual Key Usage */}
      <Card withBorder>
        <Card.Section withBorder inheritPadding py="xs">
          <Group justify="space-between">
            <Text fw={500}>Virtual Key Usage</Text>
            <Text size="sm" c="dimmed">Top 10 by request volume</Text>
          </Group>
        </Card.Section>
        <Card.Section inheritPadding py="md">
          {isLoading ? (
            <Skeleton height={300} />
          ) : (
            <BarChart
              h={300}
              data={virtualKeyUsage.slice(0, 10)}
              dataKey="keyName"
              series={[
                { name: 'requests', color: 'blue.6' },
              ]}
              tickLine="y"
              gridAxis="y"
              valueFormatter={(value) => formatters.number(value)}
            />
          )}
        </Card.Section>
      </Card>
    </>
  );
}