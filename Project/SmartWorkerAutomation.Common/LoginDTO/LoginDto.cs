namespace SmartWorkerAutomation.Common.LoginDTO;

public class MobileOTP
{
    public string PhoneNumber { get; set; } = null!;
}

public class CustomerVerifyOTP
{
    public string Phone { get; set; }  // Phone or Email
    public string Otp { get; set; }
}

public class Login
{
    public string UserIdentifier { get; set; }  // Phone or Email
    public string Pin { get; set; }
}
