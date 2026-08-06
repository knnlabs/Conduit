import {
  buildErrorReport,
  reportClientError,
  resetErrorReportingStateForTests,
  sanitizeErrorMetadata,
} from './error-reporting';

describe('privacy-safe error reporting', () => {
  beforeEach(() => {
    resetErrorReportingStateForTests();
    window.history.replaceState({}, '', '/models?api_key=route-secret#private');
  });

  it('scrubs credentials, content, bodies, and URL query data', () => {
    const error = Object.assign(
      new Error('Failed https://api.example.test/v1/models?token=url-secret prompt=private-prompt Bearer bearer-secret'),
      { requestId: 'request-123' },
    );
    error.stack = 'Error: api_key=stack-secret\n at https://app.example.test/chunk.js?token=source-secret';

    const report = buildErrorReport(error, 'model-load', {
      authorization: 'Bearer metadata-secret',
      prompt: 'draw a private image',
      responseBody: { generatedContent: 'private output' },
      safeCount: 3,
    });
    const serialized = JSON.stringify(report);

    expect(report.route).toBe('/models');
    expect(report.correlationId).toBe('request-123');
    expect(report.metadata).toEqual({
      authorization: '[REDACTED]',
      prompt: '[REDACTED]',
      responseBody: '[REDACTED]',
      safeCount: 3,
    });
    expect(serialized).not.toContain('route-secret');
    expect(serialized).not.toContain('private-prompt');
    expect(serialized).not.toContain('bearer-secret');
    expect(serialized).not.toContain('stack-secret');
    expect(serialized).not.toContain('source-secret');
  });

  it('deduplicates the same error across reporting entry points', async () => {
    const transport = jest.fn().mockResolvedValue(undefined);
    const error = new Error('shared failure');

    await expect(reportClientError(error, 'global', undefined, {
      force: true,
      now: 1_000,
      transport,
    })).resolves.toBe('sent');
    await expect(reportClientError(error, 'react-boundary', undefined, {
      force: true,
      now: 1_001,
      transport,
    })).resolves.toBe('duplicate');
    expect(transport).toHaveBeenCalledTimes(1);
  });

  it('honors sampling and contains transport failures', async () => {
    const transport = jest.fn().mockRejectedValue(new Error('collector unavailable'));

    await expect(reportClientError(new Error('sampled'), undefined, undefined, {
      force: true,
      sampleRate: 0,
      transport,
    })).resolves.toBe('sampled-out');
    expect(transport).not.toHaveBeenCalled();

    resetErrorReportingStateForTests();
    const retryableError = new Error('transport failure');
    await expect(reportClientError(retryableError, undefined, undefined, {
      force: true,
      transport,
    })).resolves.toBe('failed');

    transport.mockResolvedValue(undefined);
    await expect(reportClientError(retryableError, undefined, undefined, {
      force: true,
      transport,
    })).resolves.toBe('sent');
  });

  it('limits nested and sensitive metadata', () => {
    expect(sanitizeErrorMetadata({ headers: { safe: 'no' }, routeName: 'models' })).toEqual({
      headers: '[REDACTED]',
      routeName: 'models',
    });
  });
});
