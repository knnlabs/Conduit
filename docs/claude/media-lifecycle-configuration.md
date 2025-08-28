# Media Lifecycle Configuration

The media lifecycle management system is configured entirely through environment variables for easy production deployment.

## Environment Variables

### Core Settings

- `MediaLifecycle__SchedulerMode` - Controls where the scheduler runs
  - `Disabled` - No scheduling (default)
  - `CoreApi` - Run on Core API instances
  - `AdminApi` - Run on Admin API instances  
  - `Any` - Run on any instance

- `MediaLifecycle__DryRunMode` - Safety setting
  - `true` - Log what would be deleted but don't delete (default)
  - `false` - Actually delete media files

- `MediaLifecycle__ScheduleIntervalMinutes` - How often to run cleanup (default: 60)

### Safety Controls

- `MediaLifecycle__EnableSoftDelete` - Use soft delete with grace period (default: true)
- `MediaLifecycle__SoftDeleteGracePeriodDays` - Days before permanent deletion (default: 7)
- `MediaLifecycle__TestVirtualKeyGroups__0` - Test with specific group ID
- `MediaLifecycle__TestVirtualKeyGroups__1` - Add more test groups as needed
- `MediaLifecycle__RequireManualApprovalForLargeBatches` - Require approval for large deletions (default: false)
- `MediaLifecycle__LargeBatchThreshold` - What constitutes a large batch (default: 100)

### Performance Tuning

- `MediaLifecycle__MaxBatchSize` - Maximum files per batch (default: 50)
- `MediaLifecycle__DelayBetweenBatchesMs` - Delay between batches in milliseconds (default: 500)
- `MediaLifecycle__MaxConcurrentBatches` - Parallel batch processing (default: 2)
- `MediaLifecycle__MonthlyDeleteBudget` - Maximum deletions per month (default: 500000)
- `MediaLifecycle__R2OperationTimeoutSeconds` - Timeout for R2 operations (default: 30)

### Monitoring

- `MediaLifecycle__EnableAuditLogging` - Log all deletion operations (default: true)
- `MediaLifecycle__EnableMetrics` - Collect metrics (default: true)

## Example Docker Compose Configuration

```yaml
environment:
  # Basic configuration
  MediaLifecycle__SchedulerMode: "CoreApi"
  MediaLifecycle__DryRunMode: "true"
  MediaLifecycle__ScheduleIntervalMinutes: "60"
  
  # Safety settings
  MediaLifecycle__EnableSoftDelete: "true"
  MediaLifecycle__SoftDeleteGracePeriodDays: "7"
  MediaLifecycle__TestVirtualKeyGroups__0: "1"  # Test with group 1
  
  # Performance
  MediaLifecycle__MaxBatchSize: "50"
  MediaLifecycle__DelayBetweenBatchesMs: "500"
  MediaLifecycle__MonthlyDeleteBudget: "500000"
```

## Production Deployment Steps

1. **Start with dry run mode**:
   ```bash
   export MediaLifecycle__DryRunMode="true"
   export MediaLifecycle__SchedulerMode="CoreApi"
   ```

2. **Test with specific groups**:
   ```bash
   export MediaLifecycle__TestVirtualKeyGroups__0="1"
   ```

3. **Monitor logs** to see what would be deleted

4. **Enable actual deletion** when confident:
   ```bash
   export MediaLifecycle__DryRunMode="false"
   ```

5. **Remove test restrictions** for full production:
   ```bash
   unset MediaLifecycle__TestVirtualKeyGroups__0
   ```

## Monitoring

Check scheduler status:
```bash
docker logs conduit-api-1 | grep -i "media scheduler"
```

Check cleanup activity:
```bash
docker logs conduit-api-1 | grep -E "cleanup|retention|expired"
```

## Database Tables

- `MediaRetentionPolicies` - Retention policy definitions
- `MediaRecords` - Tracks all generated media
- `VirtualKeyGroups.MediaRetentionPolicyId` - Links groups to policies

## Admin API Endpoints

- `GET /api/admin/media-retention/policies` - List all policies
- `POST /api/admin/media-retention/policies` - Create new policy
- `PUT /api/admin/media-retention/policies/{id}` - Update policy
- `DELETE /api/admin/media-retention/policies/{id}` - Delete policy
- `POST /api/admin/media-retention/assign/{groupId}/{policyId}` - Assign policy to group