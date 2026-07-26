import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { components } from '../generated/admin-api';
import type { RequestConfig } from '../client/types';
import type {
  SystemHealthDto,
  ServiceStatusMapDto,
  ComponentHealthStatus,
  HealthEventDto,
  HealthEventsResponseDto,
  HealthEventSubscriptionOptions,
  HealthEventSubscription
} from '../models/system';
import type { ISystemHealthService } from './types/system-service.types';
import { ENDPOINTS } from '../constants';
import { FetchSystemService } from './FetchSystemService';
import { FetchSystemMetricsService } from './FetchSystemMetricsService';
import { FetchSystemHelpers } from './FetchSystemHelpers';

type ServiceStatusEntry = NonNullable<
  components['schemas']['ServiceHealthResponse']['services']
>[number];
type IncidentsResponse = components['schemas']['IncidentsResponse'];

/**
 * Service ids as reported by `/v1/admin/health-status/services`. Keeping them in one place makes
 * the mapping auditable — a renamed id shows up here rather than silently degrading a card to
 * `unknown`.
 */
const SERVICE_IDS = {
  gateway: 'core-api',
  admin: 'admin-api',
  database: 'database',
  cache: 'redis',
  queue: 'messaging',
} as const;

/** Human-readable status line for a component, honest about `unknown`. */
function describeComponent(name: string, status: ComponentHealthStatus): string {
  switch (status) {
    case 'healthy':
      return `${name} responding normally`;
    case 'degraded':
      return `${name} degraded`;
    case 'unhealthy':
      return `${name} unavailable`;
    default:
      return `${name} status unknown`;
  }
}

/**
 * Type-safe System health service using native fetch
 */
export class FetchSystemHealthService implements ISystemHealthService {
  private helpers: FetchSystemHelpers;

  constructor(private readonly client: FetchBaseApiClient) {
    this.helpers = new FetchSystemHelpers();
  }

  /**
   * Get comprehensive system health status and metrics.
   * This method aggregates health data from multiple endpoints to provide
   * a complete picture of system health including individual component status
   * and overall system metrics.
   *
   * @param config - Optional request configuration for timeout, signal, headers
   * @returns Promise<SystemHealthDto> - Complete system health information including:
   *   - overall: Overall system health status
   *   - components: Individual service component health (API, database, cache, queue)
   *   - metrics: Resource utilization metrics (CPU, memory, disk, active connections)
   * @throws {Error} When system health data cannot be retrieved
   * @since Issue #427 - System Health SDK Methods
   */
  async getSystemHealth(config?: RequestConfig): Promise<SystemHealthDto> {
    // Get service status for detailed component health
    const serviceStatus = await this.getServiceStatus(config);
    const lastChecked = new Date().toISOString();

    // Transform the data to match the expected SystemHealthDto structure
    const components = {
      api: {
        status: serviceStatus.coreApi.status,
        message: describeComponent('Gateway API', serviceStatus.coreApi.status),
        lastChecked,
      },
      database: {
        status: serviceStatus.database.status,
        message: describeComponent('Database', serviceStatus.database.status),
        lastChecked,
      },
      cache: {
        status: serviceStatus.cache.status,
        message: describeComponent('Cache', serviceStatus.cache.status),
        lastChecked,
      },
      // Real messaging health. This was pinned to `healthy` with the message "Message queue
      // processing normally" regardless of the transport's actual state (issue #1067).
      queue: {
        status: serviceStatus.queue.status,
        message: describeComponent('Message queue', serviceStatus.queue.status),
        lastChecked,
      },
    };

    // Calculate overall status based on components. `unknown` is not healthy — it degrades the
    // rollup, matching how the Admin API rolls up its own service list.
    const componentStatuses = Object.values(components).map(c => c.status);
    const hasUnhealthy = componentStatuses.some(s => s === 'unhealthy');
    const hasDegradedOrUnknown = componentStatuses.some(s => s === 'degraded' || s === 'unknown');

    let overall: ComponentHealthStatus = 'healthy';
    if (hasUnhealthy) overall = 'unhealthy';
    else if (hasDegradedOrUnknown) overall = 'degraded';

    // Get active connections count (null when the metrics endpoint is unavailable)
    const metricsService = new FetchSystemMetricsService(this.client);
    const activeConnections = await metricsService.getActiveConnections(config);

    return {
      overall,
      components,
      // Resource percentages are not measured by the backend — null, not 0%
      metrics: {
        cpu: null,
        memory: null,
        disk: null,
        activeConnections,
      },
    };
  }

  /**
   * Get detailed system resource metrics.
   * Delegates to FetchSystemMetricsService.
   */
  async getSystemMetrics(config?: RequestConfig): Promise<import('../models/system').SystemResourceMetricsDto> {
    const metricsService = new FetchSystemMetricsService(this.client);
    return metricsService.getSystemMetrics(config);
  }

  /**
   * Get health status of individual services.
   * Retrieves detailed health information for each service component including
   * Gateway API, Admin API, database, and cache services with latency and status details.
   * Uses dedicated services endpoint with fallback to health checks.
   *
   * @param config - Optional request configuration for timeout, signal, headers
   * @returns Promise<ServiceStatusMapDto> - Individual service health status including:
   *   - coreApi: Gateway API service health, latency, and endpoint
   *   - adminApi: Admin API service health, latency, and endpoint
   *   - database: Database health, latency, and connection count
   *   - cache: Cache service health, latency, and hit rate
   * @throws {Error} When service status data cannot be retrieved
   * @since Issue #427 - System Health SDK Methods
   */
  async getServiceStatus(config?: RequestConfig): Promise<ServiceStatusMapDto> {
    try {
      const byId = await this.getServiceEntriesById(config);

      return {
        coreApi: {
          status: this.statusOf(byId, SERVICE_IDS.gateway),
          latency: this.latencyOf(byId, SERVICE_IDS.gateway),
          endpoint: '/api',
        },
        adminApi: {
          status: this.statusOf(byId, SERVICE_IDS.admin),
          latency: this.latencyOf(byId, SERVICE_IDS.admin),
          endpoint: '/api',
        },
        database: {
          status: this.statusOf(byId, SERVICE_IDS.database),
          latency: this.latencyOf(byId, SERVICE_IDS.database),
          connections: null,
        },
        cache: {
          status: this.statusOf(byId, SERVICE_IDS.cache),
          latency: this.latencyOf(byId, SERVICE_IDS.cache),
          hitRate: null,
        },
        queue: {
          status: this.statusOf(byId, SERVICE_IDS.queue),
          latency: this.latencyOf(byId, SERVICE_IDS.queue),
        },
      };
    } catch {
      // Fallback when the service health endpoint is unreachable. Only the Admin's own readiness
      // is knowable from here, so everything else is reported `unknown` rather than being given
      // the Admin's status — the Gateway previously inherited it and looked healthy while down.
      const systemService = new FetchSystemService(this.client);
      const health = await systemService.getHealth(config);
      const adminStatus = this.helpers.normalizeStatus(health.status);

      return {
        coreApi: { status: 'unknown', latency: null, endpoint: '/api' },
        adminApi: {
          status: adminStatus,
          latency: health.totalDuration ?? null,
          endpoint: '/api',
        },
        database: {
          status: this.helpers.normalizeStatus(health.checks.database?.status),
          latency: health.checks.database?.duration ?? null,
          connections: null,
        },
        cache: { status: 'unknown', latency: null, hitRate: null },
        queue: { status: 'unknown', latency: null },
      };
    }
  }

  /**
   * Fetches the service health response and indexes its entries by service id.
   *
   * The previous implementation cast the response to `{ coreApi, adminApi, database, cache }`.
   * The endpoint has never returned that shape — it returns `{ timestamp, overallStatus,
   * summary, services[] }` — so every lookup was `undefined` and every service was reported
   * `healthy` with zero latency, regardless of what the backend actually said (issue #1067).
   */
  private async getServiceEntriesById(
    config?: RequestConfig
  ): Promise<Map<string, ServiceStatusEntry>> {
    const systemService = new FetchSystemService(this.client);
    const response = await systemService.getServiceHealth(config);
    return new Map(
      (response.services ?? [])
        .filter((service): service is ServiceStatusEntry & { id: string } =>
          typeof service.id === 'string'
        )
        .map(service => [service.id, service])
    );
  }

  private statusOf(byId: Map<string, ServiceStatusEntry>, id: string): ComponentHealthStatus {
    return this.helpers.normalizeStatus(byId.get(id)?.status);
  }

  /**
   * Probe round-trip for a service, or null when it does not have one. Heartbeat-derived
   * services report `responseTime: null`; rendering that as `0 ms` implies a probe that never ran.
   */
  private latencyOf(byId: Map<string, ServiceStatusEntry>, id: string): number | null {
    return byId.get(id)?.responseTime ?? null;
  }

  /**
   * Get system uptime in seconds.
   * Delegates to FetchSystemMetricsService.
   */
  async getUptime(config?: RequestConfig): Promise<number> {
    const metricsService = new FetchSystemMetricsService(this.client);
    return metricsService.getUptime(config);
  }

  /**
   * Get the number of active connections to the system (null when unknown).
   * Delegates to FetchSystemMetricsService.
   */
  async getActiveConnections(config?: RequestConfig): Promise<number | null> {
    const metricsService = new FetchSystemMetricsService(this.client);
    return metricsService.getActiveConnections(config);
  }

  /**
   * Get recent health events for the system.
   * Retrieves historical health events including provider outages, system issues,
   * and recovery events with detailed metadata and timestamps.
   *
   * @param limit - Optional limit on number of events to return (default: 50)
   * @param config - Optional request configuration for timeout, signal, headers
   * @returns Promise<HealthEventsResponseDto> - Array of health events with:
   *   - id: Unique event identifier
   *   - timestamp: ISO timestamp of event occurrence
   *   - type: Event type (provider_down, provider_up, system_issue, system_recovered)
   *   - message: Human-readable event description
   *   - severity: Event severity level (info, warning, error)
   *   - source: Event source (provider name, component name)
   *   - metadata: Additional context and details
   * @throws {Error} When health events cannot be retrieved
   * @since Issue #428 - Health Events SDK Methods
   */
  async getHealthEvents(limit?: number, config?: RequestConfig): Promise<HealthEventsResponseDto> {
    try {
      // Derived from the Admin incident history rather than synthesized. The previous
      // implementation invented events from the current health snapshot and stamped them with
      // `Date.now()` ids and `Math.random()` timestamps, so every poll produced a different
      // "history" of things that never happened at those times (issue #1067).
      const incidents = await this.client['get']<IncidentsResponse>(
        ENDPOINTS.SYSTEM.HEALTH_INCIDENTS,
        {
          signal: config?.signal,
          timeout: config?.timeout,
          headers: config?.headers,
        }
      );

      const events: HealthEventDto[] = (incidents.incidents ?? []).flatMap(incident => {
        if (!incident.startTime) {
          return [];
        }

        const source = incident.affectedService ?? 'system';
        const detail = incident.affectedModel
          ? `${incident.impact ?? 'Elevated error rate'} (model ${incident.affectedModel})`
          : incident.impact ?? 'Elevated error rate';

        const onset: HealthEventDto = {
          // The Admin API now returns stable incident ids, so these stay stable across polls.
          id: `${incident.id ?? source}-onset`,
          timestamp: incident.startTime,
          type: 'system_issue',
          message: incident.title ?? 'Service degradation',
          severity: incident.severity === 'critical' ? 'error' : 'warning',
          source,
          metadata: {
            componentName: source,
            errorDetails: detail,
          },
        };

        if (!incident.endTime) {
          return [onset];
        }

        return [
          onset,
          {
            id: `${incident.id ?? source}-recovered`,
            timestamp: incident.endTime,
            type: 'system_recovered',
            message: `${incident.title ?? 'Service degradation'} resolved`,
            severity: 'info',
            source,
            metadata: { componentName: source },
          },
        ];
      });

      // Sort events by timestamp (newest first)
      events.sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime());

      return {
        events: events.slice(0, limit ?? 50),
      };
    } catch {
      // If all else fails, return empty events
      return { events: [] };
    }
  }

  /**
   * Subscribe to real-time health event updates.
   * Creates a persistent connection to receive live health events as they occur,
   * supporting filtering by severity, type, and source with automatic reconnection.
   *
   * @param options - Optional subscription configuration:
   *   - severityFilter: Array of severity levels to include
   *   - typeFilter: Array of event types to include
   *   - sourceFilter: Array of sources to include
   * @param config - Optional request configuration for timeout, signal, headers
   * @returns Promise<HealthEventSubscription> - Subscription handle with:
   *   - unsubscribe(): Disconnect from events
   *   - isConnected(): Check connection status
   *   - onEvent(): Register event callback
   *   - onConnectionStateChanged(): Register connection callback
   * @throws {Error} When subscription cannot be established
   * @since Issue #428 - Health Events SDK Methods
   */
  async subscribeToHealthEvents(
    options?: HealthEventSubscriptionOptions,
    config?: RequestConfig
  ): Promise<HealthEventSubscription> {
    // Note: This implementation provides a basic subscription interface
    // In a full implementation, this would integrate with SignalR or WebSocket

    let connected = false;
    let eventCallbacks: Array<(event: HealthEventDto) => void> = [];
    let connectionCallbacks: Array<(connected: boolean) => void> = [];
    let pollInterval: ReturnType<typeof setInterval> | null = null;
    let lastEventTimestamp: string | null = null;

    const startPolling = () => {
      if (pollInterval) return;

      connected = true;
      connectionCallbacks.forEach(cb => cb(true));

      pollInterval = setInterval(() => {
        void (async () => {
          try {
            const events = await this.getHealthEvents(10, config);

          // Filter new events since last check
          const newEvents = events.events.filter(event => {
            if (!lastEventTimestamp) return true;
            return new Date(event.timestamp) > new Date(lastEventTimestamp);
          });

          // Apply filters if provided
          const filteredEvents = newEvents.filter(event => {
            if (options?.severityFilter && !options.severityFilter.includes(event.severity)) {
              return false;
            }
            if (options?.typeFilter && !options.typeFilter.includes(event.type)) {
              return false;
            }
            if (options?.sourceFilter && event.source && !options.sourceFilter.includes(event.source)) {
              return false;
            }
            return true;
          });

          // Notify callbacks of new events
          filteredEvents.forEach(event => {
            eventCallbacks.forEach(cb => cb(event));
          });

          // Update last event timestamp
          if (events.events.length > 0) {
            lastEventTimestamp = events.events[0].timestamp;
          }
          } catch (error: unknown) {
            console.warn('Health events polling error:', error);
            if (connected) {
              connected = false;
              connectionCallbacks.forEach(cb => cb(false));
            }
          }
        })();
      }, 5000); // Poll every 5 seconds
    };

    const stopPolling = () => {
      if (pollInterval) {
        clearInterval(pollInterval);
        pollInterval = null;
      }
      if (connected) {
        connected = false;
        connectionCallbacks.forEach(cb => cb(false));
      }
    };

    // Start polling immediately
    try {
      // Get initial events to establish baseline
      const initialEvents = await this.getHealthEvents(1, config);
      if (initialEvents.events.length > 0) {
        lastEventTimestamp = initialEvents.events[0].timestamp;
      }
      startPolling();
    } catch (error: unknown) {
      throw new Error(`Failed to establish health events subscription: ${String(error)}`);
    }

    return {
      unsubscribe: () => {
        stopPolling();
        eventCallbacks = [];
        connectionCallbacks = [];
      },

      isConnected: () => connected,

      onEvent: (callback: (event: HealthEventDto) => void) => {
        eventCallbacks.push(callback);
      },

      onConnectionStateChanged: (callback: (connected: boolean) => void) => {
        connectionCallbacks.push(callback);
      },
    };
  }
}
