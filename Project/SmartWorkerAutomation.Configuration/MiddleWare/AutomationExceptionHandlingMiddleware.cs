using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SmartWorkerAutomation.Configuration.MiddleWare;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "An unhandled exception occurred during request: {Method} {Path}{QueryString}",
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var message = "An internal server error occurred.";
        var details = exception.Message;

        if (exception is System.Net.Http.HttpRequestException httpEx)
        {
            context.Response.StatusCode = httpEx.StatusCode.HasValue 
                ? (int)httpEx.StatusCode.Value 
                : (int)HttpStatusCode.BadGateway;
            
            message = "An error occurred while calling the external integration service.";
            details = $"External HTTP call failed with status: {httpEx.StatusCode}. Details: {httpEx.Message}";
        }

        var response = new
        {
            StatusCode = context.Response.StatusCode,
            Message = message,
            Details = details
        };

        var json = JsonSerializer.Serialize(response);
        await context.Response.WriteAsync(json);
    }
}
