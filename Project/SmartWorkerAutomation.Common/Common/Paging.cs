namespace SmartWorkerAutomation.Common.Common;

public class Paging
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public string? SortBy { get; set; }
    public bool SortAsc { get; set; }
    public int TotalRecords { get; set; } = 0;
    public int PageCount { get; set; } = 0;
    public int MaxPageSize { get; set; } = 1000;
}
