# =============================================================================
# ConduitLLM WebUI Dockerfile
# =============================================================================
# Production-grade Next.js application with security hardening and optimization
# Features: Multi-stage build, layer caching, non-root user, minimal attack surface
# =============================================================================

# -----------------------------------------------------------------------------
# Stage 1: Dependencies
# -----------------------------------------------------------------------------
FROM node:20-alpine AS deps

# Install build dependencies
RUN apk add --no-cache libc6-compat python3 make g++

WORKDIR /app

# Copy package files for better caching
COPY ConduitLLM.WebUI/package*.json ./

# Copy SDK dependencies
COPY Clients/Node/Admin/package*.json ../Clients/Node/Admin/
COPY Clients/Node/Core/package*.json ../Clients/Node/Core/

# Install SDK dependencies (using npm install since they don't have lock files)
WORKDIR /Clients/Node/Admin
COPY Clients/Node/Admin/ ./
RUN npm install --production

WORKDIR /Clients/Node/Core
COPY Clients/Node/Core/ ./
RUN npm install --production

# Install WebUI dependencies with lock file
WORKDIR /app
RUN npm ci

# -----------------------------------------------------------------------------
# Stage 2: Builder
# -----------------------------------------------------------------------------
FROM node:20-alpine AS builder

# Install build dependencies
RUN apk add --no-cache libc6-compat

WORKDIR /app

# Copy dependencies from previous stage
COPY --from=deps /app/node_modules ./node_modules
COPY --from=deps /Clients ../Clients

# Copy source code
COPY ConduitLLM.WebUI/ .

# Build SDKs
WORKDIR /Clients/Node/Admin
RUN npm run build || echo "Admin SDK build skipped"

WORKDIR /Clients/Node/Core
RUN npm run build || echo "Core SDK build skipped"

# Build Next.js application
WORKDIR /app
ENV NEXT_TELEMETRY_DISABLED=1
ENV NODE_ENV=production

# Build with standalone output for smaller image
RUN npm run build

# -----------------------------------------------------------------------------
# Stage 3: Production Runner
# -----------------------------------------------------------------------------
FROM node:20-alpine AS runner

# Install only runtime dependencies
RUN apk add --no-cache \
    dumb-init \
    curl \
    && addgroup -g 1001 -S nodejs \
    && adduser -S nextjs -u 1001 -G nodejs

WORKDIR /app

# Create necessary directories with proper ownership
RUN mkdir -p /app/.next \
    && chown -R nextjs:nodejs /app

# Copy application files with correct ownership
COPY --from=builder --chown=nextjs:nodejs /app/package*.json ./
COPY --from=builder --chown=nextjs:nodejs /app/.next ./.next
COPY --from=builder --chown=nextjs:nodejs /app/next.config.js ./
COPY --from=builder --chown=nextjs:nodejs /app/public ./public 2>/dev/null || true
COPY --from=builder --chown=nextjs:nodejs /app/src ./src

# Copy production dependencies (instead of full node_modules)
COPY --from=deps --chown=nextjs:nodejs /app/node_modules ./node_modules

# Copy SDK builds
COPY --from=builder --chown=nextjs:nodejs /Clients/Node/Admin ../Clients/Node/Admin
COPY --from=builder --chown=nextjs:nodejs /Clients/Node/Core ../Clients/Node/Core

# Configure runtime environment
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME="0.0.0.0"

# Security: Run as non-root user
USER nextjs

# Expose port
EXPOSE 3000

# Health check using lightweight method
HEALTHCHECK --interval=30s --timeout=3s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:3000/api/core/health || exit 1

# Use dumb-init to handle signals properly
ENTRYPOINT ["dumb-init", "--"]

# Start Next.js server
CMD ["npm", "start"]

# =============================================================================
# Build-time arguments and labels
# =============================================================================
ARG BUILD_DATE
ARG VERSION
ARG VCS_REF

LABEL org.opencontainers.image.created="${BUILD_DATE}" \
      org.opencontainers.image.title="Conduit WebUI" \
      org.opencontainers.image.description="Modern web interface for Conduit LLM Gateway" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${VCS_REF}" \
      org.opencontainers.image.vendor="KNN Labs" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.source="https://github.com/knnlabs/Conduit" \
      org.opencontainers.image.documentation="https://github.com/knnlabs/Conduit/tree/main/ConduitLLM.WebUI" \
      org.opencontainers.image.base.name="node:20-alpine" \
      maintainer="KNN Labs <support@knnlabs.com>"