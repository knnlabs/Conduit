import { ValidationError } from '@/lib/conduit-common';
import {
  gatewayEphemeralKeySchema,
  gatewayFunctionExecutionSchema,
  gatewayMediaUploadSchema,
  gatewayVideoTaskSchema,
  parseCriticalResponse,
  virtualKeyIssueSchema,
  webAdminEphemeralMasterKeySchema,
} from '../critical-response-validation';

describe('critical API response validation', () => {
  it('accepts a complete WebAdmin ephemeral credential', () => {
    expect(parseCriticalResponse(webAdminEphemeralMasterKeySchema, {
      ephemeralMasterKey: 'master_ephemeral',
      expiresAt: '2026-07-20T00:00:00Z',
      expiresInSeconds: 60,
      adminApiUrl: 'https://admin.example',
    }, 'test')).toEqual(expect.objectContaining({ ephemeralMasterKey: 'master_ephemeral' }));
  });

  it('coerces the Gateway TTL published as an integer string', () => {
    expect(parseCriticalResponse(gatewayEphemeralKeySchema, {
      ephemeral_key: 'gateway_ephemeral',
      expires_at: '2026-07-20T00:00:00Z',
      expires_in_seconds: '60',
    }, 'test').expiresInSeconds).toBe(60);
  });

  it('rejects malformed virtual-key issuance responses', () => {
    expect(() => parseCriticalResponse(
      virtualKeyIssueSchema,
      { keyInfo: { id: 1 } },
      'test operation',
    )).toThrow(ValidationError);
  });

  it('rejects malformed media-upload responses', () => {
    expect(() => parseCriticalResponse(
      gatewayMediaUploadSchema,
      { success: true },
      'test operation',
    )).toThrow(ValidationError);
  });

  it('normalizes the canonical Gateway function execution resource', () => {
    const execution = parseCriticalResponse(gatewayFunctionExecutionSchema, {
      id: '85f88aa3-a3e3-43d3-9e12-f33c4f6dcb36',
      function_id: '7',
      status: 'completed',
      input: { query: 'weather' },
      output: { answer: 'sunny' },
      created_at: '2026-07-23T20:00:00Z',
      duration_ms: '12',
      cost: { estimated: '0.001', actual: '0.002', currency: 'USD' },
    }, 'test');

    expect(execution.functionId).toBe(7);
    expect(execution.status).toBe('completed');
    expect(execution.durationMs).toBe(12);
    expect(execution.cost.actual).toBe(0.002);
  });

  it('rejects function executions without structured cost data', () => {
    expect(() => parseCriticalResponse(gatewayFunctionExecutionSchema, {
      id: '85f88aa3-a3e3-43d3-9e12-f33c4f6dcb36',
      function_id: 7,
      status: 'completed',
      created_at: '2026-07-23T20:00:00Z',
      duration_ms: 12,
    }, 'test')).toThrow(ValidationError);
  });

  it('rejects malformed video-task responses', () => {
    expect(() => parseCriticalResponse(
      gatewayVideoTaskSchema,
      { status: 'pending' },
      'test operation',
    )).toThrow(ValidationError);
  });
});
