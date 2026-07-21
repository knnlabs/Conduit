import { ConduitAdminClient } from '..';

const mockFetch: jest.MockedFunction<typeof fetch> = jest.fn();
global.fetch = mockFetch;

function response(body: unknown): Response {
  return { ok: true, status: 200, statusText: 'OK', url: 'https://admin.test', headers: new Headers({ 'content-type': 'application/json' }), text: async () => JSON.stringify(body) } as Response;
}

function client() {
  return new ConduitAdminClient({ baseUrl: 'https://admin.test', masterKey: 'master-key', retries: 0 });
}

function requestBody(request: Request): unknown {
  return JSON.parse(Reflect.get(request, 'body') as unknown as string) as unknown;
}

beforeEach(() => mockFetch.mockReset());

describe('provider sync generated operations', () => {
  const cases = [
    ['list drift', 'GET', '/api/ProviderSync/drift?status=Pending&providerId=3&page=2&pageSize=50', undefined, (c: ConduitAdminClient) => c.providerSync.listDrift({ status: 'Pending', providerId: 3, page: 2, pageSize: 50 })],
    ['get drift', 'GET', '/api/ProviderSync/drift/7', undefined, (c: ConduitAdminClient) => c.providerSync.getDrift(7)],
    ['apply drift', 'POST', '/api/ProviderSync/drift/7/apply', undefined, (c: ConduitAdminClient) => c.providerSync.apply(7)],
    ['dismiss drift', 'POST', '/api/ProviderSync/drift/7/dismiss', undefined, (c: ConduitAdminClient) => c.providerSync.dismiss(7)],
    ['bulk apply', 'POST', '/api/ProviderSync/drift/bulk/apply', { ids: [7, 8] }, (c: ConduitAdminClient) => c.providerSync.applyBulk([7, 8])],
    ['bulk dismiss', 'POST', '/api/ProviderSync/drift/bulk/dismiss', { ids: [7, 8] }, (c: ConduitAdminClient) => c.providerSync.dismissBulk([7, 8])],
    ['run', 'POST', '/api/ProviderSync/run', undefined, (c: ConduitAdminClient) => c.providerSync.run()],
    ['runs', 'GET', '/api/ProviderSync/runs?page=2&pageSize=25', undefined, (c: ConduitAdminClient) => c.providerSync.listRuns(2, 25)],
  ] as const;

  it.each(cases)('%s', async (name, method, path, body, invoke) => {
    expect(name).toBeTruthy();
    const payload = [{ id: 7 }];
    mockFetch.mockResolvedValueOnce(response(payload));
    await expect(invoke(client())).resolves.toEqual(payload);
    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request.method).toBe(method);
    expect(request.url).toBe(`https://admin.test${path}`);
    expect(request.headers.get('X-Master-Key')).toBe('master-key');
    if (body) expect(requestBody(request)).toEqual(body);
  });
});

describe('provider error generated operations', () => {
  const clear = { reenableKey: true, confirmReenable: true, reason: 'fixed' };
  const cases = [
    ['recent', 'GET', '/api/provider-errors/recent?providerId=3&keyId=4&limit=20', undefined, (c: ConduitAdminClient) => c.providerErrors.getRecentErrors({ providerId: 3, keyId: 4, limit: 20 })],
    ['summary', 'GET', '/api/provider-errors/summary', undefined, (c: ConduitAdminClient) => c.providerErrors.getSummary()],
    ['stats', 'GET', '/api/provider-errors/stats?hours=48', undefined, (c: ConduitAdminClient) => c.providerErrors.getStatistics(48)],
    ['key details', 'GET', '/api/provider-errors/keys/4', undefined, (c: ConduitAdminClient) => c.providerErrors.getKeyErrors(4)],
    ['clear', 'POST', '/api/provider-errors/keys/4/clear', clear, (c: ConduitAdminClient) => c.providerErrors.clearKeyErrors(4, clear)],
  ] as const;

  it.each(cases)('%s', async (name, method, path, body, invoke) => {
    expect(name).toBeTruthy();
    const payload = { message: 'ok', keyId: 4, reenabled: true };
    mockFetch.mockResolvedValueOnce(response(payload));
    await expect(invoke(client())).resolves.toEqual(payload);
    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request.method).toBe(method);
    expect(request.url).toBe(`https://admin.test${path}`);
    expect(request.headers.get('X-Master-Key')).toBe('master-key');
    if (body) expect(requestBody(request)).toEqual(body);
  });
});
