/**
 * Provider type enumeration matching backend ProviderType enum
 * These values must match the backend enum values exactly
 */
export enum ProviderType {
  OpenAI = 1,
  Groq = 2,
  Replicate = 3,
  Fireworks = 4,
  OpenAICompatible = 5,
  MiniMax = 6,
  Ultravox = 7,
  ElevenLabs = 8,
  Cerebras = 9,
  SambaNova = 10,
  DeepInfra = 11,
}

/**
 * Map of provider type to display name
 */
export const PROVIDER_TYPE_NAMES: Record<number, string> = {
  [ProviderType.OpenAI]: 'OpenAI',
  [ProviderType.Groq]: 'Groq',
  [ProviderType.Replicate]: 'Replicate',
  [ProviderType.Fireworks]: 'Fireworks',
  [ProviderType.OpenAICompatible]: 'OpenAI Compatible',
  [ProviderType.MiniMax]: 'MiniMax',
  [ProviderType.Ultravox]: 'Ultravox',
  [ProviderType.ElevenLabs]: 'ElevenLabs',
  [ProviderType.Cerebras]: 'Cerebras',
  [ProviderType.SambaNova]: 'SambaNova',
  [ProviderType.DeepInfra]: 'DeepInfra',
};

/**
 * Get provider type display name
 * @param providerType - The provider type number
 * @returns The display name or 'Unknown' if not found
 */
export function getProviderTypeName(providerType: number): string {
  return PROVIDER_TYPE_NAMES[providerType] ?? 'Unknown';
}

/**
 * Check if a provider type is valid
 */
export function isValidProviderType(providerType: number): providerType is ProviderType {
  return Object.values(ProviderType).includes(providerType as ProviderType);
}