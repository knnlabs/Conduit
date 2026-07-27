using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace ConduitLLM.Configuration.Utilities
{
    /// <summary>
    /// Helpers for interrogating <see cref="DbUpdateException"/> chains for PostgreSQL
    /// error conditions, so callers don't each hand-roll the inner-exception walk.
    /// </summary>
    public static class DbUpdateExceptions
    {
        /// <summary>
        /// True when the exception chain carries a PostgreSQL unique violation (23505)
        /// whose constraint name contains <paramref name="constraintFragment"/>
        /// (ordinal, case-insensitive).
        /// </summary>
        public static bool IsUniqueViolation(DbUpdateException exception, string constraintFragment)
        {
            for (Exception? inner = exception.InnerException; inner != null; inner = inner.InnerException)
            {
                if (inner is PostgresException pg &&
                    pg.SqlState == PostgresErrorCodes.UniqueViolation &&
                    pg.ConstraintName?.Contains(constraintFragment, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
