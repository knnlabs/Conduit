import {
  ConduitAdminClient,
  ExecutionState,
  FunctionPricingModel,
  FunctionProviderType,
  FunctionPurpose,
} from '..';

const mockFetch: jest.MockedFunction<typeof fetch> = jest.fn();
global.fetch = mockFetch;

function response(body: unknown, status = 200): Response {
  return {
    ok: status < 400,
    status,
    statusText: 'OK',
    url: 'https://admin.test',
    headers: new Headers({ 'content-type': 'application/json' }),
    text: async () => JSON.stringify(body),
  } as Response;
}

function client() {
  return new ConduitAdminClient({
    baseUrl: 'https://admin.test',
    masterKey: 'master-key',
    retries: 0,
  });
}

function requestBody(): Record<string, unknown> {
  const request = mockFetch.mock.calls.at(-1)?.[0] as Request;
  return JSON.parse(Reflect.get(request, 'body') as unknown as string) as Record<string, unknown>;
}

beforeEach(() => mockFetch.mockReset());

describe('canonical function contracts', () => {
  it('sends configuration JSON as objects and keeps editor strings local', async () => {
    mockFetch.mockResolvedValueOnce(response({
      id: 7,
      configurationName: 'Weather',
      providerType: FunctionProviderType.Exa,
      purpose: FunctionPurpose.Search,
      defaultExecutionMode: 'synchronous',
      isEnabled: true,
      providerSettings: { region: 'us' },
      parameterSchema: { type: 'object', required: ['query'] },
      createdAt: '2026-07-23T20:00:00Z',
      updatedAt: '2026-07-23T20:00:00Z',
    }, 201));

    const result = await client().functionConfigurations.create({
      configurationName: 'Weather',
      providerType: FunctionProviderType.Exa,
      purpose: FunctionPurpose.Search,
      providerSettings: '{"region":"us"}',
      parameterSchema: '{"type":"object","required":["query"]}',
    });

    expect(requestBody()).toEqual(expect.objectContaining({
      providerSettings: { region: 'us' },
      parameterSchema: { type: 'object', required: ['query'] },
    }));
    expect(JSON.parse(result.parameterSchema ?? '{}')).toEqual({
      type: 'object',
      required: ['query'],
    });
  });

  it('rejects non-object configuration JSON before issuing a request', async () => {
    await expect(client().functionConfigurations.create({
      configurationName: 'Weather',
      providerType: FunctionProviderType.Exa,
      purpose: FunctionPurpose.Search,
      parameterSchema: '[]',
    })).rejects.toThrow('parameterSchema must be a valid JSON object');
    expect(mockFetch).not.toHaveBeenCalled();
  });

  it('maps the shared execution and scoped Admin diagnostics', async () => {
    mockFetch.mockResolvedValueOnce(response({
      id: '85f88aa3-a3e3-43d3-9e12-f33c4f6dcb36',
      functionId: 7,
      status: ExecutionState.Completed,
      input: { query: 'weather' },
      output: { answer: 'sunny' },
      createdAt: '2026-07-23T20:00:00Z',
      durationMs: 13,
      cost: { estimated: 0.001, actual: 0.002, currency: 'USD', breakdown: { units: 1 } },
      admin: {
        virtualKeyId: 11,
        executionMode: 'synchronous',
        retryCount: 0,
        version: 3,
        webhookDelivered: false,
      },
    }));

    const result = await client().functionExecutions.getById(
      '85f88aa3-a3e3-43d3-9e12-f33c4f6dcb36',
    );

    expect(result.functionId).toBe(7);
    expect(result.status).toBe(ExecutionState.Completed);
    expect(result.durationMs).toBe(13);
    expect(result.cost?.actual).toBe(0.002);
    expect(result.admin.virtualKeyId).toBe(11);
    expect(result.admin.version).toBe(3);
  });

  it('sends function pricing configuration as structured JSON', async () => {
    mockFetch.mockResolvedValueOnce(response({
      id: 2,
      costName: 'Search',
      providerType: FunctionProviderType.Exa,
      pricingModel: FunctionPricingModel.FlatRate,
      pricingConfiguration: { costPerExecution: 0.001 },
      isActive: true,
      priority: 1,
      effectiveDate: '2026-07-23T20:00:00Z',
      createdAt: '2026-07-23T20:00:00Z',
      updatedAt: '2026-07-23T20:00:00Z',
    }, 201));

    const result = await client().functionCosts.create({
      costName: 'Search',
      providerType: FunctionProviderType.Exa,
      pricingModel: FunctionPricingModel.FlatRate,
      pricingConfiguration: '{"costPerExecution":0.001}',
    });

    expect(requestBody()).toEqual(expect.objectContaining({
      pricingConfiguration: { costPerExecution: 0.001 },
    }));
    expect(JSON.parse(result.pricingConfiguration)).toEqual({ costPerExecution: 0.001 });
  });
});
