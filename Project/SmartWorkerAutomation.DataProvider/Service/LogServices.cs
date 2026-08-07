using SmartWorkerAutomation.DataProvider.Interface;
using Serilog;

namespace SmartWorkerAutomation.DataProvider.Service;

public class LogServices : ILogServices
{
    private readonly Serilog.ILogger _logger;

    public LogServices()
    {

        _logger = Serilog.Log.ForContext<LogServices>();
    }

    // Log Information level messages
    public void LogInformation(string message, params object[] args)
    {
        _logger.Information(message, args);
    }

    // Log Error level messages
    public void LogError(string message, params object[] args)
    {
        _logger.Error(message, args);
    }

    // Log Warning level messages
    public void LogWarning(string message, params object[] args)
    {
        _logger.Warning(message, args);
    }
}
