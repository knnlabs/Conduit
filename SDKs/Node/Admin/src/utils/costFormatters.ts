/**
 * Cost formatting utilities for model pricing display.
 *
 * These encode Conduit's business rules for displaying costs across
 * different model types (chat, embedding, image, video, audio).
 */

import { ModelType } from '../models/modelType';

/**
 * Minimal interface for cost display — accepts any object that has
 * the relevant cost fields (works with ModelCostDto and similar shapes).
 */
export interface CostDisplayFields {
  modelType: ModelType;
  inputCostPerMillionTokens?: number;
  outputCostPerMillionTokens?: number;
  embeddingCostPerMillionTokens?: number;
  imageCostPerImage?: number;
  videoCostPerSecond?: number;
}

/** Format a cost value as "per million tokens" — e.g., "$2.50" */
export function formatCostPerMillionTokens(cost?: number): string {
  if (!cost) return '-';
  return `$${cost.toFixed(2)}`;
}

/** Format a cost value as "per thousand tokens" (divides by 1000) — e.g., "$0.003" */
export function formatCostPerThousandTokens(cost?: number): string {
  if (!cost) return '-';
  return `$${(cost / 1000).toFixed(3)}`;
}

/** Format a cost value as "per image" — e.g., "$0.0400" */
export function formatCostPerImage(cost?: number): string {
  if (!cost) return '-';
  return `$${cost.toFixed(4)}`;
}

/** Format a cost value as "per minute" — e.g., "$0.0060" */
export function formatCostPerMinute(cost?: number): string {
  if (!cost) return '-';
  return `$${cost.toFixed(4)}`;
}

/** Format a cost value as "per second" — e.g., "$0.000500" */
export function formatCostPerSecond(cost?: number): string {
  if (!cost) return '-';
  return `$${cost.toFixed(6)}`;
}

/** Format a cost value as "per request" — e.g., "$0.000100" */
export function formatCostPerRequest(cost?: number): string {
  if (!cost) return '-';
  return `$${cost.toFixed(6)}`;
}

/** Format a ModelType enum value as a display string */
export function formatModelType(type: ModelType): string {
  switch (type) {
    case ModelType.Chat:
      return 'Chat';
    case ModelType.Embedding:
      return 'Embedding';
    case ModelType.Image:
      return 'Image';
    case ModelType.Video:
      return 'Video';
    case ModelType.Audio:
      return 'Audio';
    default:
      return type;
  }
}

/** Format a priority number as a human-readable label */
export function formatPriority(priority: number): string {
  if (priority === 0) return 'Default';
  if (priority > 0) return `High (${priority})`;
  return `Low (${Math.abs(priority)})`;
}

/** Format an ISO date string for simple display */
export function formatDateString(dateString: string): string {
  return new Date(dateString).toLocaleDateString();
}

/** Annotate a model pattern with "(Pattern)" when it contains wildcards */
export function formatModelPattern(pattern: string): string {
  if (pattern.includes('*')) {
    return `${pattern} (Pattern)`;
  }
  return pattern;
}

/**
 * Get a context-aware cost display string for a model cost entry.
 * Chat models show "input / output", embeddings show a single value,
 * images show per-image cost, videos show per-second cost.
 */
export function getCostDisplayForModelType(cost: CostDisplayFields): string {
  switch (cost.modelType) {
    case ModelType.Chat:
      if (cost.inputCostPerMillionTokens !== undefined && cost.outputCostPerMillionTokens !== undefined) {
        return `${formatCostPerMillionTokens(cost.inputCostPerMillionTokens)} / ${formatCostPerMillionTokens(cost.outputCostPerMillionTokens)}`;
      }
      return '-';
    case ModelType.Embedding:
      if (cost.embeddingCostPerMillionTokens !== undefined) {
        return formatCostPerMillionTokens(cost.embeddingCostPerMillionTokens);
      }
      return '-';
    case ModelType.Image:
      return formatCostPerImage(cost.imageCostPerImage);
    case ModelType.Video:
      return formatCostPerSecond(cost.videoCostPerSecond);
    default:
      return '-';
  }
}

/**
 * Get the appropriate label describing the cost unit for a given model type.
 */
export function getCostTypeLabel(modelType: ModelType): string {
  switch (modelType) {
    case ModelType.Chat:
      return 'Input / Output (per million tokens)';
    case ModelType.Embedding:
      return 'Per million tokens';
    case ModelType.Image:
      return 'Per image';
    case ModelType.Video:
      return 'Per second';
    case ModelType.Audio:
      return 'Per minute';
    default:
      return 'Cost';
  }
}
