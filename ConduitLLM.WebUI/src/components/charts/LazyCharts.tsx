import { lazy } from 'react';
import { lazyLoadComponent } from '@/lib/utils/lazyLoad';
import { Skeleton } from '@mantine/core';

// Create skeleton loaders for charts
const ChartSkeleton = () => (
  <Skeleton height={300} radius="md" />
);

// Lazy load individual chart components
export const LazyLineChart = lazyLoadComponent(
  () => import('recharts').then(mod => ({ default: mod.LineChart })),
  <ChartSkeleton />
);

export const LazyAreaChart = lazyLoadComponent(
  () => import('recharts').then(mod => ({ default: mod.AreaChart })),
  <ChartSkeleton />
);

export const LazyBarChart = lazyLoadComponent(
  () => import('recharts').then(mod => ({ default: mod.BarChart })),
  <ChartSkeleton />
);

export const LazyPieChart = lazyLoadComponent(
  () => import('recharts').then(mod => ({ default: mod.PieChart })),
  <ChartSkeleton />
);

// Export other recharts components needed
export { 
  Line,
  Area,
  Bar,
  Pie,
  Cell,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip as RechartsTooltip,
  Legend as RechartsLegend,
  ResponsiveContainer,
  LabelList
} from 'recharts';