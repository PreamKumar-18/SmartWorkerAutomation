namespace SmartWorkerAutomation.Common.UserDto;

public class CreateUserDto
{
    
    public string UserName { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public int PhoneNumber { get; set; }

}

public class UpdateUserDto
{
    public int UserId { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public int PhoneNumber { get; set; }

    public string Password { get; set; }
    public bool IsActive { get; set; }

}

public class UserResponseDto
{
    public int UserId { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public int PhoneNumber { get; set; }

    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

