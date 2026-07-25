import { ApiKeyTestResult, type StandardApiKeyTestResponse } from '../models/provider';

/**
 * Wire shape of an API-key test response. The Admin API serializes camelCase, but the PascalCase
 * variants are tolerated so a response that skips the configured naming policy still normalizes.
 */
export interface RawApiKeyTestResponse {
  result?: string;
  Result?: string;
  message?: string;
  Message?: string;
  details?: RawApiKeyTestDetails | null;
  Details?: RawApiKeyTestDetails | null;
}

/** Wire shape of the detail block accompanying an API-key test response. */
export interface RawApiKeyTestDetails {
  responseTimeMs?: number | null;
  ResponseTimeMs?: number;
  modelsAvailable?: string[] | null;
  ModelsAvailable?: string[];
  providerMessage?: string | null;
  ProviderMessage?: string;
  errorCode?: string | null;
  ErrorCode?: string;
  statusCode?: number | null;
  StatusCode?: number;
}

/**
 * Maps a backend result name onto the local enum.
 *
 * The backend serializes PascalCase (`InvalidKey`), the client models snake_case (`invalid_key`).
 * A name with no local member falls back to UNKNOWN_ERROR, which is why {@link ApiKeyTestResult}
 * must carry every member the backend can produce.
 */
export function normalizeApiKeyTestResult(value: string): ApiKeyTestResult {
  if (!value) return ApiKeyTestResult.UNKNOWN_ERROR;

  const snakeCase = value
    .replace(/([A-Z])/g, '_$1')
    .toLowerCase()
    .replace(/^_/, '');

  const enumMap: Record<string, ApiKeyTestResult> = {
    'success': ApiKeyTestResult.SUCCESS,
    'invalid_key': ApiKeyTestResult.INVALID_KEY,
    'ignored': ApiKeyTestResult.IGNORED,
    'provider_down': ApiKeyTestResult.PROVIDER_DOWN,
    'rate_limited': ApiKeyTestResult.RATE_LIMITED,
    'unknown_error': ApiKeyTestResult.UNKNOWN_ERROR,
    'configuration': ApiKeyTestResult.CONFIGURATION,
  };

  return enumMap[snakeCase] ?? ApiKeyTestResult.UNKNOWN_ERROR;
}

/** Normalizes a wire API-key test response into the local model. */
export function normalizeApiKeyTestResponse(response: RawApiKeyTestResponse): StandardApiKeyTestResponse {
  const result = response.result ?? response.Result ?? '';
  const message = response.message ?? response.Message ?? '';
  const details = response.details ?? response.Details;

  return {
    result: normalizeApiKeyTestResult(result),
    message: message,
    details: details ? {
      responseTimeMs: details.responseTimeMs ?? details.ResponseTimeMs ?? undefined,
      modelsAvailable: details.modelsAvailable ?? details.ModelsAvailable ?? undefined,
      providerMessage: details.providerMessage ?? details.ProviderMessage ?? undefined,
      errorCode: details.errorCode ?? details.ErrorCode ?? undefined,
      statusCode: details.statusCode ?? details.StatusCode ?? undefined,
    } : undefined,
  };
}
