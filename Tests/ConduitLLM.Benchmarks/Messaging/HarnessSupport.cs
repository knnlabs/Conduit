using System.Diagnostics;
using System.Globalization;

using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration.Messaging.Wolverine;

namespace ConduitLLM.Benchmarks.Messaging
{
    /// <summary>
    /// Synthetic handler standing in for a real <see cref="IEventHandler{TEvent}"/> on the
    /// queue under test (#1223). It records the delivery and then awaits the configured
    /// simulated cost.
    /// </summary>
    /// <remarks>
    /// The cost is an <c>await Task.Delay</c> rather than a spin, because the real handlers
    /// this stands in for are I/O bound, not CPU bound — <c>SpendUpdateProcessor</c> alone
    /// makes four sequential round trips (key read, group read, idempotent balance
    /// adjustment, follow-on publish). Modelling that as a yielding wait keeps the
    /// measurement about the transport's scheduling rather than about the harness burning
    /// the machine's cores.
    /// </remarks>
    internal sealed class BenchHandler<TEvent> : IEventHandler<TEvent>
        where TEvent : class
    {
        private readonly DeliveryCollector _collector;

        public BenchHandler(DeliveryCollector collector)
        {
            _collector = collector;
        }

        public async Task HandleAsync(TEvent @event, IEventContext context)
        {
            _collector.Record(@event);

            if (_collector.HandlerCostMs > 0)
            {
                await Task.Delay(_collector.HandlerCostMs).ConfigureAwait(false);
            }

            _collector.Complete();
        }
    }

    /// <summary>
    /// Collects delivery counts, publish→handle latencies and observed handler concurrency
    /// for one measurement run, and signals when the expected number of messages has drained.
    /// </summary>
    internal sealed class DeliveryCollector
    {
        private readonly List<double> _latenciesMs = new();
        private readonly Lock _sync = new();

        private TaskCompletionSource<bool> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _expected = int.MaxValue;
        private long _firstDeliveryTimestamp;
        private long _lastDeliveryTimestamp;
        private int _delivered;
        private int _inFlight;
        private int _maxObservedConcurrency;

        public DeliveryCollector(int handlerCostMs)
        {
            HandlerCostMs = handlerCostMs;
        }

        public int HandlerCostMs { get; }

        /// <summary>
        /// Discards everything collected so far and arms the collector for
        /// <paramref name="expected"/> deliveries.
        /// </summary>
        /// <remarks>
        /// Called after the warm-up burst has drained. Wolverine does not begin delivering on
        /// an exclusive listener until the durability agent has assigned it to a node, which
        /// on a cold host lands several seconds after startup. Measuring through that window
        /// would charge one-off leader-election latency to every message in the run.
        /// </remarks>
        public void BeginMeasurement(int expected)
        {
            lock (_sync)
            {
                _latenciesMs.Clear();
                _latenciesMs.Capacity = expected;
                _delivered = 0;
                _firstDeliveryTimestamp = 0;
                _lastDeliveryTimestamp = 0;
                _maxObservedConcurrency = 0;
                _expected = expected;
                _completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        public int Delivered => Volatile.Read(ref _delivered);

        public int MaxObservedConcurrency => Volatile.Read(ref _maxObservedConcurrency);

        /// <summary>
        /// Delivery rate across the drain window — first delivery to last. Deliberately not
        /// measured from the start of publishing: that would fold the producer's ramp-up into
        /// a number meant to describe the consumer's ceiling.
        /// </summary>
        public double DeliveryRatePerSecond
        {
            get
            {
                lock (_sync)
                {
                    if (_delivered < 2)
                    {
                        return 0;
                    }

                    var seconds = (_lastDeliveryTimestamp - _firstDeliveryTimestamp) / (double)Stopwatch.Frequency;
                    return seconds <= 0 ? 0 : _delivered / seconds;
                }
            }
        }

        public void Record(object @event)
        {
            var now = Stopwatch.GetTimestamp();
            var concurrent = Interlocked.Increment(ref _inFlight);

            // Racy max is fine: it is a diagnostic for "did the endpoint actually run the
            // parallelism its policy asks for", not a measured output.
            if (concurrent > Volatile.Read(ref _maxObservedConcurrency))
            {
                Volatile.Write(ref _maxObservedConcurrency, concurrent);
            }

            lock (_sync)
            {
                if (_delivered == 0)
                {
                    _firstDeliveryTimestamp = now;
                }

                _delivered++;
                _lastDeliveryTimestamp = now;

                if (TryReadPublishTimestamp(@event, out var publishedAt))
                {
                    _latenciesMs.Add((now - publishedAt) * 1000.0 / Stopwatch.Frequency);
                }

                if (_delivered >= _expected)
                {
                    _completion.TrySetResult(true);
                }
            }
        }

        public void Complete() => Interlocked.Decrement(ref _inFlight);

        public async Task<bool> WaitForCompletionAsync(TimeSpan timeout)
        {
            var finished = await Task.WhenAny(_completion.Task, Task.Delay(timeout)).ConfigureAwait(false);
            return finished == _completion.Task;
        }

        public double Percentile(double quantile)
        {
            lock (_sync)
            {
                if (_latenciesMs.Count == 0)
                {
                    return 0;
                }

                var sorted = _latenciesMs.ToArray();
                Array.Sort(sorted);
                var index = (int)Math.Ceiling(quantile * sorted.Length) - 1;
                return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
            }
        }

        /// <summary>
        /// The publish-side <see cref="Stopwatch"/> tick is carried in the event's
        /// CorrelationId. Publisher and consumer share a process here, so this is an exact
        /// monotonic delta rather than a wall-clock subtraction across two machines.
        /// </summary>
        private static bool TryReadPublishTimestamp(object @event, out long timestamp)
        {
            timestamp = 0;
            var correlationId = @event.GetType().GetProperty("CorrelationId")?.GetValue(@event) as string;
            return !string.IsNullOrEmpty(correlationId)
                && long.TryParse(correlationId, NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp);
        }
    }

    /// <summary>Parsed command line for the throughput harness.</summary>
    internal sealed class HarnessOptions
    {
        public string Queue { get; private set; } = "spend";

        public int MessageCount { get; private set; } = 2000;

        public IReadOnlyList<int> HandlerCostMs { get; private set; } = new[] { 0 };

        public int PublishParallelism { get; private set; } = 32;

        /// <summary>
        /// Offered load in messages/second. Zero (the default) means saturation mode: publish
        /// the whole batch as fast as possible and time the drain, which yields the ceiling.
        /// A positive value publishes at that fixed rate instead, which is the only way to get
        /// a meaningful latency figure — at saturation the queue grows without bound and
        /// "latency" just measures how long the harness ran.
        /// </summary>
        public double OfferedRate { get; private set; }

        /// <summary>
        /// Messages published and drained before measurement starts, to get past listener
        /// agent-assignment on a cold host.
        /// </summary>
        public int WarmupCount { get; private set; } = 10;

        /// <summary>
        /// Overrides the endpoint policy's <c>PrefetchCount</c>, which
        /// <see cref="WolverineEndpointPolicy"/> translates into the per-poll receive batch.
        /// Lets a proposed retune be measured before it is committed to
        /// <see cref="ConduitEndpointPolicies"/>. Null leaves the production value.
        /// </summary>
        public int? ReceiveBatchOverride { get; private set; }

        public TimeSpan Timeout { get; private set; } = TimeSpan.FromSeconds(300);

        public string Database { get; private set; } = "conduit_msgbench";

        public string AdminConnectionString { get; private set; } =
            "Host=localhost;Port=5432;Username=conduit;Password=conduitpass;Database=postgres";

        public static HarnessOptions? Parse(string[] args)
        {
            var options = new HarnessOptions();

            for (var i = 0; i < args.Length; i++)
            {
                var key = args[i];
                if (!key.StartsWith("--", StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"Missing value for {key}.");
                    return null;
                }

                var value = args[++i];
                switch (key)
                {
                    case "--queue":
                        options.Queue = value;
                        break;
                    case "--messages":
                        options.MessageCount = int.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case "--handler-cost-ms":
                        options.HandlerCostMs = value
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(v => int.Parse(v, CultureInfo.InvariantCulture))
                            .ToArray();
                        break;
                    case "--publish-parallelism":
                        options.PublishParallelism = int.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case "--rate":
                        options.OfferedRate = double.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case "--warmup":
                        options.WarmupCount = int.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case "--receive-batch":
                        options.ReceiveBatchOverride = int.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case "--timeout-seconds":
                        options.Timeout = TimeSpan.FromSeconds(int.Parse(value, CultureInfo.InvariantCulture));
                        break;
                    case "--database":
                        options.Database = value;
                        break;
                    case "--admin-connection":
                        options.AdminConnectionString = value;
                        break;
                    default:
                        Console.Error.WriteLine($"Unknown option {key}.");
                        return null;
                }
            }

            return options;
        }
    }

    /// <summary>One measurement run: a queue at one simulated handler cost.</summary>
    internal sealed record RunResult(
        string Queue,
        int HandlerCostMs,
        double OfferedRate,
        int Published,
        int Delivered,
        bool Complete,
        double PublishRate,
        double DeliveryRate,
        double LatencyP50Ms,
        double LatencyP95Ms,
        double LatencyP99Ms,
        int MaxObservedConcurrency)
    {
        public const string Header =
            "queue            costMs   offered  delivered  deliver/s  publish/s   p50ms    p95ms    p99ms  maxConc  complete";

        private string OfferedLabel => OfferedRate > 0
            ? OfferedRate.ToString("F0", CultureInfo.InvariantCulture) + "/s"
            : "saturate";

        public string ToLine() =>
            $"  cost={HandlerCostMs,3}ms offered={OfferedLabel,8} -> {DeliveryRate,8:F1} msg/s" +
            $"   p50={LatencyP50Ms,7:F0}ms  p99={LatencyP99Ms,7:F0}ms" +
            (Complete ? string.Empty : $"  [INCOMPLETE {Delivered}/{Published}]");

        public string ToTableRow() =>
            $"{Queue,-16}{HandlerCostMs,7}{OfferedLabel,10}{Delivered,11}{DeliveryRate,11:F1}{PublishRate,11:F1}" +
            $"{LatencyP50Ms,9:F0}{LatencyP95Ms,9:F0}{LatencyP99Ms,9:F0}{MaxObservedConcurrency,9}  {(Complete ? "yes" : "NO")}";
    }
}
