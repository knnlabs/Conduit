'use client';

import { useEffect, useMemo, useState } from 'react';
import {
  Box,
  Button,
  Group,
  Loader,
  Select,
  Stack,
  Text,
  Title,
} from '@mantine/core';
import { IconExternalLink, IconRefresh } from '@tabler/icons-react';
import { useTheme } from '@/contexts/ThemeContext';

const dashboards = [
  { value: 'conduit-overview/conduit-api-overview', label: 'Conduit API Overview' },
  { value: 'conduit-provider-performance/llm-provider-performance', label: 'LLM Provider Performance' },
  { value: 'conduit-virtual-keys/virtual-key-analytics', label: 'Virtual Key Analytics' },
  { value: 'conduit-request-pipeline/request-pipeline', label: 'Request Pipeline' },
  { value: 'conduit-prompt-caching/prompt-caching-analytics', label: 'Prompt Caching Analytics' },
  { value: 'conduit-infrastructure/infrastructure', label: 'Infrastructure' },
  { value: 'conduit-postgresql/postgresql', label: 'PostgreSQL' },
] as const;

const defaultDashboard = dashboards[0].value;

export default function UsageAnalyticsPage() {
  const { colorScheme } = useTheme();
  const [dashboard, setDashboard] = useState<string>(defaultDashboard);
  const [loaded, setLoaded] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  const dashboardUrl = useMemo(
    () => `/grafana/d/${dashboard}?kiosk&refresh=30s&theme=${colorScheme}`,
    [colorScheme, dashboard]
  );

  useEffect(() => {
    setLoaded(false);
  }, [dashboardUrl, reloadKey]);

  return (
    <Stack gap="md" h="calc(100vh - 120px)" mih={700}>
      <Group justify="space-between" align="flex-end">
        <div>
          <Title order={1}>Usage Analytics</Title>
          <Text c="dimmed">Live operational and usage metrics from Grafana</Text>
        </div>

        <Group align="flex-end">
          <Select
            label="Dashboard"
            aria-label="Dashboard"
            data={dashboards}
            value={dashboard}
            onChange={(value) => value && setDashboard(value)}
            allowDeselect={false}
            w={260}
          />
          <Button
            variant="default"
            aria-label="Reload dashboard"
            leftSection={<IconRefresh size={16} />}
            onClick={() => setReloadKey((value) => value + 1)}
          >
            Reload
          </Button>
          <Button
            component="a"
            href={dashboardUrl}
            target="_blank"
            rel="noopener noreferrer"
            leftSection={<IconExternalLink size={16} />}
          >
            Open in Grafana
          </Button>
        </Group>
      </Group>

      <Box
        pos="relative"
        style={{ flex: 1, overflow: 'hidden', borderRadius: 'var(--mantine-radius-md)' }}
      >
        {!loaded && (
          <Group
            justify="center"
            pos="absolute"
            inset={0}
            bg="var(--mantine-color-body)"
            style={{ zIndex: 1 }}
          >
            <Loader aria-label="Loading Grafana dashboard" />
          </Group>
        )}
        <iframe
          key={`${dashboardUrl}-${reloadKey}`}
          title="Grafana usage analytics dashboard"
          src={dashboardUrl}
          width="100%"
          height="100%"
          onLoad={() => setLoaded(true)}
          style={{ border: 0, display: 'block', background: 'transparent' }}
        />
      </Box>
    </Stack>
  );
}
