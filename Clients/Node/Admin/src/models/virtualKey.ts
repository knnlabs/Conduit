import { FilterOptions } from './common';

export type BudgetDuration = 'Total' | 'Daily' | 'Weekly' | 'Monthly';

export interface VirtualKeyDto {
  id: number;
  keyName: string;
  apiKey?: string;
  keyPrefix?: string;
  allowedModels: string;
  maxBudget: number;
  currentSpend: number;
  budgetDuration: BudgetDuration;
  budgetStartDate: string;
  isEnabled: boolean;
  expiresAt?: string;
  createdAt: string;
  updatedAt: string;
  metadata?: string;
  rateLimitRpm?: number;
  rateLimitRpd?: number;
  lastUsedAt?: string;
  requestCount?: number;
}

export interface CreateVirtualKeyRequest {
  keyName: string;
  allowedModels?: string;
  maxBudget?: number;
  budgetDuration?: BudgetDuration;
  expiresAt?: string;
  metadata?: string;
  rateLimitRpm?: number;
  rateLimitRpd?: number;
}

export interface CreateVirtualKeyResponse {
  virtualKey: string;
  keyInfo: VirtualKeyDto;
}

export interface UpdateVirtualKeyRequest {
  keyName?: string;
  allowedModels?: string;
  maxBudget?: number;
  budgetDuration?: BudgetDuration;
  isEnabled?: boolean;
  expiresAt?: string;
  metadata?: string;
  rateLimitRpm?: number;
  rateLimitRpd?: number;
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
  maxBudget?: number;
  currentSpend?: number;
  budgetRemaining?: number;
  expiresAt?: string;
  rateLimitRpm?: number;
  rateLimitRpd?: number;
}

export interface UpdateSpendRequest {
  amount: number;
  description?: string;
}

export interface RefundSpendRequest {
  amount: number;
  reason: string;
  originalTransactionId?: string;
}

export interface CheckBudgetRequest {
  estimatedCost: number;
}

export interface CheckBudgetResponse {
  hasAvailableBudget: boolean;
  availableBudget: number;
  estimatedCost: number;
  currentSpend: number;
  maxBudget: number;
}

export interface VirtualKeyValidationInfo {
  keyId: number;
  keyName: string;
  isValid: boolean;
  validationErrors: string[];
  allowedModels: string[];
  budgetInfo: {
    maxBudget: number;
    currentSpend: number;
    remaining: number;
    duration: BudgetDuration;
  };
  rateLimits?: {
    rpm?: number;
    rpd?: number;
  };
  metadata?: Record<string, any>;
}

export interface VirtualKeyMaintenanceRequest {
  cleanupExpiredKeys?: boolean;
  resetDailyBudgets?: boolean;
  resetWeeklyBudgets?: boolean;
  resetMonthlyBudgets?: boolean;
}

export interface VirtualKeyMaintenanceResponse {
  expiredKeysDeleted?: number;
  dailyBudgetsReset?: number;
  weeklyBudgetsReset?: number;
  monthlyBudgetsReset?: number;
  errors?: string[];
}

export interface VirtualKeyFilters extends FilterOptions {
  isEnabled?: boolean;
  hasExpired?: boolean;
  budgetDuration?: BudgetDuration;
  minBudget?: number;
  maxBudget?: number;
  allowedModels?: string[];
  createdAfter?: string;
  createdBefore?: string;
  lastUsedAfter?: string;
  lastUsedBefore?: string;
}

export interface VirtualKeyStatistics {
  totalKeys: number;
  activeKeys: number;
  expiredKeys: number;
  totalSpend: number;
  averageSpendPerKey: number;
  keysNearBudgetLimit: number;
  keysByDuration: Record<BudgetDuration, number>;
}