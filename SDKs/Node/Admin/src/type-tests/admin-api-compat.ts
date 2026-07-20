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
 * The 24 pre-existing drifted types cataloged in
 * https://github.com/nickna/Conduit/issues/1038 have been reconciled and are
 * now asserted below (a couple were renamed to distinct client-abstraction
 * names so they no longer falsely pair with unrelated wire schemas —
 * ServiceStatusMapDto, SystemResourceMetricsDto).
 *
 * Two of the 24 remain unasserted because the current Admin OpenAPI exposes NO
 * corresponding wire schema (the backend DTOs exist under
 * ConduitLLM.Configuration.DTOs.Cache but no annotated endpoint surfaces them):
 * `UpdateCacheConfigDto` (models/cache-types.ts) and `UpdateCachePolicyDto`
 * (models/configuration.ts). Add assertions once those endpoints are exposed.
 * (`ExportFormat` from the issue has no named hand-written type in this SDK, so
 * there is nothing to pair against the wire `ExportFormat` enum.)
 */
import type { components } from '../generated/admin-api';
import type { Expect, Compatible, SameKeys } from './helpers';

import type { RequestLog } from '../models/analyticsExport';
import type { CreateFunctionCostDto, UpdateFunctionCostDto } from '../models/functions';
import type { CreateModelCostDto, ModelCostDto, UpdateModelCostDto } from '../models/modelCost';
import type {
  CreateIpFilterDto,
  IpCheckResult,
  IpFilterDto,
  IpFilterSettingsDto,
  UpdateIpFilterDto,
} from '../models/ipFilter';
import type {
  CreateMediaRetentionPolicyRequest,
  MediaRecord,
  MediaRetentionPolicy,
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
import type { ModelUsage } from '../models/analytics-core';
import type { VirtualKeyUsageSummary } from '../models/analytics-usage';
import type {
  PricingAuditSummary,
  PricingConstraints,
  PricingRule,
  PricingRulesConfig,
  PricingSimulationRequest,
} from '../models/pricing';
import type { ProviderReferenceDto, StandardApiKeyTestResponse } from '../models/provider';
import type {
  CreateGlobalSettingDto,
  GlobalSettingDto,
  UpdateGlobalSettingByKeyDto,
  UpdateGlobalSettingDto,
} from '../models/settings';
import type {
  CreateNotificationDto,
  HealthStatusDto,
  NotificationDto,
  SystemInfoDto,
  UpdateNotificationDto,
} from '../models/system';
import type {
  AdjustBalanceDto,
  CreateVirtualKeyGroupRequestDto,
  UpdateVirtualKeyGroupRequestDto,
  VirtualKeyDto,
  VirtualKeyGroupDto,
  VirtualKeyGroupTransactionDto,
  VirtualKeyValidationResult,
} from '../models/virtualKey';

type Wire = components['schemas'];

// ---- Strict: same keys, compatible property types ----

// models/functions.ts
export type CheckCreateFunctionCostDto = Expect<Compatible<CreateFunctionCostDto, Wire['CreateFunctionCostDto']>>;
export type CheckUpdateFunctionCostDto = Expect<Compatible<UpdateFunctionCostDto, Wire['UpdateFunctionCostDto']>>;

// models/media.ts
export type CheckCreateMediaRetentionPolicyRequest = Expect<Compatible<CreateMediaRetentionPolicyRequest, Wire['CreateMediaRetentionPolicyRequest']>>; // reconciled in issue #1038
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

// models/analytics-core.ts — reconciled in issue #1038
export type CheckModelUsage = Expect<Compatible<ModelUsage, Wire['ModelUsage']>>;

// models/analytics-usage.ts — reconciled in issue #1038
export type CheckVirtualKeyUsageSummary = Expect<Compatible<VirtualKeyUsageSummary, Wire['VirtualKeyUsageSummary']>>;

// models/pricing.ts
export type CheckPricingConstraints = Expect<Compatible<PricingConstraints, Wire['PricingConstraints']>>;
export type CheckPricingAuditSummary = Expect<Compatible<PricingAuditSummary, Wire['PricingAuditSummary']>>; // reconciled in issue #1038

// models/provider.ts
export type CheckProviderReferenceDto = Expect<Compatible<ProviderReferenceDto, Wire['ProviderReferenceDto']>>;

// models/ipFilter.ts — reconciled in issue #1038
export type CheckIpCheckResult = Expect<Compatible<IpCheckResult, Wire['IpCheckResult']>>;

// models/settings.ts
export type CheckGlobalSettingDto = Expect<Compatible<GlobalSettingDto, Wire['GlobalSettingDto']>>; // reconciled in issue #1038
export type CheckCreateGlobalSettingDto = Expect<Compatible<CreateGlobalSettingDto, Wire['CreateGlobalSettingDto']>>;
export type CheckUpdateGlobalSettingByKeyDto = Expect<Compatible<UpdateGlobalSettingByKeyDto, Wire['UpdateGlobalSettingByKeyDto']>>;
export type CheckUpdateGlobalSettingDto = Expect<Compatible<UpdateGlobalSettingDto, Wire['UpdateGlobalSettingDto']>>;

// models/system.ts
export type CheckSystemInfoDto = Expect<Compatible<SystemInfoDto, Wire['SystemInfoDto']>>; // reconciled in issue #1038
export type CheckCreateNotificationDto = Expect<Compatible<CreateNotificationDto, Wire['CreateNotificationDto']>>;
export type CheckUpdateNotificationDto = Expect<Compatible<UpdateNotificationDto, Wire['UpdateNotificationDto']>>; // reconciled in issue #1038

// models/virtualKey.ts
export type CheckVirtualKeyDto = Expect<Compatible<VirtualKeyDto, Wire['VirtualKeyDto']>>; // reconciled in issue #1038
export type CheckAdjustBalanceDto = Expect<Compatible<AdjustBalanceDto, Wire['AdjustBalanceDto']>>;
export type CheckCreateVirtualKeyGroupRequestDto = Expect<Compatible<CreateVirtualKeyGroupRequestDto, Wire['CreateVirtualKeyGroupRequestDto']>>;
export type CheckUpdateVirtualKeyGroupRequestDto = Expect<Compatible<UpdateVirtualKeyGroupRequestDto, Wire['UpdateVirtualKeyGroupRequestDto']>>;
export type CheckVirtualKeyGroupDto = Expect<Compatible<VirtualKeyGroupDto, Wire['VirtualKeyGroupDto']>>;
export type CheckVirtualKeyGroupTransactionDto = Expect<Compatible<VirtualKeyGroupTransactionDto, Wire['VirtualKeyGroupTransactionDto']>>;
export type CheckVirtualKeyValidationResult = Expect<Compatible<VirtualKeyValidationResult, Wire['VirtualKeyValidationResult']>>; // reconciled in issue #1038

// ---- Key-set only: manual model intentionally narrows wire types ----

// models/analyticsExport.ts — reconciled in issue #1038
// virtualKey/billingMethod reference nested wire schemas typed loosely on the client.
export type CheckRequestLog = Expect<SameKeys<RequestLog, Wire['RequestLog']>>;

// models/ipFilter.ts
export type CheckCreateIpFilterDto = Expect<SameKeys<CreateIpFilterDto, Wire['CreateIpFilterDto']>>;
// reconciled in issue #1038 — filterType narrows the wire string to a 'whitelist' | 'blacklist' union.
export type CheckIpFilterDto = Expect<SameKeys<IpFilterDto, Wire['IpFilterDto']>>;
// reconciled in issue #1038 — filterMode narrows wire string to a union; filters are typed IpFilterDto[].
export type CheckIpFilterSettingsDto = Expect<SameKeys<IpFilterSettingsDto, Wire['IpFilterSettingsDto']>>;
export type CheckUpdateIpFilterDto = Expect<SameKeys<UpdateIpFilterDto, Wire['UpdateIpFilterDto']>>;

// models/modelCost.ts — reconciled in issue #1038
// modelType narrows the wire string to a client ModelType enum; flat media/inference cost fields
// were removed (that pricing lives in pricingConfiguration).
export type CheckModelCostDto = Expect<SameKeys<ModelCostDto, Wire['ModelCostDto']>>;
export type CheckCreateModelCostDto = Expect<SameKeys<CreateModelCostDto, Wire['CreateModelCostDto']>>;
export type CheckUpdateModelCostDto = Expect<SameKeys<UpdateModelCostDto, Wire['UpdateModelCostDto']>>;

// models/media.ts — reconciled in issue #1038
// mediaType narrows wire string to a union; virtualKey references a nested wire schema.
export type CheckMediaRecord = Expect<SameKeys<MediaRecord, Wire['MediaRecord']>>;
// virtualKeyGroups references a nested wire schema typed loosely on the client.
export type CheckMediaRetentionPolicy = Expect<SameKeys<MediaRetentionPolicy, Wire['MediaRetentionPolicy']>>;

// models/pricing.ts
export type CheckPricingRule = Expect<SameKeys<PricingRule, Wire['PricingRule']>>;
export type CheckPricingRulesConfig = Expect<SameKeys<PricingRulesConfig, Wire['PricingRulesConfig']>>;
// reconciled in issue #1038 — parameters narrows the wire's Record<string, never> to a typed dictionary.
export type CheckPricingSimulationRequest = Expect<SameKeys<PricingSimulationRequest, Wire['PricingSimulationRequest']>>;

// models/provider.ts
export type CheckStandardApiKeyTestResponse = Expect<SameKeys<StandardApiKeyTestResponse, Wire['StandardApiKeyTestResponse']>>;

// models/system.ts
export type CheckHealthStatusDto = Expect<SameKeys<HealthStatusDto, Wire['HealthStatusDto']>>;
export type CheckNotificationDto = Expect<SameKeys<NotificationDto, Wire['NotificationDto']>>;
