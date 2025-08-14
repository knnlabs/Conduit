import { ModelCost } from '../types/modelCost';
import { ModelType } from '@knn_labs/conduit-admin-client';

export const formatCostPerMillionTokens = (cost?: number): string => {
  if (!cost) return '-';
  return `$${cost.toFixed(2)}`;
};

export const formatCostPerThousandTokens = (cost?: number): string => {
  if (!cost) return '-';
  return `$${(cost / 1000).toFixed(3)}`;
};

export const formatCostPerImage = (cost?: number): string => {
  if (!cost) return '-';
  return `$${cost.toFixed(4)}`;
};

export const formatCostPerMinute = (cost?: number): string => {
  if (!cost) return '-';
  return `$${cost.toFixed(4)}`;
};

export const formatCostPerSecond = (cost?: number): string => {
  if (!cost) return '-';
  return `$${cost.toFixed(6)}`;
};

export const formatCostPerRequest = (cost?: number): string => {
  if (!cost) return '-';
  return `$${cost.toFixed(6)}`;
};

export const formatModelType = (type: ModelType): string => {
  switch (type) {
    case ModelType.Chat:
      return 'Chat';
    case ModelType.Embedding:
      return 'Embedding';
    case ModelType.Image:
      return 'Image';
    case ModelType.Audio:
      return 'Audio';
    case ModelType.Video:
      return 'Video';
    default:
      return type;
  }
};

export const formatPriority = (priority: number): string => {
  if (priority === 0) return 'Default';
  if (priority > 0) return `High (${priority})`;
  return `Low (${Math.abs(priority)})`;
};

export const formatDateString = (dateString: string): string => {
  return new Date(dateString).toLocaleDateString();
};

export const formatModelPattern = (pattern: string): string => {
  if (pattern.includes('*')) {
    return `${pattern} (Pattern)`;
  }
  return pattern;
};

export const getCostDisplayForModelType = (cost: ModelCost): string => {
  switch (cost.modelType) {
    case ModelType.Chat:
      if (cost.inputCostPerMillionTokens !== undefined && cost.outputCostPerMillionTokens !== undefined) {
        // Cost is already per million tokens
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
    case ModelType.Audio:
      return formatCostPerSecond(cost.audioCostPerMinute ? cost.audioCostPerMinute / 60 : undefined);
    case ModelType.Video:
      return formatCostPerSecond(cost.videoCostPerSecond);
    default:
      return '-';
  }
};

export const getCostTypeLabel = (modelType: ModelType): string => {
  switch (modelType) {
    case ModelType.Chat:
      return 'Input / Output (per million tokens)';
    case ModelType.Embedding:
      return 'Per million tokens';
    case ModelType.Image:
      return 'Per image';
    case ModelType.Audio:
      return 'Per second';
    case ModelType.Video:
      return 'Per second';
    default:
      return 'Cost';
  }
};