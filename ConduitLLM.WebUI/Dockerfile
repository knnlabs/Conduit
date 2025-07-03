# Optimized multi-stage Dockerfile for ConduitLLM.WebUI
# Features: Multi-stage build, Alpine base, non-root user, health checks

FROM node:20-alpine AS builder
WORKDIR /app

# Copy the entire monorepo to maintain file path dependencies
COPY . .

# Build Admin SDK
WORKDIR /app/Clients/Node/Admin
RUN npm install --no-audit --no-fund && npm run build

# Build Core SDK
WORKDIR /app/Clients/Node/Core
RUN npm install --no-audit --no-fund && npm run build

# Build WebUI
WORKDIR /app/ConduitLLM.WebUI
# Create public directory if it doesn't exist (Next.js 15 doesn't require it)
RUN mkdir -p public
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

# Copy the built SDK packages that WebUI depends on
COPY --from=builder --chown=nextjs:nodejs /app/Clients/Node/Admin /app/Clients/Node/Admin
COPY --from=builder --chown=nextjs:nodejs /app/Clients/Node/Core /app/Clients/Node/Core

# Switch to non-root user
USER nextjs

EXPOSE 3000

# Health check for container orchestration
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -q -O /dev/null http://localhost:3000/api/health || exit 1

CMD ["npm", "start"]