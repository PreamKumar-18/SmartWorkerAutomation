using System;
using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Automation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWorkerAutomation.DataProvider.Automation;

namespace SmartWorkerAutomation.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : ControllerBase
{
    private readonly IConfigurationService _configurationService;

    public ConfigurationController(IConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    [HttpPost("RuleAlertUpdate")]
    public async Task<IActionResult> UpdateRuleAlert([FromBody] UpdateRuleAlertConfigRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var success = await _configurationService.UpdateRuleAlertConfigAsync(request);
            if (!success)
            {
                return NotFound(new { message = "Rule alert configuration not found or update failed." });
            }

            return Ok(new { message = "Rule alert configuration updated successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating rule alert configuration.", details = ex.Message });
        }
    }

    [HttpPost("EmailTemplateUpdate")]
    public async Task<IActionResult> UpdateEmailTemplate([FromBody] UpdateEmailTemplateConfigRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var success = await _configurationService.UpdateEmailTemplateConfigAsync(request);
            if (!success)
            {
                return NotFound(new { message = "Email template configuration not found or update failed." });
            }

            return Ok(new { message = "Email template configuration updated successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating email template configuration.", details = ex.Message });
        }
    }
}
