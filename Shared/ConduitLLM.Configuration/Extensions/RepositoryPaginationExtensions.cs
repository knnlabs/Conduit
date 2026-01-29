namespace ConduitLLM.Configuration.Extensions
{
    /// <summary>
    /// Extension methods for working with paginated repository methods.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These helpers assist in migrating from deprecated GetAllAsync methods to paginated alternatives.
    /// Use these methods when you genuinely need all records from a repository.
    /// </para>
    /// <para>
    /// For UI/API scenarios, prefer exposing pagination parameters to the caller instead of fetching all records.
    /// </para>
    /// </remarks>
    public static class RepositoryPaginationExtensions
    {
        /// <summary>
        /// Default page size used when iterating through all pages.
        /// </summary>
        public const int DefaultPageSize = 100;

        /// <summary>
        /// Retrieves all items from a paginated repository method by iterating through all pages.
        /// </summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="paginatedMethod">
        /// A function that takes (pageNumber, pageSize, cancellationToken) and returns a paginated result.
        /// </param>
        /// <param name="pageSize">The number of items to fetch per page. Defaults to 100.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A list containing all items from all pages.</returns>
        /// <remarks>
        /// <para>
        /// This method is intended for batch processing scenarios where all records are genuinely needed,
        /// such as maintenance jobs, exports, or migration scripts.
        /// </para>
        /// <para>
        /// For large datasets, consider:
        /// <list type="bullet">
        ///   <item><description>Using a streaming/yield approach if processing one at a time</description></item>
        ///   <item><description>Adding database-level filtering to reduce the result set</description></item>
        ///   <item><description>Processing in batches with <see cref="GetAllPagesAsync{T}"/> to get page-by-page access</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Migrate from: var all = await _repo.GetAllAsync(ct);
        /// // To:
        /// var all = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
        ///     _repo.GetPaginatedAsync, cancellationToken: ct);
        /// </code>
        /// </example>
        public static async Task<List<T>> GetAllViaPaginationAsync<T>(
            Func<int, int, CancellationToken, Task<(List<T> Items, int TotalCount)>> paginatedMethod,
            int pageSize = DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(paginatedMethod);

            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be greater than zero.");
            }

            var allItems = new List<T>();
            int page = 1;
            int totalCount;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (items, total) = await paginatedMethod(page, pageSize, cancellationToken).ConfigureAwait(false);
                totalCount = total;

                if (items.Count > 0)
                {
                    allItems.AddRange(items);
                }

                page++;
            }
            while (allItems.Count < totalCount);

            return allItems;
        }

        /// <summary>
        /// Retrieves all items using a parameterized paginated method (e.g., GetByProviderIdPaginatedAsync).
        /// </summary>
        /// <typeparam name="TParam">The type of the filter parameter.</typeparam>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="paginatedMethod">
        /// A function that takes (param, pageNumber, pageSize, cancellationToken) and returns a paginated result.
        /// </param>
        /// <param name="param">The filter parameter to pass to the paginated method.</param>
        /// <param name="pageSize">The number of items to fetch per page. Defaults to 100.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A list containing all items from all pages.</returns>
        /// <example>
        /// <code>
        /// // Migrate from: var keys = await _repo.GetByProviderIdAsync(providerId);
        /// // To:
        /// var keys = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
        ///     _repo.GetByProviderIdPaginatedAsync, providerId, cancellationToken: ct);
        /// </code>
        /// </example>
        public static async Task<List<T>> GetAllViaPaginationAsync<TParam, T>(
            Func<TParam, int, int, CancellationToken, Task<(List<T> Items, int TotalCount)>> paginatedMethod,
            TParam param,
            int pageSize = DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(paginatedMethod);

            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be greater than zero.");
            }

            var allItems = new List<T>();
            int page = 1;
            int totalCount;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (items, total) = await paginatedMethod(param, page, pageSize, cancellationToken).ConfigureAwait(false);
                totalCount = total;

                if (items.Count > 0)
                {
                    allItems.AddRange(items);
                }

                page++;
            }
            while (allItems.Count < totalCount);

            return allItems;
        }

        /// <summary>
        /// Iterates through all pages of a paginated repository method, yielding each page.
        /// </summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="paginatedMethod">
        /// A function that takes (pageNumber, pageSize, cancellationToken) and returns a paginated result.
        /// </param>
        /// <param name="pageSize">The number of items to fetch per page. Defaults to 100.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>An async enumerable of pages, where each page contains a list of items.</returns>
        /// <remarks>
        /// Use this method when you want to process records in batches without loading all into memory at once.
        /// </remarks>
        /// <example>
        /// <code>
        /// await foreach (var page in RepositoryPaginationExtensions.GetAllPagesAsync(
        ///     _repo.GetPaginatedAsync, cancellationToken: ct))
        /// {
        ///     foreach (var item in page)
        ///     {
        ///         // Process each item
        ///     }
        /// }
        /// </code>
        /// </example>
        public static async IAsyncEnumerable<List<T>> GetAllPagesAsync<T>(
            Func<int, int, CancellationToken, Task<(List<T> Items, int TotalCount)>> paginatedMethod,
            int pageSize = DefaultPageSize,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(paginatedMethod);

            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be greater than zero.");
            }

            int page = 1;
            int fetchedCount = 0;
            int totalCount;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (items, total) = await paginatedMethod(page, pageSize, cancellationToken).ConfigureAwait(false);
                totalCount = total;

                if (items.Count > 0)
                {
                    fetchedCount += items.Count;
                    yield return items;
                }

                page++;
            }
            while (fetchedCount < totalCount);
        }

        /// <summary>
        /// Iterates through all pages of a parameterized paginated repository method, yielding each page.
        /// </summary>
        /// <typeparam name="TParam">The type of the filter parameter.</typeparam>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="paginatedMethod">
        /// A function that takes (param, pageNumber, pageSize, cancellationToken) and returns a paginated result.
        /// </param>
        /// <param name="param">The filter parameter to pass to the paginated method.</param>
        /// <param name="pageSize">The number of items to fetch per page. Defaults to 100.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>An async enumerable of pages, where each page contains a list of items.</returns>
        public static async IAsyncEnumerable<List<T>> GetAllPagesAsync<TParam, T>(
            Func<TParam, int, int, CancellationToken, Task<(List<T> Items, int TotalCount)>> paginatedMethod,
            TParam param,
            int pageSize = DefaultPageSize,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(paginatedMethod);

            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be greater than zero.");
            }

            int page = 1;
            int fetchedCount = 0;
            int totalCount;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (items, total) = await paginatedMethod(param, page, pageSize, cancellationToken).ConfigureAwait(false);
                totalCount = total;

                if (items.Count > 0)
                {
                    fetchedCount += items.Count;
                    yield return items;
                }

                page++;
            }
            while (fetchedCount < totalCount);
        }
    }
}
