# DTO Cleanup Plan

## Identified Aliases to Remove

### 1. BudgetAlertNotification (SpendNotifications.cs)
- `Percentage` => alias for `PercentageUsed`
- `Remaining` => computed from `BudgetLimit - CurrentSpend`

### 2. UnusualSpendingNotification (SpendNotifications.cs)
- May have computed properties that need checking

## Removal Strategy

### Phase 1: Verify Frontend Usage
1. ✅ Checked JavaScript files - no usage of `.percentage` or `.remaining`
2. ✅ Checked Razor files - need to verify C# usage
3. ✅ SignalR hub proxies don't reference these properties directly

### Phase 2: Remove Aliases
1. Remove `Percentage` property from BudgetAlertNotification
2. Remove `Remaining` property from BudgetAlertNotification
3. Ensure all code uses the primary properties:
   - Use `PercentageUsed` instead of `Percentage`
   - Calculate remaining inline if needed

### Phase 3: Update Documentation
1. Update any API documentation
2. Update SignalR event documentation
3. Add migration notes for API consumers

## Benefits
1. **Clearer API** - No confusion about which property to use
2. **Reduced maintenance** - No need to keep aliases in sync
3. **Better performance** - Slightly less memory/CPU for computed properties

## Risk Assessment
- **Low Risk** - These are read-only computed properties
- Frontend doesn't appear to use them
- Can be easily reverted if issues arise