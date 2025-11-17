# SDK to WebAdmin Type Mappings

This document tracks the differences between SDK types and WebAdmin types that need mapping functions.

## VirtualKey

| SDK Field (VirtualKeyDto) | WebAdmin Field (VirtualKey) | Type | Notes |
|---------------------------|--------------------------|------|-------|
| keyName | name | string | Display name |
| apiKey | key | string | API key value |
| maxBudget | budget | number | Budget limit |
| isEnabled | isActive | boolean | Status |
| budgetDuration | budgetPeriod | 'Daily' \| 'Monthly' \| 'Total' | Enum values differ |
| budgetStartDate | (not used) | string | SDK only |
| expiresAt | expirationDate | string \| null | Expiration |
| createdAt | createdDate | string | Creation timestamp |
| updatedAt | modifiedDate | string | Last modified |
| lastUsedAt | lastUsedDate | string \| null | Last usage |
| requestCount | (not used) | number | SDK only |
| rateLimitRpm | (not used) | number | SDK only |
| rateLimitRpd | (not used) | number | SDK only |
| keyPrefix | (not used) | string | SDK only |
| metadata | metadata | string vs Record<string,unknown> | Type differs |
| (not present) | allowedProviders | string[] \| null | WebAdmin only |

## Provider

| SDK Field (ProviderCredentialDto) | WebAdmin Field (Provider) | Type | Notes |
|-----------------------------------|------------------------|------|-------|
| id | id | number vs string | Type differs |
| providerName | name | string | Display name |
| (combined) | type | string | WebAdmin derives from name |
| isEnabled | isEnabled | boolean | Same |
| apiEndpoint | endpoint | string | API endpoint |
| organizationId | (in configuration) | string | Part of config in WebAdmin |
| additionalConfig | configuration | string vs Record<string,unknown> | Type differs |
| createdAt | createdDate | string | Creation timestamp |
| updatedAt | modifiedDate | string | Last modified |
| (not present) | supportedModels | string[] | WebAdmin only |

## ModelMapping

| SDK Field (ModelProviderMappingDto) | WebAdmin Field (ModelMapping) | Type | Notes |
|-------------------------------------|---------------------------|------|-------|
| id | id | number vs string | Type differs |
| modelAlias | sourceModel | string | Model identifier |
| targetProvider | targetProvider | string | Same |
| targetModel | targetModel | string | Same |
| isEnabled | isActive | boolean | Status |
| priority | priority | number | Same |
| createdAt | createdDate | string | Creation timestamp |
| updatedAt | modifiedDate | string | Last modified |
| capabilities | (not used) | string | SDK only |
| isDefault | (not used) | boolean | SDK only |
| metadata | metadata | string vs Record<string,unknown> | Type differs |

## ProviderHealth

| SDK Field (ProviderHealthStatusDto) | WebAdmin Field (ProviderHealth) | Type | Notes |
|-------------------------------------|------------------------------|------|-------|
| providerId | providerId | number vs string | Type differs |
| providerName | providerName | string | Same |
| status | status | string | Same (but enum values may differ) |
| lastCheckTime | lastChecked | string | Check timestamp |
| responseTimeMs | responseTime | number | Same |
| errorMessage | lastError | string | Error details |
| (not present) | uptime | number | WebAdmin only |
| (not present) | errorRate | number | WebAdmin only |
| (not present) | incidents | ProviderIncident[] | WebAdmin only |

## SystemHealth

| SDK Field (HealthStatusDto) | WebAdmin Field (SystemHealth) | Type | Notes |
|-----------------------------|----------------------------|------|-------|
| isHealthy | status | boolean vs 'healthy'\|'degraded'\|'unhealthy' | Type differs |
| version | version | string | Same |
| timestamp | timestamp | string | Same |
| services | services | Array structure differs | Different shape |
| (not present) | uptime | number | WebAdmin only |
| (not present) | dependencies | DependencyHealth[] | WebAdmin only |

## RequestLog

| SDK Field (RequestLogDto) | WebAdmin Field (RequestLog) | Type | Notes |
|---------------------------|--------------------------|------|-------|
| id | id | string | Same |
| timestamp | timestamp | string | Same |
| virtualKeyId | virtualKeyId | number | Same |
| provider | provider | string | Same |
| model | model | string | Same |
| endpoint | endpoint | string | Same |
| method | method | string | Same |
| statusCode | statusCode | number | Same |
| latencyMs | latency | number | Field name differs |
| inputTokens | inputTokens | number | Same |
| outputTokens | outputTokens | number | Same |
| totalCost | cost | number | Field name differs |
| errorMessage | error | string | Field name differs |
| ipAddress | clientIp | string | Field name differs |
| userAgent | userAgent | string | Same |
| (not present) | virtualKeyName | string | WebAdmin only |

## Common Type Differences

1. **ID Types**: SDK uses `number` for IDs, WebAdmin uses `string`
2. **Metadata**: SDK uses `string`, WebAdmin uses `Record<string, unknown>`
3. **Timestamps**: SDK uses `createdAt/updatedAt`, WebAdmin uses `createdDate/modifiedDate`
4. **Boolean Names**: SDK uses `isEnabled/isActive`, WebAdmin varies
5. **Enums**: SDK uses PascalCase ('Daily'), WebAdmin uses lowercase ('daily')

## Next Steps

1. Create mapping functions for each entity type
2. Update all imports to use SDK types
3. Apply mappers at API boundaries
4. Remove duplicate type definitions