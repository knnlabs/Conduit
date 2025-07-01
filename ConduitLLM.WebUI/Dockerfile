# Build stage
FROM node:20-alpine AS builder

WORKDIR /app

# Copy package files
COPY ConduitLLM.WebUI/package*.json ./

# Copy client dependencies
COPY Clients/ ../Clients/

# Install client dependencies
RUN cd ../Clients/Node/Admin && npm install && cd /app
RUN cd ../Clients/Node/Core && npm install && cd /app

# Install WebUI dependencies
RUN npm install

# Copy source code
COPY ConduitLLM.WebUI/ .

# Build the Next.js application
RUN npm run build

# Production stage
FROM node:20-alpine AS runner

WORKDIR /app

# Install dumb-init for proper signal handling
RUN apk add --no-cache dumb-init

# Create non-root user
RUN addgroup -g 1001 -S nodejs
RUN adduser -S nextjs -u 1001

# Set ownership of the app directory
RUN chown nextjs:nodejs /app

# Copy built application from builder with proper ownership
COPY --from=builder --chown=nextjs:nodejs /app/package*.json ./
COPY --from=builder --chown=nextjs:nodejs /app/.next ./.next
COPY --from=builder --chown=nextjs:nodejs /app/next.config.js ./
# Copy src directory for API routes
COPY --from=builder --chown=nextjs:nodejs /app/src ./src
# Copy only the built client libraries (not node_modules)
COPY --from=builder --chown=nextjs:nodejs ../Clients/Node/Admin/dist ../Clients/Node/Admin/dist
COPY --from=builder --chown=nextjs:nodejs ../Clients/Node/Admin/package.json ../Clients/Node/Admin/package.json
COPY --from=builder --chown=nextjs:nodejs ../Clients/Node/Core/dist ../Clients/Node/Core/dist
COPY --from=builder --chown=nextjs:nodejs ../Clients/Node/Core/package.json ../Clients/Node/Core/package.json

# Install only production dependencies as the nextjs user
USER nextjs
RUN npm install --omit=dev

# Expose port
EXPOSE 3000

# Environment variables
ENV NODE_ENV=production
ENV PORT=3000

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=40s --retries=3 \
  CMD node -e "require('http').get('http://localhost:3000/api/core/health', (res) => process.exit(res.statusCode === 200 ? 0 : 1))"

# Start the application
ENTRYPOINT ["dumb-init", "--"]
CMD ["npm", "start"]