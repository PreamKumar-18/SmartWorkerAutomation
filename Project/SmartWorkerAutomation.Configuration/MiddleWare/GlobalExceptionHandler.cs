using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Common.Enum;
using SmartWorkerAutomation.Common.ExceptionDTO;
using SmartWorkerAutomation.DataProvider.Interface;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using System.Net;
using System.Text;
using System.Text.Json;

namespace SmartWorkerAutomation.Configuration.MiddleWare;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogServices _logger;

    public GlobalExceptionHandler(ILogServices logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        httpContext.Response.ContentType = "application/json";
        httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        ExceptionRs exceptionRs = new ExceptionRs();

        exceptionRs.ExName = exception.GetType().Name;
        exceptionRs.ExString = exception.ToString();
        exceptionRs.ExReasonPharse = exception.Message ?? string.Empty;
        exceptionRs.BaseExString = exception.GetBaseException().Message;
        exceptionRs.InnerExString = exception.InnerException?.Message ?? string.Empty;

        var response = new GenericResponse(
            RSCodeEnum.Failure,
            exception.Message
        );

        exceptionRs.NewReponseBody = JsonSerializer.Serialize(response);

        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(response),
            cancellationToken
        );

        await LogExceptionAsync(exceptionRs, httpContext.Request);

        return true;
    }

    private async Task LogExceptionAsync(ExceptionRs exResponse, HttpRequest request)
    {
        StringBuilder sbData = new();
        sbData.AppendLine($"--- Exception Log Start --- {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sbData.AppendLine($"Exception Type : {exResponse.ExName}");
        sbData.AppendLine($"Reason Phrase  : {exResponse.ExReasonPharse}");

        try
        {
            (string header, string body) = await GetRequestDetails(request);
            
            sbData.AppendLine("\n[ REQUEST DETAILS ]");
            if (!string.IsNullOrEmpty(header)) sbData.AppendLine($"Headers : \n{header}");
            if (!string.IsNullOrEmpty(body)) sbData.AppendLine($"Body : \n{body}");
        }
        catch (Exception requestEx)
        {
            sbData.AppendLine($"\n[ REQUEST CAPTURE FAILED ]\nError: {requestEx.Message}");
        }

        sbData.AppendLine("\n[ RESPONSE PAYLOAD GENERATED ]");
        sbData.AppendLine(exResponse.NewReponseBody ?? string.Empty);

        sbData.AppendLine("\n[ FULL EXCEPTION DETAILS ]");
        sbData.AppendLine($"Message: {exResponse.ExString}");
        if (!string.IsNullOrEmpty(exResponse.BaseExString)) sbData.AppendLine($"Base Exception: {exResponse.BaseExString}");
        if (!string.IsNullOrEmpty(exResponse.InnerExString)) sbData.AppendLine($"Inner Exception: {exResponse.InnerExString}");
        
        sbData.AppendLine("\n--- Exception Log End ---");
        
        _logger.LogError(sbData.ToString());
    }

    private async Task<(string, string)> GetRequestDetails(HttpRequest request)
    {
        string header = string.Empty;
        string requestBody = string.Empty;
        string requestDetailString = string.Empty;

        if (request != null && request.Body.CanRead)
        {
            object? bodyObj = null;

            object? requestDetails = new
            {
                url = request.GetDisplayUrl(),
                requestMethod = request.Method,
                Header = request.Headers,
            };

            requestDetailString = JsonSerializer.Serialize(requestDetails);

            try
            {
                requestBody = await ReadRequestBodyAsync(request);
            }
            catch
            {
                requestBody = "Request Body Too Large to Read";
            }
        }

        return (requestDetailString, requestBody);
    }

    private async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        if (request == null || !request.Body.CanSeek) return string.Empty;

        request.Body.Position = 0;
        using (var reader = new StreamReader(request.Body, Encoding.UTF8, true, 1024, true))
        {
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
            return body;
        }
    }
}
