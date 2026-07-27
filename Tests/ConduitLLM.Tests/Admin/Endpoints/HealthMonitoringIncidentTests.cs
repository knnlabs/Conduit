using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Configuration;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Tests.Admin.Endpoints;

/// <summary>
/// Covers the incident projection behind <c>/v1/admin/health-status/incidents</c>, which previously
/// reported model names as affected services, minted a fresh incident id on every poll, and emitted
/// timestamps without a UTC marker (#1067).
/// </summary>
public class HealthMonitoringIncidentTests
{
    private static readonly DateTime WindowStart =
        new(2026, 7, 18, 9, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime CurrentHour =
        new(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);

    private static HealthMonitoringEndpoints.ErrorSpikeRow Spike(
        int hourOffset,
        string model = "gpt-4o",
        int errorCount = 12,
        int errorTypes = 2) =>
        new()
        {
            HourOffset = hourOffset,
            Model = model,
            ErrorCount = errorCount,
            ErrorTypes = errorTypes
        };

    /// <summary>Hours from <see cref="WindowStart"/> to <see cref="CurrentHour"/>.</summary>
    private static int CurrentHourOffset => (int)(CurrentHour - WindowStart).TotalHours;

    [Fact]
    public void Incident_AttributesModelToAffectedModel_NotAffectedService()
    {
        var incident = HealthMonitoringEndpoints
            .BuildIncidents([Spike(0, model: "claude-opus-5")], WindowStart, CurrentHour)
            .Single();

        incident.AffectedModel.Should().Be("claude-opus-5");
        // The service card the dashboard should light up is the Gateway, not a service named
        // after the model.
        incident.AffectedService.Should().Be("core-api");
        incident.Type.Should().Be("model_error_spike");
        incident.Title.Should().NotContain("Service Degradation");
    }

    [Fact]
    public void Incident_Id_IsStableAcrossPolls()
    {
        var first = HealthMonitoringEndpoints
            .BuildIncidents([Spike(3)], WindowStart, CurrentHour).Single();
        var second = HealthMonitoringEndpoints
            .BuildIncidents([Spike(3)], WindowStart, CurrentHour.AddMinutes(5)).Single();

        second.Id.Should().Be(first.Id);
    }

    [Fact]
    public void Incident_Id_DiffersPerModelAndHour()
    {
        var incidents = HealthMonitoringEndpoints.BuildIncidents(
            [Spike(1, model: "a"), Spike(2, model: "a"), Spike(1, model: "b")],
            WindowStart,
            CurrentHour);

        incidents.Select(i => i.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Incident_Timestamps_AreUtcKind()
    {
        var incident = HealthMonitoringEndpoints
            .BuildIncidents([Spike(4)], WindowStart, CurrentHour).Single();

        incident.StartTime.Kind.Should().Be(DateTimeKind.Utc);
        incident.EndTime!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Incident_StartTime_IsTheBucketHour()
    {
        var incident = HealthMonitoringEndpoints
            .BuildIncidents([Spike(5)], WindowStart, CurrentHour).Single();

        incident.StartTime.Should().Be(WindowStart.AddHours(5));
        incident.EndTime.Should().Be(WindowStart.AddHours(6));
    }

    [Fact]
    public void Incident_InCurrentHour_IsActiveWithNoEndTime()
    {
        var incident = HealthMonitoringEndpoints
            .BuildIncidents([Spike(CurrentHourOffset)], WindowStart, CurrentHour).Single();

        incident.Status.Should().Be("active");
        incident.EndTime.Should().BeNull();
    }

    [Fact]
    public void Incident_EarlierInSameDay_IsResolved()
    {
        // The old implementation compared calendar dates, so any spike from today stayed "active".
        var incident = HealthMonitoringEndpoints
            .BuildIncidents([Spike(CurrentHourOffset - 1)], WindowStart, CurrentHour).Single();

        incident.Status.Should().Be("resolved");
        incident.EndTime.Should().Be(CurrentHour);
    }

    [Theory]
    [InlineData(10, "minor")]
    [InlineData(24, "minor")]
    [InlineData(25, "major")]
    [InlineData(49, "major")]
    [InlineData(50, "critical")]
    public void Incident_Severity_TracksErrorCount(int errorCount, string expected)
    {
        var incident = HealthMonitoringEndpoints
            .BuildIncidents([Spike(0, errorCount: errorCount)], WindowStart, CurrentHour).Single();

        incident.Severity.Should().Be(expected);
    }

    [Fact]
    public void FloorToHourUtc_TruncatesAndKeepsUtcKind()
    {
        var floored = HealthMonitoringEndpoints.FloorToHourUtc(
            new DateTime(2026, 7, 25, 12, 47, 31, DateTimeKind.Utc));

        floored.Should().Be(new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc));
        floored.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void QueryErrorSpikes_AggregatesInPostgres_WithoutTimezoneDependentTruncation()
    {
        using var db = NpgsqlModelContext();

        var sql = HealthMonitoringEndpoints
            .QueryErrorSpikes(db.RequestLogs, WindowStart)
            .ToQueryString();

        // Bucketing must be epoch arithmetic against the UTC anchor. date_trunc over a timestamptz
        // column resolves in the server's `timezone` GUC, which would shift incident windows.
        sql.Should().Contain("date_part('epoch'");
        sql.Should().NotContain("date_trunc");
        // Grouping and thresholding stay server-side — one round trip, no client evaluation.
        sql.Should().Contain("GROUP BY");
        sql.Should().Contain("HAVING");
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
