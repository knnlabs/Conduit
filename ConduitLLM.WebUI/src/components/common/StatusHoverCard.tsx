'use client';

import { 
  HoverCard, 
  Group, 
  Stack, 
  Text, 
  Badge, 
  Progress, 
  ThemeIcon,
  Box,
  Divider,
  Timeline,
  Paper
} from '@mantine/core';
import { 
  IconDatabase, 
  IconServer, 
  IconBrandReact,
  IconMessage2,
  IconCheck,
  IconX,
  IconAlertTriangle,
  IconClock,
  IconActivity
} from '@tabler/icons-react';
import type { HealthCheckDetail } from '@/types/health';

interface StatusHoverCardProps {
  children: React.ReactNode;
  status: 'healthy' | 'degraded' | 'unavailable';
  title: string;
  lastChecked?: Date;
  checks?: Record<string, HealthCheckDetail>;
  message?: string;
}

const getStatusColor = (status: string): string => {
  switch (status?.toLowerCase()) {
    case 'healthy':
      return 'green';
    case 'degraded':
      return 'yellow';
    case 'unhealthy':
    case 'unavailable':
      return 'red';
    default:
      return 'gray';
  }
};

const getStatusIcon = (status: string) => {
  switch (status?.toLowerCase()) {
    case 'healthy':
      return IconCheck;
    case 'degraded':
      return IconAlertTriangle;
    case 'unhealthy':
    case 'unavailable':
      return IconX;
    default:
      return IconActivity;
  }
};

const getComponentIcon = (name: string) => {
  const lowerName = name.toLowerCase();
  if (lowerName.includes('database') || lowerName.includes('db')) return IconDatabase;
  if (lowerName.includes('redis') || lowerName.includes('cache')) return IconBrandReact; // Using React icon as placeholder
  if (lowerName.includes('rabbit') || lowerName.includes('queue')) return IconMessage2;
  return IconServer;
};

const formatDuration = (ms?: number): string => {
  if (!ms) return 'N/A';
  if (ms < 1) return '<1ms';
  if (ms < 1000) return `${Math.round(ms)}ms`;
  return `${(ms / 1000).toFixed(2)}s`;
};

export function StatusHoverCard({ 
  children, 
  status, 
  title, 
  lastChecked, 
  checks = {}, 
  message 
}: StatusHoverCardProps) {
  const StatusIcon = getStatusIcon(status);
  const statusColor = getStatusColor(status);
  
  // Calculate overall stats
  const checkEntries = Object.entries(checks);
  const healthyCount = checkEntries.filter(([_, check]) => 
    check.status?.toLowerCase() === 'healthy'
  ).length;
  const totalChecks = checkEntries.length;
  const healthPercentage = totalChecks > 0 ? (healthyCount / totalChecks) * 100 : 0;

  return (
    <HoverCard width={380} shadow="md" withArrow>
      <HoverCard.Target>
        {children}
      </HoverCard.Target>
      <HoverCard.Dropdown>
        <Stack gap="md">
          {/* Header */}
          <Group justify="space-between">
            <Group gap="sm">
              <ThemeIcon size="lg" color={statusColor} variant="light">
                <StatusIcon size={20} />
              </ThemeIcon>
              <div>
                <Text fw={600} size="sm">{title}</Text>
                <Text size="xs" c="dimmed">
                  {status.charAt(0).toUpperCase() + status.slice(1)}
                </Text>
              </div>
            </Group>
            <Badge color={statusColor} variant="dot" size="sm">
              {status.toUpperCase()}
            </Badge>
          </Group>

          {/* Message if provided */}
          {message && (
            <>
              <Divider />
              <Text size="sm" c="dimmed">{message}</Text>
            </>
          )}

          {/* Health Overview */}
          {totalChecks > 0 && (
            <>
              <Divider />
              <Box>
                <Group justify="space-between" mb="xs">
                  <Text size="xs" fw={500}>Health Overview</Text>
                  <Text size="xs" c="dimmed">
                    {healthyCount}/{totalChecks} healthy
                  </Text>
                </Group>
                <Progress 
                  value={healthPercentage} 
                  color={healthPercentage === 100 ? 'green' : healthPercentage >= 50 ? 'yellow' : 'red'}
                  size="sm"
                  radius="xl"
                />
              </Box>
            </>
          )}

          {/* Component Details */}
          {checkEntries.length > 0 && (
            <>
              <Divider />
              <Timeline active={-1} bulletSize={24} lineWidth={2}>
                {checkEntries.map(([name, check]) => {
                  const ComponentIcon = getComponentIcon(name);
                  const checkColor = getStatusColor(check.status);
                  const CheckStatusIcon = getStatusIcon(check.status);
                  
                  return (
                    <Timeline.Item
                      key={name}
                      bullet={
                        <ThemeIcon size={24} color={checkColor} variant="light" radius="xl">
                          <ComponentIcon size={14} />
                        </ThemeIcon>
                      }
                      title={
                        <Group justify="space-between">
                          <Text size="sm" fw={500}>{name}</Text>
                          <Badge 
                            leftSection={<CheckStatusIcon size={10} />} 
                            color={checkColor} 
                            variant="light" 
                            size="xs"
                          >
                            {check.status}
                          </Badge>
                        </Group>
                      }
                    >
                      <Paper p="xs" withBorder radius="sm" mb="sm">
                        <Stack gap={4}>
                          {check.description && (
                            <Text size="xs" c="dimmed">{check.description}</Text>
                          )}
                          <Group gap="xs">
                            <IconClock size={12} />
                            <Text size="xs" c="dimmed">
                              Response time: {formatDuration(check.duration)}
                            </Text>
                          </Group>
                          {check.error && (
                            <Text size="xs" c="red">Error: {check.error}</Text>
                          )}
                        </Stack>
                      </Paper>
                    </Timeline.Item>
                  );
                })}
              </Timeline>
            </>
          )}

          {/* Last Checked */}
          {lastChecked && (
            <>
              <Divider />
              <Group gap="xs">
                <IconClock size={14} />
                <Text size="xs" c="dimmed">
                  Last checked: {lastChecked.toLocaleTimeString()}
                </Text>
              </Group>
            </>
          )}
        </Stack>
      </HoverCard.Dropdown>
    </HoverCard>
  );
}