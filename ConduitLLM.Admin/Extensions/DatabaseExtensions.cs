using ConduitLLM.Configuration.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ConduitLLM.Admin.Extensions
{
    /// <summary>
    /// Extension methods for database operations in the Admin API
    /// </summary>
    public static class DatabaseExtensions
    {
        /// <summary>
        /// Gets the Entity Framework Database instance from an IConfigurationDbContext
        /// </summary>
        /// <param name="context">The database context</param>
        /// <returns>The EntityFramework Database instance</returns>
        public static DatabaseFacade GetDatabase(this IConfigurationDbContext context)
        {
            if (context is DbContext dbContext)
            {
                return dbContext.Database;
            }

            throw new InvalidOperationException("Cannot access Database property from interface - context is not a DbContext");
        }
    }
}