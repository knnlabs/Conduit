/**
 * Browsers and modern Node provide Request. Jest's jsdom environment does not,
 * so the transport uses this small compatibility constructor in that one case.
 * Production requests always use the platform implementation.
 */
class RequestCompatibility {
  readonly url: string;
  readonly method: string;
  readonly headers: Headers;
  readonly body: BodyInit | null;
  readonly signal: AbortSignal;

  constructor(input: RequestInfo | URL, init: RequestInit = {}) {
    if (typeof input === 'string') this.url = input;
    else if (input instanceof URL) this.url = input.toString();
    else this.url = input.url;
    this.method = init.method ?? 'GET';
    this.headers = new Headers(init.headers);
    this.body = init.body ?? null;
    this.signal = init.signal ?? new AbortController().signal;
  }
}

export function getRequestConstructor(): typeof Request {
  if (globalThis.Request) return globalThis.Request;
  return RequestCompatibility as unknown as typeof Request;
}
