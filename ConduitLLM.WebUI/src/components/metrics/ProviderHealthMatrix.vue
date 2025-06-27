<template>
  <div class="provider-health-matrix">
    <table class="health-table">
      <thead>
        <tr>
          <th>Provider</th>
          <th>Status</th>
          <th>Models</th>
          <th>Error Rate</th>
          <th>Avg Latency</th>
          <th>Last Success</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="provider in sortedProviders" :key="provider.providerName" 
            :class="{ 'disabled': !provider.isEnabled }">
          <td class="provider-name">
            <i :class="getProviderIcon(provider.providerName)" class="provider-icon"></i>
            {{ provider.providerName }}
          </td>
          <td>
            <span class="status-indicator" :class="getStatusClass(provider)">
              <i :class="getStatusIcon(provider)"></i>
              {{ provider.status }}
            </span>
          </td>
          <td class="models-count">
            {{ provider.availableModels }}
          </td>
          <td class="error-rate" :class="{ 'high': provider.errorRate > 5 }">
            {{ provider.errorRate.toFixed(1) }}%
          </td>
          <td class="latency" :class="getLatencyClass(provider.averageLatency)">
            {{ formatLatency(provider.averageLatency) }}
          </td>
          <td class="last-success">
            {{ formatLastSuccess(provider.lastSuccessfulRequest) }}
          </td>
        </tr>
      </tbody>
    </table>
    
    <div v-if="providers.length === 0" class="no-data">
      <i class="fas fa-server"></i>
      <p>No provider data available</p>
    </div>
  </div>
</template>

<script>
import { computed } from 'vue'

export default {
  name: 'ProviderHealthMatrix',
  props: {
    providers: {
      type: Array,
      default: () => []
    }
  },
  setup(props) {
    const sortedProviders = computed(() => {
      return [...props.providers].sort((a, b) => {
        // Sort by health status first (healthy > degraded > unhealthy)
        const statusOrder = { 'healthy': 0, 'degraded': 1, 'unhealthy': 2 }
        const statusDiff = (statusOrder[a.status] || 3) - (statusOrder[b.status] || 3)
        if (statusDiff !== 0) return statusDiff
        
        // Then by error rate (lower is better)
        return a.errorRate - b.errorRate
      })
    })
    
    const getProviderIcon = (providerName) => {
      const iconMap = {
        'openai': 'fab fa-openai',
        'anthropic': 'fas fa-robot',
        'google': 'fab fa-google',
        'minimax': 'fas fa-cube',
        'replicate': 'fas fa-copy',
        'openrouter': 'fas fa-route',
        'groq': 'fas fa-bolt',
        'together': 'fas fa-users'
      }
      return iconMap[providerName.toLowerCase()] || 'fas fa-server'
    }
    
    const getStatusClass = (provider) => {
      if (!provider.isEnabled) return 'disabled'
      return provider.status.toLowerCase()
    }
    
    const getStatusIcon = (provider) => {
      if (!provider.isEnabled) return 'fas fa-ban'
      switch (provider.status.toLowerCase()) {
        case 'healthy':
          return 'fas fa-check-circle'
        case 'degraded':
          return 'fas fa-exclamation-triangle'
        case 'unhealthy':
          return 'fas fa-times-circle'
        default:
          return 'fas fa-question-circle'
      }
    }
    
    const getLatencyClass = (latency) => {
      if (latency > 2000) return 'high'
      if (latency > 1000) return 'medium'
      return 'low'
    }
    
    const formatLatency = (latency) => {
      if (latency < 1000) {
        return `${latency.toFixed(0)}ms`
      }
      return `${(latency / 1000).toFixed(1)}s`
    }
    
    const formatLastSuccess = (timestamp) => {
      if (!timestamp) return 'Never'
      
      const date = new Date(timestamp)
      const now = new Date()
      const diffMs = now - date
      const diffMins = Math.floor(diffMs / 60000)
      
      if (diffMins < 1) return 'Just now'
      if (diffMins < 60) return `${diffMins}m ago`
      
      const diffHours = Math.floor(diffMins / 60)
      if (diffHours < 24) return `${diffHours}h ago`
      
      const diffDays = Math.floor(diffHours / 24)
      return `${diffDays}d ago`
    }
    
    return {
      sortedProviders,
      getProviderIcon,
      getStatusClass,
      getStatusIcon,
      getLatencyClass,
      formatLatency,
      formatLastSuccess
    }
  }
}
</script>

<style scoped>
.provider-health-matrix {
  background-color: var(--card-bg);
  border: 1px solid var(--card-border);
  border-radius: 8px;
  overflow: hidden;
}

.health-table {
  width: 100%;
  border-collapse: collapse;
}

.health-table thead {
  background-color: var(--table-header-bg);
}

.health-table th {
  padding: 12px 16px;
  text-align: left;
  font-weight: 600;
  font-size: 0.9em;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  border-bottom: 2px solid var(--border-color);
}

.health-table tbody tr {
  border-bottom: 1px solid var(--border-color);
  transition: background-color 0.2s;
}

.health-table tbody tr:hover {
  background-color: var(--row-hover-bg);
}

.health-table tbody tr.disabled {
  opacity: 0.6;
}

.health-table td {
  padding: 12px 16px;
}

.provider-name {
  font-weight: 500;
  color: var(--text-primary);
  display: flex;
  align-items: center;
  gap: 8px;
}

.provider-icon {
  font-size: 1.2em;
  color: var(--icon-color);
}

.status-indicator {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 12px;
  border-radius: 12px;
  font-size: 0.85em;
  font-weight: 500;
  text-transform: capitalize;
}

.status-indicator.healthy {
  background-color: var(--success-bg);
  color: var(--success-color);
}

.status-indicator.degraded {
  background-color: var(--warning-bg);
  color: var(--warning-color);
}

.status-indicator.unhealthy {
  background-color: var(--error-bg);
  color: var(--error-color);
}

.status-indicator.disabled {
  background-color: var(--disabled-bg);
  color: var(--text-secondary);
}

.models-count {
  font-weight: 600;
  color: var(--text-primary);
}

.error-rate {
  font-weight: 500;
}

.error-rate.high {
  color: var(--error-color);
}

.latency {
  font-weight: 500;
}

.latency.low {
  color: var(--success-color);
}

.latency.medium {
  color: var(--warning-color);
}

.latency.high {
  color: var(--error-color);
}

.last-success {
  color: var(--text-secondary);
  font-size: 0.9em;
}

.no-data {
  padding: 60px 20px;
  text-align: center;
  color: var(--text-secondary);
}

.no-data i {
  font-size: 3em;
  margin-bottom: 16px;
  opacity: 0.3;
}

.no-data p {
  margin: 0;
  font-size: 1.1em;
}

/* Theme Variables */
.provider-health-matrix {
  --card-bg: var(--bg-primary, #ffffff);
  --card-border: var(--border-color, #e0e0e0);
  --table-header-bg: var(--bg-secondary, #f5f5f5);
  --row-hover-bg: var(--bg-hover, #f9f9f9);
  --icon-color: var(--primary-color, #2196f3);
  --success-color: #4caf50;
  --success-bg: rgba(76, 175, 80, 0.1);
  --warning-color: #ff9800;
  --warning-bg: rgba(255, 152, 0, 0.1);
  --error-color: #f44336;
  --error-bg: rgba(244, 67, 54, 0.1);
  --disabled-bg: rgba(0, 0, 0, 0.05);
}

/* Dark mode */
@media (prefers-color-scheme: dark) {
  .provider-health-matrix {
    --card-bg: #1e1e1e;
    --card-border: #333333;
    --table-header-bg: #252525;
    --row-hover-bg: #2a2a2a;
    --disabled-bg: rgba(255, 255, 255, 0.05);
  }
}
</style>