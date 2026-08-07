using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace SmartWorkerAutomation.Common.UserDTO;

public class CreateSuperAdminRq
{
    [Required(ErrorMessage ="CommunityId is Required")]
    [Range(1, int.MaxValue, ErrorMessage = "CommunityId must be greater than 0")]
    public int CommunityId { get; set; }

    [Required(ErrorMessage = "User Name is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "User Name must be between 1 and 50 characters.")]
    public string UserName { get; set; }

    [Required(ErrorMessage = "Phone Number is required.")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must be exactly 10 digits.")]
    public string PhoneNumber { get; set; }
}

public class UpdateUserProfile
{
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must be exactly 10 digits.")]
    public string PhoneNumber { get; set; }

    [StringLength(50, MinimumLength = 1, ErrorMessage = "User Name must be between 1 and 50 characters.")]
    public string UserName { get; set; }

    [MaxLength(255)]
    public string? Description { get; set; }
    public IFormFile? ProfilePic { get; set; }
    public IFormFile? CoverPic { get; set; }
}