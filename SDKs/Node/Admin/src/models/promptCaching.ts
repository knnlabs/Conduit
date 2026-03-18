export interface PromptCachingConfigDto {
  autoInjectEnabled: boolean;
  injectionPoints: CacheInjectionPointDto[];
}

export interface UpdatePromptCachingConfigDto {
  autoInjectEnabled: boolean;
  injectionPoints: CacheInjectionPointDto[];
}

export interface CacheInjectionPointDto {
  role?: string | null;
  index?: number | null;
}
