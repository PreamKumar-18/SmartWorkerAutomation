using System;
using System.Security.Claims;
using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Automation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWorkerAutomation.DataProvider.Automation;

namespace SmartWorkerAutomation.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    //[HttpPost("register")]
    //public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    //{
    //    if (!ModelState.IsValid)
    //    {
    //        return BadRequest(ModelState);
    //    }

    //    var response = await _userService.RegisterAsync(request);
    //    if (!response.Success)
    //    {
    //        return BadRequest(response);
    //    }

    //    return Ok(response);
    //}

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (request.OrgId <= 0)
        {
            return BadRequest(new AuthResponse { Success = false, Message = "OrgId is required." });
        }

        // TODO: replace with real seeded ids once confirmed (see UserController.Create)
        var roleId = request.RoleId > 0 ? request.RoleId : 3;
        var accessTypeId = request.AccessTypeId > 0 ? request.AccessTypeId : 1;

        var response = await _userService.RegisterAsync(request, request.OrgId, roleId, accessTypeId);
        if (!response.Success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var response = await _userService.LoginAsync(request);
        if (!response.Success)
        {
            return Unauthorized(response);
        }

        return Ok(response);
    }

    [HttpPut("update")]
    public async Task<IActionResult> Update([FromBody] UpdateUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var response = await _userService.UpdateUserAsync(request);
        if (!response.Success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Authenticated user-management list - Admin/SuperAdmin only. Backs the
    /// user-management screen's user table.
    /// </summary>
    [Authorize(AuthenticationSchemes = "CustomTokenScheme")]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        bool isSuperAdmin = User.IsInRole("SuperAdmin")
                            || string.Equals(User.FindFirst(ClaimTypes.Role)?.Value, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
        bool isAdmin = User.IsInRole("Admin")
                            || string.Equals(User.FindFirst(ClaimTypes.Role)?.Value, "Admin", StringComparison.OrdinalIgnoreCase);

        if (!isSuperAdmin && !isAdmin)
        {
            return StatusCode(403, new { message = "You are not authorized to view users." });
        }

        var users = await _userService.GetAllUsersAsync();
        foreach (var u in users)
        {
            u.Password = string.Empty;
        }

        return Ok(users);
    }

    /// <summary>
    /// Authenticated user creation for the user-management screen -
    /// Admin/SuperAdmin only. Distinct from the public /register
    /// self-registration endpoint above. Enforces that an 'Admin' creator
    /// can only create 'User' accounts (see UserService.CreateUserAsync).
    /// </summary>
    /// 
    [Authorize(AuthenticationSchemes = "CustomTokenScheme")]
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var creatorRoleName = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        var orgIdClaim = User.FindFirst("orgid")?.Value;
        if (!int.TryParse(orgIdClaim, out var creatorOrgId))
        {
            return Unauthorized(new AuthResponse { Success = false, Message = "Could not resolve organisation from token." });
        }

        // TODO: confirm actual seeded ids for userrole/useraccesstype and
        // replace these placeholders - currently assuming "User" role = 3 and
        // a default "FullAccess"-equivalent = 1, per the earlier seed draft
        // (SuperAdmin=1, Admin=2, User=3). Ideally these come from the request
        // body (request.RoleId/request.AccessTypeId) once RegisterRequest has
        // those fields, not hardcoded here.
        var roleId = request.RoleId > 0 ? request.RoleId : 3;
        var accessTypeId = request.AccessTypeId > 0 ? request.AccessTypeId : 1;

        var response = await _userService.CreateUserAsync(request, creatorRoleName, creatorOrgId, roleId, accessTypeId);
        if (!response.Success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }
    //[Authorize]
    //[HttpPost("create")]
    //public async Task<IActionResult> Create([FromBody] RegisterRequest request)
    //{
    //    if (!ModelState.IsValid)
    //    {
    //        return BadRequest(ModelState);
    //    }

    //    var creatorRoleName = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
    //    var response = await _userService.CreateUserAsync(request, creatorRoleName);
    //    if (!response.Success)
    //    {
    //        return BadRequest(response);
    //    }

    //    return Ok(response);
    //}

    [Authorize(AuthenticationSchemes = "CustomTokenScheme")]
    [HttpPost("changePassword")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var orgIdClaim = User.FindFirst("orgid")?.Value;
        if (!int.TryParse(orgIdClaim, out var orgId))
        {
            return Unauthorized(new AuthResponse { Success = false, Message = "Could not resolve organisation from token." });
        }

        var response = await _userService.ChangePasswordAsync(request, orgId);
        if (!response.Success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    //[HttpPost("changePassword")]
    //public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    //{
    //    if (!ModelState.IsValid)
    //    {
    //        return BadRequest(ModelState);
    //    }

    //    var response = await _userService.ChangePasswordAsync(request);
    //    if (!response.Success)
    //    {
    //        return BadRequest(response);
    //    }

    //    return Ok(response);
    //}

    /// <summary>
    /// Registers/refreshes this device's push token, called right after
    /// login (web or mobile). userId is read from the JWT, not the request
    /// body, so callers can only register devices against their own account.
    /// </summary>
    [Authorize(AuthenticationSchemes = "CustomTokenScheme")]
    [HttpPost("register-device")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                           ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new DeviceRegistrationResponse { Success = false, Message = "Could not resolve user from token." });
        }

        var response = await _userService.RegisterDeviceAsync(userId, request);
        if (!response.Success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }
}
