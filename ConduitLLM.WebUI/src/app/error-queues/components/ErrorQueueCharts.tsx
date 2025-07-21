'use client';

import { Grid, Paper, Title, Text, Center, Loader } from '@mantine/core';
import {
  LineChart,
  Line,
  BarChart,
  Bar,
  PieChart,
  Pie,
  Cell,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
} from 'recharts';
import { useErrorQueueStatistics } from '@/hooks/useErrorQueues';
import type { ErrorQueueInfo } from '@knn_labs/conduit-admin-client';

interface ErrorQueueChartsProps {
  queues: ErrorQueueInfo[];
}

const COLORS = ['#FF6B6B', '#FFA502', '#4ECDC4', '#45B7D1', '#96CEB4'];

export function ErrorQueueCharts({ queues }: ErrorQueueChartsProps) {
  const { data: statisticsData, isLoading } = useErrorQueueStatistics({
    since: new Date(Date.now() - 24 * 60 * 60 * 1000), // Last 24 hours
    groupBy: 'hour',
  });

  const statistics = statisticsData as { errorRateTrends?: ErrorRateTrend[] } | undefined;

  if (isLoading) {
    return (
      <Center h={300}>
        <Loader />
      </Center>
    );
  }

  // Prepare data for charts
  const topQueues = [...queues]
    .sort((a, b) => b.messageCount - a.messageCount)
    .slice(0, 5)
    .map((q) => ({
      name: q.queueName.replace('error:', ''),
      messages: q.messageCount,
    }));

  const errorDistribution = queues
    .filter((q) => q.messageCount > 0)
    .map((q) => ({
      name: q.originalQueue,
      value: q.messageCount,
    }))
    .reduce((acc, curr) => {
      const existing = acc.find((item) => item.name === curr.name);
      if (existing) {
        existing.value += curr.value;
      } else {
        acc.push(curr);
      }
      return acc;
    }, [] as { name: string; value: number }[]);

  interface ErrorRateTrend {
    period: string;
    errorCount: number;
    errorsPerMinute: number;
  }
  
  const errorRateData =
    statistics?.errorRateTrends?.map((trend: ErrorRateTrend) => ({
      period: new Date(trend.period).toLocaleTimeString([], {
        hour: '2-digit',
        minute: '2-digit',
      }),
      errors: trend.errorCount,
      rate: trend.errorsPerMinute,
    })) ?? [];

  return (
    <Grid>
      {/* Error Rate Timeline */}
      <Grid.Col span={{ base: 12, lg: 8 }}>
        <Paper shadow="xs" p="md" radius="md">
          <Title order={4} mb="md">
            Error Rate (Last 24 Hours)
          </Title>
          {errorRateData?.length > 0 ? (
            <ResponsiveContainer width="100%" height={300}>
              <LineChart data={errorRateData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="period" />
                <YAxis />
                <Tooltip />
                <Legend />
                <Line
                  type="monotone"
                  dataKey="errors"
                  stroke="#FF6B6B"
                  name="Total Errors"
                  strokeWidth={2}
                />
                <Line
                  type="monotone"
                  dataKey="rate"
                  stroke="#45B7D1"
                  name="Errors/min"
                  strokeWidth={2}
                />
              </LineChart>
            </ResponsiveContainer>
          ) : (
            <Center h={300}>
              <Text c="dimmed">No error rate data available</Text>
            </Center>
          )}
        </Paper>
      </Grid.Col>

      {/* Error Distribution Pie Chart */}
      <Grid.Col span={{ base: 12, lg: 4 }}>
        <Paper shadow="xs" p="md" radius="md">
          <Title order={4} mb="md">
            Error Distribution
          </Title>
          {errorDistribution.length > 0 ? (
            <ResponsiveContainer width="100%" height={300}>
              <PieChart>
                <Pie
                  data={errorDistribution}
                  cx="50%"
                  cy="50%"
                  labelLine={false}
                  label={({ name, percent }) =>
                    `${name} ${(percent * 100).toFixed(0)}%`
                  }
                  outerRadius={80}
                  fill="#8884d8"
                  dataKey="value"
                >
                  {errorDistribution.map((entry, index) => (
                    <Cell
                      key={`cell-${entry.name}`}
                      fill={COLORS[index % COLORS.length]}
                    />
                  ))}
                </Pie>
                <Tooltip />
              </PieChart>
            </ResponsiveContainer>
          ) : (
            <Center h={300}>
              <Text c="dimmed">No error distribution data</Text>
            </Center>
          )}
        </Paper>
      </Grid.Col>

      {/* Top Failing Queues */}
      <Grid.Col span={12}>
        <Paper shadow="xs" p="md" radius="md">
          <Title order={4} mb="md">
            Top Error Queues
          </Title>
          {topQueues.length > 0 ? (
            <ResponsiveContainer width="100%" height={300}>
              <BarChart data={topQueues}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="name" />
                <YAxis />
                <Tooltip />
                <Bar dataKey="messages" fill="#FF6B6B" />
              </BarChart>
            </ResponsiveContainer>
          ) : (
            <Center h={300}>
              <Text c="dimmed">No queue data available</Text>
            </Center>
          )}
        </Paper>
      </Grid.Col>
    </Grid>
  );
}