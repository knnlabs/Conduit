import { ApiKeyTestResult } from '../models/provider';
import { ProviderType } from '../models/providerType';
import {
  normalizeApiKeyTestResponse,
  normalizeApiKeyTestResult,
} from '../utils/api-key-test-response';
import { classifyApiKeyTestError } from '../utils/error-classification';

/**
 * The Admin API classifies a failed key test; this client mirrors that classification for the case
 * where the call itself throws. Both halves are asserted here because a silent divergence is what
 * reduced an actionable "missing Account ID" to "An unexpected error occurred during testing".
 */
describe('api key test result normalization', () => {
  // Member names of ConduitLLM.Configuration.DTOs.ApiKeyTestResult, as serialized on the wire.
  const backendResultNames: [string, ApiKeyTestResult][] = [
    ['Success', ApiKeyTestResult.SUCCESS],
    ['InvalidKey', ApiKeyTestResult.INVALID_KEY],
    ['Ignored', ApiKeyTestResult.IGNORED],
    ['ProviderDown', ApiKeyTestResult.PROVIDER_DOWN],
    ['RateLimited', ApiKeyTestResult.RATE_LIMITED],
    ['UnknownError', ApiKeyTestResult.UNKNOWN_ERROR],
    ['Configuration', ApiKeyTestResult.CONFIGURATION],
  ];

  it.each(backendResultNames)('maps the backend result %s onto a local member', (wire, expected) => {
    expect(normalizeApiKeyTestResult(wire)).toBe(expected);
  });

  it('keeps the backend configuration message intact', () => {
    const normalized = normalizeApiKeyTestResponse({
      result: 'Configuration',
      message: 'Cloudflare is missing required configuration: Account ID. Provide the value in the provider settings.',
      details: { statusCode: null, providerMessage: 'missing account id' },
    });

    expect(normalized.result).toBe(ApiKeyTestResult.CONFIGURATION);
    expect(normalized.message).toContain('Account ID');
    expect(normalized.details?.providerMessage).toBe('missing account id');
  });

  it('falls back to unknown for a result the client does not model', () => {
    expect(normalizeApiKeyTestResult('SomethingNewFromTheBackend')).toBe(ApiKeyTestResult.UNKNOWN_ERROR);
    expect(normalizeApiKeyTestResult('')).toBe(ApiKeyTestResult.UNKNOWN_ERROR);
  });
});

describe('classifyApiKeyTestError', () => {
  const cases: [number, ApiKeyTestResult][] = [
    [401, ApiKeyTestResult.INVALID_KEY],
    [403, ApiKeyTestResult.INVALID_KEY],
    [429, ApiKeyTestResult.RATE_LIMITED],
    [500, ApiKeyTestResult.PROVIDER_DOWN],
    [503, ApiKeyTestResult.PROVIDER_DOWN],
    [400, ApiKeyTestResult.CONFIGURATION],
    [404, ApiKeyTestResult.CONFIGURATION],
    [405, ApiKeyTestResult.CONFIGURATION],
  ];

  it.each(cases)('classifies HTTP %i as %s', (statusCode, expected) => {
    expect(classifyApiKeyTestError({ statusCode }).result).toBe(expected);
  });

  it('tells the operator what to check when the endpoint rejects the request', () => {
    // Cloudflare's OpenAI-compatible path answers a wrong account-scoped URL with 405.
    const classified = classifyApiKeyTestError({ statusCode: 405 });

    expect(classified.message).toContain('HTTP 405');
    expect(classified.message).toContain('base URL');
    expect(classified.message).not.toContain('unexpected error');
    expect(classified.details?.statusCode).toBe(405);
  });

  it('still reports an unrecognized failure as an unknown error', () => {
    expect(classifyApiKeyTestError(new Error('something odd')).result)
      .toBe(ApiKeyTestResult.UNKNOWN_ERROR);
  });

  it('reports a non-testable provider as ignored before looking at the status', () => {
    expect(classifyApiKeyTestError({ statusCode: 405 }, ProviderType.Replicate).result)
      .toBe(ApiKeyTestResult.IGNORED);
  });
});
