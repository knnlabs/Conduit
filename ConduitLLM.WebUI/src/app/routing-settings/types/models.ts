// Local model discovery types to avoid broken SDK imports

export interface DiscoveredModel {
  id: string;
  displayName: string;
  providerId: string;
  providerName?: string;
  maxContextTokens?: number;
  supportsVision?: boolean;
  supportsFunctionCalling?: boolean;
  supportsToolUsage?: boolean;
  supportsJsonMode?: boolean;
  supportsStreaming?: boolean;
  capabilities?: string[];
}

export interface ModelsDiscoveryResponse {
  models: DiscoveredModel[];
  totalCount: number;
  providers: Array<{
    id: string;
    name: string;
    type: string;
    modelCount: number;
  }>;
}