/** @type {import('next').NextConfig} */
const nextConfig = {
  // Standalone output for optimized Docker deployments
  // Produces a self-contained build that doesn't need node_modules at runtime
  output: 'standalone',
  experimental: {
    optimizePackageImports: ['@mantine/core', '@mantine/hooks', '@mantine/charts'],
  },
  // Enable React strict mode for additional checks
  reactStrictMode: true,
  // Emit browser source maps for restricted build artifacts/error-symbolication.
  // The edge proxy blocks direct .map requests in production.
  productionBrowserSourceMaps: true,
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
