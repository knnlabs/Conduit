import dynamic from 'next/dynamic';

const CostDashboard = dynamic(() => import('./CostDashboard'), {
  loading: () => <div>Loading cost dashboard...</div>,
});

export default CostDashboard;