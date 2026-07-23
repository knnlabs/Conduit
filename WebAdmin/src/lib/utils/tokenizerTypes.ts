/**
 * TokenizerType enum mapping and utilities for WebAdmin display
 * Matches the backend TokenizerType enum values
 */

/**
 * TokenizerType enum values matching backend
 * These numeric values must match the backend enum exactly
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
} as const;
export type TokenizerType = (typeof TokenizerType)[keyof typeof TokenizerType];

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
 * Options for Select components
 */
export const TOKENIZER_SELECT_OPTIONS = Object.entries(TOKENIZER_DISPLAY_NAMES).map(([value, label]) => ({
  value: value.toString(),
  label
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
  return typeof value === 'string' && Object.values(TokenizerType).includes(value as TokenizerType);
}

/**
 * Parse tokenizer type from string or number
 */
export function parseTokenizerType(value: string | number | null | undefined): TokenizerType | null {
  if (value === null || value === undefined) return null;
  
  if (isValidTokenizerType(value)) return value;
  const numeric = typeof value === 'number' ? value : Number(value);
  if (!Number.isInteger(numeric)) return null;
  return Object.values(TokenizerType)[numeric] ?? null;
}
