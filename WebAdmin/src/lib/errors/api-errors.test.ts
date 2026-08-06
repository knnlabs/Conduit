import {
  AuthorizationError,
  ConduitError,
  InsufficientBalanceError,
  NotImplementedError,
  TimeoutError,
} from '@/lib/conduit-common';
import { toApiErrorResponse } from './api-errors';
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

describe('toApiErrorResponse', () => {
  test.each([
    [new InsufficientBalanceError('balance'), 402],
    [new AuthorizationError('forbidden'), 403],
    [new TimeoutError('timeout'), 408],
    [new NotImplementedError('not implemented'), 501],
    [new ConduitError('teapot', 418, 'HTTP_418'), 418],
  ])('preserves ConduitError status %#', async (error, expectedStatus) => {
    const response = toApiErrorResponse(error);

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
    const response = toApiErrorResponse(new ConduitError('network', 0, 'NETWORK_ERROR'));

    expect(response.status).toBe(500);
  });

  it('uses the canonical duck-typed HTTP status and ProblemDetails message', async () => {
    const response = toApiErrorResponse({
      message: 'request failed',
      response: {
        status: 422,
        data: { detail: 'The submitted value is invalid' },
        headers: {},
      },
    });

    expect(response.status).toBe(422);
    await expect(response.json()).resolves.toEqual({
      error: 'The submitted value is invalid',
    });
  });
});
