using BenchmarkDotNet.Running;

namespace ConduitLLM.Benchmarks;

/// <summary>
/// BenchmarkDotNet runner for Conduit performance benchmarks.
/// </summary>
/// <remarks>
/// This project contains benchmarks comparing old vs. new implementations
/// for .NET 10 modernization efforts, particularly Span&lt;T&gt; optimizations.
///
/// Run with: dotnet run -c Release --project ConduitLLM.Benchmarks
/// </remarks>
public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
