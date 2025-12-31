'use client';

import { useEffect } from 'react';
import {
  Stack,
  Title,
  Text,
  Card,
  Button,
  Group,
  Alert,
} from '@mantine/core';
import {
  IconChartBar,
  IconExternalLink,
  IconInfoCircle,
} from '@tabler/icons-react';

export default function UsageAnalyticsPage() {
  useEffect(() => {
    // Auto-redirect after 3 seconds
    const timer = setTimeout(() => {
      window.location.href = 'http://localhost:3001';
    }, 3000);

    return () => clearTimeout(timer);
  }, []);

  return (
    <Stack gap="xl" maw={600} mx="auto" mt="xl">
      <div>
        <Title order={1}>Usage Analytics</Title>
        <Text c="dimmed">Redirecting to Grafana Dashboard...</Text>
      </div>

      <Alert icon={<IconInfoCircle size={16} />} color="blue">
        We&apos;ve upgraded to Grafana for better analytics and monitoring. You&apos;ll be redirected automatically in a few seconds.
      </Alert>

      <Card withBorder padding="lg">
        <Card.Section withBorder inheritPadding pb="md">
          <Group justify="space-between">
            <Text fw={500}>Grafana Analytics Dashboard</Text>
            <IconChartBar size={20} />
          </Group>
        </Card.Section>

        <Card.Section inheritPadding pt="md">
          <Stack gap="md">
            <Text size="sm" c="dimmed">
              Grafana provides powerful visualization and monitoring capabilities including:
            </Text>
            <ul style={{ marginTop: 0 }}>
              <li>Real-time metrics and usage tracking</li>
              <li>Historical data analysis</li>
              <li>Custom dashboards and alerts</li>
              <li>Advanced querying and filtering</li>
            </ul>
            
            <Button
              fullWidth
              leftSection={<IconExternalLink size={16} />}
              onClick={() => { window.location.href = 'http://localhost:3001'; }}
            >
              Open Grafana Dashboard
            </Button>

            <Text size="xs" c="dimmed" ta="center">
              Default credentials: admin / conduitadmin
            </Text>
          </Stack>
        </Card.Section>
      </Card>
    </Stack>
  );
}