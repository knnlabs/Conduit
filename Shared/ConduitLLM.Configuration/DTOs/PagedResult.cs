namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Generic paged result for API endpoints that return paginated data
    /// </summary>
    /// <typeparam name="T">Type of items in the result</typeparam>
    public class PagedResult<T>
    {
        /// <summary>
        /// Items for the current page.
        /// </summary>
        public List<T> Data { get; set; } = new();

        /// <summary>
        /// Pagination metadata.
        /// </summary>
        public PaginationMetadata Pagination { get; set; } = new();

        [System.Text.Json.Serialization.JsonIgnore]
        public List<T> Items { get => Data; set => Data = value; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int CurrentPage { get => Pagination.Page; set => Pagination.Page = value; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int PageSize { get => Pagination.PageSize; set => Pagination.PageSize = value; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int TotalCount { get => Pagination.TotalItems; set => Pagination.TotalItems = value; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int TotalPages { get => Pagination.TotalPages; set => Pagination.TotalPages = value; }
    }

    /// <summary>
    /// Canonical Admin API pagination metadata.
    /// </summary>
    public class PaginationMetadata
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }

        /// <summary>
        /// Creates metadata for a page, computing <see cref="TotalPages"/> from the item count.
        /// </summary>
        public static PaginationMetadata Create(int page, int pageSize, int totalItems) => new()
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = pageSize > 0 ? (int)Math.Ceiling(totalItems / (double)pageSize) : 0
        };
    }

    /// <summary>Canonical normalization for one-based Admin API pagination.</summary>
    public static class Pagination
    {
        public const int DefaultPageSize = 50;
        public const int DefaultMaxPageSize = 100;

        public static (int Page, int PageSize) Normalize(
            int page,
            int pageSize,
            int maxPageSize = DefaultMaxPageSize,
            int defaultPageSize = DefaultPageSize)
        {
            if (maxPageSize < 1)
                throw new ArgumentOutOfRangeException(nameof(maxPageSize));
            if (defaultPageSize < 1 || defaultPageSize > maxPageSize)
                throw new ArgumentOutOfRangeException(nameof(defaultPageSize));

            return (
                Math.Max(1, page),
                pageSize < 1 ? defaultPageSize : Math.Min(pageSize, maxPageSize));
        }
    }
}
