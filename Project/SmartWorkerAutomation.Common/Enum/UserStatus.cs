namespace SmartWorkerAutomation.Common.Enum;

public enum UserStatus
{
    Active = 1,
    Inactive = 2,
    Blocked = 3,
    NewUser = 4,
    Pending = 5,
    AccessDenied = 6
}

public enum CodeType
{
    Barcode = 1,
    QRCode = 2,
    Both = 3
}

public enum DashboardFilter
{
    Today = 1,
    ThisWeek = 2,
    ThisMonth = 3,
    ThisYear = 4
}