using System.ComponentModel.DataAnnotations;

namespace BookHiveLibrary.ViewModels
{
    public class VerifyPhoneOtpViewModel
    {
        [Required]
        public string Email { get; set; } = "";

        [Required]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be 6 digits.")]
        public string Code { get; set; } = "";
    }
}
