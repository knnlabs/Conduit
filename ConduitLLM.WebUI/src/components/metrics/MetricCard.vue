<template>
  <div class="metric-card" :class="{ 'alert': alertLevel }">
    <div class="metric-header">
      <i :class="icon" class="metric-icon"></i>
      <h3>{{ title }}</h3>
    </div>
    <div class="metric-value">
      <span class="value">{{ formattedValue }}</span>
      <div v-if="trend" class="trend" :class="trend.direction">
        <i :class="trendIcon"></i>
        <span>{{ trend.percentage.toFixed(1) }}%</span>
      </div>
    </div>
    <div v-if="alertLevel" class="alert-indicator" :class="alertLevel">
      <i class="fas fa-exclamation-triangle"></i>
    </div>
  </div>
</template>

<script>
import { computed } from 'vue'

export default {
  name: 'MetricCard',
  props: {
    title: {
      type: String,
      required: true
    },
    value: {
      type: [Number, String],
      required: true
    },
    format: {
      type: Function,
      default: null
    },
    icon: {
      type: String,
      default: 'fas fa-chart-line'
    },
    trend: {
      type: Object,
      default: null,
      validator: (value) => {
        return !value || (value.direction && typeof value.percentage === 'number')
      }
    },
    alertLevel: {
      type: String,
      default: null,
      validator: (value) => {
        return !value || ['warning', 'critical'].includes(value)
      }
    }
  },
  setup(props) {
    const formattedValue = computed(() => {
      if (props.format && typeof props.format === 'function') {
        return props.format(props.value)
      }
      return typeof props.value === 'number' ? props.value.toLocaleString() : props.value
    })

    const trendIcon = computed(() => {
      if (!props.trend) return ''
      return props.trend.direction === 'up' ? 'fas fa-arrow-up' : 'fas fa-arrow-down'
    })

    return {
      formattedValue,
      trendIcon
    }
  }
}
</script>

<style scoped>
.metric-card {
  background-color: var(--card-bg);
  border: 1px solid var(--card-border);
  border-radius: 8px;
  padding: 20px;
  position: relative;
  transition: all 0.3s ease;
}

.metric-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
}

.metric-card.alert {
  animation: pulse 2s infinite;
}

.metric-header {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 15px;
}

.metric-icon {
  font-size: 1.2em;
  color: var(--icon-color);
}

.metric-header h3 {
  margin: 0;
  font-size: 0.9em;
  font-weight: 500;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.metric-value {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
}

.value {
  font-size: 2em;
  font-weight: 600;
  color: var(--text-primary);
}

.trend {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 0.9em;
  font-weight: 500;
  padding: 4px 8px;
  border-radius: 4px;
}

.trend.up {
  color: var(--success-color);
  background-color: var(--success-bg);
}

.trend.down {
  color: var(--error-color);
  background-color: var(--error-bg);
}

.alert-indicator {
  position: absolute;
  top: 10px;
  right: 10px;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.8em;
}

.alert-indicator.warning {
  background-color: var(--warning-bg);
  color: var(--warning-color);
}

.alert-indicator.critical {
  background-color: var(--error-bg);
  color: var(--error-color);
}

@keyframes pulse {
  0% {
    box-shadow: 0 0 0 0 rgba(255, 0, 0, 0.4);
  }
  70% {
    box-shadow: 0 0 0 10px rgba(255, 0, 0, 0);
  }
  100% {
    box-shadow: 0 0 0 0 rgba(255, 0, 0, 0);
  }
}

/* Theme Variables */
.metric-card {
  --card-bg: var(--bg-primary, #ffffff);
  --card-border: var(--border-color, #e0e0e0);
  --icon-color: var(--primary-color, #2196f3);
  --success-color: #4caf50;
  --success-bg: rgba(76, 175, 80, 0.1);
  --error-color: #f44336;
  --error-bg: rgba(244, 67, 54, 0.1);
  --warning-color: #ff9800;
  --warning-bg: rgba(255, 152, 0, 0.1);
}

/* Dark mode */
@media (prefers-color-scheme: dark) {
  .metric-card {
    --card-bg: #1e1e1e;
    --card-border: #333333;
  }
}
</style>