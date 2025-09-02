// Shared types for model-related components

export interface ProviderTypeAssociation {
  id: number;
  identifier: string;
  provider: string;
  isPrimary: boolean;
  modelCostId?: number | null;
  maxInputTokens?: number | null;
  maxOutputTokens?: number | null;
  speedScore?: number | null;
  qualityScore?: number | null;
  providerVariation?: string | null;
}

// For creating/editing associations where id might not exist yet
export interface ProviderTypeAssociationInput extends Omit<ProviderTypeAssociation, 'id'> {
  id?: number;
}