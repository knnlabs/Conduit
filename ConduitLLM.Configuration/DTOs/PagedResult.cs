using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Generic paged result for API endpoints that return paginated data
    /// </summary>
    /// <typeparam name="T">Type of items in the result</typeparam>
    public class PagedResult<T>
    {
        /// <summary>
        /// List of items for the current page
        /// </summary>
        public List<T> Items { get; set; } = new List<T>();

        /// <summary>
        /// Total number of items across all pages
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page number (1-based)
        /// </summary>
        public int CurrentPage { get; set; }

        /// <summary>
        /// Number of items per page
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// Whether there is a previous page
        /// </summary>
        public bool HasPreviousPage => CurrentPage > 1;

        /// <summary>
        /// Whether there is a next page
        /// </summary>
        public bool HasNextPage => CurrentPage < TotalPages;

        // Backwards compatibility properties

        /// <summary>
        /// Alias for CurrentPage for backwards compatibility
        /// </summary>
        public int Page
        {
            get => CurrentPage;
            set => CurrentPage = value;
        }

        /// <summary>
        /// Alias for TotalCount for backwards compatibility
        /// </summary>
        public int TotalItems
        {
            get => TotalCount;
            set => TotalCount = value;
        }
    }
}