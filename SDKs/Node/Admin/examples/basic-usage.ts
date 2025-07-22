import { ConduitAdminClient, ValidationError, NotFoundError } from '@conduit/admin-client';

async function main() {
  // Initialize the client
  const client = new ConduitAdminClient({
    masterKey: process.env.CONDUIT_MASTER_KEY!,
    adminApiUrl: process.env.CONDUIT_ADMIN_API_URL || 'http://localhost:5002',
  });

  try {
    // Example 1: Create a new virtual key
    console.log('Creating a new virtual key...');
    const { virtualKey, keyInfo } = await client.virtualKeys.create({
      keyName: 'Development Key',
      allowedModels: 'gpt-4,gpt-3.5-turbo,claude-3-opus',
      maxBudget: 50,
      budgetDuration: 'Monthly',
      metadata: JSON.stringify({
        environment: 'development',
        team: 'engineering',
      }),
      rateLimitRpm: 60,
      rateLimitRpd: 1000,
    });

    console.log(`✓ Created key: ${virtualKey}`);
    console.log(`  Key ID: ${keyInfo.id}`);
    console.log(`  Budget: $${keyInfo.maxBudget} ${keyInfo.budgetDuration}`);

    // Example 2: List all virtual keys
    console.log('\nListing virtual keys...');
    const keysList = await client.virtualKeys.list({
      pageSize: 10,
      isEnabled: true,
      sortBy: {
        field: 'createdAt',
        direction: 'desc',
      },
    });

    console.log(`✓ Found ${keysList.totalCount} keys`);
    keysList.items.forEach((key) => {
      console.log(`  - ${key.keyName} (ID: ${key.id})`);
      console.log(`    Budget: $${key.currentSpend}/$${key.maxBudget}`);
      console.log(`    Models: ${key.allowedModels}`);
    });

    // Example 3: Validate a key
    console.log('\nValidating a key...');
    const validation = await client.virtualKeys.validate(virtualKey);
    
    if (validation.isValid) {
      console.log('✓ Key is valid');
      console.log(`  Name: ${validation.keyName}`);
      console.log(`  Budget remaining: $${validation.budgetRemaining}`);
      console.log(`  Allowed models: ${validation.allowedModels?.join(', ')}`);
    } else {
      console.log('✗ Key is invalid');
      console.log(`  Reason: ${validation.reason}`);
    }

    // Example 4: Update a key
    console.log('\nUpdating key budget...');
    await client.virtualKeys.update(keyInfo.id, {
      maxBudget: 100,
      metadata: JSON.stringify({
        environment: 'development',
        team: 'engineering',
        updated: new Date().toISOString(),
      }),
    });
    console.log('✓ Key updated successfully');

    // Example 5: Check budget availability
    console.log('\nChecking budget availability...');
    const budgetCheck = await client.virtualKeys.checkBudget(keyInfo.id, 10);
    console.log(`✓ Budget check complete`);
    console.log(`  Has available budget: ${budgetCheck.hasAvailableBudget}`);
    console.log(`  Available: $${budgetCheck.availableBudget}`);
    console.log(`  Current spend: $${budgetCheck.currentSpend}`);

    // Example 6: Get validation info
    console.log('\nGetting detailed validation info...');
    const validationInfo = await client.virtualKeys.getValidationInfo(keyInfo.id);
    console.log(`✓ Validation info retrieved`);
    console.log(`  Key name: ${validationInfo.keyName}`);
    console.log(`  Is valid: ${validationInfo.isValid}`);
    if (validationInfo.validationErrors.length > 0) {
      console.log(`  Errors: ${validationInfo.validationErrors.join(', ')}`);
    }

    // Example 7: Search for keys
    console.log('\nSearching for keys...');
    const searchResults = await client.virtualKeys.search('Development');
    console.log(`✓ Found ${searchResults.length} matching keys`);
    searchResults.forEach((key) => {
      console.log(`  - ${key.keyName} (ID: ${key.id})`);
    });

    // Example 8: Reset spend (useful for testing)
    console.log('\nResetting key spend...');
    await client.virtualKeys.resetSpend(keyInfo.id);
    console.log('✓ Spend reset successfully');

    // Example 9: Delete a key (cleanup)
    console.log('\nCleaning up - deleting test key...');
    await client.virtualKeys.deleteById(keyInfo.id);
    console.log('✓ Key deleted successfully');

  } catch (error) {
    if (error instanceof ValidationError) {
      console.error('Validation error:', error.message);
      console.error('Details:', error.details);
    } else if (error instanceof NotFoundError) {
      console.error('Resource not found:', error.message);
    } else {
      console.error('Unexpected error:', error);
    }
  }
}

// Run the examples
main().catch(console.error);