import { FilterOptions } from './common';
import { VirtualKeyMetadata } from './metadata';

export interface VirtualKeyGroupDto {
  id: number;
  externalGroupId?: string;
  groupName: string;
  balance: number;
  lifetimeCreditsAdded: number;
  lifetimeSpent: number;
  createdAt: string;
  updatedAt: string;
  virtualKeyCount: number;
}

export interface CreateVirtualKeyGroupRequestDto {
  groupName: string;
  externalGroupId?: string;
  initialBalance?: number;
}

export interface UpdateVirtualKeyGroupRequestDto {
  groupName?: string;
  externalGroupId?: string;
}

export interface AdjustBalanceDto {
  amount: number;
}

export interface VirtualKeyDto {
  id: number;
  keyName: string;
  keyPrefix?: string;
  allowedModels?: string;
  virtualKeyGroupId: number;
  isEnabled: boolean;
  expiresAt?: string;
  createdAt: string;
  updatedAt: string;
  metadata?: string;
  rateLimitRpm?: number;
  rateLimitRpd?: number;
  description?: string;
  // Compatibility properties
  name?: string;
  isActive?: boolean;
  rateLimit?: number;
}

export interface CreateVirtualKeyRequest {
  keyName: string;
  virtualKeyGroupId: number;
  allowedModels?: string;
  expiresAt?: string;
  metadata?: string;
  rateLimitRpm?: number;
  rateLimitRpd?: number;
  description?: string;
}

export interface CreateVirtualKeyResponse {
  virtualKey: string;
  keyInfo: VirtualKeyDto;
}

export interface UpdateVirtualKeyRequest {
  keyName?: string;
  allowedModels?: string;
  isEnabled?: boolean;
  expiresAt?: string;
  metadata?: string;
  rateLimitRpm?: number;
  rateLimitRpd?: number;
  description?: string;
}

export interface VirtualKeyValidationRequest {
  key: string;
}

export interface VirtualKeyValidationResult {
  isValid: boolean;
  virtualKeyId?: number;
  keyName?: string;
  reason?: string;
  allowedModels?: string[];
  expiresAt?: string;
  rateLimitRpm?: number;
  rateLimitRpd?: number;
  virtualKeyGroupId?: number;
}

// Note: Spend tracking is now handled at the VirtualKeyGroup level

export interface VirtualKeyValidationInfo {
  keyId: number;
  keyName: string;
  isValid: boolean;
  validationErrors: string[];
  allowedModels: string[];
  virtualKeyGroupId: number;
  rateLimits?: {
    rpm?: number;
    rpd?: number;
  };
  metadata?: VirtualKeyMetadata;
}

export interface VirtualKeyMaintenanceRequest {
  cleanupExpiredKeys?: boolean;
}

export interface VirtualKeyMaintenanceResponse {
  expiredKeysDeleted?: number;
  errors?: string[];
}

export interface VirtualKeyFilters extends FilterOptions {
  isEnabled?: boolean;
  hasExpired?: boolean;
  allowedModels?: string[];
  createdAfter?: string;
  createdBefore?: string;
  virtualKeyGroupId?: number;
}

export interface VirtualKeyStatistics {
  totalKeys: number;
  activeKeys: number;
  expiredKeys: number;
  totalGroups: number;
  keysByGroup: Record<number, number>;
}