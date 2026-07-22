import { Buffer } from 'node:buffer';
import { sanitizeErrorReportPayload, shouldSample, type ErrorReportPayload } from '@/lib/utils/error-reporting';

const MAX_REPORT_BYTES = 64 * 1024;
const REPORT_TIMEOUT_MS = 5_000;

export const runtime = 'nodejs';

function parseSampleRate(value: string | undefined): number {
  if (!value) return 1;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? Math.min(1, Math.max(0, parsed)) : 1;
}

function isErrorReportPayload(value: unknown): value is ErrorReportPayload {
  if (!value || typeof value !== 'object') return false;
  const report = value as Partial<ErrorReportPayload>;
  return typeof report.eventId === 'string' &&
    typeof report.fingerprint === 'string' &&
    typeof report.timestamp === 'string' &&
    typeof report.name === 'string' &&
    typeof report.message === 'string' &&
    typeof report.correlationId === 'string';
}

export async function POST(request: Request): Promise<Response> {
  const reportingUrl = process.env.ERROR_REPORTING_URL;
  if (!reportingUrl) return new Response(null, { status: 204 });

  const contentLength = Number(request.headers.get('content-length') ?? 0);
  if (contentLength > MAX_REPORT_BYTES) return new Response(null, { status: 413 });

  try {
    const rawBody = await request.text();
    if (Buffer.byteLength(rawBody, 'utf8') > MAX_REPORT_BYTES) {
      return new Response(null, { status: 413 });
    }

    const parsed = JSON.parse(rawBody) as unknown;
    if (!isErrorReportPayload(parsed)) return new Response(null, { status: 400 });

    const report = sanitizeErrorReportPayload(parsed);
    if (!shouldSample(report.fingerprint, parseSampleRate(process.env.ERROR_REPORTING_SAMPLE_RATE))) {
      return new Response(null, { status: 202 });
    }

    const forwardedReport = {
      ...report,
      release: process.env.ERROR_REPORTING_RELEASE ?? report.release,
      environment: process.env.ERROR_REPORTING_ENVIRONMENT ?? report.environment,
      receivedAt: new Date().toISOString(),
    };
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (process.env.ERROR_REPORTING_TOKEN) {
      headers.Authorization = `Bearer ${process.env.ERROR_REPORTING_TOKEN}`;
    }

    const abortController = new AbortController();
    const timeout = setTimeout(() => abortController.abort(), REPORT_TIMEOUT_MS);
    try {
      const response = await fetch(reportingUrl, {
        method: 'POST',
        headers,
        body: JSON.stringify(forwardedReport),
        signal: abortController.signal,
      });
      return new Response(null, { status: response.ok ? 202 : 502 });
    } finally {
      clearTimeout(timeout);
    }
  } catch {
    return new Response(null, { status: 502 });
  }
}
