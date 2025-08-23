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

// Local types for transformed data
export interface ProviderCost {
  provider: string;
  cost: number;
  usage: number;
  trend: number;
}

export interface ModelUsage {
  model: string;
  provider: string;
  requests: number;
  tokensIn: number;
  tokensOut: number;
  cost: number;
}

export interface DailyCost {
  date: string;
  cost: number;
  [providerName: string]: string | number;
}

export interface DateRange {
  startDate: string;
  endDate: string;
}