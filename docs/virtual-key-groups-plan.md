# Virtual Key Groups Implementation Plan

## Overview
Implement shared budget groups for Virtual Keys where multiple keys can share the same budget/balance. Every Virtual Key will belong to a VirtualKeyGroup (no nullable relationships).

## Core Concept
- **Every Virtual Key belongs to a VirtualKeyGroup** (mandatory, not optional)
- Single-key groups are just groups with one member
- All budget tracking moves to the group level
- Virtual Keys become "access tokens" to a group's budget

## Implementation Steps

### Phase 1: Create VirtualKeyGroup Entity & Migration

1. **Create VirtualKeyGroup Entity**
```csharp
public class VirtualKeyGroup
{
    public int Id { get; set; }
    public string? ExternalGroupId { get; set; } // Optional link to external system
    public string GroupName { get; set; } // Auto-generated from key name for single-key groups
    public decimal? MaxBudget { get; set; } // Null = unlimited
    public decimal CurrentSpend { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation
    public ICollection<VirtualKey> VirtualKeys { get; set; }
    public ICollection<VirtualKeyTransaction> Transactions { get; set; }
}
```

2. **Update VirtualKey Entity**
   - Add `int VirtualKeyGroupId` (NOT nullable)
   - Add navigation property `VirtualKeyGroup`
   - **DELETE** `MaxBudget` and `CurrentSpend` columns

3. **Update VirtualKeyTransaction Entity**
   - Add `int VirtualKeyGroupId`
   - Add navigation property to group
   - Keep `VirtualKeyId` to track which specific key was used

4. **Create EF Migration** (using proper dotnet ef commands per CLAUDE.md)
   - Migration will need custom code to:
     - Create a VirtualKeyGroup for each existing VirtualKey
     - Copy MaxBudget and CurrentSpend from key to group
     - Set GroupName = KeyName
     - Set VirtualKeyGroupId on each key

### Phase 2: Update Repositories & Services

1. **Create IVirtualKeyGroupRepository**
   ```csharp
   public interface IVirtualKeyGroupRepository
   {
       Task<VirtualKeyGroup?> GetByIdAsync(int id);
       Task<VirtualKeyGroup?> GetByIdWithKeysAsync(int id);
       Task<VirtualKeyGroup?> GetByKeyIdAsync(int virtualKeyId);
       Task<List<VirtualKeyGroup>> GetAllAsync();
       Task<int> CreateAsync(VirtualKeyGroup group);
       Task<bool> UpdateAsync(VirtualKeyGroup group);
       Task<bool> DeleteAsync(int id);
   }
   ```

2. **Update Virtual Key Validation**
   - Change `ValidateVirtualKeyAsync` to check group budget instead of key budget
   - Soft block when `group.CurrentSpend >= group.MaxBudget`

3. **Update BatchSpendUpdateService**
   - Change Redis keys from `pending_spend:{keyId}` to `pending_spend:group:{groupId}`
   - Update flush logic to:
     - Accumulate by group
     - Update VirtualKeyGroup.CurrentSpend
     - Create transactions with both GroupId and KeyId

4. **Update AdminVirtualKeyService**
   - Remove all budget-related logic (moved to groups)
   - Update key creation to handle group assignment

### Phase 3: New API Endpoints

1. **Create VirtualKeyGroupsController**
   ```csharp
   [HttpGet]                                    // List all groups
   [HttpGet("{id}")]                           // Group details with member keys
   [HttpPost("{id}/adjust-balance")]           // Adjust group balance
   [HttpGet("{id}/transactions")]              // Group transaction history
   ```

2. **Update VirtualKeysController**
   - Remove `/adjust-balance` endpoint (moved to groups)
   - Update create endpoint to accept `VirtualKeyGroupId`

### Phase 4: Update DTOs

1. **CreateVirtualKeyRequestDto**
   ```csharp
   public class CreateVirtualKeyRequestDto
   {
       public string KeyName { get; set; }
       public int? VirtualKeyGroupId { get; set; } // Use existing group
       // If null, system creates new single-key group
       public decimal? MaxBudget { get; set; } // Only used for new group
       // Remove budget-related fields
   }
   ```

2. **Create Group DTOs**
   ```csharp
   public class VirtualKeyGroupDto
   {
       public int Id { get; set; }
       public string GroupName { get; set; }
       public decimal? MaxBudget { get; set; }
       public decimal CurrentSpend { get; set; }
       public decimal AvailableBalance => (MaxBudget ?? decimal.MaxValue) - CurrentSpend;
       public List<VirtualKeyBasicDto> Keys { get; set; }
   }
   ```

### Phase 5: Update Redis and Event Handling

1. **Update cache invalidation**
   - Cache by group ID instead of key ID
   - Invalidate all keys in a group when balance changes

2. **Update domain events**
   - Add VirtualKeyGroupId to relevant events
   - Consider new events like `GroupBalanceAdjusted`

### Implementation Order

1. Create VirtualKeyGroup entity
2. Update VirtualKey and VirtualKeyTransaction entities
3. Create and run EF migration
4. Create VirtualKeyGroupRepository
5. Update validation logic in VirtualKeyCache
6. Update BatchSpendUpdateService for group-based accumulation
7. Create VirtualKeyGroupsController
8. Update VirtualKeysController for group support
9. Update all tests
10. Remove obsolete code

## Key Decisions Made

1. **Every key belongs to a group** - Simplifies logic, no conditional checks
2. **Group names auto-generated** - Use key name for single-key groups
3. **Budget lives only on groups** - Delete MaxBudget/CurrentSpend from keys
4. **API works on groups** - `/api/VirtualKeyGroups/{id}/adjust-balance`
5. **Track both group and key** - Transactions reference both for audit trail

## Benefits

- ✅ Clean architecture - no conditional "is in group?" logic
- ✅ Backward compatible - existing keys get single-member groups
- ✅ Natural shared budgets - multiple keys can share balance
- ✅ Simplified validation - always check group balance
- ✅ Better for future features - group permissions, limits, etc.

## Migration Notes

- Use EF Core migrations following CLAUDE.md guidelines
- Run PostgreSQL syntax validation after migration creation
- Test migration with existing data before production deployment