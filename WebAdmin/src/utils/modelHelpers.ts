import type { ModelDto } from '@/lib/admin-api';

/**
 * Determines the primary type of a model based on its capabilities
 */
export function getModelPrimaryType(model?: ModelDto): string {
  if (!model) return 'Unknown';
  
  // Priority order for determining primary type
  if (model.supportsVideoGeneration) return 'Video';
  if (model.supportsImageGeneration) return 'Image';
  if (model.supportsEmbeddings) return 'Embedding';
  if (model.supportsChat || model.supportsImageInput || model.supportsVideoInput) return 'Chat';
  
  return 'Unknown';
}

/**
 * Gets the badge color for a model type
 */
export function getModelTypeBadgeColor(type: string): string {
  switch (type) {
    case 'Chat': return 'blue';
    case 'Image': return 'purple';
    case 'Video': return 'pink';
    case 'Embedding': return 'green';
    default: return 'gray';
  }
}

/**
 * Formats a token limit for compact display (e.g. 128000 -> "128K", 2000000 -> "2.0M")
 */
export function formatTokenLimit(tokens: number | null): string {
  if (!tokens) return 'Default';
  if (tokens >= 1000000) return `${(tokens / 1000000).toFixed(1)}M`;
  if (tokens >= 1000) return `${(tokens / 1000).toFixed(0)}K`;
  return tokens.toString();
}

/**
 * Formats a speed or quality score for display
 */
export function formatScore(score: number | null, type: 'speed' | 'quality'): string | null {
  if (!score) return null;

  if (type === 'speed') {
    if (score >= 2) return `${score.toFixed(1)}x faster`;
    if (score === 1) return 'Standard speed';
    return `${(1 / score).toFixed(1)}x slower`;
  }

  // Quality score
  const percentage = (score * 100).toFixed(0);
  if (score >= 0.9) return `${percentage}% quality`;
  return `${percentage}% quality (degraded)`;
}
