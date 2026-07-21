import {
  AuthError,
  ConduitError,
  NetworkError,
  RateLimitError,
  ServerError,
  ValidationError,
  isAuthError,
  isRateLimitError,
  isValidationError,
} from "@/lib/conduit-common";

export {
  AuthError,
  ConduitError,
  NetworkError,
  RateLimitError,
  ServerError,
  ValidationError,
  isAuthError,
  isRateLimitError,
  isValidationError,
};

export class InsufficientBalanceError extends ConduitError {
  constructor(
    message = "Insufficient balance",
    context?: Record<string, unknown>,
  ) {
    super(message, 402, "INSUFFICIENT_BALANCE", context);
  }
}

export function isInsufficientBalanceError(
  error: unknown,
): error is InsufficientBalanceError {
  return (
    error instanceof InsufficientBalanceError ||
    (error instanceof ConduitError &&
      (error.statusCode === 402 || error.code === "INSUFFICIENT_BALANCE"))
  );
}

function extractMessage(payload: unknown, fallback: string): string {
  if (!payload || typeof payload !== "object") return fallback;
  const record = payload as Record<string, unknown>;
  const nested = record.error;
  if (typeof nested === "string") return nested;
  if (
    nested &&
    typeof nested === "object" &&
    typeof (nested as Record<string, unknown>).message === "string"
  ) {
    return (nested as Record<string, unknown>).message as string;
  }
  return typeof record.message === "string" ? record.message : fallback;
}

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
  const message = extractMessage(
    payload,
    response.statusText || `Gateway request failed (${response.status})`,
  );
  const context = { details: payload };
  switch (response.status) {
    case 400:
      return new ValidationError(message, context);
    case 401:
    case 403:
      return new AuthError(message, context);
    case 402:
      return new InsufficientBalanceError(message, context);
    case 429:
      return new RateLimitError(
        message,
        Number(response.headers.get("retry-after") ?? undefined),
        context,
      );
    default:
      return response.status >= 500
        ? new ServerError(message, context)
        : new ConduitError(message, response.status, "GATEWAY_ERROR", context);
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
