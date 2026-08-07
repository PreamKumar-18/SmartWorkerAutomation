namespace SmartWorkerAutomation.DataProvider.Interface;

public interface IUserContextServices
{
    int GetCurrentUserId();
    string GetCurrentUserName();
}
