---
sidebar_position: 1
title: Single Instance Deployment
description: Deploy Conduit as a single instance for development and small-scale production
---

# Single Instance Deployment

This guide covers deploying Conduit as a single instance using Docker Compose, suitable for development environments and small-scale production deployments.

## Prerequisites

- Docker and Docker Compose installed
- PostgreSQL database (local or remote)
- Basic understanding of environment variables

## Quick Start

### 1. Clone and Setup

```bash
git clone https://github.com/knnlabs/conduit.git
cd conduit
```

### 2. Configure Environment

Create a `.env` file in the project root:

```bash
# Database Configuration
DATABASE_URL=postgresql://username:password@localhost:5432/conduit_db

# Redis Configuration (optional for caching)
REDIS_URL=redis://localhost:6379/0

# Master Key for Admin Access
CONDUITLLM__MASTERKEY=your-secure-master-key-here

# CORS Configuration
CONDUITLLM__CORS__ALLOWEDORIGINS=http://localhost:3000,http://localhost:5001

# Logging Configuration
CONDUITLLM__LOGGING__LOGLEVEL=Information
```

### 3. Start Services

```bash
# Start all services
docker compose up -d

# View logs
docker compose logs -f

# Check service status
docker compose ps
```

This starts:
- **Core API** on port 5000
- **Admin API** on port 5002  
- **WebUI** on port 5001
- **PostgreSQL** on port 5432
- **Redis** on port 6379

## Service Architecture

### Core API (Port 5000)
- Handles all LLM requests
- OpenAI-compatible endpoints
- Virtual key authentication
- Provider routing and communication

### Admin API (Port 5002)
- Administrative operations
- Virtual key management
- Provider configuration
- Usage analytics

### WebUI (Port 5001)
- Administrative dashboard
- Real-time monitoring
- Configuration management
- Usage analytics

## Initial Configuration

### 1. Access Admin Dashboard

Navigate to `http://localhost:5001` and log in with your master key.

### 2. Add Provider Credentials

1. Go to **Configuration > Provider Credentials**
2. Click **Add Provider Credential**
3. Select your provider (e.g., OpenAI, Anthropic)
4. Enter API credentials
5. Click **Save**

### 3. Create Virtual Keys

1. Navigate to **Virtual Keys**
2. Click **Create New Key**
3. Configure permissions and limits
4. Save and copy the generated key

### 4. Test the API

```bash
curl http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer condt_your_virtual_key" \
  -d '{
    "model": "gpt-3.5-turbo",
    "messages": [{"role": "user", "content": "Hello from Conduit!"}]
  }'
```

## Storage Configuration

### In-Memory Storage (Default)
Perfect for development and testing:

```bash
# No additional configuration needed
# Media files stored in memory
```

### S3-Compatible Storage
For production deployments:

```bash
# Storage Configuration
CONDUITLLM__STORAGE__PROVIDER=S3
CONDUITLLM__STORAGE__S3__SERVICEURL=https://your-s3-endpoint.com
CONDUITLLM__STORAGE__S3__ACCESSKEY=your-access-key
CONDUITLLM__STORAGE__S3__SECRETKEY=your-secret-key
CONDUITLLM__STORAGE__S3__BUCKETNAME=conduit-media
CONDUITLLM__STORAGE__S3__REGION=us-east-1
```

## Performance Tuning

### Database Connection Pooling

```bash
# PostgreSQL Connection Pool
CONDUITLLM__DATABASE__MAXPOOLSIZE=100
CONDUITLLM__DATABASE__MINPOOLSIZE=5
CONDUITLLM__DATABASE__CONNECTIONTIMEOUT=30
```

### Redis Configuration

```bash
# Redis Connection Settings
CONDUITLLM__REDIS__CONNECTIONSTRING=redis://localhost:6379/0
CONDUITLLM__REDIS__COMMANDTIMEOUT=5000
CONDUITLLM__REDIS__CONNECTTIMEOUT=5000
```

## Monitoring and Health Checks

### Health Endpoints

```bash
# Core API Health
curl http://localhost:5000/health

# Admin API Health  
curl http://localhost:5002/health

# Detailed Health Information
curl http://localhost:5000/health/ready
```

### Log Monitoring

```bash
# View real-time logs
docker compose logs -f conduit-api

# View specific service logs
docker compose logs -f conduit-admin
docker compose logs -f conduit-webui
```

## Backup and Recovery

### Database Backup

```bash
# Create database backup
docker exec -t conduit-postgres pg_dump -U conduit_user conduit_db > backup.sql

# Restore database
docker exec -i conduit-postgres psql -U conduit_user conduit_db < backup.sql
```

### Configuration Export

Use the Admin API to export configuration:

```bash
curl -H "Authorization: Bearer your-master-key" \
  http://localhost:5002/api/admin/export/configuration > config-backup.json
```

## Troubleshooting

### Common Issues

**Service won't start:**
```bash
# Check logs for errors
docker compose logs conduit-api

# Verify database connection
docker compose exec conduit-postgres psql -U conduit_user -d conduit_db -c "SELECT 1;"
```

**API returns 500 errors:**
```bash
# Check database migrations
docker compose logs conduit-api | grep -i migration

# Verify Redis connection
docker compose exec redis redis-cli ping
```

**WebUI won't load:**
```bash
# Check CORS configuration
# Verify CONDUITLLM__CORS__ALLOWEDORIGINS includes your domain
```

### Log Analysis

```bash
# Search for errors
docker compose logs | grep -i error

# Check authentication issues
docker compose logs | grep -i "unauthorized\|forbidden"

# Monitor performance
docker compose logs | grep -i "slow\|timeout"
```

## Scaling Considerations

### When to Scale Up

Single instance deployment is suitable for:
- Development and testing
- Small teams (< 10 users)
- Low to moderate traffic (< 100 requests/minute)
- Budget-conscious deployments

### Signs You Need Multi-Instance

Consider [Production Deployment](production-deployment) when you experience:
- High request volume (> 1000 requests/minute)
- Multiple teams or departments
- High availability requirements
- Geographic distribution needs

## Security Considerations

### Network Security

```bash
# Bind to localhost only for development
CONDUITLLM__KESTREL__ENDPOINTS__HTTP__URL=http://localhost:5000

# Use HTTPS in production
CONDUITLLM__KESTREL__ENDPOINTS__HTTPS__URL=https://0.0.0.0:5443
```

### API Key Security

- Use strong master keys (minimum 32 characters)
- Rotate virtual keys regularly
- Implement IP restrictions when possible
- Monitor API usage for anomalies

## Next Steps

- **Production Deployment**: Learn about [multi-instance deployment](production-deployment)
- **Scaling**: Configure [horizontal scaling](scaling-configuration) 
- **Monitoring**: Set up [comprehensive monitoring](monitoring-health)
- **Administration**: Explore the [Admin API](../admin/admin-api-overview)