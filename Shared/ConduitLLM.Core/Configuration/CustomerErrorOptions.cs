using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Configuration;

/// <summary>
/// Who reads provider error details in customer-facing responses.
/// </summary>
public enum CustomerErrorMode
{
    /// <summary>
    /// Customers are trusted operators of this deployment (e.g. a team running Conduit
    /// as its own LLM router). Provider errors surface with full structured detail:
    /// provider name, upstream status, and the raw provider message.
    /// </summary>
    Internal,

    /// <summary>
    /// Customers are external parties (public internet, paid service). Provider errors
    /// are translated to classified generic messages that never reveal provider
    /// identity, credentials state, or raw upstream text. Default.
    /// </summary>
    External
}

/// <summary>
/// Customer error visibility resolved from the environment.
/// Governs every customer-facing provider-error emission (HTTP, SSE, async tasks,
/// webhooks, SignalR) via <see cref="Interfaces.IProviderErrorTranslator"/>.
/// </summary>
public sealed class CustomerErrorOptions
{
    public const string ModeVariable = "CONDUIT_CUSTOMER_MODE";

    public CustomerErrorMode Mode { get; init; } = CustomerErrorMode.External;

    public static CustomerErrorOptions FromEnvironment(ILogger logger)
    {
        var rawMode = Environment.GetEnvironmentVariable(ModeVariable);
        CustomerErrorMode mode;
        if (string.IsNullOrWhiteSpace(rawMode))
        {
            mode = CustomerErrorMode.External;
        }
        else if (!Enum.TryParse(rawMode.Trim(), ignoreCase: true, out mode))
        {
            // Fail fast: silently exposing raw provider errors on a public deployment
            // is worse than refusing to start.
            throw new InvalidOperationException(
                $"Unrecognized {ModeVariable} value '{rawMode}'. Valid values: Internal, External.");
        }

        if (mode == CustomerErrorMode.Internal)
        {
            logger.LogInformation(
                "{ModeVariable}=Internal: customer-facing responses include raw provider error details.",
                ModeVariable);
        }

        return new CustomerErrorOptions { Mode = mode };
    }
}
