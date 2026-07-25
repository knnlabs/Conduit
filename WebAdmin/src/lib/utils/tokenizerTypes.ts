/**
 * TokenizerType wire values and display helpers for WebAdmin.
 *
 * The value union is taken from the generated OpenAPI contract rather than
 * hand-copied from the backend enum, so a backend change that regenerates
 * `src/generated/admin-api.ts` breaks this module at compile time (missing or
 * surplus entries in the `Record<TokenizerType, ...>` maps below) instead of
 * silently mislabeling models in the UI.
 */

import type { components } from '@/generated/admin-api';

/**
 * Wire representation of the backend `TokenizerType` enum, straight from the
 * generated Admin API contract.
 */
export type TokenizerType = components['schemas']['TokenizerType'];

/**
 * Named constants for each tokenizer wire value.
 *
 * `satisfies` rejects any value that is not part of the generated contract;
 * the exhaustive `Record<TokenizerType, string>` maps below reject any
 * contract value that is missing an entry here.
 */
export const TokenizerType = {
  None: 'none',
  Cl100KBase: 'cl100KBase',
  P50KBase: 'p50KBase',
  P50KEdit: 'p50KEdit',
  R50KBase: 'r50KBase',
  O200KBase: 'o200KBase',
  Claude: 'claude',
  Claude3: 'claude3',
  Gemini: 'gemini',
  PaLM: 'paLM',
  LLaMA: 'lLaMA',
  LLaMA2: 'lLaMA2',
  LLaMA3: 'lLaMA3',
  Mistral: 'mistral',
  Cohere: 'cohere',
  O200KHarmony: 'o200KHarmony',
  Kimi: 'kimi',
  Groq: 'groq',
  Cerebras: 'cerebras',
  MiniMax: 'miniMax',
  SentencePiece: 'sentencePiece',
  BPE: 'bpe',
  WordPiece: 'wordPiece',
  Tiktoken: 'tiktoken',
} as const satisfies Record<string, TokenizerType>;

/**
 * Every tokenizer wire value, in contract order.
 */
export const TOKENIZER_TYPE_VALUES: readonly TokenizerType[] =
  Object.values(TokenizerType);

/**
 * Human-readable display names for tokenizer types
 */
export const TOKENIZER_DISPLAY_NAMES: Record<TokenizerType, string> = {
  [TokenizerType.None]: 'None',
  [TokenizerType.Cl100KBase]: 'cl100k_base (GPT-4, GPT-3.5-turbo)',
  [TokenizerType.P50KBase]: 'p50k_base (GPT-3 models)',
  [TokenizerType.P50KEdit]: 'p50k_edit (Edit models)',
  [TokenizerType.R50KBase]: 'r50k_base (Codex models)',
  [TokenizerType.O200KBase]: 'o200k_base (GPT-4o, GPT-4o-mini)',
  [TokenizerType.Claude]: 'Claude (Claude 2 and earlier)',
  [TokenizerType.Claude3]: 'Claude 3 (Haiku, Sonnet, Opus)',
  [TokenizerType.Gemini]: 'Gemini',
  [TokenizerType.PaLM]: 'PaLM (PaLM and PaLM 2)',
  [TokenizerType.LLaMA]: 'LLaMA (LLaMA 1)',
  [TokenizerType.LLaMA2]: 'LLaMA 2',
  [TokenizerType.LLaMA3]: 'LLaMA 3',
  [TokenizerType.Mistral]: 'Mistral',
  [TokenizerType.Cohere]: 'Cohere',
  [TokenizerType.O200KHarmony]: 'o200k_harmony (GPT-OSS)',
  [TokenizerType.Kimi]: 'Kimi',
  [TokenizerType.Groq]: 'Groq',
  [TokenizerType.Cerebras]: 'Cerebras',
  [TokenizerType.MiniMax]: 'MiniMax',
  [TokenizerType.SentencePiece]: 'SentencePiece',
  [TokenizerType.BPE]: 'BPE (Byte Pair Encoding)',
  [TokenizerType.WordPiece]: 'WordPiece (BERT-style)',
  [TokenizerType.Tiktoken]: 'Tiktoken'
};

/**
 * Short display names for compact display (e.g., in tables)
 */
export const TOKENIZER_SHORT_NAMES: Record<TokenizerType, string> = {
  [TokenizerType.None]: 'None',
  [TokenizerType.Cl100KBase]: 'cl100k_base',
  [TokenizerType.P50KBase]: 'p50k_base',
  [TokenizerType.P50KEdit]: 'p50k_edit',
  [TokenizerType.R50KBase]: 'r50k_base',
  [TokenizerType.O200KBase]: 'o200k_base',
  [TokenizerType.Claude]: 'Claude',
  [TokenizerType.Claude3]: 'Claude 3',
  [TokenizerType.Gemini]: 'Gemini',
  [TokenizerType.PaLM]: 'PaLM',
  [TokenizerType.LLaMA]: 'LLaMA',
  [TokenizerType.LLaMA2]: 'LLaMA 2',
  [TokenizerType.LLaMA3]: 'LLaMA 3',
  [TokenizerType.Mistral]: 'Mistral',
  [TokenizerType.Cohere]: 'Cohere',
  [TokenizerType.O200KHarmony]: 'o200k_harmony',
  [TokenizerType.Kimi]: 'Kimi',
  [TokenizerType.Groq]: 'Groq',
  [TokenizerType.Cerebras]: 'Cerebras',
  [TokenizerType.MiniMax]: 'MiniMax',
  [TokenizerType.SentencePiece]: 'SentencePiece',
  [TokenizerType.BPE]: 'BPE',
  [TokenizerType.WordPiece]: 'WordPiece',
  [TokenizerType.Tiktoken]: 'Tiktoken'
};

/**
 * Ordinals of the backend C# `TokenizerType` enum
 * (`Shared/ConduitLLM.Configuration/Entities/TokenizerType.cs`).
 *
 * The Admin API serializes this enum as camelCase strings
 * (`JsonStringEnumConverter(..., allowIntegerValues: false)`), so these
 * ordinals exist only to decode legacy/persisted numeric values. They are
 * written out explicitly rather than derived from key order, so reordering
 * anything in this file cannot silently remap tokenizers.
 */
const TOKENIZER_NUMERIC_ORDINALS: Record<TokenizerType, number> = {
  [TokenizerType.None]: 0,
  [TokenizerType.Cl100KBase]: 1,
  [TokenizerType.P50KBase]: 2,
  [TokenizerType.P50KEdit]: 3,
  [TokenizerType.R50KBase]: 4,
  [TokenizerType.O200KBase]: 5,
  [TokenizerType.Claude]: 6,
  [TokenizerType.Claude3]: 7,
  [TokenizerType.Gemini]: 8,
  [TokenizerType.PaLM]: 9,
  [TokenizerType.LLaMA]: 10,
  [TokenizerType.LLaMA2]: 11,
  [TokenizerType.LLaMA3]: 12,
  [TokenizerType.Mistral]: 13,
  [TokenizerType.Cohere]: 14,
  [TokenizerType.O200KHarmony]: 15,
  [TokenizerType.Kimi]: 16,
  [TokenizerType.Groq]: 17,
  [TokenizerType.Cerebras]: 18,
  [TokenizerType.MiniMax]: 19,
  [TokenizerType.SentencePiece]: 20,
  [TokenizerType.BPE]: 21,
  [TokenizerType.WordPiece]: 22,
  [TokenizerType.Tiktoken]: 23
};

const TOKENIZER_BY_ORDINAL: ReadonlyMap<number, TokenizerType> = new Map(
  Object.entries(TOKENIZER_NUMERIC_ORDINALS).map(
    ([value, ordinal]) => [ordinal, value as TokenizerType] as const
  )
);

/**
 * Options for Select components
 */
export const TOKENIZER_SELECT_OPTIONS = TOKENIZER_TYPE_VALUES.map((value) => ({
  value,
  label: TOKENIZER_DISPLAY_NAMES[value]
}));

/**
 * Get display name for a tokenizer type
 */
export function getTokenizerDisplayName(tokenizer: string | number, short = false): string {
  const tokenizerType = parseTokenizerType(tokenizer);
  const nameMap = short ? TOKENIZER_SHORT_NAMES : TOKENIZER_DISPLAY_NAMES;
  return tokenizerType ? nameMap[tokenizerType] : `Unknown (${tokenizer})`;
}

/**
 * Check if a value is a valid TokenizerType
 */
export function isValidTokenizerType(value: unknown): value is TokenizerType {
  return typeof value === 'string' && TOKENIZER_TYPE_VALUES.includes(value as TokenizerType);
}

/**
 * Parse tokenizer type from a wire string, or from a legacy numeric enum value.
 *
 * Returns `null` for anything unrecognized so callers can surface the raw value
 * rather than display a confidently wrong tokenizer name.
 */
export function parseTokenizerType(value: string | number | null | undefined): TokenizerType | null {
  if (value === null || value === undefined) return null;

  if (isValidTokenizerType(value)) return value;
  if (typeof value === 'string' && value.trim() === '') return null;

  const numeric = typeof value === 'number' ? value : Number(value);
  if (!Number.isInteger(numeric)) return null;
  return TOKENIZER_BY_ORDINAL.get(numeric) ?? null;
}
