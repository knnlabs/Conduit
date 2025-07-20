# Deployment Documentation

This directory contains guides for deploying and operating Conduit in production environments.

## Contents

- **[Deployment Configuration](./DEPLOYMENT-CONFIGURATION.md)** - Comprehensive production deployment guide
- **[Docker Optimization](./docker-optimization.md)** - Container performance and security best practices

## Quick Start

### Docker Deployment
```bash
# Using Docker Compose (recommended)
docker-compose up -d

# Using standalone Docker
docker run -p 5000:5000 ghcr.io/knnlabs/conduit:latest
```

### Production Checklist
- [ ] Configure PostgreSQL connection string
- [ ] Set up Redis for caching (optional but recommended)
- [ ] Configure RabbitMQ for event processing (optional)
- [ ] Set authentication keys (CONDUIT_API_TO_API_BACKEND_AUTH_KEY)
- [ ] Configure CORS origins for your domain
- [ ] Set up SSL/TLS termination
- [ ] Configure monitoring and logging

## Environment Configuration

Key environment variables:
- `ConnectionStrings__ConduitDb` - PostgreSQL connection
- `ConnectionStrings__RedisCache` - Redis connection (optional)
- `RabbitMQ__HostName` - RabbitMQ server (optional)
- `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` - Backend service authentication
- `CORS_ALLOWED_ORIGINS` - Allowed CORS origins

See [Environment Variables](../Environment-Variables.md) for complete list.

## Deployment Options

### 1. Docker Compose (Recommended)
Best for: Single-server deployments, development, small to medium scale
- Includes all dependencies (PostgreSQL, Redis, RabbitMQ)
- Easy to manage and update
- Good for up to ~1000 requests/minute

### 2. Kubernetes
Best for: Large scale, high availability requirements
- Horizontal scaling capabilities
- Rolling updates with zero downtime
- Advanced traffic management

### 3. Cloud Platforms
- **AWS ECS/Fargate**: Managed container orchestration
- **Google Cloud Run**: Serverless container platform
- **Azure Container Instances**: Simple container hosting

## Performance Tuning

For high-throughput deployments:
1. Enable Redis caching
2. Configure RabbitMQ for async processing
3. Use connection pooling for PostgreSQL
4. Enable HTTP/2 and compression
5. Configure appropriate resource limits

See [Docker Optimization](./docker-optimization.md) for detailed tuning guide.

## Monitoring

Recommended monitoring stack:
- **Prometheus**: Metrics collection
- **Grafana**: Dashboards and visualization
- **OpenTelemetry**: Distributed tracing
- **ELK Stack**: Log aggregation

Pre-built dashboards available in [Grafana Dashboards](../grafana-dashboards/).

## Security Considerations

1. Always use HTTPS in production
2. Rotate API keys regularly
3. Limit database permissions
4. Use secrets management for sensitive configuration
5. Enable audit logging
6. Configure firewall rules appropriately

## Troubleshooting

Common deployment issues:
- Database connection failures: Check connection string and network access
- High memory usage: Enable Redis caching, tune connection pools
- Slow response times: Check provider health, enable caching
- Event processing delays: Scale RabbitMQ consumers

For detailed troubleshooting, see [Runbooks](../runbooks/).