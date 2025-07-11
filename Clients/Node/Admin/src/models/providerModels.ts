export interface ModelDto {
  id: string;
  name: string;
  displayName: string;
  provider: string;
  description?: string;
  contextWindow: number;
  maxTokens: number;
  inputCost: number;  // Cost per 1K tokens
  outputCost: number; // Cost per 1K tokens
  capabilities: ModelCapabilities;
  status: 'active' | 'deprecated' | 'beta' | 'preview';
  releaseDate?: string;
  deprecationDate?: string;
}

export interface ModelDetailsDto extends ModelDto {
  version: string;
  trainingData?: string;
  benchmarks?: Record<string, number>;
  limitations?: string[];
  bestPractices?: string[];
  examples?: ModelExample[];
}

export interface ModelExample {
  title: string;
  description: string;
  input: string;
  output: string;
}

export interface ModelSearchFilters {
  providers?: string[];
  capabilities?: Partial<ModelCapabilities>;
  status?: ('active' | 'deprecated' | 'beta' | 'preview')[];
  minContextWindow?: number;
  maxCost?: number;
}

export interface ModelSearchResult {
  models: ModelDto[];
  totalCount: number;
  facets: {
    providers: Record<string, number>;
    capabilities: Record<keyof ModelCapabilities, number>;
    status: Record<string, number>;
  };
}

// ModelCapabilities is shared with ModelMappingsService
export interface ModelCapabilities {
  chat: boolean;
  completion: boolean;
  embedding: boolean;
  vision: boolean;
  functionCalling: boolean;
  streaming: boolean;
  fineTuning: boolean;
  plugins: boolean;
}

// List response for paginated results
export interface ModelListResponseDto {
  items: ModelDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// Request/Response for model refresh
export interface RefreshModelsRequest {
  providerName: string;
  forceRefresh?: boolean;
}

export interface RefreshModelsResponse {
  providerName: string;
  modelsUpdated: number;
  modelsAdded: number;
  modelsRemoved: number;
  refreshedAt: string;
}