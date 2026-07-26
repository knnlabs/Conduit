'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Accordion,
  Alert,
  Badge,
  Button,
  Card,
  Code,
  Container,
  Group,
  LoadingOverlay,
  Paper,
  SimpleGrid,
  Stack,
  Text,
  ThemeIcon,
  Title,
} from '@mantine/core';
import {
  IconActivityHeartbeat,
  IconAlertTriangle,
  IconDatabase,
  IconDownload,
  IconRefresh,
  IconServer,
  IconStack2,
} from '@tabler/icons-react';
import type { components } from '@/generated/admin-api';
import type { SystemInfoDto } from '@/lib/admin-api';
import { withAdminClient } from '@/lib/client/adminClient';
import { notify } from '@/lib/notifications';
import { getHealthColor } from '@/lib/utils/badge-helpers';
import { buildDiagnosticBundle } from './diagnosticBundle';

type ServiceHealthResponse = components['schemas']['ServiceHealthResponse'];
type ServiceStatusDto = components['schemas']['ServiceStatusDto'];
type ServiceInstanceStatusDto = components['schemas']['ServiceInstanceStatusDto'];

function statusBadge(status?: string) {
  const normalized = status ?? 'unknown';
  return (
    <Badge color={getHealthColor(normalized)} variant="light">
      {normalized}
    </Badge>
  );
}

function formatCommit(commit?: string) {
  if (!commit) return 'dev';
  return commit.length > 12 ? commit.slice(0, 12) : commit;
}

function detailsEntries(details: unknown): Array<[string, unknown]> {
  return details && typeof details === 'object'
    ? Object.entries(details as Record<string, unknown>)
    : [];
}

function formatDetail(value: unknown): string {
  if (value === null || value === undefined) return 'N/A';
  if (typeof value === 'object') return JSON.stringify(value);
  if (typeof value === 'string') return value;
  if (typeof value === 'number' || typeof value === 'boolean' || typeof value === 'bigint') {
    return String(value);
  }
  return 'N/A';
}

function InstanceList({
  service,
  emptyMessage,
}: {
  service?: ServiceStatusDto;
  emptyMessage: string;
}) {
  const instances = service?.instances ?? [];
  if (instances.length === 0) {
    return <Text size="sm" c="dimmed">{emptyMessage}</Text>;
  }
  return (
    <Stack gap="xs">
      {instances.map((instance: ServiceInstanceStatusDto) => (
        <Paper key={instance.instanceId} withBorder p="sm">
          <Group justify="space-between" align="flex-start">
            <div>
              <Group gap="xs">
                <Text fw={600}>{instance.instanceId}</Text>
                {statusBadge(instance.status)}
              </Group>
              <Text size="sm" c="dimmed">
                Version {instance.version ?? 'unknown'} · commit{' '}
                <Code>{formatCommit(instance.commitSha)}</Code>
              </Text>
            </div>
            <Stack gap={0} align="flex-end">
              <Text size="sm">Uptime {instance.uptime ?? 'unknown'}</Text>
              <Text size="xs" c="dimmed">
                heartbeat {instance.heartbeatAgeSeconds?.toFixed(1) ?? '?'}s ago
              </Text>
            </Stack>
          </Group>
          <Text size="xs" c="dimmed" mt="xs">
            Built {instance.buildTimestamp ?? 'unknown'} · last heartbeat{' '}
            {instance.lastHeartbeat
              ? new Date(instance.lastHeartbeat).toLocaleString()
              : 'unknown'}
          </Text>
        </Paper>
      ))}
    </Stack>
  );
}

function DependencyCard({ service }: { service?: ServiceStatusDto }) {
  return (
    <Card withBorder>
      <Group justify="space-between" mb="sm">
        <Text fw={700}>{service?.name ?? 'Unavailable dependency'}</Text>
        {statusBadge(service?.status)}
      </Group>
      {service?.version && <Text size="sm">Version {service.version}</Text>}
      {service?.responseTime !== null && service?.responseTime !== undefined && (
        <Text size="sm">Probe {service.responseTime} ms</Text>
      )}
      <Stack gap={3} mt="xs">
        {detailsEntries(service?.details).map(([key, value]) => (
          <Text key={key} size="xs" c="dimmed">
            {key}: {formatDetail(value)}
          </Text>
        ))}
      </Stack>
    </Card>
  );
}

export default function SystemInfoPage() {
  const [systemInfo, setSystemInfo] = useState<SystemInfoDto | null>(null);
  const [health, setHealth] = useState<ServiceHealthResponse | null>(null);
  const [metadataError, setMetadataError] = useState<string | null>(null);
  const [healthError, setHealthError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  const fetchDiagnostics = useCallback(async () => {
    const [metadataResult, healthResult] = await Promise.allSettled([
      withAdminClient(client => client.system.getSystemInfo()),
      withAdminClient(client => client.system.getServiceHealth()),
    ]);

    if (metadataResult.status === 'fulfilled') {
      setSystemInfo(metadataResult.value);
      setMetadataError(null);
    } else {
      setMetadataError(metadataResult.reason instanceof Error
        ? metadataResult.reason.message
        : 'System metadata is unavailable');
    }

    if (healthResult.status === 'fulfilled') {
      setHealth(healthResult.value);
      setHealthError(null);
    } else {
      setHealthError(healthResult.reason instanceof Error
        ? healthResult.reason.message
        : 'Service health is unavailable');
    }
    setLoading(false);
  }, []);

  useEffect(() => {
    void fetchDiagnostics();
  }, [fetchDiagnostics]);

  const refresh = async () => {
    setRefreshing(true);
    await fetchDiagnostics();
    setRefreshing(false);
    notify.success('System metadata and service health refreshed');
  };

  const services = useMemo(() => health?.services ?? [], [health?.services]);
  const service = useCallback(
    (id: string) => services.find(item => item.id === id),
    [services],
  );
  const gateway = service('core-api');
  const admin = service('admin-api');
  const instanceSummary = useMemo(() => {
    const summarize = (item?: ServiceStatusDto) => ({
      healthy: (item?.instances ?? []).filter(instance => instance.status === 'healthy').length,
      total: item?.instances?.length ?? 0,
    });
    return { gateway: summarize(gateway), admin: summarize(admin) };
  }, [admin, gateway]);

  const exportBundle = () => {
    const blob = new Blob(
      [JSON.stringify(buildDiagnosticBundle(systemInfo, health), null, 2)],
      { type: 'application/json' },
    );
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `conduit-diagnostics-${new Date().toISOString()}.json`;
    anchor.click();
    URL.revokeObjectURL(url);
    notify.success('Redacted diagnostics bundle exported');
  };

  return (
    <Container size="xl">
      <Stack gap="lg" pos="relative">
        <LoadingOverlay visible={loading || refreshing} />
        <Group justify="space-between">
          <div>
            <Title order={2}>System Information</Title>
            <Text c="dimmed">Live cluster health, build identity, and dependencies</Text>
          </div>
          <Group>
            <Button
              variant="light"
              leftSection={<IconRefresh size={16} />}
              onClick={() => void refresh()}
              loading={refreshing}
            >
              Refresh
            </Button>
            <Button leftSection={<IconDownload size={16} />} onClick={exportBundle}>
              Export diagnostics
            </Button>
          </Group>
        </Group>

        {healthError && (
          <Alert color="red" icon={<IconAlertTriangle size={16} />}>
            Service health could not be refreshed: {healthError}. Runtime metadata is still shown.
          </Alert>
        )}
        {metadataError && (
          <Alert color="yellow" icon={<IconAlertTriangle size={16} />}>
            Runtime metadata could not be refreshed: {metadataError}. Live service health is still shown.
          </Alert>
        )}

        <SimpleGrid cols={{ base: 1, sm: 3 }}>
          <Card withBorder>
            <Group justify="space-between">
              <div>
                <Text size="xs" tt="uppercase" c="dimmed" fw={700}>Overall health</Text>
                <Title order={3} tt="capitalize">{health?.overallStatus ?? 'unknown'}</Title>
              </div>
              <ThemeIcon
                size="xl"
                variant="light"
                color={getHealthColor(health?.overallStatus ?? 'unknown')}
              >
                <IconActivityHeartbeat size={24} />
              </ThemeIcon>
            </Group>
          </Card>
          <Card withBorder>
            <Group justify="space-between">
              <div>
                <Text size="xs" tt="uppercase" c="dimmed" fw={700}>Gateway instances</Text>
                <Title order={3}>
                  {instanceSummary.gateway.healthy}/{instanceSummary.gateway.total} healthy
                </Title>
              </div>
              <ThemeIcon size="xl" variant="light"><IconStack2 size={24} /></ThemeIcon>
            </Group>
          </Card>
          <Card withBorder>
            <Group justify="space-between">
              <div>
                <Text size="xs" tt="uppercase" c="dimmed" fw={700}>Admin instances</Text>
                <Title order={3}>
                  {instanceSummary.admin.healthy}/{instanceSummary.admin.total} healthy
                </Title>
              </div>
              <ThemeIcon size="xl" variant="light"><IconServer size={24} /></ThemeIcon>
            </Group>
          </Card>
        </SimpleGrid>

        <Card withBorder>
          <Title order={3} mb="sm">Application instances</Title>
          <Accordion multiple defaultValue={['gateway', 'admin']}>
            <Accordion.Item value="gateway">
              <Accordion.Control>
                <Group justify="space-between" pr="md">
                  <Text fw={600}>Gateway API</Text>
                  {statusBadge(gateway?.status)}
                </Group>
              </Accordion.Control>
              <Accordion.Panel>
                <InstanceList
                  service={gateway}
                  emptyMessage="No Gateway heartbeat has been received."
                />
              </Accordion.Panel>
            </Accordion.Item>
            <Accordion.Item value="admin">
              <Accordion.Control>
                <Group justify="space-between" pr="md">
                  <Text fw={600}>Admin API</Text>
                  {statusBadge(admin?.status)}
                </Group>
              </Accordion.Control>
              <Accordion.Panel>
                <InstanceList
                  service={admin}
                  emptyMessage="No Admin heartbeat has been recorded."
                />
              </Accordion.Panel>
            </Accordion.Item>
          </Accordion>
        </Card>

        <div>
          <Title order={3} mb="sm">Core dependencies</Title>
          <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }}>
            <DependencyCard service={service('database')} />
            <DependencyCard service={service('redis')} />
            <DependencyCard service={service('messaging')} />
            <DependencyCard service={service('media-storage')} />
          </SimpleGrid>
        </div>

        <SimpleGrid cols={{ base: 1, md: 2 }}>
          <Card withBorder>
            <Group mb="md">
              <ThemeIcon variant="light"><IconServer size={18} /></ThemeIcon>
              <Title order={3}>Admin runtime and build</Title>
            </Group>
            <Stack gap="xs">
              <Group justify="space-between">
                <Text c="dimmed">Version</Text>
                <Code>{systemInfo?.version?.appVersion ?? 'unknown'}</Code>
              </Group>
              <Group justify="space-between">
                <Text c="dimmed">Commit</Text>
                <Code>{formatCommit(systemInfo?.version?.commitSha)}</Code>
              </Group>
              <Group justify="space-between">
                <Text c="dimmed">Built</Text>
                <Text>{systemInfo?.version?.buildTimestamp ?? 'unknown'}</Text>
              </Group>
              <Group justify="space-between">
                <Text c="dimmed">Runtime</Text>
                <Text>{systemInfo?.runtime?.runtimeVersion ?? 'unknown'}</Text>
              </Group>
              <Group justify="space-between">
                <Text c="dimmed">Uptime</Text>
                <Text>{systemInfo?.runtime?.uptime ?? 'unknown'}</Text>
              </Group>
              <Group justify="space-between">
                <Text c="dimmed">Platform</Text>
                <Text ta="right">
                  {systemInfo?.operatingSystem?.description ?? 'unknown'} ·{' '}
                  {systemInfo?.operatingSystem?.architecture ?? 'unknown'}
                </Text>
              </Group>
            </Stack>
          </Card>

          <Card withBorder>
            <Group mb="md">
              <ThemeIcon variant="light"><IconDatabase size={18} /></ThemeIcon>
              <Title order={3}>Configuration inventory</Title>
            </Group>
            <SimpleGrid cols={2}>
              {[
                ['Virtual keys', systemInfo?.recordCounts?.virtualKeys],
                ['Global settings', systemInfo?.recordCounts?.settings],
                ['Providers', systemInfo?.recordCounts?.providers],
                ['Model mappings', systemInfo?.recordCounts?.modelMappings],
              ].map(([label, value]) => (
                <Paper key={String(label)} withBorder p="md">
                  <Text size="xs" c="dimmed">{label}</Text>
                  <Title order={3}>{value ?? 'N/A'}</Title>
                </Paper>
              ))}
            </SimpleGrid>
          </Card>
        </SimpleGrid>
      </Stack>
    </Container>
  );
}
