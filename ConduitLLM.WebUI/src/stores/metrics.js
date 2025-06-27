import { defineStore } from 'pinia'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useAuthStore } from './auth'

export const useMetricsStore = defineStore('metrics', {
  state: () => ({
    connection: null,
    isConnected: false,
    connectionError: null,
    currentSnapshot: null,
    historicalData: {
      requestRate: [],
      errorRate: [],
      responseTime: [],
      costRate: []
    },
    alerts: [],
    updateIntervalId: null,
    subscribedMetrics: ['http', 'infrastructure', 'business', 'providers']
  }),

  actions: {
    async connect() {
      if (this.connection) {
        return
      }

      const authStore = useAuthStore()
      
      try {
        this.connection = new HubConnectionBuilder()
          .withUrl('/api/hubs/metrics', {
            accessTokenFactory: () => authStore.token
          })
          .withAutomaticReconnect({
            nextRetryDelayInMilliseconds: retryContext => {
              if (retryContext.elapsedMilliseconds < 60000) {
                return Math.random() * 10000
              }
              return null
            }
          })
          .configureLogging(LogLevel.Information)
          .build()

        // Set up event handlers
        this.connection.on('MetricsSnapshot', (snapshot) => {
          this.handleMetricsSnapshot(snapshot)
        })

        this.connection.on('MetricAlerts', (alerts) => {
          this.handleMetricAlerts(alerts)
        })

        this.connection.on('HttpMetricsUpdate', (metrics) => {
          if (this.currentSnapshot) {
            this.currentSnapshot.http = metrics
          }
        })

        this.connection.on('InfrastructureMetricsUpdate', (metrics) => {
          if (this.currentSnapshot) {
            this.currentSnapshot.infrastructure = metrics
          }
        })

        this.connection.on('BusinessMetricsUpdate', (metrics) => {
          if (this.currentSnapshot) {
            this.currentSnapshot.business = metrics
          }
        })

        this.connection.on('ProviderHealthUpdate', (health) => {
          if (this.currentSnapshot) {
            this.currentSnapshot.providerHealth = health
          }
        })

        // Connection lifecycle events
        this.connection.onclose((error) => {
          this.isConnected = false
          if (error) {
            this.connectionError = 'Connection lost. Attempting to reconnect...'
          }
        })

        this.connection.onreconnecting((error) => {
          this.connectionError = 'Reconnecting...'
        })

        this.connection.onreconnected(() => {
          this.isConnected = true
          this.connectionError = null
          this.subscribeToMetrics()
        })

        await this.connection.start()
        this.isConnected = true
        this.connectionError = null

        // Subscribe to metrics
        await this.subscribeToMetrics()
      } catch (error) {
        console.error('Failed to connect to metrics hub:', error)
        this.connectionError = error.message || 'Failed to connect to metrics server'
        this.isConnected = false
      }
    },

    async disconnect() {
      if (this.connection) {
        this.stopMetricsStream()
        await this.connection.stop()
        this.connection = null
        this.isConnected = false
      }
    },

    async subscribeToMetrics() {
      if (!this.connection || !this.isConnected) return

      try {
        await this.connection.invoke('SubscribeToMetrics', this.subscribedMetrics)
      } catch (error) {
        console.error('Failed to subscribe to metrics:', error)
      }
    },

    async updateInterval(seconds) {
      this.stopMetricsStream()
      
      if (!this.connection || !this.isConnected) return

      try {
        // Start streaming metrics at the specified interval
        const stream = this.connection.stream('StreamMetrics', seconds)
        
        stream.subscribe({
          next: (snapshot) => {
            this.handleMetricsSnapshot(snapshot)
          },
          error: (err) => {
            console.error('Metrics stream error:', err)
            this.connectionError = 'Metrics stream interrupted'
          },
          complete: () => {
            console.log('Metrics stream completed')
          }
        })
      } catch (error) {
        console.error('Failed to start metrics stream:', error)
      }
    },

    stopMetricsStream() {
      if (this.updateIntervalId) {
        clearInterval(this.updateIntervalId)
        this.updateIntervalId = null
      }
    },

    async loadHistoricalData(timeRange) {
      if (!this.connection || !this.isConnected) return

      try {
        const endTime = new Date()
        const startTime = new Date()
        
        // Calculate start time based on range
        switch (timeRange) {
          case '15m':
            startTime.setMinutes(startTime.getMinutes() - 15)
            break
          case '1h':
            startTime.setHours(startTime.getHours() - 1)
            break
          case '6h':
            startTime.setHours(startTime.getHours() - 6)
            break
          case '24h':
            startTime.setHours(startTime.getHours() - 24)
            break
        }

        const request = {
          startTime: startTime.toISOString(),
          endTime: endTime.toISOString(),
          metricNames: [
            'http_requests_per_second',
            'http_error_rate',
            'http_response_time_p95',
            'cost_per_minute'
          ],
          interval: timeRange === '15m' ? '1m' : timeRange === '1h' ? '5m' : '15m'
        }

        const response = await this.connection.invoke('GetHistoricalMetrics', request)
        
        // Process historical data
        response.series.forEach(series => {
          switch (series.metricName) {
            case 'http_requests_per_second':
              this.historicalData.requestRate = series.dataPoints
              break
            case 'http_error_rate':
              this.historicalData.errorRate = series.dataPoints
              break
            case 'http_response_time_p95':
              this.historicalData.responseTime = series.dataPoints
              break
            case 'cost_per_minute':
              this.historicalData.costRate = series.dataPoints
              break
          }
        })
      } catch (error) {
        console.error('Failed to load historical data:', error)
      }
    },

    async checkProviderHealth(providerName = null) {
      if (!this.connection || !this.isConnected) return []

      try {
        return await this.connection.invoke('CheckProviderHealth', providerName)
      } catch (error) {
        console.error('Failed to check provider health:', error)
        return []
      }
    },

    async getTopVirtualKeys(metric, count = 10) {
      if (!this.connection || !this.isConnected) return []

      try {
        return await this.connection.invoke('GetTopVirtualKeys', metric, count)
      } catch (error) {
        console.error('Failed to get top virtual keys:', error)
        return []
      }
    },

    handleMetricsSnapshot(snapshot) {
      this.currentSnapshot = snapshot
      
      // Update historical data with new point
      const now = new Date()
      
      if (snapshot.http) {
        this.addHistoricalPoint('requestRate', {
          timestamp: now,
          value: snapshot.http.requestsPerSecond
        })
        
        this.addHistoricalPoint('errorRate', {
          timestamp: now,
          value: snapshot.http.errorRate
        })
        
        if (snapshot.http.responseTimes) {
          this.addHistoricalPoint('responseTime', {
            timestamp: now,
            value: snapshot.http.responseTimes.p95
          })
        }
      }
      
      if (snapshot.business?.costs) {
        this.addHistoricalPoint('costRate', {
          timestamp: now,
          value: snapshot.business.costs.totalCostPerMinute
        })
      }
    },

    handleMetricAlerts(alerts) {
      this.alerts = alerts
    },

    addHistoricalPoint(metric, point) {
      const data = this.historicalData[metric]
      if (!data) return
      
      data.push(point)
      
      // Keep only last 24 hours of data
      const cutoff = new Date()
      cutoff.setHours(cutoff.getHours() - 24)
      
      this.historicalData[metric] = data.filter(p => new Date(p.timestamp) > cutoff)
    }
  }
})