import { lazyLoadPage } from '@/lib/utils/lazyLoad';

const MetricsDashboard = lazyLoadPage(
  () => import('./MetricsDashboard'),
  { 
    loadingMessage: 'Loading metrics dashboard...',
    moduleName: 'Metrics Dashboard'
  }
);

export default MetricsDashboard;