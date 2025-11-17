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

export interface StatCard {
  title: string;
  value: number;
  description: string;
  icon: React.ComponentType<{ size?: number }>;
  color: string;
}