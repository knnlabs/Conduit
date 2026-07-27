namespace ConduitLLM.Configuration.Messaging.Wolverine;

/// <summary>
/// PostgreSQL queue resources shared by runtime topology and the explicit
/// migration command.
/// </summary>
public static class WolverineQueueNames
{
    public const string GatewayEvents = "gateway-events";
    public const string AdminEvents = "admin-events";

    public static IReadOnlyList<string> Gateway { get; } =
    [
        ConduitEndpointPolicies.WebhookDelivery.Name,
        ConduitEndpointPolicies.SpendUpdate.Name,
        ConduitEndpointPolicies.VideoGeneration.Name,
        ConduitEndpointPolicies.ImageGeneration.Name,
        GatewayEvents
    ];

    public static IReadOnlyList<string> Admin { get; } = [AdminEvents];
}
