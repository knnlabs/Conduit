import type { components } from '@/generated/admin-api';
import type { SystemInfoDto } from '@/lib/admin-api';

type ServiceHealthResponse = components['schemas']['ServiceHealthResponse'];
type ServiceStatusDto = components['schemas']['ServiceStatusDto'];

const blockedKey = /(connection|string|password|secret|token|credential|api.?key)/i;

function redactDetails(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(redactDetails);
  if (!value || typeof value !== 'object') return value;
  return Object.fromEntries(
    Object.entries(value as Record<string, unknown>)
      .filter(([key]) => !blockedKey.test(key))
      .map(([key, child]) => [key, redactDetails(child)]),
  );
}

function instanceSummary(service: ServiceStatusDto | undefined) {
  return (service?.instances ?? []).map(instance => ({
    instanceId: instance.instanceId,
    status: instance.status,
    version: instance.version,
    commitSha: instance.commitSha,
    buildTimestamp: instance.buildTimestamp,
    uptime: instance.uptime,
    lastHeartbeat: instance.lastHeartbeat,
    heartbeatAgeSeconds: instance.heartbeatAgeSeconds,
  }));
}

/** Builds the intentionally narrow, secret-free diagnostics export contract. */
export function buildDiagnosticBundle(
  systemInfo: SystemInfoDto | null,
  health: ServiceHealthResponse | null,
  generatedAt = new Date().toISOString(),
) {
  const services = health?.services ?? [];
  const find = (id: string) => services.find(service => service.id === id);
  const dependencies = services
    .filter(service => ['redis', 'messaging', 'media-storage'].includes(service.id ?? ''))
    .map(service => ({
      id: service.id,
      name: service.name,
      status: service.status,
      version: service.version,
      lastCheck: service.lastCheck,
      responseTime: service.responseTime,
      details: redactDetails(service.details),
    }));

  return {
    generatedAt,
    overallStatus: health?.overallStatus ?? 'unknown',
    build: {
      admin: {
        version: systemInfo?.version?.appVersion ?? 'unknown',
        commitSha: systemInfo?.version?.commitSha ?? 'dev',
        buildTimestamp: systemInfo?.version?.buildTimestamp ?? 'unknown',
      },
    },
    runtime: {
      framework: systemInfo?.runtime?.runtimeVersion ?? 'unknown',
      startTime: systemInfo?.runtime?.startTime ?? null,
      uptime: systemInfo?.runtime?.uptime ?? null,
      operatingSystem: systemInfo?.operatingSystem?.description ?? 'unknown',
      architecture: systemInfo?.operatingSystem?.architecture ?? 'unknown',
      customerMode: systemInfo?.runtime?.customerMode ?? 'External',
    },
    instances: {
      gateway: instanceSummary(find('core-api')),
      admin: instanceSummary(find('admin-api')),
    },
    dependencies,
    database: {
      health: find('database')?.status ?? 'unknown',
      provider: systemInfo?.database?.provider ?? 'unknown',
      version: systemInfo?.database?.version ?? find('database')?.version ?? 'unknown',
      connected: systemInfo?.database?.connected ?? false,
      location: systemInfo?.database?.location ?? 'unknown',
      size: systemInfo?.database?.size ?? 'unknown',
      tableCount: systemInfo?.database?.tableCount ?? 0,
    },
    inventory: {
      virtualKeys: systemInfo?.recordCounts?.virtualKeys ?? 0,
      globalSettings: systemInfo?.recordCounts?.settings ?? 0,
      providers: systemInfo?.recordCounts?.providers ?? 0,
      modelMappings: systemInfo?.recordCounts?.modelMappings ?? 0,
    },
  };
}
