# Optimized multi-stage Dockerfile for WebAdmin
# Features: Multi-stage build, Alpine base, non-root user, health checks

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
# The WebAdmin's npm install will symlink to the local Admin and Gateway packages
WORKDIR /app/WebAdmin
# Create public directory if it doesn't exist (Next.js 15 doesn't require it)
RUN mkdir -p public
# npm install here will resolve file: dependencies to the already-built SDKs above
RUN npm install --no-audit --no-fund --verbose || (cat /root/.npm/_logs/*.log 2>/dev/null && exit 1)
RUN npm run build

# Production stage - smaller final image
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

# Copy only what's needed from builder
COPY --from=builder --chown=nextjs:nodejs /app/WebAdmin/package*.json ./
COPY --from=builder --chown=nextjs:nodejs /app/WebAdmin/.next ./.next
COPY --from=builder --chown=nextjs:nodejs /app/WebAdmin/node_modules ./node_modules
COPY --from=builder --chown=nextjs:nodejs /app/WebAdmin/public ./public

# IMPORTANT: We must also copy the SDK packages because WebAdmin's node_modules contains
# symlinks to these local packages (due to file: references in package.json)
# Without these, the runtime will fail to resolve the packages
COPY --from=builder --chown=nextjs:nodejs /app/SDKs/Node/Common /app/SDKs/Node/Common
COPY --from=builder --chown=nextjs:nodejs /app/SDKs/Node/Admin /app/SDKs/Node/Admin
COPY --from=builder --chown=nextjs:nodejs /app/SDKs/Node/Gateway /app/SDKs/Node/Gateway

# Switch to non-root user
USER nextjs

EXPOSE 3000

# Health check for container orchestration
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -q -O /dev/null http://localhost:3000/api/health || exit 1

# Start the application
CMD ["npm", "start"]