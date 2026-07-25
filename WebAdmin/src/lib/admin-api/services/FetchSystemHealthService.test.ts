import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import { FetchSystemHealthService } from './FetchSystemHealthService';

/**
 * The service health endpoint returns `{ timestamp, overallStatus, summary, services[] }`. The
 * previous mapping cast it to `{ coreApi, adminApi, database, cache }`, so every lookup was
 * `undefined` and `normalizeStatus` turned each one into `healthy` — the dashboard showed green
 * no matter what the backend reported (issue #1067).
 */
const serviceHealthResponse = {
  timestamp: '2026-07-25T12:00:00Z',
  overallStatus: 'unhealthy',
  summary: { healthy: 2, degraded: 1, unhealthy: 1, unknown: 1, total: 5 },
  services: [
    { id: 'core-api', name: 'Gateway API', status: 'unhealthy', responseTime: null },
    { id: 'admin-api', name: 'Admin API', status: 'healthy', responseTime: null },
    { id: 'database', name: 'PostgreSQL Database', status: 'healthy', responseTime: 3 },
    { id: 'redis', name: 'Redis', status: 'degraded', responseTime: 11 },
    { id: 'messaging', name: 'Messaging', status: 'unhealthy', responseTime: 42 },
  ],
};

const mockGetServiceHealth = jest.fn();
const mockGetHealth = jest.fn();

jest.mock('./FetchSystemService', () => ({
  FetchSystemService: jest.fn(() => ({
    getServiceHealth: mockGetServiceHealth,
    getHealth: mockGetHealth,
  })),
}));

jest.mock('./FetchSystemMetricsService', () => ({
  FetchSystemMetricsService: jest.fn(() => ({
    getActiveConnections: jest.fn().mockResolvedValue(0),
  })),
}));

/** Builds the service over a stub client exposing only the methods a test needs. */
function createService(clientOverrides: unknown = {}) {
  return new FetchSystemHealthService(clientOverrides as FetchBaseApiClient);
}

describe('FetchSystemHealthService.getServiceStatus', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGetServiceHealth.mockResolvedValue(serviceHealthResponse);
  });

  it('reports each service the status the backend actually returned', async () => {
    const status = await createService().getServiceStatus();

    expect(status.coreApi.status).toBe('unhealthy');
    expect(status.adminApi.status).toBe('healthy');
    expect(status.database.status).toBe('healthy');
    expect(status.cache.status).toBe('degraded');
    expect(status.queue.status).toBe('unhealthy');
  });

  it('does not report a downed Gateway as healthy', async () => {
    const status = await createService().getServiceStatus();

    // The regression this guards: an outage rendered green.
    expect(status.coreApi.status).not.toBe('healthy');
  });

  it('reports null latency when a status comes from a heartbeat rather than a probe', async () => {
    const status = await createService().getServiceStatus();

    // `0` would imply a probe that returned instantly; these services are not probed.
    expect(status.coreApi.latency).toBeNull();
    expect(status.adminApi.latency).toBeNull();
    expect(status.database.latency).toBe(3);
    expect(status.cache.latency).toBe(11);
  });

  it('reports unknown for a service missing from the response', async () => {
    mockGetServiceHealth.mockResolvedValue({ ...serviceHealthResponse, services: [] });

    const status = await createService().getServiceStatus();

    expect(status.coreApi.status).toBe('unknown');
    expect(status.adminApi.status).toBe('unknown');
    expect(status.queue.status).toBe('unknown');
  });

  it('does not fabricate connection counts or cache hit rates', async () => {
    const status = await createService().getServiceStatus();

    expect(status.database.connections).toBeNull();
    expect(status.cache.hitRate).toBeNull();
  });

  it('falls back to unknown for the Gateway when the endpoint is unreachable', async () => {
    mockGetServiceHealth.mockRejectedValue(new Error('unreachable'));
    mockGetHealth.mockResolvedValue({
      status: 'healthy',
      totalDuration: 7,
      checks: { database: { status: 'degraded', duration: 4 } },
    });

    const status = await createService().getServiceStatus();

    // The Admin's own readiness says nothing about the Gateway; it previously inherited it.
    expect(status.coreApi.status).toBe('unknown');
    expect(status.adminApi.status).toBe('healthy');
    expect(status.database.status).toBe('degraded');
    expect(status.cache.status).toBe('unknown');
  });
});

describe('FetchSystemHealthService.getSystemHealth', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGetServiceHealth.mockResolvedValue(serviceHealthResponse);
  });

  it('derives queue health from the messaging service instead of pinning it healthy', async () => {
    const health = await createService().getSystemHealth();

    expect(health.components.queue.status).toBe('unhealthy');
    expect(health.components.queue.message).not.toMatch(/processing normally/);
  });

  it('rolls unhealthy components up to an unhealthy overall status', async () => {
    const health = await createService().getSystemHealth();

    expect(health.overall).toBe('unhealthy');
  });

  it('treats unknown components as degrading, not healthy', async () => {
    mockGetServiceHealth.mockResolvedValue({
      ...serviceHealthResponse,
      services: [{ id: 'database', name: 'PostgreSQL Database', status: 'healthy', responseTime: 2 }],
    });

    const health = await createService().getSystemHealth();

    expect(health.components.api.status).toBe('unknown');
    expect(health.overall).toBe('degraded');
  });
});

describe('FetchSystemHealthService.getHealthEvents', () => {
  beforeEach(() => jest.clearAllMocks());

  it('derives events from real incidents with stable ids and reported timestamps', async () => {
    const get = jest.fn().mockResolvedValue({
      incidents: [
        {
          id: 'model-error-spike:gpt-4o:20260725T110000Z',
          title: 'Elevated error rate for model gpt-4o',
          severity: 'critical',
          affectedService: 'core-api',
          affectedModel: 'gpt-4o',
          impact: '80 failed requests in a 1 hour period',
          startTime: '2026-07-25T11:00:00Z',
          endTime: '2026-07-25T12:00:00Z',
        },
      ],
    });

    const events = await createService({ get })
      .getHealthEvents();

    expect(get).toHaveBeenCalledWith('/v1/admin/health-status/incidents', expect.anything());
    expect(events.events).toHaveLength(2);
    // Newest first: the recovery, then the onset — at the times the backend reported, not
    // `Math.random()` offsets from now.
    expect(events.events[0].timestamp).toBe('2026-07-25T12:00:00Z');
    expect(events.events[0].type).toBe('system_recovered');
    expect(events.events[1].timestamp).toBe('2026-07-25T11:00:00Z');
    expect(events.events[1].type).toBe('system_issue');
    expect(events.events[1].source).toBe('core-api');
    expect(events.events[1].id).toContain('model-error-spike:gpt-4o:20260725T110000Z');
  });

  it('emits no recovery event while an incident is still active', async () => {
    const get = jest.fn().mockResolvedValue({
      incidents: [
        {
          id: 'active-incident',
          title: 'Elevated error rate for model claude-opus-5',
          severity: 'minor',
          affectedService: 'core-api',
          startTime: '2026-07-25T12:00:00Z',
          endTime: null,
        },
      ],
    });

    const events = await createService({ get })
      .getHealthEvents();

    expect(events.events).toHaveLength(1);
    expect(events.events[0].type).toBe('system_issue');
  });

  it('returns ids that are stable across polls', async () => {
    const incident = {
      id: 'model-error-spike:gpt-4o:20260725T110000Z',
      title: 'Elevated error rate for model gpt-4o',
      severity: 'major',
      affectedService: 'core-api',
      startTime: '2026-07-25T11:00:00Z',
      endTime: '2026-07-25T12:00:00Z',
    };
    const get = jest.fn().mockResolvedValue({ incidents: [incident] });
    const service = createService({ get });

    const first = await service.getHealthEvents();
    const second = await service.getHealthEvents();

    expect(second.events.map(e => e.id)).toEqual(first.events.map(e => e.id));
  });

  it('returns no events when the incidents endpoint fails', async () => {
    const get = jest.fn().mockRejectedValue(new Error('boom'));

    const events = await createService({ get })
      .getHealthEvents();

    expect(events.events).toEqual([]);
  });
});
