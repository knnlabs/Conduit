// Provider metadata sync (OpenRouter drift review) DTOs.

export interface DriftItemDto {
  id: number;
  modelProviderMappingId: number;
  modelAlias: string;
  providerId: number;
  providerName: string;
  openRouterModelId: string;
  /** Pricing | MissingCost | ContextWindow | Capabilities | ModelRemoved | ModelDeprecated */
  driftType: string;
  /** Pending | Applied | Dismissed | AutoResolved */
  status: string;
  /** Conduit's current values at detection time (JSON; shape depends on driftType). */
  currentValuesJson: string;
  /** The provider's proposed values (JSON; shape depends on driftType). */
  proposedValuesJson: string;
  firstDetectedAt: string;
  lastDetectedAt: string;
  resolvedAt?: string | null;
  resolvedBy?: string | null;
}

export interface ProviderSyncRunDto {
  id: number;
  providerType: string;
  startedAt: string;
  completedAt?: string | null;
  status: string;
  triggeredBy: string;
  modelsFetched: number;
  mappingsChecked: number;
  itemsCreated: number;
  itemsUpdated: number;
  itemsAutoResolved: number;
  errorMessage?: string | null;
}

export interface DriftActionResultDto {
  id: number;
  success: boolean;
  /** True when rejected because Conduit's current values changed since detection. */
  stale: boolean;
  error?: string | null;
}

export interface BulkDriftActionRequest {
  ids: number[];
}

export interface BulkDriftActionResponse {
  succeededCount: number;
  failedCount: number;
  results: DriftActionResultDto[];
}

export interface DriftItemFilter {
  status?: string;
  driftType?: string;
  providerId?: number;
  page?: number;
  pageSize?: number;
}
