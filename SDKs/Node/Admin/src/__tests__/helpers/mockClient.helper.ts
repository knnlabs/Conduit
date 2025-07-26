/**
 * Creates a mock client with all necessary methods for testing
 * This avoids TypeScript errors about accessing protected methods
 */
export function createMockClient() {
  return {
    get: jest.fn<Promise<any>, any[]>(),
    post: jest.fn<Promise<any>, any[]>(),
    put: jest.fn<Promise<any>, any[]>(),
    patch: jest.fn<Promise<any>, any[]>(),
    delete: jest.fn<Promise<any>, any[]>(),
    request: jest.fn<Promise<any>, any[]>(),
    withCache: jest.fn<Promise<any>, [string, () => Promise<any>, number?]>(
      async (key: string, fn: () => Promise<any>) => fn()
    ),
  };
}

export type MockClient = ReturnType<typeof createMockClient>;