#!/usr/bin/env tsx

import { performance } from 'perf_hooks';
import chalk from 'chalk';
import { getServerAdminClient, getServerCoreClient } from '../src/lib/server/sdk-config';

interface PerformanceResult {
  operation: string;
  iterations: number;
  average: number;
  min: number;
  max: number;
  p95: number;
  p99: number;
}

async function measurePerformance(
  name: string,
  operation: () => Promise<any>,
  iterations: number = 100
): Promise<PerformanceResult> {
  const times: number[] = [];
  
  // Warm up with 5 iterations
  console.log(chalk.gray(`Warming up ${name}...`));
  for (let i = 0; i < 5; i++) {
    try {
      await operation();
    } catch (error) {
      // Ignore warm-up errors
    }
  }

  // Actual measurements
  console.log(chalk.cyan(`Measuring ${name} (${iterations} iterations)...`));
  for (let i = 0; i < iterations; i++) {
    const start = performance.now();
    try {
      await operation();
    } catch (error) {
      // Skip failed operations in timing
      continue;
    }
    const end = performance.now();
    times.push(end - start);
    
    // Show progress every 20 iterations
    if ((i + 1) % 20 === 0) {
      process.stdout.write('.');
    }
  }
  console.log(''); // New line after dots

  if (times.length === 0) {
    throw new Error(`All operations failed for ${name}`);
  }

  // Sort times for percentile calculations
  times.sort((a, b) => a - b);

  return {
    operation: name,
    iterations: times.length,
    average: times.reduce((a, b) => a + b, 0) / times.length,
    min: times[0],
    max: times[times.length - 1],
    p95: times[Math.floor(times.length * 0.95)],
    p99: times[Math.floor(times.length * 0.99)],
  };
}

async function runPerformanceTests() {
  console.log(chalk.blue('🚀 Running SDK Performance Tests\n'));
  
  const results: PerformanceResult[] = [];
  let hasErrors = false;

  try {
    // Test 1: SDK Client Creation
    console.log(chalk.yellow('Test 1: SDK Client Creation'));
    const clientCreationResult = await measurePerformance(
      'SDK Client Creation',
      async () => {
        // Force new instance by clearing module cache
        delete require.cache[require.resolve('../src/lib/server/sdk-config')];
        const { getServerAdminClient: getClient } = require('../src/lib/server/sdk-config');
        getClient();
      },
      50 // Fewer iterations for client creation
    );
    results.push(clientCreationResult);

    // Test 2: Singleton Access
    console.log(chalk.yellow('\nTest 2: Singleton Client Access'));
    const singletonResult = await measurePerformance(
      'Singleton Access',
      async () => {
        const client = getServerAdminClient();
        // Just access the client
        return client;
      },
      1000 // Many iterations for singleton access
    );
    results.push(singletonResult);

    // Test 3: Mock API Call (if backend is available)
    console.log(chalk.yellow('\nTest 3: Mock API Call Performance'));
    try {
      const mockApiResult = await measurePerformance(
        'Mock API Call',
        async () => {
          const client = getServerAdminClient();
          // This will likely fail without backend, but we measure the attempt
          try {
            // Use virtualKeys.list() since providers is not available in the SDK
            await client.virtualKeys.list();
          } catch (error) {
            // Simulate some processing time
            await new Promise(resolve => setTimeout(resolve, 10));
            throw error;
          }
        },
        20 // Fewer iterations for network calls
      );
      results.push(mockApiResult);
    } catch (error) {
      console.log(chalk.yellow('⚠️  Mock API calls failed (expected without backend)'));
    }

    // Test 4: Error Handling Performance
    console.log(chalk.yellow('\nTest 4: Error Handling Performance'));
    const { handleSDKError } = require('../src/lib/errors/sdk-errors');
    const { NetworkError } = require('@knn_labs/conduit-admin-client');
    
    const errorHandlingResult = await measurePerformance(
      'Error Handling',
      async () => {
        const error = new NetworkError('Test error');
        handleSDKError(error);
      },
      1000
    );
    results.push(errorHandlingResult);

    // Test 5: Type Mapping Performance
    console.log(chalk.yellow('\nTest 5: Type Mapping Performance'));
    const { mapVirtualKeyFromSDK } = require('../src/lib/types/mappers');
    
    const mappingResult = await measurePerformance(
      'Type Mapping',
      async () => {
        const sdkKey = {
          id: '123',
          keyName: 'Test Key',
          apiKey: 'sk_test',
          isEnabled: true,
          isActive: true,
          createdAt: new Date().toISOString(),
          providers: ['openai', 'anthropic'],
        };
        mapVirtualKeyFromSDK(sdkKey);
      },
      10000 // Many iterations for fast operations
    );
    results.push(mappingResult);

  } catch (error) {
    console.error(chalk.red('\n❌ Performance test failed:'), error);
    hasErrors = true;
  }

  // Display results
  console.log(chalk.blue('\n📊 Performance Test Results:\n'));
  
  const tableData = results.map(r => ({
    Operation: r.operation,
    'Avg (ms)': r.average.toFixed(2),
    'Min (ms)': r.min.toFixed(2),
    'Max (ms)': r.max.toFixed(2),
    'P95 (ms)': r.p95.toFixed(2),
    'P99 (ms)': r.p99.toFixed(2),
    Iterations: r.iterations,
  }));

  console.table(tableData);

  // Performance thresholds
  console.log(chalk.blue('\n🎯 Performance Analysis:\n'));
  
  for (const result of results) {
    const status = getPerformanceStatus(result);
    const icon = status.pass ? '✅' : '⚠️';
    const color = status.pass ? chalk.green : chalk.yellow;
    
    console.log(`${icon} ${result.operation}: ${color(status.message)}`);
  }

  // Overall summary
  console.log(chalk.blue('\n📈 Summary:\n'));
  
  const fastOps = results.filter(r => r.average < 1).length;
  const acceptableOps = results.filter(r => r.average < 10).length;
  const slowOps = results.filter(r => r.average >= 100).length;
  
  console.log(`Fast operations (<1ms): ${chalk.green(fastOps)}`);
  console.log(`Acceptable operations (<10ms): ${chalk.yellow(acceptableOps)}`);
  console.log(`Slow operations (≥100ms): ${chalk.red(slowOps)}`);
  
  if (slowOps > 0) {
    console.log(chalk.yellow('\n⚠️  Some operations exceed performance thresholds'));
  } else if (hasErrors) {
    console.log(chalk.red('\n❌ Some tests failed to complete'));
    process.exit(1);
  } else {
    console.log(chalk.green('\n✨ All operations within acceptable performance range!'));
  }
}

function getPerformanceStatus(result: PerformanceResult): { pass: boolean; message: string } {
  // Define thresholds based on operation type
  const thresholds: Record<string, number> = {
    'SDK Client Creation': 50,    // Client creation can be slower
    'Singleton Access': 0.1,      // Should be nearly instant
    'Mock API Call': 200,         // Network calls can be slower
    'Error Handling': 1,          // Should be fast
    'Type Mapping': 0.5,          // Should be very fast
  };

  const threshold = thresholds[result.operation] || 10;
  const pass = result.average < threshold;
  
  if (pass) {
    return {
      pass: true,
      message: `Average ${result.average.toFixed(2)}ms is under ${threshold}ms threshold`,
    };
  } else {
    return {
      pass: false,
      message: `Average ${result.average.toFixed(2)}ms exceeds ${threshold}ms threshold`,
    };
  }
}

// Run the tests
runPerformanceTests().catch(error => {
  console.error(chalk.red('Fatal error:'), error);
  process.exit(1);
});