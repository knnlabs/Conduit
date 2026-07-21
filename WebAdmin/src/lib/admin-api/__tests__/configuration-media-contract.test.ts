import { ConduitAdminClient } from '..';

const mockFetch: jest.MockedFunction<typeof fetch> = jest.fn();
global.fetch = mockFetch;

function response(body: unknown = {}, status = 200): Response {
  return { ok: status < 400, status, statusText: 'OK', url: 'https://admin.test',
    headers: new Headers(status === 204 ? undefined : { 'content-type': 'application/json' }),
    text: async () => status === 204 ? '' : JSON.stringify(body) } as Response;
}
function client() { return new ConduitAdminClient({ baseUrl: 'https://admin.test', masterKey: 'master-key', retries: 0 }); }
function lastRequest() { return mockFetch.mock.calls.at(-1)?.[0] as Request; }
function body(request: Request) { return JSON.parse(Reflect.get(request, 'body') as unknown as string) as unknown; }

beforeEach(() => mockFetch.mockReset());

describe('Configuration and prompt-caching generated operations', () => {
  const cases: Array<[string, string, string, (api: ReturnType<typeof client>) => Promise<unknown>, unknown?]> = [
    ['routing', 'GET', '/api/config/routing', a => a.configuration.getRoutingConfiguration()],
    ['defaults read', 'GET', '/api/config/routing/defaults', a => a.configuration.getRoutingDefaults()],
    ['defaults update', 'PUT', '/api/config/routing/defaults', a => a.configuration.updateRoutingDefaults({ costWeight: 1 }), { costWeight: 1 }],
    ['encoded alias read', 'GET', '/api/config/routing/aliases/a%2Fb%20c', a => a.configuration.getAliasRouting('a/b c')],
    ['encoded alias update', 'PUT', '/api/config/routing/aliases/a%2Fb%20c', a => a.configuration.updateAliasRouting('a/b c', { strategy: 'Balanced' }), { strategy: 'Balanced' }],
    ['prompt config', 'GET', '/api/prompt-caching/config', a => a.configuration.getPromptCachingConfig()],
    ['prompt update', 'PUT', '/api/prompt-caching/config', a => a.configuration.updatePromptCachingConfig({ schemaVersion: 3, enabled: false, rules: [] }), { schemaVersion: 3, enabled: false, rules: [] }],
    ['capabilities', 'GET', '/api/prompt-caching/capabilities', a => a.configuration.getPromptCachingCapabilities()],
    ['analytics filters', 'GET', '/api/prompt-caching/analytics?from=2026-01-01&alias=a%2Fb&mappingId=4', a => a.configuration.getPromptCachingAnalytics({ from: '2026-01-01', alias: 'a/b', mappingId: 4 })],
  ];
  test.each(cases)('%s', async (...args) => {
    const [, method, path, invoke, expectedBody] = args;
    mockFetch.mockResolvedValueOnce(response(method === 'GET' && path.endsWith('capabilities') ? [] : {}));
    await invoke(client());
    const request = lastRequest();
    expect(request.method).toBe(method); expect(request.url).toBe(`https://admin.test${path}`);
    expect(request.headers.get('X-Master-Key')).toBe('master-key');
    if (expectedBody !== undefined) expect(body(request)).toEqual(expectedBody);
  });
});

describe('Media and retention generated operations', () => {
  const cases: Array<[string, string, string, (api: ReturnType<typeof client>) => Promise<unknown>, unknown?, unknown?]> = [
    ['list', 'GET', '/api/admin/Media/virtual-key/7', a => a.media.getMediaByVirtualKey(7), undefined, []],
    ['overall stats', 'GET', '/api/admin/Media/stats?virtualKeyGroupId=3', a => a.media.getMediaStats('overall', undefined, 3)],
    ['key stats', 'GET', '/api/admin/Media/stats/virtual-key/7', a => a.media.getMediaStats('virtual-key', 7)],
    ['provider stats', 'GET', '/api/admin/Media/stats/by-provider', a => a.media.getMediaStats('by-provider')],
    ['type stats', 'GET', '/api/admin/Media/stats/by-type', a => a.media.getMediaStats('by-type')],
    ['search encoding', 'GET', '/api/admin/Media/search?pattern=a%2Fb%20c', a => a.media.searchMedia('a/b c'), undefined, []],
    ['delete encoding', 'DELETE', '/api/admin/Media/m%2F1', a => a.media.deleteMedia('m/1')],
    ['expired', 'POST', '/api/admin/Media/cleanup/expired', a => a.media.cleanupMedia({ type: 'expired' })],
    ['orphaned', 'POST', '/api/admin/Media/cleanup/orphaned', a => a.media.cleanupMedia({ type: 'orphaned' })],
    ['prune', 'POST', '/api/admin/Media/cleanup/prune', a => a.media.cleanupMedia({ type: 'prune', daysToKeep: 30 }), { daysToKeep: 30 }],
    ['status', 'GET', '/api/admin/media-cleanup/status', a => a.media.getCleanupServiceStatus()],
    ['enabled read', 'GET', '/api/admin/media-cleanup/enabled', a => a.media.getCleanupServiceEnabled()],
    ['enabled update', 'POST', '/api/admin/media-cleanup/enabled', a => a.media.setCleanupServiceEnabled(false), { enabled: false }],
    ['simple read', 'GET', '/api/admin/media-cleanup/simple-retention', a => a.media.getSimpleRetentionOverride()],
    ['simple update', 'POST', '/api/admin/media-cleanup/simple-retention', a => a.media.setSimpleRetentionOverride(null), { retentionDays: null }],
    ['policy list', 'GET', '/api/admin/media-retention/policies', a => a.media.getRetentionPolicies(), undefined, []],
    ['policy read', 'GET', '/api/admin/media-retention/policies/2', a => a.media.getRetentionPolicy(2)],
    ['policy create', 'POST', '/api/admin/media-retention/policies', a => a.media.createRetentionPolicy({ name: 'p', positiveBalanceRetentionDays: 3, zeroBalanceRetentionDays: 2, negativeBalanceRetentionDays: 1 }), { name: 'p', positiveBalanceRetentionDays: 3, zeroBalanceRetentionDays: 2, negativeBalanceRetentionDays: 1 }],
    ['policy patch-shaped update', 'PUT', '/api/admin/media-retention/policies/2', a => a.media.updateRetentionPolicy(2, { description: 'only' }), { description: 'only' }],
    ['policy delete', 'DELETE', '/api/admin/media-retention/policies/2', a => a.media.deleteRetentionPolicy(2), undefined, undefined],
    ['policy default', 'POST', '/api/admin/media-retention/policies/2/set-default', a => a.media.setDefaultRetentionPolicy(2)],
  ];
  test.each(cases)('%s', async (...args) => {
    const [, method, path, invoke, expectedBody, suppliedResult] = args;
    const result = suppliedResult ?? {};
    mockFetch.mockResolvedValueOnce(response(result, method === 'DELETE' && path.endsWith('/2') ? 204 : 200));
    await invoke(client()); const request = lastRequest();
    expect(request.method).toBe(method); expect(request.url).toBe(`https://admin.test${path}`);
    expect(request.headers.get('X-Master-Key')).toBe('master-key');
    if (expectedBody !== undefined) expect(body(request)).toEqual(expectedBody);
  });
});
