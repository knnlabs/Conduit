import { withAdminClient } from '../adminClient';

const mockFetch: jest.MockedFunction<typeof fetch> = jest.fn();
global.fetch = mockFetch;

function jsonResponse(body: unknown, status = 200): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    statusText: status === 200 ? 'OK' : 'Error',
    url: 'http://example.test',
    headers: new Headers({ 'content-type': 'application/json' }),
    json: async () => body,
    text: async () => JSON.stringify(body),
  } as Response;
}

beforeEach(() => mockFetch.mockReset());

describe('browser Admin API boundary', () => {
  it('gets a fresh ephemeral credential and sends it as X-Master-Key', async () => {
    mockFetch
      .mockResolvedValueOnce(jsonResponse({
        ephemeralMasterKey: 'ephemeral-key',
        expiresAt: '2026-07-20T00:00:00Z',
        expiresInSeconds: 60,
        adminApiUrl: 'http://admin.example',
      }))
      .mockResolvedValueOnce(jsonResponse([]));

    await withAdminClient((client) => client.virtualKeys.list(1, 10));

    expect(mockFetch).toHaveBeenNthCalledWith(
      1,
      '/api/auth/ephemeral-master-key',
      expect.objectContaining({ method: 'POST' }),
    );
    const secondCall = mockFetch.mock.calls[1];
    const request = secondCall?.[0] as Request;
    expect(request.url).toBe('http://admin.example/api/VirtualKeys');
    expect(request.method).toBe('GET');
    expect(request.headers.get('X-Master-Key')).toBe('ephemeral-key');
  });

  it('does not retry a failed Admin request with a single-use credential', async () => {
    mockFetch
      .mockResolvedValueOnce(jsonResponse({
        ephemeralMasterKey: 'single-use-key',
        expiresAt: '2026-07-20T00:00:00Z',
        expiresInSeconds: 60,
        adminApiUrl: 'http://admin.example',
      }))
      .mockRejectedValueOnce(new Error('network failed'));

    await expect(
      withAdminClient((client) => client.virtualKeys.list(1, 10)),
    ).rejects.toThrow('network failed');
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });
});
