export type PromptCachingStrategy = 'OpenRouterAutomatic' | 'OpenRouterExplicit';

export interface CacheInjectionPointDto {
  role?: 'system' | 'user' | 'assistant' | null;
  index?: number | null;
}

export interface PromptCachingRuleDto {
  name: string;
  enabled: boolean;
  provider: 'OpenRouter';
  modelPattern: string;
  strategy: PromptCachingStrategy;
  ttl?: '5m' | '1h' | null;
  injectionPoints: CacheInjectionPointDto[];
}

export interface PromptCachingConfigDto {
  schemaVersion: 2;
  enabled: boolean;
  rules: PromptCachingRuleDto[];
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
