/**
 * Example: Using bulk discovery APIs for efficient model capability testing
 */

import { ConduitAdminClient } from '../src';

async function main() {
  // Initialize the admin client
  const client = ConduitAdminClient.fromEnvironment();

  try {
    console.log('🔍 Testing bulk discovery APIs...\n');

    // Example 1: Bulk capability testing
    console.log('1. Testing bulk model capabilities...');
    const capabilityTestRequest = {
      tests: [
        { model: 'gpt-4', capability: 'Chat' },
        { model: 'gpt-4', capability: 'Vision' },
        { model: 'dall-e-3', capability: 'ImageGeneration' },
        { model: 'claude-3-sonnet', capability: 'Chat' },
        { model: 'claude-3-sonnet', capability: 'Vision' },
        { model: 'whisper-1', capability: 'AudioTranscription' }
      ]
    };

    const capabilityResults = await client.discovery.testBulkCapabilities(capabilityTestRequest);
    
    console.log(`✅ Tested ${capabilityResults.totalTests} capabilities in ${capabilityResults.executionTimeMs}ms`);
    console.log(`   Successful: ${capabilityResults.successfulTests}, Failed: ${capabilityResults.failedTests}`);
    
    // Show results for each test
    capabilityResults.results.forEach(result => {
      const status = result.supported ? '✅' : '❌';
      console.log(`   ${status} ${result.model} -> ${result.capability}`);
      if (result.error) {
        console.log(`      Error: ${result.error}`);
      }
    });

    console.log('\n2. Bulk model discovery...');
    
    // Example 2: Bulk model discovery
    const modelDiscoveryRequest = {
      models: ['gpt-4', 'gpt-3.5-turbo', 'claude-3-sonnet', 'dall-e-3', 'unknown-model'],
      includeCapabilities: true
    };

    const discoveryResults = await client.discovery.getBulkModels(modelDiscoveryRequest);
    
    console.log(`🔍 Discovered ${discoveryResults.foundModels}/${discoveryResults.totalRequested} models in ${discoveryResults.executionTimeMs}ms`);
    
    // Show results for each model
    discoveryResults.results.forEach(result => {
      const status = result.found ? '✅' : '❌';
      console.log(`   ${status} ${result.model}`);
      if (result.found) {
        console.log(`      Provider: ${result.provider}`);
        console.log(`      Capabilities: ${result.capabilities?.join(', ') || 'None'}`);
      } else if (result.error) {
        console.log(`      Error: ${result.error}`);
      }
    });

    console.log('\n3. Convenience method: Test multiple capabilities for one model...');
    
    // Example 3: Convenience method - test multiple capabilities for one model
    const gpt4Capabilities = await client.discovery.testModelCapabilities('gpt-4', [
      'Chat', 'Vision', 'FunctionCalling', 'ImageGeneration'
    ]);

    console.log(`🔍 Tested GPT-4 capabilities in ${gpt4Capabilities.executionTimeMs}ms:`);
    gpt4Capabilities.results.forEach(result => {
      const status = result.supported ? '✅' : '❌';
      console.log(`   ${status} ${result.capability}`);
    });

    console.log('\n4. Single model capability test...');
    
    // Example 4: Single capability test (cached)
    const singleTest = await client.discovery.testModelCapability('claude-3-sonnet', 'Chat');
    console.log(`✅ Claude-3-Sonnet Chat capability: ${singleTest.supported ? 'Supported' : 'Not supported'}`);

    console.log('\n5. Get all available models...');
    
    // Example 5: Get all models
    const allModels = await client.discovery.getAllModels();
    console.log(`📋 Found ${allModels.totalModels} total models (${allModels.availableModels} available)`);
    
    // Group models by provider
    const modelsByProvider = allModels.models.reduce((acc, model) => {
      if (!acc[model.provider]) {
        acc[model.provider] = [];
      }
      acc[model.provider].push(model.name);
      return acc;
    }, {} as Record<string, string[]>);

    Object.entries(modelsByProvider).forEach(([provider, models]) => {
      console.log(`   ${provider}: ${models.slice(0, 3).join(', ')}${models.length > 3 ? ` (+${models.length - 3} more)` : ''}`);
    });

    console.log('\n6. Get provider-specific models...');
    
    // Example 6: Get models for a specific provider
    const openaiModels = await client.discovery.getProviderModels('openai');
    console.log(`🤖 OpenAI has ${openaiModels.totalModels} models available`);
    openaiModels.models.slice(0, 5).forEach(model => {
      console.log(`   • ${model.name} (${model.capabilities?.join(', ') || 'No capabilities'})`);
    });

    console.log('\n✅ Bulk discovery examples completed successfully!');

  } catch (error) {
    console.error('❌ Error:', error);
    
    if (error instanceof Error) {
      console.error('Details:', error.message);
      if ('response' in error) {
        console.error('Response:', (error as any).response?.data);
      }
    }
  }
}

// Handle async execution
main().catch(console.error);

// Export for use in other examples
export { main as runBulkDiscoveryExample };