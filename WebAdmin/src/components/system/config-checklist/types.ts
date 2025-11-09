import type {
  ProviderDto,
  ProviderKeyCredentialDto,
  ModelProviderMappingDto,
  ModelCostDto,
  GlobalSettingDto
} from '@knn_labs/conduit-admin-client';

export interface CheckResult {
  id: string;
  title: string;
  status: 'success' | 'error' | 'warning';
  message: string;
  details?: string[];
  icon: React.ComponentType<{ size?: number }>;
}

export interface ConfigData {
  providers: ProviderDto[];
  allProviderKeys: ProviderKeyCredentialDto[];
  modelMappings: ModelProviderMappingDto[];
  modelCosts: ModelCostDto[];
  settings: GlobalSettingDto[];
}

// Cost thresholds based on GPT-OSS 120B pricing
export const COST_THRESHOLDS = {
  INPUT_FLOOR: 0.15,  // $0.15 per 1M tokens
  OUTPUT_FLOOR: 0.60  // $0.60 per 1M tokens
};