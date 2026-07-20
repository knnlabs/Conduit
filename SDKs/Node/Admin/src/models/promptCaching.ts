export type PromptCachingStrategy = 'Automatic' | 'Explicit';

export interface CacheInjectionPointDto {
  role?: 'system' | 'developer' | 'user' | 'assistant' | null;
  index?: number | null;
}

export interface PromptCachingRuleDto {
  name: string;
  enabled: boolean;
  provider: string;
  modelPattern: string;
  strategy: PromptCachingStrategy;
  ttl?: string | null;
  injectionPoints: CacheInjectionPointDto[];
}

export interface PromptCachingConfigDto {
  schemaVersion: 3;
  enabled: boolean;
  rules: PromptCachingRuleDto[];
}

export interface PromptCachingAnalyticsDto {
  from: string; to: string; requests: number; eligibleMisses: number; readEvents: number;
  writeEvents: number; unknownOutcomes: number; cachedTokens: number; grossSavings: number;
  writePremium: number; netSavings: number; hitLatencyMs?: number | null; missLatencyMs?: number | null;
  affinityReuse: number; failovers: number;
  providerDistribution: Array<{ provider: string; mappingId?: number | null; requests: number }>;
}

export type UpdatePromptCachingConfigDto = PromptCachingConfigDto;

export interface PromptCachingCapabilityDto {
  provider: string;
  modelPattern: string;
  strategies: PromptCachingStrategy[];
  ttls: string[];
  minimumTokens?: number | null;
  maxBreakpoints: number;
  providerManaged: boolean;
}
