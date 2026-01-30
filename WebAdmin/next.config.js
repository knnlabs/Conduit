/** @type {import('next').NextConfig} */
const nextConfig = {
  experimental: {
    optimizePackageImports: ['@mantine/core', '@mantine/hooks', '@mantine/charts'],
  },
  transpilePackages: [
    '@knn_labs/conduit-admin-client',
    '@knn_labs/conduit-gateway-client'
  ],
  // Enable source maps for better debugging
  productionBrowserSourceMaps: true,
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
