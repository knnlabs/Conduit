/** Provider values as serialized by the Admin API. */
export const ProviderType = {
  Unknown: 'unknown',
  OpenAI: 'openAI',
  Groq: 'groq',
  Replicate: 'replicate',
  Fireworks: 'fireworks',
  OpenAICompatible: 'openAICompatible',
  MiniMax: 'miniMax',
  Ultravox: 'ultravox',
  ElevenLabs: 'elevenLabs',
  Cerebras: 'cerebras',
  SambaNova: 'sambaNova',
  DeepInfra: 'deepInfra',
  Cloudflare: 'cloudflare',
  OpenRouter: 'openRouter',
  Meta: 'meta',
  Azure: 'azure',
} as const;

export type ProviderType = (typeof ProviderType)[keyof typeof ProviderType];
