using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConduitLLM.Tests.Configuration.Data
{
    public class PendingMigrationsReadinessCheckTests
    {
        [Fact]
        public async Task CheckHealthAsync_SchemaNotCurrent_ReturnsUnhealthy()
        {
            var state = new MigrationReadinessState();
            var check = new PendingMigrationsReadinessCheck(state);

            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
        }

        [Fact]
        public async Task CheckHealthAsync_SchemaCurrent_ReturnsHealthy()
        {
            var state = new MigrationReadinessState { IsSchemaCurrent = true };
            var check = new PendingMigrationsReadinessCheck(state);

            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }
    }
}
