using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;

namespace SmartWorkerAutomation.Configuration.MiddleWare;

public class ModelStateInvalidDetailsCaptureFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(ms => ms.Value.Errors.Count > 0)
                .SelectMany(kvp => kvp.Value.Errors.Select(e => new
                {
                    Field = kvp.Key,
                    Error = e.ErrorMessage
                }))
                .ToList();

            context.Result = new BadRequestObjectResult(new
            {
                isSuccess = false,
                message = "Validation failed",
                errors
            });
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // No implementation needed for this example
    }
}
