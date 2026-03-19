/**
 * Model pattern matching and validation utilities
 *
 * Used by the model cost mapping system to match model identifiers
 * against wildcard patterns (e.g., "openai/gpt-4*").
 */

import type { PatternValidationResult } from './types';

/**
 * Check whether a model pattern string contains only valid characters.
 * Allows letters, numbers, hyphens, underscores, slashes, dots, spaces, and asterisks.
 */
export function isValidModelPattern(pattern: string): boolean {
  if (!pattern || pattern.trim() === '') return false;

  const invalidChars = /[<>:"|?]/;
  if (invalidChars.test(pattern)) return false;

  const validPattern = /^[a-zA-Z0-9\-_/.* ]+$/;
  return validPattern.test(pattern);
}

/**
 * Test whether a wildcard pattern matches a given model identifier.
 * `*` matches any sequence of characters; `?` matches a single character.
 * Matching is case-insensitive.
 */
export function isPatternMatch(pattern: string, modelId: string): boolean {
  if (!pattern || !modelId) return false;

  const regexPattern = pattern
    .replace(/\./g, '\\.')
    .replace(/\*/g, '.*')
    .replace(/\?/g, '.');

  const regex = new RegExp(`^${regexPattern}$`, 'i');
  return regex.test(modelId);
}

/**
 * Generate example model identifiers that would match a given pattern.
 * Returns up to 3 examples.
 */
export function getPatternExamples(pattern: string): string[] {
  const examples: string[] = [];

  if (pattern.includes('*')) {
    if (pattern.startsWith('openai/')) {
      examples.push('openai/gpt-4', 'openai/gpt-3.5-turbo', 'openai/text-embedding-ada-002');
    } else if (pattern.startsWith('anthropic/')) {
      examples.push('anthropic/claude-3-opus', 'anthropic/claude-3-sonnet', 'anthropic/claude-3-haiku');
    } else if (pattern.includes('gpt-4')) {
      examples.push('openai/gpt-4', 'openai/gpt-4-turbo', 'openai/gpt-4-32k');
    } else {
      examples.push(`${pattern.replace('*', 'model-1')}`, `${pattern.replace('*', 'model-2')}`);
    }
  } else {
    examples.push(pattern);
  }

  return examples.slice(0, 3);
}

/**
 * Validate the syntax of a model pattern string.
 * Checks for empty patterns, length limits, invalid characters,
 * consecutive asterisks, and single-asterisk patterns.
 */
export function validatePatternSyntax(pattern: string): PatternValidationResult {
  const errors: string[] = [];

  if (!pattern || pattern.trim() === '') {
    errors.push('Pattern cannot be empty');
    return { isValid: false, errors };
  }

  if (pattern.length > 100) {
    errors.push('Pattern cannot exceed 100 characters');
  }

  if (!isValidModelPattern(pattern)) {
    errors.push('Pattern contains invalid characters');
  }

  if (pattern.includes('**')) {
    errors.push('Pattern cannot contain consecutive asterisks');
  }

  if (pattern.startsWith('*') && pattern.length === 1) {
    errors.push('Pattern cannot be a single asterisk');
  }

  return { isValid: errors.length === 0, errors };
}

/**
 * Normalize a model pattern by trimming whitespace and lowercasing.
 */
export function normalizeModelPattern(pattern: string): string {
  return pattern.trim().toLowerCase();
}

/**
 * Calculate pattern specificity. Higher values indicate more specific patterns.
 * Exact patterns score 100; wildcards reduce the score.
 */
export function getPatternSpecificity(pattern: string): number {
  if (!pattern.includes('*')) {
    return 100;
  }

  const wildcardCount = (pattern.match(/\*/g) ?? []).length;
  const firstWildcardPos = pattern.indexOf('*');
  const positionWeight = firstWildcardPos === 0 ? 20 : Math.max(0, 20 - firstWildcardPos);

  return Math.max(0, 100 - (wildcardCount * 10) - positionWeight);
}
