<template>
  <div class="infrastructure-card" :class="{ 'unhealthy': !isHealthy }">
    <div class="card-header">
      <i :class="icon" class="service-icon"></i>
      <h3>{{ title }}</h3>
      <span class="status-badge" :class="statusClass">
        {{ statusText }}
      </span>
    </div>
    
    <div class="metrics-list">
      <div v-if="metrics?.activeConnections !== undefined" class="metric-item">
        <span class="metric-label">Active Connections</span>
        <span class="metric-value">{{ metrics.activeConnections }}</span>
      </div>
      
      <div v-if="metrics?.poolUtilization !== undefined" class="metric-item">
        <span class="metric-label">Pool Utilization</span>
        <span class="metric-value">{{ metrics.poolUtilization.toFixed(1) }}%</span>
      </div>
      
      <div v-if="metrics?.memoryUsageMB !== undefined" class="metric-item">
        <span class="metric-label">Memory Usage</span>
        <span class="metric-value">{{ metrics.memoryUsageMB.toFixed(1) }} MB</span>
      </div>
      
      <div v-if="metrics?.hitRate !== undefined" class="metric-item">
        <span class="metric-label">Hit Rate</span>
        <span class="metric-value">{{ metrics.hitRate.toFixed(1) }}%</span>
      </div>
      
      <div v-if="metrics?.queueDepths" class="metric-item">
        <span class="metric-label">Queue Depths</span>
        <div class="queue-list">
          <div v-for="(depth, queue) in metrics.queueDepths" :key="queue" class="queue-item">
            <span class="queue-name">{{ queue }}</span>
            <span class="queue-depth" :class="{ 'high': depth > 100 }">{{ depth }}</span>
          </div>
        </div>
      </div>
      
      <div v-if="metrics?.averageLatency !== undefined" class="metric-item">
        <span class="metric-label">Avg Latency</span>
        <span class="metric-value">{{ metrics.averageLatency.toFixed(1) }} ms</span>
      </div>
      
      <div v-if="metrics?.errorsPerMinute !== undefined" class="metric-item">
        <span class="metric-label">Errors/min</span>
        <span class="metric-value" :class="{ 'error': metrics.errorsPerMinute > 0 }">
          {{ metrics.errorsPerMinute }}
        </span>
      </div>
    </div>
  </div>
</template>

<script>
import { computed } from 'vue'

export default {
  name: 'InfrastructureCard',
  props: {
    title: {
      type: String,
      required: true
    },
    metrics: {
      type: Object,
      default: () => ({})
    },
    icon: {
      type: String,
      default: 'fas fa-server'
    }
  },
  setup(props) {
    const isHealthy = computed(() => {
      if (!props.metrics) return false
      
      // Check various health indicators
      if (props.metrics.healthStatus === 'unhealthy') return false
      if (props.metrics.isConnected === false) return false
      if (props.metrics.errorsPerMinute > 10) return false
      if (props.metrics.poolUtilization > 90) return false
      
      return true
    })
    
    const statusClass = computed(() => {
      if (!props.metrics) return 'unknown'
      
      if (props.metrics.isConnected === false) return 'disconnected'
      if (props.metrics.healthStatus === 'unhealthy') return 'unhealthy'
      if (props.metrics.healthStatus === 'degraded') return 'degraded'
      if (props.metrics.errorsPerMinute > 0) return 'degraded'
      
      return 'healthy'
    })
    
    const statusText = computed(() => {
      if (!props.metrics) return 'Unknown'
      
      if (props.metrics.isConnected === false) return 'Disconnected'
      if (props.metrics.healthStatus) return props.metrics.healthStatus
      
      return isHealthy.value ? 'Healthy' : 'Unhealthy'
    })
    
    return {
      isHealthy,
      statusClass,
      statusText
    }
  }
}
</script>

<style scoped>
.infrastructure-card {
  background-color: var(--card-bg);
  border: 1px solid var(--card-border);
  border-radius: 8px;
  padding: 20px;
  transition: all 0.3s ease;
}

.infrastructure-card.unhealthy {
  border-color: var(--error-color);
  background-color: var(--error-bg);
}

.card-header {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 20px;
}

.service-icon {
  font-size: 1.5em;
  color: var(--icon-color);
}

.card-header h3 {
  margin: 0;
  flex: 1;
  font-size: 1.1em;
  color: var(--text-primary);
}

.status-badge {
  padding: 4px 12px;
  border-radius: 12px;
  font-size: 0.85em;
  font-weight: 500;
  text-transform: capitalize;
}

.status-badge.healthy {
  background-color: var(--success-bg);
  color: var(--success-color);
}

.status-badge.degraded {
  background-color: var(--warning-bg);
  color: var(--warning-color);
}

.status-badge.unhealthy,
.status-badge.disconnected {
  background-color: var(--error-bg);
  color: var(--error-color);
}

.status-badge.unknown {
  background-color: var(--unknown-bg);
  color: var(--text-secondary);
}

.metrics-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.metric-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 0;
  border-bottom: 1px solid var(--border-color);
}

.metric-item:last-child {
  border-bottom: none;
}

.metric-label {
  font-size: 0.9em;
  color: var(--text-secondary);
}

.metric-value {
  font-weight: 600;
  color: var(--text-primary);
}

.metric-value.error {
  color: var(--error-color);
}

.queue-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 0.85em;
}

.queue-item {
  display: flex;
  justify-content: space-between;
  padding-left: 10px;
}

.queue-name {
  color: var(--text-secondary);
}

.queue-depth {
  font-weight: 500;
}

.queue-depth.high {
  color: var(--warning-color);
}

/* Theme Variables */
.infrastructure-card {
  --card-bg: var(--bg-primary, #ffffff);
  --card-border: var(--border-color, #e0e0e0);
  --icon-color: var(--primary-color, #2196f3);
  --success-color: #4caf50;
  --success-bg: rgba(76, 175, 80, 0.1);
  --warning-color: #ff9800;
  --warning-bg: rgba(255, 152, 0, 0.1);
  --error-color: #f44336;
  --error-bg: rgba(244, 67, 54, 0.05);
  --unknown-bg: rgba(0, 0, 0, 0.05);
}

/* Dark mode */
@media (prefers-color-scheme: dark) {
  .infrastructure-card {
    --card-bg: #1e1e1e;
    --card-border: #333333;
    --error-bg: rgba(244, 67, 54, 0.1);
    --unknown-bg: rgba(255, 255, 255, 0.05);
  }
}
</style>