using SmartWorkerAutomation.Common.Enum;

namespace SmartWorkerAutomation.Common.Common;

public class GenericResponse
{
    public GenericResponse(RSCodeEnum codeEnum, string? message = null)
    {
        switch (codeEnum)
        {
            case RSCodeEnum.Success:
                ResponseStatus = new ResponseStatus
                {
                    StatusCode = "0",
                    StatusMessage = message ?? "Success"
                };
                break;

            case RSCodeEnum.NoRecordFound:
                ResponseStatus = new ResponseStatus
                {
                    StatusCode = "9",
                    StatusMessage = "No Record Found"
                };
                break;

            case RSCodeEnum.Failure:
                ResponseStatus = new ResponseStatus
                {
                    StatusCode = "1",
                    StatusMessage = message ?? "Failure"
                };
                break;

        }
    }

    public ResponseStatus? ResponseStatus { get; set; } 
}

public class GenericResponse<T> : GenericResponse
{
    public GenericResponse(RSCodeEnum codeEnum, T? data, string? message = null)
        : base(codeEnum, message)
    {
        Data = data;
    }

    public T? Data { get; set; }
}

public class GenericPaginatedRes<T> : GenericResponse<T>
{
    public GenericPaginatedRes(RSCodeEnum codeEnum, T? data, Paging paging, string? message = null)
        : base(codeEnum, data, message)
    {
        Paging = paging;
    }

    public Paging Paging { get; set; }
}

public class ResponseStatus
{
    public string StatusCode { get; set; }
    public string? StatusMessage { get; set; }
}