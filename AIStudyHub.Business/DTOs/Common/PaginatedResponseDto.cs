namespace AIStudyHub.Business.DTOs.Common;

public sealed record PaginatedResponseDto<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public static class PaginatedResponseDto
{
    public static PaginatedResponseDto<T> Create<T>(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        var totalPages = pageSize > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0;
        return new PaginatedResponseDto<T>(items, totalCount, page, pageSize, totalPages);
    }
}
