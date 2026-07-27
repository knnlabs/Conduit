import {
  TokenizerType,
  isValidTokenizerType,
} from "@/lib/utils/tokenizerTypes";

export const MIN_MODEL_TOKEN_LIMIT = 1024;

export const modelFormValidation = {
  name: (value: string): string | null =>
    value.trim() ? null : "Name is required",
  tokenizerType: (
    value: TokenizerType | null | undefined,
  ): string | null => {
    if (value === null || value === undefined) {
      return "Tokenizer type is required";
    }
    return isValidTokenizerType(value) ? null : "Invalid tokenizer type";
  },
  maxInputTokens: validateTokenLimit,
  maxOutputTokens: validateTokenLimit,
};

export function validateTokenLimit(
  value: number | null | undefined,
): string | null {
  return value !== null &&
    value !== undefined &&
    value < MIN_MODEL_TOKEN_LIMIT
    ? `Minimum value is ${MIN_MODEL_TOKEN_LIMIT} tokens`
    : null;
}
