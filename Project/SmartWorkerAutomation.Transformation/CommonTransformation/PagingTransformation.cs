using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Common.DTOs.CommonDTO;

namespace SmartWorkerAutomation.Transformation.CommonTransformation;

public static class PagingTransformation
{
    public static Paging GetPaging(PaginationFilterDto paging)
        => new Paging { PageIndex = paging.PageIndex, PageSize = paging.PageSize, SortBy = paging.SortBy ?? string.Empty, SortAsc = paging.SortAsc };
}
