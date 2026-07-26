export interface StatCard {
  title: string;
  value: number;
  description: string;
  icon: React.ComponentType<{ size?: number }>;
  color: string;
}