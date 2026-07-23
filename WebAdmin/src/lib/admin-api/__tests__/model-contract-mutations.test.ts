import { NotFoundError } from '@/lib/conduit-common';

import { ConduitAdminClient } from '..';
import type { RequestConfigInfo, ResponseInfo } from '../client/types';

const mockFetch: jest.MockedFunction<typeof fetch> = jest.fn();
const TEST_HEADER = 'X-Test-Header';
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

function createClient(overrides: ConstructorParameters<typeof ConduitAdminClient>[0] = {
  baseUrl: 'https://admin.test',
  masterKey: 'master-key',
  retries: 0,
}) {
  return new ConduitAdminClient(overrides);
}

function requestBody(request: Request): unknown {
  const body = Reflect.get(request, 'body') as unknown;
  if (body === null || body === undefined) return undefined;
  if (typeof body !== 'string') throw new TypeError('Expected the test Request body to be serialized JSON');
  return JSON.parse(body) as unknown;
}

beforeEach(() => mockFetch.mockReset());

describe('contract-native model-family mutations', () => {
  const model = { id: 41, name: 'nova-chat', modelSeriesId: 29 };
  const mapping = {
    id: 7,
    modelAlias: 'nova',
    providerModelId: 'provider/nova-chat',
    providerId: 9,
    modelProviderTypeAssociationId: 17,
    priority: 100,
    weight: 1,
    isEnabled: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  };
  const catalogResult = {
    providersProcessed: 1,
    modelsDiscovered: 2,
    created: { authors: 1, series: 1, models: 1, costs: 1, identifiers: 1 },
    skippedExistingIdentifiers: 1,
    conflicts: [],
    providers: [{
      provider: 'Groq',
      modelsDiscovered: 2,
      created: { authors: 1, series: 1, models: 1, costs: 1, identifiers: 1 },
      skippedExistingIdentifiers: 1,
      conflicts: 0,
    }],
  };

  const cases: Array<{
    name: string;
    method: string;
    path: string;
    body?: unknown;
    payload?: unknown;
    status: number;
    invoke: (client: ConduitAdminClient) => Promise<unknown>;
  }> = [
    {
      name: 'modelAuthors.create', method: 'POST', path: '/v1/admin/model-authors',
      body: { name: 'Acme' }, payload: { id: 17, name: 'Acme' }, status: 201,
      invoke: client => client.modelAuthors.create({ name: 'Acme' }),
    },
    {
      name: 'modelAuthors.update', method: 'PATCH', path: '/v1/admin/model-authors/17',
      body: { name: 'Acme Labs' }, payload: { id: 17, name: 'Acme Labs' }, status: 200,
      invoke: client => client.modelAuthors.update(17, { name: 'Acme Labs' }),
    },
    {
      name: 'modelAuthors.delete', method: 'DELETE', path: '/v1/admin/model-authors/17', status: 204,
      invoke: client => client.modelAuthors.delete(17),
    },
    {
      name: 'modelSeries.create', method: 'POST', path: '/v1/admin/model-series',
      body: { authorId: 17, name: 'Nova' }, payload: { id: 29, authorId: 17, name: 'Nova' }, status: 201,
      invoke: client => client.modelSeries.create({ authorId: 17, name: 'Nova' }),
    },
    {
      name: 'modelSeries.update', method: 'PATCH', path: '/v1/admin/model-series/29',
      body: { name: 'Nova 2' }, payload: { id: 29, authorId: 17, name: 'Nova 2' }, status: 200,
      invoke: client => client.modelSeries.update(29, { name: 'Nova 2' }),
    },
    {
      name: 'modelSeries.delete', method: 'DELETE', path: '/v1/admin/model-series/29', status: 204,
      invoke: client => client.modelSeries.delete(29),
    },
    {
      name: 'models.create', method: 'POST', path: '/v1/admin/models',
      body: { name: 'nova-chat', modelSeriesId: 29 }, payload: model, status: 201,
      invoke: client => client.models.create({ name: 'nova-chat', modelSeriesId: 29 }),
    },
    {
      name: 'models.update', method: 'PATCH', path: '/v1/admin/models/41',
      body: { name: 'nova-chat-2' }, payload: { ...model, name: 'nova-chat-2' }, status: 200,
      invoke: client => client.models.update(41, { name: 'nova-chat-2' }),
    },
    {
      name: 'models.delete', method: 'DELETE', path: '/v1/admin/models/41', status: 204,
      invoke: client => client.models.delete(41),
    },
    {
      name: 'models.createProviderMapping', method: 'POST', path: '/v1/admin/models/41/provider-mappings',
      body: mapping, payload: mapping, status: 201,
      invoke: client => client.models.createProviderMapping(41, mapping),
    },
    {
      name: 'models.updateProviderMapping', method: 'PATCH', path: '/v1/admin/models/41/provider-mappings/7',
      body: mapping, payload: mapping, status: 200,
      invoke: client => client.models.updateProviderMapping(41, 7, mapping),
    },
    {
      name: 'models.deleteProviderMapping', method: 'DELETE', path: '/v1/admin/models/41/provider-mappings/7', status: 204,
      invoke: client => client.models.deleteProviderMapping(41, 7),
    },
    {
      name: 'models.updateIdentifier', method: 'PATCH', path: '/v1/admin/models/41/identifiers/17',
      body: {
        identifier: 'provider/nova-chat', provider: 2, isPrimary: true,
        maxInputTokens: 32768, maxOutputTokens: 8192,
      },
      payload: {
        id: 17, identifier: 'provider/nova-chat', provider: 2, isPrimary: true,
        maxInputTokens: 32768, maxOutputTokens: 8192,
      },
      status: 200,
      invoke: client => client.models.updateIdentifier(41, 17, {
        identifier: 'provider/nova-chat', provider: 'Groq', isPrimary: true,
      }),
    },
    {
      name: 'models.deleteIdentifier', method: 'DELETE', path: '/v1/admin/models/41/identifiers/17', status: 204,
      invoke: client => client.models.deleteIdentifier(41, 17),
    },
    {
      name: 'models.importBundledCatalog', method: 'POST', path: '/v1/admin/model-catalogs/import',
      payload: catalogResult, status: 200,
      invoke: client => client.models.importBundledCatalog(),
    },
  ];

  it.each(cases)('$name uses its generated operation', async ({ method, path, body, payload, status, invoke }) => {
    mockFetch.mockResolvedValueOnce(response(payload, status));

    await expect(invoke(createClient())).resolves.toEqual(payload);

    expect(mockFetch).toHaveBeenCalledTimes(1);
    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request.method).toBe(method);
    expect(request.url).toBe(`https://admin.test${path}`);
    expect(request.headers.get('X-Master-Key')).toBe('master-key');
    expect(requestBody(request)).toEqual(body);
  });

  it('creates identifiers with validation, defaults, numeric provider normalization, and normalized output', async () => {
    mockFetch.mockResolvedValueOnce(response({
      id: 17,
      identifier: 'provider/nova-chat',
      provider: 2,
      isPrimary: true,
      maxInputTokens: 32768,
      maxOutputTokens: 8192,
      speedScore: null,
      qualityScore: null,
      providerVariation: null,
    }, 201));

    await expect(createClient().models.createIdentifier(41, {
      identifier: 'provider/nova-chat',
      provider: 'Groq',
      isPrimary: true,
    }, { headers: { [TEST_HEADER]: 'identifier-create' } })).resolves.toEqual({
      id: 17,
      identifier: 'provider/nova-chat',
      provider: 2,
      isPrimary: true,
      maxInputTokens: 32768,
      maxOutputTokens: 8192,
      speedScore: null,
      qualityScore: null,
      providerVariation: null,
      normalizedProvider: 'groq',
      providerName: 'Groq',
    });

    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request.url).toBe('https://admin.test/v1/admin/models/41/identifiers');
    expect(request.headers.get(TEST_HEADER)).toBe('identifier-create');
    expect(requestBody(request)).toEqual({
      identifier: 'provider/nova-chat',
      provider: 2,
      isPrimary: true,
      maxInputTokens: 32768,
      maxOutputTokens: 8192,
    });
  });

  it('does not issue a request when identifier validation fails', async () => {
    await expect(createClient().models.createIdentifier(41, {
      identifier: '',
      provider: 'Groq',
    })).rejects.toEqual(expect.objectContaining({ name: 'ModelValidationError' }));
    expect(mockFetch).not.toHaveBeenCalled();
  });

  it('preserves mutation callbacks, request data, headers, logging, and retries', async () => {
    const onRequest = jest.fn<void, [RequestConfigInfo]>();
    const onResponse = jest.fn<void, [ResponseInfo]>();
    const onError = jest.fn<void, [Error]>();
    const logger = { debug: jest.fn(), info: jest.fn(), warn: jest.fn(), error: jest.fn() };
    const body = { name: 'Acme Labs' };
    const updated = { id: 17, name: 'Acme Labs' };
    mockFetch.mockRejectedValueOnce(new Error('network failed')).mockResolvedValueOnce(response(updated, 200));

    const client = createClient({
      baseUrl: 'https://admin.test', masterKey: 'master-key',
      retries: { maxRetries: 1, retryDelay: 0, retryCondition: () => true },
      onRequest, onResponse, onError, logger,
    });
    await expect(client.modelAuthors.update(17, body, {
      headers: { [TRACE_HEADER]: 'trace-1' },
    })).resolves.toEqual(updated);

    expect(mockFetch).toHaveBeenCalledTimes(2);
    expect(onRequest).toHaveBeenCalledWith(expect.objectContaining({
      method: 'PATCH', url: 'https://admin.test/v1/admin/model-authors/17', data: body,
    }));
    expect(onRequest.mock.calls[0]?.[0].headers[TRACE_HEADER]).toBe('trace-1');
    expect(onResponse).toHaveBeenCalledWith(expect.objectContaining({ status: 200 }));
    expect(onError).not.toHaveBeenCalled();
    expect(logger.debug).toHaveBeenCalledWith(expect.stringContaining('Retrying request'));
  });

  it('normalizes structured mutation errors', async () => {
    mockFetch.mockResolvedValueOnce(response({
      error: 'Model missing', details: 'No model has ID 404', code: 'MODEL_NOT_FOUND',
    }, 404));

    const mutation = createClient().models.delete(404);
    await expect(mutation).rejects.toEqual(expect.objectContaining({
      message: 'Model missing', statusCode: 404, code: 'NOT_FOUND',
    }));
    await expect(mutation).rejects.toBeInstanceOf(NotFoundError);
  });

  it('propagates caller cancellation to a mutation', async () => {
    const controller = new AbortController();
    let requestSignal: AbortSignal | undefined;
    mockFetch.mockImplementationOnce(input => {
      requestSignal = (input as Request).signal;
      return new Promise<Response>((resolve, reject) => {
        void resolve;
        requestSignal?.addEventListener('abort', () => reject(
          new DOMException('The operation was aborted.', 'AbortError'),
        ), { once: true });
      });
    });

    const mutation = createClient().modelSeries.delete(29, { signal: controller.signal });
    controller.abort('caller cancelled');

    await expect(mutation).rejects.toEqual(expect.objectContaining({ name: 'AbortError' }));
    expect(requestSignal?.aborted).toBe(true);
    expect(requestSignal?.reason).toBe('caller cancelled');
  });

  it('aborts a mutation when its timeout elapses', async () => {
    let requestSignal: AbortSignal | undefined;
    mockFetch.mockImplementationOnce(input => {
      requestSignal = (input as Request).signal;
      return new Promise<Response>((resolve, reject) => {
        void resolve;
        requestSignal?.addEventListener('abort', () => reject(
          new DOMException('The operation was aborted.', 'AbortError'),
        ), { once: true });
      });
    });

    const client = createClient({
      baseUrl: 'https://admin.test', masterKey: 'master-key', timeout: 5, retries: 0,
    });
    await expect(client.models.importBundledCatalog()).rejects.toEqual(
      expect.objectContaining({ name: 'AbortError' }),
    );
    expect(requestSignal?.aborted).toBe(true);
  });
});
