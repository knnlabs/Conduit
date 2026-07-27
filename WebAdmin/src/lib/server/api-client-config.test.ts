import { validateApiClientEnvironment } from './api-client-config';

describe('validateApiClientEnvironment', () => {
  const originalEnvironment = { ...process.env };

  beforeEach(() => {
    process.env = { ...originalEnvironment };
    Object.defineProperty(process.env, 'NODE_ENV', { value: 'test', writable: true });
    delete process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY;
    delete process.env.CONDUIT_ADMIN_API_BASE_URL;
    delete process.env.CONDUIT_API_BASE_URL;
  });

  afterAll(() => {
    process.env = originalEnvironment;
  });

  it('requires the server-side authentication key', () => {
    expect(() => validateApiClientEnvironment())
      .toThrow('CONDUIT_API_TO_API_BACKEND_AUTH_KEY');
  });

  it('accepts configured non-production clients', () => {
    process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY = 'test-key';

    expect(() => validateApiClientEnvironment()).not.toThrow();
  });

  it('rejects loopback API URLs in production', () => {
    Object.defineProperty(process.env, 'NODE_ENV', { value: 'production', writable: true });
    process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY = 'test-key';
    process.env.CONDUIT_ADMIN_API_BASE_URL = 'http://localhost:5002';
    process.env.CONDUIT_API_BASE_URL = 'http://127.0.0.1:5000';

    expect(() => validateApiClientEnvironment()).toThrow(
      /CONDUIT_ADMIN_API_BASE_URL[\s\S]*CONDUIT_API_BASE_URL/,
    );
  });

  it('accepts non-loopback API URLs in production', () => {
    Object.defineProperty(process.env, 'NODE_ENV', { value: 'production', writable: true });
    process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY = 'test-key';
    process.env.CONDUIT_ADMIN_API_BASE_URL = 'http://admin-api:5002';
    process.env.CONDUIT_API_BASE_URL = 'http://gateway-api:5000';

    expect(() => validateApiClientEnvironment()).not.toThrow();
  });
});
