import { ModelType } from '@knn_labs/conduit-admin-client';
import { FormValues } from './types';

export const getInitialValues = (): FormValues => ({
  costName: '',
  modelProviderMappingIds: [],
  modelType: ModelType.Chat,
  inputCostPerMillion: 0,
  outputCostPerMillion: 0,
  cachedInputCostPerMillion: 0,
  cachedInputWriteCostPerMillion: 0,
  embeddingCostPerMillion: 0,
  searchUnitCostPer1K: 0,
  inferenceStepCost: 0,
  defaultInferenceSteps: 0,
  imageCostPerImage: 0,
  audioCostPerMinute: 0,
  audioCostPerKCharacters: 0,
  audioInputCostPerMinute: 0,
  audioOutputCostPerMinute: 0,
  videoCostPerSecond: 0,
  videoResolutionMultipliers: '',
  supportsBatchProcessing: false,
  batchProcessingMultiplier: 0.5,
  imageQualityMultipliers: '',
  priority: 0,
  description: '',
  isActive: true,
});

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