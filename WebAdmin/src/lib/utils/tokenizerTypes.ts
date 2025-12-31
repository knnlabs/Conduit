/**
 * TokenizerType enum mapping and utilities for WebAdmin display
 * Matches the backend TokenizerType enum values
 */

/**
 * TokenizerType enum values matching backend
 * These numeric values must match the backend enum exactly
 */
export enum TokenizerType {
  // OpenAI tokenizers
  Cl100KBase = 0,      // GPT-3.5-turbo, GPT-4, GPT-4-turbo
  P50KBase = 1,        // Older GPT-3 models (text-davinci-002, etc.)
  P50KEdit = 2,        // Edit models (text-davinci-edit-001)
  R50KBase = 3,        // Codex models
  O200KBase = 4,       // GPT-4o, GPT-4o-mini (newest tokenizer)
  
  // Anthropic tokenizers
  Claude = 5,          // Claude 2 and earlier
  Claude3 = 6,         // Claude 3 (Haiku, Sonnet, Opus)
  
  // Google tokenizers
  Gemini = 7,          // Gemini models
  PaLM = 8,            // PaLM and PaLM 2 models
  
  // Meta tokenizers
  LLaMA = 9,           // LLaMA 1
  LLaMA2 = 10,         // LLaMA 2
  LLaMA3 = 11,         // LLaMA 3
  
  // Mistral tokenizers
  Mistral = 12,        // Mistral models
  
  // Cohere tokenizers
  Cohere = 13,         // Command and other Cohere models
  
  // Other provider tokenizers
  Groq = 14,           // Groq-specific if different from base models
  Cerebras = 15,       // Cerebras-specific if different
  MiniMax = 16,        // MiniMax models
  
  // Common open-source tokenizers
  SentencePiece = 17,  // Used by various models
  BPE = 18,            // Byte Pair Encoding (generic)
  WordPiece = 19,      // Used by BERT-style models
  Tiktoken = 20        // OpenAI's open-source tokenizer library
}

/**
 * Human-readable display names for tokenizer types
 */
export const TOKENIZER_DISPLAY_NAMES: Record<TokenizerType, string> = {
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
export function getTokenizerDisplayName(tokenizer: TokenizerType | number, short = false): string {
  const tokenizerType = typeof tokenizer === 'number' ? tokenizer as TokenizerType : tokenizer;
  const nameMap = short ? TOKENIZER_SHORT_NAMES : TOKENIZER_DISPLAY_NAMES;
  return nameMap[tokenizerType] ?? `Unknown (${tokenizerType})`;
}

/**
 * Check if a value is a valid TokenizerType
 */
export function isValidTokenizerType(value: unknown): value is TokenizerType {
  return typeof value === 'number' && value >= 0 && value <= 20 && Number.isInteger(value);
}

/**
 * Parse tokenizer type from string or number
 */
export function parseTokenizerType(value: string | number | null | undefined): TokenizerType | null {
  if (value === null || value === undefined) return null;
  
  const num = typeof value === 'string' ? parseInt(value, 10) : value;
  if (isNaN(num) || !isValidTokenizerType(num)) return null;
  
  return num;
}