import { lazy } from 'react';
import { withSuspense } from '@/lib/utils/lazyLoad';
import { Skeleton } from '@mantine/core';

// Create skeleton loaders for charts
const ChartSkeleton = () => (
  <Skeleton height={300} radius="md" />
);

// Lazy load individual chart components
const LineChartLazy = lazy(() => import('recharts').then(mod => ({ default: mod.LineChart })));
export const LazyLineChart = withSuspense(LineChartLazy, <ChartSkeleton />);

const AreaChartLazy = lazy(() => import('recharts').then(mod => ({ default: mod.AreaChart })));
export const LazyAreaChart = withSuspense(AreaChartLazy, <ChartSkeleton />);

const BarChartLazy = lazy(() => import('recharts').then(mod => ({ default: mod.BarChart })));
export const LazyBarChart = withSuspense(BarChartLazy, <ChartSkeleton />);

const PieChartLazy = lazy(() => import('recharts').then(mod => ({ default: mod.PieChart })));
export const LazyPieChart = withSuspense(PieChartLazy, <ChartSkeleton />);

// Direct re-exports for chart components
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