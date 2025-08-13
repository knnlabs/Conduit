using System.Threading.Tasks;
using ConduitLLM.Configuration.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace ConduitLLM.Tests.Helpers
{
    /// <summary>
    /// Test wrapper for RedisConnectionFactory that allows injection of mock connections
    /// Shared across test classes to avoid duplication
    /// </summary>
    public class TestRedisConnectionFactory : RedisConnectionFactory
    {
        private readonly IConnectionMultiplexer _connection;

        public TestRedisConnectionFactory(IConnectionMultiplexer connection) 
            : base(Options.Create(new ConduitLLM.Configuration.Options.CacheOptions()), 
                  new Mock<ILogger<RedisConnectionFactory>>().Object)
        {
            _connection = connection;
        }

        public override Task<IConnectionMultiplexer> GetConnectionAsync(string connectionString = null)
        {
            return Task.FromResult(_connection);
        }
    }
}