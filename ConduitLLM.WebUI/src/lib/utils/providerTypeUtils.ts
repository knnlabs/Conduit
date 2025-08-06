/**
 * Utility functions for provider type conversion and display
 * Provides type-safe conversion between numeric provider types and display strings
 */

// Import ProviderType from the SDK instead of duplicating it
import { ProviderType } from '@knn_labs/conduit-admin-client';

// Re-export the SDK's ProviderType enum
export { ProviderType };

// Map ProviderType enum values to display strings
export const PROVIDER_TYPE_DISPLAY_NAMES: Record<ProviderType, string> = {
  [ProviderType.OpenAI]: 'OpenAI',
  [ProviderType.Groq]: 'Groq',
  [ProviderType.Replicate]: 'Replicate',
  [ProviderType.Fireworks]: 'Fireworks AI',
  [ProviderType.OpenAICompatible]: 'OpenAI Compatible',
  [ProviderType.MiniMax]: 'MiniMax',
  [ProviderType.Ultravox]: 'Ultravox',
  [ProviderType.ElevenLabs]: 'ElevenLabs',
  [ProviderType.Cerebras]: 'Cerebras',
};

// Convert ProviderType to display name
export const getProviderDisplayName = (providerType: ProviderType): string => {
  return PROVIDER_TYPE_DISPLAY_NAMES[providerType] ?? `Provider ${providerType}`;
};

// Convert display name back to ProviderType (for reverse lookups)
export const getProviderTypeFromDisplayName = (displayName: string): ProviderType | undefined => {
  const entry = Object.entries(PROVIDER_TYPE_DISPLAY_NAMES).find(
    ([, name]) => name === displayName
  );
  return entry ? parseInt(entry[0], 10) as ProviderType : undefined;
};

// Convert ProviderType to select options
export const getProviderSelectOptions = () => {
  return Object.values(ProviderType)
    .filter((value): value is ProviderType => typeof value === 'number')
    .map(type => ({
      value: type.toString(),
      label: getProviderDisplayName(type),
    }));
};

// Convert ProviderType enum value to string for API compatibility
export const providerTypeToString = (providerType: ProviderType): string => {
  return providerType.toString();
};

// Convert string back to ProviderType
export const stringToProviderType = (str: string): ProviderType => {
  const num = parseInt(str, 10);
  if (isNaN(num)) {
    throw new Error(`Invalid provider type string: ${str}`);
  }
  return num as ProviderType;
};

// Type guard to check if a value is a valid ProviderType
export const isValidProviderType = (value: unknown): value is ProviderType => {
  return typeof value === 'number' && value in ProviderType;
};

// Map of provider names (as they come from the API) to ProviderType enum values
const PROVIDER_NAME_TO_TYPE_MAP: Record<string, ProviderType> = {
  openai: ProviderType.OpenAI,
  groq: ProviderType.Groq,
  replicate: ProviderType.Replicate,
  fireworks: ProviderType.Fireworks,
  openaicompatible: ProviderType.OpenAICompatible,
  openaiCompatible: ProviderType.OpenAICompatible, // Alternative casing
  minimax: ProviderType.MiniMax,
  ultravox: ProviderType.Ultravox,
  elevenlabs: ProviderType.ElevenLabs,
  cerebras: ProviderType.Cerebras,
};

// Convert provider name string to ProviderType enum
export const providerNameToType = (providerName: string): ProviderType => {
  const normalized = providerName.toLowerCase().replace(/[\s_-]/g, '');
  const type = PROVIDER_NAME_TO_TYPE_MAP[normalized];
  
  if (type === undefined) {
    throw new Error(`Unknown provider name: ${providerName}`);
  }
  
  return type;
};

// Reverse map of ProviderType enum values to provider names
const PROVIDER_TYPE_TO_NAME_MAP: Record<ProviderType, string> = {
  [ProviderType.OpenAI]: 'openai',
  [ProviderType.Groq]: 'groq',
  [ProviderType.Replicate]: 'replicate',
  [ProviderType.Fireworks]: 'fireworks',
  [ProviderType.OpenAICompatible]: 'openaicompatible',
  [ProviderType.MiniMax]: 'minimax',
  [ProviderType.Ultravox]: 'ultravox',
  [ProviderType.ElevenLabs]: 'elevenlabs',
  [ProviderType.Cerebras]: 'cerebras',
};

// Get provider name string from ProviderType
export const providerTypeToName = (providerType: ProviderType): string => {
  const name = PROVIDER_TYPE_TO_NAME_MAP[providerType];
  if (!name) {
    throw new Error(`Invalid provider type: ${providerType}`);
  }
  return name;
};

// Helper to get ProviderType from a DTO that may have providerType or providerName
export const getProviderTypeFromDto = (dto: { providerType?: number; providerName?: string | null }): ProviderType => {
  // Prefer providerType if available
  if (dto.providerType !== undefined) {
    return dto.providerType as ProviderType;
  }
  
  // Fall back to providerName if needed
  if (dto.providerName) {
    return providerNameToType(dto.providerName);
  }
  
  throw new Error('DTO must have either providerType or providerName');
};