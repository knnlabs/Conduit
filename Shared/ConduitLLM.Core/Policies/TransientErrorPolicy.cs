using System.IO;
using System.Net.Sockets;

using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Policies;

/// <summary>
/// Defines the solution-wide transient exception set for retries and provider failover.
/// </summary>
public static class TransientErrorPolicy
{
    public static bool IsTransient(
        Exception exception,
        CancellationToken callerToken = default)
    {
        if (callerToken.IsCancellationRequested)
        {
            return false;
        }

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is OperationCanceledException
                or TimeoutException
                or HttpRequestException
                or IOException
                or SocketException)
            {
                return true;
            }

            var providerError = ProviderErrorClassifier.ClassifyException(current);
            if (providerError is ProviderErrorType.RateLimitExceeded
                or ProviderErrorType.ServiceUnavailable
                or ProviderErrorType.NetworkError
                or ProviderErrorType.Timeout)
            {
                return true;
            }
        }

        return false;
    }
}
