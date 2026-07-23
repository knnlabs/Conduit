import { ConduitAdminClient } from '..';
import type { RequestConfigInfo, ResponseInfo } from '../client/types';
import { NotFoundError } from '@/lib/conduit-common';

const mockFetch: jest.MockedFunction<typeof fetch> = jest.fn();
const TEST_HEADER = 'X-Test-Header';
const TRACE_HEADER = 'X-Trace';
global.fetch = mockFetch;

function jsonResponse(body: unknown, status = 200): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    statusText: status === 200 ? 'OK' : 'Error',
    url: 'https://admin.test',
    headers: new Headers({ 'content-type': 'application/json' }),
    text: async () => JSON.stringify(body),
  } as Response;
}

function createClient(overrides: ConstructorParameters<typeof ConduitAdminClient>[0] = {
  baseUrl: 'https://admin.test',
  masterKey: 'master-key',
  retries: 0,
}) {
  return new ConduitAdminClient(overrides);
}

beforeEach(() => mockFetch.mockReset());

describe('contract-native model author and series reads', () => {
  const cases: Array<{
    name: string;
    path: string;
    customHeader: string;
    payload: unknown;
    invoke: (client: ConduitAdminClient) => Promise<unknown>;
  }> = [
    {
      name: 'modelAuthors.list',
      path: '/api/ModelAuthor',
      customHeader: 'authors-list',
      payload: [{ id: 1, name: 'Acme' }],
      invoke: (client) => client.modelAuthors.list({ headers: { [TEST_HEADER]: 'authors-list' } }),
    },
    {
      name: 'modelAuthors.get',
      path: '/api/ModelAuthor/17',
      customHeader: 'authors-get',
      payload: { id: 17, name: 'Acme' },
      invoke: (client) => client.modelAuthors.get(17, { headers: { [TEST_HEADER]: 'authors-get' } }),
    },
    {
      name: 'modelAuthors.getSeries',
      path: '/api/ModelAuthor/17/series',
      customHeader: 'authors-series',
      payload: [{ id: 29, name: 'Nova' }],
      invoke: (client) => client.modelAuthors.getSeries(17, { headers: { [TEST_HEADER]: 'authors-series' } }),
    },
    {
      name: 'modelSeries.list',
      path: '/api/ModelSeries',
      customHeader: 'series-list',
      payload: [{ id: 29, name: 'Nova', modelAuthorId: 17 }],
      invoke: (client) => client.modelSeries.list({ headers: { [TEST_HEADER]: 'series-list' } }),
    },
    {
      name: 'modelSeries.get',
      path: '/api/ModelSeries/29',
      customHeader: 'series-get',
      payload: { id: 29, name: 'Nova', modelAuthorId: 17 },
      invoke: (client) => client.modelSeries.get(29, { headers: { [TEST_HEADER]: 'series-get' } }),
    },
    {
      name: 'modelSeries.getModels',
      path: '/api/ModelSeries/29/models',
      customHeader: 'series-models',
      payload: [{ id: 41, name: 'nova-chat' }],
      invoke: (client) => client.modelSeries.getModels(29, { headers: { [TEST_HEADER]: 'series-models' } }),
    },
  ];

  it.each(cases)('$name uses the generated GET operation and returns its payload unchanged', async ({ path, customHeader, payload, invoke }) => {
    mockFetch.mockResolvedValueOnce(jsonResponse(payload));

    await expect(invoke(createClient())).resolves.toEqual(payload);

    expect(mockFetch).toHaveBeenCalledTimes(1);
    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request).toBeDefined();
    expect(request.method).toBe('GET');
    expect(request.url).toBe(`https://admin.test${path}`);
    expect(request.headers.get('X-Master-Key')).toBe('master-key');
    expect(request.headers.get(TEST_HEADER)).toBe(customHeader);
  });

  it('normalizes structured Admin errors', async () => {
    mockFetch.mockResolvedValueOnce(jsonResponse({
      error: 'Author missing',
      details: 'No author has ID 404',
      code: 'AUTHOR_NOT_FOUND',
    }, 404));

    const failedRead = createClient().modelAuthors.get(404);
    await expect(failedRead).rejects.toEqual(
      expect.objectContaining({
        message: 'Author missing',
        statusCode: 404,
        code: 'NOT_FOUND',
      }),
    );
    await expect(failedRead).rejects.toBeInstanceOf(NotFoundError);
  });

  it('aborts a read when its timeout elapses', async () => {
    let requestSignal: AbortSignal | undefined;
    mockFetch.mockImplementationOnce((input) => {
      requestSignal = (input as Request).signal;
      return new Promise<Response>((resolve, reject) => {
        void resolve;
        requestSignal?.addEventListener('abort', () => {
          reject(new DOMException('The operation was aborted.', 'AbortError'));
        }, { once: true });
      });
    });
    const client = createClient({
      baseUrl: 'https://admin.test',
      masterKey: 'master-key',
      timeout: 5,
      retries: 0,
    });

    await expect(client.modelSeries.list()).rejects.toEqual(
      expect.objectContaining({ name: 'AbortError' }),
    );
    expect(requestSignal?.aborted).toBe(true);
  });

  it('propagates caller cancellation to the contract request', async () => {
    const controller = new AbortController();
    let requestSignal: AbortSignal | undefined;
    mockFetch.mockImplementationOnce((input) => {
      requestSignal = (input as Request).signal;
      return new Promise<Response>((resolve, reject) => {
        void resolve;
        requestSignal?.addEventListener('abort', () => {
          reject(new DOMException('The operation was aborted.', 'AbortError'));
        }, { once: true });
      });
    });

    const pendingRead = createClient().modelAuthors.getSeries(17, { signal: controller.signal });
    controller.abort('caller cancelled');

    await expect(pendingRead).rejects.toEqual(expect.objectContaining({ name: 'AbortError' }));
    expect(requestSignal?.aborted).toBe(true);
    expect(requestSignal?.reason).toBe('caller cancelled');
  });

  it('retains request, response, logging, and retry behavior', async () => {
    const onRequest = jest.fn<void, [RequestConfigInfo]>();
    const onResponse = jest.fn<void, [ResponseInfo]>();
    const onError = jest.fn<void, [Error]>();
    const logger = {
      debug: jest.fn(),
      info: jest.fn(),
      warn: jest.fn(),
      error: jest.fn(),
    };
    const payload = [{ id: 29, name: 'Nova' }];
    mockFetch
      .mockRejectedValueOnce(new Error('network failed'))
      .mockResolvedValueOnce(jsonResponse(payload));
    const client = createClient({
      baseUrl: 'https://admin.test',
      masterKey: 'master-key',
      retries: {
        maxRetries: 1,
        retryDelay: 0,
        retryCondition: () => true,
      },
      onRequest,
      onResponse,
      onError,
      logger,
    });

    await expect(client.modelSeries.list({ headers: { [TRACE_HEADER]: 'trace-1' } })).resolves.toEqual(payload);

    expect(mockFetch).toHaveBeenCalledTimes(2);
    expect(onRequest).toHaveBeenCalledTimes(1);
    const requestInfo = onRequest.mock.calls[0]?.[0];
    expect(requestInfo).toMatchObject({
      method: 'GET',
      url: 'https://admin.test/api/ModelSeries',
    });
    expect(requestInfo?.headers['X-Master-Key']).toBe('master-key');
    expect(requestInfo?.headers[TRACE_HEADER]).toBe('trace-1');
    const responseInfo = onResponse.mock.calls[0]?.[0];
    expect(responseInfo).toMatchObject({
      status: 200,
      data: payload,
    });
    expect(responseInfo?.config).toMatchObject({
      method: 'GET',
      url: 'https://admin.test/api/ModelSeries',
    });
    expect(onError).not.toHaveBeenCalled();
    expect(logger.debug).toHaveBeenCalledWith(expect.stringContaining('API Request'));
    expect(logger.debug).toHaveBeenCalledWith(expect.stringContaining('Retrying request'));
    expect(logger.debug).toHaveBeenCalledWith(expect.stringContaining('API Response'));
  });
});

describe('contract-native model reads', () => {
  const cases: Array<{
    name: string;
    url: string;
    payload: unknown;
    invoke: (client: ConduitAdminClient) => Promise<unknown>;
  }> = [
    {
      name: 'models.list',
      url: 'https://admin.test/api/Model',
      payload: [{ id: 41, name: 'nova-chat' }],
      invoke: (client) => client.models.list({ headers: { [TEST_HEADER]: 'model-read' } }),
    },
    {
      name: 'models.get',
      url: 'https://admin.test/api/Model/41',
      payload: { id: 41, name: 'nova-chat' },
      invoke: (client) => client.models.get(41, { headers: { [TEST_HEADER]: 'model-read' } }),
    },
    {
      name: 'models.search',
      url: 'https://admin.test/api/Model/search?query=nova%20%26%20vision',
      payload: [{ id: 41, name: 'nova & vision' }],
      invoke: (client) => client.models.search('nova & vision', { headers: { [TEST_HEADER]: 'model-read' } }),
    },
    {
      name: 'models.getByProvider',
      url: 'https://admin.test/api/Model/provider/models/open%20ai%2Fcompatible',
      payload: [{ id: 41, name: 'nova-chat', providerModelId: 'provider/nova-chat' }],
      invoke: (client) => client.models.getByProvider('open ai/compatible', { headers: { [TEST_HEADER]: 'model-read' } }),
    },
    {
      name: 'models.getModelProviders',
      url: 'https://admin.test/api/Model/41/available-providers',
      payload: [{
        associationId: 17,
        identifier: 'provider/nova-chat',
        provider: 2,
        providerVariation: null,
        maxInputTokens: 32000,
        maxOutputTokens: 8000,
        speedScore: 0.9,
        qualityScore: 0.8,
        isPrimary: true,
        availableProviders: [{ providerId: 9, providerName: 'Groq', providerType: 'Groq' }],
      }],
      invoke: (client) => client.models.getModelProviders(41, { headers: { [TEST_HEADER]: 'model-read' } }),
    },
    {
      name: 'models.getProviderMappings',
      url: 'https://admin.test/api/Model/41/provider-mappings',
      payload: [{
        id: 7,
        modelAlias: 'nova',
        providerModelId: 'provider/nova-chat',
        providerId: 9,
        modelProviderTypeAssociationId: 17,
      }],
      invoke: (client) => client.models.getProviderMappings(41, { headers: { [TEST_HEADER]: 'model-read' } }),
    },
  ];

  it.each(cases)('$name resolves the generated request and preserves its payload', async ({ url, payload, invoke }) => {
    mockFetch.mockResolvedValueOnce(jsonResponse(payload));

    await expect(invoke(createClient())).resolves.toEqual(payload);

    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request).toBeDefined();
    expect(request.method).toBe('GET');
    expect(request.url).toBe(url);
    expect(request.headers.get('X-Master-Key')).toBe('master-key');
    expect(request.headers.get(TEST_HEADER)).toBe('model-read');
  });

  it('models.getIdentifiers uses numeric path parameters and preserves identifier normalization', async () => {
    mockFetch.mockResolvedValueOnce(jsonResponse([{
      id: 17,
      identifier: 'provider/nova-chat',
      provider: 2,
      isPrimary: true,
      maxInputTokens: 32000,
      maxOutputTokens: 8000,
      speedScore: 0.9,
      qualityScore: 0.8,
      providerVariation: 'fast',
      modelCostId: 12,
    }]));

    await expect(createClient().models.getIdentifiers(41, {
      headers: { [TEST_HEADER]: 'identifiers' },
    })).resolves.toEqual([{
      id: 17,
      identifier: 'provider/nova-chat',
      provider: 2,
      isPrimary: true,
      maxInputTokens: 32000,
      maxOutputTokens: 8000,
      speedScore: 0.9,
      qualityScore: 0.8,
      providerVariation: 'fast',
      modelCostId: 12,
      normalizedProvider: 2,
      providerName: 'Groq',
    }]);

    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request.url).toBe('https://admin.test/api/Model/41/identifiers');
    expect(request.headers.get(TEST_HEADER)).toBe('identifiers');
  });

  it('models.listPaginated sends typed numeric and filter queries and preserves provider enrichment', async () => {
    mockFetch.mockResolvedValueOnce(jsonResponse({
      items: [{
        id: 41,
        name: 'nova-chat',
        identifiers: [{
          id: 17,
          identifier: 'provider/nova-chat',
          provider: 2,
          isPrimary: true,
        }],
      }],
      totalCount: 51,
      currentPage: 2,
      pageSize: 25,
      totalPages: 3,
      hasPreviousPage: true,
      hasNextPage: true,
    }));

    await expect(createClient().models.listPaginated({
      page: 2,
      pageSize: 25,
      search: 'nova & vision',
      capability: 'vision',
      hasProviders: true,
    }, { headers: { [TEST_HEADER]: 'paged' } })).resolves.toEqual({
      items: [{
        id: 41,
        name: 'nova-chat',
        identifiers: [{
          id: 17,
          identifier: 'provider/nova-chat',
          provider: 2,
          isPrimary: true,
        }],
        hasProviderMappings: true,
        providerCount: 1,
        providers: [{
          id: 17,
          identifier: 'provider/nova-chat',
          provider: 2,
          isPrimary: true,
          normalizedProvider: 2,
          providerName: 'Groq',
        }],
      }],
      totalCount: 51,
      currentPage: 2,
      pageSize: 25,
      totalPages: 3,
    });

    const request = mockFetch.mock.calls[0]?.[0] as Request;
    expect(request.url).toBe(
      'https://admin.test/api/Model/paged?page=2&pageSize=25&search=nova%20%26%20vision&capability=vision&hasProviders=true',
    );
    expect(request.headers.get(TEST_HEADER)).toBe('paged');
  });

  it('normalizes structured errors from model reads', async () => {
    mockFetch.mockResolvedValueOnce(jsonResponse({
      error: 'Model missing',
      details: 'No model has ID 404',
      code: 'MODEL_NOT_FOUND',
    }, 404));

    const failedRead = createClient().models.get(404);
    await expect(failedRead).rejects.toEqual(expect.objectContaining({
      message: 'Model missing',
      statusCode: 404,
      code: 'NOT_FOUND',
    }));
    await expect(failedRead).rejects.toBeInstanceOf(NotFoundError);
  });
});
