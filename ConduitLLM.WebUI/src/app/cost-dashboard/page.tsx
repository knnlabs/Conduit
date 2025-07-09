import { lazyLoadPage } from '@/lib/utils/lazyLoad';

const CostDashboard = lazyLoadPage(
  () => import('./CostDashboard'),
  { loadingMessage: 'Loading cost dashboard...' }
);

export default CostDashboard;