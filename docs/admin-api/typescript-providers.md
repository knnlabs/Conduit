# Admin API Provider Management

Complete examples for managing AI providers, model mappings, and IP filtering using the Conduit Admin API TypeScript client.

## Overview

Provider management is essential for configuring AI services, routing models to providers, and controlling access through IP filtering.

## Related Documentation

- [TypeScript Setup Guide](./typescript-setup.md) - Authentication and type definitions
- [TypeScript Client](./typescript-client.md) - Complete client implementation
- [Virtual Keys Management](./typescript-virtual-keys.md) - Virtual key examples
- [Analytics Guide](./typescript-analytics.md) - Request logs and cost analytics

## Provider Management

### List All Providers

```typescript
async function listProviders() {
    try {
        const providers = await adminClient.getAllProviderCredentials();
        
        console.log('Configured Providers:');
        providers.forEach(provider => {
            console.log(`- ${provider.providerName}`);
            console.log(`  Status: ${provider.isEnabled ? 'Enabled' : 'Disabled'}`);
            console.log(`  Endpoint: ${provider.apiEndpoint || 'Default'}`);
            console.log(`  Created: ${new Date(provider.createdAt).toLocaleDateString()}`);
        });
    } catch (error) {
        console.error('Error listing providers:', error);
    }
}

// Usage
await listProviders();
```

### Add New Provider

```typescript
async function addProvider() {
    try {
        const newProvider: CreateProviderCredentialDto = {
            providerName: 'openai',
            apiKey: 'sk-...',
            organizationId: 'org-...',
            isEnabled: true,
            additionalConfig: JSON.stringify({
                maxRetries: 3,
                timeout: 30000
            })
        };
        
        const created = await adminClient.createProviderCredential(newProvider);
        console.log('Provider added:', created.providerName);
        
        // Test the connection
        const testResult = await adminClient.testProviderConnection(created.providerName);
        console.log('Connection test:', testResult.success ? 'Success' : 'Failed');
        if (testResult.modelsAvailable) {
            console.log('Available models:', testResult.modelsAvailable.join(', '));
        }
    } catch (error) {
        console.error('Error adding provider:', error);
    }
}

// Usage
await addProvider();
```

### Add Anthropic Provider

```typescript
async function addAnthropicProvider() {
    try {
        const anthropicProvider: CreateProviderCredentialDto = {
            providerName: 'anthropic',
            apiKey: 'sk-ant-...',
            isEnabled: true,
            additionalConfig: JSON.stringify({
                maxRetries: 2,
                timeout: 60000,
                maxTokens: 100000
            })
        };
        
        const created = await adminClient.createProviderCredential(anthropicProvider);
        console.log('Anthropic provider added:', created.id);
        
        // Test connection
        const testResult = await adminClient.testProviderConnection('anthropic');
        if (testResult.success) {
            console.log('✅ Anthropic connection successful');
            console.log('Available models:', testResult.modelsAvailable?.join(', '));
        } else {
            console.log('❌ Anthropic connection failed:', testResult.message);
        }
    } catch (error) {
        console.error('Error adding Anthropic provider:', error);
    }
}

// Usage
await addAnthropicProvider();
```

### Update Provider Configuration

```typescript
async function updateProviderConfig(providerId: number, config: UpdateProviderCredentialDto) {
    try {
        const updated = await adminClient.updateProviderCredential(providerId, config);
        console.log(`Updated provider: ${updated.providerName}`);
        
        // Test the updated configuration
        const testResult = await adminClient.testProviderConnection(updated.providerName);
        console.log(`Connection test: ${testResult.success ? 'Success' : 'Failed'}`);
        
        return updated;
    } catch (error) {
        console.error('Error updating provider:', error);
    }
}

// Usage examples
await updateProviderConfig(1, {
    isEnabled: true,
    additionalConfig: JSON.stringify({
        maxRetries: 5,
        timeout: 45000
    })
});

await updateProviderConfig(2, {
    apiKey: 'new-api-key-here'
});
```

### Configure Provider Health Monitoring

```typescript
async function setupHealthMonitoring(providerName: string) {
    try {
        const healthConfig: Omit<ProviderHealthConfigurationDto, 'lastCheckTime' | 'isHealthy'> = {
            providerName,
            isEnabled: true,
            checkIntervalSeconds: 300, // 5 minutes
            timeoutSeconds: 30,
            unhealthyThreshold: 3,
            healthyThreshold: 2,
            testModel: 'gpt-3.5-turbo'
        };
        
        const created = await adminClient.createProviderHealthConfiguration(healthConfig);
        console.log('Health monitoring configured for:', created.providerName);
        
        // Get recent health records
        const records = await adminClient.getProviderHealthRecords(providerName);
        console.log(`Recent health checks: ${records.length}`);
        
        const recentRecord = records[0];
        if (recentRecord) {
            console.log(`Last check: ${recentRecord.checkTime}`);
            console.log(`Status: ${recentRecord.isHealthy ? 'Healthy' : 'Unhealthy'}`);
            console.log(`Response time: ${recentRecord.responseTimeMs}ms`);
        }
    } catch (error) {
        console.error('Error setting up health monitoring:', error);
    }
}

// Usage
await setupHealthMonitoring('openai');
await setupHealthMonitoring('anthropic');
```

### Monitor Provider Health

```typescript
async function monitorProviderHealth() {
    try {
        const healthSummary = await adminClient.getProviderHealthSummary();
        
        console.log('Provider Health Summary:');
        console.log('======================');
        
        for (const provider of healthSummary) {
            console.log(`${provider.providerName}:`);
            console.log(`  Status: ${provider.isHealthy ? '🟢 Healthy' : '🔴 Unhealthy'}`);
            console.log(`  Last Check: ${new Date(provider.lastCheckTime).toLocaleString()}`);
            console.log(`  Response Time: ${provider.responseTimeMs}ms`);
            
            if (!provider.isHealthy && provider.errorMessage) {
                console.log(`  Error: ${provider.errorMessage}`);
            }
            console.log('');
        }
    } catch (error) {
        console.error('Error monitoring provider health:', error);
    }
}

// Usage
await monitorProviderHealth();
```

## Model Mappings

### Configure Model Routing

```typescript
async function setupModelMapping() {
    try {
        // Create a mapping for GPT-4 to use OpenAI
        const mapping: Omit<ModelProviderMappingDto, 'id' | 'createdAt' | 'updatedAt'> = {
            modelId: 'gpt-4',
            providerId: '1', // Provider credential ID
            providerModelId: 'gpt-4',
            isEnabled: true,
            priority: 100
        };
        
        await adminClient.createModelProviderMapping(mapping);
        console.log('Model mapping created');
        
        // List all mappings
        const allMappings = await adminClient.getAllModelProviderMappings();
        console.log(`Total mappings: ${allMappings.length}`);
        
        allMappings.forEach(m => {
            console.log(`${m.modelId} -> ${m.providerModelId} (Provider: ${m.providerId})`);
        });
    } catch (error) {
        console.error('Error setting up model mapping:', error);
    }
}

// Usage
await setupModelMapping();
```

### Create Claude Model Mappings

```typescript
async function setupClaudeMappings(anthropicProviderId: string) {
    try {
        const claudeModels = [
            { model: 'claude-3-sonnet', provider: 'claude-3-sonnet-20240229', priority: 100 },
            { model: 'claude-3-haiku', provider: 'claude-3-haiku-20240307', priority: 90 },
            { model: 'claude-3-opus', provider: 'claude-3-opus-20240229', priority: 110 }
        ];
        
        for (const model of claudeModels) {
            const mapping: Omit<ModelProviderMappingDto, 'id' | 'createdAt' | 'updatedAt'> = {
                modelId: model.model,
                providerId: anthropicProviderId,
                providerModelId: model.provider,
                isEnabled: true,
                priority: model.priority
            };
            
            await adminClient.createModelProviderMapping(mapping);
            console.log(`Mapped ${model.model} -> ${model.provider}`);
        }
        
        console.log('All Claude model mappings created');
    } catch (error) {
        console.error('Error setting up Claude mappings:', error);
    }
}

// Usage
await setupClaudeMappings('2'); // Anthropic provider ID
```

### Find Provider for Model

```typescript
async function findProviderForModel(modelAlias: string) {
    try {
        const mapping = await adminClient.getModelProviderMappingByAlias(modelAlias);
        console.log(`Model ${modelAlias} is served by:`);
        console.log(`- Provider ID: ${mapping.providerId}`);
        console.log(`- Provider Model: ${mapping.providerModelId}`);
        console.log(`- Priority: ${mapping.priority}`);
        console.log(`- Status: ${mapping.isEnabled ? 'Active' : 'Inactive'}`);
    } catch (error) {
        console.error(`No mapping found for model ${modelAlias}`);
    }
}

// Usage
await findProviderForModel('gpt-4');
await findProviderForModel('claude-3-sonnet');
```

### Update Model Mapping Priority

```typescript
async function updateMappingPriority(mappingId: number, newPriority: number) {
    try {
        await adminClient.updateModelProviderMapping(mappingId, {
            priority: newPriority
        });
        
        console.log(`Updated mapping ${mappingId} priority to ${newPriority}`);
        
        // Verify the update
        const updated = await adminClient.getModelProviderMappingById(mappingId);
        console.log(`Confirmed: ${updated.modelId} priority is now ${updated.priority}`);
    } catch (error) {
        console.error('Error updating mapping priority:', error);
    }
}

// Usage
await updateMappingPriority(1, 120);
```

## IP Filtering

### Configure IP Access Control

```typescript
async function setupIpFiltering() {
    try {
        // Enable IP filtering with settings
        const settings: IpFilterSettingsDto = {
            isEnabled: true,
            defaultAllow: false, // Deny by default
            bypassForAdminUi: true,
            excludedEndpoints: ['/api/v1/health', '/api/v1/status'],
            filterMode: 'restrictive',
            whitelistFilters: [],
            blacklistFilters: []
        };
        
        await adminClient.updateIpFilterSettings(settings);
        console.log('IP filtering enabled');
        
        // Add allowed IP ranges
        const officeNetwork: Omit<IpFilterDto, 'id' | 'createdAt' | 'updatedAt'> = {
            name: 'Office Network',
            cidrRange: '192.168.1.0/24',
            filterType: 'Allow',
            isEnabled: true,
            description: 'Main office network'
        };
        
        const vpn: Omit<IpFilterDto, 'id' | 'createdAt' | 'updatedAt'> = {
            name: 'Corporate VPN',
            cidrRange: '10.0.0.0/16',
            filterType: 'Allow',
            isEnabled: true,
            description: 'VPN access for remote workers'
        };
        
        await adminClient.createIpFilter(officeNetwork);
        await adminClient.createIpFilter(vpn);
        
        console.log('IP filters created');
    } catch (error) {
        console.error('Error setting up IP filtering:', error);
    }
}

// Usage
await setupIpFiltering();
```

### Add Developer IP Addresses

```typescript
async function addDeveloperIps(developerIps: Array<{name: string, ip: string}>) {
    try {
        for (const dev of developerIps) {
            const filter: Omit<IpFilterDto, 'id' | 'createdAt' | 'updatedAt'> = {
                name: `Developer: ${dev.name}`,
                cidrRange: `${dev.ip}/32`, // Single IP
                filterType: 'Allow',
                isEnabled: true,
                description: `Home IP for ${dev.name}`
            };
            
            const created = await adminClient.createIpFilter(filter);
            console.log(`Added IP filter for ${dev.name}: ${dev.ip}`);
        }
        
        console.log('All developer IPs added successfully');
    } catch (error) {
        console.error('Error adding developer IPs:', error);
    }
}

// Usage
const devTeam = [
    { name: 'Alice Smith', ip: '203.0.113.10' },
    { name: 'Bob Johnson', ip: '203.0.113.20' },
    { name: 'Charlie Brown', ip: '203.0.113.30' }
];

await addDeveloperIps(devTeam);
```

### Check IP Access

```typescript
async function checkIpAccess(ipAddress: string) {
    try {
        const result = await adminClient.checkIpAddress(ipAddress);
        
        console.log(`IP ${ipAddress}: ${result.isAllowed ? 'ALLOWED' : 'DENIED'}`);
        if (result.reason) {
            console.log(`Reason: ${result.reason}`);
        }
        if (result.matchedFilter) {
            console.log(`Matched filter: ${result.matchedFilter}`);
        }
    } catch (error) {
        console.error('Error checking IP:', error);
    }
}

// Usage
await checkIpAccess('192.168.1.100');
await checkIpAccess('203.0.113.45');
```

### List and Manage IP Filters

```typescript
async function manageIpFilters() {
    try {
        // Get all filters
        const allFilters = await adminClient.getAllIpFilters();
        console.log(`Total IP filters: ${allFilters.length}`);
        
        // Get only enabled filters
        const enabledFilters = await adminClient.getEnabledIpFilters();
        console.log(`Active filters: ${enabledFilters.length}`);
        
        // Display filter details
        console.log('\nActive IP Filters:');
        enabledFilters.forEach(filter => {
            console.log(`- ${filter.name}: ${filter.cidrRange} (${filter.filterType})`);
            if (filter.description) {
                console.log(`  Description: ${filter.description}`);
            }
        });
        
        // Get current settings
        const settings = await adminClient.getIpFilterSettings();
        console.log('\nIP Filter Settings:');
        console.log(`- Enabled: ${settings.isEnabled}`);
        console.log(`- Default Action: ${settings.defaultAllow ? 'Allow' : 'Deny'}`);
        console.log(`- Filter Mode: ${settings.filterMode}`);
        console.log(`- Admin UI Bypass: ${settings.bypassForAdminUi}`);
        
    } catch (error) {
        console.error('Error managing IP filters:', error);
    }
}

// Usage
await manageIpFilters();
```

### Temporarily Disable IP Filtering

```typescript
async function toggleIpFiltering(enabled: boolean) {
    try {
        // Get current settings
        const currentSettings = await adminClient.getIpFilterSettings();
        
        // Update only the enabled flag
        const updatedSettings: IpFilterSettingsDto = {
            ...currentSettings,
            isEnabled: enabled
        };
        
        await adminClient.updateIpFilterSettings(updatedSettings);
        
        console.log(`IP filtering ${enabled ? 'enabled' : 'disabled'}`);
        
        if (!enabled) {
            console.log('⚠️  Warning: All IP addresses now have access!');
        }
    } catch (error) {
        console.error('Error toggling IP filtering:', error);
    }
}

// Usage
await toggleIpFiltering(false); // Disable for maintenance
await toggleIpFiltering(true);  // Re-enable
```

## Provider Status Monitoring

### Check All Providers Status

```typescript
async function checkAllProviderStatus() {
    try {
        const statuses = await adminClient.checkAllProvidersStatus();
        
        console.log('Provider Status Report:');
        console.log('======================');
        
        for (const [providerName, status] of Object.entries(statuses)) {
            console.log(`${providerName}:`);
            console.log(`  Available: ${status.available ? '🟢 Yes' : '🔴 No'}`);
            console.log(`  Response Time: ${status.responseTimeMs}ms`);
            
            if (status.models && status.models.length > 0) {
                console.log(`  Models: ${status.models.slice(0, 3).join(', ')}${status.models.length > 3 ? '...' : ''}`);
            }
            
            if (status.error) {
                console.log(`  Error: ${status.error}`);
            }
            console.log('');
        }
    } catch (error) {
        console.error('Error checking provider status:', error);
    }
}

// Usage
await checkAllProviderStatus();
```

### Check Specific Provider

```typescript
async function checkSpecificProvider(providerName: string) {
    try {
        const status = await adminClient.checkProviderStatus(providerName);
        
        console.log(`Status for ${providerName}:`);
        console.log(`Available: ${status.available ? 'Yes' : 'No'}`);
        console.log(`Response Time: ${status.responseTimeMs}ms`);
        
        if (status.models) {
            console.log('Available Models:');
            status.models.forEach((model: string) => {
                console.log(`  - ${model}`);
            });
        }
        
        if (status.error) {
            console.log(`Error: ${status.error}`);
        }
    } catch (error) {
        console.error(`Error checking ${providerName} status:`, error);
    }
}

// Usage
await checkSpecificProvider('openai');
await checkSpecificProvider('anthropic');
```

## Bulk Provider Setup

### Setup Complete Provider Environment

```typescript
async function setupCompleteEnvironment() {
    try {
        console.log('Setting up complete provider environment...');
        
        // 1. Add providers
        const providers = [
            {
                providerName: 'openai',
                apiKey: process.env.OPENAI_API_KEY!,
                organizationId: process.env.OPENAI_ORG_ID,
                isEnabled: true
            },
            {
                providerName: 'anthropic', 
                apiKey: process.env.ANTHROPIC_API_KEY!,
                isEnabled: true
            }
        ];
        
        const createdProviders: ProviderCredentialDto[] = [];
        for (const provider of providers) {
            const created = await adminClient.createProviderCredential(provider);
            createdProviders.push(created);
            console.log(`✅ Added provider: ${created.providerName}`);
        }
        
        // 2. Setup model mappings
        const mappings = [
            { modelId: 'gpt-4', providerId: createdProviders[0].id.toString(), providerModelId: 'gpt-4', priority: 100 },
            { modelId: 'gpt-3.5-turbo', providerId: createdProviders[0].id.toString(), providerModelId: 'gpt-3.5-turbo', priority: 90 },
            { modelId: 'claude-3-sonnet', providerId: createdProviders[1].id.toString(), providerModelId: 'claude-3-sonnet-20240229', priority: 100 },
            { modelId: 'claude-3-haiku', providerId: createdProviders[1].id.toString(), providerModelId: 'claude-3-haiku-20240307', priority: 80 }
        ];
        
        for (const mapping of mappings) {
            await adminClient.createModelProviderMapping({
                modelId: mapping.modelId,
                providerId: mapping.providerId,
                providerModelId: mapping.providerModelId,
                isEnabled: true,
                priority: mapping.priority
            });
            console.log(`✅ Mapped ${mapping.modelId} -> ${mapping.providerModelId}`);
        }
        
        // 3. Setup health monitoring
        for (const provider of createdProviders) {
            await setupHealthMonitoring(provider.providerName);
            console.log(`✅ Health monitoring enabled for ${provider.providerName}`);
        }
        
        // 4. Test all connections
        for (const provider of createdProviders) {
            const testResult = await adminClient.testProviderConnection(provider.providerName);
            console.log(`✅ ${provider.providerName} test: ${testResult.success ? 'Passed' : 'Failed'}`);
        }
        
        console.log('\n🎉 Complete provider environment setup finished!');
        
    } catch (error) {
        console.error('Error setting up environment:', error);
    }
}

// Usage
await setupCompleteEnvironment();
```

## Next Steps

- [Analytics Guide](./typescript-analytics.md) - Monitor usage and costs
- [Virtual Keys Management](./typescript-virtual-keys.md) - Manage API keys
- [Advanced Patterns](./typescript-advanced.md) - Error handling and production patterns