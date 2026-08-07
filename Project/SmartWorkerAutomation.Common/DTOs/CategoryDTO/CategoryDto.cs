namespace SmartWorkerAutomation.Common.DTOs.CategoryDTO;

public class CategoryCreateDto
{
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal DiscountPct { get; set; }
    public int DisplayOrder { get; set; }
}

public class CategoryUpdateDto : CategoryCreateDto
{
    public int CategoryId { get; set; }
}

public class CategoryResponseDto
{
    public int CategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal DiscountPct { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedOn { get; set; }

}
