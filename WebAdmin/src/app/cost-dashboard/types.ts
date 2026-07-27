// Cost DTOs come from the Admin SDK analytics service
export type {
  DetailedCostDataDto,
  CostTrendDataDto,
  CostDashboardDto,
  CostTrendDto,
} from '@/lib/admin-api';

// Local types for transformed data. Fields the analytics summary does not
// measure (per-model provider/token counts, per-provider trend, per-day
// provider splits) are deliberately absent — do not re-add them with
// placeholder values.
export interface ProviderCost {
  provider: string;
  cost: number;
  usage: number;
}

export interface ModelSpendUsage {
  model: string;
  requests: number;
  cost: number;
}

export interface DailyCost {
  date: string;
  cost: number;
}

export type { DateRange } from '@/lib/conduit-common';
