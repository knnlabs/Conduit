using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.UsageTracking;

namespace ConduitLLM.Tests.Http.Accounting;

public class RequestAccountingContextTests
{
    [Fact]
    public void SnapshotCarriesTypedUsageTransportAndIndeterminateState()
    {
        var context = new RequestAccountingContext();
        var usage = new Usage { PromptTokens = 3, CompletionTokens = 4, TotalTokens = 7 };
        context.SetOperation(RequestOperation.ChatCompletion, 42, "requested-model");
        context.RecordProviderUsage(usage, "resolved-model", UsageEvidenceSource.Provider);
        context.RecordTransport(new StreamTransportEvidence(
            StreamTransportOutcome.Completed,
            2,
            3,
            100,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            false));
        context.MarkIndeterminate("settlement pending");

        var snapshot = context.Snapshot();

        Assert.Equal(RequestOperation.ChatCompletion, snapshot.Operation);
        Assert.Equal(42, snapshot.VirtualKeyId);
        Assert.Same(usage, snapshot.ProviderUsage!.Usage);
        Assert.Equal("resolved-model", snapshot.ProviderUsage.Model);
        Assert.Equal(StreamTransportOutcome.Completed, snapshot.Transport!.Outcome);
        Assert.True(snapshot.IsIndeterminate);
        Assert.Equal("settlement pending", snapshot.IndeterminateReason);
    }

    [Fact]
    public void BoundedAccumulatorNeverRetainsMoreThanConfiguredLimit()
    {
        var accumulator = new BoundedStringAccumulator(5);

        accumulator.Append("abc");
        accumulator.Append("defgh");

        Assert.Equal(5, accumulator.Length);
        Assert.Equal(8, accumulator.TotalCharactersObserved);
        Assert.True(accumulator.LimitExceeded);
        Assert.Equal("abcde", accumulator.ToString());
    }
}
