import { ValidationError } from '@/lib/conduit-common';
import {
  gatewayEphemeralKeySchema,
  gatewayMediaUploadSchema,
  gatewayVideoTaskSchema,
  parseCriticalResponse,
  virtualKeyIssueSchema,
  webAdminEphemeralKeySchema,
} from '../critical-response-validation';

describe('critical API response validation', () => {
  it('accepts a complete WebAdmin ephemeral credential', () => {
    expect(parseCriticalResponse(webAdminEphemeralKeySchema, {
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

  it('rejects malformed video-task responses', () => {
    expect(() => parseCriticalResponse(
      gatewayVideoTaskSchema,
      { status: 'pending' },
      'test operation',
    )).toThrow(ValidationError);
  });
});
