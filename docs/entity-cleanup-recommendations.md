# Entity Cleanup Recommendations

Based on my analysis of the Conduit codebase, here are recommendations for cleaning up unused fields and improving the entity structure:

## 1. ProviderCredential Entity

### Fields to Remove/Deprecate:
- **`ApiKey` field** - Already marked as DEPRECATED in comments. The system has migrated to `ProviderKeyCredentials` collection for multi-key support.
  - Current usage: Still being read in provider clients and admin services
  - Migration path: Already implemented - new providers automatically create a `ProviderKeyCredential` entry
  - Action: Add `[Obsolete]` attribute with migration message, plan removal in next major version

### Fields to Refactor:
- **Provider Identification Architecture**:
  - **Primary Identifier**: `Provider.Id` (int) - canonical identifier for all Provider records
  - **Categorization**: `ProviderType` enum - categorizes providers by API type (OpenAI, Anthropic, etc.)
  - **Display Name**: `ProviderName` string - user-facing name, can change, should not be used for identification
  - **Current Issue**: `ProviderName` is marked with TODO to use ID instead - this is correct
  - **Recommendation**: Use `Provider.Id` for identification, `ProviderType` for categorization only
  - **Benefits**: Supports multiple providers of same type, prevents identification confusion

- **`BaseUrl` inconsistency**:
  - Entity uses `BaseUrl` property
  - DTOs use `ApiBase` property
  - Creates unnecessary mapping complexity
  - Recommendation: Standardize on one name across all layers

### Fields Never Used:
- **`ApiVersion`** - No usage found in codebase beyond entity definition
  - Recommendation: Remove unless planned for future use

## 2. ProviderKeyCredential Entity

### Well-Designed Fields:
- All fields appear to be actively used
- Good separation of concerns from ProviderCredential
- Supports multi-key architecture effectively

### Potential Improvements:
- **`ApiVersion`** - Also unused here like in ProviderCredential
- **`ProviderAccountGroup`** - Limited to range 0-32, but purpose unclear from code
  - Recommendation: Add clearer documentation or consider removing if not used

## 3. VirtualKey Entity

### Observations:
- Well-structured with all fields appearing to be used
- Good use of navigation properties for related entities
- Proper use of `[Timestamp]` for concurrency control

### No immediate cleanup needed

## 4. Migration Strategy

### Phase 1 - Mark Deprecated (Immediate):
```csharp
[Obsolete("Use ProviderKeyCredentials collection instead. This field will be removed in v2.0")]
public string? ApiKey { get; set; }
```

### Phase 2 - Remove References (Next Sprint):
1. Update all provider clients to use ProviderKeyCredentials exclusively
2. Update admin services to stop reading/writing ApiKey field
3. Remove ApiKey from DTOs

### Phase 3 - Database Migration (Major Version):
1. Create migration to drop ApiKey column
2. Drop ApiVersion columns if confirmed unused
3. Potentially rename BaseUrl for consistency

## 5. Code Improvements

### Naming Consistency:
- `BaseUrl` vs `ApiBase` - Pick one and use consistently
- `Organization` vs `OrganizationId` - Standardize naming

### Type Safety:
- Use `Provider.Id` for identification, `ProviderType` enum for categorization
- Consider enum for `BudgetDuration` in VirtualKey instead of string

### Documentation:
- Add XML documentation to explain `ProviderAccountGroup` purpose
- Document the migration path from single-key to multi-key architecture

## 6. Database Impact

### Low Risk Changes:
- Adding `[Obsolete]` attributes - no DB impact
- Updating documentation - no DB impact

### Medium Risk Changes:
- Removing unused columns (ApiVersion) - requires migration
- Renaming columns for consistency - requires migration

### High Risk Changes:
- Removing ApiKey field - requires careful migration and testing
- Clarifying Provider.Id vs ProviderType usage patterns - architectural change

## Recommended Approach

1. **Immediate Actions** (No breaking changes):
   - Add `[Obsolete]` attributes with clear migration messages
   - Update XML documentation
   - Fix naming inconsistencies in new code

2. **Next Release** (Minor breaking changes):
   - Remove unused ApiVersion fields
   - Standardize on BaseUrl vs ApiBase in DTOs
   - Complete migration away from ApiKey field usage

3. **Future Major Version** (Breaking changes):
   - Drop deprecated database columns
   - Enforce Provider.Id for identification, ProviderType for categorization
   - Full architectural cleanup