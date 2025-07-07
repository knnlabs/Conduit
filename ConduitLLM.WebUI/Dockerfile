# Optimized multi-stage Dockerfile for ConduitLLM.WebUI
# Features: Multi-stage build, Alpine base, non-root user, health checks

FROM node:20-alpine AS builder
WORKDIR /app

# IMPORTANT: This is a monorepo where packages depend on each other via file: references
# The WebUI package.json contains:
#   "@knn_labs/conduit-admin-client": "file:../Clients/Node/Admin"
#   "@knn_labs/conduit-core-client": "file:../Clients/Node/Core"
# Therefore, we MUST copy the entire monorepo structure before running npm install
# Otherwise npm will timeout trying to fetch these packages from the registry
COPY . .

# Build Admin SDK first (no external dependencies on other local packages)
WORKDIR /app/Clients/Node/Admin
RUN npm install --no-audit --no-fund && npm run build

# Build Core SDK next (no external dependencies on other local packages)
WORKDIR /app/Clients/Node/Core
RUN npm install --no-audit --no-fund && npm run build

# Build WebUI last (depends on Admin and Core SDKs via file: references)
# The WebUI's npm install will symlink to the local Admin and Core packages
WORKDIR /app/ConduitLLM.WebUI
# Create public directory if it doesn't exist (Next.js 15 doesn't require it)
RUN mkdir -p public
# npm install here will resolve file: dependencies to the already-built SDKs above
RUN npm install --no-audit --no-fund && npm run build

# Production stage - smaller final image
FROM node:20-alpine AS runner
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
COPY --from=builder --chown=nextjs:nodejs /app/ConduitLLM.WebUI/package*.json ./
COPY --from=builder --chown=nextjs:nodejs /app/ConduitLLM.WebUI/.next ./.next
COPY --from=builder --chown=nextjs:nodejs /app/ConduitLLM.WebUI/node_modules ./node_modules
COPY --from=builder --chown=nextjs:nodejs /app/ConduitLLM.WebUI/public ./public

# IMPORTANT: We must also copy the SDK packages because WebUI's node_modules contains
# symlinks to these local packages (due to file: references in package.json)
# Without these, the runtime will fail to resolve @knn_labs/conduit-admin-client and @knn_labs/conduit-core-client
COPY --from=builder --chown=nextjs:nodejs /app/Clients/Node/Admin /app/Clients/Node/Admin
COPY --from=builder --chown=nextjs:nodejs /app/Clients/Node/Core /app/Clients/Node/Core

# Switch to non-root user
USER nextjs

EXPOSE 3000

# Health check for container orchestration
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -q -O /dev/null http://localhost:3000/api/health || exit 1

CMD ["npm", "start"]