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
    mockFetch.mockResolvedValueOnce(response([setting]));

    await expect(client().settings.getGlobalSettings()).resolves.toEqual({
      settings: [setting],
      categories: [],
      lastModified: setting.updatedAt,
    });

    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request.method).toBe('GET');
    expect(request.url).toBe('https://admin.test/api/GlobalSettings');
    expect(request.headers.get('X-Master-Key')).toBe('master-key');
  });

  it('gets and URL-encodes a setting key', async () => {
    mockFetch.mockResolvedValueOnce(response(setting));
    await expect(client().settings.getGlobalSetting('Routing/Alias value')).resolves.toEqual(setting);
    expect((mockFetch.mock.calls[0]?.[0] as Request).url)
      .toBe('https://admin.test/api/GlobalSettings/by-key/Routing%2FAlias%20value');
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
    expect(request.url).toBe('https://admin.test/api/GlobalSettings');
    expect(requestBody(request)).toEqual(body);
  });

  it('updates a setting by key', async () => {
    mockFetch.mockResolvedValueOnce(response(undefined, 204));
    await expect(client().settings.updateGlobalSetting('Feature.Enabled', 'false', 'Toggle')).resolves.toBeUndefined();
    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request.method).toBe('PUT');
    expect(request.url).toBe('https://admin.test/api/GlobalSettings/by-key');
    expect(requestBody(request)).toEqual({ key: 'Feature.Enabled', value: 'false', description: 'Toggle' });
  });

  it('deletes a setting by encoded key', async () => {
    mockFetch.mockResolvedValueOnce(response(undefined, 204));
    await expect(client().settings.deleteGlobalSetting('Feature/Enabled')).resolves.toBeUndefined();
    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request.method).toBe('DELETE');
    expect(request.url).toBe('https://admin.test/api/GlobalSettings/by-key/Feature%2FEnabled');
  });

  it('gets cache statistics', async () => {
    const stats = { cacheSize: 1, cacheHits: 2, cacheMisses: 3, invalidations: 4, hitRate: 40, lastLoadTime: setting.updatedAt, cachedKeys: [setting.key] };
    mockFetch.mockResolvedValueOnce(response(stats));
    await expect(client().settings.getCacheStats()).resolves.toEqual(stats);
    expect((mockFetch.mock.calls[0]?.[0] as Request).url).toBe('https://admin.test/api/GlobalSettings/cache/stats');
  });

  it('reloads the cache', async () => {
    mockFetch.mockResolvedValueOnce(response(undefined, 204));
    await expect(client().settings.reloadCache()).resolves.toBeUndefined();
    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request.method).toBe('POST');
    expect(request.url).toBe('https://admin.test/api/GlobalSettings/cache/reload');
  });

  it('invalidates an encoded cache key', async () => {
    mockFetch.mockResolvedValueOnce(response(undefined, 204));
    await expect(client().settings.invalidateSetting('Feature/Enabled')).resolves.toBeUndefined();
    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request.method).toBe('POST');
    expect(request.url).toBe('https://admin.test/api/GlobalSettings/cache/invalidate/Feature%2FEnabled');
  });
});
