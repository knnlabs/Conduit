# Incident Management and Health History Requirements

This document outlines the requirements for implementing incident management and health history tracking features in the Conduit Admin SDK and WebUI.

## Overview

To complete the system monitoring and health tracking capabilities, the following features need to be implemented:

### 1. Incident Management

**Purpose**: Track, manage, and analyze system incidents and outages across all providers and services.

**Use Cases in WebUI**:
- Display active incidents on dashboards
- Show incident history and timeline
- Track incident resolution and impact
- Generate incident reports and metrics
- Alert administrators about new incidents

**Required API Endpoints**:
```
GET    /api/admin/incidents                    # List all incidents with filtering
GET    /api/admin/incidents/{id}               # Get specific incident details
POST   /api/admin/incidents                    # Create new incident
PUT    /api/admin/incidents/{id}               # Update incident status/details
DELETE /api/admin/incidents/{id}               # Delete incident
POST   /api/admin/incidents/{id}/updates       # Add update to incident timeline
POST   /api/admin/incidents/{id}/resolve       # Mark incident as resolved
GET    /api/admin/incidents/statistics         # Get incident statistics
```

**Data Model**:
```typescript
interface IncidentDto {
  id: string;
  title: string;
  description: string;
  severity: 'low' | 'medium' | 'high' | 'critical';
  status: 'investigating' | 'identified' | 'monitoring' | 'resolved';
  category: 'provider' | 'system' | 'network' | 'performance' | 'security';
  affectedServices: string[];
  affectedProviders: string[];
  startTime: string;
  endTime?: string;
  impact: {
    usersAffected: number;
    requestsAffected: number;
    estimatedDowntime: number;
    revenueImpact?: number;
  };
  updates: IncidentUpdateDto[];
  rootCause?: string;
  resolution?: string;
  postmortem?: string;
  createdBy: string;
  lastUpdatedBy: string;
}

interface IncidentUpdateDto {
  id: string;
  timestamp: string;
  status: string;
  message: string;
  author: string;
}

interface CreateIncidentDto {
  title: string;
  description: string;
  severity: 'low' | 'medium' | 'high' | 'critical';
  category: string;
  affectedServices: string[];
  affectedProviders: string[];
}
```

**React Query Hooks Needed**:
```typescript
// Query hooks
useIncidents(filters?: IncidentFilters)          // List incidents
useIncident(id: string)                          // Get specific incident
useIncidentStatistics(dateRange?: DateRange)     // Get statistics

// Mutation hooks
useCreateIncident()                              // Create new incident
useUpdateIncident()                              // Update incident
useDeleteIncident()                              // Delete incident
useAddIncidentUpdate()                           // Add timeline update
useResolveIncident()                             // Mark as resolved
```

### 2. Health History Tracking

**Purpose**: Store and analyze historical health metrics for trend analysis and SLA reporting.

**Use Cases in WebUI**:
- Display health trends over time
- Generate uptime/SLA reports
- Analyze performance degradation patterns
- Compare provider reliability
- Predict potential issues based on trends

**Required API Endpoints**:
```
GET    /api/admin/health/history               # Get health history with filtering
GET    /api/admin/health/history/aggregate     # Get aggregated health metrics
GET    /api/admin/health/history/trends        # Get health trend analysis
GET    /api/admin/health/history/sla           # Get SLA compliance report
POST   /api/admin/health/history/export        # Export health history data
```

**Data Model**:
```typescript
interface HealthHistoryRecordDto {
  id: string;
  timestamp: string;
  service: string;
  provider?: string;
  status: 'healthy' | 'degraded' | 'unhealthy';
  responseTimeMs: number;
  uptime: number;
  errorRate: number;
  throughput: number;
  checks: HealthCheckResultDto[];
  metadata: Record<string, any>;
}

interface HealthCheckResultDto {
  name: string;
  status: 'pass' | 'warn' | 'fail';
  duration: number;
  message?: string;
  details?: Record<string, any>;
}

interface HealthAggregateDto {
  period: string;
  service: string;
  provider?: string;
  avgResponseTime: number;
  avgUptime: number;
  avgErrorRate: number;
  totalRequests: number;
  totalErrors: number;
  availability: number;
}

interface HealthTrendDto {
  service: string;
  provider?: string;
  trendDirection: 'improving' | 'stable' | 'degrading';
  changePercent: number;
  projectedSLA: number;
  anomalies: AnomalyDto[];
}
```

**React Query Hooks Needed**:
```typescript
// Query hooks
useHealthHistory(filters?: HealthHistoryFilters)     // Get raw history
useHealthAggregate(params: AggregateParams)         // Get aggregated metrics
useHealthTrends(dateRange: DateRange)               // Get trend analysis
useHealthSLA(dateRange: DateRange)                  // Get SLA report

// Mutation hooks
useExportHealthHistory()                             // Export history data
```

### 3. Integration Points

**Dashboard Integration**:
- Real-time incident alerts
- Health trend widgets
- SLA compliance indicators
- Incident impact metrics

**Notification System**:
- New incident alerts
- SLA breach warnings
- Health degradation notifications
- Incident resolution updates

**Reporting**:
- Monthly uptime reports
- Incident postmortem templates
- SLA compliance reports
- Performance trend analysis

### 4. Implementation Considerations

**Backend Requirements**:
1. Database schema for incidents and health history
2. Background jobs for health data collection
3. Data retention policies
4. Aggregation and analytics services
5. Real-time incident detection

**Frontend Requirements**:
1. Incident management UI components
2. Health history charts and visualizations
3. Real-time updates via SignalR
4. Export and reporting features
5. Mobile-responsive incident tracking

**Performance Considerations**:
- Efficient data aggregation for large datasets
- Caching strategies for historical data
- Pagination for incident lists
- Real-time updates without overwhelming the UI

### 5. Benefits of Implementation

1. **Proactive Monitoring**: Detect and respond to issues before they impact users
2. **Historical Analysis**: Learn from past incidents to prevent future occurrences
3. **SLA Compliance**: Track and report on service level agreements
4. **Trend Analysis**: Identify degradation patterns early
5. **Accountability**: Maintain audit trail of incidents and resolutions
6. **Customer Trust**: Transparent incident communication and tracking

## Next Steps

1. Design and implement database schema
2. Create API endpoints in the Core API
3. Implement services in Admin SDK
4. Create React Query hooks
5. Build UI components in WebUI
6. Add real-time notifications
7. Implement reporting features

This infrastructure would provide comprehensive monitoring and incident management capabilities, making Conduit a more robust and enterprise-ready platform.