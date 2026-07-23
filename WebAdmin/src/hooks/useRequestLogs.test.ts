import { act, renderHook, waitFor } from '@testing-library/react';

import { withAdminClient } from '@/lib/client/adminClient';
import { useRequestLogs } from './useRequestLogs';

jest.mock('@/lib/client/adminClient', () => ({
  withAdminClient: jest.fn(),
}));

const mockedWithAdminClient = jest.mocked(withAdminClient);

interface Deferred<T> {
  promise: Promise<T>;
  resolve: (value: T) => void;
}

function deferred<T>(): Deferred<T> {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((resolvePromise) => {
    resolve = resolvePromise;
  });
  return { promise, resolve };
}

function response(id: number, page: number) {
  return {
    items: [{ id, model: `model-${id}`, timestamp: '2026-07-22T00:00:00Z' }],
    totalCount: 1,
    totalPages: 1,
    page,
  };
}

describe('useRequestLogs', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('ignores a stale response that resolves after a newer page request', async () => {
    const firstRequest = deferred<ReturnType<typeof response>>();
    const secondRequest = deferred<ReturnType<typeof response>>();
    mockedWithAdminClient
      .mockImplementationOnce(() => firstRequest.promise as never)
      .mockImplementationOnce(() => secondRequest.promise as never);

    const { result, rerender } = renderHook(
      ({ page }) => useRequestLogs({ page, pageSize: 25 }),
      { initialProps: { page: 1 } }
    );

    rerender({ page: 2 });
    await waitFor(() => expect(mockedWithAdminClient).toHaveBeenCalledTimes(2));

    await act(async () => {
      secondRequest.resolve(response(2, 2));
      await secondRequest.promise;
    });
    await waitFor(() => expect(result.current.logs[0]?.id).toBe(2));

    await act(async () => {
      firstRequest.resolve(response(1, 1));
      await firstRequest.promise;
    });

    expect(result.current.logs[0]?.id).toBe(2);
    expect(result.current.currentPage).toBe(2);
    expect(result.current.isLoading).toBe(false);
  });
});
