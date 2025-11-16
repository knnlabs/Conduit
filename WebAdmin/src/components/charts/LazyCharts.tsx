import { lazy, Suspense } from 'react';
import { Skeleton } from '@mantine/core';

// Create skeleton loaders for charts
const ChartSkeleton = () => (
  <Skeleton height={300} radius="md" />
);

// Lazy load individual chart components
const LineChartLazy = lazy(() => import('recharts').then(mod => ({ default: mod.LineChart })));
export const LazyLineChart = (props: React.ComponentProps<typeof LineChartLazy>) => (
  <Suspense fallback={<ChartSkeleton />}>
    <LineChartLazy {...props} />
  </Suspense>
);

const AreaChartLazy = lazy(() => import('recharts').then(mod => ({ default: mod.AreaChart })));
export const LazyAreaChart = (props: React.ComponentProps<typeof AreaChartLazy>) => (
  <Suspense fallback={<ChartSkeleton />}>
    <AreaChartLazy {...props} />
  </Suspense>
);

const BarChartLazy = lazy(() => import('recharts').then(mod => ({ default: mod.BarChart })));
export const LazyBarChart = (props: React.ComponentProps<typeof BarChartLazy>) => (
  <Suspense fallback={<ChartSkeleton />}>
    <BarChartLazy {...props} />
  </Suspense>
);

const PieChartLazy = lazy(() => import('recharts').then(mod => ({ default: mod.PieChart })));
export const LazyPieChart = (props: React.ComponentProps<typeof PieChartLazy>) => (
  <Suspense fallback={<ChartSkeleton />}>
    <PieChartLazy {...props} />
  </Suspense>
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