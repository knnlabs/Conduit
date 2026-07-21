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
