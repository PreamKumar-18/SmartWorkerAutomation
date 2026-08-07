namespace SmartWorkerAutomation.Common.DTOs.CommonDTO;

public class PaginationFilterDto
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 100;
    public string? SortBy { get; set; }
    public bool SortAsc { get; set; } = false;
}

public class PurchaseDetailsFilterDto : PaginationFilterDto
{
    public System.DateTime? FromDate { get; set; }
    public System.DateTime? ToDate { get; set; }
}

public class SingleIdFilterDto
{
    public int Id { get; set; }
}
