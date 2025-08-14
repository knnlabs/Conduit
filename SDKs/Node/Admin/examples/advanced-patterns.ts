import { ConduitAdminClient } from '@conduit/admin-client';

async function advancedExamples() {
  const client = new ConduitAdminClient({
    masterKey: process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY!,
    adminApiUrl: process.env.CONDUIT_ADMIN_API_URL || 'http://localhost:5002',
    options: {
      timeout: 30000,
      retries: {
        maxRetries: 5,
        retryDelay: 1000,
        retryCondition: (error) => {
          // Custom retry logic
          return error.response?.status >= 500 || error.code === 'ECONNABORTED';
        },
      },
      logger: {
        debug: (msg, ...args) => console.debug(`[DEBUG] ${msg}`, ...args),
        info: (msg, ...args) => console.info(`[INFO] ${msg}`, ...args),
        warn: (msg, ...args) => console.warn(`[WARN] ${msg}`, ...args),
        error: (msg, ...args) => console.error(`[ERROR] ${msg}`, ...args),
      },
    },
  });

  // Example 1: Cost Analytics Dashboard
  console.log('=== Cost Analytics Dashboard ===');
  
  const today = new Date();
  const startOfMonth = new Date(today.getFullYear(), today.getMonth(), 1);
  const dateRange = {
    startDate: startOfMonth.toISOString(),
    endDate: today.toISOString(),
  };

  // Get cost summary
  const costSummary = await client.analytics.getCostSummary(dateRange);
  console.log(`Total cost this month: $${costSummary.totalCost.toFixed(2)}`);
  console.log(`Total tokens: ${costSummary.totalInputTokens + costSummary.totalOutputTokens}`);

  // Get cost breakdown by model
  const costByModel = await client.analytics.getCostByModel(dateRange);
  console.log('\nTop 5 most expensive models:');
  costByModel.models
    .sort((a, b) => b.totalCost - a.totalCost)
    .slice(0, 5)
    .forEach((model) => {
      console.log(`  ${model.modelId}: $${model.totalCost.toFixed(2)} (${model.totalRequests} requests)`);
    });

  // Get cost trend
  const costTrend = await client.analytics.getCostByPeriod(dateRange, 'day');
  console.log(`\nCost trend: ${costTrend.trend} (${costTrend.trendPercentage.toFixed(1)}%)`);

  // Example 2: IP Filter Management with Bulk Operations
  console.log('\n=== IP Filter Management ===');

  // Get current IP filter settings
  const ipSettings = await client.ipFilters.getSettings();
  console.log(`IP Filtering: ${ipSettings.isEnabled ? 'Enabled' : 'Disabled'}`);
  console.log(`Filter mode: ${ipSettings.filterMode}`);

  // Create multiple IP filters
  const companyRanges = [
    { name: 'Office Network', cidr: '192.168.1.0/24' },
    { name: 'VPN Range', cidr: '10.0.0.0/16' },
    { name: 'Cloud Servers', cidr: '172.16.0.0/12' },
  ];

  for (const range of companyRanges) {
    await client.ipFilters.createAllowFilter(
      range.name,
      range.cidr,
      'Company infrastructure'
    );
    console.log(`✓ Created allow filter: ${range.name}`);
  }

  // Check if an IP is allowed
  const testIp = '192.168.1.100';
  const ipCheck = await client.ipFilters.checkIp(testIp);
  console.log(`\nIP ${testIp} is ${ipCheck.isAllowed ? 'allowed' : 'blocked'}`);
  if (ipCheck.matchedFilter) {
    console.log(`Matched filter: ${ipCheck.matchedFilter}`);
  }

  // Example 3: Provider Health Monitoring
  console.log('\n=== Provider Health Monitoring ===');

  // Get all provider health status
  const healthSummary = await client.providers.getHealthStatus();
  console.log(`Total providers: ${healthSummary.totalProviders}`);
  console.log(`Healthy: ${healthSummary.healthyProviders}`);
  console.log(`Unhealthy: ${healthSummary.unhealthyProviders}`);

  // Configure health checks for critical providers
  const criticalProviders = ['openai', 'anthropic'];
  for (const provider of criticalProviders) {
    await client.providers.updateHealthConfiguration(provider, {
      isEnabled: true,
      checkIntervalSeconds: 60,
      timeoutSeconds: 10,
      unhealthyThreshold: 3,
      healthyThreshold: 2,
    });
    console.log(`✓ Configured health monitoring for ${provider}`);
  }

  // Example 4: Model Cost Optimization
  console.log('\n=== Model Cost Optimization ===');

  // Compare costs between models
  const modelsToCompare = ['gpt-4', 'gpt-3.5-turbo', 'claude-3-opus'];
  const testScenario = {
    inputTokens: 1000,
    outputTokens: 500,
  };

  const costComparisons = await Promise.all(
    modelsToCompare.map(async (modelId) => {
      try {
        const cost = await client.modelCosts.calculateCost(
          modelId,
          testScenario.inputTokens,
          testScenario.outputTokens
        );
        return { modelId, cost };
      } catch (error) {
        console.warn(`No cost data for ${modelId}`);
        return null;
      }
    })
  );

  console.log('\nCost comparison for 1000 input + 500 output tokens:');
  costComparisons
    .filter(Boolean)
    .sort((a, b) => a!.cost.totalCost - b!.cost.totalCost)
    .forEach((comparison) => {
      console.log(`  ${comparison!.modelId}: $${comparison!.cost.totalCost.toFixed(4)}`);
    });

  // Example 5: System Backup and Maintenance
  console.log('\n=== System Maintenance ===');

  // Create a backup
  console.log('Creating system backup...');
  const backup = await client.system.createBackup({
    description: 'Pre-update backup',
    includeKeys: true,
    includeProviders: true,
    includeSettings: true,
    includeLogs: false,
  });
  console.log(`✓ Backup created: ${backup.filename} (${(backup.size / 1024 / 1024).toFixed(2)} MB)`);

  // Run maintenance tasks
  await client.virtualKeys.performMaintenance({
    cleanupExpiredKeys: true,
    resetDailyBudgets: true,
  });
  console.log('✓ Virtual key maintenance completed');

  // Get system health
  const health = await client.system.getHealth();
  console.log(`\nSystem health: ${health.status}`);
  Object.entries(health.checks).forEach(([check, result]) => {
    console.log(`  ${check}: ${result.status}`);
  });

  // Example 6: Advanced Settings Management
  console.log('\n=== Advanced Settings ===');

  // Configure router settings
  await client.settings.updateRouterConfiguration({
    routingStrategy: 'least-cost',
    fallbackEnabled: true,
    maxRetries: 3,
    retryDelay: 1000,
    loadBalancingEnabled: true,
    circuitBreakerEnabled: true,
    circuitBreakerThreshold: 5,
    circuitBreakerDuration: 60000,
  });
  console.log('✓ Router configuration updated');

  // Set custom settings
  await client.settings.setSetting('RATE_LIMIT_WINDOW', '60', {
    description: 'Rate limit window in seconds',
    dataType: 'number',
    category: 'RateLimiting',
  });

  await client.settings.setSetting('MAX_TOKENS_PER_REQUEST', '4096', {
    description: 'Maximum tokens allowed per request',
    dataType: 'number',
    category: 'RequestLimits',
  });

  console.log('✓ Custom settings configured');

  // Example 7: Request Log Analysis
  console.log('\n=== Request Log Analysis ===');

  // Find failed requests
  const failedRequests = await client.analytics.getRequestLogs({
    status: 'error',
    startDate: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(),
    endDate: new Date().toISOString(),
    pageSize: 10,
  });

  if (failedRequests.totalCount > 0) {
    console.log(`Found ${failedRequests.totalCount} failed requests in the last 24 hours`);
    failedRequests.items.forEach((log) => {
      console.log(`  [${log.timestamp}] ${log.model} - ${log.errorMessage}`);
    });
  }

  // Find expensive requests
  const expensiveRequests = await client.analytics.getRequestLogs({
    minCost: 1.0,
    sortBy: { field: 'cost', direction: 'desc' },
    pageSize: 5,
  });

  if (expensiveRequests.items.length > 0) {
    console.log('\nMost expensive requests:');
    expensiveRequests.items.forEach((log) => {
      console.log(`  $${log.cost.toFixed(2)} - ${log.model} (${log.inputTokens + log.outputTokens} tokens)`);
    });
  }
}

// Run the examples
advancedExamples().catch(console.error);