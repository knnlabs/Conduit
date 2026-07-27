using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Utilities;

/// <summary>
/// Preserves authentication-verification behavior through client decorators.
/// </summary>
public static class AuthenticationVerificationDelegator
{
    /// <summary>
    /// Delegates verification when the wrapped client supports it.
    /// </summary>
    public static Task<AuthenticationResult> VerifyAsync(
        ILLMClient innerClient,
        string clientName,
        string? apiKey,
        string? baseUrl,
        CancellationToken cancellationToken)
    {
        if (innerClient is IAuthenticationVerifiable authenticationVerifiable)
        {
            return authenticationVerifiable.VerifyAuthenticationAsync(apiKey, baseUrl, cancellationToken);
        }

        return Task.FromResult(AuthenticationResult.Failure(
            "Provider does not support authentication verification",
            $"The {clientName} client has not implemented authentication verification"));
    }

    /// <summary>
    /// Delegates health-check URL selection when the wrapped client supports it.
    /// </summary>
    public static string GetHealthCheckUrl(ILLMClient innerClient, string? baseUrl)
    {
        if (innerClient is IAuthenticationVerifiable authenticationVerifiable)
        {
            return authenticationVerifiable.GetHealthCheckUrl(baseUrl);
        }

        throw new NotSupportedException(
            $"The {innerClient.GetType().Name} client does not support authentication verification.");
    }
}
