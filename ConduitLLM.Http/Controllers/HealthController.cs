using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Controller for health check endpoints
    /// </summary>
    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<HealthController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthController"/> class
        /// </summary>
        public HealthController(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<HealthController> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the health status of the database
        /// </summary>
        /// <returns>A health status response</returns>
        [HttpGet("db")]
        [ProducesResponseType(typeof(DatabaseHealthResponse), 200)]
        [ProducesResponseType(typeof(DatabaseHealthResponse), 503)]
        public async Task<IActionResult> GetDatabaseHealthAsync()
        {
            _logger.LogInformation("Database health check requested");
            
            var response = new DatabaseHealthResponse
            {
                Status = "healthy",
                Timestamp = DateTimeOffset.UtcNow
            };

            try
            {
                // Create a DbContext and check connection
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                
                // Check if database can be connected to
                if (!await CanConnectAsync(dbContext))
                {
                    response.Status = "unhealthy";
                    response.Details = "Cannot connect to database";
                    return StatusCode(503, response);
                }

                // Check for pending migrations
                var pendingMigrations = await GetPendingMigrationsAsync(dbContext);
                var hasPendingMigrations = pendingMigrations.Any();
                
                if (hasPendingMigrations)
                {
                    response.Status = "unhealthy";
                    response.Details = "Database has pending migrations";
                    return StatusCode(503, response);
                }
                
                // Database is healthy if we reached here
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking database health");
                response.Status = "unhealthy";
                response.Details = $"Error: {ex.Message}";
                return StatusCode(500, response);
            }
        }

        /// <summary>
        /// Checks if the database can be connected to, can be overridden in tests
        /// </summary>
        protected virtual Task<bool> CanConnectAsync(ConfigurationDbContext dbContext)
        {
            return dbContext.Database.CanConnectAsync();
        }

        /// <summary>
        /// Helper method to get pending migrations, can be overridden in tests
        /// </summary>
        protected virtual async Task<IEnumerable<string>> GetPendingMigrationsAsync(ConfigurationDbContext dbContext)
        {
            // Use the real method in production
            return await dbContext.Database.GetPendingMigrationsAsync();
        }
    }

    /// <summary>
    /// Response model for database health check
    /// </summary>
    public class DatabaseHealthResponse
    {
        /// <summary>
        /// Health status: "healthy" or "unhealthy"
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp of the health check in UTC
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Details of health status, particularly for unhealthy states
        /// </summary>
        [JsonPropertyName("details")]
        public string? Details { get; set; }
    }
}
