namespace SmartWorkerAutomation.DataProvider.Interface;

public interface ILogServices
{
    void LogInformation(string message, params object[] args);
    void LogError(string message, params object[] args);
    void LogWarning(string message, params object[] args);
}
