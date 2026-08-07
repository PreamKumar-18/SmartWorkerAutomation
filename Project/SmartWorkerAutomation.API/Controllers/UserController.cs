//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using SmartWorkerAutomation.Common.Common;
//using SmartWorkerAutomation.Common.DTOs.CommonDTO;
//using SmartWorkerAutomation.Common.DTOs.UserDTO;
//using SmartWorkerAutomation.DataProvider.Interface;

//namespace SmartWorkerAutomation.API.Controllers;

////[Authorize(AuthenticationSchemes = "CustomTokenScheme")]
//[Route("api/[controller]")]
//[ApiController]
//public class UserController : ControllerBase
//{
//    private readonly IUserServices _userServices;

//    public UserController(IUserServices userServices)
//    {
//        _userServices = userServices;
//    }

//    [HttpPost("Create")]
//    [ProducesResponseType(typeof(GenericResponse<UserResponseDto>), 200)]
//    public async Task<IActionResult> Create([FromBody] UserCreateDto request)
//    {
//        var response = await _userServices.CreateAsync(request);
//        return Ok(response);
//    }

//    [HttpPost("Details")]
//    [ProducesResponseType(typeof(GenericPaginatedRes<List<UserResponseDto>>), 200)]
//    public async Task<IActionResult> GetDetails([FromBody] PaginationFilterDto request)
//    {
//        var response = await _userServices.GetDetailsAsync(request.PageIndex, request.PageSize, request.SortBy, request.SortAsc);
//        return Ok(response);
//    }

//    [HttpPost("Update")]
//    [ProducesResponseType(typeof(GenericResponse<UserResponseDto>), 200)]
//    public async Task<IActionResult> Update([FromBody] UserUpdateDto request)
//    {
//        var response = await _userServices.UpdateAsync(request);
//        return Ok(response);
//    }

//    [HttpGet("{id}")]
//    [ProducesResponseType(typeof(GenericResponse<UserResponseDto>), 200)]
//    public async Task<IActionResult> GetById(int id)
//    {
//        var response = await _userServices.GetByIdAsync(id);
//        return Ok(response);
//    }

//    [HttpPost("softDelete")]
//    [ProducesResponseType(typeof(GenericResponse<bool>), 200)]
//    public async Task<IActionResult> SoftDelete([FromBody] SingleIdFilterDto request)
//    {
//        var response = await _userServices.SoftDeleteAsync(request.Id);
//        return Ok(response);
//    }
//}