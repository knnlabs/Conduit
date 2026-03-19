const path = require('path');

/** @type {import('next').NextConfig} */
const nextConfig = {
  // Standalone output for optimized Docker deployments
  // Produces a self-contained build that doesn't need node_modules at runtime
  output: 'standalone',
  // Monorepo: trace files from repo root so SDK dependencies are included in standalone output
  // This causes standalone to preserve directory structure (WebAdmin/server.js, SDKs/Node/...)
  outputFileTracingRoot: path.resolve(__dirname, '..'),
  experimental: {
    optimizePackageImports: ['@mantine/core', '@mantine/hooks', '@mantine/charts'],
    // Turbopack resolution for monorepo file: dependencies
    turbopack: {
      resolveAlias: {
        '@knn_labs/conduit-admin-client': path.resolve(__dirname, '../SDKs/Node/Admin/dist'),
        '@knn_labs/conduit-gateway-client': path.resolve(__dirname, '../SDKs/Node/Gateway/dist'),
        '@knn_labs/conduit-common': path.resolve(__dirname, '../SDKs/Node/Common/dist'),
      },
    },
  },
  transpilePackages: [
    '@knn_labs/conduit-admin-client',
    '@knn_labs/conduit-gateway-client'
  ],
  // Enable React strict mode for additional checks
  reactStrictMode: true,
  // Image configuration to allow loading from API server
  images: {
    remotePatterns: [
      {
        protocol: 'http',
        hostname: 'localhost',
        port: '5000',
        pathname: '/v1/media/**',
      },
      {
        protocol: 'http',
        hostname: 'api',
        port: '8080',
        pathname: '/v1/media/**',
      },
      {
        protocol: 'https',
        hostname: '**',
      },
    ],
  },
}

module.exports = nextConfig
