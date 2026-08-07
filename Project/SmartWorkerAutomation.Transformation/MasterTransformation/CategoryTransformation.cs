
using SmartWorkerAutomation.Common.DTOs.CategoryDTO;
using SmartWorkerAutomation.Core.Models;

namespace SmartWorkerAutomation.Transformation.MasterTransformation;

public static class CategoryTransformation
{
    public static Category ToEntity(CategoryCreateDto dto)
    {
        return new Category
        {
            CategoryCode = dto.CategoryCode,
            CategoryName = dto.CategoryName,
            Description = dto.Description,
            DiscountPct = dto.DiscountPct,
            DisplayOrder = dto.DisplayOrder,
            IsActive = true,
            CreatedOn = System.DateTime.Now,
            UpdatedOn = System.DateTime.Now
        };
    }

    public static CategoryResponseDto ToDto(Category entity)
    {
        return new CategoryResponseDto
        {
            CategoryId = entity.CategoryId,
            CategoryCode = entity.CategoryCode,
            CategoryName = entity.CategoryName,
            Description = entity.Description,
            DiscountPct = entity.DiscountPct,
            DisplayOrder = entity.DisplayOrder,
            IsActive = entity.IsActive,
            CreatedOn = entity.CreatedOn
        };
    }

    public static void UpdateEntity(Category entity, CategoryUpdateDto dto)
    {
        entity.CategoryCode = dto.CategoryCode;
        entity.CategoryName = dto.CategoryName;
        entity.Description = dto.Description;
        entity.DiscountPct = dto.DiscountPct;
        entity.DisplayOrder = dto.DisplayOrder;
        entity.UpdatedOn = System.DateTime.Now;
    }
}
