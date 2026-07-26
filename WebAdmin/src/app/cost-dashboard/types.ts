// Define types locally since SDK exports may not be fully available
export interface DetailedCostDataDto {
  name: string;
  cost: number;
  percentage: number;
  requestCount: number;
}

export interface CostTrendDataDto {
  date: string;
  cost: number;
  requestCount: number;
}

export interface CostDashboardDto {
  timeFrame: string;
  startDate: string;
  endDate: string;
  last24HoursCost: number;
  last7DaysCost: number;
  last30DaysCost: number;
  totalCost: number;
  topModelsBySpend: DetailedCostDataDto[];
  topProvidersBySpend: DetailedCostDataDto[];
  topVirtualKeysBySpend: DetailedCostDataDto[];
}

export interface CostTrendDto {
  period: string;
  startDate: string;
  endDate: string;
  data: CostTrendDataDto[];
}

// Local types for transformed data. Fields the analytics summary does not
// measure (per-model provider/token counts, per-provider trend, per-day
// provider splits) are deliberately absent — do not re-add them with
// placeholder values.
export interface ProviderCost {
  provider: string;
  cost: number;
  usage: number;
}

export interface ModelUsage {
  model: string;
  requests: number;
  cost: number;
}

export interface DailyCost {
  date: string;
  cost: number;
}

export interface DateRange {
  startDate: string;
  endDate: string;
}