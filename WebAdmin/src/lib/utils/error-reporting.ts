const MAX_MESSAGE_LENGTH = 2_048;
const MAX_STACK_LENGTH = 16_384;
const MAX_METADATA_DEPTH = 5;
const MAX_METADATA_ENTRIES = 50;
const DEDUPE_WINDOW_MS = 10_000;
const MAX_DEDUPE_ENTRIES = 200;

const SENSITIVE_KEY_PATTERN = /(?:api[-_]?key|authorization|bearer|cookie|credential|headers?|password|secret|session|token|master[-_]?key|private[-_]?key|prompt|messages?|content|request[-_]?body|response[-_]?body|generated|input|output|image|audio|video)/i;
const CORRELATION_KEYS = [
  'requestId', 'request_id', 'correlationId', 'correlation_id', 'traceId', 'trace_id',
];

export interface ErrorReportPayload {
  eventId: string;
  fingerprint: string;
  timestamp: string;
  name: string;
  message: string;
  stack?: string;
  context?: string;
  route?: string;
  correlationId: string;
  release: string;
  environment: string;
  browser?: {
    userAgent?: string;
    language?: string;
    viewport?: string;
    online?: boolean;
  };
  metadata?: unknown;
}

interface ReportOptions {
  force?: boolean;
  now?: number;
  sampleRate?: number;
  transport?: (payload: ErrorReportPayload) => Promise<void>;
}

export type ErrorReportResult = 'sent' | 'disabled' | 'duplicate' | 'sampled-out' | 'failed';

const recentFingerprints = new Map<string, number>();

function truncate(value: string, maximum: number): string {
  return value.length <= maximum ? value : `${value.slice(0, maximum)}…[truncated]`;
}

export function sanitizeErrorString(value: string, maximum = MAX_MESSAGE_LENGTH): string {
  const withoutSensitiveUrls = value.replace(/\bhttps?:\/\/[^\s)\]}]+/gi, rawUrl => {
    try {
      const parsed = new URL(rawUrl);
      return `${parsed.origin}${parsed.pathname}`;
    } catch {
      return '[URL REDACTED]';
    }
  });

  return truncate(
    withoutSensitiveUrls
      .replace(/\bBearer\s+[A-Za-z0-9._~+/=-]+/gi, 'Bearer [REDACTED]')
      .replace(/\b(?:sk|pk|emk|condt)[-_][A-Za-z0-9._-]{8,}/gi, '[CREDENTIAL REDACTED]')
      .replace(/\b(authorization|api[-_]?key|token|password|secret|prompt)\s*[:=]\s*["']?[^,\s"']+/gi, '$1=[REDACTED]'),
    maximum,
  );
}

export function sanitizeErrorMetadata(value: unknown, depth = 0): unknown {
  if (depth > MAX_METADATA_DEPTH) return '[MAX DEPTH]';
  if (value === null || value === undefined || typeof value === 'boolean' || typeof value === 'number') return value;
  if (typeof value === 'string') return sanitizeErrorString(value);
  if (value instanceof Error) {
    return {
      name: sanitizeErrorString(value.name, 128),
      message: sanitizeErrorString(value.message),
      stack: value.stack ? sanitizeErrorString(value.stack, MAX_STACK_LENGTH) : undefined,
    };
  }
  if (Array.isArray(value)) {
    return value.slice(0, MAX_METADATA_ENTRIES).map(item => sanitizeErrorMetadata(item, depth + 1));
  }
  if (typeof value === 'bigint') return value.toString();
  if (typeof value === 'symbol') return sanitizeErrorString(value.description ?? '[symbol]');
  if (typeof value !== 'object') return '[unsupported value]';

  const sanitized: Record<string, unknown> = {};
  Object.entries(value as Record<string, unknown>)
    .slice(0, MAX_METADATA_ENTRIES)
    .forEach(([key, item]) => {
      sanitized[key] = SENSITIVE_KEY_PATTERN.test(key)
        ? '[REDACTED]'
        : sanitizeErrorMetadata(item, depth + 1);
    });
  return sanitized;
}

function stableHash(value: string): string {
  let hash = 0x811c9dc5;
  for (let index = 0; index < value.length; index += 1) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193);
  }
  return (hash >>> 0).toString(16).padStart(8, '0');
}

export function shouldSample(fingerprint: string, sampleRate: number): boolean {
  if (sampleRate <= 0) return false;
  if (sampleRate >= 1) return true;
  return parseInt(stableHash(fingerprint), 16) / 0xffffffff < sampleRate;
}

function createId(prefix: string): string {
  const randomId = typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(36).slice(2)}`;
  return `${prefix}_${randomId}`;
}

function getCorrelationId(error: unknown): string {
  if (error && typeof error === 'object') {
    const record = error as Record<string, unknown>;
    for (const key of CORRELATION_KEYS) {
      const value = record[key];
      if (typeof value === 'string' && value.length > 0) return sanitizeErrorString(value, 128);
    }
  }
  return createId('corr');
}

function getBrowserContext(): ErrorReportPayload['browser'] {
  if (typeof window === 'undefined' || typeof navigator === 'undefined') return undefined;
  return {
    userAgent: sanitizeErrorString(navigator.userAgent, 512),
    language: sanitizeErrorString(navigator.language, 32),
    viewport: `${window.innerWidth}x${window.innerHeight}`,
    online: navigator.onLine,
  };
}

export function buildErrorReport(
  error: unknown,
  context?: string,
  metadata?: unknown,
): ErrorReportPayload {
  const errorObject = error instanceof Error ? error : new Error(String(error));
  const route = typeof window !== 'undefined' ? window.location.pathname : undefined;
  const message = sanitizeErrorString(errorObject.message || 'Unknown error');
  const stack = errorObject.stack ? sanitizeErrorString(errorObject.stack, MAX_STACK_LENGTH) : undefined;
  const fingerprint = stableHash(`${errorObject.name}|${message}|${stack ?? ''}|${route ?? ''}`);

  return {
    eventId: createId('err'),
    fingerprint,
    timestamp: new Date().toISOString(),
    name: sanitizeErrorString(errorObject.name || 'Error', 128),
    message,
    ...(stack ? { stack } : {}),
    ...(context ? { context: sanitizeErrorString(context, 256) } : {}),
    ...(route ? { route: sanitizeErrorString(route, 1_024) } : {}),
    correlationId: getCorrelationId(error),
    release: process.env.NEXT_PUBLIC_APP_RELEASE ?? 'unknown',
    environment: process.env.NEXT_PUBLIC_APP_ENVIRONMENT ?? process.env.NODE_ENV ?? 'unknown',
    browser: getBrowserContext(),
    ...(metadata !== undefined ? { metadata: sanitizeErrorMetadata(metadata) } : {}),
  };
}

export function sanitizeErrorReportPayload(payload: ErrorReportPayload): ErrorReportPayload {
  return {
    ...payload,
    eventId: sanitizeErrorString(payload.eventId, 128),
    fingerprint: sanitizeErrorString(payload.fingerprint, 128),
    name: sanitizeErrorString(payload.name, 128),
    message: sanitizeErrorString(payload.message),
    ...(payload.stack ? { stack: sanitizeErrorString(payload.stack, MAX_STACK_LENGTH) } : {}),
    ...(payload.context ? { context: sanitizeErrorString(payload.context, 256) } : {}),
    ...(payload.route ? { route: sanitizeErrorString(payload.route.split(/[?#]/, 1)[0], 1_024) } : {}),
    correlationId: sanitizeErrorString(payload.correlationId, 128),
    release: sanitizeErrorString(payload.release, 128),
    environment: sanitizeErrorString(payload.environment, 64),
    browser: payload.browser ? sanitizeErrorMetadata(payload.browser) as ErrorReportPayload['browser'] : undefined,
    ...(payload.metadata !== undefined ? { metadata: sanitizeErrorMetadata(payload.metadata) } : {}),
  };
}

async function defaultTransport(payload: ErrorReportPayload): Promise<void> {
  const response = await fetch('/api/error-reports', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'same-origin',
    keepalive: true,
    body: JSON.stringify(payload),
  });
  if (!response.ok) throw new Error(`Error reporting proxy returned ${response.status}`);
}

export async function reportClientError(
  error: unknown,
  context?: string,
  metadata?: unknown,
  options: ReportOptions = {},
): Promise<ErrorReportResult> {
  if (!options.force && process.env.NODE_ENV !== 'production') return 'disabled';

  let fingerprint: string | undefined;
  try {
    const payload = buildErrorReport(error, context, metadata);
    fingerprint = payload.fingerprint;
    const now = options.now ?? Date.now();
    const previousReport = recentFingerprints.get(payload.fingerprint);
    if (previousReport !== undefined && now - previousReport < DEDUPE_WINDOW_MS) return 'duplicate';

    if (!shouldSample(payload.fingerprint, options.sampleRate ?? 1)) return 'sampled-out';

    recentFingerprints.set(payload.fingerprint, now);
    if (recentFingerprints.size > MAX_DEDUPE_ENTRIES) {
      const oldest = recentFingerprints.keys().next().value;
      if (oldest) recentFingerprints.delete(oldest);
    }

    await (options.transport ?? defaultTransport)(payload);
    return 'sent';
  } catch {
    if (fingerprint) recentFingerprints.delete(fingerprint);
    return 'failed';
  }
}

export function resetErrorReportingStateForTests(): void {
  recentFingerprints.clear();
}
