/** @type {import('next').NextConfig} */
const nextConfig = {
  experimental: {
    optimizePackageImports: ['@mantine/core', '@mantine/hooks', '@mantine/charts'],
  },
  transpilePackages: [
    '@knn_labs/conduit-admin-client',
    '@knn_labs/conduit-core-client'
  ],
  // Enable source maps for better debugging
  productionBrowserSourceMaps: true,
  // Enable React strict mode for additional checks
  reactStrictMode: true,
  // ESLint configuration
  eslint: {
    // Warning: This allows production builds to successfully complete even if
    // your project has ESLint errors.
    ignoreDuringBuilds: true,
  },
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
  // Enhanced webpack configuration for hot reload
  webpack: (config, { dev, isServer }) => {
    config.externals.push({
      'utf-8-validate': 'commonjs utf-8-validate',
      'bufferutil': 'commonjs bufferutil',
    });
    
    // Better source maps for debugging
    if (dev && !isServer) {
      config.devtool = 'eval-source-map';
    }
    
    // Disable minification and enable React development mode
    config.optimization = {
      ...config.optimization,
      minimize: false,
      minimizer: [],
    };
    
    // Force React to run in development mode for better debugging
    const webpack = require('webpack');
    config.plugins.push(
      new webpack.DefinePlugin({
        'process.env.NODE_ENV': JSON.stringify('development'),
        '__REACT_DEVTOOLS_GLOBAL_HOOK__': '({ isDisabled: false })',
      })
    );
    
    // Disable optimization in development
    if (dev) {
      config.optimization = {
        ...config.optimization,
        minimize: false,
        minimizer: [],
        splitChunks: false,
        runtimeChunk: false,
      };
      
      // Enhanced hot reload configuration
      config.watchOptions = {
        poll: 1000,
        aggregateTimeout: 300,
        ignored: ['**/node_modules', '**/.next'],
      };
    }
    
    return config;
  },
}

module.exports = nextConfig
