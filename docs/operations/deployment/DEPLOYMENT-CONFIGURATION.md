# Conduit WebUI Deployment & Configuration Guide

## Overview

This guide covers deployment and configuration of the Conduit WebUI with SDK integration for various environments including development, staging, and production.

## Table of Contents

1. [Environment Configuration](#environment-configuration)
2. [Development Setup](#development-setup)
3. [Docker Deployment](#docker-deployment)
4. [Production Deployment](#production-deployment)
5. [Environment Variables](#environment-variables)
6. [Security Configuration](#security-configuration)
7. [Performance Tuning](#performance-tuning)
8. [Monitoring Setup](#monitoring-setup)
9. [Backup and Recovery](#backup-and-recovery)

## Environment Configuration

### Required Environment Variables

Create a `.env.local` file in the root directory:

```bash
# API Endpoints (Internal - for server-side calls)
CONDUIT_API_BASE_URL=http://api:8080
CONDUIT_ADMIN_API_BASE_URL=http://admin:8080

# API Endpoints (External - for client-side/SignalR)
NEXT_PUBLIC_CONDUIT_API_EXTERNAL_URL=https://api.yourdomain.com
NEXT_PUBLIC_CONDUIT_ADMIN_API_EXTERNAL_URL=https://admin-api.yourdomain.com

# Authentication
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=your-master-key-here
NEXTAUTH_SECRET=your-nextauth-secret-here
NEXTAUTH_URL=https://app.yourdomain.com

# Database
DATABASE_URL=postgresql://user:password@localhost:5432/conduit

# Redis (for caching and sessions)
REDIS_URL=redis://localhost:6379/0
REDIS_URL_SIGNALR=redis://localhost:6379/2

# Security
ENCRYPTION_KEY=your-32-char-encryption-key-here
SESSION_TIMEOUT=3600000

# Features
ENABLE_SIGNALR=true
ENABLE_WEBHOOKS=true
ENABLE_RATE_LIMITING=true

# Monitoring
SENTRY_DSN=https://your-sentry-dsn
ENABLE_TELEMETRY=true
```

### Environment-Specific Configurations

#### Development (.env.development)
```bash
NODE_ENV=development
NEXT_PUBLIC_CONDUIT_CORE_API_URL=http://localhost:5000
NEXT_PUBLIC_CONDUIT_ADMIN_API_URL=http://localhost:5002
LOG_LEVEL=debug
ENABLE_HOT_RELOAD=true
```

#### Staging (.env.staging)
```bash
NODE_ENV=staging
NEXT_PUBLIC_CONDUIT_CORE_API_URL=https://staging-api.yourdomain.com
NEXT_PUBLIC_CONDUIT_ADMIN_API_URL=https://staging-admin.yourdomain.com
LOG_LEVEL=info
ENABLE_PERFORMANCE_MONITORING=true
```

#### Production (.env.production)
```bash
NODE_ENV=production
NEXT_PUBLIC_CONDUIT_CORE_API_URL=https://api.yourdomain.com
NEXT_PUBLIC_CONDUIT_ADMIN_API_URL=https://admin-api.yourdomain.com
LOG_LEVEL=error
ENABLE_SECURITY_HEADERS=true
ENABLE_RATE_LIMITING=true
```

## Development Setup

### Prerequisites
- Node.js 18+ 
- npm or yarn
- Redis (optional for development)
- PostgreSQL or compatible database

### Local Development

1. Clone the repository:
```bash
git clone https://github.com/your-org/conduit-webui.git
cd conduit-webui/ConduitLLM.WebUI
```

2. Install dependencies:
```bash
npm install
```

3. Set up environment:
```bash
cp .env.example .env.local
# Edit .env.local with your configuration
```

4. Run database migrations:
```bash
npm run db:migrate
```

5. Start development server:
```bash
npm run dev
```

### Development with Docker Compose

```yaml
# docker-compose.dev.yml
version: '3.8'

services:
  webui:
    build:
      context: ./ConduitLLM.WebUI
      dockerfile: Dockerfile.dev
    ports:
      - "3000:3000"
    environment:
      - NODE_ENV=development
      - CONDUIT_API_BASE_URL=http://api:8080
      - CONDUIT_ADMIN_API_BASE_URL=http://admin:8080
    volumes:
      - ./ConduitLLM.WebUI:/app
      - /app/node_modules
      - /app/.next
    depends_on:
      - redis
      - postgres

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

  postgres:
    image: postgres:15-alpine
    environment:
      POSTGRES_DB: conduit_dev
      POSTGRES_USER: conduit
      POSTGRES_PASSWORD: devpassword
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

volumes:
  postgres_data:
```

## Docker Deployment

### Production Dockerfile

```dockerfile
# ConduitLLM.WebUI/Dockerfile
FROM node:18-alpine AS deps
WORKDIR /app
COPY package*.json ./
RUN npm ci --only=production

FROM node:18-alpine AS builder
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM node:18-alpine AS runner
WORKDIR /app

ENV NODE_ENV production

RUN addgroup --system --gid 1001 nodejs
RUN adduser --system --uid 1001 nextjs

COPY --from=builder /app/public ./public
COPY --from=builder /app/.next/standalone ./
COPY --from=builder /app/.next/static ./.next/static

USER nextjs

EXPOSE 3000

ENV PORT 3000

CMD ["node", "server.js"]
```

### Docker Compose Production

```yaml
# docker-compose.yml
version: '3.8'

services:
  webui:
    image: conduit-webui:latest
    ports:
      - "3000:3000"
    environment:
      - NODE_ENV=production
      - CONDUIT_API_BASE_URL=http://api:8080
      - CONDUIT_ADMIN_API_BASE_URL=http://admin:8080
      - NEXT_PUBLIC_CONDUIT_API_EXTERNAL_URL=${EXTERNAL_API_URL}
      - NEXT_PUBLIC_CONDUIT_ADMIN_API_EXTERNAL_URL=${EXTERNAL_ADMIN_URL}
      - DATABASE_URL=${DATABASE_URL}
      - REDIS_URL=redis://redis:6379/0
      - REDIS_URL_SIGNALR=redis://redis:6379/2
    depends_on:
      - redis
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:3000/api/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  redis:
    image: redis:7-alpine
    volumes:
      - redis_data:/data
    restart: unless-stopped
    command: redis-server --appendonly yes

  nginx:
    image: nginx:alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - ./ssl:/etc/nginx/ssl:ro
    depends_on:
      - webui
    restart: unless-stopped

volumes:
  redis_data:
```

### Nginx Configuration

```nginx
# nginx.conf
events {
    worker_connections 1024;
}

http {
    upstream webui {
        server webui:3000;
    }

    server {
        listen 80;
        server_name app.yourdomain.com;
        return 301 https://$server_name$request_uri;
    }

    server {
        listen 443 ssl http2;
        server_name app.yourdomain.com;

        ssl_certificate /etc/nginx/ssl/cert.pem;
        ssl_certificate_key /etc/nginx/ssl/key.pem;
        ssl_protocols TLSv1.2 TLSv1.3;
        ssl_ciphers HIGH:!aNULL:!MD5;

        location / {
            proxy_pass http://webui;
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection 'upgrade';
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_cache_bypass $http_upgrade;
        }

        # WebSocket support for SignalR
        location /hubs/ {
            proxy_pass http://webui;
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection "upgrade";
            proxy_set_header Host $host;
            proxy_read_timeout 86400;
        }
    }
}
```

## Production Deployment

### Vercel Deployment

1. Install Vercel CLI:
```bash
npm i -g vercel
```

2. Configure project:
```bash
vercel
```

3. Set environment variables:
```bash
vercel env add CONDUIT_API_BASE_URL
vercel env add CONDUIT_API_TO_API_BACKEND_AUTH_KEY
# Add all required environment variables
```

4. Deploy:
```bash
vercel --prod
```

### AWS ECS Deployment

```yaml
# task-definition.json
{
  "family": "conduit-webui",
  "taskRoleArn": "arn:aws:iam::123456789012:role/ConduitTaskRole",
  "executionRoleArn": "arn:aws:iam::123456789012:role/ConduitExecutionRole",
  "networkMode": "awsvpc",
  "containerDefinitions": [
    {
      "name": "webui",
      "image": "123456789012.dkr.ecr.us-east-1.amazonaws.com/conduit-webui:latest",
      "portMappings": [
        {
          "containerPort": 3000,
          "protocol": "tcp"
        }
      ],
      "environment": [
        {
          "name": "NODE_ENV",
          "value": "production"
        }
      ],
      "secrets": [
        {
          "name": "CONDUIT_API_TO_API_BACKEND_AUTH_KEY",
          "valueFrom": "arn:aws:secretsmanager:us-east-1:123456789012:secret:conduit/master-key"
        }
      ],
      "logConfiguration": {
        "logDriver": "awslogs",
        "options": {
          "awslogs-group": "/ecs/conduit-webui",
          "awslogs-region": "us-east-1",
          "awslogs-stream-prefix": "ecs"
        }
      },
      "healthCheck": {
        "command": ["CMD-SHELL", "curl -f http://localhost:3000/api/health || exit 1"],
        "interval": 30,
        "timeout": 5,
        "retries": 3
      }
    }
  ],
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "512",
  "memory": "1024"
}
```

### Kubernetes Deployment

```yaml
# k8s/deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: conduit-webui
  labels:
    app: conduit-webui
spec:
  replicas: 3
  selector:
    matchLabels:
      app: conduit-webui
  template:
    metadata:
      labels:
        app: conduit-webui
    spec:
      containers:
      - name: webui
        image: conduit-webui:latest
        ports:
        - containerPort: 3000
        env:
        - name: NODE_ENV
          value: "production"
        - name: CONDUIT_API_TO_API_BACKEND_AUTH_KEY
          valueFrom:
            secretKeyRef:
              name: conduit-secrets
              key: master-key
        envFrom:
        - configMapRef:
            name: conduit-config
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /api/health
            port: 3000
          initialDelaySeconds: 30
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /api/health
            port: 3000
          initialDelaySeconds: 10
          periodSeconds: 10
---
apiVersion: v1
kind: Service
metadata:
  name: conduit-webui
spec:
  selector:
    app: conduit-webui
  ports:
  - port: 80
    targetPort: 3000
  type: LoadBalancer
```

## Environment Variables

### Core Configuration

| Variable | Description | Required | Default |
|----------|-------------|----------|---------|
| `CONDUIT_API_BASE_URL` | Internal Core API URL | Yes | - |
| `CONDUIT_ADMIN_API_BASE_URL` | Internal Admin API URL | Yes | - |
| `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` | Master API key | Yes | - |
| `DATABASE_URL` | Database connection string | Yes | - |
| `REDIS_URL` | Redis connection string | No | - |
| `NEXTAUTH_SECRET` | NextAuth encryption secret | Yes | - |

### Public Configuration

| Variable | Description | Required | Default |
|----------|-------------|----------|---------|
| `NEXT_PUBLIC_CONDUIT_API_EXTERNAL_URL` | External Core API URL | Yes | - |
| `NEXT_PUBLIC_CONDUIT_ADMIN_API_EXTERNAL_URL` | External Admin API URL | Yes | - |
| `NEXT_PUBLIC_APP_NAME` | Application name | No | "Conduit" |
| `NEXT_PUBLIC_ENABLE_ANALYTICS` | Enable analytics | No | "false" |

### Security Configuration

| Variable | Description | Required | Default |
|----------|-------------|----------|---------|
| `ENCRYPTION_KEY` | 32-character encryption key | Yes | - |
| `SESSION_TIMEOUT` | Session timeout in ms | No | 3600000 |
| `ENABLE_RATE_LIMITING` | Enable rate limiting | No | "true" |
| `ALLOWED_ORIGINS` | CORS allowed origins | No | "*" |

## Security Configuration

### SSL/TLS Setup

1. Generate SSL certificates:
```bash
# Using Let's Encrypt
certbot certonly --standalone -d app.yourdomain.com
```

2. Configure in production:
```javascript
// next.config.js
module.exports = {
  async headers() {
    return [
      {
        source: '/:path*',
        headers: [
          { key: 'X-Content-Type-Options', value: 'nosniff' },
          { key: 'X-Frame-Options', value: 'DENY' },
          { key: 'X-XSS-Protection', value: '1; mode=block' },
          { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
          { key: 'Permissions-Policy', value: 'camera=(), microphone=(), geolocation=()' },
        ],
      },
    ];
  },
};
```

### Content Security Policy

```javascript
// middleware.ts
export function middleware(request: NextRequest) {
  const response = NextResponse.next();
  
  response.headers.set(
    'Content-Security-Policy',
    "default-src 'self'; " +
    "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
    "style-src 'self' 'unsafe-inline'; " +
    "img-src 'self' data: https:; " +
    "connect-src 'self' wss: https:;"
  );
  
  return response;
}
```

## Performance Tuning

### Next.js Optimization

```javascript
// next.config.js
module.exports = {
  images: {
    domains: ['cdn.yourdomain.com'],
    formats: ['image/avif', 'image/webp'],
  },
  experimental: {
    optimizeCss: true,
    scrollRestoration: true,
  },
  compress: true,
  poweredByHeader: false,
  reactStrictMode: true,
};
```

### Redis Configuration

```bash
# redis.conf
maxmemory 2gb
maxmemory-policy allkeys-lru
save 900 1
save 300 10
save 60 10000
```

### Database Optimization

```sql
-- Create indexes for common queries
CREATE INDEX idx_virtual_keys_user_id ON virtual_keys(user_id);
CREATE INDEX idx_virtual_keys_created_at ON virtual_keys(created_at);
CREATE INDEX idx_request_logs_virtual_key_id ON request_logs(virtual_key_id);
CREATE INDEX idx_request_logs_timestamp ON request_logs(timestamp);

-- Analyze tables
ANALYZE virtual_keys;
ANALYZE request_logs;
```

## Monitoring Setup

### Application Monitoring

```javascript
// lib/monitoring/sentry.ts
import * as Sentry from '@sentry/nextjs';

Sentry.init({
  dsn: process.env.SENTRY_DSN,
  environment: process.env.NODE_ENV,
  tracesSampleRate: process.env.NODE_ENV === 'production' ? 0.1 : 1.0,
  integrations: [
    new Sentry.BrowserTracing(),
    new Sentry.Replay(),
  ],
  replaysSessionSampleRate: 0.1,
  replaysOnErrorSampleRate: 1.0,
});
```

### Health Check Endpoint

```typescript
// app/api/health/route.ts
export async function GET() {
  const checks = {
    database: await checkDatabase(),
    redis: await checkRedis(),
    apis: await checkAPIs(),
  };
  
  const healthy = Object.values(checks).every(check => check.status === 'healthy');
  
  return new Response(
    JSON.stringify({
      status: healthy ? 'healthy' : 'unhealthy',
      checks,
      timestamp: new Date().toISOString(),
    }),
    {
      status: healthy ? 200 : 503,
      headers: { 'Content-Type': 'application/json' },
    }
  );
}
```

### Logging Configuration

```javascript
// lib/utils/logging.ts
import winston from 'winston';

export const logger = winston.createLogger({
  level: process.env.LOG_LEVEL || 'info',
  format: winston.format.json(),
  transports: [
    new winston.transports.Console({
      format: winston.format.simple(),
    }),
    new winston.transports.File({
      filename: 'error.log',
      level: 'error',
    }),
    new winston.transports.File({
      filename: 'combined.log',
    }),
  ],
});
```

## Backup and Recovery

### Database Backup

```bash
#!/bin/bash
# backup.sh

# Configuration
DB_NAME="conduit_production"
BACKUP_DIR="/backups"
S3_BUCKET="conduit-backups"
DATE=$(date +%Y%m%d_%H%M%S)

# Create backup
pg_dump $DATABASE_URL > "$BACKUP_DIR/backup_$DATE.sql"

# Compress
gzip "$BACKUP_DIR/backup_$DATE.sql"

# Upload to S3
aws s3 cp "$BACKUP_DIR/backup_$DATE.sql.gz" "s3://$S3_BUCKET/database/"

# Clean old local backups (keep 7 days)
find $BACKUP_DIR -name "backup_*.sql.gz" -mtime +7 -delete
```

### Redis Backup

```bash
# Redis backup configuration
save 900 1      # Save after 900 sec if at least 1 key changed
save 300 10     # Save after 300 sec if at least 10 keys changed
save 60 10000   # Save after 60 sec if at least 10000 keys changed

# Backup script
redis-cli BGSAVE
```

### Disaster Recovery Plan

1. **Regular Backups**: Automated daily backups of database and Redis
2. **Offsite Storage**: Store backups in different region/provider
3. **Recovery Testing**: Monthly recovery drills
4. **Documentation**: Maintain recovery procedures
5. **Monitoring**: Alert on backup failures

## Deployment Checklist

### Pre-deployment
- [ ] All tests passing
- [ ] Environment variables configured
- [ ] Database migrations ready
- [ ] SSL certificates valid
- [ ] Security headers configured
- [ ] Rate limiting enabled
- [ ] Monitoring configured

### Deployment
- [ ] Build Docker image
- [ ] Push to registry
- [ ] Update configuration
- [ ] Deploy to staging
- [ ] Run smoke tests
- [ ] Deploy to production
- [ ] Verify health checks

### Post-deployment
- [ ] Monitor error rates
- [ ] Check performance metrics
- [ ] Verify SignalR connections
- [ ] Test critical paths
- [ ] Update documentation
- [ ] Notify team

## Troubleshooting Deployment

### Common Issues

1. **SignalR Connection Failures**
   - Check WebSocket support in reverse proxy
   - Verify CORS configuration
   - Check firewall rules

2. **Memory Issues**
   - Increase Node.js heap size: `NODE_OPTIONS="--max-old-space-size=4096"`
   - Enable memory monitoring
   - Check for memory leaks

3. **Database Connection Pool**
   - Adjust pool size based on load
   - Monitor active connections
   - Use connection pooling

4. **Redis Connection**
   - Verify Redis is accessible
   - Check authentication
   - Monitor memory usage

## Conclusion

This deployment guide covers the essential aspects of deploying the Conduit WebUI:

1. **Environment Setup** - Proper configuration for each environment
2. **Container Deployment** - Docker and orchestration options
3. **Security Hardening** - SSL, headers, and CSP
4. **Performance Optimization** - Caching and database tuning
5. **Monitoring & Logging** - Observability setup
6. **Backup Strategy** - Data protection and recovery

Follow the deployment checklist and adapt the configurations to your specific infrastructure requirements.