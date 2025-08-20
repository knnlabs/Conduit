using System;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Configuration.Repositories
{
    public partial class ProviderKeyCredentialRepositoryTests : IDisposable
    {
        private readonly ConduitDbContext _context;
        private readonly ProviderKeyCredentialRepository _repository;
        private readonly Mock<ILogger<ProviderKeyCredentialRepository>> _mockLogger;

        public ProviderKeyCredentialRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context = new ConduitDbContext(options);
            _context.IsTestEnvironment = true;
            _mockLogger = new Mock<ILogger<ProviderKeyCredentialRepository>>();
            _repository = new ProviderKeyCredentialRepository(_context, _mockLogger.Object);
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}