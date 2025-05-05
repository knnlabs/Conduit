using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Repositories
{
    public class ModelCostRepositoryTests
    {
        private readonly Mock<IDbContextFactory<ConfigurationDbContext>> _mockDbContextFactory;
        private readonly Mock<ILogger<ModelCostRepository>> _mockLogger;
        private readonly ModelCostRepository _repository;

        public ModelCostRepositoryTests()
        {
            _mockDbContextFactory = new Mock<IDbContextFactory<ConfigurationDbContext>>();
            _mockLogger = new Mock<ILogger<ModelCostRepository>>();
            
            _repository = new ModelCostRepository(_mockDbContextFactory.Object, _mockLogger.Object);
        }
        
        // Helper method to create DbSet mocks
        private static Mock<DbSet<T>> CreateDbSetMock<T>(List<T> data) where T : class
        {
            var queryable = data.AsQueryable();
            var mockSet = new Mock<DbSet<T>>();
            
            mockSet.As<IAsyncEnumerable<T>>()
                .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
                .Returns(new TestAsyncEnumerator<T>(queryable.GetEnumerator()));
            
            mockSet.As<IQueryable<T>>()
                .Setup(m => m.Provider)
                .Returns(new TestAsyncQueryProvider<T>(queryable.Provider));
            
            mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
            mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
            mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator());
            
            return mockSet;
        }

        [Fact]
        public async Task GetByProviderAsync_WithMatchingModels_ReturnsCorrectCosts()
        {
            // Arrange
            var providerName = "OpenAI";
            var credentials = new List<ProviderCredential> {
                new ProviderCredential
                {
                    Id = 1,
                    ProviderName = providerName,
                    ApiKey = "sk-test-key"
                }
            };
            
            var modelMappings = new List<ConduitLLM.Configuration.Entities.ModelProviderMapping>
            {
                new ConduitLLM.Configuration.Entities.ModelProviderMapping
                {
                    Id = 1,
                    ModelAlias = "gpt-4",
                    ProviderModelName = "gpt-4-turbo-preview",
                    ProviderCredentialId = 1
                },
                new ConduitLLM.Configuration.Entities.ModelProviderMapping
                {
                    Id = 2,
                    ModelAlias = "gpt-3.5",
                    ProviderModelName = "gpt-3.5-turbo",
                    ProviderCredentialId = 1
                }
            };
            
            var modelCosts = new List<ModelCost>
            {
                new ModelCost
                {
                    Id = 1,
                    ModelIdPattern = "gpt-4-turbo-preview",
                    InputTokenCost = 0.00001m,
                    OutputTokenCost = 0.00003m
                },
                new ModelCost
                {
                    Id = 2,
                    ModelIdPattern = "gpt-3.5-turbo",
                    InputTokenCost = 0.000001m,
                    OutputTokenCost = 0.000002m
                },
                new ModelCost
                {
                    Id = 3,
                    ModelIdPattern = "openai/whisper",
                    InputTokenCost = 0.0001m,
                    OutputTokenCost = 0.0001m
                },
                new ModelCost
                {
                    Id = 4,
                    ModelIdPattern = "anthropic*",
                    InputTokenCost = 0.00001m,
                    OutputTokenCost = 0.00003m
                }
            };
            
            // Set up DbSet mocks
            var credentialsDbSet = CreateDbSetMock(credentials);
            var modelMappingsDbSet = CreateDbSetMock(modelMappings);
            var modelCostsDbSet = CreateDbSetMock(modelCosts);
            
            // Create a new mock context for this test
            var mockDbContext = new Mock<ConfigurationDbContext>(new DbContextOptions<ConfigurationDbContext>());
            mockDbContext.Setup(c => c.ProviderCredentials).Returns(credentialsDbSet.Object);
            mockDbContext.Setup(c => c.ModelProviderMappings).Returns(modelMappingsDbSet.Object);
            mockDbContext.Setup(c => c.ModelCosts).Returns(modelCostsDbSet.Object);
            
            // Add mock for Database
            var mockDatabase = new Mock<DatabaseFacade>(mockDbContext.Object);
            mockDatabase.Setup(d => d.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>());
            mockDbContext.Setup(c => c.Database).Returns(mockDatabase.Object);
            
            // Update the factory to return this context
            _mockDbContextFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDbContext.Object);
            
            // Act
            var result = await _repository.GetByProviderAsync(providerName);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count); // 2 exact matches + openai/* pattern
            Assert.Contains(result, c => c.Id == 1); // gpt-4-turbo-preview exact match
            Assert.Contains(result, c => c.Id == 2); // gpt-3.5-turbo exact match
            Assert.Contains(result, c => c.Id == 3); // openai/whisper (provider name in pattern)
            Assert.DoesNotContain(result, c => c.Id == 4); // anthropic* (not related to OpenAI)
        }
        
        [Fact]
        public async Task GetByProviderAsync_WithWildcardPatterns_ReturnsMatchingCosts()
        {
            // Arrange
            var providerName = "Anthropic";
            var credentials = new List<ProviderCredential> {
                new ProviderCredential
                {
                    Id = 1,
                    ProviderName = providerName,
                    ApiKey = "sk-ant-key"
                }
            };
            
            var modelMappings = new List<ConduitLLM.Configuration.Entities.ModelProviderMapping>
            {
                new ConduitLLM.Configuration.Entities.ModelProviderMapping
                {
                    Id = 1,
                    ModelAlias = "claude-3",
                    ProviderModelName = "claude-3-opus-20240229",
                    ProviderCredentialId = 1
                }
            };
            
            var modelCosts = new List<ModelCost>
            {
                new ModelCost
                {
                    Id = 1,
                    ModelIdPattern = "claude-3*",
                    InputTokenCost = 0.00001m,
                    OutputTokenCost = 0.00003m
                },
                new ModelCost
                {
                    Id = 2,
                    ModelIdPattern = "anthropic-claude*",
                    InputTokenCost = 0.00001m,
                    OutputTokenCost = 0.00003m
                },
                new ModelCost
                {
                    Id = 3,
                    ModelIdPattern = "anthropic*",
                    InputTokenCost = 0.00001m,
                    OutputTokenCost = 0.00003m
                },
                new ModelCost
                {
                    Id = 4,
                    ModelIdPattern = "openai*",
                    InputTokenCost = 0.00001m,
                    OutputTokenCost = 0.00003m
                }
            };
            
            // Set up DbSet mocks
            var credentialsDbSet = CreateDbSetMock(credentials);
            var modelMappingsDbSet = CreateDbSetMock(modelMappings);
            var modelCostsDbSet = CreateDbSetMock(modelCosts);
            
            // Create a new mock context for this test
            var mockDbContext = new Mock<ConfigurationDbContext>(new DbContextOptions<ConfigurationDbContext>());
            mockDbContext.Setup(c => c.ProviderCredentials).Returns(credentialsDbSet.Object);
            mockDbContext.Setup(c => c.ModelProviderMappings).Returns(modelMappingsDbSet.Object);
            mockDbContext.Setup(c => c.ModelCosts).Returns(modelCostsDbSet.Object);
            
            // Add mock for Database
            var mockDatabase = new Mock<DatabaseFacade>(mockDbContext.Object);
            mockDatabase.Setup(d => d.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>());
            mockDbContext.Setup(c => c.Database).Returns(mockDatabase.Object);
            
            // Update the factory to return this context
            _mockDbContextFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDbContext.Object);
            
            // Act
            var result = await _repository.GetByProviderAsync(providerName);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count); // claude-3* wildcard match + anthropic* prefix patterns
            Assert.Contains(result, c => c.Id == 1); // claude-3* wildcard that matches the model
            Assert.Contains(result, c => c.Id == 2); // anthropic-claude* matches provider name pattern
            Assert.Contains(result, c => c.Id == 3); // anthropic* matches provider name pattern
            Assert.DoesNotContain(result, c => c.Id == 4); // openai* not related to Anthropic
        }
        
        [Fact]
        public async Task GetByProviderAsync_WithNoCredentials_ReturnsEmptyList()
        {
            // Arrange
            var providerName = "NonExistentProvider";
            
            // Set up empty DbSet mocks
            var credentialsDbSet = CreateDbSetMock(new List<ProviderCredential>());
            var modelMappingsDbSet = CreateDbSetMock(new List<ConduitLLM.Configuration.Entities.ModelProviderMapping>());
            var modelCostsDbSet = CreateDbSetMock(new List<ModelCost>());
            
            // Create a new mock context for this test
            var mockDbContext = new Mock<ConfigurationDbContext>(new DbContextOptions<ConfigurationDbContext>());
            mockDbContext.Setup(c => c.ProviderCredentials).Returns(credentialsDbSet.Object);
            mockDbContext.Setup(c => c.ModelProviderMappings).Returns(modelMappingsDbSet.Object);
            mockDbContext.Setup(c => c.ModelCosts).Returns(modelCostsDbSet.Object);
            
            // Add mock for Database
            var mockDatabase = new Mock<DatabaseFacade>(mockDbContext.Object);
            mockDatabase.Setup(d => d.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>());
            mockDbContext.Setup(c => c.Database).Returns(mockDatabase.Object);
            
            // Update the factory to return this context
            _mockDbContextFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDbContext.Object);
            
            // Act
            var result = await _repository.GetByProviderAsync(providerName);
            
            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }
        
        [Fact]
        public async Task GetByProviderAsync_WithNoModelMappings_ReturnsEmptyList()
        {
            // Arrange
            var providerName = "OpenAI";
            var credentials = new List<ProviderCredential> {
                new ProviderCredential
                {
                    Id = 1,
                    ProviderName = providerName,
                    ApiKey = "sk-test-key"
                }
            };
            
            // Set up DbSet mocks
            var credentialsDbSet = CreateDbSetMock(credentials);
            var modelMappingsDbSet = CreateDbSetMock(new List<ConduitLLM.Configuration.Entities.ModelProviderMapping>());
            var modelCostsDbSet = CreateDbSetMock(new List<ModelCost>());
            
            // Create a new mock context for this test
            var mockDbContext = new Mock<ConfigurationDbContext>(new DbContextOptions<ConfigurationDbContext>());
            mockDbContext.Setup(c => c.ProviderCredentials).Returns(credentialsDbSet.Object);
            mockDbContext.Setup(c => c.ModelProviderMappings).Returns(modelMappingsDbSet.Object);
            mockDbContext.Setup(c => c.ModelCosts).Returns(modelCostsDbSet.Object);
            
            // Add mock for Database
            var mockDatabase = new Mock<DatabaseFacade>(mockDbContext.Object);
            mockDatabase.Setup(d => d.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>());
            mockDbContext.Setup(c => c.Database).Returns(mockDatabase.Object);
            
            // Update the factory to return this context
            _mockDbContextFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDbContext.Object);
            
            // Act
            var result = await _repository.GetByProviderAsync(providerName);
            
            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }
    
    // Helper classes for mocking async queries
    internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        internal TestAsyncQueryProvider(IQueryProvider inner)
        {
            _inner = inner;
        }

        public IQueryable CreateQuery(System.Linq.Expressions.Expression expression)
        {
            return new TestAsyncEnumerable<TEntity>(expression);
        }

        public IQueryable<TElement> CreateQuery<TElement>(System.Linq.Expressions.Expression expression)
        {
            return new TestAsyncEnumerable<TElement>(expression);
        }

        public object? Execute(System.Linq.Expressions.Expression expression)
        {
            return _inner.Execute(expression);
        }

        public TResult Execute<TResult>(System.Linq.Expressions.Expression expression)
        {
            return _inner.Execute<TResult>(expression)!;
        }

        public TResult ExecuteAsync<TResult>(System.Linq.Expressions.Expression expression, CancellationToken cancellationToken = default)
        {
            var resultType = typeof(TResult).GetGenericArguments()[0];
            var executionResult = _inner.Execute(expression);
            
            var method = typeof(Task).GetMethod(nameof(Task.FromResult))!;
            return (TResult)method
                .MakeGenericMethod(resultType)
                .Invoke(null, new[] { executionResult })!;
        }
    }

    internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable)
            : base(enumerable)
        { }

        public TestAsyncEnumerable(System.Linq.Expressions.Expression expression)
            : base(expression)
        { }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
        }

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(((IQueryable<T>)this).Provider);
    }

    internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;

        public TestAsyncEnumerator(IEnumerator<T> inner)
        {
            _inner = inner;
        }

        public T Current => _inner.Current;

        public ValueTask DisposeAsync()
        {
            _inner.Dispose();
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return ValueTask.FromResult(_inner.MoveNext());
        }
    }
}