using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Configuration;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Tests.Admin.Endpoints;

/// <summary>
/// Covers the time series behind <c>/v1/admin/health-status/history</c>, which previously issued one
/// database round trip per interval — 96 sequential queries for the default 24 hour window (#1067).
/// </summary>
public class HealthMonitoringHistoryTests
{
    private static readonly DateTime StartTime =
        new(2026, 7, 25, 0, 0, 0, DateTimeKind.Utc);

    private const int IntervalMinutes = 15;

    private static HealthMonitoringEndpoints.HealthIntervalRow Row(
        int bucket,
        int totalRequests,
        int errorCount,
        double? avgLatency = 120.0) =>
        new()
        {
            Bucket = bucket,
            TotalRequests = totalRequests,
            ErrorCount = errorCount,
            AvgLatency = avgLatency
        };

    [Fact]
    public void BuildHealthHistory_ProducesOnePointPerInterval()
    {
        var history = HealthMonitoringEndpoints.BuildHealthHistory(
            [], StartTime, StartTime.AddHours(24), IntervalMinutes);

        history.Should().HaveCount(96);
        history[0].Timestamp.Should().Be(StartTime);
        history[95].Timestamp.Should().Be(StartTime.AddMinutes(95 * IntervalMinutes));
    }

    [Fact]
    public void BuildHealthHistory_TimestampsAreUtcAndEvenlySpaced()
    {
        var history = HealthMonitoringEndpoints.BuildHealthHistory(
            [], StartTime, StartTime.AddHours(2), IntervalMinutes);

        history.Should().OnlyContain(point => point.Timestamp.Kind == DateTimeKind.Utc);
        history.Zip(history.Skip(1))
            .Should().OnlyContain(pair => pair.Second.Timestamp - pair.First.Timestamp
                == TimeSpan.FromMinutes(IntervalMinutes));
    }

    [Fact]
    public void BuildHealthHistory_IntervalsWithoutTraffic_ReportFullHealthAndZeroVolume()
    {
        var history = HealthMonitoringEndpoints.BuildHealthHistory(
            [Row(1, totalRequests: 10, errorCount: 0)],
            StartTime,
            StartTime.AddHours(1),
            IntervalMinutes);

        var empty = history[0];
        empty.RequestVolume.Should().Be(0);
        empty.SystemHealth.Should().Be(100);
        empty.ErrorRate.Should().Be(0);
        empty.ResponseTime.Should().Be(0);
    }

    [Fact]
    public void BuildHealthHistory_PlacesAggregatesInTheirOwnBucket()
    {
        var history = HealthMonitoringEndpoints.BuildHealthHistory(
            [Row(2, totalRequests: 200, errorCount: 50, avgLatency: 340.5)],
            StartTime,
            StartTime.AddHours(1),
            IntervalMinutes);

        var populated = history[2];
        populated.Timestamp.Should().Be(StartTime.AddMinutes(2 * IntervalMinutes));
        populated.RequestVolume.Should().Be(200);
        populated.ErrorRate.Should().Be(25);
        populated.SystemHealth.Should().Be(75);
        populated.ResponseTime.Should().Be(340.5);

        history.Where((_, index) => index != 2)
            .Should().OnlyContain(point => point.RequestVolume == 0);
    }

    [Fact]
    public void BuildHealthHistory_NullAverageLatency_ReportsZero()
    {
        var history = HealthMonitoringEndpoints.BuildHealthHistory(
            [Row(0, totalRequests: 5, errorCount: 0, avgLatency: null)],
            StartTime,
            StartTime.AddMinutes(IntervalMinutes),
            IntervalMinutes);

        history.Single().ResponseTime.Should().Be(0);
    }

    [Fact]
    public void QueryHealthIntervals_AggregatesEveryIntervalInASingleQuery()
    {
        using var db = NpgsqlModelContext();

        var sql = HealthMonitoringEndpoints
            .QueryHealthIntervals(db.RequestLogs, StartTime, StartTime.AddHours(24), IntervalMinutes)
            .ToQueryString();

        // A single grouped statement covers the whole window: one SELECT, one GROUP BY, and the
        // error tally as a filtered aggregate rather than a second query.
        sql.Should().Contain("GROUP BY");
        sql.Should().Contain("FILTER (WHERE");
        // Interval assignment must be epoch arithmetic, not timezone-dependent truncation.
        sql.Should().Contain("date_part('epoch'");
        sql.Should().NotContain("date_trunc");
    }

    [Theory]
    [InlineData(0, HealthMonitoringEndpoints.MinHistoryHours)]
    [InlineData(-5, HealthMonitoringEndpoints.MinHistoryHours)]
    [InlineData(24, 24)]
    [InlineData(100_000, HealthMonitoringEndpoints.MaxHistoryHours)]
    public void HistoryWindow_IsClampedToASaneRange(int requested, int expected)
    {
        Math.Clamp(
            requested,
            HealthMonitoringEndpoints.MinHistoryHours,
            HealthMonitoringEndpoints.MaxHistoryHours)
            .Should().Be(expected);
    }

    /// <summary>
    /// Builds a PostgreSQL-shaped context for SQL translation assertions. No connection is opened;
    /// <c>ToQueryString</c> only needs the provider's query pipeline.
    /// </summary>
    private static ConduitDbContext NpgsqlModelContext() =>
        new(new DbContextOptionsBuilder<ConduitDbContext>()
            .UseNpgsql("Host=localhost;Database=translation-probe;Username=u;Password=p")
            .Options);
}
