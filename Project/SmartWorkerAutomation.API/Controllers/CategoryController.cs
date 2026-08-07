using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Common.DTOs.CategoryDTO;
using SmartWorkerAutomation.Common.DTOs.CommonDTO;
using SmartWorkerAutomation.DataProvider.Interface;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.API.Controllers;

[Route("api/1.0/[Controller]")]
[Authorize(AuthenticationSchemes = "CustomTokenScheme")]
    [ApiController]
public class CategoryController : ControllerBase
{
    private readonly ICategoryServices _services;

    public CategoryController(ICategoryServices services)
    {
        _services = services;
    }

    [HttpPost("Create")]
    [ProducesResponseType(typeof(GenericResponse<CategoryResponseDto>), 200)]
    public async Task<IActionResult> Create([FromBody] CategoryCreateDto request)
    {
        var response = await _services.CreateAsync(request);
        return Ok(response);
    }

    [HttpPost("Details")]
    [ProducesResponseType(typeof(GenericPaginatedRes<List<CategoryResponseDto>>), 200)]
    public async Task<IActionResult> GetDetails([FromBody] PaginationFilterDto request)
    {
        var response = await _services.GetDetailsAsync(request.PageIndex, request.PageSize, request.SortBy, request.SortAsc);
        return Ok(response);
    }

    [HttpPost("Update")]
    [ProducesResponseType(typeof(GenericResponse<CategoryResponseDto>), 200)]
    public async Task<IActionResult> Update([FromBody] CategoryUpdateDto request)
    {
        var response = await _services.UpdateAsync(request);
        return Ok(response);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(GenericResponse<CategoryResponseDto>), 200)]
    public async Task<IActionResult> GetById(int id)
    {
        var response = await _services.GetByIdAsync(id);
        return Ok(response);
    }

    [HttpPost("softDelete")]
    [ProducesResponseType(typeof(GenericResponse<bool>), 200)]
    public async Task<IActionResult> SoftDelete([FromBody] SingleIdFilterDto request)
    {
        var response = await _services.SoftDeleteAsync(request.Id);
        return Ok(response);
    }
}
