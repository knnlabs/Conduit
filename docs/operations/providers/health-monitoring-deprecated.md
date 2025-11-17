# Provider Health Monitoring - DEPRECATED

**Status**: Removed in 2025

Provider health monitoring functionality has been removed from Conduit as part of issue #687. This feature was replaced with simpler error tracking and alerting mechanisms.

For historical documentation, see Git history prior to this date.

## Migration Notes

- The `conduit_provider_health` Prometheus gauge has been removed
- Provider health DTOs and SDK methods have been removed  
- Health monitoring background services have been removed
- Real-time provider health SignalR events have been removed

## Alternative Approaches

Instead of dedicated provider health monitoring, use:
- Standard provider error metrics (`conduit_provider_errors_total`)
- Provider latency tracking (`conduit_provider_latency_seconds`)
- Request success/failure rates from `conduit_model_requests_total`

