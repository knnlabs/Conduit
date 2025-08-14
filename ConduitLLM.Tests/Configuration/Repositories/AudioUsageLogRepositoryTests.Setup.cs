using System;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Configuration.Repositories
{
    /// <summary>
    /// Unit tests for the AudioUsageLogRepository class - Setup and common infrastructure.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Repository")]
    public partial class AudioUsageLogRepositoryTests : IDisposable
    {
        private readonly ConduitDbContext _context;
        private readonly AudioUsageLogRepository _repository;
        private readonly ITestOutputHelper _output;

        public AudioUsageLogRepositoryTests(ITestOutputHelper output)
        {
            _output = output;
            
            var options = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context = new ConduitDbContext(options);
            _context.IsTestEnvironment = true;
            _repository = new AudioUsageLogRepository(_context);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}