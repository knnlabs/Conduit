using System;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Configuration.Tests.Repositories
{
    /// <summary>
    /// Unit tests for the AudioUsageLogRepository class - Setup and common infrastructure.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Repository")]
    public partial class AudioUsageLogRepositoryTests : IDisposable
    {
        private readonly ConfigurationDbContext _context;
        private readonly AudioUsageLogRepository _repository;
        private readonly ITestOutputHelper _output;

        public AudioUsageLogRepositoryTests(ITestOutputHelper output)
        {
            _output = output;
            
            var options = new DbContextOptionsBuilder<ConfigurationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.TransactionIgnoredWarning))
                .Options;

            _context = new ConfigurationDbContext(options);
            _context.IsTestEnvironment = true;
            _repository = new AudioUsageLogRepository(_context);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}