namespace AIStudyHub.Business.DTOs.Common;

public class PaginationParams
{
    private const int MaxLimit = 100;
    private int _limit = 10;

    public int Offset { get; set; } = 0;

    public int Limit
    {
        get => _limit;
        set => _limit = (value > MaxLimit) ? MaxLimit : (value < 1 ? 1 : value);
    }

    public string? SearchTerm { get; set; }
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; } = false;
}
