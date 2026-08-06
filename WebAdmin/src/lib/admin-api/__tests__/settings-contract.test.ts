import { ConduitAdminClient } from '..';

const mockFetch: jest.MockedFunction<typeof fetch> = jest.fn();
global.fetch = mockFetch;

function response(body?: unknown, status = 200): Response {
  const hasBody = status !== 204;
  return {
    ok: status >= 200 && status < 300,
    status,
    statusText: status === 204 ? 'No Content' : 'OK',
    url: 'https://admin.test',
    headers: new Headers(hasBody ? { 'content-type': 'application/json' } : undefined),
    text: async () => hasBody ? JSON.stringify(body) : '',
  } as Response;
}

function client() {
  return new ConduitAdminClient({ baseUrl: 'https://admin.test', masterKey: 'master-key', retries: 0 });
}

function requestBody(request: Request): unknown {
  return JSON.parse(Reflect.get(request, 'body') as unknown as string) as unknown;
}

beforeEach(() => mockFetch.mockReset());

describe('Global Settings generated operations', () => {
  const setting = {
    id: 1,
    key: 'Routing.Defaults',
    value: '{}',
    description: null,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-02-01T00:00:00Z',
  };

  it('preserves the settings aggregation facade over the generated list operation', async () => {
    mockFetch.mockResolvedValueOnce(response({
      data: [setting],
      pagination: { page: 1, pageSize: 100, totalItems: 1, totalPages: 1 },
    }));

    await expect(client().settings.getGlobalSettings()).resolves.toEqual({
      settings: [setting],
      categories: [],
      lastModified: setting.updatedAt,
    });

    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request.method).toBe('GET');
    expect(request.url).toBe('https://admin.test/v1/admin/global-settings?page=1&pageSize=100');
    expect(request.headers.get('X-Master-Key')).toBe('master-key');
  });

  it('loads every settings page for the Advanced editor', async () => {
    const secondSetting = {
      ...setting,
      id: 2,
      key: 'Custom.SecondPage',
    };
    mockFetch
      .mockResolvedValueOnce(response({
        data: [setting],
        pagination: { page: 1, pageSize: 100, totalItems: 2, totalPages: 2 },
      }))
      .mockResolvedValueOnce(response({
        data: [secondSetting],
        pagination: { page: 2, pageSize: 100, totalItems: 2, totalPages: 2 },
      }));

    const result = await client().settings.getGlobalSettings();

    expect(result.settings).toEqual([setting, secondSetting]);
    expect((mockFetch.mock.calls[1]?.[0] as Request).url)
      .toBe('https://admin.test/v1/admin/global-settings?page=2&pageSize=100');
  });

  it('gets and URL-encodes a setting key', async () => {
    mockFetch.mockResolvedValueOnce(response(setting));
    await expect(client().settings.getGlobalSetting('Routing/Alias value')).resolves.toEqual(setting);
    expect((mockFetch.mock.calls[0]?.[0] as Request).url)
      .toBe('https://admin.test/v1/admin/global-settings/by-key/Routing%2FAlias%20value');
  });

  it('preserves the settingExists 404 adapter', async () => {
    mockFetch.mockResolvedValueOnce(response({ title: 'Not Found' }, 404));
    await expect(client().settings.settingExists('Missing.Key')).resolves.toBe(false);
  });

  it('creates a setting', async () => {
    const body = { key: 'Feature.Enabled', value: 'true', description: 'Toggle' };
    mockFetch.mockResolvedValueOnce(response(setting, 201));
    await expect(client().settings.createGlobalSetting(body)).resolves.toEqual(setting);
    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request.method).toBe('POST');
    expect(request.url).toBe('https://admin.test/v1/admin/global-settings');
    expect(requestBody(request)).toEqual(body);
  });

  it('updates a setting by key', async () => {
    mockFetch.mockResolvedValueOnce(response(undefined, 204));
    await expect(client().settings.updateGlobalSetting('Feature.Enabled', 'false', 'Toggle')).resolves.toBeUndefined();
    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request.method).toBe('PUT');
    expect(request.url).toBe('https://admin.test/v1/admin/global-settings/by-key');
    expect(requestBody(request)).toEqual({ key: 'Feature.Enabled', value: 'false', description: 'Toggle' });
  });

  it('deletes a setting by encoded key', async () => {
    mockFetch.mockResolvedValueOnce(response(undefined, 204));
    await expect(client().settings.deleteGlobalSetting('Feature/Enabled')).resolves.toBeUndefined();
    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request.method).toBe('DELETE');
    expect(request.url).toBe('https://admin.test/v1/admin/global-settings/by-key/Feature%2FEnabled');
  });

  it('gets cache statistics', async () => {
    const stats = {
      hitCount: 2,
      missCount: 3,
      invalidationCount: 4,
      hitRate: 0.4,
      averageGetTime: '00:00:00',
      lastResetTime: setting.updatedAt,
      lastInvalidationTime: null,
      entryCount: 1,
      patternMatchCount: 0,
      isEnabled: true,
      cachedKeys: [setting.key],
    };
    mockFetch.mockResolvedValueOnce(response(stats));
    await expect(client().settings.getCacheStats()).resolves.toEqual(stats);
    expect((mockFetch.mock.calls[0]?.[0] as Request).url).toBe('https://admin.test/v1/admin/global-settings/cache/stats');
  });

  it('gets typed setting definitions', async () => {
    const definitions = [{
      key: 'Agentic.MaxIterations',
      displayName: 'Maximum agentic iterations',
      description: 'Maximum loop count',
      type: 'integer',
      category: 'Agentic',
      defaultValue: '5',
      minimum: 1,
      maximum: 100,
      isFeatureOwned: false,
    }];
    mockFetch.mockResolvedValueOnce(response({
      data: definitions,
      pagination: { page: 1, pageSize: 50, totalItems: 1, totalPages: 1 },
    }));

    await expect(client().settings.getDefinitions()).resolves.toEqual(definitions);
    expect((mockFetch.mock.calls[0]?.[0] as Request).url)
      .toBe('https://admin.test/v1/admin/global-settings/definitions?page=1&pageSize=100');
  });

  it('reloads the cache', async () => {
    const accepted = {
      message: 'Reload accepted',
      requestId: 'reload-123',
      acceptedAt: '2026-07-23T18:00:00Z',
    };
    mockFetch.mockResolvedValueOnce(response(accepted, 202));
    await expect(client().settings.reloadCache()).resolves.toEqual(accepted);
    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request.method).toBe('POST');
    expect(request.url).toBe('https://admin.test/v1/admin/global-settings/cache/reload');
  });

  it('invalidates an encoded cache key', async () => {
    mockFetch.mockResolvedValueOnce(response(undefined, 204));
    await expect(client().settings.invalidateSetting('Feature/Enabled')).resolves.toBeUndefined();
    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request.method).toBe('POST');
    expect(request.url).toBe('https://admin.test/v1/admin/global-settings/cache/invalidate/Feature%2FEnabled');
  });
});
