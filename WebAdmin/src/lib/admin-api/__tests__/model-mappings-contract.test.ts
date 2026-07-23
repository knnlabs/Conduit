import { NotFoundError } from '@/lib/conduit-common';

import { ConduitAdminClient } from '..';
import type { RequestConfigInfo, ResponseInfo } from '../client/types';

const mockFetch: jest.MockedFunction<typeof fetch> = jest.fn();
const TRACE_HEADER = 'X-Trace';
global.fetch = mockFetch;

function response(body: unknown, status = 200): Response {
  const headers = new Headers({ 'content-type': 'application/json' });
  if (status === 204) headers.set('content-length', '0');
  let statusText = 'Error';
  if (status === 204) statusText = 'No Content';
  else if (status < 300) statusText = 'OK';
  return {
    ok: status >= 200 && status < 300,
    status,
    statusText,
    url: 'https://admin.test',
    headers,
    text: async () => status === 204 ? '' : JSON.stringify(body),
  } as Response;
}

function client(overrides: Partial<ConstructorParameters<typeof ConduitAdminClient>[0]> = {}) {
  return new ConduitAdminClient({
    baseUrl: 'https://admin.test', masterKey: 'master-key', retries: 0, ...overrides,
  });
}

function body(request: Request): unknown {
  const value = Reflect.get(request, 'body') as unknown;
  if (value === null || value === undefined) return undefined;
  if (typeof value !== 'string') throw new TypeError('Expected a serialized Request body');
  return JSON.parse(value) as unknown;
}

const createRequest = {
  modelAlias: 'nova', providerId: 9, providerModelId: 'provider/nova',
  modelProviderTypeAssociationId: 17, priority: 100, weight: 1, isEnabled: true,
};
const updateRequest = { ...createRequest, priority: 50, weight: 1.2, isEnabled: false };
const mapping = {
  id: 7, ...createRequest,
  createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-02T00:00:00Z',
  provider: null, providerOptions: null, capabilities: null,
};

beforeEach(() => mockFetch.mockReset());

describe('contract-native top-level model mappings', () => {
  const bulkItem = { modelAlias: 'nova', providerId: 9, providerModelId: 'provider/nova' };
  const bulkRequest = { mappings: [bulkItem], priority: 100, weight: 1, isEnabled: true };
  const bulkPreview = {
    items: [{ index: 0, ...bulkItem, modelProviderTypeAssociationId: 17, hasConflict: false }],
    totalProcessed: 1,
    conflictCount: 0,
  };
  const bulkCreate = {
    created: [mapping], existing: [], failed: [], totalProcessed: 1,
    createdCount: 1, existingCount: 0, successCount: 1, failureCount: 0,
    isSuccess: true, isPartialSuccess: false,
  };
  const bulkDelete = { deletedIds: [7], errors: [], totalProcessed: 1, successCount: 1, failureCount: 0 };
  const bulkUpdate = { updated: [mapping], errors: [], totalProcessed: 1, successCount: 1, failureCount: 0 };

  const cases = [
    ['list', 'GET', '/api/ModelProviderMapping', undefined, [mapping], 200,
      (api: ConduitAdminClient) => api.modelMappings.list()],
    ['getById', 'GET', '/api/ModelProviderMapping/7', undefined, mapping, 200,
      (api: ConduitAdminClient) => api.modelMappings.getById(7)],
    ['create', 'POST', '/api/ModelProviderMapping', createRequest, mapping, 201,
      (api: ConduitAdminClient) => api.modelMappings.create(createRequest)],
    ['update', 'PUT', '/api/ModelProviderMapping/7', updateRequest, undefined, 204,
      (api: ConduitAdminClient) => api.modelMappings.update(7, updateRequest)],
    ['deleteById', 'DELETE', '/api/ModelProviderMapping/7', undefined, undefined, 204,
      (api: ConduitAdminClient) => api.modelMappings.deleteById(7)],
    ['previewBulk', 'POST', '/api/ModelProviderMapping/bulk/preview', bulkRequest, bulkPreview, 200,
      (api: ConduitAdminClient) => api.modelMappings.previewBulk(bulkRequest)],
    ['bulkCreate', 'POST', '/api/ModelProviderMapping/bulk', bulkRequest, bulkCreate, 200,
      (api: ConduitAdminClient) => api.modelMappings.bulkCreate(bulkRequest)],
    ['bulkDelete', 'POST', '/api/ModelProviderMapping/bulk/delete', [7], bulkDelete, 200,
      (api: ConduitAdminClient) => api.modelMappings.bulkDelete([7])],
    ['bulkEnable', 'POST', '/api/ModelProviderMapping/bulk/enable', [7], bulkUpdate, 200,
      (api: ConduitAdminClient) => api.modelMappings.bulkEnable([7])],
    ['bulkDisable', 'POST', '/api/ModelProviderMapping/bulk/disable', [7], bulkUpdate, 200,
      (api: ConduitAdminClient) => api.modelMappings.bulkDisable([7])],
  ] as const;

  it.each(cases)('%s uses its generated operation', async (name, method, path, requestBody, payload, status, invoke) => {
    void name;
    mockFetch.mockResolvedValueOnce(response(payload, status));
    await expect(invoke(client())).resolves.toEqual(payload);

    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request.method).toBe(method);
    expect(request.url).toBe(`https://admin.test${path}`);
    expect(request.headers.get('X-Master-Key')).toBe('master-key');
    expect(body(request)).toEqual(requestBody);
  });

  it('fans bulkUpdate out through the migrated update operation', async () => {
    mockFetch.mockResolvedValue(response(undefined, 204));
    await expect(client().modelMappings.bulkUpdate([
      { id: 7, data: updateRequest }, { id: 8, data: { ...updateRequest, modelAlias: 'nova-2' } },
    ])).resolves.toBeUndefined();
    expect(mockFetch).toHaveBeenCalledTimes(2);
    expect((mockFetch.mock.calls[0]?.[0] as Request).url).toBe('https://admin.test/api/ModelProviderMapping/7');
    expect((mockFetch.mock.calls[1]?.[0] as Request).url).toBe('https://admin.test/api/ModelProviderMapping/8');
  });

  it('preserves headers, callbacks, request payloads, and retries', async () => {
    const onRequest = jest.fn<void, [RequestConfigInfo]>();
    const onResponse = jest.fn<void, [ResponseInfo]>();
    mockFetch.mockRejectedValueOnce(new Error('network')).mockResolvedValueOnce(response(undefined, 204));
    const api = client({
      retries: { maxRetries: 1, retryDelay: 0, retryCondition: () => true }, onRequest, onResponse,
    });
    await api.modelMappings.update(7, updateRequest, { headers: { [TRACE_HEADER]: 'mapping' } });
    expect(mockFetch).toHaveBeenCalledTimes(2);
    expect(onRequest).toHaveBeenCalledWith(expect.objectContaining({
      method: 'PUT', url: 'https://admin.test/api/ModelProviderMapping/7', data: updateRequest,
    }));
    expect(onRequest.mock.calls[0]?.[0].headers['X-Trace']).toBe('mapping');
    expect(onResponse).toHaveBeenCalledWith(expect.objectContaining({ status: 204 }));
  });

  it('preserves structured errors', async () => {
    mockFetch.mockResolvedValueOnce(response({ error: 'Mapping missing', code: 'NOT_FOUND' }, 404));
    const operation = client().modelMappings.getById(404);
    await expect(operation).rejects.toEqual(expect.objectContaining({ message: 'Mapping missing', statusCode: 404 }));
    await expect(operation).rejects.toBeInstanceOf(NotFoundError);
  });

  it.each(['cancellation', 'timeout'])('preserves %s', async mode => {
    const controller = new AbortController();
    let signal: AbortSignal | undefined;
    mockFetch.mockImplementationOnce(input => {
      signal = (input as Request).signal;
      return new Promise<Response>((resolve, reject) => {
        void resolve;
        signal?.addEventListener('abort', () =>
          reject(new DOMException('aborted', 'AbortError')), { once: true });
      });
    });
    const operation = client(mode === 'timeout' ? { timeout: 5 } : {})
      .modelMappings.list(mode === 'cancellation' ? { signal: controller.signal } : undefined);
    if (mode === 'cancellation') controller.abort('caller');
    await expect(operation).rejects.toEqual(expect.objectContaining({ name: 'AbortError' }));
    expect(signal?.aborted).toBe(true);
  });
});
