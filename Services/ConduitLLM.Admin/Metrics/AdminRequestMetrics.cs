using System.Diagnostics;

namespace ConduitLLM.Admin.Metrics
{
    /// <summary>
    /// Distributed tracing spans for high-value Admin operations.
    /// Uses ActivitySource for OpenTelemetry-compatible distributed tracing.
    /// </summary>
    public static class AdminRequestMetrics
    {
        /// <summary>
        /// ActivitySource for Admin API operations.
        /// </summary>
        public static readonly ActivitySource ActivitySource = new("ConduitLLM.Admin.Requests", "1.0.0");

        /// <summary>
        /// Starts an activity for a provider test operation.
        /// </summary>
        public static Activity? StartProviderTestActivity(string providerType, int providerId)
        {
            return ActivitySource.StartActivity("admin.provider.test", ActivityKind.Client,
                default(ActivityContext),
                new[]
                {
                    new KeyValuePair<string, object?>("admin.provider_type", providerType),
                    new KeyValuePair<string, object?>("admin.provider_id", providerId),
                    new KeyValuePair<string, object?>("admin.operation", "provider_test")
                });
        }

        /// <summary>
        /// Starts an activity for a virtual key operation.
        /// </summary>
        public static Activity? StartVirtualKeyActivity(string operation, int? keyId = null)
        {
            var tags = new List<KeyValuePair<string, object?>>
            {
                new("admin.operation", $"virtualkey_{operation}")
            };
            if (keyId.HasValue)
                tags.Add(new("admin.virtualkey_id", keyId.Value));

            return ActivitySource.StartActivity($"admin.virtualkey.{operation}", ActivityKind.Server,
                default(ActivityContext), tags);
        }

        /// <summary>
        /// Starts an activity for a media cleanup cycle.
        /// </summary>
        public static Activity? StartMediaCleanupActivity(string instanceId, bool dryRun)
        {
            return ActivitySource.StartActivity("admin.media.cleanup", ActivityKind.Internal,
                default(ActivityContext),
                new[]
                {
                    new KeyValuePair<string, object?>("admin.instance_id", instanceId),
                    new KeyValuePair<string, object?>("admin.dry_run", dryRun),
                    new KeyValuePair<string, object?>("admin.operation", "media_cleanup")
                });
        }

        /// <summary>
        /// Starts an activity for a CSV import/export operation.
        /// </summary>
        public static Activity? StartCsvActivity(string operation, string entityType)
        {
            return ActivitySource.StartActivity($"admin.csv.{operation}", ActivityKind.Server,
                default(ActivityContext),
                new[]
                {
                    new KeyValuePair<string, object?>("admin.csv_operation", operation),
                    new KeyValuePair<string, object?>("admin.entity_type", entityType),
                    new KeyValuePair<string, object?>("admin.operation", $"csv_{operation}")
                });
        }

        /// <summary>
        /// Starts an activity for a configuration change.
        /// </summary>
        public static Activity? StartConfigurationActivity(string entityType, string changeType)
        {
            return ActivitySource.StartActivity($"admin.config.{changeType}", ActivityKind.Server,
                default(ActivityContext),
                new[]
                {
                    new KeyValuePair<string, object?>("admin.entity_type", entityType),
                    new KeyValuePair<string, object?>("admin.change_type", changeType),
                    new KeyValuePair<string, object?>("admin.operation", "configuration_change")
                });
        }
    }
}
