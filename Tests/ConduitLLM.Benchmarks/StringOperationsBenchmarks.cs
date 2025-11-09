using BenchmarkDotNet.Attributes;
using ConduitLLM.Core.Utilities;

namespace ConduitLLM.Benchmarks;

/// <summary>
/// Benchmarks comparing traditional string operations vs. Span&lt;T&gt; optimized versions.
/// </summary>
/// <remarks>
/// Run with: dotnet run -c Release --project ConduitLLM.Benchmarks --filter *StringOperations*
/// </remarks>
[MemoryDiagnoser]
[ShortRunJob]
public class StringOperationsBenchmarks
{
    private const string SampleAuthHeader = "Bearer sk-1234567890abcdefghijklmnopqrstuvwxyz";
    private const string SampleLongString = "This is a very long string that needs to be truncated because it exceeds the maximum allowed length for display purposes";
    private const string SampleBaseUrl = "https://api.openai.com/";
    private const string SampleEndpoint = "/v1/chat/completions";
    private const string SampleIpHeader = "192.168.1.100, 10.0.0.1, 172.16.0.1";

    #region Bearer Token Extraction

    /// <summary>
    /// Old implementation: authHeader.Substring("Bearer ".Length).Trim()
    /// </summary>
    [Benchmark]
    public string BearerToken_Old()
    {
        const string bearerPrefix = "Bearer ";
        if (SampleAuthHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return SampleAuthHeader.Substring(bearerPrefix.Length).Trim();
        }
        return string.Empty;
    }

    /// <summary>
    /// New implementation: Span-based extraction with zero intermediate allocations
    /// </summary>
    [Benchmark]
    public string? BearerToken_New()
    {
        return SpanHelper.ExtractBearerToken(SampleAuthHeader);
    }

    #endregion

    #region String Truncation with Ellipsis

    /// <summary>
    /// Old implementation: value.Substring(0, maxLength) + "..."
    /// </summary>
    [Benchmark]
    public string Truncate_Old()
    {
        const int maxLength = 50;
        if (SampleLongString.Length <= maxLength)
        {
            return SampleLongString;
        }
        return SampleLongString.Substring(0, maxLength) + "...";
    }

    /// <summary>
    /// New implementation: Span-based truncation with stack allocation
    /// </summary>
    [Benchmark]
    public string Truncate_New()
    {
        const int maxLength = 50;
        return SpanHelper.TruncateWithEllipsis(SampleLongString, maxLength);
    }

    #endregion

    #region URL Combining

    /// <summary>
    /// Old implementation: baseUrl.TrimEnd('/') + "/" + endpoint.TrimStart('/')
    /// </summary>
    [Benchmark]
    public string UrlCombine_Old()
    {
        return SampleBaseUrl.TrimEnd('/') + "/" + SampleEndpoint.TrimStart('/');
    }

    /// <summary>
    /// New implementation: Span-based URL combining with stack allocation
    /// </summary>
    [Benchmark]
    public string UrlCombine_New()
    {
        return SpanHelper.CombineUrl(SampleBaseUrl, SampleEndpoint);
    }

    #endregion

    #region IP Address Extraction

    /// <summary>
    /// Old implementation: ipAddress.Split(',').First().Trim()
    /// </summary>
    [Benchmark]
    public string IpExtract_Old()
    {
        return SampleIpHeader.Split(',').First().Trim();
    }

    /// <summary>
    /// New implementation: Span-based segment extraction
    /// </summary>
    [Benchmark]
    public string IpExtract_New()
    {
        return SpanHelper.ExtractFirstSegment(SampleIpHeader);
    }

    #endregion

    #region Correlation ID Generation

    /// <summary>
    /// Old implementation: Guid.NewGuid().ToString().Substring(0, 8)
    /// </summary>
    [Benchmark]
    public string CorrelationId_Old()
    {
        return Guid.NewGuid().ToString().Substring(0, 8);
    }

    /// <summary>
    /// New implementation: Span-based GUID formatting with stack allocation
    /// </summary>
    [Benchmark]
    public string CorrelationId_New()
    {
        return SpanHelper.CreateShortCorrelationId(Guid.NewGuid());
    }

    #endregion
}
