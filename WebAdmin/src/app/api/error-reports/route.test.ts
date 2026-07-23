import { POST } from './route';
import type { ErrorReportPayload } from '@/lib/utils/error-reporting';

const report: ErrorReportPayload = {
  eventId: 'err-1',
  fingerprint: 'abc123',
  timestamp: '2026-07-22T00:00:00.000Z',
  name: 'Error',
  message: 'Request failed token=secret',
  stack: 'at https://app.example/chunk.js?api_key=secret',
  context: 'test',
  route: '/models?token=secret',
  correlationId: 'request-1',
  release: 'client-release',
  environment: 'client-environment',
  metadata: { prompt: 'private prompt', safeCount: 1 },
};

describe('error reporting proxy', () => {
  const originalEnvironment = { ...process.env };
  const originalResponse = global.Response;

  beforeAll(() => {
    Object.defineProperty(global, 'Response', {
      configurable: true,
      writable: true,
      value: class MockResponse {
        status: number;

        constructor(body: unknown, init?: { status?: number }) {
          void body;
          this.status = init?.status ?? 200;
        }
      },
    });
  });

  beforeEach(() => {
    jest.restoreAllMocks();
    delete process.env.ERROR_REPORTING_URL;
    delete process.env.ERROR_REPORTING_TOKEN;
    delete process.env.ERROR_REPORTING_SAMPLE_RATE;
    delete process.env.ERROR_REPORTING_RELEASE;
    delete process.env.ERROR_REPORTING_ENVIRONMENT;
  });

  afterAll(() => {
    process.env = originalEnvironment;
    Object.defineProperty(global, 'Response', {
      configurable: true,
      writable: true,
      value: originalResponse,
    });
  });

  const createRequest = () => {
    const body = JSON.stringify(report);
    return {
      headers: { get: (name: string) => name === 'content-length' ? String(body.length) : null },
      text: jest.fn().mockResolvedValue(body),
    } as unknown as Request;
  };

  it('is disabled by default when no backend is configured', async () => {
    const fetchSpy = jest.spyOn(global, 'fetch');
    expect((await POST(createRequest())).status).toBe(204);
    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it('forwards a re-scrubbed report with server-owned deployment context', async () => {
    process.env.ERROR_REPORTING_URL = 'https://errors.internal/events';
    process.env.ERROR_REPORTING_TOKEN = 'server-only-token';
    process.env.ERROR_REPORTING_RELEASE = 'release-42';
    process.env.ERROR_REPORTING_ENVIRONMENT = 'production';
    const fetchSpy = jest.spyOn(global, 'fetch').mockResolvedValue({ ok: true } as Response);

    expect((await POST(createRequest())).status).toBe(202);
    expect(fetchSpy).toHaveBeenCalledTimes(1);
    const [url, options] = fetchSpy.mock.calls[0];
    if (typeof options?.body !== 'string') throw new Error('Expected a JSON string body');
    const forwarded = JSON.parse(options.body) as Record<string, unknown>;

    expect(url).toBe('https://errors.internal/events');
    expect(options?.headers).toMatchObject({ Authorization: 'Bearer server-only-token' });
    expect(forwarded).toMatchObject({
      release: 'release-42',
      environment: 'production',
      route: '/models',
      metadata: { prompt: '[REDACTED]', safeCount: 1 },
    });
    expect(JSON.stringify(forwarded)).not.toContain('private prompt');
    expect(JSON.stringify(forwarded)).not.toContain('token=secret');
  });

  it('returns a contained failure when the reporting backend is unavailable', async () => {
    process.env.ERROR_REPORTING_URL = 'https://errors.internal/events';
    jest.spyOn(global, 'fetch').mockRejectedValue(new Error('offline'));
    await expect(POST(createRequest())).resolves.toMatchObject({ status: 502 });
  });
});
