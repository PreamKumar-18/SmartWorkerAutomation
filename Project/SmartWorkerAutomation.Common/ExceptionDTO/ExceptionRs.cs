using System.Net;

namespace SmartWorkerAutomation.Common.ExceptionDTO;

public class ExceptionRs
{
    public HttpStatusCode StatusCode { get; set; }
    public string ExName { get; set; } = string.Empty;
    public string ExString { get; set; } = string.Empty;
    public string ExReasonPharse { get; set; } = string.Empty;
    public string NewReponseBody { get; set; } = string.Empty;
    public string BaseExString { get; set; } = string.Empty;
    public string InnerExString { get; set; } = string.Empty;
}
