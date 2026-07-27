import {
  AuthError,
  ConduitError,
  ConflictError,
  InsufficientBalanceError,
  NetworkError,
  NotFoundError,
  RateLimitError,
  ServerError,
  ValidationError,
  throwApiError,
  isAuthError,
  isInsufficientBalanceError,
  isConflictError,
  isNotFoundError,
  isRateLimitError,
  isValidationError,
} from "@/lib/conduit-common";

export {
  AuthError,
  ConduitError,
  ConflictError,
  InsufficientBalanceError,
  NetworkError,
  NotFoundError,
  RateLimitError,
  ServerError,
  ValidationError,
  isAuthError,
  isInsufficientBalanceError,
  isConflictError,
  isNotFoundError,
  isRateLimitError,
  isValidationError,
};

export async function createGatewayError(
  response: Response,
): Promise<ConduitError> {
  const text = await response.text();
  let payload: unknown;
  try {
    payload = text ? JSON.parse(text) : undefined;
  } catch {
    payload = undefined;
  }
  const headers = Object.fromEntries(response.headers.entries());
  try {
    throwApiError({
      response: {
        status: response.status,
        data: payload,
        headers,
      },
      message:
        response.statusText ||
        `Gateway request failed (${response.status})`,
    }, response.url, "unknown");
  } catch (error) {
    return error instanceof ConduitError
      ? error
      : new ConduitError(
          error instanceof Error ? error.message : String(error),
          response.status,
          "GATEWAY_ERROR",
          { details: payload },
        );
  }
}

export function getErrorDisplayMessage(
  error: unknown,
  context?: string,
): string {
  if (isInsufficientBalanceError(error))
    return `💳 Insufficient balance to ${context ?? "complete this request"}. Please add credits to your account.`;
  if (isAuthError(error))
    return "🔐 Authentication failed. Please check your API key or login status.";
  if (isValidationError(error)) return `⚠️ Invalid request: ${error.message}`;
  if (isRateLimitError(error))
    return `🐌 Rate limit exceeded.${error.retryAfter ? ` Try again in ${error.retryAfter} seconds.` : ""}`;
  if (error instanceof NetworkError)
    return "🌐 Network error. Please check your internet connection and try again.";
  if (error instanceof Error) return error.message;
  return "An unexpected error occurred. Please try again.";
}

export function createToastErrorHandler(
  showNotification: (options: {
    title: string;
    message: string;
    color: string;
  }) => void,
) {
  return (error: unknown, operation?: string): string => {
    const message = getErrorDisplayMessage(error, operation);
    showNotification({
      title: isInsufficientBalanceError(error)
        ? "Insufficient Balance"
        : "Error",
      message,
      color: isInsufficientBalanceError(error) ? "orange" : "red",
    });
    return message;
  };
}

export const shouldShowBalanceWarning = isInsufficientBalanceError;
export const isRetryableError = (error: unknown): boolean =>
  error instanceof NetworkError ||
  (error instanceof ServerError && error.statusCode >= 502);
