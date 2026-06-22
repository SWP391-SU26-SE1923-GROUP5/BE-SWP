using System.Collections.Generic;

namespace AIStudyHub.Business.DTOs.Common;

public class PagedResultDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; }

    public PagedResultDto() { }

    public PagedResultDto(IReadOnlyList<T> items, int totalCount, int offset, int limit)
    {
        Items = items;
        TotalCount = totalCount;
        Offset = offset;
        Limit = limit;
    }
}
