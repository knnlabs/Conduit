# WebUI Dockerfile - Fixed npm config
FROM node:20-alpine

# Install tools
RUN apk add --no-cache python3 make g++ curl

WORKDIR /app

# Test network connectivity
RUN echo "Testing network connectivity..." && \
    curl -I https://registry.npmjs.org || echo "Warning: npm registry may be unreachable" && \
    nslookup registry.npmjs.org || echo "Warning: DNS issues detected"

# Configure npm for better reliability
RUN npm config set registry https://registry.npmjs.org/ && \
    npm config set fetch-retries 10 && \
    npm config set fetch-retry-mintimeout 60000 && \
    npm config set fetch-retry-maxtimeout 300000 && \
    npm config set maxsockets 5

# Copy everything
COPY . .

# Try to build with retries
WORKDIR /app/Clients/Node/Admin
RUN echo "Building Admin SDK..." && \
    (npm install || (echo "Retrying after delay..." && sleep 30 && npm install)) && \
    npm run build

WORKDIR /app/Clients/Node/Core
RUN echo "Building Core SDK..." && \
    (npm install || (echo "Retrying after delay..." && sleep 30 && npm install)) && \
    npm run build

WORKDIR /app/ConduitLLM.WebUI
RUN echo "Building WebUI..." && \
    (npm install || (echo "Retrying after delay..." && sleep 30 && npm install)) && \
    npm run build

# Production
ENV NODE_ENV=production
ENV PORT=3000
ENV HOSTNAME="0.0.0.0"

EXPOSE 3000
CMD ["npm", "start"]