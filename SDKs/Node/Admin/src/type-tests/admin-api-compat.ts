/**
 * Compile-time drift detection between hand-written SDK models and the
 * OpenAPI-generated wire types (src/generated/admin-api.ts).
 *
 * This file is never imported at runtime and emits no JavaScript; it is
 * validated by `npm run type-check`, which CI runs. When a backend DTO
 * changes, regenerating the wire types (npm run generate:admin:offline in
 * SDKs/Node/scripts) makes the matching assertion here fail to compile,
 * naming the exact model that drifted.
 *
 * Check strength:
 * - `Compatible` — same property names AND mutually assignable property
 *   types after nullability normalization (the strict default).
 * - `SameKeys` — same property names only. Used where the hand-written
 *   model intentionally narrows wire types (string-literal unions, typed
 *   dictionaries, client-side enums), which `Compatible` cannot express.
 *
 * 24 additional same-named types are NOT asserted here because they had
 * already drifted before this gate existed. They are cataloged in
 * https://github.com/nickna/Conduit/issues/1038 — as each one is
 * reconciled, add it to the sections below so it can never drift again.
 */
import type { components } from '../generated/admin-api';
import type { Expect, Compatible, SameKeys } from './helpers';

import type { LLMCacheControlDto, ToggleLLMCacheRequest } from '../models/cache-types';
import type { CreateFunctionCostDto, UpdateFunctionCostDto } from '../models/functions';
import type { CreateIpFilterDto, UpdateIpFilterDto } from '../models/ipFilter';
import type {
  MediaStorageStats,
  MediaTypeStats,
  OverallMediaStorageStats,
  SimpleRetentionResponse,
  UpdateMediaRetentionPolicyRequest,
  UpdateSimpleRetentionRequest,
} from '../models/media';
import type {
  CreateModelAuthorDto,
  CreateModelDto,
  CreateModelSeriesDto,
  ModelAuthorDto,
  ModelDto,
  ModelSeriesDto,
  SeriesSimpleModelDto,
  SimpleModelSeriesDto,
  UpdateModelAuthorDto,
  UpdateModelDto,
  UpdateModelSeriesDto,
} from '../models/model';
import type {
  BulkDeleteResult,
  BulkMappingResult,
  BulkUpdateResult,
  ModelCapabilitiesDto,
  ModelProviderMappingDto,
} from '../models/modelMapping';
import type { PricingConstraints, PricingRule, PricingRulesConfig } from '../models/pricing';
import type { ProviderReferenceDto, StandardApiKeyTestResponse } from '../models/provider';
import type {
  CreateGlobalSettingDto,
  UpdateGlobalSettingByKeyDto,
  UpdateGlobalSettingDto,
} from '../models/settings';
import type { CreateNotificationDto, HealthStatusDto, NotificationDto } from '../models/system';
import type {
  AdjustBalanceDto,
  CreateVirtualKeyGroupRequestDto,
  UpdateVirtualKeyGroupRequestDto,
  VirtualKeyGroupDto,
  VirtualKeyGroupTransactionDto,
} from '../models/virtualKey';

type Wire = components['schemas'];

// ---- Strict: same keys, compatible property types ----

// models/cache-types.ts
export type CheckLLMCacheControlDto = Expect<Compatible<LLMCacheControlDto, Wire['LLMCacheControlDto']>>;
export type CheckToggleLLMCacheRequest = Expect<Compatible<ToggleLLMCacheRequest, Wire['ToggleLLMCacheRequest']>>;

// models/functions.ts
export type CheckCreateFunctionCostDto = Expect<Compatible<CreateFunctionCostDto, Wire['CreateFunctionCostDto']>>;
export type CheckUpdateFunctionCostDto = Expect<Compatible<UpdateFunctionCostDto, Wire['UpdateFunctionCostDto']>>;

// models/media.ts
export type CheckMediaStorageStats = Expect<Compatible<MediaStorageStats, Wire['MediaStorageStats']>>;
export type CheckMediaTypeStats = Expect<Compatible<MediaTypeStats, Wire['MediaTypeStats']>>;
export type CheckOverallMediaStorageStats = Expect<Compatible<OverallMediaStorageStats, Wire['OverallMediaStorageStats']>>;
export type CheckSimpleRetentionResponse = Expect<Compatible<SimpleRetentionResponse, Wire['SimpleRetentionResponse']>>;
export type CheckUpdateMediaRetentionPolicyRequest = Expect<Compatible<UpdateMediaRetentionPolicyRequest, Wire['UpdateMediaRetentionPolicyRequest']>>;
export type CheckUpdateSimpleRetentionRequest = Expect<Compatible<UpdateSimpleRetentionRequest, Wire['UpdateSimpleRetentionRequest']>>;

// models/model.ts
export type CheckCreateModelAuthorDto = Expect<Compatible<CreateModelAuthorDto, Wire['CreateModelAuthorDto']>>;
export type CheckCreateModelDto = Expect<Compatible<CreateModelDto, Wire['CreateModelDto']>>;
export type CheckCreateModelSeriesDto = Expect<Compatible<CreateModelSeriesDto, Wire['CreateModelSeriesDto']>>;
export type CheckModelAuthorDto = Expect<Compatible<ModelAuthorDto, Wire['ModelAuthorDto']>>;
export type CheckModelDto = Expect<Compatible<ModelDto, Wire['ModelDto']>>;
export type CheckModelSeriesDto = Expect<Compatible<ModelSeriesDto, Wire['ModelSeriesDto']>>;
export type CheckSeriesSimpleModelDto = Expect<Compatible<SeriesSimpleModelDto, Wire['SeriesSimpleModelDto']>>;
export type CheckSimpleModelSeriesDto = Expect<Compatible<SimpleModelSeriesDto, Wire['SimpleModelSeriesDto']>>;
export type CheckUpdateModelAuthorDto = Expect<Compatible<UpdateModelAuthorDto, Wire['UpdateModelAuthorDto']>>;
export type CheckUpdateModelDto = Expect<Compatible<UpdateModelDto, Wire['UpdateModelDto']>>;
export type CheckUpdateModelSeriesDto = Expect<Compatible<UpdateModelSeriesDto, Wire['UpdateModelSeriesDto']>>;

// models/modelMapping.ts
export type CheckBulkDeleteResult = Expect<Compatible<BulkDeleteResult, Wire['BulkDeleteResult']>>;
export type CheckBulkMappingResult = Expect<Compatible<BulkMappingResult, Wire['BulkMappingResult']>>;
export type CheckBulkUpdateResult = Expect<Compatible<BulkUpdateResult, Wire['BulkUpdateResult']>>;
export type CheckModelCapabilitiesDto = Expect<Compatible<ModelCapabilitiesDto, Wire['ModelCapabilitiesDto']>>;
export type CheckModelProviderMappingDto = Expect<Compatible<ModelProviderMappingDto, Wire['ModelProviderMappingDto']>>;

// models/pricing.ts
export type CheckPricingConstraints = Expect<Compatible<PricingConstraints, Wire['PricingConstraints']>>;

// models/provider.ts
export type CheckProviderReferenceDto = Expect<Compatible<ProviderReferenceDto, Wire['ProviderReferenceDto']>>;

// models/settings.ts
export type CheckCreateGlobalSettingDto = Expect<Compatible<CreateGlobalSettingDto, Wire['CreateGlobalSettingDto']>>;
export type CheckUpdateGlobalSettingByKeyDto = Expect<Compatible<UpdateGlobalSettingByKeyDto, Wire['UpdateGlobalSettingByKeyDto']>>;
export type CheckUpdateGlobalSettingDto = Expect<Compatible<UpdateGlobalSettingDto, Wire['UpdateGlobalSettingDto']>>;

// models/system.ts
export type CheckCreateNotificationDto = Expect<Compatible<CreateNotificationDto, Wire['CreateNotificationDto']>>;

// models/virtualKey.ts
export type CheckAdjustBalanceDto = Expect<Compatible<AdjustBalanceDto, Wire['AdjustBalanceDto']>>;
export type CheckCreateVirtualKeyGroupRequestDto = Expect<Compatible<CreateVirtualKeyGroupRequestDto, Wire['CreateVirtualKeyGroupRequestDto']>>;
export type CheckUpdateVirtualKeyGroupRequestDto = Expect<Compatible<UpdateVirtualKeyGroupRequestDto, Wire['UpdateVirtualKeyGroupRequestDto']>>;
export type CheckVirtualKeyGroupDto = Expect<Compatible<VirtualKeyGroupDto, Wire['VirtualKeyGroupDto']>>;
export type CheckVirtualKeyGroupTransactionDto = Expect<Compatible<VirtualKeyGroupTransactionDto, Wire['VirtualKeyGroupTransactionDto']>>;

// ---- Key-set only: manual model intentionally narrows wire types ----

// models/ipFilter.ts
export type CheckCreateIpFilterDto = Expect<SameKeys<CreateIpFilterDto, Wire['CreateIpFilterDto']>>;
export type CheckUpdateIpFilterDto = Expect<SameKeys<UpdateIpFilterDto, Wire['UpdateIpFilterDto']>>;

// models/pricing.ts
export type CheckPricingRule = Expect<SameKeys<PricingRule, Wire['PricingRule']>>;
export type CheckPricingRulesConfig = Expect<SameKeys<PricingRulesConfig, Wire['PricingRulesConfig']>>;

// models/provider.ts
export type CheckStandardApiKeyTestResponse = Expect<SameKeys<StandardApiKeyTestResponse, Wire['StandardApiKeyTestResponse']>>;

// models/system.ts
export type CheckHealthStatusDto = Expect<SameKeys<HealthStatusDto, Wire['HealthStatusDto']>>;
export type CheckNotificationDto = Expect<SameKeys<NotificationDto, Wire['NotificationDto']>>;
