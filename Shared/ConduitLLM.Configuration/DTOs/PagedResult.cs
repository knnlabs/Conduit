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
    }
}
