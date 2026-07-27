using ConduitLLM.Configuration.Data;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Configuration.Data
{
    /// <summary>
    /// Tests mutate process environment variables, so they must not run in parallel
    /// with each other; the collection serializes them.
    /// </summary>
    [Collection("MigrationEnvironment")]
    public class MigrationStartupOptionsTests
    {
        private readonly Mock<ILogger> _loggerMock = new();

        private static MigrationStartupOptions RunWithEnvironment(
            Func<ILogger, MigrationStartupOptions> act,
            ILogger logger,
            params (string Name, string? Value)[] variables)
        {
            var saved = variables
                .Select(v => (v.Name, Original: Environment.GetEnvironmentVariable(v.Name)))
                .ToList();
            try
            {
                foreach (var (name, value) in variables)
                {
                    Environment.SetEnvironmentVariable(name, value);
                }

                return act(logger);
            }
            finally
            {
                foreach (var (name, original) in saved)
                {
                    Environment.SetEnvironmentVariable(name, original);
                }
            }
        }

        [Fact]
        public void FromEnvironment_NoVariable_DefaultsToWait()
        {
            var options = RunWithEnvironment(
                MigrationStartupOptions.FromEnvironment,
                _loggerMock.Object,
                (MigrationStartupOptions.ModeVariable, null));

            Assert.Equal(MigrationMode.Wait, options.Mode);
            Assert.Equal(MigrationStartupOptions.DefaultLockTimeoutSeconds, options.LockTimeoutSeconds);
            Assert.Equal(0, options.WaitTimeoutSeconds);
        }

        [Theory]
        [InlineData("wait", MigrationMode.Wait)]
        [InlineData("Wait", MigrationMode.Wait)]
        [InlineData("WAIT", MigrationMode.Wait)]
        [InlineData("skip", MigrationMode.Skip)]
        [InlineData(" Skip ", MigrationMode.Skip)]
        public void FromEnvironment_ValidValueAnyCase_ParsesMode(string raw, MigrationMode expected)
        {
            var options = RunWithEnvironment(
                MigrationStartupOptions.FromEnvironment,
                _loggerMock.Object,
                (MigrationStartupOptions.ModeVariable, raw));

            Assert.Equal(expected, options.Mode);
        }

        [Fact]
        public void FromEnvironment_InvalidValue_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => RunWithEnvironment(
                MigrationStartupOptions.FromEnvironment,
                _loggerMock.Object,
                (MigrationStartupOptions.ModeVariable, "Automatic")));

            Assert.Contains("Automatic", ex.Message);
        }

        [Fact]
        public void FromEnvironment_Apply_ThrowsWithExplicitMigratorInstruction()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => RunWithEnvironment(
                MigrationStartupOptions.FromEnvironment,
                _loggerMock.Object,
                (MigrationStartupOptions.ModeVariable, "Apply")));

            Assert.Contains("no longer supported", ex.Message);
            Assert.Contains("migrate", ex.Message);
        }

        [Fact]
        public void ForMigrator_IgnoresRuntimeMode()
        {
            var options = RunWithEnvironment(
                MigrationStartupOptions.ForMigrator,
                _loggerMock.Object,
                (MigrationStartupOptions.ModeVariable, "Apply"),
                (MigrationStartupOptions.LockTimeoutVariable, "42"));

            Assert.Equal(MigrationMode.Wait, options.Mode);
            Assert.Equal(42, options.LockTimeoutSeconds);
        }

        [Fact]
        public void FromEnvironment_LockTimeoutSet_ParsesSeconds()
        {
            var options = RunWithEnvironment(
                MigrationStartupOptions.FromEnvironment,
                _loggerMock.Object,
                (MigrationStartupOptions.ModeVariable, null),
                (MigrationStartupOptions.LockTimeoutVariable, "900"));

            Assert.Equal(900, options.LockTimeoutSeconds);
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("abc")]
        public void FromEnvironment_InvalidLockTimeout_Throws(string raw)
        {
            Assert.Throws<InvalidOperationException>(() => RunWithEnvironment(
                MigrationStartupOptions.FromEnvironment,
                _loggerMock.Object,
                (MigrationStartupOptions.ModeVariable, null),
                (MigrationStartupOptions.LockTimeoutVariable, raw)));
        }

        [Fact]
        public void FromEnvironment_LegacySkipVariableSet_LogsWarning()
        {
            RunWithEnvironment(
                MigrationStartupOptions.FromEnvironment,
                _loggerMock.Object,
                (MigrationStartupOptions.ModeVariable, null),
                ("CONDUIT_SKIP_DATABASE_INIT", "true"));

            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) =>
                        state.ToString()!.Contains("CONDUIT_SKIP_DATABASE_INIT")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void FromEnvironment_LegacyForceRecreateVariableSet_LogsWarning()
        {
            RunWithEnvironment(
                MigrationStartupOptions.FromEnvironment,
                _loggerMock.Object,
                (MigrationStartupOptions.ModeVariable, null),
                ("FORCE_RECREATE_DB_ON_FAILURE", "true"));

            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) =>
                        state.ToString()!.Contains("FORCE_RECREATE_DB_ON_FAILURE")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }

    [CollectionDefinition("MigrationEnvironment")]
    public class MigrationEnvironmentCollection
    {
    }
}
