import { ModelCost } from '../types/modelCost';

export const formatCostPerMillionTokens = (cost?: number): string => {
  if (!cost) return '-';
  return `$${(cost / 1000).toFixed(3)}`;
};

export const formatCostPerThousandTokens = (cost?: number): string => {
  if (!cost) return '-';
  return `$${(cost / 1000000).toFixed(6)}`;
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

export const formatModelType = (type: string): string => {
  switch (type) {
    case 'chat':
      return 'Chat';
    case 'embedding':
      return 'Embedding';
    case 'image':
      return 'Image';
    case 'audio':
      return 'Audio';
    case 'video':
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
    case 'chat':
      if (cost.inputTokenCost !== undefined && cost.outputTokenCost !== undefined) {
        // Convert from per token to per million for display
        const inputPerMillion = cost.inputTokenCost * 1000000;
        const outputPerMillion = cost.outputTokenCost * 1000000;
        return `${formatCostPerMillionTokens(inputPerMillion)} / ${formatCostPerMillionTokens(outputPerMillion)}`;
      }
      return '-';
    case 'embedding':
      if (cost.embeddingTokenCost !== undefined) {
        const perMillion = cost.embeddingTokenCost * 1000000;
        return formatCostPerMillionTokens(perMillion);
      }
      return '-';
    case 'image':
      return formatCostPerImage(cost.imageCostPerImage);
    case 'audio':
      return formatCostPerSecond(cost.audioCostPerMinute ? cost.audioCostPerMinute / 60 : undefined);
    case 'video':
      return formatCostPerSecond(cost.videoCostPerSecond);
    default:
      return '-';
  }
};

export const getCostTypeLabel = (modelType: string): string => {
  switch (modelType) {
    case 'chat':
      return 'Input / Output (per 1K tokens)';
    case 'embedding':
      return 'Per 1K tokens';
    case 'image':
      return 'Per image';
    case 'audio':
      return 'Per second';
    case 'video':
      return 'Per second';
    default:
      return 'Cost';
  }
};