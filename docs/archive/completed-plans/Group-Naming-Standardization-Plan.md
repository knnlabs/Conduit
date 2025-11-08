# Group Naming Standardization Plan

## Current State Analysis

### Consistent Patterns (Keep As-Is)
1. **Virtual Key Groups**: `vkey-{id}` - Used consistently across all services
2. **Admin Groups**: `admin`, `admin-vkey-{id}`, `admin-provider-{name}` - Well structured
3. **Task Groups**: `task-{id}` - Clear and consistent
4. **Content Type Groups**: `image-{id}`, `video-{id}` - Type-specific, clear

### Inconsistent Patterns (Need Standardization)
1. **Webhook Groups**: `webhook_{host}_{path}` uses underscores while others use hyphens
2. **Compound Groups**: Some use `-` (e.g., `vkey-42-content`) while webhooks use `_`
3. **Model Groups**: `vkey-{id}-model-{alias}` could be simplified

## Proposed Standards

### 1. Delimiter Convention
**Use hyphens (-) consistently for all group names**
- Current: `webhook_api_example_com_hooks_webhook`
- Proposed: `webhook-api-example-com-hooks-webhook`

### 2. Hierarchy Convention
**Use colon (:) for hierarchical relationships**
- Current: `vkey-42-content`
- Proposed: `vkey:42:content`
- Current: `admin-vkey-42`
- Proposed: `admin:vkey:42`

### 3. Type Prefix Convention
**Always use type prefix for clarity**
- ✅ `task-{id}`
- ✅ `image-{id}`
- ✅ `video-{id}`
- ✅ `webhook-{sanitized-url}`

## Implementation Plan

### Phase 1: Document Standards
1. Create group naming standards document
2. Add examples for each pattern
3. Document migration strategy

### Phase 2: Update Webhook Groups
1. Change webhook group sanitization to use hyphens
2. Update WebhookDeliveryHub
3. Update WebhookDeliveryNotificationService

### Phase 3: Consider Hierarchical Updates (Optional)
1. Evaluate if colon-based hierarchy improves clarity
2. Create migration utilities if needed
3. Update gradually with backward compatibility

## Benefits
1. **Consistency** - Single delimiter across all groups
2. **Readability** - Easier to parse visually
3. **Tooling** - Consistent patterns for monitoring/debugging
4. **Future-proof** - Clear hierarchy for complex scenarios

## Risks
1. **Breaking Change** - Existing subscriptions would break
2. **Migration Complexity** - Need to support both patterns temporarily
3. **Client Updates** - All clients need to update group names

## Recommendation
Given the low priority and potential breaking changes, I recommend:
1. **Fix only webhook groups** - Change underscores to hyphens
2. **Document current patterns** - Create clear documentation
3. **Defer hierarchy changes** - Not worth the breaking change risk

The current patterns are mostly consistent and functional. The webhook underscore issue is the only real inconsistency worth fixing.