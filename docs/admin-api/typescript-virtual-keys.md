# Admin API Virtual Keys Management

Complete examples for managing virtual keys using the Conduit Admin API TypeScript client.

## Overview

Virtual keys are the primary mechanism for controlling access to Conduit's services. They provide authentication, authorization, budgeting, and rate limiting capabilities.

## Related Documentation

- [TypeScript Setup Guide](./typescript-setup.md) - Authentication and type definitions
- [TypeScript Client](./typescript-client.md) - Complete client implementation
- [Provider Management](./typescript-providers.md) - Provider configuration examples  
- [Analytics Guide](./typescript-analytics.md) - Request logs and cost analytics

## Client Initialization

```typescript
const adminClient = new ConduitAdminApiClient(
    'http://localhost:5002',
    'your_master_key_here',
    true // Use X-API-Key header (recommended)
);
```

## Listing Virtual Keys

### Enumerate All Virtual Keys

```typescript
async function listAllVirtualKeys() {
    try {
        const virtualKeys = await adminClient.getAllVirtualKeys();
        
        console.log(`Found ${virtualKeys.length} virtual keys:`);
        virtualKeys.forEach(key => {
            console.log(`- ${key.keyName} (ID: ${key.id})`);
            console.log(`  Budget: $${key.currentSpend}/$${key.maxBudget}`);
            console.log(`  Status: ${key.isEnabled ? 'Active' : 'Inactive'}`);
            console.log(`  Expires: ${key.expiresAt || 'Never'}`);
            console.log('');
        });
    } catch (error) {
        console.error('Error listing virtual keys:', error);
    }
}

// Usage
await listAllVirtualKeys();
```

### Search Virtual Keys by Name or Comments

```typescript
async function searchKeys(searchTerm: string) {
    try {
        const matchingKeys = await adminClient.searchVirtualKeys(searchTerm);
        
        console.log(`Found ${matchingKeys.length} keys matching "${searchTerm}":`);
        matchingKeys.forEach(key => {
            console.log(`- ${key.keyName}`);
            if (key.metadata) {
                console.log(`  Metadata: ${key.metadata}`);
            }
        });
    } catch (error) {
        console.error('Error searching virtual keys:', error);
    }
}

// Usage examples
await searchKeys('development');
await searchKeys('Project: Research');
await searchKeys('test');
```

## Creating Virtual Keys

### Create Virtual Key with Budget and Expiration

```typescript
async function createVirtualKeyWithBudget() {
    try {
        const newKeyRequest: CreateVirtualKeyRequest = {
            keyName: 'Development API Key',
            allowedModels: 'gpt-4*,claude-*',
            maxBudget: 100.00,
            budgetDuration: 'Monthly',
            expiresAt: new Date(Date.now() + 90 * 24 * 60 * 60 * 1000).toISOString(), // 90 days
            metadata: 'Project: Development Environment',
            rateLimitRpm: 60,
            rateLimitRpd: 1000
        };

        const response = await adminClient.createVirtualKey(newKeyRequest);
        
        console.log('Virtual key created successfully!');
        console.log(`Key: ${response.virtualKey}`);
        console.log(`Name: ${response.keyInfo.keyName}`);
        console.log(`Budget: $${response.keyInfo.maxBudget}`);
        console.log(`Expires: ${response.keyInfo.expiresAt}`);
        
        return response;
    } catch (error) {
        console.error('Error creating virtual key:', error);
    }
}

// Usage
const newKey = await createVirtualKeyWithBudget();
```

### Create Virtual Key with Model Permissions

```typescript
async function createKeyWithModelPermissions() {
    try {
        const keyRequest: CreateVirtualKeyRequest = {
            keyName: 'GPT-4 Only Key',
            allowedModels: 'gpt-4*,gpt-4-turbo*', // Only GPT-4 models
            maxBudget: 50.00,
            budgetDuration: 'Weekly',
            metadata: 'Restricted to GPT-4 models only',
            rateLimitRpm: 30 // Lower rate limit
        };

        const response = await adminClient.createVirtualKey(keyRequest);
        
        console.log('Model-restricted key created:');
        console.log(`Key: ${response.virtualKey}`);
        console.log(`Allowed Models: ${response.keyInfo.allowedModels}`);
        console.log(`Rate Limit: ${response.keyInfo.rateLimitRpm} requests/minute`);
        
        return response;
    } catch (error) {
        console.error('Error creating model-restricted key:', error);
    }
}

// Usage
await createKeyWithModelPermissions();
```

### Create Unlimited Key for Testing

```typescript
async function createUnlimitedTestKey() {
    try {
        const keyRequest: CreateVirtualKeyRequest = {
            keyName: 'Unlimited Test Key',
            allowedModels: '*', // All models
            maxBudget: 999999.99, // Effectively unlimited
            budgetDuration: 'Monthly',
            metadata: 'Testing key - unlimited access',
            // No rate limits specified = unlimited
        };

        const response = await adminClient.createVirtualKey(keyRequest);
        
        console.log('Unlimited test key created:');
        console.log(`Key: ${response.virtualKey}`);
        console.log(`Name: ${response.keyInfo.keyName}`);
        
        return response;
    } catch (error) {
        console.error('Error creating unlimited test key:', error);
    }
}

// Usage
await createUnlimitedTestKey();
```

## Updating Virtual Keys

### Update Key Budget and Settings

```typescript
async function updateKeyBudget(keyId: number, newBudget: number) {
    try {
        const updateRequest: UpdateVirtualKeyRequest = {
            maxBudget: newBudget,
            isEnabled: true // Ensure key is enabled
        };

        await adminClient.updateVirtualKey(keyId, updateRequest);
        
        console.log(`Updated key ${keyId} with new budget: $${newBudget}`);
        
        // Verify the update
        const updatedKey = await adminClient.getVirtualKeyById(keyId);
        console.log(`Confirmed: ${updatedKey.keyName} budget is now $${updatedKey.maxBudget}`);
        
    } catch (error) {
        console.error('Error updating key budget:', error);
    }
}

// Usage
await updateKeyBudget(123, 200.00);
```

### Disable/Enable Virtual Key

```typescript
async function toggleKeyStatus(keyId: number, enable: boolean) {
    try {
        const updateRequest: UpdateVirtualKeyRequest = {
            isEnabled: enable
        };

        await adminClient.updateVirtualKey(keyId, updateRequest);
        
        const status = enable ? 'enabled' : 'disabled';
        console.log(`Virtual key ${keyId} has been ${status}`);
        
    } catch (error) {
        console.error(`Error ${enable ? 'enabling' : 'disabling'} key:`, error);
    }
}

// Usage
await toggleKeyStatus(123, false); // Disable key
await toggleKeyStatus(123, true);  // Enable key
```

### Update Rate Limits

```typescript
async function updateRateLimits(keyId: number, rpm: number, rpd: number) {
    try {
        const updateRequest: UpdateVirtualKeyRequest = {
            rateLimitRpm: rpm,
            rateLimitRpd: rpd
        };

        await adminClient.updateVirtualKey(keyId, updateRequest);
        
        console.log(`Updated rate limits for key ${keyId}:`);
        console.log(`- Requests per minute: ${rpm}`);
        console.log(`- Requests per day: ${rpd}`);
        
    } catch (error) {
        console.error('Error updating rate limits:', error);
    }
}

// Usage
await updateRateLimits(123, 100, 2000);
```

## Key Validation

### Validate Virtual Key

```typescript
async function validateKey(key: string, model?: string) {
    try {
        const validation = await adminClient.validateVirtualKey(key, model);
        
        if (validation.isValid) {
            console.log('Key is valid!');
            console.log(`Key Name: ${validation.keyName}`);
            console.log(`Allowed Models: ${validation.allowedModels?.join(', ')}`);
            console.log(`Budget: $${validation.currentSpend}/$${validation.maxBudget}`);
        } else {
            console.log('Key is invalid:', validation.reason);
        }
    } catch (error) {
        console.error('Error validating key:', error);
    }
}

// Usage
await validateKey('vk_abcd1234...');
await validateKey('vk_abcd1234...', 'gpt-4');
```

### Validate Key with Model Permissions

```typescript
async function validateKeyForModel(key: string, requestedModel: string) {
    try {
        const validation = await adminClient.validateVirtualKey(key, requestedModel);
        
        console.log(`Validation for ${requestedModel}:`);
        
        if (validation.isValid) {
            console.log('✅ Key is valid for this model');
            console.log(`Key Name: ${validation.keyName}`);
            
            // Check budget status
            const budgetUsed = (validation.currentSpend! / validation.maxBudget!) * 100;
            console.log(`Budget used: ${budgetUsed.toFixed(1)}%`);
            
            if (budgetUsed > 80) {
                console.log('⚠️  Warning: Budget usage above 80%');
            }
        } else {
            console.log('❌ Key validation failed');
            console.log(`Reason: ${validation.reason}`);
            
            // Provide specific guidance based on reason
            if (validation.reason?.includes('model')) {
                console.log('💡 This key does not have permission for the requested model');
            } else if (validation.reason?.includes('budget')) {
                console.log('💡 Key has exceeded its budget limit');
            } else if (validation.reason?.includes('expired')) {
                console.log('💡 Key has expired');
            }
        }
    } catch (error) {
        console.error('Error validating key for model:', error);
    }
}

// Usage
await validateKeyForModel('vk_abcd1234...', 'gpt-4-turbo');
await validateKeyForModel('vk_abcd1234...', 'claude-3-sonnet');
```

## Budget Management

### Reset Virtual Key Spend

```typescript
async function resetKeySpend(keyId: number) {
    try {
        // Get current spend before reset
        const keyBefore = await adminClient.getVirtualKeyById(keyId);
        console.log(`Current spend for ${keyBefore.keyName}: $${keyBefore.currentSpend}`);
        
        // Reset spend
        await adminClient.resetVirtualKeySpend(keyId);
        
        // Verify reset
        const keyAfter = await adminClient.getVirtualKeyById(keyId);
        console.log(`Spend reset successfully. New spend: $${keyAfter.currentSpend}`);
        
    } catch (error) {
        console.error('Error resetting key spend:', error);
    }
}

// Usage
await resetKeySpend(123);
```

### Monitor Key Budget Usage

```typescript
async function monitorKeyBudgets() {
    try {
        const keys = await adminClient.getAllVirtualKeys();
        
        console.log('Budget Usage Report:');
        console.log('==================');
        
        keys.forEach(key => {
            if (key.maxBudget > 0) {
                const usagePercent = (key.currentSpend / key.maxBudget) * 100;
                
                console.log(`${key.keyName}:`);
                console.log(`  Spend: $${key.currentSpend.toFixed(2)} / $${key.maxBudget.toFixed(2)} (${usagePercent.toFixed(1)}%)`);
                
                if (usagePercent >= 100) {
                    console.log('  🔴 OVER BUDGET');
                } else if (usagePercent >= 80) {
                    console.log('  🟡 WARNING: Near budget limit');
                } else if (usagePercent >= 50) {
                    console.log('  🟢 GOOD: Within budget');
                } else {
                    console.log('  🟢 LOW USAGE');
                }
                console.log('');
            }
        });
    } catch (error) {
        console.error('Error monitoring budgets:', error);
    }
}

// Usage
await monitorKeyBudgets();
```

## Key Deletion

### Delete a Virtual Key

```typescript
async function deleteVirtualKey(keyId: number) {
    try {
        // First, get the key details for confirmation
        const keyDetails = await adminClient.getVirtualKeyById(keyId);
        console.log(`Deleting virtual key: ${keyDetails.keyName}`);
        
        // Delete the key
        await adminClient.deleteVirtualKey(keyId);
        console.log('Virtual key deleted successfully');
    } catch (error) {
        console.error('Error deleting virtual key:', error);
    }
}

// Usage
await deleteVirtualKey(123);
```

### Bulk Delete Inactive Keys

```typescript
async function deleteInactiveKeys() {
    try {
        const allKeys = await adminClient.getAllVirtualKeys();
        const inactiveKeys = allKeys.filter(key => !key.isEnabled);
        
        console.log(`Found ${inactiveKeys.length} inactive keys to delete`);
        
        for (const key of inactiveKeys) {
            console.log(`Deleting: ${key.keyName}`);
            await adminClient.deleteVirtualKey(key.id);
        }
        
        console.log('All inactive keys deleted successfully');
    } catch (error) {
        console.error('Error deleting inactive keys:', error);
    }
}

// Usage (use with caution!)
// await deleteInactiveKeys();
```

## Advanced Key Operations

### Clone Existing Key

```typescript
async function cloneVirtualKey(sourceKeyId: number, newKeyName: string) {
    try {
        // Get the source key details
        const sourceKey = await adminClient.getVirtualKeyById(sourceKeyId);
        
        // Create a new key with the same settings
        const cloneRequest: CreateVirtualKeyRequest = {
            keyName: newKeyName,
            allowedModels: sourceKey.allowedModels,
            maxBudget: sourceKey.maxBudget,
            budgetDuration: sourceKey.budgetDuration,
            metadata: `Cloned from: ${sourceKey.keyName}`,
            rateLimitRpm: sourceKey.rateLimitRpm,
            rateLimitRpd: sourceKey.rateLimitRpd
        };
        
        const response = await adminClient.createVirtualKey(cloneRequest);
        
        console.log(`Cloned key successfully:`);
        console.log(`Source: ${sourceKey.keyName}`);
        console.log(`Clone: ${response.keyInfo.keyName}`);
        console.log(`New Key: ${response.virtualKey}`);
        
        return response;
    } catch (error) {
        console.error('Error cloning virtual key:', error);
    }
}

// Usage
await cloneVirtualKey(123, 'Development Clone');
```

### Batch Create Keys for Team

```typescript
async function createTeamKeys(teamMembers: string[], baseBudget: number = 50) {
    try {
        const createdKeys: CreateVirtualKeyResponse[] = [];
        
        for (const member of teamMembers) {
            const keyRequest: CreateVirtualKeyRequest = {
                keyName: `${member} - Development Key`,
                allowedModels: 'gpt-4*,claude-*',
                maxBudget: baseBudget,
                budgetDuration: 'Monthly',
                metadata: `Owner: ${member}, Team: Development`,
                rateLimitRpm: 60,
                rateLimitRpd: 1000
            };
            
            const response = await adminClient.createVirtualKey(keyRequest);
            createdKeys.push(response);
            
            console.log(`Created key for ${member}: ${response.virtualKey}`);
        }
        
        console.log(`\n✅ Created ${createdKeys.length} team keys successfully`);
        return createdKeys;
    } catch (error) {
        console.error('Error creating team keys:', error);
    }
}

// Usage
const teamMembers = ['Alice', 'Bob', 'Charlie', 'Diana'];
await createTeamKeys(teamMembers, 75);
```

## Usage Statistics

### Get Key Usage Statistics

```typescript
async function getKeyUsageStats(virtualKeyId?: number) {
    try {
        const stats = await adminClient.getVirtualKeyUsageStatistics(virtualKeyId);
        
        if (virtualKeyId) {
            console.log(`Usage statistics for key ID ${virtualKeyId}:`);
        } else {
            console.log('Overall virtual key usage statistics:');
        }
        
        console.log(JSON.stringify(stats, null, 2));
    } catch (error) {
        console.error('Error getting usage statistics:', error);
    }
}

// Usage
await getKeyUsageStats(123); // Specific key
await getKeyUsageStats();    // All keys
```

## Error Handling Best Practices

### Robust Key Management

```typescript
async function robustKeyOperation<T>(
    operation: () => Promise<T>,
    operationName: string
): Promise<T | null> {
    try {
        const result = await operation();
        console.log(`✅ ${operationName} completed successfully`);
        return result;
    } catch (error: any) {
        console.error(`❌ ${operationName} failed:`, error.message);
        
        if (error.status === 404) {
            console.log('💡 Resource not found - check the key ID or name');
        } else if (error.status === 403) {
            console.log('💡 Forbidden - check your master key permissions');
        } else if (error.status === 429) {
            console.log('💡 Rate limited - waiting before retry...');
            // Client automatically retries rate-limited requests
        } else if (error.status >= 500) {
            console.log('💡 Server error - operation will be retried automatically');
        }
        
        return null;
    }
}

// Usage with robust error handling
async function safeCreateKey() {
    const keyRequest: CreateVirtualKeyRequest = {
        keyName: 'Safe Test Key',
        maxBudget: 25.00,
        budgetDuration: 'Weekly'
    };
    
    return await robustKeyOperation(
        () => adminClient.createVirtualKey(keyRequest),
        'Create virtual key'
    );
}
```

## Next Steps

- [Provider Management](./typescript-providers.md) - Configure AI providers
- [Analytics Guide](./typescript-analytics.md) - Monitor usage and costs  
- [Advanced Patterns](./typescript-advanced.md) - Error handling and production patterns