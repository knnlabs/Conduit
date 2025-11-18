using System.Text.Json;
using ConduitLLM.Functions.Models;
using ConduitLLM.Functions.Providers.Exa.Models;

namespace ConduitLLM.Functions.Providers.Exa;

/// <summary>
/// Usage calculation for ExaClient.
/// </summary>
public partial class ExaClient
{
    /// <inheritdoc />
    public FunctionExecutionUsage CalculateUsageFromResponse(
        Dictionary<string, object> parameters,
        Interfaces.FunctionExecutionResult result)
    {
        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.ResponseJson))
        {
            _logger.LogWarning("Cannot calculate usage from failed execution");
            return new FunctionExecutionUsage
            {
                ResultCount = 0,
                ExecutionDuration = result.Duration
            };
        }

        try
        {
            var exaResponse = JsonSerializer.Deserialize<ExaSearchResponse>(result.ResponseJson, _jsonOptions);

            if (exaResponse == null)
            {
                _logger.LogError("Failed to deserialize Exa response for usage calculation");
                return new FunctionExecutionUsage
                {
                    ResultCount = 0,
                    ExecutionDuration = result.Duration
                };
            }

            // Calculate usage dimensions
            var usage = new FunctionExecutionUsage
            {
                ResultCount = exaResponse.Results.Count,
                SearchType = exaResponse.ResolvedSearchType,
                ExecutionDuration = result.Duration,
                Metadata = new Dictionary<string, object>
                {
                    ["requestId"] = exaResponse.RequestId
                }
            };

            // Determine content extraction counts based on actual results
            usage.TextPagesExtracted = CountTextExtractions(exaResponse.Results);
            usage.HighlightPagesExtracted = CountHighlightExtractions(exaResponse.Results);
            usage.SummaryPagesGenerated = CountSummaryGenerations(exaResponse.Results);

            // Store provider-reported cost for reconciliation
            if (exaResponse.CostDollars != null)
            {
                usage.ProviderReportedCost = exaResponse.CostDollars.Total;
                usage.Metadata["exaCostBreakdown"] = new
                {
                    search = exaResponse.CostDollars.Search,
                    getText = exaResponse.CostDollars.GetText,
                    getHighlights = exaResponse.CostDollars.GetHighlights,
                    getSummary = exaResponse.CostDollars.GetSummary,
                    total = exaResponse.CostDollars.Total
                };
            }

            _logger.LogDebug("Calculated Exa usage: SearchType={SearchType}, Results={ResultCount}, Text={TextPages}, Highlights={HighlightPages}, Summary={SummaryPages}",
                usage.SearchType, usage.ResultCount, usage.TextPagesExtracted, usage.HighlightPagesExtracted, usage.SummaryPagesGenerated);

            return usage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating usage from Exa response");
            return new FunctionExecutionUsage
            {
                ResultCount = 0,
                ExecutionDuration = result.Duration
            };
        }
    }

    /// <summary>
    /// Counts how many results have text content extracted.
    /// </summary>
    private int CountTextExtractions(List<ExaResult> results)
    {
        return results.Count(r => !string.IsNullOrWhiteSpace(r.Text));
    }

    /// <summary>
    /// Counts how many results have highlights extracted.
    /// </summary>
    private int CountHighlightExtractions(List<ExaResult> results)
    {
        return results.Count(r => r.Highlights != null && r.Highlights.Any());
    }

    /// <summary>
    /// Counts how many results have summaries generated.
    /// </summary>
    private int CountSummaryGenerations(List<ExaResult> results)
    {
        return results.Count(r => !string.IsNullOrWhiteSpace(r.Summary));
    }
}
