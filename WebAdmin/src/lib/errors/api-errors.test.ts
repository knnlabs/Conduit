import {
  AuthorizationError,
  ConduitError,
  InsufficientBalanceError,
  NotImplementedError,
  TimeoutError,
} from '@/lib/conduit-common';
import { handleApiError } from './api-errors';
import { logger } from '@/lib/utils/logging';

jest.mock('next/server', () => ({
  NextResponse: {
    json: (body: unknown, init: { status: number }) => ({
      status: init.status,
      json: async () => body,
    }),
  },
}));

jest.mock('@/lib/utils/logging', () => ({
  logger: {
    error: jest.fn(),
  },
}));

describe('handleApiError', () => {
  test.each([
    [new InsufficientBalanceError('balance'), 402],
    [new AuthorizationError('forbidden'), 403],
    [new TimeoutError('timeout'), 408],
    [new NotImplementedError('not implemented'), 501],
    [new ConduitError('teapot', 418, 'HTTP_418'), 418],
  ])('preserves ConduitError status %#', async (error, expectedStatus) => {
    const response = handleApiError(error);

    expect(response.status).toBe(expectedStatus);
    await expect(response.json()).resolves.toEqual({ error: error.message });
    expect(logger.error).toHaveBeenLastCalledWith(
      'API operation failed',
      expect.objectContaining({
        error: error.message,
        type: String(expectedStatus),
      }),
    );
  });

  it('uses 500 for a non-HTTP ConduitError status', () => {
    const response = handleApiError(new ConduitError('network', 0, 'NETWORK_ERROR'));

    expect(response.status).toBe(500);
  });
});
