import { auth } from '@clerk/nextjs/server';
import { GET, isDevelopmentAuthBypass } from './route';

jest.mock('next/server', () => ({
  NextResponse: class MockNextResponse {
    status: number;

    constructor(body: unknown, init?: { status?: number }) {
      void body;
      this.status = init?.status ?? 200;
    }
  },
}));

jest.mock('@clerk/nextjs/server', () => ({
  auth: jest.fn(),
}));

const mockedAuth = jest.mocked(auth);

describe('Grafana proxy authorization', () => {
  const originalNodeEnv = process.env.NODE_ENV;
  const originalAuthEnabled = process.env.CLERK_AUTH_ENABLED;

  afterEach(() => {
    Object.defineProperty(process.env, 'NODE_ENV', { value: originalNodeEnv, writable: true });
    process.env.CLERK_AUTH_ENABLED = originalAuthEnabled;
    jest.resetAllMocks();
  });

  it('allows the development authentication bypass', () => {
    expect(isDevelopmentAuthBypass('development', 'false')).toBe(true);
    expect(isDevelopmentAuthBypass('development', undefined)).toBe(true);
    expect(isDevelopmentAuthBypass('production', 'false')).toBe(false);
    expect(mockedAuth).not.toHaveBeenCalled();
  });

  it('allows authenticated site administrators', async () => {
    Object.defineProperty(process.env, 'NODE_ENV', { value: 'production', writable: true });
    process.env.CLERK_AUTH_ENABLED = 'true';
    mockedAuth.mockResolvedValue({
      userId: 'user_admin',
      sessionClaims: { metadata: { siteadmin: true } },
    } as unknown as Awaited<ReturnType<typeof auth>>);

    expect((await GET()).status).toBe(204);
  });

  it('rejects requests without a session', async () => {
    Object.defineProperty(process.env, 'NODE_ENV', { value: 'production', writable: true });
    process.env.CLERK_AUTH_ENABLED = 'true';
    mockedAuth.mockResolvedValue({ userId: null, sessionClaims: null } as Awaited<ReturnType<typeof auth>>);

    expect((await GET()).status).toBe(401);
  });

  it('rejects authenticated users without site administrator access', async () => {
    Object.defineProperty(process.env, 'NODE_ENV', { value: 'production', writable: true });
    process.env.CLERK_AUTH_ENABLED = 'true';
    mockedAuth.mockResolvedValue({
      userId: 'user_regular',
      sessionClaims: { metadata: { siteadmin: false } },
    } as unknown as Awaited<ReturnType<typeof auth>>);

    expect((await GET()).status).toBe(403);
  });
});
