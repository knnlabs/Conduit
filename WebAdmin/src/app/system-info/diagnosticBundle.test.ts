import { buildDiagnosticBundle } from './diagnosticBundle';

describe('buildDiagnosticBundle', () => {
  it('constructs a narrow bundle and removes secret-like dependency details', () => {
    const bundle = buildDiagnosticBundle(
      {
        version: {
          appVersion: '3.0.0',
          commitSha: 'abc123',
          buildTimestamp: '2026-07-23T17:00:00Z',
        },
        database: {
          provider: 'PostgreSQL',
          connected: true,
          connectionString: 'Host=secret',
        },
        recordCounts: {
          virtualKeys: 2,
          settings: 3,
          providers: 4,
          modelMappings: 5,
        },
      },
      {
        timestamp: '2026-07-23T18:00:00Z',
        overallStatus: 'degraded',
        summary: { healthy: 5, degraded: 1, unhealthy: 0, unknown: 0, total: 6 },
        services: [{
          id: 'redis',
          name: 'Redis',
          status: 'healthy',
          details: {
            pingMilliseconds: 1,
            connectionString: 'redis-secret',
            apiKey: 'secret-key',
            nested: { password: 'secret-password', mode: 'cluster' },
          },
        }],
      },
      '2026-07-23T18:00:00Z',
    );

    const serialized = JSON.stringify(bundle);
    expect(serialized).not.toContain('Host=secret');
    expect(serialized).not.toContain('redis-secret');
    expect(serialized).not.toContain('secret-key');
    expect(serialized).not.toContain('secret-password');
    expect(serialized).not.toContain('connectionString');
    expect(serialized).toContain('pingMilliseconds');
    expect(bundle.inventory).toEqual({
      virtualKeys: 2,
      globalSettings: 3,
      providers: 4,
      modelMappings: 5,
    });
  });
});
