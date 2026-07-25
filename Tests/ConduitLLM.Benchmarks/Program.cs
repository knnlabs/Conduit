using BenchmarkDotNet.Running;

using ConduitLLM.Benchmarks.Messaging;

namespace ConduitLLM.Benchmarks;

/// <summary>
/// BenchmarkDotNet runner for Conduit performance benchmarks.
/// </summary>
/// <remarks>
/// This project contains benchmarks comparing old vs. new implementations
/// for .NET 10 modernization efforts, particularly Span&lt;T&gt; optimizations.
///
/// Run with: dotnet run -c Release --project ConduitLLM.Benchmarks
///
/// The <c>transport-throughput</c> mode is not a BenchmarkDotNet benchmark — it drives a
/// live Wolverine host against a real PostgreSQL queue to measure sustained delivery
/// throughput and headroom (#1223):
///   dotnet run -c Release --project Tests/ConduitLLM.Benchmarks -- transport-throughput --queue spend
/// </remarks>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("transport-throughput", StringComparison.OrdinalIgnoreCase))
        {
            return await TransportThroughputHarness.RunAsync(args[1..]).ConfigureAwait(false);
        }

        _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return 0;
    }
}
