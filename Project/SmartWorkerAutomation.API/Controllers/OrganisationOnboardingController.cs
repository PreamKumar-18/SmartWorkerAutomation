using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWorkerAutomation.Common.Automation;
using SmartWorkerAutomation.DataProvider.Automation;
using SmartWorkerAutomation.DataProvider.Interface.Automation;

namespace SmartWorkerAutomation.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrganisationOnboardingController : ControllerBase
{
    private readonly IOrganisationOnboardingService _onboardingService;

    public OrganisationOnboardingController(IOrganisationOnboardingService onboardingService)
    {
        _onboardingService = onboardingService;
    }

    /// <summary>
    /// Onboards a new client organisation - creates the organisation,
    /// organisationinfo (encrypted tenant connection string), the first
    /// userinfo Admin row, and the matching tenant-side User row.
    /// Ops-only: the client's DB must already exist and have the tenant
    /// schema applied before calling this (manual step for now).
    /// </summary>
    [HttpPost("onboard")]
    //[Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Onboard([FromBody] OnboardOrganisationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrganisationName)
            || string.IsNullOrWhiteSpace(request.TenantConnectionString)
            || string.IsNullOrWhiteSpace(request.AdminUsername)
            || string.IsNullOrWhiteSpace(request.AdminEmail)
            || string.IsNullOrWhiteSpace(request.AdminPassword))
        {
            return BadRequest(new OnboardOrganisationResponse
            {
                Success = false,
                Message = "OrganisationName, TenantConnectionString, AdminUsername, AdminEmail, and AdminPassword are all required."
            });
        }

        var result = await _onboardingService.OnboardAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}