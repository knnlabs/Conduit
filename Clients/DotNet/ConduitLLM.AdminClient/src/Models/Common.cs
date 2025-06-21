namespace ConduitLLM.AdminClient.Models;

/// <summary>
/// Represents a paginated response from the API.
/// </summary>
/// <typeparam name="T">The type of items in the response.</typeparam>
public class PaginatedResponse<T>
{
    /// <summary>
    /// Gets or sets the items in the current page.
    /// </summary>
    public IEnumerable<T> Items { get; set; } = new List<T>();

    /// <summary>
    /// Gets or sets the total number of items across all pages.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the current page number (1-based).
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Gets or sets the number of items per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages.
    /// </summary>
    public int TotalPages { get; set; }
}

/// <summary>
/// Represents an error response from the API.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional error message details.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets additional error details.
    /// </summary>
    public Dictionary<string, object>? Details { get; set; }

    /// <summary>
    /// Gets or sets the HTTP status code.
    /// </summary>
    public int? StatusCode { get; set; }
}

/// <summary>
/// Represents a generic API response.
/// </summary>
/// <typeparam name="T">The type of the response data.</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// Gets or sets whether the request was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the response data.
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Gets or sets error information if the request failed.
    /// </summary>
    public ErrorResponse? Error { get; set; }
}

/// <summary>
/// Specifies the sort direction.
/// </summary>
public enum SortDirection
{
    /// <summary>
    /// Sort in ascending order.
    /// </summary>
    Asc,

    /// <summary>
    /// Sort in descending order.
    /// </summary>
    Desc
}

/// <summary>
/// Represents sorting options for API requests.
/// </summary>
public class SortOptions
{
    /// <summary>
    /// Gets or sets the field to sort by.
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort direction.
    /// </summary>
    public SortDirection Direction { get; set; } = SortDirection.Asc;
}

/// <summary>
/// Base class for filter options used in API requests.
/// </summary>
public class FilterOptions
{
    /// <summary>
    /// Gets or sets the search query string.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Gets or sets the sorting options.
    /// </summary>
    public SortOptions? SortBy { get; set; }

    /// <summary>
    /// Gets or sets the page number (1-based).
    /// </summary>
    public int? PageNumber { get; set; }

    /// <summary>
    /// Gets or sets the number of items per page.
    /// </summary>
    public int? PageSize { get; set; }
}

/// <summary>
/// Represents a date range filter.
/// </summary>
public class DateRange
{
    /// <summary>
    /// Gets or sets the start date.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date.
    /// </summary>
    public DateTime EndDate { get; set; }
}

/// <summary>
/// Specifies HTTP methods for API requests.
/// </summary>
public enum HttpMethod
{
    /// <summary>
    /// GET method.
    /// </summary>
    GET,

    /// <summary>
    /// POST method.
    /// </summary>
    POST,

    /// <summary>
    /// PUT method.
    /// </summary>
    PUT,

    /// <summary>
    /// DELETE method.
    /// </summary>
    DELETE,

    /// <summary>
    /// PATCH method.
    /// </summary>
    PATCH
}

/// <summary>
/// Represents options for HTTP requests.
/// </summary>
public class RequestOptions
{
    /// <summary>
    /// Gets or sets the request timeout in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets the number of retry attempts.
    /// </summary>
    public int? Retries { get; set; }

    /// <summary>
    /// Gets or sets additional headers for the request.
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }
}