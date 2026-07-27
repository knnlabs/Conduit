using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.Tests.Configuration.DTOs;

public sealed class PaginationTests
{
    [Theory]
    [InlineData(0, 0, 1, 50)]
    [InlineData(-5, 25, 1, 25)]
    [InlineData(2, 500, 2, 100)]
    public void Normalize_ClampsToCanonicalRange(
        int page,
        int pageSize,
        int expectedPage,
        int expectedPageSize)
    {
        Assert.Equal(
            (expectedPage, expectedPageSize),
            Pagination.Normalize(page, pageSize));
    }

    [Fact]
    public void Normalize_RespectsRouteSpecificLimit()
    {
        Assert.Equal((1, 200), Pagination.Normalize(1, 500, maxPageSize: 200));
    }
}
