using System;

namespace ConduitLLM.Configuration.Exceptions
{
    /// <summary>
    /// Exception thrown when an unbounded query is attempted on a high-risk table.
    /// This prevents accidental full table scans on tables that could contain millions of records.
    /// </summary>
    public class UnboundedQueryException : InvalidOperationException
    {
        /// <summary>
        /// Gets the entity type that was queried.
        /// </summary>
        public string EntityType { get; }

        /// <summary>
        /// Gets the method name that was called.
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// Initializes a new instance of the UnboundedQueryException class.
        /// </summary>
        /// <param name="entityType">The entity type being queried</param>
        /// <param name="methodName">The method name that was called</param>
        public UnboundedQueryException(string entityType, string methodName)
            : base($"Unbounded query attempted on {entityType} via {methodName}(). " +
                   $"Use GetPaginatedAsync() or GetAllUnboundedAsync() for explicit batch needs.")
        {
            EntityType = entityType;
            MethodName = methodName;
        }

        /// <summary>
        /// Initializes a new instance of the UnboundedQueryException class with an inner exception.
        /// </summary>
        /// <param name="entityType">The entity type being queried</param>
        /// <param name="methodName">The method name that was called</param>
        /// <param name="innerException">The inner exception</param>
        public UnboundedQueryException(string entityType, string methodName, Exception innerException)
            : base($"Unbounded query attempted on {entityType} via {methodName}(). " +
                   $"Use GetPaginatedAsync() or GetAllUnboundedAsync() for explicit batch needs.", innerException)
        {
            EntityType = entityType;
            MethodName = methodName;
        }
    }
}
