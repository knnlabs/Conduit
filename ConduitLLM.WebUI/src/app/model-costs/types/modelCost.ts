export interface ModelCost {
  id: number;
  modelIdPattern: string;
  providerName: string;
  modelType: 'chat' | 'embedding' | 'image' | 'audio' | 'video';
  inputCostPerMillionTokens?: number;
  outputCostPerMillionTokens?: number;
  costPerRequest?: number;
  costPerSecond?: number;
  costPerImage?: number;
  isActive: boolean;
  priority: number;
  effectiveDate: string;
  expiryDate?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateModelCostDto {
  modelIdPattern: string;
  inputTokenCost: number;
  outputTokenCost: number;
  embeddingTokenCost?: number;
  imageCostPerImage?: number;
  audioCostPerMinute?: number;
  audioCostPerKCharacters?: number;
  audioInputCostPerMinute?: number;
  audioOutputCostPerMinute?: number;
  videoCostPerSecond?: number;
  videoResolutionMultipliers?: string;
  description?: string;
  priority?: number;
}

export interface UpdateModelCostDto extends Partial<CreateModelCostDto> {
  id?: number;
}

export interface ModelCostListResponse {
  items: ModelCost[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ModelCostFilters {
  provider?: string;
  isActive?: boolean;
}