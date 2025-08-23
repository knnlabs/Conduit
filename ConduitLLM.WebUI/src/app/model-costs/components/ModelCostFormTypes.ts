import { ModelType } from '@knn_labs/conduit-admin-client';

export interface FormValues {
  costName: string;
  modelProviderMappingIds: number[];
  modelType: ModelType;
  // Token-based costs (per million tokens)
  inputCostPerMillion: number;
  outputCostPerMillion: number;
  cachedInputCostPerMillion: number;
  cachedInputWriteCostPerMillion: number;
  embeddingCostPerMillion: number;
  // Other cost types
  searchUnitCostPer1K: number;
  inferenceStepCost: number;
  defaultInferenceSteps: number;
  imageCostPerImage: number;
  audioCostPerMinute: number;
  audioCostPerKCharacters: number;
  audioInputCostPerMinute: number;
  audioOutputCostPerMinute: number;
  videoCostPerSecond: number;
  videoResolutionMultipliers: string;
  // Batch processing
  supportsBatchProcessing: boolean;
  batchProcessingMultiplier: number;
  // Image quality
  imageQualityMultipliers: string;
  // Metadata
  priority: number;
  description: string;
  isActive: boolean;
}

export const getFormValidation = () => ({
  costName: (value: string) => !value?.trim() ? 'Cost name is required' : null,
  modelProviderMappingIds: (value: number[]) => !value || value.length === 0 ? 'At least one model must be selected' : null,
  priority: (value: number) => value < 0 ? 'Priority must be non-negative' : null,
  inputCostPerMillion: (value: number) => value < 0 ? 'Cost must be non-negative' : null,
  outputCostPerMillion: (value: number) => value < 0 ? 'Cost must be non-negative' : null,
  cachedInputCostPerMillion: (value: number) => value < 0 ? 'Cost must be non-negative' : null,
  cachedInputWriteCostPerMillion: (value: number) => value < 0 ? 'Cost must be non-negative' : null,
  embeddingCostPerMillion: (value: number) => value < 0 ? 'Cost must be non-negative' : null,
  searchUnitCostPer1K: (value: number) => value < 0 ? 'Cost must be non-negative' : null,
  inferenceStepCost: (value: number) => value < 0 ? 'Cost must be non-negative' : null,
  defaultInferenceSteps: (value: number) => value < 0 ? 'Steps must be non-negative' : null,
  imageCostPerImage: (value: number) => value < 0 ? 'Cost must be non-negative' : null,
  audioCostPerMinute: (value: number) => value < 0 ? 'Cost must be non-negative' : null,
  videoCostPerSecond: (value: number) => value < 0 ? 'Cost must be non-negative' : null,
  batchProcessingMultiplier: (value: number, values: FormValues) => {
    if (values.supportsBatchProcessing && value) {
      if (value <= 0 || value > 1) {
        return 'Multiplier must be between 0 and 1';
      }
    }
    return null;
  },
  imageQualityMultipliers: (value: string) => {
    if (!value || value === '{}') return null;
    try {
      const parsed = JSON.parse(value) as unknown;
      if (typeof parsed !== 'object' || Array.isArray(parsed)) {
        return 'Must be a JSON object';
      }
      const parsedObj = parsed as Record<string, unknown>;
      for (const [key, val] of Object.entries(parsedObj)) {
        if (typeof val !== 'number' || val <= 0) {
          return `Value for "${key}" must be a positive number`;
        }
      }
      return null;
    } catch {
      return 'Invalid JSON';
    }
  },
});