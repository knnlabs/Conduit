export const isValidModelPattern = (pattern: string): boolean => {
  if (!pattern || pattern.trim() === '') return false;
  
  // Check for invalid characters
  const invalidChars = /[<>:"|?]/;
  if (invalidChars.test(pattern)) return false;
  
  // Allow letters, numbers, hyphens, underscores, slashes, dots, and asterisks
  const validPattern = /^[a-zA-Z0-9\-_/.* ]+$/;
  return validPattern.test(pattern);
};

export const isPatternMatch = (pattern: string, modelId: string): boolean => {
  if (!pattern || !modelId) return false;
  
  // Convert pattern to regex
  const regexPattern = pattern
    .replace(/\./g, '\\.')
    .replace(/\*/g, '.*')
    .replace(/\?/g, '.');
  
  const regex = new RegExp(`^${regexPattern}$`, 'i');
  return regex.test(modelId);
};

export const getPatternExamples = (pattern: string): string[] => {
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
};

export const validatePatternSyntax = (pattern: string): { isValid: boolean; errors: string[] } => {
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
  
  // Check for multiple consecutive asterisks
  if (pattern.includes('**')) {
    errors.push('Pattern cannot contain consecutive asterisks');
  }
  
  // Check for leading/trailing asterisks without content
  if (pattern.startsWith('*') && pattern.length === 1) {
    errors.push('Pattern cannot be a single asterisk');
  }
  
  return { isValid: errors.length === 0, errors };
};

export const normalizeModelPattern = (pattern: string): string => {
  return pattern.trim().toLowerCase();
};

export const getPatternSpecificity = (pattern: string): number => {
  // Higher number = more specific
  // No wildcards = 100
  // With wildcards = 100 - (number of wildcards * 10) - (wildcard position weight)
  
  if (!pattern.includes('*')) {
    return 100;
  }
  
  const wildcardCount = (pattern.match(/\*/g) || []).length;
  const firstWildcardPos = pattern.indexOf('*');
  const positionWeight = firstWildcardPos === 0 ? 20 : Math.max(0, 20 - firstWildcardPos);
  
  return Math.max(0, 100 - (wildcardCount * 10) - positionWeight);
};