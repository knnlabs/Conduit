<template>
  <div class="metrics-dashboard">
    <div class="dashboard-header">
      <h1>Real-Time Metrics Dashboard</h1>
      <div class="dashboard-controls">
        <select v-model="updateInterval" @change="onIntervalChange" class="interval-selector">
          <option :value="1">1 second</option>
          <option :value="5">5 seconds</option>
          <option :value="10">10 seconds</option>
          <option :value="30">30 seconds</option>
          <option :value="60">1 minute</option>
        </select>
        <button @click="toggleConnection" class="connection-toggle" :class="{ connected: isConnected }">
          <i :class="isConnected ? 'fas fa-link' : 'fas fa-unlink'"></i>
          {{ isConnected ? 'Connected' : 'Disconnected' }}
        </button>
      </div>
    </div>

    <div v-if="connectionError" class="connection-error">
      <i class="fas fa-exclamation-triangle"></i>
      {{ connectionError }}
    </div>

    <!-- System Overview -->
    <div class="metrics-section">
      <h2>System Overview</h2>
      <div class="metrics-grid overview-grid">
        <MetricCard
          title="Requests/sec"
          :value="currentMetrics?.http?.requestsPerSecond || 0"
          :format="formatRate"
          icon="fas fa-tachometer-alt"
          :trend="getTrend('requestsPerSecond')"
        />
        <MetricCard
          title="Active Requests"
          :value="currentMetrics?.http?.activeRequests || 0"
          icon="fas fa-sync"
          :alertLevel="getActiveRequestsAlert()"
        />
        <MetricCard
          title="Error Rate"
          :value="currentMetrics?.http?.errorRate || 0"
          :format="formatPercent"
          icon="fas fa-exclamation-circle"
          :alertLevel="getErrorRateAlert()"
        />
        <MetricCard
          title="P95 Response Time"
          :value="currentMetrics?.http?.responseTimes?.p95 || 0"
          :format="formatMilliseconds"
          icon="fas fa-clock"
          :alertLevel="getResponseTimeAlert()"
        />
      </div>
    </div>

    <!-- Request Distribution -->
    <div class="metrics-section">
      <h2>Request Distribution</h2>
      <div class="metrics-grid">
        <div class="chart-container">
          <EndpointRequestChart :data="endpointRequestData" />
        </div>
        <div class="chart-container">
          <StatusCodeChart :data="statusCodeData" />
        </div>
      </div>
    </div>

    <!-- Infrastructure Health -->
    <div class="metrics-section">
      <h2>Infrastructure Health</h2>
      <div class="metrics-grid">
        <InfrastructureCard
          title="Database"
          :metrics="currentMetrics?.infrastructure?.database"
          icon="fas fa-database"
        />
        <InfrastructureCard
          title="Redis Cache"
          :metrics="currentMetrics?.infrastructure?.redis"
          icon="fas fa-memory"
        />
        <InfrastructureCard
          title="RabbitMQ"
          :metrics="currentMetrics?.infrastructure?.rabbitMQ"
          icon="fas fa-exchange-alt"
        />
        <InfrastructureCard
          title="SignalR"
          :metrics="currentMetrics?.infrastructure?.signalR"
          icon="fas fa-broadcast-tower"
        />
      </div>
    </div>

    <!-- Provider Health Matrix -->
    <div class="metrics-section">
      <h2>Provider Health</h2>
      <ProviderHealthMatrix :providers="currentMetrics?.providerHealth || []" />
    </div>

    <!-- Business Metrics -->
    <div class="metrics-section">
      <h2>Business Metrics</h2>
      <div class="metrics-grid">
        <MetricCard
          title="Active Virtual Keys"
          :value="currentMetrics?.business?.activeVirtualKeys || 0"
          icon="fas fa-key"
        />
        <MetricCard
          title="Cost/minute"
          :value="currentMetrics?.business?.costs?.totalCostPerMinute || 0"
          :format="formatCurrency"
          icon="fas fa-dollar-sign"
          :trend="getTrend('costPerMinute')"
        />
        <MetricCard
          title="Total Requests/min"
          :value="currentMetrics?.business?.totalRequestsPerMinute || 0"
          icon="fas fa-chart-line"
        />
        <MetricCard
          title="Avg Cost/Request"
          :value="currentMetrics?.business?.costs?.averageCostPerRequest || 0"
          :format="formatCurrency"
          icon="fas fa-receipt"
        />
      </div>
    </div>

    <!-- Top Virtual Keys -->
    <div class="metrics-section">
      <h2>Top Virtual Keys</h2>
      <VirtualKeyTable :virtualKeys="currentMetrics?.business?.topVirtualKeys || []" />
    </div>

    <!-- Model Usage -->
    <div class="metrics-section">
      <h2>Model Usage</h2>
      <ModelUsageChart :modelUsage="currentMetrics?.business?.modelUsage || []" />
    </div>

    <!-- System Resources -->
    <div class="metrics-section">
      <h2>System Resources</h2>
      <div class="metrics-grid">
        <SystemResourceGauge
          title="CPU Usage"
          :value="currentMetrics?.system?.cpuUsagePercent || 0"
          :max="100"
          unit="%"
          :thresholds="[60, 80]"
          icon="fas fa-microchip"
        />
        <SystemResourceGauge
          title="Memory Usage"
          :value="currentMetrics?.system?.memoryUsageMB || 0"
          :max="getMaxMemory()"
          unit="MB"
          :thresholds="[getMaxMemory() * 0.7, getMaxMemory() * 0.85]"
          icon="fas fa-memory"
        />
        <MetricCard
          title="Threads"
          :value="currentMetrics?.system?.threadCount || 0"
          icon="fas fa-tasks"
        />
        <MetricCard
          title="Uptime"
          :value="currentMetrics?.system?.uptime || '00:00:00'"
          :format="formatUptime"
          icon="fas fa-clock"
        />
      </div>
    </div>

    <!-- Active Alerts -->
    <div v-if="activeAlerts.length > 0" class="metrics-section alerts-section">
      <h2>Active Alerts</h2>
      <AlertsList :alerts="activeAlerts" @dismiss="dismissAlert" />
    </div>

    <!-- Historical Trends -->
    <div class="metrics-section">
      <h2>Historical Trends</h2>
      <div class="trend-controls">
        <button 
          v-for="range in timeRanges" 
          :key="range.value"
          @click="selectedTimeRange = range.value"
          :class="{ active: selectedTimeRange === range.value }"
          class="time-range-btn"
        >
          {{ range.label }}
        </button>
      </div>
      <div class="metrics-grid">
        <TrendChart
          title="Request Rate"
          :data="historicalData.requestRate"
          :timeRange="selectedTimeRange"
          color="#3498db"
        />
        <TrendChart
          title="Error Rate"
          :data="historicalData.errorRate"
          :timeRange="selectedTimeRange"
          color="#e74c3c"
          :format="formatPercent"
        />
        <TrendChart
          title="Response Time (P95)"
          :data="historicalData.responseTime"
          :timeRange="selectedTimeRange"
          color="#f39c12"
          :format="formatMilliseconds"
        />
        <TrendChart
          title="Cost Rate"
          :data="historicalData.costRate"
          :timeRange="selectedTimeRange"
          color="#27ae60"
          :format="formatCurrency"
        />
      </div>
    </div>
  </div>
</template>

<script>
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useMetricsStore } from '@/stores/metrics'
import { useToast } from '@/composables/useToast'
import MetricCard from '@/components/metrics/MetricCard.vue'
import InfrastructureCard from '@/components/metrics/InfrastructureCard.vue'
import ProviderHealthMatrix from '@/components/metrics/ProviderHealthMatrix.vue'
import VirtualKeyTable from '@/components/metrics/VirtualKeyTable.vue'
import ModelUsageChart from '@/components/metrics/ModelUsageChart.vue'
import SystemResourceGauge from '@/components/metrics/SystemResourceGauge.vue'
import AlertsList from '@/components/metrics/AlertsList.vue'
import TrendChart from '@/components/metrics/TrendChart.vue'
import EndpointRequestChart from '@/components/metrics/EndpointRequestChart.vue'
import StatusCodeChart from '@/components/metrics/StatusCodeChart.vue'

export default {
  name: 'MetricsDashboard',
  components: {
    MetricCard,
    InfrastructureCard,
    ProviderHealthMatrix,
    VirtualKeyTable,
    ModelUsageChart,
    SystemResourceGauge,
    AlertsList,
    TrendChart,
    EndpointRequestChart,
    StatusCodeChart
  },
  setup() {
    const metricsStore = useMetricsStore()
    const { showToast } = useToast()
    
    const updateInterval = ref(5)
    const selectedTimeRange = ref('1h')
    const activeAlerts = ref([])
    
    const timeRanges = [
      { label: '15m', value: '15m' },
      { label: '1h', value: '1h' },
      { label: '6h', value: '6h' },
      { label: '24h', value: '24h' }
    ]
    
    const isConnected = computed(() => metricsStore.isConnected)
    const connectionError = computed(() => metricsStore.connectionError)
    const currentMetrics = computed(() => metricsStore.currentSnapshot)
    const historicalData = computed(() => metricsStore.historicalData)
    
    const endpointRequestData = computed(() => {
      const endpoints = currentMetrics.value?.http?.endpointRequestRates || {}
      return Object.entries(endpoints).map(([endpoint, rate]) => ({
        endpoint,
        rate
      }))
    })
    
    const statusCodeData = computed(() => {
      const codes = currentMetrics.value?.http?.statusCodeCounts || {}
      return Object.entries(codes).map(([code, count]) => ({
        code: parseInt(code),
        count
      }))
    })
    
    // Alert level calculations
    const getErrorRateAlert = () => {
      const rate = currentMetrics.value?.http?.errorRate || 0
      if (rate > 5) return 'critical'
      if (rate > 2) return 'warning'
      return null
    }
    
    const getResponseTimeAlert = () => {
      const time = currentMetrics.value?.http?.responseTimes?.p95 || 0
      if (time > 5000) return 'critical'
      if (time > 2000) return 'warning'
      return null
    }
    
    const getActiveRequestsAlert = () => {
      const count = currentMetrics.value?.http?.activeRequests || 0
      if (count > 1000) return 'warning'
      return null
    }
    
    // Formatting functions
    const formatRate = (value) => `${value.toFixed(1)}/s`
    const formatPercent = (value) => `${value.toFixed(1)}%`
    const formatMilliseconds = (value) => `${value.toFixed(0)}ms`
    const formatCurrency = (value) => `$${value.toFixed(2)}`
    const formatUptime = (value) => {
      if (typeof value === 'string') return value
      // Convert TimeSpan to readable format
      const days = Math.floor(value.totalDays || 0)
      const hours = Math.floor(value.hours || 0)
      const minutes = Math.floor(value.minutes || 0)
      if (days > 0) return `${days}d ${hours}h ${minutes}m`
      if (hours > 0) return `${hours}h ${minutes}m`
      return `${minutes}m`
    }
    
    const getMaxMemory = () => {
      // Estimate max memory based on current usage
      const current = currentMetrics.value?.system?.memoryUsageMB || 0
      return Math.max(4096, Math.ceil(current * 1.5 / 1024) * 1024)
    }
    
    const getTrend = (metric) => {
      // Calculate trend from historical data
      const data = historicalData.value[metric] || []
      if (data.length < 2) return null
      
      const recent = data.slice(-10)
      const older = data.slice(-20, -10)
      
      if (recent.length === 0 || older.length === 0) return null
      
      const recentAvg = recent.reduce((sum, d) => sum + d.value, 0) / recent.length
      const olderAvg = older.reduce((sum, d) => sum + d.value, 0) / older.length
      
      const change = ((recentAvg - olderAvg) / olderAvg) * 100
      
      return {
        direction: change > 0 ? 'up' : 'down',
        percentage: Math.abs(change)
      }
    }
    
    const onIntervalChange = () => {
      metricsStore.updateInterval(updateInterval.value)
    }
    
    const toggleConnection = async () => {
      if (isConnected.value) {
        await metricsStore.disconnect()
      } else {
        await metricsStore.connect()
      }
    }
    
    const dismissAlert = (alertId) => {
      activeAlerts.value = activeAlerts.value.filter(a => a.id !== alertId)
    }
    
    // Watch for new alerts
    watch(() => metricsStore.alerts, (newAlerts) => {
      activeAlerts.value = newAlerts
      
      // Show toast for critical alerts
      newAlerts.filter(a => a.severity === 'critical').forEach(alert => {
        showToast(alert.message, 'error')
      })
    })
    
    onMounted(async () => {
      await metricsStore.connect()
      metricsStore.updateInterval(updateInterval.value)
      
      // Load initial historical data
      await metricsStore.loadHistoricalData(selectedTimeRange.value)
    })
    
    onUnmounted(() => {
      metricsStore.disconnect()
    })
    
    return {
      updateInterval,
      selectedTimeRange,
      timeRanges,
      isConnected,
      connectionError,
      currentMetrics,
      historicalData,
      activeAlerts,
      endpointRequestData,
      statusCodeData,
      getErrorRateAlert,
      getResponseTimeAlert,
      getActiveRequestsAlert,
      formatRate,
      formatPercent,
      formatMilliseconds,
      formatCurrency,
      formatUptime,
      getMaxMemory,
      getTrend,
      onIntervalChange,
      toggleConnection,
      dismissAlert
    }
  }
}
</script>

<style scoped>
.metrics-dashboard {
  padding: 20px;
  background-color: var(--bg-secondary);
  min-height: 100vh;
}

.dashboard-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 30px;
}

.dashboard-header h1 {
  margin: 0;
  color: var(--text-primary);
}

.dashboard-controls {
  display: flex;
  gap: 15px;
  align-items: center;
}

.interval-selector {
  padding: 8px 12px;
  border: 1px solid var(--border-color);
  border-radius: 4px;
  background-color: var(--bg-primary);
  color: var(--text-primary);
}

.connection-toggle {
  padding: 8px 16px;
  border: 1px solid var(--border-color);
  border-radius: 4px;
  background-color: var(--bg-primary);
  color: var(--text-primary);
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 8px;
  transition: all 0.3s;
}

.connection-toggle.connected {
  border-color: var(--success-color);
  color: var(--success-color);
}

.connection-toggle:hover {
  background-color: var(--bg-hover);
}

.connection-error {
  background-color: var(--error-bg);
  color: var(--error-color);
  padding: 12px 20px;
  border-radius: 4px;
  margin-bottom: 20px;
  display: flex;
  align-items: center;
  gap: 10px;
}

.metrics-section {
  margin-bottom: 40px;
}

.metrics-section h2 {
  margin: 0 0 20px 0;
  color: var(--text-primary);
  font-size: 1.4em;
}

.metrics-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 20px;
}

.overview-grid {
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
}

.chart-container {
  background-color: var(--bg-primary);
  border-radius: 8px;
  padding: 20px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.alerts-section {
  background-color: var(--bg-primary);
  border-radius: 8px;
  padding: 20px;
  border: 1px solid var(--warning-color);
}

.trend-controls {
  display: flex;
  gap: 10px;
  margin-bottom: 20px;
}

.time-range-btn {
  padding: 6px 16px;
  border: 1px solid var(--border-color);
  background-color: var(--bg-primary);
  color: var(--text-secondary);
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.3s;
}

.time-range-btn.active {
  background-color: var(--primary-color);
  color: white;
  border-color: var(--primary-color);
}

.time-range-btn:hover:not(.active) {
  background-color: var(--bg-hover);
}

/* Dark mode adjustments */
@media (prefers-color-scheme: dark) {
  .metrics-dashboard {
    --bg-primary: #1e1e1e;
    --bg-secondary: #121212;
    --bg-hover: #2a2a2a;
    --text-primary: #ffffff;
    --text-secondary: #b0b0b0;
    --border-color: #333333;
    --success-color: #4caf50;
    --warning-color: #ff9800;
    --error-color: #f44336;
    --error-bg: rgba(244, 67, 54, 0.1);
    --primary-color: #2196f3;
  }
}

/* Light mode */
@media (prefers-color-scheme: light) {
  .metrics-dashboard {
    --bg-primary: #ffffff;
    --bg-secondary: #f5f5f5;
    --bg-hover: #f0f0f0;
    --text-primary: #212121;
    --text-secondary: #666666;
    --border-color: #e0e0e0;
    --success-color: #4caf50;
    --warning-color: #ff9800;
    --error-color: #f44336;
    --error-bg: rgba(244, 67, 54, 0.05);
    --primary-color: #2196f3;
  }
}
</style>