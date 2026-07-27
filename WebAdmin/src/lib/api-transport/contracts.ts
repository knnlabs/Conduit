import type { z } from 'zod';
import {
  adminEphemeralKeySchema,
  gatewayEphemeralKeySchema,
  webAdminEphemeralKeySchema,
  webAdminEphemeralMasterKeySchema,
} from './critical-response-validation';

export type AdminEphemeralMasterKeyResponse = z.infer<
  typeof adminEphemeralKeySchema
>;

export type GatewayEphemeralKeyResponse = z.infer<
  typeof gatewayEphemeralKeySchema
>;

export type WebAdminEphemeralKeyResponse = z.infer<
  typeof webAdminEphemeralKeySchema
>;

export type WebAdminEphemeralMasterKeyResponse = z.infer<
  typeof webAdminEphemeralMasterKeySchema
>;
