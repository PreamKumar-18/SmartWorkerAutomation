using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartWorkerAutomation.DataProvider.Interface.Automation;
using System.Security.Claims;

namespace SmartWorkerAutomation.API.Controllers;

[Authorize(AuthenticationSchemes = "CustomTokenScheme")]
[Route("api/[controller]")]
[ApiController]
public class BranchController : ControllerBase
{
    private readonly IBranchService _branchService;


    public BranchController(IBranchService branchService)
    {
        _branchService = branchService;
    }

    /// <summary>
    /// Returns the branches a given userId has access to, and which one is
    /// their primary. SuperAdmin gets every active branch regardless of
    /// user_branch mappings; Admin/User get only their mapped branches.
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserBranches(int userId)
    {
        var branches = await _branchService.GetBranchesForUserAsync(userId);
        return Ok(branches);
    }
}
