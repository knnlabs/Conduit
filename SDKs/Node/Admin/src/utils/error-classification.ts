import { ApiKeyTestResult, StandardApiKeyTestResponse } from '../models/provider';
import { ProviderType } from '../models/providerType';

export function classifyApiKeyTestError(
  error: unknown,
  providerType?: ProviderType
): StandardApiKeyTestResponse {
  // Extract error details
  const errorDetails = extractErrorDetails(error);
  const { statusCode, message, code } = errorDetails;

  // Check for provider-specific non-testable providers
  if (isNonTestableProvider(providerType)) {
    return {
      result: ApiKeyTestResult.IGNORED,
      message: "Your API Key was untested because this provider doesn't support API Key testing without making a real API request that can cost money",
      details: {
        providerMessage: message
      }
    };
  }

  // Classify based on HTTP status code
  if (statusCode === 401 || statusCode === 403) {
    return {
      result: ApiKeyTestResult.INVALID_KEY,
      message: 'Your API Key failed the authorization test',
      details: {
        statusCode,
        providerMessage: message,
        errorCode: code
      }
    };
  }

  if (statusCode === 429) {
    return {
      result: ApiKeyTestResult.RATE_LIMITED,
      message: 'API Key test was rate limited. Please try again later',
      details: {
        statusCode,
        providerMessage: message,
        errorCode: code
      }
    };
  }

  if (statusCode && statusCode >= 500) {
    return {
      result: ApiKeyTestResult.PROVIDER_DOWN,
      message: 'We were unable to verify the request. Perhaps the LLM provider is down?',
      details: {
        statusCode,
        providerMessage: message,
        errorCode: code
      }
    };
  }

  // Check for network errors
  if (isNetworkError(error)) {
    return {
      result: ApiKeyTestResult.PROVIDER_DOWN,
      message: 'We were unable to verify the request. Perhaps the LLM provider is down?',
      details: {
        providerMessage: message,
        errorCode: code
      }
    };
  }

  // Unknown error
  return {
    result: ApiKeyTestResult.UNKNOWN_ERROR,
    message: 'An unexpected error occurred during testing',
    details: {
      providerMessage: message,
      errorCode: code,
      statusCode
    }
  };
}

export function createSuccessResponse(
  responseTimeMs?: number,
  modelsAvailable?: string[]
): StandardApiKeyTestResponse {
  return {
    result: ApiKeyTestResult.SUCCESS,
    message: 'Your API Key was tested and is authorized',
    details: {
      responseTimeMs,
      modelsAvailable
    }
  };
}

function extractErrorDetails(error: unknown): {
  statusCode?: number;
  message?: string;
  code?: string;
} {
  if (!error || typeof error !== 'object') {
    return { message: String(error) };
  }

  const err = error as Record<string, unknown>;
  
  // Handle different error shapes
  let statusCode: number | undefined;
  let message: string | undefined;
  let code: string | undefined;

  // Check for response status
  if ('response' in err && err.response && typeof err.response === 'object') {
    const response = err.response as Record<string, unknown>;
    if ('status' in response && typeof response.status === 'number') {
      statusCode = response.status;
    }
    if ('data' in response && response.data && typeof response.data === 'object') {
      const data = response.data as Record<string, unknown>;
      if ('error' in data && data.error && typeof data.error === 'object') {
        const errorObj = data.error as Record<string, unknown>;
        if ('message' in errorObj && typeof errorObj.message === 'string') {
          message = errorObj.message;
        }
        if ('code' in errorObj && typeof errorObj.code === 'string') {
          code = errorObj.code;
        }
      }
      // Also check for message at the data level
      if (!message && 'message' in data && typeof data.message === 'string') {
        message = data.message;
      }
    }
  }

  // Check for status at the error level
  if (!statusCode && 'status' in err && typeof err.status === 'number') {
    statusCode = err.status;
  }

  // Check for statusCode at the error level
  if (!statusCode && 'statusCode' in err && typeof err.statusCode === 'number') {
    statusCode = err.statusCode;
  }

  // Check for message at the error level
  if (!message && 'message' in err && typeof err.message === 'string') {
    message = err.message;
  }

  // Check for code at the error level
  if (!code && 'code' in err && typeof err.code === 'string') {
    code = err.code;
  }

  return { statusCode, message, code };
}

function isNetworkError(error: unknown): boolean {
  if (!error || typeof error !== 'object') {
    return false;
  }

  const err = error as Record<string, unknown>;
  const code = err.code as string | undefined;
  
  return code === 'ECONNREFUSED' || 
         code === 'ENOTFOUND' || 
         code === 'ETIMEDOUT' ||
         code === 'ECONNRESET' ||
         code === 'ENETUNREACH';
}

function isNonTestableProvider(providerType?: ProviderType): boolean {
  if (!providerType) {
    return false;
  }

  // List of providers that don't support simple API key testing
  const nonTestableProviders: ProviderType[] = [
    ProviderType.Replicate,
  ];

  return nonTestableProviders.includes(providerType);
}