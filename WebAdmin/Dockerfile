# Optimized multi-stage Dockerfile for WebAdmin
# Features: Multi-stage build, standalone output, Alpine base, non-root user, health checks

FROM node:22-alpine AS builder
WORKDIR /app

# IMPORTANT: This is a monorepo where packages depend on each other via file: references
# The WebAdmin package.json contains:
#   "@knn_labs/conduit-admin-client": "file:../SDKs/Node/Admin"
#   "@knn_labs/conduit-gateway-client": "file:../SDKs/Node/Gateway"
# Therefore, we MUST copy the entire monorepo structure before running npm install
# Otherwise npm will timeout trying to fetch these packages from the registry
COPY . .

# Configure npm for ARM64 compatibility (increase timeouts for slower I/O)
RUN npm config set fetch-timeout 600000 && \
    npm config set fetch-retry-mintimeout 20000 && \
    npm config set fetch-retry-maxtimeout 120000 && \
    npm config set registry https://registry.npmjs.org/

# Build Common package first (base dependency for other packages)
WORKDIR /app/SDKs/Node/Common
RUN npm install --no-audit --no-fund --verbose || (cat /root/.npm/_logs/*.log 2>/dev/null && exit 1)
RUN npm run build

# Build Admin SDK next (depends on Common package)
WORKDIR /app/SDKs/Node/Admin
RUN npm install --no-audit --no-fund --verbose || (cat /root/.npm/_logs/*.log 2>/dev/null && exit 1)
RUN npm run build

# Build Gateway SDK next (depends on Common package)
WORKDIR /app/SDKs/Node/Gateway
RUN npm install --no-audit --no-fund --verbose || (cat /root/.npm/_logs/*.log 2>/dev/null && exit 1)
RUN npm run build

# Build WebAdmin last (depends on Admin and Gateway SDKs via file: references)
WORKDIR /app/WebAdmin
# Create public directory if it doesn't exist (Next.js 16 doesn't require it)
RUN mkdir -p public
# npm install here will resolve file: dependencies to the already-built SDKs above
RUN npm install --no-audit --no-fund --verbose || (cat /root/.npm/_logs/*.log 2>/dev/null && exit 1)
RUN npm run build

# Production stage - minimal image using standalone output
# next.config.js has output: 'standalone' which bundles only needed dependencies
FROM node:22-alpine AS runner
WORKDIR /app

# Create non-root user for security
RUN addgroup -g 1001 -S nodejs && \
    adduser -S nextjs -u 1001

# Set production environment
ENV NODE_ENV=production
ENV PORT=3000
ENV HOSTNAME="0.0.0.0"
ENV NEXT_TELEMETRY_DISABLED=1

# Copy standalone output — self-contained server with all dependencies bundled
# No need for node_modules or SDK symlinks; standalone includes everything
# outputFileTracingRoot is set to repo root, so standalone preserves monorepo structure:
#   .next/standalone/WebAdmin/server.js, .next/standalone/SDKs/Node/...
COPY --from=builder --chown=nextjs:nodejs /app/WebAdmin/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /app/WebAdmin/.next/static ./WebAdmin/.next/static
COPY --from=builder --chown=nextjs:nodejs /app/WebAdmin/public ./WebAdmin/public

# Switch to non-root user
USER nextjs

EXPOSE 3000

# Health check for container orchestration
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -q -O /dev/null http://localhost:3000/api/health || exit 1

# Start using the standalone server directly (faster startup than npm start)
# server.js is nested under WebAdmin/ due to outputFileTracingRoot preserving monorepo structure
CMD ["node", "WebAdmin/server.js"]
